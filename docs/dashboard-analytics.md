# Dashboard Analytics — M12.1 metrics, M12.2 queries and M12.3 period series

## Scope and authoritative inputs

Implemented: pure, read-only Application contracts and `DashboardMetricCalculator`, with per-currency summary and Setup metrics; M12.2 adds the filtered `IDashboardAnalyticsReader` persistence path; M12.3 adds daily/weekly period and cumulative series. No Dashboard wiring, rendered chart or migration is added. The existing Dashboard cards remain placeholders.

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
| Weekly P&L | Same rules as daily P&L, grouped by Monday–Sunday New York dates, keyed by the Monday date (not a week number). Only selected closed Trades contribute, including at partially selected week boundaries. Empty weeks are omitted. |
| Equity Curve input (M12.3 cumulative series; chart deferred) | Daily and weekly points include cumulative **closed-Trade P&L** from the selection boundary, separately by currency and basis. This is not account equity/balance: starting balances, cash flows and open exposure are not included. Each prefix retains coverage; after an unknown Net Trade, complete cumulative Net remains unavailable for subsequent prefixes until the missing input is resolved or excluded by a new selection. A known-only subtotal line must be labeled partial and cannot bridge missing values as if complete. Never sum null period totals as zero. |
| Trading Setups | Partition by current `TradingSetupId`, including an explicit null/unclassified bucket. Every selected closed Trade belongs to exactly one bucket; inactive historical Setup references remain included. Use identical Gross/Net rules per currency and bucket. Renaming/classification corrections can change grouping, not economics. Setup outcome does not prove process quality. Names/active flags and ranking are deferred to the reader/UI. |
| Recent Trades (presentation/query deferred) | A browse list, not a metric population: show open and closed Trades ordered by OpenedAtUtc descending, then Trade ID ascending for stable ties, using authoritative individual values. Never use its limited rows as the source for Dashboard totals. A closed-only recent-outcomes list would require an explicit different label/filter. |

Gross and Net can give different classifications (e.g. positive Gross but negative Net after costs). Their labels, counts and ratios must stay separate. A later UI must not label Gross-based wins or profitability as Net-based results.

## New York close-day attribution

`GetNewYorkCloseDate` reuses `TradingTimePolicy` / `America/New_York`: convert the zero-offset `ClosedAtUtc` instant to New York local time, then take its **civil calendar date**. This is an explicitly chosen daily analytics convention, not an exchange session, an inferred 17:00/18:00 rollover or an imported broker TradeDay. No session boundary is currently authoritative.

Winter midnight is 05:00 UTC; summer midnight is 04:00 UTC. Spring-forward skips a local hour; fall-back repeats an hour, but each UTC closure remains unambiguous and contributes once to the correct date. Original UTC timestamps remain unchanged. Non-UTC or inconsistent lifecycle inputs are rejected. Opening date, import date and audit timestamps do not drive closed-outcome attribution.

## Verification and later boundaries

### Period and cumulative contracts (M12.3)

`CurrencyTradeMetrics.Days` returns `DailyTradeMetrics(NewYorkDate, Metrics, CumulativeMetrics)`; `Weeks` returns `WeeklyTradeMetrics(WeekStartingMonday, Metrics, CumulativeMetrics)`. Both metrics use the existing `ClosedTradeMetrics`: Trade count, unknown-cost count, separate Gross/Net complete totals, labeled known subtotals and coverage. Ratios retain M12.1's completeness and denominator rules. No UI-formatted strings or recalculated price economics enter the series.

Dates come only from each authoritative ClosedAtUtc via the existing New York policy. Monday is computed by subtracting the local date's Monday-based weekday offset; Sunday stays in the preceding Monday's week. Months and years do not split a week. DST affects the UTC-to-local conversion, not the number of dates in a week; both UTC occurrences of a fall-back hour contribute once per Trade. Periods sort ascending by local date/Monday; currencies sort ordinally. Within each period, accumulation uses ClosedAtUtc then Trade ID for deterministic results independent of query enumeration order.

Each cumulative point contains all selected closed Trades in that currency up to the end of that occupied period, including the period itself. The numerical origin is zero before the first selected Trade, but no synthetic zero point is emitted. Periods containing no selected closed Trades are omitted—even if a filter spans them. A period containing real break-even Trades is retained with numeric zero. An empty selection has no currency or series points; an open-only population passed directly to the calculator has empty series. Gross and Net coverage evolve independently. An unknown Net cannot contaminate another currency, and a later completely known day/week can have complete period Net while its cumulative Net is still partial/unavailable.

Account, Instrument and inclusive closure-date filters are applied by M12.2 **before** aggregation. A week crossing a filter boundary is labeled by its actual Monday but includes only selected dates/Trades; a complete Net coverage flag means all **selected Trades** are known, not that the full calendar week was selected. Re-querying a narrower range rebuilds the sequence from that selection, without carrying unknown coverage from excluded history. Later UI must retain the selected date-range context. Neither broker TradeDay, current Instrument metadata nor Trades-list paging is involved.

Running accumulators reuse the same M12.1 rules for summaries, Setup groups, periods and prefixes; they do not repeatedly scan all earlier periods. Values and returned collections are read-only snapshots. Checked decimal addition and normal decimal division apply without presentation rounding. Any period, prefix or ratio overflow throws `OverflowException` and returns no partial snapshot; there is no clamping, skipped Trade or infinity sentinel. Cancellation propagates from the reader through enumeration/accumulation. This stage adds no persistence writes or new query calls.

Synthetic USD example (all dates are New York closure dates): December 31, 2026 contains Gross +10 with costs 2 and Gross −3 with costs 0; January 3, 2027 contains Gross +5 with unknown costs; January 4 contains a known zero Trade.

| Period | Trades | Gross | Complete Net | Known Net subtotal | Net coverage |
| --- | ---: | ---: | ---: | ---: | --- |
| Day 2026-12-31 | 2 | 7 | 5 | 5 | 2/2 |
| Day 2027-01-03 | 1 | 5 | unavailable | unavailable | 0/1 |
| Day 2027-01-04 | 1 | 0 | 0 | 0 | 1/1 |
| Week starting 2026-12-28 | 3 | 12 | unavailable | 5 | 2/3 |
| Week starting 2027-01-04 | 1 | 0 | 0 | 0 | 1/1 |

At the last daily and weekly point, cumulative Gross is 12 USD; cumulative complete Net is unavailable; its known subtotal is 5 USD with 3/4 coverage. December 31's earlier cumulative Net snapshot remains 5 USD. Dates without selected Trades do not appear as zero points.

### Automated evidence

Focused Application tests exercise known/unknown costs, explicit zero costs, opposing Gross/Net signs, subset coverage, all Profit Factor denominator states, break-even Win Rate, empty/open/partial-exit populations, multiple currencies, current/unclassified Setups, exact Domain prices, UTC midnight and both New York DST transitions, missing risk, duplicate facts, invalid projections, overflow and cancellation. These are calculation tests, not database or interactive Dashboard acceptance.

M12.2 tests use isolated migrated SQLite databases and production persistence registrations. They verify individual/combined/reset ID filters, missing IDs, inclusive boundaries on the days before/during/after both DST changes (including single-tick edges), both occurrences of the repeated hour, exact decimals, nullable costs/Net, historical currencies, zero and empty results, open/partial-exit exclusion, and 121 matching Trades beyond a browse page. Read-only SQLite connections reject writes, captured SQL verifies filtering/no paging/no execution or screenshot access, and cancellation is observed during SQL execution. The same reader sees committed manual insert/close/edit/Setup/delete and actual TopstepX/Tradovate import/replay outcomes. These are automated data-path tests, not Desktop acceptance.

M12.3 tests cover multiple Trades per day, Monday/Sunday and month/year boundaries, both DST transitions, unknown-first/known-later coverage, actual zero versus null, exact decimals, missing Gross, separate currencies, stable ordering, empty/open-only populations, cancellation and period/prefix overflow. An isolated migrated SQLite test reads 121 same-day Trades plus later known/unknown outcomes through the production reader, exercises combined Account/Instrument/date filters, and proves a narrower query resets cumulative coverage. The read-only SQLite test also checks the resulting daily and weekly series. This is automated Application/Infrastructure acceptance, not interactive Desktop evidence.

Database-side metric aggregation, currency selection UI, chart rendering, recent-row loading, active-view refresh, Dashboard commands, labels and accessible visual presentation belong to later M12 stages. They must preserve these coverage and currency contracts rather than silently converting missing values to zero.
