# Architecture

## Architectural Goals

The architecture is designed for long-term maintainability, testability, correctness, clear dependency boundaries, extensibility, local-first desktop operation, and possible future web or SaaS evolution.

Architectural decisions should avoid both short-term coupling and unnecessary enterprise-style abstractions. The repository favors explicit boundaries that provide current value without introducing distributed-system or framework complexity prematurely.

## Solution Layers

### PersonalTradingJournal.Domain

The Domain project is the intended home for the core trading domain model, domain rules and invariants, and future value objects and entities. It is currently a foundation project; the trading business domain has not yet been implemented.

Rules:

- no WPF dependency;
- no Entity Framework Core dependency;
- no Infrastructure dependency; and
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

Large screenshots should later be stored as files rather than database BLOBs, with persistence retaining metadata and relative paths. This is an architectural intention, not implemented behavior.

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

- **Domain.Tests** covers pure domain rules and invariants as the domain is introduced.
- **Application.Tests** covers application and use-case behavior.
- **Infrastructure.Tests** covers implementation and integration-focused behavior.

The currently implemented tests verify `LocalApplicationPaths` path construction and directory initialization, including that `journal.db` is not created. Tests use isolated temporary directories and must not write to the user's real local application data.

Broad trading-domain coverage does not exist yet because the domain has not been implemented.

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

## Trading Domain Design Principles

Future trading business logic should:

- model trades from executions rather than assuming one entry and one exit;
- distinguish trading-process quality from trade profit and loss;
- support scale-in and scale-out behavior;
- preserve broker or source execution identity when available;
- support extensible instruments rather than hardcoding only NQ or ES;
- support meaningful risk and performance analytics; and
- avoid deriving trader quality solely from profitable versus losing trades.

These are architecture-level principles, not a specification of an implemented domain model. Detailed domain documentation belongs to M2 after that milestone is complete.

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
