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
            // Enable Azure SDK logging
            // AzureEventSourceListener listener = AzureEventSourceListener.CreateConsoleLogger();

            // load configuration
            Settings settings = new();

            KernelPlugin plugin = KernelPluginFactory.CreateFromType<PIIExtractionPlugin>();
            PersistentAgentsClient client = AzureAIAgent.CreateAgentsClient(settings.AzureAIAgent.Endpoint, new AzureCliCredential());

            // Debugging: Print the ChatModel value to ensure it is not null or empty
            // Console.WriteLine($"Debug: ChatModel = '{settings.AzureAIAgent.ChatModel}'");
            // had to troubleshoot an error "model" required, because I missed an s when setting the ChatModel in user secrets

            PersistentAgent definition = await client.Administration.CreateAgentAsync(
                settings.AzureAIAgent.ChatModel,
                name: "PIIAgent",
                description: " Extract any Personally Identifiable Information (PII) from files",
                instructions: $" You are an agent designed to extract any Personally Identifiable Information (PII) in files you receive.\n" +
                "If the user provides a file path, process the file and extract PII.");

            AzureAIAgent agent = new(
                definition,
                client,
                plugins: [plugin]);
            // you can add mopre stuff here, such as new KernelPromptTemplateFactory()
                
            // leaving this here for later because I feel like I'm going to have to create a thread
            // AgentThread thread = new AzureAIAgentThread(client);

            // main loop
            bool isComplete = false;
            do
            {
                Console.WriteLine();
                Console.Write("> ");
                string? input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                {
                    continue;
                }

                if (input.Trim().Equals("EXIT", StringComparison.OrdinalIgnoreCase))
                {
                    isComplete = true;
                    break;
                }

                AzureAIAgentThread agentThread = new(agent.Client);
                // what's the difference between starting here or before the main loop?
                try
                {
                    ChatMessageContent message = new(AuthorRole.User, input);
                    await foreach (ChatMessageContent response in agent.InvokeAsync(message, agentThread))
                    {
                        Console.WriteLine(response.Content);
                    }
                }
                finally
                {
                    await agentThread.DeleteAsync();
                }
            } while (!isComplete);

            Console.WriteLine("Goodbye!");
        }
    }
}