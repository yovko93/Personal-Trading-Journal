using System.Globalization;
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
public sealed class NotebookFolderActionsViewTests
{
    private const string Host = "PTJ_NOTEBOOK_FOLDER_ACTIONS_HOST";
    [Theory]
    [InlineData("Light", 1280, 96)]
    [InlineData("Dark", 480, 240)]
    public async Task FolderHeaderActionsRenameAndRetainErrors(string theme, int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            string expected = string.Create(CultureInfo.InvariantCulture,
                $"{typeof(NotebookFolderActionsViewTests).FullName}.{nameof(FolderHeaderActionsRenameAndRetainErrors)}(theme: \"{theme}\", width: {width}, dpi: {dpi})");
            await IsolatedTestProcess.RunSuiteAsync(typeof(NotebookFolderActionsViewTests), "notebook-folder-actions", Host,
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
            var window = new Window { Content = view, SizeToContent = SizeToContent.WidthAndHeight, ShowActivated = false, ShowInTaskbar = false };
            try
            {
                window.Show(); Pump(vm.ActivateAsync()); vm.Panel = 1; Layout();
                var actions = (StackPanel)view.FindName("FolderActions");
                Assert.Equal(Visibility.Collapsed, actions.Visibility);
                vm.Scope = vm.Scopes.Single(s => s.FolderId == folder.Id); Pump(vm.LoadTask); Layout();
                var button = (Button)view.FindName("RenameFolderButton");
                var delete = (Button)view.FindName("DeleteFolderButton");
                var input = (TextBox)view.FindName("RenameFolderName");
                Assert.Equal(Visibility.Visible, actions.Visibility); Assert.True(button.IsVisible); Assert.True(delete.IsVisible);
                Assert.Equal("Rename folder", AutomationProperties.GetName(button)); Assert.Equal("Delete folder", AutomationProperties.GetName(delete));
                Assert.NotNull(button.ToolTip); Assert.NotNull(delete.ToolTip);
                Assert.Equal(folder.Name, ((TextBlock)view.FindName("FolderHeading")).Text); Render("long-heading");
                UiKeyboardProbe.Space(button); Layout(); Assert.False(button.IsPressed);
                Assert.True(vm.IsFolderRenameOpen); Assert.Same(input, Keyboard.FocusedElement); Assert.Equal(folder.Name, input.Text);
                Render("rename");
                input.Text = "Do not save"; PressKey(Key.Escape); Layout();
                Assert.False(vm.IsFolderRenameOpen); Assert.Same(button, Keyboard.FocusedElement); Assert.Equal(0, data.Writes);
                UiKeyboardProbe.Space(button); Layout(); Assert.Same(input, Keyboard.FocusedElement);
                input.Text = " "; PressKey(Key.Enter); Pump(vm.RenameFolderCommand.ExecutionTask!); Layout();
                Assert.Equal(0, data.Writes); Assert.True(vm.IsFolderRenameOpen); Assert.Equal(" ", input.Text);
                Assert.Equal("Enter a folder name of 1–128 characters.", vm.FolderActionMessage); Render("validation");
                input.Text = "Renamed research"; PressKey(Key.Enter); Pump(vm.RenameFolderCommand.ExecutionTask!); Layout();
                Assert.Equal(1, data.Writes); Assert.False(vm.IsFolderRenameOpen); Assert.Equal("Renamed research", vm.ScopeName);
                Assert.Same(vm.Scope, ((ListBox)view.FindName("FolderScopeList")).SelectedItem);
                UiKeyboardProbe.Space(delete); Pump(vm.DeleteFolderCommand.ExecutionTask!); Layout();
                Assert.Contains("Renamed research", dialogs.ConfirmationRequest!.Message); Assert.Equal(1, data.Writes);
                foreach (var scope in vm.Scopes.Where(s => !s.FolderId.HasValue).ToArray())
                { vm.Scope = scope; Pump(vm.LoadTask); Layout(); Assert.Equal(Visibility.Collapsed, actions.Visibility); }
                Assert.False(app.Dispatcher.HasShutdownStarted);
            }
            finally { window.Close(); vm.Deactivate(); Pump(vm.DeactivationTask); app.Shutdown(); SynchronizationContext.SetSynchronizationContext(prior); }
            void Layout() { view.UpdateLayout(); Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); }
            void PressKey(Key key)
            {
                Assert.Same(view.FindName("RenameFolderName"), Keyboard.FocusedElement);
                var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(view), Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                ((TextBox)view.FindName("RenameFolderName")).RaiseEvent(args); Assert.True(args.Handled);
            }
            void Render(string state)
            {
                var snapshot = new NotebookWorkspaceView { DataContext = vm };
                VisualTreeHelper.SetRootDpi(snapshot, new(dpi / 96d, dpi / 96d));
                snapshot.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                try
                {
                    snapshot.Measure(new Size(width, 900)); snapshot.Arrange(new Rect(0, 0, width, 900)); snapshot.UpdateLayout();
                    string directory = Path.Combine(Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY")!, "folder-action-renders"); Directory.CreateDirectory(directory);
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
