using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PersonalTradingJournal.Desktop.Tests.Journals;

internal static class JournalButtonAssertions
{
    public static void CallToAction(Button button)
    {
        var hover = Key(typeof(UIElement), "IsMouseOverPropertyKey");
        var pressed = Key(typeof(ButtonBase), "IsPressedPropertyKey");
        var focus = Key(typeof(UIElement), "IsKeyboardFocusedPropertyKey");
        button.ApplyTemplate();
        var chrome = Assert.IsType<Border>(button.Template.FindName("ButtonChrome", button));
        var indicator = Assert.IsType<Border>(button.Template.FindName("FocusIndicator", button));
        try
        {
            button.SetValue(hover, false); button.SetValue(pressed, false); button.SetValue(focus, false);
            Check(button, "PtjAccentBrush");
            Assert.NotEqual(Color(button.FindResource("PtjJournalActionBrush")), Color(button.Background));
            button.SetValue(hover, true);
            Assert.Equal(.88, chrome.Opacity);
            button.SetValue(pressed, true);
            Assert.Equal(.72, chrome.Opacity);
            button.SetValue(focus, true);
            Assert.NotEqual(Colors.Transparent, Color(indicator.BorderBrush));
            button.IsEnabled = false;
            Assert.Equal(.45, chrome.Opacity);
        }
        finally
        {
            button.SetValue(hover, false); button.SetValue(pressed, false); button.SetValue(focus, false);
            button.ClearValue(UIElement.IsEnabledProperty);
        }
        Assert.True(button.Focusable);
    }

    // Exercise compiled theme/template triggers deterministically, not a live-pointer claim.
    public static void States(Button button, string kind)
    {
        var hover = Key(typeof(UIElement), "IsMouseOverPropertyKey");
        var pressed = Key(typeof(ButtonBase), "IsPressedPropertyKey");
        var focus = Key(typeof(UIElement), "IsKeyboardFocusedPropertyKey");
        button.ApplyTemplate();
        var indicator = Assert.IsType<Rectangle>(button.Template.FindName("FocusIndicator", button));
        try
        {
            button.SetValue(hover, false); button.SetValue(pressed, false); button.SetValue(focus, false);
            Check(button, $"PtjJournal{kind}Brush");
            Assert.Equal(Visibility.Collapsed, indicator.Visibility);
            button.SetValue(hover, true);
            Check(button, $"PtjJournal{kind}HoverBrush");
            button.SetValue(pressed, true);
            Check(button, $"PtjJournal{kind}PressedBrush");
            button.SetValue(focus, true);
            Assert.Equal(Visibility.Visible, indicator.Visibility);
            Assert.NotEmpty(indicator.StrokeDashArray);
            button.IsEnabled = false;
            Check(button, "PtjSurfaceBrush");
            Assert.Equal(Color(button.FindResource("PtjTextSecondaryBrush")), Color(button.Foreground));
        }
        finally
        {
            button.SetValue(hover, false); button.SetValue(pressed, false); button.SetValue(focus, false);
            button.ClearValue(UIElement.IsEnabledProperty);
        }
        Assert.True(button.Focusable);
    }

    private static DependencyPropertyKey Key(Type type, string name) =>
        (DependencyPropertyKey)type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;

    private static Color Color(object brush) => Assert.IsType<SolidColorBrush>(brush).Color;

    private static void Check(Button button, string resource)
    {
        var background = Color(button.Background);
        Assert.Equal(Color(button.FindResource(resource)), background);
        var a = Luminance(background); var b = Luminance(Color(button.Foreground));
        Assert.True((Math.Max(a, b) + .05) / (Math.Min(a, b) + .05) >= 4.5, $"{resource}: insufficient text contrast");
    }

    private static double Luminance(Color c)
    {
        static double Channel(byte v) { double x = v / 255d; return x <= .04045 ? x / 12.92 : Math.Pow((x + .055) / 1.055, 2.4); }
        return .2126 * Channel(c.R) + .7152 * Channel(c.G) + .0722 * Channel(c.B);
    }
}
