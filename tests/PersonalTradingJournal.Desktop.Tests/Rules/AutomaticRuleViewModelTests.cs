using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Desktop.ViewModels.Rules;
using PersonalTradingJournal.Domain.Rules;

namespace PersonalTradingJournal.Desktop.Tests.Rules;

public sealed class AutomaticRuleViewModelTests
{
    [Fact]
    public async Task ExplicitOnlyExactTargetDuplicateAndCancellationPreserveManualDraft()
    {
        var f = new AssessmentFixture(); var checks = new Checks { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var vm = Create(f, checks); await vm.OpenAsync(f.Data.Target); await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(0, checks.Calls); vm.Explanation = "Unsaved manual explanation";
        var pending = vm.RunChecksCommand.ExecuteAsync(null);
        await checks.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(f.Data.Target, checks.Target); Assert.Equal(1, checks.Calls);
        Assert.False(vm.RunChecksCommand.CanExecute(null)); Assert.False(vm.TryLeave());
        await vm.RunChecksCommand.ExecuteAsync(null); Assert.Equal(1, checks.Calls);
        vm.CancelCommand.Execute(null); checks.Gate.SetResult(); await pending;
        Assert.Contains("cancelled", vm.Message); Assert.Equal("Unsaved manual explanation", vm.Explanation); Assert.Empty(f.Data.Saved);
    }
    [Theory]
    [InlineData(RuleCheckStatus.Conflict)] [InlineData(RuleCheckStatus.LimitExceeded)] [InlineData(RuleCheckStatus.UnavailableTarget)]
    public async Task ErrorAndVetoNeverDiscardDraftOrRecordManualOutcome(RuleCheckStatus status)
    {
        var f = new AssessmentFixture(); var checks = new Checks { Status = status }; var vm = Create(f, checks);
        await vm.OpenAsync(f.Data.Target); vm.Explanation = "Keep this";
        f.Dialogs.ConfirmationResult = false; await vm.RunChecksCommand.ExecuteAsync(null); Assert.Equal(0, checks.Calls);
        f.Dialogs.ConfirmationResult = true; await vm.RunChecksCommand.ExecuteAsync(null);
        Assert.Equal(1, checks.Calls); Assert.Equal("Keep this", vm.Explanation); Assert.Empty(f.Data.Saved); Assert.DoesNotContain("committed", vm.Message);
    }
    [Fact]
    public async Task SuccessReloadsAndUnavailableTargetCannotRun()
    {
        var f = new AssessmentFixture(); var checks = new Checks(); var vm = Create(f, checks);
        await vm.OpenAsync(f.Data.Target); await vm.RunChecksCommand.ExecuteAsync(null);
        Assert.Contains("0 new", vm.Message); Assert.Contains("1 identical", vm.Message);
        f.Data.Available = false; await vm.RefreshCommand.ExecuteAsync(null); Assert.False(vm.RunChecksCommand.CanExecute(null));
    }
    [Fact]
    public async Task AuthoringPersistsTypedCriterionAndGuardsChanges()
    {
        var f = new RulesFixture(); await f.Vm.ActivateAsync(); f.Vm.CreateCommand.Execute(null); f.Fill();
        f.Vm.Automatic = true; f.Vm.LossLimit = "25"; f.Vm.CheckCurrency = "USD"; f.Vm.Basis = RuleProfitBasis.StrictNet;
        await f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(new AutomaticRuleCriterion(RuleProfitBasis.StrictNet, 25, "USD"), f.Vm.SelectedRule!.Details.Revision.Definition.AutomaticCriterion);
        f.Vm.EditCommand.Execute(null); f.Vm.LossLimit = "-1"; await f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.True(f.Vm.IsEditing); Assert.Contains("Cannot save", f.Vm.Message); Assert.True(f.Vm.HasUnsavedChanges);
        f.Dialogs.ConfirmationResult = false; Assert.False(f.Vm.TryLeave()); Assert.Equal("-1", f.Vm.LossLimit);
    }
    private static RuleAssessmentViewModel Create(AssessmentFixture f, Checks checks) =>
        new(f.Data, f.Data, f.Data.Rules, f.Data, new(f.Data.Rules, f.Data, TimeProvider.System), f.Dialogs, checks);
    internal sealed class Checks : IAutomaticRuleChecks
    {
        internal int Calls; internal TradingRuleAssessmentTarget? Target;
        internal TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal RuleCheckStatus Status = RuleCheckStatus.Completed;
        internal TaskCompletionSource? Gate;
        public async Task<RuleCheckRun> RunAsync(TradingRuleAssessmentTarget target, CancellationToken cancellationToken = default)
        { Calls++; Target = target; Started.TrySetResult(); if (Gate is not null) await Gate.Task; cancellationToken.ThrowIfCancellationRequested(); return new(Status, 0, 1, []); }
    }
}
