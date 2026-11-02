# Application Branding

## Current Release status — 2 November 2026

The latest local complete Release gate passed **5,535/5,535**, zero failures/skips, exit 0. All 25 failures from the previous gate passed unchanged both in the affected-group run and in this complete rerun. Retained dumps and phase logs identify primary construction/layout/render deadlines and an aggregate suite timeout, rather than a demonstrated shared owner-close cleanup defect. No production or fixture correction was justified; the underlying historical timing variation remains unproven.

See [Release gate lifecycle investigation](release-gate-lifecycle.md) for exact parent/child identities, first failures, completed stages, dump stacks, cleanup distinctions, focused coverage and final totals. That current result supersedes the failed local gate as the latest automated status; the historical results below are preserved. The branding assets and implementation are unchanged. Native branding, Explorer/taskbar and screen-reader checks remain pending; no screenshot retry or hosted run occurred.

## N26 Emerald Bull

Implemented on 1 November 2026 in the existing checkout, starting from clean commit `1fe01fc`. The supplied branding-package README and patch were reference material; its branch-creation instructions were not followed. The selected artwork is copied unchanged, including its lettering and transparency.

## Verification follow-up — 2 November 2026

Current source: clean `c7cadee20acff1f1547c305f971ec903f84bc536` before this documentation update. This commits the previously tested branding implementation. The original run tested the uncommitted branding changes on `1fe01fc`; no original per-binary source attestation was retained, so this is source-history attribution rather than a newly inferred binary identity. The Notebook fixture sources have no diff between those commits.

The five original failures were **assertions at native input readiness**, before injecting the intended mouse click. They were not product toggle assertions, migration/setup exceptions, process timeouts, hang-watchdog expirations, or cancellation. Each isolated child produced a final failed TRX, exited with code 1, and was confirmed exited by its supervisor. The parent testhost was PID **21984** (VSTest console **21188**); each parent failure propagated the corresponding child failure through `IsolatedTestProcess.RunSuiteAsync`, line 48. Parent testhost exit 0 indicates normal host termination, not a passing test run.

All names below belong to `PersonalTradingJournal.Desktop.Tests.Notebook.NotebookHeaderViewTests`. Theory values are **theme / logical width / render DPI**; the original native desktop was actually at DPI scale 2.5, independently of each render-DPI parameter.

| Exact method and theory | Child launcher PID | Child directory suffix | Failure site / direct evidence |
| --- | ---: | --- | --- |
| `OverflowTriggersToggleThroughNativeMouseAndOwnedKeyboard("Light", 1280, 96)` | 24544 | `183555-db2db806b66b4d6fbca3feb7140be57d` | `Menus.cs:199`, called at line 98. Target `NoteOverflow` at (2908,375), expected HWND 919512, actual HWND 131864, title `Chrome Legacy Window`; `settled=True`, `mouseOver=False`, `hit=False`. |
| `OverflowTriggersToggleThroughNativeMouseAndOwnedKeyboard("Dark", 1280, 96)` | 22956 | `183603-c495f1a45d244e0ba6cd141ed7a79872` | Same stack sites and point, expected HWND 2426456, actual Chrome HWND 131864; settled true, hit false. |
| `OverflowTriggersToggleThroughNativeMouseAndOwnedKeyboard("Light", 480, 240)` | 21288 | `183611-0c5a75678ce94d6ebb8d7c4566adcdf2` | Same stack sites; point (1096,514), expected HWND 1312778, actual Chrome HWND 131864; settled true, hit false. |
| `TagMenuRoutedToggleDismissalAndCapturedOwnership("Light", 1280, 96)` | 17772 | `183619-c2e284f4b8794ce6989a9b42d4e5fdb2` | `TagMenus.cs:130`, called at line 56. First Beta trigger click, point (736,1100), expected HWND `0x270658`, child testhost PID 3552. Hit `0x20318` / Chrome renderer PID 14560; foreground `0x20284` / same Chrome PID. |
| `TagMenuRoutedToggleDismissalAndCapturedOwnership("Dark", 480, 240)` | 21588 | `183627-25b34531d7b9414f94273ac6a72cd6c3` | Same stack sites; point (1096,1239), expected HWND `0x16080A`, child testhost PID 18936; same Chrome renderer/foreground as above. |

The overflow failure message is **“The intended control did not become hit-testable with settled popup animation and mouse-over.”** The tag failure message is **“Intended control does not own the native target coordinates.”** Full child stacks continue through `CalendarStaTest.ExecuteAction` lines 124/140 and `RunAsync` line 89; parent stacks continue through the theory dispatch at `Menus.cs:31` or `TagMenus.cs:27`. Exact complete stacks, TRX paths, messages, and outcomes are copied into `TestResults/n26-verification/original-failures.json`.

Original evidence root: `TestResults/n26-branding/complete/release-20261101-183214-d054ff7ba4d749ecacd6b525ae1d22fd/exclusive-wpf/`. The parent TRX is `Yovko_DESKTOP-N2ONT35_2026-11-01_20_33_26_net10.0.trx`. Child folders use `notebook-menus-20261101-` or `notebook-tag-menus-20261101-` plus the suffix above. Each contains its final named TRX, `stdout.log`, `stderr.log`, and `process-supervision.log`; native observations are in `menu-evidence/input-sequence.log` for overflow cases and `input-sequence.log` for tags. `vstest.log` and the parent host log record the parent process lifetime. No native dump is needed to establish these managed assertion sites.

**Established cause:** the intended native HWND did not own the injection coordinates; Chrome owned them. Local WPF `IsActive=True` did not establish desktop foreground/hit ownership. The retained evidence does not establish why Chrome was above those points (for example, user interaction versus denied foreground activation); no such deeper cause is claimed. These fixtures host Notebook directly without `MainWindow`. They are not described as pre-existing failures: no pre-branding baseline run was performed.

### Focused rerun and correction decision

Using the established parent-to-isolated-child workflow, normal Windows permissions, and the existing `PTJ_NOTEBOOK_MENU_DIAGNOSTICS=1` option, all five failed identities passed unchanged, alongside the sixth menu theory: **6/6 parents and 6/6 matching children**, zero failures/skips, exit 0. Parent and child totals are separate. Evidence: `TestResults/n26-verification/reproduce/`, including the final parent TRX and per-child native logs. Those logs show the fixture's actual HWND owning the target point and foreground during input. The selection included one extra control case, Dark/480/240 overflow; it was not a five-case-only run.

No production or fixture correction is justified by this result. No assertions, timeouts, skips, concurrency, assets, layout, icon configuration, or foreground-activation policy were changed. An isolated pre-branding comparison was unnecessary for this correction decision because the failure did not reproduce on unchanged current source; this does not substitute for baseline evidence or prove immunity to future desktop interference.

The follow-up Release build passed with zero warnings/errors. Reinspection of the rebuilt EXE and retained published EXE again matched all ten supplied ICO frames. Native screenshots were not retried. All interactive checks remain pending.

The complete gate finished with **5,510 passed / 25 failed / 0 skipped**, exit 1, with all five original Notebook failures passing. All five expected final parent TRX files and the gate summary are present. The four exclusive-partition failed parents are `TradingPlanWorkspaceViewTests.FullPageBindingsLifecycleGuardsFocusAndLightDarkResponsiveLayouts`, at `(980,96)`, `(1280,144)`, `(1600,192)`, and `(460,240)`. They all await the same `Lazy<Task>` child suite, so these are four propagated parent failures from **one child launch**, not four crashes.

The child directory is `complete/release-20261101-225400-37dc24757f5248f5b20c6efa5e3ee29c/exclusive-wpf/plan-workspace-20261101-230026-74bc383fa6ee4a26b0c9fb5561908446/` under `TestResults/n26-verification/`. Its launcher PID was **7936**, testhost **21576**, with the unchanged 90-second supervisor and 30-second inactivity watchdog. VSTest reported the 30-second inactivity limit, collected `testhost_21576_20261102T010105_hangdump.dmp` and a Sequence XML, then reported the child run aborted/host exited. The supervisor confirmed exit 1 and cleanup at `23:01:10.773… UTC`. Parent stacks end at `IsolatedTestProcess.cs:48` and `TradingPlanWorkspaceViewTests.cs:40`.

This is a **watchdog-triggered child abort**, distinct from the original five managed readiness assertions; no user cancellation or spontaneous product crash is established. Child phase evidence reaches test action on STA 23 at `23:00:49.218 UTC`; it does not identify the blocking stack or why total inactivity exceeded the limit. The dump has been retained but not analyzed. No cause beyond the watchdog boundary, or pre-existing-baseline attribution, is claimed. No timeout, assertion, concurrency, or production behavior was changed in response.

### Final gate totals and unresolved failures

| Partition / project | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Exclusive WPF / Desktop | 136 | 4 | 0 |
| Parallel / Domain | 602 | 0 | 0 |
| Parallel / Application | 1,050 | 0 | 0 |
| Parallel / Infrastructure | 1,608 | 0 | 0 |
| Parallel / Desktop | 2,114 | 21 | 0 |
| **Total** | **5,510** | **25** | **0** |

The run lasted about 24 minutes 29 seconds, exceeding the existing hosted CI Test-step budget of 15 minutes. No hosted run was triggered and its budget was not changed. This local failed gate does not establish hosted acceptance.

| Unresolved method / family (Desktop tests) | Failed parents | Observed failure category |
| --- | ---: | --- |
| `TradingPlans.TradingPlanWorkspaceViewTests.FullPageBindingsLifecycleGuardsFocusAndLightDarkResponsiveLayouts` | 4 | One child inactivity-watchdog abort, detailed above. |
| `TradingPlans.SessionBriefCompositionTests.ProductionShellDraftReadyAndInvalidComposition` | 4 | Shared `brief-composition` child inactivity-watchdog abort; dump retained. |
| `Journals.JournalHistoryViewTests.CompiledHistoryPagesAndSnapshotsWrapInBothThemesAndRemainReadOnlyAtHighDpi` | 1 | `journal-history` child inactivity watchdog and aborted run; dump retained. |
| `Breakdown.BreakdownTableViewTests.BoundTableSortSelectionCoverageVirtualizationAndReachabilityInBothThemes` | 1 | Child `Calendar STA timed out in phase 'test action'`, thread 19; inactivity dump also collected. |
| `Rules.RulesDropTransitionTests.PendingDropRetainsLiftAndVerifiedMoveRetainsCardsFocusAndBothScrollPositions` | 1 | Child `Calendar STA timed out in phase 'test action' (Rules drop retained cards and scrolling)`, thread 19, with inner operation timeout. |
| `Navigation.MainWindowViewModelTests.CalendarOwnerCloseProtectsDirtyJournalAndClosesCleanWithoutDuplicatePrompt` | 3 | Shared `calendar-owner-close` child exceeded its 60-second supervisor limit. |
| `Navigation.MainWindowViewModelTests.CalendarOwnerCloseWaitsForEveryGuardBeforeEndingAssessmentModal` | 10 | Same shared child supervisor timeout. |
| `Navigation.MainWindowViewModelTests.CalendarOwnerCloseStandaloneJournalStillUsesItsOwnGuard` | 1 | Same shared child supervisor timeout. |

The Calendar owner-close exception records launcher PID **5620** and directory `parallel/calendar-owner-close-20261101-231232-11abfc5f99144f6586484183aefda8ca/`. Its 14 parent failures do not imply 14 child launches. The other child evidence directories are `parallel/brief-composition-20261101-230502-a1b09a13bd914696911937e291dd5c20/`, `parallel/journal-history-20261101-230406-deedfeb22ddc434bb4b38260e5ebff02/`, `parallel/breakdown-table-20261101-231043-46ffe542c67247a0876c9035ed4633e3/`, and `parallel/rules-drop-20261101-230500-a61e768873a245d6a6bba4471acc5dd0/` under the current run. Exact final identities, messages, parent TRX paths, and complete stacks are retained in `TestResults/n26-verification/final-failures.json` and `final-failed-identities.txt`.

These new failures remain unresolved. Their timeout mechanisms are observed; their underlying blocking causes and any relationship to branding have not been established. They were not corrected speculatively or labeled pre-existing. No cancellation was issued by this task. Passing the original five does not supersede these failed full-gate results.

Only `README.md` and `docs/application-branding.md` change in this follow-up. Branding assets, sidebar layout, embedded icons, production code, fixtures, deadlines, and concurrency are preserved. No push, PR, merge, or native screenshot retry was performed.

| Asset | Properties | SHA-256 |
| --- | --- | --- |
| [Sidebar PNG](../src/PersonalTradingJournal.Desktop/Resources/Branding/ptj-logo-n26.png) | 1254 × 1254, 8-bit RGBA | `52511600342FB22C4C6282055899BC532B3B781D30AE569621415E83E63CC4EE` |
| [Windows ICO](../src/PersonalTradingJournal.Desktop/Resources/Branding/ptj-app-n26.ico) | 16, 20, 24, 32, 40, 48, 64, 96, 128, and 256 pixel frames | `964C1CA092D69D60B6DF1445D22CB568F952A298179B3BA0B3868A5568B859D3` |

## Sidebar and accessibility

The existing sidebar header contains a 48 × 48 DIP image followed by a 12 DIP gap and the application name. `Stretch="Uniform"` preserves the artwork's aspect ratio; high-quality bitmap scaling handles display density changes. The title retains its `ApplicationTitle` binding, existing section-title style, dynamic sidebar text brush, and natural wrapping. The sidebar remains 252 DIP wide, with the same navigation structure and scrolling behavior.

`DecorativeImage` supplies a WPF automation peer excluded from both the control and content views. The image cannot receive keyboard focus or pointer input; the adjacent application name conveys its meaning. This replaces the package's `AutomationProperties.AccessibilityView` suggestion, which is not a supported WPF property.

## Embedded resources and Windows icon

The Desktop project declares exactly one WPF `Resource` item per asset and removes the corresponding default `None` items. Assembly-qualified `pack://application:,,,/PersonalTradingJournal.Desktop;component/Resources/Branding/…` URIs load the PNG and ICO independently of the working directory.

`ApplicationIcon` embeds the supplied ICO into the Windows apphost executable. `MainWindow.Icon` loads the embedded ICO for the main window's title bar and normal Windows taskbar/Alt+Tab representation. No loose PNG or ICO is needed beside the executable.

## Historical verification — 1 November 2026

Evidence is retained locally under `TestResults/n26-branding/` (ignored build/test artifacts, not committed documentation assets).

| Check | Result |
| --- | --- |
| Supplied asset identity | Source and repository SHA-256 hashes match for both files. |
| Decode and transparency | WPF decodes the PNG and all ten ICO frames; decoded frame sizes and transparent pixels are asserted. Embedded resource hashes match the originals. |
| Release build | Passed with zero warnings and errors. |
| Header rendering | All 19 isolated header cases passed, including four new branding cases covering Light/Dark at 96/240 DPI (100%/250%). Four sidebar renders were inspected with no title clipping or overlap. |
| Working-directory independence | Compiled shell and both resource streams load while the test's current directory is an empty temporary directory. |
| Accessibility and layout | Logo is absent from UIA control/content views and cannot focus; 48 DIP image, 12 DIP gap, 252 DIP sidebar, full title height, binding, and theme updates are checked. |
| Built EXE icon | Windows resource inspection found all ten `RT_ICON` payloads and matching `RT_GROUP_ICON` entries. Each frame's bytes and metadata match the supplied ICO exactly. |
| Publish | No existing publishing profile was found. A default framework-dependent Release publish passed; its EXE also contains all ten exact icon frames and its output has no loose PNG/ICO assets. The published app started from a separate empty working directory with an isolated profile, and Windows automation discovered its main window. Native visual acceptance remains pending. |
| Shell/navigation checks | Passed with normal Windows permissions: 227/227 parent tests, zero failures/skips. The isolated header child passed 19/19 cases; child totals are not added to parent totals. |
| Complete Release gate | Completed with exit 1: **5,530 passed / 5 failed / 0 skipped**, 5,535 parent tests total. All five failures are in existing Notebook native-menu cases, detailed below. No timeout or existing assertion changes. |
| Native application visuals | Capture blocked twice: `FrameArrived timed out: timed out waiting on channel`, then `window capture timed out: timed out waiting on channel` after fresh selection/activation. No usable native screenshot was obtained. Automated WPF renders are not native screenshots or interactive verification. |

The first build rejected the package's unsupported accessibility property; the WPF peer implementation corrected it. An initial new-test expectation incorrectly assumed the PNG was 1280 pixels wide; the actual supplied IHDR is 1254 × 1254, and the assertion now matches it without changing the asset. After those corrections, the restricted focused run passed 225/227 parents, with two Daily Review cases unable to read their synthetic protected credential storage. The final normal-permission run passed all 227 parents. Earlier failures remain in the evidence directory.

### Complete gate results

Run: `complete/release-20261101-183214-d054ff7ba4d749ecacd6b525ae1d22fd/`. All five expected final parent TRX files and the runner's `summary.json` are present. Child executions are evidence, not extra cases in the totals.

| Partition / project | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Exclusive WPF / Desktop | 135 | 5 | 0 |
| Parallel / Domain | 602 | 0 | 0 |
| Parallel / Application | 1,050 | 0 | 0 |
| Parallel / Infrastructure | 1,608 | 0 | 0 |
| Parallel / Desktop | 2,135 | 0 | 0 |
| **Total** | **5,530** | **5** | **0** |

The five failed parents each have a corresponding failed isolated child:

- `NotebookHeaderViewTests.OverflowTriggersToggleThroughNativeMouseAndOwnedKeyboard`: Light/1280/96, Dark/1280/96, and Light/480/240. The intended control did not become hit-testable with settled popup animation and mouse-over.
- `NotebookHeaderViewTests.TagMenuRoutedToggleDismissalAndCapturedOwnership`: Light/1280/96 and Dark/480/240. The intended control did not own the native target coordinates.

These existing fixtures host Notebook views directly, without the branded `MainWindow`. Their failure cause was not established by this task; no clean-baseline comparison or assertion/timeout changes were made. The complete gate remains failed despite the passing branding and shell checks. All 19 isolated header cases also passed inside this full run.

### Native verification boundary

Sandbox-launched trial processes started but were not discoverable by the computer-use tool. Launching on the interactive Windows desktop made the exact published executable discoverable as **Personal Trading Journal**. Its startup log confirms application initialization. The two supported capture attempts then timed out, so Light/Dark native appearance, actual monitor scaling, title-bar/Alt+Tab/taskbar presentation, and screen-reader behavior remain unverified. The task-owned process was stopped after capture recovery failed; the isolated profile is retained, and the user's normal journal was not opened.

`summary.json`, `native-result.json`, `asset-hashes.json`, build/test logs, render PNGs, and executable-resource inspection results under `TestResults/n26-branding/` retain the evidence. No push, pull request, or merge was performed.

### Commands

From the repository root in PowerShell:

```powershell
dotnet build PersonalTradingJournal.sln --configuration Release

$env:PTJ_HEADER_RENDER_DIRECTORY = Join-Path $PWD 'TestResults/n26-branding/renders'
$env:PTJ_TEST_RESULTS_DIRECTORY = Join-Path $PWD 'TestResults/n26-branding/focused-windows'
dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~MainWindowViewModelTests' --logger trx --results-directory TestResults/n26-branding/focused-windows
Remove-Item Env:PTJ_HEADER_RENDER_DIRECTORY

pwsh -NoProfile -File scripts/Test-Release.ps1 -ResultsDirectory TestResults/n26-branding/complete

dotnet publish src/PersonalTradingJournal.Desktop/PersonalTradingJournal.Desktop.csproj -c Release --no-build --no-restore -o TestResults/n26-branding/publish
```

The local `inspect-icons.ps1` evidence script uses the Windows resource loader in data-file mode to compare group entries and SHA-256 hashes of all embedded icon payloads against the source ICO. Its results are in `exe-icons.json` and `published-exe-icons.json`.

## Remaining manual checks

- Inspect the actual app header in Light and Dark themes at Windows 100% and 250% scaling.
- Inspect the native title-bar, Alt+Tab, and normal taskbar icons, including the published executable launched from a separate working directory.
- Inspect both built and published EXE icons in Explorer.
- Confirm with a screen reader and Tab navigation that the decorative image introduces neither a focus stop nor a duplicate accessible announcement.
- Existing pinned shortcuts may retain a Windows-cached icon; check a fresh launch separately from an old pinned shortcut.

## Changed files

- `README.md`
- `docs/application-branding.md`
- `src/PersonalTradingJournal.Desktop/MainWindow.xaml`
- `src/PersonalTradingJournal.Desktop/PersonalTradingJournal.Desktop.csproj`
- `src/PersonalTradingJournal.Desktop/Interactions/DecorativeImage.cs`
- `src/PersonalTradingJournal.Desktop/Resources/Branding/ptj-logo-n26.png`
- `src/PersonalTradingJournal.Desktop/Resources/Branding/ptj-app-n26.ico`
- `tests/PersonalTradingJournal.Desktop.Tests/Navigation/MainWindowBrandingTests.cs`
