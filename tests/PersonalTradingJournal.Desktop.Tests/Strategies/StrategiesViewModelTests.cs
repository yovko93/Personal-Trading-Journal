using PersonalTradingJournal.Application.Strategies;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Strategies;
using PersonalTradingJournal.Domain.Strategies;

namespace PersonalTradingJournal.Desktop.Tests.Strategies;

public sealed class StrategiesViewModelTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 9, 8, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task InitialLoadIncludesActiveAndInactiveAndRunsOnlyOnce()
    {
        StrategyListItem[] rows = [Item(true), Item(false)];
        var reader = new FakeStrategyReader();
        reader.EnqueueResult(rows);
        StrategiesViewModel viewModel = Create(reader);

        await viewModel.EnsureLoadedAsync();
        await viewModel.EnsureLoadedAsync();

        Assert.Same(rows, viewModel.Strategies);
        Assert.Contains(viewModel.Strategies, row => !row.IsActive);
        Assert.Equal(1, reader.CallCount);
    }

    [Fact]
    public async Task LoadFailureUsesSafeMessageAndRetainsExistingRows()
    {
        StrategyListItem[] rows = [Item(true)];
        var reader = new FakeStrategyReader();
        reader.EnqueueResult(rows);
        reader.EnqueueException(new IOException("Sensitive details"));
        StrategiesViewModel viewModel = Create(reader);
        await viewModel.EnsureLoadedAsync();

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Same(rows, viewModel.Strategies);
        Assert.Equal("Strategies could not be loaded.", viewModel.ListErrorMessage);
    }

    [Fact]
    public void ShowAndCancelCreateResetDraft()
    {
        StrategiesViewModel viewModel = Create();
        viewModel.NameText = "old";
        viewModel.DescriptionText = "old";

        viewModel.ShowCreateCommand.Execute(null);
        Assert.True(viewModel.IsCreateFormVisible);
        Assert.Equal(string.Empty, viewModel.NameText);

        viewModel.NameText = "new";
        viewModel.DescriptionText = "description";
        viewModel.CancelCreateCommand.Execute(null);
        Assert.False(viewModel.IsCreateFormVisible);
        Assert.Equal(string.Empty, viewModel.NameText);
        Assert.Equal(string.Empty, viewModel.DescriptionText);
    }

    [Fact]
    public async Task BlankNameBlocksUseCase()
    {
        var store = new FakeStrategyStore();
        StrategiesViewModel viewModel = Create(store: store);
        viewModel.ShowCreateCommand.Execute(null);
        viewModel.NameText = "   ";

        await viewModel.CreateCommand.ExecuteAsync(null);

        Assert.Equal("Name is required.", viewModel.ValidationErrorMessage);
        Assert.Equal(0, store.AddCallCount);
    }

    [Fact]
    public async Task CreatePassesExactInputAndReloadsAuthoritatively()
    {
        StrategyListItem[] authoritative = [Item(true)];
        var reader = new FakeStrategyReader();
        reader.EnqueueResult(authoritative);
        var store = new FakeStrategyStore();
        StrategiesViewModel viewModel = Create(reader, store);
        viewModel.ShowCreateCommand.Execute(null);
        viewModel.NameText = "  ICT  ";
        viewModel.DescriptionText = "  Model  ";

        await viewModel.CreateCommand.ExecuteAsync(null);

        Assert.Equal("ICT", store.Strategy?.Name);
        Assert.Equal("Model", store.Strategy?.Description);
        Assert.Same(authoritative, viewModel.Strategies);
        Assert.Equal("Strategy created.", viewModel.SuccessMessage);
        Assert.False(viewModel.IsCreateFormVisible);
        Assert.Equal(CancellationToken.None, reader.CancellationToken);
    }

    [Fact]
    public async Task DuplicateMapsToStableValidationMessage()
    {
        var checker = new FakeStrategyNameChecker { Exists = true };
        StrategiesViewModel viewModel = Create(checker: checker);
        viewModel.ShowCreateCommand.Execute(null);
        viewModel.NameText = "ICT";

        await viewModel.CreateCommand.ExecuteAsync(null);

        Assert.Equal(CreateStrategyUseCase.DuplicateNameMessage,
            viewModel.ValidationErrorMessage);
        Assert.True(viewModel.IsCreateFormVisible);
    }

    [Fact]
    public async Task SuccessfulCreateWithReloadFailureRemainsSuccess()
    {
        var reader = new FakeStrategyReader();
        reader.EnqueueException(new IOException("Read failed"));
        StrategiesViewModel viewModel = Create(reader);
        viewModel.ShowCreateCommand.Execute(null);
        viewModel.NameText = "Breakout";

        await viewModel.CreateCommand.ExecuteAsync(null);

        Assert.Equal("Strategy created.", viewModel.SuccessMessage);
        Assert.Equal("Strategies could not be loaded.", viewModel.ListErrorMessage);
        Assert.Null(viewModel.SaveErrorMessage);
    }

    [Theory]
    [InlineData(true, false, "Strategy deactivated.")]
    [InlineData(false, true, "Strategy activated.")]
    public async Task ToggleUsesExactIdAndReloadsAuthoritatively(
        bool initiallyActive,
        bool expectedActive,
        string expectedMessage)
    {
        StrategyListItem row = Item(initiallyActive);
        var strategy = Strategy.Rehydrate(
            row.Id, row.Name, row.Description, initiallyActive, Timestamp, Timestamp);
        var store = new FakeStrategyStore { Strategy = strategy };
        StrategyListItem[] refreshed = [row with { IsActive = expectedActive }];
        var reader = new FakeStrategyReader();
        reader.EnqueueResult(refreshed);
        StrategiesViewModel viewModel = Create(reader, store);

        await viewModel.ToggleActiveCommand.ExecuteAsync(row);

        Assert.Equal(row.Id, store.RequestedStrategyId);
        Assert.Equal(expectedActive, store.Strategy?.IsActive);
        Assert.Equal(1, store.UpdateCallCount);
        Assert.Same(refreshed, viewModel.Strategies);
        Assert.Equal(expectedMessage, viewModel.SuccessMessage);
    }

    [Fact]
    public async Task ToggleMissingUsesSafeMessageWithoutReload()
    {
        var store = new FakeStrategyStore();
        var reader = new FakeStrategyReader();
        StrategiesViewModel viewModel = Create(reader, store);

        await viewModel.ToggleActiveCommand.ExecuteAsync(Item(true));

        Assert.Equal("The selected strategy is no longer available.",
            viewModel.SaveErrorMessage);
        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task ToggleFailureUsesSafeFallback()
    {
        StrategyListItem row = Item(true);
        var store = new FakeStrategyStore
        {
            Strategy = Strategy.Rehydrate(
                row.Id, row.Name, null, true, Timestamp, Timestamp),
            UpdateException = new IOException("Database path"),
        };
        StrategiesViewModel viewModel = Create(store: store);

        await viewModel.ToggleActiveCommand.ExecuteAsync(row);

        Assert.Equal("Strategy status could not be changed.", viewModel.SaveErrorMessage);
    }

    [Fact]
    public async Task LoadingGatesCreateAndLifecycleMutations()
    {
        var reader = new BlockingStrategyReader();
        var store = new FakeStrategyStore();
        var checker = new FakeStrategyNameChecker();
        var timeProvider = new FixedTimeProvider();
        var viewModel = new StrategiesViewModel(
            reader,
            new CreateStrategyUseCase(store, checker, timeProvider),
            new StrategyLifecycleUseCase(store, timeProvider));

        Task load = viewModel.EnsureLoadedAsync();
        await reader.Started.Task;

        Assert.True(viewModel.IsLoading);
        Assert.False(viewModel.ShowCreateCommand.CanExecute(null));
        Assert.False(viewModel.ToggleActiveCommand.CanExecute(Item(true)));

        reader.Complete([]);
        await load;
    }

    private static StrategiesViewModel Create(
        FakeStrategyReader? reader = null,
        FakeStrategyStore? store = null,
        FakeStrategyNameChecker? checker = null)
    {
        reader ??= new FakeStrategyReader();
        store ??= new FakeStrategyStore();
        checker ??= new FakeStrategyNameChecker();
        var timeProvider = new FixedTimeProvider();
        return new StrategiesViewModel(
            reader,
            new CreateStrategyUseCase(store, checker, timeProvider),
            new StrategyLifecycleUseCase(store, timeProvider));
    }

    private static StrategyListItem Item(bool isActive) => new(
        Guid.NewGuid(),
        isActive ? "Active Strategy" : "Inactive Strategy",
        null,
        isActive,
        Timestamp,
        Timestamp);

    private sealed class BlockingStrategyReader : IStrategyReader
    {
        private readonly TaskCompletionSource<IReadOnlyList<StrategyListItem>> _result =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<IReadOnlyList<StrategyListItem>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            return _result.Task.WaitAsync(cancellationToken);
        }

        public void Complete(IReadOnlyList<StrategyListItem> strategies) =>
            _result.TrySetResult(strategies);
    }
}
