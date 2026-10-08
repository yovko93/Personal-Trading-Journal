# Daily Review with AI Coaching — M15

## M15.1: scope and evidence only

`IDailyReviewEvidenceReader.GetAsync(DailyReviewQuery, CancellationToken)` provides disconnected, read-only evidence for later calculated statistics and user-requested AI generation. This milestone makes no AI request, creates no generated prose or analysis record, and adds no Daily Review UI. Existing Journal review answers remain the user's saved content. Later M15 tasks must define calculation coverage, generation, persistence and presentation explicitly.

`DailyReviewQuery(DateOnly date, Guid? tradingAccountId = null)` requires an explicit **New York calendar date**. It has no machine-timezone default or retained selection. `Guid.Empty` and a date whose exclusive end cannot be represented are rejected before database access. `FromUtc` and `BeforeUtc` reuse `TradingCalendarDayQuery`, which delegates to the existing DST-safe Dashboard/TradingTimePolicy boundaries.

| Scope | Trades | Journals |
| --- | --- | --- |
| Exact Account ID | Only that original Account ID | Only that original Account ID; excludes null-scoped entries |
| All accounts (`null` query filter) | Every Account | Every Account **plus** the distinct null-scoped All accounts entry |

The returned query echoes the requested scope. An unavailable filter never falls back to All accounts. Inactive Accounts are included; historical dangling references retain their original IDs with unavailable metadata. A valid ID with no matching records returns empty lists. Journal write uniqueness and null-scoped editing retain their existing meaning; aggregation here does not move, merge or retarget entries.

## Trade day attribution and ordering

The Trade browse projection is maintained transactionally from the Domain by normal writes/imports. This reader copies its status, lifecycle and economics rather than recomputing outcomes from displayed prices.

- **ClosedOnDate:** fully closed Trades whose authoritative `ClosedAtUtc` is in `[New York midnight, next New York midnight)`. This is the same eligible set as Calendar day details. Opening on a previous date does not split P&L or attribute the Trade to that previous date. A closure exactly at next midnight belongs to the next day. Import `TradeDay` and current Instrument pricing do not participate.
- **OpenActivityOnDate:** currently open Trades, including partial exits, with at least one allocated execution inside those same UTC bounds. These are separate context, **not inputs to the selected day's realized closed-Trade metrics**. An older open position with no execution that day is omitted. No end-of-day valuation is inferred.
- **UnavailableLifecycleActivityOnDate:** a Trade with an absent projection, unavailable closure time or unsupported lifecycle status can be retained through an execution on that day. Its available facts and limitations are explicit; it must not be silently treated as a completed Trade.

The candidate query uses existence checks, not execution joins that multiply Trade rows. Each Trade ID appears once in a response. Several executions in the day do not increase the Trade count. Open activity may legitimately be context on several dates; it is not a second realized-day attribution. Once a Trade closes, subsequent reads follow its current closed status and closure date. This is **current persisted evidence**, not a reconstruction of which positions were open at a past midnight.

Spring and autumn transition days span 23 and 25 hours respectively. Stored UTC instants stay authoritative; repeated autumn clock times remain separate instants. Trade rows sort by recorded closure descending (null last), then Trade ID. Executions retain full lifecycle sequence order, then execution ID. Assigned Mistakes sort by definition ID and assignment ID. Journals sort null scope first, then Account ID and Journal ID; renaming an Account does not reorder evidence. Ordering never depends on Trades-list paging or UI sorting.

## Contract and provenance

`DailyReviewEvidence` contains the request and two read-only collections, `Trades` and `Journals`; it contains no combined P&L or inferred assessment.

Each `DailyReviewTradeEvidence` includes:

- Original Trade ID, Account and Instrument IDs, current names/activity/availability, optional Setup reference, and assigned Mistake definition/assignment IDs with assignment audit timestamps. A null Setup means unassigned; a reference with an ID and null metadata means unavailable. Assigned Mistakes are user classifications, not inferred violations.
- Historical pricing currency and point value, Trade creation/update UTC timestamps, inclusion reason, and nullable `DailyReviewTradeFacts`. Facts copy projection version, status/direction, opened/closed UTC instants, open quantity, average prices, total costs, Gross P&L and authoritative Net P&L.
- Full `TradeExecutionDetailItem` facts, reusing the Trade-details contract: stable execution ID, sequence, UTC instant, side, allocated quantity, price, nullable commission and fees, and available broker/order/execution references. Execution total cost uses the existing commission-plus-fees semantic; a missing component keeps it null. Full lifecycle context can include executions outside the selected date; their timestamps are preserved.
- `DailyReviewTradeQuality` flags for missing projection/executions/closure, unsupported projection format, unknown commissions/fees/Gross/Net, and unavailable references. A closed projection with unavailable P&L is retained, not changed into a zero result. These flags describe evidence limitations, not rule violations or a comprehensive corruption diagnosis. Open-Trade P&L being unavailable is expected Domain behavior.

No Net estimate is invented: unknown authoritative `NetPnL` remains null even when Gross is known. Genuine zero costs and zero P&L remain numeric zeros. Historical currencies stay on each Trade; the reader never sums or converts them. Any later use of the Dashboard Effective Net estimate must be an explicitly labeled calculation with its own coverage, not a replacement of these source values.

Each `DailyReviewJournalEvidence` includes original Journal ID, exact trading date and saved Account ID, current Account name/state, all four exact text fields, Draft/Completed state, durable revision number, and created/updated UTC timestamps. An absent entry is an absent list item. An existing entry with `Text == ""` or empty optional answers still exists. Legacy Completed entries with only answers remain readable. Whitespace and Unicode are not trimmed or rewritten. Only current content is read; revision browsing remains the separate Journal history contract.

Journal `(ID, Revision)` references its current durable revision. Trades have **no durable per-Trade revision token**: audit timestamps and source IDs are the available metadata. `ProjectionVersion` is the projection **format**, not a concurrency token. Reference metadata is current, not a historical name snapshot. Future persisted AI analysis must retain the actual evidence it used if reproducibility after edits/deletion is required; M15.1 does not pretend IDs/timestamps alone can restore old Trade content.

## Persistence, consistency and cancellation

`DailyReviewEvidenceReader` is registered by `AddPersistence`. Every call creates a fresh `JournalDbContext`, uses no-tracking projections and executes four SELECTs: selected Trades/references, executions, assigned Mistakes, and current Journals. Filtering is performed in SQL, using the same selected-Trade subquery for child batches. No query runs once per row; no arbitrary page or result cap silently drops evidence. Memory scales with the selected day's Trades and their full executions/Journals; future prompt-size policies must be explicit and must not redefine this complete source read.

A SQLite **deferred read transaction** establishes one database snapshot at the first SELECT. All four batches observe that snapshot, including when another connection commits between the Trade and Journal reads. It does not acquire an immediate writer reservation. The transaction and context are disposed after assembling the response; no SaveChanges, projection repair, schema update, Journal revision or other database write is performed. A new call reflects later commits. This boundary is intentionally SQLite-specific in Infrastructure.

Cancellation is checked before context creation, passed to connection/query operations, checked while assembling Trades and again before return. Cancellation or a read error throws; no partial response is published or retained, and another call remains usable. A malformed record with neither a usable day-attribution projection nor any dated execution cannot be assigned to a selected day and is omitted; this reader is not a whole-database integrity audit.

No migration is required: existing Trade/browse/execution/reference records and current Journal fields already contain the evidence. No Trade economics, import rules, Journal write/revision policy, Calendar summaries or navigation change.

## Verification

Baseline: clean `develop`, `b04ba12a0fef2a7c28cdd5a19e4ed0ebe586b429`. Focused Release: **15/15 passed**, zero failures/skips (5 Application, 10 Infrastructure). Tests use isolated migrated SQLite databases and cover:

- Exact and aggregate scope, null-scoped and inactive/unavailable Account journals, source identities and current revision refresh.
- Calendar-equivalent closure selection, cross-midnight Trades, both DST changes and precise inclusive/exclusive boundaries, deterministic ties and unique Trade rows.
- Open/partial activity versus realized closed results, known/unknown cost components, mixed currencies, zero P&L, missing projections/executions/economics/references, empty days/fields and legacy entries.
- A 75-Trade date with four SELECTs on an actual read-only SQLite connection; no tracked entities or writes.
- Cancellation before/between queries, subsequent reuse, and a concurrent WAL commit proving the four reads cannot mix database snapshots.

Full parallel Release: **3,027/3,027 passed**, zero failures/skips — Domain 454, Application 534, Infrastructure 816, Desktop 1,223. Release build passed with **zero warnings/errors**; EF reports **no pending model changes**; `git diff --check` passed. Synthetic TRX/log evidence is retained under ignored `artifacts/m151/` and `artifacts/m151-*.log`. No UI or AI interaction is part of this milestone, and no real journal was used for verification. These are local results; GitHub Actions must verify the user's eventual commit/push.
