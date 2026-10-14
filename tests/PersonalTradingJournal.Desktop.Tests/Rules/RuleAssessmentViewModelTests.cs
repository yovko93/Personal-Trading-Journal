using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Rules;
using PersonalTradingJournal.Domain.Rules;

namespace PersonalTradingJournal.Desktop.Tests.Rules;

public sealed class RuleAssessmentViewModelTests
{
    [Fact]
    public async Task RetainedRuleHistoryIsAccessibleInsideAssessmentWorkflowAndGuardsEnteredExplanation()
    {
        var f = new AssessmentFixture(); await f.Open(); f.Fill(); await f.Vm.SaveCommand.ExecuteAsync(null);
        f.Vm.Explanation = "Unsaved correction"; f.Dialogs.ConfirmationResult = false;
        await f.Vm.RuleHistoryCommand.ExecuteAsync(null);
        Assert.Equal(f.Data.Target, f.Vm.Target); Assert.Equal("Unsaved correction", f.Vm.Explanation);
        f.Dialogs.ConfirmationResult = true; await f.Vm.RuleHistoryCommand.ExecuteAsync(null);
        Assert.Null(f.Vm.Target); Assert.False(f.Vm.CanRecord);
        Assert.Single(f.Vm.History); Assert.Equal(f.Data.Target, f.Vm.History[0].Details.Assessment.Target);
    }

    [Theory]
    [InlineData(TradingRuleAssessmentOutcome.CannotEvaluate)]
    [InlineData(TradingRuleAssessmentOutcome.NoViolation)]
    [InlineData(TradingRuleAssessmentOutcome.ConfirmedViolation)]
    public async Task AllOutcomesAreManualConfirmedVerifiedAndCorrectionsAppend(TradingRuleAssessmentOutcome outcome)
    {
        var f = new AssessmentFixture(); await f.Open();
        Assert.Contains("Not assessed", f.Vm.LatestLabel); Assert.Empty(f.Data.Saved);
        f.Fill(outcome); await f.Vm.SaveCommand.ExecuteAsync(null);
        var first = Assert.Single(f.Data.Saved);
        Assert.Equal(outcome, first.Outcome); Assert.Equal(f.Data.Target, first.Target);
        Assert.Equal(TradingRuleAssessmentSource.Manual, first.Source); Assert.Equal(1, first.RuleRevision.Revision);
        Assert.Contains("saved and verified", f.Vm.Message); Assert.False(f.Vm.HasUnsavedChanges);
        Assert.Contains("revision 1", f.Dialogs.ConfirmationRequest!.Message);
        f.Fill(TradingRuleAssessmentOutcome.CannotEvaluate); await f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(2, f.Data.Saved.Count); Assert.Equal(first.Id, f.Data.Saved[1].SupersedesAssessmentId);
        Assert.Equal(new[] { 2, 1 }, f.Vm.History.Select(h => h.Details.Sequence));
        Assert.Contains("Manual user assessment", f.Vm.History[0].Attribution);
    }

    [Fact]
    public async Task InsufficientEvidenceAndBlankExplanationNeverWriteOrDiscardEdits()
    {
        var f = new AssessmentFixture(); await f.Open(); await f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.True(f.Vm.HasValidation); f.Vm.Explanation = "No commission evidence";
        f.Vm.Outcome = TradingRuleAssessmentOutcome.NoViolation; await f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.Contains("sources", f.Vm.Validation); Assert.Empty(f.Data.Saved);
        Assert.Equal("No commission evidence", f.Vm.Explanation);
        f.Vm.Outcome = TradingRuleAssessmentOutcome.ConfirmedViolation; f.Vm.ExplicitViolation = true;
        await f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(TradingRuleAssessmentBasis.UserEnteredViolation, Assert.Single(f.Data.Saved).Basis);
    }

    [Theory]
    [InlineData("conflict")]
    [InlineData("failure")]
    [InlineData("cancel")]
    [InlineData("readback")]
    public async Task FailureConflictCancellationAndUnverifiedCommitRetainDraft(string mode)
    {
        var f = new AssessmentFixture(); await f.Open(); f.Fill(); f.Data.Failure = mode;
        await f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Recorded sources are incomplete", f.Vm.Explanation); Assert.True(f.Vm.Evidence[0].Selected);
        Assert.DoesNotContain("saved and verified", f.Vm.Message); Assert.Equal(mode == "readback" ? 1 : 0, f.Data.Saved.Count);
        if (mode is "conflict" or "readback") Assert.False(f.Vm.CanRecord);
    }

    [Fact]
    public async Task RuleChangeAndCompetingAssessmentAreConflictsNotSilentBaselineAdvances()
    {
        var f = new AssessmentFixture(); await f.Open(); f.Fill();
        f.Data.Rules.Rows.Add(f.Data.Rules.Rows[0].Revise(1, f.Data.Rules.Rows[0].Definition, DateTimeOffset.UtcNow));
        await f.Vm.NextEvidenceCommand.ExecuteAsync(null); // Paging cannot adopt newer revision while dirty.
        await f.Vm.SaveCommand.ExecuteAsync(null); Assert.Empty(f.Data.Saved); Assert.Contains("changed", f.Vm.Message);
        await f.Vm.RefreshCommand.ExecuteAsync(null); f.Fill();
        var other = f.Create(); await other.OpenAsync(f.Data.Target); other.Explanation = "Competing manual observation";
        await other.SaveCommand.ExecuteAsync(null); await f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.Single(f.Data.Saved); Assert.Equal("Recorded sources are incomplete", f.Vm.Explanation);
    }

    [Fact]
    public async Task DeclinedConfirmationAndDirtyRefreshSwitchClosePreserveAllFields()
    {
        var f = new AssessmentFixture(); f.Data.Rules.Seed(1); await f.Open(); f.Fill();
        f.Dialogs.ConfirmationResult = false; var id = f.Vm.SelectedRule!.Id;
        await f.Vm.SaveCommand.ExecuteAsync(null); Assert.Empty(f.Data.Saved);
        Assert.False(f.Vm.TryLeave()); Assert.False(f.Vm.TrySelect(f.Vm.Rules.Single(r => r.Id != id)));
        await f.Vm.RefreshCommand.ExecuteAsync(null); Assert.Equal(id, f.Vm.SelectedRule.Id);
        Assert.Equal("Recorded sources are incomplete", f.Vm.Explanation); Assert.True(f.Vm.Evidence[0].Selected);
    }

    [Fact]
    public async Task WriteInFlightBlocksDuplicateSubmissionAndDepartureAndSupportsCancellation()
    {
        var f = new AssessmentFixture(); await f.Open(); f.Fill();
        f.Data.WriteGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.True(f.Vm.IsBusy); Assert.False(f.Vm.SaveCommand.CanExecute(null)); Assert.False(f.Vm.TryLeave());
        f.Vm.CancelCommand.Execute(null); f.Data.WriteGate.SetResult(); await pending;
        Assert.Empty(f.Data.Saved); Assert.True(f.Vm.HasUnsavedChanges); Assert.False(f.Vm.IsBusy);
    }

    [Fact]
    public async Task LateCancelledLoadCannotPublishIntoClosedDialog()
    {
        var f = new AssessmentFixture(); f.Data.ReadGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = f.Open(); f.Vm.Deactivate(); f.Data.ReadGate.SetResult(); await pending;
        Assert.Empty(f.Vm.Rules); Assert.Empty(f.Vm.History); Assert.False(f.Vm.IsBusy);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(20)]
    public async Task BoundedHistoryLookaheadAndRulesHistoryOpenKeepExactTarget(int count)
    {
        var f = new AssessmentFixture(); await f.Open();
        for (int i = 0; i < count; i++) { f.Fill(); await f.Vm.SaveCommand.ExecuteAsync(null); }
        Assert.Equal(10, f.Vm.History.Count); Assert.Equal(count > 10, f.Vm.OlderCommand.CanExecute(null));
        if (count > 10)
        {
            f.Vm.Explanation = "Unsubmitted correction"; await f.Vm.OlderCommand.ExecuteAsync(null);
            Assert.Equal(count - 10, f.Vm.History[0].Details.Sequence); Assert.False(f.Vm.OlderCommand.CanExecute(null));
            Assert.Equal("Unsubmitted correction", f.Vm.Explanation);
        }
        var browser = f.Create(); await browser.OpenAsync(null, f.Data.RuleId);
        Assert.False(browser.HasTarget); Assert.False(browser.CanRecord);
        await browser.OpenTargetCommand.ExecuteAsync(browser.History[0]);
        Assert.Equal(f.Data.Target, browser.Target); Assert.True(browser.CanRecord);
        f.Data.Available = false; await browser.RefreshCommand.ExecuteAsync(null);
        Assert.False(browser.CanRecord); Assert.NotEmpty(browser.History); Assert.Contains("unavailable", browser.Message);
    }
}

internal sealed class AssessmentFixture
{
    public AssessmentData Data { get; } = new();
    public FakeDialogService Dialogs { get; } = new() { ConfirmationResult = true };
    public RuleAssessmentViewModel Vm { get; }
    public AssessmentFixture() => Vm = Create();
    public RuleAssessmentViewModel Create() => new(Data, Data, Data.Rules, Data, new(Data.Rules, Data, TimeProvider.System), Dialogs);
    public Task Open() => Vm.OpenAsync(Data.Target);
    public void Fill(TradingRuleAssessmentOutcome outcome = TradingRuleAssessmentOutcome.CannotEvaluate)
    {
        Vm.Explanation = "Recorded sources are incomplete"; Vm.Outcome = outcome;
        if (Vm.Evidence.Count > 0) Vm.Evidence[0].Selected = true;
        Vm.Sufficient = outcome != TradingRuleAssessmentOutcome.CannotEvaluate;
    }
}

internal sealed class AssessmentData : IManualRuleAssessmentReader, ITradingRuleReader, ITradingRuleAssessmentStore
{
    public RulesData Rules { get; } = new();
    public Guid RuleId { get; }
    public TradingRuleAssessmentTarget Target { get; }
    public bool Available { get; set; } = true;
    public string? Failure { get; set; }
    public TaskCompletionSource? WriteGate { get; set; }
    public TaskCompletionSource? ReadGate { get; set; }
    public List<TradingRuleAssessment> Saved { get; } = [];
    public AssessmentData() { RuleId = Rules.Seed(1); Target = TradingRuleAssessmentTarget.ForTrade(Guid.NewGuid(), Rules.AccountId); }
    public async Task<RuleAssessmentTargetInfo> ReadTargetAsync(TradingRuleAssessmentTarget target, CancellationToken token = default)
    { if (ReadGate is not null) await ReadGate.Task; return new("Synthetic Trade · exact Account · USD", Available); }
    public Task<TradingRulePage> ReadRulesAsync(TradingRuleAssessmentTarget target, int page = 1, int pageSize = 20, CancellationToken token = default) => Rules.ReadCurrentAsync(page: page, pageSize: pageSize, cancellationToken: token);
    public Task<IReadOnlyList<RuleEvidenceOption>> ReadEvidenceAsync(TradingRuleAssessmentTarget target, TradingRuleEvidenceKind kind, int page = 1, int pageSize = 20, CancellationToken token = default) => Task.FromResult<IReadOnlyList<RuleEvidenceOption>>(kind == TradingRuleEvidenceKind.Trade ? [new(new(kind, Target.TradeId!.Value), "Synthetic Trade · USD", "Gross 10 USD; Net unavailable: commission unknown")] : []);
    public Task<TradingRuleAssessment?> GetLatestAsync(Guid ruleId, TradingRuleAssessmentTarget target, CancellationToken cancellationToken = default) => Task.FromResult(Saved.LastOrDefault(a => a.RuleRevision.RuleId == ruleId && a.Target == target));
    public async Task<bool> TryAppendAsync(TradingRuleAssessment candidate, int expectedRuleRevision, Guid? expectedLatestAssessmentId, CancellationToken cancellationToken = default)
    {
        if (WriteGate is not null) await WriteGate.Task;
        cancellationToken.ThrowIfCancellationRequested();
        if (Failure == "cancel") throw new OperationCanceledException();
        if (Failure == "failure") throw new IOException("Synthetic failure");
        if (Failure == "conflict") return false;
        Saved.Add(candidate); return true;
    }
    private TradingRuleAssessmentDetails Details(TradingRuleAssessment a) => new(a, Saved.IndexOf(a) + 1, "Synthetic Account", Available ? TradingRuleSourceState.Available : TradingRuleSourceState.Unavailable,
        a.EvidenceReferences.Select(r => new TradingRuleEvidenceDetails(r, Rules.AccountId, "Synthetic Account", Target.TradeId, null, "USD", TradingRuleSourceState.Available)).ToArray());
    public Task<TradingRuleAssessmentDetails?> ReadAssessmentAsync(Guid id, CancellationToken cancellationToken = default)
    { if (Failure == "readback") throw new IOException("Synthetic readback failure"); return Task.FromResult(Saved.FirstOrDefault(a => a.Id == id) is { } a ? Details(a) : null); }
    public Task<IReadOnlyList<TradingRuleAssessmentDetails>> ReadAssessmentsAsync(Guid ruleId, TradingRuleAssessmentTarget target, int? beforeSequence = null, int limit = 20, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TradingRuleAssessmentDetails>>(Saved.Where(a => a.RuleRevision.RuleId == ruleId && a.Target == target).Select(Details).Where(a => beforeSequence is null || a.Sequence < beforeSequence).OrderByDescending(a => a.Sequence).Take(limit).ToArray());
    public Task<IReadOnlyList<TradingRuleAssessmentDetails>> ReadRuleHistoryAsync(Guid ruleId, int page = 1, int pageSize = 10, CancellationToken token = default) => Task.FromResult<IReadOnlyList<TradingRuleAssessmentDetails>>(Saved.Where(a => a.RuleRevision.RuleId == ruleId).Select(Details).OrderByDescending(a => a.Sequence).Skip((page - 1) * pageSize).Take(pageSize + 1).ToArray());
    public Task<TradingRulePage> ReadCatalogAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) => Rules.ReadCatalogAsync(page, pageSize, cancellationToken);
    public Task<TradingRulePage> ReadCurrentAsync(TradingRuleAccountScope? scope = null, bool? active = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) => Rules.ReadCurrentAsync(scope, active, page, pageSize, cancellationToken);
    public Task<IReadOnlyList<TradingRuleDetails>> ReadRevisionsAsync(Guid ruleId, int? beforeRevision = null, int limit = 20, CancellationToken cancellationToken = default) => Rules.ReadRevisionsAsync(ruleId, beforeRevision, limit, cancellationToken);
}
