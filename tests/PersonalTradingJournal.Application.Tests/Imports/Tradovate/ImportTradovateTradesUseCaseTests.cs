using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Tests.Imports.Tradovate;

public sealed class ImportTradovateTradesUseCaseTests
{
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid InstrumentId = Guid.NewGuid();
    private static readonly DateTimeOffset ImportedAt =
        new(2026, 9, 23, 15, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ImportAsyncFreshlyPreparesAndForwardsOneUtcTimestampAndToken()
    {
        var reader = new AccountReader(Account());
        var store = new RecordingStore();
        var useCase = new ImportTradovateTradesUseCase(
            new TradovateImportPreparationService(reader),
            store,
            new FixedTimeProvider(ImportedAt));
        using var source = new CancellationTokenSource();

        TradovateImportResult result = await useCase.ImportAsync(
            Reconstruction(), ExistingResolution(), AccountId, source.Token);

        Assert.Equal(TradovateImportStatus.Imported, result.Status);
        Assert.NotNull(store.Request);
        Assert.True(store.Request.Preparation.IsReadyForPreview);
        Assert.Equal(ImportedAt, store.Request.ImportedAtUtc);
        Assert.Equal(source.Token, reader.Token);
        Assert.Equal(source.Token, store.Token);
    }

    [Fact]
    public async Task ImportAsyncDoesNotCallStoreWhenFreshPreparationIsBlocked()
    {
        var store = new RecordingStore();
        var useCase = new ImportTradovateTradesUseCase(
            new TradovateImportPreparationService(new AccountReader(null)),
            store,
            new FixedTimeProvider(ImportedAt));

        TradovateImportResult result = await useCase.ImportAsync(
            Reconstruction(), ExistingResolution(), AccountId);

        Assert.Equal(TradovateImportStatus.Blocked, result.Status);
        Assert.Equal(
            TradovateImportPreparationDiagnosticCodes.TradingAccountNotFound,
            result.ConflictCode);
        Assert.Null(store.Request);
    }

    [Fact]
    public async Task ImportAsyncPropagatesStoreFailure()
    {
        var expected = new InvalidOperationException("database failed");
        var useCase = new ImportTradovateTradesUseCase(
            new TradovateImportPreparationService(new AccountReader(Account())),
            new RecordingStore(expected),
            new FixedTimeProvider(ImportedAt));

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ImportAsync(
                Reconstruction(), ExistingResolution(), AccountId));

        Assert.Same(expected, actual);
    }

    private static TradingAccountDetails Account() => new(
        AccountId,
        "Tradovate",
        TradingAccountType.Personal,
        "Tradovate",
        "SIM-1",
        "USD",
        null,
        true,
        ImportedAt.AddDays(-1),
        ImportedAt.AddDays(-1));

    private static TradovateExecutionReconstructionResult Reconstruction()
    {
        DateTime opened = new(2026, 9, 23, 17, 0, 0, DateTimeKind.Unspecified);
        TradovateReconstructedExecution entry = Execution(
            ExecutionSide.Buy, "BUY-1", 100m, opened);
        TradovateReconstructedExecution exit = Execution(
            ExecutionSide.Sell, "SELL-1", 101m, opened.AddMinutes(1));
        return new TradovateExecutionReconstructionResult(
            [entry, exit],
            [new TradovateTradeCandidate(
                "MNQU6", TradeDirection.Long, [entry, exit], opened,
                opened.AddMinutes(1), 1m, 1m, 0m,
                TradovateReconstructionStatus.Reconstructed, [1], [])],
            [], [], [], 1, TradovateReconstructionStatus.Reconstructed);
    }

    private static TradovateReconstructedExecution Execution(
        ExecutionSide side,
        string id,
        decimal price,
        DateTime timestamp) =>
        new("MNQU6", side, id, 1m, price, timestamp, 0.25m, [1], [2]);

    private static TradovateInstrumentResolutionResult ExistingResolution()
    {
        var snapshot = new TradovateExistingInstrumentSnapshot(
            "Micro Nasdaq", AssetClass.Futures, "CME", "USD", 0.25m, 0.50m);
        return new TradovateInstrumentResolutionResult(
            [new TradovateBrokerSymbolMapping(
                "MNQU6", "MNQ", TradovateInstrumentResolutionStatus.ExistingInstrument,
                InstrumentId, true, [])],
            [new TradovateCanonicalInstrumentResolution(
                "MNQ", ["MNQU6"], 0.25m,
                TradovateInstrumentResolutionStatus.ExistingInstrument,
                InstrumentId, true, [InstrumentId], null, [], "USD", snapshot)],
            [],
            TradovateInstrumentResolutionOverallStatus.ReadyForPreview);
    }

    private sealed class AccountReader(TradingAccountDetails? account)
        : ITradingAccountReader
    {
        public CancellationToken Token { get; private set; }

        public Task<IReadOnlyList<AccountListItem>> GetAllAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TradingAccountDetails?> GetByIdAsync(
            Guid accountId,
            CancellationToken cancellationToken = default)
        {
            Token = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(account);
        }
    }

    private sealed class RecordingStore(Exception? exception = null)
        : ITradovateImportStore
    {
        public TradovateImportRequest? Request { get; private set; }

        public CancellationToken Token { get; private set; }

        public Task<TradovateImportResult> ImportAsync(
            TradovateImportRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            Token = cancellationToken;
            if (exception is not null)
            {
                throw exception;
            }

            return Task.FromResult(new TradovateImportResult(
                TradovateImportStatus.Imported, 1, 0, 0,
                [Guid.NewGuid()], [], []));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
