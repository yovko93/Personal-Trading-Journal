using System.Collections.Immutable;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
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

public sealed class NotebookLinkViewTests
{
    private const string Host = "PTJ_NOTEBOOK_LINK_HOST";
    [Theory]
    [InlineData("Light", 1280, 96)] [InlineData("Dark", 1280, 96)]
    [InlineData("Light", 480, 240)] [InlineData("Dark", 480, 240)]
    public async Task LinkedItemsPickerAndHistoryRemainAccessible(string theme, int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            string expected = string.Create(CultureInfo.InvariantCulture,
                $"{typeof(NotebookLinkViewTests).FullName}.{nameof(LinkedItemsPickerAndHistoryRemainAccessible)}(theme: \"{theme}\", width: {width}, dpi: {dpi})");
            await IsolatedTestProcess.RunSuiteAsync(typeof(NotebookLinkViewTests), "notebook-links", Host,
                TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30),
                testCaseFilter: IsolatedTestProcess.ExactDisplayNameFilter(expected), expectedTestName: expected); return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            var app = System.Windows.Application.Current ?? new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var store = new NotebookFake(); var setup = Guid.NewGuid(); var trade = Guid.NewGuid(); var missing = Guid.NewGuid();
            var note = NotebookFake.Note("NQ — Research with linked records") with { Links =
                [new(NotebookTargetKind.Trade, trade, "NQ"), new(NotebookTargetKind.TradingSetup, setup, "London session liquidity sweep — patient confirmation and retest checklist"),
                 new(NotebookTargetKind.TradingSetup, missing, "Unavailable historical setup — retained reference")] };
            store.Notes.Add(note);
            store.OnResolve = (links, _) => Task.FromResult<ImmutableArray<NotebookResolvedLink>>([.. links.Select(l => new NotebookResolvedLink(l,
                l.TargetId == missing ? NotebookTargetAvailability.Unavailable : NotebookTargetAvailability.Available,
                l.Kind == NotebookTargetKind.Trade ? $"NQ · Research account — evaluation and execution review · 10/20/2026 8:42 AM NY · {trade}" : l.LastKnownLabel,
                l.TargetId == setup))]);
            store.OnTargets = (q, _) => Task.FromResult(new NotebookTargetPage([
                new(NotebookTargetKind.TradingSetup, setup, note.Links[1].LastKnownLabel, $"Trading Setup · Inactive · {setup}"),
                new(NotebookTargetKind.TradingSetup, Guid.NewGuid(), "London session — alternate timeframe and risk context", "Trading Setup · Active")], 2, null));
            using var vm = new NotebookWorkspaceViewModel(store, new FakeDialogService(), new NotebookChoices { Choice = NotebookLeaveChoice.Save }, new NotebookJsonCodec());
            var view = new NotebookWorkspaceView { DataContext = vm };
            view.Resources.MergedDictionaries.Add(CalendarViewLayoutTests.SharedThemeResources(theme));
            VisualTreeHelper.SetRootDpi(view, new(dpi / 96d, dpi / 96d));
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            try
            {
                Pump(vm.ActivateAsync()); vm.SelectedNote = vm.Notes[0]; Pump(vm.LoadTask); Pump(vm.EditModeCommand.ExecuteAsync(null)); Layout();
                var scroll = (ScrollViewer)view.FindName("ContentScroll"); scroll.ScrollToBottom(); Layout(); Render("linked-items");
                Assert.Equal(3, vm.LinkedItems.Count); Assert.Contains(vm.LinkedItems, x => !x.IsAvailable);
                Assert.Equal("Linked items", AutomationProperties.GetName((StackPanel)((ItemsControl)view.FindName("LinkedItemList")).Parent));
                var editor = (RichTextBox)view.FindName("Preview"); var document = editor.Document; long revision = vm.EditorRevision;
                editor.CaretPosition = editor.Document.ContentEnd; editor.CaretPosition.InsertTextInRun("New research"); Layout();
                Assert.True(vm.IsDirty);
                Pump(vm.RemoveLinkedItemCommand.ExecuteAsync(vm.LinkedItems.Single(x => !x.IsAvailable))); Layout();
                Assert.Same(document, editor.Document); Assert.Equal(revision, vm.EditorRevision); Assert.Single(vm.DraftDocument.ImageNodes);
                var undo = (Button)view.FindName("UndoButton"); Assert.True(undo.IsEnabled); undo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout();
                Assert.DoesNotContain("New research", vm.DraftDocument.PlainText); Assert.Single(vm.DraftDocument.ImageNodes);
                var redo = (Button)view.FindName("RedoButton"); Assert.True(redo.IsEnabled); redo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout();
                Assert.Contains("New research", vm.DraftDocument.PlainText); Assert.Single(vm.DraftDocument.ImageNodes); Assert.Equal(2, vm.Note!.Links.Length);
                vm.LinkTargetKind = NotebookTargetKind.TradingSetup; vm.LinkPickerOpen = true; Pump(vm.LinkSearchTask); Layout(); scroll.ScrollToBottom(); Layout();
                vm.SelectedLinkTarget = vm.LinkTargets[0]; Assert.True(vm.SelectedLinkTarget.IsLinked); Assert.False(vm.AddLinkCommand.CanExecute(null));
                Render("picker");
                foreach (var button in Descendants<Button>(view).Where(x => x.IsVisible))
                { var origin = button.TranslatePoint(new(0, 0), view); Assert.InRange(origin.X, -1, width); Assert.InRange(origin.X + button.ActualWidth, 0, width + 1); Assert.True(button.Focusable); }
            }
            finally { view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); Pump(vm.DeactivationTask); SynchronizationContext.SetSynchronizationContext(previous); }
            void Layout() { view.Measure(new(width, 900)); view.Arrange(new Rect(0, 0, width, 900)); view.UpdateLayout(); }
            void Render(string state)
            {
                string directory = Path.Combine(Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY") ?? AppContext.BaseDirectory, "m229a-renders"); Directory.CreateDirectory(directory);
                var bitmap = new RenderTargetBitmap(width * dpi / 96, 900 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(view);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var output = File.Create(Path.Combine(directory, $"{theme}-{width}-{dpi}-{state}.png")); encoder.Save(output);
            }
        });
    }
    private static void Pump(Task task)
    {
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            _ = task.ContinueWith(_ => Dispatcher.CurrentDispatcher.BeginInvoke(() => frame.Continue = false), CancellationToken.None,
                TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext()); Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        { var child = VisualTreeHelper.GetChild(parent, i); if (child is T found) yield return found; foreach (var item in Descendants<T>(child)) yield return item; }
    }
}
