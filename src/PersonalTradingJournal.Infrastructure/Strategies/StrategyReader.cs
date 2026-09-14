using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Strategies;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Strategies;

public sealed class StrategyReader : IStrategyReader
{
    private readonly IDbContextFactory<JournalDbContext> _contextFactory;

    public StrategyReader(IDbContextFactory<JournalDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<StrategyListItem>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Strategies
            .AsNoTracking()
            .OrderByDescending(record => record.IsActive)
            .ThenBy(record => EF.Functions.Collate(record.Name, "NOCASE"))
            .ThenBy(record => record.Id)
            .Select(record => new StrategyListItem(
                record.Id,
                record.Name,
                record.Description,
                record.IsActive,
                record.CreatedAtUtc,
                record.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}
