using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Threading;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Tests.Trades;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Desktop.Views.Calendar;

namespace PersonalTradingJournal.Desktop.Tests.CalendarPage;

public sealed partial class CalendarDayModalTests
{
    [Theory]
    [InlineData("Light", 1280, 240, false)]
    [InlineData("Dark", 1280, 240, false)]
    [InlineData("Light", 1900, 96, false)]
    [InlineData("Dark", 1900, 96, false)]
    [InlineData("Light", 480, 240, false)]
    [InlineData("Dark", 480, 240, false)]
    [InlineData("Light", 1280, 240, true)]
    [InlineData("Dark", 1280, 240, true)]
    [InlineData("Light", 1900, 96, true)]
    [InlineData("Dark", 1900, 96, true)]
    [InlineData("Light", 480, 240, true)]
    [InlineData("Dark", 480, 240, true)]
    public async Task InitialOwnedPanelFitsTableAtDesktopWidthAndWrapsWithoutLosingAction(string theme, int width, int dpi, bool longName)
    {
        var date = new DateOnly(2026, 9, 5);
        var row = CalendarDayDetailsTests.Row(date, -285, null) with { TradingAccountName = longName
            ? "An unusually long historical account name with a full-name tooltip" : "P 21" };
        var detail = TradesViewModelTests.CreateEditableTradeDetail(row, null);
        var details = new FakeTradeDetailReader(); details.EnqueueResult(detail);
        var editor = TradesViewModelTests.CreateViewModel(tradeDetailReader: details);
        var reader = new FakeTradingCalendarDayReader { Handler = (q, ct) => Task.FromResult(
            TradingCalendarDayDetails.Create(q.Date, [row], ct) with
            {
                Classifications = new Dictionary<Guid, CalendarTradeClassification>
                {
                    [row.Id] = new(Guid.NewGuid(), "A long Setup name with several conditions and confirmations", true,
                        [new(Guid.NewGuid(), "Entered before confirming all conditions of the planned Setup", true),
                         new(Guid.NewGuid(), "Exceeded the planned position size", true)])
                }
            }) };
        var vm = await CalendarSummaryFixture.CreateAsync(reader, editor);
        await OnSta(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            CalendarView view;
            using (CalendarStaTest.Timing("Calendar view construction and resources"))
                view = new CalendarView { DataContext = vm, Resources = CalendarViewLayoutTests.SharedThemeResources(theme) };
            var owner = new Window { Content = view, Width = width, Height = 920, ShowInTaskbar = false };
            ApplyRunnerWindowConstraint(owner);
            Exception? failure = null;
            try
            {
                using (CalendarStaTest.Timing("owner show and initial layout"))
                { owner.Show(); Pump(); owner.UpdateLayout(); }
                var cell = Descendants(view).OfType<CalendarDayHost>().Single(c => ((CalendarDayCell)c.DataContext).Date == date);
                using var focus = new CalendarFocusProbe(owner, cell, $"InitialOwnedPanel/{theme}/{width}/{dpi}/{longName}");
                dispatcher.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        var dialog = view.DayDialog!;
                        focus.ObserveDialog(dialog);
                        using (CalendarStaTest.Timing("day read and dialog layout"))
                        { await vm.DayLoadTask; dialog.UpdateLayout(); }
                        var panel = (Border)dialog.FindName("DayPanel");
                        var content = (CalendarDayDetailsView)dialog.FindName("DayContent");
                        var header = (Grid)content.FindName("DayTradesHeader");
                        var table = (StackPanel)content.FindName("DayTradesTable");
                        var scroll = (ScrollViewer)content.FindName("DayTradesScroller");
                        string? renderDirectory = Environment.GetEnvironmentVariable("PTJ_CALENDAR_RENDER_DIRECTORY");
                        double? beforeViewport = null, beforeTable = null;
                        // The obsolete width is only a before/after screenshot diagnostic.
                        // Normal acceptance must test the actual initial viewport without
                        // forcing two extra layouts or screenshot-only scrolling first.
                        if (renderDirectory is { Length: > 0 })
                        {
                            using var comparison = CalendarStaTest.Timing("historical width comparison");
                            double initialCap = panel.MaxWidth;
                            try
                            {
                                panel.MaxWidth = 1000; Pump(); dialog.UpdateLayout();
                                beforeViewport = scroll.ViewportWidth; beforeTable = table.ActualWidth;
                            }
                            finally { panel.MaxWidth = initialCap; Pump(); dialog.UpdateLayout(); }
                        }
                        Assert.Equal(0, scroll.HorizontalOffset);
                        var grid = Descendants(content).OfType<Grid>().Single(g => g.Name == "DayTradeRow");
                        WriteLayoutDiagnostics(owner, header, scroll, [grid], $"Owned: {theme}/{width}/{dpi}/{longName}");
                        for (int i = 0; i < 9; i++)
                        {
                            Assert.Equal(header.ColumnDefinitions[i].ActualWidth, grid.ColumnDefinitions[i].ActualWidth, 1);
                            var heading = header.Children.OfType<FrameworkElement>().Single(e => Grid.GetColumn(e) == i);
                            var value = grid.Children.OfType<FrameworkElement>().Single(e => Grid.GetColumn(e) == i);
                            Assert.Equal(heading.TransformToAncestor(scroll).Transform(new Point()).X,
                                value.TransformToAncestor(scroll).Transform(new Point()).X, 1);
                        }
                        Assert.Equal(150, header.ColumnDefinitions[3].ActualWidth, 1);
                        AssertContentSizedAccount(header, [grid]);
                        var action = grid.Children.OfType<Button>().Single();
                        double actionRight = action.TransformToAncestor(scroll).Transform(new Point()).X + action.ActualWidth;
                        bool actionFits = actionRight <= scroll.ViewportWidth + 1;
                        if (scroll.ViewportWidth >= table.MinWidth)
                        {
                            Assert.Equal(0, scroll.ScrollableWidth);
                            Assert.True(actionFits);
                            Assert.Equal(scroll.ViewportWidth, table.ActualWidth, 1);
                        }
                        else
                        {
                            Assert.True(scroll.ScrollableWidth > 0);
                            scroll.ScrollToRightEnd(); Pump(); dialog.UpdateLayout();
                            Assert.True(action.TransformToAncestor(scroll).Transform(new Point()).X + action.ActualWidth <= scroll.ViewportWidth + 1);
                            scroll.ScrollToLeftEnd(); Pump();
                        }
                        foreach (int column in new[] { 6, 7 })
                        {
                            var text = grid.Children.OfType<TextBlock>().Single(t => Grid.GetColumn(t) == column);
                            Assert.Equal(TextWrapping.Wrap, text.TextWrapping);
                            Assert.Equal(text.Text, text.ToolTip);
                        }
                        double gap = table.ActualWidth - header.ColumnDefinitions.Sum(c => c.ActualWidth) - 16;
                        Assert.InRange(gap, -1, 1);
                        string measured = $"Owner={owner.ActualWidth:F2}; client={dialog.ActualWidth:F2}; panel={panel.ActualWidth:F2}; " +
                            $"viewport before={beforeViewport:F2}, after={scroll.ViewportWidth:F2}; table before={beforeTable:F2}, after={table.ActualWidth:F2}; " +
                            $"Action fits={actionFits}; trailing gap={gap:F2}; columns={string.Join(", ", header.ColumnDefinitions.Select(c => c.ActualWidth.ToString("F2")))}";
                        using (CalendarStaTest.Timing("inline view and layout"))
                        { await vm.ViewTradeCommand.ExecuteAsync(row); Pump(); dialog.UpdateLayout(); }
                        Assert.True(Assert.Single(vm.DayTrades).IsExpanded);
                        Assert.Same(editor, Descendants(content).OfType<CalendarInlineTradeView>().Single().DataContext);
                        if (renderDirectory is { Length: > 0 } output)
                        {
                            Directory.CreateDirectory(output);
                            File.WriteAllText(System.IO.Path.Combine(output, $"modal-width-{theme}-{width}-{dpi}-{longName}.txt"), measured);
                            var page = (ScrollViewer)content.FindName("DayContentScroller");
                            page.ScrollToVerticalOffset(header.TransformToAncestor(page).Transform(new Point()).Y - 12); Pump();
                            Render(dialog, $"initial-{theme}-{longName}", width, dpi);
                        }
                        focus.Capture("before dialog.Close");
                        using (CalendarStaTest.Timing("dialog close")) dialog.Close();
                    }
                    catch (Exception exception) { failure = exception; view.DayDialog?.Close(); }
                }));
                using (CalendarStaTest.Timing("modal open interaction and return"))
                    cell.RaiseEvent(new RoutedEventArgs(CalendarDayHost.InvokedEvent));
                focus.Capture("ShowDialog returned");
                Pump(); if (failure is not null) throw failure;
                Assert.Null(view.DayDialog);
                focus.VerifyRestored();
                Assert.True(cell.IsKeyboardFocused);
            }
            finally { using (CalendarStaTest.Timing("owner close")) owner.Close(); }
        });
    }

    [Theory]
    [InlineData("Light", "Normal")]
    [InlineData("Dark", "Maximized")]
    [InlineData("Light", "Minimized")]
    [InlineData("Dark", "Returned")]
    public async Task WindowControlsRetainModalAndDraftThenCloseInEveryState(string theme, string closeState)
    {
        var date = new DateOnly(2026, 9, 5);
        var row = CalendarDayDetailsTests.Row(date, 10, 9);
        var detail = TradesViewModelTests.CreateEditableTradeDetail(row, null);
        var details = new FakeTradeDetailReader(); details.EnqueueResult(detail); details.EnqueueResult(detail);
        var editor = TradesViewModelTests.CreateViewModel(tradeDetailReader: details, reader: TradesViewModelTests.CreateEditReferenceReader(detail));
        var reader = new FakeTradingCalendarDayReader { Handler = (q, ct) => Task.FromResult(TradingCalendarDayDetails.Create(q.Date, [row], ct)) };
        var vm = await CalendarSummaryFixture.CreateAsync(reader, editor);
        await OnSta(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            var view = new CalendarView { DataContext = vm, Resources = CalendarViewLayoutTests.SharedThemeResources(theme) };
            var owner = new Window { Content = view, Width = 1400, Height = 980, ShowInTaskbar = false };
            Exception? failure = null;
            try
            {
                owner.Show(); Pump(); owner.UpdateLayout();
                var cell = Descendants(view).OfType<CalendarDayHost>().Single(c => ((CalendarDayCell)c.DataContext).Date == date);
                using var focus = new CalendarFocusProbe(owner, cell, $"WindowControls/{theme}/{closeState}");
                dispatcher.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        var dialog = view.DayDialog!;
                        focus.ObserveDialog(dialog);
                        await vm.DayLoadTask; dialog.UpdateLayout();
                        var panel = (Border)dialog.FindName("DayPanel");
                        var maximize = (Button)dialog.FindName("MaximizeButton");
                        var minimize = (Button)dialog.FindName("MinimizeButton");
                        double restoreWidth = panel.ActualWidth, restoreHeight = panel.ActualHeight;
                        Assert.False(IsWindowEnabled(new WindowInteropHelper(owner).Handle));
                        Assert.True(minimize.IsVisible && maximize.IsVisible);
                        Click(maximize); Pump(); dialog.UpdateLayout();
                        Assert.True(dialog.IsPanelMaximized);
                        Assert.Equal(dialog.ActualWidth - 16, panel.ActualWidth, 1);
                        Assert.Equal(dialog.ActualHeight - 16, panel.ActualHeight, 1);
                        Assert.Equal("Restore Day Performance", AutomationProperties.GetName(maximize));
                        Click(maximize); Pump(); dialog.UpdateLayout();
                        Assert.False(dialog.IsPanelMaximized);
                        Assert.Equal(restoreWidth, panel.ActualWidth, 1); Assert.Equal(restoreHeight, panel.ActualHeight, 1);
                        await vm.ViewTradeCommand.ExecuteAsync(row);
                        await editor.ShowSelectedTradeEditCommand.ExecuteAsync(null);
                        editor.EntryPriceText = "123.456";
                        string draft = editor.EntryPriceText;
                        Click(maximize); Click(minimize); Pump();
                        Assert.Equal(WindowState.Minimized, owner.WindowState);
                        Assert.Equal(WindowState.Minimized, dialog.WindowState);
                        Assert.Same(dialog, view.DayDialog); Assert.True(editor.IsTradeEditVisible);
                        Assert.False(IsWindowEnabled(new WindowInteropHelper(owner).Handle));
                        // Model the taskbar restoring the application: the owner StateChanged must return the modal.
                        owner.WindowState = WindowState.Normal; Pump(); dialog.UpdateLayout();
                        Assert.Equal(WindowState.Normal, dialog.WindowState);
                        Assert.True(dialog.IsPanelMaximized); Assert.True(dialog.IsVisible);
                        Assert.Equal(draft, editor.EntryPriceText); Assert.True(editor.IsTradeEditVisible);
                        Assert.False(IsWindowEnabled(new WindowInteropHelper(owner).Handle));
                        owner.Width = 1280; Pump(); dialog.UpdateLayout();
                        Assert.Equal(view.ActualWidth, dialog.ActualWidth, 1);
                        dialog.Close(); Assert.True(dialog.IsVisible); Assert.True(editor.IsTradeEditVisible);
                        editor.CancelTradeEditCommand.Execute(null);
                        if (closeState == "Normal") Click(maximize);
                        if (closeState == "Minimized") Click(minimize);
                        if (closeState == "Returned") { Click(minimize); owner.WindowState = WindowState.Normal; Pump(); }
                        if (closeState == "Normal")
                        {
                            var backdrop = (Border)dialog.FindName("DialogBackdrop");
                            Press(backdrop); Release(backdrop);
                        }
                        else if (closeState == "Maximized") Click((Button)dialog.FindName("DismissButton"));
                        else dialog.Close();
                    }
                    catch (Exception exception) { failure = exception; editor.CancelTradeEditCommand.Execute(null); view.DayDialog?.Close(); }
                }));
                cell.RaiseEvent(new RoutedEventArgs(CalendarDayHost.InvokedEvent));
                Pump(); if (failure is not null) throw failure;
                Assert.Null(view.DayDialog); Assert.Equal(date, vm.SelectedDate);
                Assert.True(IsWindowEnabled(new WindowInteropHelper(owner).Handle));
                if (owner.WindowState == WindowState.Minimized) owner.WindowState = WindowState.Normal;
                focus.VerifyRestored(); Assert.True(cell.IsKeyboardFocused);
                Assert.Single(reader.Calls);
            }
            finally { editor.CancelTradeEditCommand.Execute(null); owner.Close(); }
        });
    }

    [Fact]
    public async Task ClosedModalRetainsDateFocusAcrossCompetingWindowActivation()
    {
        var vm = await CalendarSummaryFixture.CreateAsync(new FakeTradingCalendarDayReader
        {
            Handler = (q, ct) => Task.FromResult(TradingCalendarDayDetails.Create(q.Date, [], ct))
        });
        await OnSta(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            var view = new CalendarView { DataContext = vm, Resources = CalendarViewLayoutTests.SharedThemeResources("Light") };
            var owner = new Window { Content = view, Width = 960, Height = 760, ShowInTaskbar = false };
            var otherButton = new Button { Content = "Competing synthetic window" };
            var other = new Window { Content = otherButton, Width = 220, Height = 160, ShowInTaskbar = false };
            Exception? failure = null;
            try
            {
                owner.Show(); Pump(); owner.UpdateLayout();
                var cell = Descendants(view).OfType<CalendarDayHost>().Single(c => ((CalendarDayCell)c.DataContext).Date == new DateOnly(2026, 9, 5));
                using var focus = new CalendarFocusProbe(owner, cell);
                dispatcher.BeginInvoke(new Action(() =>
                {
                    try { focus.ObserveDialog(view.DayDialog!); view.DayDialog!.Close(); }
                    catch (Exception error) { failure = error; view.DayDialog?.Close(); }
                }));
                cell.RaiseEvent(new RoutedEventArgs(CalendarDayHost.InvokedEvent));
                if (failure is not null) throw failure;
                Assert.Null(view.DayDialog);
                Assert.Same(cell, FocusManager.GetFocusedElement(owner));
                other.Show(); Assert.True(otherButton.Focus());
                Assert.True(other.IsActive);
                focus.Capture("competing window owns keyboard after modal closed");
                Assert.False(owner.IsActive || cell.IsKeyboardFocused);
                Assert.Same(otherButton, Keyboard.FocusedElement);
                Assert.Same(cell, FocusManager.GetFocusedElement(owner));
                // No test call to cell.Focus(): Windows must restore production's retained target.
                focus.VerifyRestored();
                Assert.True(cell.IsKeyboardFocused);
                Assert.Same(cell, Keyboard.FocusedElement);
                Assert.Equal(new DateOnly(2026, 9, 5), vm.SelectedDate);
            }
            finally { other.Close(); owner.Close(); }
        });
    }

    [Fact]
    public async Task FocusVerificationRejectsWrongRetainedTargetRatherThanRepairingIt()
    {
        await OnSta(() =>
        {
            var cell = new CalendarDayHost { Focusable = true, Width = 100, Height = 60 };
            var wrong = new Button { Content = "Wrong retained focus" };
            var panel = new StackPanel(); panel.Children.Add(cell); panel.Children.Add(wrong);
            var owner = new Window { Content = panel, Width = 320, Height = 240, ShowInTaskbar = false };
            try
            {
                owner.Show(); Pump(); Assert.True(wrong.Focus());
                using var focus = new CalendarFocusProbe(owner, cell);
                var error = Assert.Throws<InvalidOperationException>(() => focus.VerifyRestored());
                Assert.Contains("logical", error.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Same(wrong, FocusManager.GetFocusedElement(owner));
                Assert.Same(wrong, Keyboard.FocusedElement);
                Assert.False(cell.IsKeyboardFocused);
            }
            finally { owner.Close(); }
        });
    }

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool IsWindowEnabled(IntPtr handle);
}
