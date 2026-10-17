# Trading Plan

## M21.3 — persistence, migrations and application use cases

Implemented from clean `develop`, HEAD `6682482bcd68bd5fe0c2bdf66bf81468f67ba6c4`. No applicable `AGENTS.md` was present. Reuses M21.1/M21.2 identity, content bounds, readiness, checklist invalidation, frozen reference types, optimistic token, copy-forward and Domain transitions. No UI, navigation, automatic matching, Review/AI integration, market feed or existing economics changes.

### Storage and exact reconstruction

Additive migration `20261017152354_AddTradingPlans` creates:

- `TradingPlans`: one unique `(AccountId, NewYorkDate)`, stable GUID, long optimistic `Version`, UTC audit times, latest Ready number, optional working `DraftJson` and its `ReferenceStateJson` baseline. DateOnly is the explicit New York date, not converted from the save instant.
- `TradingPlanReadyRevisions`: composite `(PlanId, Revision)` primary key, UTC Ready time and self-contained `SnapshotJson`. The owner foreign key is restrictive; no Account/Instrument/Setup/Rule cascade can delete historical preparation. SQLite triggers reject Ready updates/deletes and nonconsecutive appends. No retention or deletion operation is introduced.

Draft and Ready storage envelopes are v1, separate from unrelated review/export versions. The focused mapper serializes existing content and snapshots, not an alternate authoring model. Decimals remain JSON numbers with exact decimal serialization, nullable values remain null, collection order/text/line breaks are preserved. Each payload has a 2,000,000-character limit and JSON depth 24; unknown envelope versions, missing required constructor fields, duplicate properties, malformed content, inconsistent identity/lifecycle/sequence/chronology and invalid reference baselines fail rather than being repaired. A payload exceeding the storage bound is a safe storage failure with no commit, not truncated content.

Domain reconstruction validates existing identity, UTC instants, Draft derivation, Ready readiness/reference membership, source-Draft version and aggregate state. It does not execute Edit/Ready, generate IDs/times, clear checkmarks, or look up live catalogs. Catalog currencies retain their existing up-to-eight-character contract; automatic Rule criteria retain their own narrower currency rules. `LoadAsync` uses one SQLite read transaction for owner, sequence and latest Ready. History reads are bounded to 1–100 rows: exact Account/date descending with an exclusive `beforeDate` cursor (date is unique within Account), and separate Ready history by revision descending with an exclusive `beforeRevision`. No write loads the entire history.

### Operations and transaction boundaries

`ITradingPlanStore` provides exact-scope load, exact Ready load, basic Plan/Ready history and `WriteAsync`; `WriteTradingPlanUseCase` is registered in existing persistence DI. Commands capture scope independently of future navigation and carry `TradingPlanExpectedState` for existing Plans. Results return the committed aggregate/version, validation issues or typed reference issues. Read cancellation/storage errors propagate; malformed persisted evidence uses sanitized `InvalidDataException`. Write failures return `Invalid`, `Conflict`, `TargetAlreadyExists`, `ReferenceUnavailable`, `ReferenceChanged`, `Cancelled` or `StorageFailure`, never raw source text or database diagnostics.

| Operation | Authoritative behavior |
| --- | --- |
| Create Draft | Revalidate exact active references, new identity/version 1; reset all incoming confirmations. Existing Account/date is not overwritten. |
| Save Draft | Apply existing content comparison/checklist invalidation. Incoming checkmarks are ignored; exact no-op content leaves the aggregate version unchanged. |
| ConfirmChecklist | Explicit separate operation; requires unchanged preparation and current token. A simultaneous prose edit is rejected. |
| Mark Ready | Requires exact stored Draft content, current version, complete readiness/checklist and fresh references. Append one revision and update current state atomically. |
| Start Draft | Read the selected immutable revision from this Plan; ignore caller replacement content. Retain all Ready revisions and reset confirmations. Current references must still validate. |
| RefreshReferences | Explicitly accept fresh reference interpretation, optionally update selected Setup/Rule tokens/content, clear all checks and advance the aggregate version. |
| Copy forward | Read the authoritative source Ready payload, ignore caller-provided replacement content, apply existing daily-context/checklist resets, revalidate target references and create a new target identity. Concurrent target creation yields one success and one `TargetAlreadyExists`. |

Every mutation obtains SQLite's immediate writer reservation **before** reading the Plan or references. Fresh version/identity checks, catalog reads, candidate Domain transitions, owner/revision writes and reconstruction readback share that transaction. Uniqueness and EF concurrency tokens are additional guards. A stale writer never overwrites a newer Draft. Mark Ready cannot append without the owner transition. Any failed write/readback or cancellation before commit rolls back. A final cancellation check precedes commit; commit uses `CancellationToken.None`, and cancellation arriving after a successful commit still returns success. SQLite's synchronous writer-reservation wait is not interruptible mid-call; it uses the existing provider busy timeout. No background operation is abandoned or automatically retried.

### Fresh references and retained history

Reference resolution uses exact IDs in bounded batched queries under the writer reservation. Account and Instruments must be active; Setup must be active with the selected current Playbook revision and Setup update token; Rule must be active/global with the exact latest revision. Missing/inactive/legacy-scoped references are explicitly unavailable. No matching by name/symbol is used.

The Draft baseline stores SHA-256 fingerprints of authoritative selected catalog records (not credentials). Surviving selected references must match it. This detects changed Account currency, Instrument tick interpretation, Setup guidance/version and Rule text/version even if a test updates fields without changing an audit timestamp. The policy is deliberately conservative: cosmetic labels/audit or other catalog record changes also require explicit `RefreshReferences`. New selections resolve fresh; intentionally removed selections no longer block saving. Refresh is not implicit during Save or Ready and clears every confirmation. Changed Setup/Rule revision tokens must first be explicitly reselected by the caller. Ready captures verified labels/guidance, never caller-supplied labels.

Ready reads use only their preserved payloads. Account/Instrument deletion or same-name replacement does not retarget them. A deleted Playbook revision is not needed for historical readability because its selected guidance is frozen; the existing catalog deletion/image policies are unchanged. Starting/copying into an active Draft may be blocked until unavailable/changed references are resolved. Ready numbers never rewind when drafting from an older revision. A saved plan still proves preparation, not Trade adherence.

### Migration and portability

The migration preserves existing rows and creates no historical Plan backfill. Complete backups retain both tables and triggers. Synthetic portability tests verify exact Draft-after-Ready and Ready payloads after backup, preflight, offline recovery-protected restore and fresh services. Portable JSON advances to **v10** with `tradingPlans` and `tradingPlanReadyRevisions`; serialized payload strings remain exact, and the four existing CSVs remain unchanged. Current-schema-only restore rejects pre-migration-21 archives pending the deferred staged upgrade path. [Confidentiality, backup validation boundary and compatibility](backup-restore.md#m213--trading-plan-compatibility).

### Verification — 17 October 2026

| Final local check | Observed result |
| --- | --- |
| Focused Trading Plan tests | **132 passed / 0 failed / 0 skipped**: Domain 77, Application 27, Infrastructure 28; 40 new cases |
| Complete Domain Release | **567 passed / 0 failed / 0 skipped** |
| Complete Application Release | **1,003 passed / 0 failed / 0 skipped** |
| Complete Infrastructure Release | **1,410 passed / 0 failed / 0 skipped**, 1 minute 57 seconds |
| Required three-project total | **2,980 passed / 0 failed / 0 skipped**, all commands exit 0 |
| Final Release solution build | **0 warnings / 0 errors**, 12.98 seconds |
| EF consistency | No model changes since the new migration; design-time factory uses in-memory SQLite |
| Whitespace / documentation | Tracked and new-file checks passed; **159 local links** resolve; documented 35-file inventory matches Git status |
| Desktop gate, renders, live interaction, matching CI | Not performed/claimed for this persistence slice; changes are uncommitted |

Initial focused testing found and corrected a malformed-payload exception escaping write-outcome translation; a trigger test's literal braces also needed a fixture-only SQL correction. The initial complete Infrastructure run reported **1,395 passed / 10 failed**: nine explicit old-schema expectations and one Windows symlink privilege failure. The schema expectations are updated, not removed; the final complete run uses the required Windows test privileges. Subsequent reconstruction hardening rejects nested missing/defaulted values and mismatched Ready ownership. Its initial owner-join projection failed EF translation; filtering/paging now occurs before that projection and the 28-case SQLite run passed. One overlapping test-build was stopped after DLL-copy contention; no test lifetime/deadline was changed. No deadline, test retry policy or assertion is weakened. Final TRX evidence is under ignored `artifacts/m21-3/final-focused/` and `artifacts/m21-3/final-regression/`.

Commands used (all paths below are synthetic output directories):

```powershell
dotnet test tests/PersonalTradingJournal.Domain.Tests/PersonalTradingJournal.Domain.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~TradingPlans --logger "trx;LogFileName=domain.trx" --results-directory artifacts/m21-3/final-focused
dotnet test tests/PersonalTradingJournal.Application.Tests/PersonalTradingJournal.Application.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~TradingPlans --logger "trx;LogFileName=application.trx" --results-directory artifacts/m21-3/final-focused
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests/PersonalTradingJournal.Infrastructure.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~TradingPlans --logger "trx;LogFileName=infra.trx" --results-directory artifacts/m21-3/final-focused
dotnet test tests/PersonalTradingJournal.Domain.Tests/PersonalTradingJournal.Domain.Tests.csproj -c Release --no-build --no-restore --logger "trx;LogFileName=domain.trx" --results-directory artifacts/m21-3/final-regression
dotnet test tests/PersonalTradingJournal.Application.Tests/PersonalTradingJournal.Application.Tests.csproj -c Release --no-build --no-restore --logger "trx;LogFileName=application.trx" --results-directory artifacts/m21-3/final-regression
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests/PersonalTradingJournal.Infrastructure.Tests.csproj -c Release --no-build --no-restore --logger "trx;LogFileName=infrastructure.trx" --results-directory artifacts/m21-3/final-regression
dotnet build PersonalTradingJournal.sln -c Release --no-restore
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure/PersonalTradingJournal.Infrastructure.csproj --context JournalDbContext --configuration Release --no-build
git diff --check
```

The Infrastructure complete run requires the normal Windows symlink privilege used by the existing backup safety fixture. A related 55-case schema/initialization/integrity/portability selection also passed before the final added owner-attribution test. Focused coverage includes all three lifecycle states, preserved checks, historical derivation, precision/nulls/Unicode, exact scopes, keyset paging, stale saves, concurrent Ready/copy targets, rollback after SQL writes, cancellation before and after commit, a competing reference writer blocked until commit, cosmetic/material changes, inactive/deleted/same-name sources, immutable SQL triggers, malformed reconstruction, migration and real backup/preflight/offline restore/export. These are isolated SQLite/service tests, not Desktop or production-journal actions.

Scope limits: no Session Brief/History UI, renders, live interaction, integrated Desktop gate, Trade matching, Daily Review integration or matching CI acceptance is claimed. No production journal or provider was accessed. Full M21 remains pending.

### Exact M21.3 changed files

Documentation:

- `README.md`
- `docs/architecture.md`
- `docs/trading-plan.md`
- `docs/backup-restore.md`

Domain/Application:

- `src/PersonalTradingJournal.Domain/TradingPlans/TradingPlan.cs`
- `src/PersonalTradingJournal.Domain/TradingPlans/TradingPlanReadyRevision.cs`
- `src/PersonalTradingJournal.Domain/TradingPlans/TradingPlanReferenceSnapshot.cs`
- `src/PersonalTradingJournal.Domain/TradingPlans/TradingPlanSnapshotReconstruction.cs`
- `src/PersonalTradingJournal.Application/TradingPlans/TradingPlanContracts.cs`
- `src/PersonalTradingJournal.Application/TradingPlans/ITradingPlanStore.cs`
- `src/PersonalTradingJournal.Application/Backups/IPortableExportService.cs`

Infrastructure:

- `src/PersonalTradingJournal.Infrastructure/TradingPlans/TradingPlanJson.cs`
- `src/PersonalTradingJournal.Infrastructure/TradingPlans/TradingPlanReferences.cs`
- `src/PersonalTradingJournal.Infrastructure/TradingPlans/TradingPlanStore.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Records/TradingPlanRecords.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Configurations/TradingPlanConfiguration.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/JournalDbContext.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261017152354_AddTradingPlans.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261017152354_AddTradingPlans.Designer.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/JournalDbContextModelSnapshot.cs`
- `src/PersonalTradingJournal.Infrastructure/Backups/PortableExportSchema.cs`

Tests:

- `tests/PersonalTradingJournal.Domain.Tests/TradingPlans/TradingPlanReconstructionTests.cs`
- `tests/PersonalTradingJournal.Application.Tests/TradingPlans/TradingPlanRevisionContractTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/TradingPlanPersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/TradingPlanPortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Migrations/InitialMigrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Initialization/JournalDatabaseInitializerTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integration/ProductionPersistenceIntegrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integrity/SqlitePersistenceIntegrityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookPersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/PortableExportTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/SavedPeriodPortabilityTests.cs`

The historical M21.1/M21.2 sections below retain their original checkpoint results and boundaries; the M21.3 section above supersedes their persistence-pending statements.

## M21.2 — preserved Ready revisions and working Drafts

Implemented on the existing M21.1 foundation, starting from clean `develop` at `317305e582036aa28ee9fd66d506bf9dcc6161f2` (`Define Trading Plan identity and readiness contracts`). No repository/ancestor `AGENTS.md` was present. The prior scope, exact account/date identity, immutable bounded content, readiness validation, checklist invalidation mapping, nullable decimals, New York defaults and optimistic token are reused. Missing aggregate transitions and reference captures are now implemented; persistence, migration, Desktop and review contracts are unchanged.

### Aggregate lifecycle and failure guarantees

`TradingPlan` derives from the existing `AuditableEntity`. Plan ID and `TradingPlanKey` never change. Its `Version` is the same long optimistic save token exposed by M21.1; no second concurrency token is introduced. Successful Domain mutations advance it, and exact no-op preparation/confirmation assignments do not. Application `TradingPlanLoadedState.FromPlan` adapts the actual aggregate to the existing `CheckTransition` contract.

| Domain state | Working Draft | Latest Ready |
| --- | --- | --- |
| `DraftOnly` | Initial preparation; confirmations start unset | Absent |
| `Ready` | Absent | Complete immutable confirmed revision |
| `DraftAfterReady` | New preparation with its exact source Ready revision ID | Previous latest Ready remains available |

These states are computed from aggregate data, not UI flags. They do not assert that an in-memory operation has been saved to disk.

- `EditDraft` compares preparation values, including nested collections, rather than immutable-array object identity. It invalidates affected existing confirmations using M21.1's policy. Incoming `Content.Confirmed` flags are ignored, including on a no-op assignment; they cannot confirm edited preparation. Confirmations can be changed only through explicit `SetConfirmations`.
- A new Plan and `StartDraft` always begin with no confirmations, even when their source content had all five. Confirmation alone does not waive missing content: `MarkReady` still runs `TradingPlanReadiness.Validate` and returns field-addressed issues through `TradingPlanNotReadyException` when incomplete.
- `MarkReady` requires a working Draft, the exact expected version, canonical nondecreasing UTC time and an exact matching reference capture for that Ready operation. It allocates `(PlanId, latest Ready number + 1)`, returns the complete immutable revision, retains it as latest and removes the working Draft. A repeated call cannot generate another revision without a valid Start Draft transition and reconfirmation.
- `StartDraft` requires Ready state and an exact historical Ready value from the same Plan/key/creation identity. It cannot overwrite a working Draft. Starting from revision 1 after revision 2 preserves revision 2 as latest; the next Ready is revision 3, with `DerivedFromRevision` identifying revision 1.
- Revision/content/reference objects expose no mutable setters. Input enumerables are bounded and copied; nested values are sealed immutable objects. Sharing immutable leaf values is safe. Replacing a working Draft or deriving a `with` copy of price levels cannot modify any previous Ready value.
- All validation, content construction and checked counter arithmetic finish before observable aggregate state changes. Rejected readiness, wrong source identity/version, inactive references, malformed timestamps, stale versions and repeated transitions retain the same Draft, latest Ready, version and audit timestamp. No speculative state is published on a failed operation.

The aggregate intentionally retains only its working Draft and latest Ready, rather than loading an unbounded history into every command. Each returned `TradingPlanReadyRevision` is independently immutable. M21.3 must append and retain **every** returned Ready value and load an older source revision by its exact identity when requested. Replacing the aggregate's latest reference never authorizes replacing or deleting a stored historical row. Domain instances have single-owner mutation semantics; concurrent database writers still require the planned transaction/version check.

### Self-contained snapshots

Each Ready revision contains the complete M21.1 content: selected session, Focus, Instrument contexts/bias/thesis/nullable levels, ordered scenarios/IF/THEN/invalidation and Setup selection tokens, stand-aside intent/notes, planned limits, Rule selections, manually maintained news/restrictions and completed checklist. It also contains stable Plan/revision ID, account/date key, source-Draft version, derivation identity and the timestamps below.

`TradingPlanReferenceSnapshot.Capture` accepts supplied canonical Account, Instrument, Setup/Playbook and Rule objects. It contains no service or storage lookup. Supplied references must exactly match selected IDs, with no missing, duplicate or unrelated entries. A Setup used in multiple scenarios is captured once; all its selection tokens must agree. Capture rejects inactive sources, changed selected revisions/Setup timestamps, legacy scoped Rules and reference timestamps after the capture time. Output reference order follows the preparation rather than catalog query order. A separate final validation checks that the capture still matches the Draft's links and exact Ready timestamp.

| Reference | Frozen context | Deliberately excluded |
| --- | --- | --- |
| Account | Exact ID, name, currency, source update UTC time | Starting balance, broker/external identity, credentials |
| Instrument | Exact ID, symbol, display name, currency, tick size, update UTC time | Live market values, executions, economic recalculation |
| Playbook Setup | Exact Setup ID, selected label/description, Setup update UTC time, exact Playbook revision/recorded time, its five guidance text sections | Example images, example annotations, filesystem paths, unrelated revisions |
| Global Rule | Exact Rule ID/revision/recorded UTC time and immutable definition: name, criterion, authored evidence description and any typed condition | Assessment history, automatic evaluation, unrelated Rules |

The Setup label is the supplied catalog label at capture; the guidance belongs to the exact selected Playbook revision. Linked Rule definitions retain their typed condition as context only; finalizing a Plan does not run it. All text is user-authored preparation, not proof of adherence. Planned monetary limits use the captured Account currency; Instrument price-level context retains its own currency/tick size. No conversion or zero substitution occurs.

Historical reading uses these frozen values even if catalogs change, archive or disappear. Any future “open source” navigation must resolve the stable ID and report unavailability separately; it must not replace snapshot labels/content or substitute a same-name entity. No screenshot ownership/retention rule changes: Plan snapshots do not own or require the excluded Playbook images.

### Chronology

`PlanCreatedAtUtc`, `DraftStartedAtUtc`, `DraftUpdatedAtUtc`, `ReadyAtUtc`, reference `CapturedAtUtc`, source update/revision times and `SourceDraftVersion` distinguish intended date from actual preparation chronology. Domain accepts explicit canonical UTC instants, rejects backwards audit transitions and never synthesizes Ready time from the New York date. Equal operation timestamps are permitted (revision/version order remains explicit). The caller must supply the actual clock instant; Domain cannot independently attest the operating-system clock.

Starting from history preserves that historical revision's original timestamps and creates new Draft timestamps. A Ready value's `DerivedFromRevision` records its source, including older revisions. The existing `TradingPlanDatePolicy.WasRecordedBeforeEntry` can consume `ReadyAtUtc`: strictly earlier establishes only record availability, equality/later/unknown does not. No Trade matching or Review classification is added.

### M21.3 implementation requirements

- Add validated reconstruction/serialization and an additive schema with exact account/date uniqueness. No persistence constructor, serializer, repository or migration is introduced here.
- Read the loaded Plan and selected reference states under the committing writer transaction; compare the existing loaded token before storing the resulting aggregate version. Domain mutations are candidates until commit, and their counters must not be reported as committed before readback.
- Revalidate Account currency/name/update and Instrument state against the user's selection, as well as selected Setup/Rule versions; snapshot creation proves only the supplied objects matched, not that a previously read catalog is still current in SQLite.
- Atomically persist the Draft/Ready transition and full Ready payload. Roll back on validation/conflict/cancellation/storage failure, preserve caller content, and return successful saved status only after commit/readback.
- Retain all Ready revisions independently of mutable catalogs and the aggregate's latest pointer. History/selected-source reads must be bounded and deterministic. Persist derivation and source-Draft version without rewinding Ready numbering.
- Preserve all new content in backup/restore/export and validate restored snapshot chronology/identity. Existing backup and export contracts are unchanged in M21.2 because no persisted data was added.

### Verification — 17 October 2026

| Check | Result |
| --- | --- |
| Focused Trading Plan Domain | **67 passed / 0 failed / 0 skipped** |
| Focused Trading Plan Application | **25 passed / 0 failed / 0 skipped** |
| New M21.2 cases | **45** (43 Domain, 2 Application); prior 47 remain green |
| Complete Domain Release | **557 passed / 0 failed / 0 skipped** |
| Complete Application Release | **1,001 passed / 0 failed / 0 skipped** |
| Final Release solution build | **0 warnings / 0 errors**, 7.63 seconds |
| EF model consistency | No changes since the last migration; design-time factory uses in-memory SQLite |
| Whitespace / documentation | `git diff --check` and new-file whitespace passed; **147** local links resolved |

Commands actually run:

```powershell
dotnet test tests/PersonalTradingJournal.Domain.Tests/PersonalTradingJournal.Domain.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~TradingPlans --logger "trx;LogFileName=domain.trx" --results-directory artifacts/m21-2/final-focused
dotnet test tests/PersonalTradingJournal.Application.Tests/PersonalTradingJournal.Application.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~TradingPlans --logger "trx;LogFileName=application.trx" --results-directory artifacts/m21-2/final-focused
dotnet test tests/PersonalTradingJournal.Domain.Tests/PersonalTradingJournal.Domain.Tests.csproj -c Release --no-build --no-restore --logger "trx;LogFileName=domain.trx" --results-directory artifacts/m21-2/regression
dotnet test tests/PersonalTradingJournal.Application.Tests/PersonalTradingJournal.Application.Tests.csproj -c Release --no-build --no-restore --logger "trx;LogFileName=application.trx" --results-directory artifacts/m21-2/regression
dotnet build PersonalTradingJournal.sln -c Release --no-restore
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure/PersonalTradingJournal.Infrastructure.csproj --context JournalDbContext --configuration Release --no-build
git diff --check
```

Coverage includes complete and stand-aside Ready transitions, incomplete readiness, structured checklist invalidation, exact no-ops, explicit confirmation, immutable nested inputs, multiple revisions derived from earlier history, UTC chronology, stale/repeated transitions, independent catalog edits/archiving, same-name replacements, missing/extra/duplicate references, selected revision mismatch and bounded capture. All test data is synthetic; no installed journal or provider was accessed. The complete Desktop/Infrastructure gate, persistence/restart, renders, live interaction, Review integration and matching CI are not claimed for this Domain-only slice. M21.2 is locally implemented; M21.3 remains next, and full M21 acceptance remains pending.

Exact M21.2 changed/new files (12):

- `README.md`
- `docs/architecture.md`
- `docs/trading-plan.md`
- `src/PersonalTradingJournal.Domain/TradingPlans/TradingPlanContent.cs`
- `src/PersonalTradingJournal.Domain/TradingPlans/TradingPlan.cs`
- `src/PersonalTradingJournal.Domain/TradingPlans/TradingPlanReadyRevision.cs`
- `src/PersonalTradingJournal.Domain/TradingPlans/TradingPlanReferenceSnapshot.cs`
- `src/PersonalTradingJournal.Application/TradingPlans/TradingPlanContracts.cs`
- `tests/PersonalTradingJournal.Domain.Tests/TradingPlans/TradingPlanFixture.cs`
- `tests/PersonalTradingJournal.Domain.Tests/TradingPlans/TradingPlanRevisionTests.cs`
- `tests/PersonalTradingJournal.Domain.Tests/TradingPlans/TradingPlanSnapshotTests.cs`
- `tests/PersonalTradingJournal.Application.Tests/TradingPlans/TradingPlanRevisionContractTests.cs`

## M21.1 — scope, identity and behavioral contracts

The first dependency slice establishes pure Domain/Application contracts. It does **not** yet replace the Desktop placeholder, write plans, create a migration, or add plan evidence to reviews. Existing Accounts, Instruments, Trading Setups/Playbooks and global Rules remain the only catalogs. The accepted M20 implementation and its separate Strategy scope decision are unchanged.

Repository inspection began on clean `develop`, `c76d819541e0b47341fc913518744c8c9410c7df` (`Merge pull request #23 from yovko93/develop`). That history includes `9eb002a` (the M20 coverage-sort correction). This is local merge-history evidence, not a fresh CI acceptance claim.

### Identity and time

- One logical Plan has a stable non-empty Plan ID and the unique key **(exact Account ID, New York calendar date)**. No All-accounts or null-scoped Plan exists. A same-name replacement Account is a different scope.
- Session is metadata: Asia, London, New York or Other. A Draft may leave it unselected. Changing session or adding Instruments cannot bypass account/date uniqueness.
- The default date comes from injected `TimeProvider.GetUtcNow()` through the existing `TradingTimePolicy.ConvertUtcToTradingTime`. A user-selected date is captured in the write command; saving does not recalculate today from the machine's timezone.
- Audit and Ready timestamps must be canonical UTC. Market/session labels do not redefine a trading day. Later Trade/Review membership continues to use the application's New York closure-date convention; a cross-midnight Trade is not counted twice.
- Ready time strictly before a Trade's entry time proves only that the record existed before entry. Equal/later timestamps, missing entry times and a backdated selected Plan date do not establish prior preparation. Association does not prove adherence.

### Draft and Ready lifecycle

| Operation | Contract |
| --- | --- |
| Create Draft | Exact account/date must be absent. Empty/incomplete content is allowed. New Plan identity and initial optimistic version; no Ready revision yet. |
| Save Draft | Require loaded Plan ID/version and Draft status. Preserve incomplete content. Advance the optimistic version only after a successful commit. |
| Mark Ready | Require Draft status, valid required content, all five explicit confirmations and revalidated references. Atomically append the next immutable Ready revision and advance the optimistic version. |
| Start Draft | Explicitly derive from an existing Ready revision of the same Plan; retain the latest Ready revision independently. Do not overwrite an already working Draft. |
| Copy forward | A new target account/date must be absent. An existing target is opened or reported as a conflict; never overwritten. Reset daily observations and every confirmation. |

The **save version** and **Ready revision number** are distinct. Ready revisions are addressed by `(PlanId, positive revision)` and allocated monotonically from the maximum ever issued; never reused, renumbered or edited in place. A Draft derived from an older Ready revision does not replace the latest Ready revision or rewind numbering. Starting a new Draft resets confirmations. Draft saves do not claim to be Ready history. Existing saved Reviews keep their exact captured Ready revision even after later Plan changes.

`TradingPlanWriteCommand.CheckTransition` is a pure preliminary decision: fresh state is required under the future SQLite writer reservation. A permitted transition is **not** a successful write. Content validation, reference validation, unique indexing, transaction rollback, cancellation boundaries, atomic append and verified readback are mandatory work for M21.2–M21.3, not guarantees implemented by this contract-only slice. An application message may say Saved only after committed readback. Exceptions, validation and conflicts must retain the editor's raw input and images.

Write outcomes distinguish Success, Invalid, Conflict, Target already exists, Reference unavailable, Reference changed, Cancelled and Storage failure. A stale or duplicate submission cannot silently edit Ready content. No provider calls, automatic trading checks or broker enforcement are introduced.

### Structured content and readiness

The bounded immutable authoring values preserve exact text, whitespace and decimal values. They defensively copy collections and reject duplicate Instrument, Scenario or Rule IDs. A Scenario must belong to an Instrument context included in that Plan. Scenario ordering follows the supplied collection. Its ID is local to its containing Plan; retaining an ID during copy-forward does not make two Plans share mutable content.

| Content | Draft | Ready |
| --- | --- | --- |
| Exact Account/date | Required identity | Required identity; available active Account checked at commit |
| Session and Today's Focus | May be empty | Selected session and meaningful focus required |
| Instrument contexts | May be empty | At least one context; exact existing active identities |
| Opportunity plan | May be incomplete | Bias and market thesis for every context, plus at least one complete scenario |
| Scenario | Name/direction/IF/THEN/INVALID IF may be incomplete | Every included scenario requires all five; Long/Short reuse `TradeDirection`; a second/opposite-direction scenario is optional |
| Stand-aside-only plan | May be incomplete | Explicit StandAside intent and reason; no trading scenarios required or allowed; no invented thesis, bias or levels required |
| Price levels | Optional | Optional; blank remains null, never zero |
| Risk limits | Optional | Optional; user explicitly confirms review of any unset limits |
| News/restricted-time notes | Optional manual text | Optional manual text; news checklist confirmation still required |
| Playbook and Rule links | Optional | Optional; all supplied links must validate at commit |
| Checklist | Any explicitly confirmed subset | All five checks required |

An opportunity plan may include stand-aside restrictions without changing its intent. A stand-aside-only plan cannot simultaneously advertise trading scenarios. A context can be watched without its own scenario, but an opportunity Plan must contain at least one scenario overall.

Bounds: 500 UTF-16 code units for Focus; 4,000 for each long-text field; 120 for a Scenario name; 12 Instrument contexts, 24 Scenarios and 50 linked Rules per Plan. Enumeration stops at limit + 1 and rejects excess rather than truncating. Optional levels are exact nullable decimals, including a deliberately entered zero or negative level; no instrument tick rounding, FX conversion or economic calculation occurs. Risk amounts and maximum Trades must be nonnegative; a specified consecutive-loss stop must be positive. Zero monetary risk or zero maximum Trades explicitly permits none; null means unset. Amounts will be labeled in the exact Account's captured currency, with no cross-currency total.

Malformed typed numeric text is not coerced into these Domain values. M21.4/Desktop must retain the raw text and show validation. Saving an incomplete Draft is different from accepting malformed values.

The five checks are user confirmations: context/levels, scenarios or stand-aside decision, risk limits, news calendar, mindset/focus. Populated fields never auto-confirm them. On an actual semantic edit, the working Draft invalidates:

| Changed content | Confirmation reset |
| --- | --- |
| Session or Instrument set | All five |
| Focus | Mindset/focus |
| Context, bias or levels | Context/levels and scenarios |
| Scenarios, linked Setup or stand-aside decision | Scenarios |
| Risk limits or linked Rules | Risk limits |
| News/restricted-time notes | News and scenarios |

Navigation, focus and an unchanged assignment are not semantic edits. The editor and committing use cases must apply this policy to the working Draft; earlier Ready confirmations remain immutable. Progress counts only explicit known flags; unknown flags/enums are rejected.

### References, deletion and history

Selection tokens reuse exact existing source IDs: a Playbook link retains its expected revision and Setup update timestamp; a Rule link retains its expected revision. These are **selection tokens, not historical captures**. M21.2 must freeze the referenced Setup name, relevant guidance and exact revision, Rule name/criterion and revision, Account name/currency, and Instrument labels in each Ready revision. No reference may be resolved by display name.

Fresh create/copy/Ready operations require an active Account and Instruments, active linked Playbook Setups at the selected revision, and active global Rules at the selected revision. Changed, inactive, unavailable and legacy-scoped references are explicit states, not silent substitutions. New Plan operations cannot broaden a legacy scoped Rule into a global one. A Draft with a now-unavailable optional link must retain its authored text, offer relink/remove, and explain why finalization is blocked. Existing Draft content may be saved without inventing replacement catalog content; the persisted availability warning must be resolved before Ready. An unavailable/inactive owning Account permits historical reading only until it is explicitly reactivated; a removed Account is never recreated automatically.

Frozen Ready content must remain readable after source edits/archiving/deletion. Future persistence must retain original identity/labels independently of mutable catalogs and must not cascade Plan history deletion. Plan links do not authorize deletion of shared images or alteration of existing Trade/Playbook retention policy. No screenshot attachments are added to Plans in this contract; there are no new image paths or binary transfers.

History is bounded to 1–100 entries per read (default 20), exact Account and inclusive selected date range, optionally Draft/Ready. Ordering is date descending, stable Plan ID, Ready revision descending. Later reads need a cursor carrying that full order and a separate representation for the working Draft. Historical Ready views are read-only and visually distinct; an explicit Start Draft is required to edit derived content.

### Copy forward

`TradingPlanContent.CopyForward` retains selected session, Focus, Instrument identities, scenario structure/order/text and selected Playbook tokens, planned limits and selected Rule tokens. It clears bias, market thesis, all six price levels, dated news/restriction notes and stand-aside notes, and resets all five confirmations. StandAside intent is retained but its daily reason must be re-entered. Retained scenario text may itself mention a daily condition; the user must review it before reconfirming. Copy never carries Ready status.

The future use case must load the exact authoritative source Ready revision, apply this reset, revalidate all references, and enforce target uniqueness transactionally. Caller-supplied content alone cannot prove a source revision or authorize overwriting the target. The pure method performs no catalog reads or writes.

### Synthetic NQ example

Account “Synthetic USD”, New York date 17 October 2026, New York session; Focus “Wait for confirmation. One setup at a time.” An NQ context has Conditional bias and a manually entered thesis. Previous Day High 20,000.25 is an illustrative nullable decimal, not downloaded market data. One Long Primary scenario says IF price reclaims that level, THEN wait for a retest, INVALID IF acceptance returns below it. A Playbook link may identify the existing Setup's exact revision; it is optional and does not assert compliance. Planned risk 50 USD/trade, daily limit 150 USD, maximum 3 Trades and stop after 2 losses are preparation, not enforcement. Every check is user-confirmed. Alternatively, StandAside with “Missing confirmation; no entries today” requires no invented opportunity or numerical limit.

### Verification and implementation sequence

M21.1 adds focused Domain/Application tests for exact identity, New York year/midnight/DST defaults, selected-date stability, incomplete Draft versus Ready, stand-aside, scenario ownership, bounded immutable inputs, decimals/nulls, checklist invalidation, copy resets, version/source tokens, transition conflicts, Ready protection, history bounds and before-entry timing. These are synthetic in-memory tests, not SQLite restart/UI acceptance.

Final local verification for this slice (17 October 2026):

| Check | Observed result |
| --- | --- |
| New Domain/Application contract cases | 47 passed: 24 Domain and 23 Application, included in the complete project runs below |
| Complete Domain Release tests | **514 passed / 0 failed / 0 skipped** |
| Complete Application Release tests | **999 passed / 0 failed / 0 skipped** |
| Release solution build | **0 warnings / 0 errors**, 26.09 seconds |
| EF model consistency | No changes since the last migration |
| Whitespace and documentation | Tracked and new-file whitespace passed; 145 local links resolved across the three changed documents |
| Complete Desktop/Infrastructure Release gate | Not run for this contract-only slice; required again after M21 integration |
| UI renders, restart, live interaction, matching CI | Not applicable to this contract-only implementation / remain pending for integrated M21 |

Evidence: `artifacts/m21-1/regression/domain.trx` and `application.trx`. Initial focused runs covered 24 Domain and 21 Application cases; two additional transition cases were then added and passed in the complete Application run. Two initial test-fixture assertions were corrected (a reusable fixture intentionally contained a Rule link; immutable-array value comparison required sequence comparison); the final complete runs above have no failures. No old M20 test total is reused as M21 evidence. All nine files remain uncommitted, so no matching CI run exists for this change set.

| Slice | State |
| --- | --- |
| M21.1 Scope/identity/behavior | Implemented and locally verified as above |
| M21.2 Preserved Ready revisions | Implemented in Domain; see the current M21.2 section above |
| M21.3 Persistence | Pending additive migration, transactions, readback, cancellation, DI, backup/export coverage |
| M21.4–M21.6 Preparation/scenarios/risk | Authoring value/readiness contracts defined; Desktop workflows pending |
| M21.7 Session Brief | Pending full-width three-section WPF workspace |
| M21.8 History/copy/navigation | Policies defined; durable operations and guarded UI pending |
| M21.9 Daily Review/AI evidence | Pending versioned frozen revision integration; existing reviews unchanged |
| M21.10 Acceptance | Pending integrated gate, renders, restart and live interaction |

M21 is **not complete** at this checkpoint. No Trading Plan persistence, visual/live acceptance, matching CI or new full-gate result is implied by the first contract slice.

Exact M21.1 changed/new files:

- `README.md`
- `docs/architecture.md`
- `docs/trading-plan.md`
- `src/PersonalTradingJournal.Domain/TradingPlans/TradingPlanIdentity.cs`
- `src/PersonalTradingJournal.Domain/TradingPlans/TradingPlanContent.cs`
- `src/PersonalTradingJournal.Domain/TradingPlans/TradingPlanReadiness.cs`
- `src/PersonalTradingJournal.Application/TradingPlans/TradingPlanContracts.cs`
- `tests/PersonalTradingJournal.Domain.Tests/TradingPlans/TradingPlanContractTests.cs`
- `tests/PersonalTradingJournal.Application.Tests/TradingPlans/TradingPlanContractTests.cs`
