# Notebook

Current checkpoint: **M22.4 workspace and navigation**, with read-only content preview. Logical export v15 includes all six Notebook tables; current-schema v1 archives preserve their database records through preflight/offline restore. JSON/CSV are not restore inputs. Editing/autosave, Notebook-owned image-file copying/restoration and full integrated acceptance remain deferred. Earlier milestone sections below are historical decisions/results.

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
| M22.5 | Formatting/checklists/images adapter, paste normalization, undo/caret and round trips | Pending |
| M22.6 | Deterministic autosave, failure, stale completion, cancellation and restart tests | Pending |
| M22.7 | Tags, AND filters, stable paging, counts and stale-search tests | Pending |
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
