# Daily Journal — M14.1–M14.4

## Scope and identity

A `DailyJournalEntry` stores optional freeform text and three Daily Review answers for **one explicit New York calendar/trading date and one exact Account scope**. The caller supplies `DateOnly TradingDate`; neither UTC audit time, machine timezone, linked Trades nor import TradeDay determines it. Both DST transition dates and days with zero Trades are valid, including for completion. Journals have no Trade dependency or currency scope and do not alter Calendar/Dashboard P&L.

`TradingAccountId == null` means the independent **All accounts journal**. It is not a wildcard, fallback, sum or concatenation of account journals. The same date may have one global entry and one per Account. `Guid.Empty` is invalid. The stable journal ID, date and Account scope cannot be changed by update. There is no delete, scope reassignment or merge operation in this milestone.

Existing inactive Accounts permit create/read/update: historical review is not new trading activity. Missing Accounts reject creates/updates with `AccountUnavailable`, never retarget to null or another Account. Reads retain an orphaned historical entry and identify its scope as `Unavailable` if references were damaged outside the application. Account names/activity are current reference metadata, not revision contents. A restrictive foreign key normally prevents deleting a referenced Account; the existing Account delete workflow reports it as referenced and recommends deactivation.

## Text, draft and audits

- `Text` preserves exact plain text, including whitespace, Unicode and line endings. Empty text is valid; null is not. The limit is **100,000 UTF-16 code units**, enforced by Domain validation without truncation.
- `Review` is an immutable `DailyReviewAnswers` value with `WentWell`, `NeedsImprovement`, and `NextTradingDay`. Each exact answer permits empty content and has a **100,000 UTF-16 code-unit** limit, separately from freeform text. No answer is trimmed or truncated. Partially answered drafts are valid.
- `IsDraft` defaults to true. **Complete review** is explicit: all three answers must each contain at least one Unicode letter or digit. Whitespace, punctuation, emoji or invisible format characters alone are insufficient; this is a content-presence check, not an assessment of answer quality. Freeform text is optional. Completed content is locked until an explicit **Reopen review** creates a draft revision with unchanged content.
- `CreatedAtUtc` and `UpdatedAtUtc` are supplied by the repository's `TimeProvider` in canonical UTC. Updates cannot move audit time backward. They do not change the selected New York date.
- Revision starts at **1**. Each actual text, answer or state change increments the positive 64-bit revision. Saving changed answers, completing and reopening each produce a complete durable snapshot. Equal content/state with the current expected revision returns `Unchanged`, preserving audit time and history. Equal timestamps remain valid; revision, not timestamp, is the concurrency token. Overflow fails without changes.

## Application boundary

`Application.Journals.IDailyJournalRepository` is the single purpose-specific persistence boundary, registered by `AddPersistence`:

| Operation | Contract |
| --- | --- |
| `GetAsync(date, accountId?, ct)` | Exact-scope disconnected entry plus `AllAccounts`, `Active`, `Inactive` or `Unavailable` Account state and readable name; null if no entry. Never falls back to another scope. |
| `GetHistoryAsync(journalId, ct)` | Full committed snapshots ordered by revision ascending, including initial and current revisions; empty for a missing valid ID. |
| `CreateAsync(command, ct)` | `Created`, `AlreadyExists` or `AccountUnavailable`. Duplicate creation never overwrites the existing entry. |
| `UpdateAsync(command, ct)` | Required journal ID and positive `ExpectedRevision`; `Updated`, `Unchanged`, `NotFound`, `Conflict` or `AccountUnavailable`. Stale submissions conflict even if their proposed text happens to match current text. |

Write results contain the authoritative disconnected journal when available. A conflict requires rereading/reconciling the newer revision, not automatic resubmission or last-write-wins. Invalid identifiers/revision and Domain-invalid content throw argument exceptions; cancellation propagates `OperationCanceledException`. Unexpected database failures propagate without disguising a failed save as success. No customer journal text is logged by this path.

Create/update commands carry `Review` with the exact answers. For compatibility, an omitted create review means empty answers; an omitted update review retains the current answers. History reads always include the stored answers. `IsDraft=false` explicitly requests completion; `IsDraft=true` on a completed entry permits only an exact-content reopen. Combining an edit with that reopen is rejected: reopen must commit first. The expected revision is checked before changing content or state, including same-content, completion and reopen requests.

## SQLite persistence and safety

Additive migration `20261004145757_AddDailyJournals` creates only:

- `DailyJournals`: current root, explicit date/nullable Account, text, draft flag, revision and UTC audits.
- `DailyJournalRevisions`: complete text/draft/audit snapshot keyed by `(JournalId, Revision)`. Every successful creation/update appends exactly one snapshot. Reads never reconstruct history from mutable current content. No repository API changes or removes an old snapshot.

SQLite allows multiple NULLs in an ordinary composite unique index. Two filtered unique indexes enforce **date+Account for non-null Accounts** and **date alone for null Accounts**. Both revisions and root use positive-revision checks; the root revision is also an EF concurrency token. Account deletion and journal deletion are restricted by their foreign keys, preserving scope and history. Direct external SQL tampering is not an audit-security boundary.

Writes use a fresh context and SQLite writer transaction **before** checking the key, Account and expected revision. Head and snapshot are saved and committed together. A concurrent writer waits for the transaction, then sees the newer key/revision; it cannot silently overwrite it. There are no application retries. Cancellation or a save/commit failure rolls back uncommitted head and history changes. No-op, conflict, duplicate or missing-reference outcomes write nothing.

Reads use fresh no-tracking contexts: one exact-key query with Account left join, or one journal-scoped ordered history query. They are cancellable, do not load Trades and perform no writes. Existing Trades, executions, import ledgers and analytics projections are untouched. Migration generation/model checks use the in-memory design-time factory, not the real journal.

Additive migration **`20261004165044_AddDailyJournalReviewAnswers`** adds `WentWell`, `NeedsImprovement` and `NextTradingDay` as non-null TEXT columns with empty-string defaults to **both** tables. Existing text, scope, state, audit timestamps and revision numbers are preserved; old history remains intact and reads empty answers. A legacy `IsDraft=false` record may predate the questions: it is retained read-only as stored, can be reopened, and any new completion must satisfy the current three-answer rule. Migration does not retroactively create answers or relabel past revisions as having passed this rule. Downgrading removes only the six answer columns and therefore discards their content; it is not part of the application workflow.

## Desktop editor (M14.2)

The shell's **Journal** destination hosts `JournalViewModel` and `JournalView`. The initial date is today's New York calendar date. An explicit date picker and Account selector identify the exact entry; All accounts is a separate option, and days without Trades are valid. Date and Account are retained on re-entry, which rereads committed content. The text editor depends on `IDailyJournalRepository`, `ITradingAccountReader` and the Desktop dialog boundary; it never queries EF Core. The independent M14.3 context below it reads Trades through the existing Calendar Application boundary.

**Save draft** or **Ctrl+S** explicitly creates or updates the selected draft. There is no autosave, save on blur, or automatic save during navigation. The editor passes text and answers without trimming or truncation, including empty text, whitespace and Unicode. It shows a character count, unsaved/saved state and the saved revision. Over-limit content remains visible while saving/completion is blocked. The date picker uses shared Light/Dark calendar resources and culture-aware typed dates; invalid or uncommitted date input blocks writes against the old bound date. Tab leaves each multiline editor instead of inserting a tab.

Changing date/Account, choosing **Reload latest**, navigating to another page or closing the main window checks for unsaved text **or any answer**. **Keep editing** retains the current scope, all edits and page; **Discard changes** permits the requested transition. Clicking Journal while already there preserves the draft. A save, completion or reopen in progress blocks scope changes, page navigation and window close; **Cancel operation** requests cancellation and retains local content if the write is cancelled. A committed result is still accepted if cancellation arrived after the write committed.

Each load captures its date/Account and rejects late results after scope changes, cancellation or deactivation. Loading/saving state, safe error feedback and retry controls are explicit. Load and save failures keep the local text. A stale update, duplicate creation or missing entry does not overwrite the stored journal or silently replace the local draft: Save is blocked until an explicit successful **Reload latest**. Reload asks before discarding dirty text, then reads the authoritative entry and revision. There is no automatic merge or forced overwrite.

Inactive Accounts remain named, marked inactive and selectable for historical journals. An unavailable selected Account stays selected with its original ID and an unavailable label; the editor blocks writes and never falls back to All accounts. New journals start as drafts, including partial answers. Revision history is persisted but has no browsing UI in this milestone.

## Daily Review and completion (M14.4)

The Journal page shows these questions with multiline, accessible answer controls:

1. **What went well?**
2. **What needs improvement?**
3. **What will I do differently next trading day?**

**Complete review** validates all three answers and atomically saves the current optional freeform text and exact answers with completed state. Invalid completion names the unanswered questions and retains every local value. A first completion may create revision one directly; completing an existing draft creates its next revision. Neither requires any closed Trades or a successful Trade-context query.

Completed text and answers remain enabled for reading/copying but are read-only. **Save draft** and **Complete review** are replaced by **Reopen review**. Reopening uses the loaded revision and unchanged content, creates a new draft snapshot, and enables editing only after success. A failed/cancelled/stale reopen stays completed. Changing scope remains a separate selection operation and never moves the journal's stored scope.

All three write actions share one submission guard and cancellation path. Failures preserve local text/answers/state. A stale completion or reopen uses the same explicit **Reload latest** recovery as a stale save, with a discard prompt if local content is dirty. Successful Trade-context refresh never reloads or overwrites these edits. Completion is a user action, not a claim that the app evaluated the answers or trading quality.

Calendar's **Day Journal / Add Journal** remains disabled until M14.5. The standalone editor does not change Calendar/Dashboard calculations, Trades, executions or import records.

## Read-only Trade context (M14.3)

`JournalTradeContextViewModel` composes `ITradingCalendarDayReader` and `ITradingAccountReader`. It receives only the editor's **applied** date and exact Account scope; a rejected dirty-scope change or an uncommitted typed date cannot retarget it. A null Account filters no Trades: every Account contributes, but that does not aggregate or change the independent All accounts journal text. Inactive Accounts remain supported. An unavailable selected Account produces an explicit context error, never an All accounts fallback. There is no currency filter or Trade editing/navigation in this milestone.

`TradingCalendarDayQuery` reuses the existing New York midnight-to-next-midnight **half-open UTC range** over authoritative `ClosedAtUtc`, including 23-/25-hour DST days. The reader excludes open/partially exited Trades, has no browse-page limit, and returns newest closure first with Trade ID as the stable tie-breaker. Its fixed set of batch queries reads browse rows, peak execution/allocation Size, Setups and Mistakes; there is no per-Trade query. Account/Instrument activity metadata is carried in `TradingCalendarDayDetails.References` from the same left joins. Missing references are retained as unavailable; inactive and unassigned classification states remain explicit. The additional metadata is read-only and requires no schema change.

The panel displays daily closed-Trade count, separate per-currency Effective Net summaries, and each Trade's New York closing time, Instrument, Account, Net, Size, direction, Setup and Mistakes. It reuses `CalendarTradePresentation` and `CalendarPnlSummary`; it never recomputes economics from prices. Known Net remains authoritative; unknown Net with known Gross is visibly **Estimated**; unknown economics remain **—**, not zero. A genuinely zero Trade still has a row and count. Detailed time help keeps the actual timestamp's explicit UTC offset. Full names/classifications have tooltips, long text wraps, and the table scrolls horizontally when necessary. Read-only rows are keyboard-focusable and carry complete accessible descriptions; ordinary vertical wheel input reaches the Journal page.

Context loading, cancellation, empty and error states are independent of the editor. **Refresh Trades** and confirmed Trade create/edit/delete, Calendar inline edits, and successful Tradovate/TopstepX imports reload only context while Journal is active. Noncommitted import outcomes do not generate a commit refresh. Re-entry always reads current context. Each read captures the date/Account and a generation; scope changes, newer refreshes and deactivation invalidate older responses, including readers that finish after cancellation. Obsolete rows are cleared rather than shown under another scope. Context refresh never calls the journal repository, prompts to discard text, changes draft/revision state or changes the applied scope; journal saves also do not depend on a successful Trade read.

M14.3 itself adds no Calendar Add Journal activation, review workflow, history browser, Trade mutation or economics/schema change. M14.4 adds the review workflow described above; Trade context remains read-only.

## Verification and later milestones

Focused Domain and isolated migrated-SQLite tests cover exact scope uniqueness (including direct database enforcement), empty trading dates, New York DST date identity, text/audit round trips, durable history, no-ops, stale and concurrent writes, inactive/missing Accounts, referential protection, cancellation/rollback and read-only retrieval. Migration tests verify an upgrade preserves existing trading data and a downgrade removes only the new journal tables.

M14.1 verification on 2026-10-04: **91 focused tests passed** (18 Domain, 54 persistence/schema, 19 Account presentation regressions). The complete Release suite passed **2,576 tests** (418 Domain, 516 Application, 753 Infrastructure, 889 Desktop), with no failures or skips. Release build had **zero warnings/errors**, EF reported **no pending model changes**, and `git diff --check` passed. Tests used isolated migrated SQLite databases; the real journal was not opened. Existing Desktop regression tests passing is not Journal editor or interactive acceptance.

The historical M14.1 results above do not establish later editor or interactive acceptance. M14.2 adds the explicit-save editor; M14.3 adds read-only Trade context; M14.4 adds structured review/completion. Richer text, attachments, history browsing, scope migration and journal deletion remain deferred. **Calendar Add Journal activation belongs to M14.5** and must pass the selected Calendar date/Account explicitly, including All accounts, without treating currency filtering as a different journal scope.

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

### M14.3 automated verification and manual follow-up

Verified on 2026-10-04 using synthetic data and isolated migrated SQLite databases:

- **223 focused tests passed**: 189 Desktop Journal/navigation/Calendar-day regressions and 34 Calendar-reader/Journal-persistence cases. Coverage includes exact Account scope, both DST transition days, more rows than a browse page, separate currencies, estimated/zero/unavailable economics, inactive/missing classifications and references, read-only SQLite access, and an unchanged journal/history snapshot during Trade-context reads.
- Actual persisted Trade insert, correction and deletion refresh the context while preserving exact unsaved Journal text, revision and scope. Controlled TopstepX/Tradovate confirmation flows verify committed versus NoChanges/Blocked/cancelled/failed outcomes, commit-after-presentation-cancellation, active/inactive navigation, dispatcher publication and stale-read rejection. These controlled import-notification tests are not live import acceptance.
- **2,669 complete Release tests passed**: 418 Domain, 516 Application, 753 Infrastructure and 982 Desktop; no failures or skips. Release build: **zero warnings/errors**. EF model check: **no pending changes**. `git diff --check` passed. No schema or persistence-rule change is needed.
- Compiled WPF rendering and layout checks cover the context in Light/Dark at **1,100 DIP / 96 DPI** and **480 DIP / 240 DPI**. The rendered images were inspected: wrapped historical names, aligned headers/rows, separate-currency summaries, sign/estimated/unavailable styling and narrow horizontal scrolling. The checks also verify UI Automation row peers with complete names and no edit/invoke pattern, command bindings, empty/error states and vertical wheel forwarding. Existing editor renders/regressions remain covered.

**Live interaction remains unverified**; native desktop interaction was unavailable in this session. Automated renders and routed-event/automation-peer assertions are not mouse, keyboard or screen-reader acceptance. With a disposable `--isolated-data-root`, check:

1. In both themes, Tab from the editor to Refresh Trades and through read-only rows; inspect focus, full-name/time/estimate help, horizontal navigation and vertical wheel scrolling at normal and narrow/high-DPI sizes. Check status/error announcements with a screen reader.
2. Change the applied date and Account, including an inactive Account and an empty day. Confirm the row count, classifications, New York times and separate currencies match Calendar; veto a dirty scope change and confirm both context and draft stay on the original scope.
3. Keep unsaved Journal text while a pending synthetic Trade/import commit finishes. Confirm context refreshes without losing the draft or changing scope; noncommitted outcomes must not refresh as commits. Test Refresh Trades/cancellation/error recovery independently of Save and Reload latest.

The historical M14.3 checks did not cover review questions/completion. Those arrive in M14.4; Calendar Add Journal and history navigation remain deferred.

### M14.4 automated verification and manual follow-up

Verified on 2026-10-04 using synthetic data and isolated migrated SQLite databases:

- **317 focused tests passed**: 44 Domain review/journal cases, 57 persistence/migration regressions and 216 Desktop Journal/navigation regressions. Coverage includes partial drafts, all three meaningful-answer checks, exact answer text, zero-Trade completion, completed read-only state, explicit reopen, durable history, stale save/completion/reopen, cancellation and injected rollback, dirty-answer scope/page guards, unavailable Accounts, overlapping writes and stale reads. Two editor-to-SQLite flows verify completion/reopen/history and explicit conflict recovery without losing local answers.
- The complete Release suite passed **2,740 tests**: 444 Domain, 516 Application, 766 Infrastructure and 1,014 Desktop; no failures or skips. Release build: **zero warnings/errors**. EF model consistency: **no pending changes**. `git diff --check` passed. The initial full run exposed an obsolete seven-migration assertion; it now verifies the exact configured migration list and the complete rerun passed.
- Compiled WPF layout/render checks cover draft and completed reviews in Light/Dark at **960 DIP / 96 DPI** and **480 DIP / 240 DPI**. Rendered images were inspected. Checks verify question order, accessible names, exact bindings, enabled read-only/copyable completed controls, answer-only dirty guards and reachable Complete/Reopen actions after scrolling. Trade context remains the existing read-only component.

**Live pointer, keyboard and screen-reader interaction remains unverified**; native desktop interaction was unavailable. Automated WPF rendering and routed controls are not interactive acceptance. Use a disposable `--isolated-data-root` for these remaining Windows checks:

1. In both themes and a narrow/high-DPI window, Tab through date, Account, freeform text and all three answers; check focus, scrolling, accessible question/state/error announcements and copy from completed answers.
2. On a day without Trades, save a partial draft, attempt completion, then answer all three questions and complete with freeform text empty. Confirm validation preserves every answer, completed controls are read-only, and reopening unlocks editing only after a successful write.
3. With an answer-only unsaved edit, change date/Account, navigate away, close the window and choose Reload latest. Exercise Keep editing and Discard changes; confirm the exact scope and all four content fields are retained or discarded only as chosen.
4. In two application instances using the same disposable root, save competing draft/completion/reopen revisions. Confirm the stale editor keeps local content, cannot overwrite the newer revision and asks before Reload latest discards a dirty draft. Cancel an in-flight operation and verify its final committed/cancelled state is reported correctly.
5. Refresh Trade context while answers are unsaved; confirm no text, answer or scope changes. Calendar Add Journal must remain disabled; review-history navigation is still deferred.
