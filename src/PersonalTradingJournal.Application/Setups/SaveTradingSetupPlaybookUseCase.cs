using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Application.Setups;

public sealed record SaveTradingSetupPlaybookCommand(Guid TradingSetupId, int ExpectedRevision, PlaybookGuidance Guidance);

/// <summary>Explicit direct editing of current Setup guidance with optimistic concurrency.</summary>
public sealed class SaveTradingSetupPlaybookUseCase(
    ITradingSetupStore setups, ITradingSetupPlaybookStore playbooks, TimeProvider timeProvider)
{
    private readonly ITradingSetupStore _setups = setups ?? throw new ArgumentNullException(nameof(setups));
    private readonly ITradingSetupPlaybookStore _playbooks = playbooks ?? throw new ArgumentNullException(nameof(playbooks));
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public async Task<TradingSetupPlaybookRevision> ExecuteAsync(SaveTradingSetupPlaybookCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Guidance);
        if (command.TradingSetupId == Guid.Empty || command.ExpectedRevision < 0)
            throw new ArgumentException("A Setup identity and non-negative expected revision are required.", nameof(command));
        cancellationToken.ThrowIfCancellationRequested();
        TradingSetup setup = await _setups.GetByIdAsync(command.TradingSetupId, cancellationToken)
            ?? throw new KeyNotFoundException("The owning Setup is unavailable.");
        TradingSetupPlaybookRevision? current = await _playbooks.GetLatestAsync(setup.Id, cancellationToken);
        if ((current?.Revision ?? 0) != command.ExpectedRevision)
            throw new InvalidOperationException("The Playbook changed; reload before saving.");
        TradingSetupPlaybookRevision candidate = current is null
            ? TradingSetupPlaybookRevision.Create(setup, command.Guidance, _timeProvider.GetUtcNow())
            : current.Revise(setup, command.ExpectedRevision, command.Guidance, _timeProvider.GetUtcNow());
        cancellationToken.ThrowIfCancellationRequested();
        if (!await _playbooks.TrySaveCurrentAsync(candidate, command.ExpectedRevision, setup.UpdatedAtUtc, cancellationToken))
            throw new InvalidOperationException("The Setup or Playbook changed; reload before saving.");
        return candidate;
    }
}
