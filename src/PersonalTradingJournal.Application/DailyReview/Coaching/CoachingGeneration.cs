namespace PersonalTradingJournal.Application.DailyReview.Coaching;

public enum CoachingGenerationStatus
{
    Success, MissingCredentials, InvalidConfiguration, AuthenticationFailed, AccessDenied,
    InputTooLarge, RateLimited, QuotaExceeded, ServiceUnavailable, ProviderFailure,
    Refused, IncompleteResponse, InvalidResponse, Cancelled, TimedOut, ModelUnavailable, InvalidRequest,
}

public enum CoachingGenerationPhase { CredentialResolution, EvidencePreflight, HttpRequest, HttpResponse, ProviderResponseValidation, ResponseValidation, SnapshotValidation, AtomicSave, Saved }

public sealed record CoachingTokenUsage(long? InputTokens, long? CachedInputTokens,
    long? OutputTokens, long? TotalTokens);

/// <summary>Local estimate, not billed usage. Numeric diagnostics only; no source text or secrets.</summary>
public sealed record CoachingInputBudget(int InstructionTokens, int SchemaTokens, int EvidenceTokens,
    int FramingTokens, int SafetyMarginTokens, int ReserveTokens, int InputLimit, int OutputLimit)
{
    public int EstimatedInputTokens => InstructionTokens + SchemaTokens + EvidenceTokens + FramingTokens + SafetyMarginTokens + ReserveTokens;
}

/// <summary>Only allowlisted operational fields; never prompts, source text or error bodies.
/// Null usage/cost means unknown, not zero. No verified pricing configuration is installed in M15.4.</summary>
public sealed record CoachingRequestMetadata(string Provider, string Model, string ClientRequestId,
    string? RequestId = null, string? ResponseId = null, CoachingTokenUsage? Usage = null,
    int? HttpStatus = null, TimeSpan? RetryAfter = null,
    CoachingGenerationPhase Phase = CoachingGenerationPhase.CredentialResolution,
    string? ErrorCode = null, string? ErrorType = null, CoachingInputBudget? InputBudget = null)
{
    public decimal? MonetaryCost => null;
    public string CostStatus => "Unknown: no verified pricing configuration.";
}

/// <summary>Internal provider boundary. Success means transport completed, not validated coaching.
/// Consumers should invoke DailyCoachingGenerationService, never publish this raw response.</summary>
public sealed record CoachingProviderReply(CoachingGenerationStatus Status, string? ResponseJson,
    CoachingRequestMetadata? Metadata)
{
    public override string ToString() => $"CoachingProviderReply: {Status}";
}

public interface ICoachingProvider
{
    // Capture routing at explicit click time, before asynchronous evidence loading.
    ICoachingProvider Capture() => this;
    Task<CoachingProviderReply> GenerateAsync(CoachingEvidencePacket packet, CancellationToken cancellationToken);
}

public sealed record CoachingGenerationResult(CoachingGenerationStatus Status, CoachingResponse? Review,
    CoachingRequestMetadata? Metadata, string Message)
{
    public override string ToString() => $"CoachingGenerationResult: {Status}";
}

public sealed record CoachingGenerationOptions
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(90);
}

/// <summary>No startup, read, refresh or write hook. Each explicit invocation submits at most once.
/// Local cancellation cannot guarantee that an already accepted provider request was not billed.</summary>
public sealed class DailyCoachingGenerationService(ICoachingProvider provider,
    CoachingGenerationOptions options, TimeProvider timeProvider)
{
    public DailyCoachingGenerationService Capture() => new(provider.Capture(), options, timeProvider);
    public async Task<CoachingGenerationResult> GenerateAsync(CoachingEvidencePacket packet,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (cancellationToken.IsCancellationRequested) return Result(CoachingGenerationStatus.Cancelled);
        if (options.Timeout <= TimeSpan.Zero || options.Timeout > TimeSpan.FromMinutes(3))
            return Result(CoachingGenerationStatus.InvalidConfiguration);
        using var deadline = new CancellationTokenSource(options.Timeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        CoachingRequestMetadata? metadata = null;
        try
        {
            linked.Token.ThrowIfCancellationRequested();
            var pending = provider.GenerateAsync(packet, linked.Token);
            // Observe late faults even for a noncooperative adapter; never publish its late result.
            _ = pending.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            var reply = await pending.WaitAsync(linked.Token);
            metadata = reply.Metadata;
            linked.Token.ThrowIfCancellationRequested();
            if (reply.Status != CoachingGenerationStatus.Success) return Result(reply.Status, metadata);
            metadata = metadata is null ? null : metadata with { Phase = CoachingGenerationPhase.ResponseValidation };
            if (reply.ResponseJson is null) return Result(CoachingGenerationStatus.InvalidResponse, metadata);
            var validation = CoachingResponseValidator.Validate(reply.ResponseJson, packet, linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            return validation.IsValid
                ? new(CoachingGenerationStatus.Success, validation.Response, metadata, "Review generated and source references validated.")
                : Result(CoachingGenerationStatus.InvalidResponse, metadata);
        }
        catch (OperationCanceledException)
        {
            return Result(cancellationToken.IsCancellationRequested ? CoachingGenerationStatus.Cancelled :
                deadline.IsCancellationRequested ? CoachingGenerationStatus.TimedOut : CoachingGenerationStatus.ProviderFailure, metadata);
        }
        catch (Exception)
        {
            // This external-service boundary deliberately does not log/return exception messages:
            // a provider exception may embed credentials, request text or response bodies.
            return Result(CoachingGenerationStatus.ProviderFailure, metadata);
        }
    }

    private static CoachingGenerationResult Result(CoachingGenerationStatus status, CoachingRequestMetadata? metadata = null) =>
        new(status, null, metadata, status switch
        {
            CoachingGenerationStatus.MissingCredentials => "Configure AI in Settings. The selected provider's key is missing or its saved storage cannot be read.",
            CoachingGenerationStatus.InvalidConfiguration => "Check the supported model, input/output budget and positive timeout (at most 180 seconds).",
            CoachingGenerationStatus.AuthenticationFailed => "The provider rejected the credential. Check or replace the API key.",
            CoachingGenerationStatus.AccessDenied => "The selected provider denied this request. Check the safe error code and the account's API/model permissions. Unknown codes do not establish the cause.",
            CoachingGenerationStatus.ModelUnavailable => "The configured model is unavailable to this request. Ask the provider account owner to verify access to the model in the safe diagnostics; the response does not distinguish a missing model from denied access.",
            CoachingGenerationStatus.InvalidRequest => "The provider rejected the request or structured-output schema. Report the safe diagnostics; changing billing or retrying is not a confirmed remedy.",
            CoachingGenerationStatus.InputTooLarge when metadata?.InputBudget is { } budget && metadata.Phase == CoachingGenerationPhase.EvidencePreflight =>
                $"Complete evidence exceeds the configured input budget: estimated {budget.EstimatedInputTokens:N0} input tokens, limit {budget.InputLimit:N0} (including instructions, schema and safety reserve). No request was sent; nothing was truncated. This provider profile cannot fit the complete day. Configure a supported higher-budget provider in Settings and explicitly generate again if appropriate; there is no automatic fallback.",
            CoachingGenerationStatus.InputTooLarge => "Complete evidence exceeds the configured input budget. Nothing was truncated. Check the selected provider's supported limits; a complete day may require a higher-budget provider selected explicitly in Settings.",
            CoachingGenerationStatus.RateLimited => "The provider rate-limited this request. Respect RetryAfter before manually generating again.",
            CoachingGenerationStatus.QuotaExceeded => "Check the provider's billing balance and project/organization limits before generating again.",
            CoachingGenerationStatus.ServiceUnavailable => "Check the network or provider availability before manually generating again.",
            CoachingGenerationStatus.Refused => "The provider declined this request. No coaching review was accepted.",
            CoachingGenerationStatus.IncompleteResponse => "The provider did not finish the review. No partial review was accepted.",
            CoachingGenerationStatus.InvalidResponse => "The response failed structure, packet identity or citation validation. No review was accepted.",
            CoachingGenerationStatus.Cancelled => "Generation cancelled locally. An accepted provider request may still incur a charge; it was not retried.",
            CoachingGenerationStatus.TimedOut => "Generation timed out. The provider may still have processed the request; it was not retried.",
            _ => "Generation failed. Check provider configuration/availability before manually trying again; no automatic retry was made.",
        });
}
