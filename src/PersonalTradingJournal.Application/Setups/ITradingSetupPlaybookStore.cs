using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Application.Setups;

/// <summary>Current guidance under the existing Setup identity, with a legacy append boundary for compatibility.</summary>
public interface ITradingSetupPlaybookStore
{
    Task<TradingSetupPlaybookRevision?> GetLatestAsync(Guid tradingSetupId, CancellationToken cancellationToken = default);

    /// <summary>Replace the current definition under its stable Setup identity. Revision is an internal
    /// optimistic token, not a published revision. Historical captures must remain unchanged.</summary>
    Task<bool> TrySaveCurrentAsync(TradingSetupPlaybookRevision definition, int expectedVersion,
        DateTimeOffset expectedSetupUpdatedAtUtc, CancellationToken cancellationToken = default) =>
        TryAppendAsync(definition, expectedVersion, expectedSetupUpdatedAtUtc, cancellationToken);

    /// <summary>
    /// Legacy import/fixture compatibility, not the active authoring path. Atomically append only if latest revision equals expectedRevision (0 means absent),
    /// and the owning Setup still exists, is active and has the supplied audit timestamp.
    /// Return false on a stale owner/revision, without writing. Never replace prior revisions.
    /// Honor cancellation before commit; do not report cancellation after a successful commit.
    /// </summary>
    Task<bool> TryAppendAsync(TradingSetupPlaybookRevision revision, int expectedRevision,
        DateTimeOffset expectedSetupUpdatedAtUtc, CancellationToken cancellationToken = default);
}
