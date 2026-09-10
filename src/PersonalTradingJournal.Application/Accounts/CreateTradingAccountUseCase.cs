using PersonalTradingJournal.Domain.Accounts;

namespace PersonalTradingJournal.Application.Accounts;

public sealed class CreateTradingAccountUseCase
{
    private readonly ITradingAccountStore _accountStore;
    private readonly TimeProvider _timeProvider;

    public CreateTradingAccountUseCase(
        ITradingAccountStore accountStore,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(accountStore);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _accountStore = accountStore;
        _timeProvider = timeProvider;
    }

    public async Task<Guid> ExecuteAsync(
        CreateTradingAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        DateTimeOffset createdAtUtc = _timeProvider.GetUtcNow();
        var account = new TradingAccount(
            command.Name,
            command.AccountType,
            command.ProviderName,
            command.ExternalAccountId,
            command.Currency,
            command.StartingBalance,
            createdAtUtc);

        await _accountStore.AddAsync(account, cancellationToken);

        return account.Id;
    }
}
