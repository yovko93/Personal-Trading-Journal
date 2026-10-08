using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Formatting;
using PersonalTradingJournal.Desktop.Dialogs;

namespace PersonalTradingJournal.Desktop.ViewModels.Journals;

public sealed class JournalHistoryRow(JournalHistoryItem item) : ObservableObject
{
    public JournalHistoryItem Item { get; } = item;
    private JournalHistoryViewModel? _review;
    public JournalHistoryViewModel? Review
    {
        get => _review;
        internal set
        {
            if (!SetProperty(ref _review, value)) return;
            OnPropertyChanged(nameof(IsExpanded));
            OnPropertyChanged(nameof(ActionLabel));
            OnPropertyChanged(nameof(OpenAccessibleName));
        }
    }
    public bool IsExpanded => Review is not null;
    public string ActionLabel => IsExpanded ? "Close review" : "Open review";
    public string DateText => Item.TradingDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public string DisplayDate => Item.TradingDate.ToString("dd MMM yyyy", CultureInfo.CurrentCulture);
    public string RevisionText => $"Revision {Item.Revision}";
    public string StateText => Item.IsDraft ? "Draft" : "Completed";
    public string ScopeText => Item.AccountState switch
    {
        DailyJournalAccountState.AllAccounts => "All accounts",
        DailyJournalAccountState.Inactive => $"{Item.AccountName} (inactive)",
        DailyJournalAccountState.Unavailable => "Account unavailable — scope retained",
        _ => Item.AccountName ?? "Account unavailable",
    };
    public string Description => $"{DateText} · {ScopeText} · {StateText} · Revision {Item.Revision}";
    public string OpenAccessibleName => ActionLabel + ": " + Description;
}

public sealed class JournalRevisionRow(JournalRevisionItem item, bool isCurrent = false) : ObservableObject
{
    public JournalRevisionItem Item { get; } = item;
    public bool IsCurrent { get; } = isCurrent;
    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        internal set
        {
            if (!SetProperty(ref _isExpanded, value)) return;
            OnPropertyChanged(nameof(ActionLabel));
            OnPropertyChanged(nameof(ViewAccessibleName));
        }
    }
    public string ActionLabel => IsExpanded ? "Close revision" : "View revision";
    public string StateText => $"Revision {Item.Revision} · {(Item.IsDraft ? "Draft" : "Completed")}";
    public string SavedAtText => TradingTimestampFormatter.FormatNewYork(Item.SavedAtUtc, "G", CultureInfo.CurrentCulture) + " · New York";
    public string Description => StateText + " · " + SavedAtText;
    public string ViewAccessibleName => ActionLabel + " (read-only): " + Description;
    public string DeleteAccessibleName => "Delete revision: " + Description;
    public string DeleteHelp => IsCurrent ? "Current revision is protected. Older revisions can be deleted without changing this journal."
        : "Permanently delete only this revision. Current journal content and its other revisions are retained.";
}

public sealed record JournalFieldPreview(string Label, string Text)
{
    // Bounded plain-text excerpt; the view additionally limits wrapped text to three lines.
    public static string Excerpt(string text)
    {
        string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] lines = normalized.Split('\n', 4);
        string excerpt = string.Join("\n", lines.Take(3));
        bool clipped = lines.Length > 3 || excerpt.Length > 240;
        if (excerpt.Length > 240)
        {
            int length = char.IsHighSurrogate(excerpt[239]) ? 239 : 240;
            excerpt = excerpt[..length];
        }
        return excerpt + (clipped ? "…" : "");
    }
}

/// <summary>Scoped history browsing with explicit older-snapshot deletion; never replaces editor contents.</summary>
public sealed class JournalHistoryViewModel : ObservableObject
{
    private readonly IDailyJournalHistoryReader _reader;
    private readonly Func<JournalHistoryItem, bool> _open;
    private readonly IDailyJournalRepository? _repository;
    private readonly IDailyJournalRevisionWriter? _revisionWriter;
    private readonly IDialogService? _dialogs;
    private DailyJournalDetails? _preview;
    private bool _previewLoading, _deleting, _deleteNeedsRefresh;
    private string? _previewError, _deleteMessage;
    private CancellationTokenSource? _previewCancellation, _deleteCancellation;
    private long _previewGeneration;
    private long? _viewingRevision;
    private Guid? _accountId;
    private bool _active, _loading, _revisionLoading, _snapshotLoading;
    private int _page = 1, _total, _revisionPage = 1, _revisionTotal;
    private CancellationTokenSource? _listCancellation, _revisionCancellation, _snapshotCancellation;
    private long _listGeneration, _revisionGeneration, _snapshotGeneration;
    private IReadOnlyList<JournalHistoryRow> _entries = [];
    private IReadOnlyList<JournalRevisionRow> _revisions = [];
    private JournalHistoryRow? _selectedEntry;
    private DailyJournalRevision? _snapshot;
    private string? _error, _revisionError, _snapshotError;

    public JournalHistoryViewModel(IDailyJournalHistoryReader reader, Func<JournalHistoryItem, bool> open,
        IDailyJournalRepository? repository = null, IDialogService? dialogs = null)
    {
        _reader = reader;
        _open = open;
        _repository = repository;
        _revisionWriter = repository as IDailyJournalRevisionWriter;
        _dialogs = dialogs;
        RefreshCommand = new AsyncRelayCommand(() => RefreshRequested?.Invoke() ?? RefreshAsync(), () => _active,
            AsyncRelayCommandOptions.AllowConcurrentExecutions);
        PreviousCommand = new AsyncRelayCommand(() => ChangePageAsync(-1), () => _active && !IsLoading && _page > 1);
        NextCommand = new AsyncRelayCommand(() => ChangePageAsync(1), () => _active && !IsLoading && (long)_page * PageSize < _total);
        OpenCommand = new AsyncRelayCommand<JournalHistoryRow>(OpenAsync, row => _active && row is not null,
            AsyncRelayCommandOptions.AllowConcurrentExecutions);
        OpenInEditorCommand = new RelayCommand(() =>
        {
            if (_selectedEntry is { } row && _entries.Any(r => ReferenceEquals(r, row))) _open(row.Item);
        }, () => _active && HasSelectedEntry);
        PreviousRevisionsCommand = new AsyncRelayCommand(() => ChangeRevisionPageAsync(-1),
            () => _active && !_previewLoading && !_deleting && !IsRevisionLoading && _revisionPage > 1);
        NextRevisionsCommand = new AsyncRelayCommand(() => ChangeRevisionPageAsync(1),
            () => _active && !_previewLoading && !_deleting && !IsRevisionLoading && (long)_revisionPage * RevisionPageSize < _revisionTotal);
        ViewRevisionCommand = new AsyncRelayCommand<JournalRevisionRow>(ViewRevisionAsync,
            row => _active && row is not null, AsyncRelayCommandOptions.AllowConcurrentExecutions);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
        CloseViewCommand = new RelayCommand(() => { ClearSnapshot(); Notify(); }, () => _active && HasRevisionView);
        CloseReviewCommand = new RelayCommand(() => { ClearSelection(); Notify(); }, () => _active && HasSelectedEntry);
        DeleteRevisionCommand = new AsyncRelayCommand<JournalRevisionRow>(DeleteRevisionAsync, CanDeleteRevision);
    }

    public const int PageSize = 10;
    public const int RevisionPageSize = 20;
    public IReadOnlyList<JournalHistoryRow> Entries => _entries;
    public IReadOnlyList<JournalRevisionRow> Revisions => _revisions;
    public JournalHistoryRow? SelectedEntry => _selectedEntry;
    public bool HasSelectedEntry => _selectedEntry is not null;
    public DailyJournalRevision? Snapshot => _snapshot;
    public bool HasSnapshot => _snapshot is not null;
    public bool HasRevisionView => HasSnapshot || _snapshotLoading || _snapshotError is not null;
    public string SnapshotDescription => _snapshot is null ? "" : new JournalRevisionRow(new(
        _snapshot.JournalId, _snapshot.Revision, _snapshot.IsDraft, _snapshot.SavedAtUtc)).Description + " · Read-only";
    public bool IsLoading => _loading;
    public bool IsRevisionLoading => _revisionLoading;
    public bool IsBusy => _loading || _revisionLoading || _snapshotLoading || _previewLoading || _deleting;
    public IReadOnlyList<JournalFieldPreview> Previews => _preview is null ? [] : new[]
    {
        new JournalFieldPreview("Journal text", _preview.Entry.Text),
        new JournalFieldPreview("What went well?", _preview.Entry.Review.WentWell),
        new JournalFieldPreview("What needs improvement?", _preview.Entry.Review.NeedsImprovement),
        new JournalFieldPreview("What will I do differently next trading day?", _preview.Entry.Review.NextTradingDay)
    }.Where(p => !string.IsNullOrWhiteSpace(p.Text)).Select(p => p with { Text = JournalFieldPreview.Excerpt(p.Text) }).ToArray();
    public string? PreviewStatus => _previewLoading ? "Loading current review…" : _previewError
        ?? (_preview is null ? null : $"Current content · Revision {_preview.Entry.Revision}");
    public string? DeleteStatus => _deleting ? "Deleting revision…" : _deleteMessage;
    public string PageText => $"Page {_page} · {_total} reviews";
    public string Heading => $"Review History · {_total} reviews";
    public string RevisionPageText => $"Page {_revisionPage} · {_revisionTotal} revisions";
    public string StatusText => IsLoading ? "Loading reviews…" : _error ?? (_entries.Count == 0
        ? _accountId is null ? "No saved reviews in any Account scope." : "No saved reviews in this Account scope."
        : _accountId is null ? "All account scopes. Open a review in its original Account scope." : "Open a review or browse revisions.");
    public string RevisionStatusText => IsRevisionLoading ? "Loading revisions…" : _revisionError ?? (_selectedEntry is null
        ? "" : _revisions.Count == 0 ? "No revisions available." : "Viewing history does not save or restore a revision.");
    public string? SnapshotStatusText => _snapshotLoading ? "Loading revision…" : _snapshotError;
    public Task LoadTask { get; private set; } = Task.CompletedTask;
    public Task RevisionLoadTask { get; private set; } = Task.CompletedTask;
    public Task SnapshotLoadTask { get; private set; } = Task.CompletedTask;
    public Task PreviewLoadTask { get; private set; } = Task.CompletedTask;
    public IAsyncRelayCommand RefreshCommand { get; }
    public Func<Task>? RefreshRequested { get; set; }
    public IAsyncRelayCommand PreviousCommand { get; }
    public IAsyncRelayCommand NextCommand { get; }
    public IAsyncRelayCommand<JournalHistoryRow> OpenCommand { get; }
    public IRelayCommand OpenInEditorCommand { get; }
    public IAsyncRelayCommand PreviousRevisionsCommand { get; }
    public IAsyncRelayCommand NextRevisionsCommand { get; }
    public IAsyncRelayCommand<JournalRevisionRow> ViewRevisionCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand CloseViewCommand { get; }
    public IRelayCommand CloseReviewCommand { get; }
    public IAsyncRelayCommand<JournalRevisionRow> DeleteRevisionCommand { get; }

    public Task ActivateAsync(Guid? accountId)
    {
        _active = true;
        return SetScopeAsync(accountId, reload: true);
    }

    public Task SetScopeAsync(Guid? accountId, bool reload = false)
    {
        if (accountId == Guid.Empty) throw new ArgumentException("An explicit Account scope is required.", nameof(accountId));
        if (_accountId == accountId && !reload) return LoadTask;
        if (_accountId != accountId)
        {
            CancelList();
            ClearSelection();
            _accountId = accountId;
            _page = 1;
            _total = 0;
            _entries = [];
        }
        Notify();
        return _active ? LoadTask = LoadAsync() : Task.CompletedTask;
    }

    public void Deactivate()
    {
        _active = false;
        CancelList();
        CancelRevisions();
        CancelSnapshot();
        CancelPreview();
        _deleteCancellation?.Cancel();
        Notify();
    }

    /// <summary>Clears page and selection state before a fresh standalone Journal visit.</summary>
    public void Reset()
    {
        Deactivate();
        ClearSelection();
        _page = 1;
        _total = 0;
        _entries = [];
        _error = null;
        Notify();
    }

    public async Task RefreshAsync()
    {
        if (!_active || _deleting) return;
        await (LoadTask = LoadAsync());
        if (_active && _selectedEntry is not null)
        {
            var selected = _selectedEntry;
            long? revision = _viewingRevision;
            await (RevisionLoadTask = LoadReviewAsync());
            if (_active && ReferenceEquals(selected, _selectedEntry) && revision.HasValue
                && _revisions.FirstOrDefault(r => r.Item.Revision == revision.Value) is { } row)
                await (SnapshotLoadTask = LoadSnapshotAsync(row.Item));
            else if (_active && ReferenceEquals(selected, _selectedEntry) && revision.HasValue) { ClearSnapshot(); Notify(); }
        }
    }

    private Task ChangePageAsync(int delta)
    {
        _page += delta;
        ClearSelection();
        return LoadTask = LoadAsync();
    }

    private async Task LoadAsync()
    {
        CancelList();
        long generation = _listGeneration;
        Guid? account = _accountId;
        int page = _page;
        using var cancellation = new CancellationTokenSource();
        _listCancellation = cancellation;
        _loading = true;
        _entries = [];
        _error = null;
        Notify();
        try
        {
            var result = await Task.Run(() => _reader.BrowseAsync(account, page, PageSize, cancellation.Token), cancellation.Token);
            if (!_active || generation != _listGeneration || cancellation.IsCancellationRequested) return;
            _entries = result.Items.Select(i => new JournalHistoryRow(i)).ToArray();
            _total = result.TotalCount;
            int lastPage = Math.Max(1, (int)Math.Ceiling(_total / (double)PageSize));
            if (_page > lastPage) { _page = lastPage; await LoadAsync(); return; }
            if (_selectedEntry is not null && _entries.FirstOrDefault(r => r.Item.Id == _selectedEntry.Item.Id) is { } selected)
                _selectedEntry = selected;
            else if (_selectedEntry is not null) ClearSelection();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception)
        {
            if (generation == _listGeneration && _active) _error = "Review history could not be loaded. Refresh to retry; your editor is unchanged.";
        }
        finally
        {
            if (generation == _listGeneration) { _loading = false; _listCancellation = null; Notify(); }
        }
    }

    private Task OpenAsync(JournalHistoryRow? row)
    {
        // Ignore obsolete buttons retained by a disconnected row/template.
        if (row is null || !_active || (_accountId.HasValue && row.Item.AccountId != _accountId)
            || !_entries.Any(current => ReferenceEquals(current, row)))
            return Task.CompletedTask;
        bool closing = ReferenceEquals(_selectedEntry, row);
        ClearSelection();
        if (closing) { Notify(); return Task.CompletedTask; }
        _selectedEntry = row;
        Notify();
        return RevisionLoadTask = LoadReviewAsync();
    }

    private Task ChangeRevisionPageAsync(int delta)
    {
        _revisionPage += delta;
        ClearSnapshot();
        return RevisionLoadTask = LoadRevisionsAsync();
    }

    private async Task LoadRevisionsAsync()
    {
        CancelRevisions();
        long generation = _revisionGeneration;
        if (_selectedEntry is null) return;
        Guid journalId = _selectedEntry.Item.Id;
        int page = _revisionPage;
        using var cancellation = new CancellationTokenSource();
        _revisionCancellation = cancellation;
        _revisionLoading = true;
        _revisions = [];
        _revisionError = null;
        Notify();
        try
        {
            var result = await Task.Run(() => _reader.BrowseRevisionsAsync(journalId, page, RevisionPageSize, cancellation.Token), cancellation.Token);
            if (!_active || generation != _revisionGeneration || cancellation.IsCancellationRequested) return;
            _revisions = result.Items.Select(i => new JournalRevisionRow(i,
                i.Revision == (_preview?.Entry.Revision ?? _selectedEntry.Item.Revision))).ToArray();
            _revisionTotal = result.TotalCount;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception)
        {
            if (generation == _revisionGeneration && _active) _revisionError = "Revisions could not be loaded. Refresh history to retry.";
        }
        finally
        {
            if (generation == _revisionGeneration) { _revisionLoading = false; _revisionCancellation = null; Notify(); }
        }
    }

    private Task ViewRevisionAsync(JournalRevisionRow? row)
    {
        if (!_active || row is null || !_revisions.Any(current => ReferenceEquals(current, row))) return Task.CompletedTask;
        if (_viewingRevision == row.Item.Revision && HasRevisionView)
        {
            ClearSnapshot(); Notify(); return Task.CompletedTask;
        }
        return SnapshotLoadTask = LoadSnapshotAsync(row.Item);
    }

    private async Task LoadSnapshotAsync(JournalRevisionItem item)
    {
        ClearSnapshot();
        _viewingRevision = item.Revision;
        long generation = _snapshotGeneration;
        using var cancellation = new CancellationTokenSource();
        _snapshotCancellation = cancellation;
        _snapshotLoading = true;
        Notify();
        try
        {
            var result = await Task.Run(() => _reader.GetRevisionAsync(item.JournalId, item.Revision, cancellation.Token), cancellation.Token);
            if (!_active || generation != _snapshotGeneration || cancellation.IsCancellationRequested) return;
            _snapshot = result;
            _snapshotError = result is null ? "This revision is unavailable. Refresh history to check again." : null;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception)
        {
            if (generation == _snapshotGeneration && _active) _snapshotError = "Revision could not be loaded. Close and reopen this revision, or Refresh to retry.";
        }
        finally
        {
            if (generation == _snapshotGeneration) { _snapshotLoading = false; _snapshotCancellation = null; Notify(); }
        }
    }

    private void ClearSelection()
    {
        CancelPreview();
        _deleteCancellation?.Cancel();
        _preview = null;
        _previewError = _deleteMessage = null;
        _deleteNeedsRefresh = false;
        if (_selectedEntry is not null) _selectedEntry.Review = null;
        CancelRevisions();
        ClearSnapshot();
        _selectedEntry = null;
        _revisions = [];
        _revisionPage = 1;
        _revisionTotal = 0;
        _revisionError = null;
    }
    private void ClearSnapshot()
    {
        CancelSnapshot(); _snapshot = null; _snapshotError = null; _viewingRevision = null;
        foreach (var row in _revisions) row.IsExpanded = false;
    }
    private void CancelPreview() { _previewGeneration++; _previewCancellation?.Cancel(); _previewCancellation = null; _previewLoading = false; }
    private void CancelList() { _listGeneration++; _listCancellation?.Cancel(); _listCancellation = null; _loading = false; }
    private void CancelRevisions() { _revisionGeneration++; _revisionCancellation?.Cancel(); _revisionCancellation = null; _revisionLoading = false; }
    private void CancelSnapshot() { _snapshotGeneration++; _snapshotCancellation?.Cancel(); _snapshotCancellation = null; _snapshotLoading = false; }
    private void Cancel()
    {
        CancelList(); CancelRevisions(); CancelSnapshot();
        CancelPreview(); _deleteCancellation?.Cancel();
        _error = "History loading cancelled. Refresh to retry; your editor is unchanged.";
        Notify();
    }
    private void Notify()
    {
        foreach (var row in _entries) row.Review = ReferenceEquals(row, _selectedEntry) ? this : null;
        foreach (var row in _revisions) row.IsExpanded = HasRevisionView && row.Item.Revision == _viewingRevision;
        OnPropertyChanged(string.Empty);
        RefreshCommand.NotifyCanExecuteChanged(); PreviousCommand.NotifyCanExecuteChanged(); NextCommand.NotifyCanExecuteChanged();
        OpenCommand.NotifyCanExecuteChanged(); PreviousRevisionsCommand.NotifyCanExecuteChanged(); NextRevisionsCommand.NotifyCanExecuteChanged();
        ViewRevisionCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged();
        CloseViewCommand.NotifyCanExecuteChanged();
        CloseReviewCommand.NotifyCanExecuteChanged();
        OpenInEditorCommand.NotifyCanExecuteChanged();
        DeleteRevisionCommand.NotifyCanExecuteChanged();
    }

    private async Task LoadPreviewAsync()
    {
        CancelPreview();
        _preview = null;
        _previewError = null;
        if (_repository is null || _selectedEntry is not { } row) return;
        long generation = _previewGeneration;
        using var cancellation = new CancellationTokenSource();
        _previewCancellation = cancellation;
        _previewLoading = true;
        Notify();
        try
        {
            var result = await Task.Run(() => _repository.GetAsync(row.Item.TradingDate, row.Item.AccountId, cancellation.Token), cancellation.Token);
            if (!_active || generation != _previewGeneration || cancellation.IsCancellationRequested) return;
            _preview = result?.Entry.Id == row.Item.Id ? result : null;
            _previewError = _preview is null ? "Current review is unavailable. Refresh History; your editor is unchanged." : null;
            _deleteNeedsRefresh = false;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception) { if (generation == _previewGeneration) _previewError = "Current review could not be loaded. Refresh History to retry."; }
        finally { if (generation == _previewGeneration) { _previewLoading = false; _previewCancellation = null; Notify(); } }
    }

    private async Task LoadReviewAsync()
    {
        // Establish the current token before decorating revision rows. Independent continuations
        // must not replace a freshly loaded revision list with a stale pre-preview list.
        Task preview = PreviewLoadTask = LoadPreviewAsync();
        long generation = _previewGeneration;
        await preview;
        if (_active && generation == _previewGeneration && _selectedEntry is not null) await LoadRevisionsAsync();
    }

    private bool CanDeleteRevision(JournalRevisionRow? row) => _active && !_deleting && !_previewLoading && !_deleteNeedsRefresh
        && _preview is not null && _revisionWriter is not null && _dialogs is not null && row is not null
        && row.Item.Revision < _preview.Entry.Revision && _revisions.Any(r => ReferenceEquals(r, row));

    private async Task DeleteRevisionAsync(JournalRevisionRow? row)
    {
        if (!CanDeleteRevision(row) || _selectedEntry is not { } selected || _preview is not { } preview || row is null) return;
        if (!_dialogs!.Confirm(new("Delete revision permanently?",
            $"New York date {selected.DateText}, Account scope: {selected.ScopeText} (ID: {selected.Item.AccountId?.ToString() ?? "All accounts"}), " +
            $"{row.StateText}. Delete only this revision permanently? The current journal and other revisions are kept. This cannot be undone.",
            "Delete revision", "Keep revision", isDestructive: true))) return;
        using var cancellation = new CancellationTokenSource();
        _deleteCancellation = cancellation;
        _deleting = true;
        _deleteMessage = null;
        long selection = _previewGeneration;
        Notify();
        try
        {
            var result = await Task.Run(() => _revisionWriter!.DeleteRevisionAsync(
                new(selected.Item.Id, row.Item.Revision, preview.Entry.Revision), cancellation.Token), cancellation.Token);
            if (!_active || selection != _previewGeneration) return;
            if (result == DeleteJournalRevisionStatus.Deleted)
            {
                if (_viewingRevision == row.Item.Revision) ClearSnapshot();
                _revisionTotal = Math.Max(0, _revisionTotal - 1);
                _revisionPage = Math.Min(_revisionPage, Math.Max(1, (_revisionTotal + RevisionPageSize - 1) / RevisionPageSize));
                await (RevisionLoadTask = LoadRevisionsAsync());
                _deleteMessage = "Revision deleted. Current journal content and state are unchanged.";
            }
            else
            {
                _deleteNeedsRefresh = true;
                _deleteMessage = result switch
                {
                    DeleteJournalRevisionStatus.Conflict => "The journal has a newer revision. Refresh History, review it, then select an older revision again.",
                    DeleteJournalRevisionStatus.CurrentRevisionProtected => "The current revision cannot be deleted. Refresh History to see older revisions.",
                    _ => "The journal or revision no longer exists. Refresh History to update the list."
                };
            }
        }
        catch (OperationCanceledException) { if (_active && selection == _previewGeneration) _deleteMessage = "Deletion cancelled. The list is retained; Refresh History to check committed state."; }
        catch (Exception) { if (_active && selection == _previewGeneration) _deleteMessage = "Revision could not be deleted. The list is retained; refresh or try again."; }
        finally { _deleting = false; _deleteCancellation = null; Notify(); }
    }
}
