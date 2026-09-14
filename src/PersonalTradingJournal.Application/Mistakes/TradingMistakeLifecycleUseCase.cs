using PersonalTradingJournal.Domain.Mistakes;

namespace PersonalTradingJournal.Application.Mistakes;

public sealed class TradingMistakeLifecycleUseCase
{
    private readonly ITradingMistakeStore _store;
    private readonly TimeProvider _timeProvider;

    public TradingMistakeLifecycleUseCase(ITradingMistakeStore store, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(store); ArgumentNullException.ThrowIfNull(timeProvider);
        _store = store; _timeProvider = timeProvider;
    }

    public async Task ExecuteAsync(SetTradingMistakeActiveStateCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.TradingMistakeId == Guid.Empty)
        {
            throw new ArgumentException("A trading mistake identifier is required.", nameof(command));
        }

        TradingMistake? mistake = await _store.GetByIdAsync(command.TradingMistakeId, cancellationToken);
        if (mistake is null)
        {
            throw new KeyNotFoundException($"Trading mistake '{command.TradingMistakeId}' was not found.");
        }

        if (mistake.IsActive == command.IsActive) return;
        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (command.IsActive) mistake.Activate(now); else mistake.Deactivate(now);
        await _store.UpdateAsync(mistake, cancellationToken);
    }
}
