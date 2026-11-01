using System.Collections.Immutable;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
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
    [InlineData("Dark", 1280, 96)]
    [InlineData("Light", 480, 240)]
    [InlineData("Dark", 480, 240)]
    public async Task ReadingAndEditShareRichDocumentAndHistoryOffscreen(string theme, int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            string expected = string.Create(CultureInfo.InvariantCulture,
                $"{typeof(NotebookHeaderViewTests).FullName}.{nameof(ReadingAndEditShareRichDocumentAndHistoryOffscreen)}(theme: \"{theme}\", width: {width}, dpi: {dpi})");
            await IsolatedTestProcess.RunSuiteAsync(typeof(NotebookHeaderViewTests), "notebook-reading", Host,
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
            var data = new NotebookFake(); var codec = new NotebookJsonCodec();
            var folder = new NotebookNamedItem(Guid.NewGuid(), "Market research", 1); data.Folders.Add(folder);
            var tag = new NotebookNamedItem(Guid.NewGuid(), "NQ", 1); data.Tags.Add(tag);
            var image = NotebookNode.Image(Guid.NewGuid(), Guid.NewGuid(), "Illustrative chart · synthetic image", "Synthetic chart");
            var document = new NotebookDocument([
                NotebookNode.Heading(Guid.NewGuid(), 2, [NotebookNode.Run(Guid.NewGuid(), "Observation")]),
                NotebookNode.Paragraph(Guid.NewGuid(), [NotebookNode.Run(Guid.NewGuid(), "Wait for confirmation. ", NotebookTextMarks.Bold),
                    NotebookNode.Run(Guid.NewGuid(), "Record the context.", NotebookTextMarks.Italic | NotebookTextMarks.Underline)]),
                NotebookNode.Paragraph(Guid.NewGuid(), [NotebookNode.Hyperlink(Guid.NewGuid(), "https://example.com/research", [NotebookNode.Run(Guid.NewGuid(), "Research reference")])]),
                NotebookNode.List(Guid.NewGuid(), NotebookNodeKind.Checklist, [NotebookNode.Item(Guid.NewGuid(), [NotebookNode.Paragraph(Guid.NewGuid(), [NotebookNode.Run(Guid.NewGuid(), "Review the recorded session")])], true)]), image]);
            var note = NotebookFake.Note("NQ — A long research title, observations and lessons for the next session", folder.Id) with {
                Document = document, IsPinned = true, TagIds = [tag.Id],
                Links = [new(NotebookTargetKind.Trade, Guid.NewGuid(), "Trade #057"), new(NotebookTargetKind.TradingSetup, Guid.NewGuid(), "Setup · Liquidity sweep")] };
            data.Notes.Add(note); data.Notes.Add(NotebookFake.Note("Other note") with { Document = NotebookEditingTests.Text("Independent content") });
            data.OnResolve = (links, _) => Task.FromResult<ImmutableArray<NotebookResolvedLink>>([.. links.Select(l => new NotebookResolvedLink(l, NotebookTargetAvailability.Available, l.LastKnownLabel))]);
            using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService(), new NotebookChoices { Choice = NotebookLeaveChoice.Discard }, codec,
                imagePresentation: new NotebookImageEditorTests.Presentation(image.ImageId!.Value));
            var view = new NotebookWorkspaceView { DataContext = vm };
            VisualTreeHelper.SetRootDpi(view, new(dpi / 96d, dpi / 96d));
            try
            {
                Pump(vm.ActivateAsync()); vm.SelectedNote = vm.Notes.Single(n => n.Id == note.Id); Pump(vm.LoadTask);
                view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent)); vm.Panel = 2; Layout(); Pump(view.ImageLoadsTask); Layout();
                var editor = (RichTextBox)view.FindName("Preview"); var title = (TextBox)view.FindName("TitleEditor");
                var mode = (Button)view.FindName("EditNoteButton"); var toolbar = (WrapPanel)view.FindName("FormattingToolbar");
                var pin = (Button)view.FindName("PinNoteButton"); var menu = (Button)view.FindName("NoteOverflow");
                var actions = (StackPanel)pin.Parent;
                Assert.Same(mode, actions.Children[actions.Children.IndexOf(pin) + 1]);
                Assert.Same(menu, actions.Children[actions.Children.IndexOf(mode) + 1]);
                Assert.Same(vm.EditModeCommand, mode.Command); Assert.Equal("Edit", AutomationProperties.GetName(mode));
                Assert.True(editor.IsReadOnly); Assert.True(title.IsReadOnly); Assert.Equal(new Thickness(0), title.BorderThickness);
                Assert.Equal(Visibility.Collapsed, ((Border)toolbar.Parent).Visibility);
                Assert.Equal(Visibility.Collapsed, ((Button)view.FindName("AddTagButton")).Visibility);
                Assert.Equal(2, ((ItemsControl)view.FindName("LinkedItemList")).Items.Count);
                Assert.Equal(codec.Encode(document), codec.Encode(NotebookEditorAdapter.Read(editor.Document)));
                Assert.Contains("☑ Checked", new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text);
                Assert.Contains(SwitchDescendants<Image>(editor), i => i.Source is not null);
                Assert.All(SwitchDescendants<Expander>(editor), e => Assert.Equal(Visibility.Collapsed, e.Visibility));
                var link = ((Paragraph)editor.Document.Blocks.ElementAt(2)).Inlines.OfType<Hyperlink>().Single();
                Uri? opened = null; view.LaunchLink = uri => opened = uri;
                link.RaiseEvent(new RequestNavigateEventArgs(link.NavigateUri, "") { RoutedEvent = Hyperlink.RequestNavigateEvent });
                Assert.Equal(link.NavigateUri, opened); // Validated existing launch boundary; no network/browser is opened.
                var original = editor.Document; long revision = vm.EditorRevision;
                ((Button)view.FindName("ToggleCheckButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(0, data.Writes); Assert.False(vm.IsDirty); Assert.Equal(codec.Encode(document), codec.Encode(vm.DraftDocument));
                Render("reading"); ScrollLinks(); Render("reading-links"); ScrollTop();
                Pump(vm.EditModeCommand.ExecuteAsync(null)); Layout();
                Assert.False(editor.IsReadOnly); Assert.False(title.IsReadOnly); Assert.Same(original, editor.Document);
                Assert.Equal(Visibility.Visible, ((Border)toolbar.Parent).Visibility); Assert.Equal("Done", AutomationProperties.GetName(mode));
                Assert.All(SwitchDescendants<Expander>(editor), e => Assert.Equal(Visibility.Visible, e.Visibility));
                Render("editing");
                var paragraph = (Paragraph)editor.Document.Blocks.ElementAt(1);
                editor.Selection.Select(paragraph.ContentStart, paragraph.ContentEnd);
                ((Button)view.FindName("ItalicButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.True(vm.IsDirty); Assert.True(ApplicationCommands.Undo.CanExecute(null, editor));
                var changed = codec.Encode(vm.DraftDocument);
                long beforeDone = vm.DraftRevision;
                Pump(vm.EditModeCommand.ExecuteAsync(null)); Layout();
                Assert.False(vm.IsEditing, $"Done: writes={data.Writes}; revision={beforeDone}->{vm.DraftRevision}; dirty={vm.IsDirty}; state={vm.SaveState}; feedback={vm.SaveFeedback}; message={vm.Message}"); Assert.Equal(1, data.Writes); Assert.Same(original, editor.Document); Assert.Equal(revision, vm.EditorRevision);
                Assert.Equal(changed, codec.Encode(vm.Note!.Document));
                Pump(vm.EditModeCommand.ExecuteAsync(null)); Layout();
                Assert.True(ApplicationCommands.Undo.CanExecute(null, editor)); ApplicationCommands.Undo.Execute(null, editor);
                Assert.Equal(codec.Encode(document), codec.Encode(vm.DraftDocument));
                ApplicationCommands.Redo.Execute(null, editor); Assert.Equal(changed, codec.Encode(vm.DraftDocument));
                vm.SelectedNote = vm.Notes.Single(n => n.Id != note.Id); Pump(vm.LoadTask); Layout();
                Assert.False(vm.IsEditing); Assert.True(editor.IsReadOnly); Assert.NotSame(original, editor.Document);
                Pump(vm.EditModeCommand.ExecuteAsync(null)); Assert.False(ApplicationCommands.Undo.CanExecute(null, editor));
                // Text-only notes use WPF's undo stack rather than the image/reference history.
                editor.Selection.Select(editor.Document.ContentStart, editor.Document.ContentEnd);
                NotebookEditorAdapter.PasteText(editor, "Text-only revision", vm.ValidateDocument);
                Assert.True(vm.IsDirty); Pump(vm.EditModeCommand.ExecuteAsync(null)); Assert.False(vm.IsEditing);
                Pump(vm.EditModeCommand.ExecuteAsync(null)); Assert.True(ApplicationCommands.Undo.CanExecute(null, editor));
                ApplicationCommands.Undo.Execute(null, editor);
                Assert.Equal("Independent content\n", vm.DraftDocument.PlainText);
                Assert.False(app.Dispatcher.HasShutdownStarted);
                void ScrollLinks() { ((ScrollViewer)view.FindName("ContentScroll")).ScrollToBottom(); Layout(); }
                void ScrollTop() { ((ScrollViewer)view.FindName("ContentScroll")).ScrollToTop(); Layout(); }
            }
            finally
            {
                vm.Deactivate(); Pump(vm.DeactivationTask); view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                app.Shutdown(); SynchronizationContext.SetSynchronizationContext(prior);
            }
            void Layout() { view.Measure(new Size(width, 1050)); view.Arrange(new Rect(0, 0, width, 1050)); view.UpdateLayout(); Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); }
            void Render(string state)
            {
                string directory = Path.Combine(Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY")!, "reading-renders"); Directory.CreateDirectory(directory);
                var bitmap = new RenderTargetBitmap(width * dpi / 96, 1050 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(view);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(directory, $"{theme}-{width}-{dpi}-{state}.png")); encoder.Save(file);
            }
        });
    }
}
