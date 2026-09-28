# M10 Final Acceptance

Reviewed on 2026-09-28 on `develop`, source HEAD `f8e1c5c05befc1b0f8343cb72385147da1e18ae9`.

M10 covers parsing/normalization (M10.1), reconstruction (M10.2), Instrument resolution (M10.3), Account/time preparation (M10.4), read-only preview (M10.5), transactional confirmation/deduplication (M10.6), and Desktop acceptance/presentation (M10.7). The contracts and remaining format limitations are described in [Tradovate CSV Import](tradovate-csv-import.md).

**Completion decision: final interactive acceptance remains open.** Automated checks passed and no concrete import defect was found in this review. The isolated app started, but native mouse input failed with `coordinate input geometry is unavailable`. Capture failed with `FrameArrived timed out` and, after refreshing the window binding, `window capture timed out`. Keyboard focus could be inspected but did not reliably complete navigation. Startup, ViewModel tests, and programmatic WPF renders are not interactive acceptance.

## Acceptance matrix

Status describes the requested scenario, including native interaction where applicable. Automated evidence is labeled separately.

| Scenario | Status | Evidence |
| --- | --- | --- |
| Select Account/CSV and review counts, diagnostics, resolution, prices, and quantities | Unverified | Automated Desktop ViewModel-to-migrated-SQLite runs passed for both supplied files; native picker/review interaction was not completed |
| Cancel confirmation without writes, then confirm | Unverified | `TradovateImportAcceptanceTests.ConfirmedDesktopWorkflowPersistsOnceAndReportsDuplicateReplay` passed with a fake dialog and real migrated SQLite; native dialog not exercised |
| Imported, NoChanges, completed-state clearing, and heading | Unverified | SQLite acceptance, `ImportViewModelTests`, and `ImportViewXamlTests` passed; actual completed screen interaction remains to be observed |
| Blocking diagnostics and recovery after file/Account changes | Unverified | `TradovateTimestampWorkflowTests` and ViewModel tests passed for blocked source, account invalidation, replacement file, missing Account, reference change, and retry; native recovery flow not observed |
| Transaction, reversal allocation, deduplication, and reference revalidation | Passed | `TradovateImportStoreTests`, `TradovateReversalImportTests`, reconstruction tests, and supplied-file replay passed; rollback and newly ambiguous existing Instruments are covered |
| Imported list/Details, Size, prices, P&L, account/header layout, View and menu | Unverified | Authoritative readers, converters, ViewModels, and XAML coverage passed; isolated programmatic list renders checked layout, but native View/Details/Edit/Delete interaction remains open |
| Narrow-window scrolling and access to actions | Unverified | Programmatic WPF layout/scroll checks passed at 1,280 and 1,024 DIP with 250% scaling; mouse/keyboard scrollbar interaction not observed |

No scenario is labeled Failed: the outstanding items are missing interactive evidence, not a reproduced application failure.

## Automated supplied-file evidence

Both files ran through the actual `ImportViewModel` commands, parser, reconstructor, resolver, preparation, preview builder, import use case, and registered SQLite store. The file picker and confirmation dialog were deterministic test adapters. Each run created a fresh temporary application root, applied production migrations, and seeded synthetic Accounts. No real journal was opened.

| Input | Preview | First confirmation | Replay |
| --- | --- | --- | --- |
| Local A049 source | 198 valid rows, 57 candidates; ready | Imported: 57 Trades, 0 skipped, 1 Instrument | NoChanges: 0 imported, 57 skipped |
| Small valid fixture | 1 valid row, 1 candidate; ready | Imported: 1 Trade, 0 skipped, 1 Instrument | NoChanges: 0 imported, 1 skipped |

Both previews reported only `SOURCE_COMPLETENESS_UNVERIFIED` and `COSTS_UNAVAILABLE`, with no blocking diagnostic. Database checks before confirmation found no Trades, executions, allocation-ledger rows, or proposed Instruments. Account changes invalidated the old preview; a new file cleared the old result. Source P&L was conserved across candidate evidence.

The larger source persisted 314 allocations for 313 real fills, including a shared reversal fill. All 57 closed Trades retained unknown Net P&L and nonzero peak Size (1–20 contracts). Every paged Size was compared with the persisted execution sequence; replay and a fresh service provider preserved the values. The small fixture retained quantity 2, exact entry/exit values 20123.125/20124.375, Gross P&L 5 USD, and unknown costs/Net. Customer rows and broker IDs are not stored in this acceptance document or tracked fixtures.

The layout check uses an isolated copy of imported data and `RenderTargetBitmap`, not native UI automation. At a 1,280-DIP window the measured viewport is 884.4 DIP, with no horizontal overflow. Opened is 126 DIP, Account 94 DIP with at most two lines, Size 56 DIP, and Actions 122 DIP. At 1,024 DIP the viewport is 628.4 DIP; horizontal extent is 884 DIP and scrolling 255.6 DIP exposes both actions. Hover, focus styling, native file selection, dialogs, and menu activation still require interactive observation.

## Verification

- `dotnet build PersonalTradingJournal.sln --configuration Release`: passed, 0 warnings and 0 errors; restore up to date.
- `dotnet test PersonalTradingJournal.sln --configuration Release --no-build`: 1,720 passed, 0 failed, 0 skipped (Domain 400, Application 312, Infrastructure 504, Desktop 504).
- `git diff --check`: passed.

Existing coverage also checks harmless timestamp ties, matched-fill constraints across reversals, genuinely unresolved equal-time orders, cancellation, repeated confirmation, failed-write rollback, proposal races, unknown versus zero costs, exact persisted prices, and Gross/Net sign presentation. No new application functionality or fee sourcing was introduced by this documentation review.

## Remaining Windows desktop checks

Use an acceptance build whose `IApplicationPaths` resolves to a fresh temporary root, initialized through `JournalDatabaseInitializer`; the ordinary Desktop executable uses the real journal and is not an isolated acceptance launcher. The review's local helper/build is deliberately untracked. Keep customer CSVs outside tracked files.

1. Open Import, select the small valid CSV and a synthetic Account, then Build Preview. Expect one candidate, explicit MNQ resolution/proposal, the two warnings above, quantity 2, and displayed prices 20123.13/20124.38. Instrument and Trade counts in the isolated database must remain unchanged.
2. Choose Import Trades, cancel the dialog, and verify the preview remains with no writes. Open it again and accept. Expect Imported with 1 Trade and, in a clean catalog, 1 Instrument; intermediate sections disappear and the result is under `Confirm import`. Repeated clicks must not submit the completed preview.
3. Select the same file and Account again, rebuild, and accept. Expect No changes, 0 imported, 1 skipped, 0 Instruments created, with the same completed layout.
4. Select a synthetic malformed or unresolved-timestamp fixture. Expect a precise diagnostic and disabled confirmation. Select the valid file and change Account: the stale preview must clear and rebuilding must recover. Separately, change/duplicate the referenced Instrument after a preview in the isolated store: confirmation must block with `REFERENCE_DATA_CHANGED` and require rebuilding.
5. Use Cancel during an operation if its duration permits. Verify the controls become usable again without competing submissions. Deterministic cancellation/concurrency evidence already exists in tests; record native behavior separately.
6. Open Trades and View. Check Size 2 in the list, Open Qty 0 in Details, the two-decimal averages, Gross 5 USD, Net `—` with unknown-cost guidance, and positive Gross styling. Open the row's menu: Edit and Delete must target that Trade; Edit must explain the unknown-cost limitation and Delete must show its confirmation. Cancel deletion for this check.
7. At 1,280 DIP/250% scaling, check the full Opened heading, two-line `UI Test Account`, compact Size, prices, P&L, View, and the three-dot menu. At 1,024 DIP, scroll vertically to the table scrollbar and horizontally to the actions; test a long Account tooltip.
8. Repeat the import/replay with the locally supplied A049 file in an isolated Account. Expect the aggregate counts above; inspect both directions and the reversal's Trades without copying broker IDs into tracked evidence.

Commission/fee source identification is deferred and is not a completion blocker for the supported unknown-cost contract. The remaining completion blocker is the unobserved interactive workflow above. Record its results before marking M10 complete.
