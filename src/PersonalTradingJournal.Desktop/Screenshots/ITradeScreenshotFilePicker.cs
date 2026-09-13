using System.IO;

namespace PersonalTradingJournal.Desktop.Screenshots;

public interface ITradeScreenshotFilePicker
{
    TradeScreenshotFileSelection? Pick();
}

/// <summary>
/// Represents a screenshot selected by the user.
/// </summary>
/// <remarks>
/// The caller owns the selection and must dispose it after the screenshot workflow
/// finishes. Disposing the selection asynchronously disposes its content stream.
/// </remarks>
public sealed record TradeScreenshotFileSelection(
    string FileName,
    Stream Content) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
