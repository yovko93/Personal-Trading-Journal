using PersonalTradingJournal.Domain.Screenshots;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Screenshots;

public sealed class TradeScreenshotPersistenceMapperTests
{
    private static readonly Guid ScreenshotId =
        Guid.Parse("8f4826c5-d9cc-43b2-a296-87c88db77682");

    private static readonly Guid TradeId =
        Guid.Parse("3df77d28-0696-44e2-894c-ed77572e43e9");

    private static readonly DateTimeOffset CapturedAtUtc =
        new(2026, 8, 20, 13, 45, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 8, 20, 14, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset UpdatedAtUtc =
        CreatedAtUtc.AddMinutes(30);

    [Fact]
    public void ToRecordMapsAllPersistedScreenshotState()
    {
        TradeScreenshot screenshot = CreateScreenshot();

        TradeScreenshotRecord record =
            TradeScreenshotPersistenceMapper.ToRecord(screenshot);

        Assert.Equal(ScreenshotId, record.Id);
        Assert.Equal(TradeId, record.TradeId);
        Assert.Equal(TradeScreenshotType.Entry, record.Type);
        Assert.Equal(
            "future/object-store/trades/abc/chart-01",
            record.StorageKey);
        Assert.Equal("nq-entry.png", record.FileName);
        Assert.Equal(CapturedAtUtc, record.CapturedAtUtc);
        Assert.Equal("2m", record.Timeframe);
        Assert.Equal("Entry after liquidity sweep.", record.Description);
        Assert.Equal(CreatedAtUtc, record.CreatedAtUtc);
        Assert.Equal(UpdatedAtUtc, record.UpdatedAtUtc);
    }

    [Fact]
    public void ToRecordPreservesNullOptionalMetadata()
    {
        TradeScreenshot screenshot = TradeScreenshot.Rehydrate(
            ScreenshotId,
            TradeId,
            TradeScreenshotType.Entry,
            "future/object-store/trades/abc/chart-01",
            "nq-entry.png",
            null,
            null,
            null,
            CreatedAtUtc,
            UpdatedAtUtc);

        TradeScreenshotRecord record =
            TradeScreenshotPersistenceMapper.ToRecord(screenshot);

        Assert.Null(record.CapturedAtUtc);
        Assert.Null(record.Timeframe);
        Assert.Null(record.Description);
    }

    [Fact]
    public void ToRecordRejectsNullScreenshot()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TradeScreenshotPersistenceMapper.ToRecord(null!));
    }

    [Fact]
    public void ToDomainRehydratesAllPersistedScreenshotState()
    {
        TradeScreenshotRecord record = CreateValidRecord();

        TradeScreenshot screenshot =
            TradeScreenshotPersistenceMapper.ToDomain(record);

        Assert.Equal(record.Id, screenshot.Id);
        Assert.Equal(record.TradeId, screenshot.TradeId);
        Assert.Equal(record.Type, screenshot.Type);
        Assert.Equal(record.StorageKey, screenshot.StorageKey);
        Assert.Equal(record.FileName, screenshot.FileName);
        Assert.Equal(record.CapturedAtUtc, screenshot.CapturedAtUtc);
        Assert.Equal(record.Timeframe, screenshot.Timeframe);
        Assert.Equal(record.Description, screenshot.Description);
        Assert.Equal(record.CreatedAtUtc, screenshot.CreatedAtUtc);
        Assert.Equal(record.UpdatedAtUtc, screenshot.UpdatedAtUtc);
    }

    [Fact]
    public void ToDomainPreservesNullOptionalMetadataAndOpaqueStorageKey()
    {
        TradeScreenshotRecord record = CreateValidRecord();
        record.StorageKey = "future/container/object-key";
        record.CapturedAtUtc = null;
        record.Timeframe = null;
        record.Description = null;

        TradeScreenshot screenshot =
            TradeScreenshotPersistenceMapper.ToDomain(record);

        Assert.Equal("future/container/object-key", screenshot.StorageKey);
        Assert.Null(screenshot.CapturedAtUtc);
        Assert.Null(screenshot.Timeframe);
        Assert.Null(screenshot.Description);
    }

    [Fact]
    public void ToDomainRejectsNullRecord()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TradeScreenshotPersistenceMapper.ToDomain(null!));
    }

    [Fact]
    public void ToDomainRejectsPersistedDataThatViolatesDomainInvariants()
    {
        AssertInvalid(record => record.Id = Guid.Empty);
        AssertInvalid(record => record.TradeId = Guid.Empty);
        AssertInvalid(record => record.Type = (TradeScreenshotType)999);
        AssertInvalid(record => record.StorageKey = "   ");
        AssertInvalid(record => record.StorageKey = new string('K', 513));
        AssertInvalid(record => record.FileName = "   ");
        AssertInvalid(record => record.FileName = new string('F', 256));
        AssertInvalid(record =>
            record.CapturedAtUtc = CapturedAtUtc.ToOffset(TimeSpan.FromHours(2)));
        AssertInvalid(record => record.Timeframe = new string('T', 33));
        AssertInvalid(record => record.Description = new string('D', 2001));
        AssertInvalid(record =>
            record.CreatedAtUtc = CreatedAtUtc.ToOffset(TimeSpan.FromHours(2)));
        AssertInvalid(record =>
            record.UpdatedAtUtc = UpdatedAtUtc.ToOffset(TimeSpan.FromHours(2)));
        AssertInvalid(record => record.UpdatedAtUtc = CreatedAtUtc.AddTicks(-1));
    }

    private static void AssertInvalid(Action<TradeScreenshotRecord> corrupt)
    {
        TradeScreenshotRecord record = CreateValidRecord();
        corrupt(record);

        Assert.ThrowsAny<ArgumentException>(() =>
            TradeScreenshotPersistenceMapper.ToDomain(record));
    }

    private static TradeScreenshot CreateScreenshot()
    {
        return TradeScreenshot.Rehydrate(
            ScreenshotId,
            TradeId,
            TradeScreenshotType.Entry,
            "future/object-store/trades/abc/chart-01",
            "nq-entry.png",
            CapturedAtUtc,
            "2m",
            "Entry after liquidity sweep.",
            CreatedAtUtc,
            UpdatedAtUtc);
    }

    private static TradeScreenshotRecord CreateValidRecord()
    {
        return new TradeScreenshotRecord
        {
            Id = ScreenshotId,
            TradeId = TradeId,
            Type = TradeScreenshotType.Entry,
            StorageKey = "future/object-store/trades/abc/chart-01",
            FileName = "nq-entry.png",
            CapturedAtUtc = CapturedAtUtc,
            Timeframe = "2m",
            Description = "Entry after liquidity sweep.",
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = UpdatedAtUtc,
        };
    }
}
