using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Desktop.ViewModels.TradingPlans;

namespace PersonalTradingJournal.Desktop.Tests.TradingPlans;

public sealed class PlanDeletionTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task DeleteLoadedDraftOrReadyPreservesCancelledInputAndSkipsSavePrompt(bool ready)
    {
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync(true);
        var vm = f.Vm;
        if (ready) { await vm.SaveAsync(); await vm.MarkReadyAsync(); Assert.True(vm.Editor!.IsReadOnly); }
        var original = vm.Editor!; var key = original.Key;
        if (!ready) original.MaximumTrades.Text = "invalid retained draft";
        vm.SelectTab(true); // History is independent; delete still targets the loaded Session Brief.
        await vm.DeletePlanAsync();
        Assert.Same(original, vm.Editor); Assert.Equal(0, f.Interaction.Prompts);
        if (!ready) Assert.Equal("invalid retained draft", original.MaximumTrades.Text);
        Assert.NotNull(await f.Store.Inner.LoadAsync(key));
        f.Interaction.DeleteAccepted = true;
        f.Store.Status = TradingPlanWriteStatus.Conflict;
        await vm.DeletePlanAsync(); Assert.Same(original, vm.Editor); Assert.Contains("changed", vm.Message);
        f.Store.Status = null; await vm.DeletePlanAsync();
        Assert.Null(vm.Editor); Assert.Equal("", vm.Message); Assert.True(vm.ShowEmpty);
        Assert.Null(await f.Store.Inner.LoadAsync(key)); Assert.Equal(0, f.Interaction.Prompts);
        await vm.CreateDraftAsync(); Assert.NotNull(vm.Editor); Assert.NotEqual(original.Token.PlanId, vm.Editor.Token.PlanId);
        Assert.Equal(key, vm.Editor.Key);
    }

    [Fact]
    public async Task DeleteCancellationAndDuplicateActionsDoNotLosePendingInput()
    {
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync(true);
        f.Interaction.DeleteAccepted = true; var vm = f.Vm; var original = vm.Editor!;
        original.Focus.Text = "Retained pending explanation";
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Store.BeforeWrite = async ct => { entered.SetResult(); await Task.Delay(Timeout.Infinite, ct); };
        var deletion = vm.DeletePlanAsync(); await entered.Task;
        Assert.False(vm.DeletePlanCommand.CanExecute(null));
        await vm.DeletePlanAsync(); Assert.Equal(1, f.Store.Writes);
        await vm.CancelAsync(); await deletion;
        Assert.Same(original, vm.Editor); Assert.Equal("Retained pending explanation", original.Focus.Text);
        Assert.NotNull(await f.Store.Inner.LoadAsync(original.Key));
        Assert.Equal(1, f.Interaction.DeletePrompts);
    }
}
