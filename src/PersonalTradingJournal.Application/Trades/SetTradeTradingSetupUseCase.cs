using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Trades;

public sealed class SetTradeTradingSetupUseCase
{
    private readonly ITradeMutationStore _tradeMutationStore;
    private readonly ITradingSetupStore _tradingSetupStore;
    private readonly TimeProvider _timeProvider;

    public SetTradeTradingSetupUseCase(
        ITradeMutationStore tradeMutationStore,
        ITradingSetupStore tradingSetupStore,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(tradeMutationStore);
        ArgumentNullException.ThrowIfNull(tradingSetupStore);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _tradeMutationStore = tradeMutationStore;
        _tradingSetupStore = tradingSetupStore;
        _timeProvider = timeProvider;
    }

    public async Task ExecuteAsync(
        SetTradeTradingSetupCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.TradeId == Guid.Empty)
        {
            throw new ArgumentException(
                "A trade identifier is required.",
                nameof(command.TradeId));
        }

        if (command.TradingSetupId == Guid.Empty)
        {
            throw new ArgumentException(
                "A trading setup identifier cannot be empty.",
                nameof(command.TradingSetupId));
        }

        Trade? trade = await _tradeMutationStore.GetByIdAsync(
            command.TradeId,
            cancellationToken);
        if (trade is null)
        {
            throw new KeyNotFoundException(
                $"Trade '{command.TradeId}' could not be found.");
        }

        if (command.TradingSetupId is Guid tradingSetupId)
        {
            TradingSetup? tradingSetup = await _tradingSetupStore.GetByIdAsync(
                tradingSetupId,
                cancellationToken);
            if (tradingSetup is null)
            {
                throw new KeyNotFoundException(
                    "The selected trading setup was not found.");
            }

            if (!tradingSetup.IsActive)
            {
                throw new InvalidOperationException(
                    "The selected trading setup is inactive.");
            }
        }

        if (trade.TradingSetupId == command.TradingSetupId)
        {
            return;
        }

        trade.SetTradingSetup(command.TradingSetupId, _timeProvider.GetUtcNow());
        await _tradeMutationStore.SaveAsync(trade, cancellationToken);
    }
}
