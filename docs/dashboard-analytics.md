# Dashboard Analytics — M12.1 metrics and M12.2 read boundary

## Scope and authoritative inputs

Implemented: pure, read-only Application contracts and `DashboardMetricCalculator`, with per-currency summary, daily and Setup metrics; M12.2 adds the filtered `IDashboardAnalyticsReader` persistence path. No Dashboard wiring, chart or migration is added. The existing Dashboard cards remain placeholders.

`TradeAnalyticsFact` represents **one journal Trade**, not an execution, broker fill, source row count or displayed price. `FromTrade` copies the Domain's status, original UTC lifecycle timestamps, historical pricing currency, current optional Setup ID, Gross P&L, total costs and Net P&L. M12.2 projects the same contract from authoritative persisted facts. `TradeBrowsePersistenceMapper` maintains Domain-derived P&L, but the paged `ITradeListReader` is not a complete analytics population and does not include Setup classification. Do not sum the currently visible browse page.

The calculator receives the complete selected population, once per Trade ID. Duplicate IDs and structurally inconsistent facts are rejected, not silently counted, repaired or deduplicated. M12.2 applies Account, Instrument and closure-date filters before calculating all cards/buckets; coverage always refers to the entire selected closed-Trade population, not just rows with known Net.

## Filtered read boundary (M12.2)

`DashboardAnalyticsQuery` is immutable and explicit on every call. Optional `TradingAccountId` and `InstrumentId` combine with AND; null means all, with no remembered UI selection. `Guid.Empty` is invalid and throws `ArgumentException` naming the parameter. A syntactically valid but missing/deleted reference ID simply matches no Trades. Reference activity is not a filter: historical closed Trades with inactive Accounts, Instruments or Setups remain eligible.

`ClosedFromNewYork` and `ClosedThroughNewYork` are independently optional, inclusive civil dates. An inverted range throws `ArgumentException` naming `closedThroughNewYork`; equal dates are valid. The inclusive end cannot be `9999-12-31` because the next midnight is unrepresentable (`ArgumentOutOfRangeException`). Validation happens in the query constructor, before any database call. Each supplied boundary is resolved separately using the existing New York time policy: start is local midnight inclusive; end is the following local midnight exclusive. SQL compares **ClosedAtUtc >= start AND ClosedAtUtc < end** for whichever bounds exist. Never add 24 hours to a UTC start or subtract a rounded fraction of a second from an end. Spring-forward days span 23 hours; fall-back days span 25 hours. Dates of opening, import/audit activity and broker TradeDay are not used.

Infrastructure `DashboardAnalyticsReader`, registered by `AddPersistence`, creates a fresh `JournalDbContext` for each call and executes one no-tracking scalar projection joining `TradeBrowse` to `Trades`. Status must be Closed and ClosedAtUtc non-null. Account, Instrument and UTC boundary predicates execute in SQLite through the existing UTC timestamp converter; no `Skip`/`Take`, arbitrary cap, browse sort, execution load or screenshot load is used. P&L, costs and lifecycle facts come directly from `TradeBrowse`; historical currency and current Setup ID come from `Trades`. Exact decimals and nullable values pass unchanged to the M12.1 calculator; no SQL floating-point P&L summation or price-based recalculation is introduced.

The query runs against the initialized/migrated database. Existing startup reconciliation repairs old/missing browse projections; production manual add/edit/close and both import transactions maintain the projection atomically, and deletion removes it. The reader performs no repair or writes. No retained snapshot/cache or change-tracker subscription is used: a query started after a committed write sees current data, even through the same reader instance. A write concurrent with an already-running query requires a subsequent query to obtain the later committed snapshot. Automatic active-view refresh is outside this stage.

`GetAsync` returns `DashboardAnalyticsSnapshot`. Since SQL excludes open Trades, its SelectedTradeCount equals matching closed Trades and ExcludedOpenTradeCount is zero, **not** a census of open Trades outside the result. Null Net rows remain in coverage. A no-match selection returns no currency buckets, not a fabricated zero result. Cancellation reaches context creation, EF materialization and the calculator; database failures propagate rather than becoming an empty snapshot. All matching scalar facts are materialized for the reusable calculator: memory scales with the selected closed-Trade count; streaming/database metric aggregation is deferred, with no silent truncation.

Only `TradeStatus.Closed` contributes to outcome metrics. An open Trade, including one with partial exits and an average exit price, contributes **nothing** to final Gross/Net, Win Rate, Profit Factor, daily outcomes or Setup outcomes. Open Trades are counted separately as excluded. Under the existing Domain, their final Gross/Net and ClosedAtUtc are null; M12.1 introduces no partial-realized, unrealized or mark-to-market policy.

The Trade Domain remains authoritative: Gross uses exact execution buy/sell notionals and the captured point value; Net subtracts total costs only when every execution's commission **and** fees are known. Analytics only adds/classifies those values. It never reconstructs economics from rounded average prices, current Instrument specifications, imported source PnL or account balance. TopstepX's supported one-row-per-Trade import and Tradovate reversal allocations retain their established journal Trade boundaries. No imported facts or cost interpretation change.

## Currency, precision and coverage

Each result is scoped to exactly one canonical **historical pricing-snapshot currency**. USD and EUR, for example, produce separate buckets, including separate Win Rate/Profit Factor; there is no combined-currency total or implicit FX conversion. Account currency and current Instrument currency cannot replace the snapshot. Currency buckets are ordered ordinally; an empty selection has no invented default-currency bucket.

Gross and Net have separate `PnlBasis` labels and independent `MetricCoverage`:

- `Empty`: no selected closed Trades; total and known subtotal are null.
- `Complete`: every selected closed Trade has a value on that basis.
- `Partial`: some, but not all, selected closed Trades have a value.
- `Unavailable`: selected closed Trades exist, but none has a value.

`ClosedTradeCount`, `KnownTradeCount`, `UnknownTradeCount` and `UnknownCostTradeCount` let the later UI explain coverage and cause. Missing Gross is also represented as unavailable, never zero; it must not be described as an unknown-cost problem when costs are actually known. Complete `Total` is numeric **only** for a nonempty, complete basis. `KnownSubtotal` adds available values, but is explicitly a subset, not an estimated complete result; it is null if none are available. Known win/loss/break-even counts and profit/loss sums describe that same known subset. Win Rate and Profit Factor are suppressed when coverage is incomplete rather than presenting a potentially biased subset ratio.

For example: a +5 USD Gross Trade with unknown costs plus a +10 USD Gross Trade with 2 USD known costs gives **Gross total 15 USD**, **Net total unavailable**, **known Net subtotal 8 USD**, Net coverage 1/2 and one unknown-cost Trade. Unknown costs never become zero; Gross never fills a missing Net value. Explicit zero costs, zero outcomes and an actual zero sum remain numeric zero, not missing data.

Arithmetic uses `decimal` without monetary/display rounding in this layer. Ratios use normal decimal division precision. Arithmetic overflow propagates rather than wrapping, capping or silently dropping a Trade. Collection results are read-only snapshots and calculations do not mutate input facts. Cancellation propagates.

## Metric definitions

| Metric | Definition and edge cases |
| --- | --- |
| Gross / Net P&L | Sum of the selected closed Trades' authoritative value on that basis, within one currency; complete total and labeled known subtotal follow the coverage rules above. |
| Gross / Net Win Rate | `100 × wins / closed Trades`, only with complete nonempty coverage. Positive = win; negative = loss; exactly zero = break-even. Break-evens **are included** in the denominator, never counted as wins/losses. No Trades or incomplete coverage → null. All break-even → 0%. |
| Gross / Net Profit Factor | Sum of strictly positive outcomes divided by the magnitude of strictly negative outcomes, on the same basis. Zero outcomes affect neither sum. Positive loss denominator → `Defined` numeric ratio (including 0 when there are losses but no wins). Positive profits with zero losses → `NoLosses`, unbounded ratio, null numeric value. All break-even → `AllBreakEven`, undefined 0/0, null value. Empty → `NoTrades`; incomplete → `IncompleteCoverage`. Never encode infinity as zero, a large decimal or an ordinary missing-value dash without its reason. |
| Avg R | Always null with `AuthoritativeInitialRiskNotRecorded`. No authoritative per-Trade initial monetary risk is recorded. Do not infer it from quantity, P&L, an unrecorded stop, or a generic risk percentage. A later risk contract is required before calculating/averaging R. |
| Daily P&L | Same complete/subtotal/coverage rules per New York close date and currency. Only dates containing selected closed Trades are returned, sorted ascending; absent dates are not synthesized as zero-valued observations. |
| Equity Curve (definition only; chart/series deferred) | A future series may show cumulative **closed-Trade P&L**, starting at zero at the selection boundary, separately by currency and basis. It is not account equity/balance: starting balances, cash flows and open exposure are not included. Each prefix must retain coverage; after an unknown Net Trade, the complete cumulative Net remains unavailable for subsequent prefixes until that missing input is resolved. A known-only subtotal line must be labeled partial and cannot bridge missing values as if complete. Reuse the same metrics on prefix populations; do not sum null daily totals as zero. |
| Trading Setups | Partition by current `TradingSetupId`, including an explicit null/unclassified bucket. Every selected closed Trade belongs to exactly one bucket; inactive historical Setup references remain included. Use identical Gross/Net rules per currency and bucket. Renaming/classification corrections can change grouping, not economics. Setup outcome does not prove process quality. Names/active flags and ranking are deferred to the reader/UI. |
| Recent Trades (presentation/query deferred) | A browse list, not a metric population: show open and closed Trades ordered by OpenedAtUtc descending, then Trade ID ascending for stable ties, using authoritative individual values. Never use its limited rows as the source for Dashboard totals. A closed-only recent-outcomes list would require an explicit different label/filter. |

Gross and Net can give different classifications (e.g. positive Gross but negative Net after costs). Their labels, counts and ratios must stay separate. A later UI must not label Gross-based wins or profitability as Net-based results.

## New York close-day attribution

`GetNewYorkCloseDate` reuses `TradingTimePolicy` / `America/New_York`: convert the zero-offset `ClosedAtUtc` instant to New York local time, then take its **civil calendar date**. This is an explicitly chosen daily analytics convention, not an exchange session, an inferred 17:00/18:00 rollover or an imported broker TradeDay. No session boundary is currently authoritative.

Winter midnight is 05:00 UTC; summer midnight is 04:00 UTC. Spring-forward skips a local hour; fall-back repeats an hour, but each UTC closure remains unambiguous and contributes once to the correct date. Original UTC timestamps remain unchanged. Non-UTC or inconsistent lifecycle inputs are rejected. Opening date, import date and audit timestamps do not drive closed-outcome attribution.

## Verification and later boundaries

Focused Application tests exercise known/unknown costs, explicit zero costs, opposing Gross/Net signs, subset coverage, all Profit Factor denominator states, break-even Win Rate, empty/open/partial-exit populations, multiple currencies, current/unclassified Setups, exact Domain prices, UTC midnight and both New York DST transitions, missing risk, duplicate facts, invalid projections, overflow and cancellation. These are calculation tests, not database or interactive Dashboard acceptance.

M12.2 tests use isolated migrated SQLite databases and production persistence registrations. They verify individual/combined/reset ID filters, missing IDs, inclusive boundaries on the days before/during/after both DST changes (including single-tick edges), both occurrences of the repeated hour, exact decimals, nullable costs/Net, historical currencies, zero and empty results, open/partial-exit exclusion, and 121 matching Trades beyond a browse page. Read-only SQLite connections reject writes, captured SQL verifies filtering/no paging/no execution or screenshot access, and cancellation is observed during SQL execution. The same reader sees committed manual insert/close/edit/Setup/delete and actual TopstepX/Tradovate import/replay outcomes. These are automated data-path tests, not Desktop acceptance.

Database-side metric aggregation, currency selection UI, equity-series construction, recent-row loading, active-view refresh, Dashboard commands, charts, labels and accessible visual presentation belong to later M12 stages. They must preserve these coverage and currency contracts rather than silently converting missing values to zero.
