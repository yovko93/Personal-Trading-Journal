using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

internal static class NotebookMenuLayout
{
    // Keep the theory's logical viewport, but fit its native host to the actual desktop.
    // An oversized SizeToContent window is constrained by Windows while its fixed-size
    // child can remain larger, clipping the controls this fixture intends to click.
    public static Viewbox Viewport(FrameworkElement content, double width, double height)
    {
        content.Width = width;
        content.Height = height;
        return new Viewbox { Child = content, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
    }

    public static Window Window(FrameworkElement content, double width, double height)
    {
        var area = SystemParameters.WorkArea;
        return new Window
        {
            Content = Viewport(content, width, height),
            Width = Math.Min(width + 32, area.Width - 32),
            Height = Math.Min(height + 48, area.Height - 32),
            Left = area.Left + 16, Top = area.Top + 16,
            WindowStartupLocation = WindowStartupLocation.Manual, ShowInTaskbar = false
        };
    }
}
