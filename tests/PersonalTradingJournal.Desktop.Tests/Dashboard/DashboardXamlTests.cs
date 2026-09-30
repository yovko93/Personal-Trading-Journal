using System.IO;
using System.Xml.Linq;

namespace PersonalTradingJournal.Desktop.Tests.Dashboard;

public sealed class DashboardXamlTests
{
    [Fact]
    public void DashboardBindsOneSelectedCurrencyAndOffersAccessibleResponsiveChartsAndControls()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "PersonalTradingJournal.sln"))) root = root.Parent;
        Assert.NotNull(root);
        string xaml = File.ReadAllText(Path.Combine(root.FullName, "src/PersonalTradingJournal.Desktop/Views/Dashboard/DashboardView.xaml"));
        XDocument view = XDocument.Parse(xaml);
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace chart = "clr-namespace:PersonalTradingJournal.Desktop.Views.Dashboard";
        Assert.Contains("Cumulative Realized P&amp;L", xaml);
        Assert.DoesNotContain("Equity Curve", xaml);
        Assert.DoesNotContain("No trading data yet.", xaml);
        Assert.Contains("{Binding Period}", xaml);
        Assert.Contains("{Binding PreviousCommand}", xaml);
        Assert.Contains("{Binding NextCommand}", xaml);
        Assert.Contains("{Binding CancelCommand}", xaml);
        Assert.Contains("{Binding SelectedCurrency}", xaml);
        XElement account = Assert.Single(view.Descendants(p + "ComboBox"), e => (string?)e.Attribute("ItemsSource") == "{Binding Accounts}");
        Assert.Equal("{Binding SelectedAccount}", (string?)account.Attribute("SelectedItem"));
        Assert.Equal("Name", (string?)account.Attribute("DisplayMemberPath"));
        Assert.Equal("Dashboard account", (string?)account.Attribute("AutomationProperties.Name"));
        Assert.Empty(view.Descendants(p + "DatePicker"));
        Assert.Equal(new[] { "{Binding StartDate}", "{Binding EndDate}" }, view.Descendants(p + "Calendar").Select(e => (string?)e.Attribute("SelectedDate")));
        Assert.All(view.Descendants(p + "Calendar"), e =>
        {
            Assert.NotNull(e.Attribute("AutomationProperties.Name"));
            Assert.Equal("{StaticResource RangeCalendar}", (string?)e.Attribute("Style"));
        });
        foreach (string command in new[] { "ApplyRange", "Today", "LastWeek", "LastMonth", "AllHistory" })
            Assert.Contains(view.Descendants(p + "Button"), e => (string?)e.Attribute("Command") == $"{{Binding {command}Command}}");
        Assert.Contains("Date Range:", xaml);
        Assert.Contains("{Binding IsDateRangeOpen,", xaml);
        XElement rangeBox = Assert.Single(view.Descendants(p + "Button"), e => (string?)e.Attribute("Command") == "{Binding ToggleRangeCommand}");
        Assert.Null(rangeBox.Attribute("Visibility"));
        Assert.Equal("1", (string?)rangeBox.Attribute("BorderThickness"));
        Assert.Contains("{Binding AppliedRangeLabel}", rangeBox.ToString());
        Assert.DoesNotContain(view.Descendants(p + "Expander"), e => (string?)e.Attribute("IsExpanded") == "{Binding IsDateRangeOpen}");
        Assert.All(view.Descendants(p + "Calendar"), c =>
        {
            Assert.Equal("{Binding StartDate}", (string?)c.Attribute(chart + "CalendarRange.Start"));
            Assert.Equal("{Binding EndDate}", (string?)c.Attribute(chart + "CalendarRange.End"));
            Assert.Equal("{Binding IsRangeDraft}", (string?)c.Attribute(chart + "CalendarRange.IsDraft"));
        });
        Assert.Contains("{Binding CancelRangeCommand}", xaml);
        Assert.Contains("{Binding RangeValidationMessage}", xaml);
        XElement selected = Assert.Single(view.Descendants(p + "StackPanel"), e => (string?)e.Attribute("DataContext") == "{Binding Selected}");
        Assert.Equal(2, selected.Descendants(chart + "PnlChart").Count());
        Assert.All(selected.Descendants(chart + "PnlChart"), c =>
        {
            Assert.Equal("True", (string?)c.Attribute("Focusable"));
            Assert.Equal("{Binding PeriodStart}", (string?)c.Attribute("StartDate"));
            Assert.NotNull(c.Attribute("AutomationProperties.Name"));
            Assert.Equal("{DynamicResource PtjSuccessBrush}", (string?)c.Attribute("PositiveBrush"));
            Assert.Equal("{DynamicResource PtjDangerBrush}", (string?)c.Attribute("NegativeBrush"));
            Assert.Equal("{DynamicResource PtjTextMutedBrush}", (string?)c.Attribute("NeutralBrush"));
        });
        Assert.Equal(2, selected.Descendants(p + "Expander").Count());
        Assert.Contains(selected.Descendants(p + "WrapPanel"), _ => true);
        foreach (string binding in new[] { "Cards", "DailyPnl", "CumulativePnl", "Setups" })
            Assert.Contains(selected.Descendants(p + "ItemsControl"), c => (string?)c.Attribute("ItemsSource") == $"{{Binding {binding}}}");
        Assert.Contains(view.Descendants(p + "ItemsControl").Except(selected.Descendants(p + "ItemsControl")),
            c => (string?)c.Attribute("ItemsSource") == "{Binding RecentTrades}");
        Assert.Contains("TextWrapping=\"Wrap\"", xaml);
        Assert.DoesNotContain(view.Descendants(p + "TextBlock"), text => (string?)text.Attribute("Text") == "{Binding Explanation}");
        Assert.Contains("ToolTip=\"{Binding Explanation}\"", xaml);
        Assert.Contains("AutomationProperties.HelpText=\"{Binding Explanation}\"", xaml);
        XElement cards = Assert.Single(selected.Descendants(p + "ItemsControl"), c => (string?)c.Attribute("ItemsSource") == "{Binding Cards}");
        Assert.DoesNotContain(cards.Descendants(p + "TextBlock"), t =>
            ((string?)t.Attribute("Text")) is "Estimated" or "{Binding Badge}" or "{Binding IsEstimated}");
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement averages = Assert.Single(view.Descendants(p + "DataTemplate"), t => (string?)t.Attribute(x + "Key") == "AverageWinLossTemplate");
        XElement averageLayout = Assert.Single(averages.Elements(p + "Grid"));
        Assert.Equal("64", (string?)averageLayout.Attribute("MinHeight"));
        Assert.Equal(new[] { "86", "*" }, averageLayout.Element(p + "Grid.ColumnDefinitions")!.Elements(p + "ColumnDefinition").Select(c => (string?)c.Attribute("Width")));
        Assert.Contains(averageLayout.Descendants(p + "TextBlock"), t => (string?)t.Attribute("Text") == "Avg Win / Avg Loss");
        XElement ratio = Assert.Single(averageLayout.Descendants(p + "TextBlock"), t => (string?)t.Attribute("Text") == "{Binding RatioText}");
        Assert.Contains("Win / Loss ratio", (string?)ratio.Attribute("AutomationProperties.Name"));
        XElement right = Assert.Single(averageLayout.Elements(p + "Grid"), g => (string?)g.Attribute("Grid.Column") == "1");
        XElement amountRow = Assert.Single(right.Elements(p + "Grid"), g => (string?)g.Attribute("Grid.Row") == "1");
        Assert.Equal(new[] { "*", "*" }, amountRow.Element(p + "Grid.ColumnDefinitions")!.Elements(p + "ColumnDefinition").Select(c => (string?)c.Attribute("Width")));
        Assert.Equal("Right", (string?)Assert.Single(amountRow.Elements(p + "TextBlock"), t => (string?)t.Attribute("Grid.Column") == "1").Attribute("TextAlignment"));
        Assert.Contains(averages.Descendants(p + "TextBlock"), t => (string?)t.Attribute("Text") == "{Binding WinText}" &&
            (string?)t.Attribute("Foreground") == "{DynamicResource PtjSuccessBrush}");
        Assert.Contains(averages.Descendants(p + "TextBlock"), t => (string?)t.Attribute("Text") == "{Binding LossText}" &&
            (string?)t.Attribute("Foreground") == "{DynamicResource PtjDangerBrush}");
        Assert.Contains("{Binding RatioText", averages.ToString());
        XElement comparison = Assert.Single(averages.Descendants(chart + "AverageComparisonBar"));
        Assert.Null(comparison.Attribute("Grid.Row"));
        XElement cardWidthTrigger = Assert.Single(cards.Descendants(p + "DataTrigger"), t =>
            (string?)t.Attribute("Binding") == "{Binding Label}" && (string?)t.Attribute("Value") == "Average Win / Average Loss" &&
            t.Elements(p + "Setter").Any(s => (string?)s.Attribute("Property") == "Width"));
        Assert.Equal("280", (string?)Assert.Single(cardWidthTrigger.Elements(p + "Setter")).Attribute("Value"));
        Assert.Equal("{Binding}", (string?)comparison.Attribute("Value"));
        Assert.Equal("True", (string?)comparison.Attribute("Focusable"));
        Assert.Equal("{Binding Description}", (string?)comparison.Attribute("AutomationProperties.Name"));
        Assert.Equal("{DynamicResource PtjSuccessBrush}", (string?)comparison.Attribute("WinBrush"));
        Assert.Equal("{DynamicResource PtjDangerBrush}", (string?)comparison.Attribute("LossBrush"));
        Assert.Equal("{DynamicResource PtjTextMutedBrush}", (string?)comparison.Attribute("NeutralBrush"));
        XElement daily = Assert.Single(view.Descendants(p + "DataTemplate"), t => (string?)t.Attribute(x + "Key") == "DailyValues");
        Assert.Equal(new[] { "{Binding Date, StringFormat={}{0:yyyy-MM-dd}}", "{Binding AmountText}", "{Binding TradeCountText}" },
            daily.Descendants(p + "TextBlock").Select(t => (string?)t.Attribute("Text")));
        Assert.Contains(daily.Descendants(p + "TextBlock"), t => (string?)t.Attribute("AutomationProperties.HelpText") == "{Binding Description}");
        XElement cumulative = Assert.Single(view.Descendants(p + "DataTemplate"), t => (string?)t.Attribute(x + "Key") == "CumulativeValues");
        Assert.All(new[] { daily, cumulative }, template => Assert.Contains(template.Descendants(p + "TextBlock"),
            t => (string?)t.Attribute("Text") == "{Binding AmountText}" && (string?)t.Attribute("Style") == "{StaticResource ChartAmountStyle}"));
        XElement amountStyle = Assert.Single(view.Descendants(p + "Style"), s => (string?)s.Attribute(x + "Key") == "ChartAmountStyle");
        Assert.Contains(amountStyle.Descendants(p + "Setter"), s => (string?)s.Attribute("Value") == "{DynamicResource PtjSuccessBrush}");
        Assert.Contains(amountStyle.Descendants(p + "Setter"), s => (string?)s.Attribute("Value") == "{DynamicResource PtjDangerBrush}");
        Assert.Contains(amountStyle.Descendants(p + "Setter"), s => (string?)s.Attribute("Value") == "{DynamicResource PtjTextMutedBrush}");
        Assert.Equal(2, view.Descendants(chart + "OutcomeRing").Count());
        foreach (XElement ring in view.Descendants(chart + "OutcomeRing"))
        {
        Assert.Equal("True", (string?)ring.Attribute("Focusable"));
        Assert.Equal("{Binding Description}", (string?)ring.Attribute("AutomationProperties.Name"));
        Assert.Equal("{DynamicResource PtjSuccessBrush}", (string?)ring.Attribute("WinBrush"));
        Assert.Equal("{DynamicResource PtjDangerBrush}", (string?)ring.Attribute("LossBrush"));
        Assert.Equal("{DynamicResource PtjTextMutedBrush}", (string?)ring.Attribute("NeutralBrush"));
        }
        foreach (string binding in new[] { "WinsText", "LossesText", "BreakEvensText", "HasBreakEvens", "ProfitText", "LossText" })
            Assert.Contains("{Binding " + binding, xaml);
        Assert.Contains("{Binding HasNoSetups", xaml);
        XElement recent = Assert.Single(view.Descendants(p + "ItemsControl"), c => (string?)c.Attribute("ItemsSource") == "{Binding RecentTrades}");
        XElement action = Assert.Single(recent.Descendants(p + "Button"));
        Assert.Contains("ViewTradeCommand", (string?)action.Attribute("Command"));
        Assert.Equal("{Binding}", (string?)action.Attribute("CommandParameter"));
        Assert.Contains("PtjTradeOutcomeRowStyle", xaml);
        Assert.Contains("{Binding TradingAccountName}", xaml);
        Assert.Contains("MaxHeight=\"36\"", xaml);
        Assert.Contains("{0:F2}", xaml);
        Assert.Contains("HorizontalScrollBarVisibility=\"Auto\"", xaml);
    }
}
