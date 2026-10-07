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
    private static readonly DailyReviewAnswers CompleteReview = new(
        "I followed my entry plan.", "Reduce late entries.", "Wait for confirmation.");

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
        var journal = new DailyJournalEntry(TradingDate, null, text, true, CreatedAtUtc, CompleteReview);

        Assert.Equal(text, journal.Text);
        Assert.True(journal.IsDraft);
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
            JournalId, TradingDate, AccountId, text, false, 12, CreatedAtUtc, updatedAtUtc, CompleteReview);

        Assert.Equal(JournalId, journal.Id);
        Assert.Equal(TradingDate, journal.TradingDate);
        Assert.Equal(AccountId, journal.TradingAccountId);
        Assert.Equal(text, journal.Text);
        Assert.Equal(CompleteReview, journal.Review);
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

        Assert.True(journal.UpdateContent("  changed\r\n", false, updatedAtUtc, CompleteReview));

        Assert.Equal(JournalId, journal.Id);
        Assert.Equal(TradingDate, journal.TradingDate);
        Assert.Equal(AccountId, journal.TradingAccountId);
        Assert.Equal("  changed\r\n", journal.Text);
        Assert.False(journal.IsDraft);
        Assert.Equal(CompleteReview, journal.Review);
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
        Assert.True(journal.UpdateContent("review ", false, CreatedAtUtc, CompleteReview));
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
            "changed", true, currentUpdatedAtUtc.ToOffset(TimeSpan.FromHours(2))));
        Assert.Throws<ArgumentOutOfRangeException>(() => journal.UpdateContent(
            "changed", true, currentUpdatedAtUtc.AddTicks(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => journal.UpdateContent(
            "changed", true, CreatedAtUtc.AddTicks(-1)));

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
            journal.UpdateContent("changed", true, CreatedAtUtc.AddMinutes(1)));

        Assert.Equal("review", journal.Text);
        Assert.True(journal.IsDraft);
        Assert.Equal(long.MaxValue, journal.Revision);
        Assert.Equal(CreatedAtUtc, journal.UpdatedAtUtc);
    }

    [Fact]
    public void PartialAnswersCanBeSavedWithoutFreeformTextOrTrades()
    {
        var review = new DailyReviewAnswers("  Patient entries\r\n", string.Empty, "\t");
        var journal = new DailyJournalEntry(TradingDate, null, string.Empty, true, CreatedAtUtc, review);

        Assert.Equal(review, journal.Review);
        Assert.True(journal.IsDraft);
        Assert.True(journal.UpdateContent(string.Empty, true, CreatedAtUtc.AddMinutes(1),
            new DailyReviewAnswers(review.WentWell, "Better sizing", review.NextTradingDay)));
        Assert.Equal("  Patient entries\r\n", journal.Review.WentWell);
        Assert.Equal(2L, journal.Revision);
    }

    [Theory]
    [InlineData("", "", "")]
    [InlineData(" ", "\t\r\n", "")]
    [InlineData("...", "🙂", "...!? 🙂 \u200B")]
    public void NonMeaningfulContentRejectsCompletionWithoutMutatingEntry(
        string wentWell, string needsImprovement, string nextTradingDay)
    {
        var review = new DailyReviewAnswers(wentWell, needsImprovement, nextTradingDay);
        DailyJournalEntry journal = Rehydrate();

        Assert.Throws<ArgumentException>(() => new DailyJournalEntry(
            TradingDate, null, " \t", false, CreatedAtUtc, review));
        Assert.Throws<ArgumentException>(() => journal.UpdateContent(
            "...!?", false, CreatedAtUtc.AddMinutes(1), review));

        AssertOriginalContent(journal);
        Assert.Equal(DailyReviewAnswers.Empty, journal.Review);
    }

    [Theory]
    [InlineData("  Plan\r\n ", "", "", "")]
    [InlineData("文", "  文 ", "", "")]
    [InlineData("٢", "", "٢", "")]
    [InlineData("\U00010400", "", "", "\U00010400")]
    [InlineData("Notes", "Partial", "...", "")]
    public void MeaningfulJournalTextCanCompleteWithOptionalAnswersWithoutTrimming(string text, string well, string improve, string next)
    {
        var answers = new DailyReviewAnswers(well, improve, next);
        var journal = new DailyJournalEntry(TradingDate, AccountId, text, false, CreatedAtUtc, answers);
        Assert.False(journal.IsDraft);
        Assert.Equal(text, journal.Text);
        Assert.Equal(answers, journal.Review);
        Assert.Equal(1, journal.Revision);
        Assert.Throws<InvalidOperationException>(() => journal.UpdateContent("New", false, CreatedAtUtc, answers));
    }

    [Fact]
    public void CompleteThenReopenPreservesScopeAndAnswersAndAddsDurableRevisionNumbers()
    {
        DailyJournalEntry journal = Rehydrate();

        Assert.True(journal.UpdateContent("Journal", false, CreatedAtUtc.AddMinutes(1), CompleteReview));
        Assert.False(journal.IsDraft);
        Assert.Equal(2L, journal.Revision);
        Assert.True(journal.UpdateContent("Journal", true, CreatedAtUtc.AddMinutes(2), CompleteReview));
        Assert.True(journal.IsDraft);
        Assert.Equal(3L, journal.Revision);
        Assert.True(journal.UpdateContent("New note", true, CreatedAtUtc.AddMinutes(3),
            new DailyReviewAnswers("Updated", CompleteReview.NeedsImprovement, CompleteReview.NextTradingDay)));
        Assert.Equal(4L, journal.Revision);
        Assert.Equal(TradingDate, journal.TradingDate);
        Assert.Equal(AccountId, journal.TradingAccountId);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void CompletedReviewCannotChangeContentEvenWhenAttemptingToReopen(
        bool isDraft, bool changeAnswers)
    {
        var journal = new DailyJournalEntry(TradingDate, AccountId, "note", false, CreatedAtUtc, CompleteReview);
        DailyReviewAnswers review = changeAnswers
            ? new DailyReviewAnswers("Changed answer", CompleteReview.NeedsImprovement, CompleteReview.NextTradingDay)
            : CompleteReview;
        string text = changeAnswers ? "note" : "Changed note";

        Assert.Throws<InvalidOperationException>(() => journal.UpdateContent(
            text, isDraft, CreatedAtUtc.AddMinutes(1), review));

        Assert.False(journal.IsDraft);
        Assert.Equal("note", journal.Text);
        Assert.Equal(CompleteReview, journal.Review);
        Assert.Equal(1L, journal.Revision);
        Assert.Equal(CreatedAtUtc, journal.UpdatedAtUtc);
    }

    [Fact]
    public void LegacyCompletedEntryRemainsReadableAndMustBeReopenedBeforeEditing()
    {
        DailyJournalEntry journal = DailyJournalEntry.Rehydrate(
            JournalId, TradingDate, AccountId, "historic completed note", false, 8,
            CreatedAtUtc, CreatedAtUtc);

        Assert.Equal(DailyReviewAnswers.Empty, journal.Review);
        Assert.False(journal.UpdateContent(journal.Text, false, CreatedAtUtc.AddMinutes(1)));
        Assert.Throws<InvalidOperationException>(() => journal.UpdateContent(
            journal.Text, true, CreatedAtUtc.AddMinutes(1), CompleteReview));
        Assert.True(journal.UpdateContent(journal.Text, true, CreatedAtUtc.AddMinutes(1)));
        Assert.Equal(9L, journal.Revision);
        Assert.True(journal.UpdateContent(journal.Text, false, CreatedAtUtc.AddMinutes(2), CompleteReview));
        Assert.Equal(10L, journal.Revision);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t...")]
    public void AnswersCannotReplaceRequiredTextButLegacyCompletedAnswersRemainReadable(string text)
    {
        Assert.Throws<ArgumentException>(() => new DailyJournalEntry(TradingDate, AccountId, text, false, CreatedAtUtc, CompleteReview));
        var legacy = DailyJournalEntry.Rehydrate(JournalId, TradingDate, AccountId, text, false, 8, CreatedAtUtc, CreatedAtUtc, CompleteReview);
        Assert.Equal(text, legacy.Text);
        Assert.False(legacy.UpdateContent(text, false, CreatedAtUtc.AddMinutes(1), CompleteReview));
        Assert.True(legacy.UpdateContent(text, true, CreatedAtUtc.AddMinutes(1), CompleteReview));
        Assert.Throws<ArgumentException>(() => legacy.UpdateContent(text, false, CreatedAtUtc.AddMinutes(2), CompleteReview));
        Assert.Equal(9, legacy.Revision);
        Assert.True(legacy.UpdateContent("Journal", false, CreatedAtUtc.AddMinutes(2), CompleteReview));
    }

    [Fact]
    public void ReviewValueEqualityAndOlderUpdateOverloadPreserveAnswers()
    {
        var equivalent = new DailyReviewAnswers(
            CompleteReview.WentWell, CompleteReview.NeedsImprovement, CompleteReview.NextTradingDay);
        var journal = new DailyJournalEntry(TradingDate, AccountId, "note", true, CreatedAtUtc, CompleteReview);

        Assert.False(journal.UpdateContent("note", true, CreatedAtUtc.AddMinutes(1), equivalent));
        Assert.True(journal.UpdateContent("new note", true, CreatedAtUtc.AddMinutes(1)));
        Assert.Equal(CompleteReview, journal.Review);
    }

    [Fact]
    public void ReviewOnlyUpdateRejectsInvalidAuditAndRevisionOverflowAtomically()
    {
        DailyJournalEntry journal = Rehydrate(revision: long.MaxValue);
        Assert.Throws<OverflowException>(() => journal.UpdateContent(
            journal.Text, true, CreatedAtUtc.AddMinutes(1), CompleteReview));
        Assert.Equal(DailyReviewAnswers.Empty, journal.Review);
        Assert.Equal(long.MaxValue, journal.Revision);

        journal = Rehydrate();
        Assert.Throws<ArgumentException>(() => journal.UpdateContent(
            journal.Text, true, CreatedAtUtc.ToOffset(TimeSpan.FromHours(1)), CompleteReview));
        Assert.Equal(DailyReviewAnswers.Empty, journal.Review);
        AssertOriginalContent(journal);
    }

    [Fact]
    public void NullReviewCannotBeCreatedOrAppliedAndDoesNotMutateState()
    {
        Assert.Throws<ArgumentNullException>(() => new DailyJournalEntry(
            TradingDate, null, "note", true, CreatedAtUtc, null!));
        DailyJournalEntry journal = Rehydrate();
        Assert.Throws<ArgumentNullException>(() => journal.UpdateContent(
            "Changed", true, CreatedAtUtc.AddMinutes(1), null!));
        AssertOriginalContent(journal);
        Assert.Equal(DailyReviewAnswers.Empty, journal.Review);
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
