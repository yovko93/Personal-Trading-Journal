using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Common.Storage;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Backups;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;
using PersonalTradingJournal.Infrastructure.Screenshots;

namespace PersonalTradingJournal.Infrastructure.Notebook;

/// <summary>Dedicated notebook-* namespace in the established managed screenshots root; originals are immutable.
/// This preserves archive v1's existing attachment paths and capture lease, without exposing source paths.</summary>
public sealed partial class NotebookImageFiles : INotebookImageFiles
{
    private readonly IApplicationPaths paths;
    private readonly IDbContextFactory<JournalDbContext> factory;
    private readonly INotebookImageValidator validator;
    private readonly LocalTradeScreenshotFileStorage _resolver;
    private readonly Action? _written;
    public NotebookImageFiles(IApplicationPaths paths, IDbContextFactory<JournalDbContext> factory, INotebookImageValidator validator)
        : this(paths, factory, validator, null) { }
    // Controlled fault seam after private staging is flushed/closed, before final publication.
    internal NotebookImageFiles(IApplicationPaths paths, IDbContextFactory<JournalDbContext> factory,
        INotebookImageValidator validator, Action? written)
    { this.paths = paths; this.factory = factory; this.validator = validator; _resolver = new(paths); _written = written; }
    internal static bool Owns(string key, Guid note, Guid image) => key.StartsWith($"notebook-{note:N}-{image:N}-", StringComparison.Ordinal);
    public async Task<NotebookStoredImage> StageAsync(Guid noteId, Guid imageId, Stream content, CancellationToken token)
    {
        NotebookLimits.Id(noteId, nameof(noteId)); NotebookLimits.Id(imageId, nameof(imageId));
        if (!content.CanRead) throw new NotebookImageFileException(NotebookImageStatus.Unreadable);
        using var buffer = new MemoryStream(); byte[] chunk = new byte[65536];
        int count;
        while ((count = await content.ReadAsync(chunk, token)) != 0)
        {
            if (buffer.Length + count > NotebookLimits.ImageBytes) throw new NotebookImageFileException(NotebookImageStatus.TooLarge);
            await buffer.WriteAsync(chunk.AsMemory(0, count), token);
        }
        token.ThrowIfCancellationRequested();
        byte[] bytes = buffer.ToArray();
        string extension = Admit(bytes);
        token.ThrowIfCancellationRequested();
        var image = new NotebookStoredImage($"notebook-{noteId:N}-{imageId:N}-{Guid.NewGuid():N}{extension}", bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));
        string final = Resolve(image.StorageKey), temporary = Resolve($"notebook-{Guid.NewGuid():N}.png") + ".partial";
        SqliteDatabaseSnapshotService.EnsureNoLinks(paths.ScreenshotsDirectory);
        Directory.CreateDirectory(paths.ScreenshotsDirectory);
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                65536, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                if (!BackupFileIdentity.IsUnaliased(output, temporary)) throw new IOException("Unsafe managed staging path.");
                await output.WriteAsync(bytes, token); await output.FlushAsync(token); output.Flush(true);
            }
            _written?.Invoke();
            token.ThrowIfCancellationRequested(); SqliteDatabaseSnapshotService.EnsureNoLinks(final);
            File.Move(temporary, final); // Publication boundary: always return its identity, even if cancellation arrives now.
            return image;
        }
        catch (Exception failure)
        {
            // A failure before publication owns only this private staging file. Never delete the final destination.
            try { File.Delete(temporary); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            { throw new NotebookImageFileException(failure is OperationCanceledException ? NotebookImageStatus.Cancelled : NotebookImageStatus.Unreadable, cleanupPending: true); }
            throw;
        }
    }
    private string Admit(byte[] bytes)
    {
        if (bytes.Length == 0) throw new NotebookImageFileException(NotebookImageStatus.Invalid);
        string? extension = NotebookImageAdmission.Extension(bytes);
        if (extension is null) throw new NotebookImageFileException(NotebookImageStatus.Unsupported);
        if (NotebookImageAdmission.HasAnimation(bytes, extension)) throw new NotebookImageFileException(NotebookImageStatus.Unsupported);
        var probe = validator.Validate(bytes);
        if (probe.Status != NotebookImageStatus.Success) throw new NotebookImageFileException(probe.Status);
        if (probe.Extension != extension || probe.Width <= 0 || probe.Height <= 0)
            throw new NotebookImageFileException(NotebookImageStatus.Invalid);
        if (probe.Width > 8192 || probe.Height > 8192 || (long)probe.Width * probe.Height > 16_000_000)
            throw new NotebookImageFileException(NotebookImageStatus.TooLarge);
        return extension;
    }
    private string Resolve(string key)
    {
        if (!key.StartsWith("notebook-", StringComparison.Ordinal)) throw new ArgumentException("Not a managed Notebook key.");
        var path = _resolver.ResolveStoragePath(key); SqliteDatabaseSnapshotService.EnsureNoLinks(path); return path;
    }
    public async Task<byte[]> ReadAsync(NotebookStoredImage image, CancellationToken token, bool requireAdmission = false)
    {
        if (image.SizeBytes is <= 0 or > NotebookLimits.ImageBytes) throw new NotebookImageFileException(NotebookImageStatus.TooLarge);
        string path = _resolver.ResolveStoragePath(image.StorageKey); SqliteDatabaseSnapshotService.EnsureNoLinks(path);
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        if (!BackupFileIdentity.IsUnaliased(input, path) || input.Length != image.SizeBytes) throw new NotebookImageFileException(NotebookImageStatus.Unreadable);
        byte[] bytes = new byte[checked((int)image.SizeBytes)]; await input.ReadExactlyAsync(bytes, token);
        if (input.ReadByte() != -1 || !string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), image.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new NotebookImageFileException(NotebookImageStatus.Unreadable);
        if (requireAdmission && Path.GetExtension(image.StorageKey) != Admit(bytes)) throw new NotebookImageFileException(NotebookImageStatus.Invalid);
        token.ThrowIfCancellationRequested();
        return bytes;
    }
    public Task<bool> DiscardUncommittedAsync(NotebookStoredImage image, CancellationToken token) => Task.Run(() =>
    {
        try
        {
            string path = Resolve(image.StorageKey);
            using var lease = ScreenshotCaptureLease.Acquire(paths.ScreenshotsDirectory, token);
            using var db = factory.CreateDbContext(); db.Database.OpenConnection();
            using var tx = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
            db.Database.UseTransaction(tx);
            // Lock order matches archive capture. The ledger includes removed/recovery/undo references indefinitely.
            var keys = db.NotebookOwnedImageFiles.Select(x => x.StorageKey)
                .Concat(db.NotebookImages.Where(x => x.StorageKey != null).Select(x => x.StorageKey!))
                .Concat(db.TradeScreenshots.Select(x => x.StorageKey))
                .Concat(db.PlaybookExamples.Where(x => x.ImageStorageKey != null).Select(x => x.ImageStorageKey!))
                .Concat(db.CurrentPlaybookExamples.Where(x => x.ImageStorageKey != null).Select(x => x.ImageStorageKey!))
                .Concat(db.TradingPlanAssets.Select(x => x.StorageKey));
            foreach (var key in keys.AsEnumerable())
            { token.ThrowIfCancellationRequested(); if (StringComparer.OrdinalIgnoreCase.Equals(key, image.StorageKey)) return true; }
            var parts = image.StorageKey.Split('-');
            if (parts.Length != 4 || !Guid.TryParseExact(parts[1], "N", out var noteId) || !Guid.TryParseExact(parts[2], "N", out var imageId)) return false;
            // A checkpoint can win while an import is awaiting its canonical write. Preserve its bytes/metadata too.
            // Unreadable/future checkpoints are conservatively treated as potential references, never rewritten.
            var drafts = db.NotebookRecoveryDrafts.AsNoTracking().Select(x => new { x.Id, x.EnvelopeVersion, Length = x.DocumentJson.Length }).Take(129).ToArray();
            bool recovered = drafts.Length > 128; var codec = new NotebookJsonCodec();
            foreach (var draft in drafts)
            {
                token.ThrowIfCancellationRequested();
                if (draft.EnvelopeVersion != 1 || draft.Length > 1024 * 1024) { recovered = true; break; }
                var json = db.NotebookRecoveryDrafts.Where(x => x.Id == draft.Id).Select(x => x.DocumentJson).Single();
                var content = codec.Decode(json);
                if (content.Status != NotebookContentStatus.Success || content.Document!.ImageNodes.Any(n => n.ImageId == imageId)) { recovered = true; break; }
            }
            if (recovered)
            {
                if (db.NotebookOwnedImageFiles.Any(x => x.Id == imageId)) return false;
                db.NotebookOwnedImageFiles.Add(new NotebookOwnedImageRecord { Id = imageId, NoteId = noteId, StorageKey = image.StorageKey, SizeBytes = image.SizeBytes, Sha256 = image.Sha256 });
                db.SaveChanges(); tx.Commit(); return true;
            }
            token.ThrowIfCancellationRequested(); File.Delete(path); return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException or DbUpdateException or InvalidOperationException or OperationCanceledException or ArgumentException) { return false; }
    }, CancellationToken.None);
}
