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

**Milestone M11.7 — TopstepX automated acceptance passed; empty Diagnostics manually verified by the user; remaining interactive gates open**

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
- a live read-only analytics Dashboard;
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

Trading Setup is the single reusable trade-pattern classification. The overlapping Strategy concept was intentionally removed, and no Strategy catalog or Trade Strategy assignment exists in the current architecture. Dashboard Setup performance breakdowns are available; Mistake performance analytics are not implemented.

The Entity Lifecycle & CRUD UX milestone completes consistent View/Edit/Delete presentation and explicit entity-specific update and deletion workflows. Accounts, Instruments, Trading Setups, and Trading Mistakes may be hard deleted only while unused; referenced records remain editable and can be deactivated without breaking historical Trades. Trades support correction through the Domain aggregate and confirmed hard deletion of their owned database records, followed by best-effort physical screenshot cleanup.

Trade browsing uses fixed 20-row pages with server-side count, sorting, skip, and take. Opened UTC, Trade, Account, Average Prices, and Net P&L are sortable; deterministic Trade-ID tie-breaking and exact decimal sort keys preserve stable page boundaries without SQLite floating-point economics.

The Trades list's **Size** column shows peak simultaneous absolute position quantity (contracts for futures), including for closed Trades. It is calculated from each Trade's persisted execution sequence, using only that Trade's allocated portion of a reversal fill. Scaling back in after a partial close does not add previously closed exposure to Size. Only the current page's execution quantities are fetched, in one batch; no stored values or schema changes are required. Size is display-only, not sorted by the former Open Qty sort key. Whole quantities display without trailing decimals, while meaningful fractional quantities remain exact. Details and close-position controls still use the genuine remaining **Open Qty**.

Each Trades list row shows **View** and a three-dot actions button. Open the actions menu for **Edit** or **Delete**; both actions apply to that row's Trade. Delete retains its confirmation dialog, and keyboard users can open and navigate the menu.

The Trades table keeps header and row columns aligned. Its Opened column shows the full "Opened (New York)" heading and sort indicator, while Size remains compact and Account names can wrap across two lines. Longer account names may be shortened visually; hover or focus the name to read the full name in a tooltip. View and the three-dot actions menu remain visible at the normal window size. At narrower viewport widths the table scrolls horizontally so prices, P&L, status, and row actions remain accessible; an ordinary mouse wheel over the table scrolls the page vertically. The unknown-cost explanation wraps within the P&L column.

The M10 Tradovate CSV Import milestone parses and reconstructs matched fills, resolves existing or proposed Instruments, applies the unified Europe/Sofia source-to-UTC-to-America/New_York time policy, and prepares an explicit Trading Account selection. The Desktop presents the analysis, warnings, blocking diagnostics, Instrument economics, New York trade times, and a read-only preview before showing a non-destructive confirmation dialog. Only an explicitly confirmed, currently valid preview reaches the Application import use case and its atomic SQLite transaction.

M10 covers stages M10.1–M10.7, not just Desktop confirmation. See [Tradovate CSV Import](docs/tradovate-csv-import.md) for the exact required matched-fills headers, reconstruction and confirmation contracts, and [M10 acceptance](docs/m10-acceptance.md) for the final acceptance matrix and remaining interactive checks. Arbitrary execution/order CSV formats are not supported. The current automatic Instrument-creation profile is MNQ; other canonical Instruments need complete compatible catalog metadata, and Import has no arbitrary symbol-mapping editor.

The following M11 stage descriptions distinguish each stage's responsibility from the complete workflow below. Earlier grouping warnings remain technical reconstruction provenance only: the current TopstepX preview imports one Trade per valid row without warning acknowledgments. Costs are reconciled by M11.3, not guessed by the parser.

M11.1 adds a separate, read-only **Topstep CSV parser**, not a Desktop import workflow. It validates the `Id,ContractName,EnteredAt,ExitedAt,EntryPrice,ExitPrice,Fees,PnL,Size,Type,TradeDay,TradeDuration,Commissions` schema and returns one normalized record per accepted source row with line-located diagnostics. Entry/exit offsets produce unambiguous UTC instants; the broker's TradeDay date/offset and subsecond duration remain source evidence. Duplicate/conflicting IDs within a file are rejected. Fees, Commissions, and reported PnL stay separate: no Net P&L is computed, rows are not grouped, and no Trades, executions, or reference data are persisted. That parser-only milestone did not add Desktop routing; the current dual-provider flow is described below. See [TopstepX CSV import](docs/topstep-csv-import.md) for supported formats, row semantics, verified cost interpretation, and limitations.

M11.2 retains **one closed-row Trade candidate per Topstep source row**, with exact quantity, entry/exit prices, direction, UTC instants, and the complete original normalized row for traceability. Shared entry timestamps, apparent partial closes, overlaps, TradeDay labels, and Long/Short transitions do not prove common fills or a complete account position. Related intervals receive explicit grouping warnings without merging or splitting the source activity. The supplied 25-row file produces 25 row-based candidates with a conserved closed-quantity total of 72, not 25 verified broker positions or peak exposure of 72. Invalid input blocks candidate preparation; valid candidates remain read-only review evidence, not automatic-import approval. There is no execution-ID fabrication, reversal inference, Tradovate behavior change, Desktop wiring, or persistence.

M11.3 reconciles each Topstep row using caller-verified Instrument point value/currency and an explicit verified USD source interpretation. Reported PnL must equal calculated directional Gross exactly; then Net is Gross minus the CSV's separate Fees and Commissions totals, each counted once. Published fees corroborate the interpretation but are never a rate schedule in application code. Missing costs, mismatched Gross, unknown interpretation/pricing, unsupported currency or rebates, and decimal overflow/precision loss block verified numeric Net. The supplied 25 rows reconcile to **1,241.00 USD Gross − 51.84 USD Fees − 36.00 USD Commissions = 1,153.16 USD Net**, while retaining all M11.2 grouping warnings. This remains read-only economics evidence, not import authorization; Tradovate costs remain unknown. See [Topstep economics](docs/topstep-csv-import.md#m113-gross-fees-commissions-and-net) for sources, formulas, the zero-tolerance policy, and remaining limitations.

M11.4 adds `TopstepReferencePreparationService`, using the existing read-only Account and Instrument readers. Each request requires an explicitly selected **USD Account with ProviderName `Topstep`** (trimmed, case-insensitive exact match); there is no account inference, remembered fallback, or provider alias. Inactive explicitly selected Accounts and uniquely verified inactive Instruments are allowed for historical review with warnings, never reactivated. Contract root/month/year tokens remain traceable: MNQ is checked against reviewed CME/USD specifications (0.25 tick, 0.50 tick value, 2 point value, quarterly contracts). A missing MNQ produces a complete **read-only proposal requiring later approval**, not an Instrument. Multiple canonical matches always block before activity/specification filtering; wrong specifications also block. Other roots require an existing complete catalog entry plus explicit verification bound to the exact source contract and full catalog snapshot, never evidence inferred from PnL. Resolved pricing feeds M11.3 without changing rows or costs. Every run rereads reference data; the result retains matching-set/account snapshots for M11.5 review and M11.6 revalidation, plus separate provider/account/source-row identity. The sample retains 25 candidates and four grouping warnings. No Topstep persistence, deduplication, confirmation, or Desktop integration is added. See [reference resolution and account policy](docs/topstep-csv-import.md#m114-instrument-resolution-and-explicit-account-mapping).

Topstep's read-only `TopstepImportPreviewBuilder.BuildAsync` composes parsing, closed-row reconstruction, current reference resolution and economics. Supply a complete CSV stream, file name, explicitly selected Account and verified cost interpretation. The preview exposes exact row values, counts, account/Instrument snapshots, complete proposals and stage-specific diagnostics with affected source rows. For the supported TopstepX Trades export, **one source row is one Trade**. Grouping/source-completeness warnings are technical provenance, not user acknowledgment requirements. Confirmation binds to a versioned fingerprint of source bytes and the reviewed snapshot; any Instrument proposal must be explicitly approved through the final dialog, and errors cannot be overridden. Rebuild after changing the file/account/references; concurrent requests on the same builder are rejected until the active request finishes or is cancelled. Preview persists nothing. The Desktop routes explicitly selected TopstepX files to this preview; interactive acceptance remains pending. See [Topstep preview and review requirements](docs/topstep-csv-import.md#m115-read-only-preview-and-validation).

M11.6 adds `ImportTopstepTradesUseCase` and a transactional SQLite store. Confirmation requires the exact preview snapshot and approval of displayed Instrument proposals plus a freshly opened complete source stream; there are no warning acknowledgments. Inside one transaction it rebuilds the preview against current account/Instrument data, rejects changed source or references (including new ambiguous matches), and then atomically persists new closed-row Trades, derived entry/exit records, browse projections, approved Instruments and source provenance. Each row's full Fees and Commissions totals are allocated once to its derived exit; entry costs are zero allocation, not evidence of free broker fills. External execution/order IDs remain absent. Domain Gross/Net must exactly equal reviewed economics. Tradovate's unknown-cost behavior is unchanged.

Topstep replay identity is **Topstep + explicitly selected destination Account + case-sensitive source Id**. Exact previously imported economics are skipped; changed facts under an existing key block the entire batch without overwriting a Trade. Mixed new/duplicate rows commit together. An all-duplicate replay returns `NoChanges`; if the first import created an Instrument, rebuild and review against that now-existing Instrument first. Cancellation/failure rolls back all writes. Committed imports invalidate retained Trades, and Instruments when created, before the next navigation load. The additive ledger migration follows existing Trade hard-delete behavior: deleting a Trade removes its import identity, allowing an explicit later reimport. Snapshot version `topstep-preview-v3` canonicalizes equivalent decimal scales without rounding or weakening the exact source-byte check; older previews must be rebuilt. See [Topstep confirmation and identity](docs/topstep-csv-import.md#m116-transactional-confirmation-and-deduplication).

### Tradovate and TopstepX Desktop import

Both sources share these resource limits. They apply to actual counted reads, including non-seekable streams, not just file metadata:

| Resource | Supported maximum |
| --- | ---: |
| Complete source (including UTF-8 BOM/newlines) | 16 MiB (16,777,216 bytes) |
| Header detection, including leading blank records/BOM | 64 KiB (65,536 bytes), through the first nonblank record |
| CSV records, including header and blank records | 50,000 |
| Fields per record, including empty fields | 32 |
| Decoded field length | 4,096 UTF-16 code units |
| Raw record length, including quotes, commas and embedded newlines | 16,384 UTF-16 code units, excluding the terminating newline |

Every limit must be satisfied; quoted multiline content is one CSV record, while blank lines outside quotes count as records. Parsing is incremental and strict UTF-8 (optional UTF-8 BOM). Header detection stops at the first nonblank record without parsing the data rows. `CSV_LIMIT_EXCEEDED` means **CSV exceeds the supported limit**, not an unsupported schema. Export a smaller date range, reduce unnecessary extra columns/oversized fields or excessive blank lines, then select the complete CSV again. The app never imports a truncated prefix. TopstepX rechecks source bytes when reopening for preview and transactional confirmation; oversized/changed sources require a fresh valid preview. Cancellation/limit failure leaves the workflow reusable and cannot save a partial import. Limits do not change within-budget pricing, costs or duplicate identity rules.

1. Open **Import** and choose **Tradovate** or **TopstepX** in **Import source**. There is no default choice; **Select CSV** is disabled until a source is chosen.
2. Select the CSV. Its header must match the chosen source. A wrong source gives one `CSV_SOURCE_MISMATCH` message; unknown, mixed or malformed headers give one `CSV_FORMAT_UNSUPPORTED` message. Neither case invokes the other provider's parser.
3. For TopstepX, explicitly choose a **USD Account with ProviderName Topstep**, then **Build Preview**. The supported Trades export imports **each row individually as one Trade**: the supplied 25 rows yield 25 candidates. Review quantities, UTC times, two-decimal displayed prices, Gross, Fees, Commissions and Net. MNQ is the built-in verified profile; other roots requiring independent attestations remain blocked rather than guessed.
4. Choose **Import Trades** under **Confirm import**. No grouping/source-completeness or Instrument-approval checkboxes are required. If creation is needed, the final dialog shows every proposed Instrument's exact symbol, name, asset class, exchange, currency, tick size/value, point value and evidence. Accepting that dialog explicitly approves those specifications and the import. **Cancel** writes nothing and retains the preview.
5. `Imported` and `NoChanges` clear the active preview and keep final counts under the unnumbered **Confirm import** heading. Re-select the CSV/account and rebuild for replay; a now-existing Instrument replaces the original proposal only through a fresh preview.

Changing source clears file/account selection, previews, diagnostics, confirmation and outcome; changing CSV clears the previous file's state and account. Changing Account clears the preview/diagnostics/outcome. The selected provider never changes automatically. Source/reference conflicts disable stale confirmation and require a fresh preview; no Instrument is silently substituted. **Cancel operation** forwards cancellation where supported, and disabled commands plus an in-flight guard prevent duplicate submissions. Tradovate retains its existing matched-fill analysis, warnings and import rules.

The entire **Diagnostics** card (heading, border and spacing) is hidden when there are no entries, independently of source. Real warnings/errors remain visible; format and confirmation recovery messages are separate from this card. File/source/account changes and completed imports update visibility with the diagnostic content. The user manually verified the empty-card visual fix; this is not evidence for other interactive scenarios.

After a committed TopstepX import, the Desktop shell consumes the shared change generations and refreshes **Trades if currently active**, or **Instruments if currently active and an Instrument was created**. Notification is independent of the Import page's transient preview: navigating away/back or cancelling presentation after commit cannot suppress it. UI updates run on the owning WPF dispatcher. A read started before invalidation discards its obsolete result (or failure) and rereads under the existing load gate, so it cannot replace committed data or mark a stale cache current. Later navigation retains generation-based invalidation without duplicate refreshes. NoChanges, Blocked, rolled-back cancellation and failed transactions do not trigger refresh; Tradovate keeps its existing notification path. Deterministic delayed-import/load and dispatcher tests cover these races; this is automated coverage, not interactive UI acceptance.

The final supplied-file automated Desktop command-path check imported **25 Trades** and replayed as **NoChanges with 25 skipped**, without grouping checkboxes. Persisted Gross/Fees/Commissions/Net were **1,241.00 / 51.84 / 36.00 / 1,153.16 USD**. Cancelling the final dialog made zero import-data writes; the same session subsequently built a Tradovate preview. That supplied-file audit passed **1,986 Release tests** (400 Domain, 405 Application, 651 Infrastructure, 530 Desktop); **462 focused import tests** passed, Release build has zero warnings/errors, and the EF model has no pending migration changes. These are automated results, not interactive UI acceptance; see the [M11 acceptance matrix and Windows checklist](docs/m11-acceptance.md) for the separately recorded user verification and remaining gates.

The active-view refresh follow-up adds **19 deterministic Desktop regression cases**. Its **291 focused tests** and **2,005-test full Release suite** pass (400 Domain, 405 Application, 651 Infrastructure, 549 Desktop), with zero build warnings/errors and a clean `git diff --check`. The dispatcher checks use automated STA test loops, not an interactive application session; the outstanding manual acceptance gates remain unchanged.

For isolated verification of the **same production executable**, launch it with `--isolated-data-root <absolute-test-directory>`. It uses `<absolute-test-directory>/PersonalTradingJournal/` for its database, settings, logs and screenshots, without copying or opening the normal journal. This opt-in must point to separate test storage; do not use an alias/junction to the real data. Omit the flag for normal operation. The exact Release executable path and the remaining manual acceptance gates are recorded in the acceptance document.

To import a supported Tradovate matched-fills export:

1. Open **Import**, choose **Tradovate** in **Import source**, then choose **Select CSV**.
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

Trades list rows and Details show Gross P&L with its currency. Closed Trades display authoritative Net when known; otherwise known Gross is shown as **estimated Net**, with **Estimated — commission/fees unknown**. Unknown Gross has no numeric estimate; open Trades still have no final Net. No costs are deducted from an estimate, even an individually known component; stored costs and authoritative Net remain unchanged/null. Explicit zero costs and zero Net remain verified zero. Each displayed amount retains its sign color; row color follows authoritative Net when known, otherwise Gross. Net sorting remains by authoritative Net (estimates retain unknown-Net placement), explained by the header tooltip. Average prices retain exactly two culture-aware displayed decimals without changing stored precision. The MNQ example with 5 USD Gross and unknown costs therefore shows 5 USD **estimated** Net, not verified Net.

The Desktop Theme System adds one semantic design system backed by parity-checked Dark and Light resource dictionaries. Theme-sensitive brushes update live through `DynamicResource`. Settings offers System, Dark, and Light; System follows the Windows application theme, while the compact header toggle switches the effective appearance to an explicit opposite preference. `%LocalAppData%\PersonalTradingJournal\settings.json` restores the preferred mode—not its resolved appearance—before the main window is shown. Missing or invalid settings safely fall back to System.

The Desktop creation workflows share a compact form language for Manual Trades, Accounts, Instruments, Trading Setups, and Trading Mistakes. Consistent section hierarchy, field labels, optional markers, restrained helper text, visible focus treatment, semantic feedback, and primary/secondary actions improve scanability without changing validation or persistence behavior.

Nine of the 19 shell destinations are concrete: Dashboard, Calendar, Trades, Import, Accounts, Instruments, Setups, Mistakes, and Settings. Dashboard shows read-only analytics, Calendar provides a navigable month grid, Settings owns appearance preference, and the other six are functional data-backed pages. The other 10 destinations remain placeholders.

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

Topstep coverage includes preview, snapshot-bound confirmation, replay/conflicts, reference changes, concurrency, rollback and postcommit retained-data invalidation. The historical M11.6 baseline passed **232 focused Topstep tests** and **1,952 full Release tests** (400 Domain, 405 Application, 639 Infrastructure, 508 Desktop). Its supplied-file acceptance used isolated migrated SQLite: preview and rejected confirmations left its bytes unchanged; confirmation imported **25 closed-row Trades, 50 derived executions and one Instrument**; a fresh replay returned **NoChanges with 25 skipped**. Current M11.7 counts and rerun evidence are reported above and in [M11 acceptance](docs/m11-acceptance.md). Historical baseline counts below are not the current suite total. No customer data/identifiers are tracked; API/SQLite checks do not constitute interactive UI acceptance.

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

## Dashboard Analytics Foundation

Read-only Application metric contracts and rules in `Application/Analytics` preserve authoritative facts, historical currency boundaries and strict Gross/Net coverage. Incomplete strict Net exposes a verified subtotal but no complete total, ratios or outcome averages. A separate `EffectiveNet` basis uses explicitly estimated Gross amounts where closed-Trade Net is unknown. `IsEstimated`, estimated/verified Trade counts and numeric coverage accompany every total, Win Rate, Average Win/Loss, Profit Factor, Setup, daily/weekly period and cumulative result. Any result containing estimates must be labeled estimated, even with complete numeric coverage. Avg R remains unavailable without authoritative initial risk.

Outcome metrics share the same accumulated statistics on each basis: Win Rate is wins divided by all selected closed Trades (including break-evens), Average Win is positive P&L divided by win count, Average Loss is positive loss magnitude divided by loss count, and Profit Factor is positive P&L divided by loss magnitude. Break-evens do not enter either average. Empty/incomplete populations and averages with no matching wins/losses have explicit unavailable states, not invented zero; Profit Factor never returns infinity. For +100, −40, 0, +20 USD the results are 50%, 60 USD, 40 USD and 3 respectively. Calculations retain decimal precision; display rounding is deferred.

`IDashboardAnalyticsReader.GetAsync(DashboardAnalyticsQuery)` now reads all matching closed Trades from the persisted Domain-derived browse projection and feeds these rules. Optional Account/Instrument IDs and inclusive New York closure dates filter in SQL; dates become DST-aware half-open UTC bounds. Every call uses a fresh context, with no retained Account or result, paging, execution loading or query-time writes. Invalid filter IDs/date ranges produce parameter-specific argument exceptions before database access; valid IDs with no matches return an empty snapshot.

Each currency includes ordered daily and Monday–Sunday weekly series. Empty periods are omitted; genuine zero remains zero. Cumulative values start from the filtered selection, not account balance. Unknown Net keeps later strict cumulative Net unavailable; effective cumulative Net can remain numeric but stays labeled estimated while that Trade is included. Supplying real costs replaces the estimate on the next read; deletes and committed imports also appear on subsequent queries. Weeks cut by filters contain only selected Trades. Decimal overflow rejects the calculation rather than returning partial results.

Chart-ready `DailyPnl` and `CumulativeRealizedPnl` points reuse those daily snapshots, retaining dates, coverage and estimate provenance. The Dashboard's **Cumulative Realized P&L** panel represents realized Trade P&L, not account equity: starting balance, deposits/withdrawals and open valuation are not available. Setup breakdowns include current names/activity and explicit unclassified or missing-reference states, without discarding historical results. One filtered, no-tracking query left-joins Setup metadata; no per-point/Setup query is made. Renaming and classification edits appear on the next read. Empty selections have no currency/point buckets; unclassified-only results remain visible; unknown verified Net stays null rather than zero. See the analytics contracts for these distinct empty states.

The Dashboard loads live read-only summary cards, daily P&L and **Cumulative Realized P&L** charts, Setup breakdowns and an independently queried latest-ten Trades list (including open Trades). It defaults to **All accounts / All history**. Account is the first control below the Dashboard heading, above the wrapping period/currency/actions and Date Range controls. The Account selector includes inactive accounts, labeled as such; selecting one scopes every analytics section and Recent Trades. An unavailable selected account clears results and asks for an explicit replacement, never silently switching to All accounts. Instruments remain unfiltered. An explicit currency selector scopes analytics without combining currencies. Best/Worst Day rank daily EffectiveNet, with earliest-date tie-breaking and estimate provenance; incomplete daily values prevent a misleading subset ranking. Net P&L and Best/Worst Day amounts use sign colors from their numeric values (green positive, red negative, neutral zero or unavailable), even when every day has the same sign. Total Trades counts only fully closed Trades. Avg R stays unavailable because initial risk is not recorded.

Week (Monday–Sunday), Month and Year initially select the current New York calendar period; Previous/Next move calendar periods without allowing a future period. The compact bordered **Date Range** box always remains visible, showing All history or the applied dates. Activate it to open two themed month calendars. Every day in the inclusive range is shaded across both months, with stronger endpoints and softer interior fill. Draft shading, partial endpoint outlines and a separate dashed keyboard-focus outline distinguish unfinished edits from the applied range. **Apply range** applies a complete valid draft; **Cancel**, Escape or closing via the box discards it without changing statistics. The calendars wrap vertically on narrower screens and retain native keyboard navigation. **Today**, **Last Week** (the preceding complete Monday–Sunday week), and **Last Month** (the preceding complete calendar month) use New York dates. Applied ranges/shortcuts select **Custom**, with Previous/Next disabled; selecting Custom directly restores the last custom range (initially today). **All history** clears date bounds without changing the account. Currency separation, DST-aware closure dates, and estimate rules are unchanged.

Dashboard cards use concise labels and prominent values. Net P&L uses EffectiveNet with estimate provenance retained in tooltips/accessibility rather than visible statistic-card labels; Gross and strict verified-Net cards are not duplicated on Dashboard (their authoritative contracts remain unchanged). Best/Worst Day show the daily total and closure date. Statistic cards have no visible Estimated labels. Detailed coverage, unknown-cost estimation and unavailable explanations remain available through accessible tooltips/descriptions. Win Rate keeps its percentage inside the ring, with green winning and red losing Trade counts alongside matching indicators; a neutral break-even count appears when nonzero and remains in the denominator. Profit Factor adds a monetary donut using existing winning EffectiveNet and absolute losing EffectiveNet totals, with currency-labeled amounts. Its ratio remains N/A without a defined denominator; missing economics never become a known-subset donut. The visible period label omits “New York”, but all calendar calculations still use it.

A combined **Avg Win / Avg Loss** card places its heading in the same top position as neighboring cards. Its horizontal comparison bar and the two currency-labeled averages beneath it form a vertically centered group, with the ratio at the bottom center. The bar is solid green when Average Win exceeds the positive Average Loss magnitude, solid red when the loss exceeds the win, and neutral for equality or unavailable values. Average Loss has a display-only minus sign. The ratio remains Average Win divided by positive Average Loss magnitude, not Profit Factor. Missing averages, empty or one-sided scopes, zero denominators and decimal overflow show N/A for the ratio and a neutral bar. Currency and estimate provenance stay in the existing presentation contracts and accessible explanations; stored economics are unchanged.

An empty successful account/date selection keeps all statistic cards in place: Net P&L and Total Trades show zero, and Win Rate shows **0%**, zero counts and a neutral ring. These are display-only empty values, not new calculated economics or a fabricated currency bucket. Profit Factor and Average Win/Loss show **N/A**; Best/Worst Day show N/A with no dates. Charts and Setups state that there are no closed Trades, while Recent Trades continues to show that account's latest rows independently of dates. A nonempty population with unavailable economics still shows unavailable values, not empty zeroes.

**Recent Trades is deliberately independent of Dashboard date/period and currency selection:** All accounts queries the newest ten persisted Trades across accounts; a specific account queries its newest ten, including open Trades. Account filtering precedes SQL count/paging; ordering is OpenedAtUtc descending, then Trade ID ascending. An empty analytics date range does not hide these rows. Shared outcome colors, two-decimal average prices, wrapped Account names, compact Size and a View action mirror the Trades page. View opens that exact Trade through the existing detail flow, including when it is outside the current Trades browse page. Narrow tables scroll horizontally; an ordinary mouse wheel over the table scrolls the Dashboard vertically.

Shared Light/Dark resources now use near-white/white with a charcoal sidebar and mint selection in Light, and navy/plum with a deep navy sidebar in Dark. Dedicated sidebar and on-accent foreground resources retain contrast without changing navigation.

Daily P&L has a New York closure-date axis, a currency-labeled value axis and a clear zero baseline. Occupied dates receive individually labeled green/red/neutral bars; many dates scroll horizontally instead of overlapping labels. An ordinary mouse wheel over the chart scrolls the Dashboard vertically; use the chart's horizontal scrollbar for its date axis. The compact question-mark beside its heading explains the chart without visible coverage prose. Hover or tab to a day for a prompt tooltip with its date, closed-Trades count and signed, full-precision amount; estimate/coverage provenance remains available to assistive technology. The former daily-values expander is removed. Cumulative line segments still split at the interpolated zero crossing, coloring only the portions above/below zero green/red; flat zero segments are neutral. Unavailable values remain gaps, not zero.

Cumulative Realized P&L now matches Daily P&L with a compact question-mark help icon, New York date labels, currency-labeled amount ticks and a zero baseline. Hover within a plotted point's full-height date region, or tab to the point, for its exact signed cumulative amount and date; a single vertical guide and subtle point highlight track the active point. Estimate/coverage details remain in accessible descriptions. The visible explanation, coverage lines and cumulative-values expander are removed. A labeled Start point represents zero before the selected period's closed Trades (All history uses the first occupied date), never an account balance. Occupied dates and the Start point have readable horizontal slots; many dates scroll horizontally, while an ordinary mouse wheel scrolls the Dashboard vertically. Unavailable values remain gaps. Refresh reloads the complete selected population, independent of the ten Recent Trades. Navigation back to Dashboard rereads committed data; active Dashboard loads refresh after manual Trade writes and both import providers. Cancel loading and safe retry messages are available, and superseded reads cannot replace newer results. See [Dashboard Analytics](docs/dashboard-analytics.md) for exact scope, metric and interaction rules.

On both P&L charts, hover tooltips follow the pointer within the active day or point region and flip away from window edges; keyboard-focus tooltips stay anchored to their focused target. The user has manually confirmed the pointer-follow placement. Other M12 interactive checks remain open; see the [M12 acceptance checklist](docs/m12-acceptance.md).

The user has visually verified the estimated-Net presentation in Trades and Details. This is separate from automated analytics coverage and is **not Dashboard visual verification**.

## Trading Calendar

`ITradingCalendarReader.GetAsync(TradingCalendarQuery)` provides a read-only monthly Calendar data path. A request specifies a year, month, and optional Account ID; omitting the Account means all accounts. The result includes every date of the complete Monday–Sunday grid, including adjacent-month dates, and separate historical-currency buckets. It reuses the Dashboard's DST-safe New York closure-date query and authoritative daily/weekly Effective Net metrics; it does not reconstruct P&L from displayed prices. A day or week with no closed Trades has no metrics, distinct from a real zero result. Estimated Net retains its provenance and unavailable economics remain unavailable. Weekly summaries include all seven days, including Sunday.

The Calendar destination opens a themed, keyboard-accessible month grid across all accounts. It starts at the current New York month; Previous, Next, and Today navigate months. Centered **Monthly P/L** above navigation sums only that month's daily Effective Net under the current Account/currency filters, excluding adjacent dates and never counting weekly totals again. Currencies remain separate; incomplete economics show a dash, a genuine zero stays numeric, and an empty month says No closed Trades. Monthly summary tooltips/accessibility retain estimate and coverage information. Day numbers are centered at the top: today's New York date has a small blue circle, selection outlines both the cell and number marker, and keyboard focus has a distinct high-contrast outer marker ring. Hover only the number marker for a concise date, daily P/L and Trade-count tooltip, including Saturday's own daily result. Amounts, counts, weekly summaries and empty cell areas do not open a date tooltip. Accessible descriptions retain estimated-Net and incomplete-coverage explanations without showing cost prose on date hover. Non-Saturday dates with closed Trades show currency-labelled daily Effective Net and Trade counts, with green positive, red negative, and neutral zero/unavailable amounts. Active-month daily results have subtle outcome backgrounds; adjacent dates are subdued. Each Saturday shows only its row's Monday–Sunday summary as Week 1, Week 2, etc., including Saturday and Sunday activity. Multiple currencies occupy separate lines rather than a combined total. Empty dates stay quiet; unavailable P&L uses a dash, while actual zero remains numeric. The date grid remains visible during cancellable loading and after an empty or failed read, with horizontal scrolling at narrow widths.

Hover anywhere in a Calendar day cell to lighten its outcome background; zero, empty, unavailable, Saturday and adjacent-month cells use a neutral hover surface. Leaving restores the original background. Selection, today's blue marker and keyboard focus remain visible, and only the day-number marker opens a tooltip.

Click any date, or focus it with Tab and press Enter/Space, to open a themed **Day Performance** modal, including empty days, Saturdays and adjacent-month dates. The Calendar is dimmed and blocked while it is open. Close or Escape retains the month, filters and selected date and restores date-cell focus. Separate-currency cumulative realized Effective Net charts step only at actual UTC closure instants, displayed in New York time; they do not interpolate intraday profit or represent account balance. Simultaneous closures share a point, and unavailable economics break the complete cumulative sequence. Exact values and estimated-cost provenance are accessible by hover and keyboard focus. The scrollable Trades table shows daily counts, Effective Net summaries, Time (New York), Instrument, Account, direction, Size, assigned Setup and Trading Mistakes. View expands that row inside the modal; Edit reuses the same form, supported execution shapes, validation and save service as the Trades page. Successful saves refresh the row, day chart/summaries and Calendar totals without leaving the modal. Save or Cancel a draft before closing or switching rows; read/commit refreshes wait while editing. Inactive and unavailable classification references are identified, and unassigned values say None assigned. Clock time is shown separately from its explicitly labeled UTC offset in accessible help. The chart axes are labeled Profit and Time. **Day Journal / Add Journal** is visibly unavailable with Coming later guidance; no journal editor or storage is implemented. Loading, Cancel, Retry and stale-response guards reuse the read-only day query, and committed refresh updates an open modal without resetting selection. The former below-grid details panel is removed.

Day Performance initially uses a panel up to 1,280 DIPs wide within the application's available area, so its shared responsive table fits the normal 1,280-DIP desktop window, including Action/View. Setup and Trading Mistakes wrap in their flexible columns; genuinely narrow windows retain horizontal scrolling. **Maximize/Restore** expands the panel within the owner area and returns to its prior size. **Minimize** minimizes the application while retaining the open modal, selected date and any inline edit draft; returning restores Day Performance with the Calendar still blocked.

Dismiss Day Performance with **×**, **Close**, **Escape**, or a click on the dimmed application backdrop outside its panel. The backdrop consumes the complete click, so it cannot also select another Calendar date. Content, scrollbars, chart tooltips and inline editing do not dismiss the modal; switching applications does not close it. All dismissal paths require finishing loading or saving/cancelling inline edits, retain the Calendar selections, and restore date-cell focus.

The modal table sizes Account to its header and the names in the displayed day, with a comfortable 100-DIP minimum and a 160-DIP cap including padding. Longer names use an ellipsis with the full name in a tooltip. After Account, columns are Net P&L, Size, Direction, Setup, Trading Mistakes, and Action. Net P&L uses a compact fixed 150-DIP column with centered headings and values. Setup and Trading Mistakes share the remaining width, with minimum widths of 150 and 170 DIPs, keeping Action near the right edge. Long text wraps and remains available in full through tooltips. Header and row widths stay aligned while scrolling or expanding View, and narrow windows retain horizontal scrolling.

Day Performance charts use thin green/red steps and faint fills. Full-height hover regions meet at closure-point midpoints, activating one subtle vertical guide and point highlight per currency chart. Keyboard focus supplies the same guide and exact time/value tooltip; pointer tooltips follow the mouse with the Dashboard's 100 ms behavior. Visible time-axis ticks show New York clock time with seconds (`HH:mm:ss`), without an offset suffix. Exact subsecond closure times and explicitly labeled New York UTC offsets remain available in tooltips and accessible descriptions. Unavailable values do not become numeric points or filled gaps, and modal wheel/horizontal scrolling is preserved.

Compact Account and Currency selectors above the Calendar default to All accounts and All currencies. Inactive Accounts remain selectable and are labelled inactive. Both selectors apply consistently to daily/weekly amounts and counts and selected-day rows/summaries; currencies are never converted or combined. Selections persist across month/date navigation and empty results. Changing filters reloads the current month and any selected date, cancelling obsolete reads. An unavailable Account remains selected with an explicit recovery message instead of silently switching to All accounts. Currency options reflect the Account's visible-grid currencies plus the retained selection, so an empty month never resets it. See [Trading Calendar](docs/trading-calendar.md).

Refresh reloads the displayed Calendar month and selected-day details without resetting month, date, Account or currency. Committed Trade creation, edits, deletion and Tradovate/TopstepX imports also refresh an active Calendar through the shared Desktop commit notification; NoChanges, blocked, cancelled or rolled-back imports do not. Obsolete reads cannot overwrite newer results. Empty-month and empty-day messages differ from genuine zero or unavailable P&L; recoverable read errors retain the grid and selectors. Day cells support Tab, Enter/Space and assistive-technology invocation, with selected/loading descriptions and distinct daily versus Saturday weekly summaries. Refresh retains same-month date containers for keyboard focus, and vertical wheel routing over the grid/details preserves horizontal navigation at narrow widths. Automated component renders and interaction tests are not live desktop acceptance; see the remaining isolated Windows checks in the Calendar documentation.

## Architectural Principles

The repository follows a domain-first design with dependencies directed toward the Domain. Infrastructure implements meaningful Application abstractions, while Desktop remains the composition and presentation layer. The system is local-first today, but the core should remain independent of WPF so a future web or SaaS presentation can evolve without replacing domain and application logic.

Abstractions and infrastructure should be introduced only when they protect a real boundary or solve a current need. See [Architecture](docs/architecture.md) for the system-level rules, [Desktop UI](docs/desktop-ui.md) for extending the WPF presentation layer, [Domain Model](docs/domain-model.md) for the trading model, and [Persistence](docs/persistence.md) for database semantics and migration workflow.

## Documentation Policy

`README.md` and milestone-level documentation are updated when a milestone is completed rather than after every individual task. Documentation must be updated immediately when a task changes developer setup, build, run, configuration, or another workflow developers need to use the repository correctly.

The README is not a task-by-task changelog.
