using PersonalTradingJournal.Domain.Accounts;

namespace PersonalTradingJournal.Application.Accounts;

public sealed class UpdateTradingAccountUseCase
{
    private readonly ITradingAccountStore _accountStore;
    private readonly TimeProvider _timeProvider;

    public UpdateTradingAccountUseCase(
        ITradingAccountStore accountStore,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(accountStore);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _accountStore = accountStore;
        _timeProvider = timeProvider;
    }

    public async Task<UpdateTradingAccountResult> ExecuteAsync(
        UpdateTradingAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.AccountId == Guid.Empty)
        {
            throw new ArgumentException(
                "A trading account identifier is required.",
                nameof(command));
        }

        TradingAccount? account = await _accountStore.GetByIdAsync(
            command.AccountId,
            cancellationToken);
        if (account is null)
        {
            throw new KeyNotFoundException(
                $"Trading account '{command.AccountId}' was not found.");
        }

        bool wasChanged = account.UpdateDetails(
            command.Name,
            command.AccountType,
            command.ProviderName,
            command.ExternalAccountId,
            command.Currency,
            command.StartingBalance,
            _timeProvider.GetUtcNow());

        if (wasChanged)
        {
            await _accountStore.UpdateAsync(account, cancellationToken);
        }

        return new UpdateTradingAccountResult(
            TradingAccountDetails.FromDomain(account),
            wasChanged);
    }
}
