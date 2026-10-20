using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Infrastructure.Tests.Notebook;

public sealed class NotebookTagWorkflowTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StaleAutosaveCannotRestoreExternallyRemovedAssignment(bool deleteCatalog)
    {
        await using var f = await NotebookFixture.Create(); var service = f.Service();
        var tag = await f.Tag(); var note = await f.CreateNote();
        note = NotebookFixture.Success(await service.ExecuteAsync(new AssignNotebookTag(note.Id, note.Version, tag.Id)));
        var draft = new NotebookDocument([NotebookNode.Paragraph(Guid.NewGuid(), [NotebookNode.Run(Guid.NewGuid(), "Retained observation")])]);
        var status = deleteCatalog
            ? (await service.ExecuteCatalogAsync(new DeleteNotebookTag(tag.Id, tag.Version, true))).Status
            : (await service.ExecuteAsync(new RemoveNotebookTag(note.Id, note.Version, tag.Id))).Status;
        Assert.Equal(NotebookWriteStatus.Success, status);
        Assert.Equal(NotebookWriteStatus.Conflict, (await service.ExecuteAsync(new SaveNotebookDraft(note.Id, note.Version, "Stale autosave", draft, note.Images))).Status);
        var current = (await f.Service().LoadAsync(note.Id)).Note!;
        Assert.Empty(current.TagIds); Assert.Equal(note.Version + 1, current.Version);
        Assert.Equal(note.Title, current.Title); Assert.Equal(note.Document.PlainText, current.Document.PlainText);
        // An explicitly refreshed content save also cannot recreate an old assignment.
        var saved = NotebookFixture.Success(await service.ExecuteAsync(new SaveNotebookDraft(current.Id, current.Version, "Refreshed save", draft, current.Images)));
        Assert.Empty(saved.TagIds); Assert.Equal("Retained observation\n", saved.Document.PlainText);
        Assert.Equal(current.Version + 1, saved.Version);
        Assert.Equal(deleteCatalog ? 0 : 1, (await service.ListCatalogAsync(new(NotebookCatalogKind.Tags))).Items.Length);
    }

    [Fact]
    public async Task SelectedCatalogIdentitiesBeyondFirstPageResolveRenameAndDeletionInSnapshot()
    {
        await using var f = await NotebookFixture.Create(); var service = f.Service();
        for (int i = 0; i < 51; i++) await f.Tag($"Tag {i:00}");
        var selected = await f.Tag("Z selected");
        var query = new NotebookQuery(tagIds: [selected.Id]);
        var page = await service.ListAsync(query);
        Assert.Equal(50, page.Summary.Tags.Items.Length);
        Assert.DoesNotContain(page.Summary.Tags.Items, t => t.Item.Id == selected.Id);
        Assert.Equal(selected, Assert.Single(page.Summary.RequestedTags).Item);
        var renamed = await service.ExecuteCatalogAsync(new RenameNotebookTag(selected.Id, selected.Version, "Z renamed"));
        page = await service.ListAsync(query);
        Assert.Equal(renamed.Item, Assert.Single(page.Summary.RequestedTags).Item);
        await service.ExecuteCatalogAsync(new DeleteNotebookTag(selected.Id, renamed.Item!.Version, true));
        Assert.Empty((await service.ListAsync(query)).Summary.RequestedTags);
    }

    [Fact]
    public async Task CatalogNormalizationRenameAndGlobalDeletePreserveIdentityContentAndAuditRules()
    {
        await using var f = await NotebookFixture.Create();
        var service = f.Service(); var tag = await f.Tag("  Cafe\u0301  ");
        Assert.Equal("Café", tag.Name);
        Assert.Equal(NotebookWriteStatus.Invalid, (await service.ExecuteCatalogAsync(new CreateNotebookTag("CAFÉ"))).Status);
        var note = await f.CreateNote();
        note = NotebookFixture.Success(await service.ExecuteAsync(new AssignNotebookTag(note.Id, note.Version, tag.Id)));
        var original = note;
        await using var db = f.Context();
        string json = (await db.NotebookNotes.AsNoTracking().SingleAsync()).DocumentJson;
        f.Time.Now = f.Time.Now.AddMinutes(1);
        var renamed = await service.ExecuteCatalogAsync(new RenameNotebookTag(tag.Id, tag.Version, "Research"));
        Assert.Equal(NotebookWriteStatus.Success, renamed.Status); Assert.Equal(tag.Id, renamed.Item!.Id);
        Assert.Equal(tag.Version + 1, renamed.Item.Version);
        note = (await service.LoadAsync(note.Id)).Note!;
        Assert.Equal(original.Version, note.Version); Assert.Equal(original.UpdatedAtUtc, note.UpdatedAtUtc);
        Assert.Equal(tag.Id, Assert.Single(note.TagIds));
        Assert.Equal(NotebookWriteStatus.Conflict, (await service.ExecuteCatalogAsync(new DeleteNotebookTag(tag.Id, tag.Version, true))).Status);
        Assert.Equal(NotebookWriteStatus.Invalid, (await service.ExecuteCatalogAsync(new DeleteNotebookTag(tag.Id, renamed.Item.Version, false))).Status);
        Assert.Equal(NotebookWriteStatus.Success, (await service.ExecuteCatalogAsync(new DeleteNotebookTag(tag.Id, renamed.Item.Version, true))).Status);
        note = (await service.LoadAsync(note.Id)).Note!;
        Assert.Empty(note.TagIds); Assert.Equal(original.Version + 1, note.Version); Assert.Equal(f.Time.Now, note.UpdatedAtUtc);
        Assert.Equal(json, (await db.NotebookNotes.AsNoTracking().SingleAsync()).DocumentJson);
        Assert.Equal(NotebookWriteStatus.Conflict, (await service.ExecuteAsync(new SaveNotebookDraft(original.Id, original.Version, "stale", original.Document, original.Images))).Status);
    }

    [Fact]
    public async Task RemovingOneAssignmentKeepsCatalogAndOtherNotesAndEnforcesLifecycle()
    {
        await using var f = await NotebookFixture.Create(); var service = f.Service();
        var tag = await f.Tag(); var a = await f.CreateNote("A"); var b = await f.CreateNote("B");
        a = NotebookFixture.Success(await service.ExecuteAsync(new AssignNotebookTag(a.Id, a.Version, tag.Id)));
        b = NotebookFixture.Success(await service.ExecuteAsync(new AssignNotebookTag(b.Id, b.Version, tag.Id)));
        Assert.Equal(NotebookWriteStatus.Invalid, (await service.ExecuteAsync(new AssignNotebookTag(a.Id, a.Version, tag.Id))).Status);
        a = NotebookFixture.Success(await service.ExecuteAsync(new RemoveNotebookTag(a.Id, a.Version, tag.Id)));
        Assert.Empty(a.TagIds); Assert.Single((await service.ListCatalogAsync(new(NotebookCatalogKind.Tags))).Items);
        Assert.Single((await service.LoadAsync(b.Id)).Note!.TagIds);
        a = NotebookFixture.Success(await service.ExecuteAsync(new ArchiveNotebookNote(a.Id, a.Version)));
        Assert.Equal(NotebookWriteStatus.Invalid, (await service.ExecuteAsync(new AssignNotebookTag(a.Id, a.Version, tag.Id))).Status);
        b = NotebookFixture.Success(await service.ExecuteAsync(new TrashNotebookNote(b.Id, b.Version)));
        Assert.Equal(NotebookWriteStatus.Invalid, (await service.ExecuteAsync(new RemoveNotebookTag(b.Id, b.Version, tag.Id))).Status);
        Assert.Equal(NotebookWriteStatus.Success, (await service.ExecuteCatalogAsync(new DeleteNotebookTag(tag.Id, tag.Version, true))).Status);
        Assert.Empty((await service.LoadAsync(b.Id)).Note!.TagIds);
    }

    [Fact]
    public async Task CombinedAndSearchScopeSortPagingCountsAndRestartUseDerivedText()
    {
        await using var f = await NotebookFixture.Create(); var service = f.Service();
        var folder = await f.Folder(); var a = await f.Tag("A"); var b = await f.Tag("B");
        var wanted = new List<Guid>();
        for (int i = 0; i < 6; i++)
        {
            var n = await f.CreateNote($"Note {i}", folder.Id);
            var doc = new NotebookDocument([NotebookNode.Paragraph(Guid.NewGuid(), [NotebookNode.Run(Guid.NewGuid(), "Searchable thesis")])]);
            n = NotebookFixture.Success(await service.ExecuteAsync(new SaveNotebookDraft(n.Id, n.Version, n.Title, doc, n.Images)));
            n = NotebookFixture.Success(await service.ExecuteAsync(new AssignNotebookTag(n.Id, n.Version, a.Id)));
            if (i != 5) n = NotebookFixture.Success(await service.ExecuteAsync(new AssignNotebookTag(n.Id, n.Version, b.Id)));
            if (i == 3) n = NotebookFixture.Success(await service.ExecuteAsync(new ArchiveNotebookNote(n.Id, n.Version)));
            else if (i == 4) n = NotebookFixture.Success(await service.ExecuteAsync(new TrashNotebookNote(n.Id, n.Version)));
            else if (i < 3) wanted.Add(n.Id);
        }
        var q = new NotebookQuery(NotebookScope.Folder, folder.Id, "THESIS", [a.Id, b.Id], NotebookSort.Title, 2);
        var first = await service.ListAsync(q);
        Assert.Equal(3, first.FilteredCount); Assert.Equal(4, first.Summary.ScopeCounts.Single(x => x.Scope == NotebookScope.Folder).Count);
        Assert.Equal(wanted.Take(2), first.Items.Select(x => x.Id)); Assert.NotNull(first.NextCursor);
        var last = await f.Service().ListAsync(new(NotebookScope.Folder, folder.Id, "THESIS", [b.Id, a.Id], NotebookSort.Title, 2, first.NextCursor));
        Assert.Equal(wanted[2], Assert.Single(last.Items).Id); Assert.Null(last.NextCursor); Assert.Equal(3, last.FilteredCount);
        foreach (var scope in new[] { NotebookScope.Archived, NotebookScope.Trash })
            Assert.Single((await service.ListAsync(new(scope, search: "thesis", tagIds: [a.Id, b.Id]))).Items);
        Assert.Empty((await service.ListAsync(new(search: "ptj.notebook.document"))).Items);
        Assert.Empty((await service.ListAsync(new(search: "thesis", tagIds: [Guid.NewGuid()]))).Items);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ListAsync(new(NotebookScope.Folder, folder.Id, "different", [a.Id], NotebookSort.Title, 2, first.NextCursor)));
    }

    [Fact]
    public async Task ConcurrentGlobalDeletionAndEditorSaveNeverResurrectAssignment()
    {
        await using var f = await NotebookFixture.Create(); var tag = await f.Tag();
        var note = await f.CreateNote();
        note = NotebookFixture.Success(await f.Service().ExecuteAsync(new AssignNotebookTag(note.Id, note.Version, tag.Id)));
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var save = Task.Run(async () => { await ready.Task; return await f.Service().ExecuteAsync(new SaveNotebookDraft(note.Id, note.Version, "Saved research", note.Document, note.Images)); });
        var delete = Task.Run(async () => { await ready.Task; return await f.Service().ExecuteCatalogAsync(new DeleteNotebookTag(tag.Id, tag.Version, true)); });
        try
        {
            ready.TrySetResult();
            await Task.WhenAll(save, delete).WaitAsync(TimeSpan.FromSeconds(10));
            var savedResult = await save; var deletedResult = await delete;
            Assert.Equal(NotebookWriteStatus.Success, deletedResult.Status);
            Assert.Contains(savedResult.Status, new[] { NotebookWriteStatus.Success, NotebookWriteStatus.Conflict });
            var stored = (await f.Service().LoadAsync(note.Id)).Note!;
            Assert.Empty(stored.TagIds); Assert.Empty((await f.Service().ListCatalogAsync(new(NotebookCatalogKind.Tags))).Items);
            Assert.Equal(savedResult.Status == NotebookWriteStatus.Success ? "Saved research" : note.Title, stored.Title);
            Assert.Equal(note.Version + (savedResult.Status == NotebookWriteStatus.Success ? 2 : 1), stored.Version);
        }
        finally { ready.TrySetResult(); await Task.WhenAll(save, delete).WaitAsync(TimeSpan.FromSeconds(10)); }
    }

    [Fact]
    public async Task GlobalDeleteWorksOnUnsupportedContentWithoutDecodingOrRewritingIt()
    {
        await using var f = await NotebookFixture.Create(); var tag = await f.Tag();
        var n = await f.CreateNote();
        n = NotebookFixture.Success(await f.Service().ExecuteAsync(new AssignNotebookTag(n.Id, n.Version, tag.Id)));
        await using var db = f.Context();
        const string future = "{\"format\":\"ptj.notebook.document\",\"version\":999}";
        await db.NotebookNotes.Where(x => x.Id == n.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.DocumentJson, future));
        Assert.Equal(NotebookWriteStatus.UnsupportedContent, (await f.Service().ExecuteAsync(new RemoveNotebookTag(n.Id, n.Version, tag.Id))).Status);
        Assert.Equal(NotebookWriteStatus.Success, (await f.Service().ExecuteCatalogAsync(new DeleteNotebookTag(tag.Id, tag.Version, true))).Status);
        var stored = await db.NotebookNotes.AsNoTracking().SingleAsync();
        Assert.Equal(future, stored.DocumentJson); Assert.Equal(n.Version + 1, stored.Version);
    }
}
