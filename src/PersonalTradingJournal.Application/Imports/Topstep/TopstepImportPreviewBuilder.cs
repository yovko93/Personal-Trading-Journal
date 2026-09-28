using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace PersonalTradingJournal.Application.Imports.Topstep;

/// <summary>Composes read-only stages. One active request per instance, no cached selection or latest-result state.</summary>
public sealed class TopstepImportPreviewBuilder
{
    private readonly ITopstepCsvParser _parser;
    private readonly ITopstepTradeCandidateReconstructor _reconstructor;
    private readonly TopstepReferencePreparationService _references;
    private int _building;
    private static readonly JsonSerializerOptions FingerprintJson = new() { Converters = { new CanonicalDecimalConverter() } };

    public TopstepImportPreviewBuilder(ITopstepCsvParser parser, ITopstepTradeCandidateReconstructor reconstructor,
        TopstepReferencePreparationService references)
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(reconstructor);
        ArgumentNullException.ThrowIfNull(references);
        _parser = parser;
        _reconstructor = reconstructor;
        _references = references;
    }

    /// <summary>
    /// The caller owns the source stream and must not mutate/read it concurrently. Readable seekable
    /// streams must start at zero; nonseekable streams must contain the complete file. The source is
    /// consumed but not closed. Hashing and parsing use the same private byte snapshot.
    /// </summary>
    public async Task<TopstepImportPreview> BuildAsync(string fileName, Stream source, Guid? selectedTradingAccountId,
        TopstepCostInterpretation costInterpretation = TopstepCostInterpretation.Unverified,
        IReadOnlyCollection<TopstepInstrumentVerification>? verifiedExistingInstruments = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        if (!source.CanRead || (source.CanSeek && source.Position != 0))
            throw new ArgumentException("Supply a readable complete CSV stream positioned at its beginning.", nameof(source));
        string displayName = Path.GetFileName(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName, nameof(fileName));
        if (Interlocked.CompareExchange(ref _building, 1, 0) != 0)
            throw new InvalidOperationException("A Topstep preview is already being built. Cancel or await it before rebuilding.");
        try
        {
            TopstepInstrumentVerification[] verificationSnapshot = (verifiedExistingInstruments ?? [])
                .Distinct().OrderBy(v => JsonSerializer.Serialize(v, FingerprintJson), StringComparer.Ordinal).ToArray();
            using var bytes = new MemoryStream();
            await source.CopyToAsync(bytes, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            string contentHash = Convert.ToHexString(SHA256.HashData(bytes.GetBuffer().AsSpan(0, checked((int)bytes.Length))));
            var identity = new TopstepPreviewSourceIdentity(displayName, bytes.Length, contentHash);
            bytes.Position = 0;
            TopstepCsvParseResult parsed = await _parser.ParseAsync(bytes, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            TopstepTradeReconstructionResult reconstructed = _reconstructor.Reconstruct(parsed, cancellationToken);
            TopstepReferencePreparationResult preparation = await _references.PrepareAsync(reconstructed,
                selectedTradingAccountId, costInterpretation, verificationSnapshot, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            List<TopstepPreviewDiagnostic> diagnostics = BuildDiagnostics(preparation);
            var resolutions = preparation.Instruments.ToDictionary(r => r.SourceContract, StringComparer.Ordinal);
            TopstepPreviewCandidate[] candidates = preparation.Economics.Rows
                .Select(row => new TopstepPreviewCandidate(row, resolutions[row.Candidate.ContractName])).ToArray();
            TopstepPreviewTotals? totals = Totals(preparation, diagnostics);
            var requirements = new List<TopstepPreviewReviewRequirement>();
            foreach (TopstepPreviewDiagnostic diagnostic in diagnostics.Where(d => d.Severity == TopstepPreviewSeverity.Warning))
            {
                string key = "warning:" + Hash(diagnostic);
                requirements.Add(new(key, TopstepPreviewReviewKind.WarningAcknowledgment, diagnostic.Message, diagnostic.SourceReferences));
            }
            // Defensive explicit boundary notice even if an alternate reconstructor omitted its warning.
            if (candidates.Length > 0 && !diagnostics.Any(d => d.Stage == TopstepPreviewDiagnosticStage.Reconstruction &&
                    d.Code == TopstepReconstructionDiagnosticCodes.PositionBoundariesUnverified))
                requirements.Add(new("closed-row-boundaries", TopstepPreviewReviewKind.WarningAcknowledgment,
                    "These are separate reported closed rows, not verified broker positions. Review unverified position boundaries before accepting.",
                    Array.AsReadOnly(candidates.Select(c => new TopstepSourceReference(c.SourceRecordIndex, c.SourceLineNumber)).ToArray())));
            foreach (TopstepInstrumentCreationProposal proposal in preparation.CreationProposals)
                requirements.Add(new("create:" + proposal.CanonicalSymbol, TopstepPreviewReviewKind.InstrumentCreationApproval,
                    "Explicitly approve the displayed verified Instrument specifications for later creation; preview creates nothing.",
                    Array.AsReadOnly(candidates.Where(c => c.Instrument.CreationProposal == proposal)
                        .Select(c => new TopstepSourceReference(c.SourceRecordIndex, c.SourceLineNumber)).ToArray()), proposal.CanonicalSymbol));

            var summary = new TopstepPreviewSummary(parsed.SourceRecordCount, parsed.ValidRecordCount, parsed.RejectedRecordCount,
                candidates.Length, preparation.Instruments.Count(i => i.Status == TopstepInstrumentResolutionStatus.ExistingInstrument),
                preparation.Instruments.Count(i => i.Status == TopstepInstrumentResolutionStatus.ProposedCreation),
                preparation.Instruments.Count(i => i.Status == TopstepInstrumentResolutionStatus.Blocked), preparation.CreationProposals.Count,
                diagnostics.Count(d => d.Severity == TopstepPreviewSeverity.Warning), diagnostics.Count(d => d.Severity == TopstepPreviewSeverity.Error), totals);
            // Versioned deterministic JSON uses fixed property order, invariant decimal/date serialization,
            // sorted verification/matching sets and source-record ordering. Never a runtime GetHashCode.
            string fingerprint = Hash(new { TopstepImportPreview.PolicyVersion, identity, selectedTradingAccountId,
                costInterpretation, verificationSnapshot, preparation, summary, diagnostics, requirements });
            cancellationToken.ThrowIfCancellationRequested();
            return new(identity, fingerprint, preparation, summary, candidates, diagnostics, requirements, verificationSnapshot);
        }
        finally
        {
            Volatile.Write(ref _building, 0);
        }
    }

    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, FingerprintJson)));

    private sealed class CanonicalDecimalConverter : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => reader.GetDecimal();
        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
            writer.WriteRawValue(value.ToString("G29", CultureInfo.InvariantCulture));
    }

    private static TopstepPreviewTotals? Totals(TopstepReferencePreparationResult preparation, List<TopstepPreviewDiagnostic> diagnostics)
    {
        if (!preparation.Economics.IsEconomicallyReconciled) return null;
        try
        {
            IReadOnlyList<TopstepReconciledTradeEconomics> rows = preparation.Economics.Rows;
            var totals = new TopstepPreviewTotals("USD", rows.Sum(r => r.SourceReportedPnL), rows.Sum(r => r.CalculatedGrossPnL!.Value),
                rows.Sum(r => r.SourceReportedFees), rows.Sum(r => r.SourceReportedCommissions), rows.Sum(r => r.NetPnL!.Value));
            if (!IsExactSum(rows.Select(r => r.SourceReportedPnL), totals.ReportedGross) ||
                !IsExactSum(rows.Select(r => r.CalculatedGrossPnL!.Value), totals.CalculatedGross) ||
                !IsExactSum(rows.Select(r => r.SourceReportedFees), totals.Fees) ||
                !IsExactSum(rows.Select(r => r.SourceReportedCommissions), totals.Commissions) ||
                !IsExactSum(rows.Select(r => r.NetPnL!.Value), totals.Net))
            {
                diagnostics.Add(new(TopstepPreviewDiagnosticStage.Preview, TopstepPreviewSeverity.Error, "TOTALS_PRECISION_LOSS",
                    "The complete row totals cannot be represented exactly as decimals.",
                    "Review source amounts; a rounded summary cannot replace the exact row economics.", Array.Empty<TopstepSourceReference>()));
                return null;
            }
            return totals;
        }
        catch (OverflowException)
        {
            diagnostics.Add(new(TopstepPreviewDiagnosticStage.Preview, TopstepPreviewSeverity.Error, "TOTALS_OVERFLOW",
                "The complete reconciled row totals exceed supported decimal range.",
                "Review the export amounts; a partial total or rounded replacement cannot be accepted.", Array.Empty<TopstepSourceReference>()));
            return null;
        }
    }

    private static bool IsExactSum(IEnumerable<decimal> values, decimal total)
    {
        // Audit decimal addition without replacing the Domain's row economics or rounding a result.
        static BigInteger CoefficientAtScale28(decimal value)
        {
            int[] bits = decimal.GetBits(value);
            BigInteger coefficient = (uint)bits[0] + ((BigInteger)(uint)bits[1] << 32) + ((BigInteger)(uint)bits[2] << 64);
            if (bits[3] < 0) coefficient = -coefficient;
            return coefficient * BigInteger.Pow(10, 28 - ((bits[3] >> 16) & 0xff));
        }
        return values.Aggregate(BigInteger.Zero, (sum, value) => sum + CoefficientAtScale28(value)) == CoefficientAtScale28(total);
    }

    private static List<TopstepPreviewDiagnostic> BuildDiagnostics(TopstepReferencePreparationResult preparation)
    {
        TopstepTradeReconstructionResult source = preparation.Economics.Source;
        var diagnostics = new List<TopstepPreviewDiagnostic>();
        static IReadOnlyList<TopstepSourceReference> Row(int? index, int? line) => index.HasValue && line.HasValue
            ? Array.AsReadOnly(new[] { new TopstepSourceReference(index.Value, line.Value) }) : Array.Empty<TopstepSourceReference>();
        foreach (TopstepCsvDiagnostic d in source.Source.Diagnostics)
            diagnostics.Add(new(TopstepPreviewDiagnosticStage.Csv,
                d.Severity == TopstepCsvDiagnosticSeverity.Error ? TopstepPreviewSeverity.Error : TopstepPreviewSeverity.Warning,
                d.Code, d.Message, "Correct the indicated CSV field/line or select a valid Topstep export, then rebuild preview.",
                Row(d.SourceRecordIndex, d.SourceLineNumber), FieldName: d.FieldName, SourceLineNumber: d.SourceLineNumber));
        foreach (TopstepReconstructionDiagnostic d in source.Diagnostics)
            diagnostics.Add(new(TopstepPreviewDiagnosticStage.Reconstruction,
                d.Severity == TopstepReconstructionDiagnosticSeverity.Error ? TopstepPreviewSeverity.Error : TopstepPreviewSeverity.Warning,
                d.Code, d.Message, d.Severity == TopstepReconstructionDiagnosticSeverity.Warning
                    ? "Review the affected closed-row records and explicitly acknowledge that complete broker position boundaries are unverified."
                    : "Correct the source rows indicated by the reconstruction diagnostic and rebuild; rows cannot be merged or guessed.", d.SourceReferences));
        foreach (TopstepReferenceDiagnostic d in preparation.Diagnostics)
        {
            bool account = d.Code.StartsWith("ACCOUNT_", StringComparison.Ordinal);
            IReadOnlyList<TopstepSourceReference> affected = Array.AsReadOnly(source.Candidates
                .Where(c => d.SourceContract is null || c.ContractName == d.SourceContract)
                .Select(c => new TopstepSourceReference(c.SourceRow.SourceRecordIndex, c.SourceRow.SourceLineNumber)).ToArray());
            TopstepPreviewDiagnosticStage stage = d.Code == TopstepReferenceDiagnosticCodes.SourceNotReady
                ? TopstepPreviewDiagnosticStage.Preview
                : account ? TopstepPreviewDiagnosticStage.Account : TopstepPreviewDiagnosticStage.Instrument;
            diagnostics.Add(new(stage,
                d.Severity == TopstepReferenceDiagnosticSeverity.Error ? TopstepPreviewSeverity.Error : TopstepPreviewSeverity.Warning,
                d.Code, d.Message, d.Message + " Rebuild preview after changing the file, account or reference data.", affected, d.SourceContract));
        }
        foreach (TopstepEconomicsDiagnostic d in preparation.Economics.Diagnostics)
            diagnostics.Add(new(TopstepPreviewDiagnosticStage.Economics, TopstepPreviewSeverity.Error, d.Code, d.Message,
                "Verify source amounts, the reported cost interpretation and Instrument specifications, then rebuild. Do not substitute zero costs or alter source PnL.",
                Row(d.SourceRecordIndex, d.SourceLineNumber), FieldName: d.FieldName, SourceLineNumber: d.SourceLineNumber));
        return diagnostics;
    }
}
