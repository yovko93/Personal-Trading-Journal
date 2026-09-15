using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Mistakes;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Mistakes;

public sealed class TradeMistakeReader : ITradeMistakeReader
{
    private readonly IDbContextFactory<JournalDbContext> _contextFactory;

    public TradeMistakeReader(
        IDbContextFactory<JournalDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<TradeMistakeListItem>> GetByTradeIdAsync(
        Guid tradeId,
        CancellationToken cancellationToken = default)
    {
        if (tradeId == Guid.Empty)
        {
            throw new ArgumentException(
                "A trade identifier is required.",
                nameof(tradeId));
        }

        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await (
                from tradeMistake in context.TradeMistakes.AsNoTracking()
                join tradingMistake in context.TradingMistakes.AsNoTracking()
                    on tradeMistake.TradingMistakeId equals tradingMistake.Id
                where tradeMistake.TradeId == tradeId
                orderby EF.Functions.Collate(tradingMistake.Name, "NOCASE"),
                    tradingMistake.Id
                select new TradeMistakeListItem(
                    tradeMistake.Id,
                    tradeMistake.TradeId,
                    tradeMistake.TradingMistakeId,
                    tradingMistake.Name,
                    tradingMistake.IsActive,
                    tradeMistake.Note,
                    tradeMistake.CreatedAtUtc,
                    tradeMistake.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}
