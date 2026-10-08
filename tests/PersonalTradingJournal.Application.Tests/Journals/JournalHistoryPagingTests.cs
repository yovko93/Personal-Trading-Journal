using PersonalTradingJournal.Application.Journals;

namespace PersonalTradingJournal.Application.Tests.Journals;

public sealed class JournalHistoryPagingTests
{
    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(1, 50, 0)]
    [InlineData(3, 20, 40)]
    [InlineData(int.MaxValue, 1, int.MaxValue - 1)]
    public void ValidPagesAreBounded(int page, int size, int offset) => Assert.Equal(offset, JournalHistoryPaging.Offset(page, size));

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 51)]
    [InlineData(int.MaxValue, 50)]
    public void InvalidOrOverflowingPagesAreRejected(int page, int size) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => JournalHistoryPaging.Offset(page, size));

    [Theory]
    [InlineData(0, 1, 20, false)]
    [InlineData(20, 1, 20, false)]
    [InlineData(21, 1, 20, true)]
    [InlineData(21, 2, 20, false)]
    public void NextPageDoesNotInventAnEmptyPage(int count, int page, int size, bool hasNext) =>
        Assert.Equal(hasNext, new JournalHistoryPage<int>([], count, page, size).HasNext);
}
