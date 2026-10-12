using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Rules;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Rules;

public sealed partial class TradingRulePersistenceTests
{
    [Fact]
    public async Task ManualDiscoveryFiltersApplicabilityAndExactScopesWhileAggregateRetainsAllJournalIdentities()
    {
        await using var f = await Fixture.Create(); var first = await f.Seed(); var second = await f.Seed("Other", "EUR");
        var day = new DateOnly(2026, 11, 1);
        var global = await f.Rule(Definition(applicability: TradingRuleApplicability.NewYorkDay));
        var exact = await f.Rule(Definition(first.TradingAccountId, TradingRuleApplicability.NewYorkDay));
        await f.Rule(Definition(second.TradingAccountId, TradingRuleApplicability.NewYorkDay));
        await f.Rule(Definition());
        var repository = f.Get<IDailyJournalRepository>();
        foreach (Guid? account in new Guid?[] { null, first.TradingAccountId, second.TradingAccountId })
            await repository.CreateAsync(new(day, account, "Synthetic source"));
        var discovery = f.Get<IManualRuleAssessmentReader>();
        var aggregate = TradingRuleAssessmentTarget.ForNewYorkDay(day, TradingRuleAccountScope.AllAccounts);
        var target = TradingRuleAssessmentTarget.ForNewYorkDay(day, TradingRuleAccountScope.ForAccount(first.TradingAccountId));
        Assert.Contains("aggregate", (await discovery.ReadTargetAsync(aggregate)).Label);
        Assert.Equal(global.RuleId, Assert.Single((await discovery.ReadRulesAsync(aggregate)).Items).Revision.RuleId);
        Assert.Equal(2, (await discovery.ReadRulesAsync(target)).TotalCount);
        Assert.Equal(3, (await discovery.ReadEvidenceAsync(aggregate, TradingRuleEvidenceKind.Journal)).Count);
        Assert.Single(await discovery.ReadEvidenceAsync(target, TradingRuleEvidenceKind.Journal));
        var trades = await discovery.ReadEvidenceAsync(aggregate, TradingRuleEvidenceKind.Trade);
        Assert.Equal(2, trades.Count); Assert.Contains(trades, t => t.Reference.SourceId == second.Id && t.Label.Contains("EUR"));
        Assert.Contains(trades, t => t.Reference.SourceId == first.Id && t.Description.Contains("Unavailable"));
        Assert.Equal(first.Id, Assert.Single(await discovery.ReadEvidenceAsync(target, TradingRuleEvidenceKind.Trade)).Reference.SourceId);
        Assert.Empty(await discovery.ReadEvidenceAsync(TradingRuleAssessmentTarget.ForNewYorkDay(day.AddDays(1), TradingRuleAccountScope.AllAccounts), TradingRuleEvidenceKind.Trade));
        var saved = Assess(exact, target); Assert.True(await f.Assessments.TryAppendAsync(saved, 1, null));
        Assert.True(await f.Rules.TryAppendAsync(exact.Archive(1, Now), 1));
        Assert.Contains((await discovery.ReadRulesAsync(target)).Items, r => r.Revision.RuleId == exact.RuleId && !r.Revision.IsActive);
        await using var db = f.Context(); Assert.Single(await db.TradingRuleAssessments.ToArrayAsync());
    }

    [Theory]
    [InlineData(3, 8, 6, 59, 7, 1)]
    [InlineData(11, 1, 5, 59, 6, 1)]
    [InlineData(11, 1, 3, 59, 4, 1)]
    public async Task ManualSourcePickerMatchesWriterAtNewYorkDstAndMidnight(int month, int date, int eh, int em, int ch, int cm)
    {
        await using var f = await Fixture.Create();
        var trade = await f.Seed(entry: new(2026, month, date, eh, em, 0, TimeSpan.Zero), close: new(2026, month, date, ch, cm, 0, TimeSpan.Zero));
        var day = new DateOnly(2026, month, date); var target = TradingRuleAssessmentTarget.ForNewYorkDay(day, TradingRuleAccountScope.AllAccounts);
        var reader = f.Get<IManualRuleAssessmentReader>();
        var option = Assert.Single(await reader.ReadEvidenceAsync(target, TradingRuleEvidenceKind.Trade));
        Assert.Equal(trade.Id, option.Reference.SourceId);
        Assert.Empty(await reader.ReadEvidenceAsync(TradingRuleAssessmentTarget.ForNewYorkDay(day.AddDays(-1), TradingRuleAccountScope.AllAccounts), TradingRuleEvidenceKind.Trade));
        var rule = await f.Rule(Definition(applicability: TradingRuleApplicability.NewYorkDay));
        Assert.True(await f.Assessments.TryAppendAsync(Assess(rule, target, null, option.Reference), 1, null));
    }

    [Fact]
    public async Task ManualHistoryBoundedDiscoveryRetainsDeletedTargetsAfterRestartAndCancellationWritesNothing()
    {
        await using var f = await Fixture.Create(); var trade = await f.Seed(); var rule = await f.Rule(Definition());
        var target = TradingRuleAssessmentTarget.ForTrade(trade.Id, trade.TradingAccountId); Guid? previous = null;
        for (int i = 0; i < 20; i++) { var a = Assess(rule, target, previous); Assert.True(await f.Assessments.TryAppendAsync(a, 1, previous)); previous = a.Id; }
        await f.Get<ITradeDeletionStore>().DeleteAsync(trade.Id); await f.Restart();
        var reader = f.Get<IManualRuleAssessmentReader>();
        Assert.False((await reader.ReadTargetAsync(target)).Available);
        var first = await reader.ReadRuleHistoryAsync(rule.RuleId); var second = await reader.ReadRuleHistoryAsync(rule.RuleId, 2);
        Assert.Equal(11, first.Count); Assert.Equal(10, second.Count);
        Assert.Equal(20, first.Take(10).Concat(second).Select(x => x.Assessment.Id).Distinct().Count());
        Assert.All(first.Concat(second), x => Assert.Equal(TradingRuleSourceState.Unavailable, x.TargetState));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadRulesAsync(target, token: new(true)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadEvidenceAsync(target, TradingRuleEvidenceKind.Trade, token: new(true)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reader.ReadRuleHistoryAsync(rule.RuleId, pageSize: 101));
        await using var db = f.Context(); Assert.Equal(20, await db.TradingRuleAssessments.CountAsync());
    }
}
