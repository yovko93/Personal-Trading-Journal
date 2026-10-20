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

    [Fact]
    public async Task PlaybookSelectionIsExplicitAndChangedCatalogDoesNotUpdateTokenOrCriteria()
    {
        var f = new SessionPreparationTests.Fixture(); using var vm = f.Vm; var books = new Books();
        vm.ConfigurePlaybooks(books, books, books); await vm.LoadPlaybooksAsync(); var row = vm.Scenarios[0];
        row.Candidate = Assert.Single(vm.AvailablePlaybooks); Assert.Null(row.Setup);
        await row.LinkCommand.ExecuteAsync(null); var original = row.Setup!; Assert.Equal(1, original.ExpectedPlaybookRevision);
        Assert.Contains("original IF", row.PlaybookText); vm.Focus.Text = "retain navigation edits";
        Guid? request = null; vm.PlaybookNavigation = (id, _) => { request = id; return Task.FromResult(false); };
        await row.OpenPlaybookCommand.ExecuteAsync(null); Assert.Equal(books.Setup.Id, request); Assert.Equal("retain navigation edits", vm.Focus.Text);
        books.Revision = books.Revision.Revise(books.Setup, 1, new("new context", "new IF", "confirm", "invalid", "execution"), Books.Now.AddMinutes(1));
        await vm.LoadPlaybooksAsync(); Assert.Same(original, row.Setup); Assert.Contains("original IF", row.PlaybookText); Assert.Contains("changed", row.PlaybookText);
        row.Candidate = Assert.Single(vm.AvailablePlaybooks); await row.LinkCommand.ExecuteAsync(null);
        Assert.Equal(2, row.Setup!.ExpectedPlaybookRevision); Assert.Contains("new IF", row.PlaybookText);
        books.Missing = true; await vm.LoadPlaybooksAsync(); Assert.Empty(vm.AvailablePlaybooks); Assert.Contains("unavailable", row.PlaybookText);
        Assert.False(row.LinkCommand.CanExecute(null)); // A retained candidate is not eligible after catalog removal.
        Assert.False(row.OpenPlaybookCommand.CanExecute(null)); row.UnlinkCommand.Execute(null); Assert.Null(row.Setup);
        Assert.Equal(0, books.Writes); Assert.Equal(0, f.Store.Writes);
    }

    [Fact]
    public async Task FrozenReadyUsesSavedNameAndCriteriaEvenIfCurrentSourceIsMissing()
    {
        var f = new SessionPreparationTests.Fixture(); using var vm = f.Vm; var books = new Books();
        vm.ConfigurePlaybooks(books, books, books); await vm.LoadPlaybooksAsync(); var row = vm.Scenarios[0];
        row.Candidate = vm.AvailablePlaybooks[0]; await row.LinkCommand.ExecuteAsync(null); await vm.SaveAsync();
        var plan = f.Store.Plan; plan.SetConfirmations(plan.Version, TradingPlanChecklist.All, Books.Now.AddHours(1));
        var ready = plan.MarkReady(plan.Version, TradingPlanReferenceSnapshot.Capture(plan.Key, plan.WorkingDraft!.Content,
            f.Account, [f.Instrument], [new(books.Setup, books.Revision)], [], Books.Now.AddHours(1)), Books.Now.AddHours(1));
        using var reading = new SessionPreparationViewModel(plan, f.Store, f.Catalog);
        reading.ConfigurePlaybooks(books, books, books);
        books.Revision = books.Revision.Revise(books.Setup, 1, new("new context", "new IF", "confirm", "invalid", "execution"), Books.Now.AddMinutes(1));
        await reading.LoadPlaybooksAsync();
        Assert.Contains("Start a Draft", reading.Scenarios[0].PlaybookText);
        Assert.DoesNotContain("new IF", reading.Scenarios[0].PlaybookText);
        books.Missing = true; reading.ConfigurePlaybooks(books, books, books); await reading.LoadPlaybooksAsync();
        var saved = reading.Scenarios[0]; Assert.Contains("Synthetic setup", saved.PlaybookText); Assert.Contains("original IF", saved.PlaybookText);
        Assert.Contains("saved Plan-owned context", saved.PlaybookText); Assert.Contains("unavailable", saved.PlaybookText);
        saved.Condition.Text = "ignored"; saved.UnlinkCommand.Execute(null); saved.RemoveCommand.Execute(null);
        Assert.Equal(ready.Content.Scenarios[0].Condition, saved.Condition.Text); Assert.NotNull(saved.Setup); Assert.Single(reading.Scenarios);
    }

    [Fact]
    public async Task FailedOrCancelledLinkRetainsPreviousSelectionAndAllInput()
    {
        var f = new SessionPreparationTests.Fixture(); using var vm = f.Vm; var books = new Books();
        vm.ConfigurePlaybooks(books, books, books); await vm.LoadPlaybooksAsync(); var row = vm.Scenarios[0];
        row.Candidate = vm.AvailablePlaybooks[0]; await row.LinkCommand.ExecuteAsync(null); var original = row.Setup;
        books.Fail = true; await row.LinkCommand.ExecuteAsync(null); Assert.Same(original, row.Setup);
        Assert.Contains("failed", vm.Message); books.Fail = false;
        using var ct = new CancellationTokenSource(); ct.Cancel(); await vm.LinkPlaybookAsync(row, ct.Token);
        Assert.Same(original, row.Setup); Assert.Contains("cancelled", vm.Message); Assert.False(vm.IsBusy);
        books.Setup.Deactivate(Books.Now.AddMinutes(1)); await row.LinkCommand.ExecuteAsync(null);
        Assert.Same(original, row.Setup); Assert.Contains("active Setup", vm.Message);
    }

    [Fact]
    public async Task RealSqliteSharedSaveRestartChangedRevisionRefreshAndUnavailableLink()
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        var f = new SessionPreparationTests.Fixture(); using var unused = f.Vm; var books = new Books();
        await db.Provider.GetRequiredService<ITradingAccountStore>().AddAsync(f.Account);
        await db.Provider.GetRequiredService<IInstrumentStore>().AddAsync(f.Instrument);
        var setupStore = db.Provider.GetRequiredService<ITradingSetupStore>(); await setupStore.AddAsync(books.Setup);
        var playbooks = db.Provider.GetRequiredService<ITradingSetupPlaybookStore>();
        Assert.True(await playbooks.TryAppendAsync(books.Revision, 0, books.Setup.UpdatedAtUtc));
        var store = db.Provider.GetRequiredService<ITradingPlanStore>();
        var created = await store.WriteAsync(new(TradingPlanWriteOperation.CreateDraft, f.Store.Plan.Key, f.Content));
        using var vm = new SessionPreparationViewModel(created.Plan!, store, f.Catalog);
        vm.ConfigurePlaybooks(db.Provider.GetRequiredService<ITradingSetupReader>(), playbooks, db.Provider.GetRequiredService<ITradingSetupPlaybookHistoryReader>());
        await vm.LoadPlaybooksAsync(); var row = vm.Scenarios[0]; row.Candidate = vm.AvailablePlaybooks[0]; await row.LinkCommand.ExecuteAsync(null);
        vm.Focus.Text = "shared SQLite focus"; row.Response.Text = "shared SQLite response";
        Assert.Equal(TradingPlanWriteStatus.Success, await vm.SaveAsync());
        var loaded = (await store.LoadAsync(vm.Key))!;
        using var reopen = new SessionPreparationViewModel(loaded, store, f.Catalog);
        Assert.Equal(vm.Focus.Text, reopen.Focus.Text); Assert.Equal(row.Response.Text, reopen.Scenarios[0].Response.Text); Assert.Equal(row.Setup, reopen.Scenarios[0].Setup);
        var revised = books.Revision.Revise(books.Setup, 1, new("new", "new IF", "confirmation", "invalid", "execution"), DateTimeOffset.UtcNow);
        Assert.True(await playbooks.TryAppendAsync(revised, 1, books.Setup.UpdatedAtUtc));
        row.Condition.Text = "retained after changed reference";
        Assert.Equal(TradingPlanWriteStatus.ReferenceChanged, await vm.SaveAsync());
        Assert.Equal(1, row.Setup!.ExpectedPlaybookRevision); await vm.LoadPlaybooksAsync();
        Assert.Equal(1, row.Setup.ExpectedPlaybookRevision); row.Candidate = vm.AvailablePlaybooks[0]; await row.LinkCommand.ExecuteAsync(null);
        Assert.Equal(2, row.Setup!.ExpectedPlaybookRevision);
        Assert.Equal(TradingPlanWriteStatus.ReferenceChanged, await vm.SaveAsync());
        Assert.Equal(TradingPlanWriteStatus.Success, await vm.RefreshReferencesAsync()); Assert.Equal(TradingPlanChecklist.None, vm.Confirmed);
        Assert.Equal("retained after changed reference", row.Condition.Text);
        books.Setup.Deactivate(DateTimeOffset.UtcNow); await setupStore.UpdateAsync(books.Setup);
        row.Name.Text = "retained inactive"; Assert.Equal(TradingPlanWriteStatus.ReferenceUnavailable, await vm.SaveAsync());
        row.UnlinkCommand.Execute(null); Assert.Equal(TradingPlanWriteStatus.Success, await vm.SaveAsync());
        Assert.Null((await store.LoadAsync(vm.Key))!.WorkingDraft!.Content.Scenarios[0].Setup);
        reopen.Focus.Text = "stale input"; Assert.Equal(TradingPlanWriteStatus.Conflict, await reopen.SaveAsync()); Assert.Equal("stale input", reopen.Focus.Text);
    }

    internal sealed class Books : ITradingSetupReader, ITradingSetupPlaybookStore, ITradingSetupPlaybookHistoryReader
    {
        internal static readonly DateTimeOffset Now = SessionPreparationTests.Fixture.Now;
        internal TradingSetup Setup = new("Synthetic setup", null, Now.AddHours(-1));
        internal TradingSetupPlaybookRevision Revision;
        private readonly TradingSetupPlaybookRevision _first;
        internal bool Missing, Fail;
        internal int Writes;
        internal Books() { Revision = _first = TradingSetupPlaybookRevision.Create(Setup, new("context", "original IF", "confirmation", "invalidation", "execution"), Now); }
        public Task<IReadOnlyList<TradingSetupListItem>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TradingSetupListItem>>(Missing ? [] :
            [new(Setup.Id, Setup.Name, Setup.Description, Setup.IsActive, Setup.CreatedAtUtc, Setup.UpdatedAtUtc)]);
        public Task<TradingSetupDetails?> GetByIdAsync(Guid setupId, CancellationToken cancellationToken = default) => Fail ? throw new IOException("Synthetic") : Task.FromResult<TradingSetupDetails?>(Missing ? null :
            new(Setup.Id, Setup.Name, Setup.Description, Setup.IsActive, Setup.CreatedAtUtc, Setup.UpdatedAtUtc));
        public Task<TradingSetupPlaybookRevision?> GetLatestAsync(Guid tradingSetupId, CancellationToken cancellationToken = default) => Task.FromResult<TradingSetupPlaybookRevision?>(Missing ? null : Revision);
        public Task<TradingSetupPlaybookRevision?> GetRevisionAsync(Guid setupId, int revision, CancellationToken cancellationToken = default) => Task.FromResult<TradingSetupPlaybookRevision?>(revision == 1 ? _first : Revision);
        public Task<IReadOnlyList<TradingSetupPlaybookRevision>> GetHistoryAsync(Guid setupId, int? beforeRevision, int limit, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> TryAppendAsync(TradingSetupPlaybookRevision revision, int expectedRevision, DateTimeOffset expectedSetupUpdatedAtUtc, CancellationToken cancellationToken = default) { Writes++; throw new InvalidOperationException("Editor must not write the catalog."); }
    }
}
