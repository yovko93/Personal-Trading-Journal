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
        Assert.Equal(new[] { "{Binding StartDate}", "{Binding EndDate}" }, view.Descendants(p + "DatePicker").Select(e => (string?)e.Attribute("SelectedDate")));
        Assert.All(view.Descendants(p + "DatePicker"), e =>
        {
            Assert.NotNull(e.Attribute("AutomationProperties.Name"));
            Assert.Equal("OnDateValidationError", (string?)e.Attribute("DateValidationError"));
        });
        foreach (string command in new[] { "ApplyRange", "Today", "LastWeek", "LastMonth", "AllHistory" })
            Assert.Contains(view.Descendants(p + "Button"), e => (string?)e.Attribute("Command") == $"{{Binding {command}Command}}");
        Assert.Contains("Applied range:", xaml);
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
        XElement ring = Assert.Single(view.Descendants(chart + "WinRateRing"));
        Assert.Equal("True", (string?)ring.Attribute("Focusable"));
        Assert.Equal("{Binding Description}", (string?)ring.Attribute("AutomationProperties.Name"));
        Assert.Equal("{DynamicResource PtjSuccessBrush}", (string?)ring.Attribute("WinBrush"));
        Assert.Equal("{DynamicResource PtjDangerBrush}", (string?)ring.Attribute("LossBrush"));
        Assert.Equal("{DynamicResource PtjTextMutedBrush}", (string?)ring.Attribute("NeutralBrush"));
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
