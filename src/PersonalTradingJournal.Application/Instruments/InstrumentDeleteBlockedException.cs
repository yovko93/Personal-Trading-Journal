namespace PersonalTradingJournal.Application.Instruments;

public sealed class InstrumentDeleteBlockedException : Exception
{
    public InstrumentDeleteBlockedException(
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
