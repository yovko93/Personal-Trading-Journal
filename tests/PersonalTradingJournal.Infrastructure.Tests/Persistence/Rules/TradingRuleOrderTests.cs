using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Domain.Rules;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Rules;

public sealed partial class TradingRulePersistenceTests
{
    [Fact]
    public async Task LegacyOrderMigrationMoveRestartAndProseEditKeepDensePositionsAndRevisions()
    {
        await using var f = await Fixture.Create("20261013090509_OptionalGlobalRuleCriterion");
        // Previous schema has no order trigger; author through the unchanged revision store.
        var z = await f.Rule(new TradingRuleDefinition("Zulu"));
        var a = await f.Rule(new TradingRuleDefinition("Alpha"));
        var b = await f.Rule(new TradingRuleDefinition("Beta"));
        await using (var db = f.Context()) await db.GetService<IMigrator>().MigrateAsync();
        var page = await f.Reader.ReadCatalogAsync();
        Assert.Equal(new[] { a.RuleId, b.RuleId, z.RuleId }, page.Items.Select(r => r.Revision.RuleId));
        Assert.Equal(RuleMoveStatus.Saved, await f.Get<ITradingRuleOrderStore>().MoveAsync(new(b.RuleId, a.RuleId, page.CatalogVersion)));
        await f.Restart();
        Assert.Equal(new[] { b.RuleId, a.RuleId, z.RuleId }, (await f.Reader.ReadCatalogAsync()).Items.Select(r => r.Revision.RuleId));
        Assert.True(await f.Rules.TryAppendAsync(b.Revise(1, new("ZZ changed text"), Now), 1));
        Assert.Equal(b.RuleId, (await f.Reader.ReadCatalogAsync()).Items[0].Revision.RuleId);
        var appended = await f.Rule(new TradingRuleDefinition("A newly created Rule"));
        Assert.Equal(appended.RuleId, (await f.Reader.ReadCatalogAsync()).Items[^1].Revision.RuleId);
        Assert.True(await f.Rules.TryAppendAsync(a.Archive(1, Now), 1));
        await using var check = f.Context();
        Assert.Equal(new long[] { 0, 1, 2 }, await check.TradingRulePositions.OrderBy(p => p.Position).Select(p => p.Position).ToArrayAsync());
        Assert.Equal(2, (await f.Reader.ReadRevisionsAsync(a.RuleId)).Count);
        Assert.Single(await f.Reader.ReadRevisionsAsync(z.RuleId));
    }

    [Fact]
    public async Task ConcurrentMovesEditsUnavailableTargetsAndCancellationNeverPartiallyReorder()
    {
        await using var f = await Fixture.Create();
        var a = await f.Rule(new TradingRuleDefinition("First"));
        var b = await f.Rule(new TradingRuleDefinition("Second"));
        var c = await f.Rule(new TradingRuleDefinition("Third"));
        var page = await f.Reader.ReadCatalogAsync(); var order = f.Get<ITradingRuleOrderStore>();
        var results = await Task.WhenAll(
            Task.Run(() => order.MoveAsync(new(b.RuleId, a.RuleId, page.CatalogVersion))),
            Task.Run(() => order.MoveAsync(new(c.RuleId, a.RuleId, page.CatalogVersion))));
        Assert.Single(results, r => r == RuleMoveStatus.Saved); Assert.Single(results, r => r == RuleMoveStatus.Conflict);
        page = await f.Reader.ReadCatalogAsync(); var ids = page.Items.Select(r => r.Revision.RuleId).ToArray();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => order.MoveAsync(new(a.RuleId, null, page.CatalogVersion), new(true)));
        Assert.Equal(RuleMoveStatus.Unavailable, await order.MoveAsync(new(a.RuleId, Guid.NewGuid(), page.CatalogVersion)));
        Assert.True(await f.Rules.TryAppendAsync(a.Revise(1, new("Changed"), Now), 1));
        Assert.Equal(RuleMoveStatus.Conflict, await order.MoveAsync(new(a.RuleId, null, page.CatalogVersion)));
        Assert.Equal(ids, (await f.Reader.ReadCatalogAsync()).Items.Select(r => r.Revision.RuleId));
        // Failure after the position UPDATE must roll the entire permutation back.
        await using var db = f.Context();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER FailOrder BEFORE UPDATE ON TradingRuleCatalogState BEGIN SELECT RAISE(ABORT, 'synthetic failure'); END;");
        page = await f.Reader.ReadCatalogAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => order.MoveAsync(new(ids[^1], ids[0], page.CatalogVersion)));
        Assert.Equal(ids, (await f.Reader.ReadCatalogAsync()).Items.Select(r => r.Revision.RuleId));
        Assert.Equal(page.CatalogVersion, (await f.Reader.ReadCatalogAsync()).CatalogVersion);
    }
}
