using System.Collections.Immutable;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Tests.Notebook;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Navigation;

public sealed partial class MainWindowViewModelTests
{
    [Theory]
    [InlineData(NotebookTargetKind.Trade)] [InlineData(NotebookTargetKind.TradingSetup)]
    public async Task LinkedIdentityOpensExistingDetailsAndReturnsToSameNotebookContext(NotebookTargetKind kind)
    {
        var store = new NotebookFake(); var target = Guid.NewGuid(); var folder = new NotebookNamedItem(Guid.NewGuid(), "Research", 1); store.Folders.Add(folder);
        var tag = new NotebookNamedItem(Guid.NewGuid(), "Context", 1); store.Tags.Add(tag);
        var note = NotebookFake.Note("Linked research details", folder.Id) with { Document = NotebookEditingTests.Text("Exact body"), TagIds = [tag.Id], Links = [new(kind, target, "Original label")] };
        store.Notes.Add(note); store.OnResolve = (links, _) => Task.FromResult<ImmutableArray<NotebookResolvedLink>>([.. links.Select(l => new NotebookResolvedLink(l, NotebookTargetAvailability.Available, "Current label"))]);
        using var notebook = new NotebookWorkspaceViewModel(store, new FakeDialogService(), new NotebookChoices { Choice = NotebookLeaveChoice.Save });
        var fixture = CreateFixture(notebook: notebook); using var shell = fixture.Main;
        shell.NavigateCommand.Execute(NavigationDestination.Notebook); await notebook.LoadTask;
        notebook.Scope = notebook.Scopes.Single(x => x.FolderId == folder.Id); await notebook.LoadTask;
        notebook.SearchText = "details"; await notebook.SearchCommand.ExecuteAsync(null);
        await notebook.ToggleTagFilterCommand.ExecuteAsync(notebook.Tags.Single(x => x.Id == tag.Id));
        notebook.Sort = NotebookWorkspaceViewModel.SortOptions[1]; await notebook.LoadTask;
        notebook.SelectedNote = notebook.Notes[0]; await notebook.LoadTask;
        var now = DateTimeOffset.UtcNow;
        fixture.SetupReader.DetailsToReturn = new(target, "Actual setup", "Description", false, now, now);
        // Identity navigation is independent of ordinary browse membership and filters.
        var row = new PersonalTradingJournal.Application.Trades.TradeListItem(target, Guid.NewGuid(), "Account", Guid.NewGuid(), "NQ",
            PersonalTradingJournal.Domain.Trades.TradeDirection.Long, PersonalTradingJournal.Domain.Trades.TradeStatus.Closed,
            now, now, 0, 100, 101, 0, 20, 20, "USD", 1);
        fixture.TradeDetailReader.EnqueueResult(CreateTradeDetail(row));
        notebook.DraftTitle = "Saved before details";
        await notebook.OpenLinkedItemCommand.ExecuteAsync(notebook.LinkedItems.Single());
        Assert.Equal(kind == NotebookTargetKind.Trade ? NavigationDestination.Trades : NavigationDestination.Setups, shell.CurrentDestination);
        if (kind == NotebookTargetKind.Trade) Assert.Equal(target, fixture.Trades.SelectedTradeDetail!.Id);
        else Assert.Equal(target, fixture.Setups.SelectedTradingSetup!.Id);
        Assert.True(shell.CanReturnToNotebook); shell.ReturnToNotebookCommand.Execute(null); await notebook.LoadTask;
        Assert.Equal(NavigationDestination.Notebook, shell.CurrentDestination); Assert.Equal(note.Id, notebook.Note!.Id);
        Assert.Equal(folder.Id, notebook.Scope!.FolderId); Assert.Equal("Saved before details", notebook.DraftTitle);
        Assert.Equal("details", notebook.SearchText); Assert.Equal(tag.Id, Assert.Single(notebook.SelectedTagFilters).Id);
        Assert.Equal(NotebookSort.Title, notebook.Sort.Value);
        Assert.Equal("Exact body\n", notebook.DraftDocument.PlainText); Assert.False(notebook.IsDirty); Assert.Equal(1, store.Writes);
    }
    [Theory]
    [InlineData("cancel")] [InlineData("failed-save")] [InlineData("missing-target")] [InlineData("failed-details")]
    public async Task FailedLinkedNavigationKeepsNotebookAndEveryDraftField(string failure)
    {
        var store = new NotebookFake(); var target = Guid.NewGuid();
        store.Notes.Add(NotebookFake.Note("Research") with { Document = NotebookEditingTests.Text("Original"), Links = [new(NotebookTargetKind.TradingSetup, target, "Study")] });
        store.OnResolve = (links, _) => Task.FromResult<ImmutableArray<NotebookResolvedLink>>([.. links.Select(l => new NotebookResolvedLink(l, NotebookTargetAvailability.Available, "Study"))]);
        var choices = new NotebookChoices { Choice = failure is "missing-target" or "failed-details" ? NotebookLeaveChoice.Discard : failure == "cancel" ? NotebookLeaveChoice.Cancel : NotebookLeaveChoice.Save };
        using var vm = new NotebookWorkspaceViewModel(store, new FakeDialogService(), choices);
        var f = CreateFixture(notebook: vm); using var shell = f.Main;
        shell.NavigateCommand.Execute(NavigationDestination.Notebook); await vm.LoadTask; vm.SelectedNote = vm.Notes[0]; await vm.LoadTask;
        vm.DraftTitle = "Retain title"; vm.SetDraftDocument(NotebookEditingTests.Text("Retain exact body"));
        if (failure == "failed-save") store.NextWrite = NotebookWriteStatus.StorageFailure;
        if (failure == "failed-details") f.SetupReader.DetailsException = new IOException("private failure");
        await vm.OpenLinkedItemCommand.ExecuteAsync(vm.LinkedItems.Single());
        Assert.Equal(NavigationDestination.Notebook, shell.CurrentDestination); Assert.Equal("Retain title", vm.DraftTitle);
        Assert.Equal("Retain exact body\n", vm.DraftDocument.PlainText); Assert.True(vm.IsDirty); Assert.Single(vm.Note!.Links);
        Assert.DoesNotContain("private", vm.LinkMessage);
    }
}
