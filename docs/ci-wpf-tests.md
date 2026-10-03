# Windows WPF test diagnostics

## Test execution and layout contract

Calendar modal/chart tests use a fresh test process with STA dispatchers. This isolates native windows from the Desktop routing test that intentionally shuts down WPF's process-wide Application. The parent cases share one native-suite result; a native failure therefore appears against multiple parent names. This is not evidence that every parent case independently failed. No tests are skipped, retried, or globally serialized to hide failures.

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

## Local verification of the correction (2026-10-04)

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

The workflow sets `PTJ_TEST_RESULTS_DIRECTORY`, enables TRX logging, and uploads `windows-test-results` even after a test failure. It includes project results, `calendar-native/calendar-native.trx`, `calendar-native/native-output.log`, and `calendar-layout.txt` (actual screen/work area, window, DPI, viewport, extent and columns). Synthetic layout diagnostics do not log Account names, Trade IDs or customer rows. The complete child output and stacks remain in artifacts instead of being repeated in every parent exception.

A passing local reproduction/full suite is not a passing GitHub run. The user must commit/push the changes to the PR branch, let a new Windows CI run test that new head/merge tree, and verify all suites plus the uploaded native-suite results. Rerunning run 69 would only retest the old tree.
