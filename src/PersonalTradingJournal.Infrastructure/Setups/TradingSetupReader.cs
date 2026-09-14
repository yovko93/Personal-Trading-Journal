using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Setups;

public sealed class TradingSetupReader(IDbContextFactory<JournalDbContext> contextFactory)
    : ITradingSetupReader
{
    private readonly IDbContextFactory<JournalDbContext> _contextFactory =
        contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));

    public async Task<IReadOnlyList<TradingSetupListItem>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.TradingSetups.AsNoTracking()
            .OrderByDescending(record => record.IsActive)
            .ThenBy(record => EF.Functions.Collate(record.Name, "NOCASE"))
            .ThenBy(record => record.Id)
            .Select(record => new TradingSetupListItem(record.Id, record.Name,
                record.Description, record.IsActive, record.CreatedAtUtc, record.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}
