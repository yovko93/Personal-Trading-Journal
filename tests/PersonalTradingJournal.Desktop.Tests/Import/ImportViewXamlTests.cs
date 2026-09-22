namespace PersonalTradingJournal.Desktop.Tests.Import;

public sealed class ImportViewXamlTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void ImportViewUsesOnePageScrollAndExposesReadOnlyWorkflowSections()
    {
        string xaml = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "src",
            "PersonalTradingJournal.Desktop",
            "Views",
            "Import",
            "ImportView.xaml"));

        Assert.Equal(1, Count(xaml, "<ScrollViewer "));
        Assert.Contains("Select CSV", xaml, StringComparison.Ordinal);
        Assert.Contains("Build Preview", xaml, StringComparison.Ordinal);
        Assert.Contains("Analysis summary", xaml, StringComparison.Ordinal);
        Assert.Contains("Instrument resolution", xaml, StringComparison.Ordinal);
        Assert.Contains("Trade candidates", xaml, StringComparison.Ordinal);
        Assert.Contains("Diagnostics", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"Import\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("#", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void AppMapsImportViewModelToImportView()
    {
        string app = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "src",
            "PersonalTradingJournal.Desktop",
            "App.xaml"));

        Assert.Contains("DataType=\"{x:Type importViewModels:ImportViewModel}\"", app, StringComparison.Ordinal);
        Assert.Contains("<importViews:ImportView />", app, StringComparison.Ordinal);
    }

    private static int Count(string value, string text) =>
        value.Split(text, StringSplitOptions.None).Length - 1;

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "PersonalTradingJournal.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
