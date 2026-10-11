using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Desktop.ViewModels.Setups;

namespace PersonalTradingJournal.Desktop.Tests.Setups;

public sealed class PlaybookValidationDeletionTests
{
    [Fact]
    public async Task ValidationMarksAllInvalidFieldsAndCorrectingThemClearsOnlyTheirErrors()
    {
        var f = new PlaybookFixture(); f.Seed(1); await f.OpenAsync(); f.Edit("kept");
        f.Vm.Fields[0].Text = " "; f.Vm.Fields[2].Text = ""; f.Vm.Fields[4].Text = new string('x', 4001);
        await f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(3, f.Vm.Fields.Count(x => x.IsInvalid)); Assert.Equal(1, f.Vm.ValidationAttempt);
        Assert.Contains("Market context", f.Vm.ValidationSummary); Assert.Single(f.Vm.EditorExamples);
        Assert.Equal(0, f.Data.AppendCount); Assert.True(f.Vm.IsEditing);
        f.Vm.Fields[0].Text = "fixed"; Assert.False(f.Vm.Fields[0].IsInvalid); Assert.True(f.Vm.HasValidationErrors);
        f.Vm.Fields[2].Text = "fixed"; f.Vm.Fields[4].Text = "fixed";
        Assert.False(f.Vm.HasValidationErrors); Assert.Empty(f.Vm.ValidationSummary);
        await f.Vm.SaveCommand.ExecuteAsync(null); Assert.False(f.Vm.IsEditing); Assert.Contains("Revision 2 saved.", f.Vm.Message);
    }

    [Theory]
    [InlineData(PlaybookRevisionDeletionStatus.CurrentRevision)]
    [InlineData(PlaybookRevisionDeletionStatus.Referenced)]
    [InlineData(PlaybookRevisionDeletionStatus.Missing)]
    public async Task BlockedRevisionExplainsReasonWithoutConfirmationOrWrite(PlaybookRevisionDeletionStatus status)
    {
        var deletion = new Deletion { Check = status }; var f = new PlaybookFixture(deletion: deletion);
        f.Seed(2); await f.OpenAsync(); await f.Vm.DeleteRevisionCommand.ExecuteAsync(f.Vm.Revisions[1]);
        Assert.Null(f.Dialogs.ConfirmationRequest); Assert.Equal(0, deletion.Writes);
        Assert.Contains(status == PlaybookRevisionDeletionStatus.CurrentRevision ? "current" : status == PlaybookRevisionDeletionStatus.Referenced ? "retained" : "no longer", f.Vm.Message);
    }

    [Fact]
    public async Task CancelAndConflictPreserveHistoricalSelectionAndSuccessReturnsToCurrent()
    {
        var deletion = new Deletion(); var f = new PlaybookFixture(deletion: deletion);
        f.Seed(3); await f.OpenAsync(); var selected = f.Vm.Revisions[1]; f.Vm.ViewRevisionCommand.Execute(selected);
        await f.Vm.DeleteRevisionCommand.ExecuteAsync(selected);
        Assert.Equal(0, deletion.Writes); Assert.Same(selected, f.Vm.DisplayedRevision);
        Assert.Contains("revision 2", f.Dialogs.ConfirmationRequest!.Message);
        f.Dialogs.ConfirmationResult = true; deletion.Result = PlaybookRevisionDeletionStatus.Conflict;
        await f.Vm.DeleteRevisionCommand.ExecuteAsync(selected); Assert.True(f.Vm.IsHistorical); Assert.Contains("Refresh", f.Vm.Message);
        deletion.Result = PlaybookRevisionDeletionStatus.Deleted; deletion.OnDelete = () => f.Data.Revisions.Remove(selected);
        await f.Vm.DeleteRevisionCommand.ExecuteAsync(selected);
        Assert.False(f.Vm.IsHistorical); Assert.Equal(3, f.Vm.DisplayedRevision!.Revision); Assert.Equal(new[] { 3, 1 }, f.Vm.Revisions.Select(r => r.Revision));
        Assert.Equal(f.Setup.Id, deletion.Setup); Assert.Equal(2, deletion.Revision); Assert.Equal(3, deletion.Expected);
    }

    [Fact]
    public async Task StorageFailureAndDirtyEditorNeverLoseTextOrDelete()
    {
        var deletion = new Deletion { Fail = true }; var f = new PlaybookFixture(deletion: deletion);
        f.Seed(2); await f.OpenAsync(); f.Dialogs.ConfirmationResult = true;
        await f.Vm.DeleteRevisionCommand.ExecuteAsync(f.Vm.Revisions[1]); Assert.Contains("could not", f.Vm.Message); Assert.DoesNotContain("SECRET", f.Vm.Message);
        f.Edit("preserve me"); int writes = deletion.Writes;
        Assert.False(f.Vm.DeleteRevisionCommand.CanExecute(f.Vm.Revisions[1]));
        await f.Vm.DeleteRevisionCommand.ExecuteAsync(f.Vm.Revisions[1]); Assert.Equal(writes, deletion.Writes);
        Assert.True(f.Vm.IsEditing); Assert.All(f.Vm.Fields, field => Assert.Equal("preserve me", field.Text)); Assert.Single(f.Vm.EditorExamples);
    }

    internal sealed class Deletion : IPlaybookRevisionDeletion
    {
        internal PlaybookRevisionDeletionStatus Check = PlaybookRevisionDeletionStatus.Eligible, Result = PlaybookRevisionDeletionStatus.Deleted;
        internal int Writes, Revision, Expected; internal Guid Setup; internal bool Fail; internal Action? OnDelete;
        public Task<PlaybookRevisionDeletionCheck> CheckAsync(Guid id, int revision, CancellationToken token = default) => Task.FromResult(new PlaybookRevisionDeletionCheck(Check, 3));
        public Task<PlaybookRevisionDeletionStatus> DeleteAsync(Guid id, int revision, int expected, CancellationToken token = default)
        { Writes++; Setup = id; Revision = revision; Expected = expected; if (Fail) throw new IOException("SECRET path"); OnDelete?.Invoke(); return Task.FromResult(Result); }
    }
}
