using System.Collections.Concurrent;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Desktop.Tests.CalendarPage;

public sealed class CalendarDayDetailsTests
{
    [Fact]
    public async Task OpeningAndNavigatingDaysLoadsJournalsWithoutReadingTrades()
    {
        var repository = new FakeDailyJournalRepository();
        var tradeReader = new FakeTradingCalendarDayReader();
        var vm = await CalendarSummaryFixture.CreateAsync(journalStatusReader: repository, journalRepository: repository,
            journalTradeContextReader: tradeReader);
        var populated = Cell(vm, new(2026, 9, 5));
        var empty = Cell(vm, new(2026, 9, 10));
        await repository.CreateAsync(new(populated.Date, null, "Populated date Journal"));
        await repository.CreateAsync(new(empty.Date, null, "Empty date Journal"));

        Assert.True(populated.HasDailyTrades);
        Assert.NotEmpty(populated.WeeklySummaries);
        Assert.False(empty.HasDailyTrades);
        await vm.SelectDayCommand.ExecuteAsync(populated);
        Assert.Equal(populated.Date, vm.SelectedDate);
        Assert.True(populated.IsSelected);
        Assert.Equal("Populated date Journal", Assert.Single(vm.DayJournals).Text);
        await vm.SelectDayCommand.ExecuteAsync(empty);
        Assert.Equal(empty.Date, vm.SelectedDate);
        Assert.True(empty.IsSelected);
        Assert.False(populated.IsSelected);
        Assert.Equal("Empty date Journal", Assert.Single(vm.DayJournals).Text);

        await vm.OpenDayJournalCommand.ExecuteAsync(Assert.Single(vm.DayJournals));
        Assert.NotNull(vm.InlineJournal);
        Assert.Empty(tradeReader.Calls);
        vm.NextCommand.Execute(null);
        await vm.LoadTask;
        Assert.Equal(new DateOnly(2026, 10, 1), vm.SelectedMonth);
        Assert.Null(vm.SelectedDate);
        Assert.Empty(vm.DayJournals);
        Assert.Empty(tradeReader.Calls);
        vm.Deactivate();
    }

    [Fact]
    public void RowPresentationKeepsEstimateZeroUnknownAndDstOverlapDistinct()
    {
        DateOnly date = new(2026, 11, 1);
        var first = new CalendarTradePresentation(Row(date, -285m, null) with { ClosedAtUtc = new(2026, 11, 1, 5, 30, 0, TimeSpan.Zero) });
        var second = new CalendarTradePresentation(first.Trade with { ClosedAtUtc = new(2026, 11, 1, 6, 30, 0, TimeSpan.Zero) });
        Assert.Equal("01:30:00", first.ClosingTime);
        Assert.Equal("01:30:00", second.ClosingTime);
        Assert.Contains("UTC-4", first.ClosingTimeDescription);
        Assert.Contains("UTC-5", second.ClosingTimeDescription);
        Assert.Equal(-285m, first.Amount);
        Assert.True(first.IsEstimated);
        Assert.Contains("commission/fees unknown", first.NetDescription);
        Assert.Equal("2", first.SizeText);
        var zero = new CalendarTradePresentation(Row(date, 0m, 0m));
        Assert.Equal(0m, zero.Amount);
        Assert.False(zero.IsEstimated);
        Assert.Contains("Verified", zero.NetDescription);
        var unknown = new CalendarTradePresentation(Row(date, null, null));
        Assert.Null(unknown.Amount);
        Assert.Equal("— USD", unknown.AmountText);
    }

    internal static CalendarDayCell Cell(CalendarViewModel vm, DateOnly date) => vm.Weeks.SelectMany(w => w.Days).Single(d => d.Date == date);
    internal static TradeListItem Row(DateOnly date, decimal? gross, decimal? net) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Calendar historical account", Guid.NewGuid(), "MNQ", TradeDirection.Long, TradeStatus.Closed,
            new DateTimeOffset(date.ToDateTime(new TimeOnly(15, 0)), TimeSpan.Zero),
            new DateTimeOffset(date.ToDateTime(new TimeOnly(16, 0)), TimeSpan.Zero),
            0m, 100m, 101m, net.HasValue ? gross - net : null, gross, net, "USD", 2m);
}
