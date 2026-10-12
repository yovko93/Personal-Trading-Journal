using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Domain.Tests.Setups;

public sealed class PlaybookExampleImageTests
{
    [Theory]
    [InlineData("../bad.png")] [InlineData("C:\\bad.png")] [InlineData("bad.gif")] [InlineData("bad.png ")] [InlineData("bad\n.png")]
    public void UnsafeOrUnsupportedNamesRejected(string name) => Assert.Throws<ArgumentException>(() => new PlaybookExampleImage(name, "chart.png", 1, new string('a', 64)));
    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(8388609)]
    public void BoundedPositiveBytesRequired(long length) => Assert.Throws<ArgumentOutOfRangeException>(() => new PlaybookExampleImage("chart.png", "chart.png", length, new string('a', 64)));
    [Fact]
    public void FrozenImageRequiresUserClassificationAndCannotCompeteWithTradeScreenshotOwnership()
    {
        var image = new PlaybookExampleImage("chart.png", "chart.png", 1, new string('a', 64));
        Assert.Equal(new string('A', 64), image.Sha256);
        Assert.Throws<ArgumentException>(() => new PlaybookExample(Guid.NewGuid(), "note", image: image));
        Assert.Throws<ArgumentException>(() => new PlaybookExample(Guid.NewGuid(), "note", Guid.NewGuid(), Guid.NewGuid(), PlaybookExampleKind.Valid, image));
        Assert.Equal(PlaybookExampleKind.Unspecified, new PlaybookExample(Guid.NewGuid(), "legacy").Kind);
        Assert.Same(image, new PlaybookExample(Guid.NewGuid(), "valid", kind: PlaybookExampleKind.Valid, image: image).Image);
    }
}
