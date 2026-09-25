using System.Globalization;
using PersonalTradingJournal.Desktop.Converters;

namespace PersonalTradingJournal.Desktop.Tests.Converters;

public sealed class TradeRowOutcomeConverterTests
{
    public static TheoryData<decimal?, decimal?, PnLOutcome> Outcomes => new()
    {
        { null, -582m, PnLOutcome.Negative },
        { -3300m, -3300m, PnLOutcome.Negative },
        { -1m, 5m, PnLOutcome.Negative },
        { 1m, -5m, PnLOutcome.Positive },
        { null, 5m, PnLOutcome.None },
        { null, 0m, PnLOutcome.None },
        { 0m, 0m, PnLOutcome.Zero },
        { null, null, PnLOutcome.None },
    };

    [Theory]
    [MemberData(nameof(Outcomes))]
    public void ConvertPrioritizesKnownNetAndNeverTreatsUnknownNetAsAProfit(
        decimal? net,
        decimal? gross,
        PnLOutcome expected)
    {
        var converter = new TradeRowOutcomeConverter();

        object result = converter.Convert(
            [net.HasValue ? net.Value : null!, gross.HasValue ? gross.Value : null!],
            typeof(PnLOutcome),
            parameter: null!,
            CultureInfo.InvariantCulture);

        Assert.Equal(expected, result);
    }
}
