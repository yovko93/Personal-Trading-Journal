using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Domain.Instruments;

namespace PersonalTradingJournal.Application.Tests.Instruments;

public sealed class InstrumentManagementUseCaseTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 14, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset UpdatedAtUtc = CreatedAtUtc.AddDays(1);

    [Fact]
    public async Task GetDetailsReturnsReaderProjectionAndMissingReturnsNull()
    {
        InstrumentDetails details = CreateDetails();
        var reader = new RecordingReader(details);
        var useCase = new GetInstrumentDetailsUseCase(reader);

        Assert.Same(details, await useCase.ExecuteAsync(details.Id));
        Assert.Equal(details.Id, reader.InstrumentId);

        Assert.Null(await new GetInstrumentDetailsUseCase(
            new RecordingReader(null)).ExecuteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateNormalizesAndPersistsAuthoritativeAggregate()
    {
        Instrument instrument = CreateInstrument();
        var store = new RecordingStore(instrument);
        var useCase = CreateUpdateUseCase(store, new RecordingDeletionStore());

        UpdateInstrumentResult result = await useCase.ExecuteAsync(new(
            instrument.Id, " mnq ", " Micro Nasdaq ", AssetClass.Futures,
            "  ", " eur ", 0.25m, 0.50m));

        Assert.True(result.WasChanged);
        Assert.Same(instrument, store.UpdatedInstrument);
        Assert.Equal("MNQ", result.Instrument.Symbol);
        Assert.Null(result.Instrument.Exchange);
        Assert.Equal("EUR", result.Instrument.Currency);
        Assert.Equal(2m, result.Instrument.PointValue);
        Assert.Equal(UpdatedAtUtc, result.Instrument.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpdateCanonicalNoOpDoesNotPersistOrCheckReferences()
    {
        Instrument instrument = CreateInstrument();
        var store = new RecordingStore(instrument);
        var deletionStore = new RecordingDeletionStore();

        UpdateInstrumentResult result = await CreateUpdateUseCase(store, deletionStore)
            .ExecuteAsync(new(
                instrument.Id, " nq ", " Nasdaq-100 E-mini ", AssetClass.Futures,
                " CME ", " usd ", 0.25m, 5m));

        Assert.False(result.WasChanged);
        Assert.Equal(0, store.UpdateCallCount);
        Assert.Equal(0, deletionStore.HasTradesCallCount);
        Assert.Equal(CreatedAtUtc, result.Instrument.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpdateReferencedInstrumentAllowsSafeMetadataAndEconomicsChanges()
    {
        Instrument instrument = CreateInstrument();
        var store = new RecordingStore(instrument);
        var deletionStore = new RecordingDeletionStore { HasTrades = true };

        UpdateInstrumentResult result = await CreateUpdateUseCase(store, deletionStore)
            .ExecuteAsync(new(
                instrument.Id, "MNQ", "Micro Nasdaq", AssetClass.Futures,
                null, "EUR", 0.25m, 0.50m));

        Assert.True(result.WasChanged);
        Assert.Equal(0, deletionStore.HasTradesCallCount);
        Assert.Equal(1, store.UpdateCallCount);
    }

    [Fact]
    public async Task UpdateReferencedInstrumentBlocksAssetClassChange()
    {
        Instrument instrument = CreateInstrument();
        var store = new RecordingStore(instrument);
        var deletionStore = new RecordingDeletionStore { HasTrades = true };

        await Assert.ThrowsAsync<InstrumentAssetClassChangeBlockedException>(() =>
            CreateUpdateUseCase(store, deletionStore).ExecuteAsync(new(
                instrument.Id, "NQ", "Nasdaq-100 E-mini", AssetClass.Crypto,
                "CME", "USD", 0.25m, 5m)));

        Assert.Equal(1, deletionStore.HasTradesCallCount);
        Assert.Equal(0, store.UpdateCallCount);
    }

    [Fact]
    public async Task UpdateUnusedInstrumentAllowsAssetClassChange()
    {
        Instrument instrument = CreateInstrument();
        var store = new RecordingStore(instrument);
        var deletionStore = new RecordingDeletionStore();

        UpdateInstrumentResult result = await CreateUpdateUseCase(store, deletionStore)
            .ExecuteAsync(new(
                instrument.Id, "NQ", "Nasdaq-100 E-mini", AssetClass.Equity,
                "CME", "USD", 0.25m, 5m));

        Assert.True(result.WasChanged);
        Assert.Equal(AssetClass.Equity, result.Instrument.AssetClass);
    }

    [Fact]
    public async Task UpdateMissingAndInvalidEconomicsDoNotPersist()
    {
        var missingStore = new RecordingStore(null);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            CreateUpdateUseCase(missingStore, new RecordingDeletionStore()).ExecuteAsync(new(
                Guid.NewGuid(), "NQ", "Name", AssetClass.Futures, null, "USD", 1m, 1m)));

        Instrument instrument = CreateInstrument();
        var store = new RecordingStore(instrument);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            CreateUpdateUseCase(store, new RecordingDeletionStore()).ExecuteAsync(new(
                instrument.Id, "NQ", "Name", AssetClass.Futures, null, "USD", 0m, 1m)));
        Assert.Equal(0, store.UpdateCallCount);
    }

    [Fact]
    public async Task DeleteUnusedSucceedsReferencedIsBlockedAndMissingThrows()
    {
        Instrument instrument = CreateInstrument();
        var unusedDeletionStore = new RecordingDeletionStore();
        DeleteInstrumentResult deleted = await new DeleteInstrumentUseCase(
            new RecordingStore(instrument), unusedDeletionStore).ExecuteAsync(instrument.Id);
        Assert.Equal(DeleteInstrumentResult.Deleted, deleted);
        Assert.Equal(1, unusedDeletionStore.DeleteCallCount);

        var referencedStore = new RecordingDeletionStore { HasTrades = true };
        DeleteInstrumentResult blocked = await new DeleteInstrumentUseCase(
            new RecordingStore(instrument), referencedStore).ExecuteAsync(instrument.Id);
        Assert.Equal(DeleteInstrumentResult.Referenced, blocked);
        Assert.Equal(0, referencedStore.DeleteCallCount);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new DeleteInstrumentUseCase(
                new RecordingStore(null), new RecordingDeletionStore())
                .ExecuteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteConstraintRaceReturnsReferenced()
    {
        Instrument instrument = CreateInstrument();
        var deletionStore = new RecordingDeletionStore
        {
            DeleteException = new InstrumentDeleteBlockedException("Referenced."),
        };

        DeleteInstrumentResult result = await new DeleteInstrumentUseCase(
            new RecordingStore(instrument), deletionStore).ExecuteAsync(instrument.Id);

        Assert.Equal(DeleteInstrumentResult.Referenced, result);
    }

    private static UpdateInstrumentUseCase CreateUpdateUseCase(
        RecordingStore store,
        RecordingDeletionStore deletionStore) =>
        new(store, deletionStore, new FixedTimeProvider(UpdatedAtUtc));

    private static Instrument CreateInstrument() => new(
        "NQ", "Nasdaq-100 E-mini", AssetClass.Futures, "CME", "USD",
        0.25m, 5m, CreatedAtUtc);

    private static InstrumentDetails CreateDetails() => new(
        Guid.NewGuid(), "NQ", "Nasdaq-100 E-mini", AssetClass.Futures,
        "CME", "USD", 0.25m, 5m, 20m, true, CreatedAtUtc, CreatedAtUtc);

    private sealed class RecordingReader(InstrumentDetails? details) : IInstrumentReader
    {
        public Guid InstrumentId { get; private set; }
        public Task<IReadOnlyList<InstrumentListItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<InstrumentDetails?> GetByIdAsync(Guid instrumentId, CancellationToken cancellationToken = default)
        {
            InstrumentId = instrumentId;
            return Task.FromResult(details);
        }
    }

    private sealed class RecordingStore(Instrument? instrument) : IInstrumentStore
    {
        public Instrument? UpdatedInstrument { get; private set; }
        public int UpdateCallCount { get; private set; }
        public Task AddAsync(Instrument value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Instrument?> GetByIdAsync(Guid instrumentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(instrument?.Id == instrumentId ? instrument : null);
        public Task UpdateAsync(Instrument value, CancellationToken cancellationToken = default)
        {
            UpdateCallCount++;
            UpdatedInstrument = value;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingDeletionStore : IInstrumentDeletionStore
    {
        public bool HasTrades { get; init; }
        public Exception? DeleteException { get; init; }
        public int HasTradesCallCount { get; private set; }
        public int DeleteCallCount { get; private set; }
        public Task<bool> HasTradesAsync(Guid instrumentId, CancellationToken cancellationToken = default)
        {
            HasTradesCallCount++;
            return Task.FromResult(HasTrades);
        }
        public Task DeleteAsync(Guid instrumentId, CancellationToken cancellationToken = default)
        {
            DeleteCallCount++;
            return DeleteException is null ? Task.CompletedTask : Task.FromException(DeleteException);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
