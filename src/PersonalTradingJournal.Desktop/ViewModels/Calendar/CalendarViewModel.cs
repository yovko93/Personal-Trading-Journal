using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Common.Time;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Converters;

namespace PersonalTradingJournal.Desktop.ViewModels.Calendar;

public sealed class CalendarDayCell(DateOnly date, bool isInDisplayedMonth, bool isToday, bool isSaturday) : ObservableObject
{
    public DateOnly Date { get; } = date;
    public bool IsInDisplayedMonth { get; } = isInDisplayedMonth;
    public bool IsToday { get; } = isToday;
    public bool IsSaturday { get; } = isSaturday;
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        internal set { if (SetProperty(ref _isSelected, value)) OnPropertyChanged(nameof(AccessibleName)); }
    }
    public IReadOnlyList<CalendarPnlSummary> DailySummaries { get; init; } = [];
    public IReadOnlyList<CalendarPnlSummary> WeeklySummaries { get; init; } = [];
    public bool IsDataLoaded { get; init; }
    public string WeekLabel { get; init; } = "";
    public bool HasDailyTrades => DailySummaries.Count > 0;
    public bool ShowsDailySummary => !IsSaturday && HasDailyTrades;
    public bool HasEmptyWeek => IsDataLoaded && IsSaturday && WeeklySummaries.Count == 0;
    // Saturday displays its weekly outcome; its daily metrics remain available for day details.
    public PnLOutcome DailyOutcome => IsInDisplayedMonth && !IsSaturday && DailySummaries.Count == 1
        ? DailySummaries[0].Outcome : PnLOutcome.None;
    public string WeeklyDescription => !IsSaturday ? "" : $"{WeekLabel}, Monday {Date.AddDays(-5):yyyy-MM-dd} through Sunday {Date.AddDays(1):yyyy-MM-dd}. " +
        (WeeklySummaries.Count > 0 ? string.Join(" ", WeeklySummaries.Select(s => s.Description))
            : IsDataLoaded ? "No closed Trades." : "Summary not loaded.");
    public string DayNumber => Date.Day.ToString(CultureInfo.CurrentCulture);
    public string AccessibleName => $"{Date.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture)}" +
        (IsToday ? ", today" : "") + (IsInDisplayedMonth ? "" : ", adjacent month") +
        (IsSelected ? ", selected" : "") +
        ". " + (IsSaturday ? WeeklyDescription
            : HasDailyTrades ? "Daily: " + string.Join(" ", DailySummaries.Select(s => s.Description))
            : IsDataLoaded ? "No closed Trades on this date." : "Summary not loaded.");
}

public sealed record CalendarWeekRow(IReadOnlyList<CalendarDayCell> Days);

/// <summary>Month navigation and presentation of the reader's currency-specific daily and weekly metrics.</summary>
public sealed class CalendarViewModel : ObservableObject
{
    private readonly ITradingCalendarReader _reader;
    private readonly ITradingCalendarDayReader _dayReader;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CalendarViewModel> _logger;
    private CancellationTokenSource? _loadCancellation;
    private long _generation;
    private bool _isActive, _isLoading;
    private DateOnly _month;
    private IReadOnlyList<CalendarWeekRow> _weeks = [];
    private TradingCalendarMonth? _monthData;
    private string? _errorMessage;
    private CancellationTokenSource? _dayCancellation;
    private long _dayGeneration;
    private DateOnly? _selectedDate;
    private bool _isDayLoading;
    private string? _dayErrorMessage;
    private TradingCalendarDayDetails? _dayDetails;
    private IReadOnlyList<CalendarTradePresentation> _dayTrades = [];
    private IReadOnlyList<CalendarPnlSummary> _daySummaries = [];

    public CalendarViewModel(ITradingCalendarReader reader, TimeProvider timeProvider,
        ITradingCalendarDayReader dayReader,
        ILogger<CalendarViewModel>? logger = null)
    {
        _reader = reader;
        _dayReader = dayReader;
        _timeProvider = timeProvider;
        _logger = logger ?? NullLogger<CalendarViewModel>.Instance;
        DateOnly today = Today;
        _month = new(today.Year, today.Month, 1);
        SetGrid(new TradingCalendarQuery(_month.Year, _month.Month));
        PreviousCommand = new RelayCommand(() => Move(-1), () => _month > DateOnly.MinValue);
        NextCommand = new RelayCommand(() => Move(1), () => _month.Year != 9999 || _month.Month < 11);
        TodayCommand = new RelayCommand(() => SelectMonth(Today));
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, AsyncRelayCommandOptions.AllowConcurrentExecutions);
        CancelCommand = new RelayCommand(Cancel, () => IsLoading);
        SelectDayCommand = new AsyncRelayCommand<CalendarDayCell>(SelectDayAsync,
            day => day is not null && _isActive && Weeks.SelectMany(w => w.Days).Any(d => d.Date == day.Date),
            AsyncRelayCommandOptions.AllowConcurrentExecutions);
        RetryDayCommand = new AsyncRelayCommand(RefreshDayAsync, () => SelectedDate.HasValue,
            AsyncRelayCommandOptions.AllowConcurrentExecutions);
        CancelDayCommand = new RelayCommand(CancelDay, () => IsDayLoading);
        ViewTradeCommand = new AsyncRelayCommand<TradeListItem>(ViewTradeAsync,
            item => item is not null && _isActive && !IsDayLoading && DayTrades.Any(row => row.Trade.Id == item.Id));
    }

    public DateOnly SelectedMonth => _month;
    public string MonthLabel => _month.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
    public IReadOnlyList<CalendarWeekRow> Weeks { get => _weeks; private set => SetProperty(ref _weeks, value); }
    public TradingCalendarMonth? MonthData { get => _monthData; private set => SetProperty(ref _monthData, value); }
    public bool IsLoading
    {
        get => _isLoading;
        private set { if (SetProperty(ref _isLoading, value)) CancelCommand.NotifyCanExecuteChanged(); }
    }
    public string? ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public string? StatusMessage => IsLoading ? "Loading Calendar…" : null;
    public Task LoadTask { get; private set; } = Task.CompletedTask;
    public IRelayCommand PreviousCommand { get; }
    public IRelayCommand NextCommand { get; }
    public IRelayCommand TodayCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IAsyncRelayCommand<CalendarDayCell> SelectDayCommand { get; }
    public IAsyncRelayCommand RetryDayCommand { get; }
    public IRelayCommand CancelDayCommand { get; }
    public IAsyncRelayCommand<TradeListItem> ViewTradeCommand { get; }
    public Func<TradeListItem, Task>? OpenTradeAsync { get; set; }
    public Task DayLoadTask { get; private set; } = Task.CompletedTask;
    public DateOnly? SelectedDate => _selectedDate;
    public bool HasSelectedDate => SelectedDate.HasValue;
    public string SelectedDateLabel => SelectedDate?.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture) ?? "";
    public TradingCalendarDayDetails? DayDetails { get => _dayDetails; private set => SetProperty(ref _dayDetails, value); }
    public IReadOnlyList<CalendarTradePresentation> DayTrades { get => _dayTrades; private set => SetProperty(ref _dayTrades, value); }
    public IReadOnlyList<CalendarPnlSummary> DaySummaries { get => _daySummaries; private set => SetProperty(ref _daySummaries, value); }
    public string? DayErrorMessage { get => _dayErrorMessage; private set => SetProperty(ref _dayErrorMessage, value); }
    public bool IsDayLoading
    {
        get => _isDayLoading;
        private set
        {
            if (!SetProperty(ref _isDayLoading, value)) return;
            CancelDayCommand.NotifyCanExecuteChanged();
            ViewTradeCommand.NotifyCanExecuteChanged();
            NotifyDayStatus();
        }
    }
    public string? DayStatusMessage => IsDayLoading ? "Loading day Trades…" : null;
    public string DayTradeCountText => DayDetails is { } details ? $"{details.ClosedTradeCount} closed {(details.ClosedTradeCount == 1 ? "Trade" : "Trades")}" : "";
    public bool IsSelectedDayEmpty => !IsDayLoading && DayDetails?.ClosedTradeCount == 0;

    public Task ActivateAsync() { _isActive = true; return RefreshAsync(); }
    public void Deactivate() { _isActive = false; Cancel(); CancelDay(); }
    public Task RefreshAsync()
    {
        LoadTask = LoadAsync();
        return SelectedDate.HasValue ? Task.WhenAll(LoadTask, RefreshDayAsync()) : LoadTask;
    }

    private DateOnly Today => DateOnly.FromDateTime(
        TradingTimePolicy.ConvertUtcToTradingTime(_timeProvider.GetUtcNow()).DateTime);

    private void Move(int months)
    {
        if (months < 0 && _month == DateOnly.MinValue) return;
        if (months > 0 && _month.Year == 9999 && _month.Month >= 11) return;
        SelectMonth(_month.AddMonths(months));
    }

    private void SelectMonth(DateOnly date)
    {
        ClearDaySelection();
        DateOnly first = new(date.Year, date.Month, 1);
        if (first == _month)
        {
            // Today may have advanced within this month while the window remained open.
            SetGrid(new TradingCalendarQuery(first.Year, first.Month));
            if (_isActive) _ = RefreshAsync();
            return;
        }
        _month = first;
        OnPropertyChanged(nameof(SelectedMonth));
        OnPropertyChanged(nameof(MonthLabel));
        PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
        MonthData = null;
        ErrorMessage = null;
        // Replace the dates synchronously so a cancelled/slow read cannot blank the month.
        SetGrid(new TradingCalendarQuery(first.Year, first.Month));
        if (_isActive) _ = RefreshAsync();
    }

    private void SetGrid(TradingCalendarQuery query, TradingCalendarMonth? data = null)
    {
        DateOnly today = Today;
        var daily = data?.Currencies.SelectMany(c => c.Weeks.SelectMany(w => w.Days)
            .Where(d => d.Metrics is not null)
            .Select(d => (d.Date, Summary: new CalendarPnlSummary(c.Currency, d.Metrics!,
                d.IsInDisplayedMonth))))
            .ToLookup(d => d.Date);
        var weekly = data?.Currencies.SelectMany(c => c.Weeks.Where(w => w.Metrics is not null)
            .Select(w => (w.Monday, Summary: new CalendarPnlSummary(c.Currency, w.Metrics!))))
            .ToLookup(w => w.Monday);
        CalendarDayCell[] cells = Enumerable.Range(0, query.GridEnd.DayNumber - query.GridStart.DayNumber + 1)
            .Select(offset =>
            {
                DateOnly date = query.GridStart.AddDays(offset);
                return new CalendarDayCell(date, date.Year == query.MonthStart.Year && date.Month == query.MonthStart.Month,
                    date == today, date.DayOfWeek == DayOfWeek.Saturday)
                {
                    IsDataLoaded = data is not null,
                    IsSelected = date == SelectedDate,
                    DailySummaries = daily?[date].Select(d => d.Summary).OrderBy(s => s.Currency, StringComparer.Ordinal).ToArray() ?? [],
                    WeeklySummaries = date.DayOfWeek == DayOfWeek.Saturday
                        ? weekly?[date.AddDays(-5)].Select(w => w.Summary).OrderBy(s => s.Currency, StringComparer.Ordinal).ToArray() ?? [] : [],
                    WeekLabel = $"Week {offset / 7 + 1}",
                };
            }).ToArray();
        Weeks = Enumerable.Range(0, cells.Length / 7)
            .Select(row => new CalendarWeekRow(Array.AsReadOnly(cells.Skip(row * 7).Take(7).ToArray())))
            .ToArray();
    }

    private async Task LoadAsync()
    {
        long generation = ++_generation;
        _loadCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        DateOnly requestedMonth = _month;
        MonthData = null;
        SetGrid(new TradingCalendarQuery(requestedMonth.Year, requestedMonth.Month));
        ErrorMessage = null;
        IsLoading = true;
        OnPropertyChanged(nameof(StatusMessage));
        try
        {
            // SQLite's async provider can perform synchronous work; keep it off the dispatcher.
            TradingCalendarMonth result = await Task.Run(() => _reader.GetAsync(
                new TradingCalendarQuery(requestedMonth.Year, requestedMonth.Month), cancellation.Token), cancellation.Token);
            if (generation != _generation || cancellation.IsCancellationRequested || !_isActive) return;
            MonthData = result;
            SetGrid(new TradingCalendarQuery(requestedMonth.Year, requestedMonth.Month), result);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (generation != _generation || !_isActive) return;
            _logger.LogError(exception, "Calendar month load failed");
            ErrorMessage = "Calendar could not be loaded. Select Refresh to retry.";
        }
        finally
        {
            if (generation == _generation)
            {
                _loadCancellation = null;
                IsLoading = false;
                OnPropertyChanged(nameof(StatusMessage));
            }
        }
    }

    private void Cancel()
    {
        _generation++;
        _loadCancellation?.Cancel();
        _loadCancellation = null;
        IsLoading = false;
        OnPropertyChanged(nameof(StatusMessage));
    }

    private Task SelectDayAsync(CalendarDayCell? day)
    {
        if (day is null || !_isActive || !Weeks.SelectMany(w => w.Days).Any(d => d.Date == day.Date)) return Task.CompletedTask;
        _selectedDate = day.Date;
        NotifySelection();
        return RefreshDayAsync();
    }

    private Task RefreshDayAsync() => DayLoadTask = SelectedDate is { } date ? LoadDayAsync(date) : Task.CompletedTask;

    private async Task LoadDayAsync(DateOnly date)
    {
        long generation = ++_dayGeneration;
        _dayCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _dayCancellation = cancellation;
        ClearDayResults();
        IsDayLoading = true;
        try
        {
            TradingCalendarDayDetails result = await Task.Run(() => _dayReader.GetAsync(new(date), cancellation.Token), cancellation.Token);
            if (generation != _dayGeneration || cancellation.IsCancellationRequested || !_isActive || SelectedDate != date) return;
            DayDetails = result;
            DayTrades = result.Trades.Select(t => new CalendarTradePresentation(t)).ToArray();
            DaySummaries = result.Currencies.Select(c => new CalendarPnlSummary(c.Currency, c.Metrics)).ToArray();
            ViewTradeCommand.NotifyCanExecuteChanged();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (generation != _dayGeneration || !_isActive) return;
            _logger.LogError(exception, "Calendar day load failed");
            DayErrorMessage = "Day Trades could not be loaded. Select Retry to try again.";
        }
        finally
        {
            if (generation == _dayGeneration)
            {
                _dayCancellation = null;
                IsDayLoading = false;
                NotifyDayStatus();
            }
        }
    }

    private async Task ViewTradeAsync(TradeListItem? item)
    {
        if (item is null || OpenTradeAsync is null || !_isActive || IsDayLoading || !DayTrades.Any(row => row.Trade.Id == item.Id)) return;
        long generation = _dayGeneration;
        try { await OpenTradeAsync(item); }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Calendar Trade navigation failed");
            if (generation == _dayGeneration && _isActive)
                DayErrorMessage = "Trade could not be opened. Select Retry and try again.";
        }
    }

    private void CancelDay()
    {
        _dayGeneration++;
        _dayCancellation?.Cancel();
        _dayCancellation = null;
        IsDayLoading = false;
        NotifyDayStatus();
    }

    private void ClearDayResults()
    {
        DayDetails = null;
        DayTrades = [];
        DaySummaries = [];
        DayErrorMessage = null;
        ViewTradeCommand.NotifyCanExecuteChanged();
        NotifyDayStatus();
    }

    private void ClearDaySelection()
    {
        CancelDay();
        _selectedDate = null;
        ClearDayResults();
        NotifySelection();
    }

    private void NotifySelection()
    {
        foreach (CalendarDayCell cell in Weeks.SelectMany(w => w.Days)) cell.IsSelected = cell.Date == SelectedDate;
        OnPropertyChanged(nameof(SelectedDate));
        OnPropertyChanged(nameof(HasSelectedDate));
        OnPropertyChanged(nameof(SelectedDateLabel));
        RetryDayCommand.NotifyCanExecuteChanged();
    }

    private void NotifyDayStatus()
    {
        OnPropertyChanged(nameof(DayStatusMessage));
        OnPropertyChanged(nameof(DayTradeCountText));
        OnPropertyChanged(nameof(IsSelectedDayEmpty));
    }
}
