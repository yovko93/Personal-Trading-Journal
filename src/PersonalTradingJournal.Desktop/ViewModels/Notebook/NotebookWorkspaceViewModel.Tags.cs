using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Desktop.ViewModels.Notebook;

public sealed record NotebookTagOption(Guid Id, string Name, long Version, long Count, bool IsSelected, bool IsAssigned)
{
    public string FilterLabel => $"{Name} · {Count}";
    public string PickerLabel => Name + (IsAssigned ? " · already assigned" : "");
}

public sealed partial class NotebookWorkspaceViewModel
{
    private readonly HashSet<Guid> _filterIds = [];
    private CancellationTokenSource? _searchDelay;
    private string _tagName = "", _tagMessage = "";
    private NotebookTagOption? _managedTag, _assignmentTag;
    private bool _tagOperation;
    public ObservableCollection<NotebookTagOption> Tags { get; } = [];
    public ObservableCollection<NotebookTagOption> SelectedTagFilters { get; } = [];
    public ObservableCollection<NotebookNamedItem> AssignedTags { get; } = [];
    public string TagName { get => _tagName; set { if (SetProperty(ref _tagName, value)) { if (_tagOperation) ++_tagFormGeneration; OnPropertyChanged(nameof(HasTagName)); } } }
    public string TagMessage { get => _tagMessage; private set => SetProperty(ref _tagMessage, value); }
    public NotebookTagOption? ManagedTag { get => _managedTag; set { SetProperty(ref _managedTag, value); NotifyTagCommands(); } }
    public NotebookTagOption? AssignmentTag { get => _assignmentTag; set { SetProperty(ref _assignmentTag, value); NotifyTagCommands(); } }
    public string FilterSummary => !ScopeMatchesList ? "Filters not applied. Previous results remain visible." :
        _query.Search.Length == 0 && _query.TagIds.Length == 0 ? "" : "Matching saved title/body and every selected tag.";
    public Task SearchTask { get; private set; } = Task.CompletedTask;
    public AsyncRelayCommand<NotebookTagOption> ToggleTagFilterCommand { get; private set; } = null!;
    public AsyncRelayCommand ClearFiltersCommand { get; private set; } = null!;
    public AsyncRelayCommand CreateTagCommand { get; private set; } = null!;
    public AsyncRelayCommand RenameTagCommand { get; private set; } = null!;
    public AsyncRelayCommand DeleteTagCommand { get; private set; } = null!;
    public AsyncRelayCommand AssignTagCommand { get; private set; } = null!;
    public AsyncRelayCommand<NotebookNamedItem> RemoveTagCommand { get; private set; } = null!;
    private bool CanChangeTags => !_tagOperation && _active && !_disposed && (CanMutate || IsSaving);
    private bool CanAssignTags => CanChangeTags && !_recoveryConflict && !_detachedRecovery && Note?.State == NotebookNoteState.Active;
    private void InitializeTags()
    {
        ToggleTagFilterCommand = new(ToggleTagFilterAsync, t => t is not null && CanRead);
        ClearFiltersCommand = new(ClearFiltersAsync, () => CanRead);
        CreateTagCommand = new(() => CatalogTagAsync(new CreateNotebookTag(TagName)), () => CanChangeTags);
        RenameTagCommand = new(() => (_tagFormTarget ?? ManagedTag) is { } t ? CatalogTagAsync(new RenameNotebookTag(t.Id, t.Version, TagName)) : Task.CompletedTask,
            () => CanChangeTags && ManagedTag is not null);
        DeleteTagCommand = new(DeleteTagAsync, () => CanChangeTags && ManagedTag is not null);
        AssignTagCommand = new(() => AssignmentTag is { } t ? ChangeAssignmentAsync(t.Id, remove: false) : Task.CompletedTask,
            () => CanAssignTags && AssignmentTag is { } t && Note!.TagIds.Length < NotebookLimits.Tags && !Note.TagIds.Contains(t.Id));
        RemoveTagCommand = new(t => t is null ? Task.CompletedTask : ChangeAssignmentAsync(t.Id, remove: true),
            t => CanAssignTags && t is not null && Note!.TagIds.Contains(t.Id));
    }
    private void NotifyTagCommands()
    {
        OnPropertyChanged(nameof(IsTagOperationPending)); OnPropertyChanged(nameof(CanManageTags));
        ToggleTagFilterCommand?.NotifyCanExecuteChanged(); ClearFiltersCommand?.NotifyCanExecuteChanged();
        CreateTagCommand?.NotifyCanExecuteChanged(); RenameTagCommand?.NotifyCanExecuteChanged(); DeleteTagCommand?.NotifyCanExecuteChanged();
        AssignTagCommand?.NotifyCanExecuteChanged(); RemoveTagCommand?.NotifyCanExecuteChanged();
    }
    private void PublishTags()
    {
        var managed = ManagedTag?.Id; var picker = AssignmentTag?.Id;
        var known = AssignedTags.Concat(SelectedNote?.Item.Tags ?? []).Concat(_tags.Select(t => t.Item))
            .GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.Last());
        Tags.Clear(); SelectedTagFilters.Clear();
        foreach (var tag in _tags.OrderBy(t => t.Item.Name, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.Item.Id))
        {
            var option = new NotebookTagOption(tag.Item.Id, tag.Item.Name, tag.Item.Version, tag.ActiveNoteCount,
                _filterIds.Contains(tag.Item.Id), Note?.TagIds.Contains(tag.Item.Id) == true);
            Tags.Add(option); if (option.IsSelected) SelectedTagFilters.Add(option);
        }
        AssignedTags.Clear();
        foreach (var id in Note?.TagIds ?? [])
            AssignedTags.Add(known.GetValueOrDefault(id) ?? new(id, "Unavailable tag", 0));
        ManagedTag = Tags.FirstOrDefault(t => t.Id == managed);
        AssignmentTag = Tags.FirstOrDefault(t => t.Id == picker);
        OnPropertyChanged(nameof(TagText)); OnPropertyChanged(nameof(TagCatalogText)); OnPropertyChanged(nameof(FilterSummary));
        OnPropertyChanged(nameof(CanSelectNote)); NotifyTagCommands();
    }
    private void StopSearch() => _searchDelay?.Cancel();
    private void ScheduleSearch()
    {
        StopSearch(); OnPropertyChanged(nameof(FilterSummary)); OnPropertyChanged(nameof(CanSelectNote)); NotifyCommands();
        if (!_active || _disposed || _lifetime?.IsCancellationRequested != false) return;
        var owner = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token); _searchDelay = owner;
        SearchTask = Track(Run());
        async Task Run()
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(300), _time, owner.Token);
                if (Current(owner.Token)) await ApplySearchAsync();
            }
            catch (OperationCanceledException) { }
            finally { if (ReferenceEquals(_searchDelay, owner)) _searchDelay = null; owner.Dispose(); }
        }
    }
    private async Task ApplySearchAsync()
    {
        StopSearch(); if (!CanRead) return;
        bool accepted = false;
        await GuardedAsync(async () => { accepted = true; await ReloadAsync(); });
        if (!accepted && _active && !_disposed) RestoreAppliedFilters();
    }
    private void RestoreAppliedFilters()
    {
        _publishing = true;
        try
        {
            SearchText = _query.Search;
            _filterIds.Clear(); foreach (var id in _query.TagIds) _filterIds.Add(id);
            Scope = Scopes.FirstOrDefault(s => s.Scope == _query.Scope && s.FolderId == _query.FolderId);
            Sort = SortOptions.Single(s => s.Value == _query.Sort);
        }
        finally { _publishing = false; }
        PublishTags(); NotifyCommands();
    }
    private async Task ToggleTagFilterAsync(NotebookTagOption? tag)
    {
        if (tag is null) return;
        StopSearch();
        await GuardedAsync(async () =>
        {
            if (!_filterIds.Remove(tag.Id))
            {
                if (_filterIds.Count >= NotebookLimits.Tags) { TagMessage = "Select at most 32 tag filters."; return; }
                _filterIds.Add(tag.Id);
            }
            PublishTags(); await ReloadAsync();
        });
        PublishTags();
    }
    private async Task ClearFiltersAsync()
    {
        StopSearch();
        await GuardedAsync(async () =>
        {
            _publishing = true; SearchText = ""; _publishing = false;
            _filterIds.Clear(); PublishTags(); await ReloadAsync();
        });
    }
    private Task DeleteTagAsync()
    {
        return DeleteGlobalTagAsync(ManagedTag);
    }
    public Task DeleteGlobalTagAsync(NotebookTagOption? tag)
    {
        if (!CanManageTags || tag is null || !Tags.Any(t => t.Id == tag.Id && t.Version == tag.Version)) return Task.CompletedTask;
        return !_dialogs.Confirm(new("Delete tag globally?",
            $"Delete “{tag.Name}” from every note, including Archived and Trash? Notes and their content remain. This is not just removing one assignment.",
            "Delete tag", isDestructive: true)) ? Task.CompletedTask : CatalogTagAsync(new DeleteNotebookTag(tag.Id, tag.Version, true));
    }
    private async Task CatalogTagAsync(NotebookCatalogCommand command)
    {
        if (!CanChangeTags) return;
        long form = _tagFormGeneration, selection = _selectionGeneration;
        var activation = _lifetime;
        bool OwnsContext() => _active && !_disposed && ReferenceEquals(activation, _lifetime) &&
            activation?.IsCancellationRequested == false && selection == _selectionGeneration;
        bool OwnsForm() => _active && !_disposed && ReferenceEquals(activation, _lifetime) &&
            activation?.IsCancellationRequested == false && form == _tagFormGeneration;
        bool OwnsInput() => OwnsContext() && OwnsForm();
        var name = command is CreateNotebookTag c ? c.Name : command is RenameNotebookTag r ? r.Name : null;
        if (name is not null)
        {
            try { NotebookLimits.Name(name, NotebookLimits.TagName); }
            catch (ArgumentException) { TagMessage = "Enter a tag name of 1–64 characters (after trimming)."; return; }
        }
        _tagOperation = true; NotifyTagCommands(); StopSearch();
        bool completed = false;
        try
        {
            await GuardedAsync(() => MutateAsync(async ct =>
            {
                if (!OwnsInput()) return;
                var before = Note;
                bool sourceConflict = false;
                var result = await Task.Run(() => _service.ExecuteCatalogAsync(command, ct), ct);
                if (!Current(ct)) return;
                if (!PublishWrite(result.Status, result.Issues.IsDefault ? "" : string.Join(" ", result.Issues.Select(i => i.Message))))
                { if (OwnsInput()) TagMessage = Message; return; }
                if (result.DeletedItemId is Guid deleted) { _filterIds.Remove(deleted); _tags.RemoveAll(t => t.Item.Id == deleted); }
                if (result.Item is { } item)
                {
                    _tags.RemoveAll(t => t.Item.Id == item.Id); _tags.Add(new(item, 0));
                    if (OwnsInput()) ManagedTag = new(item.Id, item.Name, item.Version, 0, _filterIds.Contains(item.Id), before?.TagIds.Contains(item.Id) == true);
                }
                // Catalog commits remain authoritative after dismissal, but must not replace a newer note context.
                if (!OwnsContext()) { PublishTags(); Message = "Tag catalog updated. Refresh to reload stored counts."; return; }
                // Only our confirmed deletion may advance the open note's baseline. Never adopt
                // an unrelated concurrent version just to make a later autosave succeed.
                if (before is not null && result.DeletedItemId is Guid removed && before.TagIds.Contains(removed))
                {
                    var current = await Task.Run(() => _service.LoadAsync(before.Id, ct), ct);
                    if (!Current(ct) || !OwnsContext()) return;
                    if (current.Note is { } n && n.Version == before.Version + 1 &&
                        Signature(n.Title, n.Document) == _savedSignature &&
                        n.TagIds.Order().SequenceEqual(before.TagIds.Where(id => id != removed).Order()))
                        Note = n;
                    else { sourceConflict = true; _autosavePaused = true; _recoveryConflict = true; TagMessage = "Tag deleted. The open note changed elsewhere; preserve your input and Refresh before editing its stored version."; }
                }
                await ReadPageAsync(false, before?.Id ?? SelectedNote?.Id, ct, keepEditor: true);
                if (Current(ct))
                {
                    completed = true; Message = "Tag catalog updated.";
                    // This write owns the list refresh: WPF can transiently clear/reselect the
                    // same item while Notes is republished. External selection is disabled during
                    // IsWriting; the input generation still protects dismissal and newer input.
                    if (OwnsForm())
                    {
                        TagName = ""; CloseTagForm();
                        if (!sourceConflict) TagMessage = "";
                    }
                }
            }), owns: OwnsInput);
            if (!completed && OwnsInput() && TagMessage.Length == 0)
                TagMessage = Message.Length == 0 ? "Tag change cancelled. Your input is retained." : Message;
        }
        finally { _tagOperation = false; NotifyTagCommands(); }
    }
    private async Task ChangeAssignmentAsync(Guid tagId, bool remove)
    {
        if (!CanAssignTags || Note is not { } target) return;
        _tagOperation = true; NotifyTagCommands(); StopSearch();
        try
        {
            await GuardedAsync(async () =>
            {
                if (Note?.Id != target.Id || _recoveryConflict) return;
                await MutateAsync(async ct =>
                {
                    var before = Note!;
                    if (remove ? !before.TagIds.Contains(tagId) : before.TagIds.Contains(tagId)) return;
                    NotebookCommand command = remove ? new RemoveNotebookTag(before.Id, before.Version, tagId) : new AssignNotebookTag(before.Id, before.Version, tagId);
                    var result = await Task.Run(() => _service.ExecuteAsync(command, ct), ct);
                    if (!Current(ct) || Note?.Id != target.Id) return;
                    if (!PublishWrite(result.Status, result.Issues.IsDefault ? "" : string.Join(" ", result.Issues.Select(i => i.Message))))
                    { TagMessage = Message; if (result.Status == NotebookWriteStatus.Conflict) _autosavePaused = true; return; }
                    if (result.Note is not { } saved || saved.Id != before.Id || saved.Version != before.Version + 1 ||
                        Signature(saved.Title, saved.Document) != _savedSignature)
                    { TagMessage = "The stored tag change could not be verified. Refresh before trying again."; _autosavePaused = true; return; }
                    Note = saved; // Content/caret/undo and editor generation stay untouched.
                    await ReadPageAsync(false, saved.Id, ct, keepEditor: true);
                    if (Current(ct)) { TagMessage = ""; Message = remove ? "Tag removed from this note." : "Tag assigned to this note."; DraftChanged(); }
                });
            });
        }
        finally { _tagOperation = false; NotifyTagCommands(); }
    }
}
