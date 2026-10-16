using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.DailyReview;

namespace PersonalTradingJournal.Application.PeriodReviews;

/// <summary>Read-only comparisons of adjacent complete NY periods, not historical as-of reconstructions.
/// Reuses strict Daily/Period economics. No FX, sessions guessed from clock time, or adherence inference.</summary>
public static class PeriodReviewComparisonCalculator
{
    public static PeriodComparisonResult Calculate(PeriodComparisonEvidence evidence,
        CancellationToken cancellationToken = default, PeriodComparisonLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        limits ??= new();
        cancellationToken.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(evidence.Consistency) || evidence.Previous.Query != PeriodComparisonEvidence.PreviousQuery(evidence.Current.Query))
            throw new ArgumentException("Supply adjacent equivalent periods with the same exact Account scope.", nameof(evidence));
        bool consistent = evidence.Consistency == PeriodSourceConsistency.SingleReadTransaction;
        var previous = PeriodReviewStatisticsCalculator.Calculate(evidence.Previous, cancellationToken);
        var current = PeriodReviewStatisticsCalculator.Calculate(evidence.Current, cancellationToken);
        var pp = PeriodReviewPatternsCalculator.Calculate(evidence.Previous, cancellationToken, limits.MaxRuleTargetsPerPeriod);
        var cp = PeriodReviewPatternsCalculator.Calculate(evidence.Current, cancellationToken, limits.MaxRuleTargetsPerPeriod);
        if (pp.Patterns is null || cp.Patterns is null) return Limited(pp.LimitCode ?? cp.LimitCode!);

        var memberships = new Dictionary<PeriodBreakdownKey, (List<DailyReviewTradeEvidence> Previous, List<DailyReviewTradeEvidence> Current)>();
        int membershipCount = 0;
        foreach (bool isCurrent in new[] { false, true })
        {
            var source = isCurrent ? evidence.Current : evidence.Previous;
            foreach (var trade in source.ClosedTrades.Concat(source.ActivityContext).OrderBy(t => t.TradeId))
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var key in Keys(trade))
                {
                    if (++membershipCount > limits.MaxMemberships) return Limited("breakdown-memberships");
                    if (!memberships.TryGetValue(key, out var bucket))
                    {
                        if (memberships.Count >= limits.MaxBreakdownGroups) return Limited("breakdown-groups");
                        bucket = ([], []); memberships.Add(key, bucket);
                    }
                    (isCurrent ? bucket.Current : bucket.Previous).Add(trade);
                }
            }
        }
        var breakdowns = new List<PeriodBreakdownComparison>();
        var groups = Groups(previous, current);
        int outputGroups = groups.Count;
        foreach (var (key, rows) in memberships.OrderBy(k => k.Key.Kind).ThenBy(k => k.Key.Context)
            .ThenBy(k => k.Key.SourceId).ThenBy(k => k.Key.Revision))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var p = Subset(evidence.Previous, rows.Previous); var c = Subset(evidence.Current, rows.Current);
            var subgroup = Groups(p, c);
            outputGroups = checked(outputGroups + subgroup.Count);
            if (outputGroups > limits.MaxMemberships) return Limited("comparison-groups");
            breakdowns.Add(new(key, Ids(rows.Previous), Ids(rows.Current), subgroup));
        }
        var rules = RuleComparisons(pp.Patterns, cp.Patterns, consistent, cancellationToken);
        static IEnumerable<(Guid, string)> Accounts(PeriodReviewEvidence e) => e.ClosedTrades.Concat(e.ActivityContext)
            .Select(t => (t.Account.Id, t.PricingCurrency)).Distinct().OrderBy(x => x.Id).ThenBy(x => x.PricingCurrency, StringComparer.Ordinal);
        bool sameAccounts = Accounts(evidence.Previous).SequenceEqual(Accounts(evidence.Current));
        cancellationToken.ThrowIfCancellationRequested();
        return new(PeriodEvidenceStatus.Ready, new(evidence, previous, current, pp.Patterns, cp.Patterns,
            Count(previous.Population.Closed.Count, current.Population.Closed.Count, sameAccounts),
            Count(previous.ActiveDays.Count, current.ActiveDays.Count, sameAccounts), groups, breakdowns.AsReadOnly(), rules), null);

        PeriodReviewStatistics Subset(PeriodReviewEvidence source, List<DailyReviewTradeEvidence> rows) =>
            PeriodReviewStatisticsCalculator.Calculate(source with
            {
                ClosedTrades = rows.Where(t => t.Inclusion == DailyReviewTradeInclusion.ClosedOnDate).ToArray(),
                ActivityContext = rows.Where(t => t.Inclusion != DailyReviewTradeInclusion.ClosedOnDate).ToArray(),
            }, cancellationToken);

        IReadOnlyList<PeriodGroupComparison> Groups(PeriodReviewStatistics p, PeriodReviewStatistics c)
        {
            var results = new List<PeriodGroupComparison>();
            var previousCurrencies = p.Currencies.ToDictionary(x => x.Currency, StringComparer.Ordinal);
            var currentCurrencies = c.Currencies.ToDictionary(x => x.Currency, StringComparer.Ordinal);
            foreach (var currency in previousCurrencies.Keys.Union(currentCurrencies.Keys).Order(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                previousCurrencies.TryGetValue(currency, out var before); currentCurrencies.TryGetValue(currency, out var after);
                var previousAccounts = before?.Accounts.ToDictionary(a => a.Account.Id) ?? [];
                var currentAccounts = after?.Accounts.ToDictionary(a => a.Account.Id) ?? [];
                Guid[] beforeIds = previousAccounts.Keys.Order().ToArray();
                Guid[] afterIds = currentAccounts.Keys.Order().ToArray();
                Add(null, before?.Statistics, after?.Statistics, beforeIds, afterIds);
                foreach (var account in beforeIds.Union(afterIds).Order())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    previousAccounts.TryGetValue(account, out var b); currentAccounts.TryGetValue(account, out var a);
                    Add(account, b?.Statistics, a?.Statistics, b is not null ? [account] : [], a is not null ? [account] : []);
                }

                void Add(Guid? account, PeriodGroupStatistics? b, PeriodGroupStatistics? a, Guid[] bIds, Guid[] aIds)
                {
                    bool matchingAccounts = bIds.SequenceEqual(aIds);
                    PeriodValueChange Counts(Func<PeriodGroupStatistics, int> select) =>
                        Count(b is null ? 0 : select(b), a is null ? 0 : select(a), matchingAccounts);
                    PeriodValueChange Money(bool net)
                    {
                        var bm = (net ? b?.Net : b?.Gross)?.Outcomes.Metrics;
                        var am = (net ? a?.Net : a?.Gross)?.Outcomes.Metrics;
                        var status = b is null || a is null ? PeriodChangeStatus.MissingPopulation
                            : bm!.Coverage.Status != MetricCoverageStatus.Complete || am!.Coverage.Status != MetricCoverageStatus.Complete
                                ? PeriodChangeStatus.IncompleteCoverage : PeriodChangeStatus.Comparable;
                        return Change(bm?.Total, am?.Total, status, matchingAccounts);
                    }
                    results.Add(new(currency, account, b, a, Array.AsReadOnly(bIds), Array.AsReadOnly(aIds),
                        Counts(g => g.Population.Closed.Count), Counts(g => g.ActiveDays.Count),
                        Counts(g => g.Population.ExcludedOpenActivity.Count), Counts(g => g.Population.ExcludedUnavailableLifecycle.Count),
                        Counts(g => g.Costs.MissingCommissionTrades.Count), Counts(g => g.Costs.MissingFeeTrades.Count),
                        Counts(g => g.Costs.MissingCostTrades.Count), Money(false), Money(true)));
                }
            }
            return results.AsReadOnly();
        }

        PeriodValueChange Count(int p, int c, bool accountsMatch = true) => Change(p, c, PeriodChangeStatus.Comparable, accountsMatch);
        PeriodValueChange Change(decimal? p, decimal? c, PeriodChangeStatus status, bool accountsMatch)
        {
            if (!consistent) status = PeriodChangeStatus.IndependentSnapshots;
            if (status != PeriodChangeStatus.Comparable) return new(p, c, null, null, status, PeriodPercentStatus.ChangeUnavailable);
            decimal delta = checked(c!.Value - p!.Value);
            var percentStatus = !accountsMatch ? PeriodPercentStatus.AccountOrCurrencyPopulationChanged
                : p == 0 ? PeriodPercentStatus.ZeroBaseline : p < 0 ? PeriodPercentStatus.NegativeBaseline : PeriodPercentStatus.Defined;
            return new(p, c, delta, percentStatus == PeriodPercentStatus.Defined ? checked(delta / p.Value * 100m) : null, status, percentStatus);
        }
    }

    private static PeriodComparisonResult Limited(string code) => new(PeriodEvidenceStatus.LimitExceeded, null, code);
    private static DailyReviewTradeSet Ids(IEnumerable<DailyReviewTradeEvidence> rows) => new(Array.AsReadOnly(rows.Select(t => t.TradeId).Order().ToArray()));

    private static IEnumerable<PeriodBreakdownKey> Keys(DailyReviewTradeEvidence trade)
    {
        yield return new(PeriodBreakdownKind.Instrument, PeriodBreakdownContext.CatalogIdentity, trade.Instrument.Id);
        var captured = trade.Playbook?.Associations.SingleOrDefault(a => a.State == ReviewPlaybookAssociationState.Current)?.Guidance;
        yield return captured is not null
            ? new(PeriodBreakdownKind.Setup, PeriodBreakdownContext.FrozenRevision, captured.SetupId, captured.Revision)
            : trade.Setup is not null
                ? new(PeriodBreakdownKind.Setup, PeriodBreakdownContext.LegacyClassification, trade.Setup.Id)
                : new(PeriodBreakdownKind.Setup, PeriodBreakdownContext.NoAssociation);
        // NY civil closure dates exist; a defensible persisted trading-session definition does not.
        yield return new(PeriodBreakdownKind.Session, PeriodBreakdownContext.UnknownSession);
        if (trade.AssignedMistakes.Count == 0)
            yield return new(PeriodBreakdownKind.RecordedMistake, PeriodBreakdownContext.NoRecordedMistake);
        foreach (var mistake in trade.AssignedMistakes.Select(m => m.Mistake.Id).Distinct().Order())
            yield return new(PeriodBreakdownKind.RecordedMistake, PeriodBreakdownContext.CatalogIdentity, mistake);
    }

    private static IReadOnlyList<PeriodRuleComparison> RuleComparisons(PeriodReviewPatterns p, PeriodReviewPatterns c,
        bool consistent, CancellationToken token)
    {
        static object Key(PeriodRuleSummary r) => (r.Rule.RuleId, r.Rule.Revision, r.VersionState, r.TargetKind, r.Scope, r.Denominator);
        var previous = p.Rules.ToDictionary(Key); var current = c.Rules.ToDictionary(Key);
        var rows = p.Rules.Concat(c.Rules).DistinctBy(Key).OrderBy(r => r.Rule.RuleId).ThenBy(r => r.Rule.Revision)
            .ThenBy(r => r.VersionState).ThenBy(r => r.TargetKind).ThenBy(r => r.Scope.AccountId).ThenBy(r => r.Denominator);
        var results = new List<PeriodRuleComparison>();
        foreach (var row in rows)
        {
            token.ThrowIfCancellationRequested();
            previous.TryGetValue(Key(row), out var b); current.TryGetValue(Key(row), out var a);
            var status = !consistent ? PeriodRuleComparisonStatus.IndependentSnapshots
                : b is null || a is null ? PeriodRuleComparisonStatus.MissingCohort
                : b.TargetCount != a.TargetCount ? PeriodRuleComparisonStatus.DifferentTargetCount : PeriodRuleComparisonStatus.ComparableCounts;
            int? Delta(Func<PeriodRuleSummary, int> select) => status == PeriodRuleComparisonStatus.ComparableCounts ? select(a!) - select(b!) : null;
            results.Add(new(b, a, status, Delta(r => r.Assessed), Delta(r => r.Compliant), Delta(r => r.ConfirmedViolation),
                Delta(r => r.CannotEvaluate), Delta(r => r.NotAssessed)));
        }
        return results.AsReadOnly();
    }
}
