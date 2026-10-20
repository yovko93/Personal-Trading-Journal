using System.Collections.Immutable;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Notebook;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Storage;

namespace PersonalTradingJournal.Infrastructure.Tests.Notebook;

public sealed class NotebookImageTests
{
    internal static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
    internal sealed class Probe(NotebookImageProbe? result = null) : INotebookImageValidator
    {
        public NotebookImageProbe Validate(ReadOnlyMemory<byte> content) => result ?? new(NotebookImageStatus.Success, ".png", 1, 1);
    }
    private static (NotebookImageFiles Files, NotebookService Notes, NotebookImageService Images, LocalApplicationPaths Paths) Services(
        NotebookFixture f, INotebookImageValidator? probe = null, params IInterceptor[] hooks)
    {
        var paths = new LocalApplicationPaths(f.Root); paths.EnsureDirectoriesExist();
        var files = new NotebookImageFiles(paths, f.Factory(), probe ?? new Probe());
        var notes = new NotebookService(f.Factory(hooks), f.Codec, f.Time, files);
        return (files, notes, new(f.Factory(), notes, f.Codec, files), paths);
    }
    internal static NotebookDocument Append(NotebookDocument doc, Guid id, string caption = "Chart") =>
        new([.. doc.Blocks, NotebookNode.Image(Guid.NewGuid(), id, caption, "Alt")]);
    private static async Task<NotebookNoteSnapshot> Import(NotebookImageService images, NotebookNoteSnapshot note, Guid? id = null)
    {
        var image = id ?? Guid.NewGuid(); using var source = new MemoryStream(Png);
        return NotebookFixture.Success(await images.ImportAsync(new(note.Id, note.Version, image, Append(note.Document, image)), source));
    }
    [Fact]
    public async Task ImportCopiesOriginalIntoManagedNamespaceAndSourceCanBeReusedOrDeleted()
    {
        await using var f = await NotebookFixture.Create(); var s = Services(f); var note = await f.CreateNote();
        var id = Guid.NewGuid(); var sourcePath = Path.Combine(f.Root, "chosen.wrong-extension"); await File.WriteAllBytesAsync(sourcePath, Png);
        await using (var source = File.OpenRead(sourcePath))
        {
            note = NotebookFixture.Success(await s.Images.ImportAsync(new(note.Id, note.Version, id, Append(note.Document, id)), source));
            Assert.True(source.CanRead); // Caller owns the stream; no source path crosses the contract.
        }
        await File.WriteAllTextAsync(sourcePath, "reused source"); File.Delete(sourcePath);
        var image = Assert.Single(note.Images); Assert.StartsWith($"notebook-{note.Id:N}-{id:N}-", image.StorageKey);
        var read = await s.Images.ReadOriginalAsync(note.Id, id);
        Assert.Equal(NotebookImageStatus.Success, read.Status); Assert.Equal(Png, read.Original);
        Assert.Equal(image.StorageKey, read.Metadata!.StorageKey); Assert.Equal(Png.Length, read.Metadata.SizeBytes);
        Assert.Equal("Chart\n", note.Document.PlainText); Assert.Equal(2, note.Version);
        await using var db = f.Context(); Assert.Single(await db.NotebookOwnedImageFiles.ToArrayAsync());
        Assert.Empty(Directory.GetFiles(s.Paths.ScreenshotsDirectory, "*.partial"));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailureOrCancellationAfterPrivateStagingRemovesTemporaryFile(bool cancelled)
    {
        await using var f = await NotebookFixture.Create(); var s = Services(f); var note = await f.CreateNote();
        using var cancel = new CancellationTokenSource();
        var files = new NotebookImageFiles(s.Paths, f.Factory(), new Probe(), () =>
        { if (cancelled) cancel.Cancel(); else throw new IOException("Controlled staging failure"); });
        var images = new NotebookImageService(f.Factory(), s.Notes, f.Codec, files);
        var id = Guid.NewGuid(); using var content = new MemoryStream(Png);
        var result = await images.ImportAsync(new(note.Id, note.Version, id, Append(note.Document, id)), content, cancel.Token);
        Assert.Equal(cancelled ? NotebookWriteStatus.Cancelled : NotebookWriteStatus.StorageFailure, result.Status);
        Assert.False(result.CleanupPending); Assert.Empty(Directory.GetFiles(s.Paths.ScreenshotsDirectory));
        Assert.Equal(note.Version, (await s.Notes.LoadAsync(note.Id)).Note!.Version);
    }
    [Theory]
    [InlineData("empty", NotebookWriteStatus.Invalid)]
    [InlineData("unsupported", NotebookWriteStatus.UnsupportedContent)]
    [InlineData("oversize", NotebookWriteStatus.TooLarge)]
    [InlineData("malformed", NotebookWriteStatus.Invalid)]
    public async Task InvalidAdmissionLeavesNoFilesOrMetadata(string mode, NotebookWriteStatus expected)
    {
        await using var f = await NotebookFixture.Create();
        var s = Services(f, mode == "malformed" ? new Probe(new(NotebookImageStatus.Invalid)) : null);
        var note = await f.CreateNote(); var id = Guid.NewGuid();
        using var source = new MemoryStream(mode switch { "empty" => [], "unsupported" => [1, 2, 3], "oversize" => new byte[NotebookLimits.ImageBytes + 1], _ => Png });
        Assert.Equal(expected, (await s.Images.ImportAsync(new(note.Id, note.Version, id, Append(note.Document, id)), source)).Status);
        Assert.Empty(Directory.GetFiles(s.Paths.ScreenshotsDirectory));
        await using var db = f.Context(); Assert.Empty(await db.NotebookOwnedImageFiles.ToArrayAsync());
        Assert.Equal(note.Version, (await s.Notes.LoadAsync(note.Id)).Note!.Version);
    }
    [Theory]
    [InlineData(8193, 1)]
    [InlineData(1, 8193)]
    [InlineData(5000, 4000)]
    public async Task DimensionAndPixelLimitsAreEnforcedBeforeStorage(int width, int height)
    {
        await using var f = await NotebookFixture.Create(); var s = Services(f, new Probe(new(NotebookImageStatus.Success, ".png", width, height)));
        var note = await f.CreateNote(); var id = Guid.NewGuid(); using var input = new MemoryStream(Png);
        Assert.Equal(NotebookWriteStatus.TooLarge, (await s.Images.ImportAsync(new(note.Id, note.Version, id, Append(note.Document, id)), input)).Status);
        Assert.Empty(Directory.GetFiles(s.Paths.ScreenshotsDirectory));
    }
    [Fact]
    public async Task CaptionOrderingRemovalRecoveryAndLifecycleRetainOriginalsAndExactOtherNodes()
    {
        await using var f = await NotebookFixture.Create(); var s = Services(f); var note = await f.CreateNote();
        var text = NotebookNode.Paragraph(Guid.NewGuid(), [NotebookNode.Run(Guid.NewGuid(), "Keep exact text", NotebookTextMarks.Bold)]);
        note = NotebookFixture.Success(await s.Notes.ExecuteAsync(new UpdateNotebookContent(note.Id, note.Version, new([text]), [])));
        note = await Import(s.Images, note); note = await Import(s.Images, note);
        var a = note.Images[0]; var b = note.Images[1]; var nodeA = note.Document.ImageNodes[0]; var nodeB = note.Document.ImageNodes[1];
        var changed = NotebookImage.Owned(a.Id, 1, a.StorageKey!, a.SizeBytes!.Value, a.Sha256!, "Changed caption", "Changed alt");
        var reordered = new NotebookDocument([text, nodeB, NotebookNode.Image(nodeA.Id, a.Id, changed.Caption, changed.AlternativeText)]);
        note = NotebookFixture.Success(await s.Notes.ExecuteAsync(new UpdateNotebookImage(note.Id, note.Version, a.Id, reordered, [NotebookService.At(b, 0), changed])));
        Assert.Equal("Keep exact text\nChart\nChanged caption\n", note.Document.PlainText);
        var loaded = (await f.Service().LoadAsync(note.Id)).Note!; Assert.Equal(f.Codec.Encode(reordered), f.Codec.Encode(loaded.Document));
        var recovery = new NotebookRecoveryStore(f.Factory(), f.Codec);
        Assert.Equal(NotebookWriteStatus.Success, await recovery.PutAsync(new(Guid.NewGuid(), note.Id, note.Version, 1, note.Title, note.Document, f.Time.Now), 0));
        note = NotebookFixture.Success(await s.Notes.ExecuteAsync(new RemoveNotebookImage(note.Id, note.Version, a.Id, new([text, nodeB]), [NotebookService.At(b, 0)])));
        Assert.Equal(Png, (await s.Images.ReadOriginalAsync(note.Id, a.Id)).Original);
        Assert.True(await s.Files.DiscardUncommittedAsync(new(a.StorageKey!, a.SizeBytes!.Value, a.Sha256!), default));
        Assert.True(File.Exists(Path.Combine(s.Paths.ScreenshotsDirectory, a.StorageKey!)));
        // An explicit undo can restore the exact retained ID/key; it never stages a duplicate file.
        note = NotebookFixture.Success(await s.Notes.ExecuteAsync(new AddNotebookImage(note.Id, note.Version, a.Id, reordered, [NotebookService.At(b, 0), changed])));
        note = NotebookFixture.Success(await s.Notes.ExecuteAsync(new ArchiveNotebookNote(note.Id, note.Version)));
        Assert.Equal(Png, (await s.Images.ReadOriginalAsync(note.Id, a.Id)).Original);
        note = NotebookFixture.Success(await s.Notes.ExecuteAsync(new TrashNotebookNote(note.Id, note.Version)));
        Assert.Equal(Png, (await s.Images.ReadOriginalAsync(note.Id, b.Id)).Original);
        Assert.Equal(NotebookWriteStatus.Invalid, (await s.Notes.ExecuteAsync(new RemoveNotebookImage(note.Id, note.Version, a.Id, new([text, nodeB]), [NotebookService.At(b, 0)]))).Status);
        Assert.Equal(2, Directory.GetFiles(s.Paths.ScreenshotsDirectory).Length);
    }
    [Fact]
    public async Task MissingUnreadableAndTamperedReadsNeverModifyCanonicalContent()
    {
        await using var f = await NotebookFixture.Create(); var s = Services(f); var note = await Import(s.Images, await f.CreateNote());
        var image = note.Images[0]; var path = Path.Combine(s.Paths.ScreenshotsDirectory, image.StorageKey!);
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.Equal(NotebookImageStatus.Unreadable, (await s.Images.ReadOriginalAsync(note.Id, image.Id)).Status);
        await File.WriteAllTextAsync(path, "tampered");
        Assert.Equal(NotebookImageStatus.Unreadable, (await s.Images.ReadOriginalAsync(note.Id, image.Id)).Status);
        File.Delete(path); Assert.Equal(NotebookImageStatus.Missing, (await s.Images.ReadOriginalAsync(note.Id, image.Id)).Status);
        var unchanged = (await s.Notes.LoadAsync(note.Id)).Note!;
        Assert.Equal(note.Version, unchanged.Version); Assert.Equal(f.Codec.Encode(note.Document), f.Codec.Encode(unchanged.Document));
    }
    [Fact]
    public async Task DirectAddCannotBypassDecoderAdmissionAndInvalidRequestsAreExplicit()
    {
        await using var f = await NotebookFixture.Create(); var s = Services(f, new UnavailableNotebookImageValidator());
        var note = await f.CreateNote(); var id = Guid.NewGuid(); var key = $"notebook-{note.Id:N}-{id:N}-{Guid.NewGuid():N}.png";
        await File.WriteAllBytesAsync(Path.Combine(s.Paths.ScreenshotsDirectory, key), Png);
        var image = NotebookImage.Owned(id, 0, key, Png.Length, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Png)), "Chart", "Alt");
        var result = await s.Notes.ExecuteAsync(new AddNotebookImage(note.Id, note.Version, id, Append(note.Document, id), [image]));
        Assert.Equal(NotebookWriteStatus.UnsupportedContent, result.Status);
        Assert.Equal(note.Version, (await s.Notes.LoadAsync(note.Id)).Note!.Version);
        await using var db = f.Context(); Assert.Empty(await db.NotebookOwnedImageFiles.ToArrayAsync());
        using var content = new MemoryStream(Png);
        Assert.Equal(NotebookWriteStatus.Invalid, (await s.Images.ImportAsync(null!, content)).Status);
        Assert.Equal(NotebookWriteStatus.Invalid, (await s.Images.ImportAsync(new(note.Id, 0, id, Append(note.Document, id)), content)).Status);
    }
    [Theory]
    [InlineData("cancel-stage", NotebookWriteStatus.Cancelled)]
    [InlineData("stale-stage", NotebookWriteStatus.Conflict)]
    [InlineData("fail-before-commit", NotebookWriteStatus.StorageFailure)]
    [InlineData("cancel-before-commit", NotebookWriteStatus.Cancelled)]
    [InlineData("fail-after-commit", NotebookWriteStatus.Success)]
    [InlineData("cancel-after-commit", NotebookWriteStatus.Success)]
    public async Task ControlledStagingAndCommitBoundariesNeverDeleteCommittedBytes(string boundary, NotebookWriteStatus expected)
    {
        await using var f = await NotebookFixture.Create(); using var cancel = new CancellationTokenSource();
        var hook = new Boundary(boundary, cancel); var s = Services(f, null, hook, new BeforeCommitSave(boundary, cancel)); var note = await f.CreateNote();
        var files = new FileBoundary(s.Files, async () =>
        {
            if (boundary == "cancel-stage") cancel.Cancel();
            if (boundary == "stale-stage") NotebookFixture.Success(await f.Service().ExecuteAsync(new UpdateNotebookTitle(note.Id, note.Version, "External")));
        });
        var service = new NotebookImageService(f.Factory(), s.Notes, f.Codec, files);
        var id = Guid.NewGuid(); using var input = new MemoryStream(Png);
        var result = await service.ImportAsync(new(note.Id, note.Version, id, Append(note.Document, id)), input, cancel.Token);
        Assert.Equal(expected, result.Status); Assert.False(result.CleanupPending);
        var persisted = (await f.Service().LoadAsync(note.Id)).Note!;
        Assert.Equal(expected == NotebookWriteStatus.Success ? 1 : 0, persisted.Images.Length);
        Assert.Equal(expected == NotebookWriteStatus.Success ? 1 : 0, Directory.GetFiles(s.Paths.ScreenshotsDirectory).Length);
        if (expected == NotebookWriteStatus.Success) Assert.Equal(Png, (await s.Images.ReadOriginalAsync(note.Id, id)).Original);
        else Assert.Equal(boundary == "stale-stage" ? "External" : note.Title, persisted.Title);
    }
    private sealed class FileBoundary(INotebookImageFiles inner, Func<Task> after) : INotebookImageFiles
    {
        public async Task<NotebookStoredImage> StageAsync(Guid note, Guid image, Stream content, CancellationToken token)
        { var saved = await inner.StageAsync(note, image, content, token); await after(); return saved; }
        public Task<byte[]> ReadAsync(NotebookStoredImage image, CancellationToken token, bool requireAdmission = false) => inner.ReadAsync(image, token, requireAdmission);
        public Task<bool> DiscardUncommittedAsync(NotebookStoredImage image, CancellationToken token) => inner.DiscardUncommittedAsync(image, token);
    }
    [Fact]
    public async Task UnresolvedCommitRetainsBytesAndReportsUncertaintyRatherThanCompensating()
    {
        await using var f = await NotebookFixture.Create(); var s = Services(f); var note = await f.CreateNote();
        var failure = new ResolutionFailure();
        var notes = new NotebookService(f.Factory(new AmbiguousCommit(() => failure.Enabled = true)), f.Codec, f.Time, s.Files);
        var images = new NotebookImageService(f.Factory(failure), notes, f.Codec, s.Files);
        var id = Guid.NewGuid(); using var input = new MemoryStream(Png);
        var result = await images.ImportAsync(new(note.Id, note.Version, id, Append(note.Document, id)), input);
        Assert.Equal(NotebookWriteStatus.StorageFailure, result.Status); Assert.True(result.CleanupPending);
        Assert.Contains(result.Issues, x => x.Message.Contains("uncertain"));
        Assert.Single(Directory.GetFiles(s.Paths.ScreenshotsDirectory));
        var stored = (await f.Service().LoadAsync(note.Id)).Note!; Assert.Equal(id, Assert.Single(stored.Images).Id);
        Assert.Equal(Png, (await s.Images.ReadOriginalAsync(note.Id, id)).Original);
    }
    [Fact]
    public async Task CleanupFailureIsExplicitAndOnlyUncommittedBytesRemain()
    {
        await using var f = await NotebookFixture.Create(); var s = Services(f); var note = await f.CreateNote();
        using var cancel = new CancellationTokenSource();
        var files = new FailedCleanup(s.Files, cancel);
        var images = new NotebookImageService(f.Factory(), s.Notes, f.Codec, files);
        var id = Guid.NewGuid(); using var input = new MemoryStream(Png);
        var result = await images.ImportAsync(new(note.Id, note.Version, id, Append(note.Document, id)), input, cancel.Token);
        Assert.Equal(NotebookWriteStatus.Cancelled, result.Status); Assert.True(result.CleanupPending);
        Assert.Empty((await s.Notes.LoadAsync(note.Id)).Note!.Images); Assert.Single(Directory.GetFiles(s.Paths.ScreenshotsDirectory));
        await using var db = f.Context(); Assert.Empty(await db.NotebookOwnedImageFiles.ToArrayAsync());
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CheckpointRacingCancelledImportRetainsStagedOriginalAndLedger(bool futureEnvelope)
    {
        await using var f = await NotebookFixture.Create(); var s = Services(f); var note = await f.CreateNote();
        var id = Guid.NewGuid(); var document = Append(note.Document, id); using var cancel = new CancellationTokenSource();
        var files = new FileBoundary(s.Files, async () =>
        {
            await using var db = f.Context();
            db.NotebookRecoveryDrafts.Add(new() { Id = Guid.NewGuid(), NoteId = note.Id, ExpectedVersion = note.Version, Revision = 1,
                EnvelopeVersion = futureEnvelope ? 99 : 1, Title = "Pending image", DocumentJson = futureEnvelope ? "future original" : f.Codec.Encode(document).Json, CheckpointedAtUtc = f.Time.Now });
            await db.SaveChangesAsync(); cancel.Cancel();
        });
        var images = new NotebookImageService(f.Factory(), s.Notes, f.Codec, files); using var input = new MemoryStream(Png);
        var result = await images.ImportAsync(new(note.Id, note.Version, id, document), input, cancel.Token);
        Assert.Equal(NotebookWriteStatus.Cancelled, result.Status); Assert.False(result.CleanupPending);
        Assert.Empty((await s.Notes.LoadAsync(note.Id)).Note!.Images);
        Assert.Equal(Png, (await s.Images.ReadOriginalAsync(note.Id, id)).Original);
        await using var verify = f.Context(); Assert.Single(await verify.NotebookOwnedImageFiles.ToArrayAsync());
        if (futureEnvelope) Assert.Equal("future original", (await verify.NotebookRecoveryDrafts.SingleAsync()).DocumentJson);
    }
    [Theory]
    [InlineData("../outside.png")]
    [InlineData("nested/file.png")]
    [InlineData("C:\\outside.png")]
    public async Task UnsafeReadAndCleanupKeysCannotEscapeManagedRoot(string key)
    {
        await using var f = await NotebookFixture.Create(); var s = Services(f);
        await Assert.ThrowsAsync<ArgumentException>(() => s.Files.ReadAsync(new(key, 1, new string('A', 64)), default));
        Assert.False(await s.Files.DiscardUncommittedAsync(new(key, 1, new string('A', 64)), default));
        Assert.Empty(Directory.GetFiles(s.Paths.ScreenshotsDirectory));
    }
    [Fact]
    public async Task UnsupportedOriginalAndUnrelatedDocumentEditsRemainExactAndRejectImageOperations()
    {
        await using var f = await NotebookFixture.Create(); var s = Services(f); var note = await f.CreateNote();
        var text = NotebookNode.Paragraph(Guid.NewGuid(), [NotebookNode.Run(Guid.NewGuid(), "Original paragraph")]);
        note = NotebookFixture.Success(await s.Notes.ExecuteAsync(new UpdateNotebookContent(note.Id, note.Version, new([text]), [])));
        var id = Guid.NewGuid(); using var input = new MemoryStream(Png);
        var bad = Append(new([NotebookNode.Paragraph(text.Id, [NotebookNode.Run(Guid.NewGuid(), "Replaced text")])]), id);
        Assert.Equal(NotebookWriteStatus.Invalid, (await s.Images.ImportAsync(new(note.Id, note.Version, id, bad), input)).Status);
        const string future = " {\"format\":\"ptj.notebook.document\",\"version\":99} \r\n";
        await using (var db = f.Context()) await db.NotebookNotes.Where(x => x.Id == note.Id).ExecuteUpdateAsync(p => p.SetProperty(x => x.DocumentJson, future));
        using var valid = new MemoryStream(Png);
        Assert.Equal(NotebookWriteStatus.UnsupportedContent, (await s.Images.ImportAsync(new(note.Id, note.Version, id, Append(note.Document, id)), valid)).Status);
        Assert.Equal(future, (await s.Notes.LoadAsync(note.Id)).OriginalContent);
        Assert.Empty(Directory.GetFiles(s.Paths.ScreenshotsDirectory));
    }
    private sealed class FailedCleanup(INotebookImageFiles inner, CancellationTokenSource cancel) : INotebookImageFiles
    {
        public async Task<NotebookStoredImage> StageAsync(Guid note, Guid image, Stream content, CancellationToken token)
        { var result = await inner.StageAsync(note, image, content, token); cancel.Cancel(); return result; }
        public Task<byte[]> ReadAsync(NotebookStoredImage image, CancellationToken token, bool requireAdmission = false) => inner.ReadAsync(image, token, requireAdmission);
        public Task<bool> DiscardUncommittedAsync(NotebookStoredImage image, CancellationToken token) => throw new IOException("Controlled cleanup failure");
    }
    private sealed class AmbiguousCommit(Action failResolution) : DbTransactionInterceptor
    {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        { failResolution(); throw new IOException("Controlled lost commit acknowledgement"); }
    }
    private sealed class ResolutionFailure : DbCommandInterceptor
    {
        internal bool Enabled;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { if (Enabled) throw new IOException("Controlled unavailable resolution"); return ValueTask.FromResult(result); }
    }
    private sealed class Boundary(string mode, CancellationTokenSource cancel) : DbTransactionInterceptor
    {
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (mode == "fail-before-commit") throw new IOException("Controlled precommit failure");
            return ValueTask.FromResult(result);
        }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (mode == "cancel-after-commit") cancel.Cancel();
            if (mode == "fail-after-commit") throw new IOException("Controlled ambiguous commit");
            return Task.CompletedTask;
        }
    }
    private sealed class BeforeCommitSave(string mode, CancellationTokenSource cancel) : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        { if (mode == "cancel-before-commit") cancel.Cancel(); return ValueTask.FromResult(result); }
    }
}
