using PersonalTradingJournal.Domain.Accounts;

namespace PersonalTradingJournal.Domain.Tests.Accounts;

public sealed class TradingAccountTests
{
    private static readonly Guid ExistingId =
        new("40575458-070d-4226-9431-b964beeb911b");

    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(TradingAccountType.Personal)]
    [InlineData(TradingAccountType.PropEvaluation)]
    [InlineData(TradingAccountType.PropFunded)]
    public void CreatesSupportedTradingAccount(TradingAccountType accountType)
    {
        TradingAccount account = CreateAccount(accountType: accountType);

        Assert.Equal(accountType, account.AccountType);
        Assert.NotEqual(Guid.Empty, account.Id);
        Assert.True(account.IsActive);
        Assert.Equal(CreatedAtUtc, account.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, account.UpdatedAtUtc);
    }

    [Fact]
    public void AllowsNullStartingBalance()
    {
        TradingAccount account = CreateAccount(startingBalance: null);

        Assert.Null(account.StartingBalance);
    }

    [Fact]
    public void AllowsZeroStartingBalance()
    {
        TradingAccount account = CreateAccount(startingBalance: 0m);

        Assert.Equal(0m, account.StartingBalance);
    }

    [Fact]
    public void PreservesPositiveStartingBalance()
    {
        TradingAccount account = CreateAccount(startingBalance: 50_000.1234m);

        Assert.Equal(50_000.1234m, account.StartingBalance);
    }

    [Fact]
    public void NormalizesTextProperties()
    {
        TradingAccount account = CreateAccount(
            name: "  Topstep 50K Evaluation  ",
            providerName: "  Topstep  ",
            externalAccountId: "  Account-AbC-123  ",
            currency: "  usd  ");

        Assert.Equal("Topstep 50K Evaluation", account.Name);
        Assert.Equal("Topstep", account.ProviderName);
        Assert.Equal("Account-AbC-123", account.ExternalAccountId);
        Assert.Equal("USD", account.Currency);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConvertsMissingProviderNameToNull(string? providerName)
    {
        TradingAccount account = CreateAccount(providerName: providerName);

        Assert.Null(account.ProviderName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConvertsMissingExternalAccountIdToNull(string? externalAccountId)
    {
        TradingAccount account = CreateAccount(externalAccountId: externalAccountId);

        Assert.Null(account.ExternalAccountId);
    }

    [Fact]
    public void PreservesExternalAccountIdCasing()
    {
        TradingAccount account = CreateAccount(externalAccountId: "aBc-123-XyZ");

        Assert.Equal("aBc-123-XyZ", account.ExternalAccountId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsMissingName(string? name)
    {
        Assert.Throws<ArgumentException>(() => CreateAccount(name: name!));
    }

    [Fact]
    public void RejectsNameLongerThanMaximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateAccount(name: new string('N', 129)));
    }

    [Theory]
    [InlineData((TradingAccountType)0)]
    [InlineData((TradingAccountType)999)]
    public void RejectsUndefinedAccountType(TradingAccountType accountType)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateAccount(accountType: accountType));
    }

    [Fact]
    public void RejectsProviderNameLongerThanMaximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateAccount(providerName: new string('P', 129)));
    }

    [Fact]
    public void RejectsExternalAccountIdLongerThanMaximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateAccount(externalAccountId: new string('A', 129)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsMissingCurrency(string? currency)
    {
        Assert.Throws<ArgumentException>(() => CreateAccount(currency: currency!));
    }

    [Fact]
    public void RejectsCurrencyLongerThanMaximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateAccount(currency: new string('C', 9)));
    }

    [Fact]
    public void RejectsNegativeStartingBalance()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateAccount(startingBalance: -0.01m));
    }

    [Fact]
    public void RejectsNonUtcCreationTimestamp()
    {
        var nonUtcTimestamp = new DateTimeOffset(
            2026,
            3,
            1,
            12,
            0,
            0,
            TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(
            () => CreateAccount(createdAtUtc: nonUtcTimestamp));
    }

    [Fact]
    public void DeactivateChangesStateAndAdvancesTimestamp()
    {
        TradingAccount account = CreateAccount();
        DateTimeOffset updatedAtUtc = CreatedAtUtc.AddMinutes(1);

        account.Deactivate(updatedAtUtc);

        Assert.False(account.IsActive);
        Assert.Equal(updatedAtUtc, account.UpdatedAtUtc);
    }

    [Fact]
    public void RepeatedDeactivateDoesNotChangeTimestamp()
    {
        TradingAccount account = CreateAccount();
        DateTimeOffset firstUpdatedAtUtc = CreatedAtUtc.AddMinutes(1);
        account.Deactivate(firstUpdatedAtUtc);

        account.Deactivate(CreatedAtUtc.AddMinutes(2));

        Assert.False(account.IsActive);
        Assert.Equal(firstUpdatedAtUtc, account.UpdatedAtUtc);
    }

    [Fact]
    public void ActivateChangesStateAndAdvancesTimestamp()
    {
        TradingAccount account = CreateAccount();
        account.Deactivate(CreatedAtUtc.AddMinutes(1));
        DateTimeOffset activatedAtUtc = CreatedAtUtc.AddMinutes(2);

        account.Activate(activatedAtUtc);

        Assert.True(account.IsActive);
        Assert.Equal(activatedAtUtc, account.UpdatedAtUtc);
    }

    [Fact]
    public void RepeatedActivateDoesNotChangeTimestamp()
    {
        TradingAccount account = CreateAccount();

        account.Activate(CreatedAtUtc.AddMinutes(1));

        Assert.True(account.IsActive);
        Assert.Equal(CreatedAtUtc, account.UpdatedAtUtc);
    }

    [Fact]
    public void LifecycleTimestampCannotMoveBackwards()
    {
        TradingAccount account = CreateAccount();
        DateTimeOffset deactivatedAtUtc = CreatedAtUtc.AddMinutes(2);
        account.Deactivate(deactivatedAtUtc);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => account.Activate(CreatedAtUtc.AddMinutes(1)));
        Assert.False(account.IsActive);
        Assert.Equal(deactivatedAtUtc, account.UpdatedAtUtc);
    }

    [Fact]
    public void LifecycleTimestampRequiresUtcOffset()
    {
        TradingAccount account = CreateAccount();
        var nonUtcTimestamp = new DateTimeOffset(
            2026,
            3,
            1,
            13,
            0,
            0,
            TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(() => account.Deactivate(nonUtcTimestamp));
        Assert.True(account.IsActive);
        Assert.Equal(CreatedAtUtc, account.UpdatedAtUtc);
    }

    [Fact]
    public void RehydratesExistingInactiveAccount()
    {
        DateTimeOffset updatedAtUtc = CreatedAtUtc.AddDays(1);

        TradingAccount account = TradingAccount.Rehydrate(
            ExistingId,
            "Topstep Funded 1",
            TradingAccountType.PropFunded,
            "Topstep",
            "Ts-AbC-123",
            "USD",
            50_000m,
            isActive: false,
            CreatedAtUtc,
            updatedAtUtc);

        Assert.Equal(ExistingId, account.Id);
        Assert.Equal("Topstep Funded 1", account.Name);
        Assert.Equal(TradingAccountType.PropFunded, account.AccountType);
        Assert.Equal("Topstep", account.ProviderName);
        Assert.Equal("Ts-AbC-123", account.ExternalAccountId);
        Assert.Equal("USD", account.Currency);
        Assert.Equal(50_000m, account.StartingBalance);
        Assert.False(account.IsActive);
        Assert.Equal(CreatedAtUtc, account.CreatedAtUtc);
        Assert.Equal(updatedAtUtc, account.UpdatedAtUtc);
    }

    [Fact]
    public void RehydrationPreservesNullStartingBalance()
    {
        TradingAccount account = RehydrateAccount(startingBalance: null);

        Assert.Null(account.StartingBalance);
    }

    [Fact]
    public void RehydrationRejectsInvalidMetadata()
    {
        Assert.Throws<ArgumentException>(
            () => RehydrateAccount(name: "   "));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RehydrateAccount(startingBalance: -1m));
    }

    [Fact]
    public void RehydrationRejectsEmptyIdentifier()
    {
        Assert.Throws<ArgumentException>(
            () => RehydrateAccount(id: Guid.Empty));
    }

    [Fact]
    public void RehydrationRejectsInvalidTimestamps()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RehydrateAccount(updatedAtUtc: CreatedAtUtc.AddMinutes(-1)));

        var nonUtcTimestamp = new DateTimeOffset(
            2026,
            3,
            1,
            12,
            0,
            0,
            TimeSpan.FromHours(2));
        Assert.Throws<ArgumentException>(
            () => RehydrateAccount(updatedAtUtc: nonUtcTimestamp));
    }

    private static TradingAccount CreateAccount(
        string name = "Topstep 50K Evaluation",
        TradingAccountType accountType = TradingAccountType.PropEvaluation,
        string? providerName = "Topstep",
        string? externalAccountId = "TS-AbC-123",
        string currency = "USD",
        decimal? startingBalance = 50_000m,
        DateTimeOffset? createdAtUtc = null)
    {
        return new TradingAccount(
            name,
            accountType,
            providerName,
            externalAccountId,
            currency,
            startingBalance,
            createdAtUtc ?? CreatedAtUtc);
    }

    private static TradingAccount RehydrateAccount(
        Guid? id = null,
        string name = "Topstep Funded 1",
        TradingAccountType accountType = TradingAccountType.PropFunded,
        string? providerName = "Topstep",
        string? externalAccountId = "TS-AbC-123",
        string currency = "USD",
        decimal? startingBalance = 50_000m,
        bool isActive = false,
        DateTimeOffset? createdAtUtc = null,
        DateTimeOffset? updatedAtUtc = null)
    {
        return TradingAccount.Rehydrate(
            id ?? ExistingId,
            name,
            accountType,
            providerName,
            externalAccountId,
            currency,
            startingBalance,
            isActive,
            createdAtUtc ?? CreatedAtUtc,
            updatedAtUtc ?? CreatedAtUtc.AddMinutes(1));
    }
}
