using PersonalTradingJournal.Application.Screenshots;

namespace PersonalTradingJournal.Application.Trades;

/// <summary>Confirmation describes one exact Account and a fingerprint of its complete owned Trade graph.</summary>
public sealed record AccountTradeDeletionPlan(Guid AccountId, string AccountName, int TradeCount, string Fingerprint);
public enum AccountTradeDeletionStatus { Deleted, Changed, AccountUnavailable }
public sealed record AccountTradeDeletionCommit(AccountTradeDeletionStatus Status, int DeletedCount,
    IReadOnlyList<string> ScreenshotStorageKeys);
public sealed record AccountTradeDeletionResult(AccountTradeDeletionStatus Status, int DeletedCount, int FailedFileCleanupCount);

public interface IAccountTradeDeletionStore
{
    Task<AccountTradeDeletionPlan?> PrepareAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task<AccountTradeDeletionCommit> DeleteAsync(AccountTradeDeletionPlan confirmed, CancellationToken cancellationToken = default);
}

public sealed class DeleteAccountTradesUseCase(IAccountTradeDeletionStore store, ITradeScreenshotFileStorage files)
{
    public Task<AccountTradeDeletionPlan?> PrepareAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        store.PrepareAsync(accountId, cancellationToken);

    public async Task<AccountTradeDeletionResult> ExecuteAsync(AccountTradeDeletionPlan confirmed,
        CancellationToken cancellationToken = default)
    {
        var commit = await store.DeleteAsync(confirmed, cancellationToken);
        int failed = 0;
        // As for single-Trade deletion, files are compensating work AFTER commit. Never delete
        // attachments before a rollback is impossible, or abandon cleanup on late cancellation.
        if (commit.Status == AccountTradeDeletionStatus.Deleted)
            foreach (string key in commit.ScreenshotStorageKeys.Distinct(StringComparer.Ordinal))
                try { await files.DeleteIfExistsAsync(key, CancellationToken.None); }
                catch { failed++; }
        return new(commit.Status, commit.DeletedCount, failed);
    }
}
