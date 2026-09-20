using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Domain.Accounts;

namespace PersonalTradingJournal.Application.Tests.Accounts;

public sealed class TradingAccountManagementUseCaseTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 10, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset UpdatedAtUtc =
        new(2026, 9, 11, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetDetailsReturnsReaderProjection()
    {
        TradingAccountDetails details = CreateDetails();
        var reader = new RecordingReader(details);
        var useCase = new GetTradingAccountDetailsUseCase(reader);

        TradingAccountDetails? result = await useCase.ExecuteAsync(details.Id);

        Assert.Same(details, result);
        Assert.Equal(details.Id, reader.AccountId);
    }

    [Fact]
    public async Task GetDetailsReturnsNullWhenMissing()
    {
        var useCase = new GetTradingAccountDetailsUseCase(
            new RecordingReader(null));

        Assert.Null(await useCase.ExecuteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateChangesAndPersistsAuthoritativeAggregate()
    {
        TradingAccount account = CreateAccount();
        var store = new RecordingStore(account);
        var useCase = new UpdateTradingAccountUseCase(
            store,
            new FixedTimeProvider(UpdatedAtUtc));

        UpdateTradingAccountResult result = await useCase.ExecuteAsync(
            new UpdateTradingAccountCommand(
                account.Id,
                "  Funded Account  ",
                TradingAccountType.PropFunded,
                "  New Provider ",
                "   ",
                " eur ",
                100000m));

        Assert.True(result.WasChanged);
        Assert.Same(account, store.UpdatedAccount);
        Assert.Equal(1, store.UpdateCallCount);
        Assert.Equal("Funded Account", result.Account.Name);
        Assert.Equal("New Provider", result.Account.ProviderName);
        Assert.Null(result.Account.ExternalAccountId);
        Assert.Equal("EUR", result.Account.Currency);
        Assert.Equal(UpdatedAtUtc, result.Account.UpdatedAtUtc);
        Assert.Equal(CreatedAtUtc, result.Account.CreatedAtUtc);
    }

    [Fact]
    public async Task UpdateWithSameCanonicalValuesDoesNotPersist()
    {
        TradingAccount account = CreateAccount();
        var store = new RecordingStore(account);
        var useCase = new UpdateTradingAccountUseCase(
            store,
            new FixedTimeProvider(UpdatedAtUtc));

        UpdateTradingAccountResult result = await useCase.ExecuteAsync(
            new UpdateTradingAccountCommand(
                account.Id,
                " Primary ",
                TradingAccountType.Personal,
                " Provider ",
                " EXT-42 ",
                " usd ",
                25000m));

        Assert.False(result.WasChanged);
        Assert.Equal(0, store.UpdateCallCount);
        Assert.Equal(CreatedAtUtc, result.Account.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpdateMissingThrowsAndDoesNotPersist()
    {
        var store = new RecordingStore(null);
        var useCase = new UpdateTradingAccountUseCase(
            store,
            new FixedTimeProvider(UpdatedAtUtc));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => useCase.ExecuteAsync(
            new UpdateTradingAccountCommand(
                Guid.NewGuid(),
                "Primary",
                TradingAccountType.Personal,
                null,
                null,
                "USD",
                null)));

        Assert.Equal(0, store.UpdateCallCount);
    }

    [Fact]
    public async Task UpdatePropagatesDomainValidationWithoutPersisting()
    {
        TradingAccount account = CreateAccount();
        var store = new RecordingStore(account);
        var useCase = new UpdateTradingAccountUseCase(
            store,
            new FixedTimeProvider(UpdatedAtUtc));

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(
            new UpdateTradingAccountCommand(
                account.Id,
                " ",
                TradingAccountType.Personal,
                null,
                null,
                "USD",
                null)));

        Assert.Equal(0, store.UpdateCallCount);
    }

    [Fact]
    public async Task DeleteUnusedAccountSucceeds()
    {
        TradingAccount account = CreateAccount();
        var deletionStore = new RecordingDeletionStore();
        var useCase = new DeleteTradingAccountUseCase(
            new RecordingStore(account),
            deletionStore);

        DeleteTradingAccountResult result = await useCase.ExecuteAsync(account.Id);

        Assert.Equal(DeleteTradingAccountResult.Deleted, result);
        Assert.Equal(1, deletionStore.HasTradesCallCount);
        Assert.Equal(1, deletionStore.DeleteCallCount);
    }

    [Fact]
    public async Task DeleteReferencedAccountIsBlockedBeforeDelete()
    {
        TradingAccount account = CreateAccount();
        var deletionStore = new RecordingDeletionStore { HasTrades = true };
        var useCase = new DeleteTradingAccountUseCase(
            new RecordingStore(account),
            deletionStore);

        DeleteTradingAccountResult result = await useCase.ExecuteAsync(account.Id);

        Assert.Equal(DeleteTradingAccountResult.Referenced, result);
        Assert.Equal(0, deletionStore.DeleteCallCount);
    }

    [Fact]
    public async Task DeleteMissingAccountThrowsBeforeReferenceCheck()
    {
        var deletionStore = new RecordingDeletionStore();
        var useCase = new DeleteTradingAccountUseCase(
            new RecordingStore(null),
            deletionStore);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => useCase.ExecuteAsync(Guid.NewGuid()));

        Assert.Equal(0, deletionStore.HasTradesCallCount);
        Assert.Equal(0, deletionStore.DeleteCallCount);
    }

    [Fact]
    public async Task DeleteConstraintRaceReturnsReferencedResult()
    {
        TradingAccount account = CreateAccount();
        var deletionStore = new RecordingDeletionStore
        {
            DeleteException = new TradingAccountDeleteBlockedException("Referenced."),
        };
        var useCase = new DeleteTradingAccountUseCase(
            new RecordingStore(account),
            deletionStore);

        DeleteTradingAccountResult result = await useCase.ExecuteAsync(account.Id);

        Assert.Equal(DeleteTradingAccountResult.Referenced, result);
    }

    private static TradingAccount CreateAccount() => new(
        "Primary",
        TradingAccountType.Personal,
        "Provider",
        "EXT-42",
        "USD",
        25000m,
        CreatedAtUtc);

    private static TradingAccountDetails CreateDetails() => new(
        Guid.NewGuid(),
        "Primary",
        TradingAccountType.Personal,
        "Provider",
        "EXT-42",
        "USD",
        25000m,
        true,
        CreatedAtUtc,
        CreatedAtUtc);

    private sealed class RecordingReader : ITradingAccountReader
    {
        private readonly TradingAccountDetails? _details;

        public RecordingReader(TradingAccountDetails? details) => _details = details;

        public Guid AccountId { get; private set; }

        public Task<IReadOnlyList<AccountListItem>> GetAllAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TradingAccountDetails?> GetByIdAsync(
            Guid accountId,
            CancellationToken cancellationToken = default)
        {
            AccountId = accountId;
            return Task.FromResult(_details);
        }
    }

    private sealed class RecordingStore : ITradingAccountStore
    {
        private readonly TradingAccount? _account;

        public RecordingStore(TradingAccount? account) => _account = account;

        public TradingAccount? UpdatedAccount { get; private set; }

        public int UpdateCallCount { get; private set; }

        public Task AddAsync(
            TradingAccount account,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TradingAccount?> GetByIdAsync(
            Guid accountId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_account?.Id == accountId ? _account : null);

        public Task UpdateAsync(
            TradingAccount account,
            CancellationToken cancellationToken = default)
        {
            UpdateCallCount++;
            UpdatedAccount = account;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingDeletionStore : ITradingAccountDeletionStore
    {
        public bool HasTrades { get; init; }

        public Exception? DeleteException { get; init; }

        public int HasTradesCallCount { get; private set; }

        public int DeleteCallCount { get; private set; }

        public Task<bool> HasTradesAsync(
            Guid accountId,
            CancellationToken cancellationToken = default)
        {
            HasTradesCallCount++;
            return Task.FromResult(HasTrades);
        }

        public Task DeleteAsync(
            Guid accountId,
            CancellationToken cancellationToken = default)
        {
            DeleteCallCount++;
            return DeleteException is null
                ? Task.CompletedTask
                : Task.FromException(DeleteException);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
