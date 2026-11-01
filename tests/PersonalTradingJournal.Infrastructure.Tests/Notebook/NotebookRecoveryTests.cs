using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Notebook;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Notebook;

public sealed class NotebookRecoveryTests
{
    private static NotebookRecoveryDraft Draft(NotebookFixture f, Guid note, long version = 1) =>
        new(Guid.NewGuid(), note, version, 1, "Unsaved title", NotebookCodecTests.Rich(), f.Time.GetUtcNow());
    private static NotebookRecoveryStore Store(NotebookFixture f, params IInterceptor[] interceptors) => new(f.Factory(interceptors), f.Codec);
    [Fact]
    public async Task PreviousSchemaMigratesWithoutChangingCanonicalContentAndRestartRetainsCheckpoint()
    {
        await using var f = await NotebookFixture.Create("20261020085611_AddNotebook");
        var note = await f.CreateNote("Existing title");
        await using (var db = f.Context()) await db.Database.MigrateAsync();
        var draft = Draft(f, note.Id);
        Assert.Equal(NotebookWriteStatus.Success, await Store(f).PutAsync(draft, 0));
        var loaded = await Store(f).LoadAsync(draft.Id);
        Assert.Equal(NotebookLoadStatus.Success, loaded.Status);
        Assert.Equal(draft.Title, loaded.Draft!.Title); Assert.Equal(draft.ExpectedVersion, loaded.Draft.ExpectedVersion);
        Assert.Equal(f.Codec.Encode(draft.Document).Json, f.Codec.Encode(loaded.Draft.Document).Json);
        Assert.Equal("Existing title", (await f.Service().LoadAsync(note.Id)).Note!.Title);
        Assert.Equal(1, (await f.Service().LoadAsync(note.Id)).Note!.Version);
    }
    [Fact]
    public async Task AtomicReplacementRejectsStaleWriterAndOldCleanupAndCannotRecreateRemovedSession()
    {
        await using var f = await NotebookFixture.Create(); var note = await f.CreateNote(); var draft = Draft(f, note.Id);
        Assert.Equal(NotebookWriteStatus.Success, await Store(f).PutAsync(draft, 0));
        var newer = draft with { Revision = 2, Title = "Newer", ExpectedVersion = 2 };
        var results = await Task.WhenAll(
            Task.Run(() => Store(f).PutAsync(newer, 1)),
            Task.Run(() => Store(f).PutAsync(newer with { Title = "Competing" }, 1)));
        Assert.Single(results, x => x == NotebookWriteStatus.Success); Assert.Single(results, x => x == NotebookWriteStatus.Conflict);
        Assert.Equal(NotebookWriteStatus.Conflict, await Store(f).RemoveAsync(draft.Id, 1));
        Assert.Equal(2, (await Store(f).LoadAsync(draft.Id)).Draft!.Revision);
        Assert.Equal(NotebookWriteStatus.Conflict, await Store(f).PutAsync(newer with { Revision = 3, ExpectedVersion = 1 }, 2));
        Assert.Equal(NotebookWriteStatus.Success, await Store(f).RemoveAsync(draft.Id, 2));
        Assert.Equal(NotebookWriteStatus.Conflict, await Store(f).PutAsync(newer with { Revision = 3 }, 2));
        Assert.Equal(NotebookLoadStatus.NotFound, (await Store(f).LoadAsync(draft.Id)).Status);
        Assert.Equal(NotebookWriteStatus.Success, await Store(f).RemoveAsync(draft.Id, 2));
    }
    [Fact]
    public async Task FailedOrCancelledCheckpointRollsBackAndSourceDeletionNeverCascades()
    {
        await using var f = await NotebookFixture.Create(); var note = await f.CreateNote(); var draft = Draft(f, note.Id);
        Assert.Equal(NotebookWriteStatus.Success, await Store(f).PutAsync(draft, 0));
        Assert.Equal(NotebookWriteStatus.StorageFailure, await Store(f, new FailSave()).PutAsync(draft with { Title = "Failed", Revision = 2 }, 1));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Equal(NotebookWriteStatus.Cancelled, await Store(f).PutAsync(draft with { Title = "Cancelled", Revision = 2 }, 1, cancel.Token));
        await using (var db = f.Context()) { db.Remove(await db.NotebookNotes.SingleAsync()); await db.SaveChangesAsync(); }
        var retained = (await Store(f).LoadAsync(draft.Id)).Draft!;
        Assert.Equal(draft.Title, retained.Title); Assert.Equal(1, retained.Revision);
        Assert.Equal(NotebookLoadStatus.NotFound, (await f.Service().LoadAsync(note.Id)).Status);
    }
    private sealed class FailSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            throw new IOException("Synthetic checkpoint failure");
    }
    [Theory]
    [InlineData("malformed", NotebookLoadStatus.InvalidContent)]
    [InlineData("future-envelope", NotebookLoadStatus.UnsupportedContent)]
    [InlineData("future-body", NotebookLoadStatus.UnsupportedContent)]
    [InlineData("oversized", NotebookLoadStatus.TooLarge)]
    public async Task UnreadableAndFutureRecordsRemainUntouched(string variant, NotebookLoadStatus expected)
    {
        await using var f = await NotebookFixture.Create(); var note = await f.CreateNote();
        var row = new NotebookRecoveryRecord { Id = Guid.NewGuid(), NoteId = note.Id, ExpectedVersion = note.Version, Revision = 1,
            Title = "Protected", DocumentJson = variant == "future-body" ? """{"format":"ptj.notebook.document","version":99,"blocks":[]}""" :
            variant == "oversized" ? new string('x', 1024 * 1024 + 1) : "malformed", EnvelopeVersion = variant == "future-envelope" ? 99 : 1, CheckpointedAtUtc = f.Time.GetUtcNow() };
        await using (var db = f.Context()) { db.Add(row); await db.SaveChangesAsync(); }
        Assert.Equal(expected, (await Store(f).LoadAsync(row.Id)).Status);
        Assert.Single((await Store(f).ListAsync()).Items);
        await using var check = f.Context();
        Assert.Equal(row.DocumentJson, (await check.NotebookRecoveryDrafts.SingleAsync()).DocumentJson);
    }
    [Fact]
    public async Task CheckpointListIsBoundedStableAndRefusesSilentEvictionAtCapacity()
    {
        await using var f = await NotebookFixture.Create(); var note = await f.CreateNote();
        var drafts = Enumerable.Range(0, 128).Select(_ => Draft(f, note.Id)).ToArray();
        await using (var db = f.Context())
        {
            db.NotebookRecoveryDrafts.AddRange(drafts.Select(d => new NotebookRecoveryRecord { Id = d.Id, NoteId = d.NoteId,
                ExpectedVersion = 1, Revision = 1, Title = d.Title, DocumentJson = f.Codec.Encode(d.Document).Json, CheckpointedAtUtc = d.CheckpointedAtUtc }));
            await db.SaveChangesAsync();
        }
        var pages = new List<Guid>(); Guid? cursor = null;
        do { var page = await Store(f).ListAsync(cursor); Assert.InRange(page.Items.Length, 1, 50); pages.AddRange(page.Items.Select(x => x.Id)); cursor = page.Next; } while (cursor is not null);
        Assert.Equal(128, pages.Count); Assert.Equal(128, pages.Distinct().Count());
        await using (var db = f.Context()) Assert.Equal(await db.NotebookRecoveryDrafts.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync(), pages);
        Assert.Equal(NotebookWriteStatus.TooLarge, await Store(f).PutAsync(Draft(f, note.Id), 0));
        Assert.Equal(NotebookWriteStatus.Success, await Store(f).PutAsync(drafts[0] with { Title = "", Revision = 2 }, 1));
        Assert.Equal("", (await Store(f).LoadAsync(drafts[0].Id)).Draft!.Title);
    }
    [Fact]
    public async Task CanonicalCommitWithoutCleanupIsRecognizableFromFrozenCheckpoint()
    {
        await using var f = await NotebookFixture.Create(); var note = await f.CreateNote(); var draft = Draft(f, note.Id);
        Assert.Equal(NotebookWriteStatus.Success, await Store(f).PutAsync(draft, 0));
        var saved = NotebookFixture.Success(await f.Service().ExecuteAsync(new SaveNotebookDraft(note.Id, note.Version, draft.Title, draft.Document, [])));
        // Simulate process interruption before cleanup, then use fresh service/store instances.
        var recovered = (await Store(f).LoadAsync(draft.Id)).Draft!;
        var read = (await f.Service().LoadAsync(note.Id)).Note!;
        Assert.Equal(saved.Version, read.Version); Assert.Equal(recovered.Title, read.Title);
        Assert.Equal(f.Codec.Encode(recovered.Document).Json, f.Codec.Encode(read.Document).Json);
        Assert.Equal(NotebookWriteStatus.Success, await Store(f).RemoveAsync(draft.Id, recovered.Revision));
        Assert.Empty((await Store(f).ListAsync()).Items);
    }
}
