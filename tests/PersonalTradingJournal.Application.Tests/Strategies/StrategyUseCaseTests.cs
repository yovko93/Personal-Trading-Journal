using PersonalTradingJournal.Application.Strategies;
using PersonalTradingJournal.Domain.Strategies;

namespace PersonalTradingJournal.Application.Tests.Strategies;

public sealed class StrategyUseCaseTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CurrentUtc = CreatedAtUtc.AddDays(1);

    [Fact]
    public async Task CreateRejectsNullCommandWithoutCallingDependencies()
    {
        var fixture = new Fixture();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => fixture.Create.ExecuteAsync(null!));

        Assert.Equal(0, fixture.Checker.CallCount);
        Assert.Equal(0, fixture.Store.AddCallCount);
    }

    [Fact]
    public async Task CreateUsesDomainNormalizationTimestampAndPersistsStrategy()
    {
        var fixture = new Fixture();

        Guid id = await fixture.Create.ExecuteAsync(
            new CreateStrategyCommand("  ICT  ", "   "));

        Strategy strategy = Assert.IsType<Strategy>(fixture.Store.Strategy);
        Assert.Equal(strategy.Id, id);
        Assert.Equal("ICT", strategy.Name);
        Assert.Null(strategy.Description);
        Assert.True(strategy.IsActive);
        Assert.Equal(CurrentUtc, strategy.CreatedAtUtc);
        Assert.Equal(CurrentUtc, strategy.UpdatedAtUtc);
        Assert.Equal("ICT", fixture.Checker.Name);
        Assert.Equal(1, fixture.Store.AddCallCount);
    }

    [Theory]
    [InlineData("ICT")]
    [InlineData("ict")]
    public async Task CreateRejectsDuplicateIncludingInactiveAndDoesNotPersist(string name)
    {
        var fixture = new Fixture { Checker = { Exists = true } };

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => fixture.Create.ExecuteAsync(
                    new CreateStrategyCommand(name, null)));

        Assert.Equal(CreateStrategyUseCase.DuplicateNameMessage, exception.Message);
        Assert.Equal(0, fixture.Store.AddCallCount);
    }

    [Fact]
    public async Task CreateForwardsCancellationAndPropagatesPersistenceFailure()
    {
        var fixture = new Fixture();
        var failure = new IOException("Write failed.");
        fixture.Store.AddException = failure;
        using var source = new CancellationTokenSource();

        IOException actual = await Assert.ThrowsAsync<IOException>(
            () => fixture.Create.ExecuteAsync(
                new CreateStrategyCommand("Breakout", "Range expansion"),
                source.Token));

        Assert.Same(failure, actual);
        Assert.Equal(source.Token, fixture.Checker.Token);
        Assert.Equal(source.Token, fixture.Store.AddToken);
    }

    [Fact]
    public async Task LifecycleRejectsEmptyIdWithoutLoading()
    {
        var fixture = new Fixture();

        await Assert.ThrowsAsync<ArgumentException>(
            () => fixture.Lifecycle.ExecuteAsync(
                new SetStrategyActiveStateCommand(Guid.Empty, false)));

        Assert.Equal(0, fixture.Store.GetCallCount);
    }

    [Fact]
    public async Task LifecycleThrowsKeyNotFoundWithIdWhenMissing()
    {
        var fixture = new Fixture();
        Guid id = Guid.NewGuid();

        KeyNotFoundException exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => fixture.Lifecycle.ExecuteAsync(
                new SetStrategyActiveStateCommand(id, false)));

        Assert.Contains(id.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, fixture.Store.UpdateCallCount);
    }

    [Fact]
    public async Task LifecycleDeactivatesAndPersistsAtProvidedTime()
    {
        var fixture = new Fixture();
        fixture.Store.Strategy = new Strategy("Trend Following", null, CreatedAtUtc);

        await fixture.Lifecycle.ExecuteAsync(
            new SetStrategyActiveStateCommand(fixture.Store.Strategy.Id, false));

        Assert.False(fixture.Store.Strategy.IsActive);
        Assert.Equal(CurrentUtc, fixture.Store.Strategy.UpdatedAtUtc);
        Assert.Equal(1, fixture.Store.UpdateCallCount);
        Assert.Equal(1, fixture.Time.CallCount);
    }

    [Fact]
    public async Task LifecycleActivatesAndPersistsAtProvidedTime()
    {
        var fixture = new Fixture();
        var strategy = new Strategy("Mean Reversion", null, CreatedAtUtc);
        strategy.Deactivate(CreatedAtUtc.AddHours(1));
        fixture.Store.Strategy = strategy;

        await fixture.Lifecycle.ExecuteAsync(
            new SetStrategyActiveStateCommand(strategy.Id, true));

        Assert.True(strategy.IsActive);
        Assert.Equal(CurrentUtc, strategy.UpdatedAtUtc);
        Assert.Equal(1, fixture.Store.UpdateCallCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LifecycleSameStateDoesNotChangeTimestampOrPersist(bool active)
    {
        var fixture = new Fixture();
        var strategy = new Strategy("Breakout", null, CreatedAtUtc);
        if (!active)
        {
            strategy.Deactivate(CreatedAtUtc.AddHours(1));
        }

        fixture.Store.Strategy = strategy;
        DateTimeOffset previousTimestamp = strategy.UpdatedAtUtc;

        await fixture.Lifecycle.ExecuteAsync(
            new SetStrategyActiveStateCommand(strategy.Id, active));

        Assert.Equal(previousTimestamp, strategy.UpdatedAtUtc);
        Assert.Equal(0, fixture.Store.UpdateCallCount);
        Assert.Equal(0, fixture.Time.CallCount);
    }

    [Fact]
    public async Task LifecycleForwardsCancellationAndPropagatesUpdateFailure()
    {
        var fixture = new Fixture();
        fixture.Store.Strategy = new Strategy("Breakout", null, CreatedAtUtc);
        var failure = new IOException("Update failed.");
        fixture.Store.UpdateException = failure;
        using var source = new CancellationTokenSource();

        IOException actual = await Assert.ThrowsAsync<IOException>(
            () => fixture.Lifecycle.ExecuteAsync(
                new SetStrategyActiveStateCommand(fixture.Store.Strategy.Id, false),
                source.Token));

        Assert.Same(failure, actual);
        Assert.Equal(source.Token, fixture.Store.GetToken);
        Assert.Equal(source.Token, fixture.Store.UpdateToken);
    }

    [Fact]
    public async Task LifecyclePropagatesLoadFailureWithoutUpdating()
    {
        var fixture = new Fixture();
        var failure = new IOException("Load failed.");
        fixture.Store.GetException = failure;

        IOException actual = await Assert.ThrowsAsync<IOException>(
            () => fixture.Lifecycle.ExecuteAsync(
                new SetStrategyActiveStateCommand(Guid.NewGuid(), false)));

        Assert.Same(failure, actual);
        Assert.Equal(0, fixture.Store.UpdateCallCount);
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Create = new CreateStrategyUseCase(Store, Checker, Time);
            Lifecycle = new StrategyLifecycleUseCase(Store, Time);
        }

        public FakeStore Store { get; } = new();
        public FakeChecker Checker { get; } = new();
        public RecordingTimeProvider Time { get; } = new(CurrentUtc);
        public CreateStrategyUseCase Create { get; }
        public StrategyLifecycleUseCase Lifecycle { get; }
    }

    private sealed class FakeChecker : IStrategyNameChecker
    {
        public bool Exists { get; set; }
        public int CallCount { get; private set; }
        public string? Name { get; private set; }
        public CancellationToken Token { get; private set; }

        public Task<bool> ExistsAsync(string normalizedName, Guid? excludingStrategyId = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Name = normalizedName;
            Token = cancellationToken;
            return Task.FromResult(Exists);
        }
    }

    private sealed class FakeStore : IStrategyStore
    {
        public Strategy? Strategy { get; set; }
        public Exception? AddException { get; set; }
        public Exception? GetException { get; set; }
        public Exception? UpdateException { get; set; }
        public int AddCallCount { get; private set; }
        public int GetCallCount { get; private set; }
        public int UpdateCallCount { get; private set; }
        public CancellationToken AddToken { get; private set; }
        public CancellationToken GetToken { get; private set; }
        public CancellationToken UpdateToken { get; private set; }

        public Task AddAsync(Strategy strategy, CancellationToken cancellationToken = default)
        {
            AddCallCount++;
            AddToken = cancellationToken;
            Strategy = strategy;
            return AddException is null ? Task.CompletedTask : Task.FromException(AddException);
        }

        public Task<Strategy?> GetByIdAsync(Guid strategyId, CancellationToken cancellationToken = default)
        {
            GetCallCount++;
            GetToken = cancellationToken;
            return GetException is null
                ? Task.FromResult(Strategy)
                : Task.FromException<Strategy?>(GetException);
        }

        public Task UpdateAsync(Strategy strategy, CancellationToken cancellationToken = default)
        {
            UpdateCallCount++;
            UpdateToken = cancellationToken;
            Strategy = strategy;
            return UpdateException is null ? Task.CompletedTask : Task.FromException(UpdateException);
        }
    }

    private sealed class RecordingTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public int CallCount { get; private set; }

        public override DateTimeOffset GetUtcNow()
        {
            CallCount++;
            return utcNow;
        }
    }
}
