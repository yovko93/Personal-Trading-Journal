using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Common.Time;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Domain.Journals;

namespace PersonalTradingJournal.Desktop.ViewModels.Journals;

public sealed record JournalAccountOption(Guid? Id, string Name, bool IsAvailable = true);

/// <summary>An explicit-save editor for one New York date and one exact Account scope.</summary>
public sealed class JournalViewModel : ObservableObject
{
    private static readonly JournalAccountOption AllAccounts = new(null, "All accounts");
    private readonly IDailyJournalRepository _repository;
    private readonly ITradingAccountReader _accountReader;
    private readonly IDialogService _dialogs;
    private IReadOnlyList<JournalAccountOption> _accounts = [AllAccounts];
    private JournalAccountOption _selectedAccount = AllAccounts;
    private DateTime? _selectedDate;
    private DailyJournalEntry? _entry;
    private string _text = "", _savedText = "";
    private string? _errorMessage, _notice;
    private bool _active, _hasLoaded, _isLoading, _isSaving, _reloadRequired, _updatingAccounts, _hasDateInputError;
    private CancellationTokenSource? _loadCancellation, _saveCancellation;
    private long _generation;

    public JournalViewModel(IDailyJournalRepository repository, ITradingAccountReader accountReader,
        IDialogService dialogs, TimeProvider? timeProvider = null)
    {
        _repository = repository;
        _accountReader = accountReader;
        _dialogs = dialogs;
        _selectedDate = TradingTimePolicy.ConvertUtcToTradingTime(
            (timeProvider ?? TimeProvider.System).GetUtcNow()).Date;
        SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        ReloadCommand = new AsyncRelayCommand(ReloadAsync, () => _active && !IsBusy,
            AsyncRelayCommandOptions.AllowConcurrentExecutions);
        CancelOperationCommand = new RelayCommand(CancelOperation,
            () => IsLoading || (IsSaving && _saveCancellation?.IsCancellationRequested == false));
    }

    public IReadOnlyList<JournalAccountOption> Accounts => _accounts;
    public JournalAccountOption SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            // WPF can temporarily clear selection while replacing ItemsSource. Only the
            // explicit All accounts item may select the independent global journal.
            if (_updatingAccounts || value is null || value.Id == _selectedAccount.Id) return;
            if (value.Id == Guid.Empty || !TryChangeScope())
            {
                OnPropertyChanged();
                return;
            }
            _selectedAccount = value;
            OnPropertyChanged();
            ScopeChanged();
        }
    }

    public DateTime? SelectedDate
    {
        get => _selectedDate;
        set
        {
            DateTime? date = value.HasValue ? DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Unspecified) : null;
            if (_selectedDate == date) return;
            if (!TryChangeScope())
            {
                OnPropertyChanged();
                return;
            }
            _selectedDate = date;
            OnPropertyChanged();
            ScopeChanged();
        }
    }

    public string Text
    {
        get => _text;
        set
        {
            if (!CanEdit || value is null || !SetProperty(ref _text, value)) return;
            _notice = null;
            NotifyState();
        }
    }

    public bool HasDateInputError
    {
        get => _hasDateInputError;
        set { if (SetProperty(ref _hasDateInputError, value)) NotifyState(); }
    }
    public bool IsDirty => !string.Equals(_text, _savedText, StringComparison.Ordinal);
    public bool IsExisting => _entry is not null;
    public bool IsDraft => _entry?.IsDraft ?? true;
    public long? Revision => _entry?.Revision;
    public bool IsLoading => _isLoading;
    public bool IsSaving => _isSaving;
    public bool IsBusy => IsLoading || IsSaving;
    public bool CanChangeScope => !IsSaving;
    public bool CanEdit => _active && _hasLoaded && SelectedDate.HasValue && SelectedAccount.IsAvailable && !IsBusy;
    public string CharacterCountText => $"{Text.Length:N0} / {DailyJournalEntry.MaximumTextLength:N0} characters";
    public string? ErrorMessage => HasDateInputError ? "Enter a valid journal date before saving."
        : Text.Length > DailyJournalEntry.MaximumTextLength
            ? "Journal text cannot exceed 100,000 UTF-16 code units. Your text has been kept; shorten it before saving."
            : _errorMessage;
    public string ScopeMessage => SelectedAccount.Id is null
        ? "All accounts is its own journal, separate from each account's journal. Dates use New York trading time."
        : !SelectedAccount.IsAvailable
            ? "This account is unavailable. Its journal keeps its original account scope and cannot be saved."
            : $"Journal for {SelectedAccount.Name}. Dates use New York trading time.";
    public string StatusText => IsSaving
        ? _saveCancellation?.IsCancellationRequested == true ? "Cancelling save…" : "Saving…"
        : IsLoading ? "Loading journal…"
        : !SelectedDate.HasValue ? "Choose a journal date."
        : _reloadRequired ? "Reload required before saving. Your text has been kept."
        : IsDirty ? "Unsaved changes"
        : _notice ?? (!_hasLoaded ? "Select Reload to load this journal."
            : !IsExisting ? "New draft — not saved"
            : IsDraft ? $"Saved draft · Revision {Revision}" : $"Saved journal · Revision {Revision}");

    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand ReloadCommand { get; }
    public IRelayCommand CancelOperationCommand { get; }
    public Task LoadTask { get; private set; } = Task.CompletedTask;

    public Task ActivateAsync()
    {
        // Repeated activation cannot discard work in an already-visible editor.
        if (_active && (IsDirty || IsSaving)) return LoadTask;
        _active = true;
        return LoadTask = LoadAsync();
    }

    public bool TryLeave() => !IsSaving && ConfirmDiscard();

    public void Deactivate()
    {
        if (IsSaving) return;
        _active = false;
        CancelLoad();
        NotifyState();
    }

    private bool TryChangeScope() => !IsSaving && ConfirmDiscard();

    private bool ConfirmDiscard() => !IsDirty || _dialogs.Confirm(new ConfirmationDialogRequest(
        "Discard journal changes?", "This journal has unsaved changes. Discard them to continue?",
        "Discard changes", "Keep editing", isDestructive: true));

    private void ScopeChanged()
    {
        CancelLoad();
        _entry = null;
        _text = _savedText = "";
        _hasLoaded = _reloadRequired = false;
        _errorMessage = _notice = null;
        OnPropertyChanged(nameof(Text));
        NotifyState();
        if (_active) LoadTask = LoadAsync();
    }

    private Task ReloadAsync()
    {
        if (!_active || IsBusy || !ConfirmDiscard()) return Task.CompletedTask;
        return LoadTask = LoadAsync();
    }

    private async Task LoadAsync()
    {
        CancelLoad();
        long generation = _generation;
        using var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        DateOnly? date = SelectedDate is { } selected ? DateOnly.FromDateTime(selected) : null;
        JournalAccountOption account = SelectedAccount;
        _isLoading = true;
        _errorMessage = _notice = null;
        NotifyState();
        try
        {
            // SQLite's async APIs can execute synchronously; do that work off the dispatcher.
            var loaded = await Task.Run(async () =>
            {
                IReadOnlyList<AccountListItem> accounts = await _accountReader.GetAllAsync(cancellation.Token);
                DailyJournalDetails? journal = date.HasValue
                    ? await _repository.GetAsync(date.Value, account.Id, cancellation.Token) : null;
                return (accounts, journal);
            }, cancellation.Token);
            if (generation != _generation || cancellation.IsCancellationRequested || !_active) return;
            PublishAccounts(loaded.accounts, account, loaded.journal);
            _entry = loaded.journal?.Entry;
            _text = _savedText = _entry?.Text ?? "";
            _hasLoaded = date.HasValue;
            _reloadRequired = false;
            if (!SelectedAccount.IsAvailable) _errorMessage = UnavailableAccountMessage;
            OnPropertyChanged(nameof(Text));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception)
        {
            if (generation != _generation || !_active) return;
            _errorMessage = "Journal could not be loaded. Your text has been kept. Select Reload to retry.";
        }
        finally
        {
            if (generation == _generation)
            {
                _loadCancellation = null;
                _isLoading = false;
                NotifyState();
            }
        }
    }

    private void PublishAccounts(IReadOnlyList<AccountListItem> accounts, JournalAccountOption requested,
        DailyJournalDetails? journal)
    {
        var options = accounts.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(a => a.Id)
            .Select(a => new JournalAccountOption(a.Id, a.Name + (a.IsActive ? "" : " (inactive)")))
            .Prepend(AllAccounts).ToList();
        JournalAccountOption? selected = options.FirstOrDefault(a => a.Id == requested.Id);
        if (selected is null || journal?.AccountState == DailyJournalAccountState.Unavailable)
        {
            string name = journal?.AccountName ?? requested.Name;
            selected = new(requested.Id, name.Replace(" (unavailable)", "", StringComparison.Ordinal) + " (unavailable)", false);
            options.RemoveAll(a => a.Id == requested.Id);
            options.Add(selected);
        }
        _updatingAccounts = true;
        try
        {
            _selectedAccount = selected;
            _accounts = options;
            OnPropertyChanged(nameof(Accounts));
            OnPropertyChanged(nameof(SelectedAccount));
        }
        finally { _updatingAccounts = false; }
    }

    private bool CanSave() => CanEdit && !_reloadRequired && !HasDateInputError
        && Text.Length <= DailyJournalEntry.MaximumTextLength;

    private async Task SaveAsync()
    {
        // Guard the method as well as ICommand so repeated invocations remain single-flight.
        if (!CanSave()) return;
        using var cancellation = new CancellationTokenSource();
        _saveCancellation = cancellation;
        _isSaving = true;
        _errorMessage = _notice = null;
        string text = Text;
        DailyJournalEntry? entry = _entry;
        DateOnly date = DateOnly.FromDateTime(SelectedDate!.Value);
        Guid? accountId = SelectedAccount.Id;
        NotifyState();
        try
        {
            DailyJournalWriteResult result = await Task.Run(() => entry is null
                ? _repository.CreateAsync(new(date, accountId, text), cancellation.Token)
                : _repository.UpdateAsync(new(entry.Id, entry.Revision, text, entry.IsDraft), cancellation.Token), cancellation.Token);
            // A repository can return a committed result after cancellation was requested.
            // Its authoritative result must still be accepted; cancellation cannot undo a commit.
            switch (result.Status)
            {
                case DailyJournalWriteStatus.Created:
                case DailyJournalWriteStatus.Updated:
                case DailyJournalWriteStatus.Unchanged:
                    if (result.Journal is null) throw new InvalidOperationException("A saved journal result is required.");
                    _entry = result.Journal.Entry;
                    _text = _savedText = _entry.Text;
                    _reloadRequired = false;
                    _notice = result.Status == DailyJournalWriteStatus.Unchanged ? "No changes to save." : null;
                    OnPropertyChanged(nameof(Text));
                    break;
                case DailyJournalWriteStatus.AlreadyExists:
                    _reloadRequired = true;
                    _errorMessage = "A journal was created for this date and account elsewhere. Your text has been kept. Reload to read it before saving.";
                    break;
                case DailyJournalWriteStatus.Conflict:
                    _reloadRequired = true;
                    _errorMessage = "This journal was changed elsewhere. Your text has been kept. Reload the latest revision before saving.";
                    break;
                case DailyJournalWriteStatus.NotFound:
                    _reloadRequired = true;
                    _errorMessage = "This journal no longer exists. Your text has been kept. Reload before saving.";
                    break;
                case DailyJournalWriteStatus.AccountUnavailable:
                    _reloadRequired = true;
                    _errorMessage = UnavailableAccountMessage;
                    break;
                default:
                    throw new InvalidOperationException("Unknown journal save result.");
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            _errorMessage = "Save cancelled. Your text has been kept.";
        }
        catch (Exception)
        {
            _errorMessage = "Journal could not be saved. Your text has been kept. Try Save again.";
        }
        finally
        {
            _saveCancellation = null;
            _isSaving = false;
            NotifyState();
        }
    }

    private void CancelOperation()
    {
        if (IsSaving)
        {
            _saveCancellation?.Cancel();
        }
        else if (IsLoading)
        {
            CancelLoad();
            _notice = "Loading cancelled. Select Reload to retry.";
        }
        NotifyState();
    }

    private void CancelLoad()
    {
        _generation++;
        _loadCancellation?.Cancel();
        _loadCancellation = null;
        _isLoading = false;
    }

    private const string UnavailableAccountMessage =
        "The selected account is no longer available. Your text and account scope have been kept. Select Reload to check again, or choose another scope.";

    private void NotifyState()
    {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsExisting));
        OnPropertyChanged(nameof(IsDraft));
        OnPropertyChanged(nameof(Revision));
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(IsSaving));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanChangeScope));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CharacterCountText));
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(ScopeMessage));
        OnPropertyChanged(nameof(StatusText));
        SaveCommand.NotifyCanExecuteChanged();
        ReloadCommand.NotifyCanExecuteChanged();
        CancelOperationCommand.NotifyCanExecuteChanged();
    }
}
