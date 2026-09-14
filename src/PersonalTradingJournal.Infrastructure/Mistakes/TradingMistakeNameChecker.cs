using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Mistakes;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Mistakes;

public sealed class TradingMistakeNameChecker(IDbContextFactory<JournalDbContext> contextFactory)
    : ITradingMistakeNameChecker
{
    private readonly IDbContextFactory<JournalDbContext> _factory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    public async Task<bool> ExistsAsync(string normalizedName, Guid? excludingMistakeId = null,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(normalizedName); string comparison = normalizedName.Trim();
        await using JournalDbContext c = await _factory.CreateDbContextAsync(token);
        return await c.TradingMistakes.AsNoTracking().AnyAsync(x =>
            (!excludingMistakeId.HasValue || x.Id != excludingMistakeId.Value) &&
            EF.Functions.Collate(x.Name.Trim(), "NOCASE") == comparison, token);
    }
}
