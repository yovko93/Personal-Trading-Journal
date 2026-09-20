using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Setups;
using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Desktop.Tests.Setups;

public sealed class TradingSetupsViewModelTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 9, 8, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task InitialLoadShowsActiveAndInactiveOnlyOnce()
    {
        TradingSetupListItem[] rows = [Item(true), Item(false)]; var reader = new FakeTradingSetupReader(); reader.EnqueueResult(rows);
        TradingSetupsViewModel vm = Create(reader); await vm.EnsureLoadedAsync(); await vm.EnsureLoadedAsync();
        Assert.Same(rows, vm.TradingSetups); Assert.Contains(vm.TradingSetups, x => !x.IsActive); Assert.Equal(1, reader.CallCount);
    }

    [Fact]
    public async Task LoadFailureIsSafeAndRetainsRows()
    {
        TradingSetupListItem[] rows = [Item(true)]; var reader = new FakeTradingSetupReader();
        reader.EnqueueResult(rows); reader.EnqueueException(new IOException("secret")); var vm = Create(reader);
        await vm.EnsureLoadedAsync(); await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Same(rows, vm.TradingSetups); Assert.Equal("Trading setups could not be loaded.", vm.ListErrorMessage);
    }

    [Fact]
    public void ShowAndCancelResetDraft()
    {
        var vm = Create(); vm.NameText = "old"; vm.DescriptionText = "old"; vm.ShowCreateCommand.Execute(null);
        Assert.True(vm.IsCreateFormVisible); Assert.Empty(vm.NameText);
        vm.NameText = "new"; vm.DescriptionText = "description"; vm.CancelCreateCommand.Execute(null);
        Assert.False(vm.IsCreateFormVisible); Assert.Empty(vm.NameText); Assert.Empty(vm.DescriptionText);
    }

    [Fact]
    public async Task BlankNameBlocksCreate()
    {
        var store = new FakeTradingSetupStore(); var vm = Create(store: store); vm.ShowCreateCommand.Execute(null); vm.NameText = " ";
        await vm.CreateCommand.ExecuteAsync(null);
        Assert.Equal("Name is required.", vm.ValidationErrorMessage); Assert.True(vm.IsNameInvalid); Assert.Equal(0, store.AddCallCount);
        vm.NameText = "ORB";
        Assert.False(vm.IsNameInvalid); Assert.Null(vm.ValidationErrorMessage);
    }

    [Fact]
    public void CreateCommandRequiresNonBlankName()
    {
        var vm = Create();

        vm.ShowCreateCommand.Execute(null);
        Assert.False(vm.CreateCommand.CanExecute(null));

        vm.NameText = "ORB";
        Assert.True(vm.CreateCommand.CanExecute(null));

        vm.NameText = " ";
        Assert.False(vm.CreateCommand.CanExecute(null));
    }

    [Fact]
    public async Task CreateUsesInputAndAuthoritativeReloadWithSuccess()
    {
        TradingSetupListItem[] rows = [Item(true)]; var reader = new FakeTradingSetupReader(); reader.EnqueueResult(rows);
        var store = new FakeTradingSetupStore(); var vm = Create(reader, store); vm.ShowCreateCommand.Execute(null);
        vm.NameText = "  ORB  "; vm.DescriptionText = "  Opening range  "; await vm.CreateCommand.ExecuteAsync(null);
        Assert.Equal("ORB", store.Setup?.Name); Assert.Equal("Opening range", store.Setup?.Description);
        Assert.Same(rows, vm.TradingSetups); Assert.Equal("Trading setup created.", vm.SuccessMessage);
        Assert.False(vm.IsCreateFormVisible); Assert.False(reader.CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task DuplicateUsesStableMessage()
    {
        var checker = new FakeTradingSetupNameChecker { Exists = true }; var vm = Create(checker: checker);
        vm.ShowCreateCommand.Execute(null); vm.NameText = "ORB"; await vm.CreateCommand.ExecuteAsync(null);
        Assert.Equal(CreateTradingSetupUseCase.DuplicateNameMessage, vm.ValidationErrorMessage); Assert.True(vm.IsCreateFormVisible);
    }

    [Fact]
    public async Task CreateReloadFailureRemainsSuccess()
    {
        var reader = new FakeTradingSetupReader(); reader.EnqueueException(new IOException("read")); var vm = Create(reader);
        vm.ShowCreateCommand.Execute(null); vm.NameText = "CRT"; await vm.CreateCommand.ExecuteAsync(null);
        Assert.Equal("Trading setup created.", vm.SuccessMessage); Assert.Equal("Trading setups could not be loaded.", vm.ListErrorMessage);
        Assert.Null(vm.SaveErrorMessage);
    }

    [Theory]
    [InlineData(true, false, "Trading setup deactivated.")]
    [InlineData(false, true, "Trading setup activated.")]
    public async Task ToggleUsesExactIdAndReloads(bool initial, bool desired, string message)
    {
        TradingSetupListItem row = Item(initial); var setup = TradingSetup.Rehydrate(row.Id, row.Name, null, initial, Timestamp, Timestamp);
        var store = new FakeTradingSetupStore { Setup = setup }; TradingSetupListItem[] rows = [row with { IsActive = desired }];
        var reader = new FakeTradingSetupReader(); reader.EnqueueResult(rows); var vm = Create(reader, store);
        await vm.ToggleActiveCommand.ExecuteAsync(row);
        Assert.Equal(row.Id, store.RequestedId); Assert.Equal(desired, setup.IsActive); Assert.Equal(1, store.UpdateCallCount);
        Assert.Same(rows, vm.TradingSetups); Assert.Equal(message, vm.SuccessMessage);
    }

    [Fact]
    public async Task MissingAndLifecycleFailureUseSafeMessages()
    {
        TradingSetupListItem row = Item(true); var store = new FakeTradingSetupStore(); var vm = Create(store: store);
        await vm.ToggleActiveCommand.ExecuteAsync(row);
        Assert.Equal("The selected trading setup is no longer available.", vm.SaveErrorMessage);
        store.Setup = TradingSetup.Rehydrate(row.Id, row.Name, null, true, Timestamp, Timestamp);
        store.UpdateException = new IOException("secret"); await vm.ToggleActiveCommand.ExecuteAsync(row);
        Assert.Equal("Trading setup status could not be changed.", vm.SaveErrorMessage);
    }

    [Fact]
    public async Task ViewLoadsDetailsAndEditPrepopulatesWhileCancelDoesNotPersist()
    {
        TradingSetupDetails details = Details();
        var reader = new FakeTradingSetupReader { DetailsToReturn = details };
        var store = new FakeTradingSetupStore
        {
            Setup = TradingSetup.Rehydrate(
                details.Id, details.Name, details.Description, details.IsActive,
                details.CreatedAtUtc, details.UpdatedAtUtc),
        };
        TradingSetupsViewModel vm = Create(reader, store);

        await vm.ViewCommand.ExecuteAsync(details.Id);
        Assert.Same(details, vm.SelectedTradingSetup);
        Assert.False(vm.IsEditFormVisible);

        await vm.EditCommand.ExecuteAsync(details.Id);
        Assert.True(vm.IsEditFormVisible);
        Assert.Equal(details.Name, vm.EditNameText);
        Assert.Equal(details.Description, vm.EditDescriptionText);

        vm.EditNameText = "Changed";
        vm.CancelEditCommand.Execute(null);
        Assert.False(vm.IsEditFormVisible);
        Assert.Equal(0, store.UpdateCallCount);
    }

    [Fact]
    public async Task SaveUpdateNormalizesRefreshesListAndExitsEdit()
    {
        TradingSetupDetails details = Details();
        var reader = new FakeTradingSetupReader { DetailsToReturn = details };
        reader.EnqueueResult([Item(details.Id, "Renamed", true)]);
        var store = new FakeTradingSetupStore
        {
            Setup = TradingSetup.Rehydrate(
                details.Id, details.Name, details.Description, details.IsActive,
                details.CreatedAtUtc, details.UpdatedAtUtc),
        };
        TradingSetupsViewModel vm = Create(reader, store);
        await vm.EditCommand.ExecuteAsync(details.Id);
        vm.EditNameText = "  Renamed  ";
        vm.EditDescriptionText = "   ";

        await vm.SaveChangesCommand.ExecuteAsync(null);

        Assert.Equal(1, store.UpdateCallCount);
        Assert.Equal("Renamed", store.Setup?.Name);
        Assert.Null(store.Setup?.Description);
        Assert.False(vm.IsEditFormVisible);
        Assert.Equal("Trading setup updated.", vm.SuccessMessage);
    }

    [Fact]
    public async Task EditValidationUsesFieldStateAndDoesNotPersist()
    {
        TradingSetupDetails details = Details();
        var reader = new FakeTradingSetupReader { DetailsToReturn = details };
        var store = new FakeTradingSetupStore
        {
            Setup = TradingSetup.Rehydrate(
                details.Id, details.Name, details.Description, true,
                details.CreatedAtUtc, details.UpdatedAtUtc),
        };
        TradingSetupsViewModel vm = Create(reader, store);
        await vm.EditCommand.ExecuteAsync(details.Id);
        vm.EditNameText = " ";

        await vm.SaveChangesCommand.ExecuteAsync(null);

        Assert.True(vm.IsEditNameInvalid);
        Assert.Equal("Name is required.", vm.EditErrorMessage);
        Assert.Equal(0, store.UpdateCallCount);
    }

    [Fact]
    public async Task DeleteRequestsDestructiveConfirmationAndCancelDoesNotDelete()
    {
        TradingSetupDetails details = Details();
        var dialog = new FakeDialogService { ConfirmationResult = false };
        var deletionStore = new FakeTradingSetupDeletionStore();
        TradingSetupsViewModel vm = Create(
            deletionStore: deletionStore,
            dialog: dialog);

        await vm.DeleteCommand.ExecuteAsync(details.Id);

        Assert.Equal("Delete trading setup?", dialog.ConfirmationRequest?.Title);
        Assert.True(dialog.ConfirmationRequest?.IsDestructive);
        Assert.Equal(0, deletionStore.DeleteCallCount);
    }

    [Fact]
    public async Task ReferencedDeleteShowsDeactivateInformation()
    {
        TradingSetupDetails details = Details();
        var store = new FakeTradingSetupStore
        {
            Setup = TradingSetup.Rehydrate(
                details.Id, details.Name, details.Description, true,
                details.CreatedAtUtc, details.UpdatedAtUtc),
        };
        var dialog = new FakeDialogService { ConfirmationResult = true };
        var deletionStore = new FakeTradingSetupDeletionStore { HasTrades = true };
        TradingSetupsViewModel vm = Create(
            store: store,
            deletionStore: deletionStore,
            dialog: dialog);

        await vm.DeleteCommand.ExecuteAsync(details.Id);

        Assert.Equal("Cannot delete trading setup", dialog.InformationRequest?.Title);
        Assert.Contains("Deactivate", dialog.InformationRequest?.Message, StringComparison.Ordinal);
        Assert.Equal(0, deletionStore.DeleteCallCount);
    }

    [Fact]
    public async Task SuccessfulDeleteClearsSelectionAndRefreshesAuthoritativeList()
    {
        TradingSetupDetails details = Details();
        var reader = new FakeTradingSetupReader { DetailsToReturn = details };
        reader.EnqueueResult([]);
        var store = new FakeTradingSetupStore
        {
            Setup = TradingSetup.Rehydrate(
                details.Id, details.Name, details.Description, true,
                details.CreatedAtUtc, details.UpdatedAtUtc),
        };
        var dialog = new FakeDialogService { ConfirmationResult = true };
        var deletionStore = new FakeTradingSetupDeletionStore();
        TradingSetupsViewModel vm = Create(reader, store,
            deletionStore: deletionStore, dialog: dialog);
        await vm.ViewCommand.ExecuteAsync(details.Id);

        await vm.DeleteCommand.ExecuteAsync(details.Id);

        Assert.Equal(1, deletionStore.DeleteCallCount);
        Assert.Null(vm.SelectedTradingSetup);
        Assert.False(vm.IsEditFormVisible);
        Assert.Empty(vm.TradingSetups);
        Assert.Equal("Trading setup deleted.", vm.SuccessMessage);
    }

    [Fact]
    public async Task DeleteBusyStateGatesOtherMutations()
    {
        TradingSetupDetails details = Details();
        var store = new FakeTradingSetupStore
        {
            Setup = TradingSetup.Rehydrate(
                details.Id, details.Name, details.Description, true,
                details.CreatedAtUtc, details.UpdatedAtUtc),
        };
        var deletionStore = new BlockingDeletionStore();
        TradingSetupsViewModel vm = Create(
            store: store,
            deletionStore: deletionStore,
            dialog: new FakeDialogService { ConfirmationResult = true });

        Task deleting = vm.DeleteCommand.ExecuteAsync(details.Id);
        await deletionStore.Started.Task;

        Assert.True(vm.IsDeleting);
        Assert.False(vm.ViewCommand.CanExecute(Guid.NewGuid()));
        Assert.False(vm.ToggleActiveCommand.CanExecute(Item(true)));

        deletionStore.Complete();
        await deleting;
    }

    [Fact]
    public async Task LoadingGatesMutations()
    {
        var reader = new BlockingReader(); var vm = new TradingSetupsViewModel(reader,
            new CreateTradingSetupUseCase(new FakeTradingSetupStore(), new FakeTradingSetupNameChecker(), new FixedTimeProvider()),
            new TradingSetupLifecycleUseCase(new FakeTradingSetupStore(), new FixedTimeProvider()),
            new GetTradingSetupDetailsUseCase(reader),
            new UpdateTradingSetupUseCase(new FakeTradingSetupStore(), new FakeTradingSetupNameChecker(), new FixedTimeProvider()),
            new DeleteTradingSetupUseCase(new FakeTradingSetupStore(), new FakeTradingSetupDeletionStore()),
            new FakeDialogService());
        Task load = vm.EnsureLoadedAsync(); await reader.Started.Task;
        Assert.True(vm.IsLoading); Assert.False(vm.ShowCreateCommand.CanExecute(null)); Assert.False(vm.ToggleActiveCommand.CanExecute(Item(true)));
        reader.Complete([]); await load;
    }

    [Fact]
    public async Task RefreshCancellationPropagatesToReader()
    {
        var reader = new BlockingReader();
        var vm = new TradingSetupsViewModel(reader,
            new CreateTradingSetupUseCase(new FakeTradingSetupStore(), new FakeTradingSetupNameChecker(), new FixedTimeProvider()),
            new TradingSetupLifecycleUseCase(new FakeTradingSetupStore(), new FixedTimeProvider()),
            new GetTradingSetupDetailsUseCase(reader),
            new UpdateTradingSetupUseCase(new FakeTradingSetupStore(), new FakeTradingSetupNameChecker(), new FixedTimeProvider()),
            new DeleteTradingSetupUseCase(new FakeTradingSetupStore(), new FakeTradingSetupDeletionStore()),
            new FakeDialogService());

        Task refresh = vm.RefreshCommand.ExecuteAsync(null);
        await reader.Started.Task;
        vm.RefreshCommand.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refresh);
    }

    private static TradingSetupsViewModel Create(
        FakeTradingSetupReader? reader = null,
        FakeTradingSetupStore? store = null,
        FakeTradingSetupNameChecker? checker = null,
        ITradingSetupDeletionStore? deletionStore = null,
        FakeDialogService? dialog = null)
    {
        reader ??= new(); store ??= new(); checker ??= new();
        deletionStore ??= new FakeTradingSetupDeletionStore(); dialog ??= new();
        var time = new FixedTimeProvider();
        return new(
            reader,
            new CreateTradingSetupUseCase(store, checker, time),
            new TradingSetupLifecycleUseCase(store, time),
            new GetTradingSetupDetailsUseCase(reader),
            new UpdateTradingSetupUseCase(store, checker, time),
            new DeleteTradingSetupUseCase(store, deletionStore),
            dialog);
    }
    private static TradingSetupListItem Item(bool active) =>
        Item(Guid.NewGuid(), active ? "Active" : "Inactive", active);
    private static TradingSetupListItem Item(Guid id, string name, bool active) =>
        new(id, name, null, active, Timestamp, Timestamp);
    private static TradingSetupDetails Details() =>
        new(Guid.NewGuid(), "Silver Bullet", "Timed model", true, Timestamp, Timestamp);
    private sealed class BlockingReader : ITradingSetupReader
    {
        private readonly TaskCompletionSource<IReadOnlyList<TradingSetupListItem>> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IReadOnlyList<TradingSetupListItem>> GetAllAsync(CancellationToken token = default)
        { Started.TrySetResult(); return _result.Task.WaitAsync(token); }
        public Task<TradingSetupDetails?> GetByIdAsync(Guid setupId, CancellationToken token = default) =>
            Task.FromResult<TradingSetupDetails?>(null);
        public void Complete(IReadOnlyList<TradingSetupListItem> rows) => _result.TrySetResult(rows);
    }

    private sealed class BlockingDeletionStore : ITradingSetupDeletionStore
    {
        private readonly TaskCompletionSource _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<bool> HasTradesAsync(
            Guid setupId,
            CancellationToken cancellationToken = default) => Task.FromResult(false);
        public async Task DeleteAsync(
            Guid setupId,
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await _completion.Task.WaitAsync(cancellationToken);
        }
        public void Complete() => _completion.TrySetResult();
    }
}
