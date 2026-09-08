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

The Infrastructure project provides implementations of Application abstractions and hosts external concerns such as filesystem integration. Future persistence and import implementations may also belong here when those capabilities are introduced.

The current implementation is `LocalApplicationPaths`, which resolves paths under Windows local application data and explicitly creates the required directories. Database persistence does not exist yet.

### PersonalTradingJournal.Contracts

The Contracts project is reserved for stable DTOs or contracts that may later be shared across presentation or API boundaries. It does not currently define application contracts.

### PersonalTradingJournal.Desktop

The Desktop project contains the WPF presentation layer and the application composition root. It owns Generic Host lifecycle management, dependency registration, logging composition, and Windows application startup and shutdown.

Trading business logic must not accumulate in this project.

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

## Composition Root

`PersonalTradingJournal.Desktop/App.xaml.cs` is the current composition root. It is responsible for:

- creating `LocalApplicationPaths`;
- creating the required local application directories;
- configuring Serilog;
- building the Generic Host;
- registering dependencies;
- starting the host;
- resolving and showing `MainWindow`; and
- stopping and disposing the host.

Domain and Application must not know about WPF startup or application lifecycle details.

## Local-First Storage

Local application data is centralized under:

```text
%LocalAppData%\PersonalTradingJournal
```

The current paths are:

- `journal.db` — reserved for future SQLite persistence; the file is not currently created;
- `screenshots` — reserved for future trade screenshot files;
- `logs` — active storage for local rolling logs; and
- `backups` — reserved for future backup data.

Centralizing these paths gives the application one predictable per-user storage location while keeping Windows-specific resolution in Infrastructure.

Screenshot binary files are intended to live outside the database. Domain stores only an opaque `StorageKey`; it does not interpret that key as a Windows or relative filesystem path. Application and Infrastructure will later map the key to physical storage, with the current local-first design intending to use the screenshot directory above. No `TradeScreenshot` file-storage implementation exists yet, and cloud storage is not implemented.

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
- **Infrastructure.Tests** covers implementation and integration-focused behavior.

Domain tests receive timestamps explicitly and do not depend on a real clock, filesystem, database, or network. Infrastructure tests verify `LocalApplicationPaths` path construction and directory initialization, including that `journal.db` is not created, using isolated temporary directories rather than the user's real local application data.

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

`TradingMistake` is a reusable, user-defined catalog definition. `TradeMistake` is a separate occurrence/association between a trade and a mistake definition, with an optional occurrence-specific note. These concepts do not alter execution history, and neither aggregate owns an association collection. Persistence/Application behavior in M3 must enforce at most one association for each `(TradeId, TradingMistakeId)` pair.

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

This illustrates an architectural direction only. The ASP.NET Core API, web client, and SaaS capabilities are not implemented.
