using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Imports.Topstep;

/// <summary>Fresh read-only reference resolution on every call; no cached selection or write services.</summary>
public sealed class TopstepReferencePreparationService
{
    private const string MnqEvidence = "CME MNQ specifications / quarterly cycle, reviewed 2026-09-28: https://www.cmegroup.com/markets/equities/nasdaq/micro-e-mini-nasdaq-100.contractSpecs.html";
    private readonly IInstrumentReader _instruments;
    private readonly ITradingAccountReader _accounts;

    public TopstepReferencePreparationService(IInstrumentReader instruments, ITradingAccountReader accounts)
    {
        ArgumentNullException.ThrowIfNull(instruments);
        ArgumentNullException.ThrowIfNull(accounts);
        _instruments = instruments;
        _accounts = accounts;
    }

    /// <param name="verifiedExistingInstruments">
    /// Explicit caller attestation that a non-profile catalog Instrument's identity/specifications
    /// were independently verified. Catalog existence or matching PnL is not verification.
    /// Never used to filter away competing canonical matches.
    /// </param>
    public async Task<TopstepReferencePreparationResult> PrepareAsync(
        TopstepTradeReconstructionResult source, Guid? selectedTradingAccountId,
        TopstepCostInterpretation costInterpretation = TopstepCostInterpretation.Unverified,
        IReadOnlyCollection<TopstepInstrumentVerification>? verifiedExistingInstruments = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        var diagnostics = new List<TopstepReferenceDiagnostic>();
        var resolutions = new List<TopstepInstrumentResolution>();
        var pricing = new Dictionary<string, TradePricingSnapshot>(StringComparer.Ordinal);
        if (!source.CanUseRowCandidates)
        {
            diagnostics.Add(new(TopstepReferenceDiagnosticSeverity.Error, TopstepReferenceDiagnosticCodes.SourceNotReady,
                null, "Correct source parsing/reconstruction errors before reference preparation."));
            return new(selectedTradingAccountId, null, [],
                new TopstepEconomicsReconciler().Reconcile(source, pricing, costInterpretation, cancellationToken), [], diagnostics);
        }

        TradingAccountDetails? account = null;
        bool validAccount = false;
        void AccountError(string code, string message) => diagnostics.Add(new(TopstepReferenceDiagnosticSeverity.Error, code, null, message));
        if (selectedTradingAccountId is null || selectedTradingAccountId == Guid.Empty)
        {
            AccountError(TopstepReferenceDiagnosticCodes.AccountSelectionRequired, "Explicitly select a destination Topstep Trading Account for this source. No account is inferred or remembered by preparation.");
        }
        else
        {
            account = await _accounts.GetByIdAsync(selectedTradingAccountId.Value, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (account is null || account.Id != selectedTradingAccountId.Value)
            {
                AccountError(TopstepReferenceDiagnosticCodes.AccountNotFound, "The selected Account no longer exists. Refresh Accounts and explicitly select an available Topstep account.");
            }
            else
            {
                if (!string.Equals(account.ProviderName?.Trim(), "Topstep", StringComparison.OrdinalIgnoreCase))
                    AccountError(TopstepReferenceDiagnosticCodes.AccountProviderMismatch, "Select an Account whose ProviderName is Topstep (trimmed, case-insensitive exact match), or verify/correct its provider in Accounts. No provider alias or unrelated account is chosen automatically.");
                if (!string.Equals(account.Currency, "USD", StringComparison.OrdinalIgnoreCase))
                    AccountError(TopstepReferenceDiagnosticCodes.AccountCurrencyMismatch, "The reviewed Topstep cost schema is USD. Select a USD Topstep Account; no currency conversion is performed.");
                validAccount = diagnostics.All(d => d.Severity != TopstepReferenceDiagnosticSeverity.Error);
                if (!account.IsActive)
                    diagnostics.Add(new(TopstepReferenceDiagnosticSeverity.Warning, TopstepReferenceDiagnosticCodes.AccountInactive,
                        null, "The explicitly selected Account is inactive. Historical rows can be reviewed without reactivating it."));
            }
        }

        IReadOnlyList<InstrumentListItem> catalog = await _instruments.GetAllAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var verifiedSnapshots = (verifiedExistingInstruments ?? []).ToHashSet();
        foreach (string contract in source.Candidates.Select(c => c.ContractName).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            TopstepInstrumentResolution resolution = Resolve(contract, catalog, verifiedSnapshots, diagnostics);
            resolutions.Add(resolution);
            if (resolution.Pricing is not null) pricing.Add(contract, resolution.Pricing);
        }

        TopstepEconomicsReconciliationResult economics = new TopstepEconomicsReconciler()
            .Reconcile(source, pricing, costInterpretation, cancellationToken);
        var mappings = resolutions.ToDictionary(r => r.SourceContract, StringComparer.Ordinal);
        var rows = new List<TopstepMappedCandidate>();
        if (validAccount)
        {
            foreach (TopstepReconciledTradeEconomics row in economics.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                rows.Add(new(row, account!, mappings[row.Candidate.ContractName]));
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new(selectedTradingAccountId, account, resolutions, economics, rows, diagnostics);
    }

    private static TopstepInstrumentResolution Resolve(string contract, IReadOnlyList<InstrumentListItem> catalog,
        IReadOnlySet<TopstepInstrumentVerification> verifiedSnapshots, List<TopstepReferenceDiagnostic> diagnostics)
    {
        TopstepContractIdentity? identity = TopstepContractIdentity.Parse(contract);
        InstrumentListItem[] matches = identity is null ? [] : catalog
            .Where(i => string.Equals(i.Symbol, identity.CanonicalSymbol, StringComparison.OrdinalIgnoreCase))
            .OrderBy(i => i.Id).ToArray();
        TopstepInstrumentResolution Block(string code, string message)
        {
            diagnostics.Add(new(TopstepReferenceDiagnosticSeverity.Error, code, contract, message));
            return new(contract, identity, TopstepInstrumentResolutionStatus.Blocked, matches, null, null, null);
        }

        if (identity is null)
            return Block(TopstepReferenceDiagnosticCodes.UnrecognizedContract, "Supply a verified futures root/month/one-or-two-digit-year contract code. No fuzzy or continuous-symbol mapping is inferred.");
        // Cardinality is checked before activity, validity, or attestation, like M10's confirmation safeguard.
        if (matches.Length > 1)
            return Block(TopstepReferenceDiagnosticCodes.MultipleInstrumentMatches, "Multiple Instruments share this canonical symbol, including inactive/invalid records. Resolve the catalog ambiguity and rerun; none is chosen.");

        bool mnq = identity.CanonicalSymbol == "MNQ";
        if (mnq && !"HMUZ".Contains(identity.MonthCode))
            return Block(TopstepReferenceDiagnosticCodes.UnrecognizedContract, "The verified MNQ profile lists March/June/September/December (H/M/U/Z). Verify the source contract; no different expiry is substituted.");

        if (matches.Length == 0)
        {
            if (!mnq)
                return Block(TopstepReferenceDiagnosticCodes.InstrumentMetadataRequired, "No canonical Instrument or verified creation profile exists. Supply independently verified contract identity, Futures classification, display name, exchange, currency, tick size/value and point value; create/verify the catalog entry separately and rerun.");
            var proposal = new TopstepInstrumentCreationProposal("MNQ", "Micro E-mini Nasdaq-100", AssetClass.Futures,
                "CME", "USD", 0.25m, 0.50m, 2m, MnqEvidence);
            diagnostics.Add(new(TopstepReferenceDiagnosticSeverity.Warning, TopstepReferenceDiagnosticCodes.InstrumentCreationProposed,
                contract, "A fully specified MNQ creation proposal is available for review. No Instrument has been created; explicit approval is required in the later confirmation workflow."));
            return new(contract, identity, TopstepInstrumentResolutionStatus.ProposedCreation, [], proposal,
                new(proposal.PointValue, proposal.Currency), MnqEvidence);
        }

        InstrumentListItem instrument = matches[0];
        if (instrument.Id == Guid.Empty || instrument.AssetClass != AssetClass.Futures ||
            string.IsNullOrWhiteSpace(instrument.DisplayName) || string.IsNullOrWhiteSpace(instrument.Exchange) ||
            string.IsNullOrWhiteSpace(instrument.Currency) || instrument.Currency.Length > 8 ||
            instrument.TickSize <= 0m || instrument.TickValue <= 0m || instrument.PointValue <= 0m)
            return Block(TopstepReferenceDiagnosticCodes.InstrumentSpecificationMismatch, "The existing Instrument needs complete verified Futures identity, name, exchange, currency, and positive tick/point economics. Correct the catalog and rerun.");
        try
        {
            if (instrument.PointValue != instrument.TickValue / instrument.TickSize ||
                checked(instrument.PointValue * instrument.TickSize) != instrument.TickValue)
                return Block(TopstepReferenceDiagnosticCodes.InstrumentSpecificationMismatch, "Instrument point value and tick size/value are inconsistent. Verify the catalog specifications and rerun.");
        }
        catch (OverflowException)
        {
            return Block(TopstepReferenceDiagnosticCodes.InstrumentSpecificationMismatch, "Instrument tick/point economics exceed supported decimal arithmetic. Correct the verified catalog specifications.");
        }

        if (mnq && (!string.Equals(instrument.Currency, "USD", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(instrument.Exchange, "CME", StringComparison.OrdinalIgnoreCase) ||
                    instrument.TickSize != 0.25m || instrument.TickValue != 0.50m || instrument.PointValue != 2m))
            return Block(TopstepReferenceDiagnosticCodes.InstrumentSpecificationMismatch, "Existing MNQ specifications conflict with verified CME/USD metadata: tick size 0.25, tick value 0.50, point value 2. Correct reference data explicitly; source PnL is not a specification source.");
        if (!mnq && !verifiedSnapshots.Contains(new(contract, instrument)))
            return Block(TopstepReferenceDiagnosticCodes.InstrumentVerificationRequired, "This root has no built-in verified profile. Explicitly verify the existing Instrument's contract identity and full specifications before supplying its verification attestation; matching PnL or catalog existence is insufficient.");
        if (!instrument.IsActive)
            diagnostics.Add(new(TopstepReferenceDiagnosticSeverity.Warning, TopstepReferenceDiagnosticCodes.InstrumentInactive,
                contract, "The uniquely resolved, verified Instrument is inactive and is reused for historical review without reactivation."));
        string evidence = mnq ? MnqEvidence : "Explicit caller verification of existing Instrument identity/specifications";
        return new(contract, identity, TopstepInstrumentResolutionStatus.ExistingInstrument, matches, null,
            new(instrument.PointValue, instrument.Currency), evidence);
    }
}
