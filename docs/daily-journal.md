# Daily Journal — M14.1–M14.2

## Scope and identity

A `DailyJournalEntry` is plain text for **one explicit New York calendar/trading date and one exact Account scope**. The caller supplies `DateOnly TradingDate`; neither UTC audit time, machine timezone, linked Trades nor import TradeDay determines it. Both DST transition dates and days with zero Trades are valid. Journals have no Trade dependency or currency scope and do not alter Calendar/Dashboard P&L.

`TradingAccountId == null` means the independent **All accounts journal**. It is not a wildcard, fallback, sum or concatenation of account journals. The same date may have one global entry and one per Account. `Guid.Empty` is invalid. The stable journal ID, date and Account scope cannot be changed by update. There is no delete, scope reassignment or merge operation in this milestone.

Existing inactive Accounts permit create/read/update: historical review is not new trading activity. Missing Accounts reject creates/updates with `AccountUnavailable`, never retarget to null or another Account. Reads retain an orphaned historical entry and identify its scope as `Unavailable` if references were damaged outside the application. Account names/activity are current reference metadata, not revision contents. A restrictive foreign key normally prevents deleting a referenced Account; the existing Account delete workflow reports it as referenced and recommends deactivation.

## Text, draft and audits

- `Text` preserves exact plain text, including whitespace, Unicode and line endings. Empty text is valid; null is not. The limit is **100,000 UTF-16 code units**, enforced by Domain validation without truncation.
- `IsDraft` defaults to true. M14.1 stores this flag; the M14.2 editor preserves it on every text update and creates new entries as drafts. There is no completion toggle, review questions, completion criteria or locking. False is not a claim that later review requirements were met.
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

## Desktop editor (M14.2)

The shell's **Journal** destination hosts `JournalViewModel` and `JournalView`. The initial date is today's New York calendar date. An explicit date picker and Account selector identify the exact entry; All accounts is a separate option, and days without Trades are valid. Date and Account are retained on re-entry, which rereads committed content. The editor depends on `IDailyJournalRepository`, `ITradingAccountReader` and the Desktop dialog boundary; it never queries EF Core or Trade data.

**Save** or **Ctrl+S** explicitly creates or updates the selected entry. There is no autosave, save on blur, or automatic save during navigation. The editor passes plain text without trimming or truncation, including empty text, whitespace and Unicode. It shows a character count, unsaved/saved state and the saved revision. Text beyond 100,000 UTF-16 code units remains visible while Save is blocked. The date picker uses shared Light/Dark calendar resources and culture-aware typed dates; invalid or uncommitted date input blocks Save against the old bound date. Tab leaves the multiline editor instead of inserting a tab.

Changing date/Account, choosing **Reload latest**, navigating to another page or closing the main window checks for unsaved text. **Keep editing** retains the current scope, text and page; **Discard changes** permits the requested transition. Clicking Journal while already there preserves the draft. A save in progress blocks scope changes, page navigation and window close; **Cancel operation** requests cancellation and retains local text if the save is cancelled. A committed result is still accepted if cancellation arrived after the write committed.

Each load captures its date/Account and rejects late results after scope changes, cancellation or deactivation. Loading/saving state, safe error feedback and retry controls are explicit. Load and save failures keep the local text. A stale update, duplicate creation or missing entry does not overwrite the stored journal or silently replace the local draft: Save is blocked until an explicit successful **Reload latest**. Reload asks before discarding dirty text, then reads the authoritative entry and revision. There is no automatic merge or forced overwrite.

Inactive Accounts remain named, marked inactive and selectable for historical journals. An unavailable selected Account stays selected with its original ID and an unavailable label; the editor blocks writes and never falls back to All accounts. New journals remain drafts and existing draft state is preserved. Revision history is persisted but has no browsing UI in this milestone.

Calendar's **Day Journal / Add Journal** remains disabled until M14.5. The standalone editor does not change Calendar/Dashboard calculations, Trades, executions or import records.

## Verification and later milestones

Focused Domain and isolated migrated-SQLite tests cover exact scope uniqueness (including direct database enforcement), empty trading dates, New York DST date identity, text/audit round trips, durable history, no-ops, stale and concurrent writes, inactive/missing Accounts, referential protection, cancellation/rollback and read-only retrieval. Migration tests verify an upgrade preserves existing trading data and a downgrade removes only the new journal tables.

M14.1 verification on 2026-10-04: **91 focused tests passed** (18 Domain, 54 persistence/schema, 19 Account presentation regressions). The complete Release suite passed **2,576 tests** (418 Domain, 516 Application, 753 Infrastructure, 889 Desktop), with no failures or skips. Release build had **zero warnings/errors**, EF reported **no pending model changes**, and `git diff --check` passed. Tests used isolated migrated SQLite databases; the real journal was not opened. Existing Desktop regression tests passing is not Journal editor or interactive acceptance.

The historical M14.1 results above do not establish M14.2 editor or interactive acceptance. The M14.2 implementation adds the explicit-save editor and conflict/reload presentation described here; it does not add autosave. Review questions, completion workflow, richer text, attachments, history browsing, scope migration and journal deletion remain deferred. **Calendar Add Journal activation belongs to M14.5** and must pass the selected Calendar date/Account explicitly, including All accounts, without treating currency filtering as a different journal scope.

### M14.2 automated verification and manual follow-up

Verified on 2026-10-04 with synthetic data only:

- **142 focused Desktop tests passed**, including 38 Journal ViewModel cases, three migrated-SQLite editor tests, compiled WPF checks and 100 navigation regressions. Coverage includes exact text, explicit create/edit, revision conflicts, cancelled/failed operations, inactive/unavailable Accounts, dirty-scope veto, late reads and save single-flight behavior.
- The complete Release suite passed **2,627 tests**: 418 Domain, 516 Application, 753 Infrastructure and 940 Desktop; no failures/skips. Release build: zero warnings/errors. EF model consistency: no pending changes. No migration or persistence rule changed in M14.2.
- Automated WPF rendering covered Light/Dark at **960-DIP width / 96 DPI** and **480-DIP width / 240 DPI**. These rendered images were inspected. The compiled view checks both culture-aware typed-date validation and rejected scope rollback, exact text binding, themed calendar selection, reachable Save after scrolling, command bindings and accessible labels. The WPF checks run on one STA/Application inside a supervised isolated child process, not against the real journal.

These results are **not live interactive acceptance**. Remaining Windows checklist, using `--isolated-data-root` with a disposable directory:

1. Open Journal in each theme; use Tab, date-calendar navigation and Account selection. Confirm readable focus, wrapping/scrolling at narrow/high-DPI sizes, and screen-reader labels/status/error feedback.
2. Save a multiline empty-day draft with Ctrl+S, leave and return, edit and save again. Check exact text and revision feedback; verify All accounts and an inactive Account remain separate.
3. With dirty text, change date/Account, navigate away, close the window and choose Reload latest. Exercise both Keep editing and Discard changes. Invalid typed dates must block Save.
4. In two instances sharing the same isolated test root, save competing revisions. Confirm the losing editor keeps its text, cannot overwrite the newer revision, and asks before Reload latest discards it.
5. Confirm Calendar's Add Journal remains disabled with Coming later guidance.
