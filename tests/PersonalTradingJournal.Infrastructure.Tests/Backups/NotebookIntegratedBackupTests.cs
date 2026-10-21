using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Backups;
using PersonalTradingJournal.Infrastructure.Notebook;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Tests.Notebook;
using Fixture = PersonalTradingJournal.Infrastructure.Tests.Backups.BackupArchiveTests.Fixture;

namespace PersonalTradingJournal.Infrastructure.Tests.Backups;

public sealed partial class NotebookPortabilityTests
{
    private static (NotebookService Notes, NotebookImageService Images) ImageOperations(Fixture f)
    {
        var factory = f.Services.GetRequiredService<IDbContextFactory<JournalDbContext>>();
        var codec = new NotebookJsonCodec();
        var files = new NotebookImageFiles(f.Paths, factory, new NotebookImageTests.Probe());
        var notes = new NotebookService(factory, codec, imageFiles: files);
        return (notes, new(factory, notes, codec, files));
    }

    private static async Task<NotebookNoteSnapshot> AddOriginal(Fixture f, NotebookNoteSnapshot note)
    {
        var id = Guid.NewGuid(); using var input = new MemoryStream(NotebookImageTests.Png);
        return NotebookFixture.Success(await ImageOperations(f).Images.ImportAsync(
            new(note.Id, note.Version, id, NotebookImageTests.Append(note.Document, id, "Original caption")), input));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IntegratedRestoreReflectsChosenBackupIncludingIndependentCopiesAndDeletedNoteRetention(bool older)
    {
        await using var source = await Fixture.Create(); var seeded = await Seed(source, "trade.png");
        var s = ImageOperations(source);
        var note = NotebookFixture.Success(await s.Notes.ExecuteAsync(new CreateNotebookNote(null, "Managed rich note")));
        note = NotebookFixture.Success(await s.Notes.ExecuteAsync(new UpdateNotebookContent(note.Id, note.Version, NotebookCodecTests.Rich(), [])));
        note = await AddOriginal(source, note);
        var copy = NotebookFixture.Success(await s.Notes.ExecuteAsync(new DuplicateNotebookNote(note.Id, note.Version)));
        var deleted = NotebookFixture.Success(await s.Notes.ExecuteAsync(new DuplicateNotebookNote(note.Id, note.Version)));
        Assert.NotEqual(note.Images[0].Id, copy.Images[0].Id);
        Assert.NotEqual(note.Images[0].StorageKey, copy.Images[0].StorageKey);
        var recovery = source.Services.GetRequiredService<INotebookRecoveryStore>();
        var draft = new NotebookRecoveryDraft(Guid.NewGuid(), copy.Id, copy.Version, 1, "Checkpoint only", copy.Document, DateTimeOffset.UtcNow);
        Assert.Equal(NotebookWriteStatus.Success, await recovery.PutAsync(draft, 0));
        await using (var db = source.Context())
        {
            // Links retain stable identity after the target disappears; the Trade remains available.
            db.TradingSetups.RemoveRange(db.TradingSetups); await db.SaveChangesAsync();
        }
        var before = await Export(source);
        string oldArchive = Path.Combine(source.Root, "before-delete.ptjbackup");
        Assert.True((await source.Service().CreateAsync(oldArchive)).Succeeded);
        deleted = NotebookFixture.Success(await s.Notes.ExecuteAsync(new TrashNotebookNote(deleted.Id, deleted.Version)));
        var deletion = await s.Notes.ExecuteAsync(new DeleteNotebookNote(deleted.Id, deleted.Version, true));
        Assert.Equal(NotebookWriteStatus.Success, deletion.Status);
        Assert.True(deletion.CleanupPending); // Unknown/future bodies conservatively retain possible references.
        await using (var db = source.Context())
        {
            var retained = await db.NotebookOwnedImageFiles.SingleAsync(x => x.NoteId == deleted.Id);
            Assert.False(retained.CleanupPending); // Not yet proven safe to remove: still required by backup.
        }
        var after = await Export(source);
        Assert.True((await source.Service().CreateAsync(source.Output)).Succeeded);
        string archive = older ? oldArchive : source.Output;
        var inspected = await new RestorePreflightService().InspectAsync(archive, source.Root);
        Assert.True(inspected.Validated);
        await using var target = await Fixture.Create(); await Seed(target, "replaced.png");
        await target.Services.DisposeAsync(); JournalDataSession.ClearOwnedPools(target.Paths);
        Assert.Equal(JournalRestoreStatus.Restored, (await new JournalRestoreService(target.Paths)
            .RestoreAsync(new(archive, inspected.Summary!.ArchiveSha256))).Status);
        Assert.Equal((older ? before : after).GetRawText(), (await Export(target)).GetRawText());
        await using var fresh = new ServiceCollection().AddPersistence(target.Paths).BuildServiceProvider();
        var notes = fresh.GetRequiredService<INotebookService>();
        Assert.Equal(older ? NotebookLoadStatus.Success : NotebookLoadStatus.NotFound, (await notes.LoadAsync(deleted.Id)).Status);
        foreach (var n in new[] { note, copy })
        {
            var loaded = (await notes.LoadAsync(n.Id)).Note!;
            Assert.Equal(new NotebookJsonCodec().Encode(n.Document), new NotebookJsonCodec().Encode(loaded.Document));
            var original = await fresh.GetRequiredService<INotebookImages>().ReadOriginalAsync(n.Id, n.Images[0].Id);
            Assert.Equal(NotebookImageStatus.Success, original.Status); Assert.Equal(NotebookImageTests.Png, original.Original);
        }
        Assert.Equal(NotebookImageTests.Png, await File.ReadAllBytesAsync(Path.Combine(target.Paths.ScreenshotsDirectory, deleted.Images[0].StorageKey!)));
        var links = await notes.ResolveLinksAsync((await notes.LoadAsync(seeded.Active)).Note!.Links);
        Assert.Equal(NotebookTargetAvailability.Available, links.Single(x => x.Link.Kind == NotebookTargetKind.Trade).Availability);
        Assert.Equal(NotebookTargetAvailability.Unavailable, links.Single(x => x.Link.Kind == NotebookTargetKind.TradingSetup).Availability);
        var recovered = await fresh.GetRequiredService<INotebookRecoveryStore>().LoadAsync(draft.Id);
        Assert.Equal(draft.ExpectedVersion, recovered.Draft!.ExpectedVersion);
        Assert.Equal(draft.Title, recovered.Draft.Title);
        var conflicting = (await fresh.GetRequiredService<INotebookRecoveryStore>().ListAsync()).Items.Single(x => x.NoteId == seeded.Active);
        var conflict = (await fresh.GetRequiredService<INotebookRecoveryStore>().LoadAsync(conflicting.Id)).Draft!;
        Assert.Equal(NotebookWriteStatus.Conflict, (await notes.ExecuteAsync(new SaveNotebookDraft(conflict.NoteId, conflict.ExpectedVersion, conflict.Title, conflict.Document, []))).Status);
        Assert.Equal((older ? before : after).GetRawText(), (await Export(target)).GetRawText());
    }

    [Theory]
    [InlineData(BackupArchivePhase.Snapshot)]
    [InlineData(BackupArchivePhase.Inventory)]
    public async Task AutosaveImportAndCheckpointAreCapturedOnlyAtTheirDurableSnapshotBoundary(BackupArchivePhase at)
    {
        await using var f = await Fixture.Create(); var s = ImageOperations(f);
        var original = NotebookFixture.Success(await s.Notes.ExecuteAsync(new CreateNotebookNote(null, "Before save")));
        NotebookNoteSnapshot? saved = null; Guid draftId = Guid.NewGuid();
        var result = await f.Service((phase, _) =>
        {
            if (phase != at) return;
            saved = NotebookFixture.Success(s.Notes.ExecuteAsync(new SaveNotebookDraft(original.Id, original.Version, "Saved title", NotebookCodecTests.Rich(), [])).GetAwaiter().GetResult());
            saved = AddOriginal(f, saved).GetAwaiter().GetResult();
            Assert.Equal(NotebookWriteStatus.Success, f.Services.GetRequiredService<INotebookRecoveryStore>().PutAsync(
                new(draftId, saved.Id, saved.Version, 1, "Checkpoint title", saved.Document, DateTimeOffset.UtcNow), 0).GetAwaiter().GetResult());
        }).CreateAsync(f.Output);
        Assert.True(result.Succeeded, result.ToString()); Assert.NotNull(saved);
        var preflight = await new RestorePreflightService().InspectAsync(f.Output, f.Root); Assert.True(preflight.Validated);
        await using var target = await Fixture.Create(); await target.Services.DisposeAsync(); JournalDataSession.ClearOwnedPools(target.Paths);
        Assert.Equal(JournalRestoreStatus.Restored, (await new JournalRestoreService(target.Paths).RestoreAsync(new(f.Output, preflight.Summary!.ArchiveSha256))).Status);
        await using var fresh = new ServiceCollection().AddPersistence(target.Paths).BuildServiceProvider();
        var captured = (await fresh.GetRequiredService<INotebookService>().LoadAsync(original.Id)).Note!;
        var expected = at == BackupArchivePhase.Snapshot ? saved : original;
        Assert.Equal(expected.Title, captured.Title); Assert.Equal(expected.Version, captured.Version);
        Assert.Equal(new NotebookJsonCodec().Encode(expected.Document), new NotebookJsonCodec().Encode(captured.Document));
        Assert.Equal(expected.Images.Length, captured.Images.Length);
        Assert.Equal(at == BackupArchivePhase.Snapshot ? 1 : 0, (await fresh.GetRequiredService<INotebookRecoveryStore>().ListAsync()).Items.Length);
        using var zip = ZipFile.OpenRead(f.Output);
        Assert.Equal(at == BackupArchivePhase.Snapshot, zip.GetEntry(BackupArchiveContract.ScreenshotsPrefix + saved.Images[0].StorageKey) is not null);
    }

    [Fact]
    public async Task ClosedSessionRejectsLateAutosaveAndCheckpointAfterOfflineRestore()
    {
        await using var source = await Fixture.Create(); var s = ImageOperations(source);
        var incoming = await AddOriginal(source, NotebookFixture.Success(await s.Notes.ExecuteAsync(new CreateNotebookNote(null, "Incoming"))));
        Assert.True((await source.Service().CreateAsync(source.Output)).Succeeded);
        var preflight = await new RestorePreflightService().InspectAsync(source.Output, source.Root); Assert.True(preflight.Validated);
        await using var target = await Fixture.Create();
        var oldNotes = target.Services.GetRequiredService<INotebookService>();
        var oldRecovery = target.Services.GetRequiredService<INotebookRecoveryStore>();
        NotebookFixture.Success(await oldNotes.ExecuteAsync(new CreateNotebookNote(null, "Old journal")));
        // Retain real services while invalidating their session, as host shutdown does before offline replacement.
        target.Services.GetRequiredService<JournalDataSession>().Dispose(); JournalDataSession.ClearOwnedPools(target.Paths);
        Assert.Equal(JournalRestoreStatus.Restored, (await new JournalRestoreService(target.Paths).RestoreAsync(new(source.Output, preflight.Summary!.ArchiveSha256))).Status);
        var before = await Export(target);
        // Even a coincidentally valid restored ID/version cannot authorize an old-session callback.
        Assert.Equal(NotebookWriteStatus.StorageFailure, (await oldNotes.ExecuteAsync(new SaveNotebookDraft(incoming.Id, incoming.Version, "Stale callback", NotebookDocument.Empty, []))).Status);
        Assert.Equal(NotebookWriteStatus.StorageFailure, await oldRecovery.PutAsync(new(Guid.NewGuid(), incoming.Id, incoming.Version, 1, "Stale checkpoint", incoming.Document, DateTimeOffset.UtcNow), 0));
        Assert.Equal(before.GetRawText(), (await Export(target)).GetRawText());
        await using var fresh = new ServiceCollection().AddPersistence(target.Paths).BuildServiceProvider();
        Assert.Equal("Incoming", (await fresh.GetRequiredService<INotebookService>().LoadAsync(incoming.Id)).Note!.Title);
        Assert.Equal(NotebookImageTests.Png, (await fresh.GetRequiredService<INotebookImages>().ReadOriginalAsync(incoming.Id, incoming.Images[0].Id)).Original);
    }

    [Fact]
    public async Task PermanentDeletionCannotRemoveCapturedOriginalAndFinishesBeforeCompression()
    {
        await using var f = await Fixture.Create(); var s = ImageOperations(f);
        var note = await AddOriginal(f, NotebookFixture.Success(await s.Notes.ExecuteAsync(new CreateNotebookNote())));
        note = NotebookFixture.Success(await s.Notes.ExecuteAsync(new TrashNotebookNote(note.Id, note.Version)));
        var factory = f.Services.GetRequiredService<IDbContextFactory<JournalDbContext>>();
        var files = new CleanupSignal(new NotebookImageFiles(f.Paths, factory, new NotebookImageTests.Probe()));
        var deleting = new NotebookService(factory, new NotebookJsonCodec(), imageFiles: files);
        Task<NotebookWriteResult>? deletion = null;
        var archive = await f.Service((phase, _) =>
        {
            if (phase == BackupArchivePhase.Capture)
            {
                deletion = Task.Run(() => deleting.ExecuteAsync(new DeleteNotebookNote(note.Id, note.Version, true)));
                files.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                using var db = f.Context(); Assert.False(db.NotebookNotes.Any(x => x.Id == note.Id));
                Assert.False(deletion.IsCompleted);
                Assert.True(File.Exists(Path.Combine(f.Paths.ScreenshotsDirectory, note.Images[0].StorageKey!)));
            }
            if (phase == BackupArchivePhase.Package)
            {
                var result = deletion!.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                Assert.Equal(NotebookWriteStatus.Success, result.Status); Assert.False(result.CleanupPending);
                Assert.False(File.Exists(Path.Combine(f.Paths.ScreenshotsDirectory, note.Images[0].StorageKey!)));
            }
        }).CreateAsync(f.Output);
        Assert.NotNull(deletion); await deletion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(archive.Succeeded, archive.ToString());
        var preflight = await new RestorePreflightService().InspectAsync(f.Output, f.Root); Assert.True(preflight.Validated);
        await using var target = await Fixture.Create(); await target.Services.DisposeAsync(); JournalDataSession.ClearOwnedPools(target.Paths);
        Assert.Equal(JournalRestoreStatus.Restored, (await new JournalRestoreService(target.Paths).RestoreAsync(new(f.Output, preflight.Summary!.ArchiveSha256))).Status);
        await using var fresh = new ServiceCollection().AddPersistence(target.Paths).BuildServiceProvider();
        Assert.Equal(NotebookNoteState.Trashed, (await fresh.GetRequiredService<INotebookService>().LoadAsync(note.Id)).Note!.State);
        Assert.Equal(NotebookImageTests.Png, (await fresh.GetRequiredService<INotebookImages>().ReadOriginalAsync(note.Id, note.Images[0].Id)).Original);
    }

    // Signal the real post-commit cleanup boundary without replacing its lease or filesystem behavior.
    private sealed class CleanupSignal(INotebookImageFiles inner) : INotebookImageFiles
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<NotebookStoredImage> StageAsync(Guid note, Guid image, Stream content, CancellationToken token) => inner.StageAsync(note, image, content, token);
        public Task<byte[]> ReadAsync(NotebookStoredImage image, CancellationToken token, bool requireAdmission = false) => inner.ReadAsync(image, token, requireAdmission);
        public Task<bool> DiscardUncommittedAsync(NotebookStoredImage image, CancellationToken token) => inner.DiscardUncommittedAsync(image, token);
        public Task<bool> CleanupDeletedNoteAsync(Guid note, CancellationToken token)
        { Entered.TrySetResult(); return inner.CleanupDeletedNoteAsync(note, token); }
    }
}
