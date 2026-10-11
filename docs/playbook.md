# Playbook — Domain, Application and persistence

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
