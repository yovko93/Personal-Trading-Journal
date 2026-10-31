using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Common.Storage;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Infrastructure.Backups;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Screenshots;

/// <summary>Production cleanup rechecks ownership, not only a previously committed deletion plan.</summary>
internal sealed class RetainedScreenshotFileStorage(IApplicationPaths paths, IDbContextFactory<JournalDbContext> factory)
    : ITradeScreenshotFileStorage
{
    private readonly LocalTradeScreenshotFileStorage _files = new(paths);
    public Task<string> StoreAsync(Stream content, string extension, CancellationToken cancellationToken = default) => _files.StoreAsync(content, extension, cancellationToken);
    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default) => _files.OpenReadAsync(key, cancellationToken);
    public Task DeleteIfExistsAsync(string key, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        string path = _files.ResolveStoragePath(key);
        SqliteDatabaseSnapshotService.EnsureNoLinks(path);
        // Lock order matches backup. The capture lease is thread-affine: stay on one worker.
        using var lease = ScreenshotCaptureLease.Acquire(paths.ScreenshotsDirectory, cancellationToken);
        using var db = factory.CreateDbContext(); db.Database.OpenConnection();
        using var transaction = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
        db.Database.UseTransaction(transaction);
        var references = db.TradeScreenshots.Select(s => s.StorageKey)
            .Concat(db.PlaybookExamples.Where(e => e.ImageStorageKey != null).Select(e => e.ImageStorageKey!))
            .Concat(db.CurrentPlaybookExamples.Where(e => e.ImageStorageKey != null).Select(e => e.ImageStorageKey!))
            .Concat(db.TradingPlanAssets.Select(e => e.StorageKey))
            .Concat(db.NotebookOwnedImageFiles.Select(e => e.StorageKey))
            .Concat(db.NotebookImages.Where(e => e.StorageKey != null).Select(e => e.StorageKey!));
        foreach (string reference in references.AsEnumerable())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (StringComparer.OrdinalIgnoreCase.Equals(reference, key)) return;
        }
        cancellationToken.ThrowIfCancellationRequested();
        _files.DeleteIfExistsAsync(key, cancellationToken).GetAwaiter().GetResult();
    }, cancellationToken);
}
