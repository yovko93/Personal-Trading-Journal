using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Domain.Rules;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Rules;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Rules;

public sealed partial class TradingRulePersistenceTests
{
    private static TradingRuleDefinition AutomaticDefinition(RuleProfitBasis basis = RuleProfitBasis.Gross,
        Guid? account = null, TradingRuleApplicability target = TradingRuleApplicability.Trade) =>
        new("Synthetic loss limit", "No expression interpretation", "Complete closed recorded economics", target,
            account is Guid id ? TradingRuleAccountScope.ForAccount(id) : TradingRuleAccountScope.AllAccounts,
            TradingRuleEvaluationMode.AutomaticCandidate, new(basis, 5, "USD"));

    [Fact]
    public async Task AutomaticChecksRestartIdempotencyChangedEvidenceAndManualHistory()
    {
        await using var f = await Fixture.Create(); var trade = await f.Seed(); var rule = await f.Rule(AutomaticDefinition());
        await f.Rule(Definition()); // No typed criterion must remain unassessed.
        var target = TradingRuleAssessmentTarget.ForTrade(trade.Id, trade.TradingAccountId);
        var checks = f.Get<IAutomaticRuleChecks>();
        var first = await checks.RunAsync(target); Assert.Equal(1, first.Recorded);
        var again = await checks.RunAsync(target); Assert.Equal(0, again.Recorded); Assert.Equal(first.AssessmentIds, again.AssessmentIds);
        var saved = (await f.Reader.ReadAssessmentAsync(first.AssessmentIds[0]))!;
        Assert.Equal(10m, saved.Evaluation!.Calculation.Value); Assert.Equal(AutomaticRuleCalculator.Version, saved.Evaluation.EvaluatorVersion);
        Assert.Equal(trade.Executions.Select(e => e.Id), saved.Evaluation.ExecutionIds);
        var manual = TradingRuleAssessment.Record(Guid.NewGuid(), rule, target, TradingRuleAssessmentOutcome.CannotEvaluate,
            TradingRuleAssessmentSource.Manual, TradingRuleAssessmentBasis.RecordedEvidence, TradingRuleEvidenceSufficiency.Missing,
            "Manual observation retained", [], saved.Assessment.Id, Now);
        Assert.True(await f.Assessments.TryAppendAsync(manual, 1, saved.Assessment.Id));
        Assert.Equal(1, (await checks.RunAsync(target)).Unchanged);
        Assert.Equal(manual.Id, (await f.Assessments.GetLatestAsync(rule.RuleId, target))!.Id);
        await using (var db = f.Context())
        { var exit = await db.TradeExecutions.SingleAsync(e => e.TradeId == trade.Id && e.Sequence == 2); exit.Price = 90; await db.SaveChangesAsync(); }
        var changed = await checks.RunAsync(target); Assert.Equal(1, changed.Recorded);
        await f.Restart();
        var history = await f.Reader.ReadAssessmentsAsync(rule.RuleId, target);
        Assert.Equal(3, history.Count); Assert.Equal(TradingRuleAssessmentOutcome.ConfirmedViolation, history[0].Assessment.Outcome);
        Assert.Equal(-10m, history[0].Evaluation!.Calculation.Value); Assert.Equal(10m, history[2].Evaluation!.Calculation.Value);
        Assert.Equal(TradingRuleSourceState.Changed, Assert.Single(history[2].Evidence).State);
    }

    [Theory]
    [InlineData(3, 8, 6, 59)] [InlineData(3, 8, 7, 0)]
    [InlineData(11, 1, 5, 30)] [InlineData(11, 1, 6, 30)]
    public async Task DayChecksUseNyClosureAcrossMidnightAndBothDstTransitions(int month, int date, int hour, int minute)
    {
        await using var f = await Fixture.Create();
        var closed = new DateTimeOffset(2026, month, date, hour, minute, 0, TimeSpan.Zero);
        var trade = await f.Seed(entry: closed.AddDays(-1), close: closed);
        await f.Rule(AutomaticDefinition(target: TradingRuleApplicability.NewYorkDay));
        await f.Rule(AutomaticDefinition(RuleProfitBasis.StrictNet, target: TradingRuleApplicability.NewYorkDay));
        await f.Seed("Other", "EUR", closed.AddMinutes(-1), closed);
        var target = TradingRuleAssessmentTarget.ForNewYorkDay(new(2026, month, date), TradingRuleAccountScope.AllAccounts);
        var run = await f.Get<IAutomaticRuleChecks>().RunAsync(target); Assert.Equal(2, run.Recorded);
        var results = await Task.WhenAll(run.AssessmentIds.Select(id => f.Reader.ReadAssessmentAsync(id)));
        var gross = results.Single(r => r!.Evaluation!.Criterion.Basis == RuleProfitBasis.Gross)!;
        var net = results.Single(r => r!.Evaluation!.Criterion.Basis == RuleProfitBasis.StrictNet)!;
        Assert.Equal(10m, gross.Evaluation!.Calculation.Value); Assert.Equal(1, gross.Evaluation.Calculation.OtherCurrencyTrades);
        Assert.Equal(TradingRuleAssessmentOutcome.CannotEvaluate, net.Assessment.Outcome);
        Assert.Equal(1, net.Evaluation!.Calculation.MissingCommissions);
        var exact = target with { }; // Target is immutable; create the explicit exact scope.
        exact = TradingRuleAssessmentTarget.ForNewYorkDay(target.NewYorkDate!.Value, TradingRuleAccountScope.ForAccount(trade.TradingAccountId));
        var exactRun = await f.Get<IAutomaticRuleChecks>().RunAsync(exact);
        Assert.All(await Task.WhenAll(exactRun.AssessmentIds.Select(id => f.Reader.ReadAssessmentAsync(id))), r =>
            Assert.Equal(trade.TradingAccountId, Assert.Single(r!.Evaluation!.Facts).AccountId));
        var previous = await f.Get<IAutomaticRuleChecks>().RunAsync(TradingRuleAssessmentTarget.ForNewYorkDay(target.NewYorkDate.Value.AddDays(-1), TradingRuleAccountScope.AllAccounts));
        Assert.All(await Task.WhenAll(previous.AssessmentIds.Select(id => f.Reader.ReadAssessmentAsync(id))), r => Assert.Empty(r!.Evaluation!.Facts));
    }

    [Fact]
    public async Task ConcurrentRunsAppendOnceAndCancellationRollsBackWholeBatch()
    {
        await using var f = await Fixture.Create(); var trade = await f.Seed(); await f.Rule(AutomaticDefinition());
        var target = TradingRuleAssessmentTarget.ForTrade(trade.Id, trade.TradingAccountId);
        var runs = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(() => f.Get<IAutomaticRuleChecks>().RunAsync(target))));
        Assert.Equal(1, runs.Sum(r => r.Recorded)); Assert.Equal(1, runs.Sum(r => r.Unchanged));
        await f.Rule(AutomaticDefinition(RuleProfitBasis.StrictNet));
        using var cancel = new CancellationTokenSource();
        var options = new DbContextOptionsBuilder<JournalDbContext>().UseSqlite($"Data Source={f.Paths.DatabasePath};Foreign Keys=True").AddInterceptors(new CancelSaved(cancel)).Options;
        var repo = new TradingRuleRepository(new Factory(options), new Clock());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repo.RunAsync(target, cancel.Token));
        await using var db = f.Context(); Assert.Single(await db.TradingRuleAssessments.ToArrayAsync());
    }

    [Fact]
    public async Task EmptyDayUnknownTargetAndArchivedRulesHaveDistinctOutcomes()
    {
        await using var f = await Fixture.Create(); var trade = await f.Seed();
        var rule = await f.Rule(AutomaticDefinition(target: TradingRuleApplicability.NewYorkDay));
        var checks = f.Get<IAutomaticRuleChecks>();
        var result = await checks.RunAsync(TradingRuleAssessmentTarget.ForNewYorkDay(new(2020, 1, 1), TradingRuleAccountScope.AllAccounts));
        Assert.Equal(TradingRuleAssessmentOutcome.CannotEvaluate, (await f.Reader.ReadAssessmentAsync(Assert.Single(result.AssessmentIds)))!.Assessment.Outcome);
        Assert.True(await f.Rules.TryAppendAsync(rule.Archive(1, Now), 1));
        Assert.Empty((await checks.RunAsync(TradingRuleAssessmentTarget.ForNewYorkDay(new(2020, 1, 2), TradingRuleAccountScope.AllAccounts))).AssessmentIds);
        Assert.Equal(RuleCheckStatus.UnavailableTarget, (await checks.RunAsync(TradingRuleAssessmentTarget.ForTrade(Guid.NewGuid(), trade.TradingAccountId))).Status);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => checks.RunAsync(TradingRuleAssessmentTarget.ForTrade(trade.Id, trade.TradingAccountId), new(true)));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SourceOrRuleChangedBetweenPreparationAndCommitWritesNothing(bool ruleChange)
    {
        await using var f = await Fixture.Create(); var trade = await f.Seed(); var rule = await f.Rule(AutomaticDefinition());
        var options = new DbContextOptionsBuilder<JournalDbContext>().UseSqlite($"Data Source={f.Paths.DatabasePath};Foreign Keys=True").Options;
        var factory = new BetweenCheckFactory(options, () =>
        {
            if (ruleChange) f.Rules.TryAppendAsync(rule.Archive(1, Now), 1).GetAwaiter().GetResult();
            else { using var db = f.Context(); db.TradeExecutions.Single(e => e.TradeId == trade.Id && e.Sequence == 2).Price = 50; db.SaveChanges(); }
        });
        var result = await new TradingRuleRepository(factory, new Clock()).RunAsync(TradingRuleAssessmentTarget.ForTrade(trade.Id, trade.TradingAccountId));
        Assert.Equal(RuleCheckStatus.Conflict, result.Status);
        await using var check = f.Context(); Assert.Empty(await check.TradingRuleAssessments.ToArrayAsync());
    }
    private sealed class BetweenCheckFactory(DbContextOptions<JournalDbContext> options, Action change) : IDbContextFactory<JournalDbContext>
    {
        private int count;
        public JournalDbContext CreateDbContext() { if (++count == 2) change(); return new(options); }
    }

    [Fact]
    public async Task OpenPartialAndDeletedSourcesNeverBecomeCompleteNetOrAnotherAccount()
    {
        await using var f = await Fixture.Create(); var trade = await f.Seed(); var rule = await f.Rule(AutomaticDefinition(RuleProfitBasis.StrictNet));
        var target = TradingRuleAssessmentTarget.ForTrade(trade.Id, trade.TradingAccountId);
        await using (var db = f.Context())
        { var exit = await db.TradeExecutions.SingleAsync(e => e.TradeId == trade.Id && e.Sequence == 2); exit.Quantity = 0.5m; await db.SaveChangesAsync(); }
        var run = await f.Get<IAutomaticRuleChecks>().RunAsync(target);
        var old = (await f.Reader.ReadAssessmentAsync(Assert.Single(run.AssessmentIds)))!;
        Assert.Equal(TradingRuleAssessmentOutcome.CannotEvaluate, old.Assessment.Outcome);
        Assert.Equal(1, old.Evaluation!.Calculation.ExcludedActivity); Assert.Equal(0, old.Evaluation.Calculation.ContributingTrades);
        await f.Get<ITradeDeletionStore>().DeleteAsync(trade.Id); await f.Restart();
        var saved = (await f.Reader.ReadAssessmentAsync(old.Assessment.Id))!;
        Assert.Equal(TradingRuleSourceState.Unavailable, saved.TargetState);
        Assert.Equal(trade.TradingAccountId, Assert.Single(saved.Evaluation!.Facts).AccountId);
        Assert.Equal(RuleCheckStatus.UnavailableTarget, (await f.Get<IAutomaticRuleChecks>().RunAsync(target)).Status);
    }

    [Fact]
    public async Task BoundedBatchFailureWritesNoResults()
    {
        await using var f = await Fixture.Create(); var trade = await f.Seed();
        for (int i = 0; i < 101; i++) await f.Rule(AutomaticDefinition());
        var run = await f.Get<IAutomaticRuleChecks>().RunAsync(TradingRuleAssessmentTarget.ForTrade(trade.Id, trade.TradingAccountId));
        Assert.Equal(RuleCheckStatus.LimitExceeded, run.Status);
        await using var db = f.Context(); Assert.Empty(await db.TradingRuleAssessments.ToArrayAsync());
    }

    [Fact]
    public async Task MigrationRoundTripPreservesOldRulesAndAppendOnlyTriggers()
    {
        await using var f = await Fixture.Create(); var trade = await f.Seed(); var rule = await f.Rule(Definition());
        var assessment = Assess(rule, TradingRuleAssessmentTarget.ForTrade(trade.Id, trade.TradingAccountId));
        Assert.True(await f.Assessments.TryAppendAsync(assessment, 1, null));
        await using (var db = f.Context())
        {
            await db.GetService<IMigrator>().MigrateAsync("20261012163222_AddTradingRules");
            await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => db.Database.ExecuteSqlRawAsync("UPDATE TradingRuleRevisions SET Name = 'forbidden'"));
            await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM TradingRuleAssessments"));
            await db.Database.MigrateAsync();
        }
        Assert.Equal(rule.Definition, (await f.Rules.GetLatestAsync(rule.RuleId))!.Definition);
        Assert.Equal(assessment.Explanation, (await f.Reader.ReadAssessmentAsync(assessment.Id))!.Assessment.Explanation);
    }
}
