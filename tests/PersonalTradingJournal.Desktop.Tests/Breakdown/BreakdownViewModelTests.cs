using System.Collections.Concurrent;
using System.Threading.Channels;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.Analytics.Breakdown;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Desktop.ViewModels.Breakdown;
using PersonalTradingJournal.Desktop.ViewModels.Dashboard;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Desktop.Tests.Breakdown;

public sealed class BreakdownViewModelTests
{
    [Fact]
    public async Task DefaultsLookupsAndRepeatedActivationLoadOnce()
    {
        using var f = new BreakdownFixture();
        Assert.False(f.Vm.HasResult);
        await f.Vm.ActivateAsync(); await f.Vm.ActivateAsync();
        var call = Assert.Single(f.Reader.Calls);
        Assert.Null(call.Query.TradeFilter.TradingAccountId); Assert.Null(call.Query.TradeFilter.InstrumentId);
        Assert.Null(call.Query.TradeFilter.ClosedFromUtc);
        Assert.Equal(BreakdownGrouping.Instrument, call.Query.Grouping);
        Assert.Equal(BreakdownCalculationMode.NetPreferred, call.Query.Mode);
        Assert.Equal("EUR", call.Query.Currency); // ordinal-first catalog suggestion, as documented
        Assert.Equal(7, f.Vm.Groupings.Count);
        Assert.Contains(f.Vm.Accounts, a => a.Id == BreakdownFixture.AccountId && a.Name.Contains("inactive"));
        Assert.Contains(f.Vm.Instruments, i => i.Id == BreakdownFixture.InstrumentId && i.Name.Contains("inactive"));
        Assert.Same(call.Returned, f.Vm.Result);
        Assert.False(f.Vm.HasUnappliedChanges);
    }

    [Fact]
    public async Task DraftApplyRefreshResetAndExactMappingRemainSeparate()
    {
        using var f = new BreakdownFixture(); await f.Vm.ActivateAsync();
        var original = f.Vm.Result;
        f.Vm.Period = DashboardPeriod.Custom; f.Vm.StartDateText = "2026-03-08"; f.Vm.EndDateText = "2026-03-08";
        f.Vm.SelectedAccount = f.Vm.Accounts.Single(a => a.Id == BreakdownFixture.AccountId);
        f.Vm.SelectedInstrument = f.Vm.Instruments.Single(i => i.Id == BreakdownFixture.InstrumentId);
        f.Vm.Currency = " usd "; f.Vm.Grouping = BreakdownGrouping.EntryHour; f.Vm.Mode = BreakdownCalculationMode.Gross;
        Assert.Single(f.Reader.Calls); Assert.True(f.Vm.HasUnappliedChanges); Assert.Same(original, f.Vm.Result);
        await f.Vm.ApplyCommand.ExecuteAsync(null);
        var query = f.Vm.Result!.Query;
        Assert.Equal(new BreakdownQuery("USD", BreakdownFixture.AccountId, BreakdownFixture.InstrumentId,
            new(2026, 3, 8), new(2026, 3, 8), BreakdownCalculationMode.Gross, BreakdownGrouping.EntryHour), query);
        Assert.Equal(TimeSpan.FromHours(23), query.TradeFilter.ClosedBeforeUtc - query.TradeFilter.ClosedFromUtc);
        Assert.False(f.Vm.HasUnappliedChanges);
        f.Vm.Currency = "JPY";
        await f.Vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(query, f.Vm.Result!.Query); Assert.Equal("JPY", f.Vm.Currency); Assert.True(f.Vm.HasUnappliedChanges);
        int count = f.Reader.Calls.Count;
        await f.Vm.ResetCommand.ExecuteAsync(null);
        Assert.Equal(count + 1, f.Reader.Calls.Count);
        Assert.Equal(new BreakdownQuery("EUR"), f.Vm.Result!.Query);
        Assert.False(f.Vm.HasUnappliedChanges);
    }

    [Theory]
    [InlineData(DashboardPeriod.Week, "2026-03-02", "2026-03-08")]
    [InlineData(DashboardPeriod.Month, "2026-03-01", "2026-03-31")]
    [InlineData(DashboardPeriod.Year, "2026-01-01", "2026-12-31")]
    public async Task PresetsUseNewYorkToday(DashboardPeriod period, string start, string end)
    {
        using var f = new BreakdownFixture(); await f.Vm.ActivateAsync(); f.Vm.Period = period;
        await f.Vm.ApplyAsync();
        Assert.Equal(DateOnly.Parse(start), f.Vm.Result!.Query.TradeFilter.ClosedFromNewYork);
        Assert.Equal(DateOnly.Parse(end), f.Vm.Result.Query.TradeFilter.ClosedThroughNewYork);
    }

    [Theory]
    [InlineData("not a date", "2026-03-08", "USD")]
    [InlineData("2026-03-09", "2026-03-08", "USD")]
    [InlineData("", "9999-12-31", "USD")]
    [InlineData("", "", "")]
    [InlineData("", "", "TOO-LONG-CURRENCY")]
    public async Task InvalidFiltersNeverReadAndKeepLastResult(string start, string end, string currency)
    {
        using var f = new BreakdownFixture(); await f.Vm.ActivateAsync(); var before = f.Vm.Result;
        f.Vm.Period = DashboardPeriod.Custom; f.Vm.StartDateText = start; f.Vm.EndDateText = end; f.Vm.Currency = currency;
        await f.Vm.ApplyAsync();
        Assert.Single(f.Reader.Calls); Assert.Same(before, f.Vm.Result); Assert.NotNull(f.Vm.ErrorMessage);
        Assert.True(f.Vm.DateValidation is not null || f.Vm.CurrencyValidation is not null);
        f.Vm.StartDateText = ""; f.Vm.EndDateText = ""; f.Vm.Currency = "USD";
        Assert.Null(f.Vm.DateValidation); Assert.Null(f.Vm.CurrencyValidation);
        await f.Vm.ApplyAsync(); Assert.Null(f.Vm.ErrorMessage);
    }

    [Fact]
    public async Task EmptyUnavailableAndFallbackUseExactResultMetadata()
    {
        using var f = new BreakdownFixture();
        f.Reader.MakeResult = q => BreakdownMetricCalculator.Calculate(q, []);
        await f.Vm.ActivateAsync(); Assert.True(f.Vm.IsEmpty); Assert.Contains("No matching", f.Vm.ResultStatus);
        f.Reader.MakeResult = q => BreakdownFixture.Snapshot(q, unavailable: true);
        await f.Vm.RefreshAsync(); Assert.False(f.Vm.IsEmpty); Assert.Contains("No evaluable", f.Vm.ResultStatus);
        Assert.Contains("Unavailable: 1", f.Vm.CoverageStatus);
        f.Reader.MakeResult = q => BreakdownFixture.Snapshot(q, fallback: true);
        await f.Vm.RefreshAsync(); Assert.Contains("Gross fallback", f.Vm.ResultStatus);
        Assert.Contains("Gross fallback: 1", f.Vm.CoverageStatus);
        Assert.Same(f.Reader.Calls.Last().Returned, f.Vm.Result);
    }

    [Fact]
    public async Task FailureAndCancellationKeepScopeAndAllowRetry()
    {
        using var f = new BreakdownFixture(); await f.Vm.ActivateAsync(); var before = f.Vm.Result;
        f.Reader.Hold = true; f.Reader.Drain();
        f.Vm.Currency = "USD"; Task failed = f.Vm.ApplyAsync(); var request = await f.Reader.Next();
        request.Completion.SetException(new IOException("Do not expose private provider or database details")); await failed;
        Assert.Same(before, f.Vm.Result); Assert.DoesNotContain("private", f.Vm.ErrorMessage!); Assert.False(f.Vm.IsLoading);
        Task cancelled = f.Vm.RefreshAsync(); var second = await f.Reader.Next();
        f.Vm.CancelCommand.Execute(null); Assert.True(second.Token.IsCancellationRequested);
        second.Completion.SetResult(BreakdownFixture.Snapshot(second.Query)); await cancelled;
        Assert.Same(before, f.Vm.Result); Assert.Contains("cancelled", f.Vm.StatusMessage); Assert.Null(f.Vm.ErrorMessage);
        f.Reader.Hold = false; await f.Vm.RefreshAsync();
        Assert.Equal("USD", f.Vm.Result!.Query.Currency); Assert.Null(f.Vm.ErrorMessage);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("failure")]
    [InlineData("cancel")]
    public async Task OlderCompletionCannotPublishOrClearNewerLoading(string outcome)
    {
        using var f = new BreakdownFixture(); await f.Vm.ActivateAsync(); f.Reader.Hold = true; f.Reader.Drain();
        Task older = f.Vm.ApplyAsync(); var oldCall = await f.Reader.Next();
        f.Vm.Currency = "USD"; Task newer = f.Vm.ApplyAsync(); var newCall = await f.Reader.Next();
        Assert.True(oldCall.Token.IsCancellationRequested);
        Complete(oldCall, outcome); await older;
        Assert.True(f.Vm.IsLoading); Assert.Null(f.Vm.ErrorMessage);
        newCall.Completion.SetResult(BreakdownFixture.Snapshot(newCall.Query)); await newer;
        Assert.Equal("USD", f.Vm.Result!.Query.Currency); Assert.False(f.Vm.IsLoading);
    }

    [Fact]
    public async Task LateSuccessAfterNewSuccessAndEditedDraftCannotRelabelResult()
    {
        using var f = new BreakdownFixture(); await f.Vm.ActivateAsync(); f.Reader.Hold = true; f.Reader.Drain();
        Task older = f.Vm.ApplyAsync(); var a = await f.Reader.Next();
        f.Vm.Currency = "USD"; Task newer = f.Vm.ApplyAsync(); var b = await f.Reader.Next();
        f.Vm.Currency = "GBP";
        b.Completion.SetResult(BreakdownFixture.Snapshot(b.Query)); await newer; var current = f.Vm.Result;
        a.Completion.SetResult(BreakdownFixture.Snapshot(a.Query)); await older;
        Assert.Same(current, f.Vm.Result); Assert.Contains("USD", f.Vm.AppliedContext);
        Assert.Equal("GBP", f.Vm.Currency); Assert.True(f.Vm.HasUnappliedChanges);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InactiveOrDisposedPageIgnoresLateResultsAndKeepsDraft(bool dispose)
    {
        using var f = new BreakdownFixture(); await f.Vm.ActivateAsync(); var original = f.Vm.Result;
        f.Reader.Hold = true; f.Reader.Drain(); f.Vm.Currency = "USD";
        Task work = f.Vm.ApplyAsync(); var call = await f.Reader.Next();
        if (dispose) f.Vm.Dispose(); else f.Vm.Deactivate();
        var status = f.Vm.StatusMessage;
        Assert.True(call.Token.IsCancellationRequested); Assert.False(f.Vm.IsLoading);
        call.Completion.SetResult(BreakdownFixture.Snapshot(call.Query)); await work;
        Assert.Equal(status, f.Vm.StatusMessage); Assert.Same(original, f.Vm.Result);
        await f.Vm.ActivateAsync(); Assert.Equal(2, f.Reader.Calls.Count); Assert.Equal("USD", f.Vm.Currency);
        if (!dispose) { f.Reader.Hold = false; await f.Vm.RefreshAsync(); Assert.Equal("USD", f.Vm.Result!.Query.Currency); }
    }

    [Fact]
    public async Task MissingLookupRetainsExactIdAndSameNameReplacementIsNeverSelected()
    {
        using var f = new BreakdownFixture(); await f.Vm.ActivateAsync();
        f.Vm.SelectedAccount = f.Vm.Accounts.Single(a => a.Id == BreakdownFixture.AccountId);
        f.Accounts.Rows = [BreakdownFixture.Account(Guid.NewGuid())];
        await f.Vm.ApplyAsync();
        Assert.Equal(BreakdownFixture.AccountId, f.Vm.Result!.Query.TradeFilter.TradingAccountId);
        Assert.Equal(BreakdownFixture.AccountId, f.Vm.SelectedAccount.Id);
        Assert.False(f.Vm.SelectedAccount.Available); Assert.Contains("unavailable", f.Vm.LookupWarning);
        var label = f.Vm.SelectedAccount.Name; await f.Vm.RefreshAsync(); Assert.Equal(label, f.Vm.SelectedAccount.Name);
    }

    [Fact]
    public async Task CommitInvalidatesAnInFlightReadWithoutAutoApplyingDraft()
    {
        using var f = new BreakdownFixture(); await f.Vm.ActivateAsync(); var before = f.Vm.Result;
        f.Reader.Hold = true; f.Reader.Drain(); Task work = f.Vm.RefreshAsync(); var call = await f.Reader.Next();
        f.Vm.Currency = "JPY"; f.Vm.OnDataCommitted();
        call.Completion.SetResult(BreakdownFixture.Snapshot(call.Query)); await work;
        Assert.True(f.Vm.IsStale); Assert.Same(before, f.Vm.Result); Assert.Equal(2, f.Reader.Calls.Count);
        f.Reader.Hold = false; await f.Vm.RefreshAsync(); Assert.False(f.Vm.IsStale);
        Assert.Equal("EUR", f.Vm.Result!.Query.Currency); Assert.Equal("JPY", f.Vm.Currency);
    }

    [Fact]
    public async Task LookupFailureIsRecoverableAndInitialReaderFailureRetainsActualSubmittedCurrency()
    {
        using var f = new BreakdownFixture();
        f.Accounts.Load = () => throw new IOException("Synthetic lookup failure");
        await f.Vm.ActivateAsync(); Assert.Empty(f.Reader.Calls); Assert.NotNull(f.Vm.ErrorMessage);
        f.Accounts.Load = null;
        await f.Vm.ApplyAsync(); Assert.NotNull(f.Vm.Result); Assert.Null(f.Vm.ErrorMessage);

        using var other = new BreakdownFixture();
        other.Reader.MakeResult = _ => throw new IOException("Synthetic read failure");
        await other.Vm.ActivateAsync(); Assert.Equal("EUR", Assert.Single(other.Reader.Calls).Query.Currency);
        other.Reader.MakeResult = q => BreakdownFixture.Snapshot(q);
        await other.Vm.RefreshAsync(); Assert.Equal("EUR", other.Vm.Result!.Query.Currency);
    }

    [Fact]
    public async Task InitialLookupCannotRetargetDraftEditedWhileItWasLoading()
    {
        using var f = new BreakdownFixture();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<IReadOnlyList<AccountListItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Accounts.Load = () => { entered.SetResult(); return release.Task; };
        Task activation = f.Vm.ActivateAsync(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Same(activation, f.Vm.ActivateAsync());
        f.Vm.Currency = "JPY"; f.Vm.Mode = BreakdownCalculationMode.Gross;
        release.SetResult(f.Accounts.Rows); await activation;
        Assert.Equal("EUR", f.Vm.Result!.Query.Currency); Assert.Equal(BreakdownCalculationMode.NetPreferred, f.Vm.Result.Query.Mode);
        Assert.Equal("JPY", f.Vm.Currency); Assert.Equal(BreakdownCalculationMode.Gross, f.Vm.Mode);
        Assert.True(f.Vm.HasUnappliedChanges); Assert.Single(f.Reader.Calls);
    }

    [Fact]
    public async Task InitialCancellationRetainsActualSubmittedCurrencyForRefreshWithoutApplyingDraft()
    {
        using var f = new BreakdownFixture();
        f.Reader.Hold = true;
        Task initial = f.Vm.ActivateAsync(); var call = await f.Reader.Next();
        Assert.Equal("EUR", call.Query.Currency);
        f.Vm.Currency = "JPY";
        f.Vm.CancelCommand.Execute(null);
        call.Completion.SetResult(BreakdownFixture.Snapshot(call.Query)); await initial;
        Assert.Null(f.Vm.Result); Assert.False(f.Vm.IsLoading);
        f.Reader.Hold = false;
        await f.Vm.RefreshAsync();
        Assert.Equal("EUR", f.Vm.Result!.Query.Currency);
        Assert.Equal("JPY", f.Vm.Currency); Assert.True(f.Vm.HasUnappliedChanges);
    }

    private static void Complete(BreakdownFixture.Call call, string outcome)
    {
        if (outcome == "failure") call.Completion.SetException(new IOException("Synthetic"));
        else if (outcome == "cancel") call.Completion.SetCanceled(call.Token);
        else call.Completion.SetResult(BreakdownFixture.Snapshot(call.Query));
    }
}

internal sealed class BreakdownFixture : IDisposable
{
    internal static readonly Guid AccountId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    internal static readonly Guid InstrumentId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    internal readonly ControlledReader Reader = new();
    internal readonly AccountsReader Accounts = new();
    internal readonly InstrumentsReader Instruments = new();
    internal readonly BreakdownViewModel Vm;
    internal BreakdownFixture() => Vm = new(Reader, Accounts, Instruments, new Clock());
    public void Dispose() => Vm.Dispose();
    internal static AccountListItem Account(Guid id) => new(id, "Synthetic Account", TradingAccountType.Personal, null, null, "EUR", 0m, false);
    internal static BreakdownAnalyticsSnapshot Snapshot(BreakdownQuery q, bool unavailable = false, bool fallback = false)
    {
        var close = q.TradeFilter.ClosedFromUtc?.AddHours(1) ?? new DateTimeOffset(2026, 3, 8, 12, 0, 0, TimeSpan.Zero);
        var fact = new TradeAnalyticsFact(Guid.NewGuid(), TradeStatus.Closed, close.AddMinutes(-5), close, q.Currency, null,
            unavailable ? null : 10m, unavailable || fallback ? null : 2m, unavailable || fallback ? null : 8m);
        return BreakdownMetricCalculator.Calculate(q, [new(fact, q.TradeFilter.TradingAccountId ?? AccountId,
            q.TradeFilter.InstrumentId ?? InstrumentId, TradeDirection.Long)]);
    }
    internal sealed record Call(BreakdownQuery Query, CancellationToken Token, int Thread,
        TaskCompletionSource<BreakdownAnalyticsSnapshot> Completion)
    { internal BreakdownAnalyticsSnapshot? Returned { get; set; } }
    internal sealed class ControlledReader : IBreakdownAnalyticsReader
    {
        internal bool Hold;
        internal Func<BreakdownQuery, BreakdownAnalyticsSnapshot> MakeResult = q => Snapshot(q);
        internal ConcurrentQueue<Call> Calls { get; } = new();
        private readonly Channel<Call> _started = Channel.CreateUnbounded<Call>();
        internal void Drain() { while (_started.Reader.TryRead(out _)) { } }
        internal async Task<Call> Next() => await _started.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        public async Task<BreakdownAnalyticsSnapshot> GetAsync(BreakdownQuery query, CancellationToken cancellationToken = default)
        {
            var call = new Call(query, cancellationToken, Environment.CurrentManagedThreadId, new(TaskCreationOptions.RunContinuationsAsynchronously));
            Calls.Enqueue(call); _started.Writer.TryWrite(call);
            call.Returned = Hold ? await call.Completion.Task : MakeResult(query);
            return call.Returned;
        }
    }
    internal sealed class AccountsReader : ITradingAccountReader
    {
        internal IReadOnlyList<AccountListItem> Rows = [Account(AccountId)];
        internal Func<Task<IReadOnlyList<AccountListItem>>>? Load;
        public Task<IReadOnlyList<AccountListItem>> GetAllAsync(CancellationToken cancellationToken = default) => Load?.Invoke() ?? Task.FromResult(Rows);
        public Task<TradingAccountDetails?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult<TradingAccountDetails?>(null);
    }
    internal sealed class InstrumentsReader : IInstrumentReader
    {
        public Task<IReadOnlyList<InstrumentListItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<InstrumentListItem>>([new(InstrumentId, "TEST", "Synthetic instrument", AssetClass.Futures, "CME", "USD", .25m, .5m, 2m, false)]);
        public Task<InstrumentDetails?> GetByIdAsync(Guid instrumentId, CancellationToken cancellationToken = default) => Task.FromResult<InstrumentDetails?>(null);
    }
    private sealed class Clock : TimeProvider
    { public override DateTimeOffset GetUtcNow() => new(2026, 3, 9, 1, 0, 0, TimeSpan.Zero); /* Still Sunday in New York. */ }
}
