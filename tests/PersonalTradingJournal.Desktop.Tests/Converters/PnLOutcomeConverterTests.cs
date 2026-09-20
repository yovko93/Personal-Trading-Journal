using System.Globalization;
using PersonalTradingJournal.Desktop.Converters;

namespace PersonalTradingJournal.Desktop.Tests.Converters;

public sealed class PnLOutcomeConverterTests
{
    public static TheoryData<object?, PnLOutcome> Outcomes => new()
    {
        { 125.50m, PnLOutcome.Positive },
        { -0.01m, PnLOutcome.Negative },
        { 0m, PnLOutcome.Zero },
        { null, PnLOutcome.None },
    };

    [Theory]
    [MemberData(nameof(Outcomes))]
    public void ConvertClassifiesNullableProfitAndLoss(
        object? value,
        PnLOutcome expected)
    {
        var converter = new PnLOutcomeConverter();

        object actual = converter.Convert(
            value,
            typeof(PnLOutcome),
            parameter: null,
            CultureInfo.InvariantCulture);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ConvertTreatsUnsupportedValuesAsNoOutcome()
    {
        var converter = new PnLOutcomeConverter();

        object result = converter.Convert(
            "12.50",
            typeof(PnLOutcome),
            parameter: null,
            CultureInfo.InvariantCulture);

        Assert.Equal(PnLOutcome.None, result);
    }
}
