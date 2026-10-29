using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;
using PersonalTradingJournal.Desktop.Views.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed partial class NotebookHeaderViewTests
{
    [Theory]
    [InlineData("Light", 1280, 96)]
    [InlineData("Dark", 480, 240)]
    public async Task TagMenuRoutedToggleDismissalAndCapturedOwnership(string theme, int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            string expected = string.Create(CultureInfo.InvariantCulture,
                $"{typeof(NotebookHeaderViewTests).FullName}.{nameof(TagMenuRoutedToggleDismissalAndCapturedOwnership)}(theme: \"{theme}\", width: {width}, dpi: {dpi})");
            await IsolatedTestProcess.RunSuiteAsync(typeof(NotebookHeaderViewTests), "notebook-tag-menus", Host,
                TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30),
                testCaseFilter: IsolatedTestProcess.ExactDisplayNameFilter(expected), expectedTestName: expected);
            return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown,
                Resources = CalendarViewLayoutTests.SharedThemeResources(theme) };
            var prior = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var data = new NotebookFake(); data.Tags.AddRange([new(Guid.NewGuid(), "Alpha", 1), new(Guid.NewGuid(), "Beta", 1)]);
            data.Notes.Add(NotebookFake.Note());
            using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService { ConfirmationResult = true });
            var view = new NotebookWorkspaceView { DataContext = vm };
            var window = new Window { Content = view, Width = width, Height = 900, ShowInTaskbar = false };
            try
            {
                window.Show(); Pump(vm.ActivateAsync()); vm.Panel = 0; Layout();
                Button Trigger(string name) => SwitchDescendants<Button>((ItemsControl)view.FindName("TagFilters"))
                    .Single(b => AutomationProperties.GetName(b) == $"Manage global tag: {name}");
                var a = Trigger("Alpha"); var b = Trigger("Beta");
                Click(a); var first = a.ContextMenu!; Assert.True(first.IsOpen);
                Click(a); Assert.False(a.ContextMenu?.IsOpen == true); // Fails on the old unconditional reopen.
                Click(a); var menu = a.ContextMenu!; Assert.True(menu.IsOpen);
                // Exercise the popup callback's geometry/timestamp path without native pointer input.
                view.RememberOverflowDismissal(a, 42, new Point(a.ActualWidth / 2, a.ActualHeight / 2));
                menu.IsOpen = false; // Model MenuBase's automatic dismissal before button activation.
                a.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 42, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
                Click(a); Assert.False(a.ContextMenu?.IsOpen == true);
                a.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 43, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
                Click(a); Assert.True(a.ContextMenu!.IsOpen);
                Click(b); Assert.False(a.ContextMenu?.IsOpen == true); Assert.True(b.ContextMenu!.IsOpen);
                var oldDelete = (MenuItem)b.ContextMenu.Items[1];
                ((MenuItem)b.ContextMenu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Layout();
                Assert.True(vm.IsTagRenameOpen); Assert.Equal("Beta", vm.TagName);
                vm.TagName = "Beta renamed"; Pump(vm.RenameTagCommand.ExecuteAsync(null)); Layout();
                Assert.Equal(1, data.Writes); Assert.Contains(data.Tags, t => t.Name == "Beta renamed");
                Click(a); var current = a.ContextMenu!;
                var focusedItem = (MenuItem)current.Items[0];
                Assert.True(focusedItem.Focus(), $"Escape prerequisite: open={current.IsOpen}; itemVisible={focusedItem.IsVisible}; enabled={focusedItem.IsEnabled}; focusable={focusedItem.Focusable}; ownerActive={window.IsActive}; popupSource={PresentationSource.FromVisual(current) is not null}; menuSize={current.RenderSize}; focused={Keyboard.FocusedElement}");
                focusedItem.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(focusedItem), 44, Key.Escape) { RoutedEvent = Keyboard.KeyDownEvent }); Layout();
                Assert.False(current.IsOpen);
                oldDelete.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Pump(view.TagMenuTask); Assert.Equal(1, data.Writes);
                a.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, null, 45, Key.Apps) { RoutedEvent = Keyboard.PreviewKeyDownEvent }); Layout(); Assert.True(a.ContextMenu!.IsOpen);
                // Ordinary outside dismissal must not suppress a later unrelated click.
                view.RememberOverflowDismissal(a, 46, new Point(-1, -1));
                a.ContextMenu.IsOpen = false; a.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 46, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
                Click(a); Assert.True(a.ContextMenu.IsOpen);
                var stale = (MenuItem)a.ContextMenu.Items[1]; data.Tags.RemoveAll(t => t.Name == "Alpha");
                Pump(vm.RefreshCommand.ExecuteAsync(null)); Layout(); Assert.False(current.IsOpen); Assert.False(a.ContextMenu?.IsOpen == true);
                stale.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Pump(view.TagMenuTask); Assert.Equal(1, data.Writes);
                b = Trigger("Beta renamed"); Click(b); var delete = (MenuItem)b.ContextMenu!.Items[1];
                delete.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Pump(view.TagMenuTask); Layout();
                delete.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Pump(view.TagMenuTask);
                Assert.Equal(2, data.Writes); Assert.Empty(data.Tags); Assert.Empty(vm.SelectedTagFilters);
                Assert.All(data.Queries, q => Assert.Empty(q.TagIds));
                // Shared tracking must retain the note-menu's direct toggle path.
                vm.SelectedNote = Assert.Single(vm.Notes); Pump(vm.LoadTask); vm.Panel = 2; Layout();
                var note = (Button)view.FindName("NoteOverflow"); Click(note); Assert.True(note.ContextMenu.IsOpen);
                Click(note); Assert.False(note.ContextMenu.IsOpen); Assert.False(vm.IsEditing);
                view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                Assert.Null(b.ContextMenu); Assert.False(note.ContextMenu.IsOpen);
            }
            finally { window.Close(); vm.Deactivate(); Pump(vm.DeactivationTask); app.Shutdown(); SynchronizationContext.SetSynchronizationContext(prior); }
            void Layout() { view.Measure(new Size(width, 900)); view.Arrange(new Rect(0, 0, width, 900)); view.UpdateLayout(); Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); }
            void Click(Button button) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout(); }
        });
    }
}
