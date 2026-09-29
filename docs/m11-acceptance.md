# M11.7 acceptance — explicit source and simplified TopstepX import

Review date: 2026-09-29. Actual starting HEAD: `dccce62c479794ef48cac0c6c3d6c80794905d4b` on `develop`; the checkout was clean. The earlier routing work was already committed. This request supersedes automatic source selection and warning-checkbox consent. No commit, push, branch change or real-journal access was performed.

## Implemented behavior

- Exactly two source choices: **Tradovate** and **TopstepX**, with neither selected initially. Select CSV is disabled until a source is chosen.
- Header validation must match the selected source. Wrong source yields one `CSV_SOURCE_MISMATCH`; unknown/mixed/malformed schemas yield one `CSV_FORMAT_UNSUPPORTED`. Neither case reaches the other parser.
- User-confirmed rule: one TopstepX Trades row is one Trade. The 25-row sample yields 25 candidates without grouping/source-completeness checkboxes or warning gates.
- Snapshot-bound `TopstepImportConfirmation` replaces the warning-review contract. An ordinary final confirmation explicitly approves its displayed Instrument specifications. No separate approval checkbox remains.
- Source changes clear file/account/preview/diagnostics/outcome; file and account changes invalidate prior confirmation. Source/reference/economic revalidation remains transactional and cannot be overridden.
- Completed outcomes clear active preview and retain counts under Confirm import. Command-availability notifications now run after releasing the submission guard, so Select CSV and stale-preview Build Preview recovery actually re-enable in WPF bindings.

Internal provider identity remains Topstep for compatible accounts and durable deduplication; the source's visible name is TopstepX. No parser, Domain economics, stored-execution, schema, migration, package or Tradovate import-rule changes were needed. Technical row provenance remains distinct from broker-fill reconstruction.

## Acceptance matrix

**Passed** below denotes automated evidence, not interactive UI acceptance.

| Scenario | Status | Evidence |
| --- | --- | --- |
| Required source and exact visible names | Passed | Command test starts with no source and proves the picker is not called; compiled WPF view materializes exactly Tradovate/TopstepX. |
| Wrong format in either direction | Passed | Real Desktop command tests return one mismatch, no parser diagnostics/candidates/writes, then recover with the selected format. |
| Unknown schema | Passed | One format message, cleared prior state and successful subsequent selection. |
| Supplied TopstepX preview | Passed | 25 accepted, 0 rejected, 25 candidates; zero displayed grouping diagnostics; normal confirmation available with one proposed MNQ. |
| No review checkboxes | Passed | Compiled-WPF STA test loads production resources and asserts no CheckBox controls, while rendering economics. |
| Cancel then confirm proposal | Passed | Cancel leaves zero Trades/ledger/Instruments; exact proposal specs appear in the final request; affirmative confirmation imports 25 Trades, 50 derived executions and one Instrument. |
| Persisted economics | Passed | Fresh Domain aggregates from isolated SQLite: Gross **1,241.00**, Fees **51.84**, Commissions **36.00**, Net **1,153.16 USD**. |
| Exact duplicate replay | Passed | Fresh preview against existing MNQ → NoChanges, 25 skipped, no extra Instrument; old preview cannot resubmit. |
| Bad economics, incompatible account, ambiguous/wrong Instrument | Passed | Existing real-stage/SQLite and Desktop tests still block without writes. |
| Stale source/account/Instrument and invalid proposal approval | Passed | Exact fingerprint/specification checks retained before and within the transaction; stale UI disables confirmation and enables Build Preview recovery. |
| Duplicate content conflicts, mixed overlap, cancellation, concurrent imports, rollback | Passed | Existing isolated SQLite transaction tests pass, including injected mid-import exception/cancellation rollback. |
| Source switching from preview, blocked or completed state | Passed | Dedicated state tests prove file/account/diagnostics/outcome/candidates are cleared; switching back cannot reuse old confirmation. |
| Tradovate regression | Passed | Supplied small fixture builds one ready candidate after an explicit same-session source switch; existing import/replay tests pass. |
| Interactive source selector / Import page / final dialog / replay | Unverified | Windows helper could observe Dashboard but could not click Import or capture the window; no interactive import was performed. |

The supplied-file runs used the ignored local helper with the **real Desktop ViewModel commands and production services**, substituting only file picker/final-dialog decisions. Storage was fresh migrated databases under `.tools/m11-routing-check/isolated-20260929-m117/PersonalTradingJournal/` and `isolated-20260929-m117-final/PersonalTradingJournal/` (final-code rerun). Both ended with 25 Trades, 50 executions, 25 Topstep ledger rows, one Instrument and zero Tradovate ledger rows. The Tradovate step was preview-only. This is synthetic automated consent, not a claim of human confirmation.

## Interactive attempt and binary provenance

The actual production Release executable was launched with `--isolated-data-root`:

```text
C:\Users\Yovko\source\repos\Personal-Trading-Journal\src\PersonalTradingJournal.Desktop\bin\Release\net10.0-windows\PersonalTradingJournal.Desktop.exe
```

It used only `.tools/m11-routing-check/isolated-20260929-m117-ui`, a separate migrated database seeded with synthetic Topstep/Tradovate USD accounts and no Trades/Instruments. Executable path and command-line isolation were checked. File version: `1.0.0.0`; product version: `1.0.0+dccce62c479794ef48cac0c6c3d6c80794905d4b`. This identifies the base commit plus the uncommitted build, not a clean committed binary.

The sandbox launch was not discoverable by Windows automation. After an approved isolated relaunch, the Dashboard accessibility tree was readable. Clicking Import failed with **coordinate input geometry is unavailable**. The prescribed fresh-window/activation/capture retry failed with **FrameArrived timed out: timed out waiting on channel**. Automation stopped, and only the path/argument-verified isolated process was terminated. No interactive acceptance is claimed. Subsequent final command-notification hardening was verified by tests/build, not another interactive run.

## Verification

- Release build (including restore): **passed, zero warnings/errors**.
- Full suite: **1,984 passed**, zero failures/skips — Domain **400**, Application **405**, Infrastructure **651**, Desktop **528**.
- Focused Topstep/Tradovate/import UI tests: **460 passed** (169 Application, 231 Infrastructure, 60 Desktop); `git diff --check` passed.
- No new schema/migration; all existing migration and isolated database tests passed.
- No customer CSV/IDs were added to tracked fixtures or documentation.

## Changed files

New:

```text
src/PersonalTradingJournal.Desktop/ViewModels/Import/ImportSourceOption.cs
```

Modified:

```text
README.md
docs/topstep-csv-import.md
docs/m11-acceptance.md
src/PersonalTradingJournal.Application/Imports/Topstep/ImportTopstepTradesUseCase.cs
src/PersonalTradingJournal.Application/Imports/Topstep/TopstepImportContracts.cs
src/PersonalTradingJournal.Application/Imports/Topstep/TopstepImportPreview.cs
src/PersonalTradingJournal.Application/Imports/Topstep/TopstepImportPreviewBuilder.cs
src/PersonalTradingJournal.Desktop/Imports/WpfTradovateCsvFilePicker.cs
src/PersonalTradingJournal.Desktop/ViewModels/Import/ImportViewModel.cs
src/PersonalTradingJournal.Desktop/ViewModels/Import/ImportViewModel.Topstep.cs
src/PersonalTradingJournal.Desktop/Views/Import/ImportView.xaml
src/PersonalTradingJournal.Desktop/Views/Import/TopstepReviewView.xaml
src/PersonalTradingJournal.Infrastructure/Imports/Topstep/TopstepImportStore.cs
tests/PersonalTradingJournal.Application.Tests/Imports/Topstep/TopstepImportPreviewBuilderTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Import/ImportViewModelTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Import/ImportViewXamlTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Import/TopstepDesktopRoutingTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Import/TradovateImportAcceptanceTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Import/TradovateTimestampWorkflowTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Navigation/MainWindowViewModelTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Imports/Topstep/TopstepImportStoreTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Imports/Topstep/TopstepPreviewPipelineTests.cs
```

Ignored acceptance helper/data under `.tools/` were reused locally and are not Git deliverables. Initial status was clean; final status is 22 modified tracked files and one new untracked source file, all uncommitted.

## Remaining manual checks

Use the Release executable with an explicit fresh isolated root, never the real journal or an alias/junction to it.

1. Open Import. Select CSV must be disabled; source choices must read Tradovate and TopstepX.
2. Choose TopstepX, select the supplied CSV and a synthetic Topstep USD account, then Build Preview. Expect 25 valid/25 Trades, concise individual-row text and no warning checkboxes.
3. Inspect economics and proposed MNQ; Import Trades opens the exact-specification dialog. Cancel (no writes), then accept: Imported 25, totals **1,241.00 / 51.84 / 36.00 / 1,153.16 USD**.
4. Re-select/rebuild/confirm: NoChanges, 25 skipped. Select CSV must be usable after completion; intermediate sections are hidden.
5. Switch to Tradovate: previous state clears. Its valid CSV works; selecting the TopstepX file while Tradovate is chosen gives one mismatch instead of switching source.
6. Try a wrong-provider account or stale isolated source/reference; verify clear blocking/rebuild recovery, enabled recovery buttons, cancellation and keyboard access.

Implementation and automated acceptance pass. **Interactive acceptance remains open**. Independent non-MNQ verification still has no Desktop editor and must not be guessed.

Suggested manual commit message: `feat: simplify TopstepX import with explicit source selection`
