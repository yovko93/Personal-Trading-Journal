using PersonalTradingJournal.Desktop.ViewModels.Rules;

namespace PersonalTradingJournal.Desktop.Tests.Rules;

public sealed class RulesFeedbackTests
{
    [Fact]
    public async Task ReplacementExpiresTenSecondsAfterLatestActionWithoutClearingValidation()
    {
        var clock = new FeedbackClock(); var f = new RulesFixture(clock);
        await f.Vm.ActivateAsync(); Assert.Equal(RuleFeedbackKind.Success, f.Vm.FeedbackKind);
        f.Vm.CreateCommand.Execute(null);
        var oldExpiry = f.Vm.FeedbackExpiryTask;
        Assert.Equal(RuleFeedbackKind.Information, f.Vm.FeedbackKind);
        clock.Advance(9); Assert.True(f.Vm.HasMessage);
        await f.Vm.SaveCommand.ExecuteAsync(null); await oldExpiry;
        Assert.Equal(RuleFeedbackKind.Error, f.Vm.FeedbackKind);
        clock.Advance(1); Assert.True(f.Vm.HasMessage); Assert.True(f.Vm.HasErrors);
        clock.Advance(8); Assert.True(f.Vm.HasMessage);
        clock.Advance(1); await f.Vm.FeedbackExpiryTask;
        Assert.False(f.Vm.HasMessage); Assert.All(f.Vm.Fields, field => Assert.True(field.IsInvalid));
        f.Vm.Fields[0].Text = "Correct name"; Assert.False(f.Vm.Fields[0].IsInvalid);
        Assert.True(f.Vm.Fields[1].IsInvalid);
        f.Fill(); await f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Rule saved.", f.Vm.Message); Assert.Equal(RuleFeedbackKind.Success, f.Vm.FeedbackKind);
        await f.Vm.EditRowCommand.ExecuteAsync(Assert.Single(f.Vm.Rules));
        Assert.StartsWith("Editing rule", f.Vm.Message);
        f.Vm.CancelEditCommand.Execute(null); Assert.Equal("Editing cancelled.", f.Vm.Message);
        await f.Vm.DeleteCommand.ExecuteAsync(Assert.Single(f.Vm.Rules));
        Assert.StartsWith("Delete cancelled", f.Vm.Message);
        f.Dialogs.ConfirmationResult = true;
        await f.Vm.DeleteCommand.ExecuteAsync(Assert.Single(f.Vm.Rules));
        Assert.Equal(RuleFeedbackKind.Success, f.Vm.FeedbackKind); Assert.StartsWith("Rule deleted", f.Vm.Message);
        f.Vm.Deactivate(); Assert.False(f.Vm.HasMessage);
        clock.Advance(10); await f.Vm.FeedbackExpiryTask; Assert.False(f.Vm.HasMessage);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledOrFailedLaterPageNeverPublishesPartialCatalog(bool fail)
    {
        var f = new RulesFixture(); f.Data.Seed(1); await f.Vm.ActivateAsync();
        var prior = f.Vm.Rules;
        for (int i = 0; i < 40; i++) f.Data.Seed(1);
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Data.BeforeCatalogPage = async (page, token) =>
        {
            if (page != 2) return;
            started.SetResult(token); await release.Task;
            if (fail) throw new IOException("Synthetic read failure");
        };
        var load = f.Vm.RefreshCommand.ExecuteAsync(null);
        var token = await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(f.Vm.IsLoading); Assert.Same(prior, f.Vm.Rules);
        if (!fail) { f.Vm.CancelOperationCommand.Execute(null); Assert.True(token.IsCancellationRequested); }
        release.SetResult(); await load;
        Assert.Same(prior, f.Vm.Rules); Assert.False(f.Vm.IsLoading);
        Assert.Contains(fail ? "could not be loaded completely" : "cancelled", f.Vm.Message);
        Assert.DoesNotContain(f.Data.CatalogCalls, call => call.Page == 3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChangedCountOrDuplicateAcrossPagesIsReportedInsteadOfOmitted(bool sameCount)
    {
        var f = new RulesFixture(); for (int i = 0; i < 21; i++) f.Data.Seed(1);
        await f.Vm.ActivateAsync(); var prior = f.Vm.Rules;
        f.Data.BeforeCatalogPage = (page, _) =>
        {
            if (page == 2)
            {
                if (sameCount)
                {
                    // Move the first row after the page boundary, without changing the total.
                    var first = f.Data.Rows.Single(r => r.RuleId == prior[0].Id);
                    f.Data.Rows.Remove(first);
                    f.Data.Rows.Add(PersonalTradingJournal.Domain.Rules.TradingRuleRevision.Rehydrate(first.RuleId, first.Revision,
                        new("ZZ last", "Moved during read"), true, first.RecordedAtUtc));
                }
                else f.Data.Seed(1);
            }
            return Task.CompletedTask;
        };
        await f.Vm.RefreshCommand.ExecuteAsync(null);
        Assert.Same(prior, f.Vm.Rules); Assert.Equal(RuleFeedbackKind.Error, f.Vm.FeedbackKind);
    }
}

internal sealed class FeedbackClock : TimeProvider
{
    private TimeSpan _now;
    private readonly List<ClockTimer> _timers = [];
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ClockTimer(callback, state, _now + dueTime); _timers.Add(timer); return timer;
    }
    internal void Advance(int seconds)
    {
        _now += TimeSpan.FromSeconds(seconds);
        foreach (var timer in _timers.ToArray())
            if (!timer.Disposed && timer.Due <= _now) { timer.Dispose(); timer.Callback(timer.State); }
    }
    private sealed class ClockTimer(TimerCallback callback, object? state, TimeSpan due) : ITimer
    {
        internal TimerCallback Callback { get; } = callback;
        internal object? State { get; } = state;
        internal TimeSpan Due { get; } = due;
        internal bool Disposed { get; private set; }
        public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();
        public void Dispose() => Disposed = true;
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
