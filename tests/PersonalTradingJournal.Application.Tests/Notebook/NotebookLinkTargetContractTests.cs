using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Application.Tests.Notebook;

public sealed class NotebookLinkTargetContractTests
{
    [Theory]
    [InlineData(0)] [InlineData(101)] [InlineData(-1)]
    public void PickerRequiresBoundedPages(int limit) => Assert.Throws<ArgumentOutOfRangeException>(() => new NotebookTargetQuery(NotebookTargetKind.Trade, limit: limit));
    [Fact]
    public void PickerUsesOnlyExistingTypedTargetsAndBoundedSearch()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NotebookTargetQuery((NotebookTargetKind)3));
        Assert.ThrowsAny<ArgumentException>(() => new NotebookTargetQuery(NotebookTargetKind.Trade, new string('x', 257)));
        Assert.ThrowsAny<ArgumentException>(() => new NotebookTargetQuery(NotebookTargetKind.TradingSetup, cursor: new string('x', 2049)));
        var query = new NotebookTargetQuery(NotebookTargetKind.TradingSetup, "  name  ", 100, "cursor");
        Assert.Equal("name", query.Search); Assert.Equal(100, query.Limit); Assert.Equal("cursor", query.Cursor);
    }
}
