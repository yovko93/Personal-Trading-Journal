using System.Text.RegularExpressions;

namespace PersonalTradingJournal.Desktop.Tests.Resources;

public sealed class EntityActionResourceTests
{
    private static readonly string[] RequiredControlKeys =
    [
        "PtjDangerButtonStyle",
        "PtjIconButtonStyle",
        "PtjEntityActionButtonStyle",
        "PtjEntityActionMenuStyle",
        "PtjEntityActionMenuItemStyle",
        "PtjLifecycleMenuItemStyle",
        "PtjDeleteMenuItemStyle",
        "PtjDetailLabelTextStyle",
        "PtjDetailValueTextStyle",
        "PtjStatusBadgeStyle",
        "PtjActiveStatusTextStyle",
        "PtjInactiveStatusTextStyle",
    ];

    private static readonly string[] RequiredIconKeys =
    [
        "PtjIconView",
        "PtjIconEdit",
        "PtjIconMore",
        "PtjIconDelete",
        "PtjIconActivate",
        "PtjIconDeactivate",
    ];

    [Fact]
    public void SharedEntityActionAndDetailResourcesAreCentralized()
    {
        string controls = ReadDesktopFile("Resources", "Controls.xaml");
        string icons = ReadDesktopFile("Resources", "Icons.xaml");

        foreach (string key in RequiredControlKeys)
        {
            Assert.Contains($"x:Key=\"{key}\"", controls, StringComparison.Ordinal);
        }

        foreach (string key in RequiredIconKeys)
        {
            Assert.Contains($"x:Key=\"{key}\"", icons, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void DeleteAndLifecycleActionsUseDifferentSemanticBrushes()
    {
        string controls = ReadDesktopFile("Resources", "Controls.xaml");

        Assert.Matches(
            new Regex(
                "PtjDeleteMenuItemStyle[\\s\\S]*?PtjDangerBrush",
                RegexOptions.CultureInvariant),
            controls);
        Assert.Matches(
            new Regex(
                "PtjLifecycleMenuItemStyle[\\s\\S]*?PtjTextSecondaryBrush",
                RegexOptions.CultureInvariant),
            controls);
    }

    [Fact]
    public void SharedActionAndDialogXamlContainNoHardcodedColors()
    {
        string controls = ReadDesktopFile("Resources", "Controls.xaml");
        string dialog = ReadDesktopFile("Dialogs", "PtjDialogWindow.xaml");

        Assert.DoesNotMatch("#[0-9A-Fa-f]{3,8}", controls);
        Assert.DoesNotMatch("#[0-9A-Fa-f]{3,8}", dialog);
    }

    [Fact]
    public void ConfirmationDialogDefaultsKeyboardFocusToCancel()
    {
        string dialog = ReadDesktopFile("Dialogs", "PtjDialogWindow.xaml");

        Assert.Contains("IsCancel=\"True\"", dialog, StringComparison.Ordinal);
        Assert.Contains("IsDefault=\"True\"", dialog, StringComparison.Ordinal);
        Assert.Contains("PtjDangerButtonStyle", ReadDesktopFile(
            "Dialogs",
            "PtjDialogWindow.xaml.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void OverflowBehaviorProvidesAnAccessibleDefaultName()
    {
        string behavior = ReadDesktopFile(
            "Interactions",
            "EntityActionMenu.cs");

        Assert.Contains(
            "AutomationProperties.SetName(button, \"More actions\")",
            behavior,
            StringComparison.Ordinal);
        Assert.Contains(
            "button.ToolTip ??= \"More actions\"",
            behavior,
            StringComparison.Ordinal);
    }

    private static string ReadDesktopFile(params string[] segments) =>
        File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "PersonalTradingJournal.Desktop",
            Path.Combine(segments)));

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
