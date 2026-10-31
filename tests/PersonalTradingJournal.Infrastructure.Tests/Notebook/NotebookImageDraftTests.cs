using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Notebook;
using PersonalTradingJournal.Infrastructure.Storage;

namespace PersonalTradingJournal.Infrastructure.Tests.Notebook;

public sealed class NotebookImageDraftTests
{
    [Fact]
    public async Task WholeEditorSaveReordersCaptionsRemovesAndRestoresRetainedReferencesWithExactText()
    {
        await using var f = await NotebookFixture.Create(); var paths = new LocalApplicationPaths(f.Root);
        var files = new NotebookImageFiles(paths, f.Factory(), new NotebookImageTests.Probe());
        var service = new NotebookService(f.Factory(), f.Codec, f.Time, files);
        var images = new NotebookImageService(f.Factory(), service, f.Codec, files);
        var note = await f.CreateNote();
        for (int i = 0; i < 2; i++)
        {
            var id = Guid.NewGuid(); using var input = new MemoryStream(NotebookImageTests.Png);
            note = NotebookFixture.Success(await images.ImportAsync(new(note.Id, note.Version, id, NotebookImageTests.Append(note.Document, id)), input));
        }
        var original = note;
        var second = note.Document.ImageNodes[1]; var first = note.Document.ImageNodes[0];
        var changed = NotebookNode.Image(second.Id, second.ImageId!.Value, "Changed caption", "Changed alt");
        var text = NotebookNode.Paragraph(Guid.NewGuid(), [NotebookNode.Run(Guid.NewGuid(), "Surrounding bold", NotebookTextMarks.Bold)]);
        var doc = new NotebookDocument([changed, text, first]);
        var a = note.Images[1]; var b = note.Images[0];
        ImmutableArray<NotebookImage> refs = [NotebookImage.Owned(a.Id, 0, a.StorageKey!, a.SizeBytes!.Value, a.Sha256!, changed.Caption, changed.AlternativeText),
            NotebookImage.Owned(b.Id, 1, b.StorageKey!, b.SizeBytes!.Value, b.Sha256!, first.Caption, first.AlternativeText)];
        note = NotebookFixture.Success(await service.ExecuteAsync(new SaveNotebookImageDraft(note.Id, note.Version, "Editor title", doc, refs)));
        Assert.Equal("Changed caption\nSurrounding bold\nChart\n", note.Document.PlainText);
        Assert.Equal(f.Codec.Encode(doc).Json, f.Codec.Encode(note.Document).Json);
        var removed = new NotebookDocument([text]);
        note = NotebookFixture.Success(await service.ExecuteAsync(new SaveNotebookImageDraft(note.Id, note.Version, note.Title, removed, [])));
        Assert.Empty(note.Images);
        foreach (var image in original.Images) Assert.Equal(NotebookImageTests.Png, (await images.ReadOriginalAsync(note.Id, image.Id)).Original);
        // Undo can restore references even when original bytes went missing: no lossy body rewrite.
        File.Delete(Path.Combine(paths.ScreenshotsDirectory, b.StorageKey!));
        var missing = await images.ReadOriginalAsync(note.Id, b.Id);
        Assert.Equal(NotebookImageStatus.Missing, missing.Status); Assert.Equal(b.StorageKey, missing.Metadata!.StorageKey);
        note = NotebookFixture.Success(await service.ExecuteAsync(new SaveNotebookImageDraft(note.Id, note.Version, note.Title, doc, refs)));
        Assert.Equal(f.Codec.Encode(doc).Json, f.Codec.Encode(note.Document).Json);
        Assert.Equal<NotebookImage>(refs, note.Images); Assert.Equal(original.Version + 3, note.Version);
        await using var db = f.Context(); Assert.Equal(2, await db.NotebookOwnedImageFiles.CountAsync());
    }
    [Theory]
    [InlineData("unknown")]
    [InlineData("different-note")]
    [InlineData("file-identity")]
    [InlineData("node-identity")]
    [InlineData("stale")]
    [InlineData("archived")]
    public async Task EditorReconciliationCannotImportOrRebindFilesOrBypassVersionAndLifecycle(string mode)
    {
        await using var f = await NotebookFixture.Create(); var paths = new LocalApplicationPaths(f.Root);
        var files = new NotebookImageFiles(paths, f.Factory(), new NotebookImageTests.Probe());
        var service = new NotebookService(f.Factory(), f.Codec, f.Time, files);
        var images = new NotebookImageService(f.Factory(), service, f.Codec, files);
        var note = await f.CreateNote(); var id = Guid.NewGuid(); using var input = new MemoryStream(NotebookImageTests.Png);
        note = NotebookFixture.Success(await images.ImportAsync(new(note.Id, note.Version, id, NotebookImageTests.Append(note.Document, id)), input));
        var old = note.Images[0]; var node = note.Document.ImageNodes[0]; var original = note;
        if (mode == "different-note") note = await f.CreateNote("Other note");
        if (mode == "archived") note = NotebookFixture.Success(await service.ExecuteAsync(new ArchiveNotebookNote(note.Id, note.Version)));
        if (mode == "unknown") { id = Guid.NewGuid(); node = NotebookNode.Image(Guid.NewGuid(), id, old.Caption, old.AlternativeText); }
        if (mode == "node-identity") node = NotebookNode.Image(Guid.NewGuid(), id, old.Caption, old.AlternativeText);
        var reference = NotebookImage.Owned(id, 0, mode == "file-identity" ? "injected.png" : old.StorageKey!, old.SizeBytes!.Value, old.Sha256!, old.Caption, old.AlternativeText);
        var result = await service.ExecuteAsync(new SaveNotebookImageDraft(note.Id, mode == "stale" ? note.Version - 1 : note.Version, note.Title, new([node]), [reference]));
        Assert.Equal(mode == "stale" ? NotebookWriteStatus.Conflict : NotebookWriteStatus.Invalid, result.Status);
        var read = (await service.LoadAsync(original.Id)).Note!;
        Assert.Equal(f.Codec.Encode(original.Document).Json, f.Codec.Encode(read.Document).Json);
        Assert.Equal<NotebookImage>(original.Images, read.Images);
        Assert.Equal(NotebookImageTests.Png, (await images.ReadOriginalAsync(original.Id, old.Id)).Original);
    }
}
