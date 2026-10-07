using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Journals;

/// <summary>One fresh, no-tracking projection for the visible Calendar; null aggregates all scopes.</summary>
public sealed class DailyJournalStatusReader : IDailyJournalStatusReader
{
    private readonly IDbContextFactory<JournalDbContext> _contextFactory;

    public DailyJournalStatusReader(IDbContextFactory<JournalDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<DailyJournalStatus>> GetAsync(
        DateOnly from,
        DateOnly through,
        Guid? tradingAccountId = null,
        CancellationToken cancellationToken = default)
    {
        if (through < from)
            throw new ArgumentException("The journal status range must end on or after its start date.", nameof(through));
        if (through.DayNumber - from.DayNumber + 1 > IDailyJournalStatusReader.MaximumVisibleDays)
            throw new ArgumentOutOfRangeException(nameof(through), "Journal status can be read for at most 42 visible dates at a time.");
        if (tradingAccountId == Guid.Empty)
            throw new ArgumentException("Use null for All accounts, or a non-empty Account ID.", nameof(tradingAccountId));

        cancellationToken.ThrowIfCancellationRequested();
        await using JournalDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.DailyJournals.AsNoTracking()
            .Where(j => j.TradingDate >= from && j.TradingDate <= through &&
                (tradingAccountId == null || j.TradingAccountId == tradingAccountId))
            .OrderBy(j => j.TradingDate)
            .ThenBy(j => j.Id)
            .Select(j => new DailyJournalStatus(j.Id, j.TradingDate, j.IsDraft, j.Revision, j.TradingAccountId))
            .ToArrayAsync(cancellationToken);
    }
}
