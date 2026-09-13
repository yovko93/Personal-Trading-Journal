using PersonalTradingJournal.Desktop.Screenshots;
using System.IO;
using System.Windows.Media;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradeScreenshotImageDecoder :
    ITradeScreenshotImageDecoder
{
    public int CallCount { get; private set; }

    public Stream? DecodedContent { get; private set; }

    public ImageSource ImageToReturn { get; set; } = new DrawingImage();

    public Exception? Exception { get; set; }

    public ImageSource Decode(Stream content)
    {
        CallCount++;
        DecodedContent = content;

        if (Exception is not null)
        {
            throw Exception;
        }

        return ImageToReturn;
    }
}
