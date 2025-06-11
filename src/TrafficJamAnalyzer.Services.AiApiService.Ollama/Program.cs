using Newtonsoft.Json;
using TrafficJamAnalyzer.Shared.Models;
using OllamaSharp;
using OllamaSharp.Models;
using System.Text;
using OllamaSharp.Models.Chat;
using Microsoft.Extensions.AI;

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

// register chat client
builder.Services.AddSingleton<OllamaApiClient>(static serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    var ollamaCnnString = config.GetConnectionString("ollamaVision");
    var defaultLLM = config.GetValue<string>("OllamaVisionModel") ?? "llama3.2-vision";

    // remove the text "Endpoint=" from the ollamaCnnString
    ollamaCnnString = ollamaCnnString.Replace("Endpoint=", string.Empty);

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

// Map the endpoint with logging
app.MapGet("/analyze/{identifier}", async (string identifier, ILogger<Program> logger, OllamaApiClient client) =>
{
    logger.LogInformation("Received analyze request with identifier: {Identifier}", identifier);

    var imageUrl = $"http://cic.tenerife.es/e-Traffic3/data/{identifier}.jpg";

    var userPrompt = @"Analyze the image, return a JSON object with the fields 'Title', 'Traffic' and 'Date'.
Extract the text from the top left corner of the image and assign the extracted text to the JSON field 'Title'. 
Extract the text from the bottom right corner of the image and assign the extracted text to the JSON field 'Date'. 
Analyze the amount of traffic in the image. Based on the amount of traffic, define a value from 0 to 100, where 0 is no traffic and 100 is heavy traffic. Assign the integer value of the traffic to the JSON field 'Traffic'.
Only provide the JSON result and nothing else. 
Return only the JSON object without any markdown. ";

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

        analyzeResult = new TrafficJamAnalyzeResult
        {
            CreatedAt = DateTime.UtcNow,
            Result = JsonConvert.DeserializeObject<TrafficJamAnalyze>(content)!,
            SourceUrl = imageUrl
        };

        logger.LogInformation("Analysis result created: {AnalyzeResult}", JsonConvert.SerializeObject(analyzeResult));
    }
    catch (Exception exc)
    {
        logger.LogError(exc, "error deserializing the content response from LLM");
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