namespace PersonalTradingJournal.Application.Journals;

/// <summary>Permanent removal of an older snapshot, never the current revision or journal root.</summary>
public interface IDailyJournalRevisionWriter
{
    Task<DeleteJournalRevisionStatus> DeleteRevisionAsync(DeleteJournalRevisionCommand command,
        CancellationToken cancellationToken = default);
}

/// <param name="ExpectedCurrentRevision">The journal root's optimistic concurrency token, not the snapshot number.</param>
public sealed record DeleteJournalRevisionCommand(Guid JournalId, long Revision, long ExpectedCurrentRevision);
public enum DeleteJournalRevisionStatus { Deleted, NotFound, Conflict, CurrentRevisionProtected }
