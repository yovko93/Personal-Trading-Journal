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
    [Fact]
    public async Task LinkedShellRechecksOriginAfterAncestorGuardBeforeTouchingAnotherDraft()
    {
        await using var plan = await TradingPlans.TradingPlanWorkspaceTests.Fixture.CreateAsync(seed: true);
        var store = new NotebookFake(); var target = Guid.NewGuid();
        var origin = NotebookFake.Note("Origin") with { Document = NotebookEditingTests.Text("Origin body"), Links = [new(NotebookTargetKind.TradingSetup, target, "Study")] };
        var other = NotebookFake.Note("Other") with { Document = NotebookEditingTests.Text("Other body") };
        store.Notes.AddRange([origin, other]);
        store.OnResolve = (links, _) => Task.FromResult<ImmutableArray<NotebookResolvedLink>>([.. links.Select(l => new NotebookResolvedLink(l, NotebookTargetAvailability.Available, "Study"))]);
        var choices = new NotebookChoices { Choice = NotebookLeaveChoice.Discard };
        using var notebook = new NotebookWorkspaceViewModel(store, new FakeDialogService(), choices);
        using var shell = CreateFixture(notebook: notebook, tradingPlan: plan.Vm).Main;
        shell.NavigateCommand.Execute(NavigationDestination.Notebook); await notebook.LoadTask;
        notebook.SelectedNote = notebook.Notes.Single(n => n.Id == origin.Id); await notebook.LoadTask;
        plan.Vm.Editor!.Focus.Text = "Pending ancestor save";
        plan.Interaction.Choice = PersonalTradingJournal.Desktop.ViewModels.TradingPlans.PlanLeaveChoice.Save;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        plan.Store.BeforeWrite = _ => { entered.TrySetResult(); return release.Task; };
        var navigation = notebook.OpenLinkedItemCommand.ExecuteAsync(notebook.LinkedItems.Single());
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            notebook.SelectedNote = notebook.Notes.Single(n => n.Id == other.Id); await notebook.LoadTask;
            notebook.DraftTitle = "Retain this newer draft";
            notebook.SetDraftDocument(NotebookEditingTests.Text("Retain exact newer body"));
            release.SetResult(); await navigation.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(0, choices.Prompts); Assert.Equal(0, store.Writes);
            Assert.Equal(NavigationDestination.Notebook, shell.CurrentDestination); Assert.False(shell.CanReturnToNotebook);
            Assert.Equal(other.Id, notebook.Note!.Id); Assert.Equal("Retain this newer draft", notebook.DraftTitle);
            Assert.Equal("Retain exact newer body\n", notebook.DraftDocument.PlainText); Assert.True(notebook.IsDirty);
            Assert.False(notebook.OpenLinkedItemCommand.IsRunning);
        }
        finally { release.TrySetResult(); await navigation.WaitAsync(TimeSpan.FromSeconds(10)); }
    }

    [Theory]
    [InlineData("resolution")] [InlineData("round-trip")] [InlineData("save-completion")]
    public async Task LinkedShellRejectsAbandonedSelectionAndCanNavigateAgain(string boundary)
    {
        var store = new NotebookFake(); var target = Guid.NewGuid();
        var origin = NotebookFake.Note("Origin") with { Document = NotebookEditingTests.Text("Origin body"), Links = [new(NotebookTargetKind.TradingSetup, target, "Study")] };
        var other = NotebookFake.Note("Other") with { Document = NotebookEditingTests.Text("Other body") };
        store.Notes.AddRange([origin, other]);
        static Task<ImmutableArray<NotebookResolvedLink>> Resolve(ImmutableArray<NotebookLink> links, CancellationToken _) =>
            Task.FromResult<ImmutableArray<NotebookResolvedLink>>([.. links.Select(l => new NotebookResolvedLink(l, NotebookTargetAvailability.Available, "Study"))]);
        store.OnResolve = Resolve;
        var choices = new NotebookChoices { Choice = NotebookLeaveChoice.Save };
        using var notebook = new NotebookWorkspaceViewModel(store, new FakeDialogService(), choices);
        var fixture = CreateFixture(notebook: notebook); using var shell = fixture.Main;
        shell.NavigateCommand.Execute(NavigationDestination.Notebook); await notebook.LoadTask;
        notebook.SelectedNote = notebook.Notes.Single(n => n.Id == origin.Id); await notebook.LoadTask;
        var item = notebook.LinkedItems.Single(); var originRow = notebook.SelectedNote!;
        var otherRow = notebook.Notes.Single(n => n.Id == other.Id);
        var now = DateTimeOffset.UtcNow;
        fixture.SetupReader.DetailsToReturn = new(target, "Study", "Description", false, now, now);
        using var reads = new PausedNotebookReads();
        bool changed = false;
        void ChangeSelection()
        {
            changed = true; reads.Select(notebook, otherRow);
            if (boundary == "round-trip") reads.Select(notebook, originRow);
        }
        var pending = new TaskCompletionSource<ImmutableArray<NotebookResolvedLink>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var writeEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writeResult = new TaskCompletionSource<NotebookWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Saved(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Save has published the origin and released write ownership, but its
            // asynchronous leave guard has not returned to the shell yet.
            if (e.PropertyName == nameof(notebook.IsWriting) && !notebook.IsWriting && !changed) ChangeSelection();
        }
        if (boundary == "save-completion")
        {
            notebook.DraftTitle = "Saved origin";
            store.OnWrite = (_, _) => { writeEntered.TrySetResult(); return writeResult.Task; };
            notebook.PropertyChanged += Saved;
        }
        else store.OnResolve = (_, _) => pending.Task;
        var navigation = notebook.OpenLinkedItemCommand.ExecuteAsync(item);
        if (boundary == "save-completion")
        {
            await writeEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(NavigationDestination.Notebook, shell.CurrentDestination);
            var saved = origin with { Title = "Saved origin", Version = origin.Version + 1 };
            store.Notes[0] = saved; writeResult.SetResult(new(NotebookWriteStatus.Success, saved));
        }
        else
        {
            ChangeSelection();
            Assert.Equal(origin.Id, notebook.Note!.Id); Assert.False(notebook.LoadTask.IsCompleted);
            pending.SetResult([new(item.Link, NotebookTargetAvailability.Available, "Study")]);
        }
        await navigation.WaitAsync(TimeSpan.FromSeconds(10)); notebook.PropertyChanged -= Saved;
        Assert.True(changed); Assert.Equal(NavigationDestination.Notebook, shell.CurrentDestination);
        Assert.False(shell.CanReturnToNotebook); Assert.False(shell.ReturnToNotebookCommand.CanExecute(null));
        Assert.False(notebook.OpenLinkedItemCommand.IsRunning);
        Assert.Equal(boundary == "save-completion" ? 1 : 0, choices.Prompts);
        Assert.Equal(boundary == "save-completion" ? 1 : 0, store.Writes);
        Assert.Equal("Other", store.Notes.Single(n => n.Id == other.Id).Title);
        store.OnResolve = Resolve; store.OnWrite = null;
        reads.Dispose(); await notebook.LoadTask;
        notebook.SelectedNote = notebook.Notes.Single(n => n.Id == origin.Id); await notebook.LoadTask;
        await notebook.OpenLinkedItemCommand.ExecuteAsync(notebook.LinkedItems.Single());
        Assert.Equal(NavigationDestination.Setups, shell.CurrentDestination); Assert.True(shell.CanReturnToNotebook);
        shell.ReturnToNotebookCommand.Execute(null); await notebook.LoadTask;
        Assert.Equal(origin.Id, notebook.Note!.Id); Assert.Equal("Origin body\n", notebook.DraftDocument.PlainText);
        Assert.False(shell.CanReturnToNotebook);
    }

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
        Assert.False(vm.HasLeaveConsent); // Failed destination recovery must resume the retained draft, not keep discard consent.
    }
}
