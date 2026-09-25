using System.Globalization;
using System.Windows;
using PersonalTradingJournal.Desktop.Converters;

namespace PersonalTradingJournal.Desktop.Tests.Converters;

public sealed class NetPnLUnavailableVisibilityConverterTests
{
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
    }

    private static Visibility Convert(decimal? gross, decimal? net)
    {
        var converter = new NetPnLUnavailableVisibilityConverter();
        return (Visibility)converter.Convert(
            [gross.HasValue ? gross.Value : null!, net.HasValue ? net.Value : null!],
            typeof(Visibility),
            parameter: null!,
            CultureInfo.InvariantCulture);
    }
}
