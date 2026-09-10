using PersonalTradingJournal.Domain.Instruments;

namespace PersonalTradingJournal.Application.Instruments;

public interface IInstrumentStore
{
    Task AddAsync(
        Instrument instrument,
        CancellationToken cancellationToken = default);

    Task<Instrument?> GetByIdAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Instrument instrument,
        CancellationToken cancellationToken = default);
}
