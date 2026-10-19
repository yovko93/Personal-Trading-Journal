# Architecture

## Trading Plan tombstones and explicit Create prefill

`TradingPlans.DeletedAtUtc` is a nullable UTC concurrency field. Active-only partial unique indexes retain the global/exact-Account New York-date key while permitting recreation with a new Plan ID. Delete reserves the SQLite writer, checks the loaded ID/version, tombstones and verifies within the transaction. It does not rewrite owner audit/version or immutable Ready JSON. Exact historical Ready reads and the conservative Plan asset ledger retain deleted owners; normal workspace/history, new Review evidence and prefill queries exclude them. Old saved Reviews retain their original IDs and JSON, never resolve by a replacement date/name. Current-schema backup/preflight/restore preserve the complete database and referenced images; logical export v13 includes the tombstone, metadata only for images. Older-schema archive restore remains deferred; occupied-schema downgrade is explicitly blocked.

Only CreateDraft invokes `PrefillPreviousDay`: one bounded latest-earlier query in the same writer transaction and exact nullable Account scope selects saved Draft, otherwise latest Ready. All preparation values are copied with new scenario/Plan identities and current audit times; screenshots and legacy confirmations are excluded. Source reference fingerprints are rechecked without name substitution. Correctable unavailable/changed sources retain the copied Draft and an unresolved reference baseline; Ready still requires verified sources. Frozen currency is carried without FX conversion; a legacy Draft without a recoverable currency cannot silently acquire a changed Account currency. Manual Copy Forward remains template-only, and loading empty scopes remains read-only.

Active confirmation commands are rejected and Desktop checklist adapters are inert. Historical fields/packets remain compatible but cannot gate new readiness. Real content/numeric/reference/currency checks remain, with explicit reference refresh and user correction rather than a mandatory acknowledgement popup. [Detailed evidence and limits](trading-plan.md#delete-plan-ready-simplification-and-create-prefill-before-m2110).

## Current Playbook definitions and historical evidence

Playbook authoring now updates one `CurrentPlaybooks` row per existing TradingSetup, with an optimistic Version and atomic normalized example-retention index. The existing v1 value carrier retains its `Revision` field for capture/packet compatibility; current storage treats it as a concurrency token. `SaveTradingSetupPlaybookUseCase` uses `TrySaveCurrentAsync`, not the legacy append writer. SQLite reserves the writer before rechecking Setup state/token, validating image ownership, replacing metadata and verifying readback. Stale/failed/cancelled writes roll back; cancellation after commit does not undo a reported committed result.

Migration preserves the previous maximum-retained-revision current policy as an explicit bridge. Historical revision/example rows stay immutable and readable; new Trade captures materialize their exact current definition in that ledger only when needed, in the capture transaction. Trading Plan references resolve current guidance in bounded batches and detect token/content changes before Ready. Ready and Review JSON remain unchanged. Current-only image retention participates in Trade deletion, backup inventory, preflight and restore; export v12 includes the two current tables. No second Setup catalog, automatic provider call or historical rewrite is introduced. [Detailed compatibility and verification](trading-plan.md#direct-playbook-editing-and-themed-feedback-before-m2110).

Trading Plan uses the existing semantic feedback palette/LiveAnnouncement behavior through a page-local reusable `PlanInfoBox` template; explicit operation outcomes choose severity. Inline validation remains independent. Page-local button styles retain existing command enablement, focus templates and opaque state colors.

## Shared preparation and retained Plan images

Trading Plan identity now explicitly derives `AllAccounts` versus `Account` from a nullable exact Account ID plus New York date. Partial unique SQLite indexes enforce each identity; null never stands for a fabricated Account or a frozen membership list. New global Drafts require a canonical explicit currency. Ready references freeze it and trader-wide limits without multiplying limits per Account or converting FX. Specific-Account data remains specific through migration/history/copy. Storage payload v2 reads legacy v1 without filling old evidence; downgrade with stored Plans/assets is refused (empty-schema rollback remains supported).

`TradingPlanAssets` is an immutable retention ledger owned by a logical Plan (restricted FK), separate from Trade screenshots. Draft/Ready JSON freezes asset ID, Instrument ID, timeframe and validated image metadata. Existing immutable screenshot storage handles original bytes; Desktop decodes bounded PNG/JPEG and OnLoad thumbnails. Pending uploads stay in memory until explicit Save. File publication precedes the transactional metadata write; failed/cancelled saves compensate via reference-aware cleanup, and cleanup failures remain visible. This is not filesystem/database atomicity. Persisted assets are conservatively retained even after Draft removal; no garbage collector is introduced. Backup inventories include the ledger and validate each Draft/Ready attachment against it; portable JSON v11 carries metadata only.

Review's snapshot reader includes global Plans once. Exact eligible Ready preparation takes precedence over eligible global preparation at each Trade's UTC entry; Draft-only/retrospective exact Plans do not suppress global fallback. New Daily/period packet v4 is distinct from export v11; prior packets remain immutable/readable. Images, storage keys and filenames are not projected to AI. [Detailed limits and verification](trading-plan.md#shared-preparation-and-timeframe-screenshots-before-m2110).

## M21.9 Review preparation snapshot boundary

`ReviewPlanEvidenceReader` runs inside the existing Daily/period read transaction. It adds three bounded SELECTs: relevant Plan identities/state, selected Ready payload size/count, and the payloads. Selection fetches only the latest Ready per relevant Plan and the exact entry-eligible revisions in one set-based query; no per-Trade lookup, Draft prose or all-revision scan/materialization. The existing current/previous period transaction remains the consistency boundary. `ReviewPlanEvidence` rebuilds and validates chronology/identity comparisons independently of serialization; packet builders freeze its complete local context. It is a read model, not a new association, authoring aggregate or evaluator. Closure-date economics, open/partial activity and Rule assessments are unchanged.

Existing Daily/period coaching contracts advance to v3 for preparation source kinds and Daily's `UserWrittenPreparation` observation basis. The period projection keeps complete calculated coverage but at most two Ready examples per side; citations can target only supplied sources. Existing snapshot JSON, atomic review save and export mappings carry the payload without a migration or export version change. v1/v2 saved reports remain supported; old evidence has absent preparation, never backfilled current Plans. Navigation reuses the Plan historical viewer and shell guards while retaining the originating Review and working editor. [Chronology, limits, compatibility and verification](trading-plan.md#m219--trades-review-and-ai-evidence-integration).

M21.8 extends the existing Trading Plan coordinator with an independent History read context, not another editable aggregate. It retains the same Session Brief editor/token/raw fields across tab switches and related navigation. Exact historical Ready views instantiate the existing section session in read-only historical mode, using saved reference text. All mutations still use `ITradingPlanStore.WriteAsync` and its existing SQLite writer reservation, reference checks, uniqueness, optimistic version and verified readback. History transfers stage existing leave consent and replace the editor only after a matching committed aggregate returns; failed transfer retains input even after a Discard choice. A subsequent catalog refresh cannot overwrite the visible transfer success message.

History summary reads use an exact-Account/date/status query limited to 1–100 Plans and four batched SELECTs in one deferred SQLite read transaction (no per-Plan queries). The Desktop requests 21, displays 20 and derives the next cursor from the last displayed Plan; revision paging uses the same lookahead convention. Independent bounded historical-Account discovery pages distinct identities within the typed date range, resolving current labels or the latest frozen Ready Account label in that range. Unavailable Draft-only Accounts have no fabricated historical name. Request epochs prevent late results from replacing later contexts; all read owners drain through the existing cancellation/close mechanism. No transaction survives navigation, no global navigation preferences are persisted and no schema/export/backup contract changes are necessary. [M21.8 workflow and verification](trading-plan.md#m218--history-copy-forward-and-navigation-recovery).

M21.7 adds the retained `TradingPlanWorkspaceViewModel` as a **coordinator**, not another authoring model. Its `Editor` is the sole `SessionPreparationViewModel` shared by all three compiled sections. The coordinator owns date/Account selection, catalogs, request epochs, explicit creation/lifecycle actions and staged leave consent; the session owns all raw fields, the saved baseline, validation, confirmations and optimistic token. Components hide their repeated session/status controls only when hosted. The existing Application/Infrastructure operations remain the only write path; lifecycle results are checked against the captured identity, version and content before presentation changes. No schema, financial, Review or provider behavior changes.

Scope changes obtain Save/Discard/Cancel before publishing a new selection. Obsolete reads are cancelled and cannot publish; outstanding read owners are drained before accepted closing. Busy writes veto navigation/close until they finish or are cancelled. Related Playbook/Rule navigation retains the original session and uses existing destination guards. Discard consent does not destroy fields until all shell guards accept. Async Save on close first cancels the native attempt, then performs a fresh `WindowCloseAttempt` after Save; a deferred callback is not treated as acceptance, and any final veto clears consent while preserving input. [Detailed lifecycle, full-page verification and limitations](trading-plan.md#m217--session-brief-desktop-workspace).

M21.6 completes the reusable risk/readiness component on **the same `SessionPreparationViewModel` and optimistic token**. The risk and Rules partials own only transient raw fields, exact link selections and explicitly staged checkmarks, not another aggregate or readiness engine. `TradingPlanReadiness` and `TradingPlanChecklistPolicy` remain authoritative; formatting-only numeric changes do not invalidate meaningful confirmations, but malformed raw text blocks the entire save. Optional explicit confirmations in `TradingPlanWriteCommand` apply after content edits in the **same** writer transaction; legacy callers without that argument retain the previous behavior. Success requires matching persisted content/checkmarks and returns reference context captured under that transaction. `ITradingPlanReferenceReader` supplies an explicit read-only SQLite snapshot check of the loaded token, baseline and proposed sources; it cannot acknowledge reference changes. Currency review is explicit, never FX conversion. Ready displays use frozen values. Existing paged Rule readers supply bounded choices and exact selected revisions; navigation is a host callback, not a new destination/guard. No table, migration, backup/export or provider change is introduced. M21.7 still owns full composition, toolbar, source/scope loading, guarded navigation and cancellation draining. [Ownership, reference semantics, verification and exact files](trading-plan.md#m216--risk-limits-trading-rules-and-readiness).

M21.5 extends **the same** `SessionPreparationViewModel` through a scenarios partial and transient `ScenarioEditor` fields. Both reusable sections bind the identical instance; neither creates an independent Draft/token or saves per field. The combined content builder preserves unowned risk/Rule fields and submits preparation, scenarios, links, intent and Stand Aside through the existing transactional store. Domain remains authoritative for readiness, meaningful no-ops and checklist invalidation. A single first-invalid-field identity coordinates the existing guarded `PageFocus` helpers. Existing Setup reader, Playbook current/history readers and exact selection tokens supply optional links; catalog refresh never updates a selected token or confirmed baseline. Ready reads use the existing frozen Setup snapshot without consulting live guidance. A host-supplied exact-ID navigation callback must use the existing shell/Playbook guards; no new global guard, destination or automatic save is installed. M21.7 owns callback wiring, lifecycle/cancellation draining and full Session Brief composition. [Contract, bounds, integration and evidence](trading-plan.md#m215--conditional-scenarios-and-playbook-integration).

M21.4 adds a reusable Desktop `SessionPreparationSection` and host-owned `SessionPreparationViewModel`. `PreparationField` and `InstrumentPreparationEditor` are transient raw-input adapters, not another Draft model. Valid content preserves unowned scenario/risk/rule fields and flows through existing `ITradingPlanStore` commands; checklist preview executes the existing Domain mutation on a reconstructed copy. The host supplies one exact loaded Plan, owns guarded scope switching, explicitly loads the catalog/saves/refreshes, and cancels/drains work before releasing its services. Ready views use frozen reference labels. The section owns responsive layout, inline errors and first-invalid-field focus; it has no outer page scroll, global navigation or full toolbar. M21.7 composes it into Session Brief. [Behavior, limits and verification](trading-plan.md#m214--session-preparation-and-instrument-context).

M21.3 implements `ITradingPlanStore`/`WriteTradingPlanUseCase` through `TradingPlanStore` in Infrastructure. Existing Domain transitions remain the only authoring path; explicit validated `Rehydrate` factories restore state without replaying transitions. `TradingPlans` owns the current Draft/version and hashed reference baseline; `TradingPlanReadyRevisions` owns separately versioned frozen payloads. Each write holds a SQLite immediate writer transaction through source resolution, token checking, mutation, persistence and database readback, then commits without a cancellation token after the final cancellation boundary. Ready update/delete/sequence guards are database triggers. No foreign key to mutable catalogs can erase preparation; the Ready-to-Plan foreign key restricts owner deletion. Reads use bounded keyset pages and one snapshot transaction for a logical aggregate. Logical export v10 includes both tables; complete current-schema backup/restore carries them without a custom file format. [Detailed behavior and verification](trading-plan.md#m213--persistence-migrations-and-application-use-cases). Earlier milestone notes below describe their original boundaries.

M21.2 adds the `TradingPlan` aggregate on the existing `AuditableEntity`/M21.1 content foundation. The single-owner aggregate retains one optional immutable working Draft and its latest immutable Ready revision; historical Ready values are independently retainable, so writes will not require loading the entire history. Mutations use the existing long optimistic version, validate before publishing state, and keep Ready numbering separate. Preparation edits compare structured values, reset the affected existing confirmations and ignore incoming checkmarks; explicit confirmation is a separate operation. `TradingPlanReferenceSnapshot.Capture` accepts supplied canonical catalog objects, validates exact membership/selected versions and copies only relevant immutable context. Domain performs no lookups. Application `TradingPlanLoadedState.FromPlan` exposes the aggregate through the existing command token. M21.3 still must supply fresh sources, validated rehydration, writer-transaction conflicts/uniqueness, append-only persistence and verified commit/readback. [Snapshot fields and implementation boundary](trading-plan.md#m212--preserved-ready-revisions-and-working-drafts).

M21.1 introduces pure `Domain/TradingPlans` identity, immutable bounded authoring values and content-only readiness/checklist policies, plus `Application/TradingPlans` submission/concurrency/history contracts and New York default-date adaptation through the existing `TradingTimePolicy`. These types contain no WPF, EF, filesystem, market-data or provider dependencies. Account/date is the logical uniqueness boundary; session and Instruments are content. Save versions are distinct from immutable Ready revision IDs. No second catalog, database table, Desktop behavior or review contract is added in this first slice. [Required fields, reference retention, copy resets, exact files and remaining implementation](trading-plan.md).

M20.10 audited the complete M20.1–M20.9 implementation at `ebea74a860b85dd7b0d52e65c2a51d2b287151bf`, including shared calculator reuse, single-statement financial/reference consistency, request/context lifetimes, formatting-only presentation and exact-ID current-record drill-down. No product/schema change was warranted. The opt-in `scripts/BreakdownMeasurements` console creates migrated synthetic databases and measures real reader/calculator, Desktop formatting/chart preparation and scoped first/last pages; it accepts no installed database path and is not part of test discovery. Measured populations through 50k stored Trades confirm linear materialization/ID-encoding costs, not a universal performance SLA or a render-time guarantee. [Current policies, acceptance evidence and limitations](breakdown-analytics.md#m2010--reconciliation-and-acceptance-review) supersede historical milestone proposals; in particular Breakdown uses Net preferred with explicit Gross fallback, not the initial strict-Net-only proposal.

M20.9 keeps accessibility/focus policy in the Desktop views. `PageFocus` owns one-shot dispatcher requests with owner lifetime, DataContext, input-generation and current-focus checks; it never changes query ownership or submits reads. Breakdown emits only explicit invalid-input/cancel focus intents. Scoped Trades resolves live row controls by exact identity and waits for committed deletion reloads, without overriding newer input or altering ordinary browse commands. `LiveAnnouncement` opt-in emits visible status/error automation events, not sorting/selection announcements. Native ComboBox/ListBox/DataGrid peers, label relationships and sort ItemStatus provide accessible semantics; no global keyboard interception or financial/persistence change is introduced. [Findings, verification and remaining live checks](breakdown-analytics.md#m209--desktop-ux-and-accessibility).

M20.8 adds Application `TradeIdCohort` (defensively copied exact membership) as an optional `TradeListQuery` restriction. Null means ordinary browsing; an empty cohort means no rows. Infrastructure applies one parameterized JSON ID array with SQLite `json_each` before SQL count/order/page. A per-read deferred SQLite transaction covers count, current projection/catalog values and batched position-size metadata, and is disposed before returning. No transaction spans navigation. Desktop `BreakdownTradeContext` captures the original successful query, typed group identity, source labels, original IDs/count and navigation identity. The existing retained Trades ViewModel keeps cohort and ordinary page/sort state separate; request epochs/cancellation prevent obsolete scoped publication. MainWindow coordinates guarded entry/exit and applied-query refresh on return. No financial calculations, write contracts, schema or alternative Trades application are introduced. [Policy, tests and limits](breakdown-analytics.md#m208--trade-drill-down-and-navigation).

The [Breakdown implementation map](breakdown-analytics.md) records M20.1–M20.4 reuse, contracts, calculations and reads. Application `Analytics/Breakdown` owns immutable `BreakdownQuery`, typed `BreakdownGroupKey`, fixed `HoldingDurationRange`, `BreakdownGroupSelection`, `BreakdownTradeFact`, numeric/coverage results, pure `BreakdownMetricCalculator` and `IBreakdownAnalyticsReader`. It composes Dashboard identifier/New York closure-bound validation and authoritative `TradeAnalyticsFact`, and reuses unchanged shared numeric/coverage rules. M20.3 explicitly replaces Breakdown's former strict-Net selection with its own **Gross / Net preferred** mode: actual Net when known, otherwise supplied Gross, with separate source counts rather than an estimated-Net label. Existing shared strict-Net and Dashboard/Calendar behavior are preserved. Identity excludes display metadata. Application remains independent of WPF/EF; Strategy is deferred.

Infrastructure `BreakdownAnalyticsReader`, transiently registered in `AddPersistence`, filters the full closed-Trade population in one no-tracking SELECT over `Trades`, the transactionally maintained `TradeBrowse` projection and left-joined Account/Instrument/Setup metadata. Historical pricing currency and New York final-closure bounds determine membership. One SQLite statement snapshot covers facts and labels; there are no separate metadata reads, collection joins, pages or result caches. Scalar inputs materialize and the fresh context is disposed before the Application calculator runs. Cancellation/errors propagate without partial success. Memory scales with the complete selected population; no silent row limit or speculative index/migration is introduced. Future page state and exact Trades navigation must retain the original query and typed selection; no Desktop behavior is added in M20.4.

M20.5 adds a retained Desktop `BreakdownViewModel` and an implicit `BreakdownView` template in the existing shell. DI injects the Application reader plus Account/Instrument catalog readers and `TimeProvider`; no calculation, EF dependency or backend change is added to presentation. The entire lookup/read/calculation work executes in worker tasks with independently owned backend contexts; only the owning request publishes on the captured WPF context. Lookup catalogs populate selector choices, not financial results, and are not claimed to share the analytics statement snapshot. Each successful immutable result retains its own query; editable filters remain independent. Refresh rereads the last submitted query, Apply captures a valid draft, and Reset issues one default request. Request generations supplement cancellation so older success/error/finally paths cannot publish. Activation loads once per retained session; accepted navigation/close or disposal invalidates pending work. Existing committed-Trade/import notifications mark data stale for explicit Refresh; catalog edits also use explicit Refresh. No new global invalidation platform, persisted preferences, chart/table/drill-down or financial policy is introduced. [Defaults, verification, files and limitations](breakdown-analytics.md#m205--desktop-filters-and-page-state).

M20.6 adds Desktop-only `BreakdownPresentation` wrappers over the successful immutable snapshot, referencing original metrics and keys without enumerating contributing IDs. A native WPF DataGrid provides a read-only virtualized comparison table using existing theme tokens. Sort/selection state belongs to the retained Breakdown ViewModel; raw numeric/chronological comparers and stable-key selection issue no database requests. Summary and rows are prepared before current-request publication; the M20.5 reader/cancellation/filter boundaries are unchanged. Money formatting reuses Dashboard's formatter, with explicit Breakdown source/coverage labels and ratio-state help. [Presentation rules and verification](breakdown-analytics.md#m206--compact-summary-and-group-comparison-table).

## Architectural Goals

M20.7 extends the same Desktop snapshot presentation with `BreakdownChartPresentation`, geometry-only `BreakdownBar`, and selected-group details. A virtualized native ListBox supplies category selection/accessibility rather than a new chart dependency; existing time-series controls are unchanged. The chart shares table order and authoritative key selection, validates row-instance ownership against replaced results, and consumes raw metric/coverage values without contributing-ID enumeration. Metric state is local; readers, financial calculations and query ownership are unchanged. [Chart contracts and verification](breakdown-analytics.md#m207--comparison-chart-and-selected-group-details).

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

- **Accounts** — purpose-specific list/detail reads and explicit create, update, activate/deactivate, and safe-delete workflows;
- **Instruments** — purpose-specific list/detail reads and explicit create, update, activate/deactivate, and safe-delete workflows, including referenced-AssetClass protection;
- **Setups** — Trading Setup list/detail reads, create/update/lifecycle/safe-delete use cases, duplicate-name checks, and aggregate persistence;
- **Mistakes** — Trading Mistake list/detail and catalog lifecycle workflows plus separate Trade Mistake assignment, removal, and Trade-scoped projections;
- **Trades** — manual create/correction/close/hard-delete workflows, optional Trading Setup validation and mutation, narrow aggregate-write and deletion boundaries, selector projections, pageable list reads, and complete one-Trade detail reads;
- **Imports/Tradovate** — parser/reconstruction contracts, Instrument-resolution planning, explicit Account/time preparation, deterministic preview, and confirmation through `ImportTradovateTradesUseCase` and `ITradovateImportStore`;
- **Screenshots** — purpose-specific boundaries and use cases coordinate Trade existence checks, binary storage, metadata persistence, ordered metadata reads, content retrieval, and deletion without exposing provider details to presentation; and
- **Storage** — `IApplicationPaths`, which exposes required storage locations without knowing how Windows resolves them.

Readers return presentation-oriented query projections and are optimized for their specific read use. Stores expose only the aggregate persistence operations required by write use cases. Create and lifecycle use cases construct or mutate Domain aggregates and coordinate persistence. These are explicit feature boundaries, not a generic repository pattern.

### PersonalTradingJournal.Infrastructure

The Infrastructure project implements Application abstractions and owns external technical concerns. Its current responsibilities include:

- `LocalApplicationPaths` and local directory creation;
- `JournalDbContext` and EF Core SQLite registration;
- Infrastructure-owned persistence records and `IEntityTypeConfiguration` mappings, including the derived one-to-one Trade browse projection;
- explicit persistence-record/Domain mappers;
- Application reader and store implementations for Accounts and Instruments;
- reader/store implementations for Trading Setup and Trading Mistake catalogs and Trade Mistake associations;
- `TradeStore`, `TradeMutationStore`, `TradeDeletionStore`, and `ManualTradeReferenceDataReader` for manual Trade creation, correction, closure, hard deletion, and Setup classification;
- `TradeListReader` and `TradeDetailReader` for authoritative Trade browsing;
- local screenshot file storage and SQLite screenshot metadata/read/delete implementations;
- Tradovate CSV parsing, source-constrained execution reconstruction, and atomic SQLite import with durable fill allocations and reference revalidation;
- the SQLite UTC timestamp converter;
- EF Core migrations and the design-time context factory; and
- `JournalDatabaseInitializer` for runtime migration application and version-aware Trade browse projection reconciliation.

Additional import providers and alternative persistence providers belong at this boundary when concrete use cases require them.

### PersonalTradingJournal.Contracts

The Contracts project is reserved for stable DTOs or contracts that may later be shared across presentation or API boundaries. It does not currently define application contracts.

### PersonalTradingJournal.Desktop

The Desktop project contains the WPF presentation layer and the application composition root. It owns:

- WPF Views and presentation ViewModels;
- data-backed Accounts, Instruments, and Trades feature workflows;
- data-backed Trading Setup and Trading Mistake catalog workflows;
- manual Trade create/edit state, deterministic raw-input parsing and validation, and submission through the Application use cases;
- authoritative paged Trades, Trade Detail, and ordered execution-lifecycle presentation;
- explicit Trade edit and destructive delete actions with authoritative post-write reloads;
- optional Setup selection during entry, Setup assignment/change/clear, and Trading Mistake assignment/removal in Trade Detail;
- screenshot file selection, metadata presentation, preview decoding, delete confirmation, and operation state;
- Tradovate file selection, read-only review, explicit import confirmation, and result/recovery presentation;
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
ImportViewModel      -> ImportView
TradingSetupsViewModel -> TradingSetupsView
TradingMistakesViewModel -> TradingMistakesView
SettingsViewModel        -> SettingsView
PlaceholderViewModel -> PlaceholderView
```

There are 19 destinations: eight concrete destinations—Dashboard, Trades, Import, Accounts, Instruments, Setups, Mistakes, and Settings—and 11 placeholders that share the placeholder mapping instead of carrying empty View/ViewModel pairs. A placeholder should be replaced only when its feature gains real presentation state and Application workflows.

`MainWindowViewModel` retains its injected Dashboard, Trades, Import, Accounts, Instruments, Trading Setups, Trading Mistakes, and Settings ViewModels for the lifetime of the main window. Returning to a record-based feature reuses its successfully loaded collection/reference cache but calls that feature's explicit transient-state reset, so prior create/edit/detail surfaces, drafts, validation, and preview state do not reopen. Import retains the Account options but clears its selected file and in-memory analysis/preview. Trades additionally retain the current page rows, page number, total count, sort column, and sort direction. Repeated navigation to the already-active destination is ignored. This remains direct typed shell state; no `NavigationService` exists.

### Desktop Theme System

`AppTheme`, `IThemeService`, Windows theme detection, theme selection, and JSON preference persistence are Desktop concerns; they do not enter Domain or trading Application workflows. `PreferredTheme` records the user's System/Dark/Light choice, while `EffectiveTheme` is always the concrete Dark or Light appearance. System resolves `AppsUseLightTheme` from the current-user Windows Personalize registry key and safely falls back to Dark when detection fails. Supported Windows preference changes are observed while System is preferred; explicit Dark or Light ignores them.

Shared typography, layout, and control styles remain single-source resources. `DarkTheme.xaml` and `LightTheme.xaml` contain the same semantic color and brush keys, and `ThemeService` replaces exactly one active theme dictionary while preserving merged shared dictionaries and their ordering. System is a preference and therefore has no resource dictionary of its own.

Theme-sensitive brush consumers use `DynamicResource`, allowing materialized controls and pages to update without rebuilding ViewModels or restarting. Immutable style, typography, spacing, and converter references remain `StaticResource`. Resource tests validate Dark/Light key parity and both static and dynamic project-owned references.

`JsonDesktopSettingsStore` persists only the preferred theme to the configured `IApplicationPaths.SettingsPath`; a System preference remains System even when its effective appearance is Dark or Light. Missing, malformed, or unknown settings fall back to System and are logged; writes use a temporary file followed by replacement. Settings applies a choice immediately and reports a safe error if persistence fails without reverting the usable visual theme. The header's two-state quick toggle reflects `EffectiveTheme` and creates an explicit opposite Dark/Light preference; Settings remains the route back to System.

### Desktop Dialog Boundary

`IDialogService` is a narrow Desktop-only abstraction for PTJ-styled confirmation and information dialogs. Callers provide small presentation request models containing contextual text and destructive intent; they do not provide arbitrary controls, domain policy, or modal routing metadata. `WpfDialogService` owns window creation and owner selection, and the composition root supplies it through dependency injection. This boundary lets feature ViewModels and adapters request confirmation without calling `MessageBox` or constructing WPF windows, while entity-specific update, delete, reference-checking, and file-lifecycle rules remain in their existing Application and Infrastructure workflows.

The Dashboard is currently a presentation shell. It provides neutral metric and panel surfaces but performs no analytics or database queries. Financial outcome must not be interpreted as process quality: good process can lose, and bad process can profit. Future process-quality analysis must model that distinction explicitly.

M12.1 supplies independent, read-only Application rules in `Analytics`: authoritative Trade facts, currency-isolated Gross/Net metrics with completeness, New York closure-day buckets and current Setup buckets. M12.2 adds `IDashboardAnalyticsReader` and immutable Account/Instrument/New York close-date filters. Its Infrastructure implementation projects all matching closed Trades from `TradeBrowse` joined to `Trades` through a fresh no-tracking context, preserving Domain-derived P&L and historical currency without executions, paging or query-time writes. No Desktop wiring or migration is introduced. See [Dashboard Analytics](dashboard-analytics.md) for the calculation/read contracts and deferred chart/UI work.

### Desktop Data-Access Boundary

Desktop may depend on Application abstractions and use cases. Its reference to Infrastructure exists because Desktop is the composition root that wires concrete implementations; it does not authorize feature ViewModels to query `JournalDbContext` directly. Accounts, Instruments, Trading Setups, Trading Mistakes, and Trades follow meaningful Application boundaries rather than a `ViewModel -> JournalDbContext` dependency.

Desktop objects use constructor injection. ViewModels must not locate dependencies through `IServiceProvider` or another service-locator pattern.

The Tradovate Import ViewModel is an orchestration boundary over Application contracts. Its read-only flow is `ITradovateCsvParser -> ITradovateExecutionReconstructor -> TradovateInstrumentResolver -> TradovateImportPreparationService -> TradovateImportPreviewBuilder`. Infrastructure implements parsing and source-constrained reconstruction; Application owns Instrument resolution, Account/time preparation, and deterministic preview projection. The Desktop picker owns the WPF dialog and returns a base filename plus a caller-owned stream. Analysis/preview make no import writes.

After explicit dialog approval, Desktop calls `ImportTradovateTradesUseCase`. Preparation rechecks the selected Account, then `ITradovateImportStore` owns one SQLite transaction: validate conserved source-fill allocations, classify duplicates, revalidate reference identity/uniqueness/economics for new candidates, build Domain Trades, and persist the complete graph. Reversals allocate one real fill across two directional Trades without changing Domain invariants. Desktop does not reproduce reconstruction, deduplication, or persistence policy. `Imported`, `NoChanges`, and `Blocked` have explicit presentation/recovery behavior; unknown commission/fees never become zero. See [Tradovate CSV Import](tradovate-csv-import.md).

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

Write failures produce operation-specific create, update, lifecycle, or delete errors. If persistence succeeds but the projection reload fails, the mutation remains successful, the visible list may temporarily be stale, and Desktop presents a list-level refresh warning so the user can retry Refresh. This distinction avoids falsely reporting that a successful mutation failed.

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
  -> Trade + executions + Domain-derived browse projection
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
  -> transactional execution replacement + regenerated browse projection
  -> authoritative Trade Detail and first-page Trade reload
```

The close command deliberately contains no quantity. The use case closes the aggregate's exact remaining `OpenQuantity`, and Domain execution rules remain authoritative for sequence, chronology, status, closure time, and P&L. Desktop does not fabricate the closed projection; post-commit detail and list reloads use non-cancellable tokens so a committed close is not reclassified as cancellation. The screenshot list is unaffected by the close.

An existing manual Trade can be corrected through the same presentation form without exposing mutable execution setters:

```text
TradesViewModel
  -> UpdateTradeUseCase
  -> authoritative Trade, Account, Instrument, and optional Setup lookup
  -> Trade.CorrectDetails
  -> ITradeMutationStore.SaveAsync
  -> transactional root update, execution-row replacement, and regenerated browse projection
  -> authoritative Trade Detail and current-page Trade reload
```

The use case preserves the historical pricing snapshot when Instrument is unchanged and rebuilds it from the authoritative Instrument when the Instrument changes. It permits current inactive references to remain selected, rejects different inactive references, preserves retained execution identities and broker/import provenance, and applies target-Instrument quantity policy. Domain validates the complete replacement before mutation, recalculates all derived lifecycle/economic values, and treats canonically identical input as a no-write result. Trade Mistake associations and screenshot metadata are not part of this correction.

Trade deletion is a separate explicit workflow:

```text
TradesViewModel
  -> shared destructive confirmation
  -> DeleteTradeUseCase
  -> ITradeDeletionStore
  -> transactional dependent-row, browse-projection, and Trade deletion
  -> post-commit best-effort screenshot file cleanup
  -> authoritative current-page Trade reload with invalid-page correction
```

Database deletion commits before physical screenshot cleanup. A cleanup exception therefore returns a deterministic warning instead of misreporting the committed database deletion as a failure. The store removes execution rows, Trade Mistake associations, and screenshot metadata for only the target Trade while preserving reference catalogs and unrelated Trade graphs.

The pageable list path is:

```text
TradesViewModel
  -> ITradeListReader
  -> TradeListReader
  -> SQLite
  -> COUNT + selected ORDER BY + Skip/Take
  -> TradeBrowse + current Account/Instrument labels
  -> TradeListPage
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

Both read paths use current Account and Instrument values only as display labels. Trade Detail reconstructs the canonical aggregate. Trade list economics come from the persisted browse projection, which is regenerated exclusively from those same Domain properties during writes and startup reconciliation.

`TradeListReader` and `TradeDetailReader` each create a fresh `JournalDbContext` and use no-tracking EF queries. `TradeDetailReader` reconstructs Domain Trades through `TradePersistenceMapper`; `TradeListReader` counts, sorts, skips, takes, and projects `TradeBrowse` plus live catalog labels in SQLite without aggregate hydration. It then fetches the current page's execution sides/quantities in one batch to calculate peak Size. Exact decimal ordering uses the persisted sort keys, Net P&L explicitly keeps null values last in both directions, and Trade ID ascending is the final tie-breaker. `TradeBrowse` is an Infrastructure cache rather than Domain state: canonical Trade and execution records always win, and missing/outdated rows are rebuilt to projection version 2.

After a successful Trade commit, Desktop resets and closes the draft, reports success, and performs a best-effort authoritative list reload. That post-commit reload deliberately does not inherit the completed Save command's cancellation. If it fails, the persisted write remains successful, the existing rows remain visible, and a list-level error allows an explicit Refresh; it is not reclassified as a Save failure.

`TradesViewModel` keeps independent areas of feature state for manual reference data, the current 20-row Trade page and sort, the shared create/edit draft and save operation, Trade Detail, and delete feedback. Browse state survives navigation transient-state reset. Create returns to page 1 / Opened UTC descending; refresh, edit, and close preserve page/sort; delete corrects an invalid last page. Detail state is exposed through `SelectedTradeDetail`, `IsTradeDetailVisible`, `IsTradeDetailLoading`, `IsTradeDetailNotFound`, and `TradeDetailErrorMessage`. Loading detail does not replace the list or mutate the aggregate.

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

The architecture deliberately does not introduce a generic `Repository<T>` or Unit of Work abstraction. Narrow Application-facing readers and stores support the concrete Accounts, Instruments, Trading Setup, Trading Mistake, Trade correction/deletion/classification, and screenshot workflows. Trade and screenshot readers express distinct query shapes; `ITradeDeletionStore` expresses the one hard-delete operation and its screenshot cleanup inventory without exposing persistence records. `IDbContextFactory<JournalDbContext>` remains Infrastructure persistence machinery and is not exposed to UI code.

`AddPersistence(...)` registers `IDbContextFactory<JournalDbContext>`. Contexts are short-lived, created per operation, and disposed after use; the desktop application does not retain a long-lived context. Production-wired integration tests verify that writes made through one context are visible through later fresh contexts.

For Trade creation, `TradeStore.AddAsync(...)` creates a fresh context, maps the authoritative aggregate with the existing Trade and execution persistence mappers, derives `TradeBrowse` from the valid Domain aggregate, and calls `SaveChangesAsync(...)` once. EF Core's transactional SaveChanges behavior commits the root, executions, and browse projection atomically; there is no custom Unit of Work abstraction.

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

The composition root wires current Application workflows, their Infrastructure implementations, feature ViewModels, the Desktop theme/settings services, and the shell. This includes Trade creation/correction/closure/deletion/browsing/classification, Trading Setup and Trading Mistake catalogs, Trade Mistake associations, screenshot add/read/preview/delete dependencies, the Tradovate preview and transactional confirmation pipeline, and all eight retained concrete feature ViewModels. `MainWindowViewModel` and `MainWindow` are also created through dependency injection. Feature ViewModels receive dependencies through their constructors and do not resolve services themselves.

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

Runtime initialization uses `Database.MigrateAsync()`, never `EnsureCreated`, then reconciles missing or old-version Trade browse rows from canonical Trade aggregates. Migration or reconciliation exceptions propagate into the existing fatal startup handler, so the main window is not resolved or shown after a failure. There is no automatic database deletion, recreation, or destructive recovery fallback.

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
- **Application.Tests** covers use-case behavior and authoritative validation, including reference lookup, historical pricing capture, manual Trade creation/correction/closure/deletion, Setup classification, Trade Mistake assignment/removal, and screenshot orchestration.
- **Infrastructure.Tests** covers implementation and integration behavior, including mapping, real SQLite semantics, relational integrity, migrated temporary databases, runtime initialization, Trade persistence/reads/correction/deletion, Setup and Mistake persistence, Trade Mistake relationships, screenshot storage, and cancellation.
- **Desktop.Tests** targets `net10.0-windows` and exercises ViewModel, navigation, and project-owned XAML-resource behavior using real Application use cases with hand-written test readers and stores. It covers catalog lifecycle, manual Trade and screenshot forms, Trade edit/delete state, classification and association state, authoritative reloads, cancellation, operation gating, Trade browsing, and screenshot lifecycle. The suite does not instantiate WPF `Window`, `Application`, or `UserControl` objects or perform UI automation.

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

A `Trade` represents one directional position lifecycle, from the opening execution until the position returns exactly to flat. Scale-in and partial scale-out are supported. An execution that would reverse through zero is rejected by the aggregate. Tradovate reconstruction allocates a supported crossing broker fill between the closing Trade and a new opposite Trade, retaining one source identity with conserved quantities and distinct allocation indexes.

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
