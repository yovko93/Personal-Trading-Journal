namespace PersonalTradingJournal.Application.DailyReview.Coaching;

/// <summary>Closed vocabulary only. Unknown strings (including provider messages) never leave the boundary.</summary>
public static class CoachingSafeDiagnostics
{
    public static string Code(string? value) => value switch
    {
        "invalid_api_key" or "invalid_authentication" or "permission_denied" or "insufficient_permissions" or
        "model_not_found" or "model_not_available" or "context_length_exceeded" or "input_too_large" or
        "insufficient_quota" or "credit_balance_exhausted" or "organization_spend_limit_exceeded" or
        "project_spend_limit_exceeded" or "organization_usage_limit_exceeded" or "rate_limit_exceeded" or
        "invalid_json_schema" or "invalid_request" or "invalid_request_error" or "unsupported_parameter" or
        "invalid_value" or "server_error" or "server_is_overloaded" or "country_region_territory_not_supported" => value,
        _ => "unknown",
    };
    public static string Type(string? value) => value switch
    {
        "invalid_request_error" or "authentication_error" or "permission_error" or "permission_denied_error" or
        "rate_limit_error" or "insufficient_quota" or "server_error" or "service_unavailable_error" or "not_found_error" => value,
        _ => "unknown",
    };
    public static string Format(CoachingRequestMetadata? metadata)
    {
        if (metadata is null) return "No provider diagnostics available.";
        var phase = Enum.IsDefined(metadata.Phase) ? metadata.Phase.ToString() : "unknown";
        string client = Guid.TryParseExact(metadata.ClientRequestId, "N", out _) ? metadata.ClientRequestId : "unknown";
        string request = metadata.RequestId is { Length: > 4 and <= 128 } id && id.StartsWith("req_", StringComparison.Ordinal)
            && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-') ? id : "unknown";
        string model = metadata.Model == "gpt-4.1-mini-2025-04-14" ? metadata.Model : "unknown";
        return $"Phase: {phase} · HTTP: {(metadata.HttpStatus is >= 100 and <= 599 ? metadata.HttpStatus.ToString() : "not received")} · error.code: {Code(metadata.ErrorCode)} · error.type: {Type(metadata.ErrorType)} · Model: {model} · Client request: {client} · Server request: {request}";
    }
}
