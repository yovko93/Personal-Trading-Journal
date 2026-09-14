using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Setups;

public sealed class TradingSetupNameChecker(IDbContextFactory<JournalDbContext> contextFactory)
    : ITradingSetupNameChecker
{
    private readonly IDbContextFactory<JournalDbContext> _contextFactory =
        contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));

    public async Task<bool> ExistsAsync(string normalizedName, Guid? excludingSetupId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(normalizedName);
        string comparisonName = normalizedName.Trim();
        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.TradingSetups.AsNoTracking().AnyAsync(
            record => (!excludingSetupId.HasValue || record.Id != excludingSetupId.Value) &&
                EF.Functions.Collate(record.Name.Trim(), "NOCASE") == comparisonName,
            cancellationToken);
    }
}
