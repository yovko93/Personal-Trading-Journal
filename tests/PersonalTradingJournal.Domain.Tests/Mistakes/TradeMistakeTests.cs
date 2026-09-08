using PersonalTradingJournal.Domain.Mistakes;

namespace PersonalTradingJournal.Domain.Tests.Mistakes;

public sealed class TradeMistakeTests
{
    private static readonly Guid TradeId =
        new("d9e572fc-4477-4a74-bc79-dd1d7f8c6c6e");

    private static readonly Guid TradingMistakeId =
        new("db66e2b2-7317-441f-88b6-8086a19aa46c");

    private static readonly Guid ExistingAssociationId =
        new("43add603-f5e5-409b-ab34-b8dd090787c1");

    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 7, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreatesValidTradeMistake()
    {
        TradeMistake association = CreateAssociation();

        Assert.NotEqual(Guid.Empty, association.Id);
        Assert.Equal(TradeId, association.TradeId);
        Assert.Equal(TradingMistakeId, association.TradingMistakeId);
        Assert.Equal(
            "Missed the first 5m FVG, then chased the move.",
            association.Note);
        Assert.Equal(CreatedAtUtc, association.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, association.UpdatedAtUtc);
    }

    [Fact]
    public void AcceptsNonEmptyTradeIdentifier()
    {
        Guid tradeId = new("6d2ea0d3-1605-4869-8332-3b52378780ad");

        TradeMistake association = CreateAssociation(tradeId: tradeId);

        Assert.Equal(tradeId, association.TradeId);
    }

    [Fact]
    public void RejectsEmptyTradeIdentifier()
    {
        Assert.Throws<ArgumentException>(
            () => CreateAssociation(tradeId: Guid.Empty));
    }

    [Fact]
    public void AcceptsNonEmptyTradingMistakeIdentifier()
    {
        Guid tradingMistakeId = new("8c6248af-dd43-429c-ae79-95664a4bd54c");

        TradeMistake association = CreateAssociation(
            tradingMistakeId: tradingMistakeId);

        Assert.Equal(tradingMistakeId, association.TradingMistakeId);
    }

    [Fact]
    public void RejectsEmptyTradingMistakeIdentifier()
    {
        Assert.Throws<ArgumentException>(
            () => CreateAssociation(tradingMistakeId: Guid.Empty));
    }

    [Fact]
    public void PreservesValidNote()
    {
        const string note = "Third trade after the daily plan allowed only two.";

        TradeMistake association = CreateAssociation(note: note);

        Assert.Equal(note, association.Note);
    }

    [Fact]
    public void TrimsNoteAndPreservesCasingAndPunctuation()
    {
        TradeMistake association = CreateAssociation(
            note: "  Moved Stop after Pullback #1 — ignored invalidation!  ");

        Assert.Equal(
            "Moved Stop after Pullback #1 — ignored invalidation!",
            association.Note);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConvertsMissingNoteToNull(string? note)
    {
        TradeMistake association = CreateAssociation(note: note);

        Assert.Null(association.Note);
    }

    [Fact]
    public void RejectsNoteLongerThanMaximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateAssociation(note: new string('N', 2001)));
    }

    [Fact]
    public void AcceptsUtcCreationTimestamp()
    {
        TradeMistake association = CreateAssociation(createdAtUtc: CreatedAtUtc);

        Assert.Equal(CreatedAtUtc, association.CreatedAtUtc);
    }

    [Fact]
    public void RejectsNonUtcCreationTimestamp()
    {
        var nonUtcTimestamp = new DateTimeOffset(
            2026,
            7,
            1,
            12,
            0,
            0,
            TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(
            () => CreateAssociation(createdAtUtc: nonUtcTimestamp));
    }

    [Fact]
    public void RehydratesAllPersistedValues()
    {
        DateTimeOffset updatedAtUtc = CreatedAtUtc.AddDays(1);

        TradeMistake association = TradeMistake.Rehydrate(
            ExistingAssociationId,
            TradeId,
            TradingMistakeId,
            "Moved stop instead of respecting invalidation.",
            CreatedAtUtc,
            updatedAtUtc);

        Assert.Equal(ExistingAssociationId, association.Id);
        Assert.Equal(TradeId, association.TradeId);
        Assert.Equal(TradingMistakeId, association.TradingMistakeId);
        Assert.Equal(
            "Moved stop instead of respecting invalidation.",
            association.Note);
        Assert.Equal(CreatedAtUtc, association.CreatedAtUtc);
        Assert.Equal(updatedAtUtc, association.UpdatedAtUtc);
    }

    [Fact]
    public void RehydrationPreservesNullNote()
    {
        TradeMistake association = RehydrateAssociation(note: null);

        Assert.Null(association.Note);
    }

    [Fact]
    public void RehydrationNormalizesNote()
    {
        TradeMistake association = RehydrateAssociation(
            note: "  Chased Price after missing Entry #1.  ");

        Assert.Equal("Chased Price after missing Entry #1.", association.Note);
    }

    [Fact]
    public void RehydrationRejectsEmptyEntityIdentifier()
    {
        Assert.Throws<ArgumentException>(
            () => RehydrateAssociation(id: Guid.Empty));
    }

    [Fact]
    public void RehydrationRejectsEmptyTradeIdentifier()
    {
        Assert.Throws<ArgumentException>(
            () => RehydrateAssociation(tradeId: Guid.Empty));
    }

    [Fact]
    public void RehydrationRejectsEmptyTradingMistakeIdentifier()
    {
        Assert.Throws<ArgumentException>(
            () => RehydrateAssociation(tradingMistakeId: Guid.Empty));
    }

    [Fact]
    public void RehydrationRejectsInvalidNote()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RehydrateAssociation(note: new string('N', 2001)));
    }

    [Fact]
    public void RehydrationRejectsInvalidAuditTimestamps()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RehydrateAssociation(updatedAtUtc: CreatedAtUtc.AddTicks(-1)));

        var nonUtcTimestamp = new DateTimeOffset(
            2026,
            7,
            1,
            12,
            0,
            0,
            TimeSpan.FromHours(2));
        Assert.Throws<ArgumentException>(
            () => RehydrateAssociation(updatedAtUtc: nonUtcTimestamp));
    }

    [Fact]
    public void SameTradeCanReferenceDifferentTradingMistakes()
    {
        Guid anotherTradingMistakeId =
            new("5b5552a6-29c3-42ea-99b5-f60952a97bdb");

        TradeMistake first = CreateAssociation();
        TradeMistake second = CreateAssociation(
            tradingMistakeId: anotherTradingMistakeId);

        Assert.Equal(first.TradeId, second.TradeId);
        Assert.NotEqual(first.TradingMistakeId, second.TradingMistakeId);
    }

    [Fact]
    public void SameTradingMistakeCanReferenceDifferentTrades()
    {
        Guid anotherTradeId = new("22180d7f-5f1e-4f61-8e1b-8244504a6e24");

        TradeMistake first = CreateAssociation();
        TradeMistake second = CreateAssociation(tradeId: anotherTradeId);

        Assert.Equal(first.TradingMistakeId, second.TradingMistakeId);
        Assert.NotEqual(first.TradeId, second.TradeId);
    }

    private static TradeMistake CreateAssociation(
        Guid? tradeId = null,
        Guid? tradingMistakeId = null,
        string? note = "Missed the first 5m FVG, then chased the move.",
        DateTimeOffset? createdAtUtc = null)
    {
        return new TradeMistake(
            tradeId ?? TradeId,
            tradingMistakeId ?? TradingMistakeId,
            note,
            createdAtUtc ?? CreatedAtUtc);
    }

    private static TradeMistake RehydrateAssociation(
        Guid? id = null,
        Guid? tradeId = null,
        Guid? tradingMistakeId = null,
        string? note = null,
        DateTimeOffset? createdAtUtc = null,
        DateTimeOffset? updatedAtUtc = null)
    {
        return TradeMistake.Rehydrate(
            id ?? ExistingAssociationId,
            tradeId ?? TradeId,
            tradingMistakeId ?? TradingMistakeId,
            note,
            createdAtUtc ?? CreatedAtUtc,
            updatedAtUtc ?? CreatedAtUtc.AddMinutes(1));
    }
}
