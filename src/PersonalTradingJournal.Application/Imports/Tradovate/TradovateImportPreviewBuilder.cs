using PersonalTradingJournal.Application.Common.Time;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Imports.Tradovate;

/// <summary>
/// Builds a deterministic, side-effect-free representation of a prepared Tradovate import.
/// </summary>
public sealed class TradovateImportPreviewBuilder
{
    public TradovateImportPreview Build(
        string fileName,
        TradovateCsvParseResult parseResult,
        TradovateExecutionReconstructionResult reconstruction,
        TradovateImportPreparationResult preparation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(parseResult);
        ArgumentNullException.ThrowIfNull(reconstruction);
        ArgumentNullException.ThrowIfNull(preparation);

        IReadOnlyList<TradovateImportPreviewDiagnostic> diagnostics =
            BuildDiagnostics(parseResult, reconstruction, preparation);
        TradovateImportPreviewInstrumentItem[] instruments = preparation.InstrumentResolution
            .CanonicalInstrumentResolutions
            .OrderBy(item => item.CanonicalSymbol, StringComparer.Ordinal)
            .Select(ToInstrumentItem)
            .ToArray();
        TradovateImportPreviewTradeItem[] trades = preparation.PreparedCandidates
            .OrderBy(candidate => candidate.OpenedAtUtc)
            .ThenBy(candidate => candidate.SourceRecordIndices.DefaultIfEmpty(int.MaxValue).Min())
            .ThenBy(candidate => candidate.BrokerSymbol, StringComparer.Ordinal)
            .Select((candidate, index) => ToTradeItem(
                index + 1,
                candidate,
                parseResult,
                preparation.AccountSnapshot!))
            .ToArray();

        DateTimeOffset? periodStart = trades.Length == 0
            ? null
            : trades.Min(item => item.OpenedAtNewYork);
        DateTimeOffset? periodEnd = trades.Length == 0
            ? null
            : preparation.PreparedCandidates.Max(candidate =>
                candidate.ClosedAtNewYork ??
                candidate.OrderedExecutions.Select(ToNewYorkTimestamp).Max());
        TradovateImportAccountSnapshot? account = preparation.AccountSnapshot;
        var summary = new TradovateImportPreviewSummary(
            fileName,
            account?.Name ?? "No account selected",
            account?.Currency ?? "—",
            account?.IsActive ?? false,
            TradingTimePolicy.TradovateSourceTimeZoneId,
            "UTC",
            TradingTimePolicy.TradingTimeZoneId,
            parseResult.SourceRecordCount,
            parseResult.ValidRecordCount,
            parseResult.RejectedRecordCount,
            reconstruction.UniqueBuyFillCount,
            reconstruction.UniqueSellFillCount,
            reconstruction.Candidates.Count,
            instruments.Length,
            instruments.Count(item =>
                item.Status == TradovateInstrumentResolutionStatus.ExistingInstrument),
            instruments.Count(item =>
                item.Status == TradovateInstrumentResolutionStatus.ProposedCreation),
            diagnostics.Count(item =>
                item.Severity == TradovateReconstructionDiagnosticSeverity.Warning),
            diagnostics.Count(item =>
                item.Severity == TradovateReconstructionDiagnosticSeverity.Error),
            periodStart,
            periodEnd);

        bool structurallyReady = preparation.IsReadyForPreview && trades.Length > 0;
        return new TradovateImportPreview(
            summary,
            instruments,
            trades,
            diagnostics,
            structurallyReady,
            IsReadyForConfirmation: false);
    }

    private static TradovateImportPreviewInstrumentItem ToInstrumentItem(
        TradovateCanonicalInstrumentResolution resolution)
    {
        TradovateInstrumentCreationProposal? proposal = resolution.CreationProposal;
        return new TradovateImportPreviewInstrumentItem(
            resolution.CanonicalSymbol,
            resolution.SourceBrokerSymbols,
            resolution.Status,
            resolution.ExistingInstrumentId,
            resolution.IsExistingInstrumentActive,
            proposal?.DisplayName,
            proposal?.AssetClass.ToString(),
            proposal?.Exchange,
            resolution.ResolvedCurrency ?? proposal?.Currency,
            resolution.SourceTickSize ?? proposal?.TickSize,
            proposal?.TickValue,
            proposal?.MetadataSource);
    }

    private static TradovateImportPreviewTradeItem ToTradeItem(
        int candidateIndex,
        TradovatePreparedTradeCandidate candidate,
        TradovateCsvParseResult parseResult,
        TradovateImportAccountSnapshot account)
    {
        ExecutionSide openingSide = candidate.ProvisionalDirection == TradeDirection.Long
            ? ExecutionSide.Buy
            : ExecutionSide.Sell;
        TradovatePreparedExecution[] entryExecutions = candidate.OrderedExecutions
            .Where(execution => execution.Side == openingSide)
            .ToArray();
        TradovatePreparedExecution[] exitExecutions = candidate.OrderedExecutions
            .Where(execution => execution.Side != openingSide)
            .ToArray();
        decimal openingQuantity = entryExecutions.Sum(execution => execution.Quantity);

        return new TradovateImportPreviewTradeItem(
            candidateIndex,
            candidate.BrokerSymbol,
            candidate.CanonicalSymbol,
            account.Name,
            candidate.ProvisionalDirection,
            candidate.OpenedAtNewYork,
            candidate.ClosedAtNewYork,
            candidate.OrderedExecutions.Count,
            openingQuantity,
            WeightedAverage(entryExecutions),
            exitExecutions.Length == 0 ? null : WeightedAverage(exitExecutions),
            SumSourcePnL(candidate.SourceRecordIndices, parseResult),
            candidate.ExistingInstrumentId.HasValue
                ? TradovateInstrumentResolutionStatus.ExistingInstrument
                : TradovateInstrumentResolutionStatus.ProposedCreation);
    }

    private static decimal WeightedAverage(
        IReadOnlyCollection<TradovatePreparedExecution> executions)
    {
        decimal quantity = 0m;
        decimal weightedPrice = 0m;
        checked
        {
            foreach (TradovatePreparedExecution execution in executions)
            {
                quantity += execution.Quantity;
                weightedPrice += execution.Price * execution.Quantity;
            }
        }

        return weightedPrice / quantity;
    }

    private static decimal? SumSourcePnL(
        IReadOnlyCollection<int> sourceRecordIndices,
        TradovateCsvParseResult parseResult)
    {
        Dictionary<int, decimal> pnlByRecord = parseResult.Rows.ToDictionary(
            row => row.SourceRecordIndex,
            row => row.SourceReportedPnL);
        int[] uniqueIndices = sourceRecordIndices.Distinct().ToArray();
        if (uniqueIndices.Any(index => !pnlByRecord.ContainsKey(index)))
        {
            return null;
        }

        decimal total = 0m;
        checked
        {
            foreach (int index in uniqueIndices)
            {
                total += pnlByRecord[index];
            }
        }

        return total;
    }

    private static DateTimeOffset ToNewYorkTimestamp(TradovatePreparedExecution execution) =>
        new(execution.TradingLocalTimestamp, execution.TradingUtcOffset);

    private static IReadOnlyList<TradovateImportPreviewDiagnostic> BuildDiagnostics(
        TradovateCsvParseResult parseResult,
        TradovateExecutionReconstructionResult reconstruction,
        TradovateImportPreparationResult preparation)
    {
        var diagnostics = new List<TradovateImportPreviewDiagnostic>();
        diagnostics.AddRange(parseResult.Diagnostics.Select(diagnostic => new TradovateImportPreviewDiagnostic(
            TradovateImportPreviewDiagnosticStage.Csv,
            diagnostic.Severity == TradovateCsvDiagnosticSeverity.Error
                ? TradovateReconstructionDiagnosticSeverity.Error
                : TradovateReconstructionDiagnosticSeverity.Warning,
            diagnostic.Code,
            diagnostic.Message,
            BuildCsvContext(diagnostic))));
        diagnostics.AddRange(reconstruction.Diagnostics.Select(diagnostic =>
            new TradovateImportPreviewDiagnostic(
                TradovateImportPreviewDiagnosticStage.Reconstruction,
                diagnostic.Severity,
                diagnostic.Code,
                diagnostic.Message,
                diagnostic.BrokerSymbol)));
        diagnostics.AddRange(preparation.InstrumentResolution.Diagnostics.Select(diagnostic =>
            new TradovateImportPreviewDiagnostic(
                TradovateImportPreviewDiagnosticStage.Instrument,
                diagnostic.Severity,
                diagnostic.Code,
                diagnostic.Message,
                diagnostic.CanonicalSymbol ?? string.Join(", ", diagnostic.SourceBrokerSymbols))));
        diagnostics.AddRange(preparation.Diagnostics.Select(diagnostic =>
            new TradovateImportPreviewDiagnostic(
                TradovateImportPreviewDiagnosticStage.Preparation,
                diagnostic.Severity,
                diagnostic.Code,
                diagnostic.Message,
                diagnostic.BrokerSymbol)));

        if (!reconstruction.IsSourceCompletenessIndependentlyVerified &&
            !diagnostics.Any(item => item.Code ==
                TradovateReconstructionDiagnosticCodes.SourceCompletenessUnverified))
        {
            diagnostics.Add(new TradovateImportPreviewDiagnostic(
                TradovateImportPreviewDiagnosticStage.Preview,
                TradovateReconstructionDiagnosticSeverity.Warning,
                TradovateReconstructionDiagnosticCodes.SourceCompletenessUnverified,
                "The matched-fill export cannot prove that the source selection is complete."));
        }

        diagnostics.Add(new TradovateImportPreviewDiagnostic(
            TradovateImportPreviewDiagnosticStage.Preview,
            TradovateReconstructionDiagnosticSeverity.Warning,
            "COSTS_UNAVAILABLE",
            "Commission and fee inputs are not available in this preview; final import remains disabled."));

        return diagnostics
            .DistinctBy(item => new
            {
                item.Stage,
                item.Severity,
                item.Code,
                item.Message,
                item.Context,
            })
            .OrderBy(item => item.Stage)
            .ThenByDescending(item => item.Severity)
            .ThenBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.Context, StringComparer.Ordinal)
            .ToArray();
    }

    private static string? BuildCsvContext(TradovateCsvDiagnostic diagnostic)
    {
        var parts = new List<string>();
        if (diagnostic.SourceRecordIndex.HasValue)
        {
            parts.Add($"Record {diagnostic.SourceRecordIndex.Value}");
        }

        if (diagnostic.SourceLineNumber.HasValue)
        {
            parts.Add($"line {diagnostic.SourceLineNumber.Value}");
        }

        if (!string.IsNullOrWhiteSpace(diagnostic.FieldName))
        {
            parts.Add(diagnostic.FieldName);
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }
}
