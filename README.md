# Personal Trading Journal

**Record your trades. Review your decisions. Refine your process.**

A Windows desktop trading journal that brings trade tracking, performance analytics, planning, and reflection into one local workspace.

[![CI](https://github.com/yovko93/Personal-Trading-Journal/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/yovko93/Personal-Trading-Journal/actions/workflows/ci.yml)
[![Status: In development](https://img.shields.io/badge/status-in_development-amber)](#project-status)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](global.json)
[![Windows · WPF](https://img.shields.io/badge/platform-Windows_WPF-0078D4)](#quickstart)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

[Quickstart](#quickstart) · [Features](#features) · [Documentation](#documentation) · [Report an issue](https://github.com/yovko93/Personal-Trading-Journal/issues)

## Table of Contents

- [About](#about)
- [Project Status](#project-status)
- [Features](#features)
- [Quickstart](#quickstart)
- [Data and Privacy](#data-and-privacy)
- [Architecture](#architecture)
- [Development and Testing](#development-and-testing)
- [Documentation](#documentation)
- [Contributing](#contributing)
- [License](#license)

## About

Personal Trading Journal helps individual traders connect their results with the decisions behind them. Capture executions, attach chart screenshots, organize setups and mistakes, prepare a trading plan, and review your progress over time.

The application stores your journal locally using SQLite. Core journaling and analytics work without a cloud account or an AI provider. Optional AI-assisted reviews extend the review workflow when you choose to use them.

The guiding principle is simple: **process quality and financial outcome are separate**. A profitable trade can break your rules, and a well-executed trade can lose. The journal keeps execution facts, review notes, and rule assessments distinct so you can evaluate both.

## Project Status

**In development.** The repository contains a working desktop application with the features below. Release acceptance is still in progress.

| Area | Status |
| --- | --- |
| Desktop application | Windows WPF application targeting .NET 10; build and run from source. |
| Core workflows | Trade capture, CSV import, analytics, journaling, planning, reviews, and Notebook are implemented. |
| Verification | Latest complete local Release gate: **5,546 passed / 1 failed / 1 skipped**, exit 1. Instruments checks pass; a Notebook post-close hover assertion remains unresolved. See [UI verification](docs/instruments-ui.md#complete-release-gate) for evidence. One Rules-view acceptance test remains [temporarily quarantined](docs/rules-view-quarantine.md); hosted confirmation is pending. |
| Known gaps | The separate **Performance** navigation page remains a placeholder. Use **Dashboard** and **Breakdown** for implemented analytics. |
| Deployment scope | Local Windows desktop application; web, multi-user, and cloud synchronization are not implemented. |

The CI badge tracks `main`; it does not certify every development change. The [Notebook input diagnosis](docs/ci117-notebook-retained-onset.md#interactive-follow-up-and-bounded-input-correction) records the controlled reproduction, bounded native-movement correction and passing verification. The original hosted first-hit trigger remains unknown. See also the [Calendar lifecycle correction](docs/ci115-calendar-and-notebook.md). Detailed historical evidence is retained in the [development history](docs/development-history.md), [menu-readiness guide](docs/ci-menu-readiness.md) and [Daily Review diagnosis](docs/ci-daily-review-acceptance.md).

## Features

| Workflow | What you can do |
| --- | --- |
| **Trade tracking** | Manage accounts and instruments; record entries, scale-ins, partial exits, and closures; attach screenshots and classify trades by setup and mistake. |
| **CSV import** | Preview and confirm supported Tradovate matched-fills and TopstepX Trades exports, with validation and duplicate detection. |
| **Analytics** | Explore Dashboard metrics, Calendar results, and Breakdown groups with drill-down into the contributing trades. |
| **Daily journal** | Capture reflections, save drafts and completed entries, and revisit revision history by date and account. |
| **Planning** | Maintain Playbook definitions, trading rules, daily principles, and Trading Plans with saved Ready revisions. |
| **Reviews** | Review daily, weekly, and monthly evidence, retain saved analyses, and optionally use AI-assisted coaching or the manual ChatGPT prompt workflow. |
| **Notebook** | Organize rich-text notes with folders, tags, images, templates, pinned notes, linked trades and setups, autosave, and recovery. |
| **Data management** | Create complete backups, validate and restore compatible archives, and export data as JSON or CSV. |
| **Reference data** | Manage Instruments with left-aligned columns, Edit inside each row's action menu, and horizontal table scrolling at narrow widths. Three-dot menus toggle open/closed; shared buttons have theme-aware hover highlights. |
| **Appearance** | Choose System, Light, or Dark themes, with the Bull and Bear Trading Journal artwork, a distinct emerald sidebar header, and matching Windows application icons. |

Analytics preserve currency boundaries and expose missing costs or incomplete evidence. Each view documents its Gross/Net policy; unavailable values are not silently treated as zero.

See [Instruments and action-menu verification](docs/instruments-ui.md) for menu coverage, Light/Dark layout checks, and the distinction between automated native input, rendered previews and pending live visual inspection.

The selected Bull and Bear artwork uses the supplied PNG unchanged and a generated ten-resolution Windows ICO. The approved emerald sidebar layout is preserved. Its Release build, 40 focused header/theme checks, inspected renders at 100% and 250%, and built/published EXE icon inspections pass. Native visual acceptance remains pending. See [Application Branding](docs/application-branding.md) for current results and [Release lifecycle investigation](docs/release-gate-lifecycle.md) for earlier complete-gate evidence.

## Quickstart

### Prerequisites

- **Windows** with desktop support for WPF.
- **.NET SDK 10.0.302**, or a compatible later patch in the same SDK feature band, as specified in [global.json](global.json).
- **Git** to clone the repository.
- **PowerShell 7** for the complete test script.

No separate database server is required. Package restore needs access to NuGet.

### 1. Clone and build

Run these commands in PowerShell:

```powershell
git clone https://github.com/yovko93/Personal-Trading-Journal.git
Set-Location Personal-Trading-Journal

dotnet restore PersonalTradingJournal.sln
dotnet build PersonalTradingJournal.sln --configuration Release --no-restore
```

### 2. Launch the application

```powershell
dotnet run --project src/PersonalTradingJournal.Desktop/PersonalTradingJournal.Desktop.csproj --configuration Release --no-build
```

On startup, the app creates its local data directories and applies database migrations automatically. The default database is `%LOCALAPPDATA%\PersonalTradingJournal\journal.db`.

### 3. Create your first journal entries

1. Open **Accounts** and create a trading account.
2. Open **Instruments** and configure the instruments you trade, including their pricing and currency.
3. Add a trade in **Trades**, or open **Import** to preview a supported CSV export before confirming it.
4. Explore **Dashboard** and **Calendar**, then add your observations in **Journal**.
5. Build your preparation and review routine with **Playbook**, **Trading Plan**, **Rules**, and **Notebook**.

### Tradovate and TopstepX desktop import

Import supports two distinct formats: [Tradovate matched fills](docs/tradovate-csv-import.md) and [TopstepX Trades exports](docs/topstep-csv-import.md). Select the appropriate account and review the preview before confirming. Tradovate reconstructs trades from matched fills; each supported TopstepX row represents one journal trade.

Shared limits are 16 MiB per source file, 64 KiB for header detection, 50,000 CSV records including headers and blank records, 32 fields per record, 4,096 decoded UTF-16 code units per field, and 16,384 raw UTF-16 code units per record. Oversized files are rejected without truncation; export a smaller date range and rebuild the preview.

### Optional: use a separate trial profile

To explore the app without using your default journal, launch it with an absolute, separate data root:

```powershell
$trialRoot = Join-Path $env:TEMP 'PersonalTradingJournal-Trial'
dotnet run --project src/PersonalTradingJournal.Desktop/PersonalTradingJournal.Desktop.csproj --configuration Release --no-build -- --isolated-data-root "$trialRoot"
```

This profile stores journal data under `$trialRoot\PersonalTradingJournal` and keeps credentials separate from your normal profile. Reusing the same path reopens that trial journal.

## Data and Privacy

- **Local storage:** the database, settings, images, logs, and backups live under `%LOCALAPPDATA%\PersonalTradingJournal` by default.
- **Optional AI:** ordinary browsing and journaling do not trigger AI generation. Explicit provider requests or manually shared prompts send the selected review evidence outside the app. See the [Daily Review guide](docs/daily-review.md) for configuration and data-handling details.
- **Credentials:** saved provider keys use Windows DPAPI for the current user and live separately under `%LOCALAPPDATA%\PersonalTradingJournal.Secrets`. They are excluded from journal backups and exports.
- **Backups and exports:** archives and exports are unencrypted and may include sensitive historical journal content. Complete backups include supported image assets; JSON/CSV exports are not restore inputs. Restore currently requires a compatible current-schema archive.

See [Backup and Restore](docs/backup-restore.md) for archive compatibility, included data, and recovery behavior.

## Architecture

The solution separates trading rules and use cases from storage and presentation:

| Project | Responsibility |
| --- | --- |
| `PersonalTradingJournal.Domain` | Trade lifecycle, execution-derived economics, entities, and business rules. |
| `PersonalTradingJournal.Application` | Use cases, analytics, validation, and interfaces to external concerns. |
| `PersonalTradingJournal.Infrastructure` | EF Core/SQLite persistence, file storage, imports, backups, and provider adapters. |
| `PersonalTradingJournal.Desktop` | WPF presentation, MVVM, navigation, themes, and application composition. |
| `PersonalTradingJournal.Contracts` | Boundary contracts kept separate from UI and persistence implementations. |

The stack uses **C# / .NET 10**, **WPF**, **CommunityToolkit.Mvvm**, **EF Core with SQLite**, **Serilog**, and **xUnit**. Package versions are managed centrally in [Directory.Packages.props](Directory.Packages.props).

```text
src/        Application projects
tests/      Domain, Application, Infrastructure, and Desktop tests
docs/       Feature guides, architecture, and verification records
scripts/    Release test runner and performance measurements
.github/    Windows CI workflow
```

See [Architecture](docs/architecture.md) and [Domain Model](docs/domain-model.md) for design decisions and extension boundaries.

## Development and Testing

From the repository root, run the same Release build and test sequence used by CI:

```powershell
dotnet restore PersonalTradingJournal.sln
dotnet build PersonalTradingJournal.sln --configuration Release --no-restore
pwsh -File ./scripts/Test-Release.ps1 -ResultsDirectory TestResults
```

The test runner executes exclusive WPF tests first, followed by the complementary parallel suite. It writes timestamped results, diagnostics, and a summary under `TestResults/`. Desktop tests use native Windows behavior and need a suitable Windows desktop environment; automated passes do not replace manual application checks.

The gate permits only the [explicitly quarantined Rules-view test](docs/rules-view-quarantine.md) to be skipped in parallel Desktop. Other skips and all test failures still fail the gate. A successful gate with this quarantine does not mean all acceptance coverage passed.

[GitHub Actions](.github/workflows/ci.yml) runs on Windows for pushes to `main` and pull requests targeting `main`. See [WPF test diagnostics](docs/ci-wpf-tests.md) and [SQLite test lifetimes](docs/sqlite-test-lifetimes.md) when investigating failures.

## Documentation

| Topic | Guides |
| --- | --- |
| Trades and imports | [Domain model](docs/domain-model.md) · [Tradovate](docs/tradovate-csv-import.md) · [TopstepX](docs/topstep-csv-import.md) |
| Analytics | [Dashboard](docs/dashboard-analytics.md) · [Calendar](docs/trading-calendar.md) · [Breakdown](docs/breakdown-analytics.md) |
| Journaling and reviews | [Daily Journal](docs/daily-journal.md) · [Daily Review](docs/daily-review.md) · [Weekly and Monthly Reviews](docs/period-reviews.md) · [Notebook](docs/notebook.md) |
| Planning | [Playbook](docs/playbook.md) · [Trading Plan](docs/trading-plan.md) · [Trading Rules](docs/trading-rules.md) |
| Storage and recovery | [Persistence](docs/persistence.md) · [Backup and Restore](docs/backup-restore.md) |
| Engineering | [Architecture](docs/architecture.md) · [Desktop UI](docs/desktop-ui.md) · [WPF tests](docs/ci-wpf-tests.md) · [SQLite tests](docs/sqlite-test-lifetimes.md) |
| Visual identity | [Application Branding](docs/application-branding.md) |
| Project history | [Milestones and verification records](docs/development-history.md) |

Feature guides include dated implementation and acceptance notes. Read their current behavior sections alongside the historical entries.

## Contributing

Bug reports, usability feedback, documentation improvements, and focused pull requests are welcome.

1. [Open an issue](https://github.com/yovko93/Personal-Trading-Journal/issues) with reproduction steps, expected behavior, and relevant environment details. Use sanitized examples for trade data and logs.
2. Keep changes focused and follow the [architecture boundaries](docs/architecture.md).
3. Add or update tests when behavior changes, and run the Release checks above.
4. After any major or important change, update this README in the same pull request. Keep the overview, status, features, and setup instructions accurate; place detailed milestone and verification notes in `docs/`.
5. Update the relevant feature guide and describe validation and any remaining limitations in your pull request. See [Repository Instructions](AGENTS.md) for the README maintenance rule.

## License

Licensed under the [MIT License](LICENSE). Copyright © 2026 Yovko Garkov.
