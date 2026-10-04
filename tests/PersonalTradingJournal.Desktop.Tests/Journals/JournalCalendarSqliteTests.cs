using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Desktop.ViewModels.Journals;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Journals;
using PersonalTradingJournal.Domain.Trades;
using JournalTestDatabase = PersonalTradingJournal.Desktop.Tests.Journals.JournalSqliteTests.JournalTestDatabase;

namespace PersonalTradingJournal.Desktop.Tests.Journals;

public sealed class JournalCalendarSqliteTests
{
    private static readonly DateTimeOffset AuditTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DailyReviewAnswers Answers = new("Followed plan", "Wait longer", "Use confirmation");

    [Fact]
    public async Task EmptyAdjacentSaturdayOpensExactAllAccountsJournalAndCommittedReviewStatesRefreshWithoutChangingTrades()
    {
        await using JournalTestDatabase database = await JournalTestDatabase.CreateAsync();
        var calendar = Calendar(database);
        var before = await database.ReadTradeDataAsync();
        DateOnly saturday = new(2026, 10, 3);
        JournalViewModel? editor = null;
        try
        {
            await calendar.ActivateAsync();
            calendar.SelectedCurrency = "USD";
            await calendar.LoadTask;
            var cell = Cell(calendar, saturday);
            Assert.False(cell.IsInDisplayedMonth);
            Assert.True(cell.IsSaturday);
            Assert.False(cell.HasJournal);
            await calendar.SelectDayCommand.ExecuteAsync(cell);
            Assert.True(calendar.IsSelectedDayEmpty);
            Assert.Equal("Add Journal", calendar.DayJournalActionText);
            calendar.OpenJournalAsync = async (date, account) =>
            {
                Assert.Equal(saturday, date);
                Assert.Null(account.Id);
                calendar.Deactivate();
                editor = await database.OpenEditorAsync(date, account.Id);
                editor.JournalDataCommitted += (_, _) => calendar.OnJournalCommitted();
            };

            await calendar.NavigateToJournalAsync(saturday, calendar.SelectedAccount);
            Assert.NotNull(editor);
            Assert.True(editor.TradeContext.IsEmpty);
            Assert.Equal(saturday.ToDateTime(TimeOnly.MinValue), editor.SelectedDate);
            Assert.Null(editor.SelectedAccount.Id);
            await editor.SaveCommand.ExecuteAsync(null); // Even an empty trading-day draft is a real entry.
            Assert.Null(editor.ErrorMessage);

            await calendar.ActivateAsync();
            AssertRetainedScope();
            Assert.Equal("Draft", Cell(calendar, saturday).JournalStatusText);
            Assert.Equal("Continue Journal", calendar.DayJournalActionText);
            Assert.Equal(1L, calendar.SelectedDayJournalStatus!.Revision);

            editor.WentWell = Answers.WentWell;
            editor.NeedsImprovement = Answers.NeedsImprovement;
            editor.NextTradingDay = Answers.NextTradingDay;
            await editor.CompleteReviewCommand.ExecuteAsync(null);
            await calendar.JournalLoadTask;
            Assert.Null(editor.ErrorMessage);
            Assert.Equal("Completed", Cell(calendar, saturday).JournalStatusText);
            Assert.Equal("Open Journal", calendar.DayJournalActionText);
            Assert.Equal(2L, calendar.SelectedDayJournalStatus!.Revision);

            await editor.ReopenReviewCommand.ExecuteAsync(null);
            await calendar.JournalLoadTask;
            Assert.Equal("Draft", Cell(calendar, saturday).JournalStatusText);
            Assert.Equal(3L, calendar.SelectedDayJournalStatus!.Revision);
            AssertRetainedScope();
            Assert.Empty(Cell(calendar, saturday).DailySummaries);
            Assert.Empty(Cell(calendar, saturday).WeeklySummaries);
            Assert.True(Cell(calendar, saturday).HasEmptyWeek);
            Assert.Empty(calendar.MonthlySummaries);
            Assert.Equivalent(before, await database.ReadTradeDataAsync(), strict: true);
            var saved = Assert.IsType<DailyJournalDetails>(await database.Repository.GetAsync(saturday));
            Assert.Null(saved.Entry.TradingAccountId);
            Assert.Equal(new[] { true, false, true }, (await database.Repository.GetHistoryAsync(saved.Entry.Id)).Select(r => r.IsDraft));

            void AssertRetainedScope()
            {
                Assert.Equal(new DateOnly(2026, 9, 1), calendar.SelectedMonth);
                Assert.Equal(saturday, calendar.SelectedDate);
                Assert.Null(calendar.SelectedAccount.Id);
                Assert.Equal("USD", calendar.SelectedCurrency);
                Assert.True(calendar.IsSelectedDayEmpty);
            }
        }
        finally
        {
            calendar.Deactivate();
            await Task.WhenAll(calendar.LoadTask, calendar.DayLoadTask, calendar.JournalLoadTask);
        }
    }

    [Fact]
    public async Task InactiveAccountJournalIsDistinctAndCurrencyFilteringDoesNotChangeJournalIndicatorsOrSourceEconomics()
    {
        await using JournalTestDatabase database = await JournalTestDatabase.CreateAsync();
        var account = new TradingAccount("Historical Calendar account", TradingAccountType.Personal, null, null, "USD", 0m, AuditTime);
        account.Deactivate(AuditTime.AddDays(1));
        await database.Provider.GetRequiredService<ITradingAccountStore>().AddAsync(account);
        var instrument = new Instrument("JCI", "Journal Calendar synthetic", AssetClass.Futures, "CME", "USD", 1m, 1m, AuditTime);
        await database.Provider.GetRequiredService<IInstrumentStore>().AddAsync(instrument);
        DateOnly saturday = new(2026, 9, 12);
        var tradeStore = database.Provider.GetRequiredService<ITradeStore>();
        await tradeStore.AddAsync(ClosedTrade(account.Id, instrument.Id, new(2026, 9, 12, 16, 0, 0, TimeSpan.Zero), "EUR", 20m));
        await tradeStore.AddAsync(ClosedTrade(account.Id, instrument.Id, new(2026, 9, 13, 16, 0, 0, TimeSpan.Zero), "USD", 7m));
        var all = (await database.Repository.CreateAsync(new(saturday, null, "All accounts draft"))).Journal!;
        var specific = (await database.Repository.CreateAsync(new(saturday, account.Id, "Historical account review", false, Answers))).Journal!;
        var before = await database.ReadTradeDataAsync();
        var calendar = Calendar(database);
        try
        {
            await calendar.ActivateAsync();
            await calendar.SelectDayCommand.ExecuteAsync(Cell(calendar, saturday));
            Assert.Equal(all.Entry.Id, calendar.SelectedDayJournalStatus!.JournalId);
            Assert.Equal("Draft", Cell(calendar, saturday).JournalStatusText);

            calendar.SelectedAccount = calendar.Accounts.Single(a => a.Id == account.Id);
            await calendar.LoadTask;
            Assert.Contains("inactive", calendar.SelectedAccount.Name);
            Assert.Equal(specific.Entry.Id, calendar.SelectedDayJournalStatus!.JournalId);
            Assert.Equal("Completed", Cell(calendar, saturday).JournalStatusText);
            Assert.Equal(1, calendar.DayDetails!.ClosedTradeCount);
            Assert.Equal("EUR", Assert.Single(calendar.DaySummaries).Currency);
            Assert.Equal(new[] { "EUR", "USD" }, Cell(calendar, saturday).WeeklySummaries.Select(s => s.Currency));

            calendar.SelectedCurrency = "USD";
            await calendar.LoadTask;
            Assert.Equal(account.Id, calendar.SelectedAccount.Id);
            Assert.Equal(specific.Entry.Id, calendar.SelectedDayJournalStatus!.JournalId);
            Assert.Equal("Completed", Cell(calendar, saturday).JournalStatusText);
            Assert.True(calendar.IsSelectedDayEmpty); // Saturday's Trade is EUR; Sunday contributes USD to the week.
            Assert.Equal(7m, Assert.Single(Cell(calendar, saturday).WeeklySummaries).Amount);
            Assert.Equal(1, Assert.Single(Cell(calendar, saturday).WeeklySummaries).Metrics.ClosedTradeCount);

            var editor = await database.OpenEditorAsync(saturday, account.Id);
            Assert.True(editor.IsCompleted);
            Assert.Contains("inactive", editor.SelectedAccount.Name);
            editor.JournalDataCommitted += (_, _) => calendar.OnJournalCommitted();
            await editor.ReopenReviewCommand.ExecuteAsync(null);
            await calendar.JournalLoadTask;
            Assert.Equal("Draft", Cell(calendar, saturday).JournalStatusText);
            Assert.Equal(2L, calendar.SelectedDayJournalStatus!.Revision);
            await editor.CompleteReviewCommand.ExecuteAsync(null);
            await calendar.JournalLoadTask;
            Assert.Equal("Completed", Cell(calendar, saturday).JournalStatusText);
            Assert.Equal(3L, calendar.SelectedDayJournalStatus!.Revision);
            Assert.Equal("USD", calendar.SelectedCurrency);
            Assert.True(calendar.IsSelectedDayEmpty);

            calendar.Deactivate();
            await calendar.ActivateAsync();
            Assert.Equal(new DateOnly(2026, 9, 1), calendar.SelectedMonth);
            Assert.Equal(saturday, calendar.SelectedDate);
            Assert.Equal(account.Id, calendar.SelectedAccount.Id);
            Assert.Equal("USD", calendar.SelectedCurrency);
            Assert.Equal(specific.Entry.Id, calendar.SelectedDayJournalStatus!.JournalId);

            calendar.SelectedAccount = calendar.Accounts.Single(a => a.Id is null);
            await calendar.LoadTask;
            Assert.Equal(all.Entry.Id, calendar.SelectedDayJournalStatus!.JournalId);
            Assert.Equal("Draft", Cell(calendar, saturday).JournalStatusText);
            Assert.Equal(1L, calendar.SelectedDayJournalStatus!.Revision);
            Assert.Equal("USD", calendar.SelectedCurrency);
            Assert.Equivalent(before, await database.ReadTradeDataAsync(), strict: true);
            Assert.True((await database.Repository.GetAsync(saturday))!.Entry.IsDraft);
            Assert.False((await database.Repository.GetAsync(saturday, account.Id))!.Entry.IsDraft);
        }
        finally
        {
            calendar.Deactivate();
            await Task.WhenAll(calendar.LoadTask, calendar.DayLoadTask, calendar.JournalLoadTask);
        }
    }

    private static CalendarViewModel Calendar(JournalTestDatabase database) => new(
        database.Provider.GetRequiredService<ITradingCalendarReader>(),
        new CalendarClock(),
        database.Provider.GetRequiredService<ITradingCalendarDayReader>(),
        database.Provider.GetRequiredService<ITradingAccountReader>(),
        journalStatusReader: database.Provider.GetRequiredService<IDailyJournalStatusReader>());

    private static CalendarDayCell Cell(CalendarViewModel calendar, DateOnly date) =>
        calendar.Weeks.SelectMany(w => w.Days).Single(d => d.Date == date);

    private static Trade ClosedTrade(Guid accountId, Guid instrumentId, DateTimeOffset closed, string currency, decimal amount)
    {
        Guid id = Guid.NewGuid();
        return Trade.Rehydrate(id, accountId, instrumentId, new(1m, currency), null,
            [new TradeExecution(id, 1, closed.AddHours(-1), ExecutionSide.Buy, 1m, 100m, 0m, 0m, null, null, null),
             new TradeExecution(id, 2, closed, ExecutionSide.Sell, 1m, 100m + amount, 0m, 0m, null, null, null)], AuditTime, AuditTime);
    }

    private sealed class CalendarClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);
    }
}
