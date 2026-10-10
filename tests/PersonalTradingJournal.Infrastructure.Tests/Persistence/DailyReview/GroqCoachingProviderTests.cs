using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Infrastructure.DailyReview.Coaching;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.DailyReview;

public sealed class GroqCoachingProviderTests
{
    private static CoachingEvidencePacket Packet() => CoachingEvidencePacketBuilder.Build(
        new DailyReviewEvidence(new(new DateOnly(2026, 10, 9)), [], [])).Packet!;
    private static string Review(CoachingEvidencePacket p, string source = "calculated:day", string? id = null) =>
        JsonSerializer.Serialize(new { contractVersion = p.ContractVersion, packetId = id ?? p.PacketId,
            daySummary = new { text = "No recorded activity.", sourceIds = new[] { source } },
            executionObservations = Array.Empty<object>(), behaviorObservations = Array.Empty<object>(),
            improvementSuggestions = Array.Empty<object>(), uncertainties = Array.Empty<object>() });
    private static JsonObject Envelope(CoachingEvidencePacket p) => JsonNode.Parse(JsonSerializer.Serialize(new {
        id = "chatcmpl-synthetic", model = GroqCoachingOptions.SupportedModel, @object = "chat.completion",
        choices = new[] { new { index = 0, finish_reason = "stop", message = new { role = "assistant", content = Review(p) } } },
        usage = new { prompt_tokens = 50, completion_tokens = 30, total_tokens = 80 }
    }))!.AsObject();
    private static HttpResponseMessage Response(int status, string body)
    {
        var result = new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body) };
        result.Headers.Add("x-request-id", "req_groq_test"); result.Headers.Add("Retry-After", "7");
        return result;
    }
    private static DailyCoachingGenerationService Service(HttpClient client, GroqCoachingOptions? options = null,
        Func<string?>? credential = null) => new(new GroqCoachingProvider(client, options ?? new(), credential ?? (() => "synthetic-groq")),
            new(), TimeProvider.System);

    [Fact]
    public async Task DedicatedStrictChatRequestPreservesCompleteEvidenceAndValidatedMetadata()
    {
        var packet = Packet();
        using var handler = new Handler(async (request, token) =>
        {
            Assert.Equal("https://api.groq.com/openai/v1/chat/completions", request.RequestUri!.ToString());
            Assert.True(request.Headers.Authorization?.Parameter == "synthetic-groq");
            var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(token))!;
            Assert.Equal(GroqCoachingOptions.SupportedModel, body["model"]!.GetValue<string>());
            Assert.Equal(packet.Json, body["messages"]![1]!["content"]!.GetValue<string>());
            Assert.Contains("untrusted source content", body["messages"]![0]!["content"]!.GetValue<string>());
            Assert.Equal(2, body["messages"]!.AsArray().Count);
            Assert.True(body["response_format"]!["json_schema"]!["strict"]!.GetValue<bool>());
            Assert.False(body["response_format"]!["json_schema"]!["schema"]!["additionalProperties"]!.GetValue<bool>());
            Assert.Equal(1000, body["max_completion_tokens"]!.GetValue<int>());
            Assert.Null(body["input"]); Assert.Null(body["text"]); Assert.Null(body["tools"]);
            Assert.False(body["stream"]!.GetValue<bool>()); Assert.Equal(1, body["n"]!.GetValue<int>());
            return Response(200, Envelope(packet).ToJsonString());
        });
        using var client = new HttpClient(handler);
        Assert.Equal(0, handler.Calls);
        var result = await Service(client).GenerateAsync(packet);
        Assert.Equal(CoachingGenerationStatus.Success, result.Status);
        Assert.Equal("Groq", result.Metadata!.Provider);
        Assert.Equal("chatcmpl-synthetic", result.Metadata.ResponseId);
        Assert.Equal(new CoachingTokenUsage(50, null, 30, 80), result.Metadata.Usage);
        Assert.Null(result.Metadata.MonetaryCost); Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(401, "invalid_api_key", CoachingGenerationStatus.AuthenticationFailed)]
    [InlineData(403, "model_permission_blocked", CoachingGenerationStatus.AccessDenied)]
    [InlineData(404, "model_not_found", CoachingGenerationStatus.ModelUnavailable)]
    [InlineData(429, "rate_limit_exceeded", CoachingGenerationStatus.RateLimited)]
    [InlineData(429, "insufficient_quota", CoachingGenerationStatus.QuotaExceeded)]
    [InlineData(402, "", CoachingGenerationStatus.QuotaExceeded)]
    [InlineData(400, "context_length_exceeded", CoachingGenerationStatus.InputTooLarge)]
    [InlineData(400, "json_validate_failed", CoachingGenerationStatus.InvalidRequest)]
    [InlineData(500, "", CoachingGenerationStatus.ServiceUnavailable)]
    [InlineData(404, "private-unknown-value", CoachingGenerationStatus.ProviderFailure)]
    public async Task FailuresAreSanitizedDistinctAndNotRetried(int status, string code, CoachingGenerationStatus expected)
    {
        using var handler = new Handler((_, _) => Task.FromResult(Response(status,
            JsonSerializer.Serialize(new { error = new { code, type = "invalid_request_error",
                message = "synthetic-groq private journal", failed_generation = "private output" } }))));
        using var client = new HttpClient(handler);
        var result = await Service(client).GenerateAsync(Packet());
        Assert.Equal(expected, result.Status); Assert.Null(result.Review); Assert.Equal(1, handler.Calls);
        Assert.Equal(TimeSpan.FromSeconds(7), result.Metadata!.RetryAfter);
        string safe = CoachingSafeDiagnostics.Format(result.Metadata) + result.Message;
        Assert.DoesNotContain("private", safe); Assert.DoesNotContain("synthetic-groq", safe);
        Assert.Contains(GroqCoachingOptions.SupportedModel, safe);
    }

    [Theory]
    [InlineData("citation", CoachingGenerationStatus.InvalidResponse)]
    [InlineData("identity", CoachingGenerationStatus.InvalidResponse)]
    [InlineData("length", CoachingGenerationStatus.IncompleteResponse)]
    [InlineData("refusal", CoachingGenerationStatus.Refused)]
    [InlineData("filter", CoachingGenerationStatus.Refused)]
    [InlineData("model", CoachingGenerationStatus.InvalidResponse)]
    [InlineData("choices", CoachingGenerationStatus.InvalidResponse)]
    [InlineData("malformed", CoachingGenerationStatus.InvalidResponse)]
    [InlineData("oversized", CoachingGenerationStatus.InvalidResponse)]
    public async Task NeverAcceptsIncompleteOrUnsupportedOutput(string failure, CoachingGenerationStatus expected)
    {
        var p = Packet(); var envelope = Envelope(p);
        switch (failure)
        {
            case "citation": envelope["choices"]![0]!["message"]!["content"] = Review(p, "not-supplied"); break;
            case "identity": envelope["choices"]![0]!["message"]!["content"] = Review(p, id: "wrong"); break;
            case "length": envelope["choices"]![0]!["finish_reason"] = "length"; break;
            case "filter": envelope["choices"]![0]!["finish_reason"] = "content_filter"; break;
            case "refusal": envelope["choices"]![0]!["message"]!["refusal"] = "private refusal"; break;
            case "model": envelope["model"] = "other"; break;
            case "choices": envelope["choices"] = new JsonArray(); break;
        }
        using var handler = new Handler((_, _) => Task.FromResult(Response(200, failure switch {
            "malformed" => "{", "oversized" => new string('x', 1_048_577), _ => envelope.ToJsonString() })));
        using var client = new HttpClient(handler);
        var result = await Service(client).GenerateAsync(p);
        Assert.Equal(expected, result.Status); Assert.Null(result.Review); Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task CredentialAndBudgetPreflightSendNothingAndCancellationReachesTransport()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new Handler(async (_, token) => { started.SetResult(); await Task.Delay(Timeout.Infinite, token); return Response(200, "{}"); });
        using var client = new HttpClient(handler);
        Assert.Equal(CoachingGenerationStatus.MissingCredentials, (await Service(client, credential: () => null).GenerateAsync(Packet())).Status);
        Assert.Equal(CoachingGenerationStatus.InputTooLarge, (await Service(client, new() { InputTokenBudget = 10 }).GenerateAsync(Packet())).Status);
        Assert.Equal(CoachingGenerationStatus.InvalidConfiguration, (await Service(client, new() { MaximumOutputTokens = 8000 }).GenerateAsync(Packet())).Status);
        Assert.Equal(0, handler.Calls);
        using var cancel = new CancellationTokenSource();
        var pending = Service(client).GenerateAsync(Packet(), cancel.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10)); cancel.Cancel();
        Assert.Equal(CoachingGenerationStatus.Cancelled, (await pending).Status); Assert.Equal(1, handler.Calls);
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        internal int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { Calls++; return send(request, token); }
    }
}
