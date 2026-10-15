using System.Text;
using System.Text.Json;

namespace PersonalTradingJournal.Application.PeriodReviews.Coaching;

public enum PeriodFindingBasis { CalculatedFact, RecordedEvidence, UserObservation, Interpretation }
public sealed record PeriodCoachingFinding(PeriodFindingBasis Basis, string Text, IReadOnlyList<string> SourceIds);
public sealed record PeriodCoachingSection(bool InsufficientEvidence, IReadOnlyList<PeriodCoachingFinding> Findings);
public sealed record PeriodCoachingAction(string Action, string Measure, string Timeframe, IReadOnlyList<string> SourceIds);
public sealed record PeriodCoachingActionPlan(bool InsufficientEvidence, string Explanation,
    IReadOnlyList<string> SourceIds, IReadOnlyList<PeriodCoachingAction> Steps);
public sealed record PeriodCoachingResponse(string ContractVersion, string PacketId,
    PeriodCoachingSection Strengths, PeriodCoachingSection AreasForImprovement,
    PeriodCoachingSection MistakesObserved, PeriodCoachingSection ProgressVersusPreviousPeriod, PeriodCoachingActionPlan ActionPlan)
{
    public override string ToString() => $"PeriodCoachingResponse: {ContractVersion}";
}
public sealed record PeriodCoachingValidation(bool IsValid, PeriodCoachingResponse? Response, string? ErrorCode);

/// <summary>Structure and citation membership, not semantic truth, measured feasibility or prompt-injection immunity.</summary>
public static class PeriodCoachingResponseValidator
{
    public static PeriodCoachingValidation Validate(string json, PeriodCoachingPacket packet, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > PeriodCoachingContract.MaximumResponseBytes) return Invalid("response-size");
        try
        {
            using var doc = JsonDocument.Parse(json, new() { MaxDepth = 32 });
            if (Duplicates(doc.RootElement)) return Invalid("duplicate-properties");
            var r = JsonSerializer.Deserialize<PeriodCoachingResponse>(json, PeriodCoachingContract.JsonOptions);
            if (r is null || r.ContractVersion != packet.ContractVersion || r.PacketId != packet.PacketId) return Invalid("packet-identity");
            var sources = packet.SuppliedSources.ToDictionary(s => s.Id, StringComparer.Ordinal);
            bool Text(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= PeriodCoachingContract.MaximumTextLength;
            bool Refs(IReadOnlyList<string>? ids) => ids is { Count: >= 1 and <= 16 } && ids.All(id => id is not null && sources.ContainsKey(id)) &&
                ids.Distinct(StringComparer.Ordinal).Count() == ids.Count;
            foreach (var section in new[] { r.Strengths, r.AreasForImprovement, r.MistakesObserved, r.ProgressVersusPreviousPeriod })
            {
                token.ThrowIfCancellationRequested();
                if (section?.Findings is not { Count: >= 1 and <= PeriodCoachingContract.MaximumFindings } ||
                    section.InsufficientEvidence && section.Findings.Count != 1) return Invalid("section");
                foreach (var f in section.Findings)
                {
                    if (f is null || !Text(f.Text) || !Refs(f.SourceIds) || !Enum.IsDefined(f.Basis)) return Invalid("finding");
                    bool validBasis = f.SourceIds.All(id => f.Basis switch
                    {
                        PeriodFindingBasis.CalculatedFact => sources[id].Kind is PeriodCoachingSourceKind.CalculatedStatistics or PeriodCoachingSourceKind.CalculatedPatterns or PeriodCoachingSourceKind.Comparison,
                        PeriodFindingBasis.UserObservation => sources[id].Kind is PeriodCoachingSourceKind.Journal or PeriodCoachingSourceKind.JournalRevision or PeriodCoachingSourceKind.PlaybookAssociation or PeriodCoachingSourceKind.RuleRevision,
                        PeriodFindingBasis.RecordedEvidence => sources[id].Kind is PeriodCoachingSourceKind.Trade or PeriodCoachingSourceKind.Execution or PeriodCoachingSourceKind.Assessment or PeriodCoachingSourceKind.UnattributedAssessment or PeriodCoachingSourceKind.RuleRevision,
                        PeriodFindingBasis.Interpretation => true,
                        _ => false,
                    });
                    if (!validBasis) return Invalid("citation-basis");
                }
            }
            var plan = r.ActionPlan;
            if (plan is null || !Text(plan.Explanation) || !Refs(plan.SourceIds) || plan.Steps is null ||
                (plan.InsufficientEvidence ? plan.Steps.Count != 0 : plan.Steps.Count is < 1 or > 3)) return Invalid("action-plan");
            foreach (var step in plan.Steps)
            {
                token.ThrowIfCancellationRequested();
                if (step is null || !Text(step.Action) || !Text(step.Measure) || !Text(step.Timeframe) || !Refs(step.SourceIds)) return Invalid("action-step");
            }
            IReadOnlyList<string> FreezeRefs(IReadOnlyList<string> ids) => Array.AsReadOnly(ids.ToArray());
            PeriodCoachingSection Freeze(PeriodCoachingSection s) => s with
                { Findings = Array.AsReadOnly(s.Findings.Select(f => f with { SourceIds = FreezeRefs(f.SourceIds) }).ToArray()) };
            token.ThrowIfCancellationRequested();
            return new(true, r with { Strengths = Freeze(r.Strengths), AreasForImprovement = Freeze(r.AreasForImprovement),
                MistakesObserved = Freeze(r.MistakesObserved), ProgressVersusPreviousPeriod = Freeze(r.ProgressVersusPreviousPeriod),
                ActionPlan = plan with { SourceIds = FreezeRefs(plan.SourceIds), Steps = Array.AsReadOnly(plan.Steps.Select(s => s with { SourceIds = FreezeRefs(s.SourceIds) }).ToArray()) } }, null);
        }
        catch (JsonException) { return Invalid("structure"); }

        bool Duplicates(JsonElement element)
        {
            token.ThrowIfCancellationRequested();
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var p in element.EnumerateObject()) if (!names.Add(p.Name) || Duplicates(p.Value)) return true;
            }
            else if (element.ValueKind == JsonValueKind.Array)
                foreach (var item in element.EnumerateArray()) if (Duplicates(item)) return true;
            return false;
        }
    }
    private static PeriodCoachingValidation Invalid(string code) => new(false, null, code);
}
