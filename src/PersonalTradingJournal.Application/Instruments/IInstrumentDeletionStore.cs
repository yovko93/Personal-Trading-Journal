namespace PersonalTradingJournal.Application.Instruments;

public interface IInstrumentDeletionStore
{
    Task<bool> HasTradesAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default);
}
