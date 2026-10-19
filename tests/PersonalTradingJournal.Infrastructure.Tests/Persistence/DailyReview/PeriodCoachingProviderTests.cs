using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Application.PeriodReviews;
using PersonalTradingJournal.Application.PeriodReviews.Coaching;
using PersonalTradingJournal.Infrastructure.DailyReview.Coaching;
using Xunit.Abstractions;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.DailyReview;

public sealed class PeriodCoachingProviderTests(ITestOutputHelper output)
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task TwentyFiveTradeBudgetMeasurement(bool previousPopulated)
    {
        var pair = TwentyFiveTrades();
        if (previousPopulated)
        {
            var prior = pair.Current.ClosedTrades.Select((t, i) => t with {
                TradeId = new Guid(i + 2000, 0, 0, new byte[8]),
                Facts = t.Facts! with { OpenedAtUtc = t.Facts.OpenedAtUtc.AddDays(-7), ClosedAtUtc = t.Facts.ClosedAtUtc!.Value.AddDays(-7) },
                Executions = t.Executions.Select(e => e with { Id = new Guid(i + 3000, 0, 0, new byte[8]), ExecutedAtUtc = e.ExecutedAtUtc.AddDays(-7) }).ToArray() }).ToArray();
            pair = pair with { Previous = pair.Previous with { ClosedTrades = prior } };
        }
        var packet = Packet(pair);
        var handler = new Handler((_, _) => Task.FromResult(Response(200, Envelope(true, Review(packet)).ToJsonString())));
        using var client = new HttpClient(handler);
        var result = await Service(pair, Provider(true, client)).GenerateAsync(pair.Current.Query);
        output.WriteLine($"25 Trades, previous populated={previousPopulated}: local bytes={packet.Utf8ByteCount}; provider bytes={Encoding.UTF8.GetByteCount(packet.ProviderJson)}; status={result.Status}; requests={handler.Calls}");
        foreach (var group in packet.Sources.GroupBy(s => s.Kind))
            output.WriteLine($"{group.Key}: count={group.Count()}, bytes={group.Sum(s => Encoding.UTF8.GetByteCount(s.Content.GetRawText()))}");
        LogBudget(result.Metadata!.InputBudget!);
        Assert.Equal(PeriodCoachingStatus.Success, result.Status);
        Assert.Equal(1, handler.Calls);
        Assert.Equal(previousPopulated ? 50 : 25, packet.Sources.Count(s => s.Kind == PeriodCoachingSourceKind.Trade));
        Assert.Equal(previousPopulated ? 4 : 2, packet.SuppliedSources.Count(s => s.Kind == PeriodCoachingSourceKind.Trade));
    }

    internal static PeriodComparisonEvidence TwentyFiveTrades()
    {
        var pair = Evidence(); var q = pair.Current.Query;
        var trades = Enumerable.Range(1, 25).Select(i =>
        {
            var close = q.FromUtc.AddDays(i % 5).AddHours(12);
            return new DailyReviewTradeEvidence(new Guid(i, 0, 0, new byte[8]), new(new Guid(100, 0, 0, new byte[8]), "Synthetic", true),
                new(new Guid(200, 0, 0, new byte[8]), "NQ", true), null, "USD", 1, close, close,
                DailyReviewTradeInclusion.ClosedOnDate,
                new(1, Domain.Trades.TradeStatus.Closed, Domain.Trades.TradeDirection.Long, close.AddHours(-1), close, 0, 100, 101, 0, 1, 1),
                [new Application.Trades.TradeExecutionDetailItem(new Guid(i + 1000, 0, 0, new byte[8]), 1, close,
                    Domain.Trades.ExecutionSide.Sell, 1, 101, 0, 0, 0, null, null, null)], [], DailyReviewTradeQuality.None);
        }).ToArray();
        return pair with { Current = pair.Current with { ClosedTrades = trades, Journals = [] } };
    }
    [Theory]
    [InlineData(false, ReviewPeriodKind.Week)] [InlineData(true, ReviewPeriodKind.Week)]
    [InlineData(false, ReviewPeriodKind.Month)] [InlineData(true, ReviewPeriodKind.Month)]
    public async Task BothProtocolsSendOneCompletePacketAndValidateFiveCards(bool groq, ReviewPeriodKind kind)
    {
        var pair = Evidence(kind); var packet = Packet(pair); int requestBytes = 0;
        var handler = new Handler(async (request, ct) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(groq ? "https://api.groq.com/openai/v1/chat/completions" : "https://api.openai.com/v1/responses", request.RequestUri!.ToString());
            Assert.True(request.Headers.Authorization?.Parameter == (groq ? "synthetic-groq" : "synthetic-openai"));
            string requestJson = await request.Content!.ReadAsStringAsync(ct); requestBytes = Encoding.UTF8.GetByteCount(requestJson);
            var body = JsonNode.Parse(requestJson)!;
            Assert.Equal(packet.ProviderJson, groq ? body["messages"]![1]!["content"]!.GetValue<string>() : body["input"]![0]!["content"]!.GetValue<string>());
            var format = groq ? body["response_format"]!["json_schema"]! : body["text"]!["format"]!;
            Assert.True(format["strict"]!.GetValue<bool>()); Assert.Equal("period_coaching_v4", format["name"]!.GetValue<string>());
            CheckSchema(format["schema"]!);
            Assert.Contains("untrusted source content", (groq ? body["messages"]![0]!["content"] : body["instructions"])!.GetValue<string>());
            Assert.False(body["stream"]!.GetValue<bool>()); Assert.Null(body["tools"]);
            if (!groq) { Assert.False(body["store"]!.GetValue<bool>()); Assert.Equal("disabled", body["truncation"]!.GetValue<string>()); }
            return Response(200, Envelope(groq, Review(packet)).ToJsonString());
        });
        using var client = new HttpClient(handler); var service = Service(pair, Provider(groq, client));
        Assert.Equal(0, handler.Calls); var result = await service.GenerateAsync(pair.Current.Query);
        Assert.Equal(PeriodCoachingStatus.Success, result.Status); Assert.NotNull(result.Review); Assert.Equal(packet.Json, result.Packet!.Json);
        Assert.Equal(groq ? "Groq" : "OpenAI", result.Metadata!.Provider); Assert.Equal("req_synthetic", result.Metadata.RequestId);
        Assert.Equal(30, result.Metadata.Usage!.TotalTokens); Assert.Null(result.Metadata.MonetaryCost); Assert.Equal(1, handler.Calls);
        output.WriteLine($"{result.Metadata.Provider} {kind}: packet bytes={packet.Utf8ByteCount}; {CoachingSafeDiagnostics.Format(result.Metadata)}");
        if (result.Metadata.InputBudget is { } budget) LogBudget(budget);
        else output.WriteLine($"OpenAI conservative byte ceiling: request={requestBytes}, reserve=16384, estimate={requestBytes + 16384}, input=300000, output=8192");
    }

    [Theory]
    [InlineData(false, "citation")] [InlineData(true, "citation")]
    [InlineData(false, "identity")] [InlineData(true, "identity")]
    [InlineData(false, "incomplete")] [InlineData(true, "incomplete")]
    [InlineData(false, "refusal")] [InlineData(true, "refusal")]
    [InlineData(false, "output-budget")] [InlineData(true, "output-budget")]
    [InlineData(false, "unreported-output-budget")] [InlineData(true, "unreported-output-budget")]
    [InlineData(false, "malformed")] [InlineData(true, "malformed")]
    public async Task PartialUnsupportedOrOverBudgetOutputNeverBecomesAReview(bool groq, string problem)
    {
        var pair = Evidence(); var packet = Packet(pair); string review = Review(packet);
        if (problem == "citation") review = review.Replace("current:statistics", "unknown-source");
        if (problem == "identity") review = review.Replace(packet.PacketId, "wrong-packet");
        if (problem == "unreported-output-budget") review = review.Replace("Insufficient evidence for a step.", string.Concat(Enumerable.Repeat(" x", 10000)));
        var envelope = Envelope(groq, review);
        if (problem == "incomplete") { if (groq) envelope["choices"]![0]!["finish_reason"] = "length"; else envelope["status"] = "incomplete"; }
        if (problem == "refusal") { if (groq) envelope["choices"]![0]!["message"]!["refusal"] = "private"; else envelope["output"]![0]!["content"]![0]!["type"] = "refusal"; }
        if (problem == "output-budget")
        {
            envelope["usage"]![groq ? "completion_tokens" : "output_tokens"] = 9000;
            envelope["usage"]!["total_tokens"] = 9020;
        }
        var handler = new Handler((_, _) => Task.FromResult(Response(200, problem == "malformed" ? "{" : envelope.ToJsonString())));
        using var client = new HttpClient(handler);
        var result = await Service(pair, Provider(groq, client)).GenerateAsync(pair.Current.Query);
        Assert.Equal(problem == "incomplete" ? PeriodCoachingStatus.IncompleteResponse : problem == "refusal" ? PeriodCoachingStatus.Refused : PeriodCoachingStatus.InvalidResponse, result.Status);
        Assert.Null(result.Review); Assert.Null(result.Packet); Assert.Equal(1, handler.Calls);
        if (problem.EndsWith("output-budget", StringComparison.Ordinal)) Assert.Equal(CoachingGenerationPhase.ProviderResponseValidation, result.Metadata!.Phase);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BudgetsMissingKeysAndCancellationPreventHttp(bool groq)
    {
        var pair = Evidence(); var handler = new Handler((_, _) => throw new Exception("Must not send"));
        using var client = new HttpClient(handler);
        var limited = groq ? (IPeriodCoachingProvider)new GroqCoachingProvider(client, new() { InputTokenBudget = 1 }, () => "synthetic")
            : new OpenAiCoachingProvider(client, new() { InputTokenBudget = 1 }, () => "synthetic");
        var result = await Service(pair, limited).GenerateAsync(pair.Current.Query);
        Assert.Equal(PeriodCoachingStatus.TooLarge, result.Status);
        if (groq) Assert.NotNull(result.Metadata!.InputBudget); // OpenAI retains its existing byte-ceiling preflight.
        Assert.Equal(CoachingGenerationPhase.EvidencePreflight, result.Metadata!.Phase);
        Assert.DoesNotContain("narrow", result.Message, StringComparison.OrdinalIgnoreCase);
        var missing = groq ? (IPeriodCoachingProvider)new GroqCoachingProvider(client, new(), () => null) : new OpenAiCoachingProvider(client, new(), () => null);
        Assert.Equal(PeriodCoachingStatus.MissingCredentials, (await Service(pair, missing).GenerateAsync(pair.Current.Query)).Status);
        Assert.Equal(PeriodCoachingStatus.Cancelled, (await Service(pair, Provider(groq, client)).GenerateAsync(pair.Current.Query, new(true))).Status);
        var huge = pair with { Current = pair.Current with { Journals = [pair.Current.Journals[0] with { Text = new string('x', 300_000) }] } };
        Assert.Equal(PeriodCoachingStatus.TooLarge, (await Service(huge, Provider(groq, client)).GenerateAsync(pair.Current.Query)).Status);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task CompleteLongJournalExceedsGroqBudgetWithoutTruncationOrHttp()
    {
        var pair = Evidence(); pair = pair with { Current = pair.Current with { Journals = [pair.Current.Journals[0] with { Text = string.Join(' ', Enumerable.Range(0, 5000)) }] } };
        var packet = Packet(pair); var handler = new Handler((_, _) => throw new Exception("Must not send")); using var client = new HttpClient(handler);
        var result = await Service(pair, Provider(true, client)).GenerateAsync(pair.Current.Query);
        Assert.Equal(PeriodCoachingStatus.TooLarge, result.Status); Assert.Equal(0, handler.Calls); Assert.Null(result.Review);
        output.WriteLine($"Oversized Groq packet bytes={packet.Utf8ByteCount}; {CoachingSafeDiagnostics.Format(result.Metadata!)}");
        LogBudget(result.Metadata!.InputBudget!);
    }

    [Theory]
    [InlineData(false, 403, "model_not_found", PeriodCoachingStatus.ModelUnavailable)]
    [InlineData(true, 404, "model_not_found", PeriodCoachingStatus.ModelUnavailable)]
    [InlineData(false, 429, "rate_limit_exceeded", PeriodCoachingStatus.RateLimited)]
    [InlineData(true, 429, "rate_limit_exceeded", PeriodCoachingStatus.RateLimited)]
    [InlineData(false, 401, "invalid_api_key", PeriodCoachingStatus.AuthenticationFailed)]
    [InlineData(true, 401, "invalid_api_key", PeriodCoachingStatus.AuthenticationFailed)]
    public async Task RejectionsKeepOnlySafeDiagnosticsAndNeverRetry(bool groq, int status, string code, PeriodCoachingStatus expected)
    {
        var handler = new Handler((_, _) => Task.FromResult(Response(status, JsonSerializer.Serialize(new { error = new { code, message = "private source synthetic-key" } }))));
        using var client = new HttpClient(handler); var pair = Evidence(); var result = await Service(pair, Provider(groq, client)).GenerateAsync(pair.Current.Query);
        Assert.Equal(expected, result.Status); Assert.Equal(status, result.Metadata!.HttpStatus); Assert.Equal(code, result.Metadata.ErrorCode);
        Assert.Equal(TimeSpan.FromSeconds(3), result.Metadata.RetryAfter); Assert.Null(result.Review); Assert.Equal(1, handler.Calls);
        Assert.DoesNotContain("private", result.Message + CoachingSafeDiagnostics.Format(result.Metadata));
        Assert.DoesNotContain("synthetic-key", result.Message + CoachingSafeDiagnostics.Format(result.Metadata));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CancellationDuringHttpDiscardsLateCompleteResponse(bool groq)
    {
        var pair = Evidence(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously); CancellationToken seen = default;
        var handler = new Handler((_, ct) => { seen = ct; entered.SetResult(); return pending.Task; }); using var client = new HttpClient(handler);
        using var cts = new CancellationTokenSource(); var task = Service(pair, Provider(groq, client)).GenerateAsync(pair.Current.Query, cts.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); cts.Cancel(); var result = await task;
        Assert.Equal(PeriodCoachingStatus.Cancelled, result.Status); Assert.True(seen.IsCancellationRequested);
        pending.SetResult(Response(200, Envelope(groq, Review(Packet(pair))).ToJsonString()));
        Assert.Null(result.Review); Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task CapturedProviderDoesNotFollowLaterSelectionAndNeverUsesOtherKey()
    {
        var pair = Evidence(); var configuration = new Configuration(); var pending = new TaskCompletionSource<PeriodComparisonEvidenceResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new Handler((request, _) =>
        {
            Assert.Equal("api.groq.com", request.RequestUri!.Host); Assert.True(request.Headers.Authorization?.Parameter == "synthetic-groq");
            return Task.FromResult(Response(200, Envelope(true, Review(Packet(pair))).ToJsonString()));
        });
        using var client = new HttpClient(handler);
        var provider = new SelectedPeriodCoachingProvider(configuration,
            new OpenAiCoachingProvider(client, new(), () => throw new Exception("Wrong provider key")),
            new GroqCoachingProvider(client, new(), () => configuration.CredentialsFor(CoachingProviderKind.Groq).Resolve()));
        var service = new PeriodCoachingGenerationService(new PendingReader(pending.Task), provider, new(), TimeProvider.System);
        Assert.Equal(0, configuration.CredentialReads); var task = service.GenerateAsync(pair.Current.Query);
        configuration.SelectedProvider = CoachingProviderKind.OpenAI;
        pending.SetResult(new(PeriodEvidenceStatus.Ready, pair, null));
        Assert.Equal(PeriodCoachingStatus.Success, (await task).Status); Assert.Equal(1, configuration.CredentialReads); Assert.Equal(1, handler.Calls);
    }

    internal static PeriodComparisonEvidence Evidence(ReviewPeriodKind kind = ReviewPeriodKind.Week)
    {
        var q = new PeriodReviewQuery(kind, new(2026, 10, 8)); var p = PeriodComparisonEvidence.PreviousQuery(q);
        return new(new(p, [], [], [], [], [], [], []), new(q, [], [],
            [new(Guid.Parse("00000000-0000-0000-0000-000000000001"), q.FirstDate, null, null, DailyJournalAccountState.AllAccounts, "Synthetic journal-only observation", new("", "", ""), true, 1, q.FromUtc, q.FromUtc)], [], [], [], []), PeriodSourceConsistency.SingleReadTransaction);
    }
    private void LogBudget(CoachingInputBudget b) => output.WriteLine($"Groq token estimate: instructions={b.InstructionTokens}, schema={b.SchemaTokens}, evidence={b.EvidenceTokens}, framing={b.FramingTokens}, margin={b.SafetyMarginTokens}, reserve={b.ReserveTokens}, total={b.EstimatedInputTokens}, input={b.InputLimit}, output={b.OutputLimit}");
    internal static PeriodCoachingPacket Packet(PeriodComparisonEvidence pair)
    { var result = PeriodCoachingPacketBuilder.Build(pair); Assert.Equal(CoachingPacketBuildStatus.Ready, result.Status); return result.Packet!; }
    internal static string Review(PeriodCoachingPacket p)
    {
        var section = new { insufficientEvidence = true, findings = new[] { new { basis = "CalculatedFact", text = "Insufficient recorded evidence.", sourceIds = new[] { "current:statistics" } } } };
        return JsonSerializer.Serialize(new { contractVersion = p.ContractVersion, packetId = p.PacketId, strengths = section, areasForImprovement = section,
            mistakesObserved = section, progressVersusPreviousPeriod = section,
            actionPlan = new { insufficientEvidence = true, explanation = "Insufficient evidence for a step.", sourceIds = new[] { "current:statistics" }, steps = Array.Empty<object>() } });
    }
    internal static JsonObject Envelope(bool groq, string review) => JsonNode.Parse(groq ? JsonSerializer.Serialize(new
    {
        id = "chatcmpl-synthetic", model = GroqCoachingOptions.SupportedModel, @object = "chat.completion",
        choices = new[] { new { finish_reason = "stop", message = new { role = "assistant", content = review } } },
        usage = new { prompt_tokens = 20, completion_tokens = 10, total_tokens = 30 },
    }) : JsonSerializer.Serialize(new
    {
        id = "resp_synthetic", model = OpenAiCoachingOptions.SupportedModel, status = "completed",
        output = new[] { new { type = "message", role = "assistant", status = "completed", content = new[] { new { type = "output_text", text = review } } } },
        usage = new { input_tokens = 20, output_tokens = 10, total_tokens = 30 },
    }))!.AsObject();
    internal static HttpResponseMessage Response(int status, string body)
    {
        var r = new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        r.Headers.Add("x-request-id", "req_synthetic"); r.Headers.Add("Retry-After", "3"); return r;
    }
    private static IPeriodCoachingProvider Provider(bool groq, HttpClient client) => groq
        ? new GroqCoachingProvider(client, new(), () => "synthetic-groq") : new OpenAiCoachingProvider(client, new(), () => "synthetic-openai");
    private static PeriodCoachingGenerationService Service(PeriodComparisonEvidence p, IPeriodCoachingProvider provider) => new(new PendingReader(Task.FromResult(new PeriodComparisonEvidenceResult(PeriodEvidenceStatus.Ready, p, null))), provider, new(), TimeProvider.System);
    private static void CheckSchema(JsonNode schema)
    {
        if (schema["type"]?.GetValue<string>() == "object")
        {
            Assert.False(schema["additionalProperties"]!.GetValue<bool>());
            Assert.Equal(schema["properties"]!.AsObject().Select(p => p.Key).Order(), schema["required"]!.AsArray().Select(p => p!.GetValue<string>()).Order());
            foreach (var p in schema["properties"]!.AsObject()) CheckSchema(p.Value!);
        }
        if (schema["items"] is { } item) CheckSchema(item);
    }
    internal sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { Calls++; return send(request, ct); }
    }
    private sealed class PendingReader(Task<PeriodComparisonEvidenceResult> pending) : IPeriodComparisonEvidenceReader
    { public Task<PeriodComparisonEvidenceResult> ReadComparisonAsync(PeriodReviewQuery q, CancellationToken cancellationToken = default) => pending; }
    private sealed class Configuration : ICoachingConfiguration, ICoachingCredentials
    {
        public CoachingProviderKind SelectedProvider { get; set; } = CoachingProviderKind.Groq;
        public int CredentialReads;
        public ICoachingCredentials CredentialsFor(CoachingProviderKind provider) { Assert.Equal(CoachingProviderKind.Groq, provider); return this; }
        public string? Resolve() { CredentialReads++; return "synthetic-groq"; }
        public CoachingCredentialSource GetSource() => CoachingCredentialSource.Saved;
    }
}
