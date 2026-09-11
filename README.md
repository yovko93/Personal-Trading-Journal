# Personal Trading Journal

Personal Trading Journal is a local-first Windows desktop application designed to help traders record, review, analyze, and improve their trading process. The initial focus is futures trading, especially instruments such as NQ and ES, while the architecture is intended to remain extensible to other markets and a possible future SaaS or web version.

The repository currently contains the application foundation, the core trading Domain model, local EF Core/SQLite persistence, the WPF shell and navigation foundation, real Trading Account and Instrument management, Manual Trade Entry, an authoritative Recent Trades list, read-only Trade Detail with complete execution-lifecycle presentation, Desktop ViewModel tests, and production-wired persistence integration tests. Trade editing and deletion, imports, journal workflows, operational analytics, and AI capabilities have not yet been implemented.

## Current Status

**Milestone M1 — Foundation: Complete**

**Milestone M2 — Domain Foundation: Complete**

**Milestone M3 — EF Core + SQLite Persistence: Complete**

**Milestone M4 — WPF Shell + Navigation: Complete**

**Milestone M5 — Accounts + Instruments: Complete**

**Milestone M6 — Manual Trade Entry: Complete**

**Milestone M7 — Trade List / Detail: Complete**

The completed foundation includes:

- a .NET 10 solution with a layered project structure;
- repository-wide build configuration;
- Central Package Management;
- a WPF application hosted with the .NET Generic Host;
- a dependency injection foundation;
- local application storage paths and directory initialization;
- structured Serilog file logging; and
- automated GitHub Actions CI.

The M2 domain foundation includes:

- shared entity identity and UTC audit primitives;
- canonical instruments and trading-account reference data;
- an execution-based `Trade` aggregate with scale-in, scale-out, and a directional flat-to-flat lifecycle;
- historical pricing snapshots and closed-trade gross/net P&L;
- independent `Strategy` and `TradingSetup` classification;
- storage-agnostic trade-screenshot metadata; and
- a user-defined trading-mistake catalog with trade-mistake associations.

The M3 persistence foundation includes:

- EF Core 10 with a local SQLite `journal.db`;
- Infrastructure-owned persistence records, configurations, and explicit Domain mapping;
- an initial EF Core migration applied automatically before the main window is shown;
- exact decimal round-trips and explicit UTC timestamp provider handling;
- referential integrity that protects historical references; and
- migrated-schema and production-wired integration tests.

The M4 desktop presentation foundation includes:

- a `CommunityToolkit.Mvvm`-based MVVM foundation;
- a reusable dark WPF design system and permanent application shell;
- grouped sidebar navigation, including a top-level Notebook destination;
- typed `NavigationDestination` state and selected-navigation UX;
- `ContentControl` hosting with implicit ViewModel-to-View `DataTemplate` mappings;
- a presentation-only Dashboard shell;
- shared placeholder content for destinations without implemented workflows; and
- keyboard, focus, scrolling, and resizing hardening.

The M5 Accounts and Instruments milestone includes:

- persisted Trading Account and Instrument lists, creation, and reversible active/inactive lifecycle workflows;
- inactive reference data retained and visible for historical use;
- account Starting Balance as reference data rather than a current-balance calculation;
- canonical/root Instrument symbols with `AssetClass`, exchange, currency, `TickSize`, and `TickValue` metadata;
- derived Instrument `PointValue` (`TickValue / TickSize`), not a separately editable input;
- Application read boundaries and explicit create/lifecycle use cases backed by Infrastructure readers and stores;
- Desktop feature ViewModels that never access EF Core or `JournalDbContext` directly; and
- authoritative list reloads after writes, with reload failures reported separately from persistence failures.

The M6 Manual Trade Entry milestone includes:

- explicit Trading Account and Instrument selection;
- Long or Short direction and decimal Quantity;
- one opening execution with an optional full closing execution;
- explicit UTC execution timestamps, price, commission, and fees;
- open or closed `Trade` creation through Domain APIs; and
- atomic persistence of the Trade and its executions to SQLite.

This deliberately simple capture workflow does not limit the richer Domain model, which continues to support scale-in and partial scale-out. After a successful Save, Desktop persists the Trade, resets and closes the draft, reports success, and performs a best-effort authoritative Recent Trades reload. If that reload fails, the successful write and existing visible rows are retained while a list-level error directs the user to refresh.

The M7 Trade List / Detail milestone includes:

- a bounded authoritative Recent Trades list showing both open and closed Trades;
- market-event ordering by opening execution time, with deterministic Trade-ID tie-breaking;
- current Trading Account and Instrument labels alongside historically authoritative pricing snapshots and economics;
- authoritative post-save list reload without locally fabricated rows;
- an authoritative one-Trade detail projection;
- complete ordered execution-lifecycle presentation rather than entry/exit pairing;
- a read-only Trade Detail surface; and
- nullable P&L for open Trades, including partially exited positions.

Four of the 19 shell destinations are concrete: Dashboard, Trades, Accounts, and Instruments. Dashboard remains presentation-only, while Trades, Accounts, and Instruments are functional data-backed pages. The other 15 destinations remain placeholders.

The next milestone is **M8 — Screenshot Management**.

## Technology Stack

- .NET 10
- WPF
- CommunityToolkit.Mvvm
- Microsoft.Extensions.Hosting
- Microsoft.Extensions.DependencyInjection through the Generic Host
- Microsoft.Extensions.Logging
- Entity Framework Core 10
- SQLite
- Serilog
- xUnit
- GitHub Actions

## Repository Structure

```text
PersonalTradingJournal.sln
global.json
Directory.Build.props
Directory.Packages.props

src/
├── PersonalTradingJournal.Domain
├── PersonalTradingJournal.Application
├── PersonalTradingJournal.Infrastructure
├── PersonalTradingJournal.Contracts
└── PersonalTradingJournal.Desktop

tests/
├── PersonalTradingJournal.Domain.Tests
├── PersonalTradingJournal.Application.Tests
├── PersonalTradingJournal.Infrastructure.Tests
└── PersonalTradingJournal.Desktop.Tests

docs/
├── architecture.md
├── desktop-ui.md
├── domain-model.md
└── persistence.md

.github/
└── workflows/
    └── ci.yml
```

- **Domain** contains the framework-independent M2 trading model, rules, and invariants.
- **Application** owns use-case orchestration and meaningful read/persistence abstractions, including Account and Instrument workflows, manual Trade creation, the narrow Trade persistence boundary, manual Trade reference selection, `ITradeListReader` with `TradeListItem`, `ITradeDetailReader` with `TradeDetail` and `TradeExecutionDetailItem`, and `IApplicationPaths`.
- **Infrastructure** owns local Windows storage paths and the EF Core/SQLite implementation, including persistence records, configurations, mappers, migrations, runtime database initialization, the Account and Instrument readers/stores, `TradeStore`, `ManualTradeReferenceDataReader`, `TradeListReader`, and `TradeDetailReader`.
- **Contracts** is reserved for stable DTOs or contracts shared across presentation and API boundaries.
- **Desktop** contains the WPF shell, real Accounts, Instruments, and Trades feature pages, the manual Trade form and validation/submission workflow, authoritative Recent Trades and Trade Detail presentation, the ordered execution lifecycle, navigation state, shared XAML resources, and the composition root for hosting, dependency injection, persistence composition, storage initialization, and logging.

## Prerequisites

- A Windows development environment
- Git
- A .NET 10-compatible SDK resolved through the repository's `global.json`

Visual Studio is not required. Visual Studio or another .NET-capable IDE may be used.

## Build

```powershell
dotnet restore PersonalTradingJournal.sln
dotnet build PersonalTradingJournal.sln
```

## Test

```powershell
dotnet test PersonalTradingJournal.sln
```

The accepted M7 completion baseline contains 786 passing tests: 401 Domain, 46 Application, 224 Infrastructure, and 115 Desktop tests, with zero failed and zero skipped. Desktop tests exercise presentation and ViewModel behavior without instantiating the WPF visual tree; they are not UI automation.

## Run

```powershell
dotnet run --project src/PersonalTradingJournal.Desktop/PersonalTradingJournal.Desktop.csproj
```

## Local Application Data

Application data is rooted at:

```text
%LocalAppData%\PersonalTradingJournal
```

The current path layout is:

```text
PersonalTradingJournal/
├── journal.db
├── screenshots/
├── logs/
└── backups/
```

`journal.db` is the active local SQLite database. EF Core creates it and applies pending migrations automatically during desktop startup.

- `screenshots` is reserved for future physical screenshot storage. `TradeScreenshot` metadata is persisted in `journal.db`, while image copying and file lifecycle behavior remain unimplemented.
- `logs` contains the active application log files.
- `backups` is reserved for future backup functionality.

Binary screenshot storage and backup workflows are not yet implemented.

## Startup Persistence

Desktop startup starts the Generic Host, applies database migrations, and only then resolves and shows `MainWindow`. A migration failure is logged as a fatal startup error, aborts startup, and prevents the window from being shown. The application does not delete or recreate a failed database automatically.

## Database Schema Changes

Schema changes use EF Core migrations. Keep the `dotnet-ef` version aligned with the project's EF Core version line and see [Persistence](docs/persistence.md) for the migration commands and safety workflow.

## Logging

The desktop application uses Serilog for structured logging with:

- Information as the minimum level;
- daily rolling files named `ptj-YYYYMMDD.log`;
- storage under `%LocalAppData%\PersonalTradingJournal\logs`;
- retention limited to 14 files; and
- application startup and shutdown lifecycle events.

## Continuous Integration

GitHub Actions runs the CI workflow:

- on pushes to `main`;
- on pull requests targeting `main`;
- on a Windows-hosted runner;
- with the SDK resolved from `global.json`; and
- through restore, Release build, and test stages.

## Architectural Principles

The repository follows a domain-first design with dependencies directed toward the Domain. Infrastructure implements meaningful Application abstractions, while Desktop remains the composition and presentation layer. The system is local-first today, but the core should remain independent of WPF so a future web or SaaS presentation can evolve without replacing domain and application logic.

Abstractions and infrastructure should be introduced only when they protect a real boundary or solve a current need. See [Architecture](docs/architecture.md) for the system-level rules, [Desktop UI](docs/desktop-ui.md) for extending the WPF presentation layer, [Domain Model](docs/domain-model.md) for the trading model, and [Persistence](docs/persistence.md) for database semantics and migration workflow.

## Documentation Policy

`README.md` and milestone-level documentation are updated when a milestone is completed rather than after every individual task. Documentation must be updated immediately when a task changes developer setup, build, run, configuration, or another workflow developers need to use the repository correctly.

The README is not a task-by-task changelog.
