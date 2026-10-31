using System.Collections.Immutable;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Notebook;
using PersonalTradingJournal.Infrastructure.Persistence.Records;
using static PersonalTradingJournal.Infrastructure.Tests.Notebook.NotebookFixture;

namespace PersonalTradingJournal.Infrastructure.Tests.Notebook;

public sealed class NotebookPersistenceTests
{
    [Fact]
    public async Task EditorSaveIsOneVersionedTransactionIncludingTitleAndProjection()
    {
        await using var f = await NotebookFixture.Create();
        var original = await f.CreateNote();
        var document = NotebookCodecTests.Rich();
        using var cancel = new CancellationTokenSource();
        var rolledBack = await f.Service(new AfterSave(() => cancel.Cancel())).ExecuteAsync(
            new SaveNotebookDraft(original.Id, original.Version, "Must roll back", document, []), cancel.Token);
        Assert.Equal(NotebookWriteStatus.Cancelled, rolledBack.Status);
        var unchanged = (await f.Service().LoadAsync(original.Id)).Note!;
        Assert.Equal(original.Title, unchanged.Title); Assert.Equal(original.Version, unchanged.Version);
        Assert.Equal(original.Document.PlainText, unchanged.Document.PlainText);
        var attempts = await Task.WhenAll(
            f.Service().ExecuteAsync(new SaveNotebookDraft(original.Id, original.Version, "First", document, [])),
            f.Service().ExecuteAsync(new SaveNotebookDraft(original.Id, original.Version, "Second", document, [])));
        var winner = Assert.Single(attempts, r => r.Status == NotebookWriteStatus.Success).Note!;
        Assert.Single(attempts, r => r.Status == NotebookWriteStatus.Conflict);
        Assert.Equal(original.Version + 1, winner.Version);
        var stored = (await f.Service().LoadAsync(original.Id)).Note!;
        Assert.Equal(winner.Title, stored.Title); Assert.Equal(f.Codec.Encode(document), f.Codec.Encode(stored.Document));
        var archived = Success(await f.Service().ExecuteAsync(new ArchiveNotebookNote(stored.Id, stored.Version)));
        Assert.Equal(NotebookWriteStatus.Invalid, (await f.Service().ExecuteAsync(new SaveNotebookDraft(archived.Id, archived.Version, "Forbidden", document, []))).Status);
    }

    [Fact]
    public async Task CancellationAfterCommitReturnsSuccessAndCatalogCancellationBeforeCommitRestoresReferences()
    {
        await using var f = await NotebookFixture.Create(); var folder = await f.Folder();
        var note = await f.CreateNote(folder: folder.Id);
        using var before = new CancellationTokenSource();
        var cancelled = await f.Service(new AfterSave(() => before.Cancel()))
            .ExecuteCatalogAsync(new DeleteNotebookFolder(folder.Id, folder.Version, true), before.Token);
        Assert.Equal(NotebookWriteStatus.Cancelled, cancelled.Status);
        var unchanged = (await f.Service().LoadAsync(note.Id)).Note!;
        Assert.Equal(note.Version, unchanged.Version); Assert.Equal(folder.Id, unchanged.FolderId);
        Assert.Single((await f.Service().ListCatalogAsync(new(NotebookCatalogKind.Folders))).Items);
        using var after = new CancellationTokenSource();
        var result = await f.Service(new AfterCommit(after)).ExecuteAsync(new UpdateNotebookTitle(note.Id, note.Version, "Committed"), after.Token);
        Assert.True(after.IsCancellationRequested); var committed = Success(result);
        Assert.Equal("Committed", (await f.Service().LoadAsync(note.Id)).Note!.Title);
        Assert.Equal(note.Version + 1, committed.Version);
    }
    private sealed class AfterCommit(CancellationTokenSource cts) : DbTransactionInterceptor
    {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        { cts.Cancel(); return Task.CompletedTask; }
    }
    [Fact]
    public async Task LifecycleAuditContentTagsAndRestoreSurviveFreshConnections()
    {
        await using var f = await NotebookFixture.Create(); var folder = await f.Folder(); var tag = await f.Tag();
        var note = await f.CreateNote("Research", folder.Id); var created = note.CreatedAtUtc;
        f.Time.Now = f.Time.Now.AddMinutes(2);
        note = Success(await f.Service().ExecuteAsync(new UpdateNotebookContent(note.Id, note.Version, NotebookCodecTests.Rich(), [])));
        note = Success(await f.Service().ExecuteAsync(new AssignNotebookTag(note.Id, note.Version, tag.Id)));
        note = Success(await f.Service().ExecuteAsync(new SetNotebookPin(note.Id, note.Version, true)));
        note = Success(await f.Service().ExecuteAsync(new ArchiveNotebookNote(note.Id, note.Version)));
        note = Success(await f.Service().ExecuteAsync(new TrashNotebookNote(note.Id, note.Version)));
        var loaded = (await f.Service().LoadAsync(note.Id)).Note!;
        Assert.Equal(note.Version, loaded.Version); Assert.Equal(created, loaded.CreatedAtUtc); Assert.Equal(f.Time.Now, loaded.UpdatedAtUtc);
        Assert.Equal(note.RestorePoint, loaded.RestorePoint); Assert.Equal(NotebookNoteState.Archived, loaded.RestorePoint!.PreviousState);
        Assert.Equal(f.Codec.Encode(note.Document), f.Codec.Encode(loaded.Document)); Assert.True(loaded.IsPinned);
        Assert.Equal(tag.Id, Assert.Single(loaded.TagIds));
        Assert.Equal(NotebookWriteStatus.Success, (await f.Service().ExecuteCatalogAsync(new DeleteNotebookFolder(folder.Id, folder.Version, true))).Status);
        loaded = (await f.Service().LoadAsync(note.Id)).Note!;
        Assert.Null(loaded.FolderId); Assert.Null(loaded.RestorePoint!.FolderId);
        loaded = Success(await f.Service().ExecuteAsync(new RestoreNotebookNote(loaded.Id, loaded.Version)));
        Assert.Equal(NotebookNoteState.Archived, loaded.State);
        Assert.Equal(NotebookWriteStatus.Invalid, (await f.Service().ExecuteAsync(new UpdateNotebookTitle(loaded.Id, loaded.Version, "Forbidden"))).Status);
    }
    [Fact]
    public async Task UnicodeUniquenessUsesDomainNormalizationNotSqliteAsciiNoCase()
    {
        await using var f = await NotebookFixture.Create();
        var a = await f.Folder(" Cafe\u0301 ");
        Assert.Equal("Café", a.Name);
        Assert.Equal(NotebookWriteStatus.Invalid, (await f.Service().ExecuteCatalogAsync(new CreateNotebookFolder("CAFÉ"))).Status);
        var t = await f.Tag("Σ");
        Assert.Equal(NotebookWriteStatus.Invalid, (await f.Service().ExecuteCatalogAsync(new CreateNotebookTag("ς"))).Status);
        var other = await f.Tag("Other");
        Assert.Equal(NotebookWriteStatus.Invalid, (await f.Service().ExecuteCatalogAsync(new RenameNotebookTag(other.Id, other.Version, "σ"))).Status);
        Assert.Equal(NotebookWriteStatus.Conflict, (await f.Service().ExecuteCatalogAsync(new RenameNotebookTag(t.Id, 999, "Changed"))).Status);
        await using var db = f.Context();
        var row = await db.NotebookFolders.AsNoTracking().SingleAsync();
        db.NotebookFolders.Add(new() { Id = Guid.NewGuid(), Name = "CAFÉ", NameKey = row.NameKey, Version = 1, CreatedAtUtc = f.Time.Now, UpdatedAtUtc = f.Time.Now });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
    [Fact]
    public async Task ImagesRoundTripWithoutClaimingManagedFileOperationsAreImplemented()
    {
        await using var f = await NotebookFixture.Create();
        var owned = NotebookImage.Owned(Guid.NewGuid(), 0, "synthetic.png", 20, new string('A', 64), "Chart", "Alt");
        var reference = NotebookImage.TradeReference(Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), "Reference");
        var doc = new NotebookDocument([.. NotebookCodecTests.Rich(owned.Id).Blocks, NotebookNode.Image(Guid.NewGuid(), reference.Id, reference.Caption)]);
        var note = new NotebookNote(f.Time.Now); note.UpdateContent(doc, [owned, reference], f.Time.Now);
        await using (var db = f.Context())
        {
            var row = new NotebookNoteRecord(); NotebookMapper.Apply(row, note, f.Codec.Encode(doc));
            db.NotebookNotes.Add(row); db.NotebookImages.AddRange(note.Images.Select(x => NotebookMapper.Image(note.Id, x))); await db.SaveChangesAsync();
        }
        var loaded = (await f.Service().LoadAsync(note.Id)).Note!;
        Assert.Equal(note.Images, loaded.Images); Assert.Equal(note.Version, loaded.Version);
        Assert.Null(loaded.Images[1].StorageKey);
        var edited = Success(await f.Service().ExecuteAsync(new UpdateNotebookTitle(note.Id, note.Version, "Metadata only")));
        Assert.Equal(loaded.Images.ToArray(), edited.Images.ToArray());
        edited = Success(await f.Service().ExecuteAsync(new SaveNotebookDraft(note.Id, edited.Version, "Protected editor save", doc, loaded.Images)));
        Assert.Equal(loaded.Images.ToArray(), edited.Images.ToArray());
        Assert.Equal(f.Codec.Encode(doc), f.Codec.Encode(edited.Document));
        Assert.Equal(NotebookWriteStatus.StorageFailure, (await f.Service().ExecuteAsync(new DuplicateNotebookNote(note.Id, edited.Version))).Status); // No image file port.
        Assert.Equal(NotebookWriteStatus.Invalid, (await f.Service().ExecuteAsync(new DeleteNotebookNote(note.Id, edited.Version, true))).Status); // Active is not deletable.
        Assert.Equal(NotebookWriteStatus.Deferred, (await f.Service().ExecuteAsync(new UpdateNotebookContent(note.Id, edited.Version, NotebookDocument.Empty, []))).Status);
        Assert.Equal(NotebookWriteStatus.Invalid, (await f.Service().ExecuteAsync(new AddNotebookImage(note.Id, edited.Version, Guid.NewGuid(), doc, loaded.Images))).Status);
        Assert.Equal(NotebookWriteStatus.Invalid, (await f.Service().ExecuteAsync(new RemoveNotebookImage(note.Id, edited.Version, owned.Id, NotebookDocument.Empty, []))).Status);
        Assert.Equal(edited.Version, (await f.Service().LoadAsync(note.Id)).Note!.Version);
    }
    [Theory]
    [InlineData("{bad", NotebookLoadStatus.InvalidContent)]
    [InlineData("{\"format\":\"ptj.notebook.document\",\"version\":8,\"blocks\":[]}", NotebookLoadStatus.UnsupportedContent)]
    public async Task UnreadableContentIsRetainedThroughReadsRejectedWritesAndCatalogMaintenance(string original, NotebookLoadStatus status)
    {
        await using var f = await NotebookFixture.Create(); var folder = await f.Folder(); var tag = await f.Tag();
        var note = await f.CreateNote(folder: folder.Id);
        note = Success(await f.Service().ExecuteAsync(new AssignNotebookTag(note.Id, note.Version, tag.Id)));
        await using (var db = f.Context()) await db.NotebookNotes.Where(x => x.Id == note.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.DocumentJson, original));
        var load = await f.Service().LoadAsync(note.Id);
        Assert.Equal(status, load.Status); Assert.Null(load.Note); Assert.Equal(original, load.OriginalContent);
        Assert.Equal(note.Id, load.UnavailableMetadata!.Id);
        Assert.Equal(NotebookWriteStatus.UnsupportedContent, (await f.Service().ExecuteAsync(new UpdateNotebookTitle(note.Id, note.Version, "Not overwritten"))).Status);
        Assert.Equal(NotebookWriteStatus.UnsupportedContent, (await f.Service().ExecuteAsync(new SaveNotebookDraft(note.Id, note.Version, "Not replaced", NotebookDocument.Empty, []))).Status);
        Assert.Equal(NotebookWriteStatus.Success, (await f.Service().ExecuteCatalogAsync(new DeleteNotebookTag(tag.Id, tag.Version, true))).Status);
        Assert.Equal(NotebookWriteStatus.Success, (await f.Service().ExecuteCatalogAsync(new DeleteNotebookFolder(folder.Id, folder.Version, true))).Status);
        await using var read = f.Context(); var row = await read.NotebookNotes.SingleAsync();
        Assert.Equal(original, row.DocumentJson); Assert.Equal(note.Title, row.Title); Assert.Null(row.FolderId); Assert.Empty(await read.NotebookTagAssignments.ToArrayAsync());
    }
    [Fact]
    public async Task LinkedSetupRenameAndDeletionNeverRetargetOrEraseNotes()
    {
        await using var f = await NotebookFixture.Create(); var target = Guid.NewGuid();
        await using (var db = f.Context()) { db.TradingSetups.Add(new() { Id = target, Name = "Original", IsActive = true, CreatedAtUtc = f.Time.Now, UpdatedAtUtc = f.Time.Now }); await db.SaveChangesAsync(); }
        var note = await f.CreateNote(); var link = new NotebookLink(NotebookTargetKind.TradingSetup, target, "Original");
        note = Success(await f.Service().ExecuteAsync(new AddNotebookLink(note.Id, note.Version, link)));
        Assert.Equal(NotebookWriteStatus.Invalid, (await f.Service().ExecuteAsync(new AddNotebookLink(note.Id, note.Version, link))).Status);
        await using (var db = f.Context()) await db.TradingSetups.Where(x => x.Id == target).ExecuteUpdateAsync(s => s.SetProperty(x => x.Name, "Renamed"));
        Assert.Equal("Renamed", Assert.Single(await f.Service().ResolveLinksAsync([link])).DisplayLabel);
        await using (var db = f.Context()) await db.TradingSetups.Where(x => x.Id == target).ExecuteDeleteAsync();
        Assert.Equal(NotebookTargetAvailability.Unavailable, Assert.Single(await f.Service().ResolveLinksAsync([link])).Availability);
        Assert.Single((await f.Service().LoadAsync(note.Id)).Note!.Links);
        Assert.Equal(NotebookWriteStatus.UnavailableTarget, (await f.Service().ExecuteAsync(new AddNotebookLink(note.Id, note.Version, new(NotebookTargetKind.Trade, Guid.NewGuid(), "Missing")))).Status);
        note = Success(await f.Service().ExecuteAsync(new RemoveNotebookLink(note.Id, note.Version, link.Kind, target)));
        Assert.Empty(note.Links);
    }
    [Fact]
    public async Task ConcurrentExpectedVersionWritesHaveExactlyOneWinner()
    {
        await using var f = await NotebookFixture.Create(); var note = await f.CreateNote();
        var tasks = new[] { "A", "B" }.Select(title => Task.Run(() => f.Service().ExecuteAsync(new UpdateNotebookTitle(note.Id, note.Version, title)))).ToArray();
        var results = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30));
        var winner = Assert.Single(results, x => x.Status == NotebookWriteStatus.Success);
        Assert.Single(results, x => x.Status == NotebookWriteStatus.Conflict);
        var loaded = (await f.Service().LoadAsync(note.Id)).Note!;
        Assert.Equal(winner.Note!.Title, loaded.Title); Assert.Equal(2, loaded.Version);
    }
    private sealed class AfterSave(Action action) : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        { action(); return ValueTask.FromResult(result); }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ControlledFailureOrCancellationAfterDatabaseWriteRollsBackEntireAggregate(bool cancel)
    {
        await using var f = await NotebookFixture.Create(); var note = await f.CreateNote(); var tag = await f.Tag();
        using var cts = new CancellationTokenSource();
        var service = f.Service(new AfterSave(() => { if (cancel) cts.Cancel(); else throw new IOException("Synthetic failure after SQL."); }));
        var result = await service.ExecuteAsync(new AssignNotebookTag(note.Id, note.Version, tag.Id), cts.Token);
        Assert.Equal(cancel ? NotebookWriteStatus.Cancelled : NotebookWriteStatus.StorageFailure, result.Status);
        var loaded = (await f.Service().LoadAsync(note.Id)).Note!;
        Assert.Equal(note.Version, loaded.Version); Assert.Equal(note.UpdatedAtUtc, loaded.UpdatedAtUtc); Assert.Empty(loaded.TagIds);
        result = await service.ExecuteAsync(new UpdateNotebookContent(note.Id, note.Version, NotebookCodecTests.Rich(), []), cancel ? new(true) : default);
        Assert.Equal(cancel ? NotebookWriteStatus.Cancelled : NotebookWriteStatus.StorageFailure, result.Status);
        loaded = (await f.Service().LoadAsync(note.Id)).Note!; Assert.Empty(loaded.Document.PlainText); Assert.Equal(note.Version, loaded.Version);
        var success = Success(await f.Service().ExecuteAsync(new SetNotebookPin(note.Id, note.Version, true)));
        cts.Cancel(); Assert.True((await f.Service().LoadAsync(success.Id)).Note!.IsPinned);
    }
    [Fact]
    public async Task StaleWritesAndTooLargeContentLeaveEveryStoredFieldUnchanged()
    {
        await using var f = await NotebookFixture.Create(); var note = await f.CreateNote(); var tag = await f.Tag();
        note = Success(await f.Service().ExecuteAsync(new SetNotebookPin(note.Id, note.Version, true)));
        Assert.Equal(NotebookWriteStatus.Conflict, (await f.Service().ExecuteAsync(new AssignNotebookTag(note.Id, 1, tag.Id))).Status);
        Assert.Equal(NotebookWriteStatus.Conflict, (await f.Service().ExecuteAsync(new TrashNotebookNote(note.Id, 1))).Status);
        Assert.Equal(NotebookWriteStatus.Conflict, (await f.Service().ExecuteAsync(new UpdateNotebookContent(note.Id, 1, NotebookCodecTests.Rich(), []))).Status);
        Assert.Equal(NotebookWriteStatus.TooLarge, (await f.Service().ExecuteAsync(new UpdateNotebookContent(note.Id, note.Version, new([NotebookCodecTests.P(new string('中', 199999))]), []))).Status);
        var loaded = (await f.Service().LoadAsync(note.Id)).Note!;
        Assert.Equal(note.Version, loaded.Version); Assert.Equal(note.UpdatedAtUtc, loaded.UpdatedAtUtc);
        Assert.Empty(loaded.TagIds); Assert.Empty(loaded.Document.PlainText); Assert.True(loaded.IsPinned);
    }
}
