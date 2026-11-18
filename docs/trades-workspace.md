# Trades workspace

## Acceptance on 18 November 2026

The live check used the Release executable at `src/PersonalTradingJournal.Desktop/bin/Release/net10.0-windows/PersonalTradingJournal.Desktop.exe`. The first launch used clean develop commit `67c318738541faff03b10917bbe2141c8ec02aca`, tree `f65e1fb7c18ca56e40930d2fdd32fecd6e0ee6d4`. All launches used `--isolated-data-root` with the disposable directory `TestResults/TradesLive-b85e956134904da48f2258f58c8e5582`. The account, instrument, classifications, executions and chart are synthetic. Provider-key overrides were empty.

In the original live form, Windows UI Automation exposed the Account, Instrument and Direction combo boxes and the Quantity and execution text fields without names. The adjacent captions were separate text nodes. The shared create/edit template now supplies control names, binds the quantity name to its current caption, and distinguishes entry and exit costs. A regression finds controls by their functional binding and checks the name returned by their WPF automation peer across both themes and sizes.

| Acceptance check | Live Windows evidence | Other verification |
| --- | --- | --- |
| Manual Trade entry, Description and focus | Traversed the sidebar to Trades, opened Add Trade by keyboard, and read back quantity, entry time, price and optional Description entered into the live form. | Compiled form and persistence checks cover Description and optional entry. |
| Page scrolling over Executions | Native wheel and touchpad gestures pending. | Compiled page checks cover vertical wheel deltas, Page Down and preservation of the horizontal offset. |
| Saved economics, Description and Trade Details | Opened the saved SYN Trade by keyboard. Details exposed the saved Description, both New York timestamps, and no Pricing Snapshot heading. With two contracts bought at 100 and sold at 102, the historical point value of 5 produces USD 20 gross, USD 3 costs and USD 17 net. The current instrument point value is 10. The isolated database projection and live row agree. | Persistence and compiled-view checks also cover stored pricing and the retired details section. |
| View and three-dot actions | Tab and Shift+Tab reached both row actions. Physical button geometry remains pending. | Both row controls measure 36 DIP and share their vertical alignment in the bound layouts. |
| Setup, Mistake and five-second feedback | Saved Breakout, opened Change Setup, selected the other choice and canceled. The saved value and SQLite row still showed Breakout. The live status appeared and disappeared. Opened Add Mistake and observed its selector, note, Save and Cancel. Saving or changing a Mistake, canceling its draft, and spoken feedback remain pending. | Controlled-clock tests establish five-second expiry and replacement. Live UI Automation sampled the status and could not time publication precisely. Bound tests cover the classification drafts. |
| Screenshot picker and cards | The create form exposed Add Screenshot. On the saved Trade, Add Screenshot opened Type, Timeframe, Upload, Save and Cancel inline. The native picker, persistence of a live upload and card preview remain pending for both workflows. | The compiled workflow uses a fake picker and checks staged/saved cards. Persistence cases exercise atomic creation and failure cleanup. |
| Timestamps, themes and DPI | The original light and corrected dark sessions ran. Dark Trade Details exposed 2026-11-17 09:30:00 and 10:30:00 with their New York labels. Physical sizing, visual contrast and narrow-window readability remain pending. | Compiled Light/Dark views at 960 DIP / 96 DPI and 360 DIP / 240 DPI. Both narrow renders show the complete New York labels and timestamps. |
| Accessible names and speech | The corrected create form exposed Trading Account, Instrument, Direction, Quantity, Trading Setup (optional), the four entry-execution names and Trade description. Full keyboard traversal and screen-reader speech remain pending. | The corrected compiled form checks 13 control names. A polite live region is configured for Setup feedback. |

The Windows helper reported `coordinate input geometry is unavailable`. The initial capture failed with `FrameArrived timed out: timed out waiting on channel` and the recovery capture failed with `window capture timed out: timed out waiting on channel`. Text-only UI Automation remained available. Inside the screenshot and Mistake forms, the helper continued to report focus on the editable field after Tab. It could not reliably identify the next target. No screenshot or Mistake record was saved in the synthetic database. No active Narrator, NVDA or JAWS process was detected.

**Build identity.** The native apphost SHA-256 is `84C1F091EC35ECCEFBBF96265745379C41EC284B2617A1F364589CA7DFF012E6` for both builds. The managed Desktop DLL SHA-256 changed from `DF26C5FA284E4E5608B6337C3A38B09FA1302724A746A36669346391E589BDE8` to `10E437018799799575FCBE40A3702B124F357203B2796793F3BE4FCCF49A4FFB`. The build-input tree, calculated from the base tree plus the corrected XAML and regression, is `03b7a8dafba58ef51c6d1bc042a2de64176c9265`. Documentation edits followed the build.

**Local verification.** The Release solution build had zero warnings and errors. The focused run passed 27 of 27. The complete gate (`release-20261118-134537-8822de0fec0c4883ad32ab90f8a6879b`) exited 0 with **5,610 passed, 0 failed and one existing [Rules-view quarantine](rules-view-quarantine.md) skip**. Exclusive WPF passed 151, Domain 602, Application 1,052, Infrastructure 1,652 and parallel Desktop 2,153. Wall time was **23m 16s**, 3m 16s over the hosted 20-minute Test budget. The historical [Journal History failure](ci-wpf-tests.md#journal-history-zero-row-assertion--17-november-2026) remains unexplained. The synthetic profile contains `gate.log`, `gate-exit.json`, the focused TRX and renders, and four live UI Automation observations (`baseline-manual-uia.txt`, `corrected-manual-uia.txt`, `corrected-detail-uia.txt` and `setup-after-cancel-uia.txt`).

**Hosted CI.** GitHub reported develop at the base commit, zero workflow runs for that SHA, no associated PR and no open develop PR. The workflow runs for pushes to main and PRs targeting main. CI #121 and the head of PR run #120 have tree `7442ca555cbffe2ecd458ceeb994f0f38ad4b62c`. The accessibility correction is local. The hosted gate counts, Rules skip and Test-step elapsed time for the corrected sources remain pending. [CI #121](https://github.com/yovko93/Personal-Trading-Journal/actions/runs/42018550512)

**Changed files.** `src/PersonalTradingJournal.Desktop/Views/Trades/TradeEntryForm.xaml`, `tests/PersonalTradingJournal.Desktop.Tests/Trades/TradeWorkspaceViewTests.cs`, `README.md`, and `docs/trades-workspace.md`.

## Current behavior

Trades focuses on execution facts, optional Trade Description, Setup/mistake classification and screenshots. The shared page title remains; the repeated list heading is removed. Row View uses a distinct theme-aware action style, including hover, keyboard focus and disabled states. Execution facts and broker/source identifiers use separate Light/Dark surfaces, with horizontal scrolling for narrow execution tables.

Trade Details no longer starts Rule assessment or opens captured Playbook context. The Rules catalog, Calendar day assessment and historical assessment readers remain available. Basic Setup classification does not capture guidance or append history. The Calendar's shared inline Trade details also loses the retired entry points.

## Timestamp and Setup feedback

The Trade Details economics summary uses two columns when its available width falls below 560 DIP, with Opened and Closed beside each other. The wider layout keeps its four columns. The complete New York labels, date, and time keep the same conversion and format. A Trade without a close time still shows the existing dash.

The “Trading setup updated successfully.” message disappears five seconds after publication. A later successful save or clear starts a fresh five seconds. Changing the selected Trade, leaving Trades, closing the shared inline detail, or disposing the page cancels the pending dismissal. The visible message is a polite live region. Errors and validation messages follow their existing resolution paths.

### Verification

The final Release build passed with zero warnings and errors. All 365 focused Desktop cases passed. The controlled-clock cases cover expiry, a replacement with its own five seconds, existing error and validation behavior, navigation, inline reset, disposal, and a save completing after navigation.

Inspected all four compiled economics summary renders in Light and Dark at 960 DIP / 96 DPI and 360 DIP / 240 DPI. Bound WPF checks compare the full timestamp and New York label with the allocated width and confirm the wide and narrow column arrangements. The status region is configured for polite announcement. Live resizing and a screen-reader walkthrough remain pending.

The complete local gate (`pwsh -File scripts/Test-Release.ps1`) finished in about 14 minutes 18 seconds on 17 November 2026 with exit 1.

| Stage/project | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Exclusive WPF Desktop | 151 | 0 | 0 |
| Parallel Domain | 602 | 0 | 0 |
| Parallel Application | 1,052 | 0 | 0 |
| Parallel Infrastructure | 1,652 | 0 | 0 |
| Parallel Desktop | 2,152 | 1 | 1 |
| **Total** | **5,609** | **1** | **1** |

JournalHistorySqliteTests.AllScopesHistoryOpensEditsAndDeletesOriginalAccountWithoutChangingOtherEntries failed at JournalHistorySqliteTests.cs:38. The assertion expected three History rows after activation and observed zero. The case passed in the preceding complete gate and in a single unchanged isolated rerun on the current build. Its cause remains unresolved. The sole skip is the existing [Rules-view quarantine](rules-view-quarantine.md).

The gate summary and TRX files are under `TestResults/release-20261117-124842-e27c4cdf27fe4a1ea97ff1535caab4f2`. The focused TRX files, inspected PNGs and isolated Journal rerun are under `TestResults/TradesTimestampFeedback/`. Hosted timing under the 20-minute workflow limit is unverified. The prior layout refinement and its verification follow below.

### Changed files for this correction

- `docs/trades-workspace.md`
- `README.md`
- `src/PersonalTradingJournal.Desktop/ViewModels/Trades/TradesViewModel.Breakdown.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Trades/TradesViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Trades/TradesViewModel.SetupFeedback.cs`
- `src/PersonalTradingJournal.Desktop/Views/Trades/TradesView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Trades/TradesView.xaml.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Trades/TradeSetupFeedbackTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Trades/TradesViewModelTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Trades/TradeWorkspaceViewTests.cs`

## Trades layout refinement

Trade Details shows the saved Setup and assigned Mistakes with their notes. **Add Setup** or **Change Setup** opens the inline Setup selector. For a saved Setup, **Clear Setup** removes the classification. **Add Mistake** opens its selector and optional note. Use **Remove** on a saved Mistake to remove that assignment. **Cancel** discards the current draft and preserves saved values. A failed write keeps the editor open. Save or cancel the draft before switching Trades, leaving the page, or closing the Calendar Trade panel.

The View and overflow buttons in each list row share a fixed 36 DIP height and are centered together. **Add Screenshot** sits at the right of the heading, including the shared manual entry form. Setup and Mistake headings use the same right-aligned layout. Trade Details omits the Pricing Snapshot section. The captured pricing facts still serve calculations, imports and export.

Execution columns keep their horizontal scrollbar. Ordinary vertical wheel events over an execution go to the nearest vertically scrollable parent that can move in the requested direction, even at the horizontal boundary. This includes small wheel deltas of the kind a touchpad can emit. The page retains vertical panning. Up, Down, Page Up and Page Down over a focused execution scroller go to the Trades page. Horizontal navigation remains with the inner controls.

### Refinement verification

The Release build passed with zero warnings and errors. The focused Desktop run passed 366 cases. After widening the Actions column to eliminate clipping found in the renders, the repeated compiled view and table geometry checks passed 12 cases. The two focused counts overlap.

Inspected compiled WPF renders in Light and Dark at 960 DIP / 96 DPI and 360 DIP / 240 DPI. The section actions fit beside their headings, the forms collapse after Cancel, and both row controls are fully visible after scrolling right. That run found some timestamps truncated at 360 DIP. See the later [timestamp correction](#timestamp-and-setup-feedback) above.

Automated input covers a routed full wheel notch, a smaller wheel delta, both vertical directions, the inner horizontal boundary and a routed Page Down key. These were WPF events, with no live mouse, touchpad or keyboard walkthrough on the Trades page. Real touchpad gestures, physical keyboard focus traversal, screen reader checks and manual UI inspection remain pending.

The complete local gate, `pwsh -File scripts/Test-Release.ps1`, finished with exit 0 in 14 minutes 8 seconds on 17 November 2026.

| Stage/project | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Exclusive WPF Desktop | 151 | 0 | 0 |
| Parallel Domain | 602 | 0 | 0 |
| Parallel Application | 1,052 | 0 | 0 |
| Parallel Infrastructure | 1,652 | 0 | 0 |
| Parallel Desktop | 2,147 | 0 | 1 |
| **Total** | **5,604** | **0** | **1** |

The sole skip is the existing [Rules-view quarantine](rules-view-quarantine.md). The added classification draft regression accounts for the extra test relative to the preceding complete gate. Totals count top-level cases only.

Local evidence is under `TestResults/release-20261117-120020-e5075a1ae37743919805618160000503` and the focused TRX files and render PNGs are under `TestResults/TradesRefinement/`. Hosted CI has a 20-minute limit and its runtime for this change is unverified. The earlier Trades workspace results below describe the original simplification.

### Refinement changed files

- `docs/trades-workspace.md`
- `README.md`
- `src/PersonalTradingJournal.Desktop/ViewModels/Calendar/CalendarViewModel.InlineTrade.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Trades/TradesViewModel.Breakdown.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Trades/TradesViewModel.Classification.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Trades/TradesViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/Trades/TradeScreenshotWorkspace.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Trades/TradeSetupClassificationView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Trades/TradesView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Trades/TradesView.xaml.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Interactions/NestedTableWheelRoutingTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Trades/TradeClassificationDraftTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Trades/TradeMistakeAssignmentViewModelTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Trades/TradePlaybookAssociationTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Trades/TradePlaybookViewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Trades/TradesViewModelTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Trades/TradesViewPagingXamlTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Trades/TradeWorkspaceViewTests.cs`

## Screenshots and manual creation

**Add Screenshot** opens an inline form, without opening the file picker. Choose Type and optional Timeframe, then **Upload Screenshot**. Save adds the selected image; Cancel discards the draft. Compact image cards provide View and the existing confirmed, ownership-checked Remove action.

The manual Trade form uses the same screenshot component. Uploaded bytes stay in memory (up to 20 MB per image), after decoding through the existing image decoder. Saving the screenshot form stages a card; it does not write files or metadata. Staged images can be viewed or removed. Save or cancel an unfinished screenshot form before saving the Trade.

On Trade Save, the use case validates references/economics, stores the staged files through the existing guarded storage abstraction, and commits the Trade, executions, browse projection and all screenshot metadata in a single database save. Failure/cancellation compensates the operation-owned files using an uncancelled cleanup token. Cleanup failures are retained in the exception rather than reported as successful creation. The editor retains the draft after a failed save; Cancel discards in-memory staging. A successful save opens the new Trade with its screenshots.

Stored screenshot capture times, descriptions and file identities are not rewritten. Trades no longer enters or displays capture time, screenshot description, raw storage keys or audit metadata. Other consumers and backup/export retain the old fields. Trade Description is an independent nullable field, limited to 10,000 characters in manual entry; it is not screenshot description or Journal text. Imported economics and provenance are unchanged.

## Migration and retained evidence

Migration `20261114204255_SimplifyTradeWorkspace` adds nullable `Trades.Description` and retires Trade capture data:

- Deletes `TradePlaybookAssociations` and `TradePlaybookContexts` rows, without changing `Trades.TradingSetupId`.
- Keeps the two empty compatibility tables for historical readers/export shape, with insert-rejecting retirement triggers. They are not an active hidden ledger.
- Removes the runtime capture writers and confirmation use case. Their old implementations are retained only in explicit historical test fixtures, where synthetic pre-retirement data is needed to test immutable evidence compatibility.
- Leaves Playbook definitions, revisions, image-retention indexes and physical images untouched. No image pruning accompanies retirement.
- Leaves Rule assessments and immutable Daily/Period Review JSON untouched. Saved reviews read their frozen payloads, not newly reconstructed capture history. New evidence sees classification without a captured plan.

Rule evidence continues hashing its established identity/execution fields, excluding the new optional Description property. This prevents the additive null column from marking historical assessments changed. A historical assessment referencing a retired capture remains readable and reports that source as unavailable. Historical-schema tests seed the columns available at that migration rather than writing a later model into an older table.

Downgrade restores the old schema capability but cannot reconstruct deleted associations. Historical migrations remain unchanged. Tests deliberately seed old captures, apply the actual retirement migration, and check image retention, classification, execution facts, saved review readability and rejection of new capture inserts.

Whole-database backup archive v1 includes Trade Description and screenshots. Portable export v20 explicitly adds `description` to the Trade JSON/CSV allowlist; legacy screenshot metadata remains exported. The empty retired tables remain represented. Exact-schema backup/preflight/restore checks are preserved: older-schema archives are identified and rejected rather than silently modified. Existing local databases migrate during normal initialization; all verification here uses synthetic isolated databases, not the production journal.

## Verification

Focused verification on 16 November 2026, on the uncommitted `develop` worktree based on `6ae434963e205c43e9e45bb8a871943a8ad09bed`:

| Check | Result | Local evidence under `TestResults/TradesWorkspace/` |
| --- | --- | --- |
| Release solution build | Passed, zero warnings/errors | `../trades-build.log` |
| Trades, Calendar shared layout, manual-assessment entry and resource checks | 313 passed | `desktop-verified/` |
| Final Trades and owned-Calendar regressions on the interactive desktop, after resource correction | 358 passed | `interactive-focused/` |
| Persistence, historical Rules/reviews, Playbook images, migration and export | 227 passed | `infra-verified/` |
| Schema inventory, initialization, persistence integration and integrity | 29 passed | `schema-final/` |
| Application Trade/screenshot checks | 131 passed | `application-verified/` |
| Domain Trade/screenshot checks | 209 passed | `domain-verified/` |
| Final screenshot/layout and theme-contrast checks | 56 passed | `final-layout/` |
| Final Notebook old-schema and related migration checks | 19 passed | `final-migrations/` |

These are top-level counts; isolated child results are not added again. Earlier focused failures exposed stale export-version/schema fixtures and a new template's resource-resolution assumptions; those checks were corrected without dropping assertions. EF previously reported no pending model changes.

The compiled WPF fixture renders Light/Dark at 960 DIP / 96 DPI and 360 DIP / 240 DPI. Its 24 PNGs cover the screenshot form, staged cards, details, saved cards/execution surfaces and manual description/screenshot entry. Relevant renders were inspected for readable wrapping, image proportions and scroll access. Hover/disabled styling is asserted through WPF properties; the focus template is checked, without claiming physical keyboard interaction. Bound-WPF assertions require primary and caption text on both execution surfaces to meet 4.5:1 contrast in Light and Dark themes.

The first complete-gate attempt, `release-20261116-075644-e405e07b28b1406db805cfbefe20d3f3`, exposed a shared screenshot preview `StaticResource PtjCardStyle` lookup failure in the Calendar owned-window path. The correction retains dynamic theme lookup on the card, separates visibility styling, and tests construction before theme attachment. This is a demonstrated resource-resolution regression, distinct from native-input failures in that run.

That attempt ran in the command sandbox. A bounded read-only comparison returned foreground HWND `0` inside the sandbox and a nonzero foreground HWND outside it, in the same Windows session; neither check activated windows or injected input. Retained Notebook traces also report foreground `0`, missing managed capture and unavailable native input state. The sandbox attempt was intentionally stopped before a complete result, retaining its logs and child TRX files. **It has no complete-gate total and is not a passing run.** After rebuilding the resource correction, all 358 focused Trades/owned-Calendar checks passed outside the sandbox. The subsequent interactive attempt, `release-20261116-080433-edfc1fb7c1bc4c2d862f410c4d751f68`, retained 151/151 ExclusiveWpf, 602/602 Domain and 1,052/1,052 Application results, but no final Infrastructure/Desktop results or complete total. Its log identified `NotebookMigrationTests.PreviousSchemaUpgradePreservesJournalTradeSetupScreenshotsAndResolvesDiAfterRestart` using the current Trade model against the pre-Description schema. That fixture now seeds the original columns explicitly, preserving its metadata and migration assertions; the 19 final migration checks pass. This attempt is incomplete, not accepted. The final gate is `release-20261116-133931-e7fa482c495a417cac47bc703ecc9992`, with a durable runner exit record at `TestResults/trades-release-final-exit.json`: **exit 0, 5,603 passed, 0 failed, 1 existing quarantined Rules skip**. The existing [Rules quarantine](rules-view-quarantine.md) remains separate and unchanged.

### Completed Release gate

Command: `pwsh -File scripts/Test-Release.ps1`, after the final Release build. Retained `summary.json` and the five top-level TRX files agree:

| Stage/project | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| ExclusiveWpf Desktop | 151 | 0 | 0 |
| Parallel Domain | 602 | 0 | 0 |
| Parallel Application | 1,052 | 0 | 0 |
| Parallel Infrastructure | 1,652 | 0 | 0 |
| Parallel Desktop | 2,146 | 0 | 1 |
| **Complete top-level set** | **5,603** | **0** | **1** |

The sole skip is `PersonalTradingJournal.Desktop.Tests.Rules.RulesViewTests.CompiledThemesNarrowHighDpiBindingsValidationHistoryAndWindowClose`. Its implementation, skip reason, gate exception and failure propagation are unchanged; the historical CLR crash is not fixed. No additional tests were quarantined. Tests for retired Trade UI actions were replaced with retirement/classification checks, while historical capture readers remain covered through explicit test-only fixtures. Child results are supporting evidence, not additional cases in these totals.

The local runner recorded 13:39:30–14:26:14 UTC on 16 November (46 minutes 44 seconds). ExclusiveWpf took about 6 minutes 53 seconds; parallel Desktop reported 39 minutes 41 seconds, while Infrastructure reported 5 minutes 26 seconds. The retained result establishes successful local completion, not the cause of the elapsed duration or hosted timing. The existing 20-minute hosted workflow budget is unchanged; a hosted run remains unverified. There were no remaining failed cases in this completed local gate.

Offscreen WPF rendering is not native visual acceptance. Live file-picker interaction, physical DPI, keyboard focus traversal, screen-reader announcements and real-window inspection remain pending. No production data, commit, push or merge is involved.

Pending manual acceptance with a disposable journal:

- Add a manual Trade, stage/view/remove screenshots, cancel, and then save another Trade with screenshots; confirm the native picker opens only on Upload and the successful Trade opens with its cards.
- Edit Description, view a historical screenshot, and exercise the guarded Remove confirmation without changing stored legacy metadata.
- Inspect Light/Dark at physical 100% and 250% scaling, including keyboard focus, screen-reader labels, narrow-window scrolling and the View action states. The existing dense economics overview is retained; the new execution table provides horizontal scrolling at narrow widths.

## Changed files

Exact worktree paths (including removals and test-only relocations):

- `docs/backup-restore.md`
- `docs/playbook.md`
- `docs/trades-workspace.md`
- `README.md`
- `src/PersonalTradingJournal.Application/Backups/IPortableExportService.cs`
- `src/PersonalTradingJournal.Application/Screenshots/AddTradeScreenshotUseCase.cs`
- `src/PersonalTradingJournal.Application/Trades/ConfirmTradePlaybookAssociation.cs`
- `src/PersonalTradingJournal.Application/Trades/CreateManualTradeCommand.cs`
- `src/PersonalTradingJournal.Application/Trades/CreateManualTradeUseCase.cs`
- `src/PersonalTradingJournal.Application/Trades/ManualTradeScreenshot.cs`
- `src/PersonalTradingJournal.Application/Trades/TradeDetail.cs`
- `src/PersonalTradingJournal.Application/Trades/UpdateTradeCommand.cs`
- `src/PersonalTradingJournal.Application/Trades/UpdateTradeUseCase.cs`
- `src/PersonalTradingJournal.Desktop/Resources/Controls.xaml`
- `src/PersonalTradingJournal.Desktop/Resources/Themes/DarkTheme.xaml`
- `src/PersonalTradingJournal.Desktop/Resources/Themes/LightTheme.xaml`
- `src/PersonalTradingJournal.Desktop/ViewModels/Trades/TradesViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Trades/TradesViewModel.Playbook.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Trades/TradesViewModel.Screenshots.cs`
- `src/PersonalTradingJournal.Desktop/Views/Calendar/CalendarInlineTradeView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Trades/TradeEntryForm.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Trades/TradePlaybookAssociationView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Trades/TradePlaybookAssociationView.xaml.cs`
- `src/PersonalTradingJournal.Desktop/Views/Trades/TradeScreenshotWorkspace.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Trades/TradeScreenshotWorkspace.xaml.cs`
- `src/PersonalTradingJournal.Desktop/Views/Trades/TradeSetupClassificationView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Trades/TradeSetupClassificationView.xaml.cs`
- `src/PersonalTradingJournal.Desktop/Views/Trades/TradesView.xaml`
- `src/PersonalTradingJournal.Domain/Trades/Trade.cs`
- `src/PersonalTradingJournal.Infrastructure/Backups/PortableExportSchema.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Mapping/TradePersistenceMapper.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261114204255_SimplifyTradeWorkspace.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261114204255_SimplifyTradeWorkspace.Designer.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/JournalDbContextModelSnapshot.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Records/TradeRecord.cs`
- `src/PersonalTradingJournal.Infrastructure/Rules/RuleEvidenceSnapshots.cs`
- `src/PersonalTradingJournal.Infrastructure/Rules/TradingRuleRepository.Automatic.cs`
- `src/PersonalTradingJournal.Infrastructure/Trades/TradeDetailReader.cs`
- `src/PersonalTradingJournal.Infrastructure/Trades/TradeMutationStore.cs`
- `src/PersonalTradingJournal.Infrastructure/Trades/TradePlaybookAssociationStore.cs`
- `src/PersonalTradingJournal.Infrastructure/Trades/TradePlaybookContextStore.cs`
- `src/PersonalTradingJournal.Infrastructure/Trades/TradeStore.cs`
- `tests/PersonalTradingJournal.Application.Tests/Trades/ConfirmTradePlaybookAssociation.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Calendar/CalendarViewLayoutTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Rules/ManualAssessmentEntryTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Trades/TradePlaybookAssociationTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Trades/TradePlaybookViewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Trades/TradeScreenshotPreviewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Trades/TradeScreenshotWorkflowTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Trades/TradesViewModelTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Trades/TradesViewPagingXamlTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Trades/TradeWorkspaceDraftTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Trades/TradeWorkspaceViewTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/NotebookImagePortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/NotebookPortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/PortableExportTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/SavedPeriodPortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookMigrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/DailyReview/DailyReviewPlaybookReaderTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/DailyReview/PeriodReviewComparisonIntegrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/DailyReview/PeriodReviewEvidenceReaderTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Initialization/JournalDatabaseInitializerTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integration/ProductionPersistenceIntegrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Legacy/TradePlaybookAssociationStore.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Legacy/TradePlaybookContextStore.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/LegacyTradeCaptureFixture.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/LegacyTradeSchemaFixture.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Migrations/InitialMigrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Migrations/TradeBrowseProjectionMigrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRuleEvidenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookImagePersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookPersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookRevisionDeletionTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/TradePlaybookAssociationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/TradeWorkspaceMigrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Trades/TradeWorkspacePersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/DailyTradingPrincipleTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/SharedTradingPlanTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/TradingPlanPortabilityTests.cs`
