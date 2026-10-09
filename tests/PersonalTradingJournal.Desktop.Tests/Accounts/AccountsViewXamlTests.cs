namespace PersonalTradingJournal.Desktop.Tests.Accounts;

public sealed class AccountsViewXamlTests
{
    [Fact]
    public void BothAccountMenusPlaceDistinctBulkDeletionImmediatelyBeforeAccountDeletion()
    {
        var document = System.Xml.Linq.XDocument.Parse(File.ReadAllText(GetAccountsViewPath()));
        System.Xml.Linq.XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var menus = document.Descendants(ns + "ContextMenu").ToArray();
        Assert.Equal(2, menus.Length);
        foreach (var menu in menus)
        {
            var actions = menu.Elements(ns + "MenuItem").ToArray();
            Assert.Equal("Delete All Trades", (string?)actions[^2].Attribute("Header"));
            Assert.Equal("Delete", (string?)actions[^1].Attribute("Header"));
            Assert.Contains("DeleteAllTradesCommand", (string?)actions[^2].Attribute("Command"));
            Assert.NotEqual((string?)actions[^1].Attribute("Style"), (string?)actions[^2].Attribute("Style"));
            Assert.Equal((string?)actions[^1].Attribute("CommandParameter"), (string?)actions[^2].Attribute("CommandParameter"));
        }
    }

    [Fact]
    public void AccountActionsUseSharedResourcesAndValidPopupBindingPaths()
    {
        string xaml = File.ReadAllText(GetAccountsViewPath());

        Assert.Contains("ViewAccountCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("EditAccountCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("DeleteAccountCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("PtjEntityActionMenuStyle", xaml, StringComparison.Ordinal);
        Assert.Contains("PtjDeleteMenuItemStyle", xaml, StringComparison.Ordinal);
        Assert.Contains("EntityActionMenu.OpensContextMenu", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("DataContext.Tag", xaml, StringComparison.Ordinal);
        Assert.DoesNotMatch("#[0-9A-Fa-f]{3,8}", xaml);
    }

    private static string GetAccountsViewPath() => Path.Combine(
        FindRepositoryRoot(),
        "src",
        "PersonalTradingJournal.Desktop",
        "Views",
        "Accounts",
        "AccountsView.xaml");

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
