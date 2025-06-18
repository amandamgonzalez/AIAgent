using Azure.Identity;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Plugin;

namespace ChatCompletionAgentSample
{
    public static class Program
    {
        public static async Task Main()
        {
            // load configuration
            Settings settings = new();

            // initialize kernel
            IKernelBuilder builder = Kernel.CreateBuilder();

            // add Azure OpenAI chat completion service to the kernel
            builder.AddAzureOpenAIChatCompletion(
                settings.AzureOpenAI.ChatModelDeployment,
                settings.AzureOpenAI.Endpoint,
                new AzureCliCredential());
            
            Kernel kernel = builder.Build();

            // define agent
            ChatCompletionAgent agent = new()
            {
                Name = "PII Agent",
                Instructions = $" You are an agent designed to extract any Personally Identifiable Information (PII) in files you receive.\n" +
                "Your name is PII Agent, and you are only allowed to answer questions relating PII, and document extraction. \n" +
                "If the user provides a file path, process the file and extract PII.",
                Kernel = kernel,

                // allow the agent to automatically choose the plugins, and function to execute based on the input
                Arguments = new KernelArguments(new PromptExecutionSettings() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() }),
            };

            // initialize plugin and add to the agent's kernel 
            // there's a difference between adding it directly to the kernel and adding it to the agent's kernel
            agent.Kernel.Plugins.Add(KernelPluginFactory.CreateFromType<PIIExtractionPlugin>());

            // create a history to store the conversation
            // chat history is how the plugin will be able to access the image
            var history = new ChatHistory();
            
            // history.AddSystemMessage(
            // "Your name is PIIAgent. If the user provides a file path, process the file and extract PII. 
            // Don't answer questions that are not related to PII extraction.");

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

                // check for exit command by the user
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
                    // agent will call the plugin methods based on the input

                    Console.WriteLine($"Assistant > {response.Content}");

                    // add the assistant's response to the chat history
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