using System.Text.RegularExpressions;

namespace PersonalTradingJournal.Desktop.Tests.Resources;

public sealed class TradeOutcomePresentationTests
{
    [Fact]
    public void BothThemesDefineTradeOutcomeSurfaceBrushes()
    {
        foreach (string theme in new[] { "DarkTheme.xaml", "LightTheme.xaml" })
        {
            string content = File.ReadAllText(Path.Combine(
                FindRepositoryRoot(),
                "src",
                "PersonalTradingJournal.Desktop",
                "Resources",
                "Themes",
                theme));

            Assert.Contains("x:Key=\"PtjSuccessSurfaceBrush\"", content, StringComparison.Ordinal);
            Assert.Contains("x:Key=\"PtjDangerSurfaceBrush\"", content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TradesViewUsesSemanticPnlForegroundsAndNeutralNullPresentation()
    {
        string view = ReadTradesView();

        Assert.Contains("SelectedTradeDetail.GrossPnL, Converter={StaticResource PnLOutcomeConverter}", view, StringComparison.Ordinal);
        Assert.Contains("SelectedTradeDetail.NetPnL, Converter={StaticResource PnLOutcomeConverter}", view, StringComparison.Ordinal);
        Assert.Contains("NetPnL, Converter={StaticResource PnLOutcomeConverter}", view, StringComparison.Ordinal);
        Assert.Contains("{DynamicResource PtjSuccessBrush}", view, StringComparison.Ordinal);
        Assert.Contains("{DynamicResource PtjDangerBrush}", view, StringComparison.Ordinal);
        Assert.Contains("{DynamicResource PtjTextMutedBrush}", view, StringComparison.Ordinal);
        Assert.Contains("Binding=\"{Binding NetPnL}\" Value=\"{x:Null}\"", view, StringComparison.Ordinal);
        Assert.Contains("Text=\"—\"", view, StringComparison.Ordinal);
    }

    [Fact]
    public void RecentTradeRowsUseDistinctThemeAwareOutcomeCards()
    {
        string view = ReadTradesView();

        Assert.Contains("Margin=\"0,0,0,8\"", view, StringComparison.Ordinal);
        Assert.Contains("CornerRadius=\"{StaticResource PtjCornerRadiusMd}\"", view, StringComparison.Ordinal);
        Assert.Contains("{DynamicResource PtjSurfaceElevatedBrush}", view, StringComparison.Ordinal);
        Assert.Contains("{DynamicResource PtjSuccessSurfaceBrush}", view, StringComparison.Ordinal);
        Assert.Contains("{DynamicResource PtjDangerSurfaceBrush}", view, StringComparison.Ordinal);
    }

    [Fact]
    public void TradesViewKeepsActionsNeutralAndContainsNoHardcodedColors()
    {
        string view = ReadTradesView();

        Assert.DoesNotMatch(
            new Regex("#[0-9A-Fa-f]{6,8}", RegexOptions.CultureInvariant),
            view);
        Assert.Contains("ShowTradeDetailCommand", view, StringComparison.Ordinal);
        Assert.Contains("ShowTradeEditCommand", view, StringComparison.Ordinal);
        Assert.Contains("EntityActionMenu.OpensContextMenu", view, StringComparison.Ordinal);
        Assert.Contains("PtjDeleteMenuItemStyle", view, StringComparison.Ordinal);
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
