using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Trades;

public sealed class AccountTradeDeletionStore(IDbContextFactory<JournalDbContext> factory) : IAccountTradeDeletionStore
{
    public async Task<AccountTradeDeletionPlan?> PrepareAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        if (accountId == Guid.Empty) throw new ArgumentException("An exact Account is required.", nameof(accountId));
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        // A coherent read snapshot; preparation never changes data or files.
        await using var transaction = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: true);
        await db.Database.UseTransactionAsync(transaction, cancellationToken);
        return (await ReadAsync(db, accountId, cancellationToken))?.Plan;
    }

    public async Task<AccountTradeDeletionCommit> DeleteAsync(AccountTradeDeletionPlan confirmed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(confirmed);
        if (confirmed.AccountId == Guid.Empty) throw new ArgumentException("An exact Account is required.", nameof(confirmed));
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        // BEGIN IMMEDIATE takes SQLite's writer reservation BEFORE rechecking the graph.
        // Imports/writers that committed first invalidate confirmation. Writers queued behind
        // this transaction can add Trades afterward, but those new Trades are never swept up.
        await using var transaction = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
        await db.Database.UseTransactionAsync(transaction, cancellationToken);
        var current = await ReadAsync(db, confirmed.AccountId, cancellationToken);
        if (current is null) return new(AccountTradeDeletionStatus.AccountUnavailable, 0, []);
        if (current.Plan != confirmed) return new(AccountTradeDeletionStatus.Changed, 0, []);
        if (confirmed.TradeCount == 0) return new(AccountTradeDeletionStatus.Deleted, 0, []);

        var targets = db.Trades.Where(t => t.TradingAccountId == confirmed.AccountId).Select(t => t.Id);
        // Same ownership rules as TradeDeletionStore: restricted children first; executions,
        // browse projection and both import ledgers cascade with the Trade in the same transaction.
        await db.TradeMistakes.Where(m => targets.Contains(m.TradeId)).ExecuteDeleteAsync(cancellationToken);
        await db.TradeScreenshots.Where(s => targets.Contains(s.TradeId)).ExecuteDeleteAsync(cancellationToken);
        int count = await db.Trades.Where(t => t.TradingAccountId == confirmed.AccountId).ExecuteDeleteAsync(cancellationToken);
        if (count != confirmed.TradeCount) throw new InvalidOperationException("Trade set changed during deletion.");
        // Storage keys are normally unique GUID filenames. Protect surviving references even
        // if a legacy/imported database contains shared keys.
        var retainedKeys = await db.TradeScreenshots.Select(s => s.StorageKey).Distinct().ToArrayAsync(cancellationToken);
        var cleanup = current.StorageKeys.Except(retainedKeys, StringComparer.OrdinalIgnoreCase).ToArray();
        await transaction.CommitAsync(cancellationToken);
        return new(AccountTradeDeletionStatus.Deleted, count, cleanup);
    }

    private sealed record Snapshot(AccountTradeDeletionPlan Plan, string[] StorageKeys);

    private static async Task<Snapshot?> ReadAsync(JournalDbContext db, Guid accountId, CancellationToken token)
    {
        TradingAccountRecord? account = await db.TradingAccounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == accountId, token);
        if (account is null) return null;
        var trades = await db.Trades.AsNoTracking().Where(t => t.TradingAccountId == accountId).OrderBy(t => t.Id).ToArrayAsync(token);
        var ids = db.Trades.Where(t => t.TradingAccountId == accountId).Select(t => t.Id);
        var executions = await db.TradeExecutions.AsNoTracking().Where(e => ids.Contains(e.TradeId)).OrderBy(e => e.Id).ToArrayAsync(token);
        var browse = await db.TradeBrowse.AsNoTracking().Where(t => ids.Contains(t.TradeId)).OrderBy(t => t.TradeId).ToArrayAsync(token);
        var mistakes = await db.TradeMistakes.AsNoTracking().Where(m => ids.Contains(m.TradeId)).OrderBy(m => m.Id).ToArrayAsync(token);
        var screenshots = await db.TradeScreenshots.AsNoTracking().Where(s => ids.Contains(s.TradeId)).OrderBy(s => s.Id).ToArrayAsync(token);
        var tradovate = await db.TradovateImportedExecutions.AsNoTracking().Where(e => ids.Contains(e.TradeId)).OrderBy(e => e.TradeExecutionId).ToArrayAsync(token);
        var topstep = await db.TopstepImportedRows.AsNoTracking().Where(e => ids.Contains(e.TradeId)).OrderBy(e => e.Id).ToArrayAsync(token);
        // Include full owned records, not only timestamps (several writes may share a clock tick).
        // This digest is transient confirmation state, never logged or persisted.
        byte[] canonical = JsonSerializer.SerializeToUtf8Bytes(new { account, trades, executions, browse, mistakes, screenshots, tradovate, topstep });
        return new(new(account.Id, account.Name, trades.Length, Convert.ToHexString(SHA256.HashData(canonical))),
            screenshots.Select(s => s.StorageKey).Distinct(StringComparer.Ordinal).ToArray());
    }
}
