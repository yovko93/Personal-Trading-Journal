using PersonalTradingJournal.Desktop.Screenshots;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradeScreenshotFilePicker : ITradeScreenshotFilePicker
{
    public int CallCount { get; private set; }

    public TradeScreenshotFileSelection? Selection { get; set; }

    public Exception? Exception { get; set; }

    public TradeScreenshotFileSelection? Pick()
    {
        CallCount++;

        if (Exception is not null)
        {
            throw Exception;
        }

        return Selection;
    }
}
