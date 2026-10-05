using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Journals;
using PersonalTradingJournal.Desktop.Views.Journals;

namespace PersonalTradingJournal.Desktop.Tests.Journals;

public sealed class JournalHistoryViewTests
{
    [Fact]
    public async Task CompiledHistoryPagesAndSnapshotsWrapInBothThemesAndRemainReadOnlyAtHighDpi()
    {
        var reader = new JournalHistoryTestReader();
        reader.Add(new(2026, 10, 5), revision: 23);
        var vm = new JournalHistoryViewModel(reader, _ => true);
        await vm.ActivateAsync(null);
        await vm.OpenCommand.ExecuteAsync(vm.Entries[0]);
        await vm.ViewRevisionCommand.ExecuteAsync(vm.Revisions[0]);
        await OnSta(() =>
        {
            _ = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (var (theme, width, dpi) in new[] { ("Light", 960, 96), ("Dark", 960, 96), ("Light", 480, 240), ("Dark", 480, 240) })
            {
                using var phase = CalendarStaTest.Phase($"History {theme} {width} DIP / {dpi} DPI");
                var resources = CalendarViewLayoutTests.SharedThemeResources(theme);
                System.Windows.Application.Current.Resources = resources;
                var view = new JournalHistoryView { DataContext = vm };
                ((Expander)view.FindName("HistoryExpander")).IsExpanded = true;
                var root = new Border { Resources = resources, Child = new ScrollViewer { Content = view, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled } };
                root.SetResourceReference(Border.BackgroundProperty, "PtjBackgroundBrush");
                root.Measure(new Size(width, 720));
                root.Arrange(new Rect(0, 0, width, 720));
                root.UpdateLayout();
                Assert.Equal(20, ((ItemsControl)view.FindName("HistoryRevisions")).Items.Count);
                Assert.Single(((ItemsControl)view.FindName("HistoryEntries")).Items);
                foreach (string name in new[] { "RevisionText", "RevisionWentWell", "RevisionNeedsImprovement", "RevisionNextTradingDay" })
                {
                    var text = (TextBox)view.FindName(name);
                    Assert.True(text.IsReadOnly);
                    Assert.True(text.Focusable);
                    Assert.Equal(BindingMode.OneWay, BindingOperations.GetBinding(text, TextBox.TextProperty)!.Mode);
                    Assert.Contains("read-only", AutomationProperties.GetName(text));
                    Assert.InRange(text.ActualWidth, 100, width);
                    Assert.NotNull(text.Foreground);
                }
                Assert.Equal("  text 23\r\n", ((TextBox)view.FindName("RevisionText")).Text);
                var buttons = Descendants(view).OfType<Button>().Where(b => b.Content?.ToString() is "Open review" or "View revision").ToArray();
                Assert.Equal(21, buttons.Length);
                Assert.All(buttons, b => { Assert.True(b.Focusable); Assert.True(b.IsEnabled); Assert.InRange(b.TranslatePoint(new Point(b.ActualWidth, 0), root).X, 0, width); Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(b))); });
                var open = buttons.Single(b => b.Content.ToString() == "Open review");
                Assert.Same(vm.OpenCommand, open.Command);
                Assert.Same(vm.Entries[0], open.CommandParameter);
                var bitmap = new RenderTargetBitmap(width * dpi / 96, 720 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
                bitmap.Render(root);
                Assert.Equal(width * dpi / 96, bitmap.PixelWidth);
                if (Environment.GetEnvironmentVariable("PTJ_JOURNAL_RENDER_DIRECTORY") is { Length: > 0 } path)
                {
                    Directory.CreateDirectory(path);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var output = File.Create(Path.Combine(path, $"journal-history-{theme}-{width}-{dpi}.png"));
                    encoder.Save(output);
                }
                ((ScrollViewer)root.Child).Content = null;
            }
        });
        vm.Deactivate();
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    private static readonly Lazy<Task> Host = new(() => IsolatedTestProcess.RunSuiteAsync(
        typeof(JournalHistoryViewTests), "journal-history", "PTJ_JOURNAL_HISTORY_TEST_HOST", TimeSpan.FromMinutes(2), caseHangTimeout: TimeSpan.FromSeconds(30)));
    private static Task OnSta(Action action) => Environment.GetEnvironmentVariable("PTJ_JOURNAL_HISTORY_TEST_HOST") == "1"
        ? CalendarStaTest.RunAsync(action, "Journal history render") : Host.Value;
}
