# Trading Rules — contracts, persistence and manual assessments

M18.1 established the Domain/Application foundation; M18.2 adds SQLite persistence and data portability; M18.3 adds Desktop management under Planning → Rules; M18.4 adds manual Trade/day assessments. There is no automatic evaluation or AI integration. Historical revisions are read exactly as recorded.

## Rule identity and scope

A Rule has a stable `RuleId` and immutable numbered `TradingRuleRevision` values (contract version 1). A revision contains a name, human-written criterion, description of required evidence, applicability to **one Trade** or **one New York trading day**, an explicit All-accounts or exact-Account scope, an evaluation mode, active/archive state, and UTC creation time. Create starts at revision 1. Editing, archiving, and reopening each append the next revision; an archived rule cannot receive new assessments. Revision numbers never move backward or get reused. A saved assessment embeds the exact revision, so a later rename, scope edit, or archive cannot rewrite what was evaluated.

All accounts means an explicit aggregate scope, not a missing Account ID. An exact-Account rule may only assess that Account's Trade or date/scope. An All-accounts rule may assess an exact Account or an All-accounts day. The target for a day is a `DateOnly` New York calendar date, never a machine-local instant. Later evidence readers must use Calendar's New York half-open midnight bounds and its closure-date attribution: a Trade spanning midnight belongs to its New York **closure** date, including 23- and 25-hour DST days. Open or partial activity is not silently counted as a closed Trade. No currency conversion is implied by an aggregate scope.

Example: “For NQ Trades in Account A, wait for the documented entry confirmation.” Its criterion and the execution/plan evidence needed to assess it are text, **not** executable expressions. A second rule could apply to a New York day: “Review the day's closed Trades and record whether the daily loss limit was respected.” The day rule must state what evidence is needed; incomplete commissions/fees or ambiguous closing times cannot be treated as known zero or resolved by inference.

## Assessment semantics

| State | Meaning |
| --- | --- |
| Not assessed | No assessment record exists. It is not stored as an assessment outcome. |
| Confirmed violation | Supported by referenced recorded evidence, or explicitly entered by a user and labeled `UserEnteredViolation`. |
| No violation | Evaluated with sufficient, referenced evidence and passed. It is not implied by merely linking a Setup or Playbook. |
| Cannot evaluate | Required facts are missing or ambiguous; the explanation identifies the gap. It is neither a pass nor a violation. |

Each append-only assessment has its own stable ID, optional superseded assessment ID, target Trade ID or explicit New York day, original Account scope, UTC time, source (`Manual` or future `AutomaticCandidate`), basis, evidence sufficiency, explanation, and source references. It freezes the full rule revision. An evidence-based pass/violation requires a sufficient-evidence declaration and at least one non-Playbook-only source reference. A user-entered violation is manual and explicit; it can be recorded despite missing market facts. A `CannotEvaluate` assessment requires `Missing` or `Ambiguous` sufficiency. This contract does **not** claim that a supplied reference proves the wording of an assessment. Future evidence verification must check that cited facts exist, belong to the target Account/date and relevant currency, and actually satisfy the rule before recording an evidence-based pass or violation. A Setup association alone never verifies adherence.

The Application store interfaces require atomic compare-and-append of the latest rule revision and, for assessment transitions, both the current active rule revision and previous assessment ID. A stale write returns `Conflict`; a pass attempted with absent/ambiguous evidence returns `MissingEvidence`; a missing or archived rule has distinct results. Domain validation rejects blank or oversized definitions, invalid scopes/targets, mismatched applicability, unsupported outcomes, and impossible evidence combinations. Cancellation is passed to reads/writes. M18.2 wires the repository and use cases through `AddPersistence`.

## M18.2 — SQLite persistence and portability

Additive migration `20261012163222_AddTradingRules` adds `TradingRuleRevisions` and `TradingRuleAssessments`. Existing Accounts, Trades, Journals, Setups, and economics are untouched; no assessment is backfilled. Each definition is stored on its numbered revision, including active/archive state, applicability, exact scope, original Account label, and UTC time. An assessment has a restrictive composite foreign key to that exact immutable revision, its original Trade/day target, source/basis/outcome/sufficiency, explanation, source references, original Account label, recorded UTC time, and the preceding assessment ID. Numbered assessment sequences are local to a Rule and exact target.

SQLite writer reservations cover validation through commit. The repository rechecks the latest Rule and previous assessment, verifies that the supplied frozen definition is identical to the stored revision, validates actual target ownership, and resolves cited sources in the same transaction. SQL checks and triggers additionally reject updates/deletes, reused or skipped revision/assessment numbers, invalid lifecycle transitions, invalid scopes, stale active-rule revisions, and outcome zero (`NotAssessed`). Cancellation before commit rolls back; after the final cancellation check, commit finishes without interruption. Storage exceptions propagate, with the transaction rolled back. No retry is added.

An existing inactive Account is valid for historical work. A missing Account blocks creation/edit/reopen of an active exact-scope Rule and new exact-scope assessments. Its existing Rule can still be archived without changing scope. All-accounts definitions and exact-account definitions remain separate; `ITradingRuleReader.ReadCurrentAsync` uses an exact scope filter, with a null filter meaning all definitions. Reads use a consistent SQLite snapshot, pages of 1–100 rows, Rule-ID ordering for current definitions, descending revision keysets for Rule history, and descending sequence keysets for assessments of a Rule/target. Evidence collections are limited to 1,000 references per assessment and resolved in bounded SQL batches rather than per-source queries.

### Source retention and deletion

Trade and Account deletion paths keep their existing behavior. Assessments and Rule revisions are retained indefinitely in M18.2, with **historical identities**, not cascading foreign keys to mutable sources. Single-Trade and Delete All Trades operations therefore cannot erase assessments; removing an Account or creating a different Account with the same name cannot retarget them. Reads expose `Available`, `Changed`, or `Unavailable` for the target and each evidence reference. They retain the original IDs, scope, Account label, currency, and recorded source date. Fingerprints detect changes to the referenced record; an old outcome is never recalculated or described as a fresh assessment. These are identity/provenance snapshots, **not complete copies of deleted Trade/Journal content**. The explanation and exact Rule definition remain readable; unavailable source content cannot be reconstructed from a hash.

Reference conventions:

- Trade/Execution: original GUID, no source revision; ownership, currency, closure-date/activity attribution and nullable facts participate in source validation/fingerprinting. Unknown costs remain null; no new Net calculation is introduced.
- Journal: original Journal GUID and a required, positive saved revision. Null-scoped Journals may be cited by an All-accounts day; an exact-Account target cannot cite a different or null scope. Later Journal edits do not replace a cited old revision; deletion makes it unavailable.
- Playbook capture: `SourceId` is the Trade GUID; positive `Revision` is the immutable **association sequence**, not the Setup revision. Null `Revision` explicitly identifies the original legacy capture. Removal/classification-only transitions are not captures. A capture reference alone cannot prove compliance.

Day references follow the existing New York closure-date rule. For open/partial Trades, only actual execution activity on the selected date is eligible evidence; it remains excluded from closed realized results. Cross-account references are rejected for an exact target. An All-accounts day retains each source's currency and Account; it never creates a converted or combined monetary total. Referential validation checks identity, scope and date, not whether arbitrary prose criteria are satisfied. Assessment sufficiency remains an explicit manual declaration until a later, separately scoped evaluator exists.

### Backups and exports

Complete backup archive format remains **v1**: it already stores the whole consistent SQLite database, migration list, and referenced screenshots. Current-schema preflight/restore preserve the new tables and triggers, verified by synthetic round trips including the retained recovery archive. An archive ending at the previous M17 migration is recognized as requiring staged migration and rejected without changing the installed data. Older-schema restore remains deferred; installed-database migration does not imply archive compatibility.

Portable logical export advances from **v4 to v5** and adds `tradingRuleRevisions` and `tradingRuleAssessments` to `journal.json`, including every stored column and the complete `evidenceJson` identity metadata. The four existing CSVs are unchanged; Rules detail is JSON-only. Earlier export versions remain valid documents for their original schema but contain no Rules data and are not importable/restorable backups. Exports and backups remain unencrypted sensitive local data and exclude provider credentials.

## M18.3 — Desktop management

Open **Planning → Rules**. The existing catalog lists active and archived definitions in deterministic 20-row pages, with the selected Rule's name, state, target, Account scope, current revision and actions given priority. **Create rule** defaults explicitly to All accounts and manual-only eligibility. Choose **One Trade** or **New York trading day**, then supply Rule name (1–120 characters), Criterion (1–4,000) and Required evidence (1–2,000); whitespace-only input is invalid. Limits and normalization follow the M18.1 definition. Inactive Accounts remain selectable and labeled; saved definitions for missing Accounts remain readable with an unavailable label. Those unavailable Rules may be archived but cannot be edited or reopened until the original Account is available; no same-name substitution occurs.

**Save rule** uses the expected revision through the M18.2 use case. It reads the exact committed revision back, compares its identity, definition, lifecycle and UTC time, then reloads current state and history before reporting success. Conflict, validation, cancellation and storage failures retain all entered fields. If commit succeeds but readback fails or is cancelled, the form remains open and Save is disabled until **Refresh** verifies the stored Rule; a new unsaved Rule retains its committed identity for that recovery. Refresh asks before replacing dirty fields. Save and lifecycle operations block navigation/window closing while in progress; **Cancel operation** requests cancellation without claiming rollback of an already committed revision.

**Edit rule** edits the active current version, not a historical snapshot. **Archive rule / Reopen rule** require confirmation and append lifecycle revisions through the same concurrency rules. No delete action is added. Revision history shows ten rows with one lookahead, descending numbered revision cursors and Newer/Older controls. **View revision** shows a distinct read-only preview; **Return to current**, history paging and viewing old definitions do not overwrite an open editor. Rule/catalog switching, Cancel editing, Refresh, navigation and main-window closing guard dirty fields. Declining discard preserves selection and all fields. Accepting window close authorizes shutdown without prematurely clearing editor state.

All three text fields have inline validation, red borders, an accessible error summary, required-field/help metadata and first-invalid-field focus/scroll. Correcting a field clears its error. Ctrl+S saves; tab order and visible focus follow shared controls. Teal create/edit, green Save, blue Refresh, amber lifecycle and a distinct Return action reuse shared Light/Dark resources. One page scrollbar reaches the editor and history; multiline fields scroll internally until their edge, then pass the wheel to the page. The shell remains the only page-level “Rules” heading.

**Eligibility is metadata, not execution.** Manual-only and “Future-check candidate (not enabled)” preserve the established enum. This UI neither invokes nor records assessments, and it never interprets a Rule or Setup link as adherence or a violation. There is no Rules schema, provider, Trade or Journal change in M18.3.

### M18.3 verification and remaining live checks

Work started on clean `develop` at `8bd2b9ff1c39e9e39886fc6222025b66ad600256`; the M18.1/M18.2 implementation was already committed. Only the 13 files below are changed. No production data, provider, schema or Git writes were used.

- **22 focused Desktop cases passed**, including 20 new Rules cases and two existing Playbook navigation guard cases. Evidence: ignored `artifacts/m183-focused-verified/*.trx`.
- Synthetic SQLite-backed ViewModel tests create and edit real revisions, reject a stale editor without losing its fields, archive/reopen, then recreate services and read exact history from the same isolated database. They retain inactive exact-Account/New York-day scope and verify zero assessments were created. This is a service-restart persistence test, not a claim of user-driven application restart acceptance.
- Compiled WPF tests exercise Light/Dark at **960 DIP / 96 DPI** and **480 DIP / 240 DPI**, current/editor/historical/error states, field bindings and limits, aligned reachable actions, revision paging, mouse-wheel/page scrolling, accessible names and first-invalid-field logical focus on a real WPF presentation source. The actual `MainWindow.OnClosing` override is exercised for veto/acceptance. Foreground activation is not assumed.
- Sixteen automated render images were generated under ignored `artifacts/m183-renders`; current cards, historical previews, editor/actions and validation were visually inspected across both themes and sizes. These are **automated renders and events**, not live mouse/keyboard or screen-reader observations.

Final complete parallel Release (`artifacts/m183-full`): **3,943 passed / 2 failed / 0 skipped**, out of 3,945. Domain **485/485**, Application **738/738**, Infrastructure **1,225/1,225**, Desktop **1,495/1,497**. All new Rules cases passed in this full run. Calendar native exited successfully after **83.940 seconds** (36.060 seconds headroom against the unchanged 120-second deadline); the isolated Rules view child exited successfully after **9.816 seconds**. No deadlines, assertions, concurrency settings, retries or skips were added or changed.

The two unresolved full-run failures are in unchanged code/tests:

1. `JournalTradeContextViewTests.ReadOnlyRowsAlignWrapScrollAndExposeCurrenciesAndReferenceStatesInBothThemes`, `CheckLayout` line **123**: `Assert.True(wheel.Handled)` after a synthetic preview wheel event. Child diagnostics: `artifacts/m183-full/journal-context-20261012-172132-bde7fb5806e6420eb72bfc9b26d6f767`.
2. `PlaybookSqliteViewTests.CompiledSaveCreatesAndRevisesWithRealSqliteAndSurvivesReactivation`, line **129**: `Assert.NotSame(previousTask, vm.SaveCommand.ExecutionTask)` after invoking Save through an automation peer and flushing the dispatcher. The command's task had not advanced at that assertion; this is **not** evidence of a failed database commit or a focus assertion. Child diagnostics: `artifacts/m183-full/playbook-sqlite-write-20261012-172212-427fc012d2244a239aa347cb59e1c793`.

Both passed unchanged in one bounded targeted diagnostic run (`artifacts/m183-existing-ui-diagnostic`, **2/2**). At that stage their precise intermittent causes remained unproven and **M18.3 full-suite verification remained open**. The follow-up below records the subsequent investigation and final run; the earlier failure is not erased by a later pass.

Solution Release build passed with **0 warnings / 0 errors**. EF `has-pending-model-changes --configuration Release --no-build` through the isolated design-time factory reports no changes. `git diff --check` and new-file trailing-whitespace checks passed. No migration is added.

Remaining manual checks, in an isolated synthetic app only: open Planning → Rules, create an exact-Account rule using Tab/Ctrl+S, restart and inspect the saved revision; edit and decline discard during Rule switching and window closing; browse an old revision while fields are dirty and Return to current; archive/reopen; verify error announcements, keyboard focus and scrolling in both themes at minimum width/240 DPI. M18.4 assessments and automatic evaluation remain out of scope. GitHub Actions must test the user's eventual commit/PR tree; uncommitted changes cannot have a matching CI result. M17.7 live acceptance is not implied.

### Full-load interaction follow-up

Started from the user's committed M18.3 implementation on clean `develop`, HEAD `b094d6fda04f1b7d02d07804068aa4ddb5c8203f`. Two faulty test assumptions were demonstrated without changing production behavior:

- A synthetic wheel event does not specify modifiers. Production intentionally ignores Shift-wheel; setting Shift on the test STA reproduces `Handled=False`. The test now explicitly scopes ordinary synthetic input to no Shift, also asserts Shift bypass, and restores thread state even on failure. The same narrow correction covers the previously reported Journal History wheel check. It neither sends keyboard input to the desktop nor changes the user's physical keys.
- A new command execution need not produce a new Task object. Two automation invocations of a synchronously completed command reuse one completed task. Save automation now waits for and asserts exactly one routed Click, awaits completion, and retains every validation/focus/scroll, exact new SQLite revision, UI readback and separate-process restart assertion. Dispatcher-deferred completion is covered too.

The original failed run did not record modifiers or the state after a potentially synchronous Save. Therefore these are **proven harness assumptions with controlled regressions**, not proof of the exact old schedule and not demonstrated production Save/wheel defects. No persistence, UI behavior, deadlines, isolation, concurrency bounds, retries or skips changed. [Detailed chronology, safe diagnostic fields and timings](ci-wpf-tests.md#m183--synthetic-wheel-and-save-invocation-follow-up-12-october-2026).

Final verification: **46/46** focused cases under concurrent export/Rules rendering (39 Infrastructure, 7 Desktop); **3/3** isolated comparison cases; **3,946/3,946** in the final complete parallel Release run, zero failures/skips (**485 Domain, 738 Application, 1,225 Infrastructure, 1,498 Desktop**). Journal Trade Context used **7.887 s** of its unchanged 30-second STA limit in the full run; Playbook SQLite write/validation **6.173 s**, fresh-process read **2.475 s**. Calendar native **99.309 s / 20.691 s headroom**, grid **33.460 s**. Release build **0 warnings/errors**; EF no pending model changes; whitespace checks passed. Raw evidence: ignored `artifacts/m145-load/m183a-final-full`, with earlier comparisons alongside it.

**Local M18.3 automated verification is green; live and matching CI acceptance remain open.** Runner timing/input variability is not declared eliminated. No real journal or provider was accessed, no live mouse/keyboard checks were performed, and no Git writes were made. These eight follow-up files are uncommitted and cannot have a matching GitHub Actions result. The user must commit/push through the existing PR-to-main workflow and inspect the tested head/merge tree; M17.7 live acceptance remains separate. Do not start M18.4 from this report.

Exact follow-up files:

- `tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalTradeContextViewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalHistoryViewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Setups/PlaybookSqliteViewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/SyntheticKeyboardState.cs` (new)
- `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/WpfStepDiagnostics.cs` (new)
- `README.md`
- `docs/trading-rules.md`
- `docs/ci-wpf-tests.md`

### Original M18.3 changed files

- `README.md`
- `docs/trading-rules.md`
- `src/PersonalTradingJournal.Desktop/App.xaml`
- `src/PersonalTradingJournal.Desktop/App.xaml.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/MainWindowViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Rules/RulesViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/Rules/RulesView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Rules/RulesView.xaml.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Navigation/MainWindowViewModelTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Navigation/RulesNavigationTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/RulesViewModelTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/RulesSqliteTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/RulesViewTests.cs`

## Deferred work and limits

M18.4 supplies manual Trade/day assessment UI. Later milestones may add carefully specified automatic checks. `AutomaticCandidate` only reserves a source label; it is **not** an engine or a promise of automated evaluation. Rules never modify gross/net economics or imply that a Trade followed a linked Playbook. No M18.5 work or AI prompt change is included. M17.7 live acceptance remains independent of these tests or any CI run.

## M18.4 — Manual Desktop assessments

### Entry points and semantics

- **Trade Details → Assess Rules** opens the loaded Trade's exact identity and Account, for manual and imported Trades alike. The same action is available in Calendar's inline Trade detail. Parent editor fields remain untouched; only already-saved facts are assessed.
- **Calendar Day Performance → Assess day Rules** opens the selected New York date and explicit Account scope. All accounts means a positively identified **aggregate day target**, not a null-scoped Journal and not an implicit choice of Account. Exact-account rules are not offered for an aggregate target. No Trade is required to record Cannot evaluate on an empty day.
- **Planning → Rules → Manual assessments** shows retained results across targets, newest UTC recording time first with ID tie-breaker. Open exact target / correction deliberately loads that record's original target. Missing Accounts/Trades, archived or no-longer-applicable rules block new writes while history stays readable. Existing inactive Accounts remain usable for historical assessment.

The dialog shows the current immutable rule revision, criterion and required evidence before confirmation. All new UI records are **Manual user assessments — not calculated facts**; “Manual” records the local user's source declaration, not an authenticated multi-user identity. No violation requires explicitly declared sufficient, selected recorded evidence. Confirmed violation either requires sufficient recorded evidence or the explicit “I report this violation” declaration with an explanation. Cannot evaluate requires an explanation identifying missing/ambiguous facts, with available references selected where useful; it never becomes a pass. Not assessed remains absence, not a stored fourth outcome.

### Reads, history and retention

`IManualRuleAssessmentReader` adds read-only SQL discovery: 20 displayed rules or source rows, 10 displayed assessments, one lookahead source/history row, and repository limits of 1–100. Applicable active/archived definitions and previously assessed rules are discoverable for a target. Target history uses descending sequence keysets; Rules' cross-target browser uses bounded offset pages ordered by UTC then ID. Refresh restarts no writes; concurrent inserts may shift offset pages until refreshed. No per-source repository call or unbounded scan into Desktop memory is used.

The current picker exposes a Trade identity (covering its recorded lifecycle) and, for day assessments, saved Journal identities/revisions. It does not add a separate Execution or Playbook-capture picker; older records of those kinds remain readable. Closed Trade sources use the same New York closure date as Calendar/Daily Review, including midnight and DST transitions; open/partial activity appears only on its recorded activity date and is not called realized P&L. Unknown Net is unavailable, not zero; currencies and Account labels remain explicit. Journal selection includes all account scopes and null-scoped Journals only for a genuinely aggregate day.

Record confirmation captures the rule revision, target, selected references and previous assessment ID. The M18.2 store rechecks them under its writer transaction, including live target ownership, source existence/scope and current rule applicability. No storage or schema semantics changed. Corrections append new immutable records and supersede an earlier assessment ID without editing it. The history shows frozen criterion/revision, original Account identity, explanation, manual attribution, sequence, reference state and UTC recording time presented in New York with explicit DST offset; exact UTC is also expandable. References establish identity, not proof of an explanation. Deleted/moved sources never silently become another record or erase history.

### Guards and failures

Selections are immutable for a dialog target. Rule switching, Refresh and closing ask before discarding dirty explanation/source choices; read cancellation and late-result rejection cannot repopulate a closed dialog. During save, duplicate submission and departure are blocked; cancellation is supported before commit, and a committed record is independently read back before success. Validation and storage/conflict errors retain all fields. An unverified reported commit disables resubmission until an explicit guarded Refresh; there is no automatic retry. A successful correction reloads latest/history. The owned dialog blocks parent navigation, and watches every owner in the Day Performance → main-window chain: a veto leaves all windows and drafts intact; accepted owner close explicitly ends its modal frame before owned-window destruction.

### M18.4 verification

Started from clean `develop`, HEAD `1c79d374b879d6acb78a954edf6badcc92dd7caf`. All fixtures are synthetic; no production database or provider is opened. This work adds no migration and does not begin M18.5.

Focused validation: **86 passed, zero failures/skips** (Domain 9, Application 8, Infrastructure 28, Desktop 41), including **27 new cases**. Evidence: ignored `artifacts/m145-load/m184-focused-final`. The SQLite-backed editor tests record all outcomes and corrections, check exact versus explicit aggregate day targets and conflicting prior assessments, then rebuild services and read history again. Existing M18.2 tests additionally cover atomic cancellation/rollback, deleted Account/Trade references and evidence ownership. New discovery tests exercise bounded pages, null-scope Journals, other Accounts/currencies, unknown Net, midnight/DST attribution, cancellation and deleted-source restart reads. VM tests cover confirmed append/readback, validation, explicit user violation, cancellation, storage and readback failures, stale rule/prior assessment, paging without advancing dirty baselines, guards and late-load rejection.

Compiled WPF tests inspect Light/Dark at **960 DIP / 96 DPI** and **480 DIP / 240 DPI**, including validation borders/accessibility, logical focus, scrolling to Record, history, reachable fixed Close, and actual owned-window closing. Twelve automated overview/validation/history PNGs are in ignored `artifacts/m184-renders`; Light and Dark normal/narrow samples were visually inspected. **These are automated native-window/renderer checks, not live human keyboard/mouse or screen-reader acceptance.**

During implementation the new test caught a missing error-border trigger (fixed), and a fixture initially omitted its clock (fixed in the fixture). The nested-owner test then consistently left WPF `ShowDialog → ShowHelper → Dispatcher.PushFrameImpl` waiting after owned-window destruction. Content-free phase traces and the synthetic hang dump identified that path. Observable modal-entry coordination alone did not resolve it. The dialog now explicitly closes its modal frame after owner-close consent, guards the full owner chain, and bypasses a second confirmation only for that accepted close. The retained regression exercises child, parent and ancestor veto, draft preservation, and accepted ancestor close. Its 30-second action/90-second child deadlines remain unchanged; no retries, skips, concurrency changes or timeout increases were added.

Remaining live checklist (isolated synthetic data only): open each Trade/day entry point with Tab; record each outcome and inspect the confirmation; leave an explanation dirty and decline Refresh/Close/Escape/main-window close; open Rules history, append a correction and inspect the original; verify native focus restoration, screen-reader announcements and wheel/touchpad use at minimum width in both themes. No automated result closes these manual gates.

| Final verification | Observed result |
| --- | --- |
| Complete parallel Release suite | **3,973 passed, 0 failed, 0 skipped**: Domain 485, Application 739, Infrastructure 1,230, Desktop 1,519. Raw TRX/process/STA diagnostics: ignored `artifacts/m145-load/m184-full-final`. |
| Native deadlines under full load | Calendar native **62.522 s / 57.478 s headroom** to 120 seconds; Calendar grid **28.622 s**. New manual-assessment native child also passed. Existing Desktop/Infrastructure concurrency bounds and all deadlines are unchanged. |
| Build / model / whitespace | Release build **0 warnings, 0 errors**; EF design-time `has-pending-model-changes`: none; `git diff --check` plus changed/new-file trailing-whitespace check passed. |
| Live / CI / data safety | Human keyboard/mouse and screen-reader workflow unverified; matching Actions run open. No production journal, AI provider, Git commit/push/merge or M18.5 work. |

CI: `gh` is unavailable locally. The GitHub connector returned **no PR-triggered workflow runs** for the starting HEAD on 12 October 2026 (that API is filtered to PR runs, not proof about every event). More importantly, these M18.4 changes remain uncommitted and cannot have a matching Actions run. `.github/workflows/ci.yml` runs on pushes to `main` and PRs targeting `main`; the user must commit/push to the existing develop PR workflow and inspect the tested head/merge tree. Local verification is not matching CI or M17.7 live acceptance.

### M18.4 exact changed files

- `README.md`
- `docs/trading-rules.md`
- `src/PersonalTradingJournal.Application/Rules/IManualRuleAssessmentReader.cs` (new)
- `src/PersonalTradingJournal.Infrastructure/Rules/TradingRuleRepository.Manual.cs` (new)
- `src/PersonalTradingJournal.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs`
- `src/PersonalTradingJournal.Desktop/App.xaml.cs`
- `src/PersonalTradingJournal.Desktop/Dialogs/IRuleAssessmentDialog.cs` (new)
- `src/PersonalTradingJournal.Desktop/Dialogs/WpfRuleAssessmentDialog.cs` (new)
- `src/PersonalTradingJournal.Desktop/ViewModels/Rules/RuleAssessmentViewModel.cs` (new)
- `src/PersonalTradingJournal.Desktop/ViewModels/Rules/RulesViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Trades/TradesViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Calendar/CalendarViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/Rules/RuleAssessmentView.xaml` (new)
- `src/PersonalTradingJournal.Desktop/Views/Rules/RuleAssessmentView.xaml.cs` (new)
- `src/PersonalTradingJournal.Desktop/Views/Rules/RulesView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Trades/TradesView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Calendar/CalendarDayDetailsView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Calendar/CalendarInlineTradeView.xaml`
- `tests/PersonalTradingJournal.Application.Tests/Rules/TradingRuleUseCaseTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/ManualRuleDiscoveryTests.cs` (new)
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/RuleAssessmentViewModelTests.cs` (new)
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/ManualAssessmentEntryTests.cs` (new)
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/ManualAssessmentSqliteTests.cs` (new)
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/ManualAssessmentViewTests.cs` (new)
- `tests/PersonalTradingJournal.Desktop.Tests/Trades/TradesViewModelTests.cs` (fixture dependency only)

## M18.2 verification — 12 October 2026

Tested `develop` at `9193a92217c9bd1a9b8ca38d40309a04fba930fd` plus the 25 files listed below. The starting worktree was clean. No Git writes, production journal access, provider calls or Desktop implementation occurred.

| Check | Observed result / evidence |
| --- | --- |
| Focused Domain / Application | 9 / 6 Rules cases passed. |
| Focused SQLite / portability / schema | 78 passed in `artifacts/m182-focused-verified`; another 32 passed in `artifacts/m182-integrity`, including 22 overlapping Rules cases. **103 distinct focused cases** in total, with **22 newly added** SQLite cases. |
| Migration / restart | Previous-schema synthetic Trade data retained, nullable commission unchanged, Rules initially empty; restarted services read exact revisions and assessment transitions. |
| Concurrency / rollback | Competing Rule and assessment writers commit only one candidate; stale revisions, forged definitions, invalid scope, fault injection and cancellation leave no partial append or consumed number. |
| Retention / scope / paging | Single/bulk Trade deletion and Account deletion retain assessments; same-name replacement never changes identity. Exact/All scope, bounded pages, changed/unavailable references, Journal revisions, Playbook transitions and New York DST attribution passed. |
| Data portability | Complete backup → preflight → offline restore and recovery round trips passed. Export v5 retains exact Rules/reference metadata. Previous-schema archive rejection leaves installed synthetic data unchanged. |
| Final complete parallel Release | **3,925 passed, 0 failed, 0 skipped**: Domain 485, Application 738, Infrastructure 1,225, Desktop 1,477. TRX and child diagnostics: `artifacts/m182-full-final`. |
| Build / EF / whitespace | Solution Release build: **0 warnings, 0 errors**. In-memory design-time EF `has-pending-model-changes`: none. `git diff --check` and new-file trailing-whitespace check passed. |
| Live / CI | No Rules UI exists in this milestone; no live interaction acceptance claimed. These changes are uncommitted and therefore have no matching GitHub Actions run. M17.7 live acceptance remains separate and open. |

The first complete parallel run (`artifacts/m182-full`) passed **3,921/3,925**: three complete-model inventory tests required the new entity/FK/index expectations, and the previously documented `JournalHistoryViewTests.CompiledHistoryPagesAndSnapshotsWrapInBothThemesAndRemainReadOnlyAtHighDpi` failed at line 242 (`wheel.Handled`). The model expectations were extended, not relaxed. The Journal test passed **unchanged** in a diagnostic run (`artifacts/m182-journal-diagnostic`) and the final parallel run. Its intermittent cause remains unproven; this task does not claim to fix it. Calendar native completed in **111.489 s** initially and **97.736 s** finally (22.264 s final headroom to the unchanged 120-second deadline). No assertions, concurrency settings, deadlines, retry policies or Desktop code changed.

## M18.2 changed files

- `README.md`
- `docs/trading-rules.md`
- `docs/backup-restore.md`
- `src/PersonalTradingJournal.Application/Rules/ITradingRuleReader.cs`
- `src/PersonalTradingJournal.Application/Backups/IPortableExportService.cs`
- `src/PersonalTradingJournal.Infrastructure/Rules/TradingRuleRepository.cs`
- `src/PersonalTradingJournal.Infrastructure/Rules/TradingRuleRepository.Reads.cs`
- `src/PersonalTradingJournal.Infrastructure/Rules/RuleEvidenceSnapshots.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Records/TradingRuleRecords.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Configurations/TradingRuleRecordConfigurations.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/JournalDbContext.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261012163222_AddTradingRules.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261012163222_AddTradingRules.Designer.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/JournalDbContextModelSnapshot.cs`
- `src/PersonalTradingJournal.Infrastructure/Backups/PortableExportSchema.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRuleEvidenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/PortableExportTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookPersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Initialization/JournalDatabaseInitializerTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integration/ProductionPersistenceIntegrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integrity/SqlitePersistenceIntegrityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Migrations/InitialMigrationTests.cs`
