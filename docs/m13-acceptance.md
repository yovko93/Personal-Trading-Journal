# M13 Calendar acceptance

Review date: 2026-10-03. Audited branch: `develop`. Actual HEAD: `93746d3d32655fca0af2a4ba61002f85bf44408e` (not the last reported `a93d6ad`). The worktree was clean before this audit. Scope covers M13.1–M13.6 and all subsequent Calendar UI refinements. See [Trading Calendar](trading-calendar.md) for the delivered contracts and behavior.

## Decision and findings

**Automated acceptance: Passed. Full interactive acceptance: Unverified/open.** No confirmed Calendar code defect was found. The documentation did contain obsolete below-grid/modal descriptions, superseded View-navigation evidence and historical test counts presented alongside current behavior. These have been consolidated into the current specification and this evidence record. No runtime code, schema, stored economics or import behavior was changed.

Only the following observations are user-verified, as reported in the M13.7 request:

- Calendar cell hover behavior.
- Day Performance modal controls and backdrop dismissal.
- Explicit UTC-4 display.

These reports do not establish other modal/editor/chart interactions, UTC-5 live rendering, every close/focus/unsaved-edit combination, or both-theme/high-DPI acceptance. Those combinations remain automated evidence with a live check outstanding.

## Acceptance matrix

Passed below means the named automated tests passed in this audit. No automated ViewModel, native-window or render test is labelled interactive verification.

| Scenario | Automated result and evidence | User/live evidence |
| --- | --- | --- |
| Monday–Sunday grids, adjacent dates, four/five/six rows, Today, navigation and selection | **Passed** — `TradingCalendarReaderTests`, `CalendarViewModelTests`, `CalendarViewLayoutTests`: complete grids, New York Today, mouse/Enter/Space/automation selection, cell/marker/today/focus states and late-month rejection. | **Unverified** live navigation and selection combinations. |
| Monthly versus weekly totals; Saturday versus Sunday | **Passed** — Application `SeptemberGridIncludesAdjacentDatesAndFullSundayWeekTotals`, `CalendarSummaryTests`, SQLite month/day readers: active-month daily-only sum; seven-day weekly totals; weekly-only Saturday rendering; Saturday query returns its own Trades. | **Unverified** live amount comparison and Saturday modal data. |
| Account/currency scope, inactive/unavailable Accounts and empty/zero/estimated/unknown economics | **Passed** — `CalendarFilterTests`, `CalendarSummaryTests`, Application and migrated-SQLite reader tests: retained filters, explicit unavailable selection, consistent filtered counts/buckets, distinct financial states, no currency addition. | **Unverified** live selector/recovery flows. |
| New York attribution across both DST transitions | **Passed** — `TradingCalendarDayTests`, both Infrastructure Calendar reader suites and Desktop time tests: 23/25-hour date bounds, authoritative UTC closures, repeated local times and genuine 04:00. | **Unverified** live spring/fall data. |
| Modal/backdrop, X/Close/Escape, maximize/restore/minimize/return, focus and unsaved edits | **Passed** — `CalendarDayModalTests` (including window partial): actual owned native-window tests exercise consumed backdrop clicks, inside clicks, close paths/focus, retained drafts and owner-disabled chrome states. | **User-verified** modal controls/backdrop dismissal only; other combinations **Unverified** live. |
| Day Performance closure points, currency series, gaps, axes, guides/tooltips and offsets | **Passed** — `CalendarDayPerformanceChartTests`, `CalendarDayModalTimestampTests`, `TradingTimePresentationTests`, `ChartTooltipFollowerTests`: actual closure-time steps, coincident points, zero crossings/gaps, full-height hover/focus geometry, scrolling, clock-only ticks and explicit UTC-4/UTC-5/UTC+5:30 help. | **User-verified** explicit UTC-4 display only. Guide/pointer placement/keyboard interaction and UTC-5 live display **Unverified**. |
| Responsive Trades table and inline View/Edit/classifications | **Passed** — native modal layout tests and `CalendarInlineTradeTests`/`CalendarSqliteRefreshTests`: all nine aligned columns at tested default width, narrow horizontal reachability, Account cap/Net centering, Setup/Mistakes batching/presentation, correct Trade targeting, save/cancel/errors and production-store edit followed by row/day/chart/month refresh. | **Unverified** live table widths and inline editing. |
| Committed changes/imports, noncommitted results and stale responses | **Passed** — `CalendarFilterTests`, `CalendarSqliteRefreshTests`, `MainWindowViewModelTests`, `TradovateImportAcceptanceTests`: create/edit/delete, both import providers, dispatcher notification, commit after cleared presentation, no commit refresh on replay/Blocked/rollback/failure, deferred refresh during editing, obsolete reads discarded. | **Unverified** live commit-while-Calendar/modal-active scenarios. |
| Themes, narrow/high-DPI layout, hover, wheel and accessibility | **Passed** — compiled WPF layout/interaction tests for both resource sets, 96/240-DPI renders, marker-only tooltip hit testing, outcome hover restoration, selected/busy automation descriptions, keyboard targets and scoped vertical/horizontal wheel routing. Generated renders inspected separately below. | **User-verified** Calendar hover only. Actual monitor scaling, wheel/keyboard/screen-reader acceptance and full Light/Dark flows **Unverified**. |

## Concrete isolated-data checks

- September 2026 grid spans August 31–October 4. The synthetic Application fixture has adjacent August 31 = 2 USD, Saturday September 5 = 3 USD, Sunday September 6 = 4 USD and adjacent October 4 = 5 USD. First weekly total is 9 USD; September Monthly P/L is 7 USD, counting Saturday/Sunday once and excluding both adjacent dates.
- The migrated-SQLite inactive-Account fixture has Saturday USD −285 estimated and USD 0 verified, Saturday EUR 10, and Sunday USD 20. Saturday's own list has three Trades; its visible week has USD −265 across three USD Trades and EUR 10 across one EUR Trade. Monthly totals stay separate and retain the USD estimate. Another Account's 999 USD Trade is excluded.
- Unknown Gross/Net does not become zero or a complete subtotal. Genuine zero remains populated; empty dates have no metrics. Readers use no-tracking fresh contexts, and isolated assertions retain unchanged Trade/execution counts with no read writes.
- SQLite selected-day results are not limited to one browse page. Classification reads are batched and existing peak-Size logic is reused. A production-store cost correction refreshes selected-day/chart/month values without changing the selected filters.

## Build, tests and migration consistency

| Check | Result |
| --- | --- |
| Focused Release tests | **313 passed**, 0 failed/skipped: Application 12, Infrastructure 20, Desktop 281. No Domain tests match this focused filter. |
| Complete Release suite | **2,497 passed**, 0 failed/skipped: Domain 400, Application 516, Infrastructure 719, Desktop 862. |
| Solution Release build | **Passed**, 0 warnings and 0 errors. |
| Migration consistency | **Passed** — EF reports no model changes since the latest migration; schema/migration tests also pass. The design-time factory uses in-memory SQLite, not the journal. |
| `git diff --check` | **Passed** after documentation edits. |

Commands used from the repository root:

```powershell
dotnet build PersonalTradingJournal.sln --configuration Release --artifacts-path bin/calendar-inline-verification --no-restore
dotnet test PersonalTradingJournal.sln --configuration Release --artifacts-path bin/calendar-inline-verification --no-restore --filter 'FullyQualifiedName~Calendar|FullyQualifiedName~MainWindowViewModelTests|FullyQualifiedName~TradingTimePresentationTests|FullyQualifiedName~ChartTooltipFollowerTests|FullyQualifiedName~Persistence.Migrations|FullyQualifiedName~JournalDbContextDesignTimeFactoryTests' --blame-hang-timeout 120s
dotnet test PersonalTradingJournal.sln --configuration Release --artifacts-path bin/calendar-inline-verification --no-build --blame-hang-timeout 120s
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --startup-project src/PersonalTradingJournal.Infrastructure --configuration Release
git diff --check
```

Native modal/chart tests use a fresh WPF test-host process because another existing test shuts down WPF's process-wide Application. That is automated interaction coverage, not a live app session. `PTJ_CALENDAR_RENDER_DIRECTORY` was set only for the full test command to generate synthetic component images and measurement files outside the repository.

## Measured layout and inspected renders

Native-window measurements, identical behavior in Light and Dark, list columns in their current order. Widths are DIPs; generated images use the stated render DPI, not proof of physical-monitor scaling.

| Owner / render DPI / Account text | Table viewport / width | Account / Net / Setup / Mistakes | Action fits initially / trailing gap |
| --- | --- | --- | --- |
| 1,280 / 240 / short | 1,184 / 1,184 | 100 / 150 / 234.4 / 281.6 | Yes / 0 |
| 1,280 / 240 / long | 1,184 / 1,184 | 159.2 / 150 / 207.6 / 249.2 | Yes / 0 |
| 1,900 / 96 / short | 1,228.8 / 1,228.8 | 100 / 150 / 254.8 / 306 | Yes / 0 |
| 1,900 / 96 / long | 1,228.8 / 1,228.8 | 159.2 / 150 / 228 / 273.6 | Yes / effectively 0 |
| 480 / 240 / long | 366.8 / 1,048 | 159.2 / 150 / 150 / 170.8 | No; reachable by horizontal scroll / 0 |

The 1,280-DIP owner has a 1,267.2-DIP client and 1,235.2-DIP panel; the 1,900-DIP owner uses the 1,280-DIP panel cap. These measured initial layouts keep Net compact and give flexible width to classifications without an empty trailing column.

Inspected generated images: Light grid at 1,100 DIPs/96 DPI; Dark grid at 640/240 with horizontal scrollbar; long-Account Light expanded modal at 1,280/240; short-Account Dark expanded modal at 1,900/96; Light day chart at 960/96; Dark day chart at 480/240 with focused guide. They show distinct empty/zero/unknown/currency states, weekly-only Saturdays, aligned wrapped classification columns, inline details, clock-only axes and subtle step fills. These are component renders using test resources, not screenshots of the running application or evidence for all shell styling.

## Live attempt and outstanding Windows checklist

The rebuilt executable was `bin/calendar-inline-verification/bin/PersonalTradingJournal.Desktop/release/PersonalTradingJournal.Desktop.exe`, file version 1.0.0.0, product version `1.0.0+93746d3d32655fca0af2a4ba61002f85bf44408e`. It launched with a new isolated data root outside the repository and exposed a native window titled Personal Trading Journal. The computer-use helper's window discovery, app discovery and repeated exact-title discovery returned **no targetable PTJ window**. No Calendar navigation, screenshot capture or live input could therefore be performed. Only that isolated process was stopped afterward. Startup is not interactive acceptance; no real journal or customer CSV was accessed.

On a Windows desktop, launch a separate isolated instance from the repository root (never omit the argument):

```powershell
$calendarAuditRoot = Join-Path $env:TEMP ('PTJ-M13-Acceptance-' + [Guid]::NewGuid().ToString('N'))
& '.\bin\calendar-inline-verification\bin\PersonalTradingJournal.Desktop\release\PersonalTradingJournal.Desktop.exe' --isolated-data-root $calendarAuditRoot
```

1. Create only synthetic Accounts/Instruments/closed Trades in this instance: include USD profit/loss/zero, unknown costs, EUR, Saturday and Sunday, and an inactive historical Account. Record expected totals; never point the app at the normal journal or copy customer CSVs into Git.
2. Navigate February 2021 (four rows), September 2026 (five), and March 2026 (six), then Today. Check adjacent dates, today/selected/focus states and retained Account/currency filters. Compare active-month Monthly P/L with daily values and seven-day Saturday totals; Saturday modal must list only Saturday Trades.
3. Switch Account/currency and an empty month. Expect separate currencies, quiet empty dates, populated zero dates, honest unavailable amounts and explicit unavailable-Account recovery. Use synthetic closures around March 8 and November 1, 2026; verify New York day boundaries and UTC-4/UTC-5 details without offsets on clock ticks.
4. Open populated, empty, Saturday and adjacent dates by click and Enter/Space. Exercise each close action, backdrop without click-through, maximize/restore/minimize/return and selected-cell focus restoration. Start an inline edit: dismissal must require Save/Cancel, and switching apps must not dismiss.
5. In each currency chart, hover above/below closures and across regions, focus with Tab, scroll/resize and approach window edges. Expect one aligned guide, exact closure/cumulative tooltip, prompt pointer following, clock-only ticks, readable Profit/Time axes and preserved gaps.
6. At a 1,280-DIP owner check all nine headers and View without horizontal scrolling. At a narrow/high-DPI size scroll to Action and back; check long names/tooltips and expanded View/Edit alignment. Save a supported synthetic edit, then verify row/day/chart/month totals refresh; cancel another edit without writes.
7. With Calendar or its modal active, perform synthetic committed create/edit/delete and both import providers; verify current data without re-navigation. Repeat cancelled/replay/Blocked cases with no committed refresh. Change month/filters rapidly during reads and confirm no old results appear.
8. Repeat key flows in Light/Dark and at normal/narrow/high display scaling. Wheel over cells, chart, table and controls; check single vertical delivery, horizontal navigation, visible focus and accessible names/selected/busy states. Add Journal must remain disabled. Record these live results before declaring full interactive M13 acceptance.
