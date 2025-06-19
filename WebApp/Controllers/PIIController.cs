using System.Threading.Tasks;
using AgentsSample;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Azure.Identity;
using Microsoft.SemanticKernel.ChatCompletion;
using Plugin;

// in this controller, there is no agent, and the plugin is called directly

namespace WebApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PIIController : ControllerBase
    {
        [HttpPost("extract")]
        public async Task<IActionResult> ExtractPII([FromBody] PiiRequest request)
        {
            // load configuration
            var settings = new Settings();

            // initialize kernel
            var builder = Kernel.CreateBuilder();
            builder.AddAzureOpenAIChatCompletion(
                settings.AzureOpenAI.ChatModelDeployment,
                settings.AzureOpenAI.Endpoint,
                new Azure.Identity.AzureCliCredential());
                
            var kernel = builder.Build();

            // initialize plugin
            var plugin = new PIIExtractionPlugin();

            // call the plugin directly with the file path
            if (string.IsNullOrEmpty(request.FilePath))
            {
                return BadRequest("FilePath is required.");
            }

            // in this example the plugin is called manually
            var result = await plugin.ProcessFileAsync(request.FilePath, kernel);

            return Ok(new { PII = result });
        }
    }

    public class PiiRequest
    {
        public string? FilePath { get; set; }
    }
}