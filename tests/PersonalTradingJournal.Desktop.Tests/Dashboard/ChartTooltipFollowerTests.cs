using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
            Assert.Equal(PlacementMode.RelativePoint, tip.Placement);
            Assert.Same(root, tip.PlacementTarget);
            Point first = new(tip.HorizontalOffset, tip.VerticalOffset);
            tip.IsOpen = true;
            follower.Follow(new Point(30, 35));
            Point second = new(tip.HorizontalOffset, tip.VerticalOffset);
            follower.Follow(new Point(60, 65));
            Point third = new(tip.HorizontalOffset, tip.VerticalOffset);
            Assert.Equal(new Point(26, 26), first);
            Assert.Equal(new Point(46, 51), second);
            Assert.Equal(new Point(76, 81), third);
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
            Assert.Equal(first.TranslatePoint(new Point(20, 30), scroll).X + 16, firstTip.HorizontalOffset, 6);
            secondFollower.Follow(new Point(20, 30));
            Assert.Same(scroll, secondTip.PlacementTarget);
            Assert.Equal(second.TranslatePoint(new Point(20, 30), scroll).X + 16, secondTip.HorizontalOffset, 6);
            Assert.NotEqual(firstTip.HorizontalOffset, secondTip.HorizontalOffset);
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
            Assert.Equal(PlacementMode.RelativePoint, tip.Placement);
            Assert.Same(root, tip.PlacementTarget);
        });
    }

    private static void Draw(FrameworkElement element)
    {
        element.Measure(new Size(element.Width, element.Height));
        element.Arrange(new Rect(0, 0, element.Width, element.Height));
        element.UpdateLayout();
        new RenderTargetBitmap((int)element.Width, (int)element.Height, 96, 96, PixelFormats.Pbgra32).Render(element);
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
