using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Domain.Instruments;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeInstrumentStore : IInstrumentStore
{
    public Instrument? InstrumentToReturn { get; set; }

    public Exception? AddException { get; set; }

    public Exception? GetException { get; set; }

    public Exception? UpdateException { get; set; }

    public int AddCallCount { get; private set; }

    public int GetCallCount { get; private set; }

    public int UpdateCallCount { get; private set; }

    public Instrument? AddedInstrument { get; private set; }

    public Instrument? UpdatedInstrument { get; private set; }

    public Task AddAsync(
        Instrument instrument,
        CancellationToken cancellationToken = default)
    {
        AddCallCount++;
        AddedInstrument = instrument;

        if (AddException is not null)
        {
            return Task.FromException(AddException);
        }

        InstrumentToReturn = instrument;
        return Task.CompletedTask;
    }

    public Task<Instrument?> GetByIdAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        GetCallCount++;

        if (GetException is not null)
        {
            return Task.FromException<Instrument?>(GetException);
        }

        Instrument? instrument = InstrumentToReturn?.Id == instrumentId
            ? InstrumentToReturn
            : null;

        return Task.FromResult(instrument);
    }

    public Task UpdateAsync(
        Instrument instrument,
        CancellationToken cancellationToken = default)
    {
        UpdateCallCount++;
        UpdatedInstrument = instrument;

        if (UpdateException is not null)
        {
            return Task.FromException(UpdateException);
        }

        InstrumentToReturn = instrument;
        return Task.CompletedTask;
    }
}
