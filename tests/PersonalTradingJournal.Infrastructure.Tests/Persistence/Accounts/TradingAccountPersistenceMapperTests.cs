using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Accounts;

public sealed class TradingAccountPersistenceMapperTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 3, 10, 8, 30, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset UpdatedAtUtc =
        new(2026, 3, 11, 9, 45, 0, TimeSpan.Zero);

    [Fact]
    public void ToRecordMapsPersistedAccountState()
    {
        var account = new TradingAccount(
            "Topstep 50K",
            TradingAccountType.PropEvaluation,
            "Topstep",
            "TS-ACC-001",
            "usd",
            50000m,
            CreatedAtUtc);
        account.Deactivate(UpdatedAtUtc);

        TradingAccountRecord record = TradingAccountPersistenceMapper.ToRecord(account);

        Assert.Equal(account.Id, record.Id);
        Assert.Equal("Topstep 50K", record.Name);
        Assert.Equal(TradingAccountType.PropEvaluation, record.AccountType);
        Assert.Equal("Topstep", record.ProviderName);
        Assert.Equal("TS-ACC-001", record.ExternalAccountId);
        Assert.Equal("USD", record.Currency);
        Assert.Equal(50000m, record.StartingBalance);
        Assert.False(record.IsActive);
        Assert.Equal(CreatedAtUtc, record.CreatedAtUtc);
        Assert.Equal(UpdatedAtUtc, record.UpdatedAtUtc);
    }

    [Fact]
    public void ToRecordPreservesNullAndZeroStartingBalances()
    {
        TradingAccountRecord nullBalance = TradingAccountPersistenceMapper.ToRecord(
            CreateAccount(null));
        TradingAccountRecord zeroBalance = TradingAccountPersistenceMapper.ToRecord(
            CreateAccount(0m));

        Assert.Null(nullBalance.StartingBalance);
        Assert.Equal(0m, zeroBalance.StartingBalance);
    }

    [Fact]
    public void ToRecordRejectsNullAccount()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TradingAccountPersistenceMapper.ToRecord(null!));
    }

    [Fact]
    public void ToDomainRehydratesPersistedAccountState()
    {
        TradingAccountRecord record = CreateValidRecord();
        record.IsActive = false;

        TradingAccount account = TradingAccountPersistenceMapper.ToDomain(record);

        Assert.Equal(record.Id, account.Id);
        Assert.Equal(record.Name, account.Name);
        Assert.Equal(record.AccountType, account.AccountType);
        Assert.Equal(record.ProviderName, account.ProviderName);
        Assert.Equal(record.ExternalAccountId, account.ExternalAccountId);
        Assert.Equal(record.Currency, account.Currency);
        Assert.Equal(record.StartingBalance, account.StartingBalance);
        Assert.False(account.IsActive);
        Assert.Equal(record.CreatedAtUtc, account.CreatedAtUtc);
        Assert.Equal(record.UpdatedAtUtc, account.UpdatedAtUtc);
    }

    [Fact]
    public void ToDomainPreservesNullAndZeroOptionalValues()
    {
        TradingAccountRecord nullRecord = CreateValidRecord();
        nullRecord.ProviderName = null;
        nullRecord.ExternalAccountId = null;
        nullRecord.StartingBalance = null;

        TradingAccount nullAccount = TradingAccountPersistenceMapper.ToDomain(nullRecord);

        Assert.Null(nullAccount.ProviderName);
        Assert.Null(nullAccount.ExternalAccountId);
        Assert.Null(nullAccount.StartingBalance);
        Assert.True(nullAccount.IsActive);

        TradingAccountRecord zeroRecord = CreateValidRecord();
        zeroRecord.StartingBalance = 0m;

        TradingAccount zeroAccount = TradingAccountPersistenceMapper.ToDomain(zeroRecord);

        Assert.Equal(0m, zeroAccount.StartingBalance);
    }

    [Fact]
    public void ToDomainRejectsNullRecord()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TradingAccountPersistenceMapper.ToDomain(null!));
    }

    [Fact]
    public void ToDomainRejectsPersistedDataThatViolatesDomainInvariants()
    {
        AssertInvalid(record => record.Id = Guid.Empty);
        AssertInvalid(record => record.AccountType = (TradingAccountType)999);
        AssertInvalid(record => record.Name = "   ");
        AssertInvalid(record => record.Currency = "   ");
        AssertInvalid(record => record.StartingBalance = -0.01m);
        AssertInvalid(record =>
            record.CreatedAtUtc = record.CreatedAtUtc.ToOffset(TimeSpan.FromHours(2)));
        AssertInvalid(record => record.UpdatedAtUtc = record.CreatedAtUtc.AddTicks(-1));
    }

    private static void AssertInvalid(Action<TradingAccountRecord> corrupt)
    {
        TradingAccountRecord record = CreateValidRecord();
        corrupt(record);

        Assert.ThrowsAny<ArgumentException>(() =>
            TradingAccountPersistenceMapper.ToDomain(record));
    }

    private static TradingAccount CreateAccount(decimal? startingBalance)
    {
        return new TradingAccount(
            "Personal",
            TradingAccountType.Personal,
            null,
            null,
            "USD",
            startingBalance,
            CreatedAtUtc);
    }

    private static TradingAccountRecord CreateValidRecord()
    {
        return new TradingAccountRecord
        {
            Id = Guid.Parse("728470b0-746c-48a8-870d-d620f755a866"),
            Name = "Topstep 50K",
            AccountType = TradingAccountType.PropEvaluation,
            ProviderName = "Topstep",
            ExternalAccountId = "TS-ACC-001",
            Currency = "USD",
            StartingBalance = 50000m,
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = UpdatedAtUtc,
        };
    }
}
