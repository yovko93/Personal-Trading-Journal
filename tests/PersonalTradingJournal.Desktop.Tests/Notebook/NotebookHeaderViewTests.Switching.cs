using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;
using PersonalTradingJournal.Desktop.Views.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed partial class NotebookHeaderViewTests
{
    [Theory]
    [InlineData("Light", 1280, 96)]
    [InlineData("Dark", 480, 240)]
    public async Task DelayedNoteSwitchRetainsReadOnlyDetailAndWorkspaceGeometry(string theme, int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            string expected = string.Create(CultureInfo.InvariantCulture,
                $"{typeof(NotebookHeaderViewTests).FullName}.{nameof(DelayedNoteSwitchRetainsReadOnlyDetailAndWorkspaceGeometry)}(theme: \"{theme}\", width: {width}, dpi: {dpi})");
            await IsolatedTestProcess.RunSuiteAsync(typeof(NotebookHeaderViewTests), "notebook-switch", Host,
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
            var tag = new NotebookNamedItem(Guid.NewGuid(), "Research", 1); data.Tags.Add(tag);
            var image = NotebookNode.Image(Guid.NewGuid(), Guid.NewGuid(), "A retained chart", "Synthetic chart");
            var a = NotebookFake.Note("A — displayed original") with { Document = new NotebookDocument([
                .. NotebookEditingTests.Text("A content remains visible while the target is loading.").Blocks, image]), TagIds = [tag.Id],
                Links = [new(NotebookTargetKind.Trade, Guid.NewGuid(), "A trade")] };
            var b = NotebookFake.Note("B — requested target") with { Document = NotebookEditingTests.Text("B independent target content."),
                Links = [new(NotebookTargetKind.TradingSetup, Guid.NewGuid(), "B setup")] };
            data.Notes.AddRange([a, b]);
            for (int i = 0; i < 35; i++) data.Notes.Add(NotebookFake.Note($"Other research {i:00}"));
            using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService(), new NotebookChoices(), new NotebookJsonCodec(),
                time: new NotebookClock(), imagePresentation: new NotebookImageEditorTests.Presentation(image.ImageId!.Value));
            var view = new NotebookWorkspaceView { DataContext = vm }; VisualTreeHelper.SetRootDpi(view, new(dpi / 96d, dpi / 96d));
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<NotebookLoadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task pending = Task.CompletedTask; int noteResets = 0, catalogResets = 0, editorChanges = 0;
            try
            {
                Pump(vm.ActivateAsync()); view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent)); Layout();
                vm.SelectedNote = vm.Notes.Single(n => n.Id == a.Id); Pump(vm.LoadTask); vm.Panel = 1; Layout();
                var list = (ListBox)view.FindName("NoteList");
                var listScroll = SwitchDescendants<ScrollViewer>(list).First(); listScroll.ScrollToVerticalOffset(8); Layout();
                double offset = listScroll.VerticalOffset; var rows = vm.Notes.ToArray(); var scopes = vm.Scopes.ToArray();
                var originalDocument = ((RichTextBox)view.FindName("Preview")).Document;
                var listBounds = list.TransformToAncestor(view).TransformBounds(new Rect(list.RenderSize));
                vm.Notes.CollectionChanged += (_, e) => { if (e.Action == NotifyCollectionChangedAction.Reset) noteResets++; };
                vm.Tags.CollectionChanged += (_, e) => { if (e.Action == NotifyCollectionChangedAction.Reset) catalogResets++; };
                vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.EditorRevision)) editorChanges++; };
                vm.Panel = 2; Layout(); Render("01-before");
                int queries = data.Queries.Count;
                data.OnLoad = (id, _) => id == b.Id ? Hold() : Task.FromResult(new NotebookLoadResult(NotebookLoadStatus.NotFound));
                Task<NotebookLoadResult> Hold() { entered.TrySetResult(); return release.Task; }
                list.SetCurrentValue(Selector.SelectedItemProperty, vm.Notes.Single(n => n.Id == b.Id)); pending = vm.LoadTask;
                Pump(entered.Task.WaitAsync(TimeSpan.FromSeconds(10))); Layout(); Render("02-pending");
                bool retained = ReferenceEquals(originalDocument, ((RichTextBox)view.FindName("Preview")).Document);
                bool oldOwner = vm.Note?.Id == a.Id && vm.DraftTitle == a.Title && vm.DraftDocument.ImageNodes.Single().ImageId == image.ImageId;
                bool readOnly = !vm.CanEditDraft && !vm.CanEditBody && !vm.PinCommand.CanExecute(null) && !vm.TrashCommand.CanExecute(null);
                bool listEnabled = list.IsEnabled; string empty = vm.EmptyMessage;
                var busyVisibility = ((FrameworkElement)view.FindName("ActiveNotebookOperation")).Visibility;
                int pendingEditorChanges = editorChanges;
                release.SetResult(new(NotebookLoadStatus.Success, b)); Pump(pending.WaitAsync(TimeSpan.FromSeconds(10))); Layout(); Render("03-complete");
                File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY")!, $"{theme}-transition.json"),
                    System.Text.Json.JsonSerializer.Serialize(new { retained, oldOwner, readOnly, listEnabled, empty, busyVisibility, pendingEditorChanges,
                        editorChanges, noteResets, catalogResets, offset, finalOffset = listScroll.VerticalOffset, queries, finalQueries = data.Queries.Count }));
                Assert.True(retained, "Pending detail must retain the displayed document rather than replace it with a blank frame.");
                Assert.True(oldOwner); Assert.True(readOnly); Assert.True(listEnabled); Assert.Equal("", empty);
                Assert.Equal(Visibility.Collapsed, busyVisibility); Assert.Equal(0, pendingEditorChanges); Assert.Equal(1, editorChanges);
                Assert.Equal(0, noteResets); Assert.Equal(0, catalogResets); Assert.Equal(queries, data.Queries.Count);
                Assert.Equal(rows, vm.Notes); Assert.Equal(scopes, vm.Scopes); Assert.Equal(offset, listScroll.VerticalOffset);
                Assert.Equal(b.Id, vm.Note!.Id); Assert.Equal(b.Title, vm.DraftTitle); Assert.Equal(b.Document, vm.DraftDocument);
                Assert.Equal(b.Links.Single().TargetId, Assert.Single(vm.LinkedItems).Link.TargetId); Assert.Empty(vm.AssignedTags);
                Assert.True(vm.CanEditDraft); Assert.False(System.Windows.Input.ApplicationCommands.Undo.CanExecute(null, (RichTextBox)view.FindName("Preview")));
                vm.Panel = 1; Layout(); Assert.Equal(listBounds, list.TransformToAncestor(view).TransformBounds(new Rect(list.RenderSize)));
            }
            finally
            {
                release.TrySetResult(new(NotebookLoadStatus.Success, b)); Pump(pending.WaitAsync(TimeSpan.FromSeconds(10)));
                vm.Deactivate(); Pump(vm.DeactivationTask); view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                app.Shutdown(); SynchronizationContext.SetSynchronizationContext(previous);
            }
            void Layout() { view.Measure(new(width, 900)); view.Arrange(new Rect(0, 0, width, 900)); view.UpdateLayout(); Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); Assert.Null(PresentationSource.FromVisual(view)); }
            void Render(string state)
            {
                var bitmap = new RenderTargetBitmap(width * dpi / 96, 900 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(view);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var output = File.Create(Path.Combine(Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY")!, $"{theme}-{width}-{dpi}-{state}.png")); encoder.Save(output);
            }
        });
    }
    private static IEnumerable<T> SwitchDescendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T item) yield return item;
            foreach (var nested in SwitchDescendants<T>(child)) yield return nested;
        }
    }
}
