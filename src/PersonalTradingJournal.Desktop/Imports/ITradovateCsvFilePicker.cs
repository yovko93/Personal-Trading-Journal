using System.IO;

namespace PersonalTradingJournal.Desktop.Imports;

public interface ITradovateCsvFilePicker
{
    TradovateCsvFileSelection? Pick();
}

/// <summary>
/// A selected CSV stream. The caller owns and must dispose the selection.
/// </summary>
public sealed record TradovateCsvFileSelection(
    string FileName,
    Stream Content) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
