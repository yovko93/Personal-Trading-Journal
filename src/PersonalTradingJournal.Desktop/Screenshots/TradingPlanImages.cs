using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Desktop.Screenshots;

public sealed record PlanImageSelection(string FileName, byte[] Bytes, ImageSource Thumbnail);
public interface ITradingPlanImages
{
    Task<PlanImageSelection?> PickAsync(CancellationToken token);
    Task<PlaybookExampleImage> StoreAsync(PlanImageSelection selection, CancellationToken token);
    Task<ImageSource> ThumbnailAsync(PlaybookExampleImage image, CancellationToken token);
    Task ShowAsync(PlaybookExampleImage? image, PlanImageSelection? pending, string label, CancellationToken token);
    Task CleanupAsync(string key);
}

/// <summary>Reuses bounded, OnLoad decoding and immutable screenshot storage. No image handles survive decoding.</summary>
public sealed class TradingPlanImages(IPlaybookImageStorage storage) : ITradingPlanImages
{
    public async Task<PlanImageSelection?> PickAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var dialog = new OpenFileDialog { Title = "Upload Trading Plan screenshot", CheckFileExists = true,
            Filter = "PNG or JPEG images|*.png;*.jpg;*.jpeg", Multiselect = false };
        if (dialog.ShowDialog() != true) return null;
        await using var input = new FileStream(dialog.FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] bytes = await PlaybookExampleImages.ReadBoundedAsync(input, token);
        var bitmap = await Task.Run(() => PlaybookExampleImages.Decode(bytes, true), token);
        token.ThrowIfCancellationRequested();
        return new(Path.GetFileName(dialog.FileName), bytes, bitmap);
    }
    public Task<PlaybookExampleImage> StoreAsync(PlanImageSelection selection, CancellationToken token) =>
        storage.StoreAsync(selection.FileName, selection.Bytes, token);
    public async Task<ImageSource> ThumbnailAsync(PlaybookExampleImage image, CancellationToken token)
    {
        var bytes = await storage.ReadAsync(image, token);
        return await Task.Run(() => PlaybookExampleImages.Decode(bytes, true), token);
    }
    public Task CleanupAsync(string key) => storage.DeleteUnreferencedAsync(key, CancellationToken.None);
    public async Task ShowAsync(PlaybookExampleImage? image, PlanImageSelection? pending, string label, CancellationToken token)
    {
        var bytes = pending?.Bytes ?? await storage.ReadAsync(image ?? throw new InvalidDataException("Missing image."), token);
        var bitmap = await Task.Run(() => PlaybookExampleImages.Decode(bytes, false), token);
        token.ThrowIfCancellationRequested();
        var chart = new Image { Source = bitmap, Stretch = Stretch.None, Focusable = true };
        System.Windows.Automation.AutomationProperties.SetName(chart, label);
        var close = new Button { Content = "Close image", IsCancel = true, Margin = new(8) };
        close.SetResourceReference(FrameworkElement.StyleProperty, "PtjPlaybookCloseButtonStyle");
        var panel = new DockPanel(); DockPanel.SetDock(close, Dock.Bottom); panel.Children.Add(close);
        panel.Children.Add(new ScrollViewer { Content = chart, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var window = new Window { Title = label, Content = panel, Width = Math.Min(1000, SystemParameters.WorkArea.Width),
            Height = Math.Min(760, SystemParameters.WorkArea.Height), ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) };
        window.SetResourceReference(Control.BackgroundProperty, "PtjSurfaceBrush");
        window.SetResourceReference(Control.ForegroundProperty, "PtjTextPrimaryBrush");
        close.Click += (_, _) => window.Close(); window.ContentRendered += (_, _) => close.Focus();
        window.ShowDialog();
    }
}
