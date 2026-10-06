using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Desktop.ViewModels.Journals;

namespace PersonalTradingJournal.Desktop.ViewModels.Calendar;

public sealed partial class CalendarViewModel
{
    private Func<JournalViewModel>? _dayJournalFactory;
    public JournalViewModel? InlineJournal { get; private set; }
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

    private void OnInlineJournalCommitted(object? sender, EventArgs e) => OnJournalCommitted();

    public bool TryCloseInlineJournal()
    {
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
        NotifyInlineJournal();
    }

    private void NotifyInlineJournal()
    {
        OnPropertyChanged(nameof(InlineJournal));
        OnPropertyChanged(nameof(HasInlineJournal));
        OpenInlineJournalCommand.NotifyCanExecuteChanged();
        CloseInlineJournalCommand.NotifyCanExecuteChanged();
    }
}
