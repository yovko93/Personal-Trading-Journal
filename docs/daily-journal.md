# Daily Journal — M14.1–M14.6

## Form Account and selected review presentation

Both standalone and Calendar-inline forms have a labeled, keyboard-accessible **Journal Account** selector beside their date context and before Journal text. **All accounts** is an explicit null scope and the default when no Account was chosen. Calendar Add preselects its selected Account; existing entries load their exact saved Account. Inactive Accounts remain selectable. The standalone top selector filters History while a form is open; it never changes that form's Account, fields or loaded revision. Closed-form date/Account browsing retains its existing behavior. The saved card displays the actual journal scope even when it differs from the History filter.

Selecting another form Account is local until **Save Journal** or **Cancel**. Existing entries move, rather than copy: one SQLite writer transaction checks the loaded revision and target Account, rejects an occupied date/Account with `AccountScopeOccupied`, changes the same root's Account/content/state, and appends one revision. Journal ID, date, creation timestamp, older text/answer/state snapshots and monotonic numbering are preserved. The original scope becomes available for a separate new entry. No journals are merged. A scope-only change is an edit; changed Cancel saves Draft, while changing back to the loaded Account and original four fields preserves the unchanged-Completed no-write rule. Save still requires meaningful Journal text and completes. A never-saved empty form creates nothing.

`UpdateDailyJournalCommand.TargetScope` is optional: omitted retains the stored Account; `DailyJournalAccountScope(null)` explicitly targets All accounts. Completed entries require explicit reopening before a scope move, like a content edit. Missing target Accounts block saves. Failure, cancellation before commit, target collisions and stale revisions keep all local fields and the chosen Account. Changing away from a collided target permits another deliberate save; revision conflicts require guarded Reload. Delete still targets and confirms the persisted entry's scope, not an unsaved target. Other navigation/window-close paths retain discard/keep-editing protection.

After commit the selected card shows the returned exact scope, History refreshes under its unchanged filter, and Calendar's existing generation-guarded indicator refresh covers both old and new scopes. The Calendar month/date/Account/currency and financial results do not change. Revision snapshots have always stored text, answers, state and audit time, **not historical Account assignments**; the entire preserved history belongs to the moved journal's current scope. This policy needs no migration and does not retroactively alter snapshot content.

The selected standalone read-only card uses shared `PtjJournalReviewSurfaceBrush` / `PtjJournalReviewBorderBrush`: pale teal in Light, muted deep teal in Dark, with primary text and existing ordinary Reopen styling. Forms retain one bottom Save/Cancel action row; Account is above Journal text.

### Form Account verification (2026-10-07)

- Focused Release: **389 passed**, zero failures/skips (Domain 54, Application 13, Infrastructure 77, Desktop 245). Includes explicit scope moves, inactive Accounts, null/non-null target collisions, stale revisions, monotonic history and rollback after saving; both editor hosts, top-filter independence, Cancel/Draft, scope change/revert and Calendar indicator refresh.
- Complete **parallel** Release: **2,985 passed**, zero failures/skips (Domain 454, Application 529, Infrastructure 803, Desktop 1,199). Desktop elapsed 2m24s; Calendar native child **86/86**. No deadlines, retries, skips or concurrency settings changed. The earlier full run passed 2,984 tests before adding the collision-then-revision-conflict regression; the final run covers that fix too.
- Release build: zero warnings/errors. EF pending-model check: no changes since the last migration. `git diff --check`: passed. No migration or model change was needed.
- Automated WPF layout/render inspection: selected Completed card, form Account and existing controls in Light/Dark, standalone **960 DIP / 96 DPI** and **480 DIP / 240 DPI**, inline **1100 DIP / 96 DPI** and **480 DIP / 240 DPI**. Existing scroll-to-actions checks still pass. Logs, TRX and synthetic render output are ignored under `artifacts/journal-form-account/`; no customer data or real journal was accessed.
- The first focused pass exposed old tests that used the top selector to retarget an already-open form. Their setup now selects scope before opening; interaction assertions now verify that changing the History filter retains form Account/fields. Date, Reload and page-discard protection remains tested. A test compilation typo was also corrected before the final runs.
- Final review added a collision-then-stale-write regression: a later revision conflict clears the earlier collision recovery flag, so changing Account cannot bypass the guarded Reload requirement. The repository's token check remains authoritative regardless of UI state.

**Still unverified live / in CI:** no native desktop interaction was performed and these uncommitted changes have no GitHub Actions result. On an isolated Windows data root, check keyboard selection in each form, changing the top filter with unsaved text, moving an entry to a free Account and attempting an occupied target, changed Cancel versus change/revert Cancel, conflict Reload, and old/new Calendar markers. Repeat in both themes at narrow/high-DPI width; the real journal must remain closed. A new CI run is required after the user's commit/push.

Changed-file inventory for this refinement:

```text
README.md
docs/daily-journal.md
docs/trading-calendar.md
src/PersonalTradingJournal.Application/Journals/DailyJournalWriteStatus.cs
src/PersonalTradingJournal.Application/Journals/UpdateDailyJournalCommand.cs
src/PersonalTradingJournal.Domain/Journals/DailyJournalAccountScope.cs (new)
src/PersonalTradingJournal.Domain/Journals/DailyJournalEntry.cs
src/PersonalTradingJournal.Infrastructure/Journals/DailyJournalRepository.cs
src/PersonalTradingJournal.Desktop/Resources/Themes/DarkTheme.xaml
src/PersonalTradingJournal.Desktop/Resources/Themes/LightTheme.xaml
src/PersonalTradingJournal.Desktop/ViewModels/Journals/JournalViewModel.cs
src/PersonalTradingJournal.Desktop/Views/Journals/InlineJournalView.xaml
src/PersonalTradingJournal.Desktop/Views/Journals/JournalView.xaml
tests/PersonalTradingJournal.Desktop.Tests/Journals/InlineJournalViewTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalFormAccountTests.cs (new)
tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalReviewViewModelTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalSqliteTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalViewModelTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalViewTests.cs
tests/PersonalTradingJournal.Domain.Tests/Journals/DailyJournalEntryTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Journals/DailyJournalAccountMoveTests.cs (new)
```

## Completed Cancel and Calendar aggregation (2026-10-07)

This section supersedes earlier statements that opening Reopen immediately saves Draft or that Calendar All accounts markers use null scope only.

- **Reopen review** explicitly authorizes a local editor in both the standalone page and Day Performance. The persisted Completed entry, timestamp and revision remain unchanged until a write. Reloading a Completed revision closes the old editor authorization; it must be explicitly reopened again.
- **Cancel** compares exact ordinal values of Journal text and all three answers, plus the form Account ID, against the loaded snapshot. For a Completed entry with no differences, including change-then-revert, it closes only the editor without a repository write, revision, notification or status transition. Changed fields/scope save together as Draft in one transaction/revision. A failed/cancelled/conflicting save retains every field and the open form. New exactly empty forms still close without creation; existing Drafts retain existing save/no-op behavior. Cancel does not ask to discard; other close/navigation paths retain explicit unsaved-change protection.
- **Save Journal** still requires meaningful Journal text (Unicode letter/digit), with optional answers, and saves as Completed. `UpdateDailyJournalCommand.ReopenCompleted` explicitly combines reopening and an edit in one revision, after the existing expected-revision check. Ordinary callers cannot modify Completed content without explicit reopening. No intermediate Draft revision, schema change or legacy-data rewrite is required. Historical answers-only Completed entries remain readable and unchanged Cancel remains valid.
- Calendar **All accounts indicators** aggregate all journals on each New York journal date, including null scope. Any Draft produces **Draft**; all Completed produces **✓**; no entries produces no marker. Multiple entries expose total, Draft and Completed counts accessibly, independent of query order. A specific account shows only its entries. Currency never filters journal indicators.
- The indicator aggregate does **not** change editing identity: All accounts Day Performance still opens/creates the separate null-scoped entry, even if its marker came only from P 21. The status-only query retains Account IDs, uses one fresh bounded grid-range projection (at most 42 dates), and never loads text/answers. Account switching and committed save/Draft-save/deletion use existing generation-guarded refresh. Financial summaries and trade counts are unchanged.

### Verification and remaining manual checks

Evidence is in ignored `artifacts/journal-cancel-aggregate/`. All fixtures are synthetic/isolated; no real journal was accessed.

- Focused Release: **377 passed**, zero failures/skips (Domain 53, Application 13, Infrastructure 71, Desktop 240). Covers each field independently, exact change/revert, unchanged Completed Cancel with retained snapshot/audits, both hosts, stale saves, existing Draft/new-empty behavior, aggregate P 21 + inactive + null scope, mixed states, exact modal editing, save/deletion refresh and existing stale-batch guards.
- Targeted WPF layout/render run: **6 passed**, including the supervised **86/86 Calendar native** cases. Inspected automated Calendar Light/Dark renders at **1100 DIP / 96 DPI** and **480 DIP / 240 DPI**; narrow grids retain horizontal scrolling. Tests check mixed aggregate accessible counts, compact Saturday indicators and visible standalone Save after local Completed reopening.
- Complete parallel Release: **2,972 passed**, zero failures/skips (Domain **453**, Application **529**, Infrastructure **797**, Desktop **1,193**). Desktop elapsed 2m36s; Calendar native **86/86**. The first full run had 2,971 passed and one old shell-navigation expectation that Reopen alone persists Draft. The regression now explicitly checks Completed after local Reopen, then changes an answer and Cancels before expecting Draft. The final full run is green; no deadlines, retries, skips or harness settings were changed.
- Release build: **0 warnings, 0 errors**. EF: **no pending model changes**. `git diff --check`: passed. No migration, Trade economics, import or P&L changes.
- Branch **develop**, HEAD **786e8038df2a78ce7db113f4cc98a2821e2e30cf**; initially clean. **28 modified + 2 new files**, unstaged; no commit/push. Live interaction and matching GitHub Actions remain **unverified**; a new pushed CI run is required to verify these changes there.

Remaining live Windows checks: in both themes, open a Completed entry in each host, change/revert all four fields and Cancel (still Completed); then change an answer and Cancel (Draft). Confirm the Calendar remains open. With a synthetic P 21 journal on 7 Oct 2026, switch P 21 → All accounts, add another account's Draft and verify the mixed-state accessible counts; delete the entries and verify marker removal. Confirm All accounts Add/Open still targets its own null-scoped journal. Local automated tests/renders do not verify live interaction or GitHub Actions.

### Changed files

```text
README.md
docs/daily-journal.md
docs/trading-calendar.md
src/PersonalTradingJournal.Application/Journals/DailyJournalStatus.cs
src/PersonalTradingJournal.Application/Journals/IDailyJournalStatusReader.cs
src/PersonalTradingJournal.Application/Journals/UpdateDailyJournalCommand.cs
src/PersonalTradingJournal.Desktop/ViewModels/Calendar/CalendarViewModel.Journal.cs
src/PersonalTradingJournal.Desktop/ViewModels/Journals/JournalViewModel.cs
src/PersonalTradingJournal.Desktop/Views/Journals/InlineJournalView.xaml
src/PersonalTradingJournal.Desktop/Views/Journals/JournalView.xaml
src/PersonalTradingJournal.Domain/Journals/DailyJournalEntry.cs
src/PersonalTradingJournal.Infrastructure/Journals/DailyJournalRepository.cs
src/PersonalTradingJournal.Infrastructure/Journals/DailyJournalStatusReader.cs
tests/PersonalTradingJournal.Desktop.Tests/Calendar/CalendarDayModalJournalTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Calendar/CalendarJournalTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Journals/CalendarInlineJournalSqliteTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalCalendarIntegrationViewModelTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalCalendarSqliteTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalHistorySqliteTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalReviewSqliteTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalReviewViewModelTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalSqliteTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalViewModelTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalViewTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Navigation/CalendarJournalNavigationTests.cs
tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/FakeDailyJournalRepository.cs
tests/PersonalTradingJournal.Domain.Tests/Journals/DailyJournalEntryTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Journals/DailyJournalStatusReaderTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Journals/CalendarAggregateJournalTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalCompletedCancelTests.cs
```

## Previews, action colors and revision deletion (2026-10-07)

The expanded review shows compact, labeled previews directly below **Open in editor** and before revision controls. Only nonempty Journal text/answers occupy space. One exact-date/Account repository read supplies current content for the opened journal ID (not the selected-day editor and not a stale historical snapshot). A replaced/deleted ID is unavailable rather than silently substituted. Reads are cancellable and generation-guarded. The preview is limited to three lines and at most 240 UTF-16 code units plus an ellipsis; wrapped text is also capped to three lines. Full text is unchanged in the editor and read-only revision view. All accounts filter, count, ordering, ten-entry page and unsaved editor remain intact.

**Open review** uses amber, **Open in editor** slate, and **View revision** sky theme resources. These are distinct from mint/teal Add/Continue, green Save, violet Cancel/Draft, red-outline Close and solid-red Delete. Actions retain explicit labels, hover/pressed states, dashed keyboard focus and readable disabled states. Revision actions move below metadata at narrow widths; previews and snapshots share the page scroller.

### Permanent older-snapshot deletion

`IDailyJournalRevisionWriter.DeleteRevisionAsync(JournalId, Revision, ExpectedCurrentRevision)` deletes one selected older snapshot inside a SQLite writer transaction. It checks the journal root's expected revision before deleting. Outcomes are `Deleted`, `NotFound`, `Conflict` or `CurrentRevisionProtected`; cancellation and write failure roll back removal. The confirmation identifies the New York trading date, exact Account name/ID, revision and Draft/Completed state, and warns that removal is permanent. It never deletes the journal or another snapshot. Inactive/unavailable reference names remain explicit; no scope fallback occurs.

**The current/latest revision cannot be deleted**, including a journal with only one revision. Its matching current-state snapshot is an existing persistence invariant. The row visibly says Current revision — protected, with a disabled Delete revision action and accessible explanation. An older deleted number is never reused. Root text, answers, status, UTC audits and concurrency token remain unchanged; a later changed Save (including an explicitly reopened edit) still increments the root token, not history count. Remaining history may have gaps. No tombstones or schema migration are added. Earlier statements that history always contains every past revision now apply only until an explicit deletion.

After success, the revision count is reread, an emptied final page moves to the last remaining page, and a deleted open snapshot closes. The journal-entry History page/filter remains intact. Confirmation cancellation makes no write. Failed/cancelled/conflicting writes retain the visible revision list and snapshot with recovery guidance; a stale head requires Refresh History before another deletion. Changing scope/row or leaving cancels pending work and prevents a late response from populating another review. Preview/deletion never overwrites unsaved editor text. No Calendar financial/status refresh is needed for removing an old snapshot because current journal state does not change.

### Verification and remaining checks

Synthetic automated evidence is stored under ignored `artifacts/journal-preview-revision-delete/`. No real journal, customer text, schema, Trade economics or import rules were changed.

- Focused Journal Release: **344 passed** (Domain 51, Application 13, Infrastructure 71, Desktop 209), zero failures/skips. SQLite covers exact scopes, middle removal, protected latest/only snapshot, stale token, cancellation and failure after SQL with rollback, retained head/audits, and subsequent Completed saves with non-reused revision numbers. Desktop covers confirmation cancellation, errors/conflicts, deleted versus retained open snapshots, last-page reconciliation, preview truncation and exact scope, unchanged aggregate History, and stale previews.
- Complete parallel Release: **2,955 passed**, zero failures/skips (Domain **451**, Application **529**, Infrastructure **797**, Desktop **1,178**; Desktop 3m03s). Release build **zero warnings/errors**; EF **no pending model changes**. `git diff --check` passed.
- The initial broad run had **87 Desktop failures**: 85 propagated the unchanged Calendar native **120-second** deadline, one editor child exhausted its **30-second** action budget while progressing through renders (24.625s process CPU, phase log at 30.145s), and one History layout test exposed competing preview/list continuations. Preview loading now precedes revision decoration, with a controlled regression. The layout fixture also explicitly awaits its independent History list. No retry, sleep, skip, deadline increase or harness serialization was added. The later complete parallel run passed, but it does not prove the earlier resource-sensitive deadlines are eliminated.
- Automated Light/Dark renders inspected at **960 DIP / 96 DPI** and **480 DIP / 240 DPI**. Compiled tests verify bounded previews, responsive revision actions, distinct colors with at least **4.5:1** text contrast in normal/hover/pressed/disabled states, and keyboard focus indicators. Automated renders are not live pointer, keyboard, screen-reader or physical-DPI acceptance.
- Final targeted layout/action run: **9/9 passed**, including additional scrolled revision-action captures in both themes and sizes. The final Release build again reports **zero warnings/errors**.
- Live interaction and matching GitHub Actions remain **unverified**. HEAD `6f2de66ecb53e4a00bc116f6f4469d7ca89b2b7b`, branch `develop`; initially clean, no commits or pushes.

Files in this refinement (**15 modified + 3 new**, unstaged):

```text
README.md
docs/daily-journal.md
docs/desktop-ui.md
src/PersonalTradingJournal.Application/Journals/IDailyJournalRepository.cs
src/PersonalTradingJournal.Application/Journals/IDailyJournalRevisionWriter.cs
src/PersonalTradingJournal.Desktop/Resources/Themes/DarkTheme.xaml
src/PersonalTradingJournal.Desktop/Resources/Themes/LightTheme.xaml
src/PersonalTradingJournal.Desktop/ViewModels/Journals/JournalHistoryViewModel.cs
src/PersonalTradingJournal.Desktop/ViewModels/Journals/JournalViewModel.cs
src/PersonalTradingJournal.Desktop/Views/Journals/JournalHistoryView.xaml
src/PersonalTradingJournal.Desktop/Views/Journals/JournalPresentationResources.xaml
src/PersonalTradingJournal.Desktop/Views/Journals/JournalReviewView.xaml
src/PersonalTradingJournal.Infrastructure/Journals/DailyJournalRepository.cs
src/PersonalTradingJournal.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs
tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalHistoryRevisionActionsTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalHistorySqliteTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalHistoryViewTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Journals/DailyJournalRevisionDeletionTests.cs
```

For isolated Windows acceptance: open a P 21 row on All accounts page two; check labeled, ellipsized previews without changing scope/order/page. View full text, cancel a middle-revision deletion, then confirm it; check count and snapshot closure, protected current revision, and unchanged editor draft. Save another revision through the editor and verify numbering continues. Repeat in Light/Dark at normal and narrow/high-DPI sizes with keyboard focus and screen-reader labels. Use a disposable isolated data root only.

## Required Journal text and inline History (2026-10-07)

This refinement supersedes the older completion and History-navigation acceptance notes below. **Save Journal requires meaningful Journal text** (at least one Unicode letter or digit); the three answers are optional. Both standalone and inline Calendar editors use the same Domain rule and show an error next to Journal text without closing or clearing the form. Cancel still saves exact partial content as Draft, including answers-only content, without a discard prompt. A completely empty new form closes without writing. Other navigation/close guards and expected-revision conflict protection are unchanged.

No persistence migration or data rewrite is necessary. Rehydration and revision reads preserve legacy answers-only Completed entries. An unchanged existing record remains unchanged; explicit reopening records a Draft revision, and subsequent completion requires Journal text. Tests seed an isolated legacy record and its snapshot, read both unchanged, reopen it, reject invalid recompletion without a write, then complete with text while retaining the original history.

**Open review** is read-only and independent of editor navigation. The selected row owns its inline revision browser and snapshot, directly before the next History row. Opening, switching or closing rows preserves All accounts, row order, total, current ten-entry page, selected editor date and every unsaved field. Close view closes the snapshot only; Close review closes the browser and snapshot. Only explicit **Open in editor** requests the original stored date/Account through existing unsaved-change guards for later editing/deletion. Viewing never writes or converts a P 21 entry to null scope. Scope/page changes still cancel and reject stale reads.

### Verification for this refinement

Evidence uses synthetic fixtures and isolated migrated SQLite only, under ignored `artifacts/journal-required-inline-history/`. Focused Journal tests: **331 passed** (Domain 51, Application 13, Infrastructure 66, Desktop 201). Navigation tests: **146 passed**. Tests cover optional answers, answers-only Draft cancellation, field-level errors in both hosts, legacy read/reopen/history, aggregate page-two row switching and closing, exact-scope editor/delete actions, read-only snapshots, ten-entry boundaries, and existing stale-response protections.

The first parallel full run reported **2,939 passed / 3 failed / 0 skipped**: two navigation fixtures still assumed the superseded completion/navigation rules and were corrected without weakening their guard/refresh assertions. The remaining `ConcurrentCreatesKeepOneEntryAndOneInitialRevision(accountScoped: True)` exhausted its existing 20-second token; cancellation was observed at `DailyJournalRepository.CreateAsync` line 57 before validation/context creation. Both unchanged concurrent-create cases passed in isolation (**2/2**, 3 seconds). This does not establish the full-load scheduling cause, and no timeout, retry, serialization or production persistence change was made.

After correcting the navigation fixtures, the final complete parallel Release run passed **2,942/2,942**, zero failures/skips: Domain **451**, Application **529**, Infrastructure **792**, Desktop **1,170** (Desktop 2m32s). This includes the unchanged concurrent-create cases; the earlier cancellation remains an intermittent verification observation, not a claimed harness fix. Release build: **zero warnings/errors**. EF model consistency: **no pending changes**, checked with the isolated design-time factory. `git diff --check` passed. Initial/final HEAD: `7f676c27275641e83e3ddead1f389c19debe5be5`, branch `develop`; initially clean, final **25 modified + 2 new files**, all unstaged.

Automated Light/Dark renders inspected at **960 DIP / 96 DPI** and **480 DIP / 240 DPI**, with inline Calendar components at **1,100/480 DIPs**. Review controls wrap and remain reachable, snapshots use the page scroller, and the required-text label appears in both hosts. These are compiled WPF renders/routed-control tests, not live pointer/keyboard, screen-reader or physical-DPI acceptance. Live UI and matching GitHub Actions remain **unverified**; no commit/push was made.

Remaining isolated Windows checks:

1. In both hosts, enter only an answer and press Save: field error, all content retained. Cancel saves Draft; reopen, add Journal text and Save: Completed. Test freeform-only completion and an empty new Cancel.
2. With All accounts and page two selected, open a P 21 row, view/close a revision, switch rows and close the review. Filter/count/order/page and an unrelated unsaved editor must remain unchanged. Open in editor must explicitly target P 21 and honor Keep editing.
3. Check both themes at normal and narrow/high-DPI sizes: Tab/Enter, field-error announcements, Close review versus Close view, and scrolling to the end of a long inline snapshot. Use only a disposable isolated data root.

### Exact changed files

```text
README.md
docs/daily-journal.md
docs/desktop-ui.md
docs/trading-calendar.md
src/PersonalTradingJournal.Desktop/ViewModels/Journals/JournalHistoryViewModel.cs
src/PersonalTradingJournal.Desktop/ViewModels/Journals/JournalViewModel.cs
src/PersonalTradingJournal.Desktop/Views/Journals/InlineJournalView.xaml
src/PersonalTradingJournal.Desktop/Views/Journals/JournalHistoryView.xaml
src/PersonalTradingJournal.Desktop/Views/Journals/JournalReviewView.xaml
src/PersonalTradingJournal.Desktop/Views/Journals/JournalReviewView.xaml.cs
src/PersonalTradingJournal.Desktop/Views/Journals/JournalView.xaml
src/PersonalTradingJournal.Domain/Journals/DailyJournalEntry.cs
tests/PersonalTradingJournal.Desktop.Tests/Journals/InlineJournalViewTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalCalendarIntegrationViewModelTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalEditorActionTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalHistorySqliteTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalHistoryViewModelTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalHistoryViewTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalReviewSqliteTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalReviewViewModelTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalViewModelTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalViewTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Navigation/CalendarJournalNavigationTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Navigation/JournalReentryTests.cs
tests/PersonalTradingJournal.Domain.Tests/Journals/DailyJournalEntryTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Journals/DailyJournalReviewRepositoryTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Journals/DailyJournalStatusReaderTests.cs
```

## Scope and identity

A `DailyJournalEntry` stores freeform text (required for new completion) and three optional Daily Review answers for **one explicit New York calendar/trading date and one exact Account scope**. The caller supplies `DateOnly TradingDate`; neither UTC audit time, machine timezone, linked Trades nor import TradeDay determines it. Both DST transition dates and days with zero Trades are valid, including for completion. Journals have no Trade dependency or currency scope and do not alter Calendar/Dashboard P&L.

`TradingAccountId == null` means the independent **All accounts journal**. It is not a wildcard, fallback, sum or concatenation of account journals. The same date may have one global entry and one per Account. `Guid.Empty` is invalid. Journal ID and date remain stable. Account reassignment is permitted only through the explicit, revision-checked `TargetScope` move described above; there is no merge. Explicit permanent deletion is described below.

Existing inactive Accounts permit create/read/update: historical review is not new trading activity. A missing write-target Account rejects the save with `AccountUnavailable`, never implicitly retargeting to null or another Account. Reads retain an orphaned historical entry and identify its scope as `Unavailable` if references were damaged outside the application; Desktop retains its read-only recovery state. Account names/activity are current reference metadata, not revision contents. A restrictive foreign key normally prevents deleting a referenced Account; the existing Account delete workflow reports it as referenced and recommends deactivation.

## Text, draft and audits

- `Text` preserves exact plain text, including whitespace, Unicode and line endings. Empty text is valid in Drafts and historical snapshots; null is not. The limit is **100,000 UTF-16 code units**, enforced by Domain validation without truncation.
- `Review` is an immutable `DailyReviewAnswers` value with `WentWell`, `NeedsImprovement`, and `NextTradingDay`. Each exact answer permits empty content and has a **100,000 UTF-16 code-unit** limit, separately from freeform text. No answer is trimmed or truncated. Partially answered drafts are valid.
- The repository contract still permits Draft entries (including empty content), and existing Drafts remain recoverable. **Save Journal** in either editor explicitly requests `IsDraft=false`: Journal text must contain a Unicode letter or digit. Journal text alone is sufficient; all three answers are optional and cannot substitute for it. Whitespace, punctuation, emoji or invisible format characters alone are insufficient; this is a content-presence check, not an assessment of writing quality. The shared Domain rule validates create and update. Completed content is locked until an explicit **Reopen review** creates a draft revision with unchanged content.
- `CreatedAtUtc` and `UpdatedAtUtc` are supplied by the repository's `TimeProvider` in canonical UTC. Updates cannot move audit time backward. They do not change the selected New York date.
- Revision starts at **1**. Each actual text, answer, Account scope or state change increments the positive 64-bit revision and produces a complete durable content snapshot. Opening Reopen is local; the next changed Save/Cancel combines reopening and editing atomically. Equal content/scope/state with the current expected revision returns `Unchanged`, preserving audit time and history. Equal timestamps remain valid; revision, not timestamp, is the concurrency token. Overflow fails without changes.

## Application boundary

`Application.Journals.IDailyJournalRepository` is the write/entry persistence boundary, registered by `AddPersistence`. M14.6 adds a separate bounded, read-only history reader; the legacy full-history method below is not used by Desktop browsing:

| Operation | Contract |
| --- | --- |
| `GetAsync(date, accountId?, ct)` | Exact-scope disconnected entry plus `AllAccounts`, `Active`, `Inactive` or `Unavailable` Account state and readable name; null if no entry. Never falls back to another scope. |
| `GetHistoryAsync(journalId, ct)` | Full committed snapshots ordered by revision ascending, including initial and current revisions; empty for a missing valid ID. |
| `CreateAsync(command, ct)` | `Created`, `AlreadyExists` or `AccountUnavailable`. Duplicate creation never overwrites the existing entry. |
| `UpdateAsync(command, ct)` | Required journal ID and positive `ExpectedRevision`; optional explicit `TargetScope`. Returns `Updated`, `Unchanged`, `NotFound`, `Conflict`, `AccountUnavailable` or `AccountScopeOccupied`. Stale submissions conflict even if their proposed text happens to match current text. |
| `DeleteAsync(command, ct)` | Required journal ID and positive `ExpectedRevision`; `Deleted`, `NotFound` or `Conflict`. Removes the exact entry and every snapshot atomically. No tombstone is retained. |

Write results contain the authoritative disconnected journal when available. A conflict requires rereading/reconciling the newer revision, not automatic resubmission or last-write-wins. Invalid identifiers/revision and Domain-invalid content throw argument exceptions; cancellation propagates `OperationCanceledException`. Unexpected database failures propagate without disguising a failed save as success. No customer journal text is logged by this path.

Create/update commands carry `Review` with the exact answers. For compatibility, an omitted create review means empty answers; an omitted update review retains the current answers. History reads always include the stored answers. `IsDraft=false` explicitly requests completion. `ReopenCompleted=true` explicitly allows a changed Completed entry to be saved or moved in the same transaction; otherwise its content/scope remains locked. The expected revision is checked before changing content, scope or state, including same-content, completion and reopen requests.

## SQLite persistence and safety

Additive migration `20261004145757_AddDailyJournals` creates only:

- `DailyJournals`: current root, explicit date/nullable Account, text, draft flag, revision and UTC audits.
- `DailyJournalRevisions`: complete text/draft/audit snapshot keyed by `(JournalId, Revision)`. Every successful creation/update appends exactly one snapshot. Reads never reconstruct history from mutable current content. Updates cannot change old snapshots; explicit whole-journal deletion removes them all.

SQLite allows multiple NULLs in an ordinary composite unique index. Two filtered unique indexes enforce **date+Account for non-null Accounts** and **date alone for null Accounts**. Both revisions and root use positive-revision checks; the root revision is also an EF concurrency token. The restrictive foreign keys remain: deleting a journal requires explicitly deleting its snapshots in the same transaction; deleting a referenced Account remains blocked. Direct external SQL tampering is not an audit-security boundary.

Writes use a fresh context and SQLite writer transaction **before** checking the key, Account and expected revision. Head and snapshot are saved and committed together. A concurrent writer waits for the transaction, then sees the newer key/revision; it cannot silently overwrite it. There are no application retries. Cancellation or a save/commit failure rolls back uncommitted head and history changes. No-op, conflict, duplicate or missing-reference outcomes write nothing.

Reads use fresh no-tracking contexts: one exact-key query with Account left join, or one journal-scoped ordered history query. They are cancellable, do not load Trades and perform no writes. Existing Trades, executions, import ledgers and analytics projections are untouched. Migration generation/model checks use the in-memory design-time factory, not the real journal.

Additive migration **`20261004165044_AddDailyJournalReviewAnswers`** adds `WentWell`, `NeedsImprovement` and `NextTradingDay` as non-null TEXT columns with empty-string defaults to **both** tables. Existing text, scope, state, audit timestamps and revision numbers are preserved; old history remains intact and reads empty answers. A legacy `IsDraft=false` record may predate the questions: it is retained read-only as stored, can be reopened, and any new completion must satisfy the current minimum-content rule (a Unicode letter or digit in Journal text). Migration does not retroactively create answers or relabel past revisions as having passed this rule. Downgrading removes only the six answer columns and therefore discards their content; it is not part of the application workflow.

## Desktop editor (M14.2)

### Permanent deletion

**Delete Journal** is a separate, solid-red destructive action for an existing Draft or Completed entry. Its confirmation names the exact New York date and Account scope and states that the entry, **all revision history**, and any local unsaved edits will be permanently removed. There is no tombstone, undo, or revision restore after deletion. Keep Journal/cancelling the confirmation changes nothing. Closing the form, review or snapshot never deletes.

The repository starts its SQLite writer transaction before reading the ID/revision, rejects stale `ExpectedRevision` as `Conflict`, then deletes snapshots and the root atomically. Failures/cancellation before commit roll back both tables. `NotFound` cannot target a replacement entry: recreation uses a fresh ID and revision 1. Inactive and unavailable Account scopes may delete an existing exact entry without retargeting it; unavailable Accounts still cannot create/update. A conflict or missing result preserves local text/answers and requires explicit Reload; other failures/cancellation preserve the form for retry. A committed deletion emits the same Journal notification as a committed save, clears any opened review/snapshot, refreshes paged History and invalidates Calendar markers. A returned commit is honored even if cancellation was requested too late. No schema change is required.

### Compact review and explicit form

The default page browses saved content and an initially expanded Review History. Date/Account selection shows a compact saved entry, not the full editor. Opening a History row expands its read-only revision browser in place without changing the selected editor. **Add Journal** in the top action area opens the exact selected scope; an existing draft continues the same entry, never a duplicate. **Continue Journal** is also available on a saved draft. Completed reviews instead offer **Reopen review**, which commits unchanged content as a draft before opening the form; failed reopening stays read-only.

Only an open draft form can Save. Both hosts have **one Save Journal / Cancel row below the freeform text and all three answers**; there are no upper duplicate actions or separate Complete review button. Save atomically writes all four exact fields and **Completed** state through the expected-revision contract. The minimum-content rule above accepts freeform-only or partially answered reviews. Success closes the form, shows Completed and refreshes History/Calendar status. Day Performance remains open. A returned commit is honored even if cancellation arrived too late. Failed, rolled-back/cancelled or conflicting saves retain the form and all fields; Reload latest remains guarded recovery. **Cancel** means **Save as draft and close**: it atomically saves all four current fields as Draft through the same expected-revision contract, then closes only the form. It does not ask to discard. A completely empty never-saved form simply closes; nonempty text (including whitespace/punctuation) is preserved as Draft, without Completed-content validation. Existing entries append a revision only when content/state changes; unchanged saves close without adding history. Failures, cancellation and conflicts keep the editor and all fields, with guarded Reload recovery. Cancel never deletes. Same-scope dirty/conflicted history navigation preserves the form. Immutable snapshots remain read-only. Scrolling and keyboard navigation reach the bottom actions.

Freeform text uses a 120-DIP height and 80-DIP minimum; each answer uses 72/56 DIPs. Longer content scrolls inside each multiline field, without truncation. Shared Light/Dark brushes, wrapping top controls and page scrolling keep actions reachable at narrow/high-DPI sizes. Trade context refresh never closes the form or replaces edits.

The open form and its Save/Cancel actions appear **before Review History**, not beneath it. The page owns one vertical viewport. Editable fields retain bounded internal text scrolling; at their top/bottom boundary, ordinary wheel input passes once to the page if it can move. Shift+wheel is not redirected. Read-only revision fields grow with their full wrapped content and use the page scrollbar, keyboard paging/BringIntoView and vertical touch panning, rather than independent capped viewports.

The shell's **Journal** destination hosts `JournalViewModel` and `JournalView`. The initial date is today's New York calendar date. An explicit date picker and Account selector identify the exact entry; All accounts is a separate option, and days without Trades are valid. After accepted departure, re-entry resets to today's New York date and All accounts and rereads that exact entry. Vetoed navigation preserves scope and edits. The text editor depends on `IDailyJournalRepository`, `ITradingAccountReader` and the Desktop dialog boundary; it never queries EF Core. The independent M14.3 context below it reads Trades through the existing Calendar Application boundary.

### Readability and history layout

Journal-scoped presentation resources use **16-DIP primary text**, **22-DIP section headings**, **24-DIP note line height** and stronger theme-aware primary text contrast; secondary audit/helper labels use 14 DIPs and the shared secondary brush rather than muted text. Other application pages retain their typography. Long saved notes are fully wrapped, with no ellipsis or height cap; empty note values reserve no blank line. Notes, editor content and read-only snapshots use a **760-DIP maximum reading width**, while their panels and history use available width. Editable fields keep their practical 120-/72-DIP heights and internal scrolling; snapshots grow into the page scrollbar.

The selected **New York trading date**, not a converted audit instant, is formatted as a culture-aware `dd MMM yyyy` heading (for example **06 Oct 2026**). A separate **No entry / Draft / Completed** badge identifies the loaded entry; loading/recovery feedback remains independent. Add Journal remains in the top action area and supports empty trading days. **About Account scope** is collapsed by default, keyboard-accessible and retains the explanation that All accounts is a separate journal. Unavailable Account errors and recovery still retain the exact scope.

Review History is below the selected journal/form and its actions, spanning the page's available width. Its header reads **Review History · N reviews**, with Refresh at the right. Wide rows align **Date (New York) | Account | Status | Revision | Action**: emphasized dates, Account consuming remaining width, bounded metadata/action columns, subtle separators and a shared theme-aware hover background. Below 720 DIPs of history-view width, the column header is replaced by stacked date, status/revision and wrapping Account content; Open review keeps a normal-height, keyboard-accessible target at the right. Status/revision metadata wraps if needed instead of overlapping. Page number and Previous/Next sit at the right of the table footer. The ten-entry page size, deterministic ordering, independent revision paging and stale-read protections are unchanged. Close review and Close view remain distinct, and revision timestamps retain New York/UTC-4/UTC-5 presentation.

**Save Journal** or **Ctrl+S** explicitly saves the selected entry as Completed. There is no autosave, save on blur, or automatic save during navigation. The editor passes text and answers without trimming or truncation, preserving whitespace and Unicode; Journal text must contain a letter or digit; all answer fields may be empty. Validation appears beside Journal text and leaves every field and the form intact. It shows a character count, unsaved/saved state and the saved revision. Over-limit content remains visible while saving/completion is blocked. The date picker uses shared Light/Dark calendar resources and culture-aware typed dates; invalid or uncommitted date input blocks writes against the old bound date. Tab leaves each multiline editor instead of inserting a tab.

Changing date/Account, choosing **Reload latest**, navigating to another page or closing the main window checks for unsaved text **or any answer**. **Keep editing** retains the current scope, all edits and page; **Discard changes** permits the requested transition. Clicking Journal while already there preserves the draft. A save, completion or reopen in progress blocks scope changes, page navigation and window close; **Cancel operation** requests cancellation and retains local content if the write is cancelled. A committed result is still accepted if cancellation arrived after the write committed.

Each load captures its date/Account and rejects late results after scope changes, cancellation or deactivation. Loading/saving state, safe error feedback and retry controls are explicit. Load and save failures keep the local text. A stale update, duplicate creation or missing entry does not overwrite the stored journal or silently replace the local draft: Save is blocked until an explicit successful **Reload latest**. Reload asks before discarding dirty text, then reads the authoritative entry and revision. There is no automatic merge or forced overwrite.

Inactive Accounts remain named, marked inactive and selectable for historical journals. An unavailable selected Account stays selected with its original ID and an unavailable label; the editor blocks create/update and never falls back to All accounts. Explicit deletion of an existing exact entry is still permitted. New forms are unsaved drafts; Save persists Completed when Journal text meets the minimum-content rule. M14.6 adds the bounded history browser described below.

## Daily Review and completion (M14.4)

The Journal page shows these questions with multiline, accessible answer controls:

1. **What went well?**
2. **What needs improvement?**
3. **What will I do differently next trading day?**

The original requirement to fill all three answers is superseded by the shared minimum-content rule: meaningful Journal text is required for completion; answers are optional. **Save Journal is the completion action** in both editors. It writes exact text, all answers and Completed state in the same revision transaction. Historical Draft/Completed states and snapshots remain as stored; there is no data migration or automatic completion merely from typing.

Completed text and answers appear in the compact read-only review, with wrapped text, tooltips and copyable revision snapshots. **Reopen review** uses the loaded revision and unchanged content, creates a Draft snapshot and opens the form only after success. A failed/cancelled/stale reopen stays Completed. Save then completes the edited entry; Cancel saves current edits as a new Draft revision when changed; an unchanged reopened Draft closes without adding a revision. Changing scope never moves an entry's stored scope.

All three write actions share one submission guard and cancellation path. Failures preserve local text/answers/state. A stale completion or reopen uses the same explicit **Reload latest** recovery as a stale save, with a discard prompt if local content is dirty. Successful Trade-context refresh never reloads or overwrites these edits. Completion is a user action, not a claim that the app evaluated the answers or trading quality.

M14.5 connects Calendar's **Day Journal** action to this editor as described below. Journal operations do not change Calendar/Dashboard calculations, Trades, executions or import records.

## Save completion and standalone re-entry

After navigation away from standalone Journal passes its unsaved-change guard, the next visit resets to the current **America/New_York date and All accounts**. The date is calculated from the current clock instant on re-entry, including midnight and DST changes. Editor, opened review, revision snapshot and both History pages reset; a fresh exact-scope read shows today's entry or No entry/Add Journal. Clicking the already-active Journal page or a vetoed departure does not reset anything. Reads from the previous visit are cancelled/invalidated before loading the new scope. This reset does not write or remove the older entry or history.

Calendar's separate inline session does not request the standalone reset. Its selected date/Account remain authoritative, with All accounts still a distinct scope. Save/Cancel and revision validation are shared across both hosts. Cancel of an existing Draft saves current fields under its expected revision and closes only after success. Separate navigation/window-close paths still ask before discarding unsaved edits; they do not invoke Draft save. An explicitly requested scope (for example the retained targeted-navigation API) takes precedence over a default page visit.

## Cancel saves Draft and action colors (2026-10-07)

The editor's **Cancel** label is retained, with the tooltip and accessible name **Save as draft and close**. Its dedicated `SaveDraftAndCloseCommand` sends all four exact fields and `IsDraft=true` through the same atomic writer as Save Journal. It never calls the discard dialog. Success collapses only the editor and refreshes History/Calendar status; failure, cancellation and conflict leave every field and the editor intact. Cancel is disabled during writes and when a stale revision requires Reload, so overlapping saves and silent overwrites remain blocked.

Only a never-saved form with **four exactly empty strings** closes without writing. Nonempty partial content, including whitespace/punctuation, is preserved as Draft rather than silently discarded. An existing entry whose fields are cleared is saved as an empty Draft revision; it is not deleted. An unchanged Draft returns Unchanged and closes without another revision or a commit notification. Existing Completed reviews remain read-only until explicitly reopened; Save Journal still requires a Unicode letter/digit in Journal text and writes Completed. Account/date are captured together at submission and never retargeted by these actions.

This behavior applies only to the two editors' Cancel buttons. Close Journal, navigation, date/account changes, Reload and modal/window closing retain explicit discard/keep-editing guards; none automatically saves a Draft. Standalone accepted re-entry still resets to today in New York/All accounts; Calendar stays on its selected day/account and Day Performance remains open after Cancel.

Shared **Journal-only** styles keep green Save, violet Cancel/Draft and blue ordinary actions such as Refresh and Reopen; the newer History styles use amber Open review, slate Open in editor and sky View revision. **Add Journal and Continue Journal** reuse the established mint/teal `PtjPrimaryButtonStyle` accent in both hosts, distinct from blue actions. Their existing hover/pressed opacity, visible focus outline and disabled state remain intact. Solid-red Delete remains distinct from red-outlined Close review/Close view. Labels convey meaning without color alone; no persistence, migrations or Trade economics change.

Baseline: clean `develop`, HEAD `cd6c9ec9e8894e6178cc20a01a2fa760ac346bf6`. Focused Release: **688/688 passed**, zero failures/skips (Domain **49**, Application **13**, Infrastructure **65**, Desktop **561**), including theme tests. New/expanded cases cover both hosts' Draft creation and status refresh, all exact fields, write errors/cancellation/conflicts, empty/nonmeaningful forms, SQLite update/history/no-op behavior and a stale Draft save against a newer Completed revision. Guarded navigation/close tests remain unchanged in behavior. Compiled WPF assertions check the Cancel command/name, unique bottom actions, state colors/focus, and ordinary History actions. Evidence is under ignored `artifacts/journal-cancel-draft`.

Full parallel Release: **2,936/2,936 passed**, zero failures/skips (Domain **449**, Application **529**, Infrastructure **791**, Desktop **1,167**). The native Calendar child passed its unchanged 120-second deadline in **84.071 seconds**. Release build: **zero warnings/errors**; EF: **no pending model changes**, using the isolated in-memory design-time factory; diff check passed. Automated Light/Dark renders were inspected at standalone **960 DIP / 96 DPI**, inline **1,100 DIP / 96 DPI**, and both hosts at **480 DIP / 240 DPI**, including reachable bottom actions and History buttons. These are component renders and routed/state tests, not live interaction or physical display-scaling acceptance. No real journal, Git write or customer data was used.

Changed files for this refinement: Desktop `JournalViewModel.cs`, `Resources/Controls.xaml`, `Resources/Themes/LightTheme.xaml`, `Resources/Themes/DarkTheme.xaml`, `JournalView.xaml`, `InlineJournalView.xaml`, `JournalPresentationResources.xaml`; tests `JournalEditorActionTests.cs`, `JournalDraftCloseTests.cs` (new), `JournalButtonAssertions.cs` (new), `JournalViewTests.cs`, `InlineJournalViewTests.cs`, `JournalHistoryViewTests.cs`; README and `docs/daily-journal.md`, `docs/trading-calendar.md`, `docs/desktop-ui.md`. Domain/Application/repository contracts and schema are unchanged.

Remaining isolated Windows checks (live UI and matching GitHub Actions are not verified):

1. In both hosts, enter freeform text and partial answers; Cancel must save Draft without a discard dialog, collapse the form and update History/Calendar. Continue must show every field. Calendar must stay open on the same date/account.
2. Cancel a new empty form: no entry. Reopen an existing entry, edit and Cancel: prior revisions remain and the changed Draft adds one revision. An unchanged Draft adds none. Save Journal still marks meaningful content Completed.
3. Provoke a newer revision in a second isolated instance and Cancel the stale editor: fields stay open, recovery is explicit. Navigation/modal close still asks to discard; Keep editing preserves the draft.
4. Check keyboard focus, hover, pressed/disabled colors, tooltips and action-row scrolling in Light/Dark at normal and narrow/240-DPI sizes. Re-run GitHub Actions after the user's commit/push; local success is not CI acceptance.

### Previous Save completion verification (2026-10-06)

Historical evidence below predates Cancel-as-Draft. Its Cancel/discard manual steps are superseded by the current checklist above.

Baseline: clean `develop`, HEAD `593d63640a507436901e5c1f9bbb5637e099f56d`. Save now uses the existing atomic Completed write rather than Draft; the Domain minimum-content rule is shared by both editors and repository create/update. No migration, Trade/import economics, timestamp conversion or test-harness scheduling/deadline changes were needed. Cancel already used a local-only discard path; new SQLite regressions prove it preserves persisted Drafts and history in both hosts.

Focused automated tests: **648/648 passed** (Domain **49**, Application **13**, Infrastructure **65**, Desktop **521**), zero failures/skips. Coverage includes freeform-only/partial-answer completion, all-field preservation, empty/nonmeaningful validation, expected revisions and durable snapshots, inactive exact scopes, Cancel of clean/dirty persisted Drafts, new unsaved forms, conflict/cancellation, Calendar Completed indicators, accepted/vetoed navigation, History reset, New York midnight and both DST transitions. Existing late-read rejection tests remain green. Logs and TRX are under ignored `artifacts/journal-save-completed/focused-final`.

Full parallel Release: **2,917/2,917 passed**, zero failures/skips: Domain **449**, Application **529**, Infrastructure **791**, Desktop **1,148**. The Calendar native child passed **86/86** in **72.375 seconds** against its unchanged 120-second aggregate deadline. Logs/TRX and child diagnostics are under ignored `artifacts/journal-save-completed/full-release`. Release build has **zero warnings/errors**; EF reports **no pending model changes** using the in-memory design-time factory; `git diff --check` passed.

Automated Light/Dark renders were inspected at **960 DIP / 96 DPI** standalone, **1,100 DIP / 96 DPI** inline, and **480 DIP / 240 DPI** in both hosts. The single bottom Save/Cancel pair remains readable and reachable; accessible Save names identify Completed behavior. These are component renders, not live user interaction. Live UI and a matching GitHub Actions run remain unverified; the latter requires the user's commit/push.

Remaining isolated Windows checks (never use the real journal):

1. In each host, save freeform-only text with empty answers. Confirm Completed, collapsed editor, exact saved text and refreshed History/Calendar indicator; Calendar stays open on the same day/account.
2. Reopen to create a persisted Draft. Change text/answers, Cancel and choose Keep editing, then Cancel/Discard. Continue the Draft and verify only the unsaved changes were discarded. Cancel a new unsaved form and verify No entry.
3. On standalone Journal, select an older account-specific entry and open History/revision page 2. Make an edit, try leaving and choose Keep editing: verify nothing resets. Accept departure, return and verify today's New York date, All accounts, closed editor/review/snapshot and page 1. Check an existing-today entry and a scope with No entry.
4. Exercise keyboard Save/Cancel and conflict/Reload recovery in Light/Dark at normal and high display scaling. Verify Calendar inline never switches to today on its own.

Changed production files: Domain `DailyJournalEntry.cs`, `DailyReviewAnswers.cs`; Desktop `JournalViewModel.cs`, `JournalHistoryViewModel.cs`, `MainWindowViewModel.cs`, `JournalView.xaml`, `InlineJournalView.xaml`. Documentation: README, this file, `desktop-ui.md`, `trading-calendar.md`. Tests: Domain `DailyJournalEntryTests.cs`, `DailyReviewAnswersTests.cs`; Infrastructure `DailyJournalReviewRepositoryTests.cs`; Desktop Journal `CalendarInlineJournalSqliteTests.cs`, `InlineJournalViewTests.cs`, `JournalCalendarIntegrationViewModelTests.cs`, `JournalCalendarSqliteTests.cs`, `JournalEditorActionTests.cs`, `JournalHistorySqliteTests.cs`, `JournalReviewSqliteTests.cs`, `JournalReviewViewModelTests.cs`, `JournalSqliteTests.cs`, `JournalViewModelTests.cs`, `JournalViewTests.cs`; navigation `CalendarJournalNavigationTests.cs`, `JournalNavigationTests.cs`, new `JournalReentryTests.cs`. Earlier Draft-save fixtures now seed Drafts explicitly or reopen Completed entries instead of assuming Save remains Draft.

## Previous simplified editor actions verification (2026-10-06)

Initial checkout: clean `develop`, HEAD `7124e1848dd2bf2d4cf456ac668719f19333ed8c`. This supersedes earlier UI checks requiring upper Save/Cancel actions or a Complete review button. Only the two editor layouts, regression tests and documentation change; the existing Save command already writes all four fields as a Draft and publishes the authoritative result before notifying its host. The completion/reopening contract and persisted history are unchanged. No migration, economics change, test-harness deadline change, Git write or real journal access.

| Evidence | Result |
| --- | --- |
| Focused automated tests | **503/503 passed**, zero failures/skips: Journal, Calendar and navigation. Ten new cases cover both hosts, partial and fully answered exact-content SQLite saves, and controlled conflict, failed write, cancellation and explicit discard. Saves remain Draft in both current rows and durable revisions. Successful standalone saves refresh History; inline saves keep the same Day Performance/Trade data and refresh Calendar markers. Failed saves retain every field and guarded recovery. Existing Completed/reopen behavior remains covered. |
| Full parallel Release | **2,892/2,892 passed**, zero failures/skips: Domain **444**, Application **529**, Infrastructure **789**, Desktop **1,130**. TRX, VSTest and unchanged supervised-child diagnostics are under ignored `artifacts/journal-editor-actions/full-release`. |
| Build / model / diff | Release build **zero warnings/errors**; EF **no pending model changes**, using the in-memory design-time factory; `git diff --check` passed. |
| Automated WPF layouts/renders | Exactly one Save-command button and one Cancel-editor button per host, no Complete-command button, actions below the final answer, focusable controls and scroll reachability. Light/Dark renders inspected at **960 DIP / 96 DPI** standalone, **1,100 DIP / 96 DPI** inline and **480 DIP / 240 DPI** for both. Evidence under ignored `artifacts/journal-editor-actions`; detached renders and routed-command tests are not live interaction/display-scale acceptance. |
| Live UI / GitHub Actions | **Unverified for these changes.** Native desktop interaction is unavailable in the current tooling. A new matching hosted run requires the user's commit/push; local green tests do not establish GitHub CI acceptance. |

Remaining isolated Windows checklist (use a disposable `--isolated-data-root`):

1. In both themes and normal/narrow-high-DPI layouts, open standalone and inline forms. Confirm no upper Save/Cancel or Complete button, one bottom action pair, and keyboard/wheel access through all fields to those actions.
2. Save partial and fully answered reviews in each host. Both must show Draft, close only their form and retain exact text. Calendar must remain in Day Performance and update its marker; standalone History must update.
3. Cancel with Keep editing/Discard and provoke a competing revision save in isolated instances. Keep editing and failed/conflicting saves must retain every field; Reload requires explicit discard. Previously Completed entries must stay read-only until Reopen, after which Save remains Draft. No new completion action is expected.

## Inline Day Performance acceptance (2026-10-06)

This refinement supersedes the earlier Calendar-to-standalone navigation checks below. Automated coverage uses only synthetic fixtures and isolated migrated SQLite. The native modal test explicitly lays out the embedded view under scoped window resources, tests Trade and Journal close vetoes, and confirms the old navigation callback is never invoked. The editor is created lazily; shared control styles resolve after attachment rather than requiring application-global resources during construction.

Local verification on `develop`, baseline HEAD `6e4aca1f3bb5c36593911a23c7b678ceb162fd6b`:

| Evidence | Result |
| --- | --- |
| Focused Journal/Calendar/navigation | **493 passed**, zero failures/skips. Real SQLite covers All accounts and inactive account scope, save/complete/reopen/delete, external revision conflicts, marker refresh and preserved standalone drafts. Controlled reads verify cancellation and rejected late completion. |
| Full parallel Release | **2,882 passed**, zero failures/skips: Domain 444, Application 529, Infrastructure 789, Desktop 1,120. Native Calendar 86/86 and grid 36/36 passed within their unchanged child deadlines. |
| Build / schema / diff | Release build **0 warnings, 0 errors**; EF **no pending model changes**; `git diff --check` passed. No Application/Domain/Infrastructure/schema changes. |
| WPF renders / interaction tests | Expanded/collapsed Light/Dark at **1,100 DIP / 96 DPI** and **480 DIP / 240 DPI** inspected. Compiled tests check command bindings, scoped-resource construction, fields, form-before-Trades order, wheel handoff, reachable Trades and protected native modal closure. These are automated checks, not live acceptance. |
| Live UI / GitHub Actions | **Unverified**. No live desktop interaction was performed; no matching Actions run exists for this uncommitted work. A new hosted run requires the user's commit/push. |

Logs, TRX and synthetic renders are ignored local artifacts under `artifacts/inline-day-journal/` (`focused-complete`, `full-release`, `renders`). Initial integration runs found eager style lookup and a navigation guard that blocked cancellation of a pending Trade read; lazy rendering/deferred shared styles and a Journal-only shell guard fixed those causes. An obsolete null-command assertion now verifies the inline command remains disabled without Journal status. No assertions, timeouts or isolation were removed to pass.

Remaining **live Windows** checks (not established by automated renders):

1. Launch with a disposable `--isolated-data-root`. Select Saturday and an adjacent-month day, first with All accounts, then an inactive historical Account. Add/Continue must keep Calendar selected and reveal the exact-scope form inside Day Performance. Confirm chart and Trades remain reachable with Tab, wheel and the scrollbars.
2. Enter text and all three answers. Cancel, Close Journal, modal X/Close/Escape/backdrop must preserve every field when discard is declined. Cancel/Close Journal must never dismiss Day Performance. Repeat with an unsaved inline Trade edit.
3. Save, Complete, Open, Reopen and Delete synthetic entries; check compact content and Calendar indicators. Cancel Delete first. Use a second editor to create a newer revision: inline Save must conflict, retain all fields, and only discard after explicit Reload confirmation.
4. Leave an unsaved standalone Journal draft, use a different inline scope, then return to the standalone page; its draft must remain intact. Inspect both themes at normal and narrow/high-DPI sizes, including keyboard focus and screen-reader names. No current live interaction or GitHub Actions acceptance is claimed.

Changed files (repository-relative, this refinement):

- `README.md`, `docs/daily-journal.md`, `docs/trading-calendar.md`, `docs/desktop-ui.md`.
- Desktop `ViewModels/Calendar/CalendarViewModel.cs`, `CalendarViewModel.Journal.cs`, `CalendarViewModel.InlineTrade.cs`, new `CalendarViewModel.InlineJournal.cs`; `ViewModels/Journals/JournalViewModel.cs`; `ViewModels/MainWindowViewModel.cs`.
- Desktop `Views/Calendar/CalendarDayDetailsView.xaml`, `CalendarDayDetailsView.xaml.cs`, `CalendarDayDialogWindow.xaml.cs`, `CalendarView.xaml.cs`; new `Views/Journals/InlineJournalView.xaml` and `.xaml.cs`.
- Desktop tests `Calendar/CalendarDayModalJournalTests.cs`, `CalendarDayModalTests.cs`, `CalendarSummaryTests.cs`; new `Journals/CalendarInlineJournalSqliteTests.cs`, `InlineJournalViewTests.cs`, and `TestDoubles/FakeDailyJournalRepository.cs`.

## Calendar integration (M14.5)

`IDailyJournalStatusReader.GetAsync(from, through, accountId?, ct)` reads committed status for an inclusive visible-grid range of at most **42 dates**. A null Account selects **only** explicit All accounts journals. One fresh no-tracking query projects journal ID, trading date, `IsDraft` and revision, ordered by date; it never reads text, answers or Trades and makes no writes. Invalid ranges/empty IDs fail before querying. The reader is registered through `AddPersistence`; no new migration is required.

Calendar loads this batch independently of its financial data and currency filter. Dates with entries show compact **Draft** or **✓** indicators; Completed is conveyed by the accessible name, not visible cell text. No entry means no marker. Indicators do not own a tooltip, preserving the day-number-only hover tooltip. Adjacent-month dates and Saturdays use their own daily Journal status. Saturday retains weekly-only financial content. Unknown/loading/error status is not interpreted as no entry: the modal action stays unavailable with Refresh recovery until a valid exact-scope status read succeeds.

The Day Performance action says **Add Journal** for a new entry, **Continue Journal** for a draft and **Open Journal** for completed content. It now opens an **inline section below the chart and immediately above Trades**; the modal remains open and Calendar stays selected. No Account is inferred from a Trade, currency or the last Journal selection. Null Account remains the independent All accounts Journal.

After a successful exact-scope load, Add/Continue opens the writable form immediately. The modal reveals the fields after layout; its **single Save Journal / Cancel row sits below all four fields** and is reachable by keyboard and the modal scroller. There is no Complete review button. Previously Completed entries display saved content read-only until an explicit revision-checked Reopen; unavailable references remain protected. Save success records Completed and collapses the form to a compact saved view. Failed/cancelled/conflicting saves retain all four fields and offer guarded Reload latest. Cancel saves all four fields as Draft without a discard prompt and closes only after success. Empty never-saved forms close without a write; failed/conflicting/cancelled writes keep every field. **Close Journal** collapses the entire inline section without closing Day Performance. Delete uses the existing permanent-history confirmation and revision check; only a committed result removes the Calendar indicator.

Calendar creates a separate `JournalViewModel` for each inline session and applies date/Account together through `TryOpenScope`. This reuses persistence/validation/unsaved guards rather than introducing Calendar write rules. It does not share or discard the standalone Journal page's retained draft or history selection. Inline activation omits History and duplicate Trade-context reads because the modal already has its Trade table. Closing invalidates/cancels the session's reads; late completions cannot populate another session. Date/Account/month changes require the same unsaved-change decision. Currency changes and Trade refresh never rewrite Journal fields or scope. Loads revalidate availability without falling back to All accounts.

The modal's single protected closing path checks existing Trade loading/edit protection first, then the Journal save/unsaved guard. A veto leaves the window, fields and selection intact. X, Close, Escape and backdrop dismissal all use it. Saving blocks closure until its authoritative outcome. Chart, form and Trades share the modal's vertical viewport; bounded text fields scroll locally and hand off ordinary wheel input at their boundaries, leaving Shift/horizontal input unchanged. Close Journal is distinct from modal Close and from Delete. No History is embedded; the standalone page retains its existing navigation and Review History.

Only committed `Created`/`Updated`/`Deleted` results emit `JournalDataCommitted`, including a commit returned after cancellation was requested. Unchanged, invalid, conflicting, cancelled or failed writes do not emit it. The inline Calendar session consumes this notification to refresh status without replacing Trade data; standalone writes retain the shell's dispatcher-marshaled notification. Inactive Calendar rereads on return. Batch reads have cancellation/generation guards against stale results. Journal text and answers never enter the grid; M14.6 history remains on the standalone page and rereads on activation.

## Read-only Trade context (M14.3)

All eight headings stay on one line: Time (New York), Instrument, Account, Net P&L, Size, Direction, Setup, Trading Mistakes. Time and Direction are **132 / 96 DIPs**, with a **1,010-DIP minimum** table and shared header/row widths. Normal 1,100-DIP content fits every column; narrower pages scroll horizontally while retaining value wrapping and full-text tooltips. Nothing is hidden or removed. Close review and Close view elsewhere on the page use theme-aware red outlines with hover/focus/disabled states, distinct from solid-red Delete and ordinary Save/Cancel.

`JournalTradeContextViewModel` composes `ITradingCalendarDayReader` and `ITradingAccountReader`. It receives only the editor's **applied** date and exact Account scope; a rejected dirty-scope change or an uncommitted typed date cannot retarget it. A null Account filters no Trades: every Account contributes, but that does not aggregate or change the independent All accounts journal text. Inactive Accounts remain supported. An unavailable selected Account produces an explicit context error, never an All accounts fallback. There is no currency filter or Trade editing/navigation in this milestone.

`TradingCalendarDayQuery` reuses the existing New York midnight-to-next-midnight **half-open UTC range** over authoritative `ClosedAtUtc`, including 23-/25-hour DST days. The reader excludes open/partially exited Trades, has no browse-page limit, and returns newest closure first with Trade ID as the stable tie-breaker. Its fixed set of batch queries reads browse rows, peak execution/allocation Size, Setups and Mistakes; there is no per-Trade query. Account/Instrument activity metadata is carried in `TradingCalendarDayDetails.References` from the same left joins. Missing references are retained as unavailable; inactive and unassigned classification states remain explicit. The additional metadata is read-only and requires no schema change.

The panel displays daily closed-Trade count, separate per-currency Effective Net summaries, and each Trade's New York closing time, Instrument, Account, Net, Size, direction, Setup and Mistakes. It reuses `CalendarTradePresentation` and `CalendarPnlSummary`; it never recomputes economics from prices. Known Net remains authoritative; unknown Net with known Gross is visibly **Estimated**; unknown economics remain **—**, not zero. A genuinely zero Trade still has a row and count. Detailed time help keeps the actual timestamp's explicit UTC offset. Full names/classifications have tooltips, long text wraps, and the table scrolls horizontally when necessary. Read-only rows are keyboard-focusable and carry complete accessible descriptions; ordinary vertical wheel input reaches the Journal page.

Context loading, cancellation, empty and error states are independent of the editor. **Refresh Trades** and confirmed Trade create/edit/delete, Calendar inline edits, and successful Tradovate/TopstepX imports reload only context while Journal is active. Noncommitted import outcomes do not generate a commit refresh. Re-entry always reads current context. Each read captures the date/Account and a generation; scope changes, newer refreshes and deactivation invalidate older responses, including readers that finish after cancellation. Obsolete rows are cleared rather than shown under another scope. Context refresh never calls the journal repository, prompts to discard text, changes draft/revision state or changes the applied scope; journal saves also do not depend on a successful Trade read.

M14.3 itself adds no Calendar Add Journal activation, review workflow, history browser, Trade mutation or economics/schema change. M14.4 adds the review workflow described above; Trade context remains read-only.

## Review History (M14.6)

### All accounts History and Add/Continue accents, 2026-10-07

Baseline: clean `develop` at `41628aa5f52a16c26935db811abd9648f5c2a964`. The previous History query always compared Account ID with the selected nullable ID, so All accounts returned only null-scoped journals. Its row-open guard also rejected specific-account rows under a null filter. The reader now omits the Account predicate only for aggregate History browsing; the row command accepts current rows from that aggregate. The existing editor scope transition selects the stored date/Account before loading or writing. Journal uniqueness, revisions, Calendar status batches, Trade economics and schema are unchanged.

Add/Continue in standalone and inline editors reuse the established teal/mint primary style. Save remains green, Cancel/Draft violet, Reload/Refresh actions blue (History now uses distinct amber/slate/sky actions), Close red-outlined and Delete solid red. Compiled template tests exercise hover, pressed, disabled and keyboard-focus states. The no-entry card and Account help distinguish the separate All accounts journal from aggregate History.

Automated evidence uses synthetic data only, under ignored `artifacts/journal-history-all/`. Journal-focused Release: **327/327 passed** (Domain 49, Application 13, Infrastructure 65, Desktop 200), zero failures/skips. SQLite checks cover 9/10/11/20 mixed null/P 21/other entries, same-date ID ordering, database count/LIMIT/OFFSET, inactive/unavailable names, SELECT-only reads, and exact-account open/edit/Draft/Complete/delete without touching other scopes. Controlled reads verify cancellation and late-result rejection when switching both to and from All accounts. Existing revision/conflict, Calendar marker and navigation guards remain covered.

Automated Light/Dark component renders were inspected at **960 DIP / 96 DPI** and **480 DIP / 240 DPI** for compact entries, Add/Continue and mixed-account History. Inline render coverage uses 1,100/480 DIPs. Account labels wrap at narrow sizes with Open review reachable; button colors remain distinct. These are automated renders, not live mouse/keyboard or physical display-scaling verification. No GitHub Actions result for these uncommitted changes is claimed.

Full parallel Release: **2,938/2,938 passed**, zero failures/skips (Domain **449**, Application **529**, Infrastructure **791**, Desktop **1,169**). The native Calendar child completed in **113.329 seconds**, leaving **6.671 seconds** against the unchanged 120-second aggregate deadline. The initial wider focused run was **605 passed / 85 propagated failures**: that same child hit its aggregate deadline while starting `ViewExpandsExactTradeWhileKeepingModalOpen`; the preceding action finished in 1.807 seconds. This was not a Journal assertion failure or a demonstrated individual hang. Both sets of raw phase/process diagnostics remain in `focused` and `full-release`; passing the complete run does not resolve timing variability. No harness assertion, retry, isolation or deadline was changed. Release build: **0 warnings, 0 errors**. EF reports **no pending model changes**; the diff check passes.

Changed files (repository-relative):

- `README.md`, `docs/daily-journal.md`, `docs/desktop-ui.md`, `docs/trading-calendar.md`.
- Application `Journals/IDailyJournalHistoryReader.cs`; Infrastructure `Journals/DailyJournalHistoryReader.cs`.
- Desktop `ViewModels/Journals/JournalHistoryViewModel.cs`, `JournalViewModel.cs`; `Views/Journals/JournalView.xaml`, `InlineJournalView.xaml`, `JournalPresentationResources.xaml`.
- Desktop tests `Journals/JournalHistoryTestReader.cs`, `JournalHistoryViewModelTests.cs`, `JournalHistorySqliteTests.cs`, `JournalHistoryViewTests.cs`, `JournalButtonAssertions.cs`, `JournalViewTests.cs`, `InlineJournalViewTests.cs`.
- Infrastructure tests `Persistence/Journals/DailyJournalHistoryReaderTests.cs`.

Remaining live Windows checks, using a disposable `--isolated-data-root` only:

1. Create null-, P 21- and other-account entries, including identical dates. All accounts History must list all with their actual scopes and ten-entry paging; select one Account and verify its count/page reset. Rapid switches must not restore stale rows.
2. Open P 21 from All accounts: read-only viewing must preserve the aggregate selector/list/page. Explicit Open in editor selects P 21; Continue, Cancel/Draft, Save, Reopen and Delete must target P 21 only. Repeat with an unsaved All accounts form and decline discard; the local draft and scope must stay intact. Calendar All accounts markers must still represent only null scope.
3. In both hosts and themes, check teal/mint Add/Continue against blue ordinary actions at normal and narrow/high-DPI sizes, with Tab focus, hover and pressed/disabled states. Existing Save, Cancel/Draft, Close and Delete semantics must remain unchanged. A new GitHub Actions run requires the user's commit/push.

### Browsing and exact editor scope

The **Review History** expander uses the page's Account selection as a browsing filter: **All accounts includes every account-specific entry plus explicit null-scoped All accounts entries**; an individual Account includes only that Account's entries. Creation, the selected-day card and Calendar indicators still use the independent exact null scope. A card saying No entry for All accounts does not mean that no account-specific journal exists for that date. Changing the editor date does not filter history to that single date. Reviews are ordered by stored New York `TradingDate` descending, then journal ID. The database applies the filter, count, ordering and paging together; separate account pages are never merged. Rows retain their original Account ID and current readable name, with null labeled All accounts and inactive/unavailable references explicit. Snapshots retain text, answers, state and audits, not historical Account names.

**Open review** expands the read-only revision browser directly beneath that row, before the next row. Viewing, switching rows, Close review and Close view preserve the aggregate Account filter, total, order, ten-entry page and unsaved editor fields; no editor load or write occurs. Only **Open in editor** requests the stored date/Account through the editor's existing unsaved-change guard and shows the compact saved entry. Subsequent editing, Save, Cancel/Draft and Delete target that original entry, never a new null-scoped journal. Keep editing vetoes a different scope without losing freeform text or any answer. Discard changes is explicit. Opening a clean current entry reloads its latest committed version without opening the form; opening the same entry with dirty or conflicted content preserves the open form and does not bypass Reload latest. Completed entries still require explicit Reopen review before editing. A stale row button after a page/scope refresh cannot open another entry.

Once a review is opened, a separate newest-first revision page shows revision number, Draft/Completed state and its audit time in **New York**. The shared DST-safe formatter converts the stored UTC instant for presentation only, using the current culture's general date/time format (seconds, no fractional digits) and an explicit timestamp-specific **UTC-4** or **UTC-5** offset. The two autumn occurrences of the same local clock time remain distinguishable. The revision list, snapshot heading and accessible action descriptions share this format; timestamps remain stored in UTC and revision ordering is unchanged. **View revision** fetches only that immutable snapshot, with exact freeform text and all three structured answers in read-only, copyable controls. Viewing is not restoring: it never calls create/update, changes editor content/revision, completes/reopens a review or adds history. There is no restore-old-version action. Journal saves/completion/reopening refresh metadata; they do not copy a displayed historical snapshot into the editor.

The prominent **Close view** action dismisses only that snapshot (or its pending/error presentation). It retains the selected review, journal-entry page, revision page, date and exact Account scope, without reloading, restoring or writing. Any pending snapshot read is cancelled and generation-invalidated so a late result cannot reopen it. Unsaved editor text/answers are untouched; normal scope/history/navigation discard guards continue to apply.

**Close review**, beside the opened review heading, instead collapses the complete revision browser and any snapshot. It keeps the same journal-entry history page, scope/filter and selected editor date, with every unsaved text/answer field untouched. Revision and snapshot reads are cancelled and generation-invalidated, so late responses cannot reopen the review. Closing does not reload the editor, write, restore a revision or invoke a discard prompt. The heading wraps beside the keyboard-accessible action at narrow widths; **Close view** and **Close review** have distinct accessible names.

`IDailyJournalHistoryReader`, registered by `AddPersistence`, exposes:

| Read | Semantics |
| --- | --- |
| `BrowseAsync(accountId?, page, pageSize, ct)` | Null includes all scopes; non-null filters that Account exactly. Date descending then ID; database total and bounded page. No text/answers, Trades or per-entry revision query. |
| `BrowseRevisionsAsync(journalId, page, pageSize, ct)` | Revision metadata descending by revision; total count and bounded page. No snapshot text. |
| `GetRevisionAsync(journalId, revision, ct)` | One stored full snapshot, or null if unavailable. |

Page numbers are **one-based**; sizes **1–50**, with Desktop fixed at **10 journal entries** and **20 revisions** per page. Previous/Next use each list's own size and authoritative total. The general Application reader's default size remains 20; the Journal page explicitly requests 10 for entries. Invalid identifiers, sizes and overflowing offsets fail before querying. Empty or beyond-last pages remain empty without scope fallback. Each metadata page uses a count query and one `Skip`/`Take` projection, not one query per row. A selected snapshot uses one query. Fresh no-tracking contexts observe committed changes; cancellation propagates. All reads are independent of Trade queries and make no database writes. The schema and optimistic write rules are unchanged; no migration is required. The existing unbounded `GetHistoryAsync` remains a compatibility API, not the browser's read path.

List, revision-page and snapshot work run off the WPF dispatcher, each with a cancellation token and generation. Account/page/entry/revision changes or deactivation invalidate older work even if a reader returns after cancellation. Loading clears obsolete rows/content; failures show safe Refresh/View revision recovery without altering the editor. History cancellation is separate from editor Save/Reload and never discards its draft. Shared Light/Dark resources, wrapping controls and one page scrollbar keep the bounded lists and full read-only revision content reachable; Tab/Enter reach row actions and the expander has a visible focus border.

## Verification and later milestones

Focused Domain and isolated migrated-SQLite tests cover exact scope uniqueness (including direct database enforcement), empty trading dates, New York DST date identity, text/audit round trips, durable history, no-ops, stale and concurrent writes, inactive/missing Accounts, referential protection, cancellation/rollback and read-only retrieval. Migration tests verify an upgrade preserves existing trading data and a downgrade removes only the new journal tables.

M14.1 verification on 2026-10-04: **91 focused tests passed** (18 Domain, 54 persistence/schema, 19 Account presentation regressions). The complete Release suite passed **2,576 tests** (418 Domain, 516 Application, 753 Infrastructure, 889 Desktop), with no failures or skips. Release build had **zero warnings/errors**, EF reported **no pending model changes**, and `git diff --check` passed. Tests used isolated migrated SQLite databases; the real journal was not opened. Existing Desktop regression tests passing is not Journal editor or interactive acceptance.

The historical M14.1 results above do not establish later editor or interactive acceptance. M14.2 adds the explicit-save editor; M14.3 adds read-only Trade context; M14.4 adds structured review/completion; M14.5 adds exact-scope Calendar integration; M14.6 adds paged review/revision browsing. Subsequent refinements add permanent deletion and an inline Day Performance editor. Richer text, attachments, scope migration and revision restoration remain deferred. Calendar passes the selected date/Account explicitly, including All accounts, without treating currency filtering as a different journal scope.

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

### Journal presentation refinement verification, 2026-10-06

The initial worktree was clean `develop` at `1099c242baf753ea6e95755f490de85b92ca3d76`. This refinement changes Desktop presentation and its regression tests only; Journal persistence, revision rules, Trade economics, database model and test-harness deadlines are unchanged. No real journal or customer data was accessed.

| Gate | Current evidence |
| --- | --- |
| Focused automated checks | **272 passed**, no failures/skips: 268 Journal/history/navigation cases plus four direct compiled Calendar marker cases. Includes save/completion close, cancelled/failed/conflicting saves retaining all fields, explicit discard, completed/reopen behavior, exact scope, history snapshots, import/context refresh and stale results. |
| Complete parallel Release suite | **Not green: 2,807 passed / 36 failed / zero skipped**. Domain 444/444, Application 529/529, Infrastructure 779/779; Desktop 1,055/1,091. All 36 failures propagate the same `calendar-grid` child 120-second aggregate timeout, not 36 distinct assertion defects. |
| Child diagnostics | The final grid child completed assertions for 31 actions. Its next `HitTestingScopesDateHoverToMarkerNotDailyWeeklyOrEmptyCellAreas` action started with approximately 1.3 seconds left; the supervisor terminated and cleaned up the process tree. No individual operation hang was established. The native child passed **86/86 in 118.812 seconds**, leaving **1.188 seconds** against its unchanged 120-second deadline. An earlier full run completed grid 36/36 in 66.964 seconds and native 86/86 in 114.215 seconds, but exposed obsolete implicit-editor test setups subsequently corrected. Scheduling variation is observed; its precise remaining performance cause is not established by these measurements. |
| Build/model/diff | Release build: **zero warnings/errors**. EF: **no pending model changes**, using the in-memory design-time factory. `git diff --check` passed. |
| Automated renders | Journal Light/Dark at **960 DIP / 96 DPI** and **480 DIP / 240 DPI**; Calendar indicators Light/Dark at **1,100 DIP / 96 DPI** and **480 DIP / 240 DPI**. Rendered images were inspected. A render-discovered optional-history visibility binding issue was fixed by binding its containing section to the page ViewModel rather than the child history DataContext. |
| Live UI / GitHub Actions | **Unverified for these changes.** Native desktop interaction is unavailable in the current tooling. Automated WPF renders and routed/command checks are not live pointer, keyboard or screen-reader acceptance. No matching CI result is claimed. |

Local TRX, renders and child phase/supervision logs are under ignored `artifacts/journal-ui-refinement`; the final complete run is `full-release-final`, and the final focused run is `navigation-focused`. The aggregate deadline remains an open full-suite acceptance gate. No retry, skip, broader deadline, global serialization or speculative production workaround was added.

Changed files:

- Desktop: `ViewModels/Journals/JournalViewModel.cs`, `ViewModels/MainWindowViewModel.cs`, `ViewModels/Calendar/CalendarViewModel.Journal.cs`, `Views/Journals/JournalView.xaml`, `Views/Journals/JournalHistoryView.xaml`, `Views/Calendar/CalendarView.xaml`.
- Desktop tests: `Journals/JournalViewModelTests.cs`, `JournalViewTests.cs`, `JournalReviewViewModelTests.cs`, `JournalReviewSqliteTests.cs`, `JournalSqliteTests.cs`, `JournalCalendarIntegrationViewModelTests.cs`, `JournalCalendarSqliteTests.cs`, `JournalHistoryViewModelTests.cs`, `JournalHistorySqliteTests.cs`, `JournalTradeContextTests.cs`, `JournalTradeContextSqliteTests.cs`; `Calendar/CalendarDayModalJournalTests.cs`; `Navigation/CalendarJournalNavigationTests.cs`, `JournalNavigationTests.cs`, `JournalTradeRefreshTests.cs`, `MainWindowViewModelTests.cs`.
- Documentation: `README.md`, `docs/daily-journal.md`, `docs/trading-calendar.md`, `docs/desktop-ui.md`.

Remaining isolated Windows checklist (use a disposable `--isolated-data-root`, never the real journal):

1. In both themes, at normal and narrow/high-DPI sizes, verify compact entry/history browsing, the top Add Journal action, smaller internally scrolling text fields and reachable keyboard actions. Check All accounts, an inactive Account and an empty trading day.
2. Save and Complete: confirm the form closes to the saved entry. Continue a draft and Reopen a completed review; completed content must stay read-only until reopening succeeds. Verify revision snapshots remain read-only.
3. With unsaved text or answers, exercise Close editor, date/Account changes, another history review and navigation. Keep editing must preserve all fields and scope; only explicit Discard changes may restore/replace them.
4. With two instances sharing only the disposable root, provoke a stale revision; verify the open form retains every field and Reload latest requires explicit discard. Check cancellation and a safe induced save error independently.
5. Confirm Calendar shows Draft versus a check mark without visible Completed text, with full state announced accessibly, unchanged Saturday weekly content and day-number-only tooltips. Check focus visibility and a screen reader; these remain live acceptance tasks.

### Journal layout and history correction verification, 2026-10-06

Actual initial checkout: clean `develop` at `f73b8fb9cd53821c3cf50420f04b8cfe880039ba`, containing the prior presentation changes. Source confirmed the reported problems: HistorySection preceded the form, revision viewing lacked a close action, history lists had nested capped scroll viewers, and revision TextBoxes combined a 150-DIP maximum with their own scrolling. The correction moves history after the form/actions, removes those nested viewports and revision height caps, and routes ordinary wheel input once to the page only when a nested editor cannot use it. Snapshot fields remain one-way/copyable. Close view cancels only snapshot presentation, retaining the selected review and both pages.

| Gate | Result |
| --- | --- |
| Focused Desktop | **273 passed**, zero failures/skips: Journal/history/navigation, including 9/10/11/20-entry paging, scope/page preservation on Close view, pending snapshot cancellation, unchanged unsaved text, editor-before-history layout, full long answers, page/scrollbar navigation and single-step/boundary wheel routing. |
| Focused SQLite | **48 passed**, zero failures/skips: Journal repository/history cases, including four explicit 10-item page boundaries, stable order and SELECT-only reads. Editor-to-SQLite coverage proves closing a viewed revision does not add history or replace unsaved content. |
| Complete parallel Release | **2,852/2,852 passed**, zero failures/skips: Domain 444, Application 529, Infrastructure 783, Desktop 1,096. Workflow blame/diagnostic flags enabled; TRX and supervised-child logs remain in ignored `artifacts/journal-layout-fix/full-release`. |
| Calendar deadline investigation | Unchanged grid **36/36**, isolated **21.792 s**, full parallel **35.454 s** (84.546 s headroom); native **86/86**, full parallel **77.743 s** (42.257 s headroom). No deadline, retry, skip, concurrency or harness behavior changed. Prior timeout did not recur, but its precise cause is not established. See [separate timing evidence](ci-wpf-tests.md#calendar-grid-deadline-follow-up-2026-10-06). |
| Build/model/diff | Release build **zero warnings/errors**; EF **no pending model changes**, using the in-memory design-time factory; `git diff --check` passed. No migration required. |
| Automated visual evidence | Compiled Light/Dark renders inspected at **960 DIP / 96 DPI** and **480 DIP / 240 DPI**. Long revision tests bring the last answer's final lines into the viewport, then close without changing review/scope or local edits. Outputs are under ignored `artifacts/journal-layout-fix/renders`. Automated renders and routed-input tests are not live input acceptance. |
| Live UI / GitHub Actions | **Unverified for this correction.** Native desktop control is unavailable in the current tooling. A new matching GitHub run after the user's commit/push remains necessary; local green is not hosted CI green. |

Changed files: Desktop `ViewModels/Journals/JournalHistoryViewModel.cs`, `Views/Journals/JournalView.xaml`, `JournalView.xaml.cs`, `JournalHistoryView.xaml`; Desktop tests `Journals/JournalHistoryViewModelTests.cs`, `JournalHistoryViewTests.cs`, `JournalHistorySqliteTests.cs`; Infrastructure tests `Persistence/Journals/DailyJournalHistoryReaderTests.cs`; documentation `README.md`, `docs/daily-journal.md`, `docs/ci-wpf-tests.md`. Application paging bounds/defaults, persistence, revisions and economics are unchanged. No real journal/customer data was accessed and no Git write was performed.

Remaining isolated Windows checks (launch with a disposable `--isolated-data-root`):

1. In both themes and at narrow/high-DPI sizes, Add/Continue a Journal and confirm fields/actions precede history; test Tab, Page Up/Down, wheel and touchpad through the complete page.
2. Open a long revision, read/copy its last text and answer lines, then Close view. Confirm the same review, entry/revision pages, date and Account remain selected; a dirty form retains all four values.
3. Scroll inside a long editable field and past its boundary. The field should consume usable scrolling, then the page continues without a duplicate step; Shift/horizontal input is not redirected. Check screen-reader descriptions and keyboard focus.
4. Browse 10-entry pages, including a final partial or exact full page. Check scope changes and Keep editing/Discard protection independently of viewing/closing snapshots.

### Review History close and time verification, 2026-10-06

Actual checkout: `develop` at `08c6d80cf5f2d2e42286b6d32713844fbb9a8508`; existing work preserved, no Git writes. The opened review had no close action, and `JournalRevisionRow.SavedAtText` formatted the stored UTC instant directly with fractional seconds. This correction adds a separate Close review command using the existing selection/read invalidation path, and reuses the shared New York formatter with culture-aware second precision. Neither action changes persistence, revision concurrency, Account scope, economics or the database model.

| Gate | Result |
| --- | --- |
| Focused Desktop | **284 passed**, zero failures/skips: Journal/history/navigation, including Close review versus Close view, entry page and exact scope retention, all four unsaved fields, pending revision/snapshot cancellation and late-response rejection. Eight timestamp cases cover spring/fall DST boundaries in en-US and bg-BG, including both repeated autumn clock times and unchanged source instants. |
| Isolated SQLite | Included in focused and full suites: Save/Complete/Reopen, view and both close actions retain exactly three durable revisions, leave unsaved editor content intact, and create no Trade data. All databases are disposable migrated fixtures. |
| Complete parallel Release | **2,863/2,863 passed**, zero failures/skips: Domain **444**, Application **529**, Infrastructure **783**, Desktop **1,107**. TRX, VSTest diagnostics and supervised-child output are under ignored `artifacts/journal-close-review/full-release`. |
| Build/model/diff | Release build **zero warnings/errors**; EF **no pending model changes**, using the in-memory design-time factory; `git diff --check` passed. No migration or test-harness change. |
| Automated visual/layout evidence | Compiled Light/Dark renders inspected at **960 DIP / 96 DPI** and **480 DIP / 240 DPI**. The Close review heading/action fit within the viewport, remain keyboard-focusable/tab stops and reachable through the page scrollbar; Close view retains the revision browser, while Close review collapses it. Long snapshot text remains reachable. Render-tree visibility checks use the bound Visibility, arranged size and viewport coordinates, not IsVisible on a detached root without a native PresentationSource. Outputs are under ignored `artifacts/journal-close-review/renders`. |
| Live UI / GitHub Actions | **Unverified for these changes.** Native desktop interaction is unavailable in the current tooling. Automated render/command checks are not live keyboard, pointer or screen-reader acceptance. A new matching GitHub Actions run after the user's commit/push is required. |

Changed files: Desktop `ViewModels/Journals/JournalHistoryViewModel.cs`, `Views/Journals/JournalHistoryView.xaml`; Desktop tests `Journals/JournalHistoryViewModelTests.cs`, `JournalHistoryViewTests.cs`, `JournalHistorySqliteTests.cs`; documentation `README.md`, `docs/daily-journal.md`. No real journal or customer data was accessed.

Remaining isolated Windows checklist (launch with a disposable `--isolated-data-root`):

1. In Light/Dark and normal/narrow-high-DPI layouts, open a review and a revision. Tab/Enter or the access key must reach **Close review**; **Close view** must dismiss only the snapshot. Close review must collapse revisions/snapshot while retaining the same entry page, date and Account.
2. Repeat with unsaved freeform text and all three answers. Neither close action may change the draft, prompt to discard it, save or restore a revision. Check exact All accounts versus Account-specific scopes.
3. Check revision rows, snapshot headings and accessible action names announce **New York** and the timestamp-specific UTC offset, with no UTC+0 or fractional-second suffix. Confirm UTC-4/UTC-5 distinguish the repeated autumn hour; history ordering remains unchanged.

### Journal readability verification, 2026-10-06

Initial checkout: clean `develop` at `8979c203964b0822a820204f2a4801bcef8bdc83`, including the prior close/time changes. This refinement is Desktop presentation only. New ViewModel properties format the selected trading date, entry state and history metadata; they do not change reads, paging, writes, audits, scope, Trade economics, Calendar totals or the schema. Existing work was preserved; no commit/push or real journal access.

| Gate | Evidence |
| --- | --- |
| Focused Desktop | **286/286 passed**, zero failures/skips: Journal/history/navigation, including culture-aware explicit-date headings, No entry/Draft/Completed, save/conflict/dirty-scope guards, Close review versus Close view, NY revision audit formatting and stale results. |
| WPF layout/interaction | Existing compiled tests expanded with 16-/22-DIP typography, 760-DIP reading width, untruncated 45-line saved notes and last-line reachability, hidden empty error spacing with visible real errors, aligned five-column headers/rows, hover entry/exit theme brushes, reachable row actions and three pages of 21 inactive-Account reviews with long names. Ten entries on pages one/two, one on page three; Previous/Next states and exact row targets retained. Narrow metadata wraps rather than overlapping. |
| Complete parallel Release | **2,865/2,865 passed**, zero failures/skips: Domain **444**, Application **529**, Infrastructure **783**, Desktop **1,109**. Workflow blame/diagnostic flags enabled; evidence remains under ignored `artifacts/journal-readability/full-release`. Test-harness concurrency/deadlines/coverage are unchanged. |
| Build/model/diff | Release build **zero warnings/errors**; EF **no pending model changes**, using the in-memory design-time factory; `git diff --check` passed. No migration needed. |
| Automated renders | Inspected Light/Dark at **960 DIP / 96 DPI** and **480 DIP / 240 DPI**: saved/editor/empty states, readable notes and history with action/footer visibility. Renders under ignored `artifacts/journal-readability/renders`; these are RenderTargetBitmap/layout and routed-state checks, not live desktop input or real display-scale acceptance. |
| Live UI / GitHub Actions | **Unverified for this refinement.** Native desktop interaction is unavailable in current tooling. A new matching GitHub Actions run after the user's commit/push remains required; local success is not hosted CI acceptance. |

Exact changed files:

- Desktop ViewModels: `src/PersonalTradingJournal.Desktop/ViewModels/Journals/JournalViewModel.cs`, `JournalHistoryViewModel.cs`.
- Desktop views/resources: `src/PersonalTradingJournal.Desktop/Views/Journals/JournalView.xaml`, `JournalHistoryView.xaml`, `JournalHistoryView.xaml.cs`, `JournalTradeContextView.xaml`, new `JournalPresentationResources.xaml`.
- Desktop tests: `tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalViewModelTests.cs`, `JournalViewTests.cs`, `JournalHistoryViewTests.cs`.
- Documentation: `README.md`, `docs/daily-journal.md`.

Remaining isolated Windows checklist (use a disposable `--isolated-data-root`, never the real journal):

1. In both themes at normal/narrow/high-DPI sizes, verify the date heading, state badge and Add/Continue/Reopen actions. Expand Account help by keyboard; check contrast, focus and screen-reader names.
2. Read long saved notes and revision snapshots through their final lines using wheel, scrollbar, touchpad and keyboard. Edit/save/complete/reopen normally; test a failed/conflicting save and explicit unsaved-discard protection independently.
3. Browse more than ten exact-scope entries with long/inactive Account names. Check wide header alignment, narrow stacked rows, right-edge Open review, Refresh and footer navigation. Close view must keep the review open; Close review must preserve entry page, date, scope and local edits.

## Journal actions verification (2026-10-06)

Initial checkout: clean `develop`, HEAD `0e3773a2aab4896e740adf581a8448450467999d`. No real journal access, Git writes, schema change, Trade/import changes or Calendar financial changes. Calendar's exact-scope editor-launch path already existed and was retained; the form now reveals its actions/fields rather than an old History scroll offset.

| Evidence | Result |
| --- | --- |
| Persistence | 63 focused Journal Infrastructure tests passed. Added isolated migrated SQLite cases for permanent removal of root/history, exact All accounts versus inactive Account scope, same-scope recreation with a fresh ID, missing/stale IDs, concurrent update/delete, invalid commands, pre-cancellation and injected cancellation/failure after history removal. Both tables roll back together. Existing orphan test also verifies deletion never retargets All accounts. |
| Desktop | **293 focused Journal/navigation tests passed**, zero failures/skips. Coverage verifies confirmation date/scope/history warning, confirmation cancellation, failure/conflict/missing preservation, overlap guards, late cancellation after commit, history/status refresh and immediate Calendar Add-editor opening. Save/Cancel/unsaved protections and completed read-only behavior remain covered. |
| Full parallel Release | **2,878/2,878 passed**, zero failures/skips: Domain **444**, Application **529**, Infrastructure **789**, Desktop **1,116**. Calendar native child exited successfully in **97.996 seconds** against the unchanged 120-second deadline; grid child also passed. Raw TRX/diagnostics are ignored under `artifacts/journal-actions/full-release`. No test isolation/deadline/concurrency changes. |
| Build / model / diff | Release build **0 warnings, 0 errors**. EF: **no pending model changes**, using the in-memory design-time factory. `git diff --check` passed. |
| WPF | Compiled Light/Dark layouts cover 1,100-DIP Trade context and 960-DIP editor/history at 96 DPI, plus 480 DIP / 240 DPI. Headers are measured against their actual glyph width and remain one line, all row widths align, narrow horizontal scrolling reaches the final column, and top form actions use the existing guarded commands. Red Close styles preserve focus chrome and are distinct from Delete. Generated renders were inspected; this is not live input/display-scale acceptance. |
| Live UI / GitHub Actions | **Unverified for these changes.** Native live desktop control is unavailable in current tooling. A matching hosted run requires the user's commit/push; local green tests are not GitHub CI acceptance. |

Changed files (repository-relative):

- `README.md`, `docs/daily-journal.md`, `docs/trading-calendar.md`.
- `src/PersonalTradingJournal.Application/Journals/IDailyJournalRepository.cs`, `DailyJournalWriteStatus.cs`, new `DeleteDailyJournalCommand.cs`.
- `src/PersonalTradingJournal.Infrastructure/Journals/DailyJournalRepository.cs`.
- `src/PersonalTradingJournal.Desktop/ViewModels/Journals/JournalViewModel.cs`.
- `src/PersonalTradingJournal.Desktop/Views/Journals/JournalView.xaml`, `JournalView.xaml.cs`, `JournalTradeContextView.xaml`, `JournalHistoryView.xaml`, `JournalPresentationResources.xaml`.
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Journals/DailyJournalRepositoryTests.cs`, new `DailyJournalDeletionTests.cs`.
- `tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalViewModelTests.cs`, new `JournalDeletionViewModelTests.cs`, `JournalViewTests.cs`, `JournalTradeContextViewTests.cs`, `JournalHistorySqliteTests.cs`, `JournalHistoryViewTests.cs`, `JournalCalendarIntegrationViewModelTests.cs`, `JournalHistoryViewModelTests.cs`, `JournalReviewViewModelTests.cs`, `JournalTradeContextTests.cs` (unrelated fake repositories only gain an unsupported-delete stub).
- `tests/PersonalTradingJournal.Desktop.Tests/Navigation/CalendarJournalNavigationTests.cs`, `JournalNavigationTests.cs`.

Remaining isolated Windows checklist:

1. Start with a disposable `--isolated-data-root`. In Calendar select a date without an entry, with All accounts and then an individual Account. Add Journal now keeps Day Performance open and reveals its inline form with Save Journal/Cancel. Test Cancel → Keep editing/Discard, save, close the Journal section, and verify retained month/date/filters/status. This replaces the earlier modal-close/navigation expectation.
2. In both themes, navigate long History then open a form; confirm the fields/actions are revealed. Check Trade context headings at normal size and scroll to Trading Mistakes at narrow/high-DPI size. Verify keyboard focus, horizontal access, wheel routing and screen-reader names.
3. Open a review and a revision: red Close view closes only the snapshot; red Close review closes both. Check hover/focus/disabled styling. Neither changes text or writes.
4. On synthetic Draft and Completed entries, cancel Delete confirmation first. Then confirm the exact date/scope and permanent-history warning; verify History/Calendar removal and recreate the same scope. With a second editor committing a newer revision, stale Delete must fail and retain local text until explicit Reload. Never use a real journal for this acceptance.
