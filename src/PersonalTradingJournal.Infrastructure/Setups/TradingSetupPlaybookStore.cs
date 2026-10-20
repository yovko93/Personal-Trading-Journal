using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Setups;

public sealed class TradingSetupPlaybookStore(IDbContextFactory<JournalDbContext> factory, IPlaybookImageStorage? images = null)
    : ITradingSetupPlaybookStore, ITradingSetupPlaybookHistoryReader
{
    public async Task<TradingSetupPlaybookRevision?> GetLatestAsync(Guid tradingSetupId, CancellationToken cancellationToken = default)
    {
        ValidateId(tradingSetupId);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        return await ReadCurrentAsync(db, tradingSetupId, cancellationToken);
    }

    internal static async Task<TradingSetupPlaybookRevision?> ReadCurrentAsync(JournalDbContext db, Guid id, CancellationToken ct)
    {
        var row = await db.CurrentPlaybooks.AsNoTracking().SingleOrDefaultAsync(x => x.TradingSetupId == id, ct);
        if (row?.DefinitionJson is { } json)
        {
            var definition = PlaybookPersistenceMapper.Deserialize(json);
            if (definition.TradingSetupId != id || definition.Revision != row.Version)
                throw new InvalidDataException("Current Playbook identity mismatch.");
            return definition;
        }
        var number = row?.LegacyRevision ?? await db.PlaybookRevisions.Where(x => x.TradingSetupId == id).MaxAsync(x => (int?)x.Revision, ct);
        return number is { } version ? await ReadRevisionAsync(db, id, version, ct) : null;
    }

    public async Task<bool> TrySaveCurrentAsync(TradingSetupPlaybookRevision definition, int expectedVersion,
        DateTimeOffset expectedSetupUpdatedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (expectedVersion < 0 || expectedVersion == int.MaxValue || definition.Revision != expectedVersion + 1)
            throw new ArgumentException("A consecutive concurrency token is required.", nameof(expectedVersion));
        if (expectedSetupUpdatedAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("UTC required.", nameof(expectedSetupUpdatedAtUtc));
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await using var transaction = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
        await db.Database.UseTransactionAsync(transaction, cancellationToken);
        var setup = await db.TradingSetups.AsNoTracking().SingleOrDefaultAsync(x => x.Id == definition.TradingSetupId, cancellationToken);
        var current = await ReadCurrentAsync(db, definition.TradingSetupId, cancellationToken);
        if (setup is null || !setup.IsActive || setup.UpdatedAtUtc != expectedSetupUpdatedAtUtc ||
            setup.Name != definition.SetupName || setup.Description != definition.SetupDescription ||
            definition.RecordedAtUtc < setup.UpdatedAtUtc || (current?.Revision ?? 0) != expectedVersion ||
            current is not null && definition.RecordedAtUtc < current.RecordedAtUtc) return false;
        foreach (var example in definition.Guidance.Examples)
        {
            if (example.Image is { } image)
            {
                if (images is null) throw new InvalidOperationException("Playbook image storage is unavailable.");
                _ = await images.ReadAsync(image, cancellationToken);
            }
            if (example.SourceScreenshotId is { } screenshot && !await db.TradeScreenshots.AnyAsync(
                x => x.Id == screenshot && x.TradeId == example.SourceTradeId, cancellationToken))
                throw new InvalidOperationException("An example screenshot is unavailable or belongs to another Trade.");
        }
        var row = await db.CurrentPlaybooks.SingleOrDefaultAsync(x => x.TradingSetupId == definition.TradingSetupId, cancellationToken);
        if (row is null) { row = new() { TradingSetupId = definition.TradingSetupId }; db.CurrentPlaybooks.Add(row); }
        row.Version = definition.Revision; row.LegacyRevision = null;
        row.DefinitionJson = PlaybookPersistenceMapper.Serialize(definition);
        await db.CurrentPlaybookExamples.Where(x => x.TradingSetupId == definition.TradingSetupId).ExecuteDeleteAsync(cancellationToken);
        db.CurrentPlaybookExamples.AddRange(definition.Guidance.Examples.Select(x => new CurrentPlaybookExampleRecord
        {
            TradingSetupId = definition.TradingSetupId, ExampleId = x.Id, SourceScreenshotId = x.SourceScreenshotId,
            ImageStorageKey = x.Image?.StorageKey, ImageSizeBytes = x.Image?.SizeBytes, ImageSha256 = x.Image?.Sha256,
        }));
        await db.SaveChangesAsync(cancellationToken);
        var readback = await ReadCurrentAsync(db, definition.TradingSetupId, cancellationToken);
        if (readback is null || PlaybookPersistenceMapper.Serialize(readback) != row.DefinitionJson)
            throw new InvalidDataException("Current Playbook readback failed.");
        cancellationToken.ThrowIfCancellationRequested();
        await transaction.CommitAsync(CancellationToken.None);
        return true;
    }

    // Called only inside a capture's writer transaction. Ordinary authoring never appends history.
    internal static async Task RetainCaptureAsync(JournalDbContext db, TradingSetupPlaybookRevision definition, CancellationToken ct)
    {
        var prior = await ReadRevisionAsync(db, definition.TradingSetupId, definition.Revision, ct);
        if (prior is not null)
        {
            if (PlaybookPersistenceMapper.Serialize(prior) != PlaybookPersistenceMapper.Serialize(definition))
                throw new InvalidDataException("Historical Playbook identity collision.");
            return;
        }
        db.PlaybookRevisions.Add(PlaybookPersistenceMapper.ToRecord(definition));
        db.PlaybookExamples.AddRange(PlaybookPersistenceMapper.Examples(definition));
        await db.SaveChangesAsync(ct);
    }

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
        // Cleanup takes the same writer reservation before checking references. Once verified,
        // an application cleanup cannot remove an image between this check and the commit.
        foreach (var example in revision.Guidance.Examples.Where(e => e.Image is not null))
        {
            if (images is null) throw new InvalidOperationException("Playbook image storage is unavailable.");
            _ = await images.ReadAsync(example.Image!, cancellationToken);
        }
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
