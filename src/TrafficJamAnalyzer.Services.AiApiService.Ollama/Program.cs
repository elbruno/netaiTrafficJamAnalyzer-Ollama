using Newtonsoft.Json;
using TrafficJamAnalyzer.Shared.Models;
using OllamaSharp;
using OllamaSharp.Models;
using System.Text;
using OllamaSharp.Models.Chat;
using Microsoft.Extensions.AI;
using System.Text.RegularExpressions;

// Builder
var builder = WebApplication.CreateBuilder(args);

//var prompt = builder.Configuration["OpenAI:Prompt"];
//var systemPrompt = "You are a useful assistant that replies using a direct style";

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add service defaults & Aspire components.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddHttpClient();

// Add OpenAPI services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// register chat client
builder.Services.AddSingleton<OllamaApiClient>(static serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    var ollamaCnnString = config.GetConnectionString("ollamaVision");
    var defaultLLM = config.GetValue<string>("OllamaVisionModel") ?? "llama3.2-vision";

    // remove the text "Endpoint=" from the ollamaCnnString
    ollamaCnnString = ollamaCnnString?.Replace("Endpoint=", string.Empty) ?? string.Empty;

    logger.LogInformation("Ollama connection string: {0}", ollamaCnnString);
    logger.LogInformation("Default LLM: {0}", defaultLLM);

    var client = new OllamaApiClient(new Uri(ollamaCnnString), defaultLLM);
    // Optionally set a default model for all requests
    // client.SelectedModel = defaultLLM;

    return client;
});

var app = builder.Build();
var logger = app.Logger;
logger.LogInformation("Application starting up.");

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

// Add OpenAPI middleware in development
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Map the endpoint with logging
app.MapGet("/analyze/{identifier}", async (string identifier, ILogger<Program> logger, OllamaApiClient client) =>
{
    logger.LogInformation("Received analyze request with identifier: {Identifier}", identifier);

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

    // read the image url into a byte array
    byte[] imageByteData = Array.Empty<byte>();
    try
    {
        var httpClient = new HttpClient();
        imageByteData = await httpClient.GetByteArrayAsync(imageUrl);
        logger.LogInformation("Image URL downloaded: {ImageUrl}", imageUrl);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error downloading image from URL: {ImageUrl}", imageUrl);
    }

    var imageChatMessage = new ChatMessage(Microsoft.Extensions.AI.ChatRole.User, contents: new List<AIContent>
    {
        new DataContent(data: imageByteData, mediaType: "image/jpeg")
    });
    var messages = new List<ChatMessage>();   
    messages.Add(imageChatMessage);
    messages.Add(new ChatMessage(Microsoft.Extensions.AI.ChatRole.User, userPrompt));

    logger.LogInformation($"Chat history created for image {imageUrl}");

    var result = await client.GetResponseAsync<string>(messages: messages);

    var content = result.Text; // .Message.Text!;

    if (String.IsNullOrEmpty(content))
    {
        logger.LogWarning("No content received from chatCompletionService.");
        return new TrafficJamAnalyzeResult();
    }

    var analyzeResult = new TrafficJamAnalyzeResult();
    try
    {
        logger.LogInformation("Content received: {Content}", content);

        // Use the new parser class
        var parsed = await TrafficJamAnalyzeParser.ParseAsync(content, logger, client, imageChatMessage);
        if (parsed != null)
        {
            analyzeResult = new TrafficJamAnalyzeResult
            {
                CreatedAt = DateTime.UtcNow,
                Result = parsed,
                SourceUrl = imageUrl
            };
            logger.LogInformation("Analysis result created: {AnalyzeResult}", JsonConvert.SerializeObject(analyzeResult));
        }
        else
        {
            logger.LogWarning("Content could not be parsed into a valid TrafficJamAnalyze object.");
        }
    }
    catch (Exception exc)
    {
        logger.LogError(exc, "error processing the content response from LLM");
        return analyzeResult;
    }

    return analyzeResult;
})
    .WithDisplayName("Analyze Traffic Jam Image")
    .WithSummary("Analyze a traffic jam image and return the analysis result in JSON format.")
    .WithName("AnalyzeTrafficJamImage")
    .Produces<TrafficJamAnalyzeResult>(StatusCodes.Status200OK)
  .ProducesProblem(StatusCodes.Status400BadRequest)
  .ProducesProblem(StatusCodes.Status500InternalServerError);

logger.LogInformation("Application starting up.");
app.Run();
logger.LogInformation("Application shut down.");