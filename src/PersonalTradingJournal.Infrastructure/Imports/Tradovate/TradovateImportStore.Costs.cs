using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Imports.Tradovate;

public sealed partial class TradovateImportStore
{
    private static async Task<int> UpdateExistingCostsAsync(JournalDbContext context, TradovateImportRequest request,
        IReadOnlyList<Guid> duplicateIds, TradovateImportedExecutionRecord[] ledger, TradovateVerifiedCosts costs,
        CancellationToken token)
    {
        int updated = 0;
        foreach (Guid id in duplicateIds)
        {
            var record = await context.Trades.SingleAsync(t => t.Id == id, token);
            var executions = await context.TradeExecutions.Where(e => e.TradeId == id).ToArrayAsync(token);
            var identities = ledger.Where(e => e.TradeId == id).ToArray();
            var candidate = request.Preparation.PreparedCandidates.Single(c => c.OrderedExecutions.Any(e =>
                identities.Any(i => i.ExternalExecutionId == e.ExternalFillId && i.AllocationIndex == e.AllocationIndex)));
            if (record.TradingAccountId != request.Preparation.AccountSnapshot!.TradingAccountId ||
                record.PricingCurrency != request.Preparation.AccountSnapshot.Currency ||
                executions.Length != identities.Length || executions.Any(e => !identities.Any(i =>
                    i.TradeExecutionId == e.Id && i.SourceFillPrice == e.Price && i.AllocatedQuantity == e.Quantity &&
                    i.SourceFillExecutedAtUtc == e.ExecutedAtUtc && i.Side == e.Side && i.BrokerSymbol == e.BrokerSymbol &&
                    i.ExternalExecutionId == e.ExternalExecutionId)))
                throw new TradovateCostSourceException("Previously imported executions or Account were edited, or immutable source facts are missing. Costs were not changed.");
            var original = TradePersistenceMapper.ToDomain(record, executions);
            decimal expectedGross = request.MatchedPairs!.Where(p => candidate.SourceRecordIndices.Contains(p.SourceRecordIndex)).Sum(p => p.SourceReportedPnL);
            if (original.GrossPnL != expectedGross)
                throw new TradovateCostSourceException("Existing Trade Gross differs from verified source Gross. Economics were not changed.");
            bool changed = false;
            foreach (var execution in executions)
            {
                var identity = identities.Single(i => i.TradeExecutionId == execution.Id);
                decimal commission = costs.For(identity.ExternalExecutionId, identity.AllocationIndex);
                if (execution.Commission == commission && execution.Fees == 0m) continue; // replay, no overwrite
                if (execution.Commission.HasValue || execution.Fees.HasValue)
                    throw new TradovateCostSourceException("Existing known or manually edited costs differ from the source. No costs were overwritten.");
                if (record.UpdatedAtUtc != identity.ImportedAtUtc)
                    throw new TradovateCostSourceException("This previously imported Trade was edited after import. Unknown costs require separate review and were not changed.");
                execution.Commission = commission;
                // Zero additional fees is established by complete Cash History, not inferred from Performance.
                execution.Fees = 0m;
                changed = true;
            }
            if (!changed) continue;
            record.UpdatedAtUtc = request.ImportedAtUtc;
            var updatedTrade = TradePersistenceMapper.ToDomain(record, executions);
            var browse = await context.TradeBrowse.SingleAsync(b => b.TradeId == id, token);
            context.Entry(browse).CurrentValues.SetValues(TradeBrowsePersistenceMapper.ToRecord(updatedTrade));
            updated++;
        }
        return updated;
    }
}
