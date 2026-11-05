# CI #109 — Duplicate menu-item readiness

## Current status — 5 November 2026

Interactive follow-up: the [CI #117 controlled probe and fixture correction](ci117-notebook-retained-onset.md#interactive-follow-up-and-bounded-input-correction) now demonstrate same-position reports retaining stale hover after a modeled initial miss. One distinct native move recovers item hover without changing capture, and a genuine click performs exactly one operation. The actual menu fixture now uses one guarded movement after popup readiness, retaining IsMouseOver, HWND/WPF hit checks and native duplicate assertions. The controlled regression and all four menu parameters pass: **20 distinct focused top-level cases**, plus **8 isolated children**. One complete local gate passed **5,543 / 0 failed / 0 skipped**, exit 0; separately, 319 child-case results across 123 retained child runs passed. This does not identify the original hosted first-hit trigger; updated hosted acceptance remains pending.

Update, 5 November, subsequent CI #117: [retained onset analysis](ci117-notebook-retained-onset.md) verifies the first-16/last-48 correction in the tested source and both actual failing traces. Initial native movement reaches the popup and is processed by WPF, but hover remains on ContextMenu; the two passing menu parameters acquire item hover with the same SubTree capture. The completed gate is **5,540 passed / 2 failed / 0 skipped**. The first WPF hit-selection decision remains unresolved. A disposable controlled probe stopped at its foreground guard without native input; no readiness change or unchanged full gate was attempted.

Update, subsequent CI #116: [actual movement-trace analysis](ci116-notebook-input-onset.md) confirms repeated native popup movement and matching managed/OS positions, but the rolling buffer lost the initial input interval. Retention now keeps the onset and deadline with explicit omission counts. The interaction cause remains unresolved; no readiness guard change or unchanged full-gate rerun was made.

Update, 4 November, subsequent run: [CI #115](ci115-calendar-and-notebook.md) completed with two Notebook readiness failures plus a separate Calendar lifecycle exception. Its tested source lacked processed movement/capture-mode tracing; that bounded instrumentation is now implemented without changing readiness or native click assertions. The Notebook cause remains unresolved.

Update, 4 November: [completed CI #114](ci114-notebook-readiness.md) reports 5,537 passed / 3 failed / 0 skipped. Three actual DuplicateNoteButton snapshots show the hover discrepancy, while the fourth parameter passes with ContextMenu capture and native item delivery. All four pass unchanged in a bounded local reproduction. Cause remains unproven; no capture-aware predicate change or unchanged complete gate was attempted.

Update, 3 November: [CI #113](ci113-readiness-and-budget.md) retained the actual failure-boundary JSON. Cursor geometry, DPI, HWND ownership and explicit WPF hit ancestry reach DuplicateNoteButton, while managed hover remains on the captured ContextMenu. The cause remains unresolved. That run's separate workflow deadline and repeated child-suite correction are documented independently; it has no complete-gate total.

**Historical hosted first-hit trigger: unresolved.** The exact failing case originally passed unchanged locally. All menu parameterizations and related tag-menu/layout regressions also passed. Those unchanged passes did not establish a fix. Failure-only diagnostic instrumentation was subsequently added. The scheduling-contract correction below passed one complete local Release gate: **5,540 passed / 0 failed / 0 skipped**, exit 0. That historical result does not establish a cause or correction for the earlier readiness failure. The current bounded input correction is documented above; updated hosted acceptance and native branding manual checks remain pending.

The original CI #109 result was **5,534 passed / 1 failed / 0 skipped**, exit 1. The failed parent and its matching failed isolated child describe **one failed top-level case**, not two. The subsequent scheduling-contract failure is separate, as described below.

## Release scheduling-contract correction

The subsequently reported CI gate had **5,539 passed / 1 failed / 0 skipped**, including **145/145 exclusive cases passed**. Its sole failure was `ReleaseSchedulingTests.ExclusiveTraitAndCollectionCoverRequiredNativeClassesWithoutSerializingOtherTests`: the actual exclusive-class set contained `NotebookMouseReadinessEvidenceTests`, but the independently maintained expected set omitted it.

That class must retain its `ExclusiveWpf` trait and collection. Its controlled popup probe starts an isolated child, creates a real WPF Window and ContextMenu, acquires popup mouse capture, and compares native cursor/foreground and managed mouse state. Process isolation does not isolate the shared interactive desktop. The four other cases in the class are lightweight diagnostic contracts; preserving the established class-level scheduling convention is the smallest correct change.

The correction adds only `typeof(NotebookMouseReadinessEvidenceTests)` to the explicit expected set in `ReleaseSchedulingTests.cs`. The exact-set equality, collection/trait consistency checks, nonparallel exclusive collection, and checks preserving parallel execution elsewhere remain intact. Diagnostic behavior, native readiness requirements, deadlines, publishing and branding are unchanged. Base checkout: `6457b75301b33e19efdca96ed55cb0b94196cd9a`; local evidence: `TestResults/readiness-scheduling/`.

This fixes the demonstrated scheduling inconsistency. It does not establish a cause or correction for the historical intermittent `DuplicateNoteButton.IsMouseOver` readiness failure.

Release build passed with **0 warnings / 0 errors**. The focused selection passed **17 top-level cases / 0 failures / 0 skipped**, plus **7 matching isolated children**: the scheduling guard (1), diagnostic contracts (5), overflow parameterizations (4), tag menus (2), and layout/crop regressions (5). Child counts are evidence, not extra top-level cases.

The complete gate ran **once**, after focused checks passed, and exited **0**:

| Stage/project | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Exclusive Desktop | 145 | 0 | 0 |
| Parallel Domain | 602 | 0 | 0 |
| Parallel Application | 1,050 | 0 | 0 |
| Parallel Infrastructure | 1,608 | 0 | 0 |
| Parallel Desktop | 2,135 | 0 | 0 |
| **Total** | **5,540** | **0** | **0** |

Retained run: `TestResults/readiness-scheduling/complete/release-20261102-202648-89d71949933e41eaa693c51da7f35271/`, including `summary.json` and five top-level TRX files. `stage-audit.json` verifies **5,540 unique top-level identities**, no duplicates, all five diagnostic cases exclusively in `exclusive-wpf`, and the scheduling guard once in `parallel`. The unchanged complementary filters cover the same 5,540-case set as the reported CI run, with every case executed and none skipped. Nested child TRX files are excluded from the gate totals.

Changed files for this correction: `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/ReleaseSchedulingTests.cs`, `README.md`, and this guide. No test class was reclassified, no unrelated test was serialized, and no hosted workflow, push, PR or merge was performed.

```powershell
dotnet build PersonalTradingJournal.sln -c Release --no-restore
$env:PTJ_TEST_RESULTS_DIRECTORY = Join-Path $PWD 'TestResults/readiness-scheduling/focused'
dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~ReleaseSchedulingTests|FullyQualifiedName~NotebookMouseReadinessEvidenceTests|FullyQualifiedName~OverflowTriggersToggleThroughNativeMouseAndOwnedKeyboard|FullyQualifiedName~TagMenuRoutedToggleDismissalAndCapturedOwnership|FullyQualifiedName~NotebookMenuLayoutTests' --logger trx --results-directory TestResults/readiness-scheduling/focused
./scripts/Test-Release.ps1 -ResultsDirectory TestResults/readiness-scheduling/complete
```

## Failure-boundary instrumentation — 2 November 2026

The overflow theory now writes an **unconditional, failure-only** JSON snapshot when its unchanged readiness predicate is false at the existing three-second deadline. It does not require `PTJ_NOTEBOOK_MENU_DIAGNOSTICS`. Passing observations do not collect or write this snapshot. The existing input sequence and optional observations remain available.

Artifact path within the existing `windows-test-results` CI upload:

```text
release-<timestamp>-<id>/exclusive-wpf/
  notebook-menus-<timestamp>-<id>/menu-evidence/
    readiness-failure-<testhost-pid>-<id>.json
    input-sequence.log
```

The terminal timer observation supplies the timestamp, intended point, settled flag and elapsed readiness time. Collection runs synchronously immediately after the nested readiness frame returns, before timer/fixture/popup/window cleanup, with no additional dispatcher work. `capturedAtUtc` identifies the subsequent sequential read; native desktop state can still change externally during that read. The original readiness assertion and message then run, before any mouse injection or duplicate-command assertion.

The bounded snapshot includes:

| Evidence | Fields |
| --- | --- |
| Identity and provenance | Exact theory display name, child testhost PID, managed thread, theme, logical viewport width/height, fixture window position/size, fixture DPI, assembly informational version (SDK source revision when available), `GITHUB_SHA` and run ID when available |
| Coordinates and DPI | Intended floating-point screen point and integer injection point, native cursor read success/readback, all four target screen corners, render size, cursor coordinates in the target and popup root, target DPI scale, presentation-source transforms to/from device pixels, per-HWND native DPI |
| Target and input | Loaded, visibility, visible, enabled, hit-test visible, valid measure/arrange, `IsMouseOver`, `IsMouseDirectlyOver`, `Mouse.DirectlyOver`, `Mouse.Captured`, left-button state, active mouse source and native capture for the current thread |
| WPF hit testing | Input and visual hit at the **read-back cursor**, using the target's presentation root (the popup root for menu items), with visual ancestry including names, visibility, enabled/hit-test state, opacity and render transforms |
| Native windows | Expected HWND, HWND at the intended point and at the native cursor, foreground and fixture HWNDs; each includes PID/TID, window class, owner HWND/PID/TID, root/root-owner handles, rectangle/read success and DPI |
| Popup and lifetime | ContextMenu open/loaded/visible, placement, offsets and placement target; Popup open/loaded, animation, stays-open, placement, child and target; fixture activation and dispatcher shutdown state |

Fixture DPI is the theory parameter, **not** a claim about the actual desktop scale. The assembly version and hosted SHA provide provenance without spawning a Git process; an informational-version revision alone does not certify uncommitted changes. Missing/failed field reads are marked `unavailable: <exception type>` where possible. Ancestry is capped at 24 nodes, field strings at 4,096 characters, and each failure writes one local JSON file with no retries. There is no window activation, cursor movement, capture release, input synthesis, layout update, dispatcher pump or asynchronous fallback in collection. Capture/serialization/directory/write errors are swallowed at the diagnostic boundary; final input-log writes are also guarded so diagnostic I/O cannot mask the assertion or stop normal fixture cleanup. A failed write can leave no/partial artifact; this is best-effort evidence, not a replacement acceptance condition.

### Instrumentation verification

Local evidence is retained under `TestResults/readiness-diagnostics/`. Release build: **0 warnings / 0 errors** (`build.log`).

| Selection | Result |
| --- | --- |
| Diagnostic contract probes | **5 passed / 0 failed / 0 skipped**, plus 1 matching isolated popup child passed (`probes-verified/`) |
| Exact Dark/1280/96 overflow theory | **1 parent and 1 matching isolated child passed** (`exact/`) |
| All overflow theories, tag-menu cases and layout/crop regressions | **11 passed / 0 failed / 0 skipped**, plus 6 matching native children passed (4 overflow + 2 tags; `focused/`) |
| Final combined selection after explicit viewport dimensions were added | **16 passed / 0 failed / 0 skipped**, plus 7 matching isolated children passed (`final/`; 6 native menu cases + 1 diagnostic popup probe) |

Final source verification command (the complete Release gate was not invoked):

```powershell
dotnet build PersonalTradingJournal.sln -c Release --no-restore
$env:PTJ_TEST_RESULTS_DIRECTORY = Join-Path $PWD 'TestResults/readiness-diagnostics/final'
dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~NotebookMouseReadinessEvidenceTests|FullyQualifiedName~OverflowTriggersToggleThroughNativeMouseAndOwnedKeyboard|FullyQualifiedName~TagMenuRoutedToggleDismissalAndCapturedOwnership|FullyQualifiedName~NotebookMenuLayoutTests' --logger trx --results-directory TestResults/readiness-diagnostics/final
```

The controlled popup probe deliberately supplies a false readiness result to exercise the failure path; it does **not** reproduce the intermittent CI condition. It verifies the original assertion type/message, snapshot content while the menu is still open, unchanged cursor/capture/foreground/mouse-over source, no processed input, and a queued dispatcher sentinel remaining unprocessed throughout collection. Separate probes inject capture and write exceptions and an actual directory-creation failure; each retains the original readiness assertion and executes cleanup. Successful readiness never invokes collection.

During probe development, the new popup test completed its assertions but timed out in dispatcher cleanup (`probes/`, `probes-final/`, `probe-dump/`). A retained targeted dump (`probe-cleanup.dmp`, `probe-cleanup-stacks.log`) shows WPF destroying its popup HWND/releasing capture during shutdown, then waiting in `PenThreadWorker.WorkerGetTabletsInfo`. The probe now cancels its queued sentinel and allows **its own popup close** to finish in bounded teardown before shutting down the dispatcher. Final probe results above pass. This test-only setup/teardown correction changes no existing menu deadline or input behavior and establishes no cause for the original CI readiness failure.

The instrumentation task did not repeat the complete Release gate; the subsequent scheduling correction's gate is recorded above. No hosted failure-boundary snapshot has yet been observed. The local snapshots belong to controlled diagnostic probes; passing native menu reruns do not explain the original managed mouse-over failure. Branding assets and behavior remain unchanged; native branding/Explorer/taskbar/screen-reader acceptance stays pending.

## Tested source and retained evidence

- [CI run #109](https://github.com/yovko93/Personal-Trading-Journal/actions/runs/41446986433), attempt 1, PR #27, job `125282451335`.
- Checkout at the original investigation: `bc083bc31ead006e8fb6fdcf493cff2ee017ff25` (approved Bull and Bear artwork). Instrumentation was added on base `d595acef90cacb4b0c2755c3e714aedc17f69b48`.
- Job checkout tested merge `5280345666314ebb899de38daa4b342718801c1b`. The GitHub comparison against the original investigation checkout reports **zero changed files**. Source mapping below therefore applies to the exact tested file, not an assumed line match.
- Artifact `13144086450`, `windows-test-results`, downloaded read-only. ZIP SHA-256 `07847D092A36B4146F873A153F661ADC706C65CFFC90C09F5E3BED9C66731C2B` matches the published digest.
- Evidence root: `TestResults/ci109-menu/` (ignored local artifacts). It retains the supplied attachment, complete job log, provenance, verified ZIP/extraction, child result/stack JSON, exact and focused reruns, and native input traces.

Original run directory within the artifact: `release-20261102-124530-48d23b89140e4653a298494bb67f98cd/`. Failed child: `exclusive-wpf/notebook-menus-20261102-124807-0d2089857dc6466984c2e5549495f90c/`, with final `notebook-menus.trx`, `process-supervision.log`, `calendar-sta-phases.log`, keyboard diagnostics and `menu-evidence/input-sequence.log`.

The supervisor launched PID **5784**, which exited code 1 and was confirmed exited at `12:48:15.6099582 UTC`. The actual testhost was PID **8800**, STA **18**. The harness records dispatcher cleanup finishing with shutdown **true/true** after the failed action. This is an assertion propagated through the child exit, not a 90-second process deadline, 30-second watchdog, cancellation, failed shutdown, or missing result.

## Exact failed operation

`PersonalTradingJournal.Desktop.Tests.Notebook.NotebookHeaderViewTests.OverflowTriggersToggleThroughNativeMouseAndOwnedKeyboard(theme: "Dark", width: 1280, dpi: 96)`.

At `NotebookHeaderViewTests.Menus.cs:125`, the fixture returns to the note panel, lays out, and natively clicks `NoteOverflow`, successfully reopening its context menu. Line 126 selects the `MenuItem` named **`DuplicateNoteButton`**. The native `MouseClick(duplicate)` at **line 127** fails in the helper's readiness assertion at **line 199**. The duplicate command/persistence pump and line 128's count/write assertions were **not reached**.

Earlier repeated-click open/close, outside dismissal, Escape/focus restoration, keyboard opening, and Refresh interactions had already succeeded. The failing step is clicking the Duplicate **menu item**, not opening or toggling the overflow trigger.

The child failure message is:

> The intended control did not become hit-testable with settled popup animation and mouse-over.

The stack continues through the line-127 call, `CalendarStaTest.ExecuteAction` lines 124/140, `RunAsync` line 89, and theory line 36. The parent propagates `IsolatedTestProcess.RunSuiteAsync:48` through theory dispatch line 31. Full original stacks and outcome are retained in `hosted-child.json`; source/log files were not changed for reproduction.

## What the hosted trace proves

The final readiness row records:

```text
target=DuplicateNoteButton; point=909,210
expectedHwnd=7405960; actualHwnd=7405960
settled=True; mouseOver=False; active=True; hit=False
window=16,16,992,688; desktop=0,0,1024,768
```

The helper's readiness predicate requires settled animation, native HWND equality, and—for a MenuItem—`IsMouseOver`. In this sample, the **mouse-over conjunct failed** while the other recorded conjuncts passed. No native down/up injection occurs after the failing readiness assertion. The preceding menu-open trace records capture by an eight-item ContextMenu; it is not a capture snapshot at the eventual failure boundary.

This does **not** establish an overlapping/unexpected HWND, Chrome interference, a product toggle defect, or an incorrect DPI conversion. HWND equality shows the point reached the expected popup window, but does not prove which WPF element was hit inside it. It also does not explain why managed mouse-over stayed false. The three-second bounded readiness wait expired and asserted; that is distinct from a host watchdog timeout.

The full optional `PTJ_NOTEBOOK_MENU_DIAGNOSTICS` observations were disabled in this CI run. Consequently its final boundary lacks cursor readback, per-HWND process/class ownership, numeric native DPI, item bounds/visibility/enabled state, WPF hit-test target, active mouse source, and contemporaneous popup/capture details. Do not fill these gaps with observations from older runs.

## Exact reproduction and focused coverage

The established parent workflow dispatched exactly one matching isolated child with the unchanged 90-second process and 30-second watchdog deadlines. Existing opt-in diagnostics were enabled; no input repair, retries, direct command replacement, unrelated application close or readiness weakening was added.

| Selection | Parent result | Isolated child result |
| --- | --- | --- |
| Exact Dark/1280/96 overflow theory | 1 passed / 0 failed / 0 skipped | 1 matching case passed |
| All overflow theories, both tag-menu cases, layout/crop regressions | 11 passed / 0 failed / 0 skipped | 6 matching native menu cases passed (4 overflow + 2 tags); 5 layout cases run in parent |

The second selection includes the exact case as part of the requested full parameterization coverage, not as an automatic retry. Parent and child counts are separate.

In the exact rerun, `DuplicateNoteButton` became ready at **(2658,562)**, expected/actual popup HWND **`0x47067C`** / PID **14156** / TID **5656**. Foreground owner HWND `0x704B8` had the same process/thread. The trace recorded valid measure/arrange, popup bounds `(2371,412)-(2945,907)`, DPI scale **2.5**, open ContextMenu, managed/native capture on that popup, left button released, WPF hit **TextBlock**, and `mouseOver=True`. It then recorded the real native injection; command/count/write and subsequent scenario assertions completed. Launcher PID **20364** exited 0 and cleanup was confirmed.

The local native desktop/DPI and runtime differ from the hosted runner: local phase logs use .NET **10.0.12**, whereas the hosted child used **10.0.14**. These are reproduction limits, not a demonstrated cause. No environment or branding attribution, baseline claim, or passing CI acceptance is made from local success.

Commands, from the repository root, using normal Windows permissions for native fixtures:

```powershell
$env:PTJ_NOTEBOOK_MENU_DIAGNOSTICS = '1'
$env:PTJ_TEST_RESULTS_DIRECTORY = Join-Path $PWD 'TestResults/ci109-menu/exact'
dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~OverflowTriggersToggleThroughNativeMouseAndOwnedKeyboard&DisplayName~Dark&DisplayName~1280' --logger trx --results-directory TestResults/ci109-menu/exact

$env:PTJ_TEST_RESULTS_DIRECTORY = Join-Path $PWD 'TestResults/ci109-menu/focused'
dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~OverflowTriggersToggleThroughNativeMouseAndOwnedKeyboard|FullyQualifiedName~TagMenuRoutedToggleDismissalAndCapturedOwnership|FullyQualifiedName~NotebookMenuLayoutTests' --logger trx --results-directory TestResults/ci109-menu/focused
```

Reproduction used the existing Release binaries; no new build result is claimed for this documentation-only task. Native branding/Explorer/taskbar/screen-reader acceptance remains pending and unrelated to this fixture run.

## Next evidence needed

If this case fails again, retain the automatic boundary JSON alongside `input-sequence.log`, the matching child TRX and phase/supervisor logs. Optional menu diagnostics can supplement this evidence, but are no longer needed to enable the terminal snapshot. A failure under these observations, preferably on the matching hosted runtime/desktop, can distinguish managed hover/capture, geometry, popup behavior and external ownership before choosing a correction. The original CI trace still lacks those observations; adding instrumentation does not fill its historical gaps.

The original investigation changed only `README.md` and this note. The subsequent instrumentation added `NotebookMouseReadinessEvidence.cs` and its contract tests, and updated `NotebookHeaderViewTests.Menus.cs` plus these two documentation files. Those earlier tasks performed no product correction, complete gate, workflow rerun, asset modification, push, PR or merge. The later scheduling-contract correction and its complete gate are recorded above.
