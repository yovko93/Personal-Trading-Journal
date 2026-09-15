using PersonalTradingJournal.Application.Mistakes;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Mistakes;

namespace PersonalTradingJournal.Application.Tests.Mistakes;

public sealed class AssignTradeMistakeUseCaseTests
{
    private static readonly DateTimeOffset CurrentUtc =
        new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExecuteAsyncRejectsNullCommandBeforeAccessingDependencies()
    {
        AssignmentFixture fixture = CreateFixture();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => fixture.UseCase.ExecuteAsync(null!));

        Assert.Equal(0, fixture.TradeReader.CallCount);
        Assert.Equal(0, fixture.TradeMistakeStore.AddCallCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteAsyncRejectsEmptyIdentifiersBeforeAccessingDependencies(
        bool emptyTradeId)
    {
        AssignmentFixture fixture = CreateFixture();
        var command = new AssignTradeMistakeCommand(
            emptyTradeId ? Guid.Empty : Guid.NewGuid(),
            emptyTradeId ? Guid.NewGuid() : Guid.Empty,
            null);

        await Assert.ThrowsAsync<ArgumentException>(
            () => fixture.UseCase.ExecuteAsync(command));

        Assert.Equal(0, fixture.TradeReader.CallCount);
        Assert.Equal(0, fixture.TradeMistakeStore.AddCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncRejectsMissingTradeBeforeLoadingMistake()
    {
        AssignmentFixture fixture = CreateFixture(tradeExists: false);

        KeyNotFoundException exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => fixture.UseCase.ExecuteAsync(
                new(Guid.NewGuid(), Guid.NewGuid(), null)));

        Assert.Equal(AssignTradeMistakeUseCase.MissingTradeMessage, exception.Message);
        Assert.Equal(0, fixture.TradingMistakeStore.GetCallCount);
        Assert.Equal(0, fixture.TradeMistakeStore.AddCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncRejectsMissingMistakeWithoutAdding()
    {
        AssignmentFixture fixture = CreateFixture(missingMistake: true);

        KeyNotFoundException exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => fixture.UseCase.ExecuteAsync(
                new(Guid.NewGuid(), Guid.NewGuid(), null)));

        Assert.Equal(AssignTradeMistakeUseCase.MissingMistakeMessage, exception.Message);
        Assert.Equal(0, fixture.TradeMistakeStore.ExistsCallCount);
        Assert.Equal(0, fixture.TradeMistakeStore.AddCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncRejectsInactiveMistakeWithoutAdding()
    {
        TradingMistake mistake = CreateMistake(active: false);
        AssignmentFixture fixture = CreateFixture(mistake: mistake);

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => fixture.UseCase.ExecuteAsync(
                    new(Guid.NewGuid(), mistake.Id, null)));

        Assert.Equal(AssignTradeMistakeUseCase.InactiveMistakeMessage, exception.Message);
        Assert.Equal(0, fixture.TradeMistakeStore.ExistsCallCount);
        Assert.Equal(0, fixture.TradeMistakeStore.AddCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncRejectsDuplicatePairWithoutAdding()
    {
        TradingMistake mistake = CreateMistake();
        AssignmentFixture fixture = CreateFixture(
            mistake: mistake,
            associationExists: true);

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => fixture.UseCase.ExecuteAsync(
                    new(Guid.NewGuid(), mistake.Id, null)));

        Assert.Equal(AssignTradeMistakeUseCase.DuplicateMistakeMessage, exception.Message);
        Assert.Equal(0, fixture.TradeMistakeStore.AddCallCount);
    }

    [Theory]
    [InlineData("  Chased the move after missing confirmation.  ", "Chased the move after missing confirmation.")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public async Task ExecuteAsyncCreatesAssociationWithNormalizedOptionalNote(
        string? note,
        string? expectedNote)
    {
        Guid tradeId = Guid.NewGuid();
        TradingMistake mistake = CreateMistake();
        AssignmentFixture fixture = CreateFixture(mistake: mistake);

        Guid result = await fixture.UseCase.ExecuteAsync(
            new(tradeId, mistake.Id, note));

        TradeMistake association = Assert.IsType<TradeMistake>(
            fixture.TradeMistakeStore.AddedTradeMistake);
        Assert.Equal(association.Id, result);
        Assert.Equal(tradeId, association.TradeId);
        Assert.Equal(mistake.Id, association.TradingMistakeId);
        Assert.Equal(expectedNote, association.Note);
        Assert.Equal(CurrentUtc, association.CreatedAtUtc);
        Assert.Equal(CurrentUtc, association.UpdatedAtUtc);
        Assert.Equal(1, fixture.TradeMistakeStore.AddCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncDoesNotDependOnTradeProfitOrLossState()
    {
        TradingMistake mistake = CreateMistake();
        AssignmentFixture fixture = CreateFixture(mistake: mistake);

        _ = await fixture.UseCase.ExecuteAsync(
            new(Guid.NewGuid(), mistake.Id, "Process observation"));

        Assert.Equal(1, fixture.TradeReader.CallCount);
        Assert.Equal(1, fixture.TradeMistakeStore.AddCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncPropagatesPersistenceFailure()
    {
        TradingMistake mistake = CreateMistake();
        var expected = new InvalidOperationException("save failed");
        AssignmentFixture fixture = CreateFixture(mistake: mistake);
        fixture.TradeMistakeStore.AddException = expected;

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.UseCase.ExecuteAsync(
                new(Guid.NewGuid(), mistake.Id, null)));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task ExecuteAsyncForwardsCancellationTokenToEveryOperation()
    {
        TradingMistake mistake = CreateMistake();
        AssignmentFixture fixture = CreateFixture(mistake: mistake);
        using var cancellationSource = new CancellationTokenSource();

        _ = await fixture.UseCase.ExecuteAsync(
            new(Guid.NewGuid(), mistake.Id, null),
            cancellationSource.Token);

        Assert.Equal(cancellationSource.Token, fixture.TradeReader.CancellationToken);
        Assert.Equal(cancellationSource.Token, fixture.TradingMistakeStore.CancellationToken);
        Assert.Equal(cancellationSource.Token, fixture.TradeMistakeStore.CancellationToken);
    }

    [Fact]
    public async Task ExecuteAsyncPropagatesCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var expected = new OperationCanceledException(cancellationSource.Token);
        AssignmentFixture fixture = CreateFixture();
        fixture.TradeReader.Exception = expected;

        OperationCanceledException actual =
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => fixture.UseCase.ExecuteAsync(
                    new(Guid.NewGuid(), Guid.NewGuid(), null),
                    cancellationSource.Token));

        Assert.Same(expected, actual);
        Assert.Equal(0, fixture.TradeMistakeStore.AddCallCount);
    }

    private static AssignmentFixture CreateFixture(
        bool tradeExists = true,
        TradingMistake? mistake = null,
        bool associationExists = false,
        bool missingMistake = false)
    {
        var tradeReader = new RecordingTradeExistenceReader
        {
            Exists = tradeExists,
        };
        var tradingMistakeStore = new RecordingTradingMistakeStore
        {
            Mistake = missingMistake
                ? null
                : mistake ?? (tradeExists ? CreateMistake() : null),
        };
        var tradeMistakeStore = new RecordingTradeMistakeStore
        {
            Exists = associationExists,
        };
        return new AssignmentFixture(
            new AssignTradeMistakeUseCase(
                tradeReader,
                tradingMistakeStore,
                tradeMistakeStore,
                new FixedTimeProvider(CurrentUtc)),
            tradeReader,
            tradingMistakeStore,
            tradeMistakeStore);
    }

    private static TradingMistake CreateMistake(bool active = true)
    {
        var mistake = new TradingMistake("FOMO", null, CurrentUtc.AddDays(-1));
        if (!active)
        {
            mistake.Deactivate(CurrentUtc.AddHours(-1));
        }

        return mistake;
    }

    private sealed record AssignmentFixture(
        AssignTradeMistakeUseCase UseCase,
        RecordingTradeExistenceReader TradeReader,
        RecordingTradingMistakeStore TradingMistakeStore,
        RecordingTradeMistakeStore TradeMistakeStore);

    private sealed class RecordingTradeExistenceReader : ITradeExistenceReader
    {
        public bool Exists { get; set; }
        public Exception? Exception { get; set; }
        public int CallCount { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<bool> ExistsAsync(
            Guid tradeId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            CancellationToken = cancellationToken;
            return Exception is null
                ? Task.FromResult(Exists)
                : Task.FromException<bool>(Exception);
        }
    }

    private sealed class RecordingTradingMistakeStore : ITradingMistakeStore
    {
        public TradingMistake? Mistake { get; set; }
        public int GetCallCount { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task AddAsync(
            TradingMistake mistake,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TradingMistake?> GetByIdAsync(
            Guid mistakeId,
            CancellationToken cancellationToken = default)
        {
            GetCallCount++;
            CancellationToken = cancellationToken;
            return Task.FromResult(Mistake);
        }

        public Task UpdateAsync(
            TradingMistake mistake,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingTradeMistakeStore : ITradeMistakeStore
    {
        public bool Exists { get; set; }
        public TradeMistake? TradeMistake { get; set; }
        public TradeMistake? AddedTradeMistake { get; private set; }
        public Exception? AddException { get; set; }
        public int ExistsCallCount { get; private set; }
        public int AddCallCount { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<bool> ExistsAsync(
            Guid tradeId,
            Guid tradingMistakeId,
            CancellationToken cancellationToken = default)
        {
            ExistsCallCount++;
            CancellationToken = cancellationToken;
            return Task.FromResult(Exists);
        }

        public Task<TradeMistake?> GetByIdAsync(
            Guid tradeMistakeId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(TradeMistake);

        public Task AddAsync(
            TradeMistake tradeMistake,
            CancellationToken cancellationToken = default)
        {
            AddCallCount++;
            AddedTradeMistake = tradeMistake;
            CancellationToken = cancellationToken;
            return AddException is null
                ? Task.CompletedTask
                : Task.FromException(AddException);
        }

        public Task RemoveAsync(
            Guid tradeMistakeId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

public sealed class RemoveTradeMistakeUseCaseTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExecuteAsyncRejectsNullCommandBeforeStoreAccess()
    {
        var store = new RecordingStore();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => new RemoveTradeMistakeUseCase(store).ExecuteAsync(null!));

        Assert.Equal(0, store.GetCallCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteAsyncRejectsEmptyIdentifiersBeforeStoreAccess(
        bool emptyTradeId)
    {
        var store = new RecordingStore();
        var command = new RemoveTradeMistakeCommand(
            emptyTradeId ? Guid.Empty : Guid.NewGuid(),
            emptyTradeId ? Guid.NewGuid() : Guid.Empty);

        await Assert.ThrowsAsync<ArgumentException>(
            () => new RemoveTradeMistakeUseCase(store).ExecuteAsync(command));

        Assert.Equal(0, store.GetCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncRejectsMissingAssociation()
    {
        var store = new RecordingStore();

        KeyNotFoundException exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => new RemoveTradeMistakeUseCase(store).ExecuteAsync(
                new(Guid.NewGuid(), Guid.NewGuid())));

        Assert.Equal(RemoveTradeMistakeUseCase.MissingAssociationMessage, exception.Message);
        Assert.Equal(0, store.RemoveCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncDoesNotRemoveAssociationBelongingToAnotherTrade()
    {
        TradeMistake association = CreateAssociation();
        var store = new RecordingStore { TradeMistake = association };

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => new RemoveTradeMistakeUseCase(store).ExecuteAsync(
                new(Guid.NewGuid(), association.Id)));

        Assert.Equal(0, store.RemoveCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncRemovesExactAssociationRegardlessOfCatalogState()
    {
        TradeMistake association = CreateAssociation();
        var store = new RecordingStore { TradeMistake = association };

        await new RemoveTradeMistakeUseCase(store).ExecuteAsync(
            new(association.TradeId, association.Id));

        Assert.Equal(association.Id, store.RemovedId);
        Assert.Equal(1, store.RemoveCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncPropagatesPersistenceFailure()
    {
        TradeMistake association = CreateAssociation();
        var expected = new InvalidOperationException("remove failed");
        var store = new RecordingStore
        {
            TradeMistake = association,
            RemoveException = expected,
        };

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new RemoveTradeMistakeUseCase(store).ExecuteAsync(
                new(association.TradeId, association.Id)));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task ExecuteAsyncForwardsCancellationAndPropagatesCancellation()
    {
        TradeMistake association = CreateAssociation();
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var expected = new OperationCanceledException(cancellationSource.Token);
        var store = new RecordingStore
        {
            TradeMistake = association,
            RemoveException = expected,
        };

        OperationCanceledException actual =
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => new RemoveTradeMistakeUseCase(store).ExecuteAsync(
                    new(association.TradeId, association.Id),
                    cancellationSource.Token));

        Assert.Same(expected, actual);
        Assert.Equal(cancellationSource.Token, store.GetToken);
        Assert.Equal(cancellationSource.Token, store.RemoveToken);
    }

    private static TradeMistake CreateAssociation() =>
        new(Guid.NewGuid(), Guid.NewGuid(), null, CreatedAtUtc);

    private sealed class RecordingStore : ITradeMistakeStore
    {
        public TradeMistake? TradeMistake { get; set; }
        public Exception? RemoveException { get; set; }
        public int GetCallCount { get; private set; }
        public int RemoveCallCount { get; private set; }
        public Guid RemovedId { get; private set; }
        public CancellationToken GetToken { get; private set; }
        public CancellationToken RemoveToken { get; private set; }

        public Task<bool> ExistsAsync(
            Guid tradeId,
            Guid tradingMistakeId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TradeMistake?> GetByIdAsync(
            Guid tradeMistakeId,
            CancellationToken cancellationToken = default)
        {
            GetCallCount++;
            GetToken = cancellationToken;
            return Task.FromResult(TradeMistake);
        }

        public Task AddAsync(
            TradeMistake tradeMistake,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RemoveAsync(
            Guid tradeMistakeId,
            CancellationToken cancellationToken = default)
        {
            RemoveCallCount++;
            RemovedId = tradeMistakeId;
            RemoveToken = cancellationToken;
            return RemoveException is null
                ? Task.CompletedTask
                : Task.FromException(RemoveException);
        }
    }
}
