# CI #109 — Duplicate menu-item readiness

## Current status — 2 November 2026

**Intermittent failure, unresolved cause; not fixed.** The exact failing case passed unchanged locally. All menu parameterizations and related tag-menu/layout regressions also passed. No production, fixture, input policy, assertion, timeout, concurrency or branding change was justified. No complete gate or hosted rerun was triggered because there was no verified correction.

The hosted result remains **5,534 passed / 1 failed / 0 skipped**, exit 1. The failed parent and its matching failed isolated child describe **one failed top-level case**, not two.

## Tested source and retained evidence

- [CI run #109](https://github.com/yovko93/Personal-Trading-Journal/actions/runs/41446986433), attempt 1, PR #27, job `125282451335`.
- Branch/current clean checkout: `bc083bc31ead006e8fb6fdcf493cff2ee017ff25` (approved Bull and Bear artwork).
- Job checkout tested merge `5280345666314ebb899de38daa4b342718801c1b`. The GitHub comparison against the current checkout reports **zero changed files**. Source mapping below therefore applies to the exact tested file, not an assumed line match.
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

If this case fails again, retain its native readiness boundary with the existing optional menu diagnostics enabled, including WPF hit target, item/layout bounds, cursor readback, expected/actual HWND process/class, foreground, numeric DPI and capture/popup state. Collect within the current deadlines and preserve the actual native-click/toggle coverage. A failure under those observations, preferably on the matching hosted runtime/desktop, can distinguish managed hover/capture, geometry, popup behavior and external ownership before choosing a correction.

Changed files in this task: `README.md` and this note only. No product/fixture fix, complete gate, workflow rerun, asset modification, push, PR or merge was performed.
