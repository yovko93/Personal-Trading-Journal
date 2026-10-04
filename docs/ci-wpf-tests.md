# Windows WPF test diagnostics

## Test execution and layout contract

Calendar grid tests and modal/chart tests each use a separate supervised test process with named background STA dispatchers. Grid cases own and shut down their dispatchers. Native modal/chart cases share one background STA and a running dispatcher for the child lifetime. A gate outside that dispatcher admits one native action at a time, so a nested ShowDialog/PushFrame cannot execute another case. Other test collections and assemblies remain parallel. Each parent group shares its child-suite result; a child-suite failure therefore appears against multiple parent names. Inspect the child TRX rather than interpreting every parent failure as an independent assertion failure. No tests are skipped, retried, or globally serialized to hide failures.

Grid STA deadlines remain **30 seconds**, modal/chart deadlines **45 seconds**, and both child-process deadlines **two minutes**. Successful and failed grid actions shut down their dispatcher before completing. The native suite retains one process-owned renderer: repeated teardown was expensive, while creating a new unshutdown dispatcher for every case accumulated native resources. Reusing its STA avoids both costs. Final native cleanup belongs to the supervised child process. A timeout poisons the host, cancels queued actions and refuses further dispatch. Since managed cancellation cannot safely abort a native WPF call, the supervisor terminates the child process tree and awaits exit on deadline, cancellation, or output/logging failure. The child output is streamed to artifacts immediately; complete results are not held until process exit. A native-thread snapshot is not a managed stack. VSTest's same-deadline mini-dump collector provides best-effort stack evidence; the STA and dump watchdogs can race, so a dump is not guaranteed.

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

## CI evidence: run 71 and deterministic synthetic probes

[Run 71](https://github.com/yovko93/Personal-Trading-Journal/actions/runs/37160098649), validate job `111311587429`, completed its Test step and uploaded `windows-test-results`. The complete job log and artifact ZIP were inspected.

- PR head/local baseline: `552d1f470494769c855268f4e7f23af37e965316`.
- Actual merge checkout: `3dfe6ace5f360d27ee9772570bf807261bbe30c2`; both trees are `cf1d29616ded7b301c0564f933eae706f7ef53bf`, so CI tested the same source content.
- Windows Server 2025, image `windows-2025-vs2026`, version `20260925.250.1`.
- Domain **400**, Application **516**, Infrastructure **719** passed; Desktop **882/883** passed. Child Calendar grid **36/36** and native modal/chart **78/78** passed, without the previous timeouts.
- The sole failure was `IsolatedTestProcessTests.NormalAndFailedExitPreserveCompleteOutput(exitCode: 7)`, an `IsolatedProcessTimeoutException` at its unchanged **15-second** deadline. PID 10264 started at 22:59:00.471 UTC, timed out at 22:59:15.469 and was confirmed exited at 22:59:15.486. Its stdout/stderr files each contained only the three-byte UTF-8 BOM, **no probe output**. The exit-code-0 PowerShell probe then passed but took **11.07 seconds** between start and exit.

The demonstrated problem is a runner-sensitive scripting-host dependency in a test meant to check output and exit handling, not a supervisor cleanup failure or Calendar failure. Even with `-NoProfile -NonInteractive`, the command must initialize Windows PowerShell before reaching its first console write. The timeout snapshot contains native wait states only; no PowerShell managed stack was collected. The evidence does **not** establish the particular internal startup wait, Defender activity, or CPU contention as the cause. No such speculation is treated as a finding.

`PersonalTradingJournal.TestProcessProbe` is now a minimal prebuilt `net10.0-windows` console executable with no WPF, production services or package dependencies. Its normal executable project reference deploys the apphost, assembly, dependency manifest and runtime configuration alongside Desktop.Tests, including custom artifacts-path builds. Tests launch that apphost directly, never a shell, `dotnet run`, or an on-demand compiler.

- Output mode emits deterministic UTF-8 stdout and stderr, each over 128 KiB, then exits with exactly 0 or 7. Assertions compare **complete** artifact content and the supervisor's exact bounded returned tails, including the final Unicode marker and process exit. This tests multiple pump reads and data buffered at exit rather than only one line.
- Blocked mode uses a real foreground STA and a real descendant of the same executable. Its readiness handshake precedes the flushed `FOREGROUND STA` marker, so deadline, cancellation and injected output-write-failure tests prove they reached the intended blocked phase. This also removes PowerShell startup and `Add-Type` compilation from those tests.
- A descendant parent-exit backstop is delayed **30 seconds**, longer than the tests' five-second descendant cleanup assertion. A separate parent-only-kill negative control verifies that it cannot falsely satisfy tree cleanup; its finally block explicitly terminates both synthetic processes. The delay only bounds a possible immediate-start/kill spawn race; it is not a retry or an extended supervisor deadline.
- The already-cancelled test uses the same probe but still starts no process. The first-log-write and output-pump fault tests retain the original error and cleanup assertions. The supervisor itself, Calendar deadlines and CI watchdogs are unchanged. No tests are skipped or retried.

The run-71 failure was not reproduced locally as a PowerShell internal hang. Local constrained checks exercise the replacement and its actual output/cleanup invariants; they do not recreate the hosted Server VM or prove GitHub is green. A new run after the user's commit/push must pass all suites and the revised synthetic probes. The original run-70 inner blocking operation remains unidentified, although run 71's complete Calendar child results show it did not recur there.

### Separate local composite-host budget finding

The first complete Release verification of the probe replacement found one **different** failure: the Application-lifetime regression's outer process reached its 30-second budget during VSTest completion, although its child TRX already reported **Passed**. The regression action took **25.892 seconds**, including a nested Import process taking **21.976 seconds**; that Import's STA action and dispatcher cleanup completed in **3.264 seconds**. Including outer discovery, startup and exit, the parent confirmed termination after **32.53 seconds**. Both existing real-Popup assertions had passed. This is direct evidence of a composite process-budget mismatch, not a blocked Calendar or failed assertion, and not the run-71 PowerShell failure.

Only `ImportResourceApplicationCannotPoisonLaterNativeTooltipWindows` now uses the existing **two-minute suite/process budget**, because it supervises another test host and then two native Popup checks. The other synthetic STA processes retain 30 seconds, the Import action retains its 30-second STA deadline, and the console output/blocked probes retain 15/20 seconds. The nested Import isolation and its unchanged resource assertions are preserved; bypassing that isolation would recreate process-wide Application poisoning. No blanket timeout increase or retry was applied. The first failed full-suite TRX is retained alongside subsequent verification evidence rather than discarded.

### Local verification of the run-71 correction (2026-10-04)

| Check | Result |
| --- | --- |
| Final eight process probes plus composite Import/Popup regression, single-CPU process affinity, two reported .NET CPUs, process-local 96 DPI | **9/9** on each of three independent runs; stop-on-failure, no retries/skips. |
| Exit-code-0/7 output theory, ten additional independent single-CPU runs | **20/20** passed; case durations **0.247–1.289 seconds**. Complete stdout/stderr and exact bounded tails matched. |
| Complete Desktop suite after both corrections, two .NET CPUs / 96 DPI / 1,044-DIP native-window constraint | **884/884** passed; child grid **36/36**, modal/chart **78/78**. No synthetic probe processes remained. |
| Final full Release suite, normal local host and ordinary solution output | Domain **400/400**, Application **516/516**, Desktop **884/884** passed; Infrastructure **718/719**. **2,518 passed, one failed**; this is not a green full-suite result. |
| Separate failing Infrastructure case, unchanged isolated diagnostic execution | **1/1 passed**; not a replacement for the failed full-suite result or proof of a fix. |
| Solution Release builds, ordinary and custom artifacts paths | Passed, zero warnings/errors; probe apphost/dependency/runtime files deployed in both outputs. |
| `git diff --check` | Passed. |
| New GitHub CI run | **Pending the user's commit/push.** Local results do not establish a green CI run. |

The final full-suite exception was `ObjectDisposedException: SQLitePCL.sqlite3` in the unchanged Infrastructure test `TradeBrowseProjectionTests.SaveAsyncRegeneratesProjectionForCloseAndCorrectionOnlyForTargetTrade`, while opening a connection in `TradeMutationStore.GetByIdAsync`. Infrastructure passed all 719 cases in run 71 and the earlier local full run. The isolated case subsequently passed unchanged. Existing parallel Infrastructure fixtures use process-wide `ClearAllPools()` cleanup with default pooled connections; this is a plausible independent lifetime hazard, **not a proven cause**. No Infrastructure or production code was changed, and this transient failure is not claimed fixed. Its full-suite TRX and the isolated diagnostic result remain separate under ignored `bin/ci-probe-investigation/` for follow-up. No customer data or real journal was accessed.

The subsequent [SQLite lifetime investigation](sqlite-test-lifetimes.md) demonstrates the cross-fixture pool ownership defect and a controlled provider activation race, and replaces global cleanup with owned-pool release. Its verification is recorded separately from the historical run-71/probe results above.

## CI evidence: run 73 — ambient pointer and probe encoding

[Run 73](https://github.com/yovko93/Personal-Trading-Journal/actions/runs/37196768021), validate job `111420178931`, completed the Test step with failure and successfully uploaded its diagnostic ZIP. The full log and TRX files were inspected, not just the propagated Desktop messages.

- PR #16 head/local baseline: `b9554eaf0e326906a8e0d7be5acb6b7e64b0bf6f`, `develop`, initially clean.
- CI merge checkout: `30c3c35cdb3acfa88b94a5e4bcdb4aee78fe6cbb`. Both share tree `291f89dfa8e8ff31f173af87e09530575318c8b4`; source content matches.
- Windows Server 2025, image `windows-2025-vs2026` version `20260925.250.1`, SDK 10.0.303 / runtime 10.0.12.
- Domain **400/400**, Application **516/516**, Infrastructure **724/724**, child Calendar grid **36/36** passed. No new SQLite failure exists and that fix is unchanged.
- Native Calendar suite: **77 passed / 1 failed**. Its first and only assertion failure is `CalendarDayPerformanceChartTests.FullHeightRegionsHandOffOneGuideAndRetainKeyboardFocusWithSubtleThemeDrawing("Dark", 960, 96)`, original line **68**: expected `Collapsed`, actual `Visible`.
- Parent Desktop: **805 passed / 79 failed**. **77** failures repeat the same native-suite result. The other **two** are independent output-probe encoding assertions (exit codes 0 and 7), not Calendar failures.

### Guide initial-state assumption

The failing test creates a real native Window, calls `Show`, pumps the dispatcher, and draws the chart before asserting there is no active guide. The chart correctly handles native region `MouseEnter` immediately. Its instance-owned hovered/focused target determines guide visibility; redraw preserves an active point by identity. A fresh chart cannot inherit another chart's target. Thus the test incorrectly assumes no ambient pointer interaction while it is showing/pumping the window.

Temporary diagnostic instrumentation reproduced the exact assertion using native `WM_MOUSEMOVE` input delivered to the test window at a measured plot coordinate, before the initial assertion. All four theme/size cases failed the original `Collapsed` assertion. The Dark 960/96 case recorded:

```text
REGION MouseEnter: guide=Visible; focus=System.Windows.Window
guide=Visible; mouseOver=True; pointer=480,100
regions=0:False,1:False,2:True,3:False
```

This is legitimate hover, not retained stale state or automatic keyboard focus. The CI artifact did not record its physical cursor coordinates, so the precise original cursor location is not claimed observed. On the local hidden sandbox desktop, passive window positioning alone did not reproduce entry; controlled native input did. Windows/WPF can ignore synthetic native movement when the real cursor is outside its HWND; diagnostic positioning placed only the synthetic test window beneath the stationary cursor, without moving the machine's cursor. This reproduces the environmental-input mechanism, not the entire hosted VM.

The correction sets `IsHitTestVisible=false` on **this scripted test's Window only**, before showing it, and explicitly focuses the button outside the chart. It asserts no hover/focus before the unchanged initial guide check. Isolation lasts through all later dispatcher pumps, not just a one-time state reset. Every existing region-geometry, adjacent-boundary, guide position, highlight, delayed-leave, zero-point, tooltip, resize, real keyboard-focus and clearing assertion remains. Raw `VisualTreeHelper.HitTest` still tests region geometry; routed events explicitly drive the interaction under test. No product event handler, state behavior, assertion, case or deadline was removed or weakened.

Temporary native-message diagnostics are not retained as ordinary CI tests: adding a native-pointer-location assumption to them would recreate the problem. Their logs/TRX remain under ignored `bin/ci-73-investigation/`. The permanent interaction coverage remains the original four deterministic theory cases, not live pointer acceptance.

### Independent UTF-8 probe failure

Both `IsolatedTestProcessTests.NormalAndFailedExitPreserveCompleteOutput` cases failed at line **20**, comparing the complete stdout artifact: expected `complete stdout Ω`, actual `complete stdout ╬⌐`. The stderr artifacts have the same corruption. The processes exited normally with the correct codes in approximately **55 ms** (7) and **63 ms** (0); this is not the earlier PowerShell startup timeout.

The console probe emits UTF-8 but its `ProcessStartInfo` had unspecified stdout/stderr encodings. UTF-8 bytes `CE A9` for `Ω` decoded as OEM437 produce the exact `╬⌐` corruption. A separate diagnostic with explicit decoders reproduced it on both streams at exit codes 0 and 7; UTF-8 decoding preserved the complete marker and exit codes. It changed no global console settings.

Only the test's probe factory now declares both UTF-8 decoders. The existing theory asserts those declarations before its unchanged exact full-output, bounded-tail, exit and cleanup checks. The direct-launch parent-only-kill negative control also redirects stderr, as required for an explicit stderr decoder. The generic process supervisor still honors each caller's chosen encoding. No production code, SQLite fixtures, skipped tests, retries, sleeps, global serialization or blanket deadline changes are involved.

### Local verification

| Check (2026-10-04) | Result |
| --- | --- |
| Controlled native entry before correction | **4/4 reproduced** the original initial-guide assertion, with MouseEnter and Window-only keyboard focus recorded. This is diagnostic reproduction, not a passing acceptance run. |
| Corrected four chart theory cases plus eight process probes, hidden sandbox desktop / process-local 96 DPI / two .NET CPUs | **12/12 passed**. Chart bitmap rendering covers 96/240 DPI; this does not claim a native 240-DPI window in the constrained run. |
| Complete isolated Calendar native suite, same constrained host plus 1,044-DIP native-window limit | **78/78 passed**, no skips. |
| Full Release suite, ordinary local host | **2,524/2,524 passed**: Domain **400**, Application **516**, Infrastructure **724**, Desktop **884**. Child grid **36/36** and native Calendar **78/78** passed. |
| Solution Release build / `git diff --check` | Passed; **zero build warnings/errors**. No synthetic probe processes remained after the suite. |
| Live application pointer interaction | Not performed. WPF interaction/render checks above are automated and use synthetic data only. |
| New GitHub run | **Pending the user's commit/push.** Local passing results are not a green CI run. |

The downloaded run-73 evidence, before-correction diagnostic TRX, encoding probe output and final local TRX/logs are under ignored `bin/ci-73-investigation/`. No customer data or real journal was accessed. Only two test files, README and this investigation document changed; production code and the SQLite ownership correction are untouched.

## CI evidence: run 74 — native tooltip readiness

[Run 74](https://github.com/yovko93/Personal-Trading-Journal/actions/runs/37202250659), validate job `111436164468`, completed its Test step and uploaded the complete TRX/diagnostic artifact. PR head/local baseline was `036957ac7c4fc7306a0f7cbadcbf298321af231b`, `develop`, with a clean worktree. The CI merge checkout `6b401dfaf1f2831c029096f0e3d1e1d4ae7ac771` has the same tree, `c257c0161b2fc3757d81f11268a6c9e4450a0d8a`.

Domain **400/400**, Application **516/516**, Infrastructure **724/724**, Calendar grid **36/36**, and native Calendar **78/78** passed. Desktop was **883 passed / 1 failed**. The sole failure was `ChartTooltipFollowerTests.OpenPopupFlipsBesidePointerNearWindowRightAndBottomEdges`: `AssertPopupNearExpected`, original line **285**, threw `InvalidOperationException: This Visual is not connected to a PresentationSource` at `tip.PointToScreen`. The immediately preceding `root.PointToScreen` succeeded. The run-73 chart/probe corrections passed; neither those fixes nor SQLite are changed here.

### Lifecycle evidence and limits

The original test called `tip.IsOpen = true`, drained the dispatcher to `ApplicationIdle`, then measured. The scrolling placement test used the same idle drain. Neither sequence guaranteed the tooltip remained open at measurement. Production `ChartTooltipFollower` sets offsets and follows pointer/focus; it does not close a hover popup. The [WPF Popup implementation](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/Primitives/Popup.cs) queues app-deactivation handling for `WM_ACTIVATEAPP(false)`; [ToolTip's popup-close handler](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/ToolTip.cs) sets `IsOpen=false`. Other concurrent test processes create native windows, so draining unrelated native events is not an inert delay.

A local baseline trace at process-local 96 DPI opened synchronously: `Opened=1`, `Closed=0`, `IsOpen=True`, `Source=HwndSource`, 100×80 DIPs, target `Window`, owner `MouseOver=False`. The unmodified placement assertion passed. Controlled native app-deactivation input to that **synthetic tooltip HWND**, followed by event-coordinated completion of its close, reproduced the exact disconnected-visual exception:

```text
Opened: IsOpen=True; source=HwndSource; opened=1; closed=0
Closed: IsOpen=False; source=null; opened=1; closed=1
before measurement: IsOpen=False; source=null; rootSource=HwndSource
target=Window; window=Normal/True/True; mouseOver=False; size=100x80
```

An earlier probe also caught the closing-animation interval: `IsOpen=False` while the source was still attached. Thus a non-null source alone is not sufficient. Geometry was unchanged and the root remained connected; this reproduced closure, not a coordinate conversion failure, slow opening, or pointer hover. It did not switch applications or move the user's pointer.

**The original CI artifact did not record Opened/Closed, IsOpen or activation state.** This controlled reproduction demonstrates the test's lifetime assumption and the same failure mechanism, not proof of the precise original hosted-runner event sequence. No product tooltip defect was demonstrated. Future failures now carry the missing evidence rather than only a disconnected-visual stack.

### Test-only correction

`TooltipPopupReadiness` subscribes before opening to Opened/Closed, IsOpen changes, presentation-source changes, load/layout, window activation/closure and owner hover. It waits for an observed Opened event, `IsOpen=true`, a connected popup, and valid positive layout. The existing physical-pixel assertions then execute in the same STA operation without a generic idle drain in between. The follower's synchronous offset changes are measured the same way after pointer movement and horizontal scrolling.

Deferred readiness is coordinated by dispatcher/events with one **five-second deadline**, not sleeps or polling. A closed/closing popup fails explicitly; a never-opened popup times out with state; neither is reopened or retried. Handler, queued-check and timer cleanup is scoped. Existing five-physical-pixel X/Y tolerances, edge calculations, test cases, process/CI deadlines and production code are unchanged.

These native placement windows also exclude ambient pointer hit testing: their pointer coordinates are scripted, while their HWNDs, popup HWNDs, screen coordinates and DPI conversion remain real. This prevents an unsolicited tooltip-service opening from defeating the deliberately deferred-opening regression. It does not suppress native app-deactivation messages, as the regression still verifies. No production hit targets change.

The observer preserves a bounded lifecycle history and reports IsOpen, presentation sources, Opened/Closed counts, placement target/offsets, measured dimensions, window bounds/state/activation, pointer hover and DPI/work area. Measurements and failures write stdout/TRX and optional `chart-popup-readiness.log`. No customer content is logged.

Three regressions exercise deferred opening beyond a single idle pump; measurement before an unrelated queued native deactivation followed by diagnostic rejection of the closing popup; and a never-opened popup's immediate test deadline without measuring/retrying. Controlled deactivation is confined to the test HWND. The zero-duration test deadline exercises failure cleanup without waiting five seconds; normal placement checks use the bounded default.

### Local verification

| Local check (2026-10-04) | Result |
| --- | --- |
| Instrumented original edge test, hidden desktop / process-local 96 DPI / two .NET CPUs | Passed; Opened and source were ready synchronously, no hover or closure recorded before measurement. |
| Controlled native deactivation then Closed, before correction | Reproduced the exact disconnected-visual exception with the root still connected. This is negative diagnostic evidence, not a passing run. |
| Final tooltip suite, three independent constrained runs | **18/18 passed on each run**, stopping on failure rather than retrying. Includes the original edge/scrolling checks and three readiness regressions. |
| Final complete Desktop suite, 96 DPI / two .NET CPUs / 1,044-DIP Calendar window constraint | **887/887 passed**, child grid **36/36**, native Calendar **78/78**. |
| Final full Release suite, ordinary local host | **2,527/2,527 passed**: Domain **400**, Application **516**, Infrastructure **724**, Desktop **887**; child grid **36/36**, native Calendar **78/78**. Popup diagnostics confirm native **240 DPI** (`TransformToDevice=2.5`) in this run, versus **96 DPI** in the constrained runs. |
| Release solution build / `git diff --check` | Passed; zero build warnings/errors. |
| New GitHub CI run / live application acceptance | **Not performed.** The user must commit/push and inspect a new run; automated native WPF checks are not live application verification. |

Evidence is retained under ignored `bin/ci-74-investigation/`; the real journal was not accessed. README, this document, the tooltip test file and its new test-only readiness helper are the only changes. A new GitHub Actions run after the user's commit/push is still required; local results do not establish green CI.

## CI evidence: run 75 — modal focus and native activation

[Run 75](https://github.com/yovko93/Personal-Trading-Journal/actions/runs/37205500198), validate job `111445728411`, completed and uploaded its diagnostic artifact. Local/PR head was `214b1a50b2c0ba9e7ddd7f1565e2c6a503eda876`, branch `develop`, initially clean. CI checked out merge `4c85c3468b3af44bb18133c4734c040d89cddf79`; both trees are `7e12a7a42fcdcade330747ff8cb79da7247b2424`.

Domain **400/400**, Application **516/516**, Infrastructure **724/724** and isolated grid **36/36** passed. Native Calendar was **77 passed / 1 failed**. Parent Desktop was **810 passed / 77 propagated failures**. The first real failure was `CalendarDayModalTests.InitialOwnedPanelFitsTableAtDesktopWidthAndWrapsWithoutLosingAction("Light", 1280, 240, true)`, `CalendarDayModalWindowTests.cs`, original line **133**: `Assert.Null(view.DayDialog)` passed, then `Assert.True(cell.IsKeyboardFocused)` failed. All preceding table, wrapping, inline View and dismissal assertions passed. Tooltip-readiness checks from run 74 passed.

### Focus evidence

`CalendarView.SelectDay` calls the selected cell's `Focus()` in its `ShowDialog` finally block. The failing layout test then pumps arbitrary dispatcher/native events and assumes the owner still has keyboard activation. The window-controls theory already requested owner activation before making the same focus assertion. [WPF distinguishes logical and keyboard focus](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/focus-overview): a deactivated window can retain the correct logical target while another window receives keyboard input.

The native TRX and phase logs prove concurrent chart windows on the same desktop:

| Case | UTC interval in CI |
| --- | --- |
| Failing modal Light/1280/240/long | 13:27:00.3949189–13:27:01.5896382; STA23 action ended 13:27:01.5604334 |
| Chart Dark/960/96 | 13:26:58.6827122–13:27:00.8031664 |
| Chart Dark/480/240 | 13:27:00.8060196–13:27:01.3279132 |
| Chart Light/960/96 | 13:27:01.3339622–13:27:01.9369097; STA38 action spans the modal failure |

These are separate STAs in the same child, `WinSta0/Default`; the chart cases show real windows and use keyboard focus. Original CI logs did not record HWND activation, so the exact competing activation event is not claimed observed.

Temporary instrumentation of the **existing production Focus call**, plus a controlled competing test window during the post-close pump, reproduced the exact failed assertion in the same Light/1280/240/long case:

```text
production Focus() returned True
ShowDialog returned: owner active=True; dialog active=False, visible=False
keyboard=selected CalendarDayHost; owner logical focus=selected CalendarDayHost
competing window activated: owner active=False; keyboard=Button
after Pump: owner logical focus=selected CalendarDayHost; cell.IsKeyboardFocused=False
cell visible=True, focusable=True, enabled=True throughout
```

The original unperturbed 12-case theory passed with the instrumentation. This establishes correct restoration followed by legitimate loss of activation, not a replaced cell, incorrect target, failed modal close or layout defect. The temporary production logging was removed; `CalendarView` and all production code are unchanged. The controlled reproduction complements the proven CI overlap but cannot retroactively supply the original runner's missing focus trace.

### Test-only correction

`CalendarFocusProbe` first requires the selected cell to be the owner's **existing logical focused element**, still connected, visible, enabled and focusable. Only then does it issue one [SetActiveWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setactivewindow) request for that STA's test-owned HWND, the same API WPF uses in modal close. Observable owner activation, native active HWND, `Keyboard.FocusedElement`, and `cell.IsKeyboardFocused` must agree. Activation/focus events coordinate a bounded five-second check; it never calls `cell.Focus()`, resets logical focus, retries activation or sleeps. Existing raw keyboard-focus assertions remain, with no pump between the gate and assertion. All table/layout, dismissal and edit-protection assertions remain unchanged.

A first helper draft required `Window.Activate()` to return true. On the hidden sandbox desktop it returned **False with owner already active, exact cell keyboard focus and native foreground HWND=0**. That was another invalid foreground-permission assumption, not failed restoration. The final helper uses thread-local activation and checks observed state, rather than requiring a global foreground request to succeed. It does not force the real application to steal focus when a user switches away, and does not serialize test collections or change 45-second STA/two-minute suite deadlines.

The same gate covers the modal close/focus checks for table layout, backdrop/X/Escape, close/empty-day and window-control states. Two regressions verify that a competing window can take keyboard focus while the selected date retains logical focus, and that the gate **rejects a wrong retained target without repairing it**. Diagnostic history includes owner/dialog activation and visibility, logical/keyboard object identities, cell eligibility, HWND foreground/active/focus state and the activation request result in stdout/TRX plus `calendar-focus.log`. No Account names or Trade identifiers are logged.

### Local verification

The corrected tests passed on 2026-10-04:

| Verification | Result |
| --- | --- |
| Reported layout theory (12 cases) plus competing-window and wrong-target regressions; hidden Windows desktop, `DPIUNAWARE`, two .NET CPUs, 1,044-DIP owner cap | **14/14 passed** |
| Complete isolated Calendar-native suite under the same constraints | **80/80 passed**, 62 seconds |
| Full Release solution suite, normal local display context | Domain **400**, Application **516**, Infrastructure **724**, Desktop **889**: **2,529 passed**, no failures or skips |
| Native child suites during that full run | Grid **36/36**, modal/chart **80/80** |
| Release solution build | Passed, **0 warnings / 0 errors** |
| `git diff --check` | Passed |

The original failure was deliberately reproduced before correction, not retried until passing. The corrected focused cases then also passed inside both complete native runs. A new GitHub run after the user's commit/push remains required. These are automated native-WPF tests, not live application acceptance; no real journal or customer data is used. Diagnostic TRX/logs are retained under ignored `bin/ci-75-investigation/`.

## M14.5 local full-suite investigation (2026-10-05)

The follow-up started on `develop` at `e903e32e3be632d273b8e12816b087ada4d60be9` with a clean worktree: the prior M14.5 implementation had already been committed by the user. The earlier reported `c33b8b4` was not the checkout under test. This is a **local full-suite investigation**, not a downloaded GitHub run. No application, persistence, schema, import or economics code changed.

### Journal navigation: readiness evidence, not an inferred product failure

The historical controlled full run timed out in `LeavingJournalCancelsReadAndReturningIgnoresItsLateResult` while waiting five seconds for its fake repository to start. That wait precedes navigation away and token cancellation. The shell activates Journal synchronously and starts its load with `Task.Run`; the fake Account reader must finish before repository invocation. No WPF dispatcher callback is needed along that path. The old test observed only the repository signal: an earlier completed/faulted load was indistinguishable from a scheduling delay until its signal deadline.

Instrumentation now records elapsed time, managed thread/context/scheduler, ThreadPool state, Account/repository entry, read tokens, pending/completed tasks and navigation state, without journal content. The normal parallel reproduction of the native failure did **not** reproduce the Journal timeout: repository entry was about **1.7 ms**, and the waiter resumed in **3.8 ms**. A later normal parallel run reached repository entry within **1.6/8.5 ms** in its two late-response cases. Therefore the exact original Journal delay remains unproven; native resource pressure is not asserted to be its established cause, and no production read/cancellation behavior was changed.

The test keeps its **five-second bound**, but races readiness with completion of the actual load. If the load exits without calling the repository, it fails immediately with that state rather than waiting for a signal which can no longer arrive. Cleanup releases and drains the pending fake load even after an assertion failure. Regressions cover a late old result **and** a late old exception after navigation away/return, with a nonempty current completed revision and all review answers; 16 concurrent independent navigation flows; cancellation at the Account-reader boundary; and a completed-before-repository failure. They continue to assert the old token is cancelled and that neither obsolete outcome replaces current content, answers, state, revision or error state.

### Native suite: retained per-case renderers exceeded the aggregate budget

The instrumented normal parallel solution run reproduced **85 propagated Desktop failures** from the Calendar native child's unchanged **two-minute** process deadline. Domain, Application and Infrastructure passed. It was not a 45-second individual STA timeout: **79 actions started and 78 finished**, across **79 different managed STA threads**. At termination, phase diagnostics recorded roughly **90.8 seconds process CPU**, **93 native threads**, a **2,134.7 MiB working set / 2,036.7 MiB private bytes**, and about **219 MB managed memory**. ThreadPool pending work was zero with workers available. Earlier cases completed and resource usage grew across their lifetimes; the final action was cut off by the aggregate deadline. The supervisor terminated and awaited the child process tree.

Native cases previously created a fresh STA/dispatcher per case but intentionally did not shut it down, avoiding costly renderer teardown. Ending those threads retained native WPF resources until process exit. The fix uses **one process-owned background STA with a running dispatcher**, reusing its renderer. A gate acquired outside the dispatcher prevents nested modal frames from executing another test action reentrantly. Only this native host's actions are ordered; unrelated collections and test assemblies retain their normal concurrency. Grid tests keep their separate per-case dispatcher cleanup. No deadline, assertion, retry or skip policy was relaxed.

Before the final verification, the corrected native suite passed **85/85** in **89.1 seconds** in a focused run and **106.9 seconds** in a normal parallel solution run. Each used one STA; peak working sets were **701.0 / 691.5 MiB**, private bytes **603.3 / 591.2 MiB**, and native thread counts **42 / 43**. These are process-boundary samples, not a universal performance guarantee. The solution run's only failure was a newly added negative harness probe, described below; it was not counted as full-suite acceptance.

Lifecycle regressions verify dispatcher reuse, exclusion through nested message frames, ordinary action exception/context recovery, and timeout poisoning that cancels queued/later actions without allowing them to run after the blocked action is released. Existing background-thread and process-tree cleanup checks remain. Lifetime-boundary diagnostics now include CPU, native threads, managed/native memory and ThreadPool counters alongside the existing named operation phases.

### Escaping native callback diagnostics

Review of the shared host found that an exception escaping its pump after startup could be lost by attempting to fault an already-completed readiness task. A negative child-process probe then exposed an additional WPF boundary: without an `Application` or dispatcher `UnhandledException` handler, the exception filter reported `RequestCatch=False`. Its queued callback entered, yet remained `Executing`, its task `WaitingForActivation`, and the dispatcher thread alive when the probe's five-second bound expired. Merely rethrowing outside `Dispatcher.Run` did not report that callback's error because the native callback boundary did not unwind the pump.

The test host now captures the original unhandled exception, marks it handled **only to unwind the dispatcher**, requests shutdown, and rethrows the captured exception outside the native callback boundary. A throw-only probe recorded the original error after pump exit, but the test host still remained alive until the probe expired. The post-startup fatal path therefore calls `Environment.FailFast` with that original exception: this is confined to the supervised test child and ensures a dead dispatcher cannot masquerade as a later timeout. The corrected negative probe terminated non-timeout in **2.94 seconds**, with `Test host process crashed`, the original exception/callback stack and confirmed process cleanup. Startup failures still reach the waiting case; unexpected normal pump exit also fails explicitly. Ordinary action exceptions still return directly to their owning test. Production WPF exception behavior is unchanged.

Evidence is retained under ignored `artifacts/m14-5-failures/` (instrumented reproduction, focused/native traces and subsequent full runs). All data is synthetic. No real journal was accessed. Automated WPF tests do not establish live UI acceptance, and a new GitHub run after the user's commit/push remains necessary.

### Final local verification

| Check (2026-10-05) | Result |
| --- | --- |
| Focused Journal/navigation, process supervision, native harness, modal/chart and grid tests; two .NET-reported CPUs, process-local `DPIUNAWARE`, 1,044-DIP native-window cap on the hidden sandbox desktop | **280/280 passed**, no skips, including the 16 concurrent Journal flows and late-success/late-error cases. These process settings do not change system display scaling or physically pin CPU affinity. |
| Child suites in that constrained run | **85/85 native**, one shared STA, about **82.4 seconds**, peak sampled working set **364.9 MiB**; **36/36 grid**. Existing deadlines unchanged. |
| Complete Release solution suite, fresh normal process settings and normal assembly concurrency | **2,810/2,810 passed**, no failures/skips: Domain **444**, Application **516**, Infrastructure **775**, Desktop **1,075**. No `-m:1` or global xUnit serialization. |
| Native/grid children in that full run | **85/85 native**, **36/36 grid**. Native: one STA, **118.6 seconds**, peak **686.5 MiB working set / 592.4 MiB private bytes**, **43 native threads**. This passed the unchanged 120-second bound but leaves little margin; hosted CI performance is still unverified. |
| Journal race evidence in the final full run | Both late-response cases and 16 concurrent flows passed. First repository entries at **8.3/18.0 ms**, observed by the test at **13.1/50.7 ms**; neither old success nor old error replaced the fresh completed revision. |
| Release build / EF model consistency / `git diff --check` | Passed; **zero build warnings/errors**, no pending model changes. EF uses the existing in-memory design-time factory. |
| GitHub Actions / live WPF application verification | **Not performed.** A new run after the user's commit/push and the existing isolated interactive checklist remain required. No claim of green CI or live M14.5 acceptance. |

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

A passing local reproduction/full suite is not a passing GitHub run. The user must commit/push these changes to the PR branch, let a new Windows CI run test that new head/merge tree, and verify all project suites plus the current **36 grid** and **85 modal/chart** child cases. If it fails, inspect the first unfinished phase, lifetime counters and any dump before attributing the cause. Rerunning an older workflow run would only retest its old tree.
