using System.Collections.Concurrent;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;

namespace PersonalTradingJournal.Desktop.Tests.CalendarPage;

public sealed class CalendarViewModelTests
{
    [Theory]
    [InlineData(2021, 2, 4, "2021-02-01", "2021-02-28")]
    [InlineData(2026, 9, 5, "2026-08-31", "2026-10-04")]
    [InlineData(2026, 8, 6, "2026-07-27", "2026-09-06")]
    public async Task CompleteMonthGridHasFourFiveOrSixMondaySundayRows(
        int year, int month, int rows, string first, string last)
    {
        var vm = new CalendarViewModel(new ImmediateReader(), new FixedClock(new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero)), new FakeTradingCalendarDayReader(), new FakeTradingAccountReader());
        while (vm.SelectedMonth.Year * 12 + vm.SelectedMonth.Month < year * 12 + month)
            vm.NextCommand.Execute(null);
        while (vm.SelectedMonth.Year * 12 + vm.SelectedMonth.Month > year * 12 + month)
            vm.PreviousCommand.Execute(null);
        await vm.ActivateAsync();

        Assert.Equal(rows, vm.Weeks.Count);
        Assert.Equal(DateOnly.Parse(first), vm.Weeks[0].Days[0].Date);
        Assert.Equal(DateOnly.Parse(last), vm.Weeks[^1].Days[^1].Date);
        Assert.All(vm.Weeks, week =>
        {
            Assert.Equal(7, week.Days.Count);
            Assert.Equal(DayOfWeek.Monday, week.Days[0].Date.DayOfWeek);
            Assert.Equal(DayOfWeek.Sunday, week.Days[6].Date.DayOfWeek);
            Assert.Single(week.Days, day => day.IsSaturday);
        });
        Assert.NotNull(vm.MonthData);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task TodayUsesNewYorkDateAndRestoresCurrentMonth()
    {
        // UTC has reached October, but New York is still in September.
        var clock = new FixedClock(new(2026, 10, 1, 1, 0, 0, TimeSpan.Zero));
        var vm = new CalendarViewModel(new ImmediateReader(), clock, new FakeTradingCalendarDayReader(), new FakeTradingAccountReader());
        Assert.Equal(new DateOnly(2026, 9, 1), vm.SelectedMonth);
        Assert.Single(vm.Weeks.SelectMany(w => w.Days), day => day.IsToday && day.Date.Day == 30);
        await vm.ActivateAsync();
        vm.PreviousCommand.Execute(null);
        Assert.Equal(new DateOnly(2026, 8, 1), vm.SelectedMonth);
        vm.TodayCommand.Execute(null);
        Assert.Equal(new DateOnly(2026, 9, 1), vm.SelectedMonth);
        Assert.Equal(vm.SelectedMonth.ToString("MMMM yyyy", System.Globalization.CultureInfo.CurrentCulture), vm.MonthLabel);
    }

    [Fact]
    public void TodayRefreshesHighlightAfterNewYorkDayChangesWithinSameMonth()
    {
        var clock = new AdjustableClock(new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero));
        var vm = new CalendarViewModel(new ImmediateReader(), clock, new FakeTradingCalendarDayReader(), new FakeTradingAccountReader());
        Assert.Single(vm.Weeks.SelectMany(w => w.Days), day => day.IsToday && day.Date.Day == 9);
        clock.Instant = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);
        vm.TodayCommand.Execute(null);
        Assert.Single(vm.Weeks.SelectMany(w => w.Days), day => day.IsToday && day.Date.Day == 10);
    }

    [Fact]
    public async Task RapidMonthChangeKeepsGridAndDiscardsLateOldResult()
    {
        var reader = new DelayedReader();
        var vm = new CalendarViewModel(reader, new FixedClock(new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero)), new FakeTradingCalendarDayReader(), new FakeTradingAccountReader());
        Task september = vm.ActivateAsync();
        await reader.Seen(new(2026, 9, 1));
        Assert.True(vm.IsLoading);
        Assert.Equal(5, vm.Weeks.Count);
        Assert.Null(vm.MonthData);

        vm.NextCommand.Execute(null);
        Task october = vm.LoadTask;
        await reader.Seen(new(2026, 10, 1));
        Assert.Equal(new DateOnly(2026, 10, 1), vm.SelectedMonth);
        Assert.Equal(new DateOnly(2026, 9, 28), vm.Weeks[0].Days[0].Date);
        Assert.Null(vm.MonthData);
        reader.Complete(new(2026, 10, 1));
        await october;
        Assert.Equal(new DateOnly(2026, 10, 1), vm.MonthData!.MonthStart);
        reader.Complete(new(2026, 9, 1));
        await september;
        Assert.Equal(new DateOnly(2026, 10, 1), vm.MonthData!.MonthStart);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task EmptyErrorCancellationAndRetryRetainUsableDates()
    {
        var reader = new DelayedReader();
        var vm = new CalendarViewModel(reader, new FixedClock(new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero)), new FakeTradingCalendarDayReader(), new FakeTradingAccountReader());
        Task first = vm.ActivateAsync();
        await reader.Seen(new(2026, 9, 1));
        vm.CancelCommand.Execute(null);
        Assert.False(vm.IsLoading);
        Assert.Equal(5, vm.Weeks.Count);
        reader.Complete(new(2026, 9, 1));
        await first;
        Assert.Null(vm.MonthData);

        vm.NextCommand.Execute(null);
        await reader.Seen(new(2026, 10, 1));
        reader.Fail(new(2026, 10, 1));
        await vm.LoadTask;
        Assert.NotNull(vm.ErrorMessage);
        Assert.Equal(new DateOnly(2026, 10, 1), vm.SelectedMonth);
        Assert.NotEmpty(vm.Weeks);

        var recovered = new CalendarViewModel(new ImmediateReader(), new FixedClock(new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero)), new FakeTradingCalendarDayReader(), new FakeTradingAccountReader());
        await recovered.ActivateAsync();
        Assert.Empty(recovered.MonthData!.Currencies);
        Assert.Null(recovered.ErrorMessage);
        Assert.Equal(5, recovered.Weeks.Count);
    }

    private static TradingCalendarMonth Empty(TradingCalendarQuery query)
    {
        DateOnly[] dates = Enumerable.Range(0, query.GridEnd.DayNumber - query.GridStart.DayNumber + 1)
            .Select(i => query.GridStart.AddDays(i)).ToArray();
        return new(query.MonthStart, query.GridStart, query.GridEnd, dates, []);
    }

    private sealed class ImmediateReader : ITradingCalendarReader
    {
        public Task<TradingCalendarMonth> GetAsync(TradingCalendarQuery query, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(Empty(query)); }
    }

    private sealed class DelayedReader : ITradingCalendarReader
    {
        private readonly ConcurrentDictionary<DateOnly, TaskCompletionSource<TradingCalendarMonth>> _results = new();
        private readonly ConcurrentDictionary<DateOnly, TaskCompletionSource> _started = new();
        private readonly ConcurrentDictionary<DateOnly, TradingCalendarQuery> _queries = new();
        public Task Seen(DateOnly month) => _started.GetOrAdd(month, _ => NewSignal()).Task.WaitAsync(TimeSpan.FromSeconds(10));
        public Task<TradingCalendarMonth> GetAsync(TradingCalendarQuery query, CancellationToken cancellationToken = default)
        {
            _queries[query.MonthStart] = query;
            _started.GetOrAdd(query.MonthStart, _ => NewSignal()).TrySetResult();
            // A store may finish after cancellation: the ViewModel generation guard must still reject it.
            return _results.GetOrAdd(query.MonthStart, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        }
        public void Complete(DateOnly month) => _results[month].TrySetResult(Empty(_queries[month]));
        public void Fail(DateOnly month) => _results[month].TrySetException(new IOException("Synthetic failure"));
        private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class FixedClock(DateTimeOffset instant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instant;
    }

    private sealed class AdjustableClock(DateTimeOffset instant) : TimeProvider
    {
        public DateTimeOffset Instant { get; set; } = instant;
        public override DateTimeOffset GetUtcNow() => Instant;
    }
}
