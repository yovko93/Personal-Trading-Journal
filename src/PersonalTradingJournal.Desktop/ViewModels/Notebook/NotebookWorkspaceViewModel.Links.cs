using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Desktop.ViewModels.Notebook;

public sealed record NotebookLinkOption(NotebookLinkTarget Target, bool IsLinked)
{
    public string Label => Target.Label;
    public string Context => Target.Context + (IsLinked ? " · Already linked" : "");
}
public sealed record NotebookLinkedItem(NotebookResolvedLink Resolved)
{
    public NotebookLink Link => Resolved.Link;
    public bool IsAvailable => Resolved.Availability == NotebookTargetAvailability.Available;
    public string Label => Resolved.DisplayLabel;
    public string Context => $"{(Link.Kind == NotebookTargetKind.Trade ? "Trade" : "Trading Setup")} · {Link.TargetId}" +
        (!IsAvailable ? " · Unavailable" : Resolved.IsInactive ? " · Inactive" : "");
}

public sealed partial class NotebookWorkspaceViewModel
{
    private CancellationTokenSource? _targetRead;
    private long _targetGeneration, _linkLabelGeneration;
    private bool _linkOperation, _targetBusy, _linkPickerOpen;
    private string _linkSearch = "", _linkMessage = "", _targetMessage = "";
    private string? _targetCursor;
    private NotebookTargetKind _targetKind = NotebookTargetKind.Trade;
    private NotebookLinkOption? _selectedTarget;
    public static NotebookTargetKind[] LinkTargetKinds { get; } = [NotebookTargetKind.Trade, NotebookTargetKind.TradingSetup];
    public static NotebookTargetKind TradeLinkKind => NotebookTargetKind.Trade;
    public static NotebookTargetKind SetupLinkKind => NotebookTargetKind.TradingSetup;
    public ObservableCollection<NotebookLinkOption> LinkTargets { get; } = [];
    public ObservableCollection<NotebookLinkedItem> LinkedItems { get; } = [];
    public Func<NotebookLink, CancellationToken, Task<bool>>? OpenLinkedTarget { get; set; }
    public Task LinkSearchTask { get; private set; } = Task.CompletedTask;
    public string LinkMessage { get => _linkMessage; private set => SetProperty(ref _linkMessage, value); }
    public string TargetMessage { get => _targetMessage; private set => SetProperty(ref _targetMessage, value); }
    public bool LinkPickerOpen { get => _linkPickerOpen; set { if (SetProperty(ref _linkPickerOpen, value)) { if (value) LinkSearchTask = SearchLinkTargetsAsync(false); else StopLinkTargetRead(); } } }
    public string LinkSearch { get => _linkSearch; set { if (SetProperty(ref _linkSearch, value) && LinkPickerOpen) LinkSearchTask = SearchLinkTargetsAsync(false); } }
    public NotebookTargetKind LinkTargetKind { get => _targetKind; set { if (Enum.IsDefined(value) && SetProperty(ref _targetKind, value) && LinkPickerOpen) LinkSearchTask = SearchLinkTargetsAsync(false); } }
    public NotebookLinkOption? SelectedLinkTarget { get => _selectedTarget; set { SetProperty(ref _selectedTarget, value); NotifyLinkCommands(); } }
    public bool HasMoreLinkTargets => _targetCursor is not null && !_targetBusy;
    public AsyncRelayCommand SearchLinkTargetsCommand { get; private set; } = null!;
    public AsyncRelayCommand MoreLinkTargetsCommand { get; private set; } = null!;
    public AsyncRelayCommand AddLinkCommand { get; private set; } = null!;
    public AsyncRelayCommand<NotebookLinkedItem> RemoveLinkedItemCommand { get; private set; } = null!;
    public AsyncRelayCommand<NotebookLinkedItem> OpenLinkedItemCommand { get; private set; } = null!;
    public AsyncRelayCommand RefreshLinkedItemsCommand { get; private set; } = null!;
    private bool CanChangeLinks => !_linkOperation && !_imageOperation && !_preparingImage && !_tagOperation &&
        _active && !_disposed && !_recoveryConflict && !_detachedRecovery && Note?.State == NotebookNoteState.Active && (CanMutate || IsSaving);
    private void InitializeLinks()
    {
        SearchLinkTargetsCommand = new(() => LinkSearchTask = SearchLinkTargetsAsync(false), () => _active && !_disposed && Note is not null,
            AsyncRelayCommandOptions.AllowConcurrentExecutions);
        MoreLinkTargetsCommand = new(() => LinkSearchTask = SearchLinkTargetsAsync(true), () => HasMoreLinkTargets && _active && !_disposed);
        AddLinkCommand = new(() => SelectedLinkTarget is { } choice ? ChangeLinkAsync(new(choice.Target.Kind, choice.Target.Id, choice.Target.Label), false) : Task.CompletedTask,
            () => CanChangeLinks && !_targetBusy && SelectedLinkTarget is { IsLinked: false } && Note!.Links.Length < NotebookLimits.Links);
        RemoveLinkedItemCommand = new(item => item is null ? Task.CompletedTask : ChangeLinkAsync(item.Link, true),
            item => CanChangeLinks && item is not null && Note!.Links.Any(x => SameTarget(x, item.Link)));
        OpenLinkedItemCommand = new(OpenLinkedItemAsync, item => !_linkOperation && _active && !_disposed && !IsBusy && item?.IsAvailable == true);
        RefreshLinkedItemsCommand = new(() => Track(ResolveCurrentLinksAsync(_lifetime?.Token ?? CancellationToken.None)),
            () => _active && !_disposed && Note is not null && !_linkOperation);
    }
    private static bool SameTarget(NotebookLink a, NotebookLink b) => a.Kind == b.Kind && a.TargetId == b.TargetId;
    private void NotifyLinkCommands()
    {
        SearchLinkTargetsCommand?.NotifyCanExecuteChanged(); MoreLinkTargetsCommand?.NotifyCanExecuteChanged();
        AddLinkCommand?.NotifyCanExecuteChanged(); RemoveLinkedItemCommand?.NotifyCanExecuteChanged();
        OpenLinkedItemCommand?.NotifyCanExecuteChanged(); RefreshLinkedItemsCommand?.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(HasMoreLinkTargets));
    }
    private void StopLinkTargetRead() { ++_targetGeneration; _targetRead?.Cancel(); _targetBusy = false; NotifyLinkCommands(); }
    private void ResetLinkPresentation()
    {
        StopLinkTargetRead(); ++_linkLabelGeneration; LinkPickerOpen = false;
        LinkedItems.Clear(); LinkTargets.Clear(); SelectedLinkTarget = null; _targetCursor = null;
        LinkMessage = ""; TargetMessage = ""; LinkText = ""; OnPropertyChanged(nameof(LinkText));
    }
    private Task SearchLinkTargetsAsync(bool more)
    {
        StopLinkTargetRead();
        if (!_active || _disposed || Note is not { } note || !LinkPickerOpen) return Task.CompletedTask;
        var generation = _targetGeneration;
        var owner = CancellationTokenSource.CreateLinkedTokenSource(_lifetime?.Token ?? CancellationToken.None); _targetRead = owner;
        var kind = LinkTargetKind; var search = LinkSearch; var cursor = more ? _targetCursor : null;
        if (!more) { LinkTargets.Clear(); SelectedLinkTarget = null; _targetCursor = null; }
        _targetBusy = true; TargetMessage = "Searching existing records…"; NotifyLinkCommands();
        return Track(Run());
        async Task Run()
        {
            try
            {
                var query = new NotebookTargetQuery(kind, search, cursor: cursor);
                var page = await Task.Run(() => _service.ListLinkTargetsAsync(query, owner.Token), owner.Token);
                if (!Owns()) return;
                foreach (var target in page.Items)
                    if (LinkTargets.All(x => x.Target.Kind != target.Kind || x.Target.Id != target.Id))
                        LinkTargets.Add(new(target, Note!.Links.Any(x => x.Kind == target.Kind && x.TargetId == target.Id)));
                _targetCursor = page.NextCursor;
                TargetMessage = page.TotalCount == 0 ? "No matching records. Try another search or target type." :
                    $"{LinkTargets.Count} of {page.TotalCount} records. Inactive records remain eligible; no records are created.";
            }
            catch (OperationCanceledException) { }
            catch (ArgumentException) { if (Owns()) TargetMessage = "Use search text of at most 256 characters, then search again."; }
            catch (Exception) { if (Owns()) TargetMessage = "Targets could not be loaded. Search again to retry; existing links are retained."; }
            finally
            {
                if (generation == _targetGeneration) { _targetBusy = false; NotifyLinkCommands(); }
                if (ReferenceEquals(_targetRead, owner)) _targetRead = null;
                owner.Dispose();
            }
            bool Owns() => generation == _targetGeneration && Current(owner.Token) && Note?.Id == note.Id && LinkPickerOpen;
        }
    }
    private async Task ResolveCurrentLinksAsync(CancellationToken ct)
    {
        var generation = ++_linkLabelGeneration;
        var note = Note;
        if (note is null) return;
        try
        {
            var links = await Task.Run(() => _service.ResolveLinksAsync(note.Links, ct), ct);
            if (!Current(ct) || generation != _linkLabelGeneration || Note?.Id != note.Id || Note.Version != note.Version) return;
            LinkedItems.Clear(); foreach (var item in links) LinkedItems.Add(new(item));
            LinkText = string.Join("\n", LinkedItems.Select(x => x.Context + " · " + x.Label)); OnPropertyChanged(nameof(LinkText));
            LinkMessage = "";
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            if (!Current(ct) || generation != _linkLabelGeneration || Note?.Id != note.Id) return;
            // Failed resolution cannot erase a persisted reference or pretend it is safe to open.
            LinkedItems.Clear(); foreach (var link in note.Links) LinkedItems.Add(new(new(link, NotebookTargetAvailability.Unavailable, link.LastKnownLabel)));
            LinkMessage = "Current labels could not be resolved. References are retained; refresh linked items to retry.";
        }
    }
    private async Task ChangeLinkAsync(NotebookLink link, bool remove)
    {
        if (!CanChangeLinks || Note is not { } intended) return;
        var activation = _lifetime;
        _linkOperation = true; NotifyCommands(); StopSearch(); StopLinkTargetRead();
        try
        {
            await GuardedAsync(async () =>
            {
                if (!ReferenceEquals(activation, _lifetime) || Note?.Id != intended.Id || _recoveryConflict) return;
                await MutateAsync(async ct =>
                {
                    var before = Note!;
                    if (remove ? !before.Links.Any(x => SameTarget(x, link)) : before.Links.Any(x => SameTarget(x, link))) return;
                    NotebookCommand command = remove ? new RemoveNotebookLink(before.Id, before.Version, link.Kind, link.TargetId) : new AddNotebookLink(before.Id, before.Version, link);
                    var result = await Task.Run(() => _service.ExecuteAsync(command, ct), ct);
                    if (!_active || _disposed || !ReferenceEquals(activation, _lifetime) || Note?.Id != intended.Id) return;
                    if (!PublishWrite(result.Status, ""))
                    {
                        LinkMessage = Message;
                        if (result.Status == NotebookWriteStatus.Conflict) _autosavePaused = true;
                        return;
                    }
                    var expected = remove ? before.Links.Where(x => !SameTarget(x, link)) : before.Links.Append(link);
                    if (result.Note is not { } saved || saved.Id != before.Id || saved.Version != before.Version + 1 ||
                        Signature(saved.Title, saved.Document) != _savedSignature || !saved.Images.SequenceEqual(before.Images) ||
                        !saved.Links.OrderBy(x => x.Kind).ThenBy(x => x.TargetId).SequenceEqual(expected.OrderBy(x => x.Kind).ThenBy(x => x.TargetId)))
                    { _autosavePaused = true; LinkMessage = "The stored link change could not be verified. Preserve your draft and refresh before retrying."; return; }
                    Note = saved; // No InstallDraft: preserve document, caret, history and recovery references.
                    await ResolveCurrentLinksAsync(ct);
                    await ReadPageAsync(false, saved.Id, ct, keepEditor: true);
                    if (Current(ct)) { Message = remove ? "Link removed. The target is unchanged." : "Record linked."; DraftChanged(); }
                });
            }, linkOperation: true);
        }
        finally
        {
            _linkOperation = false;
            var selected = SelectedLinkTarget?.Target;
            for (int i = 0; i < LinkTargets.Count; i++) LinkTargets[i] = LinkTargets[i] with { IsLinked = Note?.Links.Any(x => x.Kind == LinkTargets[i].Target.Kind && x.TargetId == LinkTargets[i].Target.Id) == true };
            SelectedLinkTarget = LinkTargets.FirstOrDefault(x => x.Target == selected); NotifyCommands();
        }
    }
    private async Task OpenLinkedItemAsync(NotebookLinkedItem? item)
    {
        if (item is null || !item.IsAvailable || OpenLinkedTarget is null || Note is not { } note) return;
        var activation = _lifetime;
        var ct = activation?.Token ?? CancellationToken.None;
        try
        {
            // Re-resolve identity immediately before navigation. Never substitute a similarly named row.
            var resolved = await _service.ResolveLinksAsync([item.Link], ct);
            if (!Current(ct) || Note?.Id != note.Id || !ReferenceEquals(activation, _lifetime) || !Note.Links.Any(x => SameTarget(x, item.Link))) return;
            if (resolved.Length != 1 || resolved[0].Availability != NotebookTargetAvailability.Available)
            { await ResolveCurrentLinksAsync(ct); LinkMessage = "The linked record is unavailable. Its reference is retained and can be removed."; return; }
            if (!await OpenLinkedTarget(item.Link, ct) && Current(ct)) LinkMessage = "The record could not be opened. Your Notebook context is retained.";
        }
        catch (OperationCanceledException) { }
        catch (Exception) { if (Current(ct)) LinkMessage = "The record could not be opened. Your Notebook context is retained."; }
    }
    internal void ReportLinkedNavigationFailure()
    { if (_active && !_disposed) LinkMessage = "The linked record is unavailable or could not be opened. Your Notebook context and reference are retained."; }
}
