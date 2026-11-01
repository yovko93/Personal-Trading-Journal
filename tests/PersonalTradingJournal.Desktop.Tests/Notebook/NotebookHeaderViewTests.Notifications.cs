using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;
using PersonalTradingJournal.Desktop.Views.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed partial class NotebookHeaderViewTests
{
    [Theory]
    [InlineData("Light", 1280, 96)]
    [InlineData("Dark", 1280, 96)]
    [InlineData("Light", 480, 240)]
    [InlineData("Dark", 480, 240)]
    public async Task TopNotificationsWrapAcrossPanelsAndDismissWithoutResolvingFailedSave(string theme, int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            string expected = string.Create(CultureInfo.InvariantCulture,
                $"{typeof(NotebookHeaderViewTests).FullName}.{nameof(TopNotificationsWrapAcrossPanelsAndDismissWithoutResolvingFailedSave)}(theme: \"{theme}\", width: {width}, dpi: {dpi})");
            await IsolatedTestProcess.RunSuiteAsync(typeof(NotebookHeaderViewTests), "notebook-notifications", Host,
                TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30),
                testCaseFilter: IsolatedTestProcess.ExactDisplayNameFilter(expected), expectedTestName: expected);
            return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
            var prior = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var data = new NotebookFake(); data.Notes.Add(NotebookFake.Note("Market research") with { Document = NotebookEditingTests.Text("Retained research content") });
            var clock = new NotebookClock(); using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService(), new NotebookChoices(), time: clock);
            var view = new NotebookWorkspaceView { DataContext = vm, Width = width, Height = 900 };
            var window = new Window { Content = view, SizeToContent = SizeToContent.WidthAndHeight, ShowActivated = false, ShowInTaskbar = false };
            try
            {
                window.Show(); Pump(vm.ActivateAsync()); vm.SelectedNote = vm.Notes[0]; Pump(vm.LoadTask); Layout();
                var banner = (Border)view.FindName("NotebookNotification"); var text = (TextBlock)view.FindName("NotebookNotificationText");
                var close = (Button)view.FindName("DismissNotebookNotification"); var search = (TextBox)view.FindName("SearchBox");
                Assert.Equal(Visibility.Collapsed, banner.Visibility); Assert.Equal(TextWrapping.Wrap, text.TextWrapping);
                Assert.Equal("Dismiss Notebook notification", AutomationProperties.GetName(close)); Assert.NotNull(close.ToolTip);
                UiKeyboardProbe.Ready(search); Assert.Same(search, Keyboard.FocusedElement);
                foreach (var (kind, brush) in new[] {
                    (NotebookNotificationKind.Success, "PtjSuccessSurfaceBrush"), (NotebookNotificationKind.Information, "PtjInformationSurfaceBrush"),
                    (NotebookNotificationKind.Warning, "PtjJournalReviewBrush"), (NotebookNotificationKind.Error, "PtjDangerSurfaceBrush") })
                {
                    vm.ShowNotification(kind switch
                    {
                        NotebookNotificationKind.Success => "Folder created. It is available under FOLDERS.",
                        NotebookNotificationKind.Information => "Saved note is outside this list page/filter. Its editor remains open.",
                        NotebookNotificationKind.Warning => "Operation stopped. Refresh to reload stored data; a completed change may already be saved.",
                        _ => "The operation could not finish. Input is retained; Refresh to check stored data before trying again. Your existing note and its recovery actions remain available."
                    }, kind);
                    Layout(); Assert.Same(search, Keyboard.FocusedElement);
                    Assert.Same(view.FindResource(brush), banner.Background); Assert.StartsWith(kind.ToString(), text.Text);
                    Assert.Equal(vm.Notification!.AccessibleText, AutomationProperties.GetName(banner));
                    var timer = vm.NotificationExpiryTask; text.GetBindingExpression(TextBlock.TextProperty)!.UpdateTarget(); Assert.Same(timer, vm.NotificationExpiryTask);
                    foreach (int panel in new[] { 0, 1, 2 })
                    {
                        vm.Panel = panel; Layout(); Assert.True(banner.IsVisible); Assert.True(close.IsVisible);
                        var panels = (Grid)view.FindName("Panels");
                        Assert.True(banner.TranslatePoint(new Point(0, banner.ActualHeight), view).Y <= panels.TranslatePoint(new Point(0, 0), view).Y);
                        Assert.True(close.TranslatePoint(new Point(close.ActualWidth, 0), view).X <= width);
                    }
                    vm.Panel = kind == NotebookNotificationKind.Error ? 2 : 0; Layout(); Render(kind.ToString().ToLowerInvariant());
                }
                vm.DraftTitle = "Unsaved title retained after error"; data.NextWrite = NotebookWriteStatus.StorageFailure;
                Pump(vm.SaveDraftAsync()); Layout(); Assert.Equal(NotebookNotificationKind.Error, vm.Notification!.Kind);
                string detail = vm.SaveFeedback; Assert.NotEmpty(detail); Assert.True(vm.IsDirty);
                Render("failed-save"); UiKeyboardProbe.Space(close); Layout();
                Assert.Null(vm.Notification); Assert.Equal(Visibility.Collapsed, banner.Visibility);
                Assert.Equal(detail, ((TextBlock)view.FindName("SaveFeedback")).Text); Assert.True(vm.SaveCommand.CanExecute(null)); Assert.True(vm.DiscardCommand.CanExecute(null));
                Assert.Equal("Unsaved title retained after error", vm.DraftTitle); Render("dismissed-save-error");
                UiKeyboardProbe.Ready(search); vm.ShowNotification("Dismisses at ten seconds", NotebookNotificationKind.Information); Layout();
                clock.Advance(9999); Layout(); Assert.True(banner.IsVisible); Assert.Same(search, Keyboard.FocusedElement);
                clock.Advance(1); Pump(vm.NotificationExpiryTask); Layout(); Assert.False(banner.IsVisible); Assert.Same(search, Keyboard.FocusedElement);
            }
            finally { window.Close(); vm.Deactivate(); Pump(vm.DeactivationTask); app.Shutdown(); SynchronizationContext.SetSynchronizationContext(prior); }
            void Layout() { view.UpdateLayout(); Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); }
            void Render(string state)
            {
                var snapshot = new NotebookWorkspaceView { DataContext = vm };
                VisualTreeHelper.SetRootDpi(snapshot, new(dpi / 96d, dpi / 96d)); snapshot.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                try
                {
                    snapshot.Measure(new Size(width, 900)); snapshot.Arrange(new Rect(0, 0, width, 900)); snapshot.UpdateLayout();
                    string directory = Path.Combine(Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY")!, "notification-renders"); Directory.CreateDirectory(directory);
                    var bitmap = new RenderTargetBitmap(width * dpi / 96, 900 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(snapshot);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var file = File.Create(Path.Combine(directory, $"{theme}-{width}-{dpi}-{state}.png")); encoder.Save(file);
                }
                finally { snapshot.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }
            }
        });
    }
}
