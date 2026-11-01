using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;
using PersonalTradingJournal.Desktop.Views.Notebook;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed partial class NotebookHeaderViewTests
{
    [Theory]
    [InlineData("Light", 1280, 96)]
    [InlineData("Dark", 480, 240)]
    public async Task ScopeItemSelectionKeepsRequestQueryHeadingAndRowsConsistent(string theme, int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            string expected = string.Create(CultureInfo.InvariantCulture,
                $"{typeof(NotebookHeaderViewTests).FullName}.{nameof(ScopeItemSelectionKeepsRequestQueryHeadingAndRowsConsistent)}(theme: \"{theme}\", width: {width}, dpi: {dpi})");
            await IsolatedTestProcess.RunSuiteAsync(typeof(NotebookHeaderViewTests), "notebook-scopes", Host,
                TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30),
                testCaseFilter: IsolatedTestProcess.ExactDisplayNameFilter(expected), expectedTestName: expected);
            return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown,
                Resources = CalendarViewLayoutTests.SharedThemeResources(theme) };
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var data = new NotebookFake();
            var folder = new NotebookNamedItem(Guid.NewGuid(), "Real folder", 1); data.Folders.Add(folder);
            var active = NotebookFake.Note("Active unpinned");
            var pinned = NotebookFake.Note("Active pinned") with { IsPinned = true };
            var trash = NotebookFake.Note("Only trashed", state: NotebookNoteState.Trashed);
            var filed = NotebookFake.Note("Filed active", folder.Id);
            data.Notes.AddRange([active, pinned, trash, filed]);
            var choices = new NotebookChoices();
            using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService(), choices);
            var view = new NotebookWorkspaceView { DataContext = vm };
            VisualTreeHelper.SetRootDpi(view, new(dpi / 96d, dpi / 96d));
            var list = (ListBox)view.FindName("ScopeList");
            var log = new List<string>();
            vm.PropertyChanged += (_, e) => { if (e.PropertyName is nameof(vm.Scope) or nameof(vm.ScopeName)) Observe(e.PropertyName); };
            list.SelectionChanged += (_, e) => Observe("selection: added=" + string.Join(",", e.AddedItems.Cast<object>()));
            try
            {
                Pump(vm.ActivateAsync()); view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent)); Layout();
                Assert.Equal(new[] { NotebookScope.AllNotes, NotebookScope.Pinned, NotebookScope.Trash }, list.Items.Cast<NotebookScopeOption>().Select(s => s.Scope));
                int queries = data.Queries.Count;
                Select(NotebookScope.Trash); Pump(vm.LoadTask.WaitAsync(TimeSpan.FromSeconds(10))); Layout();
                Assert.Equal(queries + 1, data.Queries.Count);
                Assert.Equal(NotebookScope.Trash, data.Queries.Last().Scope);
                Assert.Equal(NotebookScope.Trash, vm.Scope!.Scope);
                Assert.Equal("Trash", vm.ScopeName);
                Assert.Equal(NotebookScope.Trash, Assert.IsType<NotebookScopeOption>(list.SelectedItem).Scope);
                Assert.Equal(trash.Id, Assert.Single(vm.Notes).Id);
                Complete(NotebookScope.Pinned, [pinned.Id]);
                Complete(NotebookScope.Trash, [trash.Id]);
                Complete(NotebookScope.Folder, [filed.Id]);
                Complete(NotebookScope.Trash, [trash.Id]);
                Complete(NotebookScope.AllNotes, [active.Id, pinned.Id, filed.Id]);
                Complete(NotebookScope.Pinned, [pinned.Id]);

                vm.SelectedNote = Assert.Single(vm.Notes); Pump(vm.LoadTask); Layout();
                vm.DraftTitle = "Saved before leaving"; choices.Choice = NotebookLeaveChoice.Save;
                Complete(NotebookScope.Trash, [trash.Id]);
                Assert.Equal("Saved before leaving", data.Notes.Single(n => n.Id == pinned.Id).Title);
                Complete(NotebookScope.Pinned, [pinned.Id]);
                vm.SelectedNote = Assert.Single(vm.Notes); Pump(vm.LoadTask); Layout();
                vm.DraftTitle = "Retained cancelled draft"; choices.Choice = NotebookLeaveChoice.Cancel;
                queries = data.Queries.Count; Select(NotebookScope.Trash); Pump(vm.LoadTask); Layout();
                AssertApplied(NotebookScope.Pinned, [pinned.Id]); Assert.Equal(queries, data.Queries.Count);
                Assert.Equal("Retained cancelled draft", vm.DraftTitle); Assert.True(vm.IsDirty);
                choices.Choice = NotebookLeaveChoice.Save; data.NextWrite = NotebookWriteStatus.StorageFailure;
                Select(NotebookScope.Trash); Pump(vm.LoadTask); Layout();
                AssertApplied(NotebookScope.Pinned, [pinned.Id]); Assert.Equal(queries, data.Queries.Count);
                Assert.Equal("Retained cancelled draft", vm.DraftTitle); Assert.True(vm.IsDirty);
                data.NextWrite = NotebookWriteStatus.Success; choices.Choice = NotebookLeaveChoice.Discard;
                Complete(NotebookScope.AllNotes, [active.Id, pinned.Id, filed.Id]);

                var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var delayed = new TaskCompletionSource<NotebookPage>(TaskCreationOptions.RunContinuationsAsynchronously);
                data.OnList = (query, _) => query.Scope == NotebookScope.Trash ? Hold() : Task.FromResult(data.Page(query));
                Task<NotebookPage> Hold() { entered.TrySetResult(); return delayed.Task; }
                vm.Panel = 0; Layout();
                var trashItem = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(list.Items.Cast<NotebookScopeOption>().Single(s => s.Scope == NotebookScope.Trash));
                var trashPoint = trashItem.TranslatePoint(new Point(trashItem.ActualWidth / 2, trashItem.ActualHeight / 2), view);
                Select(NotebookScope.Trash); var old = vm.LoadTask;
                try
                {
                    Pump(entered.Task.WaitAsync(TimeSpan.FromSeconds(10))); Layout();
                    // Unattached offscreen roots have no native IsVisible/hit-test surface.
                    // Compare actual arranged item rectangles at the original press position.
                    var hitScope = Assert.Single(list.Items.Cast<NotebookScopeOption>(), option =>
                    {
                        var item = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(option);
                        return item.TransformToAncestor(view).TransformBounds(new Rect(item.RenderSize)).Contains(trashPoint);
                    });
                    Observe($"held-read hit at original Trash center {trashPoint}: {hitScope.Scope}");
                    Assert.Equal(NotebookScope.Trash, hitScope.Scope);
                    Assert.Equal(Visibility.Visible, ((DockPanel)view.FindName("ActiveNotebookOperation")).Visibility);
                    Assert.Same(vm.CancelCommand, ((Button)view.FindName("CancelNotebookOperation")).Command);
                    Render("scope-busy");
                    Complete(NotebookScope.Pinned, [pinned.Id]);
                    delayed.SetResult(data.Page(new(NotebookScope.Trash))); Pump(old.WaitAsync(TimeSpan.FromSeconds(10))); Layout();
                    AssertApplied(NotebookScope.Pinned, [pinned.Id]);
                }
                finally { delayed.TrySetResult(data.Page(new(NotebookScope.Trash))); Pump(old.WaitAsync(TimeSpan.FromSeconds(10))); data.OnList = null; }
                data.Notes.Remove(trash); Complete(NotebookScope.Trash, []);
                Assert.Equal(0, vm.Scope!.Count);
                Render("empty-trash");
            }
            finally
            {
                var directory = Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY")!;
                File.WriteAllLines(Path.Combine(directory, $"{theme}-{width}-{dpi}-scope-sequence.log"), log);
                vm.Deactivate(); Pump(vm.DeactivationTask);
                view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); app.Shutdown(); SynchronizationContext.SetSynchronizationContext(previous);
            }
            void Observe(string phase) => log.Add($"{phase}: accepted={vm.Scope?.Scope}, selected={(list.SelectedItem as NotebookScopeOption)?.Scope}, heading={vm.ScopeName}, queries={string.Join(',', data.Queries.Select(q => q.Scope))}");
            void Layout() { view.Measure(new(width, 900)); view.Arrange(new Rect(0, 0, width, 900)); view.UpdateLayout(); Assert.Null(PresentationSource.FromVisual(view)); }
            void Complete(NotebookScope scope, Guid[] expected)
            {
                Select(scope); Pump(vm.LoadTask.WaitAsync(TimeSpan.FromSeconds(10))); Layout(); AssertApplied(scope, expected);
                Assert.Equal(scope, data.Queries.Last().Scope);
            }
            void AssertApplied(NotebookScope scope, Guid[] expected)
            {
                Assert.Equal(scope, vm.Scope!.Scope);
                Assert.Equal(scope == NotebookScope.Folder ? folder.Name : scope == NotebookScope.AllNotes ? "All Notes" : scope.ToString(), vm.ScopeName);
                Assert.Equal(vm.ScopeName, ((TextBlock)view.FindName("FolderHeading")).Text);
                var primary = scope == NotebookScope.Folder ? (ListBox)view.FindName("FolderScopeList") : list;
                var other = scope == NotebookScope.Folder ? list : (ListBox)view.FindName("FolderScopeList");
                Assert.Same(vm.Scope, primary.SelectedItem); Assert.Null(other.SelectedItem);
                Assert.Equal(expected.Order(), vm.Notes.Select(n => n.Id).Order()); Assert.True(vm.CanSelectNote);
            }
            void Select(NotebookScope scope)
            {
                vm.Panel = 0; Layout(); // Narrow navigation requires opening Organization before choosing a scope.
                var source = scope == NotebookScope.Folder ? (ListBox)view.FindName("FolderScopeList") : list;
                var option = source.Items.Cast<NotebookScopeOption>().Single(s => s.Scope == scope);
                Assert.IsType<ListBoxItem>(source.ItemContainerGenerator.ContainerFromItem(option));
                var owner = Assert.IsType<ListBoxAutomationPeer>(UIElementAutomationPeer.CreatePeerForElement(source));
                var peer = new ListBoxItemAutomationPeer(option, owner);
                Observe("request=" + scope);
                Assert.IsAssignableFrom<ISelectionItemProvider>(peer.GetPattern(PatternInterface.SelectionItem)).Select();
            }
            void Render(string state)
            {
                var bitmap = new RenderTargetBitmap(width * dpi / 96, 900 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(view);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY")!, $"{theme}-{width}-{dpi}-{state}.png")); encoder.Save(file);
            }
        });
    }

    [Fact]
    public async Task NativeTrashPressRetainsTargetWhileReadIsPending()
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            string expected = $"{typeof(NotebookHeaderViewTests).FullName}.{nameof(NativeTrashPressRetainsTargetWhileReadIsPending)}";
            await IsolatedTestProcess.RunSuiteAsync(typeof(NotebookHeaderViewTests), "notebook-native-scope", Host,
                TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30),
                testCaseFilter: IsolatedTestProcess.ExactDisplayNameFilter(expected), expectedTestName: expected);
            return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown,
                Resources = CalendarViewLayoutTests.SharedThemeResources("Light") };
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var data = new NotebookFake(); var folder = new NotebookNamedItem(Guid.NewGuid(), "Real folder", 1); data.Folders.Add(folder);
            var active = NotebookFake.Note("Active unpinned"); var pinned = NotebookFake.Note("Active pinned") with { IsPinned = true };
            var trash = NotebookFake.Note("Only trashed", state: NotebookNoteState.Trashed);
            data.Notes.AddRange([active, pinned, trash, NotebookFake.Note("Filed active", folder.Id)]);
            using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService());
            var view = new NotebookWorkspaceView { DataContext = vm, Width = 1280, Height = 900 };
            var window = new Window { Content = view, SizeToContent = SizeToContent.WidthAndHeight, ShowInTaskbar = false };
            var log = new List<string>(); bool pressed = false;
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var up = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Input(object sender, ProcessInputEventArgs e)
            {
                if (e.StagingItem.Input is MouseButtonEventArgs mouse && mouse.RoutedEvent == Mouse.MouseUpEvent)
                    up.TrySetResult();
            }
            try
            {
                window.Show(); Pump(vm.ActivateAsync()); vm.SelectedNote = vm.Notes.Single(n => n.Id == active.Id); Pump(vm.LoadTask);
                UiKeyboardProbe.Ready((TextBox)view.FindName("SearchBox")); Layout();
                var list = (ListBox)view.FindName("ScopeList");
                var target = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(list.Items.Cast<NotebookScopeOption>().Single(s => s.Scope == NotebookScope.Trash));
                Assert.True(target.IsVisible && target.IsEnabled);
                var point = target.PointToScreen(new Point(target.ActualWidth / 2, target.ActualHeight / 2));
                Assert.True(SetCursorPos((int)point.X, (int)point.Y)); Layout();
                var expected = ((HwndSource)PresentationSource.FromVisual(target)).Handle;
                var hit = WindowFromPoint(new NativePoint((int)point.X, (int)point.Y));
                log.Add($"Before press: point={point}; expected={DescribeWindow(expected)}; hit={DescribeWindow(hit)}; foreground={DescribeWindow(GetForegroundWindow())}; mouseOver={target.IsMouseOver}; left={Mouse.LeftButton}");
                Assert.Equal(expected, hit);
                // Use the established native menu readiness condition/deadline: SetCursorPos
                // alone does not mean WPF has processed the corresponding mouse-over update.
                var readyFrame = new DispatcherFrame(); var readyWatch = System.Diagnostics.Stopwatch.StartNew();
                var readyTimer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(10) };
                bool ready = false;
                void CheckReady(object? sender, EventArgs args)
                {
                    Assert.True(SetCursorPos((int)point.X, (int)point.Y));
                    ready = target.IsMouseOver && WindowFromPoint(new NativePoint((int)point.X, (int)point.Y)) == expected;
                    if (ready || readyWatch.Elapsed >= TimeSpan.FromSeconds(3)) readyFrame.Continue = false;
                }
                readyTimer.Tick += CheckReady; readyTimer.Start();
                try { Dispatcher.PushFrame(readyFrame); }
                finally { readyTimer.Stop(); readyTimer.Tick -= CheckReady; }
                log.Add($"Readiness: ready={ready}; elapsed={readyWatch.Elapsed}; expected={DescribeWindow(expected)}; hit={DescribeWindow(WindowFromPoint(new NativePoint((int)point.X, (int)point.Y)))}; mouseOver={target.IsMouseOver}");
                Assert.True(ready, "Trash must be mouse-over and own its screen point before input.");
                Assert.Equal(MouseButtonState.Released, Mouse.LeftButton);
                data.OnList = async (query, _) => { entered.TrySetResult(); await release.Task; return data.Page(query); };
                InputManager.Current.PostProcessInput += Input;
                SendMouse(0x0002); pressed = true;
                Pump(entered.Task.WaitAsync(TimeSpan.FromSeconds(10))); Layout();
                Assert.Equal(MouseButtonState.Pressed, Mouse.LeftButton);
                var local = view.PointFromScreen(point); var element = view.InputHitTest(local) as DependencyObject;
                while (element is not null && element is not ListBoxItem) element = VisualTreeHelper.GetParent(element);
                var underPointer = (element as ListBoxItem)?.DataContext as NotebookScopeOption;
                log.Add($"During held read: hitScope={underPointer?.Scope}; accepted={vm.Scope?.Scope}; capture={Mouse.Captured}; queries={string.Join(',', data.Queries.Select(q => q.Scope))}; targetPoint={target.PointToScreen(new Point(target.ActualWidth / 2, target.ActualHeight / 2))}");
                SendMouse(0x0004); pressed = false; Pump(up.Task.WaitAsync(TimeSpan.FromSeconds(3))); Layout();
                release.TrySetResult(); Pump(vm.CompletionTask.WaitAsync(TimeSpan.FromSeconds(10))); Layout();
                Assert.Equal(NotebookScope.Trash, underPointer?.Scope);
                Assert.Equal(NotebookScope.Trash, vm.Scope!.Scope); Assert.Equal("Trash", vm.ScopeName);
                Assert.Same(vm.Scope, list.SelectedItem); Assert.Equal(trash.Id, Assert.Single(vm.Notes).Id);
                Assert.Equal(new[] { NotebookScope.AllNotes, NotebookScope.Trash }, data.Queries.Select(q => q.Scope));
                Assert.Equal(MouseButtonState.Released, Mouse.LeftButton);
            }
            finally
            {
                // Cleanup is outside the assertions and cannot make a failed press pass.
                if (pressed) SendMouse(0x0004);
                release.TrySetResult(); Pump(vm.CompletionTask.WaitAsync(TimeSpan.FromSeconds(10)));
                InputManager.Current.PostProcessInput -= Input;
                File.WriteAllLines(Path.Combine(Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY")!, "native-scope-input.log"), log);
                window.Close(); vm.Deactivate(); Pump(vm.DeactivationTask); app.Shutdown(); SynchronizationContext.SetSynchronizationContext(previous);
            }
            void Layout() { view.UpdateLayout(); Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); }
        });
    }
}
