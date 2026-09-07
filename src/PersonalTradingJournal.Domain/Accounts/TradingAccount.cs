using PersonalTradingJournal.Domain.Common;

namespace PersonalTradingJournal.Domain.Accounts;

/// <summary>
/// Represents an account against which trades are journaled.
/// </summary>
public sealed class TradingAccount : AuditableEntity
{
    private const int MaximumNameLength = 128;
    private const int MaximumProviderNameLength = 128;
    private const int MaximumExternalAccountIdLength = 128;
    private const int MaximumCurrencyLength = 8;

    public TradingAccount(
        string name,
        TradingAccountType accountType,
        string? providerName,
        string? externalAccountId,
        string currency,
        decimal? startingBalance,
        DateTimeOffset createdAtUtc)
        : base(createdAtUtc)
    {
        Name = NormalizeRequired(name, MaximumNameLength, nameof(name), useUppercase: false);
        AccountType = ValidateAccountType(accountType);
        ProviderName = NormalizeOptional(
            providerName,
            MaximumProviderNameLength,
            nameof(providerName));
        ExternalAccountId = NormalizeOptional(
            externalAccountId,
            MaximumExternalAccountIdLength,
            nameof(externalAccountId));
        Currency = NormalizeRequired(
            currency,
            MaximumCurrencyLength,
            nameof(currency),
            useUppercase: true);
        StartingBalance = ValidateStartingBalance(startingBalance);
        IsActive = true;
    }

    private TradingAccount(
        Guid id,
        string name,
        TradingAccountType accountType,
        string? providerName,
        string? externalAccountId,
        string currency,
        decimal? startingBalance,
        bool isActive,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
        : base(id, createdAtUtc, updatedAtUtc)
    {
        Name = NormalizeRequired(name, MaximumNameLength, nameof(name), useUppercase: false);
        AccountType = ValidateAccountType(accountType);
        ProviderName = NormalizeOptional(
            providerName,
            MaximumProviderNameLength,
            nameof(providerName));
        ExternalAccountId = NormalizeOptional(
            externalAccountId,
            MaximumExternalAccountIdLength,
            nameof(externalAccountId));
        Currency = NormalizeRequired(
            currency,
            MaximumCurrencyLength,
            nameof(currency),
            useUppercase: true);
        StartingBalance = ValidateStartingBalance(startingBalance);
        IsActive = isActive;
    }

    public string Name { get; }

    public TradingAccountType AccountType { get; }

    public string? ProviderName { get; }

    public string? ExternalAccountId { get; }

    public string Currency { get; }

    /// <summary>
    /// Gets the optional baseline balance from which account analytics may begin.
    /// </summary>
    public decimal? StartingBalance { get; }

    public bool IsActive { get; private set; }

    public static TradingAccount Rehydrate(
        Guid id,
        string name,
        TradingAccountType accountType,
        string? providerName,
        string? externalAccountId,
        string currency,
        decimal? startingBalance,
        bool isActive,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        return new TradingAccount(
            id,
            name,
            accountType,
            providerName,
            externalAccountId,
            currency,
            startingBalance,
            isActive,
            createdAtUtc,
            updatedAtUtc);
    }

    public void Activate(DateTimeOffset updatedAtUtc)
    {
        if (IsActive)
        {
            return;
        }

        SetUpdatedAtUtc(updatedAtUtc);
        IsActive = true;
    }

    public void Deactivate(DateTimeOffset updatedAtUtc)
    {
        if (!IsActive)
        {
            return;
        }

        SetUpdatedAtUtc(updatedAtUtc);
        IsActive = false;
    }

    private static string NormalizeRequired(
        string value,
        int maximumLength,
        string parameterName,
        bool useUppercase)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        string normalized = value.Trim();
        if (useUppercase)
        {
            normalized = normalized.ToUpperInvariant();
        }

        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"The value cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(
        string? value,
        int maximumLength,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"The value cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }

    private static TradingAccountType ValidateAccountType(TradingAccountType accountType)
    {
        if (!Enum.IsDefined(accountType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(accountType),
                accountType,
                "The trading account type is not defined.");
        }

        return accountType;
    }

    private static decimal? ValidateStartingBalance(decimal? startingBalance)
    {
        if (startingBalance < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startingBalance),
                startingBalance,
                "The starting balance cannot be negative.");
        }

        return startingBalance;
    }
}
