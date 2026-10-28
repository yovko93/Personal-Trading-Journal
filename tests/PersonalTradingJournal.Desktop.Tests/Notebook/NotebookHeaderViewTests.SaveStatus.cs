using System.Globalization;
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

public sealed partial class NotebookHeaderViewTests
{
    [Theory]
    [InlineData("Light", 1280, 96)]
    [InlineData("Dark", 1280, 96)]
    [InlineData("Light", 480, 240)]
    [InlineData("Dark", 480, 240)]
    public async Task SaveStatusBesidePinReflectsCanonicalStateOffscreen(string theme, int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            string expected = string.Create(CultureInfo.InvariantCulture,
                $"{typeof(NotebookHeaderViewTests).FullName}.{nameof(SaveStatusBesidePinReflectsCanonicalStateOffscreen)}(theme: \"{theme}\", width: {width}, dpi: {dpi})");
            await IsolatedTestProcess.RunSuiteAsync(typeof(NotebookHeaderViewTests), "notebook-save-status", Host,
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
            var data = new NotebookFake();
            data.Notes.Add(NotebookFake.Note("Long research title — observations, decisions and lessons retained across the next session")
                with { Document = NotebookEditingTests.Text("Research content remains editable and retained.") });
            data.Notes.Add(NotebookFake.Note("Second note") with { Document = NotebookEditingTests.Text("Other content") });
            var clock = new NotebookClock(); var recovery = new NotebookRecoveryFake();
            using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService(), new NotebookChoices { Choice = NotebookLeaveChoice.Discard }, new NotebookJsonCodec(), recovery, clock);
            // No Window/HwndSource, native input, activation, focus request or pointer prerequisite.
            var view = new NotebookWorkspaceView { DataContext = vm };
            VisualTreeHelper.SetRootDpi(view, new(dpi / 96d, dpi / 96d));
            var release = new TaskCompletionSource<NotebookWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                Pump(vm.ActivateAsync()); vm.SelectedNote = vm.Notes.Single(n => n.Id == data.Notes[0].Id); Pump(vm.LoadTask);
                view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent)); vm.Panel = 2; Layout();
                State("Saved locally", true); Render("saved");
                var status = (TextBlock)view.FindName("NoteSaveStatus");
                var group = (StackPanel)view.FindName("NoteSaveStatusGroup");
                var pin = (Button)view.FindName("PinNoteButton"); var menu = (Button)view.FindName("NoteOverflow");
                var actions = Assert.IsType<StackPanel>(group.Parent);
                Assert.Same(pin, actions.Children[actions.Children.IndexOf(group) + 1]);
                Assert.Same(menu, actions.Children[actions.Children.IndexOf(pin) + 1]);
                Assert.Same(vm.PinCommand, pin.Command);
                Assert.True(group.TranslatePoint(new Point(group.ActualWidth, 0), view).X <= pin.TranslatePoint(new Point(), view).X);
                Assert.InRange(Math.Abs(group.TranslatePoint(new Point(0, group.ActualHeight / 2), view).Y - pin.TranslatePoint(new Point(0, pin.ActualHeight / 2), view).Y), 0, 1);
                Assert.True(menu.TranslatePoint(new Point(menu.ActualWidth, 0), view).X <= width);
                Assert.Same(view.FindResource("PtjSuccessBrush"), ((System.Windows.Shapes.Path)view.FindName("NoteSavedCheck")).Stroke);
                Assert.Equal(((Geometry)view.FindResource("PtjIconActivate")).ToString(), ((System.Windows.Shapes.Path)view.FindName("NoteSavedCheck")).Data.ToString());
                vm.ShowNotification("Independent operation feedback", NotebookNotificationKind.Information);
                vm.DismissNotificationCommand.Execute(null); State("Saved locally", true);
                clock.Advance(10000); State("Saved locally", true);
                vm.DraftTitle = "Captured revision"; State("Unsaved changes", false);
                var entered = new TaskCompletionSource<SaveNotebookDraft>(TaskCreationOptions.RunContinuationsAsynchronously);
                data.OnWrite = (command, _) => { entered.TrySetResult((SaveNotebookDraft)command); return release.Task; };
                var old = vm.Note!; var save = vm.SaveDraftAsync(); Pump(entered.Task.WaitAsync(TimeSpan.FromSeconds(10)));
                State("Saving…", false);
                vm.SelectedNote = vm.Notes.Single(n => n.Id != old.Id); Assert.Equal(old.Id, vm.Note!.Id); // Guard refuses departure while writing.
                vm.DraftTitle = "Newer unsaved revision";
                release.SetResult(new(NotebookWriteStatus.Success, old with { Title = entered.Task.Result.Title, Version = old.Version + 1 }));
                Pump(save); State("Unsaved changes", false); Assert.Equal("Newer unsaved revision", vm.DraftTitle);
                data.OnWrite = null; data.NextWrite = NotebookWriteStatus.StorageFailure;
                Pump(vm.SaveDraftAsync()); State("Save paused", false); string feedback = vm.SaveFeedback;
                Assert.NotEmpty(feedback); Assert.NotEmpty(recovery.Drafts);
                vm.DismissNotificationCommand.Execute(null); State("Save paused", false);
                Assert.Equal(feedback, vm.SaveFeedback); Assert.True(vm.IsDirty); Render("paused");
                data.NextWrite = NotebookWriteStatus.Conflict; Pump(vm.SaveDraftAsync()); State("Save paused", false);
                Assert.Contains("Conflict", vm.SaveState); // Existing compact pause plus persistent conflict detail.
                data.NextWrite = NotebookWriteStatus.Success;
                vm.SelectedNote = vm.Notes.Single(n => n.Id != old.Id); Pump(vm.LoadTask);
                State("Saved locally", true); Assert.Equal("Second note", vm.DraftTitle);

                // Explicit recovery is not a canonical save, even when its checkpoint is durable.
                foreach (bool conflict in new[] { false, true })
                {
                    var store = new NotebookRecoveryFake(); var original = data.Notes[0];
                    var draft = new NotebookRecoveryDraft(Guid.NewGuid(), original.Id, conflict ? original.Version + 1 : original.Version, 10,
                        "Recovered unsaved draft", NotebookEditingTests.Text("Recovery content"), clock.GetUtcNow());
                    store.Drafts.Add(draft.Id, draft);
                    using var recovered = new NotebookWorkspaceViewModel(data, new FakeDialogService(), new NotebookChoices(), new NotebookJsonCodec(), store, clock);
                    Pump(recovered.ActivateAsync()); Pump(recovered.RecoverCommand.ExecuteAsync(Assert.Single(recovered.RecoveryDrafts)));
                    view.DataContext = recovered; recovered.Panel = 2; State(conflict ? "Conflict" : "Save paused", false);
                    Assert.Contains("Recovered", recovered.SaveState); Assert.True(recovered.IsDirty);
                    recovered.Deactivate(); Pump(recovered.DeactivationTask);
                }
            }
            finally
            {
                release.TrySetResult(new(NotebookWriteStatus.Cancelled));
                view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                vm.Deactivate(); Pump(vm.DeactivationTask); app.Shutdown(); SynchronizationContext.SetSynchronizationContext(prior);
            }
            void Layout()
            {
                view.Measure(new Size(width, 900)); view.Arrange(new Rect(0, 0, width, 900)); view.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                Assert.Null(PresentationSource.FromVisual(view));
            }
            void State(string expected, bool saved)
            {
                Layout(); var text = (TextBlock)view.FindName("NoteSaveStatus");
                Assert.Equal(expected, text.Text); Assert.Equal(expected, AutomationProperties.GetName(text));
                Assert.Equal(saved ? Visibility.Visible : Visibility.Collapsed, ((FrameworkElement)view.FindName("NoteSavedCheck")).Visibility);
            }
            void Render(string state)
            {
                string directory = Path.Combine(Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY")!, "save-status-renders"); Directory.CreateDirectory(directory);
                var bitmap = new RenderTargetBitmap(width * dpi / 96, 900 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(view);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(directory, $"{theme}-{width}-{dpi}-{state}.png")); encoder.Save(file);
            }
        });
    }
}
