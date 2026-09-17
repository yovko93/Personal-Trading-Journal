using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Domain.Tests.Setups;

public sealed class TradingSetupTests
{
    private static readonly Guid ExistingId =
        new("30f4a1e7-2844-4ff9-8733-ea31ad3dd101");

    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 5, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreatesValidTradingSetup()
    {
        TradingSetup setup = CreateSetup();

        Assert.NotEqual(Guid.Empty, setup.Id);
        Assert.Equal("Liquidity Sweep + MSS + FVG", setup.Name);
        Assert.Equal("A repeatable liquidity reversal configuration.", setup.Description);
        Assert.True(setup.IsActive);
        Assert.Equal(CreatedAtUtc, setup.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, setup.UpdatedAtUtc);
    }

    [Fact]
    public void TrimsNameAndPreservesCasing()
    {
        TradingSetup setup = CreateSetup(name: "  OTE after Displacement  ");

        Assert.Equal("OTE after Displacement", setup.Name);
    }

    [Fact]
    public void AcceptsMeaningfulSymbolsAndSpacesInName()
    {
        TradingSetup setup = CreateSetup(name: "PDH/PDL Sweep + MSS Reversal");

        Assert.Equal("PDH/PDL Sweep + MSS Reversal", setup.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsMissingName(string? name)
    {
        Assert.Throws<ArgumentException>(() => CreateSetup(name: name!));
    }

    [Fact]
    public void RejectsNameLongerThanMaximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateSetup(name: new string('N', 129)));
    }

    [Fact]
    public void PreservesValidDescription()
    {
        const string description =
            "Sell-side liquidity sweep followed by bullish displacement.";

        TradingSetup setup = CreateSetup(description: description);

        Assert.Equal(description, setup.Description);
    }

    [Fact]
    public void TrimsDescriptionAndPreservesCasing()
    {
        TradingSetup setup = CreateSetup(
            description: "  MSS confirmation with FVG Entry  ");

        Assert.Equal("MSS confirmation with FVG Entry", setup.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConvertsMissingDescriptionToNull(string? description)
    {
        TradingSetup setup = CreateSetup(description: description);

        Assert.Null(setup.Description);
    }

    [Fact]
    public void RejectsDescriptionLongerThanMaximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateSetup(description: new string('D', 2001)));
    }

    [Fact]
    public void AcceptsUtcCreationTimestamp()
    {
        TradingSetup setup = CreateSetup(createdAtUtc: CreatedAtUtc);

        Assert.Equal(CreatedAtUtc, setup.CreatedAtUtc);
    }

    [Fact]
    public void RejectsNonUtcCreationTimestamp()
    {
        var nonUtcTimestamp = new DateTimeOffset(
            2026,
            5,
            15,
            12,
            0,
            0,
            TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(
            () => CreateSetup(createdAtUtc: nonUtcTimestamp));
    }

    [Fact]
    public void DeactivateChangesStateAndAdvancesTimestamp()
    {
        TradingSetup setup = CreateSetup();
        DateTimeOffset deactivatedAtUtc = CreatedAtUtc.AddMinutes(1);

        setup.Deactivate(deactivatedAtUtc);

        Assert.False(setup.IsActive);
        Assert.Equal(deactivatedAtUtc, setup.UpdatedAtUtc);
    }

    [Fact]
    public void RepeatedDeactivateIsNoOp()
    {
        TradingSetup setup = CreateSetup();
        DateTimeOffset firstUpdatedAtUtc = CreatedAtUtc.AddMinutes(1);
        setup.Deactivate(firstUpdatedAtUtc);

        setup.Deactivate(CreatedAtUtc.AddMinutes(2));

        Assert.False(setup.IsActive);
        Assert.Equal(firstUpdatedAtUtc, setup.UpdatedAtUtc);
    }

    [Fact]
    public void ActivateRestoresStateAndAdvancesTimestamp()
    {
        TradingSetup setup = CreateSetup();
        setup.Deactivate(CreatedAtUtc.AddMinutes(1));
        DateTimeOffset activatedAtUtc = CreatedAtUtc.AddMinutes(2);

        setup.Activate(activatedAtUtc);

        Assert.True(setup.IsActive);
        Assert.Equal(activatedAtUtc, setup.UpdatedAtUtc);
    }

    [Fact]
    public void RepeatedActivateIsNoOp()
    {
        TradingSetup setup = CreateSetup();

        setup.Activate(CreatedAtUtc.AddMinutes(1));

        Assert.True(setup.IsActive);
        Assert.Equal(CreatedAtUtc, setup.UpdatedAtUtc);
    }

    [Fact]
    public void RejectsBackwardsLifecycleTimestampWithoutChangingState()
    {
        TradingSetup setup = CreateSetup();
        DateTimeOffset deactivatedAtUtc = CreatedAtUtc.AddMinutes(2);
        setup.Deactivate(deactivatedAtUtc);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => setup.Activate(CreatedAtUtc.AddMinutes(1)));
        Assert.False(setup.IsActive);
        Assert.Equal(deactivatedAtUtc, setup.UpdatedAtUtc);
    }

    [Fact]
    public void RejectsNonUtcLifecycleTimestampWithoutChangingState()
    {
        TradingSetup setup = CreateSetup();
        var nonUtcTimestamp = new DateTimeOffset(
            2026,
            5,
            15,
            13,
            0,
            0,
            TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(() => setup.Deactivate(nonUtcTimestamp));
        Assert.True(setup.IsActive);
        Assert.Equal(CreatedAtUtc, setup.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RehydratesExistingTradingSetup(bool isActive)
    {
        DateTimeOffset updatedAtUtc = CreatedAtUtc.AddDays(1);

        TradingSetup setup = TradingSetup.Rehydrate(
            ExistingId,
            "Opening Range Breakout",
            "A break beyond a defined opening range.",
            isActive,
            CreatedAtUtc,
            updatedAtUtc);

        Assert.Equal(ExistingId, setup.Id);
        Assert.Equal("Opening Range Breakout", setup.Name);
        Assert.Equal("A break beyond a defined opening range.", setup.Description);
        Assert.Equal(isActive, setup.IsActive);
        Assert.Equal(CreatedAtUtc, setup.CreatedAtUtc);
        Assert.Equal(updatedAtUtc, setup.UpdatedAtUtc);
    }

    [Fact]
    public void RehydrationNormalizesText()
    {
        TradingSetup setup = RehydrateSetup(
            name: "  Failed Breakout Reversal  ",
            description: "  Reversal after a Failed Breakout.  ");

        Assert.Equal("Failed Breakout Reversal", setup.Name);
        Assert.Equal("Reversal after a Failed Breakout.", setup.Description);
    }

    [Fact]
    public void RehydrationConvertsBlankDescriptionToNull()
    {
        TradingSetup setup = RehydrateSetup(description: "   ");

        Assert.Null(setup.Description);
    }

    [Fact]
    public void RehydrationRejectsEmptyIdentifier()
    {
        Assert.Throws<ArgumentException>(
            () => RehydrateSetup(id: Guid.Empty));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RehydrationRejectsInvalidName(string? name)
    {
        Assert.Throws<ArgumentException>(
            () => RehydrateSetup(name: name!));
    }

    [Fact]
    public void RehydrationRejectsOverlongDescription()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RehydrateSetup(description: new string('D', 2001)));
    }

    [Fact]
    public void RehydrationRejectsInvalidAuditTimestamps()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RehydrateSetup(updatedAtUtc: CreatedAtUtc.AddTicks(-1)));

        var nonUtcTimestamp = new DateTimeOffset(
            2026,
            5,
            15,
            12,
            0,
            0,
            TimeSpan.FromHours(2));
        Assert.Throws<ArgumentException>(
            () => RehydrateSetup(updatedAtUtc: nonUtcTimestamp));
    }

    [Fact]
    public void UpdateDetailsNormalizesAndChangesOnlyEditableFields()
    {
        TradingSetup setup = CreateSetup();
        Guid originalId = setup.Id;
        DateTimeOffset updatedAtUtc = CreatedAtUtc.AddHours(1);

        bool changed = setup.UpdateDetails(
            "  NY Open Reversal  ",
            "  Liquidity sweep at the open.  ",
            updatedAtUtc);

        Assert.True(changed);
        Assert.Equal("NY Open Reversal", setup.Name);
        Assert.Equal("Liquidity sweep at the open.", setup.Description);
        Assert.Equal(originalId, setup.Id);
        Assert.Equal(CreatedAtUtc, setup.CreatedAtUtc);
        Assert.Equal(updatedAtUtc, setup.UpdatedAtUtc);
        Assert.True(setup.IsActive);
    }

    [Fact]
    public void UpdateDetailsClearsWhitespaceDescription()
    {
        TradingSetup setup = CreateSetup();

        Assert.True(setup.UpdateDetails(
            setup.Name,
            "   ",
            CreatedAtUtc.AddMinutes(1)));

        Assert.Null(setup.Description);
    }

    [Fact]
    public void UpdateDetailsCanonicalNoOpPreservesAuditTimestamp()
    {
        TradingSetup setup = CreateSetup();

        bool changed = setup.UpdateDetails(
            $"  {setup.Name}  ",
            $"  {setup.Description}  ",
            CreatedAtUtc.AddMinutes(1));

        Assert.False(changed);
        Assert.Equal(CreatedAtUtc, setup.UpdatedAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateDetailsRejectsBlankName(string name)
    {
        TradingSetup setup = CreateSetup();

        Assert.Throws<ArgumentException>(() =>
            setup.UpdateDetails(name, null, CreatedAtUtc.AddMinutes(1)));
    }

    [Fact]
    public void UpdateDetailsPreservesCreationLimits()
    {
        TradingSetup setup = CreateSetup();
        DateTimeOffset updatedAtUtc = CreatedAtUtc.AddMinutes(1);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            setup.UpdateDetails(new string('N', 129), null, updatedAtUtc));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            setup.UpdateDetails(setup.Name, new string('D', 2001), updatedAtUtc));
    }

    private static TradingSetup CreateSetup(
        string name = "Liquidity Sweep + MSS + FVG",
        string? description = "A repeatable liquidity reversal configuration.",
        DateTimeOffset? createdAtUtc = null)
    {
        return new TradingSetup(
            name,
            description,
            createdAtUtc ?? CreatedAtUtc);
    }

    private static TradingSetup RehydrateSetup(
        Guid? id = null,
        string name = "Liquidity Sweep + MSS + FVG",
        string? description = "A repeatable liquidity reversal configuration.",
        bool isActive = true,
        DateTimeOffset? createdAtUtc = null,
        DateTimeOffset? updatedAtUtc = null)
    {
        return TradingSetup.Rehydrate(
            id ?? ExistingId,
            name,
            description,
            isActive,
            createdAtUtc ?? CreatedAtUtc,
            updatedAtUtc ?? CreatedAtUtc.AddMinutes(1));
    }
}
