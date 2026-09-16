using System.Xml.Linq;

namespace PersonalTradingJournal.Desktop.Tests.Resources;

public sealed class FormControlStyleTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [Theory]
    [InlineData("PtjTextBoxStyle")]
    [InlineData("PtjComboBoxStyle")]
    public void CanonicalInputStyleHasRoundedChromeAndValidationTriggers(
        string styleKey)
    {
        XDocument document = XDocument.Load(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "PersonalTradingJournal.Desktop",
            "Resources",
            "Controls.xaml"));
        XElement style = Assert.Single(
            document.Descendants(Presentation + "Style"),
            element => (string?)element.Attribute(Xaml + "Key") == styleKey);

        Assert.Contains(
            style.Descendants(Presentation + "Border"),
            border => (string?)border.Attribute("CornerRadius") ==
                "{StaticResource PtjCornerRadiusMd}");
        Assert.NotEmpty(style.Descendants(Presentation + "DropShadowEffect"));
        Assert.Contains(
            style.Descendants(Presentation + "Trigger"),
            trigger => (string?)trigger.Attribute("Property") == "Validation.HasError");
        Assert.Contains(
            style.Descendants(Presentation + "Trigger"),
            trigger => (string?)trigger.Attribute("Property") ==
                "validation:FormValidation.IsInvalid");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PersonalTradingJournal.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
