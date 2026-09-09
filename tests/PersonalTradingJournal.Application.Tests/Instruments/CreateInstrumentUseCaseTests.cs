using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Domain.Instruments;

namespace PersonalTradingJournal.Application.Tests.Instruments;

public sealed class CreateInstrumentUseCaseTests
{
    private static readonly DateTimeOffset CurrentUtc =
        new(2026, 9, 9, 14, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExecuteAsyncCreatesNormalizedActiveInstrumentAndReturnsItsId()
    {
        var store = new RecordingInstrumentStore();
        var useCase = new CreateInstrumentUseCase(
            store,
            new FixedTimeProvider(CurrentUtc));
        var command = new CreateInstrumentCommand(
            " nq ",
            " Nasdaq-100 E-mini ",
            AssetClass.Futures,
            " CME ",
            " usd ",
            0.25m,
            5m);

        Guid instrumentId = await useCase.ExecuteAsync(command);

        Instrument instrument = Assert.IsType<Instrument>(store.AddedInstrument);
        Assert.NotEqual(Guid.Empty, instrumentId);
        Assert.Equal(instrument.Id, instrumentId);
        Assert.Equal("NQ", instrument.Symbol);
        Assert.Equal("Nasdaq-100 E-mini", instrument.DisplayName);
        Assert.Equal(AssetClass.Futures, instrument.AssetClass);
        Assert.Equal("CME", instrument.Exchange);
        Assert.Equal("USD", instrument.Currency);
        Assert.Equal(0.25m, instrument.TickSize);
        Assert.Equal(5m, instrument.TickValue);
        Assert.Equal(20m, instrument.PointValue);
        Assert.True(instrument.IsActive);
        Assert.Equal(CurrentUtc, instrument.CreatedAtUtc);
        Assert.Equal(CurrentUtc, instrument.UpdatedAtUtc);
    }

    [Fact]
    public async Task ExecuteAsyncPreservesNullExchange()
    {
        var store = new RecordingInstrumentStore();
        var useCase = new CreateInstrumentUseCase(
            store,
            new FixedTimeProvider(CurrentUtc));
        var command = new CreateInstrumentCommand(
            "EURUSD",
            "Euro / US Dollar",
            AssetClass.Forex,
            null,
            "USD",
            0.00001m,
            1m);

        await useCase.ExecuteAsync(command);

        Instrument instrument = Assert.IsType<Instrument>(store.AddedInstrument);
        Assert.Null(instrument.Exchange);
    }

    [Fact]
    public async Task ExecuteAsyncPropagatesDomainValidationAndDoesNotCallStore()
    {
        var store = new RecordingInstrumentStore();
        var useCase = new CreateInstrumentUseCase(
            store,
            new FixedTimeProvider(CurrentUtc));
        var command = new CreateInstrumentCommand(
            "NQ",
            "Nasdaq-100 E-mini",
            AssetClass.Futures,
            "CME",
            "USD",
            0m,
            5m);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => useCase.ExecuteAsync(command));

        Assert.Equal(0, store.CallCount);
        Assert.Null(store.AddedInstrument);
    }

    [Fact]
    public async Task ExecuteAsyncForwardsCancellationTokenToStore()
    {
        var store = new RecordingInstrumentStore();
        var useCase = new CreateInstrumentUseCase(
            store,
            new FixedTimeProvider(CurrentUtc));
        var command = new CreateInstrumentCommand(
            "NQ",
            "Nasdaq-100 E-mini",
            AssetClass.Futures,
            "CME",
            "USD",
            0.25m,
            5m);
        using var cancellationSource = new CancellationTokenSource();

        await useCase.ExecuteAsync(command, cancellationSource.Token);

        Assert.Equal(cancellationSource.Token, store.CancellationToken);
    }

    [Fact]
    public async Task ExecuteAsyncRejectsNullCommand()
    {
        var store = new RecordingInstrumentStore();
        var useCase = new CreateInstrumentUseCase(
            store,
            new FixedTimeProvider(CurrentUtc));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => useCase.ExecuteAsync(null!));

        Assert.Equal(0, store.CallCount);
    }

    [Fact]
    public async Task ExecuteAsyncPropagatesStoreFailure()
    {
        var expectedException = new InvalidOperationException("Persistence failed.");
        var useCase = new CreateInstrumentUseCase(
            new FailingInstrumentStore(expectedException),
            new FixedTimeProvider(CurrentUtc));
        var command = new CreateInstrumentCommand(
            "NQ",
            "Nasdaq-100 E-mini",
            AssetClass.Futures,
            "CME",
            "USD",
            0.25m,
            5m);

        InvalidOperationException actualException =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => useCase.ExecuteAsync(command));

        Assert.Same(expectedException, actualException);
    }

    private sealed class RecordingInstrumentStore : IInstrumentStore
    {
        public Instrument? AddedInstrument { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public int CallCount { get; private set; }

        public Task AddAsync(
            Instrument instrument,
            CancellationToken cancellationToken = default)
        {
            AddedInstrument = instrument;
            CancellationToken = cancellationToken;
            CallCount++;

            return Task.CompletedTask;
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

    private sealed class FailingInstrumentStore : IInstrumentStore
    {
        private readonly Exception _exception;

        public FailingInstrumentStore(Exception exception)
        {
            _exception = exception;
        }

        public Task AddAsync(
            Instrument instrument,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException(_exception);
        }
    }
}
