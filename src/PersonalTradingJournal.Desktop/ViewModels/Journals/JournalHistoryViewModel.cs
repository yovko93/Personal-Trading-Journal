using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Formatting;

namespace PersonalTradingJournal.Desktop.ViewModels.Journals;

public sealed record JournalHistoryRow(JournalHistoryItem Item)
{
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
    public string OpenAccessibleName => "Open review: " + Description;
}

public sealed record JournalRevisionRow(JournalRevisionItem Item)
{
    public string StateText => $"Revision {Item.Revision} · {(Item.IsDraft ? "Draft" : "Completed")}";
    public string SavedAtText => TradingTimestampFormatter.FormatNewYork(Item.SavedAtUtc, "G", CultureInfo.CurrentCulture) + " · New York";
    public string Description => StateText + " · " + SavedAtText;
    public string ViewAccessibleName => "View read-only revision: " + Description;
}

/// <summary>Metadata pages and one immutable snapshot; never writes or replaces editor contents.</summary>
public sealed class JournalHistoryViewModel : ObservableObject
{
    private readonly IDailyJournalHistoryReader _reader;
    private readonly Func<JournalHistoryItem, bool> _open;
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

    public JournalHistoryViewModel(IDailyJournalHistoryReader reader, Func<JournalHistoryItem, bool> open)
    {
        _reader = reader;
        _open = open;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => _active,
            AsyncRelayCommandOptions.AllowConcurrentExecutions);
        PreviousCommand = new AsyncRelayCommand(() => ChangePageAsync(-1), () => _active && !IsLoading && _page > 1);
        NextCommand = new AsyncRelayCommand(() => ChangePageAsync(1), () => _active && !IsLoading && (long)_page * PageSize < _total);
        OpenCommand = new AsyncRelayCommand<JournalHistoryRow>(OpenAsync, row => _active && row is not null,
            AsyncRelayCommandOptions.AllowConcurrentExecutions);
        PreviousRevisionsCommand = new AsyncRelayCommand(() => ChangeRevisionPageAsync(-1),
            () => _active && !IsRevisionLoading && _revisionPage > 1);
        NextRevisionsCommand = new AsyncRelayCommand(() => ChangeRevisionPageAsync(1),
            () => _active && !IsRevisionLoading && (long)_revisionPage * RevisionPageSize < _revisionTotal);
        ViewRevisionCommand = new AsyncRelayCommand<JournalRevisionRow>(ViewRevisionAsync,
            row => _active && row is not null, AsyncRelayCommandOptions.AllowConcurrentExecutions);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
        CloseViewCommand = new RelayCommand(() => { ClearSnapshot(); Notify(); }, () => _active && HasRevisionView);
        CloseReviewCommand = new RelayCommand(() => { ClearSelection(); Notify(); }, () => _active && HasSelectedEntry);
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
    public bool IsBusy => _loading || _revisionLoading || _snapshotLoading;
    public string PageText => $"Page {_page} · {_total} reviews";
    public string Heading => $"Review History · {_total} reviews";
    public string RevisionPageText => $"Page {_revisionPage} · {_revisionTotal} revisions";
    public string StatusText => IsLoading ? "Loading reviews…" : _error ?? (_entries.Count == 0
        ? "No saved reviews in this Account scope." : "Open a review or browse revisions.");
    public string RevisionStatusText => IsRevisionLoading ? "Loading revisions…" : _revisionError ?? (_selectedEntry is null
        ? "" : _revisions.Count == 0 ? "No revisions available." : "Viewing history does not save or restore a revision.");
    public string? SnapshotStatusText => _snapshotLoading ? "Loading revision…" : _snapshotError;
    public Task LoadTask { get; private set; } = Task.CompletedTask;
    public Task RevisionLoadTask { get; private set; } = Task.CompletedTask;
    public Task SnapshotLoadTask { get; private set; } = Task.CompletedTask;
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand PreviousCommand { get; }
    public IAsyncRelayCommand NextCommand { get; }
    public IAsyncRelayCommand<JournalHistoryRow> OpenCommand { get; }
    public IAsyncRelayCommand PreviousRevisionsCommand { get; }
    public IAsyncRelayCommand NextRevisionsCommand { get; }
    public IAsyncRelayCommand<JournalRevisionRow> ViewRevisionCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand CloseViewCommand { get; }
    public IRelayCommand CloseReviewCommand { get; }

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

    public Task RefreshAsync() => !_active ? Task.CompletedTask : Task.WhenAll(
        LoadTask = LoadAsync(), _selectedEntry is null ? Task.CompletedTask : RevisionLoadTask = LoadRevisionsAsync());

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
            if (_selectedEntry is not null && _entries.FirstOrDefault(r => r.Item.Id == _selectedEntry.Item.Id) is { } selected)
                _selectedEntry = selected;
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
        if (row is null || !_active || row.Item.AccountId != _accountId || !_entries.Any(current => ReferenceEquals(current, row)) || !_open(row.Item))
            return Task.CompletedTask;
        ClearSelection();
        _selectedEntry = row;
        Notify();
        return RevisionLoadTask = LoadRevisionsAsync();
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
            _revisions = result.Items.Select(i => new JournalRevisionRow(i)).ToArray();
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
        return SnapshotLoadTask = LoadSnapshotAsync(row.Item);
    }

    private async Task LoadSnapshotAsync(JournalRevisionItem item)
    {
        ClearSnapshot();
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
            if (generation == _snapshotGeneration && _active) _snapshotError = "Revision could not be loaded. Select View revision to retry.";
        }
        finally
        {
            if (generation == _snapshotGeneration) { _snapshotLoading = false; _snapshotCancellation = null; Notify(); }
        }
    }

    private void ClearSelection()
    {
        CancelRevisions();
        ClearSnapshot();
        _selectedEntry = null;
        _revisions = [];
        _revisionPage = 1;
        _revisionTotal = 0;
        _revisionError = null;
    }
    private void ClearSnapshot() { CancelSnapshot(); _snapshot = null; _snapshotError = null; }
    private void CancelList() { _listGeneration++; _listCancellation?.Cancel(); _listCancellation = null; _loading = false; }
    private void CancelRevisions() { _revisionGeneration++; _revisionCancellation?.Cancel(); _revisionCancellation = null; _revisionLoading = false; }
    private void CancelSnapshot() { _snapshotGeneration++; _snapshotCancellation?.Cancel(); _snapshotCancellation = null; _snapshotLoading = false; }
    private void Cancel()
    {
        CancelList(); CancelRevisions(); CancelSnapshot();
        _error = "History loading cancelled. Refresh to retry; your editor is unchanged.";
        Notify();
    }
    private void Notify()
    {
        OnPropertyChanged(string.Empty);
        RefreshCommand.NotifyCanExecuteChanged(); PreviousCommand.NotifyCanExecuteChanged(); NextCommand.NotifyCanExecuteChanged();
        OpenCommand.NotifyCanExecuteChanged(); PreviousRevisionsCommand.NotifyCanExecuteChanged(); NextRevisionsCommand.NotifyCanExecuteChanged();
        ViewRevisionCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged();
        CloseViewCommand.NotifyCanExecuteChanged();
        CloseReviewCommand.NotifyCanExecuteChanged();
    }
}
