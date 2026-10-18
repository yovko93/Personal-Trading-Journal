# Trading Plan

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
