using PersonalTradingJournal.Domain.Strategies;

namespace PersonalTradingJournal.Domain.Tests.Strategies;

public sealed class StrategyTests
{
    private static readonly Guid ExistingId =
        new("433584cf-82b3-461f-bf2b-7e689b96a3ad");

    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreatesValidStrategy()
    {
        Strategy strategy = CreateStrategy();

        Assert.NotEqual(Guid.Empty, strategy.Id);
        Assert.Equal("ICT 2022 Model", strategy.Name);
        Assert.Equal("A repeatable liquidity-based framework.", strategy.Description);
        Assert.True(strategy.IsActive);
        Assert.Equal(CreatedAtUtc, strategy.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, strategy.UpdatedAtUtc);
    }

    [Fact]
    public void TrimsNameAndPreservesCasing()
    {
        Strategy strategy = CreateStrategy(name: "  Silver BuLLet  ");

        Assert.Equal("Silver BuLLet", strategy.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsMissingName(string? name)
    {
        Assert.Throws<ArgumentException>(() => CreateStrategy(name: name!));
    }

    [Fact]
    public void RejectsNameLongerThanMaximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateStrategy(name: new string('N', 129)));
    }

    [Fact]
    public void PreservesValidDescription()
    {
        const string description = "Trades the opening range breakout.";

        Strategy strategy = CreateStrategy(description: description);

        Assert.Equal(description, strategy.Description);
    }

    [Fact]
    public void TrimsDescriptionAndPreservesCasing()
    {
        Strategy strategy = CreateStrategy(description: "  Trade PDH/PDL Sweeps  ");

        Assert.Equal("Trade PDH/PDL Sweeps", strategy.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConvertsMissingDescriptionToNull(string? description)
    {
        Strategy strategy = CreateStrategy(description: description);

        Assert.Null(strategy.Description);
    }

    [Fact]
    public void RejectsDescriptionLongerThanMaximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateStrategy(description: new string('D', 2001)));
    }

    [Fact]
    public void AcceptsUtcCreationTimestamp()
    {
        Strategy strategy = CreateStrategy(createdAtUtc: CreatedAtUtc);

        Assert.Equal(CreatedAtUtc, strategy.CreatedAtUtc);
    }

    [Fact]
    public void RejectsNonUtcCreationTimestamp()
    {
        var nonUtcTimestamp = new DateTimeOffset(
            2026,
            5,
            1,
            12,
            0,
            0,
            TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(
            () => CreateStrategy(createdAtUtc: nonUtcTimestamp));
    }

    [Fact]
    public void DeactivateChangesStateAndAdvancesTimestamp()
    {
        Strategy strategy = CreateStrategy();
        DateTimeOffset deactivatedAtUtc = CreatedAtUtc.AddMinutes(1);

        strategy.Deactivate(deactivatedAtUtc);

        Assert.False(strategy.IsActive);
        Assert.Equal(deactivatedAtUtc, strategy.UpdatedAtUtc);
    }

    [Fact]
    public void RepeatedDeactivateIsNoOp()
    {
        Strategy strategy = CreateStrategy();
        DateTimeOffset firstUpdatedAtUtc = CreatedAtUtc.AddMinutes(1);
        strategy.Deactivate(firstUpdatedAtUtc);

        strategy.Deactivate(CreatedAtUtc.AddMinutes(2));

        Assert.False(strategy.IsActive);
        Assert.Equal(firstUpdatedAtUtc, strategy.UpdatedAtUtc);
    }

    [Fact]
    public void ActivateRestoresStateAndAdvancesTimestamp()
    {
        Strategy strategy = CreateStrategy();
        strategy.Deactivate(CreatedAtUtc.AddMinutes(1));
        DateTimeOffset activatedAtUtc = CreatedAtUtc.AddMinutes(2);

        strategy.Activate(activatedAtUtc);

        Assert.True(strategy.IsActive);
        Assert.Equal(activatedAtUtc, strategy.UpdatedAtUtc);
    }

    [Fact]
    public void RepeatedActivateIsNoOp()
    {
        Strategy strategy = CreateStrategy();

        strategy.Activate(CreatedAtUtc.AddMinutes(1));

        Assert.True(strategy.IsActive);
        Assert.Equal(CreatedAtUtc, strategy.UpdatedAtUtc);
    }

    [Fact]
    public void RejectsBackwardsLifecycleTimestampWithoutChangingState()
    {
        Strategy strategy = CreateStrategy();
        DateTimeOffset deactivatedAtUtc = CreatedAtUtc.AddMinutes(2);
        strategy.Deactivate(deactivatedAtUtc);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => strategy.Activate(CreatedAtUtc.AddMinutes(1)));
        Assert.False(strategy.IsActive);
        Assert.Equal(deactivatedAtUtc, strategy.UpdatedAtUtc);
    }

    [Fact]
    public void RejectsNonUtcLifecycleTimestampWithoutChangingState()
    {
        Strategy strategy = CreateStrategy();
        var nonUtcTimestamp = new DateTimeOffset(
            2026,
            5,
            1,
            13,
            0,
            0,
            TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(() => strategy.Deactivate(nonUtcTimestamp));
        Assert.True(strategy.IsActive);
        Assert.Equal(CreatedAtUtc, strategy.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RehydratesExistingStrategy(bool isActive)
    {
        DateTimeOffset updatedAtUtc = CreatedAtUtc.AddDays(1);

        Strategy strategy = Strategy.Rehydrate(
            ExistingId,
            "Opening Range",
            "Trades a defined opening range.",
            isActive,
            CreatedAtUtc,
            updatedAtUtc);

        Assert.Equal(ExistingId, strategy.Id);
        Assert.Equal("Opening Range", strategy.Name);
        Assert.Equal("Trades a defined opening range.", strategy.Description);
        Assert.Equal(isActive, strategy.IsActive);
        Assert.Equal(CreatedAtUtc, strategy.CreatedAtUtc);
        Assert.Equal(updatedAtUtc, strategy.UpdatedAtUtc);
    }

    [Fact]
    public void RehydrationNormalizesText()
    {
        Strategy strategy = RehydrateStrategy(
            name: "  Mean Reversion  ",
            description: "  Trades moves back toward fair value.  ");

        Assert.Equal("Mean Reversion", strategy.Name);
        Assert.Equal("Trades moves back toward fair value.", strategy.Description);
    }

    [Fact]
    public void RehydrationRejectsEmptyIdentifier()
    {
        Assert.Throws<ArgumentException>(
            () => RehydrateStrategy(id: Guid.Empty));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RehydrationRejectsInvalidName(string? name)
    {
        Assert.Throws<ArgumentException>(
            () => RehydrateStrategy(name: name!));
    }

    [Fact]
    public void RehydrationRejectsInvalidDescription()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RehydrateStrategy(description: new string('D', 2001)));
    }

    [Fact]
    public void RehydrationRejectsInvalidAuditTimestamps()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RehydrateStrategy(updatedAtUtc: CreatedAtUtc.AddTicks(-1)));

        var nonUtcTimestamp = new DateTimeOffset(
            2026,
            5,
            1,
            12,
            0,
            0,
            TimeSpan.FromHours(2));
        Assert.Throws<ArgumentException>(
            () => RehydrateStrategy(updatedAtUtc: nonUtcTimestamp));
    }

    private static Strategy CreateStrategy(
        string name = "ICT 2022 Model",
        string? description = "A repeatable liquidity-based framework.",
        DateTimeOffset? createdAtUtc = null)
    {
        return new Strategy(
            name,
            description,
            createdAtUtc ?? CreatedAtUtc);
    }

    private static Strategy RehydrateStrategy(
        Guid? id = null,
        string name = "ICT 2022 Model",
        string? description = "A repeatable liquidity-based framework.",
        bool isActive = true,
        DateTimeOffset? createdAtUtc = null,
        DateTimeOffset? updatedAtUtc = null)
    {
        return Strategy.Rehydrate(
            id ?? ExistingId,
            name,
            description,
            isActive,
            createdAtUtc ?? CreatedAtUtc,
            updatedAtUtc ?? CreatedAtUtc.AddMinutes(1));
    }
}
