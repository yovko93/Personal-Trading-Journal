using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Tests.Notebook;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Navigation;

public sealed partial class MainWindowViewModelTests
{
    [Theory]
    [InlineData(NotebookLeaveChoice.Cancel)]
    [InlineData(NotebookLeaveChoice.Discard)]
    [InlineData(NotebookLeaveChoice.Save)]
    public async Task NotebookDirtyDraftUsesShellAsyncConsentWithoutDiscardingBeforeFinalClose(NotebookLeaveChoice choice)
    {
        var store = new NotebookFake();
        var interaction = new NotebookChoices { Choice = choice };
        using var vm = new NotebookWorkspaceViewModel(store, new FakeDialogService(), interaction);
        using var shell = CreateFixture(notebook: vm).Main;
        shell.NavigateCommand.Execute(NavigationDestination.Notebook); await vm.LoadTask;
        await vm.NewNoteCommand.ExecuteAsync(null); vm.DraftTitle = "Dirty title";
        Assert.True(shell.NeedsPlanClosePreparation);
        bool accepted = await shell.PreparePlanCloseAsync();
        Assert.Equal(choice != NotebookLeaveChoice.Cancel, accepted);
        if (choice == NotebookLeaveChoice.Discard)
        {
            Assert.True(vm.IsDirty); shell.CancelPlanClose(); Assert.True(vm.IsDirty); Assert.False(shell.CanCloseWindow());
        }
        shell.NavigateCommand.Execute(NavigationDestination.Dashboard); await shell.PlanNavigationTask;
        Assert.Equal(choice == NotebookLeaveChoice.Cancel ? NavigationDestination.Notebook : NavigationDestination.Dashboard, shell.CurrentDestination);
        if (choice != NotebookLeaveChoice.Cancel) Assert.False(vm.IsDirty);
        Assert.Equal(choice == NotebookLeaveChoice.Save ? 2 : 1, store.Writes);
    }
    [Fact]
    public async Task NotebookUsesExistingDestinationAndGuardsPendingWriteThenCancelsOwnedReadsOnDeparture()
    {
        var store = new NotebookFake();
        using var vm = new NotebookWorkspaceViewModel(store, new FakeDialogService());
        using var shell = CreateFixture(notebook: vm).Main;
        shell.NavigateCommand.Execute(NavigationDestination.Notebook); await vm.LoadTask;
        Assert.Same(vm, shell.CurrentContentViewModel); Assert.Equal("Notebook", shell.PageTitle);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = new TaskCompletionSource<NotebookWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        store.OnWrite = (_, _) => { entered.TrySetResult(); return result.Task; };
        var write = vm.NewNoteCommand.ExecuteAsync(null);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            shell.NavigateCommand.Execute(NavigationDestination.Dashboard);
            Assert.Same(vm, shell.CurrentContentViewModel); Assert.False(shell.TryCloseWindow());
            result.SetResult(new(NotebookWriteStatus.Cancelled)); await write;
            vm.FolderName = "Retained organization input";
            shell.NavigateCommand.Execute(NavigationDestination.Dashboard); await vm.DeactivationTask;
            Assert.Equal(NavigationDestination.Dashboard, shell.CurrentDestination);
            shell.NavigateCommand.Execute(NavigationDestination.Notebook); await vm.LoadTask;
            Assert.Equal("Retained organization input", vm.FolderName); Assert.True(shell.TryCloseWindow());
        }
        finally { result.TrySetResult(new(NotebookWriteStatus.Cancelled)); await write.WaitAsync(TimeSpan.FromSeconds(10)); }
    }
}
