using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Application.PeriodReviews;
using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Rules;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.PeriodReviews;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.DailyReview;

public sealed partial class DailyReviewEvidenceReaderTests
{
    [Theory]
    [InlineData(ReviewPeriodKind.Week, 2026, 3, 8)]
    [InlineData(ReviewPeriodKind.Week, 2026, 11, 1)]
    [InlineData(ReviewPeriodKind.Week, 2027, 1, 1)]
    [InlineData(ReviewPeriodKind.Month, 2026, 3, 8)]
    [InlineData(ReviewPeriodKind.Month, 2026, 11, 1)]
    [InlineData(ReviewPeriodKind.Month, 2026, 12, 31)]
    public async Task PeriodClosureBoundariesKeepFullLifecycleAndSeparatePartialActivity(ReviewPeriodKind kind, int year, int month, int day)
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db);
        var query = new PeriodReviewQuery(kind, new(year, month, day), account);
        var before = TradeFact(account, instrument, query.FromUtc.AddTicks(-1));
        var first = TradeFact(account, instrument, query.FromUtc, commission: null);
        var last = TradeFact(account, instrument, query.BeforeUtc.AddTicks(-1), currency: "EUR");
        var after = TradeFact(account, instrument, query.BeforeUtc, entry: query.FromUtc); // Closed outside: not open context.
        var partial = TradeFact(account, instrument, query.FromUtc.AddHours(3), exitQuantity: 1);
        var idle = TradeFact(account, instrument, query.FromUtc, exitQuantity: 0, entry: query.FromUtc.AddDays(-2));
        await Add(db, before, first, last, after, partial, idle);
        var evidence = Ready(await Period(db).ReadAsync(query));
        Assert.Equal(new[] { last.Id, first.Id }, evidence.ClosedTrades.Select(t => t.TradeId));
        Assert.Equal(partial.Id, Assert.Single(evidence.ActivityContext).TradeId);
        Assert.Null(evidence.ActivityContext[0].Facts!.GrossPnL);
        Assert.Null(evidence.ClosedTrades[1].Facts!.NetPnL);
        Assert.Equal("EUR", evidence.ClosedTrades[0].PricingCurrency);
        Assert.Equal(first.Executions.Select(e => e.Id), evidence.ClosedTrades[1].Executions.Select(e => e.Id));
        Assert.True(first.OpenedAtUtc < query.FromUtc);
        Assert.Equal(2, evidence.ClosedTrades.Select(t => t.TradeId).Distinct().Count());
    }

    [Fact]
    public async Task PeriodAggregatePreservesExactAccountsNullScopeAndAllRetainedJournalRevisions()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (a, instrument) = await References(db, "P 21");
        var (b, _) = await References(db, "Inactive", false);
        await Add(db, TradeFact(a, instrument, Close), TradeFact(b, instrument, Close, currency: "EUR"));
        var store = db.ServiceProvider.GetRequiredService<IDailyJournalRepository>();
        var global = (await store.CreateAsync(new(Day, null, "", true))).Journal!.Entry;
        var exact = (await store.CreateAsync(new(Day, a, "First", true))).Journal!.Entry;
        await store.UpdateAsync(new(exact.Id, 1, "Current", false));
        await store.CreateAsync(new(Day.AddDays(1), b, "Other", true));
        await store.CreateAsync(new(Day.AddMonths(-1), null, "Outside", true));
        var query = new PeriodReviewQuery(ReviewPeriodKind.Month, Day);
        var all = Ready(await Period(db).ReadAsync(query));
        Assert.Equal(2, all.ClosedTrades.Count); Assert.Equal(3, all.Journals.Count);
        Assert.Equal(global.Id, all.Journals[0].JournalId); Assert.Empty(all.Journals[0].Text);
        Assert.Equal(DailyJournalAccountState.Inactive, all.Journals[2].AccountState);
        Assert.Equal(4, all.JournalRevisions.Count);
        Assert.Equal(new[] { "First", "Current" }, all.JournalRevisions.Where(r => r.JournalId == exact.Id).Select(r => r.Revision.Text));
        Assert.Equal(new long[] { 1, 2 }, all.JournalRevisions.Where(r => r.JournalId == exact.Id).Select(r => r.Revision.Revision));
        var scoped = Ready(await Period(db).ReadAsync(new(ReviewPeriodKind.Month, Day, a)));
        Assert.Equal(a, Assert.Single(scoped.ClosedTrades).Account.Id);
        Assert.Equal(exact.Id, Assert.Single(scoped.Journals).JournalId);
        Assert.Equal(2, scoped.JournalRevisions.Count);
        var empty = Ready(await Period(db).ReadAsync(new(ReviewPeriodKind.Month, Day, Guid.NewGuid())));
        Assert.Empty(empty.ClosedTrades); Assert.Empty(empty.Journals); Assert.Empty(empty.JournalRevisions);
        Assert.Equal(all.Journals.Select(j => j.JournalId), Ready(await Period(db).ReadAsync(query)).Journals.Select(j => j.JournalId));
    }

    [Fact]
    public async Task PeriodRuleCoverageRetainsCorrectionsExactRevisionOriginAndDeletedSourceIdentity()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db);
        var trade = TradeFact(account, instrument, Close);
        var undated = TradeFact(account, instrument, Close);
        await Add(db, trade, undated);
        var ruleStore = db.ServiceProvider.GetRequiredService<ITradingRuleStore>();
        var assessmentStore = db.ServiceProvider.GetRequiredService<ITradingRuleAssessmentStore>();
        var rule = TradingRuleRevision.Create(Guid.NewGuid(), new("Synthetic global principle"), Audit);
        Assert.True(await ruleStore.TryAppendAsync(rule, 0));
        var target = TradingRuleAssessmentTarget.ForTrade(trade.Id, account);
        var pass = PeriodAssessment(rule, target, TradingRuleAssessmentOutcome.NoViolation, [new(TradingRuleEvidenceKind.Trade, trade.Id)]);
        Assert.True(await assessmentStore.TryAppendAsync(pass, 1, null));
        var query = new PeriodReviewQuery(ReviewPeriodKind.Week, Day);
        Assert.Equal(RuleCoverageOutcome.Compliant, Ready(await Period(db).ReadAsync(query)).Coverage(rule, target).Outcome);
        var correction = PeriodAssessment(rule, target, TradingRuleAssessmentOutcome.ConfirmedViolation, [], pass.Id);
        Assert.True(await assessmentStore.TryAppendAsync(correction, 1, pass.Id));
        var dayTarget = TradingRuleAssessmentTarget.ForNewYorkDay(Day, TradingRuleAccountScope.AllAccounts);
        var missing = PeriodAssessment(rule, dayTarget, TradingRuleAssessmentOutcome.CannotEvaluate, []);
        Assert.True(await assessmentStore.TryAppendAsync(missing, 1, null));
        var unknown = PeriodAssessment(rule, TradingRuleAssessmentTarget.ForTrade(undated.Id, account), TradingRuleAssessmentOutcome.ConfirmedViolation, []);
        Assert.True(await assessmentStore.TryAppendAsync(unknown, 1, null));
        var next = rule.Revise(1, new("Revised principle"), Close.AddDays(1));
        Assert.True(await ruleStore.TryAppendAsync(next, 1));
        await db.ServiceProvider.GetRequiredService<ITradeDeletionStore>().DeleteAsync(trade.Id);
        await db.ServiceProvider.GetRequiredService<ITradeDeletionStore>().DeleteAsync(undated.Id);
        await db.ServiceProvider.GetRequiredService<ITradingAccountDeletionStore>().DeleteAsync(account);
        await References(db); // Same name, not the same identity.
        var evidence = Ready(await new PeriodReviewEvidenceReader(db.ContextFactory).ReadAsync(query));
        Assert.Empty(evidence.ClosedTrades);
        Assert.Equal(3, evidence.Assessments.Count);
        Assert.Equal(unknown.Id, Assert.Single(evidence.UnattributedTradeAssessments).Assessment.Id);
        Assert.Equal(RuleCoverageOutcome.ConfirmedViolation, evidence.Coverage(rule, target).Outcome);
        Assert.Equal(correction.Id, evidence.Coverage(rule, target).Latest!.Assessment.Id);
        Assert.Equal(RuleCoverageOutcome.NotAssessed, evidence.Coverage(next, target).Outcome);
        Assert.Equal(RuleCoverageOutcome.CannotEvaluate, evidence.Coverage(rule, dayTarget).Outcome);
        var retained = evidence.Assessments.Single(a => a.Assessment.Id == pass.Id);
        Assert.Equal(TradingRuleSourceState.Unavailable, retained.TargetState);
        Assert.Equal(TradingRuleSourceState.Unavailable, Assert.Single(retained.Evidence).State);
        Assert.Equal(Day, retained.Evidence[0].NewYorkDate);
        Assert.Equal(account, retained.Assessment.Target.AccountScope.AccountId);
        Assert.Equal(TradingRuleAssessmentSource.Manual, retained.Assessment.Source);
        Assert.Equal("Synthetic global principle", retained.Assessment.RuleRevision.Definition.Name);
        Assert.Equal(next.Revision, Assert.Single(evidence.ApplicableRules).Revision);
        var exact = Ready(await Period(db).ReadAsync(new(ReviewPeriodKind.Week, Day, account)));
        Assert.Equal(2, exact.Assessments.Count); Assert.DoesNotContain(exact.Assessments, a => a.Assessment.Id == missing.Id);
        Assert.True(await ruleStore.TryAppendAsync(next.Archive(2, Close.AddDays(2)), 2));
        Assert.Empty(Ready(await Period(db).ReadAsync(query)).ApplicableRules);
        Assert.Equal(3, Ready(await Period(db).ReadAsync(query)).Assessments.Count);
    }

    [Fact]
    public async Task PeriodFrozenPlaybookRetainsLegacyRemovedAndReplacedContextWithoutCurrentSubstitution()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db);
        var setup = new TradingSetup("Original", null, Audit);
        await db.ServiceProvider.GetRequiredService<ITradingSetupStore>().AddAsync(setup);
        var trade = TradeFact(account, instrument, Close); trade.SetTradingSetup(setup.Id, Audit);
        await Add(db, trade);
        var query = new PeriodReviewQuery(ReviewPeriodKind.Month, Day);
        Assert.Equal(ReviewPlaybookState.LegacyClassification, Ready(await Period(db).ReadAsync(query)).ClosedTrades[0].Playbook!.CurrentState);
        var first = TradingSetupPlaybookRevision.Create(setup, new("Context", "Entry", "Confirm", "Invalidate", "Execute",
            [new PlaybookExample(Guid.NewGuid(), "Only text; no chart inspection")]), Audit);
        var store = db.ServiceProvider.GetRequiredService<ITradingSetupPlaybookStore>();
        Assert.True(await store.TryAppendAsync(first, 0, Audit));
        Assert.True(await db.ServiceProvider.GetRequiredService<ITradePlaybookContextStore>()
            .TryAddAsync(TradePlaybookContext.Capture(trade, setup, first, Close), Audit));
        var associations = new Infrastructure.Trades.TradePlaybookAssociationStore(db.ContextFactory, TimeProvider.System);
        Assert.True(await associations.TryApplyAsync(await associations.PreviewAsync(trade.Id, setup.Id)));
        Assert.True(await store.TryAppendAsync(first.Revise(setup, 1, new("Changed", "E", "C", "I", "X"), Close.AddHours(1)), 1, Audit));
        Assert.True(await associations.TryApplyAsync(await associations.PreviewAsync(trade.Id, setup.Id)));
        Assert.True(await associations.TryApplyAsync(await associations.PreviewAsync(trade.Id, null)));
        var evidence = Ready(await new PeriodReviewEvidenceReader(db.ContextFactory).ReadAsync(query));
        var playbook = evidence.ClosedTrades[0].Playbook!;
        Assert.Equal(ReviewPlaybookState.NoAssociation, playbook.CurrentState);
        Assert.Equal(new[] { 0, 1, 2, 3 }, playbook.Associations.Select(a => a.Sequence));
        Assert.Equal("Context", playbook.Associations[0].Guidance!.MarketContext);
        Assert.Equal(1, playbook.Associations[0].Guidance!.Revision);
        Assert.Equal(2, playbook.Associations[2].Guidance!.Revision);
        Assert.Null(playbook.Associations[3].Guidance);
        Assert.Equal("Only text; no chart inspection", Assert.Single(playbook.Associations[0].Guidance!.Examples).Annotation);
    }

    [Fact]
    public async Task PeriodSnapshotStaysConsistentAcrossConcurrentJournalTradeAndRuleCommits()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db); var trade = TradeFact(account, instrument, Close); await Add(db, trade);
        await db.ServiceProvider.GetRequiredService<IDailyJournalRepository>().CreateAsync(new(Day, account, "Before", true));
        var rules = db.ServiceProvider.GetRequiredService<ITradingRuleStore>();
        var rule = TradingRuleRevision.Create(Guid.NewGuid(), new("Before"), Audit); Assert.True(await rules.TryAppendAsync(rule, 0));
        var observer = new ReadObserver();
        observer.BeforeRead = async count =>
        {
            if (count != 3) return;
            await using var writer = await db.ContextFactory.CreateDbContextAsync();
            (await writer.Trades.SingleAsync()).UpdatedAtUtc = Audit.AddDays(1);
            var journal = await writer.DailyJournals.SingleAsync(); journal.Text = "After"; journal.Revision++;
            writer.DailyJournalRevisions.Add(DailyJournalPersistenceMapper.ToRevision(DailyJournalPersistenceMapper.ToDomain(journal)));
            await writer.SaveChangesAsync();
            Assert.True(await rules.TryAppendAsync(rule.Revise(1, new("After"), Close), 1));
        };
        var reader = await ObservedPeriod(db, observer);
        var query = new PeriodReviewQuery(ReviewPeriodKind.Week, Day);
        var before = Ready(await reader.ReadAsync(query));
        Assert.Equal(Audit, before.ClosedTrades[0].UpdatedAtUtc); Assert.Equal("Before", before.Journals[0].Text);
        Assert.Single(before.JournalRevisions); Assert.Equal(1, before.ApplicableRules[0].Revision);
        observer.BeforeRead = null;
        var after = Ready(await reader.ReadAsync(query));
        Assert.Equal(Audit.AddDays(1), after.ClosedTrades[0].UpdatedAtUtc); Assert.Equal("After", after.Journals[0].Text);
        Assert.Equal(2, after.JournalRevisions.Count); Assert.Equal(2, after.ApplicableRules[0].Revision);
        Assert.False(observer.Tracked); Assert.Equal(0, observer.NonReads);
    }

    [Fact]
    public async Task PeriodLimitsRejectWholeSnapshotAndReadCountDoesNotGrowPerTradeOrDay()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db);
        await Add(db, TradeFact(account, instrument, Close));
        var observer = new ReadObserver(); var reader = await ObservedPeriod(db, observer);
        var query = new PeriodReviewQuery(ReviewPeriodKind.Month, Day);
        Assert.Single(Ready(await reader.ReadAsync(query)).ClosedTrades);
        int reads = observer.Sql.Count; observer.Sql.Clear();
        await Add(db, Enumerable.Range(0, 30).Select(i => TradeFact(account, instrument, Close.AddDays(i % 20))).ToArray());
        Assert.Equal(31, Ready(await reader.ReadAsync(query)).ClosedTrades.Count);
        Assert.Equal(reads, observer.Sql.Count); Assert.False(observer.Tracked); Assert.Equal(0, observer.NonReads);
        var rows = await new PeriodReviewEvidenceReader(db.ContextFactory, new(maxRowsPerSet: 30)).ReadAsync(query);
        Assert.Equal(PeriodEvidenceStatus.LimitExceeded, rows.Status); Assert.Null(rows.Evidence); Assert.Equal("trades", rows.LimitCode);
        var executions = await new PeriodReviewEvidenceReader(db.ContextFactory, new(maxRowsPerSet: 40)).ReadAsync(query);
        Assert.Null(executions.Evidence); Assert.Equal("executions", executions.LimitCode);
        var text = await new PeriodReviewEvidenceReader(db.ContextFactory, new(maxTextCharacters: 1)).ReadAsync(query);
        Assert.Null(text.Evidence); Assert.Equal("text-budget", text.LimitCode);
    }

    [Fact]
    public async Task PeriodAutomaticAssessmentsRetainRecordedCalculationAndDoNotRunChecksOnRead()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db);
        var trade = TradeFact(account, instrument, Close, -30, commission: null); await Add(db, trade);
        var store = db.ServiceProvider.GetRequiredService<ITradingRuleStore>();
        foreach (var basis in new[] { RuleProfitBasis.Gross, RuleProfitBasis.StrictNet })
            Assert.True(await store.TryAppendAsync(TradingRuleRevision.Create(Guid.NewGuid(),
                new("Typed synthetic", "", "", new(basis, 5, "USD", capability: RuleCheckCapability.ClosedDayLoss)), Audit), 0));
        var target = TradingRuleAssessmentTarget.ForNewYorkDay(Day, TradingRuleAccountScope.ForAccount(account));
        var run = await db.ServiceProvider.GetRequiredService<IAutomaticRuleChecks>().RunAsync(target);
        Assert.Equal(2, run.Recorded);
        await using (var writer = await db.ContextFactory.CreateDbContextAsync())
        { (await writer.TradeExecutions.SingleAsync(e => e.Sequence == 2)).Price = 1200; await writer.SaveChangesAsync(); }
        var observer = new ReadObserver(); var reader = await ObservedPeriod(db, observer);
        var query = new PeriodReviewQuery(ReviewPeriodKind.Month, Day, account);
        var evidence = Ready(await reader.ReadAsync(query));
        Assert.Equal(2, evidence.Assessments.Count);
        Assert.All(evidence.Assessments, a =>
        {
            Assert.Equal(TradingRuleAssessmentSource.AutomaticCandidate, a.Assessment.Source);
            Assert.Equal(AutomaticRuleCalculator.Version, a.Evaluation!.EvaluatorVersion);
            Assert.Equal(trade.Id, Assert.Single(a.Evaluation.Facts).TradeId);
            Assert.Equal(trade.Executions.Select(e => e.Id), a.Evaluation.ExecutionIds);
            Assert.Equal(TradingRuleSourceState.Changed, Assert.Single(a.Evidence).State);
        });
        var gross = evidence.Assessments.Single(a => a.Evaluation!.Criterion.Basis == RuleProfitBasis.Gross);
        Assert.Equal(-30, gross.Evaluation!.Calculation.Value);
        Assert.Equal(RuleCoverageOutcome.ConfirmedViolation, evidence.Coverage(gross.Assessment.RuleRevision, target).Outcome);
        var net = evidence.Assessments.Single(a => a.Evaluation!.Criterion.Basis == RuleProfitBasis.StrictNet);
        Assert.Null(net.Evaluation!.Calculation.Value);
        Assert.Equal(RuleCoverageOutcome.CannotEvaluate, evidence.Coverage(net.Assessment.RuleRevision, target).Outcome);
        Assert.Equal(2, Ready(await reader.ReadAsync(query)).Assessments.Count); Assert.Equal(0, observer.NonReads);
        var limited = await new PeriodReviewEvidenceReader(db.ContextFactory, new(maxScopedAssessments: 1)).ReadAsync(query);
        Assert.Null(limited.Evidence); Assert.Equal("assessment-history", limited.LimitCode);
    }

    [Fact]
    public async Task PeriodCancellationBeforeAndDuringReadNeverReturnsPartialData()
    {
        await using var db = await ReaderTestDatabase.CreateAsync(); var observer = new ReadObserver();
        var reader = await ObservedPeriod(db, observer); var query = new PeriodReviewQuery(ReviewPeriodKind.Week, Day);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync(query, new(true)));
        Assert.Empty(observer.Sql);
        using var cancellation = new CancellationTokenSource();
        observer.BeforeRead = n => { if (n == 5) cancellation.Cancel(); return Task.CompletedTask; };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync(query, cancellation.Token));
        observer.BeforeRead = null;
        var empty = Ready(await reader.ReadAsync(query));
        Assert.Empty(empty.ClosedTrades); Assert.Empty(empty.ActivityContext); Assert.Empty(empty.Journals); Assert.Empty(empty.Assessments);
        Assert.False(observer.Tracked); Assert.Equal(0, observer.NonReads);
    }

    private static TradingRuleAssessment PeriodAssessment(TradingRuleRevision rule, TradingRuleAssessmentTarget target,
        TradingRuleAssessmentOutcome outcome, TradingRuleEvidenceReference[] sources, Guid? prior = null) =>
        TradingRuleAssessment.Record(Guid.NewGuid(), rule, target, outcome, TradingRuleAssessmentSource.Manual,
            outcome == TradingRuleAssessmentOutcome.ConfirmedViolation ? TradingRuleAssessmentBasis.UserEnteredViolation : TradingRuleAssessmentBasis.RecordedEvidence,
            outcome == TradingRuleAssessmentOutcome.NoViolation ? TradingRuleEvidenceSufficiency.Sufficient : TradingRuleEvidenceSufficiency.Missing,
            "Synthetic observation, not inferred from plan", sources, prior, Close.AddMonths(2)); // Recording date is NOT attribution date.

    private static IPeriodReviewEvidenceReader Period(ReaderTestDatabase db) => db.ServiceProvider.GetRequiredService<IPeriodReviewEvidenceReader>();
    private static PeriodReviewEvidence Ready(PeriodEvidenceResult result)
    { Assert.Equal(PeriodEvidenceStatus.Ready, result.Status); Assert.Null(result.LimitCode); return Assert.IsType<PeriodReviewEvidence>(result.Evidence); }
    private static async Task<PeriodReviewEvidenceReader> ObservedPeriod(ReaderTestDatabase db, ReadObserver observer)
    {
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        var connection = new SqliteConnectionStringBuilder(context.Database.GetConnectionString()) { Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
        return new(new ObservedFactory(new DbContextOptionsBuilder<JournalDbContext>().UseSqlite(connection).AddInterceptors(observer).Options));
    }
}
