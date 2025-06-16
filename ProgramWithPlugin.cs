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
            // Llad configuration
            Settings settings = new();

            // create a kernel builder
            var builder = Kernel.CreateBuilder();

            builder.Services.AddSingleton(new PersistentAgentsClient(settings.AzureAIAgent.Endpoint, new AzureCliCredential()));

            builder.Services.AddSingleton<TokenCredential>(sp => new AzureCliCredential());

            // ensure IChatCompletionService is registered

            builder.Services.AddSingleton<IChatCompletionService>(sp => new AzureOpenAIChatCompletionService(
                settings.AzureOpenAI.ChatModelDeployment,
                settings.AzureOpenAI.Endpoint,
                // this works with Azure OpenAI but not with AzureAIAgents
                sp.GetRequiredService<TokenCredential>()));


            // build the kernel before registering the plugin
            var kernel = builder.Build();

            // builder.Services.AddSingleton(kernel);
            // Register the PIIExtractionPlugin using CreateFromObject
            // var plugin = new PIIExtractionPlugin(kernel);
            // kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(plugin));

            // access the kernel services
            var client = kernel.Services.GetRequiredService<PersistentAgentsClient>();

            // create a thread to hold the conversation
            // AzureAIAgentThread thread = new(client);

            AgentThread thread = new AzureAIAgentThread(client);

            // create the agent
            PersistentAgent definition = await client.Administration.CreateAgentAsync(
                settings.AzureAIAgent.ChatModel,
                name: "PIIAgent",
                description: "Extract any Personally Identifiable Information (PII) from files. You have access to a plugin that can extract PII from images."
                );

            // create the agent with the definition and link the kernel
            AzureAIAgent agent = new(
                definition,
                client)
            {
                Kernel = kernel,
            };

            // Add the PIIExtractionPlugin to the agent's kernel
            agent.Kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(new PIIExtractionPlugin(kernel)));

            // Add a filter to log function invocations
            agent.Kernel.FunctionInvocationFilters.Add(new FunctionInvocationLogger());

            try
            {
                while (true)
                {
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

        private sealed class FunctionInvocationLogger : IFunctionInvocationFilter
        {
            public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
            {
                Console.WriteLine($"\nINVOKING: {context.Function.Name}");
                await next.Invoke(context);
                Console.WriteLine($"\nRESULT: {context.Result}");
            }
        }
    }
}