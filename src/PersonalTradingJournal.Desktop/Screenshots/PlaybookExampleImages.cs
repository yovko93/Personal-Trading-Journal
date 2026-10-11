using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Desktop.ViewModels.Setups;
using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Desktop.Screenshots;

public interface IPlaybookExampleImages
{
    Task<PlaybookExampleItem?> PickAsync(CancellationToken token);
    Task<ImageSource> ThumbnailAsync(PlaybookExampleItem example, CancellationToken token);
    Task ShowAsync(PlaybookExampleItem example, CancellationToken token);
}

public sealed class PlaybookExampleImages(IPlaybookImageStorage images, ITradeScreenshotContentReader screenshots) : IPlaybookExampleImages
{
    public async Task<PlaybookExampleItem?> PickAsync(CancellationToken token)
    {
        await using var selection = new WpfTradeScreenshotFilePicker().Pick("Attach Playbook chart example");
        if (selection is null) return null;
        PlaybookExampleImage.ValidateFileName(selection.FileName);
        byte[] bytes = await ReadBoundedAsync(selection.Content, token);
        var thumbnail = await Task.Run(() => Decode(bytes, true), token);
        token.ThrowIfCancellationRequested();
        return new(selection.FileName, bytes, thumbnail);
    }

    public async Task<ImageSource> ThumbnailAsync(PlaybookExampleItem example, CancellationToken token)
    {
        var bytes = await ReadAsync(example, token);
        return await Task.Run(() => Decode(bytes, true), token);
    }

    public async Task ShowAsync(PlaybookExampleItem example, CancellationToken token)
    {
        var bytes = await ReadAsync(example, token);
        var bitmap = await Task.Run(() => Decode(bytes, false), token);
        token.ThrowIfCancellationRequested();
        var image = new Image { Source = bitmap, Stretch = Stretch.None, Focusable = true };
        AutomationProperties.SetName(image, example.KindLabel + ". " + example.Annotation);
        var close = new Button { Content = "Close image", IsCancel = true, Margin = new Thickness(8), MinHeight = 36 };
        close.SetResourceReference(FrameworkElement.StyleProperty, "PtjPlaybookCloseButtonStyle");
        var panel = new DockPanel(); DockPanel.SetDock(close, Dock.Bottom); panel.Children.Add(close);
        panel.Children.Add(new ScrollViewer { Content = image, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, PanningMode = PanningMode.Both, Focusable = true });
        var owner = System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        var window = new Window { Title = "Playbook chart · " + example.KindLabel, Content = panel, Owner = owner,
            Width = Math.Min(1000, SystemParameters.WorkArea.Width), Height = Math.Min(760, SystemParameters.WorkArea.Height),
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false };
        window.SetResourceReference(Control.BackgroundProperty, "PtjSurfaceBrush");
        window.SetResourceReference(Control.ForegroundProperty, "PtjTextPrimaryBrush");
        close.Click += (_, _) => window.Close();
        window.ContentRendered += (_, _) => close.Focus();
        window.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; window.Close(); } };
        window.ShowDialog();
    }

    private async Task<byte[]> ReadAsync(PlaybookExampleItem example, CancellationToken token)
    {
        if (example.PendingBytes is not null) return example.PendingBytes;
        if (example.Source?.Image is { } image) return await images.ReadAsync(image, token);
        if (example.Source?.SourceScreenshotId is not { } id) throw new FileNotFoundException("No image for this example.");
        var content = await screenshots.OpenAsync(id, token) ?? throw new FileNotFoundException("Example image unavailable.");
        await using var stream = content.Content;
        return await ReadBoundedAsync(stream, token);
    }

    internal static async Task<byte[]> ReadBoundedAsync(Stream source, CancellationToken token)
    {
        using var output = new MemoryStream();
        byte[] buffer = new byte[64 * 1024];
        int count;
        while ((count = await source.ReadAsync(buffer, token)) != 0)
        {
            if (output.Length + count > PlaybookExampleImage.MaximumBytes) throw new InvalidDataException("Image exceeds 8 MiB.");
            await output.WriteAsync(buffer.AsMemory(0, count), token);
        }
        token.ThrowIfCancellationRequested();
        if (output.Length == 0) throw new InvalidDataException("Empty image.");
        return output.ToArray();
    }

    internal static BitmapSource Decode(byte[] bytes, bool thumbnail)
    {
        if (bytes.Length is <= 0 or > PlaybookExampleImage.MaximumBytes) throw new InvalidDataException("Image size limit.");
        bool png = bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        bool jpeg = bytes.AsSpan().StartsWith(new byte[] { 255, 216, 255 });
        bool webp = bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8);
        if (!png && !jpeg && !webp) throw new InvalidDataException("Unsupported image content.");
        using var input = new MemoryStream(bytes, false);
        var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        if (decoder.Frames.Count != 1) throw new InvalidDataException("A single-frame image is required.");
        var frame = decoder.Frames[0];
        if (frame.PixelWidth <= 0 || frame.PixelHeight <= 0 || frame.PixelWidth > 8192 || frame.PixelHeight > 8192 ||
            (long)frame.PixelWidth * frame.PixelHeight > 16_000_000) throw new InvalidDataException("Image dimensions exceed the decode limit.");
        input.Position = 0;
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
        if (thumbnail) { if (frame.PixelWidth >= frame.PixelHeight) bitmap.DecodePixelWidth = Math.Min(384, frame.PixelWidth); else bitmap.DecodePixelHeight = Math.Min(384, frame.PixelHeight); }
        bitmap.StreamSource = input; bitmap.EndInit(); bitmap.Freeze(); return bitmap;
    }
}
