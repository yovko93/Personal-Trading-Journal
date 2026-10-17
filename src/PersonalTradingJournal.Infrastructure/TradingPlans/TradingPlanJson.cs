using System.Text.Json;
using System.Text.Json.Serialization;
using PersonalTradingJournal.Domain.Rules;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Infrastructure.TradingPlans;

/// <summary>Versioned storage mapping of the existing values, not another authoring model.</summary>
internal static class TradingPlanJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        MaxDepth = 24, RespectRequiredConstructorParameters = true,
        AllowDuplicateProperties = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
    private sealed record Content(TradingPlanSession? Session, string Focus, TradingPlanIntent Intent,
        TradingPlanInstrumentContext[] Instruments, TradingPlanScenario[] Scenarios, string NewsAndRestrictions,
        string StandAside, TradingPlanRiskLimits RiskLimits, TradingPlanRuleLink[] Rules, TradingPlanChecklist Confirmed)
    {
        internal static Content From(TradingPlanContent c) => new(c.Session, c.Focus, c.Intent, c.Instruments.ToArray(),
            c.Scenarios.ToArray(), c.NewsAndRestrictions, c.StandAside, c.RiskLimits, c.Rules.ToArray(), c.Confirmed);
        internal TradingPlanContent Read()
        {
            if (Instruments is null || Scenarios is null || Rules is null || RiskLimits is null)
                throw new InvalidDataException("Incomplete Plan content.");
            return new(Session, Focus, Intent, Instruments, Scenarios, NewsAndRestrictions, StandAside, RiskLimits, Rules, Confirmed);
        }
    }
    private sealed record Draft(int Version, Content Content, TradingPlanRevisionId? DerivedFromRevision,
        DateTimeOffset StartedAtUtc, DateTimeOffset UpdatedAtUtc)
    {
        internal static Draft From(TradingPlanDraft d) => new(1, Content.From(d.Content), d.DerivedFromRevision, d.StartedAtUtc, d.UpdatedAtUtc);
        internal TradingPlanDraft Read() => Version == 1 ? TradingPlanDraft.Rehydrate(Content.Read(), DerivedFromRevision, StartedAtUtc, UpdatedAtUtc)
            : throw new InvalidDataException("Unsupported Plan content version.");
    }
    private sealed record Rule(Guid RuleId, int Revision, DateTimeOffset RecordedAtUtc, string Name, string Criterion,
        string RequiredEvidence, AutomaticRuleCriterion? AutomaticCriterion)
    {
        internal TradingPlanRuleSnapshot Read()
        {
            var d = new TradingRuleDefinition(Name, Criterion, RequiredEvidence, AutomaticCriterion);
            if (d.Name != Name || d.Criterion != Criterion || d.RequiredEvidence != RequiredEvidence)
                throw new InvalidDataException("Noncanonical saved Rule definition.");
            return new(RuleId, Revision, RecordedAtUtc, d);
        }
    }
    private sealed record References(TradingPlanAccountSnapshot Account, TradingPlanInstrumentSnapshot[] Instruments,
        TradingPlanSetupSnapshot[] Setups, Rule[] Rules, DateTimeOffset CapturedAtUtc)
    {
        internal static References From(TradingPlanReferenceSnapshot r) => new(r.Account, r.Instruments.ToArray(), r.Setups.ToArray(),
            r.Rules.Select(x => new Rule(x.RuleId, x.Revision, x.RecordedAtUtc, x.Definition.Name, x.Definition.Criterion,
                x.Definition.RequiredEvidence, x.Definition.AutomaticCriterion)).ToArray(), r.CapturedAtUtc);
        internal TradingPlanReferenceSnapshot Read() => TradingPlanReferenceSnapshot.Rehydrate(Account, Instruments, Setups,
            Rules.Select(x => x.Read()), CapturedAtUtc);
    }
    private sealed record Ready(int Version, TradingPlanRevisionId Id, TradingPlanKey Key, Draft Draft, References References,
        DateTimeOffset PlanCreatedAtUtc, DateTimeOffset ReadyAtUtc, long SourceDraftVersion)
    {
        internal TradingPlanReadyRevision Read() => Version == 1 ? TradingPlanReadyRevision.Rehydrate(Id, Key, Draft.Read(), References.Read(),
            PlanCreatedAtUtc, ReadyAtUtc, SourceDraftVersion) : throw new InvalidDataException("Unsupported Ready version.");
    }
    internal static string WriteDraft(TradingPlanDraft d) => Write(Draft.From(d));
    internal static TradingPlanDraft ReadDraft(string s) => Read(s, () =>
    {
        var draft = Deserialize<Draft>(s).Read(); RequireExactValues(s, WriteDraft(draft)); return draft;
    });
    internal static string WriteReady(TradingPlanReadyRevision r) => Write(new Ready(1, r.Id, r.Key,
        Draft.From(TradingPlanDraft.Rehydrate(r.Content, r.DerivedFromRevision, r.DraftStartedAtUtc, r.DraftUpdatedAtUtc)),
        References.From(r.References), r.PlanCreatedAtUtc, r.ReadyAtUtc, r.SourceDraftVersion));
    internal static TradingPlanReadyRevision ReadReady(string s) => Read(s, () =>
    {
        var ready = Deserialize<Ready>(s).Read(); RequireExactValues(s, WriteReady(ready)); return ready;
    });
    private static void RequireExactValues(string stored, string reconstructed)
    {
        using var a = JsonDocument.Parse(stored); using var b = JsonDocument.Parse(reconstructed);
        if (!JsonElement.DeepEquals(a.RootElement, b.RootElement))
            throw new InvalidDataException("Stored Plan values cannot be defaulted or normalized during reconstruction.");
    }
    internal static string WriteContent(TradingPlanContent c) => Write(Content.From(c));
    internal static string Write<T>(T value)
    {
        var s = JsonSerializer.Serialize(value, Options);
        if (s.Length > 2_000_000) throw new InvalidDataException("Plan storage payload exceeds its limit.");
        return s;
    }
    internal static T Deserialize<T>(string s) => Read(s, () => JsonSerializer.Deserialize<T>(s, Options)
        ?? throw new InvalidDataException("Missing Plan payload."));
    private static T Read<T>(string s, Func<T> read)
    {
        if (s.Length > 2_000_000) throw new InvalidDataException("Plan storage payload exceeds its limit.");
        try { return read(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or JsonException or NullReferenceException)
        { throw new InvalidDataException("Invalid saved Trading Plan evidence."); }
    }
}
