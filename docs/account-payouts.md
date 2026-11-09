# Account payouts

Account details provides an optional, bordered theme-aware Payouts section. Record a positive withdrawal in that exact Account's currency, view each amount and UTC creation time, and remove an erroneous record only after confirmation. No historical withdrawals are inferred.

The list places **Payout** immediately after **Current Balance** and before **Status**, including zero with no entries. Data headings and values align left. The Actions header and controls align to the right content edge. Horizontal scrolling retains access at narrow widths.

Current Balance deducts cumulative payouts from the existing closed-Trade calculation. Missing Starting Balance, incomplete economics, overflow, Estimated state and missing-cost explanations remain intact. Payouts do not mutate Trades, execution costs, P&L, win rate or analytics.

## Persistence and ownership

The additive `AddAccountPayouts` migration creates an Account-owned table with positive amounts, currency snapshots, UTC creation times and an indexed restrictive foreign key. Existing Accounts/balances are unchanged. Deleting an Account referenced by payouts is blocked; deactivate it instead.

An immediate SQLite write transaction validates the Account currency and inserts the payout together. Account updates use the same transaction discipline and reject currency changes while payouts exist; removing erroneous entries permits a later currency edit. Removal matches both payout ID and Account ID. Total overflow is rejected before insertion. No currency conversion is performed.

[Backup and restore](backup-restore.md) use the existing whole-database archive. Portable export v19 includes `accountPayouts` in JSON plus a fifth CSV; portable exports are not restore inputs.

## Accounts action layout refinement

The Actions column now shares a content-sized width across the header and bound rows. Its right edge follows the row content edge inside the card padding. The proportional data columns share the freed space. View and overflow use 36-DIP heights and a vertically centered panel, independent of multi-line Current Balance coverage. The 1120-DIP table minimum and horizontal scrolling remain in place.

Payout Remove uses the existing theme-aware red danger button style. It continues to call the removal command with the exact payout record and requires confirmation. The refinement is scoped to the Accounts view and its layout checks.

Bound-WPF regressions verify all header/row column widths and data origins, right-edge placement, equal button heights and vertical centers, narrow scrolling, and resolved destructive brushes/command parameters in Light/Dark at 1280 DIP/96 DPI and 480 DIP/240 DPI. The Release build passed with zero warnings and errors. The focused Accounts run passed 48 top-level cases, including eight balance and payout layout cases and four bulk-action layout cases in their isolated child hosts. Eight offscreen balance and payout renders were inspected across both themes and sizes. The narrow balance images show the viewport after bringing Current Balance into view. The bound checks also scroll to the right edge and verify that the overflow trigger is fully reachable. Live window inspection, physical DPI and screen-reader checks remain pending.

On 9 November 2026, `pwsh -File scripts/Test-Release.ps1` exited **0** with **5,562 passed, 0 failed and 1 skipped**. Exclusive WPF passed 151, Domain 602, Application 1,050, Infrastructure 1,612 and parallel Desktop 2,147. The sole skip was the existing Rules-view quarantine. The focused run and renders are under `TestResults/account-actions-focused` and `TestResults/account-actions-renders`. The complete run is under `TestResults/release-20261109-140411-1bd13d62a14c41edbb733dd8a655e6ce`.

Exact changed files for this refinement

- `src/PersonalTradingJournal.Desktop/Views/Accounts/AccountsView.xaml`
- `tests/PersonalTradingJournal.Desktop.Tests/Accounts/AccountBalanceLayoutTests.cs`
- `README.md`
- `docs/account-payouts.md`
- `docs/desktop-ui.md`

## Verification

Release build passed with zero warnings/errors. The final Desktop selection passed 83/83 top-level cases (16 separately retained child cases, not added to that total). The expanded Infrastructure selection passed 325/326 before correcting a stale six-file export expectation; the corrected inventory plus final payout/migration/round-trip checks passed 6/6. Earlier development checks caught and corrected an export-order declaration and outdated layout expectations. The first complete gate (`release-20261108-233747-48d4495bbbc5438e832b1d100521b70f`) finished with **5,554 passed / 8 failed / 1 existing Rules skip**, exit 1. Exclusive WPF passed 151/151 and parallel Desktop passed 2,147 with the one quarantine. All eight Infrastructure failures were stale explicit schema expectations: migration IDs/counts, latest migration, record inventory and foreign-key matrix. These expectations were extended for the additive payout schema without weakening exact-set checks. All 79 affected schema/migration/integrity/payout checks subsequently passed. All fixtures use synthetic data. The final complete gate after these corrections passed, as recorded below.

Compiled WPF renders exercise Light/Dark at 1280 DIP/96 DPI and 480 DIP/240 DPI. Offscreen rendering does not establish physical display scaling or native accessibility behavior. Live manual checks remain pending. Computer Use initialization failed before selecting an application: `failed to write kernel assets: The system cannot find the path specified. (os error 3)`. No production journal was opened.

## Manual checks

- Record multiple payouts, cancel and confirm removal, and observe Current Balance and history refresh.
- Inspect Light/Dark at normal and narrow/high-DPI display settings; reach every column/action by scrolling and keyboard.
- Check screen-reader labels and confirmation focus.

## Final Release gate — 9 November 2026

Command: `pwsh -File scripts/Test-Release.ps1` after the Release build.
Artifact root: `TestResults/release-20261109-071430-8a202a28d8b848e792a406147e614cc0`.

| Stage / project | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Exclusive WPF | 151 | 0 | 0 |
| Domain | 602 | 0 | 0 |
| Application | 1,050 | 0 | 0 |
| Infrastructure | 1,612 | 0 | 0 |
| Parallel Desktop | 2,147 | 0 | 1 |
| **Top-level total** | **5,562** | **0** | **1** |

Exit **0**. Five top-level TRX files contain 5,563 unique test identities; isolated child evidence is not added again. The sole skip remains `RulesViewTests.CompiledThemesNarrowHighDpiBindingsValidationHistoryAndWindowClose`, under the existing [Rules quarantine](rules-view-quarantine.md). No additional test was skipped or disabled. The nine additional top-level tests comprise three payout persistence/calculation/migration cases, one backup/export round trip, one view-model command/confirmation case and four payout-detail layout parameters.

Focused evidence: `TestResults/payouts-desktop-corrected` (83 parents / 16 separately retained children), `TestResults/payouts-infrastructure-corrected` (6/6), and `TestResults/payouts-schema-contract` (79/79). These selections overlap; do not sum them as unique tests. Final Release build: zero warnings/errors (`TestResults/payout-build-final.log`). The first gate's eight stale schema expectations are preserved above as historical failures, not current failures.

Eight Account table/detail renders in `TestResults/payout-renders` were inspected: Light/Dark at 1280 DIP/96 DPI and 480 DIP/240 DPI. Payout inputs/history fit, money and headings align left, and horizontal scrolling retains action access. The existing four-column Account metadata grid wraps heavily at narrow widths; this task preserves that layout. Native visual, physical-DPI and screen-reader acceptance remain pending. Hosted CI has not been run for these local changes.

## Original payout implementation files

- `docs/account-payouts.md`
- `docs/backup-restore.md`
- `docs/desktop-ui.md`
- `README.md`
- `src/PersonalTradingJournal.Application/Accounts/AccountCurrentBalance.cs`
- `src/PersonalTradingJournal.Application/Accounts/AccountListItem.cs`
- `src/PersonalTradingJournal.Application/Accounts/IAccountPayoutStore.cs`
- `src/PersonalTradingJournal.Application/Backups/IPortableExportService.cs`
- `src/PersonalTradingJournal.Desktop/Converters/AccountBalanceDisplayConverter.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Accounts/AccountsViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Accounts/AccountsViewModel.Payouts.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Settings/DataBackupsViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/Accounts/AccountsView.xaml`
- `src/PersonalTradingJournal.Infrastructure/Accounts/AccountPayoutStore.cs`
- `src/PersonalTradingJournal.Infrastructure/Accounts/TradingAccountDeletionStore.cs`
- `src/PersonalTradingJournal.Infrastructure/Accounts/TradingAccountReader.cs`
- `src/PersonalTradingJournal.Infrastructure/Accounts/TradingAccountStore.cs`
- `src/PersonalTradingJournal.Infrastructure/Backups/PortableExportSchema.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/JournalDbContext.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261108232624_AddAccountPayouts.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261108232624_AddAccountPayouts.Designer.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/JournalDbContextModelSnapshot.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Records/AccountPayoutRecord.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Accounts/AccountBalanceLayoutTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Accounts/AccountPayoutViewModelTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Accounts/AccountsViewModelTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Settings/DataBackupsFlowTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Settings/DataBackupsTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/AccountPayoutPortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/NotebookImagePortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/NotebookPortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/PortableExportTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/SavedPeriodPortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Accounts/AccountCurrentBalanceReaderTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Accounts/AccountPayoutTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Initialization/JournalDatabaseInitializerTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integration/ProductionPersistenceIntegrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integrity/SqlitePersistenceIntegrityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Migrations/InitialMigrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookPersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/DailyTradingPrincipleTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/SharedTradingPlanTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/TradingPlanPortabilityTests.cs`
