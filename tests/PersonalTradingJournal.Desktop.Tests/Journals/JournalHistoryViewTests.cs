using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Threading;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Domain.Journals;
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
        var journalPages = new List<JournalViewModel>();
        foreach (var _ in Enumerable.Range(0, 4))
        {
            var longReader = new JournalHistoryTestReader();
            var item = longReader.Add(new(2026, 10, 5));
            string text = string.Join("\n", Enumerable.Range(1, 45).Select(i => $"Line {i}: exact historical content, wrapping at narrow widths."));
            longReader.Snapshots[0] = new(item.Id, 1, text, true, JournalHistoryTestReader.Now, new(text, text, text));
            var page = new JournalViewModel(new EmptyRepository(), new FakeTradingAccountReader(), new FakeDialogService(),
                new(new FakeTradingCalendarDayReader(), new FakeTradingAccountReader()), new JournalHistoryViewModelTests.Clock(), longReader);
            await page.ActivateAsync();
            await page.History!.OpenCommand.ExecuteAsync(page.History.Entries[0]);
            await page.LoadTask;
            page.OpenEditorCommand.Execute(null);
            page.Text = "unsaved local draft";
            await page.History.ViewRevisionCommand.ExecuteAsync(page.History.Revisions[0]);
            journalPages.Add(page);
        }
        await OnSta(() =>
        {
            _ = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            int pageIndex = 0;
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
                CheckFullPage(journalPages[pageIndex++], resources, theme, width, dpi);
            }
        });
        vm.Deactivate();
        foreach (var page in journalPages) page.Deactivate();
    }

    private static void CheckFullPage(JournalViewModel vm, ResourceDictionary resources, string theme, int width, int dpi)
    {
        var page = new JournalView { DataContext = vm };
        var root = new Border { Resources = resources, Child = page };
        root.SetResourceReference(Border.BackgroundProperty, "PtjBackgroundBrush");
        root.Measure(new Size(width, 720));
        root.Arrange(new Rect(0, 0, width, 720));
        root.UpdateLayout();
        Flush();
        var scroller = (ScrollViewer)page.FindName("JournalScroller");
        var editor = (Border)page.FindName("FreeformEditor");
        var answers = (Border)page.FindName("ReviewEditor");
        var history = (Border)page.FindName("HistorySection");
        var historyView = (JournalHistoryView)page.FindName("ReviewHistory");
        Assert.True(editor.TranslatePoint(new Point(0, editor.ActualHeight), page).Y <= history.TranslatePoint(new Point(), page).Y);
        Assert.True(answers.TranslatePoint(new Point(0, answers.ActualHeight), page).Y <= history.TranslatePoint(new Point(), page).Y);
        var close = (Button)historyView.FindName("CloseRevisionView");
        var closeReview = (Button)historyView.FindName("CloseOpenedReview");
        var openedReview = (StackPanel)historyView.FindName("OpenedReview");
        // This render tree has no native PresentationSource, so IsVisible is false
        // even for its root. Check bound visibility, arranged size and reachability.
        Assert.Equal(Visibility.Visible, openedReview.Visibility);
        Assert.Equal(Visibility.Visible, closeReview.Visibility);
        Assert.InRange(closeReview.ActualHeight, 20, 80);
        Assert.InRange(closeReview.ActualWidth, 80, width);
        Assert.True(closeReview.IsEnabled);
        Assert.True(closeReview.Focusable);
        Assert.True(KeyboardNavigation.GetIsTabStop(closeReview));
        Assert.Same(vm.History!.CloseReviewCommand, closeReview.Command);
        Assert.NotSame(close.Command, closeReview.Command);
        Assert.Contains("Close opened review", AutomationProperties.GetName(closeReview));
        Assert.InRange(closeReview.TranslatePoint(new Point(), root).X, 0, width);
        Assert.InRange(closeReview.TranslatePoint(new Point(closeReview.ActualWidth, 0), root).X, 0, width);
        closeReview.BringIntoView(); Flush(); root.UpdateLayout();
        Assert.InRange(closeReview.TranslatePoint(new Point(), scroller).Y, -0.1, scroller.ViewportHeight + 0.1);
        Assert.InRange(closeReview.TranslatePoint(new Point(0, closeReview.ActualHeight), scroller).Y, -0.1, scroller.ViewportHeight + 0.1);
        Capture(root, theme, width, dpi, "opened-review-actions");
        Assert.All(Descendants(historyView).OfType<TextBlock>().Where(t => t.Text.Contains("UTC", StringComparison.Ordinal)), t =>
        {
            Assert.DoesNotContain("UTC+0", t.Text);
            Assert.Contains("New York", t.Text);
        });
        Assert.True(close.IsEnabled);
        Assert.True(close.Focusable);
        Assert.Same(vm.History!.CloseViewCommand, close.Command);
        // Lists have no independent vertical viewport. Read-only text grows fully,
        // leaving one page scrollbar rather than a clipped nested 150-DIP box.
        Assert.DoesNotContain(Descendants(historyView).OfType<ScrollViewer>(), s => s.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled);
        var last = (TextBox)historyView.FindName("RevisionNextTradingDay");
        Assert.True(last.ActualHeight > 150);
        Assert.True(double.IsPositiveInfinity(last.MaxHeight));
        Assert.Equal(vm.History!.Snapshot!.Review!.NextTradingDay, last.Text);
        Assert.True(KeyboardNavigation.GetIsTabStop(last));
        last.BringIntoView(new Rect(0, last.ActualHeight - 20, last.ActualWidth, 20));
        Flush();
        root.UpdateLayout();
        Assert.InRange(last.TranslatePoint(new Point(0, last.ActualHeight), scroller).Y, 0, scroller.ViewportHeight + 0.1);
        Assert.True(scroller.VerticalOffset > 0);

        scroller.ScrollToHome(); Flush();
        var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = UIElement.PreviewMouseWheelEvent };
        last.RaiseEvent(wheel); Flush();
        double forwardedOffset = scroller.VerticalOffset;
        Assert.True(wheel.Handled);
        Assert.True(forwardedOffset > 0);
        scroller.ScrollToHome(); Flush();
        scroller.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = UIElement.MouseWheelEvent }); Flush();
        Assert.Equal(scroller.VerticalOffset, forwardedOffset); // Exactly one step, same direction.
        double before = scroller.VerticalOffset;
        scroller.PageDown(); Flush();
        Assert.True(scroller.VerticalOffset > before); // Keyboard/page and scrollbar path.
        var input = (TextBox)page.FindName("JournalText");
        input.Text = string.Join("\n", Enumerable.Range(1, 80).Select(i => $"Local draft line {i}"));
        Flush(); root.UpdateLayout();
        var inner = Assert.Single(Descendants(input).OfType<ScrollViewer>());
        Assert.True(inner.ScrollableHeight > 0);
        inner.ScrollToHome(); scroller.ScrollToHome(); Flush();
        var content = (ScrollContentPresenter)inner.Template.FindName("PART_ScrollContentPresenter", inner);
        var withinEditor = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = UIElement.PreviewMouseWheelEvent };
        content.RaiseEvent(withinEditor);
        Assert.False(withinEditor.Handled); // Inner text can still scroll normally.
        Assert.Equal(0, scroller.VerticalOffset);
        inner.ScrollToEnd(); Flush();
        var exhaustedEditor = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = UIElement.PreviewMouseWheelEvent };
        content.RaiseEvent(exhaustedEditor); Flush();
        Assert.True(exhaustedEditor.Handled);
        Assert.True(scroller.VerticalOffset > 0);
        input.Text = "unsaved local draft"; Flush(); root.UpdateLayout();
        scroller.ScrollToEnd(); Flush();
        var atBoundary = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = UIElement.PreviewMouseWheelEvent };
        last.RaiseEvent(atBoundary);
        Assert.False(atBoundary.Handled);
        scroller.ScrollToVerticalOffset(history.TranslatePoint(new Point(), scroller).Y + scroller.VerticalOffset); Flush();
        Capture(root, theme, width, dpi, "page-history");
        last.BringIntoView(new Rect(0, last.ActualHeight - 20, last.ActualWidth, 20)); Flush();
        Capture(root, theme, width, dpi, "page-revision-end");
        var selected = vm.History.SelectedEntry;
        vm.History.CloseViewCommand.Execute(null);
        Flush(); root.UpdateLayout();
        Assert.Null(vm.History.Snapshot);
        Assert.Same(selected, vm.History.SelectedEntry);
        Assert.False(close.IsVisible);
        Assert.Equal(Visibility.Visible, openedReview.Visibility); // Close view leaves the review browser open.
        Assert.True(closeReview.IsEnabled);
        Assert.True(vm.IsEditorOpen);
        Assert.Equal("unsaved local draft", vm.Text);
        var date = vm.SelectedDate;
        var account = vm.SelectedAccount;
        string pageText = vm.History.PageText;
        vm.History.CloseReviewCommand.Execute(null);
        Flush(); root.UpdateLayout();
        Assert.Equal(Visibility.Collapsed, openedReview.Visibility);
        Assert.Equal(0, openedReview.ActualHeight);
        Assert.False(closeReview.IsEnabled);
        Assert.Empty(((ItemsControl)historyView.FindName("HistoryRevisions")).Items);
        Assert.Equal(pageText, vm.History.PageText);
        Assert.Equal(date, vm.SelectedDate);
        Assert.Same(account, vm.SelectedAccount);
        Assert.Equal("unsaved local draft", vm.Text);
        Assert.True(vm.IsEditorOpen);
        root.Child = null;
    }

    private static void Flush() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
    private static void Capture(Border root, string theme, int width, int dpi, string name)
    {
        var bitmap = new RenderTargetBitmap(width * dpi / 96, 720 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(root);
        if (Environment.GetEnvironmentVariable("PTJ_JOURNAL_RENDER_DIRECTORY") is not { Length: > 0 } path) return;
        Directory.CreateDirectory(path);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(Path.Combine(path, $"journal-{name}-{theme}-{width}-{dpi}.png"));
        encoder.Save(output);
    }

    private sealed class EmptyRepository : IDailyJournalRepository
    {
        public Task<DailyJournalDetails?> GetAsync(DateOnly date, Guid? account = null, CancellationToken cancellationToken = default) => Task.FromResult<DailyJournalDetails?>(null);
        public Task<IReadOnlyList<DailyJournalRevision>> GetHistoryAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DailyJournalWriteResult> CreateAsync(CreateDailyJournalCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DailyJournalWriteResult> UpdateAsync(UpdateDailyJournalCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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
