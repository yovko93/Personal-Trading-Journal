namespace PersonalTradingJournal.Application.Imports.Topstep;

public enum TopstepImportStatus { Imported, NoChanges, Blocked }

public sealed record TopstepImportResult(TopstepImportStatus Status, int ImportedTradeCount,
    int SkippedDuplicateTradeCount, int CreatedInstrumentCount, IReadOnlyList<Guid> ImportedTradeIds,
    IReadOnlyList<Guid> DuplicateTradeIds, IReadOnlyList<Guid> CreatedInstrumentIds,
    string? ConflictCode = null, string? Message = null)
{
    public static TopstepImportResult Blocked(string code, string message) =>
        new(TopstepImportStatus.Blocked, 0, 0, 0, [], [], [], code, message);
}

public static class TopstepImportConflictCodes
{
    public const string ReviewRequired = "REVIEW_REQUIRED";
    public const string SourceChanged = "SOURCE_CHANGED";
    public const string ReferenceDataChanged = "REFERENCE_DATA_CHANGED";
    public const string SourceIdentityConflict = "SOURCE_IDENTITY_CONFLICT";
    public const string EconomicsMismatch = "PERSISTED_ECONOMICS_MISMATCH";
}

/// <summary>The caller owns a newly opened complete source stream; confirmation leaves it open.</summary>
public sealed record TopstepImportRequest(TopstepImportPreview Preview, TopstepPreviewReview Review,
    string FileName, Stream Source, DateTimeOffset ImportedAtUtc);

public interface ITopstepImportStore
{
    Task<TopstepImportResult> ImportAsync(TopstepImportRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Thread-safe retained-data generations, advanced only after a committed import.</summary>
public sealed class TopstepImportChangeTracker
{
    private long _tradesVersion;
    private long _instrumentsVersion;
    public long TradesVersion => Interlocked.Read(ref _tradesVersion);
    public long InstrumentsVersion => Interlocked.Read(ref _instrumentsVersion);
    internal void Committed(TopstepImportResult result)
    {
        if (result.Status != TopstepImportStatus.Imported) return;
        if (result.ImportedTradeCount > 0) Interlocked.Increment(ref _tradesVersion);
        if (result.CreatedInstrumentCount > 0) Interlocked.Increment(ref _instrumentsVersion);
    }
}
