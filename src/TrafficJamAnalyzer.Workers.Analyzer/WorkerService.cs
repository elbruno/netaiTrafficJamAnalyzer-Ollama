using TrafficJamAnalyzer.Shared.Clients;
using TrafficJamAnalyzer.Shared.Models;

namespace TrafficJamAnalyzer.Workers.Analyzer
{
    public class WorkerService
    {
        private readonly WebApiClient _webApiClient;
        private readonly AiApiClient _aiApiClient;
        private readonly VectorStoreApiClient _vectorStoreApiClient;

        private readonly ILogger<BackgroundService> _logger;

        public DateTime LastRun;
        public Task? runningTask;

        public WorkerService(
            WebApiClient webApiClient,
            AiApiClient aiApiClient,
            VectorStoreApiClient vectorStoreApiClient,
            ILogger<BackgroundService> logger)
        {
            _webApiClient = webApiClient;
            _aiApiClient = aiApiClient;
            _vectorStoreApiClient = vectorStoreApiClient;
            _logger = logger;
            LastRun = DateTime.UtcNow;
        }

        public bool WorkerStatus()
        {
            _logger.LogInformation("Checking worker status...");
            return runningTask is not null;
        }


        public void Stop()
        {
            _logger.LogInformation("Stopping the worker service...");
            if (runningTask is not null)
            {
                _logger.LogInformation("Disposing the running task...");
                runningTask.Dispose();
                runningTask = null;
            }
            _logger.LogInformation("Worker service stopped.");
        }

        public void Start()
        {
            _logger.LogInformation("Starting the worker service...");   
            runningTask = Task.Run(async () =>
            {
                // Wait for the database to start correctly
                await Task.Delay(TimeSpan.FromSeconds(30));

                try
                {
                    _logger.LogInformation("Fetching traffic title entries...");

                    var traffics = await _webApiClient.GetTrafficAsync();

                    foreach (var traffic in traffics.Where(x => x.Title.ToLower() == "entry"))
                    {
                        _logger.LogInformation("Processing traffic: {Id}", traffic.Id);

                        var identifier = traffic.Url.Split("/").Last().Replace(".jpg", "");

                        var analyzeResult = await _aiApiClient.AnalyzeAsync(identifier);

                        if (analyzeResult is not null)
                        {
                            traffic.Title = analyzeResult.Result.Title;

                            traffic.CctvDate = analyzeResult.Result.Date;

                            await _webApiClient.UpdateTrafficAsync(traffic.Id, traffic);

                            _logger.LogInformation($"Added title to traffic: {traffic.Id} - {traffic.Title}");
                        }

                        await Task.Delay(TimeSpan.FromSeconds(35));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred during processing.");
                }

                while (true)
                {
                    try
                    {
                        _logger.LogInformation("Fetching traffic entries...");

                        var traffics = await _webApiClient.GetTrafficAsync();

                        foreach (var traffic in traffics.Where(x => x.Enabled))
                        {
                            _logger.LogInformation($"Processing traffic: {traffic.Id} - {traffic.Title}");

                            var identifier = traffic.Url.Split("/").Last().Replace(".jpg", "");

                            var analyzeResult = await _aiApiClient.AnalyzeAsync(identifier);

                            if (analyzeResult is not null)
                            {
                                await _webApiClient.AddTrafficResultAsync(
                                    traffic.Id,
                                    new TrafficResult()
                                    {
                                        CreatedAt = analyzeResult.Result.Date,
                                        TrafficTitle = analyzeResult.Result.Title,
                                        TrafficAmount = analyzeResult.Result.Traffic,
                                        CctvDate = analyzeResult.Result.Date
                                    }                                    
                                );
                                _logger.LogInformation($"Added analysis result for traffic: {traffic.Id} - {traffic.Title}");
                            }

                            // add the traffic entry to the vector store
                            await _vectorStoreApiClient.AddTrafficEntry(traffic);

                            await Task.Delay(TimeSpan.FromSeconds(5));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "An error occurred during processing.");
                    }

                    _logger.LogInformation("Waiting for the next iteration...");
                    LastRun = DateTime.UtcNow;
                    await Task.Delay(TimeSpan.FromMinutes(1));
                }
            });
        }
    }
}
