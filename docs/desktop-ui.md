# Desktop UI Architecture

## Purpose

`PersonalTradingJournal.Desktop` contains the Windows WPF presentation layer and the application's composition root. Milestone M4 established the shell, navigation, ViewModel-to-View mapping, shared visual resources, and a presentation-only Dashboard. Most product workflows remain intentionally unimplemented.

This document explains how to extend the Desktop layer without moving trading logic or persistence access into the UI.

## Shell Structure

`MainWindow` is the permanent application shell. Its layout has three stable regions:

- a grouped navigation sidebar;
- a page header; and
- a content region that fills the remaining workspace.

The window defaults to `1280x800`, has a minimum size of `1000x650`, retains native Windows chrome, and uses a fixed 252-pixel sidebar. The sidebar and Dashboard scroll vertically, while horizontal overflow is intentionally disabled. The shell does not collapse or replace the sidebar at smaller supported sizes.

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

`MainWindowViewModel` owns presentation state only. Trading rules, persistence queries, and application workflows belong behind Application-layer use cases.

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
- Settings

`CurrentDestination` is the single source of truth. Each Button passes a typed destination through `CommandParameter`, and `NavigationSelectionConverter` compares that parameter with `CurrentDestination` to derive selected styling. There is no separate selected-navigation property.

There is intentionally no `NavigationService` or `INavigationService`. `MainWindowViewModel` is currently the only owner and initiator of shell navigation, so a separate service would add indirection without protecting a real boundary. Introduce one only if another ViewModel later needs to initiate cross-feature navigation.

## Content Hosting

`MainWindow` does not create or switch Views manually. It binds a stretching `ContentControl` to `CurrentContentViewModel`. The active ViewModel therefore determines the rendered content without placing View construction in a ViewModel or code-behind.

Repeated navigation to the current destination is ignored. This preserves the current content instance and selected state and avoids unnecessary View recreation.

## ViewModel-to-View Mapping

`App.xaml` contains implicit `DataTemplate` mappings:

```text
DashboardViewModel -> DashboardView
PlaceholderViewModel -> PlaceholderView
```

WPF resolves these mappings from the runtime type of `CurrentContentViewModel`. Future feature mappings should follow the same pattern unless a concrete requirement justifies a different presentation mechanism.

## Placeholder Strategy

The 17 non-Dashboard destinations currently share `PlaceholderViewModel` and `PlaceholderView`. This avoids empty feature-specific View/ViewModel pairs that would contain no state or behavior.

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

## Design System

Shared Desktop resources live under `Resources/`:

- `Colors.xaml` defines semantic color tokens.
- `Brushes.xaml` exposes semantic brushes built from those colors.
- `Typography.xaml` defines the shared text hierarchy.
- `Spacing.xaml` defines the small spacing and corner-radius scale.
- `Controls.xaml` defines reusable WPF control and shell styles.

Views should reuse these resources instead of scattering hard-coded colors or duplicating styles. The compact dark ScrollBar style supports vertical and horizontal orientation and is shared by shell scrolling regions.

PTJ currently has one dark theme. There is no `ThemeManager`, light theme, or runtime theme switching; that is intentional at this stage.

## Accessibility and Window Behavior

Navigation uses native WPF Buttons, so destinations participate in natural Tab order and support Enter and Space activation. Text content supplies accessible automation names without redundant `AutomationProperties.Name` values.

Keyboard focus and active selection are independent visual states: focus has a visible outline, while the active destination uses an elevated background, accent indicator, primary foreground, and semibold label. Sidebar and Dashboard content remain vertically reachable through mouse, scrollbar, and keyboard scrolling at the minimum supported window size.

## Dependency Rules

- Desktop may depend on Application abstractions and use cases.
- Desktop references Infrastructure because it is the composition root that wires implementations.
- Feature ViewModels must not query `JournalDbContext` or EF Core directly.
- Future feature data must be exposed through meaningful Application boundaries.
- Trading and domain rules must remain outside Desktop.
- Presentation dependencies use constructor injection; ViewModels must not use a service locator.
- `DashboardViewModel`, `MainWindowViewModel`, and `MainWindow` are currently DI-created.

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

1. Create a feature ViewModel only when real presentation or use-case state exists.
2. Create its WPF `UserControl`.
3. Expose required workflows through meaningful Application use cases or boundaries.
4. Register the required ViewModel dependencies through DI.
5. Add an implicit `DataTemplate` from the ViewModel type to the View.
6. Update `MainWindowViewModel` content selection only as needed.
7. Remove generic placeholder behavior for that destination.
8. Keep EF Core and `JournalDbContext` out of the ViewModel.
9. Keep `NavigationDestination` as presentation-only state.

Do not introduce a navigation service unless a real cross-feature navigation requirement appears.

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
