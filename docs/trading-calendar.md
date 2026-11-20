# Trading Calendar

The Calendar day’s inline Journal form includes a visible **Journal Account** selector before Journal text, preselected from Calendar (All accounts = null scope). Changing it does not retarget the Calendar's Trades, filters or financial summaries. Save/Cancel-Draft write the form's scope; an existing journal moves with its preserved history only after a revision-checked atomic save to an empty target scope. Occupied targets, failures and stale revisions retain the fields and selected target; no automatic merge or copy occurs. The modal stays open and journal indicators refresh for both affected scopes. See [Journal form Account policy](daily-journal.md#form-account-and-selected-review-presentation). Calendar All accounts indicators continue to aggregate every scope, independently of exact form editing.

Current inline Journal actions: green **Save Journal** completes the entry; violet **Cancel** saves changed fields as Draft and closes only the editor, without a discard prompt. Reopen is local: an unchanged Completed entry, including change-then-revert, closes without any write/revision or status change. An exactly empty new form also closes without a write. Failures/conflicts retain edits; other modal/navigation close guards remain unchanged. Add/Continue use the established mint/teal accent; ordinary Journal actions use blue accents; Delete stays solid red. See [current Cancel and aggregate-indicator semantics](daily-journal.md#completed-cancel-and-calendar-aggregation-2026-10-07).

## Read-only data and financial semantics (M13.1)

`TradingCalendarQuery(year, month, tradingAccountId?)` selects a New York month. A null Account means all accounts; an empty ID is invalid. `ITradingCalendarReader.GetAsync` returns `TradingCalendarMonth` with `MonthStart`, `GridStart`, `GridEnd`, ordered `GridDates` and separate historical-currency buckets. The grid starts on Monday on/before the first and ends on Sunday on/after the last, giving four to six complete rows. September 2026 includes August 31 through October 4. Empty months still have complete date grids and no fabricated currency bucket.

Calendar assigns a fully closed Trade by its authoritative `ClosedAtUtc`. In `America/New_York`, a close before 18:00 belongs to the same date. At 18:00 or later it belongs to the next date. A November 1 close at 18:12 contributes to the November 2 grid totals. The Trades workspace displays the original closure time.

The reader makes one inclusive grid-date request to `IDashboardAnalyticsReader`. For an attributed date, the SQL interval runs from 18:00 the previous New York date through just before 18:00 on the labeled date. Each endpoint is converted to UTC using its own time-zone offset, so spring and fall DST intervals can span 23 and 25 hours. The grid groups those same closures by attributed date for daily, weekly and monthly totals. Adjacent intervals meet at the cutoff, so a closure at 18:00 enters exactly one date. Open and partially exited Trades, import TradeDay, current Instrument prices and Trades-list paging do not determine results. Reads forward cancellation and make no database writes.

The Calendar grid requests this boundary from the shared analytics reader. The shared day-reader API still supports the same trading-day interval. Dashboard, Breakdown, standalone Journal Trade Context, Rules, Daily Review and Period Reviews continue to use New York civil dates. Journal cards and status indicators use the stored Journal date and exact scope. The cutoff affects Calendar Trade attribution and does not rewrite stored timestamps, economics or historical review snapshots. Today continues to highlight the current New York civil date.

Each currency has ordered `TradingCalendarWeek` rows with seven daily values and a Monday–Sunday `ClosedTradeMetrics` summary, reusing M12 daily/weekly calculations and historical pricing currency. Saturday and Sunday activity contribute once to their week, including adjacent-month activity. There is no cross-currency monetary total.

`Metrics == null` means no closed Trades, not financial zero. A populated period with `EffectiveNet.Total == 0` is genuine zero; a null total is unavailable/incomplete. Known subtotals and coverage remain partial evidence, never a complete total. Effective Net uses authoritative Net when known, otherwise known Gross as an estimate for a closed Trade with unknown costs. Unknown Gross cannot become a numeric estimate. Estimate counts, strict verified Net and coverage remain available; stored unknown costs and Net remain null. Culture-aware display formatting does not change decimal precision.

`TradingCalendarMonthlyPnl.From` combines only daily metrics belonging to the displayed month, with checked arithmetic and coverage. It excludes adjacent-month dates and never sums weekly summaries again. Saturday's own day and Sunday each contribute once. Currencies remain separate; incomplete months show a dash, genuine zero stays numeric, and no activity says No closed Trades. Overflow becomes a recoverable read error, not a truncated total.

### Trading day cutoff verification (9 November 2026)

The Release build passed with zero warnings and errors. Focused Release checks passed 59 Application, 31 Infrastructure and 64 Desktop cases. The SQLite tests compare the grid and selected-day Trade IDs and totals for one Account and All accounts at 17:59:59, 18:00, 18:12 and midnight. They cover spring and fall DST dates, a month boundary at the edge of the grid and a year boundary. Separate checks verify that both ends of a DST trading day use their own offsets and that the shared civil-date queries still select the original date.

Light and Dark offscreen grid renders at 1100 DIP/96 DPI and 640 DIP/240 DPI show the synthetic September 23 close at 18:12 on September 24. The populated grid fixture uses theme brushes and simplified header controls. The day-reader checks from that release also verified that the Trade appeared only on the destination date. The Accounts checks verify the plain red **Remove** button and its confirmation command alongside the separate Account Actions menu. Artifacts are under `TestResults/calendar-cutoff-focused` and `TestResults/calendar-cutoff-renders`. Live Calendar and payout interaction, physical display scaling and screen-reader checks remain pending.

The 9 November 2026 complete `pwsh -File scripts/Test-Release.ps1` run exited **0** with **5,569 passed, 0 failed and 1 skipped**. Exclusive WPF passed 151, Domain 602, Application 1,052, Infrastructure 1,616 and parallel Desktop 2,148. The sole skip was the existing [Rules-view quarantine](rules-view-quarantine.md). Full results are under `TestResults/release-20261109-173551-e8a8d79659d4410399de5e82818bb93d`.

#### Changed files for the cutoff and payout label

- `README.md`
- `docs/account-payouts.md`
- `docs/trading-calendar.md`
- `src/PersonalTradingJournal.Application/Analytics/DashboardAnalyticsQuery.cs`
- `src/PersonalTradingJournal.Application/Analytics/DashboardMetricCalculator.cs`
- `src/PersonalTradingJournal.Application/Calendar/TradingCalendarDayDetails.cs`
- `src/PersonalTradingJournal.Application/Calendar/TradingCalendarDayQuery.cs`
- `src/PersonalTradingJournal.Application/Calendar/TradingCalendarQuery.cs`
- `src/PersonalTradingJournal.Application/Calendar/TradingCalendarReader.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Calendar/CalendarViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/Accounts/AccountsView.xaml`
- `src/PersonalTradingJournal.Infrastructure/Analytics/DashboardAnalyticsReader.cs`
- `src/PersonalTradingJournal.Infrastructure/Calendar/TradingCalendarDayReader.cs`
- `tests/PersonalTradingJournal.Application.Tests/Calendar/TradingCalendarDayTests.cs`
- `tests/PersonalTradingJournal.Application.Tests/Calendar/TradingCalendarReaderTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Accounts/AccountBalanceLayoutTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Calendar/CalendarFilterTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Calendar/CalendarSummaryTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Calendar/CalendarViewLayoutTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Calendar/TradingCalendarDayReaderTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Calendar/TradingCalendarReaderTests.cs`

## Month grid and compact summaries (M13.2–M13.3)

Calendar is a navigation destination with a retained `CalendarViewModel`. Its initial month and Today marker use the current New York date. Previous, Next and Today navigate months; centered Monthly P/L sits above navigation. Month changes replace dates synchronously, cancel old reads and reject late responses. Loading, cancellation and recoverable errors keep the grid and recovery controls available.

The reference-derived presentation is a spacious Monday–Sunday grid with subtle borders and compact financial content:

- Non-Saturday dates with Trades show daily Effective Net and a closed-Trade count per currency. Empty dates remain quiet. Positive/negative amounts are green/red; zero/unavailable amounts are neutral. Active-month daily cells have subtle outcome surfaces. Mixed currencies have separate lines/bands, never a combined total.
- Saturday shows its top-centered date, then only Week 1, Week 2, etc., weekly P/L and weekly Trade count, centered in the remaining cell. No Daily section or reserved daily space remains. An empty week shows a dash and 0 Trades without inventing currency or monetary zero.
- Every date number is top-centered. Today has a blue circle. Selection outlines the whole cell and number marker; keyboard focus has a separate high-contrast marker ring. These states remain discernible together.
- Hover anywhere in a cell to lighten its outcome surface. Zero, empty, unavailable, Saturday and adjacent-month cells use a neutral hover; mixed-currency bands lighten separately. Leaving restores the surface, with adjacent dates subdued and selection/today/focus preserved.
- Only the day-number marker owns a tooltip: date, that date's daily P/L and Trade count, including Saturday's own daily result. Amounts, counts, weekly content and empty areas do not inherit a parent tooltip. Estimated-cost/coverage explanations stay in accessible descriptions, not visible cost prose in this tooltip.

Whole-cell click, Tab then Enter/Space, and UI Automation Invoke select every date, including Saturday and adjacent dates. The grid has an 840-DIP minimum with horizontal scrolling at narrower widths; the page scrolls vertically. Wrapped currency summaries can increase row height. Shared Light/Dark resources supply outcome, hover, text, selection and focus brushes.

## Opening a Calendar day

Opening any date shows the date and the **Day Journal** section in the owned Calendar day dialog. The same layout opens on dates with and without closed Trades, including Saturdays and adjacent-month dates. The Day Performance section, its **Assess day Rules**, **Retry** and Trade-load **Cancel** actions, and the Closed Trades list with its inline Trade editor were removed. Opening a date reads its Journals. The monthly grid supplies the financial summaries and counts, including Saturday’s daily tooltip and separate weekly summary. Use **Trades** to view or edit a Trade.

The dialog still covers the owner’s client area and prevents Calendar input while open. The centered panel fits the Journal content vertically up to its 1,280 × 820 DIP size cap, with a 16-DIP backdrop inset. Longer content scrolls. Maximize and Restore change the panel within the owner area. Minimize and restore carry the modal and its draft with the owner. Close with ×, **Close**, Escape or a complete backdrop click. Close guards protect unsaved Journal changes. Closing retains the month, date and filters and restores focus to the selected date.

### Day Journal

**All accounts** lists cards across all Account scopes, including the separate All accounts Journal. A specific Account shows only its own entries. Each card shows the Account, Draft or Completed state and saved text. Open, Continue and Close use the saved card’s exact identity. **Add Journal** opens a new form with an explicit Account selector and protects occupied scopes. Currency filtering applies to grid economics. Journal cards and indicators follow the date and Account.

A saved card expands its inline Journal directly below the card. A new Journal has a separate **Close Journal** action until it has a saved card. **Save Journal** completes a valid entry. **Cancel** saves changed fields as Draft and closes the editor after the write. A new empty form and a Completed entry reopened without changes close without a write. Close controls still ask about unsaved edits. **Refresh Journals** refreshes the day list, indicators and any open entry after its dirty-work guard. **Delete Journal** retains its permanent-history confirmation and revision check. See [Daily Journal](daily-journal.md#day-performance-journal-list).

Standalone Journal still uses `ITradingCalendarDayReader` for its own read-only Trade context and New York civil-date scope. The shared Calendar reader, 18:00 cutoff, stored Trades, Rule assessment records and review snapshots are retained. The Calendar dialog no longer launches Rule assessments. Retained assessments can be read from the portable export’s `journal.json` under `tables.tradingRuleAssessments`, with Rule revisions under `tables.tradingRuleRevisions`. Full backups retain those records. Weekly and Monthly Reviews still use retained assessment evidence, including assessment citations already saved in reports. This UI change requires no migration. See [Rules retention and portability](trading-rules.md#backups-and-exports) and [period review evidence](period-reviews.md).

## Account and currency filters

Account IDs scope both the grid and Journal reads. Inactive Accounts remain selectable. An unavailable Account stays selected with recovery guidance. Calendar keeps its selected date when filters change and discards obsolete read results. Each historical currency is shown separately. Previous, Next and Today navigate the month, and filter choices persist across navigation and re-entry.

## Refresh and accessibility

**Refresh** reloads the grid and Journal indicators and refreshes Journals for the selected date when one is selected. Committed Trade changes and imports refresh active Calendar. Journal commits refresh the day cards and indicators while preserving grid totals. The grid’s **Cancel** action cancels its refresh work. Calendar and Journal reads retain cancellation and generation checks. Dates remain keyboard accessible with Enter, Space and UI Automation Invoke. The help text describes the Journal action and preserves the date’s financial summary. The day dialog scrolls vertically while the month grid retains horizontal scrolling on narrow screens.

## Calendar day simplification verification (20 November 2026)

The Release build on `develop` at base `74e6f43680f82e0c7f62dffe07e5c0ed1d0cb86d` plus the changes below passed with zero warnings and zero errors. The focused Release run passed all **299** top-level cases. The isolated Calendar window and grid hosts passed **23** and **36** child cases respectively. The ViewModel test opens a date with Trades, opens a date without Trades, opens its saved Journal and moves to the next month. A recording reader verifies zero selected-day Trade requests. The compiled WPF checks exercise date selection by keyboard and mouse events, retained Journal actions and the absence of the old chart, buttons and table. Existing Journal editing, close guards, Account filtering and commit-driven grid updates run alongside them.

Eight compiled WPF renders were inspected in Light and Dark at **1,100 DIP / 96 DPI** and **480 DIP / 240 DPI**. Each combination shows a date with Trades and a saved Journal and a date with neither. The date and Journal actions align with the card, the controls fit, and the normal panel ends after its Journal content. Local evidence is under `TestResults/CalendarDaySimplification`, with the focused TRX under `focused-final` and the contact sheet under `renders-final`.

The first complete gate finished in **14m 12s** with **5,526 passed**, **2 failed** and **1 existing Rules quarantine skip**, exit **1**. It found a timestamp test that opened the deleted inline Trade XAML and a balance-notification test that reflected over the retired Calendar Trade commit event. The timestamp test now checks the retained Journal close-time description and Import formatter. The balance test retains Trades and Tradovate coverage. The first run is retained under `TestResults/release-20261120-123733-b75cc23005c74d318d0d727057be0a39`.

The final `pwsh -File scripts/Test-Release.ps1` gate exited **0** in **14m 26s** with **5,527 passed**, **0 failed** and **1 skipped**. Exclusive WPF passed **87** in 7m 08s. The parallel stage took 7m 17s and passed **602 Domain**, **1,052 Application**, **1,652 Infrastructure** and **2,134 Desktop** cases. The sole skip is the existing [Rules-view quarantine](rules-view-quarantine.md). The inventory is 85 cases smaller than the previous Instruments run because tests for the removed chart, Trade table, inline Trade editor and assessment actions were retired or replaced. The focused follow-up after updating stale references passed all **38** cases and the final Release rebuild reported zero warnings and errors. Full results are under `TestResults/release-20261120-125330-db2002a333774c34870621411bb9f29c`.

Remaining live checks use an isolated profile to open empty and populated dates with a physical mouse and keyboard, check focus on dismissal and inspect the same views on a Windows display at 250% scaling. Screen-reader speech and matching hosted CI remain pending. The native WPF test hosts exercise real window ownership and close paths.

<details>
<summary>Exact changed files</summary>

- `README.md`
- `docs/daily-journal.md`
- `docs/desktop-ui.md`
- `docs/trading-calendar.md`
- `docs/trading-rules.md`
- `src/PersonalTradingJournal.Desktop/App.xaml.cs`
- `src/PersonalTradingJournal.Desktop/Dialogs/WpfRuleAssessmentDialog.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Calendar/CalendarDayPerformance.cs` (removed)
- `src/PersonalTradingJournal.Desktop/ViewModels/Calendar/CalendarTradePresentation.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Calendar/CalendarViewModel.InlineJournal.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Calendar/CalendarViewModel.InlineTrade.cs` (removed)
- `src/PersonalTradingJournal.Desktop/ViewModels/Calendar/CalendarViewModel.Journal.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Calendar/CalendarViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/MainWindowViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/Calendar/CalendarDayDetailsView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Calendar/CalendarDayDialogWindow.Chrome.cs`
- `src/PersonalTradingJournal.Desktop/Views/Calendar/CalendarDayDialogWindow.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Calendar/CalendarDayDialogWindow.xaml.cs`
- `src/PersonalTradingJournal.Desktop/Views/Calendar/CalendarInlineTradeView.xaml` (removed)
- `src/PersonalTradingJournal.Desktop/Views/Calendar/CalendarInlineTradeView.xaml.cs` (removed)
- `src/PersonalTradingJournal.Desktop/Views/Calendar/CalendarView.xaml.cs`
- `src/PersonalTradingJournal.Desktop/Views/Calendar/DayPerformanceChart.cs` (removed)
- `src/PersonalTradingJournal.Desktop/Views/Journals/InlineJournalView.xaml`
- `tests/PersonalTradingJournal.Desktop.Tests/Calendar/CalendarDayDetailsTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Calendar/CalendarDayModalJournalTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Calendar/CalendarDayModalRunnerTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Calendar/CalendarDayModalTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Calendar/CalendarDayModalTimestampTests.cs` (removed)
- `tests/PersonalTradingJournal.Desktop.Tests/Calendar/CalendarDayModalWindowTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Calendar/CalendarDayPerformanceChartTests.cs` (removed)
- `tests/PersonalTradingJournal.Desktop.Tests/Calendar/CalendarFilterTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Calendar/CalendarInlineTradeTests.cs` (removed)
- `tests/PersonalTradingJournal.Desktop.Tests/Calendar/CalendarJournalTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Calendar/CalendarSqliteRefreshTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Calendar/CalendarSummaryTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Calendar/CalendarViewLayoutTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Import/TradovateImportAcceptanceTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Journals/CalendarDayJournalsTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Journals/CalendarInlineJournalSqliteTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Journals/InlineJournalViewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalCalendarSqliteTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalDraftCloseTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalEditorActionTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalFormAccountTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalTradeContextTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Navigation/AssessmentOwnerCloseTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Navigation/CalendarJournalNavigationTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Navigation/CalendarOwnerCloseTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Navigation/JournalTradeRefreshTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Navigation/MainWindowViewModelTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/ManualAssessmentEntryTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/ReleaseSchedulingTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Trades/TradingTimePresentationTests.cs`

</details>

## Historical acceptance (M13.7 and M14.5)

Previous Save-completion verification: **648 focused** and **2,917 full parallel Release** tests passed, zero failures/skips. Both hosts save exact fields as Completed, accept meaningful freeform-only/partial answers, retain failed/conflicting edits, and previously preserved saved Drafts/history on Cancel without saving; this Cancel behavior is superseded by Draft saving above. Calendar stays on its selected day/account while standalone re-entry resets to today/All accounts. Automated normal/narrow-high-DPI Light/Dark renders were inspected. Live save/cancel interaction and matching GitHub Actions remain unverified. See the [current evidence and isolated Windows checklist](daily-journal.md#previous-save-completion-verification-2026-10-06); earlier Draft-save and Complete-button checks are historical, not the current workflow.

See [M13 acceptance](m13-acceptance.md) for the evidence matrix, test/build counts, measured widths, isolated Windows checklist and remaining live checks. Automated native-WPF interaction tests and generated Light/Dark/240-DPI renders are not live pointer, monitor-scaling or screen-reader acceptance. User-confirmed Calendar hover, modal controls/backdrop dismissal and explicit UTC-4 display are recorded separately, without extending those observations to every Calendar interaction. No real journal or customer data is needed for acceptance.

M14.5 Journal launch/marker evidence and the remaining isolated Windows checks are in [Daily Journal verification](daily-journal.md#m145-automated-verification-and-manual-follow-up). Its initial 509 focused cases passed, including protected close veto, exact scope, status-only SQLite queries, empty/adjacent dates and stale batches. The full-suite follow-up passes 280 constrained focused cases and all 2,810 tests in the normal parallel Release run, including 36 grid and 85 native Calendar cases, after correcting test-host renderer lifetime. Case/process deadlines and Calendar behavior are unchanged; see the [investigation](ci-wpf-tests.md#m145-local-full-suite-investigation-2026-10-05). Normal/narrow Light/Dark renders were inspected earlier; live Journal launch and a new GitHub CI run remain unverified.
