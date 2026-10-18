using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Desktop.ViewModels.TradingPlans;

public sealed partial class SessionPreparationViewModel
{
    internal TradingPlan CurrentPlan => _plan;
    internal string EditIdentity => Fingerprint();
    public string PreservedReadyLabel => _plan.WorkingDraft is not null && _plan.LatestReadyRevision is { } ready
        ? $"Working Draft · Ready revision {ready.Id.Revision} remains preserved." : "";
    public bool IsHosted { get; internal set; }
    public bool ShowComponentContext => !IsHosted;
    private TaskCompletionSource _idle = CompletedIdle();
    public Task OperationCompletion => _idle.Task;
    public void CancelOperation() => _operation?.Cancel();
    private static TaskCompletionSource CompletedIdle()
    { var value = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); value.SetResult(); return value; }

    public async Task<TradingPlanWriteResult> MarkReadyAsync(CancellationToken ct = default)
    {
        if (!CanEdit) return new(TradingPlanWriteStatus.Invalid);
        if (!IsPreparationComplete || !TryBuildContent(out var content))
        {
            Message = ReadinessBlockers.FirstOrDefault()?.Message ?? "Save the complete Draft and explicitly confirm preparation first.";
            ValidationAttempt++; OnPropertyChanged(nameof(ValidationAttempt));
            return new(TradingPlanWriteStatus.Invalid);
        }
        return await TransitionAsync(new(TradingPlanWriteOperation.MarkReady, Key, content!, Token), ct);
    }

    public Task<TradingPlanWriteResult> StartDraftAsync(string? reviewedCurrency = null, CancellationToken ct = default) =>
        !IsReadOnly || IsBusy || _historical is not null || _disposed ? Task.FromResult(new TradingPlanWriteResult(TradingPlanWriteStatus.Invalid)) :
        TransitionAsync(new(TradingPlanWriteOperation.StartDraft, Key, _original, Token,
            _plan.LatestReadyRevision!.Id, expectedAccountCurrency: reviewedCurrency), ct);

    private async Task<TradingPlanWriteResult> TransitionAsync(TradingPlanWriteCommand command, CancellationToken ct)
    {
        using var operation = Begin(ct);
        try
        {
            var result = await Task.Run(() => _store.WriteAsync(command, operation.Token), operation.Token);
            if (result.Status == TradingPlanWriteStatus.Success)
            {
                var saved = result.Plan;
                bool same = saved is not null && saved.Key == Key && saved.Id == _plan.Id && saved.Version == _plan.Version + 1;
                if (command.Operation == TradingPlanWriteOperation.MarkReady)
                    same &= saved?.WorkingDraft is null && saved?.LatestReadyRevision is { } ready &&
                        ready.Id.Revision == (_plan.LatestReadyRevision?.Id.Revision ?? 0) + 1 &&
                        ready.SourceDraftVersion == _plan.Version && SameContent(ready.Content, command.Content);
                else
                    same &= saved?.WorkingDraft is { } draft && draft.DerivedFromRevision == command.SourceRevision &&
                        draft.Content.Confirmed == TradingPlanChecklist.None && saved.LatestReadyRevision?.Id == command.SourceRevision &&
                        SamePreparation(draft.Content, command.Content);
                if (same) return result; // Committed success wins over late cancellation.
                result = new(TradingPlanWriteStatus.StorageFailure);
            }
            ObserveWriteFailure(result.Status);
            Message = result.Status switch
            {
                TradingPlanWriteStatus.Conflict => "The Plan changed elsewhere. Reload only after preserving or discarding your entered changes.",
                TradingPlanWriteStatus.ReferenceChanged => "References changed. Review and explicitly refresh/relink the Draft before reconfirming. A Ready revision is never rewritten.",
                TradingPlanWriteStatus.ReferenceUnavailable => "A reference is inactive or unavailable. The preserved Ready revision and entered preparation are unchanged.",
                TradingPlanWriteStatus.Cancelled => "Operation cancelled. The current preparation is retained.",
                TradingPlanWriteStatus.Invalid => "Preparation is not Ready. Save the Draft, resolve the listed requirements and confirm the checklist.",
                _ => "The transition could not be saved and verified. Current preparation is retained. Reload to check the stored state before trying again."
            };
            return result;
        }
        catch (OperationCanceledException) { Message = "Operation cancelled. Current preparation is retained."; return new(TradingPlanWriteStatus.Cancelled); }
        catch (Exception) { ObserveWriteFailure(TradingPlanWriteStatus.StorageFailure); Message = "The transition could not be verified. Current preparation is retained."; return new(TradingPlanWriteStatus.StorageFailure); }
        finally { End(); }
    }
    internal static bool SameContent(TradingPlanContent first, TradingPlanContent second) =>
        System.Text.Json.JsonSerializer.Serialize(first) == System.Text.Json.JsonSerializer.Serialize(second);
    private static bool SamePreparation(TradingPlanContent first, TradingPlanContent second)
    {
        var copy = new TradingPlan(new(Guid.NewGuid(), new(2026, 1, 1)), first, DateTimeOffset.UnixEpoch);
        return !copy.EditDraft(copy.Version, second, copy.UpdatedAtUtc);
    }
}
