using PersonalTradingJournal.Domain.Accounts;

namespace PersonalTradingJournal.Application.Accounts;

public sealed class TradingAccountLifecycleUseCase
{
    private readonly ITradingAccountStore _accountStore;
    private readonly TimeProvider _timeProvider;

    public TradingAccountLifecycleUseCase(
        ITradingAccountStore accountStore,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(accountStore);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _accountStore = accountStore;
        _timeProvider = timeProvider;
    }

    public async Task ActivateAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        TradingAccount account = await GetRequiredAccountAsync(
            accountId,
            cancellationToken);

        if (account.IsActive)
        {
            return;
        }

        account.Activate(_timeProvider.GetUtcNow());
        await _accountStore.UpdateAsync(account, cancellationToken);
    }

    public async Task DeactivateAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        TradingAccount account = await GetRequiredAccountAsync(
            accountId,
            cancellationToken);

        if (!account.IsActive)
        {
            return;
        }

        account.Deactivate(_timeProvider.GetUtcNow());
        await _accountStore.UpdateAsync(account, cancellationToken);
    }

    private async Task<TradingAccount> GetRequiredAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException(
                "A trading account identifier is required.",
                nameof(accountId));
        }

        TradingAccount? account = await _accountStore.GetByIdAsync(
            accountId,
            cancellationToken);

        return account ?? throw new KeyNotFoundException(
            $"Trading account '{accountId}' was not found.");
    }
}
