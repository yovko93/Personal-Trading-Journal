using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Infrastructure.Persistence.Records;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Notebook;
using PersonalTradingJournal.Infrastructure.Storage;
using static PersonalTradingJournal.Infrastructure.Tests.Notebook.NotebookFixture;

namespace PersonalTradingJournal.Infrastructure.Tests.Notebook;

public sealed class NotebookLinkTests
{
    private static async Task<Guid> Seed(NotebookFixture f, int setups = 1)
    {
        await using var db = f.Context(); var id = Guid.NewGuid(); var account = Guid.NewGuid(); var instrument = Guid.NewGuid();
        db.TradingAccounts.Add(new() { Id = account, Name = "Research account", Currency = "USD", AccountType = TradingAccountType.Personal,
            IsActive = false, CreatedAtUtc = f.Time.Now, UpdatedAtUtc = f.Time.Now });
        db.Instruments.Add(new() { Id = instrument, Symbol = "NQ", DisplayName = "Nasdaq", AssetClass = AssetClass.Futures, Currency = "USD",
            TickSize = .25m, TickValue = 5, IsActive = false, CreatedAtUtc = f.Time.Now, UpdatedAtUtc = f.Time.Now });
        var trade = Trade.Rehydrate(id, account, instrument, new(20, "USD"), null,
            [new(id, 1, f.Time.Now.AddHours(-1), ExecutionSide.Buy, 1, 100, 0, 0, null, null, null)], f.Time.Now, f.Time.Now);
        db.Trades.Add(TradePersistenceMapper.ToRecord(trade));
        db.TradeExecutions.AddRange(trade.Executions.Select(TradeExecutionPersistenceMapper.ToRecord));
        db.TradeBrowse.Add(TradeBrowsePersistenceMapper.ToRecord(trade));
        for (int i = 0; i < setups; i++) db.TradingSetups.Add(new() { Id = i == 0 ? id : Guid.NewGuid(), Name = $"Study {i:000}", IsActive = i % 2 == 0,
            CreatedAtUtc = f.Time.Now, UpdatedAtUtc = f.Time.Now });
        await db.SaveChangesAsync(); return id;
    }
    [Fact]
    public async Task PickerSearchPagesAreBoundedCompleteQueryBoundAndCancellable()
    {
        await using var f = await Create(); await Seed(f, 61); var service = f.Service();
        var page = await service.ListLinkTargetsAsync(new(NotebookTargetKind.TradingSetup, "study", 7));
        Assert.Equal(61, page.TotalCount); Assert.Equal(7, page.Items.Length); Assert.NotNull(page.NextCursor);
        var ids = page.Items.Select(x => x.Id).ToList(); string firstCursor = page.NextCursor!;
        while (page.NextCursor is { } cursor)
        { page = await f.Service().ListLinkTargetsAsync(new(NotebookTargetKind.TradingSetup, "study", 7, cursor)); Assert.InRange(page.Items.Length, 1, 7); ids.AddRange(page.Items.Select(x => x.Id)); }
        Assert.Equal(61, ids.Count); Assert.Equal(61, ids.Distinct().Count());
        await Assert.ThrowsAsync<ArgumentException>(() => service.ListLinkTargetsAsync(new(NotebookTargetKind.Trade, "study", cursor: firstCursor)));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ListLinkTargetsAsync(new(NotebookTargetKind.TradingSetup, "different", cursor: firstCursor)));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ListLinkTargetsAsync(new(NotebookTargetKind.Trade), cancelled.Token));
        Assert.Empty((await service.ListLinkTargetsAsync(new(NotebookTargetKind.Trade, "unmatched"))).Items);
        Assert.Contains("Research account", Assert.Single((await service.ListLinkTargetsAsync(new(NotebookTargetKind.Trade, "research"))).Items).Label);
    }
    [Fact]
    public async Task TypedDuplicatesConflictsAndTargetDeletionDoNotChangeTargetsOrRetargetLinks()
    {
        await using var f = await Create(); var id = await Seed(f); var note = await f.CreateNote();
        await using (var seed = f.Context())
        {
            seed.TradeScreenshots.Add(new() { Id = Guid.NewGuid(), TradeId = id, Type = PersonalTradingJournal.Domain.Screenshots.TradeScreenshotType.Entry,
                StorageKey = "trade-owned.png", FileName = "original.png", CreatedAtUtc = f.Time.Now, UpdatedAtUtc = f.Time.Now });
            await seed.SaveChangesAsync();
        }
        await using var beforeDb = f.Context();
        string trade = JsonSerializer.Serialize(await beforeDb.Trades.AsNoTracking().SingleAsync());
        string executions = JsonSerializer.Serialize(await beforeDb.TradeExecutions.AsNoTracking().ToArrayAsync());
        string browse = JsonSerializer.Serialize(await beforeDb.TradeBrowse.AsNoTracking().ToArrayAsync());
        string screenshots = JsonSerializer.Serialize(await beforeDb.TradeScreenshots.AsNoTracking().ToArrayAsync());
        string setup = JsonSerializer.Serialize(await beforeDb.TradingSetups.AsNoTracking().SingleAsync());
        var t = new NotebookLink(NotebookTargetKind.Trade, id, "NQ"); var s = new NotebookLink(NotebookTargetKind.TradingSetup, id, "Study 000");
        var initial = note;
        note = Success(await f.Service().ExecuteAsync(new AddNotebookLink(note.Id, note.Version, t)));
        Assert.Equal(NotebookWriteStatus.Conflict, (await f.Service().ExecuteAsync(new AddNotebookLink(note.Id, initial.Version, s))).Status);
        note = Success(await f.Service().ExecuteAsync(new AddNotebookLink(note.Id, note.Version, s)));
        Assert.Equal(2, note.Links.Length);
        Assert.Equal(NotebookWriteStatus.Invalid, (await f.Service().ExecuteAsync(new AddNotebookLink(note.Id, note.Version, t))).Status);
        note = Success(await f.Service().ExecuteAsync(new RemoveNotebookLink(note.Id, note.Version, t.Kind, id)));
        Assert.Equal(s, Assert.Single(note.Links));
        await using (var check = f.Context())
        {
            Assert.Equal(trade, JsonSerializer.Serialize(await check.Trades.AsNoTracking().SingleAsync()));
            Assert.Equal(setup, JsonSerializer.Serialize(await check.TradingSetups.AsNoTracking().SingleAsync()));
            Assert.Equal(executions, JsonSerializer.Serialize(await check.TradeExecutions.AsNoTracking().ToArrayAsync()));
            Assert.Equal(browse, JsonSerializer.Serialize(await check.TradeBrowse.AsNoTracking().ToArrayAsync()));
            Assert.Equal(screenshots, JsonSerializer.Serialize(await check.TradeScreenshots.AsNoTracking().ToArrayAsync()));
            await check.TradingSetups.Where(x => x.Id == id).ExecuteDeleteAsync();
        }
        Assert.Equal(NotebookWriteStatus.UnavailableTarget, (await f.Service().ExecuteAsync(new AddNotebookLink(initial.Id, note.Version, new(NotebookTargetKind.TradingSetup, id, "Selected before deletion")))).Status);
        var fresh = (await f.Service().LoadAsync(note.Id)).Note!; Assert.Equal(note.Version, fresh.Version); Assert.Equal(s, Assert.Single(fresh.Links));
        Assert.Equal(NotebookTargetAvailability.Unavailable, Assert.Single(await f.Service().ResolveLinksAsync(fresh.Links)).Availability);
        note = Success(await f.Service().ExecuteAsync(new RemoveNotebookLink(note.Id, note.Version, s.Kind, id))); Assert.Empty(note.Links);
    }
    [Fact]
    public async Task CurrentNamesAndInactiveStateResolveWithoutWritingTheNote()
    {
        await using var f = await Create(); var id = await Seed(f); var note = await f.CreateNote();
        var link = new NotebookLink(NotebookTargetKind.TradingSetup, id, "Study 000");
        note = Success(await f.Service().ExecuteAsync(new AddNotebookLink(note.Id, note.Version, link)));
        await using (var db = f.Context()) await db.TradingSetups.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Name, "Renamed study").SetProperty(x => x.IsActive, false));
        var resolved = Assert.Single(await f.Service().ResolveLinksAsync([link])); Assert.Equal("Renamed study", resolved.DisplayLabel); Assert.True(resolved.IsInactive);
        var choice = Assert.Single((await f.Service().ListLinkTargetsAsync(new(NotebookTargetKind.TradingSetup, "renamed"))).Items);
        Assert.Contains("Inactive", choice.Context); Assert.Equal(id, choice.Id);
        await using (var db = f.Context()) await db.TradingSetups.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Name, "Наблюдение Café"));
        Assert.Equal(id, Assert.Single((await f.Service().ListLinkTargetsAsync(new(NotebookTargetKind.TradingSetup, "наблюдение café"))).Items).Id);
        var fresh = (await f.Service().LoadAsync(note.Id)).Note!; Assert.Equal(note.Version, fresh.Version); Assert.Equal(link, Assert.Single(fresh.Links));
    }
    [Theory]
    [InlineData("future")] [InlineData("invalid")] [InlineData("archived")] [InlineData("trash")]
    public async Task ProtectedAndReadOnlyNotesCannotBeRewrittenByLinkOperations(string state)
    {
        await using var f = await Create(); var id = await Seed(f); var note = await f.CreateNote();
        if (state == "archived") note = Success(await f.Service().ExecuteAsync(new ArchiveNotebookNote(note.Id, note.Version)));
        if (state == "trash") note = Success(await f.Service().ExecuteAsync(new TrashNotebookNote(note.Id, note.Version)));
        await using (var db = f.Context()) if (state is "future" or "invalid")
            await db.NotebookNotes.Where(x => x.Id == note.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.DocumentJson, state == "future" ? "{\"format\":\"ptj.notebook.document\",\"version\":99,\"blocks\":[]}" : " invalid original "));
        await using var check = f.Context(); string original = (await check.NotebookNotes.AsNoTracking().SingleAsync()).DocumentJson;
        var result = await f.Service().ExecuteAsync(new AddNotebookLink(note.Id, note.Version, new(NotebookTargetKind.Trade, id, "NQ")));
        Assert.Equal(state is "future" or "invalid" ? NotebookWriteStatus.UnsupportedContent : NotebookWriteStatus.Invalid, result.Status);
        Assert.Equal(original, (await check.NotebookNotes.AsNoTracking().SingleAsync()).DocumentJson); Assert.Empty(await check.NotebookLinks.ToArrayAsync());
    }
    [Fact]
    public async Task MetadataMutationsPreserveExactImageJsonOwnershipRecoveryAndOriginalBytes()
    {
        await using var f = await Create(); var target = await Seed(f); var paths = new LocalApplicationPaths(f.Root);
        var files = new NotebookImageFiles(paths, f.Factory(), new NotebookImageTests.Probe());
        var service = new NotebookService(f.Factory(), f.Codec, f.Time, files); var images = new NotebookImageService(f.Factory(), service, f.Codec, files);
        var note = await f.CreateNote(); var imageId = Guid.NewGuid(); using var input = new MemoryStream(NotebookImageTests.Png);
        note = Success(await images.ImportAsync(new(note.Id, note.Version, imageId, NotebookImageTests.Append(note.Document, imageId)), input));
        string json = f.Codec.Encode(note.Document).Json; var originals = note.Images;
        var recovery = new NotebookRecoveryStore(f.Factory(), f.Codec);
        var draft = new NotebookRecoveryDraft(Guid.NewGuid(), note.Id, note.Version, 1, "Retained draft", note.Document, f.Time.Now);
        Assert.Equal(NotebookWriteStatus.Success, await recovery.PutAsync(draft, 0));
        var link = new NotebookLink(NotebookTargetKind.Trade, target, "NQ");
        note = Success(await service.ExecuteAsync(new AddNotebookLink(note.Id, note.Version, link)));
        note = Success(await service.ExecuteAsync(new RemoveNotebookLink(note.Id, note.Version, link.Kind, target)));
        var fresh = (await f.Service().LoadAsync(note.Id)).Note!; Assert.Equal(json, f.Codec.Encode(fresh.Document).Json); Assert.Equal<NotebookImage>(originals, fresh.Images);
        Assert.Equal(NotebookImageTests.Png, (await images.ReadOriginalAsync(note.Id, imageId)).Original);
        var recovered = (await new NotebookRecoveryStore(f.Factory(), f.Codec).LoadAsync(draft.Id)).Draft!;
        Assert.Equal(draft.ExpectedVersion, recovered.ExpectedVersion); Assert.Equal(json, f.Codec.Encode(recovered.Document).Json);
        await using var db = f.Context(); Assert.Single(await db.NotebookOwnedImageFiles.ToArrayAsync()); Assert.Equal(json, (await db.NotebookNotes.SingleAsync()).DocumentJson);
    }
    [Fact]
    public async Task ConcurrentLinkAndContentWritesHaveOneExpectedVersionWinner()
    {
        await using var f = await Create(); var target = await Seed(f); var note = await f.CreateNote();
        var results = await Task.WhenAll(Task.Run(() => f.Service().ExecuteAsync(new AddNotebookLink(note.Id, note.Version, new(NotebookTargetKind.Trade, target, "NQ")))),
            Task.Run(() => f.Service().ExecuteAsync(new UpdateNotebookTitle(note.Id, note.Version, "Concurrent title")))).WaitAsync(TimeSpan.FromSeconds(30));
        var winner = Assert.Single(results, x => x.Status == NotebookWriteStatus.Success); Assert.Single(results, x => x.Status == NotebookWriteStatus.Conflict);
        var fresh = (await f.Service().LoadAsync(note.Id)).Note!; Assert.Equal(note.Version + 1, fresh.Version);
        Assert.Equal(winner.Note!.Title, fresh.Title); Assert.Equal<NotebookLink>(winner.Note.Links, fresh.Links);
    }
}
