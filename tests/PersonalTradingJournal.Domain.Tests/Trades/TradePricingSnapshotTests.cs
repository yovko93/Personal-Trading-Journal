using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Domain.Tests.Trades;

public sealed class TradePricingSnapshotTests
{
    [Fact]
    public void PreservesValidPointValue()
    {
        var pricing = new TradePricingSnapshot(20m, "USD");

        Assert.Equal(20m, pricing.PointValue);
    }

    [Fact]
    public void PreservesFractionalPointValue()
    {
        var pricing = new TradePricingSnapshot(0.123456789m, "USD");

        Assert.Equal(0.123456789m, pricing.PointValue);
    }

    [Fact]
    public void RejectsNonPositivePointValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TradePricingSnapshot(0m, "USD"));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TradePricingSnapshot(-0.01m, "USD"));
    }

    [Fact]
    public void TrimsCurrency()
    {
        var pricing = new TradePricingSnapshot(20m, "  USD  ");

        Assert.Equal("USD", pricing.Currency);
    }

    [Fact]
    public void UppercasesCurrencyInvariantly()
    {
        var pricing = new TradePricingSnapshot(20m, "usdt");

        Assert.Equal("USDT", pricing.Currency);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsMissingCurrency(string? currency)
    {
        Assert.Throws<ArgumentException>(
            () => new TradePricingSnapshot(20m, currency!));
    }

    [Fact]
    public void RejectsCurrencyLongerThanMaximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TradePricingSnapshot(20m, new string('C', 9)));
    }
}
