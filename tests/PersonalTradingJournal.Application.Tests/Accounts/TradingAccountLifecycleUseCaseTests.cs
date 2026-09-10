using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Domain.Accounts;

namespace PersonalTradingJournal.Application.Tests.Accounts;

public sealed class TradingAccountLifecycleUseCaseTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 8, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CurrentUtc =
        new(2026, 9, 9, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ActivateAsyncActivatesInactiveAccountAndPersistsIt()
    {
        TradingAccount account = CreateAccount();
        account.Deactivate(CreatedAtUtc.AddHours(1));
        var store = new RecordingTradingAccountStore(account);
        var timeProvider = new RecordingTimeProvider(CurrentUtc);
        var useCase = new TradingAccountLifecycleUseCase(store, timeProvider);

        await useCase.ActivateAsync(account.Id);

        TradingAccount updatedAccount = Assert.IsType<TradingAccount>(store.UpdatedAccount);
        Assert.Same(account, updatedAccount);
        Assert.True(updatedAccount.IsActive);
        Assert.Equal(CurrentUtc, updatedAccount.UpdatedAtUtc);
        Assert.Equal(1, store.UpdateCallCount);
        Assert.Equal(1, timeProvider.CallCount);
    }

    [Fact]
    public async Task DeactivateAsyncDeactivatesActiveAccountAndPersistsIt()
    {
        TradingAccount account = CreateAccount();
        var store = new RecordingTradingAccountStore(account);
        var timeProvider = new RecordingTimeProvider(CurrentUtc);
        var useCase = new TradingAccountLifecycleUseCase(store, timeProvider);

        await useCase.DeactivateAsync(account.Id);

        TradingAccount updatedAccount = Assert.IsType<TradingAccount>(store.UpdatedAccount);
        Assert.Same(account, updatedAccount);
        Assert.False(updatedAccount.IsActive);
        Assert.Equal(CurrentUtc, updatedAccount.UpdatedAtUtc);
        Assert.Equal(1, store.UpdateCallCount);
        Assert.Equal(1, timeProvider.CallCount);
    }

    [Fact]
    public async Task ActivateAsyncDoesNotWriteOrObtainTimeWhenAccountIsAlreadyActive()
    {
        TradingAccount account = CreateAccount();
        DateTimeOffset originalUpdatedAtUtc = account.UpdatedAtUtc;
        var store = new RecordingTradingAccountStore(account);
        var timeProvider = new RecordingTimeProvider(CurrentUtc);
        var useCase = new TradingAccountLifecycleUseCase(store, timeProvider);

        await useCase.ActivateAsync(account.Id);

        Assert.Equal(originalUpdatedAtUtc, account.UpdatedAtUtc);
        Assert.Equal(0, store.UpdateCallCount);
        Assert.Equal(0, timeProvider.CallCount);
    }

    [Fact]
    public async Task DeactivateAsyncDoesNotWriteOrObtainTimeWhenAccountIsAlreadyInactive()
    {
        TradingAccount account = CreateAccount();
        DateTimeOffset originalUpdatedAtUtc = CreatedAtUtc.AddHours(1);
        account.Deactivate(originalUpdatedAtUtc);
        var store = new RecordingTradingAccountStore(account);
        var timeProvider = new RecordingTimeProvider(CurrentUtc);
        var useCase = new TradingAccountLifecycleUseCase(store, timeProvider);

        await useCase.DeactivateAsync(account.Id);

        Assert.Equal(originalUpdatedAtUtc, account.UpdatedAtUtc);
        Assert.Equal(0, store.UpdateCallCount);
        Assert.Equal(0, timeProvider.CallCount);
    }

    [Fact]
    public async Task ActivateAsyncThrowsWhenAccountDoesNotExist()
    {
        Guid accountId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var store = new RecordingTradingAccountStore(account: null);
        var useCase = new TradingAccountLifecycleUseCase(
            store,
            new RecordingTimeProvider(CurrentUtc));

        KeyNotFoundException exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => useCase.ActivateAsync(accountId));

        Assert.Contains(accountId.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, store.GetCallCount);
        Assert.Equal(0, store.UpdateCallCount);
    }

    [Fact]
    public async Task LifecycleMethodsRejectEmptyIdWithoutQueryingStore()
    {
        var store = new RecordingTradingAccountStore(CreateAccount());
        var useCase = new TradingAccountLifecycleUseCase(
            store,
            new RecordingTimeProvider(CurrentUtc));

        ArgumentException activateException = await Assert.ThrowsAsync<ArgumentException>(
            () => useCase.ActivateAsync(Guid.Empty));
        ArgumentException deactivateException = await Assert.ThrowsAsync<ArgumentException>(
            () => useCase.DeactivateAsync(Guid.Empty));

        Assert.Equal("accountId", activateException.ParamName);
        Assert.Equal("accountId", deactivateException.ParamName);
        Assert.Equal(0, store.GetCallCount);
        Assert.Equal(0, store.UpdateCallCount);
    }

    [Fact]
    public async Task DeactivateAsyncForwardsCancellationTokenToLoadAndUpdate()
    {
        TradingAccount account = CreateAccount();
        var store = new RecordingTradingAccountStore(account);
        var useCase = new TradingAccountLifecycleUseCase(
            store,
            new RecordingTimeProvider(CurrentUtc));
        using var cancellationSource = new CancellationTokenSource();

        await useCase.DeactivateAsync(account.Id, cancellationSource.Token);

        Assert.Equal(cancellationSource.Token, store.GetCancellationToken);
        Assert.Equal(cancellationSource.Token, store.UpdateCancellationToken);
    }

    [Fact]
    public async Task DeactivateAsyncPropagatesLoadFailureWithoutUpdating()
    {
        var expectedException = new InvalidOperationException("Load failed.");
        var store = new RecordingTradingAccountStore(
            CreateAccount(),
            loadException: expectedException);
        var useCase = new TradingAccountLifecycleUseCase(
            store,
            new RecordingTimeProvider(CurrentUtc));

        InvalidOperationException actualException =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => useCase.DeactivateAsync(store.Account!.Id));

        Assert.Same(expectedException, actualException);
        Assert.Equal(0, store.UpdateCallCount);
    }

    [Fact]
    public async Task DeactivateAsyncPropagatesUpdateFailure()
    {
        var expectedException = new InvalidOperationException("Update failed.");
        var store = new RecordingTradingAccountStore(
            CreateAccount(),
            updateException: expectedException);
        var useCase = new TradingAccountLifecycleUseCase(
            store,
            new RecordingTimeProvider(CurrentUtc));

        InvalidOperationException actualException =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => useCase.DeactivateAsync(store.Account!.Id));

        Assert.Same(expectedException, actualException);
        Assert.Equal(1, store.UpdateCallCount);
    }

    private static TradingAccount CreateAccount()
    {
        return new TradingAccount(
            "Primary",
            TradingAccountType.Personal,
            "Provider",
            "ACCOUNT-42",
            "USD",
            25000m,
            CreatedAtUtc);
    }

    private sealed class RecordingTradingAccountStore : ITradingAccountStore
    {
        private readonly Exception? _loadException;
        private readonly Exception? _updateException;

        public RecordingTradingAccountStore(
            TradingAccount? account,
            Exception? loadException = null,
            Exception? updateException = null)
        {
            Account = account;
            _loadException = loadException;
            _updateException = updateException;
        }

        public TradingAccount? Account { get; }

        public TradingAccount? UpdatedAccount { get; private set; }

        public CancellationToken GetCancellationToken { get; private set; }

        public CancellationToken UpdateCancellationToken { get; private set; }

        public int GetCallCount { get; private set; }

        public int UpdateCallCount { get; private set; }

        public Task AddAsync(
            TradingAccount account,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<TradingAccount?> GetByIdAsync(
            Guid accountId,
            CancellationToken cancellationToken = default)
        {
            GetCallCount++;
            GetCancellationToken = cancellationToken;

            return _loadException is null
                ? Task.FromResult(Account)
                : Task.FromException<TradingAccount?>(_loadException);
        }

        public Task UpdateAsync(
            TradingAccount account,
            CancellationToken cancellationToken = default)
        {
            UpdateCallCount++;
            UpdatedAccount = account;
            UpdateCancellationToken = cancellationToken;

            return _updateException is null
                ? Task.CompletedTask
                : Task.FromException(_updateException);
        }
    }

    private sealed class RecordingTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public RecordingTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public int CallCount { get; private set; }

        public override DateTimeOffset GetUtcNow()
        {
            CallCount++;
            return _utcNow;
        }
    }
}
