using Microsoft.Extensions.VectorData;

namespace TrafficJamAnalyzer.Shared.Models.Vectors;

public class TrafficEntry
{
    [VectorStoreKey]
    public int Id { get; set; }

    [VectorStoreData]
    public string Title { get; set; }

    [VectorStoreData]
    public string Url { get; set; }

    [VectorStoreData]
    public string? CctvDate { get; set; }

    [VectorStoreData]
    public bool Enabled { get; set; }

    [VectorStoreData]
    public DateTime CreatedAt { get; set; }

    [VectorStoreData]
    public DateTime UpdatedAt { get; set; }

    [VectorStoreData]
    public int CurrentTrafficAmount { get; set; }

    [VectorStoreData]
    public List<TrafficResult> Results { get; set; }

    [VectorStoreVector(384)]
    public ReadOnlyMemory<float> Vector { get; set; }

    public static TrafficEntry CreateFromModelsTrafficEntry(Models.TrafficEntry trafficEntryOrigin)
    {
        var newTrafficEntry = new TrafficEntry
        {
            Id = trafficEntryOrigin.Id,
            Title = trafficEntryOrigin.Title,
            Url = trafficEntryOrigin.Url,
            CctvDate = trafficEntryOrigin.CctvDate,
            Enabled = trafficEntryOrigin.Enabled,
            CreatedAt = trafficEntryOrigin.CreatedAt,
            UpdatedAt = trafficEntryOrigin.UpdatedAt,
            CurrentTrafficAmount = trafficEntryOrigin.CurrentTrafficAmount,
            Results = []
        };
        foreach (var trafficResult in trafficEntryOrigin.Results)
        {
            newTrafficEntry.Results.Add(new TrafficResult
            {
                Id = trafficResult.Id,
                TrafficId = trafficResult.TrafficId,
                TrafficTitle = trafficResult.TrafficTitle,
                CctvDate = trafficResult.CctvDate,
                TrafficAmount = trafficResult.TrafficAmount,
                CreatedAt = trafficResult.CreatedAt
            });
        }

        return newTrafficEntry;
    }

}
