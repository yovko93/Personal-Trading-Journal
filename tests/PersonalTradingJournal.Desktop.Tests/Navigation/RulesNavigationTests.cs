using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Tests.Rules;
using PersonalTradingJournal.Desktop.ViewModels;
using PersonalTradingJournal.Desktop.ViewModels.Rules;

namespace PersonalTradingJournal.Desktop.Tests.Navigation;

public sealed partial class MainWindowViewModelTests
{
    internal static MainWindowViewModel RulesShell(RulesViewModel rules) => CreateFixture(rules: rules).Main;
    [Fact]
    public async Task RulesDestinationUsesPlanningAndGuardsSwitchingAndOwnerClose()
    {
        var f = new RulesFixture(); using var shell = RulesShell(f.Vm);
        shell.NavigateCommand.Execute(NavigationDestination.Rules); await f.Vm.LoadTask;
        Assert.Same(f.Vm, shell.CurrentContentViewModel); Assert.Equal("Rules", shell.PageTitle);
        f.Vm.CreateCommand.Execute(null); f.Fill();
        shell.NavigateCommand.Execute(NavigationDestination.Playbook);
        Assert.Equal(NavigationDestination.Rules, shell.CurrentDestination); Assert.False(shell.TryCloseWindow());
        Assert.Equal("Recorded criterion", f.Vm.Fields[1].Text);
        f.Dialogs.ConfirmationResult = true; Assert.True(shell.TryCloseWindow()); Assert.True(f.Vm.IsEditing);
        shell.NavigateCommand.Execute(NavigationDestination.Settings); Assert.False(f.Vm.IsEditing);
        Assert.True(shell.TryCloseWindow()); Assert.Empty(f.Data.Rows);
        shell.NavigateCommand.Execute(NavigationDestination.Rules); await f.Vm.LoadTask; Assert.True(f.Vm.ShowEmpty);
    }
}
