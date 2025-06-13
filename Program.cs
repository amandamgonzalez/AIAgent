// using System;
// using System.IO;
// using System.Threading.Tasks;
// using Microsoft.SemanticKernel.Agents;
// using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
// using Azure.Core.Diagnostics;
// using Microsoft.SemanticKernel.Connectors.OpenAI;
// using System.Text.Json.Serialization;
// using System.Text.Json;
// using Newtonsoft.Json.Schema;
// using Newtonsoft.Json.Schema.Generation;

using Azure.Identity;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Agents.AzureAI;
using Azure.AI.Agents.Persistent; // dotnet add package Microsoft.SemanticKernel.Agents.AzureAI --prerelease


namespace AzureAIAgentSample
{
    public static class Program
    {
        public static async Task Main()
        {
            // load configuration
            Settings settings = new();

            //create a client to interact with Azure AI Agent
            PersistentAgentsClient client = AzureAIAgent.CreateAgentsClient(settings.AzureAIAgent.Endpoint, new AzureCliCredential());

            // create a thread to hold the conversation
            AzureAIAgentThread thread = new(client);

            // create the agent with a structured output format
            PersistentAgent definition = await client.Administration.CreateAgentAsync(
                settings.AzureAIAgent.ChatModel,
                name: "PIIAgent",
                description: "Extract any Personally Identifiable Information (PII) from files",
                responseFormat: BinaryData.FromString(
                    """
                    {
                    "type": "json_schema",
                    "json_schema": {
                    "type": "object",
                    "name": "PIIExtraction",
                    "schema": {
                        "type": "object",
                        "properties": {
                            "name": { "type": "string" },
                            "company_email": { "type": "string" },
                            "personal_email": { "type": "string" },
                            "personal_phone_number": { "type": "string" },
                            "company_phone_number": { "type": "string" },
                            "personal_address": { "type": "string" },
                            "company_ship_to_address": { "type": "string" },
                            "company_ship_from_address": { "type": "string" }
                            },
                        "required": [ "name", "company_email", "personal_email", "personal_phone_number", "company_phone_number", 
                                    "personal_address", "company_ship_to_address", "company_ship_from_address" ],
                        "additionalProperties": false
                                    },
                        "strict": true
                        }
                    }
                    """
                    ));

            // create the agent with the definition
            AzureAIAgent agent = new(
                definition,
                client);

            // local function to create a message with an image reference
            ChatMessageContent CreateMessageWithImageReference(string input, string fileId)
                => new(AuthorRole.User, [new TextContent(input), new FileReferenceContent(fileId)]);

            try
            {
                // prompt the user for a file path
                Console.WriteLine("Please enter the file path:");
                string? filePath = Console.ReadLine();

                if (!File.Exists(filePath))
                {
                    Console.WriteLine("File does not exist. Please provide a valid file path.");
                    return;
                }

                using (FileStream imageStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    // upload the image and get the file id
                    PersistentAgentFileInfo fileInfo = await client.Files.UploadFileAsync(imageStream, PersistentAgentFilePurpose.Agents, Path.GetFileName(filePath));

                    // create a message with the image reference 
                    ChatMessageContent imageMessage = CreateMessageWithImageReference("Extract any Personally Identifiable Information (PII) from image.", fileInfo.Id);

                    // invoke the agent
                    Console.WriteLine("Structured Output:");
                    await foreach (ChatMessageContent response in agent.InvokeAsync(imageMessage, thread))
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(response.Content))
                            {
                                Console.WriteLine(response.Content);
                            }
                            else
                            {
                                Console.WriteLine("Response content is null or empty.");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to process structured output: {ex.Message}");
                        }
                    }
                }
            }

            catch (System.Text.Json.JsonException ex)
            {
                Console.WriteLine($"JSON Exception: {ex.Message}");
                Console.WriteLine("Ensure the JSON data is complete and correctly formatted.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled Exception: {ex.Message}");
            }
            finally
            {
                if (thread != null && thread.Id != null)
                {
                    await thread.DeleteAsync();
                }
                Console.WriteLine("File scanning completed.");
            }
        }
    }
}