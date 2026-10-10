# Desktop UI Architecture

## Purpose

`PersonalTradingJournal.Desktop` contains the Windows WPF presentation layer and the application's composition root. M4 established the shell, M5 added Accounts and Instruments, M6–M8 completed manual Trade capture, browsing, closure, and screenshots, M9 added Trading Setup and Trading Mistake classification, and the Desktop Theme System added persisted System/Dark/Light appearance. Later milestones added CSV import, Dashboard analytics, Calendar review and the M14.2 daily Journal editor. Remaining placeholder destinations are intentionally unimplemented.

This document explains how to extend the Desktop layer without moving trading logic or persistence access into the UI.

## Shell Structure

`MainWindow` is the permanent application shell. Its layout has three stable regions:

- a grouped navigation sidebar;
- a page header; and
- a content region that fills the remaining workspace.

The window defaults to `1280x800`, has a minimum size of `1000x650`, retains native Windows chrome, and uses a fixed 252-pixel sidebar. The sidebar and page content support intentional vertical scrolling, while horizontal overflow is disabled. The sidebar itself does not collapse or get replaced at smaller supported sizes; only its four feature groups can be collapsed independently.

`MainWindow.xaml.cs` handles constructor injection, `InitializeComponent()`, `DataContext` assignment and the native window-close hook. `OnClosing` delegates to `MainWindowViewModel.TryCloseWindow()`, allowing the Journal editor to protect unsaved text or an in-progress save. It contains no persistence or trading logic; navigation routing remains in the shell ViewModel.

## MVVM Boundary

The shell uses `CommunityToolkit.Mvvm` rather than custom MVVM infrastructure. ViewModels derive from `ObservableObject`, and shell navigation is exposed through `RelayCommand<NavigationDestination>`.

```text
MainWindow
  -> DataContext: MainWindowViewModel
       -> CurrentDestination
       -> NavigateCommand
       -> PageTitle
       -> CurrentContentViewModel
            -> ContentControl
                 -> implicit DataTemplate
                      -> View
```

`MainWindowViewModel` owns shell presentation state only. Accounts, Instruments, Trades, Journal, Trading Setups, Trading Mistakes, and Settings keep feature presentation behavior in their own ViewModels while Domain rules and persistence access remain behind Application-layer boundaries and use cases.

## Navigation

`NavigationDestination` is a Desktop-only enum that represents shell destinations. It has no domain meaning and is not persisted.

The current navigation order is:

- Dashboard
- Notebook
- **Trading:** Trades, Journal, Calendar, Import
- **Analysis:** Performance, Setups, Mistakes, Breakdown
- **Planning:** Playbook, Trading Plan, Rules
- **Review:** Daily Review, Weekly Review, Monthly Review
- Accounts
- Instruments
- Settings

There are 19 destinations in total.

`MainWindowViewModel` builds one deterministic navigation catalog for the 19 destinations. `TopNavigationItems` contains Dashboard and Notebook; `NavigationSections` contains the collapsible Trading, Analysis, Planning, and Review groups; and `BottomNavigationItems` contains Accounts, Instruments, and Settings below a semantic divider. `MainWindow.xaml` renders these collections through shared item and section templates instead of repeating one Button block per route.

`CurrentDestination` remains authoritative routing state. The catalog's item-level `IsSelected` values are synchronized from it so exactly one destination receives selected styling. Every item passes its typed destination through `CommandParameter` to the existing centralized `NavigateCommand`; section headers only toggle their own `IsExpanded` state and never navigate. Groups start expanded, retain their state for the main-window session, and automatically expand if navigation targets one of their children.

Project-owned vector geometries live in `Resources/Icons.xaml`. Navigation items carry only semantic icon resource keys, and one Desktop converter resolves those keys for the shared template. Icons inherit the same normal, hover, selected, disabled, and focus-aware foreground behavior as their labels; no external icon font, bitmap asset, or package is required.

There is intentionally no `NavigationService` or `INavigationService`. `MainWindowViewModel` is currently the only owner and initiator of shell navigation, so a separate service would add indirection without protecting a real boundary. Introduce one only if another ViewModel later needs to initiate cross-feature navigation.

## Content Hosting

`MainWindow` does not create or switch Views manually. It binds a stretching `ContentControl` to `CurrentContentViewModel`. The active ViewModel therefore determines the rendered content without placing View construction in a ViewModel or code-behind.

Repeated navigation to the current destination is ignored. This preserves the current content instance and its current transient state, and avoids unnecessary View recreation.

`MainWindowViewModel` retains the injected Dashboard, Calendar, Trades, Journal, Import, Accounts, Instruments, Trading Setups, Trading Mistakes, and Settings ViewModels for the main-window lifetime. Navigating away and returning reuses those exact feature instances; Calendar rereads its selected month on re-entry, while the existing retained feature lists/reference caches keep their established loading behavior. Entering Accounts, Instruments, Setups, Mistakes, Trades, or Import from another destination explicitly resets feature-local transient presentation state: open detail/edit/create surfaces, unsaved drafts, selected import files, generated previews, validation state, and operation feedback do not reappear on re-entry. Journal resets to today's New York date and All accounts on accepted departure/re-entry, clears its editor and History pages/selections, and reloads that exact entry. Its discard guard runs before departure; a veto preserves all state. Dashboard, Calendar, Settings, placeholders, and same-destination clicks keep their existing behavior.

Before navigating away from Journal, the shell calls `JournalViewModel.TryLeave()`. Dirty text requires discard confirmation; declining keeps both the Journal page and its draft. The same guard runs before window close. While Save is in progress, both transitions are blocked until it finishes or cancellation completes. Successful departure deactivates Journal and cancels its outstanding read. Clicking the already-selected Journal destination does not reload or discard text.

## ViewModel-to-View Mapping

`App.xaml` contains implicit `DataTemplate` mappings:

```text
DashboardViewModel   -> DashboardView
CalendarViewModel    -> CalendarView
TradesViewModel      -> TradesView
JournalViewModel     -> JournalView
AccountsViewModel    -> AccountsView
InstrumentsViewModel -> InstrumentsView
ImportViewModel      -> ImportView
TradingSetupsViewModel -> TradingSetupsView
TradingMistakesViewModel -> TradingMistakesView
SettingsViewModel        -> SettingsView
PlaceholderViewModel -> PlaceholderView
```

WPF resolves these mappings from the runtime type of `CurrentContentViewModel`. Future feature mappings should follow the same pattern unless a concrete requirement justifies a different presentation mechanism.

## Placeholder Policy

Ten destinations—Dashboard, Calendar, Trades, Journal, Import, Accounts, Instruments, Setups, Mistakes, and Settings—have concrete content. The remaining nine destinations share `PlaceholderViewModel` and `PlaceholderView`. This avoids empty feature-specific View/ViewModel pairs that would contain no state or behavior.

Replace a placeholder only when its destination gains real presentation state and an Application use case. Until then, placeholder content is an accurate representation of product status, not missing architecture.

## Journal Editor, Daily Review, Calendar Integration and History (M14.2–M14.6)

The Journal page edits one explicit New York date and exact Account scope through `IDailyJournalRepository`. The date defaults to today in New York; All accounts is its own entry, and inactive Accounts remain selectable with inactive labels. Days without Trades are valid. Unavailable Account selections keep their original IDs and a visible unavailable state, with writes blocked.

The page defaults to compact saved-entry content and expanded Review History. Top-level **Add Journal** or a saved draft's **Continue Journal** explicitly opens the multiline form. The freeform field is 120 DIPs high and the three answer fields are 72 DIPs high, with internal scrolling. Standalone and inline editors have one **Save Journal / Cancel** row below all fields and no Complete review button. Save or **Ctrl+S** sends exact text and all answers together as Completed. Journal text requires a Unicode letter or digit; answers are optional. An inline field message explains a rejected completion and preserves edits. Answers-only historical Completed entries remain readable. Success closes the form; failures/cancellation/conflicts retain it and every local value with Reload recovery. Cancel saves exact fields as Draft and closes only after success, without a discard prompt. An exactly empty new form closes without a write; errors/conflicts retain fields. Other navigation/close paths keep their discard guards. Accepted standalone departure/re-entry resets to today in New York and All accounts with the editor and History selections/pages reset; a veto preserves all state. Inline Calendar scope never resets to today. Previously Completed reviews stay compact/read-only until **Reopen review** commits a draft revision and opens the form; stored completion history remains intact. All write actions share submission/concurrency guards; over-limit content stays visible with writes blocked. There is no autosave. Calendar uses Draft or a check mark with an accessible Completed label, never altering financial data.

M14.6 adds **Review History** below the selected entry/editor. All accounts aggregates all account scopes plus null-scoped entries; an individual Account filters exactly. Entry pages contain 10 rows, ordered by New York date descending then ID, with count/filter/paging in SQL. Open review expands beneath its own row without changing the History filter, page, count, order or editor scope. Open in editor explicitly selects the row's original date/Account through the unsaved-change guard. Creation, the selected-day card and Calendar indicators keep exact-scope semantics. Revision pages contain up to 20 rows; View revision reads one immutable snapshot with New York audit time and explicit UTC-4/UTC-5. Viewing never saves/restores or replaces local drafts. Cancellation/generation checks reject stale responses, and Close view/Close review stay distinct. Add/Continue use mint/teal accents; Open review, Open in editor and View revision use distinct amber/slate/sky styles. The opened row also reads exact-scope current content for compact previews. Confirmed red Delete revision removes an older snapshot only; the current revision is protected, root content/token stay unchanged, and remaining numbers may have gaps. See [Daily Journal](daily-journal.md#review-history-m146).

Date/Account changes and explicit reload require confirmation before discarding dirty text or answers. Failed or cancelled operations retain every local value. A conflict, duplicate creation or missing entry preserves local content and blocks another write until a successful explicit reload; no automatic overwrite or merge occurs. Asynchronous reads capture scope and reject obsolete results, and write operations are single-flight. The ViewModel uses Application contracts and the Desktop dialog service without accessing EF Core or Trade data.

The view uses shared semantic Light/Dark brushes, form controls and calendar styling. Its date picker accepts culture-aware input and blocks Save while input is invalid or differs from the committed selected date. Labels, automation names, status/error live regions and keyboard access describe the scope and operation. Tab moves out of the multiline field; Ctrl+S invokes Save. Wrapping scope/action controls and vertical scrolling keep the page reachable at narrower sizes. These implementation details do not establish interactive visual or accessibility acceptance.

Calendar Day Performance offers **Add Journal**, **Continue Journal** or **Open Journal**, based on a bounded exact-scope status read. It opens an inline Journal between chart and Trades without closing the modal or changing navigation. A separate `JournalViewModel` reuses the existing revisioned commands and protects the standalone page's retained draft; it does not load duplicate Trade context or Review History. Save collapses the form, while failures preserve all fields. Cancel saves Draft before closing its form; Close Journal retains its unsaved-change guard. Both leave Day Performance open; modal closing protects both Journal and Trade edits. Calendar month, date and filters stay unchanged. Compact Draft/Completed markers use the current Account, not currency, and committed Journal changes refresh only status. Calendar grid cells never display Journal text or answers. See [Daily Journal](daily-journal.md) for persistence and concurrency contracts.

M14.3 adds `JournalTradeContextViewModel` and a read-only panel below the editor. The existing Calendar day reader supplies date/Account-filtered closed Trades and separate-currency metrics; existing Calendar presentation supplies time, peak Size, Effective Net and historical classifications. Its own cancellable, generation-guarded read state refreshes after committed Trade/import changes without reloading or discarding journal text. Refresh Trades works independently of Save/Reload latest. Empty/error context remains distinct from the editable Journal. Rows are keyboard-focusable with complete accessible descriptions, shared Light/Dark outcome styling, wrapping/tooltips and narrow-table horizontal scrolling; vertical wheel input reaches the page. The panel has no View/Edit action.

## Dashboard Status

The Dashboard is presentation-only. It contains empty visual regions for:

- Net P&L
- Win Rate
- Profit Factor
- Avg R
- Equity Curve
- Daily P&L
- Trading Setups
- Recent Trades

The four metric values display `—` rather than fake zeroes or sample financial data. `DashboardViewModel` is intentionally empty, and the Dashboard performs no analytics or database queries. Data loading and real calculations are deferred.

Dashboard metrics describe financial and statistical results; they must not infer process quality from profit or loss. Good process can lose, and bad process can profit. Future process-quality analysis must be modeled explicitly.

## Tradovate Import

Import has a read-only review phase followed by explicit transactional confirmation. It uses one vertically scrolling page with source-file selection, explicit Trading Account selection, analysis summary, Instrument resolution, Trade candidates, staged diagnostics, and confirmation/result feedback. Selecting a CSV immediately runs parsing, reconstruction, and Instrument resolution; selecting an Account is not required until `Build Preview`. Account options include type, currency, and available provider/external identity, allowing duplicate names to remain distinguishable. Inactive Accounts remain selectable for historical data and are labeled as inactive.

The picker returns only the filename and stream. The ViewModel closes the stream after parsing and does not retain raw CSV content or a full path. Preview times are labeled and rendered in `America/New_York`, while the summary makes the `Europe/Sofia -> UTC -> America/New_York` policy explicit. Unknown Instrument metadata, invalid or ambiguous source time, missing Accounts, and every upstream diagnostic remain visible without silently inventing values.

Instrument cards show display name, asset class, exchange, currency, Tick Size, and Tick Value. Existing values come from the PTJ Instrument snapshot preserved during resolution; proposals use the verified creation proposal and explicitly state that creation waits for confirmation. Unresolved mappings/metadata block confirmation; the page does not offer an arbitrary mapping editor or silently choose among ambiguous Instruments. `Build Preview` reruns resolution so catalog corrections can be reviewed. Candidate weighted average prices display exactly two culture-aware decimal places without changing preview data. Source-reported P&L is evidence only. Commission/fee and completeness warnings remain non-blocking; imported costs and Net P&L stay unknown. Account changes invalidate the generated preview but preserve file analysis. Navigating away and back clears the file workflow while preserving loaded Account options.

`Import Trades` requires a current valid preview and opens the shared confirmation dialog with Account, file, counts, and warnings. Cancel performs no import and retains the preview. A completed `Imported` or `No changes` outcome clears the active preview and intermediate analysis/resolution/candidate/diagnostic sections, retains outcome counts, and changes `3. Confirm import` to `Confirm import`. The source section remains ready for another file, and the completed preview cannot be submitted again. Imported Trades/Instruments invalidate shell caches for subsequent authoritative reads; duplicate-only replay does not raise a write event. Duplicate counts are available after confirmation, not precomputed in the read-only preview.

Blocked results retain the analysis and relevant recovery information but disable stale confirmation. `REFERENCE_DATA_CHANGED` requires rebuilding; a missing Account refreshes options and requires reselection. Unsafe duplicate overlaps remain blocked. Operation cancellation and unexpected failure restore usable state, and command/submission guards prevent concurrent acceptance. See [Tradovate CSV Import](tradovate-csv-import.md) for source constraints, transaction semantics, and limitations, and [M10 acceptance](m10-acceptance.md) for verification status.

## Accounts Feature

### Current Balance

The table places Current Balance directly after Starting Balance. For each exact Account, across all recorded history, its formula is:

`Starting Balance + sum(fully closed Trade Gross in Account currency) - sum(each recorded commission and fee component on those Trades)`

Gross comes from the existing Domain-derived projection. A fully closed lifecycle contributes its allocated entry and exit costs, including existing import/reversal allocations; the view does not prorate, multiply or allocate them again. Open Trades, including partially exited positions, contribute neither Gross nor costs until fully closed. Other-currency closed Trades are excluded with a visible count, never converted or combined. No date filter or timezone rebasing is introduced.

When any included execution commission or fee is unknown, the numeric result is prefixed **Estimated** in every color. Visible supporting text reports affected Trades and missing commission/fee entry counts (not monetary amounts). The focusable value's accessible name and tooltip explain coverage and exclusions. Known components are deducted even when another component is missing. This is deliberately not Calendar's Effective Net fallback, which uses Gross when strict Net is unavailable; neither strict Net nor Calendar economics changes. With all costs known there is no Estimated prefix. Above/below/equal to Starting Balance uses the theme's green/red/neutral text respectively.

No Trades means exactly Starting Balance. A missing Starting Balance is not silently assumed zero. Missing required Gross/projection/execution facts and arithmetic overflow report an explicit Unavailable reason rather than a partial numeric total. A page-level note identifies this as a Trade-based calculation, not a broker balance, excluding unrecorded deposits, withdrawals, payouts and other broker adjustments.

Trade create/edit/close/delete, committed Tradovate/Topstep imports, and Delete All Trades invalidate the read; visible Accounts refresh immediately and inactive Accounts refresh on return. An invalidation during a pending read discards that snapshot and rereads. Account currency/Starting Balance edits use the existing authoritative reload. Read failures expose Refresh without replacing missing data with zero. Only Accounts requests the balance-enriched batch; Account selectors remain lightweight.

The table shares a finite viewport/minimum width for headers and rows so wrapping coverage cannot resize only one row's columns. At narrow widths the table scrolls horizontally, with normal vertical wheel gestures forwarded to the page; keyboard focus can bring the Current Balance or row actions into view. Automated Light/Dark checks cover alignment, value colors/Estimated text, accessible names, and scroll reachability at 1280 DIP/96 DPI and 480 DIP/240 DPI. Physical DPI, screen-reader output and live keyboard/mouse interaction remain separate manual checks.

### Account Management

Accounts performs a lazy initial load on first navigation and provides an explicit Refresh command. Its inline Add Account form captures name, account type, provider, external account ID, currency, and optional Starting Balance. Account Type defaults to Personal as a presentation convenience, and creation is coordinated by `CreateTradingAccountUseCase`.

Each persisted row displays active or inactive status and offers View, Edit, and a compact overflow menu. The detail panel shows the complete persisted account metadata and audit timestamps. Edit reuses the shared form language for Name, Account Type, Provider, External Account ID, Currency, and optional Starting Balance; it cannot change identity, creation time, or historical Trades. Starting Balance remains reference data; Current Balance is calculated for the table, never an editable or stored field.

The overflow menu shows the applicable Activate or Deactivate action, then **Delete All Trades**, immediately followed by **Delete**. Both destructive actions use danger coloring; bulk Trade deletion has a distinct Trade icon and emphasized label, while Account deletion retains its trash icon. Account Delete remains independently guarded: unused accounts are hard deleted; references from Trades or Journals prevent deletion rather than cascading into those records.

Delete All Trades prepares a read-only confirmation for the exact Account, showing its name and Trade count. Zero Trades reports that nothing changed. The safe-default Cancel button writes nothing. Confirmation explains permanent deletion of Trade-owned executions, classifications/mistake assignments and screenshots, removal of import provenance (the same source can subsequently be re-imported), and preservation of the Account, Journals, catalog definitions and saved AI snapshots. A changed Account or Trade graph stops deletion with a refresh-and-confirm-again message; the operation disables overlapping Account actions, including while the confirmation is open.

After commit, Account data reloads, cached Trades are invalidated, and the existing committed-Trade notifications refresh active Dashboard, Calendar, Journal Trade context and Daily Review current evidence; inactive pages refresh on activation. Historical snapshots retain their original evidence and unavailable-source handling for deleted Trade IDs. File cleanup is post-commit and may leave orphan files on failure; the completion dialog reports the failed cleanup count instead of claiming those files were removed. Successful writes refresh the authoritative list, while local detail/list state prevents a completed Account update or delete from appearing stale if that refresh fails.

Automated bulk-deletion layout checks cover the compiled row menu and shared confirmation in Light/Dark at 960 DIP/96 DPI and 480 DIP/240 DPI: exact Account binding, adjacent menu actions, reachable nonoverlapping buttons, logical keyboard focus on Cancel and danger styling. Automated renders are not live mouse/keyboard, screen-reader or physical-monitor DPI acceptance.

## Instruments Feature

Instruments performs a lazy initial load on first navigation and provides an explicit Refresh command. Its inline Add Instrument form captures canonical symbol, display name, asset class, exchange, currency, Tick Size, and Tick Value. Asset Class defaults to Futures as a presentation convenience, and creation is coordinated by `CreateInstrumentUseCase`. Each row now exposes shared View, Edit, and overflow actions. View presents authoritative detail and audit timestamps; Edit reuses the shared form language and preserves active state.

Tick Size and Tick Value accept current-culture decimal formatting with an invariant `.` fallback. Point Value is read-only and derived from those inputs, including a deterministic edit preview; it is not independently editable. Only the applicable Activate or Deactivate action is shown. Unused Instruments may be deleted after destructive confirmation, while referenced deletes show safe guidance to Deactivate instead. Referenced Instruments may update metadata and tick economics without changing historical Trade snapshots, but Asset Class changes are blocked because that field controls quantity semantics. Contract-specific futures symbols, expiration, contract month, and rollover are not supported by this feature.

## Trades Feature

Trades independently loads purpose-specific reference data and the first server-paged Trade list on first navigation, caching each successful result. Refresh requests both again while keeping their success and failure outcomes independent, so a failure in one read does not discard fresh data from the other. Normal Desktop entry lists active Accounts and Instruments only. Neither selector nor Direction is chosen automatically, and selector `PointValue` is presentation metadata rather than authoritative persisted economics.

Add Trade opens an inline form organized into Reference, Trade, Entry, and conditional Exit sections. It captures:

- an explicit Trading Account and Instrument;
- an optional active Trading Setup;
- Long or Short direction and decimal Quantity;
- an opening execution timestamp, price, commission, and fees; and
- optionally, one full closing execution with its own timestamp, price, commission, and fees.

Execution timestamps are entered explicitly as `America/New_York` wall-clock time and accept only `yyyy-MM-dd HH:mm:ss` or `yyyy-MM-dd HH:mm`. A trailing `Z`, an explicit offset, and culture-specific dates are rejected. Desktop converts each value through the shared trading-time policy before constructing the Application command; nonexistent spring-forward times and ambiguous fall-back times are rejected with specific validation. When Edit displays an already persisted fall-back-overlap time and that generated text remains unchanged, it retains the execution's known UTC instant instead of resolving the ambiguous wall clock again. The resulting command and Domain market facts remain zero-offset UTC and never depend on the Windows local timezone.

Manual decimal values parse with `CurrentCulture` first and `InvariantCulture` with `.` as a fallback. Leading/trailing whitespace, a sign, and a decimal point are supported; thousands separators, exponents, and currency symbols are rejected. Quantity must be greater than zero. Futures require whole contract quantities, while other asset classes allow positive decimals. Prices may be zero or negative when syntactically valid. Blank Commission or Fees becomes zero; otherwise each cost must be non-negative.

`TradesViewModel` validates raw presentation values in deterministic form order and builds `CreateManualTradeCommand`. `CreateManualTradeUseCase` then reloads the authoritative Account, Instrument, and optional Trading Setup, requires a selected Setup to exist and be active, constructs a `TradePricingSnapshot` from authoritative Instrument data, creates the Trade through Domain APIs, and persists it through `ITradeStore`. Manual creation does not assign Trading Mistakes. Desktop never accesses an Infrastructure store or `JournalDbContext` directly.

The M6 form supports one opening execution plus an optional full closing execution. This is a deliberately simple capture workflow; the Domain remains capable of scale-in and partial scale-out.

After successful persistence, the draft resets, the form closes, and "Trade saved successfully." is shown. Desktop resets browsing to page 1 ordered by canonical `OpenedAtUtc` descending, performs a best-effort authoritative page reload, and never fabricates a local row. If that post-commit reload fails, the successful write and success feedback remain intact, the existing rows stay visible, and `TradeListErrorMessage` directs the user to Refresh. The reload is not treated as part of the Save failure outcome and does not inherit cancellation from the completed Save command.

Trade operation feedback remains separated:

- `ErrorMessage` reports reference-selection/read problems;
- `ValidationErrorMessage` reports invalid manual input;
- `SaveErrorMessage` reports a validated Trade that could not be saved; and
- `SuccessMessage` reports completed persistence.

Validation failure makes no persistence attempt and retains the draft. Technical persistence failure also retains it. A stale Account or Instrument produces safe stale-reference feedback without discarding input. Cancellation propagates, retains the draft, and is not transformed into failure or success feedback. While saving, Save, Cancel, Refresh, and opening another form cannot start competing operations; this is explicit `TradesViewModel` state rather than a generic operation coordinator.

### Trade Browsing

The Trade surface uses fixed 20-row pages over the complete persisted dataset. SQLite performs the count, selected ordering, `Skip`, and `Take`; Desktop retains only the current page. The initial order is canonical `OpenedAtUtc` descending, while displayed Trade times are converted to New York. Previous and Next are shown only when more than one page exists, are disabled at their boundaries and during a page load, and display the current page plus total Trade count.

Opened (New York), Trade, Account, Average Prices, and Net P&L are clickable sort headers; Size and Actions are not sortable. Only the active header shows an up/down arrow. New columns default to ascending except the canonical `OpenedAtUtc` sort and Net P&L, which default to descending; clicking the active column toggles direction and every sort returns to page 1. Average Prices sorts by exact average entry price and Net P&L by exact numeric outcome with null values always last. Every order ends with Trade ID ascending for stable page boundaries. Size is peak simultaneous absolute position quantity derived from that Trade's executions, including reversal allocations; it is distinct from cumulative entries and remaining Open Qty. Whole contracts omit redundant decimals, while fractional quantities retain precision.

Sortable headers use a local border-free button template rather than changing the global button style. The active column keeps a theme-aware elevated background in either direction, inactive columns remain neutral, hover feedback is temporary, and keyboard focus remains available. The existing header/card Grid alignment is unchanged.

Each row remains a distinct rounded card. Row tint follows known Net P&L, falling back to known Gross when Net is unavailable; zero/null remains neutral. Gross and Net amounts independently use their own positive/negative semantic colors and currency. Unknown Net displays `—` with commission/fees guidance, while known zero remains numeric. Green Gross styling does not imply positive Net after unknown costs. Each row offers View and a compact More menu containing Edit and destructive Delete. Account name and Instrument symbol are current labels; lifecycle and economics remain authoritative. Average prices display exactly two culture-aware decimal places without rounding stored values.

Header and row grids use matching columns. Opened reserves 126 DIP for the complete heading plus sort indicator; Account reserves 94 DIP and wraps within two 18-DIP lines, with ellipsis for excess text and a full-name tooltip; Size uses 56 DIP and Actions 122 DIP. Trade and Average Prices flex above their minimums. The table keeps an 884-DIP minimum inside its own horizontal scroller, preserving access to all fields at narrower viewports. Date/time, prices, P&L, and status retain their existing formatting; the unknown-cost explanation wraps.

Refresh preserves page and sort, correcting to the highest valid page if external changes made the requested page invalid. Edit and Close preserve the current browse state and accept that a changed row may move to another page. Delete reloads the current page and explicitly retries the prior/highest page when the last row disappears. Trading Setup, Mistake, and screenshot-only changes do not trigger an unnecessary list reload. Navigating away clears transient detail, form, and preview state while retaining the loaded page, count, and sort.

### Trade Detail

View loads the selected Trade through `ITradeDetailReader` and opens the authoritative detail surface. Its facts remain read-only; Edit and destructive Delete are explicit actions rather than inline setters. `TradesViewModel` exposes `SelectedTradeDetail`, `IsTradeDetailVisible`, `IsTradeDetailLoading`, `IsTradeDetailNotFound`, and `TradeDetailErrorMessage` for the selected projection and its loading, missing, and technical-error outcomes. Close clears detail state without clearing the loaded page. Returning to Trades also closes prior detail and related edit/screenshot/mistake state while preserving page, sort, rows, total count, and reference data.

The detail presents current Account and Instrument identity labels; direction and status; current optional Trading Setup; opened and optional closed timestamps in New York; open quantity; total costs; average entry and optional average exit prices; gross and net P&L; and the historical pricing point value and currency. Gross and Net P&L use the same positive/negative/zero/null semantic foreground rules, with Net P&L given slightly stronger typographic emphasis. An open partially exited Trade may have a non-null average exit price while gross and net P&L remain null under Domain semantics.

The Setup section displays `—` when unclassified, marks an inactive historical Setup neutrally, lists active replacement options, and supports assign/change/clear through the Application use case. Successful mutations reload authoritative detail; safe section-local feedback distinguishes validation, persistence, and reload outcomes.

Trading Mistakes are displayed as separate assigned observations with optional Notes. The picker excludes already assigned items and permits only active catalog definitions; inactive historical assignments remain visible and removable. Add and Remove use independent Application workflows, reload authoritative assignments and options, and never infer mistake severity or trade quality from P&L.

Open Trades expose a close workflow that accepts a New York exit time, converts it to canonical UTC, and appends the full opposite-side execution for authoritative remaining quantity. Trade Detail also hosts screenshot add/list/preview/delete workflows. These operations retain their own loading, validation, success, error, and post-write reload states so unrelated sections do not overwrite one another.

Executions are shown individually in ascending sequence order as the complete lifecycle: sequence, execution timestamp in New York, side, quantity, price, commission, fees, total costs, and optional broker symbol, external execution ID, and external order ID provenance. Unknown commission, fees, or total costs render as an em dash rather than zero. Underlying detail DTO values remain UTC. The UI does not collapse scale-in or partial-exit history into an entry/exit pair.

### Trade Edit and Delete

Edit reuses the Manual Trade form language and prepopulates authoritative Account, Instrument, Direction, optional Setup, quantity, timestamps, prices, commission, and fees from `TradeDetail`, including execution identities rather than formatted display strings. Persisted UTC execution instants are formatted as New York input and convert back to the same UTC instants when unchanged; imported Sofia source timestamps are not shown in the manual edit fields. A Trade containing any unknown execution commission or fee is refused rather than converting that unknown value to zero. The selector lists expose active alternatives plus an inactive Account, Instrument, or Setup already assigned to the Trade; other inactive records cannot be newly selected. Switching Long/Short is translated into corrected opening/closing execution sides. The current UI deliberately edits the one-entry/optional-full-exit manual shape and refuses to flatten a richer multi-execution lifecycle into that form.

Saving calls `UpdateTradeUseCase`, which corrects immutable executions and lets the Domain recalculate status, exposure, averages, costs, and P&L. The same Instrument retains its historical pricing snapshot, while an Instrument change rebuilds it and revalidates quantity semantics. Existing execution IDs and hidden broker/import provenance are preserved when their logical execution remains. Setup may be kept, changed to an active Setup, or cleared. Trade Mistake assignments and screenshot metadata/files remain unchanged. Cancel performs no write and returns to the existing detail; success reloads authoritative detail and the current Trade page without clearing screenshot or mistake state.

Delete uses the shared Danger confirmation and identifies the Trade by Instrument and opened New York timestamp. A confirmed hard delete permanently removes the Trade plus its execution rows, Trade Mistake associations, and screenshot metadata, then attempts physical screenshot cleanup. Success clears stale detail, screenshot, mistake, and selection state and refreshes the current Trade page, correcting an invalid last page when necessary. A post-commit file cleanup failure is reported as a warning rather than presented as a failed database delete; cancellation at the confirmation performs no work.

## Trading Setup and Trading Mistake Catalogs

Analysis → Setups opens the Trading Setups catalog, and Analysis → Mistakes opens the Trading Mistakes catalog. Both pages retain lazy list loading, explicit Refresh, inline Name/optional Description creation, active/inactive status, and reversible Activate/Deactivate actions. Inactive records remain visible for historical context.

Trading Setup and Trading Mistake rows additionally provide View, Edit, and an overflow menu with the one applicable lifecycle action plus Delete. Their compact details show description, status, and audit timestamps. Edit allows normalized Name/Description changes even when referenced because historical records retain stable catalog Ids. Unused catalog entries may be hard deleted after safe-default destructive confirmation; referenced deletion is blocked with guidance to Deactivate instead. A Mistake reference is determined through the separate `TradeMistake` association, which is never removed or rewritten by catalog edits. Deactivation preserves historical review data, while only active Setups and Mistakes remain eligible for new assignment.

Create actions require a non-blank Name and are blocked while saving. Opening and cancelling a form reset its draft according to the established catalog behavior. Duplicate names and other validation failures remain near the form; successful writes close/reset the form and trigger a best-effort authoritative list reload without reclassifying a completed write as failure.

## Feature Operation and Error State

Accounts, Instruments, Trades, Trading Setups, and Trading Mistakes explicitly prevent overlapping major operations appropriate to each feature. This coordination remains per-feature ViewModel state rather than a generic operation coordinator.

Accounts, Instruments, Trading Setups, and Trading Mistakes keep separate feedback for:

- list/read errors;
- create and field-validation errors;
- edit errors;
- activate/deactivate errors; and
- row-action errors such as missing details or failed deletion.

After a successful catalog create, update, lifecycle, or delete write, the corresponding ViewModel reloads its authoritative list projection. If that reload fails, the successful mutation is not reported as a write failure; the existing list is retained or corrected locally where required, and a list-level refresh warning directs the user to retry Refresh.

Trades uses the separate read, validation, save, and success states documented in the Trades Feature section.

## Feature Page Scrolling

Accounts, Instruments, Trading Setups, and Trading Mistakes each use one page-level vertical `ScrollViewer`, containing the toolbar, inline creation form, feedback, and rows. This keeps the whole workflow reachable at reduced height without a nested list scrollbar. Horizontal scrolling is disabled, and tab order follows the visible form/action sequence.

This is appropriate for the current reference-data lists, but it is not a universal requirement for future large or virtualized datasets.

Trades likewise uses one page-level vertical `ScrollViewer`, with horizontal scrolling disabled at the page level. The paged list has its own horizontal `ScrollViewer` around both headers and rows. At narrow widths, scroll the page down to the table's horizontal scrollbar to reach columns beyond the viewport. The toolbar, manual form, conditional Exit section, Trade Detail, execution lifecycle, action controls, and feedback surfaces remain in the page's vertical region.

## Design System

Shared Desktop resources live under `Resources/`. `Typography.xaml`, `Spacing.xaml`, and `Controls.xaml` define one shared text hierarchy, layout scale, control templates, navigation states, and ScrollBar behavior. `Themes/DarkTheme.xaml` and `Themes/LightTheme.xaml` contain concrete values for the same project-prefixed semantic color and brush keys.

Theme-sensitive brush consumers use `DynamicResource`, so the current visual tree updates when `ThemeService` replaces the single active theme dictionary. Immutable styles, fonts, spacing, corner radii, and converters remain `StaticResource`. Views should use those resources instead of scattering hard-coded theme colors or duplicating styles.

Dark retains the established hierarchy and palette. Light uses distinct background, surface, raised-surface, border, text, accent, success, warning, and danger values chosen for readable hierarchy rather than mechanical inversion. Existing warning semantics remain canonical for safe user-facing operation errors; `PtjDangerBrush` remains available for destructive/danger meaning.

### Shared Entity Action and Dialog Language

Entity list and detail workflows share a presentation vocabulary without sharing business CRUD logic. View is the primary row interaction, Edit is a lightweight normal action, and the compact ellipsis trigger opens a themed action menu when more choices are available. Entity-specific ViewModels remain responsible for deciding which commands exist and whether they are enabled. The reusable ContextMenu behavior opens from mouse or keyboard activation, stock menu navigation handles arrows and Escape, visible labels accompany centralized View, Edit, Activate, Deactivate, and Delete icons, and disabled commands remain visibly disabled.

Activate and Deactivate are reversible lifecycle actions and use neutral secondary styling. Delete is separate, uses the existing Danger semantic, and is communicated by its trash icon and text label as well as color. The shared read-only detail language provides label/value text styles and a compact Active/Inactive status badge surface; it is a visual foundation, not a property-grid or metadata-driven editor.

`IDialogService` is the Desktop-only modal boundary. `ConfirmationDialogRequest` supplies entity-specific title, message, confirm/cancel labels, and destructive intent; `InformationDialogRequest` supplies a title, message, and close label for outcomes such as a delete blocked by historical references. Destructive confirmation uses the Danger button style while Cancel owns the safe default focus, Escape cancels, closing the window cancels, and the destructive button is never the implicit Enter action. The existing screenshot deletion confirmation adapts to this service, so ViewModels remain testable without constructing a WPF window or calling `MessageBox`.

Account, Instrument, Trading Setup, and Trading Mistake hard-delete enforce entity-specific integrity rules outside Desktop presentation: referenced records are retained and can be deactivated, while unused records may be deleted. Trade hard-delete uses the same explicit confirmation language but intentionally removes its owned/dependent records and performs post-commit screenshot file cleanup; no generic CRUD mechanism is used.

### Shared Form Language

The Manual Trade, Account, Instrument, Trading Setup, and Trading Mistake creation workflows use a shared compact form language from `Controls.xaml`. Form sections reuse the standard card surface, corner radius, border, and internal spacing; section titles, field labels, optional markers, helper text, and status text use consistent typography. Required fields use normal labels and existing validation, while optional fields carry an explicit muted `(Optional)` marker.

TextBox and ComboBox styles provide consistent sizing, an 8-pixel corner radius, restrained zero-depth border shadow, hover contrast, and an accent keyboard-focus border and glow. Existing validation branches identify the specific failed creation field after validation is attempted; that control uses a semantic Danger border and subtle glow until its value becomes valid, while the inline message remains the non-color cue. Cross-field, authoritative, and operation failures remain next to the action area. Error and success messages use the shared Danger and Success semantics with text.

Each workflow retains one primary create/save action and styles an existing Cancel action as secondary. Busy text and command gating remain owned by the existing ViewModels. Manual Trade is grouped by Trade Details, Position, Entry Execution, and optional Exit Execution; the lighter catalog forms keep their smaller layouts while sharing the same label, helper, validation, and action hierarchy.

`PtjStaticResourceTests` checks both static and dynamic project-owned references. Theme resource tests additionally require identical Dark/Light key sets and require every dynamic theme key to exist in both dictionaries, preventing late runtime lookup failures during switching.

The Settings page exposes only Appearance with System, Dark, and Light choices. System follows the current Windows application appearance and supported changes while PTJ is running. The selected preferred mode is persisted to the centralized local `settings.json`; its resolved effective Dark/Light value is not substituted. A save failure leaves the selected visual theme active and presents a safe message. Startup restores and resolves the preference before showing `MainWindow`, while missing, malformed, or unknown settings safely fall back to System.

The compact upper-right header toggle reflects the effective Dark/Light appearance and applies the explicit opposite theme immediately. Clicking it while System is preferred therefore creates an explicit override; Settings is the place to return to System. The rounded sun/moon pill adapts the supplied visual reference to PTJ's existing semantic resources and control hierarchy without external assets.

## Accessibility and Window Behavior

Navigation uses native WPF Buttons, so destinations and collapsible group headers participate in natural Tab order and support Enter and Space activation. Destination labels and group titles provide explicit accessible automation names; decorative vector icons do not replace those text labels.

Keyboard focus and active selection are independent visual states: focus has a visible outline, while the active destination uses an elevated background, accent indicator, primary foreground, and semibold label. Sidebar and page content remain vertically reachable through mouse, scrollbar, and keyboard scrolling at the minimum supported window size.

## Dependency Rules

- Desktop may depend on Application abstractions and use cases.
- Desktop references Infrastructure because it is the composition root that wires implementations.
- Feature ViewModels must not query `JournalDbContext` or EF Core directly.
- `AccountsViewModel` depends on account-specific list/detail reads and create, update, delete, and active-lifecycle use cases plus the Desktop dialog boundary.
- `InstrumentsViewModel` depends on instrument-specific list/detail reads and create, update, delete, and active-lifecycle use cases plus the Desktop dialog boundary.
- `TradingSetupsViewModel` and `TradingMistakesViewModel` depend on purpose-specific Application catalog readers and create/detail/update/delete/lifecycle use cases.
- `TradesViewModel` depends on purpose-specific Application readers and use cases for Trade capture/browsing/correction/closure/deletion, Setup classification, Trading Mistake associations, and screenshots, plus the Desktop dialog boundary for destructive confirmation.
- `JournalViewModel` depends on `IDailyJournalRepository`, `ITradingAccountReader`, the Desktop dialog boundary and an optional `TimeProvider`; it edits one date/Account scope and does not read or alter Trades.
- `ImportViewModel` orchestrates the Application import contracts, read-only preview builder, and `ImportTradovateTradesUseCase` after dialog approval; it neither resolves a database context nor calls an Infrastructure write store directly.
- `SettingsViewModel` depends only on Desktop theme and settings abstractions; it contains no trading or persistence-database behavior.
- Feature data must be exposed through meaningful Application boundaries rather than concrete Infrastructure stores.
- Trading and domain rules must remain outside Desktop.
- Presentation dependencies use constructor injection; ViewModels must not use a service locator.
- Application workflows, Infrastructure implementations, feature ViewModels, `MainWindowViewModel`, and `MainWindow` are wired in the Desktop composition root.

The startup invariant remains:

```text
LocalApplicationPaths
  -> create directories
  -> configure Serilog
  -> build Generic Host
  -> Host.StartAsync
  -> load settings and apply theme
  -> JournalDatabaseInitializer.InitializeAsync
  -> resolve MainWindow
  -> show MainWindow
```

Database migration must complete before `MainWindow` is resolved and displayed.

## Adding a Real Feature Page

When a placeholder destination becomes a real feature:

1. Create a feature ViewModel only when real presentation or use-case state exists, and retain it when that state should survive shell navigation.
2. Create its WPF `UserControl`.
3. Expose required workflows through meaningful Application use cases or boundaries.
4. Register the required ViewModel dependencies through DI.
5. Add an implicit `DataTemplate` from the ViewModel type to the View.
6. Update `MainWindowViewModel` content selection only as needed.
7. Remove generic placeholder behavior for that destination.
8. Keep EF Core and `JournalDbContext` out of the ViewModel.
9. Expose explicit operation and error state appropriate to the feature.
10. Keep `NavigationDestination` as presentation-only state.

Do not introduce a navigation service unless a real cross-feature navigation requirement appears.

## Desktop ViewModel Testing

`PersonalTradingJournal.Desktop.Tests` targets `net10.0-windows` and covers presentation behavior at the ViewModel level. It uses real Application use cases with hand-written test readers and stores to exercise reference/list loading, catalog create/view/edit/lifecycle/delete behavior, Trade capture/browsing/paging/sorting/correction/closure/deletion, Setup classification, Trading Mistake assignment/removal, screenshots, authoritative reloads, retained navigation instances with clean re-entry state, safe feedback, cancellation, operation gating, and state isolation. Focused tests also cover Windows theme detection, preferred/effective theme behavior, header and Settings synchronization, local settings behavior, project-owned static/dynamic resource resolution, and Dark/Light key parity.

ViewModel tests do not instantiate the visual tree. Separate compiled WPF tests cover resources, layout, routed interactions and native modal lifetime in supervised isolated processes. Neither category replaces live pointer, keyboard, screen-reader or display-scaling acceptance. M14.5's Calendar Journal launch/marker checks, passing local full-suite follow-up and remaining CI/live acceptance limits are recorded in [Daily Journal verification](daily-journal.md#m145-automated-verification-and-manual-follow-up).

## Notebook vs Journal

Notebook is a top-level destination intended for free-form market notes and knowledge, such as:

- market observations;
- important levels;
- recurring ideas;
- market concepts;
- session observations; and
- contextual market notes.

Journal is distinct: it provides text, read-only Trade context and three Daily Review questions with explicit completion/reopening for one New York date and Account scope. Notebook functionality is not implemented yet; only its place in the shell's navigation and information architecture exists. No Notebook domain or persistence design has been chosen.

## Deferred Decisions

The following are intentionally not implemented:

- the real Notebook feature;
- feature pages for placeholder destinations;
- navigation history or back/forward behavior;
- sidebar collapse;
- a chart library;
- Dashboard analytics and data loading;
- Setup/Mistake performance analytics or trade-quality scoring;
- a general keyboard shortcut system beyond existing feature shortcuts such as Journal's Ctrl+S;
- Journal autosave and restoration of historical revisions.

## Next Milestone

The Journal editor, Trade context, Daily Review completion, Calendar launch and paged Review History are implemented with explicit revisioned writes and read-only history browsing. Implementation and historical test results do not establish Journal interactive acceptance. Earlier milestone acceptance gates, including [M10 acceptance](m10-acceptance.md), remain governed by their own records.
