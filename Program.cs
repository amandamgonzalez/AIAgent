using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;


using Microsoft.SemanticKernel.Connectors.OpenAI;

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

            // load plugins and add them to the kernel
            Console.WriteLine("Initializing plugins...");
            var piiPlugin = new PIIExtractionPlugin();
            kernel.Plugins.AddFromObject(piiPlugin);

            // enable planning
            OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new()
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };

            // define agent
            Console.WriteLine("Defining agent...");
            ChatCompletionAgent agent = new()
            {
                Name = "PIIAgent",
                Instructions = $" You are an agent designed to extract any Personally Identifiable Information (PII) in files you receive.\n" +
                "If the user provides a file path, process the file and extract PII.",
                Kernel = kernel,
            };

            // a bit redundant to keep sending console messages
            //Console.WriteLine("Ready! Agent was created successfully.");

            // Console.WriteLine("Hello! I am Personally Identifiable Information Detection Agent, here to help you detect PII in image files.");

            // create a history to store the conversation
            var history = new ChatHistory();
            
            history.AddSystemMessage("You are an agent designed to extract any Personally Identifiable Information (PII) in files you receive. Your name is PIIAgent. If the user provides a file path, process the file and extract PII. Don't answer questions that are not related to PII extraction.");

            // why don't we use a thread here?
            // like this: ChatHistoryAgentThread agentThread = new();

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

                // get the chat completion service
                var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

                // use the chat completion service to process the input dynamically
                var result = await chatCompletionService.GetChatMessageContentAsync(
                    history,
                    executionSettings: openAIPromptExecutionSettings,
                    kernel: kernel);

                // return the result content as a string
                var resultContent = result.Content;

                // if the result content is null or empty, say it back to the user
                if (string.IsNullOrWhiteSpace(resultContent))
                {
                    Console.WriteLine("Assistant > No data was returned. Please ensure the file exists and is accessible.");
                }
                else
                {
                    // if processed correct then write the results directly
                    Console.Write("Assistant > ");
                    Console.Write(resultContent);

                    // add the assistant's response to the chat history
                    history.AddAssistantMessage(resultContent);
                }
            } while (!isComplete);

            Console.WriteLine("Goodbye!");
        }
    }
}