using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Desktop.ViewModels.Rules;

namespace PersonalTradingJournal.Desktop.Tests.Rules;

public sealed class RulesOrderTests
{
    [Fact]
    public async Task UnchangedSlotsNeverWriteOrReloadAndRealMovePublishesExactlyOnce()
    {
        var f = new RulesFixture(); for (int i = 0; i < 3; i++) f.Data.Seed(1);
        await f.Vm.ActivateAsync(); var original = f.Vm.Rules.ToArray(); var collection = f.Vm.Rules;
        int reads = f.Data.CatalogCalls.Count, published = 0; string feedback = f.Vm.Message;
        var changes = new List<System.Collections.Specialized.NotifyCollectionChangedAction>();
        ((System.Collections.Specialized.INotifyCollectionChanged)collection).CollectionChanged += (_, e) => changes.Add(e.Action);
        f.Vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(f.Vm.Rules)) published++; };
        await f.Vm.MoveBeforeAsync(original[0].Id, original[1].Id);
        await f.Vm.MoveBeforeAsync(original[1].Id, original[1].Id);
        await f.Vm.MoveBeforeAsync(original[^1].Id, null);
        Assert.Equal(0, f.Data.MoveCalls); Assert.Equal(reads, f.Data.CatalogCalls.Count);
        Assert.Same(collection, f.Vm.Rules); Assert.Equal(0, published); Assert.Empty(changes);
        await f.Vm.MoveBeforeAsync(original[1].Id, original[0].Id);
        Assert.Equal(1, f.Data.MoveCalls); Assert.Equal(reads + 1, f.Data.CatalogCalls.Count); Assert.Equal(0, published);
        Assert.Same(collection, f.Vm.Rules); Assert.Equal([System.Collections.Specialized.NotifyCollectionChangedAction.Move], changes);
        Assert.All(original, row => Assert.Same(row, f.Vm.Rules.Single(r => r.Id == row.Id)));
        Assert.Equal(new[] { original[1].Id, original[0].Id, original[2].Id }, f.Vm.Rules.Select(r => r.Id));
        Assert.Equal(new[] { 1, 2, 3 }, f.Vm.Rules.Select(r => r.DisplayNumber));
        Assert.Equal(feedback, f.Vm.Message); f.Vm.Deactivate();
    }

    [Fact]
    public async Task OverlappingDropIsIgnoredAndCancelledWriteReadsPersistedOrderOnce()
    {
        var f = new RulesFixture(); for (int i = 0; i < 3; i++) f.Data.Seed(1);
        await f.Vm.ActivateAsync(); var original = f.Vm.Rules; int reads = f.Data.CatalogCalls.Count;
        f.Data.WriteGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = f.Vm.MoveBeforeAsync(original[1].Id, original[0].Id);
        await f.Vm.MoveBeforeAsync(original[2].Id, original[0].Id);
        f.Vm.CancelOperationCommand.Execute(null); await pending;
        Assert.InRange(f.Data.MoveCalls, 0, 1); // Cancellation may prevent Task.Run from entering the store at all.
        Assert.Equal(reads + 1, f.Data.CatalogCalls.Count);
        Assert.Equal(original.Select(r => r.Id), f.Vm.Rules.Select(r => r.Id));
        Assert.Contains("cancelled", f.Vm.Message); f.Vm.Deactivate();
    }

    [Fact]
    public async Task MoveSecondBeforeFirstKeyboardBoundariesRefreshAndNavigationRetainIdentity()
    {
        var f = new RulesFixture(); for (int i = 0; i < 3; i++) f.Data.Seed(1);
        await f.Vm.ActivateAsync(); var original = f.Vm.Rules.Select(r => r.Id).ToArray();
        string feedback = f.Vm.Message;
        Assert.False(f.Vm.MoveUpCommand.CanExecute(f.Vm.Rules[0]));
        Assert.False(f.Vm.MoveDownCommand.CanExecute(f.Vm.Rules[^1]));
        await f.Vm.MoveBeforeAsync(original[1], original[0]);
        Assert.Equal(feedback, f.Vm.Message);
        Assert.Equal(new[] { original[1], original[0], original[2] }, f.Vm.Rules.Select(r => r.Id));
        await f.Vm.MoveDownCommand.ExecuteAsync(f.Vm.Rules[0]);
        Assert.Equal(feedback, f.Vm.Message);
        Assert.Equal(original, f.Vm.Rules.Select(r => r.Id));
        await f.Vm.MoveBeforeAsync(original[0], null);
        await f.Vm.RefreshCommand.ExecuteAsync(null); f.Vm.Deactivate(); await f.Vm.ActivateAsync();
        Assert.Equal(new[] { original[1], original[2], original[0] }, f.Vm.Rules.Select(r => r.Id));
        Assert.Equal(new[] { 1, 2, 3 }, f.Vm.Rules.Select(r => r.DisplayNumber));
        Assert.Equal(3, f.Data.Rows.Count); Assert.All(f.Data.Rows, r => Assert.Equal(1, r.Revision));
    }

    [Theory]
    [InlineData("failure")]
    [InlineData("cancel")]
    [InlineData("conflict")]
    public async Task FailedMovesReloadPersistedOrderAndRetainDraft(string failure)
    {
        var f = new RulesFixture(); f.Data.Seed(1); f.Data.Seed(1); await f.Vm.ActivateAsync();
        var ids = f.Vm.Rules.Select(r => r.Id).ToArray();
        await f.Vm.EditRowCommand.ExecuteAsync(f.Vm.Rules[0]); f.Vm.Fields[1].Text = "Unsaved";
        await f.Vm.MoveBeforeAsync(ids[1], ids[0]); // declined discard
        Assert.Equal(ids, f.Vm.Rules.Select(r => r.Id)); Assert.True(f.Vm.IsEditing);
        f.Dialogs.ConfirmationResult = true; f.Data.Failure = failure;
        await f.Vm.MoveBeforeAsync(ids[1], ids[0]);
        Assert.Equal(ids, f.Vm.Rules.Select(r => r.Id)); Assert.True(f.Vm.IsEditing);
        Assert.Equal("Unsaved", f.Vm.Fields[1].Text); Assert.Contains("persisted order", f.Vm.Message);
    }

    [Fact]
    public async Task ConcurrentMoveReloadsActualOrderAndDelayedMoveIsCancellable()
    {
        var f = new RulesFixture(); for (int i = 0; i < 3; i++) f.Data.Seed(1);
        await f.Vm.ActivateAsync(); var ids = f.Vm.Rules.Select(r => r.Id).ToArray();
        Assert.Equal(RuleMoveStatus.Saved, await f.Data.MoveAsync(new(ids[2], ids[0], f.Data.CatalogVersion)));
        await f.Vm.MoveBeforeAsync(ids[1], ids[0]);
        Assert.Equal(new[] { ids[2], ids[0], ids[1] }, f.Vm.Rules.Select(r => r.Id));
        Assert.Equal(RuleFeedbackKind.Error, f.Vm.FeedbackKind);
        f.Data.WriteGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var move = f.Vm.MoveBeforeAsync(ids[1], ids[2]);
        Assert.True(f.Vm.IsWriting); Assert.False(f.Vm.TryLeave());
        f.Vm.CancelOperationCommand.Execute(null); await move;
        Assert.Equal(new[] { ids[2], ids[0], ids[1] }, f.Vm.Rules.Select(r => r.Id));
        Assert.Contains("cancelled", f.Vm.Message);
    }
}
