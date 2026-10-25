using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Application.Common.Time;
using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Desktop.ViewModels.Notebook;

public sealed record NotebookScopeOption(NotebookScope Scope, Guid? FolderId, string Name, long Count)
{
    public string Label => $"{Name}  ·  {Count}";
}
public sealed record NotebookFolderOption(Guid? Id, string Name, long Version = 0);
public sealed record NotebookSortOption(NotebookSort Value, string Name);
public sealed record NotebookRow(NotebookListItem Item)
{
    public Guid Id => Item.Id;
    public string Title => Item.Title;
    public string Preview => Item.Preview;
    public string Tags => string.Join(" · ", Item.Tags.Select(t => t.Name));
    public IEnumerable<NotebookNamedItem> TagChips => Item.Tags.Take(2);
    public string RemainingTags => Item.Tags.Length > 2 ? $"+{Item.Tags.Length - 2}" : "";
    public string Pin => Item.IsPinned ? "Pinned" : "";
    public string Updated => $"{TradingTimePolicy.ConvertUtcToTradingTime(Item.UpdatedAtUtc):g} NY";
    public string AccessibleName => $"{Title}. {Pin}. Updated {Updated}. {Tags}";
}

/// <summary>One read owner spans list, detail and link resolution. Superseded operations are drained,
/// but cannot publish. Writes are exclusive and retain captured identity/version until verified completion.</summary>
public sealed partial class NotebookWorkspaceViewModel : ObservableObject, IDisposable
{
    private readonly INotebookService _service;
    private readonly IDialogService _dialogs;
    private readonly HashSet<Task> _pending = [];
    private CancellationTokenSource? _read, _write;
    private long _generation, _selectionGeneration;
    private bool _active, _disposed, _busy, _writing, _publishing;
    private NotebookScopeOption? _scope;
    private NotebookRow? _selected;
    private NotebookNoteSnapshot? _note;
    private string _message = "", _search = "", _folderName = "", _contentMessage = "", _appliedScopeName = "All Notes";
    private string? _cursor, _folderCursor, _tagCursor;
    private readonly List<NotebookCatalogItem> _tags = [];
    private NotebookQuery _query = new();
    private NotebookSortOption _sort;
    private NotebookFolderOption? _managedFolder, _moveFolder;
    private int _panel = 1;
    private long _total;

    public NotebookWorkspaceViewModel(INotebookService service, IDialogService dialogs,
        INotebookInteraction? interaction = null, INotebookContentCodec? codec = null,
        INotebookRecoveryStore? recovery = null, TimeProvider? time = null, INotebookImages? images = null,
        Screenshots.INotebookImagePresentation? imagePresentation = null)
    {
        _service = service; _dialogs = dialogs; _sort = SortOptions[0]; _interaction = interaction; _codec = codec;
        _recovery = recovery; _time = time ?? TimeProvider.System;
        _images = images; ImagePresentation = imagePresentation;
        InitializeTags();
        InitializeLinks();
        RecoverCommand = new(item => GuardedAsync(() => RecoverAsync(item)), _ => CanMutate);
        DeleteRecoveryCommand = new(DeleteRecoveryAsync, _ => CanMutate);
        MoreRecoveryCommand = new(() => ReadAsync(ct => DiscoverRecoveryAsync(true, ct)), () => CanMutate && _recoveryCursor is not null);
        RefreshCommand = new(() => ReloadAsync(), () => CanRead);
        SearchCommand = new(ApplySearchAsync, () => CanRead);
        MoreNotesCommand = new(() => ReadAsync(ct => ReadPageAsync(true, null, ct)), () => CanMutate && ScopeMatchesList && _cursor is not null);
        MoreFoldersCommand = new(LoadMoreFoldersAsync, () => CanMutate && _folderCursor is not null);
        MoreTagsCommand = new(LoadMoreTagsAsync, () => CanMutate && _tagCursor is not null);
        NewNoteCommand = new(() => CreateFromTemplateAsync(NotebookTemplate.BlankNote), () => CanStartNoteOperation);
        InitializeLifecycle();
        CreateFolderCommand = new(CreateFolderAsync, () => CanMutate && !IsFolderCreationPending);
        RenameFolderCommand = new(RenameFolderAsync, () => CanChangeFolder && IsFolderRenameOpen);
        DeleteFolderCommand = new(DeleteFolderAsync, () => CanChangeFolder);
        MoveCommand = new(() =>
        {
            var owns = CaptureNoteActionOwnership();
            var target = MoveFolder; // Save may refresh the bounded folder catalog; retain the user's exact destination.
            return GuardedAsync(() => Note is { } n && target is { } f
                ? WriteNoteAsync(new MoveNotebookNote(n.Id, n.Version, f.Id)) : Task.CompletedTask, owns: owns);
        }, () => CanEditNote && MoveFolder is not null);
        PinCommand = new(() =>
        {
            var owns = CaptureNoteActionOwnership();
            return GuardedAsync(() => Note is { } n ? WriteNoteAsync(new SetNotebookPin(n.Id, n.Version, !n.IsPinned)) : Task.CompletedTask, owns: owns);
        }, () => CanEditNote);
        CancelCommand = new(CancelAsync, () => IsBusy);
        ClearFolderCommand = new(() => FolderName = "");
        SaveCommand = new(async () => { await SaveDraftAsync(); }, () => CanMutate && CanEditDraft && IsDirty);
        DiscardCommand = new(async () =>
        {
            var ownsContext = CaptureNoteActionOwnership(); var revision = _draftRevision;
            bool Owns() => ownsContext() && revision == _draftRevision;
            if (_dialogs.Confirm(new("Discard changes?", "Discard pending changes and their recovery checkpoint? Already saved changes are not undone.", "Discard")) &&
                Owns() && await DiscardCheckpointAsync(Owns) && Owns()) RestoreDraft();
        }, () => CanMutate && IsDirty);
    }
    public ObservableCollection<NotebookScopeOption> Scopes { get; } = [];
    public ObservableCollection<NotebookFolderOption> Folders { get; } = [new(null, "No folder")];
    public ObservableCollection<NotebookRow> Notes { get; } = [];
    public static NotebookSortOption[] SortOptions { get; } = [new(NotebookSort.RecentlyUpdated, "Recently updated"), new(NotebookSort.Title, "Title")];
    public NotebookSortOption Sort { get => _sort; set
    {
        if (_publishing) { SetProperty(ref _sort, value); return; }
        if (value == _sort || !CanRead) return;
        LoadTask = GuardedAsync(async () => { SetProperty(ref _sort, value); await ReloadAsync(); });
        OnPropertyChanged(nameof(Sort));
    } }
    public NotebookScopeOption? Scope
    {
        get => _scope;
        set
        {
            if (value?.Scope is NotebookScope.Unfiled or NotebookScope.Archived)
                value = Scopes.FirstOrDefault(s => s.Scope == NotebookScope.AllNotes) ?? new(NotebookScope.AllNotes, null, "All Notes", 0);
            // Catalog refresh replaces equal-valued records. Select the new item instance so
            // filtered navigation views restore selection after their collections are rebuilt.
            if (_publishing) { SetProperty(ref _scope, value, ReferenceEqualityComparer.Instance); return; }
            if (!CanRead || value is null || (_scope?.Scope == value.Scope && _scope.FolderId == value.FolderId)) return;
            LoadTask = SelectScopeAsync(value);
            OnPropertyChanged(nameof(Scope));
        }
    }
    public NotebookRow? SelectedNote
    {
        get => _selected;
        set
        {
            if (_publishing) { AcceptSelection(value); return; }
            if (!CanRead || !ScopeMatchesList || _selected?.Id == value?.Id) return;
            LoadTask = GuardedAsync(async () =>
            {
                AcceptSelection(value);
                if (value is not null) Panel = 2;
                await ReadAsync(ct => ReadDetailAsync(value, ct));
            });
            OnPropertyChanged(nameof(SelectedNote));
        }
    }
    private void AcceptSelection(NotebookRow? value)
    {
        // Invalidate before notifications or the replacement detail read can yield.
        // A same-note save/list refresh is not a new accepted selection.
        if (_selected?.Id != value?.Id) ++_selectionGeneration;
        SetProperty(ref _selected, value, nameof(SelectedNote)); SelectionLabels();
    }
    public NotebookNoteSnapshot? Note { get => _note; private set { SetProperty(ref _note, value); OnPropertyChanged(nameof(NoteTitle)); OnPropertyChanged(nameof(Metadata)); OnPropertyChanged(nameof(TagText)); OnPropertyChanged(nameof(Breadcrumb)); OnPropertyChanged(nameof(PinText)); PublishTags(); NotifyCommands(); } }
    public string NoteTitle => Note?.Title ?? SelectedNote?.Title ?? "Select a note";
    public string LegacyReadOnlyMessage => LifecycleState == NotebookNoteState.Archived
        ? Note is not null ? "Archived note — read-only. Use Duplicate in Note actions to create an editable copy. The original is preserved."
            : "Archived note — read-only. Content is unavailable; its stored data is preserved." : "";
    public string Metadata => Note is { } n ? $"{n.State} · Updated {TradingTimePolicy.ConvertUtcToTradingTime(n.UpdatedAtUtc):g} NY"
        : SelectedNote is { } row ? $"{row.Item.State} · Updated {row.Updated}" : "";
    public string Breadcrumb => (Note is not null ? Note.FolderId : SelectedNote?.Item.FolderId) is Guid id ? Folders.FirstOrDefault(f => f.Id == id)?.Name ?? "Folder unavailable" : "No folder";
    public string TagText => string.Join(" · ", AssignedTags.Select(t => t.Name));
    public string LinkText { get; private set; } = "";
    private void SelectionLabels() { OnPropertyChanged(nameof(NoteTitle)); OnPropertyChanged(nameof(TagText)); OnPropertyChanged(nameof(Metadata)); OnPropertyChanged(nameof(Breadcrumb)); }
    public string ContentMessage { get => _contentMessage; private set => SetProperty(ref _contentMessage, value); }
    public string ScopeName => _appliedScopeName;
    public string TagCatalogText => _tags.Count == 0 ? "No tags" : string.Join("\n", _tags.Select(t => $"{t.Item.Name} · {t.ActiveNoteCount}"));
    public string ListSummary => $"{Notes.Count} of {_total} notes";
    public bool HasMoreNotes => _cursor is not null;
    public bool HasMoreFolders => _folderCursor is not null;
    public bool HasMoreTags => _tagCursor is not null;
    public string EmptyMessage => IsBusy ? "Loading notes…" : Notes.Count != 0 ? "" :
        _query.Search.Length != 0 || _query.TagIds.Length != 0 ? "No notes match these filters. Clear filters or change the search." :
        "No notes in this view. Create a note or choose another scope.";
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public string SearchText { get => _search; set { if (SetProperty(ref _search, value) && !_publishing) ScheduleSearch(); } }
    public string FolderName { get => _folderName; set => SetProperty(ref _folderName, value); }
    public NotebookFolderOption? ManagedFolder { get => _managedFolder; set { SetProperty(ref _managedFolder, value); NotifyCommands(); } }
    public NotebookFolderOption? MoveFolder { get => _moveFolder; set { SetProperty(ref _moveFolder, value); NotifyCommands(); } }
    public int Panel { get => _panel; set { if (SetProperty(ref _panel, value)) { ++_panelGeneration; if (IsFolderCreationPending) DismissFolderCreation(); } } }
    public bool IsBusy { get => _busy; private set { SetProperty(ref _busy, value); OnPropertyChanged(nameof(EmptyMessage)); OnPropertyChanged(nameof(CanSelectNote)); NotifyCommands(); } }
    public bool IsWriting { get => _writing; private set { SetProperty(ref _writing, value); OnPropertyChanged(nameof(CanInteract)); NotifyCommands(); } }
    public bool CanInteract => !_disposed && !IsWriting && !_noteOperation;
    public bool CanSelectNote => CanInteract && !IsBusy && ScopeMatchesList;
    private bool ScopeMatchesList => (Scope is null || (_query.Scope == Scope.Scope && _query.FolderId == Scope.FolderId)) &&
        _query.Search == SearchText && _query.TagIds.Order().SequenceEqual(_filterIds.Order()) && _query.Sort == Sort.Value;
    private bool CanRead => _active && !_disposed && !IsWriting && !_noteOperation;
    private bool CanMutate => _active && !_disposed && !IsWriting && !IsBusy;
    private bool CanEditNote => CanMutate && Note?.State == NotebookNoteState.Active;
    public string PinText => Note?.IsPinned == true ? "Unpin" : "Pin note";
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand SearchCommand { get; }
    public AsyncRelayCommand MoreNotesCommand { get; }
    public AsyncRelayCommand MoreFoldersCommand { get; }
    public AsyncRelayCommand MoreTagsCommand { get; }
    public AsyncRelayCommand NewNoteCommand { get; }
    public AsyncRelayCommand CreateFolderCommand { get; }
    public AsyncRelayCommand RenameFolderCommand { get; }
    public AsyncRelayCommand DeleteFolderCommand { get; }
    public AsyncRelayCommand MoveCommand { get; }
    public AsyncRelayCommand PinCommand { get; }
    public AsyncRelayCommand CancelCommand { get; }
    public RelayCommand ClearFolderCommand { get; }
    public Task LoadTask { get; private set; } = Task.CompletedTask;
    public Task CompletionTask { get { lock (_pending) return Task.WhenAll(_pending.ToArray()); } }
    public Task DeactivationTask { get; private set; } = Task.CompletedTask;
    public event EventHandler? NoteCreated;

    public Task ActivateAsync()
    {
        if (_disposed || _active) return LoadTask;
        _active = true;
        _lifetime?.Dispose(); _lifetime = new();
        var owner = _lifetime; var drain = DeactivationTask;
        return LoadTask = ActivateCoreAsync();
        async Task ActivateCoreAsync()
        {
            await drain;
            if (!ReferenceEquals(_lifetime, owner) || !Current(owner.Token)) return;
            if (IsDirty)
            {
                // A retained recovery draft is never reinstalled from canonical content on return.
                await ReadAsync(async ct =>
                {
                    await ReadPageAsync(false, Note?.Id ?? SelectedNote?.Id, ct, keepEditor: true);
                    if (Current(ct)) await ResolveCurrentLinksAsync(ct);
                });
            }
            else await ReloadAsync();
        }
    }
    public bool TryLeave()
    {
        if (_noteOperation) { Message = "Wait for the note operation before leaving."; return false; }
        if (_linkOperation) { Message = "Wait for the linked-record operation before leaving."; return false; }
        if (!IsWriting && (!IsDirty || HasLeaveConsent)) return true;
        Message = IsWriting ? "A Notebook change is in progress. Wait or cancel before leaving." : "Save, discard or keep editing before leaving."; return false;
    }
    public void Deactivate()
    {
        _active = false; ++_generation;
        _lifetime?.Cancel(); StopDebounce(); StopSearch(); StopLinkTargetRead(); _read?.Cancel(); _write?.Cancel();
        DeactivationTask = CompletionTask;
        IsBusy = false; NotifyCommands();
        // Departure consent is coordinated by the shell; late operations cannot publish here.
    }
    public async Task CancelAsync()
    {
        ++_generation; _lifetime?.Cancel(); StopDebounce(); StopSearch(); StopLinkTargetRead(); _read?.Cancel(); _write?.Cancel();
        await CompletionTask;
        if (!_disposed)
        {
            _lifetime?.Dispose(); _lifetime = new();
            RestoreAppliedFilters();
            IsBusy = false;
            Message = "Operation stopped. Refresh to reload stored data; a completed change may already be saved.";
            if (ContentMessage == "Loading note…") ContentMessage = "Note loading stopped. Select Refresh to try again.";
        }
    }
    public void Dispose() { if (_disposed) return; Deactivate(); _disposed = true; }
    private Task Track(Task task)
    {
        lock (_pending) _pending.Add(task);
        return DrainAsync(task);
    }
    private async Task DrainAsync(Task task)
    {
        try { await task; } finally { lock (_pending) _pending.Remove(task); }
    }
    private Task ReloadAsync(bool preserveSelection = true)
    {
        StopSearch();
        if (!CanRead) return Task.CompletedTask;
        if (IsDirty && !_guarding) return GuardedAsync(() => ReloadAsync(preserveSelection));
        return ReadAsync(ct => ReadPageAsync(false, preserveSelection ? SelectedNote?.Id : null, ct));
    }
    private Task ReadAsync(Func<CancellationToken, Task> work)
    {
        _read?.Cancel(); var owner = CancellationTokenSource.CreateLinkedTokenSource(_lifetime?.Token ?? CancellationToken.None); _read = owner; var generation = ++_generation;
        IsBusy = true; Message = "";
        return LoadTask = Track(RunAsync());
        async Task RunAsync()
        {
            await Task.Yield();
            try { owner.Token.ThrowIfCancellationRequested(); await work(owner.Token); }
            catch (OperationCanceledException) { }
            catch (ArgumentException) { if (Current(owner.Token)) Message = "Check the search text (up to 256 characters) and selected folder, then try again."; }
            catch (Exception)
            {
                if (Current(owner.Token))
                {
                    Message = "Notebook could not be loaded. Previous results are retained. Select Refresh to try again.";
                    if (ContentMessage == "Loading note…") ContentMessage = "Note content could not be loaded. Select Refresh to try again.";
                }
            }
            finally
            {
                if (generation == _generation && !_disposed) IsBusy = false;
                if (ReferenceEquals(_read, owner)) _read = null;
                owner.Dispose();
            }
        }
    }
    private bool Current(CancellationToken ct) => !ct.IsCancellationRequested && _active && !_disposed;
    private async Task ReadPageAsync(bool more, Guid? selected, CancellationToken ct, bool keepEditor = false)
    {
        var query = more ? new NotebookQuery(_query.Scope, _query.FolderId, _query.Search, _query.TagIds, sort: _query.Sort, cursor: _cursor)
            : new NotebookQuery(Scope?.Scope ?? NotebookScope.AllNotes, Scope?.FolderId, SearchText, _filterIds, sort: Sort.Value);
        var page = await Task.Run(() => _service.ListAsync(query, ct), ct);
        if (!Current(ct)) return;
        if (!more && !page.Summary.RequestedTags.IsDefault)
        {
            var present = page.Summary.RequestedTags.Select(t => t.Item.Id).ToHashSet();
            var deleted = _filterIds.Where(id => !present.Contains(id)).ToHashSet();
            if (deleted.Count > 0)
            {
                _filterIds.ExceptWith(deleted);
                // The selected-ID resolution is authoritative even outside the catalog page.
                // Drop deleted entries before catalog retention sees the old open-note snapshot.
                _tags.RemoveAll(t => deleted.Contains(t.Item.Id));
                TagMessage = "Deleted tag filters were removed. Notes and assignments were not changed.";
                query = new(query.Scope, query.FolderId, query.Search, _filterIds, query.Sort);
                page = await Task.Run(() => _service.ListAsync(query, ct), ct); // One bounded filter repair, never a retry loop.
                if (!Current(ct)) return;
            }
        }
        _publishing = true;
        try
        {
            if (!more) { Notes.Clear(); _query = query; PublishCatalog(page.Summary); _appliedScopeName = Scope?.Name ?? "All Notes"; OnPropertyChanged(nameof(ScopeName)); }
            foreach (var row in page.Items) if (Notes.All(n => n.Id != row.Id)) Notes.Add(new(row));
            _cursor = page.NextCursor; _total = page.FilteredCount;
            if (!more) SelectedNote = Notes.FirstOrDefault(n => n.Id == selected);
        }
        finally { _publishing = false; }
        PublishTags(); OnPropertyChanged(nameof(FilterSummary));
        OnPropertyChanged(nameof(ListSummary)); OnPropertyChanged(nameof(EmptyMessage)); NotifyCommands();
        if (!more && !keepEditor)
        {
            await ReadDetailAsync(SelectedNote, ct);
            if (Current(ct)) await DiscoverRecoveryAsync(false, ct);
        }
    }
    private void PublishCatalog(NotebookWorkspaceSummary summary)
    {
        var old = Scope;
        var oldFolder = Folders.FirstOrDefault(f => f.Id == old?.FolderId);
        var managedId = ManagedFolder?.Id;
        var moveId = MoveFolder is { } move ? move.Id : Note?.FolderId;
        Scopes.Clear();
        foreach (var (scope, label) in new[] { (NotebookScope.AllNotes, "All Notes"), (NotebookScope.Pinned, "Pinned") })
            Scopes.Add(new(scope, null, label, summary.ScopeCounts.FirstOrDefault(c => c.Scope == scope)?.Count ?? 0));
        Folders.Clear(); Folders.Add(new(null, "No folder"));
        foreach (var folder in summary.Folders.Items) AddFolder(folder);
        // A selected folder from a later catalog page remains reachable after a refresh.
        if (old?.FolderId is Guid id && Folders.All(f => f.Id != id))
        { Scopes.Add(old); Folders.Add(oldFolder ?? new(id, old.Name)); }
        foreach (var (scope, label) in new[] { (NotebookScope.Trash, "Trash") })
            Scopes.Add(new(scope, null, label, summary.ScopeCounts.FirstOrDefault(c => c.Scope == scope)?.Count ?? 0));
        Scope = Scopes.FirstOrDefault(s => s.Scope == old?.Scope && s.FolderId == old.FolderId) ?? Scopes[0];
        _folderCursor = summary.Folders.NextCursor;
        var retained = _tags.Where(t => _filterIds.Contains(t.Item.Id) || Note?.TagIds.Contains(t.Item.Id) == true).ToArray();
        _tags.Clear(); _tags.AddRange(summary.Tags.Items);
        if (!summary.RequestedTags.IsDefault)
            foreach (var tag in summary.RequestedTags) { _tags.RemoveAll(t => t.Item.Id == tag.Item.Id); _tags.Add(tag); }
        foreach (var tag in retained) if (_tags.All(t => t.Item.Id != tag.Item.Id)) _tags.Add(tag);
        _tagCursor = summary.Tags.NextCursor; PublishTags();
        OnPropertyChanged(nameof(TagCatalogText));
        ManagedFolder = Folders.FirstOrDefault(f => f.Id == managedId);
        MoveFolder = Folders.FirstOrDefault(f => f.Id == moveId);
        OnPropertyChanged(nameof(ScopeName)); OnPropertyChanged(nameof(Breadcrumb));
    }
    private void AddFolder(NotebookCatalogItem folder)
    {
        var option = new NotebookFolderOption(folder.Item.Id, folder.Item.Name, folder.Item.Version);
        var scope = new NotebookScopeOption(NotebookScope.Folder, folder.Item.Id, folder.Item.Name, folder.ActiveNoteCount);
        int index = Folders.ToList().FindIndex(f => f.Id == folder.Item.Id);
        if (index < 0) Folders.Add(option); else Folders[index] = option;
        index = Scopes.ToList().FindIndex(s => s.FolderId == folder.Item.Id);
        if (index < 0) Scopes.Add(scope); else Scopes[index] = scope;
    }
    private Task LoadMoreFoldersAsync() => ReadAsync(async ct =>
    {
        var query = new NotebookCatalogQuery(NotebookCatalogKind.Folders, cursor: _folderCursor);
        var page = await Task.Run(() => _service.ListCatalogAsync(query, ct), ct);
        if (!Current(ct)) return;
        _publishing = true;
        var previous = Scope;
        try
        {
            foreach (var item in page.Items) AddFolder(item);
            Scope = Scopes.FirstOrDefault(s => s.Scope == previous?.Scope && s.FolderId == previous.FolderId);
        }
        finally { _publishing = false; }
        OnPropertyChanged(nameof(ScopeName)); OnPropertyChanged(nameof(Breadcrumb));
        _folderCursor = page.NextCursor; NotifyCommands();
    });
    private Task LoadMoreTagsAsync() => ReadAsync(async ct =>
    {
        var query = new NotebookCatalogQuery(NotebookCatalogKind.Tags, cursor: _tagCursor);
        var page = await Task.Run(() => _service.ListCatalogAsync(query, ct), ct);
        if (!Current(ct)) return;
        foreach (var item in page.Items) { _tags.RemoveAll(t => t.Item.Id == item.Item.Id); _tags.Add(item); }
        _tagCursor = page.NextCursor; PublishTags(); OnPropertyChanged(nameof(TagCatalogText)); NotifyCommands();
    });
    private async Task ReadDetailAsync(NotebookRow? row, CancellationToken ct)
    {
        if (!Current(ct)) return;
        ResetLinkPresentation(); _unavailable = null; Note = null; InstallDraft();
        ContentMessage = row is null ? "Choose a note to read its content." : "Loading note…";
        if (row is null) return;
        var result = await Task.Run(() => _service.LoadAsync(row.Id, ct), ct);
        if (!Current(ct) || SelectedNote?.Id != row.Id) return;
        _unavailable = result.UnavailableMetadata; Note = result.Note;
        InstallDraft();
        ContentMessage = result.Status switch
        {
            NotebookLoadStatus.Success => Note!.Document.Blocks.Count == 0 ? "This note has no content." : "",
            NotebookLoadStatus.NotFound => "This note is no longer available. Refresh the list.",
            NotebookLoadStatus.UnsupportedContent => "This content needs a newer application. Its stored original is preserved.",
            NotebookLoadStatus.TooLarge => "This content exceeds the preview limit. Its stored original is preserved.",
            _ => "This content cannot be read safely. Its stored original is preserved."
        };
        if (Note is null) return;
        MoveFolder = Folders.FirstOrDefault(f => f.Id == Note.FolderId);
        await ResolveCurrentLinksAsync(ct);
    }
    private Task WriteNoteAsync(NotebookCommand command) => MutateAsync(async ct =>
    {
        var result = await Task.Run(() => _service.ExecuteAsync(command, ct), ct);
        if (!Current(ct)) return;
        if (!PublishWrite(result.Status, result.Issues.IsDefault ? "" : string.Join(" ", result.Issues.Select(i => i.Message)))) return;
        await ReadPageAsync(false, result.Note!.Id, ct);
        if (Current(ct)) Message = "Note updated.";
    });
    private bool PublishWrite(NotebookWriteStatus status, string validation)
    {
        if (!_active || _disposed) return false;
        Message = status switch
        {
            NotebookWriteStatus.Success => "Saved. Refreshing stored data…",
            NotebookWriteStatus.Invalid => string.IsNullOrWhiteSpace(validation) ? "Check the entered value and try again." : validation,
            NotebookWriteStatus.Conflict => "This record changed elsewhere. Your input is retained. Preserve or discard your changes before refreshing the stored version.",
            NotebookWriteStatus.Cancelled => "Change cancelled.",
            NotebookWriteStatus.NotFound or NotebookWriteStatus.UnavailableTarget => "The selected record is no longer available. Refresh to continue.",
            _ => "The change could not be saved. Your input is retained; refresh or try again."
        };
        return status == NotebookWriteStatus.Success;
    }
    private Task MutateAsync(Func<CancellationToken, Task> work)
    {
        if (!CanMutate) return Task.CompletedTask;
        var owner = CancellationTokenSource.CreateLinkedTokenSource(_lifetime?.Token ?? CancellationToken.None); _write = owner; IsWriting = true; IsBusy = true;
        return Track(RunAsync());
        async Task RunAsync()
        {
            await Task.Yield();
            try { owner.Token.ThrowIfCancellationRequested(); await work(owner.Token); }
            catch (OperationCanceledException) { if (!_disposed) Message = "Change cancelled. Refresh to check stored data."; }
            catch (Exception) { if (!_disposed) Message = "The operation could not finish. Input is retained; Refresh to check stored data before trying again."; }
            finally { if (ReferenceEquals(_write, owner)) _write = null; owner.Dispose(); if (!_disposed) { IsWriting = false; IsBusy = false; } else { _writing = false; _busy = false; } }
        }
    }
    private void NotifyCommands()
    {
        OnPropertyChanged(nameof(HasFolderActions)); OnPropertyChanged(nameof(CanChangeFolder));
        NotifyLifecycleCommands();
        NotifyTagCommands();
        NotifyLinkCommands();
        RecoverCommand?.NotifyCanExecuteChanged(); DeleteRecoveryCommand?.NotifyCanExecuteChanged(); MoreRecoveryCommand?.NotifyCanExecuteChanged();
        SaveCommand?.NotifyCanExecuteChanged(); DiscardCommand?.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanEditDraft)); OnPropertyChanged(nameof(CanEditBody)); OnPropertyChanged(nameof(BodyReadOnly));
        OnPropertyChanged(nameof(HasMoreNotes)); OnPropertyChanged(nameof(HasMoreFolders)); OnPropertyChanged(nameof(HasMoreTags));
        foreach (var c in new[] { RefreshCommand, SearchCommand, MoreNotesCommand, MoreFoldersCommand, MoreTagsCommand, NewNoteCommand, CreateFolderCommand, RenameFolderCommand, DeleteFolderCommand, MoveCommand, PinCommand, CancelCommand })
            c?.NotifyCanExecuteChanged();
    }
}
