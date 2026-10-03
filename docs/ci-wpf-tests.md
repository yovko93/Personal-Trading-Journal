# Windows WPF test diagnostics

## Test execution and layout contract

Calendar grid tests and modal/chart tests each use a separate supervised test process with named background STA dispatchers. This isolates WPF lifetime from unrelated Desktop tests. Each parent group shares its child-suite result; a child-suite failure therefore appears against multiple parent names. Inspect the child TRX rather than interpreting every parent failure as an independent assertion failure. No tests are skipped, retried, or globally serialized to hide failures.

Grid STA deadlines remain **30 seconds**, modal/chart deadlines **45 seconds**, and both child-process deadlines **two minutes**. Successful and failed grid actions shut down their dispatcher before completing. The native suite preserves its existing process-owned renderer lifetime: per-case compositor teardown was measured to repeatedly slow native Window/bitmap initialization and exceed the whole-suite budget despite continuous progress. Its final native cleanup belongs to the supervised child process, not an unbounded foreground STA. A timed-out child refuses to start more STA threads, rather than accumulating stuck work. Since managed cancellation cannot safely abort a native WPF call, the supervisor terminates the child process tree and awaits exit on deadline, cancellation, or output/logging failure. The child output is streamed to artifacts immediately; complete results are not held until process exit. A native-thread snapshot is not a managed stack. VSTest's same-deadline mini-dump collector provides best-effort stack evidence; the STA and dump watchdogs can race, so a dump is not guaranteed.

Native window layout uses the actual measured viewport. Windows can constrain a requested window width to the available desktop. Below the table's 1,048-DIP minimum, horizontal scrolling is required and Action/View must remain reachable; when the viewport fits the table, it must fill the viewport without overflow or a trailing gap. Separate measure/arrange tests of the compiled view exercise 480-, 1,044-, 1,280- and 1,900-DIP widths without native desktop constraints, including expanded content and both themes.

Account remains content-sized with its 100-DIP minimum and 160-DIP cap. Tests compare the column with measured header/row content demand (within one physical pixel of layout rounding), not a mandatory 160-DIP width. Display-mode text/ellipsis metrics depend on the actual host DPI. Net remains 150 DIPs; headers, values, full-text tooltips, flexible Setup/Mistakes columns, expanded View content and horizontal access are still checked.

## CI evidence: run 69

The complete job output was retrieved for [CI run 69](https://github.com/yovko93/Personal-Trading-Journal/actions/runs/37152240811), validate job `111288323579`, attempt 1 on 2026-10-03. The runner was Windows Server 2025, image `windows-2025-vs2026` version `20260925.250.1`.

- PR head/local baseline: `6abfef9691fc0e0d2ed038dd760046e5502f1b7a`.
- Actual PR merge checkout: `7b3067feadb9d334484824be756391d0c4067d7a`.
- Both have the same tree: `c5dc99348b3c0256287fcead3f16b5db9b468807`. CI tested the same source content, despite the different merge SHA.
- Domain 400, Application 516 and Infrastructure 719 passed. Desktop reported 69 failed / 793 passed / 862 total.
- The shared native suite actually failed 14 of 70 cases. `ViewExpandsExactTradeWhileKeepingModalOpen` passed in that child suite; its parent failure repeated the shared suite's failure.

| Actual failing group | Cases | First assertion | Cause |
| --- | ---: | --- | --- |
| `InitialOwnedPanelFitsTableAtDesktopWidthAndWrapsWithoutLosingAction`, long Account, both themes, 480/1,280/1,900 requested widths | 6 | `Assert.InRange`, expected 159–160, actual 155 | Content-sized text was incorrectly required to fill its cap; 155 DIPs is valid measured demand at 96 DPI. |
| Same test, short Account, both themes, 1,280/1,900 requested widths | 4 | `Assert.Equal`, expected horizontal overflow 0, actual 121 | Requested owner width was mistaken for the actual table viewport. |
| `ResponsiveColumnsCenterNetFillViewportAndKeepAccountStableWithExpandedRows`, both Account lengths/themes, 1,900 requested width | 4 | `Assert.Equal`, expected horizontal overflow 0, actual 47 | Same viewport assumption in the responsive-window test. |

These failures were reproduced locally before changing the assertions: a process-local 96-DPI context reproduced the six Account assertions, and constraining test window width to 1,044 DIPs reproduced all 14 failures with the exact 155/121/47 measurements. The measured owned-modal viewport was 927 DIPs and responsive-window viewport 1,001 DIPs, against a 1,048-DIP table minimum. This reproduces the failure mechanism; it is not a recreation of the hosted Server VM. The original job did not record its actual display DPI/viewport, so those runner measurements are inferred from the reproduction, not directly observed in CI.

No Calendar product defect, dispatcher exception, resource-lifetime failure, or parallel-ordering failure was evidenced by these 14 assertions. The fix changes tests and diagnostics only, not product layout or economics.

## Local verification of the run-69 correction (2026-10-04)

| Check | Result |
| --- | --- |
| Before assertion correction, 96 DPI / 1,044-DIP window constraint | 14 failed / 10 passed, reproducing all three CI assertion groups with matching values. |
| Corrected affected theories plus eight new detached-layout cases, same constraints | 32/32 passed on each of three independent runs; no retries or skips. |
| Entire native modal/chart suite, same constraints | 78/78 passed. |
| Complete Release suite, normal local 240-DPI host | 2,505/2,505 passed: Domain 400, Application 516, Infrastructure 719, Desktop 870; child native suite 78/78. |
| Complete Release suite, 96 DPI / 1,044-DIP window constraint, through the normal shared child-process gate | Same 2,505/2,505 and child 78/78 passed. |
| Solution Release build / `git diff --check` | Passed, zero build warnings/errors. |
| New GitHub CI run of the correction | Pending the user's commit/push; not verified by local results. |

Complete downloaded run-69 output and local TRX/layout evidence are retained locally under ignored `bin/ci-investigation/`, not tracked documentation or customer fixtures. Native test execution is automated WPF evidence, not manual application acceptance. No real journal was opened.

## CI evidence: run 70

[Run 70](https://github.com/yovko93/Personal-Trading-Journal/actions/runs/37155738271), validate job `111298629988`, was cancelled by the user after its Test step hung. The always-run artifact upload succeeded. Its complete log and ZIP were retrieved, not just the two failures shown in the question.

- PR head/local baseline: `8ec38859d0c9ccb87af2ce24989d5b808cb98f19`.
- Actual merge checkout: `9af1b467fe65da48469d548521075d5bbc8018a8`.
- Both trees: `07fc25a23cbb6d7f553742aa0f0699f0d9d04087`; the source content matches.
- Windows Server 2025, image `windows-2025-vs2026`, version `20260925.250.1`; SDK 10.0.303, runtime 10.0.12. Local SDK/runtime match, but the local Windows 10 desktop is not the hosted Server VM.
- Test ran from 21:38:26 until cancellation at 22:05:19 UTC on 2026-10-03. It did not complete naturally. The artifact contains only passing Domain **400**, Application **516**, and Infrastructure **719** TRX files; no Desktop TRX, child diagnostics or stack dump was available.
- **All 36** `CalendarViewLayoutTests` cases timed out, each with `System.TimeoutException: The operation has timed out.` at the outer `OnSta` await. No distinct inner assertion or WPF exception was reported. The final timeout at 21:56:32 was followed by approximately 8 minutes 46 seconds of silence before cancellation.

| Timed-out method group | Cases |
| --- | ---: |
| `CompiledGridKeepsSevenAlignedColumnsAndFocusableDaysAcrossThemesAndSizes` | 4 |
| `DaySelectionByKeyboardAndMouseRendersResponsiveDetailsAndExactViewTargets` | 4 |
| `OutcomeHoverRestoresBackgroundWithoutChangingSelectionMarkersOrTooltipTargets` | 4 |
| `MonthlyHeaderAndCenteredMarkersKeepTodaySelectionAndFocusDistinct` | 8 |
| `RefreshPreservesDateContainersAndAutomationAndMonthWheelRouting` | 4 |
| `FilterControlsUseSharedThemeKeyboardNamesBindingAndWrapAboveGrid` | 4 |
| `HitTestingScopesDateHoverToMarkerNotDailyWeeklyOrEmptyCellAreas` | 4 |
| `PopulatedGridRendersSignsAndCenteredWeeklyOnlySaturdayAcrossThemes` | 4 |

### Findings and correction

The original harness started a foreground STA and timed out only its awaiting task. It neither stopped that thread nor shut down its dispatcher. A blocked STA could survive each failed case and keep the process alive; this is a confirmed harness defect, not evidence of a Calendar product defect. The previous modal/chart child supervisor also disposed its process handle without terminating a timed-out child. Both now use the independently tested process-tree cleanup boundary described above.

Full-suite testing exposed another demonstrated lifetime problem: `TopstepDesktopRoutingTests.CompiledImportViewLoadsRealResourcesAndMaterializesTopstepReviewControls` creates and shuts down a process-wide Application. When it precedes the Dashboard's real-Popup placement tests, those tests can fail with a null `PresentationSource` / `Visual is not connected to a PresentationSource`. A forced-order isolated regression reproduced the null-source failure before the correction. The Import resource test now runs unchanged resource/UI assertions in its own supervised child, preserving its 30-second STA deadline; its Application shutdown no longer contaminates the parent. The regression then exercises both popup placement cases and requires no Application retained in their process. This proves the popup lifetime defect, **not** the original Calendar blocking call.

An initial cleanup implementation shut down every native test dispatcher. That introduced steady per-case initialization overhead, rather than an individual case hang: only 47 native cases completed before the original two-minute suite deadline. The 12 responsive-column actions averaged **2.312 seconds**; preserving the original process-owned native renderer lifetime reduced the comparison to **0.244 seconds**, and all **78** native cases passed within the unchanged deadline. Native windows still close in their test finally blocks; background STA, poison detection and process-tree cleanup remain enforced. A policy regression protects the intentional child-owned lifetime without relying on timing assertions.

The **original stalled WPF operation remains unproven**: the old log cannot distinguish construction, resource loading, layout or rendering. The representative grid theory now records start/end breadcrumbs for ViewModel construction, every navigation phase, view construction, DataContext assignment, pack resources, detached styles, Measure, Arrange, UpdateLayout, bitmap allocation, `RenderTargetBitmap.Render`, assertions and dispatcher cleanup. Month navigation uses a finite number of steps with progress assertions instead of an unbounded while loop; no navigation failure was observed. Logs also identify the STA, Application dispatcher lifetime, loaded runtime/WPF versions, logical CPU count, interactive status, window station and desktop. No test assertion or four-/five-/six-row coverage was removed.

Before isolation, all four representative cases passed locally at 96 DPI with two logical CPUs, after a controlled Application shutdown, with a deliberately nonpumping Application thread, and after the actual compiled Import-view routing test. They also passed on a unique hidden desktop in the existing interactive window station. A separate private noninteractive window-station probe was initially sandbox-denied (Win32 error 5); the approved out-of-sandbox test-only helper succeeded with `WSF_VISIBLE=False` and `UserInteractive=False`, and all four instrumented cases passed. Its bounded job/process/desktop/station handles were closed, without a desktop switch, permission or display-setting change. These negative probes do **not** rule out a CI-only lifecycle or rendering failure and do not recreate Windows Server 2025. The noninteractive probe used the corrected harness, whereas the hidden-desktop probe used the old dispatcher-cleanup behavior.

This correction contains the demonstrated lifetime failure and adds diagnostics for any recurring original stall. It must not be described as a proven fix of the original native blocking call or as a green GitHub CI result.

### Local follow-up verification (2026-10-04)

| Check | Result |
| --- | --- |
| Representative four-/five-/six-row grid theory, hidden desktop / 96 DPI / two logical CPUs | 4/4 passed; the original cleanup behavior was retained for this diagnostic probe. |
| Representative grid theory, private noninteractive window station | 4/4 passed; `UserInteractive=False`, `WSF_VISIBLE=False`. |
| All grid layout cases, three independent constrained runs | 36/36 passed on each run; no retries or skips. |
| Forced Import-Application shutdown then native Popup, before isolation | Failed at the existing null-PresentationSource assertion, reproducing the full-suite ordering problem. |
| Same forced order after Import resource isolation | Passed, including both existing real-Popup placement assertions. |
| Focused grid, harness/process supervision, Import resource and tooltip regressions | 65/65 passed. |
| Complete Desktop suite, 96 DPI / two logical CPUs / 1,044-DIP native window constraint | 883/883 passed; child grid 36/36 and native modal/chart 78/78. |
| Full Release suite, normal local 240-DPI host | 2,518/2,518 passed: Domain 400, Application 516, Infrastructure 719, Desktop 883; child grid 36/36 and modal/chart 78/78. |
| Solution Release build / `git diff --check` | Passed, zero build warnings/errors. |
| New GitHub run of this timeout/lifetime correction | Not run; requires the user's commit/push. The original blocking phase remains unverified. |

The 13 added cases cover cleanup, the blocked-phase deadline with a manually triggered TimeProvider (not a cold-start speed assumption), poisoned-host refusal, preserved action exceptions, native lifetime policy, forced Application ordering, child-tree cancellation/timeouts, ordinary exits, and diagnostic I/O failures. Ignored local evidence is under `bin/ci-timeouts/` and `bin/ci-investigation/`. No production code, stored economics, real journal, assertion weakening, retries or skips were involved.

## Reproduce without changing Windows display settings

Use a fresh PowerShell process in the repository on Windows:

```powershell
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj --configuration Release
$env:PTJ_CALENDAR_MODAL_TEST_HOST = '1'
$env:__COMPAT_LAYER = 'DPIUNAWARE'
$env:PTJ_CALENDAR_TEST_MAX_WINDOW_WIDTH = '1044'
$env:PTJ_TEST_RESULTS_DIRECTORY = Join-Path (Get-Location) 'TestResults/constrained-calendar'
dotnet vstest tests/PersonalTradingJournal.Desktop.Tests/bin/Release/net10.0-windows/PersonalTradingJournal.Desktop.Tests.dll '/TestCaseFilter:FullyQualifiedName~CalendarDayModalTests|FullyQualifiedName~CalendarDayPerformanceChartTests' '/Logger:trx;LogFileName=constrained-calendar.trx' '/ResultsDirectory:TestResults/constrained-calendar'
```

The direct-host variable runs the actual native cases instead of the parent gate. The compatibility setting applies only to child processes from this shell; it does not change system scaling. The test-only width constraint applies to the two affected native-window theories. Inspect `calendar-layout.txt` to confirm actual DPI and viewport. Close this shell afterwards; ordinary full-suite runs should not inherit these reproduction settings. Build-output paths above use the normal project output; an `--artifacts-path` build requires its corresponding assembly path instead.

## Artifacts and verification gate

The workflow sets `PTJ_TEST_RESULTS_DIRECTORY`, enables TRX and VSTest diagnostic logging, and uploads `windows-test-results` even after a test failure. Unique `calendar-grid-*` and `calendar-native-*` folders contain the child TRX, `stdout.log`, `stderr.log`, `process-supervision.log`, `calendar-sta-phases.log`, any dump/sequence files, and `calendar-layout.txt` for native cases (actual screen/work area, window, DPI, viewport, extent and columns). Synthetic diagnostics do not log Account names, Trade IDs or customer rows. The complete child output remains in artifacts; parent exceptions contain only a bounded tail and its location.

The outer Test step has a three-minute per-case VSTest hang collector and a ten-minute step bound so an unrelated stuck test can produce diagnostics and reach artifact upload without waiting indefinitely. These are fallback safety limits, not an increase of the original 30-/45-second Calendar deadlines and not retries. A collected mini dump may provide a stack where available; phase logs remain useful if dump collection fails or the host exits first.

A passing local reproduction/full suite is not a passing GitHub run. The user must commit/push these changes to the PR branch, let a new Windows CI run test that new head/merge tree, and verify all project suites plus **36 grid** and **78 modal/chart** child cases. If it fails, inspect the first unfinished phase and its dump before attributing the original cause. Rerunning run 70 would only retest the old tree.
