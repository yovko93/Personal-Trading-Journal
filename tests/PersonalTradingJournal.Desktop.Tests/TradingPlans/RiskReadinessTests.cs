using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Desktop.Tests.Journals;
using PersonalTradingJournal.Desktop.ViewModels.TradingPlans;
using PersonalTradingJournal.Domain.Rules;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Desktop.Tests.TradingPlans;

public sealed class RiskReadinessTests
{
    [Theory]
    [InlineData("en-US", "123.12345678901234567890123456", true)] [InlineData("bg-BG", "123,12345678901234567890123456", true)]
    [InlineData("de-DE", "1,25", true)] [InlineData("de-DE", "1.25", true)] [InlineData("en-US", "0", true)] [InlineData("en-US", "", true)]
    [InlineData("en-US", "79228162514264337593543950335", true)] [InlineData("en-US", "79228162514264337593543950336", false)]
    [InlineData("en-US", "0.12345678901234567890123456789", false)] [InlineData("en-US", "1,000", false)]
    [InlineData("en-US", "-1", false)] [InlineData("en-US", "unknown", false)]
    public async Task MoneyPreservesCultureExactValuesNullZeroAndMalformedText(string culture, string raw, bool valid)
    {
        using var f = new Fixture(culture); var vm = f.Vm; vm.RiskPerTrade.Text = raw;
        Assert.Equal(!valid, vm.RiskPerTrade.IsInvalid); Assert.Equal(raw, vm.RiskPerTrade.Text);
        Assert.Equal(valid ? TradingPlanWriteStatus.Success : TradingPlanWriteStatus.Invalid, await vm.SaveAsync());
        Assert.Equal(valid ? 1 : 0, f.Base.Store.Writes); Assert.Equal(raw, vm.RiskPerTrade.Text);
        if (valid)
        {
            decimal? expected = raw == "" ? null : decimal.Parse(raw.Replace(',', '.'), CultureInfo.InvariantCulture);
            Assert.Equal(expected, f.Base.Store.Plan.WorkingDraft!.Content.RiskLimits.RiskPerTrade);
            using var reopen = new SessionPreparationViewModel(f.Base.Store.Plan, f.Base.Store, f.Base.Catalog, CultureInfo.GetCultureInfo(culture));
            Assert.Equal(expected?.ToString(CultureInfo.GetCultureInfo(culture)) ?? "", reopen.RiskPerTrade.Text);
        }
        else { Assert.True(vm.HasValidationErrors); Assert.False(vm.IsPreparationComplete); Assert.Contains("Malformed", vm.ReadinessSummary); }
    }
    [Theory]
    [InlineData("0", true, false)] [InlineData("1", true, true)] [InlineData("2147483647", true, true)] [InlineData("", true, true)]
    [InlineData("-1", false, false)] [InlineData("1.0", false, false)] [InlineData("1,5", false, false)] [InlineData("2147483648", false, false)] [InlineData("1e2", false, false)]
    public async Task CountsUseExactIntegerBoundsWithoutTruncation(string text, bool maximumValid, bool consecutiveValid)
    {
        using var f = new Fixture(); var vm = f.Vm;
        vm.MaximumTrades.Text = text; vm.ConsecutiveLosses.Text = text;
        Assert.Equal(!maximumValid, vm.MaximumTrades.IsInvalid); Assert.Equal(!consecutiveValid, vm.ConsecutiveLosses.IsInvalid);
        Assert.Equal(maximumValid && consecutiveValid ? TradingPlanWriteStatus.Success : TradingPlanWriteStatus.Invalid, await vm.SaveAsync());
        Assert.Equal(text, vm.MaximumTrades.Text); Assert.Equal(text, vm.ConsecutiveLosses.Text);
        vm.MaximumTrades.Text = "0"; vm.ConsecutiveLosses.Text = "";
        Assert.False(vm.HasValidationErrors); Assert.Equal(TradingPlanWriteStatus.Success, await vm.SaveAsync());
        Assert.Equal(0, f.Base.Store.Plan.WorkingDraft!.Content.RiskLimits.MaximumTrades);
        Assert.Null(f.Base.Store.Plan.WorkingDraft.Content.RiskLimits.StopAfterConsecutiveLosses);
    }
    [Fact]
    public async Task ExplicitChecksPersistTogetherWithAllSectionsAndTargetedEditsDoNotReconfirm()
    {
        using var f = new Fixture(); var vm = f.Vm; await vm.CheckReferencesAsync();
        vm.Focus.Text = "unsaved focus"; vm.Scenarios[0].Condition.Text = "unsaved IF"; vm.DailyLossLimit.Text = "150.250000000000000000001";
        Assert.False(vm.Checklist.Single(c => c.Flag == TradingPlanChecklist.MindsetAndFocus).IsChecked);
        Assert.False(vm.Checklist.Single(c => c.Flag == TradingPlanChecklist.ScenariosOrStandAside).IsChecked);
        Assert.False(vm.Checklist.Single(c => c.Flag == TradingPlanChecklist.RiskLimits).IsChecked);
        Assert.True(vm.Checklist.Single(c => c.Flag == TradingPlanChecklist.News).IsChecked);
        foreach (var item in vm.Checklist) item.IsChecked = true;
        Assert.Equal(5, vm.CompletedConfirmations); Assert.Equal(100, vm.ConfirmationProgress); Assert.False(vm.IsPreparationComplete);
        Assert.Equal(0, f.Base.Store.Writes); var token = vm.Token;
        Assert.Equal(TradingPlanWriteStatus.Success, await vm.SaveAsync()); Assert.True(vm.IsPreparationComplete);
        Assert.Equal(TradingPlanChecklist.All, f.Base.Store.Plan.WorkingDraft!.Content.Confirmed); Assert.True(vm.Token.Version > token.Version);
        vm.DailyLossLimit.Text = "150.2500000000000000000010"; // Same numeric meaning, same check.
        Assert.Contains("Unsaved", vm.Message); Assert.Contains("unsaved", vm.StateLabel);
        Assert.Equal(TradingPlanChecklist.All, vm.Confirmed); Assert.Equal(TradingPlanWriteStatus.Success, await vm.SaveAsync());
        vm.Contexts[0].Levels[0].Text = "malformed";
        Assert.False(vm.Confirmed.HasFlag(TradingPlanChecklist.ContextAndLevels)); Assert.False(vm.Confirmed.HasFlag(TradingPlanChecklist.ScenariosOrStandAside));
        Assert.True(vm.Confirmed.HasFlag(TradingPlanChecklist.RiskLimits));
        vm.Contexts[0].Levels[0].Text = "0"; Assert.False(vm.Confirmed.HasFlag(TradingPlanChecklist.ContextAndLevels));
        foreach (var item in vm.Checklist) item.IsChecked = true;
        vm.News.Text = "news change";
        Assert.Equal(TradingPlanChecklist.All & ~(TradingPlanChecklist.News | TradingPlanChecklist.ScenariosOrStandAside), vm.Confirmed);
        vm.Session = TradingPlanSession.London; Assert.Equal(TradingPlanChecklist.None, vm.Confirmed);
    }
    [Fact]
    public async Task ReadinessIsPureAndDistinguishesPartialDraftMalformedReferencesAndConflict()
    {
        using var f = new Fixture(); var vm = f.Vm;
        Assert.False(vm.IsPreparationComplete); Assert.Contains(vm.ReadinessBlockers, b => b.Field == "References");
        await vm.CheckReferencesAsync(); Assert.True(vm.IsPreparationComplete);
        var token = vm.Token; for (int i = 0; i < 3; i++) { _ = vm.ReadinessBlockers; _ = vm.ReadinessSummary; _ = vm.ConfirmationProgress; }
        Assert.Equal(token, vm.Token); Assert.Equal(0, f.Base.Store.Writes);
        vm.Scenarios[0].Direction = null; Assert.False(vm.HasValidationErrors); Assert.Contains(vm.ReadinessBlockers, b => b.Field.EndsWith(".Direction"));
        Assert.Equal(TradingPlanWriteStatus.Success, await vm.SaveAsync()); // Incomplete valid Draft.
        vm.MaximumTrades.Text = "1.5"; Assert.True(vm.HasValidationErrors); Assert.Contains(vm.ReadinessBlockers, b => b.Message.Contains("whole number"));
        Assert.Equal(TradingPlanWriteStatus.Invalid, await vm.SaveAsync()); Assert.Same(vm.MaximumTrades, vm.FirstInvalidField);
        vm.MaximumTrades.Text = ""; f.Base.Store.Result = TradingPlanWriteStatus.Conflict;
        Assert.Equal(TradingPlanWriteStatus.Conflict, await vm.SaveAsync());
        Assert.Contains(vm.ReadinessBlockers, b => b.Field == "Conflict"); vm.Focus.Text = "not a conflict resolution";
        Assert.Contains(vm.ReadinessBlockers, b => b.Field == "Conflict");
    }
    [Fact]
    public async Task CurrencyChangeNeverRelabelsPendingAmountsAndRefreshClearsAllChecks()
    {
        using var f = new Fixture(); var vm = f.Vm;
        await vm.CheckReferencesAsync(); vm.RiskPerTrade.Text = "25.12345678901234567890123456";
        Assert.Equal(TradingPlanWriteStatus.Success, await vm.SaveAsync()); Assert.Contains("USD", vm.CurrencyLabel);
        f.References.Status = TradingPlanWriteStatus.ReferenceChanged; f.References.CurrentCurrency = "EUR";
        await vm.CheckReferencesAsync(); Assert.Contains("USD", vm.CurrencyLabel); Assert.Contains("EUR", vm.CurrencyMessage);
        Assert.False(vm.CurrencyVerified); Assert.True(vm.RequiresReferenceRefresh); Assert.False(vm.IsPreparationComplete);
        vm.Focus.Text = "retain pending preparation";
        f.Base.Store.Result = TradingPlanWriteStatus.ReferenceChanged; Assert.Equal(TradingPlanWriteStatus.ReferenceChanged, await vm.SaveAsync());
        Assert.Equal("25.12345678901234567890123456", vm.RiskPerTrade.Text); Assert.Equal("retain pending preparation", vm.Focus.Text);
        f.Base.Store.Result = null; f.References.Status = TradingPlanWriteStatus.Success;
        f.Base.Account.UpdateDetails(f.Base.Account.Name, f.Base.Account.AccountType, null, null, "EUR", f.Base.Account.StartingBalance, Fixture.Now);
        Assert.Equal(TradingPlanWriteStatus.Success, await vm.RefreshReferencesAsync()); Assert.Contains("EUR", vm.CurrencyLabel);
        Assert.Equal(TradingPlanChecklist.None, vm.Confirmed); Assert.Equal("25.12345678901234567890123456", vm.RiskPerTrade.Text);
        Assert.Equal("EUR", f.References.Snapshot(vm.TryBuildContent(out var current) ? current! : throw new InvalidOperationException()).Account.Currency);
    }
    [Fact]
    public async Task RulesAreBoundedPagedGlobalExplicitAndNeverWriteTheirCatalog()
    {
        using var f = new Fixture(ruleCount: 105); var vm = f.Vm; await vm.LoadRulesAsync();
        Assert.Equal(2, f.Rules.CatalogReads); Assert.Equal(105, vm.RuleChoices.Count);
        vm.RuleCandidate = vm.RuleChoices[0]; Assert.Empty(vm.LinkedRules); await vm.LinkRuleAsync();
        Assert.Single(vm.LinkedRules); Assert.Contains("Rule 000", vm.LinkedRules[0].Text); Assert.False(vm.Confirmed.HasFlag(TradingPlanChecklist.RiskLimits));
        await vm.LinkRuleAsync(); Assert.Single(vm.LinkedRules);
        Guid? navigation = null; vm.RuleNavigation = (id, _) => { navigation = id; return Task.FromResult(false); };
        vm.Focus.Text = "unsaved navigation"; vm.MaximumTrades.Text = "invalid";
        await vm.LinkedRules[0].OpenCommand.ExecuteAsync(null); Assert.Equal(vm.LinkedRules[0].Link.RuleId, navigation);
        Assert.Equal("unsaved navigation", vm.Focus.Text); Assert.Equal("invalid", vm.MaximumTrades.Text);
        var old = vm.LinkedRules[0].Link; f.Rules.Revise(old.RuleId);
        await vm.LoadRulesAsync(); Assert.Equal(old, vm.LinkedRules[0].Link); Assert.Contains("Criterion 000", vm.LinkedRules[0].Text);
        Assert.True(vm.RequiresReferenceRefresh); Assert.Contains("changed", vm.LinkedRules[0].Text);
        vm.MaximumTrades.Text = ""; vm.RuleCandidate = vm.RuleChoices[0]; await vm.LinkRuleAsync();
        Assert.Single(vm.LinkedRules); Assert.Equal(2, vm.LinkedRules[0].Link.ExpectedRevision); Assert.Contains("revised criterion", vm.LinkedRules[0].Text);
        f.Rules.Archive(old.RuleId); await vm.LoadRulesAsync(); Assert.False(vm.LinkedRules[0].OpenCommand.CanExecute(null));
        vm.LinkedRules[0].RemoveCommand.Execute(null); Assert.Empty(vm.LinkedRules); Assert.Equal("0 linked Rules", vm.LinkedRuleCount);
        Assert.Equal(0, f.Rules.Writes);
    }
    [Fact]
    public async Task CatalogCancellationChangesAndLimitsNeverPublishPartialChoices()
    {
        using var f = new Fixture(ruleCount: 105); var vm = f.Vm;
        f.Rules.ChangeCatalogOnSecondPage = true; await vm.LoadRulesAsync(); Assert.Empty(vm.RuleChoices); Assert.Contains("changed", vm.Message);
        f.Rules.ChangeCatalogOnSecondPage = false; await vm.LoadRulesAsync(); var prior = vm.RuleChoices;
        using var ct = new CancellationTokenSource(); f.Rules.CancelSecondPage = ct;
        await vm.LoadRulesAsync(ct.Token); Assert.Same(prior, vm.RuleChoices); Assert.Contains("cancelled", vm.Message);
        f.Rules.CancelSecondPage = null; f.Rules.ClaimedCount = 1001; await vm.LoadRulesAsync();
        Assert.Same(prior, vm.RuleChoices); Assert.Contains("1000", vm.Message); Assert.False(vm.IsBusy);
    }
    [Fact]
    public async Task UnlinkRetainsInvalidDraftInputCatalogAndFrozenReadyDefinition()
    {
        using var f = new Fixture(); var vm = f.Vm;
        await vm.LoadRulesAsync(); vm.RuleCandidate = vm.RuleChoices[0]; await vm.LinkRuleAsync();
        await vm.CheckReferencesAsync(); foreach (var item in vm.Checklist) item.IsChecked = true;
        Assert.Equal(TradingPlanWriteStatus.Success, await vm.SaveAsync());
        var saved = f.Base.Store.Plan;
        var historical = TradingPlan.Rehydrate(saved.Id, saved.Key, saved.Version, saved.WorkingDraft, saved.LatestReadyRevision, saved.CreatedAtUtc, saved.UpdatedAtUtc);
        var ready = historical.MarkReady(historical.Version, f.References.Snapshot(saved.WorkingDraft!.Content), Fixture.Now);
        var rule = Assert.Single(ready.References.Rules);
        var catalog = f.Rules.History.ToArray();
        // Existing editor still has its independent working copy; no persistence occurs on unlink.
        vm.Focus.Text = "retained focus"; vm.MaximumTrades.Text = "invalid count";
        var confirmations = vm.Confirmed; var writes = f.Base.Store.Writes;
        vm.LinkedRules[0].RemoveCommand.Execute(null);
        Assert.Empty(vm.LinkedRules); Assert.Equal("retained focus", vm.Focus.Text); Assert.Equal("invalid count", vm.MaximumTrades.Text);
        Assert.Equal(confirmations & ~TradingPlanChecklist.RiskLimits, vm.Confirmed);
        Assert.Equal(writes, f.Base.Store.Writes); Assert.Equal(0, f.Rules.Writes); Assert.Equal(catalog, f.Rules.History);
        Assert.Equal(rule, Assert.Single(ready.References.Rules)); Assert.Single(ready.Content.Rules);
    }
    [Fact]
    public async Task StandAsideOnlyAndFrozenReadyUseSavedLimitsCurrencyChecksAndRules()
    {
        using var f = new Fixture(); var vm = f.Vm; await vm.LoadRulesAsync(); vm.RuleCandidate = vm.RuleChoices[0]; await vm.LinkRuleAsync();
        vm.IsStandAside = true; vm.StandAside.Text = "No confirmed opportunity";
        vm.Scenarios[0].RemoveCommand.Execute(null); vm.Contexts[0].Bias = null; vm.Contexts[0].Thesis.Text = "";
        await vm.CheckReferencesAsync(); foreach (var item in vm.Checklist) item.IsChecked = true;
        Assert.Equal(TradingPlanWriteStatus.Success, await vm.SaveAsync()); Assert.True(vm.IsPreparationComplete);
        var plan = f.Base.Store.Plan; var ready = plan.MarkReady(plan.Version, f.References.Snapshot(plan.WorkingDraft!.Content), Fixture.Now);
        f.Base.Account.UpdateDetails(f.Base.Account.Name, f.Base.Account.AccountType, null, null, "EUR", f.Base.Account.StartingBalance, Fixture.Now);
        f.Rules.Revise(vm.LinkedRules[0].Link.RuleId);
        using var read = new SessionPreparationViewModel(plan, f.Base.Store, f.Base.Catalog);
        read.ConfigureRules(f.Rules, f.Rules); await read.LoadRulesAsync();
        Assert.Contains("USD", read.CurrencyLabel); Assert.Contains("Criterion 000", read.LinkedRules[0].Text); Assert.DoesNotContain("revised criterion", read.LinkedRules[0].Text);
        Assert.Contains("saved", read.LinkedRules[0].Text); Assert.Equal(5, read.CompletedConfirmations);
        read.RiskPerTrade.Text = "999"; foreach (var item in read.Checklist) item.IsChecked = false; read.LinkedRules[0].RemoveCommand.Execute(null);
        Assert.Equal("", read.RiskPerTrade.Text); Assert.Equal(TradingPlanChecklist.All, read.Confirmed); Assert.Single(read.LinkedRules);
        Assert.Equal("USD", ready.References.Account.Currency); Assert.False(read.IsPreparationComplete);
    }
    [Fact]
    public async Task ReadbackMismatchNeverReportsSuccessAndKeepsPendingAllSections()
    {
        using var f = new Fixture(); using var vm = new SessionPreparationViewModel(f.Base.Store.Plan, new OldReadback(f.Base.Store.Plan), f.Base.Catalog);
        vm.Focus.Text = "new focus"; vm.Scenarios[0].Condition.Text = "new condition"; vm.MaximumTrades.Text = "7";
        var token = vm.Token; Assert.Equal(TradingPlanWriteStatus.StorageFailure, await vm.SaveAsync());
        Assert.Equal(token, vm.Token); Assert.Equal("new focus", vm.Focus.Text); Assert.Equal("new condition", vm.Scenarios[0].Condition.Text);
        Assert.Equal("7", vm.MaximumTrades.Text); Assert.Contains("could not be saved", vm.Message);
    }
    [Fact]
    public async Task UnexpectedSaveFailureBlocksPreviouslyCompleteReadinessUntilVerifiedSave()
    {
        using var f = new Fixture(); var vm = f.Vm;
        await vm.CheckReferencesAsync(); foreach (var item in vm.Checklist) item.IsChecked = true;
        Assert.Equal(TradingPlanWriteStatus.Success, await vm.SaveAsync()); Assert.True(vm.IsPreparationComplete);
        var token = vm.Token;
        f.Base.Store.Block = _ => throw new InvalidOperationException("Synthetic storage failure");
        Assert.Equal(TradingPlanWriteStatus.StorageFailure, await vm.SaveAsync());
        Assert.Equal(token, vm.Token); Assert.False(vm.IsPreparationComplete);
        Assert.Contains(vm.ReadinessBlockers, b => b.Field == "Save");
        Assert.Equal(TradingPlanChecklist.All, vm.Confirmed); Assert.Contains("could not be saved", vm.Message);
        f.Base.Store.Block = null;
        Assert.Equal(TradingPlanWriteStatus.Success, await vm.SaveAsync()); Assert.True(vm.IsPreparationComplete);
    }
    [Fact]
    public async Task CancelledSaveKeepsStagedChecksAndRejectsOverlappingSubmission()
    {
        using var f = new Fixture(); var vm = f.Vm; await vm.CheckReferencesAsync(); vm.DailyLossLimit.Text = "25";
        foreach (var item in vm.Checklist) item.IsChecked = true;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Base.Store.Block = async ct => { entered.SetResult(); await Task.Delay(Timeout.Infinite, ct); };
        var pending = vm.SaveAsync(); await entered.Task;
        Assert.True(vm.IsBusy); Assert.False(vm.Checklist[0].CanInteract);
        vm.Checklist[0].IsChecked = false; vm.DailyLossLimit.Text = "ignored while saving";
        Assert.Equal(TradingPlanWriteStatus.Invalid, await vm.SaveAsync()); Assert.Equal(1, f.Base.Store.Writes);
        vm.CancelCommand.Execute(null); Assert.Equal(TradingPlanWriteStatus.Cancelled, await pending);
        Assert.Equal("25", vm.DailyLossLimit.Text); Assert.Equal(TradingPlanChecklist.All, vm.Confirmed); Assert.True(vm.IsDirty);
        Assert.Null(f.Base.Store.Plan.WorkingDraft!.Content.RiskLimits.DailyLossLimit);
    }
    [Fact]
    public async Task RuleSelectionRechecksRevisionAndCapsLinksWithoutChangingOtherInput()
    {
        using var f = new Fixture(ruleCount: 51); var vm = f.Vm; await vm.LoadRulesAsync();
        vm.RuleCandidate = vm.RuleChoices[0]; f.Rules.Revise(vm.RuleCandidate.Id); vm.Focus.Text = "retained selection draft";
        await vm.LinkRuleAsync(); Assert.Empty(vm.LinkedRules); Assert.Contains("changed", vm.Message);
        await vm.LoadRulesAsync();
        foreach (var choice in vm.RuleChoices.Take(50)) { vm.RuleCandidate = choice; await vm.LinkRuleAsync(); }
        Assert.Equal(50, vm.LinkedRules.Count); vm.RuleCandidate = vm.RuleChoices[50]; await vm.LinkRuleAsync();
        Assert.Equal(50, vm.LinkedRules.Count); Assert.Contains("50", vm.Message); Assert.Equal("retained selection draft", vm.Focus.Text);
        Assert.Equal(0, f.Rules.Writes); Assert.Equal(0, f.Base.Store.Writes);
    }
    [Fact]
    public async Task RealSqliteSharedChecklistRuleAndCurrencyContextSurviveRestartAndStaleWrites()
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync(); using var f = new Fixture();
        await db.Provider.GetRequiredService<ITradingAccountStore>().AddAsync(f.Base.Account);
        await db.Provider.GetRequiredService<IInstrumentStore>().AddAsync(f.Base.Instrument);
        var ruleStore = db.Provider.GetRequiredService<ITradingRuleStore>(); Assert.True(await ruleStore.TryAppendAsync(f.Rules.Revisions[0], 0));
        var store = db.Provider.GetRequiredService<ITradingPlanStore>();
        var create = await store.WriteAsync(new(TradingPlanWriteOperation.CreateDraft, f.Base.Store.Plan.Key, f.Base.Content));
        using var vm = new SessionPreparationViewModel(create.Plan!, store, f.Base.Catalog);
        vm.ConfigureReferenceReader(db.Provider.GetRequiredService<ITradingPlanReferenceReader>());
        vm.ConfigureRules(db.Provider.GetRequiredService<ITradingRuleReader>(), ruleStore);
        await vm.CheckReferencesAsync(); await vm.LoadRulesAsync(); vm.RuleCandidate = vm.RuleChoices[0]; await vm.LinkRuleAsync();
        vm.Focus.Text = "SQLite shared focus"; vm.Scenarios[0].Condition.Text = "SQLite shared condition"; vm.DailyLossLimit.Text = "10.123456789012345678901234567";
        foreach (var item in vm.Checklist) item.IsChecked = true;
        Assert.Equal(TradingPlanWriteStatus.Success, await vm.SaveAsync()); Assert.True(vm.IsPreparationComplete);
        var loaded = (await store.LoadAsync(vm.Key))!;
        using var reopen = new SessionPreparationViewModel(loaded, store, f.Base.Catalog);
        Assert.Equal(vm.Focus.Text, reopen.Focus.Text); Assert.Equal(vm.DailyLossLimit.Text, reopen.DailyLossLimit.Text);
        Assert.Equal(vm.Scenarios[0].Condition.Text, reopen.Scenarios[0].Condition.Text); Assert.Equal(TradingPlanChecklist.All, reopen.Confirmed); Assert.Single(reopen.LinkedRules);
        vm.Focus.Text = "other editor change"; await vm.SaveAsync(); reopen.Focus.Text = "retain stale text";
        Assert.Equal(TradingPlanWriteStatus.Conflict, await reopen.SaveAsync()); Assert.Equal("retain stale text", reopen.Focus.Text);
    }

    internal sealed class Fixture : IDisposable
    {
        internal static readonly DateTimeOffset Now = SessionPreparationTests.Fixture.Now;
        internal readonly SessionPreparationTests.Fixture Base;
        internal readonly RuleSources Rules;
        internal readonly References References;
        internal SessionPreparationViewModel Vm => Base.Vm;
        internal Fixture(string culture = "en-US", int ruleCount = 2)
        {
            Base = new(culture); Rules = new(ruleCount); References = new(this);
            Base.Store.CaptureReferences = References.Snapshot;
            Vm.ConfigureRules(Rules, Rules); Vm.ConfigureReferenceReader(References);
        }
        public void Dispose() => Vm.Dispose();
    }
    internal sealed class References(Fixture fixture) : ITradingPlanReferenceReader
    {
        internal TradingPlanWriteStatus Status = TradingPlanWriteStatus.Success;
        internal string CurrentCurrency = "USD";
        public Task<TradingPlanReferenceCheck> CheckAsync(TradingPlanKey key, TradingPlanExpectedState expected, TradingPlanContent proposedContent, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new TradingPlanReferenceCheck(Status, key, expected,
                new(key.AccountId, "Synthetic Account", CurrentCurrency, Fixture.Now), Status == TradingPlanWriteStatus.Success,
                Status == TradingPlanWriteStatus.Success ? Snapshot(proposedContent) : null));
        }
        internal TradingPlanReferenceSnapshot Snapshot(TradingPlanContent content) => TradingPlanReferenceSnapshot.Capture(fixture.Vm.Key, content,
            fixture.Base.Account, [fixture.Base.Instrument], [], content.Rules.Select(r => fixture.Rules.History.Single(v => v.RuleId == r.RuleId && v.Revision == r.ExpectedRevision)), Fixture.Now);
    }
    internal sealed class RuleSources : ITradingRuleReader, ITradingRuleStore
    {
        internal readonly List<TradingRuleRevision> Revisions = [];
        internal readonly List<TradingRuleRevision> History = [];
        internal int Writes, CatalogReads;
        internal bool ChangeCatalogOnSecondPage;
        internal int? ClaimedCount;
        internal CancellationTokenSource? CancelSecondPage;
        internal RuleSources(int count)
        { for (int i = 0; i < count; i++) { var r = TradingRuleRevision.Create(Guid.NewGuid(), new($"Rule {i:000}", $"Criterion {i:000}"), Fixture.Now.AddHours(-1)); Revisions.Add(r); History.Add(r); } }
        internal void Revise(Guid id) { int i = Revisions.FindIndex(r => r.RuleId == id); var next = Revisions[i].Revise(Revisions[i].Revision, new(Revisions[i].Definition.Name, "revised criterion"), Fixture.Now); Revisions[i] = next; History.Add(next); }
        internal void Archive(Guid id) { int i = Revisions.FindIndex(r => r.RuleId == id); var next = Revisions[i].Archive(Revisions[i].Revision, Fixture.Now); Revisions[i] = next; History.Add(next); }
        public Task<TradingRulePage> ReadCatalogAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
        {
            CatalogReads++; if (page == 2) CancelSecondPage?.Cancel();
            var active = Revisions.Where(r => r.IsActive && r.Definition.IsGlobal).ToArray();
            return Task.FromResult(new TradingRulePage(active.Skip((page - 1) * pageSize).Take(pageSize).Select(r => new TradingRuleDetails(r, null, true)).ToArray(), ClaimedCount ?? active.Length, page, pageSize, ChangeCatalogOnSecondPage && page == 2 ? 2 : 1));
        }
        public Task<TradingRuleRevision?> GetLatestAsync(Guid ruleId, CancellationToken cancellationToken = default) => Task.FromResult(Revisions.SingleOrDefault(r => r.RuleId == ruleId));
        public Task<IReadOnlyList<TradingRuleDetails>> ReadRevisionsAsync(Guid ruleId, int? beforeRevision = null, int limit = 20, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TradingRuleDetails>>(History.Where(r => r.RuleId == ruleId && (!beforeRevision.HasValue || r.Revision < beforeRevision)).OrderByDescending(r => r.Revision).Take(limit).Select(r => new TradingRuleDetails(r, null, true)).ToArray());
        public Task<bool> TryAppendAsync(TradingRuleRevision candidate, int expectedRevision, CancellationToken cancellationToken = default) { Writes++; throw new InvalidOperationException("Plan editor must not write Rules"); }
        public Task<TradingRulePage> ReadCurrentAsync(TradingRuleAccountScope? scope = null, bool? active = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TradingRuleAssessmentDetails?> ReadAssessmentAsync(Guid assessmentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TradingRuleAssessmentDetails>> ReadAssessmentsAsync(Guid ruleId, TradingRuleAssessmentTarget target, int? beforeSequence = null, int limit = 20, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class OldReadback(TradingPlan old) : ITradingPlanStore
    {
        public Task<TradingPlanWriteResult> WriteAsync(TradingPlanWriteCommand command, CancellationToken cancellationToken = default) => Task.FromResult(new TradingPlanWriteResult(TradingPlanWriteStatus.Success, old));
        public Task<TradingPlan?> LoadAsync(TradingPlanKey key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TradingPlanReadyRevision?> LoadReadyAsync(TradingPlanRevisionId id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TradingPlanLoadedState>> HistoryAsync(TradingPlanHistoryQuery query, DateOnly? beforeDate = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TradingPlanHistoryAccount>> HistoryAccountsAsync(DateOnly firstDate, DateOnly lastDate, int offset = 0, int limit = 21, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TradingPlanReadyRevision>> ReadyHistoryAsync(Guid planId, int? beforeRevision = null, int limit = 20, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
