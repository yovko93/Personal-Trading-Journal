using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Tests.Journals;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Storage;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed class NotebookSqliteWorkspaceTests
{
    [Fact]
    public async Task AutosaveConflictCheckpointRestartsAsProtectedDraftAndNeverRetargetsTheVersion()
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        var service = db.Provider.GetRequiredService<INotebookService>();
        var recovery = db.Provider.GetRequiredService<INotebookRecoveryStore>();
        var codec = db.Provider.GetRequiredService<INotebookContentCodec>();
        var clock = new NotebookClock();
        using var vm = new NotebookWorkspaceViewModel(service, new FakeDialogService { ConfirmationResult = true }, codec: codec, recovery: recovery, time: clock);
        await vm.ActivateAsync(); await vm.NewNoteCommand.ExecuteAsync(null);
        vm.DraftTitle = "Autosaved"; vm.SetDraftDocument(NotebookEditingTests.Text("Canonical observation"));
        clock.Advance(750); await vm.AutosaveTask.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(vm.IsDirty); Assert.Equal("Saved", vm.SaveState);
        var saved = vm.Note!;
        Assert.Equal("Autosaved", (await service.LoadAsync(saved.Id)).Note!.Title);
        Assert.Empty((await recovery.ListAsync()).Items);
        var external = await service.ExecuteAsync(new UpdateNotebookTitle(saved.Id, saved.Version, "External revision"));
        Assert.Equal(NotebookWriteStatus.Success, external.Status);
        vm.DraftTitle = "Keep my local title"; vm.SetDraftDocument(NotebookEditingTests.Text("Keep my local body"));
        clock.Advance(750); await vm.AutosaveTask.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.StartsWith("Conflict", vm.SaveState); Assert.True(vm.IsDirty);
        var checkpoint = Assert.Single((await recovery.ListAsync()).Items);
        vm.Dispose(); await vm.DeactivationTask;
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        string root = Path.GetDirectoryName(Path.GetDirectoryName(context.Database.GetDbConnection().DataSource))!;
        await using var fresh = new ServiceCollection().AddPersistence(new LocalApplicationPaths(root)).BuildServiceProvider();
        using var restarted = new NotebookWorkspaceViewModel(fresh.GetRequiredService<INotebookService>(), new FakeDialogService { ConfirmationResult = true },
            codec: fresh.GetRequiredService<INotebookContentCodec>(), recovery: fresh.GetRequiredService<INotebookRecoveryStore>(), time: clock);
        try
        {
            await restarted.ActivateAsync();
            await restarted.RecoverCommand.ExecuteAsync(Assert.Single(restarted.RecoveryDrafts));
            Assert.Equal("Keep my local title", restarted.DraftTitle); Assert.Contains("local body", restarted.DraftDocument.PlainText);
            Assert.False(await restarted.SaveDraftAsync());
            Assert.Equal("External revision", (await service.LoadAsync(saved.Id)).Note!.Title);
            Assert.Equal(external.Note!.Version, (await service.LoadAsync(saved.Id)).Note!.Version);
            Assert.Equal(checkpoint.Id, Assert.Single((await recovery.ListAsync()).Items).Id);
            await restarted.DiscardCommand.ExecuteAsync(null);
            Assert.False(restarted.IsDirty); Assert.Empty((await recovery.ListAsync()).Items);
            Assert.Equal("External revision", restarted.DraftTitle);
        }
        finally { restarted.Dispose(); await restarted.DeactivationTask; }
    }
    [Fact]
    public async Task AtomicEditorSaveRestartConflictAndInvalidWriteRetainTheWholeNote()
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        var service = db.Provider.GetRequiredService<INotebookService>();
        using var vm = new NotebookWorkspaceViewModel(service, new FakeDialogService());
        await vm.ActivateAsync(); await vm.NewNoteCommand.ExecuteAsync(null);
        var initial = vm.Note!;
        vm.DraftTitle = "Saved title"; vm.SetDraftDocument(NotebookEditingTests.Text("Saved searchable body"));
        Assert.True(await vm.SaveDraftAsync()); Assert.Equal(initial.Version + 1, vm.Note!.Version);
        var saved = vm.Note;
        var invalid = await service.ExecuteAsync(new SaveNotebookDraft(saved.Id, saved.Version, "", NotebookEditingTests.Text("Must not appear"), saved.Images));
        Assert.Equal(NotebookWriteStatus.Invalid, invalid.Status);
        var unchanged = (await service.LoadAsync(saved.Id)).Note!;
        Assert.Equal(saved.Title, unchanged.Title); Assert.Equal(saved.Version, unchanged.Version); Assert.Equal(saved.Document.PlainText, unchanged.Document.PlainText);
        Assert.Equal(NotebookWriteStatus.Success, (await service.ExecuteAsync(new UpdateNotebookTitle(saved.Id, saved.Version, "External title"))).Status);
        vm.DraftTitle = "Conflicting title"; vm.SetDraftDocument(NotebookEditingTests.Text("Keep conflict body"));
        Assert.False(await vm.SaveDraftAsync()); Assert.True(vm.IsDirty); Assert.Contains("Keep conflict body", vm.DraftDocument.PlainText);
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        string root = Path.GetDirectoryName(Path.GetDirectoryName(context.Database.GetDbConnection().DataSource))!;
        vm.Dispose(); await vm.DeactivationTask;
        await using var fresh = new ServiceCollection().AddPersistence(new LocalApplicationPaths(root)).BuildServiceProvider();
        var restarted = await fresh.GetRequiredService<INotebookService>().LoadAsync(saved.Id);
        Assert.Equal("External title", restarted.Note!.Title); Assert.Equal(saved.Document.PlainText, restarted.Note.Document.PlainText);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Equal(NotebookWriteStatus.Cancelled, (await service.ExecuteAsync(new SaveNotebookDraft(saved.Id, restarted.Note.Version, "Cancelled", NotebookEditingTests.Text("No write"), saved.Images), cancellation.Token)).Status);
        Assert.Equal(restarted.Note.Version, (await service.LoadAsync(saved.Id)).Note!.Version);
    }

    [Fact]
    public async Task WorkspaceMutationsPersistAcrossFreshServicesAndConflictRetainsFolderInput()
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        var service = db.Provider.GetRequiredService<INotebookService>();
        using var vm = new NotebookWorkspaceViewModel(service, new FakeDialogService { ConfirmationResult = true });
        await vm.ActivateAsync(); vm.FolderName = "Research"; await vm.CreateFolderCommand.ExecuteAsync(null);
        vm.Scope = vm.Scopes.Single(s => s.FolderId.HasValue); await vm.LoadTask;
        await vm.NewNoteCommand.ExecuteAsync(null); var id = vm.Note!.Id;
        await vm.PinCommand.ExecuteAsync(null);
        vm.ManagedFolder = vm.Folders.Single(f => f.Id.HasValue); var folder = vm.ManagedFolder;
        Assert.Equal(NotebookWriteStatus.Success, (await service.ExecuteCatalogAsync(new RenameNotebookFolder(folder.Id!.Value, folder.Version, "External rename"))).Status);
        vm.FolderName = "Keep my rename"; await vm.RenameFolderCommand.ExecuteAsync(null);
        Assert.Contains("changed elsewhere", vm.Message); Assert.Equal("Keep my rename", vm.FolderName);
        await vm.RefreshCommand.ExecuteAsync(null); vm.ManagedFolder = vm.Folders.Single(f => f.Id.HasValue);
        await vm.DeleteFolderCommand.ExecuteAsync(null); Assert.Null(vm.Note!.FolderId);
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        string root = Path.GetDirectoryName(Path.GetDirectoryName(context.Database.GetDbConnection().DataSource))!;
        vm.Deactivate(); await vm.DeactivationTask;
        await using var fresh = new ServiceCollection().AddPersistence(new LocalApplicationPaths(root)).BuildServiceProvider();
        using var restarted = new NotebookWorkspaceViewModel(fresh.GetRequiredService<INotebookService>(), new FakeDialogService());
        await restarted.ActivateAsync(); restarted.SelectedNote = restarted.Notes.Single(n => n.Id == id); await restarted.LoadTask;
        Assert.Equal("Untitled", restarted.Note!.Title); Assert.True(restarted.Note.IsPinned); Assert.Null(restarted.Note.FolderId);
        Assert.DoesNotContain(restarted.Folders, f => f.Id.HasValue);
    }
}
