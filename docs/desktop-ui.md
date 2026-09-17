# Desktop UI Architecture

## Purpose

`PersonalTradingJournal.Desktop` contains the Windows WPF presentation layer and the application's composition root. M4 established the shell, M5 added Accounts and Instruments, M6–M8 completed manual Trade capture, browsing, closure, and screenshots, M9 added Trading Setup and Trading Mistake classification, and the Desktop Theme System added persisted System/Dark/Light appearance. Most other product workflows remain intentionally unimplemented.

This document explains how to extend the Desktop layer without moving trading logic or persistence access into the UI.

## Shell Structure

`MainWindow` is the permanent application shell. Its layout has three stable regions:

- a grouped navigation sidebar;
- a page header; and
- a content region that fills the remaining workspace.

The window defaults to `1280x800`, has a minimum size of `1000x650`, retains native Windows chrome, and uses a fixed 252-pixel sidebar. The sidebar and page content support intentional vertical scrolling, while horizontal overflow is disabled. The sidebar itself does not collapse or get replaced at smaller supported sizes; only its four feature groups can be collapsed independently.

`MainWindow.xaml.cs` is intentionally limited to constructor injection, `InitializeComponent()`, and `DataContext` assignment. It contains no event handlers, navigation routing, or business logic.

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

`MainWindowViewModel` owns shell presentation state only. Accounts, Instruments, Trades, Trading Setups, Trading Mistakes, and Settings keep feature presentation behavior in their own ViewModels while Domain rules and persistence access remain behind Application-layer boundaries and use cases.

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

`MainWindowViewModel` retains the injected Dashboard, Trades, Accounts, Instruments, Trading Setups, Trading Mistakes, and Settings ViewModels for the main-window lifetime. Navigating away and returning reuses those exact feature instances and their successfully loaded lists/reference caches without repeating successful reads. Entering Accounts, Instruments, Setups, Mistakes, or Trades from another destination explicitly resets feature-local transient presentation state: open detail/edit/create surfaces, unsaved drafts, validation state, and operation feedback do not reappear on re-entry. Dashboard, Settings, placeholders, and same-destination clicks keep their existing behavior.

## ViewModel-to-View Mapping

`App.xaml` contains implicit `DataTemplate` mappings:

```text
DashboardViewModel   -> DashboardView
TradesViewModel      -> TradesView
AccountsViewModel    -> AccountsView
InstrumentsViewModel -> InstrumentsView
TradingSetupsViewModel -> TradingSetupsView
TradingMistakesViewModel -> TradingMistakesView
SettingsViewModel        -> SettingsView
PlaceholderViewModel -> PlaceholderView
```

WPF resolves these mappings from the runtime type of `CurrentContentViewModel`. Future feature mappings should follow the same pattern unless a concrete requirement justifies a different presentation mechanism.

## Placeholder Policy

Seven destinations—Dashboard, Trades, Accounts, Instruments, Setups, Mistakes, and Settings—have concrete content. The remaining 12 destinations share `PlaceholderViewModel` and `PlaceholderView`. This avoids empty feature-specific View/ViewModel pairs that would contain no state or behavior.

Replace a placeholder only when its destination gains real presentation state and an Application use case. Until then, placeholder content is an accurate representation of product status, not missing architecture.

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

## Accounts Feature

Accounts performs a lazy initial load on first navigation and provides an explicit Refresh command. Its inline Add Account form captures name, account type, provider, external account ID, currency, and optional Starting Balance. Account Type defaults to Personal as a presentation convenience, and creation is coordinated by `CreateTradingAccountUseCase`.

Each persisted row displays active or inactive status and offers View, Edit, and a compact overflow menu. The detail panel shows the complete persisted account metadata and audit timestamps. Edit reuses the shared form language for Name, Account Type, Provider, External Account ID, Currency, and optional Starting Balance; it cannot change identity, creation time, or historical Trades. Starting Balance remains reference data, and there is no Current Balance field.

The overflow menu shows only the applicable Activate or Deactivate action plus Danger-styled Delete. Delete requires the shared safe-default confirmation. Unused accounts are hard deleted; an account referenced by any Trade is preserved and an information dialog recommends Deactivate instead. Successful writes refresh the authoritative list, while local detail/list state prevents a completed update or delete from appearing stale if that refresh fails.

## Instruments Feature

Instruments performs a lazy initial load on first navigation and provides an explicit Refresh command. Its inline Add Instrument form captures canonical symbol, display name, asset class, exchange, currency, Tick Size, and Tick Value. Asset Class defaults to Futures as a presentation convenience, and creation is coordinated by `CreateInstrumentUseCase`. Each row now exposes shared View, Edit, and overflow actions. View presents authoritative detail and audit timestamps; Edit reuses the shared form language and preserves active state.

Tick Size and Tick Value accept current-culture decimal formatting with an invariant `.` fallback. Point Value is read-only and derived from those inputs, including a deterministic edit preview; it is not independently editable. Only the applicable Activate or Deactivate action is shown. Unused Instruments may be deleted after destructive confirmation, while referenced deletes show safe guidance to Deactivate instead. Referenced Instruments may update metadata and tick economics without changing historical Trade snapshots, but Asset Class changes are blocked because that field controls quantity semantics. Contract-specific futures symbols, expiration, contract month, and rollover are not supported by this feature.

## Trades Feature

Trades independently loads purpose-specific reference data and the bounded Recent Trades list on first navigation, caching each successful result. Refresh requests both again while keeping their success and failure outcomes independent, so a failure in one read does not discard fresh data from the other. Normal Desktop entry lists active Accounts and Instruments only. Neither selector nor Direction is chosen automatically, and selector `PointValue` is presentation metadata rather than authoritative persisted economics.

Add Trade opens an inline form organized into Reference, Trade, Entry, and conditional Exit sections. It captures:

- an explicit Trading Account and Instrument;
- an optional active Trading Setup;
- Long or Short direction and decimal Quantity;
- an opening execution timestamp, price, commission, and fees; and
- optionally, one full closing execution with its own timestamp, price, commission, and fees.

Execution timestamps are entered explicitly as UTC and accept only `yyyy-MM-dd HH:mm:ss`, `yyyy-MM-dd HH:mm`, `yyyy-MM-dd'T'HH:mm:ss'Z'`, or `yyyy-MM-dd'T'HH:mm'Z'`. Desktop does not infer the Windows or New York timezone, convert arbitrary offsets, or accept ambiguous culture-specific dates. The resulting market facts use zero-offset UTC.

Manual decimal values parse with `CurrentCulture` first and `InvariantCulture` with `.` as a fallback. Leading/trailing whitespace, a sign, and a decimal point are supported; thousands separators, exponents, and currency symbols are rejected. Quantity must be greater than zero. Futures require whole contract quantities, while other asset classes allow positive decimals. Prices may be zero or negative when syntactically valid. Blank Commission or Fees becomes zero; otherwise each cost must be non-negative.

`TradesViewModel` validates raw presentation values in deterministic form order and builds `CreateManualTradeCommand`. `CreateManualTradeUseCase` then reloads the authoritative Account, Instrument, and optional Trading Setup, requires a selected Setup to exist and be active, constructs a `TradePricingSnapshot` from authoritative Instrument data, creates the Trade through Domain APIs, and persists it through `ITradeStore`. Manual creation does not assign Trading Mistakes. Desktop never accesses an Infrastructure store or `JournalDbContext` directly.

The M6 form supports one opening execution plus an optional full closing execution. This is a deliberately simple capture workflow; the Domain remains capable of scale-in and partial scale-out.

After successful persistence, the draft resets, the form closes, and "Trade saved successfully." is shown. Desktop then performs a best-effort authoritative Recent Trades reload and never fabricates a local row. If that post-commit reload fails, the successful write and success feedback remain intact, the existing rows stay visible, and `TradeListErrorMessage` directs the user to Refresh. The reload is not treated as part of the Save failure outcome and does not inherit cancellation from the completed Save command.

Trade operation feedback remains separated:

- `ErrorMessage` reports reference-selection/read problems;
- `ValidationErrorMessage` reports invalid manual input;
- `SaveErrorMessage` reports a validated Trade that could not be saved; and
- `SuccessMessage` reports completed persistence.

Validation failure makes no persistence attempt and retains the draft. Technical persistence failure also retains it. A stale Account or Instrument produces safe stale-reference feedback without discarding input. Cancellation propagates, retains the draft, and is not transformed into failure or success feedback. While saving, Save, Cancel, Refresh, and opening another form cannot start competing operations; this is explicit `TradesViewModel` state rather than a generic operation coordinator.

### Recent Trades

The Recent Trades surface requests at most 50 authoritative rows. It is intentionally a bounded working view, not a claim to show lifetime history. Rows include both open and closed Trades and are ordered by opening market-event time descending, with Trade ID ascending as the deterministic tie-breaker.

The columns are Opened UTC, Trade identity, Account, Average Prices, Open Qty, Net P&L, and Actions. Each row is a distinct rounded card: winning and losing closed Trades receive restrained theme-aware success/danger surface tints, while flat and open Trades use the neutral elevated surface. Net P&L is the primary outcome signal through semantic success/danger foregrounds; zero remains neutral and open Trades display a muted em dash rather than a fabricated zero or loss. Each row offers View, Edit, and a compact More menu containing destructive Delete. Account name and Instrument symbol are current reference-data labels, while lifecycle state, average prices, exposure, currency, and economics come from the reconstructed Trade and its historical pricing snapshot.

### Trade Detail

View loads the selected Trade through `ITradeDetailReader` and opens the authoritative detail surface. Its facts remain read-only; Edit and destructive Delete are explicit actions rather than inline setters. `TradesViewModel` exposes `SelectedTradeDetail`, `IsTradeDetailVisible`, `IsTradeDetailLoading`, `IsTradeDetailNotFound`, and `TradeDetailErrorMessage` for the selected projection and its loading, missing, and technical-error outcomes. Close clears detail state without clearing Recent Trades. Returning to Trades also closes any prior detail and related edit/screenshot/mistake state while preserving the cached Recent Trades list and reference data.

The detail presents current Account and Instrument identity labels; direction and status; current optional Trading Setup; opened and optional closed UTC timestamps; open quantity; total costs; average entry and optional average exit prices; gross and net P&L; and the historical pricing point value and currency. Gross and Net P&L use the same positive/negative/zero/null semantic foreground rules, with Net P&L given slightly stronger typographic emphasis. An open partially exited Trade may have a non-null average exit price while gross and net P&L remain null under Domain semantics.

The Setup section displays `—` when unclassified, marks an inactive historical Setup neutrally, lists active replacement options, and supports assign/change/clear through the Application use case. Successful mutations reload authoritative detail; safe section-local feedback distinguishes validation, persistence, and reload outcomes.

Trading Mistakes are displayed as separate assigned observations with optional Notes. The picker excludes already assigned items and permits only active catalog definitions; inactive historical assignments remain visible and removable. Add and Remove use independent Application workflows, reload authoritative assignments and options, and never infer mistake severity or trade quality from P&L.

Open Trades expose a close workflow that appends the full opposite-side execution for authoritative remaining quantity. Trade Detail also hosts screenshot add/list/preview/delete workflows. These operations retain their own loading, validation, success, error, and post-write reload states so unrelated sections do not overwrite one another.

Executions are shown individually in ascending sequence order as the complete lifecycle: sequence, execution UTC timestamp, side, quantity, price, commission, fees, total costs, and optional broker symbol, external execution ID, and external order ID provenance. The UI does not collapse scale-in or partial-exit history into an entry/exit pair.

### Trade Edit and Delete

Edit reuses the Manual Trade form language and prepopulates authoritative Account, Instrument, Direction, optional Setup, quantity, timestamps, prices, commission, and fees from `TradeDetail`, including execution identities rather than formatted display strings. The selector lists expose active alternatives plus an inactive Account, Instrument, or Setup already assigned to the Trade; other inactive records cannot be newly selected. Switching Long/Short is translated into corrected opening/closing execution sides. The current UI deliberately edits the one-entry/optional-full-exit manual shape and refuses to flatten a richer multi-execution lifecycle into that form.

Saving calls `UpdateTradeUseCase`, which corrects immutable executions and lets the Domain recalculate status, exposure, averages, costs, and P&L. The same Instrument retains its historical pricing snapshot, while an Instrument change rebuilds it and revalidates quantity semantics. Existing execution IDs and hidden broker/import provenance are preserved when their logical execution remains. Setup may be kept, changed to an active Setup, or cleared. Trade Mistake assignments and screenshot metadata/files remain unchanged. Cancel performs no write and returns to the existing detail; success reloads authoritative detail and Recent Trades without clearing screenshot or mistake state.

Delete uses the shared Danger confirmation and identifies the Trade by Instrument and opened UTC timestamp. A confirmed hard delete permanently removes the Trade plus its execution rows, Trade Mistake associations, and screenshot metadata, then attempts physical screenshot cleanup. Success clears stale detail, screenshot, mistake, and selection state and refreshes Recent Trades while remaining on the Trades page. A post-commit file cleanup failure is reported as a warning rather than presented as a failed database delete; cancellation at the confirmation performs no work.

## Trading Setup and Trading Mistake Catalogs

Analysis → Setups opens the Trading Setups catalog, and Analysis → Mistakes opens the Trading Mistakes catalog. Both pages retain lazy list loading, explicit Refresh, inline Name/optional Description creation, active/inactive status, and reversible Activate/Deactivate actions. Inactive records remain visible for historical context.

Trading Setup and Trading Mistake rows additionally provide View, Edit, and an overflow menu with the one applicable lifecycle action plus Delete. Their compact details show description, status, and audit timestamps. Edit allows normalized Name/Description changes even when referenced because historical records retain stable catalog Ids. Unused catalog entries may be hard deleted after safe-default destructive confirmation; referenced deletion is blocked with guidance to Deactivate instead. A Mistake reference is determined through the separate `TradeMistake` association, which is never removed or rewritten by catalog edits. Deactivation preserves historical review data, while only active Setups and Mistakes remain eligible for new assignment.

Create actions require a non-blank Name and are blocked while saving. Opening and cancelling a form reset its draft according to the established catalog behavior. Duplicate names and other validation failures remain near the form; successful writes close/reset the form and trigger a best-effort authoritative list reload without reclassifying a completed write as failure.

## Feature Operation and Error State

Accounts, Instruments, Trades, Trading Setups, and Trading Mistakes explicitly prevent overlapping major operations appropriate to each feature. This coordination remains per-feature ViewModel state rather than a generic operation coordinator.

Accounts, Instruments, Trading Setups, and Trading Mistakes distinguish three conceptual error categories:

- list/read errors;
- create errors; and
- lifecycle errors.

After a successful catalog create/lifecycle write, the corresponding ViewModel reloads its authoritative list projection. If that reload fails, the successful mutation is not reported as a write failure; the existing list is retained and a list-level refresh warning directs the user to retry Refresh.

Trades uses the separate read, validation, save, and success states documented in the Trades Feature section.

## Feature Page Scrolling

Accounts, Instruments, Trading Setups, and Trading Mistakes each use one page-level vertical `ScrollViewer`, containing the toolbar, inline creation form, feedback, and rows. This keeps the whole workflow reachable at reduced height without a nested list scrollbar. Horizontal scrolling is disabled, and tab order follows the visible form/action sequence.

This is appropriate for the current reference-data lists, but it is not a universal requirement for future large or virtualized datasets.

Trades likewise uses one page-level vertical `ScrollViewer`, with horizontal scrolling disabled. The toolbar, manual form, conditional Exit section, Trade Detail, Recent Trades list, execution lifecycle, action controls, and feedback surfaces remain in that scrolling region. This is the current feature-page layout, not a universal policy for future large or virtualized datasets.

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

`PersonalTradingJournal.Desktop.Tests` targets `net10.0-windows` and covers presentation behavior at the ViewModel level. It uses real Application use cases with hand-written test readers and stores to exercise reference/list loading, catalog creation/lifecycle, Trade capture/browsing/closure, Setup classification, Trading Mistake assignment/removal, screenshots, authoritative reloads, retained navigation instances with clean re-entry state, safe feedback, cancellation, operation gating, and state isolation. Focused tests also cover Windows theme detection, preferred/effective theme behavior, header and Settings synchronization, local settings behavior, project-owned static/dynamic resource resolution, and Dark/Light key parity.

These are not WPF UI tests: they do not instantiate the visual tree or replace visual acceptance for XAML layout, styling, scrolling appearance, or keyboard focus visuals.

## Notebook vs Journal

Notebook is a top-level destination intended for free-form market notes and knowledge, such as:

- market observations;
- important levels;
- recurring ideas;
- market concepts;
- session observations; and
- contextual market notes.

Journal is distinct: it will focus on trading-day and trade-review workflows. Notebook functionality is not implemented yet; only its place in the shell's navigation and information architecture exists. No Notebook domain or persistence design has been chosen.

## Deferred Decisions

The following are intentionally not implemented:

- the real Notebook feature;
- feature pages for placeholder destinations;
- navigation history or back/forward behavior;
- sidebar collapse;
- a chart library;
- Dashboard analytics and data loading;
- Setup/Mistake performance analytics or trade-quality scoring;
- a keyboard shortcut system.

## Next Milestone

The Desktop Theme System is complete. The next milestone is M10 — Tradovate CSV Import; no CSV import implementation exists yet.
