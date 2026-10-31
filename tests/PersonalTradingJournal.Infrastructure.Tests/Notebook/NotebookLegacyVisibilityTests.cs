using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using static PersonalTradingJournal.Infrastructure.Tests.Notebook.NotebookFixture;

namespace PersonalTradingJournal.Infrastructure.Tests.Notebook;

public sealed class NotebookLegacyVisibilityTests
{
    [Theory]
    [InlineData(NotebookSort.Title)]
    [InlineData(NotebookSort.RecentlyUpdated)]
    public async Task MixedPopulationCountsFiltersAndPagingAgreeAcrossFreshServices(NotebookSort sort)
    {
        await using var f = await NotebookFixture.Create();
        var folder = await f.Folder(); var tag = await f.Tag();
        var notes = new List<NotebookNoteSnapshot>();
        for (int i = 0; i < 6; i++)
        {
            var n = await f.CreateNote($"Research {i}", i < 3 ? folder.Id : null);
            n = Success(await f.Service().ExecuteAsync(new AssignNotebookTag(n.Id, n.Version, tag.Id)));
            n = Success(await f.Service().ExecuteAsync(new SetNotebookPin(n.Id, n.Version, true)));
            if (i % 3 == 1) n = Success(await f.Service().ExecuteAsync(new ArchiveNotebookNote(n.Id, n.Version)));
            if (i % 3 == 2) n = Success(await f.Service().ExecuteAsync(new TrashNotebookNote(n.Id, n.Version)));
            notes.Add(n);
        }
        foreach (var scope in new[] { NotebookScope.AllNotes, NotebookScope.Pinned, NotebookScope.Folder, NotebookScope.Trash })
        {
            var expected = notes.Where(n => scope == NotebookScope.Trash ? n.State == NotebookNoteState.Trashed : n.State != NotebookNoteState.Trashed)
                .Where(n => scope != NotebookScope.Folder || n.FolderId == folder.Id);
            expected = sort == NotebookSort.Title ? expected.OrderBy(n => n.Title) : expected.OrderByDescending(n => n.UpdatedAtUtc).ThenBy(n => n.Id.ToString(), StringComparer.Ordinal);
            var found = new List<Guid>(); string? cursor = null;
            do
            {
                var page = await f.Service().ListAsync(new(scope, scope == NotebookScope.Folder ? folder.Id : null, "RESEARCH", [tag.Id], sort, 1, cursor));
                Assert.Equal(expected.Count(), page.FilteredCount); Assert.Single(page.Items);
                Assert.Equal(4, page.Summary.ScopeCounts.Single(x => x.Scope == NotebookScope.AllNotes).Count);
                Assert.Equal(4, page.Summary.ScopeCounts.Single(x => x.Scope == NotebookScope.Pinned).Count);
                Assert.Equal(2, page.Summary.ScopeCounts.Single(x => x.Scope == NotebookScope.Trash).Count);
                Assert.DoesNotContain(page.Summary.ScopeCounts, x => x.Scope is NotebookScope.Archived or NotebookScope.Unfiled);
                Assert.Equal(2, Assert.Single(page.Summary.Folders.Items).ActiveNoteCount);
                Assert.Equal(4, Assert.Single(page.Summary.Tags.Items).ActiveNoteCount);
                Assert.Equal(4, Assert.Single(page.Summary.RequestedTags).ActiveNoteCount);
                found.Add(page.Items[0].Id); cursor = page.NextCursor;
            } while (cursor is not null && found.Count < 10);
            Assert.Null(cursor); Assert.Equal(expected.Select(n => n.Id), found); Assert.Equal(found.Count, found.Distinct().Count());
        }
    }

    [Fact]
    public async Task ArchivedRestrictionsRestoreAndFolderDeletionPreserveCanonicalData()
    {
        await using var f = await NotebookFixture.Create(); var folder = await f.Folder(); var tag = await f.Tag();
        var n = await f.CreateNote("Legacy", folder.Id);
        n = Success(await f.Service().ExecuteAsync(new UpdateNotebookContent(n.Id, n.Version, NotebookCodecTests.Rich(), [])));
        n = Success(await f.Service().ExecuteAsync(new AssignNotebookTag(n.Id, n.Version, tag.Id)));
        n = Success(await f.Service().ExecuteAsync(new SetNotebookPin(n.Id, n.Version, true)));
        n = Success(await f.Service().ExecuteAsync(new ArchiveNotebookNote(n.Id, n.Version)));
        string json;
        await using (var db = f.Context()) json = (await db.NotebookNotes.AsNoTracking().SingleAsync()).DocumentJson;
        foreach (var command in new NotebookCommand[] { new SaveNotebookDraft(n.Id, n.Version, "Forbidden", n.Document, n.Images),
            new MoveNotebookNote(n.Id, n.Version, null), new SetNotebookPin(n.Id, n.Version, false), new RemoveNotebookTag(n.Id, n.Version, tag.Id) })
            Assert.Equal(NotebookWriteStatus.Invalid, (await f.Service().ExecuteAsync(command)).Status);
        Assert.Equal(n.Version, (await f.Service().LoadAsync(n.Id)).Note!.Version);
        n = Success(await f.Service().ExecuteAsync(new TrashNotebookNote(n.Id, n.Version)));
        Assert.Empty((await f.Service().ListAsync(new())).Items);
        Assert.Equal(NotebookWriteStatus.Success, (await f.Service().ExecuteCatalogAsync(new DeleteNotebookFolder(folder.Id, folder.Version, true))).Status);
        n = (await f.Service().LoadAsync(n.Id)).Note!;
        Assert.Null(n.FolderId); Assert.Null(n.RestorePoint!.FolderId);
        n = Success(await f.Service().ExecuteAsync(new RestoreNotebookNote(n.Id, n.Version)));
        Assert.Equal(NotebookNoteState.Archived, n.State); Assert.Null(n.FolderId); Assert.True(n.IsPinned);
        Assert.Equal(tag.Id, Assert.Single(n.TagIds)); Assert.Equal(n.Id, Assert.Single((await f.Service().ListAsync(new())).Items).Id);
        await using (var db = f.Context()) Assert.Equal(json, (await db.NotebookNotes.AsNoTracking().SingleAsync()).DocumentJson);
        var copy = Success(await f.Service().ExecuteAsync(new DuplicateNotebookNote(n.Id, n.Version)));
        Assert.Equal(NotebookNoteState.Active, copy.State); Assert.NotEqual(n.Id, copy.Id); Assert.Equal(n.Document.PlainText, copy.Document.PlainText);
        Assert.Equal(NotebookNoteState.Archived, (await f.Service().LoadAsync(n.Id)).Note!.State);
    }
}
