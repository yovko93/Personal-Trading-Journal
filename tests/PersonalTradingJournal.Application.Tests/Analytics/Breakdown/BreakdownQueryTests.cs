using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.Analytics.Breakdown;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Tests.Analytics.Breakdown;

public sealed class BreakdownQueryTests
{
    [Theory]
    [InlineData(" usd ", "USD")]
    [InlineData("eur", "EUR")]
    [InlineData("12345678", "12345678")]
    public void NormalizesCurrencyLikeHistoricalPricingAndKeepsDefaultScopeUnrestricted(string input, string expected)
    {
        var query = new BreakdownQuery(input);
        Assert.Equal(new TradePricingSnapshot(1m, input).Currency, query.Currency);
        Assert.Equal(expected, query.Currency);
        Assert.Equal(BreakdownGrouping.Instrument, query.Grouping);
        Assert.Equal(PnlBasis.Net, query.Basis);
        Assert.Equal(new DashboardAnalyticsQuery(), query.TradeFilter);
        Assert.Equal(query, new BreakdownQuery(expected));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("123456789")]
    public void RequiresOneRepresentableCurrency(string? currency)
    {
        Assert.Equal("currency", Assert.ThrowsAny<ArgumentException>(() => new BreakdownQuery(currency!)).ParamName);
    }

    [Fact]
    public void ReusesAccountInstrumentAndRangeValidationWithoutWideningFilters()
    {
        Assert.Equal("tradingAccountId", Assert.Throws<ArgumentException>(() => new BreakdownQuery("USD", tradingAccountId: Guid.Empty)).ParamName);
        Assert.Equal("instrumentId", Assert.Throws<ArgumentException>(() => new BreakdownQuery("USD", instrumentId: Guid.Empty)).ParamName);
        Assert.Equal("closedThroughNewYork", Assert.Throws<ArgumentException>(() => new BreakdownQuery("USD",
            closedFromNewYork: new(2026, 10, 3), closedThroughNewYork: new(2026, 10, 2))).ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => new BreakdownQuery("USD", closedThroughNewYork: DateOnly.MaxValue));

        Guid account = Guid.NewGuid(), instrument = Guid.NewGuid();
        var query = new BreakdownQuery("USD", account, instrument, new(2026, 10, 1), new(2026, 10, 31), PnlBasis.Gross);
        Assert.Equal(new DashboardAnalyticsQuery(account, instrument, new(2026, 10, 1), new(2026, 10, 31)), query.TradeFilter);
        Assert.NotEqual(query, new BreakdownQuery("EUR", account, instrument, new(2026, 10, 1), new(2026, 10, 31), PnlBasis.Gross));
        Assert.NotEqual(query, new BreakdownQuery("USD", account, instrument, new(2026, 10, 1), new(2026, 10, 31), PnlBasis.Net));
    }

    [Fact]
    public void OneSidedDatesRetainTheEstablishedOpenBoundary()
    {
        var from = new BreakdownQuery("USD", closedFromNewYork: new(2026, 10, 1));
        var through = new BreakdownQuery("USD", closedThroughNewYork: new(2026, 10, 1));
        Assert.Null(from.TradeFilter.ClosedBeforeUtc);
        Assert.Null(through.TradeFilter.ClosedFromUtc);
        Assert.Equal(new DashboardAnalyticsQuery(closedFromNewYork: new(2026, 10, 1)), from.TradeFilter);
        Assert.Equal(new DashboardAnalyticsQuery(closedThroughNewYork: new(2026, 10, 1)), through.TradeFilter);
    }

    [Theory]
    [InlineData(PnlBasis.EffectiveNet)]
    [InlineData((PnlBasis)(-1))]
    [InlineData((PnlBasis)99)]
    public void RejectsEstimatedAndUnsupportedBases(PnlBasis basis)
    {
        Assert.Equal("basis", Assert.Throws<ArgumentOutOfRangeException>(() => new BreakdownQuery("USD", basis: basis)).ParamName);
    }

    [Theory]
    [InlineData((BreakdownGrouping)0)]
    [InlineData((BreakdownGrouping)8)]
    [InlineData((BreakdownGrouping)(-1))]
    public void RejectsUnsupportedDimensionsWithoutSubstitutingSetup(BreakdownGrouping grouping)
    {
        Assert.Equal("grouping", Assert.Throws<ArgumentOutOfRangeException>(() => new BreakdownQuery("USD", grouping: grouping)).ParamName);
        Assert.False(Enum.TryParse<BreakdownGrouping>("Strategy", out _));
    }
}
