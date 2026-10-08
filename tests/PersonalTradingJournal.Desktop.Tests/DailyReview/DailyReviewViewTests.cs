using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Views.DailyReview;

namespace PersonalTradingJournal.Desktop.Tests.DailyReview;

public sealed class DailyReviewViewTests
{
    private static readonly Lazy<Task> Host = new(() => IsolatedTestProcess.RunSuiteAsync(
        typeof(DailyReviewViewTests), "daily-review-layout", "PTJ_DAILY_REVIEW_TEST_HOST",
        TimeSpan.FromMinutes(2), caseHangTimeout: TimeSpan.FromSeconds(30)));

    [Fact]
    public async Task CompiledWorkspaceSupportsBothThemesNarrowHighDpiAndReachableSavedEvidenceWithoutGeneration()
    {
        if (Environment.GetEnvironmentVariable("PTJ_DAILY_REVIEW_TEST_HOST") != "1") { await Host.Value; return; }
        var f = new ReviewFixture();
        f.Reader.Handler = (q, _) => Task.FromResult(ReviewFixture.Evidence(q));
        f.History.Items.Add(ReviewFixture.Saved(ReviewFixture.Evidence(new(ReviewFixture.Day))));
        await f.Vm.ActivateAsync();
        await f.Vm.OpenAnalysisCommand.ExecuteAsync(f.Vm.Analyses.Single());
        await CalendarStaTest.RunAsync(() =>
        {
            _ = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (var (theme, width, dpi) in new[] { ("Light", 960, 96), ("Dark", 960, 96), ("Light", 480, 240), ("Dark", 480, 240) })
            {
                using var phase = CalendarStaTest.Phase($"Daily Review {theme} {width} DIP / {dpi} DPI");
                var resources = CalendarViewLayoutTests.SharedThemeResources(theme);
                System.Windows.Application.Current.Resources = resources;
                var view = new DailyReviewView { DataContext = f.Vm };
                var root = new Border { Resources = resources, Child = view };
                root.SetResourceReference(Border.BackgroundProperty, "PtjBackgroundBrush");
                root.Measure(new Size(width, 760)); root.Arrange(new Rect(0, 0, width, 760)); root.UpdateLayout();
                Flush(); root.UpdateLayout();
                var scroll = (ScrollViewer)view.FindName("ReviewScroll");
                var date = (DatePicker)view.FindName("ReviewDate");
                var account = (ComboBox)view.FindName("ReviewAccount");
                Assert.Equal(f.Vm.SelectedDate, date.SelectedDate);
                Assert.Equal(f.Vm.SelectedAccount, account.SelectedItem);
                Assert.Contains("New York", AutomationProperties.GetName(date));
                Assert.True(account.Focusable && date.Focusable);
                Assert.True(scroll.ScrollableHeight > 0);
                Assert.Equal(0, scroll.ScrollableWidth);
                var buttons = Descendants(view).OfType<Button>().Where(b => b.Visibility == Visibility.Visible && b.Content is string && b.Command is not null).ToArray();
                Assert.Contains(buttons, b => Equals(b.Content, "Open saved analysis") && b.IsEnabled);
                Assert.Contains(buttons, b => Equals(b.Content, "Delete analysis") && b.IsEnabled);
                Assert.DoesNotContain(buttons, b => b.Content.ToString()!.Contains("Generate", StringComparison.OrdinalIgnoreCase));
                Assert.All(buttons, b =>
                {
                    Assert.True(b.Focusable);
                    double right = b.TranslatePoint(new Point(b.ActualWidth, 0), view).X;
                    Assert.InRange(right, 0, width + 1);
                });
                var text = Descendants(view).OfType<TextBlock>().Select(t => t.Text).ToArray();
                Assert.Contains("Saved snapshot · NOT current records", text);
                Assert.Contains(text, t => t.Contains("Net coverage 0/1"));
                Assert.Contains(text, t => t.Contains("All accounts · aggregate analysis"));
                var bodies = Descendants(view).OfType<TextBlock>().Where(t => ReferenceEquals(t.Style, view.Resources["ReviewBody"])).ToArray();
                Assert.True(bodies.Length > 20);
                Assert.All(bodies, t =>
                {
                    Assert.Equal(TextWrapping.Wrap, t.TextWrapping);
                    Assert.Equal(((SolidColorBrush)resources["PtjTextPrimaryBrush"]).Color, ((SolidColorBrush)t.Foreground).Color);
                    Assert.True(t.ActualWidth <= width);
                });
                Assert.All(Descendants(view).OfType<Expander>(), e =>
                    Assert.Equal(((SolidColorBrush)resources["PtjTextPrimaryBrush"]).Color, ((SolidColorBrush)e.Foreground).Color));
                Render(root, theme, width, dpi, "current");
                scroll.ScrollToVerticalOffset(scroll.VerticalOffset + ((ItemsControl)view.FindName("AnalysisHistory")).TranslatePoint(new Point(), scroll).Y);
                Flush(); root.UpdateLayout();
                Render(root, theme, width, dpi, "history");
                scroll.ScrollToVerticalOffset(scroll.VerticalOffset + ((ContentControl)view.FindName("SavedSnapshot")).TranslatePoint(new Point(), scroll).Y);
                Flush(); root.UpdateLayout();
                Render(root, theme, width, dpi, "snapshot");
                scroll.ScrollToEnd(); Flush(); root.UpdateLayout();
                Assert.InRange(scroll.VerticalOffset, scroll.ScrollableHeight - 1, scroll.ScrollableHeight + 1);
                Assert.Equal(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
                Assert.Single(Descendants(view).OfType<ScrollViewer>(), s => s.ScrollableHeight > 0);
                root.Child = null;
            }
        });
    }

    private static void Flush() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    private static void Render(Border root, string theme, int width, int dpi, string section)
    {
        var bitmap = new RenderTargetBitmap(width * dpi / 96, 760 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(root);
        Assert.Equal(width * dpi / 96, bitmap.PixelWidth);
        if (Environment.GetEnvironmentVariable("PTJ_REVIEW_RENDER_DIRECTORY") is not { Length: > 0 } directory) return;
        Directory.CreateDirectory(directory);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, $"review-{section}-{theme}-{width}-{dpi}.png"));
        encoder.Save(file);
    }
}
