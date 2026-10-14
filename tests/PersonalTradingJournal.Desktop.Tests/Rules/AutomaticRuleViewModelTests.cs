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
    public async Task ProseEditorRetainsTypedConditionAndHistoricalEvidenceWithoutEditableCheckFields()
    {
        var f = new RulesFixture();
        var criterion = new AutomaticRuleCriterion(RuleProfitBasis.StrictNet, 25.125m, "EUR", capability: RuleCheckCapability.ClosedDayLoss);
        var original = TradingRuleRevision.Create(Guid.NewGuid(), new("Existing automatic rule", "Original statement", "Original required facts", criterion), DateTimeOffset.UtcNow.AddDays(-1));
        f.Data.Rows.Add(original); await f.Open(original.RuleId);
        Assert.True(f.Vm.SelectedRule!.HasAutomaticCondition);
        Assert.Contains("25.125", f.Vm.SelectedRule.AutomaticCondition);
        Assert.Contains("EUR", f.Vm.SelectedRule.AutomaticCondition);
        Assert.Contains("New York closed-day", f.Vm.SelectedRule.AutomaticCondition);
        f.Vm.EditCommand.Execute(null); Assert.Equal(2, f.Vm.Fields.Count);
        f.Vm.Fields[1].Text = "Edited statement";
        f.Dialogs.ConfirmationResult = false; Assert.False(f.Vm.TryLeave());
        f.Data.Failure = "conflict"; await f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.True(f.Vm.IsEditing); Assert.Equal("Edited statement", f.Vm.Fields[1].Text);
        f.Data.Failure = null; await f.Vm.SaveCommand.ExecuteAsync(null);
        var saved = f.Vm.SelectedRule.Details.Revision.Definition;
        Assert.Equal(criterion, saved.AutomaticCriterion);
        Assert.Equal("Original required facts", saved.RequiredEvidence);
        Assert.Equal("Original statement", f.Data.Rows[0].Definition.Criterion);
        f.Vm.CreateCommand.Execute(null); f.Fill(); await f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.Null(f.Vm.SelectedRule.Details.Revision.Definition.AutomaticCriterion);
        Assert.Empty(f.Vm.SelectedRule.Details.Revision.Definition.RequiredEvidence);
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
