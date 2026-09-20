namespace PersonalTradingJournal.Application.Instruments;

public interface IInstrumentReader
{
    Task<IReadOnlyList<InstrumentListItem>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<InstrumentDetails?> GetByIdAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default);
}
