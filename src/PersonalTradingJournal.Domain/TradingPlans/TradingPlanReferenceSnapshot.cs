using System.Collections.Immutable;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Rules;
using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Domain.TradingPlans;

/// <summary>Transient inputs supplied by the caller; no Domain database/catalog lookup occurs.</summary>
public sealed record TradingPlanPlaybookSource(TradingSetup Setup, TradingSetupPlaybookRevision Revision);

public sealed class TradingPlanAccountSnapshot
{
    internal TradingPlanAccountSnapshot(TradingAccount account)
    {
        AccountId = account.Id; Name = account.Name; Currency = account.Currency; UpdatedAtUtc = account.UpdatedAtUtc;
    }
    public Guid AccountId { get; }
    public string Name { get; }
    public string Currency { get; }
    public DateTimeOffset UpdatedAtUtc { get; }
}

public sealed class TradingPlanInstrumentSnapshot
{
    internal TradingPlanInstrumentSnapshot(Instrument instrument)
    {
        InstrumentId = instrument.Id; Symbol = instrument.Symbol; DisplayName = instrument.DisplayName;
        Currency = instrument.Currency; TickSize = instrument.TickSize; UpdatedAtUtc = instrument.UpdatedAtUtc;
    }
    public Guid InstrumentId { get; }
    public string Symbol { get; }
    public string DisplayName { get; }
    public string Currency { get; }
    public decimal TickSize { get; }
    public DateTimeOffset UpdatedAtUtc { get; }
}

/// <summary>Selected Setup label and five guidance sections at one exact Playbook revision.
/// Examples, images, paths and unrelated catalog content are deliberately excluded.</summary>
public sealed class TradingPlanSetupSnapshot
{
    internal TradingPlanSetupSnapshot(TradingPlanPlaybookSource source)
    {
        SetupId = source.Setup.Id; Name = source.Setup.Name; Description = source.Setup.Description;
        SetupUpdatedAtUtc = source.Setup.UpdatedAtUtc;
        Revision = source.Revision.Revision; RecordedAtUtc = source.Revision.RecordedAtUtc;
        var guidance = source.Revision.Guidance;
        MarketContext = guidance.MarketContext; EntryConditions = guidance.EntryConditions;
        Confirmations = guidance.Confirmations; Invalidation = guidance.Invalidation; ExecutionGuidance = guidance.ExecutionGuidance;
    }
    public Guid SetupId { get; }
    public string Name { get; }
    public string? Description { get; }
    public DateTimeOffset SetupUpdatedAtUtc { get; }
    public int Revision { get; }
    public DateTimeOffset RecordedAtUtc { get; }
    public string MarketContext { get; }
    public string EntryConditions { get; }
    public string Confirmations { get; }
    public string Invalidation { get; }
    public string ExecutionGuidance { get; }
}

public sealed class TradingPlanRuleSnapshot
{
    internal TradingPlanRuleSnapshot(TradingRuleRevision rule)
    {
        RuleId = rule.RuleId; Revision = rule.Revision; RecordedAtUtc = rule.RecordedAtUtc;
        // Both the definition and its optional typed criterion are sealed immutable values.
        Definition = rule.Definition;
    }
    public Guid RuleId { get; }
    public int Revision { get; }
    public DateTimeOffset RecordedAtUtc { get; }
    public TradingRuleDefinition Definition { get; }
}

/// <summary>Only the references actually used by one preparation. Capture validates supplied
/// source states, not their freshness in storage. M21.3 must recheck them under its transaction.</summary>
public sealed class TradingPlanReferenceSnapshot
{
    private TradingPlanReferenceSnapshot(TradingPlanAccountSnapshot account,
        ImmutableArray<TradingPlanInstrumentSnapshot> instruments, ImmutableArray<TradingPlanSetupSnapshot> setups,
        ImmutableArray<TradingPlanRuleSnapshot> rules, DateTimeOffset capturedAtUtc)
    {
        Account = account; Instruments = instruments; Setups = setups; Rules = rules; CapturedAtUtc = capturedAtUtc;
    }

    public const int ContractVersion = 1;
    public TradingPlanAccountSnapshot Account { get; }
    public ImmutableArray<TradingPlanInstrumentSnapshot> Instruments { get; }
    public ImmutableArray<TradingPlanSetupSnapshot> Setups { get; }
    public ImmutableArray<TradingPlanRuleSnapshot> Rules { get; }
    public DateTimeOffset CapturedAtUtc { get; }

    public static TradingPlanReferenceSnapshot Capture(TradingPlanKey key, TradingPlanContent content,
        TradingAccount account, IEnumerable<Instrument> instruments,
        IEnumerable<TradingPlanPlaybookSource> setups, IEnumerable<TradingRuleRevision> rules,
        DateTimeOffset capturedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(account);
        CheckTime(capturedAtUtc, account.UpdatedAtUtc);
        if (account.Id != key.AccountId || !account.IsActive)
            throw new InvalidOperationException("The exact active Account is required to finalize preparation.");
        var instrumentSources = Read(instruments, TradingPlanContent.MaximumInstruments);
        var setupSources = Read(setups, TradingPlanContent.MaximumScenarios);
        var ruleSources = Read(rules, TradingPlanContent.MaximumRules);
        ExactIds(content.Instruments.Select(i => i.InstrumentId), instrumentSources.Select(i => i.Id));
        ExactIds(content.Scenarios.Where(s => s.Setup is not null).Select(s => s.Setup!.SetupId).Distinct(),
            setupSources.Select(s => s.Setup?.Id ?? Guid.Empty));
        ExactIds(content.Rules.Select(r => r.RuleId), ruleSources.Select(r => r.RuleId));

        foreach (var instrument in instrumentSources)
        {
            CheckTime(capturedAtUtc, instrument.UpdatedAtUtc);
            if (!instrument.IsActive) throw new InvalidOperationException("An Instrument is inactive.");
        }
        foreach (var source in setupSources)
        {
            ArgumentNullException.ThrowIfNull(source.Setup);
            ArgumentNullException.ThrowIfNull(source.Revision);
            CheckTime(capturedAtUtc, source.Setup.UpdatedAtUtc);
            CheckTime(capturedAtUtc, source.Revision.RecordedAtUtc);
            if (!source.Setup.IsActive || source.Revision.TradingSetupId != source.Setup.Id ||
                content.Scenarios.Where(s => s.Setup?.SetupId == source.Setup.Id).Any(s =>
                    s.Setup!.ExpectedPlaybookRevision != source.Revision.Revision ||
                    s.Setup.ExpectedUpdatedAtUtc != source.Setup.UpdatedAtUtc))
                throw new InvalidOperationException("A selected Playbook changed or is unavailable. Reload its selection.");
        }
        foreach (var rule in ruleSources)
        {
            CheckTime(capturedAtUtc, rule.RecordedAtUtc);
            if (!rule.IsActive || !rule.Definition.IsGlobal ||
                content.Rules.Single(r => r.RuleId == rule.RuleId).ExpectedRevision != rule.Revision)
                throw new InvalidOperationException("A selected global Rule changed or is unavailable. Reload its selection.");
        }
        // Output ordering follows the preparation, independent of catalog query order.
        return new(new(account), content.Instruments.Select(i => new TradingPlanInstrumentSnapshot(
                instrumentSources.Single(s => s.Id == i.InstrumentId))).ToImmutableArray(),
            content.Scenarios.Where(s => s.Setup is not null).Select(s => s.Setup!.SetupId).Distinct()
                .Select(id => new TradingPlanSetupSnapshot(setupSources.Single(s => s.Setup.Id == id))).ToImmutableArray(),
            content.Rules.Select(r => new TradingPlanRuleSnapshot(ruleSources.Single(s => s.RuleId == r.RuleId))).ToImmutableArray(),
            capturedAtUtc);
    }

    internal void ValidateFor(TradingPlanKey key, TradingPlanContent content, DateTimeOffset readyAtUtc)
    {
        if (key.AccountId != Account.AccountId || CapturedAtUtc != readyAtUtc)
            throw new InvalidOperationException("Capture references for this exact Account and Ready operation.");
        ExactIds(content.Instruments.Select(i => i.InstrumentId), Instruments.Select(i => i.InstrumentId));
        ExactIds(content.Scenarios.Where(s => s.Setup is not null).Select(s => s.Setup!.SetupId).Distinct(), Setups.Select(s => s.SetupId));
        ExactIds(content.Rules.Select(r => r.RuleId), Rules.Select(r => r.RuleId));
        if (content.Scenarios.Where(s => s.Setup is not null).Any(s => !Setups.Any(p =>
                p.SetupId == s.Setup!.SetupId && p.Revision == s.Setup.ExpectedPlaybookRevision && p.SetupUpdatedAtUtc == s.Setup.ExpectedUpdatedAtUtc)) ||
            content.Rules.Any(r => !Rules.Any(p => p.RuleId == r.RuleId && p.Revision == r.ExpectedRevision)))
            throw new InvalidOperationException("The captured references do not match the working Draft.");
    }

    private static T[] Read<T>(IEnumerable<T> input, int limit)
    {
        ArgumentNullException.ThrowIfNull(input);
        var items = input.Take(limit + 1).ToArray();
        if (items.Length > limit || items.Any(i => i is null)) throw new ArgumentException("Invalid or excessive reference input.", nameof(input));
        return items;
    }

    private static void ExactIds(IEnumerable<Guid> expected, IEnumerable<Guid> supplied)
    {
        if (!expected.Order().SequenceEqual(supplied.Order()))
            throw new InvalidOperationException("Supply every selected reference exactly once, with no unrelated sources.");
    }

    private static void CheckTime(DateTimeOffset capturedAtUtc, DateTimeOffset sourceAtUtc)
    {
        if (capturedAtUtc.Offset != TimeSpan.Zero || capturedAtUtc < sourceAtUtc)
            throw new ArgumentException("Reference capture requires UTC and cannot predate its sources.", nameof(capturedAtUtc));
    }
}
