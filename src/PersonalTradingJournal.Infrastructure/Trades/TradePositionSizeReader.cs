using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Trades;

/// <summary>Shared batched peak exposure read for browse and Calendar rows.</summary>
internal static class TradePositionSizeReader
{
    public static async Task<Dictionary<Guid, decimal>> GetAsync(JournalDbContext context,
        Guid[] tradeIds, CancellationToken cancellationToken)
    {
        if (tradeIds.Length == 0) return [];
        var executions = await context.TradeExecutions.AsNoTracking()
            .Where(execution => tradeIds.Contains(execution.TradeId))
            .OrderBy(execution => execution.Sequence)
            .Select(execution => new { execution.TradeId, execution.Side, execution.Quantity })
            .ToListAsync(cancellationToken);
        var sizes = new Dictionary<Guid, decimal>();
        foreach (var group in executions.GroupBy(execution => execution.TradeId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            decimal position = 0m;
            decimal peak = 0m;
            foreach (var execution in group)
            {
                cancellationToken.ThrowIfCancellationRequested();
                position = checked(position + (execution.Side == ExecutionSide.Buy
                    ? execution.Quantity : -execution.Quantity));
                peak = Math.Max(peak, Math.Abs(position));
            }
            sizes.Add(group.Key, peak);
        }
        return sizes;
    }
}
