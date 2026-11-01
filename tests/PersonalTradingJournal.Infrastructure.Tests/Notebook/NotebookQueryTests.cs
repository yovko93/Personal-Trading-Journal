using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using static PersonalTradingJournal.Infrastructure.Tests.Notebook.NotebookFixture;

namespace PersonalTradingJournal.Infrastructure.Tests.Notebook;

public sealed class NotebookQueryTests
{
    [Fact]
    public async Task ConcurrentCommittedWriteCannotSplitListRowsFromCounts()
    {
        await using var f = await NotebookFixture.Create();
        await using (var db = f.Context()) await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        var boundary = new ReadBoundary();
        var read = f.Service(boundary).ListAsync(new());
        try
        {
            await boundary.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await f.CreateNote("Committed during read");
            boundary.Release.TrySetResult();
            var page = await read.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Empty(page.Items); Assert.Equal(0, page.FilteredCount);
            Assert.All(page.Summary.ScopeCounts, x => Assert.Equal(0, x.Count));
            Assert.Single((await f.Service().ListAsync(new())).Items);
        }
        finally
        {
            boundary.Release.TrySetResult();
            await read.WaitAsync(TimeSpan.FromSeconds(30));
        }
    }
    private sealed class ReadBoundary : DbCommandInterceptor
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int reads;
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref reads) == 2)
            { Entered.TrySetResult(); await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken); }
            return result;
        }
    }
    [Fact]
    public async Task ScopesSearchAndTagsIncludeArchivedButExcludeTrashedNotes()
    {
        await using var f = await NotebookFixture.Create();
        var folder = await f.Folder(); var tag = await f.Tag(); var second = await f.Tag("ICT");
        var note = await f.CreateNote("Café", folder.Id);
        note = Success(await f.Service().ExecuteAsync(new UpdateNotebookContent(note.Id, note.Version, new([NotebookCodecTests.P("Синтетичен research")]), [])));
        note = Success(await f.Service().ExecuteAsync(new AssignNotebookTag(note.Id, note.Version, tag.Id)));
        note = Success(await f.Service().ExecuteAsync(new AssignNotebookTag(note.Id, note.Version, second.Id)));
        Assert.Equal(NotebookWriteStatus.Invalid, (await f.Service().ExecuteAsync(new AssignNotebookTag(note.Id, note.Version, tag.Id))).Status);
        note = Success(await f.Service().ExecuteAsync(new SetNotebookPin(note.Id, note.Version, true)));
        var unfiled = await f.CreateNote("Other");
        var archived = await f.CreateNote(folder: folder.Id);
        archived = Success(await f.Service().ExecuteAsync(new ArchiveNotebookNote(archived.Id, archived.Version)));
        var trash = await f.CreateNote(folder: folder.Id);
        trash = Success(await f.Service().ExecuteAsync(new TrashNotebookNote(trash.Id, trash.Version)));
        foreach (var scope in new[] { NotebookScope.AllNotes, NotebookScope.Pinned, NotebookScope.Folder })
        {
            var page = await f.Service().ListAsync(new(scope, scope == NotebookScope.Folder ? folder.Id : null, "СИНТЕТИЧЕН", [tag.Id, second.Id]));
            Assert.Equal(note.Id, Assert.Single(page.Items).Id); Assert.Equal(1, page.FilteredCount);
            Assert.Equal(3, page.Summary.ScopeCounts.Single(x => x.Scope == NotebookScope.AllNotes).Count);
            Assert.Equal(2, Assert.Single(page.Summary.Folders.Items).ActiveNoteCount);
            Assert.All(page.Summary.Tags.Items, x => Assert.Equal(1, x.ActiveNoteCount));
        }
        Assert.Equal(note.Id, Assert.Single((await f.Service().ListAsync(new(search: "CAFE\u0301"))).Items).Id);
        Assert.Empty((await f.Service().ListAsync(new(tagIds: [Guid.NewGuid()]))).Items);
        foreach (var legacy in new[] { NotebookScope.Unfiled, NotebookScope.Archived })
            Assert.Equal(new[] { note.Id, unfiled.Id, archived.Id }.Order(), (await f.Service().ListAsync(new(legacy))).Items.Select(x => x.Id).Order());
        Assert.Equal(trash.Id, Assert.Single((await f.Service().ListAsync(new(NotebookScope.Trash))).Items).Id);
    }

    [Theory]
    [InlineData(NotebookSort.Title)]
    [InlineData(NotebookSort.RecentlyUpdated)]
    public async Task ExactFinalPagesStableTiesAndRestartDoNotOmitOrRepeatRows(NotebookSort sort)
    {
        await using var f = await NotebookFixture.Create();
        var notes = new List<NotebookNoteSnapshot>();
        foreach (string title in new[] { "Zulu", "alpha", "ALPHA", "Álpha", "Beta", "beta" }) notes.Add(await f.CreateNote(title));
        for (int i = 1; i < notes.Count; i += 2)
            notes[i] = Success(await f.Service().ExecuteAsync(new ArchiveNotebookNote(notes[i].Id, notes[i].Version)));
        var ids = new List<Guid>(); string? cursor = null;
        do
        {
            var page = await f.Service().ListAsync(new(sort: sort, limit: 2, cursor: cursor));
            Assert.Equal(2, page.Items.Length); Assert.Equal(6, page.FilteredCount);
            ids.AddRange(page.Items.Select(x => x.Id)); cursor = page.NextCursor;
        } while (cursor is not null && ids.Count < 10);
        Assert.Equal(6, ids.Distinct().Count());
        var expected = sort == NotebookSort.Title
            ? notes.OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Id.ToString(), StringComparer.Ordinal)
            : notes.OrderByDescending(x => x.UpdatedAtUtc).ThenBy(x => x.Id.ToString(), StringComparer.Ordinal);
        Assert.Equal(expected.Select(x => x.Id), ids);
        var first = await f.Service().ListAsync(new(sort: sort, limit: 2));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service().ListAsync(new(search: "other", sort: sort, cursor: first.NextCursor)));
    }

    [Fact]
    public async Task CatalogPagingAndListProjectionRemainBoundedEvenWithUnreadableBodies()
    {
        await using var f = await NotebookFixture.Create();
        foreach (string name in new[] { "A", "B", "C" }) { await f.Folder(name); await f.Tag(name); }
        foreach (var kind in new[] { NotebookCatalogKind.Folders, NotebookCatalogKind.Tags })
        {
            var first = await f.Service().ListCatalogAsync(new(kind, 2));
            Assert.Equal(3, first.TotalCount); Assert.Equal(2, first.Items.Length);
            var last = await f.Service().ListCatalogAsync(new(kind, 2, first.NextCursor));
            Assert.Equal("C", Assert.Single(last.Items).Item.Name); Assert.Null(last.NextCursor);
            await Assert.ThrowsAsync<ArgumentException>(() => f.Service().ListCatalogAsync(new(kind == NotebookCatalogKind.Tags ? NotebookCatalogKind.Folders : NotebookCatalogKind.Tags, cursor: first.NextCursor)));
        }
        var note = await f.CreateNote();
        note = Success(await f.Service().ExecuteAsync(new UpdateNotebookContent(note.Id, note.Version, new([NotebookCodecTests.P(new string('x', 300))]), [])));
        await using (var db = f.Context()) await db.NotebookNotes.Where(x => x.Id == note.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.DocumentJson, "{invalid"));
        var commands = new Commands();
        var page = await f.Service(commands).ListAsync(new());
        Assert.Equal(180, Assert.Single(page.Items).Preview.Length);
        Assert.DoesNotContain(commands.Sql, sql => sql.Contains("DocumentJson", StringComparison.Ordinal));
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Service().ListAsync(new(), cts.Token));
    }
    private sealed class Commands : DbCommandInterceptor
    {
        internal List<string> Sql { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Sql.Add(command.CommandText); return ValueTask.FromResult(result); }
    }
}
