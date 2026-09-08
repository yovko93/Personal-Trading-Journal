using PersonalTradingJournal.Domain.Strategies;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Strategies;

public sealed class StrategyPersistenceMapperTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 4, 10, 8, 30, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset UpdatedAtUtc =
        new(2026, 4, 11, 9, 45, 0, TimeSpan.Zero);

    [Fact]
    public void ToRecordMapsPersistedStrategyState()
    {
        var strategy = new Strategy(
            "ICT 2022 Model",
            "Liquidity-based discretionary framework.",
            CreatedAtUtc);
        strategy.Deactivate(UpdatedAtUtc);

        StrategyRecord record = StrategyPersistenceMapper.ToRecord(strategy);

        Assert.Equal(strategy.Id, record.Id);
        Assert.Equal("ICT 2022 Model", record.Name);
        Assert.Equal("Liquidity-based discretionary framework.", record.Description);
        Assert.False(record.IsActive);
        Assert.Equal(CreatedAtUtc, record.CreatedAtUtc);
        Assert.Equal(UpdatedAtUtc, record.UpdatedAtUtc);
    }

    [Fact]
    public void ToRecordPreservesNullDescription()
    {
        var strategy = new Strategy("Opening Range", null, CreatedAtUtc);

        StrategyRecord record = StrategyPersistenceMapper.ToRecord(strategy);

        Assert.Null(record.Description);
    }

    [Fact]
    public void ToRecordRejectsNullStrategy()
    {
        Assert.Throws<ArgumentNullException>(() =>
            StrategyPersistenceMapper.ToRecord(null!));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToDomainRehydratesPersistedStrategyState(bool isActive)
    {
        StrategyRecord record = CreateValidRecord();
        record.IsActive = isActive;

        Strategy strategy = StrategyPersistenceMapper.ToDomain(record);

        Assert.Equal(record.Id, strategy.Id);
        Assert.Equal(record.Name, strategy.Name);
        Assert.Equal(record.Description, strategy.Description);
        Assert.Equal(isActive, strategy.IsActive);
        Assert.Equal(record.CreatedAtUtc, strategy.CreatedAtUtc);
        Assert.Equal(record.UpdatedAtUtc, strategy.UpdatedAtUtc);
    }

    [Fact]
    public void ToDomainPreservesNullDescription()
    {
        StrategyRecord record = CreateValidRecord();
        record.Description = null;

        Strategy strategy = StrategyPersistenceMapper.ToDomain(record);

        Assert.Null(strategy.Description);
    }

    [Fact]
    public void ToDomainRejectsNullRecord()
    {
        Assert.Throws<ArgumentNullException>(() =>
            StrategyPersistenceMapper.ToDomain(null!));
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

    private static void AssertInvalid(Action<StrategyRecord> corrupt)
    {
        StrategyRecord record = CreateValidRecord();
        corrupt(record);

        Assert.ThrowsAny<ArgumentException>(() =>
            StrategyPersistenceMapper.ToDomain(record));
    }

    private static StrategyRecord CreateValidRecord()
    {
        return new StrategyRecord
        {
            Id = Guid.Parse("f4879854-89fa-483e-866a-6753d1006b2a"),
            Name = "ICT 2022 Model",
            Description = "Liquidity-based discretionary framework.",
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = UpdatedAtUtc,
        };
    }
}
