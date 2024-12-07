using Microsoft.Extensions.VectorData;

namespace TrafficJamAnalyzer.Shared.Models.Vectors;
public class TrafficResult
{
    [VectorStoreRecordKey]
    public int Id { get; set; }

    [VectorStoreRecordData]
    public int TrafficId { get; set; }

    [VectorStoreRecordData]
    public string TrafficTitle { get; set; }

    [VectorStoreRecordData]
    public string? CctvDate { get; set; }

    [VectorStoreRecordData]
    public int TrafficAmount { get; set; }

    [VectorStoreRecordData]
    public string CreatedAt { get; set; }
}