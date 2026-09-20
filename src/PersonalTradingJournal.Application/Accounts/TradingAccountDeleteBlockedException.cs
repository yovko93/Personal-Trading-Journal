namespace PersonalTradingJournal.Application.Accounts;

public sealed class TradingAccountDeleteBlockedException : Exception
{
    public TradingAccountDeleteBlockedException(
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
