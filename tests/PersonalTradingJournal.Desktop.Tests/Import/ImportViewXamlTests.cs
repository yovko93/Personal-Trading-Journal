using System.Xml.Linq;

namespace PersonalTradingJournal.Desktop.Tests.Import;

public sealed class ImportViewXamlTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void ImportViewUsesOnePageScrollAndExposesReviewAndConfirmationSections()
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
        Assert.Contains("DisplayMemberPath=\"DisplayText\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TICK VALUE", xaml, StringComparison.Ordinal);
        Assert.Contains("Will be created only when the import is confirmed.", xaml, StringComparison.Ordinal);
        Assert.Contains("Trade candidates", xaml, StringComparison.Ordinal);
        Assert.Contains("Diagnostics", xaml, StringComparison.Ordinal);
        Assert.Contains("The preview is read-only.", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ConfirmImportCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Import Trades\"", xaml, StringComparison.Ordinal);
        Assert.Contains("DUPLICATES SKIPPED", xaml, StringComparison.Ordinal);
        Assert.Contains("INSTRUMENTS CREATED", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("#", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportRecoveryMessageIsOutsidePreviewDependentSections()
    {
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XDocument document = XDocument.Load(Path.Combine(
            RepositoryRoot, "src", "PersonalTradingJournal.Desktop", "Views", "Import", "ImportView.xaml"));
        XElement message = Assert.Single(document.Descendants(presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "{Binding ImportErrorMessage}");
        XElement page = document.Root!.Element(presentation + "ScrollViewer")!
            .Element(presentation + "StackPanel")!;

        // Account-not-found clears HasPreview. Recovery must remain a direct page child,
        // not disappear with the preview or confirmation section.
        Assert.Same(page, message.Parent);
        Assert.Contains(message.Descendants(presentation + "DataTrigger"), trigger =>
            (string?)trigger.Attribute("Binding") == "{Binding ImportErrorMessage}" &&
            (string?)trigger.Attribute("Value") == "{x:Null}");
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
