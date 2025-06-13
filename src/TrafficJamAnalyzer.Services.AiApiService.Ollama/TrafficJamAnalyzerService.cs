using Newtonsoft.Json;
using TrafficJamAnalyzer.Shared.Models;
using OllamaSharp;
using OllamaSharp.Models;
using System.Text;
using OllamaSharp.Models.Chat;
using Microsoft.Extensions.AI;
using System.Text.RegularExpressions;

namespace TrafficJamAnalyzer.Services.AiApiService.Ollama
{
    public class TrafficJamAnalyzerService
    {
        private readonly ILogger<TrafficJamAnalyzerService> _logger;
        private readonly OllamaApiClient _client;
        private readonly IHttpClientFactory _httpClientFactory;

        public TrafficJamAnalyzerService(ILogger<TrafficJamAnalyzerService> logger, OllamaApiClient client, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _client = client;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<TrafficJamAnalyzeResult> AnalyzeAsync(string identifier)
        {
            _logger.LogInformation("Received analyze request with identifier: {Identifier}", identifier);

            var imageUrl = $"http://cic.tenerife.es/e-Traffic3/data/{identifier}.jpg";

            var userPrompt = @"You are analyzing a CCTV traffic camera image. Your task is to extract and return a single, valid JSON object with the following fields: 'Title', 'Traffic', and 'Date'.

Instructions:
- 'Title': Extract ONLY the text visible in the top left corner of the image and assign it to this field.
- 'Date': Extract ONLY the text visible in the bottom right corner of the image and assign it to this field.
- 'Traffic': Analyze the visible road area and estimate the current traffic level as an integer from 0 (no traffic) to 100 (maximum congestion), based on the number of vehicles and the degree of congestion you observe.

Requirements:
- The image is from a real-time traffic CCTV camera. Focus on the road and vehicles for the 'Traffic' value.
- Do NOT include any information not visible in the image.
- Return ONLY a single valid JSON object, with no extra text, explanation, or markdown formatting.
- The JSON must have exactly these three fields: 'Title', 'Date', and 'Traffic'.

Example output:
{""Title"": ""3M-TVM-21 (Túnel 3 de Mayo)"", ""Date"": ""12/06/2025 18:47"", ""Traffic"": 0}
";

            byte[] imageByteData = System.Array.Empty<byte>();
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                imageByteData = await httpClient.GetByteArrayAsync(imageUrl);
                _logger.LogInformation("Image URL downloaded: {ImageUrl}", imageUrl);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error downloading image from URL: {ImageUrl}", imageUrl);
            }

            var imageChatMessage = new ChatMessage(Microsoft.Extensions.AI.ChatRole.User, contents: new List<AIContent>
            {
                new DataContent(data: imageByteData, mediaType: "image/jpeg")
            });
            var messages = new List<ChatMessage>();   
            messages.Add(imageChatMessage);
            messages.Add(new ChatMessage(Microsoft.Extensions.AI.ChatRole.User, userPrompt));

            _logger.LogInformation($"Chat history created for image {imageUrl}");

            var result = await _client.GetResponseAsync<string>(messages: messages);

            var content = result.Text;

            if (string.IsNullOrEmpty(content))
            {
                _logger.LogWarning("No content received from chatCompletionService.");
                return new TrafficJamAnalyzeResult();
            }

            var analyzeResult = new TrafficJamAnalyzeResult();
            try
            {
                _logger.LogInformation("Content received: {Content}", content);

                var parsed = await TrafficJamAnalyzeParser.ParseAsync(content, _logger, _client, imageChatMessage);
                if (parsed != null)
                {
                    analyzeResult = new TrafficJamAnalyzeResult
                    {
                        CreatedAt = System.DateTime.UtcNow,
                        Result = parsed,
                        SourceUrl = imageUrl
                    };
                    _logger.LogInformation("Analysis result created: {AnalyzeResult}", JsonConvert.SerializeObject(analyzeResult));
                }
                else
                {
                    _logger.LogWarning("Content could not be parsed into a valid TrafficJamAnalyze object.");
                }
            }
            catch (System.Exception exc)
            {
                _logger.LogError(exc, "error processing the content response from LLM");
                return analyzeResult;
            }

            return analyzeResult;
        }
    }
}
