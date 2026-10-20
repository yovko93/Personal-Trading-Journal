using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Application.Tests.Notebook;

public sealed class NotebookContractTests
{
    [Theory]
    [InlineData(NotebookScope.AllNotes)]
    [InlineData(NotebookScope.Pinned)]
    [InlineData(NotebookScope.Unfiled)]
    [InlineData(NotebookScope.Archived)]
    [InlineData(NotebookScope.Trash)]
    public void NonFolderScopesRejectAnAccidentalFolderIdentity(NotebookScope scope)
    {
        Assert.Throws<ArgumentException>(() => new NotebookQuery(scope, Guid.NewGuid()));
        Assert.Null(new NotebookQuery(scope).FolderId);
    }

    [Fact]
    public void FolderFilterAndBoundedTagSetAreValidatedAndCopied()
    {
        Assert.Throws<ArgumentException>(() => new NotebookQuery(NotebookScope.Folder));
        Assert.Throws<ArgumentException>(() => new NotebookQuery(NotebookScope.Folder, Guid.Empty));
        var ids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var query = new NotebookQuery(NotebookScope.Folder, Guid.NewGuid(), "Research", ids);
        ids.Clear(); Assert.Equal(2, query.TagIds.Length);
        Assert.Throws<ArgumentException>(() => new NotebookQuery(tagIds: [query.TagIds[0], query.TagIds[0]]));
        Assert.Throws<ArgumentException>(() => new NotebookQuery(tagIds: [Guid.Empty]));
        Assert.Throws<ArgumentException>(() => new NotebookQuery(tagIds: Enumerable.Range(0, 33).Select(_ => Guid.NewGuid())));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void PagingRejectsUnboundedRequests(int limit) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new NotebookQuery(limit: limit));

    [Fact]
    public void CatalogContinuationIsAlsoBoundedAndTyped()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NotebookCatalogQuery((NotebookCatalogKind)3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NotebookCatalogQuery(NotebookCatalogKind.Folders, 101));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NotebookCatalogQuery(NotebookCatalogKind.Tags, 0));
        Assert.Throws<ArgumentException>(() => new NotebookCatalogQuery(NotebookCatalogKind.Tags, cursor: ""));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NotebookCatalogQuery(NotebookCatalogKind.Tags, cursor: new string('a', 2049)));
        Assert.Equal(100, new NotebookCatalogQuery(NotebookCatalogKind.Folders, 100).Limit);
    }
    [Fact]
    public void SearchCursorAndSortValidationCannotChangeScopeImplicitly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NotebookQuery(search: new string('a', 257)));
        Assert.Throws<ArgumentException>(() => new NotebookQuery(search: "\ud800"));
        Assert.Throws<ArgumentException>(() => new NotebookQuery(cursor: ""));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NotebookQuery(cursor: new string('a', 2049)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NotebookQuery(scope: (NotebookScope)100));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NotebookQuery(sort: (NotebookSort)0));
        Assert.Equal(100, new NotebookQuery(limit: 100).Limit);
    }

    [Fact]
    public void PublishedSnapshotCannotBeChangedByLaterAggregateEdits()
    {
        var now = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
        var note = new NotebookNote(now); var tag = Guid.NewGuid();
        note.AssignTag(tag, now);
        note.AddLink(new(NotebookTargetKind.TradingSetup, Guid.NewGuid(), "Original Setup"), now);
        var snapshot = NotebookNoteSnapshot.From(note);
        note.RemoveTag(tag, now); note.UpdateTitle("Later", now); note.Trash(now);
        Assert.Equal("Untitled", snapshot.Title); Assert.Equal(NotebookNoteState.Active, snapshot.State);
        Assert.Equal(tag, Assert.Single(snapshot.TagIds)); Assert.Single(snapshot.Links);
        Assert.True(note.Version > snapshot.Version);
    }
}
