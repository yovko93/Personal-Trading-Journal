using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Rules;

namespace PersonalTradingJournal.Infrastructure.Rules;

public sealed partial class TradingRuleRepository
{
    public async Task<RuleMoveStatus> MoveAsync(MoveTradingRuleCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateId(command.RuleId);
        if (command.BeforeRuleId is Guid before) ValidateId(before);
        if (command.ExpectedCatalogVersion < 0) throw new ArgumentOutOfRangeException(nameof(command));
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await using var transaction = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
        await db.Database.UseTransactionAsync(transaction, cancellationToken);
        long version = await db.TradingRuleCatalogState.Select(s => s.Version).SingleAsync(cancellationToken);
        if (version != command.ExpectedCatalogVersion) return RuleMoveStatus.Conflict;
        var source = await db.TradingRulePositions.AsNoTracking().SingleOrDefaultAsync(p => p.RuleId == command.RuleId, cancellationToken);
        if (source is null) return RuleMoveStatus.Unavailable;
        long destination;
        if (command.BeforeRuleId is Guid target)
        {
            var next = await db.TradingRulePositions.AsNoTracking().SingleOrDefaultAsync(p => p.RuleId == target, cancellationToken);
            if (next is null) return RuleMoveStatus.Unavailable;
            destination = next.Position - (source.Position < next.Position ? 1 : 0);
        }
        else destination = await db.TradingRulePositions.LongCountAsync(cancellationToken) - 1;
        if (command.BeforeRuleId == command.RuleId || source.Position == destination) return RuleMoveStatus.Saved;
        // Positions are dense and changed in one statement inside the writer transaction.
        // A nonunique ordering index permits this permutation without transient uniqueness violations.
        long start = Math.Min(source.Position, destination), end = Math.Max(source.Position, destination);
        long shift = source.Position < destination ? -1 : 1;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE TradingRulePositions
            SET Position = CASE WHEN RuleId = {command.RuleId} THEN {destination} ELSE Position + {shift} END
            WHERE Position BETWEEN {start} AND {end};
            """, cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE TradingRuleCatalogState SET Version = {checked(version + 1)} WHERE Id = 1;", cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await transaction.CommitAsync(CancellationToken.None);
        return RuleMoveStatus.Saved;
    }
}
