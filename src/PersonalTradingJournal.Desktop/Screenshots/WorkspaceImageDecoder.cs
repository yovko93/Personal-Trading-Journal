using System.IO;
using System.Windows.Media.Imaging;
using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Desktop.Screenshots;

internal static class WorkspaceImageDecoder
{
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
