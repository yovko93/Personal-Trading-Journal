using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Notebook;

namespace PersonalTradingJournal.Infrastructure.Tests.Notebook;

public sealed class NotebookTemplateLifecycleTests
{
    [Theory]
    [InlineData("success", NotebookWriteStatus.Success)]
    [InlineData("fail", NotebookWriteStatus.StorageFailure)]
    [InlineData("cancel", NotebookWriteStatus.Cancelled)]
    [InlineData("ack-cancel", NotebookWriteStatus.Success)]
    public async Task TemplateCreationPublishesOnlyFinalDocumentAtOneCommit(string boundary, NotebookWriteStatus expected)
    {
        await using var f = await NotebookFixture.Create(); using var cancel = new CancellationTokenSource(); int commits = 0;
        var hook = new CommitBoundary(async after =>
        {
            await using var fresh = f.Context(); var rows = await fresh.NotebookNotes.AsNoTracking().ToArrayAsync();
            if (!after) { Assert.Empty(rows); if (boundary == "fail") throw new IOException("before commit"); if (boundary == "cancel") { cancel.Cancel(); cancel.Token.ThrowIfCancellationRequested(); } }
            else
            {
                ++commits; var row = Assert.Single(rows); Assert.Equal(1, row.Version); Assert.Equal("Idea\nConditions\nInvalidation\nEvidence to collect\n", row.PlainText);
                Assert.Equal(row.PlainText, (await f.Service().LoadAsync(row.Id)).Note!.Document.PlainText);
                if (boundary == "ack-cancel") cancel.Cancel();
            }
        });
        var result = await f.Service(hook).ExecuteAsync(new CreateNotebookNote(Title: "Trading Idea", Template: NotebookTemplate.TradingIdea), cancel.Token);
        Assert.Equal(expected, result.Status); Assert.Equal(expected == NotebookWriteStatus.Success ? 1 : 0, commits);
        await using var db = f.Context(); Assert.Equal(commits, await db.NotebookNotes.CountAsync());
    }
    [Fact]
    public async Task CancelledLifecycleCommitRollsBackMetadataAndPreservesBody()
    {
        await using var f=await NotebookFixture.Create();var note=await f.CreateNote();using var cancel=new CancellationTokenSource();
        var writer=f.Service(new CommitBoundary(async after=>
        {
            Assert.False(after);var fresh=(await f.Service().LoadAsync(note.Id)).Note!;Assert.Equal(note.Version,fresh.Version);Assert.Equal(NotebookNoteState.Active,fresh.State);
            cancel.Cancel();await Task.CompletedTask;cancel.Token.ThrowIfCancellationRequested();
        }));
        Assert.Equal(NotebookWriteStatus.Cancelled,(await writer.ExecuteAsync(new ArchiveNotebookNote(note.Id,note.Version),cancel.Token)).Status);
        var retained=(await f.Service().LoadAsync(note.Id)).Note!;Assert.Equal(note.Version,retained.Version);Assert.Equal(NotebookNoteState.Active,retained.State);Assert.Equal(f.Codec.Encode(note.Document),f.Codec.Encode(retained.Document));
    }
    [Fact]
    public async Task InvalidTemplateTitleAndDeletedFolderNeverCreateRows()
    {
        await using var f = await NotebookFixture.Create();
        foreach(var command in new[] { new CreateNotebookNote(Template: (NotebookTemplate)77), new CreateNotebookNote(Title: " "), new CreateNotebookNote(Title: new string('x',201)) })
            Assert.Equal(NotebookWriteStatus.Invalid, (await f.Service().ExecuteAsync(command)).Status);
        Assert.Equal(NotebookWriteStatus.NotFound, (await f.Service().ExecuteAsync(new CreateNotebookNote(Guid.NewGuid(), Template: NotebookTemplate.Psychology))).Status);
        await using var db = f.Context(); Assert.Empty(await db.NotebookNotes.ToArrayAsync());
    }
    [Theory]
    [InlineData("{bad")]
    [InlineData("{\"format\":\"ptj.notebook.document\",\"version\":99,\"blocks\":[]}")]
    public async Task UnreadableBodiesKeepExactPayloadAndRecoveryAcrossEveryTransition(string original)
    {
        await using var f = await NotebookFixture.Create(); var folder = await f.Folder(); var tag = await f.Tag(); var note = await f.CreateNote(folder:folder.Id);
        note = NotebookFixture.Success(await f.Service().ExecuteAsync(new AssignNotebookTag(note.Id,note.Version,tag.Id)));
        note = NotebookFixture.Success(await f.Service().ExecuteAsync(new SetNotebookPin(note.Id,note.Version,true)));
        var recovery = new NotebookRecoveryStore(f.Factory(),f.Codec); var recoveryId=Guid.NewGuid();
        Assert.Equal(NotebookWriteStatus.Success, await recovery.PutAsync(new(recoveryId,note.Id,note.Version,1,"Unresolved",NotebookTemplates.Create(NotebookTemplate.Psychology),f.Time.Now),0));
        await using(var db=f.Context()) await db.NotebookNotes.Where(x=>x.Id==note.Id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.DocumentJson,original));
        long version=note.Version;
        foreach(var target in new[]{NotebookNoteState.Archived,NotebookNoteState.Active,NotebookNoteState.Trashed,NotebookNoteState.Active})
        {
            NotebookCommand c=target switch { NotebookNoteState.Archived=>new ArchiveNotebookNote(note.Id,version), NotebookNoteState.Trashed=>new TrashNotebookNote(note.Id,version), _=>version==note.Version+1?new UnarchiveNotebookNote(note.Id,version):new RestoreNotebookNote(note.Id,version) };
            var result=await f.Service().ExecuteAsync(c); Assert.Equal(NotebookWriteStatus.Success,result.Status); Assert.Null(result.Note);
            Assert.Equal(++version,result.UnavailableMetadata!.Version); Assert.Equal(target,result.UnavailableMetadata.State);
            await using var fresh=f.Context(); var row=await fresh.NotebookNotes.SingleAsync(); Assert.Equal(original,row.DocumentJson); Assert.Equal(note.Document.PlainText,row.PlainText); Assert.True(row.IsPinned); Assert.Equal(folder.Id,row.FolderId);
            Assert.Single(await fresh.NotebookTagAssignments.ToArrayAsync()); Assert.NotNull((await recovery.LoadAsync(recoveryId)).Draft);
        }
        Assert.Equal(NotebookWriteStatus.Conflict,(await f.Service().ExecuteAsync(new TrashNotebookNote(note.Id,note.Version))).Status);
        Assert.Equal(original,(await f.Service().LoadAsync(note.Id)).OriginalContent);
    }
    private sealed class CommitBoundary(Func<bool,Task> action):DbTransactionInterceptor
    {
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,TransactionEventData eventData,InterceptionResult result,CancellationToken cancellationToken=default) { await action(false);return result; }
        public override Task TransactionCommittedAsync(DbTransaction transaction,TransactionEndEventData eventData,CancellationToken cancellationToken=default)=>action(true);
    }
}
