
var builder = DistributedApplication.CreateBuilder(args);

var ollama = builder.AddOllama("ollamaVision", port: 11434);
ollama.AddModel("llama3.2-vision");
ollama.AddModel("phi3.5");
ollama.WithDataVolume();
ollama.WithContainerRuntimeArgs("--gpus=all");

var sqldb = builder.AddSqlServer("sql")
    .WithDataVolume()
    .AddDatabase("sqldb");

var apiService = builder.AddProject<Projects.TrafficJamAnalyzer_Microservices_WebApiService>("apiservice")
    .WithReference(sqldb);

var aiService = builder.AddProject<Projects.TrafficJamAnalyzer_Microservices_AiApiService>("aiservice")
    .WithReference(ollama);

var scrapService = builder.AddProject<Projects.TrafficJamAnalyzer_Microservices_ScraperApiService>("scrapservice");

var worker = builder.AddProject<Projects.TrafficJamAnalyzer_Workers_Analyzer>("worker")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WithReference(aiService)
    .WithReference(scrapService);


builder.AddProject<Projects.TrafficJamAnalyzer_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WithReference(scrapService);

builder.Build().Run();
