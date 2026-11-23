namespace PersonalTradingJournal.Infrastructure.Persistence.Records;

/// <summary>Immutable image ownership recorded once during Playbook retirement.</summary>
public sealed class ArchivedPlaybookFile
{
    public string StorageKey { get; init; } = null!;
    public long? SizeBytes { get; init; }
    public string? Sha256 { get; init; }
}
