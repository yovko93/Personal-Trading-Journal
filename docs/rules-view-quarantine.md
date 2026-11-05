# Temporary Rules-view acceptance quarantine

## Status — 5 November 2026

User-authorized, temporary quarantine of exactly:

`PersonalTradingJournal.Desktop.Tests.Rules.RulesViewTests.CompiledThemesNarrowHighDpiBindingsValidationHistoryAndWindowClose`

An unconditional xUnit `Fact(Skip = ...)` preserves the complete implementation and its existing isolated-child dispatch. Normal parent discovery reports one skipped case before entering the method, so no `rules-view` child is launched. No class, collection, project, stage or other test is disabled. **The CLR crash is not fixed; its underlying cause is unproven.** Investigation is deferred to a separate task.

## Observed source failure

[Hosted CI #119](https://github.com/yovko93/Personal-Trading-Journal/actions/runs/41931492698), attempt 1, job `126929234570`; head `e5764b92bdddc62a63db88d474e92dcec82a84da`, tested merge `c3a141621a39186f43ce61151126a7eadc2d41f2` (job checkout).

Completed artifact root: `release-20261104-231135-f6b3bacf8d084587983f1e41a4a09ad7`. Failing child: `parallel/rules-view-20261104-231807-03cf7be7f9a64ec0a3fce8375ef20a3b`. The log reports an aborted child testhost, PID 5372, thread 4176, with:

> Attempt to execute managed code after the .NET runtime thread state has been destroyed.

The parent propagates isolated-suite exit 1. Completed top-level gate: **5,542 passed / 1 failed / 0 skipped**; ExclusiveWpf **147/147 passed**, parallel Desktop **2,135 passed / 1 failed**; Domain 602, Application 1,050 and Infrastructure 1,608 passed. These observations establish the failure, not its cause. No new crash reproduction, dump analysis or causal attribution is part of this quarantine task.

## Temporarily unavailable coverage

This one test combines compiled Rules-view acceptance for Light/Dark themes, normal/narrow high-DPI layouts, bindings and text/scroll/accessibility structure, inline editing/validation/revision history, logical focus/live theme updates, and dirty Rules editor veto/acceptance through MainWindow closing. Its assertions remain in source but are not executed while skipped. Other Rules model, persistence, manual-assessment and drag/drop tests remain enabled, as do all Calendar, Notebook and application tests.

The Release runner permits exactly this one skipped identity, only in the parallel Desktop project result. It counts explicit `NotExecuted` result rows, since VSTest can leave `Counters.notExecuted` at zero for xUnit skips, and checks aggregate consistency. It still rejects any other skip, duplicate identity, missing/empty result, incomplete execution or failed test, and preserves the underlying dotnet exit status. There are no filter, retry, timeout, scheduling or workflow-budget changes.

## Restoration criteria

1. Investigate the CLR termination separately using matching retained evidence and a controlled reproduction where possible.
2. Demonstrate a targeted production or fixture correction; do not treat an unchanged passing rerun as a fix.
3. Remove this test's Skip and the runner's exact quarantine exception together.
4. Validate the restored test and relevant Rules/lifecycle regressions locally, then run the complete Release gate with zero skips and validate the restored test in hosted CI.
5. Update this record and README with the actual results. Until then, report this as missing acceptance coverage, not a repaired CLR crash.

## Quarantine verification

- Release solution build: **0 warnings / 0 errors**, `TestResults/rules-quarantine/build.log`.
- Exact parent selection: **0 passed / 0 failed / 1 skipped**, exit 0; normal xUnit/TRX skip reason names the CLR crash and this guide. No `rules-view-*` child directory, child TRX or supervisor log is created (`exact.log`, `exact/`).
- Desktop Rules discovery: **71 cases**, including this skipped Fact; the other **70** passed in the complete run. This is the only Skip declaration in the Rules namespace (`rules-discovery.log`, `audit.json`).
- Runner-policy verification: **11 scenarios passed** against the actual script with synthetic top-level TRX/dotnet results. All-pass and the allowed skip return 0, including the zero-counter serialization variant; unexpected/additional skips (also with a zero counter), a failed case, nonzero native exit, wrong project, wrong stage and duplicated quarantine return 1 (`policy-audit.json`). These local protocol probes add no xUnit cases.
- Complete test execution, run **once**: **5,542 passed / 0 failed / 1 skipped**, matching the expected unchanged **5,543-case** inventory. No other test failed or was quarantined. No `rules-view-*` child was launched in either the exact selection or complete run.

| Stage/project | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Exclusive Desktop | 147 | 0 | 0 |
| Parallel Desktop | 2,135 | 0 | 1 |
| Domain | 602 | 0 | 0 |
| Application | 1,050 | 0 | 0 |
| Infrastructure | 1,608 | 0 | 0 |
| **Total** | **5,542** | **0** | **1** |

Actual run: `TestResults/rules-quarantine/complete/release-20261105-103500-22ae5ac8ac2745f98e449e8e6bc132fc`; `complete.log` and independent top-level/Rules audit `TestResults/rules-quarantine/audit.json` preserve the evidence. Notebook/Calendar fixes, scheduling, budgets and branding are unchanged.

**Runner accounting correction:** the initial full invocation returned **exit 1**, although both dotnet stages completed successfully with the test counts above. The new quarantine check initially assumed the skip would populate `Counters.notExecuted`; the actual Desktop TRX has `total=2136`, `executed=2135`, `notExecuted=0` and one explicit `NotExecuted` result row. This also made its original `summary.json` incorrectly display zero skips. That original log/summary is preserved; it is not a zero-skip acceptance result.

The corrected runner uses result rows for skip accounting and was evaluated against all five retained project TRX files: **validation exit 0**, correct **5,542/0/1** totals (`retained-result-validation.log` and `retained-result-validation/release-20261105-105150-e4ee8a2216fa4ad897201670b9fae402/summary.json`). This replay copies existing results into the actual runner; it launches no tests or isolated children. **The complete test suite was not rerun after the accounting correction**, and no fresh full-gate exit 0 or hosted pass is claimed. Updated hosted confirmation remains pending; the CLR defect and missing acceptance coverage remain unresolved.

Changed files: `RulesViewTests.cs` (Skip only; body/dispatch intact), `scripts/Test-Release.ps1` (exact skip exception and accurate accounting), `README.md` and this record. No push, PR or merge.
