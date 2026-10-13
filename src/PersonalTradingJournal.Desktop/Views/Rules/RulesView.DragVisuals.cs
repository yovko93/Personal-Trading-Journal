using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using PersonalTradingJournal.Desktop.ViewModels.Rules;

namespace PersonalTradingJournal.Desktop.Views.Rules;

public partial class RulesView
{
    private Border? _liftSource;
    private Point _grabOffset;
    private double _originalOpacity;

    internal Cursor? RowCursor(DependencyObject? hit, Border row) =>
        DataContext is RulesViewModel { IsBusy: false } && CanStartRowDrag(hit, row)
            ? ReferenceEquals(_dragRow, row) ? RuleDragCursor.Grabbing : Cursors.Hand : null;

    private void OnRowQueryCursor(object sender, QueryCursorEventArgs e)
    {
        if (sender is Border row && RowCursor(e.OriginalSource as DependencyObject, row) is { } cursor)
        {
            e.Cursor = cursor; e.Handled = true;
        }
    }

    private void OnDragFeedback(object sender, GiveFeedbackEventArgs e)
    {
        // No global OverrideCursor: another window/control must never inherit the drag cursor.
        e.UseDefaultCursors = false;
        Mouse.SetCursor(LiftedRule.Visibility == Visibility.Visible ? RuleDragCursor.Grabbing : Cursors.No);
        e.Handled = true;
    }

    internal bool BeginRowLift(Border source, Point pointer)
    {
        EndRowDrag();
        if (source.ActualWidth <= 0 || source.ActualHeight <= 0) return false;
        // Snapshot the actual laid-out row, including its number, text, condition and actions.
        // No duplicate bindings/commands or live VisualBrush that would inherit placeholder styling.
        double dpiScale = Math.Max(VisualTreeHelper.GetDpi(this).DpiScaleX, VisualTreeHelper.GetDpi(source).DpiScaleX);
        double scale = Math.Min(dpiScale, Math.Sqrt(8_000_000d / (source.ActualWidth * source.ActualHeight)));
        var bitmap = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(source.ActualWidth * scale)),
            Math.Max(1, (int)Math.Ceiling(source.ActualHeight * scale)), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        var drawing = new DrawingVisual();
        var cardBounds = new Rect(0, 0, source.ActualWidth, source.ActualHeight);
        using (var context = drawing.RenderOpen())
            // Explicit bounds exclude shadow overflow and a collapsed disclosure's old
            // descendant bounds. Stretching their union would squash the captured card.
            context.DrawRectangle(new VisualBrush(source)
            {
                AutoLayoutContent = false, ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = cardBounds, Stretch = Stretch.Fill
            }, null, cardBounds);
        bitmap.Render(drawing); bitmap.Freeze();
        _liftSource = source; _originalOpacity = source.Opacity;
        Vector offset = pointer - source.TranslatePoint(new Point(), DragOverlay);
        _grabOffset = new Point(offset.X, offset.Y);
        LiftedRuleImage.Source = bitmap;
        // Scale only the frozen image, never the stored card or its text layout. Respect
        // reduced motion and clamp to the viewport; no neighbor animation or layout churn.
        double liftScale = SystemParameters.ClientAreaAnimation ? 1.015 : 1;
        double fit = Math.Min(liftScale, Math.Max(1, DragOverlay.ActualWidth - 16) / source.ActualWidth);
        LiftedRuleImage.Width = source.ActualWidth * fit; LiftedRuleImage.Height = source.ActualHeight * fit;
        _grabOffset = new Point(_grabOffset.X * fit, _grabOffset.Y * fit);
        MoveRowLift(pointer);
        return true;
    }

    internal void MoveRowLift(Point pointer)
    {
        if (_liftSource is null) return;
        _liftSource.Opacity = _originalOpacity * 0.55;
        OriginMarker(_liftSource).Visibility = Visibility.Visible;
        LiftedRule.Visibility = Visibility.Visible;
        // Keep its width in the viewport, while following vertical pointer motion. Clip only
        // to the page viewport, never create another window or cover the app's other controls.
        Canvas.SetLeft(LiftedRule, Math.Clamp(pointer.X - _grabOffset.X + 8, 8,
            Math.Max(8, DragOverlay.ActualWidth - LiftedRuleImage.Width - 12)));
        Canvas.SetTop(LiftedRule, pointer.Y - _grabOffset.Y + 12);
    }

    internal void HideRowLift()
    {
        LiftedRule.Visibility = Visibility.Collapsed;
        if (_liftSource is not null)
        {
            _liftSource.Opacity = _originalOpacity;
            OriginMarker(_liftSource).Visibility = Visibility.Collapsed;
        }
        ClearDropHighlights();
        Mouse.UpdateCursor();
    }

    private static Rectangle OriginMarker(Border source) =>
        Descendants(source).OfType<Rectangle>().Single(r => r.Name == "DragOriginMarker");
}
