using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed class NotebookTagTests
{
    internal sealed class Fixture : IAsyncDisposable
    {
        internal readonly NotebookFake Data = new();
        internal readonly NotebookClock Clock = new();
        internal readonly NotebookRecoveryFake Recovery = new();
        internal readonly NotebookChoices Choice = new();
        internal readonly FakeDialogService Dialogs = new() { ConfirmationResult = true };
        internal NotebookWorkspaceViewModel Vm = null!;
        internal static async Task<Fixture> Open(int count = 1)
        {
            var f = new Fixture();
            f.Data.Tags.AddRange(new[] { new NotebookNamedItem(Guid.NewGuid(), "Research", 1), new NotebookNamedItem(Guid.NewGuid(), "Risk", 1) });
            for (int i = 0; i < count; i++) f.Data.Notes.Add(NotebookFake.Note($"Note {i:000}") with { Document = NotebookEditingTests.Text("Research observation"), TagIds = [f.Data.Tags[0].Id, f.Data.Tags[1].Id] });
            f.Vm = new(f.Data, f.Dialogs, f.Choice, new NotebookJsonCodec(), f.Recovery, f.Clock);
            await f.Vm.ActivateAsync(); f.Vm.SelectedNote = f.Vm.Notes[0]; await f.Vm.LoadTask; return f;
        }
        public async ValueTask DisposeAsync() { Vm.Dispose(); await Vm.DeactivationTask.WaitAsync(TimeSpan.FromSeconds(10)); Assert.False(Vm.IsBusy); }
    }
    [Fact]
    public async Task AndFiltersPageCorrectlyRenameRetainsIdentityDeleteRepairsAndClearRetainsScopeSort()
    {
        await using var f = await Fixture.Open(55); var vm = f.Vm;
        await vm.ToggleTagFilterCommand.ExecuteAsync(vm.Tags[0]);
        await vm.ToggleTagFilterCommand.ExecuteAsync(vm.Tags[1]);
        Assert.Equal(2, f.Data.Queries.Last().TagIds.Length); Assert.Equal(50, vm.Notes.Count);
        await vm.MoreNotesCommand.ExecuteAsync(null);
        Assert.Equal(55, vm.Notes.Count); Assert.Contains("55 of 55", vm.ListSummary);
        var id = vm.Tags[0].Id; vm.ManagedTag = vm.Tags[0]; vm.TagName = "Changed";
        await vm.RenameTagCommand.ExecuteAsync(null);
        Assert.Contains(vm.SelectedTagFilters, t => t.Id == id && t.Name == "Changed"); Assert.Equal(50, vm.Notes.Count);
        await vm.DeleteTagCommand.ExecuteAsync(null);
        Assert.DoesNotContain(vm.SelectedTagFilters, t => t.Id == id);
        Assert.Single(f.Data.Queries.Last().TagIds); Assert.Equal(55, f.Data.Notes.Count);
        vm.Scope = vm.Scopes.Single(s => s.Scope == NotebookScope.Unfiled); await vm.LoadTask;
        vm.Sort = NotebookWorkspaceViewModel.SortOptions[1]; await vm.LoadTask;
        vm.SearchText = "Note"; await vm.SearchCommand.ExecuteAsync(null);
        await vm.ClearFiltersCommand.ExecuteAsync(null);
        Assert.Empty(f.Data.Queries.Last().TagIds); Assert.Equal("", vm.SearchText);
        Assert.Equal(NotebookScope.Unfiled, vm.Scope!.Scope); Assert.Equal(NotebookSort.Title, vm.Sort.Value);
    }
    [Fact]
    public async Task DirtyAssignmentFlushesAndPreservesEditorThenGlobalDeleteAdvancesBaseline()
    {
        await using var f = await Fixture.Open(); var vm = f.Vm;
        vm.DraftTitle = "Keep title"; vm.SetDraftDocument(NotebookEditingTests.Text("Keep body"));
        var editor = vm.EditorRevision; var version = vm.Note!.Version;
        await vm.RemoveTagCommand.ExecuteAsync(vm.AssignedTags[0]);
        Assert.Equal("Keep title", vm.DraftTitle); Assert.Equal(NotebookEditingTests.Text("Keep body").PlainText, vm.DraftDocument.PlainText);
        Assert.Equal(editor, vm.EditorRevision); Assert.False(vm.IsDirty); Assert.Single(vm.AssignedTags);
        Assert.Equal(version + 2, vm.Note!.Version);
        vm.AssignmentTag = vm.Tags.Single(t => !t.IsAssigned); Assert.True(vm.AssignTagCommand.CanExecute(null));
        await vm.AssignTagCommand.ExecuteAsync(null);
        Assert.False(vm.AssignTagCommand.CanExecute(null)); Assert.Equal(2, vm.AssignedTags.Count);
        vm.ManagedTag = vm.AssignmentTag; var assignedVersion = vm.Note.Version;
        await vm.DeleteTagCommand.ExecuteAsync(null);
        Assert.Equal(assignedVersion + 1, vm.Note.Version); Assert.Single(vm.AssignedTags);
        vm.DraftTitle = "After deletion"; f.Clock.Advance(750); await vm.AutosaveTask;
        Assert.Equal("Saved", vm.SaveState); Assert.Single(f.Data.Notes[0].TagIds);
    }
    [Fact]
    public async Task MetadataWaitsForEnteredAutosaveAndUsesReturnedVersionWithoutLosingNewerTyping()
    {
        await using var f = await Fixture.Open(); var vm = f.Vm;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writes = new List<NotebookCommand>();
        f.Data.OnWrite = async (c, _) =>
        {
            writes.Add(c);
            if (writes.Count == 1) { entered.TrySetResult(); await release.Task; }
            var n = f.Data.Notes[0];
            if (c is SaveNotebookDraft d) { Assert.Equal(n.Version, d.ExpectedVersion); n = n with { Title = d.Title, Document = d.Document, Version = n.Version + 1 }; }
            else { var r = Assert.IsType<RemoveNotebookTag>(c); Assert.Equal(n.Version, r.ExpectedVersion); n = n with { TagIds = n.TagIds.Remove(r.TagId), Version = n.Version + 1 }; }
            f.Data.Notes[0] = n; return new(NotebookWriteStatus.Success, n);
        };
        Task? remove = null;
        try
        {
            vm.DraftTitle = "First"; f.Clock.Advance(750);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            vm.DraftTitle = "Newer"; remove = vm.RemoveTagCommand.ExecuteAsync(vm.AssignedTags[0]);
            Assert.Single(writes); release.TrySetResult(); await remove.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(3, writes.Count); Assert.Equal("Newer", vm.DraftTitle);
            Assert.Single(vm.AssignedTags); Assert.False(vm.IsDirty); Assert.Empty(f.Recovery.Drafts);
        }
        finally { release.TrySetResult(); if (remove is not null) await remove.WaitAsync(TimeSpan.FromSeconds(10)); await vm.CancelAsync(); }
    }
    [Fact]
    public async Task CancelledFilterAndFailedTagWriteKeepDraftRecoveryAndControlsCoherent()
    {
        await using var f = await Fixture.Open(); var vm = f.Vm;
        vm.DraftTitle = "Preserve"; f.Data.NextWrite = NotebookWriteStatus.Conflict;
        f.Clock.Advance(750); await vm.AutosaveTask;
        Assert.Single(f.Recovery.Drafts);
        await vm.ToggleTagFilterCommand.ExecuteAsync(vm.Tags[0]);
        Assert.Empty(vm.SelectedTagFilters); Assert.Equal("Preserve", vm.DraftTitle);
        vm.SearchText = "Other"; await vm.SearchCommand.ExecuteAsync(null);
        Assert.Equal("", vm.SearchText); Assert.Single(vm.Notes); Assert.True(vm.IsDirty);
        await vm.RemoveTagCommand.ExecuteAsync(vm.AssignedTags[0]);
        Assert.Equal(2, vm.AssignedTags.Count); Assert.Single(f.Recovery.Drafts);
        Assert.Equal(1, f.Data.Writes);
    }
    [Fact]
    public async Task SeparateSearchDebounceReplacesOlderQueriesAndCancellationDrains()
    {
        await using var f = await Fixture.Open(3); var vm = f.Vm;
        int count = f.Data.Queries.Count;
        vm.SearchText = "Note 0"; f.Clock.Advance(299); Assert.Equal(count, f.Data.Queries.Count);
        vm.SearchText = "Note 001"; f.Clock.Advance(300); await vm.SearchTask.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Single(vm.Notes); Assert.Equal("Note 001", vm.Notes[0].Title); Assert.Equal(0, f.Data.Writes);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<NotebookPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Data.OnList = (q, _) => q.Search == "old" ? Wait() : Task.FromResult(f.Data.Page(q));
        Task<NotebookPage> Wait() { entered.TrySetResult(); return release.Task; }
        Task old = Task.CompletedTask;
        try
        {
            vm.SearchText = "old"; old = vm.SearchCommand.ExecuteAsync(null);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            vm.SearchText = "Note 002"; f.Clock.Advance(300); await vm.SearchTask.WaitAsync(TimeSpan.FromSeconds(10));
            release.TrySetResult(f.Data.Page(new(search: "old"))); await old;
            Assert.Equal("Note 002", Assert.Single(vm.Notes).Title);
            vm.SearchText = "Pending"; await vm.CancelAsync();
            f.Clock.Advance(1000); await vm.CompletionTask; Assert.Equal("Note 002", vm.SearchText);
            vm.Deactivate(); await vm.DeactivationTask; f.Data.OnList = null; await vm.ActivateAsync();
            Assert.Single(vm.Notes);
        }
        finally { release.TrySetResult(f.Data.Page(new(search: "old"))); await old.WaitAsync(TimeSpan.FromSeconds(10)); }
    }
    [Fact]
    public async Task AutosaveCanMoveNoteOutsideSearchWithoutSwitchingEditorAndAssignmentKeepsItOpen()
    {
        await using var f = await Fixture.Open(); var vm = f.Vm;
        vm.SearchText = vm.Note!.Title; await vm.SearchCommand.ExecuteAsync(null);
        var id = vm.Note!.Id; vm.DraftTitle = "Outside filter";
        f.Clock.Advance(750); await vm.AutosaveTask;
        Assert.Empty(vm.Notes); Assert.Equal(id, vm.Note.Id); Assert.Null(vm.SelectedNote);
        await vm.RemoveTagCommand.ExecuteAsync(vm.AssignedTags[0]);
        Assert.Equal(id, vm.Note.Id); Assert.Equal("Outside filter", vm.DraftTitle);
    }
    [Fact]
    public async Task CatalogValidationFailureAndDeclinedDeletionKeepInputsAndAssignments()
    {
        await using var f = await Fixture.Open(); var vm = f.Vm;
        vm.TagName = " "; await vm.CreateTagCommand.ExecuteAsync(null);
        Assert.Contains("1–64", vm.TagMessage); Assert.Equal(0, f.Data.Writes);
        vm.TagName = "Retain name"; f.Data.NextWrite = NotebookWriteStatus.Invalid;
        await vm.CreateTagCommand.ExecuteAsync(null); Assert.Equal("Retain name", vm.TagName); Assert.NotEmpty(vm.TagMessage);
        f.Dialogs.ConfirmationResult = false; vm.ManagedTag = vm.Tags[0];
        int writes = f.Data.Writes; await vm.DeleteTagCommand.ExecuteAsync(null);
        Assert.Equal(writes, f.Data.Writes); Assert.Equal(2, vm.AssignedTags.Count);
    }
}
