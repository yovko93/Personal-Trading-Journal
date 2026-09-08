using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Instruments;

public sealed class InstrumentReader : IInstrumentReader
{
    private readonly IDbContextFactory<JournalDbContext> _contextFactory;

    public InstrumentReader(IDbContextFactory<JournalDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);

        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<InstrumentListItem>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Instruments
            .AsNoTracking()
            .OrderBy(record => record.Symbol)
            .ThenBy(record => record.Id)
            .Select(record => new InstrumentListItem(
                record.Id,
                record.Symbol,
                record.DisplayName,
                record.AssetClass,
                record.Exchange,
                record.Currency,
                record.TickSize,
                record.TickValue,
                record.TickValue / record.TickSize,
                record.IsActive))
            .ToListAsync(cancellationToken);
    }
}
