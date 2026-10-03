using System.Globalization;
using PersonalTradingJournal.Desktop.Converters;
using PersonalTradingJournal.Desktop.Formatting;
using System.Xml.Linq;

namespace PersonalTradingJournal.Desktop.Tests.Trades;

public sealed class TradingTimePresentationTests
{
    [Theory]
    [InlineData(-300, "UTC-5")]
    [InlineData(-240, "UTC-4")]
    [InlineData(330, "UTC+5:30")]
    [InlineData(345, "UTC+5:45")]
    [InlineData(-210, "UTC-3:30")]
    [InlineData(30, "UTC+0:30")]
    [InlineData(-30, "UTC-0:30")]
    [InlineData(0, "UTC+0")]
    public void ExplicitOffsetRetainsMinutesPrecisionAndClockCulture(int offsetMinutes, string expectedOffset)
    {
        var timestamp = new DateTimeOffset(2026, 9, 28, 5, 10, 3, TimeSpan.FromMinutes(offsetMinutes)).AddTicks(1234567);
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.DateTimeFormat.TimeSeparator = ".";
        Assert.Equal($"2026-09-28 05.10.03.1234567 {expectedOffset}",
            TradingTimestampFormatter.Format(timestamp, TradingTimestampFormatter.DefaultClockFormat, culture));
        Assert.Equal(TimeSpan.FromMinutes(offsetMinutes), timestamp.Offset);
    }

    [Theory]
    [InlineData(9, 9, "2026-09-28 05:10:03 UTC-4")]
    [InlineData(12, 10, "2026-12-28 05:10:03 UTC-5")]
    public void ExplicitConverterUsesActualNewYorkOffsetAndOptionalClockPrecision(int month, int utcHour, string expected)
    {
        var converter = new TradingTimestampConverter();
        var utc = new DateTimeOffset(2026, month, 28, utcHour, 10, 3, TimeSpan.Zero).AddTicks(1234567);
        Assert.Equal(expected, converter.Convert(utc, typeof(string), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        Assert.Equal(expected.Replace("03 UTC", "03.1234567 UTC", StringComparison.Ordinal),
            converter.Convert(utc, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal(TimeSpan.Zero, utc.Offset);
    }

    [Fact]
    public void ExplicitConverterDistinguishesMissingTimeAndIsNotAnInputConverter()
    {
        var converter = new TradingTimestampConverter();
        Assert.Equal("—", converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Throws<ArgumentException>(() => converter.Convert("invalid", typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Throws<NotSupportedException>(() => converter.ConvertBack("2026-09-28 05:10:03 UTC-4", typeof(DateTimeOffset), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void OffsetBearingUiBindingsUseSharedFormatterNotBareNumericOffsets()
    {
        string views = Path.Combine(FindRepositoryRoot(), "src/PersonalTradingJournal.Desktop/Views");
        XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var inline = XDocument.Load(Path.Combine(views, "Calendar/CalendarInlineTradeView.xaml"));
        var execution = Assert.Single(inline.Descendants(wpf + "TextBlock"),
            e => ((string?)e.Attribute("Text"))?.Contains("Binding ExecutedAtUtc", StringComparison.Ordinal) == true);
        Assert.Equal("{Binding ExecutedAtUtc, Converter={StaticResource TradingTime}}", (string?)execution.Attribute("Text"));
        Assert.Contains("TradingTimestampConverter", inline.ToString());
        var import = XDocument.Load(Path.Combine(views, "Import/ImportView.xaml"));
        var period = import.Descendants(wpf + "TextBlock").Select(e => (string?)e.Attribute("Text"))
            .Where(text => text?.Contains("Binding PreviewSummary.Period", StringComparison.Ordinal) == true).ToArray();
        Assert.Equal(2, period.Length);
        Assert.All(period, text => Assert.Contains("Converter={StaticResource TradingTimestamp}", text));
        Assert.Contains("TradingTimestampConverter", import.ToString());
        foreach (string file in Directory.EnumerateFiles(views, "*.xaml", SearchOption.AllDirectories))
            Assert.DoesNotContain("zzz", File.ReadAllText(file), StringComparison.Ordinal);
    }

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

        string form = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src/PersonalTradingJournal.Desktop/Views/Trades/TradeEntryForm.xaml"));
        Assert.Contains("Entry time (New York)", form, StringComparison.Ordinal);
        Assert.Contains("Exit time (New York)", form, StringComparison.Ordinal);
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
