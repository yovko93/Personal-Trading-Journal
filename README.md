# Personal Trading Journal

Personal Trading Journal is a local-first Windows desktop application designed to help traders record, review, analyze, and improve their trading process. The initial focus is futures trading, especially instruments such as NQ and ES, while the architecture is intended to remain extensible to other markets and a possible future SaaS or web version.

The repository currently contains the application foundation and the core in-memory trading domain model. Database persistence, trading UI workflows, importing, and analytics have not yet been implemented.

## Current Status

**Milestone M1 — Foundation: Complete**

**Milestone M2 — Domain Foundation: Complete**

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

The next milestone is **M3 — EF Core + SQLite Persistence**. M2 provides domain behavior only: there is no database persistence or end-user trading workflow yet.

## Technology Stack

- .NET 10
- WPF
- Microsoft.Extensions.Hosting
- Microsoft.Extensions.DependencyInjection through the Generic Host
- Microsoft.Extensions.Logging
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
└── PersonalTradingJournal.Infrastructure.Tests

docs/
├── architecture.md
└── domain-model.md

.github/
└── workflows/
    └── ci.yml
```

- **Domain** contains the framework-independent M2 trading model, rules, and invariants.
- **Application** defines application-level orchestration and abstractions, currently including `IApplicationPaths`.
- **Infrastructure** implements Application abstractions and integrations, currently including local Windows storage paths.
- **Contracts** is reserved for stable DTOs or contracts shared across presentation and API boundaries.
- **Desktop** contains the WPF presentation layer and serves as the composition root for hosting, dependency injection, storage initialization, and logging.

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

`journal.db` is currently only a reserved, calculated path. Database persistence has not been implemented, and no persistence component creates this file.

- `screenshots` exists as a reserved location for future screenshot storage operations. M2 defines only storage-agnostic `TradeScreenshot` metadata; it does not store image files.
- `logs` contains the active application log files.
- `backups` is reserved for future backup functionality.

Screenshot persistence and backup functionality are not yet implemented.

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

Abstractions and infrastructure should be introduced only when they protect a real boundary or solve a current need. See [Architecture](docs/architecture.md) for the detailed rules and [Domain Model](docs/domain-model.md) for the finalized M2 model.

## Documentation Policy

`README.md` and milestone-level documentation are updated when a milestone is completed rather than after every individual task. Documentation must be updated immediately when a task changes developer setup, build, run, configuration, or another workflow developers need to use the repository correctly.

The README is not a task-by-task changelog.
