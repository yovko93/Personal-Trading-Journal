using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Domain.Instruments;

namespace PersonalTradingJournal.Application.Imports.Tradovate;

/// <summary>
/// Reads existing Instruments and plans import mappings without changing reference data.
/// </summary>
public sealed class TradovateInstrumentResolver
{
    private readonly IInstrumentReader _instrumentReader;

    public TradovateInstrumentResolver(IInstrumentReader instrumentReader)
    {
        ArgumentNullException.ThrowIfNull(instrumentReader);
        _instrumentReader = instrumentReader;
    }

    public async Task<TradovateInstrumentResolutionResult> ResolveAsync(
        TradovateExecutionReconstructionResult reconstruction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reconstruction);
        cancellationToken.ThrowIfCancellationRequested();

        if (!reconstruction.IsEligibleForAutomaticImport)
        {
            return Blocked(
                TradovateInstrumentResolutionDiagnosticCodes.ReconstructionNotEligible,
                "Instrument resolution requires an eligible reconstruction with at least one Trade candidate.");
        }

        string[] candidateSymbols = reconstruction.Candidates
            .Select(candidate => candidate.BrokerSymbol)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] executionSymbols = reconstruction.Executions
            .Select(execution => execution.BrokerSymbol)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (!candidateSymbols.SequenceEqual(executionSymbols, StringComparer.Ordinal) ||
            reconstruction.Candidates.Any(candidate => candidate.OrderedExecutions.Any(
                execution => !string.Equals(
                    execution.BrokerSymbol,
                    candidate.BrokerSymbol,
                    StringComparison.Ordinal))))
        {
            return Blocked(
                TradovateInstrumentResolutionDiagnosticCodes.ReconstructionSymbolCoverageMismatch,
                "Candidate and reconstructed-execution broker symbols do not align.");
        }

        IReadOnlyList<InstrumentListItem> existingInstruments =
            await _instrumentReader.GetAllAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var diagnostics = new List<TradovateInstrumentResolutionDiagnostic>();
        var mappings = new List<TradovateBrokerSymbolMapping>();
        var canonicalResolutions = new List<TradovateCanonicalInstrumentResolution>();
        var canonicalGroups = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (string brokerSymbol in candidateSymbols)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TradovateFuturesContractSymbolParser.TryGetCanonicalSymbol(
                    brokerSymbol,
                    out string? canonicalSymbol))
            {
                const string code =
                    TradovateInstrumentResolutionDiagnosticCodes.UnrecognizedContractSymbol;
                diagnostics.Add(new TradovateInstrumentResolutionDiagnostic(
                    TradovateReconstructionDiagnosticSeverity.Warning,
                    code,
                    canonicalSymbol: null,
                    [brokerSymbol],
                    matchingInstrumentIds: [],
                    "The broker symbol is not a supported futures root/month/year contract code; user mapping is required."));
                mappings.Add(new TradovateBrokerSymbolMapping(
                    brokerSymbol,
                    canonicalSymbol: null,
                    TradovateInstrumentResolutionStatus.RequiresUserInput,
                    existingInstrumentId: null,
                    isExistingInstrumentActive: null,
                    [code]));
                continue;
            }

            if (!canonicalGroups.TryGetValue(canonicalSymbol!, out List<string>? group))
            {
                group = [];
                canonicalGroups.Add(canonicalSymbol!, group);
            }

            group.Add(brokerSymbol);
        }

        foreach ((string canonicalSymbol, List<string> brokerSymbols) in canonicalGroups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TradovateCanonicalInstrumentResolution resolution = ResolveCanonical(
                canonicalSymbol,
                brokerSymbols,
                reconstruction.Executions,
                existingInstruments,
                diagnostics);
            canonicalResolutions.Add(resolution);
            foreach (string brokerSymbol in brokerSymbols)
            {
                mappings.Add(new TradovateBrokerSymbolMapping(
                    brokerSymbol,
                    canonicalSymbol,
                    resolution.Status,
                    resolution.ExistingInstrumentId,
                    resolution.IsExistingInstrumentActive,
                    resolution.DiagnosticCodes));
            }
        }

        TradovateInstrumentResolutionOverallStatus status = mappings.Any(mapping =>
            mapping.Status == TradovateInstrumentResolutionStatus.Blocked)
            ? TradovateInstrumentResolutionOverallStatus.Blocked
            : mappings.Any(mapping =>
                mapping.Status == TradovateInstrumentResolutionStatus.RequiresUserInput)
                ? TradovateInstrumentResolutionOverallStatus.RequiresUserInput
                : TradovateInstrumentResolutionOverallStatus.ReadyForPreview;

        return new TradovateInstrumentResolutionResult(
            mappings.OrderBy(mapping => mapping.BrokerSymbol, StringComparer.Ordinal),
            canonicalResolutions,
            diagnostics.OrderBy(diagnostic => diagnostic.CanonicalSymbol, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.SourceBrokerSymbols.FirstOrDefault(), StringComparer.Ordinal),
            status);
    }

    private static TradovateCanonicalInstrumentResolution ResolveCanonical(
        string canonicalSymbol,
        IReadOnlyList<string> brokerSymbols,
        IReadOnlyList<TradovateReconstructedExecution> executions,
        IReadOnlyList<InstrumentListItem> existingInstruments,
        List<TradovateInstrumentResolutionDiagnostic> diagnostics)
    {
        var brokerSymbolSet = brokerSymbols.ToHashSet(StringComparer.Ordinal);
        decimal[] sourceTickSizes = executions
            .Where(execution => brokerSymbolSet.Contains(execution.BrokerSymbol))
            .Select(execution => execution.TickSize)
            .Distinct()
            .Order()
            .ToArray();

        if (sourceTickSizes.Length != 1)
        {
            return Failure(
                canonicalSymbol,
                brokerSymbols,
                sourceTickSize: null,
                TradovateInstrumentResolutionStatus.Blocked,
                TradovateInstrumentResolutionDiagnosticCodes.SourceTickSizeConflict,
                "Source contracts mapping to one Instrument report conflicting tick sizes.",
                matchingInstrumentIds: [],
                diagnostics);
        }

        decimal sourceTickSize = sourceTickSizes[0];
        InstrumentListItem[] matches = existingInstruments
            .Where(instrument => string.Equals(
                instrument.Symbol,
                canonicalSymbol,
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(instrument => instrument.Id)
            .ToArray();
        if (matches.Length > 1)
        {
            return Failure(
                canonicalSymbol,
                brokerSymbols,
                sourceTickSize,
                TradovateInstrumentResolutionStatus.Blocked,
                TradovateInstrumentResolutionDiagnosticCodes.MultipleExistingInstruments,
                "Multiple existing Instruments share the canonical symbol; none can be selected automatically.",
                matches.Select(instrument => instrument.Id),
                diagnostics);
        }

        TradovateKnownInstrumentProfile? profile =
            TradovateKnownInstrumentProfile.ForCanonicalSymbol(canonicalSymbol);
        if (profile is not null && sourceTickSize != profile.TickSize)
        {
            return Failure(
                canonicalSymbol,
                brokerSymbols,
                sourceTickSize,
                TradovateInstrumentResolutionStatus.Blocked,
                TradovateInstrumentResolutionDiagnosticCodes.SourceProfileTickSizeMismatch,
                "Source tick size conflicts with the verified Instrument profile.",
                matches.Select(instrument => instrument.Id),
                diagnostics);
        }

        if (matches.Length == 1)
        {
            return ResolveExisting(
                matches[0],
                canonicalSymbol,
                brokerSymbols,
                sourceTickSize,
                profile,
                diagnostics);
        }

        if (profile is null)
        {
            return Failure(
                canonicalSymbol,
                brokerSymbols,
                sourceTickSize,
                TradovateInstrumentResolutionStatus.RequiresUserInput,
                TradovateInstrumentResolutionDiagnosticCodes.InstrumentMetadataRequired,
                "A missing Instrument requires user-verified currency, tick value, and descriptive metadata.",
                matchingInstrumentIds: [],
                diagnostics,
                TradovateReconstructionDiagnosticSeverity.Warning);
        }

        var proposal = new TradovateInstrumentCreationProposal(
            canonicalSymbol,
            profile.DisplayName,
            AssetClass.Futures,
            profile.Exchange,
            profile.Currency,
            profile.TickSize,
            profile.TickValue,
            brokerSymbols,
            profile.MetadataSource);
        return new TradovateCanonicalInstrumentResolution(
            canonicalSymbol,
            brokerSymbols,
            sourceTickSize,
            TradovateInstrumentResolutionStatus.ProposedCreation,
            existingInstrumentId: null,
            isExistingInstrumentActive: null,
            matchingInstrumentIds: [],
            proposal,
            diagnosticCodes: []);
    }

    private static TradovateCanonicalInstrumentResolution ResolveExisting(
        InstrumentListItem instrument,
        string canonicalSymbol,
        IReadOnlyList<string> brokerSymbols,
        decimal sourceTickSize,
        TradovateKnownInstrumentProfile? profile,
        List<TradovateInstrumentResolutionDiagnostic> diagnostics)
    {
        var codes = new List<string>();
        void Check(bool isMismatch, string code, string message)
        {
            if (!isMismatch)
            {
                return;
            }

            codes.Add(code);
            diagnostics.Add(new TradovateInstrumentResolutionDiagnostic(
                TradovateReconstructionDiagnosticSeverity.Error,
                code,
                canonicalSymbol,
                brokerSymbols,
                [instrument.Id],
                message));
        }

        Check(
            instrument.AssetClass != AssetClass.Futures,
            TradovateInstrumentResolutionDiagnosticCodes.InstrumentAssetClassMismatch,
            "The existing Instrument is not classified as Futures.");
        Check(
            instrument.TickSize != sourceTickSize,
            TradovateInstrumentResolutionDiagnosticCodes.InstrumentTickSizeMismatch,
            "The existing Instrument tick size differs from the source.");
        if (profile is not null)
        {
            Check(
                !string.Equals(instrument.Currency, profile.Currency, StringComparison.OrdinalIgnoreCase),
                TradovateInstrumentResolutionDiagnosticCodes.InstrumentCurrencyMismatch,
                "The existing Instrument currency differs from the verified profile.");
            Check(
                instrument.TickValue != profile.TickValue,
                TradovateInstrumentResolutionDiagnosticCodes.InstrumentTickValueMismatch,
                "The existing Instrument tick value differs from the verified profile.");
        }

        if (codes.Count > 0)
        {
            return new TradovateCanonicalInstrumentResolution(
                canonicalSymbol,
                brokerSymbols,
                sourceTickSize,
                TradovateInstrumentResolutionStatus.Blocked,
                instrument.Id,
                instrument.IsActive,
                [instrument.Id],
                creationProposal: null,
                codes);
        }

        if (!instrument.IsActive)
        {
            const string code =
                TradovateInstrumentResolutionDiagnosticCodes.ExistingInstrumentInactive;
            codes.Add(code);
            diagnostics.Add(new TradovateInstrumentResolutionDiagnostic(
                TradovateReconstructionDiagnosticSeverity.Warning,
                code,
                canonicalSymbol,
                brokerSymbols,
                [instrument.Id],
                "The existing Instrument is inactive and is reused without reactivation."));
        }

        return new TradovateCanonicalInstrumentResolution(
            canonicalSymbol,
            brokerSymbols,
            sourceTickSize,
            TradovateInstrumentResolutionStatus.ExistingInstrument,
            instrument.Id,
            instrument.IsActive,
            [instrument.Id],
            creationProposal: null,
            codes);
    }

    private static TradovateCanonicalInstrumentResolution Failure(
        string canonicalSymbol,
        IReadOnlyList<string> brokerSymbols,
        decimal? sourceTickSize,
        TradovateInstrumentResolutionStatus status,
        string code,
        string message,
        IEnumerable<Guid> matchingInstrumentIds,
        List<TradovateInstrumentResolutionDiagnostic> diagnostics,
        TradovateReconstructionDiagnosticSeverity severity =
            TradovateReconstructionDiagnosticSeverity.Error)
    {
        Guid[] instrumentIds = matchingInstrumentIds.ToArray();
        diagnostics.Add(new TradovateInstrumentResolutionDiagnostic(
            severity,
            code,
            canonicalSymbol,
            brokerSymbols,
            instrumentIds,
            message));
        return new TradovateCanonicalInstrumentResolution(
            canonicalSymbol,
            brokerSymbols,
            sourceTickSize,
            status,
            existingInstrumentId: null,
            isExistingInstrumentActive: null,
            instrumentIds,
            creationProposal: null,
            [code]);
    }

    private static TradovateInstrumentResolutionResult Blocked(string code, string message) =>
        new(
            brokerSymbolMappings: [],
            canonicalInstrumentResolutions: [],
            diagnostics:
            [
                new TradovateInstrumentResolutionDiagnostic(
                    TradovateReconstructionDiagnosticSeverity.Error,
                    code,
                    canonicalSymbol: null,
                    sourceBrokerSymbols: [],
                    matchingInstrumentIds: [],
                    message),
            ],
            TradovateInstrumentResolutionOverallStatus.Blocked);

    private sealed record TradovateKnownInstrumentProfile(
        string DisplayName,
        string Exchange,
        string Currency,
        decimal TickSize,
        decimal TickValue,
        string MetadataSource)
    {
        public static TradovateKnownInstrumentProfile? ForCanonicalSymbol(string symbol) =>
            symbol == "MNQ"
                ? new(
                    "Micro E-mini Nasdaq-100",
                    "CME",
                    "USD",
                    0.25m,
                    0.50m,
                    "CME Group Micro E-mini Nasdaq-100 contract specifications")
                : null;
    }
}
