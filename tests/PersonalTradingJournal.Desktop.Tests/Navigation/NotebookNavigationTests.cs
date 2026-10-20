using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Tests.Notebook;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Navigation;

public sealed partial class MainWindowViewModelTests
{
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
