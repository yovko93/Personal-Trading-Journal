using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Setups;

public sealed class TradingSetupPlaybookStore(IDbContextFactory<JournalDbContext> factory)
    : ITradingSetupPlaybookStore, ITradingSetupPlaybookHistoryReader
{
    public async Task<TradingSetupPlaybookRevision?> GetLatestAsync(Guid tradingSetupId, CancellationToken cancellationToken = default) =>
        (await GetHistoryAsync(tradingSetupId, null, 1, cancellationToken)).SingleOrDefault();

    public async Task<TradingSetupPlaybookRevision?> GetRevisionAsync(Guid setupId, int revision, CancellationToken cancellationToken = default)
    {
        ValidateId(setupId);
        if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await ReadRevisionAsync(db, setupId, revision, cancellationToken);
    }

    public async Task<IReadOnlyList<TradingSetupPlaybookRevision>> GetHistoryAsync(Guid setupId, int? beforeRevision,
        int limit, CancellationToken cancellationToken = default)
    {
        ValidateId(setupId);
        if (limit is < 1 or > 100 || beforeRevision is < 1) throw new ArgumentOutOfRangeException(nameof(limit));
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var records = await db.PlaybookRevisions.AsNoTracking().Where(r => r.TradingSetupId == setupId &&
                (!beforeRevision.HasValue || r.Revision < beforeRevision.Value))
            .OrderByDescending(r => r.Revision).Take(limit).ToArrayAsync(cancellationToken);
        int[] revisions = records.Select(r => r.Revision).ToArray();
        var examples = await db.PlaybookExamples.AsNoTracking().Where(e => e.TradingSetupId == setupId && revisions.Contains(e.Revision))
            .ToArrayAsync(cancellationToken);
        return records.Select(r => PlaybookPersistenceMapper.ToDomain(r, examples.Where(e => e.Revision == r.Revision))).ToArray();
    }

    public async Task<bool> TryAppendAsync(TradingSetupPlaybookRevision revision, int expectedRevision,
        DateTimeOffset expectedSetupUpdatedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(revision);
        if (expectedRevision < 0 || expectedRevision == int.MaxValue || revision.Revision != expectedRevision + 1)
            throw new ArgumentException("A consecutive expected revision is required.", nameof(expectedRevision));
        if (expectedSetupUpdatedAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("UTC required.", nameof(expectedSetupUpdatedAtUtc));
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        // Reserve the SQLite writer before reading any mutable owner or current revision.
        await using var transaction = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
        await db.Database.UseTransactionAsync(transaction, cancellationToken);
        var setup = await db.TradingSetups.AsNoTracking().SingleOrDefaultAsync(s => s.Id == revision.TradingSetupId, cancellationToken);
        if (setup is null || !setup.IsActive || setup.UpdatedAtUtc != expectedSetupUpdatedAtUtc ||
            setup.Name != revision.SetupName || setup.Description != revision.SetupDescription || revision.RecordedAtUtc < setup.UpdatedAtUtc)
            return false;
        var latest = await db.PlaybookRevisions.AsNoTracking().Where(r => r.TradingSetupId == setup.Id)
            .OrderByDescending(r => r.Revision).FirstOrDefaultAsync(cancellationToken);
        if ((latest?.Revision ?? 0) != expectedRevision || (latest is not null && revision.RecordedAtUtc < latest.RecordedAtUtc))
            return false;
        Guid[] screenshotIds = revision.Guidance.Examples.Where(e => e.SourceScreenshotId.HasValue)
            .Select(e => e.SourceScreenshotId!.Value).Distinct().ToArray();
        var screenshots = await db.TradeScreenshots.AsNoTracking().Where(s => screenshotIds.Contains(s.Id)).ToArrayAsync(cancellationToken);
        foreach (var example in revision.Guidance.Examples.Where(e => e.SourceScreenshotId.HasValue))
            if (!screenshots.Any(s => s.Id == example.SourceScreenshotId && s.TradeId == example.SourceTradeId))
                throw new InvalidOperationException("An example screenshot is unavailable or belongs to another Trade. Reload the examples.");
        db.PlaybookRevisions.Add(PlaybookPersistenceMapper.ToRecord(revision));
        db.PlaybookExamples.AddRange(PlaybookPersistenceMapper.Examples(revision));
        await db.SaveChangesAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await transaction.CommitAsync(CancellationToken.None);
        return true;
    }

    internal static async Task<TradingSetupPlaybookRevision?> ReadRevisionAsync(JournalDbContext db, Guid setupId, int revision, CancellationToken token)
    {
        var record = await db.PlaybookRevisions.AsNoTracking().SingleOrDefaultAsync(r => r.TradingSetupId == setupId && r.Revision == revision, token);
        if (record is null) return null;
        var examples = await db.PlaybookExamples.AsNoTracking().Where(e => e.TradingSetupId == setupId && e.Revision == revision).ToArrayAsync(token);
        return PlaybookPersistenceMapper.ToDomain(record, examples);
    }

    private static void ValidateId(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("A Setup identity is required.", nameof(id));
    }
}
