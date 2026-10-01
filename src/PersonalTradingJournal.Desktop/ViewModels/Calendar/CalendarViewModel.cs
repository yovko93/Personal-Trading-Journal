using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Common.Time;

namespace PersonalTradingJournal.Desktop.ViewModels.Calendar;

public sealed record CalendarDayCell(DateOnly Date, bool IsInDisplayedMonth, bool IsToday, bool IsSaturday)
{
    public string DayNumber => Date.Day.ToString(CultureInfo.CurrentCulture);
    public string AccessibleName => $"{Date.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture)}" +
        (IsToday ? ", today" : "") + (IsInDisplayedMonth ? "" : ", adjacent month") +
        (IsSaturday ? ", weekly summary area" : "");
}

public sealed record CalendarWeekRow(IReadOnlyList<CalendarDayCell> Days);

/// <summary>Month navigation and stable grid state only; monetary presentation belongs to M13.3.</summary>
public sealed class CalendarViewModel : ObservableObject
{
    private readonly ITradingCalendarReader _reader;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CalendarViewModel> _logger;
    private CancellationTokenSource? _loadCancellation;
    private long _generation;
    private bool _isActive, _isLoading;
    private DateOnly _month;
    private IReadOnlyList<CalendarWeekRow> _weeks = [];
    private TradingCalendarMonth? _monthData;
    private string? _errorMessage;

    public CalendarViewModel(ITradingCalendarReader reader, TimeProvider timeProvider,
        ILogger<CalendarViewModel>? logger = null)
    {
        _reader = reader;
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

    public Task ActivateAsync() { _isActive = true; return RefreshAsync(); }
    public void Deactivate() { _isActive = false; Cancel(); }
    public Task RefreshAsync() => LoadTask = LoadAsync();

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

    private void SetGrid(TradingCalendarQuery query)
    {
        DateOnly today = Today;
        CalendarDayCell[] cells = Enumerable.Range(0, query.GridEnd.DayNumber - query.GridStart.DayNumber + 1)
            .Select(offset =>
            {
                DateOnly date = query.GridStart.AddDays(offset);
                return new CalendarDayCell(date, date.Year == query.MonthStart.Year && date.Month == query.MonthStart.Month,
                    date == today, date.DayOfWeek == DayOfWeek.Saturday);
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
}
