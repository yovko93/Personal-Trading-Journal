using PersonalTradingJournal.Domain.Instruments;

namespace PersonalTradingJournal.Domain.Tests.Instruments;

public sealed class InstrumentTests
{
    private static readonly Guid ExistingId =
        new("d214cd94-8f45-4249-ab79-b74404536547");

    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 2, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreatesValidFuturesInstrument()
    {
        Instrument instrument = CreateInstrument();

        Assert.NotEqual(Guid.Empty, instrument.Id);
        Assert.Equal("NQ", instrument.Symbol);
        Assert.Equal("E-mini Nasdaq-100", instrument.DisplayName);
        Assert.Equal(AssetClass.Futures, instrument.AssetClass);
        Assert.Equal("CME", instrument.Exchange);
        Assert.Equal("USD", instrument.Currency);
        Assert.Equal(0.25m, instrument.TickSize);
        Assert.Equal(5.00m, instrument.TickValue);
        Assert.True(instrument.IsActive);
        Assert.Equal(CreatedAtUtc, instrument.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, instrument.UpdatedAtUtc);
    }

    [Fact]
    public void CanonicalizesTextProperties()
    {
        Instrument instrument = CreateInstrument(
            symbol: "  nq  ",
            displayName: "  E-mini Nasdaq-100  ",
            exchange: "  CME  ",
            currency: "  usd  ");

        Assert.Equal("NQ", instrument.Symbol);
        Assert.Equal("E-mini Nasdaq-100", instrument.DisplayName);
        Assert.Equal("CME", instrument.Exchange);
        Assert.Equal("USD", instrument.Currency);
    }

    [Fact]
    public void ConvertsWhitespaceOnlyExchangeToNull()
    {
        Instrument instrument = CreateInstrument(exchange: "   ");

        Assert.Null(instrument.Exchange);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsMissingSymbol(string? symbol)
    {
        Assert.Throws<ArgumentException>(() => CreateInstrument(symbol: symbol!));
    }

    [Fact]
    public void RejectsSymbolLongerThanMaximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateInstrument(symbol: new string('S', 33)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsMissingDisplayName(string? displayName)
    {
        Assert.Throws<ArgumentException>(() => CreateInstrument(displayName: displayName!));
    }

    [Fact]
    public void RejectsDisplayNameLongerThanMaximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateInstrument(displayName: new string('D', 129)));
    }

    [Fact]
    public void RejectsExchangeLongerThanMaximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateInstrument(exchange: new string('E', 65)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsMissingCurrency(string? currency)
    {
        Assert.Throws<ArgumentException>(() => CreateInstrument(currency: currency!));
    }

    [Fact]
    public void RejectsCurrencyLongerThanMaximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateInstrument(currency: new string('C', 9)));
    }

    [Fact]
    public void RejectsNonPositiveTickSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateInstrument(tickSize: 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateInstrument(tickSize: -0.25m));
    }

    [Fact]
    public void RejectsNonPositiveTickValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateInstrument(tickValue: 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateInstrument(tickValue: -1m));
    }

    [Fact]
    public void RejectsUndefinedAssetClass()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateInstrument(assetClass: (AssetClass)999));
    }

    [Fact]
    public void RejectsNonUtcCreationTimestamp()
    {
        var nonUtcTimestamp = new DateTimeOffset(
            2026,
            2,
            1,
            12,
            0,
            0,
            TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(
            () => CreateInstrument(createdAtUtc: nonUtcTimestamp));
    }

    [Fact]
    public void CalculatesNqPointValue()
    {
        Instrument instrument = CreateInstrument(symbol: "NQ", tickValue: 5.00m);

        Assert.Equal(20m, instrument.PointValue);
    }

    [Fact]
    public void CalculatesMnqPointValue()
    {
        Instrument instrument = CreateInstrument(symbol: "MNQ", tickValue: 0.50m);

        Assert.Equal(2m, instrument.PointValue);
    }

    [Fact]
    public void CalculatesEsPointValue()
    {
        Instrument instrument = CreateInstrument(symbol: "ES", tickValue: 12.50m);

        Assert.Equal(50m, instrument.PointValue);
    }

    [Fact]
    public void CalculatesMesPointValue()
    {
        Instrument instrument = CreateInstrument(symbol: "MES", tickValue: 1.25m);

        Assert.Equal(5m, instrument.PointValue);
    }

    [Fact]
    public void DeactivateChangesStateAndAdvancesTimestamp()
    {
        Instrument instrument = CreateInstrument();
        DateTimeOffset updatedAtUtc = CreatedAtUtc.AddMinutes(1);

        instrument.Deactivate(updatedAtUtc);

        Assert.False(instrument.IsActive);
        Assert.Equal(updatedAtUtc, instrument.UpdatedAtUtc);
    }

    [Fact]
    public void RepeatedDeactivateDoesNotChangeTimestamp()
    {
        Instrument instrument = CreateInstrument();
        DateTimeOffset firstUpdatedAtUtc = CreatedAtUtc.AddMinutes(1);
        instrument.Deactivate(firstUpdatedAtUtc);

        instrument.Deactivate(CreatedAtUtc.AddMinutes(2));

        Assert.False(instrument.IsActive);
        Assert.Equal(firstUpdatedAtUtc, instrument.UpdatedAtUtc);
    }

    [Fact]
    public void ActivateChangesStateAndAdvancesTimestamp()
    {
        Instrument instrument = CreateInstrument();
        instrument.Deactivate(CreatedAtUtc.AddMinutes(1));
        DateTimeOffset activatedAtUtc = CreatedAtUtc.AddMinutes(2);

        instrument.Activate(activatedAtUtc);

        Assert.True(instrument.IsActive);
        Assert.Equal(activatedAtUtc, instrument.UpdatedAtUtc);
    }

    [Fact]
    public void LifecycleTimestampCannotMoveBackwards()
    {
        Instrument instrument = CreateInstrument();
        DateTimeOffset deactivatedAtUtc = CreatedAtUtc.AddMinutes(2);
        instrument.Deactivate(deactivatedAtUtc);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => instrument.Activate(CreatedAtUtc.AddMinutes(1)));
        Assert.False(instrument.IsActive);
        Assert.Equal(deactivatedAtUtc, instrument.UpdatedAtUtc);
    }

    [Fact]
    public void LifecycleTimestampRequiresUtcOffset()
    {
        Instrument instrument = CreateInstrument();
        var nonUtcTimestamp = new DateTimeOffset(
            2026,
            2,
            1,
            13,
            0,
            0,
            TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(() => instrument.Deactivate(nonUtcTimestamp));
        Assert.True(instrument.IsActive);
        Assert.Equal(CreatedAtUtc, instrument.UpdatedAtUtc);
    }

    [Fact]
    public void RehydratesExistingInactiveInstrument()
    {
        DateTimeOffset updatedAtUtc = CreatedAtUtc.AddDays(1);

        Instrument instrument = Instrument.Rehydrate(
            ExistingId,
            "NQ",
            "E-mini Nasdaq-100",
            AssetClass.Futures,
            "CME",
            "USD",
            0.25m,
            5.00m,
            isActive: false,
            CreatedAtUtc,
            updatedAtUtc);

        Assert.Equal(ExistingId, instrument.Id);
        Assert.Equal(CreatedAtUtc, instrument.CreatedAtUtc);
        Assert.Equal(updatedAtUtc, instrument.UpdatedAtUtc);
        Assert.False(instrument.IsActive);
    }

    [Fact]
    public void RehydrationRejectsEmptyIdentifier()
    {
        Assert.Throws<ArgumentException>(() => Instrument.Rehydrate(
            Guid.Empty,
            "NQ",
            "E-mini Nasdaq-100",
            AssetClass.Futures,
            "CME",
            "USD",
            0.25m,
            5.00m,
            isActive: true,
            CreatedAtUtc,
            CreatedAtUtc));
    }

    [Fact]
    public void RehydrationRejectsInvalidDomainValues()
    {
        Assert.Throws<ArgumentException>(() => Instrument.Rehydrate(
            ExistingId,
            "   ",
            "E-mini Nasdaq-100",
            AssetClass.Futures,
            "CME",
            "USD",
            0.25m,
            5.00m,
            isActive: true,
            CreatedAtUtc,
            CreatedAtUtc));

        Assert.Throws<ArgumentOutOfRangeException>(() => Instrument.Rehydrate(
            ExistingId,
            "NQ",
            "E-mini Nasdaq-100",
            AssetClass.Futures,
            "CME",
            "USD",
            0.25m,
            0m,
            isActive: true,
            CreatedAtUtc,
            CreatedAtUtc));
    }

    [Fact]
    public void UpdateDetailsNormalizesValuesAndRecalculatesPointValue()
    {
        Instrument instrument = CreateInstrument();
        Guid id = instrument.Id;
        DateTimeOffset updatedAtUtc = CreatedAtUtc.AddDays(1);

        bool changed = instrument.UpdateDetails(
            " mnq ",
            " Micro Nasdaq-100 ",
            AssetClass.Futures,
            "   ",
            " eur ",
            0.25m,
            0.50m,
            updatedAtUtc);

        Assert.True(changed);
        Assert.Equal(id, instrument.Id);
        Assert.Equal("MNQ", instrument.Symbol);
        Assert.Equal("Micro Nasdaq-100", instrument.DisplayName);
        Assert.Null(instrument.Exchange);
        Assert.Equal("EUR", instrument.Currency);
        Assert.Equal(2m, instrument.PointValue);
        Assert.Equal(CreatedAtUtc, instrument.CreatedAtUtc);
        Assert.Equal(updatedAtUtc, instrument.UpdatedAtUtc);
        Assert.True(instrument.IsActive);
    }

    [Fact]
    public void UpdateDetailsAllowsAssetClassChangeWithoutChangingLifecycleState()
    {
        Instrument instrument = CreateInstrument();

        instrument.UpdateDetails(
            "NQ", "E-mini Nasdaq-100", AssetClass.Equity, "CME", "USD",
            0.01m, 1m, CreatedAtUtc.AddMinutes(1));

        Assert.Equal(AssetClass.Equity, instrument.AssetClass);
        Assert.True(instrument.IsActive);
    }

    [Fact]
    public void UpdateDetailsWithCanonicalSameValuesIsNoOp()
    {
        Instrument instrument = CreateInstrument();

        bool changed = instrument.UpdateDetails(
            " nq ", " E-mini Nasdaq-100 ", AssetClass.Futures,
            " CME ", " usd ", 0.25m, 5m, CreatedAtUtc.AddDays(1));

        Assert.False(changed);
        Assert.Equal(CreatedAtUtc, instrument.UpdatedAtUtc);
    }

    [Fact]
    public void UpdateDetailsRejectsInvalidTickEconomicsWithoutMutation()
    {
        Instrument instrument = CreateInstrument();

        Assert.Throws<ArgumentOutOfRangeException>(() => instrument.UpdateDetails(
            "MNQ", "Changed", AssetClass.Futures, null, "EUR",
            0m, 1m, CreatedAtUtc.AddDays(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => instrument.UpdateDetails(
            "MNQ", "Changed", AssetClass.Futures, null, "EUR",
            0.25m, -1m, CreatedAtUtc.AddDays(1)));

        Assert.Equal("NQ", instrument.Symbol);
        Assert.Equal(20m, instrument.PointValue);
        Assert.Equal(CreatedAtUtc, instrument.UpdatedAtUtc);
    }

    private static Instrument CreateInstrument(
        string symbol = "NQ",
        string displayName = "E-mini Nasdaq-100",
        AssetClass assetClass = AssetClass.Futures,
        string? exchange = "CME",
        string currency = "USD",
        decimal tickSize = 0.25m,
        decimal tickValue = 5.00m,
        DateTimeOffset? createdAtUtc = null)
    {
        return new Instrument(
            symbol,
            displayName,
            assetClass,
            exchange,
            currency,
            tickSize,
            tickValue,
            createdAtUtc ?? CreatedAtUtc);
    }
}
