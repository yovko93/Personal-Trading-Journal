using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Desktop.Screenshots;

public interface INotebookImageInput
{
    Task<byte[]?> PickAsync(CancellationToken ct);
    byte[]? ClipboardImage(IDataObject data);
}

public sealed class WpfNotebookImageInput : INotebookImageInput
{
    public async Task<byte[]?> PickAsync(CancellationToken ct)
    {
        await using var selected = new WpfTradeScreenshotFilePicker().Pick("Insert Notebook image · WebP requires an installed decoder");
        return selected is null ? null : await PlaybookExampleImages.ReadBoundedAsync(selected.Content, ct);
    }
    public byte[]? ClipboardImage(IDataObject data)
    {
        // Explicit local formats only. No HTML/RTF conversion or remote image fetch.
        if (data.GetData("PNG", false) is MemoryStream encoded)
        {
            if (encoded.Length is <= 0 or > 8 * 1024 * 1024) throw new ArgumentException("Image exceeds 8 MiB.");
            return encoded.ToArray();
        }
        if (data.GetData(DataFormats.Bitmap, false) is not BitmapSource bitmap) return null;
        if (bitmap.PixelWidth is <= 0 or > 8192 || bitmap.PixelHeight is <= 0 or > 8192 ||
            (long)bitmap.PixelWidth * bitmap.PixelHeight > 16_000_000) throw new ArgumentException("Image dimensions exceed the limit.");
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = new MemoryStream(); encoder.Save(output);
        if (output.Length > 8 * 1024 * 1024) throw new ArgumentException("Image exceeds 8 MiB.");
        return output.ToArray(); // Lossless PNG when original encoded content is unavailable.
    }
}

public interface INotebookImagePresentation
{
    Task<ImageSource> PreviewAsync(Guid noteId, NotebookImage image, CancellationToken ct);
    Task ShowAsync(Guid noteId, NotebookImage image, CancellationToken ct);
}

public sealed class NotebookImagePresentation(INotebookImages images, ITradeScreenshotContentReader screenshots) : INotebookImagePresentation
{
    private async Task<byte[]> ReadAsync(Guid noteId, NotebookImage image, CancellationToken ct)
    {
        if (image.Ownership == NotebookImageOwnership.NoteOwned)
        {
            var result = await images.ReadOriginalAsync(noteId, image.Id, ct);
            if (result.Status != NotebookImageStatus.Success || result.Original is null) throw new IOException("Original is missing or unreadable.");
            return result.Original;
        }
        var content = await screenshots.OpenAsync(image.TradeScreenshotId!.Value, ct);
        if (content is null) throw new IOException("Trade screenshot is unavailable.");
        await using var stream = content.Content;
        if (content.Id != image.TradeScreenshotId || content.TradeId != image.TradeId) throw new IOException("Trade screenshot reference does not match.");
        return await PlaybookExampleImages.ReadBoundedAsync(stream, ct);
    }
    public async Task<ImageSource> PreviewAsync(Guid noteId, NotebookImage image, CancellationToken ct)
    {
        var bytes = await ReadAsync(noteId, image, ct);
        var preview = await Task.Run(() => PlaybookExampleImages.Decode(bytes, true), ct);
        ct.ThrowIfCancellationRequested(); return preview;
    }
    public async Task ShowAsync(Guid noteId, NotebookImage image, CancellationToken ct)
    {
        var bytes = await ReadAsync(noteId, image, ct);
        var bitmap = await Task.Run(() => PlaybookExampleImages.Decode(bytes, false), ct);
        ct.ThrowIfCancellationRequested();
        var picture = new Image { Source = bitmap, Stretch = Stretch.None };
        AutomationProperties.SetName(picture, image.AlternativeText.Length == 0 ? "Notebook original image" : image.AlternativeText);
        var close = new Button { Content = "Close image", IsCancel = true, Margin = new(8), MinHeight = 36 };
        var panel = new DockPanel(); DockPanel.SetDock(close, Dock.Bottom); panel.Children.Add(close);
        panel.Children.Add(new ScrollViewer { Content = picture, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto });
        var owner = System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        var window = new Window { Title = "Notebook original · " + image.Caption, Content = panel, Owner = owner,
            Width = Math.Min(1000, SystemParameters.WorkArea.Width), Height = Math.Min(760, SystemParameters.WorkArea.Height),
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false };
        window.SetResourceReference(Control.BackgroundProperty, "PtjSurfaceBrush");
        window.SetResourceReference(Control.ForegroundProperty, "PtjTextPrimaryBrush");
        close.Click += (_, _) => window.Close();
        window.ContentRendered += (_, _) => close.Focus();
        window.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; window.Close(); } };
        window.ShowDialog();
    }
}
