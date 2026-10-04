using PersonalTradingJournal.Domain.Journals;

namespace PersonalTradingJournal.Domain.Tests.Journals;

public sealed class DailyJournalEntryTests
{
    private static readonly DateOnly TradingDate = new(2026, 9, 29);
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 10, 4, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid AccountId =
        new("5daafc60-4789-4b5c-a471-55b01a1a9af6");
    private static readonly Guid JournalId =
        new("50f1e4a7-b4b3-4e62-94c0-8e4be05fab35");

    [Theory]
    [InlineData(null)]
    [InlineData("5daafc60-4789-4b5c-a471-55b01a1a9af6")]
    public void CreatesIndependentExactScopeWithDraftAndInitialRevision(string? accountIdText)
    {
        Guid? accountId = accountIdText is null ? null : Guid.Parse(accountIdText);
        var journal = new DailyJournalEntry(TradingDate, accountId, "review", CreatedAtUtc);

        Assert.NotEqual(Guid.Empty, journal.Id);
        Assert.Equal(TradingDate, journal.TradingDate);
        Assert.Equal(accountId, journal.TradingAccountId);
        Assert.Equal("review", journal.Text);
        Assert.True(journal.IsDraft);
        Assert.Equal(1L, journal.Revision);
        Assert.Equal(CreatedAtUtc, journal.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, journal.UpdatedAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \t\r\n")]
    [InlineData("  café 文 🙂\r\n  next line\n\r\n")]
    public void PreservesExactTextIncludingEmptyContent(string text)
    {
        var journal = new DailyJournalEntry(TradingDate, null, text, false, CreatedAtUtc);

        Assert.Equal(text, journal.Text);
        Assert.False(journal.IsDraft);
    }

    [Fact]
    public void EnforcesUtf16TextLimitWithoutTruncation()
    {
        string maximumText = string.Concat(Enumerable.Repeat("🙂", DailyJournalEntry.MaximumTextLength / 2));
        var journal = new DailyJournalEntry(TradingDate, null, maximumText, CreatedAtUtc);

        Assert.Equal(DailyJournalEntry.MaximumTextLength, journal.Text.Length);
        Assert.Equal(maximumText, journal.Text);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DailyJournalEntry(TradingDate, null, maximumText + "x", CreatedAtUtc));
    }

    [Fact]
    public void RejectsNullTextAndEmptyAccountId()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DailyJournalEntry(TradingDate, null, null!, CreatedAtUtc));
        Assert.Throws<ArgumentException>(() =>
            new DailyJournalEntry(TradingDate, Guid.Empty, "review", CreatedAtUtc));
    }

    [Fact]
    public void RehydratesEveryStoredValueWithoutChangingContent()
    {
        DateTimeOffset updatedAtUtc = CreatedAtUtc.AddMinutes(5);
        const string text = "  complete\r\n";
        DailyJournalEntry journal = DailyJournalEntry.Rehydrate(
            JournalId, TradingDate, AccountId, text, false, 12, CreatedAtUtc, updatedAtUtc);

        Assert.Equal(JournalId, journal.Id);
        Assert.Equal(TradingDate, journal.TradingDate);
        Assert.Equal(AccountId, journal.TradingAccountId);
        Assert.Equal(text, journal.Text);
        Assert.False(journal.IsDraft);
        Assert.Equal(12L, journal.Revision);
        Assert.Equal(CreatedAtUtc, journal.CreatedAtUtc);
        Assert.Equal(updatedAtUtc, journal.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RehydrationRejectsNonpositiveRevision(long revision)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DailyJournalEntry.Rehydrate(
            JournalId, TradingDate, AccountId, "review", true, revision, CreatedAtUtc, CreatedAtUtc));
    }

    [Fact]
    public void RehydrationUsesCreationValidation()
    {
        Assert.Throws<ArgumentException>(() => DailyJournalEntry.Rehydrate(
            Guid.Empty, TradingDate, AccountId, "review", true, 1, CreatedAtUtc, CreatedAtUtc));
        Assert.Throws<ArgumentException>(() => DailyJournalEntry.Rehydrate(
            JournalId, TradingDate, Guid.Empty, "review", true, 1, CreatedAtUtc, CreatedAtUtc));
        Assert.Throws<ArgumentNullException>(() => DailyJournalEntry.Rehydrate(
            JournalId, TradingDate, AccountId, null!, true, 1, CreatedAtUtc, CreatedAtUtc));
        Assert.Throws<ArgumentOutOfRangeException>(() => DailyJournalEntry.Rehydrate(
            JournalId, TradingDate, AccountId, new string('x', DailyJournalEntry.MaximumTextLength + 1),
            true, 1, CreatedAtUtc, CreatedAtUtc));
    }

    [Fact]
    public void CreationAndRehydrationRequireValidUtcAudits()
    {
        Assert.Throws<ArgumentException>(() => new DailyJournalEntry(
            TradingDate, null, "review", CreatedAtUtc.ToOffset(TimeSpan.FromHours(2))));
        Assert.Throws<ArgumentException>(() => DailyJournalEntry.Rehydrate(
            JournalId, TradingDate, null, "review", true, 1,
            CreatedAtUtc, CreatedAtUtc.ToOffset(TimeSpan.FromHours(2))));
        Assert.Throws<ArgumentOutOfRangeException>(() => DailyJournalEntry.Rehydrate(
            JournalId, TradingDate, null, "review", true, 1,
            CreatedAtUtc, CreatedAtUtc.AddTicks(-1)));
    }

    [Fact]
    public void ChangedContentAdvancesRevisionAndAuditButPreservesIdentityAndScope()
    {
        DailyJournalEntry journal = Rehydrate();
        DateTimeOffset updatedAtUtc = CreatedAtUtc.AddMinutes(5);

        Assert.True(journal.UpdateContent("  changed\r\n", false, updatedAtUtc));

        Assert.Equal(JournalId, journal.Id);
        Assert.Equal(TradingDate, journal.TradingDate);
        Assert.Equal(AccountId, journal.TradingAccountId);
        Assert.Equal("  changed\r\n", journal.Text);
        Assert.False(journal.IsDraft);
        Assert.Equal(2L, journal.Revision);
        Assert.Equal(CreatedAtUtc, journal.CreatedAtUtc);
        Assert.Equal(updatedAtUtc, journal.UpdatedAtUtc);
    }

    [Fact]
    public void SameTextAndDraftStatePreserveRevisionAndAudit()
    {
        DailyJournalEntry journal = Rehydrate();

        Assert.False(journal.UpdateContent("review", true, CreatedAtUtc.AddDays(1)));

        Assert.Equal(1L, journal.Revision);
        Assert.Equal(CreatedAtUtc, journal.UpdatedAtUtc);
    }

    [Fact]
    public void WhitespaceAndDraftOnlyChangesEachCreateARevision()
    {
        DailyJournalEntry journal = Rehydrate();

        Assert.True(journal.UpdateContent("review ", true, CreatedAtUtc));
        Assert.Equal(2L, journal.Revision);
        Assert.True(journal.UpdateContent("review ", false, CreatedAtUtc));
        Assert.Equal(3L, journal.Revision);
        Assert.True(journal.UpdateContent("review ", true, CreatedAtUtc));
        Assert.Equal(4L, journal.Revision);
        Assert.Equal(CreatedAtUtc, journal.UpdatedAtUtc);
    }

    [Fact]
    public void InvalidTextLeavesAllStateUntouched()
    {
        DailyJournalEntry journal = Rehydrate();

        Assert.Throws<ArgumentNullException>(() =>
            journal.UpdateContent(null!, false, CreatedAtUtc.AddMinutes(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => journal.UpdateContent(
            new string('x', DailyJournalEntry.MaximumTextLength + 1), false, CreatedAtUtc.AddMinutes(1)));

        AssertOriginalContent(journal);
    }

    [Fact]
    public void InvalidUpdateAuditLeavesContentDraftAndRevisionUntouched()
    {
        DateTimeOffset currentUpdatedAtUtc = CreatedAtUtc.AddMinutes(5);
        DailyJournalEntry journal = Rehydrate(updatedAtUtc: currentUpdatedAtUtc);

        Assert.Throws<ArgumentException>(() => journal.UpdateContent(
            "changed", false, currentUpdatedAtUtc.ToOffset(TimeSpan.FromHours(2))));
        Assert.Throws<ArgumentOutOfRangeException>(() => journal.UpdateContent(
            "changed", false, currentUpdatedAtUtc.AddTicks(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => journal.UpdateContent(
            "changed", false, CreatedAtUtc.AddTicks(-1)));

        Assert.Equal("review", journal.Text);
        Assert.True(journal.IsDraft);
        Assert.Equal(1L, journal.Revision);
        Assert.Equal(currentUpdatedAtUtc, journal.UpdatedAtUtc);
    }

    [Fact]
    public void RevisionOverflowIsAtomicAndAnUnchangedMaximumRevisionRemainsReadable()
    {
        DailyJournalEntry journal = Rehydrate(revision: long.MaxValue);

        Assert.False(journal.UpdateContent("review", true, CreatedAtUtc.AddMinutes(1)));
        Assert.Throws<OverflowException>(() =>
            journal.UpdateContent("changed", false, CreatedAtUtc.AddMinutes(1)));

        Assert.Equal("review", journal.Text);
        Assert.True(journal.IsDraft);
        Assert.Equal(long.MaxValue, journal.Revision);
        Assert.Equal(CreatedAtUtc, journal.UpdatedAtUtc);
    }

    private static DailyJournalEntry Rehydrate(long revision = 1, DateTimeOffset? updatedAtUtc = null) =>
        DailyJournalEntry.Rehydrate(JournalId, TradingDate, AccountId, "review", true,
            revision, CreatedAtUtc, updatedAtUtc ?? CreatedAtUtc);

    private static void AssertOriginalContent(DailyJournalEntry journal)
    {
        Assert.Equal("review", journal.Text);
        Assert.True(journal.IsDraft);
        Assert.Equal(1L, journal.Revision);
        Assert.Equal(CreatedAtUtc, journal.UpdatedAtUtc);
    }
}
