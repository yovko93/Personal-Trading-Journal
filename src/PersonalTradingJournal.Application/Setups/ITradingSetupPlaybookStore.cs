using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Application.Setups;

/// <summary>Future persistence boundary; M17.1 provides no implementation or registration.</summary>
public interface ITradingSetupPlaybookStore
{
    Task<TradingSetupPlaybookRevision?> GetLatestAsync(Guid tradingSetupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically append only if latest revision equals expectedRevision (0 means absent),
    /// and the owning Setup still exists, is active and has the supplied audit timestamp.
    /// Return false on a stale owner/revision, without writing. Never replace prior revisions.
    /// Honor cancellation before commit; do not report cancellation after a successful commit.
    /// </summary>
    Task<bool> TryAppendAsync(TradingSetupPlaybookRevision revision, int expectedRevision,
        DateTimeOffset expectedSetupUpdatedAtUtc, CancellationToken cancellationToken = default);
}
