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
using Azure.Core; // Add this for TokenCredential
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Agents.AzureAI;
using Azure.AI.Agents.Persistent; // dotnet add package Microsoft.SemanticKernel.Agents.AzureAI --prerelease
using Plugin;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Agents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;


namespace AzureAIAgentSample
{
    public static class ProgramDraft
    {
        public static async Task Main()
        {
            // Load configuration
            Settings settings = new();

            // Create a kernel builder
            var builder = Kernel.CreateBuilder();

            builder.Services.AddSingleton(new PersistentAgentsClient(settings.AzureAIAgent.Endpoint, new AzureCliCredential()));

            builder.Services.AddSingleton<TokenCredential>(sp => new AzureCliCredential());

            // Ensure IChatCompletionService is registered
            builder.Services.AddSingleton<IChatCompletionService>(sp => new AzureOpenAIChatCompletionService(
                settings.AzureAIAgent.ChatModel,
                settings.AzureAIAgent.Endpoint,
                sp.GetRequiredService<TokenCredential>()));


            // Debugging to verify service registration
            Console.WriteLine("IChatCompletionService registered successfully.");

            // Build the kernel before registering the plugin
            var kernel = builder.Build();

            // Register the PIIExtractionPlugin
            var plugin = new PIIExtractionPlugin();
            kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(plugin));

            // Debugging to verify plugin registration
            Console.WriteLine("PIIExtractionPlugin registered successfully.");

            // Access the kernel services
            var client = kernel.Services.GetRequiredService<PersistentAgentsClient>();

            // Create a thread to hold the conversation
            AzureAIAgentThread thread = new(client);

            // Create the agent with a structured output format
            PersistentAgent definition = await client.Administration.CreateAgentAsync(
                settings.AzureAIAgent.ChatModel,
                name: "PIIAgent",
                description: "Extract any Personally Identifiable Information (PII) from files"//,
                // responseFormat: BinaryData.FromString(
                //     """
                //     {
                //         "type": "json_schema",
                //         "json_schema": {
                //             "type": "object",
                //             "name": "PIIExtraction",
                //             "schema": {
                //                 "type": "object",
                //                 "properties": {
                //                     "name": { "type": "string" },
                //                     "company_email": { "type": "string" },
                //                     "personal_email": { "type": "string" },
                //                     "personal_phone_number": { "type": "string" },
                //                     "company_phone_number": { "type": "string" },
                //                     "personal_address": { "type": "string" },
                //                     "company_ship_to_address": { "type": "string" },
                //                     "company_ship_from_address": { "type": "string" }
                //                 },
                //                 "required": [ "name", "company_email", "personal_email", "personal_phone_number", "company_phone_number", 
                //                     "personal_address", "company_ship_to_address", "company_ship_from_address" ],
                //                 "additionalProperties": false
                //             },
                //             "strict": true
                //         }
                //     }
                //     """
                // )
                );

            // create the agent with the definition
            AzureAIAgent agent = new(
                definition,
                client);

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

                // create chat history
                Console.WriteLine("Creating chat history using PIIExtractionPlugin...");
                var imageBytes = await File.ReadAllBytesAsync(filePath);
                var chatHistory = plugin.CreateChatHistory(imageBytes);

                // invoke the agent with the chat history
                Console.WriteLine("Invoking Azure AI agent...");

                // Access the ChatMessageContent within the response
                await foreach (var response in agent.InvokeAsync(chatHistory, thread))
                {
                    if (response is AgentResponseItem<ChatMessageContent> chatResponse)
                    {
                        Console.WriteLine("Response content:");
                        Console.WriteLine(chatResponse.Message?.ToString() ?? "Message is null");
                    }
                    else
                    {
                        Console.WriteLine("Unexpected response type:");
                        Console.WriteLine(response.ToString());
                    }
                }

                Console.WriteLine("Invoking ExtractPIIAsync...");
                var extractedPII = await plugin.ExtractPIIAsync(chatHistory, kernel);
                Console.WriteLine("Extracted PII:");
                Console.WriteLine(extractedPII);
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