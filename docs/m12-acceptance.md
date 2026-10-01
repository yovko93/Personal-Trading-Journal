# M12 Dashboard acceptance

Reviewed on 2026-10-01 on `develop` at `2c0f68e2fcd8c39b75941e370657db3cf1cb64aa`. This review used synthetic fixtures and isolated migrated SQLite databases; it did not open the real journal. No Dashboard calculation or interaction defect was confirmed. Documentation was corrected where it still described the implemented Cumulative Realized P&L panel as a future Equity Curve.

**Decision:** M12's automated acceptance checks pass. Full interactive acceptance remains **open**: the Windows computer-use helper failed before window discovery, so automated ViewModel, SQLite and off-screen WPF tests are not presented as observed Dashboard interaction. The user separately confirmed that pointer-follow tooltip placement works in both charts; that confirmation does not cover the other visual scenarios.

Verification: 298 focused Dashboard/shell tests passed (84 Application, 19 Infrastructure, 195 Desktop). The complete Release suite passed **2,304 tests** (400 Domain, 504 Application, 711 Infrastructure, 689 Desktop), with zero failed or skipped. `dotnet build PersonalTradingJournal.sln --configuration Release` succeeded with zero warnings and errors; `git diff --check` passed. These are automated checks, not live Dashboard acceptance.

| Scenario | Automated result and evidence | Interactive result |
| --- | --- | --- |
| All/specific Accounts, inactive history, unavailable selection | **Passed** — `DashboardFilterTests`, `DashboardSqliteTests`: explicit account scoping, no fallback to another account, account-scoped Recent Trades. | Unverified |
| All/Week/Month/Year/Custom, navigation, New York DST, empty periods | **Passed** — `DashboardViewModelTests`, `DashboardFilterTests`, `DashboardSqliteTests`, `DashboardRefinementTests`: inclusive DST bounds, shortcuts, draft/Apply/Cancel, stale-read rejection and zero/N/A empty cards. | Unverified |
| Currency and verified/estimated Net | **Passed** — `DashboardMetricCalculatorTests`, `EstimatedNetPnLTests`, `DashboardAnalyticsReaderTests`, `DashboardSqliteTests`: historical-currency partitions, strict Net coverage, EffectiveNet provenance and cost correction. | Unverified for Dashboard presentation |
| Outcome cards, Best/Worst, Total Trades and Setup breakdowns | **Passed** — Application metric/performance tests and Desktop ViewModel/SQLite tests cover formulas, break-evens, ties, unknowns, inactive/missing Setups and closed-only counts. | Unverified |
| Daily and cumulative series, axes, colors, gaps, crossings, wheel/keyboard/help/tooltips | **Passed** — `DailyPnlChartTests`, `PnlChartTests`, `ChartTooltipFollowerTests`, `DashboardXamlTests`: off-screen Light/Dark renders, exact tooltip content, focus targets, scrolling and popup geometry. | Pointer-follow **passed — user-reported manual check**; all other chart interactions unverified |
| Latest ten Recent Trades and View target | **Passed** — `DashboardSqliteTests` exercises production paging and open Trades independently of date/currency; `DashboardViewModelTests` and `MainWindowViewModelTests` check exact-row navigation. | Unverified |
| Refresh after Trade writes/imports and stale-response protection | **Passed** — isolated SQLite insert/edit/delete/cost-correction tests, import/replay reader tests and `MainWindowViewModelTests` delayed-commit cases. NoChanges/Blocked do not signal a commit refresh. | Unverified |
| Light/Dark, narrow and high-DPI layout | **Passed for automated renders only** — compiled WPF calendar/card/chart resources render at 96/240 DPI and narrow widths; layout and accessibility assertions pass. | Unverified at the Windows desktop |

## Remaining Windows checks

Use a fresh `--isolated-data-root` with synthetic Accounts, Instruments and Trades; never launch the ordinary journal for acceptance. In both Light and Dark at normal and narrow/high-DPI sizes:

1. Switch All, an active Account, an inactive Account, and an unavailable/deleted selection. Verify cards, charts, Setups and Recent Trades scope correctly without fallback.
2. Exercise All, Week/Month/Year with Previous/Next, an empty period, and a cross-month Custom range. Check New York DST edge dates, draft/Cancel/Apply, keyboard calendars and zero/N/A empty cards.
3. Compare cards, ring counts, Setup groups and both plotted series with known synthetic Gross/Net/estimated values in separate currencies. Check colors, axes, gaps, zero crossings, help and keyboard tooltips, plus vertical and horizontal wheel behavior. Pointer-follow placement alone has already been user-verified.
4. Confirm Recent Trades stays latest-ten and account-scoped while date/currency changes; use View on an open Trade and a row outside the current Trades browse page.
5. Edit/delete/close a synthetic Trade and commit synthetic Tradovate/TopstepX imports while Dashboard is active or revisited. Verify refresh and that an earlier delayed read cannot restore stale values.

The interactive attempt in this review failed at computer-use initialization, including one reset/retry: `failed to write kernel assets: The system cannot find the path specified. (os error 3)`. No Dashboard window was captured or controlled. This is a verification limitation, not evidence of an application failure.
