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
        XElement selected = Assert.Single(view.Descendants(p + "StackPanel"), e => (string?)e.Attribute("DataContext") == "{Binding Selected}");
        Assert.Equal(2, selected.Descendants(chart + "PnlChart").Count());
        Assert.All(selected.Descendants(chart + "PnlChart"), c =>
        {
            Assert.Equal("True", (string?)c.Attribute("Focusable"));
            Assert.Equal("{Binding PeriodStart}", (string?)c.Attribute("StartDate"));
            Assert.NotNull(c.Attribute("AutomationProperties.Name"));
        });
        Assert.Equal(2, selected.Descendants(p + "Expander").Count());
        Assert.Contains(selected.Descendants(p + "WrapPanel"), _ => true);
        foreach (string binding in new[] { "Cards", "DailyPnl", "CumulativePnl", "Setups", "RecentTrades" })
            Assert.Contains(selected.Descendants(p + "ItemsControl"), c => (string?)c.Attribute("ItemsSource") == $"{{Binding {binding}}}");
        Assert.Contains("TextWrapping=\"Wrap\"", xaml);
    }
}
