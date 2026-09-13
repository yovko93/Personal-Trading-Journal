using Microsoft.Win32;
using System.IO;

namespace PersonalTradingJournal.Desktop.Screenshots;

public sealed class WpfTradeScreenshotFilePicker : ITradeScreenshotFilePicker
{
    public TradeScreenshotFileSelection? Pick()
    {
        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Filter =
                "Image files (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|" +
                "PNG files (*.png)|*.png|" +
                "JPEG files (*.jpg;*.jpeg)|*.jpg;*.jpeg|" +
                "WebP files (*.webp)|*.webp",
            Multiselect = false,
            Title = "Select Trade Screenshot",
        };

        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        var content = new FileStream(
            dialog.FileName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return new TradeScreenshotFileSelection(
            Path.GetFileName(dialog.FileName),
            content);
    }
}
