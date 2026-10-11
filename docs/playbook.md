# Playbook — Domain, persistence and Desktop management

## 12 October Save diagnostics follow-up

Started clean on `develop`, `434a0055591cd71dba5715baf1fee3cca34d0452`. Earlier M17 work is preserved. The original user-observed silent failure is still **not reproduced on real SQLite with synthetic data**. The compiled Save button, bound fields, image metadata, first creation, second revision, navigation and separate-process restart reads continue to pass.

A narrower defect is demonstrated with a controlled read result: after a successful append, the previous code accepted `_ready == true` as proof of successful display even when `GetLatestAsync` returned null. That produced a success message with no current revision / “Create guidance”. It also collapsed the editor before checking the reload. This is **not proof that SQLite returned null in the user's app**, nor a reproduction of the absent-message detail.

Save now captures Setup ID/expected revision before awaiting, verifies the exact committed immutable revision using the history reader (including its complete content), then reloads the catalog/current/revision page. Success requires verified readback and the correct selected Setup/current revision. Verification failure or incomplete reload retains every field/example and displays an explicit post-commit recovery message. It never reports cancellation as rollback after a reported commit. A verified fallback revision prevents a misleading “Create guidance” state if the current-view reload is missing. Refresh/selection/navigation remain guarded while writing; delayed readback regressions test these boundaries. No persistence, schema or Trade economics changes.

### Opt-in Save diagnostics

For a single user-run reproduction, close the old app and launch the intended build from a PowerShell session with `$env:PTJ_PLAYBOOK_SAVE_DIAGNOSTICS='1'`. The user must supply their own intended executable path; agent reproductions must additionally pass `--isolated-data-root` with a fresh absolute synthetic directory. Do **not** send a database, screenshot, guidance, ordinary application logs or full local paths.

The separate `logs/playbook-save-diagnostics.jsonl` file under the selected data directory contains:

- Runtime PID, assembly build MVID/SHA-256, and SHA-256 fingerprints of normalized executable and database paths (not their paths or filenames).
- Attempt ID, invocation, field/example validation result, write start, reported committed revision, exact database readback, reload and final UI state.
- Selected Setup ID before/after, expected/committed revision, elapsed milliseconds, editing/ready flags and a closed set of result codes. No Setup name, freeform exception, authored text, image data or filename is accepted by the diagnostic API.

It is disabled by default, capped at 256 records per app process (including startup) and 512 KiB per file across processes. A full/unwritable diagnostic file must not affect Save. Move aside the old diagnostic file yourself if another capture is needed; disable the environment variable afterwards (`Remove-Item Env:PTJ_PLAYBOOK_SAVE_DIAGNOSTICS`). Sharing just this bounded file is sufficient for the next investigation.

Verify paths locally without sharing them: inspect the recorded PID's executable in Task Manager, compare `Get-FileHash -Algorithm SHA256` of its sibling `PersonalTradingJournal.Desktop.dll` with `BinarySha256`, and compare `DatabasePathFingerprint` with SHA-256 of UTF-8 `Path.GetFullPath(expectedDatabasePath).ToUpperInvariant()`. The fingerprint identifies a path, **not** the database's contents. A different build/path is evidence to resolve before diagnosing persistence. If `Invoked` is absent, check that the enabled process/build is the one being used and that Save is enabled; do not infer a failed database write. `Committed` followed by an unverified readback is not a reason to repeatedly Save; retain/copy edits and use guarded Refresh to inspect storage.

The actual newly built executable was launched with an isolated synthetic root: PID 19896, build MVID `575ea71e-bffb-4fba-9a08-cdafc68635db`, assembly SHA-256 `07C77BCE15E878204FB98A54CA134A36BEB3D345909E5D6466AB28467CA07737`. Executable path, binary hash and expected synthetic database-path fingerprint all matched. That process was stopped. This is runtime identity/startup verification, **not live mouse/keyboard Save acceptance**, and it does not verify the user's running app.

Focused Playbook Release: **46/46 passed**, including real SQLite compiled Save/restart, delayed readback, post-commit exception, missing-current reload, validation visibility, retained examples and bounded/sanitized diagnostics (`artifacts/pre-m177-save-diagnostics-focused`). Release build: **0 warnings/errors**; EF: **no pending model changes**; tracked/new-file whitespace checks pass.

The **final parallel Release suite is not green: 3,781 passed / 86 failed / zero skipped** (476 Domain, 732 Application, 1,196 Infrastructure passed; Desktop 1,377 passed / 86 failed). Every failure propagates the single native 120-second timeout; Playbook tests passed. The baseline passed 3,863/3,863 before the four added regressions, but it does not supersede the final failure. Raw final results: `artifacts/m145-load/pre-m177-diagnostic-final/`. Calendar measurements and remaining uncertainty are in [CI notes](ci-wpf-tests.md#pre-m177-save-diagnostics-and-calendar-full-load-measurement).

Changed files for this follow-up: `README.md`, `docs/playbook.md`, `docs/ci-wpf-tests.md`, `src/PersonalTradingJournal.Desktop/App.xaml.cs`, `src/PersonalTradingJournal.Desktop/ViewModels/Setups/PlaybookViewModel.cs`, new `src/PersonalTradingJournal.Desktop/ViewModels/Setups/PlaybookSaveDiagnostics.cs`, `tests/PersonalTradingJournal.Desktop.Tests/Setups/PlaybookViewModelTests.cs`, and new `tests/PersonalTradingJournal.Desktop.Tests/Setups/PlaybookSaveDiagnosticsTests.cs`.

Matching GitHub Actions and user-run sanitized live reproduction remain open. The workflow requires a PR targeting main (or a main push); these uncommitted changes cannot have a matching run. No production database or provider access, Git write, or M17.7 work. HEAD remains `434a0055591cd71dba5715baf1fee3cca34d0452` on `develop`.

## Pre-M17.7 — Save investigation and action colors

Investigation baseline: `develop`, `eb1b3fd38534051f013a83db6b4f473052143e14`; worktree initially clean. The reported Save failure remains an **open reproduction gate**, not a proven SQLite or selection defect. The supplied screenshot was not available in this task payload. On 12 October the user answered “Yes. No message” to the filled-sections/image and post-Save-message question. The reproduction therefore includes all five filled sections and an image. The running executable/build and a redacted screenshot immediately after Save are still needed to distinguish the observed UI from the passing compiled path; do not supply private guidance or the production database.

### Save trace and observed results

| Phase | Synthetic evidence | Observed result |
| --- | --- | --- |
| Command/validation | Existing focused tests plus real SQLite-backed compiled `PlaybookView` | All five sections required; incomplete validation keeps editing. Complete text creates revision 1; editing creates revision 2. |
| Optimistic concurrency / transaction | `PlaybookSqliteViewTests.RealSqliteWriteRejectionPreservesEveryFieldAndExampleThenCanSave` (new and existing guidance) | SQLite abort trigger writes no partial revision; all five fields and example object remain. Another author commits; stale save is rejected and local edits remain. |
| Post-save reload / selected Setup / paging | `CompiledSaveCreatesAndRevisesWithRealSqliteAndSurvivesReactivation` | Exact Setup identity retained; “Current revision 1/2,” “Edit guidance,” and the matching revision rows appear immediately. Reactivation rereads the committed revision. |
| Images / restart | Same test: real image storage, separate supervised author/read processes | Original image metadata and notes survive revision 2 and process restart. This is automated WPF/SQLite verification, not a manually operated application restart. |
| Live application | Production executable launched only with a new `--isolated-data-root` | Helper read the synthetic empty Dashboard accessibility tree. Capture failed with `FrameArrived timed out`; click failed with `coordinate input geometry is unavailable`. Live create/save/navigation/restart remains unverified. The isolated process was stopped; production data was never opened. |

The inspected command validates fields/examples, sends selected Setup ID plus expected revision, appends metadata/revision in one SQLite writer transaction, commits, resets revision paging, and reloads current guidance. These paths passed without changing production Save code. Immediate-completion fake stores previously left this cross-layer boundary uncovered; the new tests exercise real SQLite, asynchronous image storage, compiled binding and a UI dispatcher synchronization context. A passing synthetic path does **not** establish the cause of the user's reported failure.

12 October follow-up: the regression now enters text through the five compiled TextBox bindings and invokes the actual Save button's automation peer instead of calling the ViewModel command directly. It also uses the production image-content reader/thumbnail decoder and asserts the visible status reads “Revision 1 saved.” / “Revision 2 saved.” Both creation/editing and separate-process reload pass; all **3 SQLite-backed regression cases pass** (`artifacts/pre-m177-save-button`). This additional evidence still does not reproduce the reported silent failure. No production Save logic was changed, and the earlier full-suite timeout remains open rather than being superseded by this focused pass.

### Action presentation

Shared `PtjPlaybook*ButtonStyle` resources reuse the application theme palette: Refresh blue, Archive Setup amber caution, Create/Edit guidance teal/mint, Open full-size chart sky, Close image red-outline, Remove example solid red. Archive still preserves history; Remove example changes only the pending definition; no behavior or retention rules changed. Opaque hover/pressed/disabled colors and a dashed keyboard-focus indicator retain readable text. Narrow wrapping and scroll-to-example-action checks keep actions reachable without overlap. Light/Dark renders cover 960 DIP/96 DPI and 480 DIP/240 DPI, including current, historical, editor, examples, action reachability and palette states.

Focused Release verification: **142 passed** (22 Domain, 19 Application, 59 Infrastructure, 42 Desktop), followed by the final compiled style test passing after the pressed-state contrast adjustment. Release build: **0 warnings/errors**; EF reports no pending model changes; tracked and new-file whitespace checks pass. The initial sandboxed broader run hit the existing DPAPI-dependent Settings check; the supported Windows-context focused run passed without changing that test.

The complete parallel Release run is **not green: 3,777 passed / 86 failed / 0 skipped** (Domain 476/476, Application 732/732, Infrastructure 1,196/1,196, Desktop 1,373 passed/86 failed). All failures propagate the Calendar native child's unchanged 120-second aggregate deadline; all Playbook tests, including the new SQLite/WPF cases, passed in this run. Diagnostics: `calendar-native-20261011-192412-f98bfcecb9564f6ea5a6bf7f664caae9` under the Desktop test output's `TestResults`. It completed 72 STA actions. The last completed `DetachedTableChecksWideAndNarrowLayoutRegardlessOfRunnerDesktop` action took 0.171 seconds; the next began at `19:26:12.792 UTC`, about 0.033 seconds before termination. Last sampled child CPU was 119.70 seconds and working set 491,061,248 bytes. This demonstrates aggregate budget exhaustion while work was progressing, **not** an identified individual hang or a proven cause of contention. No deadline, assertion or harness was weakened; no full-suite retry was used to replace that result. Parent TRX evidence is in `artifacts/pre-m177-full-release`; focused output is in `artifacts/pre-m177-focused-final` and `artifacts/pre-m177-style-final`.

Live mouse/keyboard and matching GitHub Actions acceptance remain open; no commit, push, merge, production-journal access or provider call is part of this investigation.

Exact changed files for this investigation:

- `README.md`
- `docs/playbook.md`
- `src/PersonalTradingJournal.Desktop/Resources/Controls.xaml`
- `src/PersonalTradingJournal.Desktop/Resources/Themes/LightTheme.xaml`
- `src/PersonalTradingJournal.Desktop/Resources/Themes/DarkTheme.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Setups/PlaybookView.xaml`
- `src/PersonalTradingJournal.Desktop/Screenshots/PlaybookExampleImages.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Setups/PlaybookViewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Setups/PlaybookSqliteViewTests.cs` (new)

## M17.6 — Daily AI Review context

Daily Review reads both the original immutable Trade capture and all retained association transitions from the **same read-only SQLite transaction** as the selected Trades. It includes only those Trades selected by the existing New York closure/activity and Account rules. The latest transition must match the Trade's classification before it can be current; older captures cannot be revived by matching a Setup ID. Legacy classifications remain explicitly uncaptured. Removed/replaced captures are historical, not evidence of today's selected plan. Stored captures remain usable after Setup rename/archive.

The provider projection preserves Setup identity/frozen name/description, revision and revision timestamp, association sequence/capture timestamp/Trade opening instant, five guidance sections and ordered example IDs, provenance IDs, validity labels and text annotations. It has **no image storage key, filename, content hash, local path field or image bytes**. `HasImage` only indicates an image reference; AI has not seen it. User prose remains untrusted evidence, not instructions. A plan, link or “valid” example never verifies Trade adherence. No automatic condition checks are introduced.

`daily-coaching.v2` gives every retained transition a stable `trade:{TradeId:N}:playbook:{sequence}` citation (sequence zero is the original capture). `UserWrittenPlaybookContext` observations can cite only these supplied sources; they cannot label Playbook prose as calculated or verified Trade facts. Reference validation does not establish the truth of a generated claim. New saved snapshots retain the complete sanitized projection exactly as sent; v1 analyses remain unchanged/readable and do not acquire retroactive Playbook context.

Saved citations expand the frozen text directly in Daily Review, with capture status, retrospective warning, guidance and annotations. This read-only expansion neither leaves the page nor closes a dirty editor. “Open current Trade” is a separate existing guarded navigation action and may report an unavailable source; it never replaces the saved capture. No current Setup navigation substitutes later definitions.

The complete packet and both providers' existing budgets include this text. Large histories/guidance can make a previously eligible day oversized; no captures or example notes are dropped to fit. No schema, image-retention, Trade-association or economics changes were needed. Verification and remaining live/CI limits are recorded in [Daily Review M17.6](daily-review.md#m176--frozen-playbook-evidence).

## M17.5 — Trade association

### Workflow and meaning

Use the existing **Trading Setup** selector in Trade Details (also available in Calendar's inline Trade details). Selection loads a preview of the current revision. **Associate Setup…** confirms the Setup name and exact revision; save never reloads and substitutes a later definition. **Refresh preview** explicitly reviews a newer revision after a conflict. The Trade must still match the details/confirmation state. Missing, archived, changed or failed reads/writes retain the selection and show a recovery message. Confirmation decline writes nothing. Existing edit/work-in-progress guards disable association while editing, saving or manipulating attachments.

New manual Trades are first created through the unchanged economics workflow, then open Details for explicit association. Creation itself does not silently capture a plan: the subsequent association/classification write is atomic. Imported Trades use the same action without changing executions, provenance or deduplication. A Setup without guidance remains a valid classification, explicitly without a captured revision. No legacy Trade is backfilled. Archived Setups remain readable through existing captures but cannot supply new links; removal/replacement with an active Setup remains allowed.

**Open captured context** reads saved definitions, not the current catalog. Expand an association to read all five guidance sections and exact saved example annotations/classification; image viewing reuses bounded, hash-verified M17.4 presentation. History is newest first, queried in pages of 20 with **Older associations** and a return to newest through Open captured context. Long text wraps in the containing page/modal scroll, without navigation that can discard Trade edits. Capture is user-selected, often retrospective: **associated with this plan does not mean all conditions were followed**.

### Persistence and retention

M17.2's `TradePlaybookContexts` retains its insert-only original capture per Trade. Additive migration **`20261011175435_AddTradePlaybookAssociations`** adds `TradePlaybookAssociations`, keyed by `(TradeId, Sequence)`: selected Setup ID, optional exact revision/full version-1 definition JSON, recording UTC instant and original Trade opening UTC instant. Relink, revision update and removal append; removal has no definition. Sequence numbers are not reused while a Trade exists. Update/direct-delete triggers protect history; legitimate parent Trade deletion cascades its owned transitions. Original captures remain readable. Immutable Setup revisions pin their images after current associations are removed.

Preview reads a consistent SQLite transaction. Confirmation obtains a SQLite writer reservation and rechecks transient SHA-256 tokens over actual Trade, ordered executions, mistakes/screenshots, original capture/latest transition, and active Setup plus canonical latest definition. Equal-audit-timestamp changes are detected. Tokens are not logged. Classification and frozen transition commit together; errors/cancellation before commit roll back, committed results are not reported as cancellation. No retries. Ordinary manual/classification edits append no-capture transitions when Setup changes, so returning to an earlier Setup cannot implicitly reactivate a snapshot. Economics/projections are unchanged.

Frozen JSON uses the established versioned serializer, retaining names, all guidance, example identities, annotations and image metadata. Reads check definition identity against stored Setup/revision. Saved AI evidence is not rewritten; no new Playbook data is sent to AI. Complete backups retain the new table and historical-image inventory; logical export v4 adds `tradePlaybookAssociations` (JSON only), with exact UTC instants and definition JSON. Older-schema archive restore, including pre-M17.5, remains deferred; installed databases migrate additively.

### M17.5 verification

Synthetic tests cover exact confirmation, conflict/decline/failure retention, no implicit latest-revision substitution, legacy/no-guidance classification, concurrent confirmation, same-timestamp changes, atomic rollback, cancellation, relink/removal/restart, imported provenance/economics, archive behavior, paging and retained source images. Shared compiled-WPF controls cover Light/Dark at 960 DIP / 96 DPI and 480 DIP / 240 DPI: accessible names, command targets, read-only expanded content, wrapping and scroll reachability.

Final verification on 2026-10-11: **217 focused tests passed** (Domain 22, Application 18, Infrastructure 133, Desktop 44; `artifacts/m175-final-focused`). Complete **parallel Release: 3,846/3,846 passed**, zero failures/skips: Domain 476, Application 729, Infrastructure 1,189, Desktop 1,452 (`artifacts/m175-final-full`). Infrastructure took 3m46s; Desktop 4m37s. This is 20 additional cases plus extended backup/restore and stale-selection assertions. Release build: **0 warnings/errors**, 29.43s (`artifacts/m175-final-build.log`). EF reports no pending model changes. Tracked and new-file whitespace checks pass.

Eight new association renders were generated in `artifacts/m175-renders`, alongside the existing Playbook renders. Representative collapsed/expanded outputs covering each theme and size were visually inspected. An initial full run exposed outdated explicit schema allowlists and a shared-control `StaticResource` construction failure in Calendar leaf hosts. The schema/FK matrices now assert the additive table explicitly, and the shared control uses Calendar-compatible deferred theme resources. The final full run above includes the corrections; no test assertion, isolation or deadline was removed. The original frozen image and new association metadata also survive the synthetic backup → preflight → offline restore regression.

Live mouse/keyboard interaction, full-size image-window focus return, physical high-DPI and screen-reader acceptance remain unverified. No production journal, provider call or Git write is used. Matching CI must run after the user's commit/push; local tests are not a GitHub Actions result.

### M17.5 changed files

Starting worktree was clean on `develop` at `a9aba0481555dc8863d03e05bae50e857867808c`. Existing M17.1–M17.4 work was already committed and preserved. These 36 paths remain uncommitted (24 modified, 12 new):

- `README.md`
- `docs/backup-restore.md`
- `docs/playbook.md`
- `src/PersonalTradingJournal.Application/Backups/IPortableExportService.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Trades/TradesViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/Calendar/CalendarInlineTradeView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Trades/TradeEntryForm.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Trades/TradesView.xaml`
- `src/PersonalTradingJournal.Infrastructure/Backups/PortableExportSchema.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Configurations/PlaybookRecordConfigurations.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/JournalDbContext.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/JournalDbContextModelSnapshot.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Records/PlaybookRecords.cs`
- `src/PersonalTradingJournal.Infrastructure/Trades/AccountTradeDeletionStore.cs`
- `src/PersonalTradingJournal.Infrastructure/Trades/TradeMutationStore.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Trades/TradesViewModelTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/PortableExportTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Initialization/JournalDatabaseInitializerTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integration/ProductionPersistenceIntegrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integrity/SqlitePersistenceIntegrityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Migrations/InitialMigrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookImagePersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookPersistenceTests.cs`
- `src/PersonalTradingJournal.Application/Trades/ConfirmTradePlaybookAssociation.cs`
- `src/PersonalTradingJournal.Application/Trades/ITradePlaybookAssociationStore.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Trades/TradesViewModel.Playbook.cs`
- `src/PersonalTradingJournal.Desktop/Views/Trades/TradePlaybookAssociationView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Trades/TradePlaybookAssociationView.xaml.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261011175435_AddTradePlaybookAssociations.Designer.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261011175435_AddTradePlaybookAssociations.cs`
- `src/PersonalTradingJournal.Infrastructure/Trades/TradePlaybookAssociationStore.cs`
- `tests/PersonalTradingJournal.Application.Tests/Trades/ConfirmTradePlaybookAssociationTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Trades/TradePlaybookAssociationTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Trades/TradePlaybookViewTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/TradePlaybookAssociationTests.cs`

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
