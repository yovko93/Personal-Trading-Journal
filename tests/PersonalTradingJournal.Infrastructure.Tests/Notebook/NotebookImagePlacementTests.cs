using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Notebook;

namespace PersonalTradingJournal.Infrastructure.Tests.Notebook;

public sealed partial class NotebookImageTests
{
    private static NotebookNode Text(string value) => NotebookNode.Paragraph(Guid.NewGuid(), [NotebookNode.Run(Guid.NewGuid(), value, NotebookTextMarks.Bold)]);

    [Theory]
    [InlineData("before-commit", NotebookWriteStatus.StorageFailure)]
    [InlineData("after-commit", NotebookWriteStatus.Success)]
    [InlineData("cancel-stage", NotebookWriteStatus.Cancelled)]
    [InlineData("fail-stage", NotebookWriteStatus.StorageFailure)]
    [InlineData("cancel-private", NotebookWriteStatus.Cancelled)]
    [InlineData("stale-stage", NotebookWriteStatus.Conflict)]
    [InlineData("cancel-before-commit", NotebookWriteStatus.Cancelled)]
    public async Task FinalMiddlePlacementIsAtomicAcrossInterruptionBoundaries(string boundary, NotebookWriteStatus expected)
    {
        await using var f = await NotebookFixture.Create(); var s = Services(f);
        var note = await f.CreateNote(); var original = new NotebookDocument([Text("Before selected After")]);
        note = NotebookFixture.Success(await s.Notes.ExecuteAsync(new UpdateNotebookContent(note.Id, note.Version, original, [])));
        var id = Guid.NewGuid(); var proposed = new NotebookDocument([Text("Before "), NotebookNode.Image(Guid.NewGuid(), id, "Chart", "Alt"), Text(" After")]);
        using var cancel = new CancellationTokenSource(); int observed = 0;
        async Task VerifyFresh(bool committed)
        {
            var fresh = Services(f); var read = (await fresh.Notes.LoadAsync(note.Id)).Note!;
            Assert.Equal(f.Codec.Encode(committed ? proposed : original).Json, f.Codec.Encode(read.Document).Json);
            Assert.Equal(note.Version + (committed ? 1 : 0), read.Version);
            await using var db = f.Context();
            if (committed)
            {
                var image = Assert.Single(read.Images); var ledger = await db.NotebookOwnedImageFiles.SingleAsync();
                Assert.Equal(id, image.Id); Assert.Equal(0, image.Position); Assert.Equal("Chart", image.Caption); Assert.Equal("Alt", image.AlternativeText);
                Assert.Equal(note.Id, ledger.NoteId); Assert.Equal(id, ledger.Id); Assert.Equal(image.StorageKey, ledger.StorageKey);
                Assert.Equal(image.Sha256, ledger.Sha256); Assert.Equal(image.SizeBytes, ledger.SizeBytes);
                Assert.Equal(Png, (await fresh.Images.ReadOriginalAsync(note.Id, id)).Original);
                Assert.Equal("Before \nChart\n After\n", read.Document.PlainText);
            }
            else { Assert.Empty(read.Images); Assert.Empty(await db.NotebookOwnedImageFiles.ToArrayAsync()); }
            observed++;
        }
        var hook = new PlacementCommitBoundary(async committed =>
        {
            if (!committed) { await VerifyFresh(false); if (boundary == "before-commit") throw new IOException("Interrupted before canonical commit"); }
            if (committed && boundary == "after-commit") { await VerifyFresh(true); throw new IOException("Lost client acknowledgement"); }
        });
        var writer = Services(f, null, hook, new BeforeCommitSave(boundary, cancel));
        var files = boundary is "fail-stage" or "cancel-private"
            ? new NotebookImageFiles(s.Paths, f.Factory(), new Probe(), () => { if (boundary == "cancel-private") cancel.Cancel(); else throw new IOException("Preparation failed"); })
            : s.Files;
        var preparation = new FileBoundary(files, async () =>
        {
            await VerifyFresh(false);
            if (boundary == "cancel-stage") cancel.Cancel();
            if (boundary == "stale-stage")
                NotebookFixture.Success(await Services(f).Notes.ExecuteAsync(new SaveNotebookDraft(note.Id, note.Version, "Newer", new([Text("Newer content")]), [])));
        });
        var service = new NotebookImageService(f.Factory(), writer.Notes, f.Codec, preparation);
        using var content = new MemoryStream(Png);
        var result = await service.ImportAsync(new(note.Id, note.Version, id, proposed, EditorPlacement: true), content, cancel.Token);
        Assert.Equal(expected, result.Status); Assert.False(result.CleanupPending);
        var persisted = (await Services(f).Notes.LoadAsync(note.Id)).Note!;
        if (boundary == "stale-stage")
        {
            Assert.Equal("Newer", persisted.Title); Assert.Equal("Newer content\n", persisted.Document.PlainText);
            Assert.Equal(note.Version + 1, persisted.Version); Assert.Empty(persisted.Images);
            await using var db = f.Context(); Assert.Empty(await db.NotebookOwnedImageFiles.ToArrayAsync());
        }
        else await VerifyFresh(expected == NotebookWriteStatus.Success);
        Assert.True(observed > 0);
        Assert.Equal(expected == NotebookWriteStatus.Success ? 1 : 0, Directory.GetFiles(s.Paths.ScreenshotsDirectory).Length);
    }

    [Fact]
    public async Task ConsecutiveMiddlePlacementsUndoRedoAndFreshRecoveryResolveExactRetainedOriginals()
    {
        await using var f = await NotebookFixture.Create(); var s = Services(f); var note = await f.CreateNote();
        var left = Text("Left"); var right = Text("Right");
        note = NotebookFixture.Success(await s.Notes.ExecuteAsync(new UpdateNotebookContent(note.Id, note.Version, new([left, right]), [])));
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var first = NotebookNode.Image(Guid.NewGuid(), a, "First", "A"); var second = NotebookNode.Image(Guid.NewGuid(), b, "Second", "B");
        var once = new NotebookDocument([left, first, right]); var twice = new NotebookDocument([left, first, second, right]);
        foreach (var (id, document) in new[] { (a, once), (b, twice) })
        {
            using var content = new MemoryStream(Png); long previous = note.Version;
            note = NotebookFixture.Success(await Services(f).Images.ImportAsync(new(note.Id, previous, id, document, true), content));
            Assert.Equal(previous + 1, note.Version); Assert.Equal(f.Codec.Encode(document).Json, f.Codec.Encode((await Services(f).Notes.LoadAsync(note.Id)).Note!.Document).Json);
        }
        Assert.Equal(new[] { a, b }, note.Images.Select(x => x.Id)); Assert.Equal(new[] { 0, 1 }, note.Images.Select(x => x.Position));
        Assert.Equal("Left\nFirst\nSecond\nRight\n", note.Document.PlainText);
        var both = note.Images;
        note = NotebookFixture.Success(await Services(f).Notes.ExecuteAsync(new SaveNotebookImageDraft(note.Id, note.Version, note.Title, once, [both[0]])));
        Assert.Equal(Png, (await Services(f).Images.ReadOriginalAsync(note.Id, b)).Original);
        var recoveryId = Guid.NewGuid();
        Assert.Equal(NotebookWriteStatus.Success, await new NotebookRecoveryStore(f.Factory(), f.Codec).PutAsync(new(recoveryId, note.Id, note.Version, 1, note.Title, twice, f.Time.Now), 0));
        var recovered = (await new NotebookRecoveryStore(f.Factory(), f.Codec).LoadAsync(recoveryId)).Draft!;
        Assert.Equal(f.Codec.Encode(twice).Json, f.Codec.Encode(recovered.Document).Json);
        note = NotebookFixture.Success(await Services(f).Notes.ExecuteAsync(new SaveNotebookImageDraft(note.Id, recovered.ExpectedVersion, recovered.Title, recovered.Document, both)));
        Assert.Equal(f.Codec.Encode(twice).Json, f.Codec.Encode(note.Document).Json);
        Assert.Equal<NotebookImage>(both, note.Images);
        foreach (var id in new[] { a, b }) Assert.Equal(Png, (await Services(f).Images.ReadOriginalAsync(note.Id, id)).Original);
        await using var db = f.Context(); Assert.Equal(2, await db.NotebookOwnedImageFiles.CountAsync());
        Assert.Equal(2, Directory.GetFiles(s.Paths.ScreenshotsDirectory).Length);
    }

    private sealed class PlacementCommitBoundary(Func<bool, Task> observe) : DbTransactionInterceptor
    {
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default)
        { await observe(false); return result; }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) => observe(true);
    }
}
