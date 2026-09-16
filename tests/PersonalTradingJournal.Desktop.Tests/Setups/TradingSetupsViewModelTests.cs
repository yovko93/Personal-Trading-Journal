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
        Assert.False(vm.IsCreateFormVisible); Assert.Equal(CancellationToken.None, reader.CancellationToken);
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
    public async Task LoadingGatesMutations()
    {
        var reader = new BlockingReader(); var vm = new TradingSetupsViewModel(reader,
            new CreateTradingSetupUseCase(new FakeTradingSetupStore(), new FakeTradingSetupNameChecker(), new FixedTimeProvider()),
            new TradingSetupLifecycleUseCase(new FakeTradingSetupStore(), new FixedTimeProvider()));
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
            new TradingSetupLifecycleUseCase(new FakeTradingSetupStore(), new FixedTimeProvider()));

        Task refresh = vm.RefreshCommand.ExecuteAsync(null);
        await reader.Started.Task;
        vm.RefreshCommand.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refresh);
    }

    private static TradingSetupsViewModel Create(FakeTradingSetupReader? reader = null, FakeTradingSetupStore? store = null,
        FakeTradingSetupNameChecker? checker = null)
    {
        reader ??= new(); store ??= new(); checker ??= new(); var time = new FixedTimeProvider();
        return new(reader, new CreateTradingSetupUseCase(store, checker, time), new TradingSetupLifecycleUseCase(store, time));
    }
    private static TradingSetupListItem Item(bool active) => new(Guid.NewGuid(), active ? "Active" : "Inactive", null, active, Timestamp, Timestamp);
    private sealed class BlockingReader : ITradingSetupReader
    {
        private readonly TaskCompletionSource<IReadOnlyList<TradingSetupListItem>> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IReadOnlyList<TradingSetupListItem>> GetAllAsync(CancellationToken token = default)
        { Started.TrySetResult(); return _result.Task.WaitAsync(token); }
        public void Complete(IReadOnlyList<TradingSetupListItem> rows) => _result.TrySetResult(rows);
    }
}
