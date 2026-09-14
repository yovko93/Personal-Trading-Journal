using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Strategies;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Strategies;

public sealed class StrategyNameChecker : IStrategyNameChecker
{
    private readonly IDbContextFactory<JournalDbContext> _contextFactory;

    public StrategyNameChecker(IDbContextFactory<JournalDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    public async Task<bool> ExistsAsync(
        string normalizedName,
        Guid? excludingStrategyId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(normalizedName);
        string comparisonName = normalizedName.Trim();

        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Strategies
            .AsNoTracking()
            .AnyAsync(
                record =>
                    (!excludingStrategyId.HasValue || record.Id != excludingStrategyId.Value) &&
                    EF.Functions.Collate(record.Name.Trim(), "NOCASE") == comparisonName,
                cancellationToken);
    }
}
