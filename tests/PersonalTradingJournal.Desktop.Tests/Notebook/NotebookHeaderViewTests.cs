using System.Globalization;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;
using PersonalTradingJournal.Desktop.Views.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

[Collection(ExclusiveWpfCollection.Name)]
[Trait(ExclusiveWpfCollection.Trait, ExclusiveWpfCollection.Value)]
public sealed class NotebookHeaderViewTests
{
    private const string Host = "PTJ_NOTEBOOK_HEADER_HOST";
    [Theory]
    [InlineData("Light", 1280, 96)]
    [InlineData("Dark", 480, 240)]
    public async Task SplitCreationMenusRefreshAndContextualCancellation(string theme, int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            string expected = string.Create(CultureInfo.InvariantCulture,
                $"{typeof(NotebookHeaderViewTests).FullName}.{nameof(SplitCreationMenusRefreshAndContextualCancellation)}(theme: \"{theme}\", width: {width}, dpi: {dpi})");
            await IsolatedTestProcess.RunSuiteAsync(typeof(NotebookHeaderViewTests), "notebook-header", Host,
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
            var data = new NotebookFake(); var folder = new PersonalTradingJournal.Application.Notebook.NotebookNamedItem(Guid.NewGuid(), "Long folder name for market research and trading observations", 1); data.Folders.Add(folder); var dialogs = new FakeDialogService(); using var vm = new NotebookWorkspaceViewModel(data, dialogs);
            var view = new NotebookWorkspaceView { DataContext = vm, Width = width, Height = 900 };
            var outside = new Button { Content = "Other shell action", Height = 34 }; var host = new DockPanel(); DockPanel.SetDock(outside, Dock.Top); host.Children.Add(outside); host.Children.Add(view);
            var window = new Window { Content = host, SizeToContent = SizeToContent.WidthAndHeight, ShowActivated = false, ShowInTaskbar = false };
            try
            {
                window.Show(); Pump(vm.ActivateAsync()); vm.Panel = 0; Layout();
                var arrow = (Button)view.FindName("NewNoteMenuButton");
                var main = (Button)view.FindName("NewNoteButton");
                var active = (DockPanel)view.FindName("ActiveNotebookOperation");
                Assert.Equal("New blank note", AutomationProperties.GetName(main)); Assert.Equal("New note templates", AutomationProperties.GetName(arrow));
                Assert.Null(view.FindName("TemplatePicker")); Assert.Null(view.FindName("CreateTemplateButton"));
                Assert.Equal(Visibility.Collapsed, active.Visibility); Render("header");
                UiKeyboardProbe.Space(arrow); Layout();
                var menu = arrow.ContextMenu!; Assert.True(menu.IsOpen); Assert.Equal(5, menu.Items.Count);
                Assert.Equal(vm.Templates, menu.Items.Cast<NotebookTemplateDefinition>());
                RenderMenu(menu, "templates"); PressMenuKey(menu, Key.Escape); Layout();
                Assert.False(menu.IsOpen); Assert.Equal(0, data.Writes);
                foreach (var definition in vm.Templates)
                {
                    UiKeyboardProbe.Space(arrow); Layout();
                    var item = (MenuItem)menu.ItemContainerGenerator.ContainerFromItem(definition);
                    Assert.True(item.IsEnabled); Assert.Equal(definition.Name, AutomationProperties.GetName(item));
                    Assert.True(item.Focus()); Assert.Same(item, Keyboard.FocusedElement);
                    var before = vm.CreateTemplateCommand.ExecutionTask; int writes = data.Writes;
                    PressMenuKey(item, Key.Enter); Layout();
                    Assert.NotSame(before, vm.CreateTemplateCommand.ExecutionTask); Pump(vm.CreateTemplateCommand.ExecutionTask!); Layout();
                    Assert.False(menu.IsOpen); Assert.Equal(writes + 1, data.Writes); Assert.Equal(1, vm.Note!.Version);
                    Assert.Equal(NotebookTemplates.Create(definition.Id).PlainText, vm.Note.Document.PlainText);
                }
                Pump(UiKeyboardProbe.SpaceCommand(main)); Layout(); Assert.Equal("Untitled", vm.Note!.Title); Assert.Empty(vm.Note.Document.Blocks);
                vm.Panel = 0; Layout(); var overflow = (Button)view.FindName("NotebookOverflow");
                UiKeyboardProbe.Space(overflow); Layout(); var other = overflow.ContextMenu!; Assert.True(other.IsOpen);
                Assert.Equal(new[] { NotebookScope.Unfiled, NotebookScope.Archived, NotebookScope.Trash }, other.Items.OfType<NotebookScopeOption>().Select(s => s.Scope));
                var refresh = Assert.Single(other.Items.OfType<MenuItem>()); Assert.Equal("Refresh Notebook", AutomationProperties.GetName(refresh));
                Assert.Same(vm.RefreshCommand, refresh.Command); RenderMenu(other, "overflow");
                Assert.True(refresh.Focus()); PressMenuKey(refresh, Key.Enter); Layout(); Pump(vm.RefreshCommand.ExecutionTask!); Layout(); Assert.False(other.IsOpen);
                var search = (TextBox)view.FindName("SearchBox");
                Assert.Same(vm.SearchCommand, search.InputBindings.OfType<KeyBinding>().Single(k => k.Key == Key.Enter).Command);
                var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                data.OnList = async (query, ct) => { entered.SetResult(); await Task.Delay(Timeout.Infinite, ct); return data.Page(query); };
                var loading = vm.RefreshCommand.ExecuteAsync(null); Pump(entered.Task); Layout();
                Assert.Equal(Visibility.Visible, active.Visibility); Render("busy");
                Pump(UiKeyboardProbe.SpaceCommand((Button)view.FindName("CancelNotebookOperation"))); Pump(loading); Layout();
                Assert.False(vm.IsBusy); Assert.Equal(Visibility.Collapsed, active.Visibility); Assert.True(vm.CanCreateNote);
                Assert.Contains("completed change may already be saved", vm.Message); data.OnList = null;
                var writeEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var release = new TaskCompletionSource<NotebookWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                data.OnWrite = (_, _) => { writeEntered.SetResult(); return release.Task; };
                var pending = UiKeyboardProbe.SpaceCommand(main); Pump(writeEntered.Task);
                UiKeyboardProbe.Ready(outside); Assert.Same(outside, Keyboard.FocusedElement);
                var saved = NotebookFake.Note("Untitled") with { Document = NotebookDocument.Empty }; data.Notes.Add(saved);
                release.SetResult(new(NotebookWriteStatus.Success, saved)); Pump(pending); Layout();
                Assert.Same(outside, Keyboard.FocusedElement); Assert.Equal(saved.Id, vm.Note!.Id); data.OnWrite = null;
                Assert.False(app.Dispatcher.HasShutdownStarted);
            }
            finally { window.Close(); vm.Deactivate(); Pump(vm.DeactivationTask); app.Shutdown(); SynchronizationContext.SetSynchronizationContext(prior); }
            void Layout() { view.UpdateLayout(); Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); }
            void PressMenuKey(UIElement target, Key key)
            {
                target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target), Environment.TickCount, key) { RoutedEvent = Keyboard.KeyDownEvent });
            }
            void RenderMenu(ContextMenu menu, string state)
            {
                menu.UpdateLayout(); Save(menu, state, menu.ActualWidth, menu.ActualHeight);
            }
            void Save(Visual visual, string state, double w, double h)
            {
                Assert.True(w > 0 && h > 0);
                string directory = Path.Combine(Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY")!, "header-renders"); Directory.CreateDirectory(directory);
                var bitmap = new RenderTargetBitmap((int)Math.Ceiling(w * dpi / 96d), (int)Math.Ceiling(h * dpi / 96d), dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(visual);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(directory, $"{theme}-{width}-{dpi}-{state}.png")); encoder.Save(file);
            }
            void Render(string state)
            {
                var snapshot = new NotebookWorkspaceView { DataContext = vm };
                VisualTreeHelper.SetRootDpi(snapshot, new(dpi / 96d, dpi / 96d));
                snapshot.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                try
                {
                    snapshot.Measure(new Size(width, 900)); snapshot.Arrange(new Rect(0, 0, width, 900)); snapshot.UpdateLayout();
                    string directory = Path.Combine(Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY")!, "header-renders"); Directory.CreateDirectory(directory);
                    var bitmap = new RenderTargetBitmap(width * dpi / 96, 900 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(snapshot);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var file = File.Create(Path.Combine(directory, $"{theme}-{width}-{dpi}-{state}.png")); encoder.Save(file);
                }
                finally { snapshot.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }
            }
        });
    }
    private static void Pump(Task task)
    {
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            _ = task.ContinueWith(_ => Dispatcher.CurrentDispatcher.BeginInvoke(() => frame.Continue = false), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
            Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }
}
