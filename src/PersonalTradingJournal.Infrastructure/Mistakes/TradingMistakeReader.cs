using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Mistakes;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Mistakes;

public sealed class TradingMistakeReader(IDbContextFactory<JournalDbContext> contextFactory)
    : ITradingMistakeReader
{
    private readonly IDbContextFactory<JournalDbContext> _factory =
        contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    public async Task<IReadOnlyList<TradingMistakeListItem>> GetAllAsync(CancellationToken token = default)
    {
        await using JournalDbContext context = await _factory.CreateDbContextAsync(token);
        return await context.TradingMistakes.AsNoTracking().OrderByDescending(x => x.IsActive)
            .ThenBy(x => EF.Functions.Collate(x.Name, "NOCASE")).ThenBy(x => x.Id)
            .Select(x => new TradingMistakeListItem(x.Id, x.Name, x.Description, x.IsActive,
                x.CreatedAtUtc, x.UpdatedAtUtc)).ToListAsync(token);
    }

    public async Task<TradingMistakeDetails?> GetByIdAsync(
        Guid mistakeId,
        CancellationToken cancellationToken = default)
    {
        await using JournalDbContext context =
            await _factory.CreateDbContextAsync(cancellationToken);
        return await context.TradingMistakes.AsNoTracking()
            .Where(record => record.Id == mistakeId)
            .Select(record => new TradingMistakeDetails(
                record.Id,
                record.Name,
                record.Description,
                record.IsActive,
                record.CreatedAtUtc,
                record.UpdatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
