namespace PersonalTradingJournal.Infrastructure.Persistence.Records;

public sealed class TradingPlanRecord
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public DateOnly NewYorkDate { get; set; }
    public long Version { get; set; }
    public int LatestReadyRevision { get; set; }
    public string? DraftJson { get; set; }
    public string? ReferenceStateJson { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
public sealed class TradingPlanReadyRecord
{
    public Guid PlanId { get; set; }
    public int Revision { get; set; }
    public string SnapshotJson { get; set; } = "";
    public DateTimeOffset ReadyAtUtc { get; set; }
}
