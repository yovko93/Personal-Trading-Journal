using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Journals;
using PersonalTradingJournal.Desktop.Views.Journals;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Journals;

namespace PersonalTradingJournal.Desktop.Tests.Journals;

public sealed class JournalViewTests
{
    [Fact]
    public async Task CompiledEditorBindingsScopeVetoAndLightDarkNormalHighDpiLayouts()
    {
        const string text = "  Plan\r\n\nТърпение 📈\t  ";
        var cases = new[] { ("Light", 960, 96), ("Dark", 960, 96), ("Light", 480, 240), ("Dark", 480, 240) };
        var editors = new List<JournalViewModel>();
        foreach (var _ in cases) editors.Add(await CreateAsync(text));
        var dialogs = new FakeDialogService();
        var dateEditors = new[] { await CreateAsync("saved", dialogs), await CreateAsync("saved", dialogs) };
        var empty = await CreateAsync(null);
        string longText = string.Join("\n", Enumerable.Range(1, 45).Select(i => $"Journal line {i}: full saved content remains readable without ellipsis."));
        var longNotes = new List<JournalViewModel>();
        foreach (var _ in cases) longNotes.Add(await CreateAsync(longText));
        var reviews = new List<(JournalViewModel Draft, JournalViewModel Completed)>();
        foreach (var _ in cases)
        {
            reviews.Add((await CreateAsync("", review: new("I followed the plan.", "", "")),
                await CreateAsync("", draft: false, review: new("  I waited for my entry.\r\n", "I chased one Trade.", "I will wait for confirmation."))));
        }
        await OnSta(() =>
        {
            // One Application and STA for this supervised child, matching production BAML's
            // application-level StaticResource lookup without polluting the main test host.
            _ = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            for (int i = 0; i < cases.Length; i++)
            {
                var (theme, width, dpi) = cases[i];
                CheckLayout(editors[i], text, theme, width, dpi);
                CheckReviewLayout(reviews[i].Draft, theme, width, dpi, completed: false);
                CheckReviewLayout(reviews[i].Completed, theme, width, dpi, completed: true);
                CheckLongSavedNotes(longNotes[i], longText, theme, width, dpi);
            }
            CheckDateInput(dateEditors[0], dialogs, "en-US");
            CheckDateInput(dateEditors[1], dialogs, "bg-BG");
            CheckEmptyAndValidation(empty);
        });
    }

    private static void CheckReviewLayout(JournalViewModel vm, string theme, int width, int dpi, bool completed)
    {
        using var phase = CalendarStaTest.Phase($"Daily Review {theme} {width} DIP / {dpi} DPI completed={completed}");
        Assert.False(vm.IsEditorOpen);
        if (!completed) vm.OpenEditorCommand.Execute(null);
        var (view, root) = Layout(vm, theme, width);
        Assert.Equal(completed ? "Completed" : "Draft", ((TextBlock)view.FindName("EntryState")).Text);
        Assert.Equal(vm.SelectedDate!.Value.ToString("dd MMM yyyy", CultureInfo.CurrentCulture), ((TextBlock)view.FindName("SelectedJournalDate")).Text);
        var wentWell = (TextBox)view.FindName("WentWellAnswer");
        var improvement = (TextBox)view.FindName("NeedsImprovementAnswer");
        var nextDay = (TextBox)view.FindName("NextTradingDayAnswer");
        var freeform = (TextBox)view.FindName("JournalText");
        var save = (Button)view.FindName("SaveJournal");
        var cancel = (Button)view.FindName("CloseJournalEditor");
        var reopen = (Button)view.FindName("ReopenJournalReview");
        Assert.Equal(new[] { vm.WentWell, vm.NeedsImprovement, vm.NextTradingDay }, new[] { wentWell.Text, improvement.Text, nextDay.Text });
        Assert.Equal(new[] { "What went well?", "What needs improvement?", "What will I do differently next trading day?" },
            new[] { wentWell, improvement, nextDay }.Select(AutomationProperties.GetName));
        Assert.All(new[] { freeform, wentWell, improvement, nextDay }, box =>
        {
            Assert.True(box.IsEnabled && box.Focusable); // Completed text can still be read and copied.
            Assert.Equal(completed, box.IsReadOnly);
            Assert.True(box.AcceptsReturn);
            Assert.False(box.AcceptsTab);
            Assert.Equal(0, box.MaxLength);
            Assert.Equal(ScrollBarVisibility.Auto, box.VerticalScrollBarVisibility);
            Assert.True(box.ActualWidth <= width);
            Assert.Equal(((SolidColorBrush)root.Resources["PtjSurfaceElevatedBrush"]).Color, ((SolidColorBrush)box.Background).Color);
        });
        Assert.Null(view.FindName("CompleteJournalReview"));
        Assert.Null(view.FindName("EditorSaveJournal"));
        Assert.Null(view.FindName("EditorCancel"));
        Assert.Single(Descendants(view).OfType<Button>(), b => ReferenceEquals(b.Command, vm.SaveCommand));
        Assert.Single(Descendants(view).OfType<Button>(), b => ReferenceEquals(b.Command, vm.SaveDraftAndCloseCommand));
        Assert.DoesNotContain(Descendants(view).OfType<Button>(), b => ReferenceEquals(b.Command, vm.CompleteReviewCommand));
        Assert.Same(vm.SaveDraftAndCloseCommand, cancel.Command);
        Assert.Contains("save as draft and close", AutomationProperties.GetName(cancel));
        if (!completed)
        {
            JournalButtonAssertions.States(save, "Save");
            JournalButtonAssertions.States(cancel, "Draft");
            JournalButtonAssertions.CallToAction((Button)view.FindName("AddJournal"));
            JournalButtonAssertions.CallToAction((Button)view.FindName("ContinueJournal"));
        }
        Assert.Equal("Save Journal as Completed", AutomationProperties.GetName(save));
        Assert.Same(vm.ReopenReviewCommand, reopen.Command);
        Assert.Equal(completed ? Visibility.Collapsed : Visibility.Visible, save.Visibility);
        Assert.Equal(completed ? Visibility.Collapsed : Visibility.Visible, ((WrapPanel)view.FindName("JournalEditorActions")).Visibility);
        Assert.Equal(completed ? Visibility.Visible : Visibility.Collapsed, reopen.Visibility);
        Assert.True(completed ? reopen.IsEnabled : save.IsEnabled);
        Assert.Equal(completed ? Visibility.Collapsed : Visibility.Visible, ((Border)view.FindName("ReviewEditor")).Visibility);
        Assert.Equal(completed ? Visibility.Visible : Visibility.Collapsed, ((Border)view.FindName("CompactJournal")).Visibility);
        Assert.Equal(completed, vm.ShowCompactReview);
        if (completed)
        {
            Assert.False(((Button)view.FindName("AddJournal")).IsEnabled);
            Assert.Contains(Descendants((Border)view.FindName("CompactJournal")).OfType<TextBlock>(), t => t.Text == vm.SavedReview.WentWell);
        }
        Assert.Same(vm.TradeContext, Assert.Single(Descendants(view).OfType<JournalTradeContextView>()).DataContext);
        if (!completed)
        {
            Assert.Same(vm.SaveCommand, save.Command);
            Assert.True(save.Focusable && cancel.Focusable);
            Assert.True(save.TranslatePoint(new Point(), view).Y >= nextDay.TranslatePoint(new Point(0, nextDay.ActualHeight), view).Y);
            Assert.InRange(cancel.TranslatePoint(new Point(cancel.ActualWidth, 0), view).X, 0, width);
            Assert.True(wentWell.TranslatePoint(new Point(), view).Y < improvement.TranslatePoint(new Point(), view).Y);
            Assert.True(improvement.TranslatePoint(new Point(), view).Y < nextDay.TranslatePoint(new Point(), view).Y);
            Assert.Equal(120, freeform.ActualHeight);
            Assert.All(new[] { wentWell, improvement, nextDay }, box => Assert.Equal(72, box.ActualHeight));
            improvement.Text = "  More patience.\r\n ";
            nextDay.Text = "I will wait.";
            Flush();
            Assert.Equal(improvement.Text, vm.NeedsImprovement);
            Assert.Equal(nextDay.Text, vm.NextTradingDay);
            Assert.True(vm.IsDirty);
            Assert.False(vm.CanComplete); // Answers alone cannot complete; unsaved protection still covers them.
            DateTime? date = vm.SelectedDate;
            ((DatePicker)view.FindName("JournalDate")).SelectedDate = date!.Value.AddDays(1);
            Flush();
            Assert.Equal(date, vm.SelectedDate); // Answers alone participate in the discard guard.
            Assert.Equal(improvement.Text, vm.NeedsImprovement);
        }
        var scroller = (ScrollViewer)view.FindName("JournalScroller");
        if (!completed) scroller.ScrollToVerticalOffset(wentWell.TranslatePoint(new Point(), view).Y - 48);
        Flush();
        Render(root, theme, width, dpi, completed ? "review-completed-" : "review-draft-");
        (completed ? reopen : cancel).BringIntoView();
        Flush();
        // WPF layout can differ by subpixel floating-point error at the viewport edge.
        Assert.InRange((completed ? reopen : cancel).TranslatePoint(new Point(), root).Y, -0.1, root.ActualHeight - (completed ? reopen : cancel).ActualHeight + 0.1);
        if (!completed)
        {
            Assert.InRange(save.TranslatePoint(new Point(), root).Y, -0.1, root.ActualHeight - save.ActualHeight + 0.1);
            Render(root, theme, width, dpi, "editor-actions-");
        }
        else
        {
            vm.ReopenReviewCommand.Execute(null); Flush();
            Assert.True(vm.IsCompleted && vm.CanEdit);
            Assert.Equal(Visibility.Visible, save.Visibility);
            Assert.Equal(Visibility.Visible, ((WrapPanel)view.FindName("JournalEditorActions")).Visibility);
            Assert.True(save.IsEnabled && cancel.IsEnabled && !freeform.IsReadOnly);
            Assert.Contains("unchanged Completed", AutomationProperties.GetHelpText(cancel));
            cancel.Command.Execute(null); Flush();
            Assert.True(vm.IsCompleted && vm.IsReadOnly);
            Assert.Equal(Visibility.Collapsed, ((WrapPanel)view.FindName("JournalEditorActions")).Visibility);
        }
        vm.Deactivate();
    }

    private static void CheckLayout(JournalViewModel vm, string text, string theme, int width, int dpi)
    {
        using (CalendarStaTest.Phase("Journal construction and layout"))
        {
            var (view, root) = Layout(vm, theme, width);
            Assert.False(vm.IsEditorOpen);
            Assert.Equal(Visibility.Collapsed, ((Border)view.FindName("HistorySection")).Visibility);
            Assert.Equal(Visibility.Collapsed, ((Border)view.FindName("FreeformEditor")).Visibility);
            Assert.Equal(Visibility.Visible, ((Border)view.FindName("CompactJournal")).Visibility);
            var add = (Button)view.FindName("AddJournal");
            Assert.True(add.IsEnabled && add.Focusable && add.IsVisible == view.IsVisible);
            Assert.Same(vm.OpenEditorCommand, add.Command);
            Assert.Equal("Draft", ((TextBlock)view.FindName("EntryState")).Text);
            var dateHeading = (TextBlock)view.FindName("SelectedJournalDate");
            Assert.Equal(vm.SelectedDate!.Value.ToString("dd MMM yyyy", CultureInfo.CurrentCulture), dateHeading.Text);
            Assert.Equal(22, dateHeading.FontSize);
            Assert.Equal(16, ((TextBlock)view.FindName("EntryState")).FontSize);
            Assert.False(((Expander)view.FindName("ScopeHelp")).IsExpanded);
            Assert.Equal(Visibility.Collapsed, ((TextBlock)view.FindName("JournalError")).Visibility);
            var notes = Descendants((StackPanel)view.FindName("SavedJournalContent")).OfType<TextBlock>().Where(t => t.Text == vm.SavedText).ToArray();
            Assert.NotEmpty(notes);
            Assert.All(notes, note =>
            {
                Assert.Equal(16, note.FontSize);
                Assert.Equal(24, note.LineHeight);
                Assert.Equal(760, note.MaxWidth);
                Assert.True(double.IsPositiveInfinity(note.MaxHeight));
                Assert.Equal(TextTrimming.None, note.TextTrimming);
                Assert.Equal(((SolidColorBrush)root.Resources["PtjTextPrimaryBrush"]).Color, ((SolidColorBrush)note.Foreground).Color);
            });
            Assert.InRange(add.TranslatePoint(new Point(), root).X + add.ActualWidth, 1, width);
            Render(root, theme, width, dpi, "compact-");
            add.Command.Execute(null);
            Flush(); root.UpdateLayout();
            Assert.Equal(Visibility.Visible, ((Border)view.FindName("FreeformEditor")).Visibility);
            Assert.Equal(Visibility.Collapsed, ((Border)view.FindName("CompactJournal")).Visibility);
            var date = (DatePicker)view.FindName("JournalDate");
            var account = (ComboBox)view.FindName("JournalAccount");
            var editor = (TextBox)view.FindName("JournalText");
            var save = (Button)view.FindName("SaveJournal");
            var reload = (Button)view.FindName("ReloadJournal");
            Assert.Same(vm.TradeContext, Assert.Single(Descendants(view).OfType<JournalTradeContextView>()).DataContext);
            Assert.Equal(text, editor.Text);
            Assert.Equal(16, editor.FontSize);
            Assert.False(vm.IsDirty); // Rendering must not normalize persisted line endings.
            Assert.False(vm.HasDateInputError);
            Assert.True(editor.AcceptsReturn);
            Assert.False(editor.AcceptsTab); // Tab remains keyboard navigation, not a trap.
            Assert.Equal(0, editor.MaxLength); // Oversize input is validated, never silently truncated.
            Assert.True(editor.Focusable && date.Focusable && account.Focusable && save.Focusable && reload.Focusable);
            Assert.Equal("Daily journal text", AutomationProperties.GetName(editor));
            Assert.Equal("Journal date in New York", AutomationProperties.GetName(date));
            Assert.Equal("Journal Account scope", AutomationProperties.GetName(account));
            Assert.Same(vm.SaveCommand, save.Command);
            Assert.Same(vm.ReloadCommand, reload.Command);
            Assert.Contains(view.InputBindings.OfType<KeyBinding>(), b => b.Key == Key.S && b.Modifiers == ModifierKeys.Control);
            Assert.Equal(ScrollBarVisibility.Auto, editor.VerticalScrollBarVisibility);
            Assert.Equal(ScrollBarVisibility.Disabled, ((ScrollViewer)view.FindName("JournalScroller")).HorizontalScrollBarVisibility);
            var datePosition = date.TranslatePoint(new Point(), root);
            var accountPosition = account.TranslatePoint(new Point(), root);
            Assert.True(accountPosition.X + account.ActualWidth <= width);
            Assert.True(datePosition.X + date.ActualWidth <= width);
            if (width < 600) Assert.True(accountPosition.Y > datePosition.Y);
            else Assert.Equal(datePosition.Y, accountPosition.Y, 1);
            Assert.InRange(editor.ActualWidth, 250, width);
            Assert.Equal(((SolidColorBrush)root.Resources["PtjSurfaceElevatedBrush"]).Color,
                ((SolidColorBrush)editor.Background).Color);
            editor.Text = text + "\nExact appended line  ";
            Flush();
            Assert.Equal(editor.Text, vm.Text);
            Assert.True(vm.IsDirty);
            Assert.True(save.IsEnabled);
            Assert.Equal("Unsaved changes", ((TextBlock)view.FindName("JournalStatus")).Text);
            Render(root, theme, width, dpi);
            var scroller = (ScrollViewer)view.FindName("JournalScroller");
            scroller.ScrollToEnd();
            Flush();
            root.UpdateLayout();
            Assert.InRange(save.TranslatePoint(new Point(), root).Y, 0, root.ActualHeight - save.ActualHeight);

            // Exercise the actual DatePicker-owned calendar template without opening a
            // native popup. Live popup/focus acceptance is deliberately reported separately.
            var popup = (Popup)date.Template.FindName("PART_Popup", date);
            var calendar = Assert.IsType<System.Windows.Controls.Calendar>(popup.Child);
            calendar.Measure(new Size(300, 320));
            calendar.Arrange(new Rect(0, 0, 300, 320));
            calendar.UpdateLayout();
            Assert.Same(date.CalendarStyle, calendar.Style);
            var selectedDay = Assert.Single(Descendants(calendar).OfType<CalendarDayButton>(), b => b.IsSelected);
            Assert.Equal(Desktop.Views.Dashboard.RangeDay.Single, selectedDay.Tag);
            Assert.True(selectedDay.Focusable);
            Assert.Equal(vm.SelectedDate, calendar.SelectedDate);
            vm.Deactivate();
        }
    }

    private static void CheckDateInput(JournalViewModel vm, FakeDialogService dialogs, string culture)
    {
        vm.OpenEditorCommand.Execute(null);
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        var (view, _) = Layout(vm, "Light", 960);
        var date = (DatePicker)view.FindName("JournalDate");
        var account = (ComboBox)view.FindName("JournalAccount");
        var input = (DatePickerTextBox)date.Template.FindName("PART_TextBox", date);
        DateTime applied = vm.SelectedDate!.Value;
        vm.Text = "local draft";
        input.Text = "not a date";
        Flush();
        Assert.True(vm.HasDateInputError);
        Assert.False(vm.SaveCommand.CanExecute(null));
        Assert.Equal(applied, vm.SelectedDate);
        input.Text = applied.AddDays(1).ToString("d", CultureInfo.CurrentCulture);
        Flush();
        Assert.True(vm.HasDateInputError); // Valid but not yet applied must not save under yesterday.
        input.Text = applied.ToString("d", CultureInfo.CurrentCulture);
        Flush();
        Assert.False(vm.HasDateInputError);
        date.SelectedDate = applied.AddDays(1); // Keep editing rejects the scope change.
        Flush();
        Assert.Equal(applied, vm.SelectedDate);
        Assert.Equal(applied, date.SelectedDate);
        Assert.Equal(applied, DateTime.Parse(input.Text, CultureInfo.CurrentCulture));
        Assert.False(vm.HasDateInputError);
        account.SelectedItem = vm.Accounts.Single(a => a.Id.HasValue);
        Flush();
        Assert.Null(vm.SelectedAccount.Id);
        Assert.Same(vm.SelectedAccount, account.SelectedItem);
        Assert.Equal("local draft", vm.Text);
        Assert.True(vm.SaveCommand.CanExecute(null));
        Assert.NotNull(dialogs.ConfirmationRequest);
        vm.Deactivate();
    }

    private static void CheckEmptyAndValidation(JournalViewModel vm)
    {
        Assert.True(vm.ShowEmptyReview);
        foreach (var (theme, width, dpi) in new[] { ("Light", 960, 96), ("Dark", 960, 96), ("Light", 480, 240), ("Dark", 480, 240) })
        {
            var (emptyView, emptyRoot) = Layout(vm, theme, width);
            Assert.Equal("No entry", ((TextBlock)emptyView.FindName("EntryState")).Text);
            Assert.Equal(Visibility.Visible, ((Border)emptyView.FindName("CompactJournal")).Visibility);
            var add = (Button)emptyView.FindName("AddJournal");
            Assert.True(add.IsEnabled && add.Focusable);
            Assert.InRange(add.TranslatePoint(new Point(add.ActualWidth, 0), emptyRoot).X, 0, width);
            Render(emptyRoot, theme, width, dpi, "no-entry-");
        }
        vm.OpenEditorCommand.Execute(null);
        var (view, _) = Layout(vm, "Dark", 480);
        var editor = (TextBox)view.FindName("JournalText");
        Assert.Equal("No entry", ((TextBlock)view.FindName("EntryState")).Text);
        Assert.Same(vm.OpenEditorCommand, ((Button)view.FindName("AddJournal")).Command);
        Assert.Equal("New draft — not saved", ((TextBlock)view.FindName("JournalStatus")).Text);
        Assert.Empty(editor.Text);
        var save = (Button)view.FindName("SaveJournal");
        Assert.True(save.IsEnabled); // An explicit attempt explains the minimum-content validation.
        save.Command.Execute(null);
        Flush();
        Assert.Contains("Journal text is required", ((TextBlock)view.FindName("JournalTextError")).Text);
        Assert.True(((TextBlock)view.FindName("JournalTextError")).TranslatePoint(new Point(), view).Y >= editor.TranslatePoint(new Point(0, editor.ActualHeight), view).Y);
        Assert.False(vm.IsExisting);
        Assert.True(vm.IsEditorOpen);
        editor.Text = new string('x', DailyJournalEntry.MaximumTextLength + 1);
        Flush();
        Assert.Equal(DailyJournalEntry.MaximumTextLength + 1, vm.Text.Length);
        Assert.False(((Button)view.FindName("SaveJournal")).IsEnabled);
        Assert.Contains("kept", ((TextBlock)view.FindName("JournalError")).Text);
        Assert.Equal(Visibility.Visible, ((TextBlock)view.FindName("JournalError")).Visibility);
        Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting((TextBlock)view.FindName("JournalError")));
        vm.Deactivate();
    }

    private static void CheckLongSavedNotes(JournalViewModel vm, string text, string theme, int width, int dpi)
    {
        var (view, root) = Layout(vm, theme, width);
        var note = Assert.Single(Descendants((StackPanel)view.FindName("SavedJournalContent")).OfType<TextBlock>(), t => t.Text == text);
        Assert.True(note.ActualHeight > 800);
        Assert.InRange(note.ActualWidth, 100, Math.Min(760, width));
        Assert.Equal(TextTrimming.None, note.TextTrimming);
        var scroller = (ScrollViewer)view.FindName("JournalScroller");
        note.BringIntoView(new Rect(0, note.ActualHeight - 24, note.ActualWidth, 24)); Flush(); root.UpdateLayout();
        Assert.InRange(note.TranslatePoint(new Point(0, note.ActualHeight), scroller).Y, -0.1, scroller.ViewportHeight + 0.1);
        Render(root, theme, width, dpi, "long-note-end-");
        vm.Deactivate();
    }

    private static async Task<JournalViewModel> CreateAsync(string? text, FakeDialogService? dialogs = null,
        bool draft = true, DailyReviewAnswers? review = null)
    {
        var accounts = new FakeTradingAccountReader();
        accounts.EnqueueResult([new AccountListItem(Guid.NewGuid(), "Archive account", TradingAccountType.Personal, null, null, "USD", null, false)]);
        var vm = new JournalViewModel(new ReadRepository(text, draft, review), accounts, dialogs ?? new(),
            new JournalTradeContextViewModel(new FakeTradingCalendarDayReader(), new FakeTradingAccountReader()), new FixedTimeProvider());
        await vm.ActivateAsync();
        return vm;
    }

    private static (JournalView View, Border Root) Layout(JournalViewModel vm, string theme, int width)
    {
        ResourceDictionary resources = CalendarViewLayoutTests.SharedThemeResources(theme);
        System.Windows.Application.Current.Resources = resources;
        var view = new JournalView { DataContext = vm };
        var root = new Border { Resources = resources, Child = view };
        root.SetResourceReference(Border.BackgroundProperty, "PtjBackgroundBrush");
        root.Measure(new Size(width, 720));
        root.Arrange(new Rect(0, 0, width, 720));
        root.UpdateLayout();
        Flush();
        return (view, root);
    }

    private static void Flush() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (DependencyObject nested in Descendants(child)) yield return nested;
        }
    }

    private static void Render(Border root, string theme, int width, int dpi, string variant = "")
    {
        using (CalendarStaTest.Phase("Journal RenderTargetBitmap"))
        {
            var bitmap = new RenderTargetBitmap(width * dpi / 96, 720 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
            bitmap.Render(root);
            Assert.Equal(width * dpi / 96, bitmap.PixelWidth);
            if (Environment.GetEnvironmentVariable("PTJ_JOURNAL_RENDER_DIRECTORY") is { Length: > 0 } path)
            {
                Directory.CreateDirectory(path);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(path, $"journal-{variant}{theme}-{width}-{dpi}.png"));
                encoder.Save(file);
            }
        }
    }

    private static readonly Lazy<Task> Host = new(() => IsolatedTestProcess.RunSuiteAsync(
        typeof(JournalViewTests), "journal-editor", "PTJ_JOURNAL_EDITOR_TEST_HOST",
        TimeSpan.FromMinutes(2), caseHangTimeout: TimeSpan.FromSeconds(30)));
    private static Task OnSta(Action action, [System.Runtime.CompilerServices.CallerMemberName] string scenario = "") =>
        Environment.GetEnvironmentVariable("PTJ_JOURNAL_EDITOR_TEST_HOST") == "1"
            ? CalendarStaTest.RunAsync(action, scenario) : Host.Value;

    private sealed class ReadRepository(string? text, bool draft = true, DailyReviewAnswers? review = null) : IDailyJournalRepository
    {
        public Task<DailyJournalWriteResult> DeleteAsync(DeleteDailyJournalCommand command, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DailyJournalDetails?> GetAsync(DateOnly date, Guid? accountId = null, CancellationToken cancellationToken = default) =>
            // Persisted fixtures include legacy answers-only Completed entries; reads rehydrate rather than create.
            Task.FromResult(text is null ? null : new DailyJournalDetails(DailyJournalEntry.Rehydrate(Guid.NewGuid(), date, accountId, text, draft, 1,
                FixedTimeProvider.FixedUtcNow, FixedTimeProvider.FixedUtcNow, review ?? DailyReviewAnswers.Empty), DailyJournalAccountState.AllAccounts, null));
        public Task<IReadOnlyList<DailyJournalRevision>> GetHistoryAsync(Guid journalId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DailyJournalWriteResult> CreateAsync(CreateDailyJournalCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DailyJournalWriteResult> UpdateAsync(UpdateDailyJournalCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
