using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Mistakes;
using PersonalTradingJournal.Domain.Mistakes;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Mistakes;

public sealed class TradeMistakeStore : ITradeMistakeStore
{
    private readonly IDbContextFactory<JournalDbContext> _contextFactory;

    public TradeMistakeStore(
        IDbContextFactory<JournalDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    public async Task<bool> ExistsAsync(
        Guid tradeId,
        Guid tradingMistakeId,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(
            tradeId,
            nameof(tradeId),
            "A trade identifier is required.");
        ValidateIdentifier(
            tradingMistakeId,
            nameof(tradingMistakeId),
            "A trading mistake identifier is required.");

        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.TradeMistakes
            .AsNoTracking()
            .AnyAsync(
                record => record.TradeId == tradeId &&
                    record.TradingMistakeId == tradingMistakeId,
                cancellationToken);
    }

    public async Task<TradeMistake?> GetByIdAsync(
        Guid tradeMistakeId,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(
            tradeMistakeId,
            nameof(tradeMistakeId),
            "A trade mistake identifier is required.");

        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);
        TradeMistakeRecord? record = await context.TradeMistakes
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == tradeMistakeId,
                cancellationToken);

        return record is null
            ? null
            : TradeMistakePersistenceMapper.ToDomain(record);
    }

    public async Task AddAsync(
        TradeMistake tradeMistake,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tradeMistake);

        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.TradeMistakes.Add(
            TradeMistakePersistenceMapper.ToRecord(tradeMistake));
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(
        Guid tradeMistakeId,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(
            tradeMistakeId,
            nameof(tradeMistakeId),
            "A trade mistake identifier is required.");

        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);
        TradeMistakeRecord record = await context.TradeMistakes
            .SingleOrDefaultAsync(
                candidate => candidate.Id == tradeMistakeId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Trade mistake '{tradeMistakeId}' could not be found.");
        context.TradeMistakes.Remove(record);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateIdentifier(
        Guid id,
        string parameterName,
        string message)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(message, parameterName);
        }
    }
}
