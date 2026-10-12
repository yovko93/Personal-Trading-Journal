using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Setups;

public sealed partial class PlaybookPersistenceTests
{
    [Fact]
    public async Task AssociationRelinkRemovalAndRestartRetainExactFrozenDefinitionsAndExamples()
    {
        await using var f = await Fixture.Create(); var a = await f.Seed(); var b = await f.Seed("Second plan");
        var first = await f.Save(a, true); await f.Save(b);
        var store = f.Get<ITradePlaybookAssociationStore>();
        Assert.True(await store.TryApplyAsync(await store.PreviewAsync(a.Trade.Id, a.Setup.Id)));
        Assert.True(await f.Store.TryAppendAsync(first.Revise(a.Setup, 1, Guidance(), Now.AddHours(1)), 1, Now));
        Assert.Equal(1, (await store.ReadHistoryAsync(a.Trade.Id))[0].Definition!.Revision);
        Assert.True(await store.TryApplyAsync(await store.PreviewAsync(a.Trade.Id, a.Setup.Id)));
        Assert.True(await store.TryApplyAsync(await store.PreviewAsync(a.Trade.Id, b.Setup.Id)));
        Assert.True(await store.TryApplyAsync(await store.PreviewAsync(a.Trade.Id, null)));
        a.Setup.Deactivate(Now.AddHours(2)); await f.Get<ITradingSetupStore>().UpdateAsync(a.Setup);
        await f.Restart(); store = f.Get<ITradePlaybookAssociationStore>();
        var history = await store.ReadHistoryAsync(a.Trade.Id);
        Assert.Equal(new[] {4,3,2,1}, history.Select(h => h.Sequence)); Assert.Null(history[0].Definition);
        Assert.Equal(b.Setup.Id, history[1].SetupId); Assert.Equal(2, history[2].Definition!.Revision);
        Assert.Equal(a.Screenshot.Id, Assert.Single(history[3].Definition!.Guidance.Examples).SourceScreenshotId);
        Assert.Equal(new[] {2,1}, (await store.ReadHistoryAsync(a.Trade.Id, 3, 2)).Select(h => h.Sequence));
        await using var db = f.Context(); Assert.Null((await db.Trades.SingleAsync(t => t.Id == a.Trade.Id)).TradingSetupId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.PreviewAsync(a.Trade.Id, a.Setup.Id));
        Assert.True(File.Exists(Path.Combine(f.Paths.ScreenshotsDirectory, a.Screenshot.StorageKey)));
    }

    [Theory]
    [InlineData("revision")]
    [InlineData("rename")]
    [InlineData("archive")]
    [InlineData("execution")]
    [InlineData("trade")]
    [InlineData("screenshot")]
    public async Task ChangesAfterPreviewConflictWithoutUpdatingClassification(string change)
    {
        await using var f = await Fixture.Create(); var g = await f.Seed(); var first = await f.Save(g);
        var store = f.Get<ITradePlaybookAssociationStore>(); var preview = await store.PreviewAsync(g.Trade.Id, g.Setup.Id);
        await using var db = f.Context();
        switch (change)
        {
            case "revision": Assert.True(await f.Store.TryAppendAsync(first.Revise(g.Setup, 1, Guidance(), Now.AddHours(1)), 1, Now)); break;
            case "rename": g.Setup.UpdateDetails("Renamed", null, Now); await f.Get<ITradingSetupStore>().UpdateAsync(g.Setup); break;
            case "archive": g.Setup.Deactivate(Now); await f.Get<ITradingSetupStore>().UpdateAsync(g.Setup); break;
            case "execution": await db.TradeExecutions.Where(e => e.TradeId == g.Trade.Id).ExecuteUpdateAsync(s => s.SetProperty(e => e.Price, 101)); break;
            case "trade": await db.Trades.Where(t => t.Id == g.Trade.Id).ExecuteUpdateAsync(s => s.SetProperty(t => t.TradingSetupId, (Guid?)null)); break;
            case "screenshot": await db.TradeScreenshots.Where(s => s.TradeId == g.Trade.Id).ExecuteUpdateAsync(s => s.SetProperty(s => s.Description, "Updated synthetic annotation")); break;
        }
        Assert.False(await store.TryApplyAsync(preview)); Assert.Empty(await store.ReadHistoryAsync(g.Trade.Id));
    }

    [Fact]
    public async Task ConcurrentConfirmationIsSingleAppendAndFailureRollsBackClassification()
    {
        await using var f = await Fixture.Create(); var g = await f.Seed(); await f.Save(g);
        var store = f.Get<ITradePlaybookAssociationStore>(); var preview = await store.PreviewAsync(g.Trade.Id, g.Setup.Id);
        var results = await Task.WhenAll(Task.Run(() => store.TryApplyAsync(preview)), Task.Run(() => store.TryApplyAsync(preview)));
        Assert.Single(results, r => r); Assert.Single(results, r => !r);
        await using var db = f.Context();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER SyntheticAssociationFailure BEFORE INSERT ON TradePlaybookAssociations BEGIN SELECT RAISE(ABORT, 'synthetic'); END;");
        var removal = await store.PreviewAsync(g.Trade.Id, null);
        await Assert.ThrowsAsync<DbUpdateException>(() => store.TryApplyAsync(removal));
        Assert.Equal(g.Setup.Id, (await db.Trades.AsNoTracking().SingleAsync(t => t.Id == g.Trade.Id)).TradingSetupId);
        Assert.Single(await store.ReadHistoryAsync(g.Trade.Id));
    }

    [Fact]
    public async Task CancelledAssociationNeverWritesAndLegacyClassificationIsNotBackfilled()
    {
        await using var f = await Fixture.Create(); var g = await f.Seed();
        var store = f.Get<ITradePlaybookAssociationStore>(); var preview = await store.PreviewAsync(g.Trade.Id, g.Setup.Id);
        Assert.Null(preview.Definition); Assert.Empty(await store.ReadHistoryAsync(g.Trade.Id));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.TryApplyAsync(preview, new(true)));
        Assert.Empty(await store.ReadHistoryAsync(g.Trade.Id));
        Assert.True(await store.TryApplyAsync(preview)); Assert.Null(Assert.Single(await store.ReadHistoryAsync(g.Trade.Id)).Definition);
    }

    [Fact]
    public async Task ImportedTradeAssociationPreservesExecutionsAndProvenanceAndRetainsImageAfterUnlink()
    {
        await using var f = await Fixture.Create(); var g = await f.Seed(); var definition = await SaveImage(f,g);
        await using var db = f.Context();
        var execution = await db.TradeExecutions.SingleAsync(e => e.TradeId == g.Trade.Id);
        db.TradovateImportedExecutions.Add(new() { TradeId = g.Trade.Id, TradeExecutionId = execution.Id,
            TradingAccountIdAtImport = g.Trade.TradingAccountId, BrokerSymbol = "NQZ6", Side = execution.Side,
            ExternalExecutionId = "synthetic-only", ImportedAtUtc = Now }); await db.SaveChangesAsync();
        string before = System.Text.Json.JsonSerializer.Serialize(execution);
        var store = f.Get<ITradePlaybookAssociationStore>();
        Assert.True(await store.TryApplyAsync(await store.PreviewAsync(g.Trade.Id,g.Setup.Id)));
        Assert.True(await store.TryApplyAsync(await store.PreviewAsync(g.Trade.Id,null)));
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(await db.TradeExecutions.AsNoTracking().SingleAsync(e => e.Id == execution.Id)));
        Assert.Equal("synthetic-only", (await db.TradovateImportedExecutions.SingleAsync()).ExternalExecutionId);
        var image = definition.Guidance.Examples.Single().Image!;
        await f.Get<IPlaybookImageStorage>().DeleteUnreferencedAsync(image.StorageKey);
        Assert.Equal(ImageBytes, await f.Get<IPlaybookImageStorage>().ReadAsync(image));
        Assert.Equal(g.Trade.OpenedAtUtc, (await store.ReadHistoryAsync(g.Trade.Id))[1].TradeOpenedAtUtc);
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM TradePlaybookAssociations"));
        // A legitimate parent deletion still cascades its owned transition rows, not the retained Setup images.
        await f.Get<ITradeDeletionStore>().DeleteAsync(g.Trade.Id);
        Assert.Empty(await store.ReadHistoryAsync(g.Trade.Id));
        Assert.Equal(ImageBytes, await f.Get<IPlaybookImageStorage>().ReadAsync(image));
    }

    [Fact]
    public async Task ClassificationOnlyEditRecordsRemovalWithoutRevivingEarlierCapture()
    {
        await using var f = await Fixture.Create(); var g = await f.Seed(); await f.Save(g);
        var store = f.Get<ITradePlaybookAssociationStore>();
        Assert.True(await store.TryApplyAsync(await store.PreviewAsync(g.Trade.Id,g.Setup.Id)));
        var mutations = f.Get<ITradeMutationStore>(); var trade = (await mutations.GetByIdAsync(g.Trade.Id))!;
        trade.SetTradingSetup(null, trade.UpdatedAtUtc.AddSeconds(1)); await mutations.SaveAsync(trade);
        trade.SetTradingSetup(g.Setup.Id, trade.UpdatedAtUtc.AddSeconds(1)); await mutations.SaveAsync(trade);
        var history = await store.ReadHistoryAsync(trade.Id);
        Assert.Equal(new[] {3,2,1},history.Select(a=>a.Sequence)); Assert.Null(history[0].Definition); Assert.NotNull(history[2].Definition);
    }
}
