using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;
using PersonalTradingJournal.Desktop.Views.Notebook;
using PersonalTradingJournal.Infrastructure.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed class NotebookAutosaveViewTests
{
    private const string Host = "PTJ_NOTEBOOK_AUTOSAVE_HOST";
    [Theory]
    [InlineData("Light", 1280, 96)]
    [InlineData("Dark", 1280, 96)]
    [InlineData("Light", 480, 240)]
    [InlineData("Dark", 480, 240)]
    public async Task BoundAutosaveAndRecoveryStatusesRetainDocumentSelectionAndAccessibleActions(string theme, int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            await IsolatedTestProcess.RunSuiteAsync(typeof(NotebookAutosaveViewTests), "notebook-autosave", Host,
                TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30));
            return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            var app = System.Windows.Application.Current ?? new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
            var prior = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var clock = new NotebookClock(); var recovery = new NotebookRecoveryFake();
            var data = new NotebookFake(); var note = NotebookFake.Note("Synthetic research") with { Document = NotebookEditingTests.Text("Research observation") };
            data.Notes.Add(note);
            var copy = new NotebookRecoveryDraft(Guid.NewGuid(), note.Id, note.Version, 10, "Recovered research",
                NotebookEditingTests.Text("Retained observation from a previous session"), clock.GetUtcNow());
            recovery.Drafts.Add(copy.Id, copy);
            using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService { ConfirmationResult = true }, new NotebookChoices(), new NotebookJsonCodec(), recovery, clock);
            var view = new NotebookWorkspaceView { DataContext = vm };
            VisualTreeHelper.SetRootDpi(view, new(dpi / 96d, dpi / 96d));
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task? save = null;
            try
            {
                Pump(vm.ActivateAsync()); vm.Panel = 0;
                Layout();
                var recoveryExpander = Descendants<Expander>(view).Single(x => Equals(x.Header, "Recovery drafts"));
                recoveryExpander.IsExpanded = true;
                Layout(); Render("recovery-available");
                Assert.Equal("Local unsaved recovery drafts", AutomationProperties.GetName(recoveryExpander));
                Pump(vm.RecoverCommand.ExecuteAsync(Assert.Single(vm.RecoveryDrafts))); Layout(); Render("recovered");
                Assert.True(vm.IsDirty); Assert.Equal(0, data.Writes);
                var editor = (RichTextBox)view.FindName("Preview");
                var document = editor.Document;
                editor.SelectAll();
                var selectionStart = editor.Selection.Start; var selectionEnd = editor.Selection.End;
                double offset = editor.VerticalOffset;
                long editorRevision = vm.EditorRevision;
                data.OnWrite = async (command, _) =>
                {
                    entered.TrySetResult(); await release.Task;
                    var draft = Assert.IsType<SaveNotebookDraft>(command);
                    var persisted = note with { Title = draft.Title, Document = draft.Document, Version = draft.ExpectedVersion + 1 };
                    data.Notes[0] = persisted; return new(NotebookWriteStatus.Success, persisted);
                };
                save = vm.SaveDraftAsync(); Pump(entered.Task.WaitAsync(TimeSpan.FromSeconds(10)));
                Layout(); Render("saving");
                int previousPanel = vm.Panel;
                foreach (var name in new[] { "ScopeList", "FolderScopeList", "NoteList" })
                {
                    vm.Panel = name == "NoteList" ? 1 : 0; Layout();
                    var list = (ListBox)view.FindName(name);
                    Assert.False(list.IsEnabled);
                    var chrome = Descendants<Border>(list).First();
                    Assert.Equal(((SolidColorBrush)view.FindResource("PtjSurfaceBrush")).Color,
                        Assert.IsType<SolidColorBrush>(chrome.Background).Color);
                    if (width < 1000 && name != "FolderScopeList") Render("saving-" + name);
                }
                vm.Panel = previousPanel; Layout();
                Assert.Equal("Saving…", vm.SaveState);
                release.TrySetResult(); Pump(save);
                Assert.Equal("Saved", vm.SaveState); Assert.False(vm.IsDirty);
                Assert.Same(document, editor.Document); Assert.Equal(editorRevision, vm.EditorRevision);
                Assert.Equal(0, selectionStart.CompareTo(editor.Selection.Start)); Assert.Equal(0, selectionEnd.CompareTo(editor.Selection.End));
                Assert.Equal(offset, editor.VerticalOffset); Layout(); Render("saved");
                data.OnWrite = null; data.NextWrite = NotebookWriteStatus.StorageFailure;
                vm.DraftTitle = "Retain this unsaved research"; clock.Advance(750); Pump(vm.AutosaveTask);
                Layout(); Render("failed");
                Assert.Contains("retry", vm.SaveState); Assert.True(vm.IsDirty);
                Assert.Contains(Descendants<TextBlock>(view), x => x.Text == vm.SaveState && AutomationProperties.GetLiveSetting(x) == AutomationLiveSetting.Polite);
                data.NextWrite = NotebookWriteStatus.Conflict; Pump(vm.SaveDraftAsync());
                Layout(); Render("conflict");
                Assert.StartsWith("Conflict", vm.SaveState);
                foreach (var button in Descendants<Button>(view).Where(x => x.IsVisible))
                {
                    var point = button.TranslatePoint(new(0, 0), view);
                    Assert.InRange(point.X, -1, width); Assert.InRange(point.X + button.ActualWidth, 0, width + 1);
                }
            }
            finally
            {
                release.TrySetResult(); if (save is not null) Pump(save);
                vm.Dispose(); Pump(vm.DeactivationTask);
                view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                SynchronizationContext.SetSynchronizationContext(prior);
            }
            int Height() => width < 1000 ? 720 : 900;
            void Layout() { view.Measure(new(width, Height())); view.Arrange(new Rect(0, 0, width, Height())); view.UpdateLayout(); }
            void Render(string state)
            {
                var directory = Path.Combine(AppContext.BaseDirectory, "notebook-autosave-renders"); Directory.CreateDirectory(directory);
                var bitmap = new RenderTargetBitmap(width * dpi / 96, Height() * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
                bitmap.Render(view); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
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
                TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
            Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
}
