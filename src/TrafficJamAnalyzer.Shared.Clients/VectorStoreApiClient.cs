using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using TrafficJamAnalyzer.Shared.Models;

namespace TrafficJamAnalyzer.Shared.Clients
{
    public class VectorStoreApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<VectorStoreApiClient> _logger;

        public VectorStoreApiClient(HttpClient httpClient, ILogger<VectorStoreApiClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<bool?> AddTrafficEntry(TrafficEntry trafficEntry, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation($"Adding new traffic entry to vector store: {trafficEntry.Title}");

            var content = JsonContent.Create(trafficEntry);
            var response = await _httpClient.PostAsync($"/addTrafficEntry/{trafficEntry.Id}", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Failed to add new traffic entry with identifier: {trafficEntry.Id}");
                return null;
            }

            var analyzeResult = await response.Content.ReadFromJsonAsync<bool>(cancellationToken: cancellationToken);

            if (analyzeResult == null)
            {
                _logger.LogWarning("No content received from Vector Store API service.");
                return null;
            }

            _logger.LogInformation($"New traffic entry added to vector store: {trafficEntry.Title}");

            return analyzeResult;
        }
    }
}