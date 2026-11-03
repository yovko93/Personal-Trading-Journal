# CI #112 — Daily Review acceptance watchdog

## Status — 3 November 2026

**Unresolved duration/root cause; not fixed.** The hosted failure occurred during database initialization, with the sampled execution inside a SQLite migration transaction commit. Both parameters passed unchanged locally. Only bounded fixture phase diagnostics have been added; no production, scheduling, assertion, deadline or branding correction is justified by this evidence. No complete gate was rerun for this diagnostic-only task.

The hosted gate reports **5,538 passed / 2 failed / 0 skipped**, with **145/145 exclusive cases passed**. The two failed top-level cases share **one aborted isolated suite**, not two independently observed hangs.

## Provenance and artifacts

- [CI run #112](https://github.com/yovko93/Personal-Trading-Journal/actions/runs/41524249602), attempt 1, job `125547799718`, PR #27.
- Head/local base: `f2df74c51b7ba21ba6d192ee29455b4ce0d28de1` (the corrected scheduling contract).
- Actual checkout: merge `2160dea6feec78356c0ded4435a625dacb8c1fa1`, verified in the checkout job log. GitHub comparison against the local base reports zero changed files.
- Artifact `13178744524`, `windows-test-results`, ZIP SHA-256 `7067D7D442434F35320182BC609271BE7B45A59550609AC1FF2F8E87B3AF1B39`, verified against the published digest.
- Local evidence root: `TestResults/ci112-daily-review/`. It retains the downloaded ZIP/extraction, job log, hosted summary, parent VSTest extract, dump analyses, builds and focused results. Earlier dumps were not used as evidence for this incident; only the existing debugger executable was reused.
- Hosted run: `release-20261102-210231-3069d144a7b44e9193c3b7fd99cf4b13/`.
- Child: `parallel/daily-review-acceptance-20261102-210908-45b3c7de05cb4c93b3a4c86b443d8e66/`.
- Dump: `c4baec89-bb3d-48ca-884c-ed465cbd8546/testhost_9284_20261102T210940_hangdump.dmp` (6,960,472 bytes). The artifact also contains a TRX deployment copy of this dump and sequence, not a second incident.

## Which cases executed

`MainWindowViewModelTests.ReviewAcceptanceHost` is one static `Lazy<Task>` that runs all `DailyReviewAcceptance` cases in a single supervised child. Both parent theory invocations await that same task. Its exception therefore fails both parent identities (`notifyChange: True` and `False`). The outer deadline remains two minutes and the child inactivity watchdog remains 30 seconds.

The child `Sequence_1b0fabb45ae94236ad7d00b0bcf6d33f.xml` contains exactly one entry: **`notifyChange: True`, `Completed="False"`**. There is no recorded start for `False`. The child TRX has no case-result rows and zero completed/executed result counters; it records adapter discovery/start and an aborted run. Those counters do not mean the body never began: the sequence and dump establish that True was executing. False has no independently observed execution or hang in this child.

Launcher PID **1444** began at `21:09:08.6395494 UTC`; testhost PID **9284** ran .NET **10.0.14**. The watchdog reported 30 seconds of inactivity, collected the `21:09:40` hang dump (createdump reports **17,511 ms**), then the run was aborted. Launcher exit 1 and cleanup were confirmed at `21:10:11.662 UTC`, before the outer two-minute deadline. VSTest's generic “Test host process crashed” text accompanies that watchdog/dump/abort sequence; no separate crash exception is established.

Available diagnostics include child stdout/stderr, supervision log, TRX, Sequence XML and the parent stage's VSTest logs. No dedicated child `/Diag` log or acceptance phase file was retained: the original suite launcher did not request one. No Calendar STA phase file exists for this child.

## Dump findings and limits

`dotnet-dump` analyzed this exact dump with Microsoft symbol-server resolution. `sosstatus` identifies the target as Windows x64 PID 9284, CoreCLR **10.0.1426.52326**, runtime path `Microsoft.NETCore.App/10.0.14/coreclr.dll`, DAC timestamp/image identity **6ADAED32 / 0014D000**, with DAC signature checking enabled. The debugger's own .NET 8 host is separate from that target runtime. Public runtime/EF frames resolve to source locations; application method identities come from the dump's loaded assembly metadata and are correlated with the verified tested source tree.

The artifact contains no private application DLL/PDB outputs. Consequently exact private PDB/IL-offset line mapping and full native SQLite internals are unavailable; local rebuilt PDBs were not substituted as if they matched the hosted build. The minidump could not supply SOS SyncBlk or ThreadPool aggregate data (`Error requesting SyncBlk data`, `Failed to obtain ThreadPool data`). These limits remain explicit in the retained analysis logs.

The active managed thread is **10**, OS TID **0x18b0 / 6320**. Its stack is:

```text
SQLitePCL.SQLite3Provider_e_sqlite3.NativeMethods.sqlite3_step
Microsoft.Data.Sqlite.SqliteDataReader.NextResult
SqliteCommand.ExecuteNonQuery
SqliteConnectionExtensions.ExecuteNonQuery
SqliteTransaction.Commit
DbTransaction.CommitAsync / RelationalTransaction.CommitAsync
MigrationCommandExecutor.ExecuteAsync / Migrator.MigrateAsync
JournalDatabaseInitializer.InitializeAsync
JournalTestDatabase.CreateAsync
DailyReviewAcceptanceCompiledShellKeepsCapturedEvidenceOrCancelsOnSourceNotification
```

This locates the unfinished phase at the test's first awaited operation, `JournalTestDatabase.CreateAsync`, specifically migration transaction commit. Source order establishes that directory creation, service registration/provider construction, initializer resolution and context creation had already returned sufficiently to reach migration execution. Execution reached the commit after migration-command work; the artifacts do **not** identify the last completed SQL statement, exact migration, or when this particular commit started. The watchdog bounds test inactivity, not the measured duration of that individual commit.

`JournalDatabaseInitializer` runs migration before projection reconciliation. The acceptance fixture has not yet seeded its accounts/trades/journals, constructed Daily Review services, entered its first `Ui` action, created the WPF shell, started synthetic transport generation, or reached UI cleanup. Its UI setup appears later in source, after database creation returns. There is no sampled application dispatcher stack. The commit uses synchronous SQLite work under the async EF API; an async method name alone does not establish an asynchronous wait or sync-over-async deadlock.

Other decoded stacks include the normal testhost engine Task wait, xUnit runner/reporting waits, timer/I/O waits and idle worker semaphore waits. These do not establish a circular wait. The dump cannot establish a SQLite lock owner, storage latency, disk pressure, CPU contention or a managed deadlock. Each remains a hypothesis, not a demonstrated cause. A unique temporary database root is constructed per fixture; that alone neither proves nor rules out wider host/storage contention.

## Narrow reproduction and added evidence

On the unchanged local source, the normal parent filter ran **both parents successfully**, backed by **one child TRX with both True and False passed** (`unchanged/`). Local runtime is **10.0.12**, while hosted runtime was **10.0.14**; hosted SDK setup selected **10.0.303**. Local success is not an equivalent hosted-load reproduction and does not resolve the cause.

Only `DailyReviewAcceptanceTests.cs` gains instrumentation. It uses the existing optional database interceptor argument, scoped to this acceptance fixture. File-only transition records cover database creation begin/complete, initialization transaction committing/committed (transaction ID and database path), seed/evidence completion, shell creation/initial load, assertions and UI cleanup. They include UTC, elapsed time, parameter, PID and managed thread. Commit tracing stops when database initialization returns. Output is capped at 128 records per parameter and errors are best-effort; there is no periodic timer, stdout heartbeat, database query, retry, input change or watchdog reset.

In future CI artifacts the records appear at:

```text
release-*/parallel/daily-review-acceptance-*/daily-review-phases-True.jsonl
release-*/parallel/daily-review-acceptance-*/daily-review-phases-False.jsonl
```

A missing completion record can now delimit creation or a particular commit; it still cannot alone explain why an operation failed to complete. Native wait/lock/storage evidence would be needed to establish that cause.

Both Release builds passed with **0 warnings / 0 errors**. After instrumentation, the focused run passed **3 top-level cases / 0 failed / 0 skipped** (two acceptance parents plus the scheduling guard), with **both acceptance parameters passed in one matching isolated child**. Phase files were inspected for initialization commit start/completion and completed UI cleanup. These are local observations, not retrospective hosted timings. Changed files: `tests/PersonalTradingJournal.Desktop.Tests/Navigation/DailyReviewAcceptanceTests.cs`, `README.md`, and this guide.

Commands used:

```powershell
dotnet build PersonalTradingJournal.sln -c Release --no-restore
$env:PTJ_TEST_RESULTS_DIRECTORY = Join-Path $PWD 'TestResults/ci112-daily-review/unchanged'
dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~MainWindowViewModelTests.DailyReviewAcceptance' --logger trx --results-directory TestResults/ci112-daily-review/unchanged

# After adding phase diagnostics and rebuilding:
$env:PTJ_TEST_RESULTS_DIRECTORY = Join-Path $PWD 'TestResults/ci112-daily-review/instrumented'
dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~MainWindowViewModelTests.DailyReviewAcceptance|FullyQualifiedName~ReleaseSchedulingTests' --logger trx --results-directory TestResults/ci112-daily-review/instrumented
```

The earlier 5,540/5,540 local scheduling-correction gate remains historical evidence. This incident does not invalidate its result or prove the historical Notebook mouse-readiness issue shares a cause. Branding and the corrected scheduling contract are preserved. No complete gate is warranted without a verified corrective change; no push, PR, merge or hosted rerun was performed.
