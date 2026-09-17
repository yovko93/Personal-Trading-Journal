namespace PersonalTradingJournal.Application.Setups;

public sealed class TradingSetupDeleteBlockedException : Exception
{
    public TradingSetupDeleteBlockedException(
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
