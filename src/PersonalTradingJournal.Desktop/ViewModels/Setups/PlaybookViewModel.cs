using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Desktop.ViewModels.Setups;

public sealed class PlaybookField(string label) : ObservableObject
{
    private string _text = "";
    public string Label { get; } = label;
    public string Text { get => _text; set => SetProperty(ref _text, value); }
}

/// <summary>Authoring host for the existing Setup catalog; never classifies a Trade.</summary>
public sealed class PlaybookViewModel : ObservableObject
{
    private readonly ITradingSetupReader _setups;
    private readonly ITradingSetupPlaybookStore _store;
    private readonly ITradingSetupPlaybookHistoryReader _history;
    private readonly SaveTradingSetupPlaybookUseCase _save;
    private readonly TradingSetupLifecycleUseCase _lifecycle;
    private readonly IDialogService _dialogs;
    private CancellationTokenSource? _read;
    private long _generation;
    private TradingSetupListItem? _selected;
    private TradingSetupPlaybookRevision? _current, _historical;
    private IReadOnlyList<TradingSetupListItem> _catalog = [];
    private IReadOnlyList<TradingSetupPlaybookRevision> _revisions = [];
    private string[] _baseline = [];
    private readonly List<int?> _cursors = [null];
    private int _page;
    private bool _loading, _writing, _editing, _ready, _hasNext;
    private string _message = "Select a Trading Setup. Create Setups in Trading Setups; this page uses the same catalog.";

    public PlaybookViewModel(ITradingSetupReader setups, ITradingSetupPlaybookStore store,
        ITradingSetupPlaybookHistoryReader history, SaveTradingSetupPlaybookUseCase save,
        TradingSetupLifecycleUseCase lifecycle, IDialogService dialogs)
    {
        _setups = setups; _store = store; _history = history; _save = save; _lifecycle = lifecycle; _dialogs = dialogs;
        Fields = new[] { "Market context", "Entry conditions", "Confirmations", "Invalidation", "Execution guidance" }
            .Select(label => new PlaybookField(label)).ToArray();
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsWriting);
        EditCommand = new RelayCommand(BeginEdit, () => _ready && !IsBusy && !IsEditing && SelectedSetup?.IsActive == true && !IsHistorical);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => IsEditing && !IsBusy);
        CancelEditCommand = new RelayCommand(() => { if (TryLeave()) { IsEditing = false; Message = "Editing closed. No revision was saved."; } }, () => IsEditing && !IsWriting);
        CancelOperationCommand = new RelayCommand(() => { _read?.Cancel(); SaveCommand.Cancel(); ToggleArchiveCommand?.Cancel(); }, () => IsBusy);
        ToggleArchiveCommand = new AsyncRelayCommand(ToggleArchiveAsync, () => SelectedSetup is not null && _ready && !IsBusy && !IsEditing);
        ViewRevisionCommand = new RelayCommand<TradingSetupPlaybookRevision>(ShowRevision, r => r is not null && !IsBusy);
        ShowCurrentCommand = new RelayCommand(() => { _historical = null; Changed(); }, () => IsHistorical && !IsBusy);
        NextPageCommand = new AsyncRelayCommand(() => ChangePageAsync(1), () => _hasNext && !IsBusy);
        PreviousPageCommand = new AsyncRelayCommand(() => ChangePageAsync(-1), () => _page > 0 && !IsBusy);
    }

    public IReadOnlyList<PlaybookField> Fields { get; }
    public IReadOnlyList<TradingSetupListItem> Setups { get => _catalog; private set => SetProperty(ref _catalog, value); }
    public IReadOnlyList<TradingSetupPlaybookRevision> Revisions { get => _revisions; private set => SetProperty(ref _revisions, value); }
    public TradingSetupListItem? SelectedSetup => _selected;
    public TradingSetupPlaybookRevision? DisplayedRevision => _historical ?? _current;
    public bool IsHistorical => _historical is not null;
    public bool HasGuidance => DisplayedRevision is not null;
    public bool ShowReading => !IsEditing && HasGuidance;
    public bool IsEditing { get => _editing; private set { SetProperty(ref _editing, value); Changed(); } }
    public bool IsLoading { get => _loading; private set { SetProperty(ref _loading, value); Changed(); } }
    public bool IsWriting { get => _writing; private set { SetProperty(ref _writing, value); Changed(); } }
    public bool IsBusy => IsLoading || IsWriting;
    public bool CanSelectSetup => !IsBusy;
    public bool CanEditFields => !IsWriting;
    public bool HasUnsavedChanges => IsEditing && !Fields.Select(f => f.Text).SequenceEqual(_baseline);
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public string SetupState => SelectedSetup is null ? "No Setup selected" : SelectedSetup.IsActive ? "Active Setup" : "Archived Setup — reopen to edit guidance";
    public string ArchiveLabel => SelectedSetup?.IsActive == true ? "Archive Setup" : "Reopen Setup";
    public string EditLabel => _current is null ? "Create guidance" : "Edit guidance";
    public string VersionLabel => IsHistorical ? $"Historical revision {DisplayedRevision!.Revision} · read-only" :
        _current is null ? "No Playbook guidance yet" : $"Current revision {_current.Revision}";
    public string PageLabel => $"Revision page {_page + 1}";
    public Task LoadTask { get; private set; } = Task.CompletedTask;
    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand EditCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IRelayCommand CancelEditCommand { get; }
    public IRelayCommand CancelOperationCommand { get; }
    public IAsyncRelayCommand ToggleArchiveCommand { get; }
    public IRelayCommand<TradingSetupPlaybookRevision> ViewRevisionCommand { get; }
    public IRelayCommand ShowCurrentCommand { get; }
    public IAsyncRelayCommand NextPageCommand { get; }
    public IAsyncRelayCommand PreviousPageCommand { get; }

    public Task ActivateAsync() => LoadTask = LoadAsync(refreshCatalog: true);

    // The host restores the ComboBox's selection if this guard vetoes the change.
    public bool TrySelectSetup(TradingSetupListItem? setup)
    {
        if (setup?.Id == _selected?.Id) return true;
        if (!TryLeave()) return false;
        IsEditing = false;
        _selected = setup;
        ResetHistory();
        LoadTask = LoadAsync(refreshCatalog: false);
        return true;
    }

    public bool TryLeave()
    {
        if (IsWriting) { Message = "Wait for the Playbook operation to finish, or cancel it before leaving."; return false; }
        return !HasUnsavedChanges || _dialogs.Confirm(new ConfirmationDialogRequest(
            "Discard Playbook changes?", "The entered guidance has not been saved. Discard it and continue?", "Discard changes", isDestructive: true));
    }

    public void Deactivate()
    {
        ++_generation; _read?.Cancel(); _read = null; IsLoading = false; IsEditing = false;
    }

    private async Task RefreshAsync()
    {
        if (!TryLeave()) return;
        IsEditing = false;
        ResetHistory();
        await (LoadTask = LoadAsync(refreshCatalog: true));
    }

    private void ResetHistory() { _cursors.Clear(); _cursors.Add(null); _page = 0; _historical = null; }

    private async Task LoadAsync(bool refreshCatalog)
    {
        _read?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _read = cancellation;
        long generation = ++_generation;
        var token = cancellation.Token;
        _ready = false; _current = null; _historical = null; Revisions = []; _hasNext = false;
        IsLoading = true; Message = "Loading Playbook…";
        try
        {
            if (refreshCatalog)
            {
                var catalog = await _setups.GetAllAsync(token);
                token.ThrowIfCancellationRequested();
                if (generation != _generation) return;
                Setups = catalog;
                _selected = catalog.FirstOrDefault(s => s.Id == _selected?.Id);
            }
            if (_selected is null) { Message = "Select a Trading Setup. Create Setups in Trading Setups; this page uses the same catalog."; return; }
            Guid setupId = _selected.Id;
            int? cursor = _cursors[_page];
            var current = await _store.GetLatestAsync(setupId, token);
            token.ThrowIfCancellationRequested();
            if (generation != _generation) return;
            var rows = await _history.GetHistoryAsync(setupId, cursor, 11, token);
            token.ThrowIfCancellationRequested();
            if (generation != _generation) return;
            _current = current; Revisions = rows.Take(10).ToArray(); _hasNext = rows.Count > 10; _ready = true;
            Message = current is null ? "This Setup remains usable without guidance. Create its first Playbook revision when ready." : "Guidance is user-authored. A link or snapshot is not proof that a plan was followed.";
        }
        catch (OperationCanceledException) { if (generation == _generation) Message = "Loading cancelled. Refresh to try again."; }
        catch (Exception) { if (generation == _generation) Message = "Playbook could not be loaded. Refresh to try again."; }
        finally
        {
            if (generation == _generation) { _read = null; IsLoading = false; Changed(); }
        }
    }

    private void BeginEdit()
    {
        var g = _current?.Guidance;
        string[] values = g is null ? ["", "", "", "", ""] : [g.MarketContext, g.EntryConditions, g.Confirmations, g.Invalidation, g.ExecutionGuidance];
        for (int i = 0; i < Fields.Count; i++) Fields[i].Text = values[i];
        _baseline = values; IsEditing = true;
        Message = "All five sections are required (up to 4,000 characters each). Existing example metadata is retained.";
    }

    private async Task SaveAsync(CancellationToken token)
    {
        // Snapshot the five fields and owning identity before the first await; editing is disabled during the write.
        var invalid = Fields.FirstOrDefault(f => string.IsNullOrWhiteSpace(f.Text) || f.Text.Trim().Length > PlaybookGuidance.MaximumFieldLength);
        if (invalid is not null) { Message = $"{invalid.Label}: enter meaningful text, up to 4,000 characters. Your text has been kept."; return; }
        IsWriting = true; Message = "Saving a new immutable revision…";
        try
        {
            var guidance = new PlaybookGuidance(Fields[0].Text, Fields[1].Text, Fields[2].Text, Fields[3].Text, Fields[4].Text, _current?.Guidance.Examples);
            var saved = await _save.ExecuteAsync(new(_selected!.Id, _current?.Revision ?? 0, guidance), token);
            _current = saved; IsEditing = false; ResetHistory();
            // A committed save is successful even if a later refresh fails.
            await LoadAsync(refreshCatalog: true);
            if (_ready) Message = $"Revision {saved.Revision} saved.";
            else
            {
                _current = saved; Changed();
                Message = $"Revision {saved.Revision} saved, but the refreshed list could not be loaded. Saved guidance is shown; Refresh before editing again.";
            }
        }
        catch (OperationCanceledException) { Message = "Save cancelled. Your entered text is retained."; }
        catch (ArgumentException) { Message = "Check all five sections: meaningful text, at most 4,000 characters each. Your text is retained."; }
        catch (InvalidOperationException) { Message = "The Setup or Playbook changed or was archived. Your text is retained. Copy it if needed, then Refresh to load the latest revision."; }
        catch (KeyNotFoundException) { Message = "The Setup is unavailable. Your text is retained; copy it before refreshing."; }
        catch (Exception) { Message = "Playbook could not be saved. Your text is retained. Try Save again or Refresh."; }
        finally { IsWriting = false; }
    }

    private async Task ToggleArchiveAsync(CancellationToken token)
    {
        if (!_dialogs.Confirm(new ConfirmationDialogRequest(ArchiveLabel + "?",
            $"{ArchiveLabel} “{_selected!.Name}”? Existing classifications, revisions and frozen Trade context are retained.", ArchiveLabel))) return;
        IsWriting = true;
        try
        {
            await _lifecycle.ExecuteAsync(new(_selected.Id, !_selected.IsActive), token);
            ResetHistory(); await LoadAsync(refreshCatalog: true);
        }
        catch (OperationCanceledException) { Message = "Setup operation cancelled. Refresh to check its current state."; }
        catch (Exception) { Message = "Setup state could not be changed. Refresh to check its current state."; }
        finally { IsWriting = false; }
    }

    private void ShowRevision(TradingSetupPlaybookRevision? revision)
    {
        if (revision is null || revision.TradingSetupId != _selected?.Id || !Revisions.Contains(revision) || !TryLeave()) return;
        IsEditing = false; _historical = revision; Changed();
    }

    private async Task ChangePageAsync(int delta)
    {
        if (!TryLeave()) return;
        IsEditing = false;
        if (delta > 0 && _cursors.Count == _page + 1) _cursors.Add(Revisions[^1].Revision);
        _page += delta;
        await (LoadTask = LoadAsync(refreshCatalog: false));
    }

    private void Changed()
    {
        foreach (string name in new[] { nameof(SelectedSetup), nameof(DisplayedRevision), nameof(IsHistorical), nameof(HasGuidance), nameof(ShowReading), nameof(IsBusy), nameof(CanSelectSetup), nameof(CanEditFields), nameof(SetupState), nameof(ArchiveLabel), nameof(EditLabel), nameof(VersionLabel), nameof(PageLabel) }) OnPropertyChanged(name);
        RefreshCommand?.NotifyCanExecuteChanged(); EditCommand?.NotifyCanExecuteChanged(); SaveCommand?.NotifyCanExecuteChanged();
        CancelEditCommand?.NotifyCanExecuteChanged(); CancelOperationCommand?.NotifyCanExecuteChanged(); ToggleArchiveCommand?.NotifyCanExecuteChanged();
        ViewRevisionCommand?.NotifyCanExecuteChanged(); ShowCurrentCommand?.NotifyCanExecuteChanged(); NextPageCommand?.NotifyCanExecuteChanged(); PreviousPageCommand?.NotifyCanExecuteChanged();
    }
}
