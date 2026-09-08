using PersonalTradingJournal.Domain.Mistakes;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Mistakes;

public sealed class TradeMistakePersistenceMapperTests
{
    private static readonly Guid AssociationId =
        Guid.Parse("bc06b127-0213-4242-9f46-1736c6c963dd");

    private static readonly Guid TradeId =
        Guid.Parse("b9a351c2-9266-4c3a-98f5-74793d4b9195");

    private static readonly Guid TradingMistakeId =
        Guid.Parse("0e767ff4-e942-4fbc-97ef-afc6100a8dc8");

    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 8, 26, 14, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset UpdatedAtUtc =
        CreatedAtUtc.AddMinutes(20);

    [Fact]
    public void ToRecordMapsAllPersistedTradeMistakeState()
    {
        TradeMistake tradeMistake = CreateTradeMistake();

        TradeMistakeRecord record =
            TradeMistakePersistenceMapper.ToRecord(tradeMistake);

        Assert.Equal(AssociationId, record.Id);
        Assert.Equal(TradeId, record.TradeId);
        Assert.Equal(TradingMistakeId, record.TradingMistakeId);
        Assert.Equal("Entered before planned confirmation.", record.Note);
        Assert.Equal(CreatedAtUtc, record.CreatedAtUtc);
        Assert.Equal(UpdatedAtUtc, record.UpdatedAtUtc);
    }

    [Fact]
    public void ToRecordPreservesNullNote()
    {
        TradeMistake tradeMistake = TradeMistake.Rehydrate(
            AssociationId,
            TradeId,
            TradingMistakeId,
            null,
            CreatedAtUtc,
            UpdatedAtUtc);

        TradeMistakeRecord record =
            TradeMistakePersistenceMapper.ToRecord(tradeMistake);

        Assert.Null(record.Note);
    }

    [Fact]
    public void ToRecordRejectsNullTradeMistake()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TradeMistakePersistenceMapper.ToRecord(null!));
    }

    [Fact]
    public void ToDomainRehydratesAllPersistedTradeMistakeState()
    {
        TradeMistakeRecord record = CreateValidRecord();

        TradeMistake tradeMistake =
            TradeMistakePersistenceMapper.ToDomain(record);

        Assert.Equal(record.Id, tradeMistake.Id);
        Assert.Equal(record.TradeId, tradeMistake.TradeId);
        Assert.Equal(record.TradingMistakeId, tradeMistake.TradingMistakeId);
        Assert.Equal(record.Note, tradeMistake.Note);
        Assert.Equal(record.CreatedAtUtc, tradeMistake.CreatedAtUtc);
        Assert.Equal(record.UpdatedAtUtc, tradeMistake.UpdatedAtUtc);
    }

    [Fact]
    public void ToDomainDelegatesWhitespaceNoteNormalizationToDomain()
    {
        TradeMistakeRecord record = CreateValidRecord();
        record.Note = "   ";

        TradeMistake tradeMistake =
            TradeMistakePersistenceMapper.ToDomain(record);

        Assert.Null(tradeMistake.Note);
    }

    [Fact]
    public void ToDomainRejectsNullRecord()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TradeMistakePersistenceMapper.ToDomain(null!));
    }

    [Fact]
    public void ToDomainRejectsPersistedDataThatViolatesDomainInvariants()
    {
        AssertInvalid(record => record.Id = Guid.Empty);
        AssertInvalid(record => record.TradeId = Guid.Empty);
        AssertInvalid(record => record.TradingMistakeId = Guid.Empty);
        AssertInvalid(record => record.Note = new string('N', 2001));
        AssertInvalid(record =>
            record.CreatedAtUtc = CreatedAtUtc.ToOffset(TimeSpan.FromHours(2)));
        AssertInvalid(record =>
            record.UpdatedAtUtc = UpdatedAtUtc.ToOffset(TimeSpan.FromHours(-3)));
        AssertInvalid(record => record.UpdatedAtUtc = CreatedAtUtc.AddTicks(-1));
    }

    private static void AssertInvalid(Action<TradeMistakeRecord> corrupt)
    {
        TradeMistakeRecord record = CreateValidRecord();
        corrupt(record);

        Assert.ThrowsAny<ArgumentException>(() =>
            TradeMistakePersistenceMapper.ToDomain(record));
    }

    private static TradeMistake CreateTradeMistake()
    {
        return TradeMistake.Rehydrate(
            AssociationId,
            TradeId,
            TradingMistakeId,
            "Entered before planned confirmation.",
            CreatedAtUtc,
            UpdatedAtUtc);
    }

    private static TradeMistakeRecord CreateValidRecord()
    {
        return new TradeMistakeRecord
        {
            Id = AssociationId,
            TradeId = TradeId,
            TradingMistakeId = TradingMistakeId,
            Note = "Entered before planned confirmation.",
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = UpdatedAtUtc,
        };
    }
}
