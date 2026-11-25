using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Application.Common.Time;
using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Imports.Tradovate;

/// <summary>Explicit maintenance only. Normal import retains its timestamp conflict guard.</summary>
public sealed class TradovateTimestampRepairService(IDbContextFactory<JournalDbContext> factory)
{
    public async Task<TradovateTimestampRepairPlan> PreviewAsync(
        TradovateTimestampRepairScope scope, byte[] fillsBytes, CancellationToken token = default)
    {
        await using var db = await factory.CreateDbContextAsync(token);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var plan = await InspectAsync(db, scope, fillsBytes, token);
        await transaction.RollbackAsync(token);
        return plan;
    }

    public async Task<TradovateTimestampRepairResult> ApplyAsync(
        TradovateTimestampRepairScope scope, byte[] fillsBytes, TradovateTimestampRepairPlan approved,
        IBackupArchiveService backups, string backupPath, CancellationToken token = default)
    {
        var preview = await PreviewAsync(scope, fillsBytes, token);
        if (preview.ScopeSha256 != approved.ScopeSha256 || preview.FillsSha256 != approved.FillsSha256 ||
            preview.AccountId != approved.AccountId)
            throw Blocked("Approval belongs to another repair scope.");
        if (preview.AlreadyApplied) return new(0, true, null);
        RequireApproved(preview, approved);

        var backup = await backups.CreateAsync(backupPath, token);
        if (!backup.Succeeded) throw Blocked("A verified complete backup is required before repair.");

        await using var db = await factory.CreateDbContextAsync(token);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        // The read transaction stays open through validation and writes. SQLite rejects stale
        // snapshot promotion if another writer committed; all changes then roll back together.
        var current = await InspectAsync(db, scope, fillsBytes, token);
        RequireApproved(current, approved);
        foreach (var change in current.Changes)
        {
            Guid key = Guid.Parse(change.Key);
            switch (change.Table)
            {
                case "TradeExecutions":
                    (await db.TradeExecutions.SingleAsync(e => e.Id == key, token)).ExecutedAtUtc = change.After;
                    break;
                case "TradovateImportedExecutions":
                    (await db.TradovateImportedExecutions.SingleAsync(e => e.TradeExecutionId == key, token)).SourceFillExecutedAtUtc = change.After;
                    break;
                case "TradeBrowse":
                    var browse = await db.TradeBrowse.SingleAsync(e => e.TradeId == key, token);
                    if (change.Field == "OpenedAtUtc") browse.OpenedAtUtc = change.After;
                    else browse.ClosedAtUtc = change.After;
                    break;
                default: throw Blocked("Unexpected repair field.");
            }
        }
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return new(current.Changes.Count(c => c.Table == "TradeExecutions"), false, backupPath);
    }

    private static void RequireApproved(TradovateTimestampRepairPlan current, TradovateTimestampRepairPlan approved)
    {
        if (Hash(current) != Hash(approved)) throw Blocked("Database or approved diff changed. Create a new dry run.");
    }

    private static async Task<TradovateTimestampRepairPlan> InspectAsync(
        JournalDbContext db, TradovateTimestampRepairScope scope, byte[] fillsBytes, CancellationToken token)
    {
        if (scope.AccountId == Guid.Empty || scope.Identities.Count == 0 ||
            scope.Identities.Select(i => i.ExecutionId).Distinct().Count() != scope.Identities.Count)
            throw Blocked("The baseline contains empty or duplicate identities.");
        string sourceHash = Convert.ToHexStringLower(SHA256.HashData(fillsBytes));
        if (!sourceHash.Equals(scope.FillsSha256, StringComparison.OrdinalIgnoreCase))
            throw Blocked("Fills source hash differs from the diagnosed file.");
        using var input = new MemoryStream(fillsBytes, false);
        var fills = await new TradovateCostCsvParser().ParseFillsAsync(input, token);
        if (fills.Select(f => f.Id).Distinct().Count() != fills.Count ||
            !fills.Select(f => f.Id).ToHashSet(StringComparer.Ordinal).SetEquals(scope.Identities.Select(i => i.FillId)))
            throw Blocked("Missing, extra or duplicate source Fill IDs.");
        var byFill = fills.ToDictionary(f => f.Id, StringComparer.Ordinal);
        foreach (var fill in fills)
        {
            var whole = fill.UtcTimestamp.AddTicks(-(fill.UtcTimestamp.Ticks % TimeSpan.TicksPerSecond));
            if (fill.Account != scope.ExternalAccountId || fill.LocalTimestamp != whole.AddHours(2).DateTime)
                throw Blocked("Account or verified UTC+02:00 source convention changed.");
        }
        var account = await db.TradingAccounts.AsNoTracking().SingleAsync(a => a.Id == scope.AccountId, token);
        if (account.ExternalAccountId != scope.ExternalAccountId || account.Currency != scope.Currency)
            throw Blocked("Account identity or currency changed.");

        var tradeIds = scope.Identities.Select(i => i.TradeId).Distinct().Order().ToArray();
        var trades = await db.Trades.AsNoTracking().Where(t => tradeIds.Contains(t.Id)).OrderBy(t => t.Id).ToArrayAsync(token);
        var executions = await db.TradeExecutions.AsNoTracking().Where(e => tradeIds.Contains(e.TradeId)).OrderBy(e => e.Id).ToArrayAsync(token);
        var ledger = await db.TradovateImportedExecutions.AsNoTracking().Where(e => tradeIds.Contains(e.TradeId)).OrderBy(e => e.TradeExecutionId).ToArrayAsync(token);
        var browses = await db.TradeBrowse.AsNoTracking().Where(e => tradeIds.Contains(e.TradeId)).OrderBy(e => e.TradeId).ToArrayAsync(token);
        if (trades.Length != tradeIds.Length || browses.Length != tradeIds.Length ||
            executions.Length != scope.Identities.Count || ledger.Length != executions.Length ||
            trades.Any(t => t.TradingAccountId != scope.AccountId || t.PricingCurrency != scope.Currency))
            throw Blocked("Trade scope or saved execution membership changed.");

        var changes = new List<TradovateTimestampFieldChange>();
        var beforeIds = new HashSet<Guid>();
        var afterIds = new HashSet<Guid>();
        foreach (var baseline in scope.Identities)
        {
            var fill = byFill[baseline.FillId];
            var execution = executions.SingleOrDefault(e => e.Id == baseline.ExecutionId);
            var identity = ledger.SingleOrDefault(e => e.TradeExecutionId == baseline.ExecutionId);
            if (execution is null || identity is null || execution.TradeId != baseline.TradeId ||
                identity.TradeId != baseline.TradeId || identity.TradingAccountIdAtImport != scope.AccountId ||
                identity.ExternalExecutionId != baseline.FillId || execution.ExternalExecutionId != baseline.FillId ||
                identity.AllocationIndex != baseline.AllocationIndex || execution.Side != baseline.Side ||
                identity.Side != baseline.Side || fill.Side != baseline.Side ||
                execution.BrokerSymbol != baseline.Symbol || identity.BrokerSymbol != baseline.Symbol || fill.Symbol != baseline.Symbol ||
                execution.Quantity != baseline.Quantity || identity.AllocatedQuantity != baseline.Quantity ||
                identity.SourceFillQuantity != baseline.SourceQuantity || fill.Quantity != baseline.SourceQuantity ||
                execution.Price != baseline.Price || identity.SourceFillPrice != baseline.Price || fill.Price != baseline.Price ||
                identity.SourceFillExecutedAtUtc != execution.ExecutedAtUtc)
                throw Blocked("Saved execution or source identity differs from the diagnosed baseline.");

            var floor = fill.UtcTimestamp.AddTicks(-(fill.UtcTimestamp.Ticks % TimeSpan.TicksPerSecond));
            var original = TradingTimePolicy.ConvertTradovateSourceToUtc(fill.LocalTimestamp);
            if (baseline.DiagnosedUtc.Offset != TimeSpan.Zero || !original.IsSuccess ||
                original.UtcTimestamp != baseline.DiagnosedUtc)
                throw Blocked("Baseline is not the diagnosed Sofia interpretation.");
            if (baseline.DiagnosedUtc == floor || baseline.DiagnosedUtc == fill.UtcTimestamp)
            {
                if (execution.ExecutedAtUtc != baseline.DiagnosedUtc)
                    throw Blocked("An unaffected whole-second record changed.");
                continue;
            }
            if (floor - baseline.DiagnosedUtc != TimeSpan.FromHours(1))
                throw Blocked("Baseline is outside the explicit one-hour repair mechanism.");
            if (execution.ExecutedAtUtc == fill.UtcTimestamp) afterIds.Add(baseline.ExecutionId);
            else if (execution.ExecutedAtUtc == baseline.DiagnosedUtc) beforeIds.Add(baseline.ExecutionId);
            else throw Blocked("A target timestamp differs from both baseline and repaired state.");

            changes.Add(new("TradeExecutions", baseline.ExecutionId.ToString(), "ExecutedAtUtc", baseline.DiagnosedUtc, fill.UtcTimestamp));
            changes.Add(new("TradovateImportedExecutions", baseline.ExecutionId.ToString(), "SourceFillExecutedAtUtc", baseline.DiagnosedUtc, fill.UtcTimestamp));
        }
        if (afterIds.Count > 0 && beforeIds.Count > 0) throw Blocked("Partially repaired state requires separate review.");
        foreach (var group in scope.Identities.GroupBy(i => i.FillId))
            if (group.Sum(i => i.Quantity) != byFill[group.Key].Quantity ||
                group.Select(i => i.AllocationIndex).Distinct().Count() != group.Count())
                throw Blocked("Fill allocations are ambiguous or do not conserve quantity.");

        bool already = afterIds.Count > 0;
        var changedTrades = scope.Identities.Where(i => beforeIds.Contains(i.ExecutionId) || afterIds.Contains(i.ExecutionId))
            .Select(i => i.TradeId).ToHashSet();
        foreach (var trade in trades)
        {
            var saved = executions.Where(e => e.TradeId == trade.Id).ToArray();
            var before = TradePersistenceMapper.ToDomain(trade, saved);
            var browse = browses.Single(b => b.TradeId == trade.Id);
            if (browse.OpenedAtUtc != before.OpenedAtUtc || browse.ClosedAtUtc != before.ClosedAtUtc)
                throw Blocked("Saved browse dates are stale. Review before repair.");
            foreach (var execution in saved)
            {
                var fix = changes.SingleOrDefault(c => c.Table == "TradeExecutions" && c.Key == execution.Id.ToString());
                if (fix is not null) execution.ExecutedAtUtc = fix.After;
            }
            var after = TradePersistenceMapper.ToDomain(trade, saved);
            if (before.GrossPnL != after.GrossPnL || before.NetPnL != after.NetPnL ||
                before.TotalCosts != after.TotalCosts || before.Status != after.Status || before.Direction != after.Direction)
                throw Blocked("Timestamp repair would change economics or position state.");
            if (!already)
            {
                if (before.OpenedAtUtc != after.OpenedAtUtc)
                    changes.Add(new("TradeBrowse", trade.Id.ToString(), "OpenedAtUtc", before.OpenedAtUtc, after.OpenedAtUtc));
                if (before.ClosedAtUtc != after.ClosedAtUtc)
                    changes.Add(new("TradeBrowse", trade.Id.ToString(), "ClosedAtUtc", before.ClosedAtUtc!.Value, after.ClosedAtUtc!.Value));
            }
        }
        string state = Hash(new { account, trades, ledger, browses,
            Executions = await db.TradeExecutions.AsNoTracking().Where(e => tradeIds.Contains(e.TradeId)).OrderBy(e => e.Id).ToArrayAsync(token) });
        return new(scope.AccountId, sourceHash, state, Hash(scope), executions.Length,
            changedTrades.Count, executions.Length - beforeIds.Count - afterIds.Count, already,
            already ? [] : changes.OrderBy(c => c.Table).ThenBy(c => c.Key).ThenBy(c => c.Field).ToArray());
    }

    private static string Hash<T>(T value) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    private static InvalidOperationException Blocked(string message) => new("Timestamp repair blocked. " + message);
}
