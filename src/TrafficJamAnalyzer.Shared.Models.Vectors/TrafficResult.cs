using Microsoft.Extensions.VectorData;

namespace TrafficJamAnalyzer.Shared.Models.Vectors;
public class TrafficResult
{
    [VectorStoreKey]
    public int Id { get; set; }

    [VectorStoreData]
    public int TrafficId { get; set; }

    [VectorStoreData]
    public string TrafficTitle { get; set; }

    [VectorStoreData]
    public string? CctvDate { get; set; }

    [VectorStoreData]
    public int TrafficAmount { get; set; }

    [VectorStoreData]
    public string CreatedAt { get; set; }
}