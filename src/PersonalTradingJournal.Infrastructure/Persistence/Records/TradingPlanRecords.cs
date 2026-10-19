namespace PersonalTradingJournal.Infrastructure.Persistence.Records;

public sealed class TradingPlanRecord
{
    public Guid Id { get; set; }
    public Guid? AccountId { get; set; }
    public DateOnly NewYorkDate { get; set; }
    public long Version { get; set; }
    public int LatestReadyRevision { get; set; }
    public string? DraftJson { get; set; }
    public string? ReferenceStateJson { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
}
public sealed class TradingPlanReadyRecord
{
    public Guid PlanId { get; set; }
    public int Revision { get; set; }
    public string SnapshotJson { get; set; } = "";
    public DateTimeOffset ReadyAtUtc { get; set; }
}

// Append-only retention ledger includes assets removed from a later Draft. Ready and saved
// Review references therefore never depend on the current Draft's attachment selection.
public sealed class TradingPlanAssetRecord
{
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public Guid InstrumentId { get; set; }
    public int Timeframe { get; set; }
    public string StorageKey { get; set; } = "";
    public string FileName { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = "";
}
