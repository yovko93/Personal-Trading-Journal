using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace PersonalTradingJournal.Desktop.Views.Dashboard;

/// <summary>Moves an already prepared chart tooltip without touching its data or hover timing.</summary>
internal sealed class ChartTooltipFollower
{
    private const double CursorGap = 16;
    private const double EdgePadding = 8;
    private readonly UIElement _owner;
    private readonly ToolTip _tip;
    private Point? _lastPointer;
    private bool _keyboardAnchored;

    internal ChartTooltipFollower(UIElement owner, ToolTip tip)
    {
        _owner = owner;
        _tip = tip;
        _tip.IsHitTestVisible = false;
        _tip.Focusable = false;
        owner.MouseEnter += (_, e) => Follow(e.GetPosition(owner));
        owner.MouseMove += (_, e) => Follow(e.GetPosition(owner));
        // Re-measure once WPF opens the popup; subsequent offsets reposition it in place.
        tip.Opened += (_, _) =>
        {
            if (!_keyboardAnchored && _lastPointer is { } point) Follow(point);
        };
        ToolTipService.SetInitialShowDelay(owner, 100);
        ToolTipService.SetBetweenShowDelay(owner, 2000);
        ToolTipService.SetShowDuration(owner, int.MaxValue);
    }

    internal void Follow(Point pointerInOwner)
    {
        _lastPointer = pointerInOwner;
        if (_keyboardAnchored) return;
        UIElement placementRoot = Window.GetWindow(_owner) ?? VisualRoot(_owner);
        Point pointer = _owner.TranslatePoint(pointerInOwner, placementRoot);
        _tip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Size tooltipSize = new(
            _tip.ActualWidth > 0 ? _tip.ActualWidth : _tip.DesiredSize.Width,
            _tip.ActualHeight > 0 ? _tip.ActualHeight : _tip.DesiredSize.Height);
        Point position = PositionInside(pointer, tooltipSize, placementRoot.RenderSize);
        // The service otherwise coerces PlacementTarget back to the hovered element,
        // even if ToolTip.PlacementTarget names the window. Keep both the service's
        // effective target and the offsets in the same, scroll-independent space.
        ToolTipService.SetPlacementTarget(_owner, placementRoot);
        _tip.PlacementTarget = placementRoot;
        // Relative avoids RelativePoint's separate screen-edge flip. PositionInside
        // already chooses the side of the cursor within the visible chart window.
        _tip.Placement = PlacementMode.Relative;
        // Right-aligned menu systems use the popup's right edge as their X alignment
        // point. Compensate with the measured popup width, not a device-pixel constant.
        _tip.HorizontalOffset = position.X + (SystemParameters.MenuDropAlignment ? tooltipSize.Width : 0);
        _tip.VerticalOffset = position.Y;
    }

    internal void AnchorToKeyboard()
    {
        _keyboardAnchored = true;
        ToolTipService.SetPlacementTarget(_owner, _owner);
        _tip.PlacementTarget = _owner;
        _tip.Placement = PlacementMode.Bottom;
        _tip.HorizontalOffset = 0;
        _tip.VerticalOffset = EdgePadding;
        _tip.IsOpen = true;
    }

    internal void ReleaseKeyboard()
    {
        _tip.IsOpen = false;
        _keyboardAnchored = false;
    }

    internal static Point PositionInside(Point pointer, Size tooltip, Size bounds)
    {
        double maxX = Math.Max(EdgePadding, bounds.Width - tooltip.Width - EdgePadding);
        double maxY = Math.Max(EdgePadding, bounds.Height - tooltip.Height - EdgePadding);
        double x = pointer.X + CursorGap;
        double y = pointer.Y + CursorGap;
        if (x > maxX) x = pointer.X - tooltip.Width - CursorGap;
        if (y > maxY) y = pointer.Y - tooltip.Height - CursorGap;
        return new(Math.Clamp(x, EdgePadding, maxX), Math.Clamp(y, EdgePadding, maxY));
    }

    private static UIElement VisualRoot(UIElement owner)
    {
        UIElement root = owner;
        while (VisualTreeHelper.GetParent(root) is UIElement parent) root = parent;
        return root;
    }
}
