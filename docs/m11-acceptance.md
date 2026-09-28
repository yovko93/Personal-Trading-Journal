# M11 Topstep acceptance — Desktop routing correction

Review date: 2026-09-29. Base revision: `bdd7ebc0a6d0c64cde586bb45e09271659322122` on `develop`; checkout was clean at the start (the prior 24-file M11.6 work had been committed). The changes described here are uncommitted. No commit/push or real-journal access was performed.

## Root cause and binary provenance

The baseline `ImportViewModel.SelectCsvAsync` unconditionally invoked `ITradovateCsvParser.ParseAsync`. Its build guard required Tradovate reconstruction and its confirmation command invoked only `ImportTradovateTradesUseCase`. `App.xaml.cs` registered Topstep confirmation but neither injected its preview/confirmation path into the Import ViewModel nor presented review controls. `ImportView.xaml` also had a fixed Tradovate-only description. This explains the wrong-provider header errors in current source; changing the customer's CSV or weakening either parser is not a fix.

No journal process was running at audit start, including an elevated process-path check. The executable used for the earlier screenshots therefore **cannot be identified retrospectively**; a stale published/Debug binary for those screenshots is unverified, not ruled out by assumption. The source-level routing defect exists independently of that uncertainty.

The acceptance launch used the actual production executable:

```text
C:\Users\Yovko\source\repos\Personal-Trading-Journal\src\PersonalTradingJournal.Desktop\bin\Release\net10.0-windows\PersonalTradingJournal.Desktop.exe
```

Observed version: `1.0.0.0`; product/informational version `1.0.0+bdd7ebc0a6d0c64cde586bb45e09271659322122`. This version identifies the base commit, **not a clean binary**: the executable was built with the uncommitted routing changes. Process command lines were verified to include `--isolated-data-root` pointing under ignored `.tools/m11-routing-check/isolated-20260929-a`, whose `PersonalTradingJournal/journal.db` was seeded with synthetic Topstep/Tradovate USD Accounts and no Trades/Instruments. No special test App implementation replaced production startup.

The final Release binary was also launched once more against that same isolated root after the code/tests were finalized (startup-only, no renewed interactive claim). Its Desktop DLL SHA-256 was `434F49E5C1918432B59DF3F792334DA739F4B4366E7A135D60BCC89DE281ECEA`; the informational version remained the base commit plus uncommitted changes.

Startup/migration completed and the Dashboard accessibility tree was readable. The first capture failed with `FrameArrived timed out`; clicking Import failed with `coordinate input geometry is unavailable`. Refreshing window selection and activating/capturing again failed with `window capture timed out`. The helper was not used further to invent an interactive result. The isolated acceptance processes were stopped after verification.

## Acceptance matrix

Automated observations below are **Desktop ViewModel command + real pipeline/SQLite checks**, not a person or automation navigating the rendered Import page.

| Gate | Automated evidence | Interactive status |
| --- | --- | --- |
| A: supplied Topstep file/account → preview | **Passed:** 25 accepted, 0 rejected, 25 closed-row candidates, four reconstruction warnings, one proposed MNQ; confirmation disabled before review; no Tradovate header errors. | **Unverified** — Import navigation could not be operated. |
| B: cancel then explicitly confirm | **Passed:** cancelled final dialog retained preview and left zero Trades/ledger/Instruments; reviewed confirmation imported 25 Trades, 50 derived executions, one Instrument. | **Unverified**. |
| B: stored economics | **Passed:** fresh persisted aggregates Gross **1,241.00**, Fees **51.84**, Commissions **36.00**, Net **1,153.16 USD**. | **Unverified** presentation; amounts verified from SQLite/Domain. |
| C: fresh review and replay | **Passed:** `NoChanges`, 25 skipped, zero new Instruments; completed preview cleared and resubmit disabled. | **Unverified**. |
| D: same-session Tradovate | **Passed:** small valid CSV routed to Tradovate, one candidate, PreviewReady and confirmation available; existing Tradovate import/replay tests retained. | **Unverified**. |
| E: unknown/mixed schema and provider switch | **Passed:** one format diagnostic, no wrong-parser cascade; new selection clears old account/review/outcome and can recover. | **Unverified**. |
| E: stale source/account/Instrument | **Passed:** SOURCE_CHANGED / REFERENCE_DATA_CHANGED retain recovery context, disable old review, require fresh preview/acknowledgments and write nothing. | **Unverified**. |
| E: blocked Instrument/incompatible Account | **Passed:** ambiguous/wrong pricing/wrong provider produce blocked previews; checked boxes cannot override them. | **Unverified**. |
| Cancellation/concurrent submission | **Passed:** Cancel operation reaches the active token; failure/cancel retain retryable preview; WPF CanExecute disables repeated submission and store call count remains one. M11.6 tests protect concurrent transactions/rollback. | **Unverified** button interaction. |

The supplied-file command-path run used separate isolated migrated databases under ignored `.tools/m11-routing-check/isolated-20260929-b` and `isolated-20260929-c` (repeat with the final code). Customer rows/IDs were not printed or added to repository fixtures. Programmatically accepting checkboxes in this helper is synthetic test consent, not a claim of human review. Both runs confirmed 25 Topstep ledger rows and zero Tradovate ledger rows after the same-session Tradovate preview (which was not imported).

## Verification results and changed files

- Release build: **passed, 0 warnings / 0 errors**.
- Full Release suite rerun: **1,978 passed, 0 failed/skipped** — Domain 400, Application 405, Infrastructure 651, Desktop 522.
- Focused Desktop routing/compiled-XAML tests: **20 passed**. Focused Infrastructure header-routing tests: **12 passed**; the full suite includes both sets.
- The compiled-WPF test loads the actual Import view and production theme resources on an STA thread, materializes the unchecked review checkboxes, validates accessible names and rendered economics text. It does not instantiate production `App`, open real storage, or substitute for interactive acceptance.
- EF `has-pending-model-changes` (Release): **none**. Existing migration tests and isolated migrated database checks pass. No schema/migration changes are part of this correction.
- `git diff --check`: passed; newly added files were also checked separately.
- The first full run had one disposed-SQLite-connection failure in the unchanged setup-classification test `TradeMutationStoreTests.SaveAsyncPersistsSetupClassificationWithoutReplacingTradeState`. Both cases passed in a focused rerun, and the complete suite then passed without changing that persistence code. The initial test failure is not represented as a clean first run.

Changed files (12 modified, 11 added; all uncommitted):

```text
README.md
docs/topstep-csv-import.md
docs/m11-acceptance.md (new)
src/PersonalTradingJournal.Application/Imports/IImportCsvFormatDetector.cs (new)
src/PersonalTradingJournal.Infrastructure/Imports/Csv/ImportCsvFormatDetector.cs (new)
src/PersonalTradingJournal.Desktop/App.xaml.cs
src/PersonalTradingJournal.Desktop/Imports/ITradovateCsvFilePicker.cs
src/PersonalTradingJournal.Desktop/Imports/WpfTradovateCsvFilePicker.cs
src/PersonalTradingJournal.Desktop/Imports/DesktopApplicationPaths.cs (new)
src/PersonalTradingJournal.Desktop/Imports/ImportServiceCollectionExtensions.cs (new)
src/PersonalTradingJournal.Desktop/ViewModels/Import/ImportViewModel.cs
src/PersonalTradingJournal.Desktop/ViewModels/Import/ImportViewModel.Topstep.cs (new)
src/PersonalTradingJournal.Desktop/Views/Import/ImportView.xaml
src/PersonalTradingJournal.Desktop/Views/Import/TopstepReviewView.xaml (new)
src/PersonalTradingJournal.Desktop/Views/Import/TopstepReviewView.xaml.cs (new)
tests/PersonalTradingJournal.Desktop.Tests/Import/ImportViewModelTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Import/ImportViewXamlTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Import/TradovateImportAcceptanceTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Import/TradovateTimestampWorkflowTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Import/TopstepDesktopRoutingTests.cs (new)
tests/PersonalTradingJournal.Desktop.Tests/Navigation/MainWindowViewModelTests.cs
tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/TradovateOnlyFormatDetector.cs (new)
tests/PersonalTradingJournal.Infrastructure.Tests/Imports/ImportCsvFormatDetectorTests.cs (new)
```

The real parsers, Domain economics and M11.6 transaction/deduplication implementation are unchanged. The new detector is a routing boundary, not relaxed validation. Legacy parser-double unit tests receive an explicit Tradovate routing double; production-header routing is exercised separately with real CSV and both pipelines.

## Manual completion checklist

Use the production Release executable with `--isolated-data-root` and a **fresh separate test directory**, never the normal journal or a junction to it. Seed/create synthetic Topstep USD and Tradovate USD accounts there; leave MNQ absent to exercise explicit proposal approval.

1. Open Import → Select CSV → supplied Topstep export. Expect format Topstep, **25 valid / 0 rejected**, 25 closed-row candidates, no Tradovate missing headers. Choose the synthetic Topstep account → Build Preview.
2. Inspect four grouping/boundary warnings plus proposal-related review, exact MNQ specifications and totals **1,241.00 / 51.84 / 36.00 / 1,153.16 USD**. Import Trades must stay disabled until every warning and separate proposal approval is checked. Confirm closed-row labels and source lines, not broker-position/fill claims.
3. Choose Import Trades → Cancel. Verify preview/decisions remain and the isolated database has no Trades. Choose Import Trades again → accept. Expect Imported 25 and one Instrument; intermediate sections hide, Confirm import retains counts, old preview cannot resubmit.
4. Re-select the same file/account, rebuild and explicitly review the now-existing Instrument. Confirm → expect NoChanges with 25 skipped and no new Instrument.
5. In the same application session select the small valid Tradovate CSV. Account/review/outcome must reset; select the Tradovate account and verify its existing preview behavior and unknown-cost explanation.
6. Try a synthetic unknown/mixed header → one format diagnostic. Recover by selecting a valid file. Try a wrong-provider account or ambiguous Instrument catalog → blocked preview. Change an isolated source/reference after preview → confirmation blocks with a clear rebuild/review path.
7. Check Cancel operation during work, disabled repeat submission, keyboard access to every review box, readable wrapping at a narrow window, and retained Trades/Instruments refresh after commit.

M11 must **not** be marked interactively accepted until these remaining checks pass. No Desktop editor for arbitrary non-MNQ verification attestations was added; such references remain blocked rather than guessed. Domain economics, both parsers' validation, M11.6 transactions/deduplication and Tradovate rules are unchanged.
