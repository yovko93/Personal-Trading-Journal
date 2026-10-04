# Daily Journal — M14.1

## Scope and identity

A `DailyJournalEntry` is plain text for **one explicit New York calendar/trading date and one exact Account scope**. The caller supplies `DateOnly TradingDate`; neither UTC audit time, machine timezone, linked Trades nor import TradeDay determines it. Both DST transition dates and days with zero Trades are valid. Journals have no Trade dependency or currency scope and do not alter Calendar/Dashboard P&L.

`TradingAccountId == null` means the independent **All accounts journal**. It is not a wildcard, fallback, sum or concatenation of account journals. The same date may have one global entry and one per Account. `Guid.Empty` is invalid. The stable journal ID, date and Account scope cannot be changed by update. There is no delete, scope reassignment or merge operation in this milestone.

Existing inactive Accounts permit create/read/update: historical review is not new trading activity. Missing Accounts reject creates/updates with `AccountUnavailable`, never retarget to null or another Account. Reads retain an orphaned historical entry and identify its scope as `Unavailable` if references were damaged outside the application. Account names/activity are current reference metadata, not revision contents. A restrictive foreign key normally prevents deleting a referenced Account; the existing Account delete workflow reports it as referenced and recommends deactivation.

## Text, draft and audits

- `Text` preserves exact plain text, including whitespace, Unicode and line endings. Empty text is valid; null is not. The limit is **100,000 UTF-16 code units**, enforced by Domain validation without truncation.
- `IsDraft` defaults to true. M14.1 stores this flag but does not implement review questions, completion criteria, locking or a completion workflow. False is not a claim that later review requirements were met.
- `CreatedAtUtc` and `UpdatedAtUtc` are supplied by the repository's `TimeProvider` in canonical UTC. Updates cannot move audit time backward. They do not change the selected New York date.
- Revision starts at **1**. Each actual text or draft-state change increments the positive 64-bit revision. Equal content/state with the current expected revision returns `Unchanged`, preserving audit time and history. Equal timestamps remain valid; revision, not timestamp, is the concurrency token. Overflow fails without changes.

## Application boundary

`Application.Journals.IDailyJournalRepository` is the single purpose-specific persistence boundary, registered by `AddPersistence`:

| Operation | Contract |
| --- | --- |
| `GetAsync(date, accountId?, ct)` | Exact-scope disconnected entry plus `AllAccounts`, `Active`, `Inactive` or `Unavailable` Account state and readable name; null if no entry. Never falls back to another scope. |
| `GetHistoryAsync(journalId, ct)` | Full committed snapshots ordered by revision ascending, including initial and current revisions; empty for a missing valid ID. |
| `CreateAsync(command, ct)` | `Created`, `AlreadyExists` or `AccountUnavailable`. Duplicate creation never overwrites the existing entry. |
| `UpdateAsync(command, ct)` | Required journal ID and positive `ExpectedRevision`; `Updated`, `Unchanged`, `NotFound`, `Conflict` or `AccountUnavailable`. Stale submissions conflict even if their proposed text happens to match current text. |

Write results contain the authoritative disconnected journal when available. A conflict requires rereading/reconciling the newer revision, not automatic resubmission or last-write-wins. Invalid identifiers/revision and Domain-invalid content throw argument exceptions; cancellation propagates `OperationCanceledException`. Unexpected database failures propagate without disguising a failed save as success. No customer journal text is logged by this path.

## SQLite persistence and safety

Additive migration `20261004145757_AddDailyJournals` creates only:

- `DailyJournals`: current root, explicit date/nullable Account, text, draft flag, revision and UTC audits.
- `DailyJournalRevisions`: complete text/draft/audit snapshot keyed by `(JournalId, Revision)`. Every successful creation/update appends exactly one snapshot. Reads never reconstruct history from mutable current content. No repository API changes or removes an old snapshot.

SQLite allows multiple NULLs in an ordinary composite unique index. Two filtered unique indexes enforce **date+Account for non-null Accounts** and **date alone for null Accounts**. Both revisions and root use positive-revision checks; the root revision is also an EF concurrency token. Account deletion and journal deletion are restricted by their foreign keys, preserving scope and history. Direct external SQL tampering is not an audit-security boundary.

Writes use a fresh context and SQLite writer transaction **before** checking the key, Account and expected revision. Head and snapshot are saved and committed together. A concurrent writer waits for the transaction, then sees the newer key/revision; it cannot silently overwrite it. There are no application retries. Cancellation or a save/commit failure rolls back uncommitted head and history changes. No-op, conflict, duplicate or missing-reference outcomes write nothing.

Reads use fresh no-tracking contexts: one exact-key query with Account left join, or one journal-scoped ordered history query. They are cancellable, do not load Trades and perform no writes. Existing Trades, executions, import ledgers and analytics projections are untouched. Migration generation/model checks use the in-memory design-time factory, not the real journal.

## Verification and later milestones

Focused Domain and isolated migrated-SQLite tests cover exact scope uniqueness (including direct database enforcement), empty trading dates, New York DST date identity, text/audit round trips, durable history, no-ops, stale and concurrent writes, inactive/missing Accounts, referential protection, cancellation/rollback and read-only retrieval. Migration tests verify an upgrade preserves existing trading data and a downgrade removes only the new journal tables.

M14.1 verification on 2026-10-04: **91 focused tests passed** (18 Domain, 54 persistence/schema, 19 Account presentation regressions). The complete Release suite passed **2,576 tests** (418 Domain, 516 Application, 753 Infrastructure, 889 Desktop), with no failures or skips. Release build had **zero warnings/errors**, EF reported **no pending model changes**, and `git diff --check` passed. Tests used isolated migrated SQLite databases; the real journal was not opened. Existing Desktop regression tests passing is not Journal editor or interactive acceptance.

**M14.2–M14.5 remain separate:** editor and autosave UX, Calendar Add Journal activation, review questions, completion workflow and their acceptance. Conflict-resolution presentation, richer text, attachments, scope migration and journal deletion are not defined or implemented here. Future UI must pass the selected Calendar date/Account explicitly (including All accounts) and must not treat currency filtering as a different journal. The current Day Journal button remains disabled.
