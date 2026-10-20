using System.Collections.Immutable;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Domain.TradingPlans;

/// <summary>Bounded, immutable authoring values. Incompleteness is allowed in a Draft;
/// malformed values are not. Raw invalid numeric input remains the future editor's responsibility.</summary>
public sealed record TradingPlanContent
{
    public const int ContractVersion = 2;
    public const int MaximumTextLength = 4000;
    public const int MaximumFocusLength = 500;
    public const int MaximumInstruments = 12;
    public const int MaximumScenarios = 24;
    public const int MaximumRules = 50;
    public const int MaximumScreenshots = 24;

    public TradingPlanContent(TradingPlanSession? session = null, string focus = "",
        TradingPlanIntent intent = TradingPlanIntent.Opportunities,
        IEnumerable<TradingPlanInstrumentContext>? instruments = null,
        IEnumerable<TradingPlanScenario>? scenarios = null,
        string newsAndRestrictions = "", string standAside = "",
        TradingPlanRiskLimits? riskLimits = null,
        IEnumerable<TradingPlanRuleLink>? rules = null,
        TradingPlanChecklist confirmed = TradingPlanChecklist.None, string? planCurrency = null,
        IEnumerable<TradingPlanScreenshot>? screenshots = null)
    {
        if (session.HasValue && !Enum.IsDefined(session.Value)) throw new ArgumentOutOfRangeException(nameof(session));
        if (!Enum.IsDefined(intent)) throw new ArgumentOutOfRangeException(nameof(intent));
        TradingPlanChecklistPolicy.Validate(confirmed);
        Session = session;
        Focus = Text(focus, nameof(focus), MaximumFocusLength);
        Intent = intent;
        Instruments = Bounded(instruments, MaximumInstruments, nameof(instruments));
        Scenarios = Bounded(scenarios, MaximumScenarios, nameof(scenarios));
        Rules = Bounded(rules, MaximumRules, nameof(rules));
        Unique(Instruments.Select(i => i.InstrumentId), nameof(instruments));
        Unique(Scenarios.Select(s => s.Id), nameof(scenarios));
        Unique(Rules.Select(r => r.RuleId), nameof(rules));
        if (Scenarios.Any(s => !Instruments.Any(i => i.InstrumentId == s.InstrumentId)))
            throw new ArgumentException("Every scenario must belong to an included Instrument context.", nameof(scenarios));
        NewsAndRestrictions = Text(newsAndRestrictions, nameof(newsAndRestrictions));
        StandAside = Text(standAside, nameof(standAside));
        RiskLimits = riskLimits ?? new();
        Confirmed = confirmed;
        PlanCurrency = planCurrency is null ? null : TradingPlanStoredValue.Currency(planCurrency);
        Screenshots = Bounded(screenshots, MaximumScreenshots, nameof(screenshots));
        Unique(Screenshots.Select(s => s.Id), nameof(screenshots));
        if (Screenshots.Any(s => !Instruments.Any(i => i.InstrumentId == s.InstrumentId)))
            throw new ArgumentException("Every screenshot requires an included Instrument.", nameof(screenshots));
    }

    public TradingPlanSession? Session { get; }
    public string Focus { get; }
    public TradingPlanIntent Intent { get; }
    public ImmutableArray<TradingPlanInstrumentContext> Instruments { get; }
    public ImmutableArray<TradingPlanScenario> Scenarios { get; }
    public string NewsAndRestrictions { get; }
    public string StandAside { get; }
    public TradingPlanRiskLimits RiskLimits { get; }
    public ImmutableArray<TradingPlanRuleLink> Rules { get; }
    public TradingPlanChecklist Confirmed { get; }
    public string? PlanCurrency { get; }
    public ImmutableArray<TradingPlanScreenshot> Screenshots { get; }

    /// <summary>Reuse structural preparation, not dated observations or attestations.
    /// Linking/saving the target still requires fresh reference validation and a uniqueness check.</summary>
    public TradingPlanContent CopyForward() => new(Session, Focus, Intent,
        Instruments.Select(i => new TradingPlanInstrumentContext(i.InstrumentId)),
        Scenarios, riskLimits: RiskLimits, rules: Rules, planCurrency: PlanCurrency);

    /// <summary>Create-only policy: retain saved daily values, never assets or confirmations.
    /// The caller supplies the saved currency, never a current replacement currency.</summary>
    public TradingPlanContent PrefillPreviousDay(string? savedCurrency) => new(Session, Focus, Intent,
        Instruments, Scenarios.Select(s => new TradingPlanScenario(Guid.NewGuid(), s.InstrumentId, s.Name,
            s.Role, s.Direction, s.Condition, s.Response, s.Invalidation, s.Setup)),
        NewsAndRestrictions, StandAside, RiskLimits, Rules, planCurrency: savedCurrency ?? PlanCurrency);

    public TradingPlanContent WithPlanCurrency(string? currency) => new(Session, Focus, Intent,
        Instruments, Scenarios, NewsAndRestrictions, StandAside, RiskLimits, Rules, Confirmed, currency, Screenshots);

    internal TradingPlanContent WithConfirmations(TradingPlanChecklist confirmed) => new(Session, Focus, Intent,
        Instruments, Scenarios, NewsAndRestrictions, StandAside, RiskLimits, Rules, confirmed, PlanCurrency, Screenshots);

    // ImmutableArray record equality is backing-array equality. Authoring compares values instead,
    // so rebuilding equivalent input collections neither changes the version nor clears checks.
    /// <summary>Compares preparation values, excluding historical-only confirmation flags.</summary>
    public bool HasSamePreparation(TradingPlanContent other) => Session == other.Session && Focus == other.Focus &&
        Intent == other.Intent && Instruments.SequenceEqual(other.Instruments) && Scenarios.SequenceEqual(other.Scenarios) &&
        NewsAndRestrictions == other.NewsAndRestrictions && StandAside == other.StandAside &&
        RiskLimits == other.RiskLimits && Rules.SequenceEqual(other.Rules) && PlanCurrency == other.PlanCurrency && Screenshots.SequenceEqual(other.Screenshots);

    internal TradingPlanChecklist ConfirmationsAfterEdit(TradingPlanContent next)
    {
        var remaining = Confirmed;
        void Changed(bool changed, TradingPlanEditArea area)
        {
            if (changed) remaining = TradingPlanChecklistPolicy.AfterEdit(remaining, area);
        }
        Changed(Session != next.Session, TradingPlanEditArea.Session);
        Changed(Focus != next.Focus, TradingPlanEditArea.Focus);
        Changed(!Instruments.Select(i => i.InstrumentId).Order().SequenceEqual(
            next.Instruments.Select(i => i.InstrumentId).Order()), TradingPlanEditArea.Instruments);
        Changed(Instruments.Any(i => next.Instruments.Any(n => n.InstrumentId == i.InstrumentId && n != i)),
            TradingPlanEditArea.ContextAndLevels);
        Changed(!Scenarios.SequenceEqual(next.Scenarios), TradingPlanEditArea.ScenariosAndSetups);
        Changed(Intent != next.Intent || StandAside != next.StandAside, TradingPlanEditArea.StandAside);
        Changed(RiskLimits != next.RiskLimits || PlanCurrency != next.PlanCurrency || !Rules.SequenceEqual(next.Rules), TradingPlanEditArea.RiskLimitsAndRules);
        Changed(NewsAndRestrictions != next.NewsAndRestrictions, TradingPlanEditArea.News);
        Changed(!Screenshots.SequenceEqual(next.Screenshots), TradingPlanEditArea.ContextAndLevels);
        return remaining;
    }

    internal static string Text(string value, string name, int limit = MaximumTextLength)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (value.Length > limit) throw new ArgumentException($"Text must not exceed {limit} characters.", name);
        return value; // Preserve exact authored text, line endings and whitespace.
    }

    internal static Guid Id(Guid value, string name) => value != Guid.Empty ? value :
        throw new ArgumentException("A non-empty source identity is required.", name);

    private static ImmutableArray<T> Bounded<T>(IEnumerable<T>? values, int limit, string name)
    {
        var result = (values ?? []).Take(limit + 1).ToImmutableArray();
        if (result.Length > limit || result.Any(v => v is null))
            throw new ArgumentException($"Supply at most {limit} non-null values.", name);
        return result;
    }

    private static void Unique(IEnumerable<Guid> ids, string name)
    {
        var seen = new HashSet<Guid>();
        if (ids.Any(id => !seen.Add(id))) throw new ArgumentException("Duplicate source identity.", name);
    }
}

public sealed record TradingPlanLevels(decimal? PreviousDayHigh = null, decimal? PreviousDayLow = null,
    decimal? AsiaHigh = null, decimal? AsiaLow = null, decimal? LondonHigh = null, decimal? LondonLow = null);

public sealed record TradingPlanInstrumentContext
{
    public TradingPlanInstrumentContext(Guid instrumentId, TradingPlanBias? bias = null,
        string marketThesis = "", TradingPlanLevels? levels = null)
    {
        InstrumentId = TradingPlanContent.Id(instrumentId, nameof(instrumentId));
        if (bias.HasValue && !Enum.IsDefined(bias.Value)) throw new ArgumentOutOfRangeException(nameof(bias));
        Bias = bias;
        MarketThesis = TradingPlanContent.Text(marketThesis, nameof(marketThesis));
        Levels = levels ?? new();
    }

    public Guid InstrumentId { get; }
    public TradingPlanBias? Bias { get; }
    public string MarketThesis { get; }
    public TradingPlanLevels Levels { get; }
}

/// <summary>A selection token, not a frozen historical definition or evidence of adherence.</summary>
public sealed record TradingPlanSetupLink
{
    public TradingPlanSetupLink(Guid setupId, int expectedPlaybookRevision, DateTimeOffset expectedUpdatedAtUtc)
    {
        SetupId = TradingPlanContent.Id(setupId, nameof(setupId));
        if (expectedPlaybookRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedPlaybookRevision));
        if (expectedUpdatedAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("UTC required.", nameof(expectedUpdatedAtUtc));
        ExpectedPlaybookRevision = expectedPlaybookRevision;
        ExpectedUpdatedAtUtc = expectedUpdatedAtUtc;
    }
    public Guid SetupId { get; }
    public int ExpectedPlaybookRevision { get; }
    public DateTimeOffset ExpectedUpdatedAtUtc { get; }
}

public sealed record TradingPlanRuleLink
{
    public TradingPlanRuleLink(Guid ruleId, int expectedRevision)
    {
        RuleId = TradingPlanContent.Id(ruleId, nameof(ruleId));
        if (expectedRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedRevision));
        ExpectedRevision = expectedRevision;
    }
    public Guid RuleId { get; }
    public int ExpectedRevision { get; }
}

public sealed record TradingPlanScenario
{
    public TradingPlanScenario(Guid id, Guid instrumentId, string name = "",
        TradingPlanScenarioRole role = TradingPlanScenarioRole.Primary, TradeDirection? direction = null,
        string condition = "", string response = "", string invalidation = "", TradingPlanSetupLink? setup = null)
    {
        Id = TradingPlanContent.Id(id, nameof(id));
        InstrumentId = TradingPlanContent.Id(instrumentId, nameof(instrumentId));
        Name = TradingPlanContent.Text(name, nameof(name), 120);
        if (!Enum.IsDefined(role)) throw new ArgumentOutOfRangeException(nameof(role));
        if (direction.HasValue && !Enum.IsDefined(direction.Value)) throw new ArgumentOutOfRangeException(nameof(direction));
        Role = role;
        Direction = direction;
        Condition = TradingPlanContent.Text(condition, nameof(condition));
        Response = TradingPlanContent.Text(response, nameof(response));
        Invalidation = TradingPlanContent.Text(invalidation, nameof(invalidation));
        Setup = setup;
    }
    public Guid Id { get; }
    public Guid InstrumentId { get; }
    public string Name { get; }
    public TradingPlanScenarioRole Role { get; }
    public TradeDirection? Direction { get; }
    public string Condition { get; }
    public string Response { get; }
    public string Invalidation { get; }
    public TradingPlanSetupLink? Setup { get; }
}

/// <summary>Optional planned amounts in the selected Account's currency. Null is unset;
/// zero explicitly permits no monetary risk/Trades. No enforcement or P&amp;L calculation.</summary>
public sealed record TradingPlanRiskLimits
{
    public TradingPlanRiskLimits(decimal? riskPerTrade = null, decimal? dailyLossLimit = null,
        int? maximumTrades = null, int? stopAfterConsecutiveLosses = null)
    {
        if (riskPerTrade < 0) throw new ArgumentOutOfRangeException(nameof(riskPerTrade));
        if (dailyLossLimit < 0) throw new ArgumentOutOfRangeException(nameof(dailyLossLimit));
        if (maximumTrades < 0) throw new ArgumentOutOfRangeException(nameof(maximumTrades));
        if (stopAfterConsecutiveLosses <= 0) throw new ArgumentOutOfRangeException(nameof(stopAfterConsecutiveLosses));
        RiskPerTrade = riskPerTrade;
        DailyLossLimit = dailyLossLimit;
        MaximumTrades = maximumTrades;
        StopAfterConsecutiveLosses = stopAfterConsecutiveLosses;
    }
    public decimal? RiskPerTrade { get; }
    public decimal? DailyLossLimit { get; }
    public int? MaximumTrades { get; }
    public int? StopAfterConsecutiveLosses { get; }
}
