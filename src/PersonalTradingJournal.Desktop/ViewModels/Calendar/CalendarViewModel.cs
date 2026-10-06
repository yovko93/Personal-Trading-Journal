using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Common.Time;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Converters;

namespace PersonalTradingJournal.Desktop.ViewModels.Calendar;

public sealed partial class CalendarDayCell(DateOnly date, bool isInDisplayedMonth, bool isToday, bool isSaturday) : ObservableObject
{
    public DateOnly Date { get; } = date;
    public bool IsInDisplayedMonth { get; } = isInDisplayedMonth;
    public bool IsToday { get; private set; } = isToday;
    public bool IsSaturday { get; } = isSaturday;
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        internal set
        {
            if (!SetProperty(ref _isSelected, value)) return;
            OnPropertyChanged(nameof(AccessibleName));
            OnPropertyChanged(nameof(AccessibleStatus));
        }
    }
    public IReadOnlyList<CalendarPnlSummary> DailySummaries { get; private set; } = [];
    public IReadOnlyList<CalendarPnlSummary> WeeklySummaries { get; private set; } = [];
    public bool IsDataLoaded { get; private set; }
    public bool IsBusy { get; private set; }
    public string AccessibleStatus => (IsSelected ? "Selected. " : "") + (IsBusy ? "Loading summary." : IsDataLoaded ? "Summary loaded." : "Summary not loaded.") +
        (JournalAccessibleDescription.Length == 0 ? "" : " " + JournalAccessibleDescription);
    public string WeekLabel { get; private set; } = "";

    internal void Update(bool today, bool loaded, bool busy, string weekLabel,
        IReadOnlyList<CalendarPnlSummary> daily, IReadOnlyList<CalendarPnlSummary> weekly)
    {
        IsToday = today;
        IsDataLoaded = loaded;
        IsBusy = busy;
        WeekLabel = weekLabel;
        DailySummaries = daily;
        WeeklySummaries = weekly;
        // Keep cell/container identity (and keyboard focus) when refreshing the same grid.
        OnPropertyChanged(string.Empty);
    }
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
    public string DailyAccessibleDescription => HasDailyTrades
        ? "Daily: " + string.Join(" ", DailySummaries.Select(s => s.Description))
        : IsDataLoaded ? "No closed Trades on this date." : "Daily results not loaded.";
    public string SelectionHelpText => "Select this date with Enter or Space to show its closed Trades. " + DailyAccessibleDescription;
    // Hover is deliberately concise, while accessibility retains provenance and coverage.
    // Saturday's marker describes Saturday, not the weekly total displayed beneath it.
    public string DateTooltip => Date.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture) + Environment.NewLine +
        (HasDailyTrades ? string.Join(Environment.NewLine, DailySummaries.Select(s => $"P/L: {s.AmountText}; {s.TradeCountText}"))
            : IsBusy ? "Loading daily results…" : IsDataLoaded ? "0 Trades" : "Daily results not loaded.");
    public string AccessibleName => $"{Date.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture)}" +
        (IsToday ? ", today" : "") + (IsInDisplayedMonth ? "" : ", adjacent month") +
        (IsSelected ? ", selected" : "") +
        ". " + (IsSaturday ? WeeklyDescription
            : HasDailyTrades ? "Daily: " + string.Join(" ", DailySummaries.Select(s => s.Description))
            : IsDataLoaded ? "No closed Trades on this date." : "Summary not loaded.") +
        (JournalAccessibleDescription.Length == 0 ? "" : " " + JournalAccessibleDescription);
}

public sealed record CalendarWeekRow(IReadOnlyList<CalendarDayCell> Days);
public sealed record CalendarAccountOption(Guid? Id, string Name);

/// <summary>Month navigation and presentation of the reader's currency-specific daily and weekly metrics.</summary>
public sealed partial class CalendarViewModel : ObservableObject
{
    private readonly ITradingCalendarReader _reader;
    private readonly ITradingCalendarDayReader _dayReader;
    private readonly ITradingAccountReader _accountReader;
    private static readonly CalendarAccountOption AllAccounts = new(null, "All accounts");
    private const string AllCurrencies = "All currencies";
    private CalendarAccountOption _selectedAccount = AllAccounts;
    private IReadOnlyList<CalendarAccountOption> _accounts = [AllAccounts];
    private string _selectedCurrency = AllCurrencies;
    private IReadOnlyList<string> _currencies = [AllCurrencies];
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CalendarViewModel> _logger;
    private CancellationTokenSource? _loadCancellation;
    private long _generation;
    private bool _isActive, _isLoading;
    private DateOnly _month;
    private IReadOnlyList<CalendarWeekRow> _weeks = [];
    private TradingCalendarMonth? _monthData;
    private IReadOnlyList<CalendarMonthlyPnlSummary> _monthlySummaries = [];
    private string? _errorMessage;
    private CancellationTokenSource? _dayCancellation;
    private long _dayGeneration;
    private DateOnly? _selectedDate;
    private bool _isDayLoading;
    private string? _dayErrorMessage;
    private string? _loadNotice, _dayNotice;
    private TradingCalendarDayDetails? _dayDetails;
    private IReadOnlyList<CalendarDayPerformance> _dayPerformance = [];
    private IReadOnlyList<CalendarTradePresentation> _dayTrades = [];
    private IReadOnlyList<CalendarPnlSummary> _daySummaries = [];

    public CalendarViewModel(ITradingCalendarReader reader, TimeProvider timeProvider,
        ITradingCalendarDayReader dayReader, ITradingAccountReader accountReader,
        ILogger<CalendarViewModel>? logger = null,
        PersonalTradingJournal.Desktop.ViewModels.Trades.TradesViewModel? tradeEditor = null,
        IDailyJournalStatusReader? journalStatusReader = null,
        IDailyJournalRepository? journalRepository = null,
        PersonalTradingJournal.Desktop.Dialogs.IDialogService? journalDialogs = null)
    {
        _reader = reader;
        _dayReader = dayReader;
        _accountReader = accountReader;
        _timeProvider = timeProvider;
        _logger = logger ?? NullLogger<CalendarViewModel>.Instance;
        _journalStatusReader = journalStatusReader;
        InitializeDayJournal(journalRepository, journalDialogs);
        DateOnly today = Today;
        _month = new(today.Year, today.Month, 1);
        SetGrid(new TradingCalendarQuery(_month.Year, _month.Month));
        PreviousCommand = new RelayCommand(() => Move(-1), () => _month > DateOnly.MinValue);
        NextCommand = new RelayCommand(() => Move(1), () => _month.Year != 9999 || _month.Month < 11);
        TodayCommand = new RelayCommand(() => SelectMonth(Today));
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, AsyncRelayCommandOptions.AllowConcurrentExecutions);
        CancelCommand = new RelayCommand(CancelRefresh, () => IsBusy);
        SelectDayCommand = new AsyncRelayCommand<CalendarDayCell>(SelectDayAsync,
            day => day is not null && _isActive && Weeks.SelectMany(w => w.Days).Any(d => d.Date == day.Date),
            AsyncRelayCommandOptions.AllowConcurrentExecutions);
        RetryDayCommand = new AsyncRelayCommand(RefreshDayAsync, () => SelectedDate.HasValue,
            AsyncRelayCommandOptions.AllowConcurrentExecutions);
        CancelDayCommand = new RelayCommand(CancelDay, () => IsDayLoading);
        ViewTradeCommand = new AsyncRelayCommand<TradeListItem>(ViewTradeAsync,
            item => item is not null && _isActive && !IsDayLoading && !HasInlineWork && DayTrades.Any(row => row.Trade.Id == item.Id));
        InitializeInlineEditor(tradeEditor);
    }

    public DateOnly SelectedMonth => _month;
    public IReadOnlyList<CalendarAccountOption> Accounts { get => _accounts; private set => SetProperty(ref _accounts, value); }
    public CalendarAccountOption SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            if (HasInlineWork || value is null || value.Id == _selectedAccount.Id) return;
            if (!TryCloseInlineJournal()) { OnPropertyChanged(); return; }
            SetProperty(ref _selectedAccount, value);
            FiltersChanged();
        }
    }
    public IReadOnlyList<string> Currencies { get => _currencies; private set => SetProperty(ref _currencies, value); }
    public string SelectedCurrency
    {
        get => _selectedCurrency;
        set
        {
            if (HasInlineWork || string.IsNullOrWhiteSpace(value) || !SetProperty(ref _selectedCurrency, value)) return;
            FiltersChanged();
        }
    }
    public string MonthLabel => _month.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
    public IReadOnlyList<CalendarWeekRow> Weeks { get => _weeks; private set => SetProperty(ref _weeks, value); }
    public TradingCalendarMonth? MonthData
    {
        get => _monthData;
        private set
        {
            IReadOnlyList<CalendarMonthlyPnlSummary> summaries = value is null ? [] : TradingCalendarMonthlyPnl.From(value)
                .Select(s => new CalendarMonthlyPnlSummary(s)).ToArray();
            if (!SetProperty(ref _monthData, value)) return;
            MonthlySummaries = summaries;
            OnPropertyChanged(nameof(MonthlyStatusText));
            OnPropertyChanged(nameof(IsMonthEmpty));
        }
    }
    public IReadOnlyList<CalendarMonthlyPnlSummary> MonthlySummaries
    {
        get => _monthlySummaries;
        private set => SetProperty(ref _monthlySummaries, value);
    }
    public string MonthlyStatusText => MonthData is null ? "—" : MonthlySummaries.Count == 0 ? "No closed Trades" : "";
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (!SetProperty(ref _isLoading, value)) return;
            NotifyBusy();
            OnPropertyChanged(nameof(IsMonthEmpty));
        }
    }
    public string? ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public bool IsBusy => IsLoading || IsDayLoading || IsJournalLoading;
    public string AccessibleStatus => IsBusy ? "Loading Calendar data." : "Calendar ready.";
    public bool IsMonthEmpty => !IsLoading && MonthData is { } month && !month.Currencies
        .SelectMany(c => c.Weeks).SelectMany(w => w.Days).Any(d => d.IsInDisplayedMonth && d.ClosedTradeCount > 0);
    public string? StatusMessage => IsLoading ? "Loading Calendar…" : _loadNotice;
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
    public Task DayLoadTask { get; private set; } = Task.CompletedTask;
    public DateOnly? SelectedDate => _selectedDate;
    public bool HasSelectedDate => SelectedDate.HasValue;
    public string SelectedDateLabel => SelectedDate?.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture) ?? "";
    public TradingCalendarDayDetails? DayDetails { get => _dayDetails; private set => SetProperty(ref _dayDetails, value); }
    public IReadOnlyList<CalendarDayPerformance> DayPerformance { get => _dayPerformance; private set => SetProperty(ref _dayPerformance, value); }
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
            NotifyBusy();
            NotifyDayStatus();
        }
    }
    public string? DayStatusMessage => IsDayLoading ? "Loading day Trades…" : _dayNotice;
    public string DayTradeCountText => DayDetails is { } details ? $"{details.ClosedTradeCount} closed {(details.ClosedTradeCount == 1 ? "Trade" : "Trades")}" : "";
    public bool IsSelectedDayEmpty => !IsDayLoading && DayDetails?.ClosedTradeCount == 0;

    public Task ActivateAsync() { _isActive = true; return RefreshAsync(); }
    public void Deactivate() { _isActive = false; ReleaseInlineJournal(); Cancel(); CancelDay(); }
    public Task RefreshAsync() => LoadTask = RefreshAllAsync();
    public void OnDataCommitted()
    {
        if (_isActive) _ = RefreshAsync();
    }

    private Task RefreshAllAsync()
    {
        if (HasInlineWork) { _inlineRefreshPending = true; return Task.CompletedTask; }
        Task month = LoadAsync();
        Task journals = RefreshJournalStatusesAsync();
        return SelectedDate.HasValue ? Task.WhenAll(month, journals, RefreshDayAsync()) : Task.WhenAll(month, journals);
    }

    private void FiltersChanged()
    {
        CancelDay();
        ClearDayResults();
        MonthData = null;
        SetGrid(new TradingCalendarQuery(_month.Year, _month.Month, SelectedAccount.Id));
        if (_isActive) _ = RefreshAsync();
    }

    private void PublishAccounts(IReadOnlyList<AccountListItem> accounts, CalendarAccountOption requested)
    {
        var options = accounts.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(a => a.Id)
            .Select(a => new CalendarAccountOption(a.Id, a.Name + (a.IsActive ? "" : " (inactive)"))).Prepend(AllAccounts).ToList();
        CalendarAccountOption? selected = options.FirstOrDefault(a => a.Id == requested.Id);
        if (selected is null)
        {
            selected = new(requested.Id, requested.Name.Replace(" (unavailable)", "", StringComparison.Ordinal) + " (unavailable)");
            options.Add(selected);
        }
        // Publish without invoking the selection setter and starting a second request.
        _selectedAccount = selected;
        Accounts = options;
        OnPropertyChanged(nameof(SelectedAccount));
    }

    private static string UnavailableAccountMessage => "The selected account is no longer available. Choose another account or All accounts.";

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
        if (HasInlineWork) return;
        if (!TryCloseInlineJournal()) return;
        CloseInlineDetails();
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
        EnsureJournalGrid(query);
        DateOnly today = Today;
        var existing = Weeks.SelectMany(w => w.Days).ToDictionary(d => d.Date);
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
                bool inMonth = date.Year == query.MonthStart.Year && date.Month == query.MonthStart.Month;
                CalendarDayCell cell = existing.TryGetValue(date, out var previous) && previous.IsInDisplayedMonth == inMonth
                    ? previous : new(date, inMonth, date == today, date.DayOfWeek == DayOfWeek.Saturday);
                cell.IsSelected = date == SelectedDate;
                cell.Update(date == today, data is not null, IsLoading, $"Week {offset / 7 + 1}",
                    daily?[date].Select(d => d.Summary).OrderBy(s => s.Currency, StringComparer.Ordinal).ToArray() ?? [],
                    cell.IsSaturday ? weekly?[date.AddDays(-5)].Select(w => w.Summary)
                        .OrderBy(s => s.Currency, StringComparer.Ordinal).ToArray() ?? [] : []);
                cell.UpdateJournal(_journalStatuses.GetValueOrDefault(date), _journalStatusesLoaded);
                return cell;
            }).ToArray();
        if (Weeks.SelectMany(w => w.Days).SequenceEqual(cells)) return;
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
        CalendarAccountOption requestedAccount = SelectedAccount;
        string requestedCurrency = SelectedCurrency;
        _loadNotice = null;
        IsLoading = true;
        MonthData = null;
        SetGrid(new TradingCalendarQuery(requestedMonth.Year, requestedMonth.Month));
        ErrorMessage = null;
        OnPropertyChanged(nameof(StatusMessage));
        try
        {
            // SQLite's async provider can perform synchronous work; keep it off the dispatcher.
            var loaded = await Task.Run(async () =>
            {
                IReadOnlyList<AccountListItem> accounts = await _accountReader.GetAllAsync(cancellation.Token);
                TradingCalendarMonth? month = requestedAccount.Id is { } id && !accounts.Any(a => a.Id == id) ? null
                    : await _reader.GetAsync(new TradingCalendarQuery(requestedMonth.Year, requestedMonth.Month, requestedAccount.Id), cancellation.Token);
                return (accounts, month);
            }, cancellation.Token);
            if (generation != _generation || cancellation.IsCancellationRequested || !_isActive) return;
            PublishAccounts(loaded.accounts, requestedAccount);
            if (loaded.month is not { } source)
            {
                ErrorMessage = UnavailableAccountMessage;
                return;
            }
            Currencies = source.Currencies.Select(c => c.Currency)
                .Concat(requestedCurrency == AllCurrencies ? [] : new[] { requestedCurrency })
                .Distinct(StringComparer.Ordinal).OrderBy(c => c, StringComparer.Ordinal).Prepend(AllCurrencies).ToArray();
            OnPropertyChanged(nameof(SelectedCurrency));
            TradingCalendarMonth result = requestedCurrency == AllCurrencies ? source
                : source with { Currencies = source.Currencies.Where(c => c.Currency == requestedCurrency).ToArray() };
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
                SetGrid(new TradingCalendarQuery(_month.Year, _month.Month), MonthData);
                OnPropertyChanged(nameof(StatusMessage));
            }
        }
    }

    private void Cancel()
    {
        CancelJournalStatuses();
        _generation++;
        _loadCancellation?.Cancel();
        _loadCancellation = null;
        IsLoading = false;
        SetGrid(new TradingCalendarQuery(_month.Year, _month.Month), MonthData);
        OnPropertyChanged(nameof(StatusMessage));
    }

    private void CancelRefresh()
    {
        Cancel();
        CancelDay();
        _loadNotice = "Calendar refresh cancelled. Select Refresh to retry.";
        OnPropertyChanged(nameof(StatusMessage));
    }

    private void NotifyBusy()
    {
        CancelCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(AccessibleStatus));
    }

    private Task SelectDayAsync(CalendarDayCell? day)
    {
        if (HasInlineWork || day is null || !_isActive || !Weeks.SelectMany(w => w.Days).Any(d => d.Date == day.Date)) return Task.CompletedTask;
        if (day.Date != SelectedDate && !TryCloseInlineJournal()) return Task.CompletedTask;
        CloseInlineDetails();
        _selectedDate = day.Date;
        NotifySelection();
        return RefreshDayAsync();
    }

    private Task RefreshDayAsync()
    {
        if (HasInlineWork) { _inlineRefreshPending = true; return Task.CompletedTask; }
        return DayLoadTask = SelectedDate is { } date ? LoadDayAsync(date) : Task.CompletedTask;
    }

    private async Task LoadDayAsync(DateOnly date)
    {
        long generation = ++_dayGeneration;
        _dayCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _dayCancellation = cancellation;
        Guid? requestedAccount = SelectedAccount.Id;
        string requestedCurrency = SelectedCurrency;
        ClearDayResults();
        IsDayLoading = true;
        try
        {
            TradingCalendarDayDetails? source = await Task.Run(async () =>
            {
                if (requestedAccount is { } id && !(await _accountReader.GetAllAsync(cancellation.Token)).Any(a => a.Id == id)) return null;
                return await _dayReader.GetAsync(new(date, requestedAccount), cancellation.Token);
            }, cancellation.Token);
            if (generation != _dayGeneration || cancellation.IsCancellationRequested || !_isActive || SelectedDate != date) return;
            if (source is null)
            {
                DayErrorMessage = UnavailableAccountMessage;
                return;
            }
            TradingCalendarDayDetails result = requestedCurrency == AllCurrencies ? source : source with
            {
                Trades = source.Trades.Where(t => t.Currency == requestedCurrency).ToArray(),
                Currencies = source.Currencies.Where(c => c.Currency == requestedCurrency).ToArray(),
            };
            IReadOnlyList<CalendarDayPerformance> performance = CalendarDayPerformance.From(result.Trades);
            DayDetails = result;
            DayPerformance = performance;
            DayTrades = result.Trades.Select(t => new CalendarTradePresentation(t,
                result.Classifications.GetValueOrDefault(t.Id)) { Editor = TradeEditor, IsExpanded = t.Id == _inlineTradeId }).ToArray();
            if (_inlineTradeId.HasValue && !DayTrades.Any(t => t.Trade.Id == _inlineTradeId))
            {
                CloseInlineDetails();
                _dayNotice = "The edited Trade is no longer in this date or filter selection.";
            }
            DaySummaries = result.Currencies.Select(c => new CalendarPnlSummary(c.Currency, c.Metrics)).ToArray();
            ViewTradeCommand.NotifyCanExecuteChanged();
            await ReloadExpandedTradeAsync(generation);
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

    private void CancelDay()
    {
        if (IsDayLoading) _dayNotice = "Day read cancelled. Select Retry to try again.";
        _dayGeneration++;
        _dayCancellation?.Cancel();
        _dayCancellation = null;
        IsDayLoading = false;
        NotifyDayStatus();
    }

    private void ClearDayResults()
    {
        _dayNotice = null;
        DayDetails = null;
        DayPerformance = [];
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
        NotifyJournalDay();
    }

    private void NotifyDayStatus()
    {
        OnPropertyChanged(nameof(DayStatusMessage));
        OnPropertyChanged(nameof(DayTradeCountText));
        OnPropertyChanged(nameof(IsSelectedDayEmpty));
    }
}
