using PersonalTradingJournal.Application.Mistakes;
using PersonalTradingJournal.Domain.Mistakes;

namespace PersonalTradingJournal.Application.Tests.Mistakes;

public sealed class TradingMistakeUseCaseTests
{
    private static readonly DateTimeOffset Created = new(2026, 9, 14, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = Created.AddDays(1);

    [Fact]
    public async Task CreateRejectsNullWithoutCallingStore()
    {
        var fixture = new Fixture();
        await Assert.ThrowsAsync<ArgumentNullException>(() => fixture.Create.ExecuteAsync(null!));
        Assert.Equal(0, fixture.Store.AddCalls);
    }

    [Fact]
    public async Task CreateUsesDomainNormalizationTimeAndPersists()
    {
        var fixture = new Fixture();
        Guid id = await fixture.Create.ExecuteAsync(new CreateTradingMistakeCommand("  FOMO  ", "   "));
        Assert.Equal(id, fixture.Store.Mistake?.Id);
        Assert.Equal("FOMO", fixture.Store.Mistake?.Name);
        Assert.Null(fixture.Store.Mistake?.Description);
        Assert.True(fixture.Store.Mistake?.IsActive);
        Assert.Equal(Now, fixture.Store.Mistake?.CreatedAtUtc);
        Assert.Equal("FOMO", fixture.Checker.Name);
    }

    [Theory]
    [InlineData("FOMO")]
    [InlineData("fomo")]
    public async Task CreateRejectsActiveOrInactiveDuplicateWithoutAdding(string name)
    {
        var fixture = new Fixture(); fixture.Checker.Exists = true;
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Create.ExecuteAsync(new CreateTradingMistakeCommand(name, null)));
        Assert.Equal(CreateTradingMistakeUseCase.DuplicateNameMessage, exception.Message);
        Assert.Equal(0, fixture.Store.AddCalls);
    }

    [Fact]
    public async Task CreateForwardsCancellationAndPropagatesPersistenceFailure()
    {
        var fixture = new Fixture(); var failure = new IOException("write failed"); fixture.Store.AddException = failure;
        using var source = new CancellationTokenSource();
        IOException actual = await Assert.ThrowsAsync<IOException>(() => fixture.Create.ExecuteAsync(
            new CreateTradingMistakeCommand("Moved Stop", "Execution error"), source.Token));
        Assert.Same(failure, actual);
        Assert.Equal(source.Token, fixture.Checker.Token);
        Assert.Equal(source.Token, fixture.Store.AddToken);
    }

    [Fact]
    public async Task LifecycleRejectsEmptyIdWithoutLoading()
    {
        var fixture = new Fixture();
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Lifecycle.ExecuteAsync(
            new SetTradingMistakeActiveStateCommand(Guid.Empty, false)));
        Assert.Equal(0, fixture.Store.GetCalls);
    }

    [Fact]
    public async Task LifecycleThrowsKeyNotFoundContainingId()
    {
        var fixture = new Fixture(); Guid id = Guid.NewGuid();
        KeyNotFoundException exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            fixture.Lifecycle.ExecuteAsync(new SetTradingMistakeActiveStateCommand(id, false)));
        Assert.Contains(id.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, fixture.Store.UpdateCalls);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task LifecycleChangesStateAtProvidedTimeAndPersists(bool initial, bool desired)
    {
        var fixture = new Fixture(); var mistake = new TradingMistake("Overtrading", null, Created);
        if (!initial) mistake.Deactivate(Created.AddHours(1)); fixture.Store.Mistake = mistake;
        await fixture.Lifecycle.ExecuteAsync(new SetTradingMistakeActiveStateCommand(mistake.Id, desired));
        Assert.Equal(desired, mistake.IsActive); Assert.Equal(Now, mistake.UpdatedAtUtc); Assert.Equal(1, fixture.Store.UpdateCalls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LifecycleSameStateIsSafeNoOp(bool active)
    {
        var fixture = new Fixture(); var mistake = new TradingMistake("Overtrading", null, Created);
        if (!active) mistake.Deactivate(Created.AddHours(1)); fixture.Store.Mistake = mistake;
        DateTimeOffset timestamp = mistake.UpdatedAtUtc;
        await fixture.Lifecycle.ExecuteAsync(new SetTradingMistakeActiveStateCommand(mistake.Id, active));
        Assert.Equal(timestamp, mistake.UpdatedAtUtc); Assert.Equal(0, fixture.Store.UpdateCalls);
    }

    [Fact]
    public async Task LifecycleForwardsCancellationAndPropagatesFailure()
    {
        var fixture = new Fixture(); fixture.Store.Mistake = new TradingMistake("Revenge Trading", null, Created);
        var failure = new IOException("update failed"); fixture.Store.UpdateException = failure; using var source = new CancellationTokenSource();
        IOException actual = await Assert.ThrowsAsync<IOException>(() => fixture.Lifecycle.ExecuteAsync(
            new SetTradingMistakeActiveStateCommand(fixture.Store.Mistake.Id, false), source.Token));
        Assert.Same(failure, actual); Assert.Equal(source.Token, fixture.Store.GetToken); Assert.Equal(source.Token, fixture.Store.UpdateToken);
    }

    private sealed class Fixture
    {
        public Store Store { get; } = new(); public Checker Checker { get; } = new();
        public CreateTradingMistakeUseCase Create { get; } public TradingMistakeLifecycleUseCase Lifecycle { get; }
        public Fixture() { var time = new FixedTime(Now); Create = new(Store, Checker, time); Lifecycle = new(Store, time); }
    }
    private sealed class Checker : ITradingMistakeNameChecker
    {
        public bool Exists { get; set; } public string? Name { get; private set; } public CancellationToken Token { get; private set; }
        public Task<bool> ExistsAsync(string name, Guid? excluded = null, CancellationToken token = default)
        { Name = name; Token = token; return Task.FromResult(Exists); }
    }
    private sealed class Store : ITradingMistakeStore
    {
        public TradingMistake? Mistake { get; set; } public Exception? AddException { get; set; } public Exception? UpdateException { get; set; }
        public int AddCalls { get; private set; } public int GetCalls { get; private set; } public int UpdateCalls { get; private set; }
        public CancellationToken AddToken { get; private set; } public CancellationToken GetToken { get; private set; } public CancellationToken UpdateToken { get; private set; }
        public Task AddAsync(TradingMistake item, CancellationToken token = default)
        { AddCalls++; AddToken = token; Mistake = item; return AddException is null ? Task.CompletedTask : Task.FromException(AddException); }
        public Task<TradingMistake?> GetByIdAsync(Guid id, CancellationToken token = default)
        { GetCalls++; GetToken = token; return Task.FromResult(Mistake); }
        public Task UpdateAsync(TradingMistake item, CancellationToken token = default)
        { UpdateCalls++; UpdateToken = token; return UpdateException is null ? Task.CompletedTask : Task.FromException(UpdateException); }
    }
    private sealed class FixedTime(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}
