# Personal Trading Journal

Personal Trading Journal is a local-first Windows desktop application designed to help traders record, review, analyze, and improve their trading process. The initial focus is futures trading, especially instruments such as NQ and ES, while the architecture is intended to remain extensible to other markets and a possible future SaaS or web version.

The repository currently contains the application foundation, the core trading Domain model, local EF Core/SQLite persistence, the WPF shell and navigation foundation, persisted System/Dark/Light appearance preferences, complete lifecycle management for Trading Accounts, Instruments, Trading Setups, and Trading Mistakes, and manual Trade create/list/view/edit/close/delete workflows. Trades also support local screenshots, Setup classification, Mistake assignments, an authoritative SQLite-paged and sortable browse view, and a reviewed Tradovate matched-fills CSV import workflow. Journal workflows, operational analytics, and AI capabilities have not yet been implemented.

## Current Status

**Milestone M1 — Foundation: Complete**

**Milestone M2 — Domain Foundation: Complete**

**Milestone M3 — EF Core + SQLite Persistence: Complete**

**Milestone M4 — WPF Shell + Navigation: Complete**

**Milestone M5 — Accounts + Instruments: Complete**

**Milestone M6 — Manual Trade Entry: Complete**

**Milestone M7 — Trade List / Detail: Complete**

**Milestone M8 — Screenshot Management: Complete**

**Milestone M9 — Setup and Mistake Classification: Complete**

**Milestone M10 — Tradovate CSV Import: Complete**

**Desktop Theme System — System / Dark / Light: Complete**

**Entity Lifecycle & CRUD UX: Complete**

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
- optional `TradingSetup` classification as the single reusable trade-pattern concept;
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
- data-driven sidebar navigation with centralized vector icons and a top-level Notebook destination;
- collapsible Trading, Analysis, Planning, and Review groups with session-local expansion state;
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
- Long or Short direction with whole-contract quantity for Futures and positive decimal quantity for other asset classes;
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

The M8 Screenshot Management milestone includes:

- PNG, JPG/JPEG, and WebP screenshots attached to persisted Trades;
- required screenshot classification plus optional captured UTC timestamp, timeframe, and description metadata persisted in SQLite;
- authoritative screenshot listing, safe preview, and deletion through the Trades page;
- local-first binary storage under the application screenshots directory rather than SQLite blob storage;
- deliberate file-first compensation for Add and database-first best-effort file cleanup for Delete; and
- opaque storage identities and Infrastructure-owned physical paths, with no individual screenshot filesystem path exposed to Domain, Application workflows, or the Desktop UI.

PNG and JPEG preview use native WPF/WIC support. WebP files are accepted for storage, but preview depends on codec support available through the operating system's WIC installation; unsupported WebP content fails with a safe preview error, and no third-party image package is used.

Post-M8 manual Trade lifecycle corrections allow an open manually entered Trade to be closed later by adding an opposite-side execution for its full authoritative remaining quantity. Futures manual quantity is expressed as a positive whole number of contracts, while non-Futures instruments retain positive decimal quantities. Contract economics remain Instrument-driven through asset class, tick size, tick value, and derived point value; no symbol-specific quantity or pricing rules are used.

The M9 Setup and Mistake Classification milestone includes:

- persisted Trading Setup and Trading Mistake catalogs with create and reversible active/inactive lifecycle workflows;
- an optional Trading Setup classification during manual Trade creation and assign/change/clear behavior in Trade Detail;
- zero or more Trading Mistake assignments per Trade, each with an optional occurrence-specific note and explicit removal;
- preservation of inactive historical Setup and Mistake references while allowing only active catalog items to be newly assigned;
- authoritative post-write reloads and safe Desktop operation feedback; and
- regression protection for project-owned WPF `StaticResource` keys.

Trading Setup is the single reusable trade-pattern classification. The overlapping Strategy concept was intentionally removed, and no Strategy catalog or Trade Strategy assignment exists in the current architecture. Setup and Mistake performance analytics are not implemented.

The Entity Lifecycle & CRUD UX milestone completes consistent View/Edit/Delete presentation and explicit entity-specific update and deletion workflows. Accounts, Instruments, Trading Setups, and Trading Mistakes may be hard deleted only while unused; referenced records remain editable and can be deactivated without breaking historical Trades. Trades support correction through the Domain aggregate and confirmed hard deletion of their owned database records, followed by best-effort physical screenshot cleanup.

Trade browsing uses fixed 20-row pages with server-side count, sorting, skip, and take. Opened UTC, Trade, Account, Average Prices, Open Qty, and Net P&L are sortable; deterministic Trade-ID tie-breaking and exact decimal sort keys preserve stable page boundaries without SQLite floating-point economics.

The M10 Tradovate CSV Import milestone parses and reconstructs matched fills, resolves existing or proposed Instruments, applies the unified Europe/Sofia source-to-UTC-to-America/New_York time policy, and prepares an explicit Trading Account selection. The Desktop presents the analysis, warnings, blocking diagnostics, Instrument economics, New York trade times, and a read-only preview before showing a non-destructive confirmation dialog. Only an explicitly confirmed, currently valid preview reaches the Application import use case and its atomic SQLite transaction.

To import a supported Tradovate matched-fills export:

1. Open **Import** and choose **Select CSV**.
2. Review the analysis and Instrument resolution, then explicitly select the destination Trading Account.
3. Choose **Build Preview** and inspect the summary, proposed Instruments, candidate Trades, New York timestamps, warnings, and errors.
4. Choose **Import Trades**, review the final confirmation, and accept it to persist the import.
5. Review the Imported, Duplicates Skipped, and Instruments Created counts. Opening Trades or Instruments after a committed import reloads their authoritative data.

Imported commission and fee values remain `null` (unknown, not known zero) because the supported export does not contain them. Exact fill identities are durable per selected PTJ Account: exact duplicate Trades are skipped, mixed new/duplicate imports report both counts, and unsafe partial overlaps block the whole operation. A reference-data change after preview never causes silent Instrument substitution; the user is directed to rebuild the preview, which re-runs Instrument resolution without reparsing the CSV. Unsupported or ambiguous Instrument metadata remains blocking and must be resolved in reference data before import.

The Desktop Theme System adds one semantic design system backed by parity-checked Dark and Light resource dictionaries. Theme-sensitive brushes update live through `DynamicResource`. Settings offers System, Dark, and Light; System follows the Windows application theme, while the compact header toggle switches the effective appearance to an explicit opposite preference. `%LocalAppData%\PersonalTradingJournal\settings.json` restores the preferred mode—not its resolved appearance—before the main window is shown. Missing or invalid settings safely fall back to System.

The Desktop creation workflows share a compact form language for Manual Trades, Accounts, Instruments, Trading Setups, and Trading Mistakes. Consistent section hierarchy, field labels, optional markers, restrained helper text, visible focus treatment, semantic feedback, and primary/secondary actions improve scanability without changing validation or persistence behavior.

Eight of the 19 shell destinations are concrete: Dashboard, Trades, Import, Accounts, Instruments, Setups, Mistakes, and Settings. Dashboard remains presentation-only, Settings owns appearance preference, and the other six are functional data-backed pages. The other 11 destinations remain placeholders.

The fixed-width sidebar renders all 19 destinations from one Desktop-owned navigation catalog. Dashboard and Notebook remain top-level, four labeled feature groups can be collapsed independently, and Accounts, Instruments, and Settings remain standalone utilities below a divider. Every destination uses a project-owned vector icon and the existing semantic theme resources in both Dark and Light modes.

The active milestone is not yet defined beyond the completed **M10 — Tradovate CSV Import** workflow.

Historically, M9.1 introduced a Strategy catalog. M9.3.5 removed that concept after the taxonomy was simplified around Trading Setup as the sole reusable trade-pattern classification. M9 then completed Trading Setup and Trading Mistake catalogs, Trade classification and review associations, Desktop integration, UX hardening, acceptance, and documentation.

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
- **Application** owns use-case orchestration and meaningful read/persistence abstractions, including Account, Instrument, Trading Setup, Trading Mistake, Trade classification, manual Trade creation, Trade list/detail, and screenshot workflows.
- **Infrastructure** owns local Windows storage paths, screenshot files, and the EF Core/SQLite implementation, including persistence records, configurations, mappers, migrations, runtime database initialization, and concrete readers/stores.
- **Contracts** is reserved for stable DTOs or contracts shared across presentation and API boundaries.
- **Desktop** contains the WPF shell, real Accounts, Instruments, Trades, Setups, and Mistakes feature pages, manual Trade entry, Trade browsing and classification, screenshot selection/list/preview/delete presentation, navigation state, shared XAML resources, and the composition root.

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

The completed M10 baseline contains 1,668 passing tests: 400 Domain, 312 Application, 478 Infrastructure, and 478 Desktop tests, with zero failed and zero skipped. Desktop tests exercise presentation, ViewModel orchestration, import confirmation state, an isolated SQLite acceptance path, paging/sorting, lifecycle actions, navigation, settings persistence, Windows theme resolution, theme switching, and project-owned XAML-resource behavior without serving as broad UI automation.

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
├── settings.json
├── screenshots/
├── logs/
└── backups/
```

`journal.db` is the active local SQLite database. EF Core creates it and applies pending migrations automatically during desktop startup.

- `settings.json` stores the local System/Dark/Light preferred appearance; missing or invalid content falls back to System. The System preference resolves the current Windows application theme at startup and follows supported changes while the app is running.
- `screenshots` contains locally stored Trade screenshot binaries. Screenshot metadata is persisted separately in `journal.db`.
- `logs` contains the active application log files.
- `backups` is reserved for future backup functionality.

Screenshot binaries are not stored as SQLite blobs. Backup workflows are not yet implemented.

## Startup Persistence

Desktop startup starts the Generic Host, loads the local preferred theme, resolves System to the Windows application theme when needed, applies the concrete theme, applies database migrations, and only then resolves and shows `MainWindow`. This avoids an incorrect-theme startup flash. A migration failure is logged as a fatal startup error, aborts startup, and prevents the window from being shown. The application does not delete or recreate a failed database automatically.

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
