using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;


// the order goes process_file -> create_chat_history -> extract_pii -> generate_pii_schema

namespace Plugin
{
    public class PIIExtractionPlugin
    {
        // private readonly JSchemaGenerator _schemaGenerator;
        private readonly string _systemMessage;
        private readonly Kernel _kernel;
        // passing kernel directly to the plugin allows it to access services like IChatCompletionService

        public PIIExtractionPlugin(Kernel kernel)
        {
            // _schemaGenerator = new JSchemaGenerator();
            _systemMessage = "Extract any Personally Identifiable Information (PII) in files you receive.";
            _kernel = kernel;
        }

        // [KernelFunction("generate_pii_schema")]
        // [Description("Generates a JSON schema for PII extraction.")]
        // public string GeneratePIISchema()
        // {
        //     return _schemaGenerator.Generate(typeof(PII)).ToString();
        // }

        [KernelFunction("create_chat_history")]
        [Description("Creates chat history from image bytes.")]
        public ChatHistory CreateChatHistory(byte[] imageBytes)
        {
            var imageContent = new ImageContent(data: imageBytes, mimeType: "image/png");
            var imageCollection = new ChatMessageContentItemCollection();
            imageCollection.Add(imageContent);

            var chatHistory = new ChatHistory(systemMessage: _systemMessage);
            chatHistory.AddUserMessage(imageCollection);

            // Console.WriteLine("ChatHistory created successfully.");
            return chatHistory;
        }

        [KernelFunction("extract_pii")]
        [Description("Extracts PII from the provided chat history.")]
        public async Task<string> ExtractPIIAsync(ChatHistory chatHistory)
        {
            // var jsonSchema = GeneratePIISchema();

            // Console.WriteLine("Generated JSON schema: " + jsonSchema);
            Console.WriteLine("Got to ExtractPIIAsync");

            var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

            // if (chatCompletionService == null)
            // {
            //     Console.WriteLine("Error: ChatCompletionService is not registered in the kernel.");
            //     return "Service not registered. Unable to process PII.";
            // }

            var chatUpdates = chatCompletionService
                .GetStreamingChatMessageContentsAsync(
                    chatHistory,
                    new AzureOpenAIPromptExecutionSettings
                    {
                        ResponseFormat = typeof(PII)
                    });

            Console.WriteLine("Got to AFTER OpenAIPromptExecutionSettings");

            string extractedPII = string.Empty;

            await foreach (var message in chatUpdates)
            {
                extractedPII += message.Content;
            }

            return extractedPII;
        }

        [KernelFunction("process_file")]
        [Description("Extracts PII from a file at the specified path.")]
        public async Task<string> ProcessFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return "File not found. Please provide a valid file path.";
            }

            Console.WriteLine($"Processing file: {filePath}");
            // Console.WriteLine($"Kernel Check: {kernel}");

            var imageBytes = await File.ReadAllBytesAsync(filePath);
            var chatHistory = CreateChatHistory(imageBytes);
            return await ExtractPIIAsync(chatHistory);
        }
    }

    // structured output for PII extraction
    public class PII
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("company_email")]
        public string? CompanyEmail { get; set; }

        [JsonPropertyName("personal_email")]
        public string? PersonalEmail { get; set; }

        [JsonPropertyName("personal_phone_number")]
        public string? PersonalPhoneNumber { get; set; }

        [JsonPropertyName("company_phone_number")]
        public string? CompanyPhoneNumber { get; set; }

        [JsonPropertyName("personal_address")]
        public string? PersonalAddress { get; set; }

        [JsonPropertyName("company_ship_to_address")]
        public string? CompanyShipToAddress { get; set; }

        [JsonPropertyName("company_ship_from_address")]
        public string? CompanyShipFromAddress { get; set; }
    }
}