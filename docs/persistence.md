# Persistence

## Scope

Personal Trading Journal uses EF Core 10 with SQLite for its current local-first persistence implementation. Persistence stores the approved domain facts, preserves exact authoritative values, applies schema changes through migrations, and keeps the Domain independent from EF Core.

Narrow Application persistence boundaries now support the concrete Account, Instrument, and manual Trade creation workflows. Persistence still does not provide generic repositories, authoritative Trade list/detail queries, Trade edit/delete, seed data, physical screenshot storage, backup/restore, analytics read models, or cloud synchronization.

## Persistence Architecture

```text
Domain entities and value objects
          ↑ explicit ToDomain / Rehydrate(...)
Infrastructure persistence records
          ↓ IEntityTypeConfiguration
JournalDbContext / EF Core
          ↓
SQLite journal.db
```

EF Core adapts to the Domain. It does not materialize Domain entities directly, and Domain has no EF attributes or package dependency. Mutable persistence records and their configurations are owned by Infrastructure. Explicit mappers translate in both directions; AutoMapper is not used.

The repository deliberately has no generic repository, Unit of Work abstraction, or speculative persistence interface. Concrete workflows use narrow Application boundaries such as `ITradingAccountStore`, `IInstrumentStore`, and `ITradeStore`, implemented in Infrastructure. Desktop feature code uses these boundaries through Application use cases and does not access `JournalDbContext` directly.

## JournalDbContext

`JournalDbContext` exposes one `DbSet` for each current persistence record and applies nine explicit `IEntityTypeConfiguration` implementations. `AddPersistence(...)` configures SQLite from `IApplicationPaths.DatabasePath`, enables SQLite foreign-key enforcement, registers `IDbContextFactory<JournalDbContext>`, and registers the runtime initializer.

Contexts are short-lived: create one from the factory for an operation, save or query, and dispose it. The desktop application does not retain a long-lived context.

## Persistence Records

Infrastructure defines one persistence record for each current entity concept:

- `InstrumentRecord`
- `TradingAccountRecord`
- `StrategyRecord`
- `TradingSetupRecord`
- `TradingMistakeRecord`
- `TradeRecord`
- `TradeExecutionRecord`
- `TradeScreenshotRecord`
- `TradeMistakeRecord`

The records carry persistence state without changing Domain encapsulation. Relationships are configured explicitly and do not rely on CLR navigation properties or generate implicit join entities.

Every application entity has an application/domain-generated `Guid` primary key. Configurations use `ValueGeneratedNever`, so SQLite does not assign identities, use autoincrement semantics, or provide database-generated Guid defaults. The current SQLite provider representation stores these IDs as `TEXT`.

## Domain Mapping

Each record type has a corresponding explicit persistence mapper. `ToRecord(...)` captures authoritative Domain state, while `ToDomain(...)` calls the entity's `Rehydrate(...)` API. Trade rehydration maps execution records mechanically and passes them to
Trade.Rehydrate(...). The Domain owns execution ordering and aggregate validation before reconstructing the trade lifecycle.

Inactive reference records are still mapped and queryable. `IsActive` controls lifecycle and future selection behavior; it is not a historical-visibility filter. There are no global `IsActive` query filters.

## Database Schema

The database contains exactly nine application tables:

1. `Instruments`
2. `TradingAccounts`
3. `Strategies`
4. `TradingSetups`
5. `TradingMistakes`
6. `Trades`
7. `TradeExecutions`
8. `TradeScreenshots`
9. `TradeMistakes`

EF manages `__EFMigrationsHistory`. The SQLite provider may also create `__EFMigrationsLock` to coordinate migration execution; it is provider infrastructure, not an application table or Domain concept.

### Referential Integrity

| Dependent column | Principal | Required | Delete behavior |
|---|---|---:|---|
| `Trades.TradingAccountId` | `TradingAccounts.Id` | Yes | Restrict |
| `Trades.InstrumentId` | `Instruments.Id` | Yes | Restrict |
| `Trades.StrategyId` | `Strategies.Id` | No | Restrict |
| `Trades.TradingSetupId` | `TradingSetups.Id` | No | Restrict |
| `TradeExecutions.TradeId` | `Trades.Id` | Yes | Cascade |
| `TradeScreenshots.TradeId` | `Trades.Id` | Yes | Restrict |
| `TradeMistakes.TradeId` | `Trades.Id` | Yes | Restrict |
| `TradeMistakes.TradingMistakeId` | `TradingMistakes.Id` | Yes | Restrict |

`TradeExecution` is an aggregate-owned factual child, so it is the only cascading relationship. Screenshot records use restrict because their external-file lifecycle must not be silently implied by a database cascade. Trade-mistake associations are historical process-quality evidence, and reference records are protected so historical trades remain rehydratable.

Two composite business indexes are unique:

- `UNIQUE (TradeId, Sequence)` on `TradeExecutions`
- `UNIQUE (TradeId, TradingMistakeId)` on `TradeMistakes`

The schema does not currently define uniqueness for instrument symbols, strategy names, setup names, mistake names, account names, storage keys, or external execution identifiers. EF also creates ordinary non-unique indexes for foreign keys where appropriate.

## Trade Source of Truth

`Trades` persists authoritative root facts: its identity, `TradingAccountId`, `InstrumentId`, historical `PricingPointValue`, `PricingCurrency`, optional `StrategyId`, optional `TradingSetupId`, and audit timestamps. The historical pricing snapshot is independent from current instrument economics.

The following values are deliberately not persisted because ordered executions and the pricing snapshot derive them:

- direction and status;
- open quantity;
- opened and closed timestamps;
- average entry and exit prices;
- total costs;
- gross P&L; and
- net P&L.

Production-wired integration tests persist a complete closed trade and prove exact reconstruction of these economics through a fresh context.

### Manual Trade Aggregate Persistence

M6 Manual Trade Entry uses the existing M3 Trade/Execution schema and migration; it introduces no schema change. The aggregate write path is:

```text
ITradeStore.AddAsync
  -> TradeStore
  -> TradePersistenceMapper + TradeExecutionPersistenceMapper
  -> one TradeRecord + all TradeExecutionRecords
  -> one SaveChangesAsync
  -> SQLite
```

`TradeStore` creates a fresh `JournalDbContext` for each write, maps the authoritative Domain aggregate, tracks its root and all executions, and saves them together. EF Core's transactional `SaveChangesAsync` supplies atomicity; there is no custom transaction wrapper or Unit of Work.

The store persists the Trade's existing `PricingPointValue` and `PricingCurrency`. It does not reload current Instrument economics. `CreateManualTradeUseCase` creates that snapshot from the authoritative Domain Instrument's `PointValue` and `Currency`, so historical Trade economics remain stable after later Instrument metadata changes.

`TradeScreenshot` rows contain metadata and an opaque `StorageKey`; they do not contain image bytes or assert a filesystem layout. `TradeMistake` rows associate historical process evidence with a trade and do not infer severity, cost, or P&L impact.

## Decimal Semantics

Domain types and persistence records use `System.Decimal`. Current decimal columns use SQLite `TEXT` storage through EF Core's provider mapping. Tests prove exact round-trip behavior for authoritative decimal facts, including fractional quantities, prices, commissions, fees, account balances, instrument values, and historical trade pricing.

This representation is not a native fixed-decimal SQL type, and M3 does not define general database-side `SUM`, `AVG`, numeric ordering, or numeric comparison as an analytics contract. Exact authoritative persistence is the current priority. Future analytics or read models may require deliberate query and storage design.

## Timestamp Semantics

Domain and persistence-record properties use `DateTimeOffset` or nullable `DateTimeOffset`. Their invariant is an explicit zero UTC offset. At the SQLite boundary, `SqliteUtcDateTimeOffsetConverter` maps them to UTC `DateTime`, which the provider stores as `TEXT`.

The converter rejects non-zero offsets rather than silently normalizing them, preserves exact ticks, and reconstructs a zero-offset `DateTimeOffset`. It exists because direct SQLite `DateTimeOffset` comparison and ordering support is insufficient for PTJ's query requirements.

Tests verify exact equality, ascending and descending ordering, inclusive `>=`/`<=` ranges, tick-level distinctions, and nullable screenshot capture timestamps.

## Migrations

The current and only application migration is:

```text
20260908122839_InitialCreate
```

It creates the nine application tables, eight foreign keys, ordinary FK indexes, and two deliberate composite unique indexes without seed data. Production schema management uses migrations and must not use `EnsureCreated`. Some focused test-only model characterizations use `EnsureCreated`, but migrated-schema tests and runtime initialization use the migration lifecycle.

## Runtime Initialization

`JournalDatabaseInitializer` depends on `IDbContextFactory<JournalDbContext>`. Its `InitializeAsync(...)` method creates a short-lived context with `CreateDbContextAsync(...)`, calls `Database.MigrateAsync(...)`, and disposes the context. The supplied cancellation token is propagated to both asynchronous operations.

Desktop startup follows this order:

```text
start Generic Host
  -> resolve JournalDatabaseInitializer
  -> await database initialization
  -> resolve and show MainWindow
```

Initialization creates a missing `journal.db`, applies pending migrations, and is idempotent on later starts. There is no seed data, destructive recovery, automatic database deletion, fallback to `EnsureCreated`, custom retry policy, or application-owned migration lock. Migration exceptions propagate, are logged by the startup failure handler, and prevent the main window from being shown.

## Design-Time Tooling

`JournalDbContextDesignTimeFactory` is isolated from runtime storage. It uses SQLite `Data Source=:memory:` with foreign keys enabled and exists only to construct the model for EF tooling. It does not resolve `LocalApplicationPaths`, access the user's `journal.db`, start WPF, create a physical database, or apply migrations.

The project and `dotnet-ef` tooling currently use the EF Core 10.0.11 line. Keep the tool aligned with the project's EF Core major/version line.

## Testing Strategy

Infrastructure tests cover:

- explicit mapper validation and round-trips;
- real SQLite record and aggregate round-trips;
- decimal precision characterization;
- UTC timestamp persistence and server-side queries;
- foreign-key, delete-behavior, and unique-index integrity;
- initial migration metadata and migrated-schema behavior;
- runtime initializer creation, idempotency, and cancellation; and
- `TradeStore`, `ManualTradeReferenceDataReader`, and fresh-read behavior;
- rollback of a new Trade root when an execution insert fails; and
- production-wired `CreateManualTradeUseCase`, full-graph, and transaction-atomicity scenarios.

Together they prove fresh database migration, fresh-context visibility, full graph rehydration, exact trade economics, historical pricing independence, inactive historical references, opaque storage-key preservation, atomic rollback after relational failure, and continued database usability afterward. Physical test databases live in unique temporary directories and are disposed and removed after each scenario.

## Developer Migration Workflow

Run EF commands from the repository root against the Infrastructure project and `JournalDbContext`:

```powershell
dotnet ef migrations list `
  --project src/PersonalTradingJournal.Infrastructure/PersonalTradingJournal.Infrastructure.csproj `
  --context JournalDbContext

dotnet ef migrations has-pending-model-changes `
  --project src/PersonalTradingJournal.Infrastructure/PersonalTradingJournal.Infrastructure.csproj `
  --context JournalDbContext
```

For a future approved schema change:

```powershell
dotnet ef migrations add <MigrationName> `
  --project src/PersonalTradingJournal.Infrastructure/PersonalTradingJournal.Infrastructure.csproj `
  --context JournalDbContext `
  --output-dir Persistence/Migrations
```

Migration generation must continue to use the design-time factory rather than `LocalApplicationPaths`, Desktop startup, or the real user database. Do not treat `dotnet ef database update` against `%LocalAppData%\PersonalTradingJournal\journal.db` as a normal development workflow; application startup applies migrations.

Before accepting a migration:

1. Review the generated migration and model snapshot.
2. Run the migrated-schema tests.
3. Run `has-pending-model-changes`.
4. Run the full restore, Release build, and test pipeline.

## Deliberately Deferred Concerns

The persistence foundation does not yet include:

- an authoritative Trade browsing/detail read model;
- Trade edit/delete workflows or imports;
- physical screenshot file persistence;
- backup and restore;
- seed/reference-data provisioning;
- analytics-specific database queries or read models;
- WAL or busy-timeout customization;
- custom retry or concurrency layers;
- authentication, multi-user tenancy, or cloud synchronization; or
- an alternative server persistence provider.
