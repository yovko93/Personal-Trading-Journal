using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Domain.TradingPlans;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.TradingPlans;

public sealed class TradingPlanReferenceReader(IDbContextFactory<JournalDbContext> factory, TimeProvider? clock = null) : ITradingPlanReferenceReader
{
    public async Task<TradingPlanReferenceCheck> CheckAsync(TradingPlanKey key, TradingPlanExpectedState expected,
        TradingPlanContent proposedContent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key); ArgumentNullException.ThrowIfNull(expected); ArgumentNullException.ThrowIfNull(proposedContent);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var tx = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: true);
        await db.Database.UseTransactionAsync(tx, cancellationToken);
        var row = await db.TradingPlans.AsNoTracking().SingleOrDefaultAsync(p => p.AccountId == key.AccountId && p.NewYorkDate == key.NewYorkDate, cancellationToken);
        if (row is null || row.Id != expected.PlanId || row.Version != expected.Version || row.DraftJson is null)
            return new(TradingPlanWriteStatus.Conflict, key, expected);
        var stored = TradingPlanJson.ReadDraft(row.DraftJson);
        TradingPlanReferences.ValidateStored(key.AccountId, stored.Content, row.ReferenceStateJson!);
        var baseline = TradingPlanJson.Deserialize<Dictionary<string, string>>(row.ReferenceStateJson!);
        var account = await db.TradingAccounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == key.AccountId, cancellationToken);
        TradingPlanAccountSnapshot? current = account is null ? null : new(account.Id, account.Name, account.Currency, account.UpdatedAtUtc);
        bool matches = account is { IsActive: true } && baseline[$"1:{key.AccountId:D}"] == TradingPlanReferences.Fingerprint(account);
        try
        {
            var refs = await TradingPlanReferences.Resolve(db, key, proposedContent, (clock ?? TimeProvider.System).GetUtcNow(), cancellationToken);
            refs.CheckPrevious(row.ReferenceStateJson!);
            return new(TradingPlanWriteStatus.Success, key, expected, current, matches, refs.Snapshot);
        }
        catch (PlanReferenceException e)
        {
            return new(e.Issue.State == TradingPlanReferenceState.Changed ? TradingPlanWriteStatus.ReferenceChanged : TradingPlanWriteStatus.ReferenceUnavailable,
                key, expected, current, matches, Issues: [e.Issue]);
        }
    }
}
