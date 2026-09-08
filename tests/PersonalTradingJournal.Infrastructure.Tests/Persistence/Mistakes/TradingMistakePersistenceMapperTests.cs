using PersonalTradingJournal.Domain.Mistakes;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Mistakes;

public sealed class TradingMistakePersistenceMapperTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 6, 10, 8, 30, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset UpdatedAtUtc =
        new(2026, 6, 11, 9, 45, 0, TimeSpan.Zero);

    [Fact]
    public void ToRecordMapsPersistedTradingMistakeState()
    {
        var mistake = new TradingMistake(
            "FOMO",
            "Entered because price was moving away before planned confirmation.",
            CreatedAtUtc);
        mistake.Deactivate(UpdatedAtUtc);

        TradingMistakeRecord record = TradingMistakePersistenceMapper.ToRecord(mistake);

        Assert.Equal(mistake.Id, record.Id);
        Assert.Equal("FOMO", record.Name);
        Assert.Equal(
            "Entered because price was moving away before planned confirmation.",
            record.Description);
        Assert.False(record.IsActive);
        Assert.Equal(CreatedAtUtc, record.CreatedAtUtc);
        Assert.Equal(UpdatedAtUtc, record.UpdatedAtUtc);
    }

    [Fact]
    public void ToRecordPreservesNullDescription()
    {
        var mistake = new TradingMistake("Moved Stop", null, CreatedAtUtc);

        TradingMistakeRecord record = TradingMistakePersistenceMapper.ToRecord(mistake);

        Assert.Null(record.Description);
    }

    [Fact]
    public void ToRecordRejectsNullTradingMistake()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TradingMistakePersistenceMapper.ToRecord(null!));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToDomainRehydratesPersistedTradingMistakeState(bool isActive)
    {
        TradingMistakeRecord record = CreateValidRecord();
        record.IsActive = isActive;

        TradingMistake mistake = TradingMistakePersistenceMapper.ToDomain(record);

        Assert.Equal(record.Id, mistake.Id);
        Assert.Equal(record.Name, mistake.Name);
        Assert.Equal(record.Description, mistake.Description);
        Assert.Equal(isActive, mistake.IsActive);
        Assert.Equal(record.CreatedAtUtc, mistake.CreatedAtUtc);
        Assert.Equal(record.UpdatedAtUtc, mistake.UpdatedAtUtc);
    }

    [Fact]
    public void ToDomainPreservesNullDescription()
    {
        TradingMistakeRecord record = CreateValidRecord();
        record.Description = null;

        TradingMistake mistake = TradingMistakePersistenceMapper.ToDomain(record);

        Assert.Null(mistake.Description);
    }

    [Fact]
    public void ToDomainRejectsNullRecord()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TradingMistakePersistenceMapper.ToDomain(null!));
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

    private static void AssertInvalid(Action<TradingMistakeRecord> corrupt)
    {
        TradingMistakeRecord record = CreateValidRecord();
        corrupt(record);

        Assert.ThrowsAny<ArgumentException>(() =>
            TradingMistakePersistenceMapper.ToDomain(record));
    }

    private static TradingMistakeRecord CreateValidRecord()
    {
        return new TradingMistakeRecord
        {
            Id = Guid.Parse("68880020-07c5-45f0-aec1-f58572678618"),
            Name = "FOMO",
            Description =
                "Entered because price was moving away before planned confirmation.",
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = UpdatedAtUtc,
        };
    }
}
