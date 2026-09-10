using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Domain.Instruments;

namespace PersonalTradingJournal.Application.Tests.Instruments;

public sealed class InstrumentLifecycleUseCaseTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CurrentUtc =
        new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ActivateAsyncActivatesInactiveInstrumentAndPersistsIt()
    {
        Instrument instrument = CreateInstrument();
        instrument.Deactivate(CreatedAtUtc.AddHours(1));
        var store = new RecordingInstrumentStore(instrument);
        var timeProvider = new RecordingTimeProvider(CurrentUtc);
        var useCase = new InstrumentLifecycleUseCase(store, timeProvider);

        await useCase.ActivateAsync(instrument.Id);

        Instrument updatedInstrument = Assert.IsType<Instrument>(store.UpdatedInstrument);
        Assert.Same(instrument, updatedInstrument);
        Assert.True(updatedInstrument.IsActive);
        Assert.Equal(CurrentUtc, updatedInstrument.UpdatedAtUtc);
        Assert.Equal(1, store.UpdateCallCount);
        Assert.Equal(1, timeProvider.CallCount);
    }

    [Fact]
    public async Task DeactivateAsyncDeactivatesActiveInstrumentAndPersistsIt()
    {
        Instrument instrument = CreateInstrument();
        var store = new RecordingInstrumentStore(instrument);
        var timeProvider = new RecordingTimeProvider(CurrentUtc);
        var useCase = new InstrumentLifecycleUseCase(store, timeProvider);

        await useCase.DeactivateAsync(instrument.Id);

        Instrument updatedInstrument = Assert.IsType<Instrument>(store.UpdatedInstrument);
        Assert.Same(instrument, updatedInstrument);
        Assert.False(updatedInstrument.IsActive);
        Assert.Equal(CurrentUtc, updatedInstrument.UpdatedAtUtc);
        Assert.Equal(1, store.UpdateCallCount);
        Assert.Equal(1, timeProvider.CallCount);
    }

    [Fact]
    public async Task ActivateAsyncDoesNotWriteOrObtainTimeWhenInstrumentIsAlreadyActive()
    {
        Instrument instrument = CreateInstrument();
        DateTimeOffset originalUpdatedAtUtc = instrument.UpdatedAtUtc;
        var store = new RecordingInstrumentStore(instrument);
        var timeProvider = new RecordingTimeProvider(CurrentUtc);
        var useCase = new InstrumentLifecycleUseCase(store, timeProvider);

        await useCase.ActivateAsync(instrument.Id);

        Assert.Equal(originalUpdatedAtUtc, instrument.UpdatedAtUtc);
        Assert.Equal(0, store.UpdateCallCount);
        Assert.Equal(0, timeProvider.CallCount);
    }

    [Fact]
    public async Task DeactivateAsyncDoesNotWriteOrObtainTimeWhenInstrumentIsAlreadyInactive()
    {
        Instrument instrument = CreateInstrument();
        DateTimeOffset originalUpdatedAtUtc = CreatedAtUtc.AddHours(1);
        instrument.Deactivate(originalUpdatedAtUtc);
        var store = new RecordingInstrumentStore(instrument);
        var timeProvider = new RecordingTimeProvider(CurrentUtc);
        var useCase = new InstrumentLifecycleUseCase(store, timeProvider);

        await useCase.DeactivateAsync(instrument.Id);

        Assert.Equal(originalUpdatedAtUtc, instrument.UpdatedAtUtc);
        Assert.Equal(0, store.UpdateCallCount);
        Assert.Equal(0, timeProvider.CallCount);
    }

    [Fact]
    public async Task ActivateAsyncThrowsWhenInstrumentDoesNotExist()
    {
        Guid instrumentId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var store = new RecordingInstrumentStore(instrument: null);
        var useCase = new InstrumentLifecycleUseCase(
            store,
            new RecordingTimeProvider(CurrentUtc));

        KeyNotFoundException exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => useCase.ActivateAsync(instrumentId));

        Assert.Contains(instrumentId.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, store.GetCallCount);
        Assert.Equal(0, store.UpdateCallCount);
    }

    [Fact]
    public async Task LifecycleMethodsRejectEmptyIdWithoutQueryingStore()
    {
        var store = new RecordingInstrumentStore(CreateInstrument());
        var useCase = new InstrumentLifecycleUseCase(
            store,
            new RecordingTimeProvider(CurrentUtc));

        ArgumentException activateException = await Assert.ThrowsAsync<ArgumentException>(
            () => useCase.ActivateAsync(Guid.Empty));
        ArgumentException deactivateException = await Assert.ThrowsAsync<ArgumentException>(
            () => useCase.DeactivateAsync(Guid.Empty));

        Assert.Equal("instrumentId", activateException.ParamName);
        Assert.Equal("instrumentId", deactivateException.ParamName);
        Assert.Equal(0, store.GetCallCount);
        Assert.Equal(0, store.UpdateCallCount);
    }

    [Fact]
    public async Task DeactivateAsyncForwardsCancellationTokenToLoadAndUpdate()
    {
        Instrument instrument = CreateInstrument();
        var store = new RecordingInstrumentStore(instrument);
        var useCase = new InstrumentLifecycleUseCase(
            store,
            new RecordingTimeProvider(CurrentUtc));
        using var cancellationSource = new CancellationTokenSource();

        await useCase.DeactivateAsync(instrument.Id, cancellationSource.Token);

        Assert.Equal(cancellationSource.Token, store.GetCancellationToken);
        Assert.Equal(cancellationSource.Token, store.UpdateCancellationToken);
    }

    [Fact]
    public async Task DeactivateAsyncPropagatesLoadFailureWithoutUpdating()
    {
        var expectedException = new InvalidOperationException("Load failed.");
        var store = new RecordingInstrumentStore(
            CreateInstrument(),
            loadException: expectedException);
        var useCase = new InstrumentLifecycleUseCase(
            store,
            new RecordingTimeProvider(CurrentUtc));

        InvalidOperationException actualException =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => useCase.DeactivateAsync(store.Instrument!.Id));

        Assert.Same(expectedException, actualException);
        Assert.Equal(0, store.UpdateCallCount);
    }

    [Fact]
    public async Task DeactivateAsyncPropagatesUpdateFailure()
    {
        var expectedException = new InvalidOperationException("Update failed.");
        var store = new RecordingInstrumentStore(
            CreateInstrument(),
            updateException: expectedException);
        var useCase = new InstrumentLifecycleUseCase(
            store,
            new RecordingTimeProvider(CurrentUtc));

        InvalidOperationException actualException =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => useCase.DeactivateAsync(store.Instrument!.Id));

        Assert.Same(expectedException, actualException);
        Assert.Equal(1, store.UpdateCallCount);
    }

    private static Instrument CreateInstrument()
    {
        return new Instrument(
            "NQ",
            "Nasdaq-100 E-mini",
            AssetClass.Futures,
            "CME",
            "USD",
            0.25m,
            5m,
            CreatedAtUtc);
    }

    private sealed class RecordingInstrumentStore : IInstrumentStore
    {
        private readonly Exception? _loadException;
        private readonly Exception? _updateException;

        public RecordingInstrumentStore(
            Instrument? instrument,
            Exception? loadException = null,
            Exception? updateException = null)
        {
            Instrument = instrument;
            _loadException = loadException;
            _updateException = updateException;
        }

        public Instrument? Instrument { get; }

        public Instrument? UpdatedInstrument { get; private set; }

        public CancellationToken GetCancellationToken { get; private set; }

        public CancellationToken UpdateCancellationToken { get; private set; }

        public int GetCallCount { get; private set; }

        public int UpdateCallCount { get; private set; }

        public Task AddAsync(
            Instrument instrument,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Instrument?> GetByIdAsync(
            Guid instrumentId,
            CancellationToken cancellationToken = default)
        {
            GetCallCount++;
            GetCancellationToken = cancellationToken;

            return _loadException is null
                ? Task.FromResult(Instrument)
                : Task.FromException<Instrument?>(_loadException);
        }

        public Task UpdateAsync(
            Instrument instrument,
            CancellationToken cancellationToken = default)
        {
            UpdateCallCount++;
            UpdatedInstrument = instrument;
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
