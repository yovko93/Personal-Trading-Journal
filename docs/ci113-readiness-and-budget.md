# CI #113 — Notebook hover readiness and workflow budget

Subsequent evidence: [CI #114](ci114-notebook-readiness.md) includes both corrections below and **completed** with 5,537 passed / 3 failed / 0 skipped, exit 1. Its parallel Desktop and all other parallel projects passed. Its three Notebook readiness failures are analyzed separately; this does not retroactively supply a total for the incomplete CI #113 run.

## Provenance

[Run #113](https://github.com/yovko93/Personal-Trading-Journal/actions/runs/41683107591), attempt 1, job `126081657087`, head `8f73e8d1458aced0bc22c98f2e2bbfe828ac421a`; tested merge `6bea8499f732e9948d40b2e6757ecb70edc06501`. The checkout log and snapshot assembly/GitHub SHA agree. GitHub comparison reports zero changed files between head and merge.

Artifact `13254028756`, `windows-test-results`, SHA-256 `EB865124970EC882A6ED5B809867564E739102ECD55ADA20B3434DF2E42502E2` was downloaded and verified. Evidence is retained locally under `TestResults/ci113/`, including `job.log`, ZIP/extraction and `children.json`. Hosted artifact root: `release-20261103-172134-e9deeb9fe74543d48b57df9093f4ce5a`.

## A. Native mouse-readiness assertion

The exact child is `exclusive-wpf/notebook-menus-20261103-172359-88bd19d1b71d4d32bcff74750896247a/`. Its `menu-evidence/readiness-failure-*.json` exists and was read with `input-sequence.log`, child TRX and supervision log. Capture/write/retention succeeded in this run.

The line-127 click is **DuplicateNoteButton**, not NoteOverflow. Child PID **7256**, managed STA **18**, native TID **2708**, Dark/1280/96. The 3.015-second readiness observation at `17:24:06.4275545 UTC` records:

| Observation | Recorded value |
| --- | --- |
| Intended point and native cursor readback | Both `(909,210)` |
| Target screen corners | `(799,192)` to `(1019,228)`; cursor is inside |
| Actual DPI / device transforms | 1×1 / identity; native HWND DPI 96 |
| Target state | Loaded, visible, enabled, hit-test visible, valid measure/arrange |
| Animation | Settled, opacity 1, identity transforms, no animated transform; popup animation None |
| Expected / actual point / cursor HWND | All `0x790028`, owned by testhost PID 7256 / TID 2708 |
| Foreground and fixture HWND | Both `0x1B014A`, same process/thread; fixture active |
| Managed and native capture | ContextMenu / its popup HWND `0x790028` |
| Managed mouse target | ContextMenu; target `IsMouseOver=False`, `IsMouseDirectlyOver=False` |
| Explicit WPF input and visual hit at cursor | TextBlock → ContentPresenter → Grid → MenuItemChrome → **DuplicateNoteButton** → ContextMenu → PopupRoot |
| Menu/popup lifetime | Menu open/loaded/visible; popup open, StaysOpen=True; dispatcher not shutting down; left button released |

The failed conjunct is managed menu-item `IsMouseOver`. Explicit geometry/hit testing reaches the correct descendant while WPF's maintained mouse-over target remains the captured ContextMenu. This rules out the recorded cursor being outside the item, incorrect recorded DPI conversion, or an unexpected HWND at that observation. It does not explain why managed hover remained stale/different. Capture by an open ContextMenu alone is not proof of a capture defect. No external-app/Chrome attribution is supported.

The helper asserts before native down/up injection and duplicate command/count/write assertions. Launcher PID 4300 exits 1 and is confirmed cleaned up at `17:24:06.742 UTC`; this is an assertion failure, not an aborted host, watchdog or workflow cancellation. The exclusive stage later finishes **144 passed / 1 failed / 0 skipped**. **Root cause remains unresolved; no readiness predicate, native input or product correction was made.** Earlier CI #109 evidence remains separate.

## B. Workflow deadline

| UTC | Event |
| --- | --- |
| 17:21:34 | Test step starts; 15-minute budget |
| 17:21:34.886 | Exclusive stage starts |
| 17:24:06.742 | Failed menu child exits and cleanup completes |
| 17:26:38.823 | Parallel stage starts after 303.94 seconds (5m03.94s) |
| Approximately 17:36:34 | Nominal step deadline; parallel stage had about 595 seconds (9m55s) remaining |
| 17:36:39.416 | Runner reports timeout after cancellation/termination processing |

Domain, Application and Infrastructure have final passing TRX results. Parallel Desktop has **no final TRX**, so there is **no complete-gate total**. The lone exclusive assertion is not evidence that it caused the workflow deadline.

Latest console progress identifies `IsolatedTestProcessTests.DeadlineKillsBlockedForegroundStaAndItsDescendantWithoutAnotherTestAttempt` in progress at `17:36:21.087`. Its deliberately blocked probe PID **6080** starts at `17:36:20.237`, reaches its expected 15-second timeout at `17:36:35.235`, and is confirmed exited at `17:36:35.246`. The next `OutputPumpFailurePromptlyKillsTheBlockedStaAndChildWithoutMaskingTheIoError` probe PID **7872** starts at `17:36:35.447` and is confirmed cleaned up at `17:36:35.610`. These are deliberate process-supervision tests, not a demonstrated application hang. Parent result reporting was interrupted; their final parent acceptance cannot be inferred from child cleanup alone. No retained evidence establishes a stuck production operation at cancellation.

Daily Review's child launcher PID **9628** ran once, `17:33:09`–`17:33:28`, exit 0 in **18.70 seconds**. Both parameters passed; both phase files reach `ui-cleanup.complete`. This run is not evidence of another Daily Review migration hang.

### Demonstrated unnecessary child work

After deduplicating copied supervision evidence by suite/PID/start, the parallel artifact records **77 launches**. **75** have normal exit rows (604.67 aggregate child-seconds); the two intentional failure/timeout probes above instead have confirmed-cleanup rows. Empty logs have no attributable launch. Aggregates overlap in wall time and are not a direct stage-duration sum.

| Suite | Launches | Child cases passed | Aggregate normal-exit seconds |
| --- | ---: | ---: | ---: |
| notebook-tags | 4 | 16 | 78.30 |
| notebook-workspace | 4 | 16 | 35.64 |
| notebook-autosave | 4 | 16 | 29.58 |
| notebook-editor | 4 | 16 | 24.99 |
| **Total** | **16** | **64** | **168.51** |

Each class has four parent parameterizations but previously launched its entire four-case class for every parent. This repeats **48 child-case executions**. Four launches for exact-case suites such as notebook-links and notebook-image-editor are legitimate: their child TRX files each contain one different parameter, not four repeated cases. Other repeated suite names were inspected rather than treated automatically as duplication.

The correction applies the established `ExactDisplayNameFilter` plus `expectedTestName` validation to these four classes only. Each parent still starts its own isolated process with the same 90-second outer deadline and 30-second watchdog; each child now executes only its intended parameter. The case bodies, assertions, scheduling contract and concurrency are preserved. This removes demonstrated waste; it does not prove all other work fits the hosted budget.

After that correction, one complete local gate **finished successfully in 904.72 seconds (15m04.7s)**. That measured completed execution exceeds the old outer step budget, so the workflow Test step is now **20 minutes**. This gives the complete workload headroom while retaining all per-case and child-process watchdogs. The workflow-only budget edit followed measurement; the completed local gate invoked the same test script directly and did not depend on a GitHub step timeout. No second full gate was run. Hosted timing still requires a new run; the budget edit cannot resolve the independent readiness assertion.

## Verification and remaining acceptance

Release build passed with **0 warnings / 0 errors**. The focused run passed **34 top-level cases / 0 failed / 0 skipped**: all four corrected classes, all overflow-menu parameters, diagnostic contracts, scheduling guard and process-supervision regressions. `focused-audit.json` verifies four child launches and four distinct passed one-case results for each corrected class (16 child cases total, not 64). All four native overflow cases passed locally, including Dark/1280/96; that does not resolve the recorded managed-hover discrepancy.

Evidence paths: `TestResults/ci113/build.log`, `focused.log`, `focused/`, `focused-audit.json`. The complete gate is run once after focused success. Hosted timing acceptance requires a new run after the correction. Branding and native branding manual-verification boundaries remain unchanged.

The single complete local gate exited **0**: **5,540 passed / 0 failed / 0 skipped**. Breakdown: exclusive Desktop 145; parallel Domain 602, Application 1,050, Infrastructure 1,608, Desktop 2,135. `complete-audit.json` confirms 5,540 unique top-level identities and zero duplicates. In the complete run each corrected suite again has four one-case child results, preserving all 16 intended parameterizations exactly once.

Retained run: `TestResults/ci113/complete/release-20261103-195716-e6b9fd17fd4d40d1a426e8b9a4ec7b03/`. Exclusive stage elapsed 452.03 seconds; parallel stage plus result accounting elapsed 452.69 seconds. Local timing is not an equivalent hosted-load benchmark. The historical hosted run remains incomplete and receives no invented total.

Commands from the repository root:

```powershell
dotnet build PersonalTradingJournal.sln -c Release --no-restore
$env:PTJ_TEST_RESULTS_DIRECTORY = Join-Path $PWD 'TestResults/ci113/focused'
dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~NotebookAutosaveViewTests|FullyQualifiedName~NotebookEditorViewTests|FullyQualifiedName~NotebookTagViewTests|FullyQualifiedName~NotebookViewTests|FullyQualifiedName~OverflowTriggersToggleThroughNativeMouseAndOwnedKeyboard|FullyQualifiedName~NotebookMouseReadinessEvidenceTests|FullyQualifiedName~ReleaseSchedulingTests|FullyQualifiedName~IsolatedTestProcessTests' --logger trx --results-directory TestResults/ci113/focused
./scripts/Test-Release.ps1 -ResultsDirectory TestResults/ci113/complete
```

Changed source files are `NotebookAutosaveViewTests.cs`, `NotebookEditorViewTests.cs`, `NotebookTagViewTests.cs`, and `NotebookViewTests.cs` under `tests/PersonalTradingJournal.Desktop.Tests/Notebook/`. The measured outer budget adjustment is in `.github/workflows/ci.yml`. Documentation changes are `README.md`, this guide, `docs/ci-menu-readiness.md`, and `docs/ci-daily-review-acceptance.md`. No production/branding files were changed.
