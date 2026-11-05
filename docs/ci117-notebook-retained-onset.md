# CI #117 — retained initial movement and unresolved hover selection

## Status — 5 November 2026

**A bounded fixture-input correction now addresses the demonstrated cached-hover mechanism; the original hosted first-hit trigger remains unknown.** The interactive follow-up below reproduces an induced initial miss, shows the original same-position readiness failing, then verifies hover and one genuine native operation after one distinct move. Release build, 20 distinct focused cases and one complete local gate (**5,543 passed / 0 failed / 0 skipped**, exit 0) pass. The earlier trace-retention correction is independently verified in both hosted failures. Updated hosted validation of the input correction remains pending.

The initial artifact investigation established that both failing children retain attachment, initial cursor positioning, native message delivery and WPF processing. The earliest observed discrepancy is the first processed move at the duplicate item: WPF updates its position but retains ContextMenu as the element under the mouse. Later explicit hit testing reaches the item. These observations do not establish why the first WPF hit selection differed.

This completed hosted gate has **5,540 passed / 2 failed / 0 skipped**, exit 1. It is separate from the incomplete CI #113 workflow and the completed CI #116 5,540/1 run. No unchanged complete gate was rerun during the initial artifact investigation.

## Provenance and retrieved artifacts

- [CI #117](https://github.com/yovko93/Personal-Trading-Journal/actions/runs/41909683423), attempt 1; job `126853608770`.
- Head/current checkout: **`c7856a95f24de4bf42ecaec8347139f9cb0c6fdd`**. Tested merge: **`fc01ab917030b0b0960dba3a3955a29cae8f14df`**. Checkout output, both snapshots' `githubSha` and `testAssemblyInformationalVersion` agree on the merge. GitHub head-to-merge comparison reports zero changed files.
- Matching source contains `TransitionBuffer` first-16/last-48 retention, attachment-hook flags and existing cursor-call result observations. It also contains Calendar `ReadCancellation`, exact single-parameter child dispatch and the 20-minute workflow Test budget.
- Retrieved artifact: `windows-test-results`, ID **`13362129161`**. Verified ZIP SHA-256: **`604352764C18F8F9BA97A0BAD422441C6C12D2BE228130BC9F5373614DE86407`**.
- Artifact root: `release-20261104-204806-ce28f00d1a7745419e1d3158281e024a`. Local ZIP, extracted files and job log are retained under `TestResults/ci117/`.

Both failing directories are under `exclusive-wpf/`:

| Parameter | Child directory | Actual boundary JSON in `menu-evidence/` |
| --- | --- | --- |
| Light / 1280 / 96 | `notebook-menus-20261104-205025-f250f3e2164a4738abab8d7e6834e8c9` | `readiness-failure-6920-40f49a89e49244f08e9c07e0c0c26d7d.json` |
| Dark / 1280 / 96 | `notebook-menus-20261104-205032-6e6af80ec15e4de88ba9717b6f082c26` | `readiness-failure-6396-9811f1bbb3d2442bb4fb52f37a53477a.json` |

Each contains the actual `notebook-menus.trx`, `stdout.log`, `stderr.log`, `process-supervision.log`, `calendar-sta-phases.log`, `menu-evidence/input-sequence.log` and **`menu-evidence/mouse-movement.log`**. Snapshot and trace existence/content were checked directly, rather than inferred from console output.

The tested Menus.cs:125 reopens NoteOverflow after the earlier toggle/dismissal/Refresh interactions. Line 126 selects **DuplicateNoteButton**; line 127 calls `MouseClick(duplicate)`. The helper asserts readiness at line 208 before native down/up injection. Neither failed case reaches the duplicate command, count/write assertions or native duplicate click.

## Retained sequence

Times below are UTC. Both traces begin the duplicate interval with the pointer on NoteOverflow at **933,137**, managed position **134,-54.62** relative to the duplicate item, and normal ContextMenu **SubTree** capture.

| Observation | Light | Dark |
| --- | --- | --- |
| Attached to target popup; native and processed hooks true | 20:50:29.5140533; `0x6C005C` | 20:50:36.9073819; `0x72005C` |
| Existing SetCursorPos requests **909,210**, returns true; OS readback agrees | 29.5144372 | 36.9077920 |
| Hook sees **WM_MOUSEMOVE**, client **115,60**, on that popup | 29.5173230 | 36.9104000 |
| Processed **PreviewMouseMove**, source ContextMenu; managed position now **110,18.38**, hover false | 29.5187692; input timestamp 617437 | 36.9117887; input timestamp 624828 |
| Processed **MouseMove**, same position/source/capture/false hover | 29.5188414 | 36.9118571 |
| Final unsuccessful readiness observation | 20:50:32.5284058; elapsed 3.0138334 s | 20:50:39.9226223; elapsed 3.0147003 s |
| Retention summary | total 312; first 16, last 48; omitted middle 248 | total 325; first 16, last 48; omitted middle 261 |

Later surviving rows repeat successful cursor requests and same-position native moves without recovering item hover. Attachment and both hook flags precede the first observed popup message and processed movement. Thus initial delivery and processing **survived retention**. The omitted middle remains explicitly unknown; do not claim every event in the three-second interval was retained. Native hooks cover the named popup during this interval, not every HWND or all earlier interactions.

At both final boundaries:

- Intended and OS cursor: **909,210**; target bounds: **799,192–1019,228**. Native DPI 96, scale 1 and identity device transforms; source-relative position **115,60** matches the native message.
- Expected popup, window at intended point and window at actual cursor agree. Light testhost PID **6920**, native TID **3156**, foreground/owner **`0xF01DA`**; Dark PID **6396**, native TID **4292**, foreground/owner **`0x1A008E`**. No unexpected process/window is recorded there.
- Target is loaded, visible, enabled, hit-testable and layout-valid; animation is settled. Menu is open and its context menu is loaded/visible. The separately recorded Popup wrapper reports `loaded=False`, `animation=None`; this does not mean the target or ContextMenu is unloaded.
- Explicit WPF input/visual hits traverse TextBlock → MenuItemChrome → **DuplicateNoteButton** → ContextMenu → PopupRoot. Managed `IsMouseOver` and `IsMouseDirectlyOver` are false; `Mouse.DirectlyOver` and capture remain ContextMenu/SubTree.

This matches the **final-boundary geometry/hover discrepancy** in the actual [CI #113 snapshot](ci113-readiness-and-budget.md). Unlike that earlier run, CI #117 proves initial native delivery and processed movement. Its final explicit hit does not retroactively establish the first input-processing hit result.

The same run's Dark/480/240 child `notebook-menus-20261104-205020-c42e66d89c2f402bbcd90050777e80ed` and Light/480/240 child `notebook-menus-20261104-205040-c625a054a7314bb4b5294f695a850556` pass. Their first processed moves select TextBlock with **SubTree capture still present**, item hover becomes true, and native duplicate injection plus the duplicate-operation assertions complete. Capture alone therefore cannot justify dropping hover readiness.

## WPF semantics and the remaining question

Public .NET 10 [MouseDevice source](https://github.com/dotnet/wpf/blob/v10.0.0/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/Input/MouseDevice.cs) shows that SubTree capture uses a global hit test and falls back to the capture root for a miss/outside-subtree result. It can retain cached hover for identical-position reports; a fresh hit test requires changed position, synchronization or an unavailable previous element. This explains how correct later explicit hits can coexist with stale managed hover. It does **not** prove which path selected ContextMenu on the hosted first move.

The hosted WPF version is `10.0.14-servicing.26522.126+0427b9abbead3fa64c6655eb4290c537397131fa`. That exact source ref was not available from the public dotnet/wpf API (404). The public v10.0.0 implementation validates a mechanism, not an exact servicing-build diagnosis.

The precise missing observation is: **during the first absolute report at client 115,60, did WPF run its global hit test, and what input source, cached position, native window and subtree hit did it use?** Existing native-message rows precede processing; processed-event rows follow it. Neither exposes that internal decision or a contemporaneous explicit hit. Later matching HWNDs do not settle transient initial visibility/placement; conversely, those possibilities are not demonstrated defects.

## Initial bounded controlled experiment and blocker

A disposable probe is retained locally at `TestResults/ci117/probe/Program.cs` and `NativeHoverProbe.csproj`. It uses its own Window, ContextMenu and synthetic item, with an explicit WM_NCHITTEST barrier to model an initial global-hit miss. The planned comparison restores native hit testing without changing capture, runs the original three-second same-point readiness, then performs one distinct native movement and a genuine native click, asserting exactly one synthetic operation. This isolates the cached-hover mechanism; it cannot identify the hosted initial miss by itself. Acceptance tests are not modified to click after failed readiness.

The probe built successfully, but **both launches stopped at the foreground guard**, including after normal activation of its own window. Exact blocker: `Probe owner is not foreground; no native input attempted.` No cursor movement or click was sent, and no mechanism reproduction or repair result is claimed. `TestResults/ci117/probe/bin/Release/net10.0-windows/probe.json` retains the failure. The probe is an ignored local investigation artifact, not a new scheduled test.

Required conditions: an interactive desktop where the disposable window can normally become foreground and both planned points belong to its HWNDs. Do not close unrelated applications or bypass the guard. Run this bounded experiment there first. For attribution, inspect the first-report decision above with matching WPF binaries/symbols on one affected hosted-equivalent case; distinguish a cached-state path from an actual global-hit miss before choosing a fixture correction. Any capture-aware acceptance change additionally needs real native duplicate delivery in the false-hover state, which is still unobserved.

## Initial artifact investigation verification and scope

Retained top-level TRX counters agree with the job's completed result:

| Stage/project | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Exclusive Desktop | 144 | 2 | 0 |
| Parallel Desktop | 2,136 | 0 | 0 |
| Domain | 602 | 0 | 0 |
| Application | 1,050 | 0 | 0 |
| Infrastructure | 1,608 | 0 | 0 |
| **Total** | **5,540** | **2** | **0** |

The four isolated overflow children each execute exactly one matching parameter: **2 passed / 2 failed**, with no child abort/timeout. They substantiate four parent results, not additional top-level cases. Failing launcher PIDs **3456** (Light) and **5888** (Dark) exit 1 and are confirmed cleaned up; STA logs reach dispatcher cleanup. These are readiness assertions, not watchdog cancellations, product duplicate assertion failures or independent host crashes.

Local verification for this investigation:

- `dotnet build PersonalTradingJournal.sln -c Release --no-restore`: **0 warnings / 0 errors**, retained in `TestResults/ci117/build.log`.
- Existing retention, diagnostic failure-preservation/no-collection and scheduling contracts: **6 passed / 0 failed / 0 skipped**, no isolated children, retained in `TestResults/ci117/verification/diagnostic-contracts/` and `focused.log`.
- Disposable probe: builds with zero warnings/errors; native execution **blocked before input**, as above.
- No all-parameter native rerun or complete local gate: no demonstrated interaction correction and the controlled probe's desktop guard is not satisfied. The historical Calendar-correction local gate remains separate evidence.

Focused command:

```powershell
$env:PTJ_TEST_RESULTS_DIRECTORY = Join-Path $PWD 'TestResults/ci117/verification/diagnostic-contracts'
dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~MovementFloodRetainsOnsetAndDeadlineWithExplicitOmissionCount|FullyQualifiedName~DiagnosticErrorsPreserveReadinessAssertionAndCleanup|FullyQualifiedName~SuccessfulReadinessDoesNotCollectOrWriteEvidence|FullyQualifiedName~ReleaseSchedulingTests' --logger trx --results-directory TestResults/ci117/verification/diagnostic-contracts
```

The initial artifact task changed only `README.md`, `docs/ci-menu-readiness.md` and this guide. It made no interaction fix or new instrumentation layer. Existing native input, hover/command assertions, watchdogs, scheduling, dispatch, workflow budget, Calendar fix and approved branding/assets were preserved. No push, PR or merge was performed.

## Interactive follow-up and bounded input correction

Follow-up base checkout: `156122cd4fb81391ce0f3067da00f0217a0c04fe` plus the local source changes below. These are local verification results, not a new hosted CI run or an assertion that the hosted trigger has been identified.

The existing ignored probe at `TestResults/ci117/probe/` now has a visible **Start controlled comparison** action. It is a WinExe with a taskbar entry and `ShowActivated=false`; it does not activate itself or force foreground. It waits up to five minutes for a human Start click, then checks foreground ownership, released input and target HWND/bounds before native input. Historical `probe.json` is preserved; each launch writes a separate `results/probe-start-<UTC>-<PID>.json`.

Exact launch command from any PowerShell directory, after building the local probe:

```powershell
dotnet build 'C:\Users\Yovko\source\repos\Personal-Trading-Journal\TestResults\ci117\probe\NativeHoverProbe.csproj' -c Release --no-restore
Start-Process -FilePath 'C:\Users\Yovko\source\repos\Personal-Trading-Journal\TestResults\ci117\probe\bin\Release\net10.0-windows\NativeHoverProbe.exe'
```

Activate **Notebook native hover probe — click Start**, click Start once, then leave the mouse untouched until it closes. Each native readiness phase is bounded to three seconds. If Start is not clicked within five minutes, the probe exits without native input. Foreground loss or an unexpected HWND stops the comparison. Do not close unrelated windows or bypass these guards.

### Actual execution

Human activation succeeded. The first Start launch (PID **19676**) passed foreground readiness, moved to its own anchor, then stopped at the immediate popup-HWND guard before the controlled target move/click. Probe setup was corrected to await the exact popup HWND within the existing three-second bound and to disable popup animation before opening. This is setup for an isolated experiment, not an acceptance-test timeout increase or a claim about the hosted cause.

The second human-started launch, PID **6280**, completed successfully. Evidence: `TestResults/ci117/probe/results/probe-start-20261104-223830-6280.json`; build: `probe/start-build.log` (**0 warnings / 0 errors**).

The subsequent controlled child records local **.NET 10.0.12 / WPF 10.0.12-servicing.26422.108**, with the verification desktop at **240 DPI / scale 2.5** (also recorded by the focused snapshot contract probe). All four existing theory parameterizations run through the established workflow on that desktop. This is not native 96-DPI hosted equivalence: CI #117 used .NET/WPF 10.0.14 at 96 DPI, so updated hosted validation remains necessary.

| UTC / phase | Observed result |
| --- | --- |
| 22:38:52.7726442 — deliberate initial miss | Popup hook returned HTTRANSPARENT during hit testing. Native move processed at **469,357**; explicit hit TextBlock, managed hover false, ContextMenu/SubTree capture. Barrier handled 3 hit tests. |
| 22:38:52.7738534 — barrier removed | Window-at-cursor now equals popup **0x1B0616**; explicit hit TextBlock; hover remains false, capture unchanged. Foreground owner **0x506CC**. |
| 22:38:55.8040202 — original readiness expires | **133 native movement messages**, only **1 processed move**; unchanged **469,357**, false hover, correct popup/explicit hit, no click. `oldReady=false`. |
| 22:38:55.8351765 — one distinct native move | **471,357**, within verified bounds **194,330–744,385**. Processed moves rise to 2; TextBlock hover true; ContextMenu/SubTree capture retained. `changedPointReady=true`. |
| 22:38:55.9297302 — genuine native click | One native down/up pair produces **exactly 1 item Click**; menu closes normally. Exit **0**. |

This reproduces persistence and recovery of cached hover after an **induced** miss. It does not show that HTTRANSPARENT, animation, an external window, or any particular initial-hit condition caused CI #117. No acceptance test clicks after failed readiness or invokes the duplicate command directly.

### Fixture correction and controlled regression

`NotebookMenuMouseApproach` is shared by the actual Notebook overflow test and the new controlled regression. After animation/layout and native popup readiness, it verifies that the center and a point **2 physical pixels to its right** lie inside the intended target and explicitly hit that target through the expected HWND. It issues **one** SetCursorPos to the distinct point. Later readiness observations do not keep writing the same menu coordinates. It never releases capture, activates a window or invokes a command.

The actual MouseClick helper still requires **IsMouseOver**, settled animation and the correct HWND; it additionally keeps an explicit WPF target hit at the chosen point. Existing genuine down/up injection, native mouse-up observation, duplicate count/write assertions and repeated trigger open/close coverage remain. The original three-second readiness deadline is unchanged. Non-menu button movement is unchanged. This is a test-input correction for a demonstrated mechanism, not a production menu-lifecycle change or a capture-aware weakening of readiness.

`NotebookMouseReadinessEvidenceTests.OneDistinctNativeApproachRecoversCachedHoverAndClicksExactlyOnce` runs in one exactly dispatched isolated child with the existing 90-second process / 30-second watchdog bounds. In a disposable menu it explicitly induces a miss, restores native hit testing without changing capture, and requires the **original same-position readiness to remain false for three seconds**. It then uses the real shared approach helper, requires hover with exactly one movement request, and checks one native button-down, one native button-up, exactly one synthetic operation and normal menu close. It retains `controlled-approach.json` and the existing movement trace under the child artifact tree.

The class retains its existing ExclusiveWpf trait/collection; no unrelated tests are serialized, and no scheduling expected-set change is needed. No broad tracing layer, retries, sleeps, watchdog changes or fallback invocation was added.

### Follow-up verification

- Controlled regression: **1 top-level passed / 0 failed / 0 skipped**, plus **1 matching child passed**. The old loop remains false after 130 native moves; one shared-helper request restores hover, then native down/up and operation counts are each exactly 1. Evidence: `TestResults/ci117/approach/controlled/` and `approach-controlled.log`.
- Release solution build: **0 warnings / 0 errors**, `TestResults/ci117/approach-build.log`.
- All four Notebook parameters and related input/layout/diagnostic/scheduling/Calendar checks: **19 top-level passed / 0 failed / 0 skipped**, plus **7 matching isolated children passed**, `TestResults/ci117/approach/focused/` and `approach-focused.log`. This selection excludes the separately executed new controlled regression: combined focused coverage is **20 distinct top-level cases**, with **8 child cases** as evidence, not additional top-level tests. Each duplicate trace has exactly three cursor requests (initial, one distinct approach, final), rather than repeated requests during readiness; see `approach/focused-audit.json`.
- Complete Release gate: run **once** after the supported correction and passing focused checks; **5,543 passed / 0 failed / 0 skipped**, exit **0**. No unchanged gate was rerun.

Actual complete run: `TestResults/ci117/approach/complete/release-20261104-224645-8de7de3fa4be4f63aa4e048a77cf0c23`; console log `TestResults/ci117/approach-complete.log`; stage records `summary.json`; independent retained-TRX audit `audit.json`.

| Stage/project | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Exclusive Desktop | 147 | 0 | 0 |
| Parallel Desktop | 2,136 | 0 | 0 |
| Domain | 602 | 0 | 0 |
| Application | 1,050 | 0 | 0 |
| Infrastructure | 1,608 | 0 | 0 |
| **Top-level total** | **5,543** | **0** | **0** |

The audit finds **319 passed child-case results across 123 retained isolated child TRX runs**, zero failed/skipped/aborted, with distinct run IDs. These are separate execution evidence, **not added to 5,543**. The eight relevant Notebook children all pass: four overflow parameters, two tag menus, one existing snapshot contract probe and the new controlled approach regression (`TestResults/ci117/approach/complete-notebook-audit.json`). The complete gate's scheduling/partition checks report no omitted/skipped cases or duplicate top-level identities. The exclusive set increases from 146 to 147 solely because of the new controlled Fact; parallel counts are unchanged.

The exclusive stage ran from **22:46:45.3306430 to 22:55:26.1460191 UTC**; the parallel stage began **22:55:26.1826334 UTC**. Existing per-case watchdogs and the 20-minute hosted workflow budget are unchanged. This local result does not replace CI #117's completed **5,540/2** evidence or establish the initial hosted miss's cause.

Complete-gate command:

```powershell
./scripts/Test-Release.ps1 -ResultsDirectory TestResults/ci117/approach/complete
```

Focused command (the new controlled regression is run separately):

```powershell
$env:PTJ_TEST_RESULTS_DIRECTORY = Join-Path $PWD 'TestResults/ci117/approach/focused'
dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~OverflowTriggersToggleThroughNativeMouseAndOwnedKeyboard|FullyQualifiedName~TagMenuRoutedToggleDismissalAndCapturedOwnership|(FullyQualifiedName~NotebookMouseReadinessEvidenceTests&FullyQualifiedName!~OneDistinctNativeApproachRecoversCachedHoverAndClicksExactlyOnce)|FullyQualifiedName~NotebookMenuLayoutTests|FullyQualifiedName~ReleaseSchedulingTests|FullyQualifiedName~CompletedLoadCannotDisposeCancellationStillOwnedByDeactivate' --logger trx --results-directory TestResults/ci117/approach/focused
```

Tracked changes in this follow-up: `NotebookHeaderViewTests.Menus.cs`, new `NotebookMenuMouseApproach.cs`, the partial declaration in `NotebookMouseReadinessEvidenceTests.cs`, new `NotebookMouseReadinessEvidenceTests.NativeApproach.cs`, `README.md`, `docs/ci-menu-readiness.md` and this guide. The existing probe's `Program.cs`/project are updated locally under ignored TestResults, not added to the scheduled test set. Calendar cancellation ownership, isolation/single-case dispatch, scheduling contract, workflow budget, approved branding and assets are preserved. Updated hosted validation of the correction and native branding/Explorer/taskbar/accessibility checks remain pending. No push, PR or merge is performed.
