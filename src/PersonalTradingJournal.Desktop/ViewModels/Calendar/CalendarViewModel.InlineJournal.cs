using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Desktop.ViewModels.Journals;

namespace PersonalTradingJournal.Desktop.ViewModels.Calendar;

public sealed partial class CalendarViewModel
{
    private Func<JournalViewModel>? _dayJournalFactory;
    private Guid? _inlineJournalRowId;
    private bool _deletingDayJournal;
    public JournalViewModel? InlineJournal { get; private set; }
    public JournalViewModel? DetachedInlineJournal => DayJournals.Any(r => ReferenceEquals(r.ExpandedJournal, InlineJournal)) ? null : InlineJournal;
    public bool HasDetachedInlineJournal => DetachedInlineJournal is not null;
    public bool HasInlineJournal => InlineJournal is not null;
    public IAsyncRelayCommand OpenInlineJournalCommand { get; private set; } = null!;
    public IRelayCommand CloseInlineJournalCommand { get; private set; } = null!;

    private void InitializeDayJournal(IDailyJournalRepository? repository, IDialogService? dialogs)
    {
        // Separate from the retained standalone editor: opening a Calendar day must never
        // discard that page's draft. No History or duplicate Trade-context query in the modal.
        if (repository is not null && dialogs is not null)
            _dayJournalFactory = () => new JournalViewModel(repository, _accountReader, dialogs,
                new JournalTradeContextViewModel(_dayReader, _accountReader), _timeProvider);
        OpenInlineJournalCommand = new AsyncRelayCommand(OpenInlineJournalAsync,
            () => CanOpenDayJournal && !HasInlineJournal && _dayJournalFactory is not null);
        CloseInlineJournalCommand = new RelayCommand(() => TryCloseInlineJournal(), () => HasInlineJournal);
        AddDayJournalCommand = new AsyncRelayCommand(() => OpenDayJournalAsync(null),
            () => _isActive && !_deletingDayJournal && SelectedDate.HasValue && _dayJournalFactory is not null && InlineJournal?.IsBusy != true);
        OpenDayJournalCommand = new AsyncRelayCommand<CalendarJournalEntry>(OpenDayJournalAsync,
            row => _isActive && !_deletingDayJournal && !IsDayJournalLoading && row is not null && DayJournals.Contains(row)
                && _dayJournalFactory is not null && InlineJournal?.IsBusy != true);
        DeleteDayJournalCommand = new AsyncRelayCommand<CalendarJournalEntry>(DeleteDayJournalAsync,
            row => _isActive && !_deletingDayJournal && !IsDayJournalLoading && row is not null && DayJournals.Contains(row)
                && _dayJournalFactory is not null && InlineJournal?.IsBusy != true);
        RefreshDayJournalsCommand = new AsyncRelayCommand(RefreshInlineAndListAsync,
            () => _isActive && !_deletingDayJournal && SelectedDate.HasValue, AsyncRelayCommandOptions.AllowConcurrentExecutions);
    }

    private async Task OpenDayJournalAsync(CalendarJournalEntry? row)
    {
        if (!_isActive || _dayJournalFactory is null || SelectedDate is not { } date
            || (row is not null && !DayJournals.Contains(row))) return;
        bool closing = row?.IsExpanded == true;
        if (!TryCloseInlineJournal() || closing) return;
        var journal = _dayJournalFactory();
        Guid? account = row is null ? SelectedAccount.Id : row.AccountId;
        if (!journal.TryOpenScope(date, account, row?.AccountLabel ?? SelectedAccount.Name)) return;
        InlineJournal = journal;
        _inlineJournalRowId = row?.Id;
        journal.JournalDataCommitted += OnInlineJournalCommitted;
        NotifyInlineJournal();
        await journal.ActivateAsync(loadTradeContext: false, newEntry: row is null, expectedJournalId: row?.Id);
        if (!ReferenceEquals(InlineJournal, journal) || !_isActive) return;
        if (journal.ErrorMessage is not null) return; // Retain guarded load-error recovery, never authorize a replacement.
        // A deleted/recreated or moved journal at the same date/scope is not this row.
        if (row is not null && journal.JournalId != row.Id)
        {
            ReleaseInlineJournal();
            await RefreshDayJournalsAsync();
            DayJournalError = "This Journal was moved or deleted. The list was refreshed; select its current entry to continue.";
            NotifyDayJournals();
            return;
        }
        if (journal.OpenEditorCommand.CanExecute(null)) journal.OpenEditorCommand.Execute(null);
        NotifyInlineJournal();
    }

    private async Task RefreshInlineAndListAsync()
    {
        if (_deletingDayJournal) return;
        if (InlineJournal is { } journal && !await journal.RefreshAsync()) return;
        if (_isActive) await RefreshJournalStatusesAsync();
    }

    private async Task OpenInlineJournalAsync()
    {
        if (!CanOpenDayJournal || HasInlineJournal || _dayJournalFactory is null || SelectedDate is not { } date) return;
        var journal = _dayJournalFactory();
        if (!journal.TryOpenScope(date, SelectedAccount.Id, SelectedAccount.Name)) return;
        InlineJournal = journal;
        journal.JournalDataCommitted += OnInlineJournalCommitted;
        NotifyInlineJournal();
        await journal.ActivateAsync(loadTradeContext: false);
        if (ReferenceEquals(InlineJournal, journal) && _isActive && journal.OpenEditorCommand.CanExecute(null))
            journal.OpenEditorCommand.Execute(null);
    }

    private async Task DeleteDayJournalAsync(CalendarJournalEntry? row)
    {
        if (!DeleteDayJournalCommand.CanExecute(row) || row is null || SelectedDate is not { } date) return;
        // Reuse the exact editor when open, so its dirty fields and loaded token stay protected.
        // A collapsed card gets a temporary, non-presented host; other open drafts are untouched.
        bool usesInline = InlineJournal?.JournalId == row.Id;
        var journal = usesInline ? InlineJournal! : _dayJournalFactory!();
        _deletingDayJournal = true;
        DayJournalError = null;
        NotifyDayJournals();
        try
        {
            if (!usesInline)
            {
                if (!journal.TryOpenScope(date, row.AccountId, row.AccountLabel)) return;
                await journal.ActivateAsync(loadTradeContext: false, expectedJournalId: row.Id);
            }
            if (!_isActive || SelectedDate != date || !DayJournals.Contains(row)) return;
            if (journal.JournalId != row.Id || journal.Revision != row.Details.Entry.Revision)
            {
                DayJournalError = "This Journal changed, moved or was deleted. Nothing was deleted. Refresh Journals before deciding again; open edits are kept.";
                return;
            }
            if (!journal.DeleteCommand.CanExecute(null))
            {
                DayJournalError = journal.ErrorMessage ?? "This Journal cannot be deleted now. Refresh Journals to review its current state.";
                return;
            }
            // Existing destructive confirmation explicitly protects unsaved fields and all history.
            await journal.DeleteCommand.ExecuteAsync(null);
            if (!journal.IsExisting)
            {
                if (!usesInline) OnJournalCommitted();
                await JournalLoadTask;
            }
            else if (_isActive && SelectedDate == date)
                DayJournalError = journal.ErrorMessage; // Confirmation cancellation is not an error.
        }
        finally
        {
            if (!usesInline) journal.Deactivate();
            _deletingDayJournal = false;
            NotifyDayJournals();
        }
    }

    private void OnInlineJournalCommitted(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, InlineJournal) && InlineJournal?.JournalId is null) ReleaseInlineJournal();
        OnJournalCommitted();
    }

    public bool TryCloseInlineJournal()
    {
        if (_deletingDayJournal) return false;
        if (InlineJournal is { } journal && !journal.TryLeave()) return false;
        ReleaseInlineJournal();
        return true;
    }

    private void ReleaseInlineJournal()
    {
        if (InlineJournal is not { } journal) return;
        journal.JournalDataCommitted -= OnInlineJournalCommitted;
        journal.Deactivate(); // Cancels obsolete reads; their generation cannot publish to a new editor.
        InlineJournal = null;
        _inlineJournalRowId = null;
        NotifyInlineJournal();
    }

    private void NotifyInlineJournal()
    {
        NotifyDayJournals();
        OnPropertyChanged(nameof(InlineJournal));
        OnPropertyChanged(nameof(HasInlineJournal));
        OpenInlineJournalCommand.NotifyCanExecuteChanged();
        CloseInlineJournalCommand.NotifyCanExecuteChanged();
    }
}
