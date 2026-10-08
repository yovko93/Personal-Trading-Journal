# M14 Journal acceptance

Audit date: **2026-10-08**. Branch: `develop`. Actual local and remote HEAD: **`c7cc44f110550c9e96396bdcafa54850d9064824`**. The worktree was clean before this audit. The production executable reports product version `1.0.0+c7cc44f110550c9e96396bdcafa54850d9064824`.

## Decision and scope

**Focused automated checks passed. Live end-to-end acceptance is blocked. Matching GitHub CI is absent. M14.7 remains open.**

The milestone scope was reviewed in [Daily Journal](daily-journal.md) (M14.1 persistence through M14.6 history), [Desktop's next-milestone notes](desktop-ui.md#next-milestone), README and the subsequent Journal refinements. No separate tracked roadmap file was found. This acceptance step adds no features. No application defect was reproduced; the confirmed documentation defects were stale descriptions of immediate Draft writes on Reopen, removed Reload latest/Close view buttons, and exact-only Calendar indicators. Current behavior is now described consistently in the Journal specification. Historical dated test records remain historical.

Only documentation changes were made: `README.md`, `docs/daily-journal.md`, and this file. No new regression test is warranted for a documentation correction. No production, test-harness, schema, Trade economics or import rule changed.

## Scenario matrix

Passed refers to the existing automated tests rerun for this audit. The live columns explicitly cover both themes; blocked means no actual mouse/keyboard result is available. Automated render and native-window tests are not live acceptance.

| Requested scenario | Automated | Light live | Dark live | Concise evidence |
| --- | --- | --- | --- | --- |
| Standalone create and Save as Completed on an empty trading day | Passed | Blocked | Blocked | `JournalSqliteTests`, `JournalReviewSqliteTests`, `JournalReviewViewModelTests`: exact fields, scope, state and durable revisions. |
| Required Journal text; optional answers; readable legacy entries | Passed | Blocked | Blocked | Domain/repository and `JournalReviewViewModelTests`: answers alone cannot complete; failed validation preserves form. |
| Edit/Reopen a Completed entry | Passed | Blocked | Blocked | `ExplicitReopenIsLocalAndSaveWritesOneRevisionFromLoadedCompletedContent`: Reopen itself writes nothing. |
| Cancel unchanged or change-then-revert | Passed | Blocked | Blocked | `JournalCompletedCancelTests`: all four fields, both hosts, no new revision or Completed-to-Draft change. |
| Cancel changed content as Draft; new empty Cancel | Passed | Blocked | Blocked | `JournalDraftCloseTests`, `JournalCompletedCancelTests`: atomic Draft save, empty-new no-op, failure/conflict retention. |
| Refresh and conflict recovery | Passed | Blocked | Blocked | `JournalCombinedRefreshTests`: selected entry and History update; declined dirty guard leaves state intact. |
| Delete Journal, confirmation cancellation and stale token | Passed | Blocked | Blocked | `JournalDeletionViewModelTests`, `JournalHistorySqliteTests`: exact identity, permanent history policy, atomic commit/rollback. |
| All accounts and specific-account History filters | Passed | Blocked | Blocked | `JournalHistorySqliteTests` and Infrastructure history reader: all scopes vs exact account, inactive/unavailable names. |
| Ten-entry paging and stable order | Passed | Blocked | Blocked | `ReviewPagesContainTenEntriesWithStableOrderAndExactNavigation` and SQLite 9/10/11/20-entry boundaries. |
| Open/Close review, switching rows and unsaved editor preservation | Passed | Blocked | Blocked | `JournalHistoryViewModelTests`, compiled `JournalHistoryViewTests`: aggregate page/filter retained, in-place row expansion. |
| View/Close revision, switching and pending reads | Passed | Blocked | Blocked | `JournalHistoryRevisionActionsTests`: one expanded row, read-only snapshot, late-result cancellation and label reconciliation. |
| Delete older revision; protect latest and future numbering | Passed | Blocked | Blocked | Revision writer/repository tests and `DeleteConfirmationCancelThenCommitReconcilesLastPageAndClosesDeletedSnapshotOnly`. |
| Several account journals on one Calendar date, including null scope | Passed | Blocked | Blocked | `CalendarDayJournalsTests`, `CalendarAggregateJournalTests`: exact-account list vs All accounts aggregate, no-Trade/error independence. |
| Inline Add/Open/Close/edit, Save and Cancel | Passed | Blocked | Blocked | `CalendarInlineJournalSqliteTests`, `JournalFormAccountTests`, `InlineJournalViewTests`: exact scope, account moves/collisions and in-place expansion. |
| Delete collapsed or expanded Calendar card; indicator refresh | Passed | Blocked | Blocked | `CardDeleteConfirmsExactScopeAndRemovesOnlyItsEntryAndIndicatorContribution`: only target removed; unrelated draft retained. |
| Committed writes/import refresh; rejected writes and stale responses | Passed | Blocked | Blocked | Calendar/Journal integration and Trade-context tests: controlled commit notifications, cancellation/generation rejection, unsaved text retained; no live import exercised. |
| Narrow layout, scrolling, focusable actions and unsaved-close guards | Passed | Blocked | Blocked | Compiled Journal/History/inline views and ViewModel modal-close guards; logical/routed/automation assertions with isolated synthetic data. |
| Real monitor scaling, pointer, keyboard, touchpad and screen-reader behavior | No live substitute | Blocked | Blocked | RenderTargetBitmap at 240 DPI checks layout; it does not establish physical display scaling or live input. |

There are no failed assertions in the current focused run. The helper failure and absent CI run are acceptance blockers, not observed product failures. Earlier user-confirmed Calendar hover/modal controls and UTC-4 display remain the limited historical observations recorded in [M13 acceptance](m13-acceptance.md); they do not certify these Journal workflows.

## Isolated startup and live-tool evidence

The Windows computer-use skill was read and its supported `@oai/sky` initialization attempted. The Node kernel exited before app discovery. Recovery attempted initialization again, then a kernel reset and one final initialization. The diagnostic was:

```text
node_repl kernel exited unexpectedly
windows sandbox failed: helper_unknown_error: setup refresh had errors
```

No window capture, app input, live theme switch or mouse/keyboard workflow was possible. No alternate custom input helper was used. App startup was checked separately through the real Release executable with `--isolated-data-root`:

| Launch | Disposable root under ignored `artifacts/m147-acceptance/` | Evidence |
| --- | --- | --- |
| Light | `startup-Light-1791457397546` | PID 5608; input-idle returned true; native window handle observed. Log: effective theme Light, database initialization and fresh Dashboard SELECTs. |
| Dark | `startup-Dark-1791457397546` | PID 15876; input-idle returned true; native window handle observed. Log: effective theme Dark, database initialization and fresh Dashboard SELECTs. |

Both roots have their own `PersonalTradingJournal/journal.db`, settings, logs and screenshot directories. They are not the user's LocalAppData journal and contain no customer data. Only the two launched processes were closed; Dark required stopping that owned PID after a bounded close request. That process cleanup is not evidence of a successful UI Close workflow. Disposable startup files remain ignored for diagnostics; repeat live acceptance with a fresh root.

## Automated verification

- Focused Release: **411 passed**, **0 failed**, **0 skipped** — Domain 54, Application 13, Infrastructure 80, Desktop 264. Filter: `FullyQualifiedName~.Journals.|FullyQualifiedName~CalendarJournalTests`; solution tests ran normally in parallel. No retries, deadlines, skips or concurrency settings changed.
- Release solution build: **0 warnings, 0 errors**. EF `has-pending-model-changes`: **no pending model changes**. `git diff --check`: passed.
- Current generated Light/Dark History/revision and Day Performance card images were inspected at **960/1100 DIP, 96 DPI**, and **480 DIP, 240 DPI**. Distinct toggle/delete styles, exact account labels, wrapping actions and in-place detail are visible. Compiled tests verify full-content scroll reachability and accessibility state. Files are ignored under `artifacts/m147-acceptance/renders/`.
- Current TRX/logs: `artifacts/m147-acceptance/focused/`, `focused.log`; build log: `artifacts/m147-build.log`; EF log: `artifacts/m147-acceptance/ef.log`.
- No code changed, so the complete 3,007-test matrix was not rerun solely for documentation. The preceding full parallel **3,007/3,007** result is retained in the dated revision/card verification section of Daily Journal. It is not a new full-suite or GitHub result for this audit.

## GitHub Actions gate

The repository-wide API query for exact `head_sha=c7cc44f110550c9e96396bdcafa54850d9064824` returned **`total_count: 0`**. Remote `develop` matches that SHA. Both the open-PR query and open-PRs targeting `main` query returned an empty list.

`.github/workflows/ci.yml` runs for **pushes to `main`** and **pull requests targeting `main`**. It has no `develop` push or manual-dispatch trigger. No triggers were changed. Latest successful [CI #77](https://github.com/yovko93/Personal-Trading-Journal/actions/runs/37210548636) tests **`9fbbc9220f742ceda1debfdd93715d55a7a36667`**, an older M13 `main` commit. Latest successful develop PR run #76 tests `f724b49f2a14943e29aa063acad2efd07a428cfb`, also before the Journal work. **There is no matching CI run SHA to report.**

Smallest next action for the already-pushed Journal code: **open a PR from `develop` to `main`** and let the existing workflow run; no merge is required. To include this uncommitted acceptance documentation, the user first commits these three documentation files and pushes `develop`, then opens or updates that PR. Record its head SHA and the workflow's tested SHA (which may be a PR merge SHA) before accepting the result. A green older run or local tests cannot fill this gate. No commit, push, PR creation, merge or `main` modification was performed in this audit.

## Remaining isolated Windows checklist

Use a fresh disposable directory and launch the built app explicitly; never launch the ordinary data path for this checklist:

```powershell
$acceptanceRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('ptj-m147-' + [guid]::NewGuid().ToString('N'))
& .\src\PersonalTradingJournal.Desktop\bin\Release\net10.0-windows\PersonalTradingJournal.Desktop.exe --isolated-data-root $acceptanceRoot
```

1. Create synthetic Accounts P 21 and Other. In Journal choose an explicit New York date with no Trades. Add, type Journal text, Save; verify Completed, collapsed form and one revision. Reopen, Cancel unchanged and change-then-revert; verify no extra revision. Reopen, change an answer, Cancel; verify Draft. Continue and Save; verify Completed. Repeat validation with answers only; fields must remain intact.
2. Create entries for null, P 21 and Other scopes on the same date, then enough other dates for **11 entries**. All accounts History must show all scopes and 10/1 paging; exact P 21 must filter/count correctly. Open/Close review and View/Close revision with mouse and Enter/Space; preserve page/filter/editor draft. Delete an older revision after first declining; latest must stay disabled, snapshots remain read-only, later saves must not reuse numbers.
3. In Calendar All accounts select that date. Verify every card above Trades. Open/Close different cards, Add into a free scope, edit, unchanged/changed Cancel, Save and Delete. Verify target account, indicator/count refresh, retained modal, other cards and an unrelated dirty form. Repeat under exact P 21; currency must not change journal scope.
4. Test Refresh after a second isolated instance writes a newer revision. Decline dirty-discard and verify all fields stay; stale Save/Delete must explain the conflict. Accept guarded Refresh and verify current content/count. Confirm deletion only for synthetic entries, then recreate the same date/scope.
5. Leave dirty forms through date/editor changes, navigation, modal Close/X/Escape/backdrop and card switches. Keep editing must veto the transition with fields and toggle labels intact; explicit discard must proceed. After accepted standalone departure, re-entry defaults to New York today/All accounts. Check Tab focus returns to the surviving row/date action.
6. Repeat key flows in Light and Dark via Appearance settings. At normal and narrow supported window sizes and actual high display scaling, wheel/touchpad/keyboard-scroll through long text, revision snapshots, inline forms and Trades; reach bottom actions and horizontal columns. Record actual viewport/DPI and screenshots. Mark each live matrix cell Passed/Failed with concise observations and reproduce any defect before modifying code.

M14.7 closes only after these live results and matching CI evidence are recorded. Current automated checks and startup evidence remain available while those gates are open.
