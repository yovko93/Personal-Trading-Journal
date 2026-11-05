# CI #115 — Calendar cancellation ownership and Notebook input evidence

Subsequent [CI #116](ci116-notebook-input-onset.md) includes these corrections and completes with 5,540 passed / 1 Notebook failure. All 2,136 parallel Desktop cases pass. Its retained movement trace exposes a rolling-buffer retention gap, now corrected separately; the Calendar fix is preserved and the Notebook interaction cause remains unresolved.

## Provenance and hosted result

[Hosted run #115](https://github.com/yovko93/Personal-Trading-Journal/actions/runs/41816254257), attempt 1, job `126527631951`, tested merge **`159f99166af40ec86363f266e4e3d2aa28342b95`**. Head/local base is `ba183d3f421185d3744887e6b5b4f8fb52558236`; the checkout log identifies that merge and GitHub comparison against head reports zero changed files.

Artifact `13315333180` (`windows-test-results`) was retrieved and verified against SHA-256 `F38E85F34D85FC439DD5FE2468426375FDAC5DDB910CBAC84B29F0B60B6C4A92`. Artifact root: `release-20261104-104707-fa4a7dc906854fec854be631e9042307`. Local evidence root: `TestResults/ci115/`, including ZIP/extraction, `job.log`, `hosted-summary.json`, `notebook-summary.json`, original/fixed regression TRX and build/focused logs.

The run **completed with exit 1**, **5,537 passed / 3 failed / 0 skipped**: exclusive Desktop 143/2, parallel Desktop 2,134/1; Domain 602, Application 1,050, Infrastructure 1,608 all passed. There was no workflow timeout. The tested source includes single-parameter child dispatch and the 20-minute workflow budget. Its Notebook source contains boundary snapshots but **no processed-movement trace or controlled false-hover/native-delivery reproduction**. These facts were checked from matching source/artifacts, not line-number similarity. The two Notebook snapshots have the original 59 fields and no movement log.

## A. Calendar lifecycle exception

The failed top-level case is `JournalDraftCloseTests.EmptyNewCancelWritesNothingButNonemptyDraftIsNotDiscarded(inline: True, text: "...🙂")`. Line 52 is the test's `finally`: `editor?.Deactivate(); calendar.Deactivate();`. It follows draft retention/persistence assertions. The exception frame identifies `CalendarViewModel.Deactivate:257`, which releases the inline editor then cancels month, day, journal-status and day-journal reads. The hosted optimized stack does not identify the particular inlined source field or the exact interleaving; no claim is made that it does.

Saving an inline Journal raises its committed notification; Calendar starts status/day-journal refresh work without requiring its caller to await every refresh. Deactivation is responsible for canceling outstanding reads. Requiring this fixture to await them merely to avoid an invalid source lifetime would hide the ownership defect.

Original ownership pattern in all four Calendar read channels:

1. Each asynchronous load creates a local `using CancellationTokenSource` and publishes it in a field.
2. Replacement/deactivation increments the generation and requests cancellation through that shared field.
3. Completion clears the field only if its generation still matches; obsolete completion still disposes its local source.
4. Cancellation can still hold/reference that source while obsolete completion disposes it. A later/current cancellation call can then use a disposed source.

### Controlled regression and correction

`CalendarViewModelTests.CompletedLoadCannotDisposeCancellationStillOwnedByDeactivate` forces an ordering with a cancellation callback, `TaskCompletionSource` signals and a `ManualResetEventSlim` gate. No sleep or retry is used:

- Start a delayed month read and capture its token.
- Start deactivation and hold its cancellation callback in flight.
- Explicitly complete the obsolete read and await the load's completion/disposal.
- Call deactivation again while the first cancellation is still in flight.
- Release the callback in `finally`, await deactivation, verify canceled data did not publish, then activate again and verify a successful new load.

Against the **original production implementation**, this fails in **68 ms** with `ObjectDisposedException` at `CalendarViewModel.Cancel:434 → Deactivate:257` (`original-regression/`). The corrected implementation passes (`fixed-regression/`). This establishes a production cancellation-source ownership defect independently of the hosted exception; it does not reconstruct the unrecorded hosted field/interleaving or claim general view-model thread safety.

The four private Calendar source fields now use one private `ReadCancellation` owner. A load's `using` still owns completion. Each `Cancel` acquires an in-flight lease under a gate, delivers cancellation **outside** the gate so callbacks can complete/reenter, and releases its lease in `finally`. Completion marks the operation finished under the same gate; the underlying CTS is disposed only after completion **and** the last cancellation call returns. Late cancellation of already-completed work does nothing. A token is captured once before disposal. There is no ObjectDisposedException catch or unsynchronized check/use disposed flag. Existing generation guards, task completion, draft/no-write semantics and later activation are preserved.

## B. Notebook: continued investigation, no readiness correction

Light/1280/96 child `notebook-menus-20261104-104925-972cc7d4571d4cad986e167d85ba984d` and Dark/1280/96 child `notebook-menus-20261104-104932-2a2d82033b7d4390ad0179790bd64cf0` each fail at the same **DuplicateNoteButton** click on tested Menus.cs:127. Matching child TRX, logs and JSON retain correct cursor `(909,210)`, matching native HWND ownership and explicit hit ancestry through DuplicateNoteButton, while maintained hover/capture point to the ContextMenu. This extends the evidence in [CI #114](ci114-notebook-readiness.md); capture alone still does not justify changing the readiness predicate. These are two failed top-level cases with two matching failed children, not four top-level failures.

The missing bounded diagnostics are now implemented without duplicating the existing snapshot:

- `NotebookMouseMovementTrace` attaches a read-only hook to the target's actual HwndSource during each `MouseClick`. It observes `WM_MOUSEMOVE`, left down/up and capture-change messages, retaining their native client coordinates and read-back cursor.
- `PostProcessInput` observations retain processed preview/bubble mouse movement, capture and button events, source, timestamp and handled state.
- Rows include current managed position relative to the target, managed hover, capture owner and capture mode. A 64-row ring buffer per click bounds memory/output. It writes `menu-evidence/mouse-movement.log` on scope exit, including failed readiness before popup cleanup. It produces no runner heartbeat.
- Existing failure JSON adds `managedMousePositionInTarget`, `managedMousePositionInSourceRoot`, and `managedCaptureMode`. Internal WPF capture-mode reflection is diagnostic-only and best-effort; it is not a readiness condition.
- Hook handlers do not consume input or alter handled state. Collection never activates, moves the cursor, releases capture, pumps extra dispatcher work, retries clicks, or changes assertions/deadlines. Hook removal and file output are best-effort.

The existing controlled diagnostic popup probe also runs with the trace attached and checks that queued dispatcher work, cursor, capture and foreground remain unchanged. It validates the new snapshot fields. This is a read-only instrumentation probe, **not** a claimed reproduction of the hosted false-hover cause. No experiment clicking after failed readiness was performed, and no such bypass was added to acceptance tests. Native click and duplicate-command assertions remain in their original order.

## Verification

The original controlled regression failure and corrected pass are separate evidence. Final Release build: **0 warnings / 0 errors**. The focused run passed **83 top-level cases / 0 failed / 0 skipped**: Calendar month/day/filter/status/day-journal/SQLite lifecycle cases, all Journal draft-close cases, all four Notebook overflow parameters, tag-menu/capture regressions, diagnostic contracts, layout checks and the scheduling guard. Seven matching isolated child cases passed separately (4 overflow, 2 tags, 1 diagnostic popup); child evidence is not added to top-level totals. All four menu parameters passed locally with tracing. `movement-audit.json` confirms native movement, processed WPF movement and SubTree capture observations were written. This does not reproduce or correct the hosted false-hover state.

Commands from the repository root:

```powershell
dotnet build PersonalTradingJournal.sln -c Release --no-restore
dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~CompletedLoadCannotDisposeCancellationStillOwnedByDeactivate' --logger trx --results-directory TestResults/ci115/fixed-regression
$env:PTJ_TEST_RESULTS_DIRECTORY = Join-Path $PWD 'TestResults/ci115/focused'
dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~CalendarViewModelTests|FullyQualifiedName~CalendarDayDetailsTests|FullyQualifiedName~CalendarJournalTests|FullyQualifiedName~CalendarFilterTests|FullyQualifiedName~CalendarSqliteRefreshTests|FullyQualifiedName~CalendarDayJournalsTests|FullyQualifiedName~CalendarInlineJournalSqliteTests|FullyQualifiedName~JournalDraftCloseTests|FullyQualifiedName~OverflowTriggersToggleThroughNativeMouseAndOwnedKeyboard|FullyQualifiedName~TagMenuRoutedToggleDismissalAndCapturedOwnership|FullyQualifiedName~NotebookMouseReadinessEvidenceTests|FullyQualifiedName~NotebookMenuLayoutTests|FullyQualifiedName~ReleaseSchedulingTests' --logger trx --results-directory TestResults/ci115/focused
./scripts/Test-Release.ps1 -ResultsDirectory TestResults/ci115/complete
```

Calendar and Notebook outcomes must remain separate: a Calendar ownership correction does not establish a Notebook fix, and passing local native-menu reruns do not explain its hosted hover discrepancy.

The complete Release gate ran **once** after focused checks passed and exited **0**: **5,541 passed / 0 failed / 0 skipped**. Exclusive Desktop 145; parallel Domain 602, Application 1,050, Infrastructure 1,608, Desktop 2,136. The additional top-level case is the controlled Calendar ownership regression. `gate-audit.json` verifies 5,541 unique identities, zero duplicates, the regression exactly once and all four original draft-close variants passed. Nested child TRX files are excluded from those totals. Retained full run: `TestResults/ci115/complete/release-20261104-132939-d74c479bfa1743a6b8d4382ce0e09ff4/`.

Updated hosted acceptance is pending. The exact Calendar source/interleaving in the hosted optimized exception remains unrecorded; the controlled production defect and correction have direct failing/passing evidence. Notebook still needs a false-hover observation with the newly added movement/capture trace before a cause or interaction correction can be established.

Exact changed files:

```text
README.md
docs/ci115-calendar-and-notebook.md
docs/ci-menu-readiness.md
docs/ci114-notebook-readiness.md
src/PersonalTradingJournal.Desktop/ViewModels/Calendar/CalendarViewModel.cs
src/PersonalTradingJournal.Desktop/ViewModels/Calendar/CalendarViewModel.Journal.cs
src/PersonalTradingJournal.Desktop/ViewModels/Calendar/CalendarViewModel.DayJournals.cs
src/PersonalTradingJournal.Desktop/ViewModels/Calendar/CalendarViewModel.ReadCancellation.cs
tests/PersonalTradingJournal.Desktop.Tests/Calendar/CalendarViewModelTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookHeaderViewTests.Menus.cs
tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookMouseReadinessEvidence.cs
tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookMouseReadinessEvidenceTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookMouseMovementTrace.cs
```

Native branding manual acceptance remains pending. Branding/artwork, dispatch selection, 20-minute workflow budget, per-case watchdogs and scheduling contract are unchanged. No push, PR, merge or hosted rerun is performed.
