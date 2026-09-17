using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Mistakes;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Mistakes;

public sealed class TradingMistakeDeletionStore(
    IDbContextFactory<JournalDbContext> contextFactory)
    : ITradingMistakeDeletionStore
{
    private const int SqliteConstraintErrorCode = 19;
    private readonly IDbContextFactory<JournalDbContext> _contextFactory =
        contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));

    public async Task<bool> HasTradeMistakesAsync(
        Guid mistakeId,
        CancellationToken cancellationToken = default)
    {
        ValidateMistakeId(mistakeId);
        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.TradeMistakes.AsNoTracking().AnyAsync(
            association => association.TradingMistakeId == mistakeId,
            cancellationToken);
    }

    public async Task DeleteAsync(
        Guid mistakeId,
        CancellationToken cancellationToken = default)
    {
        ValidateMistakeId(mistakeId);
        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);
        TradingMistakeRecord? record = await context.TradingMistakes
            .SingleOrDefaultAsync(
                candidate => candidate.Id == mistakeId,
                cancellationToken);
        if (record is null)
        {
            throw new KeyNotFoundException(
                $"Trading mistake '{mistakeId}' was not found.");
        }

        context.TradingMistakes.Remove(record);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqliteException
            {
                SqliteErrorCode: SqliteConstraintErrorCode,
            })
        {
            throw new TradingMistakeDeleteBlockedException(
                "The trading mistake is referenced by existing trade mistakes.",
                exception);
        }
    }

    private static void ValidateMistakeId(Guid mistakeId)
    {
        if (mistakeId == Guid.Empty)
        {
            throw new ArgumentException(
                "A trading mistake identifier is required.",
                nameof(mistakeId));
        }
    }
}
