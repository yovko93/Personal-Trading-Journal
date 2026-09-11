# Desktop UI Architecture

## Purpose

`PersonalTradingJournal.Desktop` contains the Windows WPF presentation layer and the application's composition root. Milestone M4 established the shell and navigation, M5 added the Accounts and Instruments management pages, M6 added Manual Trade Entry, and M7 completed authoritative Trade browsing and read-only Trade Detail. Most other product workflows remain intentionally unimplemented.

This document explains how to extend the Desktop layer without moving trading logic or persistence access into the UI.

## Shell Structure

`MainWindow` is the permanent application shell. Its layout has three stable regions:

- a grouped navigation sidebar;
- a page header; and
- a content region that fills the remaining workspace.

The window defaults to `1280x800`, has a minimum size of `1000x650`, retains native Windows chrome, and uses a fixed 252-pixel sidebar. The sidebar and page content support intentional vertical scrolling, while horizontal overflow is disabled. The shell does not collapse or replace the sidebar at smaller supported sizes.

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

`MainWindowViewModel` owns shell presentation state only. Accounts, Instruments, and Trades keep feature presentation behavior in their own ViewModels while Domain rules and persistence access remain behind Application-layer boundaries and use cases.

## Navigation

`NavigationDestination` is a Desktop-only enum that represents shell destinations. It has no domain meaning and is not persisted.

The current navigation order is:

- Dashboard
- Notebook
- **Trading:** Trades, Journal, Calendar, Import
- **Analysis:** Performance, Strategies, Mistakes, Breakdown
- **Planning:** Playbook, Trading Plan, Rules
- **Review:** Daily Review, Weekly Review, Monthly Review
- Accounts
- Instruments
- Settings

There are 19 destinations in total.

`CurrentDestination` is the single source of truth. Each Button passes a typed destination through `CommandParameter`, and `NavigationSelectionConverter` compares that parameter with `CurrentDestination` to derive selected styling. There is no separate selected-navigation property.

There is intentionally no `NavigationService` or `INavigationService`. `MainWindowViewModel` is currently the only owner and initiator of shell navigation, so a separate service would add indirection without protecting a real boundary. Introduce one only if another ViewModel later needs to initiate cross-feature navigation.

## Content Hosting

`MainWindow` does not create or switch Views manually. It binds a stretching `ContentControl` to `CurrentContentViewModel`. The active ViewModel therefore determines the rendered content without placing View construction in a ViewModel or code-behind.

Repeated navigation to the current destination is ignored. This preserves the current content instance and selected state and avoids unnecessary View recreation.

`MainWindowViewModel` retains the injected Dashboard, Trades, Accounts, and Instruments ViewModels for the main-window lifetime. Navigating away and returning reuses those exact feature instances. In particular, returning to Trades preserves an in-progress draft, successfully cached reference/list data, and Trade Detail state without repeating successful reads. The draft remains until Cancel or a successful Save; navigation itself does not reset the form.

## ViewModel-to-View Mapping

`App.xaml` contains implicit `DataTemplate` mappings:

```text
DashboardViewModel   -> DashboardView
TradesViewModel      -> TradesView
AccountsViewModel    -> AccountsView
InstrumentsViewModel -> InstrumentsView
PlaceholderViewModel -> PlaceholderView
```

WPF resolves these mappings from the runtime type of `CurrentContentViewModel`. Future feature mappings should follow the same pattern unless a concrete requirement justifies a different presentation mechanism.

## Placeholder Strategy

Four destinations—Dashboard, Trades, Accounts, and Instruments—have concrete content. The remaining 15 destinations share `PlaceholderViewModel` and `PlaceholderView`. This avoids empty feature-specific View/ViewModel pairs that would contain no state or behavior.

Replace a placeholder only when its destination gains real presentation state and an Application use case. Until then, placeholder content is an accurate representation of product status, not missing architecture.

## Dashboard Status

The Dashboard is presentation-only. It contains empty visual regions for:

- Net P&L
- Win Rate
- Profit Factor
- Avg R
- Equity Curve
- Daily P&L
- Strategies
- Recent Trades

The four metric values display `—` rather than fake zeroes or sample financial data. `DashboardViewModel` is intentionally empty, and the Dashboard performs no analytics or database queries. Data loading and real calculations are deferred.

Dashboard metrics describe financial and statistical results; they must not infer process quality from profit or loss. Good process can lose, and bad process can profit. Future process-quality analysis must be modeled explicitly.

## Accounts Feature

Accounts performs a lazy initial load on first navigation and provides an explicit Refresh command. Its inline Add Account form captures name, account type, provider, external account ID, currency, and optional Starting Balance. Account Type defaults to Personal as a presentation convenience, and creation is coordinated by `CreateTradingAccountUseCase`.

Each persisted row displays active or inactive status and exposes only the lifecycle action applicable to that state. Activation and deactivation are reversible, inactive accounts remain visible, and the page intentionally provides no edit, delete, or Current Balance behavior. Starting Balance remains reference data.

## Instruments Feature

Instruments performs a lazy initial load on first navigation and provides an explicit Refresh command. Its inline Add Instrument form captures canonical symbol, display name, asset class, exchange, currency, Tick Size, and Tick Value. Asset Class defaults to Futures as a presentation convenience, and creation is coordinated by `CreateInstrumentUseCase`.

Tick Size and Tick Value accept current-culture decimal formatting with an invariant `.` fallback. Point Value is display-only and derived upstream; it is not a form input. Each row exposes only its applicable reversible lifecycle action, inactive instruments remain visible, and the page intentionally provides no edit or delete behavior. Contract-specific futures symbols, expiration, contract month, and rollover are not supported by this feature.

## Trades Feature

Trades independently loads purpose-specific reference data and the bounded Recent Trades list on first navigation, caching each successful result. Refresh requests both again while keeping their success and failure outcomes independent, so a failure in one read does not discard fresh data from the other. Normal Desktop entry lists active Accounts and Instruments only. Neither selector nor Direction is chosen automatically, and selector `PointValue` is presentation metadata rather than authoritative persisted economics.

Add Trade opens an inline form organized into Reference, Trade, Entry, and conditional Exit sections. It captures:

- an explicit Trading Account and Instrument;
- Long or Short direction and decimal Quantity;
- an opening execution timestamp, price, commission, and fees; and
- optionally, one full closing execution with its own timestamp, price, commission, and fees.

Execution timestamps are entered explicitly as UTC and accept only `yyyy-MM-dd HH:mm:ss`, `yyyy-MM-dd HH:mm`, `yyyy-MM-dd'T'HH:mm:ss'Z'`, or `yyyy-MM-dd'T'HH:mm'Z'`. Desktop does not infer the Windows or New York timezone, convert arbitrary offsets, or accept ambiguous culture-specific dates. The resulting market facts use zero-offset UTC.

Manual decimal values parse with `CurrentCulture` first and `InvariantCulture` with `.` as a fallback. Leading/trailing whitespace, a sign, and a decimal point are supported; thousands separators, exponents, and currency symbols are rejected. Quantity must be greater than zero. Prices may be zero or negative when syntactically valid. Blank Commission or Fees becomes zero; otherwise each cost must be non-negative. Quantity is not restricted to integers.

`TradesViewModel` validates raw presentation values in deterministic form order and builds `CreateManualTradeCommand`. `CreateManualTradeUseCase` then reloads the authoritative Account and Instrument aggregates, constructs a `TradePricingSnapshot` from `Instrument.PointValue` and `Instrument.Currency`, creates the `Trade` through Domain APIs, and persists it through `ITradeStore`. Desktop never accesses `TradeStore` or `JournalDbContext` directly.

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

The columns are Opened UTC, Trade identity, Account, Average Prices, Open Qty, Net P&L, and View. Account name and Instrument symbol are current reference-data labels, while lifecycle state, average prices, exposure, currency, and economics come from the reconstructed Trade and its historical pricing snapshot. Open Trades display an em dash for nullable Net P&L rather than a fabricated zero.

### Trade Detail

View loads the selected Trade through `ITradeDetailReader` and opens a read-only detail surface. `TradesViewModel` exposes `SelectedTradeDetail`, `IsTradeDetailVisible`, `IsTradeDetailLoading`, `IsTradeDetailNotFound`, and `TradeDetailErrorMessage` for the selected projection and its loading, missing, and technical-error outcomes. Close clears detail state without clearing Recent Trades, while shell navigation away and back retains the current detail with the rest of the retained Trades ViewModel.

The detail presents current Account and Instrument identity labels; direction and status; opened and optional closed UTC timestamps; open quantity; total costs; average entry and optional average exit prices; gross and net P&L; and the historical pricing point value and currency. An open partially exited Trade may have a non-null average exit price while gross and net P&L remain null under Domain semantics.

Executions are shown individually in ascending sequence order as the complete lifecycle: sequence, execution UTC timestamp, side, quantity, price, commission, fees, total costs, and optional broker symbol, external execution ID, and external order ID provenance. The UI does not collapse scale-in or partial-exit history into an entry/exit pair.

## Feature Operation and Error State

Accounts, Instruments, and Trades explicitly prevent overlapping major operations appropriate to each feature. This coordination remains per-feature ViewModel state rather than a generic operation coordinator.

Accounts and Instruments distinguish three conceptual error categories:

- list/read errors;
- create errors; and
- lifecycle errors.

After a successful Account or Instrument create/lifecycle write, the corresponding ViewModel reloads its authoritative list projection. If that reload fails, the successful mutation is not reported as a write failure; the existing list is retained and a list-level refresh warning directs the user to retry Refresh.

Trades uses the separate read, validation, save, and success states documented in the Trades Feature section.

## Feature Page Scrolling

Accounts and Instruments each use one page-level vertical `ScrollViewer`, containing the toolbar, inline creation form, errors, table headers, and rows. This keeps the whole workflow reachable at reduced height without a nested list scrollbar. Horizontal scrolling is disabled, and header and row column definitions remain aligned.

This is appropriate for the current reference-data lists, but it is not a universal requirement for future large or virtualized datasets.

Trades likewise uses one page-level vertical `ScrollViewer`, with horizontal scrolling disabled. The toolbar, manual form, conditional Exit section, Trade Detail, Recent Trades list, execution lifecycle, action controls, and feedback surfaces remain in that scrolling region. This is the current feature-page layout, not a universal policy for future large or virtualized datasets.

## Design System

Shared Desktop resources live under `Resources/`:

- `Colors.xaml` defines semantic color tokens.
- `Brushes.xaml` exposes semantic brushes built from those colors.
- `Typography.xaml` defines the shared text hierarchy.
- `Spacing.xaml` defines the small spacing and corner-radius scale.
- `Controls.xaml` defines reusable WPF control and shell styles.

Views should reuse these resources instead of scattering hard-coded colors or duplicating styles. The compact dark ScrollBar style supports vertical and horizontal orientation and is shared by shell scrolling regions.

The reusable dark ComboBox style uses semantic PTJ resources for both popup items and selected content. Its selected value retains the primary text color against the dark elevated surface.

PTJ currently has one dark theme. There is no `ThemeManager`, light theme, or runtime theme switching; that is intentional at this stage.

## Accessibility and Window Behavior

Navigation uses native WPF Buttons, so destinations participate in natural Tab order and support Enter and Space activation. Text content supplies accessible automation names without redundant `AutomationProperties.Name` values.

Keyboard focus and active selection are independent visual states: focus has a visible outline, while the active destination uses an elevated background, accent indicator, primary foreground, and semibold label. Sidebar and page content remain vertically reachable through mouse, scrollbar, and keyboard scrolling at the minimum supported window size.

## Dependency Rules

- Desktop may depend on Application abstractions and use cases.
- Desktop references Infrastructure because it is the composition root that wires implementations.
- Feature ViewModels must not query `JournalDbContext` or EF Core directly.
- `AccountsViewModel` depends on `ITradingAccountReader`, `CreateTradingAccountUseCase`, and `TradingAccountLifecycleUseCase`.
- `InstrumentsViewModel` depends on `IInstrumentReader`, `CreateInstrumentUseCase`, and `InstrumentLifecycleUseCase`.
- `TradesViewModel` depends on `IManualTradeReferenceDataReader`, `CreateManualTradeUseCase`, `ITradeListReader`, and `ITradeDetailReader`.
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

`PersonalTradingJournal.Desktop.Tests` targets `net10.0-windows` and covers presentation behavior at the ViewModel level. It uses real Application use cases with hand-written test readers and stores to exercise independent reference/list loading, refresh and partial failures, bounded Recent Trades, validation, lifecycle actions, authoritative post-save reload outcomes, retained navigation state, Trade Detail loading/not-found/error/close behavior, execution lifecycle projection, manual Trade form state, UTC parsing, decimal culture handling, Save behavior, separated errors, cancellation, double-submit prevention, and draft retention.

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
- runtime theme switching;
- navigation history or back/forward behavior;
- sidebar collapse;
- a chart library;
- Dashboard analytics and data loading; and
- a keyboard shortcut system.

## Next Milestone

M7 — Trade List / Detail is complete. The next milestone is M8 — Screenshot Management. Trade editing and deletion remain deferred.
