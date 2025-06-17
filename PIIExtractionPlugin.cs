using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Newtonsoft.Json.Schema.Generation;
using Microsoft.SemanticKernel.Connectors.OpenAI;


// the order goes process_file -> create_chat_history -> extract_pii -> generate_pii_schema

namespace Plugin
{
    public class PIIExtractionPlugin
    {
        private readonly JSchemaGenerator _schemaGenerator;
        private readonly string _systemMessage;

        public PIIExtractionPlugin()
        {
            _schemaGenerator = new JSchemaGenerator();
            _systemMessage = "Extract any Personally Identifiable Information (PII) in files you receive.";
        }

        [KernelFunction("generate_pii_schema")]
        [Description("Generates a JSON schema for PII extraction.")]
        public string GeneratePIISchema()
        {
            Console.WriteLine("[LOG] GeneratePIISchema method called.");
            return _schemaGenerator.Generate(typeof(PII)).ToString();
        }

        [KernelFunction("create_chat_history")]
        [Description("Creates chat history from image bytes.")]
        public ChatHistory CreateChatHistory(byte[] imageBytes)
        {
            Console.WriteLine("[LOG] CreateChatHistory method called.");
            var imageContent = new ImageContent(data: imageBytes, mimeType: "image/png");
            var imageCollection = new ChatMessageContentItemCollection();
            imageCollection.Add(imageContent);

            var chatHistory = new ChatHistory(systemMessage: _systemMessage);
            chatHistory.AddUserMessage(imageCollection);

            return chatHistory;
        }

        [KernelFunction("extract_pii")]
        [Description("Extracts PII from the provided chat history.")]
        public async Task<string> ExtractPIIAsync(ChatHistory chatHistory, Kernel kernel)
        {
            Console.WriteLine("[LOG] ExtractPIIAsync method called.");
            var jsonSchema = GeneratePIISchema();

            var chatUpdates = kernel.GetRequiredService<IChatCompletionService>()
                .GetStreamingChatMessageContentsAsync(
                    chatHistory,
                    new OpenAIPromptExecutionSettings
                    {
                        ResponseFormat = typeof(PII)
                    });

            string extractedPII = string.Empty;

            await foreach (var chatUpdate in chatUpdates)
            {
                extractedPII += chatUpdate.Content;
            }

            return extractedPII;
        }

        [KernelFunction("process_file")]
        [Description("Processes a file, extracts its content, and detects PII.")]
        public async Task<string> ProcessFileAsync(string filePath, Kernel kernel)
        {
            Console.WriteLine("[LOG] ProcessFileAsync method called.");
            if (!File.Exists(filePath))
            {
                return "File not found. Please provide a valid file path.";
            }

            var imageBytes = await File.ReadAllBytesAsync(filePath);
            var chatHistory = CreateChatHistory(imageBytes);
            return await ExtractPIIAsync(chatHistory, kernel);
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