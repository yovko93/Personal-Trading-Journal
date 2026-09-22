using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Infrastructure.Imports.Tradovate;

public sealed class TradovateExecutionReconstructor : ITradovateExecutionReconstructor
{
    public TradovateExecutionReconstructionResult Reconstruct(
        TradovateCsvParseResult parseResult,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parseResult);
        cancellationToken.ThrowIfCancellationRequested();

        if (!parseResult.IsCompleteInputValid)
        {
            return BlockedByParser(parseResult);
        }

        if (parseResult.SourceRecordCount == 0)
        {
            return BlockedByEmptySource();
        }

        TradovateMatchedFillRow[] rows = parseResult.Rows.ToArray();
        var diagnostics = new List<TradovateReconstructionDiagnostic>
        {
            new(
                TradovateReconstructionDiagnosticSeverity.Warning,
                TradovateReconstructionDiagnosticCodes.SourceCompletenessUnverified,
                brokerSymbol: null,
                sourceRecordIndices: [],
                externalFillIds: [],
                "The matched-fills export cannot independently prove complete account execution history."),
        };
        var blockedSymbols = new HashSet<string>(StringComparer.Ordinal);
        var ambiguousSymbols = new HashSet<string>(StringComparer.Ordinal);

        DetectDuplicateMatchedRows(rows, ambiguousSymbols, diagnostics, cancellationToken);
        DetectCrossSymbolFillCollisions(rows, blockedSymbols, diagnostics, cancellationToken);

        List<TradovateReconstructedExecution> executions = ReconstructExecutions(
            rows,
            blockedSymbols,
            diagnostics,
            cancellationToken);
        List<TradovateSymbolReconciliation> reconciliations = ReconcileSymbols(
            rows,
            executions,
            blockedSymbols,
            diagnostics,
            cancellationToken);

        var candidates = new List<TradovateTradeCandidate>();
        foreach (IGrouping<string, TradovateMatchedFillRow> symbolRows in rows
                     .GroupBy(row => row.BrokerSymbol, StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            TradovateReconstructedExecution[] symbolExecutions = executions
                .Where(execution => string.Equals(
                    execution.BrokerSymbol,
                    symbolRows.Key,
                    StringComparison.Ordinal))
                .ToArray();

            if (blockedSymbols.Contains(symbolRows.Key))
            {
                AddUnsafeCandidate(
                    candidates,
                    symbolRows,
                    symbolExecutions,
                    TradovateReconstructionStatus.Blocked,
                    diagnostics);
            }
            else if (ambiguousSymbols.Contains(symbolRows.Key))
            {
                AddUnsafeCandidate(
                    candidates,
                    symbolRows,
                    symbolExecutions,
                    TradovateReconstructionStatus.Ambiguous,
                    diagnostics);
            }
            else
            {
                candidates.AddRange(GroupFlatToFlat(
                    symbolRows.Key,
                    symbolExecutions,
                    diagnostics,
                    cancellationToken));
            }
        }

        TradovateReconstructionStatus status = DetermineOverallStatus(
            blockedSymbols,
            ambiguousSymbols,
            candidates);
        TradovateMatchedPairEvidence[] matchedPairs = rows
            .OrderBy(row => row.SourceRecordIndex)
            .Select(row => new TradovateMatchedPairEvidence(
                row.SourceRecordIndex,
                row.SourceLineNumber,
                row.BrokerSymbol,
                row.BuyFillId,
                row.SellFillId,
                row.MatchedQuantity,
                row.SourceReportedPnL))
            .ToArray();

        return new TradovateExecutionReconstructionResult(
            executions
                .OrderBy(execution => execution.BrokerSymbol, StringComparer.Ordinal)
                .ThenBy(execution => execution.SourceLocalTimestamp)
                .ThenBy(execution => execution.Side)
                .ThenBy(execution => execution.ExternalFillId, StringComparer.Ordinal),
            candidates,
            matchedPairs,
            reconciliations,
            diagnostics,
            parseResult.SourceRecordCount,
            status);
    }

    private static TradovateExecutionReconstructionResult BlockedByParser(
        TradovateCsvParseResult parseResult)
    {
        var diagnostic = new TradovateReconstructionDiagnostic(
            TradovateReconstructionDiagnosticSeverity.Error,
            TradovateReconstructionDiagnosticCodes.ParserInputIncomplete,
            brokerSymbol: null,
            sourceRecordIndices: parseResult.Rows.Select(row => row.SourceRecordIndex),
            externalFillIds: [],
            "Execution reconstruction requires a complete parser result with a usable header and no rejected records.");

        return new TradovateExecutionReconstructionResult(
            [],
            [],
            [],
            [],
            [diagnostic],
            parseResult.SourceRecordCount,
            TradovateReconstructionStatus.Blocked);
    }

    private static TradovateExecutionReconstructionResult BlockedByEmptySource()
    {
        var diagnostic = new TradovateReconstructionDiagnostic(
            TradovateReconstructionDiagnosticSeverity.Error,
            TradovateReconstructionDiagnosticCodes.EmptySourceData,
            brokerSymbol: null,
            sourceRecordIndices: [],
            externalFillIds: [],
            "The source contains no matched-fill data records to reconstruct.");

        return new TradovateExecutionReconstructionResult(
            [],
            [],
            [],
            [],
            [diagnostic],
            sourceRecordCount: 0,
            TradovateReconstructionStatus.Blocked);
    }

    private static void DetectDuplicateMatchedRows(
        IReadOnlyList<TradovateMatchedFillRow> rows,
        ISet<string> ambiguousSymbols,
        List<TradovateReconstructionDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        foreach (IGrouping<MatchedRowSignature, TradovateMatchedFillRow> duplicate in rows
                     .GroupBy(row => new MatchedRowSignature(
                         row.BrokerSymbol,
                         row.BuyFillId,
                         row.SellFillId,
                         row.MatchedQuantity,
                         row.BuyPrice,
                         row.SellPrice,
                         row.BoughtLocalTimestamp,
                         row.SoldLocalTimestamp))
                     .Where(group => group.Count() > 1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            TradovateMatchedFillRow first = duplicate.First();
            ambiguousSymbols.Add(first.BrokerSymbol);
            diagnostics.Add(new TradovateReconstructionDiagnostic(
                TradovateReconstructionDiagnosticSeverity.Error,
                TradovateReconstructionDiagnosticCodes.DuplicateMatchedRow,
                first.BrokerSymbol,
                duplicate.Select(row => row.SourceRecordIndex),
                [first.BuyFillId, first.SellFillId],
                "Identical matched rows cannot be distinguished as duplicate export data or separate match events."));
        }
    }

    private static void DetectCrossSymbolFillCollisions(
        IReadOnlyList<TradovateMatchedFillRow> rows,
        ISet<string> blockedSymbols,
        List<TradovateReconstructionDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var observations = rows.SelectMany(row => new[]
        {
            new FillObservation(
                row.BrokerSymbol,
                ExecutionSide.Buy,
                row.BuyFillId,
                row.MatchedQuantity,
                row.BuyPrice,
                row.BoughtLocalTimestamp,
                row.TickSize,
                row.SourceRecordIndex,
                row.SourceLineNumber),
            new FillObservation(
                row.BrokerSymbol,
                ExecutionSide.Sell,
                row.SellFillId,
                row.MatchedQuantity,
                row.SellPrice,
                row.SoldLocalTimestamp,
                row.TickSize,
                row.SourceRecordIndex,
                row.SourceLineNumber),
        });

        foreach (IGrouping<SideFillIdentity, FillObservation> collision in observations
                     .GroupBy(observation => new SideFillIdentity(
                         observation.Side,
                         observation.ExternalFillId))
                     .Where(group => group
                         .Select(observation => observation.BrokerSymbol)
                         .Distinct(StringComparer.Ordinal)
                         .Skip(1)
                         .Any()))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string[] symbols = collision
                .Select(observation => observation.BrokerSymbol)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            foreach (string symbol in symbols)
            {
                blockedSymbols.Add(symbol);
            }

            diagnostics.Add(new TradovateReconstructionDiagnostic(
                TradovateReconstructionDiagnosticSeverity.Error,
                TradovateReconstructionDiagnosticCodes.ConflictingFillSymbol,
                brokerSymbol: null,
                collision.Select(observation => observation.SourceRecordIndex),
                [collision.Key.ExternalFillId],
                "The same side and external fill identifier appears under multiple broker symbols."));
        }
    }

    private static List<TradovateReconstructedExecution> ReconstructExecutions(
        IReadOnlyList<TradovateMatchedFillRow> rows,
        ISet<string> blockedSymbols,
        List<TradovateReconstructionDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        IEnumerable<FillObservation> observations = rows.SelectMany(row => new[]
        {
            new FillObservation(
                row.BrokerSymbol,
                ExecutionSide.Buy,
                row.BuyFillId,
                row.MatchedQuantity,
                row.BuyPrice,
                row.BoughtLocalTimestamp,
                row.TickSize,
                row.SourceRecordIndex,
                row.SourceLineNumber),
            new FillObservation(
                row.BrokerSymbol,
                ExecutionSide.Sell,
                row.SellFillId,
                row.MatchedQuantity,
                row.SellPrice,
                row.SoldLocalTimestamp,
                row.TickSize,
                row.SourceRecordIndex,
                row.SourceLineNumber),
        });
        var result = new List<TradovateReconstructedExecution>();

        foreach (IGrouping<FillIdentity, FillObservation> fillGroup in observations
                     .GroupBy(observation => new FillIdentity(
                         observation.BrokerSymbol,
                         observation.Side,
                         observation.ExternalFillId))
                     .OrderBy(group => group.Key.BrokerSymbol, StringComparer.Ordinal)
                     .ThenBy(group => group.Key.Side)
                     .ThenBy(group => group.Key.ExternalFillId, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            FillObservation[] fillObservations = fillGroup.ToArray();
            bool consistent = true;
            consistent &= ReportConflict(
                fillObservations,
                observation => observation.Price,
                TradovateReconstructionDiagnosticCodes.ConflictingFillPrice,
                "Rows for the same fill report conflicting execution prices.",
                diagnostics);
            consistent &= ReportConflict(
                fillObservations,
                observation => observation.SourceLocalTimestamp,
                TradovateReconstructionDiagnosticCodes.ConflictingFillTimestamp,
                "Rows for the same fill report conflicting source timestamps.",
                diagnostics);
            consistent &= ReportConflict(
                fillObservations,
                observation => observation.TickSize,
                TradovateReconstructionDiagnosticCodes.ConflictingFillTickSize,
                "Rows for the same fill report conflicting tick-size metadata.",
                diagnostics);

            if (!consistent)
            {
                blockedSymbols.Add(fillGroup.Key.BrokerSymbol);
                continue;
            }

            if (!TryCheckedSum(
                    fillObservations.Select(observation => observation.MatchedQuantity),
                    out decimal quantity))
            {
                blockedSymbols.Add(fillGroup.Key.BrokerSymbol);
                diagnostics.Add(new TradovateReconstructionDiagnostic(
                    TradovateReconstructionDiagnosticSeverity.Error,
                    TradovateReconstructionDiagnosticCodes.QuantityOverflow,
                    fillGroup.Key.BrokerSymbol,
                    fillObservations.Select(observation => observation.SourceRecordIndex),
                    [fillGroup.Key.ExternalFillId],
                    "Matched quantity overflowed while reconstructing a broker fill."));
                continue;
            }

            FillObservation first = fillObservations[0];
            result.Add(new TradovateReconstructedExecution(
                first.BrokerSymbol,
                first.Side,
                first.ExternalFillId,
                quantity,
                first.Price,
                first.SourceLocalTimestamp,
                first.TickSize,
                fillObservations.Select(observation => observation.SourceRecordIndex),
                fillObservations
                    .Where(observation => observation.SourceLineNumber.HasValue)
                    .Select(observation => observation.SourceLineNumber!.Value)));
        }

        return result;
    }

    private static bool ReportConflict<T>(
        IReadOnlyList<FillObservation> observations,
        Func<FillObservation, T> selector,
        string code,
        string message,
        List<TradovateReconstructionDiagnostic> diagnostics)
    {
        if (observations.Select(selector).Distinct().Skip(1).Any())
        {
            FillObservation first = observations[0];
            diagnostics.Add(new TradovateReconstructionDiagnostic(
                TradovateReconstructionDiagnosticSeverity.Error,
                code,
                first.BrokerSymbol,
                observations.Select(observation => observation.SourceRecordIndex),
                [first.ExternalFillId],
                message));
            return false;
        }

        return true;
    }

    private static List<TradovateSymbolReconciliation> ReconcileSymbols(
        IReadOnlyList<TradovateMatchedFillRow> rows,
        IReadOnlyList<TradovateReconstructedExecution> executions,
        ISet<string> blockedSymbols,
        List<TradovateReconstructionDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var result = new List<TradovateSymbolReconciliation>();
        foreach (IGrouping<string, TradovateMatchedFillRow> symbolRows in rows
                     .GroupBy(row => row.BrokerSymbol, StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            TradovateMatchedFillRow[] materializedRows = symbolRows.ToArray();
            TradovateReconstructedExecution[] symbolExecutions = executions
                .Where(execution => string.Equals(
                    execution.BrokerSymbol,
                    symbolRows.Key,
                    StringComparison.Ordinal))
                .ToArray();
            bool matchedSuccess = TryCheckedSum(
                materializedRows.Select(row => row.MatchedQuantity),
                out decimal matchedQuantity);
            bool buySuccess = TryCheckedSum(
                symbolExecutions
                    .Where(execution => execution.Side == ExecutionSide.Buy)
                    .Select(execution => execution.Quantity),
                out decimal buyQuantity);
            bool sellSuccess = TryCheckedSum(
                symbolExecutions
                    .Where(execution => execution.Side == ExecutionSide.Sell)
                    .Select(execution => execution.Quantity),
                out decimal sellQuantity);
            bool reconciled = matchedSuccess && buySuccess && sellSuccess &&
                              matchedQuantity == buyQuantity &&
                              matchedQuantity == sellQuantity;

            if (!matchedSuccess || !buySuccess || !sellSuccess)
            {
                blockedSymbols.Add(symbolRows.Key);
                diagnostics.Add(new TradovateReconstructionDiagnostic(
                    TradovateReconstructionDiagnosticSeverity.Error,
                    TradovateReconstructionDiagnosticCodes.QuantityOverflow,
                    symbolRows.Key,
                    materializedRows.Select(row => row.SourceRecordIndex),
                    [],
                    "Quantity overflowed while reconciling the broker-symbol stream."));
            }
            else if (!reconciled)
            {
                blockedSymbols.Add(symbolRows.Key);
                diagnostics.Add(new TradovateReconstructionDiagnostic(
                    TradovateReconstructionDiagnosticSeverity.Error,
                    TradovateReconstructionDiagnosticCodes.QuantityReconciliationFailed,
                    symbolRows.Key,
                    materializedRows.Select(row => row.SourceRecordIndex),
                    [],
                    "Reconstructed buy and sell quantities do not conserve every matched source quantity."));
            }

            result.Add(new TradovateSymbolReconciliation(
                symbolRows.Key,
                materializedRows.Length,
                matchedQuantity,
                buyQuantity,
                sellQuantity,
                reconciled));
        }

        return result;
    }

    private static IReadOnlyList<TradovateTradeCandidate> GroupFlatToFlat(
        string brokerSymbol,
        IReadOnlyList<TradovateReconstructedExecution> executions,
        List<TradovateReconstructionDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var candidates = new List<TradovateTradeCandidate>();
        var current = new List<TradovateReconstructedExecution>();
        var assigned = new HashSet<TradovateReconstructedExecution>();
        decimal signedPosition = 0m;
        TradeDirection? direction = null;
        List<IGrouping<DateTime, TradovateReconstructedExecution>> timestampGroups = executions
            .GroupBy(execution => execution.SourceLocalTimestamp)
            .OrderBy(group => group.Key)
            .ToList();

        for (int groupIndex = 0; groupIndex < timestampGroups.Count; groupIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TradovateReconstructedExecution[] timestampGroup = timestampGroups[groupIndex].ToArray();
            if (ContainsBothSides(timestampGroup) &&
                CanTimestampOrderAffectLifecycle(signedPosition, timestampGroup))
            {
                int[] affectedRecords = timestampGroup
                    .SelectMany(execution => execution.SourceRecordIndices)
                    .Distinct()
                    .Order()
                    .ToArray();
                diagnostics.Add(new TradovateReconstructionDiagnostic(
                    TradovateReconstructionDiagnosticSeverity.Error,
                    TradovateReconstructionDiagnosticCodes.TimestampOrderAmbiguous,
                    brokerSymbol,
                    affectedRecords,
                    timestampGroup.Select(execution => execution.ExternalFillId),
                    "Opposite-side fills share a timestamp and their unknown ordering can change position boundaries."));

                current.AddRange(OrderForPresentation(timestampGroup));
                current.AddRange(timestampGroups
                    .Skip(groupIndex + 1)
                    .SelectMany(group => OrderForPresentation(group)));
                decimal ambiguousEndPosition = CalculateSignedPosition(current);
                candidates.Add(CreateCandidate(
                    brokerSymbol,
                    direction,
                    current,
                    closingLocalTimestamp: null,
                    ambiguousEndPosition,
                    TradovateReconstructionStatus.Ambiguous,
                    [TradovateReconstructionDiagnosticCodes.TimestampOrderAmbiguous]));
                return candidates;
            }

            foreach (TradovateReconstructedExecution execution in OrderSafeTimestampGroup(
                         timestampGroup,
                         signedPosition))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (current.Count == 0)
                {
                    direction = execution.Side == ExecutionSide.Buy
                        ? TradeDirection.Long
                        : TradeDirection.Short;
                }

                decimal nextPosition = checked(signedPosition + SignedQuantity(execution));
                current.Add(execution);
                if (CrossesThroughZero(signedPosition, nextPosition))
                {
                    diagnostics.Add(new TradovateReconstructionDiagnostic(
                        TradovateReconstructionDiagnosticSeverity.Error,
                        TradovateReconstructionDiagnosticCodes.PositionReversal,
                        brokerSymbol,
                        current.SelectMany(item => item.SourceRecordIndices),
                        current.Select(item => item.ExternalFillId),
                        "An execution crosses the position through zero and cannot be split without fabricating broker fills."));

                    HashSet<TradovateReconstructedExecution> included =
                        assigned.Concat(current).ToHashSet();
                    current.AddRange(executions
                        .Where(item => !included.Contains(item))
                        .OrderBy(item => item.SourceLocalTimestamp)
                        .ThenBy(item => item.Side)
                        .ThenBy(item => item.ExternalFillId, StringComparer.Ordinal));
                    candidates.Add(CreateCandidate(
                        brokerSymbol,
                        direction,
                        current,
                        closingLocalTimestamp: null,
                        nextPosition,
                        TradovateReconstructionStatus.Blocked,
                        [TradovateReconstructionDiagnosticCodes.PositionReversal]));
                    return candidates;
                }

                signedPosition = nextPosition;
                if (signedPosition == 0m)
                {
                    candidates.Add(CreateCandidate(
                        brokerSymbol,
                        direction,
                        current,
                        execution.SourceLocalTimestamp,
                        signedPosition,
                        TradovateReconstructionStatus.Reconstructed,
                        []));
                    assigned.UnionWith(current);
                    current = [];
                    direction = null;
                }
            }
        }

        if (current.Count > 0)
        {
            diagnostics.Add(new TradovateReconstructionDiagnostic(
                TradovateReconstructionDiagnosticSeverity.Error,
                TradovateReconstructionDiagnosticCodes.IncompleteLifecycle,
                brokerSymbol,
                current.SelectMany(execution => execution.SourceRecordIndices),
                current.Select(execution => execution.ExternalFillId),
                "The source stream ends with a nonzero position and cannot form a complete flat-to-flat Trade."));
            candidates.Add(CreateCandidate(
                brokerSymbol,
                direction,
                current,
                closingLocalTimestamp: null,
                signedPosition,
                TradovateReconstructionStatus.Incomplete,
                [TradovateReconstructionDiagnosticCodes.IncompleteLifecycle]));
        }

        return candidates;
    }

    private static void AddUnsafeCandidate(
        ICollection<TradovateTradeCandidate> candidates,
        IEnumerable<TradovateMatchedFillRow> symbolRows,
        IReadOnlyList<TradovateReconstructedExecution> symbolExecutions,
        TradovateReconstructionStatus status,
        IReadOnlyList<TradovateReconstructionDiagnostic> diagnostics)
    {
        if (symbolExecutions.Count == 0)
        {
            return;
        }

        TradovateMatchedFillRow[] rows = symbolRows.ToArray();
        List<TradovateReconstructedExecution> ordered = OrderForPresentation(symbolExecutions).ToList();
        DateTime firstTimestamp = ordered[0].SourceLocalTimestamp;
        ExecutionSide[] firstSides = ordered
            .Where(execution => execution.SourceLocalTimestamp == firstTimestamp)
            .Select(execution => execution.Side)
            .Distinct()
            .ToArray();
        TradeDirection? direction = firstSides.Length == 1
            ? firstSides[0] == ExecutionSide.Buy
                ? TradeDirection.Long
                : TradeDirection.Short
            : null;
        HashSet<int> sourceRecords = rows.Select(row => row.SourceRecordIndex).ToHashSet();
        string[] codes = diagnostics
            .Where(diagnostic =>
                string.Equals(diagnostic.BrokerSymbol, rows[0].BrokerSymbol, StringComparison.Ordinal) ||
                diagnostic.SourceRecordIndices.Any(sourceRecords.Contains))
            .Select(diagnostic => diagnostic.Code)
            .ToArray();
        if (!TryCalculateSignedPosition(ordered, out decimal signedPosition))
        {
            return;
        }

        candidates.Add(CreateCandidate(
            rows[0].BrokerSymbol,
            direction,
            ordered,
            closingLocalTimestamp: null,
            signedPosition,
            status,
            codes,
            sourceRecords));
    }

    private static TradovateTradeCandidate CreateCandidate(
        string brokerSymbol,
        TradeDirection? direction,
        IReadOnlyList<TradovateReconstructedExecution> executions,
        DateTime? closingLocalTimestamp,
        decimal signedPositionAtEnd,
        TradovateReconstructionStatus status,
        IEnumerable<string> diagnosticCodes,
        IEnumerable<int>? sourceRecordIndices = null)
    {
        decimal? openingQuantity = null;
        decimal? closingQuantity = null;
        if (direction.HasValue)
        {
            ExecutionSide openingSide = direction == TradeDirection.Long
                ? ExecutionSide.Buy
                : ExecutionSide.Sell;
            openingQuantity = CheckedSum(executions
                .Where(execution => execution.Side == openingSide)
                .Select(execution => execution.Quantity));
            closingQuantity = CheckedSum(executions
                .Where(execution => execution.Side != openingSide)
                .Select(execution => execution.Quantity));
        }

        return new TradovateTradeCandidate(
            brokerSymbol,
            direction,
            executions,
            executions.Min(execution => execution.SourceLocalTimestamp),
            closingLocalTimestamp,
            openingQuantity,
            closingQuantity,
            signedPositionAtEnd,
            status,
            sourceRecordIndices ?? executions.SelectMany(execution => execution.SourceRecordIndices),
            diagnosticCodes);
    }

    private static IEnumerable<TradovateReconstructedExecution> OrderSafeTimestampGroup(
        IEnumerable<TradovateReconstructedExecution> group,
        decimal signedPosition)
    {
        ExecutionSide preferredSide = signedPosition < 0m
            ? ExecutionSide.Sell
            : ExecutionSide.Buy;

        return group
            .OrderBy(execution => execution.Side == preferredSide ? 0 : 1)
            .ThenBy(execution => execution.ExternalFillId, StringComparer.Ordinal);
    }

    private static IEnumerable<TradovateReconstructedExecution> OrderForPresentation(
        IEnumerable<TradovateReconstructedExecution> executions) =>
        executions
            .OrderBy(execution => execution.SourceLocalTimestamp)
            .ThenBy(execution => execution.Side)
            .ThenBy(execution => execution.ExternalFillId, StringComparer.Ordinal);

    private static bool CanTimestampOrderAffectLifecycle(
        decimal signedPosition,
        IReadOnlyList<TradovateReconstructedExecution> timestampGroup)
    {
        decimal buyQuantity = CheckedSum(timestampGroup
            .Where(execution => execution.Side == ExecutionSide.Buy)
            .Select(execution => execution.Quantity));
        decimal sellQuantity = CheckedSum(timestampGroup
            .Where(execution => execution.Side == ExecutionSide.Sell)
            .Select(execution => execution.Quantity));

        if (signedPosition == 0m)
        {
            return true;
        }

        return signedPosition > 0m
            ? sellQuantity >= signedPosition && buyQuantity > 0m
            : buyQuantity >= -signedPosition && sellQuantity > 0m;
    }

    private static bool ContainsBothSides(
        IEnumerable<TradovateReconstructedExecution> executions) =>
        executions.Select(execution => execution.Side).Distinct().Skip(1).Any();

    private static bool CrossesThroughZero(decimal current, decimal next) =>
        current > 0m && next < 0m || current < 0m && next > 0m;

    private static decimal SignedQuantity(TradovateReconstructedExecution execution) =>
        execution.Side == ExecutionSide.Buy
            ? execution.Quantity
            : -execution.Quantity;

    private static decimal CalculateSignedPosition(
        IEnumerable<TradovateReconstructedExecution> executions)
    {
        decimal total = 0m;
        foreach (TradovateReconstructedExecution execution in executions)
        {
            total = checked(total + SignedQuantity(execution));
        }

        return total;
    }

    private static bool TryCalculateSignedPosition(
        IEnumerable<TradovateReconstructedExecution> executions,
        out decimal signedPosition)
    {
        try
        {
            signedPosition = CalculateSignedPosition(executions);
            return true;
        }
        catch (OverflowException)
        {
            signedPosition = 0m;
            return false;
        }
    }

    private static bool TryCheckedSum(IEnumerable<decimal> values, out decimal total)
    {
        total = 0m;
        try
        {
            foreach (decimal value in values)
            {
                total = checked(total + value);
            }

            return true;
        }
        catch (OverflowException)
        {
            total = 0m;
            return false;
        }
    }

    private static decimal CheckedSum(IEnumerable<decimal> values)
    {
        decimal total = 0m;
        foreach (decimal value in values)
        {
            total = checked(total + value);
        }

        return total;
    }

    private static TradovateReconstructionStatus DetermineOverallStatus(
        IReadOnlyCollection<string> blockedSymbols,
        IReadOnlyCollection<string> ambiguousSymbols,
        IReadOnlyCollection<TradovateTradeCandidate> candidates)
    {
        if (blockedSymbols.Count > 0 ||
            candidates.Any(candidate => candidate.Status == TradovateReconstructionStatus.Blocked))
        {
            return TradovateReconstructionStatus.Blocked;
        }

        if (ambiguousSymbols.Count > 0 ||
            candidates.Any(candidate => candidate.Status == TradovateReconstructionStatus.Ambiguous))
        {
            return TradovateReconstructionStatus.Ambiguous;
        }

        return candidates.Any(candidate =>
            candidate.Status == TradovateReconstructionStatus.Incomplete)
            ? TradovateReconstructionStatus.Incomplete
            : TradovateReconstructionStatus.Reconstructed;
    }

    private sealed record FillIdentity(
        string BrokerSymbol,
        ExecutionSide Side,
        string ExternalFillId);

    private sealed record SideFillIdentity(
        ExecutionSide Side,
        string ExternalFillId);

    private sealed record FillObservation(
        string BrokerSymbol,
        ExecutionSide Side,
        string ExternalFillId,
        decimal MatchedQuantity,
        decimal Price,
        DateTime SourceLocalTimestamp,
        decimal TickSize,
        int SourceRecordIndex,
        int? SourceLineNumber);

    private sealed record MatchedRowSignature(
        string BrokerSymbol,
        string BuyFillId,
        string SellFillId,
        decimal MatchedQuantity,
        decimal BuyPrice,
        decimal SellPrice,
        DateTime BoughtLocalTimestamp,
        DateTime SoldLocalTimestamp);
}
