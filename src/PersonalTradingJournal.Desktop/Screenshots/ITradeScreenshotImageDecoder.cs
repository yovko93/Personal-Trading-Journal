using System.IO;
using System.Windows.Media;

namespace PersonalTradingJournal.Desktop.Screenshots;

/// <summary>
/// Decodes screenshot content into an in-memory WPF image.
/// </summary>
public interface ITradeScreenshotImageDecoder
{
    /// <remarks>
    /// The caller owns <paramref name="content"/> and remains responsible for disposing it.
    /// </remarks>
    ImageSource Decode(Stream content);
}
