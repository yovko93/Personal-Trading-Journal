# Release gate lifecycle investigation — 2 November 2026

## Scope and provenance

The current checkout began clean at `432dc15` (documentation follow-up to branding commit `c7cadee`). The investigated run is `TestResults/n26-verification/complete/release-20261101-225400-37dc24757f5248f5b20c6efa5e3ee29c/`: 5,510 passed, 25 failed, zero skipped. Source, branding assets, sidebar layout, window/EXE icons, tests, concurrency and deadlines are unchanged by this investigation. No attribution to branding, baseline, or environment is inferred from the failure counts.

Evidence was inspected **before** further tests: final parent/child TRX, stdout/stderr, supervisor logs, STA phases, UI-case phases, keyboard diagnostics, Sequence XML and retained dumps. Existing `TestResults/m2212c3-diagnosis/tools/dotnet-dump.exe` successfully read the retained Windows/.NET dumps. Full managed stacks are in `TestResults/n26-lifecycle/plan-stack.log`, `stack-22840.log`, and `stack-24304.log`. No native screenshot mechanism was retried.

## Failure signatures and execution stages

All methods below belong to `PersonalTradingJournal.Desktop.Tests`. The exact 25 parent identities are listed at the end; their complete exceptions/stacks and original parent TRX paths remain in `TestResults/n26-verification/final-failures.json`. Multiple parents awaiting one failed lazy suite are not independent child failures.

| Group | Parents | Original child identity / process | First failure and last completed stage | Body versus cleanup |
| --- | ---: | --- | --- | --- |
| Cold shell construction | 4 | `TradingPlans.TradingPlanWorkspaceViewTests.FullPageBindingsLifecycleGuardsFocusAndLightDarkResponsiveLayouts(980,96)`; Sequence XML says `Completed=False`. Launcher 7936, host 21576, STA 23. | Thirty-second VSTest inactivity watchdog. Dump: WIC `WICBitmapDecoder.GetFrameCount` → `BitmapDecoder.SetupFrames` → `ImageSourceConverter.ConvertFrom` → BAML loading → `MainWindow.InitializeComponent`/constructor → test line 169. Prior validation-window section had returned; construction of the subsequent owner had not returned. | **Primary execution abort.** That owner had not been constructed or shown; its close assertions and cleanup were not reached. No owner-lifetime defect is established. Other three theory rows lack completed child execution evidence. |
| Initial detached-shell layout | 4 | `TradingPlans.SessionBriefCompositionTests.ProductionShellDraftReadyAndInvalidComposition(1000,850,240,"Dark")`; Sequence XML says `Completed=False`. Launcher 20748, host 22840, STA 15. | Thirty-second inactivity watchdog. UI trace completed fixture creation, activation/context tasks and detached shell initialization, then entered first `Layout` at line 88. Dump shows binding/template creation and WPF `Measure` under that layout (line 85), not an async task wait or window close. | **Primary execution abort.** Body and its assertions were incomplete. No `window cleanup / ApplicationIdle` phase began. The other three theories are propagated parent failures, not three further completed/failed bodies. |
| Multi-render body deadline | 1 | `Journals.JournalHistoryViewTests.CompiledHistoryPagesAndSnapshotsWrapInBothThemesAndRemainReadOnlyAtHighDpi`; host 24304, STA 22. | Harness 30-second timeout: fourth render, `History Dark 480 DIP / 240 DPI`. Previous `History Light 480 DIP / 240 DPI` phase ended at elapsed 28.608 s. Watchdog dump/aborted host also recorded. | **Primary body timeout, then child abort.** No body-completed or successful cleanup marker. Dump was sampled after the timeout and does not recover a conclusive active rendering stack; phase log is the stage evidence. |
| Table body deadline | 1 | `Breakdown.BreakdownTableViewTests.BoundTableSortSelectionCoverageVirtualizationAndReachabilityInBothThemes`; host 9072, STA 19. | Harness timeout at elapsed 30.088 s, phase `test action`; application alive, dispatcher shutdown false/false. Parent stack propagates child timeout from `CalendarStaTest.cs:98`, test line 32. | **Primary execution timeout.** No assertions-completed marker; exact inner operation is unrecorded. No retained dump found for this host, despite collector diagnostics. No cleanup/Owner cause can be established. |
| Body deadline followed by successful teardown | 1 | `Rules.RulesDropTransitionTests.PendingDropRetainsLiftAndVerifiedMoveRetainsCardsFocusAndBothScrollPositions`; host 3552, STA 19. | Harness timeout at elapsed 30.355 s in `test action`. Assertions-completed appears later at 30.536 s; STA finished at 30.665 s with dispatcher shutdown true/true. | **Primary deadline failure.** Assertions eventually completed **after** timeout, and dispatcher cleanup completed. It is not a shutdown failure or evidence of a dropped async operation. The earlier deadline failure remains valid. |
| Aggregate shared-suite supervisor deadline | 14 | `Navigation.MainWindowViewModelTests.CalendarOwnerClose*`, shared filter; launcher 5620, host 19524, STA 21. No final child TRX or active-case Sequence XML. | Sixty-second process supervisor expiry. Eleven STA actions logged assertions-completed/finished; a later `RunOwnerCloseSta` action began at `23:13:31.416 UTC`, with the existing Application on STA 21 and shutdown false/false, before supervisor termination. Full suite includes 15 child identities; exact active identity at kill was not logged. | **Primary shared-suite timeout and forced cleanup**, not 14 demonstrated owner-close defects. Earlier actions completed, but remaining bodies are incomplete/unattributed. No retained stack establishes whether the last action was in `Close`, modal work, or other code. Process-tree termination is secondary supervisor cleanup, not the original failure. |

All trace timestamps above are UTC. The process supervisor confirms terminated children exited; no user cancellation was issued. An aborted run is incomplete execution evidence. A parent method name containing “owner close” does not identify the blocked operation. No retained primary `Window.Owner` assignment exception, popup-teardown exception, or cross-case `Application`/dispatcher ownership violation was found. That absence is not proof those paths can never fail.

Unlike the earlier five native-menu cases, these 25 do not record a failed HWND hit-readiness assertion. No failed-case HWND ownership snapshot identifies an unexpected input window. Available dispatcher evidence is the Application/STA ownership and shutdown state in the phase logs; the exact Calendar window/active-case ownership at termination was not captured. No unrelated application was closed and no unexpected input target was forced.

## Reproduction and correction decision

Using normal Windows permissions and the existing parent-to-isolated-child workflow, the six representative families passed unchanged: **12 parent tests, 26 isolated child cases, zero failures/skips**. The lazy suites deliberately retain their full established child selections: four Trading Plan, four Session Brief, fifteen Calendar owner-close, and one each Journal, Breakdown and Rules. Parent and child totals are counted separately.

Every one of the original 25 parent identities matches one passing isolated child identity in this run. `TestResults/n26-lifecycle/affected-identity-audit.json` records the exact mapping, outcomes and TRX paths. This includes all original Calendar identities, rather than treating the single selected Calendar parent as coverage of unexecuted rows.

No demonstrated production or fixture defect reproduced, so **no code correction was made**. In particular, an owner-lifetime patch would not address the observed image-decode, initial-layout and render-body stages. The dumps establish where execution was sampled, not why it was slow. The cause of the earlier timing variation remains unresolved; no environmental or pre-existing attribution is claimed, and no baseline comparison was needed for a code correction that was not justified.

The top image-related frame in the Trading Plan dump is a `PrestubMethodFrame`; that snapshot alone cannot distinguish cold method preparation from decoder work or establish a WIC deadlock. The Session Brief dump samples active binding/template measurement. Neither stack is evidence that a task was abandoned or dispatcher shutdown had started.

The new dump-stage evidence and green affected-group results justified one complete-gate run under the unchanged script and deadlines. It passed **5,535/5,535**, zero failures/skips, exit 0. No further full run was repeated merely to obtain green.

## Final complete Release gate

Evidence root: `TestResults/n26-lifecycle/complete/release-20261102-092834-679f7e4187d946c5877c5d238848e4dc/`. All five expected final parent TRX files and the existing runner's summary are present. Every originally failed parent identity is explicitly matched to a passing final parent result in `TestResults/n26-lifecycle/final-identity-audit.json`.

| Partition / project | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Exclusive WPF / Desktop | 140 | 0 | 0 |
| Parallel / Domain | 602 | 0 | 0 |
| Parallel / Application | 1,050 | 0 | 0 |
| Parallel / Infrastructure | 1,608 | 0 | 0 |
| Parallel / Desktop | 2,135 | 0 | 0 |
| **Total** | **5,535** | **0** | **0** |

There are no unresolved failures in this final run. The historical execution-time variation is not explained by these passing results; they do not prove a code fix, environment attribution, or immunity to future deadline failures. No baseline run or matching hosted run was performed. Existing branding build/icon/render evidence is retained separately from this gate; manual branding acceptance remains pending.

Changed files in this task: `README.md`, `docs/application-branding.md`, and this investigation note. No production/test/asset changes, timeout increases, skipped cases, isolation changes, concurrency changes, push, PR, or merge occurred.

```powershell
$env:PTJ_TEST_RESULTS_DIRECTORY = Join-Path $PWD 'TestResults/n26-lifecycle/representatives'
$env:PTJ_CALENDAR_TIMINGS = '1'
dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~FullPageBindingsLifecycleGuardsFocusAndLightDarkResponsiveLayouts|FullyQualifiedName~ProductionShellDraftReadyAndInvalidComposition|FullyQualifiedName~CompiledHistoryPagesAndSnapshotsWrapInBothThemesAndRemainReadOnlyAtHighDpi|FullyQualifiedName~BoundTableSortSelectionCoverageVirtualizationAndReachabilityInBothThemes|FullyQualifiedName~PendingDropRetainsLiftAndVerifiedMoveRetainsCardsFocusAndBothScrollPositions|FullyQualifiedName~CalendarOwnerCloseStandaloneJournalStillUsesItsOwnGuard' --logger trx --results-directory TestResults/n26-lifecycle/representatives
pwsh -NoProfile -File scripts/Test-Release.ps1 -ResultsDirectory TestResults/n26-lifecycle/complete
```

## Manual branding acceptance

Still pending; automated renders and icon resource comparisons do not fulfill these checks:

- [ ] Sidebar in Light/Dark at native Windows 100% and 250% scaling.
- [ ] Title-bar, Alt+Tab and taskbar icons.
- [ ] Built and published EXE icons in Explorer.
- [ ] Screen reader and Tab navigation: logo has no focus stop or duplicate announcement.

## Exact original parent identities

Each identity below maps to its shared original child group in the stage table above; a matching **passing rerun child** is recorded individually in `affected-identity-audit.json`. This does not imply that aborted original child theories completed.
- `PersonalTradingJournal.Desktop.Tests.TradingPlans.TradingPlanWorkspaceViewTests.FullPageBindingsLifecycleGuardsFocusAndLightDarkResponsiveLayouts(width: 980, dpi: 96)`
- `PersonalTradingJournal.Desktop.Tests.TradingPlans.TradingPlanWorkspaceViewTests.FullPageBindingsLifecycleGuardsFocusAndLightDarkResponsiveLayouts(width: 1280, dpi: 144)`
- `PersonalTradingJournal.Desktop.Tests.TradingPlans.TradingPlanWorkspaceViewTests.FullPageBindingsLifecycleGuardsFocusAndLightDarkResponsiveLayouts(width: 1600, dpi: 192)`
- `PersonalTradingJournal.Desktop.Tests.TradingPlans.TradingPlanWorkspaceViewTests.FullPageBindingsLifecycleGuardsFocusAndLightDarkResponsiveLayouts(width: 460, dpi: 240)`
- `PersonalTradingJournal.Desktop.Tests.Navigation.MainWindowViewModelTests.CalendarOwnerCloseWaitsForEveryGuardBeforeEndingAssessmentModal(target: "main", veto: "subscriber")`
- `PersonalTradingJournal.Desktop.Tests.Navigation.MainWindowViewModelTests.CalendarOwnerCloseWaitsForEveryGuardBeforeEndingAssessmentModal(target: "day", veto: "subscriber")`
- `PersonalTradingJournal.Desktop.Tests.Navigation.MainWindowViewModelTests.CalendarOwnerCloseWaitsForEveryGuardBeforeEndingAssessmentModal(target: "main", veto: "assessment")`
- `PersonalTradingJournal.Desktop.Tests.Navigation.MainWindowViewModelTests.CalendarOwnerCloseWaitsForEveryGuardBeforeEndingAssessmentModal(target: "main", veto: "journal")`
- `PersonalTradingJournal.Desktop.Tests.Journals.JournalHistoryViewTests.CompiledHistoryPagesAndSnapshotsWrapInBothThemesAndRemainReadOnlyAtHighDpi`
- `PersonalTradingJournal.Desktop.Tests.Navigation.MainWindowViewModelTests.CalendarOwnerCloseProtectsDirtyJournalAndClosesCleanWithoutDuplicatePrompt(dirty: True, accept: False)`
- `PersonalTradingJournal.Desktop.Tests.Navigation.MainWindowViewModelTests.CalendarOwnerCloseWaitsForEveryGuardBeforeEndingAssessmentModal(target: "assessment", veto: "assessment")`
- `PersonalTradingJournal.Desktop.Tests.Navigation.MainWindowViewModelTests.CalendarOwnerCloseWaitsForEveryGuardBeforeEndingAssessmentModal(target: "day", veto: "assessment")`
- `PersonalTradingJournal.Desktop.Tests.Navigation.MainWindowViewModelTests.CalendarOwnerCloseWaitsForEveryGuardBeforeEndingAssessmentModal(target: "day", veto: "trade")`
- `PersonalTradingJournal.Desktop.Tests.Navigation.MainWindowViewModelTests.CalendarOwnerCloseProtectsDirtyJournalAndClosesCleanWithoutDuplicatePrompt(dirty: True, accept: True)`
- `PersonalTradingJournal.Desktop.Tests.TradingPlans.SessionBriefCompositionTests.ProductionShellDraftReadyAndInvalidComposition(width: 1000, height: 850, dpi: 240, theme: "Dark")`
- `PersonalTradingJournal.Desktop.Tests.TradingPlans.SessionBriefCompositionTests.ProductionShellDraftReadyAndInvalidComposition(width: 1586, height: 1000, dpi: 96, theme: "Dark")`
- `PersonalTradingJournal.Desktop.Tests.Navigation.MainWindowViewModelTests.CalendarOwnerCloseWaitsForEveryGuardBeforeEndingAssessmentModal(target: "main", veto: "trade")`
- `PersonalTradingJournal.Desktop.Tests.TradingPlans.SessionBriefCompositionTests.ProductionShellDraftReadyAndInvalidComposition(width: 1586, height: 1000, dpi: 96, theme: "Light")`
- `PersonalTradingJournal.Desktop.Tests.Navigation.MainWindowViewModelTests.CalendarOwnerCloseWaitsForEveryGuardBeforeEndingAssessmentModal(target: "command", veto: "assessment")`
- `PersonalTradingJournal.Desktop.Tests.Rules.RulesDropTransitionTests.PendingDropRetainsLiftAndVerifiedMoveRetainsCardsFocusAndBothScrollPositions`
- `PersonalTradingJournal.Desktop.Tests.Navigation.MainWindowViewModelTests.CalendarOwnerCloseProtectsDirtyJournalAndClosesCleanWithoutDuplicatePrompt(dirty: False, accept: False)`
- `PersonalTradingJournal.Desktop.Tests.Breakdown.BreakdownTableViewTests.BoundTableSortSelectionCoverageVirtualizationAndReachabilityInBothThemes`
- `PersonalTradingJournal.Desktop.Tests.Navigation.MainWindowViewModelTests.CalendarOwnerCloseStandaloneJournalStillUsesItsOwnGuard`
- `PersonalTradingJournal.Desktop.Tests.Navigation.MainWindowViewModelTests.CalendarOwnerCloseWaitsForEveryGuardBeforeEndingAssessmentModal(target: "day", veto: "journal")`
- `PersonalTradingJournal.Desktop.Tests.TradingPlans.SessionBriefCompositionTests.ProductionShellDraftReadyAndInvalidComposition(width: 1000, height: 850, dpi: 240, theme: "Light")`
