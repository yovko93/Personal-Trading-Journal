namespace PersonalTradingJournal.Application.Accounts;

public sealed class DeleteTradingAccountUseCase
{
    private readonly ITradingAccountDeletionStore _deletionStore;
    private readonly ITradingAccountStore _accountStore;

    public DeleteTradingAccountUseCase(
        ITradingAccountStore accountStore,
        ITradingAccountDeletionStore deletionStore)
    {
        ArgumentNullException.ThrowIfNull(accountStore);
        ArgumentNullException.ThrowIfNull(deletionStore);
        _accountStore = accountStore;
        _deletionStore = deletionStore;
    }

    public async Task<DeleteTradingAccountResult> ExecuteAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException(
                "A trading account identifier is required.",
                nameof(accountId));
        }

        if (await _accountStore.GetByIdAsync(accountId, cancellationToken) is null)
        {
            throw new KeyNotFoundException(
                $"Trading account '{accountId}' was not found.");
        }

        if (await _deletionStore.HasTradesAsync(accountId, cancellationToken))
        {
            return DeleteTradingAccountResult.Referenced;
        }

        try
        {
            await _deletionStore.DeleteAsync(accountId, cancellationToken);
            return DeleteTradingAccountResult.Deleted;
        }
        catch (TradingAccountDeleteBlockedException)
        {
            return DeleteTradingAccountResult.Referenced;
        }
    }
}
