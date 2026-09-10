using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Trades;

public sealed class ManualTradeReferenceDataReader : IManualTradeReferenceDataReader
{
    private readonly IDbContextFactory<JournalDbContext> _contextFactory;

    public ManualTradeReferenceDataReader(
        IDbContextFactory<JournalDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);

        _contextFactory = contextFactory;
    }

    public async Task<ManualTradeReferenceData> GetAsync(
        bool includeInactiveReferences = false,
        CancellationToken cancellationToken = default)
    {
        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<TradingAccountRecord> accountQuery =
            context.TradingAccounts.AsNoTracking();
        IQueryable<InstrumentRecord> instrumentQuery =
            context.Instruments.AsNoTracking();

        if (!includeInactiveReferences)
        {
            accountQuery = accountQuery.Where(record => record.IsActive);
            instrumentQuery = instrumentQuery.Where(record => record.IsActive);
        }

        List<ManualTradeAccountOption> accounts = await accountQuery
            .OrderByDescending(record => record.IsActive)
            .ThenBy(record => EF.Functions.Collate(record.Name, "NOCASE"))
            .ThenBy(record => record.Id)
            .Select(record => new ManualTradeAccountOption(
                record.Id,
                record.Name,
                record.AccountType,
                record.ProviderName,
                record.ExternalAccountId,
                record.Currency,
                record.IsActive))
            .ToListAsync(cancellationToken);

        List<ManualTradeInstrumentOption> instruments = await instrumentQuery
            .OrderByDescending(record => record.IsActive)
            .ThenBy(record => record.Symbol)
            .ThenBy(record => record.Id)
            .Select(record => new ManualTradeInstrumentOption(
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

        return new ManualTradeReferenceData(accounts, instruments);
    }
}
