using System.Globalization;
using PersonalTradingJournal.Desktop.Converters;

namespace PersonalTradingJournal.Desktop.Tests.Trades;

public sealed class TradingTimePresentationTests
{
    [Theory]
    [InlineData(2026, 1, 15, 14, 30, -5)]
    [InlineData(2026, 9, 10, 13, 30, -4)]
    public void ConverterProjectsCanonicalUtcToNewYork(
        int year,
        int month,
        int day,
        int utcHour,
        int utcMinute,
        int expectedOffsetHours)
    {
        var converter = new UtcToTradingTimeConverter();
        var utc = new DateTimeOffset(
            year, month, day, utcHour, utcMinute, 0, TimeSpan.Zero);

        DateTimeOffset result = Assert.IsType<DateTimeOffset>(converter.Convert(
            utc,
            typeof(DateTimeOffset),
            parameter: null,
            CultureInfo.InvariantCulture));

        Assert.Equal(new DateTime(year, month, day, 9, 30, 0), result.DateTime);
        Assert.Equal(TimeSpan.FromHours(expectedOffsetHours), result.Offset);
    }

    [Fact]
    public void TradesXamlLabelsAndBindingsUseNewYorkPresentation()
    {
        string view = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "PersonalTradingJournal.Desktop",
            "Views",
            "Trades",
            "TradesView.xaml"));

        Assert.Contains("Entry time (New York)", view, StringComparison.Ordinal);
        Assert.Contains("Exit time (New York)", view, StringComparison.Ordinal);
        Assert.Contains("Opened (New York)", view, StringComparison.Ordinal);
        Assert.Contains("Closed (New York)", view, StringComparison.Ordinal);
        Assert.Contains("Executed (New York)", view, StringComparison.Ordinal);
        Assert.Contains("UtcToTradingTimeConverter", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Entry time (UTC)", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Exit time (UTC)", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Opened (UTC)", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Executed (UTC)", view, StringComparison.Ordinal);
        Assert.Contains(
            "TradeListSortColumn.OpenedAtUtc",
            view,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "PersonalTradingJournal.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException(
            "Could not find the repository root.");
    }
}
