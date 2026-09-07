using PersonalTradingJournal.Domain.Common;

namespace PersonalTradingJournal.Domain.Tests.Common;

public sealed class EntityTests
{
    [Fact]
    public void NewEntityReceivesNonEmptyIdentifier()
    {
        var entity = new TestEntity();

        Assert.NotEqual(Guid.Empty, entity.Id);
    }

    [Fact]
    public void RehydratedEntityPreservesIdentifier()
    {
        var id = new Guid("9f305286-68ef-4867-ac20-939b621a9cff");

        var entity = new TestEntity(id);

        Assert.Equal(id, entity.Id);
    }

    [Fact]
    public void RehydratedEntityRejectsEmptyIdentifier()
    {
        Assert.Throws<ArgumentException>(() => new TestEntity(Guid.Empty));
    }

    private sealed class TestEntity : Entity
    {
        public TestEntity()
        {
        }

        public TestEntity(Guid id)
            : base(id)
        {
        }
    }
}
