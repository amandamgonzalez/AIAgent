using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

using Plugin;

namespace AgentsSample
{
    public static class Program
    {
        public static async Task Main()
        {
            // load configuration
            Settings settings = new();

            // initialize kernel
            Console.WriteLine("Creating kernel...");
            
            IKernelBuilder builder = Kernel.CreateBuilder();

            builder.AddAzureOpenAIChatCompletion(
                settings.AzureOpenAI.ChatModelDeployment,
                settings.AzureOpenAI.Endpoint,
                new AzureCliCredential());
            
            Kernel kernel = builder.Build();

            // define agent
            Console.WriteLine("Defining agent...");
            ChatCompletionAgent agent = new()
            {
                Name = "PIIAgent",
                Instructions = $" You are an agent designed to extract any Personally Identifiable Information (PII) in files you receive.\n" +
                "If the user provides a file path, process the file and extract PII.",
                Kernel = kernel,
                Arguments = new KernelArguments(new PromptExecutionSettings() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() }),
            };

            // initialize plugin and add to the agent's Kernel (same as direct Kernel usage).
            agent.Kernel.Plugins.Add(KernelPluginFactory.CreateFromType<PIIExtractionPlugin>());

            // Console.WriteLine("Hello! I am Personally Identifiable Information Detection Agent, here to help you detect PII in image files.");

            // create a history to store the conversation
            // chat history is how the plugin will be able to access the image
            var history = new ChatHistory();
            
            history.AddSystemMessage("You are an agent designed to extract any Personally Identifiable Information (PII) in files you receive. Your name is PIIAgent. If the user provides a file path, process the file and extract PII. Don't answer questions that are not related to PII extraction.");

            // create agent thread
            ChatHistoryAgentThread agentThread = new();

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

                // add the user message to the chat history
                history.AddUserMessage(input);

                // invoke the agent
                await foreach (ChatMessageContent response in agent.InvokeAsync(input, agentThread))
                {
                    Console.WriteLine($"Assistant > {response.Content}");

                    // add the assistant's response to the chat history if not null
                    if (!string.IsNullOrWhiteSpace(response.Content))
                    {
                        history.AddAssistantMessage(response.Content);
                    }
                }
            } while (!isComplete);

            Console.WriteLine("Goodbye!");
        }
    }
}