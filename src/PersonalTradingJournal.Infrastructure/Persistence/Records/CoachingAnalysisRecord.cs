namespace PersonalTradingJournal.Infrastructure.Persistence.Records;

/// <summary>Independent immutable snapshot; intentionally no navigation/FK to mutable source entities.</summary>
public sealed class CoachingAnalysisRecord
{
    public Guid Id { get; set; }
    public DateOnly ReviewDate { get; set; }
    public int ScopeKind { get; set; }
    public Guid? AccountId { get; set; }
    public string? AccountDisplayName { get; set; }
    public DateTimeOffset GeneratedAtUtc { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string EvidenceContractVersion { get; set; } = string.Empty;
    public string ResponseContractVersion { get; set; } = string.Empty;
    public string PacketId { get; set; } = string.Empty;
    public string EvidenceJson { get; set; } = string.Empty;
    public string ResponseJson { get; set; } = string.Empty;
    public string MetadataJson { get; set; } = string.Empty;
}
