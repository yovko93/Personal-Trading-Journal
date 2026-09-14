using PersonalTradingJournal.Application.Mistakes;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Mistakes;
using PersonalTradingJournal.Domain.Mistakes;

namespace PersonalTradingJournal.Desktop.Tests.Mistakes;

public sealed class TradingMistakesViewModelTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 9, 8, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task InitialLoadShowsActiveAndInactiveOnlyOnce()
    {
        TradingMistakeListItem[] rows = [Item(true), Item(false)]; var reader = new FakeTradingMistakeReader(); reader.EnqueueResult(rows);
        TradingMistakesViewModel viewModel = Create(reader); await viewModel.EnsureLoadedAsync(); await viewModel.EnsureLoadedAsync();
        Assert.Same(rows, viewModel.TradingMistakes); Assert.Contains(viewModel.TradingMistakes, x => !x.IsActive); Assert.Equal(1, reader.CallCount);
    }

    [Fact]
    public async Task LoadFailureUsesSafeMessageAndRetainsRows()
    {
        TradingMistakeListItem[] rows = [Item(true)]; var reader = new FakeTradingMistakeReader();
        reader.EnqueueResult(rows); reader.EnqueueException(new IOException("secret")); TradingMistakesViewModel viewModel = Create(reader);
        await viewModel.EnsureLoadedAsync(); await viewModel.RefreshCommand.ExecuteAsync(null);
        Assert.Same(rows, viewModel.TradingMistakes); Assert.Equal("Trading mistakes could not be loaded.", viewModel.ListErrorMessage);
    }

    [Fact]
    public void ShowAndCancelResetDraftAndVisibility()
    {
        TradingMistakesViewModel viewModel = Create(); viewModel.NameText = "old"; viewModel.DescriptionText = "old";
        viewModel.ShowCreateCommand.Execute(null); Assert.True(viewModel.IsCreateFormVisible); Assert.Empty(viewModel.NameText);
        viewModel.NameText = "new"; viewModel.DescriptionText = "description"; viewModel.CancelCreateCommand.Execute(null);
        Assert.False(viewModel.IsCreateFormVisible); Assert.Empty(viewModel.NameText); Assert.Empty(viewModel.DescriptionText);
    }

    [Fact]
    public async Task BlankNameBlocksCreate()
    {
        var store = new FakeTradingMistakeStore(); TradingMistakesViewModel viewModel = Create(store: store);
        viewModel.ShowCreateCommand.Execute(null); viewModel.NameText = " "; await viewModel.CreateCommand.ExecuteAsync(null);
        Assert.Equal("Name is required.", viewModel.ValidationErrorMessage); Assert.Equal(0, store.AddCalls);
    }

    [Fact]
    public async Task CreateUsesInputAndAuthoritativeReloadWithSuccess()
    {
        TradingMistakeListItem[] rows = [Item(true)]; var reader = new FakeTradingMistakeReader(); reader.EnqueueResult(rows);
        var store = new FakeTradingMistakeStore(); TradingMistakesViewModel viewModel = Create(reader, store);
        viewModel.ShowCreateCommand.Execute(null); viewModel.NameText = "  FOMO  "; viewModel.DescriptionText = "  Early entry  ";
        await viewModel.CreateCommand.ExecuteAsync(null);
        Assert.Equal("FOMO", store.Mistake?.Name); Assert.Equal("Early entry", store.Mistake?.Description); Assert.Same(rows, viewModel.TradingMistakes);
        Assert.Equal("Trading mistake created.", viewModel.SuccessMessage); Assert.False(viewModel.IsCreateFormVisible); Assert.Equal(CancellationToken.None, reader.Token);
    }

    [Fact]
    public async Task DuplicateUsesStableMessage()
    {
        var checker = new FakeTradingMistakeNameChecker { Exists = true }; TradingMistakesViewModel viewModel = Create(checker: checker);
        viewModel.ShowCreateCommand.Execute(null); viewModel.NameText = "FOMO"; await viewModel.CreateCommand.ExecuteAsync(null);
        Assert.Equal(CreateTradingMistakeUseCase.DuplicateNameMessage, viewModel.ValidationErrorMessage); Assert.True(viewModel.IsCreateFormVisible);
    }

    [Fact]
    public async Task CreateReloadFailureRemainsSuccessfulWrite()
    {
        var reader = new FakeTradingMistakeReader(); reader.EnqueueException(new IOException("read")); TradingMistakesViewModel viewModel = Create(reader);
        viewModel.ShowCreateCommand.Execute(null); viewModel.NameText = "FOMO"; await viewModel.CreateCommand.ExecuteAsync(null);
        Assert.Equal("Trading mistake created.", viewModel.SuccessMessage); Assert.Equal("Trading mistakes could not be loaded.", viewModel.ListErrorMessage);
        Assert.Null(viewModel.SaveErrorMessage);
    }

    [Theory]
    [InlineData(true, false, "Trading mistake deactivated.")]
    [InlineData(false, true, "Trading mistake activated.")]
    public async Task ToggleUsesExactIdAndReloads(bool initial, bool desired, string message)
    {
        TradingMistakeListItem row = Item(initial); var mistake = TradingMistake.Rehydrate(row.Id, row.Name, null, initial, Timestamp, Timestamp);
        var store = new FakeTradingMistakeStore { Mistake = mistake }; TradingMistakeListItem[] rows = [row with { IsActive = desired }];
        var reader = new FakeTradingMistakeReader(); reader.EnqueueResult(rows); TradingMistakesViewModel viewModel = Create(reader, store);
        await viewModel.ToggleActiveCommand.ExecuteAsync(row);
        Assert.Equal(row.Id, store.RequestedId); Assert.Equal(desired, mistake.IsActive); Assert.Equal(1, store.UpdateCalls);
        Assert.Same(rows, viewModel.TradingMistakes); Assert.Equal(message, viewModel.SuccessMessage);
    }

    [Fact]
    public async Task MissingAndLifecycleFailureUseSafeMessages()
    {
        TradingMistakeListItem row = Item(true); var store = new FakeTradingMistakeStore(); TradingMistakesViewModel viewModel = Create(store: store);
        await viewModel.ToggleActiveCommand.ExecuteAsync(row);
        Assert.Equal("The selected trading mistake is no longer available.", viewModel.SaveErrorMessage);
        store.Mistake = TradingMistake.Rehydrate(row.Id, row.Name, null, true, Timestamp, Timestamp);
        store.UpdateException = new IOException("secret"); await viewModel.ToggleActiveCommand.ExecuteAsync(row);
        Assert.Equal("Trading mistake status could not be changed.", viewModel.SaveErrorMessage);
    }

    [Fact]
    public async Task LoadingGatesMutations()
    {
        var reader = new BlockingReader(); TradingMistakesViewModel viewModel = BuildWithBlockingReader(reader);
        Task load = viewModel.EnsureLoadedAsync(); await reader.Started.Task;
        Assert.True(viewModel.IsLoading); Assert.False(viewModel.ShowCreateCommand.CanExecute(null)); Assert.False(viewModel.ToggleActiveCommand.CanExecute(Item(true)));
        reader.Complete([]); await load;
    }

    [Fact]
    public async Task RefreshCancellationPropagatesToReader()
    {
        var reader = new BlockingReader(); TradingMistakesViewModel viewModel = BuildWithBlockingReader(reader);
        Task refresh = viewModel.RefreshCommand.ExecuteAsync(null); await reader.Started.Task; viewModel.RefreshCommand.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refresh);
    }

    private static TradingMistakesViewModel Create(FakeTradingMistakeReader? reader = null, FakeTradingMistakeStore? store = null,
        FakeTradingMistakeNameChecker? checker = null)
    {
        reader ??= new(); store ??= new(); checker ??= new(); var time = new FixedTimeProvider();
        return new(reader, new CreateTradingMistakeUseCase(store, checker, time), new TradingMistakeLifecycleUseCase(store, time));
    }
    private static TradingMistakesViewModel BuildWithBlockingReader(BlockingReader reader)
    {
        var time = new FixedTimeProvider(); var store = new FakeTradingMistakeStore();
        return new(reader, new CreateTradingMistakeUseCase(store, new FakeTradingMistakeNameChecker(), time), new TradingMistakeLifecycleUseCase(store, time));
    }
    private static TradingMistakeListItem Item(bool active) => new(Guid.NewGuid(), active ? "Active" : "Inactive", null, active, Timestamp, Timestamp);
    private sealed class BlockingReader : ITradingMistakeReader
    {
        private readonly TaskCompletionSource<IReadOnlyList<TradingMistakeListItem>> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IReadOnlyList<TradingMistakeListItem>> GetAllAsync(CancellationToken token = default)
        { Started.TrySetResult(); return _result.Task.WaitAsync(token); }
        public void Complete(IReadOnlyList<TradingMistakeListItem> rows) => _result.TrySetResult(rows);
    }
}
