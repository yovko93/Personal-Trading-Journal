using PersonalTradingJournal.Domain.Screenshots;

namespace PersonalTradingJournal.Domain.Tests.Screenshots;

public sealed class TradeScreenshotTests
{
    private static readonly Guid TradeId =
        new("22c82a2b-2443-489d-8464-21af8601aff3");

    private static readonly Guid ExistingScreenshotId =
        new("7b9f39e5-c3ac-49b3-a716-2d2cb7e52255");

    private static readonly DateTimeOffset CapturedAtUtc =
        new(2026, 1, 10, 15, 30, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 8, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(TradeScreenshotType.PreTrade)]
    [InlineData(TradeScreenshotType.Entry)]
    [InlineData(TradeScreenshotType.Management)]
    [InlineData(TradeScreenshotType.Exit)]
    [InlineData(TradeScreenshotType.PostTrade)]
    [InlineData(TradeScreenshotType.Other)]
    public void CreatesValidScreenshotOfEachDefinedType(TradeScreenshotType type)
    {
        TradeScreenshot screenshot = CreateScreenshot(
            type: type,
            capturedAtUtc: CapturedAtUtc);

        Assert.NotEqual(Guid.Empty, screenshot.Id);
        Assert.Equal(TradeId, screenshot.TradeId);
        Assert.Equal(type, screenshot.Type);
        Assert.Equal("screenshots/trade-1/NQ-Entry.png", screenshot.StorageKey);
        Assert.Equal("NQ-Entry.png", screenshot.FileName);
        Assert.Equal(CapturedAtUtc, screenshot.CapturedAtUtc);
        Assert.Equal("5m", screenshot.Timeframe);
        Assert.Equal("Entry after sell-side sweep.", screenshot.Description);
        Assert.Equal(CreatedAtUtc, screenshot.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, screenshot.UpdatedAtUtc);
    }

    [Fact]
    public void RejectsEmptyTradeIdentifier()
    {
        Assert.Throws<ArgumentException>(
            () => CreateScreenshot(tradeId: Guid.Empty));
    }

    [Theory]
    [InlineData((TradeScreenshotType)0)]
    [InlineData((TradeScreenshotType)999)]
    public void RejectsUndefinedType(TradeScreenshotType type)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateScreenshot(type: type));
    }

    [Fact]
    public void PreservesValidStorageKey()
    {
        const string storageKey = "screenshots/trade-1/image-1.png";

        TradeScreenshot screenshot = CreateScreenshot(storageKey: storageKey);

        Assert.Equal(storageKey, screenshot.StorageKey);
    }

    [Fact]
    public void TrimsStorageKeyAndPreservesCasing()
    {
        TradeScreenshot screenshot = CreateScreenshot(
            storageKey: "  TradeS/AbC-123/Image.File-PNG  ");

        Assert.Equal("TradeS/AbC-123/Image.File-PNG", screenshot.StorageKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsMissingStorageKey(string? storageKey)
    {
        Assert.Throws<ArgumentException>(
            () => CreateScreenshot(storageKey: storageKey!));
    }

    [Fact]
    public void RejectsStorageKeyLongerThanMaximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateScreenshot(storageKey: new string('K', 513)));
    }

    [Fact]
    public void PreservesValidFileName()
    {
        const string fileName = "2026-09-08-review.jpg";

        TradeScreenshot screenshot = CreateScreenshot(fileName: fileName);

        Assert.Equal(fileName, screenshot.FileName);
    }

    [Fact]
    public void TrimsFileNameAndPreservesCasingAndPunctuation()
    {
        TradeScreenshot screenshot = CreateScreenshot(
            fileName: "  NQ.Entry-Review_v2.PNG  ");

        Assert.Equal("NQ.Entry-Review_v2.PNG", screenshot.FileName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsMissingFileName(string? fileName)
    {
        Assert.Throws<ArgumentException>(
            () => CreateScreenshot(fileName: fileName!));
    }

    [Fact]
    public void RejectsFileNameLongerThanMaximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateScreenshot(fileName: new string('F', 256)));
    }

    [Fact]
    public void AcceptsNullCapturedTimestamp()
    {
        TradeScreenshot screenshot = CreateScreenshot(capturedAtUtc: null);

        Assert.Null(screenshot.CapturedAtUtc);
    }

    [Fact]
    public void PreservesUtcHistoricalCapturedTimestamp()
    {
        TradeScreenshot screenshot = CreateScreenshot(capturedAtUtc: CapturedAtUtc);

        Assert.Equal(CapturedAtUtc, screenshot.CapturedAtUtc);
        Assert.True(screenshot.CapturedAtUtc < screenshot.CreatedAtUtc);
    }

    [Fact]
    public void AcceptsCapturedTimestampLaterThanCreationTimestamp()
    {
        DateTimeOffset capturedAtUtc = CreatedAtUtc.AddDays(1);

        TradeScreenshot screenshot = CreateScreenshot(capturedAtUtc: capturedAtUtc);

        Assert.Equal(capturedAtUtc, screenshot.CapturedAtUtc);
    }

    [Fact]
    public void RejectsNonUtcCapturedTimestamp()
    {
        var nonUtcTimestamp = new DateTimeOffset(
            2026,
            1,
            10,
            17,
            30,
            0,
            TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(
            () => CreateScreenshot(capturedAtUtc: nonUtcTimestamp));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConvertsMissingTimeframeToNull(string? timeframe)
    {
        TradeScreenshot screenshot = CreateScreenshot(timeframe: timeframe);

        Assert.Null(screenshot.Timeframe);
    }

    [Theory]
    [InlineData("5m")]
    [InlineData("1H")]
    [InlineData("Tick 2000")]
    public void PreservesFlexibleTimeframeValues(string timeframe)
    {
        TradeScreenshot screenshot = CreateScreenshot(timeframe: timeframe);

        Assert.Equal(timeframe, screenshot.Timeframe);
    }

    [Fact]
    public void TrimsTimeframeAndPreservesCasing()
    {
        TradeScreenshot screenshot = CreateScreenshot(timeframe: "  Tick 2000  ");

        Assert.Equal("Tick 2000", screenshot.Timeframe);
    }

    [Fact]
    public void RejectsTimeframeLongerThanMaximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateScreenshot(timeframe: new string('T', 33)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConvertsMissingDescriptionToNull(string? description)
    {
        TradeScreenshot screenshot = CreateScreenshot(description: description);

        Assert.Null(screenshot.Description);
    }

    [Fact]
    public void PreservesValidDescription()
    {
        const string description = "Pre-market liquidity map.";

        TradeScreenshot screenshot = CreateScreenshot(description: description);

        Assert.Equal(description, screenshot.Description);
    }

    [Fact]
    public void TrimsDescriptionAndPreservesCasing()
    {
        TradeScreenshot screenshot = CreateScreenshot(
            description: "  Entry after Sell-Side Sweep and bullish MSS.  ");

        Assert.Equal(
            "Entry after Sell-Side Sweep and bullish MSS.",
            screenshot.Description);
    }

    [Fact]
    public void RejectsDescriptionLongerThanMaximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateScreenshot(description: new string('D', 2001)));
    }

    [Fact]
    public void AcceptsUtcCreationTimestamp()
    {
        TradeScreenshot screenshot = CreateScreenshot(createdAtUtc: CreatedAtUtc);

        Assert.Equal(CreatedAtUtc, screenshot.CreatedAtUtc);
    }

    [Fact]
    public void RejectsNonUtcCreationTimestamp()
    {
        var nonUtcTimestamp = new DateTimeOffset(
            2026,
            9,
            8,
            11,
            0,
            0,
            TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(
            () => CreateScreenshot(createdAtUtc: nonUtcTimestamp));
    }

    [Fact]
    public void RehydratesAllPersistedMetadata()
    {
        DateTimeOffset updatedAtUtc = CreatedAtUtc.AddDays(1);

        TradeScreenshot screenshot = TradeScreenshot.Rehydrate(
            ExistingScreenshotId,
            TradeId,
            TradeScreenshotType.PostTrade,
            "screenshots/trade-1/review.webp",
            "review.webp",
            CapturedAtUtc,
            "15m",
            "Post-trade review chart.",
            CreatedAtUtc,
            updatedAtUtc);

        Assert.Equal(ExistingScreenshotId, screenshot.Id);
        Assert.Equal(TradeId, screenshot.TradeId);
        Assert.Equal(TradeScreenshotType.PostTrade, screenshot.Type);
        Assert.Equal("screenshots/trade-1/review.webp", screenshot.StorageKey);
        Assert.Equal("review.webp", screenshot.FileName);
        Assert.Equal(CapturedAtUtc, screenshot.CapturedAtUtc);
        Assert.Equal("15m", screenshot.Timeframe);
        Assert.Equal("Post-trade review chart.", screenshot.Description);
        Assert.Equal(CreatedAtUtc, screenshot.CreatedAtUtc);
        Assert.Equal(updatedAtUtc, screenshot.UpdatedAtUtc);
    }

    [Fact]
    public void RehydrationAppliesNormalTextNormalization()
    {
        TradeScreenshot screenshot = RehydrateScreenshot(
            storageKey: "  Screenshots/Trade-1/Image.PNG  ",
            fileName: "  Image.PNG  ",
            timeframe: "  4H  ",
            description: "  Review Context.  ");

        Assert.Equal("Screenshots/Trade-1/Image.PNG", screenshot.StorageKey);
        Assert.Equal("Image.PNG", screenshot.FileName);
        Assert.Equal("4H", screenshot.Timeframe);
        Assert.Equal("Review Context.", screenshot.Description);
    }

    [Fact]
    public void RehydrationRejectsEmptyEntityIdentifier()
    {
        Assert.Throws<ArgumentException>(
            () => RehydrateScreenshot(id: Guid.Empty));
    }

    [Fact]
    public void RehydrationRejectsEmptyTradeIdentifier()
    {
        Assert.Throws<ArgumentException>(
            () => RehydrateScreenshot(tradeId: Guid.Empty));
    }

    [Theory]
    [InlineData((TradeScreenshotType)0)]
    [InlineData((TradeScreenshotType)999)]
    public void RehydrationRejectsUndefinedType(TradeScreenshotType type)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RehydrateScreenshot(type: type));
    }

    [Fact]
    public void RehydrationRejectsInvalidMetadata()
    {
        Assert.Throws<ArgumentException>(
            () => RehydrateScreenshot(storageKey: "   "));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RehydrateScreenshot(timeframe: new string('T', 33)));

        var nonUtcCapturedAt = new DateTimeOffset(
            2026,
            1,
            10,
            17,
            30,
            0,
            TimeSpan.FromHours(2));
        Assert.Throws<ArgumentException>(
            () => RehydrateScreenshot(capturedAtUtc: nonUtcCapturedAt));
    }

    [Fact]
    public void RehydrationRejectsInvalidAuditTimestamps()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RehydrateScreenshot(updatedAtUtc: CreatedAtUtc.AddTicks(-1)));

        var nonUtcTimestamp = new DateTimeOffset(
            2026,
            9,
            8,
            11,
            0,
            0,
            TimeSpan.FromHours(2));
        Assert.Throws<ArgumentException>(
            () => RehydrateScreenshot(updatedAtUtc: nonUtcTimestamp));
    }

    private static TradeScreenshot CreateScreenshot(
        Guid? tradeId = null,
        TradeScreenshotType type = TradeScreenshotType.Entry,
        string storageKey = "screenshots/trade-1/NQ-Entry.png",
        string fileName = "NQ-Entry.png",
        DateTimeOffset? capturedAtUtc = null,
        string? timeframe = "5m",
        string? description = "Entry after sell-side sweep.",
        DateTimeOffset? createdAtUtc = null)
    {
        return new TradeScreenshot(
            tradeId ?? TradeId,
            type,
            storageKey,
            fileName,
            capturedAtUtc,
            timeframe,
            description,
            createdAtUtc ?? CreatedAtUtc);
    }

    private static TradeScreenshot RehydrateScreenshot(
        Guid? id = null,
        Guid? tradeId = null,
        TradeScreenshotType type = TradeScreenshotType.Entry,
        string storageKey = "screenshots/trade-1/NQ-Entry.png",
        string fileName = "NQ-Entry.png",
        DateTimeOffset? capturedAtUtc = null,
        string? timeframe = "5m",
        string? description = "Entry after sell-side sweep.",
        DateTimeOffset? createdAtUtc = null,
        DateTimeOffset? updatedAtUtc = null)
    {
        return TradeScreenshot.Rehydrate(
            id ?? ExistingScreenshotId,
            tradeId ?? TradeId,
            type,
            storageKey,
            fileName,
            capturedAtUtc,
            timeframe,
            description,
            createdAtUtc ?? CreatedAtUtc,
            updatedAtUtc ?? CreatedAtUtc.AddMinutes(1));
    }
}
