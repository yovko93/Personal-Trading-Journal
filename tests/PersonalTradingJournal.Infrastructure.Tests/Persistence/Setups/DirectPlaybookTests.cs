using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Setups;

public sealed partial class PlaybookPersistenceTests
{
    [Fact]
    public async Task DirectEditsReplaceOneCurrentDefinitionAndOnlyCapturesRetainHistoryAcrossRestart()
    {
        await using var f = await Fixture.Create(); var g = await f.Seed();
        var save = f.Get<SaveTradingSetupPlaybookUseCase>();
        var first = await save.ExecuteAsync(new(g.Setup.Id, 0, Guidance(PlaybookExample.FromScreenshot(Guid.NewGuid(), "Original note", g.Screenshot))));
        await using (var db = f.Context())
        { Assert.Equal(1, await db.CurrentPlaybooks.CountAsync()); Assert.Empty(await db.PlaybookRevisions.ToArrayAsync()); }
        Assert.True(await f.Contexts.TryAddAsync(TradePlaybookContext.Capture(g.Trade, g.Setup, first, Now.AddMinutes(1)), Now));
        var second = await save.ExecuteAsync(new(g.Setup.Id, first.Revision, new("Edited directly", "e", "c", "i", "x")));
        Assert.Equal(first.TradingSetupId, second.TradingSetupId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => save.ExecuteAsync(new(g.Setup.Id, first.Revision, Guidance())));
        await f.Restart();
        Assert.Equal("Edited directly", (await f.Store.GetLatestAsync(g.Setup.Id))!.Guidance.MarketContext);
        var frozen = (await f.Contexts.GetAsync(g.Trade.Id))!.Definition;
        Assert.Equal("Synthetic NQ context", frozen.Guidance.MarketContext);
        Assert.Equal("Original note", Assert.Single(frozen.Guidance.Examples).Annotation);
        await using var read = f.Context();
        Assert.Equal(1, await read.CurrentPlaybooks.CountAsync());
        Assert.Equal(first.Revision, Assert.Single(await read.PlaybookRevisions.ToArrayAsync()).Revision);
        Assert.Empty(await read.CurrentPlaybookExamples.ToArrayAsync());
        Assert.Single(await read.PlaybookExamples.ToArrayAsync());
    }

    [Fact]
    public async Task DirectConcurrentWritersAndExampleFailureLeaveOneVerifiedDefinition()
    {
        await using var f = await Fixture.Create(); var g = await f.Seed();
        var first = await f.Get<SaveTradingSetupPlaybookUseCase>().ExecuteAsync(new(g.Setup.Id, 0, Guidance()));
        var a = first.Revise(g.Setup, 1, new("A", "e", "c", "i", "x"), Now);
        var b = first.Revise(g.Setup, 1, new("B", "e", "c", "i", "x"), Now);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> Write(TradingSetupPlaybookRevision d) => Task.Run(async () => { await start.Task; return await f.Store.TrySaveCurrentAsync(d, 1, Now); });
        var one = Write(a); var two = Write(b); start.SetResult();
        var results = await Task.WhenAll(one, two); Assert.Single(results, x => x); Assert.Single(results, x => !x);
        var current = (await f.Store.GetLatestAsync(g.Setup.Id))!;
        await using var db = f.Context();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER SyntheticCurrentFailure BEFORE INSERT ON CurrentPlaybookExamples BEGIN SELECT RAISE(ABORT, 'synthetic'); END;");
        var next = current.Revise(g.Setup, current.Revision, Guidance(PlaybookExample.FromScreenshot(Guid.NewGuid(), "note", g.Screenshot)), Now);
        await Assert.ThrowsAsync<DbUpdateException>(() => f.Store.TrySaveCurrentAsync(next, current.Revision, Now));
        Assert.Equal(current.Guidance.MarketContext, (await f.Store.GetLatestAsync(g.Setup.Id))!.Guidance.MarketContext);
        Assert.Empty(await db.PlaybookRevisions.ToArrayAsync());
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Store.TrySaveCurrentAsync(next, current.Revision, Now, cancelled.Token));
        Assert.Equal(current.Revision, (await f.Store.GetLatestAsync(g.Setup.Id))!.Revision);
    }

    [Fact]
    public async Task MigrationSelectsExactLegacyCurrentDefinitionWithoutChangingHistoricalRows()
    {
        await using var f = await Fixture.Create();
        await using (var db = f.Context()) await db.GetService<IMigrator>().MigrateAsync("20261019080838_SharedTradingPlansAndScreenshots");
        var g = await f.Seed();
        // Previous-schema SQL seed through the unchanged legacy writer does not query new tables.
        var first = TradingSetupPlaybookRevision.Create(g.Setup, Guidance(), Now);
        Assert.True(await f.Store.TryAppendAsync(first, 0, Now));
        var second = first.Revise(g.Setup, 1, new("Authoritative current", "e", "c", "i", "x"), Now);
        Assert.True(await f.Store.TryAppendAsync(second, 1, Now));
        await using (var db = f.Context()) await db.Database.MigrateAsync();
        var migrated = (await f.Store.GetLatestAsync(g.Setup.Id))!;
        Assert.Equal(2, migrated.Revision); Assert.Equal("Authoritative current", migrated.Guidance.MarketContext);
        await f.Get<SaveTradingSetupPlaybookUseCase>().ExecuteAsync(new(g.Setup.Id, 2, Guidance()));
        await using var read = f.Context();
        Assert.Equal(2, await read.PlaybookRevisions.CountAsync());
        Assert.Null((await read.CurrentPlaybooks.SingleAsync()).LegacyRevision);
        Assert.Equal("Authoritative current", (await f.History.GetRevisionAsync(g.Setup.Id, 2))!.Guidance.MarketContext);
    }
}
