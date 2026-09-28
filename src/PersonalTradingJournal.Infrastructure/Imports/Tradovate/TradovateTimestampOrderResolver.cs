using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Infrastructure.Imports.Tradovate;

/// <summary>
/// Orders matched-fill lifecycle events, not an invented sub-second broker sequence.
/// Definite closures of earlier matched lots precede unrelated openings of later lots.
/// Undirected same-second matches must have only one feasible allocation outcome.
/// </summary>
internal static class TradovateTimestampOrderResolver
{
    private const int MaximumSearchStates = 10_000;
    private const int MaximumSearchDepth = 256;
    private const string ConflictingEvidence =
        "No order reconciles the matched closing/opening quantities with the incoming position; complete execution history or corrected matched-fill evidence is required.";

    internal sealed record Result(TradovateReconstructedExecution[]? Order, string? Reason);

    public static Result Resolve(
        IReadOnlyList<TradovateReconstructedExecution> group,
        decimal initialPosition,
        IReadOnlyList<TradovateMatchedFillRow> rows,
        CancellationToken cancellationToken)
    {
        TradovateReconstructedExecution[] stable = group
            .OrderBy(fill => fill.Side == (initialPosition < 0 ? ExecutionSide.Sell : ExecutionSide.Buy) ? 0 : 1)
            .ThenBy(fill => fill.ExternalFillId, StringComparer.Ordinal).ToArray();
        if (!CanChangeBoundary(initialPosition, stable))
        {
            return new(stable, null);
        }

        var evidence = stable.ToDictionary(fill => fill, fill => rows
            .Where(row => fill.SourceRecordIndices.Contains(row.SourceRecordIndex)).ToArray());
        int Rank(TradovateReconstructedExecution fill)
        {
            var pairs = evidence[fill];
            if (pairs.All(row => CounterpartTime(fill, row) < fill.SourceLocalTimestamp))
            {
                return 0;
            }
            if (pairs.All(row => CounterpartTime(fill, row) > fill.SourceLocalTimestamp))
            {
                return 2;
            }
            return 1;
        }

        bool HasSameSecondMatch(TradovateReconstructedExecution fill) =>
            evidence[fill].Any(row => CounterpartTime(fill, row) == fill.SourceLocalTimestamp);

        if (!stable.Any(HasSameSecondMatch))
        {
            // All lot roles are known. Pure closures commute, a crossing closes the
            // remainder, and pure openings commute; no permutation search is necessary.
            TradovateReconstructedExecution[] ordered = stable.OrderBy(Rank).ToArray();
            decimal position = initialPosition;
            foreach (TradovateReconstructedExecution fill in ordered)
            {
                cancellationToken.ThrowIfCancellationRequested();
                decimal expectedClosing = evidence[fill]
                    .Where(row => CounterpartTime(fill, row) < fill.SourceLocalTimestamp)
                    .Sum(row => row.MatchedQuantity);
                if (expectedClosing != ClosingQuantity(position, fill))
                {
                    return new(null, ConflictingEvidence);
                }
                position = NextPosition(position, fill);
            }
            return new(ordered, null);
        }

        var path = new List<TradovateReconstructedExecution>();
        var assignments = new List<Allocation>();
        TradovateReconstructedExecution[]? acceptedOrder = null;
        Allocation[]? acceptedAllocations = null;
        bool ambiguous = false;
        bool exhausted = false;
        int visited = 0;
        int completed = 0;

        void Search(decimal position, int segment)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ambiguous || exhausted)
            {
                return;
            }
            if (++visited > MaximumSearchStates || path.Count > MaximumSearchDepth)
            {
                exhausted = true;
                return;
            }
            var remaining = stable.Where(fill => !path.Contains(fill)).ToArray();
            if (remaining.Length == 0)
            {
                completed++;
                Allocation[] outcome = assignments.OrderBy(item => item.Fill.Side)
                    .ThenBy(item => item.Fill.ExternalFillId, StringComparer.Ordinal)
                    .ThenBy(item => item.Segment).ToArray();
                if (acceptedAllocations is null)
                {
                    acceptedAllocations = outcome;
                    acceptedOrder = path.ToArray();
                }
                else if (!acceptedAllocations.SequenceEqual(outcome))
                {
                    ambiguous = true;
                }
                return;
            }

            // If every remaining fill stays in one lifecycle, permutations cannot change
            // its membership, quantities, weighted prices, or P&L. Avoid factorial search.
            bool invariantTail = !CanChangeBoundary(position, remaining);
            foreach (TradovateReconstructedExecution fill in remaining.OrderBy(Rank))
            {
                // Known closing / crossing / opening phases come from matched lot evidence.
                // A fill-ID may stabilize ordering only inside an economically equivalent phase.
                if (!HasSameSecondMatch(fill) && remaining.Any(other =>
                        !HasSameSecondMatch(other) && Rank(other) < Rank(fill)))
                {
                    continue;
                }

                TradovateMatchedFillRow[] closing = evidence[fill]
                    .Where(row => IsClosingRow(fill, row, path)).ToArray();
                decimal expectedClosing = closing.Sum(row => row.MatchedQuantity);
                decimal actualClosing = ClosingQuantity(position, fill);
                if (expectedClosing != actualClosing)
                {
                    continue;
                }

                int count = assignments.Count;
                decimal next = NextPosition(position, fill);
                bool crosses = actualClosing > 0 && actualClosing < fill.Quantity;
                if (crosses)
                {
                    assignments.Add(new(fill, segment, actualClosing, 0m,
                        string.Join(',', closing.Select(row => row.SourceRecordIndex).Order())));
                    assignments.Add(new(fill, segment + 1, 0m, fill.Quantity - actualClosing,
                        string.Join(',', evidence[fill].Except(closing).Select(row => row.SourceRecordIndex).Order())));
                }
                else
                {
                    // Closing vs opening must also match: an instantaneous Long and Short
                    // with the same gross total are not the same Trade direction.
                    assignments.Add(new(fill, segment, actualClosing, fill.Quantity - actualClosing, "whole"));
                }
                path.Add(fill);
                int completedBefore = completed;
                Search(next, segment + (crosses || next == 0 ? 1 : 0));
                path.RemoveAt(path.Count - 1);
                assignments.RemoveRange(count, assignments.Count - count);
                if (ambiguous || exhausted || invariantTail && completed > completedBefore)
                {
                    return;
                }
            }
        }

        Search(initialPosition, 0);
        return exhausted
            ? new(null, "Matched evidence could not establish a unique lifecycle within the bounded order search; broker sub-second timestamps or sequence are required.")
            : ambiguous
                ? new(null, "Alternative orders consistent with the matched rows change Trade boundaries, direction, or fill allocations; broker sub-second timestamps or sequence are required.")
                : acceptedOrder is null
                    ? new(null, ConflictingEvidence)
                    : new(acceptedOrder, null);
    }

    public static bool IsClosingRow(
        TradovateReconstructedExecution fill,
        TradovateMatchedFillRow row,
        IEnumerable<TradovateReconstructedExecution> earlierInGroup) =>
        CounterpartTime(fill, row) < fill.SourceLocalTimestamp ||
        (CounterpartTime(fill, row) == fill.SourceLocalTimestamp && earlierInGroup.Any(other =>
            other.Side != fill.Side && other.ExternalFillId ==
                (fill.Side == ExecutionSide.Buy ? row.SellFillId : row.BuyFillId)));

    private static DateTime CounterpartTime(TradovateReconstructedExecution fill, TradovateMatchedFillRow row) =>
        fill.Side == ExecutionSide.Buy ? row.SoldLocalTimestamp : row.BoughtLocalTimestamp;

    private static decimal ClosingQuantity(decimal position, TradovateReconstructedExecution fill) =>
        position != 0m && (position > 0m) != (fill.Side == ExecutionSide.Buy)
            ? Math.Min(Math.Abs(position), fill.Quantity) : 0m;

    private static decimal NextPosition(decimal position, TradovateReconstructedExecution fill) =>
        checked(position + (fill.Side == ExecutionSide.Buy ? fill.Quantity : -fill.Quantity));

    private static bool CanChangeBoundary(decimal position, IReadOnlyList<TradovateReconstructedExecution> group)
    {
        decimal buys = group.Where(fill => fill.Side == ExecutionSide.Buy).Sum(fill => fill.Quantity);
        decimal sells = group.Where(fill => fill.Side == ExecutionSide.Sell).Sum(fill => fill.Quantity);
        if (position == 0)
        {
            return buys > 0 && sells > 0;
        }
        return position > 0
            ? sells >= position && (buys > 0 || sells > position && group.Count > 1)
            : buys >= -position && (sells > 0 || buys > -position && group.Count > 1);
    }

    private sealed record Allocation(
        TradovateReconstructedExecution Fill,
        int Segment,
        decimal ClosingQuantity,
        decimal OpeningQuantity,
        string Evidence);
}
