using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AgentsSample;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Plugin;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;

namespace WebApp.Controllers
{
    [ApiController]
    [Route("api/chat")]
    public class ChatController : ControllerBase
    {
        [HttpPost("send")]
        public async Task<IActionResult> Send([FromBody] ChatRequest request)
        {
            try
            {
                // 1. Set up kernel and agent
                var settings = new Settings();
                var builder = Kernel.CreateBuilder();
                builder.AddAzureOpenAIChatCompletion(
                    settings.AzureOpenAI.ChatModelDeployment,
                    settings.AzureOpenAI.Endpoint,
                    new Azure.Identity.AzureCliCredential());
                var kernel = builder.Build();

                var agent = new ChatCompletionAgent
                {
                    Name = "PIIAgent",
                    Instructions = "You are an agent designed to extract any Personally Identifiable Information (PII) in files you receive. If the user provides a file path or uploads a file, process it and extract PII.",
                    Kernel = kernel,
                    Arguments = new KernelArguments(new PromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() }),
                };
                agent.Kernel.Plugins.Add(KernelPluginFactory.CreateFromType<PIIExtractionPlugin>());

                // 2. Rebuild chat history if provided
                var agentThread = new ChatHistoryAgentThread();
                if (request.History != null)
                {
                    foreach (var msg in request.History)
                    {
                        if (msg.Sender == "user")
                            agentThread.ChatHistory.AddUserMessage(msg.Text);
                        else
                            agentThread.ChatHistory.AddAssistantMessage(msg.Text);
                    }
                }

                // 3. Compose input (message + file info)
                string input = request.Message;
                if (request.Files != null && request.Files.Count > 0)
                {
                    input += "\nFiles: " + string.Join(", ", request.Files.Select(f => f.Name));
                }

                // 4. Call the agent
                string result = "";
                await foreach (ChatMessageContent response in agent.InvokeAsync(input, agentThread))
                {
                    result += response.Content;
                }

                // 5. Return as array for frontend compatibility
                return Ok(new[] { new { content = result } });
            }
            catch (Exception ex)
            {
                // Log to console for backend debugging
                Console.WriteLine("Agent error: " + ex.ToString());
                // Return error to frontend for easier troubleshooting
                return StatusCode(500, new[] { new { content = $"Agent error: {ex.Message}" } });
            }
        }
    }

    // Models for request/response
    public class ChatRequest
    {
        public string Message { get; set; }
        public List<ChatFile> Files { get; set; }
        public List<ChatMessage> History { get; set; }
    }

    public class ChatFile
    {
        public string Name { get; set; }
        public long Size { get; set; }
        public string Type { get; set; }
    }

    public class ChatMessage
    {
        public string Sender { get; set; }
        public string Text { get; set; }
    }
}