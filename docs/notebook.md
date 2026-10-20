# Notebook

Current checkpoint: **M22.8A image storage and application operations; M22.8 overall acceptance pending**. Logical export **v17** includes canonical Notebook records, recovery checkpoints and retained owned-file metadata. Current-schema archive **v1** includes required Notebook originals; JSON/CSV are not restore inputs. Desktop image insertion/rendering, duplication/permanent deletion, file reclamation, linked-target navigation and full integrated M22.11 acceptance remain deferred. Earlier milestone sections below are historical decisions/results.

## M22.8A — image storage and application operations

20 October 2026. Actual starting baseline: **clean `develop`, `e0682d3a898742ff8612857de5a2245267eae041`**, worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. M22.7 automated verification was already committed. Inspected repository/ancestor instructions (no applicable `AGENTS.md`), this contract, existing Deferred commands, recovery, screenshot storage/capture/retention, Playbook admission, export and archive/restore implementations. One agent; no subagents. This phase preserves existing work and makes no Git publication or production/provider requests.

### Operations and admission

- `INotebookImages.ImportAsync` receives a caller-owned readable stream, note ID, expected version, new attachment ID and the complete proposed document. The source path/name is never stored. Import returns a verified canonical snapshot through the existing `AddNotebookImage` command. Stream ownership stays with the caller; managed read/write handles are disposed internally. No clipboard or image-insertion control is added.
- Existing `UpdateNotebookImage` and `RemoveNotebookImage` commands now persist caption, alternative-text, ordering and reference removal. The immutable domain image model is unchanged. Image positions/captions/alt text must match the document's image nodes exactly; the command's named delta must preserve every unrelated node, identity, mark, reference and metadata value. Ordering is a move of the named image in the document plus matching ordered metadata. The canonical document, derived search text, metadata, audit time and one version advance commit together.
- Ordinary title/body saves cannot smuggle file metadata changes. Archived/Trashed notes remain read-only to image mutations. Unsupported/malformed original documents are not rewritten or replaced by an empty body. Duplicate and permanent-note deletion remain explicitly **Deferred**.
- Byte reads are bounded before decode: nonempty, at most **8 MiB**; signatures must be PNG/JPEG/WebP. Host decoding enforces one frame, dimensions at most **8,192 each**, at most **16,000,000 pixels**, and actual pixel materialization. APNG/WebP animation chunks are explicitly rejected even if an installed decoder exposes only the first frame. The existing Playbook decoder policy is reused by a thin Windows admission adapter, serialized to bound concurrent full decoding. Original bytes are never re-encoded. Cancellation is checked during bounded reads and around decode/staging/commit boundaries; native decode itself is a bounded synchronous operation.
- The decoder is an Application interface; all WPF code remains in Desktop. Non-Desktop hosts fail closed with `Unsupported` unless they provide a decoder. Import distinguishes Invalid, UnsupportedContent, TooLarge, Conflict, NotFound, Cancelled and StorageFailure. `ReadOriginalAsync` explicitly distinguishes Success, Missing, Unreadable, Invalid, Unsupported, TooLarge and Cancelled, verifying frozen size/hash before returning bounded original bytes and stored file metadata. Trade screenshot references return Unsupported through this **owned-image** reader; they do not grant ownership or authorize Trade mutation.

### Storage, commit and retention

Originals use a dedicated **`notebook-{noteId:N}-{imageId:N}-{random:N}.{png|jpg|webp}` namespace** in `IApplicationPaths.ScreenshotsDirectory`, normally `%LocalAppData%/PersonalTradingJournal/screenshots`. This deliberately reuses the documented flat managed-key model, application paths, backup attachment layout and screenshot capture lease instead of introducing another image root/archive path convention. No source paths or file-name identities survive import. A newly generated key cannot collide with a referenced Trade screenshot key. Resolution rejects path traversal, links/aliases and unsafe keys using the existing storage and opened-handle identity checks.

Sequence: validate version/document delta → read bounded source and decode → write/flush/close a private temporary file → publish a fresh immutable original → expected-version SQLite writer transaction inserts the ownership ledger/current reference and writes the canonical document/projection → verify readback → non-cancellable commit → return Success. Direct add/restoration commands also verify original size/hash and actual admission; a caller-supplied metadata array is not proof of a valid image.

Before-publication failure/cancellation cleans the private temporary file. After publication, unsuccessful canonical writes are resolved through a fresh non-cancelled database read. A verified committed result returns Success even if its original acknowledgement/cancellation was ambiguous. Compensation acquires the existing capture lease and an immediate SQLite writer transaction in the established lock order, rechecking all ownership catalogs before deletion. A failed/unavailable resolution **never deletes the original**; `CleanupPending` and sanitized issues explicitly report uncertain outcomes or failed cleanup. Process death can leave uncommitted staging/orphan bytes; this phase does not promise automatic orphan reclamation.

Additive migration **`20261020180223_AddNotebookOwnedImageFiles`** introduces `NotebookOwnedImageFiles`: application-assigned attachment ID, original Note ID, unique storage key, byte count and SHA-256. It has no note FK/cascade, so future source deletion cannot silently destroy recovery ownership. Current image references remain in `NotebookImages`; the ledger records immutable file identity, not a second image/document model. Existing owned metadata is adopted into retention before image edits can remove its current reference.

Reference removal deletes current metadata/document nodes **only**. Ledger entries/originals remain indefinitely across Active/Archived/Trash, recovery and upcoming undo/redo, including an explicit restoration of a retained same-note ID/key. Screenshot/Playbook cleanup now checks both Notebook catalogs as well as existing owners. If a recovery checkpoint wins while an import is waiting for its canonical write, compensation retains the staged original and records its ownership even when the note write is cancelled. Unreadable/future/oversized or over-capacity recovery inventories are conservatively treated as potential references; their raw originals are untouched. Ordinary note/image/recovery reads never create retention records or change canonical content.

**Physical cleanup responsibility:** no committed owned-file reclamation occurs here. Later lifecycle/undo work must prove absence from every retained note, durable checkpoint and live undo/redo history before reclaiming ledger/file pairs under the same capture/writer locks. Uncertain staging cleanup requires later verified resolution, not deletion by filename age. Until that ownership/lifecycle work ships, disk retention is intentional; deleting a reference or recovery draft alone cannot release bytes. Trade-owned originals are never deleted by Notebook reference removal.

### Export and archive compatibility

The ledger adds a persisted record set, so logical export advances **16 → 17**, exporting every ledger field and preserving the exact table/column completeness assertions. Document JSON remains **v1**. Complete archive format remains **v1** because the existing managed `screenshots/` paths, manifest entry kind, size/hash and capture/restore machinery already represent these files. Inventory now includes **both current owned Notebook images and the retained ledger**, across all lifecycle states and recovery-only copies. Missing required files or metadata/hash mismatches fail capture/preflight instead of publishing an apparently complete archive. Offline restore includes those originals using the existing atomic switch/recovery path. Logical JSON/CSV still contain metadata, not image bytes, and are not restore inputs.

Existing metadata-only portability fixtures now supply exact owned originals; their exact content/recovery assertions remain intact. Migration, model/column/index inventories and export-version assertions include the additive table and unique key. The installed schema now has **28 migrations**. Older-schema archives remain rejected by exact-schema preflight; a live additive migration is not an offline archive-upgrade mechanism. **Full integrated M22.11 acceptance remains pending**, including the later image UI/lifecycle/duplication workflows, reclamation, broader fault scenarios and any separately designed old-schema archive upgrade path.

### Focused verification and limitations

Final affected Release builds passed with **0 warnings/errors**: Infrastructure/test project **8.12 s**, Desktop/test project **8.88 s**, Application/test project **2.97 s**. Final build logs are `TestResults/m228a-infrastructure-build-complete.log`, `TestResults/m228a-desktop-build-complete.log` and `TestResults/m228a-application-build-ready.log`; earlier build logs are also retained. EF reports **no pending model changes** through the existing in-memory design-time factory.

Durable final evidence is under **`TestResults/m228a-verified/`**, including **`summary.json`** with actual counters, durations, source inventory and binary/source hashes. Final focused runs all exited **0**, with zero failures/skips:

| Final focused run | Passed | Failed / skipped | Reported test duration | TRX |
| --- | ---: | ---: | --- | --- |
| Infrastructure storage/Notebook/schema/export/archive/restore compatibility | 506 | 0 / 0 | 2m39s | `infrastructure-focused-complete.trx` |
| Desktop admission/editor/autosave/SQLite coordination | 49 | 0 / 0 | 11s | `admission-and-editor-complete.trx` |
| Application Notebook contracts | 13 | 0 / 0 | 124 ms | `application-focused.trx` |

This includes **27 new image/storage/fault/retention cases, 4 new archive/ownership cases, 7 actual Windows decoder/import cases, and 2 portable header-policy cases**, alongside unchanged editor/recovery content assertions and focused existing storage, schema, export, backup, preflight, offline restore and compatibility suites. The older/intermediate runs below overlap these totals; they are not additional cases. Fixtures use isolated temporary paths/databases; deterministic interceptors and operation boundaries replace timing sleeps.

Earlier artifacts are retained: `TestResults/m228a/images-initial.trx` passed **16/16**; `images-and-archives.trx` recorded **19 passed / 1 failed**, exposing a paths-DI registration omission; `admission-and-editor-coordination.trx` recorded **40 passed / 8 failed**, seven from that same DI issue and one from an uncaught WPF `FileFormatException`. The factory registration and exception mapping were corrected. The first broader Infrastructure run at `TestResults/m228a-verified/infrastructure-focused.trx` recorded **504 passed / 1 failed**, zero skips: migration test `InitialMigrationTests.LatestMigrationsCreateOnlyTheApprovedSchema` still expected 18 unique indexes, now 19 with `NotebookOwnedImageFiles.StorageKey`. The exact inventory now includes and asserts that new index; no assertion/deadline/concurrency rule was relaxed.

The corrected migration-inventory run passed **505/505**, retained as `infrastructure-focused-final.trx`. Final API review then corrected low-level image-command outcome mapping (UnsupportedContent for an unavailable decoder, StorageFailure for unreadable storage) and made null/invalid-version import requests explicit Invalid results. One added direct-admission regression proves those boundaries; the rebuilt final source passed **506/506** Infrastructure and **49/49** Desktop in the `*-complete.trx` runs above. `summary-before-api-review.json` retains the earlier source/binary hashes; `summary.json` identifies the final tested source.

Final tracked/new-file whitespace checks pass. README/Notebook local Markdown target-existence checks passed **166 targets / zero missing**, evidence `TestResults/m228a-documentation-links.json`; the retained local checker is `TestResults/m228a-check-doc-links.ps1`. External URLs and fragment/anchor validation are outside that target-existence check. Only documentation/evidence changed after final runtime verification, so passing runtime suites were not repeated for those edits.

**M22.8A focused implementation/verification is complete; M22.8 overall acceptance remains pending.** No complete Release gate was launched in M22.8A. Desktop insertion, inline rendering/thumbnails, clipboard admission flows, caret/undo integration and image-reference-body editing are M22.8B work; image-reference bodies remain read-only in the existing editor. Complete integrated verification belongs to M22.8C. Native-process restart, human interaction/Narrator, matching CI and actual installed-codec WebP success on this machine are unverified. The decoder implementation supports WebP only when the installed codec actually accepts it; PNG/JPEG success and animated/malformed rejection are tested. No M22.8B work or Git publication was performed.

Commands for this focused phase (the Infrastructure filter intentionally includes every modified compatibility-test class):

```powershell
dotnet build tests/PersonalTradingJournal.Infrastructure.Tests -c Release --no-restore -v minimal -flp:logfile=TestResults/m228a-infrastructure-build-complete.log
dotnet build tests/PersonalTradingJournal.Desktop.Tests -c Release --no-restore -v minimal -flp:logfile=TestResults/m228a-desktop-build-complete.log
dotnet build tests/PersonalTradingJournal.Application.Tests -c Release --no-restore -v minimal -flp:logfile=TestResults/m228a-application-build-ready.log
dotnet ef migrations add AddNotebookOwnedImageFiles --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests -c Release --no-build --no-restore --filter 'FullyQualifiedName~Notebook|FullyQualifiedName~Backups|FullyQualifiedName~SqlitePersistenceIntegrityTests|FullyQualifiedName~InitialMigrationTests|FullyQualifiedName~JournalDatabaseInitializerTests|FullyQualifiedName~ProductionPersistenceIntegrationTests|FullyQualifiedName~TradingRulePersistenceTests|FullyQualifiedName~TradingRulePortabilityTests|FullyQualifiedName~PlaybookPersistenceTests|FullyQualifiedName~PlaybookImagePersistenceTests|FullyQualifiedName~DailyTradingPrincipleTests|FullyQualifiedName~SharedTradingPlanTests|FullyQualifiedName~TradingPlanPortabilityTests|FullyQualifiedName~LocalTradeScreenshotFileStorageTests' --logger 'trx;LogFileName=infrastructure-focused-complete.trx' --results-directory TestResults/m228a-verified
dotnet test tests/PersonalTradingJournal.Desktop.Tests -c Release --no-build --no-restore --filter 'FullyQualifiedName~NotebookImageAdmissionTests|FullyQualifiedName~NotebookEditingTests|FullyQualifiedName~NotebookAutosaveTests|FullyQualifiedName~NotebookSqliteWorkspaceTests' --logger 'trx;LogFileName=admission-and-editor-complete.trx' --results-directory TestResults/m228a-verified
dotnet test tests/PersonalTradingJournal.Application.Tests -c Release --no-build --no-restore --filter 'FullyQualifiedName~Notebook' --logger 'trx;LogFileName=application-focused.trx' --results-directory TestResults/m228a-verified
git diff --check
pwsh -NoProfile -File TestResults/m228a-check-doc-links.ps1
```

### Exact M22.8A changed-file inventory

Relative to starting HEAD `e0682d3a898742ff8612857de5a2245267eae041`: **28 modified + 11 added files**, all uncommitted. Generated migration/designer/snapshot changes belong to the single additive ownership-ledger migration. Existing test-file changes preserve the exact schema/export/content assertions while including the new table/index/version. Ignored TestResults artifacts are additional local evidence, not source inventory.

Modified:

- `README.md`
- `docs/notebook.md`
- `src/PersonalTradingJournal.Application/Backups/IPortableExportService.cs`
- `src/PersonalTradingJournal.Application/Notebook/INotebookService.cs`
- `src/PersonalTradingJournal.Desktop/App.xaml.cs`
- `src/PersonalTradingJournal.Infrastructure/Backups/PlaybookImageInventory.cs`
- `src/PersonalTradingJournal.Infrastructure/Backups/PortableExportSchema.cs`
- `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Configurations/NotebookConfiguration.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/JournalDbContext.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/JournalDbContextModelSnapshot.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Records/NotebookRecords.cs`
- `src/PersonalTradingJournal.Infrastructure/Screenshots/RetainedScreenshotFileStorage.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/NotebookPortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/PortableExportTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/SavedPeriodPortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookPersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Initialization/JournalDatabaseInitializerTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integration/ProductionPersistenceIntegrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integrity/SqlitePersistenceIntegrityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Migrations/InitialMigrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookPersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/DailyTradingPrincipleTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/SharedTradingPlanTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/TradingPlanPortabilityTests.cs`

Added:

- `src/PersonalTradingJournal.Application/Notebook/INotebookImages.cs`
- `src/PersonalTradingJournal.Desktop/Screenshots/WpfNotebookImageValidator.cs`
- `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookImageFiles.cs`
- `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookImageService.cs`
- `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.Images.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261020180223_AddNotebookOwnedImageFiles.Designer.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261020180223_AddNotebookOwnedImageFiles.cs`
- `tests/PersonalTradingJournal.Application.Tests/Notebook/NotebookImageContractTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookImageAdmissionTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/NotebookImagePortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookImageTests.cs`

## M22.7 — tags, assignments and combined filters: focused verification

20 October 2026. Resumed the interrupted implementation in **`C:/Users/Yovko/source/repos/Personal-Trading-Journal`**, branch **`develop`**, unchanged HEAD **`4520b3f782453ff5ef1bdeac509b765fac7b7099`**. One agent; no subagents. Inspected ancestor/repository instructions (no applicable `AGENTS.md`), existing changes, this contract and prior TRX. Process inspection confirmed reusable MSBuild workers only, with no active test run before rebuilding. All existing changes were preserved; this phase made no commits, pushes, resets or clean operations.

### Existing implementation and focused corrections

- Desktop tag creation, rename and confirmed global deletion are distinct from assigning/removing a tag on one active note. Identity/version checks and dirty-draft flushes coordinate with autosave without replacing the editor. Global deletion removes assignments across lifecycle states while preserving note content.
- Text over saved title/body, all selected tags and the selected scope combine with AND. Stable cursor paging preserves tag IDs; selected filters resolve independently of the first catalog page in the same SQLite read snapshot. Sidebar counts precede text/tag filters; list totals reflect them. Search has a separate **300 ms** debounce with superseded-read suppression and cancellation/draining. Clear filters retains scope/sort.
- Prior artifacts remain unchanged: `TestResults/m227-desktop/` records **85 passed / 2 failed / zero skipped**; `TestResults/m227-infrastructure/` records **96/96 passed**. The two failures expected paragraph text without its canonical LF. **Both corrections already existed on resumption**, comparing the full expected document projection. `NotebookDocument.Visit` appends `"\n"` after paragraphs/headings, consistent with the original domain projection test and this document's content contract. No production serialization change, trimming, weakened content assertion or timeout adjustment was used.
- Expanded SQLite refresh coverage to selected tags both within and beyond the first 50 catalog entries, retaining another selected filter. The new assertion exposed a deleted tag retained in catalog/pickers from the pre-refresh open-note snapshot. Filter repair now removes authoritatively deleted selected identities from the retained catalog before republishing it. It preserves remaining filters and uses the existing single bounded repair read.
- Expanded Desktop recovery coverage for both external global deletion and removal of one assignment. A stale autosave conflicts, keeps exact title/body in recovery, and cannot restore an assignment after workspace restart. Added deterministic Infrastructure cases proving stale-version rejection after either removal and proving a subsequent explicitly refreshed content save also leaves the assignment removed. Existing concurrent deletion/save coverage remains intact.

The initial added regression run is retained at `TestResults/m227-resumed-regression/selected-tag-regression.trx`: **0 passed / 2 failed**. One failure exposed stale catalog retention; the beyond-first-page case also revealed a test setup omission, fixed by loading the next catalog page before each selection. Both variants pass in the final focused run below.

### Verified focused results

```powershell
dotnet build tests/PersonalTradingJournal.Desktop.Tests -c Release --no-restore -v minimal -flp:logfile=TestResults/m227-resumed-desktop-build-final.log
dotnet build tests/PersonalTradingJournal.Infrastructure.Tests -c Release --no-restore -v minimal -flp:logfile=TestResults/m227-resumed-infrastructure-build.log
dotnet test tests/PersonalTradingJournal.Desktop.Tests -c Release --no-build --no-restore --filter 'FullyQualifiedName~Notebook' --logger 'trx;LogFileName=notebook-desktop.trx' --results-directory TestResults/m227-resumed-focused
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests -c Release --no-build --no-restore --filter 'FullyQualifiedName~Notebook|FullyQualifiedName~PortableExportTests' --logger 'trx;LogFileName=notebook-infrastructure.trx' --results-directory TestResults/m227-resumed-focused
dotnet test tests/PersonalTradingJournal.Domain.Tests -c Release --no-restore --filter 'FullyQualifiedName~NotebookDocumentTests.ProjectionIncludesVisibleTextAndCaptionsNotUrlsOrAlternativeText' --logger 'trx;LogFileName=document-projection-contract.trx' --results-directory TestResults/m227-resumed-focused
git diff --check
```

Both affected Release test-project builds, including their production dependencies, passed with **0 warnings/errors**: Desktop **16.27 s**, Infrastructure **5.25 s**. The domain contract run also rebuilt its test project. Final TRX under `TestResults/m227-resumed-focused/`:

| Focused run | Passed | Failed / skipped | Reported test duration | Evidence |
| --- | ---: | ---: | --- | --- |
| Notebook Desktop, including matching shell navigation cases | 90 | 0 / 0 | 2m01s | `notebook-desktop.trx` |
| Notebook Infrastructure and portable export | 99 | 0 / 0 | 1m09s | `notebook-infrastructure.trx` |
| Exact canonical document projection | 1 | 0 / 0 | 27 ms | `document-projection-contract.trx` |

All commands exited **0**. Desktop includes the existing autosave, editing, workspace, navigation, SQLite restart, tag coordination and isolated compiled-WPF view suites. Coverage retains entered-autosave/newer-typing coordination, returned versions, guarded/failed filter changes, failure recovery, stale search suppression, paging/counts, outside-filter editor retention, cancellation and reactivation. Infrastructure includes codec, query, persistence, migration, recovery, tag workflow and portability/export checks. Isolated child suites can repeat cases internally; these table totals are the outer TRX counts, not sums of nested runs.

Tag WPF renders were regenerated for Light/Dark at 1280 DIP / 96 DPI and 480 DIP / 240 DPI under `tests/PersonalTradingJournal.Desktop.Tests/bin/Release/net10.0-windows/notebook-tag-renders/`. Sampled Light normal and Dark narrow filter renders were inspected; the narrow organization pane uses vertical scrolling. Bound tests assert retained editor identity, accessible labels/live validation and visible button bounds. This is automated render/interaction evidence, not human or Narrator acceptance. Final whitespace checks pass.

### Focused-phase acceptance boundary (historical)

At the focused checkpoint, **final Release acceptance was pending**: the complete gate had deliberately not been launched. The historical M22.6 gate did not accept the changed worktree. The subsequent complete verification below supersedes that automated-verification gap. No EF/schema or image-ownership work was introduced; M22.8 onward remains deferred.

### Complete Release verification — 20 October 2026

This phase resumed on **clean `develop`, `97b0ee82a7622ebd578b150db946d5c661b6c4ef`**, which already commits the implementation and focused corrections described above. The worktree remains `C:/Users/Yovko/source/repos/Personal-Trading-Journal`; no applicable repository/ancestor `AGENTS.md` or duplicate test process was present. The existing tag-view harness was extended only to capture saved-body search combined with both selected tags, the scrolled management controls, and the expanded note assignment picker in all four theme/size cases. Its existing assertions, process/STA deadlines, and concurrency remain intact; additional exact list/filter-count assertions confirm the combined state. No production code changed during this completion.

The entire solution was rebuilt after this capture extension: **0 warnings/errors, 8.06 s**, log `TestResults/m227-complete-release-build-final.log`. EF's existing in-memory design-time factory reports **no pending model changes**, with no production database access. `TestResults/m227-complete-source-manifest.json` records the tested HEAD, Notebook source hashes and Desktop/Infrastructure test-binary SHA-256 values. Full-gate execution uses the established Windows context for synthetic credential/symlink checks and the unchanged `scripts/Test-Release.ps1` partition scheduling.

Commands executed for the final source:

```powershell
dotnet build PersonalTradingJournal.sln -c Release --no-restore -v minimal -flp:logfile=TestResults/m227-complete-release-build-final.log
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
pwsh -NoProfile -File scripts/Test-Release.ps1 -ResultsDirectory TestResults
# After final documentation edits:
git diff --check 4520b3f782453ff5ef1bdeac509b765fac7b7099
pwsh -NoProfile -File TestResults/m227-check-doc-links.ps1
```

The **complete Release gate passed 5,131/5,131, zero failures/skips, exit 0**, once, without retries, removed cases, relaxed assertions/deadlines, or scheduling changes. It ran from **20:40:16.226 to 20:50:03.114 Europe/Kiev**, **9m46.887s**. The wrapper preserved the actual process exit code in `TestResults/m227-complete-release-exit-code.txt`.

| Complete partition / project | Passed | Failed / skipped |
| --- | ---: | ---: |
| Exclusive WPF / Desktop | 88 | 0 / 0 |
| Parallel / Domain | 602 | 0 / 0 |
| Parallel / Application | 1,032 | 0 / 0 |
| Parallel / Infrastructure | 1,512 | 0 / 0 |
| Parallel / Desktop | 1,897 | 0 / 0 |
| Total | 5,131 | 0 / 0 |

Combined Desktop total: **1,985**. The complete gate includes all **15 M22.7 Desktop workflow/visual cases and 8 Infrastructure tag workflow cases**, alongside existing Notebook and repository regressions. These are included in the table, not additional totals. Reported test durations: Infrastructure **4m58s**, parallel Desktop **8m08s**. Calendar native process exited **0** in **72.7175179 s**, leaving **47.2824821 s** against its unchanged **120 s** deadline; process cleanup was confirmed.

Durable gate evidence:

- `TestResults/release-20261020-174016-954f7bd34d35413a860b961ced6ad1e9/summary.json` — complete project/partition totals.
- Same directory: partition TRX, VSTest diagnostics, supervised child results/stdout/stderr/phase and process logs; `m227-verification.json` records exit/totals/timing, verified unchanged source/binary hashes and the 28 render hashes.
- Calendar timing/cleanup: `exclusive-wpf/calendar-native-20261020-174020-7907050c66364b1197b9de627883bc3a/process-supervision.log` beneath that gate directory.

**Visual verification:** the existing compiled-WPF harness generated **28 PNGs**, copied without modification from `tests/PersonalTradingJournal.Desktop.Tests/bin/Release/net10.0-windows/notebook-tag-renders/` into the gate's **`m227-visuals/`** directory. The filename pattern is `{Light|Dark}-{1280-96|480-240}-{state}.png`; dimensions are 1280 × 960 DIP / 96 DPI and 480 × 760 DIP / 240 DPI. All **12** combinations of the four theme/size prefixes and `management`, `combined-filters`, `assignments` states were inspected. Additional retained states are `filters`, `validation`, `assignment-removed`, `cleared`.

Management captures show the scrolled Create/Rename/Delete globally controls and explanatory text; combined-filter captures show `observation` search, both selected tags, one matching saved note and filter help; assignment captures show full chips, remove buttons, the expanded picker and disabled duplicate assignment. Narrow views wrap buttons/text and expose lower content by vertical scrolling. No blocking layout defect was found. **Minor visual limitation:** long selected names truncate in the compact management/assignment combo boxes; full names remain readable in chips/filter labels. Render fixtures use synthetic stub catalog counts; actual count correctness is covered by SQLite query/workflow tests. These captures and bound tests do not establish human keyboard/mouse/Narrator acceptance.

Final whitespace checks cover the full M22.7 slice, including the capture extension and completion documentation. Local Markdown target-existence checks passed **165 targets / zero missing**, for README and this document; evidence `TestResults/m227-documentation-links.json`, reproducible with the retained local `TestResults/m227-check-doc-links.ps1`. External URLs and fragment/anchor validation are outside that target-existence check. Only documentation changed after the passing runtime gate, so it was not repeated.

**M22.7 local automated Release verification is complete.** Human interaction, actual native-process restart and matching-source CI remain **unverified**. The local gate used **586.887 s**, approximately **13.113 s** below CI's configured **600 s** test-step timeout before wrapper/runner overhead; hosted timing margin is not assured. No production/provider access, publication, new EF/schema work or M22.8 implementation was performed. Notebook image ownership/backup and linked-target workflows remain deferred to their planned slices.

### Exact final M22.7 changed-file inventory

Relative to starting HEAD **`4520b3f782453ff5ef1bdeac509b765fac7b7099`**, M22.7 contains **8 modified + 4 added files**. The implementation is committed in `97b0ee8…`; this completion additionally edits only `README.md`, `docs/notebook.md`, and the existing `NotebookTagViewTests.cs` capture harness, leaving them uncommitted. Durable local test/log/render artifacts are ignored evidence, not additional source files.

Modified:

- `README.md`
- `docs/notebook.md`
- `src/PersonalTradingJournal.Application/Notebook/NotebookQueries.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml`
- `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.Queries.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookSqliteWorkspaceTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookWorkspaceTests.cs`

Added:

- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Tags.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookTagTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookTagViewTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookTagWorkflowTests.cs`

## M22.6 — autosave, concurrency and durable recovery

20 October 2026. Starting baseline: **clean `develop`, `0a89d26ff036b12561160e6bb4c569954a5a28c3`**, preserving committed M22.1–M22.5. No applicable repository/ancestor `AGENTS.md` was found. Existing SaveNotebookDraft, semantic draft generations, shell close/navigation consent, managed application paths, short-lived SQLite context/transaction ownership, portable-export completeness and controlled-time conventions were inspected.

### Autosave and operation ownership

- A meaningful title/document edit replaces the **750 ms** quiet-period timer, using injectable `TimeProvider`. Loading, preview rendering and selecting do not save. Undo to the persisted semantic value cancels unnecessary canonical work; an obsolete checkpoint can still be cleaned up.
- One tracked pipeline serializes checkpoint, existing `SaveNotebookDraft`, returned-version acknowledgement and checkpoint cleanup. It is not a second canonical write path. Ctrl+S/Save cancels the pending timer and requests immediate saving; its UI remains disabled during an entered write. Captured identity, expected version, draft generation and activation owner prevent stale acknowledgement. Cancellation of a replaced timer does not cancel a write already entered.
- Typing during saving keeps the live editor and newer generation. A successful older save advances the persisted baseline/version but cannot mark newer input Saved. Remaining dirty content receives another quiet interval without overlapping writes. Pin and move flush first and then use the returned version; existing destination capture and guards are retained.
- Any failed/invalid/conflicting save pauses automatic canonical attempts. Further meaningful edits can checkpoint the latest draft, but do not retry a failing canonical write. Use Save explicitly to retry. Stale-version conflicts never fetch a new version to overwrite it. There is no periodic retry, popup loop, merge or duplicate-note workaround.
- The existing editor status distinguishes Unsaved, Saving, Saved, failure/retry and conflict. Checkpoint success explicitly says **not yet saved as a note**. Only canonical success/readback produces Saved. Background acknowledgement does not recreate the FlowDocument or reset selection/caret/scroll/undo.

### Checkpoint format, limits and portability

The earlier architecture paragraph promised only canonical-save durability. This slice explicitly strengthens that guarantee with the requested durable dirty-draft checkpoints; it does not promise durability for every keystroke.

`INotebookRecoveryStore` is the application port; `NotebookRecoveryStore` is a DI-wired infrastructure adapter. Additive migration **`20261020130020_AddNotebookRecovery`** adds `NotebookRecoveryDrafts` inside `IApplicationPaths.DatabasePath`: `journal.db` in the managed data directory (normally `%LocalAppData%/PersonalTradingJournal`, overridden for isolated tests). No Desktop control performs filesystem persistence.

The **envelope version is 1**: editing-session/checkpoint ID, original Note ID, expected canonical version, monotonic checkpoint revision, title, canonical document JSON v1 and UTC checkpoint time. Empty/temporarily invalid titles are recoverable within the unchanged 200-character bound. Document limits remain **1 MiB serialized UTF-8**, 200,000 plain-text characters, 4,096 nodes/depth 16/list depth 3. Invalid editor structures cannot be checkpointed; their last successful checkpoint remains intact and the editor reports the limitation.

At most **128 checkpoints** are retained, discovered in deterministic ID order through **50-row pages**. Capacity failure is explicit; no eviction or silent truncation. Each writer owns an immediate SQLite transaction and short-lived context/connection. Updates require the exact previous checkpoint revision and stable note identity, with no backwards expected-version change. Failed/cancelled writes roll back. Cleanup uses the captured checkpoint revision, so an old acknowledgement cannot delete a newer checkpoint. Updating an ID after its removal conflicts instead of recreating it. No note FK/cascade is used: deleting the source cannot erase unresolved work.

Checkpoint and canonical transactions are intentionally separate. A process can stop between canonical commit and cleanup. Recovery compares semantic title/document content with the current note, recognizes an already-persisted copy and cleans it without another canonical write. A cleanup failure does not deny a confirmed canonical save.

Logical export advances **v15 → v16**, explicitly exporting every checkpoint field alongside all existing records. The unchanged completeness assertion covers the new table. Complete SQLite archives remain format **v1**, including checkpoints; synthetic export, preflight, offline restore, cancellation, rollback and interrupted-restore tests seed valid, malformed and future-envelope recovery copies and compare their raw fields. Credentials remain excluded; note/recovery text is sensitive local content. Earlier logical exports contain no checkpoints and are not restore inputs. **Older-schema archives are still rejected by exact-schema preflight**; the normal additive live-database migration does not make offline old-schema archives compatible. No Notebook image-file restore workflow is added.

### Recovery and departure decisions

Activation/Refresh discovers recovery copies in Organization → Recovery drafts, including a bounded More action. **Recover** reads the original copy and current source before installing a draft. Nothing is automatically overwritten on discovery. A matching active source version allows explicit Save; recovery initially pauses automatic canonical saving. A changed version, missing source, archived/trashed source or incompatible image references remains protected. Missing/non-editable sources show recovered title/body read-only and are never recreated/reactivated. Existing image bodies remain read-only with title-only editing. Unreadable/future/oversized recovery originals remain stored; only explicit confirmed discard removes them.

For conflicts, preserve/copy the text you need, or explicitly discard the pending draft before refreshing the current note. There is no merge engine, automatic adoption of the latest source version, duplication workflow or attachment editing.

Before replacing/closing a dirty draft, the existing guard cancels its pending timer, drains the entered save and attempts a final flush unless automatic attempts are paused. Successful persistence proceeds without a dialog. Otherwise the existing Save / Discard / Cancel choice remains. Cancel keeps the editor. Explicit Discard drains the pipeline and removes the exact durable checkpoint before granting departure consent; failure to remove it refuses departure. The in-memory editor is restored only after the ancestor accepts. A later ancestor veto retains the input, clears consent and schedules a new checkpoint; until that checkpoint succeeds, the retained in-memory edits are not crash-durable. Discard does not undo an already committed autosave.

Actual deactivation/disposal cancels the activation owner before linked operations, drains tracked tasks/timers and prevents late publication. Re-entry waits for prior deactivation work before loading. A returned successful commit can still be acknowledged when cancellation arrived after the commit boundary; cancelled cleanup leaves a comparable recovery copy rather than claiming nothing was written.

**Durability boundary:** only the last successfully committed checkpoint/canonical note survives abrupt termination. Edits inside the debounce, edits made during an earlier save but not yet checkpointed, invalid editor structures, capacity failures and failed checkpoint writes may remain memory-only. Abrupt-process recovery is not a substitute for backups.

### Verification and remaining acceptance

Synthetic data only; controlled clock/timer tests and explicit operation gates, not timing sleeps. Coverage includes debounce replacement/undo, manual flush, newer edits/returned versions, metadata coordination, image restrictions, failure pause/retry, discard/later veto, cancellation/draining, checkpoint CAS/rollback/capacity/paging, restart conflicts, source deletion/read-only sources, future/malformed content, interrupted cleanup and export/restore inclusion.

Commands:

```powershell
dotnet ef migrations add AddNotebookRecovery --project src/PersonalTradingJournal.Infrastructure --configuration Release
dotnet test tests/PersonalTradingJournal.Desktop.Tests -c Release --no-restore --filter "FullyQualifiedName~Notebook" --logger trx --results-directory TestResults/m226-focused-desktop-final
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests -c Release --no-restore --filter "FullyQualifiedName~Notebook|FullyQualifiedName~Portability|FullyQualifiedName~PortableExport|FullyQualifiedName~InitialMigration|FullyQualifiedName~JournalDatabaseInitializer|FullyQualifiedName~ProductionPersistenceIntegration" --logger trx --results-directory TestResults/m226-focused-infra-final
dotnet test tests/PersonalTradingJournal.Desktop.Tests -c Release --no-restore --filter "FullyQualifiedName~NotebookAutosaveTests|FullyQualifiedName~NotebookSqliteWorkspaceTests|FullyQualifiedName~NotebookNavigationTests|FullyQualifiedName~NotebookEditingTests" --logger trx --results-directory TestResults/m226-final-coordination
dotnet build PersonalTradingJournal.sln -c Release --no-restore
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests -c Release --no-build --no-restore --filter "FullyQualifiedName~SqlitePersistenceIntegrityTests|FullyQualifiedName~NotebookRecoveryTests|FullyQualifiedName~NotebookPortabilityTests|FullyQualifiedName~PortableExportTests|FullyQualifiedName~InitialMigrationTests|FullyQualifiedName~JournalDatabaseInitializerTests" --logger trx --results-directory TestResults/m226-inventory-verified
pwsh -File scripts/Test-Release.ps1 -ResultsDirectory TestResults
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
git diff --check
```

Development chronology: initial compilation caught two invalid target-typed generic constructor expressions. The first focused Desktop run was **64 passed / 2 failed**: an accidentally relaxed Save-button enablement rule violated the existing no-duplicate-in-flight assertion. The existing rule was restored; no assertion changed. Initial affected Infrastructure was **107 passed / 2 failed**: the explicit schema lists already included the migration, but two migration-count checks still expected 26 instead of 27; those expectations now include the additive migration.

Subsequent focused runs: **71/71 Notebook Desktop**, **114/114 persistence/portability**, then **38/38 final coordination** cases passed, zero skips. These overlap; they are not additive unique totals. The preceding solution Release build passed with **0 warnings/errors**, 6.50 seconds.

The first complete gate finished with **5,107 passed / 1 failed / zero skipped**, exit 1, at `TestResults/release-20261020-161829-bf9a8e8f1d794dd0a29d185534755e39/summary.json`. The sole failure was `SqlitePersistenceIntegrityTests.ModelContainsExactlyExpectedRecordsWithDomainOwnedIdentifiers` (line 91 in that build): its explicit expected inventory omitted the new `NotebookRecoveryRecord`. Calendar passed in **79.106 s**, leaving **40.894 s**. The one-line inventory addition was already present in the worktree but postdated the tested binary; this failed run is retained, not reclassified as successful.

**Final verification, 20 October 2026:** the actual baseline had advanced to clean `develop`, **`7dbcd657ee48313d582a92952cf12fcc4954f060`**, already containing all 35 M22.6 files and the inventory correction. No test process was active before the rebuild. Inspection confirmed the recovery record is deliberately mapped, has an application-assigned `Id` with `ValueGeneratedNever`, and participates in the unchanged exact-inventory and all-primary-key assertions. Foreign-key/cascade checks remain unchanged; recovery deliberately has no source-note FK so source deletion cannot silently erase unresolved work. No further production/test correction was necessary.

Rebuilt the entire solution from this HEAD: **0 warnings/errors**, **1m01.31s**. The rebuilt-binary affected run above passed **81/81**, zero failures/skips, **43s**; evidence `TestResults/m226-inventory-verified/Yovko_DESKTOP-N2ONT35_2026-10-20_19_51_38_net10.0.trx`. It covers the full integrity class plus checkpoint, migration, initialization, export and portability checks. Infrastructure test DLL SHA-256: `E57BC1C5238416D395CB8AA2CDF3EFC5CEAF135CC449F7269A7C424E59C1B51F`.

The **final complete Release gate passed 5,108/5,108**, zero failures/skips, exit **0**, using those rebuilt binaries and the unchanged repository scheduling/deadlines/assertions. Evidence directory: `TestResults/release-20261020-165252-3c7dae4b44fd491ea3a0867595b81443/`; `summary.json`, partition TRX, VSTest diagnostics and supervised child logs are retained. The formerly failing integrity test also passed in this full run.

| Final partition / project | Passed | Failed / skipped |
| --- | ---: | ---: |
| Exclusive WPF / Desktop | 88 | 0 / 0 |
| Parallel / Domain | 602 | 0 / 0 |
| Parallel / Application | 1,032 | 0 / 0 |
| Parallel / Infrastructure | 1,504 | 0 / 0 |
| Parallel / Desktop | 1,882 | 0 / 0 |

Calendar native process: **69.942 s / 120 s**, **50.058 s headroom**, exit 0 and confirmed cleanup (`exclusive-wpf/calendar-native-20261020-165256-3104b8759f93481a96d1a6140e5d7283/process-supervision.log`). Parallel Infrastructure took **4m32s** and Desktop **7m30s**. These timings describe this local run, not a guarantee against future contention or CI deadlines.

EF reports **no pending model changes**, using the existing in-memory design-time factory. Final whitespace checks cover the full M22.6 slice and completion edits; local documentation target-existence checks pass **163 targets / zero missing** (not anchor validation). Only `README.md` and `docs/notebook.md` changed during this completion, after runtime verification; no additional runtime gate was needed for these documentation-only edits.

Compiled-WPF renders: `tests/PersonalTradingJournal.Desktop.Tests/bin/Release/net10.0-windows/notebook-autosave-renders/{Light|Dark}-{1280-96|480-240}-{recovery-available|recovered|saving|saved|failed|conflict}.png`. Inspected Light/Dark at 1280 × 900 DIP / 96 DPI and 480 × 720 DIP / 240 DPI: recovery actions, wrapped status/help, disabled in-flight actions and retained document are readable. Narrow content/organization use the established panel switcher and vertical scrolling. These are automated renders and bound interactions, **not live human or screen-reader acceptance**.

| Evidence | Status |
| --- | --- |
| Autosave/version/guard/recovery deterministic checks | Passed focused |
| SQLite migration, CAS/rollback, restart, source retention and export/restore | Passed focused |
| Compiled WPF retained document/selection, accessible status and layout | Passed focused; renders inspected |
| Complete repository Release gate | Passed 5,108/5,108; zero failures/skips |
| Human keyboard/mouse/Narrator and native-process restart | Not performed; pending |
| Matching published-source CI | Not checked/claimed for `7dbcd657…`; local passing gate is not CI acceptance |

No production journal, provider, real clipboard or browser access during implementation/verification. This completion made no commits, pushes or merges and leaves its documentation edits uncommitted. **M22.6 implementation and local automated verification are complete; human interaction, native-process restart and matching CI remain separate unverified checks. M22.7–M22.12 were pending at that checkpoint.**

### Exact M22.6 changed-file inventory

The full slice relative to `0a89d26ff036b12561160e6bb4c569954a5a28c3` contains **26 modified + 9 added files** below, now included in `7dbcd657…`. The only additional uncommitted changes from this verification/completion are `README.md` and `docs/notebook.md`, already included in that inventory. No implementation/test files were altered during this completion.

Modified:

- `README.md`
- `docs/notebook.md`
- `src/PersonalTradingJournal.Application/Backups/IPortableExportService.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Editing.cs`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml.cs`
- `src/PersonalTradingJournal.Infrastructure/Backups/PortableExportSchema.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Configurations/NotebookConfiguration.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/JournalDbContext.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/JournalDbContextModelSnapshot.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookSqliteWorkspaceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/NotebookPortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/PortableExportTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/SavedPeriodPortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Initialization/JournalDatabaseInitializerTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integration/ProductionPersistenceIntegrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integrity/SqlitePersistenceIntegrityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Migrations/InitialMigrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookPersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/DailyTradingPrincipleTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/SharedTradingPlanTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/TradingPlanPortabilityTests.cs`

Added:

- `src/PersonalTradingJournal.Application/Notebook/INotebookRecoveryStore.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Autosave.cs`
- `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookRecoveryStore.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Records/NotebookRecoveryRecord.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261020130020_AddNotebookRecovery.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261020130020_AddNotebookRecovery.Designer.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookAutosaveTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookAutosaveViewTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookRecoveryTests.cs`

## M22.5 — rich-text editor and manual Save

20 October 2026. Starting baseline: **clean `develop`, `41f0bfac263f5214e99bcee614f00491d4dbe953`**, preserving committed M22.1–M22.4. No applicable repository/ancestor `AGENTS.md` was found. Existing Journal text controls, Notebook immutable nodes/codec, transactional mutations, theme resources, supervised STA tests and shell close/navigation guards were inspected before implementation.

### Canonical content and editing

- Desktop-only `NotebookEditorAdapter` explicitly maps canonical JSON-v1 nodes to/from WPF paragraphs, headings 1–3, marked runs, line breaks, HTTP(S) hyperlinks, bulleted/numbered lists and checklist items. WPF `Tag` retains document-local identities where an element survives; native splitting/insertion receives unique IDs. Semantic dirty comparison ignores IDs, not content or formatting. No WPF/XAML/RTF payload is persisted or deserialized.
- Native undoable paragraph properties carry heading presentation. Selection-based formatting reports On/Off/Mixed states; title text remains outside body formatting. Native undo/redo is retained across Save. Checklist markers expose readable Checked/Not checked text; click their marker, use Toggle check or Ctrl+Enter. Enter at the end adds an unchecked item; mid-item Enter inserts a line break. Editing a structural marker directly gives a recoverable validation error rather than silently losing its state.
- Images retain exact canonical reference nodes and metadata. This milestone deliberately makes a body containing image references read-only, with a clear explanation; title editing still works. No fabricated image insertion/viewing workflow or lossy placeholder serialization. Archived/trashed and unavailable/unsupported content remain read-only.
- Paste is explicit, Unicode-text only, including line boundaries. Unsupported external formatting is normalized with visible feedback; HTML scripts, XAML, RTF objects, fonts/colors and images are never imported. Admission validates the complete candidate tree and existing codec byte/node/text/depth limits before touching the live selection. Oversized/invalid input leaves the draft intact. Clipboard access occurs only in Paste execution, never command enablement, activation or polling. Clipboard failure produces a recoverable message.
- Links are validated against the existing HTTP(S)-only domain policy before editing. Creation requires text in one paragraph; edit/remove preserve surrounding content. Opening requires the separate Open link action and repeats validation before OS URI launch. No existing general HTTP-link launcher was found to reuse; this small explicit handler does not fetch content. Tests do not launch browsers or inspect the actual clipboard.

### Manual save and draft protection

`SaveNotebookDraft` adds one atomic title+document mutation through the existing application service, Domain validation and SQLite expected-version transaction/readback. Validation happens before mutation; cancellation before commit rolls back. No schema/migration, export version or financial/source semantics changed. Existing image metadata cannot be changed through this command.

The workspace tracks captured note identity/version, saved semantic content, current draft generation and confirmed Save status. Duplicate submissions are disabled. Edits remain possible during saving: only the captured generation is acknowledged; newer input stays dirty. Successful Save does not replace the live FlowDocument, selection/caret or undo stack. The affected row/search membership/counts refresh through bounded reads without reloading the selected document. A saved note outside the current filter/page keeps its editor and explains why the list no longer contains it.

Failures and conflicts keep all draft text; no automatic overwrite/retry. Save / Discard / Cancel protects note/scope/sort/search/Refresh, creation, metadata changes, shell navigation and application close. Pin/move construct their mutation using the post-save version; Move captures its exact requested destination before Save refreshes the bounded catalog, including Unfiled and later-page folders. Departure during an unfinished write is blocked until completion or explicit cancellation. Discard consent is not destructive until the owning navigation/close actually accepts; later veto clears consent and preserves the draft. Disposal cancels/drains tracked operations and rejects late publication. A canceled operation may already have committed; a confirmed returned commit is acknowledged truthfully.

Shortcuts: Ctrl+N creates, Ctrl+F focuses search, Ctrl+S saves; Ctrl+B/I/U formats, Ctrl+Z/Y undoes/redoes; Ctrl+K opens link editing; Ctrl+Enter toggles the current checklist item. All toolbar actions remain reachable by keyboard. Autosave timers, crash recovery of unsaved drafts, image operations and linked-target opening are not implemented here.

### Visual and verification evidence

Automated compiled-WPF renders use 1280 × 900 DIP at 96 DPI and 480 × 650 DIP at 240 DPI, in Light/Dark. Files are under `tests/PersonalTradingJournal.Desktop.Tests/bin/Release/net10.0-windows/notebook-editor-renders/`: `{Light|Dark}-{1280-96|480-240}-{long-title|saved|document}.png`. The wide view follows the supplied reference's organization/list/editor composition; separate title, secondary metadata, wrapping toolbar and readable document area are retained within the existing shell. Narrow layouts use the established panel switcher and scrollable content; long content and toolbar remain reachable. Image previews, rich imported formatting and a one-line graphical checkbox treatment are not claimed to match the mockup.

Development checks caught checklist TextRange export prefixes, native heading undo representation, normalized hyperlink selection boundaries and stale test paragraph references after native undo. The adapter now reads explicit marker runs, uses undoable paragraph styling and resolves selected link intersections. Two compile errors in the last toolbar/checklist test extension (bool-returning Undo/Redo method groups and IReadOnlyList.Count) were corrected before execution. Final render review caught a blank initial paragraph-style selector; it now initializes from the current selection. Coordination review added regressions for retaining an Unfiled/later-page Move destination across Save. Failed intermediate checks are retained under `TestResults/m225/`; they are not acceptance evidence. Assertions/deadlines/isolation/concurrency were not weakened.

Commands (synthetic data only):

```powershell
dotnet test tests/PersonalTradingJournal.Desktop.Tests -c Release --no-restore --filter "FullyQualifiedName~Notebook" --logger "trx;LogFileName=editor-verified.trx" --results-directory TestResults/m225
dotnet test tests/PersonalTradingJournal.Domain.Tests -c Release --no-build --no-restore --filter "FullyQualifiedName~Notebook" --logger "trx;LogFileName=domain.trx" --results-directory TestResults/m225
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests -c Release --no-build --no-restore --filter "FullyQualifiedName~Notebook|FullyQualifiedName~PortableExportTests" --logger "trx;LogFileName=persistence-export.trx" --results-directory TestResults/m225
dotnet build PersonalTradingJournal.sln -c Release --no-restore
pwsh -File scripts/Test-Release.ps1 -ResultsDirectory TestResults
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
git diff --check
```

Focused Domain **35/35** and Infrastructure Notebook/export **82/82** passed, zero failures/skips; logs `domain.trx` and `persistence-export.trx`. The initial complete gate passed **5,072/5,072**, exit 0, at `TestResults/release-20261020-121301-656044d633ae4e88b561b0df42303fbe/`. Calendar child wall time was **61.370 s / 120 s** (58.630 s headroom). This gate preceded the final style-initialization/Move-destination correction and is not substituted for final-source verification.

Final focused Desktop verification passed **50/50**, zero failures/skips, 42 seconds (`editor-verified.trx`), including both Move destinations and the initial style-state check. Final solution Release build passed with **0 warnings/errors**, 4.58 seconds. EF reports no pending model changes, using the existing in-memory design-time factory. Tracked and all five new files passed whitespace checks; new files additionally used `git diff --no-index --check -- /dev/null <path>`. Documentation target-existence validation checked **162 local links / zero missing** (not anchor validation).

Final **complete Release gate: 5,074 passed, zero failures/skips, exit 0**. Freshly built final binaries, unchanged scheduling/deadlines, outside the restricted sandbox for the existing native/symlink tests. Evidence directory: `TestResults/release-20261020-122506-76b995df1971421982df95e4644726ed/` (summary JSON, per-project TRX, VSTest diagnostics and supervised child output).

| Final gate partition / project | Passed | Failed / skipped |
| --- | ---: | ---: |
| Exclusive WPF / Desktop | 88 | 0 / 0 |
| Parallel / Domain | 602 | 0 / 0 |
| Parallel / Application | 1,032 | 0 / 0 |
| Parallel / Infrastructure | 1,495 | 0 / 0 |
| Parallel / Desktop | 1,857 | 0 / 0 |

Calendar native child: **66.346 seconds**, **53.654 seconds headroom** to the unchanged 120-second deadline, exit 0 and confirmed cleanup. Infrastructure duration 4m37s; parallel Desktop 6m56s. These measurements describe these runs, not a guarantee against future machine contention.

| Acceptance evidence | Status |
| --- | --- |
| Canonical editor mapping, atomic/conflicting saves, guards, lifecycle, cancellation, synthetic restart/export | Passed focused and complete automated gates |
| Bound WPF formatting, mixed selection, checklist/undo, links, paste, retained document/caret and narrow reachability | Passed automated checks |
| Light/Dark normal and narrow/240-DPI long-title, saved and scrolled-document renders | Inspected; initial selector and narrow-scroll issues corrected |
| Human keyboard/mouse, Narrator, real clipboard, explicit browser opening and live restart | Not performed; pending |
| CI matching these changes | Not verified; changes are uncommitted, no matching-source CI acceptance claimed |

Human keyboard/mouse, Narrator, real clipboard/browser launch, live app restart and matching published-source CI remain unverified. Automated WPF interaction and synthetic SQLite restart are not substitutes for those checks.

Final branch/HEAD remain **`develop` / `41f0bfac263f5214e99bcee614f00491d4dbe953`**, with **15 modified + 5 new files** below. No production journal, provider, credentials, real clipboard or browser was accessed; no commit/push/merge. **M22.6–M22.12 remain pending.**

### Exact M22.5 changed-file inventory

Modified:

- `README.md`
- `docs/notebook.md`
- `src/PersonalTradingJournal.Application/Notebook/NotebookCommands.cs`
- `src/PersonalTradingJournal.Domain/Notebook/NotebookNote.cs`
- `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.cs`
- `src/PersonalTradingJournal.Desktop/App.xaml.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/MainWindowViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml.cs`
- `tests/PersonalTradingJournal.Domain.Tests/Notebook/NotebookNoteTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookPersistenceTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Navigation/NotebookNavigationTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookWorkspaceTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookSqliteWorkspaceTests.cs`

Added:

- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Editing.cs`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookEditorAdapter.cs`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookLeaveInteraction.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookEditingTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookEditorViewTests.cs`

## M22.4 — workspace and navigation

20 October 2026. Actual starting baseline: **clean `develop`, `d974177bdaca3743210c335af8bac329d88ae53d`**. M22.1–M22.3A, including export v15, were already committed and remain preserved. No applicable `AGENTS.md` was found in the repository or checked ancestor chain.

### Workspace and operation ownership

The existing Notebook destination now resolves a DI-backed `NotebookWorkspaceViewModel` and its compiled view through the existing shell/DataTemplate mechanism. The shared centered section header, application sidebar and theme selection are unchanged. The visual reference guides the three adjacent bordered surfaces, teal selection accent, clear title hierarchy and dominant reading panel; its sample content is not seeded.

- Organization shows All Notes, Pinned, Unfiled, named folders, Archived and Trash with scope counts. Existing tag names/counts are informational, not pretend filter controls. Catalogs start with the service's bounded 50-item pages; More folders/More tag names expose continuations.
- Notes show title, short saved projection, New York updated time, pin state and tag labels. Recently updated and Title sorting retain repository tie-breaking. Search is explicit through Search/Enter (Ctrl+F focuses its input); Refresh/scope/sort changes use the current query. Full debounced/tag-filter UX remains M22.7. List totals use the filtered population; sidebar counts exclude search.
- Only list metadata is loaded for rows. Each 50-row page exposes Load more until its cursor ends. Selection is by note identity; refresh preserves it when present in the refreshed page, otherwise clears it predictably. A failed scope request retains the last applied list heading/counts and prevents wrong-scope selection until a successful refresh. Moving a selected note outside the current scope removes it from that list; it remains reachable in its destination.
- New note/Ctrl+N creates one verified **Untitled**, Active, unpinned, empty note in the selected folder or Unfiled. It clears search, chooses Recently updated and an appropriate active scope, refreshes counts/list and selects that note. Concurrent submissions are blocked.
- Folder management provides name entry, Create, Rename, confirmed Delete and explicit Clear. Names and expected versions come from real catalog snapshots, including later catalog pages. Deletion uses existing transactional Unfiled behavior across lifecycle/restoration references; it never deletes notes. Failed/conflicting operations retain entered names and explain refresh/retry.
- Active note pin/unpin and Move use the loaded note's exact ID/version. Archived/Trashed and unavailable content remain read-only. No archive/trash/restore/duplicate/permanent-delete/image operations were newly exposed.
- Selection projects the existing validated document tree into a `FlowDocument`: headings, paragraphs, marked runs, line breaks, lists and recorded checklist state. A read-only RichTextBox supports text selection, keyboard scrolling and a text automation pattern. URLs are informational text, not auto-opened; images show available caption/alt text and an honest unavailable-preview label. No arbitrary XAML/parser, network/file loading, body mutation or simulated save feedback.
- Existing Trade/Trading Setup link labels resolve in one bounded batch, retain explicit unavailable states and occupy a scroll-bounded secondary area. There is no fabricated Strategy/Playbook identity or nonfunctional navigation link. Unreadable/unsupported/oversized content keeps list metadata visible and original storage untouched; no blank editable replacement or raw JSON is shown.

List/detail/link reads share a cancellation owner; supersession cancels the prior operation and prevents late publication even if a service ignores cancellation. Tracked completion tasks drain cancellation/departure work. SQLite service work runs off the UI dispatcher; UI continuations publish on the captured context. Repeated activation does not duplicate loads/subscriptions. Writes are exclusive, carry captured identity/version, and block destination/window departure until completion or explicit cancellation. Cancellation messages do not promise rollback of an already committed change. Folder-name input survives navigation; this slice has no unsaved editable body. Disposal cancels tracked work and suppresses late publication; deterministic tests drain its exposed completion task.

### Responsive and visual verification

At **1,000 DIP of page width or more**, the organization/list columns are 210/290 DIP and the remaining width belongs to content. Below that breakpoint, reachable Organization / Notes / Content buttons switch the same panels; note selection opens Content. This avoids squeezing three unreadable columns into the shell's minimum window size. The toolbar wraps; note list, organization, preview and long linked metadata have bounded scrolling. Folder management is secondary and expandable. Shared theme-based input/action styles and visible selected/focus borders are used locally.

Automated renders are written under:

`tests/PersonalTradingJournal.Desktop.Tests/bin/Release/net10.0-windows/notebook-renders/`

The four combinations are **Light/Dark at 1280 DIP / 96 DPI and 480 DIP / 240 DPI**. Filenames use `<Theme>-<Width>-<Dpi>-<state>.png`; states include `content`, `unavailable`, `error`, `loading`, `empty`, plus narrow `organization` and `notes`. These are synthetic bound-WPF renders, not real application input or Narrator acceptance.

Initial inspection exposed fallback Windows styling on several controls; local styles now reuse the actual shared theme resources. Empty feedback spacing was removed, and the Dark folder-management heading received explicit themed foreground. Wide composition and narrow panel reachability were inspected against the supplied mockup. Formatting toolbar, editable body/checklists, chart images, autosave indicator and target-opening chips are intentionally absent rather than simulated. No complete visual parity with those later capabilities is claimed.

### Verification chronology and limits

The first implementation build caught target-typed construction of command interfaces; concrete Toolkit command types corrected compilation. Initial ViewModel checks passed **17/17**. An expanded compile caught the repository's xUnit collection-assertion analyzer; the assertion was expressed as `DoesNotContain` without changing its meaning. The first expanded run passed **23/23**, then additional paging/selection regressions passed **26/26**. The final focused run, including failed-scope recovery, passed **27/27**, zero failures/skips, 33 seconds (`TestResults/m224/notebook-final.trx`). No existing assertion, deadline, isolation or concurrency setting was weakened.

```powershell
dotnet test tests/PersonalTradingJournal.Desktop.Tests -c Release --no-restore --filter "FullyQualifiedName~Notebook" --logger "trx;LogFileName=notebook-final.trx" --results-directory TestResults/m224
dotnet build PersonalTradingJournal.sln -c Release --no-restore
pwsh -File scripts/Test-Release.ps1 -ResultsDirectory TestResults
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
git diff --check
```

Final solution Release build: **zero warnings/errors**, 4.90 seconds. EF: **no pending model changes**, using the in-memory design-time factory. Tracked and all eight new files passed whitespace checks (new files checked with `git diff --no-index --check -- /dev/null <path>`). Documentation target-existence checks found **161 local links, zero missing** across README and this document; this is not anchor validation.

The **complete Release gate passed: 5,049 tests, zero failures/skips, exit 0**. Results are under `TestResults/release-20261020-105354-ff90cd57b00e4720aa1a6ac97c2d7e79/`, including top-level per-project TRX, stage output, diagnostics and supervised child logs. This was one complete gate run against freshly built final code, outside the restricted sandbox for the existing native/symlink tests; scheduling and deadlines were unchanged.

| Gate partition/project | Passed | Failed / skipped |
| --- | ---: | ---: |
| Exclusive Desktop WPF | 88 | 0 / 0 |
| Parallel Desktop | 1,834 | 0 / 0 |
| Domain | 601 | 0 / 0 |
| Application | 1,032 | 0 / 0 |
| Infrastructure | 1,494 | 0 / 0 |

Parallel Desktop took 7m57s and Infrastructure 5m45s. Calendar's native child exited successfully in **92.361 seconds**, leaving **27.639 seconds** against its unchanged 120-second deadline, measured from `process-supervision.log` start/exit timestamps. This is this run's headroom, not a guarantee against future machine-load variability.

| Acceptance evidence | Status |
| --- | --- |
| Workspace, navigation, owned cancellation, mutation recovery, synthetic SQLite restart | Passed focused and complete automated checks |
| Compiled Light/Dark, normal/narrow 240-DPI layout and accessibility properties | Passed automated checks; representative content, unavailable and narrow panel renders inspected |
| Human keyboard/Narrator, native window/DPI interaction | Not performed; still pending |
| CI for these exact changes | Unverified; working-tree changes are uncommitted |

No production journal, provider, credentials, clipboard or Notebook image-file workflow was accessed. Final branch/HEAD remain `develop` / `d974177bdaca3743210c335af8bac329d88ae53d`, with **6 modified and 8 new files**, no commit/push/merge. M22.5–M22.12 remain pending; export/schema/backup contracts and Deferred outcomes are unchanged.

### Exact M22.4 changed-file inventory

Modified:

1. `README.md`
2. `docs/notebook.md`
3. `src/PersonalTradingJournal.Desktop/App.xaml`
4. `src/PersonalTradingJournal.Desktop/App.xaml.cs`
5. `src/PersonalTradingJournal.Desktop/ViewModels/MainWindowViewModel.cs`
6. `tests/PersonalTradingJournal.Desktop.Tests/Navigation/MainWindowViewModelTests.cs`

New:

7. `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.cs`
8. `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookDocumentPreview.cs`
9. `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml`
10. `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml.cs`
11. `tests/PersonalTradingJournal.Desktop.Tests/Navigation/NotebookNavigationTests.cs`
12. `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookWorkspaceTests.cs`
13. `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookSqliteWorkspaceTests.cs`
14. `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookViewTests.cs`

## M22.3A — export completeness and database compatibility

20 October 2026. Starting baseline: **clean `develop`, `1402f6a3909d3fef479fccd9297decc9047acf3c`**. Existing M22.1–M22.3 work is preserved. No repository/ancestor `AGENTS.md` was found. This correction adds no migration, Desktop workspace, image operation, duplication, permanent deletion or AI integration.

### Failure, correction and version boundaries

`PortableExportTests.AllLogicalColumnsAreExplicitlyCoveredExceptRebuildableBrowseCache` failed at its table-count assertion: **35 persisted logical tables versus 29 explicit export tables**. The existing assertion then checks every persisted column, excluding only the rebuildable Trade browse cache. It remains unchanged, with no Notebook exemption.

**v14 was the portable logical JSON/CSV contract**, not a SQLite schema or restore version. `PortableExportService` reads an immutable SQLite snapshot through `PortableExportSchema`'s explicit allowlist, streams deterministic JSON and four CSVs, and publishes a directory with a versioned, hashed row/file inventory. Merely migrating SQLite did not add Notebook to that allowlist.

The correction advances logical export to **v15** and adds JSON arrays `notebookFolders`, `notebookTags`, `notebookNotes`, `notebookTagAssignments`, `notebookLinks` and `notebookImages`, with every persisted scalar column. Identity, UTC audit values, versions, normalized keys, plain/search text, folder membership, tag assignments, pins, lifecycle/restoration metadata, Trade/Trading Setup links and image ownership/reference metadata are retained. Ordering is explicit by stable IDs, composite reference keys and image position. Folders/tags precede notes and Notebook child arrays; references retain exact IDs, not display names.

`documentJson` is exported as **opaque original text**, not decoded/reformatted by the Notebook codec. Malformed and future-version payloads, including whitespace/Unicode, retain their exact string values after JSON decoding. Existing exporter field/output limits, snapshot consistency, cancellation/atomic publication, sanitization, credentials exclusion and CSV behavior are unchanged. Seven existing version assertions advance from 14 to 15 because they read a newly generated whole-database export; their existing payload checks are preserved.

| Boundary | Supported behavior |
| --- | --- |
| SQLite schema | Unchanged here: 26 migrations, latest `20261020085611_AddNotebook`; normal installed-database upgrade is additive. |
| Logical export v15 | All 35 logical tables and columns, including Notebook text/metadata. Four existing CSVs unchanged; Notebook is JSON-only. Unencrypted, no image bytes or credentials. |
| Logical exports v14 and earlier | Keep their original meaning and lack Notebook data. No upgrade can reconstruct omitted notes. Neither old nor new JSON/CSV has a supported import/restore path. |
| Complete archive v1, current schema | Existing whole-database capture and offline replacement already retain all six tables. No restore algorithm/version change is necessary. Synthetic round trips verify metadata and existing screenshot guarantees. |
| Pre-Notebook schema archive | Recognized as requiring staged migration and rejected before replacing installed data. The separate older-archive upgrade path remains deferred. |
| Future schema or archive version | Rejected before replacement; existing target Notebook/data remain unchanged. |
| Future/unreadable Notebook content inside current schema | Retained byte-for-byte as stored text through export/archive/restore. A fresh service still reports UnsupportedContent/InvalidContent, not an empty editable note. |

Restore means the established **`.ptjbackup` workflow**, not importing `journal.json`. Archive preflight checks database integrity, FKs and exact schema; it does not rewrite or claim to semantically validate every Notebook document. Notebook-owned image bytes are **not yet captured/restored**. Tests intentionally seed ownership metadata without an owned image file; metadata recovery is not full image backup. Existing Trade screenshot bytes remain covered by their unchanged inventory. M22.8/M22.11 must integrate Notebook-owned file inventory, retention, staging/hash reconciliation, compensation and complete image round trips before claiming full Notebook portability.

### Regression evidence and verification

New synthetic tests use the existing archive/export fixtures and real SQLite. They compare every Notebook scalar to EF records, verify repeat-export ordering, and compare all 35 logical tables before/after offline restore. Fixtures include Active/Archived/Trashed notes, both restoration origins, folders/tags/pins, version increments, Trade/Setup links, both image ownership kinds, rich content, malformed/future payloads, existing Accounts/Instruments/Trades/executions/Journal and screenshot bytes. Restart uses a fresh DI provider and normal initialization.

Fault cases include cancellation before switch, failure after incoming installation, interrupted-switch recovery, and pre-Notebook/future-schema/future-archive rejection against a populated target. No ignored cleanup failures, retries, skips, relaxed assertions or deadline/concurrency changes.

Initial test-authoring corrections: compilation caught an incorrectly named preflight result property; the first executed focused run had **184 passed / 4 failed / zero skipped** (`TestResults/m223a/focused.trx`). All four new restart cases used the old archive fixture's execution-free Trade, which normal startup correctly rejected. Adding a valid synthetic execution to the new fixture fixes its stronger restart precondition; no production startup/restore behavior was changed.

Final commands (fresh Release binaries, synthetic databases only):

```powershell
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests -c Release --filter "FullyQualifiedName~Notebook|FullyQualifiedName~PortableExportTests|FullyQualifiedName~JournalRestoreTests|FullyQualifiedName~RestorePreflightTests" --logger "trx;LogFileName=focused-final.trx" --results-directory TestResults/m223a
dotnet build PersonalTradingJournal.sln -c Release --no-restore
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests -c Release --no-build --no-restore --logger "trx;LogFileName=infrastructure-all.trx" --results-directory TestResults/m223a
dotnet test tests/PersonalTradingJournal.Application.Tests -c Release --no-build --no-restore --logger "trx;LogFileName=application-all.trx" --results-directory TestResults/m223a
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
git diff --check
git diff --no-index --check -- /dev/null tests/PersonalTradingJournal.Infrastructure.Tests/Backups/NotebookPortabilityTests.cs
```

- Focused Notebook/export/preflight/restore: **188 passed, zero failures/skips**, 1m50s, `focused-final.trx`. Includes eight new portability cases and the unchanged completeness assertion.
- **Complete Infrastructure Release: 1,494 passed, zero failures/skips, exit 0**, 3m02s, `infrastructure-all.trx`. Run outside the restricted sandbox to provide the Windows symlink privilege needed by the existing backup-security test; no filters, test-setting changes or retries. This supersedes M22.3's one-failure Infrastructure result.
- Complete Application Release: **1,032 passed, zero failures/skips**, 2s, `application-all.trx`.
- Solution Release build: **0 warnings/errors**, 29.87s. EF reports **no pending model changes**, using the in-memory design-time factory.
- Tracked/new-file whitespace passed; PowerShell target-existence validation checked **172 local links, zero missing** across the three changed documents (not anchor validation).
- Final tree: **12 modified + 1 new file**, unchanged branch/HEAD, no commit/push/merge. The exact inventory follows.

The requested **M22.3A local checks are green**. The full multi-project Desktop Release gate, human interaction and matching corrected-source CI were not run or claimed for this bounded database correction. No production/provider access occurred. No full Notebook backup claim is made while owned image bytes remain deferred. **M22.4 onward remains pending**; M22.11 owns remaining integrated file portability and M22.12 full feature acceptance.

### Exact M22.3A changed-file inventory

Modified:

1. `README.md`
2. `docs/notebook.md`
3. `docs/backup-restore.md`
4. `src/PersonalTradingJournal.Application/Backups/IPortableExportService.cs`
5. `src/PersonalTradingJournal.Infrastructure/Backups/PortableExportSchema.cs`
6. `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/PortableExportTests.cs`
7. `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/SavedPeriodPortabilityTests.cs`
8. `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePortabilityTests.cs`
9. `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookPersistenceTests.cs`
10. `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/DailyTradingPrincipleTests.cs`
11. `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/SharedTradingPlanTests.cs`
12. `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/TradingPlanPortabilityTests.cs`

New:

13. `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/NotebookPortabilityTests.cs`

## M22.1 — scope, UX contract and repository review

Design checkpoint, 20 October 2026. Inspected baseline: clean `develop`, `a279aaf28845e614b8ca94a2e533aaa52f45957c`. This document specifies subsequent implementation; it does **not** describe an already functional Notebook. Deliver one submilestone at a time. No entities, migrations, editor, seeded notes or application behavior change in M22.1.

The supplied *Trading journal notebook workspace.png* is visual direction for organization, note selection and a wide editor. Reuse the actual PTJ shell, centered shared section heading, navigation, dynamic theme resources and focus conventions; do not replace them with the mockup's branding/sidebar. Sample text, charts, counts and timestamps are illustrative and must never be seeded into a user's database.

### Ownership and agreed link scope

| Module | Owns |
| --- | --- |
| Journal | Date-associated observations and reflection |
| Trading Plan | Session preparation, intentions and scenarios |
| Trading Setup / Playbook | Existing pattern classification and its guidance |
| Notebook | Flexible knowledge, research and lessons spanning sessions |

Notebook is global, not Account/date-scoped. A link does not change target data, classify a Trade, prove adherence or supply automatic AI evidence.

**User clarification:** M22.9 is **Linked Trades & Trading Setups**. Separate Strategy support is deferred. The current model has no independent Playbook catalog identity: `CurrentPlaybookRecord` is owned by `TradingSetupId`; retained Playbook revisions use `(TradingSetupId, Revision)`. Link the underlying Trading Setup, labelled **Trading Setup**, not an invented Playbook ID. No Strategy catalog, Strategy-specific persistence, placeholder Strategy target kind or duplicate Setup catalog. Typed links can be extended by a future version when real target identities exist.

Initial scope: formatted notes, flat folders, tags, pins, search, managed images, these links, built-in templates, duplication, archive, trash, autosave and complete backup/restore. Deferred: nested folders, arbitrary attachments, custom templates, collaboration, sync, OCR, semantic search and AI features. Notebook must work offline and never enter review prompts automatically.

### Repository review and reuse map

Paths below are relative to `src/`; inspect their then-current implementations again at the relevant implementation slice.

| Existing implementation | Reuse / necessary extension |
| --- | --- |
| `PersonalTradingJournal.Desktop/Navigation/NavigationDestination.cs`, `ViewModels/MainWindowViewModel.cs` | Notebook destination/icon already exist, but fall through to `PlaceholderViewModel`. Replace that content mapping, not navigation taxonomy; integrate activation, departure and close guards. |
| `PersonalTradingJournal.Desktop/MainWindow.xaml`, `Resources/Controls.xaml`, `Typography.xaml`, `Spacing.xaml`, `Themes/LightTheme.xaml`, `Themes/DarkTheme.xaml` | Shared heading, surfaces, text, actions and focus treatments. Local Notebook templates may compose them; keep both themes and live theme switching. |
| `PersonalTradingJournal.Desktop/Views/Journals/InlineJournalView.xaml` | Existing journal fields are plain multiline TextBoxes, not a reusable rich-text editor. Searches of application source found no RichTextBox/FlowDocument editor. A bounded document adapter is new work, not an extension to Journal storage. |
| `PersonalTradingJournal.Desktop/ViewModels/TradingPlans/TradingPlanWorkspaceViewModel.cs` | Reuse owned asynchronous lifetimes and guarded navigation patterns. Cancel the owner before dependent cancellation can release continuations; drain tracked operations. Do not copy an unrelated Plan state machine. |
| `PersonalTradingJournal.Infrastructure/Persistence/JournalDbContext.cs`, `TradingPlans/DailyTradingPrincipleStore.cs` | Existing EF records/configurations, factory-created short-lived contexts, SQLite transactions, expected-version outcomes and bounded reads. Add Notebook mappings/use cases, not another database or long-lived editor DbContext. |
| `PersonalTradingJournal.Application/Common/Storage/IApplicationPaths.cs`, `PersonalTradingJournal.Infrastructure/Storage/LocalApplicationPaths.cs` | Managed database, screenshots, logs and backups paths. Resolve injected paths; tests use isolated roots, never the production default. Credentials remain outside portable data. |
| `PersonalTradingJournal.Infrastructure/Screenshots/LocalTradeScreenshotFileStorage.cs`, `ScreenshotCaptureLease.cs`, `RetainedScreenshotFileStorage.cs` | Safe storage keys, staging and capture/deletion coordination. Existing retention union covers Trade, Playbook and Plan assets, **not Notebook**; extend it before note images can be removed safely. Do not fabricate a Trade to own a note image. |
| `PersonalTradingJournal.Desktop/Screenshots/PlaybookExampleImages.cs` | Bounded byte reading, signatures, single-frame/dimension validation and 384-pixel thumbnails are the relevant reusable image policy. The generic Trade decoder loads full images and is not sufficient for Notebook list/inline preview admission. |
| `PersonalTradingJournal.Infrastructure/Backups/BackupArchiveService.cs`, `PlaybookImageInventory.cs`, `RestorePreflightService.cs`, `JournalRestoreService.cs` | Existing capture lease, immutable SQLite snapshot, file inventory/hash reconciliation, preflight and recovery. Extend image inventory, metadata checks and retention to Notebook, including Archived/Trashed notes. A database table alone does not include its images. |
| `PersonalTradingJournal.Infrastructure/Backups/PortableExportSchema.cs` | Current logical JSON export v14 explicitly enumerates tables. Advance its version when Notebook persistence ships; never silently omit notes/content/metadata. Separate Notebook import/export remains deferred. |

Related conventions: [architecture](architecture.md), [backup and restore](backup-restore.md), [Trading Plan lifetime/guards](trading-plan.md), [WPF verification](ci-wpf-tests.md).

### Workspace, commands and navigation

- Organization: All Notes, Pinned, named flat folders, Unfiled, Archived, Trash and tag filters. Normal views include Active notes only. Archived and Trash are explicit, searchable scopes.
- Note list: title, short plain-text preview, last-updated time, tags and pin indicator; stable selection by note ID. Default Recently Updated descending, then stable ID; alternative normalized Title ascending, then stable ID. No automatic pin-first reorder inside All Notes.
- Editor: separate title, selection-aware formatting toolbar, body, image captions, typed links and save status. Saved metadata is secondary; never expose JSON as the normal editor.
- New note creates a persisted empty **Untitled** note in the selected folder, otherwise Unfiled; active state even when launched from Archived/Trash. Focus title after creation succeeds. Failed creation retains input and cannot claim a persisted identity. Blank titles fail validation; body may be empty.
- Empty workspace explains Notebook and offers Create note. No fabricated research/trades or templates inserted until an explicit user action.
- On wide layouts show three panels; at narrow widths collapse organization and switch between list/editor with explicit Back/Notes controls. Retain selected identity, filters, caret, scroll and unsaved content; reflow alone cannot save or discard.
- Commands: create/load/edit, move, pin/unpin, duplicate, archive/unarchive, trash/restore/permanent delete; folder and tag management; link/unlink/open; add/remove/view image; Retry save and explicit discard. Trash is read-only until restored; Archived notes are readable and can be unarchived to edit.
- Keyboard: native Tab/Shift+Tab, lists and toolbar navigation; scoped Ctrl+N New, Ctrl+F Search and Ctrl+S flush save without stealing editor undo/redo. Accessible labels, selection/expanded state and restrained live save/error announcements. Do not move focus merely to announce Saving/Saved.
- Note switch, linked-target navigation and owner-window closing must first settle dirty content. Respect other destinations' vetoes; do not destroy/discard the editor before all relevant guards accept. Search/filter exclusion of a dirty selected note uses the same guard.

### Content format decision

Use **Notebook document JSON v1**, a bounded typed tree, not XAML, RTF or HTML as persistent truth. Domain/Application contracts contain only data. A Desktop adapter maps allowlisted nodes to WPF presentation/editor objects; those objects never cross persistence boundaries.

Envelope: `format: "ptj.notebook.document"`, integer `version: 1`, ordered `blocks`. Nodes have stable document-local IDs to preserve editing/image identity. Supported blocks:

- Paragraph and heading (levels 1–3), with inline text runs and explicit line breaks.
- Runs with bold/italic/underline marks; hyperlink with displayed runs and an absolute HTTP/HTTPS URL.
- Ordered/unordered lists and checklist items with explicit checked state. List nesting is bounded.
- Image block at its position in the text flow, referring to a note attachment ID plus optional caption/alternative text; no embedded bytes, source filename or arbitrary filesystem path.

No arbitrary runtime type names, scripts, bindings, event handlers, unrestricted XAML loading, external images or automatic URL fetching. Open a validated HTTP/HTTPS link only on explicit activation; no file/data/javascript schemes. Clipboard access occurs only on explicit paste. A restricted parser may retain supported clipboard formatting; unsupported styling is normalized to safe plain text with feedback. Never execute/deserialise clipboard objects.

The WPF implementation should use a restricted RichTextBox/FlowDocument adapter where appropriate, with explicit checklist/image handling. **Control feasibility, undo/redo and accessible interactive checklists still require M22.5 tests**; choosing the portable format does not claim an editor already works. Autosave serializes the current model without rebuilding the editor document.

Persist formatted content and a deterministic plain-text projection in the same transaction with metadata/version. The projection includes visible body text, checklist/list text and image captions, not linked-record contents, OCR or hidden formatting. Search title separately. Never trust a caller-supplied stale projection.

Unknown versions, malformed JSON or invalid nodes yield an unavailable-content state retaining exact original stored bytes/text. No automatic conversion to an empty document, autosave or overwrite. Offer safe metadata/read-only recovery guidance; future repair/export actions must be explicit. Round-trip support and schema upgrades require fixtures before enabling a new format.

### Initial bounds and normalization

These are the M22 implementation contract. M22.2 enforces the domain/query bounds described in its implementation section below; serialized-byte and image-decoding limits still require their later adapters. Reject invalid/oversized changes with retained input and an actionable error; never silently truncate. Counts of text below use .NET UTF-16 code units; reject malformed Unicode.

| Value | Limit / rule |
| --- | --- |
| Title | Required after trimming, 200 characters; default Untitled |
| Folder / tag display name | Required, respectively 128 / 64 characters |
| Name uniqueness | Trim and Unicode Form C; ordinal case-insensitive within folders or tags separately. Preserve normalized display casing; rename collision returns validation, no implicit merge. |
| Document | 1 MiB serialized UTF-8; 200,000 projected body characters; 4,096 total nodes; depth 16, list nesting at most 3 |
| URL | 2,048 characters, allowed absolute schemes above |
| Per-note assignments | 32 distinct tags; 100 distinct typed links; 32 managed images |
| Caption / alternative text | Optional, at most 2,000 / 500 characters |
| Image | Nonempty, at most 8 MiB, PNG/JPEG/WebP with installed decoder support; validated signature and single frame; each dimension at most 8,192, total at most 16,000,000 pixels |
| Image preview | Decode to display size; list thumbnail longest edge at most 384 pixels. Never decode every original in list rows. Full-size opens only on request. |
| Search / page | Search at most 256 characters; default page 50, accepted 1–100; stable cursor with lookahead, never silently truncate a complete result into one page |

Image bounds reuse the established Playbook admission policy, including single-frame WebP only when decoding succeeds. New Notebook images cannot exploit legacy zero-byte Trade screenshot admission. Existing screenshots/data remain unchanged. Stream byte bounds and cancellation apply before decode; bounded concurrent decoding is required. File bytes remain original; clipboard bitmap encoding is a newly created image and must pass the same admission policy.

### Identity, lifecycle and persistence contracts

Stable GUIDs for notes/folders/tags/attachments; typed target IDs for links; UTC audit timestamps from injected TimeProvider. Display positions/names are never identities. One nullable folder reference means Unfiled; duplicate `(NoteId, TagId)` and `(NoteId, TargetKind, TargetId)` are rejected.

Expected note version covers content and metadata mutations. Saves are atomic; stale writes return Conflict and retain local content rather than overwriting a newer version. Success requires confirmed committed state/version, not merely queued work. Folder/tag rename/delete also recheck their expected state transactionally. No per-keystroke revision archive or mandatory immutable note history in M22; the last persisted note is the recovery baseline.

Active, Archived and Trashed are distinct. Pinning is metadata, not a lifecycle state. Trash stores the prior Active/Archived state and folder reference. Restore returns there if available, otherwise Unfiled; pin/tag/link/image content survives. Deleting a folder moves its notes to Unfiled, including archived/trashed references; deleting a tag removes only assignments. Permanent deletion is available only in Trash after explicit confirmation and transactional ownership recheck. No automatic expiry.

Duplicate creates an Active note with independent identity/version/audit, copies content/tags/links, and starts unpinned; it does not mutate the source. Use new attachment identities and independent managed file copies for note-owned images, so deleting either note cannot break the other. Validate and stage all copies before publishing success; compensate failed staging without deleting referenced assets.

### Autosave and operation ownership

Target a 750 ms quiet-period debounce using injectable time, with explicit Ctrl+S/navigation flush. Serialize writes per note; each operation captures note ID, expected version and edit generation. Edits arriving during a write remain dirty and schedule the next save. A completion can acknowledge only its captured generation, never newer typing or another selected note.

Saving…, Saved and Could not save describe real persisted state. A failed/conflicting save keeps every field/image selection and offers Retry, Remain or explicit Discard to the last persisted state. Discard must not delete an already saved note. Never automatically retry a conflict or navigate through a failure.

Cancel the context owner before dependencies, drain tracked loads/saves and check operation identity before publishing. Cancellation cannot retract a committed write: reconcile that result before reporting Saved/Cancelled. Repeated cancellation, disposal, rapid selection and activation retry require controlled-interleaving tests. A crash may lose edits still inside the debounce; promise recovery of **last successful persistence**, not uncommitted keystrokes. No separate volatile working-file exclusion for saved Notebook drafts.

### Filtering, counts and linked records

Text search covers title and saved plain-text body; text, folder and all selected tags combine with AND. Dedicated Archived/Trash searches stay inside their lifecycle scope. Sidebar scope counts represent that scope's lifecycle population before text/tag filters; list count represents all applied filters. Read counts/list consistently; label both clearly. Debounce search, cancel superseded work and suppress stale results. Bounded pages keep all results reachable.

M22.9 pickers resolve existing **Trades** and **Trading Setups** only. Persist `(kind, targetId)` plus last-known display label for unavailable presentation, not a copy of target-owned content. Current names are resolved in bounded batches, not a query per row; renamed targets do not change link identity. Deleted/unavailable targets retain the note and a clearly marked unavailable chip; never attach a same-name replacement. Open existing details through guarded navigation. Opening a Setup may use its Playbook destination when available; it remains a Setup link, not a promise of frozen Playbook guidance.

Removing a link never deletes a target or its screenshots. A displayed Trade screenshot reference remains Trade-owned; if an independent image is wanted in the note, import an explicitly owned copy. Do not add Strategy-specific persistence or an unused discriminator. Future real target types require explicit contract/reader support.

### Images, backup and compatibility

Store note-owned images through managed screenshot storage keys and a Notebook attachment ledger, not caller paths. Retain originals and metadata (size, hash, type, ordering and caption). Missing/unreadable images show placeholders while preserving text and references; do not silently remove them on load.

Extend the existing capture lease and reference checks to include all retained notes. Image removal commits metadata first, then cleanup rechecks shared ownership; import stages bytes before metadata references become visible. Failed compensation is reported/retained for safe cleanup rather than deleting uncertain references. Trade, Playbook and Plan assets retain their existing protections.

Notebook persistence requires additive migration(s), no seed notes and no modification of existing economics/classifications. Complete backups include note content, folders/tags, links, all lifecycle states and note-owned binaries. Preflight checks storage/content versions, required files, sizes/hashes and metadata consistency; restore must preserve recovery protection and installed data on failure.

At this baseline archive format is v1, logical JSON export v14. Installed databases migrate additively; **older-schema archive restore remains unsupported** until a separate staged-upgrade path is implemented. A pre-Notebook archive must not be presented as restorable after a Notebook schema change merely because its archive format is still v1. Advance logical export when tables are added, preserving formatted JSON and metadata; logical export is not a restorable backup and does not embed image binaries. Standalone Notebook import/export is deferred. Backups/exports contain sensitive unencrypted notes; credentials remain excluded.

Portability hooks must accompany the owning storage changes; M22.11 is their integrated acceptance, not permission to silently omit newly persisted data until then.

### Delivery and verification

| Slice | Deliverable / evidence required | Status |
| --- | --- | --- |
| M22.1 | Repository map, UX/content/limits/ownership contract | Documented here |
| M22.2 | Domain/Application invariants, lifecycle and explicit outcomes | Implemented and locally verified below |
| M22.3 | EF/additive migration, bounded reads, safe codec, transactional projection and concurrency | Implemented; historical verification below |
| M22.3A | Logical export completeness and current-schema database round-trip compatibility | Correction and verification above; owned image files still deferred |
| M22.4 | Shell workspace, folders, responsive navigation and guards | Implemented; verification above |
| M22.5 | Rich-text adapter, protected image references, safe paste, manual Save and draft guards | Implemented; automated verification and live/CI limits above |
| M22.6 | Deterministic autosave, failure, stale completion, cancellation and restart tests | Pending |
| M22.7 | Tags, AND filters, stable paging, counts and stale-search tests | Implemented; complete local Release gate 5,131/5,131; human/native restart and matching CI unverified above |
| M22.8 | Image admission, ownership, compensation, missing files and retention | Pending |
| M22.9 | **Linked Trades & Trading Setups**, current labels/unavailable targets and guarded opening | Pending |
| M22.10 | Blank Note, Market Study, Trading Idea, Lesson Learned, Psychology templates; duplicate/archive/trash/restore/delete | Pending |
| M22.11 | Consistent complete backup/preflight/restore round trips and failure recovery | Pending |
| M22.12 | Integrated synthetic workflows, full Release gate, automated renders and separate live acceptance | Pending |

Templates supply editable headings only: Market Study (context/observation/examples/conclusion/open questions), Trading Idea (idea/conditions/invalidation/evidence to collect), Lesson Learned (situation/observation/lesson/next action), Psychology (trigger/thoughts/behavior/alternative response). No invented outcomes or automatic links.

Implementation verification must cover Light/Dark, normal and narrow/240-DPI layouts, keyboard focus, accessible names/states, long content and missing-content recovery. Distinguish bound-WPF/render evidence from actual input and screen-reader acceptance. Use isolated synthetic databases/images and fake dependencies only. Preserve existing assertions, deadlines and concurrency coverage.

M22.1 changed-file inventory: **`README.md`, `docs/notebook.md` only**. Read-only repository inspection confirmed the existing placeholder, plain-text Journal controls, Setup-owned Playbook identities, image bounds/retention and explicit backup inventory. No `AGENTS.md` was found in the repository or checked ancestor chain. Verification: `git diff --check` and `git diff --no-index --check -- /dev/null docs/notebook.md` passed; a PowerShell local-link target check across both changed documents checked **157 targets, zero missing** (target existence, not anchor validation). No build, EF check, test suite, render, live UI or matching CI run is claimed for this documentation-only slice. The prior 4,935-test Release result remains historical and is not Notebook acceptance. Run focused checks as implementation arrives and the established `pwsh -File scripts/Test-Release.ps1 -ResultsDirectory TestResults` gate at integrated completion, with Release build, EF consistency and whitespace checks.

## M22.2 — Domain Model and Application Contracts

20 October 2026. Actual starting baseline: **clean `develop` at `ab9097c008755acf512c91c1e2b6b9f7cf3b36e2`**. M22.1 documentation was already committed; its scope decisions are preserved. Repository/ancestor instruction discovery found no `AGENTS.md`. Only the Domain/Application foundation and focused tests are implemented in this slice.

### Implemented boundaries and invariants

- `NotebookNote`, `NotebookFolder` and `NotebookTag` reuse `AuditableEntity`: nonempty stable GUIDs, zero-offset UTC timestamps, monotonic updates and reconstruction validation. Their positive versions start at one and advance on mutations; overflow or invalid audit input cannot partially apply an edit. The future store compares expected versions transactionally.
- A new note is Active, unpinned, empty and **Untitled**, with either a valid folder ID or null (Unfiled). Required titles trim and retain text/line breaks; folder/tag names use trim + Unicode Form C with ordinal-ignore-case name comparers. Invalid Unicode/NUL and documented length bounds are rejected. Cross-entity name uniqueness and folder/tag existence remain transactional application responsibilities.
- Note collections are bounded defensive read-only copies. Tags reject duplicate/empty IDs; links reject duplicate `(kind, targetId)` even when labels differ. Link kinds are exactly `Trade` and `TradingSetup`; a same GUID in different target kinds is not the same link. No Strategy or independent Playbook kind was introduced.
- Active-only editing covers title, document/images, folder, pin, tags and links. Archive/unarchive and trash/restore are explicit transitions. Trash preserves prior Active/Archived state and folder, plus content, pin and assignments. Restore takes folder availability as application-supplied transactional information and falls back to Unfiled; no database lookup occurs in the domain.
- Folder/tag deletion maintenance is explicitly different from editing read-only content: `ApplyFolderDeletion` and `ApplyTagDeletion` update references in all lifecycle states and advance the affected note version. A deleted folder is cleared from both current and restoration metadata. Ordinary editing of Archived/Trashed notes remains prohibited.
- `NotebookDocument` / `NotebookNode` represent the agreed v1 tree: paragraph, heading, run marks, line break, HTTP/HTTPS hyperlink, ordered/unordered lists, interactive checklist data and positioned images. Factories prevent irrelevant attributes; document construction validates parent/child shape, unique node IDs, total nodes/depth/list nesting, image references and total projected text. Its immutable search projection is derived from visible text/captions, with LF after paragraphs/headings/images and explicit line breaks; URLs, alt text, target content and image bytes are excluded. No caller can supply a different projection.
- `NotebookImage` distinguishes **NoteOwned** (managed key, nonzero bounded byte size and SHA-256) from **TradeScreenshotReference** (exact Trade and screenshot IDs, no owned key/hash/size). Metadata has ordered attachment identity, optional caption and alternative text. The document and ordered image list must match one-to-one, including captions/alt text, before an atomic aggregate content edit succeeds. Removing an image/reference has no file side effects.
- Link fallback labels are capped at 2,000 characters to bound unavailable-target metadata; they are labels, not frozen Trade/Setup content. Opaque pagination cursors are capped at 2,048 characters. These transport metadata bounds do not change authored title/body limits.

Application ports and DTOs:

- `INotebookService`: load, consistent list/summary, bounded folder/tag continuation, note commands, catalog commands and batched link resolution. Read cancellation/storage exceptions follow existing ports; writes carry explicit sanitized Invalid, Conflict, NotFound, UnavailableTarget, UnsupportedContent, TooLarge, Cancelled or StorageFailure outcomes.
- `NotebookCommands.cs`: create/load-related workflows, title/content update, move, pin, tag assignment/removal, link/unlink, archive/unarchive, trash/restore, confirmed permanent delete, duplicate and image metadata add/update/remove. Catalog commands cover folder/tag create/rename/delete. Every existing-entity mutation carries an expected version. Transport command payload validation belongs to later handlers; domain types enforce aggregate invariants now.
- Image commands include the complete resulting document and ordered metadata, not an isolated caption mutation that could desynchronize search text. The later handler verifies the operation's delta, managed file admission and ownership before committing.
- Duplication requires independent managed copies/new attachment identities and a new Active/unpinned note; permanent deletion requires confirmed Trash state and atomic reference/retention recheck. These are **orchestration contracts**, not filesystem operations or a completed duplicate/delete workflow.
- `NotebookQuery` specifies lifecycle scope, exact folder, bounded search, all-tags-required filtering, sort and 1–100 page size. Folder scope requires its ID; other scopes reject one instead of silently changing scope. Recently Updated uses descending timestamp and ascending ID; Title uses normalized ordinal-ignore-case ordering and ascending ID. Cursors must be bound to the query and derived from the last displayed row. Scope counts exclude text/tag filtering; filtered total and note rows share the same read. Folder/tag catalog pages also remain bounded, with continuations.
- `NotebookNoteSnapshot` copies aggregate collections into immutable arrays, so later aggregate edits cannot rewrite a previously returned snapshot. Load outcomes distinguish missing, malformed, unsupported and oversized content without substituting an empty editable note.
- `INotebookContentCodec` defines the **future** safe JSON boundary: exact format/version, 1 MiB UTF-8 limit, allowlisted nodes and projection derived from the validated document. No parser/serializer implementation or editor serialization was added. Exact serialized byte size cannot be measured by this typed-tree layer; codec/store enforcement is required in M22.3. Image signatures, dimensions, decoder support and original bytes likewise require M22.8 admission; metadata validation is not proof of decoded image safety.

### Verification and limits

All tests use synthetic in-memory objects; no database/file/provider operations were introduced. Final commands and results:

```powershell
dotnet test tests/PersonalTradingJournal.Domain.Tests/PersonalTradingJournal.Domain.Tests.csproj -c Release --filter FullyQualifiedName~Notebook --logger trx --results-directory TestResults/m222-focused-domain
dotnet test tests/PersonalTradingJournal.Application.Tests/PersonalTradingJournal.Application.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~Notebook --logger trx --results-directory TestResults/m222-focused-application-final
dotnet test tests/PersonalTradingJournal.Domain.Tests/PersonalTradingJournal.Domain.Tests.csproj -c Release --no-restore --logger trx --results-directory TestResults/m222-domain-all
dotnet test tests/PersonalTradingJournal.Application.Tests/PersonalTradingJournal.Application.Tests.csproj -c Release --no-restore --logger trx --results-directory TestResults/m222-application-all
dotnet build PersonalTradingJournal.sln -c Release --no-restore
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
git diff --check
```

- Focused: **34 Domain + 11 Application = 45 passed**, zero failures/skips.
- Complete affected-layer Release suites: **601 Domain + 1,032 Application = 1,633 passed**, zero failures/skips. TRX files are under the respective directories above.
- Final Release solution build: **0 warnings / 0 errors**, 25.57 seconds.
- EF: **no pending model changes**, using the repository's `:memory:` design-time factory. The first EF invocation incorrectly specified Desktop as startup and failed because Desktop does not reference EF Design. Correcting the command to the existing Infrastructure design-time factory passed; no dependency/package/schema change was made to accommodate the command.
- The first focused Application run passed 10 cases; the final run includes the additional bounded catalog-query case. No failing test attempt or deadline/assertion change occurred.
- `git diff --check` plus `git diff --no-index --check -- /dev/null <new-file>` for all eleven new files passed. PowerShell checked **158 local documentation targets, zero missing** across README and this document (target existence, not anchor validation). Final working tree: two modified documentation files and eleven new source/test files; branch/HEAD unchanged, no commit/push/merge.

Coverage includes malformed/boundary input, normalization, audit/version atomicity, lifecycle/read-only transitions, restoration to existing/missing folders, maintenance of trashed references, duplicate/limit enforcement, immutable collections/snapshots, typed links, safe URL schemes, content structure/projection, image metadata ownership and exact document/attachment reconciliation. Contract-only ports cannot yet demonstrate transactional rollback, concurrent database writes, file compensation, restart or actual cancellation draining; those tests belong to the implementing slices.

No unresolved scope conflict: separate Strategy support remains deferred and Playbook links use actual Trading Setup identity. **M22.3–M22.12 remain pending.** No migration, persistence handler/DI wiring, filesystem work, editor/UI, autosave, export/backup contract change or AI evidence integration. No complete Desktop/Infrastructure Release gate or live/render/CI acceptance claimed at this foundation checkpoint; integrated full-gate acceptance remains M22.12.

### Exact M22.2 changed-file inventory

Two existing documentation files modified; eleven new source/test files:

1. `README.md`
2. `docs/notebook.md`
3. `src/PersonalTradingJournal.Domain/Notebook/NotebookLimits.cs`
4. `src/PersonalTradingJournal.Domain/Notebook/NotebookCatalog.cs`
5. `src/PersonalTradingJournal.Domain/Notebook/NotebookReferences.cs`
6. `src/PersonalTradingJournal.Domain/Notebook/NotebookDocument.cs`
7. `src/PersonalTradingJournal.Domain/Notebook/NotebookNote.cs`
8. `src/PersonalTradingJournal.Application/Notebook/NotebookQueries.cs`
9. `src/PersonalTradingJournal.Application/Notebook/NotebookCommands.cs`
10. `src/PersonalTradingJournal.Application/Notebook/INotebookService.cs`
11. `tests/PersonalTradingJournal.Domain.Tests/Notebook/NotebookNoteTests.cs`
12. `tests/PersonalTradingJournal.Domain.Tests/Notebook/NotebookDocumentTests.cs`
13. `tests/PersonalTradingJournal.Application.Tests/Notebook/NotebookContractTests.cs`

## M22.3 — persistence, migrations and content format

20 October 2026. Starting baseline: **clean `develop` at `66243544eae985b58f68a104f54f7a94f68a81cc`**. M22.1/M22.2 and the preceding Trading Plan corrections are preserved. No branch switch, commit, production database, network/provider request or Desktop change.

### Storage and content decisions

- Additive migration **`20261020085611_AddNotebook`**, generated through EF, creates six tables: `NotebookNotes`, `NotebookFolders`, `NotebookTags`, `NotebookTagAssignments`, `NotebookLinks`, `NotebookImages`. No backfill, seed notes or changes to existing financial records. Audits use the existing UTC converter; persisted versions are concurrency tokens.
- The existing Domain `NotebookNote.Rehydrate` remains authoritative. Separate records contain document JSON/version, derived plain/search text, title, pin, lifecycle/restoration state, folder, tags, ordered images, typed links and audit/version fields. Loading validates the complete aggregate and projection; it does not reset identity/timestamps or manufacture content.
- Folder/tag names use Domain trim + Form C normalization and ordinal-ignore-case identity. Persisted keys are invariant-uppercase UTF-16 big-endian hex with SQLite BINARY ordering, not SQLite's ASCII-only NOCASE. Unique indexes enforce those keys. Note-title sort keys normalize for ordering without rewriting saved titles. Storage does not require custom SQLite collations.
- Restrictive FKs prevent catalog/child cascades. Folder deletion atomically moves all lifecycle/restoration references to Unfiled; tag deletion removes assignments, never notes. Both advance affected note versions/timestamps. Linked Trade/Trading Setup identities and Trade-screenshot references deliberately have no target cascade: missing sources remain identifiable by their original IDs/last-known labels.
- JSON v1 has exact `format`, `version`, `blocks` properties. Every node has `id` and an allowlisted `kind`: `text`, `lineBreak`, `paragraph`, `heading`, `hyperlink`, `bulletedList`, `numberedList`, `checklist`, `listItem`, `checklistItem`, `image`. Type-specific properties preserve marks, heading levels, ordered children, checkbox state, safe URLs, attachment IDs, captions and alternative text. Unknown/duplicate/missing properties, unsupported nodes, unsafe values and invalid shapes fail explicitly. No WPF objects, XAML, arbitrary types, HTML execution or editor serialization.
- Codec checks strict UTF-8 and **1 MiB serialized content**, then the Domain's **200,000 text characters / 4,096 nodes / depth 16 / list depth 3** constraints. Encoding also checks the actual escaped JSON byte count. `PlainText` comes only from the validated tree, not a supplied projection.
- Unsupported version, malformed content and oversized content remain distinct load outcomes. No empty editable document is substituted. Bounded original JSON is returned for recovery; oversized original stays in storage. Minimal contract additions: optional `NotebookUnavailableMetadata` (identity/title/version/state) and an appended `Deferred` write result. Ordinary writes to unreadable notes are blocked rather than fabricating a Domain object. Unrelated catalog maintenance can clear folder/tag references without decoding or rewriting that payload.

### Wired operations and transaction ownership

DI registers `INotebookContentCodec` → `NotebookJsonCodec` and `INotebookService` → `NotebookService`. Each operation owns a short-lived factory-created context and SQLite connection.

Implemented database-only commands: create; title and text/document editing with unchanged image metadata; move; pin/unpin; assign/remove tag; link/unlink existing **Trade / Trading Setup**; archive/unarchive; trash/restore; create/rename/confirmed-delete folders and tags. No Strategy link/catalog is introduced.

Writes reserve SQLite's writer before reading/checking the expected version and references. Domain validation, metadata/content/projection changes, child assignments and verified readback share that transaction. Stale writes return Conflict without mutation. Invalid/missing/unavailable/unsupported/too-large/storage outcomes are explicit and sanitized. Cancellation before the final commit rolls everything back; the final cancellation check is followed by a non-cancellable commit. A cancellation arriving after commit returns Success with the verified snapshot. There is no shared editor context, retry policy or autosave scheduler.

Reads use a deferred SQLite snapshot transaction. Active/Pinned/folder/Unfiled exclude Archived/Trash; search combines with **all selected tags**. Recently Updated is descending, Title is normalized ordinal-ignore-case ascending, each with stable ID tie-breaking. Page sizes remain 1–100; opaque cursors bind filter/sort and advance from the last displayed row, not lookahead. Scalar list projections omit full JSON and bound previews to 180 characters. Sidebar counts exclude text/tag filters and share the same snapshot as filtered totals/rows. Initial folder/tag pages are bounded to 50, with separate bounded continuations. Link resolution is a bounded batch, not per-note target queries; same-name replacements never retarget an old link.

**Explicitly deferred:** all duplicate/permanent-delete/image commands return `Deferred` without writes, including currently image-free duplicates/deletes. This avoids metadata-only shortcuts before file ownership, copy/cleanup compensation and retention orchestration are implemented. `UpdateNotebookContent` also rejects image-set changes through this route. Image metadata round trips are tested using synthetic records; no file import, decoding, deletion, thumbnail generation or retention-union implementation is claimed. Cross-resource work belongs to the later image/lifecycle slices.

### Portability and compatibility boundary

Historical M22.3 result (superseded by M22.3A above): per that slice's instruction, archive format v1 and logical export v14 were unchanged. SQLite snapshots naturally included tables, but that alone did **not** establish complete Notebook backup/preflight/restore or owned-image inclusion. The logical export omitted the six new tables and its unchanged all-logical-columns assertion detected the gap. M22.3A brings the database/export correction forward rather than leaving this failure until M22.11. Older-schema archive restore remains unsupported by the existing exact-schema preflight; a pre-Notebook archive is not automatically compatible with the migration.

No Notebook Desktop workspace, editor adapter, autosave, image-file workflow, template workflow, complete portability, live accessibility or matching CI acceptance is claimed. **M22.4 onward remains pending**; M22.12 owns integrated acceptance.

### Verification

Synthetic temporary SQLite databases only, with independent contexts/connections, controlled interceptors and bounded task gates. Coverage includes every supported node, Unicode and byte bounds, strict malformed/future payload handling, audit/lifecycle/restore, uniqueness and relational constraints, queries/tags/cursors/counts, unavailable links, unchanged image ownership, stale concurrent writers, cancellation/rollback before commit, cancellation after commit, DI restart reads and upgrade preservation of Journal/Trades/Setups/screenshots/Playbook/Rule records.

Commands used (all Release; logs in `TestResults/m223`):

```powershell
dotnet ef migrations add AddNotebook --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests -c Release --filter "FullyQualifiedName~Notebook|FullyQualifiedName~InitialMigrationTests" --logger "trx;LogFileName=notebook-focused-final.trx" --results-directory TestResults/m223
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests -c Release --no-restore --filter "FullyQualifiedName~Notebook|FullyQualifiedName~InitialMigrationTests|FullyQualifiedName~JournalDatabaseInitializerTests|FullyQualifiedName~SqlitePersistenceIntegrityTests|FullyQualifiedName~ProductionPersistenceIntegrationTests|FullyQualifiedName~TradingRulePersistenceTests" --logger "trx;LogFileName=notebook-persistence-final.trx" --results-directory TestResults/m223
dotnet test tests/PersonalTradingJournal.Domain.Tests -c Release --no-build --no-restore --logger "trx;LogFileName=domain-all.trx" --results-directory TestResults/m223
dotnet test tests/PersonalTradingJournal.Application.Tests -c Release --no-build --no-restore --logger "trx;LogFileName=application-all.trx" --results-directory TestResults/m223
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests -c Release --no-build --no-restore --logger "trx;LogFileName=infrastructure-all.trx" --results-directory TestResults/m223
dotnet build PersonalTradingJournal.sln -c Release --no-restore
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
git diff --check
```

Implementation-test chronology: the first focused run was **25 passed / 1 failed** (an immutable-array identity assertion, corrected to sequence comparison). The expanded run was **26 passed / 4 failed**: EF could not translate ordering over the positional catalog projection; member-initialized projections fixed translation while keeping paging in SQL. Subsequent focused runs passed **42**, then **45**, then **46** tests. The first complete Infrastructure run was **1,477 passed / 9 failed / zero skipped** (3m21s): seven pre-Notebook schema/model inventory expectations, one unchanged export-completeness failure (35 model tables versus 29 exported tables), and one Windows symbolic-link privilege failure. Schema inventory checks were extended explicitly for the migration; no functional assertion/deadline was removed. Final verification results are recorded below.

Final local results:

- Expanded affected persistence regressions: **109 passed / zero failed or skipped**, 34s (`notebook-persistence-final.trx`). Includes **34 new Notebook codec/SQLite checks**, the approved-schema inventory and existing initialization/integrity/Rules/graph regressions.
- Complete Domain **601/601** and Application **1,032/1,032**, zero failures/skips.
- Complete Infrastructure rerun, same entire project, freshly built binaries, outside the sandbox to permit the existing symlink-security check: **1,485 passed / 1 failed / zero skipped**, exit 1, **3m18s**, `infrastructure-all-final.trx`. Command identical to the complete Infrastructure command above except `LogFileName=infrastructure-all-final.trx`. The symbolic-link test passed; the sole remaining failure is `PortableExportTests.AllLogicalColumnsAreExplicitlyCoveredExceptRebuildableBrowseCache`: **35 model tables / 29 export tables**. This is the six-table Notebook portability gap deliberately deferred by the M22.3 request. The assertion, backup/export code and formats remain unchanged; integrated regression acceptance is **not green**.
- Final `dotnet build PersonalTradingJournal.sln -c Release --no-restore`: **zero warnings/errors**, 8.04s. EF: **no pending model changes**, in-memory design-time factory, no production connection.
- Tracked and all 14 new-file whitespace checks passed. README/Notebook documentation target-existence checks: **159 local targets, zero missing** (not anchor validation).
- Final branch/HEAD unchanged; **11 existing files modified + 14 new files**, all uncommitted. Complete Desktop/parallel Release gate, renders, human interaction and matching corrected-source CI were not run/claimed for this persistence-only slice. No retries/skips/deadline changes were added to tests.

### Exact M22.3 changed-file inventory

Existing files:

1. `README.md`
2. `docs/notebook.md`
3. `src/PersonalTradingJournal.Application/Notebook/INotebookService.cs`
4. `src/PersonalTradingJournal.Infrastructure/Persistence/JournalDbContext.cs`
5. `src/PersonalTradingJournal.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs`
6. `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/JournalDbContextModelSnapshot.cs`
7. `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Initialization/JournalDatabaseInitializerTests.cs`
8. `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integration/ProductionPersistenceIntegrationTests.cs`
9. `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integrity/SqlitePersistenceIntegrityTests.cs`
10. `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Migrations/InitialMigrationTests.cs`
11. `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePersistenceTests.cs`

New files:

12. `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookJsonCodec.cs`
13. `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookMapper.cs`
14. `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.cs`
15. `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.Catalog.cs`
16. `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.Queries.cs`
17. `src/PersonalTradingJournal.Infrastructure/Persistence/Configurations/NotebookConfiguration.cs`
18. `src/PersonalTradingJournal.Infrastructure/Persistence/Records/NotebookRecords.cs`
19. `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261020085611_AddNotebook.cs`
20. `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261020085611_AddNotebook.Designer.cs`
21. `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookCodecTests.cs`
22. `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookFixture.cs`
23. `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookPersistenceTests.cs`
24. `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookQueryTests.cs`
25. `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookMigrationTests.cs`
