namespace PersonalTradingJournal.Application.Mistakes;

public sealed class TradingMistakeDeleteBlockedException : Exception
{
    public TradingMistakeDeleteBlockedException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
