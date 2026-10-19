using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.TradingPlans;

public sealed class DailyTradingPrincipleStore(IDbContextFactory<JournalDbContext> factory, TimeProvider? clock = null) : IDailyTradingPrincipleStore
{
    public async Task<DailyTradingPrinciples> ReadAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var tx = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: true);
        await db.Database.UseTransactionAsync(tx, cancellationToken);
        return await Read(db, cancellationToken);
    }
    private static async Task<DailyTradingPrinciples> Read(JournalDbContext db, CancellationToken ct)
    {
        var state = await db.DailyTradingPrincipleState.AsNoTracking().SingleAsync(ct);
        var rows = await db.DailyTradingPrinciples.AsNoTracking().OrderBy(x => x.Position).ThenBy(x => x.Id)
            .Take(DailyTradingPrincipleLimits.MaximumEntries + 1).ToArrayAsync(ct);
        if (rows.Length > DailyTradingPrincipleLimits.MaximumEntries || rows.Any(x => !DailyTradingPrincipleLimits.IsValid(x.Text)))
            throw new InvalidDataException("Invalid reminder list.");
        return new(state.Version, rows.Select(x => new DailyTradingPrinciple(x.Id, x.Text, x.Position, x.CreatedAtUtc)).ToArray());
    }
    public Task<PrincipleWriteResult> AddAsync(string text, long expectedVersion, CancellationToken cancellationToken = default) =>
        DailyTradingPrincipleLimits.IsValid(text) ? Write(text, null, expectedVersion, cancellationToken) : Task.FromResult(new PrincipleWriteResult(PrincipleWriteStatus.Invalid));
    public Task<PrincipleWriteResult> RemoveAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default) =>
        id != Guid.Empty ? Write(null, id, expectedVersion, cancellationToken) : Task.FromResult(new PrincipleWriteResult(PrincipleWriteStatus.Invalid));

    private async Task<PrincipleWriteResult> Write(string? text, Guid? remove, long expected, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            await using var db = await factory.CreateDbContextAsync(ct);
            await db.Database.OpenConnectionAsync(ct);
            // Reserve the writer before reading the catalog token and append position.
            await using var tx = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
            await db.Database.UseTransactionAsync(tx, ct);
            var state = await db.DailyTradingPrincipleState.SingleAsync(ct);
            if (state.Version != expected) return new(PrincipleWriteStatus.Conflict);
            if (remove is { } id)
            {
                var row = await db.DailyTradingPrinciples.SingleOrDefaultAsync(x => x.Id == id, ct);
                if (row is null) return new(PrincipleWriteStatus.Conflict);
                db.Remove(row);
            }
            else
            {
                if (await db.DailyTradingPrinciples.CountAsync(ct) >= DailyTradingPrincipleLimits.MaximumEntries)
                    return new(PrincipleWriteStatus.Invalid);
                db.DailyTradingPrinciples.Add(new() { Id = Guid.NewGuid(), Text = text!, Position = checked(state.Version + 1), CreatedAtUtc = (clock ?? TimeProvider.System).GetUtcNow() });
            }
            state.Version = checked(state.Version + 1);
            await db.SaveChangesAsync(ct);
            var verified = await Read(db, ct);
            ct.ThrowIfCancellationRequested();
            await tx.CommitAsync(ct);
            // Once committed, publish this exact snapshot even if cancellation arrives afterward.
            return new(PrincipleWriteStatus.Success, verified);
        }
        catch (OperationCanceledException) { return new(PrincipleWriteStatus.Cancelled); }
        catch (Exception ex) when (ex is SqliteException or DbUpdateException or IOException or InvalidOperationException or OverflowException)
        { return new(PrincipleWriteStatus.StorageFailure); }
    }
}
