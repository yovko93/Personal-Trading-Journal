using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Setups;

public sealed partial class PlaybookPersistenceTests
{
    [Fact]
    public async Task WorkspaceMigrationRetiresOnlyTradeCapturesAndPreservesSharedImagesAndClassification()
    {
        await using var f = await Fixture.Create();
        var graph = await f.Seed();
        var definition = await SaveImage(f, graph);
        var image = definition.Guidance.Examples.Single().Image!;
        Assert.True(await f.Contexts.TryAddAsync(TradePlaybookContext.Capture(graph.Trade, graph.Setup, definition, Now.AddMinutes(1)), Now));
        var associations = f.Get<ITradePlaybookAssociationStore>();
        Assert.True(await associations.TryApplyAsync(await associations.PreviewAsync(graph.Trade.Id, graph.Setup.Id)));
        await using var db = f.Context();
        var screenshots = await db.TradeScreenshots.AsNoTracking().ToArrayAsync();
        var executions = System.Text.Json.JsonSerializer.Serialize(await db.TradeExecutions.AsNoTracking().ToArrayAsync());
        var migrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.EndsWith("SimplifyTradeWorkspace", migrations[^1]);
        await db.GetService<IMigrator>().MigrateAsync(migrations[^2]);
        await db.GetService<IMigrator>().MigrateAsync();
        Assert.Empty(await db.TradePlaybookAssociations.ToArrayAsync());
        Assert.Empty(await db.TradePlaybookContexts.ToArrayAsync());
        var trade = await db.Trades.SingleAsync();
        Assert.Equal(graph.Setup.Id, trade.TradingSetupId); Assert.Null(trade.Description);
        Assert.Equal(executions, System.Text.Json.JsonSerializer.Serialize(await db.TradeExecutions.AsNoTracking().ToArrayAsync()));
        Assert.Equal(screenshots.Select(s => s.StorageKey), (await db.TradeScreenshots.AsNoTracking().ToArrayAsync()).Select(s => s.StorageKey));
        await f.Get<IPlaybookImageStorage>().DeleteUnreferencedAsync(image.StorageKey);
        Assert.Equal(ImageBytes, await f.Get<IPlaybookImageStorage>().ReadAsync(image));
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => db.Database.ExecuteSqlRawAsync("INSERT INTO TradePlaybookAssociations (TradeId,Sequence,RecordedAtUtc) VALUES ('synthetic',1,0)"));
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => db.Database.ExecuteSqlRawAsync("INSERT INTO TradePlaybookContexts (TradeId) VALUES ('synthetic')"));
        var mutation = f.Get<ITradeMutationStore>();
        var loaded = (await mutation.GetByIdAsync(graph.Trade.Id))!;
        loaded.SetTradingSetup(null, Now.AddHours(1)); await mutation.SaveAsync(loaded);
        Assert.Empty(await db.TradePlaybookAssociations.ToArrayAsync());
        Assert.NotEmpty(await db.PlaybookExamples.ToArrayAsync());
    }
}
