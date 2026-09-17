using System.Text.RegularExpressions;

namespace PersonalTradingJournal.Desktop.Tests.Trades;

public sealed class TradesViewPagingXamlTests
{
    [Fact]
    public void RequiredHeadersAreClickableAndActionsRemainStatic()
    {
        string view = ReadTradesView();
        string[] accessibleNames =
        [
            "Sort by Opened UTC",
            "Sort by Trade",
            "Sort by Account",
            "Sort by Average Entry Price",
            "Sort by Open Quantity",
            "Sort by Net P&amp;L",
        ];
        string[] sortColumns =
        [
            "OpenedAtUtc",
            "Instrument",
            "Account",
            "AverageEntryPrice",
            "OpenQuantity",
            "NetPnL",
        ];

        Assert.All(accessibleNames, name => Assert.Contains(
            $"AutomationProperties.Name=\"{name}\"",
            view,
            StringComparison.Ordinal));
        Assert.All(sortColumns, column => Assert.Contains(
            $"TradeListSortColumn.{column}",
            view,
            StringComparison.Ordinal));
        Assert.Contains("Text=\"Actions\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Sort by Actions", view, StringComparison.Ordinal);
        Assert.DoesNotContain("TradeListSortColumn.Actions", view, StringComparison.Ordinal);
    }

    [Fact]
    public void SortIndicatorsAndPaginationBindingsArePresent()
    {
        string view = ReadTradesView();

        Assert.Contains("OpenedAtUtcSortIndicator", view, StringComparison.Ordinal);
        Assert.Contains("InstrumentSortIndicator", view, StringComparison.Ordinal);
        Assert.Contains("AccountSortIndicator", view, StringComparison.Ordinal);
        Assert.Contains("AverageEntryPriceSortIndicator", view, StringComparison.Ordinal);
        Assert.Contains("OpenQuantitySortIndicator", view, StringComparison.Ordinal);
        Assert.Contains("NetPnLSortIndicator", view, StringComparison.Ordinal);
        Assert.Contains("PreviousTradePageCommand", view, StringComparison.Ordinal);
        Assert.Contains("NextTradePageCommand", view, StringComparison.Ordinal);
        Assert.Contains("HasMultipleTradePages", view, StringComparison.Ordinal);
        Assert.Contains("PageSummary", view, StringComparison.Ordinal);
    }

    [Fact]
    public void HeaderAndCardsRetainMatchingColumnsAndSemanticOutcomeResources()
    {
        string view = ReadTradesView();
        const string columns =
            "<ColumnDefinition Width=\"110\" />\\s*" +
            "<ColumnDefinition Width=\"1.1\\*\" />\\s*" +
            "<ColumnDefinition Width=\"1.25\\*\" />\\s*" +
            "<ColumnDefinition Width=\"1.2\\*\" />\\s*" +
            "<ColumnDefinition Width=\"90\" />\\s*" +
            "<ColumnDefinition Width=\"110\" />\\s*" +
            "<ColumnDefinition Width=\"220\" />";

        Assert.True(Regex.Matches(view, columns).Count >= 2);
        Assert.Contains("{DynamicResource PtjSuccessSurfaceBrush}", view, StringComparison.Ordinal);
        Assert.Contains("{DynamicResource PtjDangerSurfaceBrush}", view, StringComparison.Ordinal);
        Assert.Contains("{DynamicResource PtjSuccessBrush}", view, StringComparison.Ordinal);
        Assert.Contains("{DynamicResource PtjDangerBrush}", view, StringComparison.Ordinal);
        Assert.DoesNotMatch(
            new Regex("#[0-9A-Fa-f]{6,8}", RegexOptions.CultureInvariant),
            view);
    }

    private static string ReadTradesView() => File.ReadAllText(Path.Combine(
        FindRepositoryRoot(),
        "src",
        "PersonalTradingJournal.Desktop",
        "Views",
        "Trades",
        "TradesView.xaml"));

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PersonalTradingJournal.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
