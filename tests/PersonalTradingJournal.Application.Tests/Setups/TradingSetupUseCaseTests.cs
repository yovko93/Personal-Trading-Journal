using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Application.Tests.Setups;

public sealed class TradingSetupUseCaseTests
{
    private static readonly DateTimeOffset Created = new(2026, 9, 14, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = Created.AddDays(1);

    [Fact]
    public async Task CreateRejectsNull()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<ArgumentNullException>(() => f.Create.ExecuteAsync(null!));
        Assert.Equal(0, f.Store.AddCalls);
    }

    [Fact]
    public async Task CreateUsesDomainNormalizationTimeAndPersists()
    {
        var f = new Fixture();
        Guid id = await f.Create.ExecuteAsync(new CreateTradingSetupCommand("  Silver Bullet  ", "   "));
        Assert.Equal(id, f.Store.Setup?.Id);
        Assert.Equal("Silver Bullet", f.Store.Setup?.Name);
        Assert.Null(f.Store.Setup?.Description);
        Assert.Equal(Now, f.Store.Setup?.CreatedAtUtc);
        Assert.Equal("Silver Bullet", f.Checker.Name);
    }

    [Theory]
    [InlineData("Silver Bullet")]
    [InlineData("silver bullet")]
    public async Task CreateRejectsActiveOrInactiveDuplicateWithoutAdding(string name)
    {
        var f = new Fixture(); f.Checker.Exists = true;
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Create.ExecuteAsync(new CreateTradingSetupCommand(name, null)));
        Assert.Equal(CreateTradingSetupUseCase.DuplicateNameMessage, ex.Message);
        Assert.Equal(0, f.Store.AddCalls);
    }

    [Fact]
    public async Task CreateForwardsCancellationAndPropagatesFailure()
    {
        var f = new Fixture(); var expected = new IOException("failure"); f.Store.AddException = expected;
        using var source = new CancellationTokenSource();
        IOException actual = await Assert.ThrowsAsync<IOException>(() => f.Create.ExecuteAsync(
            new CreateTradingSetupCommand("ORB", "Opening range"), source.Token));
        Assert.Same(expected, actual); Assert.Equal(source.Token, f.Checker.Token); Assert.Equal(source.Token, f.Store.AddToken);
    }

    [Fact]
    public async Task LifecycleRejectsEmptyAndMissing()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<ArgumentException>(() => f.Lifecycle.ExecuteAsync(
            new SetTradingSetupActiveStateCommand(Guid.Empty, false)));
        Guid id = Guid.NewGuid();
        KeyNotFoundException ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => f.Lifecycle.ExecuteAsync(
            new SetTradingSetupActiveStateCommand(id, false)));
        Assert.Contains(id.ToString(), ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task LifecycleChangesStateAndPersists(bool initial, bool desired)
    {
        var f = new Fixture();
        var setup = new TradingSetup("Pullback", null, Created);
        if (!initial) setup.Deactivate(Created.AddHours(1));
        f.Store.Setup = setup;
        await f.Lifecycle.ExecuteAsync(new SetTradingSetupActiveStateCommand(setup.Id, desired));
        Assert.Equal(desired, setup.IsActive); Assert.Equal(Now, setup.UpdatedAtUtc); Assert.Equal(1, f.Store.UpdateCalls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LifecycleSameStateIsSafeNoOp(bool active)
    {
        var f = new Fixture(); var setup = new TradingSetup("CRT", null, Created);
        if (!active) setup.Deactivate(Created.AddHours(1));
        f.Store.Setup = setup; DateTimeOffset timestamp = setup.UpdatedAtUtc;
        await f.Lifecycle.ExecuteAsync(new SetTradingSetupActiveStateCommand(setup.Id, active));
        Assert.Equal(timestamp, setup.UpdatedAtUtc); Assert.Equal(0, f.Store.UpdateCalls);
    }

    [Fact]
    public async Task LifecycleForwardsCancellationAndPropagatesUpdateFailure()
    {
        var f = new Fixture(); f.Store.Setup = new TradingSetup("CRT", null, Created);
        var expected = new IOException("failure"); f.Store.UpdateException = expected;
        using var source = new CancellationTokenSource();
        IOException actual = await Assert.ThrowsAsync<IOException>(() => f.Lifecycle.ExecuteAsync(
            new SetTradingSetupActiveStateCommand(f.Store.Setup.Id, false), source.Token));
        Assert.Same(expected, actual); Assert.Equal(source.Token, f.Store.GetToken); Assert.Equal(source.Token, f.Store.UpdateToken);
    }

    private sealed class Fixture
    {
        public Store Store { get; } = new(); public Checker Checker { get; } = new();
        public CreateTradingSetupUseCase Create { get; } public TradingSetupLifecycleUseCase Lifecycle { get; }
        public Fixture() { var time = new FixedTime(Now); Create = new(Store, Checker, time); Lifecycle = new(Store, time); }
    }
    private sealed class Checker : ITradingSetupNameChecker
    {
        public bool Exists { get; set; } public string? Name { get; private set; } public CancellationToken Token { get; private set; }
        public Task<bool> ExistsAsync(string name, Guid? excludingSetupId = null, CancellationToken cancellationToken = default)
        { Name = name; Token = cancellationToken; return Task.FromResult(Exists); }
    }
    private sealed class Store : ITradingSetupStore
    {
        public TradingSetup? Setup { get; set; } public Exception? AddException { get; set; } public Exception? UpdateException { get; set; }
        public int AddCalls { get; private set; } public int UpdateCalls { get; private set; }
        public CancellationToken AddToken { get; private set; } public CancellationToken GetToken { get; private set; } public CancellationToken UpdateToken { get; private set; }
        public Task AddAsync(TradingSetup setup, CancellationToken token = default)
        { AddCalls++; Setup = setup; AddToken = token; return AddException is null ? Task.CompletedTask : Task.FromException(AddException); }
        public Task<TradingSetup?> GetByIdAsync(Guid id, CancellationToken token = default)
        { GetToken = token; return Task.FromResult(Setup); }
        public Task UpdateAsync(TradingSetup setup, CancellationToken token = default)
        { UpdateCalls++; UpdateToken = token; return UpdateException is null ? Task.CompletedTask : Task.FromException(UpdateException); }
    }
    private sealed class FixedTime(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}
