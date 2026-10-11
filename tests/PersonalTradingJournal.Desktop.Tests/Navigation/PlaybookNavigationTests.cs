using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Tests.Setups;
using PersonalTradingJournal.Desktop.ViewModels;
using PersonalTradingJournal.Desktop.ViewModels.Setups;

namespace PersonalTradingJournal.Desktop.Tests.Navigation;

public sealed partial class MainWindowViewModelTests
{
    internal static MainWindowViewModel PlaybookShell(PlaybookViewModel playbook) => CreateFixture(playbook: playbook).Main;

    [Fact]
    public async Task PlaybookNavigationAndOwnerClosePreserveDirtyFieldsOnVeto()
    {
        var f = new PlaybookFixture(); using var shell = PlaybookShell(f.Vm);
        shell.NavigateCommand.Execute(NavigationDestination.Playbook); await f.Vm.LoadTask;
        Assert.Same(f.Vm, shell.CurrentContentViewModel);
        f.Vm.TrySelectSetup(f.Data.Row(f.Setup)); await f.Vm.LoadTask; f.Edit("do not discard");
        shell.NavigateCommand.Execute(NavigationDestination.Settings);
        Assert.Equal(NavigationDestination.Playbook, shell.CurrentDestination);
        Assert.False(shell.TryCloseWindow()); Assert.True(f.Vm.IsEditing);
        Assert.All(f.Vm.Fields, field => Assert.Equal("do not discard", field.Text));
        f.Dialogs.ConfirmationResult = true;
        Assert.True(shell.TryCloseWindow()); // Acceptance alone must not prematurely clear the editor.
        Assert.True(f.Vm.IsEditing);
        shell.NavigateCommand.Execute(NavigationDestination.Settings);
        Assert.Equal(NavigationDestination.Settings, shell.CurrentDestination); Assert.False(f.Vm.IsEditing);
        shell.NavigateCommand.Execute(NavigationDestination.Playbook); await f.Vm.LoadTask;
        Assert.Equal(f.Setup.Id, f.Vm.SelectedSetup!.Id); Assert.False(f.Vm.IsEditing); Assert.Empty(f.Data.Revisions);
    }

    [Fact]
    public async Task CleanPlaybookLeavesWithoutConfirmationAndDoesNotAlterOtherGuards()
    {
        var f = new PlaybookFixture(); using var shell = PlaybookShell(f.Vm);
        shell.NavigateCommand.Execute(NavigationDestination.Playbook); await f.Vm.LoadTask;
        Assert.True(shell.TryCloseWindow()); Assert.Null(f.Dialogs.ConfirmationRequest);
        shell.NavigateCommand.Execute(NavigationDestination.Dashboard);
        Assert.Equal(NavigationDestination.Dashboard, shell.CurrentDestination); Assert.True(shell.TryCloseWindow());
    }
}
