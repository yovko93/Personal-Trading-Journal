using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Desktop.Views.Calendar;
using PersonalTradingJournal.Desktop.Views.Journals;

namespace PersonalTradingJournal.Desktop.Tests.Journals;

public sealed class InlineJournalViewTests
{
    [Fact]
    public async Task InlineFormAndCompactReviewKeepTradesReachableAcrossThemesAndSizes()
    {
        if (Environment.GetEnvironmentVariable("PTJ_INLINE_JOURNAL_TEST_HOST") != "1")
        {
            await IsolatedTestProcess.RunSuiteAsync(typeof(InlineJournalViewTests), "inline-journal", "PTJ_INLINE_JOURNAL_TEST_HOST",
                TimeSpan.FromMinutes(2), caseHangTimeout: TimeSpan.FromSeconds(30));
            return;
        }
        var cases = new[] { ("Light", 1100, 96), ("Dark", 1100, 96), ("Light", 480, 240), ("Dark", 480, 240) };
        var models = new List<CalendarViewModel>();
        var reviews = new List<CalendarViewModel>();
        foreach (var _ in cases)
        {
            var repo = new FakeDailyJournalRepository();
            var vm = await CalendarSummaryFixture.CreateAsync(journalStatusReader: repo, journalRepository: repo,
                journalDialogs: new FakeDialogService { ConfirmationResult = true });
            var date = vm.Weeks[0].Days[5].Date;
            Guid p21 = Guid.NewGuid(), other = Guid.NewGuid();
            repo.AccountNames[p21] = "P 21";
            repo.AccountNames[other] = "Another Account with a longer readable name";
            await repo.CreateAsync(new(date, null, "Saved global Journal", false));
            await repo.CreateAsync(new(date, p21, "Saved account Journal\n" + new string('x', 500)));
            await repo.CreateAsync(new(date, other, "Another completed account Journal", false));
            await vm.SelectDayCommand.ExecuteAsync(vm.Weeks[0].Days[5]);
            await vm.AddDayJournalCommand.ExecuteAsync(null);
            vm.InlineJournal!.Text = "Exact local text\n" + new string('x', 200);
            models.Add(vm);
            var review = await CalendarSummaryFixture.CreateAsync(journalStatusReader: repo, journalRepository: repo,
                journalDialogs: new FakeDialogService { ConfirmationResult = true });
            await review.SelectDayCommand.ExecuteAsync(review.Weeks[0].Days[5]);
            await review.OpenDayJournalCommand.ExecuteAsync(review.DayJournals.Single(r => r.AccountId is null));
            reviews.Add(review);
        }
        await CalendarStaTest.RunAsync(() =>
        {
            _ = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            for (int i = 0; i < cases.Length; i++)
            {
                var (theme, width, dpi) = cases[i];
                var vm = models[i];
                System.Windows.Application.Current.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
                var view = new CalendarDayDetailsView { DataContext = vm };
                view.Measure(new Size(width, 720)); view.Arrange(new Rect(0, 0, width, 720)); view.UpdateLayout(); Flush();
                var host = (ContentControl)view.FindName("DayJournalEditor");
                var inline = Assert.IsType<InlineJournalView>(FindInlineView(host));
                var section = (StackPanel)view.FindName("InlineJournalSection");
                var table = (ScrollViewer)view.FindName("DayTradesScroller");
                var page = (ScrollViewer)view.FindName("DayContentScroller");
                var entries = (ItemsControl)view.FindName("DayJournalEntries");
                Assert.Equal(3, entries.Items.Count);
                Assert.True(entries.TranslatePoint(new Point(0, entries.ActualHeight), view).Y <= section.TranslatePoint(new Point(), view).Y);
                var openButtons = Descendants(entries).OfType<Button>().ToArray();
                Assert.Equal(3, openButtons.Length);
                foreach (var button in openButtons)
                {
                    Assert.Same(vm.OpenDayJournalCommand, button.Command);
                    var row = Assert.IsType<CalendarJournalEntry>(button.CommandParameter);
                    Assert.Contains(row, vm.DayJournals);
                    Assert.Equal(row.Description, System.Windows.Automation.AutomationProperties.GetName(button));
                    Assert.True(button.Focusable && button.IsEnabled);
                    Assert.InRange(button.TranslatePoint(new Point(button.ActualWidth, 0), entries).X, 1, entries.ActualWidth);
                }
                Assert.Contains(Descendants(entries).OfType<TextBlock>(), t => t.Text == "All accounts");
                Assert.Contains(Descendants(entries).OfType<TextBlock>(), t => t.Text == "P 21");
                Assert.Contains(Descendants(entries).OfType<TextBlock>(), t => t.Text == "Draft");
                Assert.Contains(Descendants(entries).OfType<TextBlock>(), t => t.Text == "Completed");
                entries.BringIntoView(new Rect(0, 0, entries.ActualWidth, 300)); Flush();
                Render(view, theme, width, dpi, "journals");
                var form = (StackPanel)inline.FindName("InlineForm");
                Assert.Equal(Visibility.Visible, section.Visibility);
                Assert.True(section.TranslatePoint(new Point(0, section.ActualHeight), view).Y <= table.TranslatePoint(new Point(), view).Y);
                Assert.Same(vm.InlineJournal, inline.DataContext);
                var formAccount = (ComboBox)inline.FindName("InlineJournalAccount");
                Assert.Same(vm.InlineJournal!.EditorAccount, formAccount.SelectedItem);
                Assert.True(formAccount.Focusable && formAccount.IsEnabled);
                Assert.InRange(formAccount.ActualWidth, 150, width);
                Assert.True(formAccount.TranslatePoint(new Point(0, formAccount.ActualHeight), inline).Y < ((TextBox)inline.FindName("InlineText")).TranslatePoint(new Point(), inline).Y);
                Assert.Same(vm.InlineJournal!.SaveCommand, ((Button)inline.FindName("SaveInlineJournal")).Command);
                Assert.Same(vm.InlineJournal.SaveDraftAndCloseCommand, ((Button)inline.FindName("CancelInlineJournal")).Command);
                Assert.Single(Descendants(inline).OfType<Button>(), b => ReferenceEquals(b.Command, vm.InlineJournal.SaveCommand));
                Assert.Single(Descendants(inline).OfType<Button>(), b => ReferenceEquals(b.Command, vm.InlineJournal.SaveDraftAndCloseCommand));
                Assert.DoesNotContain(Descendants(inline).OfType<Button>(), b => ReferenceEquals(b.Command, vm.InlineJournal.CompleteReviewCommand));
                foreach (var name in new[] { "InlineText", "InlineWell", "InlineImprove", "InlineNext" })
                {
                    var field = (TextBox)inline.FindName(name);
                    Assert.True(field.Focusable && !field.IsReadOnly);
                    Assert.False(field.AcceptsTab);
                    Assert.InRange(field.ActualWidth, 150, width);
                    Assert.Equal(ScrollBarVisibility.Auto, field.VerticalScrollBarVisibility);
                }
                form.BringIntoView(new Rect(0, 0, form.ActualWidth, 240)); Flush();
                var save = (Button)inline.FindName("SaveInlineJournal");
                var cancel = (Button)inline.FindName("CancelInlineJournal");
                Assert.Equal("Save Journal as Completed", System.Windows.Automation.AutomationProperties.GetName(save));
                Assert.Contains("save as draft and close", System.Windows.Automation.AutomationProperties.GetName(cancel));
                JournalButtonAssertions.States(save, "Save");
                JournalButtonAssertions.States(cancel, "Draft");
                foreach (var cta in Descendants(inline).OfType<Button>().Where(b => b.Content?.ToString() is "Add Journal" or "Continue Journal"))
                    JournalButtonAssertions.CallToAction(cta);
                var lastAnswer = (TextBox)inline.FindName("InlineNext");
                Assert.True(save.TranslatePoint(new Point(), inline).Y >= lastAnswer.TranslatePoint(new Point(0, lastAnswer.ActualHeight), inline).Y);
                Assert.True(save.Focusable && cancel.Focusable);
                Render(view, theme, width, dpi, "expanded");
                var fieldText = (TextBox)inline.FindName("InlineText");
                var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = UIElement.PreviewMouseWheelEvent };
                double before = page.VerticalOffset;
                fieldText.RaiseEvent(wheel); Flush();
                Assert.True(wheel.Handled);
                Assert.True(page.VerticalOffset > before);
                ((WrapPanel)inline.FindName("InlineEditorActions")).BringIntoView(); Flush();
                Assert.InRange(save.TranslatePoint(new Point(), page).Y, -0.1, page.ViewportHeight - save.ActualHeight + 0.1);
                Assert.InRange(cancel.TranslatePoint(new Point(), page).Y, -0.1, page.ViewportHeight - cancel.ActualHeight + 0.1);
                Render(view, theme, width, dpi, "actions");
                string keptText = vm.InlineJournal.Text;
                vm.InlineJournal.Text = "";
                vm.InlineJournal.WentWell = "Answer alone cannot complete";
                save.Command.Execute(null); Flush(); view.UpdateLayout();
                var error = (TextBlock)inline.FindName("JournalTextError");
                Assert.Contains("Journal text is required", error.Text);
                Assert.True(error.TranslatePoint(new Point(), inline).Y >= fieldText.TranslatePoint(new Point(0, fieldText.ActualHeight), inline).Y);
                Assert.True(vm.InlineJournal.IsEditorOpen);
                vm.InlineJournal.Text = keptText; Flush();
                Assert.Empty(error.Text);
                table.BringIntoView(new Rect(0, 0, table.ActualWidth, 80)); Flush();
                Assert.InRange(table.TranslatePoint(new Point(), page).Y, -1, page.ViewportHeight);
                vm.InlineJournal.CloseEditorCommand.Execute(null); Flush();
                Assert.False(vm.InlineJournal.IsEditorOpen);
                Assert.Equal(Visibility.Collapsed, form.Visibility);
                section.BringIntoView(); Flush(); Render(view, theme, width, dpi, "collapsed");
                vm.CloseInlineJournalCommand.Execute(null); Flush();
                Assert.Equal(Visibility.Collapsed, section.Visibility);
                Assert.NotNull(vm.DayDetails);
                vm.Deactivate();
                var reviewVm = reviews[i];
                var reviewView = new CalendarDayDetailsView { DataContext = reviewVm };
                reviewView.Measure(new Size(width, 720)); reviewView.Arrange(new Rect(0, 0, width, 720)); reviewView.UpdateLayout(); Flush();
                var list = (ItemsControl)reviewView.FindName("DayJournalEntries");
                var detailHosts = Descendants(list).OfType<ContentControl>().Where(c => c.Name == "RowJournalDetail").ToArray();
                Assert.Equal(3, detailHosts.Length);
                var expanded = Assert.Single(detailHosts, c => c.Content is not null);
                Assert.Same(reviewVm.InlineJournal, expanded.Content);
                Assert.Equal(Visibility.Collapsed, ((StackPanel)reviewView.FindName("InlineJournalSection")).Visibility);
                var containers = Enumerable.Range(0, list.Items.Count).Select(n => (FrameworkElement)list.ItemContainerGenerator.ContainerFromIndex(n)).ToArray();
                Assert.True(expanded.TranslatePoint(new Point(0, expanded.ActualHeight), list).Y <= containers[1].TranslatePoint(new Point(), list).Y);
                var close = Assert.Single(Descendants(expanded).OfType<Button>(), b => Equals(b.Content, "Close Journal"));
                Assert.Same(reviewVm.CloseInlineJournalCommand, close.Command);
                Assert.True(close.Focusable && close.IsEnabled);
                Assert.DoesNotContain(Descendants(reviewView).OfType<Button>(), b => b.Content?.ToString()?.Contains("Reload") == true);
                var refresh = Assert.Single(Descendants(reviewView).OfType<Button>(), b => Equals(b.Content, "Refresh Journals"));
                Assert.Same(reviewVm.RefreshDayJournalsCommand, refresh.Command);
                expanded.BringIntoView(new Rect(0, 0, expanded.ActualWidth, 260)); Flush();
                Render(reviewView, theme, width, dpi, "in-place-review");
                var lastAction = Descendants(expanded).OfType<Button>().Last(b => b.Visibility == Visibility.Visible);
                lastAction.BringIntoView(); Flush();
                var detailScroller = (ScrollViewer)reviewView.FindName("DayContentScroller");
                Assert.InRange(lastAction.TranslatePoint(new Point(0, lastAction.ActualHeight), detailScroller).Y, 0, detailScroller.ViewportHeight + 1);
                Render(reviewView, theme, width, dpi, "in-place-review-bottom");
                close.BringIntoView(); Flush();
                var reviewScroller = (ScrollViewer)reviewView.FindName("DayContentScroller");
                Assert.InRange(close.TranslatePoint(new Point(), reviewScroller).Y, -1, reviewScroller.ViewportHeight);
                close.Command.Execute(null); Flush();
                Assert.All(detailHosts, c => Assert.Null(c.Content));
                Assert.Equal(3, list.Items.Count);
                reviewVm.Deactivate();
            }
        }, "Inline Journal themes, layout and scrolling");
    }

    private static void Flush() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject node)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    private static InlineJournalView? FindInlineView(DependencyObject node)
    {
        if (node is InlineJournalView inline) return inline;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            if (FindInlineView(child) is { } result) return result;
        }
        return null;
    }
    private static void Render(Visual view, string theme, int width, int dpi, string state)
    {
        var bitmap = new RenderTargetBitmap(width * dpi / 96, 720 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(view);
        if (Environment.GetEnvironmentVariable("PTJ_JOURNAL_RENDER_DIRECTORY") is not { Length: > 0 } path) return;
        Directory.CreateDirectory(path);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(Path.Combine(path, $"inline-journal-{theme}-{width}-{dpi}-{state}.png"));
        encoder.Save(output);
    }
}
