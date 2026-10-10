# Backup and restore — M16

## M16.2 — consistent SQLite snapshot (2026-10-10)

Current implementation baseline: clean `develop`, `7b09afa3a895c786302234f91be63f64a6726760`, containing M16.1. The M16.1 manifest, limits and exclusions remain intact. `IDatabaseSnapshotService` / Infrastructure `SqliteDatabaseSnapshotService` now implement **database-only staging** for later packaging. Registration in `AddPersistence` is inert: resolving/opening the app does not create a snapshot. Only an explicit `CreateAsync(absoluteStagingParent, cancellationToken)` call does work. There is no Desktop action, archive creation, restore or installed-schema migration.

### Snapshot lifecycle and ownership

1. Source is exactly `IApplicationPaths.DatabasePath`. It must already exist as a regular, non-linked file. Open a separate **read-only**, private-cache, nonpooled `Microsoft.Data.Sqlite` connection. Never create a missing source, copy its live `.db` bytes, checkpoint its WAL, change its journal mode or use global pool cleanup.
2. The caller supplies an absolute staging parent. Reject linked path components and invalid/unwritable locations. Create an operation-ID-named `snapshot-<id>.partial` child, reserve `journal.db` with `CreateNew`, then open a separate nonpooled read/write destination. Never reuse an arbitrary existing destination. Other completed snapshots and unrelated files remain untouched.
3. Call the repository's resolved **Microsoft.Data.Sqlite 10.0.11 `source.BackupDatabase(destination)`**. This copies committed SQLite state, including committed pages still in WAL, through the SQLite backup facility. A held, uncommitted WAL writer cannot leak half a Trade/Journal transaction into the copy. The snapshot reflects a coherent database point, not necessarily every write completed before the overall service returns.
4. Check cancellation immediately after native copy. Convert **only the staging destination** to `journal_mode=DELETE`, then require exactly one `ok` result from `PRAGMA integrity_check` and zero rows from `PRAGMA foreign_key_check`. These checks are deliberately separate: the former does not detect FK violations. Reject corrupt/incomplete output, not a readable subset. See [SQLite's integrity-check distinction](https://www.sqlite.org/pragma.html#pragma_integrity_check).
5. Require the copied migration IDs to exactly match the current shipped catalog. Build the trusted current schema by running shipped migrations against a separate **in-memory reference DB**, not the source/destination, and compare application table/index/view/trigger definitions. Missing columns/indexes/tables or unexpected application objects fail even if the migration table claims the latest version. Exclude SQLite internal objects and EF's migration-lock helper from that comparison. This is intentionally fail-closed for manually changed DDL. An older known prefix is not captured by this service: return `IncompatibleSchema`; staged legacy upgrade/restore remains later work under M16.1, never an implicit live migration.
6. Close connections. Require a positive database size within M16.1's 8-GiB ceiling and no sidecars. Stream SHA-256 of the closed file with cancellation checks. Return M16.1 `BackupDatabaseSchema` and database `BackupFileEntry` (`data/journal.db`, byte length, lowercase digest), plus UTC creation time and local staging handoff paths. The size limit is checked after the native copy; native disk-full failures during copying are handled, not prevented by a free-space reservation.
7. Recheck cancellation, rename the private `.partial` directory to its completed operation name without overwrite, and recheck before returning Success. A cancellation noticed after the rename removes that operation's output instead of returning it. Cancellation after the final check can race a completed Success; callers must handle that already-complete staging result, not interpret cancellation as proof no work occurred. Success transfers staging ownership to the caller for later packaging/cleanup. It is **not** a complete portable backup: screenshot bytes are not captured or checked here.

### Cancellation, locking and diagnostics

The service runs synchronous SQLite work on an awaited worker task; it never returns a cancelled result while abandoning a copy in the background. Version 10.0.11 performs one native whole-database backup step and exposes no cancellation token. Pending cancellation is observed before work, at phase boundaries (including immediately after copy), while reading schema rows/hashing, and before publication. It cannot preempt an executing native copy, `integrity_check`, synchronous filesystem call or individual SQL operation. Large copies/checks can therefore delay cancellation; **no hard whole-operation wall-clock deadline is claimed**. Do not dispose those connections from a cancellation callback.

Connections use a two-second default SQLite command/busy timeout. There are no service-level retries. Backup can return Busy immediately; synchronous native copying can hold a source read lock and block writers in rollback-journal mode. WAL improves reader/writer overlap but does not make all locking conflicts impossible. The successful busy test uses an exclusive rollback-journal writer and an outer test deadline, not sleeps or a claimed hard copy timeout. See [Microsoft.Data.Sqlite backup behavior](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/backup) and the [pinned 10.0.11 implementation](https://github.com/dotnet/efcore/blob/v10.0.11/src/Microsoft.Data.Sqlite.Core/SqliteConnection.cs).

`DatabaseSnapshotResult` carries only an allowlisted status, phase, random operation ID, optional primary SQLite error number and `CleanupFailed` diagnostic. Failures have no usable snapshot. No exception message, source/staging path, SQL output, Account identity, credentials or payload appears in its diagnostic `ToString`; success paths are separate local handoff properties, never manifest fields. No raw SQLite integrity/FK rows are logged.

| Condition | Outcome |
| --- | --- |
| Source absent; source inaccessible/invalid location | `SourceMissing`; `SourceUnavailable` (never creates a source) |
| Invalid/occupied/inaccessible staging location | `DestinationUnavailable` |
| SQLite BUSY/LOCKED | `Busy` |
| SQLite FULL or Windows disk-full I/O error | `InsufficientStorage` |
| Corruption/not-a-database/integrity failure | `InvalidDatabase` |
| FK violations | `ForeignKeyViolation` |
| Missing/older/newer/forked migrations or mismatched current DDL | `IncompatibleSchema` |
| Staged DB outside size limit | `LimitExceeded` |
| Observed cancellation | `Cancelled` |
| Other known storage errors | `IoFailure` |

On failure, close handles first, then remove only the operation-owned database and recognized SQLite sidecars and its empty operation directory. Do not recursively delete the parent or unrelated files. If cleanup fails, return `CleanupFailed=true`; a leftover directory must not be consumed as a completed snapshot or described as removed. The operation ID identifies its staging name for controlled later cleanup. Process termination/power loss can also leave private staging directories; restart scavenging/recovery is not implemented in M16.2. The caller must use a private writable staging parent; these checks are not a security boundary against a malicious process with the same user's filesystem access.

### Isolated evidence and remaining scope

`SqliteDatabaseSnapshotTests` uses migrated disposable databases, synthetic Trades/Journals/AI responses, and nonpooled test connections. Coverage includes committed uncheckpointed WAL; an active uncommitted WAL writer whose later commit remains wholly outside the captured revision; exact Trade/execution/Account, Journal history and AI JSON identities; independent reopening without sidecars; missing/corrupt/empty source; separate integrity/FK failures; false/latest/missing schema metadata and staged-output tampering; pre-start/post-copy/finalization cancellation; busy writers; repeated snapshots preserving earlier output; inaccessible destination; bounded safe diagnostics and cleanup failure. Native disk-full/I/O mapping is fault-injected after a real copy, **not** tested by exhausting a physical disk. Raw source byte comparisons close only that fixture's idle pool; no global pool invalidation was added.

Screenshot inventory/mutations and DB/file consistency are explicitly **M16.3**. M16.2 must be composed inside that future attachment-coordination lifetime; a valid database snapshot alone cannot guarantee that referenced screenshot files still exist. Full ZIP verification, staged migrations for legacy restore, recovery replacement, UI/progress and crash recovery remain later milestones. Physical disk exhaustion, very large DB performance and hostile filesystem races remain unverified. No production journal, credential or AI provider was accessed. Verification results follow in README; earlier M16.1 notes below are historical contract-stage scope, not a claim that this service does not exist.

Verification: **23 snapshot integration cases and 37 manifest cases passed**, zero failures/skips. The complete parallel Release run passed **3,485 tests** (Domain 454, Application 710, Infrastructure 941, Desktop 1,380), zero failures/skips; no full-suite retry was needed. Solution Release build: **0 warnings/errors**; EF reports no pending model changes; tracked/new-file whitespace checks passed. Logs/TRX are ignored under `artifacts/m162*`. Local results are not a GitHub Actions result or live UI acceptance.

Changed files: Application `Backups/IDatabaseSnapshotService.cs`; Infrastructure `Backups/SqliteDatabaseSnapshotService.cs`, `Persistence/PersistenceServiceCollectionExtensions.cs`, `Properties/AssemblyInfo.cs` (internal deterministic test seam); Infrastructure tests `Backups/SqliteDatabaseSnapshotTests.cs`; `.gitignore` (source-only exceptions), `README.md`, and this document. Branch/HEAD remain `develop` / `7b09afa3a895c786302234f91be63f64a6726760`; no commit, push or merge. M16.1 contracts remain unchanged.

## M16.1 scope and status (2026-10-10)

M16.1 defines a complete **portable journal-data** archive, not a machine image or a transfer of credentials. It implements Application manifest records, fixed JSON conventions, resource limits, safe validation codes and a pure manifest-declaration validator. It does **not** implement ZIP creation/reading, file hashing, SQLite snapshot creation, archive-content validation, extraction, restore, recovery replacement, scheduling or Desktop UI. A valid manifest is never permission to replace the installed journal.

Inventory was inspected in source at clean `develop` HEAD `d10dc6d2de69e10e204affa6657f5a535a4a8e20`; no production data/credential directory was opened. The existing shared page header, Account Current Balance, M15 evidence and generation behavior are unchanged. No migration is needed for this contract-only milestone.

## Persisted inventory and portable scope

Paths below describe source-code conventions, not files inspected on a user's machine. `LocalApplicationPaths` uses `%LOCALAPPDATA%/PersonalTradingJournal/` for ordinary data. The explicit isolated-data-root launch option substitutes a separate root for tests.

| Item | Current storage / relationships | Portable v1 policy |
| --- | --- | --- |
| Database and migration history | `journal.db`, all 14 current application tables plus `__EFMigrationsHistory` | Required **consistent SQLite snapshot**, including IDs, nulls, decimal facts, audit times and migration rows. Never export selected rows or reconstruct economics. |
| Accounts, Instruments, Setups, Mistakes | `TradingAccounts`, `Instruments`, `TradingSetups`, `TradingMistakes` | Included in database. Preserve inactive states and exact identities, not name-based remapping. |
| Trades, executions, classifications | `Trades`, `TradeExecutions`, `TradeMistakes`, `TradeBrowse` | Included. Preserve Account/Instrument/Setup references, execution IDs and pricing snapshots; retain the derived browse projection rather than silently omitting it. |
| Import provenance / replay protection | `TradovateImportedExecutions`, `TopstepImportedRows` | Included, including allocation identity and stored source facts/fingerprints. Restoring without these would change deduplication. Original CSV files are user-owned inputs, not database file references, and are not required or copied. |
| Screenshot metadata and bytes | `TradeScreenshots`; `screenshots/<StorageKey>` | Every distinct referenced storage key must have exactly one payload file. Many screenshot rows may share a key. Retain screenshot/Trade IDs, original filename label, metadata and key; do not rewrite the key or reinterpret `FileName` as a path. |
| Daily Journals and durable history | `DailyJournals`, `DailyJournalRevisions` | Include all text, answers, Draft/Completed state, revision/concurrency numbers, date and exact/null Account scope. Preserve intentional gaps from older-revision deletion; do not renumber or rebuild revisions. |
| Saved AI analyses | `CoachingAnalyses` | Include original account display/identity, versions, evidence/response JSON and safe usage/request metadata. Snapshots intentionally have no FK to mutable sources; deleted source citations or historical Accounts are valid, not corruption. Never substitute current evidence or invoke a provider. |
| Other external attachments | No other file reference property found in current persistence records; screenshot `StorageKey` is the only managed external-content reference | No speculative directory scan or unrelated file inclusion. New attachment types require explicit inventory/validation support before a backup can claim completeness for that schema. Text/notes may mention URLs or paths; they remain text, never instructions to follow/copy files. |
| Appearance preferences | `settings.json` currently contains only `Theme` (`System`, `Light`, `Dark`) | Optional `preferences/settings.json`, produced from the allowlisted typed preference, not a raw copy of arbitrary configuration. Missing source means default System. Invalid source settings must be reported, not quietly claimed backed up. Future secrets/settings are not implicitly opted in. |
| AI credentials and provider choice | Sibling `%LOCALAPPDATA%/PersonalTradingJournal.Secrets/{openai.dpapi,groq.dpapi,provider.txt}`; environment fallbacks | **Excluded**, including encrypted temporary files and the nonsecret provider-selection file. DPAPI uses `CurrentUser`, so copying ciphertext is not a portable credential transfer. Keep any target machine's local configuration untouched; configure provider/keys again on another computer. Never archive environment variables. |
| Logs, crash/test diagnostics | `logs/`, process/test output outside the data tree | Excluded. May contain private diagnostics; not authoritative journal data. |
| Temporary files, caches, SQLite sidecars | Screenshot/settings `.tmp`, staging files, `journal.db-wal`, `journal.db-shm`, rollback journal, decoded image caches | Excluded from archive. SQLite's snapshot operation must incorporate committed database state; exclusion of WAL is **not** permission to copy a live database file alone. |
| Prior backups / recovery copies / orphan screenshot files | `backups/`, later recovery/staging directories, unreferenced screenshot keys | Excluded from portable journal content; no recursive backup-of-backup. Unreferenced keys are not silently attached to a Trade and are not deleted by backup. Report orphan count separately. Local recovery preservation is broader than portable scope, described below. |

The inventory is grounded in `LocalApplicationPaths`, `JournalDbContext`, all `Persistence/Records` and relationship configurations, `LocalTradeScreenshotFileStorage`, `JsonDesktopSettingsStore` and `ProtectedCoachingCredentials`. No arbitrary external files are fetched.

## Version 1 archive and manifest

Container contract: single ordinary ZIP/ZIP64 file (recommended extension `.ptjbackup`), **unencrypted**, with one root `manifest.json` encoded as UTF-8. Only regular files are allowed: no directory records, links/reparse-point targets, encrypted entries or nested archive interpretation. Do not extract paths before validation. Manifest properties use exact camelCase; `kind` uses exact string enums `Database`, `Screenshot`, `Preferences`. Duplicate/unknown JSON properties, missing required properties, numeric enums, null required fields and depth greater than 16 are rejected. `BackupArchiveContract.JsonOptions` pins these JSON rules; a future bounded archive reader must enforce the manifest byte cap before deserializing.

The small manifest contains:

| Field | Contract |
| --- | --- |
| `format`, `archiveVersion` | `personal-trading-journal-backup`, integer `1`; another value is unsupported, never guessed. |
| `applicationVersion` | Informational numeric three/four-component version (max 23 characters). Never used instead of schema checks; no free-form build paths. |
| `createdAtUtc` | Nondefault ISO-8601 timestamp with zero UTC offset. Date-only/timezone-local creation times are invalid. Future producer sets it when the staged snapshot is ready; it is not a per-row commit timestamp. |
| `databaseSchema.engine` | Exact `sqlite`. |
| `databaseSchema.appliedMigrations` | Nonempty ordered EF migration IDs copied from the staged database. Must match its real migration table and the consuming app's trusted catalog. Do not infer schema from a filename. |
| `hashAlgorithm` | Exact `SHA-256` in v1. |
| `files[]` | `path`, `kind`, positive `sizeBytes` (uncompressed bytes), lowercase 64-hex `sha256` of complete uncompressed bytes. Each payload appears once; `manifest.json` is not self-listed/hashed. Producer writes files in ordinal path order for reproducibility; validator does not depend on order. |

Stable paths are `data/journal.db` (exactly one), `attachments/screenshots/<StorageKey>` (zero or more) and `preferences/settings.json` (zero or one). They are archive-relative mappings, not the installed directory layout. The future restore maps only these allowlisted roles to its controlled staging paths. No absolute local paths, machine/user names, Account labels, source filenames, secrets or provider text fields belong in the manifest.

All paths use `/` and ASCII letters/digits/`-`/`_`/`.` within nonempty segments. No leading/trailing slash, backslash, colon/ADS, whitespace, NUL/control, percent escapes, `.`/`..`, leading/trailing dots, Windows device names (including extensions), traversal or rooted/UNC paths. Screenshot keys must be one filename under the prefix with lowercase `.png`, `.jpg`, `.jpeg` or `.webp`. Compare duplicate paths **case-insensitively** even on non-Windows validation hosts. Do not normalize aliases into acceptance. Current storage generates GUID filenames; an older unsupported key must produce a specific blocking outcome, never be omitted or silently renamed.

### Fixed resource limits

Limits bound declarations **and actual streamed decompressed bytes**; compressed ZIP sizes are not proof. All limits are inclusive and checked before allocation/extraction, then while streaming. No automatic increase or silent truncation.

| Resource | v1 maximum |
| --- | ---: |
| Manifest UTF-8 bytes | 32 MiB |
| Listed payload files | 100,002 (one database, optional preferences, at most 100,000 distinct screenshot keys) |
| Full relative path | 240 ASCII characters |
| Database | 8 GiB |
| One screenshot | 512 MiB |
| Preferences | 64 KiB |
| Sum of uncompressed payload | 32 GiB |
| Complete container bytes | 34 GiB |
| Applied migrations | 1,024 (IDs at most 160 characters) |
| Reported validation issues | 64 safe codes with optional file ordinal; no untrusted path/exception echo |

These are initial safety ceilings, not tested archive capacity or existing screenshot upload limits. A larger journal is blocked with `LimitExceeded`; v1 never claims a partial backup is complete. Available disk-space and destination-path-length checks remain mandatory later, independently of archive limits.

## Compatibility and validation outcomes

The trusted migration catalog comes from the installed application, not the archive. Current latest migration is `20261008195353_AddCoachingAnalysisSnapshots` (nine migrations). Exact ordered sequence gives `Exact`; a nonempty strict prefix gives `RequiresStagedMigration`; unknown/newer/forked/reordered/duplicated/missing migration IDs give `Unsupported`. The app version is informative only. An older known prefix is **not immediately restore-ready**: later code must migrate an isolated copy, verify the supported upgrade path and all invariants, and retain the original archive. Never downgrade, migrate the installed journal during validation, or use `EnsureCreated` to fill missing schema.

`BackupManifestValidator.Validate` is a pure cancellable declaration check. It checks only version/shape, migration prefix, paths/roles/duplicates, size declarations and hash syntax. Its `IsValidManifest` explicitly says nothing about payload existence, actual digest, ZIP safety, database health or restore readiness. It performs no filesystem/SQLite operations. In-memory cancellation throws `OperationCanceledException`; later operation boundaries may translate this to the safe `Cancelled` outcome.

| Outcome | Required meaning / later behavior |
| --- | --- |
| `UnsupportedArchiveVersion` | Wrong format/version. Stop before interpreting payload or applying heuristics. |
| `InvalidManifest`, `InvalidUtcTimestamp`, `InvalidHash` | Malformed declarations/JSON/time/hash syntax. No fallback values. |
| `UnsupportedDatabaseSchema` | Unrecognized migration lineage/engine; request a compatible application rather than replace data. |
| `MissingEntry`, `DuplicateEntry`, `UnexpectedEntry` | Missing manifest/database/listed payload; repeated names including case aliases; undeclared file or unsupported role. Check both central-directory and actual entries, not only manifest declarations. |
| `UnsafePath`, `LimitExceeded` | Traversal/alias/link/encryption/resource breach. Stop; never extract to installed paths. |
| `ContentMismatch` | Observed length or streamed SHA-256 differs from manifest. Treat as changed/corrupt content, not a warning that permits restore. |
| `IncompleteArchive` | Truncated ZIP, unreadable central directory/entry, invalid CRC or unfinished publication. Never accept the readable subset. |
| `DatabaseIntegrityFailed` | Staged SQLite integrity/foreign-key checks fail, required schema missing, or actual migration table disagrees with manifest. Open untrusted staged DB without extensions or application writes. |
| `DatabaseAttachmentMismatch` | Distinct screenshot storage keys from staged DB do not exactly match screenshot inventory, or required bytes are missing. Shared keys count once. Intentional historical AI source absence and removed older Journal revisions are not attachment failures. |
| `SourceChanged` | A referenced attachment cannot be stably captured during packaging. Abort the whole attempt; never drop its row, substitute bytes or publish partial success. |
| `IoFailure`, `Cancelled` | Read/write/permission/disk-space/cancellation failure. Report safe actionable stage/code; preserve installed journal and do not publish a successful archive. |

The codes for content/container/database checks are **contracts for later milestones**, not checks claimed implemented in M16.1. A future validator must inspect **all** entries and verify actual bounded bytes/hashes, schema and relationships before producing a complete validation result. A manifest-only result must never be accepted at a restore boundary. Validation failure does not modify installed data; safe temporary staging cleanup is separate and must report cleanup failure honestly.

## Consistency plan for later archive creation

1. Explicit request only; acquire a cross-process lease for the data root's **attachment mutations** spanning screenshot add/store/commit, screenshot/Trade/bulk deletion and cleanup. Integrate this lease into all those writers before claiming complete backups. It is not implemented by M16.1. Ordinary unrelated Trade/Journal writes may continue subject to SQLite's own locking.
2. Use SQLite's supported online backup mechanism into a newly allocated private staging database, not `File.Copy(journal.db)`. Read-only source connections, bounded busy/deadline handling and cancellation must be designed/tested against WAL and rollback modes. Microsoft.Data.Sqlite's `BackupDatabase` wraps the backup mechanism but is synchronous and can block writers; later implementation must not claim that cancelling its caller instantly interrupts native work. Do not publish until successful completion and destination closure. See the [SQLite backup API](https://www.sqlite.org/backup.html) and [Microsoft.Data.Sqlite backup documentation](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/backup).
3. Read the migration catalog and screenshot keys **from that frozen staged database**, not a later query of live tables. Retain the attachment lease while staging every referenced file. Reject unsafe keys/reparse points; open stable read handles denying writes/deletion, stream/hash bounded bytes into staging and verify them. Screenshot files currently have no persisted content hashes, so pre-existing tampering cannot be attributed retrospectively; the archive records the stable bytes captured for each key. Missing, unstable, inaccessible or unexpected changed files block publication. A noncooperating external writer cannot be made safe by an in-process flag; detect/abort, never claim a guessed consistent result.
4. Release the attachment lease once required immutable bytes are staged; packaging uses only staging, so new imports, screenshots and deletions afterward cannot alter the archive's contents. Capture only validated typed appearance settings. Check integrity/FKs, migration history and exact screenshot key-set equality. Avoid treating intentionally dangling saved AI citations as database foreign keys.
5. Hash finalized staged payloads, emit bounded manifest, package to an exclusive temporary destination, verify the completed archive, then safely publish. No final successful filename before completion; failure/cancellation leaves no claimed successful backup. Clean only operation-owned temporary paths; report any failed cleanup without deleting unrelated files. Do not recursively enumerate secrets, logs or original import inputs.

## Restore/recovery design gate (not implemented)

Restore will first validate the **entire** archive into a private staging area, including bounded content, hashes, schema, attachments and any supported staged migration. This happens before stopping the live app or replacing anything. Then request explicit confirmation and quiesce **all** journal writers/readers and file handles across application instances, reject unsaved-edit vetoes, close database pools, and obtain exclusive data-root ownership. Source changes since preview require a fresh confirmation/check rather than racing replacement.

Before replacement, create and verify a recovery copy of the current database, screenshots and settings with their relationships intact. Preserve other pre-existing files/orphans in the local recovery directory as well; unlike portable scope, recovery must not lose existing files. Never include or overwrite the sibling credential store. If recovery capture/verification fails or disk space is insufficient, stop with the installed journal intact.

Database and screenshot directories cannot be replaced atomically as unrelated individual file moves. Later restore needs a durable recovery-intent record and recoverable same-volume directory-switch protocol, with explicit crash/failure injection tests and startup recovery. Retain old data until successful reopen/integrity verification; on any failed switch, recover the original complete set before normal use. Do not resume the app against a new DB and old attachments. No automatic deletion/retention of recovery copies is authorized by this contract. Restored AI analyses remain snapshots; restore must never trigger generation.

## Confidentiality and limits

Portable v1 is **not encrypted** and SHA-256 provides corruption detection, **not authenticity** against an attacker who can rewrite both manifest and payload. The archive is highly sensitive: Account names, trading history, notes, screenshot pixels, Journal text/answers/revisions, and AI evidence/responses (including previously deleted source text retained in snapshots) are present. Database free pages may retain deleted content; this is not a sanitized export or secure-erasure operation. Do not log payloads, raw names/paths, keys, JSON or source text. Handle archives/recovery copies as private user data and use trusted storage/transfer. Do not claim password protection or cloud security. Credentials are excluded even when encrypted by DPAPI; no app-owned key is distributed. Re-enter user-owned keys on another computer.

## Remaining M16 work and verification

Remaining work after M16.2, in dependency order: M16.3 attachment inventory/staging with writer coordination around the database snapshot; bounded archive writer/full verifier and safe diagnostics; staged compatibility migration; confirmed recovery-first restore with crash rollback; Desktop progress/cancellation/unsaved guards; isolated end-to-end acceptance including secrets exclusion, cross-file races, corruption/ZIP-bomb/path/link cases, disk-full and recovery interruption. Database snapshot tests are not acceptance of these remaining workflows.

M16.1 tests use only synthetic in-memory manifests: pinned JSON round-trip/strictness, schema lineage, UTC, hash syntax, empty-journal database requirement, unsafe/duplicate/excluded entries, size/count/path/overflow bounds and cancellation. They do not read a real database or create an archive. **37 focused cases passed; all 710 Application Release tests passed**, zero failures/skips. Solution Release build: **0 warnings/errors**. EF model consistency: no pending changes (design-time SQLite `:memory:` context). Tracked/new-file whitespace checks passed. Local logs/TRX are ignored under `artifacts/m161*`. The full multi-project suite was not rerun for these unconnected contracts; archive creation, capacity/performance, filesystem/SQLite snapshot races, actual restore, live UI and matching GitHub CI remain unverified by design.

Changed files: `README.md`, this document, Application `Backups/BackupArchiveContract.cs`, `Backups/BackupManifestValidator.cs`, Application tests `Backups/BackupManifestTests.cs`, and narrow source-only `.gitignore` exceptions for those folders (the existing Visual Studio `Backup*/` rule otherwise hides them). Baseline was clean, HEAD/branch unchanged; no commit, push or merge. No production journal, screenshots or credentials were accessed.
