using System.Collections.Immutable;
using PersonalTradingJournal.Domain.Rules;

namespace PersonalTradingJournal.Domain.TradingPlans;

// These constructors validate saved values, never normalize them or resolve live sources.
internal static class TradingPlanStoredValue
{
    internal static string Text(string value, int limit = 4000, bool required = true)
    {
        TradingPlanContent.Text(value, nameof(value), limit);
        if (required && string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Missing snapshot text.");
        return value;
    }
    internal static DateTimeOffset Utc(DateTimeOffset value) => value.Offset == TimeSpan.Zero ? value :
        throw new ArgumentException("Stored UTC required.");
    internal static int Revision(int value) => value > 0 ? value : throw new ArgumentException("Invalid revision.");
    // Match the existing Account/Instrument contract (up to eight canonical characters), not
    // the narrower three-letter constraint of an optional automatic Rule criterion.
    internal static string Currency(string value) => Text(value, 8) == value.Trim().ToUpperInvariant()
        ? value : throw new ArgumentException("Invalid stored currency.");
}

public sealed partial class TradingPlanAccountSnapshot
{
    public TradingPlanAccountSnapshot(Guid? accountId, string name, string currency, DateTimeOffset updatedAtUtc)
    {
        if (accountId == Guid.Empty) throw new ArgumentException("Invalid Account identity.");
        AccountId = accountId; Name = TradingPlanStoredValue.Text(name, 128);
        Currency = TradingPlanStoredValue.Currency(currency); UpdatedAtUtc = TradingPlanStoredValue.Utc(updatedAtUtc);
    }
}
public sealed partial class TradingPlanInstrumentSnapshot
{
    public TradingPlanInstrumentSnapshot(Guid instrumentId, string symbol, string displayName, string currency,
        decimal tickSize, DateTimeOffset updatedAtUtc)
    {
        InstrumentId = TradingPlanContent.Id(instrumentId, nameof(instrumentId)); Symbol = TradingPlanStoredValue.Text(symbol, 32);
        DisplayName = TradingPlanStoredValue.Text(displayName, 128); Currency = TradingPlanStoredValue.Currency(currency);
        TickSize = tickSize > 0 ? tickSize : throw new ArgumentException("Invalid tick size.");
        UpdatedAtUtc = TradingPlanStoredValue.Utc(updatedAtUtc);
    }
}
public sealed partial class TradingPlanSetupSnapshot
{
    public TradingPlanSetupSnapshot(Guid setupId, string name, string? description, DateTimeOffset setupUpdatedAtUtc,
        int revision, DateTimeOffset recordedAtUtc, string marketContext, string entryConditions,
        string confirmations, string invalidation, string executionGuidance)
    {
        SetupId = TradingPlanContent.Id(setupId, nameof(setupId)); Name = TradingPlanStoredValue.Text(name, 128);
        Description = description is null ? null : TradingPlanStoredValue.Text(description, 2000, false);
        SetupUpdatedAtUtc = TradingPlanStoredValue.Utc(setupUpdatedAtUtc); Revision = TradingPlanStoredValue.Revision(revision);
        RecordedAtUtc = TradingPlanStoredValue.Utc(recordedAtUtc); MarketContext = TradingPlanStoredValue.Text(marketContext);
        EntryConditions = TradingPlanStoredValue.Text(entryConditions); Confirmations = TradingPlanStoredValue.Text(confirmations);
        Invalidation = TradingPlanStoredValue.Text(invalidation); ExecutionGuidance = TradingPlanStoredValue.Text(executionGuidance);
    }
}
public sealed partial class TradingPlanRuleSnapshot
{
    public TradingPlanRuleSnapshot(Guid ruleId, int revision, DateTimeOffset recordedAtUtc, TradingRuleDefinition definition)
    {
        RuleId = TradingPlanContent.Id(ruleId, nameof(ruleId)); Revision = TradingPlanStoredValue.Revision(revision);
        RecordedAtUtc = TradingPlanStoredValue.Utc(recordedAtUtc);
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        if (!definition.IsGlobal) throw new ArgumentException("Only global Rules can be captured.");
    }
}
public sealed partial class TradingPlanReferenceSnapshot
{
    public static TradingPlanReferenceSnapshot Rehydrate(TradingPlanAccountSnapshot account,
        IEnumerable<TradingPlanInstrumentSnapshot> instruments, IEnumerable<TradingPlanSetupSnapshot> setups,
        IEnumerable<TradingPlanRuleSnapshot> rules, DateTimeOffset capturedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(account);
        var i = Read(instruments, TradingPlanContent.MaximumInstruments).ToImmutableArray();
        var s = Read(setups, TradingPlanContent.MaximumScenarios).ToImmutableArray();
        var r = Read(rules, TradingPlanContent.MaximumRules).ToImmutableArray();
        ExactIds(i.Select(x => x.InstrumentId).Distinct(), i.Select(x => x.InstrumentId));
        ExactIds(s.Select(x => x.SetupId).Distinct(), s.Select(x => x.SetupId));
        ExactIds(r.Select(x => x.RuleId).Distinct(), r.Select(x => x.RuleId));
        CheckTime(capturedAtUtc, account.UpdatedAtUtc);
        foreach (var x in i) CheckTime(capturedAtUtc, x.UpdatedAtUtc);
        foreach (var x in s) { CheckTime(capturedAtUtc, x.SetupUpdatedAtUtc); CheckTime(capturedAtUtc, x.RecordedAtUtc); }
        foreach (var x in r) CheckTime(capturedAtUtc, x.RecordedAtUtc);
        return new(account, i, s, r, capturedAtUtc);
    }
}
