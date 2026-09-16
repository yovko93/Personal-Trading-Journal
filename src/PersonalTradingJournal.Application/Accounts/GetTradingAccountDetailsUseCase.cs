namespace PersonalTradingJournal.Application.Accounts;

public sealed class GetTradingAccountDetailsUseCase
{
    private readonly ITradingAccountReader _accountReader;

    public GetTradingAccountDetailsUseCase(ITradingAccountReader accountReader)
    {
        ArgumentNullException.ThrowIfNull(accountReader);
        _accountReader = accountReader;
    }

    public Task<TradingAccountDetails?> ExecuteAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException(
                "A trading account identifier is required.",
                nameof(accountId));
        }

        return _accountReader.GetByIdAsync(accountId, cancellationToken);
    }
}
