using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Setups;

public sealed class TradingSetupPersistenceMapperTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 5, 10, 8, 30, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset UpdatedAtUtc =
        new(2026, 5, 11, 9, 45, 0, TimeSpan.Zero);

    [Fact]
    public void ToRecordMapsPersistedTradingSetupState()
    {
        var setup = new TradingSetup(
            "Liquidity Sweep + MSS + FVG",
            "Liquidity sweep followed by market structure shift and FVG entry.",
            CreatedAtUtc);
        setup.Deactivate(UpdatedAtUtc);

        TradingSetupRecord record = TradingSetupPersistenceMapper.ToRecord(setup);

        Assert.Equal(setup.Id, record.Id);
        Assert.Equal("Liquidity Sweep + MSS + FVG", record.Name);
        Assert.Equal(
            "Liquidity sweep followed by market structure shift and FVG entry.",
            record.Description);
        Assert.False(record.IsActive);
        Assert.Equal(CreatedAtUtc, record.CreatedAtUtc);
        Assert.Equal(UpdatedAtUtc, record.UpdatedAtUtc);
    }

    [Fact]
    public void ToRecordPreservesNullDescription()
    {
        var setup = new TradingSetup("Opening Range Breakout", null, CreatedAtUtc);

        TradingSetupRecord record = TradingSetupPersistenceMapper.ToRecord(setup);

        Assert.Null(record.Description);
    }

    [Fact]
    public void ToRecordRejectsNullTradingSetup()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TradingSetupPersistenceMapper.ToRecord(null!));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToDomainRehydratesPersistedTradingSetupState(bool isActive)
    {
        TradingSetupRecord record = CreateValidRecord();
        record.IsActive = isActive;

        TradingSetup setup = TradingSetupPersistenceMapper.ToDomain(record);

        Assert.Equal(record.Id, setup.Id);
        Assert.Equal(record.Name, setup.Name);
        Assert.Equal(record.Description, setup.Description);
        Assert.Equal(isActive, setup.IsActive);
        Assert.Equal(record.CreatedAtUtc, setup.CreatedAtUtc);
        Assert.Equal(record.UpdatedAtUtc, setup.UpdatedAtUtc);
    }

    [Fact]
    public void ToDomainPreservesNullDescription()
    {
        TradingSetupRecord record = CreateValidRecord();
        record.Description = null;

        TradingSetup setup = TradingSetupPersistenceMapper.ToDomain(record);

        Assert.Null(setup.Description);
    }

    [Fact]
    public void ToDomainRejectsNullRecord()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TradingSetupPersistenceMapper.ToDomain(null!));
    }

    [Fact]
    public void ToDomainRejectsPersistedDataThatViolatesDomainInvariants()
    {
        AssertInvalid(record => record.Id = Guid.Empty);
        AssertInvalid(record => record.Name = "   ");
        AssertInvalid(record => record.Name = new string('N', 129));
        AssertInvalid(record => record.Description = new string('D', 2001));
        AssertInvalid(record =>
            record.CreatedAtUtc = record.CreatedAtUtc.ToOffset(TimeSpan.FromHours(2)));
        AssertInvalid(record =>
            record.UpdatedAtUtc = record.UpdatedAtUtc.ToOffset(TimeSpan.FromHours(-3)));
        AssertInvalid(record => record.UpdatedAtUtc = record.CreatedAtUtc.AddTicks(-1));
    }

    private static void AssertInvalid(Action<TradingSetupRecord> corrupt)
    {
        TradingSetupRecord record = CreateValidRecord();
        corrupt(record);

        Assert.ThrowsAny<ArgumentException>(() =>
            TradingSetupPersistenceMapper.ToDomain(record));
    }

    private static TradingSetupRecord CreateValidRecord()
    {
        return new TradingSetupRecord
        {
            Id = Guid.Parse("6b6617b4-6adf-4210-b236-0528b0fdd9ee"),
            Name = "Liquidity Sweep + MSS + FVG",
            Description =
                "Liquidity sweep followed by market structure shift and FVG entry.",
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = UpdatedAtUtc,
        };
    }
}
