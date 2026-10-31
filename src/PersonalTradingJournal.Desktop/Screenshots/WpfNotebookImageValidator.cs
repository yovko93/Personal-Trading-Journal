using System.IO;
using System.Windows.Media.Imaging;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Desktop.Screenshots;

/// <summary>Installed-codec admission only, shared with the established Playbook decoder policy. No Notebook UI.</summary>
public sealed class WpfNotebookImageValidator : INotebookImageValidator
{
    private static readonly object DecodeGate = new();
    public NotebookImageProbe Validate(ReadOnlyMemory<byte> content)
    {
        if (content.Length is <= 0 or > NotebookLimits.ImageBytes) return new(NotebookImageStatus.TooLarge);
        lock (DecodeGate)
        {
            try
            {
                byte[] bytes = content.ToArray();
                string? extension = NotebookImageAdmission.Extension(bytes);
                if (extension is null) return new(NotebookImageStatus.Unsupported);
                if (NotebookImageAdmission.HasAnimation(bytes, extension)) return new(NotebookImageStatus.Unsupported);
                using var input = new MemoryStream(bytes, false);
                var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                if (decoder.Frames.Count != 1) return new(NotebookImageStatus.Unsupported);
                var frame = decoder.Frames[0];
                if (frame.PixelWidth <= 0 || frame.PixelHeight <= 0) return new(NotebookImageStatus.Invalid);
                if (frame.PixelWidth > 8192 || frame.PixelHeight > 8192 || (long)frame.PixelWidth * frame.PixelHeight > 16_000_000)
                    return new(NotebookImageStatus.TooLarge);
                var bitmap = PlaybookExampleImages.Decode(bytes, false);
                // Force pixel materialization; successful header parsing alone is not image admission.
                int stride = checked((bitmap.PixelWidth * bitmap.Format.BitsPerPixel + 7) / 8);
                bitmap.CopyPixels(new byte[checked(stride * bitmap.PixelHeight)], stride, 0);
                return new(NotebookImageStatus.Success, extension, bitmap.PixelWidth, bitmap.PixelHeight);
            }
            catch (NotSupportedException) { return new(NotebookImageStatus.Unsupported); }
            catch (Exception ex) when (ex is IOException or FormatException or ArgumentException or InvalidOperationException or System.Runtime.InteropServices.COMException or OverflowException)
            { return new(NotebookImageStatus.Invalid); }
        }
    }
}
