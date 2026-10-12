namespace PersonalTradingJournal.Application.Setups;

public enum PlaybookRevisionDeletionStatus { Eligible, Deleted, CurrentRevision, Referenced, Missing, Conflict }
public sealed record PlaybookRevisionDeletionCheck(PlaybookRevisionDeletionStatus Status, int CurrentRevision);
/// <summary>Confirmation preview is read-only; deletion rechecks references and expected current revision under a writer reservation.</summary>
public interface IPlaybookRevisionDeletion
{
    Task<PlaybookRevisionDeletionCheck> CheckAsync(Guid setupId, int revision, CancellationToken token = default);
    Task<PlaybookRevisionDeletionStatus> DeleteAsync(Guid setupId, int revision, int expectedCurrentRevision, CancellationToken token = default);
}
