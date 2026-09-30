using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.Common.Time;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Application.Accounts;

namespace PersonalTradingJournal.Desktop.ViewModels.Dashboard;

public enum DashboardPeriod { All, Week, Month, Year, Custom }
public sealed record DashboardAccountOption(Guid? Id, string Name);

public sealed class DashboardViewModel : ObservableObject
{
    private readonly IDashboardAnalyticsReader _reader;
    private readonly ITradeListReader _tradeReader;
    private readonly ITradingAccountReader _accountReader;
    private static readonly DashboardAccountOption AllAccounts = new(null, "All accounts");
    private DashboardAccountOption _selectedAccount = AllAccounts;
    private IReadOnlyList<DashboardAccountOption> _accounts = [AllAccounts];
    private bool _updatingAccounts, _rangeEdited;
    private DateTime? _startDate, _endDate;
    private DateOnly _customStart, _customEnd;
    private bool _isDateRangeOpen;
    private DateTime _startMonth, _endMonth;
    private IReadOnlyList<TradeListItem> _recentTrades = [];
    private readonly TimeProvider _time;
    private readonly ILogger<DashboardViewModel> _logger;
    private CancellationTokenSource? _loadCancellation;
    private long _generation;
    private bool _active, _isLoading;
    private DashboardPeriod _period;
    private DateOnly _anchor;
    private string? _selectedCurrency, _errorMessage, _statusMessage;
    private DashboardAnalyticsSnapshot? _snapshot;
    private DashboardCurrencyPresentation? _selected;
    private IReadOnlyList<string> _currencies = [];

    public DashboardViewModel(IDashboardAnalyticsReader reader, TimeProvider timeProvider, ITradeListReader tradeReader, ITradingAccountReader accountReader,
        ILogger<DashboardViewModel>? logger = null)
    {
        _reader = reader;
        _tradeReader = tradeReader;
        _accountReader = accountReader;
        _time = timeProvider;
        _logger = logger ?? NullLogger<DashboardViewModel>.Instance;
        _anchor = Today;
        _customStart = _customEnd = Today;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, AsyncRelayCommandOptions.AllowConcurrentExecutions);
        PreviousCommand = new RelayCommand(() => Move(-1), () => IsCalendarPeriod && _anchor.Year > 1);
        NextCommand = new RelayCommand(() => Move(1), () => IsCalendarPeriod && Start(_anchor) < Start(Today));
        CancelCommand = new RelayCommand(Cancel, () => IsLoading);
        ApplyRangeCommand = new RelayCommand(ApplyRange, () => ValidateRange() is null);
        TodayCommand = new RelayCommand(() => ApplyDates(Today, Today));
        LastWeekCommand = new RelayCommand(() =>
        {
            DateOnly first = DashboardMetricCalculator.GetWeekStartingMonday(Today).AddDays(-7);
            ApplyDates(first, first.AddDays(6));
        });
        LastMonthCommand = new RelayCommand(() =>
        {
            DateOnly first = new DateOnly(Today.Year, Today.Month, 1).AddMonths(-1);
            ApplyDates(first, first.AddMonths(1).AddDays(-1));
        });
        AllHistoryCommand = new RelayCommand(() => SelectPeriod(DashboardPeriod.All));
        CancelRangeCommand = new RelayCommand(() => { SyncDateInputs(); IsDateRangeOpen = false; });
        ViewTradeCommand = new AsyncRelayCommand<TradeListItem>(async item =>
        {
            if (item is null || OpenTradeAsync is null) return;
            try { await OpenTradeAsync(item); }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Dashboard Trade navigation failed");
                ErrorMessage = "Trade could not be opened. Refresh and try again.";
            }
        }, item => item is not null && !IsLoading);
    }

    public IReadOnlyList<DashboardPeriod> Periods { get; } = Enum.GetValues<DashboardPeriod>();
    public IReadOnlyList<DashboardAccountOption> Accounts { get => _accounts; private set => SetProperty(ref _accounts, value); }
    public DashboardAccountOption SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            // ItemsSource replacement can transiently clear a WPF selection. Only the explicit
            // All accounts option clears the filter; null must never widen a selected scope.
            if (_updatingAccounts || value is null || value.Id == _selectedAccount.Id) return;
            SetProperty(ref _selectedAccount, value);
            _ = RefreshAsync();
        }
    }
    public DateTime? StartDate { get => _startDate; set { if (SetProperty(ref _startDate, value?.Date)) RangeEdited(); } }
    public DateTime? EndDate { get => _endDate; set { if (SetProperty(ref _endDate, value?.Date)) RangeEdited(); } }
    public string? RangeValidationMessage => _rangeEdited ? ValidateRange() : null;
    public bool IsDateRangeOpen
    {
        get => _isDateRangeOpen;
        set
        {
            if (!SetProperty(ref _isDateRangeOpen, value) || !value) return;
            SyncDateInputs();
            DateTime first = StartDate ?? Today.ToDateTime(TimeOnly.MinValue);
            StartMonth = new(first.Year, first.Month, 1);
            EndMonth = EndDate is { } last && last.Year * 12 + last.Month > first.Year * 12 + first.Month
                ? new(last.Year, last.Month, 1) : StartMonth.AddMonths(StartMonth.Year == 9999 && StartMonth.Month == 12 ? 0 : 1);
        }
    }
    public DateTime StartMonth { get => _startMonth; set => SetProperty(ref _startMonth, value); }
    public DateTime EndMonth { get => _endMonth; set => SetProperty(ref _endMonth, value); }
    public string DraftRangeLabel => $"Start: {StartDate?.ToString("yyyy-MM-dd") ?? "choose date"}  ·  End: {EndDate?.ToString("yyyy-MM-dd") ?? "choose date"}";
    public IRelayCommand CancelRangeCommand { get; }
    public IRelayCommand ApplyRangeCommand { get; }
    public IRelayCommand TodayCommand { get; }
    public IRelayCommand LastWeekCommand { get; }
    public IRelayCommand LastMonthCommand { get; }
    public IRelayCommand AllHistoryCommand { get; }
    private bool IsCalendarPeriod => Period is DashboardPeriod.Week or DashboardPeriod.Month or DashboardPeriod.Year;
    public DashboardPeriod Period
    {
        get => _period;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (_period != value) SelectPeriod(value);
        }
    }
    public string PeriodLabel => Period == DashboardPeriod.All ? "All history"
        : Period == DashboardPeriod.Custom ? $"Custom: {_customStart:yyyy-MM-dd} – {_customEnd:yyyy-MM-dd}"
        : $"{Start(_anchor):yyyy-MM-dd} – {End(_anchor):yyyy-MM-dd}";
    public DashboardAnalyticsQuery Query => Period == DashboardPeriod.All ? new(tradingAccountId: SelectedAccount.Id)
        : new(tradingAccountId: SelectedAccount.Id,
            closedFromNewYork: Period == DashboardPeriod.Custom ? _customStart : Start(_anchor),
            closedThroughNewYork: Period == DashboardPeriod.Custom ? _customEnd : End(_anchor));
    public IReadOnlyList<string> Currencies { get => _currencies; private set => SetProperty(ref _currencies, value); }
    public string? SelectedCurrency
    {
        get => _selectedCurrency;
        set { if (SetProperty(ref _selectedCurrency, value)) Present(); }
    }
    public DashboardCurrencyPresentation? Selected { get => _selected; private set => SetProperty(ref _selected, value); }
    public bool IsLoading { get => _isLoading; private set { SetProperty(ref _isLoading, value); CancelCommand.NotifyCanExecuteChanged(); ViewTradeCommand.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(HasNoRecentTrades)); } }
    public IReadOnlyList<TradeListItem> RecentTrades { get => _recentTrades; private set { SetProperty(ref _recentTrades, value); OnPropertyChanged(nameof(HasNoRecentTrades)); } }
    public bool HasNoRecentTrades => !IsLoading && _snapshot is not null && ErrorMessage is null && RecentTrades.Count == 0;
    public IAsyncRelayCommand<TradeListItem> ViewTradeCommand { get; }
    public Func<TradeListItem, Task>? OpenTradeAsync { get; set; }
    public bool IsEmpty => !IsLoading && ErrorMessage is null && _snapshot is not null && Currencies.Count == 0;
    public string? ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public string? StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public Task LoadTask { get; private set; } = Task.CompletedTask;
    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand PreviousCommand { get; }
    public IRelayCommand NextCommand { get; }
    public IRelayCommand CancelCommand { get; }

    public Task ActivateAsync() { _active = true; return RefreshAsync(); }
    public void Deactivate() { _active = false; Cancel(); }
    public void OnDataCommitted() { if (_active) _ = RefreshAsync(); }
    public Task RefreshAsync() => LoadTask = LoadAsync();

    private async Task LoadAsync()
    {
        long generation = ++_generation;
        _loadCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        string? preferredCurrency = _selectedCurrency;
        _snapshot = null;
        Selected = null;
        RecentTrades = [];
        Currencies = [];
        ErrorMessage = null;
        StatusMessage = "Loading Dashboard…";
        IsLoading = true;
        OnPropertyChanged(nameof(IsEmpty));
        try
        {
            DashboardAnalyticsQuery query = Query;
            DashboardAccountOption requestedAccount = SelectedAccount;
            // SQLite's async provider and aggregation can do substantial synchronous work.
            // Keep it off the dispatcher; resume here to publish UI state on the captured context.
            var result = await Task.Run(async () =>
            {
                IReadOnlyList<AccountListItem> accounts = await _accountReader.GetAllAsync(cancellation.Token);
                if (query.TradingAccountId is { } id && !accounts.Any(a => a.Id == id))
                    return (accounts, analytics: (DashboardAnalyticsSnapshot?)null, recent: (TradeListPage?)null);
                DashboardAnalyticsSnapshot analytics = await _reader.GetAsync(query, cancellation.Token);
                // Account-scoped but independent of period/currency; filtering precedes SQL paging.
                TradeListPage recent = await _tradeReader.GetPageAsync(
                    new(1, 10, TradeListSortColumn.OpenedAtUtc, TradeListSortDirection.Descending, query.TradingAccountId), cancellation.Token);
                return (accounts, analytics: (DashboardAnalyticsSnapshot?)analytics, recent: (TradeListPage?)recent);
            }, cancellation.Token);
            if (generation != _generation || cancellation.IsCancellationRequested) return;
            PublishAccounts(result.accounts, requestedAccount);
            if (result.analytics is not { } snapshot)
            {
                ErrorMessage = "The selected account is no longer available. Choose another account or All accounts.";
                StatusMessage = null;
                return;
            }
            RecentTrades = result.recent!.Items;
            _snapshot = snapshot;
            Currencies = snapshot.Currencies.Select(c => c.Currency).ToArray();
            _selectedCurrency = preferredCurrency is not null && Currencies.Contains(preferredCurrency)
                ? preferredCurrency : Currencies.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedCurrency));
            Present();
            StatusMessage = null;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        { if (generation == _generation) StatusMessage = "Loading cancelled. Select Refresh to retry."; }
        catch (Exception exception)
        {
            if (generation != _generation) return;
            _logger.LogError(exception, "Dashboard analytics load failed");
            ErrorMessage = "Dashboard could not be loaded. Select Refresh to retry.";
            StatusMessage = null;
        }
        finally
        {
            if (generation == _generation)
            {
                _loadCancellation = null;
                IsLoading = false;
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(HasNoRecentTrades));
                UpdatePeriod();
            }
        }
    }

    private void Present()
    {
        CurrencyTradeMetrics? currency = _snapshot?.Currencies.FirstOrDefault(c => c.Currency == SelectedCurrency);
        // A successful empty read gets display-only zeroes, not a fabricated currency/metric bucket.
        Selected = currency is not null || _snapshot is { Currencies.Count: 0 }
            ? new(currency, Query.ClosedFromNewYork) : null;
    }
    private void PublishAccounts(IReadOnlyList<AccountListItem> accounts, DashboardAccountOption requested)
    {
        var options = accounts.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(a => a.Id)
            .Select(a => new DashboardAccountOption(a.Id, a.Name + (a.IsActive ? "" : " (inactive)"))).Prepend(AllAccounts).ToList();
        DashboardAccountOption? selected = options.FirstOrDefault(a => a.Id == requested.Id);
        if (selected is null)
        {
            selected = new(requested.Id, requested.Name.Replace(" (unavailable)", "", StringComparison.Ordinal) + " (unavailable)");
            options.Add(selected);
        }
        _updatingAccounts = true;
        try
        {
            Accounts = options;
            _selectedAccount = selected;
            OnPropertyChanged(nameof(SelectedAccount));
        }
        finally { _updatingAccounts = false; }
    }
    private string? ValidateRange()
    {
        if (StartDate is not { } first || EndDate is not { } last) return "Choose both a Start date and an End date, then Apply range.";
        if (first > last) return "Start date must be on or before End date.";
        try { _ = new DashboardAnalyticsQuery(closedFromNewYork: DateOnly.FromDateTime(first), closedThroughNewYork: DateOnly.FromDateTime(last)); }
        catch (ArgumentException) { return "Choose a supported date range ending before 9999-12-31."; }
        return null;
    }
    private void RangeEdited()
    {
        _rangeEdited = true;
        OnPropertyChanged(nameof(RangeValidationMessage));
        OnPropertyChanged(nameof(DraftRangeLabel));
        ApplyRangeCommand.NotifyCanExecuteChanged();
    }
    private void ApplyRange()
    {
        RangeEdited();
        if (ValidateRange() is null) ApplyDates(DateOnly.FromDateTime(StartDate!.Value), DateOnly.FromDateTime(EndDate!.Value));
    }
    private void ApplyDates(DateOnly first, DateOnly last)
    {
        _customStart = first; _customEnd = last;
        SelectPeriod(DashboardPeriod.Custom);
    }
    private void SelectPeriod(DashboardPeriod period)
    {
        _period = period;
        _anchor = Today;
        OnPropertyChanged(nameof(Period));
        SyncDateInputs();
        IsDateRangeOpen = false;
        UpdatePeriod();
        _ = RefreshAsync();
    }
    private void SyncDateInputs()
    {
        _startDate = Period == DashboardPeriod.All ? null : (Period == DashboardPeriod.Custom ? _customStart : Start(_anchor)).ToDateTime(TimeOnly.MinValue);
        _endDate = Period == DashboardPeriod.All ? null : (Period == DashboardPeriod.Custom ? _customEnd : End(_anchor)).ToDateTime(TimeOnly.MinValue);
        _rangeEdited = false;
        OnPropertyChanged(nameof(StartDate)); OnPropertyChanged(nameof(EndDate)); OnPropertyChanged(nameof(RangeValidationMessage));
        OnPropertyChanged(nameof(DraftRangeLabel));
        ApplyRangeCommand.NotifyCanExecuteChanged();
    }
    private void Cancel()
    {
        ++_generation;
        _loadCancellation?.Cancel();
        _loadCancellation = null;
        IsLoading = false;
        StatusMessage = "Loading cancelled. Select Refresh to retry.";
        OnPropertyChanged(nameof(IsEmpty));
    }
    private DateOnly Today => DateOnly.FromDateTime(TradingTimePolicy.ConvertUtcToTradingTime(_time.GetUtcNow()).DateTime);
    private DateOnly Start(DateOnly date) => Period switch
    {
        DashboardPeriod.Week => DashboardMetricCalculator.GetWeekStartingMonday(date),
        DashboardPeriod.Month => new(date.Year, date.Month, 1),
        DashboardPeriod.Year => new(date.Year, 1, 1),
        _ => date
    };
    private DateOnly End(DateOnly date) => Period switch
    {
        DashboardPeriod.Week => Start(date).AddDays(6),
        DashboardPeriod.Month => Start(date).AddMonths(1).AddDays(-1),
        DashboardPeriod.Year => Start(date).AddYears(1).AddDays(-1),
        _ => date
    };
    private void Move(int direction)
    {
        if (direction > 0 ? !NextCommand.CanExecute(null) : !PreviousCommand.CanExecute(null)) return;
        _anchor = Period switch
        {
            DashboardPeriod.Week => Start(_anchor).AddDays(7 * direction),
            DashboardPeriod.Month => Start(_anchor).AddMonths(direction),
            DashboardPeriod.Year => Start(_anchor).AddYears(direction),
            _ => _anchor
        };
        SyncDateInputs();
        UpdatePeriod();
        _ = RefreshAsync();
    }
    private void UpdatePeriod()
    {
        OnPropertyChanged(nameof(PeriodLabel));
        PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
    }
}
