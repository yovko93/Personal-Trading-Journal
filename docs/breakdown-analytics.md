# Breakdown Analytics — M20

## M20.2 — filters, grouping and exact selection contracts

Implemented **16 October 2026**, starting clean on `develop`, HEAD `d9320697c3ac669a25890f4699a431913d0d9d6e`. The committed M20.1 audit is retained below. This step adds pure Application contracts and helpers only; aggregation/results, SQL, Desktop controls, charts and executed drill-down belong to later M20 milestones.

### Scope decisions

- **Supported grouping:** Instrument (default), Trading Setup, Direction, Entry weekday, Entry hour, Account and Holding duration, one dimension at a time.
- **Strategy is deferred for M20.** There is no authoritative source after its removal from the schema. No Strategy entity/field is recreated, and Setup names or Playbook text cannot substitute for it. Unsupported enum values fail validation.
- **Basis:** Gross or **strict Net** (`PnlBasis.Gross` / `.Net`), default strict Net. `EffectiveNet` and unknown enum values are rejected. Dashboard/Calendar retain their existing, explicitly estimated Net presentation. Reconciliation must match population, filters, historical currency, closure-date basis and Gross/strict-Net semantics; an estimated Dashboard headline is not a strict-Net comparison.
- **Currency:** exactly one required historical pricing currency, trimmed and uppercased invariantly, nonempty and at most eight characters, matching `TradePricingSnapshot`. This is not a new ISO currency registry and performs no conversion. Empty/missing currency is not an implicit combined-currency selection.

### Contracts and population

Namespace: `PersonalTradingJournal.Application.Analytics.Breakdown`.

| Contract | Purpose |
| --- | --- |
| [`BreakdownQuery`](../src/PersonalTradingJournal.Application/Analytics/Breakdown/BreakdownQuery.cs) | Immutable filter context: one currency, basis, grouping and existing `DashboardAnalyticsQuery` as `TradeFilter` |
| [`BreakdownGrouping`, `BreakdownGroupKey`](../src/PersonalTradingJournal.Application/Analytics/Breakdown/BreakdownGroupKey.cs) | Supported dimensions and validated typed keys; display labels/activity never participate in identity |
| [`HoldingDurationBucket`, `HoldingDurationRange`](../src/PersonalTradingJournal.Application/Analytics/Breakdown/HoldingDurationRange.cs) | Six explicit elapsed-time bucket identities, half-open bounds and pure boundary classification helpers |
| [`BreakdownGroupSelection`](../src/PersonalTradingJournal.Application/Analytics/Breakdown/BreakdownGroupSelection.cs) | Original immutable query AND an exact typed group key for future drill-down; rejects a mismatched dimension or a group contradicting a specific Account/Instrument filter |

`TradeFilter` reuses Dashboard's identifier validation and New York boundary conversion. Optional Account/Instrument IDs combine with AND; null means All accounts / All instruments, while `Guid.Empty` is invalid. A syntactically valid ID is not an existence/activity check: unavailable and inactive reference identity must remain representable. Dates use `DateOnly?`, not WPF/desktop period enums. Both absent means all history; a single absent bound leaves that side unrestricted. Future week/month/preset UI resolves its explicit dates using existing helpers, rather than adding another calendar-period implementation here.

`IncludedStatus` is always Closed, with a required non-null closure instant. Open and partially exited Trades do not contribute realized monetary/outcome analytics. Missing Gross/Net/costs **do not remove an eligible closed Trade** or shrink the coverage denominator. Dashboard's persisted reader uses the same closed-only population; its excluded-open count of zero is not a census of open activity. The pure Dashboard calculator accepts open context separately, whereas Breakdown's query population is explicitly closed-only.

### Time and stable identity

Period membership uses **final `ClosedAtUtc`**, with inclusive New York dates converted to `[local midnight(first), local midnight(after last))` UTC bounds through `DashboardAnalyticsQuery`. Spring/fall days retain 23/25-hour lengths. Entry timestamp, broker TradeDay and futures-session rollover do not replace closure membership.

| Dimension | Exact key / predicate and ordering |
| --- | --- |
| Instrument | Stable `InstrumentId`, never symbol/name; retained missing metadata remains the same ID |
| Trading Setup | Current `TradingSetupId`; null key alone means **Unclassified**. A retained ID with unavailable name is a specific Setup, and inactive historical classifications stay eligible |
| Account | Exact `TradingAccountId`, not name or an implicit null-scoped Journal identity |
| Direction | Existing Domain `TradeDirection.Long` / `.Short` |
| Entry weekday | Existing `DayOfWeek` from `OpenedAtUtc` converted by `TradingTimePolicy` to New York; natural key order is Monday–Sunday |
| Entry hour | Integer 00–23 from that same New York opening instant; both autumn occurrences of 01 belong to group 01, each Trade once; natural order 00–23 |
| Holding duration | Explicit bucket from `ClosedAtUtc − OpenedAtUtc` in UTC, ordered shortest to longest |

A Trade opened Monday 23:30 New York and closed Tuesday 00:15 belongs to Tuesday's selected closure period, **Monday** entry weekday and hour **23**. Entry grouping cannot retarget its period. Date/time helpers require canonical zero-offset UTC instants, as existing Application time contracts do.

Factories prevent malformed key payloads (empty IDs, invalid directions/weekdays/hours/buckets). `CompareTo` uses dimension then stable reference ID or chronological value, not display text. `GetLabel` takes current display metadata separately: a known name is shown with ` · inactive` when applicable; missing metadata falls back deterministically to `Missing Instrument (ID)`, `Missing Account (ID)` or `Missing Setup (ID)`, matching the existing missing-Setup convention. Duplicate names never merge groups, and a rename/archive changes no key, hash or selection. Weekday/duration labels are English like the current UI; hour labels explicitly identify New York.

### Holding duration

These six boundaries supersede the seven-bucket **proposal** recorded in M20.1:

| Bucket ID | Label | Inclusive lower / exclusive upper |
| --- | --- | --- |
| `UnderOneMinute` | Under 1 minute | `[0, 1 minute)` |
| `OneToFiveMinutes` | 1–5 minutes | `[1, 5 minutes)` |
| `FiveToFifteenMinutes` | 5–15 minutes | `[5, 15 minutes)` |
| `FifteenToSixtyMinutes` | 15–60 minutes | `[15, 60 minutes)` |
| `OneToFourHours` | 1–4 hours | `[60, 240 minutes)` |
| `FourHoursOrMore` | 4 hours or more | `[240 minutes, infinity)` |

Zero duration belongs to Under 1 minute; every exact upper boundary belongs to the next bucket. UTC subtraction avoids distorted elapsed time across DST. Missing closure returns null/unavailable, never a valid bucket. Negative elapsed time, non-UTC instants and unsupported buckets throw parameter-specific argument exceptions. A future result must report unavailable/invalid source duration explicitly rather than assign it to a valid group or silently omit it; this step performs no database repair or result calculation.

### Exact drill-down and coverage obligations

`BreakdownGroupSelection` carries the original Account, Instrument, closure range, currency, basis and grouping unchanged, together with the exact typed key. It contains no mutable label, displayed count, rounded value or browse page. Future Trades paging must apply the **entire query AND the key** before counting/sorting/paging, including closed eligibility, historical currency, null Setup versus exact retained ID, New York **entry** weekday/hour, or the bucket's UTC elapsed-time bounds. It must preserve guarded navigation and Breakdown return state, and revalidate after source changes. The existing Account-only `TradeListQuery` cannot yet execute this descriptor; no approximate navigation or SQL predicate has been added.

M20.3 results must reuse existing `MetricCoverage`/`PnlMetrics`/unavailable statuses to expose:

- the full eligible closed count and independent Gross/strict-Net known counts;
- missing-cost Trade coverage (the union, not commission count plus fee count), with component counts only when backed by actual execution evidence;
- null-Setup/Unclassified count versus classified retained-ID count, with missing display metadata distinguished;
- Empty, Partial, Unavailable and Complete metrics: a real zero stays zero, missing stays null, and a known subset is never the complete group total, rate or average.

Unknown commission or fees never become zero or estimated Net. No R-multiple can be inferred without recorded risk. No aggregate result contract or calculation engine is introduced in M20.2.

### M20.2 verification and file inventory

**M20.2 complete.** Verification on the unchanged develop HEAD plus these uncommitted changes:

| Check | Result / evidence |
| --- | --- |
| New focused Breakdown contracts | **41/41 passed**, zero failures/skips; filter validation/normalization, identity/labels, chronological ordering, entry DST/midnight, exact duration boundaries, unavailable/invalid durations and selection conflicts |
| Complete Application Release project | **921/921 passed**, zero failures/skips; includes the 41 new cases and existing Dashboard/Calendar/Daily/Period contracts/calculations |
| Complete Domain Release project | **490/490 passed**, zero failures/skips |
| Release solution build | **0 warnings / 0 errors**, `dotnet build PersonalTradingJournal.sln -c Release --no-restore` |
| EF model consistency | No model changes since the last migration; in-memory design-time `JournalDbContext` check, no production database |
| Whitespace / documentation links | Passed; `git diff --check`, new C# file whitespace and 143 local Markdown target checks |
| Full WPF/Infrastructure Release gate, visual/live acceptance | Not run for this pure Application-contract change; no SQL, Desktop or existing production behavior modified |
| Matching CI | Unverified for these uncommitted changes; no resulting commit or CI acceptance claimed |

Test commands use `dotnet test <project> -c Release --no-build --no-restore --logger trx --results-directory <isolated directory>`. The focused Application filter was `FullyQualifiedName~.Analytics.Breakdown.`; the broader Application and Domain runs had no filter. Raw TRX: ignored `artifacts/m202/focused/breakdown.trx`, `artifacts/m202/application/application.trx`, `artifacts/m202/domain/domain.trx`. The 41 focused cases are included in 921, not additional unique tests. No database/provider access is used by these new tests.

Exact changed files (11):

- `src/PersonalTradingJournal.Application/Analytics/Breakdown/BreakdownQuery.cs` (new)
- `src/PersonalTradingJournal.Application/Analytics/Breakdown/BreakdownGroupKey.cs` (new)
- `src/PersonalTradingJournal.Application/Analytics/Breakdown/HoldingDurationRange.cs` (new)
- `src/PersonalTradingJournal.Application/Analytics/Breakdown/BreakdownGroupSelection.cs` (new)
- `tests/PersonalTradingJournal.Application.Tests/Analytics/Breakdown/BreakdownQueryTests.cs` (new)
- `tests/PersonalTradingJournal.Application.Tests/Analytics/Breakdown/BreakdownGroupKeyTests.cs` (new)
- `tests/PersonalTradingJournal.Application.Tests/Analytics/Breakdown/HoldingDurationRangeTests.cs` (new)
- `tests/PersonalTradingJournal.Application.Tests/Analytics/Breakdown/BreakdownGroupSelectionTests.cs` (new)
- `README.md`
- `docs/breakdown-analytics.md`
- `docs/architecture.md`

M20.3 remains next: implement group metrics and average Trade on the established shared formulas, with eligible/known/missing-cost/Unclassified coverage and unavailable states. SQL, UI and executed drill-down remain in their later milestones. No migration, production-journal access, provider request, branch switch, reset, commit, push or merge.

## M20.1 — existing analytics audit and shared calculation contract

Audited on **16 October 2026**, `develop`, HEAD `4864305e51be4d519cd20b90047daa5ceab51540`, with a clean starting worktree. This section retains the M20.1 checkpoint and proposals; the implemented M20.2 scope decisions above supersede its Strategy and duration proposals. Trading Plan, Notebook and new AI analysis remain separate work.

The supplied Breakdown screenshot guides layout, not calculation inputs. Its corrected Unclassified average is **−360 USD / 12 Trades = −30 USD**. Its four group P&Ls sum to 2,460 USD and counts to 120; these illustrative figures must never become application data. The written requirement for a compact summary takes precedence over the screenshot's large KPI cards. Retain the shared shell heading rather than duplicating it inside the page.

### Existing implementation and reuse boundaries

| Concern | Existing implementation | Reuse / necessary addition |
| --- | --- | --- |
| Authoritative economics | [`Trade`](../src/PersonalTradingJournal.Domain/Trades/Trade.cs), [`TradeExecution`](../src/PersonalTradingJournal.Domain/Trades/TradeExecution.cs), captured pricing snapshot | Reuse final Gross, costs and Net; never reconstruct from rounded prices or current Instrument specifications |
| Persisted analytics facts | [`TradeBrowsePersistenceMapper`](../src/PersonalTradingJournal.Infrastructure/Persistence/Mapping/TradeBrowsePersistenceMapper.cs), one browse row per Trade | Reuse the transactionally maintained projection and historical `PricingCurrency`; no analytics table or migration needed for these facts |
| Filter validation / date bounds | [`DashboardAnalyticsQuery`](../src/PersonalTradingJournal.Application/Analytics/DashboardAnalyticsQuery.cs), [`TradingTimePolicy`](../src/PersonalTradingJournal.Application/Common/Time/TradingTimePolicy.cs) | Reuse inclusive New York closure dates and UTC half-open bounds; add explicit currency and grouping/metric selection in Breakdown contracts |
| Shared metrics / coverage | [`DashboardMetricCalculator`](../src/PersonalTradingJournal.Application/Analytics/DashboardMetricCalculator.cs), [`PnlAccumulator`](../src/PersonalTradingJournal.Application/Analytics/PnlAccumulator.cs), [`PnlMetrics`](../src/PersonalTradingJournal.Application/Analytics/PnlMetrics.cs) | Reuse formulas, decimal precision, coverage and unavailable reasons. Expose a narrow reusable closed-population summary rather than repeatedly calculating unused day/week series for every group |
| Complete persisted population | [`DashboardAnalyticsReader`](../src/PersonalTradingJournal.Infrastructure/Analytics/DashboardAnalyticsReader.cs), `IDashboardAnalyticsReader` | Reuse scalar, no-tracking SQL projection patterns. Current returned snapshot has aggregated metrics and only ten recent facts per currency; it cannot provide complete group drill-down membership |
| Current grouping | `DashboardMetricCalculator`: currency, Setup ID, New York closure day, Monday-start week | Extend with stable Instrument/Account IDs, direction, entry weekday/hour and UTC holding duration. Keep missing classifications visible |
| Period breakdowns | [`PeriodReviewComparisonCalculator`](../src/PersonalTradingJournal.Application/PeriodReviews/PeriodReviewComparisonCalculator.cs), `PeriodBreakdownKey` and bounded memberships | Reuse stable-key, contributing-ID, cancellation and size-limit patterns. Existing groups include Instrument, frozen-or-legacy Setup, unknown Session and multi-valued recorded Mistake. The whole calculator requires two adjacent week/month evidence sets, Rule patterns and full coaching evidence; it is not a lightweight arbitrary-range Breakdown query |
| Current reference metadata | Reader left-joins Setup and Instrument; no activity filter | Batch metadata in the same read snapshot, preserving inactive or unavailable references and same-name distinct IDs |
| Filters / lifecycle | [`DashboardViewModel`](../src/PersonalTradingJournal.Desktop/ViewModels/Dashboard/DashboardViewModel.cs) | Reuse period controls, inactive-Account options, cancellation, background reading and request-generation stale-result checks; retain Breakdown's independent filters, sort and selection |
| Navigation / invalidation | [`MainWindowViewModel`](../src/PersonalTradingJournal.Desktop/ViewModels/MainWindowViewModel.cs): activation, destination guards and committed Trade/import events | Add Breakdown activation/deactivation and invalidation hooks; reuse guarded exact-Trade opening. Preserve Breakdown state on return |
| Trade browsing | [`TradeListQuery`](../src/PersonalTradingJournal.Application/Trades/TradeListQuery.cs), [`TradeListReader`](../src/PersonalTradingJournal.Infrastructure/Trades/TradeListReader.cs) | Current paging supports Account and sorting only, includes open Trades and has no date/Instrument/currency/group predicate. Extend with a precise drill-down context before reusing it; never sum its visible page |
| Charts / themes | Dashboard `PnlChart`, `DailyPnlChart`, `ChartValueScale`, theme brushes and shared card/text styles | Reuse scale/theme/accessibility patterns; existing date-series charts are not a categorical horizontal group chart. Add a purpose-specific view with a zero baseline and unavailable states |
| Performance / Breakdown pages | Navigation destinations exist, both fall through to `PlaceholderViewModel` | No implemented Performance calculator/page to reconcile. Replace only Breakdown in M20.5 onward |
| Strategy | [`RemoveStrategies` migration](../src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20260914212911_RemoveStrategies.cs) dropped the catalog and `Trades.StrategyId`; current Domain/EF model has no Strategy | **Requirement gap:** Strategy grouping cannot be derived from existing records. Do not relabel Setup or infer Strategy from names/Playbook prose. Resolve an explicit data-contract decision before implementing that grouping |

### Current population, date and currency rules

Dashboard's persisted reader selects **fully Closed Trades with non-null `ClosedAtUtc`**, once per Trade ID, before any grouping. Open Trades, including partial exits, contribute no final realized results. The pure calculator can report excluded open inputs; the persisted reader already filters them out, so its `ExcludedOpenTradeCount = 0` is not a database-wide count of excluded activity.

Account and Instrument predicates combine with AND. Null query IDs mean unrestricted; empty GUIDs are invalid and valid absent IDs match nothing. Inactive references do not exclude historical Trades. Account names, Instrument symbols and Setup names are display metadata, never group identity or pricing inputs.

Period membership is the **New York civil date of final closure**, not entry date, broker TradeDay, import time or a futures-session rollover. Bounds are `[midnight(first date), midnight(day after last date))`, with each New York midnight converted separately. Spring/fall DST days can span 23/25 hours. A Trade opened before midnight and closed after midnight belongs once to its closure date; entry-based grouping does not change this membership. Dashboard supports all history, calendar week/month/year and custom inclusive date ranges; weeks start Monday.

Every monetary result belongs to the Trade's original **historical pricing currency**, not its Account's or current Instrument's currency. The reader obtains all currency buckets; Dashboard's currency selector chooses one bucket in memory without a new analytics query. There is no cross-currency money sum or FX conversion. A successful empty read returns no invented currency bucket.

### Shared metric definitions

For a selected nonempty Account/currency population, let `N` be all eligible closed Trades, and `K` the number with a value on the selected basis. Gross and strict Net use independent coverage and sign classifications. Complete-population metrics require `K = N`; known-subset facts remain explicitly labeled as such.

| Metric | Existing formula / availability contract |
| --- | --- |
| Gross per Trade | `(sell execution notional − buy execution notional) × captured point value`, for a fully closed flat-to-flat Trade only |
| Costs / strict Net | Total costs require known commission **and** fees on every execution. Strict Net = Gross − total costs only when both exist. One missing component makes strict Net unavailable; individually recorded costs are not assumed zero |
| Wins / losses / break-even | Selected-basis value `> 0`, `< 0`, `= 0`. Unknown values have no inferred outcome. Known counts describe the known subset when coverage is incomplete |
| Win Rate | `100 × wins / N`, including break-even Trades in the denominator; unavailable for empty or incomplete coverage |
| Total | Sum selected-basis values only when nonempty and complete. `KnownSubtotal` may describe K/N, never a complete Net result |
| Average Trade | Complete total / N. Not yet a property of `PnlMetrics`; Period Review already derives it with coverage checks. Extract/reuse that derivation in the shared metric boundary |
| Average Win | Positive result sum / winning Trade count; excludes break-even. Undefined with no wins; unavailable if the entire population's basis coverage is incomplete |
| Average Loss | Absolute negative result sum / losing Trade count, a **positive magnitude** in the existing contract. A future negative display must label its sign convention explicitly |
| Profit Factor | Positive result sum / absolute negative result sum; computed from Trades, never averaged from group ratios. All losers gives defined 0; positive profits without losses gives `NoLosses` with null numeric value; all break-even gives `AllBreakEven`; empty and incomplete states are distinct |
| R multiples | Unavailable: authoritative initial risk is not recorded; do not derive it from prices, Setup association or P&L |

`MetricCoverage` distinguishes **Empty, Complete, Partial and Unavailable** and carries eligible/known/unknown counts. A real zero remains zero; missing values remain null. Empty/all-unknown known subtotals are null. Calculations use `decimal`, checked sums and no monetary rounding until presentation; overflow/cancellation propagates rather than publishing a partial result. Duplicate Trade facts and inconsistent lifecycle/currency inputs are rejected.

### Existing differences that must remain explicit

1. **Displayed Net:** Dashboard/Calendar currently use `EffectiveNet`, which prefers authoritative Net, otherwise uses known Gross as a clearly **Estimated** no-cost-deduction value. It does not subtract even individually known costs. Its outcome counts, ratios and averages inherit the estimated label. Daily/Period Review use strict Net and suppress complete Net metrics when required costs are unknown. These are different labeled products over the same facts, not different Gross economics. Breakdown's requested **Gross / Net** contract should use Gross / **strict Net**, with coverage warnings. Reconcile against Dashboard's underlying `.Gross` / `.Net`, not an estimated headline. Introducing an estimated option would require an explicit product decision and label; do not silently change Dashboard or substitute EffectiveNet for strict Net.
2. **Empty display:** Dashboard deliberately presents display-only 0 and 0% in empty cards with an explanation; shared calculations return unavailable values/no currency buckets. Breakdown should distinguish no matching Trades from incomplete metrics and retain the underlying null contract.
3. **Recent rows:** Dashboard's visible recent list is an independently paged Account-scoped list, not filtered by selected analytics period/currency. Its analytics snapshot also carries a separate ten-closed-Trade recent slice per currency. Neither is a full Breakdown drill-down population.
4. **Coverage depth:** Dashboard sees projected `TotalCosts` and can count Trades with unknown total costs; it cannot distinguish missing commission versus missing fees from that fact alone. Daily evidence includes execution quality and distinguishes components, unsupported/missing projections and open activity. Breakdown needs a bounded batch/read projection extension if it promises those more specific explanations; avoid loading full coaching evidence or executions per group.
5. **Classification time:** Dashboard groups by the Trade's **current** `TradingSetupId`; frozen Playbook captures are separate historical evidence. Breakdown should label current classification and not substitute an old capture or infer plan adherence. Strategy is absent, as documented above.
6. **Period groups:** M19.4 deliberately groups a captured Setup by exact frozen revision, falling back to legacy classification. That historical grouping can split a Setup across revisions, unlike Dashboard's current Setup grouping. Period Session is explicitly unknown because the app has no authoritative session definition. These semantics should stay in Period Review; Breakdown's entry hour/weekday are defined clock buckets, not inferred sessions. Recorded Mistake groups can overlap and must not be summed into disjoint population totals.
7. **Account Current Balance:** `AccountCurrentBalanceCalculator` adds closed-Trade Gross to Starting Balance and deducts every individually recorded cost, marking missing-cost results Estimated. It excludes other pricing currencies and broker adjustments. This is a separate Trade-based balance policy, not strict Net or Dashboard's no-cost-deduction EffectiveNet. Do not reuse its estimate as Breakdown P&L.

These are audited differences; M20.1 changes no page semantics or calculation code.

### Proposed grouping and query extension map (not yet implemented)

Use one complete filtered dataset for summary, all groups, coverage and contributing IDs. A Breakdown fact can compose `TradeAnalyticsFact` with Account/Instrument identity and labels plus projected direction, avoiding breaking existing Daily/Period coaching serialization. Do not repurpose or expand a saved AI contract merely to supply UI grouping fields. Project missing reference state in the same snapshot. If multiple SQL reads become necessary, hold one read transaction; do not mix current sources across incompatible snapshots. Apply SQL filters before aggregation, with explicit cancellation and a documented size policy rather than silent truncation. Do not issue one query per group or load unrelated images/text.

| Group | Stable key / exact membership |
| --- | --- |
| Instrument (default) | `InstrumentId`; current symbol as label, historical currency retained independently |
| Trading Setup | Current `TradingSetupId`; null = Unclassified; absent catalog record retains ID and an unavailable label; inactive stays visible |
| Strategy | Blocked on a real source contract; not an alias for Setup and not fabricated Unclassified data |
| Direction | Authoritative projected `Long` / `Short` |
| Entry weekday | `OpenedAtUtc` converted to New York; Monday–Sunday order |
| Entry hour | New York hour 00–23; both occurrences of an autumn repeated hour share the hour group, each Trade counted once |
| Account | Exact `TradingAccountId`, including inactive references; same-name Accounts stay separate |
| Holding duration | `ClosedAtUtc − OpenedAtUtc` in UTC; proposed non-overlapping buckets: `[0,1m)`, `[1m,5m)`, `[5m,15m)`, `[15m,1h)`, `[1h,4h)`, `[4h,24h)`, `[24h,∞)`. Exactly one minute belongs to the second bucket. Final labels/boundaries are M20.2 work |

For each selected dimension, additive counts/known amounts must reconcile to the filtered population; rates and Profit Factor must be recalculated from that population. Groups retain contributing IDs for exact drill-down. Stable IDs resolve ties after the selected numeric sort. No currency aggregate crosses currency boundaries. A future visible small-sample label should use a documented fixed UI threshold (proposed fewer than 20 eligible closed Trades) and make no certainty/significance claim; this threshold is not currently implemented.

### Incremental delivery and verification map

| Milestone | Concrete next boundary |
| --- | --- |
| M20.2 | Validated filter/group definitions, duration buckets, basis labels and explicit resolution of the absent Strategy source |
| M20.3 | Shared complete-population summaries/average Trade, coverage, classification counts and stable contributing IDs; no WPF formulas |
| M20.4 | One consistent read-only Breakdown reader/query; batch reference metadata, SQL filtering and cancellation |
| M20.5 | Independent persistent page state, latest-request publication and committed-data invalidation |
| M20.6–M20.7 | Compact summary, sortable full table, selectable categorical horizontal bars and synchronized group detail; reuse theme primitives |
| M20.8 | Guarded exact membership query/navigation in Trades, return-state preservation and revalidation after edits/deletion |
| M20.9 | Loading/error/empty/coverage states, focus/accessibility, Light/Dark, existing 1000×650 DIP minimum plus 1280 DIP and 240 DPI/250% layouts |
| M20.10 | Reconcile the same population/basis against shared calculators, verify end-to-end filters/drill-down and record automated/live/CI acceptance separately |

Existing regression anchors: Application `Analytics/*` cover strict/estimated differences, outcomes, averages, empty/one-sided data, decimals, overflow, date/DST and cumulative/group metrics; SQLite `DashboardAnalyticsReaderTests` cover combined filters, inactive/missing Setup, historical currencies, all matching rows, no writes/extra loads, refresh and cancellation. `DailyReviewStatisticsCalculatorTests` and Period statistics tests cover strict Net/account coverage. Desktop `DashboardFilterTests` and `DashboardViewModelTests` cover Account lifecycle, stale loads, cancellation and estimated/empty presentation. Reuse these rather than adding tests that merely repeat unchanged code for this audit.

### M20.1 verification

**Passed: 213 existing focused Release tests**, zero failures/skips: Application analytics and Daily statistics **115**, Period statistics/comparisons **40**, SQLite Dashboard/Calendar readers **27**, Desktop Dashboard filters/presentation **31**. Synthetic SQLite fixtures and fake readers only; no production journal or provider was accessed. TRX evidence is under ignored `artifacts/m201/{application,period,infrastructure,desktop}/`.

Commands (each project uses `-c Release --no-build --no-restore`, TRX logger and its isolated results directory):

- Application filter: `FullyQualifiedName~.Analytics.|FullyQualifiedName~DailyReviewStatisticsCalculatorTests|FullyQualifiedName~PeriodReviewStatisticsTests` — 115 cases. The last name matches no class; actual Period coverage was run separately below.
- Application Period filter: `FullyQualifiedName~PeriodReviewStatisticsCalculatorTests|FullyQualifiedName~PeriodReviewComparisonTests` — 40 cases.
- Infrastructure filter: `FullyQualifiedName~DashboardAnalyticsReaderTests|FullyQualifiedName~TradingCalendarReaderTests|FullyQualifiedName~TradingCalendarDayReaderTests` — 27 cases.
- Desktop filter: `FullyQualifiedName~DashboardFilterTests|FullyQualifiedName~DashboardViewModelTests` — 31 cases.

`dotnet build PersonalTradingJournal.sln -c Release --no-restore`: **0 warnings / 0 errors**. Whitespace and local Markdown source links checked. No calculation code, new test, migration, Desktop view or theme changed; the full Release gate and visual/live checks were not repeated for this documentation-only audit. These focused results establish the audited existing behavior, not acceptance of an implemented Breakdown page. Matching CI for these uncommitted documents is unverified.

Exact changed files: **`README.md`**, **`docs/architecture.md`**, **`docs/breakdown-analytics.md`** (new). Branch/HEAD unchanged. No commit, push or merge. M20.1 is complete; M20.2 is next, with the Strategy source gap explicitly unresolved.
