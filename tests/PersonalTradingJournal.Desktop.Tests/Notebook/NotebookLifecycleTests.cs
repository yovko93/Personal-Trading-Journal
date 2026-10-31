using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed class NotebookLifecycleTests
{
    private static async Task<NotebookWorkspaceViewModel> Open(NotebookFake store, NotebookChoices? choices=null,
        NotebookRecoveryFake? recovery=null, NotebookClock? clock=null)
    {
        store.Notes.Add(NotebookFake.Note() with {Document=NotebookEditingTests.Text("Original"),IsPinned=true});
        var vm=new NotebookWorkspaceViewModel(store,new FakeDialogService{ConfirmationResult=true},choices??new(){Choice=NotebookLeaveChoice.Save},new NotebookJsonCodec(),recovery,clock);
        await vm.ActivateAsync(); vm.SelectedNote=vm.Notes[0]; await vm.LoadTask; return vm;
    }
    [Theory]
    [InlineData(NotebookTemplate.BlankNote)] [InlineData(NotebookTemplate.MarketStudy)] [InlineData(NotebookTemplate.TradingIdea)]
    [InlineData(NotebookTemplate.LessonLearned)] [InlineData(NotebookTemplate.Psychology)]
    public async Task TemplatesCreateFreshNotesAndBlankActionStaysBlank(NotebookTemplate template)
    {
        var store=new NotebookFake();using var vm=await Open(store);var old=vm.Note!.Id;
        vm.SelectedTemplate=vm.Templates.Single(x=>x.Id==template); await vm.CreateTemplateCommand.ExecuteAsync(null);
        Assert.NotEqual(old,vm.Note!.Id);Assert.Equal(NotebookTemplates.Create(template).PlainText,vm.DraftDocument.PlainText);
        Assert.Equal(1,vm.Note.Version); Assert.False(vm.IsDirty);Assert.True(vm.CanEditBody);Assert.DoesNotContain("Choose a note",vm.ContentMessage);Assert.Empty(vm.LinkedItems);
        var first=vm.Note;await vm.CreateTemplateCommand.ExecuteAsync(null);Assert.NotEqual(first.Id,vm.Note!.Id);
        Assert.Empty(first.Document.Blocks.Select(x=>x.Id).Intersect(vm.Note.Document.Blocks.Select(x=>x.Id)));
        await vm.NewNoteCommand.ExecuteAsync(null); Assert.Equal("Untitled",vm.Note!.Title);Assert.Empty(vm.Note.Document.Blocks);
    }
    [Fact]
    public async Task LifecycleRefreshesScopesPreservesFiltersAndRestoresArchivedState()
    {
        var store=new NotebookFake();using var vm=await Open(store);var id=vm.Note!.Id;
        vm.SearchText="Liquidity";await vm.SearchCommand.ExecuteAsync(null); vm.SelectedNote=vm.Notes[0];await vm.LoadTask;
        await vm.ArchiveCommand.ExecuteAsync(null);Assert.Single(vm.Notes);Assert.Equal(id,vm.Note!.Id);Assert.Equal("Liquidity",vm.SearchText);
        Assert.Equal(1,vm.Scopes.Single(x=>x.Scope==NotebookScope.AllNotes).Count);
        vm.Scope=vm.Scopes.Single(x=>x.Scope==NotebookScope.AllNotes);await vm.LoadTask;vm.SelectedNote=vm.Notes[0];await vm.LoadTask;
        Assert.True(vm.ShowUnarchive);Assert.False(vm.CanEditDraft);Assert.False(vm.PinCommand.CanExecute(null));
        await vm.TrashCommand.ExecuteAsync(null);Assert.Empty(vm.Notes);
        vm.Scope=vm.Scopes.Single(x=>x.Scope==NotebookScope.Trash);await vm.LoadTask;vm.SelectedNote=vm.Notes[0];await vm.LoadTask;
        Assert.True(vm.ShowRestore);await vm.RestoreCommand.ExecuteAsync(null);Assert.Empty(vm.Notes);
        vm.Scope=vm.Scopes.Single(x=>x.Scope==NotebookScope.AllNotes);await vm.LoadTask;vm.SelectedNote=vm.Notes[0];await vm.LoadTask;
        Assert.Equal(id,vm.Note!.Id);Assert.True(vm.Note.IsPinned);Assert.False(vm.CanEditBody);
        await vm.UnarchiveCommand.ExecuteAsync(null);vm.Scope=vm.Scopes.Single(x=>x.Scope==NotebookScope.Pinned);await vm.LoadTask;
        vm.SelectedNote=vm.Notes[0];await vm.LoadTask;Assert.Equal(id,vm.Note!.Id);Assert.True(vm.CanEditDraft);
        vm.Deactivate();await vm.DeactivationTask;await vm.ActivateAsync();Assert.Equal(id,vm.Note!.Id);
    }
    [Theory]
    [InlineData(true,NotebookWriteStatus.StorageFailure)] [InlineData(false,NotebookWriteStatus.StorageFailure)]
    [InlineData(true,NotebookWriteStatus.Cancelled)] [InlineData(false,NotebookWriteStatus.Cancelled)]
    [InlineData(true,NotebookWriteStatus.Conflict)] [InlineData(false,NotebookWriteStatus.Conflict)]
    public async Task FailedOperationsRetainExplicitlyDiscardedDraftUntilCommit(bool create,NotebookWriteStatus failure)
    {
        var store=new NotebookFake();using var vm=await Open(store,new(){Choice=NotebookLeaveChoice.Discard});var note=vm.Note;var revision=vm.EditorRevision;
        vm.DraftTitle="Keep on failure";var document=NotebookEditingTests.Text("Exact retained body");vm.SetDraftDocument(document);store.NextWrite=failure;
        await (create?vm.CreateTemplateCommand:vm.ArchiveCommand).ExecuteAsync(null);
        Assert.Same(note,vm.Note);Assert.Same(document,vm.DraftDocument);Assert.Equal("Keep on failure",vm.DraftTitle);Assert.True(vm.IsDirty);Assert.Equal(revision,vm.EditorRevision);Assert.Single(store.Notes);
    }
    [Fact]
    public async Task CancelledConsentDoesNotCreateOrTransition()
    {
        var store=new NotebookFake();using var vm=await Open(store,new(){Choice=NotebookLeaveChoice.Cancel});vm.DraftTitle="Retained";
        await vm.CreateTemplateCommand.ExecuteAsync(null);await vm.ArchiveCommand.ExecuteAsync(null);Assert.Equal(0,store.Writes);Assert.True(vm.IsDirty);Assert.Equal("Retained",vm.DraftTitle);
    }
    [Fact]
    public async Task InFlightSaveAndNewerEditsDrainBeforeTransitionWithoutResurrectingCheckpoint()
    {
        var store=new NotebookFake();var recovery=new NotebookRecoveryFake();var clock=new NotebookClock();using var vm=await Open(store,recovery:recovery,clock:clock);
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var finish=new TaskCompletionSource<NotebookWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        store.OnWrite=(command,_)=>{Assert.IsType<SaveNotebookDraft>(command);entered.TrySetResult();return finish.Task;};
        vm.DraftTitle="First";clock.Advance(750);await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));vm.DraftTitle="Newer";
        var archive=vm.ArchiveCommand.ExecuteAsync(null);Assert.False(await vm.PrepareLeaveAsync());Assert.False(vm.NewNoteCommand.CanExecute(null));
        var saved=store.Notes[0] with {Title="First",Version=2};store.Notes[0]=saved;store.OnWrite=null;finish.SetResult(new(NotebookWriteStatus.Success,saved));
        await archive.WaitAsync(TimeSpan.FromSeconds(10));Assert.Equal("Newer",store.Notes[0].Title);Assert.Equal(NotebookNoteState.Archived,store.Notes[0].State);
        Assert.Equal(4,store.Notes[0].Version);Assert.Empty(recovery.Drafts);clock.Advance(2000);await vm.AutosaveTask;Assert.Empty(recovery.Drafts);
    }
    [Fact]
    public async Task PendingCreationBlocksCompetingCommandsAndDisposalSuppressesLatePublication()
    {
        var store=new NotebookFake();using var vm=await Open(store);var old=vm.Note;
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var finish=new TaskCompletionSource<NotebookWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        store.OnWrite=(_,_)=>{entered.TrySetResult();return finish.Task;};
        var create=vm.CreateTemplateCommand.ExecuteAsync(null);await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(vm.TryLeave());Assert.False(vm.NewNoteCommand.CanExecute(null));vm.DraftTitle="Blocked";Assert.Equal(old!.Title,vm.DraftTitle);
        await vm.NewNoteCommand.ExecuteAsync(null);Assert.Equal(1,store.Writes);vm.Dispose();
        finish.SetResult(new(NotebookWriteStatus.Success,NotebookFake.Note("Untitled") with{Document=NotebookDocument.Empty}));await create;
        Assert.Same(old,vm.Note);
    }
    [Fact]
    public async Task ConfirmedCommitAfterCancellationAcknowledgesStateAndExactDiscardConsent()
    {
        var store=new NotebookFake();using var vm=await Open(store,new(){Choice=NotebookLeaveChoice.Discard});var before=vm.Note!;
        vm.DraftTitle="Discard only after commit";
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var finish=new TaskCompletionSource<NotebookWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        store.OnWrite=(_,_)=>{entered.TrySetResult();return finish.Task;};
        var operation=vm.ArchiveCommand.ExecuteAsync(null);await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var cancel=vm.CancelAsync();var saved=before with{State=NotebookNoteState.Archived,Version=before.Version+1};store.Notes[0]=saved;
        finish.SetResult(new(NotebookWriteStatus.Success,saved));await Task.WhenAll(operation,cancel).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(saved,vm.Note);Assert.Equal(before.Title,vm.DraftTitle);Assert.False(vm.IsDirty);Assert.False(vm.CanEditDraft);Assert.Equal(1,store.Writes);
    }
    [Fact]
    public async Task UnresolvedCheckpointSurvivesCleanLifecycleTransition()
    {
        var store=new NotebookFake();var recovery=new NotebookRecoveryFake();var clock=new NotebookClock();using var vm=await Open(store,recovery:recovery,clock:clock);
        var draft=new NotebookRecoveryDraft(Guid.NewGuid(),vm.Note!.Id,1,1,"Other unresolved draft",NotebookEditingTests.Text("Recovery"),clock.GetUtcNow());recovery.Drafts.Add(draft.Id,draft);
        await vm.ArchiveCommand.ExecuteAsync(null);Assert.Equal(draft,Assert.Single(recovery.Drafts).Value);Assert.Equal(0,recovery.Removes);
    }
    [Fact]
    public async Task FutureContentOffersLifecycleActionsWithoutEditableSubstitute()
    {
        var store=new NotebookFake();store.Notes.Add(NotebookFake.Note());var id=store.Notes[0].Id;
        store.OnLoad=(_,_)=>Task.FromResult(new NotebookLoadResult(NotebookLoadStatus.UnsupportedContent,UnavailableMetadata:new(id,"Future",1,NotebookNoteState.Active)));
        using var vm=new NotebookWorkspaceViewModel(store,new FakeDialogService{ConfirmationResult=true});await vm.ActivateAsync();vm.SelectedNote=vm.Notes[0];await vm.LoadTask;
        Assert.Null(vm.Note);Assert.Equal("Future",vm.DraftTitle);Assert.False(vm.CanEditDraft);Assert.True(vm.ArchiveCommand.CanExecute(null));
        store.OnWrite=(command,_)=>{Assert.Equal(new ArchiveNotebookNote(id,1),command);store.Notes[0]=store.Notes[0] with{State=NotebookNoteState.Archived,Version=2};return Task.FromResult(new NotebookWriteResult(NotebookWriteStatus.Success,UnavailableMetadata:new(id,"Future",2,NotebookNoteState.Archived)));};
        await vm.ArchiveCommand.ExecuteAsync(null);Assert.Single(vm.Notes);Assert.Null(vm.Note);
        Assert.DoesNotContain("Duplicate",vm.LegacyReadOnlyMessage);
    }
}
