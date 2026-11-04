# CI #117 — retained initial movement and unresolved hover selection

## Status — 5 November 2026

**The retention correction is verified; the interaction failure is not fixed.** Both failing children retain attachment, initial cursor positioning, native message delivery and WPF processing. The earliest observed discrepancy is the first processed move at the duplicate item: WPF updates its position but retains ContextMenu as the element under the mouse. Later explicit hit testing reaches the item. These observations do not establish why the first WPF hit selection differed.

This completed hosted gate has **5,540 passed / 2 failed / 0 skipped**, exit 1. It is separate from the incomplete CI #113 workflow and the completed CI #116 5,540/1 run. No unchanged complete gate was rerun during this investigation.

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

## Bounded controlled experiment and blocker

A disposable probe is retained locally at `TestResults/ci117/probe/Program.cs` and `NativeHoverProbe.csproj`. It uses its own Window, ContextMenu and synthetic item, with an explicit WM_NCHITTEST barrier to model an initial global-hit miss. The planned comparison restores native hit testing without changing capture, runs the original three-second same-point readiness, then performs one distinct native movement and a genuine native click, asserting exactly one synthetic operation. This isolates the cached-hover mechanism; it cannot identify the hosted initial miss by itself. Acceptance tests are not modified to click after failed readiness.

The probe built successfully, but **both launches stopped at the foreground guard**, including after normal activation of its own window. Exact blocker: `Probe owner is not foreground; no native input attempted.` No cursor movement or click was sent, and no mechanism reproduction or repair result is claimed. `TestResults/ci117/probe/bin/Release/net10.0-windows/probe.json` retains the failure. The probe is an ignored local investigation artifact, not a new scheduled test.

Required conditions: an interactive desktop where the disposable window can normally become foreground and both planned points belong to its HWNDs. Do not close unrelated applications or bypass the guard. Run this bounded experiment there first. For attribution, inspect the first-report decision above with matching WPF binaries/symbols on one affected hosted-equivalent case; distinguish a cached-state path from an actual global-hit miss before choosing a fixture correction. Any capture-aware acceptance change additionally needs real native duplicate delivery in the false-hover state, which is still unobserved.

## Verification and scope

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

This task changes only `README.md`, `docs/ci-menu-readiness.md` and this guide. There is no interaction fix or new instrumentation layer. Existing native input, hover/command assertions, watchdogs, scheduling, dispatch, workflow budget, Calendar fix and approved branding/assets are preserved. Updated hosted acceptance and native branding/Explorer/taskbar/accessibility checks remain pending. No push, PR or merge was performed.
