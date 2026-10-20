using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;
using PersonalTradingJournal.Desktop.Views.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed class NotebookEditorViewTests
{
    private const string Host = "PTJ_NOTEBOOK_EDITOR_HOST";
    [Theory]
    [InlineData("Light", 1280, 96)]
    [InlineData("Dark", 1280, 96)]
    [InlineData("Light", 480, 240)]
    [InlineData("Dark", 480, 240)]
    public async Task EditorRoundTripsFormattingUndoPasteAndThemeLayout(string theme, int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            await IsolatedTestProcess.RunSuiteAsync(typeof(NotebookEditorViewTests), "notebook-editor", Host,
                TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30));
            return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            var app = System.Windows.Application.Current ?? new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
            var prior = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var codec = new NotebookJsonCodec();
            var doc = new NotebookDocument([
                NotebookNode.Heading(Guid.NewGuid(), 2, [NotebookNode.Run(Guid.NewGuid(), "Observation")]),
                NotebookNode.Paragraph(Guid.NewGuid(), [NotebookNode.Run(Guid.NewGuid(), "Bold", NotebookTextMarks.Bold),
                    NotebookNode.Run(Guid.NewGuid(), " and neutral"), NotebookNode.Break(Guid.NewGuid()),
                    NotebookNode.Hyperlink(Guid.NewGuid(), "https://example.com/research", [NotebookNode.Run(Guid.NewGuid(), "Research", NotebookTextMarks.Italic | NotebookTextMarks.Underline)])]),
                NotebookNode.List(Guid.NewGuid(), NotebookNodeKind.NumberedList, [NotebookNode.Item(Guid.NewGuid(), [NotebookNode.Paragraph(Guid.NewGuid(), [NotebookNode.Run(Guid.NewGuid(), "Ordered observation")])])]),
                NotebookNode.List(Guid.NewGuid(), NotebookNodeKind.Checklist, [NotebookNode.Item(Guid.NewGuid(), [NotebookNode.Paragraph(Guid.NewGuid(), [NotebookNode.Run(Guid.NewGuid(), "Review the recorded evidence")])], false)])]);
            var round = NotebookEditorAdapter.Read(NotebookEditorAdapter.Create(doc));
            Assert.Equal(codec.Encode(doc).Json, codec.Encode(round).Json);
            var image = NotebookNode.Image(Guid.NewGuid(), Guid.NewGuid(), "caption", "alt");
            Assert.Same(image, NotebookEditorAdapter.Read(NotebookEditorAdapter.Create(new([image]))).Blocks[0]);
            var data = new NotebookFake(); data.Notes.Add(NotebookFake.Note("NQ — A long research title that wraps cleanly in the narrow editor") with { Document = doc });
            using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService(), new NotebookChoices(), codec);
            var view = new NotebookWorkspaceView { DataContext = vm };
            view.Resources.MergedDictionaries.Add(CalendarViewLayoutTests.SharedThemeResources(theme));
            VisualTreeHelper.SetRootDpi(view, new(dpi / 96d, dpi / 96d));
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            try
            {
                Pump(vm.ActivateAsync()); vm.SelectedNote = vm.Notes[0]; Pump(vm.LoadTask);
                Layout();
                Assert.InRange(((ComboBox)view.FindName("ParagraphStyle")).SelectedIndex, 0, 3);
                Render("long-title");
                var editor = (RichTextBox)view.FindName("Preview");
                Assert.False(editor.IsReadOnly); Assert.Equal("Note content", AutomationProperties.GetName(editor));
                var body = (Paragraph)editor.Document.Blocks.ElementAt(1);
                editor.Selection.Select(body.ContentStart, body.ContentEnd);
                Assert.Same(DependencyProperty.UnsetValue, editor.Selection.GetPropertyValue(TextElement.FontWeightProperty));
                Assert.Equal("Mixed", AutomationProperties.GetItemStatus((Button)view.FindName("BoldButton")));
                var documentInstance = editor.Document;
                editor.Selection.Select(body.Inlines.FirstInline!.ContentStart, body.Inlines.FirstInline.ContentEnd);
                var bold = (Button)view.FindName("BoldButton");
                Assert.True(bold.IsEnabled);
                bold.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.True(vm.IsDirty); Assert.DoesNotContain(NotebookTextMarks.Bold, NotebookEditorAdapter.Read(editor.Document).Blocks[1].Children.Select(n => n.Marks));
                editor.Undo(); Assert.False(vm.IsDirty); editor.Redo(); Assert.True(vm.IsDirty); editor.Undo();
                // Style undo is a real text-container undo operation, not a model replacement.
                editor.Selection.Select(body.ContentStart, body.ContentEnd);
                NotebookEditorAdapter.Heading(editor, 1); Assert.Equal(NotebookNodeKind.Heading, NotebookEditorAdapter.Read(editor.Document).Blocks[1].Kind);
                editor.Undo(); Assert.Equal(NotebookNodeKind.Paragraph, NotebookEditorAdapter.Read(editor.Document).Blocks[1].Kind);
                var checklist = (System.Windows.Documents.List)editor.Document.Blocks.LastBlock!;
                editor.CaretPosition = checklist.ListItems.FirstListItem!.Blocks.LastBlock!.ContentStart;
                NotebookEditorAdapter.ToggleCheck(editor);
                Assert.True(NotebookEditorAdapter.Read(editor.Document).Blocks.Last().Children[0].IsChecked);
                editor.Undo(); Assert.False(NotebookEditorAdapter.Read(editor.Document).Blocks.Last().Children[0].IsChecked);
                editor.CaretPosition = checklist.ListItems.FirstListItem!.Blocks.LastBlock!.ContentEnd;
                Assert.True(NotebookEditorAdapter.AddChecklistItem(editor));
                var added = NotebookEditorAdapter.Read(editor.Document).Blocks.Last();
                Assert.Equal(2, added.Children.Count); Assert.False(added.Children.Last().IsChecked);
                editor.Undo(); Assert.Single(NotebookEditorAdapter.Read(editor.Document).Blocks.Last().Children);
                // Reject a paste before touching the live document, selection or undo stack.
                editor.CaretPosition = body.ContentEnd;
                var before = codec.Encode(NotebookEditorAdapter.Read(editor.Document)).Json;
                Assert.ThrowsAny<ArgumentException>(() => NotebookEditorAdapter.PasteText(editor, new string('x', NotebookLimits.PlainText + 1), d => codec.Encode(d)));
                Assert.Equal(before, codec.Encode(NotebookEditorAdapter.Read(editor.Document)).Json);
                NotebookEditorAdapter.PasteText(editor, "\nReadable pasted text\nSecond line", d => codec.Encode(d));
                Assert.Contains("Readable pasted text", vm.DraftDocument.PlainText);
                editor.Undo();
                Assert.Equal(doc.PlainText, NotebookEditorAdapter.Read(editor.Document).PlainText);
                body = (Paragraph)editor.Document.Blocks.ElementAt(1); // Native Undo can replace a paragraph, not the editor document.
                // Link creation/edit/removal never launches a browser and preserves the surrounding text.
                editor.Selection.Select(body.Inlines.FirstInline!.ContentStart, body.Inlines.FirstInline.ContentEnd);
                NotebookEditorAdapter.SetLink(editor, "https://example.com/one");
                var createdLink = body.Inlines.OfType<Hyperlink>().First();
                editor.CaretPosition = createdLink.Inlines.FirstInline!.ContentStart.GetPositionAtOffset(1)!;
                NotebookEditorAdapter.SetLink(editor, "https://example.com/two");
                Assert.Contains("https://example.com/two", codec.Encode(NotebookEditorAdapter.Read(editor.Document)).Json);
                NotebookEditorAdapter.SetLink(editor, null);
                Assert.DoesNotContain("https://example.com/two", codec.Encode(NotebookEditorAdapter.Read(editor.Document)).Json);
                var beforeInvalidLink = codec.Encode(NotebookEditorAdapter.Read(editor.Document)).Json;
                Assert.ThrowsAny<ArgumentException>(() => NotebookEditorAdapter.SetLink(editor, "javascript:alert(1)"));
                Assert.Equal(beforeInvalidLink, codec.Encode(NotebookEditorAdapter.Read(editor.Document)).Json);
                var clipboard = new DataObject();
                clipboard.SetData(DataFormats.UnicodeText, "Plain readable text\nSecond paragraph");
                clipboard.SetData(DataFormats.Html, "<script>never execute</script>");
                var paste = new DataObjectPastingEventArgs(clipboard, false, DataFormats.UnicodeText);
                editor.RaiseEvent(paste);
                Assert.True(paste.CommandCancelled);
                Assert.Contains("Plain readable text", vm.DraftDocument.PlainText);
                Assert.DoesNotContain("never execute", vm.DraftDocument.PlainText);
                editor.Undo();
                // Manual save must not replace the live document and clear its undo/caret/scroll state.
                vm.DraftTitle = "Saved research"; var caret = editor.CaretPosition;
                Pump(vm.SaveCommand.ExecuteAsync(null));
                Assert.Same(documentInstance, editor.Document); Assert.Equal(0, caret.CompareTo(editor.CaretPosition)); Assert.False(vm.IsDirty);
                Assert.Equal("Saved", vm.SaveState); Assert.Equal(1, data.Writes);
                Layout(); Assert.True(editor.ActualHeight > 80);
                foreach (var button in Descendants<Button>(view).Where(b => b.IsVisible))
                {
                    var at = button.TranslatePoint(new(0, 0), view);
                    Assert.InRange(at.X, -1, width); Assert.InRange(at.X + button.ActualWidth, 0, width + 1);
                }
                Render("saved");
                var scroll = (ScrollViewer)view.FindName("ContentScroll");
                scroll.ScrollToBottom(); Layout();
                var bottom = editor.TranslatePoint(new(0, editor.ActualHeight), view);
                Assert.InRange(bottom.Y, 0, Height());
                Render("document");
            }
            finally { vm.Deactivate(); Pump(vm.DeactivationTask); view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); SynchronizationContext.SetSynchronizationContext(prior); }
            int Height() => width < 1000 ? 650 : 900;
            void Layout() { view.Measure(new(width, Height())); view.Arrange(new Rect(0, 0, width, Height())); view.UpdateLayout(); }
            void Render(string state)
            {
                var directory = Path.Combine(AppContext.BaseDirectory, "notebook-editor-renders"); Directory.CreateDirectory(directory);
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
