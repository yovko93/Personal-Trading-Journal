using PersonalTradingJournal.Application.Imports.Topstep;

namespace PersonalTradingJournal.Infrastructure.Imports.Topstep;

public sealed class TopstepTradeCandidateReconstructor : ITopstepTradeCandidateReconstructor
{
    public TopstepTradeReconstructionResult Reconstruct(
        TopstepCsvParseResult source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        if (!source.IsCompleteInputValid)
        {
            return new(source, [], [new(TopstepReconstructionDiagnosticSeverity.Error,
                TopstepReconstructionDiagnosticCodes.SourceNotValid, [],
                "Row candidates require a non-empty, completely valid CSV. Correct the retained parser diagnostics and parse again.")]);
        }

        var diagnostics = new List<TopstepReconstructionDiagnostic>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var recordIndices = new HashSet<int>();
        var lineNumbers = new HashSet<int>();
        // Record order is for source traceability only, never economic or fill ordering.
        TopstepSourceRow[] rows = source.Rows.OrderBy(row => row.SourceRecordIndex).ToArray();
        foreach (TopstepSourceRow row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (row.SourceRecordIndex <= 0 || row.SourceLineNumber <= 0 ||
                string.IsNullOrWhiteSpace(row.Id) || string.IsNullOrWhiteSpace(row.ContractName) ||
                row.Size <= 0m || !Enum.IsDefined(row.Type) || row.ExitedAtUtc < row.EnteredAtUtc)
            {
                diagnostics.Add(new(TopstepReconstructionDiagnosticSeverity.Error,
                    TopstepReconstructionDiagnosticCodes.InvalidNormalizedRow, [Reference(row)],
                    "A normalized row lacks a valid source location, identity, contract, positive quantity, direction, or ordered instants. Parse the original source again."));
            }

            // Defend the public normalized boundary even when called without the CSV parser.
            bool uniqueId = ids.Add(row.Id);
            bool uniqueRecord = recordIndices.Add(row.SourceRecordIndex);
            bool uniqueLine = lineNumbers.Add(row.SourceLineNumber);
            if (!uniqueId || !uniqueRecord || !uniqueLine)
            {
                diagnostics.Add(new(TopstepReconstructionDiagnosticSeverity.Error,
                    TopstepReconstructionDiagnosticCodes.SourceIdentityNotUnique, [Reference(row)],
                    "Source Ids and record/line locations must be unique. Resolve repeated source rows before candidate preparation."));
            }
        }

        if (diagnostics.Count > 0)
        {
            return new(source, [], diagnostics);
        }

        var candidates = new List<TopstepTradeCandidate>(rows.Length);
        var references = new List<TopstepSourceReference>(rows.Length);
        foreach (TopstepSourceRow row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            candidates.Add(new TopstepTradeCandidate(row));
            references.Add(Reference(row));
        }

        diagnostics.Add(new(TopstepReconstructionDiagnosticSeverity.Warning,
            TopstepReconstructionDiagnosticCodes.PositionBoundariesUnverified, references,
            "Candidates retain individual reported closed rows. The export has no common fill/position identity or account-position history: account flat-to-flat boundaries and source completeness are not verified."));

        // Connected intervals are used ONLY to locate uncertainty, never to merge
        // quantities, average prices, infer reversals, or assign execution sequences.
        foreach (IGrouping<string, TopstepSourceRow> contractRows in rows
                     .GroupBy(row => row.ContractName, StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var related = new List<TopstepSourceRow>();
            DateTimeOffset latestExit = DateTimeOffset.MinValue;
            foreach (TopstepSourceRow row in contractRows.OrderBy(row => row.EnteredAtUtc)
                         .ThenBy(row => row.SourceRecordIndex))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (related.Count > 0 && row.EnteredAtUtc > latestExit)
                {
                    ReportUncertainRelationship(related, diagnostics, cancellationToken);
                    related.Clear();
                }

                if (related.Count == 0 || row.ExitedAtUtc > latestExit)
                {
                    latestExit = row.ExitedAtUtc;
                }

                related.Add(row);
            }

            ReportUncertainRelationship(related, diagnostics, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new(source, candidates, diagnostics);
    }

    private static void ReportUncertainRelationship(
        IReadOnlyList<TopstepSourceRow> related,
        List<TopstepReconstructionDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        if (related.Count < 2)
        {
            return;
        }

        var references = new List<TopstepSourceReference>(related.Count);
        foreach (TopstepSourceRow row in related)
        {
            cancellationToken.ThrowIfCancellationRequested();
            references.Add(Reference(row));
        }

        diagnostics.Add(new(TopstepReconstructionDiagnosticSeverity.Warning,
            TopstepReconstructionDiagnosticCodes.PositionGroupingAmbiguous, references,
            "These same-contract row intervals overlap or touch at reported timestamp precision. Shared entry times, partial quantities, and direction changes do not prove shared fills, position grouping, or reversal order. Rows remain separate; position grouping requires additional broker identity/history evidence."));
    }

    private static TopstepSourceReference Reference(TopstepSourceRow row) =>
        new(row.SourceRecordIndex, row.SourceLineNumber);
}
