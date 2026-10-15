# Windows WPF test diagnostics

## M19.8 — CI #95 child shutdown and unfinished Infrastructure (15 October 2026)

Investigation started on clean `develop`, **`52ce58592b4f0c8c7096d7bda0229bb1b43fbba8`**. [CI #95](https://github.com/yovko93/Personal-Trading-Journal/actions/runs/38792076087) failed for PR #22: checkout actually tested merge **`13588c00d192db3bca8f1b0d4436324d3a2bc70e`**, incorporating that develop head and main `c8e6c9d25e23bf38581b9434189f389744f60af3`. This is matching **failed** CI evidence, superseding the earlier no-run lookup. No workflow rerun or Git write was requested or performed.

### First failure and separate timeout

The uploaded artifact has 240 files, SHA-256 `167521e6ac0fc6ac6bcdd464d476684412ad0c7b8b299f34a9fe3877c9ca4dca`. It includes outer TRX/VSTest host/datacollector logs and child phase/output/supervision logs, but **no crash dump or blame Sequence XML**. Original evidence is retained under ignored `artifacts/ci95/download/`; do not infer a native stack from an assertion string.

| UTC | Observed event |
| --- | --- |
| 19:15:45 | Complete Release script begins exclusive WPF phase |
| 19:16:32 | Exclusive **88/88 passed**; parallel phase begins |
| 19:16:40 | Domain 490 and Application 880 passed |
| 19:22:13.554 | `playbook-sqlite-write` supervisor starts dotnet PID 8784, unchanged 90 s deadline |
| 19:22:16.362 | Child testhost PID 5828 starts STA; .NET 10.0.13, WPF 10.0.12, four logical CPUs |
| 19:22:17.976 / 18.365 | Save commands completed, displayed revisions **1 / 2**, no active write, editor collapsed |
| 19:22:18.415 | **All child assertions completed**, including reactivation/readback |
| 19:22:18.456 | Dispatcher shutdown finished (`True/True`), STA elapsed **2.117 s** |
| By 19:22:18.592 | CoreCLR fail-fast in PID 5828, native thread 7148: managed callback after runtime thread-state destruction; dotnet exits 1 after **5.038 s** |
| 19:22:18.729 | Parent reports failure at Playbook test lines 45–47: its **exit-code assertion**, not a failed Save/content assertion; restart-read phase was never launched |
| 19:23:07 | Outer Desktop completes **1,529 passed / 1 failed** in its parallel partition |
| 19:25:43.998 | Infrastructure PID 2680 is **still making progress**: 1,215 completed results, all passed; another case starts |
| 19:25:44.931 | Actions terminates Test at unchanged **10 minutes**; Infrastructure has no final TRX and the runner never completes its parallel summary |

The Playbook parent failure is explained by the child crash. The later action timeout is **not** a Playbook STA deadline or a demonstrated Infrastructure deadlock. Last active Infrastructure cases were Tradovate ambiguity-after-preview, snapshot cancellation at Copy, Trade detail inactive-Setup history, and optional-Criterion migration. All had started within the last 0.81 s of the final diagnostic record. 132 of the expected 1,347 results were not reported before cutoff. The three-minute inactivity watchdog did not fire because cases continued completing.

### Ownership and bounded correction

The parent owns an isolated `JournalTestDatabase`; its context at lines 35–36 only obtains the synthetic path, and is disposed with that fixture. Each child owns a separate DI provider. Saves await their SQLite transaction, verified revision readback and reload. UI commands and catalog `LoadTask` are complete at the logged boundary; no SQLite lock/transaction error appears in CI. The child closes its window and deactivates the ViewModel, but previously returned its short-lived STA after `Dispatcher.InvokeShutdown`. WPF text-input/native callbacks may outlive that managed dispatcher shutdown. The upstream [WPF secondary-STA teardown issue](https://github.com/dotnet/wpf/issues/11785) reports the same CoreCLR assertion during native thread cleanup. This is corroborating evidence, **not proof of the exact native callback in CI #95**, for which no dump exists.

Only this native Playbook SQLite test now uses the existing **process-owned background STA/dispatcher**, already used by Calendar and other native WPF children. Windows still close; the same Save, validation, focus, SQLite, revision and restart assertions execute. Test-only image-operation tracking drains actual thumbnail work after cancellation while its dispatcher is alive, before provider/file teardown. Added assertions require zero windows, completed image/load/save work, and a still-live non-shutdown native dispatcher after the action. The outer supervisor still requires normal child exit and preserves fatal-error handling. No runtime assertion suppression or success-on-crash behavior is introduced. Each write/restart child now retains its own TRX, VSTest diagnostics and best-effort mini crash dump collection; the previous custom launch omitted these collectors. Existing 30 s STA / 90 s child deadlines remain unchanged.

Infrastructure repeatedly migrated a fresh schema in `ReaderTestDatabase` even though those tests target current-schema readers. In CI, long initial cases took 52.117 s (period comparison), 48.063 s (Saturday Calendar reader), and 34.151 s (manual Trade references); the separately instrumented backup template took 58.191 s to migrate once. These elapsed values alone do not identify CPU, storage or security-scanner attribution. Controlled local instrumentation measured **59 reader initializations totaling 33.698 s** alongside layout/backup work. Reader fixtures now reuse the existing immutable SQLite-backup template, while retaining separate paths/providers/connections/data and the real initializer. Dedicated migration tests remain unchanged. No production initializer, database durability setting or concurrency bound changes.

The first clone implementation incorrectly retained the portable template's DELETE journal mode. **Five existing concurrent-snapshot assertions failed with SQLite busy** (119 passed / 5 failed). This was an introduced fixture defect, not the CI failure. It was corrected by restoring and checking **WAL before returning each reader fixture**, matching fresh EF-created databases. New tests check concurrent clone isolation, complete applied migration history, WAL, foreign keys, integrity, independent mutable data, exact cleanup and template immutability. Those five concurrency assertions remain intact.

### Verification and limits

Original Playbook fixture passed isolated **3/3** on local CoreCLR 10.0.12 and **3/3** on a privately staged CI CoreCLR 10.0.13 with the same WPF 10.0.12. Original competing workload passed **19 Desktop + 120 Infrastructure**. Thus the intermittent native crash was **not reproduced locally**; the lifetime correction avoids the evidenced teardown path, but matching CI must validate it. The staged runtime is under ignored artifacts; installed runtimes and OS settings are untouched. `DOTNET_PROCESSOR_COUNT=4` matches runtime processor reporting, not the hosted runner's Windows Server 2025 hardware/IO performance.

Final competing verification: **19 Desktop + 124 Infrastructure passed**. Reader initialization totals dropped from 33.698 s (59 calls) to 9.299 s (65 calls), plus 7.546 s for template acquisition including one 3.189 s migration. The focused set gained four fixture cases and used a different runtime patch; these are measured setup costs, not a controlled CI wall-time speedup claim.

Final complete `pwsh -File scripts/Test-Release.ps1 -ResultsDirectory TestResults` gate: **4,337/4,337 passed**, zero failures/skips, exit 0. Domain 490, Application 880, Infrastructure 1,349, Desktop 1,618 (88 exclusive + 1,530 parallel). UTC 19:54:52.705–20:00:36.133, **5m43.428s** total. Infrastructure test duration 3m37s; parallel Desktop 4m06s. Calendar child **72.125 s / 47.875 s headroom** to 120 s. Journal child 10.095 s, STA **6.995 s / 23.005 s headroom** to 30 s. Playbook write/restart children **12.250 / 10.188 s**, both exit 0 with zero windows/image work and complete load/save work. Crash collectors add startup work; the original local write/restart had taken 6.751 / 5.299 s without collectors, so this is not a claimed Playbook speedup.

Raw final evidence: `TestResults/release-20261015-195452-11768bd6028640ea88397c66ad7771fd/` (summary, TRX, phase, interaction and process logs). Build passed with zero warnings/errors; EF model and whitespace checks passed. Final GitHub lookup still reports failed #95; these eight uncommitted files have no matching CI run. Native crash recurrence on the hosted runner remains unverified. Final results and file inventory are in the [Daily acceptance matrix](daily-review.md#m198-ci-95-follow-up--15-october-2026). Product/manual ChatGPT workflows, assertions, deadlines, test isolation, explicit concurrency tests and the local/CI Release script are unchanged. No production data, provider requests, retries, skips, Git writes or M20 work.

## M19.8 — Controlled timeout investigation (15 October 2026)

Started clean on `develop`, `d3a2e60ff9e9cb30763769130f708def596a7a51`. Compared with the prior green `37c1aaa` implementation, only acceptance documentation changed. The recorded **4,172 passed / 163 failed** run is retained at `artifacts/m198/release/release-20261015-154338-fa57050015614cda98015537756906d8`. Its first underlying failures group as follows; outer propagation must not be counted as separate product defects:

| Child / first failure | Outer failures |
| --- | ---: |
| Calendar native, aggregate 120 s | 86 |
| Journal editor, STA/inactivity 30 s | 1 |
| Calendar grid, aggregate 120 s | 36 |
| Calendar owner-close, aggregate 60 s | 14 |
| Calendar healthy STA probe, aggregate 30 s | 6 |
| Unhandled-native probe, aggregate 30 s instead of the expected original exception | 1 |
| Import-resource/native-tooltip isolation, aggregate 120 s | 1 |
| Daily Review layout, aggregate 120 s | 10 |
| Period Review layout, child inactivity 30 s | 4 |
| Journal History, child inactivity 30 s | 1 |
| Rules drop and Rules view, STA 30 s | 2 |
| Tradovate bound preview, child inactivity 30 s | 1 |

Calendar completed 40 action blocks, then began another only about 2.6 s before termination. At the last completed boundary: process CPU 155.359 s, working set about 579 MiB. Twelve owner `Show` calls totaled **35.181 s**, versus **12.328 s** in the earlier green run; theme loading was 2.351 versus 0.548 s. Dispatcher scheduling waits totaled only 0.291 s. Journal continued from a flush at 29.687 s into another construction/layout phase at 30.698 s after its timeout at 30.411 s. These logs demonstrate cumulative progress and deadline exhaustion, not a proven individual hang.

### Supervised experiments, unchanged binaries and deadlines

The synthetic Desktop app was not running. No separate build overlapped these experiments. Opt-in `PTJ_CALENDAR_TIMINGS=1`, existing phase logs, supervision timestamps and TRX were sufficient; no new timing hooks or test behavior were needed. The competing run deliberately overlaps work that the complete runner normally separates; it is a diagnostic experiment, not the acceptance command.

| Measurement | Isolated exclusive lane | Same lane with layout + Infrastructure competitors |
| --- | ---: | ---: |
| Calendar native process wall time / 120 s headroom | 63.075 / 56.925 s | 105.529 / 14.471 s |
| Completed native actions | 86 | 86 |
| Summed STA action elapsed time | 57.475 s | 81.982 s |
| First STA start to last STA completion | 60.217 s | 102.682 s |
| Process start to first STA action (startup + initial fixture) | 2.409 s | 2.258 s |
| Twelve owner `Show` calls | 8.588 s | 10.272 s |
| Modal dispatcher pumps | 6.988 s | 9.791 s |
| Fixture create/activate | 0.604 s | 2.641 s |
| Native dispatcher scheduled waits | 0.239 s | 0.059 s |
| Native child CPU at final action boundary | 87.516 s | 106.203 s |
| Journal process wall time | 10.925 s | 15.550 s |
| Journal STA elapsed / 30 s headroom | 7.795 / 22.205 s | 10.836 / 19.164 s |
| Journal Measure / bitmap render | 1.317 / 1.114 s | 1.803 / 1.552 s |
| Result | 88/88 | 88/88 + 52/52 layout + 232/232 Infrastructure |

Evidence: `artifacts/m198-timeout/isolated/` and `artifacts/m198-timeout/overlap/{exclusive,layout,infrastructure}/`. The layout competitor selects Calendar grid, Daily and Period view tests; Infrastructure selects `Backups` and `PeriodReview`. All three overlap from 16:25:15 to 16:26:22 UTC. Between-action time increases by about 18 s, spread over many boundaries (largest observed gap 1.033 s), not one stalled action. Individual modal/layout action groups also slow. Timing scopes overlap: do not add nested scope totals or equate process CPU with STA CPU.

A bounded ten-second process sample found native testhost CPU 14.31 s alone; during overlap three testhosts consumed 21.14, 10.03 and 8.00 CPU seconds. Other Windows/app processes were active in both samples. This confirms competing execution, **not** the identity of the cause of the earlier failed run. No off-CPU trace was captured, so OS scheduling, synchronous/native window work and filesystem effects cannot be apportioned exactly. The original 35.181-second owner-Show slowdown and 163 failures were not reproduced in these controlled experiments. An isolated or subsequent green run cannot prove that variability is fixed.

No speculative product, harness or runner correction is justified by this evidence. Keep the existing exclusive Calendar/Journal lane, parallel remainder, explicit concurrency tests, assertions, limits and deadlines. Run the complete gate without a simultaneous app acceptance session, build or second test run. This is operational isolation of verification, not a claim that a particular external app caused the failures. Final complete-gate results and remaining acceptance gates are recorded in the [Daily Review matrix](daily-review.md#m198-timeout-follow-up--15-october-2026).

Final unchanged gate: **4,335/4,335 passed, zero failures/skips, exit 0**, about 5m51s. Domain 490, Application 880, Infrastructure 1,347, Desktop 1,618 (88 exclusive + 1,530 parallel). Native child **75.015 s / 44.985 s headroom**, owner Show 9.262 s, scheduling waits 0.104 s. Journal child 10.299 s, STA **7.183 s / 22.817 s headroom**. Parallel grid 29.57 s, owner-close 34.02 s, Daily layout 23.18 s, Period layout 13.17 s, Journal History 15.37 s; the previously timed-out child groups passed. Evidence: `artifacts/m198-timeout/final/release-20261015-162933-80f236b8cc0d44458dd5a1659cddbb7a/summary.json`. Build 0 warnings/errors; in-memory EF model consistency and whitespace passed. Four documentation files changed; no new regression test is warranted without a reproduced mechanism. The original exclusive failure occurred without other assemblies from its own gate, so the overlap experiment is not a complete causal explanation. Local green does not establish matching CI, live acceptance or elimination of host-load variability.

## M18.6 — Resource-aware complete Release gate (14 October 2026)

Started on clean `develop` **`7323f8da6b07ed118897ab42a7e102a1d0b3cef8`**: the previous fixture optimization and failure report were already committed and are preserved. This supersedes the prior all-assembly-overlap command as the acceptance gate, not the historical measurements below. No production/test assertions, operation deadlines, schema, Rule semantics, provider behavior or explicit concurrency tests change.

### Controlled workload comparisons

All diagnostic experiments use the same Release binaries until the scheduling correction, existing bounded phase/CPU/memory logging, the unchanged native **120-second aggregate / 45-second action** limits and Journal **30-second inactivity** limit. Raw logs are under ignored `artifacts/m145-load/`. No run is retried until green.

| Measurement (seconds unless stated) | Calendar + Journal only | Same pair + Infrastructure backups/Rules | Scheduled focused verification |
| --- | ---: | ---: | ---: |
| Run directory | `m186b-calendar-journal` | `m186b-pair-infrastructure` | `m186b-scheduled-focused` |
| Outer tests | 2/2 | 301/301 (37 Application, 262 Infrastructure, 2 Desktop) | 90/90 Desktop |
| Calendar child cases | 87/87 | 87/87 | 87/87 |
| Calendar wall / headroom to 120 | 98.908 / 21.092 | 99.909 / 20.091 | 74.373 / 45.627 |
| Calendar startup to first STA | 3.235 | 4.444 | 2.613 |
| Calendar 86 completed-action wall sum | 90.376 | 78.402 | 63.341 |
| Calendar completed-action CPU delta sum | 90.875 | 95.578 | 86.078 |
| Calendar action-boundary peak MiB | 822.31 | 799.33 | 747.11 |
| Owner `Show`, 12 calls | 8.55 | 13.00 | 9.43 |
| Initial owner dispatcher drain, 12 calls | 29.68 | 0.59 | 0.35 |
| Modal open/interaction, 12 calls | 4.97 | 7.27 | 5.23 |
| Dispatcher scheduled-wait sum | 0.16 | 0.06 | 0.34 |
| Calendar fixture setup, 61 calls | 1.10 | 2.11 | 1.20 |
| Two theme parses | 0.35 | 0.65 | 0.32 |
| Journal child wall / startup | 14.999 / 3.166 | 27.443 / 4.154 | 10.771 / 3.080 |
| Journal STA work | 11.579 | 21.727 | 7.375 |
| Journal TRX test duration / margin to 30 | 11.645 / 18.355 | 22.054 / 7.946 | 7.428 / 22.572 |
| Journal process CPU across STA boundaries | 14.641 | 19.078 | 9.891 |
| Journal construction / measure / layout / bitmap totals | 2.042 / 1.829 / 1.275 / 1.735 | 3.665 / 4.106 / 2.267 / 3.028 | See raw phase logs |

The added Infrastructure workload contains 262 cases with the prior migrated-template optimization, **without the grid child**. It consumed **285.000 sampled CPU seconds over 141.438 wall seconds**, peak **344.17 MiB**. Journal work roughly doubled and native owner creation rose from 8.55 to 13.00 seconds. This identifies a reproducible competing workload, not proof that one SQL operation blocks WPF. The previous pair-plus-grid/Infrastructure run took **118.188 s** with **1.812 s** headroom, and the full run still timed out. More redundant-work removal alone had not delivered dependable margin.

Importantly, the pair-only run contains **one 28.901-second owner dispatcher-drain outlier**. The child continued and passed. Its action-gate wait included **30.100 s** behind that already-running action; that is not another independent delay or a hung reader. This is why the two Calendar totals are similar despite slower typical work with Infrastructure. We have not demonstrated the exact native/OS trigger for that drain. Scheduled-wait sums remain under 0.34 s, separating delay *after entering WPF* from waiting to acquire its STA. Multi-thread process CPU cannot be subtracted from wall time to infer CPU scheduling; no ETW off-CPU attribution is claimed. Theme/fixture/startup measurements are separately recorded rather than blamed for the whole timeout.

### Scheduling correction and complete-set guarantee

`scripts/Test-Release.ps1` is the **single local and GitHub Actions gate** (PowerShell 7, built Release binaries):

```powershell
dotnet restore PersonalTradingJournal.sln
dotnet build PersonalTradingJournal.sln -c Release --no-restore
pwsh -File scripts/Test-Release.ps1 -ResultsDirectory TestResults
```

1. `dotnet test` the Desktop project with **`Workload=ExclusiveWpf`**. Only `CalendarDayModalTests`, `CalendarDayPerformanceChartTests` and `JournalViewTests` carry this trait. A shared nonparallel xUnit collection ensures Calendar and Journal do not overlap each other. The same collection also applies in the native child, whose STA action gate remains intact. [xUnit collection scheduling](https://xunit.net/docs/running-tests-in-parallel) provides this resource lane; it is not a global parallelism switch.
2. After that process exits, `dotnet test PersonalTradingJournal.sln` with **`Workload!=ExclusiveWpf`** runs all remaining cases with normal assembly/collection parallelism. Native supervised children, fixtures, assertions and explicit concurrent writer/reader/dispatcher tests remain unchanged. In particular `NativeActionsCannotReenterEachOtherThroughANestedDispatcherFrame` still starts competing actions itself and passes in the focused verification.

The filters are exact complements, not excluded coverage; newly untagged tests automatically enter phase two. The script records one unique run directory, phase-specific TRX/diagnostics and `summary.json`, validates expected project reports, rejects empty/skipped/failed results and duplicate identities across phases, and runs the second phase even after first-phase test failures. Only outer TRX are counted; child TRX are not double-counted. No fixed expected test total can silently omit future cases. The new regression pins the heavy classes' trait/collection, verifies global Desktop parallelism remains enabled at two, and confirms the explicit concurrency harness is not moved into the exclusive collection. Infrastructure remains at four concurrent collections. CI triggers and its ten-minute Test-step limit are unchanged; no test/child deadline is increased.

Direct unpartitioned `dotnet test PersonalTradingJournal.sln` remains useful for stress diagnosis, but is **not the supported acceptance scheduling**. An IDE's cross-assembly scheduling similarly cannot provide the script's process-level separation. Run the complete script without unrelated builds/tests competing on the same host.

### Verification status

Scheduled focused **90/90**, zero failures/skips, includes all 88 heavy outer cases plus the scheduling regression and the existing nested-dispatcher concurrency regression. The native child finishes before Journal launches, verified from supervision timestamps. Release build passed **zero warnings/errors** after correcting the new reflection regression to use xUnit v2 constructor metadata. No existing assertion changed.

**Final complete Release gate: 4,045/4,045 passed, zero failures/skips**, exit code 0. Results: `artifacts/m145-load/m186b-release-gate/release-20261014-145955-6dcd76ee3ce149bc9a943cf2f903022f`. Exclusive phase **88/88**, followed by parallel phase **3,957/3,957**. Per-project totals: Domain **490**, Application **753**, Infrastructure **1,251**, Desktop **1,551** (88 exclusive + 1,463 parallel). A comparison of outer TRX test names against `m186a-final-full` finds **zero removed** from the previous **4,044** cases; the only addition is `ReleaseSchedulingTests.ExclusiveTraitAndCollectionCoverBothNativeClassesAndJournalWithoutSerializingOtherTests`. Root TRX identity validation found no duplicate executions. All explicit concurrency and timeout/fatal-child regression tests still run.

| Final gate measurement | Calendar native | Journal editor |
| --- | ---: | ---: |
| Child cases | 87/87 | 1/1 (all original layout/behavior assertions) |
| Child elapsed, s | 69.989 | 10.158 |
| Startup to first STA, s | 2.582 | 2.726 |
| Completed STA action wall sum / work, s | 61.312 | 7.149 |
| Process CPU across STA boundaries, s | 83.969 | 9.219 |
| Journal test duration (TRX), s | — | 7.203 |
| Deadline headroom, s | **50.011** to 120-second child deadline | **22.797** test-duration margin to 30-second inactivity limit |

The Journal inactivity watchdog is not a wall clock starting at process launch; both child wall and actual test duration are shown to avoid mixing those budgets. Final native owner creation **7.85 s / 12 calls**, initial drain **0.38 s**, modal interaction **5.09 s**, theme parsing **0.40 s / two calls**, fixture setup **1.24 s / 61 calls**, scheduled-wait sum **0.20 s**, boundary peak **707.38 MiB**. Both native classes still execute all 86 STA actions; the extra child case is non-STA. Source/STA gate poison, fatal-exception reporting and nested-frame protections remain intact.

Supervision confirms Calendar exited **15:01:09.096 UTC**, Journal started **15:01:09.851** and exited **15:01:20.009**; the parallel phase started **15:01:20.677**. Thus neither heavy child overlapped Infrastructure, grid or other Desktop workloads. The complete gate ran from **14:59:55.740 to 15:05:00.730 UTC**, **304.990 s**, within the unchanged CI ten-minute Test-step bound. Parallel grid **36/36 in 29.518 s** (90.482 s headroom to 120), Journal History **14.733 s** (STA **9.767 s**), and the rest of the suite passed. EF reports no pending model changes via the in-memory design-time factory; tracked and new-file whitespace checks passed.

**Local automated acceptance is green under the documented resource-aware gate.** It does not claim the old unrestricted all-overlap command is reliable or identify the exact OS trigger of the earlier 28.901-second dispatcher drain. Calendar varied **69.989–74.373 s** and Journal test duration **7.203–7.428 s** across the two scheduled observations; future slower hosts still need their own CI evidence. No deadlines were extended and no identical failing full run was repeated until green. Live UI acceptance remains open. The workflow now uses the same script, but these changes are uncommitted, so **no GitHub Actions run verifies this exact resulting tree**.

Exact changes: `.github/workflows/ci.yml`; new `scripts/Test-Release.ps1`; `tests/PersonalTradingJournal.Desktop.Tests/Calendar/CalendarDayModalTests.cs`; `Calendar/CalendarDayPerformanceChartTests.cs`; `Journals/JournalViewTests.cs`; new `TestDoubles/ExclusiveWpfCollection.cs` and `TestDoubles/ReleaseSchedulingTests.cs` under that test project; `README.md`; `docs/ci-wpf-tests.md`; `docs/trading-rules.md`. This is a runner/test-scheduling correction only. No production journal, AI provider, commit, push, merge or M19 work.

## M18.6 — Calendar aggregate deadline and repeated fixture setup (14 October 2026)

Started on clean `develop`, **`1d99a7a5570bad2ebeb853d04edfd32839187b6e`**. The preceding acceptance report remains evidence of a failed full run; an isolated pass is not a substitute. This follow-up changes only synthetic test setup, bounded diagnostics and documentation. No production journal, provider, Git write or M19 work.

### Controlled measurements before the correction

Run directories below are under ignored `artifacts/m145-load/`. Every run uses Release `--no-build`, TRX, the existing 3-minute VSTest inactivity diagnostic, numeric process samples and `PTJ_CALENDAR_TIMINGS=1`. Native isolation remains **120 seconds aggregate / 45 seconds per action**, Desktop **two** concurrent collections, Infrastructure **four**, with assemblies parallel. No retries, skips, sleeps, increased deadlines or reduced concurrent-writer coverage.

- `m186a-native-alone`: one outer supervisor test invokes the entire native suite; **87/87**, **82.946 s**, **37.054 s headroom**.
- `m186a-competing-baseline`: add all Infrastructure `Backups` and `Persistence.Rules` cases, Journal compiled-editor and Calendar grid cases. Native timed out at 120 s after **80/86 STA actions**; Infrastructure **260/260**, Application **37/37**, other Desktop **5/5** passed. The final action had only just entered when termination began; no individual action timeout.
- `m186a-setup-measurement`: same workload with a bounded fixture-initialization timer added, not a retry to select a green result. Again a native timeout: **83/86 actions** completed, Infrastructure **260/260** and other selected cases passed. **233** backup/export/restore fixture initializations consumed **188.560 aggregate seconds**, mean **809.270 ms**, max **5,109.126 ms**. These durations overlap across collections; they are not exclusive suite elapsed time or CPU. Each fixture previously replayed the full empty-database migration chain.

| Native measurement | Alone | Competing baseline | Competing setup measurement | Corrected competing workload |
| --- | ---: | ---: | ---: | ---: |
| Child wall / headroom, s | 82.946 / 37.054 | 120 deadline / none | 120 deadline / none | 118.188 / 1.812 |
| Startup to first STA, s | 4.069 | 5.887 | 6.464 | 8.232 |
| Completed STA actions | 86 | 80 | 83 | 86 |
| Completed action wall sum, s | 74.029 | 101.881 | 101.835 | 99.439 |
| Completed action process CPU delta sum, s | 100.875 | 103.406 | 104.219 | 114.063 |
| Action-boundary peak working set, MiB | 734.00 | 690.43 | 689.75 | 714.71 |
| Owner `Show`, 12 calls, s | 9.97 | 17.49 | 16.97 | 16.15 |
| Owner initial dispatcher drain, 12 calls, s | 1.03 | 0.82 | 0.91 | 0.69 |
| Owner explicit `UpdateLayout`, 12 calls, s | <0.01 | <0.01 | <0.01 | <0.01 |
| Modal open/interaction/return, 12 calls, s | 6.55 | 12.61 | 12.94 | 11.52 |
| Day read completion, 12 calls, s | 0.09 | 0.13 | 0.14 | 0.13 |
| Dialog explicit layout, 12 calls, s | 1.15 | 2.74 | 2.72 | 2.19 |
| Inline Trade read/bind, 12 calls, s | 0.07 | 0.10 | 0.10 | 0.09 |
| Inline dispatcher drain, 12 calls, s | 1.56 | 2.97 | 3.53 | 2.62 |
| Chart layout / bitmap, 21 each, s | 1.26 / 0.48 | 1.30 / 0.53 | 1.63 / 0.54 | 1.46 / 0.47 |
| Theme parsing, two calls, s | 0.66 | 1.00 | 0.72 | 1.01 |
| Native dispatcher scheduled-wait sum, s | 0.18 | 0.34 | 0.27 | 0.25 |

Scopes are inclusive: dispatcher pumps overlap modal actions and must not be added. Native action-gate waits (24.55 s alone, 30.47/36.72/29.80 s in the loaded runs) wait outside the dispatcher behind another native action, not an additional stall. The gate continues to prevent nested `ShowDialog` dispatcher-frame reentry. Low scheduled-wait totals do not measure OS scheduler/GPU waits *inside* `Window.Show`/layout. Process CPU is multi-threaded and cannot be subtracted from wall time to calculate scheduler delay.

This establishes cumulative slower native window/layout/interaction work under load, not one stuck test or a product deadlock. Theme parsing is already cached twice per native child; explicit owner layout and fake-reader completion are small. Infrastructure was a measured competing workload: sampled CPU **358.547 s over 187.370 s** in baseline and **350.313 over 187.455** in setup measurement; grid and Journal children also overlap. The measurements do not distinguish every OS/storage/rendering contribution.

### Narrow correction and preserved isolation

Only `BackupArchiveTests.Fixture`, shared by backup/preflight/restore/export tests, uses the new template. The template is generated once per test process from the current real migrations/initializer, captured using SQLite's backup facility (including WAL), and held as private immutable bytes. All template files/connections are disposed and removed. Each fixture writes a **new, unique** database with `CreateNew`, owns its own service provider/connections/screenshots/root, and still calls the production initializer for migration checks and browse reconciliation. No production database is copied; no mutable connection or records are shared. Dedicated migration/previous-schema fixtures remain unchanged; per-database pool cleanup is retained rather than globally clearing another fixture's handles.

Two regressions verify four simultaneously created fixtures have complete applied migrations, no pending migrations, integrity, foreign keys and isolated seeded rows; dropping a table or adding data cannot contaminate a later clone. A separate case proves an existing fixture database is never overwritten. Existing concurrency, rollback, WAL, corruption, cancellation, backup/recovery/export assertions all continue to execute.

`SyntheticFixtureTiming` is opt-in with `PTJ_CALENDAR_TIMINGS=1` and `PTJ_TEST_RESULTS_DIRECTORY`, bounded to **4,096** records/process. It writes only fixed phase names and elapsed milliseconds to `synthetic-fixture-timings.jsonl`, never seeded content or local database paths. Calendar timings now split broad owner/inline scopes without changing their operation order.

### Verification and remaining risk

`m186a-fixture-regressions`: **2/2 passed**. `m186a-competing-template`: **305/305 outer tests**, zero failures/skips (Application 37, Infrastructure 262, Desktop 6; native 87 and grid 36 child cases are not double-counted). Backup initialization **68.914 aggregate seconds / 239 calls**, mean **288.345 ms**, including the initial shared build wait; snapshot build **5.345 s**, 240 clone attempts **4.781 s** total. The extra attempted clone is the overwrite-protection regression. Infrastructure sampled CPU **334.484 s over 175.111 s**, peak **360.88 MiB** (baseline 368.85 / measurement 350.07 MiB). These support less repeated setup, not a claim that migrations explain all native slowdown. Grid passed in **57.255 s**; Journal editor **28.059 s** (STA **19.740 s**).

The corrected native child passed **87/87 in 118.188 s**, but **1.812 s** remaining is marginal. This was followed by one complete parallel run of the corrected tree, not repeated unchanged full runs until green.

**Final complete parallel Release (`m186a-final-full`): 3,957 passed / 87 failed / zero skipped, 4,044 total.** Domain **490/490**, Application **753/753**, Infrastructure **1,251/1,251**; Desktop **1,463 passed / 87 failed**. The two new Infrastructure regressions account for the increase from 4,042 cases. Release build **zero warnings/errors**; EF reports no pending model changes using the in-memory design-time factory; tracked/new-file whitespace checks passed.

- **Calendar: still failing.** Started **14:39:22.478582 UTC**, deadline **14:41:22.451507**, process-tree cleanup confirmed **14:41:22.849331**. Completed **85/86 STA actions**, completed wall sum **99.184 s**, process CPU deltas **106.266 s**, longest action **3.624 s**, boundary peak **680.00 MiB**. Startup **5.685 s**. The final `ClosedModalRetainsDateFocusAcrossCompetingWindowActivation` entered at **14:41:22.143469**, only **0.308 s** before termination; it is not demonstrated to have stalled. All **86** Calendar failures propagate this shared timeout; no chart-focus assertion or individual 45-second action timeout was recorded. **Zero deadline headroom**.
- Final native measured phases: 12 owner `Show` calls **15.17 s**, initial drain **0.78 s**, explicit owner layout <0.01 s; modal interaction **8.96 s**; dialog layout **1.84 s**; inline drain **2.01 s**, read **0.08 s**; chart layout/bitmap **1.09/0.39 s**; two theme parses **0.90 s**; scheduled dispatcher wait **0.13 s**; 61 fixture preparations **2.75 s**. Approximately 14.82 s lies outside completed STA actions/startup/the final partial action, including test transitions, setup, boundary diagnostics and reporting; it is **not measured OS scheduler wait**. Native sampled CPU **113.688 s over 112.676 s**, peak **716.92 MiB**; concurrent Infrastructure **196.625 over 117.046 s**, peak **381.82 MiB**. Grid and other Desktop children overlap; the data does not isolate one OS/GPU/storage bottleneck.
- **Journal: separate unresolved failure.** `JournalViewTests.CompiledEditorBindingsScopeVetoAndLightDarkNormalHighDpiLayouts` was aborted by the child's existing **30-second VSTest inactivity** diagnostic. Child wall **45.811 s**, launch-to-STA **9.158 s**, with a hang dump and phases showing continued progress during collection: layout ended at STA elapsed **30.239 s**, bitmap at **33.682 s**, and next Dark view construction began at **33.698 s**. Completed phases include 22 view constructions **5.18 s**, 22 measures **6.76 s**, 22 layouts **4.50 s**, 28 bitmap renders **7.00 s**, plus arrangements and resource/input work. Those inclusive timings do not prove one view deadlocked; no speculative Journal code/assertion/deadline change was made. Its earlier targeted pass does not close the full-load failure.
- Grid **36/36 in 64.649 s**, Journal History **27.897 s** (STA **18.117 s**). Full-run backup setup **60.544 aggregate s / 239 calls**, template **1.019 s**, clones **6.751 s / 240 attempts**. The setup improvement persists, but does not solve the native or Journal deadline risk.

**M18.6 automated acceptance remains blocked.** The demonstrated redundant-fixture cost was reduced without changing concurrency or coverage, but remaining cumulative WPF/window/test-transition contention is unresolved. Future work needs more narrowly attributed CPU/off-CPU/native creation and inter-action diagnostics, not a longer deadline or selecting a passing rerun. Live acceptance and GitHub Actions for this uncommitted tree are also open; this task makes no new live/CI claim.

Raw supervision/phase/timing/TRX/process logs and `native-actions.csv` are retained in the named run directories. Exact changes: `README.md`, `docs/ci-wpf-tests.md`, `docs/trading-rules.md`, `tests/PersonalTradingJournal.Desktop.Tests/Calendar/CalendarDayModalWindowTests.cs`, `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/BackupArchiveTests.cs`, new `Backups/MigratedBackupTestDatabase.cs`, new `Backups/MigratedBackupTestDatabaseTests.cs`, and new `Persistence/SyntheticFixtureTiming.cs` under the Infrastructure test project.

## Quiet Rules reordering — Calendar follow-up (13 October 2026)

Started on clean `develop` at `35f9f48149174fc4c9629a6057dac0b0a60dbe19`. The preceding ordering task's failing Calendar case was `WindowControlsRetainModalAndDraftThenCloseInEveryState(Light, Normal)` at its **45-second STA action** deadline, not the 120-second aggregate limit. This task adds only bounded phase breadcrumbs to that test: owner creation/layout, modal invocation, day/Trade loading, editor opening, maximize/minimize/restore, resize, dirty close veto, final dismissal, focus and cleanup. It does not change Calendar production code, assertions, action/child deadlines, two-collection Desktop concurrency or four-collection Infrastructure concurrency.

The first complete parallel run, `artifacts/m145-load/rules-quiet-full`, passed **4,037/4,038** (Domain 490, Application 753, Infrastructure 1,249, Desktop 1,545 passed / 1 failed). Calendar native completed **87/87 in 95.026 seconds**, with **24.974 seconds** to its unchanged 120-second bound. The previously failing modal case passed; this does not establish its old cause or a fix.

The sole failure was instead `CalendarStaTestTests.NativeUnhandledDispatcherCallbackFailsItsChildWithTheOriginalError`: the parent expected the synthetic fatal child to exit with the original `InvalidOperationException`, but its **30-second** process deadline expired. Its `native-fatal-probe.log` records `callback entered`, `filter requested catch=True`, and `unhandled event handled for pump unwind=True`. The STA action had completed at 0.097 seconds; no evidence shows the original modal action hanging. Between child launch at 11:19:53.776 UTC and forced cleanup at 11:20:23.781 UTC, the supervisor recorded native threads waiting but no managed stack or completed fatal report. Those observations do **not** distinguish pump-shutdown failure from later crash/reporting delay; no speculative harness correction or deadline increase was made. Raw logs remain in `calendar-sta-unhandled-native-20261013-111953-f7d0436fb6b04870bfe590f38a43ef4c` under that run.

The Rules accessibility regression then demonstrated that a plain Border has no automation peer and that the default framework peer does not expose this row's keyboard focusability. A dedicated list-item peer now exposes the focusable row, name, position and keyboard instructions. The final Rules-focused run passed **6/6** after that correction; the initial broader focused run passed **151/151**. A final complete run tests the changed peer, rather than repeating the identical tree until green. Final results are recorded in `docs/trading-rules.md`; an eventual pass does not resolve the unproven fatal-child timing mechanism.

| Parallel run | Calendar native cases | Child seconds | Headroom to 120 s | Four modal action times, seconds |
| --- | ---: | ---: | ---: | --- |
| First (`rules-quiet-full`) | 87/87 | 95.026 | 24.974 | 2.204 / 2.315 / 2.338 / 2.761 |
| Final (`rules-quiet-final-full`) | 87/87 | 113.544 | 6.456 | 2.804 / 3.490 / 3.005 / 3.275 |

These are measured elapsed times with different test schedules, not a demonstrated performance improvement. The final aggregate has limited headroom. The earlier modal stall and the first run's fatal-child exit delay remain unresolved risks; production changes, retries, sleeps, skips, serialization and deadline increases were not used to obtain these results.

Final complete parallel Release: **4,038/4,038**, zero failures/skips (490 Domain, 753 Application, 1,249 Infrastructure, 1,546 Desktop). The fatal-dispatcher probe's final child exited in **7.780 seconds** and its parent assertion passed; this is not evidence of a causal fix. Release build: **0 warnings/errors**; EF model consistency and whitespace checks pass. Exact file inventory is in the linked Rules documentation.

No production journal or provider was accessed. Live input and matching CI acceptance remain separate from automated WPF renders and local tests.

## M18.3 — synthetic wheel and Save invocation follow-up (12 October 2026)

Started on clean `develop` at `b094d6fda04f1b7d02d07804068aa4ddb5c8203f`; the user's M18.3 work was already committed. This correction changes tests and documentation only. Production wheel routing, Playbook Save/readback, Rules, schema, concurrency settings and all existing deadlines are unchanged.

### Chronology and classification

The earlier complete run (`artifacts/m183-full`) passed 3,943/3,945. Its first failures were Journal Trade Context `Assert.True(wheel.Handled)` (line 123) and Playbook SQLite automation `Assert.NotSame(previousTask, SaveCommand.ExecutionTask)` (line 129). The former stopped in the first Light layout after 0.954 STA seconds, not at a deadline. Neither artifact captured keyboard modifiers or whether a command had synchronously completed. An unchanged targeted pass did not establish a cause.

Instrumentation now records bounded content-free interaction state in each child's `wpf-interactions.log`: modifiers, scroll extent/offset, dispatcher thread/shutdown, focus element type, window activation, button/command enablement, click count, task completion/reference equality, validation-attempt count and displayed revision number. No authored text, filenames or database rows are logged. The file has a 128-event/1,024-character-per-state bound. Existing phase/timing and supervisor logs retain startup, cleanup and timeout diagnostics.

- **Journal:** the instrumented baseline passed in isolation and alongside export/Rules work, with modifiers `None`, no focused element, offset 0, and 752.12/769.32 DIP scrollable height. A controlled test-thread Shift state reproduces the original unhandled-event observation exactly (`handled=False`, offset 0): production deliberately leaves Shift-wheel alone. Removing Shift for the synthetic ordinary event yields `handled=True`, offset 48 in all four Light/Dark 1100/96 and 480/240 cases. This demonstrates an uncontrolled-input test assumption, **not proof that Shift was down during the old failure**. No production defect or scheduling hang was demonstrated.
- **Playbook:** baseline automation delivered one click per save and displayed revisions 1 and 2. A regression with the real WPF automation peer and AsyncRelayCommand demonstrates two successful command executions sharing `Task.CompletedTask`. Thus task reference identity cannot count invocations. This was reproduced under competing load; it does **not prove** the old real-SQLite failure used that exact completion schedule. There is no recorded evidence of an unsuccessful production commit. The actual SQLite image/save/readback/restart test continues unchanged in purpose.

### Narrow test corrections and preserved coverage

`SyntheticKeyboardState` saves/restores only the current STA's keyboard-state table around a synchronous synthetic event; it never sends physical input, changes another app's keys or pumps while overridden. [Windows documents that isolation](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setkeyboardstate). The regression exercises Shift bypass, ordinary input while ambient Shift is held, and restoration even when an event throws. The same fixture fixes the previously reported Journal History synthetic-wheel assumption. Every handled/scroll-distance/layout assertion remains. Real Shift-wheel behavior is explicitly asserted, not removed.

The Playbook fixture now observes exactly one routed Click from one `IInvokeProvider.Invoke`, coordinates through that event, then awaits command completion and asserts the exact new persisted revision, success UI, retained example and fresh-process read. It does not equate an idle marker or a different Task object with a command execution. The regression covers both cached synchronous completion and dispatcher-deferred completion. All invalid-field/example focus/scroll assertions remain; validation clicks also use the same observation. A missing Click remains bounded by the existing **30-second STA / 90-second child** supervisors, with a specific automation-dispatch breadcrumb. There is no retry, sleep, skip, deadline extension or bypass of the actual button binding.

### Measurements

These are separate supervised experiments, not retry-until-green runs. Desktop remains at two collections and Infrastructure at four; assemblies run concurrently. The baseline pair has no Infrastructure load; the controlled load adds all 39 PortableExport cases and compiled Rules renders. Final focused load additionally exercises Journal History and modifier restoration (46 cases total). Different action schedules and the extra coverage mean these are observations, not proof of a speedup.

| STA wall seconds including cleanup | Baseline pair | Baseline + exports/Rules | Corrected pair | Final focused load | Final full load |
| --- | ---: | ---: | ---: | ---: | ---: |
| Journal Trade Context (four layouts) | 2.203 | 3.100 | 2.102 | 3.331 | 7.887 |
| Playbook SQLite write/validation | 3.115 | 4.777 | 2.992 | 4.192 | 6.173 |
| Playbook fresh-process restart read | 1.519 | 1.858 | 1.541 | 1.752 | 2.475 |
| Journal History | — | — | — | 11.332 | 10.268 |

Launch-to-STA time (runtime/discovery/fixture work, outside the 30-second action budget) for corrected pair → final focused → full load was **2.801 → 4.992 → 10.005 s** for Journal and **4.049 → 7.301 → 11.695 s** for Playbook write. Corresponding supervised child wall times were **5.166 → 8.693 → 18.598 s** and **7.406 → 11.986 → 18.610 s**. These show load sensitivity without a missed deadline. They do not identify a particular export/database operation as blocking UI input. Native scheduling/CPU/memory and process samples are preserved in raw artifacts, not inferred by subtracting multi-thread CPU from wall time.

Final focused ordinary-wheel routing plus dispatcher flush took **0.669–4.610 ms**; valid Save invoke/idle/completion scopes took **939.082 / 304.293 ms**. Focus changed from TextBox to Button after the first save; measured owner activation was true. This is not a requirement for foreground activation. Process-sample and STA logs retain CPU/memory/dispatcher boundaries; no per-operation stall or contention-induced failure was observed in these experiments. Measurements are inclusive and must not be added as exclusive scheduler waits.

Raw evidence is under ignored `artifacts/m145-load/m183a-{isolated,baseline-load,corrected-load,final-focused,final-isolated,final-full}`. Baseline pair **2/2**, baseline load **44/44**, initial corrected load **45/45**, final focused load **46/46**, corrected pair **3/3** passed. The added restoration Fact accounts for the one new case; synchronous/deferred automation and Shift regressions are assertions within the existing isolated cases.

The **final complete parallel Release run passed 3,946/3,946**, zero failures/skips: Domain **485**, Application **738**, Infrastructure **1,225**, Desktop **1,498**. Calendar native exited successfully in **99.309 s** (**20.691 s** headroom to 120 s), grid in **33.460 s**. All original child cleanup/fatal-dispatch/timeout regressions still run; the intentionally crashing `calendar-sta-unhandled-native` child is an expected harness assertion, not a suite failure. Release build: **0 warnings/errors**. EF consistency: no pending model changes using the in-memory design-time factory. Tracked and new-file whitespace checks: passed.

**Local automated acceptance is green.** The historical failure triggers remain unproven; these changes remove demonstrated harness assumptions, not evidence that every future Windows scheduling/input environment will pass. Live interaction and matching GitHub Actions remain separate acceptance gates. The eight changes are uncommitted, so no CI run can test this exact tree yet. No production data or providers were accessed; M18.4 is not started.

Exact files: `tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalTradeContextViewTests.cs`, `tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalHistoryViewTests.cs`, `tests/PersonalTradingJournal.Desktop.Tests/Setups/PlaybookSqliteViewTests.cs`, new `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/SyntheticKeyboardState.cs`, new `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/WpfStepDiagnostics.cs`, `README.md`, `docs/trading-rules.md`, `docs/ci-wpf-tests.md`.

## M17.7 timeout follow-up — bounded Infrastructure fan-out (12 October 2026)

Started from clean `develop` at `65df73dd9a33aae95c1be184f9fb33ae7b8e22dc`; all earlier M17 changes were already committed. The failed acceptance run below remains evidence, not a result erased by a later pass. It shows cumulative Calendar startup/layout slowdown and Journal layout budget exhaustion, **not a demonstrated production deadlock**. Native dispatcher scheduled waits were only 0.44 seconds in total; no timed-out action had used its individual 45-second limit. Infrastructure was the largest sampled competing test process (284.78 CPU seconds during that native child's lifetime).

The execution-layout correction bounds **Infrastructure** xUnit collection concurrency at four instead of the host-CPU default (eight on this machine). Desktop retains its existing two-collection bound. Assemblies still run concurrently, four Infrastructure collections can overlap, and explicitly concurrent SQLite/import/backup test operations are unchanged. This is not global serialization or a change to production services. Every Calendar/Journal assertion, child isolation, fatal-exception reporting, poisoned-dispatcher rejection, nested-frame gate, and 120-/30-/45-second deadline remains unchanged. No retries, skips, sleeps or selected-case removal were added.

Two targeted measurements used identical filters: Backups, Playbook persistence, Calendar modal/chart/grid and the two failing Journal layout cases, with opt-in phase timing, process samples and CI-style diagnostics. Each passed **476/476** (37 Application, 279 Infrastructure, 160 Desktop). Their different xUnit schedules are important: these results **do not show a universal isolated speedup** or quantify OS scheduler wait.

| Measurement, seconds | CPU-default baseline | Four Infrastructure collections |
| --- | ---: | ---: |
| Native child / remaining to 120 | 94.327 / 25.673 | 98.243 / 21.757 |
| Native startup to first STA | 5.279 | 4.983 |
| Native completed-action wall sum (86 actions) | 78.129 | 76.961 |
| Native completed-action CPU sum | 89.844 | 86.500 |
| Native action-boundary peak working set, MiB | 742.191 | 639.070 |
| Grid child / remaining to 120 | 30.621 / 89.379 | 58.007 / 61.993 |
| Journal editor STA / remaining to 30 | 12.364 / 17.636 | 23.159 / 6.841 |
| Journal History STA / remaining to 30 | 16.451 / 13.549 | 9.278 / 20.722 |

Inclusive scopes cannot be added as exclusive costs; process CPU includes multiple threads and is not wall time. The selected concurrency bound limits independent migration/SQLite/archive fixture pressure rather than claiming that a particular export call blocks WPF. Exact OS/storage scheduling and the old Journal dump's blocked instruction remain unproven. Raw evidence: ignored `artifacts/m145-load/m177-timeout-{baseline-load,bounded-load,bounded-full}/`, including supervisor logs, phase logs, JSONL timings, process samples, TRX and VSTest diagnostics. Existing harness regressions and all fixtures, not an assertion about a configuration constant, are the verification of this test-runner-only change.

### Final complete parallel Release verification

One complete parallel solution run after that correction passed **3,884/3,884**, zero failures/skips: Domain **476**, Application **732**, Infrastructure **1,203**, Desktop **1,473**. All four assembly TRX start times fall within 0.5 seconds, confirming this was not a serialized solution run. Infrastructure TRX wall time was **209.444 s**, Desktop **262.392 s**. The build had **0 warnings/errors**, EF reported no pending model changes, and tracked/new-file whitespace checks passed.

| Full-load measurement | Prior failed acceptance run (CPU default) | Final four-collection run |
| --- | ---: | ---: |
| Native child wall / headroom, seconds | 120 / none | **98.433 / 21.567** |
| Native startup to first STA, seconds | 20.957 | 5.512 |
| Native completed STA actions | 27 before termination | **86** (87 child cases passed) |
| Native owner-show/layout sum, seconds (12 calls) | 41.67 | 11.75 |
| Native modal-open/interactions sum, seconds (12 calls) | 23.66 | 6.56 |
| Native dispatcher scheduled-wait sum, seconds | 0.44 | 0.10 |
| Grid child wall / headroom, seconds | 120 / none | **21.247 / 98.753** (36 cases) |
| Grid startup to first STA, seconds | 17.208 | 3.548 |
| Journal editor STA / headroom, seconds | 30.365 / none | **12.535 / 17.465** |
| Journal History STA / headroom, seconds | 30.036 / none | **8.964 / 21.036** |

Final native completed-action wall sum **77.466 s**, CPU sum **91.234 s**, boundary peak working set **770.723 MiB**. Sampled Infrastructure CPU during the native lifetime was **187.359 s** over 98.433 wall seconds (versus 284.78 over the prior 120-second interval); its sampled peak was **445.191 MiB**. These are observations from different schedules, not a controlled proof that four collections eliminates all possible contention. The target-only comparison above retains the counterexample where the new configuration was slower. No memory leak, application deadlock, lost Journal read, or exporter defect was demonstrated. The correction is a bounded test-runner resource policy, not a product change or a guarantee against overloaded machines.

Local full-suite acceptance is green for this worktree. Matching GitHub Actions is **not verified**: the new test configuration and docs are uncommitted, so no run can test this exact change yet. The existing workflow requires a push to main or a PR targeting main; the user must commit/push develop and open/update that PR, then check its tested head/merge tree. Do not change triggers or call an earlier green run acceptance. Live Playbook Save/input and remaining M17.7 manual gates are unchanged. No production data or provider access occurred.

## M17.7 acceptance — unchanged Calendar deadlines (12 October 2026)

Acceptance audit of `develop` **`206dfd3e10d0831eb4750650eefb4c89b4fc0ae4`**, initially clean. No product/test-harness code was changed. The synthetic live app was stopped before the full run. One complete parallel Release run used the existing `Measure-Load.ps1 -Name m177-final-full -CiDiagnostics`, with phase timings, one-second process sampling, TRX and the unchanged 120-second child / 30-/45-second action bounds. No retries, skips, concurrency reductions or deadline increases.

| Measurement | Calendar native | Calendar grid |
| --- | ---: | ---: |
| Supervisor start UTC | 13:55:41.0736762 | 13:54:54.5636722 |
| Timeout UTC | 13:57:41.0391595 | 13:56:54.5062771 |
| Startup to first STA, seconds | 20.957 | 17.208 |
| Completed / started STA actions | 27 / 28 | 32 / 33 |
| Cleanup confirmed UTC | 13:57:41.3692145 | 13:56:54.9720336 |

Native completed-action wall sum **89.374 s**, process-CPU delta sum **95.563 s**, longest completed action **7.014 s**, peak action-boundary working set **548.570 MiB**. The final `JournalActionStaysInlineAndProtectedModalCloseKeepsTradeAndJournalDrafts` began its test action at **13:57:36.6917489**, only **4.347 s** before termination; the preceding same-named action completed in **6.036 s**. This is not evidence of an individual 45-second hang. The grid's last `PopulatedGridRendersSignsAndCenteredWeeklyOnlySaturdayAcrossThemes` began at **13:56:53.7675845**, only **0.739 s** before termination. Both trees were killed and exit confirmed; no completed child TRX was produced.

Native inclusive timing scopes: 12 owner-show/layout calls **41.67 s**, 12 modal open/interactions **23.66 s**, 12 inline view/layout calls **7.48 s**, 48 modal pumps **10.30 s**, two theme parses **2.78 s**, 21 chart layouts **2.39 s**, 21 bitmaps **0.64 s**. The two-parse resource reuse remains effective. Dispatcher scheduled waits total **0.44 s**; the **67.33 s** gate wait overlaps other active cases and must not be counted as additional idle time or removed (it protects nested-frame exclusion). These inclusive scopes overlap and are not an additive wall-time breakdown.

During the native lifetime, sampled Infrastructure testhost PID 24152 used **284.78 CPU s**, native testhost PID 25408 **107.59 CPU s**, grid testhost PID 24464 **62.28 CPU s**, and parent Desktop host PID 15760 **36.20 CPU s**. Multi-threaded CPU time is not wall time or a measure of scheduler wait. The previous preview-layout full run passed the native child in **86.097 s**; this audit's unchanged binary workload slowed across startup, window/layout and other actions while assemblies overlapped. This supports aggregate budget exhaustion under load, not a demonstrated new Playbook, SQLite, picker or dispatcher-lifetime defect. It does not isolate an individual competing test or precise OS scheduling cause. No sound code correction has been established; the timing variability remains **unresolved**, and the passing focused Playbook suite is not full-suite acceptance.

Final parallel result: **3,760 passed / 124 failed / zero skipped**, 3,884 total. Domain 476, Application 732 and Infrastructure 1,203 all passed; Desktop passed 1,349 and failed 124. **86 failures** propagate native timeout and **36** propagate grid timeout. Two additional failures must not be hidden by that grouping:

- `JournalHistoryViewTests.CompiledHistoryPagesAndSnapshotsWrapInBothThemesAndRemainReadOnlyAtHighDpi`: `History Light 480 DIP / 240 DPI` ran from elapsed 16.079 to 26.745 s; the Dark high-DPI phase began at 26.747 s and the STA timeout fired at **30.036 s**. Last sample: CPU **35.609 s**, working set **160.672 MiB**, background STA alive, zero pending ThreadPool work. The phase is broader than a single operation; no exact blocked instruction is established.
- `JournalViewTests.CompiledEditorBindingsScopeVetoAndLightDarkNormalHighDpiLayouts`: final Arrange ended at elapsed **30.214 s**, immediately followed by UpdateLayout; STA timeout at **30.365 s**, CPU **32.563 s**, working set **155.238 MiB**, zero pending ThreadPool work. The unchanged 30-second VSTest inactivity collector aborted the child and retained `testhost_20972_20261012T165951_hangdump.dmp`. The dump was not decoded; the final short phase does not prove a deadlock. Both Journal children exited with cleanup confirmed.

All Playbook tests, including the compiled Save/write and separate restart-read children, passed. No corrective production/harness change is supported by these cumulative-load observations alone. The unresolved full-suite result takes precedence over 163 focused passes; there was no repeat full run to select a greener outcome.

Raw phase/timing JSONL, `native-actions.csv`, process samples, supervisor diagnostics, final parent TRX and VSTest logs remain in ignored `artifacts/m145-load/m177-final-full`. Final suite totals and acceptance gates are recorded in [M17.7 acceptance](playbook.md#m177--integration-acceptance-12-october-2026). This run is local automated evidence only, not live UI or matching GitHub Actions.

## Pre-M17.7 Save diagnostics and Calendar full-load measurement

12 October 2026; started clean on `develop`, `434a0055591cd71dba5715baf1fee3cca34d0452`. The only production correction concerns Playbook Save readback/reporting; it does not run in the Calendar fake-data tests. The native harness, assertions, two-collection Desktop concurrency, process isolation, 120-second aggregate / 45-second operation deadlines and nested-frame gate are **unchanged**. M16.7a's two theme-resource parses are still effective; no repeated-resource regression was found.

Two complete parallel Release measurements used the existing `Measure-Load.ps1 -CiDiagnostics`, `PTJ_CALENDAR_TIMINGS=1`, TRX, blame collector and one-second process sampling. The first measures the original binaries; the final measures the Save correction and new diagnostics regressions. These are not a retry loop or selected passing native cases. Raw evidence is retained under ignored `artifacts/m145-load/pre-m177-diagnostic-{baseline,final}/` (supervision, phases, timing JSONL, process samples, TRX and VSTest diagnostics).

| Native measurement | Before Save correction | Final full load |
| --- | ---: | ---: |
| Supervisor start UTC | 21:37:56.2928652 | 21:43:33.3223380 |
| Exit / timeout UTC | 21:39:40.1244662 | 21:45:33.3089908 |
| Wall seconds / headroom to 120 | 103.832 / 16.168 | deadline / none |
| Child cases / completed STA actions | 87 passed / 86 | incomplete / 84 |
| Startup to first STA, seconds | 5.269 | 6.894 |
| Sum of exclusive test-action intervals, seconds | 83.127 | 92.141 (84 completed only) |
| Longest completed exclusive test action, seconds | 3.007 | 3.351 |
| Owner show + initial layout, 12, seconds | 14.693 | 17.036 |
| Modal interaction/return, 12, seconds | 7.638 | 7.682 |
| Modal dispatcher pumps, count / seconds | 220 / 9.177 | 218 / 11.386 |
| Theme resource loads, count / seconds | 2 / 1.294 | 2 / 0.733 |
| Chart layout / bitmap, 21 each, seconds | 0.916 / 0.410 | 0.751 / 0.306 |
| Inline view/layout, 12, seconds | 1.805 | 1.792 |
| Dialog close + owner close, seconds | 0.546 + 1.463 | 0.659 + 1.920 |
| Fixture activation, 60, seconds | 1.740 | 3.345 |
| Dispatcher post-to-execution, total / max seconds | 0.327 / 0.216 | 0.687 / 0.216 |
| Intentional gate wait (overlaps active work), seconds | 24.559 | 29.039 |
| Last child CPU / boundary working set | 108.328 s / 423.480 MiB | 103.500 s / 409.363 MiB |
| Sampled peak child working set | 754.14 MiB | 725.67 MiB |
| Sampled Infrastructure CPU during native lifetime | 139.81 s | 281.23 s |
| Other sampled Desktop hosts' CPU in that interval | 23.98 + 9.53 + 19.50 + 3.66 s | 45.72 + 29.23 s |

Exclusive action durations pair `start; phase=test action` and `assertions completed` UTC stamps. Per-case `elapsed` also includes waiting for the shared STA and must not be summed as wall time. Inclusive named scopes overlap; process CPU includes renderer/helper threads and can exceed wall time. Process samples are approximate interval endpoints, not a proof of causation. No machine-wide priority, affinity or DPI change was made.

The final unfinished action was `ViewExpandsExactTradeWhileKeepingModalOpen`, entering at **21:45:32.5954156 UTC**, only **0.714 seconds** before termination. Completed actions remained short, and the last boundary had zero pending ThreadPool work. This is cumulative budget exhaustion under varying parallel CPU/native-window load, **not evidence of a 45-second action hang**, an unbounded dispatcher leak, or Playbook persistence blocking Calendar. Infrastructure used roughly twice as much sampled CPU during the final native window, with different Desktop children/order overlapping. The logs cannot isolate a single Infrastructure test or an exact OS rendering/scheduling wait as the cause. No supported additional harness correction was identified; no speculative change, retry, skip, broader deadline or global serialization was applied. Process-tree cleanup was confirmed at **21:45:33.5179909 UTC**.

The baseline full suite passed **3,863/3,863**. The final full-suite result takes precedence: **3,781 passed / 86 failed / zero skipped** (Domain 476, Application 732, Infrastructure 1,196 passed; Desktop 1,377 passed / 86 failed). All failures propagate the native timeout: acceptance is **not green**, even though all 46 focused Playbook tests and their full-run counterparts pass. Release build has zero warnings/errors, EF reports no pending model changes, and whitespace checks pass. The earlier failed run's 72 completed actions (documented in Playbook) and this run's 84 reinforce the observed variability, not a claim that the timeout has been fixed. Matching GitHub Actions remains open for the uncommitted correction; local testing is not CI or live mouse/keyboard acceptance.

## M16.7a — Calendar native full-load deadline (2026-10-10)

Started on clean `develop`, `4232c32834fbd97fb91c57c733ff36c88174d416`, with M16.1–M16.7 and the M16.6a Journal correction already committed by the user. Only test-resource preparation and its regression change; no product, backup/restore, schema, navigation or economic behavior changes. M16.8 is not started.

### What the failed run actually shows

M16.7's initial complete run passed 3,696 tests; its final run had 3,611 passing tests and 85 propagated failures from one `calendar-native` aggregate timeout. The failed child's **65 started actions all completed**, the last in **0.513 s**, before process termination at **120.136 s**. No unfinished 45-second action or failed interaction assertion appears in its phase log. The supervisor confirmed process-tree cleanup. The first parent name is not the location of a stalled action.

Pairing actual `STA started`/`STA finished` UTC records (rather than using the per-case stopwatch, which includes waiting behind the other native test class) gives **78.373 s** of action work for 85 actions in the passing run versus **97.688 s** for only 65 in the failed run. Process startup to first STA was **4.246 → 6.930 s**. Comparable groups slowed broadly: 12 initial-owned-panel actions **21.99 → 27.37 s**, 12 responsive-column actions **9.39 → 16.28 s**, four window-control actions **10.01 → 13.08 s**, and six backdrop actions **8.72 → 11.67 s**. Thus cumulative work and intervening overhead exhausted the aggregate budget; the logs do not support a single hung action. They do not identify the exact OS scheduling/rendering wait, and the original run did not enable fine-grained resource/dispatcher timings.

The passing native child started about 105 seconds into the overall run; the failed child started near the beginning and overlapped Infrastructure and new Settings children. M16.6a had 1,117 Infrastructure and 1,380 Desktop tests; M16.7 retained those and added 35 Desktop cases. New Settings/maintenance render and startup checks launch three supervised child processes; owner-close coverage launches another. Picker choices and confirmation are fakes, **not real native file dialogs**. The real isolated backup/export/restore test adds work against a disposable SQLite dataset. No evidence shows that a picker waits for user input or that backup code locks Calendar's fake readers.

### Targeted measurement and correction

Before repeating the full suite, the unchanged native child was measured alongside **all 199 backup/export/preflight/restore Infrastructure tests**, 37 contract tests and all 35 M16.7 Desktop tests. The existing one-second process sampler and `PTJ_CALENDAR_TIMINGS=1` were used; no affinity, scheduling priority, concurrency cap or deadline changed. This baseline passed 357 tests: native **86/86 in 102.199 s**, with **17.801 s** headroom. Infrastructure consumed about **208.22 sampled CPU seconds**; the native testhost **112.06**, the parent Desktop host **14.97**, with additional short-lived UI children. CPU time includes helper/renderer threads and is not wall time; subtracting it from wall time does not measure a wait.

The supported avoidable cost was **85 repeated parses of unchanged shared theme resources, 10.24 s**. `CalendarViewLayoutTests.SharedThemeResources` now retains one Light and one Dark dictionary **only in the native child, on its owning STA via thread-local storage**. All other callers, including the grid's short-lived dispatchers, still receive fresh dictionaries. No Application-wide/cross-thread WPF resource cache is added. Child process exit owns final resource cleanup, as it already owns the native renderer. Existing tests do not mutate the shared dictionaries; every compiled view, window, bitmap, interaction and assertion still runs.

The new native regression requires same-theme reuse, distinct Light/Dark dictionaries/styles/colors, usable styles on the owning STA, shared-style use by two controls, and rejection of background-thread access. An intermediate run passed all 86 existing native cases in **90.149 s**, but this new regression incorrectly required a sealed Style to retain a dispatcher. WPF had detached it (`Dispatcher == null`). The assertion was corrected to check access and require ownership for **unsealed** styles; that intermediate failed result is retained, not counted as acceptance. This does not weaken any pre-existing assertion.

All original 120-second aggregate and 30/45-second operation bounds, supervisor cleanup, fatal-exception handling, timeout poisoning, nested-frame exclusion, two concurrent Desktop collections and parallel solution execution remain unchanged. No retry, skip, arbitrary sleep, global serialization or production change is used.

Raw phase, timing JSONL, process samples, per-action CSV, supervision and TRX evidence remains under ignored `artifacts/m145-load/m167a-{baseline-load,reuse-load,final-focused,final-full}/`. The original passing/failed M16.7 logs remain in `artifacts/m167-{full,final-full}/`. The measurement script initially invoked through Windows PowerShell 5 could not use `ProcessStartInfo.ArgumentList` and started no tests; it was then run in the existing PowerShell 7 shell. No test result was retried by that correction.

### Measured before/after under the same targeted competing workload

| Measurement (seconds unless stated) | Baseline | Final correction |
| --- | ---: | ---: |
| Native child / remaining to 120 s | 102.199 / 17.801 | 98.945 / 21.055 |
| Cases / completed STA actions | 86 / 85 | 87 / 86 (one added regression) |
| Startup to first STA | 5.739 | 8.739 |
| Sum of action wall time | 85.673 | 77.572 |
| Gaps between actions (fixtures/scheduling/runner work) | 9.589 | 11.474 |
| Sum of process CPU changes within action boundaries | 103.344 | 91.609 |
| Resource loads / total time | 85 / 10.24 | 2 / 0.49 |
| Owner Show + initial layout, 12 | 14.47 | 13.92 |
| Modal interaction/return, 12 | 6.95 | 6.85 |
| Dispatcher pumps, 221 | 8.48 | 9.25 |
| Chart layout / bitmap, 21 each | 0.96 / 0.33 | 0.95 / 0.34 |
| Dialog + owner close, 12 each | 0.59 + 1.23 | 0.59 + 0.97 |
| Fixture activation, 61 | 1.83 | 1.72 |
| Dispatcher scheduled wait total / maximum | 0.330 / 0.092 | 0.165 / 0.046 |
| Intentional native action gate wait, overlapping active work | 28.065 | 23.897 |
| Peak action-boundary working set, MiB | 736.57 | 833.16 |

The final targeted run passed **358/358** (37 Application, 199 Infrastructure, 122 Desktop including all 35 new M16.7 cases). Native **87/87** passed; no failures/skips. This is not an isolated-only pass. The 9.75-second reduction in resource preparation is directly measured; the smaller 3.254-second whole-child improvement also includes increased startup/inter-action time. Timings are inclusive scopes, not additive components. Gate waits overlap the other native class's action; they preserve nested-frame exclusion and are **not** extra idle time. Dispatcher readiness totals below 1 ms and short post-to-execution waits do not support changing priority or that safety gate. No reduction in peak memory is claimed. The comparable sampled Infrastructure work was **208.22 → 202.94 CPU seconds**; all 199 cases remained present. Scheduling/order and unrelated machine activity are not held perfectly constant, so this does not prove a single OS-level cause for the original timeout.

### Final verification and limits

The **final complete parallel Release suite passed 3,697/3,697**, zero failures/skips: Domain **454**, Application **710**, Infrastructure **1,117**, Desktop **1,416**. This includes all **51** previously selected M16.7/Settings/resource checks and the preserved Journal editor regression (**11.338 s**). The native child passed **87/87** in **73.249 s**, **46.751 s headroom**; grid passed **36/36** in **38.122 s**. All cleanup/timeout/fatal/nested-frame regressions still passed. There was one final full-suite run, not a retry loop.

The final native child started at **20:04:58.619 UTC** and exited at **20:06:11.868 UTC**; it started after Infrastructure completed, unlike the failed M16.7 schedule. Its 86 actions totalled **64.211 s**, process CPU changes **84.719 s**, with two resource parses **0.39 s**, 221 dispatcher pumps **7.94 s**, and 21 chart layout/bitmap pairs **1.05 / 0.29 s**. That final full-run margin is not a controlled estimate of the fix's effect. The earlier deliberately overlapping targeted workload establishes the narrower measured improvement; execution-order/host variability remains a risk, not a guarantee of every future runner's headroom.

Release build: **0 warnings/errors** (`artifacts/m145-load/m167a-final-build.log`). EF model consistency: **no pending changes**, existing in-memory design-time factory. `git diff --check`: passed. Exact changed files: `tests/PersonalTradingJournal.Desktop.Tests/Calendar/CalendarViewLayoutTests.cs`, `tests/PersonalTradingJournal.Desktop.Tests/Calendar/CalendarDayModalRunnerTests.cs`, `README.md`, `docs/ci-wpf-tests.md`, `docs/backup-restore.md`. HEAD remains `4232c32834fbd97fb91c57c733ff36c88174d416`, branch `develop`; five modified files, no commit/push/merge. No production journal, provider calls or new feature work was involved.

**Local automated acceptance is green on this final run.** Live restore/picker/restart acceptance and GitHub Actions verifying these exact uncommitted changes remain open. A new matching PR workflow after the user's commit/push is required; an earlier green commit is not verification. The supported correction removes repeated resource work, not a claim to have identified every Windows scheduling delay or eliminated all future timing variance. M16.8 has not started.

## Test execution and layout contract

Calendar grid tests and modal/chart tests each use a separate supervised test process with named background STA dispatchers. Grid cases own and shut down their dispatchers. Native modal/chart cases share one background STA and a running dispatcher for the child lifetime. A gate outside that dispatcher admits one native action at a time, so a nested ShowDialog/PushFrame cannot execute another case. Other test collections and assemblies remain parallel. Each parent group shares its child-suite result; a child-suite failure therefore appears against multiple parent names. Inspect the child TRX rather than interpreting every parent failure as an independent assertion failure. No tests are skipped, retried, or globally serialized to hide failures.

Grid STA deadlines remain **30 seconds**, modal/chart deadlines **45 seconds**, and both child-process deadlines **two minutes**. Successful and failed grid actions shut down their dispatcher before completing. The native suite retains one process-owned renderer: repeated teardown was expensive, while creating a new unshutdown dispatcher for every case accumulated native resources. Reusing its STA avoids both costs. Final native cleanup belongs to the supervised child process. A timeout poisons the host, cancels queued actions and refuses further dispatch. Since managed cancellation cannot safely abort a native WPF call, the supervisor terminates the child process tree and awaits exit on deadline, cancellation, or output/logging failure. The child output is streamed to artifacts immediately; complete results are not held until process exit. A native-thread snapshot is not a managed stack. VSTest's same-deadline mini-dump collector provides best-effort stack evidence; the STA and dump watchdogs can race, so a dump is not guaranteed.

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

## Local verification of the run-69 correction (2026-10-04)

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

## CI evidence: run 70

[Run 70](https://github.com/yovko93/Personal-Trading-Journal/actions/runs/37155738271), validate job `111298629988`, was cancelled by the user after its Test step hung. The always-run artifact upload succeeded. Its complete log and ZIP were retrieved, not just the two failures shown in the question.

- PR head/local baseline: `8ec38859d0c9ccb87af2ce24989d5b808cb98f19`.
- Actual merge checkout: `9af1b467fe65da48469d548521075d5bbc8018a8`.
- Both trees: `07fc25a23cbb6d7f553742aa0f0699f0d9d04087`; the source content matches.
- Windows Server 2025, image `windows-2025-vs2026`, version `20260925.250.1`; SDK 10.0.303, runtime 10.0.12. Local SDK/runtime match, but the local Windows 10 desktop is not the hosted Server VM.
- Test ran from 21:38:26 until cancellation at 22:05:19 UTC on 2026-10-03. It did not complete naturally. The artifact contains only passing Domain **400**, Application **516**, and Infrastructure **719** TRX files; no Desktop TRX, child diagnostics or stack dump was available.
- **All 36** `CalendarViewLayoutTests` cases timed out, each with `System.TimeoutException: The operation has timed out.` at the outer `OnSta` await. No distinct inner assertion or WPF exception was reported. The final timeout at 21:56:32 was followed by approximately 8 minutes 46 seconds of silence before cancellation.

| Timed-out method group | Cases |
| --- | ---: |
| `CompiledGridKeepsSevenAlignedColumnsAndFocusableDaysAcrossThemesAndSizes` | 4 |
| `DaySelectionByKeyboardAndMouseRendersResponsiveDetailsAndExactViewTargets` | 4 |
| `OutcomeHoverRestoresBackgroundWithoutChangingSelectionMarkersOrTooltipTargets` | 4 |
| `MonthlyHeaderAndCenteredMarkersKeepTodaySelectionAndFocusDistinct` | 8 |
| `RefreshPreservesDateContainersAndAutomationAndMonthWheelRouting` | 4 |
| `FilterControlsUseSharedThemeKeyboardNamesBindingAndWrapAboveGrid` | 4 |
| `HitTestingScopesDateHoverToMarkerNotDailyWeeklyOrEmptyCellAreas` | 4 |
| `PopulatedGridRendersSignsAndCenteredWeeklyOnlySaturdayAcrossThemes` | 4 |

### Findings and correction

The original harness started a foreground STA and timed out only its awaiting task. It neither stopped that thread nor shut down its dispatcher. A blocked STA could survive each failed case and keep the process alive; this is a confirmed harness defect, not evidence of a Calendar product defect. The previous modal/chart child supervisor also disposed its process handle without terminating a timed-out child. Both now use the independently tested process-tree cleanup boundary described above.

Full-suite testing exposed another demonstrated lifetime problem: `TopstepDesktopRoutingTests.CompiledImportViewLoadsRealResourcesAndMaterializesTopstepReviewControls` creates and shuts down a process-wide Application. When it precedes the Dashboard's real-Popup placement tests, those tests can fail with a null `PresentationSource` / `Visual is not connected to a PresentationSource`. A forced-order isolated regression reproduced the null-source failure before the correction. The Import resource test now runs unchanged resource/UI assertions in its own supervised child, preserving its 30-second STA deadline; its Application shutdown no longer contaminates the parent. The regression then exercises both popup placement cases and requires no Application retained in their process. This proves the popup lifetime defect, **not** the original Calendar blocking call.

An initial cleanup implementation shut down every native test dispatcher. That introduced steady per-case initialization overhead, rather than an individual case hang: only 47 native cases completed before the original two-minute suite deadline. The 12 responsive-column actions averaged **2.312 seconds**; preserving the original process-owned native renderer lifetime reduced the comparison to **0.244 seconds**, and all **78** native cases passed within the unchanged deadline. Native windows still close in their test finally blocks; background STA, poison detection and process-tree cleanup remain enforced. A policy regression protects the intentional child-owned lifetime without relying on timing assertions.

The **original stalled WPF operation remains unproven**: the old log cannot distinguish construction, resource loading, layout or rendering. The representative grid theory now records start/end breadcrumbs for ViewModel construction, every navigation phase, view construction, DataContext assignment, pack resources, detached styles, Measure, Arrange, UpdateLayout, bitmap allocation, `RenderTargetBitmap.Render`, assertions and dispatcher cleanup. Month navigation uses a finite number of steps with progress assertions instead of an unbounded while loop; no navigation failure was observed. Logs also identify the STA, Application dispatcher lifetime, loaded runtime/WPF versions, logical CPU count, interactive status, window station and desktop. No test assertion or four-/five-/six-row coverage was removed.

Before isolation, all four representative cases passed locally at 96 DPI with two logical CPUs, after a controlled Application shutdown, with a deliberately nonpumping Application thread, and after the actual compiled Import-view routing test. They also passed on a unique hidden desktop in the existing interactive window station. A separate private noninteractive window-station probe was initially sandbox-denied (Win32 error 5); the approved out-of-sandbox test-only helper succeeded with `WSF_VISIBLE=False` and `UserInteractive=False`, and all four instrumented cases passed. Its bounded job/process/desktop/station handles were closed, without a desktop switch, permission or display-setting change. These negative probes do **not** rule out a CI-only lifecycle or rendering failure and do not recreate Windows Server 2025. The noninteractive probe used the corrected harness, whereas the hidden-desktop probe used the old dispatcher-cleanup behavior.

This correction contains the demonstrated lifetime failure and adds diagnostics for any recurring original stall. It must not be described as a proven fix of the original native blocking call or as a green GitHub CI result.

### Local follow-up verification (2026-10-04)

| Check | Result |
| --- | --- |
| Representative four-/five-/six-row grid theory, hidden desktop / 96 DPI / two logical CPUs | 4/4 passed; the original cleanup behavior was retained for this diagnostic probe. |
| Representative grid theory, private noninteractive window station | 4/4 passed; `UserInteractive=False`, `WSF_VISIBLE=False`. |
| All grid layout cases, three independent constrained runs | 36/36 passed on each run; no retries or skips. |
| Forced Import-Application shutdown then native Popup, before isolation | Failed at the existing null-PresentationSource assertion, reproducing the full-suite ordering problem. |
| Same forced order after Import resource isolation | Passed, including both existing real-Popup placement assertions. |
| Focused grid, harness/process supervision, Import resource and tooltip regressions | 65/65 passed. |
| Complete Desktop suite, 96 DPI / two logical CPUs / 1,044-DIP native window constraint | 883/883 passed; child grid 36/36 and native modal/chart 78/78. |
| Full Release suite, normal local 240-DPI host | 2,518/2,518 passed: Domain 400, Application 516, Infrastructure 719, Desktop 883; child grid 36/36 and modal/chart 78/78. |
| Solution Release build / `git diff --check` | Passed, zero build warnings/errors. |
| New GitHub run of this timeout/lifetime correction | Not run; requires the user's commit/push. The original blocking phase remains unverified. |

The 13 added cases cover cleanup, the blocked-phase deadline with a manually triggered TimeProvider (not a cold-start speed assumption), poisoned-host refusal, preserved action exceptions, native lifetime policy, forced Application ordering, child-tree cancellation/timeouts, ordinary exits, and diagnostic I/O failures. Ignored local evidence is under `bin/ci-timeouts/` and `bin/ci-investigation/`. No production code, stored economics, real journal, assertion weakening, retries or skips were involved.

## CI evidence: run 71 and deterministic synthetic probes

[Run 71](https://github.com/yovko93/Personal-Trading-Journal/actions/runs/37160098649), validate job `111311587429`, completed its Test step and uploaded `windows-test-results`. The complete job log and artifact ZIP were inspected.

- PR head/local baseline: `552d1f470494769c855268f4e7f23af37e965316`.
- Actual merge checkout: `3dfe6ace5f360d27ee9772570bf807261bbe30c2`; both trees are `cf1d29616ded7b301c0564f933eae706f7ef53bf`, so CI tested the same source content.
- Windows Server 2025, image `windows-2025-vs2026`, version `20260925.250.1`.
- Domain **400**, Application **516**, Infrastructure **719** passed; Desktop **882/883** passed. Child Calendar grid **36/36** and native modal/chart **78/78** passed, without the previous timeouts.
- The sole failure was `IsolatedTestProcessTests.NormalAndFailedExitPreserveCompleteOutput(exitCode: 7)`, an `IsolatedProcessTimeoutException` at its unchanged **15-second** deadline. PID 10264 started at 22:59:00.471 UTC, timed out at 22:59:15.469 and was confirmed exited at 22:59:15.486. Its stdout/stderr files each contained only the three-byte UTF-8 BOM, **no probe output**. The exit-code-0 PowerShell probe then passed but took **11.07 seconds** between start and exit.

The demonstrated problem is a runner-sensitive scripting-host dependency in a test meant to check output and exit handling, not a supervisor cleanup failure or Calendar failure. Even with `-NoProfile -NonInteractive`, the command must initialize Windows PowerShell before reaching its first console write. The timeout snapshot contains native wait states only; no PowerShell managed stack was collected. The evidence does **not** establish the particular internal startup wait, Defender activity, or CPU contention as the cause. No such speculation is treated as a finding.

`PersonalTradingJournal.TestProcessProbe` is now a minimal prebuilt `net10.0-windows` console executable with no WPF, production services or package dependencies. Its normal executable project reference deploys the apphost, assembly, dependency manifest and runtime configuration alongside Desktop.Tests, including custom artifacts-path builds. Tests launch that apphost directly, never a shell, `dotnet run`, or an on-demand compiler.

- Output mode emits deterministic UTF-8 stdout and stderr, each over 128 KiB, then exits with exactly 0 or 7. Assertions compare **complete** artifact content and the supervisor's exact bounded returned tails, including the final Unicode marker and process exit. This tests multiple pump reads and data buffered at exit rather than only one line.
- Blocked mode uses a real foreground STA and a real descendant of the same executable. Its readiness handshake precedes the flushed `FOREGROUND STA` marker, so deadline, cancellation and injected output-write-failure tests prove they reached the intended blocked phase. This also removes PowerShell startup and `Add-Type` compilation from those tests.
- A descendant parent-exit backstop is delayed **30 seconds**, longer than the tests' five-second descendant cleanup assertion. A separate parent-only-kill negative control verifies that it cannot falsely satisfy tree cleanup; its finally block explicitly terminates both synthetic processes. The delay only bounds a possible immediate-start/kill spawn race; it is not a retry or an extended supervisor deadline.
- The already-cancelled test uses the same probe but still starts no process. The first-log-write and output-pump fault tests retain the original error and cleanup assertions. The supervisor itself, Calendar deadlines and CI watchdogs are unchanged. No tests are skipped or retried.

The run-71 failure was not reproduced locally as a PowerShell internal hang. Local constrained checks exercise the replacement and its actual output/cleanup invariants; they do not recreate the hosted Server VM or prove GitHub is green. A new run after the user's commit/push must pass all suites and the revised synthetic probes. The original run-70 inner blocking operation remains unidentified, although run 71's complete Calendar child results show it did not recur there.

### Separate local composite-host budget finding

The first complete Release verification of the probe replacement found one **different** failure: the Application-lifetime regression's outer process reached its 30-second budget during VSTest completion, although its child TRX already reported **Passed**. The regression action took **25.892 seconds**, including a nested Import process taking **21.976 seconds**; that Import's STA action and dispatcher cleanup completed in **3.264 seconds**. Including outer discovery, startup and exit, the parent confirmed termination after **32.53 seconds**. Both existing real-Popup assertions had passed. This is direct evidence of a composite process-budget mismatch, not a blocked Calendar or failed assertion, and not the run-71 PowerShell failure.

Only `ImportResourceApplicationCannotPoisonLaterNativeTooltipWindows` now uses the existing **two-minute suite/process budget**, because it supervises another test host and then two native Popup checks. The other synthetic STA processes retain 30 seconds, the Import action retains its 30-second STA deadline, and the console output/blocked probes retain 15/20 seconds. The nested Import isolation and its unchanged resource assertions are preserved; bypassing that isolation would recreate process-wide Application poisoning. No blanket timeout increase or retry was applied. The first failed full-suite TRX is retained alongside subsequent verification evidence rather than discarded.

### Local verification of the run-71 correction (2026-10-04)

| Check | Result |
| --- | --- |
| Final eight process probes plus composite Import/Popup regression, single-CPU process affinity, two reported .NET CPUs, process-local 96 DPI | **9/9** on each of three independent runs; stop-on-failure, no retries/skips. |
| Exit-code-0/7 output theory, ten additional independent single-CPU runs | **20/20** passed; case durations **0.247–1.289 seconds**. Complete stdout/stderr and exact bounded tails matched. |
| Complete Desktop suite after both corrections, two .NET CPUs / 96 DPI / 1,044-DIP native-window constraint | **884/884** passed; child grid **36/36**, modal/chart **78/78**. No synthetic probe processes remained. |
| Final full Release suite, normal local host and ordinary solution output | Domain **400/400**, Application **516/516**, Desktop **884/884** passed; Infrastructure **718/719**. **2,518 passed, one failed**; this is not a green full-suite result. |
| Separate failing Infrastructure case, unchanged isolated diagnostic execution | **1/1 passed**; not a replacement for the failed full-suite result or proof of a fix. |
| Solution Release builds, ordinary and custom artifacts paths | Passed, zero warnings/errors; probe apphost/dependency/runtime files deployed in both outputs. |
| `git diff --check` | Passed. |
| New GitHub CI run | **Pending the user's commit/push.** Local results do not establish a green CI run. |

The final full-suite exception was `ObjectDisposedException: SQLitePCL.sqlite3` in the unchanged Infrastructure test `TradeBrowseProjectionTests.SaveAsyncRegeneratesProjectionForCloseAndCorrectionOnlyForTargetTrade`, while opening a connection in `TradeMutationStore.GetByIdAsync`. Infrastructure passed all 719 cases in run 71 and the earlier local full run. The isolated case subsequently passed unchanged. Existing parallel Infrastructure fixtures use process-wide `ClearAllPools()` cleanup with default pooled connections; this is a plausible independent lifetime hazard, **not a proven cause**. No Infrastructure or production code was changed, and this transient failure is not claimed fixed. Its full-suite TRX and the isolated diagnostic result remain separate under ignored `bin/ci-probe-investigation/` for follow-up. No customer data or real journal was accessed.

The subsequent [SQLite lifetime investigation](sqlite-test-lifetimes.md) demonstrates the cross-fixture pool ownership defect and a controlled provider activation race, and replaces global cleanup with owned-pool release. Its verification is recorded separately from the historical run-71/probe results above.

## CI evidence: run 73 — ambient pointer and probe encoding

[Run 73](https://github.com/yovko93/Personal-Trading-Journal/actions/runs/37196768021), validate job `111420178931`, completed the Test step with failure and successfully uploaded its diagnostic ZIP. The full log and TRX files were inspected, not just the propagated Desktop messages.

- PR #16 head/local baseline: `b9554eaf0e326906a8e0d7be5acb6b7e64b0bf6f`, `develop`, initially clean.
- CI merge checkout: `30c3c35cdb3acfa88b94a5e4bcdb4aee78fe6cbb`. Both share tree `291f89dfa8e8ff31f173af87e09530575318c8b4`; source content matches.
- Windows Server 2025, image `windows-2025-vs2026` version `20260925.250.1`, SDK 10.0.303 / runtime 10.0.12.
- Domain **400/400**, Application **516/516**, Infrastructure **724/724**, child Calendar grid **36/36** passed. No new SQLite failure exists and that fix is unchanged.
- Native Calendar suite: **77 passed / 1 failed**. Its first and only assertion failure is `CalendarDayPerformanceChartTests.FullHeightRegionsHandOffOneGuideAndRetainKeyboardFocusWithSubtleThemeDrawing("Dark", 960, 96)`, original line **68**: expected `Collapsed`, actual `Visible`.
- Parent Desktop: **805 passed / 79 failed**. **77** failures repeat the same native-suite result. The other **two** are independent output-probe encoding assertions (exit codes 0 and 7), not Calendar failures.

### Guide initial-state assumption

The failing test creates a real native Window, calls `Show`, pumps the dispatcher, and draws the chart before asserting there is no active guide. The chart correctly handles native region `MouseEnter` immediately. Its instance-owned hovered/focused target determines guide visibility; redraw preserves an active point by identity. A fresh chart cannot inherit another chart's target. Thus the test incorrectly assumes no ambient pointer interaction while it is showing/pumping the window.

Temporary diagnostic instrumentation reproduced the exact assertion using native `WM_MOUSEMOVE` input delivered to the test window at a measured plot coordinate, before the initial assertion. All four theme/size cases failed the original `Collapsed` assertion. The Dark 960/96 case recorded:

```text
REGION MouseEnter: guide=Visible; focus=System.Windows.Window
guide=Visible; mouseOver=True; pointer=480,100
regions=0:False,1:False,2:True,3:False
```

This is legitimate hover, not retained stale state or automatic keyboard focus. The CI artifact did not record its physical cursor coordinates, so the precise original cursor location is not claimed observed. On the local hidden sandbox desktop, passive window positioning alone did not reproduce entry; controlled native input did. Windows/WPF can ignore synthetic native movement when the real cursor is outside its HWND; diagnostic positioning placed only the synthetic test window beneath the stationary cursor, without moving the machine's cursor. This reproduces the environmental-input mechanism, not the entire hosted VM.

The correction sets `IsHitTestVisible=false` on **this scripted test's Window only**, before showing it, and explicitly focuses the button outside the chart. It asserts no hover/focus before the unchanged initial guide check. Isolation lasts through all later dispatcher pumps, not just a one-time state reset. Every existing region-geometry, adjacent-boundary, guide position, highlight, delayed-leave, zero-point, tooltip, resize, real keyboard-focus and clearing assertion remains. Raw `VisualTreeHelper.HitTest` still tests region geometry; routed events explicitly drive the interaction under test. No product event handler, state behavior, assertion, case or deadline was removed or weakened.

Temporary native-message diagnostics are not retained as ordinary CI tests: adding a native-pointer-location assumption to them would recreate the problem. Their logs/TRX remain under ignored `bin/ci-73-investigation/`. The permanent interaction coverage remains the original four deterministic theory cases, not live pointer acceptance.

### Independent UTF-8 probe failure

Both `IsolatedTestProcessTests.NormalAndFailedExitPreserveCompleteOutput` cases failed at line **20**, comparing the complete stdout artifact: expected `complete stdout Ω`, actual `complete stdout ╬⌐`. The stderr artifacts have the same corruption. The processes exited normally with the correct codes in approximately **55 ms** (7) and **63 ms** (0); this is not the earlier PowerShell startup timeout.

The console probe emits UTF-8 but its `ProcessStartInfo` had unspecified stdout/stderr encodings. UTF-8 bytes `CE A9` for `Ω` decoded as OEM437 produce the exact `╬⌐` corruption. A separate diagnostic with explicit decoders reproduced it on both streams at exit codes 0 and 7; UTF-8 decoding preserved the complete marker and exit codes. It changed no global console settings.

Only the test's probe factory now declares both UTF-8 decoders. The existing theory asserts those declarations before its unchanged exact full-output, bounded-tail, exit and cleanup checks. The direct-launch parent-only-kill negative control also redirects stderr, as required for an explicit stderr decoder. The generic process supervisor still honors each caller's chosen encoding. No production code, SQLite fixtures, skipped tests, retries, sleeps, global serialization or blanket deadline changes are involved.

### Local verification

| Check (2026-10-04) | Result |
| --- | --- |
| Controlled native entry before correction | **4/4 reproduced** the original initial-guide assertion, with MouseEnter and Window-only keyboard focus recorded. This is diagnostic reproduction, not a passing acceptance run. |
| Corrected four chart theory cases plus eight process probes, hidden sandbox desktop / process-local 96 DPI / two .NET CPUs | **12/12 passed**. Chart bitmap rendering covers 96/240 DPI; this does not claim a native 240-DPI window in the constrained run. |
| Complete isolated Calendar native suite, same constrained host plus 1,044-DIP native-window limit | **78/78 passed**, no skips. |
| Full Release suite, ordinary local host | **2,524/2,524 passed**: Domain **400**, Application **516**, Infrastructure **724**, Desktop **884**. Child grid **36/36** and native Calendar **78/78** passed. |
| Solution Release build / `git diff --check` | Passed; **zero build warnings/errors**. No synthetic probe processes remained after the suite. |
| Live application pointer interaction | Not performed. WPF interaction/render checks above are automated and use synthetic data only. |
| New GitHub run | **Pending the user's commit/push.** Local passing results are not a green CI run. |

The downloaded run-73 evidence, before-correction diagnostic TRX, encoding probe output and final local TRX/logs are under ignored `bin/ci-73-investigation/`. No customer data or real journal was accessed. Only two test files, README and this investigation document changed; production code and the SQLite ownership correction are untouched.

## CI evidence: run 74 — native tooltip readiness

[Run 74](https://github.com/yovko93/Personal-Trading-Journal/actions/runs/37202250659), validate job `111436164468`, completed its Test step and uploaded the complete TRX/diagnostic artifact. PR head/local baseline was `036957ac7c4fc7306a0f7cbadcbf298321af231b`, `develop`, with a clean worktree. The CI merge checkout `6b401dfaf1f2831c029096f0e3d1e1d4ae7ac771` has the same tree, `c257c0161b2fc3757d81f11268a6c9e4450a0d8a`.

Domain **400/400**, Application **516/516**, Infrastructure **724/724**, Calendar grid **36/36**, and native Calendar **78/78** passed. Desktop was **883 passed / 1 failed**. The sole failure was `ChartTooltipFollowerTests.OpenPopupFlipsBesidePointerNearWindowRightAndBottomEdges`: `AssertPopupNearExpected`, original line **285**, threw `InvalidOperationException: This Visual is not connected to a PresentationSource` at `tip.PointToScreen`. The immediately preceding `root.PointToScreen` succeeded. The run-73 chart/probe corrections passed; neither those fixes nor SQLite are changed here.

### Lifecycle evidence and limits

The original test called `tip.IsOpen = true`, drained the dispatcher to `ApplicationIdle`, then measured. The scrolling placement test used the same idle drain. Neither sequence guaranteed the tooltip remained open at measurement. Production `ChartTooltipFollower` sets offsets and follows pointer/focus; it does not close a hover popup. The [WPF Popup implementation](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/Primitives/Popup.cs) queues app-deactivation handling for `WM_ACTIVATEAPP(false)`; [ToolTip's popup-close handler](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/ToolTip.cs) sets `IsOpen=false`. Other concurrent test processes create native windows, so draining unrelated native events is not an inert delay.

A local baseline trace at process-local 96 DPI opened synchronously: `Opened=1`, `Closed=0`, `IsOpen=True`, `Source=HwndSource`, 100×80 DIPs, target `Window`, owner `MouseOver=False`. The unmodified placement assertion passed. Controlled native app-deactivation input to that **synthetic tooltip HWND**, followed by event-coordinated completion of its close, reproduced the exact disconnected-visual exception:

```text
Opened: IsOpen=True; source=HwndSource; opened=1; closed=0
Closed: IsOpen=False; source=null; opened=1; closed=1
before measurement: IsOpen=False; source=null; rootSource=HwndSource
target=Window; window=Normal/True/True; mouseOver=False; size=100x80
```

An earlier probe also caught the closing-animation interval: `IsOpen=False` while the source was still attached. Thus a non-null source alone is not sufficient. Geometry was unchanged and the root remained connected; this reproduced closure, not a coordinate conversion failure, slow opening, or pointer hover. It did not switch applications or move the user's pointer.

**The original CI artifact did not record Opened/Closed, IsOpen or activation state.** This controlled reproduction demonstrates the test's lifetime assumption and the same failure mechanism, not proof of the precise original hosted-runner event sequence. No product tooltip defect was demonstrated. Future failures now carry the missing evidence rather than only a disconnected-visual stack.

### Test-only correction

`TooltipPopupReadiness` subscribes before opening to Opened/Closed, IsOpen changes, presentation-source changes, load/layout, window activation/closure and owner hover. It waits for an observed Opened event, `IsOpen=true`, a connected popup, and valid positive layout. The existing physical-pixel assertions then execute in the same STA operation without a generic idle drain in between. The follower's synchronous offset changes are measured the same way after pointer movement and horizontal scrolling.

Deferred readiness is coordinated by dispatcher/events with one **five-second deadline**, not sleeps or polling. A closed/closing popup fails explicitly; a never-opened popup times out with state; neither is reopened or retried. Handler, queued-check and timer cleanup is scoped. Existing five-physical-pixel X/Y tolerances, edge calculations, test cases, process/CI deadlines and production code are unchanged.

These native placement windows also exclude ambient pointer hit testing: their pointer coordinates are scripted, while their HWNDs, popup HWNDs, screen coordinates and DPI conversion remain real. This prevents an unsolicited tooltip-service opening from defeating the deliberately deferred-opening regression. It does not suppress native app-deactivation messages, as the regression still verifies. No production hit targets change.

The observer preserves a bounded lifecycle history and reports IsOpen, presentation sources, Opened/Closed counts, placement target/offsets, measured dimensions, window bounds/state/activation, pointer hover and DPI/work area. Measurements and failures write stdout/TRX and optional `chart-popup-readiness.log`. No customer content is logged.

Three regressions exercise deferred opening beyond a single idle pump; measurement before an unrelated queued native deactivation followed by diagnostic rejection of the closing popup; and a never-opened popup's immediate test deadline without measuring/retrying. Controlled deactivation is confined to the test HWND. The zero-duration test deadline exercises failure cleanup without waiting five seconds; normal placement checks use the bounded default.

### Local verification

| Local check (2026-10-04) | Result |
| --- | --- |
| Instrumented original edge test, hidden desktop / process-local 96 DPI / two .NET CPUs | Passed; Opened and source were ready synchronously, no hover or closure recorded before measurement. |
| Controlled native deactivation then Closed, before correction | Reproduced the exact disconnected-visual exception with the root still connected. This is negative diagnostic evidence, not a passing run. |
| Final tooltip suite, three independent constrained runs | **18/18 passed on each run**, stopping on failure rather than retrying. Includes the original edge/scrolling checks and three readiness regressions. |
| Final complete Desktop suite, 96 DPI / two .NET CPUs / 1,044-DIP Calendar window constraint | **887/887 passed**, child grid **36/36**, native Calendar **78/78**. |
| Final full Release suite, ordinary local host | **2,527/2,527 passed**: Domain **400**, Application **516**, Infrastructure **724**, Desktop **887**; child grid **36/36**, native Calendar **78/78**. Popup diagnostics confirm native **240 DPI** (`TransformToDevice=2.5`) in this run, versus **96 DPI** in the constrained runs. |
| Release solution build / `git diff --check` | Passed; zero build warnings/errors. |
| New GitHub CI run / live application acceptance | **Not performed.** The user must commit/push and inspect a new run; automated native WPF checks are not live application verification. |

Evidence is retained under ignored `bin/ci-74-investigation/`; the real journal was not accessed. README, this document, the tooltip test file and its new test-only readiness helper are the only changes. A new GitHub Actions run after the user's commit/push is still required; local results do not establish green CI.

## CI evidence: run 75 — modal focus and native activation

[Run 75](https://github.com/yovko93/Personal-Trading-Journal/actions/runs/37205500198), validate job `111445728411`, completed and uploaded its diagnostic artifact. Local/PR head was `214b1a50b2c0ba9e7ddd7f1565e2c6a503eda876`, branch `develop`, initially clean. CI checked out merge `4c85c3468b3af44bb18133c4734c040d89cddf79`; both trees are `7e12a7a42fcdcade330747ff8cb79da7247b2424`.

Domain **400/400**, Application **516/516**, Infrastructure **724/724** and isolated grid **36/36** passed. Native Calendar was **77 passed / 1 failed**. Parent Desktop was **810 passed / 77 propagated failures**. The first real failure was `CalendarDayModalTests.InitialOwnedPanelFitsTableAtDesktopWidthAndWrapsWithoutLosingAction("Light", 1280, 240, true)`, `CalendarDayModalWindowTests.cs`, original line **133**: `Assert.Null(view.DayDialog)` passed, then `Assert.True(cell.IsKeyboardFocused)` failed. All preceding table, wrapping, inline View and dismissal assertions passed. Tooltip-readiness checks from run 74 passed.

### Focus evidence

`CalendarView.SelectDay` calls the selected cell's `Focus()` in its `ShowDialog` finally block. The failing layout test then pumps arbitrary dispatcher/native events and assumes the owner still has keyboard activation. The window-controls theory already requested owner activation before making the same focus assertion. [WPF distinguishes logical and keyboard focus](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/focus-overview): a deactivated window can retain the correct logical target while another window receives keyboard input.

The native TRX and phase logs prove concurrent chart windows on the same desktop:

| Case | UTC interval in CI |
| --- | --- |
| Failing modal Light/1280/240/long | 13:27:00.3949189–13:27:01.5896382; STA23 action ended 13:27:01.5604334 |
| Chart Dark/960/96 | 13:26:58.6827122–13:27:00.8031664 |
| Chart Dark/480/240 | 13:27:00.8060196–13:27:01.3279132 |
| Chart Light/960/96 | 13:27:01.3339622–13:27:01.9369097; STA38 action spans the modal failure |

These are separate STAs in the same child, `WinSta0/Default`; the chart cases show real windows and use keyboard focus. Original CI logs did not record HWND activation, so the exact competing activation event is not claimed observed.

Temporary instrumentation of the **existing production Focus call**, plus a controlled competing test window during the post-close pump, reproduced the exact failed assertion in the same Light/1280/240/long case:

```text
production Focus() returned True
ShowDialog returned: owner active=True; dialog active=False, visible=False
keyboard=selected CalendarDayHost; owner logical focus=selected CalendarDayHost
competing window activated: owner active=False; keyboard=Button
after Pump: owner logical focus=selected CalendarDayHost; cell.IsKeyboardFocused=False
cell visible=True, focusable=True, enabled=True throughout
```

The original unperturbed 12-case theory passed with the instrumentation. This establishes correct restoration followed by legitimate loss of activation, not a replaced cell, incorrect target, failed modal close or layout defect. The temporary production logging was removed; `CalendarView` and all production code are unchanged. The controlled reproduction complements the proven CI overlap but cannot retroactively supply the original runner's missing focus trace.

### Test-only correction

`CalendarFocusProbe` first requires the selected cell to be the owner's **existing logical focused element**, still connected, visible, enabled and focusable. Only then does it issue one [SetActiveWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setactivewindow) request for that STA's test-owned HWND, the same API WPF uses in modal close. Observable owner activation, native active HWND, `Keyboard.FocusedElement`, and `cell.IsKeyboardFocused` must agree. Activation/focus events coordinate a bounded five-second check; it never calls `cell.Focus()`, resets logical focus, retries activation or sleeps. Existing raw keyboard-focus assertions remain, with no pump between the gate and assertion. All table/layout, dismissal and edit-protection assertions remain unchanged.

A first helper draft required `Window.Activate()` to return true. On the hidden sandbox desktop it returned **False with owner already active, exact cell keyboard focus and native foreground HWND=0**. That was another invalid foreground-permission assumption, not failed restoration. The final helper uses thread-local activation and checks observed state, rather than requiring a global foreground request to succeed. It does not force the real application to steal focus when a user switches away, and does not serialize test collections or change 45-second STA/two-minute suite deadlines.

The same gate covers the modal close/focus checks for table layout, backdrop/X/Escape, close/empty-day and window-control states. Two regressions verify that a competing window can take keyboard focus while the selected date retains logical focus, and that the gate **rejects a wrong retained target without repairing it**. Diagnostic history includes owner/dialog activation and visibility, logical/keyboard object identities, cell eligibility, HWND foreground/active/focus state and the activation request result in stdout/TRX plus `calendar-focus.log`. No Account names or Trade identifiers are logged.

### Local verification

The corrected tests passed on 2026-10-04:

| Verification | Result |
| --- | --- |
| Reported layout theory (12 cases) plus competing-window and wrong-target regressions; hidden Windows desktop, `DPIUNAWARE`, two .NET CPUs, 1,044-DIP owner cap | **14/14 passed** |
| Complete isolated Calendar-native suite under the same constraints | **80/80 passed**, 62 seconds |
| Full Release solution suite, normal local display context | Domain **400**, Application **516**, Infrastructure **724**, Desktop **889**: **2,529 passed**, no failures or skips |
| Native child suites during that full run | Grid **36/36**, modal/chart **80/80** |
| Release solution build | Passed, **0 warnings / 0 errors** |
| `git diff --check` | Passed |

The original failure was deliberately reproduced before correction, not retried until passing. The corrected focused cases then also passed inside both complete native runs. A new GitHub run after the user's commit/push remains required. These are automated native-WPF tests, not live application acceptance; no real journal or customer data is used. Diagnostic TRX/logs are retained under ignored `bin/ci-75-investigation/`.

## M14.5 local full-suite investigation (2026-10-05)

The follow-up started on `develop` at `e903e32e3be632d273b8e12816b087ada4d60be9` with a clean worktree: the prior M14.5 implementation had already been committed by the user. The earlier reported `c33b8b4` was not the checkout under test. This is a **local full-suite investigation**, not a downloaded GitHub run. No application, persistence, schema, import or economics code changed.

### Journal navigation: readiness evidence, not an inferred product failure

The historical controlled full run timed out in `LeavingJournalCancelsReadAndReturningIgnoresItsLateResult` while waiting five seconds for its fake repository to start. That wait precedes navigation away and token cancellation. The shell activates Journal synchronously and starts its load with `Task.Run`; the fake Account reader must finish before repository invocation. No WPF dispatcher callback is needed along that path. The old test observed only the repository signal: an earlier completed/faulted load was indistinguishable from a scheduling delay until its signal deadline.

Instrumentation now records elapsed time, managed thread/context/scheduler, ThreadPool state, Account/repository entry, read tokens, pending/completed tasks and navigation state, without journal content. The normal parallel reproduction of the native failure did **not** reproduce the Journal timeout: repository entry was about **1.7 ms**, and the waiter resumed in **3.8 ms**. A later normal parallel run reached repository entry within **1.6/8.5 ms** in its two late-response cases. Therefore the exact original Journal delay remains unproven; native resource pressure is not asserted to be its established cause, and no production read/cancellation behavior was changed.

The test keeps its **five-second bound**, but races readiness with completion of the actual load. If the load exits without calling the repository, it fails immediately with that state rather than waiting for a signal which can no longer arrive. Cleanup releases and drains the pending fake load even after an assertion failure. Regressions cover a late old result **and** a late old exception after navigation away/return, with a nonempty current completed revision and all review answers; 16 concurrent independent navigation flows; cancellation at the Account-reader boundary; and a completed-before-repository failure. They continue to assert the old token is cancelled and that neither obsolete outcome replaces current content, answers, state, revision or error state.

### Native suite: retained per-case renderers exceeded the aggregate budget

The instrumented normal parallel solution run reproduced **85 propagated Desktop failures** from the Calendar native child's unchanged **two-minute** process deadline. Domain, Application and Infrastructure passed. It was not a 45-second individual STA timeout: **79 actions started and 78 finished**, across **79 different managed STA threads**. At termination, phase diagnostics recorded roughly **90.8 seconds process CPU**, **93 native threads**, a **2,134.7 MiB working set / 2,036.7 MiB private bytes**, and about **219 MB managed memory**. ThreadPool pending work was zero with workers available. Earlier cases completed and resource usage grew across their lifetimes; the final action was cut off by the aggregate deadline. The supervisor terminated and awaited the child process tree.

Native cases previously created a fresh STA/dispatcher per case but intentionally did not shut it down, avoiding costly renderer teardown. Ending those threads retained native WPF resources until process exit. The fix uses **one process-owned background STA with a running dispatcher**, reusing its renderer. A gate acquired outside the dispatcher prevents nested modal frames from executing another test action reentrantly. Only this native host's actions are ordered; unrelated collections and test assemblies retain their normal concurrency. Grid tests keep their separate per-case dispatcher cleanup. No deadline, assertion, retry or skip policy was relaxed.

Before the final verification, the corrected native suite passed **85/85** in **89.1 seconds** in a focused run and **106.9 seconds** in a normal parallel solution run. Each used one STA; peak working sets were **701.0 / 691.5 MiB**, private bytes **603.3 / 591.2 MiB**, and native thread counts **42 / 43**. These are process-boundary samples, not a universal performance guarantee. The solution run's only failure was a newly added negative harness probe, described below; it was not counted as full-suite acceptance.

Lifecycle regressions verify dispatcher reuse, exclusion through nested message frames, ordinary action exception/context recovery, and timeout poisoning that cancels queued/later actions without allowing them to run after the blocked action is released. Existing background-thread and process-tree cleanup checks remain. Lifetime-boundary diagnostics now include CPU, native threads, managed/native memory and ThreadPool counters alongside the existing named operation phases.

### Escaping native callback diagnostics

Review of the shared host found that an exception escaping its pump after startup could be lost by attempting to fault an already-completed readiness task. A negative child-process probe then exposed an additional WPF boundary: without an `Application` or dispatcher `UnhandledException` handler, the exception filter reported `RequestCatch=False`. Its queued callback entered, yet remained `Executing`, its task `WaitingForActivation`, and the dispatcher thread alive when the probe's five-second bound expired. Merely rethrowing outside `Dispatcher.Run` did not report that callback's error because the native callback boundary did not unwind the pump.

The test host now captures the original unhandled exception, marks it handled **only to unwind the dispatcher**, requests shutdown, and rethrows the captured exception outside the native callback boundary. A throw-only probe recorded the original error after pump exit, but the test host still remained alive until the probe expired. The post-startup fatal path therefore calls `Environment.FailFast` with that original exception: this is confined to the supervised test child and ensures a dead dispatcher cannot masquerade as a later timeout. The corrected negative probe terminated non-timeout in **2.94 seconds**, with `Test host process crashed`, the original exception/callback stack and confirmed process cleanup. Startup failures still reach the waiting case; unexpected normal pump exit also fails explicitly. Ordinary action exceptions still return directly to their owning test. Production WPF exception behavior is unchanged.

Evidence is retained under ignored `artifacts/m14-5-failures/` (instrumented reproduction, focused/native traces and subsequent full runs). All data is synthetic. No real journal was accessed. Automated WPF tests do not establish live UI acceptance, and a new GitHub run after the user's commit/push remains necessary.

### Final local verification

| Check (2026-10-05) | Result |
| --- | --- |
| Focused Journal/navigation, process supervision, native harness, modal/chart and grid tests; two .NET-reported CPUs, process-local `DPIUNAWARE`, 1,044-DIP native-window cap on the hidden sandbox desktop | **280/280 passed**, no skips, including the 16 concurrent Journal flows and late-success/late-error cases. These process settings do not change system display scaling or physically pin CPU affinity. |
| Child suites in that constrained run | **85/85 native**, one shared STA, about **82.4 seconds**, peak sampled working set **364.9 MiB**; **36/36 grid**. Existing deadlines unchanged. |
| Complete Release solution suite, fresh normal process settings and normal assembly concurrency | **2,810/2,810 passed**, no failures/skips: Domain **444**, Application **516**, Infrastructure **775**, Desktop **1,075**. No `-m:1` or global xUnit serialization. |
| Native/grid children in that full run | **85/85 native**, **36/36 grid**. Native: one STA, **118.6 seconds**, peak **686.5 MiB working set / 592.4 MiB private bytes**, **43 native threads**. This passed the unchanged 120-second bound but leaves little margin; hosted CI performance is still unverified. |
| Journal race evidence in the final full run | Both late-response cases and 16 concurrent flows passed. First repository entries at **8.3/18.0 ms**, observed by the test at **13.1/50.7 ms**; neither old success nor old error replaced the fresh completed revision. |
| Release build / EF model consistency / `git diff --check` | Passed; **zero build warnings/errors**, no pending model changes. EF uses the existing in-memory design-time factory. |
| GitHub Actions / live WPF application verification | **Not performed.** A new run after the user's commit/push and the existing isolated interactive checklist remain required. No claim of green CI or live M14.5 acceptance. |

## M14.5 CI check and native timing follow-up (2026-10-05)

The checkout and remote `develop` both pointed to **`ba91aeba12d5e6834297f24cc15bdd3e4bbc61d0`**, with a clean local worktree. The user had committed the prior eight-file dispatcher/Journal correction. GitHub's repository-wide Actions API returned **no run for this exact SHA**, including non-PR events; the narrower commit/PR endpoint also returned none. The latest run was [CI #77](https://github.com/yovko93/Personal-Trading-Journal/actions/runs/37210548636), successful at **`9fbbc9220f742ceda1debfdd93715d55a7a36667`**, an earlier M13 merge to `main`. Its downloaded job `111460673793` checked out that SHA and passed Domain 400, Application 516, Infrastructure 724 and Desktop 889—not the M14.5 suites. The latest `develop` PR run was #76 at `f724b49f2a14943e29aa063acad2efd07a428cfb`, also before this fix. Neither verifies current code.

The workflow triggers on pushes to `main` and pull requests targeting `main`, not standalone `develop` pushes. Thus the dispatcher fix **is pushed**, but still needs a matching PR-triggered run. No workflow, branch, commit, PR or run was modified. The GitHub CLI was unavailable and direct shell network access was blocked; the read-only GitHub connector supplied the run/ref/job evidence.

### Measured work before the small diagnostic-path correction

`PTJ_CALENDAR_TIMINGS=1` enables test-only stopwatch scopes. They append operation/countable-duration JSON records to `calendar-timings.jsonl` in the supervised child's existing diagnostic directory and to captured output. It is off by default; it changes no dispatcher priority, rendering mode, deadline, fixture data or assertion. Existing phase timestamps continue to identify exclusive action duration and queueing; per-case elapsed/TRX durations must not be added as wall time because queued cases overlap.

The instrumented native suite passed **86/86 cases (85 STA actions) in 75.738 seconds**, versus the previous full-load run's 118.6 seconds. A reporting correction: the earlier local sections/report counted 85 STA actions as native test cases; the retained native TRX also includes one pure-calculation case and records **86**, with no added or removed case in this follow-up. These runs have different competing workloads; that difference is **not an optimization result**. The before-change measurement separates:

| Work | Observed time / sample |
| --- | --- |
| Process startup, discovery and preparation before first STA action | 2.949 s from supervisor start to first STA-start breadcrumb |
| Exclusive native test actions | 70.133 s across 85 actions on one STA |
| Remaining in-process orchestration, between-action work and diagnostics | About 2.261 s, excluding startup/actions/exit; not a hidden fixed wait |
| Final action completion to supervised process exit | 0.395 s |
| Fixture creation/activation | 0.342 s across 61 calls; outside or nested within actions, not additive to the wall-time partition |
| Fresh theme resource loading | 5.600 s across 85 calls; mutable WPF dictionaries remain independent, not shared across tests |
| Modal dispatcher pumps | 6.566 s across 257 calls; includes queued WPF work, not deliberate sleeping |
| Chart layout / bitmap rendering / chart pumps | 2.367 s / 0.202 s / 0.925 s across 21 / 21 / 16 calls |
| Initial-owned-modal theory, 12 cases: owner Show/layout | 5.778 s |
| Same theory: modal opening, interaction and return | 8.955 s, **including** nested day-read/layout 0.580 s, inline View/layout 0.878 s and dialog Close 2.103 s |
| Same theory: owner Close | 1.202 s |
| Same theory: obsolete panel-width comparison | **0.612 s** across 12 calls, nested inside modal interaction |

Scopes are inclusive where nested; the category rows are **not** a second additive wall-time partition. This sample shows real layout/window lifecycle work rather than a stuck application phase or a large synthetic wait. No new Journal-start failure occurred and its original cause remains unproven.

### Minimal correction and preserved coverage

`InitialOwnedPanelFitsTableAtDesktopWidthAndWrapsWithoutLosingAction` previously set `DayPanel.MaxWidth` to the obsolete 1,000-DIP cap, pumped/relaid out, restored the real cap, then pumped/relaid out again in **every** case. The resulting before-width numbers were used only in an optional screenshot report. It also scrolled to the table solely for a `Render` helper which immediately returned when capture was disabled.

These historical comparison layouts and screenshot-only scrolling now run only when `PTJ_CALENDAR_RENDER_DIRECTORY` is set. The cap is restored in `finally` in that diagnostic branch. All initial viewport, horizontal access, Account/Net widths, header alignment, inline View, close, focus and other assertions remain unconditional and unchanged. Ordinary acceptance now measures the actual initial table without first forcing an old-size/re-expand cycle. Required scrolling used by assertions is retained. No rendering coverage, theory case, activation/focus gate or edit-protection assertion was removed. No new regression test is needed for an optional diagnostic-only branch; both normal and capture-enabled existing theory paths are verified below.

Only the measured 0.612-second comparison and its extra screenshot preparation are identified as avoidable work. This is a small reduction, not an explanation of all variation in suite time. Fresh resource loading, actual window/modal lifecycle, layout, bitmap rendering and dispatcher pumping needed by interaction assertions remain. Shared-STA ownership, external gate against nested-frame reentry, fatal-error reporting, timeout poisoning, 30-/45-second case deadlines and the **120-second aggregate deadline** are unchanged. No production, schema, economics or M14.6 work is included.

Evidence is retained in ignored `artifacts/m14-5-performance/`. A matching GitHub run and live UI acceptance remain separate from these automated local checks.

### Full-load reproduction and redundant probe startup

The first complete parallel Release run after that small correction **failed**: Domain 444, Application 516 and Infrastructure 775 passed; Desktop passed 990 and propagated **85 failures** from one native aggregate timeout (2,725 passed / 85 failed overall). The native child started at `16:55:38.626Z`, reached its first STA at `16:55:51.783Z` (**13.157 s startup**, versus 2.949 s isolated), and was terminated at the unchanged 120-second boundary. It had finished **77** actions and started its 78th, `JournalActionClosesProtectedModalBeforeNavigatingAndVetoKeepsTradeDraft`, less than half a second before termination. There was no 45-second action timeout. Journal navigation regressions passed. This failure is retained in `full-release/`; it is not replaced with a claim that the screenshot-path change solved contention.

The trace also exposed six separate VSTest children for healthy harness probes. Their aggregate process lifetimes were **52.50 s**, versus **1.536 s** of measured STA action lifetimes (including their minor coordination overhead):

| Healthy probe | Child lifetime |
| --- | ---: |
| Successful action / dispatcher shutdown | 13.12 s |
| Assertion failure / cleanup | 5.54 s |
| Action-thrown TimeoutException classification | 10.06 s |
| Native dispatcher reuse | 10.36 s |
| Nested-frame reentry exclusion | 6.33 s |
| Native action exception / context recovery | 7.10 s |

These repeated runtime/discovery lifetimes overlap the native suite; their sum is not wall time saved from that suite. The native renderer retained a bounded working set, and actions continued progressing. This demonstrates avoidable probe-process startup load, not a new product hang.

The six non-destructive probes now share one `calendar-sta-healthy` supervised child through a single lazy result. They still execute as six separately reported xUnit cases with unchanged assertions; the parent cases consume the same child result, as the existing native/grid gates already do. The **entire group keeps a 30-second process deadline**, not six accumulated deadlines, and per-action bounds remain unchanged. The explicit allowlist excludes both poisoning probes, the fatal callback probe and the Application-lifetime composite: each still gets its own child because it intentionally changes terminal process-global state. The real native suite remains separately isolated with its **120-second** deadline. Unrelated tests/assemblies retain normal concurrency; no global serialization, retry, sleep, skip or production change is introduced. Existing reuse, nested-frame, exception/context recovery, poisoning and fatal probes exercise the new grouping; no assertion or case was dropped.

### Journal queue-delay reproduction after grouping

The next normal parallel run (`full-release-grouped/`) passed the native child **86/86 in 102.092 s** (17.908 s headroom). The healthy probe group passed **6/6 in 9.789 s**, versus six process lifetimes totalling 52.50 s before grouping. This reduces five process startups, not test assertions. That run nevertheless remained **not green: 2,809 passed / one failed**. `ConcurrentJournalNavigationsKeepFreshStateWhenCancelledReadsFinishLate` timed out in `WaitForJournalPhaseAsync`, original line 296, while waiting for its fake repository.

The newly available trace identifies the delayed flow precisely:

```text
parallel navigation 15, before navigation: poolThreads=17, busyWorkers=14, pending=26
0.140 ms after navigation: load=WaitingForActivation, loading=True, pending=28
8460.836 ms waiter: signal=WaitingForActivation, load=WaitingForActivation,
calls=0, loading=True, error=<none>, poolThreads=18, busyWorkers=11, pending=47
```

Neither the Account-reader nor repository-entry breadcrumb appeared for that flow before timeout. Other flows recorded all **17** existing workers busy while work queued. The five-second timer's continuation itself was only observed at 8.46 seconds. Thus this reproduction fails before read invocation, not after cancellation or because the application accepted a stale response. Some other flows entered their first fake promptly but were delayed on return loads. The trace demonstrates shared test-host scheduling contention; it does not identify the exact unrelated test holding each worker or retroactively prove the original historical timeout's precise event sequence.

The three reader-race cases (the two late-success/error theory cases and the 16-flow concurrent case) now share one separately supervised **30-second** `journal-navigation-races` child. This isolates their ThreadPool from unrelated synthetic WPF work while the real production activation/read/cancellation path remains unchanged. All **16 flows still run concurrently**, their **five-second** operation guards and old/fresh-state assertions remain, and other test assemblies/collections still execute normally in parallel. This adds one child while healthy-probe grouping removes five. No global worker-pool settings, sleeps, retries or broader serialization were added. Account-boundary and early-failure diagnostic cases remain in the ordinary host. The child TRX must report the three cases; passing parent names alone are not evidence of their assertions.

### Follow-up verification

| Check | Result |
| --- | --- |
| First correction, before healthy-probe grouping and Journal isolation: harness/process probes, Journal navigation regressions, grid and native tests; two .NET-reported CPUs, process-local 96-DPI mode and 1,044-DIP native-window cap | **280/280 passed**, no skips; includes 16 concurrent Journal navigation flows, late success/error handling, fatal child reporting, timeout poisoning and nested-frame exclusion. |
| Isolated children in that first run | **86/86 native cases / 85 STA actions**, **72.550 s**, **47.450 s headroom**; **36/36 grid**. Historical-width comparison scopes absent with capture disabled. |
| Existing initial-owned-panel theory with capture enabled in a fresh constrained native test host | **12/12 passed**, 18 s reported test duration. All 12 historical comparisons and both Light/Dark, normal/narrow/high-DPI bitmap outputs exercised; no live interaction claimed. |
| Final constrained run after both process changes (`constrained-final/`), same two-CPU/process-local DPI/width settings | **280/280 passed**, no skips. Child TRX: native **86/86**, grid **36/36**, healthy probes **6/6**, Journal races **3/3**. Native **73.484 s**, **46.516 s headroom**; grid 28.074 s; healthy probes 3.473 s; Journal races 3.461 s. These are observed process lifetimes, not a guarantee under full load. |
| Final normal parallel full Release run (`full-release-final/`) | **Failed: 2,725 passed / 85 propagated failures / zero skips**. Domain **444/444**, Application **516/516**, Infrastructure **775/775**, Desktop **990/1,075**. The native child reached its 120-second aggregate deadline; no individual action deadline expired. |
| Other child suites in that final full run | Grid **36/36 in 71.955 s**, healthy probes **6/6 in 12.410 s**, Journal races **3/3 in 13.278 s**; fatal/poisoning/Application-lifetime probes passed. Child cleanup was confirmed. |
| Release build / EF model consistency / `git diff --check` | Passed; zero build warnings/errors, no pending model changes, no whitespace errors. EF uses the existing in-memory design-time factory; no real journal accessed. |
| GitHub / live UI | No matching GitHub run; earlier CI #77 does not verify M14.5. No live UI acceptance performed. |

### Remaining full-load blocker

The final native process started at `17:13:30.821Z`; its first STA action began at `17:13:41.855Z` (**11.034 s startup**). **77 actions finished and the 78th started**. Its final phase was again `JournalActionClosesProtectedModalBeforeNavigatingAndVetoKeepsTradeDraft`, entering `test action` at `17:15:30.362Z`, only about 0.3 seconds before the supervisor deadline fired. This does **not** establish that the Journal modal action hung. The last lifetime sample had **101.406 s process CPU**, **34 native threads**, about **483.0 MiB working set / 372.7 MiB private bytes**, one shared STA, and zero pending ThreadPool work. The renderer-lifetime correction remains effective, but the whole workload still lacks reliable aggregate headroom under concurrent assembly load. The native process was terminated and its tree cleanup confirmed, so it produced no completed native TRX; the parent failures are one shared suite result, not 85 distinct assertion failures.

The earlier grouped full run's 102.092-second native pass is useful evidence, not proof of a stable performance fix. The final run supersedes it for acceptance: **M14.5 automated acceptance is not green**. The measured redundant startup and diagnostic work were reduced, and the observed Journal scheduling failure is isolated without changing its race assertions, but the remaining native full-load budget exhaustion is unresolved. No further speculative production/harness change, deadline increase, retry, skipped case or global serialization was made to obtain a green result. Further work needs full-load per-operation profiling to distinguish the remaining layout/window cost from concurrent renderer/CPU contention; a matching Windows CI run is also still required. No M14.6 work was started.

## M14.5 bounded Desktop fan-out (2026-10-05)

The actual checkout was **`dd683873f5b8d2709b1784955c9b3477c5bb15a0`**, `develop`, initially clean: the user had committed all ten preceding files. The remote `develop` ref matched, but the repository-wide Actions query for this exact pushed SHA returned **zero runs**. No Git writes or real journal access were performed. This follow-up does not turn the older CI #77 result into M14.5 verification.

### Controlled comparisons before correction

All runs below used the same Release binaries, eight reported logical CPUs, ordinary Windows/DPI settings, all 86 native cases and their unchanged deadlines. `PTJ_CALENDAR_TIMINGS=1` was enabled. The full baseline ran all four assemblies in parallel. The two workload comparisons excluded only unrelated tests **for diagnosis**, not from final acceptance or CI. The native child was not run concurrently with a separate experiment. Process sampling at one-second intervals recorded assembly module, CPU, working/private memory and thread counts; Windows CIM command-line inspection was denied, so loaded test modules and existing child PID breadcrumbs identified the processes. No machine-wide scheduling or display setting was changed.

| Workload | Native process wall seconds | Headroom to 120 s | Completed STA actions | Sum action wall / process CPU seconds | Peak action-boundary working set MiB |
| --- | ---: | ---: | ---: | ---: | ---: |
| Native only | 75.512 | 44.488 | 85 | 70.298 / 75.828 | 652.953 |
| Native + all 775 Infrastructure tests | 88.462 | 31.538 | 85 | 81.666 / 84.047 | 642.250 |
| All Desktop tests, no other assembly | 98.419 | 21.581 | 85 | 89.806 / 91.922 | 707.789 |
| Complete parallel Release baseline | timed out at 120 | none | 84 | 104.205 / 97.453 (completed actions only) | 650.727 |
| Complete parallel Release, bounded Desktop collections | 97.707 | 22.293 | 85 | 86.258 / 87.984 | 728.957 |
| Corrected focused harness run, isolated native child | 80.779 | 39.221 | 85 | 73.857 / 80.453 | 624.984 |
| Corrected complete parallel Release with CI diagnostics | 102.859 | 17.141 | 85 | 90.253 / 94.750 | 680.672 |

85 STA actions plus the pure calculation case are **86 tests**. CPU includes the native renderer and other threads in that child, so it can exceed wall time; wall minus process CPU is **not** a scheduling-wait measurement. Boundary memory and one-second peak samples differ. The baseline completed 84 actions in this reproduction, versus 77 in the prior run; that variance is retained rather than selecting a favourable result.

[Raw per-action timings](ci-evidence/m14-5-native-load.csv) preserve UTC starts, exact wall/CPU durations, memory samples and observed occurrence order. Occurrences are not invented theory-parameter identifiers. Full phase/JSONL/supervisor logs, TRX and sampled process data remain in ignored `artifacts/m145-load/`. The four baseline/comparison directories are `baseline-full`, `native-isolated`, `native-infrastructure`, `desktop-only`; `bounded-full-built` is the first corrected full run. An attempted `bounded-full` measurement was cancelled because it started before a build completed, locking copy destinations; it is excluded from timing/acceptance evidence. A subsequent completed build had zero warnings/errors before corrected verification.

### Where the time went

Same operations and counts (seconds; nested scopes are inclusive and must not be summed):

| Operation | Count | Native only | Native + Infrastructure | Desktop only | Full baseline | Bounded full |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Process startup to first STA | 1 | 2.855 | 3.704 | 3.959 | 8.741 | 6.901 |
| Initial owned-panel owner Show/layout | 12 | 5.901 | 9.595 | 10.624 | 13.965 | 10.102 |
| Initial owned-panel modal interaction/return | 12 | 7.996 | 11.024 | 12.670 | 14.367 | 11.891 |
| Initial owned-panel close cleanup (dialog + owner) | 12 + 12 | 3.169 | 3.669 | 4.022 | 4.417 | 3.887 |
| All theme resource loading | 85 | 6.333 | 7.978 | 8.320 | 10.537 | 8.679 |
| All modal dispatcher pumps | 221 | 6.335 | 7.246 | 8.336 | 8.875 | 7.686 |
| Chart layout / bitmap rendering | 21 / 21 | 2.024 / 0.202 | 2.639 / 0.258 | 1.776 / 0.325 | 3.582 / 0.346 | 2.964 / 0.414 |
| Fixture creation/activation | 61 | 0.501 | 0.939 | 1.317 | 2.325 | 1.014 |

The competing workload is **combined Desktop fan-out and Infrastructure CPU**, not a single stalled modal action. Infrastructure alone added about 12.95 seconds to the native lifetime; other Desktop work added about 22.91 seconds without Infrastructure. These are ablations, not additive estimates of saved wall time. In the Desktop-only trace, the main Desktop host consumed **92.09 CPU seconds**, the concurrent grid child **45.23 CPU seconds**, in addition to native **99.12 CPU seconds**; independent Journal/resource/probe children also overlapped. Infrastructure consumed **141.95 CPU seconds** in the paired run. Domain/Application finished quickly and were not the sustained load. The full baseline also failed `JournalTradeContextTests.CancelOrDeactivateRejectsLateSuccess(true)` at reader readiness; the Desktop-only run failed `RefreshClearsOldRowsImmediatelyAndErrorsRemainSeparateFromEmpty` at the same readiness boundary. Neither assertion failure establishes stale production data. Their assertions were preserved, not separately retried or isolated away in this change.

### Small execution-layout correction

`Desktop.Tests/AssemblyInfo.cs` caps **Desktop xUnit collection concurrency at two**, rather than automatically multiplying WPF renderer/native-child work by the runner's processor count. This is not global serialization: two Desktop collections can run together; Domain, Application and Infrastructure retain their existing concurrency and still overlap Desktop; explicitly concurrent test operations (including 16 Journal flows and import races) are unchanged. No test is excluded and no test method/assertion, 120-second aggregate bound, per-operation deadline, process cleanup, fatal callback or timeout poisoning rule changes. The existing external native action gate still prevents nested-frame reentry.

The only other harness code change adds opt-in timing for that gate, dispatcher readiness and the interval from posting a dispatcher operation to its execution. In the corrected full run, **85 scheduled waits totalled 102.669 ms, maximum 15.971 ms**; readiness totalled 0.501 ms. Gate waits totalled 31.483 seconds, maximum 2.749 seconds, overlapping the other native class's active work. They are intentional serialization of the one shared STA, not an extra 31-second wall-time penalty. Idempotent disposal avoids double-reporting a queued timing scope during cancellation/exception cleanup. No production dispatcher or ThreadPool setting changed.

### Verification

The first corrected complete parallel Release run passed **2,810/2,810**, zero failures/skips: Domain **444**, Application **516**, Infrastructure **775**, Desktop **1,075**. The native child passed **86/86 in 97.707 seconds**, leaving **22.293 seconds**; grid **36/36 in 24.987 seconds**; Journal race child **3/3**. Healthy, fatal, poison and nested-frame harness regressions passed with unchanged assertions. Native memory remained bounded; the improvement is concurrency control, not a claimed new memory reduction.

Final verification on the same code:

| Check | Result |
| --- | --- |
| Focused harness/process, Calendar native/grid and Journal/navigation regressions (`bounded-focused`) | **280/280 passed**, no skips. Native **86/86 in 80.779 s**, **39.221 s headroom**; grid **36/36 in 20.421 s**; Journal race child **3/3 in 3.374 s**. Ordinary eight-CPU/DPI settings, not a machine-wide CPU constraint. |
| Second complete parallel Release run, with the workflow's `--blame-hang --blame-hang-timeout 3m --blame-hang-dump-type mini --diag` (`bounded-ci-full`) | **2,810/2,810 passed**, zero failures/skips, same 444/516/775/1,075 project counts. Native **86/86 in 102.859 s**, **17.141 s headroom**; grid **36/36 in 26.452 s**; Journal races **3/3 in 7.722 s**. This is local CI-command-equivalent evidence, **not GitHub Actions**. |
| Release build | Passed, **zero warnings/errors**. |
| EF model consistency | No pending changes, using the existing in-memory design-time factory. |
| `git diff --check` | Passed. |
| Production/schema/real data/live UI | None changed/accessed; no live UI verification performed. |

The second full run checks diagnostic-collector overhead after the first green run, not a retry of a failed assertion. Both full runs keep normal solution-level parallel execution. Raw per-action CSV includes both corrected full runs and the focused run. The demonstrated local native-budget blocker is resolved in these measurements; **hosted CI verification is still outstanding**. No claim is made that unbounded processor-count-driven fan-out is safe, or that one machine's headroom predicts every runner.

Local timings are observations, not a guarantee on a hosted runner. Matching GitHub Actions and live application acceptance remain separate gates; no M14.6 work is included.

## Reproduce without changing Windows display settings

### M14.6 prerequisite check (2026-10-05)

Actual clean `develop` HEAD was `91a634c31c903bb18b9223d8f9fe788f042206a7`, containing the preceding Desktop concurrency correction. The GitHub Actions API query for this exact `head_sha` returned **total_count: 0**; latest green run #77 tests the older M13 `main` merge. Thus the two preceding local green parallel runs do **not** establish hosted M14.5 acceptance. No matching failure was found to investigate before implementing M14.6. This workflow runs for pushes to `main` and PRs targeting `main`, not standalone `develop` pushes; only the user's Git writes can trigger verification of the new changes.

M14.6 adds a separate supervised Journal-history render child (one compiled-view scenario covering both themes, normal and narrow/high-DPI), reusing existing background STA lifecycle and deadlines. It does not alter native/grid budgets, the two-collection Desktop cap, assertions or isolation. Local results and remaining live history checks are recorded in [Daily Journal](daily-journal.md#m146-automated-verification-and-manual-follow-up). A matching new GitHub Actions result remains required.

Final local workflow-command-equivalent parallel Release run: **2,838/2,838 passed**, zero failures/skips (444/529/779/1,086 by project), with TRX, blame collector and diagnostics enabled. Native child **86/86**, started **19:35:58.3951908 UTC**, exited **19:37:33.2026613 UTC**: **94.807 s**, **25.193 s** headroom against its unchanged 120 s bound. Grid **36/36**, **40.120 s** process wall time. Raw logs/TRX remain under ignored `artifacts/m146-full-release`; focused **233/233** passed, build had zero warnings/errors, EF/diff checks passed. A final exact-SHA GitHub lookup still returned zero runs. These are local results only, not hosted CI or live UI acceptance.

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

### Calendar grid deadline follow-up, 2026-10-06

The Journal correction started on clean `develop` at `f73b8fb9cd53821c3cf50420f04b8cfe880039ba`. The prior Journal refinement run had 36 propagated grid failures from one `calendar-grid` child reaching its unchanged 120-second aggregate bound. Its last completed action ended at `12:52:04.710Z`; the next `HitTestingScopesDateHoverToMarkerNotDailyWeeklyOrEmptyCellAreas` action entered at `12:52:05.212Z`, approximately 1.3 seconds before the deadline. This does not show that that action stalled. The last completed boundary reported 91.484 process CPU seconds, about 166.6 MiB working set and zero pending ThreadPool work. Process cleanup was confirmed. Native passed separately in 118.812 seconds; none of these failures were Journal assertions.

The unchanged Calendar cases were measured separately before the current Journal build, then in the final full parallel run. No scheduling, resources, dispatcher lifetime, assertions or budgets were changed. Existing opt-in timing was enabled; historical raw logs were retained.

| Measurement | Previous failed full run | Isolated unchanged grid | Current parallel Release |
| --- | ---: | ---: | ---: |
| Completed grid actions/cases | 31 before termination | 36/36 | 36/36 |
| Grid supervisor wall seconds | 120-second deadline | 21.792 | 35.454 |
| Grid headroom seconds | none | 98.208 | 84.546 |
| Compiled-grid four-case action elapsed sum | 16.080 | 3.150 | — |
| Those cases: Measure seconds | 3.470 | 1.050 | — |
| Those cases: UpdateLayout seconds | 7.830 | 0.970 | — |
| Those cases: RenderTargetBitmap.Render seconds | 0.810 | 0.160 | — |
| Day-selection four-case action elapsed sum | 23.520 | 4.840 | — |
| Monthly-header eight-case action elapsed sum | 17.090 | 2.740 | — |
| Native full-run cases / wall seconds | 86/86 / 118.812 | not run here | 86/86 / 77.743 |

Rounded phase sums are inclusive observations, not additive estimates of scheduling delay. The slowdown spans several groups and layout phases; no single blocked application operation or accumulating grid-memory defect was demonstrated. Full-load scheduling sensitivity remains plausible, not a newly proven exact cause. The current grid's final boundary reported 38.047 process CPU seconds and about 175.9 MiB working set; multi-threaded CPU is not wall time or time waiting for work. The earlier failing schedule was not reproduced, so no speculative Calendar/harness fix was made.

Raw old phases/supervision: ignored `artifacts/journal-ui-refinement/full-release-final/calendar-grid-20261006-125005-9134f102124f45bc8d31821408e5cc8b`. Isolated: `artifacts/journal-layout-fix/grid-isolated-before/calendar-grid-20261006-143327-4e81e9b4fd35439a937e06ecdd276d3c`. Current full grid: `artifacts/journal-layout-fix/full-release/calendar-grid-20261006-144106-35ae349590554f3ba845fae33a3f5420`; native: `calendar-native-20261006-144143-270060e416a84729830510ed824840d6` in that full-run directory. Logs retain phase records, supervision and child TRX where completed.

Current complete parallel Release passed **2,852/2,852**, zero failures/skips (444 Domain, 529 Application, 783 Infrastructure, 1,096 Desktop). Build had zero warnings/errors; EF/diff checks passed. This is a green local run, not permanent resolution of earlier deadline variance or green GitHub Actions. A matching new PR workflow after the user's Git writes remains required. Live UI acceptance is separate and was not performed.

The workflow sets `PTJ_TEST_RESULTS_DIRECTORY`, enables TRX and VSTest diagnostic logging, and uploads `windows-test-results` even after a test failure. Unique `calendar-grid-*` and `calendar-native-*` folders contain the child TRX, `stdout.log`, `stderr.log`, `process-supervision.log`, `calendar-sta-phases.log`, any dump/sequence files, and `calendar-layout.txt` for native cases (actual screen/work area, window, DPI, viewport, extent and columns). Synthetic diagnostics do not log Account names, Trade IDs or customer rows. The complete child output remains in artifacts; parent exceptions contain only a bounded tail and its location.

The outer Test step has a three-minute per-case VSTest hang collector and a ten-minute step bound so an unrelated stuck test can produce diagnostics and reach artifact upload without waiting indefinitely. These are fallback safety limits, not an increase of the original 30-/45-second Calendar deadlines and not retries. A collected mini dump may provide a stack where available; phase logs remain useful if dump collection fails or the host exits first.

A passing local reproduction/full suite is not a passing GitHub run. The user must commit/push these changes to a PR targeting `main`, let a new Windows CI run test that head/merge tree, and verify all project suites plus the current **36 grid** and **86 modal/chart child cases (85 STA actions)**. If it fails, inspect the first unfinished phase, lifetime counters, opt-in timing records and any dump before attributing the cause. Rerunning an older workflow run would only retest its old tree.

## M16.6a — Journal editor STA deadline investigation (2026-10-10)

Actual starting checkout: `develop`, `b00b7a243058f6dcd7f890e3cadcc991a0d292ba`, clean; M16.1–M16.6 were already committed by the user. This investigation changes only the Journal layout test and documentation. It neither accesses the production journal nor starts M16.7.

### Failure versus reproduced measurements

The M16.6 full parallel run failed `JournalViewTests.CompiledEditorBindingsScopeVetoAndLightDarkNormalHighDpiLayouts` at its **30-second STA deadline**, not at the two-minute child-process deadline. Original evidence remains in ignored `artifacts/m166-full/` and `tests/PersonalTradingJournal.Desktop.Tests/bin/Release/net10.0-windows/TestResults/journal-editor-20261010-153101-b57eb7781a5e4631a1ed729d482c496c/`. The last completed render ended at 25.928 seconds; timeout was 30.052 seconds, process CPU 28.328 seconds, working set about 157 MiB. Its last phase name was stale: no matching unfinished bitmap call establishes a renderer hang. The original dump was not decoded into a managed stack in this investigation. We cannot retrospectively name its precise unfinished instruction.

Additional breadcrumbs and opt-in `PTJ_CALENDAR_TIMINGS=1` now measure resource acquisition, view construction, Measure, Arrange, UpdateLayout, dispatcher flush, bitmap rendering, date validation, the 100,001-character binding/layout check, and fixture cleanup. Existing harness boundaries record STA startup, dispatcher acquisition/shutdown, process CPU/memory and thread-pool state. No deadline or scheduling priority changed.

The diagnostic full baseline **did not reproduce the timeout**: all 3,661 tests passed before the resource-reuse correction. Separate baseline comparisons used the same instrumented Release binary: Journal alone; Journal plus all 39 export cases in parallel; and the complete parallel solution. All ran on eight logical CPUs with the existing two-collection Desktop cap and normal parallelism in the other assemblies. No CPU affinity, global serialization, sleep, retry, skip or test-filter change was applied to the complete runs. A prematurely launched measurement (`m166a-baseline-full`) was cancelled before any testhost ran while the initial build was finishing; it is excluded. The completed build had zero warnings/errors before measured runs.

| Journal measurement (seconds) | Baseline isolated | Baseline + exports | Baseline full | Reuse + exports | Reuse full |
| --- | ---: | ---: | ---: | ---: | ---: |
| STA lifetime, including cleanup | 8.099 | 11.777 | 19.710 | 8.714 | 13.366 |
| Remaining to 30-second deadline | 21.901 | 18.223 | 10.290 | 21.286 | 16.634 |
| Child launch to STA start (runtime/discovery/fixtures) | 2.444 | 4.948 | 8.623 | 2.776 | 9.031 |
| STA thread startup | 0.026 | 0.040 | 0.076 | 0.029 | 0.043 |
| Dispatcher acquisition | 0.022 | 0.034 | 0.043 | 0.027 | 0.045 |
| Resource preparation (including assignment) | 1.867 | 2.553 | 4.915 | 0.562 | 0.731 |
| Shared-resource parses / parse seconds | 23 / 1.778 | 23 / 2.429 | 23 / 4.585 | 2 / 0.502 | 2 / 0.628 |
| View construction, 23 views | 1.014 | 1.540 | 2.525 | 1.294 | 2.027 |
| Measure, 23 calls | 1.147 | 1.604 | 3.027 | 1.413 | 2.390 |
| Arrange, 23 calls | 0.299 | 0.449 | 0.868 | 0.388 | 0.657 |
| UpdateLayout, 23 calls | 0.700 | 1.004 | 1.683 | 0.909 | 1.374 |
| Dispatcher flush, 83 calls | 0.989 | 1.808 | 2.252 | 1.449 | 2.550 |
| Bitmap rendering, 28 calls | 1.071 | 1.373 | 2.324 | 1.382 | 1.770 |
| Oversized text binding/layout | 0.807 | 1.531 | 1.957 | 1.191 | 2.260 |
| Assertions finished to STA shutdown | 0.034 | 0.011 | 0.023 | 0.011 | 0.012 |
| Process CPU increase across STA boundaries | 10.703 | 15.922 | 19.141 | 11.547 | 14.875 |

Scopes overlap; do not add them as exclusive costs. Dispatcher flush processes queued WPF/binding/layout work and is not a pure scheduling-delay measurement. Process CPU includes renderer/helper threads and can exceed wall time; wall minus CPU is not a valid wait estimate. Launch latency includes discovery and fake fixture creation, outside the STA's 30-second budget. Detailed phase/JSONL/supervisor/TRX and one-second process samples remain under ignored `artifacts/m145-load/m166a-{baseline-isolated,baseline-export,measured-full,corrected-export,corrected-full}/`.

### Evidence and narrow correction

The export-only overlap increased baseline STA time by 3.678 seconds; the entire solution increased it by 11.611 seconds versus isolation. During the baseline full Journal interval, sampled Infrastructure CPU increased by **43.42 seconds**, the concurrent Calendar-native child by **4.31 seconds**, and the main Desktop host by **8.22 seconds** (sample endpoints fall inside the interval). Ten export CSV cases overlapped; exports were not the only active Infrastructure work. This establishes shared CPU/workload sensitivity, **not** a particular export operation blocking the Journal or proof of the original timeout's exact cause. The Journal test uses fake readers and never invokes export. STA startup/acquisition/cleanup stayed short and no queued thread-pool work was observed at its boundaries.

The demonstrated avoidable work was reparsing identical shared XAML/theme resources for each of 23 detached views. `JournalViewTests` now keeps one Light and one Dark dictionary in the **test instance**, creates/uses them only on its one STA, and clears that cache in `finally` before dispatcher shutdown. It only reassigns Application resources when changing dictionaries. There is no cross-test/thread static WPF cache. This mirrors shared application styles while continuing to construct every real compiled view. Two distinct cached dictionaries are asserted; existing per-theme brush/contrast/button-state assertions still run. The cache does not mutate shared theme styles.

Every existing assertion and scenario remains: four theme/size combinations including 240 DPI, compact/editor states, Draft/Completed/read-only behavior, 45-line notes and scrolling, both date cultures, scope vetoes, empty/required text states, and the **same unbroken 100,001-character input**. All **23 views, 28 bitmaps and 83 dispatcher flushes** remain. The 30-second action/hang bounds, two-minute child bound, background STA, process cleanup, fatal/poison protection, native nested-frame guard and solution concurrency are unchanged. No production code, export code, schema or economic rules changed.

Resource preparation fell from 4.915 to 0.731 seconds in the full runs; total STA lifetime fell from 19.710 to 13.366 seconds. This is evidence for removing redundant harness work, not a claim that all timing variance or the earlier failure has been conclusively eliminated. A new matching Windows CI run and future loaded-run evidence remain separate from local success.

### Final verification

- Focused corrected Journal + export overlap: **40/40 passed** (one compiled Journal test and all 39 export cases), no skips. Before/after comparison uses the same filter, not removed coverage.
- Corrected **complete parallel Release: 3,661/3,661 passed**, zero failures/skips: Domain 454 (3 s), Application 710 (5 s), Infrastructure 1,117 (2 min 17 s), Desktop 1,380 (4 min 3 s). These include the existing fatal/timeout/dispatcher-lifetime harness regressions. Calendar native **86/86**, child wall **87.472 s**; grid **36/36**, child wall **40.586 s**. Journal, Calendar-native and grid supervisors reported confirmed exit/cleanup.
- Both complete measurements used `dotnet test PersonalTradingJournal.sln -c Release --no-build --logger trx --results-directory <run> --blame-hang --blame-hang-timeout 3m --blame-hang-dump-type mini --diag <run>/vstest.log`. The existing ignored `artifacts/m145-load/Measure-Load.ps1` supplied opt-in timings and periodic process samples. Different xUnit execution orders and overlaps occurred; the difference in overall suite duration is not attributed solely to this change. No test retry loop was used.
- Final Release build: **0 warnings/errors**. EF `has-pending-model-changes --configuration Release --no-build`, using the in-memory design-time factory: **no changes**. `git diff --check`: passed.
- Changed files: `tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalViewTests.cs`, `README.md`, `docs/ci-wpf-tests.md`, `docs/backup-restore.md`. Branch/HEAD unchanged. No production data, provider calls, migrations or Git writes.

**Local full-suite acceptance is green.** The original deadline failure's precise blocking instruction remains unverified; measurements demonstrate load sensitivity and remove a specific repeated-work cost. This is not proof that every runner has sufficient headroom. No live UI acceptance was performed or required by this harness correction. Uncommitted changes cannot have a matching GitHub Actions run; the existing workflow requires a push to `main` or a PR targeting `main`. The user must commit/push the change through their existing PR workflow and inspect the new run before claiming CI acceptance. No trigger changes or M16.7 implementation are included.
