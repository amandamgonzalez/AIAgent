using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

// the order goes process_file -> create_chat_history -> extract_pii

namespace Plugin
{
    public class PIIExtractionPlugin
    {
        private readonly string _systemMessage;

        public PIIExtractionPlugin()
        {
            _systemMessage = "Extract any Personally Identifiable Information (PII) in files you receive.";
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
            var chatHistory = CreateChatHistory(imageBytes); // create chat history, and pass image content object as a user message
            return await ExtractPIIAsync(chatHistory, kernel); // pass chat history and JSON schema
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
    }

    // response format class for structured output
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