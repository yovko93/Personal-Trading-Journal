using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.Analytics.Breakdown;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Tests.Analytics.Breakdown;

public sealed class BreakdownGroupSelectionTests
{
    [Fact]
    public void EveryDimensionCarriesTheCompleteOriginalFilterAndTypedPredicate()
    {
        Guid account = Guid.NewGuid(), instrument = Guid.NewGuid();
        BreakdownGroupKey[] keys = [BreakdownGroupKey.ForInstrument(instrument), BreakdownGroupKey.ForAccount(account),
            BreakdownGroupKey.ForSetup(null), BreakdownGroupKey.ForSetup(Guid.NewGuid()), BreakdownGroupKey.ForDirection(TradeDirection.Short),
            BreakdownGroupKey.ForEntryWeekday(DayOfWeek.Sunday), BreakdownGroupKey.ForEntryHour(1),
            BreakdownGroupKey.ForHoldingDuration(HoldingDurationBucket.OneToFiveMinutes)];
        foreach (var key in keys)
        {
            var query = new BreakdownQuery("EUR", account, instrument, new(2026, 11, 1), new(2026, 11, 1), PnlBasis.Net, key.Grouping);
            var selection = new BreakdownGroupSelection(query, key);
            Assert.Same(query, selection.Query);
            Assert.Same(key, selection.Key);
            Assert.Equal(TimeSpan.FromHours(25), selection.Query.TradeFilter.ClosedBeforeUtc - selection.Query.TradeFilter.ClosedFromUtc);
            Assert.Equal(new BreakdownGroupSelection(query, key), selection);
        }
    }

    [Fact]
    public void DimensionAndConflictingExactReferenceSelectionsAreRejected()
    {
        Guid id = Guid.NewGuid(), other = Guid.NewGuid();
        Assert.Throws<ArgumentNullException>(() => new BreakdownGroupSelection(null!, BreakdownGroupKey.ForSetup(null)));
        Assert.Throws<ArgumentNullException>(() => new BreakdownGroupSelection(new("USD"), null!));
        Assert.Equal("key", Assert.Throws<ArgumentException>(() => new BreakdownGroupSelection(
            new("USD"), BreakdownGroupKey.ForAccount(id))).ParamName);
        Assert.Equal("key", Assert.Throws<ArgumentException>(() => new BreakdownGroupSelection(
            new("USD", tradingAccountId: id, grouping: BreakdownGrouping.Account), BreakdownGroupKey.ForAccount(other))).ParamName);
        Assert.Equal("key", Assert.Throws<ArgumentException>(() => new BreakdownGroupSelection(
            new("USD", instrumentId: id), BreakdownGroupKey.ForInstrument(other))).ParamName);
    }

    [Fact]
    public void AllScopeSelectionCanTargetExactReferenceButNeverUsesItsDisplayName()
    {
        Guid id = Guid.NewGuid();
        var key = BreakdownGroupKey.ForAccount(id);
        var selection = new BreakdownGroupSelection(new("USD", grouping: BreakdownGrouping.Account), key);
        Assert.Null(selection.Query.TradeFilter.TradingAccountId);
        Assert.Equal(id, selection.Key.ReferenceId);
        Assert.StartsWith("Missing Account", selection.Key.GetLabel());
        Assert.NotEqual(selection, new BreakdownGroupSelection(selection.Query, BreakdownGroupKey.ForAccount(Guid.NewGuid())));
    }

    [Fact]
    public void UnclassifiedAndRetainedSetupSelectionsCannotBeSubstitutedForEachOther()
    {
        var query = new BreakdownQuery("USD", grouping: BreakdownGrouping.TradingSetup);
        var unclassified = new BreakdownGroupSelection(query, BreakdownGroupKey.ForSetup(null));
        var retained = new BreakdownGroupSelection(query, BreakdownGroupKey.ForSetup(Guid.NewGuid()));
        Assert.NotEqual(unclassified, retained);
        Assert.True(unclassified.Key.IsUnclassified);
        Assert.False(retained.Key.IsUnclassified);
        Assert.Contains("Missing Setup", retained.Key.GetLabel());
    }
}
