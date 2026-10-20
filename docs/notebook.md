# Notebook

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
| M22.3 | EF/additive migration, bounded reads, safe codec, transactional projection and concurrency | Pending |
| M22.4 | Shell workspace, folders, responsive navigation and guards | Pending |
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
