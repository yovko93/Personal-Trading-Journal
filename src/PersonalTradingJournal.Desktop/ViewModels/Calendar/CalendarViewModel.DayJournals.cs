using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.ViewModels.Journals;

namespace PersonalTradingJournal.Desktop.ViewModels.Calendar;

public sealed class CalendarJournalEntry(DailyJournalDetails details) : ObservableObject
{
    public DailyJournalDetails Details { get; } = details;
    private JournalViewModel? _expandedJournal;
    public JournalViewModel? ExpandedJournal
    {
        get => _expandedJournal;
        internal set
        {
            if (!SetProperty(ref _expandedJournal, value)) return;
            OnPropertyChanged(nameof(IsExpanded));
            OnPropertyChanged(nameof(ActionLabel));
            OnPropertyChanged(nameof(ActionAccessibleName));
        }
    }
    public bool IsExpanded => ExpandedJournal is not null;
    public Guid Id => Details.Entry.Id;
    public Guid? AccountId => Details.Entry.TradingAccountId;
    public string AccountLabel => Details.AccountState switch
    {
        DailyJournalAccountState.AllAccounts => "All accounts",
        DailyJournalAccountState.Inactive => $"{Details.AccountName} (inactive)",
        DailyJournalAccountState.Unavailable => $"Unavailable account ({AccountId})",
        _ => Details.AccountName ?? "Unavailable account",
    };
    public string StateLabel => Details.Entry.IsDraft ? "Draft" : "Completed";
    public string ActionLabel => IsExpanded ? "Close Journal" : "Open Journal";
    public string ActionAccessibleName => ActionLabel + ": " + Description;
    public string Text => Details.Entry.Text;
    public string Description => $"{Details.Entry.TradingDate:yyyy-MM-dd} · {AccountLabel} · {StateLabel} · Revision {Details.Entry.Revision}";
}

public sealed partial class CalendarViewModel
{
    private readonly IDailyJournalDayReader? _dayJournalReader;
    private CancellationTokenSource? _dayJournalCancellation;
    private long _dayJournalGeneration;
    private bool _dayJournalsLoaded;
    public IReadOnlyList<CalendarJournalEntry> DayJournals { get; private set; } = [];
    public bool IsDayJournalLoading { get; private set; }
    public string? DayJournalError { get; private set; }
    public Task DayJournalLoadTask { get; private set; } = Task.CompletedTask;
    public IAsyncRelayCommand AddDayJournalCommand { get; private set; } = null!;
    public IAsyncRelayCommand<CalendarJournalEntry> OpenDayJournalCommand { get; private set; } = null!;
    public IAsyncRelayCommand RefreshDayJournalsCommand { get; private set; } = null!;
    public string DayJournalStatusMessage => IsDayJournalLoading ? "Loading day Journals…"
        : DayJournalError ?? (_dayJournalsLoaded
            ? DayJournals.Count == 0 ? "No Journal for this date in the selected Account filter."
                : $"{DayJournals.Count} {(DayJournals.Count == 1 ? "Journal" : "Journals")}"
            : "Day Journals not loaded. Select Refresh Journals to retry.");

    private Task RefreshDayJournalsAsync() => DayJournalLoadTask = LoadDayJournalsAsync();

    private async Task LoadDayJournalsAsync()
    {
        CancelDayJournals();
        if (!_isActive || SelectedDate is not { } date) return;
        if (_dayJournalReader is null)
        {
            DayJournalError = "Day Journals reader is unavailable.";
            NotifyDayJournals();
            return;
        }
        long generation = _dayJournalGeneration;
        Guid? account = SelectedAccount.Id;
        using var cancellation = new CancellationTokenSource();
        _dayJournalCancellation = cancellation;
        IsDayJournalLoading = true;
        NotifyDayJournals();
        try
        {
            var rows = await Task.Run(() => _dayJournalReader.GetDayAsync(date, account, cancellation.Token), cancellation.Token);
            if (!_isActive || generation != _dayJournalGeneration || cancellation.IsCancellationRequested
                || SelectedDate != date || SelectedAccount.Id != account) return;
            DayJournals = rows.Select(row => new CalendarJournalEntry(row)).ToArray();
            _dayJournalsLoaded = true;
            // Refresh the saved cards without touching an open/dirty editor. A clean
            // retained review that moved out of this filter or changed elsewhere is
            // replaced by the fresh list, not a second stale snapshot below it.
            if (InlineJournal is { IsEditorOpen: false, IsDirty: false, IsBusy: false, JournalId: { } id } editor
                && !DayJournals.Any(row => row.Id == id && row.Details.Entry.Revision == editor.Revision))
                ReleaseInlineJournal();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception)
        {
            if (generation == _dayJournalGeneration && _isActive)
                DayJournalError = "Day Journals could not be loaded. Select Refresh Journals to retry. Any open edits are kept.";
        }
        finally
        {
            if (generation == _dayJournalGeneration)
            {
                _dayJournalCancellation = null;
                IsDayJournalLoading = false;
                NotifyDayJournals();
            }
        }
    }

    private void CancelDayJournals()
    {
        _dayJournalGeneration++;
        _dayJournalCancellation?.Cancel();
        _dayJournalCancellation = null;
        DayJournals = [];
        _dayJournalsLoaded = IsDayJournalLoading = false;
        DayJournalError = null;
        NotifyDayJournals();
    }

    private void NotifyDayJournals()
    {
        foreach (var row in DayJournals)
            row.ExpandedJournal = row.Id == (_inlineJournalRowId ?? InlineJournal?.JournalId) ? InlineJournal : null;
        OnPropertyChanged(nameof(DetachedInlineJournal));
        OnPropertyChanged(nameof(HasDetachedInlineJournal));
        OnPropertyChanged(nameof(DayJournals));
        OnPropertyChanged(nameof(IsDayJournalLoading));
        OnPropertyChanged(nameof(DayJournalError));
        OnPropertyChanged(nameof(DayJournalStatusMessage));
        AddDayJournalCommand?.NotifyCanExecuteChanged();
        OpenDayJournalCommand?.NotifyCanExecuteChanged();
        RefreshDayJournalsCommand?.NotifyCanExecuteChanged();
    }
}
