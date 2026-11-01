using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed class NotebookMenuLayoutTests
{
    [Theory]
    [InlineData(1280, 96)]
    [InlineData(480, 240)]
    public async Task SmallNativeSurfaceRetainsLogicalViewportAndContainsActionTargets(int width, int dpi)
    {
        await CalendarStaTest.RunAsync(() =>
        {
            var target = new Button { Width = 32, Height = 32, HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top };
            var root = new Grid(); root.Children.Add(target);
            var viewport = NotebookMenuLayout.Viewport(root, width, 932);
            // Controlled small surface, independent of the developer's monitor size.
            viewport.Measure(new Size(1000, 680)); viewport.Arrange(new Rect(0, 0, 1000, 680)); viewport.UpdateLayout();
            Assert.Equal(width, root.ActualWidth); Assert.Equal(932, root.ActualHeight);
            var visible = target.TransformToAncestor(viewport).TransformBounds(new Rect(target.RenderSize));
            Assert.True(new Rect(viewport.RenderSize).Contains(visible));
            var logical = target.TranslatePoint(new Point(), root); double scale = dpi / 96d;
            NotebookOverflowEvidence.AssertValidCrop(new Int32Rect((int)Math.Round(logical.X * scale),
                (int)Math.Round(logical.Y * scale), (int)(32 * scale), (int)(32 * scale)),
                (int)Math.Ceiling(root.ActualWidth * scale), (int)Math.Ceiling(root.ActualHeight * scale));
        });
    }

    [Theory]
    [InlineData(1248, 0, 32, 32, 1024, 768)] // Original wide fixture: action outside constrained host.
    [InlineData(-1, 0, 32, 32, 1280, 932)]
    [InlineData(0, 0, 0, 32, 1280, 932)]
    public void InvalidEvidenceRegionFailsInsteadOfClamping(int x, int y, int width, int height, int surfaceWidth, int surfaceHeight)
    {
        var error = Assert.Throws<Xunit.Sdk.TrueException>(() =>
            NotebookOverflowEvidence.AssertValidCrop(new Int32Rect(x, y, width, height), surfaceWidth, surfaceHeight));
        Assert.Contains("must be wholly inside", error.Message);
    }
}
