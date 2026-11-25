# Tradovate verified costs

## Explicit timestamp repair

The maintenance workflow in `scripts/TradovateTimestampRepair` repairs a reviewed Performance-only import when the original Europe/Sofia interpretation conflicts with a complete Fills export whose display timestamps consistently use UTC+02:00. The baseline identifies every execution in the selected source scope, including whole-second records that already satisfy verified import. It carries the Account ID, external Account ID, currency, exact Fills SHA-256 and the diagnosed Trade/execution/Fill/allocation identities and source facts. Keep this baseline and all private reports outside source control.

Build the tool and produce a dry run against a consistent SQLite snapshot.

```powershell
dotnet build scripts/TradovateTimestampRepair -c Release
dotnet run --project scripts/TradovateTimestampRepair -c Release --no-build -- dry-run <snapshot.db> <baseline.json> <Fills.csv> <report.json>
```

The command opens its database in SQLite ReadOnly mode. It compares every baseline timestamp, source identity, quantity, price, side, symbol, Account and currency. Missing or duplicate Fill IDs, changed source bytes, changed membership, partial repair and unexplained timestamp differences abort the operation. A saved whole-second timestamp in the accepted group must stay exact. The report lists every proposed table, row key, field and before/after UTC instant, together with source, scope and current-state hashes.

The explicit Apply mode requires the previously reviewed report and its SHA-256. Obtain separate authorization before running it on a user journal.

```powershell
dotnet run --project scripts/TradovateTimestampRepair -c Release --no-build -- apply <journal.db> <baseline.json> <Fills.csv> <approved-report.json> <report-sha256> <new-backup.ptjbackup> <new-result.json>
```

Apply first rebuilds the diff and checks the approved state. It creates and verifies a complete backup through the existing archive service, including original screenshot and Setup-image files. It then revalidates inside the transaction. SQLite rejects promotion of a stale read snapshot if another writer commits. A changed source, database state or approved diff blocks the write. Keep the backup, baseline, approved report and result as the recovery and audit record. Existing output and backup paths are never overwritten. The app should be closed during any separately approved maintenance run, and reopened afterward so views and analytics reload.

The transaction changes `TradeExecutions.ExecutedAtUtc`, `TradovateImportedExecutions.SourceFillExecutedAtUtc` and necessary `TradeBrowse.OpenedAtUtc`/`ClosedAtUtc` projections. The Trade record has no independent stored open/close fields. Calendar membership and New York display dates are derived from execution instants through the existing timezone and 18:00 trading-day policy. No separate Calendar date column needs rewriting. The repair validates that Gross, Net, costs and position state stay equal.

The transaction retains Trade/execution IDs, Description, Setup classification, Mistake notes and references, screenshot ownership, pricing snapshots, sequence and import identities. File operations are limited to the complete backup. Saved Journal dates and immutable review snapshots retain their historical content. The repair keeps `ImportedAtUtc` and `UpdatedAtUtc` intact. Cost enrichment remains a separate import operation, subject to the existing safeguards for edited Trades and known or manually edited costs. Normal import continues to reject a one-hour mismatch. A repeated repair recognizes the complete corrected state and makes zero changes.

### Timestamp repair verification

Verified on develop at `1874428da3c4c7b2a579ce22d60a9eab534ae624` plus the working-tree changes. The Release solution build passed with zero warnings and errors in 23.72s. The maintenance command build passed with zero warnings and errors in 2.05s. All 169 focused Tradovate infrastructure checks passed, including 17 repair cases. They cover rejected source, duplicate identities, stale timestamps and metadata, partial state, altered approval, backup failure, changes during backup, transactional rollback, complete-backup contents, metadata and screenshot preservation, accepted whole-second records, replay and New York Calendar movement.

A fresh SQLite backup snapshot of the local journal passed `quick_check`. Only the dry-run command was run against it, using the independently retained diagnosed baseline and exact October Fills bytes. It proposes 618 `TradeExecutions.ExecutedAtUtc` changes, 618 matching import-evidence changes and 256 TradeBrowse boundary changes across 128 Trades. All 157 accepted whole-second execution records remain outside the diff. Four Trades move to the next New York Calendar trading date. A follow-up read confirmed that all 775 snapshot execution and evidence timestamps still matched the original baseline.

Private review artifacts are under `TestResults/tradovate-time-repair/review/`, which is ignored by Git. `proposed-field-changes.csv` lists every proposed table, row, field, before/after instant and seconds difference with its Trade and Fill identities. `october-dry-run.json` is the machine-readable approval diff. `diagnosed-baseline.json` is the independent baseline. `calendar-date-effects.csv` lists all affected closing dates, and `review-summary.json` includes the source and approval-report hashes. The Fills SHA-256 is `2a3f0faabfb68031d2bc01e8c267ef819df7fdb4d9f85a28eeb3e5b0e5e492a6`. The reviewed report SHA-256 is `75398b23adb0e3ed867404295952b08c798042f0bb354fa453abeb226f267989`.

Production application of this diff awaits separate explicit approval.

The complete local Release gate finished with **5,509 passed, 1 failed and 1 existing Rules quarantine skip**, exit 1 in **14m 24.41s**. Domain passed 589, Application 1,034, Infrastructure 1,630 and parallel Desktop 2,105 with the single Rules skip. Exclusive WPF passed 151 and failed 1. The sole failing top-level case was `SetupCardInputTests.MouseAndSpaceOpenOriginalAndReturnFocusWithoutRemovingExample`. Its retained child TRX identifies `SetupCardInputTests.cs:60`, the foreground-window assertion immediately before native input. Expected HWND was `2033344`, actual `65920`. The readiness log identified the test window as active, but its sampled foreground HWND differed. The actual foreground owner and cause remain unproven. This counts as one top-level failure. The Setup test implementation was unchanged by this task, and the gate was run once.

Gate artifacts are under `TestResults/tradovate-time-repair/gate/release-20261125-124529-768888aa8512471b9cfe5b04aca64bd1/`. The parent timer is `TestResults/tradovate-time-repair/gate-time.json`. The eight changed files are listed in `TestResults/tradovate-time-repair/changed-files.txt`.

Changed files for the repair are `README.md`, this guide, `scripts/TradovateTimestampRepair/Program.cs`, `scripts/TradovateTimestampRepair/TradovateTimestampRepair.csproj`, `src/PersonalTradingJournal.Application/Imports/Tradovate/TradovateTimestampRepair.cs`, `src/PersonalTradingJournal.Infrastructure/Imports/Tradovate/TradovateTimestampRepairService.cs`, `tests/PersonalTradingJournal.Infrastructure.Tests/Imports/Tradovate/TradovateTimestampRepairTests.cs`, and `tests/PersonalTradingJournal.Infrastructure.Tests/Imports/Tradovate/TradovateVerifiedCostTests.cs`. The last file exposes its existing synthetic fixture helpers for reuse.

## September reporting mismatch

At the baseline `ce24472c8aee4a3a912f0d70fdc8fc86eaf1862b`, the raw 185-fill export blocked with 0 missing and 40 extra. The supplied screenshots for September 22 show 40 Fills, zero Performance Trades and zero Cash History transactions. Read-only CSV inspection agreed. Both dates on the extra Fill rows were September 22, the quantity netted to zero in its contract, and the Fills commission column summed to **85.28 USD**. These observations establish the discrepancy between reports.

The importer now requires every Performance fill ID to match exactly one Fills row. An extra ID is eligible for exclusion only if its broker trading date and display date are absent from the matched Performance fills and all Cash History entries, using both date fields. Each excluded contract/date must net to zero quantity, and an extra fill within a matched Trade’s UTC position interval for that contract blocks reconciliation. All raw rows still undergo Account, clock, and Fills validation. Missing IDs, duplicates, source disagreements and unexplained cash costs still block. Costs, gross checks, and the verified period use only matched fills. Preview and confirmation list excluded fill counts and Fills-column commissions by date.

The unmodified September exports reached Verified in the read-only ViewModel probe. The probe captured and declined the confirmation, with writes disabled and no journal services. **90 Performance rows** referenced **145 matched fills**, producing **24 reported closed Trades** and **145 allocations** with zero UTC mismatches. The **90 Cash Trade Paired rows** reconcile to **1,063.50 USD Gross** and **145 Cash Commission rows** reconcile to **294.32 USD costs**, yielding **769.18 USD verified Trade Net**. Preview and confirmation disclosed the **40 excluded September 22 fills** and their **85.28 USD** Fills-column commissions. See `TestResults/TradovateSeptemberUtc/exclusion-baseline.log` and `TestResults/TradovateSeptemberUtc/exclusion-verified.log`.

The Release build passed with zero warnings and errors. Focused tests passed **281/281** (84 Application, 164 Infrastructure, 33 Desktop). Synthetic tests cover new import, replay and enrichment with exclusions, declined confirmation, no extra trades or identities, report/display date collisions, an open position across an excluded date, net quantity imbalance, unexplained cash costs, missing IDs and Account or currency mismatch. Exact existing-Trade timestamp conflicts remain covered. Focused TRX files are under `TestResults/TradovateSeptemberUtc/exclusion-focused/`.

The complete local Release gate `release-20261122-142144-d133412029ee4adeb3494f567dd1f865` recorded **5,636 passed, 0 failed and 1 skipped** in **14m 01s**. Exclusive WPF passed 151, Domain 602, Application 1,052, Infrastructure 1,676 and parallel Desktop 2,155. All five top-level TRX files were checked for consistent counters and unique test identities. The sole skip was the existing [Rules-view quarantine](rules-view-quarantine.md). The original shell exit code was unavailable after the session ended.

<details>
<summary>Files changed for the September reporting mismatch</summary>

- `README.md`
- `docs/tradovate-csv-import.md`
- `docs/tradovate-verified-costs.md`
- `src/PersonalTradingJournal.Application/Imports/Tradovate/TradovateCostReconciler.cs`
- `src/PersonalTradingJournal.Application/Imports/Tradovate/TradovateCostSources.cs`
- `src/PersonalTradingJournal.Application/Imports/Tradovate/TradovateImportPreparationService.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Import/ImportViewModel.Costs.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Import/ImportViewModel.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Import/TradovateBoundPreviewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Import/TradovateImportAcceptanceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Imports/Tradovate/TradovateVerifiedCostTests.cs`

</details>

## September timestamp and fill scope

Read-only baseline on develop at `689e81e9c76527bd410ee046b05e425ee003f3b0` found two independent blocks. The full supplied Fills file had 185 unique IDs and failed the exact-ID check. A September 23–30 in-memory projection of 145 Fills reached the clock check and reproduced the specific error. Every display timestamp in the supplied file is UTC+02:00 at whole-second precision while Europe/Sofia is UTC+03:00. The prior rule required some rows to fall in Sofia standard time before accepting fixed UTC+02:00.

The verified workflow now checks every exact UTC instant against its displayed second and accepts one consistent convention for the whole file. Each execution keeps the exact Fills UTC instant, including fractions. Europe/Sofia agreement remains valid, and the preview labels fixed UTC+02:00 when it is the only supported convention that fits. When both fit, the preview reports agreement with Sofia. Mixed offsets and contradictory seconds fail clock validation. The independent exact-ID check follows it. At that milestone the full supplied files reported **0 missing, 40 extra** and kept confirmation disabled. The guarded exclusion above supersedes that result. All extra IDs are from September 22 and account for **85.28 USD** of commission outside the requested scope.

The supplied Performance and Cash History files generated a verified preview when paired with a September 23–30 in-memory Fills projection. It used **90 Performance rows, 145 matching fills, 145 execution allocations, 24 Trades and zero UTC differences**. Gross was **1,063.50 USD**, commissions **294.32 USD**, and Trade Net **769.18 USD**. Cash History has 90 Trade Paired rows and 145 matching Commission rows. No CSV was changed or written by the probe. At that milestone the raw supplied Fills file remained blocked. The verification used a synthetic Account reader and never called confirmation. See `TestResults/TradovateSeptemberUtc/baseline.log` and `TestResults/TradovateSeptemberUtc/verified.log`.

The Release build passed with zero warnings and errors. Focused tests passed **271/271** (84 Application, 154 Infrastructure, 33 Desktop) and covered Sofia and fixed UTC+02:00, both seasonal scopes, mixed offsets, contradictory and fractional seconds, extra IDs, exact Calendar cutoff, replay, and saved timestamp conflicts. Focused TRX files are under `TestResults/TradovateSeptemberUtc/focused/`.

The complete local Release gate `release-20261122-133707-88c31e079fae4d89a0409db577c0fac6` finished in **13m 57s**, exit **0**, with **5,626 passed, 0 failed and 1 skipped**. Exclusive WPF passed 151, Domain 602, Application 1,052, Infrastructure 1,666 and parallel Desktop 2,155. The sole skip was the existing [Rules-view quarantine](rules-view-quarantine.md).

<details>
<summary>Files changed for the September correction</summary>

- `README.md`
- `docs/tradovate-csv-import.md`
- `docs/tradovate-verified-costs.md`
- `src/PersonalTradingJournal.Application/Imports/Tradovate/TradovateCostReconciler.cs`
- `src/PersonalTradingJournal.Application/Imports/Tradovate/TradovateImportPreparationService.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Import/ImportViewModel.Costs.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Import/TradovateBoundPreviewTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Imports/Tradovate/TradovateVerifiedCostTests.cs`

</details>

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

At that milestone, after the row could be parsed, the original Sofia display-clock validation independently blocked cost verification. All 770 Fills displayed UTC+02:00 to the second. That matched Sofia for 155 fills and differed by one hour for 615 before the autumn clock change. At that milestone, verified costs accepted the standard Sofia convention or a consistent UTC+02:00 display across the seasonal transition. The September correction extends the latter to single-season exports. Each Performance wall-clock timestamp must still match its Fills row. Mixed clocks fail. At that milestone, the Performance interpretation still gave a UTC instant one hour earlier than Fills UTC for 615 pre-change fills. The timestamp correction above supersedes that behavior for verified imports.

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
- Fills require an active row, agreeing internal/display ID, Account, contract, side, whole quantity, price, UTC timestamp, verified display timestamp (see the October clock check above), trading date and nonnegative whole-cent commission. Every Performance fill must exist exactly once in Fills, with all matched quantity accounted for. Missing and duplicate fills block verification, including identical duplicate Fills rows. The September reporting-mismatch contract above defines when an extra fill may be excluded. A fill repeated across Performance matched rows is valid.
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

