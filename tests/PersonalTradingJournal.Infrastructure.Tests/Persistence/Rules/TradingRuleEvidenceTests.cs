using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Rules;
using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Rules;

public sealed partial class TradingRulePersistenceTests
{
    [Fact]
    public async Task RecordedPassSurvivesRestartAndAutomaticSourceRequiresExplicitEligibility()
    {
        await using var f = await Fixture.Create(); var trade = await f.Seed(); var rule = await f.Rule(Definition());
        var target = TradingRuleAssessmentTarget.ForTrade(trade.Id, trade.TradingAccountId);
        TradingRuleAssessment Candidate(TradingRuleAssessmentSource source) => TradingRuleAssessment.Record(Guid.NewGuid(), rule, target,
            TradingRuleAssessmentOutcome.NoViolation, source, TradingRuleAssessmentBasis.RecordedEvidence,
            TradingRuleEvidenceSufficiency.Sufficient, "Execution facts were manually evaluated for this entry criterion.",
            [new(TradingRuleEvidenceKind.Execution, trade.Executions[0].Id)], null, Now.AddDays(1));
        Assert.False(await f.Assessments.TryAppendAsync(Candidate(TradingRuleAssessmentSource.AutomaticCandidate), 1, null));
        var manual = Candidate(TradingRuleAssessmentSource.Manual);
        Assert.True(await f.Assessments.TryAppendAsync(manual, 1, null)); await f.Restart();
        Assert.Equal(TradingRuleAssessmentOutcome.NoViolation, (await f.Reader.ReadAssessmentAsync(manual.Id))!.Assessment.Outcome);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Assessments.TryAppendAsync(manual, 1, null, cancelled.Token));
    }

    [Theory]
    [InlineData(3, 8, 6, 59, 7, 1)]
    [InlineData(11, 1, 5, 59, 6, 1)]
    [InlineData(11, 1, 3, 59, 4, 1)]
    public async Task DayReferencesUseNewYorkClosureDateAcrossDstAndMidnight(int month, int day,
        int entryHour, int entryMinute, int closeHour, int closeMinute)
    {
        await using var f = await Fixture.Create();
        var date = new DateOnly(2026, month, day);
        var trade = await f.Seed(entry: new(2026, month, day, entryHour, entryMinute, 0, TimeSpan.Zero),
            close: new(2026, month, day, closeHour, closeMinute, 0, TimeSpan.Zero));
        var rule = await f.Rule(Definition(applicability: TradingRuleApplicability.NewYorkDay));
        var source = new TradingRuleEvidenceReference(TradingRuleEvidenceKind.Trade, trade.Id);
        var good = Assess(rule, TradingRuleAssessmentTarget.ForNewYorkDay(date, TradingRuleAccountScope.AllAccounts), null, source);
        Assert.True(await f.Assessments.TryAppendAsync(good, 1, null));
        var wrong = Assess(rule, TradingRuleAssessmentTarget.ForNewYorkDay(date.AddDays(-1), TradingRuleAccountScope.AllAccounts), null, source);
        Assert.False(await f.Assessments.TryAppendAsync(wrong, 1, null));
        Assert.Equal(date, Assert.Single((await f.Reader.ReadAssessmentAsync(good.Id))!.Evidence).NewYorkDate);
    }

    [Fact]
    public async Task AggregateDayRetainsMultipleCurrenciesAndNullJournalWhileExactScopeRejectsOtherSources()
    {
        await using var f = await Fixture.Create(); var usd = await f.Seed(); var eur = await f.Seed("EUR", "EUR");
        var day = new DateOnly(2026, 11, 1);
        var journal = (await f.Get<IDailyJournalRepository>().CreateAsync(new(day, null, "Synthetic aggregate observation"))).Journal!.Entry;
        var rule = await f.Rule(Definition(applicability: TradingRuleApplicability.NewYorkDay));
        var refs = new TradingRuleEvidenceReference[] { new(TradingRuleEvidenceKind.Trade, usd.Id),
            new(TradingRuleEvidenceKind.Trade, eur.Id), new(TradingRuleEvidenceKind.Journal, journal.Id, journal.Revision) };
        var aggregate = Assess(rule, TradingRuleAssessmentTarget.ForNewYorkDay(day, TradingRuleAccountScope.AllAccounts), null, refs);
        Assert.True(await f.Assessments.TryAppendAsync(aggregate, 1, null));
        var read = (await f.Reader.ReadAssessmentAsync(aggregate.Id))!;
        Assert.Equal(new string?[] { "USD", "EUR", null }, read.Evidence.Select(e => e.Currency));
        Assert.Null(read.Evidence[2].AccountId);
        Assert.Equal(TradingRuleEvidenceSufficiency.Missing, read.Assessment.EvidenceSufficiency);
        var exact = Assess(rule, TradingRuleAssessmentTarget.ForNewYorkDay(day, TradingRuleAccountScope.ForAccount(usd.TradingAccountId)), null, refs);
        Assert.False(await f.Assessments.TryAppendAsync(exact, 1, null));
        var unknown = Assess(rule, aggregate.Target, aggregate.Id, new TradingRuleEvidenceReference(TradingRuleEvidenceKind.Trade, Guid.NewGuid()));
        Assert.False(await f.Assessments.TryAppendAsync(unknown, 1, aggregate.Id));
        await using var db = f.Context(); Assert.Single(await db.TradingRuleAssessments.ToArrayAsync());
        Assert.Null((await db.TradeExecutions.OrderBy(e => e.Sequence).FirstAsync()).Commission);
    }

    [Fact]
    public async Task SourceEditMoveAndDeleteAreReportedWithoutRewritingAssessment()
    {
        await using var f = await Fixture.Create(); var trade = await f.Seed(); var other = await f.Seed("Other");
        var rule = await f.Rule(Definition()); var target = TradingRuleAssessmentTarget.ForTrade(trade.Id, trade.TradingAccountId);
        var assessment = Assess(rule, target, null, new TradingRuleEvidenceReference(TradingRuleEvidenceKind.Trade, trade.Id));
        Assert.True(await f.Assessments.TryAppendAsync(assessment, 1, null));
        await using (var db = f.Context())
            await db.Trades.Where(t => t.Id == trade.Id).ExecuteUpdateAsync(s => s.SetProperty(t => t.TradingAccountId, other.TradingAccountId));
        var read = (await f.Reader.ReadAssessmentAsync(assessment.Id))!;
        Assert.Equal(TradingRuleSourceState.Changed, read.TargetState);
        Assert.Equal(TradingRuleSourceState.Changed, Assert.Single(read.Evidence).State);
        Assert.Equal(trade.TradingAccountId, read.Assessment.Target.AccountScope.AccountId);
        Assert.Equal(trade.TradingAccountId, read.Evidence[0].AccountId);
        Assert.False(await f.Assessments.TryAppendAsync(Assess(rule, target, assessment.Id), 1, assessment.Id));
    }

    [Fact]
    public async Task EmptyDayAssessmentAndDeletedJournalRevisionRemainReadable()
    {
        await using var f = await Fixture.Create(); var date = new DateOnly(2026, 11, 1);
        var rule = await f.Rule(Definition(applicability: TradingRuleApplicability.NewYorkDay));
        var target = TradingRuleAssessmentTarget.ForNewYorkDay(date, TradingRuleAccountScope.AllAccounts);
        Assert.Null(await f.Assessments.GetLatestAsync(rule.RuleId, target)); // Not assessed is absence.
        var repository = f.Get<IDailyJournalRepository>();
        var journal = (await repository.CreateAsync(new(date, null, "Synthetic"))).Journal!.Entry;
        var assessment = Assess(rule, target, null, new TradingRuleEvidenceReference(TradingRuleEvidenceKind.Journal, journal.Id, 1));
        Assert.True(await f.Assessments.TryAppendAsync(assessment, 1, null));
        await repository.UpdateAsync(new(journal.Id, 1, "New text"));
        Assert.Equal(TradingRuleSourceState.Available, Assert.Single((await f.Reader.ReadAssessmentAsync(assessment.Id))!.Evidence).State);
        await repository.DeleteAsync(new(journal.Id, 2));
        Assert.Equal(TradingRuleSourceState.Unavailable, Assert.Single((await f.Reader.ReadAssessmentAsync(assessment.Id))!.Evidence).State);
        Assert.Equal(journal.Id, (await f.Assessments.GetLatestAsync(rule.RuleId, target))!.EvidenceReferences[0].SourceId);
        await using var db = f.Context(); Assert.Empty(await db.Trades.ToArrayAsync());
    }

    [Fact]
    public async Task PlaybookSourceNamesItsExactAssociationSequenceEvenAfterRelinking()
    {
        await using var f = await Fixture.Create(); var trade = await f.Seed();
        var setup = new TradingSetup("Synthetic plan", null, Now);
        await f.Get<ITradingSetupStore>().AddAsync(setup);
        await f.Get<SaveTradingSetupPlaybookUseCase>().ExecuteAsync(new(setup.Id, 0, new("context", "entry", "confirm", "invalidate", "execute")));
        var associations = f.Get<ITradePlaybookAssociationStore>();
        Assert.True(await associations.TryApplyAsync(await associations.PreviewAsync(trade.Id, setup.Id)));
        var rule = await f.Rule(Definition());
        var assessment = Assess(rule, TradingRuleAssessmentTarget.ForTrade(trade.Id, trade.TradingAccountId), null,
            new TradingRuleEvidenceReference(TradingRuleEvidenceKind.PlaybookCapture, trade.Id, 1));
        Assert.True(await f.Assessments.TryAppendAsync(assessment, 1, null));
        Assert.True(await associations.TryApplyAsync(await associations.PreviewAsync(trade.Id, null)));
        var read = (await f.Reader.ReadAssessmentAsync(assessment.Id))!;
        Assert.Equal(1, Assert.Single(read.Evidence).Reference.Revision);
        Assert.Equal(TradingRuleSourceState.Available, read.Evidence[0].State);
        Assert.Equal(TradingRuleAssessmentOutcome.CannotEvaluate, read.Assessment.Outcome);
        Assert.False(await f.Assessments.TryAppendAsync(Assess(rule, assessment.Target, assessment.Id,
            new TradingRuleEvidenceReference(TradingRuleEvidenceKind.PlaybookCapture, trade.Id, 2)), 1, assessment.Id)); // Removal is not a capture.
    }
}
