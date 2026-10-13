# Trading Rules — contracts, persistence and assessments

## Quiet whole-row reordering — 13 October 2026

This presentation update supersedes handle-only dragging, unmodified Up/Down and insertion-line/success-banner descriptions below. The Bulgarian heading and persisted order are unchanged.

- Successful moves clear action feedback quietly; no “Moving Rule” or “Rule moved” banner. Failure, cancellation, conflict and failed readback still produce the established actionable messages. Other Create/Edit/Save/Delete/Refresh messages and field validation are unchanged.
- Drag from row background, number or non-editable text. The original-hit ancestor filter excludes Edit/Delete and other buttons, TextBox/RichTextBox/PasswordBox, selectors, scrollbars, thumbs, links and the entire inline editor host. Mouse events are not swallowed or captured before crossing the standard drag threshold. Existing selectable/editable text retains its normal interaction.
- The numbered circle contains only its display number. Rows are keyboard-focusable with an accent focus outline. **Alt+Up / Alt+Down** reorder; ordinary arrows are not intercepted. Shift+F10 exposes equivalent move commands. A dedicated list-item automation peer exposes Rule name, current position and keyboard help; numbers remain positions, not IDs/revisions or adherence claims.
- A **10% accent-tinted row background** replaces the line. It is non-hit-testable and temporary: cleared on drag leave, drop (including veto/failure), cancellation, unloading and drag-loop completion. The existing before/after pointer-half placement and near-edge scrolling remain. There is no persistent selection/drag line.
- The same guarded, concurrency-checked store operation handles mouse and keyboard moves. Storage/schema, automatic checks, assessments, snapshots, JSON v8 and backup compatibility remain unchanged.

### Evidence and limits

Started on clean `develop` at `35f9f48149174fc4c9629a6057dac0b0a60dbe19`; prior ordering work was already committed and preserved. Synthetic data only. Initial focused checks: **151/151** (22 Domain, 14 Application, 47 Infrastructure, 68 Desktop), `artifacts/m145-load/rules-quiet-focused`.

Compiled WPF exercises multiple row hit locations and interactive exclusions, temporary tint cleanup after completion/failure/cancellation path, routed Alt+Down, quiet success, error feedback, right-aligned actions, editor guards and scrolling. Final Light/Dark automated renders at **960 DIP / 96 DPI** and **480 DIP / 240 DPI** are in `artifacts/rules-quiet-renders-final`; normal drag-highlight and narrow catalog/double-digit examples were inspected. Native physical dragging, screen-reader interaction and edge-scroll dwell remain live checks, not claims based on renders.

An isolated WPF diagnostic confirmed `UIElementAutomationPeer.CreatePeerForElement(new Border())` returns null. The new row automation peer corrects that demonstrated gap; compiled tests verify the actual peer's name, position, help and focusability.

The earlier Calendar timeout was in `WindowControlsRetainModalAndDraftThenCloseInEveryState(Light, Normal)` at its unchanged **45-second action** deadline, not the 120-second aggregate deadline. Test-only bounded phase breadcrumbs now distinguish owner show/layout, day/Trade loads, maximize/minimize/restore, resize, close veto, dismissal, focus and cleanup. No assertion, deadline, concurrency or production Calendar behavior changed. A passing rerun does not establish a cause or fix for that earlier timeout.

Final focused checks after the accessibility correction: **6/6**. Final complete parallel Release: **4,038/4,038**, zero failures/skips (**490 Domain, 753 Application, 1,249 Infrastructure, 1,546 Desktop**), `artifacts/m145-load/rules-quiet-final-full`. Calendar native passed **87/87 in 113.544 seconds**, leaving **6.456 seconds** to its unchanged 120-second deadline. Its four instrumented modal actions took **2.804–3.490 seconds**. This limited headroom remains a timing risk.

The first complete run was **4,037 passed / 1 failed**: the separate `NativeUnhandledDispatcherCallbackFailsItsChildWithTheOriginalError` harness probe hit its 30-second child deadline after its synthetic exception handler was reached. The same probe exited within **7.780 seconds** in the final run. No causal fix was established for that intermittent exit delay or the previous modal timeout; [raw chronology and measurements](ci-wpf-tests.md#quiet-rules-reordering--calendar-follow-up-13-october-2026) retain both outcomes. No retry, assertion removal, serialization or deadline extension was added.

Release build: **0 warnings/errors**. EF model consistency: **no pending changes**, using the isolated design-time context. `git diff --check` and the new-file whitespace check passed. No production data, providers, Git writes or M18.6. Branch remains `develop`, HEAD `35f9f48149174fc4c9629a6057dac0b0a60dbe19`. These uncommitted changes have no matching GitHub Actions acceptance; local green does not establish live mouse/keyboard or CI acceptance.

Exact changed files (10 modified, 1 new):

- `README.md`
- `docs/trading-rules.md`
- `docs/ci-wpf-tests.md`
- `src/PersonalTradingJournal.Desktop/ViewModels/Rules/RulesViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/Rules/RulesView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Rules/RulesView.Reordering.cs`
- `src/PersonalTradingJournal.Desktop/Views/Rules/RuleReorderRow.cs` (new)
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/RulesOrderTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/RulesViewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/SyntheticKeyboardState.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Calendar/CalendarDayModalWindowTests.cs` (diagnostics only)

## Persistent Rule order — 13 October 2026

The page-only heading **“Правилата са, за да се спазват!”** is centered, accent-colored and wrapping above Create/Refresh. The shared application heading is unchanged.

- Drag the numbered ↕ handle to the accent insertion line above/below a row. Edit, Delete and editor fields do not initiate drags. Near-edge drag motion scrolls the page. Escape/outside-drop cancellation writes nothing.
- Keyboard: Tab to the named reorder handle, then Up/Down; Shift+F10 opens equivalent Move up/down actions. First/last boundaries disable unavailable moves. After a move the same Rule's handle is focused and brought into view. Numbers recalculate from verified persisted order and are neither Rule IDs nor revisions.
- Reordering uses the existing unsaved-change guard before disrupting an editor. A veto retains the form, text and order. Failed/conflicting/cancelled writes reload actual persisted order and retain entered fields; failed readback reports an error, not verified success. No optimistic local shuffle or automatic retry.
- Complete list loading still uses cancellable 20-row queries. Every page must have the same catalog version/count, with no duplicates; concurrent changes reject a partial/mixed list.

### Storage and portability

Additive migration **`20261013102243_AddTradingRuleOrder`** adds `TradingRulePositions` and a singleton `TradingRuleCatalogState` (19 migrations total). It initializes active global Rules using the previous SQLite Name/RuleId order; legacy scoped Rules remain excluded and unchanged. Positions reference original Rule identity/revision 1. New Rules append; prose/typed-condition revisions keep their position; deletion removes that position and closes the gap. A trigger updates the catalog version in the same transaction as every global Rule append.

Move-before (or move-to-end) requires the catalog version last read. A SQLite immediate writer transaction rechecks source/target/version, permutes the affected positions in one statement and increments the version. Competing create/edit/delete/move operations conflict rather than silently overwriting order. A failure rolls everything back. Cancellation is checked before commit; an already completed commit is read back rather than falsely reported as rolled back. SQLite busy acquisition follows the existing provider timeout; work runs off the WPF thread.

Rule definitions, immutable revision numbers, typed conditions, assessments and saved AI snapshots are not rewritten by moves. Backup manifest remains v1 and preserves the complete current-schema database. JSON export **v8** explicitly includes `tradingRulePositions` (ruleId, initialRevision, zero-based position) and `tradingRuleCatalogState` (id/version). Even an empty export has this one metadata singleton, not a user Rule. Four CSVs are unchanged; exports are not backups. Older exports remain readable documents. Older-schema archives, including the immediately preceding schema, still require a future staged-upgrade path and are rejected by preflight/restore today.

### Verification

Started on clean `develop` at `56fbc5d4a446a3b5ebbd23eb3f4d4788c355ac8c`; preserved the existing implementation. Synthetic data only.

- Focused: **272/272 passed**, zero failures/skips (22 Domain, 14 Application, 168 Infrastructure, 68 Desktop), `artifacts/m145-load/rules-order-focused-verified`. Additional model/foreign-key integrity checks: **10/10**, `artifacts/rules-order-integrity`.
- Coverage includes second-above-first and move-to-end, first/last keyboard boundaries, dirty-editor veto, cancellation/failure/stale catalog, concurrent SQLite writers, rollback after the position UPDATE, legacy name/ID initialization, prose-edit position retention, new-row append, delete gap closure, fresh services/ViewModel restart and backup/preflight/offline restore/recovery/export.
- Compiled WPF verifies heading text/alignment/wrapping, accessible handles/menu, routed Down key and shared drop path, renumbering without revisions, themed insertion lines, double-digit badges, right-aligned actions and retained inline validation/scroll behavior. Inspected Light/Dark catalog/insertion/editor/numbered renders at **960 DIP / 96 DPI** and **480 DIP / 240 DPI**, `artifacts/rules-order-renders`.
- Development checks caught incomplete schema/table/singleton expectations and two test-fixture mistakes (fake content-version tracking and synthetic key presentation source). Corrected the exact assertions/fixtures, without removing behavior checks.
- The first complete run, `artifacts/m145-load/rules-order-full-final`, had **4,036 passed / 2 failed**: only the separate model record/FK inventory omitted the new order records. Desktop **1,546/1,546** passed; native **87/87 in 109.964 s**, **10.036 s** headroom. Those inventory assertions were updated to explicitly verify the new records and restrictive composite FK, and all ten integrity tests passed. The final full-run result is recorded below; an isolated pass is not full acceptance.

- **Final complete parallel Release is not green:** `artifacts/m145-load/rules-order-full-verified`, **3,952 passed / 86 failed / zero skipped** out of 4,038 (490 Domain, 753 Application, 1,249 Infrastructure passed; Desktop 1,460 passed / 86 failed). All Rules checks passed. The 86 Desktop failures propagate the isolated Calendar native result.
- First underlying failure: `CalendarDayModalTests.WindowControlsRetainModalAndDraftThenCloseInEveryState(Light, Normal)`, `CalendarDayModalWindowTests.cs:178`. Dispatcher acquisition completed in **8 ms**; the unchanged **45-second STA action deadline** expired at **45.011 s** in phase `test action`, thread 19 still alive. No finer operation is established by this breadcrumb. The child refused subsequent STA work; its partial TRX records 46 passed / 5 failed before termination. Its supervisor exited nonzero at **111.462 s**, before the 120-second aggregate deadline. This is an unresolved Calendar timeout, not a Rules assertion failure or proof of a specific scheduling/product cause. No retry, skip, deadline, assertion or concurrency change was made.
- Rules compiled child passed in **9.628 s** in the final full run. Release build **0 warnings/errors**, EF model consistency and tracked/new-file whitespace checks pass.
- Final worktree: **33 changed files** (24 modified, 9 new), no staged changes; branch/HEAD remain `develop` / `56fbc5d4a446a3b5ebbd23eb3f4d4788c355ac8c`. Full-suite acceptance remains open.

Live pointer dragging, edge-scroll dwell behavior and screen-reader interaction remain unverified; automated WPF layout/routed-key/drop-operation checks are not live acceptance. No production data or providers, Git writes or M18.6 work. Uncommitted changes cannot have a matching GitHub Actions run.

Remaining manual checks, using an isolated journal only: drag the second handle above the first and check insertion/edge scrolling; Refresh, navigate back and restart to confirm order; Tab/Up/Down/Shift+F10 in Light/Dark at minimum width, including a dirty-editor veto. Check screen-reader announcements of Rule name, position and operation result.

### Exact changed files — persisted order

- `README.md`
- `docs/backup-restore.md`
- `docs/trading-rules.md`
- `src/PersonalTradingJournal.Application/Backups/IPortableExportService.cs`
- `src/PersonalTradingJournal.Application/Rules/ITradingRuleOrderStore.cs`
- `src/PersonalTradingJournal.Application/Rules/ITradingRuleReader.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Rules/RulesViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/Rules/RulesView.Reordering.cs`
- `src/PersonalTradingJournal.Desktop/Views/Rules/RulesView.xaml`
- `src/PersonalTradingJournal.Infrastructure/Backups/PortableExportSchema.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Configurations/TradingRuleCatalogConfigurations.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/JournalDbContext.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261013102243_AddTradingRuleOrder.Designer.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261013102243_AddTradingRuleOrder.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/JournalDbContextModelSnapshot.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Records/TradingRuleCatalogRecords.cs`
- `src/PersonalTradingJournal.Infrastructure/Rules/TradingRuleRepository.Order.cs`
- `src/PersonalTradingJournal.Infrastructure/Rules/TradingRuleRepository.Reads.cs`
- `src/PersonalTradingJournal.Infrastructure/Rules/TradingRuleRepository.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/RulesOrderTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/RulesSqliteTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/RulesViewModelTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/RulesViewTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/PortableExportTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Initialization/JournalDatabaseInitializerTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integration/ProductionPersistenceIntegrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integrity/SqlitePersistenceIntegrityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Migrations/InitialMigrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRuleOrderTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookPersistenceTests.cs`

## Optional Criterion and numbered Rules — 13 October 2026

This earlier refinement introduced optional Criterion and numbered rows; the persisted-order section above supersedes its name/identity sorting:

- **Rule** replaces the “Rule name” UI/accessibility label. Meaningful Rule text remains required, with the existing **120-character** limit; the stored property/column remains `Name` to preserve identities and compatibility. Internal line breaks are retained. Its editor now matches Criterion: 100-DIP height, the same available width (maximum 760 DIP), multiline wrapping, vertical scrolling and no horizontal scrollbar.
- **Criterion** is optional supporting prose (0–4,000 characters). Blank/whitespace-only global Criterion becomes an empty string under the existing trim convention, never invented prose. Saving an existing Rule can clear it. The list collapses the Criterion text element when empty. Optional accessibility/help and validation reflect this; required Rule and over-limit Criterion errors remain independent of the ten-second action message.
- Circular, accent-bordered number badges show **1, 2, 3…** in the repository's deterministic name/identity order. Numbers are recalculated after complete load, Save, Delete and Refresh. They are display positions only, not identifiers, revisions, interactive checkboxes or proof of adherence. Stable Rule IDs still determine editing, deletion and historical references. Double digits fit without overlapping wrapped text or right-aligned actions; badges can grow for longer numbers.

### Persistence and compatibility

Domain/Application global definitions accept an absent Criterion; the legacy scoped-definition constructor retains its original requirements and read-only semantics. Typed automatic conditions are independent of this optional prose. Prose-only editing retains their exact values and the existing hidden evidence description. Assessment outcomes, sufficiency requirements, selection, calculation and concurrency rules are unchanged.

SQLite previously required nonempty Criterion. Additive migration **`20261013090509_OptionalGlobalRuleCriterion`** rebuilds that CHECK transactionally, copies all existing rows unchanged, reinstates revision/assessment triggers and checks foreign keys. Empty Criterion is permitted for global definitions only. Current-schema migration history now contains 18 migrations. A downgrade that cannot represent an empty Criterion fails before replacing history; it does not manufacture text.

Backup manifest v1 and JSON export v7 retain their existing fields/types and exact empty strings. Complete backup → preflight → offline restore/recovery tests include an empty-Criterion Rule and its assessment. Export verifies empty `criterion` alongside retained typed conditions and references. Existing current-schema-only restore policy is unchanged: archives from older schemas, including the immediately preceding schema, need a future staged upgrade and are rejected today, not silently migrated during restore.

### Verification and limits

Started on clean `develop` at `ff8f9c0d12ccdc04f81d6e0460d4e6751921e414`; all previous work was already committed and preserved. Synthetic data only.

- **161 focused tests passed** (22 Domain, 14 Application, 62 Infrastructure, 63 Desktop), zero failures/skips: `artifacts/m145-load/rules-numbered-focused-final`.
- New coverage: optional/whitespace Criterion, required Rule/unchanged limits, immutable revisions and stale writes, SQLite create/clear/fresh-services reload, preserved legacy scoped data, typed checks/assessment text across migration, downgrade refusal, backup/preflight/restore/recovery/export, and numbering after create/delete/refresh.
- First focused run exposed two new-test fixture mistakes (missing TimeProvider construction and an incorrect ordinal-name ordering expectation). Both were corrected without changing production behavior or weakening assertions; the final focused run passes.
- Compiled WPF exercises twelve rows, double-digit badges, collapsed empty Criteria, equal-width/100-DIP editors, required/optional accessibility, multiline wrapping, invalid Rule focus, timed-feedback validation persistence, guarded inline switching, Save/Cancel, wheel/PageDown and owner close. Light/Dark catalog/create/edit/validation/numbered renders at **960 DIP / 96 DPI** and **480 DIP / 240 DPI** were inspected in `artifacts/rules-numbered-renders`; these are automated renders, not live input.
- Final complete parallel Release: **4,031 passed, zero failures/skips** (490 Domain, 753 Application, 1,247 Infrastructure, 1,541 Desktop), `artifacts/m145-load/rules-numbered-full-final`. Calendar native **87/87 in 66.583 seconds**, **53.417 seconds** headroom to its unchanged 120-second deadline; grid **17.893 seconds**, Rules WPF child **9.296 seconds**. No Calendar/Journal timeout occurred; this is not a guarantee against future load variability.
- Release build: **0 warnings/errors**; EF model consistency, `git diff --check` and new migration whitespace checks pass. Final branch/HEAD remain `develop` / `ff8f9c0d12ccdc04f81d6e0460d4e6751921e414`; 23 files are uncommitted (21 modified and two new migration files).

Live mouse/keyboard and screen-reader checks remain open. Uncommitted changes cannot have matching GitHub Actions verification. No production data, provider calls, Git writes, assessment/automatic-check changes, deadline increases or M18.6 work.

### Exact changed files — optional Criterion

- `README.md`
- `docs/backup-restore.md`
- `docs/trading-rules.md`
- `src/PersonalTradingJournal.Application/Rules/TradingRuleContracts.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Rules/RulesViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/Rules/RulesView.xaml`
- `src/PersonalTradingJournal.Domain/Rules/TradingRuleDefinition.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Configurations/TradingRuleRecordConfigurations.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/JournalDbContextModelSnapshot.cs`
- `tests/PersonalTradingJournal.Application.Tests/Rules/TradingRuleUseCaseTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/RulesFeedbackTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/RulesSqliteTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/RulesViewModelTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/RulesViewTests.cs`
- `tests/PersonalTradingJournal.Domain.Tests/Rules/GlobalTradingRuleTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Initialization/JournalDatabaseInitializerTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integration/ProductionPersistenceIntegrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Migrations/InitialMigrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/GlobalRuleCatalogTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePortabilityTests.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261013090509_OptionalGlobalRuleCriterion.Designer.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261013090509_OptionalGlobalRuleCriterion.cs`

## Timed feedback and complete catalog — 13 October 2026

This section supersedes the pagination presentation described below. Rules shows a single, fixed top feedback box above its scrollable actions, list and editor. Information is blue, verified success green, and errors red, using shared Light/Dark resources and explicit text labels rather than color alone. Create, Edit, Save, Cancel, Delete, Refresh, declined confirmations and failures replace the previous message. The latest message expires after ten seconds; a cancelled older timer cannot dismiss its replacement. Leaving the destination cancels its feedback timer. Loading/saving cancellation remains reachable independently of the feedback lifetime.

Field errors and the form validation summary have independent lifetimes and remain until corrected. Feedback announcement is polite and does not focus or scroll anything. Existing first-invalid-field focus, editor-opening focus and unsaved-change guards are unchanged.

There is no page indicator or Previous/Next UI. The ViewModel gathers the repository's bounded 20-row pages on a worker task (SQLite async APIs can otherwise complete synchronously on the UI thread), preserving name/identity ordering. Only a complete result replaces the catalog. Cancellation, failed later reads, inconsistent counts, missing rows or duplicate identities leave the previous list and entered fields intact and request Refresh. No retries, unbounded database query, repository limit changes or partial-catalog success. The list itself materializes all loaded Rules; unusually large catalogs still have proportional memory/layout cost. Pages are individual repository snapshots, not a new catalog-wide transaction; concurrent external changes detected during paging require Refresh, and writes retain their existing revision rechecks.

Each row has a small noninteractive, theme-aware list bullet, accessible as “Rule list item”; it is not a checked/adherence state. The name, criterion and any existing typed condition stay beside it, with Edit/Delete at the far right and the shared editor underneath its exact row. Create remains above the list.

### Verification

- Started on clean `develop`, HEAD `48c66e0bf3e36051600874847b7e80ebeea72804`; preserved all committed prior work.
- **61 focused Desktop tests passed**, zero failures/skips: `artifacts/m145-load/rules-feedback-focused`. Covers 0/20/21/40/105 rows, deterministic complete loading, later-page cancellation/failure, count/duplicate changes, timed replacement/expiry, validation persistence, action feedback and existing SQLite save/guard tests.
- Compiled WPF checks retain inline placement, switching veto, correct Save/Cancel targeting, wheel/PageDown, field geometry and owner-close assertions. Added fixed top feedback placement, themed states, no pagination, marker/action separation and focus preservation when feedback expires.
- Inspected automated catalog/create/edit/validation renders in Light/Dark at 960 DIP / 96 DPI and 480 DIP / 240 DPI: `artifacts/rules-feedback-renders`. A 400-DIP viewport exercises scrolling; these are not live keyboard/mouse observations. Feedback heading/background contrast ratios are 6.42/7.23 (Light/Dark information), 5.11/5.94 (success) and 4.70/5.33 (error).
- Final complete parallel Release: **4,025 passed, zero failures/skips** (488 Domain, 752 Application, 1,246 Infrastructure, 1,539 Desktop), `artifacts/m145-load/rules-feedback-full-final`. Calendar native **87/87 in 83.482 seconds**, **36.518 seconds** headroom to its unchanged 120-second aggregate deadline; grid **32.970 seconds**. Rules compiled-WPF child passed in **7.594 seconds**. No Calendar/Journal timeout occurred; one pass does not eliminate load-dependent timing risk.
- Release build: **0 warnings, 0 errors**. EF reports no pending model changes; `git diff --check` and new-file whitespace checks pass. No schema, repository query, assessment, automatic-check, test deadline or concurrency changes.
- Final branch/HEAD remain `develop` / `48c66e0bf3e36051600874847b7e80ebeea72804`. Nine files are uncommitted (eight modified, one new), listed below.

Remaining acceptance: live mouse/keyboard and screen-reader announcements, and GitHub Actions for the user's resulting commit. Uncommitted changes cannot have matching CI verification. No production journal, provider calls, commits/push/merge or M18.6.

Exact changed files:

- `README.md`
- `docs/trading-rules.md`
- `src/PersonalTradingJournal.Desktop/Resources/Themes/LightTheme.xaml`
- `src/PersonalTradingJournal.Desktop/Resources/Themes/DarkTheme.xaml`
- `src/PersonalTradingJournal.Desktop/ViewModels/Rules/RulesViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/Rules/RulesView.xaml`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/RulesViewModelTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/RulesViewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/RulesFeedbackTests.cs` (new)

## Inline editor layout — 13 October 2026

The redundant **Rule editor** heading and explanatory sentence are removed. Rule name and Criterion labels, required-field accessibility, inline errors and the error summary remain. One shared form template is instantiated above the list for Create, or immediately under the edited Rule's header/actions and before the next row. Placement follows the captured Rule ID, never a row index or displayed name. Other rows remain visible/reachable through the page scrollbar.

Existing commands still own validation, concurrency, Save readback, Cancel and unsaved-change confirmation. A declined row switch/Create/Cancel keeps the same form and text. Accepted switching moves the form to the correct row. Successful Save or accepted Cancel closes that form; clean Edit → Create opens blank fields above the list and hands focus to the new form. On ordinary edit opening, the selected header and first field are brought into view together where the viewport permits. Closing restores focus to the matching Edit button when present, otherwise Create.

The page/count indicator and Previous/Next controls now sit **below the list**, aligned to its right edge. The indicator can wrap above the grouped navigation buttons without overlap. Paging limits, order, guards and availability are unchanged. There are no persistence, schema, assessment, automatic-check, deadline or concurrency changes in this refinement.

Started on clean `develop` at `9219f57cb3dbbb0cbd262266c2b875b08c351bb1`; prior work was already committed and remains intact. Focused Desktop verification: **54 passed, zero failures/skips**, `artifacts/m145-load/rules-inline-focused-final`. This includes SQLite-backed existing Save tests, captured-identity placement, vetoed and accepted switching, correct Save/Cancel targeting, empty Create fields, unchanged pagination guards, validation and owner-close protection.

Compiled WPF checks use three distinct Rules and one shared form in **Light/Dark, 960 DIP / 96 DPI and 480 DIP / 240 DPI**, with a 400-DIP viewport for scrolling. Assertions verify the form is inside the selected row and before the next row, only one form exists, Create is above the list, the footer is below the list/right-aligned, actions remain accessible, and declined switching preserves the same controls and text. Existing wheel, PageDown, validation, field geometry and Save reachability assertions remain. Real owned-window automation verifies first-invalid-field logical focus, selected-row visibility, and clean Edit → Create focus when `IsEditing` remains true. Catalog/create/edit/validation renders in ignored `artifacts/rules-inline-renders` were inspected; these are automated WPF renders, not live input observations.

Verification uses only synthetic data. Live mouse/keyboard and screen-reader checks and a GitHub Actions run for the resulting committed changes remain separate, open gates. No M18.6, production access or commit/push/merge.

Final full parallel Release: **4,018 passed, zero failures/skips** (488 Domain, 752 Application, 1,246 Infrastructure, 1,532 Desktop), `artifacts/m145-load/rules-inline-full-final`. Calendar native **87/87 in 104.711 seconds**, **15.289 seconds** headroom against the unchanged 120-second aggregate deadline; grid **34.647 seconds**. No Calendar or Journal timeout occurred, but this is not a claim to eliminate load-dependent timing risk. Release build: **0 warnings/errors**; design-time EF reports no pending model changes, and whitespace checks pass. HEAD/branch are unchanged; final worktree contains the seven modified files below, no new files. No matching CI run can verify this uncommitted tree yet.

Exact changed files:

- `README.md`
- `docs/trading-rules.md`
- `src/PersonalTradingJournal.Desktop/ViewModels/Rules/RulesViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/Rules/RulesView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Rules/RulesView.xaml.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/RulesViewModelTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/RulesViewTests.cs`

## Simplified editor — 13 October 2026

This is the current UI behavior, superseding the authoring controls in earlier milestone reports below. The global catalog's editor contains **Rule name** (1–120 characters) and **Criterion** (1–4,000 characters) only. Both remain required, with inline errors, first-invalid-field focus/scroll and unsaved-edit guards. Required evidence, the typed-check checkbox, metric, P&L basis, maximum loss and currency inputs are removed. A new entry has no automatic criterion and is manual-only; no threshold or evidence text is invented.

Prose editing carries forward the selected persisted revision's exact evidence description and complete typed criterion. Existing automatic checks still run only through the explicit assessment **Run checks** action. Each active typed Rule displays a read-only sentence in its catalog row: Trade or New York closed-day metric, Gross or strict Net (all costs known), exact decimal loss threshold, currency and strict `>` comparison (equality passes). Ordinary manual Rules have no extra check explanation. Assessment eligibility, historical evaluations, currency/Account boundaries and source checks are unchanged.

Rows use a flexible name/statement column and an auto-sized right-hand Edit/Delete column, with 12-DIP outer padding. Long names, statements and check explanations wrap without pushing actions off screen; the full statement remains in its tooltip. The compact editor has no hidden automatic authoring state. Failed, stale or cancelled saves keep entered text, and a successful save still requires exact database readback.

### Persistence and compatibility

Migration `20261012214801_OptionalGlobalRuleEvidence` relaxes only the global Rule evidence-description minimum from one character to zero. `requiredEvidence = ""` means **no authored description**, not zero evidence, an automatically passed assessment or fabricated placeholder. Nonempty historical descriptions retain their exact persisted values; legacy scoped definitions still require their original description and remain read-only. Name, statement and actual assessment-evidence requirements are unchanged.

SQLite requires a transactional table rebuild to change the CHECK constraint. The migration copies every revision unchanged, preserves the primary key/index and all Rule/assessment insertion and immutability triggers, and checks foreign keys before committing. Downgrade refuses new empty descriptions instead of inventing replacement text. No assessment, criterion, revision number or source relationship is rewritten. Complete backup/preflight/restore/recovery retain both empty new descriptions and old typed history; exact-current-schema compatibility still applies. JSON export remains v7: fields and data types are unchanged, with empty global evidence descriptions explicitly permitted. Exports are not backups.

### Verification for this refinement

Started with a clean `develop` worktree at `ec9b9418c4bd8ce0f09e7a9469fff9cc47fd306f`; prior changes were already committed. HEAD/branch remain unchanged. Final worktree: **21 changed files** (19 modified, two new migration files). Only synthetic data is used. No M18.6, production data, AI requests or Git writes.

Focused verification: **186 passed, zero failures/skips** (Domain 12, Application 21, Infrastructure 100, Desktop 53), recorded in ignored `artifacts/m145-load/simplified-rules-focused-final`. Tests cover two-field validation, new manual-only creation, typed-condition/evidence retention through prose edits and conflicts, SQLite restart/migration, immutable assessment retention, downgrade rejection, backup/preflight/restore/recovery and export of absent descriptions.

Final complete parallel Release: **4,017 passed, zero failures/skips** (Domain 488, Application 752, Infrastructure 1,246, Desktop 1,531), `artifacts/m145-load/simplified-rules-full-final`. Calendar native **87/87 in 114.719 seconds**, **5.281 seconds** headroom to the unchanged 120-second deadline; grid **29.338 seconds**. No Calendar or Journal timeout occurred. Limited headroom remains load-dependent; no timing or concurrency changes were made. Release solution build: **0 warnings/errors**. Design-time in-memory EF model consistency and tracked/new-file whitespace checks pass.

Compiled WPF checks pass in Light/Dark at **960 DIP / 96 DPI** and **480 DIP / 240 DPI**, using a deliberately short 400-DIP viewport to retain meaningful scrolling coverage after removing fields. Catalog, editor and error renders are in ignored `artifacts/simplified-rules-renders` and were inspected. Assertions check same-row actions, Delete flush to the right content edge (within 1 DIP, plus 12-DIP card padding), no overlap with wrapping text/condition, accessible names, focusability, first-invalid-field logical focus and reachable Save/Cancel. There are no check inputs or Required evidence editor controls.

The initial focused run caught two test updates: an obsolete third-field case and reference equality on a rehydrated revision. The former now tests the two surviving fields; migration retention compares the full serialized assessment and evaluation contents instead of object identity. No product assertions, test deadlines, concurrency bounds or isolation protections were weakened.

Live mouse/keyboard and screen-reader checks remain open. Automated compiled WPF checks/renders are not live acceptance. The changed tree is uncommitted and therefore cannot have a matching GitHub Actions run; user commit/push and the matching workflow result remain a separate gate.

### Exact changed files — simplified editor

- `README.md`
- `docs/backup-restore.md`
- `docs/trading-rules.md`
- `src/PersonalTradingJournal.Desktop/ViewModels/Rules/RulesViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/Rules/RulesView.xaml`
- `src/PersonalTradingJournal.Domain/Rules/TradingRuleDefinition.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Configurations/TradingRuleRecordConfigurations.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261012214801_OptionalGlobalRuleEvidence.Designer.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261012214801_OptionalGlobalRuleEvidence.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/JournalDbContextModelSnapshot.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/AutomaticRuleViewModelTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/RulesSqliteTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/RulesViewModelTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/RulesViewTests.cs`
- `tests/PersonalTradingJournal.Domain.Tests/Rules/GlobalTradingRuleTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Initialization/JournalDatabaseInitializerTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integration/ProductionPersistenceIntegrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Migrations/InitialMigrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/GlobalRuleCatalogTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePortabilityTests.cs`

## Global catalog refinement — before M18.6

**Current behavior supersedes the scoped authoring and archive/history UI described in the milestone reports below.** Planning → Rules is a global catalog of general trading principles. A row shows its name, short statement, Edit and confirmed destructive Delete. The editor has no Account selector or Trade/day applicability. There are no assessment/source links, revision numbers, revision browsing, or Archive/Reopen controls on this page. Save is acknowledged briefly only after exact persisted readback; validation, conflicts, cancellation and failed readback retain entered fields. Required-field focus and unsaved switching/navigation/window-close guards remain.

An optional **check metric** belongs to a typed criterion, not to the Rule's ownership: maximum closed loss per Trade or per New York closure day, with Gross/strict Net basis, threshold and currency. Manual assessments can reference a global principle from either workflow. Run checks selects active global rules by `RuleCheckCapability`, without filtering definitions by Account. The **assessment target** still carries the exact Trade/day and explicit exact-Account or aggregate scope. Evidence, costs, currencies, outcomes and the `closed-loss/1` calculator are unchanged.

### Existing scoped definitions and deletion

Additive migration `20261012205801_GlobalTradingRuleCatalog` adds `IsGlobal` (default false for existing rows) and nullable `CheckCapability`. It does not rewrite any old Rule or assessment. Legacy scoped definitions are **read-only compatibility data**, excluded from the global catalog and new manual/automatic assessments. Retained assessments remain readable in Trade/day assessment dialogs via **Rule assessment history**, including unavailable targets. A user who wants the old principle in the global catalog must explicitly create a new global Rule with a new identity; no automatic conversion, widening, merging or reassignment occurs.

New Domain authoring accepts name, statement, required-evidence description and optional typed criterion, with no target or Account parameter. The old constructor and persisted applicability/scope columns are a compatibility envelope for immutable legacy reads. For `IsGlobal=true`, those old columns have fixed neutral storage values (Trade=1 / AllAccounts=1 / null Account) and **are not definition applicability or an assessment filter**. The global marker and typed capability are authoritative. SQLite rejects new legacy inserts, attempted promotion of a legacy identity, inconsistent typed metadata, and reopening a deleted global identity. Internal revision numbers continue monotonically.

Delete appends an inactive internal tombstone under the same SQLite immediate write transaction and expected-revision check. All revisions and all existing/concurrently committed assessments are retained unconditionally; no reference-dependent hard-delete or cascade can race with a new assessment. The current revision and reference relationship are protected by the transaction and restrictive foreign keys. If Delete wins the write reservation, a later assessment with the prior revision is rejected. If an assessment commits first, Delete retains it. The Rule disappears from the active catalog, cannot be reopened there, and its original historical wording is never reinterpreted. Cancelled confirmation writes nothing; stale Delete requires Refresh.

### Portability and verification

Complete archive format v1 still includes the whole database. Current-schema preflight/restore/recovery preserve global flags, capabilities, tombstones, legacy definitions and assessments. Older-schema archives are rejected without modifying installed data; staged upgrade remains deferred. A schema downgrade is explicitly blocked once global definitions exist because the old schema cannot represent their meaning safely. Logical JSON export is **v7**, adding `isGlobal` and `checkCapability`; four CSVs and screenshot-metadata-only policy are unchanged. Earlier exports retain their original scoped meaning and are not backups.

### Global catalog verification — 13 October 2026

Started on clean `develop`, HEAD `40ad8aeb28486d3961ebaae40282222547b65cfd`; prior work was already committed and remains intact. **41 files** are modified/new (37 tracked edits, four new files), including one additive migration and its designer. No production data, provider calls, commit, push or merge; M18.6 is not started.

| Scenario | Result and evidence |
| --- | --- |
| Global definitions and independent assessment targets | Passed: Domain/Application tests validate global authoring, legacy rejection, separate Trade/day/Account assessment identities, revision conflicts and missing evidence. |
| Existing scoped data | Passed: synthetic pre-refinement SQLite data migrates without broadening or rewriting its original Account, text or assessment; retained history is readable but legacy definitions cannot be authored or checked. |
| Confirmed Delete and races | Passed: SQLite concurrent assessment/Delete, stale revision, immutable historical text, restart and tombstone checks; ViewModel cancellation/failure/conflict preserve the list and entered fields. No cascade deletion. |
| Typed checks | Passed: global per-Trade and per-day capabilities are selected across different Accounts; exact assessment scopes, strict Net, missing costs, currencies, idempotency and source conflicts remain covered. |
| Catalog, paging and guards | Passed: 20/21/40-row page boundaries, last-row deletion reconciliation, validation recovery, failed row read, verified Save, unsaved navigation and actual MainWindow closing-path tests. |
| Portability | Passed: complete backup → preflight → offline restore/recovery and export v7 retain global definitions, deleted-rule history, check capabilities and assessment evidence. Schema inventories and unsupported-schema rejection pass. |
| Compiled WPF | Passed: Light/Dark at 960 DIP / 96 DPI and 480 DIP / 240 DPI; catalog/editor/validation and assessment renders inspected in ignored `artifacts/global-rules-renders`. Focusability, accessible names, first-invalid-field logical focus, scrolling and reachable actions asserted. |
| Live interaction | Not performed: actual user mouse/keyboard and screen-reader announcements remain unverified. Renders and owned-window test automation are not live acceptance. |
| Matching GitHub Actions | Open: these changes are uncommitted, so no workflow can have tested the exact resulting tree yet. Workflow remains main pushes / PRs targeting main; user commit/push and a matching successful run are required. |

**Focused: 184 passed, zero failed/skipped** (Domain 11, Application 21, Infrastructure 99, Desktop 53), `artifacts/m145-load/global-rules-focused-complete`. **Final complete parallel Release: 4,015 passed, zero failed/skipped** (Domain 487, Application 752, Infrastructure 1,245, Desktop 1,531), `artifacts/m145-load/global-rules-full-final`. The final full build/run also covers the last legacy typed-summary presentation correction.

Calendar native **87/87**, **109.707 seconds**, **10.293 seconds** headroom to its unchanged 120-second deadline; grid **20.590 seconds**. No Calendar or Journal timeout occurred. Timing remains load-dependent; no deadlines, concurrency bounds, skips, retries or isolation protections were changed. Release solution build: **0 warnings, 0 errors**. Design-time in-memory EF consistency: **no pending changes**. Tracked/new-file whitespace checks pass.

The first focused run exposed one schema-inventory assertion with the two new columns in the wrong expected order; corrected before the green runs. New row-edit error handling prevents a failed read from opening an unverified editor or hiding its error. No claim is made that older live acceptance gaps or GitHub CI have been resolved.

Remaining isolated manual checklist: create/edit/delete a global principle; decline dirty-editor navigation; inspect retained assessments from a Trade and day after deleting the rule; run both typed metrics for two different Accounts; verify Tab order, confirmation focus and screen-reader error announcements in both themes at minimum width/240 DPI.

### Exact changed files — global catalog refinement

- `README.md`
- `docs/backup-restore.md`
- `docs/trading-rules.md`
- `src/PersonalTradingJournal.Application/Backups/IPortableExportService.cs`
- `src/PersonalTradingJournal.Application/Rules/ITradingRuleReader.cs`
- `src/PersonalTradingJournal.Application/Rules/TradingRuleContracts.cs`
- `src/PersonalTradingJournal.Application/Rules/TradingRuleUseCases.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Rules/RuleAssessmentViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Rules/RulesViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/Rules/RuleAssessmentView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Rules/RulesView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Rules/RulesView.xaml.cs`
- `src/PersonalTradingJournal.Domain/Rules/AutomaticRuleCriterion.cs`
- `src/PersonalTradingJournal.Domain/Rules/TradingRuleAssessment.cs`
- `src/PersonalTradingJournal.Domain/Rules/TradingRuleDefinition.cs`
- `src/PersonalTradingJournal.Infrastructure/Backups/PortableExportSchema.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261012205801_GlobalTradingRuleCatalog.Designer.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261012205801_GlobalTradingRuleCatalog.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/JournalDbContextModelSnapshot.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Records/TradingRuleRecords.cs`
- `src/PersonalTradingJournal.Infrastructure/Rules/TradingRuleRepository.Automatic.cs`
- `src/PersonalTradingJournal.Infrastructure/Rules/TradingRuleRepository.Manual.cs`
- `src/PersonalTradingJournal.Infrastructure/Rules/TradingRuleRepository.Reads.cs`
- `src/PersonalTradingJournal.Infrastructure/Rules/TradingRuleRepository.cs`
- `tests/PersonalTradingJournal.Application.Tests/Rules/TradingRuleUseCaseTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/ManualAssessmentSqliteTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/RuleAssessmentViewModelTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/RulesSqliteTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/RulesViewModelTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/RulesViewTests.cs`
- `tests/PersonalTradingJournal.Domain.Tests/Rules/GlobalTradingRuleTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/PortableExportTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Initialization/JournalDatabaseInitializerTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integration/ProductionPersistenceIntegrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Migrations/InitialMigrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/AutomaticRulePersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/GlobalRuleCatalogTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/ManualRuleDiscoveryTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookPersistenceTests.cs`

M18.1 established the Domain/Application foundation; M18.2 adds SQLite persistence and data portability; M18.3 adds Desktop management under Planning → Rules; M18.4 adds manual Trade/day assessments. M18.5 adds explicitly invoked typed checks below. There is no AI integration, free-text execution or background checking. Historical revisions are read exactly as recorded; earlier milestone reports below retain their original scope and results.

## M18.5 — Deterministic checks

### Supported contract and evidence

`AutomaticRuleCriterion` v1 defines a **maximum closed-loss** limit, a `Gross` or `StrictNet` basis and one uppercase three-letter currency. The nonnegative decimal limit is never rounded. A Rule revision's existing target determines the metric:

| Target | Metric and inclusion | Required evidence |
| --- | --- | --- |
| Trade | The exact Account/Trade's complete closed lifecycle P&L in the criterion currency. | At least one closed Trade; reconstructable executions/pricing; Gross. Strict Net additionally requires every execution's commission and fee. |
| New York day | Sum of closed lifecycle P&L attributed to that New York **closure date**, for the explicit exact Account or aggregate All accounts, only in the criterion currency. | At least one contributing closed Trade and complete required facts for **every** contributing Trade. No separate intra-day drawdown or broker balance calculation. |

The calculator uses the existing Domain `Trade.GrossPnL` / `Trade.NetPnL`; no new economics or estimated-Net formula. A **Confirmed violation** means P&L is strictly less than negative maximum loss. Equality, a smaller loss, zero P&L or a gain is **No violation** only when the evidence is complete. Unknown commissions/fees are counted per execution. Gross does not require costs; strict Net does. Even a large known gross loss cannot prove a strict-Net outcome when required costs are unknown. Decimal overflow is an operational error, never a recorded outcome.

No closed Trades in that currency, missing executions/unavailable lifecycle, unavailable Gross, or missing required costs yields **Cannot evaluate**, with coverage/explanation. A zero-Trade day is not a fabricated evidence-backed pass. Open/partial activity is excluded from the realized sum, with counts; it is not treated as flat or zero Net. Closed Trades spanning midnight belong only to their closure day. Candidate execution activity is queried using Calendar's half-open New York midnight UTC bounds (23/25-hour DST days), then reconstructed closure determines final inclusion. A Trade without executions has no reliable date and conservatively prevents a complete result in its currency. Invalid stored lifecycle data or IO/SQL errors fail the operation instead of becoming Cannot evaluate.

All accounts is an explicit aggregate target, retaining each source Account ID; a global rule may run on it or on an exact Account. An exact-account rule is eligible only for that exact target. Different currencies are never combined; other-currency sources retain identity and an exclusion count. Inactive but existing Accounts remain usable. Missing Accounts/Trades block new checks; history remains readable with changed/unavailable source labels. No Setup/Playbook association proves adherence. Journal prose and free-text Rule descriptions are not interpreted. Rules without a typed criterion—including older automatic-candidate definitions—are untouched by Run checks; no Not-assessed record is created.

### Explicit Desktop workflow and history

In Planning → Rules, enable the typed check and select basis, loss limit and currency. Saving creates a new immutable Rule revision; existing revisions are not backfilled. **Assess Trading Rules…** in Trade Details (including inline Calendar Trade details) and **Assess day Rules…** in Day Performance open the shared guarded assessment window. **Run checks** explicitly evaluates all applicable current typed rules, not just the selected manual rule. Opening, paging and Refresh never run checks. Manual fields are protected by the existing discard guard; declining it starts no operation. Selection and closing are blocked during a write, duplicate submissions are suppressed, and Cancel operation remains usable while bounded SQLite work runs off the WPF dispatcher.

Results display exact Rule revision, calculated versus manual attribution, evaluator version, target, coverage, explanation, UTC/New York recording time and source identities. Rules → Assessment history reads the same records. Corrections and calculated checks append; neither edits or relabels a manual record. An identical repeat returns its existing assessment ID even if a manual correction has since been recorded, leaving that correction as the latest record. Changed evidence or a new Rule revision can append a new result. Saved evaluation facts are historical, not a claim that current sources still comply.

### Transaction, limits and portability

Evaluator version is `closed-loss/1`. One read snapshot prepares the current eligible Rule set and source universe. A separate SQLite immediate write transaction re-reads and fingerprints that entire set—including new/deleted Trades, imported executions and revised/archived/new Rules—before calculating and appending. A mismatch returns Conflict with zero writes; there is no retry. The write reservation prevents another writer changing the checked state before commit. Source fingerprints, exact Trade/Account/currency facts, contributing/excluded coverage and ordered execution identities are saved with each result. A unique Rule/revision/target/evidence-fingerprint index enforces idempotency, including concurrent requests.

Work is bounded at **100 eligible rules, 1,000 candidate Trades and 10,000 executions per request**. Limits apply before publication, with no truncation or partial batch. Cancellation is checked through reads, reconstruction and before commit; the final commit is non-cancellable and returns the committed result. Busy/locked storage, overflow and unexpected failures are errors, not assessments. The desktop preserves entered manual fields on failure. Missing/changed sources do not rewrite prior calculated facts.

Additive migration `20261012194059_AddDeterministicRuleChecks` adds nullable criterion/evaluation JSON and evaluation fingerprint columns plus the unique index and narrow insertion guards. Old manual revisions/assessments remain byte-for-byte unchanged and have null typed metadata. The existing Rule envelope/source enum numbers remain compatible; the criterion and evaluator carry their own versions. Lifecycle changes retain the criterion; history update/delete guards remain. Rollback uses native SQLite DROP COLUMN rather than EF's trigger-breaking table rebuild, with a regression proving retained old history and immutability through downgrade/upgrade.

Complete **v1** archives contain the entire database and retain these fields through preflight, offline restore and recovery. Restore still requires exact current migration state; pre-M18.5 archives need the deferred staged-upgrade path. Logical export advances to **v6**, adding `automaticCriterionJson`, `evaluationJson` and `evaluationFingerprint` to the JSON-only Rules tables. Existing fields/four CSVs are unchanged. Earlier exports retain their original meaning but lack these facts; exports remain non-restorable. No AI/provider, screenshots, Trade economics, Journal rules or live-data replacement behavior changes.

### M18.5 verification

Work began on clean `develop`, HEAD `8ea74f7a6419aa45b69e84e7abb18a0b11c473ba`; prior M18 work was already committed. No commit, push or merge is performed here. Only isolated synthetic data is used; provider calls: **0**, production journal access: **none**. Automated WPF renders are not live keyboard, mouse or screen-reader acceptance.

Focused verification: **182 passed / 0 failed / 0 skipped** (Domain 9, Application 20, Infrastructure 106, Desktop 47), recorded in ignored `artifacts/m145-load/m185-focused-complete`. There are **30 new cases**: 12 calculation/contract, 12 SQLite, 6 ViewModel; existing compiled WPF, portability and schema checks were extended. The last tiny selected-rule typed-summary binding is additionally covered by the final full build/run. Tests cover strict boundary/equality, nullable costs, precision/overflow, currency and Account isolation, open/partial and empty evidence, both DST transitions, source/rule conflicts, concurrent idempotency, manual coexistence, cancellation/rollback, unavailable source reads, batch limits and migration retention. Backup → preflight → offline restore/recovery preserves the calculated metadata and idempotency; export v6 schema inventory covers every persisted field.

**Final complete parallel Release: 4,003 passed / 0 failed / 0 skipped**, with Domain **485**, Application **751**, Infrastructure **1,242**, Desktop **1,525**. Evidence: ignored `artifacts/m145-load/m185-full-final` TRX and process diagnostics. Calendar native **87/87**, process elapsed **87.272 seconds**, **32.728 seconds** headroom to the unchanged 120-second aggregate deadline; grid process **22.069 seconds**. No Calendar/Journal timeout occurred. Solution Release build passed with **0 warnings/errors**; in-memory design-time EF model consistency reports no changes; tracked and new-file whitespace checks passed. Branch/HEAD are unchanged; **40 files** remain modified/new, listed below, with no unrelated starting changes to overwrite.

Implementation-stage failures were corrected before the final run: obsolete wording/version/schema-inventory expectations, plus EF's generated Down table rebuild conflicting with retained append-only triggers. Native DROP COLUMN fixes the demonstrated rollback defect; a targeted downgrade/upgrade regression also proves old records and their no-update/no-delete guards remain. No test deadline, concurrency bound, assertion purpose, retry or skip was weakened.

Automated Light/Dark renders at **960 DIP / 96 DPI** and **480 DIP / 240 DPI** show wrapping typed inputs, reachable actions, status/errors and assessment history. Sources: ignored `artifacts/m185-renders`, `RulesViewTests` and `ManualAssessmentViewTests`; normal/narrow examples were visually inspected. Existing real owned-window close veto and focus/scroll assertions remain. These are compiled WPF automation/renders, not live user interaction.

Remaining live checklist (isolated app only): author a strict-Net rule, run it on a Trade with unknown costs, then on a fully costed one; run the day version with two currencies; inspect calculated versus manual history, repeat without changes, cancel a run, and decline discarding a manual draft. Verify Tab order, focus announcements and long evidence scrolling in both themes at minimum width/240 DPI.

Matching CI remains open: the GitHub connector returned no PR-triggered runs for the starting HEAD on 12 October 2026; it does not inspect every workflow event. More importantly, the exact resulting changes are uncommitted. The existing workflow runs on `main` pushes and PRs targeting `main`; after the user's commit/push, verify the tested PR head/merge tree. Local results do not claim matching CI, M17.7 live acceptance or M18.6 work.

### M18.5 exact changed files

- `README.md`
- `docs/backup-restore.md`
- `docs/trading-rules.md`
- `src/PersonalTradingJournal.Application/Backups/IPortableExportService.cs`
- `src/PersonalTradingJournal.Application/Rules/AutomaticRuleChecks.cs`
- `src/PersonalTradingJournal.Application/Rules/ITradingRuleReader.cs`
- `src/PersonalTradingJournal.Desktop/Dialogs/WpfRuleAssessmentDialog.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Rules/RuleAssessmentViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Rules/RulesViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/Calendar/CalendarDayDetailsView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Calendar/CalendarInlineTradeView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Rules/RuleAssessmentView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Rules/RulesView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Trades/TradesView.xaml`
- `src/PersonalTradingJournal.Domain/Rules/AutomaticRuleCriterion.cs`
- `src/PersonalTradingJournal.Domain/Rules/TradingRuleDefinition.cs`
- `src/PersonalTradingJournal.Infrastructure/Backups/PortableExportSchema.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Configurations/TradingRuleRecordConfigurations.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261012194059_AddDeterministicRuleChecks.Designer.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261012194059_AddDeterministicRuleChecks.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/JournalDbContextModelSnapshot.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Records/TradingRuleRecords.cs`
- `src/PersonalTradingJournal.Infrastructure/Rules/TradingRuleRepository.Automatic.cs`
- `src/PersonalTradingJournal.Infrastructure/Rules/TradingRuleRepository.Reads.cs`
- `src/PersonalTradingJournal.Infrastructure/Rules/TradingRuleRepository.cs`
- `tests/PersonalTradingJournal.Application.Tests/Rules/AutomaticRuleCalculatorTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/AutomaticRuleViewModelTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/ManualAssessmentViewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/RulesViewModelTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/RulesViewTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/PortableExportTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Initialization/JournalDatabaseInitializerTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integration/ProductionPersistenceIntegrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integrity/SqlitePersistenceIntegrityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Migrations/InitialMigrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/AutomaticRulePersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookPersistenceTests.cs`

## Original M18.1 rule identity and scope (legacy compatibility)

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
