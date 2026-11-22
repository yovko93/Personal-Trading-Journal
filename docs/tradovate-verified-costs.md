# Tradovate verified costs

## October timestamp correction

The October exports were compared read-only against the earlier path in `9d985f9`. Performance parses `boughtTimestamp` and `soldTimestamp` as invariant `MM/dd/yyyy HH:mm:ss` wall times with unspecified kind. Fills uses the same parser for its display `Timestamp`, and separately parses `_timestamp` with its explicit `Z` and available fractions as zero-offset UTC. All 770 unique Fills IDs matched the Performance ID set and each matched display timestamp agreed to the second. Every display timestamp matched UTC+02:00, including 615 fills before the Sofia offset changed from UTC+03:00 to UTC+02:00. No machine-local offset is involved.

Cash History parses its display timestamp as an unspecified wall time and its Date as an explicit statement trading date. Cash History supplies transaction IDs and has no fill IDs. Its timestamps govern posting order, while the cost verification compares commission multisets and paired Gross by broker trading date and contract. Posting times and statement dates do not determine Trade execution times or Calendar attribution.

Verified preparation uses exact Fills UTC instants, checks that they preserve the reconstructed execution order, and projects the prepared opens and closes to New York. The cost reconciler checks that every allocation uses that exact UTC instant, including fractions. The store repeats verification at confirmation. Preview blocks incomplete or conflicting match/clock evidence. Performance-only mode names its Sofia assumption explicitly.

The [previous October check](#october-exports) recorded the cost fix and the timestamp discrepancy. The new probe reproduced **615 whole-second errors** with the earlier Performance normalization. The corrected path had **zero UTC mismatches** across **770 unique fills and 775 allocations**. The **157 Trade groups** kept their original Performance row membership. Four Trade closes moved to another Calendar day under the existing 18:00 New York cutoff. Gross was **2,648.00 USD**, commissions **1,277.12 USD**, Trade Net **1,370.88 USD**, and total cash movement **853.88 USD** after the **−517.00 USD** Manual Adjustment. Preview reached Verified and the probe declined confirmation with writes disabled. Local evidence is saved under `TestResults/TradovateOctoberUtc/preview-probe/timestamp-result.log`.

Verified preview checks the destination Account’s existing import identities before enabling confirmation. A timestamp conflict outside the same legacy whole second blocks the verified import as a whole. The user can compare the saved Trade’s executions with the matching Fills IDs in an isolated backup for separate review. This release includes no in-place timestamp repair. Enrichment for an otherwise eligible same-second legacy import keeps its saved precision and updates only unknown costs. The existing checks for edited executions and known costs still apply. Existing Trades with a genuine time shift keep their records, costs, descriptions and Calendar allocation until separately corrected.

The final Release build had zero warnings and errors. Focused tests passed **269/269** (84 Application, 152 Infrastructure, 33 Desktop). They covered cross-season timestamps and fractional seconds, Calendar at 17:59:59 and 18:00 New York time, rejected mixed clocks, exact ID reconciliation, verified replay, legacy conflicts and retained same-second timestamps on enrichment. Focused TRX files are under `TestResults/TradovateOctoberUtc/focused-final/`.

The complete local Release gate `release-20261122-130459-c86c0d010ca04eb18cc1cfb5298d950f` finished in **13m 53s**, exit **0**, with **5,624 passed, 0 failed and 1 skipped**. Exclusive WPF passed 151, Domain 602, Application 1,052, Infrastructure 1,664 and parallel Desktop 2,155. The only skip was the existing [Rules-view quarantine](rules-view-quarantine.md). Hosted CI and native interaction with the supplied files remain unverified.

<details>
<summary>Files changed for the timestamp correction</summary>

 - `README.md`
 - `docs/tradovate-csv-import.md`
 - `docs/tradovate-verified-costs.md`
 - `src/PersonalTradingJournal.Application/Imports/Tradovate/ITradovateImportStore.cs`
 - `src/PersonalTradingJournal.Application/Imports/Tradovate/ImportTradovateTradesUseCase.cs`
 - `src/PersonalTradingJournal.Application/Imports/Tradovate/TradovateCostReconciler.cs`
 - `src/PersonalTradingJournal.Application/Imports/Tradovate/TradovateImportPreparationResult.cs`
 - `src/PersonalTradingJournal.Application/Imports/Tradovate/TradovateImportPreparationService.cs`
 - `src/PersonalTradingJournal.Application/Imports/Tradovate/TradovateImportPreviewBuilder.cs`
 - `src/PersonalTradingJournal.Desktop/ViewModels/Import/ImportViewModel.Costs.cs`
 - `src/PersonalTradingJournal.Desktop/ViewModels/Import/ImportViewModel.cs`
 - `src/PersonalTradingJournal.Desktop/Views/Import/ImportView.xaml`
 - `src/PersonalTradingJournal.Infrastructure/Imports/Tradovate/TradovateImportStore.Costs.cs`
 - `src/PersonalTradingJournal.Infrastructure/Imports/Tradovate/TradovateImportStore.cs`
 - `tests/PersonalTradingJournal.Desktop.Tests/Import/TradovateBoundPreviewTests.cs`
 - `tests/PersonalTradingJournal.Desktop.Tests/Import/TradovateImportAcceptanceTests.cs`
 - `tests/PersonalTradingJournal.Infrastructure.Tests/Imports/Tradovate/TradovateVerifiedCostTests.cs`

</details>

## October exports

On November 22, the read-only import probe reproduced the selection failure on develop at `fd7f5316b1da6fbd48f849ed167f3f8847cccd10`. On CSV line 1024 the October 27 Manual Adjustment had an empty Contract. The old `TradovateCostCsvParser.ReadAsync` rejected every empty required cell, before checking the movement type. The source panel replaced its diagnostic with “Invalid CSV”. The Contract header is still required. Commission and Trade Paired still need a contract. Manual Adjustment requires an empty Contract and a whole-cent Delta, and it participates in the same Account, currency, period, transaction ID, chronology and running-balance checks.

| Read-only evidence | Rows | USD |
| --- | ---: | ---: |
| Performance matched Gross | 464 | 2,648.00 |
| Fills commissions, exact ID set match with Performance | 770 unique fills | 1,277.12 |
| Cash History Trade Paired | 459 | 2,648.00 |
| Cash History Commission | 770 | −1,277.12 |
| Verified Trade Net | 157 reconstructed Trades | **1,370.88** |
| Cash History Manual Adjustment, October 27 | 1 | **−517.00** |
| Total cash movement | 1,230 cash entries | **853.88** |

At that milestone, after the row could be parsed, the original Sofia display-clock validation independently blocked cost verification. All 770 Fills displayed UTC+02:00 to the second. That matched Sofia for 155 fills and differed by one hour for 615 before the autumn clock change. Verified costs accept the standard Sofia convention or, for an export spanning both offsets, a consistent UTC+02:00 display on every fill. Each Performance wall-clock timestamp must still match its Fills row. Mixed clocks fail. At that milestone, the Performance interpretation still gave a UTC instant one hour earlier than Fills UTC for 615 pre-change fills. The timestamp correction above supersedes that behavior for verified imports.

The real parsers, reconstructor, resolver, preparation and reconciler accepted October 1–30 with 775 allocations and exactly 1,277.12 USD in commissions. The probe used in-memory Account metadata and had no database or write services. The Desktop source-selection commands showed both files as pending, then Verified at PreviewReady. The October confirmation contained the same figures and was declined. The read-only ViewModel evidence is in `TestResults/TradovateOctober/preview-probe/result.log`. Accepted writes, enrichment and replay used synthetic CSVs only. Manual adjustments are reconciliation evidence and do not create a Trade, execution cost, payout or balance entry.

The Release build passed with zero warnings and errors. Filtered Tradovate tests passed **227/227** (76 Application, 138 Infrastructure, 13 Desktop), including compiled preview, signed adjustment and cash totals at confirmation, declined confirmation, enrichment, replay, unchanged Account balance treatment and unsupported-movement feedback. Focused TRX files are under `TestResults/TradovateOctober/focused/`.

The complete local Release gate `release-20261122-123555-caf51e4450c74ecaa8301a2b35d63a4e` completed in **14m 01s**, exit **0**, with **5,622 passed, 0 failed and 1 skipped**. Exclusive WPF passed 151, Domain 602, Application 1,052, Infrastructure 1,662 and parallel Desktop 2,155. The sole skip was the existing [Rules-view quarantine](rules-view-quarantine.md). The counts come from the five top-level TRX files. Native file-picker interaction and hosted CI for this worktree remain unverified.

Files changed for the October correction:

- `src/PersonalTradingJournal.Infrastructure/Imports/Tradovate/TradovateCostCsvParser.cs`
- `src/PersonalTradingJournal.Application/Imports/Tradovate/TradovateCostSources.cs`
- `src/PersonalTradingJournal.Application/Imports/Tradovate/TradovateCostReconciler.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Import/ImportViewModel.Costs.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Imports/Tradovate/TradovateVerifiedCostTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Import/TradovateBoundPreviewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Import/TradovateImportAcceptanceTests.cs`
- `README.md`, `docs/tradovate-csv-import.md`, `docs/tradovate-verified-costs.md`

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
- Fills require an active row, agreeing internal/display ID, Account, contract, side, whole quantity, price, UTC timestamp, verified display timestamp (see the October clock check above), trading date and nonnegative whole-cent commission. Every Performance fill must exist exactly once in Fills, with all matched quantity accounted for. Extra, missing and duplicate fills block verification, including identical duplicate Fills rows. A fill repeated across Performance matched rows is valid.
- Account external ID must exactly match every Fills/Cash row. USD Account, Cash and Instrument currencies must agree. Sources cannot silently select/create an Account. Unsupported currencies, mixed Accounts and missing external IDs block verified costs; no currency conversion occurs.
- For each unique fill, allocate its commission across reconstructed Trade allocations proportional to matched quantity. Compute decimal cent quotas, floor them, then distribute remaining cents in descending fractional-remainder order, breaking ties by immutable allocation index. This conserves every cent and is independent of CSV row order. It handles reversal fills shared by two Trades without duplicating their costs.
- Require exact daily/contract charge multisets, Performance Gross versus Cash Trade Paired totals, and total cash movement versus the Cash Delta sum (Trade Net plus verified Manual Adjustments). Also verify each candidate's Instrument-derived Gross against its partition of Performance evidence. Trade Paired is never imported as a second P/L source. Each Performance row belongs to one candidate.
- Cash `Commission` and `Trade Paired` reconcile the trading activity. Contractless `Manual Adjustment` entries within the Fills period reconcile separately. Unsupported movements, missing transactions, nonchronological rows and broken running balances block verification. Zero additional execution Fees is recorded only after both trading components reconcile. Performance alone never supplies known zero costs.

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

