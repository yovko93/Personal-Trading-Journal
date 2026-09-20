using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Mistakes;
using PersonalTradingJournal.Domain.Mistakes;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Mistakes;

public sealed class TradingMistakeStore(IDbContextFactory<JournalDbContext> contextFactory)
    : ITradingMistakeStore
{
    private readonly IDbContextFactory<JournalDbContext> _factory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    public async Task AddAsync(TradingMistake mistake, CancellationToken token = default)
    { ArgumentNullException.ThrowIfNull(mistake); await using JournalDbContext c = await _factory.CreateDbContextAsync(token); c.TradingMistakes.Add(TradingMistakePersistenceMapper.ToRecord(mistake)); await c.SaveChangesAsync(token); }
    public async Task<TradingMistake?> GetByIdAsync(Guid mistakeId, CancellationToken token = default)
    {
        if (mistakeId == Guid.Empty) throw new ArgumentException("A trading mistake identifier is required.", nameof(mistakeId));
        await using JournalDbContext c = await _factory.CreateDbContextAsync(token);
        TradingMistakeRecord? record = await c.TradingMistakes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == mistakeId, token);
        return record is null ? null : TradingMistakePersistenceMapper.ToDomain(record);
    }
    public async Task UpdateAsync(TradingMistake mistake, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(mistake); await using JournalDbContext c = await _factory.CreateDbContextAsync(token);
        c.TradingMistakes.Update(TradingMistakePersistenceMapper.ToRecord(mistake));
        await c.SaveChangesAsync(token);
    }
}
