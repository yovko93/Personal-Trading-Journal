using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.Common.Time;

namespace PersonalTradingJournal.Desktop.ViewModels.Dashboard;

public enum DashboardPeriod { All, Week, Month, Year }

public sealed class DashboardViewModel : ObservableObject
{
    private readonly IDashboardAnalyticsReader _reader;
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

    public DashboardViewModel(IDashboardAnalyticsReader reader, TimeProvider timeProvider,
        ILogger<DashboardViewModel>? logger = null)
    {
        _reader = reader;
        _time = timeProvider;
        _logger = logger ?? NullLogger<DashboardViewModel>.Instance;
        _anchor = Today;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, AsyncRelayCommandOptions.AllowConcurrentExecutions);
        PreviousCommand = new RelayCommand(() => Move(-1), () => Period != DashboardPeriod.All && _anchor.Year > 1);
        NextCommand = new RelayCommand(() => Move(1), () => Period != DashboardPeriod.All && Start(_anchor) < Start(Today));
        CancelCommand = new RelayCommand(Cancel, () => IsLoading);
    }

    public IReadOnlyList<DashboardPeriod> Periods { get; } = Enum.GetValues<DashboardPeriod>();
    public DashboardPeriod Period
    {
        get => _period;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (!SetProperty(ref _period, value)) return;
            _anchor = Today;
            UpdatePeriod();
            _ = RefreshAsync();
        }
    }
    public string PeriodLabel => Period == DashboardPeriod.All ? "All history · New York closure dates"
        : $"{Start(_anchor):yyyy-MM-dd} – {End(_anchor):yyyy-MM-dd} · New York";
    public DashboardAnalyticsQuery Query => Period == DashboardPeriod.All ? new()
        : new(closedFromNewYork: Start(_anchor), closedThroughNewYork: End(_anchor));
    public IReadOnlyList<string> Currencies { get => _currencies; private set => SetProperty(ref _currencies, value); }
    public string? SelectedCurrency
    {
        get => _selectedCurrency;
        set { if (SetProperty(ref _selectedCurrency, value)) Present(); }
    }
    public DashboardCurrencyPresentation? Selected { get => _selected; private set => SetProperty(ref _selected, value); }
    public bool IsLoading { get => _isLoading; private set { SetProperty(ref _isLoading, value); CancelCommand.NotifyCanExecuteChanged(); } }
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
        Currencies = [];
        ErrorMessage = null;
        StatusMessage = "Loading Dashboard…";
        IsLoading = true;
        OnPropertyChanged(nameof(IsEmpty));
        try
        {
            DashboardAnalyticsQuery query = Query;
            // SQLite's async provider and aggregation can do substantial synchronous work.
            // Keep it off the dispatcher; resume here to publish UI state on the captured context.
            DashboardAnalyticsSnapshot snapshot = await Task.Run(
                () => _reader.GetAsync(query, cancellation.Token), cancellation.Token);
            if (generation != _generation || cancellation.IsCancellationRequested) return;
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
                UpdatePeriod();
            }
        }
    }

    private void Present()
    {
        CurrencyTradeMetrics? currency = _snapshot?.Currencies.FirstOrDefault(c => c.Currency == SelectedCurrency);
        Selected = currency is null ? null : new(currency, Query.ClosedFromNewYork);
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
