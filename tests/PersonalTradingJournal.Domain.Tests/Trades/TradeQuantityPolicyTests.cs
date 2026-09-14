using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Domain.Tests.Trades;

public sealed class TradeQuantityPolicyTests
{
    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("1000000")]
    [InlineData("2.000")]
    public void ValidateAcceptsWholeFuturesContracts(string quantityText)
    {
        decimal quantity = decimal.Parse(
            quantityText,
            System.Globalization.CultureInfo.InvariantCulture);

        TradeQuantityPolicy.Validate(AssetClass.Futures, quantity);
    }

    [Theory]
    [InlineData("0.5")]
    [InlineData("1.5")]
    public void ValidateRejectsFractionalFuturesContracts(string quantityText)
    {
        decimal quantity = decimal.Parse(
            quantityText,
            System.Globalization.CultureInfo.InvariantCulture);

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => TradeQuantityPolicy.Validate(
                AssetClass.Futures,
                quantity,
                "tradeQuantity"));

        Assert.Contains(
            TradeQuantityPolicy.FuturesWholeContractsMessage,
            exception.Message,
            StringComparison.Ordinal);
        Assert.Equal("tradeQuantity", exception.ParamName);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void ValidateRejectsNonPositiveQuantity(string quantityText)
    {
        decimal quantity = decimal.Parse(
            quantityText,
            System.Globalization.CultureInfo.InvariantCulture);

        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => TradeQuantityPolicy.Validate(AssetClass.Futures, quantity));
    }

    [Theory]
    [InlineData(AssetClass.Equity)]
    [InlineData(AssetClass.Forex)]
    [InlineData(AssetClass.Crypto)]
    [InlineData(AssetClass.Option)]
    [InlineData(AssetClass.Other)]
    public void ValidateAcceptsPositiveFractionalNonFuturesQuantity(
        AssetClass assetClass)
    {
        TradeQuantityPolicy.Validate(assetClass, 0.125m);
    }

    [Fact]
    public void ValidateRejectsUndefinedAssetClass()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => TradeQuantityPolicy.Validate((AssetClass)999, 1m));
    }
}
