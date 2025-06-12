using OpenAI.Embeddings;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using OpenAI.Chat;
using Microsoft.Extensions.AI;
using TrafficJamAnalyzer.Shared.Models;

// Builder
var builder = WebApplication.CreateBuilder(args);

var prompt = builder.Configuration["OpenAI:Prompt"];
var systemPrompt = "You are a useful assistant that replies using a direct style";

VectorStoreCollection<int, TrafficJamAnalyzer.Shared.Models.Vectors.TrafficEntry> trafficEntriesCollection = null;
bool isMemoryCollectionInitialized = false;

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add service defaults & Aspire components.
builder.AddServiceDefaults();

// Enable model diagnostics with sensitive data.
AppContext.SetSwitch("Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive", true);

// Add services to the container.
builder.Services.AddProblemDetails();

// register embeddings generator
builder.Services.AddSingleton<OllamaEmbeddingGenerator>(static serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    var ollamaCnnString = config.GetConnectionString("ollamaVision");
    var defaultLLM = "all-minilm";

    // remove the text "Endpoint=" from the ollamaCnnString
    ollamaCnnString = ollamaCnnString?.Replace("Endpoint=", string.Empty) ?? string.Empty;

    logger.LogInformation("Ollama connection string: {0}", ollamaCnnString);
    logger.LogInformation("Default LLM: {0}", defaultLLM);

    var embeddingsGenerator = new OllamaEmbeddingGenerator(new Uri(ollamaCnnString), defaultLLM);

    return embeddingsGenerator;
});

// register chat client
builder.Services.AddSingleton<IChatClient>(static serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    var ollamaCnnString = config.GetConnectionString("ollamaVision");
    var defaultLLM = "llama3.2-vision";

    // remove the text "Endpoint=" from the ollamaCnnString
    ollamaCnnString = ollamaCnnString?.Replace("Endpoint=", string.Empty) ?? string.Empty;

    logger.LogInformation("Ollama connection string: {0}", ollamaCnnString);
    logger.LogInformation("Default LLM: {0}", defaultLLM);

    IChatClient chatClient = new OllamaChatClient(new Uri(ollamaCnnString), defaultLLM);

    return chatClient;
});

var app = builder.Build();
var logger = app.Logger;
logger.LogInformation("Application starting up.");

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

// add a new traffic result to the inMemory store
app.MapPost("/addTrafficEntry/{identifier}", async (int identifier, TrafficEntry trafficEntry, ILogger<Program> logger, OllamaEmbeddingGenerator embeddingGenerator) =>
{
    logger.LogInformation($"Adding Traffic Entry to memory: {trafficEntry.Title} with CCTV Date: {trafficEntry.CctvDate}");

    if (!isMemoryCollectionInitialized)
    {
        await InitMemoryContextAsync(logger);
    }

    var trafficHistory = string.Empty;

    // iterate through trafficEntry.Results
    int index = 0;
    foreach (var trafficEntryResult in trafficEntry.Results)
    {
        trafficHistory += $"Index: [{index}]. The traffic is [{trafficEntryResult.TrafficAmount}/100] at the time [{trafficEntryResult.CctvDate}].";
        index++;
    }

    logger.LogInformation($"Adding traffic result to memory. Camera Tittle: {trafficEntry.Title}");
    var trafficInfo = @$"The traffic in the camera named [{trafficEntry.Title}] is [{trafficEntry.CurrentTrafficAmount}/100] at the time [{trafficEntry.CctvDate}].
Traffic Camera History: {trafficHistory}";

    // new product vector
    var newTrafficEntry = TrafficJamAnalyzer.Shared.Models.Vectors.TrafficEntry.CreateFromModelsTrafficEntry(trafficEntry);

    //var result = await embeddingClient.GenerateEmbeddingAsync(trafficInfo);
    //newTrafficEntry.Vector = result.Value.ToFloats();

    var result = await embeddingGenerator.GenerateAsync(trafficInfo);
    newTrafficEntry.Vector = result.Vector;

    await trafficEntriesCollection.UpsertAsync(newTrafficEntry);
    logger.LogInformation(@$"Traffic Entry added to memory: {trafficEntry.Title} with traffic ammount: [{trafficEntry.CurrentTrafficAmount}] and CCTV Date: {trafficEntry.CctvDate}.
Traffic Camera History: {trafficHistory}");

    return true;
});

app.MapGet("/search/{search}", async context =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    var client = context.RequestServices.GetRequiredService<IChatClient>();
    var embeddingGenerator = context.RequestServices.GetRequiredService<OllamaEmbeddingGenerator>();

    var search = context.Request.RouteValues["search"]?.ToString();
    if (string.IsNullOrEmpty(search))
    {
        logger.LogWarning("Search criteria is missing.");
        await context.Response.WriteAsync("Search criteria is missing.");
        return;
    }

    logger.LogInformation($"Search memory. Search criteria: {search}");

    var searchCriteriaEmbeddings = await embeddingGenerator.GenerateVectorAsync(search);
    var vectorSearchQuery = searchCriteriaEmbeddings;

    TrafficJamAnalyzer.Shared.Models.Vectors.TrafficEntry firstTrafficEntry = null;

    await foreach (var resultItem in trafficEntriesCollection.SearchAsync(vectorSearchQuery, top: 3))
    {
        if (resultItem.Score > 0.5)
            firstTrafficEntry = resultItem.Record;
    }

    if (firstTrafficEntry == null)
    {
        logger.LogWarning("No traffic entries found matching the search criteria.");
        await context.Response.WriteAsync("No results found.");
        return;
    }

    var lastTrafficResult = firstTrafficEntry.Results.LastOrDefault();

    var prompt = @$"You are an intelligent assistant helping clients with their search about traffic entries on a CCTV collection. Generate a catchy and friendly message using the following information:
    - User Question: {search}
    - Found Traffic Camera Name: {firstTrafficEntry.Title}
    - Found Traffic Camera CCTV Date: {firstTrafficEntry.CctvDate}
    - Found Traffic Camera, log created at: {firstTrafficEntry.CreatedAt}
    - Found Traffic Camera, log updated at: {firstTrafficEntry.UpdatedAt}
    - Found Traffic Camera traffic status : {firstTrafficEntry.CurrentTrafficAmount}/100

The traffic status is a value where 0 is no traffic and 100 is heavy traffic.
Include the camera name, camera cctv date and more information in the response to the user question.";

    var messages = new List<Microsoft.Extensions.AI.ChatMessage>
    {
        new(ChatRole.User, prompt)
    };

    logger.LogInformation($"Chat history created for CCTV Camera {firstTrafficEntry.Title}");

    var result = await client.GetResponseAsync(messages);

    var content = result.Text!;

    if (string.IsNullOrEmpty(content))
    {
        logger.LogWarning("No content received from chatCompletionService.");
        await context.Response.WriteAsync("No results.");
        return;
    }

    await context.Response.WriteAsync(content);
});

logger.LogInformation("Application starting up.");
app.Run();
logger.LogInformation("Application shut down.");


async Task<bool> InitMemoryContextAsync(ILogger<Program> logger)
{
    logger.LogInformation("Initializing vector store");
    var vectorProductStore = new InMemoryVectorStore();
    trafficEntriesCollection = vectorProductStore.GetCollection<int, TrafficJamAnalyzer.Shared.Models.Vectors.TrafficEntry>("trafficresults");
    await trafficEntriesCollection.EnsureCollectionExistsAsync();
    isMemoryCollectionInitialized = true;
    logger.LogInformation("Vector Store initialized.");
    //Task.Delay(1000).Wait();
    return true;
}