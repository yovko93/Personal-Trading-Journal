namespace PersonalTradingJournal.Application.Journals;

/// <summary>
/// Persists disconnected journals and their content-immutable revision history.
/// Older snapshots may be explicitly removed through IDailyJournalRevisionWriter; current snapshots are protected.
/// Null Account scope selects only the all-accounts journal, never Account journals.
/// </summary>
public interface IDailyJournalRepository
{
    /// <summary>Atomically removes the entry and all revision snapshots, without retargeting its scope.
    /// Inactive/unavailable accounts do not prevent removal of an existing entry by ID.</summary>
    Task<DailyJournalWriteResult> DeleteAsync(DeleteDailyJournalCommand command, CancellationToken cancellationToken = default);

    Task<DailyJournalDetails?> GetAsync(
        DateOnly tradingDate,
        Guid? tradingAccountId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns remaining committed snapshots in ascending revision order, including the current revision. Numbers may have gaps.</summary>
    Task<IReadOnlyList<DailyJournalRevision>> GetHistoryAsync(
        Guid journalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates revision one atomically with its history snapshot.
    /// An occupied date/Account scope returns AlreadyExists without replacing content.
    /// Existing inactive Accounts remain valid for historical journal work.
    /// </summary>
    Task<DailyJournalWriteResult> CreateAsync(
        CreateDailyJournalCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Requires the current positive ExpectedRevision, including for same-content submissions.
    /// A real change and its snapshot commit atomically; unchanged content creates no history.
    /// Stale submissions return Conflict and unavailable Account references block writes.
    /// </summary>
    Task<DailyJournalWriteResult> UpdateAsync(
        UpdateDailyJournalCommand command,
        CancellationToken cancellationToken = default);
}
