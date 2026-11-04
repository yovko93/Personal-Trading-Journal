# CI #114 — completed gate with three Notebook readiness failures

## Status — 4 November 2026

**Cause unresolved; no supported interaction correction.** Three independent isolated cases failed before native item-click injection. Their snapshots establish a discrepancy between maintained managed hover and explicit physical hit testing. They do not establish that the hover guard is invalid under legitimate menu capture. The fourth parameter passed with the same ContextMenu capture, item hover and native click delivery. No assertion, input, deadline, production, branding, child-dispatch or workflow-budget change is justified from that discrepancy alone.

Unlike the earlier incomplete CI #113 run, this run finished with **5,537 passed / 3 failed / 0 skipped**, exit **1**. Exclusive Desktop: 142 passed / 3 failed; parallel Desktop: 2,135 passed; Domain: 602; Application: 1,050; Infrastructure: 1,608, all with zero skipped. The Test step was not canceled by its workflow deadline.

## Provenance and configuration

- [CI run #114](https://github.com/yovko93/Personal-Trading-Journal/actions/runs/41711987841), attempt 1, job `126182121586`.
- Head/local checkout: `c19c839ddf41f68e0643a41049a2a9797eb0a3ae`.
- Tested merge: **`3652e5e0ccc9f633c1c5ff2c7be4b3aaf7cac50f`**, confirmed by checkout log, failure snapshots' assembly informational versions and `githubSha`. GitHub comparison against head reports zero changed files.
- The verified source includes `ExactDisplayNameFilter` plus `expectedTestName` for Notebook autosave/editor/tags/workspace and **`timeout-minutes: 20`** in the Test workflow step. Configuration is established from the tested source comparison, not inferred from dates or the earlier local gate. Four `notebook-editor` child TRX files each report one matching parameter passed.
- Artifact `13268238985`, `windows-test-results`, SHA-256 **`200DAE5640BB54ECF2CD5E8908597FC3464F404D462F7C13973C04B22DB550C4`**, downloaded and verified.
- Artifact root: `release-20261103-202504-1be5ec6b94c14e2cbd8fcb33b149426b`. Local retained evidence: `TestResults/ci114/` (ZIP/extraction, job log, snapshot summaries, full failed-child stacks and supervision logs).
- Test runtime: .NET **10.0.14**, WPF **10.0.14-servicing.26522.126+0427b9abbead3fa64c6655eb4290c537397131fa**. Local runtime is 10.0.12; local success is not an equivalent hosted-runtime reproduction.

## Three snapshots, not three inferred copies of earlier evidence

Each failing directory under `exclusive-wpf/` contains its own `notebook-menus.trx`, `menu-evidence/input-sequence.log`, `readiness-failure-*.json`, STA phase log and supervision log. The fourth JSON elsewhere under `notebook-readiness-evidence-*` is a deliberate diagnostic contract probe, not a fourth failed menu case.

| Parameter | Child directory suffix / testhost PID | Cursor | Target screen corners | Expected and actual HWND |
| --- | --- | --- | --- | --- |
| Light / 1280 / 96 | `20261103-202721-41d9babd3ffc4e4295b4742edd0c0883` / 2172 | 909,210 | 799,192 to 1019,228 | `0x180160` |
| Dark / 1280 / 96 | `20261103-202728-bc53cb7ad2234f62859e5062dc4aa833` / 9104 | 909,210 | 799,192 to 1019,228 | `0x1E0160` |
| Light / 480 / 240 | `20261103-202735-fb448297aa1f4b3bb2eafced0f2d549e` / 2276 | 517,258 | 407,240 to 627,276 | `0x230160` |

The supplied parameter DPI is a fixture/render parameter. Every failing popup's actual native DPI is **96**, WPF scale **1,1**, transforms identity. Native cursor readback equals the intended point and lies inside the target. All targets are loaded, visible, enabled, hit-test visible, measure/arrange valid; animations are settled. Each expected HWND equals HWND-at-point and HWND-at-cursor, belongs to the same testhost as the foreground fixture, and is its owned popup. No unexpected/Chrome HWND is recorded.

All three snapshots identify **DuplicateNoteButton**. Tested `NotebookHeaderViewTests.Menus.cs:127` invokes `MouseClick(duplicate)` after reopening NoteOverflow on line 125. Failure is in `NotebookMouseReadinessEvidence.AssertReady:23`, through the helper at Menus.cs:207. `IsMouseOver` and `IsMouseDirectlyOver` are false; `Mouse.DirectlyOver` and `Mouse.Captured` are the ContextMenu. Both explicit input and visual hit ancestry at the read-back cursor traverse TextBlock → ContentPresenter → Grid → MenuItemChrome → **DuplicateNoteButton** → ContextMenu → PopupRoot.

The menu is open, loaded and visible; popup open, animation None, StaysOpen true; native capture belongs to its popup; left button is released. The failed condition is managed item hover. These are normal assertion exits with confirmed launcher cleanup and STA shutdown true/true, not child watchdogs, cancellation, or three observed command failures. No native duplicate down/up or duplicate-command/count/write assertions were reached.

This matches CI #113's Dark/1280/96 discrepancy, but the three new snapshots independently establish it for this run. CI #113's workflow cancellation remains a separate historical incident with no complete total.

## Lifecycle and hypotheses

The tested scenario previously completes repeated native open/close, outside dismissal, Escape/focus restoration, keyboard opening and Refresh interactions. It returns to the note panel and natively clicks NoteOverflow. The input log records the trigger's down/up, `Opened`, `Click IsOpen=True`, and capture changing from the trigger Button to the eight-item ContextMenu. The helper then moves the native cursor to the duplicate item and pumps its existing bounded three-second readiness frame. Its final records have correct physical hit testing but false managed hover.

Product inspection: `NotebookWorkspaceView.Menus.cs` tracks same-gesture outside dismissal, toggles IsOpen, releases other tag menus and sets placement. It does not forcibly capture the mouse. `NoteActions.cs` binds ownership-guarded commands during Opened. There is no evidence here of an incorrectly retained previous menu or an ownership guard rejecting the duplicate command, which was never invoked.

Framework source corroborates that menu capture is normal: [WPF MenuBase](https://github.com/dotnet/wpf/blob/v10.0.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/Primitives/MenuBase.cs) takes subtree capture in menu mode. [MouseDevice](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/Input/MouseDevice.cs) distinguishes element and subtree capture; subtree routing hit-tests descendants. These public source references describe the mechanism; they are not proof of the internal state of this particular servicing binary.

The **Dark/480/240** parameter passed in the same hosted run. Its log records duplicate readiness at **517,258**, `IsMouseOver=True`, ContextMenu capture still active, then real native down/up events with TextBlock as OriginalSource and the existing duplicate persistence assertions completing. Thus ContextMenu capture does not universally prevent descendant hover and cannot alone justify dropping the guard.

Remaining hypotheses include pending/misprocessed native movement, capture-mode/routing differences, or another input-state issue. The snapshots record neither the processed WM_MOUSEMOVE/raw-input sequence nor WPF's cached mouse position/capture mode at that boundary. They cannot show whether a native click in the false-hover state would reach the item, because the guard stops it. Explicit geometry alone proves the physical hit, not native routed delivery under capture. A replacement predicate would need a controlled reproduction plus native item down/up and command evidence in that state. Neither a capture-aware production/fixture correction nor a regression asserting that mechanism is currently supported.

## Verification and decision boundary

One bounded local reproduction uses the existing isolation workflow, unchanged native input/assertions, 90-second child deadline, 30-second watchdog and three-second readiness bound. Optional menu diagnostics are enabled. It covers all four overflow parameters plus tag-menu capture, diagnostic contract, layout and scheduling checks. No automatic retry or unchanged full-gate run is used to obtain acceptance.

```powershell
dotnet build PersonalTradingJournal.sln -c Release --no-restore
$env:PTJ_NOTEBOOK_MENU_DIAGNOSTICS = '1'
$env:PTJ_TEST_RESULTS_DIRECTORY = Join-Path $PWD 'TestResults/ci114/focused'
dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~OverflowTriggersToggleThroughNativeMouseAndOwnedKeyboard|FullyQualifiedName~TagMenuRoutedToggleDismissalAndCapturedOwnership|FullyQualifiedName~NotebookMouseReadinessEvidenceTests|FullyQualifiedName~NotebookMenuLayoutTests|FullyQualifiedName~ReleaseSchedulingTests' --logger trx --results-directory TestResults/ci114/focused
```

Release build: **0 warnings / 0 errors**. The focused run passed **17 top-level cases / 0 failed / 0 skipped**, with **7 matching isolated child cases passed** (4 overflow parameters, 2 tag-menu cases, 1 synthetic diagnostic probe). Counts are separate; child evidence is not added to top-level totals. All three hosted failing parameters therefore pass unchanged locally, as does Dark/480/240. TRX identities/counters are retained in `focused-results.json`. No source correction or complete local gate is claimed for this investigation. The precise remaining requirement is reproducing false hover with native movement/capture-state tracing on the matching hosted runtime/desktop, then proving which interaction behavior requires correction. Branding, assets, exact-case dispatch and the 20-minute workflow budget remain intact.

Changed files: `README.md`, `docs/ci-menu-readiness.md`, `docs/ci113-readiness-and-budget.md`, and this guide. The investigation stops at the supported evidence boundary rather than weakening native coverage, forcing capture release, adding delays/retries, or repeating a full gate for a green result. No push, PR, merge or hosted rerun was performed.
