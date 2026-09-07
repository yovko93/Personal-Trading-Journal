using PersonalTradingJournal.Domain.Common;

namespace PersonalTradingJournal.Domain.Tests.Common;

public sealed class AuditableEntityTests
{
    private static readonly Guid ExistingId =
        new("b64d9ad8-c202-4c95-9bf6-35be33f68f11");

    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 1, 10, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public void NewEntityUsesExplicitTimestampForCreationAndUpdate()
    {
        var entity = new TestAuditableEntity(CreatedAtUtc);

        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.Equal(CreatedAtUtc, entity.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, entity.UpdatedAtUtc);
    }

    [Fact]
    public void RehydratedEntityPreservesValidUtcTimestamps()
    {
        DateTimeOffset updatedAtUtc = CreatedAtUtc.AddHours(2);

        var entity = new TestAuditableEntity(ExistingId, CreatedAtUtc, updatedAtUtc);

        Assert.Equal(CreatedAtUtc, entity.CreatedAtUtc);
        Assert.Equal(updatedAtUtc, entity.UpdatedAtUtc);
    }

    [Fact]
    public void RehydrationRejectsCreatedTimestampLaterThanUpdatedTimestamp()
    {
        DateTimeOffset updatedAtUtc = CreatedAtUtc.AddTicks(-1);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TestAuditableEntity(ExistingId, CreatedAtUtc, updatedAtUtc));
    }

    [Fact]
    public void CreationRejectsNonUtcCreatedTimestamp()
    {
        var nonUtcTimestamp = new DateTimeOffset(
            2026,
            1,
            10,
            11,
            30,
            0,
            TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(() => new TestAuditableEntity(nonUtcTimestamp));
    }

    [Fact]
    public void RehydrationRejectsNonUtcUpdatedTimestamp()
    {
        var nonUtcTimestamp = new DateTimeOffset(
            2026,
            1,
            10,
            13,
            30,
            0,
            TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(
            () => new TestAuditableEntity(ExistingId, CreatedAtUtc, nonUtcTimestamp));
    }

    [Fact]
    public void UpdatedTimestampCanAdvance()
    {
        var entity = new TestAuditableEntity(CreatedAtUtc);
        DateTimeOffset updatedAtUtc = CreatedAtUtc.AddMinutes(15);

        entity.UpdateTimestamp(updatedAtUtc);

        Assert.Equal(updatedAtUtc, entity.UpdatedAtUtc);
    }

    [Fact]
    public void UpdatedTimestampCannotMoveBackwards()
    {
        DateTimeOffset currentUpdatedAtUtc = CreatedAtUtc.AddMinutes(15);
        var entity = new TestAuditableEntity(
            ExistingId,
            CreatedAtUtc,
            currentUpdatedAtUtc);

        DateTimeOffset earlierUpdatedAtUtc = currentUpdatedAtUtc.AddTicks(-1);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => entity.UpdateTimestamp(earlierUpdatedAtUtc));
    }

    [Fact]
    public void UpdatedTimestampCannotBecomeEarlierThanCreatedTimestamp()
    {
        var entity = new TestAuditableEntity(CreatedAtUtc);
        DateTimeOffset earlierThanCreatedAtUtc = CreatedAtUtc.AddTicks(-1);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => entity.UpdateTimestamp(earlierThanCreatedAtUtc));
    }

    [Fact]
    public void UpdatedTimestampRejectsNonUtcOffset()
    {
        var entity = new TestAuditableEntity(CreatedAtUtc);
        var nonUtcTimestamp = new DateTimeOffset(
            2026,
            1,
            10,
            13,
            30,
            0,
            TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(() => entity.UpdateTimestamp(nonUtcTimestamp));
    }

    private sealed class TestAuditableEntity : AuditableEntity
    {
        public TestAuditableEntity(DateTimeOffset createdAtUtc)
            : base(createdAtUtc)
        {
        }

        public TestAuditableEntity(
            Guid id,
            DateTimeOffset createdAtUtc,
            DateTimeOffset updatedAtUtc)
            : base(id, createdAtUtc, updatedAtUtc)
        {
        }

        public void UpdateTimestamp(DateTimeOffset updatedAtUtc)
        {
            SetUpdatedAtUtc(updatedAtUtc);
        }
    }
}
