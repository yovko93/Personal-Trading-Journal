using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Journals;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.DailyReview;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.DailyReview;

public sealed class DailyReviewEvidenceReaderTests
{
    private static readonly DateOnly Day = new(2026, 10, 8);
    private static readonly DateTimeOffset Close = new(2026, 10, 8, 16, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Audit = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(2026, 3, 8)]
    [InlineData(2026, 11, 1)]
    public async Task StatisticsReuseCalendarMetricsAndScopeWithoutEstimatingUnknownNet(int year, int month, int day)
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db);
        var (other, _) = await References(db, "Inactive", false);
        var query = new DailyReviewQuery(new(year, month, day));
        var first = TradeFact(account, instrument, query.FromUtc, 100); // Opens on previous NY date.
        var last = TradeFact(account, instrument, query.BeforeUtc.AddTicks(-1), -40, commission: 5);
        var zero = TradeFact(other, instrument, query.FromUtc.AddHours(2), 0);
        var unknown = TradeFact(other, instrument, query.FromUtc.AddHours(3), -285, fees: null, currency: "EUR");
        var partial = TradeFact(account, instrument, query.FromUtc.AddHours(4), exitQuantity: 1);
        var nextDay = TradeFact(account, instrument, query.BeforeUtc, 200);
        await Add(db, first, last, zero, unknown, partial, nextDay);

        foreach (Guid? scope in new Guid?[] { null, account, other })
        {
            var evidence = await Reader(db).GetAsync(new(query.Date, scope));
            var stats = DailyReviewStatisticsCalculator.Calculate(evidence);
            var calendar = await db.ServiceProvider.GetRequiredService<ITradingCalendarDayReader>()
                .GetAsync(new(query.Date, scope));
            Assert.Equal(calendar.ClosedTradeCount, stats.Population.Closed.Count);
            Assert.Equal(calendar.Trades.Select(t => t.Id).Order(), stats.Population.Closed.TradeIds);
            Assert.DoesNotContain(nextDay.Id, stats.Population.Closed.TradeIds);
            foreach (var currency in stats.Currencies)
            {
                var expected = calendar.Currencies.Single(c => c.Currency == currency.Currency).Metrics;
                Assert.Equal(expected.Gross, currency.Gross.Metrics);
                Assert.Equal(expected.Net, currency.Net.Metrics);
                Assert.Equal(currency.Population.Closed.Count, currency.Accounts.Sum(a => a.Population.Closed.Count));
                Assert.All(currency.Accounts, group => Assert.All(group.Population.Closed.TradeIds,
                    id => Assert.Equal(group.Account.Id, evidence.Trades.Single(t => t.TradeId == id).Account.Id)));
            }
        }
        var all = DailyReviewStatisticsCalculator.Calculate(await Reader(db).GetAsync(query));
        Assert.Equal(4, all.Population.Closed.Count);
        Assert.Equal(partial.Id, Assert.Single(all.Population.ExcludedOpenActivity.TradeIds));
        Assert.Equal(60m, all.Currencies.Single(c => c.Currency == "USD").Gross.Metrics.Total);
        Assert.Equal(55m, all.Currencies.Single(c => c.Currency == "USD").Net.Metrics.Total);
        var eur = all.Currencies.Single(c => c.Currency == "EUR");
        Assert.Equal(-285m, eur.Gross.Metrics.Total);
        Assert.Null(eur.Net.Metrics.Total);
        Assert.Equal(MetricCoverageStatus.Unavailable, eur.Net.Metrics.Coverage.Status);
        Assert.Equal(unknown.Id, Assert.Single(eur.Net.Unavailable.TradeIds));
        var next = DailyReviewStatisticsCalculator.Calculate(await Reader(db).GetAsync(new(query.Date.AddDays(1))));
        Assert.DoesNotContain(first.Id, next.Population.Closed.TradeIds);
        Assert.Equal(nextDay.Id, Assert.Single(next.Population.Closed.TradeIds));
    }

    [Fact]
    public async Task AllAccountsRetainsEveryIdentityAndExactScopeExcludesNullJournals()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (p21, instrument) = await References(db, "P 21");
        var (other, _) = await References(db, "Archive", false);
        var a = TradeFact(p21, instrument, Close, -285, commission: null);
        var b = TradeFact(other, instrument, Close, 0);
        await Add(db, a, b);
        var journals = db.ServiceProvider.GetRequiredService<IDailyJournalRepository>();
        var global = (await journals.CreateAsync(new(Day, null, "", true))).Journal!.Entry;
        var scoped = (await journals.CreateAsync(new(Day, p21, "P21 note", false,
            new("Well", "", "Next")))).Journal!.Entry;
        var archived = (await journals.CreateAsync(new(Day, other, "Partial draft", true))).Journal!.Entry;
        await journals.CreateAsync(new(Day.AddDays(-1), null, "Other day", false));
        var reader = Reader(db);
        var all = await reader.GetAsync(new(Day));
        var coaching = CoachingEvidencePacketBuilder.Build(all);
        Assert.Equal(CoachingPacketBuildStatus.Ready, coaching.Status);
        Assert.Equal(all.Journals.Select(j => (j.JournalId, j.Revision)),
            coaching.Packet!.Content.UntrustedJournalObservations.Select(j => (j.JournalId, j.Revision)));
        Assert.Equal(all.Trades.Select(t => t.TradeId).Order(),
            coaching.Packet.Content.CalculatedFacts.Population.Closed.TradeIds);
        Assert.Equal(new[] { a.Id, b.Id }.Order(), all.Trades.Select(t => t.TradeId));
        Assert.Equal(3, all.Journals.Count);
        Assert.Equal(global.Id, all.Journals[0].JournalId);
        Assert.Equal("", all.Journals[0].Text); Assert.True(all.Journals[0].IsDraft);
        Assert.Equal(DailyJournalAccountState.AllAccounts, all.Journals[0].AccountState);
        Assert.Null(all.Journals[0].TradingAccountId);
        Assert.Equal("P21 note", all.Journals.Single(j => j.JournalId == scoped.Id).Text);
        Assert.Equal("Well", all.Journals.Single(j => j.JournalId == scoped.Id).Answers.WentWell);
        Assert.Equal(DailyJournalAccountState.Inactive, all.Journals.Single(j => j.JournalId == archived.Id).AccountState);
        Assert.False(all.Trades.Single(t => t.TradeId == b.Id).Account.IsActive);
        var exact = await reader.GetAsync(new(Day, p21));
        Assert.Equal(a.Id, Assert.Single(exact.Trades).TradeId);
        Assert.Equal(scoped.Id, Assert.Single(exact.Journals).JournalId);
        Assert.Equal(p21, exact.Journals[0].TradingAccountId);
        var exactPacket = CoachingEvidencePacketBuilder.Build(exact);
        Assert.Equal(CoachingPacketBuildStatus.Ready, exactPacket.Status);
        Assert.Equal(p21, Assert.Single(exactPacket.Packet!.Content.UntrustedJournalObservations).TradingAccountId);
        Assert.Equal("P 21", exact.Journals[0].AccountName);
        var unknownScope = await reader.GetAsync(new(Day, Guid.NewGuid()));
        Assert.Empty(unknownScope.Trades); Assert.Empty(unknownScope.Journals);

        await journals.UpdateAsync(new(scoped.Id, scoped.Revision, "Edited", true,
            new("", "Improve", ""), ReopenCompleted: true));
        var fresh = Assert.Single((await reader.GetAsync(new(Day, p21))).Journals);
        Assert.Equal(scoped.Id, fresh.JournalId); Assert.Equal(scoped.Revision + 1, fresh.Revision);
        Assert.Equal("Edited", fresh.Text); Assert.True(fresh.IsDraft);
        Assert.Equal(scoped.CreatedAtUtc, fresh.CreatedAtUtc);
        Assert.Equal("P21 note", exact.Journals[0].Text); // Disconnected evidence remains unchanged.
    }

    [Theory]
    [InlineData(2026, 3, 8)]
    [InlineData(2026, 11, 1)]
    public async Task ClosureAttributionMatchesCalendarAcrossMidnightAndDstWithoutDuplicates(int year, int month, int day)
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db);
        var query = new DailyReviewQuery(new(year, month, day));
        var before = TradeFact(account, instrument, query.FromUtc.AddTicks(-1));
        var first = TradeFact(account, instrument, query.FromUtc);
        var last = TradeFact(account, instrument, query.BeforeUtc.AddTicks(-1));
        var after = TradeFact(account, instrument, query.BeforeUtc);
        // Includes both distinct autumn 01:30 instants (and either side of spring's skipped hour).
        var early = TradeFact(account, instrument, query.FromUtc.AddMinutes(90));
        var later = TradeFact(account, instrument, query.FromUtc.AddMinutes(150));
        await Add(db, before, first, last, after, early, later);
        var result = await Reader(db).GetAsync(query);
        var calendar = await db.ServiceProvider.GetRequiredService<ITradingCalendarDayReader>()
            .GetAsync(new(query.Date, account));
        Assert.Equal(calendar.Trades.Select(t => t.Id), result.Trades.Select(t => t.TradeId));
        Assert.Equal(new[] { last.Id, later.Id, early.Id, first.Id }, result.Trades.Select(t => t.TradeId));
        Assert.All(result.Trades, t => Assert.Equal(DailyReviewTradeInclusion.ClosedOnDate, t.Inclusion));
        Assert.Equal(first.Executions.Select(e => e.Id), result.Trades[^1].Executions.Select(e => e.Id));
        Assert.Equal(first.OpenedAtUtc, result.Trades[^1].Facts!.OpenedAtUtc); // Previous NY date.
        var previous = await Reader(db).GetAsync(new(query.Date.AddDays(-1)));
        Assert.DoesNotContain(previous.Trades, t => t.TradeId == first.Id);
        Assert.Equal(result.Trades.Count, result.Trades.Select(t => t.TradeId).Distinct().Count());
    }

    [Fact]
    public async Task OpenAndPartialExitsAreContextOnlyAndUnrelatedOvernightPositionsAreExcluded()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db);
        var partial = TradeFact(account, instrument, Close, exitQuantity: 1, entry: Close.AddDays(-1));
        var open = TradeFact(account, instrument, Close, exitQuantity: 0, entry: Close);
        var inactive = TradeFact(account, instrument, Close, exitQuantity: 0, entry: Close.AddDays(-1));
        await Add(db, partial, open, inactive);
        var result = await Reader(db).GetAsync(new(Day));
        Assert.Equal(2, result.Trades.Count);
        Assert.DoesNotContain(result.Trades, t => t.TradeId == inactive.Id);
        Assert.All(result.Trades, t =>
        {
            Assert.Equal(DailyReviewTradeInclusion.OpenActivityOnDate, t.Inclusion);
            Assert.Equal(TradeStatus.Open, t.Facts!.Status); Assert.Null(t.Facts.ClosedAtUtc);
            Assert.Null(t.Facts.GrossPnL); Assert.Null(t.Facts.NetPnL);
            Assert.True(t.Quality.HasFlag(DailyReviewTradeQuality.UnknownNetPnL));
        });
        Assert.Equal(1m, result.Trades.Single(t => t.TradeId == partial.Id).Facts!.OpenQuantity);
        Assert.Equal(2, result.Trades.Single(t => t.TradeId == partial.Id).Executions.Count);
        Assert.Empty((await db.ServiceProvider.GetRequiredService<ITradingCalendarDayReader>().GetAsync(new(Day))).Trades);
    }

    [Fact]
    public async Task CostsCurrenciesClassificationsAndSourceAuditsRemainTraceableWithoutEstimates()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db);
        var unknownCommission = TradeFact(account, instrument, Close, -285, commission: null);
        var unknownFees = TradeFact(account, instrument, Close, 5, fees: null);
        var zero = TradeFact(account, instrument, Close, 0);
        var eur = TradeFact(account, instrument, Close, 6, commission: 1, fees: 2, currency: "EUR");
        await Add(db, unknownCommission, unknownFees, zero, eur);
        Guid setupId = Guid.NewGuid(), mistakeId = Guid.NewGuid(), assignmentId = Guid.NewGuid();
        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            context.TradingSetups.Add(new() { Id = setupId, Name = "Historical Setup", IsActive = false, CreatedAtUtc = Audit, UpdatedAtUtc = Audit });
            context.TradingMistakes.Add(new() { Id = mistakeId, Name = "Assigned by user", IsActive = false, CreatedAtUtc = Audit, UpdatedAtUtc = Audit });
            (await context.Trades.SingleAsync(t => t.Id == eur.Id)).TradingSetupId = setupId;
            context.TradeMistakes.Add(new() { Id = assignmentId, TradeId = eur.Id, TradingMistakeId = mistakeId, CreatedAtUtc = Audit, UpdatedAtUtc = Audit });
            await context.SaveChangesAsync();
        }
        var result = await Reader(db).GetAsync(new(Day));
        var unknown = result.Trades.Single(t => t.TradeId == unknownCommission.Id);
        Assert.Equal(-285m, unknown.Facts!.GrossPnL); Assert.Null(unknown.Facts.NetPnL);
        Assert.Null(unknown.Facts.TotalCosts); Assert.True(unknown.Quality.HasFlag(DailyReviewTradeQuality.UnknownCommission));
        Assert.False(unknown.Quality.HasFlag(DailyReviewTradeQuality.UnknownFees));
        Assert.Null(unknown.Executions[^1].Commission); Assert.Equal(0m, unknown.Executions[^1].Fees);
        Assert.True(result.Trades.Single(t => t.TradeId == unknownFees.Id).Quality.HasFlag(DailyReviewTradeQuality.UnknownFees));
        Assert.Equal(0m, result.Trades.Single(t => t.TradeId == zero.Id).Facts!.NetPnL);
        Assert.Equal(DailyReviewTradeQuality.None, result.Trades.Single(t => t.TradeId == zero.Id).Quality);
        var foreign = result.Trades.Single(t => t.TradeId == eur.Id);
        Assert.Equal("EUR", foreign.PricingCurrency); Assert.Equal(3m, foreign.Facts!.NetPnL);
        Assert.Equal(eur.CreatedAtUtc, foreign.CreatedAtUtc); Assert.Equal(eur.UpdatedAtUtc, foreign.UpdatedAtUtc);
        Assert.Equal(TradeBrowsePersistenceMapper.CurrentProjectionVersion, foreign.Facts.ProjectionVersion);
        Assert.Equal(setupId, foreign.Setup!.Id); Assert.False(foreign.Setup.IsActive);
        Assert.Equal(assignmentId, Assert.Single(foreign.AssignedMistakes).AssignmentId);
        Assert.Equal(mistakeId, foreign.AssignedMistakes[0].Mistake.Id);
        Assert.False(foreign.AssignedMistakes[0].Mistake.IsActive);
        Assert.Equal(eur.Executions.Select(e => e.Id), foreign.Executions.Select(e => e.Id));
        Assert.Equal("source-exit", foreign.Executions[^1].ExternalExecutionId);
    }

    [Fact]
    public async Task IncompleteProjectionEconomicsAndMissingReferencesAreExplicit()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db);
        var missing = TradeFact(account, instrument, Close);
        var noClose = TradeFact(account, instrument, Close);
        var noExecutions = TradeFact(account, instrument, Close);
        var noEconomics = TradeFact(account, instrument, Close);
        await Add(db, missing, noClose, noExecutions, noEconomics);
        var repo = db.ServiceProvider.GetRequiredService<IDailyJournalRepository>();
        var journal = (await repo.CreateAsync(new(Day, account, "Keep historical scope", false))).Journal!.Entry;
        await using var info = await db.ContextFactory.CreateDbContextAsync();
        var connection = new SqliteConnectionStringBuilder(info.Database.GetConnectionString()) { ForeignKeys = false, Pooling = false }.ToString();
        await using (var damaged = new JournalDbContext(new DbContextOptionsBuilder<JournalDbContext>().UseSqlite(connection).Options))
        {
            await damaged.TradeBrowse.Where(t => t.TradeId == missing.Id).ExecuteDeleteAsync();
            (await damaged.TradeBrowse.SingleAsync(t => t.TradeId == noClose.Id)).ClosedAtUtc = null;
            var economics = await damaged.TradeBrowse.SingleAsync(t => t.TradeId == noEconomics.Id);
            economics.GrossPnL = null; economics.NetPnL = null; economics.ProjectionVersion = -1;
            await damaged.TradeExecutions.Where(e => e.TradeId == noExecutions.Id).ExecuteDeleteAsync();
            await damaged.TradingAccounts.Where(a => a.Id == account).ExecuteDeleteAsync();
            await damaged.Instruments.Where(i => i.Id == instrument).ExecuteDeleteAsync();
            await damaged.SaveChangesAsync();
        }
        var result = await Reader(db).GetAsync(new(Day, account));
        Assert.Equal(4, result.Trades.Count);
        var unprojected = result.Trades.Single(t => t.TradeId == missing.Id);
        Assert.Null(unprojected.Facts); Assert.True(unprojected.Quality.HasFlag(DailyReviewTradeQuality.MissingProjection));
        Assert.Equal(DailyReviewTradeInclusion.UnavailableLifecycleActivityOnDate, unprojected.Inclusion);
        Assert.True(result.Trades.Single(t => t.TradeId == noClose.Id).Quality.HasFlag(DailyReviewTradeQuality.MissingClosingTime));
        Assert.True(result.Trades.Single(t => t.TradeId == noExecutions.Id).Quality.HasFlag(DailyReviewTradeQuality.MissingExecutions));
        Assert.True(result.Trades.Single(t => t.TradeId == noEconomics.Id).Quality.HasFlag(DailyReviewTradeQuality.UnknownGrossPnL));
        Assert.True(result.Trades.Single(t => t.TradeId == noEconomics.Id).Quality.HasFlag(DailyReviewTradeQuality.UnsupportedProjectionVersion));
        Assert.All(result.Trades, t =>
        {
            Assert.Equal(account, t.Account.Id); Assert.Null(t.Account.Name); Assert.Null(t.Account.IsActive);
            Assert.True(t.Quality.HasFlag(DailyReviewTradeQuality.UnavailableInstrument));
        });
        Assert.Equal(journal.Id, Assert.Single(result.Journals).JournalId);
        Assert.Equal(DailyJournalAccountState.Unavailable, result.Journals[0].AccountState);
    }

    [Fact]
    public async Task EmptyDayIsDistinctFromAnEmptyDraftAndLegacyCompletedAnswersRemainReadable()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var reader = Reader(db);
        var empty = await reader.GetAsync(new(Day));
        Assert.Empty(empty.Trades); Assert.Empty(empty.Journals);
        var repo = db.ServiceProvider.GetRequiredService<IDailyJournalRepository>();
        var created = (await repo.CreateAsync(new(Day, null, "", true))).Journal!.Entry;
        var draft = await reader.GetAsync(new(Day));
        Assert.Empty(draft.Trades); Assert.Equal("", Assert.Single(draft.Journals).Text);
        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            var record = await context.DailyJournals.SingleAsync();
            record.IsDraft = false; record.WentWell = "Legacy answer-only completion";
            await context.SaveChangesAsync();
        }
        var legacy = Assert.Single((await reader.GetAsync(new(Day))).Journals);
        Assert.Equal(created.Id, legacy.JournalId); Assert.False(legacy.IsDraft); Assert.Empty(legacy.Text);
        Assert.Equal("Legacy answer-only completion", legacy.Answers.WentWell);
    }

    [Fact]
    public async Task ReadsUseFourSelectsNoWritesNoTrackingAndNoBrowsePageLimit()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db);
        await Add(db, Enumerable.Range(0, 75).Select(_ => TradeFact(account, instrument, Close)).ToArray());
        var observer = new ReadObserver();
        var reader = await ObservedReader(db, observer);
        var result = await reader.GetAsync(new(Day));
        Assert.Equal(75, result.Trades.Count);
        var statistics = DailyReviewStatisticsCalculator.Calculate(result);
        Assert.Equal(75, statistics.Population.Closed.Count);
        Assert.Equal(result.Trades.Sum(t => t.Facts!.GrossPnL), Assert.Single(statistics.Currencies).Gross.Metrics.Total);
        Assert.Equal(result.Trades.Select(t => t.TradeId).Order(), result.Trades.Select(t => t.TradeId));
        Assert.Equal(4, observer.Sql.Count); Assert.False(observer.Tracked);
        Assert.All(observer.Sql, sql => Assert.StartsWith("SELECT", sql));
        Assert.Equal(0, observer.NonReads);
        await using var check = await db.ContextFactory.CreateDbContextAsync();
        Assert.Equal(75, await check.Trades.CountAsync()); Assert.Equal(150, await check.TradeExecutions.CountAsync());
        Assert.Empty(await check.DailyJournals.ToListAsync()); Assert.Empty(await check.DailyJournalRevisions.ToListAsync());
    }

    [Fact]
    public async Task CancellationBeforeAndBetweenQueriesReturnsNoPartialSnapshotAndAllowsNextRead()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var observer = new ReadObserver();
        var reader = await ObservedReader(db, observer);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.GetAsync(new(Day), cancelled.Token));
        Assert.Empty(observer.Sql);
        using var during = new CancellationTokenSource();
        observer.BeforeRead = count => { if (count == 3) during.Cancel(); return Task.CompletedTask; };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.GetAsync(new(Day), during.Token));
        Assert.Equal(3, observer.Sql.Count);
        observer.BeforeRead = null;
        var recovered = await reader.GetAsync(new(Day));
        Assert.Empty(recovered.Trades); Assert.Empty(recovered.Journals); Assert.Equal(0, observer.NonReads);
    }

    [Fact]
    public async Task ConcurrentCommitCannotMixNewJournalRevisionWithOldTradeEvidence()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db);
        var trade = TradeFact(account, instrument, Close);
        await Add(db, trade);
        var journal = (await db.ServiceProvider.GetRequiredService<IDailyJournalRepository>()
            .CreateAsync(new(Day, account, "Before", true))).Journal!.Entry;
        var observer = new ReadObserver();
        observer.BeforeRead = async count =>
        {
            if (count != 4) return;
            await using var writer = await db.ContextFactory.CreateDbContextAsync();
            (await writer.Trades.SingleAsync()).UpdatedAtUtc = Audit.AddDays(1);
            var current = await writer.DailyJournals.SingleAsync();
            current.Text = "After"; current.Revision++;
            writer.DailyJournalRevisions.Add(DailyJournalPersistenceMapper.ToRevision(DailyJournalPersistenceMapper.ToDomain(current)));
            await writer.SaveChangesAsync(); // WAL commit between the snapshot's Trade and Journal SELECTs.
        };
        var reader = await ObservedReader(db, observer);
        var before = await reader.GetAsync(new(Day));
        Assert.Equal(Audit, Assert.Single(before.Trades).UpdatedAtUtc);
        Assert.Equal("Before", Assert.Single(before.Journals).Text);
        Assert.Equal(journal.Revision, before.Journals[0].Revision);
        observer.BeforeRead = null;
        var after = await reader.GetAsync(new(Day));
        Assert.Equal(Audit.AddDays(1), Assert.Single(after.Trades).UpdatedAtUtc);
        Assert.Equal("After", Assert.Single(after.Journals).Text);
        Assert.Equal(journal.Revision + 1, after.Journals[0].Revision);
    }

    private static IDailyReviewEvidenceReader Reader(ReaderTestDatabase db) => db.ServiceProvider.GetRequiredService<IDailyReviewEvidenceReader>();

    private static async Task<DailyReviewEvidenceReader> ObservedReader(ReaderTestDatabase db, ReadObserver observer)
    {
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        var connection = new SqliteConnectionStringBuilder(context.Database.GetConnectionString()) { Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
        return new(new ObservedFactory(new DbContextOptionsBuilder<JournalDbContext>().UseSqlite(connection).AddInterceptors(observer).Options));
    }

    private sealed class ObservedFactory(DbContextOptions<JournalDbContext> options) : IDbContextFactory<JournalDbContext>
    {
        public JournalDbContext CreateDbContext() => new(options);
        public Task<JournalDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(CreateDbContext()); }
    }

    private sealed class ReadObserver : DbCommandInterceptor
    {
        public List<string> Sql { get; } = [];
        public bool Tracked { get; private set; }
        public int NonReads { get; private set; }
        public Func<int, Task>? BeforeRead { get; set; }
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Sql.Add(command.CommandText); Tracked |= eventData.Context!.ChangeTracker.Entries().Any();
            Assert.StartsWith("SELECT", command.CommandText);
            if (BeforeRead is { } hook) await hook(Sql.Count);
            cancellationToken.ThrowIfCancellationRequested(); return result;
        }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        { NonReads++; throw new InvalidOperationException("Evidence reader attempted a write."); }
    }

    private static async Task<(Guid Account, Guid Instrument)> References(ReaderTestDatabase db, string name = "Evidence Account", bool active = true)
    {
        var account = new TradingAccount(name, TradingAccountType.Personal, null, null, "USD", 0m, Audit);
        if (!active) account.Deactivate(Audit.AddDays(1));
        var instrument = new Instrument("EVID" + Guid.NewGuid().ToString("N")[..6], "Evidence Instrument", AssetClass.Futures, "CME", "USD", .25m, .25m, Audit);
        await db.ServiceProvider.GetRequiredService<ITradingAccountStore>().AddAsync(account);
        await db.ServiceProvider.GetRequiredService<IInstrumentStore>().AddAsync(instrument);
        return (account.Id, instrument.Id);
    }

    private static Trade TradeFact(Guid account, Guid instrument, DateTimeOffset close, decimal gross = 10,
        decimal? commission = 0, decimal? fees = 0, string currency = "USD", decimal exitQuantity = 2,
        DateTimeOffset? entry = null)
    {
        Guid id = Guid.NewGuid();
        var executions = new List<TradeExecution> { new(id, 1, entry ?? close.AddHours(-1), ExecutionSide.Buy, 2, 1000, 0, 0, null, null, null) };
        if (exitQuantity > 0) executions.Add(new(id, 2, close, ExecutionSide.Sell, exitQuantity, 1000 + gross / 2,
            commission, fees, "source-exit", null, "TEST"));
        return Trade.Rehydrate(id, account, instrument, new(1, currency), null, executions, Audit, Audit);
    }

    private static async Task Add(ReaderTestDatabase db, params Trade[] trades)
    {
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        context.Trades.AddRange(trades.Select(TradePersistenceMapper.ToRecord));
        context.TradeExecutions.AddRange(trades.SelectMany(t => t.Executions).Select(TradeExecutionPersistenceMapper.ToRecord));
        context.TradeBrowse.AddRange(trades.Select(TradeBrowsePersistenceMapper.ToRecord));
        await context.SaveChangesAsync();
    }
}
