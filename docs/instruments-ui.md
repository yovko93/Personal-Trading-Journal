# Instruments and action menus

## Behavior — 8 November 2026

The original menu/layout verification started on clean `develop`, `fb0a6157e499074e07cad04315bd8d7c9269b948`; its evidence is retained below. The currency refinement starts on clean `develop`, `dcccf2085865c540bdc4cc5b8c373345ab9ba208`. This follow-up remains local and uncommitted, with no production app startup, journal/database access, commit, push or merge.

Instruments rows show View and a three-dot trigger. Edit is the first menu item, bound to the existing `EditInstrumentCommand` and that row's ID. Opening a different row's menu preserves selection; choosing Edit loads the clicked row through the existing command. Lifecycle/Delete commands and their visibility remain intact. The detail panel retains its Edit button.

All nine table columns align left beneath their headers, including numbers, status and actions. Header and row grids share widths and inset, with 144 DIP for actions. At narrow sizes, a 1120-DIP minimum table scrolls horizontally as one region, keeping headers and rows aligned and all actions reachable. Partial columns at the horizontal viewport edges are intentional. The page retains its vertical scrolling.

## Currency and Tick Value refinement

The list has no separate Currency column. Tick Value uses both row bindings (`TickValue`, `Currency`) in one left-aligned cell, for example **5.00 USD**. Formatting displays at least two fractional digits and retains up to eight, so a value such as **1.23456789 EUR** is not rounded to cents. The former currency-column space is allocated to Tick Value; header and row grids have identical nine-column definitions, with their existing table minimum and horizontal scrolling preserved.

Currency remains in the domain model, persistence, pricing snapshots, details and Add/Edit forms. Only the Desktop Add form's initial/reset value changes to **USD**. Users can override it; cancel, successful creation and navigation reset restore USD for the next new Instrument. Edit continues loading the saved currency and submits it unchanged unless the user edits it. Domain validation, stored currency, pricing snapshots, imports and Trade economics are unchanged; there is no stored-data rewrite or migration.

Focused tests add default/reset, USD/EUR creation and EUR/JPY metadata edits plus an explicit EUR-to-GBP edit. Existing four compiled WPF cases (Light/Dark at 1280/96 and 480/240) now verify nine headers/cells, combined binding paths, exact two/eight-decimal strings, left alignment, reachable pricing/actions at narrow widths, non-USD details/Edit and editable Add currency. Native menu, keyboard, accessibility and hover assertions remain in place. Forms use in-memory fakes; no production data is involved.

Release build: **0 warnings / 0 errors**, `TestResults/instruments-currency-build.log`. Focused checks: **48 top-level passed / 0 failed / 0 skipped**, plus **8 matching isolated children passed** (four Instruments and four Notebook overflow cases), `TestResults/instruments-currency/focused/` and `TestResults/instruments-currency-focused.log`. All six new ViewModel cases pass. The four existing WPF cases add currency assertions without adding new exclusive identities or changing scheduling.

Inspected offscreen renders in `focused/instruments-ui-*/ui-evidence/`: Light/Dark normal tables and narrow pricing viewports at **1280/96** and **480/240** (logical width/render DPI). The combined USD/EUR values and headers align, and scrolling brings the entire pricing cell into view without overlap. The tests also retain left-end/action-end and Add/Edit captures. Render density does not change Windows desktop DPI. Automated native mouse checks pass; no live visual inspection is claimed, and the previously reported Computer Use runtime initialization issue remains unresolved.

One complete Release gate after the focused passes completed with **5,553 passed / 0 failed / 1 skipped**, **exit 0**:

```powershell
pwsh -File scripts/Test-Release.ps1 -ResultsDirectory TestResults/instruments-currency/complete
```

Run: `release-20261108-214839-67fa51433b204bd2a8b61851a90afe52`; console: `TestResults/instruments-currency-complete.log`. The earlier menu/hover results below remain historical evidence.

| Complete stage / project | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| ExclusiveWpf / Desktop | 151 | 0 | 0 |
| Parallel / Desktop | 2,142 | 0 | 1 |
| Domain | 602 | 0 | 0 |
| Application | 1,050 | 0 | 0 |
| Infrastructure | 1,608 | 0 | 0 |
| **Top-level total** | **5,553** | **0** | **1** |

The inventory increases from 5,548 to **5,554** top-level cases solely through the six new ViewModel cases, all passed. The only skip remains `PersonalTradingJournal.Desktop.Tests.Rules.RulesViewTests.CompiledThemesNarrowHighDpiBindingsValidationHistoryAndWindowClose`; its [quarantine and restoration criteria](rules-view-quarantine.md) are unchanged. This does not establish that the quarantined CLR crash is fixed or that its acceptance coverage passed. No new skip, filter, retry, timeout, concurrency or workflow-budget change is made.

The five top-level TRX files were audited independently: zero failed results and no duplicate test-ID/display-name identities across partitions (`TestResults/instruments-currency/complete-audit.json`). Child results are supporting evidence, not added to totals. All four Instruments children pass (`complete-instruments-audit.json`); the four Notebook overflow children also pass. `focused-audit.json` retains the 48-parent/8-child focused results, and `source-evidence.json` records the tested code and assembly hashes under `TestResults/instruments-currency/`. Hosted and live visual verification remain pending.

Exact follow-up files: `src/PersonalTradingJournal.Desktop/ViewModels/Instruments/InstrumentsViewModel.cs`, `src/PersonalTradingJournal.Desktop/Views/Instruments/InstrumentsView.xaml`, `tests/PersonalTradingJournal.Desktop.Tests/Instruments/InstrumentsViewModelTests.cs`, `tests/PersonalTradingJournal.Desktop.Tests/Instruments/InstrumentsViewTests.cs`, `tests/PersonalTradingJournal.Desktop.Tests/Resources/EntityActionResourceTests.cs`, `README.md`, `docs/desktop-ui.md`, `docs/instruments-ui.md`.

## Three-dot inventory

| Screen | Declarations | Implementation |
| --- | ---: | --- |
| Accounts | 2: detail and row | Shared `EntityActionMenu` |
| Instruments | 2: detail and row | Shared `EntityActionMenu` |
| Trades | 2: detail and row | Shared `EntityActionMenu` |
| Trading Setups | 2: detail and row | Shared `EntityActionMenu` |
| Trading Mistakes | 2: detail and row | Shared `EntityActionMenu` |
| Notebook | 2: note and tag template | Separate ownership-aware handlers |

The shared helper now toggles. WPF MenuBase can dismiss a captured menu on outside-down before the underlying button's Click; the helper remembers a press inside that same trigger so release does not reopen it. Later input, menu replacement or unload clears the gesture. Subscriptions are removed on unload/detach. It never forces capture release, executes menu commands itself or queues focus restoration.

Notebook already handles this dismissal ordering in its separate note/tag implementations; those handlers are preserved and their native tests rerun. A source inventory test checks all 12 declarations against the covered implementations. The shared behavior also toggles Notebook's existing template dropdown, preserving its commands.

## Hover states

Both theme dictionaries define `PtjButtonHoverTintBrush` and `PtjButtonHoverBorderBrush`. Standard button templates draw a 16% tint behind content plus an outline, retaining semantic foregrounds instead of fading the whole button on hover. The tint is non-interactive and enabled-only. Pressed/disabled opacity and keyboard-focus indicators remain distinct. Dedicated Notebook/Journal action templates keep their existing palettes.

Native-hover checks cover normal, primary, secondary, danger, entity-action and icon buttons in both themes, requiring at least **4.5:1** text contrast against the composited surface. The existing Journal call-to-action assertion now requires the tint/outline design and contrast, while retaining its original focus, pressed and disabled checks.

## Original menu/hover automated verification

- Release build: **0 warnings / 0 errors**, `TestResults/instruments-ui/build-final.log`. An initial missing test import was corrected before successful execution.
- Broad focused selection: **49 top-level passed / 0 failed / 0 skipped**, plus **10 matching children passed**: four Instruments, four Notebook note-menu and two tag-menu cases (`focused.log`, `focused/`). Children are evidence, not extra top-level cases.
- Instruments cases cover Light/Dark at **1280/96** and **480/240** (logical width/render DPI). They check numerical header/cell alignment for every column, absence of a standalone row Edit button, exact command/row-ID binding, selection preservation, three real native open/close cycles, outside dismissal, WPF Space/Escape/focus, UI Automation invocation, native Edit delivery and clean binding diagnostics.
- New native cases use owned synthetic HWNDs, the existing bounded pointer approach, and unchanged 90-second isolation / 30-second watchdog deadlines. Their class is explicitly added to the independently maintained ExclusiveWpf scheduling set; unrelated tests remain parallel.
- Final Instruments + Journal selection after the hover-contract/capture correction: **5 top-level passed**, plus **5 matching children passed** (`after-hover-contract.log`, `after-hover-contract/`). The earlier four-case render checks also passed; background, fixed viewbox and parent-offset capture corrections produce faithful previews without changing application behavior or assertions.
- Offscreen PNGs were inspected for normal/narrow layouts, both horizontal table ends, Edit menus and representative hover styles. Final images are under `after-hover-contract/instruments-ui-*/ui-evidence/`. They are RenderTargetBitmap previews, not live application screenshots or evidence of changing Windows scaling.

### Complete Release gate

The first attempt, `complete/release-20261108-174101-4ea70ffa3bbf4744bf171feef469c83c`, found one known top-level failure in `JournalViewTests.CompiledEditorBindingsScopeVetoAndLightDarkNormalHighDpiLayouts`. Its child failed `JournalButtonAssertions.CallToAction`: expected chrome opacity **0.88**, actual **1**. This was an obsolete assertion for the intentionally replaced hover design, not a CLR crash, timeout or cancellation. The assertion was replaced with tint, outline and contrast checks; focus/pressed/disabled checks remain. This already-failed invocation was stopped and is **incomplete**, with no final totals. Its failure log and the stopped runner's exact process descendants are retained (`complete.log`, `stopped-run-processes.json`). No unrelated application was closed.

After that correction and passing focused checks, the final complete invocation finished with **5,546 passed / 1 failed / 1 skipped** (5,548 top-level cases), **exit 1**:

```powershell
pwsh -File scripts/Test-Release.ps1 -ResultsDirectory TestResults/instruments-ui/complete-final
```

Retained run: `TestResults/instruments-ui/complete-final/release-20261108-175211-d7a84cfd28b14d4c87c3fcee22e0bc08`; console output: `TestResults/instruments-ui/complete-final.log`. Results were checked against the five top-level TRX files; isolated child results are not counted again.

| Stage / project | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| ExclusiveWpf / Desktop | 150 | 1 | 0 |
| Parallel / Desktop | 2,136 | 0 | 1 |
| Domain | 602 | 0 | 0 |
| Application | 1,050 | 0 | 0 |
| Infrastructure | 1,608 | 0 | 0 |

All four new Instruments parent/child cases and the updated Journal acceptance case passed in this complete gate. The inventory increases by five top-level cases: four Instruments parameters and one three-dot declaration inventory test. The existing Rules quarantine accounts for the single skip; no additional tests are quarantined.

The final invocation has recorded a failure in `NotebookHeaderViewTests.OverflowTriggersToggleThroughNativeMouseAndOwnedKeyboard(theme: "Light", width: 480, dpi: 240)`, child `notebook-menus-20261108-175641-6d446255e04840e9aaf1e161b0ffa4ae`. Menus.cs:103 successfully asserts that the second click closed the menu, then its color assertion expects hover brush **#FF205B55** but reads the normal brush **#FF28655F**. This is not the earlier DuplicateNoteButton readiness failure.

The retained movement trace requests **1096,514**, then records **1121,569** and **1141,597** during/after release. The last processed movement targets `Border#ContentPanel`; managed target-relative position is **40.09,58.67**, outside the 36-DIP trigger, and `IsMouseOver=false`. The normal color is consistent with that recorded state. The source of the additional cursor movement, and any relationship to this change, remain unproven. The same parameter passed in the focused selection. No Notebook assertion, pointer requirement or deadline is relaxed, and no unchanged full gate will be repeated to obtain green. Parent and child describe one failed top-level case.

This is a completed, failing gate, not Release acceptance. No other skip, filter, retry, deadline or workflow-budget change was added. The Notebook failure remains open; a passing focused rerun does not establish a correction or attribution.

Follow-up: the [post-close hover investigation](ci-menu-readiness.md#post-close-hover-precondition--8-november-2026) demonstrates a fixture precondition error with a controlled native pointer departure. The overflow test retains its immediate close assertion and now explicitly checks normal outside and hover inside. The historical gate above is preserved; the follow-up guide records new results separately and leaves the source of the original unrequested movement unknown.

The follow-up complete gate, `release-20261108-203848-e35ead66372f4d249a796e2430c99ff5`, finishes with **5,547 passed / 0 failed / 1 existing Rules skip**, exit **0**. ExclusiveWpf passes **151/151**, including all four Instruments and all four Notebook overflow cases. Focused Notebook/shared-menu coverage passes **34 top-level cases plus 12 matching children**. This is a fixture correction, not evidence identifying the original cursor input producer or completing live visual acceptance.

## Live verification limit

**No live visual inspection is claimed.** The Computer Use runtime failed before app selection/input: `failed to write kernel assets: The system cannot find the path specified. (os error 3)`. Initialization failed again after its reset recovery. No production app was opened.

A standalone synthetic preview for the original menu work was built at `TestResults/instruments-ui/live/bin/Release/net10.0-windows/InstrumentsPreview.exe`, using in-memory test fakes and Light/Dark/normal/narrow controls. It was not launched after the automation runtime failed and has not been rebuilt for the currency refinement; it is not current verification evidence. A later manual check must use a freshly built synthetic preview or an explicitly isolated synthetic app: inspect both themes and widths, scroll the pricing/actions into view, check Add defaults to editable USD, verify non-USD details/Edit, click the same dot trigger twice and inspect hover/focus/keyboard behavior. Automated native input passed; human/live visual acceptance is pending.

## Original menu/layout changed files

- `src/PersonalTradingJournal.Desktop/Interactions/EntityActionMenu.cs`
- `src/PersonalTradingJournal.Desktop/Resources/Controls.xaml`
- `src/PersonalTradingJournal.Desktop/Resources/Themes/LightTheme.xaml`
- `src/PersonalTradingJournal.Desktop/Resources/Themes/DarkTheme.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Instruments/InstrumentsView.xaml`
- `tests/PersonalTradingJournal.Desktop.Tests/Instruments/InstrumentsViewTests.cs` (new)
- `tests/PersonalTradingJournal.Desktop.Tests/Resources/EntityActionResourceTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/ReleaseSchedulingTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalButtonAssertions.cs`
- `README.md`
- `docs/desktop-ui.md`
- `docs/instruments-ui.md` (this record)

Local preview helpers, logs and images under ignored TestResults are verification artifacts. Approved branding, Calendar/Notebook corrections and the Rules quarantine policy are preserved.
