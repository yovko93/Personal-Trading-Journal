namespace PersonalTradingJournal.Infrastructure.Persistence.Records;

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
