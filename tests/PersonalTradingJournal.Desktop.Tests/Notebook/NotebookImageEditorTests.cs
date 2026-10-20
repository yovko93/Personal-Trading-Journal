using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Screenshots;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;
using PersonalTradingJournal.Desktop.Views.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed class NotebookImageEditorTests
{
    private const string Host = "PTJ_NOTEBOOK_IMAGE_EDITOR_HOST";
    [Theory]
    [InlineData("Light", 1280, 96)]
    [InlineData("Dark", 1280, 96)]
    [InlineData("Light", 480, 240)]
    [InlineData("Dark", 480, 240)]
    public async Task ImageRoundTripUndoPlacementPasteAndRenders(string theme, int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            await IsolatedTestProcess.RunSuiteAsync(typeof(NotebookImageEditorTests), "notebook-image-editor", Host,
                TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30)); return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            var app = System.Windows.Application.Current ?? new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
            var prior = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var codec = new NotebookJsonCodec(); var data = new NotebookFake();
            var first = NotebookNode.Paragraph(Guid.NewGuid(), [NotebookNode.Run(Guid.NewGuid(), "Before ", NotebookTextMarks.Bold),
                NotebookNode.Run(Guid.NewGuid(), "selected"), NotebookNode.Run(Guid.NewGuid(), " after", NotebookTextMarks.Italic)]);
            var chart = NotebookNode.Image(Guid.NewGuid(), Guid.NewGuid(), "Illustrative chart · NQ, 5m", "Synthetic chart");
            var missing = NotebookNode.Image(Guid.NewGuid(), Guid.NewGuid(), "Retained reference with unavailable original", "Unavailable chart");
            var doc = new NotebookDocument([first, chart, missing, .. NotebookEditingTests.Text("Review the context before forming a conclusion.").Blocks]);
            var note = NotebookFake.Note("NQ — Liquidity sweep study") with { Document = doc, Images =
                [NotebookImage.Owned(chart.ImageId!.Value, 0, "chart.png", 1, new string('A', 64), chart.Caption, chart.AlternativeText),
                 NotebookImage.TradeReference(missing.ImageId!.Value, 1, Guid.NewGuid(), Guid.NewGuid(), missing.Caption, missing.AlternativeText)] };
            data.Notes.Add(note);
            var imports = new Imports(data);
            var presentation = new Presentation(chart.ImageId.Value);
            using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService(), new NotebookChoices(), codec,
                images: imports, imagePresentation: presentation);
            var input = new Input(); var view = new NotebookWorkspaceView { DataContext = vm, ImageInput = input };
            view.Resources.MergedDictionaries.Add(CalendarViewLayoutTests.SharedThemeResources(theme));
            VisualTreeHelper.SetRootDpi(view, new(dpi / 96d, dpi / 96d));
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            try
            {
                Pump(vm.ActivateAsync()); vm.SelectedNote = vm.Notes[0]; Pump(vm.LoadTask); Pump(view.ImageLoadsTask); Layout();
                var editor = (RichTextBox)view.FindName("Preview");
                string original = codec.Encode(doc).Json;
                Assert.Equal(original, codec.Encode(NotebookEditorAdapter.Read(editor.Document)).Json);
                Assert.True(vm.CanEditBody); Assert.False(vm.IsDirty); Assert.Equal(0, data.Writes);
                var block = NotebookEditorAdapter.Images(editor.Document).First();
                NotebookEditorAdapter.CaptionImage(editor, block, "Revised caption", "Revised alt");
                Assert.Equal("Revised caption", vm.DraftDocument.ImageNodes[0].Caption);
                ApplicationCommands.Undo.Execute(null, editor); Assert.Equal(original, codec.Encode(NotebookEditorAdapter.Read(editor.Document)).Json);
                Assert.All(NotebookEditorAdapter.Images(editor.Document), image => Assert.IsType<Border>(image.Child));
                Pump(view.ImageLoadsTask);
                ApplicationCommands.Redo.Execute(null, editor); Assert.Equal("Revised caption", NotebookEditorAdapter.Read(editor.Document).ImageNodes[0].Caption); ApplicationCommands.Undo.Execute(null, editor);
                block = NotebookEditorAdapter.Images(editor.Document).First();
                NotebookEditorAdapter.MoveImage(editor, block, false);
                Assert.Equal(missing.ImageId, vm.DraftDocument.ImageNodes[0].ImageId);
                ApplicationCommands.Undo.Execute(null, editor); Assert.Equal(original, codec.Encode(NotebookEditorAdapter.Read(editor.Document)).Json);
                block = NotebookEditorAdapter.Images(editor.Document).First();
                editor.Selection.Select(block.ElementStart, block.ElementEnd); editor.Selection.Text = "";
                Assert.Single(vm.DraftDocument.ImageNodes); Assert.True(PumpResult(vm.SaveDraftAsync()));
                ApplicationCommands.Undo.Execute(null, editor); Assert.Equal(original, codec.Encode(NotebookEditorAdapter.Read(editor.Document)).Json);
                Assert.True(PumpResult(vm.SaveDraftAsync())); Assert.Equal(2, vm.Note!.Images.Length);
                Assert.Equal(NotebookImageOwnership.TradeScreenshotReference, vm.Note.Images[1].Ownership);
                string? cutText = null; view.WriteClipboardText = text => cutText = text;
                block = NotebookEditorAdapter.Images(editor.Document).First();
                editor.Selection.Select(block.ElementStart, block.ElementEnd); ApplicationCommands.Cut.Execute(null, editor);
                Assert.NotNull(cutText); Assert.Single(vm.DraftDocument.ImageNodes);
                ApplicationCommands.Undo.Execute(null, editor); Assert.Equal(original, codec.Encode(NotebookEditorAdapter.Read(editor.Document)).Json);
                // Validate a rejected image before replacing selected formatted content.
                var paragraph = (Paragraph)editor.Document.Blocks.FirstBlock!;
                var selected = paragraph.Inlines.ElementAt(1);
                editor.Selection.Select(selected.ContentStart, selected.ContentEnd);
                var start = editor.Selection.Start; var end = editor.Selection.End;
                input.Bytes = [1, 2, 3]; imports.Status = NotebookWriteStatus.UnsupportedContent;
                ((Button)view.FindName("InsertImageButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(view.ImageOperationTask);
                Assert.Equal(original, codec.Encode(NotebookEditorAdapter.Read(editor.Document)).Json);
                Assert.Equal(0, start.CompareTo(editor.Selection.Start)); Assert.Equal(0, end.CompareTo(editor.Selection.End));
                Assert.Contains("codec", ((TextBlock)view.FindName("EditorFeedback")).Text);
                Pump(view.ImageLoadsTask); Layout(); Render("codec-error");
                input.Bytes = null;
                ((Button)view.FindName("InsertImageButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(view.ImageOperationTask);
                Assert.Equal(original, codec.Encode(NotebookEditorAdapter.Read(editor.Document)).Json);
                input.Reject = true;
                ((Button)view.FindName("InsertImageButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Throws<InvalidDataException>(() => Pump(view.ImageOperationTask));
                Assert.Equal(original, codec.Encode(NotebookEditorAdapter.Read(editor.Document)).Json);
                Assert.Contains("read safely", ((TextBlock)view.FindName("EditorFeedback")).Text);
                Assert.Equal(0, start.CompareTo(editor.Selection.Start)); input.Reject = false;
                imports.Status = NotebookWriteStatus.Success; input.Bytes = [7, 8, 9];
                ((Button)view.FindName("InsertImageButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(view.ImageOperationTask);
                var inserted = NotebookEditorAdapter.Read(editor.Document);
                Assert.Equal("Before \n\n after\nIllustrative chart · NQ, 5m\nRetained reference with unavailable original\nReview the context before forming a conclusion.\n", inserted.PlainText);
                Assert.Equal(3, inserted.ImageNodes.Count); Assert.Equal(input.Bytes, imports.LastBytes);
                Assert.True(PumpResult(vm.SaveDraftAsync()));
                ApplicationCommands.Undo.Execute(null, editor); Assert.Equal(original, codec.Encode(NotebookEditorAdapter.Read(editor.Document)).Json);
                Assert.True(PumpResult(vm.SaveDraftAsync())); ApplicationCommands.Redo.Execute(null, editor);
                Assert.Equal(3, NotebookEditorAdapter.Read(editor.Document).ImageNodes.Count);
                Assert.True(PumpResult(vm.SaveDraftAsync()));
                // Long-caption + missing-original evidence on the production image presenter.
                NotebookEditorAdapter.RemoveImage(editor, NotebookEditorAdapter.Images(editor.Document).First());
                Assert.True(PumpResult(vm.SaveDraftAsync()));
                vm.DraftTitle = "NQ — Liquidity sweep study";
                block = NotebookEditorAdapter.Images(editor.Document).First(b => ((NotebookNode)b.Tag).ImageId == chart.ImageId);
                NotebookEditorAdapter.CaptionImage(editor, block,
                    "Illustrative chart · NQ, 5m. " + string.Concat(Enumerable.Repeat("Wait for displacement and review the return to the imbalance. ", 4)), "Synthetic chart");
                Assert.True(PumpResult(vm.SaveDraftAsync()));
                // Reinstall through a genuine reload to populate the current preview and assert no render-only saves.
                Pump(vm.RefreshCommand.ExecuteAsync(null)); Pump(view.ImageLoadsTask); Layout();
                Assert.False(vm.IsDirty); Render("inline-caption-missing");
                var missingBlock = NotebookEditorAdapter.Images(editor.Document).Single(b => ((NotebookNode)b.Tag).ImageId == missing.ImageId);
                var imageActions = ((StackPanel)((Border)missingBlock.Child).Child).Children.OfType<Expander>().Single();
                Assert.Equal(((SolidColorBrush)view.FindResource("PtjTextPrimaryBrush")).Color, ((SolidColorBrush)imageActions.Foreground).Color);
                imageActions.IsExpanded = true;
                var contentScroll = (ScrollViewer)view.FindName("ContentScroll");
                contentScroll.ScrollToBottom(); editor.ScrollToEnd(); Layout();
                contentScroll.ScrollToBottom(); editor.ScrollToEnd(); Layout(); Render("missing-and-actions");
                imageActions.IsExpanded = false; contentScroll.ScrollToTop(); editor.ScrollToHome(); Layout();
                Assert.All(NotebookEditorAdapter.Images(editor.Document), image =>
                    Assert.True(((FrameworkElement)image.Child).ActualWidth <= editor.ActualWidth));
                // Clipboard bitmap conversion is lossless PNG, with no OS clipboard access.
                var bitmap = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 1, 2, 3, 255, 4, 5, 6, 255 }, 8);
                var clipboard = new DataObject(); clipboard.SetData(DataFormats.Bitmap, bitmap);
                byte[] encoded = new WpfNotebookImageInput().ClipboardImage(clipboard)!;
                Assert.Equal(NotebookImageStatus.Success, new WpfNotebookImageValidator().Validate(encoded).Status);
                var decoded = PlaybookExampleImages.Decode(encoded, false); byte[] pixels = new byte[8]; decoded.CopyPixels(pixels, 8, 0);
                Assert.Equal(new byte[] { 1, 2, 3, 255, 4, 5, 6, 255 }, pixels);
                var png = new DataObject(); png.SetData("PNG", new MemoryStream(encoded));
                Assert.Equal(encoded, new WpfNotebookImageInput().ClipboardImage(png));
                // Exercise the actual editor paste route through the input port (no system clipboard).
                input.Bytes = encoded; editor.CaretPosition = editor.Document.Blocks.LastBlock!.ContentEnd;
                var paste = new DataObjectPastingEventArgs(clipboard, false, DataFormats.Bitmap); editor.RaiseEvent(paste);
                Pump(view.ImageOperationTask); Assert.True(paste.CommandCancelled);
                Assert.Equal(encoded, imports.LastBytes); Assert.Equal(3, vm.DraftDocument.ImageNodes.Count);
                Assert.True(PumpResult(vm.SaveDraftAsync()));
                // Control-V must select an actually present format for a bitmap-only clipboard.
                view.ReadClipboardData = () => clipboard;
                Assert.False(clipboard.GetDataPresent(DataFormats.UnicodeText, false));
                Assert.NotNull(editor.CaretPosition.Paragraph);
                Assert.True(ApplicationCommands.Paste.CanExecute(null, editor));
                ApplicationCommands.Paste.Execute(null, editor); Pump(view.ImageOperationTask);
                Assert.Equal(encoded, imports.LastBytes); Assert.Equal(4, vm.DraftDocument.ImageNodes.Count);
                Assert.True(PumpResult(vm.SaveDraftAsync()));
                // Selected content changed during async import: preserve newer typing and remove the canonical staging reference on save.
                paragraph = (Paragraph)editor.Document.Blocks.FirstBlock!;
                selected = paragraph.Inlines.FirstInline!;
                editor.Selection.Select(selected.ContentStart, selected.ContentEnd);
                var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                imports.Before = _ => { entered.TrySetResult(); return release.Task; };
                ((Button)view.FindName("InsertImageButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(entered.Task);
                ((Run)selected).Text += " newer";
                string newer = vm.DraftDocument.PlainText; int count = vm.DraftDocument.ImageNodes.Count;
                release.SetResult(); Pump(view.ImageOperationTask);
                Assert.Equal(newer, vm.DraftDocument.PlainText); Assert.Equal(count, vm.DraftDocument.ImageNodes.Count);
                Assert.True(vm.IsDirty); Assert.True(PumpResult(vm.SaveDraftAsync()));
                // An obsolete preview deliberately ignores cancellation; the view must still refuse to publish it.
                var delayed = new TaskCompletionSource<ImageSource>(TaskCreationOptions.RunContinuationsAsynchronously);
                presentation.Controlled = delayed.Task;
                var other = NotebookFake.Note("Other note") with { Document = NotebookEditingTests.Text("Other content"), Images = [] }; data.Notes.Add(other);
                Pump(vm.RefreshCommand.ExecuteAsync(null));
                var obsoleteBlock = NotebookEditorAdapter.Images(editor.Document).First(b => ((NotebookNode)b.Tag).ImageId == chart.ImageId);
                var obsoletePicture = (Image)((StackPanel)((Border)obsoleteBlock.Child).Child).Children[0];
                Assert.Null(obsoletePicture.Source);
                vm.SelectedNote = vm.Notes.Single(n => n.Id == other.Id); Pump(vm.LoadTask);
                delayed.SetResult(bitmap); Pump(view.ImageLoadsTask);
                Assert.Null(obsoletePicture.Source); Assert.Equal(other.Id, vm.Note!.Id);
                Assert.Equal("Other content\n", vm.DraftDocument.PlainText); Assert.False(vm.IsDirty);
            }
            finally
            {
                vm.Deactivate(); Pump(vm.DeactivationTask); view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                Pump(view.ImageLoadsTask); SynchronizationContext.SetSynchronizationContext(prior);
            }
            int Height() => width < 1000 ? 850 : 1000;
            void Layout() { view.Measure(new(width, Height())); view.Arrange(new Rect(0, 0, width, Height())); view.UpdateLayout(); }
            void Render(string state)
            {
                string directory = Path.Combine(AppContext.BaseDirectory, "notebook-image-renders"); Directory.CreateDirectory(directory);
                var target = new RenderTargetBitmap(width * dpi / 96, Height() * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); target.Render(view);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(target));
                using var output = File.Create(Path.Combine(directory, $"{theme}-{width}-{dpi}-{state}.png")); encoder.Save(output);
            }
        });
    }
    internal sealed class Input : INotebookImageInput
    {
        internal byte[]? Bytes;
        internal bool Reject;
        public Task<byte[]?> PickAsync(CancellationToken ct) => Reject ? throw new InvalidDataException("Controlled byte limit") : Task.FromResult(Bytes);
        public byte[]? ClipboardImage(IDataObject data) => Bytes;
    }
    internal sealed class Imports(NotebookFake data) : INotebookImages
    {
        internal NotebookWriteStatus Status = NotebookWriteStatus.Success;
        internal byte[]? LastBytes;
        internal int OwnedReads;
        internal Func<ImportNotebookImage, Task>? Before;
        public async Task<NotebookWriteResult> ImportAsync(ImportNotebookImage request, Stream content, CancellationToken token = default)
        {
            using var output = new MemoryStream(); await content.CopyToAsync(output, token); LastBytes = output.ToArray();
            if (Before is not null) await Before(request);
            if (Status != NotebookWriteStatus.Success) return new(Status);
            int index = data.Notes.FindIndex(x => x.Id == request.NoteId); var previous = data.Notes[index];
            if (previous.Version != request.ExpectedVersion) return new(NotebookWriteStatus.Conflict);
            var node = request.Document.ImageNodes.Single(x => x.ImageId == request.ImageId);
            var saved = previous with { Document = request.Document, Version = previous.Version + 1,
                Images = previous.Images.Add(NotebookImage.Owned(request.ImageId, previous.Images.Length, $"{request.ImageId:N}.png", 1, new string('A', 64), node.Caption, node.AlternativeText)) };
            data.Notes[index] = saved; return new(NotebookWriteStatus.Success, saved);
        }
        public Task<NotebookImageContent> ReadOriginalAsync(Guid noteId, Guid imageId, CancellationToken token = default)
        { OwnedReads++; return Task.FromResult(new NotebookImageContent(NotebookImageStatus.Missing)); }
    }
    private sealed class Presentation(Guid visible) : INotebookImagePresentation
    {
        internal Task<ImageSource>? Controlled;
        public Task<ImageSource> PreviewAsync(Guid noteId, NotebookImage image, CancellationToken ct)
        {
            if (image.Id != visible) throw new IOException("Synthetic unavailable original");
            if (Controlled is not null) return Controlled;
            var drawing = new DrawingVisual(); using (var dc = drawing.RenderOpen())
            {
                dc.DrawRectangle(Brushes.WhiteSmoke, null, new Rect(0, 0, 720, 180));
                for (int i = 0; i < 70; i++)
                {
                    double y = 70 + 45 * Math.Sin(i / 14d); var brush = i % 3 == 0 ? Brushes.IndianRed : Brushes.Teal;
                    dc.DrawLine(new Pen(brush, 1), new(10 + i * 10, y - 8), new(10 + i * 10, y + 16));
                    dc.DrawRectangle(brush, null, new Rect(7 + i * 10, y, 6, 9));
                }
            }
            var target = new RenderTargetBitmap(720, 180, 96, 96, PixelFormats.Pbgra32); target.Render(drawing); target.Freeze();
            return Task.FromResult<ImageSource>(target);
        }
        public Task ShowAsync(Guid noteId, NotebookImage image, CancellationToken ct) => Task.CompletedTask;
    }
    private static bool PumpResult(Task<bool> task) { Pump(task); return task.GetAwaiter().GetResult(); }
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
}
