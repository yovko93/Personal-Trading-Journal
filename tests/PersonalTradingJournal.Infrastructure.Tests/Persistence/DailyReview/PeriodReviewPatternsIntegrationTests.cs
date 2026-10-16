using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.PeriodReviews;
using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Rules;
using PersonalTradingJournal.Infrastructure.PeriodReviews;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.DailyReview;

public sealed partial class DailyReviewEvidenceReaderTests
{
    [Fact]
    public async Task PeriodPatternsSqliteCorrectionsAutomaticOriginsAndExactDayScopesRemainSeparate()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (a, instrument) = await References(db); var (b, _) = await References(db, "Other");
        var first = TradeFact(a, instrument, Close, commission: null);
        var other = TradeFact(b, instrument, Close);
        await Add(db, first, other);
        var rule = TradingRuleRevision.Create(Guid.NewGuid(), new("Synthetic loss", automaticCriterion:
            new(RuleProfitBasis.StrictNet, 100, "USD")), Audit);
        Assert.True(await db.ServiceProvider.GetRequiredService<ITradingRuleStore>().TryAppendAsync(rule, 0));
        var target = TradingRuleAssessmentTarget.ForTrade(first.Id, a);
        var run = await db.ServiceProvider.GetRequiredService<IAutomaticRuleChecks>().RunAsync(target);
        var autoId = Assert.Single(run.AssessmentIds);
        var correction = PeriodAssessment(rule, target, TradingRuleAssessmentOutcome.ConfirmedViolation, [], autoId);
        var store = db.ServiceProvider.GetRequiredService<ITradingRuleAssessmentStore>();
        Assert.True(await store.TryAppendAsync(correction, 1, autoId));
        // Same evidence rerun does not supersede the later manual assessment.
        Assert.Equal(0, (await db.ServiceProvider.GetRequiredService<IAutomaticRuleChecks>().RunAsync(target)).Recorded);
        foreach (var scope in new[] { TradingRuleAccountScope.AllAccounts, TradingRuleAccountScope.ForAccount(a), TradingRuleAccountScope.ForAccount(b) })
            Assert.True(await store.TryAppendAsync(PeriodAssessment(rule, TradingRuleAssessmentTarget.ForNewYorkDay(Day, scope),
                TradingRuleAssessmentOutcome.CannotEvaluate, []), 1, null));
        foreach (Guid? account in new Guid?[] { null, a, b })
        {
            var observer = new ReadObserver(); var reader = await ObservedPeriod(db, observer);
            var e = Ready(await reader.ReadAsync(new(ReviewPeriodKind.Week, Day, account)));
            int reads = observer.Sql.Count;
            var p = Patterns(e);
            Assert.Equal(reads, observer.Sql.Count); Assert.Equal(0, observer.NonReads); Assert.False(observer.Tracked);
            Assert.Equal(account is null ? 3 : 1, p.Rules.Where(r => r.TargetKind == TradingRuleTargetKind.NewYorkDay).Sum(r => r.CannotEvaluate));
            Assert.Equal(7, p.Rules.Single(r => r.TargetKind == TradingRuleTargetKind.NewYorkDay && r.Denominator == PeriodRuleDenominator.CurrentAssessmentOpportunities).TargetCount);
            if (account != b)
            {
                var row = p.Rules.Single(r => r.TargetKind == TradingRuleTargetKind.Trade && r.Scope.AccountId == a);
                Assert.Equal(1, row.ConfirmedViolation); Assert.Equal(1, row.LatestManual);
                var history = Assert.Single(row.Targets).History;
                Assert.Equal(2, history.Count); Assert.Equal(TradingRuleAssessmentOutcome.CannotEvaluate, history[0].Assessment.Outcome);
                Assert.Equal(TradingRuleAssessmentSource.AutomaticCandidate, history[0].Assessment.Source);
                Assert.NotNull(history[0].Evaluation); Assert.Equal(autoId, history[1].Assessment.SupersedesAssessmentId);
            }
            if (account != a) Assert.Equal(1, p.Rules.Single(r => r.TargetKind == TradingRuleTargetKind.Trade && r.Scope.AccountId == b).NotAssessed);
        }
    }

    [Fact]
    public async Task PeriodPatternsSqliteArchivedAndSupersededVersionsDeletedSourcesAndRestart()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db); var trade = TradeFact(account, instrument, Close); await Add(db, trade);
        var rule = TradingRuleRevision.Create(Guid.NewGuid(), new("Original principle"), Audit);
        var rules = db.ServiceProvider.GetRequiredService<ITradingRuleStore>(); Assert.True(await rules.TryAppendAsync(rule, 0));
        var pass = PeriodAssessment(rule, TradingRuleAssessmentTarget.ForTrade(trade.Id, account), TradingRuleAssessmentOutcome.NoViolation,
            [new(TradingRuleEvidenceKind.Trade, trade.Id)]);
        Assert.True(await db.ServiceProvider.GetRequiredService<ITradingRuleAssessmentStore>().TryAppendAsync(pass, 1, null));
        var next = rule.Revise(1, new("Revised"), Close.AddDays(1)); Assert.True(await rules.TryAppendAsync(next, 1));
        var query = new PeriodReviewQuery(ReviewPeriodKind.Week, Day);
        var revised = Patterns(Ready(await Period(db).ReadAsync(query)));
        Assert.Equal(1, revised.Rules.Single(r => r.Rule.Revision == 1).Compliant);
        Assert.Equal(PeriodRuleVersionState.SupersededGlobal, revised.Rules.Single(r => r.Rule.Revision == 1).VersionState);
        Assert.Equal(1, revised.Rules.Single(r => r.Rule.Revision == 2 && r.TargetKind == TradingRuleTargetKind.Trade).NotAssessed);
        await db.ServiceProvider.GetRequiredService<ITradeDeletionStore>().DeleteAsync(trade.Id);
        await db.ServiceProvider.GetRequiredService<ITradingAccountDeletionStore>().DeleteAsync(account);
        await References(db); // Same display name must not replace historical identity.
        Assert.True(await rules.TryAppendAsync(next.Archive(2, Close.AddDays(2)), 2));
        var p = Patterns(Ready(await Period(db).ReadAsync(query)));
        var retained = Assert.Single(p.Rules);
        Assert.Equal(PeriodRuleVersionState.NotInActiveGlobalCatalog, retained.VersionState);
        Assert.Equal(PeriodRuleDenominator.RecordedTargetsOnly, retained.Denominator);
        Assert.Equal(account, retained.Scope.AccountId); Assert.Equal(1, retained.UnavailableTargets);
        Assert.Equal(1, retained.TargetsWithChangedOrUnavailableEvidence); Assert.Equal(1, retained.Compliant);
        // Recreate all services against the SAME isolated path, with fresh contexts/readers.
        string syntheticPath;
        await using (var context = await db.ContextFactory.CreateDbContextAsync())
            syntheticPath = context.Database.GetDbConnection().DataSource;
        var paths = new PersonalTradingJournal.Infrastructure.Storage.LocalApplicationPaths(Path.GetDirectoryName(Path.GetDirectoryName(syntheticPath))!);
        Assert.Equal(syntheticPath, paths.DatabasePath);
        var services = new ServiceCollection(); services.AddPersistence(paths);
        await using var restarted = services.BuildServiceProvider();
        var readback = Ready(await restarted.GetRequiredService<IPeriodReviewEvidenceReader>().ReadAsync(query));
        Assert.Equal(JsonSerializer.Serialize(p.Rules), JsonSerializer.Serialize(Patterns(readback).Rules));
    }

    [Fact]
    public async Task PeriodPatternsSqliteRecordedMistakesRemainReadOnlyScopedAndDeterministic()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (a, instrument) = await References(db); var (b, _) = await References(db, "Inactive", false);
        var one = TradeFact(a, instrument, Close); var two = TradeFact(b, instrument, Close, currency: "EUR");
        var partial = TradeFact(a, instrument, Close, exitQuantity: 1);
        await Add(db, one, two, partial);
        var mistake = Guid.NewGuid();
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            ctx.TradingMistakes.Add(new() { Id = mistake, Name = "Recorded observation", IsActive = false, CreatedAtUtc = Audit, UpdatedAtUtc = Audit });
            foreach (var t in new[] { one, two, partial }) ctx.TradeMistakes.Add(new() { Id = Guid.NewGuid(), TradeId = t.Id,
                TradingMistakeId = mistake, CreatedAtUtc = Audit, UpdatedAtUtc = Audit });
            await ctx.SaveChangesAsync();
        }
        foreach (Guid? account in new Guid?[] { null, a, b })
        {
            var query = new PeriodReviewQuery(ReviewPeriodKind.Month, Day, account);
            var observer = new ReadObserver(); var reader = await ObservedPeriod(db, observer);
            var e = Ready(await reader.ReadAsync(query)); int reads = observer.Sql.Count;
            var p = Patterns(e); var summary = Assert.Single(p.Mistakes);
            Assert.Equal(account is null ? 3 : account == a ? 2 : 1, summary.Frequency);
            Assert.Equal(account is null ? 2 : 1, summary.ClosedFrequency);
            Assert.Equal(account == b ? 0 : 1, summary.ActivityFrequency);
            Assert.False(summary.Mistake.IsActive); Assert.Same(e, p.Evidence);
            Assert.Equal(reads, observer.Sql.Count); Assert.Equal(0, observer.NonReads);
            var fresh = Patterns(Ready(await new PeriodReviewEvidenceReader(db.ContextFactory).ReadAsync(query)));
            Assert.Equal(JsonSerializer.Serialize(p.Mistakes), JsonSerializer.Serialize(fresh.Mistakes));
        }
    }

    [Fact]
    public async Task PeriodPatternsKeepOneSqliteSnapshotWhileAssessmentAndMistakeAreAppended()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db); var trade = TradeFact(account, instrument, Close); await Add(db, trade);
        var rule = TradingRuleRevision.Create(Guid.NewGuid(), new("Synthetic principle"), Audit);
        Assert.True(await db.ServiceProvider.GetRequiredService<ITradingRuleStore>().TryAppendAsync(rule, 0));
        var observer = new ReadObserver(); var reader = await ObservedPeriod(db, observer);
        observer.BeforeRead = async count =>
        {
            if (count != 3) return;
            Assert.True(await db.ServiceProvider.GetRequiredService<ITradingRuleAssessmentStore>().TryAppendAsync(
                PeriodAssessment(rule, TradingRuleAssessmentTarget.ForTrade(trade.Id, account), TradingRuleAssessmentOutcome.ConfirmedViolation, []), 1, null));
            await using var writer = await db.ContextFactory.CreateDbContextAsync();
            var id = Guid.NewGuid();
            writer.TradingMistakes.Add(new() { Id = id, Name = "Later observation", IsActive = true, CreatedAtUtc = Audit, UpdatedAtUtc = Audit });
            writer.TradeMistakes.Add(new() { Id = Guid.NewGuid(), TradeId = trade.Id, TradingMistakeId = id, CreatedAtUtc = Audit, UpdatedAtUtc = Audit });
            await writer.SaveChangesAsync();
        };
        var query = new PeriodReviewQuery(ReviewPeriodKind.Week, Day);
        var before = Patterns(Ready(await reader.ReadAsync(query)));
        Assert.Empty(before.Mistakes); Assert.Equal(1, before.Rules.Single(r => r.TargetKind == TradingRuleTargetKind.Trade).NotAssessed);
        observer.BeforeRead = null;
        var after = Patterns(Ready(await reader.ReadAsync(query)));
        Assert.Single(after.Mistakes); Assert.Equal(1, after.Rules.Single(r => r.TargetKind == TradingRuleTargetKind.Trade).ConfirmedViolation);
        Assert.Equal(0, observer.NonReads); Assert.False(observer.Tracked);
    }

    private static PeriodReviewPatterns Patterns(PeriodReviewEvidence e)
    {
        var result = PeriodReviewPatternsCalculator.Calculate(e);
        Assert.Equal(PeriodPatternsStatus.Ready, result.Status); return result.Patterns!;
    }
}
