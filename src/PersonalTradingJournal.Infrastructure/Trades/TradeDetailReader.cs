using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Trades;

public sealed class TradeDetailReader : ITradeDetailReader
{
    private readonly IDbContextFactory<JournalDbContext> _contextFactory;

    public TradeDetailReader(IDbContextFactory<JournalDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);

        _contextFactory = contextFactory;
    }

    public async Task<TradeDetail?> GetByIdAsync(
        Guid tradeId,
        CancellationToken cancellationToken = default)
    {
        if (tradeId == Guid.Empty)
        {
            throw new ArgumentException(
                "A trade identifier cannot be empty.",
                nameof(tradeId));
        }

        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        var candidate = await (
                from tradeRecord in context.Trades.AsNoTracking()
                join accountRecord in context.TradingAccounts.AsNoTracking()
                    on tradeRecord.TradingAccountId equals accountRecord.Id
                join instrumentRecord in context.Instruments.AsNoTracking()
                    on tradeRecord.InstrumentId equals instrumentRecord.Id
                where tradeRecord.Id == tradeId
                select new
                {
                    TradeRecord = tradeRecord,
                    TradingAccountName = accountRecord.Name,
                    InstrumentSymbol = instrumentRecord.Symbol,
                    InstrumentDisplayName = instrumentRecord.DisplayName,
                })
            .SingleOrDefaultAsync(cancellationToken);

        if (candidate is null)
        {
            return null;
        }

        List<TradeExecutionRecord> executionRecords = await context.TradeExecutions
            .AsNoTracking()
            .Where(record => record.TradeId == tradeId)
            .OrderBy(record => record.Sequence)
            .ToListAsync(cancellationToken);
        Trade trade = TradePersistenceMapper.ToDomain(
            candidate.TradeRecord,
            executionRecords);
        IReadOnlyList<TradeExecutionDetailItem> executions = trade.Executions
            .Select(execution => new TradeExecutionDetailItem(
                execution.Id,
                execution.Sequence,
                execution.ExecutedAtUtc,
                execution.Side,
                execution.Quantity,
                execution.Price,
                execution.Commission,
                execution.Fees,
                execution.TotalCosts,
                execution.BrokerSymbol,
                execution.ExternalExecutionId,
                execution.ExternalOrderId))
            .ToList();

        return new TradeDetail(
            trade.Id,
            trade.TradingAccountId,
            candidate.TradingAccountName,
            trade.InstrumentId,
            candidate.InstrumentSymbol,
            candidate.InstrumentDisplayName,
            trade.Direction,
            trade.Status,
            trade.OpenedAtUtc,
            trade.ClosedAtUtc,
            trade.OpenQuantity,
            trade.AverageEntryPrice,
            trade.AverageExitPrice,
            trade.TotalCosts,
            trade.GrossPnL,
            trade.NetPnL,
            trade.Pricing.PointValue,
            trade.Pricing.Currency,
            executions);
    }
}
