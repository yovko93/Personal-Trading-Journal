# Daily Journal — M14.1–M14.6

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

`Application.Journals.IDailyJournalRepository` is the write/entry persistence boundary, registered by `AddPersistence`. M14.6 adds a separate bounded, read-only history reader; the legacy full-history method below is not used by Desktop browsing:

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

Inactive Accounts remain named, marked inactive and selectable for historical journals. An unavailable selected Account stays selected with its original ID and an unavailable label; the editor blocks writes and never falls back to All accounts. New journals start as drafts, including partial answers. M14.6 adds the bounded history browser described below.

## Daily Review and completion (M14.4)

The Journal page shows these questions with multiline, accessible answer controls:

1. **What went well?**
2. **What needs improvement?**
3. **What will I do differently next trading day?**

**Complete review** validates all three answers and atomically saves the current optional freeform text and exact answers with completed state. Invalid completion names the unanswered questions and retains every local value. A first completion may create revision one directly; completing an existing draft creates its next revision. Neither requires any closed Trades or a successful Trade-context query.

Completed text and answers remain enabled for reading/copying but are read-only. **Save draft** and **Complete review** are replaced by **Reopen review**. Reopening uses the loaded revision and unchanged content, creates a new draft snapshot, and enables editing only after success. A failed/cancelled/stale reopen stays completed. Changing scope remains a separate selection operation and never moves the journal's stored scope.

All three write actions share one submission guard and cancellation path. Failures preserve local text/answers/state. A stale completion or reopen uses the same explicit **Reload latest** recovery as a stale save, with a discard prompt if local content is dirty. Successful Trade-context refresh never reloads or overwrites these edits. Completion is a user action, not a claim that the app evaluated the answers or trading quality.

M14.5 connects Calendar's **Day Journal** action to this editor as described below. Journal operations do not change Calendar/Dashboard calculations, Trades, executions or import records.

## Calendar integration (M14.5)

`IDailyJournalStatusReader.GetAsync(from, through, accountId?, ct)` reads committed status for an inclusive visible-grid range of at most **42 dates**. A null Account selects **only** explicit All accounts journals. One fresh no-tracking query projects journal ID, trading date, `IsDraft` and revision, ordered by date; it never reads text, answers or Trades and makes no writes. Invalid ranges/empty IDs fail before querying. The reader is registered through `AddPersistence`; no new migration is required.

Calendar loads this batch independently of its financial data and currency filter. Dates with entries show compact **Draft** or **Completed** indicators; no entry means no marker. Adjacent-month dates and Saturdays use their own daily Journal status. Saturday retains weekly-only financial content. Unknown/loading/error status is not interpreted as no entry: the modal action stays unavailable with Refresh recovery until a valid exact-scope status read succeeds.

The Day Performance action says **Add Journal** for a new entry, **Continue Journal** for a draft and **Open Journal** for completed content. It captures the selected New York date and exact Account scope, closes through the same protected `Window.Closing` path as X/Close/Escape/backdrop, and navigates only after `ShowDialog` returns. Inline Trade loading/editing can veto the close; no Journal navigation occurs and Trade edits remain intact. No Account is inferred from a Trade, currency or the last Journal selection.

`JournalViewModel.TryOpenScope` applies date and Account together with one existing unsaved-change decision, never an intermediate scope/read. Keep editing vetoes a different scope without discarding content; explicit Discard changes permits it. A retained dirty or revision-conflicted editor for the same scope is preserved on targeted reactivation rather than silently reloaded. Completed content opens read-only. Loads revalidate Account availability and preserve unavailable IDs, never falling back to All accounts. Returning to Calendar keeps month, selected date, Account and currency.

Only committed `Created`/`Updated` results emit `JournalDataCommitted`, including a commit returned after cancellation was requested. Unchanged, invalid, conflicting, cancelled or failed writes do not emit it. The shell marshals notification to the dispatcher and refreshes Calendar status only; inactive Calendar rereads on return. Batch reads have their own cancellation/generation checks, so changes of month/Account/currency, refresh or deactivation reject stale results. Journal text and answers never enter the Calendar grid. M14.6 independently refreshes the editor's history metadata after committed Journal writes.

## Read-only Trade context (M14.3)

`JournalTradeContextViewModel` composes `ITradingCalendarDayReader` and `ITradingAccountReader`. It receives only the editor's **applied** date and exact Account scope; a rejected dirty-scope change or an uncommitted typed date cannot retarget it. A null Account filters no Trades: every Account contributes, but that does not aggregate or change the independent All accounts journal text. Inactive Accounts remain supported. An unavailable selected Account produces an explicit context error, never an All accounts fallback. There is no currency filter or Trade editing/navigation in this milestone.

`TradingCalendarDayQuery` reuses the existing New York midnight-to-next-midnight **half-open UTC range** over authoritative `ClosedAtUtc`, including 23-/25-hour DST days. The reader excludes open/partially exited Trades, has no browse-page limit, and returns newest closure first with Trade ID as the stable tie-breaker. Its fixed set of batch queries reads browse rows, peak execution/allocation Size, Setups and Mistakes; there is no per-Trade query. Account/Instrument activity metadata is carried in `TradingCalendarDayDetails.References` from the same left joins. Missing references are retained as unavailable; inactive and unassigned classification states remain explicit. The additional metadata is read-only and requires no schema change.

The panel displays daily closed-Trade count, separate per-currency Effective Net summaries, and each Trade's New York closing time, Instrument, Account, Net, Size, direction, Setup and Mistakes. It reuses `CalendarTradePresentation` and `CalendarPnlSummary`; it never recomputes economics from prices. Known Net remains authoritative; unknown Net with known Gross is visibly **Estimated**; unknown economics remain **—**, not zero. A genuinely zero Trade still has a row and count. Detailed time help keeps the actual timestamp's explicit UTC offset. Full names/classifications have tooltips, long text wraps, and the table scrolls horizontally when necessary. Read-only rows are keyboard-focusable and carry complete accessible descriptions; ordinary vertical wheel input reaches the Journal page.

Context loading, cancellation, empty and error states are independent of the editor. **Refresh Trades** and confirmed Trade create/edit/delete, Calendar inline edits, and successful Tradovate/TopstepX imports reload only context while Journal is active. Noncommitted import outcomes do not generate a commit refresh. Re-entry always reads current context. Each read captures the date/Account and a generation; scope changes, newer refreshes and deactivation invalidate older responses, including readers that finish after cancellation. Obsolete rows are cleared rather than shown under another scope. Context refresh never calls the journal repository, prompts to discard text, changes draft/revision state or changes the applied scope; journal saves also do not depend on a successful Trade read.

M14.3 itself adds no Calendar Add Journal activation, review workflow, history browser, Trade mutation or economics/schema change. M14.4 adds the review workflow described above; Trade context remains read-only.

## Review History (M14.6)

The **Review History** expander on Journal uses the page's explicit Account scope. Changing the editor date does not silently filter the history to that single date; previous reviews remain browsable. Reviews are ordered by their stored New York `TradingDate` descending, with journal ID as a deterministic tie-breaker. Draft and Completed entries, including zero-Trade days, have date, scope, state and current revision context. All accounts is an exact null scope, not a wildcard. Inactive names are labeled; an unavailable selected Account retains its original ID and recovery message. Historical Account names/activity use current reference metadata; snapshots retain text, answers, state and audits, not historical Account names.

**Open review** requests that row's exact date/scope through the existing editor guard. Keep editing vetoes a different scope without losing freeform text or any answer. Discard changes is explicit. Opening a clean current entry reloads its latest committed version; opening the same entry with dirty or conflicted content preserves it and does not bypass Reload latest. Completed entries still require explicit Reopen review before editing. A stale row button after a page/scope refresh cannot open another entry.

Once a review is opened, a separate newest-first revision page shows revision number, Draft/Completed state and explicit UTC audit time. **View revision** fetches only that immutable snapshot, with exact freeform text and all three structured answers in read-only, copyable controls. Viewing is not restoring: it never calls create/update, changes editor content/revision, completes/reopens a review or adds history. There is no restore-old-version action. Journal saves/completion/reopening refresh metadata; they do not copy a displayed historical snapshot into the editor.

`IDailyJournalHistoryReader`, registered by `AddPersistence`, exposes:

| Read | Semantics |
| --- | --- |
| `BrowseAsync(accountId?, page, pageSize, ct)` | Exact-scope entry metadata; date descending then ID; total count and bounded page. No text/answers, Trades or per-entry revision query. |
| `BrowseRevisionsAsync(journalId, page, pageSize, ct)` | Revision metadata descending by revision; total count and bounded page. No snapshot text. |
| `GetRevisionAsync(journalId, revision, ct)` | One stored full snapshot, or null if unavailable. |

Page numbers are **one-based**; sizes **1–50**, with Desktop fixed at **20**. Invalid identifiers, sizes and overflowing offsets fail before querying. Empty or beyond-last pages remain empty without scope fallback. Each metadata page uses a count query and one `Skip`/`Take` projection, not one query per row. A selected snapshot uses one query. Fresh no-tracking contexts observe committed changes; cancellation propagates. All reads are independent of Trade queries and make no database writes. The schema and optimistic write rules are unchanged; no migration is required. The existing unbounded `GetHistoryAsync` remains a compatibility API, not the browser's read path.

List, revision-page and snapshot work run off the WPF dispatcher, each with a cancellation token and generation. Account/page/entry/revision changes or deactivation invalidate older work even if a reader returns after cancellation. Loading clears obsolete rows/content; failures show safe Refresh/View revision recovery without altering the editor. History cancellation is separate from editor Save/Reload and never discards its draft. Shared Light/Dark resources, wrapping controls, bounded vertically scrolling lists and read-only text fields keep long history reachable; Tab/Enter reach row actions and the expander has a visible focus border.

## Verification and later milestones

Focused Domain and isolated migrated-SQLite tests cover exact scope uniqueness (including direct database enforcement), empty trading dates, New York DST date identity, text/audit round trips, durable history, no-ops, stale and concurrent writes, inactive/missing Accounts, referential protection, cancellation/rollback and read-only retrieval. Migration tests verify an upgrade preserves existing trading data and a downgrade removes only the new journal tables.

M14.1 verification on 2026-10-04: **91 focused tests passed** (18 Domain, 54 persistence/schema, 19 Account presentation regressions). The complete Release suite passed **2,576 tests** (418 Domain, 516 Application, 753 Infrastructure, 889 Desktop), with no failures or skips. Release build had **zero warnings/errors**, EF reported **no pending model changes**, and `git diff --check` passed. Tests used isolated migrated SQLite databases; the real journal was not opened. Existing Desktop regression tests passing is not Journal editor or interactive acceptance.

The historical M14.1 results above do not establish later editor or interactive acceptance. M14.2 adds the explicit-save editor; M14.3 adds read-only Trade context; M14.4 adds structured review/completion; M14.5 adds exact-scope Calendar integration; M14.6 adds paged review/revision browsing. Richer text, attachments, scope migration, revision restoration and journal deletion remain deferred. Calendar passes the selected date/Account explicitly, including All accounts, without treating currency filtering as a different journal scope.

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
5. This historical M14.2 check required Calendar's Add Journal to remain disabled. M14.5 replaces it with the protected exact-scope launch described above.

### M14.3 automated verification and manual follow-up

Initial verification on 2026-10-04 using synthetic data and isolated migrated SQLite databases (superseded only for the full-suite gate by the follow-up below):

- **223 focused tests passed**: 189 Desktop Journal/navigation/Calendar-day regressions and 34 Calendar-reader/Journal-persistence cases. Coverage includes exact Account scope, both DST transition days, more rows than a browse page, separate currencies, estimated/zero/unavailable economics, inactive/missing classifications and references, read-only SQLite access, and an unchanged journal/history snapshot during Trade-context reads.
- Actual persisted Trade insert, correction and deletion refresh the context while preserving exact unsaved Journal text, revision and scope. Controlled TopstepX/Tradovate confirmation flows verify committed versus NoChanges/Blocked/cancelled/failed outcomes, commit-after-presentation-cancellation, active/inactive navigation, dispatcher publication and stale-read rejection. These controlled import-notification tests are not live import acceptance.
- **2,669 complete Release tests passed**: 418 Domain, 516 Application, 753 Infrastructure and 982 Desktop; no failures or skips. Release build: **zero warnings/errors**. EF model check: **no pending changes**. `git diff --check` passed. No schema or persistence-rule change is needed.
- Compiled WPF rendering and layout checks cover the context in Light/Dark at **1,100 DIP / 96 DPI** and **480 DIP / 240 DPI**. The rendered images were inspected: wrapped historical names, aligned headers/rows, separate-currency summaries, sign/estimated/unavailable styling and narrow horizontal scrolling. The checks also verify UI Automation row peers with complete names and no edit/invoke pattern, command bindings, empty/error states and vertical wheel forwarding. Existing editor renders/regressions remain covered.

**Live interaction remains unverified**; native desktop interaction was unavailable in this session. Automated renders and routed-event/automation-peer assertions are not mouse, keyboard or screen-reader acceptance. With a disposable `--isolated-data-root`, check:

1. In both themes, Tab from the editor to Refresh Trades and through read-only rows; inspect focus, full-name/time/estimate help, horizontal navigation and vertical wheel scrolling at normal and narrow/high-DPI sizes. Check status/error announcements with a screen reader.
2. Change the applied date and Account, including an inactive Account and an empty day. Confirm the row count, classifications, New York times and separate currencies match Calendar; veto a dirty scope change and confirm both context and draft stay on the original scope.
3. Keep unsaved Journal text while a pending synthetic Trade/import commit finishes. Confirm context refreshes without losing the draft or changing scope; noncommitted outcomes must not refresh as commits. Test Refresh Trades/cancellation/error recovery independently of Save and Reload latest.

The historical M14.3 checks did not cover review questions/completion or Calendar launch. M14.4 and M14.5 add them respectively; M14.6 adds history navigation separately.

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
5. Refresh Trade context while answers are unsaved; confirm no text, answer or scope changes. At M14.4 Calendar Add Journal remained disabled; M14.5 supersedes that boundary with the guarded launch above. This historical checklist did not cover M14.6 history navigation.

### M14.5 automated verification and manual follow-up

Verified on 2026-10-04 using synthetic data and isolated migrated SQLite databases:

- **509 focused tests passed**: 452 Desktop Calendar/Journal/navigation cases and 57 Infrastructure Journal/Calendar-reader cases. Coverage includes exact All accounts versus account-specific scope, inactive/unavailable Accounts, empty and adjacent-month dates, Saturday's own Journal, Draft/Completed/reopened markers, status-only read projection, cancellation, stale batches and commit notifications. Controlled modal tests verify closing and backdrop cleanup before navigation, inline Trade-edit close veto, and retained Calendar selections. Editor tests retain dirty/conflicted same-scope work and require an explicit discard decision before another scope.
- Compiled WPF renders/layout checks cover markers and Journal actions in Light/Dark at **1,100 DIP / 96 DPI** and **480 DIP / 240 DPI**. Rendered images were inspected; tests check compact markers, unchanged Saturday weekly-only content, day-marker-only tooltips, accessible action names and reachable controls. These are automated renders/routed-event tests, **not live interactive acceptance**.
- Release build: **zero warnings/errors**. No migration or model change is introduced. The existing migrated-SQLite editor flow verifies empty-day create/completion/reopen and exact-scope rereads without changes to Trade economics.
- The full Release verification gate is **not green**. The normal solution run passed 2,715 and failed 86 of 2,801 tests: an unchanged TopstepX confirmation test timed out waiting for its test store to start, and the native Calendar process exceeded its two-minute aggregate deadline, propagating 85 failures. Phase logs show progress through existing cases, not a stalled WPF phase; the new Journal native cases had not run before that deadline. A controlled `-m:1` solution run (one test project at a time, unchanged test concurrency/assertions/deadlines) passed **2,800**, failed **one**, with no skips: Domain 444, Application 516, Infrastructure 775 and Desktop 1,065/1,066. The remaining unchanged `LeavingJournalCancelsReadAndReturningIgnoresItsLateResult` timed out at its five-second fake-reader-start signal; it passed alone in 149 ms and in the focused suite. This is evidence of full-run scheduling sensitivity, not proof that the complete suite or CI is green. No retries, deadline changes, skipped cases or production workaround were added.

**Full-suite follow-up, 2026-10-05:** **280/280 constrained focused tests** and the **normal parallel Release suite, 2,810/2,810**, passed with no skips (Domain 444, Application 516, Infrastructure 775, Desktop 1,075). Child suites passed 36 grid and 85 native cases. The native harness now reuses one process-owned background STA/dispatcher, avoiding demonstrated per-case native-resource accumulation; the 30-/45-second case and two-minute process deadlines are unchanged. Journal checks keep their five-second bound, report load completion before fake-repository readiness, drain pending work on failure, and test late old results/errors against a fresh completed revision plus 16 concurrent navigation flows. The original Journal delay did not recur with instrumentation, so its exact cause is not claimed established. No production Journal or Calendar behavior changed. Release build, EF model consistency and diff checks passed. See [evidence and remaining limits](ci-wpf-tests.md#m145-local-full-suite-investigation-2026-10-05); the final native child finished in 118.6 seconds, so hosted-runner headroom still requires a new CI run.

Live pointer, keyboard and screen-reader checks remain unverified; this follow-up used automated tests, not a live application session. Use only a disposable `--isolated-data-root`:

1. In both themes, including a narrow/high-DPI window, select an empty day, an adjacent date and Saturday. Tab to Add/Continue/Open Journal; confirm the exact date and Account, including the distinct All accounts entry. Change currency and verify the same Journal marker/scope remains.
2. With an unsaved inline Trade edit, invoke Journal and confirm the modal stays open with edits intact. Save or Cancel, then invoke again; confirm one navigation, no Calendar click-through and retained month/date/Account/currency on return.
3. Save a draft, complete it, reopen it and return to Calendar after each action. Confirm Draft/Completed markers change without P&L/count changes. Rapidly switch month/Account during reads and verify no stale markers. Exercise status-read error/Refresh recovery.
4. Revisit a retained dirty or revision-conflicted Journal for the same scope; confirm no silent reload. Open a different scope and exercise Keep editing and Discard changes. Verify unavailable Accounts never fall back to All accounts. Check screen-reader state/action announcements.

The historical M14.5 local complete Release gate passes; a matching CI run and its live checklist remain unverified. M14.6 adds history navigation separately below.

### M14.6 automated verification and manual follow-up

The pre-implementation checkout on 2026-10-05 was clean `develop` at `91a634c31c903bb18b9223d8f9fe788f042206a7`, containing the bounded Desktop concurrency fix. GitHub's exact-head Actions query returned **zero runs**. Latest green CI #77 covers the older M13 `main` merge, not this fix. No matching failing run existed to investigate; local M14.5 success is not GitHub acceptance.

Focused history, Journal and navigation tests use synthetic fixtures and disposable migrated SQLite databases. Coverage includes 43 reviews across three pages, exact null versus Account scope, inactive/orphan references, 23 revision snapshots across two pages, completion/reopen refresh, exact answers/audits, invalid inputs/cancellation, metadata-only SQL projections, read-only single snapshots, dirty/conflict guards and controlled late list/revision/snapshot responses. Compiled WPF checks cover Light/Dark at **960 DIP / 96 DPI** and **480 DIP / 240 DPI**, wrapping, row targeting, keyboard-focusable actions, one-way/read-only snapshot bindings and shared resources. These are automated renders, not live input or screen-reader verification.

| Gate | Result |
| --- | --- |
| Focused Journal/history/navigation | **233 passed**: 44 Domain, 13 Application, 53 Infrastructure, 123 Desktop; no failures/skips. |
| Complete parallel Release suite, with workflow blame/diagnostic flags | **2,838 passed**: 444 Domain, 529 Application, 779 Infrastructure, 1,086 Desktop; no failures/skips. TRX and child diagnostics under ignored `artifacts/m146-full-release`. |
| Native/grid safety regressions during that full run | Native **86/86**, **94.807 s** process wall time, **25.193 s** below unchanged 120 s deadline; grid **36/36**, **40.120 s**. No harness/coverage/deadline changes. |
| Build/model/diff | Release build **zero warnings/errors**; EF **no pending model changes** using the in-memory design-time factory; `git diff --check` passed. |
| Automated visual evidence | Four Light/Dark, normal/narrow-high-DPI history renders generated and inspected. This does not establish live pointer/keyboard/screen-reader acceptance. |
| GitHub Actions | Exact baseline SHA lookup still returns **zero runs** after local verification. A new matching PR run is required after user commit/push; local green is not CI green. |
| Live UI | **Unverified**. Native desktop control is not available in the current tooling. Use the checklist below. |

Changed implementation files: Application `Journals/IDailyJournalHistoryReader.cs`; Infrastructure `Journals/DailyJournalHistoryReader.cs` and `Persistence/PersistenceServiceCollectionExtensions.cs`; Desktop `ViewModels/Journals/JournalHistoryViewModel.cs`, `JournalViewModel.cs`, and `Views/Journals/JournalHistoryView.xaml`, `JournalHistoryView.xaml.cs`, `JournalView.xaml`. Added tests: Application `Journals/JournalHistoryPagingTests.cs`; Infrastructure `Persistence/Journals/DailyJournalHistoryReaderTests.cs`; Desktop `Journals/JournalHistoryViewModelTests.cs`, `JournalHistorySqliteTests.cs`, `JournalHistoryViewTests.cs`, `JournalHistoryTestReader.cs`. Documentation: `README.md`, `docs/daily-journal.md`, `docs/desktop-ui.md`, `docs/ci-wpf-tests.md`. No existing work was present initially; no Git writes, schema changes or real journal access were performed.

Remaining isolated Windows checklist (launch with a disposable `--isolated-data-root`; never the real journal):

1. In Light and Dark, expand Review History at normal and narrow/high-DPI sizes. Tab/Enter through paging, Open review and View revision; check focus, list/text scrolling and screen-reader names/live feedback.
2. Save reviews on empty dates in All accounts and an inactive Account. Switch scope and page; confirm each list contains only its exact scope, newest date first, and empty scope feedback does not fall back.
3. With unsaved text or answers, open a different review. Keep editing must retain everything; Discard changes must open the requested date. Opening the same dirty/conflicted entry must preserve it, while a later explicit discard on leaving still works.
4. View an old revision and copy all four text fields; confirm its timestamp/state/content are historical, the editor is unchanged and no revision appears merely from viewing. Complete/reopen/save normally, then check refreshed metadata.
5. In two instances sharing only that disposable root, save competing edits. Verify conflict recovery, history viewing without an overwrite, and safe cancellation/error recovery during rapid scope/page/selection changes.
