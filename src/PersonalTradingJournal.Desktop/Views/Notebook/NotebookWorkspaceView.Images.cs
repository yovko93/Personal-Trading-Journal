using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using PersonalTradingJournal.Desktop.Screenshots;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Desktop.Views.Notebook;

public partial class NotebookWorkspaceView
{
    public INotebookImageInput ImageInput { get; set; } = new WpfNotebookImageInput();
    public Task ImageOperationTask { get; private set; } = Task.CompletedTask;
    public Task ImageLoadsTask { get; private set; } = Task.CompletedTask;
    private CancellationTokenSource? _imageLoads;
    private readonly SemaphoreSlim _previewSlots = new(2);
    private bool _pickingImage;

    private async void InsertImage(object sender, RoutedEventArgs e)
    {
        if (_pickingImage || _model?.CanEditBody != true) return;
        _pickingImage = true;
        var model = _model; var document = Preview.Document; var id = model.Note!.Id;
        var start = Preview.Selection.Start; var end = Preview.Selection.End;
        var revision = model.DraftRevision;
        try
        {
            var operation = PickAsync(); ImageOperationTask = operation; await operation;
            async Task PickAsync()
            {
                var bytes = await ImageInput.PickAsync(_imageLoads?.Token ?? CancellationToken.None);
                if (bytes is null || !ReferenceEquals(model, _model) || model.Note?.Id != id || Preview.Document != document) return;
                await InsertBytesAsync(bytes, start, end, revision);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
        { if (_model == model && Preview.Document == document) EditorFeedback.Text = "The image could not be read safely. Your draft and selection are unchanged."; }
        finally { _pickingImage = false; }
    }
    private async Task InsertBytesAsync(byte[] bytes, TextPointer? start = null, TextPointer? end = null, long? selectedRevision = null)
    {
        if (_model?.CanEditBody != true) return;
        var model = _model; var document = Preview.Document; var id = model.Note!.Id;
        start ??= Preview.Selection.Start; end ??= Preview.Selection.End;
        selectedRevision ??= model.DraftRevision;
        bool replacing = start.CompareTo(end) != 0;
        if (replacing && selectedRevision != model.DraftRevision) throw new ArgumentException("The selected content changed. Select the insertion position again.");
        if (!replacing) start = end = start.GetPositionAtOffset(0, LogicalDirection.Backward)!;
        var paragraph = start.Paragraph;
        var trial = NotebookNode.Image(Guid.NewGuid(), Guid.NewGuid());
        NotebookEditorAdapter.InsertImage(Preview, trial, start, end, model.ValidateDocument, trialOnly: true);
        var operation = model.ImportImageAsync(id, bytes, image =>
        {
            if (!ReferenceEquals(_model, model) || Preview.Document != document || model.Note?.Id != id)
                throw new ArgumentException("The insertion position is no longer available.");
            if (paragraph is not null && paragraph.Parent is null ||
                (replacing || paragraph is null) && selectedRevision != model.DraftRevision)
                throw new ArgumentException("Content changed while importing. Your newer draft is preserved; choose the position again.");
            EnableReferenceHistory();
            NotebookEditorAdapter.InsertImage(Preview, image, start, end, model.ValidateDocument, CreateImageBlock);
            BodyChanged(Preview, null!); UpdateUndo();
        });
        ImageOperationTask = operation;
        bool inserted = await operation;
        if (_model != model || Preview.Document != document || model.Note?.Id != id) return;
        EditorFeedback.Text = inserted ? "Image inserted. Originals are retained when references are removed."
            : model.Message.Length == 0 ? "Image insertion cancelled; your selection is unchanged." : model.Message;
    }

    private BlockUIContainer CreateImageBlock(NotebookNode node)
    {
        var panel = new StackPanel();
        var border = new Border { Child = panel, BorderThickness = new(1), CornerRadius = new(5), Padding = new(6), Margin = new(0, 4, 0, 12) };
        border.SetResourceReference(Border.BorderBrushProperty, "PtjBorderBrush");
        border.SetResourceReference(Border.BackgroundProperty, "PtjSurfaceBrush");
        border.SetBinding(FrameworkElement.MaxWidthProperty, new Binding(nameof(ActualWidth)) { Source = Preview, Converter = new PreviewWidth() });
        var picture = new Image { Stretch = Stretch.Uniform, MaxHeight = 280, Visibility = Visibility.Collapsed };
        AutomationProperties.SetName(picture, node.AlternativeText!.Length == 0 ? "Notebook image" : node.AlternativeText);
        var status = new TextBlock { Text = "Loading image…", TextWrapping = TextWrapping.Wrap, Margin = new(4, 12, 4, 12) };
        status.SetResourceReference(TextBlock.ForegroundProperty, "PtjTextSecondaryBrush");
        panel.Children.Add(picture); panel.Children.Add(status);
        if (node.Caption!.Length > 0)
        {
            var caption = new TextBlock { Text = node.Caption, TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new(0, 5, 0, 4) };
            caption.SetResourceReference(TextBlock.ForegroundProperty, "PtjTextSecondaryBrush"); panel.Children.Add(caption);
        }
        var actions = new WrapPanel(); panel.Children.Add(actions);
        var original = Button("Open original", async () =>
        {
            if (_model?.Note is not { } note || _model.FindImage(node.ImageId!.Value) is not { } image || _model.ImagePresentation is not { } presentation) return;
            try { await presentation.ShowAsync(note.Id, image, _imageLoads?.Token ?? CancellationToken.None); }
            catch (OperationCanceledException) { }
            catch (Exception) { EditorFeedback.Text = "Original is missing, unreadable or requires an unavailable codec. The reference is preserved."; }
        });
        actions.Children.Add(original);
        var details = new StackPanel(); var expander = new Expander { Header = "Image actions", Content = details, Margin = new(4, 0, 0, 0) };
        expander.SetResourceReference(Control.ForegroundProperty, "PtjTextPrimaryBrush");
        panel.Children.Add(expander);
        var captionInput = new TextBox { Text = node.Caption, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, MaxLength = NotebookLimits.Caption,
            MaxHeight = 90, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new(0, 4, 0, 4) };
        AutomationProperties.SetName(captionInput, "Image caption"); details.Children.Add(captionInput);
        captionInput.SetBinding(TextBox.IsReadOnlyProperty, new Binding("BodyReadOnly"));
        var alternative = new TextBox { Text = node.AlternativeText, TextWrapping = TextWrapping.Wrap, MaxLength = NotebookLimits.AlternativeText, Margin = new(0, 0, 0, 4) };
        AutomationProperties.SetName(alternative, "Image alternative text"); details.Children.Add(alternative);
        alternative.SetBinding(TextBox.IsReadOnlyProperty, new Binding("BodyReadOnly"));
        var edits = new WrapPanel(); details.Children.Add(edits);
        var block = new BlockUIContainer(border) { Tag = node };
        AddEdit("Apply caption", () => NotebookEditorAdapter.CaptionImage(Preview, block, captionInput.Text, alternative.Text, CreateImageBlock));
        AddEdit("Move earlier", () => NotebookEditorAdapter.MoveImage(Preview, block, true));
        AddEdit("Move later", () => NotebookEditorAdapter.MoveImage(Preview, block, false));
        AddEdit("Remove reference", () => NotebookEditorAdapter.RemoveImage(Preview, block));
        void AddEdit(string label, Action edit)
        {
            var button = Button(label, () => { if (block.Parent is not null) Edit(edit); return Task.CompletedTask; });
            button.SetBinding(IsEnabledProperty, new Binding("CanEditBody")); edits.Children.Add(button);
        }
        if (_model?.Note is { } selected && _model.FindImage(node.ImageId!.Value) is { } metadata && _model.ImagePresentation is { } loader)
        {
            var token = _imageLoads?.Token ?? CancellationToken.None; var model = _model;
            var loading = LoadAsync(); ImageLoadsTask = Task.WhenAll(ImageLoadsTask, loading);
            async Task LoadAsync()
            {
                try
                {
                    await _previewSlots.WaitAsync(token);
                    try
                    {
                        var source = await loader.PreviewAsync(selected.Id, metadata, token);
                        if (token.IsCancellationRequested || _model != model || model.Note?.Id != selected.Id) return;
                        picture.Source = source; picture.Visibility = Visibility.Visible; status.Visibility = Visibility.Collapsed;
                    }
                    finally { _previewSlots.Release(); }
                }
                catch (OperationCanceledException) { }
                catch (Exception)
                {
                    if (!token.IsCancellationRequested) status.Text = "Image unavailable · original missing, corrupt or unsupported by the installed codec. Reference preserved.";
                }
            }
        }
        else status.Text = "Image preview unavailable. Reference preserved.";
        return block;
    }
    private static Button Button(string label, Func<Task> action)
    {
        var button = new Button { Content = label, FontSize = 12, Padding = new(6, 3, 6, 3), Margin = new(0, 2, 4, 2) };
        button.Click += async (_, _) => await action(); return button;
    }
    private sealed class PreviewWidth : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => Math.Max(1, (double)value - 28);
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
