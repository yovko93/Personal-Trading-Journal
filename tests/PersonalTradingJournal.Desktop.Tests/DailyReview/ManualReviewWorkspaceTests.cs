using System.Text.Json;
using System.Text.Json.Nodes;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Application.PeriodReviews;
using PersonalTradingJournal.Application.PeriodReviews.Coaching;
using PersonalTradingJournal.Desktop.Tests.PeriodReviews;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.DailyReview;

namespace PersonalTradingJournal.Desktop.Tests.DailyReview;

public sealed class ManualReviewWorkspaceTests
{
    [Theory] [InlineData(ManualReviewKind.Daily)] [InlineData(ManualReviewKind.Weekly)] [InlineData(ManualReviewKind.Monthly)]
    public async Task ExplicitCopyValidationDraftGuardAndDuplicateSave(ManualReviewKind kind)
    {
        var store = new Store(); var clipboard = new Clip(); var dialogs = new FakeDialogService(); int refreshed = 0;
        var selection = new ManualReviewRequest(kind, ReviewFixture.Day, null, "All accounts");
        ManualReviewViewModel Create() => new(store, kind, () => selection, dialogs, clipboard, () => { refreshed++; return Task.CompletedTask; });
        var vm = Create(); await vm.LoadAsync(); Assert.Equal(0, store.Prepares); Assert.Equal(0, clipboard.Reads + clipboard.Writes);
        await vm.PrepareCommand.ExecuteAsync(null); Assert.Equal(1, store.Prepares); Assert.True(vm.HasPending);
        string prompt = vm.Prompt; selection = selection with { Date = selection.Date.AddMonths(1), AccountId = Guid.NewGuid(), AccountLabel = "Different" };
        Assert.Equal(prompt, vm.Prompt); Assert.DoesNotContain("Different", vm.PendingScope);
        clipboard.Fail = true; vm.CopyCommand.Execute(null); Assert.Contains("Clipboard unavailable", vm.Message); Assert.Equal(prompt, vm.Prompt);
        clipboard.Fail = false; vm.CopyCommand.Execute(null); Assert.Equal(prompt, clipboard.Text);
        clipboard.Text = "bad JSON"; vm.PasteCommand.Execute(null); Assert.False(vm.TryLeave());
        await vm.ValidateCommand.ExecuteAsync(null); Assert.Null(vm.Preview); Assert.Equal("bad JSON", vm.ResponseText); Assert.True(vm.TryLeave());
        Assert.False(vm.SaveCommand.CanExecute(null)); Assert.Equal(0, store.Saves);
        vm = Create(); await vm.LoadAsync(); Assert.Equal("bad JSON", vm.ResponseText);
        vm.ResponseText = "```json\n" + Response(store.Draft!) + "\n```";
        await vm.ValidateCommand.ExecuteAsync(null); Assert.NotNull(vm.Preview); Assert.NotEmpty(vm.PreviewCards);
        await vm.SaveCommand.ExecuteAsync(null); Assert.Equal(1, store.Saves); Assert.Equal(1, refreshed);
        Assert.False(vm.SaveCommand.CanExecute(null)); Assert.NotNull(vm.Preview);
        await vm.SaveCommand.ExecuteAsync(null); Assert.Equal(1, store.Saves);
        vm = Create(); await vm.LoadAsync(); Assert.Contains("already saved", vm.Message);
        await vm.DiscardCommand.ExecuteAsync(null); Assert.True(vm.HasPending);
        dialogs.ConfirmationResult = true; await vm.DiscardCommand.ExecuteAsync(null); Assert.False(vm.HasPending);
        Assert.Equal(1, clipboard.Reads); Assert.Equal(2, clipboard.Writes);
    }

    [Fact]
    public async Task PendingOperationSuppressesDoubleClickAndCancellationRetainsState()
    {
        var store = new Store { Pending = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var vm = new ManualReviewViewModel(store, ManualReviewKind.Daily, () => new(ManualReviewKind.Daily, ReviewFixture.Day, null, "All accounts"), new FakeDialogService(), new Clip(), () => Task.CompletedTask);
        await vm.LoadAsync(); var first = vm.PrepareCommand.ExecuteAsync(null);
        await vm.PrepareCommand.ExecuteAsync(null); Assert.Equal(1, store.Prepares); Assert.False(vm.TryLeave());
        vm.CancelCommand.Execute(null); await first; Assert.False(vm.HasPending); Assert.False(vm.IsBusy);
        Assert.Contains("cancelled", vm.Message); Assert.Equal(0, store.Saves);
    }

    [Fact]
    public async Task ProductionHostWiringDisablesLegacyProviderCommands()
    {
        var provider = new DailyReviewGenerationTests.Provider(); var daily = DailyReviewGenerationTests.Ready(provider); var store = new Store();
        var host = new DailyReviewViewModel(daily.Reader, daily.History, daily.Accounts, new FakeDialogService(), TimeProvider.System,
            new(new(provider, new(), TimeProvider.System), daily.History, TimeProvider.System), manualReviews: store, clipboard: new Clip());
        await host.ActivateAsync(); Assert.False(host.GenerateCommand.CanExecute(null));
        await host.GenerateCommand.ExecuteAsync(null); Assert.NotNull(host.Manual);
        Assert.Equal(0, provider.Calls);
        foreach (var kind in new[] { ReviewPeriodKind.Week, ReviewPeriodKind.Month })
        {
            var f = new PeriodFixture(kind); int calls = 0;
            var period = new PersonalTradingJournal.Desktop.ViewModels.PeriodReviews.PeriodReviewViewModel(kind, f.Reader, f.Store, f.Accounts, f.Dialogs, TimeProvider.System,
                (_, _) => { calls++; throw new InvalidOperationException(); }, f.Credentials, manualReviews: new Store(), clipboard: new Clip());
            await period.ActivateAsync(); await period.GenerateCommand.ExecuteAsync(null); Assert.Equal(0, calls);
            await period.Manual!.PrepareCommand.ExecuteAsync(null); Assert.True(period.Manual.HasPending);
        }
    }

    internal sealed class Clip : IReviewClipboard
    {
        public int Reads, Writes; public bool Fail; public string Text = "";
        public void Copy(string text) { Writes++; if (Fail) throw new InvalidOperationException(); Text = text; }
        public string Paste() { Reads++; if (Fail) throw new InvalidOperationException(); return Text; }
    }
    internal sealed class Store : IManualReviewService
    {
        public ManualReviewDraft? Draft; public int Prepares, Saves; public bool FailSave; public bool TooLarge;
        public TaskCompletionSource? Pending;
        public Task<ManualReviewDraft?> LoadAsync(ManualReviewKind kind, CancellationToken token = default) => Task.FromResult(Draft);
        public async Task<ManualReviewDraft> PrepareAsync(ManualReviewRequest r, CancellationToken token = default)
        {
            Prepares++; if (Pending is not null) await Pending.Task.WaitAsync(token);
            if (TooLarge) throw new ManualReviewException("TooLarge: complete prompt exceeds 300,000 UTF-8 bytes. Nothing sent.");
            string json;
            if (r.Kind == ManualReviewKind.Daily) json = CoachingEvidencePacketBuilder.Build(ReviewFixture.Evidence(new(r.Date, r.AccountId))).Packet!.Json;
            else
            {
                var q = new PeriodReviewQuery(r.Kind == ManualReviewKind.Weekly ? ReviewPeriodKind.Week : ReviewPeriodKind.Month, r.Date, r.AccountId);
                var packet = PeriodCoachingPacketBuilder.Build(PeriodFixture.Pair(q)).Packet!;
                json = packet.Json; r = r with { Date = q.FirstDate };
            }
            return Draft = new(Guid.NewGuid(), 1, r, DateTimeOffset.UtcNow, json, "Synthetic exact prompt. No provider called.", "");
        }
        public Task<ManualReviewDraft> KeepResponseAsync(ManualReviewDraft expected, string response, CancellationToken token = default) => Task.FromResult(Draft = expected with { Revision = expected.Revision + 1, ResponseText = response });
        public ManualReviewValidation Validate(ManualReviewDraft draft, CancellationToken token = default) => ManualReviewContract.Validate(draft, token);
        public Task<ManualReviewDraft> SaveAsync(ManualReviewDraft expected, CancellationToken token = default)
        {
            if (expected.Saved) return Task.FromResult(expected);
            if (FailSave) throw new IOException("synthetic-private-data-must-not-display");
            Assert.True(Validate(expected, token).Valid); Saves++;
            return Task.FromResult(Draft = expected with { Saved = true, Revision = expected.Revision + 1 });
        }
        public Task DiscardAsync(ManualReviewDraft expected, CancellationToken token = default) { Draft = null; return Task.CompletedTask; }
    }
    internal static string Response(ManualReviewDraft draft)
    {
        var root = JsonNode.Parse(draft.EvidenceJson)!; string id = root["packetId"]!.GetValue<string>();
        if (draft.Request.Kind == ManualReviewKind.Daily)
            return JsonSerializer.Serialize(new { contractVersion = CoachingContract.Version, packetId = id,
                daySummary = new { text = "Synthetic day summary.", sourceIds = new[] { "calculated:day" } },
                executionObservations = Array.Empty<object>(), behaviorObservations = Array.Empty<object>(), improvementSuggestions = Array.Empty<object>(), uncertainties = Array.Empty<object>() });
        var finding = new { basis = "CalculatedFact", text = "Insufficient evidence to conclude improvement.", sourceIds = new[] { "current:statistics" } };
        var section = new { insufficientEvidence = true, findings = new[] { finding } };
        return JsonSerializer.Serialize(new { contractVersion = PeriodCoachingContract.Version, packetId = id,
            strengths = section, areasForImprovement = section, mistakesObserved = section, progressVersusPreviousPeriod = section,
            actionPlan = new { insufficientEvidence = true, explanation = "No supported actions.", sourceIds = new[] { "current:statistics" }, steps = Array.Empty<object>() } });
    }
}
