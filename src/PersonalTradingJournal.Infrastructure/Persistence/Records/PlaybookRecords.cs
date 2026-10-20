namespace PersonalTradingJournal.Infrastructure.Persistence.Records;

// One mutable definition per existing Setup. LegacyRevision is only the migration bridge;
// after the first direct edit DefinitionJson is authoritative and Version is a concurrency token.
public sealed class CurrentPlaybookRecord
{
    public Guid TradingSetupId { get; set; }
    public int Version { get; set; }
    public int? LegacyRevision { get; set; }
    public string? DefinitionJson { get; set; }
}

public sealed class CurrentPlaybookExampleRecord
{
    public Guid TradingSetupId { get; set; }
    public Guid ExampleId { get; set; }
    public Guid? SourceScreenshotId { get; set; }
    public string? ImageStorageKey { get; set; }
    public long? ImageSizeBytes { get; set; }
    public string? ImageSha256 { get; set; }
}

public sealed class PlaybookRevisionRecord
{
    public Guid TradingSetupId { get; set; }
    public int Revision { get; set; }
    public int ContractVersion { get; set; }
    public string SetupName { get; set; } = null!;
    public string? SetupDescription { get; set; }
    public string MarketContext { get; set; } = null!;
    public string EntryConditions { get; set; } = null!;
    public string Confirmations { get; set; } = null!;
    public string Invalidation { get; set; } = null!;
    public string ExecutionGuidance { get; set; } = null!;
    public DateTimeOffset RecordedAtUtc { get; set; }
}

public sealed class PlaybookExampleRecord
{
    public Guid TradingSetupId { get; set; }
    public int Revision { get; set; }
    public Guid ExampleId { get; set; }
    public int Position { get; set; }
    public string Annotation { get; set; } = null!;
    public Guid? SourceTradeId { get; set; }
    public Guid? SourceScreenshotId { get; set; }
    public int Kind { get; set; }
    public string? ImageStorageKey { get; set; }
    public string? ImageFileName { get; set; }
    public long? ImageSizeBytes { get; set; }
    public string? ImageSha256 { get; set; }
}

public sealed class TradePlaybookContextRecord
{
    public Guid TradeId { get; set; }
    public int ContractVersion { get; set; }
    public Guid TradingSetupId { get; set; }
    public int Revision { get; set; }
    public DateTimeOffset TradeOpenedAtUtc { get; set; }
    public DateTimeOffset CapturedAtUtc { get; set; }
    public string DefinitionJson { get; set; } = null!;
}

public sealed class TradePlaybookAssociationRecord
{
    public Guid TradeId { get; set; }
    public int Sequence { get; set; }
    public Guid? TradingSetupId { get; set; }
    public int? Revision { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
    public DateTimeOffset TradeOpenedAtUtc { get; set; }
    public string? DefinitionJson { get; set; }
}
