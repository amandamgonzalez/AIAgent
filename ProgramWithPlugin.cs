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
    public static class ProgramWithPlugin
    {
        public static async Task Main()
        {
            // load configuration
            Settings settings = new();

            //create a client to interact with Azure AI Agent
            PersistentAgentsClient client = AzureAIAgent.CreateAgentsClient(settings.AzureAIAgent.Endpoint, new AzureCliCredential());

            // create the agent with a structured output format
            PersistentAgent definition = await client.Administration.CreateAgentAsync(
                settings.AzureAIAgent.ChatModel,
                name: "PIIAgent",
                description: @"You are a PII Extraction Agent. Your job is to extract any Personally Identifiable Information (PII) from files you receive. 
                If someone asks 'Who are you?', always respond with 'I am PIIAgent.' You have access to a plugin that can help you with this task.",
                instructions: @"You are a PII Extraction Agent. Your job is to extract any Personally Identifiable Information (PII) from files you receive.");

            // APPROACH ONE: service collection approach

            // var serviceCollection = new ServiceCollection();
            // serviceCollection.AddSingleton(client);
            // serviceCollection.AddSingleton<TokenCredential>(new AzureCliCredential());
            // serviceCollection.AddSingleton<Kernel>(sp =>
            // {
            //     return Kernel.CreateBuilder()
            //         .AddAzureOpenAIChatCompletion(
            //             settings.AzureOpenAI.ChatModelDeployment,
            //             settings.AzureOpenAI.Endpoint,
            //             sp.GetRequiredService<TokenCredential>())
            //         .Build();
            // });

            // var serviceProvider = serviceCollection.BuildServiceProvider();

            // APPROACH TWO: semantic kernel repo approach
            // var builder = Kernel.CreateBuilder();
            // builder.Services.AddSingleton(client);
            // builder.Services.AddSingleton<TokenCredential>(new AzureCliCredential());
            // var kernel = builder.Build();

            // APPROACH THREE: using Kernel.CreateBuilder() directly, and adding it to the Agent below
            Kernel kernel = Kernel.CreateBuilder()
                    .AddAzureOpenAIChatCompletion(
                        settings.AzureOpenAI.ChatModelDeployment,
                        settings.AzureOpenAI.Endpoint,
                        new AzureCliCredential())
                    .Build();

            var agent = new AzureAIAgent(definition, client)
            {
                Kernel = kernel
            };

            agent.Kernel.Plugins.Add(KernelPluginFactory.CreateFromType<PIIExtractionPlugin>());
            // automatically passes the kernel to the PIIExtractionPlugin when called

            AgentThread thread = new AzureAIAgentThread(client);
            
            try
            {
                while (true)
                {
                    // agent.InvokeAsync(new ChatMessageContent(AuthorRole.System, "You are an agent. Your name is PII agent"), thread);

                    // ask for user input
                    Console.WriteLine("Please enter your request (or type 'exit' to quit):");
                    string? userInput = Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(userInput))
                    {
                        Console.WriteLine("Input cannot be empty. Please provide a valid request.");
                        continue;
                    }

                    if (userInput.Equals("exit", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Exiting the program. Goodbye!");
                        break;
                    }

                    // pass the user input to the agent for analysis
                    var agentResponse = agent.InvokeAsync(new ChatMessageContent(AuthorRole.User, userInput), thread);

                    await foreach (var response in agentResponse)
                    {
                        Console.WriteLine("Agent Response:");
                        Console.WriteLine(response.Message?.ToString() ?? "Message is null");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
    }
}