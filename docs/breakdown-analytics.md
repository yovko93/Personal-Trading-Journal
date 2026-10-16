# Breakdown Analytics — M20

## Final UI consistency corrections — 17 October 2026

Starting state: **clean `develop`, HEAD `59a82e3e48a05d02a0fdbfa07f2bc379b3f221c6`**. Existing M20.11 work was preserved except for the explicitly superseded header decision. Only four presentation corrections were made:

1. **Shared heading:** removed the Breakdown-only shell overrides and subtitle. The single `PageTitle` now uses the very same `PageTitleTreatment`, `PtjShellPageTitleTextStyle`, centered teal surface and underline as Dashboard. Other destinations are unchanged; Breakdown's compact body spacing remains.
2. **Local action colors:** strong teal Apply/selected chip, blue-tinted Refresh/More filters, restrained neutral Reset, tinted unselected chips and consistent teal View trades in both locations. Local styles reuse the shared opaque Journal/Playbook action templates and existing theme resources, including hover/pressed/disabled colors and focus outline. No global button styles or command/enablement rules changed. The generic opacity-based pressed treatment would reduce the blue action's Light-theme text contrast to approximately **3.48:1**; reusing the opaque template avoids fading its text. The exercised blue action states meet **4.5:1** contrast in both themes.
3. **Unapplied information box:** the same `HasUnappliedChanges` condition controls an information icon, compact blue-tinted bordered box, **Unapplied filters** and **Select Apply to update the results.** Text wraps. A visible transition supplies one polite announcement; subsequent edits while already visible do not repeat it or steal focus. Validation, loading, cancellation, failures and the last applied scope remain independent and visible.
4. **Aligned table:** actual text starts and action-button edges align with their headers in all eight columns. Cell containers and text alignment are explicit; spacing accounts for cell focus borders. Raw sorting, formatting, financial colors, coverage labels, stable selection, virtualization, horizontal scrolling and exact-group navigation are unchanged.

### Verification and renders

- **97/97 relevant Desktop tests passed**, zero failed/skipped, exit 0, **1m 5s**: `artifacts/m20-consistency/complete-focused/breakdown.trx`. The filter covers all Breakdown tests plus shared-header and Breakdown navigation/return tests. Existing deadlines/assertions remain intact. New assertions compare rendered glyph/control coordinates (including negative, zero and unavailable rows), not brush names or alignment setters; they also check draft-only announcements, focus retention, applied-result identity/read counts, shared Dashboard header reuse, and real routed-key press/release with distinct rendered button states.
- **Release solution build: 0 warnings/errors, 7.04 seconds**. EF's design-time in-memory model check reports **no pending changes**. `git diff --check` and **153** local README/Breakdown documentation links pass. The full Release gate was **not rerun for this four-item presentation-only follow-up**; the earlier 4,562-pass M20.11 result below remains historical evidence, not acceptance of these new uncommitted changes.
- Automated compiled-WPF renders inspected in **Light and Dark**, 1586×992 and 1280×800 DIP / 96 DPI, and narrow 1000×992 DIP / **240 DPI**. Applied/unapplied, retained failure, empty, unavailable, multiple positive/negative groups, horizontally scrolled table and button normal/pressed/focused/disabled samples are covered. Separate 700-DIP / 240-DPI table checks retain access to the action and coverage columns. Header-only Dashboard renders confirm the shared presentation; they do not substitute Breakdown content for Dashboard evidence.
- Test development caught an incorrect TextBlock character-coordinate API and a sub-control capture that included parent offsets. These test-only helpers were corrected, including explicit visual capture bounds; the final focused run passes. No product behavior was changed to accommodate them.

Render directory: `tests/PersonalTradingJournal.Desktop.Tests/bin/Release/net10.0-windows/breakdown-consistency-renders/` (50 PNGs, ignored build artifacts). Useful samples: `Light-1586-96-four-groups.png`, `Dark-1586-96-unapplied.png`, `Light-1000-240-unapplied.png`, `Dark-1000-240-four-groups-bottom.png`, `Light-1586-96-dashboard-header.png`, and `Light-action-pressed.png`. Table negative/unavailable/scroll samples remain in the sibling `breakdown-table-renders/` directory.

No clipping or overlap was observed in the inspected final samples. At narrow widths the table intentionally scrolls horizontally and chart/details stack as before. These are **automated renders, WPF routed input and automation peers**, not actual human mouse/keyboard or screen-reader testing. Live hover/focus perception, Narrator/NVDA announcements and matching CI remain unverified; no claim of full M20 acceptance is made. No provider call, production-journal access, branch switch, commit or push occurred.

### Exact follow-up files

1. `README.md`
2. `docs/breakdown-analytics.md`
3. `src/PersonalTradingJournal.Desktop/MainWindow.xaml`
4. `src/PersonalTradingJournal.Desktop/Views/Breakdown/BreakdownView.xaml`
5. `tests/PersonalTradingJournal.Desktop.Tests/Breakdown/BreakdownAccessibilityViewTests.cs`
6. `tests/PersonalTradingJournal.Desktop.Tests/Breakdown/BreakdownTableViewTests.cs`
7. `tests/PersonalTradingJournal.Desktop.Tests/Navigation/MainWindowBreakdownLayoutTests.cs`
8. `tests/PersonalTradingJournal.Desktop.Tests/Navigation/MainWindowHeaderTests.cs`

## M20.11 — visual alignment

Starting state: **clean `develop`, HEAD `59c8a7c1c36b80396b8c9698f5d3bc7412000a56`**. The reference is the attachment named **Visual target.png**; **Current implementation.png** is the prior UI. The numbered descriptions in the pasted request were reversed. No existing work was discarded.

This deliberately supersedes M20.6's **single compact-summary card**. The four new equal-weight KPI cards format the existing supplied summary; no calculation, query, schema, cohort membership, navigation guard or persistence contract changes.

### Layout and retained behavior

- Initial M20.11 header decision (**superseded by the final consistency corrections above**): a Breakdown-only left-aligned “Breakdown Analytics” heading and one subtitle. The other M20.11 layout decisions below remain in place.
- Period, Account, Instrument, Apply, Refresh, Reset and More filters share a compact toolbar. Currency/calculation mode start collapsed; invalid currency reveals the field before the existing guarded focus request. Custom date inputs, labels, draft indication and explicit Apply semantics remain. Normal “Breakdown loaded” / “Filters match” prose is hidden; errors, cancellation, stale/retained results and loading controls remain visible.
- Four approximately 88-DIP cards show source-labeled P&L, closed Trades, Win Rate and Profit Factor. **Net, Net/Gross, Gross fallback, Gross and unavailable remain distinct**. Empty/incomplete financial populations have an explicit explanation; no missing value becomes zero.
- Seven grouping chips edit the existing draft state, not the displayed result. Instrument is still the default; Strategy remains deferred. Chart metric changes remain local. The concise chart heading uses the **applied** grouping and source basis.
- Chart and selected-group cards use approximately **two-thirds / one-third** width and align vertically; narrow views stack. Teal/red bars retain the existing signed scale, zero baseline, unavailable values, virtualization and selection. Full scale/coverage text remains in help/tooltips; concise coverage is visible in selected details and the table/footer. Secondary definitions/outcomes/winner/loss averages are available through Metric details.
- The table retains all eight columns, raw numeric/chronological sorting, stable group selection, exact View trades command parameters, virtualization and horizontal scrolling. Its row action is now at the right. No synthetic group or automatic selection is added to production.

### Reference-based automated visual evidence

The new compiled-shell fixture uses real `MainWindow.xaml` and `BreakdownView.xaml` with fake readers and the real calculator. A 120-Trade / four-Setup synthetic population reproduces the reference's broad composition (FVG, Liquidity sweep, CRT, Unclassified), with descending P&L chosen **in the fixture** and FVG selected. It also renders one MNQ group with mixed actual-Net/Gross fallback, empty, unavailable and retained-error states. All are synthetic; nothing reads the installed journal.

Reference comparison uses a **1586×992-DIP client render at 96 DPI**, with about 1,270 DIP of page content, close to the target's approximately 1,265-pixel content region. The screenshot's monitor DPI is unknown: this is an explicit logical-viewport comparison, not a claim that the different-sized supplied screenshots are equivalent raw pixels. Additional samples use **1280×800 DIP / 96 DPI** and **1000×992 DIP / 240 DPI** (2500×2480 pixels), both themes. Narrow cards reflow 2×2 and chart/details stack; page/table scrolling reaches the remainder.

Initial render inspection led to a left-alignment correction for the detail amount, stronger section/title hierarchy, and narrower responsive breakpoints so the ordinary 1280-DIP window retains a chart/details row. The target hierarchy is preserved while intentionally retaining explicit currency suffixes, Apply/Refresh/More filters, financial-source coverage, the theme toggle, and accessible selected/focused outlines. No Demo data badge, Strategy chip, fabricated values or altered sort default was added. The app's Dark palette/sidebar are retained rather than copied from the Light mockup.

Local images: `tests/PersonalTradingJournal.Desktop.Tests/bin/Release/net10.0-windows/breakdown-alignment-renders/`; filenames include theme, client width, DPI and state. These are **automated off-screen renders**, not live mouse/keyboard or screen-reader observations.

### Verification and acceptance

Final focused run: **97/97 passed, zero failures/skips**, 53 seconds, `artifacts/m2011/focused-final-aligned/breakdown.trx`. This includes six new compiled-shell composition cases, existing Breakdown presentation/state/accessibility/drill-down tests and shared-header/navigation checks. The tests verify collapsed/expanded filters, invalid-field focus, draft-versus-applied heading, unchanged read counts on chip selection, KPI dimensions/reflow, chart/detail proportions, table/action placement, narrow scrolling, selected-source commands and restoration of other destinations' centered headings.

Iteration checks caught an object-valued WPF button format that displayed only a count, an omitted visible empty-result explanation, and unused table width after virtualized source replacement. These presentation issues were corrected before the final focused run; changed selectors/assertions follow the requested new visible/accessible placement, not weakened financial/cohort behavior. An initial new-test compile also needed its existing STA-helper namespace import. Final solution Release build: **0 warnings/errors**, 16.15 seconds. EF design-time in-memory check: **no pending model changes**. `git diff --check`, new-file whitespace and **152 local documentation links** pass.

The **single final complete Release gate passed: 4,562 unique tests, zero failures/skips, exit 0**. Command: `pwsh -File scripts/Test-Release.ps1 -ResultsDirectory artifacts/m2011/gate`. Evidence root: `artifacts/m2011/gate/release-20261016-215028-b2bbf02dd83c4aec83b32d254d024f98/`, with top-level TRX, `summary.json`, VSTest diagnostics and supervised child logs. Focused/child overlap is not added to this total.

| Project / partition | Passed | Failed / skipped | Reported duration |
| --- | ---: | --- | --- |
| Desktop exclusive WPF | 88 | 0 / 0 | 1m 31s |
| Desktop parallel | 1,626 | 0 / 0 | 5m 17s |
| Domain | 490 | 0 / 0 | 3s |
| Application | 976 | 0 / 0 | 6s |
| Infrastructure | 1,382 | 0 / 0 | 3m 26s |

Calendar native child: **78.959 / 120 seconds**, **41.041 seconds headroom**. Journal editor child: **11.797 seconds**, its STA finished in **8.086 seconds** against the unchanged 30-second deadline. No Calendar focus/deadline or Journal failure occurred. These are observed local timings, not a guarantee for another runner. No deadlines, test scheduling or financial/cohort assertions changed.

| Acceptance area | M20.11 result |
| --- | --- |
| Visual implementation | **Complete**, reference hierarchy inspected and iterated; intentional differences above |
| Local automated | **Passed**, focused + final complete gate/build/EF/whitespace/links |
| Automated visual | **Passed for inspected samples**, Light/Dark normal and narrow/240 DPI; not live input |
| Calculations / queries / cohort behavior | **Unchanged**, existing full-gate regressions passed |
| Performance | Existing M20.10 reader/cohort measurements retained; no new performance or frame-time claim |
| Live keyboard / screen reader | **Pending**, no actual interaction or Narrator/NVDA acceptance added |
| Matching CI | **Pending**, uncommitted work is not represented by a tested commit |

Actual keyboard, Narrator/NVDA and matching CI remain **pending**. The prior supported helper failed initialization; no live interaction is claimed from these renders or WPF peers. Use the existing manual checklist with More filters/grouping chips included. These changes are uncommitted, so no workflow run can cover this final worktree; the workflow remains `CI / validate`, triggered by a `main` push or PR targeting `main`. No publication or provider request is made.

### Exact changed files

1. `README.md`
2. `docs/breakdown-analytics.md`
3. `src/PersonalTradingJournal.Desktop/MainWindow.xaml`
4. `src/PersonalTradingJournal.Desktop/Resources/Themes/LightTheme.xaml`
5. `src/PersonalTradingJournal.Desktop/Resources/Themes/DarkTheme.xaml`
6. `src/PersonalTradingJournal.Desktop/ViewModels/Breakdown/BreakdownViewModel.cs`
7. `src/PersonalTradingJournal.Desktop/ViewModels/Breakdown/BreakdownPresentation.cs`
8. `src/PersonalTradingJournal.Desktop/ViewModels/Breakdown/BreakdownChartPresentation.cs`
9. `src/PersonalTradingJournal.Desktop/Views/Breakdown/BreakdownView.xaml`
10. `src/PersonalTradingJournal.Desktop/Views/Breakdown/BreakdownView.xaml.cs`
11. `tests/PersonalTradingJournal.Desktop.Tests/Breakdown/BreakdownViewTests.cs`
12. `tests/PersonalTradingJournal.Desktop.Tests/Breakdown/BreakdownChartViewTests.cs`
13. `tests/PersonalTradingJournal.Desktop.Tests/Breakdown/BreakdownDrillDownViewTests.cs`
14. `tests/PersonalTradingJournal.Desktop.Tests/Breakdown/BreakdownAccessibilityViewTests.cs`
15. `tests/PersonalTradingJournal.Desktop.Tests/Navigation/MainWindowHeaderTests.cs`
16. `tests/PersonalTradingJournal.Desktop.Tests/Navigation/MainWindowBreakdownLayoutTests.cs` (new)

## Remaining acceptance check — 17 October 2026

Actual starting state was **clean `develop` at `7ade629049a517435df10437137a3b0ad098cfb8`**. The five M20.10 files previously reported as uncommitted are now committed and published at that SHA; GitHub's `develop` branch reports the same head. Compared with `ebea74a`, this commit adds only the three acceptance documents and two measurement-tool files. Application code, tests, Release runner and CI configuration are unchanged. The recorded **4,556 passed / 0 failed / 0 skipped, exit 0** local gate and its retained `summary.json` were reused; no repeat build, EF check, render run or full test gate was justified by this documentation-only follow-up.

### Live evidence and manual checklist

The supported **computer-use** skill was retried. Initialization again failed with **“failed to write kernel assets: The system cannot find the path specified. (os error 3)”**. Reset succeeded but initialization failed identically afterward. No app was launched, no window captured, and no input or clipboard operation was performed in this follow-up. This is a tooling blocker, not a demonstrated Breakdown defect. Actual keyboard interaction, live UI automation and Narrator/NVDA acceptance remain separate and unverified; existing routed-input/automation-peer tests and renders cannot close them.

Use only an app explicitly launched with `--isolated-data-root` pointing at synthetic data (the measurement tool's `--live-fixture` mode creates a fresh suitable fixture). Never launch the normal journal for these checks. Record **pass / fail / not exercised**, theme/scaling, observed focus and any announcement for each item:

1. **Filters:** Tab/Shift+Tab through labels/inputs and actions; operate dropdowns with arrows/Enter. Submit an empty currency or reversed dates: the relevant input receives focus, the error is readable and the old result retains its original scope. Correct it, Apply, then edit drafts and Refresh (must not apply drafts); Reset restores defaults.
2. **Chart/table:** change chart metric, sort numeric and chronological columns with the keyboard, and select groups through both chart and table. Selection and details agree; unavailable values do not become zero; focus stays visible and scrolling reaches virtualized rows.
3. **Cohort:** View trades, page, Refresh, open/close a Trade detail. Back to Breakdown refreshes the last applied query and preserves draft filters; Exit Breakdown scope instead restores ordinary Trades state. Confirm each action's focus destination.
4. **Recovery:** cancel a load while it is active and exercise a safely controlled synthetic read failure. Retained results keep their original scope, retry is usable and delayed completion does not steal newer focus. If the fixture loads too quickly or no safe error scenario is available, leave that case pending rather than reporting a pass.
5. **Deletion:** delete only a disposable synthetic cohort Trade through the normal confirmation. The original is not replaced by a newly matching Trade; focus returns to a remaining row or Refresh after reload, without overriding intervening keyboard input.
6. **Screen reader, separately:** with Narrator or NVDA actually running, verify input names, group/value/coverage descriptions, selected and sorted states, action names, and meaningful validation/loading/error/cancel announcements. Confirm reading order and no excessive repeated announcements. Repeat keyboard reachability in Light/Dark and narrow/250% scaling; record the actual reader and display settings.

### Read-only CI provenance

GitHub confirms [published commit `7ade629`](https://github.com/yovko93/Personal-Trading-Journal/commit/7ade629049a517435df10437137a3b0ad098cfb8). The direct Actions API query for that **exact `head_sha`, without an event filter**, returned **`total_count: 0`**. The commit's associated-PR endpoint returned **`[]`**. This complements the connector's empty PR-event-only result and avoids mistaking that narrower lookup for all events. There is no matching workflow run, tested merge SHA, job conclusion or CI failure to investigate. No older green run was substituted.

The configured workflow is **CI**, with one **`validate`** job on `windows-latest`: Restore, Release Build, the complete `scripts/Test-Release.ps1 -ResultsDirectory TestResults` gate (10-minute Test action), and always-upload diagnostics. Its triggers are `push` to `main` and pull requests targeting `main`; publishing only to `develop` does not trigger it. Remaining CI work is a user-created/updated PR targeting `main` containing this source, followed by inspection of its exact checkout/merge SHA and `validate` conclusion. This task did not create a PR, publish, trigger or rerun anything. The two documentation updates from this follow-up are themselves uncommitted and are not covered by any CI run.

| Acceptance area | Current status |
| --- | --- |
| Implementation | **Complete**; no new concrete defect |
| Local automated acceptance | **Passed / reused**, 4,556 tests, exit 0; previous zero-warning builds and EF result retained |
| Automated visual inspection | **Passed for previously inspected samples / reused**, Light/Dark normal and narrow/240 DPI |
| Performance | **Measured / reused**, 1k/10k/50k; linear memory, per-page cohort encoding and two-group presentation limits remain |
| Actual live keyboard | **Pending**, tool initialization blocked; checklist results not yet supplied |
| Live UI automation | **Blocked**, no new input/capture; existing compiled-WPF evidence only |
| Screen reader | **Pending**, no Narrator/NVDA session observed or reported |
| Matching CI | **Pending**, published head verified but no matching run or associated PR |

**Implementation complete; M20 acceptance pending.** Only `README.md` and `docs/breakdown-analytics.md` changed in this follow-up. Documentation links and `git diff --check` were checked; no code, assertions, deadlines, data or provider behavior changed. No production-data access, provider call, commit, push, branch switch or next milestone.

## M20.10 — reconciliation and acceptance review

Reviewed **16 October 2026**, clean `develop` at **`ebea74a860b85dd7b0d52e65c2a51d2b287151bf`**, not the reported `92864ad` baseline. History isolates all nine M20.1–M20.9 commits after `4864305`: **55 files, 5,727 insertions and 32 deletions**. The review includes the final Application contracts/calculator, SQLite readers and DI, Desktop state/navigation/presentation/focus, their regression tests and documentation—not just M20.9. There were no pre-existing tracked or untracked changes to preserve. No production journal, credential, provider, Git write or next milestone was used.

**Review finding:** no actionable implementation defect was demonstrated in this scope. No speculative product, financial, persistence, test-harness or scheduling change was made. Existing regression coverage already exercises the required boundaries; the added opt-in measurement program closes the missing large-history measurement boundary and checks reconciliation at each size. Live keyboard/assistive acceptance and matching CI remain open; implementation completion is not full M20 acceptance.

### Final behavior (supersedes historical proposals below)

- Seven partitions: Instrument (default), current Trading Setup, Direction, New York entry weekday/hour, Account and UTC elapsed holding duration. Strategy remains deferred. Null Setup means Unclassified; a retained missing or inactive ID is not Unclassified. Names never determine identity.
- Membership is fully closed Trades in the selected **New York closure-date** range and one historical pricing currency. Entry-time groups do not change membership; duration uses UTC even across DST. Six duration buckets end in four hours or more.
- **Net preferred** (default) uses each known actual Net, including zero; otherwise supplied Gross. Explicit Gross uses Gross independently. Net, Net/Gross, Gross fallback, Gross and unavailable sources stay distinguishable. Gross fallback is neither actual nor estimated Net. Missing values retain their coverage denominator; incomplete totals/ratios are unavailable. Shared numeric accumulators operate on the complete eligible population, not table pages or averages of group ratios.
- One no-tracking SQLite statement snapshots financial projections and labels; no execution/screenshot collection join can multiply Trades. Its context closes before calculation. Desktop captures requests, keeps draft and successful-result scopes separate, ignores stale completion, and does not read SQLite for local chart metric, sorting or selection changes.
- Drill-down freezes **original contributing IDs** and shows **current records**. Edited originals remain; new matches stay excluded; deleted records are not replaced. Empty membership means no Trades. Count/page/position data share a read transaction with one JSON ID parameter. Back refreshes the last successful Breakdown query without applying draft edits; Exit restores ordinary Trades state.
- View-owned focus callbacks reject replacement/unload/new input. View subscriptions detach on unload; scoped reads and detail callbacks have navigation ownership. Live announcements are opt-in status/error changes, not a guarantee about any screen reader.

The initial strict-Net-only policy in M20.1/M20.2 is historical and superseded by M20.3. The initial live-predicate drill-down proposal is superseded by M20.8's frozen-ID/current-record policy. Earlier milestone “next/unverified” statements below describe their checkpoints, not current implementation gaps.

### Reconciliation and integrated acceptance matrix

| Scenario | Automated evidence reused in this gate | Live boundary |
| --- | --- | --- |
| Seven dimensions × Gross/Net preferred; disjoint counts/IDs, additive P&L, source coverage, outcomes and denominators | `BreakdownMetricCalculatorTests.EveryDimensionHasExactDisjointMembershipReconciliationAndInputIndependentOrder`; SQLite reader's 14-case matrix with multiple executions/screenshots | No production-data comparison |
| Zero Net, mixed fallback, both values absent, unequal groups, empty/one-sided/overflow | Calculator, input and presentation tests; strict-Net comparison only when all actual Net is known | No forced equality to differently defined Dashboard estimates |
| Historical currency, inactive/missing/duplicate labels, Unclassified | Query/key/selection and SQLite reader tests; stable identity independent of text | Live screen-reader naming pending |
| Overnight, NY midnight, skipped/repeated DST hours, duration ticks | Query/key/duration/calculator and SQLite boundary tests | Not a futures-session inference |
| Snapshot under concurrent write; cancellation/error disposal | Reader interceptors coordinate WAL writes before materialization, assert old consistent result then fresh result and disposed contexts | No long-running transaction spans UI navigation |
| Apply/Refresh/Reset/Cancel/retry; retained failures, draft edits and stale completion | `BreakdownViewModelTests`, compiled filters and shell navigation tests | Actual keyboard loop blocked by helper initialization |
| Local numeric/chronological sort, chart metric/selection/details consistency | Presentation/chart/ViewModel and bound-WPF tests count reads and preserve keys | Physical monitor/assistive checks pending |
| Exact scoped paging, current edit/delete/missing/empty, Back and Exit | `TradeListCohortTests` (methods in partial `TradeListPagingTests`), `TradesViewModelBreakdownTests`, `MainWindowBreakdownTests`, bound drill-down/focus tests | Real mouse/keyboard edit/delete loop not performed |
| Delayed focus and newer user input | `BreakdownAccessibilityViewTests` uses real synthetic HWND, automation peers and routed input | Automated input is not human keyboard or Narrator evidence |

### Large-history measurements

Reproduce with `dotnet run --project scripts/BreakdownMeasurements -c Release`. This small console harness is **not another test project or a benchmark framework**. It accepts no existing database path, creates fresh unique temporary databases, applies all migrations, batches Domain-derived Trade/execution/browse records in groups of 500, awaits every operation and deletes its owned root. `--live-fixture` instead retains a newly created 1k fixture for the existing `--isolated-data-root` Desktop switch; it cannot select the installed journal.

Environment: Windows 10 build 19045, Intel i7-7700HQ **4 cores / 8 logical CPUs**, **17,031,376,896 bytes RAM** (about 15.86 GiB), .NET **10.0.12**, SDK **10.0.302**, Release, warnings as errors. No complete test gate was competing with the measurements. These are local synthetic observations, not a production SLA, cold-disk benchmark or UI frame-time measurement.

Each size is a separate migrated database: ten Accounts, twenty Instruments, eight Setups (one inactive), approximately one seventh unclassified, two executions per closed Trade, varied long/short, 280 closure dates and 1–1,500-minute durations. Historical currency is 80% USD / 20% EUR. The USD query uses Direction to obtain two large cohorts; 75% uses actual Net, 25% Gross fallback, including known-zero Net cases. One warm-up is excluded from the three measured samples (the initial population/cohort lookup is also untimed). Presentation measures actual formatting, numeric sorting, accessible descriptions and chart preparation for **two groups**, not a WPF window/render or high-cardinality table.

| Stored closed Trades (USD eligible / cohort) | Fixture including migrations | Reader + calculator ms: median [min–max] | Presentation ms: median [min–max] | First 20-row cohort page ms | Last 20-row cohort page ms |
| --- | --- | --- | --- | --- | --- |
| 1,000 (800 / 400) | 4.846 s | 26.564 [22.767–29.395] | 0.067 [0.057–0.123] | 13.858 [11.997–17.721] | page 20: 12.819 [12.226–13.933] |
| 10,000 (8,000 / 4,000) | 7.720 s | 175.541 [131.363–195.325] | 0.068 [0.056–0.403] | 57.491 [51.648–65.337] | page 200: 70.771 [61.833–88.922] |
| 50,000 (40,000 / 20,000) | 38.982 s | 827.883 [776.608–851.410] | 0.089 [0.071–0.091] | 292.904 [260.453–330.499] | page 1,000: 490.451 [461.078–571.953] |

Each sample checks group count/P&L reconciliation, unique contributing membership and first/last cohort count/page presence. Measured reader allocation ranges (`GC.GetTotalAllocatedBytes`, process-wide) are **1.396–1.401 MB**, **12.641–12.652 MB**, **65.640–65.648 MB** respectively (decimal MB). Per-page allocations grow from about **0.377 MB** to **1.64 MB** to **7.22 MB**, because complete ID membership must be encoded on each request. Sampled working set ranges are **117.25–118.03 MB**, **135.37–137.08 MB**, **126.34–169.16 MB**; managed heap fluctuates with collection and is not a retained-memory/peak claim. Formatting allocations are about 3.5–3.9 KB for two groups.

Remaining performance limits: full selected facts/IDs use **linear memory**; large ID JSON and SQLite count/order/offset work repeat per scoped page. The final page is measurably slower at 50k. VM work is dispatched off the UI thread, but publication and group presentation remain UI work; these measurements do **not** establish worst-case UI latency for thousands of groups or slow storage. No unsupported performance fix or new index was introduced.

### Verification, visual/live and CI status

Final command: `pwsh -File scripts/Test-Release.ps1 -ResultsDirectory artifacts/m2010/gate`, normal Windows token for synthetic DPAPI/filesystem/native WPF fixtures. **Exit 0; 4,556 unique tests passed, zero failed/skipped.** Four actual test projects discovered; the process probe is a test helper, not an omitted test project. Existing exclusive-WPF then parallel scheduling and every assertion/deadline remain unchanged. No successful gate was repeated, and overlapping focused/child cases were not added to the aggregate.

| Project / partition | Passed | Failed | Skipped | Reported test duration |
| --- | --- | --- | --- | --- |
| Desktop exclusive WPF | 88 | 0 | 0 | 1m 28s |
| Desktop parallel | 1,620 | 0 | 0 | 4m 57s |
| Application | 976 | 0 | 0 | 6s |
| Domain | 490 | 0 | 0 | 4s |
| Infrastructure | 1,382 | 0 | 0 | 3m 47s |
| Unique total | **4,556** | **0** | **0** | gate wall time about 6m 43s |

Evidence root (ignored): `artifacts/m2010/gate/release-20261016-201358-83658b085fb4404cbba8ac0bc6a9262d/`, containing `summary.json`, top-level per-project TRX, test output, VSTest diagnostics and supervised child logs. Both stage runners exited normally. Calendar native child: **76.045 s / 120 s** (43.955 s headroom); Journal editor child **11.413 s**, its STA **8.220 s / 30 s**. No Calendar/Journal deadline failure occurred. These observations do not guarantee timing on another runner. Release solution build: **0 warnings/errors**, 14.34 s. Final measurement-project build: **0 warnings/errors**, 3.04 s. EF `has-pending-model-changes` using the design-time in-memory factory: **no changes**. `git diff --check`, new-file trailing whitespace and **162 local documentation file links** passed. Initial measurement restore was blocked only by sandbox access to NuGet's vulnerability feed (NU1900); normal-permission restore succeeded without disabling audit/warnings. That restore failure was not a test failure.

Automated renders were regenerated by this complete gate under `tests/PersonalTradingJournal.Desktop.Tests/bin/Release/net10.0-windows/breakdown-{chart,table,drilldown,accessibility}-renders/`. Inspected chart selection in **both themes at 1280×900 DIP / 96 DPI (1280×900 pixels)** and **700×900 DIP / 240 DPI (1750×2250 pixels)**, plus Dark narrow selected details, Light normal retained error, Dark narrow validation, Light populated cohort and Dark missing cohort. Positive/negative/zero/unavailable and mixed Net/Gross labels remain distinguishable; long text wraps, narrow details stack, Back/Exit are visible and table columns remain reachable via their explicit horizontal scrollbar. Fixture chart-only View buttons are deliberately disabled without navigation callbacks; the separate bound drill-down fixture verifies enabled actions. No new visual defect was observed in these samples. Off-screen render DPI is not an actual monitor-scaling check.

| Acceptance category | Final status |
| --- | --- |
| M20 implementation review | **Complete**, no demonstrated product defect requiring a fix |
| Local automated acceptance | **Passed**, full gate/build/EF/whitespace/links above; existing reconciliation and integrated regressions reused |
| Automated visual acceptance | **Passed for inspected samples**, both themes and declared sizes; not every live monitor/font combination |
| Performance measurement | **Complete for 1k/10k/50k synthetic histories**, with linear-memory/paging and two-group presentation limitations above |
| Live keyboard/mouse and assistive technology | **Blocked / unverified**, helper initialization failure; manual checklist below |
| Matching CI | **Pending**, exact HEAD lookup empty and final acceptance changes uncommitted |

**M20 is not fully accepted while the live and CI items remain open.**

The Windows computer-use skill was attempted on a newly seeded 1k synthetic application. The runtime failed before input/capture with **“failed to write kernel assets: The system cannot find the path specified. (os error 3)”**, including after reset. The synthetic app process was stopped without interaction before the gate. Live keyboard/mouse and Narrator/NVDA checks are **blocked/unperformed**, not failed product assertions. Manual follow-up: Tab/Shift+Tab through filters and popups; validation/cancel focus; chart arrows/table sorting and virtualized row actions; cohort paging/details/edit/delete/Back/Exit; rapid navigation while reads complete; announcement order/frequency, full coverage and unavailable labels; physical 250% scaling.

GitHub lookup for exact HEAD `ebea74a860b85dd7b0d52e65c2a51d2b287151bf` returned **no workflow runs** on 16 October. The workflow triggers pushes to `main` or PRs targeting `main`, not ordinary `develop` pushes. No matching tested merge revision was established. These uncommitted M20.10 documents/measurement tool additionally have no resulting commit or matching CI run. The user must commit/push through their normal PR workflow; no publication was performed here.

Exact M20.10 changed files (five): `README.md`, `docs/architecture.md`, `docs/breakdown-analytics.md`, `scripts/BreakdownMeasurements/BreakdownMeasurements.csproj`, `scripts/BreakdownMeasurements/Program.cs`. No product, test, schema, gate, CI, financial policy or existing assertions changed.

## M20.9 — Desktop UX and accessibility

Implemented **16 October 2026**, starting from clean `develop` at `92864ad177400d6558a6941eb7986815b1076b1f` (committed M20.8, newer than the reported `ca6c4fa` baseline). This is a focused Desktop change: no calculation, cohort membership, query, persistence, migration, request ownership or navigation-guard changes. No production journal, live provider or Git write was used. Next scope: **M20.10 — Reconciliation, Acceptance & Documentation**.

### Findings and corrections

| Finding | Correction / retained behavior |
| --- | --- |
| Visible filter labels lacked explicit automation relationships; invalid Apply left focus at the action | All nine selectors/inputs now have `LabeledBy`; invalid Apply requests the relevant input and exposes its validation as ItemStatus without reading or replacing the successful result |
| Page entry, cancellation and scoped detail return had no view-owned focus restoration | Breakdown enters at Period or the retained selected-group action (metric selector fallback); Cancel returns to Refresh. Scoped Trades enters at Back, opens at the detail heading, closes to the same live row or a remaining row/Refresh, and exits to ordinary Refresh |
| Removed rows/details could retain detached focus or fall back to the page scroll viewer | Resolve current row buttons by exact Trade ID, not recycled instances. A committed detail deletion waits for the authoritative list reload to enable actions. New keyboard, mouse or wheel input cancels that restoration |
| Deferred focus could outlive its page or interrupt new input | One-shot `PageFocus` checks loaded/visible owner, DataContext, generation, current input and enabled target. Unload invalidates pending requests; newer input wins. Normal asynchronous read completion does not request focus |
| Chart names omitted the selected metric; row actions repeated generic names | Chart names include group, metric, formatted value/unavailable, source and coverage. Native ListBox selection semantics remain; one composite Tab entry preserves native arrows. Group actions name their group; Trade View actions name their Instrument and describe Account |
| Sort state was only a visual glyph; header focus was weak | Sortable native headers retain Space/Enter activation, expose Ascending/Descending/Not sorted ItemStatus and a theme-accent focus border. The non-sortable action header does not claim sorting |
| Status LiveSetting alone did not emit a change event; scoped errors could be far below the banner | Opt-in, nonempty visible status/error changes raise WPF LiveRegionChanged when a listener exists. Scoped read errors also appear by Back/Exit. Sort/selection is not made a live announcement |
| Trades paging controls used a fixed horizontal arrangement | Counts and paging actions wrap at the right edge. No new keyboard handler consumes control keys, changes paging, or interferes with dialogs |

The existing read-only virtualized DataGrid remains the chart's numeric alternative. Shared theme controls already supply native button/ComboBox behavior, focus and disabled states. Chart/table selection, sorting, virtualized row access, wheel forwarding at boundaries, long-label wrapping and selected-details reflow remain intact. The concise mode explanation is **“Net preferred uses actual Net when known; otherwise Gross.”** Net, Net/Gross, Gross fallback, explicit Gross, unavailable and real zero retain existing labels and values. Applied scope stays attached to retained results after invalid filters, cancellation or failure. Scoped Trades continues to mean original membership with current records, not a historical snapshot or new matches.

### Verification and evidence

Final relevant Release verification: **672/672 passed**, zero failures/skips — **Desktop 524/524** (81 seconds), **Application 108/108**, **SQLite/Infrastructure 40/40** (26 seconds). The Desktop run includes all **11 new** tests; these are not counted twice. Release solution build: **0 warnings / 0 errors** (16.54 seconds). EF: **no pending model changes**, using the existing design-time in-memory configuration. Whitespace (tracked and new files) and local documentation file links passed. Evidence is synthetic only, under ignored `artifacts/m209/tests/{desktop-final,application-final,infrastructure}.trx` and Desktop test-output render directories. No full Release-gate or live-assistive acceptance is inferred from these checks.

Reproduction filters (each uses `dotnet test <project> -c Release --no-build --no-restore`, TRX output under `artifacts/m209/tests`):

- Desktop: `(FullyQualifiedName~.Breakdown.|FullyQualifiedName~TradesViewModelTests|FullyQualifiedName~MainWindowViewModelTests|FullyQualifiedName~.Dashboard.DashboardFilterTests|FullyQualifiedName~.Dashboard.DashboardViewModelTests|FullyQualifiedName~.Resources.)&Workload!=ExclusiveWpf`. Normal Windows token for existing synthetic DPAPI fixtures; no real credentials or live provider calls.
- Application: `FullyQualifiedName~Breakdown|FullyQualifiedName~TradeIdCohort|FullyQualifiedName~TradeListQueryTests`.
- Infrastructure: `FullyQualifiedName~BreakdownAnalyticsReaderTests|FullyQualifiedName~TradeListCohortTests|FullyQualifiedName~TradeListPagingTests`.

Build: `dotnet build PersonalTradingJournal.sln -c Release --no-restore`. EF: `dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure/PersonalTradingJournal.Infrastructure.csproj --context JournalDbContext --configuration Release --no-build`. Source formatting: `git diff --check` plus the four new files. Branch/HEAD remain unchanged; only the 13 files below are modified/new. **M20.9 implementation complete** with the explicitly unperformed acceptance checks below.

New tests cover invalid currency/start/end/reversed-date focus without reads, cancellation versus late results, all three chart metric descriptions, native bound Space activation, automation label/selection/sort contracts, newer-input/DataContext/unload focus protection, scoped entry/detail return/missing row/Exit, and delayed post-delete recovery with and without intervening input. Existing regressions retain exact cohort paging, failure/cancellation, post-commit reloads, guards, draft/applied queries, stable-key selection and ordinary Trades behavior.

Development checks caught two real focus-transition gaps in the new implementation: a row detached before its collection notification was observed, and a collapsed detail automatically focused its ancestor before a delayed deletion reload completed. The focused tests retain those assertions and cover the corrected paths. An early broader run had **521 passed / 2 failed** (both wrappers reported the same new scoped-focus case); this was not an existing economics or persistence failure. The render fixture was also corrected to use fresh off-screen roots rather than crop a previously HWND-arranged view. No deadlines, assertions, retries, skips or suite scheduling changed.

| Evidence type | Configuration / observed result | Remaining boundary |
| --- | --- | --- |
| Automated WPF behavior | Real synthetic HWND plus routed keyboard events, automation peers and logical focus assertions; explicit action and delayed-result checks | Not human keyboard input or proof of every screen reader's behavior |
| Automated renders | Light/Dark **1280×900 DIP at 96 DPI → 1280×900 pixels**, **700×900 DIP at 240 DPI → 1750×2250 pixels**. Twelve new validation/retained-error/empty images; existing chart/detail/table and populated/missing-cohort matrix regenerated | DPI is explicitly set for off-screen WPF; not a physical monitor/OS scaling check |
| Visual inspection | Representative Light normal retained error, Dark narrow validation/empty, Light normal and Dark narrow selected chart (negative, zero, unavailable, mixed Net/Gross), Light populated and Dark missing cohort. Filters/actions wrap; applied/error text remains readable; chart/details reflow and tables scroll | Inspect physical focus contrast and minimum shell with actual keyboard in M20.10 |
| Live keyboard/mouse | **Not performed** for M20.9 | Tab/Shift+Tab through filters, popups, metric, chart/table, sort, virtualized View actions, paging, details, Back/Exit; test rapid navigation/cancel/delete |
| Assistive technology | Automation properties/patterns checked; live-event wiring implemented | Narrator/NVDA reading order, selection/sort/coverage announcements and announcement frequency **unverified** |
| Complete gate / performance / CI | Not claimed by this focused milestone | Full `scripts/Test-Release.ps1`, large-history latency/memory, and CI matching a resulting commit remain for M20.10; uncommitted changes have no matching CI acceptance |

### Exact changed files (13)

- `README.md`
- `docs/breakdown-analytics.md`
- `docs/architecture.md`
- `src/PersonalTradingJournal.Desktop/Interactions/PageFocus.cs` (new)
- `src/PersonalTradingJournal.Desktop/Interactions/LiveAnnouncement.cs` (new)
- `src/PersonalTradingJournal.Desktop/ViewModels/Breakdown/BreakdownViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Breakdown/BreakdownChartPresentation.cs`
- `src/PersonalTradingJournal.Desktop/Views/Breakdown/BreakdownView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Breakdown/BreakdownView.xaml.cs`
- `src/PersonalTradingJournal.Desktop/Views/Trades/TradesView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Trades/TradesView.xaml.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Breakdown/BreakdownAccessibilityTests.cs` (new)
- `tests/PersonalTradingJournal.Desktop.Tests/Breakdown/BreakdownAccessibilityViewTests.cs` (new)

## M20.8 — Trade drill-down and navigation

Implemented **16 October 2026**, starting from clean `develop` at `ca6c4fa3b1f82fe0201cdfb7889314d7128d9631` (committed M20.7, newer than the request's reported baseline). No branch switch, migration, financial recalculation, production data, provider request or Git write. Only M20.8 is implemented; next scope is **M20.9 — Desktop UX & Accessibility**.

### Membership and navigation

**View trades** in the selected details and each comparison row freezes that displayed successful group's contributing IDs, including financially unavailable Trades. It captures the applied `BreakdownQuery`, typed stable group key, original label/financial/period context and count, plus a navigation identity. The action does not read draft filters or later selection after awaiting. Replaced row objects and duplicate submissions are rejected; duplicate display names never identify a cohort.

The scoped list is **original membership with current persisted records**, not a historical Trade snapshot or a continuously reevaluated predicate. New matching Trades do not enter; edits remain visible even after Account or classification changes; deleted originals disappear and are not replaced. Closure-date attribution is not reapplied as an Opened-date filter. Refresh retains the same frozen IDs. An empty/deleted cohort remains explicitly scoped. Current values need not equal the earlier analysis; refresh Breakdown to rebuild the analytics and choose a new cohort.

`TradeIdCohort` defensively copies valid unique IDs. A null query cohort is ordinary browsing; an empty one is zero rows. SQLite filters before count and pagination with **one parameterized JSON array**, not a variable per ID or an in-memory page filter. A deferred read transaction keeps count, rows, labels, projection and position-size metadata coherent within one call. Ordering retains the existing Trade-ID tie-breaker. No transaction is retained across pages or navigation. Existing Account filtering remains available to ordinary reader clients; scoped Desktop queries use the exact IDs without an extra Account/date predicate.

The banner identifies Breakdown, grouping/label, original period/currency/source context and available/original counts, with **Back to Breakdown** and **Exit Breakdown scope**. Existing details/edit/delete commands remain in the Trades workflow; closing details retains page/sort. Deletion corrects an invalid final page without losing the cohort. Ordinary page/sort is retained separately and restored on Exit. Scoped navigation/exit/owner close requires an open Trade form or operation to be finished/cancelled first; it never silently discards entered fields. MainWindow retains existing Journal/Calendar/other destination guards.

Back refreshes the **last successful** Breakdown query, including after a later Apply failed. Editable draft/validation, unapplied-change indication, chart metric and table sort remain intact. Selection is rebound by stable key, or cleared if absent, never substituted by label. Scope replacement, exit, deactivation and disposal invalidate obsolete loads; a failed/cancelled read has no unscoped fallback. Ordinary post-commit authoritative reloads retain their existing non-cancellable contract.

### Verification and limits

Final relevant verification: **661/661 passed**, zero failures/skips: Application **108/108**, Infrastructure **40/40** (25 seconds), Desktop **513/513** (56 seconds, normal Windows token for isolated synthetic DPAPI fixtures). The Desktop selection includes Breakdown, Trades ViewModel/paging, MainWindow navigation/guards, Dashboard filters/ViewModel and shared resource regressions. Release solution build: **0 warnings/errors**. EF consistency: **no pending model changes**, using the design-time in-memory database. Whitespace (including new files) and local Markdown-link checks passed. Ignored TRX evidence: `artifacts/m208/tests/application.trx`, `infrastructure-final.trx`, and `desktop-verified.trx`.

Migrated synthetic SQLite tests exercise 33,025 supplied IDs, deterministic multi-page ties, deleted/entirely deleted cohorts, empty versus absent scope, current edits, overnight identity and cancellation. A coordinated WAL writer deletes a member between count and page reads: the active read retains coherent count, row and size; the next read sees deletion, and connections are disposed. Desktop tests cover all seven grouping dimensions, unavailable money, duplicate/retained Setup identities, stale rows and duplicate clicks, failed Apply versus successful-query return, preserved draft/chart/sort/selection, details/delete page recovery, failure/cancellation retry and late responses after replacement/exit/deactivation/disposal.

Compiled WPF checks exercise bound commands through automation peers, row-specific parameters, scoped/ordinary visibility, wrapped banner actions and reachable table actions. Automated render viewports are **1280×900 DIP / 96 DPI** and **700×900 DIP / 240 DPI**, Light/Dark. Both populated and missing-record scope states are included. These are automated off-screen WPF renders, not live mouse, keyboard or screen-reader observations.

The initial regression identified and corrected a newly introduced ordinary post-commit cancellation-token regression without changing assertions. The sandbox run also had two synthetic DPAPI credential-fixture failures; the normal-token run verifies these without real credentials or provider requests. New render-fixture setup was corrected to include the app's shared converter/form resources and inspect arranged visibility rather than HWND visibility. No test deadlines, retries or skips were introduced.

**Remaining acceptance:** complete solution Release gate, live navigation/edit/delete interaction, actual screen-reader behavior, large-history latency/memory benchmarks and matching CI remain unverified. A 33,025-ID correctness test is not a large-history performance claim. Changes remain uncommitted, so no CI run can verify this resulting worktree. Cohorts and return display state are session-only, not a new persisted feature.

### Exact changed files (22)

- `README.md`
- `docs/breakdown-analytics.md`
- `docs/architecture.md`
- `src/PersonalTradingJournal.Application/Analytics/Breakdown/BreakdownGroupSelection.cs`
- `src/PersonalTradingJournal.Application/Trades/TradeIdCohort.cs` (new)
- `src/PersonalTradingJournal.Application/Trades/TradeListQuery.cs`
- `src/PersonalTradingJournal.Infrastructure/Trades/TradeListReader.cs`
- `src/PersonalTradingJournal.Desktop/Navigation/BreakdownTradeContext.cs` (new)
- `src/PersonalTradingJournal.Desktop/ViewModels/Breakdown/BreakdownViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/MainWindowViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Trades/TradesViewModel.Breakdown.cs` (new)
- `src/PersonalTradingJournal.Desktop/ViewModels/Trades/TradesViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/Breakdown/BreakdownView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Trades/TradesView.xaml`
- `tests/PersonalTradingJournal.Application.Tests/Trades/TradeIdCohortTests.cs` (new)
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Trades/TradeListCohortTests.cs` (new)
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Trades/TradeListPagingTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Breakdown/BreakdownDrillDownTests.cs` (new)
- `tests/PersonalTradingJournal.Desktop.Tests/Breakdown/BreakdownDrillDownViewTests.cs` (new)
- `tests/PersonalTradingJournal.Desktop.Tests/Breakdown/BreakdownTableViewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Trades/TradesViewModelBreakdownTests.cs` (new)
- `tests/PersonalTradingJournal.Desktop.Tests/Navigation/MainWindowBreakdownTests.cs`

## M20.7 — comparison chart and selected-group details

Implemented **16 October 2026**, from clean `develop` at `419ebea7eea9742fb2359c32bf95bf11cf5be104` (committed M20.6). Presentation only; the Application calculator, database reader, financial policies, filters and request ownership are unchanged. Executed Trades drill-down remains **M20.8**; no inactive navigation buttons are shown.

### Chart and details

The local **Chart metric** selector offers selected-mode P&L, eligible closed-Trade count and Win Rate. It never submits draft filters, changes calculation mode or invokes the reader. It persists for the retained page session. Labels/currency/basis come from the last successful result; loading, errors, cancellation and edited filters cannot relabel retained values.

Existing Dashboard/Calendar charts are native WPF time-series controls, not category selection lists. The new chart therefore uses a native, recycling **ListBox** with a small `BreakdownBar` drawing surface per realized row, rather than another chart library or modifying those time-series controls. The list provides keyboard selection and automation names; the existing table remains the equivalent numeric view. Theme brushes distinguish gains/losses/zero, with signed P&L and visible values so color is not the only indication. Labels and coverage wrap. Below 1,000 DIP of content width, the details panel stacks below the chart; above it they share two columns. The chart viewport is bounded to 320 DIP, all groups remain scrollable, and wheel input at either edge continues through the page.

P&L uses one common min/max range including zero across all returned groups. Count starts at zero; Win Rate stays on **0–100%**, using already percentage-scaled values unchanged. All-zero/empty P&L uses a finite 0–1 display range. A known zero is labeled numerically and drawn as a hollow marker on the zero baseline. Unavailable values show **— Unavailable**, remain selectable and have no numeric bar/marker; they do not influence the scale. Source decimals remain unchanged; only geometry uses double coordinates, including for extreme decimal values. This is coordinate normalization, not additional analytics. Axis range and metric units are visible.

Chart rows follow the table's exact current order, including chronological sorts and deterministic identity ties. Changing the metric does not change sorting. Chart/table/details share the existing selected key. Refresh rebinds that identity to new result objects or clears it if absent; a duplicate name cannot substitute. Selection callbacks must originate from a current row instance, rejecting replaced chart/table objects even if their record values match. Selection scrolls only the chart's logical viewport, not the enclosing page (ordinary end clamping may occur when selected text changes page height).

Details show the group/dimension, applied currency/mode and its own financial source, eligible/evaluable counts, known wins/losses/break-evens, P&L, Win Rate, average Trade/winner/loss magnitude, Profit Factor and its explicit defined/undefined/unbounded meaning, Net/fallback/unavailable/cost coverage, small-sample heuristic and Setup classification where relevant. They never enumerate contributing Trades, recalculate results, infer confidence or recommend trades. No selection displays a short prompt. All four sections are prepared from the same successful snapshot before notification; stale requests cannot update chart or details independently.

### Verification and limits

Final focused M20.5–M20.7 Desktop/shell checks: **60/60 passed**, zero failures/skips (15 seconds), including 10 new chart/detail cases. Relevant MainWindow navigation/guards, Dashboard filters/ViewModel and resource regressions: **252/252 passed**, zero failures/skips (53 seconds, normal Windows token for synthetic DPAPI fixtures). Two shell cases overlap the selections. Solution Release build: **0 warnings / 0 errors** (10.47 seconds). EF design-time consistency: **no pending model changes**, using the in-memory factory. Whitespace and local documentation-link checks passed. Local ignored TRX evidence: `artifacts/m207/final-complete/breakdown.trx` and `artifacts/m207/regression/desktop-regression.trx`.

New tests cover signed/common/all-zero/extreme geometry; raw percentages and counts; unavailable/empty states; shared selection and ordering; duplicate names; updated refresh details; stale row callbacks/results; cancellation retention; local interactions without reads; applied-versus-draft currency/mode; group-specific coverage and exact average formatting. Compiled WPF tests cover both binding directions, selection accessibility, virtualization with 60 groups, horizontal table access through existing M20.6 tests, chart-only scroll positioning, responsive stacking, and wrapped long labels. An inspected first render revealed a clipped chart label; explicit wrapping and a multiline layout assertion corrected it. Empty detail fields collapse rather than retaining unused vertical space. The initial new scroll assertion was corrected to allow normal outer extent clamping when a shorter selected label reduces page height; no product deadlines or existing assertions changed.

Automated chart render viewports are **1280×900 DIP / 96 DPI** and **700×900 DIP / 240 DPI**, Light/Dark, with signed/zero/unavailable rows, selected details and long labels. Additional renders show unselected and empty states. Existing M20.5/M20.6 renders remain part of focused regression coverage. These are automated WPF renders, not actual mouse, keyboard or screen-reader observations. Full solution gate, large-history performance and matching CI acceptance are not claimed. No production journal, provider call, schema change or Git write was used.

Exact changed files (10):

- `src/PersonalTradingJournal.Desktop/ViewModels/Breakdown/BreakdownChartPresentation.cs` (new)
- `src/PersonalTradingJournal.Desktop/ViewModels/Breakdown/BreakdownViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/Breakdown/BreakdownBar.cs` (new)
- `src/PersonalTradingJournal.Desktop/Views/Breakdown/BreakdownView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Breakdown/BreakdownView.xaml.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Breakdown/BreakdownChartTests.cs` (new)
- `tests/PersonalTradingJournal.Desktop.Tests/Breakdown/BreakdownChartViewTests.cs` (new)
- `README.md`
- `docs/breakdown-analytics.md`
- `docs/architecture.md`

Next: **M20.8 — Trade Drill-down & Navigation**.

## M20.6 — compact summary and group comparison table

Implemented **16 October 2026**, starting from clean `develop` at `b31bab7779a2c851c3f8579b1ad783688e519203` (committed M20.5, newer than the request's reported baseline). Desktop presentation only: calculation policy, filtering, SQL snapshot consistency, request ownership, database and other analytics screens are unchanged.

### What the page displays

The retained **last successful** scope now has a small wrapping strip: eligible closed Trades, selected-mode P&L, Win Rate and Profit Factor. The summary wraps the supplied `Summary` directly; it never totals displayed rows. Currency, calculation mode and financial source remain attached to the applied query, not editable filters. Source coverage is shown once beside the summary, with specific Setup-classification coverage for Setup grouping and New York entry-time clarification for weekday/hour groups.

A read-only native WPF **DataGrid** displays every returned group: Group, Trades, Win Rate, P&L, Average trade, Profit Factor, Basis / Coverage. Existing Desktop tables are bespoke ItemsControl/Grid layouts; there is no reusable sortable, virtualized table wrapper. This uses WPF's built-in table rather than a new control framework, styled locally with existing theme/financial/selection resources. The Group column has a resizable 260-DIP initial width and wraps; numeric columns align right, retain full formatted values in tooltips and row accessibility descriptions, and scroll horizontally at narrow widths. The bounded table viewport recycles rows/columns; an exhausted inner wheel scroll continues into the page. Short tables grow naturally up to 340 DIP, rather than reserving large KPI panels. No chart, selected-group detail panel or inactive View Trades action is added.

Each row uses **its own** returned Net, Net/Gross, Gross fallback, Gross or unavailable classification. Evaluable/eligible counts and any fallback/unavailable counts are visible, not tooltip-only. Supporting help gives Net-used, fallback, explicit Gross, unavailable and missing-total-cost counts. Gross fallback is not estimated Net. Incomplete financial coverage displays unavailable totals/rates/averages; it never substitutes a known subtotal. Unknown Net with available Gross is separate from values missing in both sources.

Formatting reuses the Dashboard monetary formatter (`N2` plus explicit currency), one-decimal percentages and two-decimal ratios. Numeric zero stays numeric. Unavailable values use **—** with an explanation. Only explicit `ProfitFactorStatus.NoLosses` displays **∞**, meaning positive profits with zero losses; empty, incomplete and all-break-even ratios remain undefined/unavailable with distinct help. Positive/negative money uses shared success/danger colors, zero/unavailable neutral; signs and text remain available independently of color. Trades-page policies are untouched.

**Small sample** means **1–19 financially evaluable Trades**, a UI heuristic, not statistical confidence. A zero-evaluable group is marked unavailable rather than merely small. Every group remains displayed. This implements the M20.6 requested threshold; the earlier audit's proposed eligible-count threshold was not an implemented policy. Setup assignment counts retained Setup IDs even if metadata is missing, while null Setup remains Unclassified; IDs, not labels, distinguish such rows.

### Sorting, selection and atomic publication

Initial order is exactly the calculator's deterministic order. Header activation (including keyboard/UI Automation) sorts raw numeric counts, totals, percentages, averages or ratios, never formatted strings. Group sorting uses stable chronological keys for Monday–Sunday, 00–23 and duration buckets; other labels use ordinal case-insensitive order. Basis / Coverage sorts by source label. Ties always use ascending `BreakdownGroupKey`. Missing numeric values stay **last in both directions**; explicit unbounded Profit Factor sorts above finite ratios and before unavailable values in descending order. There is no numeric infinity sentinel.

The ViewModel retains sort choice/direction and selected **key**, not label or row position, for the session. Sorting requires no read. Refresh retains selection only if the exact key remains; changing the applied grouping or losing that key clears it even if another row has the same name. Editing a grouping filter alone does not invalidate the still-displayed successful result. Navigation preserves the M20.5 state and the table sort. Native ItemsSource replacement cannot silently move selection to a same-name group.

`BreakdownPresentation` wraps the complete snapshot and its metric records. It does not enumerate/copy contributing Trade IDs or inspect Trades to calculate values. Summary, rows and selection are prepared and assigned before publication under the existing request-ownership check. Older callbacks, loading, invalid drafts, cancellation and failures cannot replace only part of the successful presentation. Mapping costs scale with group count; full-population performance remains unmeasured.

### Verification

Final focused Desktop/M20.5 tests: **50/50 passed**, no skipped cases (10 seconds); this includes 24 new presentation/bound-WPF cases and the 26 existing selected M20.5 cases. Relevant shell, Dashboard-filter/ViewModel and resource regressions: **252/252 passed**, no skipped cases (58 seconds, normal Windows token for synthetic DPAPI fixtures); two shell cases overlap the focused selection. Solution Release build: **0 warnings, 0 errors** (11.43 seconds). EF design-time consistency: **no pending model changes**, using the in-memory factory. Whitespace and local documentation-link checks passed. TRX evidence is in `artifacts/m206/final-verified` and `artifacts/m206/regression` (local, ignored artifacts).

Focused tests include supplied-summary mapping without enumerating source IDs, all basis labels, partial coverage, zero/undefined/unbounded formats, 0/1/19/20 sample boundaries, raw numeric/chronological sorting, ties, duplicate labels, Unclassified versus missing Setup, navigation/refresh selection, no-read sorting and stale/failure retention. Compiled WPF checks invoke a real column-header automation item, preserve selection through sorting/refresh, exercise an 80-group virtualized population, and reach the rightmost coverage column and unavailable rows. Fixtures use synthetic data only. During development, the new test's detached-tree scrolling/automation setup was corrected; its narrow-width assertion also caught star sizing compressing numeric columns to 20 DIP. Stable initial column widths now retain horizontal reachability. An overlapping test/build attempt encountered a locked output DLL; the final build/test ran after that test process exited. No existing assertions, deadlines or production financial behavior were changed.

Table render viewports: **1280×800 DIP at 96 DPI** and **700×800 DIP at 240 DPI**, Light/Dark, both horizontal ends and unavailable rows. Existing M20.5 filter renders additionally cover **980×570 / 96 DPI** and **700×570 / 240 DPI**. Automated rendering is not live mouse/keyboard or screen-reader acceptance. Full solution gate, matching CI and large-history performance are not claimed. No production journal, live provider, schema change or Git write was used.

Exact changed files (9):

- `src/PersonalTradingJournal.Desktop/ViewModels/Breakdown/BreakdownPresentation.cs` (new)
- `src/PersonalTradingJournal.Desktop/ViewModels/Breakdown/BreakdownViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/Breakdown/BreakdownView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Breakdown/BreakdownView.xaml.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Breakdown/BreakdownPresentationTests.cs` (new)
- `tests/PersonalTradingJournal.Desktop.Tests/Breakdown/BreakdownTableViewTests.cs` (new)
- `README.md`
- `docs/breakdown-analytics.md`
- `docs/architecture.md`

Next: **M20.7 — Comparison Chart & Selected-Group Details**; executed Trades drill-down remains M20.8.

## M20.5 — Desktop filters and page state

Implemented **16 October 2026**, from clean `develop` HEAD `db445629ec8623a673c293379f37cc790bce6301`. This is the actual committed M20.4 baseline, newer than the request's reported SHA. The Breakdown destination now uses the real `IBreakdownAnalyticsReader` through a retained Desktop ViewModel. The Application calculator, Infrastructure reader, single-statement snapshot, economics and schema are unchanged. No fabricated chart/table figures are displayed.

### Filters and actions

- Defaults: **All history**, **All accounts**, **All instruments**, **Instrument** grouping and **Net preferred**. Initial currency is the ordinal-first distinct Account/Instrument catalog currency (the existing Desktop sorted-currency selection convention), or USD when catalogs contain none. The editable currency field permits historical pricing currencies absent from current catalogs; suggestions are in its tooltip/accessibility help. There is no FX conversion.
- Period choices reuse `DashboardPeriod`: All, current Monday–Sunday week, calendar month, calendar year and Custom. Presets anchor to the New York date when selected/reset, not UTC or the machine's local date. Refresh preserves the submitted boundaries even across midnight. Custom inclusive closure-date bounds use `yyyy-MM-dd`; an empty side is unrestricted. `BreakdownQuery` validates the final date/identity/currency/mode/grouping request.
- All seven supported groupings have readable labels; Strategy is absent. Net preferred and Gross submit the established modes, with actual Net (including zero), fallback and unavailable classifications coming solely from the returned metadata.
- Catalogs include inactive references. Selection is by stable ID, never by name. A selected reference removed from the catalog remains a visibly unavailable exact-scope option; a same-name replacement cannot take its place.
- **Apply filters** captures the valid draft once. Editing intermediate values does not read the database. Invalid input shows nearby validation and retains the last complete result.
- **Refresh** reloads the **last submitted** query, including a failed/cancelled request, without applying edited draft values. Its tooltip/help makes the distinction explicit. **Reset** restores defaults and submits one request, not one per field. **Cancel loading** appears only during a cancellable operation and leaves the last successful result intact.

### Ownership, lifecycle and presentation

Draft controls are separate from `Result.Query`. Every displayed result is labeled **Last successful result** with its original period, Account/Instrument, currency, grouping and mode. Unapplied filters, stale data, empty population, unavailable financial values, failure and cancellation have distinct text states. The small result area shows eligible Trade/group counts, source label and Net/Gross/unavailable/missing-total-cost counts directly from the result. It does not recalculate economics or present later-milestone KPIs, tables, charts or drill-down controls.

Each operation owns a cancellation source and generation. New requests cancel and invalidate earlier requests; older success, failure, cancellation and finally paths cannot publish or clear the new loading state. Both catalog loading and the complete reader/calculator run off the dispatcher, even where SQLite async executes synchronously. UI publications resume on the captured context. The resolved first currency is recorded **before** the reader starts, so cancelling the first read cannot retarget Refresh to a provisional currency. Catalog refresh supplies selector metadata only; contributing facts and result labels remain under M20.4's existing reader snapshot guarantee.

The shell retains one page instance: first activation reads once, repeated activation does not duplicate it, and leaving/returning preserves draft filters and the last result. Accepted navigation/window close and disposal invalidate in-flight work. Existing Journal/other destination guards run unchanged before navigation. Existing Trade/import/bulk-delete and Calendar-Trade commit events mark the page stale and cancel obsolete work; they do not auto-apply a draft or auto-load. **Explicit Refresh** also reloads Account/Instrument catalogs after their edits; no new global event system is introduced. State is session-local, not a new persistence contract.

### Verification and limits

**M20.5 implemented and locally verified.** Tests use synthetic controllable readers, task barriers and the existing isolated WPF/STA harness, with no sleeps, test retries or deadline changes. Coverage includes exact queries/defaults/NY boundaries, invalid filters, metadata identity, empty/unavailable/fallback states, Reset, failure/cancel recovery, stale callbacks, first-load currency cancellation, unavailable IDs, retained navigation and owner shutdown. Bound compiled-WPF checks cover dispatcher publication, worker execution, actual filter bindings (including exact Account/Instrument after refreshed catalogs), validation, loading cancellation, wrapping, theme resources, accessible names and scroll reachability.

| Final local check | Result |
| --- | --- |
| New Breakdown ViewModel, compiled WPF and shell cases | **26/26 passed**, zero failures/skips, 7 s |
| Shared MainWindow navigation/guards, Dashboard filters/state and resource regression selection | **252/252 passed**, zero failures/skips, 59 s |
| Release solution build | **0 warnings / 0 errors** |
| `git diff --check` and new-file trailing-whitespace check | Passed |
| Local links in the three changed documents | **152 targets checked, 0 missing** |
| Interactive keyboard/mouse/screen reader; matching CI | Not performed; uncommitted changes have no matching CI claim |

The focused and regression selections overlap in the two new shell cases; these are not 278 distinct tests. The first restricted-token regression run passed **250/252**: both existing `DailyReviewAcceptanceCompiledShellKeepsCapturedEvidenceOrCancelsOnSourceNotification` cases failed before their synthetic HTTP handler because the Settings credential path reported missing/unreadable credentials. They use temporary synthetic DPAPI storage, not real keys. The unchanged pair passed **2/2** outside the restricted token, followed by the same complete 252-case selection above. This establishes an environment-sensitive credential-test boundary; no production credential, provider behavior, assertion or deadline was changed to accommodate it. No native timeout occurred in these selections; the complete Calendar native/full solution gate was not run.

Commands: `dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~.Breakdown.|FullyQualifiedName~MainWindowViewModelTests.Breakdown'`; regression uses `--no-build --no-restore --filter '(FullyQualifiedName~.Navigation.MainWindowViewModelTests|FullyQualifiedName~.Dashboard.DashboardFilterTests|FullyQualifiedName~.Dashboard.DashboardViewModelTests|FullyQualifiedName~.Resources.)&Workload!=ExclusiveWpf'`. Result files are `artifacts/m205/final-readable/breakdown.trx` and `artifacts/m205/regression-final/desktop-regression.trx`; the first regression is retained under `artifacts/m205/regression`. The focused run additionally verified wrapped inactive-reference labels and full-name accessibility after final render inspection. Build: `dotnet build PersonalTradingJournal.sln -c Release --no-restore`. No EF/schema change was made and EF consistency was not rerun for this Desktop-only request.

Automated renders are inspected at **980×570 DIP / 96 DPI** and **700×570 DIP / 240 DPI** in Light and Dark, including the bottom of the scroll area. Development render inspection caught missing local theme styles and an unsupported editable-ComboBox template: the page now reuses shared input/button/scrollbar styles and a styled currency text field. These checks are not live mouse/keyboard/screen-reader acceptance.

Large-history performance remains **unmeasured**: the unchanged reader/calculator processes the complete matching population with memory proportional to facts and IDs. Cancellation does not promise interruption of every synchronous SQLite operation. Live interaction, complete solution gate and matching CI are not claimed; unrelated backend suites were not rerun for this Desktop-only step. No production data, provider request or Git write was used. **M20.6 — Compact Summary & Group Comparison Table remains next**, followed by charts/details and exact Trades navigation in later steps.

Exact changed files (13):

- `src/PersonalTradingJournal.Desktop/App.xaml`
- `src/PersonalTradingJournal.Desktop/App.xaml.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/MainWindowViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Breakdown/BreakdownViewModel.cs` (new)
- `src/PersonalTradingJournal.Desktop/Views/Breakdown/BreakdownView.xaml` (new)
- `src/PersonalTradingJournal.Desktop/Views/Breakdown/BreakdownView.xaml.cs` (new)
- `tests/PersonalTradingJournal.Desktop.Tests/Breakdown/BreakdownViewModelTests.cs` (new)
- `tests/PersonalTradingJournal.Desktop.Tests/Breakdown/BreakdownViewTests.cs` (new)
- `tests/PersonalTradingJournal.Desktop.Tests/Navigation/MainWindowBreakdownTests.cs` (new)
- `tests/PersonalTradingJournal.Desktop.Tests/Navigation/MainWindowViewModelTests.cs` (fixture injection only)
- `README.md`
- `docs/breakdown-analytics.md`
- `docs/architecture.md`

## M20.4 — consistent database reader

Implemented **16 October 2026**, starting from a clean `develop` worktree at `45717fad0dbddf80488bbfd05ec8143431a04135` (the committed M20.3 implementation). No schema, stored economics, shared analytics policy or Desktop changes.

### Boundary and authoritative projection

[`IBreakdownAnalyticsReader.GetAsync(BreakdownQuery, CancellationToken)`](../src/PersonalTradingJournal.Application/Analytics/Breakdown/IBreakdownAnalyticsReader.cs) returns the existing `BreakdownAnalyticsSnapshot`. Application has no EF/SQLite dependency. [`BreakdownAnalyticsReader`](../src/PersonalTradingJournal.Infrastructure/Analytics/BreakdownAnalyticsReader.cs) is registered transiently through `AddPersistence`, using the existing `IDbContextFactory<JournalDbContext>` and maintenance/session lifetime rules.

One no-tracking SQL SELECT reads:

| Source | Fields / role |
| --- | --- |
| `Trades` | Stable Trade, Account and Instrument IDs, current Setup classification, historical `PricingCurrency` |
| `TradeBrowse` (one-to-one by Trade ID) | Domain-derived status/direction, opening/final closure UTC instants, Gross, nullable total costs and actual Net |
| Account / Instrument / Setup left joins | Current names or symbol and activity, in the same snapshot as the facts; never pricing or identity substitutes |

This follows the initialized database's existing browse-projection invariant: Trade writes/imports maintain it transactionally and startup reconciles its version. This reader does not repair missing projections or reconstruct economics from executions. Missing financial values remain null; known actual Net zero remains zero. Projection never substitutes Gross for Net or introduces estimated Net. `BreakdownMetricCalculator` alone applies the existing per-Trade Gross/Net-preferred policy and calculates summary, groups, coverage and contributing IDs.

### Filtering, population and consistency

SQL requires `Closed` with non-null final closure and the query's exact historical pricing currency. Optional exact Account/Instrument predicates combine with AND. Inclusive New York closure dates reuse `DashboardAnalyticsQuery`'s UTC `[start, end)` bounds, including 23/25-hour DST days and tick precision. Missing sides are unrestricted. Entry weekday/hour and elapsed duration grouping happen in the calculator and cannot change closure membership. No active-reference filter is applied. Duplicate names do not merge identities; a missing Setup record leaves its retained ID distinct from null/Unclassified.

There is **one contributing SQL statement**, with no separate count or metadata reads, unrelated cache, collection joins, paging, `LIMIT`, or per-Trade/per-group query. SQLite holds one statement read snapshot through result consumption. EF materializes scalar facts and optional labels, not tracked entities. Thus membership, economics, classification and all labels are coherent even when a WAL writer commits while that statement is being consumed. No explicit multi-statement transaction or global pragma/isolation change is needed. If a future implementation adds contributing statements, it must enclose them in the established deferred read transaction rather than extend this single-statement guarantee implicitly.

All matching scalar inputs materialize before the context is asynchronously disposed; the CPU calculator runs **after** that scope ends. No deferred query or storage object escapes. Each call creates its own context; the next call observes newly committed changes without remembered filters or cached results. Cancellation is checked before context creation, propagated to EF/materialization and the calculator, and never converted into empty success. Provider-supported cancellation cannot promise immediate interruption of every synchronous SQLite operation. Database/calculation/overflow errors propagate without partial results; there is no retry.

**Size policy:** complete selected-population aggregation, with memory proportional to matching scalar facts and contributing IDs. There is no arbitrary row cap or silent truncation, nor any promise of streaming constant-memory metrics. Narrowing filters reduces the selected population. This step loads no executions, screenshots, notes, Journal or coaching content. Very large all-history populations still need performance measurement before a future explicit limit or index is justified; no speculative migration/index is added.

### Verification and exact files

**M20.4 complete.** Focused SQLite checks pass **30/30** on independent migrated-template databases, including all dimensions/modes, full 123-Trade populations, direct calculator equality, independent/combined filters, historical currency, precise decimal values, six DST-adjacent closure dates, entry/closure separation, open/partial exclusion, known Net zero, mixed/all-Gross fallback, unavailable values, inactive/missing references, duplicate display names and multiple screenshot/execution rows.

The three deterministic concurrency cases use a read-only, non-pooled reader connection and an EF command interceptor: after SQLite starts its one SELECT and before EF consumes it, a separate WAL writer atomically changes membership, economics, classification and Account/Instrument/Setup metadata. The original read retains old IDs/values/labels; the next read sees the committed population/labels. This tests statement consumption, not invented inter-query boundaries. No sleeps, retries or production hooks are used. Additional interception asserts one SELECT, no entity tracking, no collection loads or paging; cancellation before/during/after SQL, injected read failure and invalid calculation inputs release contexts/connections and permit a subsequent normal read.

The first compiled focused run passed 25/26: a test compared JSON number spelling (`1` versus `1.0`) rather than decimal equality after SQLite round-trip. It now checks every metric/coverage record, group key/label and contributing ID numerically. Four further filter/lifetime/metadata cases were added before the final 30-case run. A preceding test compile error used an unsupported xUnit `TheoryData` constructor and was corrected to the repository's supported collection API. Neither required a production behavior change.

The first complete Infrastructure run in the restricted process passed **1,378/1,379**, with one setup failure in existing `BackupArchiveTests.LinkedScreenshotRootIsRejectedWithoutPackagingOutsideBytes`: Windows denied `Directory.CreateSymbolicLink` at line 113, before the backup assertion. The same unmodified synthetic test passed **1/1 outside the sandbox**, followed by the final complete Infrastructure project **1,379/1,379**, zero failures/skips (2m07s test duration). No assertions, fixture rules, deadlines or product behavior were changed to accommodate the permission failure.

| Final check | Result |
| --- | --- |
| New focused SQLite reader cases | **30/30 passed**, zero failures/skips |
| Application Breakdown cases | **94/94 passed**, zero failures/skips |
| Complete Infrastructure project | **1,379/1,379 passed**, including existing Dashboard/Calendar/Daily/Period readers, persistence and portability |
| Complete Application project | **974/974 passed**, including existing strict/estimated policies |
| Release solution build | **0 warnings / 0 errors** |
| EF model consistency | No changes since last migration; in-memory design-time context |
| Whitespace / documentation paths | Passed; tracked diff plus new-file whitespace, **150 local Markdown targets** |
| Full solution test gate / WPF / visual / live | Not run; no Desktop behavior changed in this reader milestone |
| Matching CI | Unverified for uncommitted changes; no resulting commit/CI acceptance claimed |

Commands: `dotnet test tests/PersonalTradingJournal.Infrastructure.Tests/PersonalTradingJournal.Infrastructure.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~BreakdownAnalyticsReaderTests` (focused); Application focused filter `FullyQualifiedName~.Analytics.Breakdown.` with `-c Release --no-build --no-restore`. Complete Infrastructure/Application projects use `-c Release --no-build --no-restore` without filters. `dotnet build PersonalTradingJournal.sln -c Release --no-restore`; `dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure/PersonalTradingJournal.Infrastructure.csproj --context JournalDbContext --configuration Release --no-build`; `git diff --check`. Final TRX evidence: ignored `artifacts/m204/focused-final/breakdown.trx`, `application-focused/breakdown.trx`, `application/application.trx`, `infrastructure-final/infrastructure.trx`. Focused cases are included in their full project totals, not additional unique tests.

Exact changed files (7):

- `src/PersonalTradingJournal.Application/Analytics/Breakdown/IBreakdownAnalyticsReader.cs` (new)
- `src/PersonalTradingJournal.Infrastructure/Analytics/BreakdownAnalyticsReader.cs` (new)
- `src/PersonalTradingJournal.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Analytics/BreakdownAnalyticsReaderTests.cs` (new)
- `README.md`
- `docs/architecture.md`
- `docs/breakdown-analytics.md`

M20.5 remains next: independent page state, cancellation/latest-request publication and committed-data invalidation. WPF controls, charts, executed Trades drill-down and visual/live acceptance are outside M20.4. No production data, provider, Git writes or matching CI acceptance are involved.

## M20.3 — group metrics and data quality

Implemented **16 October 2026**, starting clean on `develop`, HEAD `e7f718cb0c705572a18514a092e7fc6ec7a81925`. **The user-approved M20.3 Net-preferred policy supersedes the strict-Net-only Breakdown decision recorded in M20.2 below.** Shared strict-Net/estimated-Net contracts and existing Dashboard, Calendar, Trades, Daily and Period Review behavior are unchanged. No SQL, WPF, chart, persisted analytics or executed drill-down is added.

### Calculation modes and provenance

`BreakdownQuery.Mode` now uses the Breakdown-specific `BreakdownCalculationMode`, replacing its former shared `PnlBasis` selection:

| Mode | Per-Trade selected value |
| --- | --- |
| **Net preferred (default)** | Known **actual Net**, including exactly zero; otherwise supplied **Gross**; otherwise unavailable |
| **Gross** | Supplied Gross; otherwise unavailable. Explicit Gross is never counted as fallback |

Resolution happens once per eligible Trade, before aggregation. A group never switches wholesale to Gross because one member has unknown Net. No costs are estimated or coerced to zero, individually known costs are not subtracted again, and Gross is never stored as Net. **Gross fallback is a separately identified source value, not estimated Net.** Net preferred may be financially complete even when actual-Net coverage is partial, provided every missing Net has available Gross.

`BreakdownFinancialCoverage` carries the selected mode, eligible count, Net-used, Gross-fallback, explicit-Gross-used and unavailable counts. Its `Source` gives enough metadata for future UI labels:

| Source | Meaning |
| --- | --- |
| `Net` | Every evaluable selected value uses actual Net |
| `NetGross` | Evaluable values include both actual Net and Gross fallback; label **Net/Gross** |
| `GrossFallback` | Every evaluable value uses Gross because actual Net is unknown; label **Gross fallback** |
| `Gross` | Explicit Gross mode, including empty/unavailable selections |
| `NoEvaluableValues` | Net-preferred selection has no available Net or Gross value |

Source labeling is independent of completeness. A `Net` or `NetGross` source with an unavailable Trade still has an unavailable complete total. In Net preferred, **Net-used + Gross-fallback + unavailable = eligible**; in Gross, **explicit-Gross-used + unavailable = eligible**, with Net-used/fallback both zero.

### Input, filtering and grouping

[`BreakdownTradeFact`](../src/PersonalTradingJournal.Application/Analytics/Breakdown/BreakdownTradeFact.cs) composes existing `TradeAnalyticsFact` with exact Account/Instrument IDs, Domain direction and optional reference names/activity. Gross, actual Net, nullable total costs, original pricing currency, lifecycle instants and current Setup ID remain authoritative projected facts. This does not reuse a saved coaching packet or reconstruct economics from prices.

The caller supplies the **complete candidate population**, not a display page or execution join. [`BreakdownMetricCalculator.Calculate`](../src/PersonalTradingJournal.Application/Analytics/Breakdown/BreakdownMetricCalculator.cs) validates unique IDs across the entire supplied input, including excluded candidates; duplicates fail instead of silently inflating or being deduplicated. Validated lifecycle/currency/economic inputs follow the existing analytics projection contract: UTC timestamps, consistent Closed/closure state, closure not before opening, valid identities/direction, canonical currency, nonnegative known costs, no final P&L on open Trades, and known actual Net backed by known Gross/costs. A closed projection with unavailable/invalid duration fails clearly without returning a partial result or assigning a fake bucket. A valid open Trade remains excluded.

It applies all original Account, Instrument, historical currency and New York **closure** bounds before grouping. Open/partially exited Trades are excluded; missing financial values do not remove an otherwise eligible closed Trade. The seven M20.2 grouping definitions are reused exactly: stable Instrument/Account/Setup IDs, Unclassified only for null Setup ID, Long/Short, New York entry weekday/hour and the six UTC elapsed-duration buckets. Repeated autumn hours share their hour group; each Trade belongs to exactly one selected group. Strategy remains deferred.

Eligible Trades are sorted by Trade ID before arithmetic and group metadata selection; groups use the stable key order. Labels come from the lowest-Trade-ID member's optional metadata, with the established inactive/missing fallbacks. Metadata should describe one consistent read snapshot; it never changes membership. Identical display names do not merge distinct IDs, and missing Setup names do not convert retained classifications to Unclassified.

### Results, formulas and coverage

[`BreakdownAnalyticsSnapshot`, `BreakdownMetrics`, `BreakdownPnlMetrics` and coverage records](../src/PersonalTradingJournal.Application/Analytics/Breakdown/BreakdownMetrics.cs) return the original query, summary, keyed group results/labels, selected-mode and Gross metrics, financial/classification coverage and ordered contributing Trade IDs. Published lists are read-only. Monetary values are numeric decimals, not formatted strings or WPF types.

For each summary/group, **N** is every eligible closed Trade and **K** is every Trade with an available selected value. The calculator reuses unchanged `PnlAccumulator` arithmetic, `MetricCoverage`, `ProfitFactorMetric`, `AveragePnlMetric` and shared average/outcome rules; its internal basis tag is not exported as Breakdown provenance. `BreakdownPnlMetrics` deliberately carries neither a shared strict-Net basis nor an estimated-Net flag. Average Trade is the small additional derivation from a complete shared sum/count.

- **Selected P&L:** sum resolved values. `Total` is available only for a nonempty complete population; `KnownSubtotal` can describe K/N, never a complete result.
- **Outcomes:** unrounded selected value greater than / less than / equal to zero. **Wins + losses + break-even = K**. A positive Gross with negative known Net is a Net-preferred loss; unknown Net with positive Gross is a Gross-fallback win.
- **Win Rate:** `100 × wins / N`, including break-even in the denominator, available only with complete nonempty coverage.
- **Average Trade:** complete selected total / N. **Average Win:** positive selected-value sum / wins. **Average Loss:** absolute negative selected-value sum / losses, the existing positive-magnitude convention. All averages require complete population coverage; no wins/no losses remain distinct unavailable reasons.
- **Profit Factor:** positive selected-value sum / absolute negative selected-value sum. All losers gives defined zero; positive profits without losses gives `NoLosses` with no capped numeric sentinel; all break-even gives `AllBreakEven`; empty and incomplete states stay distinct.
- **Gross:** a separate bundle over supplied Gross on the same full eligible population, regardless of selected mode.

`BreakdownDataCoverage` exposes independent actual-Net, Gross, known-total-cost and Setup-classification coverage, plus missing Setup metadata count. A retained Setup ID counts as classified even if inactive or unavailable; Unclassified is only a null ID. Missing-cost count uses each Trade once when authoritative `TotalCosts` is absent. The current input does not promise separate commission/fee component counts or infer missing-cost cause merely from missing Net. Empty, Complete, Partial and Unavailable use established `MetricCoverage` semantics. A real zero stays zero; both financial values absent leaves the Trade counted with no inferred outcome. No R-multiple is derived.

Arithmetic uses checked decimal sums, decimal division and no presentation rounding. Invalid input, overflow or cancellation publishes no partial snapshot. Input is enumerated once; output and contributing IDs are deterministic across input order. Memory scales with the complete supplied population; M20.4 must enforce its reader/size policy without silently paging or truncating the metric population.

### Reconciliation and limits

For each dimension, group eligible/evaluable/source/classification/cost counts and contributing IDs reconcile to the summary, with disjoint group membership. Complete additive selected/Gross totals reconcile. Known subtotals are partial evidence, not totals. Summary ratios/averages are recalculated from underlying Trades, never averages of groups with unequal sizes.

Reconcile against another service only for the same population, filters, original currency, closure-date rule and value policy. All-known-Net populations can match shared strict Net; explicit Gross can match shared Gross. With fallback, strict Net remains incomplete elsewhere. Dashboard EffectiveNet can happen to have identical numeric precedence, but its estimated-Net interpretation and coverage metadata are different; Breakdown does not relabel that product or require a semantic equivalence.

### M20.3 verification and changed files

**M20.3 complete.** Final verification on the unchanged develop HEAD plus these uncommitted changes:

| Check | Result / evidence |
| --- | --- |
| Focused Breakdown Release checks | **94/94 passed**, zero failures/skips: 41 existing/updated contract cases and 53 new calculation/input cases |
| Complete Application Release project | **974/974 passed**, zero failures/skips, including existing Dashboard/Calendar/Daily/Period strict/estimated policies |
| Complete Domain Release project | **490/490 passed**, zero failures/skips |
| Release solution build | **0 warnings / 0 errors** |
| EF consistency | No model changes since the last migration; in-memory design-time factory |
| Whitespace / documentation links | Passed; tracked diff and new C# file checks, 147 local Markdown targets |
| Full WPF/Infrastructure gate, visual/live acceptance | Not run: no Infrastructure, persistence or Desktop behavior was changed in this Application-only step |
| Matching CI | Unverified for these uncommitted changes; no resulting commit or CI acceptance claimed |

Commands: `dotnet test tests/PersonalTradingJournal.Application.Tests/PersonalTradingJournal.Application.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~.Analytics.Breakdown.'` for focused checks; complete Application/Domain project runs use `-c Release --no-build --no-restore` with no filter. `dotnet build PersonalTradingJournal.sln -c Release --no-restore`; EF `migrations has-pending-model-changes` against `JournalDbContext` in Release, no-build. TRX evidence is under ignored `artifacts/m203/focused-complete/`, `artifacts/m203/application/`, `artifacts/m203/domain/`. Focused cases are included in the complete Application count, not additional unique tests.

Development's first focused run was 84 passed / 2 failed: test expectations incorrectly used −18 instead of −16 for a five-Trade average, and expected one `decimal.MinValue` to overflow despite its representability. Those expectations were corrected, genuine positive/negative overflow checks retained, and eight further mode/currency reconciliation cases added before the final 94/94 run. No production calculation was changed to accommodate either mistaken expectation.

Final tests cover per-Trade precedence/zero/sign reversal, fallback labels and count partitions, all seven dimensions in both modes, unequal group sizes, disjoint exact IDs, duplicate names, inactive/missing Setup, closed/open/partial eligibility, currencies, New York date/entry/DST and duration integration, unavailable coverage, decimal precision/overflow, cancellation, duplicates, single-pass input and read-only results. This step adds no Infrastructure/WPF behavior; automated results are not visual/live or matching CI acceptance.

Exact changed files (11):

- `src/PersonalTradingJournal.Application/Analytics/Breakdown/BreakdownQuery.cs`
- `src/PersonalTradingJournal.Application/Analytics/Breakdown/BreakdownTradeFact.cs` (new)
- `src/PersonalTradingJournal.Application/Analytics/Breakdown/BreakdownMetrics.cs` (new)
- `src/PersonalTradingJournal.Application/Analytics/Breakdown/BreakdownMetricCalculator.cs` (new)
- `tests/PersonalTradingJournal.Application.Tests/Analytics/Breakdown/BreakdownQueryTests.cs`
- `tests/PersonalTradingJournal.Application.Tests/Analytics/Breakdown/BreakdownGroupSelectionTests.cs`
- `tests/PersonalTradingJournal.Application.Tests/Analytics/Breakdown/BreakdownMetricCalculatorTests.cs` (new)
- `tests/PersonalTradingJournal.Application.Tests/Analytics/Breakdown/BreakdownInputTests.cs` (new)
- `README.md`
- `docs/breakdown-analytics.md`
- `docs/architecture.md`

M20.4 remains next: one consistent read-only database query supplying this complete projection, batched metadata/coverage, cancellation and explicit size handling. No production-journal access, provider request, migration, UI, branch switch, reset, commit, push or merge.

## M20.2 — filters, grouping and exact selection contracts

Implemented **16 October 2026**, starting clean on `develop`, HEAD `d9320697c3ac669a25890f4699a431913d0d9d6e`. This is the historical M20.2 checkpoint; **M20.3 above supersedes its strict-Net-only mode and future metric obligations**. Grouping, time, currency and exact selection definitions remain in force. SQL, Desktop controls, charts and executed drill-down belong to later M20 milestones.

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
