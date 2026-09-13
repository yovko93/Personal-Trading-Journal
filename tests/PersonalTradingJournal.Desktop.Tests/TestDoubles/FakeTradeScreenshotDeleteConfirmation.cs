using PersonalTradingJournal.Desktop.Screenshots;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradeScreenshotDeleteConfirmation :
    ITradeScreenshotDeleteConfirmation
{
    public bool Result { get; set; } = true;

    public int CallCount { get; private set; }

    public string? FileName { get; private set; }

    public bool Confirm(string fileName)
    {
        CallCount++;
        FileName = fileName;
        return Result;
    }
}
