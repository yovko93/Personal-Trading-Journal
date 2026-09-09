using PersonalTradingJournal.Domain.Instruments;

namespace PersonalTradingJournal.Application.Instruments;

public interface IInstrumentStore
{
    Task AddAsync(
        Instrument instrument,
        CancellationToken cancellationToken = default);
}
