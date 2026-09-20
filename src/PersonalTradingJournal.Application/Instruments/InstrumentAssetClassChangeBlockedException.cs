namespace PersonalTradingJournal.Application.Instruments;

public sealed class InstrumentAssetClassChangeBlockedException : Exception
{
    public InstrumentAssetClassChangeBlockedException(string message)
        : base(message)
    {
    }
}
