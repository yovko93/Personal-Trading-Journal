namespace PersonalTradingJournal.Desktop.Tests.Accounts;

public sealed class AccountsViewXamlTests
{
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
