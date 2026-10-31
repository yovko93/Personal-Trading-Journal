using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed class NotebookCopyDeleteTests
{
    private static async Task<NotebookWorkspaceViewModel> Open(NotebookFake data,FakeDialogService dialogs,NotebookChoices? choices=null)
    {data.Notes.Add(NotebookFake.Note() with{Document=NotebookEditingTests.Text("Exact body")});var vm=new NotebookWorkspaceViewModel(data,dialogs,choices??new(){Choice=NotebookLeaveChoice.Save},new NotebookJsonCodec());await vm.ActivateAsync();vm.SelectedNote=vm.Notes[0];await vm.LoadTask;return vm;}
    [Fact]
    public async Task DuplicateSavesDirtySourceAndPreservesSearchWithIndependentSelection()
    {
        var data=new NotebookFake();using var vm=await Open(data,new());var id=vm.Note!.Id;
        vm.SearchText="Liquidity";await vm.SearchCommand.ExecuteAsync(null);vm.SelectedNote=vm.Notes[0];await vm.LoadTask;
        vm.DraftTitle="Liquidity newer";await vm.DuplicateCommand.ExecuteAsync(null);
        Assert.Equal("Liquidity newer",data.Notes.Single(x=>x.Id==id).Title);Assert.Equal("Copy of Liquidity newer",vm.Note!.Title);Assert.Equal("Liquidity",vm.SearchText);Assert.NotEqual(id,vm.Note.Id);Assert.False(vm.IsDirty);Assert.Equal(2,vm.Notes.Count);
    }
    [Theory]
    [InlineData(NotebookWriteStatus.Conflict)] [InlineData(NotebookWriteStatus.StorageFailure)] [InlineData(NotebookWriteStatus.Cancelled)]
    public async Task DuplicateFailureRetainsDiscardDraft(NotebookWriteStatus status)
    {
        var data=new NotebookFake();using var vm=await Open(data,new(),new(){Choice=NotebookLeaveChoice.Discard});var before=vm.Note;vm.DraftTitle="Retain me";data.NextWrite=status;
        await vm.DuplicateCommand.ExecuteAsync(null);Assert.Same(before,vm.Note);Assert.Equal("Retain me",vm.DraftTitle);Assert.True(vm.IsDirty);Assert.Single(data.Notes);
    }
    [Fact]
    public async Task CancelledDeleteNamesNoteAndDoesNotWriteThenConfirmedDeleteClearsEditor()
    {
        var data=new NotebookFake();var dialogs=new FakeDialogService{ConfirmationResult=true};using var vm=await Open(data,dialogs);
        await vm.TrashCommand.ExecuteAsync(null);vm.Scope=vm.Scopes.Single(x=>x.Scope==NotebookScope.Trash);await vm.LoadTask;vm.SelectedNote=vm.Notes[0];await vm.LoadTask;
        var n=vm.Note!;dialogs.ConfirmationResult=false;int writes=data.Writes;await vm.DeletePermanentlyCommand.ExecuteAsync(null);
        Assert.Equal(writes,data.Writes);Assert.Contains(n.Title,dialogs.ConfirmationRequest!.Message);Assert.Contains("recovery drafts",dialogs.ConfirmationRequest.Message);Assert.Same(n,vm.Note);Assert.False(vm.DuplicateCommand.CanExecute(null));
        dialogs.ConfirmationResult=true;await vm.DeletePermanentlyCommand.ExecuteAsync(null);Assert.Null(vm.Note);Assert.Null(vm.SelectedNote);Assert.Empty(vm.DraftDocument.Blocks);Assert.False(vm.IsDirty);Assert.Empty(vm.Notes);Assert.Equal(0,vm.Scopes.Single(x=>x.Scope==NotebookScope.Trash).Count);
        int deletedWrites=data.Writes;await vm.DeletePermanentlyCommand.ExecuteAsync(null);Assert.Equal(deletedWrites,data.Writes);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PendingDuplicateBlocksOverlapAndIgnoresDisposedAcknowledgement(bool dispose)
    {
        var data=new NotebookFake();using var vm=await Open(data,new());var original=vm.Note!;
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var end=new TaskCompletionSource<NotebookWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        data.OnWrite=(_,_)=>{entered.TrySetResult();return end.Task;};var operation=vm.DuplicateCommand.ExecuteAsync(null);await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(vm.NewNoteCommand.CanExecute(null));Assert.False(vm.TryLeave());await vm.DuplicateCommand.ExecuteAsync(null);Assert.Equal(1,data.Writes);
        if(dispose)vm.Dispose();var copy=original with{Id=Guid.NewGuid(),Title=NotebookDuplication.Title(original.Title)};data.Notes.Add(copy);end.SetResult(new(NotebookWriteStatus.Success,copy));await operation;
        Assert.Equal(dispose?original.Id:copy.Id,vm.Note!.Id);
    }
    [Fact]
    public async Task CleanupPendingMeansDeletedNotFailed()
    {
        var data=new NotebookFake();using var vm=await Open(data,new(){ConfirmationResult=true});await vm.TrashCommand.ExecuteAsync(null);vm.Scope=vm.Scopes.Single(x=>x.Scope==NotebookScope.Trash);await vm.LoadTask;vm.SelectedNote=vm.Notes[0];await vm.LoadTask;
        var id=vm.Note!.Id;data.OnWrite=(c,_)=>{Assert.Equal(id,Assert.IsType<DeleteNotebookNote>(c).NoteId);data.Notes.Clear();return Task.FromResult(new NotebookWriteResult(NotebookWriteStatus.Success,DeletedNoteId:id,CleanupPending:true));};
        await vm.DeletePermanentlyCommand.ExecuteAsync(null);Assert.Null(vm.Note);Assert.Contains("permanently deleted",vm.Message);Assert.Contains("pending safe cleanup",vm.Message);
    }
}
