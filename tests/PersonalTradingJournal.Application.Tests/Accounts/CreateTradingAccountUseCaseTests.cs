using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Domain.Accounts;

namespace PersonalTradingJournal.Application.Tests.Accounts;

public sealed class CreateTradingAccountUseCaseTests
{
    private static readonly DateTimeOffset CurrentUtc =
        new(2026, 9, 9, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExecuteAsyncCreatesNormalizedActiveAccountAndReturnsItsId()
    {
        var store = new RecordingTradingAccountStore();
        var useCase = new CreateTradingAccountUseCase(
            store,
            new FixedTimeProvider(CurrentUtc));
        var command = new CreateTradingAccountCommand(
            "  Primary Account  ",
            TradingAccountType.PropFunded,
            "  Funding Provider  ",
            "  ACCOUNT-42  ",
            " usd ",
            100000.25m);

        Guid accountId = await useCase.ExecuteAsync(command);

        TradingAccount account = Assert.IsType<TradingAccount>(store.AddedAccount);
        Assert.NotEqual(Guid.Empty, accountId);
        Assert.Equal(account.Id, accountId);
        Assert.Equal("Primary Account", account.Name);
        Assert.Equal(TradingAccountType.PropFunded, account.AccountType);
        Assert.Equal("Funding Provider", account.ProviderName);
        Assert.Equal("ACCOUNT-42", account.ExternalAccountId);
        Assert.Equal("USD", account.Currency);
        Assert.Equal(100000.25m, account.StartingBalance);
        Assert.True(account.IsActive);
        Assert.Equal(CurrentUtc, account.CreatedAtUtc);
        Assert.Equal(CurrentUtc, account.UpdatedAtUtc);
    }

    [Fact]
    public async Task ExecuteAsyncPreservesNullOptionalValues()
    {
        var store = new RecordingTradingAccountStore();
        var useCase = new CreateTradingAccountUseCase(
            store,
            new FixedTimeProvider(CurrentUtc));
        var command = new CreateTradingAccountCommand(
            "Personal",
            TradingAccountType.Personal,
            null,
            null,
            "EUR",
            null);

        await useCase.ExecuteAsync(command);

        TradingAccount account = Assert.IsType<TradingAccount>(store.AddedAccount);
        Assert.Null(account.ProviderName);
        Assert.Null(account.ExternalAccountId);
        Assert.Null(account.StartingBalance);
    }

    [Fact]
    public async Task ExecuteAsyncPropagatesDomainValidationAndDoesNotCallStore()
    {
        var store = new RecordingTradingAccountStore();
        var useCase = new CreateTradingAccountUseCase(
            store,
            new FixedTimeProvider(CurrentUtc));
        var command = new CreateTradingAccountCommand(
            "   ",
            TradingAccountType.Personal,
            null,
            null,
            "USD",
            null);

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(command));

        Assert.Equal(0, store.CallCount);
        Assert.Null(store.AddedAccount);
    }

    [Fact]
    public async Task ExecuteAsyncForwardsCancellationTokenToStore()
    {
        var store = new RecordingTradingAccountStore();
        var useCase = new CreateTradingAccountUseCase(
            store,
            new FixedTimeProvider(CurrentUtc));
        var command = new CreateTradingAccountCommand(
            "Personal",
            TradingAccountType.Personal,
            null,
            null,
            "USD",
            null);
        using var cancellationSource = new CancellationTokenSource();

        await useCase.ExecuteAsync(command, cancellationSource.Token);

        Assert.Equal(cancellationSource.Token, store.CancellationToken);
    }

    [Fact]
    public async Task ExecuteAsyncPropagatesStoreFailure()
    {
        var expectedException = new InvalidOperationException("Persistence failed.");
        var useCase = new CreateTradingAccountUseCase(
            new FailingTradingAccountStore(expectedException),
            new FixedTimeProvider(CurrentUtc));
        var command = new CreateTradingAccountCommand(
            "Personal",
            TradingAccountType.Personal,
            null,
            null,
            "USD",
            null);

        InvalidOperationException actualException =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => useCase.ExecuteAsync(command));

        Assert.Same(expectedException, actualException);
    }

    [Fact]
    public async Task ExecuteAsyncRejectsNullCommand()
    {
        var store = new RecordingTradingAccountStore();
        var useCase = new CreateTradingAccountUseCase(
            store,
            new FixedTimeProvider(CurrentUtc));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => useCase.ExecuteAsync(null!));

        Assert.Equal(0, store.CallCount);
    }

    private sealed class RecordingTradingAccountStore : ITradingAccountStore
    {
        public TradingAccount? AddedAccount { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public int CallCount { get; private set; }

        public Task AddAsync(
            TradingAccount account,
            CancellationToken cancellationToken = default)
        {
            AddedAccount = account;
            CancellationToken = cancellationToken;
            CallCount++;

            return Task.CompletedTask;
        }

        public Task<TradingAccount?> GetByIdAsync(
            Guid accountId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task UpdateAsync(
            TradingAccount account,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private sealed class FailingTradingAccountStore : ITradingAccountStore
    {
        private readonly Exception _exception;

        public FailingTradingAccountStore(Exception exception)
        {
            _exception = exception;
        }

        public Task AddAsync(
            TradingAccount account,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException(_exception);
        }

        public Task<TradingAccount?> GetByIdAsync(
            Guid accountId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task UpdateAsync(
            TradingAccount account,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
