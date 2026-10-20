using System.Collections.Immutable;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

internal sealed class NotebookFake : INotebookService
{
    internal readonly List<NotebookNoteSnapshot> Notes = [];
    internal readonly List<NotebookNamedItem> Folders = [];
    internal readonly List<NotebookNamedItem> Tags = [];
    internal readonly List<NotebookQuery> Queries = [];
    internal int Loads, Writes;
    internal NotebookWriteStatus NextWrite = NotebookWriteStatus.Success;
    internal Func<NotebookQuery, CancellationToken, Task<NotebookPage>>? OnList;
    internal Func<Guid, CancellationToken, Task<NotebookLoadResult>>? OnLoad;
    internal Func<NotebookCommand, CancellationToken, Task<NotebookWriteResult>>? OnWrite;
    internal static NotebookNoteSnapshot Note(string title = "Liquidity sweep", Guid? folder = null, NotebookNoteState state = NotebookNoteState.Active)
    {
        var doc = new NotebookDocument([
            NotebookNode.Heading(Guid.NewGuid(), 2, [NotebookNode.Run(Guid.NewGuid(), "Observation")]),
            NotebookNode.Paragraph(Guid.NewGuid(), [NotebookNode.Run(Guid.NewGuid(), "Synthetic research: wait for confirmation before forming a conclusion.")]),
            NotebookNode.List(Guid.NewGuid(), NotebookNodeKind.Checklist, [
                NotebookNode.Item(Guid.NewGuid(), [NotebookNode.Paragraph(Guid.NewGuid(), [NotebookNode.Run(Guid.NewGuid(), "Review recorded context")])], true)]),
            NotebookNode.Image(Guid.NewGuid(), Guid.NewGuid(), "Illustrative chart caption", "Synthetic price observation")]);
        return new(Guid.NewGuid(), title, doc, folder, state, false, null, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [], [], []);
    }
    public Task<NotebookPage> ListAsync(NotebookQuery q, CancellationToken ct = default)
    {
        Queries.Add(q);
        return OnList?.Invoke(q, ct) ?? Task.FromResult(Page(q));
    }
    internal NotebookPage Page(NotebookQuery q)
    {
        var active = Notes.Where(n => n.State == NotebookNoteState.Active);
        var filtered = q.Scope switch
        {
            NotebookScope.Pinned => active.Where(n => n.IsPinned),
            NotebookScope.Folder => active.Where(n => n.FolderId == q.FolderId),
            NotebookScope.Unfiled => active.Where(n => n.FolderId is null),
            NotebookScope.Archived => Notes.Where(n => n.State == NotebookNoteState.Archived),
            NotebookScope.Trash => Notes.Where(n => n.State == NotebookNoteState.Trashed),
            _ => active
        };
        filtered = filtered.Where(n => n.Title.Contains(q.Search, StringComparison.OrdinalIgnoreCase));
        filtered = q.Sort == NotebookSort.Title ? filtered.OrderBy(n => n.Title).ThenBy(n => n.Id) : filtered.OrderByDescending(n => n.UpdatedAtUtc).ThenBy(n => n.Id);
        var all = filtered.ToArray(); int start = q.Cursor is null ? 0 : int.Parse(q.Cursor);
        return new([.. all.Skip(start).Take(q.Limit).Select(Row)], all.Length, start + q.Limit < all.Length ? (start + q.Limit).ToString() : null,
            new([new(NotebookScope.AllNotes, null, active.Count()), new(NotebookScope.Pinned, null, active.Count(n => n.IsPinned)),
                new(NotebookScope.Unfiled, null, active.Count(n => n.FolderId is null)), new(NotebookScope.Archived, null, Notes.Count(n => n.State == NotebookNoteState.Archived)), new(NotebookScope.Trash, null, Notes.Count(n => n.State == NotebookNoteState.Trashed))],
                Catalog(new(NotebookCatalogKind.Folders)), Catalog(new(NotebookCatalogKind.Tags))));
    }
    internal static NotebookListItem Row(NotebookNoteSnapshot n) => new(n.Id, n.Title, n.Document.PlainText[..Math.Min(100, n.Document.PlainText.Length)], n.State, n.IsPinned, n.FolderId, n.Version, n.UpdatedAtUtc, []);
    private NotebookCatalogPage Catalog(NotebookCatalogQuery q)
    {
        int start = q.Cursor is null ? 0 : int.Parse(q.Cursor);
        var source = q.Kind == NotebookCatalogKind.Folders ? Folders : Tags;
        return new([.. source.Skip(start).Take(q.Limit).Select(f => new NotebookCatalogItem(f, Notes.Count(n => n.FolderId == f.Id)))],
            source.Count, start + q.Limit < source.Count ? (start + q.Limit).ToString() : null);
    }
    public Task<NotebookCatalogPage> ListCatalogAsync(NotebookCatalogQuery q, CancellationToken ct = default) => Task.FromResult(Catalog(q));
    public Task<NotebookLoadResult> LoadAsync(Guid id, CancellationToken ct = default)
    { Loads++; return OnLoad?.Invoke(id, ct) ?? Task.FromResult(Notes.FirstOrDefault(n => n.Id == id) is { } n ? new NotebookLoadResult(NotebookLoadStatus.Success, n) : new(NotebookLoadStatus.NotFound)); }
    public Task<NotebookWriteResult> ExecuteAsync(NotebookCommand c, CancellationToken ct = default)
    {
        Writes++;
        if (OnWrite is not null) return OnWrite(c, ct);
        if (NextWrite != NotebookWriteStatus.Success) return Task.FromResult(new NotebookWriteResult(NextWrite));
        NotebookNoteSnapshot n;
        if (c is CreateNotebookNote create) { n = Note(create.Title, create.FolderId) with { Document = NotebookDocument.Empty }; Notes.Add(n); }
        else
        {
            var id = c is SaveNotebookDraft d ? d.NoteId : c is MoveNotebookNote m ? m.NoteId : ((SetNotebookPin)c).NoteId;
            int index = Notes.FindIndex(n => n.Id == id); n = Notes[index];
            n = c is SaveNotebookDraft draft ? n with { Title = draft.Title.Trim(), Document = draft.Document, Version = n.Version + 1 } :
                c is MoveNotebookNote move ? n with { FolderId = move.FolderId, Version = n.Version + 1 } : n with { IsPinned = ((SetNotebookPin)c).IsPinned, Version = n.Version + 1 };
            Notes[index] = n;
        }
        return Task.FromResult(new NotebookWriteResult(NotebookWriteStatus.Success, n));
    }
    public Task<NotebookCatalogResult> ExecuteCatalogAsync(NotebookCatalogCommand c, CancellationToken ct = default)
    {
        Writes++;
        if (NextWrite != NotebookWriteStatus.Success) return Task.FromResult(new NotebookCatalogResult(NextWrite));
        if (c is CreateNotebookFolder create)
        { var f = new NotebookNamedItem(Guid.NewGuid(), create.Name, 1); Folders.Add(f); return Task.FromResult(new NotebookCatalogResult(NotebookWriteStatus.Success, f)); }
        if (c is RenameNotebookFolder rename)
        { int i = Folders.FindIndex(f => f.Id == rename.FolderId); Folders[i] = Folders[i] with { Name = rename.Name, Version = Folders[i].Version + 1 }; return Task.FromResult(new NotebookCatalogResult(NotebookWriteStatus.Success, Folders[i])); }
        var delete = (DeleteNotebookFolder)c; Folders.RemoveAll(f => f.Id == delete.FolderId);
        for (int i = 0; i < Notes.Count; i++) if (Notes[i].FolderId == delete.FolderId) Notes[i] = Notes[i] with { FolderId = null, Version = Notes[i].Version + 1 };
        return Task.FromResult(new NotebookCatalogResult(NotebookWriteStatus.Success, DeletedItemId: delete.FolderId));
    }
    public Task<ImmutableArray<NotebookResolvedLink>> ResolveLinksAsync(ImmutableArray<NotebookLink> links, CancellationToken ct = default) =>
        Task.FromResult<ImmutableArray<NotebookResolvedLink>>([.. links.Select(l => new NotebookResolvedLink(l, NotebookTargetAvailability.Unavailable, l.LastKnownLabel))]);
}

public sealed class NotebookWorkspaceTests
{
    [Fact]
    public async Task FailedScopeLoadKeepsPreviouslyAppliedListLabelAndPreventsWrongScopeSelection()
    {
        var data = new NotebookFake(); data.Notes.Add(NotebookFake.Note());
        using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService()); await vm.ActivateAsync();
        var row = vm.Notes[0]; data.OnList = (_, _) => throw new IOException("synthetic");
        vm.Scope = vm.Scopes.Single(s => s.Scope == NotebookScope.Trash); await vm.LoadTask;
        Assert.Equal("All Notes", vm.ScopeName); Assert.Single(vm.Notes); Assert.False(vm.CanSelectNote);
        vm.SelectedNote = row; Assert.Null(vm.SelectedNote); Assert.Equal(0, data.Loads);
        data.OnList = null; await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal("Trash", vm.ScopeName); Assert.Empty(vm.Notes);
    }
    [Fact]
    public async Task FolderAndTagContinuationKeepsLaterFolderIdentityAndRenameVersion()
    {
        var data = new NotebookFake();
        data.Folders.AddRange(Enumerable.Range(0, 55).Select(i => new NotebookNamedItem(Guid.NewGuid(), $"Folder {i:000}", 3)));
        data.Tags.AddRange(Enumerable.Range(0, 55).Select(i => new NotebookNamedItem(Guid.NewGuid(), $"Tag {i:000}", 1)));
        using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService()); await vm.ActivateAsync();
        Assert.True(vm.HasMoreFolders); Assert.True(vm.HasMoreTags);
        await vm.MoreFoldersCommand.ExecuteAsync(null); await vm.MoreTagsCommand.ExecuteAsync(null);
        Assert.Equal(56, vm.Folders.Count); Assert.Contains("Tag 054", vm.TagCatalogText);
        Assert.False(vm.HasMoreFolders); Assert.False(vm.HasMoreTags);
        var id = data.Folders[^1].Id; vm.Scope = vm.Scopes.Single(s => s.FolderId == id); await vm.LoadTask;
        Assert.Equal(id, vm.Scope!.FolderId); vm.ManagedFolder = vm.Folders.Single(f => f.Id == id);
        Assert.Equal(3, vm.ManagedFolder.Version);
        vm.FolderName = "Renamed later folder"; await vm.RenameFolderCommand.ExecuteAsync(null);
        Assert.Equal("Renamed later folder", vm.ScopeName); Assert.Equal(4, vm.Folders.Single(f => f.Id == id).Version);
    }
    [Fact]
    public async Task NewScopeWinsOverAnOlderListAndReadFailureHasWorkingRetry()
    {
        var data = new NotebookFake(); data.Notes.Add(NotebookFake.Note());
        using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService()); await vm.ActivateAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<NotebookPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        data.OnList = (q, _) => q.Scope == NotebookScope.AllNotes ? Wait() : Task.FromResult(data.Page(q));
        Task<NotebookPage> Wait() { entered.TrySetResult(); return pending.Task; }
        var old = vm.RefreshCommand.ExecuteAsync(null);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            vm.Scope = vm.Scopes.Single(s => s.Scope == NotebookScope.Trash); await vm.LoadTask;
            pending.SetResult(data.Page(new())); await old;
            Assert.Equal(NotebookScope.Trash, vm.Scope!.Scope); Assert.Empty(vm.Notes); Assert.False(vm.IsBusy);
            data.OnList = (_, _) => throw new IOException("sensitive path");
            await vm.RefreshCommand.ExecuteAsync(null); Assert.Contains("Refresh", vm.Message); Assert.DoesNotContain("sensitive", vm.Message);
            data.OnList = null; await vm.RefreshCommand.ExecuteAsync(null); Assert.Equal("", vm.Message);
        }
        finally { pending.TrySetResult(data.Page(new())); await old.WaitAsync(TimeSpan.FromSeconds(10)); await vm.CancelAsync(); }
    }
    [Fact]
    public async Task MoveRemovesNoteFromFolderViewAndKeepsItReadableInUnfiled()
    {
        var data = new NotebookFake(); var folder = new NotebookNamedItem(Guid.NewGuid(), "Research", 1);
        data.Folders.Add(folder); var note = NotebookFake.Note(folder: folder.Id); data.Notes.Add(note);
        using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService()); await vm.ActivateAsync();
        vm.Scope = vm.Scopes.Single(s => s.FolderId == folder.Id); await vm.LoadTask;
        vm.SelectedNote = vm.Notes[0]; await vm.LoadTask;
        vm.MoveFolder = vm.Folders.Single(f => f.Id is null); await vm.MoveCommand.ExecuteAsync(null);
        Assert.Empty(vm.Notes); Assert.Null(vm.SelectedNote); Assert.Null(vm.Note);
        vm.Scope = vm.Scopes.Single(s => s.Scope == NotebookScope.Unfiled); await vm.LoadTask;
        vm.SelectedNote = vm.Notes[0]; await vm.LoadTask; Assert.Equal(note.Id, vm.Note!.Id);
    }
    [Fact]
    public async Task ActivationPagingSelectionSearchAndRepeatedActivationUseBoundedMetadata()
    {
        var data = new NotebookFake(); data.Notes.AddRange(Enumerable.Range(0, 101).Select(i => NotebookFake.Note($"Note {i:000}")));
        using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService());
        var activation = vm.ActivateAsync(); Assert.Same(activation, vm.ActivateAsync());
        await activation; Assert.Equal(50, vm.Notes.Count); Assert.Equal(0, data.Loads);
        await vm.MoreNotesCommand.ExecuteAsync(null); await vm.MoreNotesCommand.ExecuteAsync(null);
        Assert.Equal(101, vm.Notes.Count); Assert.False(vm.MoreNotesCommand.CanExecute(null)); Assert.All(data.Queries, q => Assert.Equal(50, q.Limit));
        vm.SelectedNote = vm.Notes[70]; await vm.LoadTask;
        Assert.Equal(vm.SelectedNote.Id, vm.Note!.Id); Assert.Equal(1, data.Loads); Assert.Equal(0, data.Writes);
        vm.Sort = NotebookWorkspaceViewModel.SortOptions[1]; await vm.LoadTask; Assert.Equal("Note 000", vm.Notes[0].Title);
        vm.SearchText = "099"; await vm.SearchCommand.ExecuteAsync(null); Assert.Single(vm.Notes);
        Assert.Contains("1 of 1", vm.ListSummary); Assert.Equal(101, vm.Scopes[0].Count);
        vm.Deactivate(); await vm.DeactivationTask; await vm.ActivateAsync(); Assert.Single(vm.Notes);
    }
    [Fact]
    public async Task FolderOperationsCreationPinMoveAndDeleteRefreshWithoutDeletingNotes()
    {
        var data = new NotebookFake(); var dialogs = new FakeDialogService { ConfirmationResult = true };
        using var vm = new NotebookWorkspaceViewModel(data, dialogs); await vm.ActivateAsync();
        vm.FolderName = "Research"; await vm.CreateFolderCommand.ExecuteAsync(null);
        vm.Scope = vm.Scopes.Single(s => s.FolderId.HasValue); await vm.LoadTask;
        await vm.NewNoteCommand.ExecuteAsync(null); Assert.Equal("Untitled", vm.Note!.Title); Assert.Equal(vm.Scope.FolderId, vm.Note.FolderId);
        await vm.PinCommand.ExecuteAsync(null); Assert.True(vm.Note!.IsPinned);
        vm.ManagedFolder = vm.Folders.Single(f => f.Id.HasValue); vm.FolderName = "Lessons";
        await vm.RenameFolderCommand.ExecuteAsync(null); Assert.Equal("Lessons", vm.ScopeName);
        dialogs.ConfirmationResult = false; int writes = data.Writes; await vm.DeleteFolderCommand.ExecuteAsync(null); Assert.Equal(writes, data.Writes);
        dialogs.ConfirmationResult = true; await vm.DeleteFolderCommand.ExecuteAsync(null);
        Assert.Single(data.Notes); Assert.Null(data.Notes[0].FolderId); Assert.Equal(NotebookScope.Unfiled, vm.Scope!.Scope);
        Assert.True(vm.Note!.IsPinned); Assert.Equal("", vm.FolderName);
    }
    [Theory]
    [InlineData(NotebookScope.Archived)]
    [InlineData(NotebookScope.Trash)]
    [InlineData(NotebookScope.Pinned)]
    public async Task CreateFromNonFolderScopeSelectsActiveUntitledAndClearsSearch(NotebookScope scope)
    {
        var data = new NotebookFake(); using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService());
        await vm.ActivateAsync(); vm.Scope = vm.Scopes.Single(s => s.Scope == scope); await vm.LoadTask;
        vm.SearchText = "absent"; await vm.NewNoteCommand.ExecuteAsync(null);
        Assert.Equal(NotebookScope.AllNotes, vm.Scope!.Scope); Assert.Equal("", vm.SearchText);
        Assert.Equal(NotebookNoteState.Active, vm.Note!.State); Assert.Null(vm.Note.FolderId); Assert.Single(vm.Notes);
    }
    [Theory]
    [InlineData(NotebookWriteStatus.Invalid)]
    [InlineData(NotebookWriteStatus.Conflict)]
    [InlineData(NotebookWriteStatus.StorageFailure)]
    [InlineData(NotebookWriteStatus.Cancelled)]
    public async Task MutationFailuresKeepInputCountsAndSelection(NotebookWriteStatus status)
    {
        var data = new NotebookFake { NextWrite = status }; data.Notes.Add(NotebookFake.Note());
        using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService()); await vm.ActivateAsync();
        vm.SelectedNote = vm.Notes[0]; await vm.LoadTask; var selected = vm.Note;
        vm.FolderName = "Keep this text"; await vm.CreateFolderCommand.ExecuteAsync(null);
        Assert.Equal("Keep this text", vm.FolderName); Assert.Same(selected, vm.Note); Assert.Single(vm.Notes);
        Assert.NotEmpty(vm.Message); Assert.False(vm.IsBusy); Assert.False(vm.IsWriting);
    }
    [Theory]
    [InlineData(NotebookLoadStatus.InvalidContent)]
    [InlineData(NotebookLoadStatus.UnsupportedContent)]
    [InlineData(NotebookLoadStatus.TooLarge)]
    [InlineData(NotebookLoadStatus.NotFound)]
    public async Task UnreadableContentRetainsListMetadataAndNeverWrites(NotebookLoadStatus status)
    {
        var data = new NotebookFake(); data.Notes.Add(NotebookFake.Note());
        data.OnLoad = (_, _) => Task.FromResult(new NotebookLoadResult(status, OriginalContent: "opaque"));
        using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService()); await vm.ActivateAsync();
        vm.SelectedNote = vm.Notes[0]; await vm.LoadTask;
        Assert.Null(vm.Note); Assert.Equal("Liquidity sweep", vm.NoteTitle); Assert.NotEmpty(vm.ContentMessage); Assert.Equal(0, data.Writes);
    }
    [Fact]
    public async Task SupersededDetailAndScopeCannotPublishEvenIfServiceIgnoresCancellation()
    {
        var data = new NotebookFake(); data.Notes.Add(NotebookFake.Note("First")); data.Notes.Add(NotebookFake.Note("Second"));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<NotebookLoadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        data.OnLoad = (id, _) => { if (id != data.Notes[0].Id) return Task.FromResult(new NotebookLoadResult(NotebookLoadStatus.Success, data.Notes[1])); entered.TrySetResult(); return release.Task; };
        using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService()); await vm.ActivateAsync();
        Task old = Task.CompletedTask;
        try
        {
            vm.SelectedNote = vm.Notes.Single(n => n.Id == data.Notes[0].Id); old = vm.LoadTask; await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            vm.SelectedNote = vm.Notes.Single(n => n.Id == data.Notes[1].Id); await vm.LoadTask;
            release.SetResult(new(NotebookLoadStatus.Success, data.Notes[0])); await old;
            Assert.Equal(data.Notes[1].Id, vm.Note!.Id);
        }
        finally { release.TrySetResult(new(NotebookLoadStatus.NotFound)); await old.WaitAsync(TimeSpan.FromSeconds(10)); await vm.CancelAsync(); }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAndDisposalDrainInitialLoadAndPreventLatePublication(bool dispose)
    {
        var data = new NotebookFake(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<NotebookPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        data.OnList = (_, _) => { entered.TrySetResult(); return release.Task; };
        using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService()); Task load = vm.ActivateAsync();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); Task cancel;
            if (dispose) { vm.Dispose(); cancel = vm.DeactivationTask; } else cancel = vm.CancelAsync();
            Assert.False(cancel.IsCompleted); release.SetResult(data.Page(new())); await cancel; await load;
            Assert.Empty(vm.Scopes); Assert.False(vm.IsBusy); Assert.True(vm.CompletionTask.IsCompleted);
            if (!dispose) { data.OnList = null; await vm.RefreshCommand.ExecuteAsync(null); Assert.NotEmpty(vm.Scopes); }
        }
        finally { release.TrySetResult(data.Page(new())); await load.WaitAsync(TimeSpan.FromSeconds(10)); }
    }
    [Fact]
    public async Task PendingWriteBlocksDuplicateCreationAndLeaving()
    {
        var data = new NotebookFake(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<NotebookWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        data.OnWrite = (_, _) => { entered.TrySetResult(); return release.Task; };
        using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService()); await vm.ActivateAsync();
        var write = vm.NewNoteCommand.ExecuteAsync(null);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); Assert.False(vm.NewNoteCommand.CanExecute(null)); Assert.False(vm.TryLeave());
            await vm.NewNoteCommand.ExecuteAsync(null); Assert.Equal(1, data.Writes);
            release.SetResult(new(NotebookWriteStatus.Cancelled)); await write; Assert.True(vm.TryLeave()); Assert.False(vm.IsWriting);
        }
        finally { release.TrySetResult(new(NotebookWriteStatus.Cancelled)); await write.WaitAsync(TimeSpan.FromSeconds(10)); }
    }
}
