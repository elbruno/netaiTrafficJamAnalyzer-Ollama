using Newtonsoft.Json;
using TrafficJamAnalyzer.Shared.Models;
using OllamaSharp;
using OllamaSharp.Models;
using System.Text;
using OllamaSharp.Models.Chat;
using Microsoft.Extensions.AI;
using System.Text.RegularExpressions;
using TrafficJamAnalyzer.Services.AiApiService.Ollama;

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

// Register TrafficJamAnalyzerService for DI
builder.Services.AddScoped<TrafficJamAnalyzerService>();

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
app.MapGet("/analyze/{identifier}", async (string identifier, TrafficJamAnalyzerService analyzerService) =>
{
    return await analyzerService.AnalyzeAsync(identifier);
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