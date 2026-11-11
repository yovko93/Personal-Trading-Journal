using PersonalTradingJournal.Application.Common.Time;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Imports.Tradovate;

/// <summary>Strict whole-source reconciliation. Never estimates a missing charge or partial fill.</summary>
public static class TradovateCostReconciler
{
    public static TradovateVerifiedCosts Reconcile(TradovateImportPreparationResult preparation,
        IReadOnlyList<TradovateMatchedPairEvidence> pairs, TradovateCostSources sources)
    {
        void Require(bool condition, string message)
        {
            if (!condition) throw new TradovateCostSourceException(message);
        }

        Require(preparation.IsReadyForPreview, "Prepare an eligible Performance import first.");
        var account = preparation.AccountSnapshot!;
        var fills = sources.Fills;
        var cash = sources.Cash;
        Require(fills.Count > 0 && cash.Count > 0, "Both Fills and Cash History must contain complete activity.");
        Require(!string.IsNullOrWhiteSpace(account.ExternalAccountId) &&
            fills.All(f => f.Account == account.ExternalAccountId) && cash.All(c => c.Account == account.ExternalAccountId),
            "Set the selected Account's external ID to the exact export Account. Mixed or mismatched Accounts are blocked.");
        Require(account.Currency == "USD" && cash.All(c => c.Currency == account.Currency),
            "Verified costs currently require matching USD Account, Instruments and Cash History. No FX is inferred.");
        Require(fills.Select(f => f.Id).Distinct(StringComparer.Ordinal).Count() == fills.Count &&
            cash.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() == cash.Count,
            "Duplicate fill or cash transaction IDs are ambiguous. Export each source once.");
        Require(cash.All(c => c.Type is "Commission" or "Trade Paired"),
            "Cash History contains unsupported cash movements. Select an isolated trading period without transfers or other charges.");
        Require(fills.All(f => f.Quantity > 0 && f.Quantity == decimal.Truncate(f.Quantity) &&
            f.Commission >= 0 && f.Commission == decimal.Round(f.Commission, 2)),
            "Fill quantity or commission is invalid; USD charges must be nonnegative whole cents.");
        var byId = fills.ToDictionary(f => f.Id, StringComparer.Ordinal);
        var executions = preparation.PreparedCandidates.SelectMany((c, i) => c.OrderedExecutions.Select(e => (Execution: e, Candidate: i))).ToArray();
        Require(executions.Select(e => e.Execution.ExternalFillId).ToHashSet(StringComparer.Ordinal).SetEquals(byId.Keys),
            "Performance and Fills IDs do not match exactly. Partial or extra activity cannot verify Net.");
        Require(pairs.Count > 0 && pairs.Select(p => p.SourceRecordIndex).Distinct().Count() == pairs.Count,
            "Performance matched-row evidence is missing or duplicated.");
        var candidateRows = preparation.PreparedCandidates.SelectMany(c => c.SourceRecordIndices).ToArray();
        Require(candidateRows.Length == pairs.Count && candidateRows.ToHashSet().SetEquals(pairs.Select(p => p.SourceRecordIndex)),
            "Trade candidates do not partition Performance evidence exactly once.");
        var pairsByFill = pairs.SelectMany(p => new[] { (Id: p.BuyFillId, Pair: p), (Id: p.SellFillId, Pair: p) })
            .ToLookup(p => p.Id, p => p.Pair, StringComparer.Ordinal);
        var pairsByRow = pairs.ToDictionary(p => p.SourceRecordIndex);
        var result = new List<TradovateCostAllocation>();
        foreach (var group in executions.GroupBy(e => e.Execution.ExternalFillId))
        {
            var fill = byId[group.Key];
            Require(group.All(a => a.Execution.BrokerSymbol == fill.Symbol && a.Execution.Side == fill.Side &&
                a.Execution.Price == fill.Price && a.Execution.SourceLocalTimestamp == fill.LocalTimestamp) &&
                group.Sum(a => a.Execution.Quantity) == fill.Quantity,
                "Fill price, side, timestamp, symbol or complete matched quantity differs from Performance.");
            var local = TradingTimePolicy.ConvertUtcToLocal(fill.UtcTimestamp, TradingTimePolicy.TradovateSourceTimeZoneId).DateTime;
            Require(local.AddTicks(-(local.Ticks % TimeSpan.TicksPerSecond)) == fill.LocalTimestamp,
                "Fills UTC and display timestamps disagree with the configured Sofia source zone.");
            var matched = pairsByFill[fill.Id].ToArray();
            Require(matched.Sum(p => p.MatchedQuantity) == fill.Quantity && matched.All(p =>
                p.BrokerSymbol == fill.Symbol && (fill.Side == ExecutionSide.Buy ? p.BuyFillId : p.SellFillId) == fill.Id),
                "Performance contains partial or inconsistent matched-fill evidence.");
            // Largest remainder in cents, with immutable allocation index as the tie-breaker.
            // Each source fill is charged once even across a reversal's two Trades.
            var portions = group.OrderBy(a => a.Execution.AllocationIndex).ToArray();
            Require(portions.Select(a => a.Execution.AllocationIndex).Distinct().Count() == portions.Length,
                "Repeated allocation identity is ambiguous.");
            decimal cents = fill.Commission * 100m;
            var shares = portions.Select(a => cents * a.Execution.Quantity / fill.Quantity).ToArray();
            var allocated = shares.Select(decimal.Floor).ToArray();
            int remaining = decimal.ToInt32(cents - allocated.Sum());
            foreach (int index in Enumerable.Range(0, portions.Length)
                .OrderByDescending(i => shares[i] - allocated[i]).ThenBy(i => portions[i].Execution.AllocationIndex).Take(remaining))
                allocated[index]++;
            for (int i = 0; i < portions.Length; i++)
                result.Add(new(portions[i].Candidate, fill.Id, portions[i].Execution.AllocationIndex, allocated[i] / 100m));
        }

        // A zero aggregate difference alone is insufficient: require each commission event and
        // each contract/trading-date gross total to agree, without adding Trade Paired to P&L.
        // Cash posting can lag execution (observed by one second); it has no fill ID.
        // Compare exact daily charge multisets, never invent a cash-to-fill timestamp join.
        var expectedCharges = fills.GroupBy(f => (f.Symbol, f.TradingDate, Delta: -f.Commission))
            .ToDictionary(g => g.Key, g => g.Count());
        var actualCharges = cash.Where(c => c.Type == "Commission")
            .GroupBy(c => (c.Symbol, c.TradingDate, c.Delta)).ToDictionary(g => g.Key, g => g.Count());
        Require(expectedCharges.Count == actualCharges.Count && expectedCharges.All(k => actualCharges.GetValueOrDefault(k.Key) == k.Value),
            "Cash commission amounts and counts do not match Fills by contract and trading date.");
        Require(pairs.All(p => byId.ContainsKey(p.BuyFillId) && byId.ContainsKey(p.SellFillId)), "Missing matched fill.");
        var grossByDay = pairs.GroupBy(p => (p.BrokerSymbol, Date: Later(byId[p.BuyFillId], byId[p.SellFillId]).TradingDate))
            .ToDictionary(g => g.Key, g => g.Sum(p => p.SourceReportedPnL));
        var cashByDay = cash.Where(c => c.Type == "Trade Paired").GroupBy(c => (c.Symbol, Date: c.TradingDate))
            .ToDictionary(g => g.Key, g => g.Sum(c => c.Delta));
        Require(grossByDay.Count == cashByDay.Count && grossByDay.All(k => cashByDay.TryGetValue(k.Key, out var value) && value == k.Value),
            "Cash Trade Paired totals differ from Performance by contract or trading date.");
        Require(cash.Zip(cash.Skip(1)).All(p => p.First.LocalTimestamp <= p.Second.LocalTimestamp &&
            p.First.Balance + p.Second.Delta == p.Second.Balance),
            "Cash History must be chronological with continuous running balances; missing or reordered transactions are blocked.");

        for (int i = 0; i < preparation.PreparedCandidates.Count; i++)
        {
            var candidate = preparation.PreparedCandidates[i];
            var instrument = preparation.InstrumentResolution.CanonicalInstrumentResolutions.Single(r => r.CanonicalSymbol == candidate.CanonicalSymbol);
            decimal tickValue = instrument.ExistingInstrument?.TickValue ?? instrument.CreationProposal!.TickValue;
            decimal tickSize = instrument.ExistingInstrument?.TickSize ?? instrument.CreationProposal!.TickSize;
            string currency = instrument.ExistingInstrument?.Currency ?? instrument.CreationProposal!.Currency;
            decimal gross = candidate.OrderedExecutions.Sum(e => (e.Side == ExecutionSide.Sell ? 1m : -1m) * e.Quantity * e.Price) * tickValue / tickSize;
            Require(currency == account.Currency && gross == candidate.SourceRecordIndices.Sum(r => pairsByRow[r].SourceReportedPnL),
                "Instrument-derived Gross differs from source Gross or currency. No economics will be overwritten.");
        }
        decimal sourceGross = pairs.Sum(p => p.SourceReportedPnL);
        decimal costs = fills.Sum(f => f.Commission);
        Require(cash.Sum(c => c.Delta) == sourceGross - costs && result.Sum(a => a.Commission) == costs,
            "Net cash or allocation reconciliation failed.");
        return new(sourceGross, costs, fills.Min(f => f.TradingDate), fills.Max(f => f.TradingDate), result.AsReadOnly());
    }

    private static TradovateCostFill Later(TradovateCostFill a, TradovateCostFill b) => a.UtcTimestamp >= b.UtcTimestamp ? a : b;
}
