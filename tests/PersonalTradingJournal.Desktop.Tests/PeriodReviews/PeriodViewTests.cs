using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Views.PeriodReviews;
using PersonalTradingJournal.Desktop.ViewModels.PeriodReviews;

namespace PersonalTradingJournal.Desktop.Tests.PeriodReviews;

public sealed class PeriodViewTests
{
    private static readonly Lazy<Task> Host = new(() => IsolatedTestProcess.RunSuiteAsync(typeof(PeriodViewTests),
        "period-review-layout", "PTJ_PERIOD_REVIEW_HOST", TimeSpan.FromMinutes(2), caseHangTimeout: TimeSpan.FromSeconds(30)));
    [Theory] [InlineData("Light", 1100, 96)] [InlineData("Dark", 1100, 96)] [InlineData("Light", 480, 240)] [InlineData("Dark", 480, 240)]
    public async Task BoundReportCardsWrapScrollAndKeepActionsAccessible(string theme, int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable("PTJ_PERIOD_REVIEW_HOST") != "1") { await Host.Value; return; }
        var f = new PeriodFixture(); await f.Vm.ActivateAsync(); await f.Vm.GenerateCommand.ExecuteAsync(null); Assert.NotNull(f.Vm.Report);
        await CalendarStaTest.RunAsync(() =>
        {
            var app = System.Windows.Application.Current ?? new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
            var view = new PeriodReviewView { DataContext = f.Vm }; var root = new Border { Child = view };
            root.SetResourceReference(Border.BackgroundProperty, "PtjBackgroundBrush");
            void Layout() { root.Measure(new(width, 760)); root.Arrange(new(0, 0, width, 760)); root.UpdateLayout(); Flush(); }
            Layout(); var scroll = (ScrollViewer)view.FindName("PeriodScroll");
            Assert.Equal(0, scroll.ScrollableWidth); Assert.True(scroll.ScrollableHeight > 0);
            var texts = Descendants(view).OfType<TextBlock>().Select(t => t.Text).ToArray();
            foreach (string title in new[] { "Strengths", "Areas for Improvement", "Mistakes Observed", "Progress", "Action Plan" }) Assert.Contains(title, texts);
            Assert.DoesNotContain(texts, s => s.Contains(f.Provider.Packet!.PacketId) || s.Contains("current:statistics") || s.Contains("Source details"));
            foreach (var button in Descendants(view).OfType<Button>().Where(b => b.IsVisible && b.Content is string))
            { Assert.True(button.Focusable && button.IsTabStop); Assert.InRange(button.TranslatePoint(new(button.ActualWidth, 0), view).X, 0, width); }
            Assert.Equal("Generate and save AI period review", AutomationProperties.GetName((Button)view.FindName("Generate")));
            Assert.Contains("New York", ((TextBlock)view.FindName("PeriodHeading")).Text);
            Assert.DoesNotContain(Descendants(view).OfType<Button>(), b => b.Visibility == Visibility.Visible && Equals(b.Content, "Cancel generation"));
            Render(root, theme, width, dpi, "top");
            var cards = Descendants(view).OfType<ReviewCardsPanel>().Single();
            var first = (FrameworkElement)VisualTreeHelper.GetChild(cards, 0); var second = (FrameworkElement)VisualTreeHelper.GetChild(cards, 1);
            if (width >= 840) Assert.Equal(first.TranslatePoint(new(), cards).Y, second.TranslatePoint(new(), cards).Y);
            else Assert.True(second.TranslatePoint(new(), cards).Y > first.TranslatePoint(new(), cards).Y);
            scroll.ScrollToVerticalOffset(((ContentControl)view.FindName("SavedReport")).TranslatePoint(new(), scroll).Y); Layout(); Render(root, theme, width, dpi, "cards");
            var citationExpander = Descendants(view).OfType<Expander>().First(e => Equals(e.Header, "Saved evidence citations")); citationExpander.IsExpanded = true; Layout();
            var citation = Descendants(citationExpander).OfType<Button>().First(b => b.DataContext is PeriodCitation);
            Assert.True(citation.IsEnabled); Assert.Contains("saved citation", AutomationProperties.GetName(citation)); citation.Command!.Execute(citation.CommandParameter); Layout(); Assert.NotNull(f.Vm.Citation);
            foreach (var expander in Descendants(view).OfType<Expander>().Where(e => e.Header is string s && s.StartsWith("Current ")).ToArray()) expander.IsExpanded = true;
            Layout(); Assert.Equal(0, scroll.ScrollableWidth); scroll.ScrollToEnd(); Layout(); Assert.InRange(scroll.VerticalOffset, scroll.ScrollableHeight - 1, scroll.ScrollableHeight + 1);
            Render(root, theme, width, dpi, "history");
            f.Vm.Deactivate(); view.DataContext = null; root.Child = null;
        }, shutdownDispatcher: false);
    }
    private static void Flush() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    internal static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    { for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); yield return child; foreach (var c in Descendants(child)) yield return c; } }
    private static void Render(Border root, string theme, int width, int dpi, string section)
    {
        var bitmap = new RenderTargetBitmap(width * dpi / 96, 760 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(root);
        Assert.Equal(width * dpi / 96, bitmap.PixelWidth);
        if (Environment.GetEnvironmentVariable("PTJ_PERIOD_RENDER_DIRECTORY") is not { Length: > 0 } directory) return;
        Directory.CreateDirectory(directory); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, $"period-{theme}-{width}-{dpi}-{section}.png")); png.Save(file);
    }
}
