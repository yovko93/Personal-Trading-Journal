using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;

namespace PersonalTradingJournal.Desktop.ViewModels.Journals;

/// <summary>Read-only closed Trades for the applied journal scope, independent of its editor state.</summary>
public sealed class JournalTradeContextViewModel : ObservableObject
{
    private readonly ITradingCalendarDayReader _reader;
    private readonly ITradingAccountReader _accountReader;
    private CancellationTokenSource? _cancellation;
    private long _generation;
    private bool _active, _isLoading, _hasResult;
    private DateOnly? _date;
    private Guid? _accountId;
    private string _accountLabel = "All accounts";
    private string? _errorMessage, _notice;
    private IReadOnlyList<CalendarTradePresentation> _rows = [];
    private IReadOnlyList<CalendarPnlSummary> _summaries = [];

    public JournalTradeContextViewModel(ITradingCalendarDayReader reader, ITradingAccountReader accountReader)
    {
        _reader = reader;
        _accountReader = accountReader;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => _active && _date.HasValue,
            AsyncRelayCommandOptions.AllowConcurrentExecutions);
        CancelCommand = new RelayCommand(CancelRefresh, () => IsLoading);
    }

    public IReadOnlyList<CalendarTradePresentation> Rows => _rows;
    public IReadOnlyList<CalendarPnlSummary> Summaries => _summaries;
    public int ClosedTradeCount => Rows.Count;
    public string CountText => HasResult ? $"{ClosedTradeCount} closed {(ClosedTradeCount == 1 ? "Trade" : "Trades")}"
        : "Closed Trade count unavailable";
    public string ScopeLabel => _date is { } date
        ? $"{date:yyyy-MM-dd} New York · {_accountLabel}" : $"Choose a date · {_accountLabel}";
    public bool IsLoading => _isLoading;
    public bool IsEmpty => HasResult && Rows.Count == 0;
    public bool HasResult => _hasResult;
    public string? ErrorMessage => _errorMessage;
    public string StatusText => IsLoading ? "Loading closed Trades…"
        : !_date.HasValue ? "Choose a journal date to see its closed Trades."
        : ErrorMessage is not null ? "Trade context unavailable. Select Refresh trades to retry."
        : _notice ?? (IsEmpty ? "No closed Trades for this date and account."
            : HasResult ? "Closed Trades loaded." : "Select Refresh trades to load closed Trades.");
    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public Task LoadTask { get; private set; } = Task.CompletedTask;

    public Task ActivateAsync(DateOnly? date, Guid? account)
    {
        _active = true;
        return SetScopeAsync(date, account);
    }

    public Task SetScopeAsync(DateOnly? date, Guid? account)
    {
        CancelRead();
        _date = date;
        if (_accountId != account) _accountLabel = account.HasValue ? "Selected account" : "All accounts";
        _accountId = account;
        ClearResult();
        NotifyState();
        return LoadTask = _active ? LoadAsync() : Task.CompletedTask;
    }

    public void Deactivate()
    {
        _active = false;
        CancelRead();
        ClearResult();
        NotifyState();
    }

    public void OnDataCommitted()
    {
        if (_active) _ = RefreshAsync();
    }

    private Task RefreshAsync() => LoadTask = _active ? LoadAsync() : Task.CompletedTask;

    private async Task LoadAsync()
    {
        CancelRead();
        ClearResult();
        if (_date is not { } date)
        {
            NotifyState();
            return;
        }
        long generation = _generation;
        Guid? accountId = _accountId;
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        _isLoading = true;
        NotifyState();
        try
        {
            // Reuse the authoritative Calendar day query and metrics without reading journals.
            // SQLite work remains off the UI dispatcher, including its synchronous async APIs.
            var loaded = await Task.Run(async () =>
            {
                IReadOnlyList<AccountListItem> accounts = await _accountReader.GetAllAsync(cancellation.Token);
                AccountListItem? account = accountId is { } id ? accounts.FirstOrDefault(a => a.Id == id) : null;
                cancellation.Token.ThrowIfCancellationRequested();
                TradingCalendarDayDetails? day = accountId.HasValue && account is null ? null
                    : await _reader.GetAsync(new(date, accountId), cancellation.Token);
                return (account, day);
            }, cancellation.Token);
            if (generation != _generation || cancellation.IsCancellationRequested || !_active) return;
            _accountLabel = accountId is null ? "All accounts" : loaded.account is { } account
                ? account.Name + (account.IsActive ? "" : " (inactive)") : "Unavailable account";
            if (loaded.day is not { } result)
            {
                _errorMessage = "The selected account is no longer available. Trade context keeps its original account scope. Select Refresh trades to check again.";
                return;
            }
            _rows = result.Trades.Select(trade => new CalendarTradePresentation(trade,
                result.Classifications.GetValueOrDefault(trade.Id), result.References.GetValueOrDefault(trade.Id))).ToArray();
            _summaries = result.Currencies.Select(currency => new CalendarPnlSummary(currency.Currency, currency.Metrics)).ToArray();
            _hasResult = true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception)
        {
            if (generation != _generation || !_active) return;
            ClearResult();
            _errorMessage = "Closed Trades could not be loaded. Select Refresh trades to retry. Your journal text has not changed.";
        }
        finally
        {
            if (generation == _generation)
            {
                _cancellation = null;
                _isLoading = false;
                NotifyState();
            }
        }
    }

    private void CancelRefresh()
    {
        if (!IsLoading) return;
        CancelRead();
        ClearResult();
        _notice = "Trade context refresh cancelled. Select Refresh trades to retry.";
        NotifyState();
    }

    private void CancelRead()
    {
        _generation++;
        _cancellation?.Cancel();
        _cancellation = null;
        _isLoading = false;
    }

    private void ClearResult()
    {
        _rows = [];
        _summaries = [];
        _hasResult = false;
        _errorMessage = _notice = null;
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(Summaries));
        OnPropertyChanged(nameof(ClosedTradeCount));
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(ScopeLabel));
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(StatusText));
        RefreshCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }
}
