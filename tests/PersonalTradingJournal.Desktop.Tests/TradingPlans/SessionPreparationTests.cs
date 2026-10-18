using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Desktop.Tests.Journals;
using PersonalTradingJournal.Desktop.ViewModels.TradingPlans;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Desktop.Tests.TradingPlans;

public sealed class SessionPreparationTests
{
    [Theory]
    [InlineData("en-US", "12345.123456789012345678901234", true)]
    [InlineData("bg-BG", "12345,123456789012345678901234", true)]
    [InlineData("de-DE", "-0,125", true)]
    [InlineData("bg-BG", "0.25", true)]
    [InlineData("en-US", "0", true)]
    [InlineData("en-US", " ", true)]
    [InlineData("en-US", "oops", false)]
    [InlineData("en-US", "1,000", false)]
    [InlineData("en-US", "0.12345678901234567890123456789", false)]
    [InlineData("en-US", "79228162514264337593543950336", false)]
    public async Task ExactOptionalCulturePricesNeverSaveOlderOrRoundedValues(string culture, string raw, bool valid)
    {
        var f = new Fixture(culture); using var vm = f.Vm;
        var field = vm.Contexts[0].Levels[0]; field.Text = raw;
        Assert.Equal(!valid, field.IsInvalid); Assert.Equal(raw, field.Text);
        Assert.Equal(valid ? TradingPlanWriteStatus.Success : TradingPlanWriteStatus.Invalid, await vm.SaveAsync());
        Assert.Equal(valid ? 1 : 0, f.Store.Writes);
        Assert.Equal(raw, field.Text);
        if (valid)
        {
            decimal? expected = string.IsNullOrWhiteSpace(raw) ? null : decimal.Parse(raw.Replace(',', '.'), CultureInfo.InvariantCulture);
            Assert.Equal(expected, f.Store.Plan.WorkingDraft!.Content.Instruments[0].Levels.PreviousDayHigh);
            Assert.False(vm.IsDirty);
        }
        else Assert.True(vm.IsDirty);
    }

    [Fact]
    public async Task SwitchingRetainsRawInputAndIdentityAndRemovalProtectsScenarios()
    {
        var f = new Fixture(); using var vm = f.Vm; await vm.LoadCatalogAsync();
        var first = vm.SelectedInstrument!; first.Levels[1].Text = "invalid"; first.Thesis.Text = "first\ncontext"; first.Bias = TradingPlanBias.Conditional;
        vm.InstrumentToAdd = f.Catalog.Items[1]; vm.AddInstrumentCommand.Execute(null);
        var second = vm.SelectedInstrument!; second.Thesis.Text = "second"; second.Levels[1].Text = "0";
        vm.AddInstrumentCommand.Execute(null); Assert.Equal(2, vm.Contexts.Count);
        vm.SelectedInstrument = first;
        Assert.Equal("invalid", first.Levels[1].Text); Assert.Equal("second", second.Thesis.Text);
        Assert.Equal(TradingPlanBias.Conditional, first.Bias); Assert.Null(second.Bias);
        Assert.Equal(TradingPlanWriteStatus.Invalid, await vm.SaveAsync()); Assert.Equal(0, f.Store.Writes);
        vm.RemoveInstrumentCommand.Execute(null); Assert.Equal(2, vm.Contexts.Count); Assert.Contains("owns scenarios", vm.Message);
        vm.SelectedInstrument = second; vm.RemoveInstrumentCommand.Execute(null); Assert.Single(vm.Contexts);
        Assert.Same(first, vm.SelectedInstrument);
    }

    [Fact]
    public async Task MeaningfulEditsReuseDomainInvalidationAndRetainUnownedSections()
    {
        var f = new Fixture(); using var vm = f.Vm;
        Assert.False(vm.IsDirty); Assert.Equal(TradingPlanChecklist.All, vm.Confirmed);
        vm.Focus.Text = vm.Focus.Text; Assert.False(vm.IsDirty);
        // Formatting is raw-dirty, but numerically unchanged: no checklist invalidation.
        vm.Contexts[0].Levels[0].Text = "0.00";
        Assert.True(vm.IsDirty); Assert.Equal(TradingPlanChecklist.All, vm.Confirmed);
        var version = vm.Token.Version; await vm.SaveAsync(); Assert.Equal(version, vm.Token.Version);
        vm.Focus.Text = "Patience\nWait for confirmation";
        Assert.Equal(TradingPlanChecklist.All & ~TradingPlanChecklist.MindsetAndFocus, vm.Confirmed);
        vm.News.Text = "Manual: 09:30 restricted\nCheck separately";
        await vm.SaveAsync();
        var saved = f.Store.Plan.WorkingDraft!.Content;
        Assert.False(saved.Confirmed.HasFlag(TradingPlanChecklist.News));
        Assert.False(saved.Confirmed.HasFlag(TradingPlanChecklist.ScenariosOrStandAside));
        Assert.True(f.Content.Scenarios.SequenceEqual(saved.Scenarios)); Assert.Equal(f.Content.RiskLimits, saved.RiskLimits);
        Assert.Equal(vm.News.Text, saved.NewsAndRestrictions);
        vm.News.Text = ""; await vm.SaveAsync(); Assert.Equal("", f.Store.Plan.WorkingDraft!.Content.NewsAndRestrictions);
        vm.Session = TradingPlanSession.London; Assert.Equal(TradingPlanChecklist.None, vm.Confirmed);
    }

    [Theory]
    [InlineData(TradingPlanWriteStatus.Conflict)]
    [InlineData(TradingPlanWriteStatus.ReferenceChanged)]
    [InlineData(TradingPlanWriteStatus.ReferenceUnavailable)]
    [InlineData(TradingPlanWriteStatus.StorageFailure)]
    [InlineData(TradingPlanWriteStatus.Cancelled)]
    [InlineData(TradingPlanWriteStatus.Invalid)]
    public async Task RejectedWritesRetainAllInputAndToken(TradingPlanWriteStatus status)
    {
        var f = new Fixture(); using var vm = f.Vm; f.Store.Result = status;
        vm.Focus.Text = "kept"; vm.News.Text = "notes"; vm.Contexts[0].Levels[0].Text = "-123.125";
        var token = vm.Token;
        Assert.Equal(status, await vm.SaveAsync()); Assert.True(vm.IsDirty);
        Assert.Equal(token, vm.Token); Assert.Equal("kept", vm.Focus.Text); Assert.Equal("notes", vm.News.Text);
        Assert.Equal("-123.125", vm.Contexts[0].Levels[0].Text); Assert.NotEqual("Draft saved and verified.", vm.Message);
        f.Store.Result = null;
        Assert.Equal(TradingPlanWriteStatus.Success, await vm.RefreshReferencesAsync());
        Assert.Equal(TradingPlanChecklist.None, vm.Confirmed); Assert.False(vm.IsDirty);
    }

    [Fact]
    public async Task ValidationClearsIndividuallyAndIncompleteDraftIsAllowed()
    {
        var f = new Fixture(); using var vm = f.Vm;
        vm.Focus.Text = new string('x', 501); vm.News.Text = new string('x', 4001);
        Assert.True(vm.Focus.IsInvalid); Assert.True(vm.News.IsInvalid);
        Assert.Equal(TradingPlanWriteStatus.Invalid, await vm.SaveAsync()); Assert.Equal(1, vm.ValidationAttempt);
        vm.Focus.Text = ""; Assert.False(vm.Focus.IsInvalid); Assert.True(vm.HasValidationErrors);
        vm.News.Text = ""; vm.Contexts[0].Thesis.Text = ""; vm.Contexts[0].Bias = null; vm.Session = null;
        Assert.False(vm.HasValidationErrors); Assert.Equal(TradingPlanWriteStatus.Success, await vm.SaveAsync());
    }

    [Fact]
    public async Task BusyCancellationAndDisposalDoNotLoseInputOrDoubleSubmit()
    {
        var f = new Fixture(); using var vm = f.Vm; vm.Focus.Text = "pending";
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Store.Block = async ct => { entered.SetResult(); await Task.Delay(Timeout.Infinite, ct); };
        var save = vm.SaveAsync(); await entered.Task;
        Assert.True(vm.IsBusy); Assert.False(vm.CanEdit); vm.Focus.Text = "must not replace in-flight input";
        Assert.Equal(TradingPlanWriteStatus.Invalid, await vm.SaveAsync());
        vm.Dispose(); Assert.Equal(TradingPlanWriteStatus.Cancelled, await save);
        Assert.Equal(1, f.Store.Writes); Assert.Equal("pending", vm.Focus.Text); Assert.True(vm.IsDirty);
    }

    [Fact]
    public async Task CatalogUnavailableOrInactiveNeverSubstitutesIdentityAndLoadFailureRetainsInput()
    {
        var f = new Fixture(); using var vm = f.Vm;
        f.Catalog.Items = [f.Catalog.Items[0] with { IsActive = false }]; await vm.LoadCatalogAsync();
        Assert.Empty(vm.AvailableInstruments); Assert.Contains("inactive", vm.Contexts[0].Label);
        vm.Focus.Text = "retained"; f.Catalog.Items = []; await vm.LoadCatalogAsync();
        Assert.Contains("Unavailable", vm.Contexts[0].Label); Assert.Equal(f.Instrument.Id, vm.Contexts[0].InstrumentId);
        f.Catalog.Fail = true; await vm.LoadCatalogAsync(); Assert.Contains("could not", vm.Message); Assert.Equal("retained", vm.Focus.Text);
    }

    [Fact]
    public async Task ReadyUsesFrozenLabelsAndCannotBeEditedOrSaved()
    {
        var f = new Fixture(); var plan = f.Store.Plan;
        var ready = plan.MarkReady(plan.Version, TradingPlanReferenceSnapshot.Capture(plan.Key,
            plan.WorkingDraft!.Content, f.Account, [f.Instrument], [], [], Fixture.Now), Fixture.Now);
        using var vm = new SessionPreparationViewModel(plan, f.Store, f.Catalog);
        f.Catalog.Items = [f.Catalog.Items[0] with { Symbol = "RENAMED" }]; await vm.LoadCatalogAsync();
        Assert.Contains("NQ", vm.Contexts[0].Label); Assert.DoesNotContain("RENAMED", vm.Contexts[0].Label);
        vm.Focus.Text = "ignored"; vm.News.Text = "ignored"; vm.Contexts[0].Levels[0].Text = "ignored";
        vm.Contexts[0].Bias = TradingPlanBias.Short;
        Assert.True(vm.IsReadOnly); Assert.False(vm.IsDirty); Assert.Equal(ready.Content.Focus, vm.Focus.Text);
        Assert.Equal(TradingPlanWriteStatus.Invalid, await vm.SaveAsync()); Assert.Equal(0, f.Store.Writes);
        plan.StartDraft(plan.Version, ready, Fixture.Now);
        using var draft = new SessionPreparationViewModel(plan, f.Store, f.Catalog);
        draft.Focus.Text = "new draft"; Assert.True(draft.IsDirty); Assert.Equal(ready.Content.Focus, vm.Focus.Text);
        using var historical = new SessionPreparationViewModel(plan, f.Store, f.Catalog, historical: ready);
        Assert.True(historical.IsReadOnly); Assert.Equal(TradingPlanChecklist.All, historical.Confirmed);
    }

    [Fact]
    public async Task RealSqliteSaveReloadConflictAndExplicitReferenceRefresh()
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        var f = new Fixture();
        await db.Provider.GetRequiredService<ITradingAccountStore>().AddAsync(f.Account);
        var instruments = db.Provider.GetRequiredService<IInstrumentStore>(); await instruments.AddAsync(f.Instrument);
        var store = db.Provider.GetRequiredService<ITradingPlanStore>();
        var created = await store.WriteAsync(new(TradingPlanWriteOperation.CreateDraft, f.Store.Plan.Key, f.Content));
        Assert.Equal(TradingPlanWriteStatus.Success, created.Status);
        using var vm = new SessionPreparationViewModel(created.Plan!, store, db.Provider.GetRequiredService<IInstrumentReader>());
        await vm.LoadCatalogAsync(); vm.Focus.Text = "SQLite\nexact"; vm.Contexts[0].Levels[0].Text = "123.12345678901234567890123456";
        Assert.Equal(TradingPlanWriteStatus.Success, await vm.SaveAsync());
        var loaded = (await store.LoadAsync(vm.Key))!;
        using var reopened = new SessionPreparationViewModel(loaded, store, f.Catalog);
        Assert.Equal(vm.Focus.Text, reopened.Focus.Text); Assert.Equal(vm.Contexts[0].Levels[0].Text, reopened.Contexts[0].Levels[0].Text);
        reopened.News.Text = "other editor"; Assert.Equal(TradingPlanWriteStatus.Success, await reopened.SaveAsync());
        vm.News.Text = "my retained notes"; Assert.Equal(TradingPlanWriteStatus.Conflict, await vm.SaveAsync()); Assert.Equal("my retained notes", vm.News.Text);
        f.Instrument.Deactivate(DateTimeOffset.UtcNow); await instruments.UpdateAsync(f.Instrument);
        reopened.Focus.Text = "retained during unavailable reference";
        Assert.Equal(TradingPlanWriteStatus.ReferenceUnavailable, await reopened.SaveAsync());
        f.Instrument.Activate(DateTimeOffset.UtcNow); await instruments.UpdateAsync(f.Instrument);
        Assert.Equal(TradingPlanWriteStatus.ReferenceChanged, await reopened.SaveAsync());
        Assert.Equal(TradingPlanWriteStatus.Success, await reopened.RefreshReferencesAsync());
        Assert.Equal(TradingPlanChecklist.None, reopened.Confirmed);
    }

    internal sealed class Fixture
    {
        internal static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
        internal readonly TradingAccount Account = new("Synthetic", TradingAccountType.Personal, null, null, "USD", 1000, Now.AddDays(-1));
        internal readonly Instrument Instrument = new("NQ", "Nasdaq futures", AssetClass.Futures, null, "USD", .25m, 5, Now.AddDays(-1));
        internal readonly TradingPlanContent Content;
        internal readonly Store Store;
        internal readonly Catalog Catalog = new();
        internal readonly SessionPreparationViewModel Vm;
        internal Fixture(string culture = "en-US")
        {
            Content = new(TradingPlanSession.NewYork, "Synthetic focus", instruments: [new(Instrument.Id, TradingPlanBias.Long, "Wait for acceptance", new(0))],
                scenarios: [new(Guid.NewGuid(), Instrument.Id, "Synthetic scenario", direction: Domain.Trades.TradeDirection.Long,
                    condition: "Condition", response: "Response", invalidation: "Invalidation")]);
            var plan = new TradingPlan(new(Account.Id, new(2026, 10, 17)), Content, Now);
            plan.SetConfirmations(plan.Version, TradingPlanChecklist.All, Now);
            Store = new(plan);
            Catalog.Items = [new(Instrument.Id, "NQ", "Nasdaq futures", AssetClass.Futures, null, "USD", .25m, 5, 20, true),
                new(Guid.NewGuid(), "ES", "S&P futures", AssetClass.Futures, null, "USD", .25m, 12.5m, 50, true)];
            Vm = new(plan, Store, Catalog, CultureInfo.GetCultureInfo(culture));
        }
    }
    internal sealed class Catalog : IInstrumentReader
    {
        internal IReadOnlyList<InstrumentListItem> Items = [];
        internal bool Fail;
        public Task<IReadOnlyList<InstrumentListItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Fail ? throw new IOException("Synthetic failure") : Task.FromResult(Items);
        public Task<InstrumentDetails?> GetByIdAsync(Guid instrumentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    internal sealed class Store(TradingPlan plan) : ITradingPlanStore
    {
        internal TradingPlan Plan = plan;
        internal int Writes;
        internal TradingPlanWriteStatus? Result;
        internal Func<CancellationToken, Task>? Block;
        internal Func<TradingPlanContent, TradingPlanReferenceSnapshot>? CaptureReferences;
        public async Task<TradingPlanWriteResult> WriteAsync(TradingPlanWriteCommand command, CancellationToken cancellationToken = default)
        {
            Writes++;
            if (Block is not null) await Block(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (Result.HasValue) return new(Result.Value);
            var copy = TradingPlan.Rehydrate(Plan.Id, Plan.Key, Plan.Version, Plan.WorkingDraft, Plan.LatestReadyRevision, Plan.CreatedAtUtc, Plan.UpdatedAtUtc);
            if (command.CheckTransition(TradingPlanLoadedState.FromPlan(copy)) is { } failure) return new(failure);
            copy.EditDraft(copy.Version, command.Content, copy.UpdatedAtUtc);
            if (command.ExplicitConfirmations is { } checks) copy.SetConfirmations(copy.Version, checks, copy.UpdatedAtUtc);
            if (command.Operation == TradingPlanWriteOperation.RefreshReferences) copy.RefreshReferences(copy.Version, copy.UpdatedAtUtc);
            Plan = copy; return new(TradingPlanWriteStatus.Success, copy, VerifiedReferences: CaptureReferences?.Invoke(copy.WorkingDraft!.Content));
        }
        public Task<TradingPlan?> LoadAsync(TradingPlanKey key, CancellationToken cancellationToken = default) => Task.FromResult<TradingPlan?>(Plan);
        public Task<TradingPlanReadyRevision?> LoadReadyAsync(TradingPlanRevisionId id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TradingPlanLoadedState>> HistoryAsync(TradingPlanHistoryQuery query, DateOnly? beforeDate = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TradingPlanReadyRevision>> ReadyHistoryAsync(Guid planId, int? beforeRevision = null, int limit = 20, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
