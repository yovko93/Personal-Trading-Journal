using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Domain.Rules;

namespace PersonalTradingJournal.Application.Tests.Rules;

public sealed class TradingRuleUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 13, 0, 0, TimeSpan.Zero);
    private static readonly Guid Account = Guid.NewGuid();
    private static TradingRuleDefinition Definition(TradingRuleAccountScope? scope = null) =>
        new("Planned entry", "Wait for confirmation", "Trade and execution facts",
            TradingRuleApplicability.Trade, scope ?? TradingRuleAccountScope.ForAccount(Account),
            TradingRuleEvaluationMode.ManualOnly);
    private static RecordTradingRuleAssessmentCommand AssessmentCommand(Guid ruleId, int revision,
        TradingRuleAssessmentTarget target, TradingRuleAssessmentOutcome outcome = TradingRuleAssessmentOutcome.CannotEvaluate,
        Guid? previous = null, TradingRuleEvidenceSufficiency sufficiency = TradingRuleEvidenceSufficiency.Missing,
        IReadOnlyList<TradingRuleEvidenceReference>? references = null) =>
        new(ruleId, revision, previous, target, outcome, TradingRuleAssessmentSource.Manual,
            TradingRuleAssessmentBasis.RecordedEvidence, sufficiency, "The needed execution is absent.",
            references ?? []);

    [Fact]
    public async Task AuthoringAppendsImmutableNumberedRevisionsAndRejectsStaleCommit()
    {
        var store = new Store();
        var useCase = new WriteTradingRuleUseCase(store, new Clock());
        Guid id = Guid.NewGuid();
        var created = await useCase.ExecuteAsync(new(id, 0, TradingRuleAuthoringAction.Create, Definition()));
        var revised = await useCase.ExecuteAsync(new(id, 1, TradingRuleAuthoringAction.Revise, Definition()));
        var archived = await useCase.ExecuteAsync(new(id, 2, TradingRuleAuthoringAction.Archive, null));
        Assert.Equal(new[] { 1, 2, 3 }, store.Revisions.Select(item => item.Revision));
        Assert.Equal(TradingRuleWriteStatus.Saved, archived.Status);
        Assert.False(archived.Value!.IsActive);
        Assert.True(created.Value!.IsActive);
        Assert.Equal(TradingRuleWriteStatus.Conflict,
            (await useCase.ExecuteAsync(new(id, 1, TradingRuleAuthoringAction.Revise, Definition()))).Status);
        Assert.Equal(TradingRuleWriteStatus.InvalidState,
            (await useCase.ExecuteAsync(new(id, 3, TradingRuleAuthoringAction.Revise, Definition()))).Status);
        store.RejectRuleAppend = true;
        Assert.Equal(TradingRuleWriteStatus.Conflict,
            (await useCase.ExecuteAsync(new(id, 3, TradingRuleAuthoringAction.Reopen, null))).Status);
        Assert.Equal(3, store.Revisions.Count);
        Assert.Equal(TradingRuleWriteStatus.Saved, revised.Status);
    }

    [Fact]
    public async Task MissingRuleAndInvalidCommandsNeverWrite()
    {
        var store = new Store();
        var useCase = new WriteTradingRuleUseCase(store, new Clock());
        Assert.Equal(TradingRuleWriteStatus.MissingRule,
            (await useCase.ExecuteAsync(new(Guid.NewGuid(), 1, TradingRuleAuthoringAction.Archive, null))).Status);
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(new(Guid.Empty, 0,
            TradingRuleAuthoringAction.Create, Definition())));
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(new(Guid.NewGuid(), 0,
            TradingRuleAuthoringAction.Create, null)));
        Assert.Empty(store.Revisions);
    }

    [Fact]
    public async Task AssessmentTransitionsPreserveRuleRevisionAndRequirePriorAssessmentIdentity()
    {
        var store = new Store();
        Guid id = Guid.NewGuid();
        var rule = (await new WriteTradingRuleUseCase(store, new Clock()).ExecuteAsync(
            new(id, 0, TradingRuleAuthoringAction.Create, Definition()))).Value!;
        var target = TradingRuleAssessmentTarget.ForTrade(Guid.NewGuid(), Account);
        var useCase = new RecordTradingRuleAssessmentUseCase(store, store, new Clock());
        var first = await useCase.ExecuteAsync(AssessmentCommand(id, 1, target));
        Assert.Equal(TradingRuleWriteStatus.Saved, first.Status);
        Assert.Same(rule, first.Value!.RuleRevision);
        Assert.Equal(TradingRuleWriteStatus.Conflict,
            (await useCase.ExecuteAsync(AssessmentCommand(id, 1, target))).Status);
        var second = await useCase.ExecuteAsync(AssessmentCommand(id, 1, target,
            TradingRuleAssessmentOutcome.NoViolation, first.Value.Id,
            TradingRuleEvidenceSufficiency.Sufficient,
            [new(TradingRuleEvidenceKind.Execution, Guid.NewGuid())]));
        Assert.Equal(TradingRuleWriteStatus.Saved, second.Status);
        Assert.Equal(first.Value.Id, second.Value!.SupersedesAssessmentId);
        Assert.Equal(TradingRuleAssessmentOutcome.CannotEvaluate, first.Value.Outcome);
        Assert.Equal(2, store.Assessments.Count);
    }

    [Fact]
    public async Task MissingFactsOrSetupAssociationAloneCannotBeRecordedAsPass()
    {
        var store = new Store();
        Guid id = Guid.NewGuid();
        await new WriteTradingRuleUseCase(store, new Clock()).ExecuteAsync(
            new(id, 0, TradingRuleAuthoringAction.Create, Definition()));
        var target = TradingRuleAssessmentTarget.ForTrade(Guid.NewGuid(), Account);
        var useCase = new RecordTradingRuleAssessmentUseCase(store, store, new Clock());
        Assert.Equal(TradingRuleWriteStatus.MissingEvidence,
            (await useCase.ExecuteAsync(AssessmentCommand(id, 1, target,
                TradingRuleAssessmentOutcome.NoViolation))).Status);
        Assert.Equal(TradingRuleWriteStatus.MissingEvidence,
            (await useCase.ExecuteAsync(AssessmentCommand(id, 1, target,
                TradingRuleAssessmentOutcome.NoViolation, sufficiency: TradingRuleEvidenceSufficiency.Sufficient,
                references: [new(TradingRuleEvidenceKind.PlaybookCapture, Guid.NewGuid())]))).Status);
        Assert.Empty(store.Assessments);
    }

    [Fact]
    public async Task AccountScopeAndNewRuleRevisionCannotBeSilentlySubstituted()
    {
        var store = new Store();
        Guid id = Guid.NewGuid();
        await new WriteTradingRuleUseCase(store, new Clock()).ExecuteAsync(
            new(id, 0, TradingRuleAuthoringAction.Create, Definition()));
        var useCase = new RecordTradingRuleAssessmentUseCase(store, store, new Clock());
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(AssessmentCommand(id, 1,
            TradingRuleAssessmentTarget.ForTrade(Guid.NewGuid(), Guid.NewGuid()))));
        await new WriteTradingRuleUseCase(store, new Clock()).ExecuteAsync(
            new(id, 1, TradingRuleAuthoringAction.Revise, Definition()));
        Assert.Equal(TradingRuleWriteStatus.Conflict,
            (await useCase.ExecuteAsync(AssessmentCommand(id, 1,
                TradingRuleAssessmentTarget.ForTrade(Guid.NewGuid(), Account)))).Status);
        Assert.Empty(store.Assessments);
    }

    [Fact]
    public async Task CancellationAndCommitRaceDoNotLeavePartialHistory()
    {
        var store = new Store();
        Guid id = Guid.NewGuid();
        var author = new WriteTradingRuleUseCase(store, new Clock());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => author.ExecuteAsync(
            new(id, 0, TradingRuleAuthoringAction.Create, Definition()), cancellation.Token));
        Assert.Empty(store.Revisions);
        await author.ExecuteAsync(new(id, 0, TradingRuleAuthoringAction.Create, Definition()));
        store.RejectAssessmentAppend = true;
        var recorder = new RecordTradingRuleAssessmentUseCase(store, store, new Clock());
        var result = await recorder.ExecuteAsync(AssessmentCommand(id, 1,
            TradingRuleAssessmentTarget.ForTrade(Guid.NewGuid(), Account)));
        Assert.Equal(TradingRuleWriteStatus.Conflict, result.Status);
        Assert.Empty(store.Assessments);
    }

    [Fact]
    public async Task ManualAggregateDayAndExactDayHaveIndependentCorrectionChainsAndCancelledAppendIsAbsent()
    {
        var store = new Store(); var id = Guid.NewGuid();
        var definition = new TradingRuleDefinition("Review day", "Evaluate recorded observations", "Daily facts",
            TradingRuleApplicability.NewYorkDay, TradingRuleAccountScope.AllAccounts, TradingRuleEvaluationMode.ManualOnly);
        await new WriteTradingRuleUseCase(store, new Clock()).ExecuteAsync(new(id, 0, TradingRuleAuthoringAction.Create, definition));
        var writer = new RecordTradingRuleAssessmentUseCase(store, store, new Clock());
        var date = new DateOnly(2026, 11, 1);
        var aggregate = TradingRuleAssessmentTarget.ForNewYorkDay(date, TradingRuleAccountScope.AllAccounts);
        var exact = TradingRuleAssessmentTarget.ForNewYorkDay(date, TradingRuleAccountScope.ForAccount(Account));
        var first = await writer.ExecuteAsync(AssessmentCommand(id, 1, aggregate));
        var separate = await writer.ExecuteAsync(AssessmentCommand(id, 1, exact));
        Assert.Equal(TradingRuleWriteStatus.Saved, separate.Status); Assert.Null(separate.Value!.SupersedesAssessmentId);
        Assert.Equal(TradingRuleWriteStatus.Conflict, (await writer.ExecuteAsync(AssessmentCommand(id, 1, exact, previous: first.Value!.Id))).Status);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.ExecuteAsync(AssessmentCommand(id, 1, aggregate, previous: first.Value.Id), new(true)));
        Assert.Equal(2, store.Assessments.Count);
        Assert.All(store.Assessments, a => Assert.Equal(TradingRuleAssessmentSource.Manual, a.Source));
        Assert.Equal(TradingRuleAccountScopeKind.AllAccounts, first.Value.Target.AccountScope.Kind);
    }

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Store : ITradingRuleStore, ITradingRuleAssessmentStore
    {
        public List<TradingRuleRevision> Revisions { get; } = [];
        public List<TradingRuleAssessment> Assessments { get; } = [];
        public bool RejectRuleAppend { get; set; }
        public bool RejectAssessmentAppend { get; set; }

        public Task<TradingRuleRevision?> GetLatestAsync(Guid ruleId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Revisions.LastOrDefault(item => item.RuleId == ruleId));
        }

        public Task<bool> TryAppendAsync(TradingRuleRevision candidate, int expectedRevision,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (RejectRuleAppend || (Revisions.LastOrDefault(item => item.RuleId == candidate.RuleId)?.Revision ?? 0) != expectedRevision)
                return Task.FromResult(false);
            Revisions.Add(candidate);
            return Task.FromResult(true);
        }

        public Task<TradingRuleAssessment?> GetLatestAsync(Guid ruleId, TradingRuleAssessmentTarget target,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Assessments.LastOrDefault(item => item.RuleRevision.RuleId == ruleId && item.Target == target));
        }

        public Task<bool> TryAppendAsync(TradingRuleAssessment candidate, int expectedRuleRevision,
            Guid? expectedLatestAssessmentId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (RejectAssessmentAppend ||
                Revisions.LastOrDefault(item => item.RuleId == candidate.RuleRevision.RuleId)?.Revision != expectedRuleRevision ||
                (Assessments.LastOrDefault(item => item.RuleRevision.RuleId == candidate.RuleRevision.RuleId &&
                                                   item.Target == candidate.Target)?.Id) != expectedLatestAssessmentId)
                return Task.FromResult(false);
            Assessments.Add(candidate);
            return Task.FromResult(true);
        }
    }
}
