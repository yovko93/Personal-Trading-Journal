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
    public async Task SaturdayAndAdjacentDateSelectionReadOwnDateWhileGridRetainsWeeklyTotals()
    {
        var reader = new FakeTradingCalendarDayReader
        {
            Handler = (q, ct) => Task.FromResult(TradingCalendarDayDetails.Create(q.Date, [Row(q.Date, 7m, 7m)], ct))
        };
        CalendarViewModel vm = await CalendarSummaryFixture.CreateAsync(reader);
        CalendarDayCell saturday = Cell(vm, new(2026, 9, 5));
        Assert.Equal(-205m, Assert.Single(saturday.WeeklySummaries).Amount);
        await vm.SelectDayCommand.ExecuteAsync(saturday);
        Assert.Equal(saturday.Date, vm.SelectedDate);
        Assert.True(saturday.IsSelected);
        Assert.False(saturday.ShowsDailySummary);
        Assert.Equal(1, vm.DayDetails!.ClosedTradeCount);
        Assert.Equal(7m, Assert.Single(vm.DaySummaries).Amount);
        Assert.Equal("1 closed Trade", vm.DayTradeCountText);
        Assert.Equal(5, vm.Weeks.Count);
        Assert.Null(Assert.Single(reader.Calls).Query.TradingAccountId);
        await vm.SelectDayCommand.ExecuteAsync(Cell(vm, new(2026, 8, 31)));
        Assert.Equal(new DateOnly(2026, 8, 31), vm.SelectedDate);
        Assert.False(saturday.IsSelected);
        Assert.Equal(new DateOnly(2026, 9, 1), vm.SelectedMonth);
        Assert.Equal(2, reader.Calls.Count);
    }

    [Fact]
    public async Task RapidDateAndMonthChangesDiscardOldDayResponsesAndCancelOldQueries()
    {
        var pending = new ConcurrentDictionary<DateOnly, TaskCompletionSource<TradingCalendarDayDetails>>();
        var started = new ConcurrentDictionary<DateOnly, TaskCompletionSource>();
        var reader = new FakeTradingCalendarDayReader
        {
            Handler = (q, ct) =>
            {
                var completion = pending.GetOrAdd(q.Date, _ => new(TaskCreationOptions.RunContinuationsAsynchronously));
                started.GetOrAdd(q.Date, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
                return completion.Task; // Deliberately completes despite cancellation.
            }
        };
        var vm = await CalendarSummaryFixture.CreateAsync(reader);
        DateOnly first = new(2026, 9, 5), second = first.AddDays(1), third = second.AddDays(1);
        Task a = vm.SelectDayCommand.ExecuteAsync(Cell(vm, first));
        await WaitStarted(first);
        Task b = vm.SelectDayCommand.ExecuteAsync(Cell(vm, second));
        await WaitStarted(second);
        Assert.True(reader.Calls.First().Token.IsCancellationRequested);
        pending[second].SetResult(TradingCalendarDayDetails.Create(second, [Row(second, 11m, 11m)]));
        await b;
        pending[first].SetResult(TradingCalendarDayDetails.Create(first, [Row(first, 7m, 7m)]));
        await a;
        Assert.Equal(second, vm.DayDetails!.Date);
        Assert.Equal(11m, Assert.Single(vm.DaySummaries).Amount);
        Assert.Equal(11m, Assert.Single(Assert.Single(vm.DayPerformance).Points).Value);
        Task c = vm.SelectDayCommand.ExecuteAsync(Cell(vm, third));
        await WaitStarted(third);
        vm.NextCommand.Execute(null);
        Assert.Null(vm.SelectedDate);
        pending[third].SetResult(TradingCalendarDayDetails.Create(third, [Row(third, 99m, 99m)]));
        await c;
        Assert.Null(vm.DayDetails);
        Assert.Empty(vm.DayTrades);
        Assert.Empty(vm.DayPerformance);
        Assert.False(vm.HasSelectedDate);
        await vm.LoadTask;

        async Task WaitStarted(DateOnly date) => await started.GetOrAdd(date,
            _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task CancellationFailureEmptyAndRetryKeepCurrentDateUsable()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = new TaskCompletionSource<TradingCalendarDayDetails>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reader = new FakeTradingCalendarDayReader { Handler = (_, _) => { started.SetResult(); return result.Task; } };
        var vm = await CalendarSummaryFixture.CreateAsync(reader);
        DateOnly date = new(2026, 9, 5);
        Task load = vm.SelectDayCommand.ExecuteAsync(Cell(vm, date));
        await started.Task;
        vm.CancelDayCommand.Execute(null);
        result.SetResult(TradingCalendarDayDetails.Create(date, [Row(date, 1m, 1m)]));
        await load;
        Assert.Equal(date, vm.SelectedDate);
        Assert.False(vm.IsDayLoading);
        Assert.Null(vm.DayDetails);
        Assert.True(reader.Calls.First().Token.IsCancellationRequested);
        reader.Handler = (_, _) => throw new IOException("Synthetic day read failure");
        await vm.RetryDayCommand.ExecuteAsync(null);
        Assert.Contains("Retry", vm.DayErrorMessage);
        Assert.Empty(vm.DayTrades);
        reader.Handler = (q, ct) => Task.FromResult(TradingCalendarDayDetails.Create(q.Date, [], ct));
        await vm.RetryDayCommand.ExecuteAsync(null);
        Assert.Null(vm.DayErrorMessage);
        Assert.True(vm.IsSelectedDayEmpty);
        Assert.Equal("0 closed Trades", vm.DayTradeCountText);
        Assert.Empty(vm.DaySummaries);
    }

    [Fact]
    public async Task ViewTargetsExactCurrentRowAndStaleRowsAreDisabledAfterSelectionChanges()
    {
        var reader = new FakeTradingCalendarDayReader
        {
            Handler = (q, ct) => Task.FromResult(TradingCalendarDayDetails.Create(q.Date, [Row(q.Date, -285m, null)], ct))
        };
        var vm = await CalendarSummaryFixture.CreateAsync(reader);
        await vm.SelectDayCommand.ExecuteAsync(Cell(vm, new(2026, 9, 5)));
        TradeListItem row = Assert.Single(vm.DayTrades).Trade;
        Assert.True(vm.ViewTradeCommand.CanExecute(row));
        await vm.ViewTradeCommand.ExecuteAsync(row);
        Assert.Equal(row.Id, Assert.Single(vm.DayTrades, t => t.IsExpanded).Trade.Id);
        await vm.SelectDayCommand.ExecuteAsync(Cell(vm, new(2026, 9, 6)));
        Assert.False(vm.ViewTradeCommand.CanExecute(row));
        await vm.ViewTradeCommand.ExecuteAsync(row);
        Assert.DoesNotContain(vm.DayTrades, t => t.IsExpanded);
        TradeListItem current = Assert.Single(vm.DayTrades).Trade;
        await vm.ViewTradeCommand.ExecuteAsync(current);
        Assert.Equal(current.Id, Assert.Single(vm.DayTrades, t => t.IsExpanded).Trade.Id);
        vm.NextCommand.Execute(null);
        Assert.False(vm.ViewTradeCommand.CanExecute(current));
        await vm.LoadTask;
    }

    [Fact]
    public void RowPresentationKeepsEstimateZeroUnknownAndDstOverlapDistinct()
    {
        DateOnly date = new(2026, 11, 1);
        var first = new CalendarTradePresentation(Row(date, -285m, null) with { ClosedAtUtc = new(2026, 11, 1, 5, 30, 0, TimeSpan.Zero) });
        var second = new CalendarTradePresentation(first.Trade with { ClosedAtUtc = new(2026, 11, 1, 6, 30, 0, TimeSpan.Zero) });
        Assert.Equal("01:30:00", first.ClosingTime);
        Assert.Equal("01:30:00", second.ClosingTime);
        Assert.Contains("UTC-04:00", first.ClosingTimeDescription);
        Assert.Contains("UTC-05:00", second.ClosingTimeDescription);
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
