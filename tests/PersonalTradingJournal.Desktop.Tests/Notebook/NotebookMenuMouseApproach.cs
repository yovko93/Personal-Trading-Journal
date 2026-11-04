using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

// A single native movement after popup readiness, not repeated identical cursor writes.
// CI117's controlled probe demonstrates that identical reports can retain stale hover.
internal sealed class NotebookMenuMouseApproach(FrameworkElement target, Action<Point, bool>? observe = null)
{
    internal bool IsPrepared { get; private set; }
    internal Point Position { get; private set; }

    internal bool TryPrepare(bool animationSettled)
    {
        if (IsPrepared) return true;
        if (!animationSettled || !target.IsLoaded || !target.IsVisible || !target.IsEnabled ||
            !target.IsMeasureValid || !target.IsArrangeValid) return false;
        var center = target.PointToScreen(new Point(target.ActualWidth / 2, target.ActualHeight / 2));
        center = new Point((int)center.X, (int)center.Y);
        var approach = new Point(center.X + 2, center.Y);
        // Physical pixels, checked through the target's own DPI/viewport transform.
        if (!new Rect(target.RenderSize).Contains(target.PointFromScreen(approach))) return false;
        if (!HitsTarget(center) || !HitsTarget(approach)) return false;
        bool positioned = SetCursorPos((int)approach.X, (int)approach.Y);
        observe?.Invoke(approach, positioned);
        Assert.True(positioned, "Native menu approach positioning failed.");
        Position = approach;
        IsPrepared = true;
        return true;
    }

    internal bool HitsTarget(Point point)
    {
        if (PresentationSource.FromVisual(target) is not HwndSource { RootVisual: UIElement root } source ||
            !new Rect(target.RenderSize).Contains(target.PointFromScreen(point)) ||
            WindowFromPoint(new((int)point.X, (int)point.Y)) != source.Handle) return false;
        for (DependencyObject? hit = root.InputHitTest(root.PointFromScreen(point)) as DependencyObject;
             hit is not null; hit = hit is Visual ? VisualTreeHelper.GetParent(hit) : LogicalTreeHelper.GetParent(hit))
            if (ReferenceEquals(hit, target)) return true;
        return false;
    }

    [StructLayout(LayoutKind.Sequential)] private readonly record struct NativePoint(int X, int Y);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(NativePoint point);
}
