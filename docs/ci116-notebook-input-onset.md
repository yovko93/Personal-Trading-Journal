# CI #116 — Notebook movement trace and missing onset

## Status — 4 November 2026

**Interaction cause remains unresolved.** This investigation establishes that repeated native movement reached the popup and WPF's reported position matched the OS position. It also establishes a diagnostic retention defect: the rolling buffer discarded the initial movement/processing interval. Only that retention and observation gap is corrected; native input, readiness predicate, command assertions and deadlines are preserved. No unchanged complete gate was rerun.

This run completed with **5,540 passed / 1 failed / 0 skipped**, exit 1. Exclusive Desktop: 144/1; parallel Desktop: all 2,136 passed; Domain 602, Application 1,050 and Infrastructure 1,608 passed. There was no workflow timeout. The Calendar ownership correction and its controlled regression are included; the remaining failed parent is only Dark/1280/96 overflow native readiness.

## Provenance and actual files

- [CI run #116](https://github.com/yovko93/Personal-Trading-Journal/actions/runs/41841157193), attempt 1, job `126612775043`.
- Head/local base: `d51f4ba5c08a223d01dfff9fa54f05b6074909bd`.
- Tested merge: **`c1894c2f533cff7bc2b357aa172a75c41f438ffd`**, confirmed by job checkout, snapshot GitHub SHA and assembly informational version. GitHub comparison with head reports zero changed files.
- Matching source contains Calendar `ReadCancellation`, its controlled regression, `NotebookMouseMovementTrace`, single-parameter dispatch and the 20-minute workflow Test budget. These are checked in source, not inferred from line numbers or earlier gate results.
- Artifact `13329473580`, `windows-test-results`, ZIP SHA-256 **`6BE9B8CB401B2AC7873CC1E9173DF498208246361ABA11D530625684B024D80F`**, retrieved and verified.
- Artifact root: `release-20261104-134813-5d9d3a0ae17849339f315c2dc7f8d5b0`.
- Failing child: `exclusive-wpf/notebook-menus-20261104-135013-f107240a6b1a4ed493ec51f13854e67e`.
- Actual retained files include `notebook-menus.trx`, `stdout.log`, `stderr.log`, `process-supervision.log`, `calendar-sta-phases.log`, `menu-evidence/input-sequence.log`, **`menu-evidence/mouse-movement.log`** and **`menu-evidence/readiness-failure-5748-d7076a6555194ca5bbc269747bfbd854.json`**.
- Local evidence: `TestResults/ci116/` retains ZIP/extraction, job log, `duplicate-movement.log` and `trace-audit.json`; verification logs are under `verification/`.

The actual filenames follow `NotebookMouseReadinessEvidence.Retain` and `NotebookMouseMovementTrace.Dispose`. Both files exist in this child; no capture/write success is assumed from the stack alone.

## Reconstructed sequence

Tested Menus.cs:127 still calls **`MouseClick(DuplicateNoteButton)`**. Helper line 208 calls `NotebookMouseReadinessEvidence.AssertReady`; the duplicate native down/up injection and duplicate-command/count/write assertions are not reached. Testhost PID 5748, STA 18, native TID 8424. Launcher PID 7808 exits 1 and is confirmed cleaned up at `13:50:20.019 UTC`.

| UTC | Retained observation |
| --- | --- |
| 13:50:16.732–16.739 | The final NoteOverflow press/release is processed. Button Element capture is lost; Path movement is processed; ContextMenu receives **SubTree** capture. |
| 13:50:16.739686–16.739704 | Processed PreviewMouseMove/MouseMove now target ContextMenu while OS cursor remains **933,137**, on the trigger outside the new popup. This transition alone is expected under menu capture, not proof of a defect. |
| 13:50:16.740237 | The trigger MouseUp is handled; menu remains captured. |
| 13:50:16.741047 | The trigger's trace detaches. Its hook covered the owner HWND; the duplicate helper subsequently installs its own hook on the target source. |
| Initial duplicate interval | **Evicted by the 64-row rolling buffer.** Attachment, first requested target movement, first popup message and any initial processed movement are unavailable. |
| 13:50:18.393170 | First surviving duplicate row: native **WM_MOUSEMOVE** to popup **0x1F00B4**, client **115,60**, OS **909,210**, managed target-relative **110,18.38**. Capture is ContextMenu/SubTree; hover remains false. |
| 13:50:18.393–19.737 | **63 retained WM_MOUSEMOVE rows**, all at the same correct coordinates, same popup, same managed position and false hover. No processed movement rows survive in this retained tail. |
| 13:50:19.752613 | Readiness observation is false at elapsed **3.0109367 seconds**. |
| 13:50:19.755229 | Boundary JSON records the same pointer state, settled layout, correct explicit item hit and open menu. |
| 13:50:19.767152 | Trace detaches, still ContextMenu/SubTree, hover false. |

The helper calls `SetCursorPos` before layout and on each readiness tick. Each return is asserted true. Reaching the final readiness assertion implies the executed cursor calls did not fail their assertions; the original trace does not directly log individual request results or their count. The late native messages directly prove popup delivery during that surviving interval. They do not prove coverage of the erased initial interval.

The snapshot confirms target bounds **799,192–1019,228**, OS/intended cursor **909,210**, native DPI **96**, scale **1,1**, identity device transforms. Managed position relative to the source root is **115,60**, matching the native message; relative to the target it is **110,18.38**. Expected/window-at-point/window-at-cursor HWNDs all equal **0x1F00B4**, owned by the testhost. Foreground and fixture are **0x19020E** in that same process. Target is loaded, visible, enabled, hit-testable and layout-valid; menu open/loaded/visible, popup animation None, left button released, dispatcher not shutting down. Explicit input/visual hits traverse TextBlock → MenuItemChrome → DuplicateNoteButton → ContextMenu → PopupRoot. Managed hover and capture remain ContextMenu, with **SubTree** mode now directly observed.

The earliest *retained* divergence for the duplicate item is therefore at **18.393170**: correct native movement/managed position without the expected item hover. The actual onset lies in the discarded interval; its first cause cannot be named. Zero processed moves in the retained tail is **not** proof that the initial movement was never processed. Repeated same-coordinate native reports may also be filtered/coalesced without new routed moves. Capture exists in passing cases and remains insufficient grounds to remove `IsMouseOver`.

## Small diagnostic correction

The original duplicate trace contains exactly **64 rows: 63 late native moves and detachment**, no attachment row. Its first surviving row is roughly 1.65 seconds after the inferred start of the three-second readiness wait. Thus the instrumentation ran, but its retention policy destroyed the required onset. This is distinct from missing OS input and from missing hook coverage.

The existing recorder now retains **first 16 + last 48 observations**, within the same 64-observation memory bound, plus one summary row with total and omitted-middle counts. Attachment now records the hooked HWND and native/processed-hook status. The caller records the requested point and actual return from its **existing** `SetCursorPos` call. No extra movement call, dispatcher work, capture change, sleep, retry or runner heartbeat is introduced. Output still uses the same `mouse-movement.log`; no parallel instrumentation layer is added.

`MovementFloodRetainsOnsetAndDeadlineWithExplicitOmissionCount` feeds 205 observations through the recorder, including attachment, initial request/native/processed movement and a repeated-move flood. It verifies the initial sequence survives, the final deadline survives, storage is bounded and 141 middle observations are explicitly marked omitted. This targets the demonstrated retention failure; it is not a claimed reproduction of the interaction failure.

There is no capture-aware readiness change and no experiment clicking after failed readiness. Such a change still needs native delivery/expected-operation evidence in the false-hover state. Preserving onset is the smallest correction supported by this run; the next failing trace must establish what happened when movement first entered the item.

## Verification

Release build: **0 warnings / 0 errors**. Focused run: **19 top-level cases / 0 failed / 0 skipped**, plus **7 matching isolated child cases passed** (4 overflow, 2 tag menus, 1 diagnostic popup). The selection includes the new retention regression, all four menu parameters, related input/layout/diagnostic/scheduling checks and the controlled Calendar cancellation-owner regression. Native clicks and duplicate-operation assertions passed locally; this does not reproduce the hosted false-hover state. Generated trace files were inspected for attachment HWND/coverage, cursor-request results and retention summaries.

```powershell
dotnet build PersonalTradingJournal.sln -c Release --no-restore
$env:PTJ_TEST_RESULTS_DIRECTORY = Join-Path $PWD 'TestResults/ci116/verification/focused'
dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~OverflowTriggersToggleThroughNativeMouseAndOwnedKeyboard|FullyQualifiedName~TagMenuRoutedToggleDismissalAndCapturedOwnership|FullyQualifiedName~NotebookMouseReadinessEvidenceTests|FullyQualifiedName~NotebookMenuLayoutTests|FullyQualifiedName~ReleaseSchedulingTests|FullyQualifiedName~CompletedLoadCannotDisposeCancellationStillOwnedByDeactivate' --logger trx --results-directory TestResults/ci116/verification/focused
```

No complete gate was rerun because no interaction correction was established. The prior local Calendar-correction gate remains historical evidence, not acceptance of the new diagnostics. Calendar fix, dispatch correction, workflow budget, branding and assets are unchanged. Updated hosted verification of the recorder, and a false-hover trace retaining the onset, remain pending.

Changed files: `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookMouseMovementTrace.cs`, `NotebookHeaderViewTests.Menus.cs`, `NotebookMouseReadinessEvidenceTests.cs`, `README.md`, `docs/ci-menu-readiness.md`, `docs/ci115-calendar-and-notebook.md` and this guide. No push, PR, merge or hosted rerun was performed.
