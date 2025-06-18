using System.Threading.Tasks;
using AgentsSample;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Azure.Identity;
using Microsoft.SemanticKernel.ChatCompletion;
using Plugin;

namespace WebApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PIIController : ControllerBase
    {
        [HttpPost("extract")]
        public async Task<IActionResult> ExtractPII([FromBody] PiiRequest request)
        {
            // Load configuration and initialize kernel
            var settings = new Settings();
            var builder = Kernel.CreateBuilder();
            builder.AddAzureOpenAIChatCompletion(
                settings.AzureOpenAI.ChatModelDeployment,
                settings.AzureOpenAI.Endpoint,
                new Azure.Identity.AzureCliCredential());
            var kernel = builder.Build();

            var history = new ChatHistory();

            // Initialize plugin
            var plugin = new PIIExtractionPlugin();

            // Call the plugin directly with the file path
            var result = await plugin.ProcessFileAsync(request.FilePath, kernel);

            return Ok(new { PII = result });
        }
    }

    public class PiiRequest
    {
        public string FilePath { get; set; }
    }
}