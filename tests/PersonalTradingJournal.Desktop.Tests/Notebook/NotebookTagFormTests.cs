using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed class NotebookTagFormTests
{
    [Fact]
    public async Task AssignmentNavigationDoesNotClaimAnUnopenedCatalogFormWasDismissed()
    {
        await using var f = await NotebookTagTests.Fixture.Open(); var vm = f.Vm;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Data.OnWrite = async (command, _) =>
        {
            var remove = Assert.IsType<RemoveNotebookTag>(command); entered.SetResult(); await release.Task;
            var note = f.Data.Notes[0] with { TagIds = f.Data.Notes[0].TagIds.Remove(remove.TagId), Version = f.Data.Notes[0].Version + 1 };
            f.Data.Notes[0] = note; return new(NotebookWriteStatus.Success, note);
        };
        var pending = vm.RemoveTagCommand.ExecuteAsync(vm.AssignedTags[0]);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); vm.Panel = 1; vm.DismissTagForm();
            Assert.DoesNotContain("Tag input closed", vm.Message); Assert.False(vm.IsTagCreationOpen);
        }
        finally { release.TrySetResult(); await pending.WaitAsync(TimeSpan.FromSeconds(10)); }
        Assert.Single(vm.AssignedTags); Assert.Equal(2, f.Data.Tags.Count);
    }

    [Fact]
    public async Task CreationAndCancelAreExplicitAndNeverAssignOrFilter()
    {
        await using var f = await NotebookTagTests.Fixture.Open(); var vm = f.Vm;
        var note = vm.Note!; var scope = vm.Scope; var assigned = vm.AssignedTags.ToArray();
        Assert.True(vm.BeginTagCreation()); Assert.False(vm.HasTagName);
        vm.TagName = " \t "; Assert.False(vm.HasTagName);
        vm.TagName = "Cancel draft"; Assert.True(vm.HasTagName); vm.CancelTagForm();
        Assert.False(vm.IsTagCreationOpen); Assert.Equal("", vm.TagName); Assert.Equal(0, f.Data.Writes);
        vm.BeginTagCreation(); vm.TagName = "New global tag"; await vm.CreateTagCommand.ExecuteAsync(null);
        Assert.Equal(1, f.Data.Writes); Assert.Equal(3, vm.Tags.Count);
        Assert.Contains(vm.Tags, t => t.Name == "New global tag" && !t.IsAssigned && !t.IsSelected);
        Assert.Equal(assigned, vm.AssignedTags); Assert.Empty(vm.SelectedTagFilters);
        Assert.Equal(note.Id, vm.Note!.Id); Assert.Equal(note.Version, vm.Note.Version); Assert.Equal(scope, vm.Scope);
        Assert.False(vm.IsTagCreationOpen); Assert.Equal("", vm.TagName);
    }

    [Theory]
    [InlineData(NotebookWriteStatus.Invalid)]
    [InlineData(NotebookWriteStatus.Conflict)]
    [InlineData(NotebookWriteStatus.StorageFailure)]
    [InlineData(NotebookWriteStatus.Cancelled)]
    public async Task FailureRetainsFormAndDraft(NotebookWriteStatus status)
    {
        await using var f = await NotebookTagTests.Fixture.Open(); var vm = f.Vm;
        vm.BeginTagCreation(); vm.TagName = " \t "; await vm.CreateTagCommand.ExecuteAsync(null);
        Assert.Equal(0, f.Data.Writes); Assert.Contains("1–64", vm.TagMessage); Assert.Equal(" \t ", vm.TagName);
        vm.TagName = "Retained"; f.Data.NextWrite = status; await vm.CreateTagCommand.ExecuteAsync(null);
        Assert.Equal("Retained", vm.TagName); Assert.True(vm.IsTagCreationOpen); Assert.NotEmpty(vm.TagMessage);
        Assert.Equal(2, f.Data.Tags.Count); Assert.False(vm.IsTagOperationPending);
    }

    [Theory]
    [InlineData("stay")]
    [InlineData("cancel")]
    [InlineData("panel")]
    [InlineData("new-input")]
    [InlineData("deactivate")]
    public async Task PendingCreationIsSingleAndCannotConsumeNewerInput(string action)
    {
        await using var f = await NotebookTagTests.Fixture.Open(); var vm = f.Vm;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var item = new NotebookNamedItem(Guid.NewGuid(), "Committed", 1); var note = vm.Note!;
        f.Data.OnCatalog = async (c, _) => { Assert.Equal(item.Name, Assert.IsType<CreateNotebookTag>(c).Name); entered.SetResult(); await release.Task; f.Data.Tags.Add(item); return new(NotebookWriteStatus.Success, item); };
        vm.Panel = 0; vm.BeginTagCreation(); vm.TagName = item.Name;
        var pending = vm.CreateTagCommand.ExecuteAsync(null);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(vm.IsTagOperationPending); Assert.False(vm.CreateTagCommand.CanExecute(null));
            Assert.False(vm.BeginTagCreation()); Assert.False(vm.BeginTagRename(vm.Tags[0]));
            await vm.CreateTagCommand.ExecuteAsync(null); Assert.Equal(1, f.Data.Writes);
            switch (action)
            {
                case "cancel": vm.CancelTagForm(); Assert.Contains("may already be saved", vm.Message); break;
                case "panel": vm.Panel = 1; break;
                case "new-input": vm.TagName = "Newer input"; break;
                case "deactivate": vm.Deactivate(); break;
            }
        }
        finally { release.TrySetResult(); await pending.WaitAsync(TimeSpan.FromSeconds(10)); }
        Assert.Equal(1, f.Data.Writes); Assert.Equal(3, f.Data.Tags.Count); Assert.Equal(note.Id, vm.Note!.Id);
        Assert.Equal(note.TagIds, vm.Note.TagIds); Assert.Empty(vm.SelectedTagFilters);
        if (action == "stay") { Assert.False(vm.IsTagCreationOpen); Assert.Equal("", vm.TagName); }
        if (action == "cancel") { Assert.False(vm.IsTagCreationOpen); Assert.Equal("", vm.TagName); }
        if (action == "new-input") { Assert.True(vm.IsTagCreationOpen); Assert.Equal("Newer input", vm.TagName); }
        Assert.False(vm.IsTagOperationPending);
    }

    [Fact]
    public async Task RenameUsesCapturedIdentityAndRetainsValidationThenCancelDoesNotWrite()
    {
        await using var f = await NotebookTagTests.Fixture.Open(); var vm = f.Vm;
        var target = vm.Tags[0]; var other = vm.Tags[1];
        Assert.True(vm.BeginTagRename(target)); Assert.Equal(target.Name, vm.TagName);
        vm.ManagedTag = other; vm.TagName = " "; await vm.RenameTagCommand.ExecuteAsync(null);
        Assert.Equal(0, f.Data.Writes); Assert.True(vm.IsTagRenameOpen); Assert.NotEmpty(vm.TagMessage);
        vm.TagName = "Renamed captured"; await vm.RenameTagCommand.ExecuteAsync(null);
        Assert.Contains(vm.Tags, t => t.Id == target.Id && t.Name == "Renamed captured" && t.Version == target.Version + 1);
        Assert.Contains(vm.Tags, t => t.Id == other.Id && t.Name == other.Name); Assert.False(vm.IsTagRenameOpen);
        Assert.True(vm.BeginTagRename(vm.Tags.Single(t => t.Id == other.Id))); vm.TagName = "Discard"; vm.CancelTagForm();
        Assert.Equal(1, f.Data.Writes); Assert.Equal("", vm.TagName); Assert.False(vm.IsTagRenameOpen);
        Assert.False(vm.BeginTagRename(target)); // Old version from an already-open menu cannot replace the current one.
    }

    [Fact]
    public async Task CapturedDeleteIsGlobalAndCannotFollowManagedSelection()
    {
        await using var f = await NotebookTagTests.Fixture.Open(); var vm = f.Vm;
        var target = vm.Tags[0]; var other = vm.Tags[1]; var version = vm.Note!.Version;
        vm.ManagedTag = other; f.Dialogs.ConfirmationResult = false;
        await vm.DeleteGlobalTagAsync(target); Assert.Equal(0, f.Data.Writes); Assert.Equal(2, vm.AssignedTags.Count);
        Assert.Contains(target.Name, f.Dialogs.ConfirmationRequest!.Message);
        Assert.Contains("every note", f.Dialogs.ConfirmationRequest.Message);
        f.Dialogs.ConfirmationResult = true; await vm.DeleteGlobalTagAsync(target);
        Assert.DoesNotContain(vm.Tags, t => t.Id == target.Id); Assert.Contains(vm.Tags, t => t.Id == other.Id);
        Assert.Equal(other.Id, Assert.Single(vm.AssignedTags).Id); Assert.Equal(version + 1, vm.Note!.Version);
        vm.DraftTitle = "Autosave after global delete"; f.Clock.Advance(750); await vm.AutosaveTask;
        Assert.Equal(other.Id, Assert.Single(f.Data.Notes[0].TagIds)); Assert.Equal("Saved", vm.SaveState);
        await vm.DeleteGlobalTagAsync(target); Assert.Equal(2, f.Data.Writes); // delete + save, no stale-menu write
    }

    [Fact]
    public async Task DismissalDuringDraftConsentPreventsCatalogWrite()
    {
        var data = new NotebookFake(); data.Notes.Add(NotebookFake.Note());
        var consent = new DismissingConsent(); using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService(), consent);
        await vm.ActivateAsync(); vm.SelectedNote = vm.Notes[0]; await vm.LoadTask;
        vm.DraftTitle = "Keep exact draft"; vm.BeginTagCreation(); vm.TagName = "Abandoned"; consent.Dismiss = vm.CancelTagForm;
        await vm.CreateTagCommand.ExecuteAsync(null);
        Assert.Empty(data.Tags); Assert.Equal(0, data.Writes); Assert.Equal("Keep exact draft", vm.DraftTitle); Assert.True(vm.IsDirty);
        Assert.False(vm.IsTagCreationOpen); Assert.Equal("", vm.TagName);
    }
    private sealed class DismissingConsent : INotebookInteraction
    {
        public Action? Dismiss;
        public NotebookLeaveChoice ConfirmLeave() { Dismiss?.Invoke(); return NotebookLeaveChoice.Cancel; }
    }
}
