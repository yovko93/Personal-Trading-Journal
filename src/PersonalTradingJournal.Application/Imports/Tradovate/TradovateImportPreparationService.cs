using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Common.Time;

namespace PersonalTradingJournal.Application.Imports.Tradovate;

/// <summary>
/// Combines an approved reconstruction, its Instrument resolution, and an explicitly
/// selected Account into an immutable, read-only import preview model.
/// </summary>
public sealed class TradovateImportPreparationService
{
    private readonly ITradingAccountReader _tradingAccountReader;

    public TradovateImportPreparationService(ITradingAccountReader tradingAccountReader)
    {
        ArgumentNullException.ThrowIfNull(tradingAccountReader);
        _tradingAccountReader = tradingAccountReader;
    }

    public async Task<TradovateImportPreparationResult> PrepareAsync(
        TradovateExecutionReconstructionResult reconstruction,
        TradovateInstrumentResolutionResult instrumentResolution,
        Guid selectedTradingAccountId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reconstruction);
        ArgumentNullException.ThrowIfNull(instrumentResolution);
        cancellationToken.ThrowIfCancellationRequested();

        if (!reconstruction.IsEligibleForAutomaticImport)
        {
            return Failed(
                instrumentResolution,
                TradovateImportPreparationStatus.Blocked,
                TradovateImportPreparationDiagnosticCodes.ReconstructionNotEligible,
                "Import preparation requires an eligible reconstruction with at least one Trade candidate.");
        }

        if (!instrumentResolution.IsReadyForPreview)
        {
            return Failed(
                instrumentResolution,
                TradovateImportPreparationStatus.Blocked,
                TradovateImportPreparationDiagnosticCodes.InstrumentResolutionNotReady,
                "Import preparation requires a complete Instrument resolution.");
        }

        if (selectedTradingAccountId == Guid.Empty)
        {
            return AccountNotFound(instrumentResolution, selectedTradingAccountId);
        }

        TradingAccountDetails? account = await _tradingAccountReader.GetByIdAsync(
            selectedTradingAccountId,
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (account is null)
        {
            return AccountNotFound(instrumentResolution, selectedTradingAccountId);
        }

        var accountSnapshot = new TradovateImportAccountSnapshot(
            account.Id,
            account.Name,
            account.AccountType,
            account.ProviderName,
            account.ExternalAccountId,
            account.Currency,
            account.IsActive);
        var diagnostics = new List<TradovateImportPreparationDiagnostic>();
        if (!account.IsActive)
        {
            diagnostics.Add(new TradovateImportPreparationDiagnostic(
                TradovateReconstructionDiagnosticSeverity.Warning,
                TradovateImportPreparationDiagnosticCodes.SelectedAccountInactive,
                "The selected Trading Account is inactive and is used without reactivation."));
        }

        var preparedByKey = new Dictionary<ExecutionKey, TradovatePreparedExecution>();
        var preparedExecutions = new List<TradovatePreparedExecution>(
            reconstruction.Executions.Count);
        TradovateImportPreparationStatus? conversionFailureStatus = null;
        foreach (TradovateReconstructedExecution execution in reconstruction.Executions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LocalTimeConversionResult conversion =
                TradingTimePolicy.ConvertTradovateSourceToUtc(
                    execution.SourceLocalTimestamp);
            if (!conversion.IsSuccess)
            {
                bool isAmbiguous =
                    conversion.Status == LocalTimeConversionStatus.Ambiguous;
                diagnostics.Add(new TradovateImportPreparationDiagnostic(
                    TradovateReconstructionDiagnosticSeverity.Error,
                    isAmbiguous
                        ? TradovateImportPreparationDiagnosticCodes.AmbiguousSourceLocalTime
                        : TradovateImportPreparationDiagnosticCodes.InvalidSourceLocalTime,
                    isAmbiguous
                        ? "The Europe/Sofia source time is ambiguous because of a daylight-saving transition and requires correction."
                        : "The Europe/Sofia source time does not exist because of a daylight-saving transition.",
                    execution.BrokerSymbol,
                    execution.SourceRecordIndices,
                    execution.SourceLineNumbers));
                if (!isAmbiguous || !conversionFailureStatus.HasValue)
                {
                    conversionFailureStatus = isAmbiguous
                        ? TradovateImportPreparationStatus.RequiresUserInput
                        : TradovateImportPreparationStatus.Blocked;
                }
                continue;
            }

            DateTimeOffset executedAtUtc = conversion.UtcTimestamp!.Value;
            DateTimeOffset tradingTimestamp =
                TradingTimePolicy.ConvertUtcToTradingTime(executedAtUtc);
            var prepared = new TradovatePreparedExecution(
                execution.BrokerSymbol,
                execution.Side,
                execution.ExternalFillId,
                execution.Quantity,
                execution.Price,
                execution.SourceLocalTimestamp,
                TradingTimePolicy.TradovateSourceTimeZoneId,
                executedAtUtc,
                tradingTimestamp.DateTime,
                tradingTimestamp.Offset,
                TradingTimePolicy.TradingTimeZoneId,
                execution.SourceRecordIndices,
                execution.SourceLineNumbers);
            preparedExecutions.Add(prepared);
            preparedByKey.Add(ExecutionKey.From(execution), prepared);
        }

        if (conversionFailureStatus.HasValue)
        {
            return new TradovateImportPreparationResult(
                accountSnapshot,
                instrumentResolution,
                preparedExecutions: [],
                preparedCandidates: [],
                diagnostics,
                conversionFailureStatus.Value);
        }

        var preparedCandidates = new List<TradovatePreparedTradeCandidate>(
            reconstruction.Candidates.Count);
        bool blocked = false;
        foreach (TradovateTradeCandidate candidate in reconstruction.Candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TradovateBrokerSymbolMapping? mapping = instrumentResolution.BrokerSymbolMappings
                .SingleOrDefault(item => string.Equals(
                    item.BrokerSymbol,
                    candidate.BrokerSymbol,
                    StringComparison.Ordinal));
            TradovateCanonicalInstrumentResolution? canonicalResolution =
                mapping?.CanonicalSymbol is null
                    ? null
                    : instrumentResolution.CanonicalInstrumentResolutions.SingleOrDefault(
                        item => string.Equals(
                            item.CanonicalSymbol,
                            mapping.CanonicalSymbol,
                            StringComparison.Ordinal));
            bool hasExistingInstrument =
                mapping?.Status == TradovateInstrumentResolutionStatus.ExistingInstrument &&
                mapping.ExistingInstrumentId.HasValue &&
                canonicalResolution?.ExistingInstrumentId == mapping.ExistingInstrumentId;
            bool hasCreationProposal =
                mapping?.Status == TradovateInstrumentResolutionStatus.ProposedCreation &&
                mapping.ExistingInstrumentId is null &&
                canonicalResolution?.CreationProposal is not null;
            if (mapping is null || canonicalResolution is null ||
                candidate.ProvisionalDirection is null ||
                hasExistingInstrument == hasCreationProposal)
            {
                blocked = true;
                diagnostics.Add(new TradovateImportPreparationDiagnostic(
                    TradovateReconstructionDiagnosticSeverity.Error,
                    TradovateImportPreparationDiagnosticCodes.InstrumentMappingMissing,
                    "The reconstructed Trade candidate has no complete Instrument mapping.",
                    candidate.BrokerSymbol,
                    candidate.SourceRecordIndices));
                continue;
            }

            TradovatePreparedExecution[] orderedExecutions = candidate.OrderedExecutions
                .Select(execution => preparedByKey[ExecutionKey.From(execution)])
                .ToArray();
            if (!HasNonDecreasingUtcChronology(orderedExecutions))
            {
                blocked = true;
                diagnostics.Add(new TradovateImportPreparationDiagnostic(
                    TradovateReconstructionDiagnosticSeverity.Error,
                    TradovateImportPreparationDiagnosticCodes.UtcChronologyInvalid,
                    "The reconstructed execution sequence is not chronological after UTC conversion.",
                    candidate.BrokerSymbol,
                    candidate.SourceRecordIndices));
                continue;
            }

            DateTimeOffset? closedAtUtc = candidate.ClosingLocalTimestamp.HasValue
                ? orderedExecutions[^1].ExecutedAtUtc
                : null;
            DateTimeOffset openedAtNewYork = new(
                orderedExecutions[0].TradingLocalTimestamp,
                orderedExecutions[0].TradingUtcOffset);
            DateTimeOffset? closedAtNewYork = closedAtUtc.HasValue
                ? new DateTimeOffset(
                    orderedExecutions[^1].TradingLocalTimestamp,
                    orderedExecutions[^1].TradingUtcOffset)
                : null;
            preparedCandidates.Add(new TradovatePreparedTradeCandidate(
                candidate.BrokerSymbol,
                mapping.CanonicalSymbol!,
                mapping.ExistingInstrumentId,
                canonicalResolution.CreationProposal,
                account.Id,
                candidate.ProvisionalDirection.Value,
                orderedExecutions,
                orderedExecutions[0].ExecutedAtUtc,
                closedAtUtc,
                openedAtNewYork,
                closedAtNewYork,
                candidate.SourceRecordIndices));

            if (canonicalResolution.ResolvedCurrency is { } instrumentCurrency &&
                !string.Equals(
                    account.Currency,
                    instrumentCurrency,
                    StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new TradovateImportPreparationDiagnostic(
                    TradovateReconstructionDiagnosticSeverity.Warning,
                    TradovateImportPreparationDiagnosticCodes.AccountInstrumentCurrencyDifference,
                    "The selected Account and resolved Instrument use different currencies.",
                    candidate.BrokerSymbol,
                    candidate.SourceRecordIndices));
            }
        }

        if (blocked || preparedCandidates.Count == 0)
        {
            if (preparedCandidates.Count == 0 && !diagnostics.Any(diagnostic =>
                    diagnostic.Code == TradovateImportPreparationDiagnosticCodes.InstrumentMappingMissing))
            {
                diagnostics.Add(new TradovateImportPreparationDiagnostic(
                    TradovateReconstructionDiagnosticSeverity.Error,
                    TradovateImportPreparationDiagnosticCodes.ReconstructionNotEligible,
                    "Import preparation produced no Trade candidates."));
            }

            return new TradovateImportPreparationResult(
                accountSnapshot,
                instrumentResolution,
                preparedExecutions: [],
                preparedCandidates: [],
                diagnostics,
                TradovateImportPreparationStatus.Blocked);
        }

        return new TradovateImportPreparationResult(
            accountSnapshot,
            instrumentResolution,
            preparedExecutions,
            preparedCandidates,
            diagnostics,
            TradovateImportPreparationStatus.ReadyForPreview);
    }

    private static bool HasNonDecreasingUtcChronology(
        IReadOnlyList<TradovatePreparedExecution> executions)
    {
        for (int index = 1; index < executions.Count; index++)
        {
            if (executions[index].ExecutedAtUtc < executions[index - 1].ExecutedAtUtc)
            {
                return false;
            }
        }

        return true;
    }

    private static TradovateImportPreparationResult AccountNotFound(
        TradovateInstrumentResolutionResult instrumentResolution,
        Guid accountId) =>
        Failed(
            instrumentResolution,
            TradovateImportPreparationStatus.Blocked,
            TradovateImportPreparationDiagnosticCodes.TradingAccountNotFound,
            $"Trading Account '{accountId}' was not found.");

    private static TradovateImportPreparationResult Failed(
        TradovateInstrumentResolutionResult instrumentResolution,
        TradovateImportPreparationStatus status,
        string code,
        string message) =>
        new(
            accountSnapshot: null,
            instrumentResolution,
            preparedExecutions: [],
            preparedCandidates: [],
            diagnostics:
            [
                new TradovateImportPreparationDiagnostic(
                    TradovateReconstructionDiagnosticSeverity.Error,
                    code,
                    message),
            ],
            status);

    private readonly record struct ExecutionKey(
        string BrokerSymbol,
        Domain.Trades.ExecutionSide Side,
        string ExternalFillId)
    {
        public static ExecutionKey From(TradovateReconstructedExecution execution) =>
            new(execution.BrokerSymbol, execution.Side, execution.ExternalFillId);
    }
}
