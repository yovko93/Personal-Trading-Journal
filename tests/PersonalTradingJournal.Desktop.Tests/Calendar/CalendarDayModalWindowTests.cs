using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Desktop.Views.Calendar;

namespace PersonalTradingJournal.Desktop.Tests.CalendarPage;

public sealed partial class CalendarDayModalTests
{
    [Theory]
    [InlineData("Light", "Normal")]
    [InlineData("Dark", "Maximized")]
    [InlineData("Light", "Minimized")]
    [InlineData("Dark", "Returned")]
    public async Task WindowControlsRetainModalAndDraftThenCloseInEveryState(string theme, string closeState)
    {
        var date = new DateOnly(2026, 9, 5);
        var journalDialogs = new FakeDialogService();
        var vm = await CalendarSummaryFixture.CreateAsync(journalDialogs: journalDialogs);
        await OnSta(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            var view = new CalendarView { DataContext = vm, Resources = CalendarViewLayoutTests.SharedThemeResources(theme) };
            var owner = new Window { Content = view, Width = 1400, Height = 980, ShowInTaskbar = false };
            void Step(string phase, Action action)
            {
                using var diagnostic = CalendarStaTest.Phase("Window controls: " + phase);
                action();
            }
            Exception? failure = null;
            try
            {
                Step("owner show, pump and layout", () => { owner.Show(); Pump(); owner.UpdateLayout(); });
                var cell = Descendants(view).OfType<CalendarDayHost>().Single(c => ((CalendarDayCell)c.DataContext).Date == date);
                using var focus = new CalendarFocusProbe(owner, cell, $"WindowControls/{theme}/{closeState}");
                dispatcher.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        var dialog = view.DayDialog!;
                        focus.ObserveDialog(dialog);
                        using (CalendarStaTest.Phase("Window controls: await day loading")) await vm.DayJournalLoadTask;
                        Step("initial dialog layout", dialog.UpdateLayout);
                        var panel = (Border)dialog.FindName("DayPanel");
                        var maximize = (Button)dialog.FindName("MaximizeButton");
                        var minimize = (Button)dialog.FindName("MinimizeButton");
                        double restoreWidth = panel.ActualWidth, restoreHeight = panel.ActualHeight;
                        Assert.InRange(restoreHeight, 160, 300); // Empty day needs only its date, Journal actions and chrome.
                        Assert.False(IsWindowEnabled(new WindowInteropHelper(owner).Handle));
                        Assert.True(minimize.IsVisible && maximize.IsVisible);
                        Step("maximize/restore, pump and layout", () => { Click(maximize); Pump(); dialog.UpdateLayout(); });
                        Assert.True(dialog.IsPanelMaximized);
                        Assert.Equal(dialog.ActualWidth - 16, panel.ActualWidth, 1);
                        Assert.Equal(dialog.ActualHeight - 16, panel.ActualHeight, 1);
                        Assert.Equal("Restore Calendar day", AutomationProperties.GetName(maximize));
                        Step("maximize/restore, pump and layout", () => { Click(maximize); Pump(); dialog.UpdateLayout(); });
                        Assert.False(dialog.IsPanelMaximized);
                        Assert.Equal(restoreWidth, panel.ActualWidth, 1); Assert.Equal(restoreHeight, panel.ActualHeight, 1);
                        await vm.AddDayJournalCommand.ExecuteAsync(null);
                        var editor = vm.InlineJournal!;
                        const string draft = "Keep this Journal draft";
                        editor.Text = draft;
                        Step("maximize and minimize with draft", () => { Click(maximize); Click(minimize); Pump(); });
                        Assert.Equal(WindowState.Minimized, owner.WindowState);
                        Assert.Equal(WindowState.Minimized, dialog.WindowState);
                        Assert.Same(dialog, view.DayDialog); Assert.True(editor.IsEditorOpen);
                        Assert.False(IsWindowEnabled(new WindowInteropHelper(owner).Handle));
                        // Model the taskbar restoring the application: the owner StateChanged must return the modal.
                        Step("restore owner and modal", () => { owner.WindowState = WindowState.Normal; Pump(); dialog.UpdateLayout(); });
                        Assert.Equal(WindowState.Normal, dialog.WindowState);
                        Assert.True(dialog.IsPanelMaximized); Assert.True(dialog.IsVisible);
                        Assert.Equal(draft, editor.Text); Assert.True(editor.IsEditorOpen);
                        Assert.False(IsWindowEnabled(new WindowInteropHelper(owner).Handle));
                        Step("resize owner", () => { owner.Width = 1280; Pump(); dialog.UpdateLayout(); });
                        Assert.Equal(view.ActualWidth, dialog.ActualWidth, 1);
                        Step("dirty close veto", () => { dialog.Close(); Assert.True(dialog.IsVisible); Assert.True(editor.IsEditorOpen); });
                        journalDialogs.ConfirmationResult = true;
                        using var closingPhase = CalendarStaTest.Phase("Window controls: final " + closeState + " dismissal");
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
                    catch (Exception exception) { failure = exception; Step("exception cleanup", () => { journalDialogs.ConfirmationResult = true; view.DayDialog?.Close(); }); }
                }));
                Step("modal invocation", () => cell.RaiseEvent(new RoutedEventArgs(CalendarDayHost.InvokedEvent)));
                Step("post-close dispatcher pump", Pump); if (failure is not null) throw failure;
                Assert.Null(view.DayDialog); Assert.Equal(date, vm.SelectedDate);
                Assert.True(IsWindowEnabled(new WindowInteropHelper(owner).Handle));
                if (owner.WindowState == WindowState.Minimized) owner.WindowState = WindowState.Normal;
                Step("verify restored focus", () => { focus.VerifyRestored(); Assert.True(cell.IsKeyboardFocused); });
            }
            finally { Step("owner cleanup", () => { journalDialogs.ConfirmationResult = true; owner.Close(); }); }
        });
    }

    [Fact]
    public async Task ClosedModalRetainsDateFocusAcrossCompetingWindowActivation()
    {
        var vm = await CalendarSummaryFixture.CreateAsync();
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
