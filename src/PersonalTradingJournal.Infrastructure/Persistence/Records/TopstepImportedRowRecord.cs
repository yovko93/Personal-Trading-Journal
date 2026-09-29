namespace PersonalTradingJournal.Infrastructure.Persistence.Records;

/// <summary>Topstep/account-scoped closed-row provenance; execution IDs below are internal derived records, not broker fills.</summary>
public sealed class TopstepImportedRowRecord
{
    public Guid Id { get; set; }
    public Guid TradeId { get; set; }
    public Guid TradingAccountIdAtImport { get; set; }
    public string SourceId { get; set; } = null!;
    public string EconomicFingerprint { get; set; } = null!;
    public string SourceRowJson { get; set; } = null!;
    public string PreviewFingerprint { get; set; } = null!;
    public string SourceContentSha256 { get; set; } = null!;
    public string Representation { get; set; } = "TopstepClosedRowDerivedEntryExitV1";
    public Guid DerivedEntryExecutionId { get; set; }
    public Guid DerivedExitExecutionId { get; set; }
    public DateTimeOffset ImportedAtUtc { get; set; }
}
