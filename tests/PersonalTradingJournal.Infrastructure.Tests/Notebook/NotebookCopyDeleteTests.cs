using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Notebook;
using PersonalTradingJournal.Infrastructure.Screenshots;

namespace PersonalTradingJournal.Infrastructure.Tests.Notebook;

public sealed partial class NotebookImageTests
{
    [Fact]
    public async Task CleanupReleasesCaptureLeaseOnOwnerThreadAfterAsynchronousFailure()
    {
        await using var f = await NotebookFixture.Create(); var s = Services(f);
        var hook = new CleanupReadFailure();
        var files = new NotebookImageFiles(s.Paths, f.Factory(hook), new Probe());
        var cleanup = files.CleanupDeletedNoteAsync(Guid.NewGuid(), default);
        await hook.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        // Synchronous continuations on a dedicated, different thread expose thread-affine disposal bugs.
        var release = new Thread(() => hook.Release.SetResult()); release.Start();
        Assert.False(await cleanup.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.True(release.Join(TimeSpan.FromSeconds(10)));
        Exception? failure = null;
        var reader = new Thread(() =>
        {
            try { using var lease = ScreenshotCaptureLease.Acquire(s.Paths.ScreenshotsDirectory, default); }
            catch (Exception ex) { failure = ex; }
        });
        reader.Start(); Assert.True(reader.Join(TimeSpan.FromSeconds(10))); Assert.Null(failure);
    }

    private sealed class CleanupReadFailure : DbCommandInterceptor
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new();
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult(); await Release.Task.ConfigureAwait(false);
            throw new IOException("Asynchronous cleanup read failure");
        }
    }

    [Fact]
    public async Task SecondCopyFailureCompensatesOnlyThisOperationsFirstPublishedCopy()
    {
        await using var f=await NotebookFixture.Create();var s=Services(f);var note=await Import(s.Images,await f.CreateNote());note=await Import(s.Images,note);
        var files=new CopyFiles(s.Files,()=>Task.CompletedTask){FailAt=2};
        var result=await new NotebookService(f.Factory(),f.Codec,f.Time,files).ExecuteAsync(new DuplicateNotebookNote(note.Id,note.Version));
        Assert.Equal(NotebookWriteStatus.StorageFailure,result.Status);Assert.False(result.CleanupPending);Assert.Equal(2,Directory.GetFiles(s.Paths.ScreenshotsDirectory).Length);
        foreach(var image in note.Images)Assert.Equal(Png,(await s.Images.ReadOriginalAsync(note.Id,image.Id)).Original);
        await using var fresh=f.Context();Assert.Single(await fresh.NotebookNotes.ToArrayAsync());Assert.Equal(2,await fresh.NotebookOwnedImageFiles.CountAsync());
    }
    [Fact]
    public async Task CheckpointWaitingBehindDeletionCannotResurrectRecovery()
    {
        await using var f=await NotebookFixture.Create();var n=await f.CreateNote();n=NotebookFixture.Success(await f.Service().ExecuteAsync(new TrashNotebookNote(n.Id,n.Version)));
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hook=new CopyCommit(async after=>{if(!after){entered.TrySetResult();await release.Task.WaitAsync(TimeSpan.FromSeconds(10));}});
        var deletion=Task.Run(()=>f.Service(hook).ExecuteAsync(new DeleteNotebookNote(n.Id,n.Version,true)));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var checkpoint=Task.Run(async()=>{started.TrySetResult();return await new NotebookRecoveryStore(f.Factory(),f.Codec).PutAsync(new(Guid.NewGuid(),n.Id,n.Version,1,"Late",n.Document,f.Time.Now),0);});
        await started.Task;release.SetResult();Assert.Equal(NotebookWriteStatus.Success,(await deletion).Status);Assert.Equal(NotebookWriteStatus.NotFound,await checkpoint);
        await using var fresh=f.Context();Assert.Empty(await fresh.NotebookRecoveryDrafts.ToArrayAsync());Assert.Empty(await fresh.NotebookNotes.ToArrayAsync());
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DuplicateCopiesOwnedOriginalsAndRelationshipsIndependently(bool archived)
    {
        await using var f=await NotebookFixture.Create();var s=Services(f);var folder=await f.Folder();var tag=await f.Tag();
        var source=await Import(s.Images,await f.CreateNote("Research",folder.Id));
        source=NotebookFixture.Success(await s.Notes.ExecuteAsync(new AssignNotebookTag(source.Id,source.Version,tag.Id)));
        source=NotebookFixture.Success(await s.Notes.ExecuteAsync(new SetNotebookPin(source.Id,source.Version,true)));
        var target=Guid.NewGuid();await using(var db=f.Context()){db.NotebookLinks.Add(new(){NoteId=source.Id,Kind=2,TargetId=target,LastKnownLabel="Unavailable setup"});await db.SaveChangesAsync();}
        if(archived) source=NotebookFixture.Success(await s.Notes.ExecuteAsync(new ArchiveNotebookNote(source.Id,source.Version)));
        source=(await s.Notes.LoadAsync(source.Id)).Note!;f.Time.Now=f.Time.Now.AddMinutes(1);
        var copy=NotebookFixture.Success(await Services(f).Notes.ExecuteAsync(new DuplicateNotebookNote(source.Id,source.Version)));
        Assert.NotEqual(source.Id,copy.Id);Assert.Equal(1,copy.Version);Assert.Equal(f.Time.Now,copy.CreatedAtUtc);Assert.Equal(copy.CreatedAtUtc,copy.UpdatedAtUtc);
        Assert.Equal("Copy of Research",copy.Title);Assert.False(copy.IsPinned);Assert.Equal(NotebookNoteState.Active,copy.State);Assert.Equal(folder.Id,copy.FolderId);
        Assert.Equal(source.TagIds.ToArray(),copy.TagIds.ToArray());Assert.Equal(source.Links.ToArray(),copy.Links.ToArray());Assert.Equal(source.Document.PlainText,copy.Document.PlainText);
        var before=Assert.Single(source.Images);var after=Assert.Single(copy.Images);Assert.NotEqual(before.Id,after.Id);Assert.NotEqual(before.StorageKey,after.StorageKey);
        Assert.NotEqual(source.Document.ImageNodes[0].Id,copy.Document.ImageNodes[0].Id);Assert.Equal(before.Caption,after.Caption);Assert.Equal(before.AlternativeText,after.AlternativeText);
        Assert.Equal(Png,(await Services(f).Images.ReadOriginalAsync(copy.Id,after.Id)).Original);
        source=NotebookFixture.Success(await s.Notes.ExecuteAsync(new TrashNotebookNote(source.Id,source.Version)));
        var deleted=await s.Notes.ExecuteAsync(new DeleteNotebookNote(source.Id,source.Version,true));Assert.Equal(NotebookWriteStatus.Success,deleted.Status);Assert.False(deleted.CleanupPending);
        Assert.Equal(Png,(await Services(f).Images.ReadOriginalAsync(copy.Id,after.Id)).Original);Assert.False(File.Exists(Path.Combine(s.Paths.ScreenshotsDirectory,before.StorageKey!)));
        await using var fresh=f.Context();Assert.Single(await fresh.NotebookNotes.ToArrayAsync());Assert.Single(await fresh.NotebookTags.ToArrayAsync());Assert.Single(await fresh.NotebookLinks.ToArrayAsync());Assert.Single(await fresh.NotebookOwnedImageFiles.ToArrayAsync());
    }
    [Theory]
    [InlineData("conflict",NotebookWriteStatus.Conflict)] [InlineData("cancel",NotebookWriteStatus.Cancelled)] [InlineData("failure",NotebookWriteStatus.StorageFailure)]
    public async Task DuplicatePreparationFailureNeverPublishesPartialCopy(string mode,NotebookWriteStatus expected)
    {
        await using var f=await NotebookFixture.Create();var s=Services(f);var source=await Import(s.Images,await f.CreateNote());using var cancel=new CancellationTokenSource();
        var files=new CopyFiles(s.Files,async()=>
        { if(mode=="conflict") NotebookFixture.Success(await s.Notes.ExecuteAsync(new UpdateNotebookTitle(source.Id,source.Version,"Newer")));else if(mode=="cancel")cancel.Cancel();else throw new IOException("copy fault"); });
        var service=new NotebookService(f.Factory(),f.Codec,f.Time,files);
        var result=await service.ExecuteAsync(new DuplicateNotebookNote(source.Id,source.Version),cancel.Token);Assert.Equal(expected,result.Status);Assert.False(result.CleanupPending);
        await using var fresh=f.Context();Assert.Single(await fresh.NotebookNotes.ToArrayAsync());Assert.Single(Directory.GetFiles(s.Paths.ScreenshotsDirectory));Assert.Single(await fresh.NotebookOwnedImageFiles.ToArrayAsync());
        Assert.Equal(mode=="conflict"?"Newer":source.Title,(await s.Notes.LoadAsync(source.Id)).Note!.Title);
    }
    [Fact]
    public async Task DuplicateCompensationFailureRetainsDurablePreparedOwnership()
    {
        await using var f=await NotebookFixture.Create();var s=Services(f);var source=await Import(s.Images,await f.CreateNote());using var cancel=new CancellationTokenSource();
        var files=new CopyFiles(s.Files,()=>{cancel.Cancel();return Task.CompletedTask;}){FailDiscard=true};
        var result=await new NotebookService(f.Factory(),f.Codec,f.Time,files).ExecuteAsync(new DuplicateNotebookNote(source.Id,source.Version),cancel.Token);
        Assert.Equal(NotebookWriteStatus.Cancelled,result.Status);Assert.True(result.CleanupPending);
        await using var db=f.Context();Assert.Single(await db.NotebookNotes.ToArrayAsync());Assert.Equal(2,await db.NotebookOwnedImageFiles.CountAsync());Assert.Equal(2,Directory.GetFiles(s.Paths.ScreenshotsDirectory).Length);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DuplicateCommitBoundaryUsesFreshReaderAndResolvesLostAcknowledgement(bool failBefore)
    {
        await using var f=await NotebookFixture.Create();var s=Services(f);var source=await Import(s.Images,await f.CreateNote());
        var hook=new CopyCommit(async after=>
        {
            await using var fresh=f.Context();Assert.Equal(after?2:1,await fresh.NotebookNotes.CountAsync());
            if(after!=failBefore)throw new IOException("Interrupted acknowledgement");
        });
        var result=await new NotebookService(f.Factory(hook),f.Codec,f.Time,s.Files).ExecuteAsync(new DuplicateNotebookNote(source.Id,source.Version));
        Assert.Equal(failBefore?NotebookWriteStatus.StorageFailure:NotebookWriteStatus.Success,result.Status);
        await using var db=f.Context();Assert.Equal(failBefore?1:2,await db.NotebookNotes.CountAsync());Assert.Equal(failBefore?1:2,Directory.GetFiles(s.Paths.ScreenshotsDirectory).Length);
    }
    [Theory]
    [InlineData("{bad")][InlineData("{\"format\":\"ptj.notebook.document\",\"version\":99,\"blocks\":[]}")]
    public async Task UnknownBodyCannotDuplicateButConfirmedTrashDeletionDoesNotConvertIt(string json)
    {
        await using var f=await NotebookFixture.Create();var n=await f.CreateNote();await using(var db=f.Context())await db.NotebookNotes.Where(x=>x.Id==n.Id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.DocumentJson,json));
        Assert.Equal(NotebookWriteStatus.UnsupportedContent,(await f.Service().ExecuteAsync(new DuplicateNotebookNote(n.Id,n.Version))).Status);
        Assert.Equal(json,(await f.Service().LoadAsync(n.Id)).OriginalContent);
        var trash=await f.Service().ExecuteAsync(new TrashNotebookNote(n.Id,n.Version));var version=trash.UnavailableMetadata!.Version;
        Assert.Equal(NotebookWriteStatus.Invalid,(await f.Service().ExecuteAsync(new DeleteNotebookNote(n.Id,version,false))).Status);
        Assert.Equal(json,(await f.Service().LoadAsync(n.Id)).OriginalContent);
        Assert.Equal(NotebookWriteStatus.Success,(await f.Service().ExecuteAsync(new DeleteNotebookNote(n.Id,version,true))).Status);
        Assert.Equal(NotebookLoadStatus.NotFound,(await f.Service().LoadAsync(n.Id)).Status);
    }
    [Fact]
    public async Task DeleteChecksStateVersionAndPreventsLateSavesAndCheckpoints()
    {
        await using var f=await NotebookFixture.Create();var n=await f.CreateNote();var other=await f.CreateNote("Other");var recovery=new NotebookRecoveryStore(f.Factory(),f.Codec);
        var draft=new NotebookRecoveryDraft(Guid.NewGuid(),n.Id,n.Version,1,"Unsaved",NotebookDocument.Empty,f.Time.Now);
        var otherDraft=draft with{Id=Guid.NewGuid(),NoteId=other.Id};Assert.Equal(NotebookWriteStatus.Success,await recovery.PutAsync(draft,0));Assert.Equal(NotebookWriteStatus.Success,await recovery.PutAsync(otherDraft,0));
        Assert.Equal(NotebookWriteStatus.Invalid,(await f.Service().ExecuteAsync(new DeleteNotebookNote(n.Id,n.Version,true))).Status);
        n=NotebookFixture.Success(await f.Service().ExecuteAsync(new TrashNotebookNote(n.Id,n.Version)));
        Assert.Equal(NotebookWriteStatus.Invalid,(await f.Service().ExecuteAsync(new DuplicateNotebookNote(n.Id,n.Version))).Status);
        Assert.Equal(NotebookWriteStatus.Conflict,(await f.Service().ExecuteAsync(new DeleteNotebookNote(n.Id,n.Version-1,true))).Status);
        Assert.Equal(NotebookWriteStatus.Success,(await f.Service().ExecuteAsync(new DeleteNotebookNote(n.Id,n.Version,true))).Status);
        Assert.Equal(NotebookWriteStatus.NotFound,await new NotebookRecoveryStore(f.Factory(),f.Codec).PutAsync(draft with{Revision=2},1));
        Assert.Equal(NotebookWriteStatus.NotFound,await recovery.PutAsync(draft with{Id=Guid.NewGuid()},0));
        Assert.Equal(NotebookWriteStatus.NotFound,(await f.Service().ExecuteAsync(new SaveNotebookDraft(n.Id,n.Version,"Late",n.Document,n.Images))).Status);
        await using var db=f.Context();Assert.Equal(other.Id,Assert.Single(await db.NotebookNotes.ToArrayAsync()).Id);Assert.Equal(otherDraft.Id,Assert.Single(await db.NotebookRecoveryDrafts.ToArrayAsync()).Id);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DeleteCommitBoundaryPreservesOrRemovesRecoveryAtomically(bool afterCommit)
    {
        await using var f=await NotebookFixture.Create();var n=await f.CreateNote();n=NotebookFixture.Success(await f.Service().ExecuteAsync(new TrashNotebookNote(n.Id,n.Version)));
        var draft=new NotebookRecoveryDraft(Guid.NewGuid(),n.Id,n.Version,1,"Retained",NotebookDocument.Empty,f.Time.Now);Assert.Equal(NotebookWriteStatus.Success,await new NotebookRecoveryStore(f.Factory(),f.Codec).PutAsync(draft,0));
        var hook=new CopyCommit(async after=>{await using var fresh=f.Context();Assert.Equal(after?0:1,await fresh.NotebookNotes.CountAsync());Assert.Equal(after?0:1,await fresh.NotebookRecoveryDrafts.CountAsync());if(after==afterCommit)throw new IOException("Interrupt");});
        var result=await f.Service(hook).ExecuteAsync(new DeleteNotebookNote(n.Id,n.Version,true));Assert.Equal(afterCommit?NotebookWriteStatus.Success:NotebookWriteStatus.StorageFailure,result.Status);
        Assert.Equal(afterCommit?NotebookLoadStatus.NotFound:NotebookLoadStatus.Success,(await f.Service().LoadAsync(n.Id)).Status);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CleanupFailureOrUnknownRetentionDoesNotUndoCommittedDeletion(bool unknownRecovery)
    {
        await using var f=await NotebookFixture.Create();var s=Services(f);var n=await Import(s.Images,await f.CreateNote());var image=n.Images[0];
        if(unknownRecovery){var other=await f.CreateNote("Other");await using var db=f.Context();db.NotebookRecoveryDrafts.Add(new(){Id=Guid.NewGuid(),NoteId=other.Id,EnvelopeVersion=99,ExpectedVersion=1,Revision=1,Title="Future",DocumentJson="{unknown",CheckpointedAtUtc=f.Time.Now});await db.SaveChangesAsync();}
        n=NotebookFixture.Success(await s.Notes.ExecuteAsync(new TrashNotebookNote(n.Id,n.Version)));
        // Tampered bytes fail verified cleanup after durable intent; unknown recovery prevents intent entirely.
        if(!unknownRecovery)await File.WriteAllBytesAsync(Path.Combine(s.Paths.ScreenshotsDirectory,image.StorageKey!),new byte[Png.Length]);
        var result=await s.Notes.ExecuteAsync(new DeleteNotebookNote(n.Id,n.Version,true));Assert.Equal(NotebookWriteStatus.Success,result.Status);Assert.True(result.CleanupPending);Assert.Equal(n.Id,result.DeletedNoteId);
        await using var fresh=f.Context();var ledger=Assert.Single(await fresh.NotebookOwnedImageFiles.ToArrayAsync());Assert.Equal(!unknownRecovery,ledger.CleanupPending);Assert.True(File.Exists(Path.Combine(s.Paths.ScreenshotsDirectory,image.StorageKey!)));
        if(!unknownRecovery){await File.WriteAllBytesAsync(Path.Combine(s.Paths.ScreenshotsDirectory,image.StorageKey!),Png);Assert.True(await s.Files.CleanupDeletedNoteAsync(n.Id,default));Assert.False(File.Exists(Path.Combine(s.Paths.ScreenshotsDirectory,image.StorageKey!)));}
    }
    private sealed class CopyFiles(INotebookImageFiles inner,Func<Task> after):INotebookImageFiles
    {
        public bool FailDiscard; public int FailAt; private int _stages;
        public async Task<NotebookStoredImage> StageAsync(Guid n,Guid i,Stream stream,CancellationToken ct)
        {
            if(++_stages==FailAt)throw new IOException("Second copy failed before publication");
            // Failure before publication owns no final path. Other modes run after a successful publication.
            NotebookStoredImage file=await inner.StageAsync(n,i,stream,ct);
            try{await after();}catch{await inner.DiscardUncommittedAsync(file,default);throw;}return file;
        }
        public Task<byte[]> ReadAsync(NotebookStoredImage file,CancellationToken ct,bool requireAdmission=false)=>inner.ReadAsync(file,ct,requireAdmission);
        public Task<bool> DiscardUncommittedAsync(NotebookStoredImage file,CancellationToken ct)=>FailDiscard?Task.FromResult(false):inner.DiscardUncommittedAsync(file,ct);
        public Task<bool> CleanupDeletedNoteAsync(Guid note,CancellationToken ct)=>inner.CleanupDeletedNoteAsync(note,ct);
    }
    private sealed class CopyCommit(Func<bool,Task> action):DbTransactionInterceptor
    {
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction t,TransactionEventData e,InterceptionResult r,CancellationToken ct=default){await action(false);return r;}
        public override Task TransactionCommittedAsync(DbTransaction t,TransactionEndEventData e,CancellationToken ct=default)=>action(true);
    }
}
