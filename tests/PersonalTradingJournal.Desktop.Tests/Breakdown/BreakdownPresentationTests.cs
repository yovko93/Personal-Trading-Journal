using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.Analytics.Breakdown;
using PersonalTradingJournal.Desktop.ViewModels.Breakdown;

namespace PersonalTradingJournal.Desktop.Tests.Breakdown;

public sealed class BreakdownPresentationTests
{
    [Fact]
    public void SummaryUsesSuppliedResultNotDisplayedRowsAndNeverReadsTradeIds()
    {
        var metrics = Metrics(25, 25, 123.45m);
        var packet = new BreakdownAnalyticsSnapshot(new("USD"), metrics with { TradeIds = new ForbiddenIds() }, [Group(1, 10m)]);
        var p = new BreakdownPresentation(packet);
        Assert.Same(packet, p.Source); Assert.Same(packet.Summary, p.Summary.Source);
        Assert.Equal(25, p.Summary.Trades); Assert.Contains("123", p.Summary.Pnl);
        Assert.Single(p.Rows); Assert.Same(packet.Groups[0], p.Rows[0].Source);
    }

    [Theory]
    [InlineData(2, 0, 0, 0, BreakdownCalculationMode.NetPreferred, "Net")]
    [InlineData(1, 1, 0, 0, BreakdownCalculationMode.NetPreferred, "Net/Gross")]
    [InlineData(0, 2, 0, 0, BreakdownCalculationMode.NetPreferred, "Gross fallback")]
    [InlineData(0, 0, 2, 0, BreakdownCalculationMode.Gross, "Gross")]
    [InlineData(0, 0, 0, 2, BreakdownCalculationMode.NetPreferred, "Unavailable")]
    public void EachGroupUsesItsOwnSourceCounts(int net, int fallback, int gross, int unavailable, BreakdownCalculationMode mode, string label)
    {
        var m = Metrics(2, 2 - unavailable, unavailable == 0 ? 0m : null) with { FinancialSources = new(mode, 2, net, fallback, gross, unavailable) };
        var p = new BreakdownMetricPresentation(m, "EUR");
        Assert.Equal(label, p.Basis); Assert.Contains($"{2 - unavailable}/2 evaluable", p.Coverage);
        if (fallback > 0) Assert.Contains($"{fallback} Gross fallback", p.Coverage);
        if (unavailable > 0) Assert.Contains($"{unavailable} unavailable", p.Coverage);
        Assert.Contains("not estimated Net", p.CoverageDetails);
    }

    [Theory]
    [InlineData(ProfitFactorStatus.NoTrades, "—", "no closed")]
    [InlineData(ProfitFactorStatus.IncompleteCoverage, "—", "incomplete")]
    [InlineData(ProfitFactorStatus.AllBreakEven, "—", "break-even")]
    [InlineData(ProfitFactorStatus.NoLosses, "∞", "Unbounded")]
    public void NullRatioIsNotInferredAsInfinity(ProfitFactorStatus status, string value, string explanation)
    {
        var m = Metrics(1, 1, 0m); m = m with { Selected = m.Selected with { ProfitFactor = new(status, null) } };
        var p = new BreakdownMetricPresentation(m, "USD");
        Assert.Equal(value, p.ProfitFactor); Assert.Contains(explanation, p.FactorExplanation);
    }

    [Fact]
    public void ZeroUnavailableAndFormattingRemainDistinct()
    {
        var zero = new BreakdownMetricPresentation(Metrics(1, 1, 0m), "USD");
        var missing = new BreakdownMetricPresentation(Metrics(2, 1, null), "EUR");
        Assert.Equal($"{0m:N2} USD", zero.Pnl); Assert.Equal($"{0m:N1}%", zero.WinRate);
        Assert.Equal("—", missing.Pnl); Assert.Equal("—", missing.Average); Assert.Equal("—", missing.WinRate);
        Assert.Contains("complete financial coverage", missing.UnavailableExplanation);
        Assert.Equal("No matching closed Trades.", new BreakdownMetricPresentation(Metrics(0, 0, null), "USD").UnavailableExplanation);
    }

    [Theory]
    [InlineData(0, false)] [InlineData(1, true)] [InlineData(19, true)] [InlineData(20, false)]
    public void SmallSampleUsesEvaluableNotEligibleCount(int known, bool small) =>
        Assert.Equal(small, new BreakdownMetricPresentation(Metrics(100, known, null), "USD").IsSmallSample);

    [Theory]
    [InlineData(BreakdownSortColumn.Pnl)] [InlineData(BreakdownSortColumn.Average)] [InlineData(BreakdownSortColumn.WinRate)]
    public void NumericSortUsesRawValuesWithMissingLastAndIdentityTies(BreakdownSortColumn column)
    {
        var p = Snapshot([Group(3, 100m), Group(2, 9m), Group(1, 9m), Group(4, null)]);
        Assert.Equal(new[] { 1, 2, 3, 4 }.Select(Key), p.Sort(column, false).Select(r => r.Key));
        Assert.Equal(new[] { 3, 1, 2, 4 }.Select(Key), p.Sort(column, true).Select(r => r.Key));
        Assert.Equal(new[] { 3, 2, 1, 4 }.Select(Key), p.Sort(null, false).Select(r => r.Key));
    }

    [Fact]
    public void CountsAndUnboundedRatiosSortWithoutSentinelValues()
    {
        var a = Group(1, 9m) with { Metrics = Metrics(100, 100, 9m) };
        var b = Group(2, 2m) with { Metrics = Metrics(9, 9, 2m) };
        var c = Group(3, 10m); c = c with { Metrics = c.Metrics with { Selected = c.Metrics.Selected with { ProfitFactor = new(ProfitFactorStatus.NoLosses, null) } } };
        var p = Snapshot([a, b, c, Group(4, null)]);
        Assert.Equal(100, p.Sort(BreakdownSortColumn.Trades, true)[0].Metrics.Trades);
        Assert.Equal(new[] { 2, 1, 3, 4 }.Select(Key), p.Sort(BreakdownSortColumn.ProfitFactor, false).Select(r => r.Key));
        Assert.Equal(new[] { 3, 1, 2, 4 }.Select(Key), p.Sort(BreakdownSortColumn.ProfitFactor, true).Select(r => r.Key));
    }

    [Fact]
    public void GroupSortUsesChronologicalKeysAndRetainsDuplicateLabels()
    {
        var keys = new[] { BreakdownGroupKey.ForEntryWeekday(DayOfWeek.Sunday), BreakdownGroupKey.ForEntryWeekday(DayOfWeek.Monday), BreakdownGroupKey.ForEntryWeekday(DayOfWeek.Friday) };
        var p = Snapshot(keys.Select((k, i) => new BreakdownGroupMetrics(k, k.GetLabel(), Metrics(1, 1, i))).ToArray());
        Assert.Equal(keys.Order(), p.Sort(BreakdownSortColumn.Group, false).Select(r => r.Key));
        foreach (var sequence in new[] { new[] { 23, 2, 10 }.Select(BreakdownGroupKey.ForEntryHour),
                     Enum.GetValues<HoldingDurationBucket>().Reverse().Select(BreakdownGroupKey.ForHoldingDuration) })
        {
            var temporal = Snapshot(sequence.Select(k => new BreakdownGroupMetrics(k, k.GetLabel(), Metrics(1, 1, 0))).ToArray());
            Assert.Equal(sequence.Order(), temporal.Sort(BreakdownSortColumn.Group, false).Select(r => r.Key));
        }
        var duplicates = Snapshot([Group(2, 2m) with { Label = "Same" }, Group(1, 1m) with { Label = "Same" }]);
        Assert.Equal(new[] { Key(1), Key(2) }, duplicates.Sort(BreakdownSortColumn.Group, false).Select(r => r.Key));
    }

    [Fact]
    public void UnclassifiedAndMissingSetupRemainDistinctAndCoverageIsNotInvented()
    {
        var missing = BreakdownGroupKey.ForSetup(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var empty = BreakdownGroupKey.ForSetup(null);
        var m = Metrics(2, 2, 0m); m = m with { Data = m.Data with { SetupClassification = new(2, 1), UnavailableSetupMetadataCount = 1 } };
        var p = new BreakdownPresentation(new(new("USD", grouping: BreakdownGrouping.TradingSetup), m,
            [new(empty, empty.GetLabel(), m), new(missing, missing.GetLabel(), m)]));
        Assert.Contains("Setup assigned: 1/2", p.SetupCoverage); Assert.Contains("Missing Setup metadata: 1", p.SetupCoverage);
        Assert.NotEqual(p.Rows[0].Key, p.Rows[1].Key); Assert.Equal("Unclassified", p.Rows[0].Label);
    }

    [Fact]
    public async Task SortingSelectionRefreshAndDraftDoNotRetargetOrRead()
    {
        using var f = new BreakdownFixture();
        f.Reader.MakeResult = q => new(q, Metrics(30, 30, 123m), [Group(1, 9m), Group(2, 100m) with { Label = "Same" }]);
        await f.Vm.ActivateAsync(); var p = f.Vm.Presentation;
        f.Vm.SelectedGroup = f.Vm.GroupRows[1]; f.Vm.SortGroups(BreakdownSortColumn.Pnl);
        f.Vm.Currency = "JPY"; f.Vm.Grouping = BreakdownGrouping.EntryHour;
        Assert.Single(f.Reader.Calls); Assert.Equal(Key(2), f.Vm.SelectedGroupKey); Assert.Same(p, f.Vm.Presentation);
        Assert.Equal("EUR", p!.Summary.Currency);
        f.Vm.Deactivate(); await f.Vm.ActivateAsync(); Assert.Single(f.Reader.Calls);
        await f.Vm.RefreshAsync(); Assert.Equal(Key(2), f.Vm.SelectedGroupKey); Assert.Equal(BreakdownSortColumn.Pnl, f.Vm.SortColumn);
        f.Reader.MakeResult = q => new(q, Metrics(1, 1, 0m), [Group(3, 0m) with { Label = "Same" }]);
        await f.Vm.RefreshAsync(); Assert.Null(f.Vm.SelectedGroup); Assert.Null(f.Vm.SelectedGroupKey);
        f.Vm.SelectedGroup = f.Vm.GroupRows[0];
        f.Reader.MakeResult = q => new(q, Metrics(1, 1, 0m), [new(BreakdownGroupKey.ForEntryHour(2), "02:00", Metrics(1, 1, 0m))]);
        await f.Vm.ApplyAsync(); Assert.Null(f.Vm.SelectedGroupKey);
    }

    [Fact]
    public async Task LateReadsNeverPartiallyReplacePresentationAndFailureRetainsIt()
    {
        using var f = new BreakdownFixture(); await f.Vm.ActivateAsync();
        f.Reader.Hold = true; f.Reader.Drain();
        var old = f.Vm.RefreshAsync(); var oldCall = await f.Reader.Next();
        var newer = f.Vm.RefreshAsync(); var newCall = await f.Reader.Next();
        newCall.Completion.SetResult(new(newCall.Query, Metrics(100, 100, 432m), [Group(1, -5m)])); await newer;
        var presentation = f.Vm.Presentation; var rows = f.Vm.GroupRows;
        oldCall.Completion.SetResult(BreakdownFixture.Snapshot(oldCall.Query)); await old;
        Assert.Same(presentation, f.Vm.Presentation); Assert.Same(rows, f.Vm.GroupRows);
        Assert.Same(f.Vm.Result, f.Vm.Presentation!.Source);
        var failed = f.Vm.RefreshAsync(); var call = await f.Reader.Next(); call.Completion.SetException(new IOException("synthetic")); await failed;
        Assert.Same(presentation, f.Vm.Presentation); Assert.Same(rows, f.Vm.GroupRows);
    }

    internal static BreakdownGroupKey Key(int n) => BreakdownGroupKey.ForInstrument(new Guid(n, 0, 0, new byte[8]));
    internal static BreakdownGroupMetrics Group(int n, decimal? value) => new(Key(n), "Instrument " + n, Metrics(1, value.HasValue ? 1 : 0, value));
    internal static BreakdownPresentation Snapshot(IReadOnlyList<BreakdownGroupMetrics> rows) => new(new(new("USD"), Metrics(1, 1, 0m), rows));
    internal static BreakdownMetrics Metrics(int count, int known, decimal? value)
    {
        var pnl = new BreakdownPnlMetrics(new(count, known), value, value, 0, 0, 0, null, null, value,
            new(value.HasValue ? ProfitFactorStatus.Defined : ProfitFactorStatus.IncompleteCoverage, value),
            new(value.HasValue ? AveragePnlStatus.Defined : AveragePnlStatus.IncompleteCoverage, value), new(AveragePnlStatus.NoWins, null), new(AveragePnlStatus.NoLosses, null));
        return new(pnl, pnl, new(BreakdownCalculationMode.NetPreferred, count, known, 0, 0, count - known),
            new(new(count, known), new(count, known), new(count, known), new(count, count), 0), []);
    }
    private sealed class ForbiddenIds : IReadOnlyList<Guid>
    {
        public int Count => throw new InvalidOperationException("Presentation must not enumerate Trade IDs.");
        public Guid this[int index] => throw new InvalidOperationException();
        public IEnumerator<Guid> GetEnumerator() => throw new InvalidOperationException();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
