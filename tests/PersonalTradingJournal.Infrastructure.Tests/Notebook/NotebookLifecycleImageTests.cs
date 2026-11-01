using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Notebook;

namespace PersonalTradingJournal.Infrastructure.Tests.Notebook;

public sealed partial class NotebookImageTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public async Task ReversibleLifecycleRetainsOriginalsAssignmentsAndPriorState(bool archived, bool deleteFolder)
    {
        await using var f = await NotebookFixture.Create(); var services = Services(f); var folder = await f.Folder(); var tag = await f.Tag();
        var note = await f.CreateNote(folder:folder.Id); note = await Import(services.Images,note);
        note = NotebookFixture.Success(await services.Notes.ExecuteAsync(new AssignNotebookTag(note.Id,note.Version,tag.Id)));
        note = NotebookFixture.Success(await services.Notes.ExecuteAsync(new SetNotebookPin(note.Id,note.Version,true)));
        var target = Guid.NewGuid();
        await using(var db=f.Context()) { db.NotebookLinks.Add(new(){NoteId=note.Id,Kind=(int)NotebookTargetKind.TradingSetup,TargetId=target,LastKnownLabel="Retained unavailable setup"}); await db.SaveChangesAsync(); }
        note=(await Services(f).Notes.LoadAsync(note.Id)).Note!;
        var content=f.Codec.Encode(note.Document).Json; var image=Assert.Single(note.Images);
        if(archived) note=NotebookFixture.Success(await Services(f).Notes.ExecuteAsync(new ArchiveNotebookNote(note.Id,note.Version)));
        var prior=note.State;
        note=NotebookFixture.Success(await Services(f).Notes.ExecuteAsync(new TrashNotebookNote(note.Id,note.Version)));
        Assert.Equal(prior,note.RestorePoint!.PreviousState); Assert.Equal(folder.Id,note.RestorePoint.FolderId);
        Assert.Equal(NotebookWriteStatus.Invalid,(await Services(f).Notes.ExecuteAsync(new SaveNotebookDraft(note.Id,note.Version,"Forbidden",note.Document,note.Images))).Status);
        if(deleteFolder)
        {
            Assert.Equal(NotebookWriteStatus.Success,(await Services(f).Notes.ExecuteCatalogAsync(new DeleteNotebookFolder(folder.Id,folder.Version,true))).Status);
            Assert.Equal(NotebookWriteStatus.Conflict,(await Services(f).Notes.ExecuteAsync(new RestoreNotebookNote(note.Id,note.Version))).Status);
            note=(await Services(f).Notes.LoadAsync(note.Id)).Note!;
        }
        note=NotebookFixture.Success(await Services(f).Notes.ExecuteAsync(new RestoreNotebookNote(note.Id,note.Version)));
        Assert.Equal(prior,note.State); Assert.Equal(deleteFolder?null:folder.Id,note.FolderId); Assert.Null(note.RestorePoint); Assert.True(note.IsPinned);
        Assert.Equal(content,f.Codec.Encode(note.Document).Json); Assert.Equal(tag.Id,Assert.Single(note.TagIds)); Assert.Equal(target,Assert.Single(note.Links).TargetId);
        Assert.Equal(image,Assert.Single(note.Images)); Assert.Equal(Png,(await Services(f).Images.ReadOriginalAsync(note.Id,image.Id)).Original);
        await using var fresh=f.Context(); Assert.Single(await fresh.NotebookOwnedImageFiles.ToArrayAsync()); Assert.Single(Directory.GetFiles(services.Paths.ScreenshotsDirectory));
    }
}
