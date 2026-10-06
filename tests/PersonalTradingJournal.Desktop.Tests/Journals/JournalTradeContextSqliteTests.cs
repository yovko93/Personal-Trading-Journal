using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.ViewModels.Journals;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Accounts;
using PersonalTradingJournal.Infrastructure.Calendar;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;
using JournalTestDatabase = PersonalTradingJournal.Desktop.Tests.Journals.JournalSqliteTests.JournalTestDatabase;

namespace PersonalTradingJournal.Desktop.Tests.Journals;

public sealed class JournalTradeContextSqliteTests
{
    private static readonly DateOnly TradingDate = new(2026, 10, 4);
    private static readonly DateTimeOffset AuditTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ClosingTime = new(2026, 10, 4, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CommittedTradeInsertCorrectionAndDeletionRefreshContextWithoutSavingOrDiscardingJournalDraft()
    {
        await using JournalTestDatabase database = await JournalTestDatabase.CreateAsync();
        var (account, instrument) = await SeedReferencesAsync(database);
        await database.Repository.CreateAsync(new CreateDailyJournalCommand(TradingDate, account.Id, "Saved journal"));
        JournalViewModel editor = await database.OpenEditorAsync(TradingDate, account.Id);
        JournalTradeContextViewModel tradeContext = editor.TradeContext;
        Assert.Null(tradeContext.ErrorMessage);
        Assert.True(tradeContext.IsEmpty);
        Assert.Equal(0, tradeContext.ClosedTradeCount);
        JournalSnapshot savedBefore = await ReadJournalsAsync(database);
        const string draft = "  Unsaved review\r\nKeep this text exactly.\t ";
        editor.OpenEditorCommand.Execute(null);
        editor.Text = draft;
        var trade = ClosedTrade(account.Id, instrument.Id, ClosingTime, 8m);
        await database.Provider.GetRequiredService<ITradeStore>().AddAsync(trade);

        tradeContext.OnDataCommitted();
        await tradeContext.LoadTask;

        Assert.Null(tradeContext.ErrorMessage);
        Assert.Equal(trade.Id, Assert.Single(tradeContext.Rows).Trade.Id);
        Assert.Equal(8m, Assert.Single(tradeContext.Summaries).Amount);
        AssertDraft();
        TradeExecution[] corrected = trade.Executions.Select(execution => TradeExecution.Rehydrate(
            execution.Id, trade.Id, execution.Sequence, execution.ExecutedAtUtc, execution.Side,
            execution.Quantity, execution.Sequence == 2 ? 1020m : execution.Price,
            execution.Sequence == 2 ? 1m : 0m, 0m, null, null, null)).ToArray();
        trade.CorrectDetails(account.Id, instrument.Id, trade.Pricing, null, corrected, ClosingTime.AddDays(1));
        await database.Provider.GetRequiredService<ITradeMutationStore>().SaveAsync(trade);

        tradeContext.OnDataCommitted();
        await tradeContext.LoadTask;

        Assert.Equal(19m, Assert.Single(tradeContext.Rows).Amount);
        Assert.Equal(19m, Assert.Single(tradeContext.Summaries).Amount);
        AssertDraft();
        await database.Provider.GetRequiredService<ITradeDeletionStore>().DeleteAsync(trade.Id);

        tradeContext.OnDataCommitted();
        await tradeContext.LoadTask;

        Assert.Null(tradeContext.ErrorMessage);
        Assert.True(tradeContext.IsEmpty);
        Assert.Equal(0, tradeContext.ClosedTradeCount);
        Assert.Empty(tradeContext.Rows);
        Assert.Empty(tradeContext.Summaries);
        AssertDraft();
        Assert.Equivalent(savedBefore, await ReadJournalsAsync(database), strict: true);
        Assert.Equal("Saved journal", (await database.Repository.GetAsync(TradingDate, account.Id))!.Entry.Text);
        Assert.Single(savedBefore.Entries);
        Assert.Single(savedBefore.Revisions);

        void AssertDraft()
        {
            Assert.Equal(draft, editor.Text);
            Assert.True(editor.IsDirty);
            Assert.Equal(1L, editor.Revision);
            Assert.Equal(TradingDate.ToDateTime(TimeOnly.MinValue), editor.SelectedDate);
            Assert.Equal(account.Id, editor.SelectedAccount.Id);
        }
    }

    [Fact]
    public async Task ReadOnlyContextReturnsAllClosedRowsSeparateCurrenciesPeakSizeAndHistoricalClassifications()
    {
        await using JournalTestDatabase database = await JournalTestDatabase.CreateAsync();
        var (account, instrument) = await SeedReferencesAsync(database);
        var (archive, inactiveInstrument) = await SeedReferencesAsync(database, "Archive", "OLD", inactive: true);
        Trade[] ordinary = Enumerable.Range(0, 21).Select(index =>
            ClosedTrade(account.Id, instrument.Id, ClosingTime.AddMinutes(index + 1), 1m)).ToArray();
        Guid scaledId = Guid.NewGuid();
        Trade scaled = Trade.Rehydrate(scaledId, account.Id, instrument.Id, new(1m, "USD"), null,
            [new TradeExecution(scaledId, 1, ClosingTime.AddMinutes(-4), ExecutionSide.Buy, 2m, 100m, 0m, 0m, null, null, null),
             new TradeExecution(scaledId, 2, ClosingTime.AddMinutes(-3), ExecutionSide.Sell, 1m, 100m, 0m, 0m, null, null, null),
             new TradeExecution(scaledId, 3, ClosingTime.AddMinutes(-2), ExecutionSide.Buy, 2m, 100m, 0m, 0m, null, null, null),
             new TradeExecution(scaledId, 4, ClosingTime, ExecutionSide.Sell, 3m, 110m, 0m, 0m, null, null, null)], AuditTime, AuditTime);
        Trade estimated = ClosedTrade(account.Id, instrument.Id, ClosingTime.AddMinutes(-2), -5m, costs: null);
        Trade zero = ClosedTrade(account.Id, instrument.Id, ClosingTime.AddMinutes(-1), 0m);
        Trade euros = ClosedTrade(archive.Id, inactiveInstrument.Id, ClosingTime.AddMinutes(22), 12m, currency: "EUR");
        Trade[] all = [.. ordinary, scaled, estimated, zero, euros];
        await AddTradesAsync(database, all);
        await AssignHistoricalClassificationsAsync(database, scaled.Id);
        ReadSnapshot before = await ReadAllAsync(database);
        string readOnlyConnectionString;
        await using (JournalDbContext context = await database.ContextFactory.CreateDbContextAsync())
        {
            readOnlyConnectionString = new SqliteConnectionStringBuilder(context.Database.GetConnectionString())
            {
                Mode = SqliteOpenMode.ReadOnly,
            }.ToString();
        }
        var factory = new ReadOnlyContextFactory(new DbContextOptionsBuilder<JournalDbContext>()
            .UseSqlite(readOnlyConnectionString).Options);
        var tradeContext = new JournalTradeContextViewModel(new TradingCalendarDayReader(factory), new TradingAccountReader(factory));
        try
        {
            await tradeContext.ActivateAsync(TradingDate, null);

            Assert.Null(tradeContext.ErrorMessage);
            Assert.False(tradeContext.IsEmpty);
            Assert.Equal(25, tradeContext.ClosedTradeCount);
            Assert.Equal(all.OrderByDescending(trade => trade.ClosedAtUtc).ThenBy(trade => trade.Id).Select(trade => trade.Id),
                tradeContext.Rows.Select(row => row.Trade.Id));
            Assert.Equal(2, tradeContext.Summaries.Count);
            var dollars = tradeContext.Summaries.Single(summary => summary.Currency == "USD");
            Assert.Equal(24, dollars.Metrics.ClosedTradeCount);
            Assert.Equal(46m, dollars.Amount);
            Assert.True(dollars.Metrics.EffectiveNet.IsEstimated);
            Assert.Null(dollars.Metrics.Net.Total);
            Assert.Equal(12m, tradeContext.Summaries.Single(summary => summary.Currency == "EUR").Amount);
            var scaledRow = tradeContext.Rows.Single(row => row.Trade.Id == scaled.Id);
            Assert.Equal(3m, scaledRow.Trade.Size);
            Assert.Equal("Historical setup (inactive)", scaledRow.SetupText);
            Assert.Equal("Historical mistake (inactive)", scaledRow.MistakesText);
            Assert.Equal("Journal account", scaledRow.AccountText);
            Assert.Equal("JCTX", scaledRow.InstrumentText);
            var archiveRow = tradeContext.Rows.Single(row => row.Trade.Id == euros.Id);
            Assert.Equal("Archive (inactive)", archiveRow.AccountText);
            Assert.Equal("OLD (inactive)", archiveRow.InstrumentText);
            Assert.Equal("None assigned", archiveRow.SetupText);
            Assert.Equal("None assigned", archiveRow.MistakesText);
            Assert.Null(tradeContext.Rows.Single(row => row.Trade.Id == estimated.Id).Trade.NetPnL);
            Assert.True(tradeContext.Rows.Single(row => row.Trade.Id == estimated.Id).IsEstimated);
            Assert.Equal(0m, tradeContext.Rows.Single(row => row.Trade.Id == zero.Id).Amount);
            Assert.False(tradeContext.Rows.Single(row => row.Trade.Id == zero.Id).IsEstimated);

            await tradeContext.SetScopeAsync(TradingDate, account.Id);
            Assert.Null(tradeContext.ErrorMessage);
            Assert.Equal(24, tradeContext.ClosedTradeCount);
            Assert.Equal("USD", Assert.Single(tradeContext.Summaries).Currency);
            Assert.All(tradeContext.Rows, row => Assert.Equal(account.Id, row.Trade.TradingAccountId));
            await tradeContext.SetScopeAsync(TradingDate, archive.Id);
            Assert.Null(tradeContext.ErrorMessage);
            Assert.Equal(euros.Id, Assert.Single(tradeContext.Rows).Trade.Id);
            Assert.Equal(12m, Assert.Single(tradeContext.Summaries).Amount);
            await tradeContext.RefreshCommand.ExecuteAsync(null);
            Assert.Null(tradeContext.ErrorMessage);
            Assert.Equal(euros.Id, Assert.Single(tradeContext.Rows).Trade.Id);
            Assert.Equivalent(before, await ReadAllAsync(database), strict: true);
            Assert.Empty(before.Journals.Entries);
            Assert.Empty(before.Journals.Revisions);
        }
        finally
        {
            tradeContext.Deactivate();
            await tradeContext.LoadTask;
            using var connection = new SqliteConnection(readOnlyConnectionString);
            SqliteConnection.ClearPool(connection);
        }
    }

    [Theory]
    [InlineData(2026, 3, 8, 23)]
    [InlineData(2026, 11, 1, 25)]
    public async Task ContextUsesExplicitNewYorkDstDayAndAccountAndExcludesOpenOrPartialTrades(int year, int month, int day, int hours)
    {
        await using JournalTestDatabase database = await JournalTestDatabase.CreateAsync();
        var (account, instrument) = await SeedReferencesAsync(database);
        var (other, _) = await SeedReferencesAsync(database, "Other", "OTHER");
        DateOnly date = new(year, month, day);
        var query = new TradingCalendarDayQuery(date, account.Id);
        Assert.Equal(hours, (query.ClosedBeforeUtc - query.ClosedFromUtc).TotalHours);
        Trade before = ClosedTrade(account.Id, instrument.Id, query.ClosedFromUtc.AddTicks(-1), 100m);
        Trade first = ClosedTrade(account.Id, instrument.Id, query.ClosedFromUtc, 2m);
        Trade last = ClosedTrade(account.Id, instrument.Id, query.ClosedBeforeUtc.AddTicks(-1), 3m);
        Trade after = ClosedTrade(account.Id, instrument.Id, query.ClosedBeforeUtc, 100m);
        Trade otherAccount = ClosedTrade(other.Id, instrument.Id, query.ClosedFromUtc.AddHours(2), 5m);
        Guid openId = Guid.NewGuid(), partialId = Guid.NewGuid();
        Trade open = Trade.Rehydrate(openId, account.Id, instrument.Id, new(1m, "USD"), null,
            [new TradeExecution(openId, 1, query.ClosedFromUtc.AddHours(1), ExecutionSide.Buy, 1m, 100m, 0m, 0m, null, null, null)], AuditTime, AuditTime);
        Trade partial = Trade.Rehydrate(partialId, account.Id, instrument.Id, new(1m, "USD"), null,
            [new TradeExecution(partialId, 1, query.ClosedFromUtc.AddHours(1), ExecutionSide.Buy, 2m, 100m, 0m, 0m, null, null, null),
             new TradeExecution(partialId, 2, query.ClosedFromUtc.AddHours(2), ExecutionSide.Sell, 1m, 110m, 0m, 0m, null, null, null)], AuditTime, AuditTime);
        await AddTradesAsync(database, before, first, last, after, otherAccount, open, partial);
        ReadSnapshot snapshot = await ReadAllAsync(database);
        JournalTradeContextViewModel tradeContext = database.CreateTradeContext();

        await tradeContext.ActivateAsync(date, account.Id);

        Assert.Null(tradeContext.ErrorMessage);
        Assert.Equal(new[] { last.Id, first.Id }, tradeContext.Rows.Select(row => row.Trade.Id));
        Assert.Equal(2, tradeContext.ClosedTradeCount);
        Assert.Equal(query.ClosedFromUtc, tradeContext.Rows[1].Trade.ClosedAtUtc);
        Assert.Equal(query.ClosedBeforeUtc.AddTicks(-1), tradeContext.Rows[0].Trade.ClosedAtUtc);
        Assert.Equal(5m, Assert.Single(tradeContext.Summaries).Amount);
        await tradeContext.SetScopeAsync(date, null);
        Assert.Equal(3, tradeContext.ClosedTradeCount);
        Assert.Equal(10m, Assert.Single(tradeContext.Summaries).Amount);
        await tradeContext.SetScopeAsync(date.AddDays(3), null);
        Assert.Null(tradeContext.ErrorMessage);
        Assert.True(tradeContext.IsEmpty);
        Assert.Equal(0, tradeContext.ClosedTradeCount);
        Assert.Empty(tradeContext.Rows);
        Assert.Empty(tradeContext.Summaries);
        Assert.Equivalent(snapshot, await ReadAllAsync(database), strict: true);
        Assert.Empty(snapshot.Journals.Entries);
        Assert.Empty(snapshot.Journals.Revisions);
    }

    [Fact]
    public async Task MissingHistoricalReferencesRemainVisibleInAllAccountsWithoutRetargetingUnavailableSelection()
    {
        await using JournalTestDatabase database = await JournalTestDatabase.CreateAsync();
        var (account, instrument) = await SeedReferencesAsync(database, inactive: true);
        Trade trade = ClosedTrade(account.Id, instrument.Id, ClosingTime, 7m);
        await AddTradesAsync(database, trade);
        var (setupId, mistakeId) = await AssignHistoricalClassificationsAsync(database, trade.Id);
        JournalTradeContextViewModel tradeContext = database.CreateTradeContext();
        await tradeContext.ActivateAsync(TradingDate, null);
        Assert.Equal("Journal account (inactive)", Assert.Single(tradeContext.Rows).AccountText);
        Assert.Equal("JCTX (inactive)", tradeContext.Rows[0].InstrumentText);

        // Simulate damaged historical references only inside this isolated synthetic database.
        await using (JournalDbContext context = await database.ContextFactory.CreateDbContextAsync())
        {
            await context.Database.OpenConnectionAsync();
            await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF");
            await context.TradingAccounts.Where(row => row.Id == account.Id).ExecuteDeleteAsync();
            await context.Instruments.Where(row => row.Id == instrument.Id).ExecuteDeleteAsync();
            await context.TradingSetups.Where(row => row.Id == setupId).ExecuteDeleteAsync();
            await context.TradingMistakes.Where(row => row.Id == mistakeId).ExecuteDeleteAsync();
            await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON");
        }
        ReadSnapshot before = await ReadAllAsync(database);

        tradeContext.OnDataCommitted();
        await tradeContext.LoadTask;

        Assert.Null(tradeContext.ErrorMessage);
        var row = Assert.Single(tradeContext.Rows);
        Assert.Equal(trade.Id, row.Trade.Id);
        Assert.Equal("Unavailable Account", row.AccountText);
        Assert.Equal("Unavailable Instrument", row.InstrumentText);
        Assert.Equal("Unavailable Setup", row.SetupText);
        Assert.Equal("Unavailable Mistake", row.MistakesText);
        Assert.Equal(7m, row.Amount);
        Assert.Equal(7m, Assert.Single(tradeContext.Summaries).Amount);
        await tradeContext.SetScopeAsync(TradingDate, account.Id);
        Assert.NotNull(tradeContext.ErrorMessage);
        Assert.Empty(tradeContext.Rows);
        Assert.Empty(tradeContext.Summaries);
        Assert.Equivalent(before, await ReadAllAsync(database), strict: true);
        Assert.Empty(before.Journals.Entries);
        Assert.Empty(before.Journals.Revisions);
    }

    private static async Task<(TradingAccount Account, Instrument Instrument)> SeedReferencesAsync(
        JournalTestDatabase database, string accountName = "Journal account", string symbol = "JCTX", bool inactive = false)
    {
        var account = new TradingAccount(accountName, TradingAccountType.Personal, null, null, "USD", 0m, AuditTime);
        var instrument = new Instrument(symbol, "Journal context synthetic instrument", AssetClass.Futures, "CME", "USD", 1m, 1m, AuditTime);
        if (inactive)
        {
            account.Deactivate(AuditTime.AddDays(1));
            instrument.Deactivate(AuditTime.AddDays(1));
        }
        await database.Provider.GetRequiredService<ITradingAccountStore>().AddAsync(account);
        await database.Provider.GetRequiredService<IInstrumentStore>().AddAsync(instrument);
        return (account, instrument);
    }

    private static Trade ClosedTrade(Guid accountId, Guid instrumentId, DateTimeOffset close, decimal gross,
        decimal? costs = 0m, string currency = "USD")
    {
        Guid id = Guid.NewGuid();
        return Trade.Rehydrate(id, accountId, instrumentId, new(1m, currency), null,
            [new TradeExecution(id, 1, close.AddMinutes(-1), ExecutionSide.Buy, 1m, 1000m, 0m, 0m, null, null, null),
             new TradeExecution(id, 2, close, ExecutionSide.Sell, 1m, 1000m + gross, costs, 0m, null, null, null)], AuditTime, AuditTime);
    }

    private static async Task AddTradesAsync(JournalTestDatabase database, params Trade[] trades)
    {
        var store = database.Provider.GetRequiredService<ITradeStore>();
        foreach (Trade trade in trades) await store.AddAsync(trade);
    }

    private static async Task<(Guid SetupId, Guid MistakeId)> AssignHistoricalClassificationsAsync(JournalTestDatabase database, Guid tradeId)
    {
        Guid setupId = Guid.NewGuid(), mistakeId = Guid.NewGuid();
        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        context.TradingSetups.Add(new TradingSetupRecord
        {
            Id = setupId, Name = "Historical setup", IsActive = false, CreatedAtUtc = AuditTime, UpdatedAtUtc = AuditTime,
        });
        context.TradingMistakes.Add(new TradingMistakeRecord
        {
            Id = mistakeId, Name = "Historical mistake", IsActive = false, CreatedAtUtc = AuditTime, UpdatedAtUtc = AuditTime,
        });
        (await context.Trades.SingleAsync(trade => trade.Id == tradeId)).TradingSetupId = setupId;
        context.TradeMistakes.Add(new TradeMistakeRecord
        {
            Id = Guid.NewGuid(), TradeId = tradeId, TradingMistakeId = mistakeId, CreatedAtUtc = AuditTime, UpdatedAtUtc = AuditTime,
        });
        await context.SaveChangesAsync();
        return (setupId, mistakeId);
    }

    private static async Task<JournalSnapshot> ReadJournalsAsync(JournalTestDatabase database)
    {
        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        return new(await context.DailyJournals.AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(),
            await context.DailyJournalRevisions.AsNoTracking().OrderBy(row => row.JournalId).ThenBy(row => row.Revision).ToArrayAsync());
    }

    private static async Task<ReadSnapshot> ReadAllAsync(JournalTestDatabase database)
    {
        JournalSqliteTests.TradeDataSnapshot trades = await database.ReadTradeDataAsync();
        JournalSnapshot journals = await ReadJournalsAsync(database);
        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        return new(trades, journals,
            await context.TradingAccounts.AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(),
            await context.Instruments.AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(),
            await context.TradingSetups.AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(),
            await context.TradingMistakes.AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(),
            await context.TradeMistakes.AsNoTracking().OrderBy(row => row.Id).ToArrayAsync());
    }

    private sealed record JournalSnapshot(DailyJournalRecord[] Entries, DailyJournalRevisionRecord[] Revisions);
    private sealed record ReadSnapshot(JournalSqliteTests.TradeDataSnapshot Trades, JournalSnapshot Journals,
        TradingAccountRecord[] Accounts, InstrumentRecord[] Instruments, TradingSetupRecord[] Setups,
        TradingMistakeRecord[] Mistakes, TradeMistakeRecord[] Assignments);
    private sealed class ReadOnlyContextFactory(DbContextOptions<JournalDbContext> options) : IDbContextFactory<JournalDbContext>
    {
        public JournalDbContext CreateDbContext() => new(options);
    }
}
