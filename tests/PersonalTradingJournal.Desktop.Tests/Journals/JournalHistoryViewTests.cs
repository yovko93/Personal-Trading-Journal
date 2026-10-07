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
        reader.Add(new(2026, 10, 4), Guid.NewGuid());
        reader.Items[1] = reader.Items[1] with { AccountName = "P 21" };
        reader.Add(new(2026, 10, 3), Guid.NewGuid());
        reader.Items[2] = reader.Items[2] with { AccountName = null, AccountState = DailyJournalAccountState.Unavailable };
        var vm = new JournalHistoryViewModel(reader, _ => true, new HistoryRepositoryProbe(reader),
            new FakeDialogService());
        await vm.ActivateAsync(null);
        await vm.OpenCommand.ExecuteAsync(vm.Entries[0]);
        await vm.ViewRevisionCommand.ExecuteAsync(vm.Revisions[0]);
        var pagedReviews = new List<JournalHistoryViewModel>();
        Guid archivedAccount = Guid.NewGuid();
        var pagedReader = new JournalHistoryTestReader();
        for (int i = 0; i < 21; i++)
        {
            pagedReader.Add(new DateOnly(2026, 10, 5).AddDays(-i), archivedAccount, draft: i % 2 == 0);
            pagedReader.Items[i] = pagedReader.Items[i] with { AccountName = "Archived Account with a long readable historical name" };
        }
        for (int pageNumber = 1; pageNumber <= 3; pageNumber++)
        {
            var paged = new JournalHistoryViewModel(pagedReader, _ => true);
            await paged.ActivateAsync(archivedAccount);
            for (int i = 1; i < pageNumber; i++) await paged.NextCommand.ExecuteAsync(null);
            pagedReviews.Add(paged);
        }
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
            await page.History!.LoadTask; // Editor activation and the independent History read have separate completion.
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
                CheckHistoryTable(view, root, vm, width);
                Assert.Equal(20, ((ItemsControl)Review(view).FindName("HistoryRevisions")).Items.Count);
                Assert.Equal(3, ((ItemsControl)view.FindName("HistoryEntries")).Items.Count);
                var labels = Descendants(view).OfType<TextBlock>().Where(t => t.Name == "EntryAccount").ToArray();
                Assert.Equal(vm.Entries.Select(e => e.ScopeText), labels.Select(t => t.Text));
                Assert.Contains(labels, t => t.Text == "All accounts");
                Assert.Contains(labels, t => t.Text.Contains("P 21") && t.Text.Contains("inactive"));
                Assert.Contains(labels, t => t.Text.Contains("unavailable"));
                Assert.All(labels, t => Assert.Equal(t.Text, t.ToolTip));
                foreach (string name in new[] { "RevisionText", "RevisionWentWell", "RevisionNeedsImprovement", "RevisionNextTradingDay" })
                {
                    var text = (TextBox)Review(view).FindName(name);
                    Assert.True(text.IsReadOnly);
                    Assert.True(text.Focusable);
                    Assert.Equal(BindingMode.OneWay, BindingOperations.GetBinding(text, TextBox.TextProperty)!.Mode);
                    Assert.Contains("read-only", AutomationProperties.GetName(text));
                    Assert.InRange(text.ActualWidth, 100, width);
                    Assert.NotNull(text.Foreground);
                }
                Assert.Equal("  text 23\r\n", ((TextBox)Review(view).FindName("RevisionText")).Text);
                var buttons = Descendants(view).OfType<Button>().Where(b => b.Content?.ToString() is "Open review" or "View revision").ToArray();
                Assert.Equal(23, buttons.Length);
                Assert.All(buttons, b => { Assert.True(b.Focusable); Assert.True(b.IsEnabled); Assert.InRange(b.TranslatePoint(new Point(b.ActualWidth, 0), root).X, 0, width); Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(b))); });
                var open = buttons.First(b => b.Content.ToString() == "Open review");
                var editor = (Button)Review(view).FindName("OpenInEditor");
                var revision = buttons.First(b => b.Content.ToString() == "View revision");
                JournalButtonAssertions.States(editor, "Editor");
                JournalButtonAssertions.States(revision, "Revision");
                var actionColors = new[] { open.Background, editor.Background, revision.Background,
                    resources["PtjAccentBrush"], resources["PtjJournalSaveBrush"], resources["PtjJournalDraftBrush"], resources["PtjDangerBrush"] }
                    .Select(b => ((SolidColorBrush)b).Color).ToArray();
                Assert.Equal(actionColors.Length, actionColors.Distinct().Count());
                var previews = (ItemsControl)Review(view).FindName("CurrentPreviews");
                Assert.Equal(4, previews.Items.Count);
                Assert.True(previews.TranslatePoint(new Point(), root).Y >= editor.TranslatePoint(new Point(0, editor.ActualHeight), root).Y);
                Assert.All(Descendants(previews).OfType<TextBlock>().Where(t => t.Name == "PreviewText"), t =>
                {
                    Assert.True(t.ActualHeight <= 72);
                    Assert.Equal(TextTrimming.CharacterEllipsis, t.TextTrimming);
                    Assert.InRange(t.TranslatePoint(new Point(t.ActualWidth, 0), root).X, 0, width);
                });
                var deletes = Descendants(view).OfType<Button>().Where(b => b.Content?.ToString() == "Delete revision").ToArray();
                Assert.Equal(20, deletes.Length);
                Assert.False(deletes[0].IsEnabled);
                Assert.True(ToolTipService.GetShowOnDisabled(deletes[0]));
                Assert.Contains("protected", deletes[0].ToolTip.ToString());
                Assert.All(deletes.Skip(1), b => Assert.True(b.IsEnabled));
                Assert.All(deletes, b => { Assert.True(b.Focusable); Assert.InRange(b.TranslatePoint(new Point(b.ActualWidth, 0), root).X, 0, width); });
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
                revision.BringIntoView(); Flush(); root.UpdateLayout();
                Capture(root, theme, width, dpi, "history-revision-actions");
                ((ScrollViewer)root.Child).Content = null;
                CheckFullPage(journalPages[pageIndex++], resources, theme, width, dpi);
                foreach (var paged in pagedReviews) CheckPagedTable(paged, resources, theme, width, dpi);
            }
        });
        vm.Deactivate();
        foreach (var page in journalPages) page.Deactivate();
        foreach (var paged in pagedReviews) paged.Deactivate();
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
        var reviewView = Review(historyView);
        var close = (Button)reviewView.FindName("CloseRevisionView");
        var closeReview = (Button)reviewView.FindName("CloseOpenedReview");
        var openedReview = (StackPanel)reviewView.FindName("OpenedReview");
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
        foreach (var closeButton in new[] { close, closeReview })
        {
            Assert.Equal(((SolidColorBrush)resources["PtjDangerBrush"]).Color, ((SolidColorBrush)closeButton.Foreground).Color);
            Assert.Equal(((SolidColorBrush)resources["PtjDangerBrush"]).Color, ((SolidColorBrush)closeButton.BorderBrush).Color);
            Assert.Equal(Colors.Transparent, ((SolidColorBrush)closeButton.Background).Color);
            Assert.NotNull(closeButton.Template.FindName("FocusIndicator", closeButton));
        }
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
        var last = (TextBox)reviewView.FindName("RevisionNextTradingDay");
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
        double openExtent = scroller.ExtentHeight;
        vm.History.CloseReviewCommand.Execute(null);
        Flush(); root.UpdateLayout();
        Assert.Empty(Descendants(historyView).OfType<JournalReviewView>()); // The inline view is removed, not left as a footer.
        Assert.True(scroller.ExtentHeight < openExtent);
        Assert.Empty(vm.History.Revisions);
        Assert.Equal(pageText, vm.History.PageText);
        Assert.Equal(date, vm.SelectedDate);
        Assert.Same(account, vm.SelectedAccount);
        Assert.Equal("unsaved local draft", vm.Text);
        Assert.True(vm.IsEditorOpen);
        root.Child = null;
    }

    private static JournalReviewView Review(JournalHistoryView view) => Assert.Single(Descendants(view).OfType<JournalReviewView>());

    private static void Flush() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

    private static void CheckHistoryTable(JournalHistoryView view, Border root, JournalHistoryViewModel vm, int width)
    {
        Assert.Equal(width < 720, view.IsCompact);
        Assert.Equal("Review History · 3 reviews", vm.Heading);
        var refresh = (Button)view.FindName("RefreshHistory");
        Assert.Same(vm.RefreshCommand, refresh.Command);
        Assert.True(refresh.IsEnabled && refresh.Focusable);
        Assert.Equal(16, refresh.FontSize);
        Assert.Equal(view.TranslatePoint(new Point(view.ActualWidth, 0), root).X,
            refresh.TranslatePoint(new Point(refresh.ActualWidth, 0), root).X, 1); // Excludes the page scrollbar.
        var heading = Assert.Single(Descendants(view).OfType<TextBlock>(), t => t.Text == vm.Heading);
        Assert.Equal(22, heading.FontSize);
        Assert.True(heading.TranslatePoint(new Point(), root).X < refresh.TranslatePoint(new Point(), root).X);
        var entries = (ItemsControl)view.FindName("HistoryEntries");
        var expanded = Review(view);
        var firstRow = (ContentPresenter)entries.ItemContainerGenerator.ContainerFromIndex(0);
        var firstHeader = (Border)firstRow.ContentTemplate.FindName("HistoryRow", firstRow);
        var nextRow = (ContentPresenter)entries.ItemContainerGenerator.ContainerFromIndex(1);
        Assert.True(expanded.TranslatePoint(new Point(), entries).Y >= firstHeader.TranslatePoint(new Point(0, firstHeader.ActualHeight), entries).Y);
        Assert.True(nextRow.TranslatePoint(new Point(), entries).Y >= expanded.TranslatePoint(new Point(0, expanded.ActualHeight), entries).Y);
        Assert.Same(vm, expanded.DataContext);
        var presenter = Assert.IsType<ContentPresenter>(entries.ItemContainerGenerator.ContainerFromIndex(0));
        T Part<T>(string name) where T : FrameworkElement => (T)presenter.ContentTemplate.FindName(name, presenter);
        var row = Part<Border>("HistoryRow");
        var columns = Part<Grid>("EntryColumns");
        var date = Part<TextBlock>("EntryDate");
        var account = Part<TextBlock>("EntryAccount");
        var state = Part<TextBlock>("EntryStatus");
        var revision = Part<TextBlock>("EntryRevision");
        var open = Part<Button>("OpenReview");
        var header = (Grid)view.FindName("HistoryColumns");
        Assert.Equal(width < 720 ? Visibility.Collapsed : Visibility.Visible, header.Visibility);
        Assert.Equal(vm.Entries[0].DisplayDate, date.Text);
        Assert.Equal(FontWeights.SemiBold, date.FontWeight);
        Assert.Equal(16, date.FontSize);
        Assert.Equal(vm.Entries[0].ScopeText, account.Text);
        Assert.Equal(vm.Entries[0].ScopeText, account.ToolTip);
        Assert.Equal("Draft", state.Text);
        Assert.Same(vm.Entries[0], open.CommandParameter);
        Assert.InRange(open.TranslatePoint(new Point(), root).X, 0, width);
        Assert.InRange(open.TranslatePoint(new Point(open.ActualWidth, 0), root).X, 0, width);
        Assert.True(open.Focusable && KeyboardNavigation.GetIsTabStop(open));
        JournalButtonAssertions.States(open, "Review");
        Assert.Equal(new Thickness(0, 0, 0, 1), row.BorderThickness);
        // Exercise the actual theme trigger, not a live pointer claim.
        var hoverKey = (DependencyPropertyKey)typeof(UIElement).GetField("IsMouseOverPropertyKey",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetValue(null)!;
        row.SetValue(hoverKey, true); Flush();
        Assert.Equal(((SolidColorBrush)root.Resources["PtjSurfaceElevatedBrush"]).Color, ((SolidColorBrush)row.Background).Color);
        row.SetValue(hoverKey, false); Flush();
        Assert.Equal(Colors.Transparent, ((SolidColorBrush)row.Background).Color);
        if (width >= 720)
        {
            Assert.Equal(new[] { "Date (New York)", "Account", "Status", "Revision", "Action" }, header.Children.OfType<TextBlock>().Select(t => t.Text));
            FrameworkElement[] values = [date, account, state, revision, open];
            var labels = header.Children.OfType<TextBlock>().ToArray();
            for (int i = 0; i < values.Length; i++)
                Assert.Equal(labels[i].TranslatePoint(new Point(), root).X, values[i].TranslatePoint(new Point(), root).X, 1);
            Assert.True(columns.ColumnDefinitions[1].Width.IsStar);
            Assert.True(account.ActualWidth > 100);
        }
        else
        {
            Assert.True(account.TranslatePoint(new Point(), row).Y > date.TranslatePoint(new Point(), row).Y);
            Assert.True(state.TranslatePoint(new Point(state.ActualWidth, 0), row).X <= revision.TranslatePoint(new Point(), row).X);
            Assert.Equal("Revision 23", revision.Text);
        }
        var footer = (WrapPanel)view.FindName("HistoryPaging");
        Assert.Equal(HorizontalAlignment.Right, footer.HorizontalAlignment);
        Assert.True(footer.TranslatePoint(new Point(), root).Y >= entries.TranslatePoint(new Point(0, entries.ActualHeight), root).Y);
        Assert.Equal(vm.PageText, ((TextBlock)view.FindName("HistoryPageNumber")).Text);
        Assert.Same(vm.PreviousCommand, ((Button)view.FindName("PreviousReviews")).Command);
        Assert.Same(vm.NextCommand, ((Button)view.FindName("NextReviews")).Command);
        Assert.False(((Button)view.FindName("PreviousReviews")).IsEnabled);
        Assert.False(((Button)view.FindName("NextReviews")).IsEnabled);
        Assert.Equal(10, JournalHistoryViewModel.PageSize);
    }

    private static void CheckPagedTable(JournalHistoryViewModel vm, ResourceDictionary resources, string theme, int width, int dpi)
    {
        var view = new JournalHistoryView { DataContext = vm };
        var scroller = new ScrollViewer { Content = view, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var root = new Border { Resources = resources, Child = scroller };
        root.SetResourceReference(Border.BackgroundProperty, "PtjBackgroundBrush");
        root.Measure(new Size(width, 720)); root.Arrange(new Rect(0, 0, width, 720)); root.UpdateLayout(); Flush();
        int page = int.Parse(vm.PageText.Split(' ')[1]);
        var entries = (ItemsControl)view.FindName("HistoryEntries");
        Assert.Equal(page < 3 ? 10 : 1, entries.Items.Count);
        Assert.Equal("Review History · 21 reviews", vm.Heading);
        Assert.Equal(page > 1, ((Button)view.FindName("PreviousReviews")).IsEnabled);
        Assert.Equal(page < 3, ((Button)view.FindName("NextReviews")).IsEnabled);
        var buttons = Descendants(entries).OfType<Button>().ToArray();
        Assert.Equal(entries.Items.Count, buttons.Length);
        for (int i = 0; i < buttons.Length; i++)
        {
            Assert.Same(vm.Entries[i], buttons[i].CommandParameter);
            Assert.InRange(buttons[i].TranslatePoint(new Point(buttons[i].ActualWidth, 0), root).X, 0, width);
        }
        Assert.All(Descendants(entries).OfType<TextBlock>().Where(t => t.Name == "EntryAccount"), t =>
        {
            Assert.Contains("inactive", t.Text);
            Assert.True(t.ActualWidth > 0);
            Assert.True(t.ActualHeight >= 24);
            Assert.Equal(TextWrapping.Wrap, t.TextWrapping);
        });
        var footer = (WrapPanel)view.FindName("HistoryPaging");
        footer.BringIntoView(); Flush(); root.UpdateLayout();
        Assert.InRange(footer.TranslatePoint(new Point(0, footer.ActualHeight), scroller).Y, -0.1, scroller.ViewportHeight + 0.1);
        Capture(root, theme, width, dpi, $"history-page-{page}");
        scroller.Content = null;
    }
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
        public Task<DailyJournalWriteResult> DeleteAsync(DeleteDailyJournalCommand command, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

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
