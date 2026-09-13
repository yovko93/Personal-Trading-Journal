using PersonalTradingJournal.Desktop.Screenshots;
using System.IO;
using System.Windows.Media.Imaging;

namespace PersonalTradingJournal.Desktop.Tests.Screenshots;

public sealed class WpfTradeScreenshotImageDecoderTests
{
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk" +
        "+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    [Fact]
    public void DecodeRejectsNullStream()
    {
        var decoder = new WpfTradeScreenshotImageDecoder();

        _ = Assert.Throws<ArgumentNullException>(() => decoder.Decode(null!));
    }

    [Fact]
    public void DecodeLoadsAndFreezesImageBeforeSourceStreamIsDisposed()
    {
        var decoder = new WpfTradeScreenshotImageDecoder();
        var stream = new MemoryStream(OnePixelPng);

        var image = Assert.IsType<BitmapImage>(decoder.Decode(stream));
        stream.Dispose();

        Assert.True(image.IsFrozen);
        Assert.Equal(1, image.PixelWidth);
        Assert.Equal(1, image.PixelHeight);
    }

    [Fact]
    public void DecodeRejectsCorruptImageContent()
    {
        var decoder = new WpfTradeScreenshotImageDecoder();
        using var stream = new MemoryStream([1, 2, 3, 4]);

        _ = Assert.ThrowsAny<Exception>(() => decoder.Decode(stream));
    }
}
