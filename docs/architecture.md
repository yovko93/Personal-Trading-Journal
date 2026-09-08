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
- **Strategies** — reusable broad trading methodologies;
- **Setups** — reusable specific market configurations;
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

The Application project is the application and use-case layer. It defines abstractions required by application logic and will orchestrate the domain with external implementations.

The current example is `IApplicationPaths`. This abstraction belongs in Application because application behavior may need known storage locations without knowing how Windows resolves or constructs those locations.

### PersonalTradingJournal.Infrastructure

The Infrastructure project implements Application abstractions and owns external technical concerns. Its current responsibilities include:

- `LocalApplicationPaths` and local directory creation;
- `JournalDbContext` and EF Core SQLite registration;
- Infrastructure-owned persistence records and `IEntityTypeConfiguration` mappings;
- explicit persistence-record/Domain mappers;
- the SQLite UTC timestamp converter;
- EF Core migrations and the design-time context factory; and
- `JournalDatabaseInitializer` for runtime migration application.

Future imports and alternative persistence providers also belong at this boundary when concrete use cases require them.

### PersonalTradingJournal.Contracts

The Contracts project is reserved for stable DTOs or contracts that may later be shared across presentation or API boundaries. It does not currently define application contracts.

### PersonalTradingJournal.Desktop

The Desktop project contains the WPF presentation layer and the application composition root. It owns:

- WPF Views and presentation ViewModels;
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

The ViewModels use `CommunityToolkit.Mvvm`: `ObservableObject` supplies change notification and `RelayCommand<NavigationDestination>` implements the shell command. `NavigationDestination` is Desktop-only presentation state, is not persisted, and has no domain meaning. `CurrentDestination` is the single source of truth for navigation selection; `NavigationSelectionConverter` derives each Button's selected state by comparing it with the Button's command parameter. There is no separate selected-item state.

There is intentionally no `NavigationService` or `INavigationService`. `MainWindowViewModel` is currently the only component that initiates shell navigation, so another abstraction would be premature. A navigation service should be considered only when another ViewModel has a demonstrated need to initiate cross-feature navigation.

`MainWindow` does not construct feature Views. Its `ContentControl` presents `CurrentContentViewModel`, and implicit `DataTemplate` mappings in `App.xaml` resolve `DashboardViewModel` to `DashboardView` and `PlaceholderViewModel` to `PlaceholderView`. The 17 destinations without real workflows share the placeholder mapping instead of carrying empty View/ViewModel pairs. A placeholder should be replaced only when its feature gains real presentation state and Application use cases.

The Dashboard is currently a presentation shell. It provides neutral metric and panel surfaces but performs no analytics or database queries. Financial outcome must not be interpreted as process quality: good process can lose, and bad process can profit. Future process-quality analysis must model that distinction explicitly.

### Desktop Data-Access Boundary

Desktop may depend on Application abstractions and use cases. Its reference to Infrastructure exists because Desktop is the composition root that wires concrete implementations; it does not authorize feature ViewModels to query `JournalDbContext` directly. M5 and later workflows must expose meaningful Application boundaries instead of creating a `ViewModel -> JournalDbContext` path.

Desktop objects use constructor injection. ViewModels must not locate dependencies through `IServiceProvider` or another service-locator pattern.

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

M3 deliberately does not introduce a generic `Repository<T>`, a Unit of Work abstraction, or speculative Application repository interfaces. `IDbContextFactory<JournalDbContext>` is Infrastructure persistence machinery, not an invitation for UI code to access the database directly. Application-facing persistence boundaries will be introduced with real use cases in later milestones.

`AddPersistence(...)` registers `IDbContextFactory<JournalDbContext>`. Contexts are short-lived, created per operation, and disposed after use; the desktop application does not retain a long-lived context. Production-wired integration tests verify that writes made through one context are visible through later fresh contexts.

Detailed schema, provider, and migration decisions are documented in [Persistence](persistence.md).

## Composition Root

`PersonalTradingJournal.Desktop/App.xaml.cs` is the current composition root. It is responsible for:

- creating `LocalApplicationPaths`;
- creating the required local application directories;
- configuring Serilog;
- building the Generic Host;
- registering dependencies;
- starting the host;
- resolving and awaiting `JournalDatabaseInitializer.InitializeAsync()`;
- resolving and showing `MainWindow`; and
- stopping and disposing the host.

Domain and Application must not know about WPF startup or application lifecycle details.

`DashboardViewModel`, `MainWindowViewModel`, and `MainWindow` are created through dependency injection. This keeps construction in the composition root while preserving constructor injection at the presentation boundary.

The startup order is:

```text
LocalApplicationPaths
  -> create directories
  -> configure Serilog
  -> build Generic Host
  -> StartAsync
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
- `screenshots` — reserved for future trade screenshot files;
- `logs` — active storage for local rolling logs; and
- `backups` — reserved for future backup data.

Centralizing these paths gives the application one predictable per-user storage location while keeping Windows-specific resolution in Infrastructure.

Screenshot binary files are intended to live outside the database. `TradeScreenshot` records currently persist metadata and an opaque `StorageKey` in SQLite; Domain does not interpret that key as a Windows or relative filesystem path. Physical image storage, key resolution, and file lifecycle behavior remain deferred, and cloud storage is not implemented.

## Logging

Serilog is configured in the Desktop composition root and writes local daily rolling files. `Microsoft.Extensions.Logging` is the logging abstraction available to future host-managed services, and its events are routed through Serilog.

Domain must not depend on Serilog. Application code should not require Serilog-specific APIs.

## Build Governance

Repository-wide configuration keeps build behavior consistent across projects and development environments:

- `global.json` selects the intended .NET 10 SDK feature band and patch roll-forward behavior while excluding prerelease SDKs.
- `Directory.Build.props` enables nullable reference types and implicit usings, treats warnings as errors, and uses the latest analysis level.
- `Directory.Packages.props` enables Central Package Management and is the source of truth for NuGet versions.

Target frameworks remain project-specific because the class libraries target `net10.0` while WPF targets `net10.0-windows`.

## Testing Strategy

Testing follows the solution layers:

- **Domain.Tests** contains deterministic tests for the implemented M2 entities, lifecycle rules, calculations, mutations, and invariants.
- **Application.Tests** covers application and use-case behavior.
- **Infrastructure.Tests** covers implementation and integration-focused behavior, including local paths, explicit mapper round-trips, real SQLite precision and timestamp queries, relational integrity, migrated temporary databases, runtime initialization, and production-wired full-graph scenarios.

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

Optional `StrategyId` and `TradingSetupId` classifications are independent dimensions and may be corrected during review, including after closure. Classification changes do not alter executions or P&L. `Strategy` represents a broad methodology; `TradingSetup` represents a specific repeatable market configuration and is not owned by a strategy.

### Screenshots Are Outside the Trade Aggregate

`TradeScreenshot` is separate metadata associated through `TradeId`; `Trade` does not own a screenshot collection. The metadata contains an opaque `StorageKey`, not binary image data or a filesystem path. Storage resolution belongs outside Domain.

### Mistakes Are Review Associations

`TradingMistake` is a reusable, user-defined catalog definition. `TradeMistake` is a separate occurrence/association between a trade and a mistake definition, with an optional occurrence-specific note. These concepts do not alter execution history, and neither aggregate owns an association collection. SQLite persistence enforces at most one association for each `(TradeId, TradingMistakeId)` pair.

### Process Quality Is Independent from Outcome

The model must support every combination of process quality and financial result: a good trade may profit or lose, and a process-violating trade may profit or lose. Profit does not prove correct execution, and loss does not prove poor execution. Strategy, setup, and mistake classification remain independent from P&L so future analytics and coaching can assess process rather than infer quality from outcome.

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
