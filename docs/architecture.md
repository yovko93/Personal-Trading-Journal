# Architecture

## Architectural Goals

The architecture is designed for long-term maintainability, testability, correctness, clear dependency boundaries, extensibility, local-first desktop operation, and possible future web or SaaS evolution.

Architectural decisions should avoid both short-term coupling and unnecessary enterprise-style abstractions. The repository favors explicit boundaries that provide current value without introducing distributed-system or framework complexity prematurely.

## Solution Layers

### PersonalTradingJournal.Domain

The Domain project contains the core in-memory trading model and its invariants. M2 organizes it into these areas:

- **Common** — stable `Guid` entity identity and UTC audit lifecycle primitives;
- **Instruments** — canonical instrument reference data and pricing characteristics;
- **Accounts** — stable trading-account identity and starting-balance reference data;
- **Trades** — immutable executions, the flat-to-flat `Trade` aggregate, historical pricing snapshots, and closed-trade economics;
- **Setups** — reusable specific market configurations and the sole primary trade-pattern classification;
- **Screenshots** — storage-agnostic screenshot metadata associated with trades; and
- **Mistakes** — user-defined mistake definitions and their associations with trades.

Execution history and market facts are kept distinct from review classifications. Detailed behavior is documented in [Domain Model](domain-model.md).

Rules:

- no WPF dependency;
- no Entity Framework Core dependency;
- no dependency on Application, Infrastructure, Desktop, or Contracts;
- no logging, broker, or market-data SDK dependency; and
- no dependency on filesystem or database implementations.

### PersonalTradingJournal.Application

The Application project is the application and use-case layer. It orchestrates Domain construction and lifecycle behavior through narrow external boundaries while remaining independent of WPF, EF Core, and Infrastructure.

Current feature boundaries are:

- **Accounts** — `ITradingAccountReader`, `ITradingAccountStore`, `CreateTradingAccountUseCase`, and `TradingAccountLifecycleUseCase`;
- **Instruments** — `IInstrumentReader`, `IInstrumentStore`, `CreateInstrumentUseCase`, and `InstrumentLifecycleUseCase`;
- **Setups** — Trading Setup catalog reads, create/lifecycle use cases, duplicate-name checks, and aggregate persistence;
- **Mistakes** — Trading Mistake catalog reads and writes plus Trade Mistake assignment, removal, and Trade-scoped projections;
- **Trades** — manual create/close workflows, optional Trading Setup validation and mutation, narrow aggregate-write boundaries, selector projections, bounded list reads, and complete one-Trade detail reads;
- **Screenshots** — purpose-specific boundaries and use cases coordinate Trade existence checks, binary storage, metadata persistence, ordered metadata reads, content retrieval, and deletion without exposing provider details to presentation; and
- **Storage** — `IApplicationPaths`, which exposes required storage locations without knowing how Windows resolves them.

Readers return presentation-oriented query projections and are optimized for their specific read use. Stores expose only the aggregate persistence operations required by write use cases. Create and lifecycle use cases construct or mutate Domain aggregates and coordinate persistence. These are explicit feature boundaries, not a generic repository pattern.

### PersonalTradingJournal.Infrastructure

The Infrastructure project implements Application abstractions and owns external technical concerns. Its current responsibilities include:

- `LocalApplicationPaths` and local directory creation;
- `JournalDbContext` and EF Core SQLite registration;
- Infrastructure-owned persistence records and `IEntityTypeConfiguration` mappings;
- explicit persistence-record/Domain mappers;
- Application reader and store implementations for Accounts and Instruments;
- reader/store implementations for Trading Setup and Trading Mistake catalogs and Trade Mistake associations;
- `TradeStore`, `TradeMutationStore`, and `ManualTradeReferenceDataReader` for manual Trade creation, closure, and Setup classification;
- `TradeListReader` and `TradeDetailReader` for authoritative Trade browsing;
- local screenshot file storage and SQLite screenshot metadata/read/delete implementations;
- the SQLite UTC timestamp converter;
- EF Core migrations and the design-time context factory; and
- `JournalDatabaseInitializer` for runtime migration application.

Future imports and alternative persistence providers also belong at this boundary when concrete use cases require them.

### PersonalTradingJournal.Contracts

The Contracts project is reserved for stable DTOs or contracts that may later be shared across presentation or API boundaries. It does not currently define application contracts.

### PersonalTradingJournal.Desktop

The Desktop project contains the WPF presentation layer and the application composition root. It owns:

- WPF Views and presentation ViewModels;
- data-backed Accounts, Instruments, and Trades feature workflows;
- data-backed Trading Setup and Trading Mistake catalog workflows;
- manual Trade-entry state, deterministic raw-input parsing and validation, and submission through the Application use case;
- authoritative Recent Trades, Trade Detail, and ordered execution-lifecycle presentation;
- optional Setup selection during entry, Setup assignment/change/clear, and Trading Mistake assignment/removal in Trade Detail;
- screenshot file selection, metadata presentation, preview decoding, delete confirmation, and operation state;
- the permanent shell and its navigation state;
- implicit `DataTemplate` ViewModel-to-View resolution;
- reusable XAML design resources;
- Generic Host lifecycle management and dependency injection composition;
- logging composition; and
- Windows application startup and shutdown.

Trading and domain logic must not accumulate in this project. Practical guidance for extending this layer is documented in [Desktop UI Architecture](desktop-ui.md).

## Dependency Direction

The current source-project references are:

```text
Desktop
   |
   +----> Application ----> Domain
   |
   +----> Infrastructure --> Application
   |           |
   |           +-----------> Domain
   |
   +----> Contracts
```

- Domain has no project dependencies.
- Application depends on Domain.
- Infrastructure depends on Application and Domain.
- Desktop composes Application, Infrastructure, and Contracts.
- Contracts currently has no project dependencies.

These dependencies are intentional and should not be reversed casually. They keep framework and implementation details outside the core layers.

## Desktop Presentation Architecture

`MainWindow` is the permanent application shell. Its XAML owns the sidebar, page header, and content region. Its code-behind is intentionally limited to constructor injection, `InitializeComponent()`, and assigning the injected `MainWindowViewModel` as its `DataContext`; business logic and navigation routing do not belong there.

The shell follows this presentation flow:

```text
MainWindow
  -> DataContext: MainWindowViewModel
       -> CurrentDestination
       -> NavigateCommand
       -> PageTitle
       -> CurrentContentViewModel
            -> ContentControl
                 -> implicit DataTemplate
                      -> View
```

The ViewModels use `CommunityToolkit.Mvvm`: `ObservableObject` supplies change notification and `RelayCommand<NavigationDestination>` implements the shell command. `NavigationDestination` is Desktop-only presentation state, is not persisted, and has no domain meaning. `CurrentDestination` remains authoritative routing state; `MainWindowViewModel` synchronizes it to exactly one selected item in a deterministic 19-destination catalog.

That catalog separates top-level destinations, four collapsible feature sections, and lower utility destinations. Section expansion is session-local presentation state and does not enter Application or Domain. `MainWindow.xaml` renders shared data templates, while project-owned vector geometries are centralized in `Resources/Icons.xaml`; this removes repeated route Button markup without introducing a navigation service or external icon dependency.

There is intentionally no `NavigationService` or `INavigationService`. `MainWindowViewModel` is currently the only component that initiates shell navigation, so another abstraction would be premature. A navigation service should be considered only when another ViewModel has a demonstrated need to initiate cross-feature navigation.

`MainWindow` does not construct feature Views. Its `ContentControl` presents `CurrentContentViewModel`, and implicit `DataTemplate` mappings in `App.xaml` resolve:

```text
DashboardViewModel   -> DashboardView
TradesViewModel      -> TradesView
AccountsViewModel    -> AccountsView
InstrumentsViewModel -> InstrumentsView
TradingSetupsViewModel -> TradingSetupsView
TradingMistakesViewModel -> TradingMistakesView
SettingsViewModel        -> SettingsView
PlaceholderViewModel -> PlaceholderView
```

There are 19 destinations: seven concrete destinations—Dashboard, Trades, Accounts, Instruments, Setups, Mistakes, and Settings—and 12 placeholders that share the placeholder mapping instead of carrying empty View/ViewModel pairs. A placeholder should be replaced only when its feature gains real presentation state and Application workflows.

`MainWindowViewModel` retains its injected Dashboard, Trades, Accounts, Instruments, Trading Setups, Trading Mistakes, and Settings ViewModels for the lifetime of the main window. Returning to a concrete feature therefore preserves its established ViewModel state. Returning to Trades preserves its draft, successfully cached reference and list data, and selected Trade Detail state without repeating successful reads. The draft remains until Cancel or a successful Save; navigation itself does not reset Trade facts. This remains direct typed shell state; no `NavigationService` exists.

### Desktop Theme System

`AppTheme`, `IThemeService`, Windows theme detection, theme selection, and JSON preference persistence are Desktop concerns; they do not enter Domain or trading Application workflows. `PreferredTheme` records the user's System/Dark/Light choice, while `EffectiveTheme` is always the concrete Dark or Light appearance. System resolves `AppsUseLightTheme` from the current-user Windows Personalize registry key and safely falls back to Dark when detection fails. Supported Windows preference changes are observed while System is preferred; explicit Dark or Light ignores them.

Shared typography, layout, and control styles remain single-source resources. `DarkTheme.xaml` and `LightTheme.xaml` contain the same semantic color and brush keys, and `ThemeService` replaces exactly one active theme dictionary while preserving merged shared dictionaries and their ordering. System is a preference and therefore has no resource dictionary of its own.

Theme-sensitive brush consumers use `DynamicResource`, allowing materialized controls and pages to update without rebuilding ViewModels or restarting. Immutable style, typography, spacing, and converter references remain `StaticResource`. Resource tests validate Dark/Light key parity and both static and dynamic project-owned references.

`JsonDesktopSettingsStore` persists only the preferred theme to the configured `IApplicationPaths.SettingsPath`; a System preference remains System even when its effective appearance is Dark or Light. Missing, malformed, or unknown settings fall back to System and are logged; writes use a temporary file followed by replacement. Settings applies a choice immediately and reports a safe error if persistence fails without reverting the usable visual theme. The header's two-state quick toggle reflects `EffectiveTheme` and creates an explicit opposite Dark/Light preference; Settings remains the route back to System.

### Desktop Dialog Boundary

`IDialogService` is a narrow Desktop-only abstraction for PTJ-styled confirmation and information dialogs. Callers provide small presentation request models containing contextual text and destructive intent; they do not provide arbitrary controls, domain policy, or modal routing metadata. `WpfDialogService` owns window creation and owner selection, and the composition root supplies it through dependency injection. This boundary lets feature ViewModels and adapters request confirmation without calling `MessageBox` or constructing WPF windows, while leaving entity-specific update, delete, reference-checking, and file-lifecycle rules in their proper future Application and Infrastructure workflows.

The Dashboard is currently a presentation shell. It provides neutral metric and panel surfaces but performs no analytics or database queries. Financial outcome must not be interpreted as process quality: good process can lose, and bad process can profit. Future process-quality analysis must model that distinction explicitly.

### Desktop Data-Access Boundary

Desktop may depend on Application abstractions and use cases. Its reference to Infrastructure exists because Desktop is the composition root that wires concrete implementations; it does not authorize feature ViewModels to query `JournalDbContext` directly. Accounts, Instruments, Trading Setups, Trading Mistakes, and Trades follow meaningful Application boundaries rather than a `ViewModel -> JournalDbContext` dependency.

Desktop objects use constructor injection. ViewModels must not locate dependencies through `IServiceProvider` or another service-locator pattern.

### Feature Read and Write Flows

Accounts, Instruments, Trading Setups, and Trading Mistakes use separate, purpose-specific read and write paths.

Read path:

```text
Desktop ViewModel
  -> Application reader abstraction
  -> Infrastructure reader
  -> fresh JournalDbContext
  -> SQLite
  -> Application list-item projection
```

Write path:

```text
Desktop ViewModel
  -> Application use case
  -> Domain aggregate
  -> Application store abstraction
  -> Infrastructure store
  -> SQLite
```

After a successful catalog write, Desktop performs an authoritative reload through the corresponding reader. It does not manufacture an authoritative persisted projection locally.

Write failures produce operation-specific create or lifecycle errors. If persistence succeeds but the projection reload fails, the mutation remains successful, the visible list may temporarily be stale, and Desktop presents a list-level refresh warning so the user can retry Refresh. This distinction avoids falsely reporting that a successful create or status change failed.

Trades use separate purpose-specific write, list-read, and detail-read paths. Manual creation follows:

```text
TradesViewModel
  -> TryBuildManualTradeCommand
  -> CreateManualTradeUseCase
  -> TradingAccount and Instrument aggregate lookup
  -> TradePricingSnapshot
  -> Trade and TradeExecution Domain APIs
  -> ITradeStore
  -> TradeStore
  -> SQLite
```

The use case reloads the Account and Instrument aggregates rather than treating Desktop selector metadata as persistence authority. After loading the Instrument, it applies the Domain `TradeQuantityPolicy` using the authoritative `Instrument.AssetClass`: Futures quantities must be positive whole contract counts, while other asset classes retain positive decimal quantities. The rule is asset-class-based rather than symbol-based. `TradeExecution` remains market-agnostic and decimal so historical fractional records can still rehydrate and future Equity, Forex, Crypto, Option, and Other workflows can retain fractional quantities. SQLite therefore continues to persist quantity as decimal with no schema change.

The creation use case snapshots `Instrument.PointValue` and `Instrument.Currency`, so historical economics do not change with later Instrument edits. Contract economics continue to derive from Instrument tick size and tick value; symbols provide display identity only.

An open manually entered Trade can later be fully closed from Trade Detail:

```text
TradesViewModel
  -> CloseManualTradeUseCase
  -> ITradeMutationStore.GetByIdAsync
  -> authoritative Trade reconstruction
  -> opposite-side TradeExecution for Trade.OpenQuantity
  -> Trade.AddExecution
  -> ITradeMutationStore.SaveAsync
  -> one SQLite SaveChanges
  -> authoritative Trade Detail and Recent Trades reload
```

The close command deliberately contains no quantity. The use case closes the aggregate's exact remaining `OpenQuantity`, and Domain execution rules remain authoritative for sequence, chronology, status, closure time, and P&L. Desktop does not fabricate the closed projection; post-commit detail and list reloads use non-cancellable tokens so a committed close is not reclassified as cancellation. The screenshot list is unaffected by the close.

The bounded list path is:

```text
TradesViewModel
  -> ITradeListReader
  -> TradeListReader
  -> SQLite
  -> TradePersistenceMapper
  -> Domain Trade reconstruction
  -> TradeListItem
```

The one-Trade detail path is:

```text
TradesViewModel
  -> ITradeDetailReader
  -> TradeDetailReader
  -> SQLite
  -> TradePersistenceMapper
  -> Domain Trade reconstruction
  -> TradeDetail
```

Both read paths use current Account and Instrument values only as display labels. Historical point value, currency, state, lifecycle timestamps, exposure, average prices, costs, and P&L remain authoritative from the reconstructed Trade and its pricing snapshot.

`TradeListReader` and `TradeDetailReader` each create a fresh `JournalDbContext`, use no-tracking EF queries, and reconstruct Domain Trades through `TradePersistenceMapper`. EF Core materializes Infrastructure records, never Domain entities directly; lifecycle and economics remain Domain-derived.

After a successful Trade commit, Desktop resets and closes the draft, reports success, and performs a best-effort authoritative list reload. That post-commit reload deliberately does not inherit the completed Save command's cancellation. If it fails, the persisted write remains successful, the existing rows remain visible, and a list-level error allows an explicit Refresh; it is not reclassified as a Save failure.

`TradesViewModel` keeps four independent areas of feature state: manual reference data, the bounded Trade list, the manual draft/save operation, and Trade Detail. Detail state is exposed through `SelectedTradeDetail`, `IsTradeDetailVisible`, `IsTradeDetailLoading`, `IsTradeDetailNotFound`, and `TradeDetailErrorMessage`. Loading detail does not replace the list or mutate the aggregate.

`IManualTradeReferenceDataReader` is a purpose-specific Trade-entry projection, not a generic query service. Management pages use `ITradingAccountReader` and `IInstrumentReader` to show active and inactive records. Normal Desktop manual entry requests active references only; the Application reader can explicitly include inactive references, and `CreateManualTradeUseCase` requires references to exist without making active status a Domain invariant. This preserves a path for future historical or backfill entry.

### Setup and Mistake Classification

The final M9 conceptual model is:

```text
TradingSetup
    reusable trade-pattern catalog

Trade
    optional TradingSetupId
    owned TradeExecution history

TradeScreenshot
    separate TradeId association
    opaque binary-storage key

TradingMistake
    reusable process-mistake catalog

TradeMistake
    TradeId
    TradingMistakeId
    optional Note
```

`Trade.SetTradingSetup(...)` owns the Domain mutation for assignment and clearing. Application use cases reload authoritative aggregates, require newly selected Setups to be active, and persist through the narrow Trade mutation boundary. Inactive Setups remain visible when historically referenced.

Trading Mistakes are attached through separate `TradeMistake` association records rather than an aggregate-owned `Trade.Mistakes` collection. Application permits only active mistake definitions to be newly assigned, Infrastructure prevents duplicate `(TradeId, TradingMistakeId)` pairs, and inactive historical assignments remain visible and removable. Assignment and removal do not mutate `Trade.UpdatedAtUtc`; an occurrence Note is captured only during assignment and has no standalone edit workflow.

## Trade Screenshot Architecture

`TradeScreenshot` is storage-agnostic metadata associated with a persisted Trade through `TradeId`; it is intentionally separate from the `Trade` aggregate and does not change executions or economics. Its types—PreTrade, Entry, Management, Exit, PostTrade, and Other—capture process context across the position lifecycle so review is not limited to outcome or P&L. Screenshot metadata lives in SQLite, while binary image content lives in local file storage. SQLite does not contain image blobs.

`StorageKey` is the opaque identity joining metadata to a storage provider. Domain and Application do not interpret it as a path, and list/content projections never expose it to Desktop. The original filename is separate presentation metadata and is neither a source path nor a unique storage identity. Physical path construction and validation remain Infrastructure concerns.

Application defines narrow screenshot boundaries rather than a generic repository:

- `ITradeExistenceReader` verifies the owning Trade before an Add writes a file;
- `ITradeScreenshotFileStorage` stores, opens, and deletes content through opaque keys;
- `ITradeScreenshotStore` commits new metadata;
- `ITradeScreenshotReader` returns ordered metadata projections without storage identities;
- `ITradeScreenshotContentReader` returns caller-owned content streams by screenshot ID; and
- `ITradeScreenshotDeletionStore` atomically deletes Trade-scoped metadata and returns cleanup information only after commit.

Add uses a file-first consistency model:

```text
validate input -> confirm Trade exists -> store binary -> construct Domain metadata
               -> commit SQLite metadata -> authoritative list reload
```

If Domain construction or metadata persistence fails after storage, the use case attempts file compensation with `CancellationToken.None` and rethrows the original failure. Compensation failure never replaces that failure. The source stream remains caller-owned. Once metadata commits, later cancellation does not reclassify the successful Add; Desktop's authoritative reload is also best-effort and non-cancellable from the completed command.

Metadata reads query only the owning Trade's screenshot rows and do not join Account or Instrument current state. Results place captured screenshots first by `CapturedAtUtc`, followed by screenshots without a capture timestamp, with `CreatedAtUtc` and ID providing deterministic tie-breaking. Content retrieval looks up metadata by screenshot ID, resolves the opaque key inside Infrastructure, and returns the original filename plus a caller-owned stream. Missing metadata returns `null`; existing metadata with a missing physical file raises `FileNotFoundException`. Infrastructure streams file content and does not buffer it into a `byte[]`.

Desktop owns WPF-specific file selection, image decoding, and delete confirmation. Preview requests use screenshot IDs, never paths. The decoder uses `BitmapCacheOption.OnLoad` and freezes the decoded bitmap, allowing the content stream to be disposed immediately without retaining a file handle. Preview state permits only one image at a time and uses request versioning so cancelled or stale asynchronous results cannot replace current state. Missing metadata, missing files, and decode failures produce distinct safe UI states without revealing paths, storage keys, or raw exception details.

Delete uses the opposite, database-first consistency model:

```text
delete metadata scoped by TradeId + ScreenshotId -> SaveChanges commit
               -> best-effort physical cleanup -> authoritative list reload
```

The deletion store returns the opaque key only after the SQLite commit. Cleanup then uses `CancellationToken.None`; failure is a non-fatal cleanup warning, and metadata is never recreated as compensation. Missing metadata triggers no file operation. A committed delete is never reclassified as cancellation, and a preview of the deleted screenshot closes only after commit. If the post-commit metadata reload fails, Desktop clears the known-stale screenshot list while retaining deletion success.

`LocalTradeScreenshotFileStorage` accepts PNG, JPG/JPEG, and WebP. It generates opaque GUID-based filenames, writes through a same-directory temporary file before the final move, cleans temporary files after failure or cancellation, rejects traversal and rooted keys, returns `null` for missing files, and treats deletion as idempotent. It has no database dependency. PNG and JPEG preview use native WPF/WIC; WebP preview depends on codec support installed with the operating system and fails safely when unsupported. No third-party decoder is included.

The existing `TradeScreenshots` table from migration `20260908122839_InitialCreate` is sufficient for this feature; M8 adds no schema or migration. Its foreign key to Trade retains intentional `Restrict` behavior, with no cascade redesign, and screenshot history is independent of current Account or Instrument active state.

The storage interface permits a future alternate provider without changing Domain metadata or Application workflow semantics. No cloud or SaaS storage provider, thumbnail pipeline, annotation, compression, OCR, background orphan cleanup, or screenshot analysis is implemented.

## Persistence Boundary

The persistence flow is explicit:

```text
Domain
  ↑ explicit mapping and Rehydrate(...)
Infrastructure persistence records
  ↓
EF Core
  ↓
SQLite
```

EF Core adapts to the Domain; the Domain does not adapt to EF Core. EF Core materializes mutable Infrastructure records rather than Domain entities. Domain types remain immutable or getter-heavy where appropriate, contain no EF attributes, and have no EF dependency. Infrastructure mappers reconstruct Domain entities through their explicit `Rehydrate(...)` APIs. AutoMapper is not used at this boundary.

The architecture deliberately does not introduce a generic `Repository<T>` or Unit of Work abstraction. Narrow Application-facing readers and stores support the concrete Accounts, Instruments, Trading Setup, Trading Mistake, Trade classification, and screenshot workflows. Trade and screenshot readers express distinct query shapes. `IDbContextFactory<JournalDbContext>` remains Infrastructure persistence machinery and is not exposed to UI code.

`AddPersistence(...)` registers `IDbContextFactory<JournalDbContext>`. Contexts are short-lived, created per operation, and disposed after use; the desktop application does not retain a long-lived context. Production-wired integration tests verify that writes made through one context are visible through later fresh contexts.

For Trade creation, `TradeStore.AddAsync(...)` creates a fresh context, maps the authoritative aggregate with the existing Trade and execution persistence mappers, tracks the Trade root and every execution, and calls `SaveChangesAsync(...)` once. EF Core's transactional SaveChanges behavior provides atomicity; there is no custom transaction or Unit of Work abstraction.

Detailed schema, provider, and migration decisions are documented in [Persistence](persistence.md).

### Accounts and Instruments Reference Data

`TradingAccount` is reference data used to associate journal activity with a stable account. `StartingBalance` is an optional initial/reference value, not current equity. M5 intentionally has no persisted `CurrentBalance`; future balance behavior must not be inferred or synchronized from Starting Balance. Account activation and deactivation are reversible, and inactive accounts remain available for historical references.

`Instrument.Symbol` is canonical/root reference data such as `NQ`, `MNQ`, `ES`, or `MES`. M5 does not model contract-specific symbols such as `NQZ6`, expiration, contract month, or rollover. `TickSize` and `TickValue` are authoritative pricing metadata, while `PointValue` is derived as `TickValue / TickSize` and is not separately mutable input. Instrument activation and deactivation are reversible, and inactive instruments remain available for historical references.

M5 introduces no new uniqueness business rule for account name, external account ID, or instrument symbol. No duplicate policy has yet been approved, and Desktop does not invent one.

## Composition Root

`PersonalTradingJournal.Desktop/App.xaml.cs` is the current composition root. It is responsible for:

- creating `LocalApplicationPaths`;
- creating the required local application directories;
- configuring Serilog;
- building the Generic Host;
- registering dependencies;
- starting the host;
- loading and applying the persisted Desktop theme preference;
- resolving and awaiting `JournalDatabaseInitializer.InitializeAsync()`;
- resolving and showing `MainWindow`; and
- stopping and disposing the host.

Domain and Application must not know about WPF startup or application lifecycle details.

The composition root wires current Application workflows, their Infrastructure implementations, feature ViewModels, the Desktop theme/settings services, and the shell. This includes Trade creation/browsing/classification, Trading Setup and Trading Mistake catalogs, Trade Mistake associations, screenshot add/read/preview/delete dependencies, and all seven retained concrete feature ViewModels. `MainWindowViewModel` and `MainWindow` are also created through dependency injection. Feature ViewModels receive dependencies through their constructors and do not resolve services themselves.

The startup order is:

```text
LocalApplicationPaths
  -> create directories
  -> configure Serilog
  -> build Generic Host
  -> StartAsync
  -> load settings and apply theme
  -> JournalDatabaseInitializer.InitializeAsync
  -> resolve and show MainWindow
```

Runtime initialization uses `Database.MigrateAsync()`, never `EnsureCreated`. Migration exceptions propagate into the existing fatal startup handler, so the main window is not resolved or shown after a migration failure. There is no automatic database deletion, recreation, or destructive recovery fallback.

## Design-Time Persistence Separation

`JournalDbContextDesignTimeFactory` exists only for EF tooling. It creates a context using SQLite `Data Source=:memory:` with foreign keys enabled. It does not use `LocalApplicationPaths`, access the user's `journal.db`, boot WPF, create a physical database, or apply migrations. This separation allows migration generation and model inspection without placing real user storage at risk.

## Local-First Storage

Local application data is centralized under:

```text
%LocalAppData%\PersonalTradingJournal
```

The current paths are:

- `journal.db` — the active local SQLite store, created and migrated during application startup;
- `settings.json` — the local System/Dark/Light Desktop appearance preference;
- `screenshots` — active local storage for Trade screenshot binary files;
- `logs` — active storage for local rolling logs; and
- `backups` — reserved for future backup data.

Centralizing these paths gives the application one predictable per-user storage location while keeping Windows-specific resolution in Infrastructure.

`TradeScreenshot` records persist metadata and an opaque `StorageKey` in SQLite. Binary files, key-to-path resolution, and file lifecycle operations remain in Infrastructure under the screenshots directory; cloud storage is not implemented.

## Logging

Serilog is configured in the Desktop composition root and writes local daily rolling files. `Microsoft.Extensions.Logging` is the logging abstraction available to future host-managed services, and its events are routed through Serilog.

Domain must not depend on Serilog. Application code should not require Serilog-specific APIs.

## Build Governance

Repository-wide configuration keeps build behavior consistent across projects and development environments:

- `global.json` selects the intended .NET 10 SDK feature band and patch roll-forward behavior while excluding prerelease SDKs.
- `Directory.Build.props` enables nullable reference types and implicit usings, treats warnings as errors, and uses the latest analysis level.
- `Directory.Packages.props` enables Central Package Management and is the source of truth for NuGet versions.

Target frameworks remain project-specific because the class libraries target `net10.0` while WPF targets `net10.0-windows`.

## Testing Approach

Testing follows the solution layers:

- **Domain.Tests** contains deterministic tests for the implemented M2 entities, lifecycle rules, calculations, mutations, and invariants.
- **Application.Tests** covers use-case behavior and authoritative validation, including reference lookup, historical pricing capture, manual Trade creation/closure, Setup classification, Trade Mistake assignment/removal, and screenshot orchestration.
- **Infrastructure.Tests** covers implementation and integration behavior, including mapping, real SQLite semantics, relational integrity, migrated temporary databases, runtime initialization, Trade persistence/reads, Setup and Mistake persistence, Trade Mistake relationships, screenshot storage, and cancellation.
- **Desktop.Tests** targets `net10.0-windows` and exercises ViewModel, navigation, and project-owned XAML-resource behavior using real Application use cases with hand-written test readers and stores. It covers catalog lifecycle, manual Trade and screenshot forms, classification and association state, authoritative reloads, cancellation, operation gating, Trade browsing, and screenshot lifecycle. The suite does not instantiate WPF `Window`, `Application`, or `UserControl` objects or perform UI automation.

Domain tests receive timestamps explicitly and do not depend on a real clock, filesystem, database, or network. Infrastructure persistence tests use isolated temporary SQLite databases, fresh contexts, and explicit cleanup rather than the user's real local application data. Path-construction tests separately verify that calculating local paths does not itself create `journal.db`.

## CI

GitHub Actions validates the repository on `windows-latest` using this sequence:

```text
Restore -> Release Build -> Tests
```

The workflow runs for pushes to `main` and pull requests targeting `main`, with the SDK resolved from `global.json`. CI does not launch the WPF application.

## Architecture Rules

1. Domain remains independent of infrastructure and UI frameworks.
2. Presentation layers must not contain trading business rules.
3. External concerns belong in Infrastructure behind appropriate abstractions when an abstraction provides real value.
4. Do not create abstractions speculatively; add them when they protect a meaningful boundary or enable testability.
5. Prefer explicit domain modeling over CRUD-oriented models.
6. Optimize for possible SaaS evolution without introducing multi-user, cloud, or distributed-system complexity before it is needed.
7. Use `Guid` identifiers for future persisted domain entities unless a specific domain reason requires another identity model.
8. Keep persisted timestamps in UTC where applicable.
9. Treat application data integrity as more important than UI convenience.
10. Update the README and milestone documentation at milestone completion, not after every implementation task, except when developer workflow documentation must change immediately.

## Trading Domain Decisions

### Execution History Is Authoritative

`Trade` state and economics derive from its ordered `TradeExecution` history. Direction, status, open quantity, lifecycle timestamps, costs, average prices, and final P&L are not maintained as separately mutable copies. Executions remain immutable market facts and support fractional quantities as well as zero or negative prices.

### A Trade Is Flat-to-Flat

A `Trade` represents one directional position lifecycle, from the opening execution until the position returns exactly to flat. Scale-in and partial scale-out are supported. An execution that would reverse through zero is rejected; the reversed remainder belongs to a different trade and future import/grouping behavior is responsible for splitting it.

### Historical Pricing Is Stable

Each trade owns a `TradePricingSnapshot` containing the point value and currency used for its economics. Closed-trade P&L therefore remains reproducible without loading current `Instrument` reference data, whose pricing metadata could change later.

### Review Metadata Is Separate from Market Facts

Optional `TradingSetupId` is review metadata and may be corrected during review, including after closure. Setup changes do not alter executions or P&L. The product intentionally uses `TradingSetup` as its sole primary trade-pattern classification because the former broader `Strategy` concept overlapped with it and created redundant taxonomy, UI, and analytics. Broader grouping will be introduced only if concrete analytics needs justify it.

### Screenshots Are Outside the Trade Aggregate

`TradeScreenshot` is separate metadata associated through `TradeId`; `Trade` does not own a screenshot collection. The metadata contains an opaque `StorageKey`, not binary image data or a filesystem path. Storage resolution belongs outside Domain.

### Mistakes Are Review Associations

`TradingMistake` is a reusable, user-defined catalog definition. `TradeMistake` is a separate occurrence/association between a trade and a mistake definition, with an optional occurrence-specific note. These concepts do not alter execution history, and neither aggregate owns an association collection. SQLite persistence enforces at most one association for each `(TradeId, TradingMistakeId)` pair.

### Process Quality Is Independent from Outcome

The model supports every combination of process quality and financial result: a good trade may profit or lose, and a process-violating trade may profit or lose. Profit does not prove correct execution, and loss does not prove poor execution. Setup and mistake classification remain independent from P&L so later review and analytics can assess process rather than infer quality from outcome. Those analytics are not implemented in M9.

## Future Evolution

The current deployment model is a Windows WPF application with local storage. A possible future presentation topology is:

```text
Desktop -----------> Application / Domain
ASP.NET Core API ---> Application / Domain
        ^
        |
   Web client
```

This illustrates an architectural direction only. The ASP.NET Core API, web client, and SaaS capabilities are not implemented. A future provider may replace or adapt the current Infrastructure persistence implementation while Domain and Application remain stable; the EF records and SQLite schema are not assumed to be shared directly with future server infrastructure.
