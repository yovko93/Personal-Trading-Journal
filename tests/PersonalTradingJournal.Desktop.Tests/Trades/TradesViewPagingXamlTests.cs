using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PersonalTradingJournal.Desktop.Tests.Trades;

public sealed class TradesViewPagingXamlTests
{
    [Fact]
    public void TradeRowsExposeViewAndKeyboardAccessibleMenuWithRowScopedEditAndDelete()
    {
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace interactions = "clr-namespace:PersonalTradingJournal.Desktop.Interactions";
        XDocument view = XDocument.Parse(ReadTradesView());
        XElement list = Assert.Single(view.Descendants(presentation + "ItemsControl"), item =>
            (string?)item.Attribute("ItemsSource") == "{Binding RecentTrades}");
        XElement actions = Assert.Single(list.Descendants(presentation + "StackPanel"), item =>
            (string?)item.Attribute("Grid.Column") == "6" &&
            item.Elements(presentation + "Button").Any());
        XElement[] buttons = actions.Elements(presentation + "Button").ToArray();
        Assert.Equal(2, buttons.Length);
        Assert.Equal("{Binding DataContext.ShowTradeDetailCommand, RelativeSource={RelativeSource AncestorType={x:Type UserControl}}}",
            (string?)buttons[0].Attribute("Command"));
        Assert.Equal("{Binding}", (string?)buttons[0].Attribute("CommandParameter"));
        Assert.Contains(buttons[0].Descendants(presentation + "TextBlock"), item =>
            (string?)item.Attribute("Text") == "View");
        Assert.Equal("More trade actions", (string?)buttons[1].Attribute("AutomationProperties.Name"));
        Assert.Equal("True", (string?)buttons[1].Attribute(interactions + "EntityActionMenu.OpensContextMenu"));
        Assert.Equal("{Binding DataContext, RelativeSource={RelativeSource AncestorType={x:Type UserControl}}}",
            (string?)buttons[1].Attribute("Tag"));

        XElement menu = Assert.Single(buttons[1].Descendants(presentation + "ContextMenu"));
        Assert.Equal("{Binding PlacementTarget, RelativeSource={RelativeSource Self}}",
            (string?)menu.Attribute("DataContext"));
        XElement[] items = menu.Elements(presentation + "MenuItem").ToArray();
        Assert.Equal(["Edit", "Delete"], items.Select(item => (string?)item.Attribute("Header")));
        Assert.Equal("{Binding Tag.ShowTradeEditCommand}", (string?)items[0].Attribute("Command"));
        Assert.Equal("{Binding Tag.DeleteTradeCommand}", (string?)items[1].Attribute("Command"));
        Assert.All(items, item => Assert.Equal("{Binding DataContext}",
            (string?)item.Attribute("CommandParameter")));
        Assert.Equal("Edit trade", (string?)items[0].Attribute("AutomationProperties.Name"));
        Assert.Equal("{StaticResource PtjDeleteMenuItemStyle}", (string?)items[1].Attribute("Style"));
    }

    [Fact]
    public void ListShowsPeakSizeWithExactCompactQuantityWhileDetailsKeepOpenQuantity()
    {
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XDocument view = XDocument.Parse(ReadTradesView());
        XElement list = Assert.Single(view.Descendants(presentation + "ItemsControl"), item =>
            (string?)item.Attribute("ItemsSource") == "{Binding RecentTrades}");
        const string format = "0.############################";
        XElement size = Assert.Single(list.Descendants(presentation + "TextBlock"), item =>
            (string?)item.Attribute("Text") == $"{{Binding Size, StringFormat={{}}{{0:{format}}}}}");
        Assert.Contains("Peak position quantity", (string?)size.Attribute("AutomationProperties.Name"));
        Assert.DoesNotContain(list.Descendants(presentation + "TextBlock"), item =>
            ((string?)item.Attribute("Text"))?.Contains("OpenQuantity", StringComparison.Ordinal) == true);
        Assert.Contains(view.Descendants(presentation + "TextBlock"), item =>
            (string?)item.Attribute("Text") == "Size");
        Assert.Contains(view.Descendants(presentation + "TextBlock"), item =>
            (string?)item.Attribute("Text") == "{Binding SelectedTradeDetail.OpenQuantity}");
        Assert.DoesNotContain("Sort by Open Quantity", ReadTradesView());
        Assert.Equal("20", 20.0m.ToString(format, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("0.12", 0.120m.ToString(format, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("0,12", 0.120m.ToString(format, System.Globalization.CultureInfo.GetCultureInfo("fr-FR")));
        Assert.Equal("0.0000000000000000000000000001",
            0.0000000000000000000000000001m.ToString(format, System.Globalization.CultureInfo.InvariantCulture));
    }

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
    public void RowOutcomeUsesNetThenGrossAndAveragePricesDisplayExactlyTwoDecimals()
    {
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XDocument view = XDocument.Parse(ReadTradesView());
        XElement list = Assert.Single(view.Descendants(presentation + "ItemsControl"), item =>
            (string?)item.Attribute("ItemsSource") == "{Binding RecentTrades}");

        XElement[] rowTriggers = list.Descendants(presentation + "DataTrigger")
            .Where(trigger => trigger.Descendants(presentation + "MultiBinding")
                .Any(binding => (string?)binding.Attribute("Converter") ==
                    "{StaticResource TradeRowOutcomeConverter}"))
            .ToArray();
        Assert.Equal(2, rowTriggers.Length);
        Assert.All(rowTriggers, trigger => Assert.Equal(
            ["NetPnL", "GrossPnL"],
            trigger.Descendants(presentation + "Binding")
                .Select(binding => (string?)binding.Attribute("Path"))));
        Assert.Contains(rowTriggers, trigger =>
            (string?)trigger.Attribute("Value") == "{x:Static converters:PnLOutcome.Negative}" &&
            trigger.Descendants(presentation + "Setter").Any(setter =>
                (string?)setter.Attribute("Value") == "{DynamicResource PtjDangerSurfaceBrush}"));
        Assert.Contains(rowTriggers, trigger =>
            (string?)trigger.Attribute("Value") == "{x:Static converters:PnLOutcome.Positive}" &&
            trigger.Descendants(presentation + "Setter").Any(setter =>
                (string?)setter.Attribute("Value") == "{DynamicResource PtjSuccessSurfaceBrush}"));

        string xaml = ReadTradesView();
        Assert.Contains("Text=\"{Binding AverageEntryPrice, StringFormat={}{0:F2}}\"", xaml);
        Assert.Contains("Text=\"{Binding AverageExitPrice, StringFormat={}{0:F2}, TargetNullValue=—}\"", xaml);
        Assert.Contains("Text=\"{Binding SelectedTradeDetail.AverageEntryPrice, StringFormat={}{0:F2}}\"", xaml);
        Assert.Contains("Text=\"{Binding SelectedTradeDetail.AverageExitPrice, StringFormat={}{0:F2}, TargetNullValue=—}\"", xaml);

        Assert.Equal("30907.38", 30907.375m.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("30907.48", 30907.48333333m.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("7688.00", 7688m.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("30907,38", 30907.375m.ToString("F2", System.Globalization.CultureInfo.GetCultureInfo("fr-FR")));
    }

    [Fact]
    public void ListAndDetailColorEachKnownGrossAndNetAmountByItsOwnSign()
    {
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XDocument view = XDocument.Parse(ReadTradesView());
        string[] amountPaths =
        [
            "GrossPnL",
            "NetPnL",
            "SelectedTradeDetail.GrossPnL",
            "SelectedTradeDetail.NetPnL",
        ];

        foreach (string path in amountPaths)
        {
            XElement amount = Assert.Single(view.Descendants(presentation + "TextBlock"), item =>
                (string?)item.Attribute("Text") == $"{{Binding {path}}}" ||
                (string?)item.Attribute("Text") == $"{{Binding {path}, TargetNullValue=—}}");
            XElement[] triggers = amount.Descendants(presentation + "DataTrigger")
                .Where(trigger => (string?)trigger.Attribute("Binding") ==
                    $"{{Binding {path}, Converter={{StaticResource PnLOutcomeConverter}}}}")
                .ToArray();

            Assert.Contains(triggers, trigger =>
                (string?)trigger.Attribute("Value") == "{x:Static converters:PnLOutcome.Positive}" &&
                trigger.Descendants(presentation + "Setter").Any(setter =>
                    (string?)setter.Attribute("Value") == "{DynamicResource PtjSuccessBrush}"));
            Assert.Contains(triggers, trigger =>
                (string?)trigger.Attribute("Value") == "{x:Static converters:PnLOutcome.Negative}" &&
                trigger.Descendants(presentation + "Setter").Any(setter =>
                    (string?)setter.Attribute("Value") == "{DynamicResource PtjDangerBrush}"));
        }
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
            "NetPnL",
        ];

        Assert.Equal(
            5,
            Regex.Matches(
                view,
                "BasedOn=\"\\{StaticResource TradeSortHeaderButtonStyle\\}\"")
                .Count);
        Assert.Equal(
            5,
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
            "Value=\"\\{DynamicResource PtjSurfaceElevatedBrush\\}\"").Count >= 5);
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
            "Sort by Net P&amp;L",
        ];
        string[] sortColumns =
        [
            "OpenedAtUtc",
            "Instrument",
            "Account",
            "AverageEntryPrice",
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
        Assert.DoesNotContain("OpenQuantitySortIndicator", view, StringComparison.Ordinal);
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
            "<ColumnDefinition Width=\"1.1\\*\" MinWidth=\"140\" />\\s*" +
            "<ColumnDefinition Width=\"1.25\\*\" MinWidth=\"150\" />\\s*" +
            "<ColumnDefinition Width=\"1.2\\*\" MinWidth=\"155\" />\\s*" +
            "<ColumnDefinition Width=\"80\" />\\s*" +
            "<ColumnDefinition Width=\"180\" />\\s*" +
            "<ColumnDefinition Width=\"122\" />";

        Assert.True(Regex.Matches(view, columns).Count >= 2);
        Assert.Contains("{DynamicResource PtjSuccessSurfaceBrush}", view, StringComparison.Ordinal);
        Assert.Contains("{DynamicResource PtjDangerSurfaceBrush}", view, StringComparison.Ordinal);
        Assert.Contains("{DynamicResource PtjSuccessBrush}", view, StringComparison.Ordinal);
        Assert.Contains("{DynamicResource PtjDangerBrush}", view, StringComparison.Ordinal);
        Assert.DoesNotMatch(
            new Regex("#[0-9A-Fa-f]{6,8}", RegexOptions.CultureInvariant),
            view);
    }

    [Fact]
    public void TradeTableScrollsAtNarrowWidthsAndKeepsValuesReadable()
    {
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        XDocument view = XDocument.Parse(ReadTradesView());
        XElement scroller = Assert.Single(view.Descendants(presentation + "ScrollViewer"), element =>
            (string?)element.Attribute(xaml + "Name") == "TradeListScrollViewer");
        Assert.Equal("Auto", (string?)scroller.Attribute("HorizontalScrollBarVisibility"));
        Assert.Equal("Disabled", (string?)scroller.Attribute("VerticalScrollBarVisibility"));
        XElement table = Assert.Single(scroller.Elements(presentation + "StackPanel"));
        Assert.Equal("980", (string?)table.Attribute("MinWidth"));
        Assert.Equal("{Binding ViewportWidth, ElementName=TradeListScrollViewer}",
            (string?)table.Attribute("Width"));

        XElement header = Assert.Single(table.Elements(presentation + "Grid"));
        XElement rows = Assert.Single(table.Elements(presentation + "ItemsControl"));
        XElement card = Assert.Single(rows.Descendants(presentation + "DataTemplate")
            .SelectMany(template => template.Elements(presentation + "Border")));
        XElement row = Assert.Single(card.Elements(presentation + "Grid"));
        Assert.Equal("13,0,13,8", (string?)header.Attribute("Margin"));
        Assert.Equal("12", (string?)card.Attribute("Padding"));
        Assert.Equal("1", (string?)card.Attribute("BorderThickness"));
        Assert.Equal(
            header.Element(presentation + "Grid.ColumnDefinitions")!.Elements(presentation + "ColumnDefinition")
                .Select(column => ((string?)column.Attribute("Width"), (string?)column.Attribute("MinWidth"))),
            row.Element(presentation + "Grid.ColumnDefinitions")!.Elements(presentation + "ColumnDefinition")
                .Select(column => ((string?)column.Attribute("Width"), (string?)column.Attribute("MinWidth"))));

        XElement account = Assert.Single(row.Elements(presentation + "TextBlock"), item =>
            (string?)item.Attribute("Text") == "{Binding TradingAccountName}");
        Assert.Equal("CharacterEllipsis", (string?)account.Attribute("TextTrimming"));
        Assert.Equal("{Binding TradingAccountName}", (string?)account.Attribute("ToolTip"));
        Assert.Contains(row.Descendants(presentation + "TextBlock"), item =>
            (string?)item.Attribute("Text") == "Net unavailable: commission/fees unknown." &&
            (string?)item.Attribute("TextWrapping") == "Wrap");
        Assert.Contains(row.Descendants(presentation + "TextBlock"), item =>
            (string?)item.Attribute("Text") == "{Binding AverageEntryPrice, StringFormat={}{0:F2}}" &&
            (string?)item.Attribute("TextWrapping") == "Wrap");
        Assert.Contains(row.Descendants(presentation + "TextBlock"), item =>
            (string?)item.Attribute("Text") == "{Binding AverageExitPrice, StringFormat={}{0:F2}, TargetNullValue=—}" &&
            (string?)item.Attribute("TextWrapping") == "Wrap");

        // MainWindow reserves 252 DIP for navigation, 64 for content padding,
        // and the Trades card reserves 48 for padding. Its table needs 980 DIP.
        Assert.True(1366 - 252 - 64 - 48 >= 980);
        Assert.True(1024 - 252 - 64 - 48 < 980);
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
