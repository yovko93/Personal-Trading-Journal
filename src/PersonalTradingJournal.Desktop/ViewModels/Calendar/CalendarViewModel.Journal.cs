using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Journals;
using Microsoft.Extensions.Logging;

namespace PersonalTradingJournal.Desktop.ViewModels.Calendar;

public sealed partial class CalendarDayCell
{
    public DailyJournalStatus? JournalStatus { get; private set; }
    public bool HasJournal => JournalStatus is not null;
    public string JournalStatusText => JournalStatus is { IsDraft: true } ? "Draft" : HasJournal ? "Completed" : "";
    public string JournalIndicatorText => JournalStatus is { IsDraft: true } ? "Draft" : HasJournal ? "✓" : "";
    private bool _journalStatusLoaded;
    public string JournalAccessibleDescription => JournalStatus is { } status
        ? $"Daily Journal {JournalStatusText.ToLowerInvariant()}, revision {status.Revision}, in the current Account scope."
        : _journalStatusLoaded ? "No Daily Journal for this date in the current Account scope." : "";

    internal void UpdateJournal(DailyJournalStatus? status, bool loaded)
    {
        if (JournalStatus == status && _journalStatusLoaded == loaded) return;
        JournalStatus = status;
        _journalStatusLoaded = loaded;
        OnPropertyChanged(string.Empty);
    }
}

public sealed partial class CalendarViewModel
{
    private readonly IDailyJournalStatusReader? _journalStatusReader;
    private CancellationTokenSource? _journalCancellation;
    private long _journalGeneration;
    private DateOnly? _journalGridStart, _journalGridEnd;
    private Guid? _journalAccountId;
    private IReadOnlyDictionary<DateOnly, DailyJournalStatus> _journalStatuses = new Dictionary<DateOnly, DailyJournalStatus>();
    private bool _journalStatusesLoaded, _isJournalLoading;
    private string? _journalErrorMessage;

    /// <summary>The shell supplies guarded Journal navigation; the view first closes the Day modal.</summary>
    public Func<DateOnly, CalendarAccountOption, Task>? OpenJournalAsync { get; set; }
    public Task JournalNavigationTask { get; private set; } = Task.CompletedTask;
    public Task JournalLoadTask { get; private set; } = Task.CompletedTask;
    public DailyJournalStatus? SelectedDayJournalStatus => SelectedDate is { } date
        ? _journalStatuses.GetValueOrDefault(date) : null;
    public bool CanOpenDayJournal => _isActive && SelectedDate.HasValue && _journalStatusesLoaded &&
        !IsJournalLoading && JournalErrorMessage is null;
    public string DayJournalActionText => SelectedDayJournalStatus is { IsDraft: true } ? "Continue Journal"
        : SelectedDayJournalStatus is not null ? "Open Journal" : "Add Journal";
    public string DayJournalStatusMessage => IsJournalLoading ? "Loading Journal status…"
        : JournalErrorMessage ?? (SelectedDayJournalStatus is { IsDraft: true } ? "Draft Journal"
            : SelectedDayJournalStatus is not null ? "Completed Journal"
            : _journalStatusesLoaded ? "No Journal for this date and Account scope."
            : "Journal status not loaded. Select Refresh to retry.");
    public string? JournalErrorMessage
    {
        get => _journalErrorMessage;
        private set
        {
            if (SetProperty(ref _journalErrorMessage, value)) NotifyJournalDay();
        }
    }
    public bool IsJournalLoading
    {
        get => _isJournalLoading;
        private set
        {
            if (!SetProperty(ref _isJournalLoading, value)) return;
            NotifyBusy();
            NotifyJournalDay();
        }
    }

    public Task NavigateToJournalAsync(DateOnly date, CalendarAccountOption account)
    {
        // Preserve the request captured before protected modal closure. Never infer a scope
        // from the first Trade, currency, a changed selection, or an unavailable Account.
        if (!CanOpenDayJournal || date != SelectedDate || account.Id != SelectedAccount.Id || OpenJournalAsync is null)
            return Task.CompletedTask;
        return JournalNavigationTask = OpenJournalAsync(date, account);
    }

    public void OnJournalCommitted()
    {
        if (_isActive) _ = RefreshJournalStatusesAsync();
        else CancelJournalStatuses();
    }

    private void EnsureJournalGrid(TradingCalendarQuery query)
    {
        if (_journalGridStart == query.GridStart && _journalGridEnd == query.GridEnd && _journalAccountId == SelectedAccount.Id) return;
        CancelJournalStatuses();
        _journalGridStart = query.GridStart;
        _journalGridEnd = query.GridEnd;
        _journalAccountId = SelectedAccount.Id;
    }

    private Task RefreshJournalStatusesAsync() => JournalLoadTask = LoadJournalStatusesAsync();

    private async Task LoadJournalStatusesAsync()
    {
        if (_journalStatusReader is null || !_isActive) return;
        long generation = ++_journalGeneration;
        _journalCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _journalCancellation = cancellation;
        var query = new TradingCalendarQuery(_month.Year, _month.Month, SelectedAccount.Id);
        Guid? accountId = SelectedAccount.Id;
        _journalStatusesLoaded = false;
        _journalStatuses = new Dictionary<DateOnly, DailyJournalStatus>();
        JournalErrorMessage = null;
        IsJournalLoading = true;
        PublishJournalStatuses();
        try
        {
            var statuses = await Task.Run(async () =>
            {
                if (accountId is { } id && !(await _accountReader.GetAllAsync(cancellation.Token)).Any(a => a.Id == id))
                    return null;
                return await _journalStatusReader.GetAsync(query.GridStart, query.GridEnd, accountId, cancellation.Token);
            }, cancellation.Token);
            if (generation != _journalGeneration || cancellation.IsCancellationRequested || !_isActive ||
                _month != query.MonthStart || SelectedAccount.Id != accountId) return;
            if (statuses is null) JournalErrorMessage = UnavailableAccountMessage;
            else
            {
                _journalStatuses = statuses.ToDictionary(status => status.TradingDate);
                _journalStatusesLoaded = true;
                PublishJournalStatuses();
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (generation != _journalGeneration || !_isActive) return;
            _logger.LogError(exception, "Calendar Journal status load failed");
            JournalErrorMessage = "Journal status could not be loaded. Select Refresh to retry.";
        }
        finally
        {
            if (generation == _journalGeneration)
            {
                _journalCancellation = null;
                IsJournalLoading = false;
                NotifyJournalDay();
            }
        }
    }

    private void CancelJournalStatuses()
    {
        _journalGeneration++;
        _journalCancellation?.Cancel();
        _journalCancellation = null;
        _journalStatusesLoaded = false;
        _journalStatuses = new Dictionary<DateOnly, DailyJournalStatus>();
        JournalErrorMessage = null;
        IsJournalLoading = false;
        PublishJournalStatuses();
    }

    private void PublishJournalStatuses()
    {
        foreach (CalendarDayCell cell in Weeks.SelectMany(week => week.Days))
            cell.UpdateJournal(_journalStatuses.GetValueOrDefault(cell.Date), _journalStatusesLoaded);
        NotifyJournalDay();
    }

    private void NotifyJournalDay()
    {
        OnPropertyChanged(nameof(SelectedDayJournalStatus));
        OnPropertyChanged(nameof(CanOpenDayJournal));
        OnPropertyChanged(nameof(DayJournalActionText));
        OnPropertyChanged(nameof(DayJournalStatusMessage));
    }
}
