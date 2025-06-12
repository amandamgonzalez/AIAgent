using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Azure.Core.Diagnostics;
using Microsoft.SemanticKernel.Connectors.OpenAI;

using Plugin;


using Microsoft.SemanticKernel.Agents.AzureAI;
using Azure.AI.Agents.Persistent; // dotnet add package Microsoft.SemanticKernel.Agents.AzureAI --prerelease


namespace AgentsSample
{
    public static class Program
    {
        public static async Task Main()
        {
            // load configuration
            Settings settings = new();

            PersistentAgentsClient client = AzureAIAgent.CreateAgentsClient(settings.AzureAIAgent.Endpoint, new AzureCliCredential());

            AzureAIAgentThread thread = new(client);

            // local function to create a message with an image reference
            ChatMessageContent CreateMessageWithImageReference(string input, string fileId)
                => new(AuthorRole.User, [new TextContent(input), new FileReferenceContent(fileId)]);

            try
            {
                // i am hard coding 
                using (FileStream imageStream = new FileStream(@"C:\Users\t-amandag\PII\PIIAgentAgain\images\Screenshot 2025-06-05 095614.png", FileMode.Open, FileAccess.Read))
                {
                    // upload the image and get the file id
                    PersistentAgentFileInfo fileInfo = await client.Files.UploadFileAsync(imageStream, PersistentAgentFilePurpose.Agents, "Screenshot 2025-06-05 095614.png");

                    // create a message with the image reference
                    ChatMessageContent imageMessage = CreateMessageWithImageReference("Extract any Personally Identifiable Information (PII) from image.", fileInfo.Id);

                    PersistentAgent definition = await client.Administration.CreateAgentAsync(
                        settings.AzureAIAgent.ChatModel,
                        name: "PIIAgent",
                        description: "Extract any Personally Identifiable Information (PII) from files",
                        instructions: $"You are an agent designed to extract any Personally Identifiable Information (PII) in files you receive.\n" +
                        "If the user provides a file path, process the file by calling the Plugin to extract PII.");

                    AzureAIAgent agent = new(
                        definition,
                        client);

                    // invoke the agent
                    await foreach (ChatMessageContent response in agent.InvokeAsync(imageMessage, thread))
                    {
                        Console.WriteLine(response.Content);
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
                if (thread != null)
                {
                    await thread.DeleteAsync();
                }
                Console.WriteLine("File scanning completed.");
            }
        }
    }
}