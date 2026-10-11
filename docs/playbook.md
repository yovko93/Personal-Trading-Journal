# Playbook — Domain, persistence and Desktop management

## M17.4 — Annotated chart examples

### Editor and viewing workflow

Choose an active Setup, then **Create/Edit guidance → Attach chart image…**. Select a single-frame PNG, JPEG or WebP supported by the Windows image codec. Choose **Valid** or **Invalid** and write **Context, condition and lesson**: what the chart demonstrates, the confirmation (or failed condition), and what to learn. This reuses the M17.1 annotation field (meaningful, up to 4,000 characters); it does not add competing text fields or graphic overlays. Classification is explicitly **user-authored, not verified Trade adherence**. The existing five guidance sections remain required. Up to twelve examples may be saved per revision.

Images are at most **8 MiB**, **8,192 pixels per side**, **16 million pixels**, and one frame. Invalid/empty/unsupported or oversized new images are rejected with a safe message before storage. Pending original bytes remain in bounded editor memory; the selected file is never modified or re-encoded. List thumbnails decode to at most 384 pixels on their longest side. **Open full-size chart** independently verifies stored bytes, decodes within the same bounds and opens a themed owned window with scrollbars, accessible annotation, Close image and Escape. Native codec execution itself is not forcibly interruptible; cancellation is checked around bounded reads/decoding. WebP availability depends on the installed Windows codec.

**Save Playbook** stores new images and appends all guidance, classifications, notes and image metadata through the existing expected-revision transaction. Success closes editing and refreshes current/history. Missing/changed files, validation, conflicts, cancellation and storage failures keep all fields and pending images; Refresh remains the guarded recovery action. **Remove example** affects only the pending revision. **Cancel editing** saves nothing and uses the same explicit discard guard as Setup/history/navigation/window closing. Images count as unsaved work, including annotation/classification changes. Save/Cancel stays in the viewport; attachment brings the new classification into view, removal returns focus to Attach. Fields and actions remain scrollable/wrapping at narrow widths.

Historical revisions show their exact saved classification, annotation and image reference. Legacy examples remain **Unspecified**, displayed as “Legacy example · not classified”; they are not relabeled valid automatically. Legacy source-Trade screenshot provenance remains unchanged. Missing images show a recoverable unavailable state while saved notes remain readable. No Trade linking, automatic checks, adherence claims or AI changes are included.

### Persistence, retention and failure boundaries

Additive migration **`20261011171154_AddPlaybookExampleImages`** adds optional file metadata and classification to `PlaybookExamples`, an indexed storage key, and an insert invariant trigger; existing immutable-update/delete triggers remain. `Kind` is 0 (legacy unspecified), 1 (valid), or 2 (invalid). Owned-image fields are storage key, original filename label, exact byte length and SHA-256. Either all image fields are absent or a complete positive-size, classified image is present. Owned images and legacy source-screenshot pins are distinct ownership modes. Existing frozen definition JSON remains contract version 1: optional default-omitted fields are backward-compatible and old canonical definitions are not rewritten.

New files use the existing safe screenshot storage, random non-overwriting keys and temporary-file publication. Metadata/revision append is atomic in SQLite; the **filesystem and SQLite are not one transaction**. Save verifies frozen image size/hash under a SQLite writer reservation. On any failed/cancelled append, compensating cleanup checks both Trade and every historical Playbook reference before removing a newly stored file, using the screenshot capture lease and writer reservation. Cleanup failures are explicit and never discard editor state. Process termination between file publication and metadata commit can leave an unreferenced orphan; no destructive orphan collector is added. The database never intentionally publishes a reference before its file exists. External/manual file tampering is not prevented; read/save/backup detect missing or changed content.

Every immutable revision retains its owned image. Removing an example from current guidance, archiving its Setup, or deleting an unrelated Trade does not free a historical image. Trade/screenshot/bulk-Account deletion excludes storage keys retained by owned examples, including case-insensitive aliases. Existing **source-screenshot pins** continue blocking deletion of their original screenshot/Trade. An image shared with a Trade is never physically deleted while either requires it.

Backup inventory is the union of staged-database Trade screenshots and **all revision-owned image keys**, captured once per portable key. Producer and preflight compare bytes against frozen image size/hash as well as the manifest. Offline restore and recovery use this same path. Export v3 adds the five image/classification columns to JSON `playbookExamples`; images remain metadata-only in exports. No credentials or absolute paths enter the manifest. Pre-M17.4 archives are recognized older schemas but restore is deferred; installed databases migrate additively. See [backup details](backup-restore.md#m174--owned-playbook-images).

### Verification and live limitations

Tests use synthetic bytes and isolated migrated databases only. Domain/Application cases cover bounds, classifications, success, cancellation, stale/failed appends and compensation (including cleanup failure). SQLite cases cover exact bytes/history after removal and restart, frozen Trade context, shared-file retention through single/bulk deletion, missing/tampered files, metadata rollback, legacy migration/immutable JSON, export and backup → preflight → offline restore with a retained recovery archive. Desktop cases cover add/classify/view-target/save/remove, dirty guards, failure retention, count limits and current/historical states. Compiled WPF tests render Light/Dark at **960 DIP / 96 DPI** and **480 DIP / 240 DPI**, with required labels, keyboard-focusable actions, wrapping, decode bounds and scroll reachability.

Final focused verification: **223 passed** — Domain 22, Application 15, Infrastructure 156, Desktop 30 — zero failures/skips (`artifacts/m174-final-focused`). Final complete **parallel** Release: **3,826/3,826 passed** — Domain 476, Application 726, Infrastructure 1,178, Desktop 1,446 (`artifacts/m174-final-full`); Infrastructure 3m48s, Desktop 4m42s. This includes **35 added cases**, with the existing compiled-WPF case extended rather than weakened. Release build: **0 warnings/errors** (`artifacts/m174-release-build.log`). EF reports no pending model changes; tracked and new-file whitespace checks pass.

Twenty synthetic renders were generated in `artifacts/m174-renders`; normal/narrow Light/Dark example-editor and saved-example outputs were visually inspected. Rendering revealed a default-black provenance label in Dark mode and blank thumbnail space for image-less legacy notes; these were fixed with theme foreground and image visibility bindings, with regression assertions. Storage review added a controlled interleaving regression: commit Trade deletion, save a new Playbook reference to the still-existing file, then perform delayed cleanup. The production file service now rechecks both reference catalogs under the capture/writer locks and retains the image. No test assertion, deadline or isolation protection was removed. An earlier 3,825-test full run also passed, but the final result above includes that additional regression and fix.

Automated rendering is not live interaction. Remaining manual checks: native image picker; actual Tab/Shift+Tab and attachment/removal focus; full-size window scrolling/Escape/Close and return focus; Windows WebP codec; screen-reader narration; touchpad/physical high-DPI behavior. No production data, provider request or Git write was used. Matching GitHub Actions remains a separate gate after the user's commit/push. Branch/HEAD remain **`develop` / `e1accad6aeaa25b159bb554fb6a8844fd278dae0`**. The initial worktree was clean (M17.1–M17.3 were already committed); final worktree contains **34 modified tracked files and 16 new files**, all uncommitted.


### Exact M17.4 changed files

Paths below are repository-relative; the starting worktree was clean. The migration, backup/export updates and deletion-retention checks are needed to preserve the newly owned images beyond the editor.

- `README.md`
- `docs/backup-restore.md`
- `docs/playbook.md`
- `src/PersonalTradingJournal.Application/Backups/IPortableExportService.cs`
- `src/PersonalTradingJournal.Application/Screenshots/ITradeScreenshotFileStorage.cs`
- `src/PersonalTradingJournal.Application/Screenshots/ScreenshotStagingCleanupException.cs`
- `src/PersonalTradingJournal.Application/Setups/IPlaybookImageStorage.cs`
- `src/PersonalTradingJournal.Application/Setups/SavePlaybookExamplesUseCase.cs`
- `src/PersonalTradingJournal.Desktop/App.xaml.cs`
- `src/PersonalTradingJournal.Desktop/Screenshots/PlaybookExampleImages.cs`
- `src/PersonalTradingJournal.Desktop/Screenshots/WpfTradeScreenshotFilePicker.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Setups/PlaybookExampleItem.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Setups/PlaybookViewModel.Examples.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Setups/PlaybookViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/Setups/PlaybookView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Setups/PlaybookView.xaml.cs`
- `src/PersonalTradingJournal.Domain/Setups/PlaybookExample.cs`
- `src/PersonalTradingJournal.Domain/Setups/PlaybookExampleImage.cs`
- `src/PersonalTradingJournal.Infrastructure/Backups/BackupArchiveService.cs`
- `src/PersonalTradingJournal.Infrastructure/Backups/PlaybookImageInventory.cs`
- `src/PersonalTradingJournal.Infrastructure/Backups/PortableExportSchema.cs`
- `src/PersonalTradingJournal.Infrastructure/Backups/RestorePreflightService.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Configurations/PlaybookRecordConfigurations.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Mapping/PlaybookPersistenceMapper.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261011171154_AddPlaybookExampleImages.Designer.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261011171154_AddPlaybookExampleImages.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/JournalDbContextModelSnapshot.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Records/PlaybookRecords.cs`
- `src/PersonalTradingJournal.Infrastructure/Screenshots/LocalTradeScreenshotFileStorage.cs`
- `src/PersonalTradingJournal.Infrastructure/Screenshots/RetainedScreenshotFileStorage.cs`
- `src/PersonalTradingJournal.Infrastructure/Screenshots/TradeScreenshotDeletionStore.cs`
- `src/PersonalTradingJournal.Infrastructure/Setups/PlaybookImageStorage.cs`
- `src/PersonalTradingJournal.Infrastructure/Setups/TradingSetupPlaybookStore.cs`
- `src/PersonalTradingJournal.Infrastructure/Trades/AccountTradeDeletionStore.cs`
- `src/PersonalTradingJournal.Infrastructure/Trades/TradeDeletionStore.cs`
- `tests/PersonalTradingJournal.Application.Tests/Setups/SavePlaybookExamplesTests.cs`
- `tests/PersonalTradingJournal.Application.Tests/Setups/SaveTradingSetupPlaybookUseCaseTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Setups/PlaybookExampleEditorTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Setups/PlaybookViewModelTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Setups/PlaybookViewTests.cs`
- `tests/PersonalTradingJournal.Domain.Tests/Setups/PlaybookExampleImageTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/PortableExportTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Initialization/JournalDatabaseInitializerTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integration/ProductionPersistenceIntegrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Migrations/InitialMigrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/PersistenceRegistrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookImagePersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookPersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Screenshots/LocalTradeScreenshotFileStorageTests.cs`

## M17.3 — Desktop Playbook management

The existing **Planning → Playbook** destination is now a usable page, not a placeholder. It reads the same `TradingSetup` catalog used by Trade classification and Trading Setups. Create/rename Setup identities in Trading Setups; this page adds no competing catalog. Active and archived Setups are selectable, including those without any guidance.

### Workflow and safeguards

1. Select a Setup. Current guidance shows its numbered revision and five readable sections: market context, entry conditions, confirmations, invalidation and execution guidance. No guidance is a valid empty state, not a reason to disable the Setup elsewhere.
2. Choose **Create guidance** or **Edit guidance** on an active Setup. All five fields require meaningful text, at most 4,000 characters after existing Domain normalization. No input is silently truncated. M17.3 preserved existing example metadata; M17.4 above adds image/annotation editing.
3. **Save Playbook** (also Ctrl+S) calls `SaveTradingSetupPlaybookUseCase` with the revision loaded when editing began. Success appends atomically and closes editing. If the post-commit list refresh fails, the committed guidance is still shown with an explicit saved-but-refresh-failed message; a retry cannot accidentally imply the original save failed.
4. Validation, cancellation, stale revision/archived owner, unavailable Setup and storage errors retain all five entered fields. Conflict recovery is explicit **Refresh**: copy local text if needed, then accept the existing discard confirmation to read the latest version. No automatic merge or force overwrite is offered.
5. **Cancel editing** saves nothing and asks before discarding changed fields. Change-then-revert is clean. Setup switching, revision/page switching, Refresh, destination navigation and main-window closing use the same guard. A veto keeps selection, editor and text intact. An in-progress write blocks leaving; **Cancel operation** requests cancellation under the repository's pre-commit rules. Reads are cancelled/invalidation-tagged on deactivation so late results cannot replace a newer selection.
6. **Archive Setup / Reopen Setup** uses the existing activation policy and confirmation. Archived guidance and history remain readable; reopen before editing. Trade classifications, frozen context and immutable history are not changed by this lifecycle action.
7. **Immutable revisions** uses descending numbered history with exclusive cursors: ten rows plus one look-ahead, never the whole history. **View revision** opens the frozen version in a distinct shaded, read-only panel; **Return to current** returns to current guidance. Browsing never creates a revision and there is no revision deletion action.

The main shell remains the only page heading. Prose/editing is limited to a comfortable 760-DIP width; fields are 112 DIP high with internal text scrolling. The main vertical scroller receives wheel input at field edges. Save/Cancel stays outside the content scroller at the bottom of the viewport. Toolbars and history controls wrap at narrow widths. Controls carry accessible names and required-field labels, status uses a polite live region, and shared Light/Dark resources supply focus and contrast. Historical labels and disabled authoring controls distinguish history by more than color.

No Trade-linking command, adherence assessment, executable rule, AI prompt/request, new persistence rule or migration is introduced. A link or snapshot remains **not proof that a plan was followed**. M17.2 retention and pre-M17 archive compatibility limits below remain unchanged.

### M17.3 verification and remaining acceptance

Focused verification: **101 passed** (13 Domain, 8 Application, 34 Infrastructure, 46 Desktop), zero failures/skips in `artifacts/m173-focused-verified`. There are **23 new Desktop cases** covering empty/current/history states, all five required fields and overlength retention, exact revision Save/example preservation, conflicts, cancellation, storage/read failures, post-commit refresh failure, stale/deactivated reads, archive/reopen, page cursors, unchanged/dirty navigation and owner-close guards. The compiled-view case covers both themes at 960 DIP / 96 DPI and 480 DIP / 240 DPI, text binding, selector veto/rollback, accessibility labels, Ctrl+S, visible action geometry, wheel/bottom reachability and the actual `MainWindow.OnClosing` override. Twelve current/historical/editor renders were generated; representative normal and narrow Light/Dark outputs were visually inspected in `artifacts/m173-renders`.

The first render assertion incorrectly used native `IsVisible` on a detached visual; it now checks actual visibility bindings, nonzero geometry and viewport bounds. A broader sandbox navigation check passed 214 cases but failed two pre-existing synthetic AI/DPAPI flows; the direct synthetic credential Save test also failed under the sandbox. The same credential and acceptance tests passed outside the sandbox without code changes, real keys or HTTP. No assertions, harness deadlines or concurrency protections were relaxed.

Final complete parallel Release: **3,791/3,791 passed**, zero failures/skips — Domain **467**, Application **719**, Infrastructure **1,166**, Desktop **1,439** (`artifacts/m173-full`). Infrastructure took 2m45s; Desktop 3m36s. Final Release build: **0 warnings/errors**, 3.12s. EF model consistency reports no pending changes. Tracked and new-file whitespace checks pass. The starting worktree was clean on `develop`, HEAD `6b5c012e5d4a5a25b8c8c856f84b1311fd2681ae`; M17.1/M17.2 were already committed and remain intact. M17.3 changes are uncommitted; no commit/push/merge was performed.

All fixtures are synthetic; no production journal or provider is used. Detached compiled-WPF rendering and invoking `MainWindow.OnClosing` are automated evidence, not live mouse/keyboard or screen-reader acceptance. Live checks still required: Tab/Shift+Tab through the Setup selector, editor and revision pages; focus after Save/Cancel; actual mouse/touchpad scrolling in long fields; discard confirmation veto/acceptance on navigation and window close; and a physical minimum-width window on a 240-DPI desktop. Matching GitHub Actions cannot verify uncommitted changes; a run after the user's commit/push is a separate gate.

Exact M17.3 files (relative to the repository):

- `README.md`
- `docs/playbook.md`
- `src/PersonalTradingJournal.Desktop/App.xaml`
- `src/PersonalTradingJournal.Desktop/App.xaml.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/MainWindowViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Setups/PlaybookViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/Setups/PlaybookView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Setups/PlaybookView.xaml.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Navigation/MainWindowViewModelTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Navigation/PlaybookNavigationTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Setups/PlaybookViewModelTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Setups/PlaybookViewTests.cs`

## M17.2 — SQLite persistence and historical snapshots

M17.1 below records the initial contract milestone. M17.2 implements its persistence boundary with additive migration **`20261011154034_AddPlaybookHistory`**. No existing table, Setup classification, Trade execution, screenshot key or economics is rewritten. Existing Setups remain fully usable without guidance. Strategy is not recreated.

### Storage and concurrency

- `PlaybookRevisions`: immutable full guidance and frozen Setup name/description, UTC recording time, contract version 1; composite primary key `(TradingSetupId, Revision)`. The latest revision is the current guidance, so no mutable duplicate of the guidance can drift from its history.
- `PlaybookExamples`: ordered annotation and original source Trade/screenshot identities, keyed by Setup/revision/example ID. Stable example identity can recur in later revisions, with independently frozen annotations. A unique position index preserves ordering and the existing 12-example limit.
- `TradePlaybookContexts`: one insert-only capture per Trade, contract version, original opening/capture instants and the **complete versioned definition JSON**, including all guidance and examples. Reads deserialize this saved value; they never substitute the current Setup or current guidance. A composite foreign key also verifies the referenced definition exists.

`TradingSetupPlaybookStore` implements authoring plus a separate bounded history reader (descending revision, exclusive cursor, limits 1–100; examples loaded in a batch). `TradePlaybookContextStore` supplies insert/read repository operations only. All are DI-wired, as is `SaveTradingSetupPlaybookUseCase`. No Desktop authoring or Trade-linking command calls them yet, and no legacy Trade receives an automatic capture.

Append obtains SQLite's **BEGIN IMMEDIATE writer reservation before reading** the owner/latest revision. It checks expected revision, active/existing Setup, owner audit time and frozen name/description, monotonic revision time and example screenshot ownership. Guidance and examples commit together. Stale expected revisions or owner state return false; invalid example provenance is a clear failure; no partial revision is left. The next committed revision is exactly previous + 1. SQLite triggers reject revision/example UPDATE or DELETE, so removing the latest revision cannot reset numbering. There is deliberately no revision-deletion API.

Trade-context insertion independently rechecks actual Trade classification, opening instant and expected audit timestamp, active Setup, and byte-equivalent canonical content of the persisted definition. It refuses a second capture or a fabricated definition. Cancellation is observed before commit; cancellation/error after EF changes but before commit rolls back. After the final cancellation check, commit is non-cancellable and success is returned if committed. SQLite's synchronous writer-lock acquisition follows its connection timeout; cancellation cannot interrupt that call itself. There are no automatic retries.

### Ownership, archiving and retention

Archiving continues to mean `TradingSetup.IsActive == false`. It prevents new guidance/captures but never blocks historical reading. Reactivation continues existing numbering. A Setup with Playbook history cannot be hard-deleted; archive it instead. Original name, description, guidance and annotations remain frozen even if the Setup is renamed or Trade classification is changed/cleared.

Screenshot **metadata** is pinned through a restrictive foreign key from every historical example to the existing `TradeScreenshots` row. A pinned screenshot cannot be removed, nor can its owning Trade be deleted (including Delete All Trades); these operations fail before any database or physical-file deletion. Existing images are not copied or reassigned to a competing storage system. Newer revisions that omit an example do not release the older revision's reference. There is no history/media purge operation in M17.2. A future retention feature must explicitly preserve the historical image or explain loss before releasing a pin; it must not silently cascade.

Single-screenshot/Trade cleanup also respects surviving case-insensitive shared storage keys in legacy databases: deleting an unpinned alias does not remove another owner's image. Bulk deletion already had this shared-key check, now also checks Playbook pins and includes newly captured context in its confirmation fingerprint. A fresh context invalidates an older bulk-delete plan.

Text-only source Trade IDs are provenance, not a claim that the Trade still exists. Screenshot-backed examples verify their source ownership when saved. These checks do not read, decode or certify image content, detect external edits, or recreate already-missing files; the existing complete backup process still rejects missing referenced files. Context is Trade-owned and cascades only when that Trade is legitimately deleted; the Setup's definition history remains. **Neither the link nor the frozen value proves the plan was followed.** No rule evaluation or AI prompt changes are introduced.

### Compatibility and limits

Current-schema complete backups preserve all three tables, SQLite triggers/FKs and referenced image files. M16's existing read-only preflight and recovery-protected restore remain intact. A pre-M17 archive has a known older migration prefix: it is recognized as requiring a staged upgrade but **cannot be restored by this build**. No restore-time migration is attempted. Normal additive migration of an existing installed database is supported and tested separately from archive restore. Keep older archives unchanged until a supported staged-upgrade workflow exists.

Portable logical export is now **version 2**, extending the explicit M16.6 allowlist with `playbookRevisions`, `playbookExamples` and `tradePlaybookContexts` JSON tables. Original identities, ordering, guidance/annotations, UTC instants and the exact frozen `definitionJson` are exported; four CSV files and existing field semantics are unchanged. Playbook detail is JSON-only; screenshots remain metadata-only. Exports are unencrypted and are not restorable backups. Existing version-1 export files are not modified.

Repository insertion freezes an already-selected/classified Trade; atomic integration with a future classification/edit command and append-only context corrections remain later work. M17.2 deliberately exposes no context replacement/edit UI. No paid provider, production journal or live UI acceptance is involved.

### Verification

Synthetic migrated-SQLite tests cover service recreation/restart, same-number concurrent edits, owner changes at equal timestamps, rollback during example insertion, cancellation after SaveChanges/before commit, archive/reactivation, deterministic history cursors, immutable database guards, forged/stale context, context ownership/cascade, pinned/shared-key deletion safety (including Unicode aliases), and upgrading populated pre-M17 data. Backup → preflight → offline restore checks the frozen values and referenced file with a retained recovery archive. A separately constructed real pre-M17 schema archive is rejected without migrating the installed synthetic data.

Final local verification on 2026-10-11: **223 focused cases passed** (80 Application + 143 Infrastructure). Complete parallel Release: **3,768/3,768 passed**, zero failures/skips: Domain 467, Application 719, Infrastructure 1,166, Desktop 1,416. There are **35 new cases** (34 SQLite/integration + one Application cleanup regression). Release build: **0 warnings/errors**; in-memory design-time EF model consistency and tracked/new-file whitespace checks passed. Evidence is in ignored `artifacts/m172-focused-app`, `artifacts/m172-focused-infra-verified`, and `artifacts/m172-full-final`.

An initial full run exposed five Infrastructure failures: four fixed schema/migration expectations that needed the additive tables, and the logical-export completeness invariant. Expectations now assert the new keys/FKs/indexes explicitly; the prior coaching-only migration test targets its own migration. The export invariant was preserved by implementing version 2, not removing its assertion. The initial Desktop suite passed; no harness deadlines, concurrency or coverage were relaxed. No production data, live UI/provider calls or Git writes occurred. Matching GitHub CI must run after the user's commit/push; local tests are not CI acceptance.

### Exact M17.2 changed files

Paths are relative to the repository; the starting worktree was clean at `86c4d511efc7452f3e00869db89cc099e0daad2f` on `develop`.

- `README.md`
- `docs/backup-restore.md`
- `docs/playbook.md`
- `src/PersonalTradingJournal.Application/Backups/IPortableExportService.cs`
- `src/PersonalTradingJournal.Application/Screenshots/DeleteTradeScreenshotUseCase.cs`
- `src/PersonalTradingJournal.Application/Screenshots/TradeScreenshotDeletionInfo.cs`
- `src/PersonalTradingJournal.Application/Setups/ITradingSetupPlaybookHistoryReader.cs`
- `src/PersonalTradingJournal.Application/Setups/ITradingSetupPlaybookStore.cs`
- `src/PersonalTradingJournal.Application/Trades/ITradePlaybookContextStore.cs`
- `src/PersonalTradingJournal.Domain/Setups/TradingSetupPlaybookRevision.cs`
- `src/PersonalTradingJournal.Domain/Trades/TradePlaybookContext.cs`
- `src/PersonalTradingJournal.Infrastructure/Backups/PortableExportSchema.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Configurations/PlaybookRecordConfigurations.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/JournalDbContext.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Mapping/PlaybookPersistenceMapper.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261011154034_AddPlaybookHistory.Designer.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261011154034_AddPlaybookHistory.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/JournalDbContextModelSnapshot.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Records/PlaybookRecords.cs`
- `src/PersonalTradingJournal.Infrastructure/Screenshots/TradeScreenshotDeletionStore.cs`
- `src/PersonalTradingJournal.Infrastructure/Setups/TradingSetupDeletionStore.cs`
- `src/PersonalTradingJournal.Infrastructure/Setups/TradingSetupPlaybookStore.cs`
- `src/PersonalTradingJournal.Infrastructure/Trades/AccountTradeDeletionStore.cs`
- `src/PersonalTradingJournal.Infrastructure/Trades/TradeDeletionStore.cs`
- `src/PersonalTradingJournal.Infrastructure/Trades/TradePlaybookContextStore.cs`
- `tests/PersonalTradingJournal.Application.Tests/Screenshots/DeleteTradeScreenshotUseCaseTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/PortableExportTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Initialization/JournalDatabaseInitializerTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integration/ProductionPersistenceIntegrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integrity/SqlitePersistenceIntegrityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Migrations/CoachingAnalysisMigrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Migrations/InitialMigrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookPersistenceTests.cs`

## M17.1 foundation — original scope and existing catalog

Playbook extends **Trading Setup**, the existing repeatable-pattern catalog. Strategy was deliberately removed in `RemoveStrategies`; it is not a current parent entity and is not reintroduced. A Playbook is an owned definition for a Setup, not a competing catalog with another name, identity or lifecycle. Existing Setups without a Playbook remain valid. Existing `Trade.TradingSetupId` remains mutable classification metadata; no existing Trade or import is backfilled or reinterpreted.

M17.1 introduced immutable Domain values and an Application authoring policy, without persistence or UI. M17.2 above now implements the repository, DI and migration; current Setup/Trade editors still do not author or capture Playbook content. Desktop authoring, automatic rule evaluation and AI changes remain outside both milestones.

## Version 1 contract and validation

| Value | Contract |
| --- | --- |
| `TradingSetupPlaybookRevision` | Owned identity `(TradingSetupId, Revision)`. Starts at 1, checked increment on each explicit accepted revision, never changes Setup identity. Freezes Setup name/description, guidance and UTC recording time. |
| `PlaybookGuidance` | Required market context, entry conditions, confirmations, invalidation, execution guidance. Each is trimmed, non-whitespace and at most 4,000 UTF-16 characters. This validates completeness of the authored definition, not trading correctness. No executable expressions. |
| `PlaybookExample` | Optional, ordered examples: at most 12, unique non-empty stable example IDs. Required annotation has the same 4,000-character limit. An annotation can stand alone or carry a source Trade ID and optional screenshot ID; screenshot provenance requires a Trade ID. |
| Collections | Defensive bounded copy and read-only exposure; values are immutable. Editing annotations retains their example identity in a new definition revision. Removing an example from a new revision does not mutate an old revision. |
| Audit | Zero UTC offset; definition time cannot precede the current Setup audit state or previous revision. Capture time cannot precede any supplied Trade, Setup or definition state. |

Archiving means existing `TradingSetup.Deactivate`, with `IsActive == false`; no second archived flag is introduced. Archived Setups cannot supply new Playbook revisions or new Trade-context captures. Existing revisions/snapshots remain readable. Reactivating uses the same Setup ID and continues the revision sequence. Existing hard-delete behavior is unchanged in M17.1; before persistence is wired, deletion must treat stored Playbook revisions/context as references and retain history rather than cascade it away.

`SaveTradingSetupPlaybookUseCase` requires an explicit Setup ID and expected latest revision (0 for first definition), validates the owning Setup and produces a new revision. `ITradingSetupPlaybookStore.TryAppendAsync` must later compare the revision **and** owning Setup's active/existing/audit state atomically. Stale revision, archive/delete/rename races return failure without appending. Cancellation before commit writes nothing; a committed append must not be reported as cancellation. The use case checks cancellation before reads and before append. M17.1 fake-store tests prove orchestration, not database concurrency; a later persistence milestone must prove the atomic contract in SQLite.

## Historical Trade meaning

`TradePlaybookContext` freezes Trade ID, opening instant at capture, capture UTC time, and the **complete immutable definition revision**, not just a link. Setup classification, Setup identity and definition identity must match at capture. Later definition edits, renaming, archiving or changing/clearing the Trade's Setup link cannot mutate the captured text. `IsRetrospective` explicitly identifies capture after the recorded entry time. A retrospectively recorded definition must never be presented as a contemporaneous plan; even an equal-time capture is user-selected context, not independently verified evidence.

**A Setup link or context snapshot is never proof that the plan was followed.** M17.1 assigns no pass/fail, rule violation, adherence score or inferred intent from fills, screenshots, profits or missing evidence. The definition can contain user-authored expectations; actual executions retain their existing economic meaning.

Future integration must capture and persist context with an explicit Trade action, in the same transaction as the intended classification/change, and recheck current Trade/Setup/definition versions. Retain the full frozen value with a serialized contract version; do not resolve historical text from the latest Setup. Corrections require explicit append-only history rather than overwriting the original context. Do not automatically capture all legacy classifications. M17.1 does not add a field to the persisted Trade aggregate that existing mappers would silently drop.

### Screenshot examples and retention boundary

The existing `TradeScreenshot` remains Trade-owned metadata with a storage key; file lifecycle belongs to the existing screenshot services. `PlaybookExample.FromScreenshot` copies its authoritative Trade/screenshot identities plus a separate annotation. It does not read/copy images, take ownership of the storage key, validate image bytes or promise an image remains available after Trade/screenshot deletion. Text annotations survive in frozen values; unresolved provenance must be shown as unavailable, never redirected to a different source.

M17.1 provided provenance only. M17.2 now retains referenced images by pinning their existing screenshot rows and preventing deletion, as documented above; it does not create an independently owned image copy or freeze externally modified bytes. Allowing a source Trade/image to be deleted while preserving its historical image would require a later explicit media-copy/retention design with integrity and backup rules. No local paths or binary data are part of the Domain values.

## Synthetic NQ example (illustration, not a trading recommendation)

Use one existing Setup named **NQ — range-break retest**, with description **Synthetic training example**. Its first Playbook revision could contain:

- **Market context:** A manually marked session range; record whether price is inside or outside it. Do not infer context solely from profit.
- **Entry conditions:** A move above the marked range followed by a retest; record the proposed entry before execution.
- **Confirmations:** Describe the observed retest and acceptance above the boundary. If confirmation is absent, document that absence rather than inventing it.
- **Invalidation:** The planned premise no longer applies after price returns inside the marked range; record the chosen boundary and cancellation condition.
- **Execution guidance:** Record planned size, entry, protective exit and management intent before acting; record deviations separately. This prose neither places orders nor calculates risk.
- **Optional example:** A synthetic Entry screenshot with annotation “Marked range boundary and retest; this image illustrates the pattern, not compliance.” Use its real synthetic Trade/screenshot IDs through `FromScreenshot`, never an absolute path.

A Trade captured with revision 1 keeps those words even if revision 2 changes the confirmation. A later classification-only assignment remains classification only. No NQ contract specification, source economics, cost assumption or recommendation is inferred from this example.

## Verification and remaining work

Focused Release tests cover required/bounded fields, defensive copies, stable example/provenance IDs, UTC/monotonic audits, Setup identity, revisions, archive/reactivation, stale appends, cancellation/write failure, matching Trade context and immutable history after later edits. Existing Setup/classification/screenshot regressions are included. Synthetic in-memory records only; no production journal, screenshots or provider access.

Local verification on 2026-10-11: **106 Domain + 79 Application focused cases passed**, including **13 + 8 new cases**. Complete Domain/Application Release suites: **467 + 718 = 1,185 passed**, zero failures/skips. Solution Release build: **0 warnings/errors**. The complete Infrastructure/Desktop suite was not rerun for this Domain/Application-only milestone. Tracked and new-file whitespace checks pass.

Persistence/migration and retention were outside M17.1; M17.2 now implements and tests them above. Transactional integration with future Trade-linking commands, UI/accessibility, automatic checks and AI integration remain later work. No Desktop/live acceptance or matching GitHub CI is claimed for this service milestone.
