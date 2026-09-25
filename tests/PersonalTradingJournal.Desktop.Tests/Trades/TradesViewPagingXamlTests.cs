using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PersonalTradingJournal.Desktop.Tests.Trades;

public sealed class TradesViewPagingXamlTests
{
    [Fact]
    public void ListAndDetailBindGrossAndNetSeparatelyWithUnknownCostGuidance()
    {
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XDocument view = XDocument.Parse(ReadTradesView());
        XElement list = Assert.Single(view.Descendants(presentation + "ItemsControl"), item =>
            (string?)item.Attribute("ItemsSource") == "{Binding RecentTrades}");

        Assert.Contains(list.Descendants(presentation + "TextBlock"), item =>
            (string?)item.Attribute("Text") == "Gross P&L ");
        Assert.Contains(list.Descendants(presentation + "TextBlock"), item =>
            (string?)item.Attribute("Text") == "Net P&L ");
        Assert.Contains(list.Descendants(presentation + "TextBlock"), item =>
            (string?)item.Attribute("Text") == "{Binding GrossPnL, TargetNullValue=—}");
        Assert.Contains(list.Descendants(presentation + "TextBlock"), item =>
            (string?)item.Attribute("Text") == "{Binding NetPnL}");

        XElement[] explanations = view.Descendants(presentation + "TextBlock")
            .Where(item => (string?)item.Attribute("Text") ==
                "Net unavailable: commission/fees unknown.")
            .ToArray();
        Assert.Equal(2, explanations.Length);
        Assert.All(explanations, item => Assert.Equal(
            "Net unavailable: commission and fees are unknown",
            (string?)item.Attribute("AutomationProperties.Name")));
        Assert.Contains(explanations, item => item.Ancestors().Contains(list));
        Assert.Contains(explanations, item => !item.Ancestors().Contains(list));
        Assert.Contains(explanations, item => item.Descendants(presentation + "Binding")
            .Any(binding => (string?)binding.Attribute("Path") == "GrossPnL"));
        Assert.Contains(explanations, item => item.Descendants(presentation + "Binding")
            .Any(binding => (string?)binding.Attribute("Path") == "SelectedTradeDetail.GrossPnL"));
        Assert.All(explanations, item => Assert.Contains(
            item.Descendants(presentation + "MultiBinding"), binding =>
                (string?)binding.Attribute("Converter") ==
                "{StaticResource NetPnLUnavailableVisibilityConverter}"));
    }

    [Fact]
    public void TradeSortHeaderStyleUsesDedicatedBorderFreeTemplate()
    {
        string view = ReadTradesView();
        string style = ExtractTradeSortHeaderStyle(view);

        Assert.Contains("<ControlTemplate TargetType=\"{x:Type Button}\">", style, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"HeaderChrome\"", style, StringComparison.Ordinal);
        Assert.Contains("Background=\"{TemplateBinding Background}\"", style, StringComparison.Ordinal);
        Assert.Contains("BorderThickness=\"0\"", style, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(style, "<Border\\s").Cast<Match>());
        Assert.DoesNotContain("FocusIndicator", style, StringComparison.Ordinal);
        Assert.DoesNotContain("Focusable=\"False\"", style, StringComparison.Ordinal);
        Assert.Contains("Property=\"IsKeyboardFocused\"", style, StringComparison.Ordinal);
    }

    [Fact]
    public void EverySortHeaderUsesCurrentColumnDrivenActiveBackground()
    {
        string view = ReadTradesView();
        string[] sortColumns =
        [
            "OpenedAtUtc",
            "Instrument",
            "Account",
            "AverageEntryPrice",
            "OpenQuantity",
            "NetPnL",
        ];

        Assert.Equal(
            6,
            Regex.Matches(
                view,
                "BasedOn=\"\\{StaticResource TradeSortHeaderButtonStyle\\}\"")
                .Count);
        Assert.Equal(
            6,
            Regex.Matches(view, "Binding=\"\\{Binding CurrentSortColumn\\}\"")
                .Count);
        Assert.All(sortColumns, column => Assert.Contains(
            $"Value=\"{{x:Static applicationTrades:TradeListSortColumn.{column}}}\"",
            view,
            StringComparison.Ordinal));
        Assert.Contains(
            "<Setter Property=\"Background\" Value=\"Transparent\" />",
            ExtractTradeSortHeaderStyle(view),
            StringComparison.Ordinal);
        Assert.True(Regex.Matches(
            view,
            "Value=\"\\{DynamicResource PtjSurfaceElevatedBrush\\}\"").Count >= 6);
    }

    [Fact]
    public void RequiredHeadersAreClickableAndActionsRemainStatic()
    {
        string view = ReadTradesView();
        string[] accessibleNames =
        [
            "Sort by Opened New York time",
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
            "<ColumnDefinition Width=\"180\" />\\s*" +
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

    private static string ExtractTradeSortHeaderStyle(string view)
    {
        int start = view.IndexOf(
            "<Style x:Key=\"TradeSortHeaderButtonStyle\"",
            StringComparison.Ordinal);
        Assert.True(start >= 0);
        int end = view.IndexOf("</Style>", start, StringComparison.Ordinal);
        Assert.True(end > start);
        return view[start..(end + "</Style>".Length)];
    }

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
