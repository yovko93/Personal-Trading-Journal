# Personal Trading Journal

Personal Trading Journal is a local-first Windows desktop application designed to help traders record, review, analyze, and improve their trading process. The initial focus is futures trading, especially instruments such as NQ and ES, while the architecture is intended to remain extensible to other markets and a possible future SaaS or web version.

The repository currently contains the application foundation, the core trading Domain model, local EF Core/SQLite persistence, the WPF shell and navigation foundation, persisted System/Dark/Light appearance preferences, complete lifecycle management for Trading Accounts, Instruments, Trading Setups, and Trading Mistakes, and manual Trade create/list/view/edit/close/delete workflows. Trades also support local screenshots, Setup classification, Mistake assignments, an authoritative SQLite-paged and sortable browse view, and reviewed Tradovate matched-fills and Topstep closed-row CSV import workflows. Journal workflows, operational analytics, and AI capabilities have not yet been implemented.

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

**Milestone M10 — Tradovate CSV Import: Implemented; final interactive acceptance pending**

**Milestone M11.1 — Topstep CSV Parsing & Normalization: Read-only foundation implemented**

**Milestone M11.2 — Topstep Trade Row Semantics: Read-only closed-row candidates implemented; broker position grouping unverified**

**Milestone M11.3 — Topstep Economics: Read-only Gross/cost/Net reconciliation implemented**

**Milestone M11.4 — Topstep References: Read-only Instrument resolution and explicit Account mapping implemented**

**Milestone M11.5 — Topstep Preview: Read-only composition, validation and snapshot-bound review implemented**

**Milestone M11.6 — Topstep Confirmation: Transactional persistence, account-scoped deduplication and revalidation implemented**

**Milestone M11.7 — Topstep Desktop routing/review/confirmation implemented; interactive acceptance remains unverified**

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

Trade browsing uses fixed 20-row pages with server-side count, sorting, skip, and take. Opened UTC, Trade, Account, Average Prices, and Net P&L are sortable; deterministic Trade-ID tie-breaking and exact decimal sort keys preserve stable page boundaries without SQLite floating-point economics.

The Trades list's **Size** column shows peak simultaneous absolute position quantity (contracts for futures), including for closed Trades. It is calculated from each Trade's persisted execution sequence, using only that Trade's allocated portion of a reversal fill. Scaling back in after a partial close does not add previously closed exposure to Size. Only the current page's execution quantities are fetched, in one batch; no stored values or schema changes are required. Size is display-only, not sorted by the former Open Qty sort key. Whole quantities display without trailing decimals, while meaningful fractional quantities remain exact. Details and close-position controls still use the genuine remaining **Open Qty**.

Each Trades list row shows **View** and a three-dot actions button. Open the actions menu for **Edit** or **Delete**; both actions apply to that row's Trade. Delete retains its confirmation dialog, and keyboard users can open and navigate the menu.

The Trades table keeps header and row columns aligned. Its Opened column shows the full "Opened (New York)" heading and sort indicator, while Size remains compact and Account names can wrap across two lines. Longer account names may be shortened visually; hover or focus the name to read the full name in a tooltip. View and the three-dot actions menu remain visible at the normal window size. At narrower viewport widths the table scrolls horizontally so prices, P&L, status, and row actions remain accessible. The unknown-cost explanation wraps within the P&L column.

The M10 Tradovate CSV Import milestone parses and reconstructs matched fills, resolves existing or proposed Instruments, applies the unified Europe/Sofia source-to-UTC-to-America/New_York time policy, and prepares an explicit Trading Account selection. The Desktop presents the analysis, warnings, blocking diagnostics, Instrument economics, New York trade times, and a read-only preview before showing a non-destructive confirmation dialog. Only an explicitly confirmed, currently valid preview reaches the Application import use case and its atomic SQLite transaction.

M10 covers stages M10.1–M10.7, not just Desktop confirmation. See [Tradovate CSV Import](docs/tradovate-csv-import.md) for the exact required matched-fills headers, reconstruction and confirmation contracts, and [M10 acceptance](docs/m10-acceptance.md) for the final acceptance matrix and remaining interactive checks. Arbitrary execution/order CSV formats are not supported. The current automatic Instrument-creation profile is MNQ; other canonical Instruments need complete compatible catalog metadata, and Import has no arbitrary symbol-mapping editor.

M11.1 adds a separate, read-only **Topstep CSV parser**, not a Desktop import workflow. It validates the `Id,ContractName,EnteredAt,ExitedAt,EntryPrice,ExitPrice,Fees,PnL,Size,Type,TradeDay,TradeDuration,Commissions` schema and returns one normalized record per accepted source row with line-located diagnostics. Entry/exit offsets produce unambiguous UTC instants; the broker's TradeDay date/offset and subsecond duration remain source evidence. Duplicate/conflicting IDs within a file are rejected. Fees, Commissions, and reported PnL stay separate: no Net P&L is computed, rows are not grouped, and no Trades, executions, or reference data are persisted. That parser-only milestone did not add Desktop routing; the current dual-provider flow is described below. See [Topstep Source Rows and Read-Only Candidates](docs/topstep-csv-import.md) for supported formats, row semantics, and unresolved cost questions.

M11.2 retains **one closed-row Trade candidate per Topstep source row**, with exact quantity, entry/exit prices, direction, UTC instants, and the complete original normalized row for traceability. Shared entry timestamps, apparent partial closes, overlaps, TradeDay labels, and Long/Short transitions do not prove common fills or a complete account position. Related intervals receive explicit grouping warnings without merging or splitting the source activity. The supplied 25-row file produces 25 row-based candidates with a conserved closed-quantity total of 72, not 25 verified broker positions or peak exposure of 72. Invalid input blocks candidate preparation; valid candidates remain read-only review evidence, not automatic-import approval. There is no execution-ID fabrication, reversal inference, Tradovate behavior change, Desktop wiring, or persistence.

M11.3 reconciles each Topstep row using caller-verified Instrument point value/currency and an explicit verified USD source interpretation. Reported PnL must equal calculated directional Gross exactly; then Net is Gross minus the CSV's separate Fees and Commissions totals, each counted once. Published fees corroborate the interpretation but are never a rate schedule in application code. Missing costs, mismatched Gross, unknown interpretation/pricing, unsupported currency or rebates, and decimal overflow/precision loss block verified numeric Net. The supplied 25 rows reconcile to **1,241.00 USD Gross − 51.84 USD Fees − 36.00 USD Commissions = 1,153.16 USD Net**, while retaining all M11.2 grouping warnings. This remains read-only economics evidence, not import authorization; Tradovate costs remain unknown. See [Topstep economics](docs/topstep-csv-import.md#m113-gross-fees-commissions-and-net) for sources, formulas, the zero-tolerance policy, and remaining limitations.

M11.4 adds `TopstepReferencePreparationService`, using the existing read-only Account and Instrument readers. Each request requires an explicitly selected **USD Account with ProviderName `Topstep`** (trimmed, case-insensitive exact match); there is no account inference, remembered fallback, or provider alias. Inactive explicitly selected Accounts and uniquely verified inactive Instruments are allowed for historical review with warnings, never reactivated. Contract root/month/year tokens remain traceable: MNQ is checked against reviewed CME/USD specifications (0.25 tick, 0.50 tick value, 2 point value, quarterly contracts). A missing MNQ produces a complete **read-only proposal requiring later approval**, not an Instrument. Multiple canonical matches always block before activity/specification filtering; wrong specifications also block. Other roots require an existing complete catalog entry plus explicit verification bound to the exact source contract and full catalog snapshot, never evidence inferred from PnL. Resolved pricing feeds M11.3 without changing rows or costs. Every run rereads reference data; the result retains matching-set/account snapshots for M11.5 review and M11.6 revalidation, plus separate provider/account/source-row identity. The sample retains 25 candidates and four grouping warnings. No Topstep persistence, deduplication, confirmation, or Desktop integration is added. See [reference resolution and account policy](docs/topstep-csv-import.md#m114-instrument-resolution-and-explicit-account-mapping).

Topstep's read-only `TopstepImportPreviewBuilder.BuildAsync` composes parsing, closed-row reconstruction, current reference resolution and economics. Supply a complete CSV stream, file name, explicitly selected Account and verified cost interpretation. The preview exposes exact row values, counts, account/Instrument snapshots, complete proposals and stage-specific diagnostics with affected source rows. For the supported TopstepX Trades export, **one source row is one Trade**. Grouping/source-completeness warnings are technical provenance, not user acknowledgment requirements. Confirmation binds to a versioned fingerprint of source bytes and the reviewed snapshot; any Instrument proposal must be explicitly approved through the final dialog, and errors cannot be overridden. Rebuild after changing the file/account/references; concurrent requests on the same builder are rejected until the active request finishes or is cancelled. Preview persists nothing. The Desktop routes explicitly selected TopstepX files to this preview; interactive acceptance remains pending. See [Topstep preview and review requirements](docs/topstep-csv-import.md#m115-read-only-preview-and-validation).

M11.6 adds `ImportTopstepTradesUseCase` and a transactional SQLite store. Confirmation requires the exact preview snapshot and approval of displayed Instrument proposals plus a freshly opened complete source stream; there are no warning acknowledgments. Inside one transaction it rebuilds the preview against current account/Instrument data, rejects changed source or references (including new ambiguous matches), and then atomically persists new closed-row Trades, derived entry/exit records, browse projections, approved Instruments and source provenance. Each row's full Fees and Commissions totals are allocated once to its derived exit; entry costs are zero allocation, not evidence of free broker fills. External execution/order IDs remain absent. Domain Gross/Net must exactly equal reviewed economics. Tradovate's unknown-cost behavior is unchanged.

Topstep replay identity is **Topstep + explicitly selected destination Account + case-sensitive source Id**. Exact previously imported economics are skipped; changed facts under an existing key block the entire batch without overwriting a Trade. Mixed new/duplicate rows commit together. An all-duplicate replay returns `NoChanges`; if the first import created an Instrument, rebuild and review against that now-existing Instrument first. Cancellation/failure rolls back all writes. Committed imports invalidate retained Trades, and Instruments when created, before the next navigation load. The additive ledger migration follows existing Trade hard-delete behavior: deleting a Trade removes its import identity, allowing an explicit later reimport. Snapshot version `topstep-preview-v3` canonicalizes equivalent decimal scales without rounding or weakening the exact source-byte check; older previews must be rebuilt. See [Topstep confirmation and identity](docs/topstep-csv-import.md#m116-transactional-confirmation-and-deduplication).

### Tradovate and TopstepX Desktop import

1. Open **Import** and choose **Tradovate** or **TopstepX** in **Import source**. There is no default choice; **Select CSV** is disabled until a source is chosen.
2. Select the CSV. Its header must match the chosen source. A wrong source gives one `CSV_SOURCE_MISMATCH` message; unknown, mixed or malformed headers give one `CSV_FORMAT_UNSUPPORTED` message. Neither case invokes the other provider's parser.
3. For TopstepX, explicitly choose a **USD Account with ProviderName Topstep**, then **Build Preview**. The supported Trades export imports **each row individually as one Trade**: the supplied 25 rows yield 25 candidates. Review quantities, UTC times, two-decimal displayed prices, Gross, Fees, Commissions and Net. MNQ is the built-in verified profile; other roots requiring independent attestations remain blocked rather than guessed.
4. Choose **Import Trades** under **Confirm import**. No grouping/source-completeness or Instrument-approval checkboxes are required. If creation is needed, the final dialog shows every proposed Instrument's exact symbol, name, asset class, exchange, currency, tick size/value, point value and evidence. Accepting that dialog explicitly approves those specifications and the import. **Cancel** writes nothing and retains the preview.
5. `Imported` and `NoChanges` clear the active preview and keep final counts under the unnumbered **Confirm import** heading. Re-select the CSV/account and rebuild for replay; a now-existing Instrument replaces the original proposal only through a fresh preview.

Changing source clears file/account selection, previews, diagnostics, confirmation and outcome; changing CSV clears the previous file's state and account. Changing Account clears the preview/diagnostics/outcome. The selected provider never changes automatically. Source/reference conflicts disable stale confirmation and require a fresh preview; no Instrument is silently substituted. **Cancel operation** forwards cancellation where supported, and disabled commands plus an in-flight guard prevent duplicate submissions. Tradovate retains its existing matched-fill analysis, warnings and import rules.

The supplied-file automated Desktop command-path check imported **25 Trades** and replayed as **NoChanges with 25 skipped**, without grouping checkboxes. Persisted Gross/Fees/Commissions/Net were **1,241.00 / 51.84 / 36.00 / 1,153.16 USD**. Cancellation made zero import-data writes; the same session subsequently built a Tradovate preview. The full Release suite passes **1,984 tests** (400 Domain, 405 Application, 651 Infrastructure, 528 Desktop), with zero build warnings/errors. These are automated results, not interactive UI acceptance; see the [M11 acceptance matrix and manual checklist](docs/m11-acceptance.md).

For isolated verification of the **same production executable**, launch it with `--isolated-data-root <absolute-test-directory>`. It uses `<absolute-test-directory>/PersonalTradingJournal/` for its database, settings, logs and screenshots, without copying or opening the normal journal. This opt-in must point to separate test storage; do not use an alias/junction to the real data. Omit the flag for normal operation. The exact Release executable path and the remaining manual acceptance gates are recorded in the acceptance document.

To import a supported Tradovate matched-fills export:

1. Open **Import** and choose **Select CSV**.
2. Review the analysis and Instrument resolution, then explicitly select the destination Trading Account.
3. Choose **Build Preview** and inspect the summary, proposed Instruments, candidate Trades, New York timestamps, warnings, and errors. Candidate weighted average entry and exit prices display two culture-aware decimal places; underlying preview and imported execution prices retain full precision.
4. Choose **Import Trades**, review the final confirmation, and accept it to persist the import.
5. Review the Imported, Duplicates Skipped, and Instruments Created counts. After an Imported or No changes result, temporary analysis, Instrument resolution, Trade candidates, and diagnostics are cleared; the source-file section and final outcome remain visible under the `Confirm import` heading. Select another CSV to start a new preview and explicitly choose its Trading Account. Opening Trades or Instruments after a committed import reloads their authoritative data.

If the selected Account disappears before confirmation, recovery guidance remains visible below the account selector even though the obsolete preview is cleared. Select an available Account and choose **Build Preview** again. Cancelling the confirmation dialog leaves the reviewed preview unchanged and performs no import.

Imported commission and fee values remain `null` (unknown, not known zero) because the supported export does not contain them. Exact fill identities are durable per selected PTJ Account: exact duplicate Trades are skipped, mixed new/duplicate imports report both counts, and unsafe partial overlaps block the whole operation. A reference-data change after preview never causes silent Instrument substitution; the user is directed to rebuild the preview, which re-runs Instrument resolution without reparsing the CSV. Unsupported or ambiguous Instrument metadata remains blocking and must be resolved in reference data before import.

Duplicate counts are determined during transactional confirmation, not by the read-only preview. Commission/fee source identification and import remain explicitly deferred. The manual Edit form refuses unknown costs and richer imported execution lifecycles instead of fabricating costs or flattening the Trade. The import does not prove source completeness, archive raw CSV files, persist source-reported P&L, or provide an import-history screen.

An unambiguous position reversal can allocate one real broker fill between closing the current Trade and opening the opposite Trade. Both allocations retain the same broker fill identity, price, and timestamp; their quantities sum exactly to the unchanged source fill quantity. The import ledger stores allocation ordinals and immutable source economics, so replay skips both Trades without duplicating fills or allocations. Existing identity-only ledger rows remain valid through an additive migration; their unknown historical economics are not inferred from subsequently edited Trades. Matched-row evidence is partitioned between reversal candidates so preview Source P&L is not double-counted. Commission and fees remain unknown.

Equal timestamps alone do not block reconstruction. At one timestamp, matched quantities paired with earlier fills close those earlier lots before unrelated quantities paired with later fills open new lots; a source-supported crossing fill retains its closing/opening allocations. This is a matched-lot lifecycle rule, not a claim about the broker's unrecorded sub-second sequence. Harmless within-lifecycle ties may use a stable fill-ID order only when quantities, Trade membership, weighted prices, and P&L cannot change. For directly matched buy/sell fills sharing the same timestamp, reconstruction checks feasible allocations: if direction, boundaries, or allocation quantities remain unresolved, `TIMESTAMP_ORDER_AMBIGUOUS` names the symbol, timestamp, affected fills, and missing sequence/evidence. The order search is bounded and cancellable; an unresolved search also blocks safely. Source P&L is preserved as reported evidence, never rewritten or used by itself to invent an order. Contradictory matched quantities also remain blocking.

**Build Preview** requires both an eligible reconstruction and an explicitly selected Account. A reconstruction blocker keeps it disabled; choosing an Account alone cannot bypass that blocker. Selecting a different CSV clears the obsolete preview and re-analyzes the source; changing the Account invalidates the old preview and requires rebuilding it. Analysis and preview never persist Trades, executions, allocations, or proposed Instruments. `SOURCE_COMPLETENESS_UNVERIFIED` remains a separate warning because matched fills alone cannot prove complete execution history.

Trades list rows and Details show Gross P&L with its currency for closed Trades when it can be calculated. Each known Gross and Net amount is colored by its own sign; zero and unavailable amounts remain neutral. Net P&L stays `—` when costs are unknown, with an explicit commission/fees explanation; a calculated zero is displayed as `0`. Trades list row color follows known Net P&L, or known Gross P&L when Net is unavailable. A green Gross amount or row does not establish a positive Net result when costs are unknown. Average entry and exit prices display exactly two culture-aware decimal places in the list and Details; full stored and calculation precision is unchanged. The imported MNQ sample with two contracts, a 1.25-point gain, and a 2 USD point value has 5 USD Gross P&L and unknown Net P&L.

The Desktop Theme System adds one semantic design system backed by parity-checked Dark and Light resource dictionaries. Theme-sensitive brushes update live through `DynamicResource`. Settings offers System, Dark, and Light; System follows the Windows application theme, while the compact header toggle switches the effective appearance to an explicit opposite preference. `%LocalAppData%\PersonalTradingJournal\settings.json` restores the preferred mode—not its resolved appearance—before the main window is shown. Missing or invalid settings safely fall back to System.

The Desktop creation workflows share a compact form language for Manual Trades, Accounts, Instruments, Trading Setups, and Trading Mistakes. Consistent section hierarchy, field labels, optional markers, restrained helper text, visible focus treatment, semantic feedback, and primary/secondary actions improve scanability without changing validation or persistence behavior.

Eight of the 19 shell destinations are concrete: Dashboard, Trades, Import, Accounts, Instruments, Setups, Mistakes, and Settings. Dashboard remains presentation-only, Settings owns appearance preference, and the other six are functional data-backed pages. The other 11 destinations remain placeholders.

The fixed-width sidebar renders all 19 destinations from one Desktop-owned navigation catalog. Dashboard and Notebook remain top-level, four labeled feature groups can be collapsed independently, and Accounts, Instruments, and Settings remain standalone utilities below a divider. Every destination uses a project-owned vector icon and the existing semantic theme resources in both Dark and Light modes.

M10 implementation and automated coverage are available. Final acceptance remains open until the interactive checks in the acceptance record are observed; a subsequent milestone is not selected here.

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

Topstep coverage includes preview, snapshot-bound confirmation, replay/conflicts, reference changes, concurrency, rollback and postcommit retained-data invalidation. All **232 focused Topstep tests** and the **1,952-test Release suite** pass (400 Domain, 405 Application, 639 Infrastructure, 508 Desktop), with no failures/skips and zero build warnings/errors. The supplied-file acceptance check used an isolated migrated SQLite database: preview and rejected confirmations left its bytes unchanged; confirmation imported **25 closed-row Trades, 50 derived executions and one Instrument**; a fresh reviewed replay returned **NoChanges with 25 skipped**. Fresh persisted Domain aggregates reconcile to **1,241.00 USD Gross, 51.84 Fees, 36.00 Commissions and 1,153.16 Net**. The real journal was not opened and customer data/identifiers are not tracked. These are API/SQLite acceptance checks, not interactive Topstep UI acceptance. The Desktop routing correction and remaining acceptance gates are described above.

```powershell
dotnet test PersonalTradingJournal.sln
```

The M10 acceptance baseline contains 1,720 passing tests: 400 Domain, 312 Application, 504 Infrastructure, and 504 Desktop tests, with zero failed and zero skipped. Desktop tests exercise presentation, ViewModel orchestration, import confirmation state, isolated SQLite acceptance, paging/sorting, lifecycle actions, navigation, settings persistence, Windows theme resolution, theme switching, and project-owned XAML-resource behavior. These automated tests do not establish interactive WPF acceptance; see the [acceptance record](docs/m10-acceptance.md).

The M11.1 parsing baseline adds 65 synthetic Topstep contract/parser tests: the full suite passes 1,785 tests (400 Domain, 317 Application, 564 Infrastructure, 504 Desktop). The supplied Topstep export was parsed read-only with 25 accepted rows, 0 rejected rows, and no diagnostics; it is not stored as a repository fixture. Topstep import and Desktop acceptance remain outside M11.1.

The M11.2 closed-row candidate baseline adds 35 tests, bringing the full suite to **1,820 passing tests** (400 Domain, 320 Application, 596 Infrastructure, 504 Desktop). Its synthetic coverage protects row-level conservation, ambiguous grouping warnings, direction changes, source traceability, cancellation, and Domain representability without claiming recovered broker execution history.

The M11.3 economics baseline adds 37 tests: **1,857 tests pass** (400 Domain, 348 Application, 605 Infrastructure, 504 Desktop), including 137 focused Topstep tests. Reconciliation covers exact Gross, separate actual costs, nullable unverified Net, source/pricing/interpretation gates, Domain parity, and decimal failure diagnostics. The local 25-row sample reconciles fully without persistence or customer data being added to Git.

The M11.4 reference-preparation baseline adds 35 tests: **1,892 tests pass** (400 Domain, 379 Application, 609 Infrastructure, 504 Desktop), including **172 focused Topstep tests**. Coverage includes verified/existing/proposed/inactive/ambiguous Instruments, incorrect metadata, explicit/missing/deleted/incompatible Accounts, reference changes, unchanged source economics/provenance, cancellation, and real readers against migrated SQLite opened read-only. The supplied 25-row CSV passes preparation with verified MNQ and an explicit compatible account; negative reference scenarios block as designed. Release build has zero warnings/errors, with no failed/skipped tests; `git diff --check` passes. This does not claim Topstep Desktop or confirmation acceptance.

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
