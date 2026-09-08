namespace PersonalTradingJournal.Application.Instruments;

public interface IInstrumentReader
{
    Task<IReadOnlyList<InstrumentListItem>> GetAllAsync(
        CancellationToken cancellationToken = default);
}
