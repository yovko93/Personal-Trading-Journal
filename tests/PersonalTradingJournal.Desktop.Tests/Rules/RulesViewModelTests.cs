using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Rules;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Rules;

namespace PersonalTradingJournal.Desktop.Tests.Rules;

public sealed class RulesViewModelTests
{
    [Theory]
    [InlineData(0, 121)]
    [InlineData(1, 4001)]
    [InlineData(2, 2001)]
    public async Task OversizedFieldIsPreservedAndClearsItsOwnValidationWhenCorrected(int index, int length)
    {
        var f = new RulesFixture(); await f.Vm.ActivateAsync(); f.Vm.CreateCommand.Execute(null); f.Fill();
        string entered = new('x', length); f.Vm.Fields[index].Text = entered;
        await f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.True(f.Vm.Fields[index].IsInvalid); Assert.Equal(entered, f.Vm.Fields[index].Text); Assert.Empty(f.Data.Rows);
        f.Vm.Fields[index].Text = "Corrected"; Assert.False(f.Vm.Fields[index].IsInvalid);
    }

    [Fact]
    public async Task CreateValidateCorrectSaveAndReadBackAllFieldsAndExactScope()
    {
        var f = new RulesFixture(); await f.Vm.ActivateAsync(); Assert.True(f.Vm.ShowEmpty);
        f.Vm.CreateCommand.Execute(null);
        await f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.All(f.Vm.Fields, field => Assert.True(field.IsInvalid)); Assert.Equal(1, f.Vm.ValidationAttempt);
        Assert.Empty(f.Data.Rows); Assert.True(f.Vm.IsEditing);
        f.Fill(); await f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.False(f.Vm.IsEditing); Assert.Equal("Rule saved.", f.Vm.Message);
        var row = Assert.Single(f.Data.Rows);
        Assert.True(row.Definition.IsGlobal);
        Assert.Null(row.Definition.AccountScope.AccountId);
        Assert.Equal(TradingRuleEvaluationMode.ManualOnly, row.Definition.EvaluationMode);
        Assert.Equal(new[] { "Synthetic rule", "Recorded criterion", "Required records" }, new[] { row.Definition.Name, row.Definition.Criterion, row.Definition.RequiredEvidence });
        Assert.False(f.Vm.HasErrors); Assert.Null(row.Definition.AutomaticCriterion);
    }

    [Theory]
    [InlineData("conflict")]
    [InlineData("failure")]
    [InlineData("cancel")]
    [InlineData("readback")]
    public async Task FailedSaveRetainsEveryFieldAndRecoveryDoesNotPretendSuccess(string failure)
    {
        var f = new RulesFixture(); await f.Vm.ActivateAsync(); f.Vm.CreateCommand.Execute(null); f.Fill();
        f.Data.Failure = failure;
        await f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.True(f.Vm.IsEditing); Assert.Equal("Recorded criterion", f.Vm.Fields[1].Text);
        Assert.Equal("Required records", f.Vm.Fields[2].Text); Assert.DoesNotContain("Saved and verified", f.Vm.Message);
        Assert.Equal(failure == "readback" ? 1 : 0, f.Data.Rows.Count);
        if (failure == "readback") Assert.False(f.Vm.SaveCommand.CanExecute(null));
        f.Data.Failure = null;
        Assert.False(f.Vm.TryLeave());
    }

    [Fact]
    public async Task RefreshAfterUnverifiedCreateRetainsIdentityAndLoadsCommittedRevision()
    {
        var f = new RulesFixture(); await f.Vm.ActivateAsync(); f.Vm.CreateCommand.Execute(null); f.Fill();
        f.Data.Failure = "readback"; await f.Vm.SaveCommand.ExecuteAsync(null);
        f.Data.Failure = null; f.Dialogs.ConfirmationResult = true;
        await f.Vm.RefreshCommand.ExecuteAsync(null);
        Assert.False(f.Vm.IsEditing); Assert.Equal(Assert.Single(f.Data.Rows).RuleId, f.Vm.SelectedRule!.Id);
        Assert.Equal(1, f.Vm.SelectedRule.Details.Revision.Revision);
    }

    [Fact]
    public async Task EditAndDeleteRetainOriginalInternalHistory()
    {
        var f = new RulesFixture(); var id = f.Data.Seed(3); await f.Open(id);
        f.Vm.EditCommand.Execute(null); f.Vm.Fields[1].Text = "pending change";
        Assert.Equal("pending change", f.Vm.Fields[1].Text);
        await f.Vm.SaveCommand.ExecuteAsync(null); Assert.Equal(4, f.Data.Rows.Count);
        await f.Vm.DeleteCommand.ExecuteAsync(f.Vm.SelectedRule); Assert.Equal(4, f.Data.Rows.Count); // confirmation declined
        f.Dialogs.ConfirmationResult = true; await f.Vm.DeleteCommand.ExecuteAsync(f.Vm.SelectedRule);
        Assert.Null(f.Vm.SelectedRule); Assert.False(f.Vm.EditCommand.CanExecute(null));
        Assert.Empty(f.Vm.Rules); Assert.Equal(5, f.Data.Rows.Count);
        Assert.Equal("criterion 1", f.Data.Rows[0].Definition.Criterion);
    }

    [Fact]
    public async Task DirtySwitchCancelRefreshAndDepartureUseOneGuardAndPreserveScope()
    {
        var f = new RulesFixture(); var first = f.Data.Seed(1); var second = f.Data.Seed(1); await f.Open(first);
        f.Vm.EditCommand.Execute(null); f.Vm.Fields[2].Text = "local unsaved";
        Assert.False(f.Vm.TrySelect(f.Vm.Rules.Single(r => r.Id == second)));
        f.Vm.CancelEditCommand.Execute(null); await f.Vm.RefreshCommand.ExecuteAsync(null);
        Assert.True(f.Vm.IsEditing); Assert.Equal(first, f.Vm.SelectedRule!.Id); Assert.Equal("local unsaved", f.Vm.Fields[2].Text);
        Assert.False(f.Vm.TryLeave()); f.Dialogs.ConfirmationResult = true;
        f.Vm.CancelEditCommand.Execute(null); Assert.False(f.Vm.IsEditing); Assert.Equal(2, f.Data.Rows.Count);
        Assert.True(f.Vm.TrySelect(f.Vm.Rules.Single(r => r.Id == second))); await f.Vm.LoadTask;
        Assert.Equal(second, f.Vm.SelectedRule!.Id);
    }

    [Fact]
    public async Task DelayedWriteBlocksDuplicateSubmissionAndNavigationWithoutDroppingEdits()
    {
        var f = new RulesFixture(); await f.Vm.ActivateAsync(); f.Vm.CreateCommand.Execute(null); f.Fill();
        f.Data.WriteGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var saving = f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.True(f.Vm.IsWriting); Assert.False(f.Vm.SaveCommand.CanExecute(null)); Assert.False(f.Vm.CreateCommand.CanExecute(null));
        Assert.False(f.Vm.TryLeave()); Assert.True(f.Vm.IsEditing);
        f.Vm.CancelOperationCommand.Execute(null); f.Data.WriteGate.SetResult(); await saving;
        Assert.Empty(f.Data.Rows); Assert.True(f.Vm.IsEditing); Assert.Equal("Recorded criterion", f.Vm.Fields[1].Text);
    }

    [Fact]
    public async Task CancelledLateReadCannotRepopulateDeactivatedPage()
    {
        var f = new RulesFixture(); f.Data.ReadGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var loading = f.Vm.ActivateAsync(); f.Vm.Deactivate(); f.Data.Seed(1); f.Data.ReadGate.SetResult(); await loading;
        Assert.Empty(f.Vm.Rules); Assert.False(f.Vm.IsLoading);
        f.Data.ReadGate = null; await f.Vm.ActivateAsync(); Assert.Single(f.Vm.Rules);
    }

    [Theory]
    [InlineData(20, false)]
    [InlineData(21, true)]
    [InlineData(40, true)]
    public async Task GlobalCatalogPagingHasNoEmptyFinalPageAndPreservesDirtyEditor(int count, bool hasNext)
    {
        var f = new RulesFixture(); for (int i = 0; i < count; i++) f.Data.Seed(1);
        await f.Vm.ActivateAsync(); Assert.Equal(20, f.Vm.Rules.Count);
        Assert.Equal(hasNext, f.Vm.NextCommand.CanExecute(null));
        var firstPage = f.Vm.Rules.Select(r => r.Id).ToArray();
        await f.Vm.EditRowCommand.ExecuteAsync(f.Vm.Rules[0]); f.Vm.Fields[1].Text = "draft";
        if (hasNext)
        {
            await f.Vm.NextCommand.ExecuteAsync(null);
            Assert.Equal(firstPage, f.Vm.Rules.Select(r => r.Id)); Assert.Equal("draft", f.Vm.Fields[1].Text);
            f.Dialogs.ConfirmationResult = true; await f.Vm.NextCommand.ExecuteAsync(null);
            Assert.Equal(count - 20, f.Vm.Rules.Count); Assert.False(f.Vm.NextCommand.CanExecute(null));
            Assert.Empty(firstPage.Intersect(f.Vm.Rules.Select(r => r.Id)));
        }
    }

    [Theory]
    [InlineData("conflict")]
    [InlineData("failure")]
    [InlineData("cancel")]
    public async Task DeleteFailurePreservesCatalogAndEditorFields(string failure)
    {
        var f = new RulesFixture(); var id = f.Data.Seed(1); await f.Open(id);
        f.Vm.EditCommand.Execute(null); f.Vm.Fields[1].Text = "Keep my entered text";
        f.Dialogs.ConfirmationResult = false;
        await f.Vm.DeleteCommand.ExecuteAsync(f.Vm.SelectedRule);
        Assert.Single(f.Data.Rows); Assert.True(f.Vm.IsEditing);
        f.Dialogs.ConfirmationResult = true; f.Data.Failure = failure;
        await f.Vm.DeleteCommand.ExecuteAsync(f.Vm.SelectedRule);
        Assert.Single(f.Vm.Rules); Assert.Single(f.Data.Rows);
        Assert.True(f.Vm.IsEditing); Assert.Equal("Keep my entered text", f.Vm.Fields[1].Text);
    }

    [Fact]
    public async Task FailedRowReadDoesNotOpenAnUnverifiedEditorOrHideItsError()
    {
        var f = new RulesFixture(); f.Data.Seed(1); await f.Vm.ActivateAsync();
        f.Data.Failure = "readback";
        await f.Vm.EditRowCommand.ExecuteAsync(f.Vm.Rules[0]);
        Assert.False(f.Vm.IsEditing); Assert.Contains("could not be loaded", f.Vm.Message);
    }

    [Fact]
    public async Task DeletingLastRowOnLastPageReconcilesPageAndPreservesOtherRules()
    {
        var f = new RulesFixture(); for (int i = 0; i < 21; i++) f.Data.Seed(1);
        await f.Vm.ActivateAsync(); await f.Vm.NextCommand.ExecuteAsync(null);
        var last = Assert.Single(f.Vm.Rules); f.Dialogs.ConfirmationResult = true;
        await f.Vm.DeleteCommand.ExecuteAsync(last);
        Assert.Equal(20, f.Vm.Rules.Count); Assert.False(f.Vm.PreviousCommand.CanExecute(null));
        Assert.False(f.Vm.NextCommand.CanExecute(null)); Assert.DoesNotContain(f.Vm.Rules, r => r.Id == last.Id);
    }

    [Fact]
    public async Task LegacyUnavailableDefinitionIsNotSilentlyPromotedToGlobalCatalog()
    {
        var f = new RulesFixture(); var id = f.Data.Seed(1, f.Data.AccountId); f.Data.AccountAvailable = false;
        await f.Vm.ActivateAsync(); Assert.Empty(f.Vm.Rules);
        Assert.Equal(f.Data.AccountId, f.Data.Rows.Single().Definition.AccountScope.AccountId);
        Assert.False(f.Vm.EditCommand.CanExecute(null));
    }
}

internal sealed class RulesFixture
{
    public RulesData Data { get; } = new();
    public FakeDialogService Dialogs { get; } = new();
    public RulesViewModel Vm { get; }
    public RulesFixture() => Vm = new(Data, Data, new(Data, TimeProvider.System), Dialogs);
    public void Fill() { Vm.Fields[0].Text = "Synthetic rule"; Vm.Fields[1].Text = "Recorded criterion"; Vm.Fields[2].Text = "Required records"; }
    public async Task Open(Guid id) { await Vm.ActivateAsync(); Vm.TrySelect(Vm.Rules.Single(r => r.Id == id)); await Vm.LoadTask; }
}

internal sealed class RulesData : ITradingRuleReader, ITradingRuleStore, ITradingAccountReader
{
    public List<TradingRuleRevision> Rows { get; } = [];
    public Guid AccountId { get; } = Guid.NewGuid();
    public bool AccountAvailable { get; set; } = true;
    public string? Failure { get; set; }
    public TaskCompletionSource? WriteGate { get; set; }
    public TaskCompletionSource? ReadGate { get; set; }
    public Guid Seed(int count, Guid? account = null)
    {
        var id = Guid.NewGuid();
        for (int i = 1; i <= count; i++) Rows.Add(TradingRuleRevision.Rehydrate(id, i, account is Guid a ? new("Synthetic rule", "criterion " + i, "evidence", TradingRuleApplicability.Trade, TradingRuleAccountScope.ForAccount(a), TradingRuleEvaluationMode.ManualOnly) : new("Synthetic rule", "criterion " + i, "evidence"), true, DateTimeOffset.UtcNow.AddDays(-1)));
        return id;
    }
    public Task<TradingRuleRevision?> GetLatestAsync(Guid ruleId, CancellationToken cancellationToken = default) => Task.FromResult(Rows.LastOrDefault(r => r.RuleId == ruleId));
    public async Task<bool> TryAppendAsync(TradingRuleRevision candidate, int expectedRevision, CancellationToken cancellationToken = default)
    {
        if (WriteGate is not null) await WriteGate.Task;
        cancellationToken.ThrowIfCancellationRequested();
        if (Failure == "cancel") throw new OperationCanceledException();
        if (Failure == "failure") throw new IOException("Synthetic storage failure");
        if (Failure == "conflict" || (Rows.LastOrDefault(r => r.RuleId == candidate.RuleId)?.Revision ?? 0) != expectedRevision) return false;
        Rows.Add(candidate); return true;
    }
    private TradingRuleDetails Details(TradingRuleRevision r) => new(r, r.Definition.AccountScope.AccountId is null ? null : "Synthetic Account", r.Definition.AccountScope.AccountId is null || AccountAvailable);
    public async Task<TradingRulePage> ReadCatalogAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (ReadGate is not null) await ReadGate.Task;
        var rows = Rows.GroupBy(r => r.RuleId).Select(g => g.Last()).Where(r => r.IsActive && r.Definition.IsGlobal).OrderBy(r => r.RuleId).ToArray();
        return new(rows.Skip((page - 1) * pageSize).Take(pageSize).Select(Details).ToArray(), rows.Length, page, pageSize);
    }
    public async Task<TradingRulePage> ReadCurrentAsync(TradingRuleAccountScope? scope = null, bool? active = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (ReadGate is not null) await ReadGate.Task; // Deliberately ignores cancellation to exercise late-result rejection.
        var rows = Rows.GroupBy(r => r.RuleId).Select(g => g.Last()).OrderBy(r => r.RuleId).ToArray();
        return new(rows.Skip((page - 1) * pageSize).Take(pageSize).Select(Details).ToArray(), rows.Length, page, pageSize);
    }
    public Task<IReadOnlyList<TradingRuleDetails>> ReadRevisionsAsync(Guid ruleId, int? beforeRevision = null, int limit = 20, CancellationToken cancellationToken = default)
    {
        if (Failure == "readback") throw new IOException("Synthetic read failure");
        return Task.FromResult<IReadOnlyList<TradingRuleDetails>>(Rows.Where(r => r.RuleId == ruleId && (!beforeRevision.HasValue || r.Revision < beforeRevision)).OrderByDescending(r => r.Revision).Take(limit).Select(Details).ToArray());
    }
    public Task<TradingRuleAssessmentDetails?> ReadAssessmentAsync(Guid assessmentId, CancellationToken cancellationToken = default) => throw new NotSupportedException("No assessments in management UI");
    public Task<IReadOnlyList<TradingRuleAssessmentDetails>> ReadAssessmentsAsync(Guid ruleId, TradingRuleAssessmentTarget target, int? beforeSequence = null, int limit = 20, CancellationToken cancellationToken = default) => throw new NotSupportedException("No assessments in management UI");
    public Task<IReadOnlyList<AccountListItem>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AccountListItem>>(AccountAvailable ? [new(AccountId, "Synthetic Account", TradingAccountType.Personal, null, null, "USD", 0, false)] : []);
    public Task<TradingAccountDetails?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
