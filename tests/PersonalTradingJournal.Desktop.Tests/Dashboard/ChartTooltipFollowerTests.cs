using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Views.Dashboard;

namespace PersonalTradingJournal.Desktop.Tests.Dashboard;

public sealed class ChartTooltipFollowerTests
{
    [Fact]
    public async Task OpenTooltipFollowsRepeatedPointerMovesWithoutReplacingContentOrCapturingInput()
    {
        await OnSta(() =>
        {
            var owner = new Border { Width = 100, Height = 100, Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            var root = new Grid { Width = 500, Height = 300 };
            root.Children.Add(owner);
            var tip = new ToolTip { Content = "Exact chart value" };
            var follower = new ChartTooltipFollower(owner, tip);
            Draw(root);
            follower.Follow(new Point(10, 10));
            Assert.Equal(PlacementMode.Relative, tip.Placement);
            Assert.Same(root, tip.PlacementTarget);
            Point first = new(tip.HorizontalOffset, tip.VerticalOffset);
            tip.IsOpen = true;
            follower.Follow(new Point(30, 35));
            Point second = new(tip.HorizontalOffset, tip.VerticalOffset);
            follower.Follow(new Point(60, 65));
            Point third = new(tip.HorizontalOffset, tip.VerticalOffset);
            double alignment = SystemParameters.MenuDropAlignment ? tip.DesiredSize.Width : 0;
            Assert.Equal(new Point(26 + alignment, 26), first);
            Assert.Equal(new Point(46 + alignment, 51), second);
            Assert.Equal(new Point(76 + alignment, 81), third);
            Assert.True(tip.IsOpen);
            Assert.Equal("Exact chart value", tip.Content);
            Assert.False(tip.IsHitTestVisible || tip.Focusable);
            Assert.Equal(int.MaxValue, ToolTipService.GetShowDuration(owner));
            tip.IsOpen = false;
        });
    }

    [Fact]
    public async Task ScrollingAndSwitchingTargetsUseFreshViewportCoordinatesAndContent()
    {
        await OnSta(() =>
        {
            var plot = new Canvas { Width = 1200, Height = 200 };
            var first = new Border { Width = 80, Height = 100, Background = Brushes.Transparent };
            var second = new Border { Width = 80, Height = 100, Background = Brushes.Transparent };
            Canvas.SetLeft(first, 400);
            Canvas.SetLeft(second, 500);
            plot.Children.Add(first);
            plot.Children.Add(second);
            var scroll = new ScrollViewer { Width = 360, Height = 200, Content = plot,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
            var firstTip = new ToolTip { Content = "First day" };
            var secondTip = new ToolTip { Content = "Second day" };
            var firstFollower = new ChartTooltipFollower(first, firstTip);
            var secondFollower = new ChartTooltipFollower(second, secondTip);
            Draw(scroll);
            scroll.ScrollToHorizontalOffset(350);
            scroll.UpdateLayout();
            firstFollower.Follow(new Point(20, 30));
            Assert.Same(scroll, firstTip.PlacementTarget);
            Assert.Equal(first.TranslatePoint(new Point(20, 30), scroll).X + 16 +
                (SystemParameters.MenuDropAlignment ? firstTip.DesiredSize.Width : 0), firstTip.HorizontalOffset, 6);
            secondFollower.Follow(new Point(20, 30));
            Assert.Same(scroll, secondTip.PlacementTarget);
            Assert.Equal(second.TranslatePoint(new Point(20, 30), scroll).X + 16 +
                (SystemParameters.MenuDropAlignment ? secondTip.DesiredSize.Width : 0), secondTip.HorizontalOffset, 6);
            Assert.Equal(first.TranslatePoint(new Point(20, 30), scroll).Y + 16, firstTip.VerticalOffset, 6);
            Assert.Equal(second.TranslatePoint(new Point(20, 30), scroll).Y + 16, secondTip.VerticalOffset, 6);
            Assert.Equal("First day", firstTip.Content);
            Assert.Equal("Second day", secondTip.Content);
            Assert.Equal(100, ToolTipService.GetInitialShowDelay(first));
        });
    }

    [Theory]
    [InlineData(10, 10, 26, 26)]
    [InlineData(490, 290, 374, 194)]
    [InlineData(490, 10, 374, 26)]
    [InlineData(10, 290, 26, 194)]
    public void PositionFlipsInsideWindowAtEveryEdge(double pointerX, double pointerY, double expectedX, double expectedY)
    {
        Point point = ChartTooltipFollower.PositionInside(new Point(pointerX, pointerY), new Size(100, 80), new Size(500, 300));
        Assert.Equal(new Point(expectedX, expectedY), point);
        Assert.InRange(point.X, 8, 392);
        Assert.InRange(point.Y, 8, 212);
    }

    [Fact]
    public async Task KeyboardFocusAnchorsTooltipAndIgnoresMouseUntilFocusClears()
    {
        await OnSta(() =>
        {
            var owner = new Border { Width = 100, Height = 100 };
            var tip = new ToolTip { Content = "Focused chart point" };
            var follower = new ChartTooltipFollower(owner, tip);
            var root = new Grid { Width = 500, Height = 300 };
            root.Children.Add(owner);
            Draw(root);
            follower.Follow(new Point(10, 10));
            follower.AnchorToKeyboard();
            Assert.True(tip.IsOpen);
            Assert.Equal(PlacementMode.Bottom, tip.Placement);
            Assert.Same(owner, tip.PlacementTarget);
            follower.Follow(new Point(90, 90));
            Assert.Equal(0, tip.HorizontalOffset);
            Assert.Equal(8, tip.VerticalOffset);
            follower.ReleaseKeyboard();
            Assert.False(tip.IsOpen);
            follower.Follow(new Point(20, 20));
            Assert.Equal(PlacementMode.Relative, tip.Placement);
            Assert.Same(root, tip.PlacementTarget);
        });
    }

    [Theory]
    [InlineData(96, 0)]
    [InlineData(240, 0)]
    [InlineData(96, 350)]
    [InlineData(240, 350)]
    public async Task HoverPlacementUsesWindowCoordinatesAfterScrollingAtEitherRenderDpi(int dpi, double scrollOffset)
    {
        await OnSta(() =>
        {
            var root = new Canvas { Width = 500, Height = 300 };
            var plot = new Canvas { Width = 1200, Height = 200 };
            var owner = new Border { Width = 80, Height = 100, Background = Brushes.Transparent };
            Canvas.SetLeft(owner, 400);
            Canvas.SetTop(owner, 30);
            plot.Children.Add(owner);
            var scroll = new ScrollViewer { Width = 360, Height = 200, Content = plot,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
            Canvas.SetLeft(scroll, 60);
            Canvas.SetTop(scroll, 40);
            root.Children.Add(scroll);
            Draw(root, dpi);
            scroll.ScrollToHorizontalOffset(scrollOffset);
            scroll.UpdateLayout();

            var tip = new ToolTip { Width = 100, Height = 80, Content = "Exact chart value" };
            var follower = new ChartTooltipFollower(owner, tip);
            var pointerInOwner = new Point(20, 30);
            follower.Follow(pointerInOwner);

            Point pointerInRoot = owner.TranslatePoint(pointerInOwner, root);
            Point intendedInRoot = ChartTooltipFollower.PositionInside(pointerInRoot,
                new Size(100, 80), root.RenderSize);
            Point actualInRoot = new(
                tip.HorizontalOffset - (SystemParameters.MenuDropAlignment ? tip.DesiredSize.Width : 0),
                tip.VerticalOffset);
            Assert.Same(root, tip.PlacementTarget);
            Assert.Equal(intendedInRoot.X, actualInRoot.X, 6);
            Assert.Equal(intendedInRoot.Y, actualInRoot.Y, 6);
            // WPF offsets and TranslatePoint use DIPs; the same placement survives
            // physical rendering at 96 and 240 DPI without scaling the offsets twice.
            Assert.Equal(intendedInRoot.X * dpi / 96, actualInRoot.X * dpi / 96, 6);
            Assert.Equal(intendedInRoot.Y * dpi / 96, actualInRoot.Y * dpi / 96, 6);
        });
    }

    [Theory]
    [InlineData(96)]
    [InlineData(240)]
    public async Task HoverPlacementFlipsInsideViewportEdgesAtEitherRenderDpi(int dpi)
    {
        await OnSta(() =>
        {
            var root = new Canvas { Width = 500, Height = 300 };
            var owner = new Border { Width = 30, Height = 30, Background = Brushes.Transparent };
            Canvas.SetLeft(owner, 460);
            Canvas.SetTop(owner, 260);
            root.Children.Add(owner);
            Draw(root, dpi);
            var tip = new ToolTip { Width = 100, Height = 80, Content = "Edge value" };
            var follower = new ChartTooltipFollower(owner, tip);
            follower.Follow(new Point(20, 20));

            Point popupInRoot = new(
                tip.HorizontalOffset - (SystemParameters.MenuDropAlignment ? tip.DesiredSize.Width : 0),
                tip.VerticalOffset);
            Assert.Equal(new Point(364, 184), popupInRoot);
            Assert.InRange(popupInRoot.X + tip.Width, 0, root.ActualWidth);
            Assert.InRange(popupInRoot.Y + tip.Height, 0, root.ActualHeight);
        });
    }

    [Fact]
    public async Task OpenPopupAppearsBesidePointerAndMovesAfterHorizontalScroll()
    {
        await OnSta(() =>
        {
            var plot = new Canvas { Width = 1200, Height = 200 };
            var owner = new Border { Width = 80, Height = 100, Background = Brushes.Transparent };
            Canvas.SetLeft(owner, 400);
            Canvas.SetTop(owner, 30);
            plot.Children.Add(owner);
            var scroll = new ScrollViewer { Width = 360, Height = 200, Content = plot,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
            var root = new Grid();
            root.Children.Add(scroll);
            var window = new Window { Width = 500, Height = 300, Left = 100, Top = 100,
                // Pointer coordinates are scripted; keep native geometry but exclude ambient hover.
                WindowStyle = WindowStyle.None, Content = root, ShowInTaskbar = false, IsHitTestVisible = false };
            var tip = new ToolTip { Width = 100, Height = 80, Content = "Exact chart value" };
            owner.ToolTip = tip;
            var follower = new ChartTooltipFollower(owner, tip);
            using var popup = new TooltipPopupReadiness(tip, owner, window);
            try
            {
                window.Show();
                PumpDispatcher();
                scroll.ScrollToHorizontalOffset(350);
                scroll.UpdateLayout();
                var pointerInOwner = new Point(20, 30);
                follower.Follow(pointerInOwner);
                popup.Verify(() => AssertPopupNearExpected(tip, owner, pointerInOwner, root),
                    () => tip.IsOpen = true);

                follower.Follow(new Point(30, 40));
                popup.Verify(() => AssertPopupNearExpected(tip, owner, new Point(30, 40), root));

                scroll.ScrollToHorizontalOffset(320);
                scroll.UpdateLayout();
                follower.Follow(pointerInOwner);
                popup.Verify(() => AssertPopupNearExpected(tip, owner, pointerInOwner, root));
            }
            finally
            {
                tip.IsOpen = false;
                window.Close();
            }
        });
    }

    [Fact]
    public async Task OpenPopupFlipsBesidePointerNearWindowRightAndBottomEdges()
    {
        await OnSta(() =>
        {
            var root = new Canvas();
            var owner = new Border { Width = 30, Height = 30, Background = Brushes.Transparent };
            Canvas.SetLeft(owner, 460);
            Canvas.SetTop(owner, 260);
            root.Children.Add(owner);
            var window = new Window { Width = 500, Height = 300, Left = 100, Top = 100,
                WindowStyle = WindowStyle.None, Content = root, ShowInTaskbar = false, IsHitTestVisible = false };
            var tip = new ToolTip { Width = 100, Height = 80, Content = "Edge value" };
            owner.ToolTip = tip;
            var follower = new ChartTooltipFollower(owner, tip);
            using var popup = new TooltipPopupReadiness(tip, owner, window);
            try
            {
                window.Show();
                PumpDispatcher();
                follower.Follow(new Point(20, 20));
                popup.Verify(() => AssertPopupNearExpected(tip, owner, new Point(20, 20), root),
                    () => tip.IsOpen = true);
            }
            finally
            {
                tip.IsOpen = false;
                window.Close();
            }
        });
    }

    [Fact]
    public async Task PopupReadinessWaitsForDeferredOpeningAndMeasuresNativePixels()
    {
        await WithEdgePopup((window, root, owner, tip, popup) =>
        {
            // A single ApplicationIdle pump is not an observable opening contract.
            window.Dispatcher.BeginInvoke(DispatcherPriority.SystemIdle, new Action(() => tip.IsOpen = true));
            PumpDispatcher();
            Assert.False(tip.IsOpen);
            Assert.Null(PresentationSource.FromVisual(tip));
            popup.Verify(() => AssertPopupNearExpected(tip, owner, new Point(20, 20), root));
            Assert.Equal(1, popup.OpenedCount);
            Assert.Equal(0, popup.ClosedCount);
        });
    }

    [Fact]
    public async Task PopupReadinessMeasuresBeforeUnrelatedDeactivationAndRejectsClosingWithoutReopening()
    {
        await WithEdgePopup((window, root, owner, tip, popup) =>
        {
            bool deactivated = false;
            window.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                var source = Assert.IsType<System.Windows.Interop.HwndSource>(PresentationSource.FromVisual(tip));
                // Native app deactivation, as when another test process activates a window.
                SendMessage(source.Handle, 0x001C, IntPtr.Zero, IntPtr.Zero);
                deactivated = true;
            }));
            popup.Verify(() => AssertPopupNearExpected(tip, owner, new Point(20, 20), root),
                () => tip.IsOpen = true);
            Assert.False(deactivated); // No unrelated idle drain between readiness and measurement.
            PumpDispatcher();
            Assert.True(deactivated);
            Assert.False(tip.IsOpen);
            Assert.NotNull(PresentationSource.FromVisual(root));
            bool measured = false;
            var error = Assert.Throws<InvalidOperationException>(() => popup.Verify(() => measured = true));
            Assert.Contains("IsOpen=False", error.Message);
            Assert.Contains("Opened=1", error.Message);
            Assert.Contains("target=Window", error.Message);
            Assert.False(measured || tip.IsOpen);
            Assert.Equal(1, popup.OpenedCount);
        });
    }

    [Fact]
    public async Task PopupReadinessDeadlineReportsUnopenedStateWithoutMeasuringOrRetrying()
    {
        await WithEdgePopup((_, _, _, tip, popup) =>
        {
            bool measured = false;
            // An immediate deadline exercises timeout cleanup without spending the real budget.
            var error = Assert.Throws<TimeoutException>(() =>
                popup.Verify(() => measured = true, timeout: TimeSpan.Zero));
            Assert.Contains("IsOpen=False", error.Message);
            Assert.Contains("Source=null", error.Message);
            Assert.Contains("Opened=0", error.Message);
            Assert.Contains("Closed=0", error.Message);
            Assert.False(measured || tip.IsOpen);
        });
    }

    private static Task WithEdgePopup(Action<Window, Canvas, Border, ToolTip, TooltipPopupReadiness> action) => OnSta(() =>
    {
        var root = new Canvas();
        var owner = new Border { Width = 30, Height = 30, Background = Brushes.Transparent };
        Canvas.SetLeft(owner, 460); Canvas.SetTop(owner, 260);
        root.Children.Add(owner);
        var window = new Window { Width = 500, Height = 300, Left = 100, Top = 100,
            WindowStyle = WindowStyle.None, Content = root, ShowInTaskbar = false, IsHitTestVisible = false };
        var tip = new ToolTip { Width = 100, Height = 80, Content = "Edge value" };
        owner.ToolTip = tip;
        var follower = new ChartTooltipFollower(owner, tip);
        using var popup = new TooltipPopupReadiness(tip, owner, window);
        try
        {
            window.Show(); PumpDispatcher();
            follower.Follow(new Point(20, 20));
            action(window, root, owner, tip, popup);
        }
        finally { tip.IsOpen = false; window.Close(); }
    });

    private static void AssertPopupNearExpected(ToolTip tip, UIElement owner, Point pointerInOwner, UIElement root)
    {
        Point pointer = owner.TranslatePoint(pointerInOwner, root);
        Point intended = ChartTooltipFollower.PositionInside(pointer,
            new Size(tip.ActualWidth, tip.ActualHeight), root.RenderSize);
        Point intendedScreen = root.PointToScreen(intended);
        Point actualScreen = tip.PointToScreen(new Point());
        // PointToScreen reports physical pixels; WPF performs the DIP-to-device conversion.
        Assert.True(Math.Abs(intendedScreen.X - actualScreen.X) <= 5,
            $"X: intended={intendedScreen}, actual={actualScreen}, pointer={root.PointToScreen(pointer)}, owner={owner.PointToScreen(new Point())}, offsets={tip.HorizontalOffset},{tip.VerticalOffset}, placement={tip.Placement}, size={tip.ActualWidth}x{tip.ActualHeight}, flow={tip.FlowDirection}, menuDrop={SystemParameters.MenuDropAlignment}, target={tip.PlacementTarget?.GetType().Name}, dpi={PresentationSource.FromVisual(root)?.CompositionTarget?.TransformToDevice}, workarea={SystemParameters.WorkArea}, screen={SystemParameters.VirtualScreenWidth}x{SystemParameters.VirtualScreenHeight}");
        Assert.True(Math.Abs(intendedScreen.Y - actualScreen.Y) <= 5,
            $"Y: intended={intendedScreen}, actual={actualScreen}, pointer={root.PointToScreen(pointer)}, owner={owner.PointToScreen(new Point())}, offsets={tip.HorizontalOffset},{tip.VerticalOffset}, target={tip.PlacementTarget?.GetType().Name}");
    }

    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    private static void Draw(FrameworkElement element, int dpi = 96)
    {
        element.Measure(new Size(element.Width, element.Height));
        element.Arrange(new Rect(0, 0, element.Width, element.Height));
        element.UpdateLayout();
        new RenderTargetBitmap((int)(element.Width * dpi / 96), (int)(element.Height * dpi / 96),
            dpi, dpi, PixelFormats.Pbgra32).Render(element);
    }

    private static Task OnSta(Action action)
    {
        var result = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { try { action(); result.SetResult(); } catch (Exception error) { result.SetException(error); } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return result.Task;
    }
}
