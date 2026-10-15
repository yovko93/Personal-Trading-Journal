using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Application.PeriodReviews;
using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Application.Accounts;
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
    [InlineData(ReviewPeriodKind.Week, 3, 8)] [InlineData(ReviewPeriodKind.Week, 11, 1)]
    [InlineData(ReviewPeriodKind.Month, 1, 1)] [InlineData(ReviewPeriodKind.Month, 3, 1)]
    public async Task ComparisonSqliteReadsAdjacentNyPeriodsOncePerClosureWithExactAndAggregateScope(ReviewPeriodKind kind, int month, int day)
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db); var (other, _) = await References(db, "Other", false);
        var q = new PeriodReviewQuery(kind, new(2026, month, day)); var p = PeriodComparisonEvidence.PreviousQuery(q);
        var old = TradeFact(account, instrument, p.BeforeUtc.AddTicks(-1), 10);
        var first = TradeFact(account, instrument, q.FromUtc, 20, entry: p.BeforeUtc.AddHours(-2));
        var last = TradeFact(other, instrument, q.BeforeUtc.AddTicks(-1), 30, fees: null, currency: "EUR");
        var outside = TradeFact(account, instrument, q.BeforeUtc, 50);
        await Add(db, old, first, last, outside);
        var journals = db.ServiceProvider.GetRequiredService<IDailyJournalRepository>();
        await journals.CreateAsync(new(p.LastDate, null, "Synthetic previous observation", true));
        await journals.CreateAsync(new(q.FirstDate, account, "Synthetic current observation", true));
        foreach (Guid? scope in new Guid?[] { null, account, other })
        {
            var reader = db.ServiceProvider.GetRequiredService<IPeriodComparisonEvidenceReader>();
            var result = await reader.ReadComparisonAsync(new(kind, q.FirstDate, scope));
            Assert.Equal(PeriodEvidenceStatus.Ready, result.Status); var pair = result.Evidence!;
            Assert.Equal(PeriodSourceConsistency.SingleReadTransaction, pair.Consistency);
            Assert.DoesNotContain(pair.Current.ClosedTrades, t => t.TradeId == outside.Id || t.TradeId == old.Id);
            Assert.DoesNotContain(pair.Previous.ClosedTrades, t => t.TradeId != old.Id);
            Assert.Empty(pair.Current.ActivityContext); // Midnight-spanning close belongs only to current period.
            Assert.Equal(scope == other ? 0 : 1, pair.Previous.ClosedTrades.Count);
            Assert.Equal(scope is null ? 2 : 1, pair.Current.ClosedTrades.Count);
            if (scope is null) Assert.Null(Assert.Single(pair.Previous.Journals).TradingAccountId);
            else Assert.Empty(pair.Previous.Journals);
            var comparison = PeriodReviewComparisonCalculator.Calculate(pair).Comparison!;
            Assert.Same(pair.Current, comparison.CurrentPatterns.Evidence);
            if (scope != other) Assert.Equal(10, comparison.Groups.Single(g => g.AccountId == account).Gross.Absolute);
            if (scope != account) Assert.Null(comparison.Groups.Single(g => g.Currency == "EUR" && g.AccountId is null).Net.Absolute);
        }
    }

    [Fact]
    public async Task ComparisonWriterBetweenPeriodReadsCannotMixTradeJournalOrRuleRevisions()
    {
        await using var db = await ReaderTestDatabase.CreateAsync(); var (account, instrument) = await References(db);
        var query = new PeriodReviewQuery(ReviewPeriodKind.Week, Day); var previous = PeriodComparisonEvidence.PreviousQuery(query);
        await Add(db, TradeFact(account, instrument, previous.FromUtc.AddHours(12)), TradeFact(account, instrument, query.FromUtc.AddHours(12)));
        var journals = db.ServiceProvider.GetRequiredService<IDailyJournalRepository>();
        await journals.CreateAsync(new(previous.FirstDate, account, "Before", true));
        await journals.CreateAsync(new(query.FirstDate, account, "Before", true));
        var rules = db.ServiceProvider.GetRequiredService<ITradingRuleStore>();
        var rule = TradingRuleRevision.Create(Guid.NewGuid(), new("Before"), Audit); Assert.True(await rules.TryAppendAsync(rule, 0));
        var observer = new ReadObserver(); var reader = await ObservedPeriod(db, observer);
        // Measure one complete period with this shape; inject exactly at the next period's first SELECT.
        Ready(await reader.ReadAsync(previous)); int firstPeriodReads = observer.Sql.Count; observer.Sql.Clear();
        bool committed = false;
        observer.BeforeRead = async n =>
        {
            if (n != firstPeriodReads + 1) return;
            await using var writer = await db.ContextFactory.CreateDbContextAsync();
            foreach (var t in await writer.Trades.ToListAsync()) t.UpdatedAtUtc = Audit.AddDays(1);
            foreach (var j in await writer.DailyJournals.ToListAsync())
            {
                j.Text = "After"; j.Revision++;
                writer.DailyJournalRevisions.Add(DailyJournalPersistenceMapper.ToRevision(DailyJournalPersistenceMapper.ToDomain(j)));
            }
            await writer.SaveChangesAsync();
            Assert.True(await rules.TryAppendAsync(rule.Revise(1, new("After"), Close), 1)); committed = true;
        };
        var result = await reader.ReadComparisonAsync(query); Assert.True(committed);
        Assert.Equal(PeriodEvidenceStatus.Ready, result.Status);
        foreach (var e in new[] { result.Evidence!.Previous, result.Evidence.Current })
        {
            Assert.Equal(Audit, Assert.Single(e.ClosedTrades).UpdatedAtUtc);
            Assert.Equal("Before", Assert.Single(e.Journals).Text); Assert.Single(e.JournalRevisions);
            Assert.Equal(1, Assert.Single(e.ApplicableRules).Revision);
        }
        int reads = observer.Sql.Count;
        Assert.NotNull(PeriodReviewComparisonCalculator.Calculate(result.Evidence).Comparison);
        Assert.Equal(reads, observer.Sql.Count); Assert.Equal(0, observer.NonReads); Assert.False(observer.Tracked);
        observer.BeforeRead = null;
        var after = (await reader.ReadComparisonAsync(query)).Evidence!;
        foreach (var e in new[] { after.Previous, after.Current })
        {
            Assert.Equal(Audit.AddDays(1), e.ClosedTrades[0].UpdatedAtUtc); Assert.Equal("After", e.Journals[0].Text);
            Assert.Equal(2, e.JournalRevisions.Count); Assert.Equal(2, e.ApplicableRules[0].Revision);
        }
    }

    [Fact]
    public async Task ComparisonSharedBudgetCancellationAndQueryCountAreBounded()
    {
        await using var db = await ReaderTestDatabase.CreateAsync(); var (account, instrument) = await References(db);
        var q = new PeriodReviewQuery(ReviewPeriodKind.Month, Day); var p = PeriodComparisonEvidence.PreviousQuery(q);
        await Add(db, TradeFact(account, instrument, p.FromUtc.AddHours(12)), TradeFact(account, instrument, q.FromUtc.AddHours(12)));
        var observer = new ReadObserver(); var reader = await ObservedPeriod(db, observer);
        Assert.NotNull((await reader.ReadComparisonAsync(q)).Evidence); int reads = observer.Sql.Count; observer.Sql.Clear();
        await Add(db, Enumerable.Range(0, 30).Select(i => TradeFact(account, instrument, q.FromUtc.AddHours(i + 13))).ToArray());
        var full = await reader.ReadComparisonAsync(q); Assert.Equal(31, full.Evidence!.Current.ClosedTrades.Count);
        Assert.Equal(reads, observer.Sql.Count); Assert.Equal(0, observer.NonReads);
        var limited = await new PeriodReviewEvidenceReader(db.ContextFactory, new(maxRowsPerSet: 30)).ReadComparisonAsync(q);
        Assert.Equal(PeriodEvidenceStatus.LimitExceeded, limited.Status); Assert.Null(limited.Evidence);
        var text = await new PeriodReviewEvidenceReader(db.ContextFactory, new(maxTextCharacters: 1)).ReadComparisonAsync(q);
        Assert.Null(text.Evidence); Assert.Equal("text-budget", text.LimitCode);
        observer.Sql.Clear();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadComparisonAsync(q, new(true))); Assert.Empty(observer.Sql);
        using var cancel = new CancellationTokenSource();
        observer.BeforeRead = n => { if (n == reads / 2 + 2) cancel.Cancel(); return Task.CompletedTask; };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadComparisonAsync(q, cancel.Token));
        observer.BeforeRead = null; Assert.NotNull((await reader.ReadComparisonAsync(q)).Evidence);
    }

    [Fact]
    public async Task ComparisonRestartKeepsFrozenSetupVersionsMistakesAndDeletedRuleSources()
    {
        await using var db = await ReaderTestDatabase.CreateAsync(); var (account, instrument) = await References(db);
        var query = new PeriodReviewQuery(ReviewPeriodKind.Week, Day, account); var previous = PeriodComparisonEvidence.PreviousQuery(query);
        var setup = new TradingSetup("Original", null, Audit); await db.ServiceProvider.GetRequiredService<ITradingSetupStore>().AddAsync(setup);
        var old = TradeFact(account, instrument, previous.FromUtc.AddHours(12)); old.SetTradingSetup(setup.Id, Audit);
        var current = TradeFact(account, instrument, query.FromUtc.AddHours(12)); current.SetTradingSetup(setup.Id, Audit);
        var deleted = TradeFact(account, instrument, previous.FromUtc.AddHours(13));
        await Add(db, old, current, deleted);
        var guidance = TradingSetupPlaybookRevision.Create(setup, new("Before", "Entry", "Confirm", "Invalid", "Execution"), Audit);
        var playbooks = db.ServiceProvider.GetRequiredService<ITradingSetupPlaybookStore>();
        Assert.True(await playbooks.TryAppendAsync(guidance, 0, Audit));
        Assert.True(await db.ServiceProvider.GetRequiredService<ITradePlaybookContextStore>()
            .TryAddAsync(TradePlaybookContext.Capture(old, setup, guidance, Close), Audit));
        var revised = guidance.Revise(setup, 1, new("After", "Entry", "Confirm", "Invalid", "Execution"), Close);
        Assert.True(await playbooks.TryAppendAsync(revised, 1, Audit));
        Assert.True(await db.ServiceProvider.GetRequiredService<ITradePlaybookContextStore>()
            .TryAddAsync(TradePlaybookContext.Capture(current, setup, revised, Close), Audit));
        var rule = TradingRuleRevision.Create(Guid.NewGuid(), new("Retained rule"), Audit);
        var rules = db.ServiceProvider.GetRequiredService<ITradingRuleStore>(); Assert.True(await rules.TryAppendAsync(rule, 0));
        Assert.True(await db.ServiceProvider.GetRequiredService<ITradingRuleAssessmentStore>().TryAppendAsync(
            PeriodAssessment(rule, TradingRuleAssessmentTarget.ForTrade(deleted.Id, account), TradingRuleAssessmentOutcome.NoViolation,
                [new(TradingRuleEvidenceKind.Trade, deleted.Id)]), 1, null));
        await db.ServiceProvider.GetRequiredService<ITradeDeletionStore>().DeleteAsync(deleted.Id);
        Assert.True(await rules.TryAppendAsync(rule.Archive(1, Close), 1));
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var catalog = await ctx.TradingSetups.SingleAsync(); catalog.Name = "Renamed"; catalog.IsActive = false;
            foreach (int i in Enumerable.Range(0, 2))
            {
                var id = Guid.NewGuid();
                ctx.TradingMistakes.Add(new() { Id = id, Name = "Recorded " + i, IsActive = true, CreatedAtUtc = Audit, UpdatedAtUtc = Audit });
                ctx.TradeMistakes.Add(new() { Id = Guid.NewGuid(), TradeId = current.Id, TradingMistakeId = id, CreatedAtUtc = Audit, UpdatedAtUtc = Audit });
            }
            await ctx.SaveChangesAsync();
        }
        var pair = (await db.ServiceProvider.GetRequiredService<IPeriodComparisonEvidenceReader>().ReadComparisonAsync(query)).Evidence!;
        var comparison = PeriodReviewComparisonCalculator.Calculate(pair).Comparison!;
        Assert.Equal(1, comparison.Current.Population.Closed.Count);
        Assert.Equal(2, comparison.Breakdowns.Count(g => g.Key.Kind == PeriodBreakdownKind.RecordedMistake && g.Key.SourceId is not null));
        Assert.Equal(new int?[] { 1, 2 }, comparison.Breakdowns.Where(g => g.Key.Kind == PeriodBreakdownKind.Setup).Select(g => g.Key.Revision));
        Assert.Equal("Original", pair.Previous.ClosedTrades[0].Playbook!.Associations[0].Guidance!.SetupName);
        Assert.Equal("Renamed", pair.Current.ClosedTrades[0].Setup!.Name);
        var retained = Assert.Single(comparison.Rules); Assert.Null(retained.Current);
        Assert.Equal(PeriodRuleVersionState.NotInActiveGlobalCatalog, retained.Previous!.VersionState);
        Assert.Equal(1, retained.Previous.UnavailableTargets); Assert.Null(retained.CompliantChange);
        string path;
        await using (var context = await db.ContextFactory.CreateDbContextAsync()) path = context.Database.GetDbConnection().DataSource;
        var paths = new Infrastructure.Storage.LocalApplicationPaths(Path.GetDirectoryName(Path.GetDirectoryName(path))!);
        Assert.Equal(path, paths.DatabasePath);
        var services = new ServiceCollection(); services.AddPersistence(paths); await using var restarted = services.BuildServiceProvider();
        var fresh = (await restarted.GetRequiredService<IPeriodComparisonEvidenceReader>().ReadComparisonAsync(query)).Evidence!;
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(comparison),
            System.Text.Json.JsonSerializer.Serialize(PeriodReviewComparisonCalculator.Calculate(fresh).Comparison));
    }
}
