namespace PersonalTradingJournal.Infrastructure.Persistence.Records;

/// <summary>Independent saved snapshot. No FK/cascade to mutable Accounts, Trades, Journals or Rules.</summary>
public sealed class PeriodReviewRecord
{
    public Guid Id { get; set; }
    public int Kind { get; set; }
    public DateOnly FirstDate { get; set; }
    public DateOnly LastDate { get; set; }
    public DateTimeOffset FromUtc { get; set; }
    public DateTimeOffset BeforeUtc { get; set; }
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
