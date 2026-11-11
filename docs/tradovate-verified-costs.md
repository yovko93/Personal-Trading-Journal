# Tradovate verified costs

## Source reconciliation — 2026-11-11

Read-only reconciliation of the supplied Performance, Fills and Cash History exports was reproduced on `develop`, based on `029ed27857823b6f462ce5045790c87e6e03382d` plus the local implementation. No production journal was opened. The private CSVs remain outside the repository and are not test fixtures.

| Evidence | Observed result |
| --- | --- |
| Performance | 194 matched rows, Gross **1,783.50 USD** |
| Fills | 319 rows and 319 unique IDs, exact set equality with Performance buy/sell IDs |
| Commission | Fills **588.64 USD**; 319 Cash History Commission entries total **−588.64 USD** |
| Cash Trade Paired | 193 entries total **1,783.50 USD**, used only for reconciliation |
| Cash net change | **1,194.86 USD** |
| Scope | One source Account, USD, trading dates November 2–6, 2026 |
| Actual importer | 60 reconstructed Trade candidates, 319 allocations, costs **588.64**, Net **1,194.86 USD** |

The real parser, reconstructor, instrument resolver, preparation and cost reconciler were exercised by an ignored local probe at `TestResults/tradovate-readonly/`. It constructed no database or write services. Its log is `reconciliation.log`. Synthetic in-memory Account metadata was used only to check exact source identity, not to choose or inspect a real destination Account.

One Cash commission posting is one second later than its fill's display timestamp. Therefore Cash History is not joined to Fills by an invented exact timestamp identity. Its exact charge amounts and occurrence counts must match per contract/trading date. Daily counts/totals are 77/118.56, 147/317.20, 4/6.24 and 91/146.64 USD on November 2, 3, 5 and 6 respectively. Cash posting order and running balances must also remain continuous.

## Reconciliation and allocation contract

- The main Performance schema, reconstruction, original fill IDs, UTC execution timestamps and trade grouping remain unchanged. Supporting CSVs use the shared bounded UTF-8 CSV reader and file/record/field limits.
- Fills require an active row, agreeing internal/display ID, Account, contract, side, whole quantity, price, UTC timestamp, Sofia display timestamp, trading date and nonnegative whole-cent commission. Every Performance fill must exist exactly once in Fills, with all matched quantity accounted for. Extra, missing and duplicate fills block verification, including identical duplicate Fills rows. A fill repeated across Performance matched rows is valid.
- Account external ID must exactly match every Fills/Cash row. USD Account, Cash and Instrument currencies must agree. Sources cannot silently select/create an Account. Unsupported currencies, mixed Accounts and missing external IDs block verified costs; no currency conversion occurs.
- For each unique fill, allocate its commission across reconstructed Trade allocations proportional to matched quantity. Compute decimal cent quotas, floor them, then distribute remaining cents in descending fractional-remainder order, breaking ties by immutable allocation index. This conserves every cent and is independent of CSV row order. It handles reversal fills shared by two Trades without duplicating their costs.
- Require exact daily/contract charge multisets, Performance Gross versus Cash Trade Paired totals, and total Net versus Cash Delta sum. Also verify each candidate's Instrument-derived Gross against its partition of Performance evidence. Trade Paired is never imported as a second P/L source. Each Performance row belongs to one candidate.
- Only Cash `Commission` and `Trade Paired` movements are supported in verified mode. Other charges, transfers, missing transactions, nonchronological rows or broken running balances block it. Zero additional execution Fees is recorded only after this complete supported-scope cash reconciliation establishes Gross minus commissions equals Net. Performance alone never supplies known zero costs.

## Confirmation and existing imports

The source panel offers separate Fills and Cash History selectors. Building the preview exposes verified period, currency, Gross, costs and Net. The confirmation repeats those totals and explains existing-Trade handling. Selecting only one supporting file or a malformed replacement blocks confirmation rather than reusing stale verified evidence. Changing Account invalidates the preview. Clearing supporting evidence is an explicit **Use Performance only** action.

The store revalidates the Account identity/currency and all reconciliation within the existing atomic import transaction. Existing immutable allocation identities must match completely. An enrichment additionally checks persisted execution IDs, Account, pricing currency, quantities, prices, sides, symbols, timestamps and actual Gross. Legacy records without immutable source facts cannot be enriched automatically.

Only executions with both cost fields unknown are candidates for enrichment, and the Trade must still carry its original import update timestamp. This conservative edit guard also prevents silently filling costs that a user cleared back to unknown. It may require separate review after unrelated Trade edits. Any conflicting known cost, including manually entered zero, blocks the entire request. Already-equal known costs are left untouched and replay is a no-op. There are no destructive replacements or partially committed enrichments.

Costs are persisted on existing execution records and the existing Trade browse projection is rebuilt in the same transaction. Original Trade/execution IDs, pricing snapshots, source fill identities, quantities and prices remain intact. No migration or alternate cost store is introduced. Existing backup/restore/portable export already includes execution Commission/Fees. The normal import-committed event fires for cost-only writes, refreshing Accounts, Dashboard, Breakdown, Calendar and dependent review evidence; historical saved reviews are not rewritten. Replay emits no write notification.

Only reconciled Trades gain known costs. Unrelated Trades remain unchanged, so an Account can still be Estimated when other Trades have unknown costs; payouts and the existing balance calculation continue to apply.

## Verification

Final Release build passed with **zero warnings and errors** (`TestResults/tradovate-cost-final-build-verified.log`). Feature-focused checks passed **268 cases**: Application 76, Infrastructure 128 and Desktop 64, with zero failures/skips. Final Desktop results, including the failed-source replacement guard, are in `TestResults/tradovate-cost-focused/desktop-final-guard.trx`. New tests use synthetic Account/fill/transaction IDs and amounts, not copied user rows.

The initial restricted-runner complete gate, `release-20261110-223347-a6dbac3d28ec48be8ccb8b5f71363c32`, completed with **5,530 passed / 70 failed / 1 skipped**, exit 1. Exclusive WPF was 118/33; parallel Desktop was 2,113/36/1; Infrastructure was 1,645/1; Application 1,052 and Domain 602 passed. Native failures included `SetCursorPos` returning false after correct HWND ownership and input/foreground state unavailable to WPF. The backup symbolic-link check explicitly reported a missing Windows privilege; credential-protection checks also failed. The original log and all child artifacts are retained, plus a compact list of all 70 top-level failures at `TestResults/tradovate-cost-restricted-failures.json`. These are not 70 demonstrated importer defects, nor are their individual causes claimed fixed.

Representative checks outside the sandbox then passed: **78 Desktop** cases (the 64 import checks plus Instruments native-input and credential tests) and **129 Infrastructure** cases (128 Tradovate checks plus the symbolic-link test). This supported a final unrestricted complete gate instead of repeatedly rerunning the same restricted environment. The existing Rules quarantine is unchanged; no additional skips or test-behavior changes were introduced.

The final `pwsh -File scripts/Test-Release.ps1` run, **`release-20261111-193839-784bb9e86f4f43b3a12c5cb3f4b89c9d`**, completed with **5,600 passed / 0 failed / 1 skipped**, **exit 0**. The five top-level TRX files were independently counted; isolated child cases are evidence and are not added again. This is 31 additional cases over the preceding 5,569-pass gate: 30 Infrastructure cost regressions and one Desktop confirmation/enrichment regression. Existing bound preview coverage was extended within its existing case.

| Stage/project | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Exclusive WPF | 151 | 0 | 0 |
| Domain | 602 | 0 | 0 |
| Application | 1,052 | 0 | 0 |
| Infrastructure | 1,646 | 0 | 0 |
| Parallel Desktop | 2,149 | 0 | 1 |

The sole skip is `PersonalTradingJournal.Desktop.Tests.Rules.RulesViewTests.CompiledThemesNarrowHighDpiBindingsValidationHistoryAndWindowClose`, documented in the unchanged [Rules quarantine](rules-view-quarantine.md). The final console log is `TestResults/tradovate-cost-release-unrestricted.log`; the earlier restricted run remains at `TestResults/tradovate-cost-release.log`. No assertion, deadline, concurrency setting, workflow budget or CI failure propagation was weakened. `git diff --check`, new-file whitespace checks and affected documentation-link checks passed.

Coverage includes repeated fill IDs across a reversal, equal and unequal quantity allocation, cent ties and zero costs, missing/extra/duplicate fills, partial matches, malformed values, inactive fills, Account/currency/period mismatches, cash fee/Gross/balance discrepancies, duplicate/missing cash entries, transfers, timestamp conflicts, new imports, cost-only updates, idempotent replay, manual edits and cleared costs, preserved IDs/Gross, balance/analytics refresh, confirmation decline, and failed source replacement. Bound WPF tests exercise the actual preview and dialog totals in Light/Dark with synthetic storage. They are automated checks, not manual native acceptance.

An early new Desktop acceptance test failed while deleting its synthetic database because its provider/pool cleanup ordering was incorrect. The fixture now disposes its provider before clearing the matching SQLite pool and deleting its own temporary directory. This was not a production database failure.

A later build attempt encountered an assembly-copy lock because a test was started before the build finished. After that test exited, the sequential rebuild succeeded and the final 64-case Desktop check passed. The failed attempt remains at `TestResults/tradovate-cost-final-build.log`; it is not the final build result.

Hosted CI, manual file-picker/keyboard review and native visual acceptance remain pending. Verification covers consistency of supplied exports in the supported scope, not proof of complete broker history outside that period. Sources with other cash movement types require a separately supported reconciliation contract.

## Changed files

The final task report and the list below identify the implementation, UI, tests and documentation changed by this task.

- `docs/tradovate-csv-import.md`
- `docs/tradovate-verified-costs.md`
- `README.md`
- `src/PersonalTradingJournal.Application/Imports/Tradovate/ImportTradovateTradesUseCase.cs`
- `src/PersonalTradingJournal.Application/Imports/Tradovate/TradovateCostReconciler.cs`
- `src/PersonalTradingJournal.Application/Imports/Tradovate/TradovateCostSources.cs`
- `src/PersonalTradingJournal.Application/Imports/Tradovate/TradovateImportRequest.cs`
- `src/PersonalTradingJournal.Application/Imports/Tradovate/TradovateImportResult.cs`
- `src/PersonalTradingJournal.Desktop/App.xaml.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Import/ImportViewModel.Costs.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Import/ImportViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/Import/ImportView.xaml`
- `src/PersonalTradingJournal.Infrastructure/Imports/Tradovate/TradovateCostCsvParser.cs`
- `src/PersonalTradingJournal.Infrastructure/Imports/Tradovate/TradovateImportStore.Costs.cs`
- `src/PersonalTradingJournal.Infrastructure/Imports/Tradovate/TradovateImportStore.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Import/TradovateBoundPreviewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Import/TradovateImportAcceptanceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Imports/Tradovate/TradovateVerifiedCostTests.cs`

