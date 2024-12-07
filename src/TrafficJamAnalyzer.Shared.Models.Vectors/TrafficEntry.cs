using Microsoft.Extensions.VectorData;

namespace TrafficJamAnalyzer.Shared.Models.Vectors;

public class TrafficEntry
{
    [VectorStoreRecordKey]
    public int Id { get; set; }

    [VectorStoreRecordData]
    public string Title { get; set; }

    [VectorStoreRecordData]
    public string Url { get; set; }

    [VectorStoreRecordData]
    public string? CctvDate { get; set; }

    [VectorStoreRecordData]
    public bool Enabled { get; set; }

    [VectorStoreRecordData]
    public DateTime CreatedAt { get; set; }

    [VectorStoreRecordData]
    public DateTime UpdatedAt { get; set; }

    [VectorStoreRecordData]
    public int CurrentTrafficAmount { get; set; }

    [VectorStoreRecordData]
    public List<TrafficResult> Results { get; set; }

    [VectorStoreRecordVector(384, DistanceFunction.CosineSimilarity)]
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
