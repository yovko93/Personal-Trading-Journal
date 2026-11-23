namespace PersonalTradingJournal.Infrastructure.Persistence.Records;

public sealed class SetupExampleRecord
{
    public Guid Id { get; set; }
    public Guid SetupId { get; set; }
    public string? Timeframe { get; set; }
    public required string StorageKey { get; set; }
    public required string FileName { get; set; }
    public long SizeBytes { get; set; }
    public required string Sha256 { get; set; }
}
