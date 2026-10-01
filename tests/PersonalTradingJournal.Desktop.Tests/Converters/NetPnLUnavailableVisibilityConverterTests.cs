using System.Globalization;
using System.Windows;
using PersonalTradingJournal.Desktop.Converters;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Application.Trades;

namespace PersonalTradingJournal.Desktop.Tests.Converters;

public sealed class NetPnLUnavailableVisibilityConverterTests
{
    [Theory]
    [InlineData(-285, null, PnLOutcome.Negative, true)]
    [InlineData(5, null, PnLOutcome.Positive, true)]
    [InlineData(0, null, PnLOutcome.Zero, true)]
    [InlineData(0, 0, PnLOutcome.Zero, false)]
    [InlineData(5, -2, PnLOutcome.Negative, false)]
    public void EffectiveNumberCaptionAndExistingRowStylingAgree(int gross, int? net, PnLOutcome sign, bool estimated)
    {
        EffectiveNetPnL effective = EffectiveNetPnL.Resolve(TradeStatus.Closed, gross, net);
        Assert.Equal(sign, new PnLOutcomeConverter().Convert(effective.Value, typeof(PnLOutcome), null!, CultureInfo.InvariantCulture));
        Assert.Equal(sign, new TradeRowOutcomeConverter().Convert(
            [net.HasValue ? (decimal)net.Value : null!, (decimal)gross], typeof(PnLOutcome), null!, CultureInfo.InvariantCulture));
        Assert.Equal(estimated ? Visibility.Visible : Visibility.Collapsed, Convert(gross, net));
    }

    [Fact]
    public void ShowsExplanationForKnownGrossAndUnknownNet()
    {
        Assert.Equal(Visibility.Visible, Convert(5m, null));
        Assert.Equal(Visibility.Visible, Convert(0m, null));
    }

    [Fact]
    public void HidesExplanationWhenNetIsKnownIncludingGenuineZero()
    {
        Assert.Equal(Visibility.Collapsed, Convert(5m, 3m));
        Assert.Equal(Visibility.Collapsed, Convert(5m, 0m));
    }

    [Fact]
    public void HidesUnknownCostsExplanationForOpenTradeWithoutGross()
    {
        Assert.Equal(Visibility.Collapsed, Convert(null, null));
        Assert.Equal(Visibility.Collapsed, Convert(5m, null, TradeStatus.Open));
    }

    [Fact]
    public void MissingBindingValuesCannotAssertAnEstimate()
    {
        var converter = new NetPnLUnavailableVisibilityConverter();
        Assert.Equal(Visibility.Collapsed, converter.Convert([5m, DependencyProperty.UnsetValue, TradeStatus.Closed],
            typeof(Visibility), null!, CultureInfo.InvariantCulture));
    }

    private static Visibility Convert(decimal? gross, decimal? net, TradeStatus status = TradeStatus.Closed)
    {
        var converter = new NetPnLUnavailableVisibilityConverter();
        return (Visibility)converter.Convert(
            [gross.HasValue ? gross.Value : null!, net.HasValue ? net.Value : null!, status],
            typeof(Visibility),
            parameter: null!,
            CultureInfo.InvariantCulture);
    }
}
