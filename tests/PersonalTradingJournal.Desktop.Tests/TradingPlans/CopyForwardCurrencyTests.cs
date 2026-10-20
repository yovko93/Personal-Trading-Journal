using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Desktop.ViewModels.TradingPlans;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Desktop.Tests.TradingPlans;

public sealed class CopyForwardCurrencyTests
{
    [Theory]
    [InlineData("25")]
    [InlineData("0")]
    public async Task HistoryCopyReviewsChangedCurrencyAndRetainsSavedAmountsBeforeReady(string amount)
    {
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync(true);
        f.Vm.Editor!.RiskPerTrade.Text = amount;
        f.Vm.Editor.DailyLossLimit.Text = "100";
        var source = await TradingPlanHistoryTests.Ready(f);
        string frozen = System.Text.Json.JsonSerializer.Serialize(source);
        await Currency(f, "EUR");
        f.Interaction.CurrencyReview = (_, _, _) => true;
        await SelectSource(f);

        await f.Vm.TransferAsync(true);

        var target = await f.Store.Inner.LoadAsync(Target(f));
        Assert.NotNull(target);
        Assert.Equal(("USD", "EUR", "USD"), Assert.Single(f.Interaction.CurrencyPrompts));
        Assert.Equal(source.Content.RiskLimits, target.WorkingDraft!.Content.RiskLimits);
        Assert.Equal("USD", target.WorkingDraft.Content.PlanCurrency);
        Assert.NotEqual(source.Id.PlanId, target.Id);
        Assert.Null(target.LatestReadyRevision);
        Assert.Equal(target.Id, f.Vm.Editor!.CurrentPlan.Id);
        Assert.Contains(f.Vm.Editor.ReadinessBlockers, x => x.Field == "Currency");
        Assert.Contains("saved and verified", f.Vm.Message);
        Assert.Contains("before Ready", f.Vm.Message);
        Assert.False(f.Vm.IsHistorySelected);
        Assert.Equal(frozen, System.Text.Json.JsonSerializer.Serialize(await f.Store.Inner.LoadReadyAsync(source.Id)));
        var ready = await f.Store.Inner.WriteAsync(new(TradingPlanWriteOperation.MarkReady, target.Key,
            target.WorkingDraft.Content, new(target.Id, target.Version)));
        Assert.Equal(TradingPlanWriteStatus.ReferenceChanged, ready.Status);
        Assert.Null((await f.Store.Inner.LoadAsync(target.Key))!.LatestReadyRevision);
    }

    [Fact]
    public async Task CancelCurrencyReviewRetainsPendingInputAndCreatesNothing()
    {
        await using var f = await MonetarySource();
        await f.Vm.StartDraftAsync();
        var editor = f.Vm.Editor!;
        editor.RiskPerTrade.Text = "invalid retained amount";
        editor.Focus.Text = "Unsaved Unicode — запази";
        var token = editor.Token;
        f.Interaction.Choice = PlanLeaveChoice.Discard;
        await Currency(f, "EUR"); await SelectSource(f);
        int writes = f.Store.Writes;

        await f.Vm.TransferAsync(true); // Currency confirmation defaults to Cancel.

        Assert.Single(f.Interaction.CurrencyPrompts);
        Assert.Equal(writes, f.Store.Writes);
        Assert.Null(await f.Store.Inner.LoadAsync(Target(f)));
        Assert.Same(editor, f.Vm.Editor); Assert.Equal(token, editor.Token);
        Assert.Equal("invalid retained amount", editor.RiskPerTrade.Text);
        Assert.Equal("Unsaved Unicode — запази", editor.Focus.Text);
        Assert.True(f.Vm.HasHistoricalViewer); Assert.False(f.Vm.HasLeaveConsent);
        Assert.Contains("Currency review cancelled", f.Vm.HistoryMessage);
        Assert.Equal(PlanNoticeKind.Info, f.Vm.HistoryNotice.Kind);
    }

    [Fact]
    public async Task CurrencyChangesAfterReviewAreRejectedTransactionallyAndRetryReviewsFreshContext()
    {
        await using var f = await MonetarySource();
        await Currency(f, "EUR"); await SelectSource(f);
        f.Interaction.CurrencyReview = (_, _, _) => true;
        TradingPlanWriteStatus? status = null;
        f.Store.BeforeWrite = _ => Currency(f, "GBP");
        f.Store.AfterWrite = result => { status = result.Status; return result; };
        var editor = f.Vm.Editor;
        await f.Vm.TransferAsync(true);
        Assert.Equal(TradingPlanWriteStatus.ReferenceChanged, status);
        Assert.Null(await f.Store.Inner.LoadAsync(Target(f)));
        Assert.Same(editor, f.Vm.Editor); Assert.True(f.Vm.HasHistoricalViewer);
        Assert.Contains("changed again", f.Vm.HistoryMessage);
        Assert.DoesNotContain("existing Draft", f.Vm.HistoryMessage);
        Assert.NotEqual(PlanNoticeKind.Success, f.Vm.HistoryNotice.Kind);

        f.Store.BeforeWrite = null;
        await f.Vm.TransferAsync(true);
        Assert.Equal(new[] { ("USD", "EUR", "USD"), ("USD", "GBP", "USD") }, f.Interaction.CurrencyPrompts);
        Assert.Equal(TradingPlanWriteStatus.Success, status);
        Assert.Equal("USD", (await f.Store.Inner.LoadAsync(Target(f)))!.WorkingDraft!.Content.PlanCurrency);
    }

    [Theory]
    [InlineData(false, "USD")]
    [InlineData(true, "USD")]
    [InlineData(false, "EUR")]
    public async Task UnchangedCurrencyOrNoMonetaryLimitsCopiesWithoutExtraConfirmation(bool monetary, string currency)
    {
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync(true);
        if (monetary) f.Vm.Editor!.DailyLossLimit.Text = "0"; // Explicit zero still counts.
        var source = await TradingPlanHistoryTests.Ready(f);
        if (currency != "USD") await Currency(f, currency);
        await SelectSource(f); await f.Vm.TransferAsync(true);
        Assert.Empty(f.Interaction.CurrencyPrompts);
        var target = (await f.Store.Inner.LoadAsync(Target(f)))!;
        var expected = source.Content.CopyForward().WithPlanCurrency(source.Content.PlanCurrency ?? (monetary ? source.References.Account.Currency : null));
        Assert.True(SessionPreparationViewModel.SameContent(expected, target.WorkingDraft!.Content));
        Assert.Equal(target.Id, f.Vm.Editor!.CurrentPlan.Id);
    }

    [Fact]
    public async Task DailyZeroLimitRequiresCurrencyReviewAndStoreStillRejectsUnreviewedCopy()
    {
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync(true);
        f.Vm.Editor!.DailyLossLimit.Text = "0";
        var source = await TradingPlanHistoryTests.Ready(f);
        await Currency(f, "EUR");
        Assert.Equal(TradingPlanWriteStatus.ReferenceChanged, (await f.Store.Inner.WriteAsync(new(
            TradingPlanWriteOperation.CopyForward, Target(f), source.Content, sourceRevision: source.Id))).Status);
        Assert.Null(await f.Store.Inner.LoadAsync(Target(f)));
        await SelectSource(f); await f.Vm.TransferAsync(true);
        Assert.Single(f.Interaction.CurrencyPrompts);
        Assert.Null(await f.Store.Inner.LoadAsync(Target(f)));
    }

    [Fact]
    public async Task AllAccountsMonetaryCopyIgnoresIndividualAccountCurrencyChanges()
    {
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync();
        await f.Vm.CreateDraftAsync();
        f.Vm.Editor!.InstrumentToAdd = f.Vm.Editor.AvailableInstruments.Single();
        f.Vm.Editor.AddInstrumentCommand.Execute(null);
        f.Vm.Editor!.Intent = TradingPlanIntent.StandAside;
        f.Vm.Editor.StandAside.Text = "Synthetic stand aside";
        f.Vm.Editor.Session = TradingPlanSession.NewYork;
        f.Vm.Editor.RiskPerTrade.Text = "25";
        var source = await TradingPlanHistoryTests.Ready(f);
        Assert.Null(source.Key.AccountId);
        await Currency(f, "EUR");
        f.Vm.SelectTab(true); f.Vm.HistoryAccount = f.Vm.Accounts.Single(a => a.Id is null);
        f.Vm.HistoryFrom = "01 Oct 2026"; f.Vm.HistoryTo = "31 Oct 2026";
        await TradingPlanHistoryTests.View(f.Vm, 1); f.Vm.CopyDate = "20 Oct 2026";
        await f.Vm.TransferAsync(true);
        Assert.Empty(f.Interaction.CurrencyPrompts);
        var target = (await f.Store.Inner.LoadAsync(new(null, new(2026, 10, 20))))!;
        Assert.True(SessionPreparationViewModel.SameContent(source.Content.CopyForward(), target.WorkingDraft!.Content));
        Assert.Equal(target.Id, f.Vm.Editor!.CurrentPlan.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrInactiveExactAccountCannotBeConfirmed(bool delete)
    {
        await using var f = await MonetarySource();
        await SelectSource(f);
        if (delete)
            Assert.Equal(DeleteTradingAccountResult.Deleted, await new DeleteTradingAccountUseCase(
                f.Db.Provider.GetRequiredService<ITradingAccountStore>(),
                f.Db.Provider.GetRequiredService<ITradingAccountDeletionStore>()).ExecuteAsync(f.Source.Account.Id));
        else
        {
            f.Source.Account.Deactivate(DateTimeOffset.UtcNow);
            await f.Db.Provider.GetRequiredService<ITradingAccountStore>().UpdateAsync(f.Source.Account);
        }
        f.Interaction.CurrencyReview = (_, _, _) => true;
        int writes = f.Store.Writes;
        await f.Vm.TransferAsync(true);
        Assert.Equal(writes, f.Store.Writes); Assert.Empty(f.Interaction.CurrencyPrompts);
        Assert.Null(await f.Store.Inner.LoadAsync(Target(f)));
        Assert.Contains("unavailable or inactive", f.Vm.HistoryMessage);
        Assert.True(f.Vm.HasHistoricalViewer);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("dispose")]
    [InlineData("edit")]
    public async Task ModalReentrancyCannotSubmitAfterCancellationDisposalOrChangedEditor(string action)
    {
        await using var f = await MonetarySource();
        await f.Vm.StartDraftAsync();
        await Currency(f, "EUR"); await SelectSource(f);
        Task? cancelling = null;
        f.Interaction.CurrencyReview = (_, _, _) =>
        {
            if (action == "cancel") cancelling = f.Vm.CancelAsync();
            else if (action == "dispose") f.Vm.Dispose();
            else f.Vm.Editor!.Focus.Text = "Changed while confirmation was open";
            return true;
        };
        int writes = f.Store.Writes;
        await f.Vm.TransferAsync(true);
        if (cancelling is not null) await cancelling;
        Assert.Equal(writes, f.Store.Writes);
        Assert.Null(await f.Store.Inner.LoadAsync(Target(f)));
        Assert.NotEqual(PlanNoticeKind.Success, f.Vm.HistoryNotice.Kind);
        if (action == "edit") Assert.Equal("Changed while confirmation was open", f.Vm.Editor!.Focus.Text);
    }

    [Fact]
    public async Task LegacyReadyWithoutPlannedCurrencyUsesFrozenCurrencyAndSurvivesRestart()
    {
        await using var f = await MonetarySource();
        var source = f.Vm.Editor!.CurrentPlan.LatestReadyRevision!;
        // This saved Ready has the supported legacy absent-PlanCurrency shape.
        // Inspect it without updating a Ready row or disabling immutability triggers.
        string frozenJson;
        await using (var db = await f.Db.ContextFactory.CreateDbContextAsync())
        {
            var row = await db.TradingPlanReadyRevisions.SingleAsync();
            frozenJson = row.SnapshotJson;
            Assert.False(System.Text.Json.Nodes.JsonNode.Parse(frozenJson)!["Draft"]!["Content"]!.AsObject().ContainsKey("PlanCurrency"));
        }
        Assert.Null((await f.Store.Inner.LoadReadyAsync(source.Id))!.Content.PlanCurrency);
        await Currency(f, "EUR"); await SelectSource(f);
        f.Interaction.CurrencyReview = (_, _, _) => true;
        await f.Vm.TransferAsync(true);
        Assert.Equal("USD", f.Vm.Editor!.CurrentPlan.WorkingDraft!.Content.PlanCurrency);
        Assert.Contains("saved and verified", f.Vm.Message);
        using var restarted = f.NewWorkspace();
        await restarted.ActivateAsync();
        await restarted.SelectContextAsync(Target(f).NewYorkDate, restarted.Accounts.Single(a => a.Id == f.Source.Account.Id));
        Assert.Equal("USD", restarted.Editor!.PlanCurrency);
        Assert.Equal("25", restarted.Editor.RiskPerTrade.Text);
        Assert.Contains(restarted.Editor.ReadinessBlockers, b => b.Field == "Currency");
        await using var read = await f.Db.ContextFactory.CreateDbContextAsync();
        Assert.Equal(frozenJson, (await read.TradingPlanReadyRevisions.SingleAsync()).SnapshotJson);
    }

    [Fact]
    public async Task CurrencyReviewNeverAllowsOverwriteOrDuplicateSubmissionAndRequiresReadback()
    {
        await using var f = await MonetarySource();
        await Currency(f, "EUR"); await SelectSource(f);
        f.Interaction.CurrencyReview = (_, _, _) =>
        {
            Assert.False(f.Vm.CopyForwardCommand.CanExecute(null));
            Assert.True(f.Vm.TransferAsync(true).IsCompletedSuccessfully); // Reentrant submission is rejected.
            f.Vm.CopyDate = "21 Oct 2026"; // Guarded setter must retain the captured target.
            return true;
        };
        var competing = await f.Store.Inner.WriteAsync(new(TradingPlanWriteOperation.CreateDraft, Target(f), new(focus: "Do not overwrite")));
        var before = System.Text.Json.JsonSerializer.Serialize(competing.Plan);
        int writes = f.Store.Writes;
        await f.Vm.TransferAsync(true);
        Assert.Equal(writes + 1, f.Store.Writes); Assert.Single(f.Interaction.CurrencyPrompts);
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(await f.Store.Inner.LoadAsync(Target(f))));
        Assert.True(f.Vm.HasRecoveryTarget); Assert.Contains("already exists", f.Vm.HistoryMessage);

        f.Vm.CopyDate = "22 Oct 2026";
        f.Store.AfterWrite = result => new(result.Status); // Commit succeeded but verified aggregate is absent.
        var old = f.Vm.Editor;
        await f.Vm.TransferAsync(true);
        Assert.NotNull(await f.Store.Inner.LoadAsync(new(f.Source.Account.Id, new(2026, 10, 22))));
        Assert.Same(old, f.Vm.Editor); Assert.Contains("could not be verified", f.Vm.HistoryMessage);
        Assert.Equal(PlanNoticeKind.Error, f.Vm.HistoryNotice.Kind);
    }

    [Fact]
    public void ThemedConfirmationExplainsFrozenAmountsAndHasExplicitContinueCancel()
    {
        var dialogs = new TestDoubles.FakeDialogService { ConfirmationResult = true };
        ITradingPlanInteraction interaction = new Desktop.Views.TradingPlans.WpfTradingPlanInteraction(dialogs);
        Assert.True(interaction.ConfirmCopyForwardCurrency("USD", "EUR", "USD"));
        var request = Assert.IsType<Desktop.Dialogs.ConfirmationDialogRequest>(dialogs.ConfirmationRequest);
        Assert.Contains("Source Ready Account currency: USD", request.Message);
        Assert.Contains("Current Account currency: EUR", request.Message);
        Assert.Contains("saved amounts and planned currency (USD)", request.Message);
        Assert.Contains("No FX conversion", request.Message);
        Assert.Contains("before marking it Ready", request.Message);
        Assert.Equal("Continue", request.ConfirmButtonText); Assert.Equal("Cancel", request.CancelButtonText);
        Assert.False(request.IsDestructive);
    }

    private static async Task<TradingPlanWorkspaceTests.Fixture> MonetarySource()
    {
        var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync(true);
        try
        {
            f.Vm.Editor!.RiskPerTrade.Text = "25";
            await TradingPlanHistoryTests.Ready(f);
            return f;
        }
        catch { await f.DisposeAsync(); throw; }
    }

    private static TradingPlanKey Target(TradingPlanWorkspaceTests.Fixture f) => new(f.Source.Account.Id, new(2026, 10, 20));
    private static async Task SelectSource(TradingPlanWorkspaceTests.Fixture f)
    {
        TradingPlanHistoryTests.Filters(f.Vm);
        await TradingPlanHistoryTests.View(f.Vm, 1);
        f.Vm.CopyDate = "20 Oct 2026";
    }
    private static async Task Currency(TradingPlanWorkspaceTests.Fixture f, string currency)
    {
        var a = f.Source.Account;
        a.UpdateDetails(a.Name, a.AccountType, null, null, currency, a.StartingBalance, DateTimeOffset.UtcNow);
        await f.Db.Provider.GetRequiredService<ITradingAccountStore>().UpdateAsync(a);
    }
}
