using PersonalTradingJournal.Application.Trades;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradeExistenceReader : ITradeExistenceReader
{
    public bool Exists { get; set; } = true;

    public int CallCount { get; private set; }

    public Guid RequestedTradeId { get; private set; }

    public CancellationToken CancellationToken { get; private set; }

    public Exception? Exception { get; set; }

    public Task<bool> ExistsAsync(
        Guid tradeId,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        RequestedTradeId = tradeId;
        CancellationToken = cancellationToken;

        return Exception is null
            ? Task.FromResult(Exists)
            : Task.FromException<bool>(Exception);
    }
}
