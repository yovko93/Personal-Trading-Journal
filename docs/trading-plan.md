# Trading Plan

## Key Levels heading refinement (before M21.10)

Starting state: clean `develop`, HEAD `ba301abf803424afaa7802a0a91cbcf0a72f1127`. Removed only the visible Manual levels label and centered the single Key Levels heading across the existing six-field group. The shared dynamic information brush supplies dark blue in Light and pale blue in Dark, distinct from the teal section title. The existing heading-to-fields gap and manual-price tooltip remain; narrow layout still stacks the complete group beneath instrument context. No field binding, validation, persistence, collapse behavior or Daily Trading Principles changes; no new placement/color-only tests.

Existing focused checks: `dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~SessionPreparationTests|FullyQualifiedName~SessionPreparationViewTests|FullyQualifiedName~PlanSectionsViewTests' --logger trx --results-directory TestResults/PlanLevels/focused` passed **28/28**, zero failures/skips. Release build (`dotnet build PersonalTradingJournal.sln -c Release --no-restore`) passed with **zero warnings/errors**, 58.03s; final indentation-only cleanup build also passed, 12.73s.

Complete gate: `pwsh -File scripts/Test-Release.ps1 -ResultsDirectory TestResults/PlanLevels/complete` passed **4,916 tests, zero failures/skips, exit 0**: Domain 567, Application 1,021, Infrastructure 1,452, Desktop 1,876 (88 exclusive + 1,788 parallel). Evidence directory: `TestResults/PlanLevels/complete/release-20261019-205341-59eb8457f8444a6a89cd8fd5c1af78ee`. Calendar native **66.072s / 53.928s headroom**, exit 0 and cleanup confirmed. No assertion, deadline or scheduling changes. `dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure/PersonalTradingJournal.Infrastructure.csproj --context JournalDbContext --configuration Release --no-build` reports no model changes; `git -c core.safecrlf=false diff --check` passes. All verification is local, not matching CI for uncommitted source.

Inspected synthetic compiled-WPF draft renders in `artifacts/plan-levels/`: `preparation-Light-1040-96-draft.png`, `preparation-Dark-1040-96-draft.png`, `preparation-Light-460-240-draft.png`, `preparation-Dark-460-240-draft.png`. The centered heading, distinct readable blue accent and six visible fields are preserved in normal and stacked narrow/240-DPI layouts. Long raw prices retain their existing horizontal field scrolling. These are automated renders, not live keyboard/Narrator or native-picker acceptance.

Exact changed files: `src/PersonalTradingJournal.Desktop/Views/TradingPlans/SessionPreparationSection.xaml`, `README.md`, `docs/trading-plan.md`. No production/provider access, commit, push or branch change. Matching CI and other previously pending live checks remain unverified; M21.10 stays paused.

## Folding headers and compact principle rows (before M21.10)

Actual starting state: clean `develop`, HEAD `4a105f195e5edf026030871db6ce5a3c9176c127`. The previous collapse/global-list implementation was already committed by the user and is preserved. This is local WPF presentation only: no migration, repository, command, Plan token, Ready snapshot, Rules or Review-evidence changes. No production/provider access or commit/push/merge; M21.10 remains paused.

### Presentation and interaction

- The four existing native Expanders retain their single header toggle, titles/numbers, Enter/Space and ExpandCollapse automation, immediate content visibility, session choices and guarded validation expansion. The decorative icon adds no tab stop or nested button. Tooltips explicitly say Expand/Collapse plus the section name. Shared/sidebar controls are untouched.
- A 20-DIP vector icon occupies a 36-DIP rounded teal-tinted area. Collapsed is three solid bars, expanded is X. A single 200ms render-only progress clock projects hinged strip geometry with foreshortening and a small rear-face offset; the middle strip contracts into the hinge. It is not just rotation/crossfade, does not animate content/layout, and never loops. Reversal captures the currently displayed pose; a new clock is synchronously positioned at its start to avoid exposing the final base value for a frame. Windows ClientAreaAnimation off and unloading snap to the current actual state, stopping the clock; preference-change subscriptions are released on unload.
- Principle rows use fixed-width numbered badges, flexible left-aligned text and one right-hand red X, thin themed borders and 6-DIP row separation. Multiline Bulgarian text wraps without overlapping the removal column. The input/Add row stacks at very small available widths. X retains its identifying accessible name and the existing command; guarded focus moves to the next row, preceding last row, or input when empty. New input elsewhere supersedes queued focus through the existing PageFocus guard.
- Independent global storage, insertion order, identity, cancellation/concurrency, input retention, no-plan/Ready availability and exclusion from historical evidence are unchanged.

### Reference inspection limitation

The user explicitly replaced the earlier [Yet another hamburger menu](https://dribbble.com/shots/2064473-Yet-another-hamburger-menu) with [3d hamburger menu](https://dribbble.com/shots/13943358-3d-hamburger-menu). Web reading exposed the page title but not its animation. Browser and supported Windows helper initialization both failed with `failed to write kernel assets: The system cannot find the path specified. (os error 3)`; an approved direct public-page request returned an empty body. Neither reference's motion/intermediate frames could be inspected. The subsequent reply linked the earlier page again, without explicitly revoking the folding requirement. The implemented motion follows the written folding/depth requirements; **reference visual fidelity remains unverified**, pending an accessible GIF/video or human comparison. It is not represented as an observed reproduction of that animation.

### Verification chronology and renders

All fixtures are synthetic. No assertion, deadline, isolation or test scheduling changes:

- The first test build found xUnit2013 in the new empty-list assertion; corrected to Assert.Empty. Product compilation was successful.
- `TestResults/PlanFold/initial`: **28 passed / 4 failed**, all four layout cases stopped at the missing Expand/Collapse tooltip prefix. WPF ignores Binding.StringFormat for the object-valued tooltip; the Expander now supplies a string-valued action description.
- `TestResults/PlanFold/corrected`: **28 passed / 4 failed**, all four cases found a rapid-reversal pose jump: newly attached WPF clock had not ticked, exposing destination 1 instead of captured 0.5. Synchronously seeking the new clock to zero fixes this demonstrated defect without delaying input or changing assertions.
- `TestResults/PlanFold/clock-corrected`: **32/32**, including controlled clock seeking/reversal, reduced motion, existing raw-input/token/validation assertions, no-plan/Ready, removal focus for middle/last/only row and exact input preservation.
- `TestResults/PlanFold/final-focused`: **32/32** after the Light badge border and 280-DIP entry-row assertion. Build: **zero warnings/errors**, 14.27s.
- First complete gate (`TestResults/PlanFold/complete/release-20261019-195219-b34e528c6f00476381a1e1437468dd03`): **4,916 passed, zero failures/skips, exit 0**. Calendar **73.150s / 46.850s headroom**. This run preceded the final two refinements below and is not substituted for final-source verification.
- Endpoint inspection showed that RenderTargetBitmap could read the cached intermediate drawing after stopping the icon clock. The fixture now flushes invalidated drawing with UpdateLayout before capturing endpoint screenshots (no sleep). Removal-focus bookkeeping is also cleared on operation completion/context replacement, so a failed/cancelled removal cannot influence a later reload. Neither changes persistence or test scheduling.
- `TestResults/PlanFold/endpoint-focused`: **32/32** on final source; final regenerated endpoint renders show three straight bars/X as intended. `dotnet build PersonalTradingJournal.sln -c Release --no-restore`: **zero warnings/errors**, 13.83s. `dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure/PersonalTradingJournal.Infrastructure.csproj --context JournalDbContext --configuration Release --no-build`: **no pending model changes**, exit 0.
- Final complete command: `pwsh -File scripts/Test-Release.ps1 -ResultsDirectory TestResults/PlanFold/final-complete`: **4,916 passed, zero failures/skips, exit 0**. Domain **567**, Application **1,021**, Infrastructure **1,452**, Desktop **1,876** (88 exclusive + 1,788 parallel). Run `release-20261019-200202-d7c4b29ff9334610ab45842910788908` contains the aggregate summary, parent/child TRX, VSTest diagnostics and supervision logs. Calendar **72.638s / 47.362s headroom**; updated sections child **24.090s / 65.910s aggregate headroom**, exit 0 and cleanup confirmed. History and workspace children also exited 0. Final tracked/new-file whitespace checks passed. All results are local, not matching CI for these uncommitted changes.

Focused command: `dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~PlanSectionsViewTests|FullyQualifiedName~DailyTradingPrinciplesTests|FullyQualifiedName~TradingPlanWorkspaceTests' --logger trx --results-directory TestResults/PlanFold/final-focused`. The final synthetic render run sets `PTJ_PLAN_RENDER_DIRECTORY=artifacts/plan-fold-renders-final` and uses the normal Windows desktop. Renders cover Light/Dark, 980 DIP/96 DPI and 460 DIP/240 DPI: empty, single, populated no-plan, collapsed, Ready and Ready-collapsed. A separate 280-DIP entry-row layout check verifies input width and stacked Add bounds. Endpoint screenshots settle icon clocks without a sleep; separate `fold-frames-{theme}-{dpi}.png` contact sheets show geometric poses at 0, .25, .5, .75 and 1, not an external-reference capture.

Human keyboard/Narrator, native picker, live application restart and matching CI remain pending. Automated native focus/providers and static/intermediate renders are not human interaction or screen-reader acceptance. The complete gate must pass independently of focused results.

Inspected final images in `artifacts/plan-fold-renders-final/`: Light and Dark normal Ready-collapsed (all four headers, numbered rows and multiline text), Dark narrow/240-DPI Ready-collapsed (main scrolling retained), Light narrow empty and long populated no-plan, Dark narrow single and collapsed. Intermediate contact sheets for Light/96 and Dark/240 show hinge foreshortening and final X. Row badges have a visible theme-aware outline even where Light surfaces are similar. Reference fidelity and human motion perception are still pending; no known overlapping controls were observed in these automated renders. HEAD remains `4a105f195e5edf026030871db6ce5a3c9176c127` on `develop`; seven modified files and one new file remain uncommitted.

### Exact changed files

Eight files (seven modified, one new); no unrelated existing changes were present or overwritten:

```text
README.md
docs/trading-plan.md
src/PersonalTradingJournal.Desktop/Views/TradingPlans/PlanSectionExpander.cs
src/PersonalTradingJournal.Desktop/Views/TradingPlans/PlanFoldIcon.cs (new)
src/PersonalTradingJournal.Desktop/Views/TradingPlans/SessionBriefStyles.xaml
src/PersonalTradingJournal.Desktop/Views/TradingPlans/TradingPlanWorkspaceView.xaml
src/PersonalTradingJournal.Desktop/Views/TradingPlans/TradingPlanWorkspaceView.xaml.cs
tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/PlanSectionsViewTests.cs
```

## Collapsible sections and global Daily Trading Principles (before M21.10)

Actual baseline: clean `develop`, HEAD `6f237f2dfb71c4e1b737d5473f8983ac2b5b28e1`. The previous 57-modified/4-new changes had already been committed; this task preserves them. No production journal/provider access, commit, push, merge or M21.10 work.

### Behavior, ownership and compatibility

- Sections **01 Session Preparation**, **02 If / Then Scenarios** and **03 Risk & Rules** default expanded. A chevron/header toggles only the existing body; Space, Enter, native ExpandCollapse automation and theme-aware focus are supported. Choices survive date/Account changes and navigation in the retained application workspace, but reset at application restart. They never mutate or save Plan content.
- Invalid/raw input, selection and unsaved content stay in the same editor. Validation expands its section before the existing guarded scroll/focus request. The scenario-direction readiness lookup was corrected from the obsolete ComboBox type to the actual pill Button, with a compiled focus regression.
- **Daily Trading Principles** sits immediately above Today’s Focus, outside Plan existence/read-only gates. It remains editable with no Plan and with Ready content. Its collapse choice and unsubmitted input survive context changes. It is an ordinary scrolling section, not an overlay.
- Add (also Enter) appends exact Unicode text; whitespace-only, NUL and more than **500 characters** are rejected. This matches the existing short Focus limit. There are at most **1,000 entries**; reads fail explicitly rather than truncate. Stable GUIDs and insertion positions are stored; displayed numbers renumber after X removes an entry. Long text wraps with one right-aligned X. No production examples are seeded.
- Add/remove is a separate transactional, concurrency-checked SQLite operation with UTC creation audit, verified readback and cancellation before commit. No Plan is created, dirtied, retargeted or versioned; deletion/prefill of a daily Plan leaves reminders alone. Failure retains input; conflict reloads only the saved reminder list and asks the user to review before retrying. Repeated clicks cannot submit twice. There is no automatic write retry or reminder Save/Discard prompt.
- These are **current global reminders**, not historical preparation or Trading Rules. They never enter Ready snapshots, Review evidence or AI packets, and make no compliance claim.
- Additive migration `20261019171746_AddDailyTradingPrinciples` adds two independent tables and no reminder content. Logical **export v14** includes entries and list token in JSON; four CSVs remain unchanged. Complete current-schema backup, preflight and offline restore preserve the list. Older logical exports omit it and must not be interpreted as containing invented entries; there is no JSON/CSV import or restore feature. Older-schema archive restore stays unsupported. Downgrade refuses populated reminder data.

### Verification chronology

All databases, example Bulgarian text and renders are synthetic. Attempts are kept under `TestResults/PlanPrinciples/`; failed runs are not counted as passes.

| Attempt | Result and explanation |
| --- | --- |
| `initial` | **31 passed / 29 failed** (Infrastructure 30/33, Desktop 1/27). Two schema expectations lacked the new records/integer singleton; one new Ready equality assertion compared reconstructed immutable arrays by identity instead of serialized historical content. The new store also incorrectly required TimeProvider registration, propagating through 26 Desktop fixtures; it now follows the existing optional clock/System fallback convention. |
| `focused` | **63 passed / 1 failed** (Infrastructure 32/33, Desktop 31/31). One remaining schema assertion assumed the new version singleton had zero rows. Four new WPF theme/size workflows passed. |
| `verified` | **386 passed / 1 failed**: Domain 77, Application 35, Desktop 149 passed; Infrastructure 125/126. Empty export expected one catalog-state row instead of the two explicitly exported singleton tokens. Schema/portability and all WPF workflows otherwise passed. |
| `final-focused` | **64/64**: 58 Infrastructure and 6 Desktop. Corrected export expectation plus scenario-direction readiness focus and collapse coverage. |
| `validation-focus` | **4/4** compiled WPF validation/expansion checks, including invalid reminder input expanding and receiving guarded focus. |
| First complete gate (`complete`) | **4,915/4,915**, zero failures/skips, exit 0. Domain 567, Application 1,021, Infrastructure 1,452, Desktop 1,875 (88 exclusive + 1,787 parallel). Run `release-20261019-172950-7e0c3b1692b64848ac9090d173a8a454`, UTC 17:29:50–17:37:36. Calendar 63.731s / 56.269s headroom. The later controlled activation regression is recorded separately. |

The later `activation-before` controlled regression **failed 0/1** at `ContextTask.IsCompleted`: the newly added await of reminders occurred before publishing the existing Plan context task. `MainWindowViewModel.OpenReviewPreparationAsync` awaits that public task, so this was a real navigation-contract regression even though the first complete gate passed. Reminder initialization now runs inside the existing tracked `LoadCoreAsync`, with cancellation checked before Plan reads; cancellation requests both owners before awaiting completion. The controlled test proves the task remains pending and cancellation drains initialization without starting a Plan read or write. `activation-corrected` passed **32/32** (workspace, global reminder and four compiled WPF workflows); the failing pre-correction test is retained at `TestResults/PlanPrinciples/activation-before`. This justified the corrected focused run and a second complete gate, not retrying an unchanged failing suite.

The new regression coverage also exercises a failing SQLite state-update trigger to prove transaction rollback of both add and remove, concurrent competing appends, stale/removal cancellation, exact Unicode restart reads, prior-schema migration, no-plan/Ready availability, context and input retention, Plan token independence, and backup/preflight/offline restore/export.

Commands: affected tests use `dotnet test PersonalTradingJournal.sln -c Release --no-restore --filter 'FullyQualifiedName~TradingPlans|FullyQualifiedName~InitialMigrationTests|FullyQualifiedName~SqlitePersistenceIntegrityTests|FullyQualifiedName~PortableExportTests|FullyQualifiedName~JournalDatabaseInitializerTests|FullyQualifiedName~ProductionPersistenceIntegrationTests' --logger trx --results-directory TestResults/PlanPrinciples/verified`. The final focused filter is `FullyQualifiedName~DailyTradingPrinciple|FullyQualifiedName~PlanSectionsViewTests|FullyQualifiedName~PortableExportTests|FullyQualifiedName~InitialMigrationTests`. The last four-case check filters the Desktop project to `FullyQualifiedName~PlanSectionsViewTests`. Render runs set `PTJ_PLAN_RENDER_DIRECTORY=artifacts/plan-principles-renders-final`, with child evidence under the matching result directory. Native tests use the normal Windows desktop.

### Visual and remaining acceptance

Production-resource compiled renders in `artifacts/plan-principles-renders-final/` cover Light/Dark at 980 DIP/96 DPI and 460 DIP/240 DPI: empty, populated no-plan, collapsed, Ready and Ready-collapsed principles/sections. Long Bulgarian reminders wrap while Add/X remain available; empty sections remain compact; collapsed sections leave their headings and remove body space. The first render fixture used a wider real Window than bitmap viewport, clipping its right edge; fixed-size content now matches the tested viewport. This was a test-render sizing correction, not a product layout change. Existing Session Preparation, Scenarios, Risk, History and full-workspace renders are retained too.

Automated native focus/automation-provider checks and synthetic database restart are **not human keyboard/Narrator, native picker or live application restart acceptance**. Those remain pending, as does matching CI for the uncommitted changes. No workflow publication or provider request is performed. SQLite writer acquisition follows the existing synchronous backup/store convention: cancellation is observed around the SQLite operation, not guaranteed to interrupt an in-progress native busy wait; precommit changes are rolled back and a committed result remains authoritative. The historical Calendar/History host timing risks are not declared eliminated by focused passes. M21.10 stays paused.

### Final results and exact changed files

Initial Release build before the first gate: `dotnet build PersonalTradingJournal.sln -c Release --no-restore`, **zero warnings/errors**, 4.96s. `dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure/PersonalTradingJournal.Infrastructure.csproj --context JournalDbContext --configuration Release --no-build`: **no pending model changes**. Tracked and all ten new-file whitespace checks passed. After the activation correction, the same Release build passed again with **zero warnings/errors**, 3.37s; EF consistency passed again. Final-source gate command: `pwsh -File scripts/Test-Release.ps1 -ResultsDirectory TestResults/PlanPrinciples/final-complete`. Final result: **4,916 passed, zero failures/skips, exit 0**: Domain **567**, Application **1,021**, Infrastructure **1,452**, Desktop **1,876** (**88** exclusive + **1,788** parallel). Final run `release-20261019-174127-666e40e113914ef28b99f499f9a0d0f6`, UTC **17:41:27–17:49:12**, has its aggregate `summary.json`, parent/child TRX and supervision/phase logs under `TestResults/PlanPrinciples/final-complete/`. Calendar child **64.405s / 55.595s aggregate headroom**; Journal **9.680s**; History **23.981s**; new four-case sections child **22.368s**; workspace **26.450s**, all exit 0 with cleanup confirmed. No deadlines, isolation, assertions or execution lanes were weakened. Final tracked/new-file whitespace checks passed after documentation updates. HEAD remains `6f237f2dfb71c4e1b737d5473f8983ac2b5b28e1` on `develop`, with the 40 uncommitted files below. Local automated acceptance is green; human and matching-CI acceptance remain pending.

Exact task inventory: **40 files** (30 modified, 10 new); starting tree was clean. The migration designer/model snapshot and schema/export expectation updates are deliberate.

```text
README.md
docs/architecture.md
docs/trading-plan.md
src/PersonalTradingJournal.Application/Backups/IPortableExportService.cs
src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/TradingPlanWorkspaceViewModel.cs
src/PersonalTradingJournal.Desktop/Views/TradingPlans/RiskReadinessSection.xaml
src/PersonalTradingJournal.Desktop/Views/TradingPlans/RiskReadinessSection.xaml.cs
src/PersonalTradingJournal.Desktop/Views/TradingPlans/ScenariosSection.xaml
src/PersonalTradingJournal.Desktop/Views/TradingPlans/ScenariosSection.xaml.cs
src/PersonalTradingJournal.Desktop/Views/TradingPlans/SessionBriefStyles.xaml
src/PersonalTradingJournal.Desktop/Views/TradingPlans/SessionPreparationSection.xaml
src/PersonalTradingJournal.Desktop/Views/TradingPlans/SessionPreparationSection.xaml.cs
src/PersonalTradingJournal.Desktop/Views/TradingPlans/TradingPlanWorkspaceView.xaml
src/PersonalTradingJournal.Desktop/Views/TradingPlans/TradingPlanWorkspaceView.xaml.cs
src/PersonalTradingJournal.Infrastructure/Backups/PortableExportSchema.cs
src/PersonalTradingJournal.Infrastructure/Persistence/JournalDbContext.cs
src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/JournalDbContextModelSnapshot.cs
src/PersonalTradingJournal.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs
tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/TradingPlanWorkspaceTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Backups/PortableExportTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Backups/SavedPeriodPortabilityTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Initialization/JournalDatabaseInitializerTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integration/ProductionPersistenceIntegrationTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integrity/SqlitePersistenceIntegrityTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Migrations/InitialMigrationTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePersistenceTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePortabilityTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookPersistenceTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/SharedTradingPlanTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/TradingPlanPortabilityTests.cs
src/PersonalTradingJournal.Application/TradingPlans/IDailyTradingPrincipleStore.cs
src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/DailyTradingPrinciplesViewModel.cs
src/PersonalTradingJournal.Desktop/Views/TradingPlans/PlanSectionExpander.cs
src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261019171746_AddDailyTradingPrinciples.Designer.cs
src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261019171746_AddDailyTradingPrinciples.cs
src/PersonalTradingJournal.Infrastructure/Persistence/Records/DailyTradingPrincipleRecord.cs
src/PersonalTradingJournal.Infrastructure/TradingPlans/DailyTradingPrincipleStore.cs
tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/DailyTradingPrinciplesTests.cs
tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/PlanSectionsViewTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/DailyTradingPrincipleTests.cs
```


## Delete Plan, Ready simplification and Create prefill (before M21.10)

Starting state was clean `develop`, HEAD `cd1b64c085e2716288fc19a3b69492b7ee21fb6e`; existing UI corrections are preserved. No production journal or provider is accessed. M21.10 remains paused.

- **Delete Plan:** red action beside Start Draft / Edit, enabled for loaded Draft or Ready. Confirmation identifies its exact date/scope and covers unsaved input without a second Save prompt. Cancellation/conflict retains all raw fields; success clears the editor and obsolete feedback without a benign no-plan InfoBox. History filtering/selection cannot retarget deletion. The transactional tombstone releases only active uniqueness; recreation has a new identity. Old Ready reads, saved Reviews and referenced images remain exact, not redirected to the replacement. No recycle bin or asset collector is introduced.
- **Ready:** no active checklist, counts, progress, confirmation invalidation or currency-acknowledgement popup. Risk & Rules uses the available width. Mark as Ready is still explicit and checks real required preparation, numeric validity, source integrity, currency and concurrency. Optional scenario text remains optional. Old Ready/Review confirmation flags remain immutable historical data; new preparation carries None and is not described as manually confirmed.
- **Create prefill:** latest saved earlier New York date in the same global or exact Account scope, skipping gaps and excluding future/deleted Plans. Saved Draft takes priority, otherwise latest Ready. Copies session, Focus, Instrument contexts/bias/thesis/levels, news, scenarios/Stand Aside, exact Playbook/Rule links and limits/currency. No images, thumbnail/attachment metadata, source identity/audit/token, Ready history or confirmations are copied. New Draft/Plan/scenario identities and current UTC timestamps are verified atomically. Merely visiting an empty date writes nothing. Start Draft and manual template-only Copy Forward do not acquire this policy.
- **Reference/currency failures:** copied text is retained with explicit warnings and an unverified reference baseline; Ready is blocked until sources are resolved. Names never substitute IDs. No FX conversion or silent relabeling. Frozen Ready currency survives Start Draft/prefill. Legacy Draft currency is recovered only from an unchanged saved Account fingerprint; otherwise explicit currency/amount correction is required.
- **Portability:** additive `20261019160256_TombstoneTradingPlans` migration adds nullable UTC tombstones and active-only global/exact uniqueness. Export **v13** includes deletion timestamps and retained Plan/Ready/image metadata, not image binaries. Complete backup, preflight and offline restore retain tombstoned evidence and image bytes. Older-schema restore remains unsupported; no compatibility relaxation. Downgrade of an occupied Plan schema is refused rather than resurrecting deleted data.

### Verification and remaining acceptance

Synthetic tests cover Draft/Ready deletion, cancel/stale/duplicate/concurrent writes, restart/recreation, exact historical reads, new evidence exclusion, retained images through backup/restore/export, scope/gap source selection, unavailable references, and no-confirmation Ready. Compiled WPF checks retain field/focus/navigation assertions and add prefilled/deleted states. Results and exact inventory are recorded below. Human keyboard/Narrator, native picker, live restart and matching CI remain pending; automated renders are not human interaction.


### Commands, failed attempts and visual evidence

All data is synthetic. The following result directories are separate attempts, not retries hidden in a passing total:

| Attempt under `TestResults/PlanLifecycle/` | Actual result / interpretation |
| --- | --- |
| `initial` | 295 passed / 36 failed. Old confirmation/export/migration fixture expectations conflicted with the new contract; a workspace child also reached its 30-second inactivity watchdog. The child dump and output remain available. No claim that this timeout is fixed. |
| `contracts` | 339 passed / 3 failed: remaining old Domain, currency-create and historical-flag expectations. |
| `contracts-final` | 342/342, before the additional legacy Draft normalization tests. |
| `contracts-verified` | 342 passed / 2 failed: old tests assumed saving an unchanged legacy confirmed Draft never advances its version. New policy clears its flags once; subsequent no-op saves remain no-ops. |
| `focused-final` | **344/344**, 77 Domain + 35 Application + 103 Infrastructure + 129 Desktop, zero skips. |
| `wpf` | 9 passed / 4 propagated failures from **one** workspace child assertion: key-level validation found null keyboard focus, line 122 at that build. Child itself 3/4, not a timeout. No product focus change was made on this evidence alone. |
| `focus-isolated` | Workspace 4/4, 33s child test duration. An isolated pass does not establish the cause of the earlier focus loss. |
| `wpf-final` | All **13/13** relevant WPF cases passed under the same multi-child workload; workspace workflow assertions and deadlines unchanged. |
| `layout-verified` | **27/27** session/compiled workspace cases after correcting the currency presentation; no deadline change. |
| First complete gate (`final/release-20261019-163328-a6bf9cc160b947aeb8bb45567fc5a151`) | **4,896 passed / 5 failed / zero skips, exit 1**. All five are stale schema-contract expectations (migration list/count and active-only unique-index filter). Domain 567/567, Application 1,021/1,021, Infrastructure 1,440/1,445, Desktop 1,868/1,868. Calendar child 75.838s / 44.162s headroom. No assertion removed: expected current schema updated. |
| `corrections` | 361 passed / 1 failed. All 120 Infrastructure cases passed; the newly added shared-prefill test incorrectly assumed UTC midnight had already advanced its New York date. The fixture now selects the following New York date explicitly. |
| `shared-prefill` | **2/2**, covering saved shared currency with an empty form and no-source mixed-Account currency validation. |
| Final-source gate (`final-corrected`) | **4,902 passed / zero failures / zero skips, exit 0**. Full final-source results below. |

A final code review also found a genuine Create-path problem: blank shared currency was validated by the Desktop content constructor before the atomic store could choose prior saved preparation. Create now passes an unset blank-form fallback to the store; an existing prior global Plan supplies its saved currency, while no-source invalid currency still fails with retained input and a corrective message. A real-SQLite ViewModel regression verifies the global saved-EUR case. The final corrected complete gate includes this change; the earlier run cannot establish its acceptance.

The first WPF launch was blocked by a compile error while making the Domain preparation comparison available across assemblies; no tests ran in either failed compile attempt. One build was mistakenly overlapped with the render testhost and failed with MSBuild locked-output errors (30 warnings/6 errors); no source or fixture lock was inferred. Subsequent builds are performed after test processes exit. These failures are not acceptance passes.

Commands: focused contracts use `dotnet test PersonalTradingJournal.sln -c Release --no-restore --filter '(FullyQualifiedName~TradingPlans|FullyQualifiedName~ReviewPreparation|FullyQualifiedName~PortableExport|FullyQualifiedName~InitialMigrationTests)&FullyQualifiedName!~ViewTests' --logger trx --results-directory TestResults/PlanLifecycle/focused-final`. WPF uses the Desktop test project with `FullyQualifiedName~TradingPlans&FullyQualifiedName~ViewTests`; the final currency display check uses `FullyQualifiedName~TradingPlanWorkspaceViewTests|FullyQualifiedName~SessionPreparationTests`. Render runs set `PTJ_PLAN_RENDER_DIRECTORY`, child results set `PTJ_TEST_RESULTS_DIRECTORY`, and bounded timings use `PTJ_CALENDAR_TIMINGS=1`. Native tests run on the normal Windows desktop, not the restricted sandbox desktop.

Automated bitmaps are in `artifacts/plan-lifecycle-renders-final/`: `workspace-{Light|Dark}-{980-96|460-240}-{ready|prefilled|deleted}.png` and `risk-{Light|Dark}-{1100-96|460-240}-rules-bottom.png`. Inspected: red Delete beside Edit on Ready, wrapping toolbar/clear currency label, empty post-delete state without a notice, copied preparation without image rows, four-column wide/two-column narrow limits, linked Rules with wrapping names, no checklist column. Workspace renders also exercise 1280/144 and 1600/192. Component Risk renders intentionally show standalone state context; integrated pages suppress that duplicate context. These are compiled-WPF renders, not human keyboard/Narrator or native-picker acceptance.

### Final complete verification

`pwsh -File scripts/Test-Release.ps1 -ResultsDirectory TestResults/PlanLifecycle/final-corrected` completed **exit 0**, with **4,902 passed / zero failures / zero skips**, against the final source. Results: `TestResults/PlanLifecycle/final-corrected/release-20261019-164507-867dadb2a663404880578c184771e637/summary.json` and its exclusive/parallel TRX, stdout/stderr and child supervision logs. UTC stages **16:45:07.951–16:53:01.244**. No test assertions, deadlines, execution lanes or concurrency bounds were relaxed.

| Project / partition | Passed |
| --- | ---: |
| Domain | 567 |
| Application | 1,021 |
| Infrastructure | 1,445 |
| Desktop exclusive WPF | 88 |
| Desktop parallel | 1,781 |
| Desktop combined | 1,869 |

Calendar native child exited 0 in **76.371s**, leaving **43.629s** against its unchanged 120-second aggregate deadline; Journal child passed in **10.280s**. The earlier workspace inactivity and intermittent null-focus assertion were not reproduced in final verification; this is not proof of their host-level cause or elimination of timing variability. Bounded focus diagnostics remain, and failed artifacts are retained separately.

`dotnet build PersonalTradingJournal.sln -c Release --no-restore`: **zero warnings / zero errors**, 2.85s, before the final gate. `dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure/PersonalTradingJournal.Infrastructure.csproj --context JournalDbContext --configuration Release --no-build`: **no pending model changes**, isolated design-time context. `git -c core.safecrlf=false diff --check` plus `git diff --no-index --check -- NUL <file>` for each new file: **passed** after documentation updates. Only documentation changed after the final build/gate.

**Implementation and complete local regression checks are green.** Human keyboard/Narrator, native picker interaction, live application restart and matching CI remain pending; synthetic SQLite restart and automated native focus checks do not substitute for them. These changes are uncommitted, so no published CI run represents this final working tree. No production data or provider was accessed, and no commit/push/merge was performed. HEAD remains `cd1b64c085e2716288fc19a3b69492b7ee21fb6e`, branch `develop`, **57 modified / 4 new files**. M21.10 remains paused.

### Exact changed-file inventory

The starting tree was clean; the following **61 files** are the task's current changes (migration designer and snapshot included):

```text
README.md
docs/architecture.md
docs/trading-plan.md
src/PersonalTradingJournal.Application/Backups/IPortableExportService.cs
src/PersonalTradingJournal.Application/TradingPlans/ITradingPlanStore.cs
src/PersonalTradingJournal.Application/TradingPlans/TradingPlanContracts.cs
src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/ReviewPreparation.cs
src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/SessionPreparationViewModel.Lifecycle.cs
src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/SessionPreparationViewModel.RiskReadiness.cs
src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/SessionPreparationViewModel.Rules.cs
src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/SessionPreparationViewModel.Scenarios.cs
src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/SessionPreparationViewModel.cs
src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/TradingPlanWorkspaceViewModel.History.cs
src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/TradingPlanWorkspaceViewModel.cs
src/PersonalTradingJournal.Desktop/Views/TradingPlans/RiskReadinessSection.xaml
src/PersonalTradingJournal.Desktop/Views/TradingPlans/RiskReadinessSection.xaml.cs
src/PersonalTradingJournal.Desktop/Views/TradingPlans/TradingPlanLeaveDialog.xaml.cs
src/PersonalTradingJournal.Desktop/Views/TradingPlans/TradingPlanWorkspaceView.xaml
src/PersonalTradingJournal.Desktop/Views/TradingPlans/TradingPlanWorkspaceView.xaml.cs
src/PersonalTradingJournal.Domain/TradingPlans/TradingPlan.cs
src/PersonalTradingJournal.Domain/TradingPlans/TradingPlanContent.cs
src/PersonalTradingJournal.Domain/TradingPlans/TradingPlanReadiness.cs
src/PersonalTradingJournal.Domain/TradingPlans/TradingPlanReadyRevision.cs
src/PersonalTradingJournal.Infrastructure/Backups/PortableExportSchema.cs
src/PersonalTradingJournal.Infrastructure/Persistence/Configurations/TradingPlanConfiguration.cs
src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/JournalDbContextModelSnapshot.cs
src/PersonalTradingJournal.Infrastructure/Persistence/Records/TradingPlanRecords.cs
src/PersonalTradingJournal.Infrastructure/TradingPlans/ReviewPlanEvidenceReader.cs
src/PersonalTradingJournal.Infrastructure/TradingPlans/TradingPlanReferenceReader.cs
src/PersonalTradingJournal.Infrastructure/TradingPlans/TradingPlanReferences.cs
src/PersonalTradingJournal.Infrastructure/TradingPlans/TradingPlanStore.cs
tests/PersonalTradingJournal.Application.Tests/TradingPlans/TradingPlanRevisionContractTests.cs
tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/PlanChangesTests.cs
tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/RiskReadinessTests.cs
tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/RiskReadinessViewTests.cs
tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/ScenarioTests.cs
tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/SessionPreparationTests.cs
tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/TradingPlanHistoryTests.cs
tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/TradingPlanHistoryViewTests.cs
tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/TradingPlanWorkspaceTests.cs
tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/TradingPlanWorkspaceViewTests.cs
tests/PersonalTradingJournal.Domain.Tests/TradingPlans/TradingPlanContractTests.cs
tests/PersonalTradingJournal.Domain.Tests/TradingPlans/TradingPlanRevisionTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Backups/PortableExportTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Backups/SavedPeriodPortabilityTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/DailyReview/ReviewPreparationReaderTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Initialization/JournalDatabaseInitializerTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integration/ProductionPersistenceIntegrationTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integrity/SqlitePersistenceIntegrityTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Migrations/InitialMigrationTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePersistenceTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePortabilityTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookPersistenceTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/SharedTradingPlanTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/TradingPlanPersistenceTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/TradingPlanPortabilityTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/TradingPlanReadinessPersistenceTests.cs
src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261019160256_TombstoneTradingPlans.Designer.cs
src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261019160256_TombstoneTradingPlans.cs
tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/PlanDeletionTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/PlanLifecycleChangesTests.cs
```


## History child inactivity follow-up before M21.10

Actual baseline: **clean `develop`, `a27af63b6112ce50b77dc89e73d59c0e14d02280`**, not the previously reported dirty `ddd0c081...` tree. The user had committed the six UI corrections; this task preserves them without editing production code. No production journal, provider request, commit, push, merge or M21.10 work.

### Original failure and evidence

The original complete run remains at `TestResults/PlanUiCorrections/complete/release-20261019-140523-b62b71321f4c44c098b1997f49dd8b2c`: **4,883 passed / 2 failed / zero skipped**. Both parent rows of `TradingPlanHistoryViewTests.CompiledHistoryFrozenViewerTransfersAndRetainedEditorAreAccessible` awaited the same `Lazy<Task>` child. They are **one underlying failure**, not two independent defects. Blame identified the active `(width: 460, dpi: 240)` case; the normal-width child case had not completed independently.

Exact child evidence directory: `parallel/plan-history-20261019-140822-1a230e89858d4aeab8472039a8b1a611` beneath that run. It contains `plan-history.trx`, `stdout.log`, `stderr.log`, `process-supervision.log`, `calendar-sta-phases.log`, `calendar-timings.jsonl`, and `44eb9220-87fc-45cd-a96c-d2e9982663e0/Sequence_47a2ce4a792a4e2e8e11bb5e339f4518.xml` plus `testhost_27436_20261019T170917_hangdump.dmp`. The outer Desktop TRX is `parallel/Yovko_DESKTOP-N2ONT35_2026-10-19_17_08_19_net10.0[1].trx` (verified by its Desktop assembly storage).

The parent command was `pwsh -File scripts/Test-Release.ps1 -ResultsDirectory TestResults/PlanUiCorrections/complete`; the failure occurred in its `Workload!=ExclusiveWpf` parallel partition. The child command constructed by the unchanged supervisor was `dotnet vstest <Release Desktop.Tests.dll> /TestCaseFilter:FullyQualifiedName~PersonalTradingJournal.Desktop.Tests.TradingPlans.TradingPlanHistoryViewTests /Logger:trx;LogFileName=plan-history.trx /ResultsDirectory:<child directory> /Blame:CollectHangDump;HangDumpType=Mini;TestTimeout=30s`, with `PTJ_PLAN_HISTORY_HOST=1`. The outer child deadline is 90 seconds.

UTC chronology: child process 22400 started **14:08:23.189**; testhost 27436's STA started **14:09:05.298**, acquired its dispatcher by **14:09:05.328**, entered the action at **14:09:05.343**; two theme-resource loads completed (1.304s and 0.651s); dump collection began around **14:09:17**; child exited **1** at **14:09:23.778**, with supervisor-confirmed termination. There is no completed child result in its aborted TRX. The 30-second VSTest test-inactivity collector includes fixture/test work before the STA timer; it is not a 30-second allowance starting at WPF action entry. The outer 90-second supervisor did not expire. No normal fixture-cleanup completion was recorded before forced termination.

The retained dump was decoded with `dotnet-dump` 10.0.750501 (installed only under ignored `artifacts/history-deadline/tools`). Content-free thread/stack evidence is retained in `artifacts/history-deadline/original-managed-stacks.log` and `original-sta-locals.log`. Managed thread 14 / OS thread `0x5518` is cooperative in `XmlLanguage.GetLanguage` → glyph drawing → `TextBlock.OnRender` → Arrange/`ContextLayoutManager.UpdateLayout`, dispatched inside the test's `Pump`. The pool threads shown are idle or handling test-platform transport. No SQLite reader/transaction stack, focus/modal wait or fixture shutdown is shown at capture. This is evidence of active layout at that instant, **not proof of continuous progress throughout the preceding interval** or a complete OS scheduling diagnosis. The narrow case never executes the test's native-window focus branch. A single dump cannot establish a deadlock or prove a specific competing process caused the delay.

The generic stderr “test host process crashed” follows the blame collector's explicit inactivity/dump message; it is not independent evidence of a CLR/product crash. Absence of a completed test event is not absence of output (theme timings were written), and neither alone proves absence of functional progress. The last positively completed logged operation was theme-resource loading; the stack capture adds layout-in-progress evidence but cannot identify which historical navigation step owned that pump. New boundaries remove this diagnostic blind spot without changing the pump or dispatcher priorities.

Original overlap included the Session Preparation WPF child, Dashboard chart cases, inline Journal SQLite fixtures and Infrastructure Trade/Playbook/period-review/import cases. Calendar had already exited in the exclusive lane. Its 114.998s lifetime/5.002s headroom is pressure evidence, not a shared-resource root cause. All database roots are per-fixture, connections are owned by DI/factory contexts and tracked by the existing fixture shutdown barrier; there is no shared journal or schema fixture between these children.

### Correction and diagnostic boundaries

The supported correction is **test case granularity**, not a product fix or a claim that the original host slowdown was reproduced. Previously each width case did migration/seeding, both Light and Dark full History workflows and teardown before VSTest saw a completed case. Each `(width, DPI, theme)` now independently performs that entire workflow on fresh synthetic data. All original validation, exact Ready text, read-only, unsaved-editor retention, declined transfer, collision/recovery, copy verification, accessible bounds and native tab-focus assertions remain. There are four cases instead of two. Normal-width native focus still runs in both themes; narrow/high-DPI coverage remains. This is a real completed-work boundary, not periodic output to keep a watchdog alive.

**Unchanged:** 30s inactivity, 30s STA and 90s aggregate child deadlines; cancellation, fatal/timeout handling and process-tree cleanup; Desktop two-collection and Infrastructure four-collection parallelism; exclusive-lane membership; explicit concurrency tests and CI/local gate command. No sleeps, retries, skips, heartbeat or production changes. The total child remains bounded by the original 90 seconds; splitting a case does not waive that bound.

Diagnostics now cover case entry, fixture construction, seed/Ready writes, theme start/completion, view construction, dispatcher idle, Measure/Arrange/UpdateLayout, async task status/pump, optional bitmap rendering, native Show/Close, cancellation and fixture disposal. Existing `PTJ_CALENDAR_TIMINGS=1` scopes report completed durations; phase files identify start/end boundaries. These bounded logs contain no source content or credentials. Pump durations include dispatcher work and are not pure database or scheduling measurements. Fixture cleanup additionally asserts zero active tracked connections and removal of its private directory, after the existing drain/dispose/pool teardown. No async work is intentionally abandoned.

### Experiments and verification

Evidence directories are under `TestResults/HistoryDeadline/`. Separate attempts remain distinct:

| Run | Environment/workload | Result |
| --- | --- | --- |
| `isolated` | Original two-case structure, diagnostics only, sandbox desktop | History 2/2; cold fixture 6.248s, seed 0.348s; Light/Dark narrow workflows 5.553s/1.359s |
| `overlap` | Same binary plus Session Preparation, Daily chart, period repository/evidence, Playbook and import SQLite tests | Desktop 14/14 + Infrastructure 154/154; cold fixture 7.629s, seed 0.419s; narrow workflows 2.753s/1.465s; History child 24.125s |
| `parallel-diagnostic` | All parallel-partition tests, same diagnostic binary, **restricted sandbox desktop** | 4,762 passed / 35 failed / zero skipped; History itself 2/2, child 35.358s. Not a complete gate or acceptance pass. |
| `desktop-baseline` | Original two-case binary on **Default Windows desktop**, same overlap plus synthetic credential tests | Desktop 24/24 + Infrastructure 154/154; History child 22.462s; cases 11.115s / 5.932s |
| `corrected-overlap` | Four-case correction on Default desktop, above competitors plus History logic, process/STA/scheduling and fixture-lifetime regressions | **69/69 Desktop + 154/154 Infrastructure**; History 4/4, child 20.675s, 69.325s aggregate headroom |
| Final complete gate | Repository script, Default desktop, corrected Release binaries | **4,887 passed / 0 failed / 0 skipped; exit 0** |

The restricted parallel diagnostic had synthetic credential failures (including unavailable saved-key storage), one linked-root privilege failure and a Breakdown popup assertion propagated through its child. Those are retained, not silently counted as passing. Its phase logs identify `CodexSandboxDesktop-...`, whereas the original failure used `Default`. The normal-desktop baseline rerun was justified by this environment mismatch, not retry-until-green; all ten selected credential tests then passed without code changes. It does not prove every unrelated restricted failure has a common cause. No real key/provider was used.

The original History timeout has **not** recurred in these instrumented comparisons. Timings demonstrate cumulative cold setup and completed layout work, not a reproducible single blocked action. The case-granularity correction bounds that compound work and improves reporting; residual host/load variability and the exact original off-CPU/native cause remain unproven. Full-gate acceptance depends on the final complete result, not this diagnosis or focused passes.

Normal-desktop baseline cold fixture **6.599s**, seeding **0.347s**, dispatcher ready/scheduled **24.743ms / 6.772ms**; Light/Dark narrow workflows **2.046s / 1.532s**. Fixture teardown **12–16ms**. Under the restricted full-parallel diagnostic, cold setup rose to **9.45s**, with about **8.0s** across all pump intervals and **2.6s** in explicit idle drains (inclusive category sums, not pure CPU/scheduler measurements). The largest measured individual boundary was a **2.597s** idle drain, not an unfinished operation. Corrected normal-desktop case durations: Dark normal **8.392s** (cold fixture), Light normal **3.360s**, Dark narrow **2.555s**, Light narrow **2.293s**; minimum case margin **21.608s**. Different schedules and added infrastructure-regression competitors prevent claiming a controlled throughput speedup from these totals. The correction specifically changes the completed-case boundary, not the meaning of a successful workflow.

Commands: baseline History `dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~TradingPlanHistoryViewTests --logger trx --results-directory TestResults/HistoryDeadline/isolated`; controlled overlap uses Release `--no-build --no-restore` on the solution with filters for the named classes above (Domain/Application have no matching cases in those targeted runs); parallel diagnostic uses `--filter Workload!=ExclusiveWpf --blame-hang --blame-hang-timeout 3m --blame-hang-dump-type mini --diag TestResults/HistoryDeadline/parallel-diagnostic/vstest.log`. Each child retains its own TRX and full stdout/stderr/supervision/phase artifacts. Final build, EF, whitespace and complete-gate commands/results are recorded below after execution.

Reproducible corrected competing-load command (normal Windows test desktop, built Release binaries):

```powershell
$env:PTJ_CALENDAR_TIMINGS = '1'
$env:PTJ_TEST_RESULTS_DIRECTORY = Join-Path (Get-Location) 'TestResults/HistoryDeadline/corrected-overlap'
$loadFilter = 'FullyQualifiedName~TradingPlanHistoryViewTests|FullyQualifiedName~SessionPreparationViewTests|FullyQualifiedName~DailyPnlChartTests|FullyQualifiedName~PeriodReviewRepositoryTests|FullyQualifiedName~DailyReviewEvidenceReaderTests|FullyQualifiedName~PlaybookPersistenceTests|FullyQualifiedName~TradovateImportStoreTests|FullyQualifiedName~CoachingCredentialsTests'
$regressionFilter = 'FullyQualifiedName~IsolatedTestProcessTests|FullyQualifiedName~CalendarStaTestTests|FullyQualifiedName~ReleaseSchedulingTests|FullyQualifiedName~JournalFixtureLifetimeTests|FullyQualifiedName~TradingPlanHistoryTests'
dotnet test PersonalTradingJournal.sln -c Release --no-build --no-restore --filter "$loadFilter|$regressionFilter" --logger trx --results-directory TestResults/HistoryDeadline/corrected-overlap
```

The baseline used `$loadFilter` alone. The earlier sandbox overlap omitted `CoachingCredentialsTests`. No optional PNG output switch was enabled in these timing experiments or the complete gate; the earlier render-enabled 24.668s case is a different workload and remains recorded in the preceding task's report below.

Final gate's **History child passed 4/4**, PID 4420, **15:07:14.592–15:07:43.656 UTC**, **29.063s / 60.937s aggregate headroom**. Case durations: Dark normal **12.093s** (cold setup), Light normal **3.967s**, Dark narrow **3.417s**, Light narrow **4.115s**; minimum case headroom **17.907s**. Cold fixture **6.845s**, seed **0.535s**, dispatcher ready/scheduled **34.840ms / 9.590ms**. Later fixture creations **0.868–1.393s**; all four teardowns **12.512–32.245ms**, with zero active connections and removed roots asserted. Complete output is under the final run's `parallel/plan-history-20261019-150714-c7c40c16dac14ebb93b2f43648089c5c`.

### Final complete verification

`pwsh -File scripts/Test-Release.ps1 -ResultsDirectory TestResults/HistoryDeadline/final` completed once on the final code, **exit 0**, run `release-20261019-150359-7e04ab37d1ec4126bba6e221841c1f9f`. UTC **15:03:59.173–15:12:19.383**, **8m20.210s**. **4,887 passed / zero failures / zero skips**:

| Project / partition | Passed |
| --- | ---: |
| Domain | 567 |
| Application | 1,021 |
| Infrastructure | 1,434 |
| Desktop exclusive WPF | 88 |
| Desktop parallel | 1,777 |
| Desktop combined | 1,865 |

The total increases by two because the two width cases became four width/theme cases, not because coverage was skipped or duplicated in the gate. All original assertion-bearing lines remain unchanged; two fixture-teardown assertions were added. The script's duplicate/empty/skipped-result guards pass. The full failure report and restricted diagnostic are retained separately; no final failed gate was rerun unchanged until green.

Calendar native **69.809s**, **50.191s headroom** to 120s; Journal child **11.183s**, both exit 0 before the parallel phase. No Calendar behavior, assertions or deadlines were changed. These better timings do not establish that host variability is eliminated. The full gate's final summary and top-level TRX are authoritative, rather than summing nested child results.

`dotnet build PersonalTradingJournal.sln -c Release --no-restore`: **0 warnings / 0 errors**, 39.64s. `dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure/PersonalTradingJournal.Infrastructure.csproj --context JournalDbContext --configuration Release --no-build`: **no model changes**, using the isolated design-time context. `git -c core.safecrlf=false diff --check`: **passed**. These checks use the final test code; subsequent edits only record documentation/results. No production UI changed, so no new visual acceptance or human interaction is claimed.

**Local complete regression acceptance is green for this test-only correction.** Whitespace and **168 local documentation links** pass. The exact original host-level slowdown remains uncertain, and independent human/CI acceptance remains open. HEAD stays `a27af63b6112ce50b77dc89e73d59c0e14d02280`; **3 modified / 0 new tracked-source files**, listed below. There is no matching CI evidence for these uncommitted changes; no workflow was triggered and no publication was performed.

### Exact changed files and remaining acceptance

- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/TradingPlanHistoryViewTests.cs`
- `README.md`
- `docs/trading-plan.md`

No production XAML/ViewModel, persistence, schema, migration, runner or workflow is modified. Human keyboard/Narrator, native picker interaction, live application restart and matching CI remain pending. Automated native focus assertions are not human interaction. M21.10 remains paused.

## Six focused UI corrections before M21.10

Baseline: clean `develop`, HEAD `ddd0c08193249a71d7491d7cbdb9965f72452b56`; prior work was already committed. These are Desktop presentation changes only. Shared editing state, commands, validation, All Accounts, frozen references, screenshots and historical evidence remain intact. No schema/export/backup/provider change; M21.10 remains paused.

- The context toolbar wraps whole date/Account/session groups using actual available DIP width. Date-picker arrow chrome no longer adds extra vertical margin; compact input/select content is vertically centered. History uses the same page-local input/action sizes and bottom-aligned filter actions. Existing responsive preparation/levels, scenario and risk grids remain intact; the shared header/sidebar are unchanged.
- Only a **successful empty load** clears its operation notice. No no-plan InfoBox or reserved notice margin remains. Create Draft is still explicit; load/save failures, conflicts and validation are not hidden when no editor exists. Scope loading replaces stale feedback.
- Screenshot timeframe width is **100 DIP**, reusing the production ComboBox's distinct **32-DIP arrow column**, padding and popup minimum width. Upload stays vertically centered with an 8-DIP gap, and that control pair wraps together. Values and image metadata/commands are unchanged.
- Status pills sit immediately above the date group: Draft/unsaved amber, Ready green, working Draft after Ready violet, existing opportunity/Stand Aside intent blue, read-only neutral. These derive from the existing editor/Plan state, not a new lifecycle. No status duplicate remains beside Session, and the strip collapses without an editor. Save/Ready teal and Edit blue are retained.
- The action is **Link selected rule**. Linked rows have a flexible wrapping name column and an Auto-sized X column. Per-Rule expanders, visible revision suffixes, navigation/remove captions and repeated benign paragraphs are removed. Exact frozen content/version and non-assessment explanation remain in keyboard-accessible help/tooltips; changed/unavailable references show a visible warning. The containing Linked Rules section remains expandable.
- X invokes the existing Draft unlink command. It never deletes catalog content, writes automatically, clears unrelated raw input or reconfirms anything. Ready/historical rows have no enabled unlink operation.

### Verification and remaining checks

Synthetic ViewModel and production-resource WPF tests cover empty/error recovery, old feedback clearing, unlink retention, long Rule names, exact seven timeframe labels, status placement, Light/Dark and narrow/240-DPI composition. Final results are recorded here after completion, separately from live interaction.

Initial focused run (`TestResults/PlanUiCorrections/focused`): **132 passed / 3 failed / 0 skipped**. Two new composition checks included the hidden History upload button in a `Single` lookup; the lookup now targets the actual Session Brief preparation component. The new unlink fixture had marked the editor's shared Plan object Ready, correctly disabling unlink; it now constructs an independent historical Plan before testing the working Draft. No production guard, assertion or deadline was weakened. Initial render inspection also found the empty shared-currency error line stretching toolbar selectors; the empty line now collapses and the selectors remain vertically centered.

Second affected run (`TestResults/PlanUiCorrections/focused-final`): **133 passed / 2 failed / 0 skipped**. Both failures were the added composition expectation saying Draft during the Dark pass, which intentionally reopened the same Plan after the Light pass saved Ready and started a new working Draft. That expectation was corrected to reflect the retained lifecycle rather than changing product state; the following bounded fixture split now exercises the full lifecycle independently in each theme.

The subsequent composition run (`TestResults/PlanUiCorrections/composition-final`) passed its normal child case in **24 seconds**, then aborted the high-DPI child at its **unchanged 30-second inactivity deadline**, propagating to both parent cases. It produced 57 of 60 expected images before aborting; this is not claimed as a product hang or a passing run. The expanded test now runs one theme per case (four combinations instead of two cases with both themes), with a fresh synthetic fixture for each. Every lifecycle, input-retention, layout and timeframe assertion remains; both themes explicitly check Draft → Ready → working Draft after Ready → empty context. Case deadline **30s** and child aggregate deadline **90s** are unchanged. This bounds the added render work without retries, skips or changed production scheduling.

Final composition run (`TestResults/PlanUiCorrections/composition-bounded`): **4/4 passed**, completing **137 distinct affected cases** alongside the 133 passed above. Child durations: Light normal **5.626s**, Dark normal **4.966s**, Light 240 DPI **12.610s**, Dark 240 DPI **18.970s** (minimum case headroom **11.030s**). All 60 Session Brief images were generated. History's standalone render fixture now supplies the actual theme background rather than transparent pixels appearing black in an image viewer; this changes no application behavior.

Final solution Release build: **0 warnings / 0 errors**. EF `has-pending-model-changes`: **no model changes**. Refreshed History render/focus run: **2/2 passed**, normal **7.492s**, narrow/240 DPI **24.668s**; child lifetime **40.075s**. Whitespace and **167 local documentation links** pass.

Complete gate: `pwsh -File scripts/Test-Release.ps1 -ResultsDirectory TestResults/PlanUiCorrections/complete`, run `release-20261019-140523-b62b71321f4c44c098b1997f49dd8b2c`. Exclusive WPF **88/88 passed**. Calendar native lifetime **114.998s**, only **5.002s headroom** at its unchanged 120s deadline; Journal child **25.512s**. Historical timing variability is not claimed fixed.

The parallel phase has an **unresolved History child 30-second inactivity failure**, propagated to both `TradingPlanHistoryViewTests.CompiledHistoryFrozenViewerTransfersAndRetainedEditorAreAccessible` parent cases. Blame identifies `(width: 460, dpi: 240)` as active. Its child process started at 14:08:23.189 UTC; the logged STA test action started at 14:09:05.343; hang collection began around 14:09:17; child exited code 1 at 14:09:23.778. Thus only about 12 seconds of logged WPF action preceded collection; these breadcrumbs do **not** prove a hung UI action or identify whether startup, fixture setup or scheduling caused the failure. The isolated pass does not override it. TRX, phase logs, blame sequence and dump are retained under the run's `parallel/plan-history-20261019-140822-1a230e89858d4aeab8472039a8b1a611`. No retry or deadline/assertion change was made in response. Full-gate acceptance remains open.

**Final complete gate: 4,883 passed / 2 failed / 0 skipped, exit 1**, 4,885 cases executed:

| Project / partition | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Domain | 567 | 0 | 0 |
| Application | 1,021 | 0 | 0 |
| Infrastructure | 1,434 | 0 | 0 |
| Desktop exclusive WPF | 88 | 0 | 0 |
| Desktop parallel | 1,773 | 2 | 0 |

Final HEAD remains `ddd0c08193249a71d7491d7cbdb9965f72452b56` on `develop`, with **16 modified / 0 new files** listed below. No matching CI acceptance is claimed for these uncommitted changes. The full gate was not rerun to replace its failure. No production journal, provider request, commit, push, merge or M21.10 work.

The bounded Session Brief composition child passed all **4/4** cases in this complete gate: child **69.464s / 20.536s aggregate headroom**; slowest case **26.435s / 3.565s case headroom**. This is measured variability, not a guarantee against future contention. No global runner or isolation changes were made.

Commands used: `dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~TradingPlans` (TRX/output directories above); subsequent focused filters `FullyQualifiedName~SessionBriefCompositionTests` and `FullyQualifiedName~TradingPlanHistoryViewTests`; `dotnet build PersonalTradingJournal.sln -c Release --no-restore`; `dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure/PersonalTradingJournal.Infrastructure.csproj --context JournalDbContext --configuration Release --no-build`; the complete gate above; `git -c core.safecrlf=false diff --check`. Render switches: `PTJ_BRIEF_RENDER_DIRECTORY`, `PTJ_PLAN_RENDER_DIRECTORY`, `PTJ_PLAN_HISTORY_RENDER_DIRECTORY` point only to the synthetic artifact folders below.

Production-resource render sets: `artifacts/plan-ui-corrections/final/brief` (60), `components` (121), `history` (28). Representative images inspected include:

- [Light Ready, normal](../artifacts/plan-ui-corrections/final/brief/Light-1586-96-ready.png)
- [Dark Ready, narrow/240 DPI](../artifacts/plan-ui-corrections/final/brief/Dark-1000-240-ready.png)
- [Full 15m label, Light narrow/240 DPI](../artifacts/plan-ui-corrections/final/brief/Light-1000-240-timeframe-15m.png)
- [No-plan context without a notice strip](../artifacts/plan-ui-corrections/final/brief/Light-1586-96-no-plan.png)
- [Working Draft after Ready, Dark](../artifacts/plan-ui-corrections/final/brief/Dark-1586-96-working-after-ready.png)
- [Long Rule row, Light normal](../artifacts/plan-ui-corrections/final/components/risk-Light-1100-96-rules-bottom.png)
- [Long Rule row, Dark narrow/240 DPI](../artifacts/plan-ui-corrections/final/components/risk-Dark-460-240-rules-bottom.png)
- [History filters, Light](../artifacts/plan-ui-corrections/final/history/history-Light-980-96-history.png) / [Dark narrow/240 DPI](../artifacts/plan-ui-corrections/final/history/history-Dark-460-240-history.png)

Observed: complete timeframe text with arrow separation, wrapped Rule names without X overlap, compact list spacing, colored status above date, hidden empty notice, retained visible save/error feedback, and intact scrolling in stacked layouts. Automated keyboard focus/validation routing and accessibility properties remain distinct from human interaction.

Native picker/image interaction, human keyboard/Narrator, live application restart and matching CI remain pending. Compiled WPF focus/layout tests and PNG inspection are not human accessibility acceptance. Older-schema archive restore remains unsupported as documented; this presentation-only change does not alter compatibility.

### Exact changed files

- `README.md`
- `docs/trading-plan.md`
- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/RiskReadinessItems.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/SessionPreparationViewModel.Lifecycle.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/SessionPreparationViewModel.Rules.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/TradingPlanWorkspaceViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/RiskReadinessSection.xaml`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/SessionBriefStyles.xaml`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/SessionPreparationSection.xaml`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/TradingPlanHistoryView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/TradingPlanWorkspaceView.xaml`
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/RiskReadinessTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/RiskReadinessViewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/SessionBriefCompositionTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/TradingPlanHistoryViewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/TradingPlanWorkspaceTests.cs`

## Direct Playbook editing and themed feedback before M21.10

Actual starting state: clean `develop`, HEAD `e06fa03e986c403d591d70a1468fcc1fd7de8f72`. Earlier work was already committed and is preserved. Only the three requested changes are implemented; M21.10 remains paused.

### Presentation

Page-level operation feedback is a compact, wrapping InfoBox immediately below the toolbar, reusing the application's semantic palette and live-announcement mechanism. Information/read-only/progress is blue; verified saves are success; validation/readiness/reference-refresh requirements are warning; conflicts/storage/load failures are error. Empty feedback collapses. The embedded editor and History use the same template without duplicated page messages; inline field errors remain visible. No timer hides actionable errors and the template never moves focus.

Top-right actions are page-local: Save Draft uses a pale-teal fill/outline; Mark as Ready uses filled teal/white; Start Draft / Edit uses filled blue/white. Existing commands and CanExecute remain authoritative. Shared focus/disabled behavior and compact wrapping are retained, with opaque hover/pressed states. Global button styles are unchanged.

### Current editing versus frozen evidence

Playbook Save genuinely replaces a mutable current definition under the existing TradingSetup ID. It no longer appends an immutable authoring row. Current revision selectors, publishing/history/deletion controls are removed from the authoring page; Trading Plan selection says “Use current Playbook.” Five guidance sections, images/annotations, catalog archive/reopen, input retention and optimistic concurrency remain. The compatibility value type still carries its internal token and timestamps; no new catalog is introduced.

The additive `20261019100107_DirectPlaybookDefinitions` migration bridges each Setup to exactly the definition the old reader considered current (highest retained revision). It preserves every historical row. Direct saves subsequently replace one current JSON payload and its example-retention index in an immediate SQLite writer transaction, rechecking Setup state/token and verifying readback before success. Stale or invalid changes write nothing. A retained Trade capture materializes its exact definition in the historical ledger within its own transaction; ordinary edits do not. Legacy append/deletion APIs remain compatibility paths, not active Desktop authoring.

Existing Drafts keep exact Setup identity, entered preparation and legacy expected token. Unchanged migrated criteria stay valid; changed current criteria require explicit selection/reference refresh and renewed confirmations. Ready snapshots stay immutable and keep their original criteria, labels, timestamps and Plan-owned assets. The established Plan reference contract freezes five Playbook guidance sections, not Playbook example/image contents; Trade captures retain their full examples, and existing Review packets keep exactly their supported historical representation. No saved packet is rewritten or upgraded implicitly. This change does not reinterpret an association as adherence.

Current images are retained alongside historical/Trade references. Backup/preflight/offline restore include current-only images and verify definition/index/size/hash consistency. Logical export advances to **v12** with `currentPlaybooks` and `currentPlaybookExamples`; older exports lack this representation and are not treated as v12. Exports remain unencrypted, non-restorable, screenshot metadata only. Credentials remain excluded. Older-schema archive restore remains deferred; installed databases migrate normally, archives are not migrated. Lossy downgrade with direct definitions or stored Plans is refused.

All Accounts defaults, explicit currency, frozen screenshots, optional scenario text, scenario Delete, independent designation/direction pills, shared editor/token, navigation guards and Review fallback are unchanged. No P&L/economics/chronology/provider change.

### Exact changed files

- `docs/architecture.md`
- `docs/backup-restore.md`
- `docs/playbook.md`
- `docs/trading-plan.md`
- `README.md`
- `src/PersonalTradingJournal.Application/Backups/IPortableExportService.cs`
- `src/PersonalTradingJournal.Application/Setups/ITradingSetupPlaybookStore.cs`
- `src/PersonalTradingJournal.Application/Setups/SaveTradingSetupPlaybookUseCase.cs`
- `src/PersonalTradingJournal.Desktop/Resources/Themes/DarkTheme.xaml`
- `src/PersonalTradingJournal.Desktop/Resources/Themes/LightTheme.xaml`
- `src/PersonalTradingJournal.Desktop/ViewModels/Setups/PlaybookViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/PlanNotice.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/SessionPreparationViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/SessionPreparationViewModel.Images.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/SessionPreparationViewModel.Lifecycle.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/SessionPreparationViewModel.RiskReadiness.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/SessionPreparationViewModel.Rules.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/SessionPreparationViewModel.Scenarios.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/TradingPlanWorkspaceViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/TradingPlanWorkspaceViewModel.History.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/TradingPlanWorkspaceViewModel.Review.cs`
- `src/PersonalTradingJournal.Desktop/Views/Setups/PlaybookView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/ScenariosSection.xaml`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/SessionBriefStyles.xaml`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/SessionPreparationSection.xaml`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/TradingPlanHistoryView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/TradingPlanWorkspaceView.xaml`
- `src/PersonalTradingJournal.Domain/Setups/TradingSetupPlaybookRevision.cs`
- `src/PersonalTradingJournal.Infrastructure/Backups/PlaybookImageInventory.cs`
- `src/PersonalTradingJournal.Infrastructure/Backups/PortableExportSchema.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/JournalDbContext.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261019100107_DirectPlaybookDefinitions.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261019100107_DirectPlaybookDefinitions.Designer.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/JournalDbContextModelSnapshot.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Records/PlaybookRecords.cs`
- `src/PersonalTradingJournal.Infrastructure/Screenshots/RetainedScreenshotFileStorage.cs`
- `src/PersonalTradingJournal.Infrastructure/Screenshots/TradeScreenshotDeletionStore.cs`
- `src/PersonalTradingJournal.Infrastructure/Setups/TradingSetupPlaybookStore.cs`
- `src/PersonalTradingJournal.Infrastructure/Trades/AccountTradeDeletionStore.cs`
- `src/PersonalTradingJournal.Infrastructure/Trades/TradeDeletionStore.cs`
- `src/PersonalTradingJournal.Infrastructure/Trades/TradePlaybookAssociationStore.cs`
- `src/PersonalTradingJournal.Infrastructure/Trades/TradePlaybookContextStore.cs`
- `src/PersonalTradingJournal.Infrastructure/TradingPlans/TradingPlanReferences.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Setups/PlaybookSqliteViewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Setups/PlaybookValidationDeletionTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Setups/PlaybookViewModelTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Setups/PlaybookViewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/ScenariosViewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/ScenarioTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/SessionBriefCompositionTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/TradingPlanHistoryViewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/TradingPlanWorkspaceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/PortableExportTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/SavedPeriodPortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Initialization/JournalDatabaseInitializerTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integration/ProductionPersistenceIntegrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integrity/SqlitePersistenceIntegrityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Migrations/InitialMigrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/DirectPlaybookTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookImagePersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookPersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookRevisionDeletionTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/SharedTradingPlanTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/TradingPlanPersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/TradingPlanPortabilityTests.cs`

### Verification and remaining limits

**Final complete Release gate passed: 4,881 passed, zero failures/skips, exit 0.** Domain 567, Application 1,021, Infrastructure 1,434, Desktop 1,859 (88 exclusive WPF + 1,771 complementary parallel cases). Command: `pwsh -File scripts/Test-Release.ps1 -ResultsDirectory TestResults/DirectPlaybook/complete`. Evidence: `TestResults/DirectPlaybook/complete/release-20261019-102723-4c478d88b9ec4950811ad9207a716ad2/summary.json`, stage TRX/VSTest logs and child supervision/phase logs. Calendar native process **71.374s / 48.626s headroom** against 120s; Journal editor child **10.793s**. Assertions, deadlines, isolation and explicit concurrency tests are unchanged. There was no full-gate failure in this change set; this passing run does not prove historical Calendar variability fixed.

Initial failures are retained under `TestResults/DirectPlaybook`: first focused run had 483 passed / 32 failed (obsolete append/history/export/schema assumptions, plus a downgrade-order guard gap). The downgrade refusal now occurs before dropping new tables; compatibility fixtures explicitly seed legacy history. Second focused run had 323 passed / 3 failed in completed Domain/Application/Infrastructure projects, with Desktop blocked by a status-switch compile error. Third run passed 328 Domain/Application/Infrastructure cases; Desktop still hit that compile error. Fourth Desktop attempt exposed a nullable test diagnostic. Both compile errors were corrected before the next run. These are failed/interrupted attempts, not acceptance passes.

New persistence coverage explicitly checks direct current editing with stable identity/no authoring history, two competing writers, metadata rollback/cancellation, authoritative legacy migration, retained Trade history, legacy Draft reconfirmation/unchanged Ready data, and current-only image export → backup → preflight → offline restore. Existing saved Review/packet compatibility and historical navigation tests remain. Obsolete active-authoring expectations were replaced with direct-editing assertions; legacy deletion/history cases remain as compatibility coverage, not advertised UI.

Focused final verification: **517 affected cases passed** across Domain 99, Application 55, Infrastructure 174 and Desktop 189; the final simplified-selection compiled-WPF check passed **1/1** separately after its additional accessibility assertion. Five new SQLite cases increase the complete test set; no unrelated cases or assertions were removed. The fifth Desktop attempt had 187 passed / 2 failed because two stale-write assertions still counted immutable authoring rows; the sixth passed 189/189 after asserting the current token and zero authoring rows instead. The initial WPF build also rejected an unsupported accessibility attached property; it was removed before successful compilation. No timeout or assertion was weakened.

Commands: `dotnet test PersonalTradingJournal.sln -c Release --no-restore --filter 'FullyQualifiedName~Playbook|FullyQualifiedName~TradingPlans|FullyQualifiedName~PortableExport|FullyQualifiedName~InitialMigrationTests|FullyQualifiedName~SqlitePersistenceIntegrityTests'`; final Desktop rerun uses its project with `FullyQualifiedName~Playbook|FullyQualifiedName~TradingPlans`, and the final selection check uses `FullyQualifiedName~ScenariosViewTests`. Exact TRX runs are under `TestResults/DirectPlaybook/{first-focused,second-focused,third-focused,fourth-desktop,fifth-desktop,sixth-desktop,scenario-final}`. Compile-blocked attempts have no completed Desktop TRX. `dotnet build PersonalTradingJournal.sln -c Release --no-restore` passes with **0 warnings/errors**; `dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure/PersonalTradingJournal.Infrastructure.csproj --context JournalDbContext --configuration Release --no-build` reports **no pending changes**. Tracked/new-file whitespace and **189 local documentation file links** pass.

Render evidence: `artifacts/direct-playbook/brief` has **48** production-shell-tree images (Light/Dark, 1586×1000 at 96 DPI and 1000×850 at 240 DPI). `playbook` has **56** current/editor/legacy-preview/example/Trade-capture images; `scenarios` has **20** normal/narrow selection/validation/read-only images. Representative images were visually inspected: [Light Save success](../artifacts/direct-playbook/brief/Light-1586-96-save-success.png), [Dark read-only InfoBox](../artifacts/direct-playbook/brief/Dark-1586-96-readonly-info.png), [narrow Light storage error](../artifacts/direct-playbook/brief/Light-1000-240-save-error.png), [narrow Dark Ready](../artifacts/direct-playbook/brief/Dark-1000-240-ready.png), [Dark warning with inline validation](../artifacts/direct-playbook/brief/Dark-1586-96-invalid.png), [direct editor](../artifacts/direct-playbook/playbook/playbook-Light-960-96-editor.png), [narrow Dark current guidance](../artifacts/direct-playbook/playbook/playbook-Dark-480-240-current.png), [current selection](../artifacts/direct-playbook/scenarios/scenarios-Light-1100-96-multiple.png) and [narrow Dark selection](../artifacts/direct-playbook/scenarios/scenarios-Dark-460-240-multiple.png). Info template/empty collapse is tested independently as well as actual success/error command results. Toolbar colors/disabled states are legible, feedback wraps without overlap, and long content remains scrollable. Narrow layouts necessarily require more vertical scrolling; no claim is made that all sections fit at once. Native hover/focus perception remains a human check.

Final documentation file-link check covers **198 links**, including the nine render links added after the earlier 189-link check. The exact inventory above is **67 files: 63 modified, 4 new**; branch/HEAD remain `develop` / `e06fa03e986c403d591d70a1468fcc1fd7de8f72`.

Automated production-resource WPF renders are distinct from human interaction. Native picker/full-image use, human keyboard/Narrator, live application restart and matching CI remain unverified for this change set. No published commit represents these uncommitted changes. Historical Calendar variability is not claimed fixed by a passing run. No production journal or provider was accessed; no commit/push/merge/branch switch occurred.

## Shared preparation and timeframe screenshots before M21.10

### Scope, currency and evidence

Actual starting state was **clean develop**, HEAD `5953be5748cf009721169d77d4bf2fc0defbc8e7`. The supplied older dirty baseline had already been committed; this change preserves that compact Session Brief and its editor/navigation guards.

- A fresh workspace selects **All Accounts**. This is one logical shared New York-date Plan, not a synthetic Account, copied Plans, or frozen list of Account IDs. `TradingPlanScope` distinguishes shared versus exact Account; nullable Account ID encodes shared scope without Guid.Empty. Separate SQLite partial unique indexes enforce global/date and exact-Account/date uniqueness.
- Exact Account navigation, unavailable historical identities, Ready history and guarded scope switching remain supported. Copy Forward retains the source scope and currency but clears daily screenshots and existing daily/checklist fields according to the established policy.
- Shared Drafts require explicit canonical currency. A fresh workspace defaults it only if active Accounts have one common currency; mixed/empty catalogs require entry. Limits are one trader-wide plan, not multiplied by Accounts. Currency edits invalidate risk confirmation; Ready freezes the chosen currency. No FX or monetary reinterpretation is introduced.
- At each Trade's UTC entry/New York entry date, Review prefers the latest eligible exact-Account Ready revision, otherwise eligible global Ready preparation. Draft-only/retrospective exact Plans do not suppress fallback. Shared revisions are deduplicated by stable revision identity, including All accounts and period snapshots. Identity matches remain observations, never adherence.
- Persisted Draft/Ready payload **v2** reconstructs legacy **v1** unchanged; old Account Plans are not merged. New `daily-coaching.v4` / `period-coaching.v4` use `review-preparation.v2`; saved v1/v2/v3 reviews and supported pending v2/v3 remain readable. Portable export **v11** is independently versioned. No image binaries, filenames or storage keys enter coaching packets; the handling text states images were not inspected.

### Screenshots and scenarios

The optional screenshot row supports **1m, 5m, 15m, 1h, 4h, D, W**, multiple thumbnails per selected Instrument, click-to-view and Draft-only Remove. The native picker filters PNG/JPEG; actual decoding is required, with existing **8 MiB**, **8192 pixels/side**, **16-million-pixel**, single-frame limits. Thumbnails decode to bounded size and release file handles. At most **24 attachments per Plan** are accepted; pending original bytes can occupy up to 192 MiB plus thumbnail/decoding overhead. No OCR, annotation editor or vision call is added.

Selected bytes stay in memory until explicit Save. Storage publishes a fresh immutable key; metadata is written inside the existing optimistic SQLite transaction and verified readback. Failure/cancellation attempts reference-aware compensation and reports cleanup failure, retaining input for retry. Database/file operations are **not** claimed atomic. Immutable `TradingPlanAssets` ownership and Ready/Draft metadata preserve original bytes and timeframe. Removing a saved Draft attachment does not physically delete retained assets; even assets no longer present in any Ready are conservatively retained. No garbage collector is introduced. Copy Forward never carries screenshots into a new date.

Complete current-schema backup/preflight/offline restore includes retained image binaries and checks Draft/Ready metadata against the asset ledger plus sizes/hashes. Logical export v11 carries metadata only, not image binaries, and is not a restorable backup. Older-schema archive restore remains deferred; this migration's downgrade with stored Plans/assets is refused (empty-schema rollback remains supported) rather than discarding assets or fabricating scope.

Instrument actions are **Add** (teal) and **Remove** (red outline), reusing the existing commands. Scenario/screenshot references block Instrument removal. Scenario Name, IF, THEN, INVALID IF and Playbook are optional through Save/Ready/reconstruction; blanks remain unspecified, and empty cards are retained. Direction/identity/reference validity and other existing readiness requirements remain. The prior “Remove trading scenarios…” blocker came from explicit StandAside intent, not missing prose: ordinary no-trade text may coexist with scenarios, while the visible **Stand aside only** checkbox deliberately rejects a contradictory scenario list and explains how to resolve it.

Visible scenario-header **Delete** uses the existing mutation and confirmation invalidation. Primary/Alternative and Long/Short are independent keyboard-operable pills with state/next-action accessible labels, teal/violet text and arrow cues, opaque focus/hover/pressed/disabled styling; card accent follows direction. Ready pills and attachment mutations are disabled. After Mark Ready, thumbnails load on the new read-only editor immediately.

### Verification and remaining acceptance

Only synthetic databases/images and fake HTTP were used. No production journal, API/provider request, commit, push, merge or branch switch. M21.10 remains paused.

Focused verification and complete-gate results are recorded below after the final run. Initial runs exposed outdated exact-Account fixture indexing and packet/export version assertions; these were updated for the intentional contract changes. A new fixture initially added a Domain Account directly to an EF record set (compile error, corrected through the repository), and a new legacy test compared reference-class instances rather than their immutable fields (corrected to compare all fields). No behavioral assertion/deadline was relaxed.

Automated production-resource shell-tree renders cover Light/Dark at **1586×1000 DIP/96 DPI** and **1000×850 DIP/240 DPI**, with global scope, compact empty screenshots, two timeframe thumbnails, optional blank scenario text, expanded Instrument actions, independent pill combinations, invalid input and read-only Ready states. Narrow layout stacks and requires scrolling; screenshots necessarily increase preparation height. These are compiled WPF renders and automated interactions—not a live mouse/picker, human keyboard or Narrator result. Files are under `artifacts/plan-changes/final/`; test results under `TestResults/PlanChanges/`.

Human native picker/full-image interaction, keyboard/Narrator, actual isolated-app restart, and matching CI remain pending. Synthetic SQLite restart and offline restore are covered separately. A passing current gate must not erase the previously recorded Calendar restored-size/117.868s and Breakdown F4 variability, or imply those historical failures were fixed.

### Final local results

Focused final feature run: **404 passed** (Domain 77, Application 35, Infrastructure 145, Desktop 147), zero failed/skipped, exit 0;
`dotnet test PersonalTradingJournal.sln -c Release --no-restore` with TradingPlans/ReviewPreparation/ManualReview/PortableExport/PeriodSqlite/GroqInputBudget/PeriodCoachingProvider filters, results in `TestResults/PlanChanges/verified-focused/`.

The first complete gate (`full-gate/release-20261019-083756-23293158b0094736bbef4dc5c5f2efbe`) finished **4,866 passed / 10 failed / zero skipped**, exit 1. Five migration round-trip cases hit an over-broad unconditional downgrade rejection; the corrected migration now checks for stored Plans/assets before changing anything and permits only an empty Plan schema to downgrade. The other five failures were exact-schema expectations (migration count, table/column inventory and EF FK/index/entity inventory). Expectations now explicitly include the asset ledger, restricted ownership and both partial scope indexes; none were removed or weakened. SQLite's nullable-column rebuild changes Plan column order, now asserted exactly. All **34 targeted migration/integrity/retention checks pass** in `TestResults/PlanChanges/schema-verified/`. Desktop in that first gate passed **1,859/1,859**, including 88 exclusive WPF cases.

Final complete gate: **4,876 passed, zero failures/skips, exit 0** — Domain **567**, Application **1,021**, Infrastructure **1,429**, Desktop **1,859** (88 exclusive + 1,771 parallel). Command: `pwsh -File scripts/Test-Release.ps1 -ResultsDirectory TestResults/PlanChanges/final-gate`; evidence: `release-20261019-084852-b311733db5974f7b89a19bd48ab3150c`. Calendar native **66.202s / 53.798s headroom** against 120s; Journal STA **7.915s / 22.085s headroom** against 30s; Session Brief child **24.001s / 65.999s headroom** against 90s. This is a local pass, not evidence that historical intermittent failures were fixed.

Final Release build: **0 warnings/errors**. EF `has-pending-model-changes` through the in-memory design-time factory: **no changes**. `git diff --check`, all eight new-file whitespace checks and **197 local documentation links** pass. Final HEAD remains `5953be5748cf009721169d77d4bf2fc0defbc8e7` on develop; **73 modified + 8 new files** remain uncommitted. No matching CI acceptance is claimed for these working-tree changes.

Inspected render evidence: Light wide expanded Instrument actions, populated screenshots/optional fields; Light narrow empty screenshots; Dark narrow scrolled Primary + Short pills; Dark wide Ready read-only. The 36-image final set uses production resources. Empty screenshot preparation is **356 DIP** high at the wide viewport, populated is **489 DIP**; narrow populated preparation is **625 DIP** and scrolls. Disabled Add is shown when no additional Instrument is selected; live hover/picker/full-image, human keyboard/Narrator and actual-app restart remain unverified.

| Boundary | Automated evidence | Remaining acceptance |
| --- | --- | --- |
| Global/exact scope, currency, uniqueness/history/copy | Real SQLite restart/migration and ViewModel tests | Live fresh-navigation/scope change |
| Optional scenarios and independent pills | SQLite Save → Ready → restart; bound production WPF commands/readonly states | Human Space/Enter/focus/Narrator |
| Screenshot upload/remove/retention | Picker cancellation/failure/compensation fakes; real decoder/storage and byte round-trip | Native picker and modal image interaction |
| Review fallback and compatibility | Entry-time exact/global tests, two-Account deduplication, old v1 reconstruction, manual/fake-HTTP suites | No provider call authorized |
| Backup/preflight/offline restore/export | Synthetic assets and frozen Ready round-trip; export v11 metadata-only | Older-schema archive restore still deferred |
| Complete regression / CI | Final local gate reported below | Matching CI cannot cover uncommitted files |

### Exact changed files

The following 81 files differ from the clean starting commit (generated migration designer/model snapshot included):

```text
README.md
docs/architecture.md
docs/backup-restore.md
docs/daily-review.md
docs/period-reviews.md
docs/trading-plan.md
src/PersonalTradingJournal.Application/Backups/IPortableExportService.cs
src/PersonalTradingJournal.Application/DailyReview/Coaching/CoachingEvidencePacket.cs
src/PersonalTradingJournal.Application/DailyReview/Coaching/CoachingResponseValidator.cs
src/PersonalTradingJournal.Application/DailyReview/Coaching/ManualReview.cs
src/PersonalTradingJournal.Application/PeriodReviews/Coaching/PeriodCoachingPacket.cs
src/PersonalTradingJournal.Application/TradingPlans/ReviewPlanEvidence.cs
src/PersonalTradingJournal.Application/TradingPlans/TradingPlanContracts.cs
src/PersonalTradingJournal.Desktop/App.xaml.cs
src/PersonalTradingJournal.Desktop/Resources/Themes/DarkTheme.xaml
src/PersonalTradingJournal.Desktop/Resources/Themes/LightTheme.xaml
src/PersonalTradingJournal.Desktop/Screenshots/TradingPlanImages.cs
src/PersonalTradingJournal.Desktop/ViewModels/DailyReview/ReviewPresentation.cs
src/PersonalTradingJournal.Desktop/ViewModels/PeriodReviews/PeriodPresentation.cs
src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/ScenarioEditor.cs
src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/SessionPreparationViewModel.Images.cs
src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/SessionPreparationViewModel.RiskReadiness.cs
src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/SessionPreparationViewModel.Scenarios.cs
src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/SessionPreparationViewModel.cs
src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/TradingPlanWorkspaceViewModel.History.cs
src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/TradingPlanWorkspaceViewModel.Review.cs
src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/TradingPlanWorkspaceViewModel.cs
src/PersonalTradingJournal.Desktop/Views/TradingPlans/ScenariosSection.xaml
src/PersonalTradingJournal.Desktop/Views/TradingPlans/SessionBriefStyles.xaml
src/PersonalTradingJournal.Desktop/Views/TradingPlans/SessionPreparationSection.xaml
src/PersonalTradingJournal.Desktop/Views/TradingPlans/TradingPlanWorkspaceView.xaml
src/PersonalTradingJournal.Domain/TradingPlans/TradingPlanContent.cs
src/PersonalTradingJournal.Domain/TradingPlans/TradingPlanIdentity.cs
src/PersonalTradingJournal.Domain/TradingPlans/TradingPlanReadiness.cs
src/PersonalTradingJournal.Domain/TradingPlans/TradingPlanReferenceSnapshot.cs
src/PersonalTradingJournal.Domain/TradingPlans/TradingPlanScreenshot.cs
src/PersonalTradingJournal.Domain/TradingPlans/TradingPlanSnapshotReconstruction.cs
src/PersonalTradingJournal.Infrastructure/Backups/PlaybookImageInventory.cs
src/PersonalTradingJournal.Infrastructure/Backups/PortableExportSchema.cs
src/PersonalTradingJournal.Infrastructure/Backups/TradingPlanAssetInventory.cs
src/PersonalTradingJournal.Infrastructure/DailyReview/Coaching/GroqCoachingProvider.cs
src/PersonalTradingJournal.Infrastructure/DailyReview/Coaching/OpenAiCoachingContract.cs
src/PersonalTradingJournal.Infrastructure/DailyReview/Coaching/PeriodCoachingWireContract.cs
src/PersonalTradingJournal.Infrastructure/Persistence/Configurations/TradingPlanConfiguration.cs
src/PersonalTradingJournal.Infrastructure/Persistence/JournalDbContext.cs
src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261019080838_SharedTradingPlansAndScreenshots.Designer.cs
src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261019080838_SharedTradingPlansAndScreenshots.cs
src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/JournalDbContextModelSnapshot.cs
src/PersonalTradingJournal.Infrastructure/Persistence/Records/TradingPlanRecords.cs
src/PersonalTradingJournal.Infrastructure/Screenshots/RetainedScreenshotFileStorage.cs
src/PersonalTradingJournal.Infrastructure/TradingPlans/ReviewPlanEvidenceReader.cs
src/PersonalTradingJournal.Infrastructure/TradingPlans/TradingPlanJson.cs
src/PersonalTradingJournal.Infrastructure/TradingPlans/TradingPlanReferenceReader.cs
src/PersonalTradingJournal.Infrastructure/TradingPlans/TradingPlanReferences.cs
src/PersonalTradingJournal.Infrastructure/TradingPlans/TradingPlanStore.cs
tests/PersonalTradingJournal.Desktop.Tests/PeriodReviews/PeriodSqliteTests.cs
tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/PlanChangesTests.cs
tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/ReviewPreparationViewTests.cs
tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/RiskReadinessTests.cs
tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/ScenarioTests.cs
tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/SessionBriefCompositionTests.cs
tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/SessionPreparationViewTests.cs
tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/TradingPlanHistoryTests.cs
tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/TradingPlanHistoryViewTests.cs
tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/TradingPlanWorkspaceTests.cs
tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/TradingPlanWorkspaceViewTests.cs
tests/PersonalTradingJournal.Domain.Tests/TradingPlans/TradingPlanContractTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Backups/PortableExportTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Backups/SavedPeriodPortabilityTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/DailyReview/GroqInputBudgetTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/DailyReview/PeriodCoachingProviderTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/DailyReview/ReviewPreparationReaderTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Initialization/JournalDatabaseInitializerTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integration/ProductionPersistenceIntegrationTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integrity/SqlitePersistenceIntegrityTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Migrations/InitialMigrationTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePersistenceTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePortabilityTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookPersistenceTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/SharedTradingPlanTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/TradingPlanPortabilityTests.cs
```

## Session Brief visual correction before M21.10

Baseline: clean `develop`, **`03269f64e555421543cbf10c1c14b0431e7b83ee`**. The reported M21.9 dirty changes had already been committed and are preserved. M21.10 remains paused. This correction changes Desktop composition only: no schema, money, evidence chronology, provider, or saved-data contract change.

### Cause and correction

The production shell supplied 1,270 content DIP at a 1,586 × 1,000-DIP viewport. The original component sections combined explicit 16/20-DIP type, shared 36-DIP controls with larger padding, 64/76-DIP multiline minimums, repeated instrument selectors, individually stacked scenario fields and vertically stacked risk forms. Additional page/card padding and always-expanded secondary controls compounded the height. The first full-shell baseline measured **789 DIP for preparation**, and only its upper part fitted on screen. Isolated component tests did not establish whole-page visual similarity.

Corrections against the approved `Trading Plan Session Brief.png`:

- Underlined, keyboard-focusable Session Brief/History tab presentation replaces radio circles. Save Draft/Mark as Ready/Start Draft remain the existing commands on the right, wrapping below tabs when necessary. Reload and contextual Review navigation are in the footer. History retains its own operations; Session Brief write actions are not shown over History.
- Date arrows, raw-preserving date picker, explicit **Go/Enter**, Today, exact Account, session and a small Draft/Ready badge form a compact wrapping toolbar. The New York date convention is stated separately from session metadata; full state/context remains available through accessible help and existing status feedback.
- Focus is an inline pale-teal banner. Preparation has one theme surface, numbered heading/divider, compact instrument/bias controls, expandable Add/Remove Instrument, thesis, three paired level rows and full-width manual News notes. This does not introduce a structured news calendar or new persistence fields.
- Scenario designation/direction/name form one header; IF/THEN/INVALID IF align alongside inputs. Two cards share a wide row; additional cards wrap and narrow layouts stack. Playbook selection/link/open, instrument choice and reorder/remove remain in accessible secondary options; saved Playbook context remains readable. Stand Aside is a slim strip with its existing explicit boolean and complete text.
- Limits use four columns when space permits, then two; checklist confirmations use compact labels with their full accessible names/descriptions and tooltips. Confirmation count/progress, required currency review, inline errors and readiness blockers remain visible. Linked Rules expand without losing frozen descriptions or operations.
- `SessionBriefStyles.xaml` is merged only by Trading Plan components: 13-DIP body/input text, 17-DIP headings, 32-DIP minimum controls, theme resources and inherited shared validation/focus/hover/disabled templates. Shared application title/sidebar are unchanged. `MainWindow` has a **TradingPlan-only** 16/8-DIP content-padding override; no other destination is resized.

At odd production content widths, the two half-width scenario containers could both round up under layout rounding and wrap the second card. Flooring the half width leaves rounding headroom. The new shell-level assertion verifies both cards on the same row at wide width, and stacking at narrow/high DPI. No ordering, selection or source behavior changes.

### Visual evidence and verification

Synthetic before captures: `artifacts/session-brief-correction/before/`; final after captures: `artifacts/session-brief-correction/after-final/`. They use the **actual MainWindow content tree**, shared resources, header/sidebar, inherited typography and production content container, at **1,586 × 1,000 DIP / 96 DPI** and **1,000 × 850 DIP / 240 DPI**, Light/Dark. The shell tree is rendered without a native HWND to avoid the test monitor's work-area clamping; this is automated WPF rendering, not a live screenshot or human interaction. Existing tests separately exercise real owned windows, focus and close guards. Draft, Ready/read-only, invalid input and scrolled-bottom states are captured. Long fields retain wrapping/internal scrolling rather than shrinking text or truncating data.

Render-host development initially caught an invalid DPI root, detached inherited DataContext/namescope issues, and a hidden History scenario-list lookup. These were corrected in the **new test host**; they are not product Save defects. The first affected run was **126 passed / 2 failed**, because old tests searched for actions before expanding their newly collapsed containers. Tests now expand the controls before preserving their original command assertions. A subsequent run was **126 passed / 2 failed** in the new host's hidden-list lookup; the corrected affected run passed **128/128**, zero skipped. No deadline, concurrency, assertion or retry policy was relaxed. Final verification is recorded below after integration.

Direct visual comparison was made with the supplied approved reference and the before/after shell renders, not raw pixels at dissimilar scaling. Wide content width increased from **1,270 to 1,302 DIP** through the TradingPlan-only padding change. Preparation decreased from **789 to 312 DIP** (about 60%); the final two-card scenario section is **366 DIP**. Wide Risk & Readiness is **389 DIP** with incomplete confirmations/blockers, **239 DIP** when Ready. At the 1,000-DIP/240-DPI window, available content is **716 DIP**: preparation stacks at **448 DIP**, scenarios at **617 DIP**, with vertical scrolling. The focus banner remains below 85 DIP for short content in both sizes. Coverage checks actual bounds, wide same-row scenario placement, narrow stacking, first-invalid input, retained input and verified Ready readback—not brush-name or padding-value mirrors.

Inspected final images include Light wide Draft, Dark wide Ready/bottom, Light narrow Draft and Dark narrow invalid/bottom. Both themes also have Ready and scrolled-bottom captures. The page now follows the numbered-card hierarchy with compact controls and deliberate stacking. It is **not pixel-identical** to the illustrative reference: the unchanged shared header is taller, explicit Go and verification/status text remain, and visible readiness blockers plus secondary operations require additional scrolling. The linked Playbook/Rule pickers expand inline rather than introducing new modal/flyout infrastructure. Native checkbox/expander glyphs retain the established WPF appearance. No actual human keyboard/Narrator or monitor-DPI interaction is claimed from these renders.

Broader affected verification: **334/334 passed**, zero failed/skipped, including shell/navigation guards (`TestResults/SessionBrief/focused-final`). Final complete Release gate: **4,865 passed / zero failed / zero skipped, exit 0**, with two new composition cases:

| Partition / project | Passed |
| --- | ---: |
| Exclusive WPF / Desktop | 88 |
| Parallel / Domain | 567 |
| Parallel / Application | 1,021 |
| Parallel / Infrastructure | 1,423 |
| Parallel / Desktop | 1,766 |

Evidence: `TestResults/SessionBrief/full-gate/release-20261018-202009-73f0fb8e0cb644a79f38604f6a9f7017/summary.json`, TRX and supervised child logs. This run includes the final visibility/selector-width adjustments after the focused run and regenerates all **20** final renders. Desktop parallel finished in **5m56s**. Calendar native **71.998s / 48.002s headroom** to 120s; new composition child **31.770s / 58.230s headroom** to 90s; existing workspace child **25.639s**. Journal editor child passed in **9.974s**. No assertion, deadline, isolation or concurrency policy changed. The previous Calendar/Breakdown failures did not recur; this is not a demonstrated fix for their unresolved variability.

Final Release build: **0 warnings/errors**. EF `has-pending-model-changes` uses the repository's **in-memory design-time factory**, reports no changes, and never opens the installed journal. Tracked and new-file whitespace checks pass; **146 local documentation links** resolve. Final branch/HEAD remain `develop` / `03269f64e555421543cbf10c1c14b0431e7b83ee`, with **14 modified + 2 new files**, all listed below. No commit, push, merge or branch switch. Matching CI cannot represent these uncommitted changes; M21.10 and live human acceptance remain pending.

Commands:

```powershell
dotnet build PersonalTradingJournal.sln -c Release --no-restore
$env:PTJ_BRIEF_RENDER_DIRECTORY = Join-Path $PWD 'artifacts/session-brief-correction/after-final'
dotnet test tests/PersonalTradingJournal.Desktop.Tests -c Release --no-build --no-restore --filter 'FullyQualifiedName~TradingPlans|FullyQualifiedName~MainWindowViewModelTests' --logger trx --results-directory TestResults/SessionBrief/focused-final
pwsh -File scripts/Test-Release.ps1 -ResultsDirectory TestResults/SessionBrief/full-gate
$env:PTJ_BRIEF_RENDER_DIRECTORY = $null
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure/PersonalTradingJournal.Infrastructure.csproj --context JournalDbContext --configuration Release --no-build
git diff --check
```

The previous M21.9 gate remains historical evidence: **4,776 passed / 87 failed**, Calendar restored-panel-size propagation and a separate Breakdown F4 popup failure. No code in those features, deadlines or test scheduling is changed here. A later passing gate would not prove their causes fixed. Live keyboard/Narrator, actual application restart and matching CI for the eventual published change remain open; no production journal or provider is accessed.

### Exact changed files

- `README.md`
- `docs/trading-plan.md`
- `src/PersonalTradingJournal.Desktop/MainWindow.xaml`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/SessionBriefStyles.xaml` (new)
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/TradingPlanWorkspaceView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/TradingPlanWorkspaceView.xaml.cs`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/SessionPreparationSection.xaml`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/ScenariosSection.xaml`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/ScenariosSection.xaml.cs`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/RiskReadinessSection.xaml`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/RiskReadinessSection.xaml.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/SessionBriefCompositionTests.cs` (new)
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/TradingPlanWorkspaceViewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/SessionPreparationViewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/ScenariosViewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/RiskReadinessViewTests.cs`

## M21.9 — Trades, Review and AI evidence integration

Actual baseline: **clean `develop`, `fbeaa7eead77d6931a41e5688e6d31c8f706b3b4`**. M21.8 had been committed; no earlier dirty files were present. No migration, financial/source-selection change, Trade/Journal/Rules write, provider call or production access. This implements M21.9 only, not M21.10 acceptance.

### Snapshot, chronology and limitations

Daily, Weekly and Monthly retain their existing responsibilities: Daily carries complete daily evidence; periods carry complete local snapshots plus a compact, coverage-explicit supplied projection. All use the existing explicit manual ChatGPT prepare/copy/paste/validate/save lifecycle. No new Review engine, association store, calculator, evaluator or network path is introduced.

Preparation reads share the existing SQLite transaction with Trades, Journals, frozen Playbook context and period Rule evidence. Three additional SELECTs load bounded Plan identities/state, selected Ready count/character total, then only the latest and entry-eligible Ready payloads. The populated Daily regression records **9 SELECTs total**, no tracked entities/writes, independent of its Trade count. No Draft prose and no one-query-per-Trade loop. Limits are **1,024 Account/day markers, 1,024 selected revisions, 10,000 Trade comparisons and 2,000,000 stored Ready JSON characters**, before materializing revision payloads; existing packet byte/source bounds still apply.

For each selected Trade, use the canonical **UTC opening/entry timestamp**, convert it to the **New York entry date**, then select that exact Account/date's greatest Ready timestamp **at or before entry**, breaking equal timestamps by highest numbered revision. Equality is a recorded-instant boundary, not proof of event ordering. Later preparation cannot backdate itself. Latest Ready remains display context only; overnight Trades can depend on a Plan outside their closure-date Review range. Unsupported/missing entry facts yield unknown timing. Closed-Trade economics still use closure date; open/partial/unavailable-lifecycle activity remains separate and executions are not counted as Trades.

Coverage distinguishes No Plan, Draft only, Ready, Ready with newer Draft changes, retrospective-only preparation and unknown entry time. Exact Account reads show each requested date; aggregate reads cover saved Plans and observed exact Account/day dependencies, **not** every empty catalog Account × day. Null-scoped Journals do not become Plans for an invented Account. Later Draft text never substitutes for Ready. Frozen revision payloads retain actual Ready/capture times, focus, instrument context/bias/manual levels, scenarios, Stand Aside, planned limits/currency, selected Playbook/Rule definitions and manual confirmations. Images/paths are excluded by the existing Plan reference contract.

Calculated comparisons are limited to temporal eligibility, recorded Instrument/Setup identity matches, Stand Aside intention versus recorded Trade activity, and Account/pricing-currency identity. Missing Setup stays unknown. These are not adherence, price-action confirmation or Rule violations. No initial risk is inferred from realized P&L; no maximum-Trade, risk/loss-limit enforcement or score is added. Unknown commissions/fees and strict Net remain unchanged; no FX is performed. Stand Aside with no activity is preparation evidence, not proof of future compliance.

### Contracts and historical behavior

- **Daily `daily-coaching.v3`** adds exact Ready sources, a calculated preparation-comparison source and `UserWrittenPreparation`. Category validation prevents raw intentions being cited as calculated facts. Plan text is explicitly untrusted data, never prompt instructions.
- **Period `period-coaching.v3`** carries complete local preparation plus complete supplied comparison/coverage aggregates grouped by exact Account and activity inclusion. It supplies at most **two Ready examples per period side**, deterministically ordered, and exact preparation comparisons on supplied Trade examples. Available/supplied source counts remain explicit; local-only revisions cannot be cited. Current/previous prefixes and all finding/action bounds remain unchanged.
- Daily/period builders validate preparation scope and recalculate comparison mappings before freezing. Tampered chronology/source mappings, invalid categories, wrong IDs and oversized complete payloads are rejected rather than repaired or truncated. Adding Plan prose can make a complete prompt too large.
- Saved **v1/v2** reports and supported **v2 pending/imported responses** retain their original contract. Absent preparation means **not captured**, not No Plan. New v3 saved Review JSON includes the exact frozen evidence; later Plan/catalog edits or deletion do not rewrite it.
- Existing completed-review JSON fields and backup/portable-export mappings already retain the complete evidence/response. No database or export version changes are needed. Pending prompt files remain deliberately excluded from portable backup/export; pre-current-schema archive restore remains deferred.

The retained fake-HTTP Groq regression initially exceeded its unchanged 6,800-input estimate because preparation instructions were being added even to absent legacy evidence. After making those instructions conditional on captured preparation, the two-Trade fixture measures **340 instructions + 377 schema + 4,895 evidence + 83 framing + 570 margin + 512 reserve = 6,777**, output budget 1,000. This is a synthetic estimate, not a provider bill or new live API acceptance. Real captured preparation is still fully budgeted and never silently omitted.

### Presentation and guarded navigation

Current and saved Daily evidence have a collapsed preparation section; period current/prior evidence and saved **full local** preparation are secondary expandable sections. Saved full-local detail explicitly differs from supplied examples. Human-readable citations show frozen labels and actual Ready time. Exact source opening reuses Trading Plan History's read-only viewer, checks Plan/revision/key/time, and does not replace the working editor. Return to the originating Review retains date/Account and saved report. Plan → Daily uses the current recorded editor or viewed historical key, never malformed typed date text. Dirty/invalid editor fields remain retained through this related navigation; normal unrelated navigation/window-close guards remain in force. A missing live revision produces a safe unavailable message while its saved evidence stays readable.

### M21.9 verification chronology

Initial failures are retained in ignored `TestResults/M219`, not hidden by the final runs:

- First focused Application run: **235 passed / 1 failed** (empty legacy evidence was incorrectly given an extra preparation source). Preparation sources are now conditional on captured evidence; next Application run **246/246 passed**.
- First focused Infrastructure run: **186 passed / 11 failed**: four old schema-name expectations, six retained Groq budget regressions, one fixed-query-count expectation. Second run: **197 passed / 6 failed** after schema/query expectation correction; conditional instructions resolved the remaining measured budget overhead. Third budget/reader run **68/68 passed**.
- First real-SQLite navigation run **2/2 passed**; first compiled-WPF render run **2/2 passed**.
- Broad affected-suite run found two old `period-coaching.v2` expectations after successful v3 save/display. Expectations were updated to the intentional version change; dedicated old-v2 compatibility assertions remain.
- Corrected broad focused run: **1,097/1,097 passed**, zero failures/skips — Domain 90, Application 303, Infrastructure 237, Desktop 467. Evidence: `TestResults/M219/focused-corrected/`. This includes retained Review/Trading Plan guards and fake-HTTP workflows, not only new cases.
- Complete gate first failure: Calendar child `WindowControlsRetainModalAndDraftThenCloseInEveryState(Dark, Maximized)` failed a restored-panel-size assertion, expected **802.4**, actual **762.4**. Its phase log places the exception immediately after the second maximize/restore/layout step (4.314s into that action); line 262 rethrows the captured exception. It is **not a 120-second timeout** and the logs do not establish why the dimension changed. The child completed **86 passed / 1 failed** in **117.868s**, leaving **2.132s** to its unchanged deadline. This one failure propagated to **86 parent cases**; the exclusive partition finished **2 passed / 86 failed**. No Calendar code, assertion, deadline or scheduling was changed, and no passing rerun is substituted for this failure.
- Separate parallel failure: `BreakdownAccessibilityViewTests.OwnedFocusValidationAutomationAndRetainedStatesInBothThemes`, line 239, observed `DatePicker.IsDropDownOpen == false` after F4 on a malformed date field. Its child finished (not timed out); no Breakdown code is changed by M21.9. The available evidence does not prove a shared cause with Calendar or establish a product-versus-automation diagnosis. This remains unresolved; no assertions or timing allowances were changed.
- **Final complete gate: 4,776 passed / 87 failed / zero skipped, exit 1** (4,863 total, 20 new M21.9 cases). Domain **567/567**, Application **1,021/1,021**, Infrastructure **1,423/1,423**; Desktop **1,765 passed / 87 failed** across 88 exclusive and 1,764 parallel cases. The parallel Desktop partition was **1,763 passed / 1 failed**, 7m43s. All new M21.9 cases passed. No later complete run supersedes this failure. Evidence: `TestResults/M219/full-gate/release-20261018-190524-6e18bb1e72c84d83a3f737bbf0d81cfb/summary.json`, top-level TRX and child diagnostics. **Implementation is present; complete acceptance remains blocked**, regardless of focused success.
- Release build: **0 warnings/errors**. EF `has-pending-model-changes` through the **in-memory** design-time factory: no changes. Tracked/new-file whitespace and **182 local documentation file links** pass.

Commands (all synthetic fixtures; WPF tests with normal Windows permissions):

```powershell
dotnet build PersonalTradingJournal.sln -c Release --no-restore
$env:PTJ_PREPARATION_RENDER_DIRECTORY = Join-Path $PWD 'artifacts/m219-review-preparation'
dotnet test PersonalTradingJournal.sln -c Release --no-build --no-restore --filter 'FullyQualifiedName~TradingPlan|FullyQualifiedName~ReviewPreparation|FullyQualifiedName~DailyReview|FullyQualifiedName~PeriodReview|FullyQualifiedName~MainWindowViewModelTests' --logger trx --results-directory TestResults/M219/focused-corrected
$env:PTJ_PREPARATION_RENDER_DIRECTORY = $null
pwsh -File scripts/Test-Release.ps1 -ResultsDirectory TestResults/M219/full-gate
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure/PersonalTradingJournal.Infrastructure.csproj --context JournalDbContext --configuration Release --no-build
git diff --check
```

Automated renders: `artifacts/m219-review-preparation/`, Light/Dark at **980 content DIP/96 DPI** and **460 DIP/240 DPI**. States include missing preparation, retrospective Ready with a newer Draft, unknown Net and saved period citations. Normal/narrow examples were visually inspected: text wraps, evidence remains secondary, source action is keyboard-focusable and no horizontal content clipping was observed. These are compiled-WPF synthetic renders/automation, **not** human mouse/keyboard, screen-reader or live-app restart evidence.

The new preparation render child passed in the complete gate in **24.615s** (65.385s headroom to its 90s aggregate deadline). Journal's exclusive editor case passed in **10.634s**. Calendar's 2.132s aggregate margin is narrow and does not establish timing stability. All original assertions, deadlines, isolation and scheduling are retained. Final worktree: **37 modified + 10 new files**, all M21.9; branch/HEAD remain `develop` / `fbeaa7eead77d6931a41e5688e6d31c8f706b3b4`. No commit, push, merge or branch switch.

M21.10 remains pending: isolated actual-app restart and full human workflow, keyboard/Narrator/focus/scroll acceptance, and CI matching the eventual published changes. No matching CI can represent this uncommitted worktree. Earlier Calendar scheduling variability is not claimed fixed by a later passing run; its final observed timing is reported separately. No live provider calls or production journal access occurred.

Remaining isolated manual checklist (not executed as M21.10): inspect a day with missing, retrospective and entry-eligible preparation; open an old Review's exact Ready revision while invalid Draft fields are retained, then return; prepare/copy a synthetic v3 prompt and verify its supplied source boundaries without an automatic upload; reopen pending/saved evidence after restarting the isolated app and after a source becomes unavailable; check Tab order, focus return, Narrator announcements and long-content scrolling in both themes at high DPI. Use only an explicit isolated data root, never the normal journal. Resolve the documented full-gate blockers before claiming complete acceptance.

### Exact M21.9 changed files

Paths are repository-relative; includes the documentation and tests, compared with the actual clean starting worktree.

- `README.md`
- `docs/architecture.md`
- `docs/daily-review.md`
- `docs/period-reviews.md`
- `docs/trading-plan.md`
- `src/PersonalTradingJournal.Application/DailyReview/Coaching/CoachingEvidencePacket.cs`
- `src/PersonalTradingJournal.Application/DailyReview/Coaching/CoachingEvidencePacketBuilder.cs`
- `src/PersonalTradingJournal.Application/DailyReview/Coaching/CoachingResponse.cs`
- `src/PersonalTradingJournal.Application/DailyReview/Coaching/CoachingResponseValidator.cs`
- `src/PersonalTradingJournal.Application/DailyReview/Coaching/ManualReview.cs`
- `src/PersonalTradingJournal.Application/DailyReview/DailyReviewEvidence.cs`
- `src/PersonalTradingJournal.Application/PeriodReviews/Coaching/PeriodCoachingPacket.cs`
- `src/PersonalTradingJournal.Application/PeriodReviews/Coaching/PeriodCoachingProjection.cs`
- `src/PersonalTradingJournal.Application/PeriodReviews/Coaching/PeriodCoachingResponse.cs`
- `src/PersonalTradingJournal.Application/PeriodReviews/PeriodReviewEvidence.cs`
- `src/PersonalTradingJournal.Application/TradingPlans/ReviewPlanEvidence.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/DailyReview/DailyReviewViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/DailyReview/ManualReviewViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/DailyReview/ReviewPresentation.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/MainWindowViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/PeriodReviews/PeriodPresentation.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/PeriodReviews/PeriodReviewViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/ReviewPreparation.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/TradingPlanWorkspaceViewModel.Review.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/TradingPlanWorkspaceViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/DailyReview/DailyReviewView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/PeriodReviews/PeriodReviewView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/ReviewPreparationView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/ReviewPreparationView.xaml.cs`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/TradingPlanWorkspaceView.xaml`
- `src/PersonalTradingJournal.Infrastructure/DailyReview/Coaching/GroqCoachingProvider.cs`
- `src/PersonalTradingJournal.Infrastructure/DailyReview/Coaching/ManualReviewService.cs`
- `src/PersonalTradingJournal.Infrastructure/DailyReview/Coaching/OpenAiCoachingContract.cs`
- `src/PersonalTradingJournal.Infrastructure/DailyReview/Coaching/PeriodCoachingWireContract.cs`
- `src/PersonalTradingJournal.Infrastructure/DailyReview/DailyReviewEvidenceReader.cs`
- `src/PersonalTradingJournal.Infrastructure/PeriodReviews/PeriodReviewEvidenceReader.cs`
- `src/PersonalTradingJournal.Infrastructure/TradingPlans/ReviewPlanEvidenceReader.cs`
- `tests/PersonalTradingJournal.Application.Tests/DailyReview/ReviewPreparationContractTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Navigation/MainWindowViewModelTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/PeriodReviews/PeriodSqliteTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/ReviewPreparationNavigationTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/ReviewPreparationViewTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/DailyReview/DailyReviewEvidenceReaderTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/DailyReview/GroqInputBudgetTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/DailyReview/ManualReviewTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/DailyReview/PeriodCoachingProviderTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/DailyReview/ReviewPreparationReaderTests.cs`


## M21.8 — History, Copy Forward and navigation recovery

Actual baseline: **clean `develop`, `be2fb3c6a5554ef34c367e756dbff92c1b623b1f`**. M21.7 was already committed, superseding the attachment's dirty-tree report. Existing behavior is preserved. This slice changes no schema, monetary semantics, Trade/Journal data, Rules/Playbook catalog, provider behavior or Review evidence. M21.9 and M21.10 are not implemented here.

### History and exact Ready viewing

**History** sits beside **Session Brief**. Its Account, inclusive From/To **New York dates**, and current Draft/Ready status are independent of the retained editor. Draft includes Draft-after-Ready. Search is explicit. Invalid/reversed dates stay typed, with useful errors and previous results unchanged; a visible unapplied-filter notice distinguishes pending values from the displayed search identity. Rows show date, Account/currency, session, Instruments, lifecycle, latest Ready number and saved/Ready time explicitly in **UTC**.

Plan history is date-descending within one exact Account; Ready history is revision-descending. Both request 21 and display at most 20, with cursors taken from the last displayed row, so an exactly full final page does not expose a spurious empty next page. Account selection includes inactive Accounts. **Find saved Accounts** discovers missing identities within the typed date range, 20 at a time, without loading every Plan; **More saved Accounts** continues that bounded discovery. Discovery orders by exact Account ID, with an offset safety bound of 100,000. Each discovery query is internally consistent; concurrent additions/deletions between discovery pages can change that population, so a fresh discovery restarts at page one. Already discovered/selected identities remain available in this retained workspace. An unavailable Account uses its latest Ready name/currency in the range, not a same-name replacement. A Draft-only deleted Account has no saved name and is explicitly labeled unavailable rather than inventing one. Account identity is available to accessibility help text.

History summaries are a bounded four-query batch in one deferred SQLite snapshot. Ready rows use frozen Account/Instrument labels; Draft rows use current labels (or an explicit unavailable marker). No query per Plan, day or revision is added. Stored payload validation remains strict; malformed evidence is not repaired.

**Revisions** and **View revision** do not replace the editor. The exact persisted Ready revision is read again, identified by original date, saved Account/currency/session, revision and actual Ready UTC timestamp. The existing preparation/scenario/risk/readiness components display its immutable content, references and confirmations read-only. Current names, currency, Rule criteria and Playbook text do not overwrite historical values. The view issues one guarded focus request to the new historical heading; later user input or leaving the page cancels that focus request. Page scrolling remains available for long preparation.

### Derivation, copy and conflict recovery

**Start Draft from this revision** invokes the existing `StartDraft` operation with the viewed revision and loaded owner token. It edits the original Plan identity, clears confirmations, preserves latest Ready/history and never silently substitutes the latest revision as source. An existing Draft or concurrent owner change produces a conflict with **Open existing Plan**; no Draft is overwritten.

**Copy Forward** identifies the exact persisted Ready source and requires an explicitly typed, valid, different NY date in the **same Account**. It invokes the existing atomic `CopyForward` operation, not a Desktop reimplementation of reset rules. Reusable session, focus, intent, Instrument identities, scenarios, planned limits and Rule links transfer. Bias, market thesis, levels, news, stand-aside text and every readiness confirmation reset. The target receives a new identity and remains Draft, without a Ready timestamp or inherited Ready status. Original revisions are unchanged. Current references are revalidated, and changed monetary Account currency still requires explicit review without FX conversion.

The shared Save/Discard/Cancel guard resolves pending editor input before a transfer. The UI explicitly states that the selected **persisted Ready revision**, not the pending Draft, is the transfer source. Save must succeed; invalid input/cancellation/conflict/storage failure retains the original editor and source/target selections. Discard consent is staged: a failed transfer does not destroy fields. Duplicate submissions and scope changes are blocked while the operation owns a write. Target uniqueness and optimistic conflicts are rechecked by the store; simultaneous target creation never overwrites another Plan. **Open existing Plan** explicitly reloads even an already selected identity, through the same guard, so a concurrently created Draft cannot remain hidden behind a stale Ready editor. A veto when opening a different history Plan also retains the Session Brief's raw malformed date instead of normalizing it through ordinary toolbar-selection restoration. A typed success without matching identity/content/revision readback is not reported as saved. A verified commit wins over late cancellation. Success opens the returned Draft, refreshes the affected history using its applied query, and reports the committed result after supporting reference loads. If those loads fail after a verified commit, the message explicitly says the Draft committed but supporting lists need Reload.

### Navigation and asynchronous ownership

Session Brief/History switches retain the same editor, selected Instrument, raw invalid fields and save token; History filters and revision selection remain separate. Opening another editable Plan uses the existing shared guard. Returning from a linked Playbook/Rule retains the selected tab and editor; no global navigation preferences are added. MainWindow closing protects a dirty hidden Session Brief even while History is showing, and a later owner-close veto does not consume Discard consent.

History/revision reads have independent cancellation and request epochs. Late cancellation-insensitive results cannot replace newer searches/selections, and close/disposal drains the existing tracked read owners. Writes capture source/target/token, freeze disruptive selection, and publish only the verified result. No retries, automatic copying or automatic save is introduced. Error messages do not expose database paths or exception/source contents.

### Verification and limits

Synthetic tests cover 20/21/40-row boundaries, inclusive dates, exact scope, Draft/Ready filters, revision paging, raw invalid filters, editor independence, frozen content after catalog changes, historical derivation and existing-Draft conflicts, copy/reset/readback, concurrent target creation, duplicate submission, cancellation, stale history/detail reads, deleted/same-name Account discovery, service-restart reconstruction and hidden-editor closing. A SQLite interceptor changes the current Account during summary materialization and verifies the original snapshot is retained across all four SELECTs. Existing persistence, portability and reference-policy cases remain in the focused/full gates.

Initial evidence is retained: `artifacts/m21-8/initial-focused/history.trx` reported **15 passed / 2 failed** due to new-test comparisons (immutable-array record equality and matching “saved” inside a failure sentence). Those assertions were corrected to compare exact content/identity/timestamp and actual success semantics, without changing production policy. `focused-render/history.trx` then reported **17 passed / 2 failed**: the compiled page exposed a real new-flow defect where post-commit reference loading replaced the Copy Forward success text. The coordinator now publishes the verified success after those supporting reads; the assertion remains. No existing assertions/deadlines/isolation settings were relaxed.

Render inspection also found that transfer conflicts/progress could be outside the viewport when the user was scrolled to the historical transfer controls. The same action status and a Cancel transfer button now appear beside those controls. Only the original live region announces changes, avoiding duplicate announcements. Long Account names wrap in rows and the applied-search description; standard selectors retain their compact width. Read-only component editing controls remain disabled rather than introducing a second historical rendering model.

Final focused verification passed **475 cases**: Desktop **328**, Infrastructure **35**, Application **35**, Domain **77**, with zero failures/skips. Desktop results are in `artifacts/m21-8/acceptance-focused/desktop.trx`; the other results are in the corresponding `infrastructure`, `application` and `domain` directories. The final Release build completed with **zero warnings/errors**. The complete-gate result is recorded separately below; focused success is not a substitute for it.

Final complete `scripts/Test-Release.ps1` result: **4,843 passed, zero failed/skipped, exit 0**, in `artifacts/m21-8/final-release-gate/release-20261018-171554-3849d0048d784c1f883c403a44770434`. This executes the complete set through the repository's existing exclusive-WPF and parallel partitions, with no deadline/assertion/isolation changes. The earlier complete run in `artifacts/m21-8/release-gate/release-20261018-170343-ca0ca791c4d14c4183b6a873e8cb0843` passed 4,840 cases before the final three navigation/read-error regression cases and adjacent transfer-status correction; it is not substituted for the final run.

| Final project / partition | Passed | Failed / skipped |
| --- | ---: | ---: |
| Domain | 567 | 0 / 0 |
| Application | 1,011 | 0 / 0 |
| Infrastructure | 1,417 | 0 / 0 |
| Desktop exclusive WPF | 88 | 0 / 0 |
| Desktop parallel | 1,760 | 0 / 0 |
| **Total** | **4,843** | **0 / 0** |

The baseline was 4,819: **24 new cases**, with existing guard cases extended. Final Calendar native child: **69.160s**, **50.840s headroom** against its unchanged 120-second deadline. New History child: **15.417s**, **74.583s headroom** against its 90-second aggregate deadline, retaining the 30-second case watchdog. The final parallel Desktop project took **5m37s**. Historical timing variability remains a risk, not a claimed harness fix. EF reports **no model changes since the last migration**. `git diff --check`, all six new files' trailing-whitespace checks and **164 local Markdown file links** pass. There are **12 modified + 6 new files**, uncommitted, and HEAD remains `be2fb3c6a5554ef34c367e756dbff92c1b623b1f` on `develop`.

The bound WPF checks generated **28 renders** under `artifacts/m21-8/acceptance-renders`: seven states (multi-lifecycle History, invalid filter, empty search, exact Ready, invalid copy date, target conflict, copied Draft) × Light/Dark × 980-DIP/96-DPI and 460-DIP/240-DPI content widths. Representative final renders from every state and both size/theme variants were inspected. Filters/actions wrap, long row names remain readable, error/conflict text stays beside its input/action, frozen Ready labels differ correctly from renamed current labels, and page scrolling exposes the remaining content without horizontal clipping. Compact selectors can abbreviate long labels; the complete applied identity and row labels wrap below. Examples: [Light History](../artifacts/m21-8/acceptance-renders/history-Light-980-96-history.png), [Dark Ready](../artifacts/m21-8/acceptance-renders/history-Dark-980-96-ready.png), [narrow high-DPI conflict](../artifacts/m21-8/acceptance-renders/history-Dark-460-240-conflict.png), [verified copied Draft](../artifacts/m21-8/acceptance-renders/history-Dark-980-96-copied-draft.png). These artifacts are local ignored verification output, not committed assets.

Automated native-window focus/close tests are not human keyboard or Narrator acceptance. The computer-use skill's supported helper failed initialization with **“failed to write kernel assets: The system cannot find the path specified. (os error 3)”**, including a fresh-session recovery attempt. No production app was opened to work around that failure. **Live isolated application restart, human mouse/keyboard, screen-reader announcements and matching CI remain pending.** SQLite service/ViewModel recreation is evidence of persisted reads, not a claimed full-process/live restart. All changes remain uncommitted, so no published commit/workflow matches this exact change set. Earlier Calendar variability is not claimed fixed by a passing gate.

Pending isolated manual checklist: create/save/Ready a synthetic Plan; revise it, then browse both Ready revisions; retain invalid Draft input while switching tabs and visiting a linked source; cancel a context change; copy the older Ready to another date and inspect reset fields; try an existing target and open it through the guard; close/reopen the isolated app and verify history, immutable labels and copied Draft; check Tab/focus and Narrator status/read-only announcements. Use only an explicitly isolated `--isolated-data-root`, never the normal journal.

No backup/restore redesign is introduced. Complete backup and JSON v10 already include Drafts and Ready revisions; existing portability tests remain applicable. **Restoring older-schema archives remains deferred**, unchanged from M21.3. Historical Account discovery has no authority to recreate an Account, and unavailable/inactive references can still block new preparation writes under the existing policy. Full M21 acceptance is not claimed.

Commands used (synthetic fixtures only; WPF tests run in the normal Windows environment, with no build during a test run):

```powershell
dotnet build PersonalTradingJournal.sln -c Release --no-restore
dotnet test tests/PersonalTradingJournal.Domain.Tests/PersonalTradingJournal.Domain.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~TradingPlans
dotnet test tests/PersonalTradingJournal.Application.Tests/PersonalTradingJournal.Application.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~TradingPlan
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests/PersonalTradingJournal.Infrastructure.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~TradingPlan
$env:PTJ_PLAN_HISTORY_RENDER_DIRECTORY = Join-Path (Get-Location) 'artifacts/m21-8/acceptance-renders'
dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~TradingPlans|FullyQualifiedName~MainWindowViewModelTests'
$env:PTJ_PLAN_HISTORY_RENDER_DIRECTORY = $null
pwsh -File scripts/Test-Release.ps1 -ResultsDirectory artifacts/m21-8/final-release-gate
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure/PersonalTradingJournal.Infrastructure.csproj --context JournalDbContext --configuration Release --no-build
git diff --check
```

### M21.8 changed files

Relative to the inspected clean baseline:

- `README.md`
- `docs/architecture.md`
- `docs/trading-plan.md`
- `src/PersonalTradingJournal.Application/TradingPlans/ITradingPlanStore.cs`
- `src/PersonalTradingJournal.Application/TradingPlans/TradingPlanContracts.cs`
- `src/PersonalTradingJournal.Infrastructure/TradingPlans/TradingPlanStore.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/TradingPlanWorkspaceViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/TradingPlanWorkspaceViewModel.History.cs` (new)
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/TradingPlanWorkspaceView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/TradingPlanHistoryView.xaml` (new)
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/TradingPlanHistoryView.xaml.cs` (new)
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/TradingPlanHistoryTests.cs` (new)
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/TradingPlanHistoryViewTests.cs` (new)
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/TradingPlanWorkspaceTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/TradingPlanWorkspaceViewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/SessionPreparationTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/RiskReadinessTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/TradingPlanHistoryProjectionTests.cs` (new)

## M21.7 — Session Brief Desktop workspace

Actual baseline: **clean `develop`, `f41deb0880943087a627b2c4277d3e0d0574c3e6`**. M21.6 was already committed, superseding the attachment's earlier dirty-tree report. No applicable `AGENTS.md` was found in the repository or ancestors. The implementation uses synthetic SQLite journals and test-only WPF owners; no production journal, provider call, commit, push, merge or later milestone work.

### Composition and context

The shell's existing centered **Trading Plan** title, teal treatment, underline, navigation and theme toggle are reused unchanged. `TradingPlanWorkspaceView` adds a wrapping context/action toolbar, one page ScrollViewer, the existing Focus/01/02/03 components, and a compact state footer. All sections bind the **identical** `SessionPreparationViewModel`; there are no per-section save tokens, second Draft, duplicated readiness calculation or per-field persistence. Internal two-column preparation/readiness and side-by-side scenario layouts stack at their existing DIP breakpoints. Long fields retain bounded internal text scrolling, without nested page scroll regions. The mockup supplies hierarchy, not sample defaults or new Domain requirements.

Today uses the existing New York time conversion, not machine-local date. Previous/Next move by calendar day. A custom DatePicker text surface preserves malformed input on blur; Enter/**Open date** applies a valid typed destination, and F4 opens the calendar. Invalid or unapplied typed dates cannot silently retarget Save. The loaded date/Account remain explicit. An Account/date switch is committed only after leave consent; session changes edit content within the same unique Account/date Plan. No Account is selected implicitly and there is no All accounts plan. Empty/no-active-Account, inactive, failed-load, Draft, Ready and Draft-after-Ready states are distinct.

Opening an empty context performs **zero writes**. Explicit **Create Draft** persists a valid empty Draft before the components open; this is not automatic saving. An already selected Account that later disappears remains an exact, unavailable option during this retained workspace session, never a same-name replacement. Ready uses its saved Account name/currency and frozen source text. General history/discovery after restarting with a deleted Account remains M21.8 work, not an implemented History screen.

### Save, Ready and failure behavior

Save submits all pending valid fields and explicit confirmations through the existing transaction. Malformed preparation/scenario/risk text blocks the entire save and focuses the first affected section; incomplete valid Draft content is still allowed. Entered values and selected instrument survive failed/cancelled/conflicting writes. Tokens/baselines update only after matching committed content and confirmations return. A late cancellation cannot turn a verified committed success into a cancelled result. Returned success without a matching readback is an error, not Saved.

Mark as Ready reuses cross-section readiness and requires a **saved**, fully confirmed Draft and verified sources; unsaved input requires Save first. Domain/reference/concurrency conditions are rechecked by the store. Repeated submissions cannot publish multiple transitions. Verified success replaces the editor with the persisted, read-only Ready revision; frozen currency, labels and confirmations are retained. **Start Draft / Edit** uses the exact Ready revision, leaves it preserved and clears confirmations through the existing lifecycle. A changed current Account currency requires an explicit currency-review confirmation before deriving monetary limits, without conversion. Existing fresh-reference policies can block derivation from changed/unavailable Ready sources; they are not bypassed or silently refreshed. Explicit reference refresh and source relinking remain visible, and clear the affected confirmations according to existing policy.

Reload is guarded. On conflict the user can keep entered input, or explicitly discard only unsaved changes and load the current stored Plan. There is no automatic merge or silent overwrite. UI messages are sanitized; source text, identifiers and storage exception details are not logged.

### Navigation, closing and asynchronous ownership

One Save/Discard/Cancel decision protects date/Account changes and normal page departure. Save continues only after verified persistence; invalid fields/failures keep the original context. Discard is **staged consent**, not immediate destruction, so a later destination or owner-close veto retains every field. Persisted Drafts/Ready revisions are never deleted by Discard. Reentrant navigation/decisions are rejected.

Explicit Playbook and Rule links open the exact current catalog identity through the existing shell/destination guards while retaining the same editor and invalid raw input. Returning to Trading Plan does not reload over it. Closing or leaving for an unrelated destination still guards this hidden pending session. A link is preparation context, not adherence evidence.

MainWindow uses a two-attempt close only when Trading Plan needs asynchronous preparation: the first is cancelled while Save/Discard/Cancel or load draining completes; the next runs the existing owned-window close coordinator and all guards. **Only the final uncancelled close consumes Discard consent.** A late Closing subscriber veto keeps the editor intact. Explicit Save may persist preparation even if another guard subsequently keeps the application open. Busy writes require waiting/cancellation before leaving; normal accepted closing cannot dispose services beneath those operations.

Loads run off the dispatcher with captured exact key and monotonically increasing request ownership. Obsolete loads are cancelled/superseded, and cancellation-insensitive late results cannot replace a newer selection. All outstanding load owners are drained before accepted closing. Mutation controls are frozen while writes run; no completed result can attach to a later scope. No retries or background saves are added.

### Verification and acceptance evidence

New tests use real SQLite-backed workspace sessions and a counting/fault-injection store decorator, not provider calls. They cover creation/empty states, New York defaults, invalid dates, all-section saves and exact decimals, readback rejection, Ready prerequisites/success/failure, Start Draft, repeat submission/cancellation, Save/Discard/Cancel, concurrent changes, stale loads, related Rule navigation, deleted/same-name Account identity and read-only reconstruction in a newly created ViewModel reading SQLite. This is not a live full-application restart. The compiled page tests exercise shared bindings, responsive actions, accessible names/errors, preparation/scenario/risk/date focus, and **real MainWindow Closing** with a later veto followed by an accepted Save/close.

Initial new-test corrections: one message assertion incorrectly matched the word “saved” inside a failure sentence; it now tests the exact success message. A detached-render assertion initially required `IsVisible`, which is false without a presentation source; it now checks noncollapsed ancestors and measured layout, while native-window focus/visibility remain separately asserted. Initial test fixture accessibility/analyzer build errors were corrected. No existing assertion, deadline or isolation policy was relaxed. Four layout cases share one supervised child run instead of redundantly running the entire child once per parent row; the child still executes all four cases with its unchanged 90-second aggregate/30-second case watchdogs.

The first broader focused run under the restricted desktop reported **303 passed / 2 failed**: both failures were existing `DailyReviewAcceptanceCompiledShellKeepsCapturedEvidenceOrCancelsOnSourceNotification` cases, before their synthetic HTTP handler was reached, with the sanitized missing/unreadable credential outcome. All M21.7 cases passed. This is not recorded as a Trading Plan/provider product fix; normal-Windows verification is reported separately below. Earlier M21.6 Calendar monitor-handle/aggregate-timeout and Breakdown automation variability remain unresolved historical risks unless measured otherwise.

Initial TRX evidence remains in `artifacts/m21-7/initial-focused`, `focused-render` and `final-focused`. Normal-Windows Desktop verification passed **306/306** in `normal-focused`, then **306/306** against the final toolbar corrections in `corrected-focused`; both have zero failures/skips. Focused Domain **77**, Application **35** and Infrastructure **33** passed, for **451 focused cases**. The final Release build passed with **0 warnings/errors** (31.66s); EF reported no model changes. Tracked and eight new files pass whitespace checks; 158 local documentation file links resolve. No production data or live provider was used.

The first complete normal-Windows gate passed **4,819/4,819**, zero failed/skipped, exit 0: Domain 567, Application 1,011, Infrastructure 1,415, Desktop 1,826 (88 exclusive + 1,738 parallel). Calendar native was **63.433s / 56.567s headroom**; Session Brief child **33.718s / 56.282s headroom**. Evidence: `artifacts/m21-7/release-gate/release-20261018-071531-faf2166102c147fcaad3a9847882b80f`.

Final inspection found two toolbar presentation defects: Ready/Edit remained enabled for a valid but unapplied typed date (execution already refused it), and missing-editor binding fallback exposed a disabled Start Draft button in empty/loading/error states. Command eligibility now matches Save's pending-date guard; the transition button appears only with Ready content. VM tests verify zero writes and restored eligibility, and compiled page tests check its actual presentation across lifecycle states. The first complete gate used the earlier binaries; final results below are from the rebuilt corrections, not a reused old run.

**Final complete gate: 4,819 passed, zero failures/skips, exit 0.** Evidence: `artifacts/m21-7/final-release-gate/release-20261018-072527-34dd00983f844edfb5c20497be0ccb2f/summary.json`, top-level TRX and supervised child logs. No assertions, deadlines, isolation or concurrency settings were weakened.

| Project / partition | Passed | Failed / skipped |
| --- | ---: | ---: |
| Domain | 567 | 0 / 0 |
| Application | 1,011 | 0 / 0 |
| Infrastructure | 1,415 | 0 / 0 |
| Desktop exclusive WPF | 88 | 0 / 0 |
| Desktop complementary parallel | 1,738 | 0 / 0 |

Final Calendar native **59.581s**, **60.419s** headroom against 120 seconds. Session Brief native **32.877s**, **57.123s** headroom against 90 seconds. Existing Calendar/Breakdown failures did not recur in either complete run; that is not proof their historical variability is resolved. The final source remains uncommitted on the same HEAD, so no matching CI acceptance is claimed.

Automated renders are under ignored `artifacts/m21-7/final-renders`: `workspace-{Light|Dark}-{980-96|1280-144|1600-192|460-240}-{empty|loading|draft|scenarios|invalid|ready|draft-after-ready|error}.png`, plus a shared-shell frame. These represent **usable content DIP widths** at 100/150/200/250%, not a claim that physical window width equals content width. 980 DIP approximates a 1280-DIP shell after navigation/margins; 1280/1600 are wider content, and 460 is a deliberate narrow stress case. Normal native focus/close tests use actual WPF windows. Rendered themes, field errors and wrapping are automated evidence, not human mouse/keyboard or Narrator acceptance.

Inspection covered both themes, the shared shell, empty/loading/error, Draft/Ready/Draft-after-Ready, side-by-side long scenarios and narrow stacked validation. The corrected empty/loading/error frames no longer show Start Draft/Edit. Actions wrap without horizontal clipping; scenarios stack, and long text has bounded field scrolling within the single page scroll. Compared with the concept, the reused editors are deliberately taller and retain existing explicit labels/reference controls rather than removing authoritative input fields. Actual display scaling, user-driven date-picker/leave-dialog interaction and screen-reader announcements still need human verification.

Commands:

```powershell
dotnet build PersonalTradingJournal.sln -c Release --no-restore
dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~TradingPlans|FullyQualifiedName~MainWindowViewModelTests'
dotnet test tests/PersonalTradingJournal.Application.Tests/PersonalTradingJournal.Application.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~TradingPlan
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests/PersonalTradingJournal.Infrastructure.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~TradingPlan
dotnet test tests/PersonalTradingJournal.Domain.Tests/PersonalTradingJournal.Domain.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~TradingPlans
pwsh -File scripts/Test-Release.ps1 -ResultsDirectory artifacts/m21-7/final-release-gate
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure/PersonalTradingJournal.Infrastructure.csproj --context JournalDbContext --configuration Release --no-build
git diff --check
```

### M21.7-specific changed files

Relative to the inspected clean baseline, **12 modified + 8 new = 20 files**. All earlier committed changes are preserved.

Modified:

- `README.md`
- `docs/architecture.md`
- `docs/trading-plan.md`
- `src/PersonalTradingJournal.Desktop/App.xaml`
- `src/PersonalTradingJournal.Desktop/App.xaml.cs`
- `src/PersonalTradingJournal.Desktop/MainWindow.xaml.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/MainWindowViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/SessionPreparationViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/SessionPreparationSection.xaml`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/ScenariosSection.xaml`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/RiskReadinessSection.xaml`
- `tests/PersonalTradingJournal.Desktop.Tests/Navigation/MainWindowViewModelTests.cs`

New:

- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/SessionPreparationViewModel.Lifecycle.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/TradingPlanWorkspaceViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/TradingPlanWorkspaceView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/TradingPlanWorkspaceView.xaml.cs`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/TradingPlanLeaveDialog.xaml`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/TradingPlanLeaveDialog.xaml.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/TradingPlanWorkspaceTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/TradingPlanWorkspaceViewTests.cs`

M21.8 owns History, Copy Forward and extended navigation recovery; M21.9 owns contextual Daily Review/AI evidence; M21.10 remains full acceptance. No dead History/Review buttons or internal milestone labels are exposed. Existing current-schema backup/export coverage and the deferred **older-schema archive restore** policy are unchanged. Human keyboard, Narrator, actual display scaling/monitor interaction and CI matching these uncommitted changes remain open. This is M21.7 only, not complete M21 acceptance.

## M21.6 — risk limits, Trading Rules and readiness

Actual baseline: **clean `develop`, `4217d973504fced25706bc433dd15edbfa381d97`** (`Add Trading Plan scenarios and Playbook links`). The attachment's earlier HEAD/uncommitted M21.5 report was superseded by the inspected worktree. All M21.1–M21.5 behavior is retained; the file inventory below is specifically M21.6. No applicable repository/ancestor `AGENTS.md` was found. Only synthetic data and compiled WPF component tests were used; no production journal, provider, commit, push, merge or M21.7 implementation.

### One Draft, limits and currency

`SessionPreparationSection`, `ScenariosSection` and **`RiskReadinessSection`** bind the identical `SessionPreparationViewModel`. The new partials add transient adapters, not a second aggregate, independent save token or persistence path. One builder submits all preparation, scenarios, Rules, limits and explicitly selected confirmations. Invalid raw input anywhere blocks the whole write rather than persisting an older valid number. Incomplete **valid** content still saves as Draft. Save/cancel/conflict/source/storage failures retain entered fields, links and staged confirmations. A prior verified-save message is replaced when new unsaved changes occur; success is shown only after returned database content and checkmarks match the submission.

All four planned limits are **optional**, following `TradingPlanRiskLimits` unchanged:

| Field | Valid values | Meaning |
| --- | --- | --- |
| Risk per trade / daily loss limit | Exact `decimal` >= 0, or null | Planned amount in this exact Account's currency; blank means unset, zero permits none |
| Maximum number of trades | `Int32` >= 0, or null | Whole count; zero is explicit, not missing |
| Stop after consecutive losses | `Int32` >= 1, or null | Whole consecutive-loss stop count; zero is invalid |

Monetary parsing reuses the established culture-aware, no-grouping, exact-decimal parser. Excess precision, overflow, malformed/negative text and fractional/out-of-range counts remain visible with inline errors, red invalid state, accessible HelpText/ItemStatus and coordinated first-invalid-field focus. No rounding, truncation, old-value substitution, mockup defaults, broker enforcement, loss monitoring or new P&L calculation is introduced. A different numeric spelling of the same value may remain raw-dirty but does not clear unrelated confirmations.

An editable Draft initially labels currency **unverified** until explicit Check references or a verified Save returns Account context. The new `ITradingPlanReferenceReader` uses one deferred SQLite read transaction to check the exact Plan/version, stored baseline membership, current Account and selected sources. This is an as-of check, not a reservation or permission to ignore later changes: every write still resolves sources under the existing writer transaction. A changed Account is shown separately from the last verified currency; amounts are not silently relabeled or converted. Explicit RefreshReferences with monetary values requires the reviewed current currency, rechecks it at commit and resets all confirmations. Because the existing baseline contains hashes rather than old Draft labels, an Account metadata/hash change is conservatively reviewed even when currency ultimately matches. StartDraft/CopyForward also reject cross-currency monetary derivation without explicit reviewed currency. A missing Account cannot be acknowledged. Ready/historical displays always use the saved currency/limits, regardless of current metadata.

### Existing Rule links and bounded reads

Only active **global** Rules from the existing catalog are eligible for new links; at most the established **50** links, one per stable Rule ID. Choices are read in existing deterministic order through pages of **100**, with a **1,000-choice** safety limit and a consistent catalog version/count check. An oversized/changing/incomplete catalog is rejected without a partial choice list or automatic retry. No unbounded query or new Rules catalog is added. Exact current/selected definitions are fetched only for distinct linked IDs, at most 50; historical revision reads use one-row bounded queries.

Selecting a candidate is not a link. Explicit **Link selected revision** rereads and checks that candidate's exact revision/active/global status. Duplicate exact links are no-ops; a newer revision replaces a prior link only after explicit selection. Unlink is local Draft editing until Save. Rule content and availability are readable; changed/archived/unavailable Rules keep selected context and require explicit relink/refresh/reconfirmation, not a silent source update. Ready uses frozen Rule definitions and displays live availability separately. No Rule/assessment writes or automatic checks occur. A planning link is **not evidence of compliance**.

`RuleNavigation` is an exact-ID asynchronous host request returning accepted/vetoed. Open current Rule is disabled without a handler or available live source; cancellation, veto and failure retain all preparation including invalid raw input. M21.7 must route this callback through existing shell/Rules unsaved-work guards. Reading saved historical criteria is distinct from opening a current catalog definition.

### Explicit confirmations and pure cross-section readiness

The five existing confirmations are manual: **Context and levels; Scenarios or Stand Aside; Risk limits; News calendar; Mindset and focus**. Filling fields never checks a box. Their count/progress and remaining requirements are visible. The existing `TradingPlanChecklistPolicy.AfterEdit` invalidates only affected flags for meaningful session, Focus, instrument/context/level, scenario/Setup, Stand Aside, News or limit/Rule edits. Malformed raw edits also invalidate affected checks immediately; correcting text does not automatically re-confirm it. Other confirmations remain intact. Risk confirmation with monetary limits requires verified currency. Stand-aside-only plans use existing requirements without invented scenarios or N/A states.

The new optional `ExplicitConfirmations` argument on SaveDraft is **separate from `Content.Confirmed`**. The store applies content invalidation first and the explicit flags second in the same transaction, with one commit/readback; cancellation or a failed write rolls back both. Legacy SaveDraft calls without the argument still ignore incoming content checkmarks. `ExpectedAccountCurrency` is an optional reviewed reference/concurrency assertion, not a currency conversion instruction. No schema or stored JSON change is necessary.

Readiness getters have no database calls or mutations. They combine all raw-field errors with the **existing Domain Ready validator**, then reference, busy, failed-save, conflict and dirty-state blockers in stable order. Incomplete content is distinct from malformed input. Saved fields/checks alone cannot hide a newer invalid editor value or unverified references. A complete/saved indication is explicitly as-of verified sources; the eventual Mark Ready operation must recheck source state/concurrency. The component has **no Mark Ready action**, full toolbar or second readiness engine. Ready controls/commands are genuinely read-only and show saved flags. Shared first-invalid identity lets only the owning section invoke the existing guarded `PageFocus`; a status update does not steal focus.

### Presentation and remaining integration

At >= 800 DIP usable section width, Planned Limits/Linked Rules occupy the left column and checklist/progress/readiness the right. Narrow widths stack in logical order. Shared Light/Dark resources, wrapping labels/Rule headers, opaque interaction backgrounds, native checkbox/progress automation, textual errors and visible focus are used. The component has no outer ScrollViewer; the future host supplies page scrolling. Long exact decimals remain horizontally scrollable rather than rounded. Source details are optional expansion; availability/blocking messages remain visible.

M21.7 still owns the complete Session Brief composition/toolbar, Start Draft/Ready actions and reviewed-currency workflow, scope selection, service configuration/loading, navigation/close/unsaved-work guards and **cancel-and-await** lifecycle disposal. Configure `ITradingPlanReferenceReader` and the existing `ITradingRuleReader`/`ITradingRuleStore` explicitly on the shared editing session. Existing fresh-source policies may block deriving a new Draft from unavailable or changed historical references; this slice does not bypass those safeguards. No history screens, Review/AI integration or global lifecycle changes are added. There is no migration or portable-format change; existing database backup/export still carries the unchanged Draft/Ready content.

### M21.6 verification

Focused final run: **217 passed**, zero failed/skipped — Desktop **72**, Application **35**, Infrastructure **33**, Domain **77**. This adds **46 cases** (33 Desktop, 8 Application, 5 Infrastructure). Coverage includes exact/null/zero/cultured amounts and integer bounds, invalid retention/correction, targeted checks, pure readiness, one shared Draft, duplicate/cancelled/throwing Save, stale returned readback rejection, bounded Rule pages/cancellation/version changes, stale Rule selection/50-link limit, read-only frozen snapshots, currency review, SQLite restart and injected rollback before commit.

An initial new WPF check exposed the ProgressBar default TwoWay binding to a read-only property; it is now explicitly OneWay and the same behavioral assertions pass. Render capture now flushes pending dispatcher bindings/layout before asserting visible count/value; the inspected partial state shows **2 of 5** and **0.00**, not a stale frame. The final review also added an unexpected-Save-exception regression: like a typed storage failure, it must block previously complete readiness until a subsequent verified Save, retaining checkmarks/content. No assertions, deadlines or scheduling are weakened.

Commands (from repository root, Release build completed before tests):

```powershell
dotnet build PersonalTradingJournal.sln -c Release --no-restore
dotnet test tests/PersonalTradingJournal.Domain.Tests/PersonalTradingJournal.Domain.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~TradingPlans
dotnet test tests/PersonalTradingJournal.Application.Tests/PersonalTradingJournal.Application.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~TradingPlan
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests/PersonalTradingJournal.Infrastructure.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~TradingPlan
$env:PTJ_PLAN_RENDER_DIRECTORY = Join-Path $PWD 'artifacts/m21-6/renders'
dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~TradingPlans
Remove-Item Env:PTJ_PLAN_RENDER_DIRECTORY
pwsh -File scripts/Test-Release.ps1 -ResultsDirectory artifacts/m21-6/final-gate
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure/PersonalTradingJournal.Infrastructure.csproj --context JournalDbContext --configuration Release --no-build
git diff --check
```

Focused TRX: ignored `artifacts/m21-6/focused` (Application/Infrastructure/Domain) and `artifacts/m21-6/final-focused/desktop.trx` (final Desktop). Final solution build: **0 warnings/errors**, **30.66s**; final focused Desktop **72/72**, **21s**. EF: no pending model changes. Tracked plus all 11 new-file whitespace checks pass; **156** local documentation link targets resolve. Complete-gate results below are separate from focused/component acceptance.

The initial complete run (`release-20261017-201618-c10c2a48b5ba422cbf6bf43679a2b900`) reported **4,703 passed / 87 failed / 0 skipped**, exit **1**. All Domain/Application/Infrastructure cases passed (**567 / 1,011 / 1,415**). Desktop exclusive: **2/88 passed**, with **86** parent failures propagated from the Calendar child's **120-second aggregate deadline**. The child's first recorded failure was `FullHeightRegionsHandOffOneGuideAndRetainKeyboardFocusWithSubtleThemeDrawing` (Light/960/96): **Win32Exception: Invalid monitor handle**, raised by WPF Popup/GetMonitorInfo during keyboard tooltip opening, before the eventual aggregate timeout. Last phase breadcrumbs show other chart actions completing before a subsequent modal action began; they do not establish a hung action or the reason for the total elapsed time. No timeout-time managed stack or causal fix for this native-monitor failure is claimed. Desktop parallel: **1,708/1,709 passed**; separate Breakdown accessibility case `ScopedTradesEntryDetailReturnMissingRowAndExitUseLiveFocusTargets(False)` encountered a null task in its test `Pump` helper (line 302). The new risk WPF child itself passed in **10.740s** against its unchanged 90s process deadline. These unrelated failures are retained as evidence, not suppressed. The final code was rebuilt and the complete gate repeated after the additional Save-failure regression; final results follow below.

| Final local verification — 17 October 2026 | Result |
| --- | --- |
| Complete `pwsh -File scripts/Test-Release.ps1` gate | **4,791 passed**, zero failed/skipped, exit **0** |
| Domain / Application / Infrastructure | **567 / 1,011 / 1,415 passed** |
| Complete Desktop | **1,798 passed**: 88 exclusive WPF + 1,710 parallel; parallel test duration **5m04s** |
| Calendar native child | **64.593s**, **55.407s** headroom to unchanged 120s deadline |
| Risk/readiness native child | **7.075s**, successful process exit/cleanup against unchanged 90s process deadline |
| Release build / EF / whitespace / links | **0 warnings/errors**; no pending model changes; tracked/new-file whitespace and **156** local links pass |
| Prior native/automation variability | Final Calendar and all three Breakdown accessibility children passed; **cause not fixed or proven absent** by this run |
| Full-page / human keyboard / Narrator / matching CI | **Pending**; reusable component evidence only; changes remain uncommitted |

Final gate evidence: ignored `artifacts/m21-6/final-gate/release-20261017-202529-bb7635e0fc3d403dbf4ba52bf7544cb2/summary.json`, parent/child TRX and supervision logs. The gate used normal Windows permissions needed by the existing synthetic screenshot-link safety fixture. No assertion, deadline, concurrency bound or isolation policy was changed. Implementation is complete for **M21.6 only**, with final local automated verification green, not full M21/live/CI acceptance. At handoff: `develop`, unchanged HEAD **`4217d973504fced25706bc433dd15edbfa381d97`**, **13 modified + 11 new files**, no commit/push/merge.

Automated component renders: ignored `artifacts/m21-6/renders/risk-{Light|Dark}-{1100-96|460-240}-{empty|partial|invalid|rules-bottom|confirmed|ready}.png` (**24** risk images). Representative normal/narrow Light/Dark frames were inspected for empty/partial limits, malformed amounts/counts, long expanded Rules, confirmations/progress and frozen Ready values. Tests also bind all three sections under one real WPF owner and check risk/scenario/preparation validation-focus routing. These are **automated components**, not full-page acceptance, actual human keyboard interaction, Narrator/screen-reader testing or matching CI. Those remain pending; source changes are uncommitted.

### Exact M21.6 changed-file inventory

**13 modified + 11 new = 24 files.** M21.5 was already committed at the actual baseline, so none of these is reported as merely pre-existing dirty work.

Modified:

- `README.md`
- `docs/architecture.md`
- `docs/trading-plan.md`
- `src/PersonalTradingJournal.Application/TradingPlans/ITradingPlanStore.cs`
- `src/PersonalTradingJournal.Application/TradingPlans/TradingPlanContracts.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/PreparationField.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/SessionPreparationViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/SessionPreparationSection.xaml.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs`
- `src/PersonalTradingJournal.Infrastructure/TradingPlans/TradingPlanReferences.cs`
- `src/PersonalTradingJournal.Infrastructure/TradingPlans/TradingPlanStore.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/SessionPreparationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/TradingPlanPersistenceTests.cs`

New:

- `src/PersonalTradingJournal.Application/TradingPlans/ITradingPlanReferenceReader.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/RiskReadinessItems.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/SessionPreparationViewModel.RiskReadiness.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/SessionPreparationViewModel.Rules.cs`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/RiskReadinessSection.xaml`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/RiskReadinessSection.xaml.cs`
- `src/PersonalTradingJournal.Infrastructure/TradingPlans/TradingPlanReferenceReader.cs`
- `tests/PersonalTradingJournal.Application.Tests/TradingPlans/TradingPlanReadinessSubmissionTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/RiskReadinessTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/RiskReadinessViewTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/TradingPlanReadinessPersistenceTests.cs`

## M21.5 — conditional scenarios and Playbook integration

Actual baseline: **clean** `develop`, `53ad935b9ebb153efe4d1089212be1149ee2e858` (`Add reusable Trading Plan session preparation`). This supersedes the attachment's reported uncommitted M21.4 baseline. M21.4 is retained; its chronological report below is not a claim that its files are still uncommitted. No applicable repository/ancestor `AGENTS.md` was found. Only synthetic catalogs/SQLite fixtures and automated WPF components were used.

### Shared editing ownership

`SessionPreparationSection` and `ScenariosSection` bind **one `SessionPreparationViewModel` instance**. The scenarios partial owns no second aggregate, independent save version, repository or shadow Draft. `ScenarioEditor`/`PreparationField` are transient raw-input adapters over existing `TradingPlanScenario`/`TradingPlanContent`. One content builder combines all preparation and scenario fields; one Save/Refresh operation freezes editing, checks input, sends the exact optimistic token and accepts only verified store readback. Risk limits and Rule links remain unchanged/unowned by these sections. Changing a displayed instrument does not rewrite scenario associations. Unsaved Focus, thesis, raw levels and notes survive scenario edits and all rejected/cancelled operations.

There is no automatic saving or loading on construction. The host explicitly invokes `LoadCatalogAsync`, configures existing Playbook services with `ConfigurePlaybooks`, and invokes `LoadPlaybooksAsync`/the section's Load Playbooks command. Existing Save/RefreshReferences/Cancel commands operate on the combined Draft. `IsDirty`, `HasValidationErrors`, `TryBuildContent`, `Confirmed`, `IsBusy`, exact Key/Token and status are shared. Meaningfully unchanged content retains confirmations/version through the established Domain/store semantics; raw formatting changes may still make an editor dirty. Invalid raw text blocks the entire save instead of persisting a previous value.

M21.7 must compose the sections inside one page ScrollViewer, provide the complete toolbar and Start Draft/Ready operations, integrate scope/close/unsaved-work guards, and cancel **and await** operations before releasing services. This milestone does not replace the navigation placeholder or implement those host policies. All bound session invocation/disposal belongs on its UI thread.

### Scenario and instrument semantics

- Up to the existing **24** scenarios, stable IDs and explicit collection order. Add uses the currently displayed existing plan context (or first context); it seeds no condition/name/direction/Setup. The first new scenario defaults to Primary, subsequent ones to Alternative, both explicitly editable. Domain permits repeated designations and same-direction scenarios; no fabricated opposite-side pair or role-driven sort is imposed. Accessible Move earlier/later actions use one in-memory collection move; only explicit Save persists it. Boundary moves are disabled/no-ops.
- Each card exposes name (**120** characters), designation, exact Instrument context, nullable Long/Short direction, IF observable condition, THEN intended response and INVALID IF cancellation condition (**4,000** each). Long/Short refers to Trade direction, not buy-side/sell-side liquidity. Text/line breaks remain exact. Blanks are valid incomplete Draft content; the unchanged Ready validator requires complete scenario text/direction. Oversized text remains visible with inline validation/automation HelpText/ItemStatus and a red field border.
- Instruments are selected only from the existing plan contexts. Switching the preparation selector retains every scenario and field. Explicit reassignment changes only that scenario; removing a context checks **current unsaved scenarios**, not the originally loaded array. Context removal stays blocked until owning scenarios are explicitly reassigned/removed. No Instrument/Setup creation is exposed.
- Reorder/removal/link/text edits flow through existing scenario-check invalidation. Other confirmations remain unless their own preparation changed. Save failures/conflicts preserve all raw fields and selected links; no newer stored Draft is silently loaded over them.

### Optional Playbook links and retained context

The existing Setup catalog is the sole source; cardinality remains one optional Setup link per scenario. Catalog choices list active Setups; **Select revision** requires current saved guidance (a Setup without guidance is rejected with a clear message). It reads the exact current Setup/Playbook and captures existing `TradingPlanSetupLink` identity, revision and Setup timestamp, then displays the five guidance sections for review before saving. Choosing a ComboBox candidate alone does not change the link. Unlink is explicit. No editor operation writes the Setup catalog, revisions or images.

Load Playbooks reads the existing catalog once and only fetches guidance/current availability for distinct selected tokens (at most 24), not every catalog entry. These informational reads are **not** a transactional reference snapshot; the existing write-time SQLite reservation/fingerprint checks remain authoritative. Reloading shows changed/archived/unavailable references while retaining the original selected token and exact historical criteria. After a material change, explicitly select the intended current revision, review it, then use RefreshReferences and reconfirm all checks. A catalog reload alone never accepts a baseline. For multiple scenarios linking the same Setup, reselect each affected link consistently before refresh. Failed/cancelled selection retains the previous link/input.

Ready/historical views use the frozen Setup name, exact revision and five guidance sections from `TradingPlanReferenceSnapshot`, not current catalog content. A missing live Setup disables navigation but cannot erase the saved criteria. Availability is displayed separately. Existing Plan snapshots still deliberately omit example/image metadata/binaries; no image retention, storage, backup or AI evidence contract changes are made.

`PlaybookNavigation` is a host-supplied exact-Setup-ID asynchronous request returning accepted/vetoed. The component never clears input, saves, switches destinations or implements a competing guard. M21.7 must route it through the existing shell navigation and `PlaybookViewModel.TrySelectSetup` guard, rechecking live availability. Without a handler, Open Playbook is disabled. Veto/cancellation/errors retain Draft input; opening current Playbook is explicitly separate from reading frozen Ready criteria. Association is **not verified adherence**.

### Stand Aside and responsive presentation

Full-width Stand Aside uses the existing plan-level `StandAside` text and `Intent`. Opportunity plans may retain no-trade restrictions; the explicit stand-aside decision requires no fabricated scenarios, bias, thesis or Setup. Selecting it never automatically deletes existing scenarios: Draft retains them with a message; Ready requires their explicit removal and a meaningful reason, plus the existing session/Focus/instrument/checklist requirements. Earlier Ready revisions/timestamps remain immutable; editing still requires the existing Start Draft transition.

Cards form two columns at 900 DIP of usable card width and predictable subsequent rows; narrow widths stack them in logical collection/tab order. Text inputs wrap and scroll within bounded heights; the host owns page scrolling. Labels and textual direction remain present with theme accents. Validation focus uses the shared guarded `PageFocus`: both sections consult the same first-invalid-field identity, so only its owning section requests focus/scroll after an explicit invalid Save. No focus movement occurs merely for a status message. Ready fields/commands remain read-only/disabled.

### M21.5 verification

Focused final verification: **39 passed**, zero failed/skipped (24 existing preparation cases + **15 new** scenario/shared-state/SQLite/WPF cases). Release solution build: **0 warnings, 0 errors** (12.86s). Component tests/renders are not full-page, human keyboard/Narrator or matching-CI acceptance. No assertions, deadlines or test scheduling are weakened.

| Final local verification — 17 October 2026 | Result |
| --- | --- |
| Complete `scripts/Test-Release.ps1` gate | **4,745 passed**, zero failed/skipped, exit **0** |
| Domain / Application / Infrastructure | **567 / 1,003 / 1,410 passed** |
| Complete Desktop | **1,765 passed**: 88 exclusive WPF + 1,677 parallel; parallel phase **5m08s** |
| Calendar native child | **71.474s**, **48.526s** headroom to unchanged 120s deadline |
| Preparation / scenario native children | **5.953s / 7.336s**, successful process exit and cleanup |
| EF / whitespace / documentation links | No pending model changes; tracked/new-file whitespace pass; **154** local links resolve |
| Full-page / human keyboard / Narrator / matching CI | **Pending**; reusable component evidence only, source changes uncommitted |

Final gate evidence: ignored `artifacts/m21-5/acceptance-gate/release-20261017-192817-53c3c60202c04b2199af2f5b33569220/summary.json`, parent/child TRX and supervision logs. The complete gate used normal Windows permissions needed by the existing synthetic screenshot-link safety fixture. There were no production-journal reads/writes, provider requests or Git publication operations. Worktree at handoff: **5 modified + 6 new files**, all listed below; branch/HEAD unchanged from this slice's actual baseline.

Coverage includes raw shared edits saved together/reloaded from SQLite, context switching/reassignment and removal guards, order/role/direction boundaries, 24-scenario cap, blank Draft versus Ready, all text limits, Stand Aside, no-op confirmations, link/unlink and catalog disappearance, changed revision followed by explicit reference refresh, stale writes, read-only frozen context after live change/removal, navigation veto, cancellation and zero Setup writes. The compiled test exercises three cards, live bindings, accessible field names, reorder command binding, long-content scrolling, invalid-field recovery, read-only controls and a real synthetic owner with **both** sections: an explicit invalid Save focuses/scrolls to the shared first-invalid field. This is WPF automation, not human keyboard or screen-reader testing.

Twenty scenario renders were generated under ignored `artifacts/m21-5/renders/`: `scenarios-{Light|Dark}-{1100-96|460-240}-{multiple|invalid|bottom|stand-aside|ready}.png`. Inspected representative renders in both themes and widths cover the two-column/stacked arrangement, loaded synthetic labels/Playbook criteria, Long/Short headings, long input, red inline errors, reachable lower actions/Stand Aside, and read-only Ready content. No horizontal page overflow or overlapping controls was observed. The remaining M21.4 focused test also regenerated its preparation renders. These are component views in a synthetic scroll host, **not the full Session Brief** or live accessibility acceptance.

Two earlier complete builds passed **4,744** and **4,745** tests respectively. Final inspection then clarified changed-reference wording for Ready (start a Draft, never refresh the historical view), so the final source was rebuilt and verified again rather than reusing that earlier gate result. The first-invalid-field ordering regression and stale-candidate enablement check also remain in the final tests. No failed test was hidden by a retry/skip or weaker assertion.

Commands used for this slice:

```powershell
dotnet build PersonalTradingJournal.sln -c Release --no-restore
$env:PTJ_PLAN_RENDER_DIRECTORY = Join-Path (Get-Location) 'artifacts/m21-5/renders'
dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~TradingPlans --logger 'trx;LogFileName=desktop.trx' --results-directory artifacts/m21-5/acceptance-focused
pwsh -File scripts/Test-Release.ps1 -ResultsDirectory artifacts/m21-5/acceptance-gate
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure/PersonalTradingJournal.Infrastructure.csproj --context JournalDbContext --configuration Release --no-build
git diff --check
```

New files were also checked with `git diff --no-index --check`; LF-to-CRLF notices are not whitespace failures. The local documentation link check resolves file targets in README, architecture and this document. These checks do not substitute for a matching published-commit CI run.

### Exact M21.5 changed-file inventory

New (6):

- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/ScenarioEditor.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/SessionPreparationViewModel.Scenarios.cs`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/ScenariosSection.xaml`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/ScenariosSection.xaml.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/ScenarioTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/ScenariosViewTests.cs`

Extended existing M21.4 files (2):

- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/SessionPreparationViewModel.cs` — shared combined content, dirty/validation state, current scenario ownership and access notifications.
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/SessionPreparationSection.xaml.cs` — coordinate first-invalid-field focus with the sibling section.

Documentation (3): `README.md`, `docs/architecture.md`, `docs/trading-plan.md`. No Domain/Application/Infrastructure, migration/schema, JSON export v10, backup/restore, navigation or existing economics changes. No production data, providers, commit, push or merge. Full M21 acceptance and M21.7 host integration remain pending.

## M21.4 — session preparation and instrument context

Implemented from **clean** `develop`, HEAD `6405469aaaf06d38a6c7cc341486e550195f0687`. Actual inspection superseded the attachment's reported dirty M21.3 baseline: those changes are now committed. No applicable `AGENTS.md` was found. No M21.3 files are overwritten or reimplemented; the earlier milestone reports below are historical checkpoints.

### Component and host ownership

`SessionPreparationSection` is a reusable WPF section, **not** the complete Session Brief destination. It contains Today's Focus, `01 Session Preparation`, session/instrument controls, bias/thesis, six optional key levels and full-width manual News & Restricted Times. Context and levels are side by side at sufficient width and stack below 760 DIP. The future host supplies a single page ScrollViewer; long text inputs scroll within bounded heights, and exceptionally precise single-line prices remain horizontally scrollable within their input rather than overflowing the page. Shared Light/Dark resources supply opaque controls, teal headings, focus indicators and validation borders.

The host loads or explicitly creates a Plan through the existing store, then constructs `SessionPreparationViewModel` with that exact aggregate, `ITradingPlanStore` and existing `IInstrumentReader`. The component does not automatically load, save, create, mark Ready, start a Draft, reconfirm, or navigate. `LoadCatalogAsync`, `SaveAsync`/`SaveCommand`, `RefreshReferencesAsync`/`RefreshReferencesCommand` and cancellation are explicit host operations. Catalog and storage work run off the dispatcher. Bind and invoke/dispose the session on its owning UI thread. On accepted navigation/close, cancel and **await the outstanding operation** before disposing database services; cancellation alone is not completion. M21.7 must integrate this lifecycle, dirty-state guards, save/start-Draft/refresh controls, full toolbar and single page scroll rather than create another editor state implementation.

The ViewModel retains a reconstructed private aggregate/token, immutable unowned content and transient per-instrument raw fields. It exposes `IsDirty`, `IsBusy`, `CanEdit`, `HasValidationErrors`, `TryBuildContent`, `Confirmed`, exact `Key`/`Token`, and sanitized `Message`. Dirty state compares raw input, so formatting-only edits can be dirty; Domain semantic comparison still prevents unnecessary version/checklist changes on save. `Confirmed` previews the existing Domain `EditDraft` on a reconstructed copy for valid content; when raw input is invalid, it reports the previous stored checks and `HasValidationErrors`/`TryBuildContent` prevent that input from being submitted. It is not a readiness bypass. No checklist editor or automatic confirmation is added.

### Input, scope and preservation

- Focus retains exact multiline text up to the existing **500-character** limit; thesis/news use **4,000**. Blank required-for-Ready fields remain valid incomplete Draft input. Oversized text is retained with inline errors instead of truncated. Required readiness remains the existing separate Mark Ready policy.
- Each context retains exact Instrument ID, nullable bias, thesis and all six raw price fields while switching. Adding uses active exact catalog IDs, rejects duplicates and caps contexts at the existing **12**. Removal is blocked when that context owns any scenario; it does not silently delete or reassign scenarios. Existing inactive/unavailable contexts remain visible by identity and entered preparation remains editable, but the existing store's fresh-reference checks may block persistence until the reference is restored or explicitly removed/relinked.
- Levels reuse `TradingPlanLevels`: Previous Day High/Low, Asia High/Low, London High/Low. Blank/whitespace becomes **null**, zero and negative prices remain real values. Culture-aware decimal separators and the existing invariant fallback are supported; thousands separators/exponents are not. Values display using the session culture and decimal precision, without tick-size rounding or invented padding. A canonical decimal comparison also rejects excessive precision that `decimal.TryParse` would round. Invalid raw input survives switching and rejected saves; **no older valid price is saved behind newer invalid text**.
- Validation marks inputs through shared `FormValidation`, exposes inline text plus automation HelpText/ItemStatus, and selects/scrolls/focuses the first invalid context/input after an explicit failed save. Correcting one field clears its error immediately. The component does not move focus merely to show a status message.
- Session is existing metadata, not another Plan key. Blank bias/session is never a user confirmation. News/restrictions are one existing plan-level manual text field, including multiline notes and explicit removal by clearing; no feed or event interpretation is introduced. Existing copy-forward still clears news, levels, thesis, bias and confirmations.
- Saves preserve scenarios, Setup links, risk limits, Rule links, intent and stand-aside text unchanged. Valid edits use `SaveDraft`; checklist invalidation is authoritative in Domain/SQLite. No-op semantic input preserves checks/version. Errors, conflict, cancellation and missing readback preserve raw inputs and the expected token; success is shown only with the store's verified committed aggregate. Busy state suppresses duplicate submissions and freezes edits until the operation completes.
- Catalog reload changes display availability only; it does not accept a new reference baseline. `ReferenceChanged` requires explicit refresh; `RefreshReferences` saves valid preparation and clears **all** confirmations. Unavailable/inactive references still block, and failed refresh retains input. Setup/Rule reselection remains the later owning editors' responsibility.
- Ready and explicitly supplied historical Ready revisions are read-only, with **saved** instrument names/symbols even after catalog rename/deletion. To edit, the host must execute the existing exact-revision `StartDraft` operation and construct a new editing session from its committed result; the component cannot modify a Ready value or overwrite another working Draft.

Synthetic NQ example: select NQ from the catalog, choose Conditional bias, enter a thesis and optional `20123.25` prior-day high, leave unknown lows blank, and record manual restricted times. This is user-entered preparation only, not market data, an economic-calendar fact or proof a later Trade followed the plan. No example text is seeded into real plans.

### Verification and limits

Focused coverage includes culture/precision/null/zero/invalid inputs, switching/isolation, duplicate prevention, scenario-protected removal, unchanged semantic edits, Domain checklist invalidation, incomplete Drafts, validation recovery, all sanitized write outcomes, cancellation/duplicate submission, inactive/unavailable catalog entries, frozen Ready labels and real SQLite save/reload/stale/reference-refresh behavior. Compiled WPF coverage exercises bindings, accessible controls, read-only Ready inputs, real owner-window invalid-field focus routing and responsive geometry; this is automation, not human keyboard or screen-reader acceptance.

| Final local verification (17 October 2026) | Result |
| --- | --- |
| Focused Trading Plan/section tests | **156 passed**, zero failed/skipped: Domain 77, Application 27, Infrastructure 28, Desktop 24 new cases |
| Complete Release gate | **4,730 passed**, zero failed/skipped, exit **0** |
| Complete Domain / Application / Infrastructure | **567 / 1,003 / 1,410 passed** |
| Complete Desktop | **1,750 passed**: 88 exclusive WPF + 1,662 parallel; parallel Desktop 5m03s |
| Native deadlines | Calendar child **66.887s**, **53.113s** headroom to its unchanged 120s deadline; Journal child **10.596s**; new section child **7.362s** under complete competing load |
| Release solution build | **0 warnings, 0 errors** |
| EF / whitespace / local links | No pending model changes; tracked/new-file whitespace checks pass; **152** documentation file links resolve |
| Automated visual inspection | Light/Dark, normal/narrow 96/240 DPI, Draft, invalid input and scrolled-bottom views inspected; no page overflow or inaccessible lower fields observed |
| Full-page / human interaction / Narrator / matching CI | **Pending**, not implied by component tests or renders; changes remain uncommitted |

The first new Desktop run had 21 passes and three test-expectation/setup failures: immutable-array backing identity versus sequence equality, a negative status substring also matching “could not be saved,” and render selection carried between variants. These were corrected without weakening behavioral checks. Extending the compiled test to a real owner window exposed an off-STA fixture disposal, corrected to UI-thread ownership, and an actual new-section focus-ordering issue: the normal-priority focus callback could run before the newly selected invalid context's bindings/layout. The section now reuses existing `PageFocus` (Loaded priority, owner/input-generation guards) and `LiveAnnouncement`; the final owner-window test verifies the selected invalid field receives focus. Render inspection also replaced uniform level-row heights with auto-height rows, avoiding repeated blank space when one row has a long error. No existing assertions, deadlines, runner scheduling or production workflows were weakened.

Commands actually run:

```powershell
dotnet build PersonalTradingJournal.sln -c Release --no-restore
$env:PTJ_PLAN_RENDER_DIRECTORY = Join-Path (Get-Location) 'artifacts/m21-4/renders'
dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~TradingPlans --logger 'trx;LogFileName=desktop-focused.trx' --results-directory artifacts/m21-4/final-layout-focused
dotnet test tests/PersonalTradingJournal.Domain.Tests/PersonalTradingJournal.Domain.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~TradingPlans --logger 'trx;LogFileName=domain.trx' --results-directory artifacts/m21-4/focused-complete
dotnet test tests/PersonalTradingJournal.Application.Tests/PersonalTradingJournal.Application.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~TradingPlans --logger 'trx;LogFileName=application.trx' --results-directory artifacts/m21-4/focused-complete
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests/PersonalTradingJournal.Infrastructure.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~TradingPlans --logger 'trx;LogFileName=infrastructure.trx' --results-directory artifacts/m21-4/focused-complete
pwsh -File scripts/Test-Release.ps1 -ResultsDirectory artifacts/m21-4/release-gate
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure/PersonalTradingJournal.Infrastructure.csproj --context JournalDbContext --configuration Release --no-build
git diff --check
```

The complete gate used the Windows permissions required by the existing synthetic screenshot-symlink safety fixture. Gate evidence: ignored `artifacts/m21-4/release-gate/release-20261017-162321-df600def25cc4191a628275a9a307f91/summary.json`, parent/child TRX and supervision logs. New files were also checked with `git diff --no-index --check`; Git's LF-to-CRLF notices are not whitespace failures. No GitHub run is claimed for the uncommitted changes.

Automated renders are under ignored `artifacts/m21-4/renders/`: `preparation-{Light|Dark}-{1040-96|460-240}-{draft|invalid|bottom}.png`. Normal 1040-DIP / 96-DPI and narrow 460-DIP / 240-DPI synthetic components include top/validation and scrolled-bottom views. They are **component** renders, not the unimplemented full Session Brief. M21.7 integration, actual human input/Narrator and matching CI remain pending. No navigation, scenario/risk/checklist editor, History UI, AI/Review integration, external provider or automatic market data was added. Schema/migrations, portable JSON **v10**, backup behavior and the documented **older-schema restore deferral** remain unchanged.

### Exact M21.4 changed files

Seven new files:

- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/PreparationField.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/InstrumentPreparationEditor.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/TradingPlans/SessionPreparationViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/SessionPreparationSection.xaml`
- `src/PersonalTradingJournal.Desktop/Views/TradingPlans/SessionPreparationSection.xaml.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/SessionPreparationTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/SessionPreparationViewTests.cs`

Three modified documents: `README.md`, `docs/architecture.md`, `docs/trading-plan.md`. No Domain/Application/Infrastructure code or M21.3 migration changed. Generated renders/TRX are ignored artifacts, not source files. No commit, push, merge, branch switch, production-journal access or provider request was performed.

## M21.3 — persistence, migrations and application use cases

Implemented from clean `develop`, HEAD `6682482bcd68bd5fe0c2bdf66bf81468f67ba6c4`. No applicable `AGENTS.md` was present. Reuses M21.1/M21.2 identity, content bounds, readiness, checklist invalidation, frozen reference types, optimistic token, copy-forward and Domain transitions. No UI, navigation, automatic matching, Review/AI integration, market feed or existing economics changes.

### Storage and exact reconstruction

Additive migration `20261017152354_AddTradingPlans` creates:

- `TradingPlans`: one unique `(AccountId, NewYorkDate)`, stable GUID, long optimistic `Version`, UTC audit times, latest Ready number, optional working `DraftJson` and its `ReferenceStateJson` baseline. DateOnly is the explicit New York date, not converted from the save instant.
- `TradingPlanReadyRevisions`: composite `(PlanId, Revision)` primary key, UTC Ready time and self-contained `SnapshotJson`. The owner foreign key is restrictive; no Account/Instrument/Setup/Rule cascade can delete historical preparation. SQLite triggers reject Ready updates/deletes and nonconsecutive appends. No retention or deletion operation is introduced.

Draft and Ready storage envelopes are v1, separate from unrelated review/export versions. The focused mapper serializes existing content and snapshots, not an alternate authoring model. Decimals remain JSON numbers with exact decimal serialization, nullable values remain null, collection order/text/line breaks are preserved. Each payload has a 2,000,000-character limit and JSON depth 24; unknown envelope versions, missing required constructor fields, duplicate properties, malformed content, inconsistent identity/lifecycle/sequence/chronology and invalid reference baselines fail rather than being repaired. A payload exceeding the storage bound is a safe storage failure with no commit, not truncated content.

Domain reconstruction validates existing identity, UTC instants, Draft derivation, Ready readiness/reference membership, source-Draft version and aggregate state. It does not execute Edit/Ready, generate IDs/times, clear checkmarks, or look up live catalogs. Catalog currencies retain their existing up-to-eight-character contract; automatic Rule criteria retain their own narrower currency rules. `LoadAsync` uses one SQLite read transaction for owner, sequence and latest Ready. History reads are bounded to 1–100 rows: exact Account/date descending with an exclusive `beforeDate` cursor (date is unique within Account), and separate Ready history by revision descending with an exclusive `beforeRevision`. No write loads the entire history.

### Operations and transaction boundaries

`ITradingPlanStore` provides exact-scope load, exact Ready load, basic Plan/Ready history and `WriteAsync`; `WriteTradingPlanUseCase` is registered in existing persistence DI. Commands capture scope independently of future navigation and carry `TradingPlanExpectedState` for existing Plans. Results return the committed aggregate/version, validation issues or typed reference issues. Read cancellation/storage errors propagate; malformed persisted evidence uses sanitized `InvalidDataException`. Write failures return `Invalid`, `Conflict`, `TargetAlreadyExists`, `ReferenceUnavailable`, `ReferenceChanged`, `Cancelled` or `StorageFailure`, never raw source text or database diagnostics.

| Operation | Authoritative behavior |
| --- | --- |
| Create Draft | Revalidate exact active references, new identity/version 1; reset all incoming confirmations. Existing Account/date is not overwritten. |
| Save Draft | Apply existing content comparison/checklist invalidation. Incoming checkmarks are ignored; exact no-op content leaves the aggregate version unchanged. |
| ConfirmChecklist | Explicit separate operation; requires unchanged preparation and current token. A simultaneous prose edit is rejected. |
| Mark Ready | Requires exact stored Draft content, current version, complete readiness/checklist and fresh references. Append one revision and update current state atomically. |
| Start Draft | Read the selected immutable revision from this Plan; ignore caller replacement content. Retain all Ready revisions and reset confirmations. Current references must still validate. |
| RefreshReferences | Explicitly accept fresh reference interpretation, optionally update selected Setup/Rule tokens/content, clear all checks and advance the aggregate version. |
| Copy forward | Read the authoritative source Ready payload, ignore caller-provided replacement content, apply existing daily-context/checklist resets, revalidate target references and create a new target identity. Concurrent target creation yields one success and one `TargetAlreadyExists`. |

Every mutation obtains SQLite's immediate writer reservation **before** reading the Plan or references. Fresh version/identity checks, catalog reads, candidate Domain transitions, owner/revision writes and reconstruction readback share that transaction. Uniqueness and EF concurrency tokens are additional guards. A stale writer never overwrites a newer Draft. Mark Ready cannot append without the owner transition. Any failed write/readback or cancellation before commit rolls back. A final cancellation check precedes commit; commit uses `CancellationToken.None`, and cancellation arriving after a successful commit still returns success. SQLite's synchronous writer-reservation wait is not interruptible mid-call; it uses the existing provider busy timeout. No background operation is abandoned or automatically retried.

### Fresh references and retained history

Reference resolution uses exact IDs in bounded batched queries under the writer reservation. Account and Instruments must be active; Setup must be active with the selected current Playbook revision and Setup update token; Rule must be active/global with the exact latest revision. Missing/inactive/legacy-scoped references are explicitly unavailable. No matching by name/symbol is used.

The Draft baseline stores SHA-256 fingerprints of authoritative selected catalog records (not credentials). Surviving selected references must match it. This detects changed Account currency, Instrument tick interpretation, Setup guidance/version and Rule text/version even if a test updates fields without changing an audit timestamp. The policy is deliberately conservative: cosmetic labels/audit or other catalog record changes also require explicit `RefreshReferences`. New selections resolve fresh; intentionally removed selections no longer block saving. Refresh is not implicit during Save or Ready and clears every confirmation. Changed Setup/Rule revision tokens must first be explicitly reselected by the caller. Ready captures verified labels/guidance, never caller-supplied labels.

Ready reads use only their preserved payloads. Account/Instrument deletion or same-name replacement does not retarget them. A deleted Playbook revision is not needed for historical readability because its selected guidance is frozen; the existing catalog deletion/image policies are unchanged. Starting/copying into an active Draft may be blocked until unavailable/changed references are resolved. Ready numbers never rewind when drafting from an older revision. A saved plan still proves preparation, not Trade adherence.

### Migration and portability

The migration preserves existing rows and creates no historical Plan backfill. Complete backups retain both tables and triggers. Synthetic portability tests verify exact Draft-after-Ready and Ready payloads after backup, preflight, offline recovery-protected restore and fresh services. Portable JSON advances to **v10** with `tradingPlans` and `tradingPlanReadyRevisions`; serialized payload strings remain exact, and the four existing CSVs remain unchanged. Current-schema-only restore rejects pre-migration-21 archives pending the deferred staged upgrade path. [Confidentiality, backup validation boundary and compatibility](backup-restore.md#m213--trading-plan-compatibility).

### Verification — 17 October 2026

| Final local check | Observed result |
| --- | --- |
| Focused Trading Plan tests | **132 passed / 0 failed / 0 skipped**: Domain 77, Application 27, Infrastructure 28; 40 new cases |
| Complete Domain Release | **567 passed / 0 failed / 0 skipped** |
| Complete Application Release | **1,003 passed / 0 failed / 0 skipped** |
| Complete Infrastructure Release | **1,410 passed / 0 failed / 0 skipped**, 1 minute 57 seconds |
| Required three-project total | **2,980 passed / 0 failed / 0 skipped**, all commands exit 0 |
| Final Release solution build | **0 warnings / 0 errors**, 12.98 seconds |
| EF consistency | No model changes since the new migration; design-time factory uses in-memory SQLite |
| Whitespace / documentation | Tracked and new-file checks passed; **159 local links** resolve; documented 35-file inventory matches Git status |
| Desktop gate, renders, live interaction, matching CI | Not performed/claimed for this persistence slice; changes are uncommitted |

Initial focused testing found and corrected a malformed-payload exception escaping write-outcome translation; a trigger test's literal braces also needed a fixture-only SQL correction. The initial complete Infrastructure run reported **1,395 passed / 10 failed**: nine explicit old-schema expectations and one Windows symlink privilege failure. The schema expectations are updated, not removed; the final complete run uses the required Windows test privileges. Subsequent reconstruction hardening rejects nested missing/defaulted values and mismatched Ready ownership. Its initial owner-join projection failed EF translation; filtering/paging now occurs before that projection and the 28-case SQLite run passed. One overlapping test-build was stopped after DLL-copy contention; no test lifetime/deadline was changed. No deadline, test retry policy or assertion is weakened. Final TRX evidence is under ignored `artifacts/m21-3/final-focused/` and `artifacts/m21-3/final-regression/`.

Commands used (all paths below are synthetic output directories):

```powershell
dotnet test tests/PersonalTradingJournal.Domain.Tests/PersonalTradingJournal.Domain.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~TradingPlans --logger "trx;LogFileName=domain.trx" --results-directory artifacts/m21-3/final-focused
dotnet test tests/PersonalTradingJournal.Application.Tests/PersonalTradingJournal.Application.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~TradingPlans --logger "trx;LogFileName=application.trx" --results-directory artifacts/m21-3/final-focused
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests/PersonalTradingJournal.Infrastructure.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~TradingPlans --logger "trx;LogFileName=infra.trx" --results-directory artifacts/m21-3/final-focused
dotnet test tests/PersonalTradingJournal.Domain.Tests/PersonalTradingJournal.Domain.Tests.csproj -c Release --no-build --no-restore --logger "trx;LogFileName=domain.trx" --results-directory artifacts/m21-3/final-regression
dotnet test tests/PersonalTradingJournal.Application.Tests/PersonalTradingJournal.Application.Tests.csproj -c Release --no-build --no-restore --logger "trx;LogFileName=application.trx" --results-directory artifacts/m21-3/final-regression
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests/PersonalTradingJournal.Infrastructure.Tests.csproj -c Release --no-build --no-restore --logger "trx;LogFileName=infrastructure.trx" --results-directory artifacts/m21-3/final-regression
dotnet build PersonalTradingJournal.sln -c Release --no-restore
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure/PersonalTradingJournal.Infrastructure.csproj --context JournalDbContext --configuration Release --no-build
git diff --check
```

The Infrastructure complete run requires the normal Windows symlink privilege used by the existing backup safety fixture. A related 55-case schema/initialization/integrity/portability selection also passed before the final added owner-attribution test. Focused coverage includes all three lifecycle states, preserved checks, historical derivation, precision/nulls/Unicode, exact scopes, keyset paging, stale saves, concurrent Ready/copy targets, rollback after SQL writes, cancellation before and after commit, a competing reference writer blocked until commit, cosmetic/material changes, inactive/deleted/same-name sources, immutable SQL triggers, malformed reconstruction, migration and real backup/preflight/offline restore/export. These are isolated SQLite/service tests, not Desktop or production-journal actions.

Scope limits: no Session Brief/History UI, renders, live interaction, integrated Desktop gate, Trade matching, Daily Review integration or matching CI acceptance is claimed. No production journal or provider was accessed. Full M21 remains pending.

### Exact M21.3 changed files

Documentation:

- `README.md`
- `docs/architecture.md`
- `docs/trading-plan.md`
- `docs/backup-restore.md`

Domain/Application:

- `src/PersonalTradingJournal.Domain/TradingPlans/TradingPlan.cs`
- `src/PersonalTradingJournal.Domain/TradingPlans/TradingPlanReadyRevision.cs`
- `src/PersonalTradingJournal.Domain/TradingPlans/TradingPlanReferenceSnapshot.cs`
- `src/PersonalTradingJournal.Domain/TradingPlans/TradingPlanSnapshotReconstruction.cs`
- `src/PersonalTradingJournal.Application/TradingPlans/TradingPlanContracts.cs`
- `src/PersonalTradingJournal.Application/TradingPlans/ITradingPlanStore.cs`
- `src/PersonalTradingJournal.Application/Backups/IPortableExportService.cs`

Infrastructure:

- `src/PersonalTradingJournal.Infrastructure/TradingPlans/TradingPlanJson.cs`
- `src/PersonalTradingJournal.Infrastructure/TradingPlans/TradingPlanReferences.cs`
- `src/PersonalTradingJournal.Infrastructure/TradingPlans/TradingPlanStore.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Records/TradingPlanRecords.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Configurations/TradingPlanConfiguration.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/JournalDbContext.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261017152354_AddTradingPlans.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261017152354_AddTradingPlans.Designer.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/JournalDbContextModelSnapshot.cs`
- `src/PersonalTradingJournal.Infrastructure/Backups/PortableExportSchema.cs`

Tests:

- `tests/PersonalTradingJournal.Domain.Tests/TradingPlans/TradingPlanReconstructionTests.cs`
- `tests/PersonalTradingJournal.Application.Tests/TradingPlans/TradingPlanRevisionContractTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/TradingPlanPersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/TradingPlanPortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Migrations/InitialMigrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Initialization/JournalDatabaseInitializerTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integration/ProductionPersistenceIntegrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integrity/SqlitePersistenceIntegrityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookPersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/PortableExportTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/SavedPeriodPortabilityTests.cs`

The historical M21.1/M21.2 sections below retain their original checkpoint results and boundaries; the M21.3 section above supersedes their persistence-pending statements.

## M21.2 — preserved Ready revisions and working Drafts

Implemented on the existing M21.1 foundation, starting from clean `develop` at `317305e582036aa28ee9fd66d506bf9dcc6161f2` (`Define Trading Plan identity and readiness contracts`). No repository/ancestor `AGENTS.md` was present. The prior scope, exact account/date identity, immutable bounded content, readiness validation, checklist invalidation mapping, nullable decimals, New York defaults and optimistic token are reused. Missing aggregate transitions and reference captures are now implemented; persistence, migration, Desktop and review contracts are unchanged.

### Aggregate lifecycle and failure guarantees

`TradingPlan` derives from the existing `AuditableEntity`. Plan ID and `TradingPlanKey` never change. Its `Version` is the same long optimistic save token exposed by M21.1; no second concurrency token is introduced. Successful Domain mutations advance it, and exact no-op preparation/confirmation assignments do not. Application `TradingPlanLoadedState.FromPlan` adapts the actual aggregate to the existing `CheckTransition` contract.

| Domain state | Working Draft | Latest Ready |
| --- | --- | --- |
| `DraftOnly` | Initial preparation; confirmations start unset | Absent |
| `Ready` | Absent | Complete immutable confirmed revision |
| `DraftAfterReady` | New preparation with its exact source Ready revision ID | Previous latest Ready remains available |

These states are computed from aggregate data, not UI flags. They do not assert that an in-memory operation has been saved to disk.

- `EditDraft` compares preparation values, including nested collections, rather than immutable-array object identity. It invalidates affected existing confirmations using M21.1's policy. Incoming `Content.Confirmed` flags are ignored, including on a no-op assignment; they cannot confirm edited preparation. Confirmations can be changed only through explicit `SetConfirmations`.
- A new Plan and `StartDraft` always begin with no confirmations, even when their source content had all five. Confirmation alone does not waive missing content: `MarkReady` still runs `TradingPlanReadiness.Validate` and returns field-addressed issues through `TradingPlanNotReadyException` when incomplete.
- `MarkReady` requires a working Draft, the exact expected version, canonical nondecreasing UTC time and an exact matching reference capture for that Ready operation. It allocates `(PlanId, latest Ready number + 1)`, returns the complete immutable revision, retains it as latest and removes the working Draft. A repeated call cannot generate another revision without a valid Start Draft transition and reconfirmation.
- `StartDraft` requires Ready state and an exact historical Ready value from the same Plan/key/creation identity. It cannot overwrite a working Draft. Starting from revision 1 after revision 2 preserves revision 2 as latest; the next Ready is revision 3, with `DerivedFromRevision` identifying revision 1.
- Revision/content/reference objects expose no mutable setters. Input enumerables are bounded and copied; nested values are sealed immutable objects. Sharing immutable leaf values is safe. Replacing a working Draft or deriving a `with` copy of price levels cannot modify any previous Ready value.
- All validation, content construction and checked counter arithmetic finish before observable aggregate state changes. Rejected readiness, wrong source identity/version, inactive references, malformed timestamps, stale versions and repeated transitions retain the same Draft, latest Ready, version and audit timestamp. No speculative state is published on a failed operation.

The aggregate intentionally retains only its working Draft and latest Ready, rather than loading an unbounded history into every command. Each returned `TradingPlanReadyRevision` is independently immutable. M21.3 must append and retain **every** returned Ready value and load an older source revision by its exact identity when requested. Replacing the aggregate's latest reference never authorizes replacing or deleting a stored historical row. Domain instances have single-owner mutation semantics; concurrent database writers still require the planned transaction/version check.

### Self-contained snapshots

Each Ready revision contains the complete M21.1 content: selected session, Focus, Instrument contexts/bias/thesis/nullable levels, ordered scenarios/IF/THEN/invalidation and Setup selection tokens, stand-aside intent/notes, planned limits, Rule selections, manually maintained news/restrictions and completed checklist. It also contains stable Plan/revision ID, account/date key, source-Draft version, derivation identity and the timestamps below.

`TradingPlanReferenceSnapshot.Capture` accepts supplied canonical Account, Instrument, Setup/Playbook and Rule objects. It contains no service or storage lookup. Supplied references must exactly match selected IDs, with no missing, duplicate or unrelated entries. A Setup used in multiple scenarios is captured once; all its selection tokens must agree. Capture rejects inactive sources, changed selected revisions/Setup timestamps, legacy scoped Rules and reference timestamps after the capture time. Output reference order follows the preparation rather than catalog query order. A separate final validation checks that the capture still matches the Draft's links and exact Ready timestamp.

| Reference | Frozen context | Deliberately excluded |
| --- | --- | --- |
| Account | Exact ID, name, currency, source update UTC time | Starting balance, broker/external identity, credentials |
| Instrument | Exact ID, symbol, display name, currency, tick size, update UTC time | Live market values, executions, economic recalculation |
| Playbook Setup | Exact Setup ID, selected label/description, Setup update UTC time, exact Playbook revision/recorded time, its five guidance text sections | Example images, example annotations, filesystem paths, unrelated revisions |
| Global Rule | Exact Rule ID/revision/recorded UTC time and immutable definition: name, criterion, authored evidence description and any typed condition | Assessment history, automatic evaluation, unrelated Rules |

The Setup label is the supplied catalog label at capture; the guidance belongs to the exact selected Playbook revision. Linked Rule definitions retain their typed condition as context only; finalizing a Plan does not run it. All text is user-authored preparation, not proof of adherence. Planned monetary limits use the captured Account currency; Instrument price-level context retains its own currency/tick size. No conversion or zero substitution occurs.

Historical reading uses these frozen values even if catalogs change, archive or disappear. Any future “open source” navigation must resolve the stable ID and report unavailability separately; it must not replace snapshot labels/content or substitute a same-name entity. No screenshot ownership/retention rule changes: Plan snapshots do not own or require the excluded Playbook images.

### Chronology

`PlanCreatedAtUtc`, `DraftStartedAtUtc`, `DraftUpdatedAtUtc`, `ReadyAtUtc`, reference `CapturedAtUtc`, source update/revision times and `SourceDraftVersion` distinguish intended date from actual preparation chronology. Domain accepts explicit canonical UTC instants, rejects backwards audit transitions and never synthesizes Ready time from the New York date. Equal operation timestamps are permitted (revision/version order remains explicit). The caller must supply the actual clock instant; Domain cannot independently attest the operating-system clock.

Starting from history preserves that historical revision's original timestamps and creates new Draft timestamps. A Ready value's `DerivedFromRevision` records its source, including older revisions. The existing `TradingPlanDatePolicy.WasRecordedBeforeEntry` can consume `ReadyAtUtc`: strictly earlier establishes only record availability, equality/later/unknown does not. No Trade matching or Review classification is added.

### M21.3 implementation requirements

- Add validated reconstruction/serialization and an additive schema with exact account/date uniqueness. No persistence constructor, serializer, repository or migration is introduced here.
- Read the loaded Plan and selected reference states under the committing writer transaction; compare the existing loaded token before storing the resulting aggregate version. Domain mutations are candidates until commit, and their counters must not be reported as committed before readback.
- Revalidate Account currency/name/update and Instrument state against the user's selection, as well as selected Setup/Rule versions; snapshot creation proves only the supplied objects matched, not that a previously read catalog is still current in SQLite.
- Atomically persist the Draft/Ready transition and full Ready payload. Roll back on validation/conflict/cancellation/storage failure, preserve caller content, and return successful saved status only after commit/readback.
- Retain all Ready revisions independently of mutable catalogs and the aggregate's latest pointer. History/selected-source reads must be bounded and deterministic. Persist derivation and source-Draft version without rewinding Ready numbering.
- Preserve all new content in backup/restore/export and validate restored snapshot chronology/identity. Existing backup and export contracts are unchanged in M21.2 because no persisted data was added.

### Verification — 17 October 2026

| Check | Result |
| --- | --- |
| Focused Trading Plan Domain | **67 passed / 0 failed / 0 skipped** |
| Focused Trading Plan Application | **25 passed / 0 failed / 0 skipped** |
| New M21.2 cases | **45** (43 Domain, 2 Application); prior 47 remain green |
| Complete Domain Release | **557 passed / 0 failed / 0 skipped** |
| Complete Application Release | **1,001 passed / 0 failed / 0 skipped** |
| Final Release solution build | **0 warnings / 0 errors**, 7.63 seconds |
| EF model consistency | No changes since the last migration; design-time factory uses in-memory SQLite |
| Whitespace / documentation | `git diff --check` and new-file whitespace passed; **147** local links resolved |

Commands actually run:

```powershell
dotnet test tests/PersonalTradingJournal.Domain.Tests/PersonalTradingJournal.Domain.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~TradingPlans --logger "trx;LogFileName=domain.trx" --results-directory artifacts/m21-2/final-focused
dotnet test tests/PersonalTradingJournal.Application.Tests/PersonalTradingJournal.Application.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~TradingPlans --logger "trx;LogFileName=application.trx" --results-directory artifacts/m21-2/final-focused
dotnet test tests/PersonalTradingJournal.Domain.Tests/PersonalTradingJournal.Domain.Tests.csproj -c Release --no-build --no-restore --logger "trx;LogFileName=domain.trx" --results-directory artifacts/m21-2/regression
dotnet test tests/PersonalTradingJournal.Application.Tests/PersonalTradingJournal.Application.Tests.csproj -c Release --no-build --no-restore --logger "trx;LogFileName=application.trx" --results-directory artifacts/m21-2/regression
dotnet build PersonalTradingJournal.sln -c Release --no-restore
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure/PersonalTradingJournal.Infrastructure.csproj --context JournalDbContext --configuration Release --no-build
git diff --check
```

Coverage includes complete and stand-aside Ready transitions, incomplete readiness, structured checklist invalidation, exact no-ops, explicit confirmation, immutable nested inputs, multiple revisions derived from earlier history, UTC chronology, stale/repeated transitions, independent catalog edits/archiving, same-name replacements, missing/extra/duplicate references, selected revision mismatch and bounded capture. All test data is synthetic; no installed journal or provider was accessed. The complete Desktop/Infrastructure gate, persistence/restart, renders, live interaction, Review integration and matching CI are not claimed for this Domain-only slice. M21.2 is locally implemented; M21.3 remains next, and full M21 acceptance remains pending.

Exact M21.2 changed/new files (12):

- `README.md`
- `docs/architecture.md`
- `docs/trading-plan.md`
- `src/PersonalTradingJournal.Domain/TradingPlans/TradingPlanContent.cs`
- `src/PersonalTradingJournal.Domain/TradingPlans/TradingPlan.cs`
- `src/PersonalTradingJournal.Domain/TradingPlans/TradingPlanReadyRevision.cs`
- `src/PersonalTradingJournal.Domain/TradingPlans/TradingPlanReferenceSnapshot.cs`
- `src/PersonalTradingJournal.Application/TradingPlans/TradingPlanContracts.cs`
- `tests/PersonalTradingJournal.Domain.Tests/TradingPlans/TradingPlanFixture.cs`
- `tests/PersonalTradingJournal.Domain.Tests/TradingPlans/TradingPlanRevisionTests.cs`
- `tests/PersonalTradingJournal.Domain.Tests/TradingPlans/TradingPlanSnapshotTests.cs`
- `tests/PersonalTradingJournal.Application.Tests/TradingPlans/TradingPlanRevisionContractTests.cs`

## M21.1 — scope, identity and behavioral contracts

The first dependency slice establishes pure Domain/Application contracts. It does **not** yet replace the Desktop placeholder, write plans, create a migration, or add plan evidence to reviews. Existing Accounts, Instruments, Trading Setups/Playbooks and global Rules remain the only catalogs. The accepted M20 implementation and its separate Strategy scope decision are unchanged.

Repository inspection began on clean `develop`, `c76d819541e0b47341fc913518744c8c9410c7df` (`Merge pull request #23 from yovko93/develop`). That history includes `9eb002a` (the M20 coverage-sort correction). This is local merge-history evidence, not a fresh CI acceptance claim.

### Identity and time

- One logical Plan has a stable non-empty Plan ID and the unique key **(exact Account ID, New York calendar date)**. No All-accounts or null-scoped Plan exists. A same-name replacement Account is a different scope.
- Session is metadata: Asia, London, New York or Other. A Draft may leave it unselected. Changing session or adding Instruments cannot bypass account/date uniqueness.
- The default date comes from injected `TimeProvider.GetUtcNow()` through the existing `TradingTimePolicy.ConvertUtcToTradingTime`. A user-selected date is captured in the write command; saving does not recalculate today from the machine's timezone.
- Audit and Ready timestamps must be canonical UTC. Market/session labels do not redefine a trading day. Later Trade/Review membership continues to use the application's New York closure-date convention; a cross-midnight Trade is not counted twice.
- Ready time strictly before a Trade's entry time proves only that the record existed before entry. Equal/later timestamps, missing entry times and a backdated selected Plan date do not establish prior preparation. Association does not prove adherence.

### Draft and Ready lifecycle

| Operation | Contract |
| --- | --- |
| Create Draft | Exact account/date must be absent. Empty/incomplete content is allowed. New Plan identity and initial optimistic version; no Ready revision yet. |
| Save Draft | Require loaded Plan ID/version and Draft status. Preserve incomplete content. Advance the optimistic version only after a successful commit. |
| Mark Ready | Require Draft status, valid required content, all five explicit confirmations and revalidated references. Atomically append the next immutable Ready revision and advance the optimistic version. |
| Start Draft | Explicitly derive from an existing Ready revision of the same Plan; retain the latest Ready revision independently. Do not overwrite an already working Draft. |
| Copy forward | A new target account/date must be absent. An existing target is opened or reported as a conflict; never overwritten. Reset daily observations and every confirmation. |

The **save version** and **Ready revision number** are distinct. Ready revisions are addressed by `(PlanId, positive revision)` and allocated monotonically from the maximum ever issued; never reused, renumbered or edited in place. A Draft derived from an older Ready revision does not replace the latest Ready revision or rewind numbering. Starting a new Draft resets confirmations. Draft saves do not claim to be Ready history. Existing saved Reviews keep their exact captured Ready revision even after later Plan changes.

`TradingPlanWriteCommand.CheckTransition` is a pure preliminary decision: fresh state is required under the future SQLite writer reservation. A permitted transition is **not** a successful write. Content validation, reference validation, unique indexing, transaction rollback, cancellation boundaries, atomic append and verified readback are mandatory work for M21.2–M21.3, not guarantees implemented by this contract-only slice. An application message may say Saved only after committed readback. Exceptions, validation and conflicts must retain the editor's raw input and images.

Write outcomes distinguish Success, Invalid, Conflict, Target already exists, Reference unavailable, Reference changed, Cancelled and Storage failure. A stale or duplicate submission cannot silently edit Ready content. No provider calls, automatic trading checks or broker enforcement are introduced.

### Structured content and readiness

The bounded immutable authoring values preserve exact text, whitespace and decimal values. They defensively copy collections and reject duplicate Instrument, Scenario or Rule IDs. A Scenario must belong to an Instrument context included in that Plan. Scenario ordering follows the supplied collection. Its ID is local to its containing Plan; retaining an ID during copy-forward does not make two Plans share mutable content.

| Content | Draft | Ready |
| --- | --- | --- |
| Exact Account/date | Required identity | Required identity; available active Account checked at commit |
| Session and Today's Focus | May be empty | Selected session and meaningful focus required |
| Instrument contexts | May be empty | At least one context; exact existing active identities |
| Opportunity plan | May be incomplete | Bias and market thesis for every context, plus at least one complete scenario |
| Scenario | Name/direction/IF/THEN/INVALID IF may be incomplete | Every included scenario requires all five; Long/Short reuse `TradeDirection`; a second/opposite-direction scenario is optional |
| Stand-aside-only plan | May be incomplete | Explicit StandAside intent and reason; no trading scenarios required or allowed; no invented thesis, bias or levels required |
| Price levels | Optional | Optional; blank remains null, never zero |
| Risk limits | Optional | Optional; user explicitly confirms review of any unset limits |
| News/restricted-time notes | Optional manual text | Optional manual text; news checklist confirmation still required |
| Playbook and Rule links | Optional | Optional; all supplied links must validate at commit |
| Checklist | Any explicitly confirmed subset | All five checks required |

An opportunity plan may include stand-aside restrictions without changing its intent. A stand-aside-only plan cannot simultaneously advertise trading scenarios. A context can be watched without its own scenario, but an opportunity Plan must contain at least one scenario overall.

Bounds: 500 UTF-16 code units for Focus; 4,000 for each long-text field; 120 for a Scenario name; 12 Instrument contexts, 24 Scenarios and 50 linked Rules per Plan. Enumeration stops at limit + 1 and rejects excess rather than truncating. Optional levels are exact nullable decimals, including a deliberately entered zero or negative level; no instrument tick rounding, FX conversion or economic calculation occurs. Risk amounts and maximum Trades must be nonnegative; a specified consecutive-loss stop must be positive. Zero monetary risk or zero maximum Trades explicitly permits none; null means unset. Amounts will be labeled in the exact Account's captured currency, with no cross-currency total.

Malformed typed numeric text is not coerced into these Domain values. M21.4/Desktop must retain the raw text and show validation. Saving an incomplete Draft is different from accepting malformed values.

The five checks are user confirmations: context/levels, scenarios or stand-aside decision, risk limits, news calendar, mindset/focus. Populated fields never auto-confirm them. On an actual semantic edit, the working Draft invalidates:

| Changed content | Confirmation reset |
| --- | --- |
| Session or Instrument set | All five |
| Focus | Mindset/focus |
| Context, bias or levels | Context/levels and scenarios |
| Scenarios, linked Setup or stand-aside decision | Scenarios |
| Risk limits or linked Rules | Risk limits |
| News/restricted-time notes | News and scenarios |

Navigation, focus and an unchanged assignment are not semantic edits. The editor and committing use cases must apply this policy to the working Draft; earlier Ready confirmations remain immutable. Progress counts only explicit known flags; unknown flags/enums are rejected.

### References, deletion and history

Selection tokens reuse exact existing source IDs: a Playbook link retains its expected revision and Setup update timestamp; a Rule link retains its expected revision. These are **selection tokens, not historical captures**. M21.2 must freeze the referenced Setup name, relevant guidance and exact revision, Rule name/criterion and revision, Account name/currency, and Instrument labels in each Ready revision. No reference may be resolved by display name.

Fresh create/copy/Ready operations require an active Account and Instruments, active linked Playbook Setups at the selected revision, and active global Rules at the selected revision. Changed, inactive, unavailable and legacy-scoped references are explicit states, not silent substitutions. New Plan operations cannot broaden a legacy scoped Rule into a global one. A Draft with a now-unavailable optional link must retain its authored text, offer relink/remove, and explain why finalization is blocked. Existing Draft content may be saved without inventing replacement catalog content; the persisted availability warning must be resolved before Ready. An unavailable/inactive owning Account permits historical reading only until it is explicitly reactivated; a removed Account is never recreated automatically.

Frozen Ready content must remain readable after source edits/archiving/deletion. Future persistence must retain original identity/labels independently of mutable catalogs and must not cascade Plan history deletion. Plan links do not authorize deletion of shared images or alteration of existing Trade/Playbook retention policy. No screenshot attachments are added to Plans in this contract; there are no new image paths or binary transfers.

History is bounded to 1–100 entries per read (default 20), exact Account and inclusive selected date range, optionally Draft/Ready. Ordering is date descending, stable Plan ID, Ready revision descending. Later reads need a cursor carrying that full order and a separate representation for the working Draft. Historical Ready views are read-only and visually distinct; an explicit Start Draft is required to edit derived content.

### Copy forward

`TradingPlanContent.CopyForward` retains selected session, Focus, Instrument identities, scenario structure/order/text and selected Playbook tokens, planned limits and selected Rule tokens. It clears bias, market thesis, all six price levels, dated news/restriction notes and stand-aside notes, and resets all five confirmations. StandAside intent is retained but its daily reason must be re-entered. Retained scenario text may itself mention a daily condition; the user must review it before reconfirming. Copy never carries Ready status.

The future use case must load the exact authoritative source Ready revision, apply this reset, revalidate all references, and enforce target uniqueness transactionally. Caller-supplied content alone cannot prove a source revision or authorize overwriting the target. The pure method performs no catalog reads or writes.

### Synthetic NQ example

Account “Synthetic USD”, New York date 17 October 2026, New York session; Focus “Wait for confirmation. One setup at a time.” An NQ context has Conditional bias and a manually entered thesis. Previous Day High 20,000.25 is an illustrative nullable decimal, not downloaded market data. One Long Primary scenario says IF price reclaims that level, THEN wait for a retest, INVALID IF acceptance returns below it. A Playbook link may identify the existing Setup's exact revision; it is optional and does not assert compliance. Planned risk 50 USD/trade, daily limit 150 USD, maximum 3 Trades and stop after 2 losses are preparation, not enforcement. Every check is user-confirmed. Alternatively, StandAside with “Missing confirmation; no entries today” requires no invented opportunity or numerical limit.

### Verification and implementation sequence

M21.1 adds focused Domain/Application tests for exact identity, New York year/midnight/DST defaults, selected-date stability, incomplete Draft versus Ready, stand-aside, scenario ownership, bounded immutable inputs, decimals/nulls, checklist invalidation, copy resets, version/source tokens, transition conflicts, Ready protection, history bounds and before-entry timing. These are synthetic in-memory tests, not SQLite restart/UI acceptance.

Final local verification for this slice (17 October 2026):

| Check | Observed result |
| --- | --- |
| New Domain/Application contract cases | 47 passed: 24 Domain and 23 Application, included in the complete project runs below |
| Complete Domain Release tests | **514 passed / 0 failed / 0 skipped** |
| Complete Application Release tests | **999 passed / 0 failed / 0 skipped** |
| Release solution build | **0 warnings / 0 errors**, 26.09 seconds |
| EF model consistency | No changes since the last migration |
| Whitespace and documentation | Tracked and new-file whitespace passed; 145 local links resolved across the three changed documents |
| Complete Desktop/Infrastructure Release gate | Not run for this contract-only slice; required again after M21 integration |
| UI renders, restart, live interaction, matching CI | Not applicable to this contract-only implementation / remain pending for integrated M21 |

Evidence: `artifacts/m21-1/regression/domain.trx` and `application.trx`. Initial focused runs covered 24 Domain and 21 Application cases; two additional transition cases were then added and passed in the complete Application run. Two initial test-fixture assertions were corrected (a reusable fixture intentionally contained a Rule link; immutable-array value comparison required sequence comparison); the final complete runs above have no failures. No old M20 test total is reused as M21 evidence. All nine files remain uncommitted, so no matching CI run exists for this change set.

| Slice | State |
| --- | --- |
| M21.1 Scope/identity/behavior | Implemented and locally verified as above |
| M21.2 Preserved Ready revisions | Implemented in Domain; see the current M21.2 section above |
| M21.3 Persistence | Pending additive migration, transactions, readback, cancellation, DI, backup/export coverage |
| M21.4–M21.6 Preparation/scenarios/risk | Authoring value/readiness contracts defined; Desktop workflows pending |
| M21.7 Session Brief | Pending full-width three-section WPF workspace |
| M21.8 History/copy/navigation | Policies defined; durable operations and guarded UI pending |
| M21.9 Daily Review/AI evidence | Pending versioned frozen revision integration; existing reviews unchanged |
| M21.10 Acceptance | Pending integrated gate, renders, restart and live interaction |

M21 is **not complete** at this checkpoint. No Trading Plan persistence, visual/live acceptance, matching CI or new full-gate result is implied by the first contract slice.

Exact M21.1 changed/new files:

- `README.md`
- `docs/architecture.md`
- `docs/trading-plan.md`
- `src/PersonalTradingJournal.Domain/TradingPlans/TradingPlanIdentity.cs`
- `src/PersonalTradingJournal.Domain/TradingPlans/TradingPlanContent.cs`
- `src/PersonalTradingJournal.Domain/TradingPlans/TradingPlanReadiness.cs`
- `src/PersonalTradingJournal.Application/TradingPlans/TradingPlanContracts.cs`
- `tests/PersonalTradingJournal.Domain.Tests/TradingPlans/TradingPlanContractTests.cs`
- `tests/PersonalTradingJournal.Application.Tests/TradingPlans/TradingPlanContractTests.cs`
