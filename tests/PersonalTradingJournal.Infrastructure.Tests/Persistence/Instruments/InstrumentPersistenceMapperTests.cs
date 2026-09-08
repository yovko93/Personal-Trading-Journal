using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Instruments;

public sealed class InstrumentPersistenceMapperTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 1, 10, 8, 30, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset UpdatedAtUtc =
        new(2026, 1, 11, 9, 45, 0, TimeSpan.Zero);

    [Fact]
    public void ToRecordMapsPersistedInstrumentState()
    {
        var instrument = new Instrument(
            "nq",
            "Nasdaq-100 E-mini",
            AssetClass.Futures,
            "CME",
            "usd",
            0.25m,
            5m,
            CreatedAtUtc);
        instrument.Deactivate(UpdatedAtUtc);

        InstrumentRecord record = InstrumentPersistenceMapper.ToRecord(instrument);

        Assert.Equal(instrument.Id, record.Id);
        Assert.Equal("NQ", record.Symbol);
        Assert.Equal("Nasdaq-100 E-mini", record.DisplayName);
        Assert.Equal(AssetClass.Futures, record.AssetClass);
        Assert.Equal("CME", record.Exchange);
        Assert.Equal("USD", record.Currency);
        Assert.Equal(0.25m, record.TickSize);
        Assert.Equal(5m, record.TickValue);
        Assert.False(record.IsActive);
        Assert.Equal(CreatedAtUtc, record.CreatedAtUtc);
        Assert.Equal(UpdatedAtUtc, record.UpdatedAtUtc);
    }

    [Fact]
    public void ToRecordRejectsNullInstrument()
    {
        Assert.Throws<ArgumentNullException>(() =>
            InstrumentPersistenceMapper.ToRecord(null!));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToDomainRehydratesPersistedInstrumentState(bool isActive)
    {
        InstrumentRecord record = CreateValidRecord();
        record.IsActive = isActive;

        Instrument instrument = InstrumentPersistenceMapper.ToDomain(record);

        Assert.Equal(record.Id, instrument.Id);
        Assert.Equal(record.Symbol, instrument.Symbol);
        Assert.Equal(record.DisplayName, instrument.DisplayName);
        Assert.Equal(record.AssetClass, instrument.AssetClass);
        Assert.Equal(record.Exchange, instrument.Exchange);
        Assert.Equal(record.Currency, instrument.Currency);
        Assert.Equal(record.TickSize, instrument.TickSize);
        Assert.Equal(record.TickValue, instrument.TickValue);
        Assert.Equal(2m, instrument.PointValue);
        Assert.Equal(isActive, instrument.IsActive);
        Assert.Equal(record.CreatedAtUtc, instrument.CreatedAtUtc);
        Assert.Equal(record.UpdatedAtUtc, instrument.UpdatedAtUtc);
    }

    [Fact]
    public void ToDomainRejectsNullRecord()
    {
        Assert.Throws<ArgumentNullException>(() =>
            InstrumentPersistenceMapper.ToDomain(null!));
    }

    [Fact]
    public void ToDomainRejectsPersistedDataThatViolatesDomainInvariants()
    {
        AssertInvalid(record => record.Id = Guid.Empty);
        AssertInvalid(record => record.AssetClass = (AssetClass)999);
        AssertInvalid(record => record.Symbol = "   ");
        AssertInvalid(record => record.TickSize = 0m);
        AssertInvalid(record => record.TickValue = -0.50m);
        AssertInvalid(record =>
            record.CreatedAtUtc = record.CreatedAtUtc.ToOffset(TimeSpan.FromHours(2)));
        AssertInvalid(record => record.UpdatedAtUtc = record.CreatedAtUtc.AddTicks(-1));
    }

    private static void AssertInvalid(Action<InstrumentRecord> corrupt)
    {
        InstrumentRecord record = CreateValidRecord();
        corrupt(record);

        Assert.ThrowsAny<ArgumentException>(() =>
            InstrumentPersistenceMapper.ToDomain(record));
    }

    private static InstrumentRecord CreateValidRecord()
    {
        return new InstrumentRecord
        {
            Id = Guid.Parse("d77a8dc3-0cd0-46e9-b75d-c30ae5735587"),
            Symbol = "MNQ",
            DisplayName = "Nasdaq-100 Micro E-mini",
            AssetClass = AssetClass.Futures,
            Exchange = "CME",
            Currency = "USD",
            TickSize = 0.25m,
            TickValue = 0.50m,
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = UpdatedAtUtc,
        };
    }
}
