using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Setups;
using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Desktop.Tests.Setups;

public sealed class PlaybookViewModelTests
{
    [Fact]
    public async Task EmptyCatalogAndSetupWithoutGuidanceRemainUsable()
    {
        var f = new PlaybookFixture(); f.Data.Setups.Clear(); await f.Vm.ActivateAsync();
        Assert.Empty(f.Vm.Setups); Assert.Null(f.Vm.SelectedSetup); Assert.False(f.Vm.EditCommand.CanExecute(null));
        f.Data.Setups.Add(f.Setup); await f.OpenAsync();
        Assert.False(f.Vm.HasGuidance); Assert.Contains("without guidance", f.Vm.Message);
        Assert.True(f.Vm.EditCommand.CanExecute(null)); Assert.Empty(f.Data.Revisions);
    }

    [Fact]
    public async Task SaveAppendsAllFieldsAndPreservesExamplesThenClosesEditor()
    {
        var f = new PlaybookFixture(); f.Seed(1); await f.OpenAsync(); f.Edit("  changed  ");
        await f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.False(f.Vm.IsEditing); Assert.Equal(2, f.Vm.DisplayedRevision!.Revision);
        Assert.All(PlaybookFixture.Texts(f.Vm.DisplayedRevision.Guidance), v => Assert.Equal("changed", v));
        Assert.Single(f.Vm.DisplayedRevision.Guidance.Examples);
        Assert.Equal("context", f.Data.Revisions[0].Guidance.MarketContext);
        Assert.Equal(1, f.Data.AppendCount); Assert.Contains("saved", f.Vm.Message);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task EverySectionIsRequiredAndValidationRetainsAllFields(int index)
    {
        var f = new PlaybookFixture(); await f.OpenAsync(); f.Edit("keep"); f.Vm.Fields[index].Text = " \r\n ";
        await f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.True(f.Vm.IsEditing); Assert.Contains(f.Vm.Fields[index].Label, f.Vm.ValidationSummary);
        Assert.Equal("keep", f.Vm.Fields[(index + 1) % 5].Text); Assert.Empty(f.Data.Revisions);
    }

    [Fact]
    public async Task OverlongTextIsNotSilentlyTruncated()
    {
        var f = new PlaybookFixture(); await f.OpenAsync(); f.Edit(new string('x', 4001));
        await f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.True(f.Vm.IsEditing); Assert.Equal(4001, f.Vm.Fields[0].Text.Length); Assert.Empty(f.Data.Revisions);
    }

    [Theory]
    [InlineData("conflict")] [InlineData("failure")] [InlineData("cancel")]
    public async Task WriteFailuresKeepTextAndGuardedRefreshRecovers(string failure)
    {
        var f = new PlaybookFixture(); f.Seed(1); await f.OpenAsync(); f.Edit("retain me");
        f.Data.WriteFailure = failure;
        await f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.True(f.Vm.IsEditing); Assert.All(f.Vm.Fields, x => Assert.Equal("retain me", x.Text));
        Assert.Single(f.Data.Revisions); Assert.DoesNotContain("SECRET", f.Vm.Message);
        await f.Vm.RefreshCommand.ExecuteAsync(null); Assert.True(f.Vm.IsEditing);
        f.Dialogs.ConfirmationResult = true; f.Data.WriteFailure = null;
        await f.Vm.RefreshCommand.ExecuteAsync(null); Assert.False(f.Vm.IsEditing); Assert.Equal(1, f.Vm.DisplayedRevision!.Revision);
    }

    [Fact]
    public async Task ActualNewerRevisionIsNeverOverwritten()
    {
        var f = new PlaybookFixture(); f.Seed(1); await f.OpenAsync(); f.Edit("local"); f.Seed(2);
        await f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.True(f.Vm.IsEditing); Assert.Equal(2, f.Data.Revisions.Count); Assert.Equal(0, f.Data.AppendCount);
        Assert.Contains("changed", f.Vm.Message);
    }

    [Fact]
    public async Task SwitchCancelRefreshAndLeaveShareGuardAndChangeThenRevertIsClean()
    {
        var f = new PlaybookFixture(); f.Seed(1); await f.OpenAsync(); f.Edit("local");
        Assert.False(f.Vm.TrySelectSetup(f.Data.Row(f.Other))); Assert.False(f.Vm.TryLeave());
        f.Vm.CancelEditCommand.Execute(null); Assert.True(f.Vm.IsEditing);
        Assert.Equal(f.Setup.Id, f.Vm.SelectedSetup!.Id); Assert.Equal("local", f.Vm.Fields[0].Text);
        var texts = PlaybookFixture.Texts(f.Data.Revisions[0].Guidance);
        for (int i = 0; i < 5; i++) f.Vm.Fields[i].Text = texts[i];
        Assert.False(f.Vm.HasUnsavedChanges); Assert.True(f.Vm.TryLeave());
        Assert.True(f.Vm.TrySelectSetup(f.Data.Row(f.Other))); await f.Vm.LoadTask;
        Assert.False(f.Vm.IsEditing); Assert.False(f.Vm.HasGuidance);
    }

    [Fact]
    public async Task HistoricalReadsHaveFrozenNameBoundedPagesAndNoWrite()
    {
        var f = new PlaybookFixture(); f.Seed(23); await f.OpenAsync();
        Assert.Equal(Enumerable.Range(14, 10).Reverse(), f.Vm.Revisions.Select(r => r.Revision));
        f.Vm.ViewRevisionCommand.Execute(f.Vm.Revisions[1]); Assert.True(f.Vm.IsHistorical);
        Assert.Equal(22, f.Vm.DisplayedRevision!.Revision); Assert.False(f.Vm.EditCommand.CanExecute(null));
        f.Vm.ShowCurrentCommand.Execute(null); Assert.Equal(23, f.Vm.DisplayedRevision!.Revision);
        await f.Vm.NextPageCommand.ExecuteAsync(null); Assert.Equal(13, f.Vm.Revisions[0].Revision);
        await f.Vm.NextPageCommand.ExecuteAsync(null); Assert.Equal(new[] { 3, 2, 1 }, f.Vm.Revisions.Select(r => r.Revision));
        Assert.False(f.Vm.NextPageCommand.CanExecute(null));
        await f.Vm.PreviousPageCommand.ExecuteAsync(null); Assert.Equal(13, f.Vm.Revisions[0].Revision);
        Assert.All(f.Data.Limits, x => Assert.Equal(11, x)); Assert.Equal(0, f.Data.AppendCount);
    }

    [Fact]
    public async Task ArchiveConfirmationAndReopenUseExistingCatalogWithoutLosingHistory()
    {
        var f = new PlaybookFixture(); f.Seed(1); await f.OpenAsync();
        await f.Vm.ToggleArchiveCommand.ExecuteAsync(null); Assert.True(f.Setup.IsActive);
        f.Dialogs.ConfirmationResult = true;
        await f.Vm.ToggleArchiveCommand.ExecuteAsync(null); Assert.False(f.Setup.IsActive);
        Assert.False(f.Vm.EditCommand.CanExecute(null)); Assert.Single(f.Vm.Revisions);
        Assert.Equal("Reopen Setup", f.Vm.ArchiveLabel);
        await f.Vm.ToggleArchiveCommand.ExecuteAsync(null); Assert.True(f.Setup.IsActive); Assert.True(f.Vm.EditCommand.CanExecute(null));
        Assert.Single(f.Data.Revisions);
    }

    [Fact]
    public async Task SupersededReadAndDeactivationCannotReplaceSelection()
    {
        var f = new PlaybookFixture(); f.Seed(1); await f.Vm.ActivateAsync();
        var delayed = new TaskCompletionSource<TradingSetupPlaybookRevision?>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Data.NextRead = delayed.Task;
        f.Vm.TrySelectSetup(f.Data.Row(f.Setup)); var old = f.Vm.LoadTask;
        Assert.True(f.Vm.IsLoading);
        f.Vm.TrySelectSetup(f.Data.Row(f.Other)); await f.Vm.LoadTask;
        delayed.SetResult(f.Data.Revisions[0]); await old;
        Assert.Equal(f.Other.Id, f.Vm.SelectedSetup!.Id); Assert.Null(f.Vm.DisplayedRevision);
        f.Vm.Deactivate(); await f.Vm.ActivateAsync(); // Disposed CTS cannot leak into re-entry.
        Assert.False(f.Vm.IsLoading);
    }

    [Fact]
    public async Task ActiveWriteBlocksLeavingAndDuplicateSubmissionAndCancellationKeepsDraft()
    {
        var f = new PlaybookFixture(); await f.OpenAsync(); f.Edit("retain");
        f.Data.BlockWrite = true;
        var pending = f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.True(f.Vm.IsWriting); Assert.False(f.Vm.SaveCommand.CanExecute(null)); Assert.False(f.Vm.TryLeave());
        Assert.False(f.Vm.CanEditFields); Assert.False(f.Vm.TrySelectSetup(f.Data.Row(f.Other)));
        f.Vm.CancelOperationCommand.Execute(null); await pending;
        Assert.True(f.Vm.IsEditing); Assert.False(f.Vm.IsWriting); Assert.Empty(f.Data.Revisions);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ReadErrorOrCancellationRecoversWithoutExposingException(bool cancel)
    {
        var f = new PlaybookFixture(); f.Seed(1); await f.Vm.ActivateAsync();
        var read = new TaskCompletionSource<TradingSetupPlaybookRevision?>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Data.NextRead = read.Task; f.Vm.TrySelectSetup(f.Data.Row(f.Setup)); var pending = f.Vm.LoadTask;
        if (cancel) { f.Vm.CancelOperationCommand.Execute(null); read.SetResult(f.Data.Revisions[0]); }
        else read.SetException(new IOException("SECRET filesystem path"));
        await pending;
        Assert.False(f.Vm.IsLoading); Assert.False(f.Vm.EditCommand.CanExecute(null)); Assert.Null(f.Vm.DisplayedRevision);
        Assert.DoesNotContain("SECRET", f.Vm.Message); Assert.Contains(cancel ? "cancelled" : "could not", f.Vm.Message);
        await f.Vm.RefreshCommand.ExecuteAsync(null); Assert.Equal(1, f.Vm.DisplayedRevision!.Revision);
    }

    [Fact]
    public async Task CommittedSaveRemainsSuccessfulWhenFollowUpReadFails()
    {
        var f = new PlaybookFixture(); await f.OpenAsync(); f.Edit("saved"); f.Data.FailReadAfterAppend = true;
        await f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.Single(f.Data.Revisions); Assert.True(f.Vm.IsEditing);
        Assert.Null(f.Vm.DisplayedRevision);
        Assert.All(f.Vm.Fields, field => Assert.Equal("saved", field.Text));
        Assert.Contains("verification or display failed", f.Vm.Message); Assert.False(f.Vm.EditCommand.CanExecute(null));
        f.Data.FailReadAfterAppend = false; f.Dialogs.ConfirmationResult = true; await f.Vm.RefreshCommand.ExecuteAsync(null);
        Assert.True(f.Vm.EditCommand.CanExecute(null)); Assert.Equal(1, f.Vm.DisplayedRevision!.Revision);
    }

    [Fact]
    public async Task MissingCurrentAfterCommitCannotClaimSuccessfulDisplayOrDiscardEdits()
    {
        var f = new PlaybookFixture(); await f.OpenAsync(); f.Edit("retained"); f.Data.MissingCurrentAfterAppend = true;
        await f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.Single(f.Data.Revisions); Assert.True(f.Vm.IsEditing);
        Assert.All(f.Vm.Fields, field => Assert.Equal("retained", field.Text));
        Assert.Null(f.Vm.DisplayedRevision);
        Assert.Contains("current definition could not be verified", f.Vm.Message);
        Assert.DoesNotContain("Revision 1 saved.", f.Vm.Message);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DelayedReadbackProtectsSelectionAndFieldsAndRequiresVerifiedResult(bool fail)
    {
        var f = new PlaybookFixture(); f.Seed(1); await f.OpenAsync(); f.Edit("PRIVATE entered text");
        var readback = new TaskCompletionSource<TradingSetupPlaybookRevision?>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Data.RevisionRead = readback.Task;
        var saving = f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(2, f.Data.Revisions.Count); Assert.True(f.Vm.IsEditing); Assert.True(f.Vm.IsWriting);
        Assert.False(f.Vm.RefreshCommand.CanExecute(null)); Assert.False(f.Vm.SaveCommand.CanExecute(null));
        await f.Vm.RefreshCommand.ExecuteAsync(null); // Direct command invocation still obeys the guard.
        Assert.False(f.Vm.TrySelectSetup(f.Data.Row(f.Other)));
        Assert.DoesNotContain("saved.", f.Vm.Message);
        if (fail) readback.SetException(new IOException("PRIVATE path"));
        else readback.SetResult(f.Data.Revisions[1]);
        await saving;
        Assert.Equal(f.Setup.Id, f.Vm.SelectedSetup!.Id); Assert.Equal(fail, f.Vm.IsEditing);
        Assert.All(f.Vm.Fields, field => Assert.Equal("PRIVATE entered text", field.Text));
        Assert.DoesNotContain("PRIVATE", f.Vm.Message);
        Assert.Contains(fail ? "verification or display failed" : "Playbook saved and verified.", f.Vm.Message);
        Assert.Equal(fail ? 1 : 0, f.Vm.EditorExamples.Count);
    }
}

internal sealed class PlaybookFixture
{
    internal readonly PlaybookData Data = new();
    internal readonly FakeDialogService Dialogs = new();
    internal readonly TradingSetup Setup = new("NQ pullback", "Existing catalog", FixedTimeProvider.FixedUtcNow.AddDays(-1));
    internal readonly TradingSetup Other = new("NQ range", null, FixedTimeProvider.FixedUtcNow.AddDays(-1));
    internal PlaybookViewModel Vm { get; }
    internal PlaybookFixture(PersonalTradingJournal.Desktop.Screenshots.IPlaybookExampleImages? images = null, IPlaybookImageStorage? storage = null,
        PlaybookSaveDiagnostics? diagnostics = null, IPlaybookRevisionDeletion? deletion = null)
    {
        Data.Setups.AddRange([Setup, Other]);
        var save = new SaveTradingSetupPlaybookUseCase(Data, Data, new FixedTimeProvider());
        Vm = new(Data, Data, Data, save, new(Data, new FixedTimeProvider()), Dialogs, images,
            storage is null ? null : new SavePlaybookExamplesUseCase(save, storage), diagnostics, deletion);
    }
    internal async Task OpenAsync() { await Vm.ActivateAsync(); Vm.TrySelectSetup(Data.Row(Setup)); await Vm.LoadTask; }
    internal void Edit(string text) { Vm.EditCommand.Execute(null); foreach (var f in Vm.Fields) f.Text = text; }
    internal void Seed(int count)
    {
        while (Data.Revisions.Count < count) Data.Revisions.Add(TradingSetupPlaybookRevision.Rehydrate(Setup.Id, Data.Revisions.Count + 1,
            Setup.Name, Setup.Description, new("context", "entry", "confirm", "invalidate", "execute", [new(Guid.NewGuid(), "Synthetic annotation")]), FixedTimeProvider.FixedUtcNow));
    }
    internal static string[] Texts(PlaybookGuidance g) => [g.MarketContext, g.EntryConditions, g.Confirmations, g.Invalidation, g.ExecutionGuidance];
}

internal sealed class PlaybookData : ITradingSetupStore, ITradingSetupReader, ITradingSetupPlaybookStore, ITradingSetupPlaybookHistoryReader
{
    internal List<TradingSetup> Setups { get; } = [];
    internal List<TradingSetupPlaybookRevision> Revisions { get; } = [];
    internal List<int> Limits { get; } = [];
    internal int AppendCount;
    internal string? WriteFailure;
    internal bool BlockWrite;
    internal bool FailReadAfterAppend;
    internal bool MissingCurrentAfterAppend;
    internal Task<TradingSetupPlaybookRevision?>? RevisionRead;
    internal Task<TradingSetupPlaybookRevision?>? NextRead;
    internal TradingSetupListItem Row(TradingSetup s) => new(s.Id, s.Name, s.Description, s.IsActive, s.CreatedAtUtc, s.UpdatedAtUtc);
    public Task<IReadOnlyList<TradingSetupListItem>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TradingSetupListItem>>(Setups.Select(Row).ToArray());
    Task<TradingSetupDetails?> ITradingSetupReader.GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<TradingSetupDetails?>(null);
    public Task<TradingSetup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Setups.SingleOrDefault(s => s.Id == id));
    public Task AddAsync(TradingSetup s, CancellationToken cancellationToken = default) { Setups.Add(s); return Task.CompletedTask; }
    public Task UpdateAsync(TradingSetup s, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<TradingSetupPlaybookRevision?> GetLatestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (RevisionRead is { } readback && AppendCount > 0) { RevisionRead = null; return readback; }
        if (FailReadAfterAppend && AppendCount > 0) throw new IOException("SECRET read failure");
        if (MissingCurrentAfterAppend && AppendCount > 0) return Task.FromResult<TradingSetupPlaybookRevision?>(null);
        if (NextRead is { } next) { NextRead = null; return next; }
        return Task.FromResult(Revisions.LastOrDefault(r => r.TradingSetupId == id));
    }
    public async Task<bool> TryAppendAsync(TradingSetupPlaybookRevision revision, int expectedRevision, DateTimeOffset expectedSetupUpdatedAtUtc, CancellationToken cancellationToken = default)
    {
        AppendCount++;
        if (BlockWrite) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        if (WriteFailure == "failure") throw new IOException("SECRET storage path");
        if (WriteFailure == "cancel") throw new OperationCanceledException();
        if (WriteFailure == "conflict" || (Revisions.LastOrDefault()?.Revision ?? 0) != expectedRevision) return false;
        Revisions.Add(revision); return true;
    }
    public Task<TradingSetupPlaybookRevision?> GetRevisionAsync(Guid id, int revision, CancellationToken cancellationToken = default) => RevisionRead ?? Task.FromResult(Revisions.SingleOrDefault(r => r.TradingSetupId == id && r.Revision == revision));
    public Task<IReadOnlyList<TradingSetupPlaybookRevision>> GetHistoryAsync(Guid id, int? beforeRevision, int limit, CancellationToken cancellationToken = default)
    {
        Limits.Add(limit);
        return Task.FromResult<IReadOnlyList<TradingSetupPlaybookRevision>>(Revisions.Where(r => r.TradingSetupId == id && (!beforeRevision.HasValue || r.Revision < beforeRevision)).OrderByDescending(r => r.Revision).Take(limit).ToArray());
    }
}
