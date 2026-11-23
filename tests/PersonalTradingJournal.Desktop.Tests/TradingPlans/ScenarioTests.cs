using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Desktop.Tests.Journals;
using PersonalTradingJournal.Desktop.ViewModels.TradingPlans;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Desktop.Tests.TradingPlans;

public sealed class ScenarioTests
{
    [Fact]
    public void FirstInvalidFieldFollowsSectionOrderAcrossSharedInput()
    {
        var f = new SessionPreparationTests.Fixture(); using var vm = f.Vm;
        vm.StandAside.Text = new('x', 4001); vm.Scenarios[0].Condition.Text = new('x', 4001);
        vm.News.Text = new('x', 4001); vm.Contexts[0].Thesis.Text = new('x', 4001);
        Assert.Same(vm.Contexts[0].Thesis, vm.FirstInvalidField);
        vm.Contexts[0].Thesis.Text = "corrected"; Assert.Same(vm.News, vm.FirstInvalidField);
        vm.News.Text = "corrected"; Assert.Same(vm.Scenarios[0].Condition, vm.FirstInvalidField);
        vm.Scenarios[0].Condition.Text = "corrected"; Assert.Same(vm.StandAside, vm.FirstInvalidField);
        vm.StandAside.Text = "corrected"; Assert.Null(vm.FirstInvalidField);
    }

    [Fact]
    public async Task SharedRawPreparationAndScenariosSaveTogetherWithoutStaleCopies()
    {
        var f = new SessionPreparationTests.Fixture(); using var vm = f.Vm;
        await vm.LoadCatalogAsync(); vm.Focus.Text = "unsaved focus"; vm.News.Text = "unsaved notes";
        vm.Contexts[0].Thesis.Text = "unsaved thesis"; vm.Contexts[0].Levels[0].Text = "invalid";
        vm.AddScenarioCommand.Execute(null); var row = vm.Scenarios[1]; row.Name.Text = "second"; row.Direction = TradeDirection.Long;
        row.Condition.Text = "IF\nexact input"; row.Response.Text = "THEN"; row.Invalidation.Text = "INVALID";
        Assert.Equal(TradingPlanWriteStatus.Invalid, await vm.SaveAsync()); Assert.Equal(0, f.Store.Writes);
        Assert.Equal("IF\nexact input", row.Condition.Text); Assert.Equal("invalid", vm.Contexts[0].Levels[0].Text);
        vm.Contexts[0].Levels[0].Text = "20123.25";
        Assert.Equal(TradingPlanWriteStatus.Success, await vm.SaveAsync()); Assert.False(vm.IsDirty);
        var stored = f.Store.Plan.WorkingDraft!.Content;
        Assert.Equal("unsaved focus", stored.Focus); Assert.Equal("unsaved notes", stored.NewsAndRestrictions);
        Assert.Equal("unsaved thesis", stored.Instruments[0].MarketThesis); Assert.Equal(20123.25m, stored.Instruments[0].Levels.PreviousDayHigh);
        Assert.Equal(row.Id, stored.Scenarios[1].Id); Assert.Equal(row.Condition.Text, stored.Scenarios[1].Condition);
        using var reopen = new SessionPreparationViewModel(f.Store.Plan, f.Store, f.Catalog);
        Assert.Equal(row.Condition.Text, reopen.Scenarios[1].Condition.Text);
    }

    [Fact]
    public async Task InstrumentSwitchNeverReassignsAndCurrentScenarioRemovalUnblocksContext()
    {
        var f = new SessionPreparationTests.Fixture(); using var vm = f.Vm; await vm.LoadCatalogAsync();
        var first = vm.Scenarios[0]; vm.InstrumentToAdd = vm.AvailableInstruments.Single(i => i.Symbol == "ES"); vm.AddInstrumentCommand.Execute(null);
        var es = vm.SelectedInstrument!; vm.AddScenarioCommand.Execute(null); var second = vm.Scenarios[1];
        Assert.Equal(es.InstrumentId, second.InstrumentId); Assert.Equal(f.Instrument.Id, first.InstrumentId);
        vm.SelectedInstrument = vm.Contexts[0]; Assert.Equal(es.InstrumentId, second.InstrumentId);
        vm.RemoveInstrumentCommand.Execute(null); Assert.Equal(2, vm.Contexts.Count);
        first.InstrumentId = es.InstrumentId; vm.RemoveInstrumentCommand.Execute(null); Assert.Single(vm.Contexts);
        second.InstrumentId = Guid.NewGuid(); Assert.Equal(es.InstrumentId, second.InstrumentId);
        Assert.True(vm.TryBuildContent(out _));
    }

    [Fact]
    public async Task OrderRolesAndSameDirectionAreIndependentAndBounded()
    {
        var f = new SessionPreparationTests.Fixture(); using var vm = f.Vm;
        var first = vm.Scenarios[0]; first.Name.Text = first.Name.Text;
        Assert.Equal(TradingPlanChecklist.None, vm.Confirmed); Assert.False(vm.IsDirty);
        vm.AddScenarioCommand.Execute(null); var second = vm.Scenarios[1]; second.Direction = TradeDirection.Long;
        Assert.Equal(TradingPlanScenarioRole.Alternative, second.Role); second.Role = TradingPlanScenarioRole.Primary;
        second.MoveUpCommand.Execute(null); Assert.Same(second, vm.Scenarios[0]); Assert.False(second.MoveUpCommand.CanExecute(null));
        Assert.False(vm.Confirmed.HasFlag(TradingPlanChecklist.ScenariosOrStandAside)); Assert.False(vm.Confirmed.HasFlag(TradingPlanChecklist.RiskLimits));
        Assert.Equal(TradingPlanWriteStatus.Success, await vm.SaveAsync());
        Assert.Equal(second.Id, f.Store.Plan.WorkingDraft!.Content.Scenarios[0].Id);
        first.MoveDownCommand.Execute(null); Assert.Same(first, vm.Scenarios[1]);
        second.RemoveCommand.Execute(null); Assert.Same(first, Assert.Single(vm.Scenarios));
        for (int i = 1; i < 24; i++) vm.AddScenarioCommand.Execute(null);
        Assert.Equal(24, vm.Scenarios.Count); Assert.False(vm.AddScenarioCommand.CanExecute(null));
        vm.AddScenarioCommand.Execute(null); Assert.Equal(24, vm.Scenarios.Count);
    }

    [Fact]
    public async Task StandAsideRetainsScenariosUntilExplicitRemovalAndReadyUsesEstablishedRules()
    {
        var f = new SessionPreparationTests.Fixture(); using var vm = f.Vm;
        vm.IsStandAside = true; vm.StandAside.Text = "No confirmation today";
        Assert.Single(vm.Scenarios); Assert.Contains("Remove", vm.IntentMessage);
        Assert.Equal(TradingPlanWriteStatus.Success, await vm.SaveAsync());
        Assert.Contains(TradingPlanReadiness.Validate(f.Store.Plan.WorkingDraft!.Content), e => e.Field == "Scenarios");
        vm.Scenarios[0].RemoveCommand.Execute(null); Assert.True(vm.HasNoScenarios);
        vm.Contexts[0].Bias = null; vm.Contexts[0].Thesis.Text = "";
        Assert.Equal(TradingPlanWriteStatus.Success, await vm.SaveAsync());
        var plan = f.Store.Plan; plan.SetConfirmations(plan.Version, TradingPlanChecklist.All, plan.UpdatedAtUtc);
        Assert.Empty(TradingPlanReadiness.Validate(plan.WorkingDraft!.Content));
        var ready = plan.MarkReady(plan.Version, TradingPlanReferenceSnapshot.Capture(plan.Key, plan.WorkingDraft.Content,
            f.Account, [f.Instrument], [], [], plan.UpdatedAtUtc), plan.UpdatedAtUtc);
        using var read = new SessionPreparationViewModel(plan, f.Store, f.Catalog);
        read.IsStandAside = false; read.StandAside.Text = "ignored"; read.AddScenarioCommand.Execute(null);
        Assert.True(read.IsStandAside); Assert.Empty(read.Scenarios); Assert.Equal(ready.Content.StandAside, read.StandAside.Text);
    }

    [Theory]
    [InlineData("Name", 121)] [InlineData("Condition", 4001)] [InlineData("Response", 4001)] [InlineData("Invalidation", 4001)] [InlineData("StandAside", 4001)]
    public async Task OversizeFieldsRetainRawInputAndRecoverWithoutSavingOlderValues(string name, int length)
    {
        var f = new SessionPreparationTests.Fixture(); using var vm = f.Vm;
        var row = vm.Scenarios[0]; var field = name == "StandAside" ? vm.StandAside : (PreparationField)typeof(ScenarioEditor).GetProperty(name)!.GetValue(row)!;
        field.Text = new('x', length); Assert.True(vm.HasValidationErrors);
        Assert.Equal(TradingPlanWriteStatus.Invalid, await vm.SaveAsync()); Assert.Equal(0, f.Store.Writes);
        Assert.Equal(length, field.Text.Length); Assert.Equal(1, vm.ValidationAttempt);
        field.Text = ""; Assert.False(vm.HasValidationErrors);
        Assert.Equal(TradingPlanWriteStatus.Success, await vm.SaveAsync()); // Blank is incomplete Draft, not Ready.
        if (name != "StandAside") Assert.DoesNotContain(TradingPlanReadiness.Validate(f.Store.Plan.WorkingDraft!.Content), e => e.Field.EndsWith("." + name));
    }
}
