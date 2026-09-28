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
    public void CompletedResultHidesAccountSectionButKeepsConfirmationSectionVisible()
    {
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XDocument document = XDocument.Load(Path.Combine(
            RepositoryRoot, "src", "PersonalTradingJournal.Desktop", "Views", "Import", "ImportView.xaml"));

        XElement account = FindSection(document, presentation, "2. Trading account");
        Assert.Contains(account.Descendants(presentation + "DataTrigger"), trigger =>
            (string?)trigger.Attribute("Binding") == "{Binding HasImportResult}" &&
            (string?)trigger.Attribute("Value") == "True" &&
            trigger.Descendants(presentation + "Setter").Any(setter =>
                (string?)setter.Attribute("Property") == "Visibility" &&
                (string?)setter.Attribute("Value") == "Collapsed"));

        XElement confirmation = FindSection(document, presentation, "3. Confirm import");
        Assert.Contains(confirmation.Descendants(presentation + "DataTrigger"), trigger =>
            (string?)trigger.Attribute("Binding") == "{Binding ShowConfirmationSection}" &&
            (string?)trigger.Attribute("Value") == "True" &&
            trigger.Descendants(presentation + "Setter").Any(setter =>
                (string?)setter.Attribute("Property") == "Visibility" &&
                (string?)setter.Attribute("Value") == "Visible"));

        XElement source = FindSection(document, presentation, "1. Source file");
        Assert.DoesNotContain(source.Descendants(presentation + "DataTrigger"), trigger =>
            (string?)trigger.Attribute("Binding") == "{Binding HasImportResult}");
    }

    [Fact]
    public void ConfirmationHeadingUsesStepNumberOnlyForActivePreview()
    {
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XDocument document = XDocument.Load(Path.Combine(
            RepositoryRoot, "src", "PersonalTradingJournal.Desktop", "Views", "Import", "ImportView.xaml"));
        XElement confirmation = FindSection(document, presentation, "3. Confirm import");
        XElement numbered = Assert.Single(confirmation.Descendants(presentation + "TextBlock"), item =>
            (string?)item.Attribute("Text") == "3. Confirm import");
        XElement completed = Assert.Single(confirmation.Descendants(presentation + "TextBlock"), item =>
            (string?)item.Attribute("Text") == "Confirm import");

        Assert.Contains(numbered.Descendants(presentation + "DataTrigger"), trigger =>
            (string?)trigger.Attribute("Binding") == "{Binding HasImportResult}" &&
            (string?)trigger.Attribute("Value") == "True" &&
            trigger.Descendants(presentation + "Setter").Any(setter =>
                (string?)setter.Attribute("Property") == "Visibility" &&
                (string?)setter.Attribute("Value") == "Collapsed"));
        Assert.Contains(completed.Descendants(presentation + "DataTrigger"), trigger =>
            (string?)trigger.Attribute("Binding") == "{Binding HasImportResult}" &&
            (string?)trigger.Attribute("Value") == "True" &&
            trigger.Descendants(presentation + "Setter").Any(setter =>
                (string?)setter.Attribute("Property") == "Visibility" &&
                (string?)setter.Attribute("Value") == "Visible"));
    }

    [Fact]
    public void CandidateWeightedPricesAreFormattedOnlyAtTheVisibleBindings()
    {
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XDocument document = XDocument.Load(Path.Combine(
            RepositoryRoot, "src", "PersonalTradingJournal.Desktop", "Views", "Import", "ImportView.xaml"));
        XElement candidates = Assert.Single(document.Descendants(presentation + "ItemsControl"), element =>
            (string?)element.Attribute("ItemsSource") == "{Binding Trades}");

        foreach (string binding in new[]
        {
            "{Binding AverageEntry, StringFormat={}{0:F2}}",
            "{Binding AverageExit, StringFormat={}{0:F2}, TargetNullValue=—}",
        })
        {
            XElement price = Assert.Single(candidates.Descendants(presentation + "TextBlock"), element =>
                (string?)element.Attribute("Text") == binding);
            Assert.Equal("CharacterEllipsis", (string?)price.Attribute("TextTrimming"));
            Assert.Equal("True", (string?)price.Parent?.Attribute("ClipToBounds"));
        }

        var culture = System.Globalization.CultureInfo.InvariantCulture;
        Assert.Equal("30907.38", 30907.375m.ToString("F2", culture));
        Assert.Equal("30907.48", 30907.48333333m.ToString("F2", culture));
        Assert.Equal("30894.89", 30894.88636363m.ToString("F2", culture));
        Assert.Equal("30897.48", 30897.47727272m.ToString("F2", culture));
        Assert.Equal("7688.00", 7688m.ToString("F2", culture));
        Assert.Equal("30907,38", 30907.375m.ToString(
            "F2", System.Globalization.CultureInfo.GetCultureInfo("fr-FR")));
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

    private static XElement FindSection(XDocument document, XNamespace presentation, string title) =>
        Assert.Single(document.Descendants(presentation + "Border"), border =>
            border.Element(presentation + "StackPanel")?.Elements(presentation + "TextBlock")
                .Any(text => (string?)text.Attribute("Text") == title) == true);

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
