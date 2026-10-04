# SQLite integration-test pool ownership

## Failure evidence (2026-10-04)

Investigation baseline: `57fd1cdcbac073b72d0010fd3f49ce542cce92bf`, `develop`, clean worktree. This already contains the preceding CI console-probe correction; none of that work was reverted.

The prior full Release run passed 2,518 tests and failed one of 719 Infrastructure cases:

`TradeBrowseProjectionTests.SaveAsyncRegeneratesProjectionForCloseAndCorrectionOnlyForTargetTrade`.

The unchanged case subsequently passed alone. The original full trace is retained in ignored `bin/ci-probe-investigation/final-release/Yovko_DESKTOP-N2ONT35_2026-10-04_11_30_11_net10.0[2].trx`. Its exception is `ObjectDisposedException: Cannot access a disposed object. Object name: 'SQLitePCL.sqlite3'` at:

```text
SafeHandle.DangerousAddRef
SQLite3Provider_e_sqlite3.ISQLite3Provider.sqlite3_create_function (aggregate overload)
SQLitePCL.raw.sqlite3_create_function (flags overload)
SQLitePCL.raw.sqlite3_create_function
SqliteConnection.Open
DbConnection.OpenAsync
RelationalConnection.OpenInternalAsync (two frames)
RelationalConnection.OpenAsync
RelationalCommand.ExecuteReaderAsync
SingleQueryingEnumerable.AsyncEnumerator.InitializeReaderAsync
SingleQueryingEnumerable.AsyncEnumerator.MoveNextAsync
ShapedQueryCompilingExpressionVisitor.SingleOrDefaultAsync (two frames)
TradeMutationStore.GetByIdAsync, lines 35 and 52
TradeBrowseProjectionTests.SaveAsyncRegeneratesProjectionForCloseAndCorrectionOnlyForTargetTrade,
  lines 90 and 142
```

Line 90 is the test's first mutation-store load, after awaited insertion of two Trades and a projection snapshot. The fixture stays alive through the whole test. The store owns a fresh `await using` DbContext until both queries complete. The test uses no cancellation or concurrent operation on that context. This is not evidence of a production reader disposing its context too early.

## Demonstrated ownership defect

Seven teardown/error-cleanup calls in six Infrastructure test files used `SqliteConnection.ClearAllPools()`. Each fixture owns a unique temporary file, but those calls affect every fixture in the parallel test process.

The installed Microsoft.Data.Sqlite **10.0.11** has a vulnerable activation interval: [Activate](https://github.com/dotnet/efcore/blob/v10.0.11/src/Microsoft.Data.Sqlite.Core/SqliteConnectionInternal.cs) sets the active flag before publishing its weak owner. [Pool clearing](https://github.com/dotnet/efcore/blob/v10.0.11/src/Microsoft.Data.Sqlite.Core/SqliteConnectionPool.cs) can classify that interval as a leaked connection and dispose its handle. [Factory acquisition](https://github.com/dotnet/efcore/blob/v10.0.11/src/Microsoft.Data.Sqlite.Core/SqliteConnectionFactory.cs) activates outside the pool lock; global cleanup reaches unrelated pools.

An isolated diagnostic against that installed assembly stages the two writes with reflection, then performs unrelated cleanup:

| Controlled interleaving | Global cleanup | Scoped cleanup |
| --- | --- | --- |
| Activating connection before cleanup | `Leaked=True`, handle open | Same |
| Handle after unrelated teardown | Disposed | Open |
| Aggregate registration/query | `ObjectDisposedException: SQLitePCL.sqlite3` at `DangerousAddRef` / aggregate `sqlite3_create_function` | Query returns `7` |

The native failure matches the original trace. The probe calls `CreateAggregate` after resuming the staged activation, so its upper managed frames differ from the original `Open`. This demonstrates the provider interleaving and fixture ownership defect, **not** a capture or natural replay of the original thread schedule. Probe source and output are retained under ignored `bin/sqlite-lifetime-investigation/PoolActivationProbe/` and `activation-probe-result.txt`. Reflection is diagnostic-only, not part of the committed regression suite.

## Correction and regression contract

- `SqliteTestPoolCleanup.ClearPersistencePools` releases only the exact fixture-owned connection strings: `Data Source=<unique path>;Foreign Keys=True`, plus that same builder with `Mode=ReadOnly` appended. The latter is used by the existing read-only Topstep and analytics tests. A new pooled variant must be added explicitly; cleanup must not fall back to clearing every pool.
- All seven cleanup sites use this scoped helper after their contexts/providers are disposed, including failed initialization cleanup. Temporary databases are still deleted. Production connection pooling and production context lifetimes are unchanged.
- Three Desktop import fixtures had the same global-cleanup pattern. They now use the existing Desktop `ClearPool` pattern for their own exact persistence string. Those execute in a separate test host and did not cause the Infrastructure exception; this removes the same hazard there without changing any import assertions or the prior process-probe fix. No `ClearAllPools` calls remain in `tests` or `src`.
- Four deterministic ownership cases cover read/write and read-only connections, each active or idle during another fixture's teardown. All four failed before the change because the surviving native handle was closed. They require that the same live handle, TEMP sentinel, aggregate registration and query survive teardown; simply opening a replacement connection cannot satisfy them.
- A fifth test verifies that teardown closes its **own** read/write and read-only handles before removing the temporary database/directory.
- No retries, sleeps, blanket serialization, pooling disablement, assertion weakening or schema changes are used. All data is synthetic and isolated; the real journal is not accessed.

## Verification

| Local check | Result |
| --- | --- |
| Ownership regression with old cleanup | **4/4 failed** deterministically: unrelated native handle disposed. |
| Installed-provider activation diagnostic | Global cleanup reproduces the native disposed-handle exception; scoped cleanup succeeds under the identical staged schedule. |
| Focused Infrastructure interaction group, including the original test, cleanup fixtures and read-only variants | **63/63 passed on each of three consecutive runs**, ordinary xUnit parallelization, stop on failure. |
| Focused Desktop import fixtures, eight process probes and composite Import/Popup regression | **39/39 passed**. |
| Complete Release suite, one post-fix run | **2,524/2,524 passed**, zero failures/skips: Domain **400**, Application **516**, Infrastructure **724**, Desktop **884**. |
| Child WPF suites in that full run | Calendar grid **36/36**, native modal/chart **78/78** passed. These are automated tests, not live UI acceptance. |
| Solution Release build | Passed, **zero warnings/errors**. |
| `git diff --check` | Passed. |

Evidence is retained under ignored `bin/sqlite-lifetime-investigation/{before,focused-after,desktop-focused,full-release}`. Focused repetitions cover the reproduced ownership interaction; only one full post-fix suite was needed. The original failure did not recur. No production, schema, stored economics, process-probe implementation or real journal changes were made.

For a quick ownership check on Windows, run:

```powershell
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests/PersonalTradingJournal.Infrastructure.Tests.csproj --configuration Release --filter FullyQualifiedName~SqlitePoolOwnershipTests
```

A new GitHub Actions run after the user's commit/push is still required; local success does not establish CI success.
