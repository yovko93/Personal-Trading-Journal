using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
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

// Uses the established exclusive creation fixture, scheduling and process deadlines.
public sealed partial class NotebookFolderCreationViewTests
{
    [Theory]
    [InlineData("Light", 1280, 96)]
    [InlineData("Dark", 480, 240)]
    public async Task InlineTagCreationAndCapturedManagementUseRealControls(string theme, int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            string expected = string.Create(CultureInfo.InvariantCulture,
                $"{typeof(NotebookFolderCreationViewTests).FullName}.{nameof(InlineTagCreationAndCapturedManagementUseRealControls)}(theme: \"{theme}\", width: {width}, dpi: {dpi})");
            await IsolatedTestProcess.RunSuiteAsync(typeof(NotebookFolderCreationViewTests), "notebook-tag-form", Host,
                TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30),
                testCaseFilter: IsolatedTestProcess.ExactDisplayNameFilter(expected), expectedTestName: expected); return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
            var prior = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var open = NotebookTagTests.Fixture.Open(); Pump(open); var f = open.Result; var vm = f.Vm;
            var view = new NotebookWorkspaceView { DataContext = vm, Width = width, Height = 900 };
            var outside = new Button { Content = "Other shell action", Height = 32 };
            var host = new DockPanel(); DockPanel.SetDock(outside, Dock.Top); host.Children.Add(outside); host.Children.Add(view);
            var window = new Window { Content = host, SizeToContent = SizeToContent.WidthAndHeight, ShowActivated = false, ShowInTaskbar = false };
            try
            {
                window.Show(); vm.Panel = 0; Layout();
                var button = (Button)view.FindName("NewTagButton"); var input = (TextBox)view.FindName("NewTagName");
                var add = (Button)view.FindName("AddNewTagButton"); var cancel = (Button)view.FindName("CancelTagCreationButton");
                Assert.Null(view.FindName("TagManagement")); Assert.Equal("+ New tag", button.Content);
                Assert.Same(view.FindResource("PtjPlanReadyBrush"), button.Background);
                UiKeyboardProbe.Space(button); Layout(); Assert.Same(input, Keyboard.FocusedElement);
                Assert.True(vm.IsTagCreationOpen); Assert.True(cancel.IsVisible); Assert.Equal(Visibility.Collapsed, add.Visibility); Render("empty");
                input.Text = " \t "; Layout(); Assert.Equal(Visibility.Collapsed, add.Visibility);
                input.Text = "Market context"; Layout(); Assert.True(add.IsVisible); Render("populated");
                UiKeyboardProbe.Ready(outside); Assert.Equal(0, f.Data.Writes); Assert.True(vm.IsTagCreationOpen);
                UiKeyboardProbe.Ready(input); PressTagKey(input, Key.Escape); Layout(); Assert.False(vm.IsTagCreationOpen); Assert.Equal("", input.Text); Assert.Same(button, Keyboard.FocusedElement);
                UiKeyboardProbe.Space(button); Layout(); input.Text = "Cancel draft"; UiKeyboardProbe.Space(cancel); Layout();
                Assert.Equal(0, f.Data.Writes); Assert.False(vm.IsTagCreationOpen); Assert.Equal("", input.Text);
                UiKeyboardProbe.Space(button); Layout(); input.Text = " "; PressTagKey(input, Key.Enter); Pump(vm.CreateTagCommand.ExecutionTask!); Layout();
                Assert.Equal(0, f.Data.Writes); Assert.NotEmpty(vm.TagMessage); Assert.Equal(" ", input.Text); Render("validation");
                input.Text = "Failed input"; f.Data.NextWrite = NotebookWriteStatus.StorageFailure;
                Invoke(add); Pump(vm.CreateTagCommand.ExecutionTask!); Layout(); Assert.Equal("Failed input", input.Text); Assert.True(vm.IsTagCreationOpen); Assert.NotEmpty(vm.TagMessage);
                f.Data.NextWrite = NotebookWriteStatus.Success; input.Text = "Created by Add"; Invoke(add); Pump(vm.CreateTagCommand.ExecutionTask!); Layout();
                Assert.Equal(2, f.Data.Writes); Assert.False(vm.IsTagCreationOpen); Assert.Equal("", input.Text);
                Assert.Equal(2, vm.AssignedTags.Count); Assert.Empty(vm.SelectedTagFilters); Assert.Equal(3, vm.Tags.Count);
                UiKeyboardProbe.Space(button); Layout(); input.Text = "Created by Enter"; PressTagKey(input, Key.Enter); Pump(vm.CreateTagCommand.ExecutionTask!); Layout();
                Assert.Equal(3, f.Data.Writes); Assert.Equal(4, vm.Tags.Count); Assert.False(vm.IsTagCreationOpen);
                Assert.Equal(2, vm.AssignedTags.Count); Assert.Empty(vm.SelectedTagFilters);

                var tag = vm.Tags.Single(t => t.Name == "Research"); var other = vm.Tags.Single(t => t.Name == "Risk");
                Button Manage(Guid id) => Descendants<Button>((ItemsControl)view.FindName("TagFilters")).Single(b => b.DataContext is NotebookTagOption t && t.Id == id);
                var manage = Manage(tag.Id); UiKeyboardProbe.Space(manage); Layout();
                var menu = manage.ContextMenu!; Assert.True(menu.IsOpen); Assert.Empty(vm.SelectedTagFilters); Assert.Equal(3, f.Data.Writes);
                Assert.Equal("Rename", ((MenuItem)menu.Items[0]).Header); Assert.Equal("Delete globally…", ((MenuItem)menu.Items[1]).Header);
                Save(menu, "management-menu", menu.ActualWidth, menu.ActualHeight);
                vm.ManagedTag = other; InvokeMenu((MenuItem)menu.Items[0]); Layout();
                Assert.False(menu.IsOpen); Assert.True(vm.IsTagRenameOpen); Assert.Equal(tag.Name, vm.TagName);
                var rename = (TextBox)view.FindName("RenameTagName"); Assert.Same(rename, Keyboard.FocusedElement);
                rename.Text = "Long research observations with multiple words and context"; Render("rename");
                PressTagKey(rename, Key.Enter); Pump(vm.RenameTagCommand.ExecutionTask!); Layout();
                Assert.Equal(other.Name, vm.Tags.Single(t => t.Id == other.Id).Name); Assert.Contains("Long research", vm.Tags.Single(t => t.Id == tag.Id).Name);
                Assert.False(vm.IsTagRenameOpen); Assert.Equal(4, f.Data.Writes);
                manage = Manage(tag.Id); UiKeyboardProbe.Ready(manage); PressTagKey(manage, Key.Apps); Layout(); menu = manage.ContextMenu!;
                Assert.True(menu.IsOpen); f.Dialogs.ConfirmationResult = false; InvokeMenu((MenuItem)menu.Items[1]); Pump(view.TagMenuTask); Layout();
                Assert.Equal(4, f.Data.Writes); Assert.Contains(vm.Tags, t => t.Id == tag.Id); Assert.Empty(vm.SelectedTagFilters);
                UiKeyboardProbe.Space(Manage(tag.Id)); Layout(); menu = Manage(tag.Id).ContextMenu!;
                vm.ManagedTag = other; f.Dialogs.ConfirmationResult = true; InvokeMenu((MenuItem)menu.Items[1]); Pump(view.TagMenuTask); Layout();
                Assert.Equal(5, f.Data.Writes); Assert.DoesNotContain(vm.Tags, t => t.Id == tag.Id);
                Assert.Equal(other.Id, Assert.Single(vm.AssignedTags).Id); Assert.Contains("every note", f.Dialogs.ConfirmationRequest!.Message);
                Assert.Contains(vm.Tags, t => t.Id == other.Id); Assert.Empty(vm.SelectedTagFilters);

                UiKeyboardProbe.Space(button); Layout(); input.Text = "Committed after Cancel";
                var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var item = new NotebookNamedItem(Guid.NewGuid(), input.Text, 1);
                f.Data.OnCatalog = async (_, _) => { entered.SetResult(); await release.Task; f.Data.Tags.Add(item); return new(NotebookWriteStatus.Success, item); };
                Invoke(add); var pending = vm.CreateTagCommand.ExecutionTask!;
                try
                {
                    Pump(entered.Task); Layout(); Assert.True(input.IsReadOnly); Assert.False(add.IsEnabled); Assert.True(cancel.IsEnabled);
                    UiKeyboardProbe.Ready(input); PressTagKey(input, Key.Enter); Assert.Equal(6, f.Data.Writes);
                    UiKeyboardProbe.Space(cancel); Layout(); Assert.Equal("", input.Text); Assert.False(vm.IsTagCreationOpen); Assert.Contains("may already be saved", vm.Message);
                    UiKeyboardProbe.Ready(outside);
                }
                finally { release.TrySetResult(); Pump(pending); Layout(); }
                Assert.Equal(6, f.Data.Writes); Assert.Contains(vm.Tags, t => t.Id == item.Id);
                Assert.Same(outside, Keyboard.FocusedElement); Assert.Equal("", vm.TagName); Assert.Empty(vm.SelectedTagFilters); Assert.Single(vm.AssignedTags);
            }
            finally { window.Close(); Pump(f.DisposeAsync().AsTask()); app.Shutdown(); SynchronizationContext.SetSynchronizationContext(prior); }
            void Layout() { view.UpdateLayout(); Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); }
            void Invoke(Button b)
            {
                Assert.True(b.IsVisible); Assert.True(b.IsEnabled);
                ((IInvokeProvider)new ButtonAutomationPeer(b).GetPattern(PatternInterface.Invoke)).Invoke(); Layout();
            }
            void InvokeMenu(MenuItem item)
            {
                Assert.True(item.IsVisible); Assert.True(item.IsEnabled);
                ((IInvokeProvider)new MenuItemAutomationPeer(item).GetPattern(PatternInterface.Invoke)).Invoke(); Layout();
            }
            void PressTagKey(UIElement target, Key key)
            {
                Assert.Same(target, Keyboard.FocusedElement);
                var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target), Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                target.RaiseEvent(args); Assert.True(args.Handled);
            }
            void Save(Visual visual, string state, double w, double h)
            {
                Assert.True(w > 0 && h > 0);
                string dir = Path.Combine(Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY")!, "tag-form-renders"); Directory.CreateDirectory(dir);
                var bitmap = new RenderTargetBitmap((int)Math.Ceiling(w * dpi / 96d), (int)Math.Ceiling(h * dpi / 96d), dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(visual);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(dir, $"{theme}-{width}-{dpi}-{state}.png")); encoder.Save(file);
            }
            void Render(string state)
            {
                var snapshot = new NotebookWorkspaceView { DataContext = vm };
                VisualTreeHelper.SetRootDpi(snapshot, new(dpi / 96d, dpi / 96d)); snapshot.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                try { snapshot.Measure(new Size(width, 900)); snapshot.Arrange(new Rect(0, 0, width, 900)); snapshot.UpdateLayout(); Save(snapshot, state, width, 900); }
                finally { snapshot.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }
            }
        });
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        { var child = VisualTreeHelper.GetChild(parent, i); if (child is T match) yield return match; foreach (var nested in Descendants<T>(child)) yield return nested; }
    }
}
