using PersonalTradingJournal.Domain.Mistakes;

namespace PersonalTradingJournal.Domain.Tests.Mistakes;

public sealed class TradingMistakeTests
{
    private static readonly Guid ExistingId =
        new("7d321e82-66fc-44a6-963a-45a911458cbe");

    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreatesValidTradingMistake()
    {
        TradingMistake mistake = CreateMistake();

        Assert.NotEqual(Guid.Empty, mistake.Id);
        Assert.Equal("Early Entry", mistake.Name);
        Assert.Equal("Entered before the planned confirmation.", mistake.Description);
        Assert.True(mistake.IsActive);
        Assert.Equal(CreatedAtUtc, mistake.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, mistake.UpdatedAtUtc);
    }

    [Fact]
    public void TrimsNameAndPreservesCasing()
    {
        TradingMistake mistake = CreateMistake(name: "  Revenge Trading  ");

        Assert.Equal("Revenge Trading", mistake.Name);
    }

    [Theory]
    [InlineData("FOMO")]
    [InlineData("Risked Too Much")]
    [InlineData("Entry: No Confirmation!")]
    public void AcceptsUserDefinedNamesWithSpacesAndPunctuation(string name)
    {
        TradingMistake mistake = CreateMistake(name: name);

        Assert.Equal(name, mistake.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsMissingName(string? name)
    {
        Assert.Throws<ArgumentException>(() => CreateMistake(name: name!));
    }

    [Fact]
    public void RejectsNameLongerThanMaximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateMistake(name: new string('N', 129)));
    }

    [Fact]
    public void PreservesValidDescription()
    {
        const string description =
            "Entered before confirmation because price was moving away.";

        TradingMistake mistake = CreateMistake(description: description);

        Assert.Equal(description, mistake.Description);
    }

    [Fact]
    public void TrimsDescriptionAndPreservesCasing()
    {
        TradingMistake mistake = CreateMistake(
            description: "  Moved Stop outside the planned level.  ");

        Assert.Equal("Moved Stop outside the planned level.", mistake.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConvertsMissingDescriptionToNull(string? description)
    {
        TradingMistake mistake = CreateMistake(description: description);

        Assert.Null(mistake.Description);
    }

    [Fact]
    public void RejectsDescriptionLongerThanMaximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateMistake(description: new string('D', 2001)));
    }

    [Fact]
    public void AcceptsUtcCreationTimestamp()
    {
        TradingMistake mistake = CreateMistake(createdAtUtc: CreatedAtUtc);

        Assert.Equal(CreatedAtUtc, mistake.CreatedAtUtc);
    }

    [Fact]
    public void RejectsNonUtcCreationTimestamp()
    {
        var nonUtcTimestamp = new DateTimeOffset(
            2026,
            6,
            1,
            12,
            0,
            0,
            TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(
            () => CreateMistake(createdAtUtc: nonUtcTimestamp));
    }

    [Fact]
    public void DeactivateChangesStateAndAdvancesTimestamp()
    {
        TradingMistake mistake = CreateMistake();
        DateTimeOffset deactivatedAtUtc = CreatedAtUtc.AddMinutes(1);

        mistake.Deactivate(deactivatedAtUtc);

        Assert.False(mistake.IsActive);
        Assert.Equal(deactivatedAtUtc, mistake.UpdatedAtUtc);
    }

    [Fact]
    public void RepeatedDeactivateIsNoOp()
    {
        TradingMistake mistake = CreateMistake();
        DateTimeOffset firstUpdatedAtUtc = CreatedAtUtc.AddMinutes(1);
        mistake.Deactivate(firstUpdatedAtUtc);

        mistake.Deactivate(CreatedAtUtc.AddMinutes(2));

        Assert.False(mistake.IsActive);
        Assert.Equal(firstUpdatedAtUtc, mistake.UpdatedAtUtc);
    }

    [Fact]
    public void ActivateRestoresStateAndAdvancesTimestamp()
    {
        TradingMistake mistake = CreateMistake();
        mistake.Deactivate(CreatedAtUtc.AddMinutes(1));
        DateTimeOffset activatedAtUtc = CreatedAtUtc.AddMinutes(2);

        mistake.Activate(activatedAtUtc);

        Assert.True(mistake.IsActive);
        Assert.Equal(activatedAtUtc, mistake.UpdatedAtUtc);
    }

    [Fact]
    public void RepeatedActivateIsNoOp()
    {
        TradingMistake mistake = CreateMistake();

        mistake.Activate(CreatedAtUtc.AddMinutes(1));

        Assert.True(mistake.IsActive);
        Assert.Equal(CreatedAtUtc, mistake.UpdatedAtUtc);
    }

    [Fact]
    public void RejectsBackwardsLifecycleTimestampWithoutChangingState()
    {
        TradingMistake mistake = CreateMistake();
        DateTimeOffset deactivatedAtUtc = CreatedAtUtc.AddMinutes(2);
        mistake.Deactivate(deactivatedAtUtc);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => mistake.Activate(CreatedAtUtc.AddMinutes(1)));
        Assert.False(mistake.IsActive);
        Assert.Equal(deactivatedAtUtc, mistake.UpdatedAtUtc);
    }

    [Fact]
    public void RejectsNonUtcLifecycleTimestampWithoutChangingState()
    {
        TradingMistake mistake = CreateMistake();
        var nonUtcTimestamp = new DateTimeOffset(
            2026,
            6,
            1,
            13,
            0,
            0,
            TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(() => mistake.Deactivate(nonUtcTimestamp));
        Assert.True(mistake.IsActive);
        Assert.Equal(CreatedAtUtc, mistake.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RehydratesExistingTradingMistake(bool isActive)
    {
        DateTimeOffset updatedAtUtc = CreatedAtUtc.AddDays(1);

        TradingMistake mistake = TradingMistake.Rehydrate(
            ExistingId,
            "Moved Stop",
            "Moved the stop beyond the planned invalidation level.",
            isActive,
            CreatedAtUtc,
            updatedAtUtc);

        Assert.Equal(ExistingId, mistake.Id);
        Assert.Equal("Moved Stop", mistake.Name);
        Assert.Equal(
            "Moved the stop beyond the planned invalidation level.",
            mistake.Description);
        Assert.Equal(isActive, mistake.IsActive);
        Assert.Equal(CreatedAtUtc, mistake.CreatedAtUtc);
        Assert.Equal(updatedAtUtc, mistake.UpdatedAtUtc);
    }

    [Fact]
    public void RehydrationNormalizesText()
    {
        TradingMistake mistake = RehydrateMistake(
            name: "  No Confirmation  ",
            description: "  Entered before planned Confirmation.  ");

        Assert.Equal("No Confirmation", mistake.Name);
        Assert.Equal("Entered before planned Confirmation.", mistake.Description);
    }

    [Fact]
    public void RehydrationConvertsBlankDescriptionToNull()
    {
        TradingMistake mistake = RehydrateMistake(description: "   ");

        Assert.Null(mistake.Description);
    }

    [Fact]
    public void RehydrationRejectsEmptyIdentifier()
    {
        Assert.Throws<ArgumentException>(
            () => RehydrateMistake(id: Guid.Empty));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RehydrationRejectsInvalidName(string? name)
    {
        Assert.Throws<ArgumentException>(
            () => RehydrateMistake(name: name!));
    }

    [Fact]
    public void RehydrationRejectsInvalidDescription()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RehydrateMistake(description: new string('D', 2001)));
    }

    [Fact]
    public void RehydrationRejectsInvalidAuditTimestamps()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RehydrateMistake(updatedAtUtc: CreatedAtUtc.AddTicks(-1)));

        var nonUtcTimestamp = new DateTimeOffset(
            2026,
            6,
            1,
            12,
            0,
            0,
            TimeSpan.FromHours(2));
        Assert.Throws<ArgumentException>(
            () => RehydrateMistake(updatedAtUtc: nonUtcTimestamp));
    }

    private static TradingMistake CreateMistake(
        string name = "Early Entry",
        string? description = "Entered before the planned confirmation.",
        DateTimeOffset? createdAtUtc = null)
    {
        return new TradingMistake(
            name,
            description,
            createdAtUtc ?? CreatedAtUtc);
    }

    private static TradingMistake RehydrateMistake(
        Guid? id = null,
        string name = "Early Entry",
        string? description = "Entered before the planned confirmation.",
        bool isActive = true,
        DateTimeOffset? createdAtUtc = null,
        DateTimeOffset? updatedAtUtc = null)
    {
        return TradingMistake.Rehydrate(
            id ?? ExistingId,
            name,
            description,
            isActive,
            createdAtUtc ?? CreatedAtUtc,
            updatedAtUtc ?? CreatedAtUtc.AddMinutes(1));
    }
}
