using System.Globalization;
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
    private readonly TimeProvider _clock;
    private IReadOnlyList<JournalAccountOption> _accounts = [AllAccounts];
    private JournalAccountOption _selectedAccount = AllAccounts;
    private JournalAccountOption _editorAccount = AllAccounts, _loadedAccount = AllAccounts;
    private bool _targetCollision;
    private Guid? _expectedJournalId;
    private DateTime? _selectedDate;
    private DailyJournalEntry? _entry;
    private string _text = "", _savedText = "";
    private string _wentWell = "", _needsImprovement = "", _nextTradingDay = "";
    private DailyReviewAnswers _savedReview = DailyReviewAnswers.Empty;
    private string? _errorMessage, _notice;
    private bool _active, _hasLoaded, _isLoading, _isSaving, _reloadRequired, _updatingAccounts, _hasDateInputError, _completionAttempted;
    private bool _preserveOnNextActivation;
    private bool _resetOnNextActivation;
    private bool _isEditorOpen;
    private bool _isDeleting;
    private bool _loadTradeContext = true;
    private CancellationTokenSource? _loadCancellation, _saveCancellation;
    private long _generation;

    public JournalViewModel(IDailyJournalRepository repository, ITradingAccountReader accountReader,
        IDialogService dialogs, JournalTradeContextViewModel tradeContext, TimeProvider? timeProvider = null,
        IDailyJournalHistoryReader? historyReader = null)
    {
        _repository = repository;
        _accountReader = accountReader;
        _dialogs = dialogs;
        _clock = timeProvider ?? TimeProvider.System;
        TradeContext = tradeContext;
        History = historyReader is null ? null : new JournalHistoryViewModel(historyReader, OpenFromHistory, repository, dialogs);
        _selectedDate = TradingTimePolicy.ConvertUtcToTradingTime(
            _clock.GetUtcNow()).Date;
        SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        SaveDraftAndCloseCommand = new AsyncRelayCommand(SaveDraftAndCloseAsync, CanSave);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, CanDelete);
        CompleteReviewCommand = new AsyncRelayCommand(CompleteReviewAsync, CanSave);
        ReopenReviewCommand = new AsyncRelayCommand(ReopenReviewAsync, CanReopen);
        OpenEditorCommand = new RelayCommand(OpenEditor, CanOpenEditor);
        CloseEditorCommand = new RelayCommand(CloseEditor, () => IsEditorOpen && !IsSaving);
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
            if (value.Id == Guid.Empty || IsSaving || (!IsEditorOpen && !TryChangeScope()))
            {
                OnPropertyChanged();
                return;
            }
            _selectedAccount = value;
            OnPropertyChanged();
            if (IsEditorOpen)
            {
                // The page filter may change History, never the identity or fields of an open form.
                LoadTask = History?.SetScopeAsync(value.Id) ?? Task.CompletedTask;
                NotifyState();
            }
            else ScopeChanged();
        }
    }

    public JournalAccountOption EditorAccount
    {
        get => _editorAccount;
        set
        {
            if (_updatingAccounts || value is null || value.Id == _editorAccount.Id) return;
            if (!CanChooseEditorAccount || value.Id == Guid.Empty) { OnPropertyChanged(); return; }
            _editorAccount = value;
            if (_targetCollision) { _targetCollision = _reloadRequired = false; _errorMessage = null; }
            OnPropertyChanged();
            NotifyState();
            if (_loadTradeContext) _ = TradeContext.SetScopeAsync(SelectedTradingDate, value.Id);
        }
    }
    public bool CanChooseEditorAccount => IsEditorOpen && CanReadContent && !IsBusy;

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

    public string WentWell
    {
        get => _wentWell;
        set => SetAnswer(ref _wentWell, value);
    }

    public string NeedsImprovement
    {
        get => _needsImprovement;
        set => SetAnswer(ref _needsImprovement, value);
    }

    public string NextTradingDay
    {
        get => _nextTradingDay;
        set => SetAnswer(ref _nextTradingDay, value);
    }

    private void SetAnswer(ref string field, string value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (!CanEdit || value is null || !SetProperty(ref field, value, name)) return;
        _notice = null;
        NotifyState();
    }

    public bool HasDateInputError
    {
        get => _hasDateInputError;
        set { if (SetProperty(ref _hasDateInputError, value)) NotifyState(); }
    }
    public bool IsDirty => !string.Equals(_text, _savedText, StringComparison.Ordinal)
        || !string.Equals(_wentWell, _savedReview.WentWell, StringComparison.Ordinal)
        || !string.Equals(_needsImprovement, _savedReview.NeedsImprovement, StringComparison.Ordinal)
        || !string.Equals(_nextTradingDay, _savedReview.NextTradingDay, StringComparison.Ordinal)
        || EditorAccount.Id != _loadedAccount.Id;
    public bool IsExisting => _entry is not null;
    public Guid? JournalId => _entry?.Id;
    public bool IsDraft => _entry?.IsDraft ?? true;
    public bool IsCompleted => IsExisting && !IsDraft;
    public long? Revision => _entry?.Revision;
    public bool IsLoading => _isLoading;
    public bool IsSaving => _isSaving;
    public bool IsBusy => IsLoading || IsSaving;
    public bool CanChangeScope => !IsSaving;
    public bool CanReadContent => _active && _hasLoaded && SelectedDate.HasValue && !IsLoading;
    public bool IsEditorOpen => _isEditorOpen;
    public bool ShowCompactReview => CanReadContent && !IsEditorOpen;
    public bool ShowEmptyReview => ShowCompactReview && !IsExisting;
    public bool ShowContinueReview => IsExisting && IsDraft;
    public bool HasHistory => History is not null;
    public string EntryHeading => SelectedTradingDate?.ToString("yyyy-MM-dd") + " · " + (IsEditorOpen ? EditorAccount.Name : _loadedAccount.Name);
    public string SelectedDateHeading => SelectedTradingDate?.ToString("dd MMM yyyy", CultureInfo.CurrentCulture) ?? "Choose a date";
    public string EntryStateLabel => !IsExisting ? "No entry" : IsDraft ? "Draft" : "Completed";
    public string SavedText => _entry?.Text ?? "";
    public DailyReviewAnswers SavedReview => _entry?.Review ?? DailyReviewAnswers.Empty;
    public bool CanEdit => IsEditorOpen && CanReadContent && EditorAccount.IsAvailable && !IsBusy;
    public bool IsReadOnly => !CanEdit;
    public bool CanComplete => CanSave() && HasMeaningfulContent;
    public string CharacterCountText => $"{Text.Length:N0} / {DailyJournalEntry.MaximumTextLength:N0} characters";
    public string? ErrorMessage => HasDateInputError ? "Enter a valid journal date before saving."
        : Text.Length > DailyJournalEntry.MaximumTextLength
            ? "Journal text cannot exceed 100,000 UTF-16 code units. Your text has been kept; shorten it before saving."
            : ReviewLengthError ?? (_completionAttempted && !HasMeaningfulContent ? CompletionError : _errorMessage);
    public string ScopeMessage => IsEditorOpen
        ? "The Account inside this form determines where it is saved. All accounts is a separate journal. Dates use New York trading time. The top Account filter only filters Review History while the form is open. Moving an existing journal preserves its revisions and requires an empty target date/Account scope."
        : SelectedAccount.Id is null
        ? "This date's All accounts journal is separate from each account's journal. Review History includes every Account scope. Viewing history leaves this selection unchanged; Open in editor selects the review's original scope. Dates use New York trading time."
        : !SelectedAccount.IsAvailable
            ? "This account is unavailable. Its journal keeps its original account scope and cannot be saved."
            : $"Journal for {SelectedAccount.Name}. Dates use New York trading time.";
    public string EmptyReviewText => _loadedAccount.Id is null
        ? "No All accounts journal for this date. Account-specific entries may appear in Review History."
        : $"No journal for this date in {_loadedAccount.Name}.";
    public string StatusText => IsSaving
        ? _saveCancellation?.IsCancellationRequested == true ? "Cancelling operation…" : _isDeleting ? "Deleting journal…" : "Saving…"
        : IsLoading ? "Loading journal…"
        : !SelectedDate.HasValue ? "Choose a journal date."
        : _reloadRequired ? "Reload required before saving. Your text has been kept, along with your review answers."
        : IsDirty ? "Unsaved changes"
        : _notice ?? (!_hasLoaded ? "Select Reload to load this journal."
            : !IsExisting ? "New draft — not saved"
            : IsDraft ? $"Saved draft · Revision {Revision}" : $"Completed review · Revision {Revision}");

    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand SaveDraftAndCloseCommand { get; }
    public IAsyncRelayCommand DeleteCommand { get; }
    public IAsyncRelayCommand CompleteReviewCommand { get; }
    public IAsyncRelayCommand ReopenReviewCommand { get; }
    public IAsyncRelayCommand ReloadCommand { get; }
    public IRelayCommand CancelOperationCommand { get; }
    public IRelayCommand OpenEditorCommand { get; }
    public IRelayCommand CloseEditorCommand { get; }
    public Task LoadTask { get; private set; } = Task.CompletedTask;
    public JournalTradeContextViewModel TradeContext { get; }
    public JournalHistoryViewModel? History { get; }
    public event EventHandler? JournalDataCommitted;

    private bool OpenFromHistory(JournalHistoryItem item)
    {
        bool sameScope = SelectedTradingDate == item.TradingDate && _loadedAccount.Id == item.AccountId;
        if (!TryOpenScope(item.TradingDate, item.AccountId, item.AccountName ?? "Unavailable account")) return false;
        // History is already on this page, unlike Calendar's targeted reactivation.
        // Do not carry that reactivation flag into a later explicit discard/navigation.
        _preserveOnNextActivation = false;
        // A clean current editor may be older than the history metadata. Dirty or
        // conflicted contents instead keep the existing explicit Reload protection.
        if (sameScope && _active && !IsDirty && !_reloadRequired)
        {
            _isEditorOpen = false;
            LoadTask = LoadAsync(_loadedAccount);
        }
        return true;
    }

    private bool CanOpenEditor() => CanReadContent && _loadedAccount.IsAvailable && IsDraft && !IsBusy && !HasDateInputError;
    private void OpenEditor()
    {
        if (!CanOpenEditor()) return;
        _isEditorOpen = true;
        NotifyState();
    }
    private void CloseEditor()
    {
        if (!IsEditorOpen || IsSaving || !ConfirmDiscard()) return;
        PublishEntry(_entry); // Explicit discard restores the loaded revision, not another scope.
        _isEditorOpen = false;
        if (_loadTradeContext) _ = TradeContext.SetScopeAsync(SelectedTradingDate, _loadedAccount.Id);
        _notice = null;
        NotifyState();
    }

    /// <summary>Atomically opens an explicitly requested date and Account scope; null is the independent All accounts journal.</summary>
    public bool TryOpenScope(DateOnly date, Guid? accountId, string accountName)
    {
        if (IsSaving || accountId == Guid.Empty) return false;
        bool sameScope = SelectedTradingDate == date && (_hasLoaded ? _loadedAccount.Id : SelectedAccount.Id) == accountId;
        if (sameScope)
        {
            _resetOnNextActivation = false; // An explicit scope request takes precedence over default page entry.
            // Targeted Calendar navigation may revisit an inactive editor. Do not silently
            // reload its retained draft or clear a revision conflict (including a clean reopen conflict).
            _preserveOnNextActivation = _hasLoaded && (IsDirty || _reloadRequired);
            return true;
        }
        if (!TryChangeScope()) return false;
        _resetOnNextActivation = false;
        _preserveOnNextActivation = false;
        _selectedDate = date.ToDateTime(TimeOnly.MinValue);
        _selectedAccount = accountId.HasValue ? new(accountId, accountName) : AllAccounts;
        _hasDateInputError = false;
        OnPropertyChanged(nameof(SelectedDate));
        OnPropertyChanged(nameof(SelectedAccount));
        ScopeChanged();
        return true;
    }

    public Task ActivateAsync() => ActivateAsync(loadTradeContext: true);

    public Task ActivateAsync(bool loadTradeContext, bool newEntry = false, Guid? expectedJournalId = null)
    {
        _expectedJournalId = expectedJournalId;
        _loadTradeContext = loadTradeContext;
        if (_resetOnNextActivation)
        {
            // Set only after the shell's departure guard succeeds. Inline Calendar activation
            // never requests this reset, and a vetoed departure never deactivates the page.
            _resetOnNextActivation = _preserveOnNextActivation = false;
            _selectedDate = TradingTimePolicy.ConvertUtcToTradingTime(_clock.GetUtcNow()).Date;
            _selectedAccount = AllAccounts;
            _hasDateInputError = false;
            History?.Reset();
            OnPropertyChanged(nameof(SelectedDate));
            OnPropertyChanged(nameof(SelectedAccount));
            ScopeChanged();
        }
        Task history = History?.ActivateAsync(SelectedAccount.Id) ?? Task.CompletedTask;
        Task context = loadTradeContext ? TradeContext.ActivateAsync(SelectedTradingDate, SelectedAccount.Id) : Task.CompletedTask;
        bool preserve = _preserveOnNextActivation;
        _preserveOnNextActivation = false;
        // Repeated activation cannot discard work in an already-visible editor.
        if (preserve || (_active && (IsDirty || IsSaving || _reloadRequired)))
        {
            _active = true;
            NotifyState();
            return LoadTask = Task.WhenAll(LoadTask, context, history);
        }
        _active = true;
        _isEditorOpen = false;
        return LoadTask = Task.WhenAll(LoadAsync(newEntry: newEntry), context, history);
    }

    public bool TryLeave() => !IsSaving && ConfirmDiscard();

    public void Deactivate(bool resetOnNextActivation = false)
    {
        if (IsSaving) return;
        _resetOnNextActivation |= resetOnNextActivation;
        _active = false;
        CancelLoad();
        TradeContext.Deactivate();
        History?.Deactivate();
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
        _loadedAccount = _editorAccount = SelectedAccount;
        _targetCollision = false;
        _text = _savedText = "";
        _wentWell = _needsImprovement = _nextTradingDay = "";
        _savedReview = DailyReviewAnswers.Empty;
        _hasLoaded = _reloadRequired = false;
        _isEditorOpen = false;
        _completionAttempted = false;
        _errorMessage = _notice = null;
        NotifyContent();
        NotifyState();
        Task context = _loadTradeContext ? TradeContext.SetScopeAsync(SelectedTradingDate, SelectedAccount.Id) : Task.CompletedTask;
        Task history = History?.SetScopeAsync(SelectedAccount.Id) ?? Task.CompletedTask;
        LoadTask = _active ? Task.WhenAll(LoadAsync(), context, history) : context;
    }

    private DateOnly? SelectedTradingDate => SelectedDate is { } date ? DateOnly.FromDateTime(date) : null;

    private Task ReloadAsync()
    {
        if (!_active || IsBusy || !ConfirmDiscard()) return Task.CompletedTask;
        return LoadTask = LoadAsync(_targetCollision && _entry is null ? EditorAccount : _loadedAccount);
    }

    private async Task LoadAsync(JournalAccountOption? editorScope = null, bool newEntry = false)
    {
        CancelLoad();
        long generation = _generation;
        using var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        DateOnly? date = SelectedDate is { } selected ? DateOnly.FromDateTime(selected) : null;
        JournalAccountOption account = editorScope ?? SelectedAccount;
        _isLoading = true;
        _errorMessage = _notice = null;
        NotifyState();
        try
        {
            // SQLite's async APIs can execute synchronously; do that work off the dispatcher.
            var loaded = await Task.Run(async () =>
            {
                IReadOnlyList<AccountListItem> accounts = await _accountReader.GetAllAsync(cancellation.Token);
                DailyJournalDetails? journal = date.HasValue && !newEntry
                    ? await _repository.GetAsync(date.Value, account.Id, cancellation.Token) : null;
                return (accounts, journal);
            }, cancellation.Token);
            if (generation != _generation || cancellation.IsCancellationRequested || !_active) return;
            if (_expectedJournalId is { } expected && loaded.journal?.Entry.Id != expected)
            {
                _hasLoaded = false;
                _errorMessage = "This Journal was moved or deleted. Close it and refresh the day Journals before selecting an entry again.";
                return;
            }
            PublishAccounts(loaded.accounts, account, loaded.journal, preserveFilter: editorScope is not null);
            PublishEntry(loaded.journal?.Entry, AccountOption(account.Id));
            _hasLoaded = date.HasValue;
            _reloadRequired = false;
            if (!_loadedAccount.IsAvailable) _errorMessage = UnavailableAccountMessage;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception)
        {
            if (generation != _generation || !_active) return;
            _errorMessage = "Journal could not be loaded. Your text has been kept, along with your review answers. Select Reload to retry.";
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
        DailyJournalDetails? journal, bool preserveFilter = false)
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
            if (!preserveFilter) _selectedAccount = selected;
            else
            {
                var filter = options.FirstOrDefault(a => a.Id == SelectedAccount.Id);
                if (filter is null) { filter = SelectedAccount with { IsAvailable = false }; options.Add(filter); }
                _selectedAccount = filter;
            }
            _accounts = options;
            OnPropertyChanged(nameof(Accounts));
            OnPropertyChanged(nameof(SelectedAccount));
        }
        finally { _updatingAccounts = false; }
    }

    private bool CanSave() => CanEdit && !_reloadRequired && !HasDateInputError
        && Text.Length <= DailyJournalEntry.MaximumTextLength && ReviewLengthError is null;

    private bool CanReopen() => CanReadContent && IsCompleted && !IsEditorOpen && _loadedAccount.IsAvailable
        && !IsBusy && !_reloadRequired && !HasDateInputError;

    private bool HasMeaningfulContent => DailyJournalEntry.CanComplete(Text);
    public string? JournalTextValidation => _completionAttempted && !HasMeaningfulContent ? CompletionError : null;

    private string? ReviewLengthError
    {
        get
        {
            foreach (var answer in new[]
            {
                (WentWell, "What went well?"), (NeedsImprovement, "What needs improvement?"),
                (NextTradingDay, "What will I do differently next trading day?"),
            })
            {
                if (answer.Item1.Length > DailyReviewAnswers.MaximumAnswerLength)
                    return $"The answer to ‘{answer.Item2}’ cannot exceed 100,000 UTF-16 code units. Your answers have been kept; shorten it before saving.";
            }
            return null;
        }
    }

    private const string CompletionError = "Journal text is required. Include at least one letter or digit. Review answers are optional; your text has been kept.";

    private async Task CompleteReviewAsync()
    {
        if (!CanSave()) return;
        _completionAttempted = true;
        if (!HasMeaningfulContent) { NotifyState(); return; }
        await WriteAsync(isDraft: false);
    }

    private Task ReopenReviewAsync()
    {
        if (CanReopen())
        {
            // Editing is local until Save or a genuinely changed Cancel. Preserve the
            // loaded Completed snapshot and token, including change-then-revert edits.
            _isEditorOpen = true;
            NotifyState();
        }
        return Task.CompletedTask;
    }

    private Task SaveAsync() => CompleteReviewAsync();

    private Task SaveDraftAndCloseAsync()
    {
        if (!CanSave()) return Task.CompletedTask;
        if (IsCompleted && !IsDirty)
        {
            _isEditorOpen = false;
            _completionAttempted = false;
            NotifyState();
            return Task.CompletedTask;
        }
        // Only a never-saved, exactly empty form closes without a write. Drafts preserve
        // partial/nonmeaningful text too; this button never invokes discard protection.
        if (_entry is null && Text.Length == 0 && WentWell.Length == 0
            && NeedsImprovement.Length == 0 && NextTradingDay.Length == 0)
        {
            PublishEntry(null, EditorAccount);
            _isEditorOpen = false;
            NotifyState();
            return Task.CompletedTask;
        }
        return WriteAsync(isDraft: true);
    }

    private async Task WriteAsync(bool isDraft, bool openEditorOnSuccess = false)
    {
        // All write actions share this guard so concurrent command types cannot overlap.
        if (IsSaving || !CanSave()) return;
        using var cancellation = new CancellationTokenSource();
        _saveCancellation = cancellation;
        _isSaving = true;
        _completionAttempted = false;
        _targetCollision = false; // A later revision conflict must not be cleared by changing the target.
        _errorMessage = _notice = null;
        string text = Text;
        var review = new DailyReviewAnswers(WentWell, NeedsImprovement, NextTradingDay);
        DailyJournalEntry? entry = _entry;
        DateOnly date = DateOnly.FromDateTime(SelectedDate!.Value);
        Guid? accountId = EditorAccount.Id;
        bool committed = false;
        NotifyState();
        try
        {
            DailyJournalWriteResult result = await Task.Run(() => entry is null
                ? _repository.CreateAsync(new(date, accountId, text, isDraft, review), cancellation.Token)
                : _repository.UpdateAsync(new(entry.Id, entry.Revision, text, isDraft, review, ReopenCompleted: !entry.IsDraft,
                    TargetScope: new DailyJournalAccountScope(accountId)), cancellation.Token), cancellation.Token);
            // A repository can return a committed result after cancellation was requested.
            // Its authoritative result must still be accepted; cancellation cannot undo a commit.
            switch (result.Status)
            {
                case DailyJournalWriteStatus.Created:
                case DailyJournalWriteStatus.Updated:
                case DailyJournalWriteStatus.Unchanged:
                    if (result.Journal is null) throw new InvalidOperationException("A saved journal result is required.");
                    PublishEntry(result.Journal.Entry);
                    _isEditorOpen = openEditorOnSuccess;
                    _reloadRequired = false;
                    _notice = result.Status == DailyJournalWriteStatus.Unchanged ? "No changes to save." : null;
                    committed = result.Status is DailyJournalWriteStatus.Created or DailyJournalWriteStatus.Updated;
                    break;
                case DailyJournalWriteStatus.AlreadyExists:
                    _reloadRequired = true;
                    _targetCollision = true;
                    _errorMessage = "A journal was created for this date and account elsewhere. Your text has been kept, along with your review answers. Reload to read it before saving.";
                    break;
                case DailyJournalWriteStatus.AccountScopeOccupied:
                    _targetCollision = true;
                    _errorMessage = "A journal already exists for this date and target Account. Nothing was moved or merged. Your fields are kept; choose another Account or close this edit before opening the existing journal.";
                    break;
                case DailyJournalWriteStatus.Conflict:
                    _reloadRequired = true;
                    _errorMessage = "This journal was changed elsewhere. Your text has been kept, along with your review answers. Reload the latest revision before saving.";
                    break;
                case DailyJournalWriteStatus.NotFound:
                    _reloadRequired = true;
                    _errorMessage = "This journal no longer exists. Your text has been kept, along with your review answers. Reload before saving.";
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
            _errorMessage = "Save cancelled. Your text has been kept, along with your review answers.";
        }
        catch (Exception)
        {
            _errorMessage = "Journal could not be saved. Your text has been kept, along with your review answers. Try the action again.";
        }
        finally
        {
            _saveCancellation = null;
            _isSaving = false;
            NotifyState();
        }
        // Notify only after the authoritative result and editor state are published. A late
        // cancellation cannot retract a commit, and UI subscribers are not part of the transaction.
        if (committed)
        {
            JournalDataCommitted?.Invoke(this, EventArgs.Empty);
            if (History is not null) await History.RefreshAsync();
        }
    }

    private bool CanDelete() => CanReadContent && IsExisting && !IsBusy && !_reloadRequired && !HasDateInputError;

    private async Task DeleteAsync()
    {
        if (!CanDelete() || _entry is not { } entry) return;
        if (!_dialogs.Confirm(new ConfirmationDialogRequest("Delete Journal permanently?",
            $"Delete the journal for New York date {entry.TradingDate:yyyy-MM-dd}, Account scope: {_loadedAccount.Name}? " +
            "The entry and ALL revision history will be permanently deleted and cannot be recovered. " +
            "Any unsaved text and answers in this editor will also be discarded. A new entry may then be created for this same date and scope.",
            "Delete Journal", "Keep Journal", isDestructive: true))) return;

        using var cancellation = new CancellationTokenSource();
        _saveCancellation = cancellation;
        _isSaving = _isDeleting = true;
        _errorMessage = _notice = null;
        bool committed = false;
        NotifyState();
        try
        {
            var result = await Task.Run(() => _repository.DeleteAsync(new(entry.Id, entry.Revision), cancellation.Token), cancellation.Token);
            switch (result.Status)
            {
                case DailyJournalWriteStatus.Deleted:
                    PublishEntry(null);
                    _isEditorOpen = false;
                    _notice = "Journal and revision history permanently deleted.";
                    History?.CloseReviewCommand.Execute(null);
                    committed = true;
                    break;
                case DailyJournalWriteStatus.Conflict:
                    _reloadRequired = true;
                    _errorMessage = "This journal has a newer revision. Nothing was deleted. Your edits are kept; Reload before deciding whether to delete it.";
                    break;
                case DailyJournalWriteStatus.NotFound:
                    _reloadRequired = true;
                    _errorMessage = "This journal no longer exists. Your edits are kept. Reload to refresh this scope.";
                    break;
                default: throw new InvalidOperationException("Unexpected journal deletion result.");
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        { _errorMessage = "Deletion cancelled. Your journal text and answers have been kept."; }
        catch (Exception)
        { _errorMessage = "Journal could not be deleted. Your text and answers have been kept. Try again."; }
        finally
        {
            _saveCancellation = null;
            _isSaving = _isDeleting = false;
            NotifyState();
        }
        if (committed)
        {
            JournalDataCommitted?.Invoke(this, EventArgs.Empty);
            if (History is not null) await History.RefreshAsync();
        }
    }

    private JournalAccountOption AccountOption(Guid? id) => Accounts.FirstOrDefault(a => a.Id == id)
        ?? new(id, "Account unavailable", false);

    private void PublishEntry(DailyJournalEntry? entry, JournalAccountOption? emptyScope = null)
    {
        _entry = entry;
        _editorAccount = _loadedAccount = entry is not null ? AccountOption(entry.TradingAccountId) : emptyScope ?? _loadedAccount;
        _targetCollision = false;
        // A fresh Completed snapshot requires a fresh explicit Reopen, including Reload
        // after a conflict. An earlier editor session cannot authorize editing this revision.
        if (entry is { IsDraft: false }) _isEditorOpen = false;
        _text = _savedText = entry?.Text ?? "";
        _savedReview = entry?.Review ?? DailyReviewAnswers.Empty;
        _wentWell = _savedReview.WentWell;
        _needsImprovement = _savedReview.NeedsImprovement;
        _nextTradingDay = _savedReview.NextTradingDay;
        _completionAttempted = false;
        NotifyContent();
    }

    private void NotifyContent()
    {
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(WentWell));
        OnPropertyChanged(nameof(NeedsImprovement));
        OnPropertyChanged(nameof(NextTradingDay));
        OnPropertyChanged(nameof(SavedText));
        OnPropertyChanged(nameof(SavedReview));
        OnPropertyChanged(nameof(EditorAccount));
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
        "The selected account is no longer available. Your text, review answers, and account scope have been kept. Select Reload to check again, or choose another scope.";

    private void NotifyState()
    {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsEditorOpen));
        OnPropertyChanged(nameof(ShowCompactReview));
        OnPropertyChanged(nameof(ShowEmptyReview));
        OnPropertyChanged(nameof(ShowContinueReview));
        OnPropertyChanged(nameof(EntryHeading));
        OnPropertyChanged(nameof(SelectedDateHeading));
        OnPropertyChanged(nameof(EntryStateLabel));
        OnPropertyChanged(nameof(IsExisting));
        OnPropertyChanged(nameof(IsDraft));
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(Revision));
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(IsSaving));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanChangeScope));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanChooseEditorAccount));
        OnPropertyChanged(nameof(CanReadContent));
        OnPropertyChanged(nameof(IsReadOnly));
        OnPropertyChanged(nameof(CanComplete));
        OnPropertyChanged(nameof(CharacterCountText));
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(JournalTextValidation));
        OnPropertyChanged(nameof(ScopeMessage));
        OnPropertyChanged(nameof(EmptyReviewText));
        OnPropertyChanged(nameof(StatusText));
        SaveCommand.NotifyCanExecuteChanged();
        SaveDraftAndCloseCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        CompleteReviewCommand.NotifyCanExecuteChanged();
        ReopenReviewCommand.NotifyCanExecuteChanged();
        ReloadCommand.NotifyCanExecuteChanged();
        CancelOperationCommand.NotifyCanExecuteChanged();
        OpenEditorCommand.NotifyCanExecuteChanged();
        CloseEditorCommand.NotifyCanExecuteChanged();
    }
}
