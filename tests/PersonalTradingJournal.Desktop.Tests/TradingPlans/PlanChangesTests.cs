using System.Security.Cryptography;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using System.Windows.Media;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Desktop.Screenshots;
using PersonalTradingJournal.Desktop.ViewModels.TradingPlans;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Desktop.Tests.TradingPlans;

public sealed class PlanChangesTests
{
    [Fact]
    public async Task SharedCreateUsesSavedCurrencyBeforeValidatingBlankFormFallback()
    {
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync(seed: true);
        var sourceKey = new TradingPlanKey(null, f.Source.Store.Plan.Key.NewYorkDate);
        var saved = await f.Store.Inner.WriteAsync(new(TradingPlanWriteOperation.CreateDraft, sourceKey,
            f.Source.Content.WithPlanCurrency("EUR")));
        Assert.Equal(TradingPlanWriteStatus.Success, saved.Status);
        using var vm = f.NewWorkspace(); await vm.ActivateAsync();
        await vm.SelectContextAsync(sourceKey.NewYorkDate.AddDays(1), vm.SelectedAccount);
        Assert.Null(vm.SelectedAccount!.Id); Assert.Null(vm.Editor);
        vm.PlanCurrency = ""; await vm.CreateDraftAsync();
        Assert.NotNull(vm.Editor); Assert.Equal("EUR", vm.Editor.PlanCurrency);
        Assert.Equal(f.Source.Content.Focus, vm.Editor.Focus.Text);
        Assert.Equal(sourceKey.NewYorkDate.AddDays(1), vm.Editor.Key.NewYorkDate);
        Assert.NotEqual(saved.Plan!.Id, vm.Editor.CurrentPlan.Id);
        Assert.Empty(vm.Editor.VisibleScreenshots);
    }

    [Fact]
    public async Task MixedAccountCurrenciesRequireExplicitSharedCurrencyWithoutChoosingAnAccount()
    {
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync();
        await f.Db.Provider.GetRequiredService<ITradingAccountStore>().AddAsync(new("Second synthetic",
            PersonalTradingJournal.Domain.Accounts.TradingAccountType.Personal, null, null, "EUR", 0, DateTimeOffset.UtcNow));
        using var vm = f.NewWorkspace(); await vm.ActivateAsync();
        Assert.Null(vm.SelectedAccount!.Id); Assert.Equal("", vm.PlanCurrency); Assert.True(vm.CanCreateDraft);
        await vm.CreateDraftAsync(); // No previous saved Plan can supply the missing global currency.
        Assert.Null(vm.Editor);
        vm.PlanCurrency = "EUR"; Assert.True(vm.CanCreateDraft); await vm.CreateDraftAsync();
        Assert.Equal("EUR", vm.Editor!.CurrentPlan.WorkingDraft!.Content.PlanCurrency); Assert.Null(vm.Editor.Key.AccountId);
    }

    [Fact]
    public async Task InvalidPickerAndUnavailableSavedImagePreserveDraftAndCleanupFailureIsExplicit()
    {
        var f = new SessionPreparationTests.Fixture(); using var vm = f.Vm;
        var images = new Images { Invalid = true }; vm.ConfigureImages(images); await vm.LoadCatalogAsync();
        await vm.UploadScreenshotCommand.ExecuteAsync(null);
        Assert.Empty(vm.VisibleScreenshots); Assert.False(vm.IsDirty); Assert.Contains("valid PNG/JPEG", vm.Message);
        images.Invalid = false; await vm.UploadScreenshotCommand.ExecuteAsync(null);
        f.Store.Result = TradingPlanWriteStatus.Conflict; images.FailCleanup = true;
        Assert.Equal(TradingPlanWriteStatus.Conflict, await vm.SaveAsync());
        Assert.Contains("cleanup could not be verified", vm.Message); Assert.Single(vm.VisibleScreenshots);
        images.FailPreview = true; await vm.VisibleScreenshots[0].OpenCommand.ExecuteAsync(null);
        Assert.Contains("could not be opened", vm.Message); Assert.Single(vm.VisibleScreenshots); Assert.True(vm.IsDirty);
        f.Store.Result = null; Assert.Equal(TradingPlanWriteStatus.Success, await vm.SaveAsync());
        Assert.Equal(1, images.Stores); // The retained staged key is reused, not overwritten.
    }

    [Fact]
    public async Task BlankScenarioSaveReadyAndIndependentTogglesUseRealSqlite()
    {
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync(true); var editor = f.Vm.Editor!;
        var row = editor.Scenarios[0]; var id = row.Id;
        row.Name.Text = row.Condition.Text = row.Response.Text = row.Invalidation.Text = "";
        row.UnlinkCommand.Execute(null);
        row.Direction = TradeDirection.Short; row.Role = TradingPlanScenarioRole.Primary;
        row.ToggleRoleCommand.Execute(null); Assert.Equal(TradeDirection.Short, row.Direction);
        Assert.Equal(TradingPlanScenarioRole.Alternative, row.Role);
        row.ToggleDirectionCommand.Execute(null); Assert.Equal(TradeDirection.Long, row.Direction);
        Assert.Equal(TradingPlanScenarioRole.Alternative, row.Role);
        Assert.False(editor.Confirmed.HasFlag(TradingPlanChecklist.ScenariosOrStandAside));
        editor.StandAside.Text = "Ordinary no-trade conditions coexist with scenarios"; Assert.False(editor.IsStandAside);
        foreach (var check in editor.Checklist) check.IsChecked = true;
        Assert.True(await f.Vm.SaveAsync()); await f.Vm.MarkReadyAsync(); Assert.True(f.Vm.Editor!.IsReadOnly);
        using var restart = f.NewWorkspace(); await restart.ActivateAsync();
        await restart.SelectContextAsync(editor.Key.NewYorkDate, restart.Accounts.Single(a => a.Id == editor.Key.AccountId));
        var saved = Assert.Single(restart.Editor!.Scenarios);
        Assert.Equal(id, saved.Id); Assert.Equal("", saved.Condition.Text); Assert.Equal("", saved.Name.Text);
        Assert.False(saved.ToggleRoleCommand.CanExecute(null)); Assert.False(saved.ToggleDirectionCommand.CanExecute(null)); Assert.False(saved.RemoveCommand.CanExecute(null));
        await restart.StartDraftAsync(); Assert.NotNull(restart.Editor);
        restart.Editor!.Scenarios[0].RemoveCommand.Execute(null); Assert.Empty(restart.Editor.Scenarios);
        Assert.Equal("Ordinary no-trade conditions coexist with scenarios", restart.Editor.StandAside.Text);
    }
    [Fact]
    public async Task SharedDefaultCurrencyScopeGuardAndHistoryCopyAreExplicit()
    {
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync();
        var vm = f.Vm; Assert.Null(vm.SelectedAccount!.Id); Assert.Equal("USD", vm.PlanCurrency);
        await vm.CreateDraftAsync(); Assert.Equal(TradingPlanScope.AllAccounts, vm.Editor!.Key.Scope);
        vm.Editor.Focus.Text = "Retain unsaved shared preparation";
        Assert.False(await vm.SelectContextAsync(vm.SelectedDate, vm.Accounts.Single(a => a.Id == f.Source.Account.Id)));
        Assert.Null(vm.SelectedAccount.Id); Assert.Equal("Retain unsaved shared preparation", vm.Editor.Focus.Text);
        vm.PlanCurrency = "not currency"; Assert.False(await vm.SaveAsync()); Assert.Equal("not currency", vm.PlanCurrency);
        vm.PlanCurrency = "EUR"; Assert.True(await vm.SaveAsync()); Assert.Equal("EUR", vm.Editor.CurrentPlan.WorkingDraft!.Content.PlanCurrency);
        using var restart = f.NewWorkspace(); await restart.ActivateAsync();
        Assert.Equal("EUR", restart.PlanCurrency); Assert.Null(restart.SelectedAccount!.Id);
    }
    [Fact]
    public async Task ScreenshotPickerCancellationIsolationMultipleFramesAndFailedSaveCompensation()
    {
        var f = new SessionPreparationTests.Fixture(); using var vm = f.Vm; var images = new Images();
        vm.ConfigureImages(images); await vm.LoadCatalogAsync();
        images.Cancel = true; await vm.UploadScreenshotCommand.ExecuteAsync(null);
        Assert.Empty(vm.VisibleScreenshots); Assert.False(vm.IsDirty); Assert.Equal(0, images.Stores);
        images.Cancel = false; await vm.UploadScreenshotCommand.ExecuteAsync(null);
        vm.SelectedTimeframe = SessionPreparationViewModel.Timeframes.Single(t => t.Label == "4h");
        await vm.UploadScreenshotCommand.ExecuteAsync(null);
        Assert.Equal(new[] { "1m", "4h" }, vm.VisibleScreenshots.Select(s => s.Label));
        await vm.VisibleScreenshots[0].OpenCommand.ExecuteAsync(null); Assert.Equal(1, images.Previews);
        var first = vm.SelectedInstrument;
        vm.InstrumentToAdd = vm.AvailableInstruments.First(i => i.Id != first!.InstrumentId); vm.AddInstrumentCommand.Execute(null);
        Assert.Empty(vm.VisibleScreenshots); vm.SelectedInstrument = first; Assert.Equal(2, vm.VisibleScreenshots.Count);
        f.Store.Result = TradingPlanWriteStatus.Conflict;
        Assert.Equal(TradingPlanWriteStatus.Conflict, await vm.SaveAsync()); Assert.Equal(2, images.Stores); Assert.Equal(2, images.Cleanups);
        Assert.Equal(2, vm.VisibleScreenshots.Count); Assert.True(vm.IsDirty);
        f.Store.Result = null;
        Assert.Equal(TradingPlanWriteStatus.Success, await vm.SaveAsync()); Assert.Equal(4, images.Stores);
        Assert.Equal(2, f.Store.Plan.WorkingDraft!.Content.Screenshots.Length);
        vm.VisibleScreenshots[0].RemoveCommand.Execute(null); Assert.Single(vm.VisibleScreenshots);
        Assert.Equal(2, images.Cleanups); // Removing a saved reference does not delete historical bytes.
    }
    private sealed class Images : ITradingPlanImages
    {
        public bool Cancel, Invalid, FailCleanup, FailPreview; public int Stores, Cleanups, Previews;
        public Task<PlanImageSelection?> PickAsync(CancellationToken token) => Invalid ? throw new InvalidDataException() : Task.FromResult(Cancel ? null : new PlanImageSelection("synthetic.png", [1, 2, 3], null!));
        public Task<PlaybookExampleImage> StoreAsync(PlanImageSelection selection, CancellationToken token)
        { Stores++; return Task.FromResult(new PlaybookExampleImage(Guid.NewGuid().ToString("N") + ".png", selection.FileName, selection.Bytes.Length, Convert.ToHexString(SHA256.HashData(selection.Bytes)))); }
        public Task<ImageSource> ThumbnailAsync(PlaybookExampleImage image, CancellationToken token) => Task.FromResult<ImageSource>(null!);
        public Task ShowAsync(PlaybookExampleImage? image, PlanImageSelection? pending, string label, CancellationToken token) { Previews++; if (FailPreview) throw new IOException(); return Task.CompletedTask; }
        public Task CleanupAsync(string key) { Cleanups++; if (FailCleanup) throw new IOException(); return Task.CompletedTask; }
    }
}
