using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PersonalTradingJournal.Desktop.Screenshots;

public sealed class WpfTradeScreenshotImageDecoder :
    ITradeScreenshotImageDecoder
{
    public ImageSource Decode(Stream content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!content.CanRead)
        {
            throw new ArgumentException(
                "Screenshot content must be readable.",
                nameof(content));
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = content;
        bitmap.EndInit();
        bitmap.Freeze();

        return bitmap;
    }
}
