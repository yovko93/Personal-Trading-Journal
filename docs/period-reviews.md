# Weekly and Monthly Review — M19.1–M19.7

## Manual ChatGPT workflow — 15 October 2026

This supersedes visible direct Generate/API actions without rewriting saved v1/v2 reviews. **Prepare ChatGPT Prompt** reads one fresh selected/prior-period snapshot and freezes its exact New York boundaries and Account scope. `manual-review-prompt.v1` carries the existing strict response schema and compact `period-coaching.v2` projection. Complete aggregates cover all included Trades and preserve Account/currency, strict Net, missing data, Rule outcome/provenance and comparison boundaries. Bounded individual examples and omitted details remain explicitly identified. Only supplied sources may be cited; the full immutable local evidence is retained.

**Copy Prompt** and **Paste ChatGPT Response** touch the clipboard only on explicit clicks. The prompt may contain sensitive Trade/Journal information; PTJ performs no upload and needs no API key. Inspect its exact preview and byte count first. A single JSON response or optional Markdown `json` fence is accepted. The original identity/version, five required sections, citation/basis rules, three-action maximum and response bounds still apply. Validation displays Strengths, Areas for Improvement, Mistakes, Progress and Action Plan cards without creating history. **Save Review** revalidates and atomically saves the frozen evidence/response. Provenance is **Pasted external AI response**; originating service/model, token usage, cost and truth of claims are unverified. The timestamp records local preparation, not claimed external generation.

Weekly and Monthly have separate pending slots, independent of browsing selection. Navigation and restart retain prompts and explicitly kept response text. Unsaved response text blocks leaving until **Keep response draft** or confirmed **Discard pending work**. A stable saved identity prevents duplicate rows after a save interruption. Concurrent working-copy changes fail rather than overwrite. Cancellation, malformed JSON and invalid citations never produce an accepted historical review.

The existing 2 MiB local snapshot / 256 KiB projection/source limits remain. The whole prompt is bounded at **300,000 UTF-8 bytes**; external ChatGPT limits may be smaller. Responses remain bounded at **65,536 UTF-8 bytes**. No silent truncation, automatic retries, provider fallback or provider requests occur. Pending unencrypted files in `pending-review-prompts` are deliberately excluded from portable backup/export/restore, while completed reviews retain existing portability and v1/v2 compatibility. See [Daily workflow](daily-review.md#manual-chatgpt-workflow--15-october-2026) for durable-storage, cancellation and corruption behavior. No schema/export change is required. Final automated results and remaining live/CI gaps are recorded below; M19.8 is not acceptance-tested here.

### Manual-workflow verification

Focused checks passed **48/48** (15 Infrastructure and 33 Desktop/navigation/WPF), including Daily/Weekly/Monthly restart, exact/aggregate frozen scope, clipboard failure, malformed/fenced JSON, citation validation, duplicate-save reconciliation, navigation guards and completed-snapshot portability. The complete `pwsh -File scripts/Test-Release.ps1 -ResultsDirectory artifacts/manual-review/release` gate passed **4,335/4,335**, zero failures/skips: Domain **490**, Application **880**, Infrastructure **1,347**, Desktop **1,618** (88 exclusive WPF + 1,530 parallel). Calendar native **82.577 s / 37.423 s headroom** against 120 seconds; Journal editor child **12.374 s**. Existing deadlines and concurrency scheduling are unchanged; no Calendar/Journal failure occurred.

Release build: **0 warnings/errors**. EF model consistency: no changes; no migration. Tracked and new-file whitespace checks passed. Results: `artifacts/manual-review/release/release-20261015-151936-5e4d7531f5124736b29d5442eaa668fe/summary.json`. The 28 automated renders in `artifacts/manual-review/renders-final/` cover themed normal/narrow240 prompt and report states; representative Light Weekly cards, Dark narrow Monthly prompt and Light narrow Daily cards were visually inspected. Wrapped actions and reading sections remain reachable. These are offscreen WPF observations, not actual clipboard, mouse/keyboard or screen-reader acceptance.

The [scenario matrix](daily-review.md#manual-workflow-verification) distinguishes synthetic automated evidence from remaining live gates. New manual paths assert **zero provider requests**; retained legacy provider compatibility tests use fake HTTP only. No live/paid request or production data was accessed. Real ChatGPT output quality and live UI/clipboard remain unverified. The changes are uncommitted, so there is no matching resulting commit for GitHub Actions; local green is not CI acceptance. M19.8 has not started.

### Manual-workflow file inventory

The following 29 files implement and verify the manual workflow (starting clean `develop` at `31ffd0ecabea66e9512b66a3a6659ba94373015c`; no commit/push/merge):

* `README.md`
* `docs/backup-restore.md`
* `docs/daily-review.md`
* `docs/period-reviews.md`
* `src/PersonalTradingJournal.Application/DailyReview/Coaching/SavedCoachingAnalysis.cs`
* `src/PersonalTradingJournal.Application/PeriodReviews/Coaching/SavedPeriodReview.cs`
* `src/PersonalTradingJournal.Desktop/App.xaml.cs`
* `src/PersonalTradingJournal.Desktop/ViewModels/DailyReview/DailyReviewViewModel.cs`
* `src/PersonalTradingJournal.Desktop/ViewModels/DailyReview/ReviewPresentation.cs`
* `src/PersonalTradingJournal.Desktop/ViewModels/MainWindowViewModel.cs`
* `src/PersonalTradingJournal.Desktop/ViewModels/PeriodReviews/PeriodPresentation.cs`
* `src/PersonalTradingJournal.Desktop/ViewModels/PeriodReviews/PeriodReviewViewModel.cs`
* `src/PersonalTradingJournal.Desktop/Views/DailyReview/DailyReviewView.xaml`
* `src/PersonalTradingJournal.Desktop/Views/PeriodReviews/PeriodReviewView.xaml`
* `src/PersonalTradingJournal.Desktop/Views/Settings/SettingsView.xaml`
* `tests/PersonalTradingJournal.Desktop.Tests/DailyReview/DailyReviewViewTests.cs`
* `tests/PersonalTradingJournal.Desktop.Tests/Navigation/DailyReviewAcceptanceTests.cs`
* `tests/PersonalTradingJournal.Desktop.Tests/PeriodReviews/PeriodViewTests.cs`
* `src/PersonalTradingJournal.Application/DailyReview/Coaching/ManualReview.cs`
* `src/PersonalTradingJournal.Desktop/ViewModels/DailyReview/ManualReviewViewModel.cs`
* `src/PersonalTradingJournal.Desktop/Views/DailyReview/ManualReviewView.xaml`
* `src/PersonalTradingJournal.Desktop/Views/DailyReview/ManualReviewView.xaml.cs`
* `src/PersonalTradingJournal.Desktop/Views/DailyReview/WpfReviewClipboard.cs`
* `src/PersonalTradingJournal.Infrastructure/DailyReview/Coaching/ManualReviewService.cs`
* `tests/PersonalTradingJournal.Desktop.Tests/DailyReview/ManualReviewViewTests.cs`
* `tests/PersonalTradingJournal.Desktop.Tests/DailyReview/ManualReviewWorkspaceTests.cs`
* `tests/PersonalTradingJournal.Desktop.Tests/Navigation/ManualReviewNavigationTests.cs`
* `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/ManualReviewPortabilityTests.cs`
* `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/DailyReview/ManualReviewTests.cs`

## M19.7 correction — compact projection and report-first layout

### Reproduction and measured cause

Starting state: clean `develop`, HEAD `58f36ac8eef7dabd2812f802d03beb21d89552ee`. No production journal was opened. A deterministic synthetic week with 25 closed USD Trades, one execution fact per Trade and no Journals reproduced **TooLarge before HTTP**. The v1 packet was 214,107 UTF-8 bytes: comparison 143,322; calculated statistics 27,815; Trades 24,650; separately repeated executions 10,825; patterns 52; the remainder was identity, coverage and framing. Repeated complete group statistics and source memberships in comparisons dominated, not fixed instructions or schema. This fixture models the reported population, not the user's private 25 records.

| Local Groq estimate | v1: 25 Trades, no prior Trades | v2: 25 Trades, no prior Trades | v2: 25 + 25 prior Trades |
|---|---:|---:|---:|
| Instructions | 445 | 509 | 509 |
| Strict response schema | 626 | 626 | 626 |
| Evidence | 85,033 | 2,223 | 3,215 |
| Request framing | 83 | 83 | 83 |
| 10% safety margin | 8,619 | 345 | 444 |
| Reserve | 512 | 512 | 512 |
| Total estimated input | **95,318** | **4,298** | **5,389** |
| Configured input / output | 6,800 / 1,000 | unchanged | unchanged |
| HTTP / validated result | 0 / TooLarge | 1 fake / Success | 1 fake / Success |

These are the existing tokenizer's decoded-message/schema estimates, not live Groq billing or remaining organization TPM. No provider limit, timeout or token estimator was raised. The response remains bounded by 1,000 Groq output tokens; a genuinely incomplete or over-budget response still fails validation.

### v2 projection and immutable local snapshot

* `PeriodCoachingPacket.Json` retains every original local source, execution, Journal revision, frozen Playbook association, assessment/correction, calculated metric and contributing membership, plus the exact `providerProjection`. Local complete snapshots are bounded at **2 MiB**, with **10,000 source records** and the existing calculation limits. Individual unaggregated sources remain bounded at **256 KiB**. This local-storage limit is distinct from the unchanged provider budget; it accommodates both selected and prior periods without putting repeated memberships on the wire.
* `ProviderJson` is the only period payload supplied to either provider. Its version/identity bind the complete local snapshot, projection policy, selected examples and coverage. Both adapters use the dedicated period v2 schema. Daily Review is unchanged.
* Every closed Trade and excluded activity record contributes to complete calculated population/coverage and exact Account/currency statistics. Gross/strict Net metrics retain nulls, known-subset labels, coverage, means and extremes. No FX or economic recalculation is introduced. Comparison changes retain missing-baseline/coverage statuses. Breakdown membership counts are supplied; detailed breakdown metrics and memberships stay local.
* Rule summaries preserve exact revision, lifecycle/version status, Trade/day target, scope, denominator, compliant/violation/Cannot-evaluate/not-assessed counts, manual/automatic origin counts and unavailable/changed-source counts. Assessment examples identify whether they are effective for current coverage; an older correction is not presented as the latest assessment. Mistake frequencies retain Account boundaries and are observations, not causes.
* Individual examples are the **first two ordinal source IDs per kind and period**, for Trades, current Journals, frozen Playbook associations, Rule revisions and assessments. Selection is deterministic, not representative or performance-based. A Trade example includes its facts/classifications, not execution details. Coverage explicitly counts complete local sources versus supplied examples; an absent kind means no records, not an empty field. Journal absence is also explicit. No screenshot binaries, paths or Trade notes are sent.
* The provider is told what remains local and may not claim to inspect omitted individual Trades, executions, observations or chart images. Text is untrusted evidence, never instructions. Aggregate IDs support only supplied aggregate facts. **The validator rejects individual IDs that exist locally but were not supplied.** It still cannot prove that an AI claim is true or that wording faithfully describes a citation.
* A provider-budget rejection reports the estimated input and configured limit, with zero requests/saves; it does not suggest narrowing an already exact scope. Local snapshot/projection safety-limit rejection is separate. Nothing is silently truncated and there is no retry/fallback. Long selected examples, many groups or large snapshots can still be too large. The 31-Trade synthetic SQLite case now succeeds; a 300-Trade full snapshot fails its local bound without a request or history row.
* Saved v1 JSON is not rewritten or revalidated under v2. The version-specific reader supports both. New reviews atomically save the full local snapshot **and** exact supplied projection/response; saved citations are restricted to the stored projection catalog. Backup/export already carry opaque versioned saved-period JSON, so no migration, export field or schema compatibility change is required.

### Reference and automated visual observations

Compared with the supplied `review.png`: a compact date/Account toolbar replaces large navigation buttons; the selected New York period sits at the top beside Generate on wide windows. Strengths and Areas for Improvement use separate green and amber finding cards with a bold first sentence and readable explanation. Mistakes Observed, Progress and the short Action Plan follow. The image's mascot and automatic-update promise were not copied. Provider/privacy details, statistics and saved history are collapsed below the report; coverage warnings remain available and unknown Net is never a total.

Compiled WPF checks cover pre-generation, a successful report with three findings in each primary column, TooLarge, database loading, generation progress/cancellation, saved history, citations and scrolling. Light/Dark renders at **1100 DIP / 96 DPI** and **480 DIP / 240 DPI** are in `artifacts/m197-correction-renders/`. Each state has a named PNG; the two-column report stacks at narrow width, actions wrap, and there is one page scroll viewer. Contrast assertions require at least 4.5:1 for card body and accent headings. These are automated offscreen renders, **not live mouse/keyboard or screen-reader acceptance**.

### Verification and remaining gates

Final focused checks: **328/328** — Application **64**, Infrastructure **39**, Desktop/navigation **225**. Four compiled-WPF cases also regenerated **28** final-state PNGs. Initial sandboxed broader navigation checks had two Daily Review acceptance failures: disposable saved credentials were unavailable before fake transport. The same tests in the normal Windows-user context passed (225/225 focused, and in the complete gate). No credential/provider code, deadline or assertion was weakened; this sandbox limitation is not reported as a product fix.

The final complete `pwsh -File scripts/Test-Release.ps1 -ResultsDirectory artifacts/m197-correction-release` gate passed **4,305/4,305**, zero failures/skips: Domain **490**, Application **880**, Infrastructure **1,332**, Desktop **1,603** (88 exclusive WPF + 1,515 parallel). Calendar native child **58.398 seconds**, **61.602 seconds headroom** against its unchanged 120-second deadline; Journal child **10.918 seconds** including startup. No Calendar focus failure or Calendar/Journal timeout occurred. Results: `artifacts/m197-correction-release/release-20261015-121147-3f4dec736af5415dbbba73211f041dd0/summary.json`. Release build **0 warnings/errors**; EF `has-pending-model-changes` reports no changes; tracked and new-file whitespace checks passed.

The final synthetic 25-Trade local snapshot is **222,548 bytes**, versus **8,359 bytes** actually supplied; with 25 prior Trades these are **419,207** and **11,481 bytes**. Measurements are retained in `artifacts/m197-correction-focused/period-infrastructure-final.trx`. Eight additional cases cover the new boundaries; existing assertions remain in the complete gate. All 19 listed files remain uncommitted on the starting `develop` HEAD. There is no resulting commit for GitHub Actions to verify; no matching CI acceptance is claimed.

Selected render evidence: [Light report](../artifacts/m197-correction-renders/period-Light-1100-96-cards.png), [Dark report](../artifacts/m197-correction-renders/period-Dark-1100-96-top.png), [narrow/240-DPI report](../artifacts/m197-correction-renders/period-Dark-480-240-cards.png), [TooLarge](../artifacts/m197-correction-renders/period-Light-480-240-too-large.png). Visual inspection confirmed separate finding tiles, readable themed text, stacked narrow cards, reachable cancellation/actions, concise failure feedback without the introduction panel, and collapsed secondary sections. Real keyboard/mouse, screen-reader behavior, actual provider output quality/token accounting and matching CI remain unverified.

Synthetic SQLite-backed week/month tests exercise 25 Trades with 50 executions and a missing commission: explicit Generate → real Groq wire adapter with fake HTTP → strict validation → one atomic saved row → five visible report sections → immutable reload. An unsupplied individual citation must yield no saved row. Application coverage includes deterministic projection, mixed currencies, nullable Net, correction markers, prompt-like text, old v1 display, unknown citations and cancellation. No live/paid request or production access is authorized or performed. Matching CI cannot verify uncommitted changes; M19.8 acceptance remains out of scope.

### Exact changed files

* `README.md`; `docs/period-reviews.md`
* `src/PersonalTradingJournal.Application/PeriodReviews/Coaching/PeriodCoachingGeneration.cs`
* `src/PersonalTradingJournal.Application/PeriodReviews/Coaching/PeriodCoachingPacket.cs`
* `src/PersonalTradingJournal.Application/PeriodReviews/Coaching/PeriodCoachingProjection.cs` (new)
* `src/PersonalTradingJournal.Application/PeriodReviews/Coaching/PeriodCoachingResponse.cs`
* `src/PersonalTradingJournal.Infrastructure/DailyReview/Coaching/GroqCoachingProvider.cs`
* `src/PersonalTradingJournal.Infrastructure/DailyReview/Coaching/PeriodCoachingWireContract.cs`
* `src/PersonalTradingJournal.Desktop/Resources/Themes/LightTheme.xaml`
* `src/PersonalTradingJournal.Desktop/Resources/Themes/DarkTheme.xaml`
* `src/PersonalTradingJournal.Desktop/ViewModels/PeriodReviews/PeriodPresentation.cs`
* `src/PersonalTradingJournal.Desktop/ViewModels/PeriodReviews/PeriodReviewViewModel.cs`
* `src/PersonalTradingJournal.Desktop/Views/PeriodReviews/PeriodReviewView.xaml`
* `tests/PersonalTradingJournal.Application.Tests/DailyReview/PeriodCoachingTests.cs`
* `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/DailyReview/PeriodCoachingProviderTests.cs`
* `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/DailyReview/PeriodCoachingSqliteTests.cs`
* `tests/PersonalTradingJournal.Desktop.Tests/PeriodReviews/PeriodSqliteTests.cs`
* `tests/PersonalTradingJournal.Desktop.Tests/PeriodReviews/PeriodViewTests.cs`
* `tests/PersonalTradingJournal.Desktop.Tests/PeriodReviews/PeriodWorkspaceTests.cs`

## M19.7 — Weekly/Monthly Desktop workspace

### Selection, interpretation and history

The existing Weekly Review and Monthly Review destinations now use distinct ViewModel instances and one shared WPF view. Each initially selects the current New York week (Monday–Sunday) or calendar month and All accounts. Date entry chooses the containing period; previous/next period, Today and Refresh are explicit local operations. Returning to each destination retains its own selected period, Account and history page. No load, navigation, scope change, Settings change, history operation or Refresh generates AI content.

The scope summary shows New York boundaries, original Account label, closed-Trade/active-day/Journal counts and relevant coverage warnings. Current statistics, user-written Journal observations, previous-period comparisons, Rule coverage and recorded mistakes are separate collapsed sections. Account/currency groups reuse M19.2 and Daily presentation; incomplete Net remains unavailable, not a known-subset total. Rules retain exact revision/target/scope denominators, distinguish manual/calculated origin and explicitly label compliant, confirmed violation, Cannot evaluate and not assessed. Changed/unavailable targets and unattributable retained assessments are not silently counted as current compliance.

Saved AI interpretation is presented in Strengths, Areas for Improvement, Mistakes Observed, Progress and Action Plan cards (at most three steps), with generation time in New York including UTC offset, provider/model and available token usage. Monetary cost remains unknown without verified pricing. Two balanced columns become one below 840 DIP of usable card width; prose wraps and the page has one vertical scroll surface. Theme resources, accessible headings, labeled controls and keyboard-focusable actions are reused. No raw packet/JSON/technical-ID panel is added.

Saved report details read only the stored `period-coaching.v1` packet and response, not current sources. Expandable citations open the exact supplied Trade/Journal, frozen Playbook text or Rule-assessment context. Playbook guidance is labeled as captured association, not proof of adherence; no chart image is implied. Assessment detail retains its recorded rule revision, outcome, origin, explanation, timestamp and target availability. Optional **Open current Trade / Journal** links use existing guarded navigation; Journal navigation additionally verifies the original ID at its exact saved date/scope and refuses a moved/deleted/same-scope replacement. Captured Playbook and assessment details are read-only within the report, not a shortcut to create a new assessment or substitute current guidance. Saved evidence remains readable when a live source is unavailable.

History is ten rows per page and exact-scope: All accounts history contains aggregate reviews, not every Account's individual history. Account discovery combines live/inactive Accounts with **25 saved scopes per page** for the selected period; More/Previous saved scopes make historical/unavailable identities discoverable without loading all analyses. Deleted Accounts retain their saved labels and original IDs (identity is available in the selector tooltip), never a same-name replacement. Generation is disabled for unavailable scopes; current evidence is queried only under that exact original identity. Empty last history/discovery pages reconcile after deletion. Confirmed Delete removes one saved review only, leaving source data and other history untouched.

### Generation and lifecycle safeguards

Generate captures the selected query, rechecks Account availability and invokes M19.6's fresh evidence → packet → captured provider → validation → atomic save workflow once. The nearby disclosure names the Settings provider and explicitly includes both selected and previous-period Trade facts, Journal observations, Playbook text and Rule evidence. An expandable explanation covers sensitive content, charges, Groq limits, no images/retries/fallback and local cancellation limits. Missing/unreadable credentials expose Configure AI in Settings; opening Settings performs no request.

Progress and Cancel actions are shown only for relevant operations. Duplicate Generate invocations are suppressed. Date/scope changes, Refresh and accepted navigation cancel the local request; version checks reject late reads/details/results, including a noncooperative fake provider. A save committed before cancellation remains discoverable in matching history; cancellation cannot promise to undo a provider charge or committed transaction. Navigation/window-close veto keeps the request active; cancellation occurs only when deactivation is accepted. Re-clicking the active destination is a no-op. Confirmed deletion blocks selection/navigation until its outcome is known. Failure messages come from the existing sanitized service outcomes; database failure never exposes an unsaved report as historical success. Complete oversized evidence remains rejected without truncation or requests.

### Verification and limits

| Scenario | Automated evidence | Live / remaining gate |
| --- | --- | --- |
| Week/month independence; New York midnight/DST defaults; exact/aggregate scope | ViewModel and shell-navigation regressions | Live date picker / keyboard traversal not exercised |
| Explicit-only generation, fresh immutable packet, five cards, token metadata | Fake provider with real M19.5/M19.6 orchestration | No live provider call authorized or made |
| Duplicate clicks; cancel, change selection, Refresh, owner-close/navigation veto; late reads/details | Controlled task-completion regressions, no sleeps/retries | Actual native closing interaction not manually exercised |
| Missing credentials, rate/quota/model/timeout/invalid/incomplete/oversize/storage errors | Fake outcomes and real oversized-packet preflight; zero saved success for failure | Provider/project-specific live behavior unverified |
| Current strict Net and currencies; cannot-evaluate versus not-assessed; frozen Playbook / assessment citations | Presentation regressions using exact recorded source contracts | Citation validation is not proof that an AI claim is true |
| Ten-row history; 26 historical scopes; deletion decline/confirm; shrinking pages | Bounded fake repository tests | Long real-world history performance not manually measured |
| Weekly and monthly save/reopen after Account/Journal deletion and same-name replacement | Two migrated SQLite tests with fresh ViewModels and repository reads; exact saved JSON unchanged | Fresh services/ViewModels, not a launched-app restart |
| Responsive cards, accessible names, action bounds, collapsed/expanded sections, full-page scrolling | Four compiled-WPF cases and twelve PNGs in `artifacts/m197-renders` (Light/Dark; 1100/96 and 480/240) | Automated raster-DPI checks, not physical monitor changes or live screen-reader acceptance |
| Complete Release gate / build / EF / whitespace | 4,297/4,297; zero build warnings/errors; no pending EF changes; whitespace passed | Uncommitted changes cannot have a matching GitHub Actions run |

The first new SQLite report-opening regression exposed a presentation mapping error: stored `DailyJournalRevision.Review` was read as `answers`. The mapping now deserializes that revision contract and preserves absent review fields. An initial new WPF test also deactivated its bound ViewModel off the owning STA during teardown; cleanup now runs on that STA. Render inspection caught and corrected a clipped date label and an unthemed selector. Aggregate assessment and Journal-revision citation labels now identify the saved Account scope explicitly.

A final source-navigation regression reproduced a replacement race (`artifacts/m197-focused/source-race-before.trx`): after the period citation prechecked the original Journal ID, the shell activated its editor using only date/scope, and the independent editor read displayed a newly created replacement. Period navigation now passes the saved ID to the existing editor load-time guard. The regression verifies no replacement text/ID is published and no write occurs. An explicit subsequent date/filter selection clears that old citation constraint, so unrelated Journal navigation still works. Daily's existing navigation path is unchanged. No fix required changing provider, storage, schema, test deadlines or pre-existing assertions.

Focused period + existing shell-navigation checks: **221 passed, zero failed/skipped**, including **34 new M19.7 cases**, `artifacts/m197-focused/period-navigation-final.trx`. Release build **0 warnings/errors**; EF in-memory design-time check reports no pending changes. Two earlier complete gates passed 4,296/4,296 before the added replacement-race regression; they are not substituted for the final run after that correction.

Final complete gate: `pwsh -File scripts/Test-Release.ps1 -ResultsDirectory artifacts/m197-verified-release`, results in `artifacts/m197-verified-release/release-20261015-104038-da3d9e3d274d4c2481b6140c699b77e0/summary.json` and TRX:

| Project | Passed / total | Failures / skips |
| --- | --- | --- |
| Domain | 490 / 490 | 0 / 0 |
| Application | 879 / 879 | 0 / 0 |
| Infrastructure | 1,329 / 1,329 | 0 / 0 |
| Desktop (88 exclusive + 1,511 parallel) | 1,599 / 1,599 | 0 / 0 |
| **Complete set** | **4,297 / 4,297** | **0 / 0** |

Calendar native child: **72.565 seconds**, **47.435 seconds headroom** to its unchanged 120-second aggregate deadline. The parent Journal editor case passed in **11.572 seconds** (includes child startup); no Calendar/Journal timeout or assertion failed. Timings are measurements of this host/run, not guarantees under arbitrary load. Existing scheduling, concurrency, assertions and deadlines were unchanged. Tracked/new-file whitespace checks passed. Initial worktree was clean at `develop` / `6827f0f50c84cc5016c0cd39597c248afde9dde3`; final state is **7 modified + 8 new files**, no staged changes or Git writes. All data and provider responses are synthetic. **Zero live/paid provider requests**, no production journal or credential value accessed. Live Windows mouse/keyboard/screen-reader and real-monitor DPI checks, live provider behavior, and matching CI remain open. Complete large periods can still exceed unchanged packet/provider limits; they are rejected rather than truncated. No M19.8 work.

Exact M19.7 files (paths relative to the repository):

- `src/PersonalTradingJournal.Desktop/App.xaml`
- `src/PersonalTradingJournal.Desktop/App.xaml.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/MainWindowViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Journals/JournalViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/PeriodReviews/PeriodReviewViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/PeriodReviews/PeriodPresentation.cs`
- `src/PersonalTradingJournal.Desktop/Views/PeriodReviews/PeriodReviewView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/PeriodReviews/PeriodReviewView.xaml.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Navigation/MainWindowViewModelTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Navigation/MainWindowPeriodReviewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/PeriodReviews/PeriodWorkspaceTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/PeriodReviews/PeriodSqliteTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/PeriodReviews/PeriodViewTests.cs`
- `README.md`
- `docs/period-reviews.md`

## M19.6 — Saved period reviews and history

### Explicit generation and atomic storage

`GenerateAndSavePeriodReviewService.GenerateAsync` wraps M19.5 without changing Daily generation, provider selection, budgets or validation. It generates at most once, revalidates the complete response against that exact immutable packet, and submits a `PeriodReviewSnapshot` to `IPeriodReviewRepository.SaveAsync`. Snapshot creation never rereads Trades, Journals, Accounts or other mutable sources after generation. The packet's canonical JSON is retained byte-for-byte as text; the validated structured response is serialized separately. Original New York first/last dates and half-open UTC boundaries, week/month kind, exact Account ID or explicit aggregate scope, original display name, UTC generation time, packet ID and contract versions are saved together. Account labels come only from supplied current-period sources, then previous-period sources; unavailable labels remain unknown rather than being replaced by a same-name Account.

One transaction inserts the entire record, including allowlisted provider/model/request IDs and nullable token usage. No raw HTTP diagnostics, credentials, retries or partially successful responses are stored. Failed, invalid, mismatched, oversized or pre-commit cancelled requests produce no row. A failed insert rolls back. Cancellation is checked immediately before commit; once commit begins it is completed without cancellation and a durable save is reported as saved, not falsely reported as cancelled. An ambiguous storage failure gives no usable saved review and advises checking history before another explicit, potentially billable generation. Existing Daily metadata validation was factored into a shared helper without changing its allowlist or stored contract.

### Separate history, identity and retention

- `PeriodReviews` is independent of Daily `CoachingAnalyses`, with no foreign keys or cascades to mutable sources. Historical detail reads return stored versioned JSON, not current sources or a newly validated/reinterpreted packet. Source edits/deletion, provider/key changes and Account replacement cannot rewrite a snapshot.
- History is filtered in SQLite by exact week/month boundaries and exact scope, ordered by generation UTC descending, then immutable review ID ascending. Pages are 1–100 rows (default 20); summaries do not load evidence/response bodies. Count and page share one SQLite read transaction. Separate page requests may observe subsequent writes; cross-page snapshot isolation is not promised.
- **All accounts history means aggregate analyses only**, not a merge of exact-account histories. `BrowseScopesAsync` provides separately paged, deterministic discovery of scopes with saved reviews for the period, including unavailable Accounts. It reads no current Account catalog or evidence payloads; original IDs and the newest saved label distinguish same-name replacements. The later UI must label unavailable identities explicitly.
- Confirmed deletion takes one immutable saved-review ID. Declining is write-free; missing IDs return `NotFound`. Deletion is transactional and cannot affect other analyses, source records or files. No retention schedule or bulk delete is added. Saved snapshots are immutable through the application repository; this does not protect against direct external database editing.

### Portability and confidentiality

Additive migration **`20261015091425_AddSavedPeriodReviews`** is migration 20. Existing Daily analyses and all source tables remain unchanged; normal installed-database migration preserves them. Complete archive v1 already contains the entire validated SQLite snapshot: preflight now reports total AI analyses plus a separate period count, and offline restore verifies both. Synthetic tests cover successful replacement and interrupted replacement recovering the original period history from the retained recovery dataset.

Restore still requires the exact current schema. Archives from migration 19 or earlier are rejected as requiring the deferred supported staged upgrade; adding this migration does **not** make older-schema archives restorable. Portable JSON advances from v8 to **v9**, adding `periodReviews` with every persisted column, exact evidence/response JSON, versions, metadata, Account identity and explicit dates/UTC boundaries. Four CSVs remain unchanged; period details are JSON-only, screenshots remain metadata-only, and logical exports are **not restorable backups**. Older exports retain their original meaning and omit period reviews; no export import is introduced.

Export fields are `id`, `kind` (0 Week, 1 Month), `firstDateNewYork`, `lastDateNewYork`, `fromUtc`, `beforeUtc`, `scopeKind` (0 All accounts, 1 exact Account), nullable `accountId` / `accountDisplayName`, `generatedAtUtc`, `provider`, `model`, `evidenceContractVersion`, `responseContractVersion`, `packetId`, `evidenceJson`, `responseJson` and `metadataJson`. JSON strings retain their versioned inner documents; no source identity, nullable economics or coverage field is flattened into an invented value.

These saved packets may contain sensitive Journal text, Trade facts, Playbook annotations and Rule assessment explanations. The SQLite database, archives and logical exports are **unencrypted**; DPAPI keys, environment credentials, logs and unrelated diagnostics remain excluded. No provider call is made by history, deletion, backup or export.

### Verification and remaining limits

Focused checks passed **625/625**: Application **242**; Infrastructure **383** (378 period/coaching/backup/migration checks, two post-delete rollback cases and three schema-inventory checks). There are **38 new cases**: 22 Application and 16 SQLite. Coverage includes exact/aggregate scope and 20/21/40-row boundaries; count/page consistency during a concurrent commit; source changes while generation is pending; deletion and same-name replacement; fresh context/service reads; duplicate-ID rejection; cancellation and fault injection after INSERT/DELETE; confirmed-delete isolation; previous-schema migration; exact JSON v9 export; successful offline restore and interrupted replacement/recovery. All providers are fake.

The first broad Infrastructure run exposed two outdated initializer migration lists. The first complete gate then finished **4,260 passed / 3 failed / 0 skipped**: the remaining old full migration list, latest-migration assertion and EF entity allowlist had not yet included the new table. These expectations were updated, not removed; their three focused reruns passed. No product lifetime or WPF deadline failure was observed.

The **final complete gate passed 4,263/4,263, zero failures/skips**:

| Project / stage | Passed |
| --- | ---: |
| Domain | 490 |
| Application | 879 |
| Infrastructure | 1,329 |
| Desktop exclusive WPF | 88 |
| Desktop parallel remainder | 1,477 |
| Desktop combined | 1,565 |

Command: `pwsh -File scripts/Test-Release.ps1 -ResultsDirectory artifacts/m196-release-final`. Results: `artifacts/m196-release-final/release-20261015-093914-475967823f0346abba1d3f2d67eb9423/summary.json` and per-stage TRX/diagnostics. Calendar native child **77.208 seconds**, **42.792 seconds** remaining against the unchanged 120-second deadline; Journal editor case **7.445 seconds**. No Calendar/Journal timeout; scheduling, assertions, concurrency tests and deadlines are unchanged.

Release build: **0 warnings/errors**. EF `has-pending-model-changes`: no changes. Whitespace checks include tracked and new files. Branch `develop`, HEAD `62455c5f28d4b57e254f8b1f7a713dac14627244`; initial worktree clean, **35 changed files** (22 modified, 13 new). No Weekly/Monthly UI, automatic generation, live/paid provider request, production-data access or Git write is included. Local synthetic tests do not establish live UI/provider acceptance or matching GitHub Actions acceptance of uncommitted work.

### Exact M19.6 changed files

- `README.md`
- `docs/backup-restore.md`
- `docs/period-reviews.md`
- `src/PersonalTradingJournal.Application/Backups/IPortableExportService.cs`
- `src/PersonalTradingJournal.Application/Backups/IRestorePreflightService.cs`
- `src/PersonalTradingJournal.Application/DailyReview/Coaching/SavedCoachingAnalysis.cs`
- `src/PersonalTradingJournal.Infrastructure/Backups/JournalRestoreService.cs`
- `src/PersonalTradingJournal.Infrastructure/Backups/PortableExportSchema.cs`
- `src/PersonalTradingJournal.Infrastructure/Backups/RestorePreflightService.cs`
- `src/PersonalTradingJournal.Infrastructure/DailyReview/Coaching/CoachingServiceCollectionExtensions.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/JournalDbContext.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/JournalDbContextModelSnapshot.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs`
- `tests/PersonalTradingJournal.Application.Tests/DailyReview/PeriodCoachingTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/PortableExportTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Initialization/JournalDatabaseInitializerTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integration/ProductionPersistenceIntegrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integrity/SqlitePersistenceIntegrityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Migrations/InitialMigrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookPersistenceTests.cs`
- `src/PersonalTradingJournal.Application/DailyReview/Coaching/SavedCoachingMetadata.cs`
- `src/PersonalTradingJournal.Application/PeriodReviews/Coaching/GenerateAndSavePeriodReviewService.cs`
- `src/PersonalTradingJournal.Application/PeriodReviews/Coaching/SavedPeriodReview.cs`
- `src/PersonalTradingJournal.Infrastructure/PeriodReviews/PeriodReviewRepository.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Configurations/PeriodReviewRecordConfiguration.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261015091425_AddSavedPeriodReviews.Designer.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261015091425_AddSavedPeriodReviews.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Records/PeriodReviewRecord.cs`
- `tests/PersonalTradingJournal.Application.Tests/DailyReview/SavedPeriodReviewTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/SavedPeriodPortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/DailyReview/PeriodReviewRepositoryTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/DailyReview/SavedPeriodSourceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Migrations/PeriodReviewMigrationTests.cs`

## M19.5 — Weekly/Monthly coaching contract and explicit generation

### Scope, identity and complete evidence

`PeriodCoachingGenerationService.GenerateAsync(query, cancellationToken)` is an **explicit-only, read-only operation**. It captures the currently selected Groq/OpenAI route before awaiting `IPeriodComparisonEvidenceReader`, reads both equivalent periods in one SQLite transaction, calculates M19.2–M19.4 facts without changing their formulas, builds one packet, invokes at most one provider request, then validates the complete response. There is no timer, startup/Refresh hook, UI action, saved-period review, migration or Trade/Journal/Rule write in M19.5. A successful result says **not saved**; it contains the exact immutable packet for a later persistence milestone. Failure never returns a usable review or packet as a successful result.

The separate evidence/response contract is **`period-coaching.v1`**; it does not repurpose Daily `daily-coaching.v1/v2` or change existing saved analyses. Whole Monday–Sunday weeks, calendar months, prior equivalents, exact original Account IDs, aggregate/null-scoped Journals and DST boundaries reuse M19.1/M19.4. Independently read periods are rejected, not compared as if consistent. Both sides describe the same current database snapshot, not historical as-of reconstruction.

The packet includes all selected evidence, including open/partial context, complete execution facts, current Journal fields/state/revision plus retained revisions, frozen Playbook transitions/guidance/example annotations, current global Rules, full assessment correction history and separate unattributed/deleted-source context. It also includes calculated Account/currency statistics and coverage, prior-period comparisons and breakdowns, Rule denominators/outcomes/origins and recorded mistake patterns. Nothing is reduced to a lossy summary to fit a provider. Null costs/Net remain null; currencies never merge. Not assessed, Cannot evaluate, compliant and confirmed violation remain distinct. Absent previous data is not evidence of progress. Mistakes are observations, not causes of losses; a Setup link is not adherence.

Sources have stable, explicit IDs: `current:` or `previous:` plus `trade:{id}`, `execution:{id}`, `journal:{id}:{revision}`, `journal-revision:{id}:{revision}`, `trade:{id}:playbook:{sequence}`, `rule:{id}:{revision}`, `assessment:{id}` or `unattributed:{id}`; calculated sources use `statistics`, `patterns` and `both:comparison`. IDs use the original identifiers, not names or list positions. Historical assessment content retains its embedded exact Rule revision even when it is absent from the active catalog. Source payloads include enough saved facts and availability information for later citations without substituting today's catalog. Playbook includes text annotations/provenance IDs **only**, no image bytes or local file paths; the model is explicitly told it has not inspected chart images. Trade notes were never part of this read contract and are not implied to have been supplied.

The builder owns canonical JSON and cloned read-only source payloads. Object names and arrays sort ordinally for deterministic identity independent of caller list order/culture; array positions are **not chronology**—execution sequence, revision, transition sequence and timestamps retain their meanings. Exact text, decimal values and explicit dates are preserved. `packetId` is SHA-256 of the versioned canonical content before adding that ID. This is an identity fingerprint, not authentication. Subsequent source edits cannot mutate a completed packet.

### Bounded input and structured response

The entire canonical packet, including IDs/handling labels/framing, is limited to **262,144 UTF-8 bytes and 10,000 sources**. Input copying is size-checked before potentially large comparison expansion. Existing reader and M19.3/M19.4 bounds also apply. Cancellation, invalid evidence, arithmetic overflow and oversized evidence are distinct outcomes; no partial packet escapes. An entirely empty current period (no Trade/activity/assessment and no meaningful Journal field) returns `NoEvidence` without a request, even if the prior period has data. Journal-only and Trade-only reviews are supported when the **whole** packet fits.

All five response properties are required:

| Card | Contract |
| --- | --- |
| Strengths | One to six cited findings; explicit evidence basis. |
| Areas for Improvement | Same, without inventing missing costs/adherence. |
| Mistakes Observed | Recorded observations, not proven causes. |
| Progress versus previous period | Same-period comparison coverage/revision limits stay explicit. |
| Action Plan | A cited explanation and **one to three** steps, each with action, measurable measure, timeframe and source references; or explicit insufficient evidence with zero steps. |

Each finding declares `CalculatedFact`, `RecordedEvidence`, `UserObservation` or `Interpretation`. A declared calculated fact can cite only calculated statistics/patterns/comparison; user observations cite Journal/revision/Playbook/Rule prose; recorded evidence cites Trade/execution/assessment/Rule sources. Interpretations must cite supplied evidence but are not labeled calculated facts. Every substantive finding/step has 1–16 distinct valid source references and nonblank text up to 1,000 characters. An insufficient-evidence card has exactly one cited limitation finding. Complete output is bounded to **65,536 UTF-8 bytes**, depth 32, and the configured provider output budget. Token usage above the output ceiling or a local `o200k_base` output estimate exceeding it is rejected, including absent/misleading usage metadata; the local count is not authoritative billing/Harmony accounting.

Validation rejects unknown/duplicate fields, missing sections, invalid enums, wrong version/packet identity, unsupported/duplicate citations, inappropriate source-kind/basis combinations, empty findings, incomplete/refused output and oversized responses. It returns defensive read-only collections. **Structure and citation membership cannot prove the truth of AI wording, causal claims, measurement feasibility or complete immunity to prompt injection.** Journal, Playbook, Rule, assessment and all other source strings are untrusted data, never instructions. No automatic adherence judgment or recalculation is delegated to AI.

### Provider selection, privacy and budgets

The dedicated period schema uses existing OpenAI Responses and Groq Chat Completions transports, not a URL swap. Both retain strict schema output, bounded non-streaming envelopes, no tools/retries/redirects or automatic cross-provider fallback. OpenAI's [structured-output contract](https://developers.openai.com/api/docs/guides/structured-outputs) requires separate handling of refusals/incomplete output; a schema-shaped response still goes through Application identity/citation validation. No provider SDK/model or credential-storage change was needed.

Settings provider selection is captured once; its separate credential is resolved only when generation is explicitly invoked. Switching providers during evidence loading cannot reroute the request. Saved-key precedence, environment development fallback **within that provider**, sanitized allowlisted status/code/request IDs, `Retry-After`, bounded 90-second default timeout (maximum 180), cancellation and unknown monetary cost are reused. The deadline includes evidence loading, packet construction, HTTP and validation. Late completions are discarded; cancellation cannot guarantee that an already accepted request was not billed. No raw source text, key, request/response body or provider error message is logged. Available token metadata is returned, not invented or priced without verified configuration.

Existing profiles remain unchanged: Groq `openai/gpt-oss-120b`, **6,800 input / 1,000 output**, local decoded-content/schema/framing token estimate plus 10% and 512 reserve; OpenAI `gpt-4.1-mini-2025-04-14`, **300,000 input / 8,192 output**, conservative complete HTTP UTF-8 byte ceiling plus 16,384 reserve. Both full periods, retained Journal history and Rule target expansion can exceed these limits quickly, especially Groq's small profile. `TooLarge` sends **zero requests**, omits nothing and suggests only an explicit supported higher-budget provider choice where appropriate—not an impossible narrower date within the selected period. No limit increase, automatic fallback or retry was added.

Measured synthetic preflight (fixed source identity, fake HTTP only):

| Groq fixture | Packet bytes | Instructions | Schema | Evidence | Framing | 10% margin | Reserve | Total / input limit | Outcome |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Journal-only week, empty prior | 3,736 | 445 | 626 | 974 | 83 | 213 | 512 | 2,853 / 6,800 | One fake request; validated five-card response |
| Journal-only month, empty prior | 3,738 | 445 | 626 | 968 | 83 | 213 | 512 | 2,847 / 6,800 | One fake request; validated five-card response |
| Long Journal week | 27,591 | 445 | 626 | 14,968 | 83 | 1,613 | 512 | 18,247 / 6,800 | `TooLarge`, zero requests |

The equivalent OpenAI week/month request bodies are **11,076 / 11,078 bytes**, yielding conservative ceilings **27,460 / 27,462** after reserve against the unchanged 300,000 profile. These are byte ceilings, **not actual token counts**. A 31-Trade migrated SQLite fixture is also rejected without a request; all 31 Trades/62 executions remain and no analysis is stored. Packet/calculation limits may reject an oversized period before provider preflight. No source count alone guarantees that a period fits: retained revisions, Rules, annotations and text lengths matter.

### Verification and remaining limits

Tests cover weekly/monthly/DST/year boundaries, exact/aggregate scope, mixed currencies and unknown Net, Journal history and prompt-like text, frozen Playbook revisions, Cannot evaluate/not-assessed Rules and unavailable/historical sources, deterministic immutable construction, missing prior periods, identity/source/basis validation, bounded actions/output, cancellation/timeout/late completion, safe errors and captured provider routing. Fake HTTP exercises both strict wire schemas, separate keys, token metadata, refusals/incomplete/malformed replies, rate/model/authentication errors and no retries. Synthetic migrated SQLite generation uses the real paired snapshot reader, observes zero writer operations from it, and proves concurrent source changes cannot rewrite the sent packet or response. Generation creates no analysis row. Existing Daily request shape, errors, saved v1/v2 snapshots and restart tests remain in the complete regression set.

| Verification | Final observed result |
| --- | --- |
| Focused Application period/coaching tests | **220/220 passed** |
| Focused Infrastructure period/coaching/SQLite tests | **137/137 passed** |
| New M19.5 cases | **74**: 41 Application, 33 Infrastructure |
| Complete `pwsh -File scripts/Test-Release.ps1 -ResultsDirectory artifacts/m195-release` | **4,225/4,225 passed, zero failures/skips** |
| Project totals | Domain **490**, Application **857**, Infrastructure **1,313**, Desktop **1,565** (88 exclusive + 1,477 parallel) |
| Calendar / Journal | Native **71.838 s**, **48.162 s** headroom to unchanged 120 s; Journal editor **7.666 s**. No timeout. A single pass does not eliminate host-load variability. |
| Release build / EF | **0 warnings/errors**, no pending model changes; isolated design-time context |
| Whitespace | Tracked diff and all nine new files pass |
| Live/provider/production | **Zero live or paid provider calls**, no production access; no new UI to accept |
| GitHub Actions | Uncommitted changes have no matching CI result; local gate is not CI acceptance |

Focused evidence: `artifacts/m195-focused/application-final.trx` and `infrastructure-final.trx`. Full evidence: `artifacts/m195-release/release-20261015-085657-8e18b1c631b64e208ae6d8109f818a9f/summary.json`, top-level TRX and child diagnostics. Early focused runs found two test expectation issues (duplicate synthetic null-scoped Journal selection; expecting Groq-only budget metadata from the unchanged OpenAI byte-ceiling path); corrected fixtures/assertions now match the real contracts. No production defect, deadline/concurrency change, skip or retry was needed.

Branch **develop**, HEAD **`fb82a29b6919505569d369b6466ddd42abcd4d03`**; the initial worktree was clean and final state is **six modified plus nine new files**, listed below. Existing M19.1–M19.4 work is preserved. No commit/push/merge. Remaining limits: conservative complete-evidence budgets (especially Groq), local token-estimate versus provider accounting differences, inability to prove AI semantics from citations, provider account/model access and real-world response quality remain unverified by fake HTTP. Weekly/Monthly UI, consent interaction and saved-review persistence are later work; no claim of live or matching-CI acceptance is made.

Exact M19.5 files:

- `src/PersonalTradingJournal.Application/PeriodReviews/Coaching/PeriodCoachingPacket.cs`
- `src/PersonalTradingJournal.Application/PeriodReviews/Coaching/PeriodCoachingResponse.cs`
- `src/PersonalTradingJournal.Application/PeriodReviews/Coaching/PeriodCoachingGeneration.cs`
- `src/PersonalTradingJournal.Application/DailyReview/Coaching/CoachingGeneration.cs` — sanitized message helper exposed internally; Daily behavior unchanged.
- `src/PersonalTradingJournal.Infrastructure/DailyReview/Coaching/PeriodCoachingWireContract.cs`
- `src/PersonalTradingJournal.Infrastructure/DailyReview/Coaching/SelectedPeriodCoachingProvider.cs`
- `src/PersonalTradingJournal.Infrastructure/DailyReview/Coaching/CoachingTokenCounter.cs` — shared existing bundled tokenizer, no download.
- `src/PersonalTradingJournal.Infrastructure/DailyReview/Coaching/OpenAiCoachingProvider.cs`
- `src/PersonalTradingJournal.Infrastructure/DailyReview/Coaching/GroqCoachingProvider.cs`
- `src/PersonalTradingJournal.Infrastructure/DailyReview/Coaching/CoachingServiceCollectionExtensions.cs`
- `tests/PersonalTradingJournal.Application.Tests/DailyReview/PeriodCoachingTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/DailyReview/PeriodCoachingProviderTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/DailyReview/PeriodCoachingSqliteTests.cs`
- `README.md` and `docs/period-reviews.md`.

## Scope and boundary

M19.1 supplies evidence only through `IPeriodReviewEvidenceReader.ReadAsync(PeriodReviewQuery)`. Contract version: **`period-evidence.v1`**. There is no Weekly/Monthly UI, AI request, interpretation, analysis persistence, schema migration, or Trade/Journal/Rule write. Existing Daily Review and assessment workflows remain unchanged.

`ReviewPeriodKind.Week` means the complete **Monday–Sunday** week containing an explicit New York `DateOnly`; `Month` means its complete calendar month. The request exposes inclusive `FirstDate`/`LastDate` and half-open `[FromUtc, BeforeUtc)` instants. Bounds reuse `DashboardAnalyticsQuery` and the established New York time policy, never the machine timezone or a fixed UTC offset. A DST week can contain 167 or 169 hours; weeks can cross a year boundary. Dates whose complete period cannot be represented are rejected.

An exact Account includes only records with that original Account ID. All accounts aggregates all Account identities, including inactive/unavailable references and the distinct null-scoped Journals. It does not copy aggregate day assessments into every Account or merge currencies. Global Rules have no Account owner; the same active global catalog is available for either request scope. Typed capability remains on the Rule definition; returning a Rule does not run it or make it applicable to an unsupported check target.

## Evidence contract

| Collection | Meaning and identity |
| --- | --- |
| `ClosedTrades` | Once per Trade ID, by its New York **closing date**, using the same projection and predicate as Calendar/Daily Review. Includes its full execution lifecycle, not just executions inside the period. A Trade spanning midnight belongs to its closure period only. |
| `ActivityContext` | Separate currently open/partial or unavailable-lifecycle Trades with an actual execution in the period. Not realized results and not a reconstruction of positions as of period end. A now-closed Trade outside the period is not recast as open activity. |
| Trade facts | Original Trade/execution IDs, audit/closure/execution instants, Account/Instrument/Setup identity and availability, currency, decimal economics, assigned mistakes and quality flags. Null commission, fees, Gross or Net stay null. No estimates, FX, cross-currency totals, adherence claims or duplicate Trades. Daily evidence inclusion enum names are reused; here `ClosedOnDate` means closure on one of the period's dates. |
| Frozen Playbook | Original capture and every retained association transition, sequence, exact Setup/revision, capture time, frozen guidance and example annotations/provenance IDs. Current, replaced, removed and legacy-without-capture remain distinct. No substitution of today's Setup guidance; no image bytes or local paths. |
| `Journals` | Current exact text/three answers, Draft/Completed, Journal ID, New York date, original Account scope, revision and UTC audit times. No item means absent Journal, not an empty string. |
| `JournalRevisions` | All **retained** immutable revisions for the selected Journal identities, including exact text, answers, state, original revision number and UTC saved time. Gaps from permitted old-revision deletion remain gaps. The existing revision schema does not record prior Account/date values after a Journal move; revisions are associated with the parent's current date/scope, not invented historical scopes. |
| `ApplicableRules` | Current active global Rule revisions with exact identity, version, prose, timestamp and optional typed condition. Archived/deleted and legacy scoped definitions are not silently broadened into this catalog. Historical assessments still carry their exact older Rule revision and text. |
| `Assessments` | Recorded Trade/day assessments attributable to this period and scope, each once by assessment ID. Includes append-only corrections, exact Rule revision, target, original Account name/ID, manual versus automatic origin, outcome, explanation, evidence references, UTC recording time, sequence and superseded ID. Automatic assessments retain the stored evaluator version, facts, calculation and execution IDs. Reads never run checks. |
| `UnattributedTradeAssessments` | Bounded scope history that cannot safely be assigned to a period. Kept separate from period coverage/results, with original identities and available/unavailable/changed source state. See attribution limits below. |

Collections have deterministic ordering: Trades by closure descending then ID (context uses the same ordering); executions by Trade/sequence/ID; Journals by date, null scope first, Account ID then Journal ID; Journal revisions by Journal ID/revision; active Rules by ID; assessments by Rule ID, durable target key, sequence ascending. Rule display order is not an evidence identity.

## Rules coverage and historical attribution

`Coverage(ruleRevision, exactTarget)` reports the latest retained assessment by durable sequence for **that exact Rule revision and target**:

- `Compliant` means the recorded `NoViolation` assessment, not a fresh calculation or blanket certification of the period.
- `ConfirmedViolation` and `CannotEvaluate` retain their existing Domain meanings and manual/calculated origin.
- `NotAssessed` means no matching record. A pass for an earlier Rule revision does not certify its revised definition. Missing evidence is not a pass or violation.

The returned assessment and source states accompany coverage. A recorded historical pass remains historical even if its sources are now changed/unavailable. Consumers must not relabel it as a current verified pass. No assessment is inferred from Rule prose, a Setup link, Journal text or absence of a record; no whole-period compliance percentage is produced.

Day assessments use their explicit New York target date, **not UTC recording date**. Trade assessments use retained source provenance for that exact Trade/Account family when it establishes one unambiguous date; corrections without their own source references share that attribution. Otherwise a still-existing Trade can be associated through its current selected-period lifecycle. If the Trade is deleted with no recoverable date, or retained source dates disagree, the records go to `UnattributedTradeAssessments` and cannot contribute to period coverage. The current assessment schema does not always store a Trade target's date separately: M19.1 exposes that limitation rather than changing history, guessing from recording time or silently dropping records. This separate collection can therefore contain un-attributable history from outside the requested period.

Deleted Trades/Journals cannot have their deleted content reconstructed. Retained assessment references preserve their saved identity/name/currency/date where available and explicitly report current source availability. A same-name replacement Account is never substituted. This is a consistent **current read snapshot**, not a saved report or a historical database reconstruction.

## Consistency, bounds and failure behavior

The Infrastructure reader opens one deferred SQLite transaction. Its first SELECT establishes the snapshot; **all** size checks, Trade/Journal/Playbook reads, Rules and source-availability checks use that same connection/transaction. WAL writers may commit meanwhile without mixing old and new revisions in one result. All reads are untracked; tests also run the reader on a read-only SQLite connection with a write-rejecting interceptor.

The shared range reader uses batched subqueries, not one query per day or Trade. Source resolution reuses the Rule repository's batches of at most 200 references. SQL count/text-length aggregates run before full payload materialization. Default hard ceilings (callers may lower, not raise them):

- **10,000 rows per checked set**, including full executions, Playbook transitions, Journal revisions, reference catalogs and Rule revisions; at most 10,000 decoded assessment source references.
- **5,000 assessments in the requested Account scope** across its retained history, needed to resolve existing provenance/corrections before date attribution.
- **8,000,000 counted text characters** across checked payloads, including serialized evidence/evaluation and frozen Playbook data. This is a text bound, not a promise about exact runtime bytes; object/JSON overhead and repeated references consume additional bounded memory.

These first-version limits are deliberately conservative: full reference/Rule catalogs and historical source dependencies are checked too. A small period can be rejected because its scope history or a shared catalog exceeds a ceiling. `LimitExceeded` returns a bounded dataset code and **no partial evidence**. No `Take` silently truncates a period. Cancellation throws `OperationCanceledException`, disposes the read transaction and returns no partial result. Operational or corrupt-data failures propagate as errors, never as a recorded `CannotEvaluate` assessment. SQLite's provider may not interrupt an already-executing synchronous engine operation immediately; cancellation is checked at query and assembly boundaries.

## Verification — 14 October 2026

Synthetic tests cover Monday/Sunday and month edges, cross-midnight lifecycles, both DST transitions and year changes; exact/aggregate scope, null-scoped and inactive-Account Journals; nullable costs/separate currencies; retained Journal revisions; legacy/frozen/replaced/removed Playbooks; all four coverage states, corrections, automatic metadata, deleted sources and same-name replacements; consistent reads during concurrent commits; deterministic ordering, fixed query count as Trade count grows, read-only connections, limits and cancellation.

| Verification | Observed result |
| --- | --- |
| Focused Application period contracts | **9/9 passed** |
| Focused Infrastructure period, Daily Review and Rules persistence | **72/72 passed** |
| Complete `pwsh -File scripts/Test-Release.ps1 -ResultsDirectory artifacts/m191-release` | **4,077/4,077 passed**, zero failures/skips: Domain **490**, Application **762**, Infrastructure **1,264**, Desktop **1,561** (88 exclusive WPF + 1,473 parallel) |
| Native/Journal deadlines | Calendar child **87/87**, process **61.015 s**, **58.985 s** headroom to 120 seconds. Journal editor child passed; no timeout or assertion change. |
| Release build | **0 warnings, 0 errors** |
| EF `has-pending-model-changes` | **No model changes**; in-memory design-time context |
| Whitespace | Tracked diff and all new files passed |
| Live UI / provider / production | Not exercised or required for this read-model milestone; **zero provider calls**, no production data access |
| GitHub Actions | No matching acceptance claimed for these uncommitted changes. Existing CI uses the same Release script on a PR targeting `main` or a push to `main`; triggers are unchanged. |

Evidence: `artifacts/m191-focused/application.trx`, `infrastructure.trx`, and `artifacts/m191-release/release-20261014-163006-3945308297cc4e9a9caccf197df22417/summary.json` with per-stage TRX/child diagnostics. Complete gate elapsed **304.819 s**. The 22 added cases are 9 Application and 13 SQLite cases; unchanged Daily Review/Rules cases provide the rest of the focused regression set. No deadlines, isolation, concurrency settings or assertions were weakened.

Branch **develop**, HEAD **`c8e6c9d25e23bf38581b9434189f389744f60af3`**, initially clean; final worktree has **five modified and five new files**, listed below. No commit/push/merge. Remaining limits are the conservative catalog/history ceilings and irrecoverable legacy attribution described above, not silent truncation or inferred compliance. This milestone has no UI to accept interactively, and local results do not establish GitHub Actions coverage for uncommitted changes.

## Exact implementation files

- `src/PersonalTradingJournal.Application/PeriodReviews/PeriodReviewEvidence.cs`
- `src/PersonalTradingJournal.Infrastructure/PeriodReviews/PeriodReviewEvidenceReader.cs`
- `src/PersonalTradingJournal.Infrastructure/DailyReview/DailyReviewEvidenceReader.cs` — shared range projection/predicate; existing daily query semantics retained.
- `src/PersonalTradingJournal.Infrastructure/Rules/TradingRuleRepository.cs` and `TradingRuleRepository.Reads.cs` — existing read mapping made assembly-internal for reuse, no write changes.
- `src/PersonalTradingJournal.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs`
- `tests/PersonalTradingJournal.Application.Tests/DailyReview/PeriodReviewQueryTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/DailyReview/PeriodReviewEvidenceReaderTests.cs`
- `README.md` and this document.

Weekly/Monthly UI, provider requests and saved analyses are deferred to later milestones. M19.2 adds the statistics below. No database migration or backup/export format change is needed for these read-only contracts.

## M19.2 — Period statistics and coverage

`PeriodReviewStatisticsCalculator.Calculate(evidence, cancellationToken)` is a pure Application calculation over a complete M19.1 snapshot. It makes **no database or provider request**, recalculates no Trade economics and mutates no source record. `PeriodReviewStatistics.Evidence` carries the original evidence unchanged, including Journal content/revisions, frozen Playbooks, global Rules, recorded assessments and unattributed/deleted-source states. M19.3, not this milestone, will calculate Rule adherence and mistake patterns. A missing assessment still means not assessed.

The shared `DailyReviewStatisticsCalculator.CalculateTrades` core performs identity, Account/currency and closure-bound validation and strict Gross/Net eligibility; its public Daily Review entry point retains its existing one-day boundaries. Both use the existing `PnlAccumulator`/`PnlMetrics` formulas shared with Dashboard and Calendar. Calendar's **Effective Net** may include estimates and is intentionally **not** a period Net result. No missing commission/fee becomes zero.

### Groups, definitions and denominators

There is one group per historical pricing currency, with nested exact Account-ID groups. Currency-level amounts aggregate unique Trades across Accounts in that same currency only. A complete Account group can have valid Net even if another Account makes the enclosing currency Net incomplete. Overall population and active-day counts are nonmonetary; there is **no cross-currency money total**, conversion, equity return or broker-balance calculation. Inactive/unavailable reference metadata and quality flags remain accessible; a same-name Account is not an identity substitute.

| Field | Definition and availability |
| --- | --- |
| `Population.Closed` | Unique closure-attributed Trade IDs/count, including break-evens and unavailable monetary outcomes. Open/partial activity is excluded. |
| `ActiveDays` | Distinct **New York closure dates** with at least one closed Trade, ordered ascending, each with its contributing IDs. Unknown P&L still counts as activity. Journal-only days, open activity, idle calendar days and holidays without closures do not count. Across Accounts/currencies, a shared date counts once, not once per group. |
| `Gross.Outcomes` / `Net.Outcomes` | Basis-labeled `PnlMetrics` and ordered known/unavailable/win/loss/breakeven Trade-ID sets. Positive/negative/zero refers to that basis; a Gross winner can be a Net loser. Outcome counts concern known values only; the coverage denominator always includes **every** closed Trade in the group. |
| `Metrics.Total` | Sum of authoritative recorded Gross or strict Net, present only with complete coverage. No Trades or all-unknown values return null, distinct from a genuine complete zero. |
| Strict Net eligibility | Requires eligible known Gross, every execution's commission/fees known, known nonnegative total costs and eligible recorded Net. Missing executions or quality flags block Net even if a projection happens to contain a numeric value. Uses the existing Daily Review rule, never a reconstructed estimate. |
| `Metrics.KnownSubtotal`, known profit/loss sums | Explicit known-subset evidence; **not a complete period total** when coverage is partial. Loss sum is a positive magnitude. All-unknown/empty selections have null subtotals. Consumers must retain the accompanying coverage label. |
| `Metrics.WinRatePercent` | 100 × wins / **all closed Trades**, including break-evens; null for empty/incomplete coverage. |
| `Metrics.ProfitFactor` | Positive profit sum / positive loss magnitude, same currency/basis and complete coverage only. No-loss, all-breakeven, no-Trade and incomplete states have no invented numeric sentinel. |
| `Metrics.AverageWin` / `AverageLoss` | Profit sum / winning Trade count; positive loss magnitude / losing Trade count. Exclude break-evens and require complete population coverage. Explicit no-wins/no-losses/no-Trades/incomplete statuses. |
| `AveragePerClosedTrade` | Complete total / **all closed Trade count**, including break-evens. |
| `AveragePerActiveDay` | Complete total / **distinct active closure dates in that group**, not seven days, month length, trading sessions or a count summed across Accounts. |
| `BestTrade` / `WorstTrade` | Maximum/minimum **individual Trade** result, not best/worst day or account return. Only complete coverage in one currency/basis; includes every tied Trade ID in stable order. A best result can be negative. Empty/incomplete groups return null. |

### Coverage and exclusions for later presentation

`MetricCoverage` explicitly distinguishes **Empty**, **Complete**, **Partial**, and **Unavailable**, with closed/known/unknown counts. Any unknown value suppresses complete totals, win rate, profit factor, all averages and extrema on that basis. Gross may remain complete while Net is unavailable. No statistically selected known-Net subset is presented as the whole period.

`PeriodGroupStatistics.Costs` contains **closed-Trade counts and IDs**, not execution counts: `MissingCommissionTrades`, `MissingFeeTrades`, and `MissingCostTrades`. The latter is the union/unknown-cost population, not the sum of overlapping component counts; unavailable total costs can also block Net. `Population.UnknownCommissions`/`UnknownFees` additionally retain context Trade quality, whereas `Costs` restricts its counts to realized candidates. Relevant evidence is available to explain unsupported/missing projection, unknown Gross/Net, missing executions and unavailable Account/Instrument/Setup/mistake sources through `Population.SourceQuality`, the basis-specific unavailable IDs and the original evidence. Missing data is neither a realized zero nor a Rule outcome.

`ExcludedOpenActivity` and `ExcludedUnavailableLifecycle` retain separate contributing IDs/counts. Partial exits do not become final realized P&L or active closure days; open/partial context is a current snapshot, not a historical as-of position reconstruction. A now-closed Trade outside the selected period is not treated as in-period open activity. New York boundary and DST rules remain M19.1's.

### Determinism, precision and limits

Trades and monetary accumulation use stable ID order, currencies ordinal order, Accounts ID order and dates ascending order. Every aggregate has contributing IDs, including tied extrema and date denominators. Reordering the input does not change calculated groups; the original evidence remains intact rather than being rewritten to that calculation order. Decimal values are not rounded for presentation; division retains .NET decimal precision. Checked arithmetic (including profit/loss sums and ratios) throws `OverflowException` without returning a partial result. It never wraps, clamps or reclassifies overflow as missing evidence. Cancellation likewise returns no partial statistics.

Duplicate IDs, wrong exact scope/currency format, out-of-period closures, invalid closed/context bucket assignment and context without in-period executions are rejected. The combined Trade population must fit M19.1's 10,000-Trade ceiling. The evidence reader's additional text/history/source limits still apply; this calculator cannot make an oversized or failed read valid. Existing missing-source and unrecoverable historical-attribution limitations remain unchanged.

### M19.2 verification and changed files

Focused tests cover mixed Account/currency groups; component-level unknown costs and inconsistent numeric Net; zero/breakeven and one-sided outcomes; active-day denominators and tied extrema; empty and open/partial-only periods; missing projections and unavailable sources; cross-midnight/DST/year/month boundaries; deterministic ordering, decimal precision, overflow, invalid evidence and cancellation. Synthetic migrated SQLite tests compare a month with **one active date** against the existing Daily Review and Calendar strict Gross/Net results across both DST transitions, known/unknown costs and exact/All accounts scopes. The period contract itself remains week/month only; no artificial one-day period kind was introduced. Calculation adds zero queries/writes and preserves source evidence.

Verification on **14 October 2026**:

| Check | Result |
| --- | --- |
| Focused Application period/Daily Review/Dashboard formulas | **62/62 passed** |
| Focused SQLite evidence/statistics regressions | **30/30 passed** |
| Complete `pwsh -File scripts/Test-Release.ps1 -ResultsDirectory artifacts/m192-release` | **4,103/4,103 passed**, zero failures/skips: Domain **490**, Application **784**, Infrastructure **1,268**, Desktop **1,561** (88 exclusive WPF + 1,473 parallel) |
| Calendar / Journal | Both passed unchanged deadlines; Calendar native process **69.056 s**, **50.944 s** headroom to 120 seconds |
| Release build | **0 warnings/errors** |
| EF consistency | No model changes; checked with the in-memory design-time context |
| Whitespace | Tracked diff and all new files passed |
| Live UI / provider / CI | No new UI or live provider call. No matching CI acceptance claimed for uncommitted changes. |

There are **26 new cases** (22 Application, 4 SQLite). Focused TRX files: `artifacts/m192-focused/application-final.trx` and `infrastructure.trx`. Complete evidence: `artifacts/m192-release/release-20261014-164648-449b013e484b4a249e11cb21dbe98b9a/summary.json` and its stage/child logs. Gate elapsed **314.859 s**. No assertion, deadline, concurrency or isolation changes.

Started clean on **develop**, HEAD **`d38ff0456dec263818873a81fb34d7f7a4acd2e9`**. Final worktree: **three modified and four new files**, below; no commit, push or merge. Existing M19.1 source/history ceilings and unrecoverable historical-attribution limits still apply. Arithmetic overflow fails explicitly rather than returning a misleading partial metric. UI, AI contracts/calls, saved analyses and M19.3 adherence/pattern work remain deferred. No production data was accessed.

Exact files:

- `src/PersonalTradingJournal.Application/PeriodReviews/PeriodReviewStatistics.cs` — result/coverage contracts.
- `src/PersonalTradingJournal.Application/PeriodReviews/PeriodReviewStatisticsCalculator.cs` — pure period calculation.
- `src/PersonalTradingJournal.Application/DailyReview/DailyReviewStatisticsCalculator.cs` — internal reusable range calculation; existing formulas/public Daily behavior preserved.
- `tests/PersonalTradingJournal.Application.Tests/DailyReview/PeriodReviewStatisticsCalculatorTests.cs`.
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/DailyReview/PeriodReviewStatisticsIntegrationTests.cs`.
- `README.md` and `docs/period-reviews.md`.

## M19.4 — Period comparisons and breakdowns

`IPeriodComparisonEvidenceReader.ReadComparisonAsync(selectedPeriod)` returns `PeriodComparisonEvidenceResult` (`Ready` or `LimitExceeded`, no half-result). The preceding period is the complete week/month containing **selected FirstDate minus one day**, with the same exact Account or All-accounts scope. Weeks remain Monday–Sunday, months remain calendar months; month lengths need not match. The two half-open UTC ranges are adjacent even across DST, leap days and years. Closure attribution, cross-midnight lifecycles and separate open/partial activity remain M19.1 semantics, not a futures-session rollover or as-of position reconstruction.

### Source consistency and boundaries

The Infrastructure reader opens one separate DbContext/connection and a deferred SQLite read transaction, reads **both** periods (including preflight counts, catalog/revision reads and source-availability checks), then disposes the transaction. Concurrent WAL commits cannot insert newer source state into the second period. No data is written, no migration/check/AI operation runs, and no query is issued per day or Trade. A controlled test commits Trade, Journal/revision and Rule changes **between the last previous-period SELECT and first current-period SELECT**: both returned periods retain the old state; a fresh read sees the new state.

`PeriodComparisonEvidence.Consistency` defaults to `IndependentReads`. The reader explicitly supplies `SingleReadTransaction`. A caller combining independently captured snapshots must retain that default: the calculator still exposes labeled side-by-side evidence/statistics, but withholds **all absolute and percentage changes**, including Rule outcome changes, as `IndependentSnapshots`. The marker describes how a trusted reader obtained the snapshots, not a cryptographic proof or a durable historical database version. Nonadjacent periods or unequal scopes are rejected.

The two periods are current read views of past dates, **not** historical database reconstructions. Later source edits, retrospective Playbook associations, corrections and catalog changes remain visible under M19.1 rules. Retained unavailable-source information and unattributed assessments stay in the unchanged source snapshots; unattributed records do not enter either period's totals.

### Comparisons and coverage

Contract version: **`period-comparison.v1`**. `PeriodReviewComparisonCalculator.Calculate` is deterministic, read-only Application code. It carries both original M19.1 evidence objects, M19.2 statistics and M19.3 patterns unchanged, plus the comparisons. Amounts retain decimal precision; arithmetic overflow or cancellation throws without returning a partial result. No presentation rounding or FX conversion.

| Result | Definition / eligibility |
| --- | --- |
| Closed Trades / active days | Current minus previous counts; active days are distinct NY closure dates, never summed Account/day counts. Empty counts are genuine zero. |
| Account/currency groups | Each original Account ID and pricing currency has its own comparison, plus one currency-level aggregate. Account ID `null` in a comparison means **currency aggregate**, not a null-owned Trade or Journal. No cross-currency monetary total. Original labels/availability remain in the side statistics and evidence. |
| Gross change | Current complete Gross total minus previous complete Gross total, in the same currency. Missing/empty populations and incomplete coverage give an unavailable delta, not inferred zero. |
| Net change | Same calculation, only when **every closed Trade on both sides** has strict known-cost Net coverage. A known subtotal is never used in a delta or percentage. |
| Percent | `100 × (current − previous) / previous`, only for a **positive** baseline, comparable complete amounts and the same observed Account/currency population. Zero and negative baselines have explicit unavailable reasons; negative-baseline percentages are deliberately withheld, not called improvement. Counts also expose their baseline and Account/currency compatibility. |
| Coverage changes | Separate changes in missing-commission, missing-fee and missing-either-cost Trade counts; the union is not a sum. Open/partial activity and unavailable lifecycle counts remain excluded from realized totals. Both underlying Gross/Net coverage objects and contributing/unknown Trade IDs remain attached. |
| Different recorded populations | Both ordered Account lists remain available. A complete same-currency absolute change may still be calculated across a changed Account population, but its percentage is withheld. A new/absent group is `MissingPopulation`; a present group without closed/known outcomes is `IncompleteCoverage`. These are data differences, not evidence of a performance trend. |

Example: an exact Account records USD Gross 40 in the previous week and 60 in the selected week: change **+20 USD, +50%**. If one selected-week Trade has unknown fees, Gross can still be complete but **Net change is unavailable**, with the missing-fee count shown. If another Account joins the aggregate USD population, the complete USD absolute difference can be shown with the changed Account sets, but not a percentage. A previous Gross of zero or −40 has no percentage. February versus March retains 28 versus 31 possible calendar days rather than pretending equal exposure.

### Bounded, traceable breakdowns

Every bucket keeps sorted previous/current Trade IDs and the same Account/currency statistics and coverage comparisons. All original Account/Instrument/Setup/mistake labels, active/unavailable states, exact capture metadata and assignment identities are accessible through the retained evidence. Display names are **never** keys; same-name replacement catalog entries do not merge.

- **Instrument:** original Instrument ID, including an unavailable catalog reference. Its missing label does not erase its identity.
- **Setup:** current associated **frozen Setup ID + exact Playbook revision** when captured; otherwise the currently recorded classification ID explicitly labeled `LegacyClassification` (no captured revision). No selected Setup is `NoAssociation`. Removed/replaced captures stay in evidence but do not place a Trade in additional performance buckets. Renaming/archiving a Setup does not replace frozen guidance or merge revision 1 with revision 2. A legacy classification is not falsely described as the Setup definition at Trade execution time; a link is never adherence.
- **New York session:** **Unknown session**. The application defines NY civil closure dates but has no authoritative persisted session taxonomy. M19.4 does not invent Asia/London/New York/RTH buckets, infer them from clock time, or call a date a session. This deliberately remains an unavailable breakdown dimension until a defensible definition exists.
- **Recorded mistake:** one bucket per recorded mistake ID; `NoRecordedMistake` means no recorded assignment, not verified mistake-free execution. A Trade with two mistakes appears once in each applicable bucket, but **once only in period totals**. Mistake buckets must not be summed to reconstruct period P&L. These are recorded associations, not proven causes of losses. Closed versus open/partial context remains separate.

There is no extra database access for breakdowns. Two M19.1 row limits apply (maximum 10,000 per source set per period, 5,000 scoped assessments); the reader shares the **8,000,000-character** budget across both reads, conservatively counting repeated catalogs/history twice. Oversize returns no comparison evidence. Calculation additionally caps 2,000 distinct breakdown buckets, 80,000 total Trade/bucket memberships and 80,000 output Account/currency groups, plus the existing 100,000 Rule targets **per period**. Limits can be lowered, not raised, by tests/callers. Bounded excess returns `LimitExceeded` with a safe code, never silent truncation. Source ordering cannot affect bucket order: kind, context, source ID, revision; monetary groups use ordinal currency then Account identity. No unbounded collection query was introduced.

### Rule comparisons are coverage, not an improvement score

Only identical **Rule ID + exact revision + version state + Trade/day target kind + Account scope + denominator kind** pair. Side summaries retain current/old/legacy/absent-current labels, latest-effective outcomes, manual/automatic origin, unavailable/changed targets, all correction histories and contributing source references. New revisions never inherit an older revision's pass. Archived/deleted definitions remain explicitly outside the active global catalog rather than being asserted active; M19.1 cannot distinguish those two absence causes.

Absolute assessed/compliant/violation/Cannot-evaluate/not-assessed count changes are exposed only when both matching cohorts have the **same target count** and the pair is transactionally consistent. Different target counts, absent cohorts or independent reads produce explicit statuses and side-by-side counts with no delta. No adherence percentage, cross-revision aggregate or improvement score exists. An All-accounts day remains distinct from each exact-Account day; a day result is never multiplied by its Trades. Current opportunities remain current-catalog opportunities, not historical obligations; recorded-only cohorts remain recorded-only. No check is executed and no missing assessment becomes compliance.

### M19.4 verification and changed files

Tests cover adjacent DST/week/month/leap/year boundaries, scope and null-scoped Journals, complete/partial/unknown economics, empty/zero/negative baselines, new Account/currency populations, missing Instruments, open activity, multiple mistakes, frozen/legacy/removed Setup contexts, Rule revisions and unequal denominators, cancellation, bounds, ordering, overflow, concurrent WAL writes, read-only query counts and fresh service-provider restart with archived Setup/Rule and deleted Trade source retention. All fixtures are synthetic. No production database, provider, UI or paid request is involved.

Final focused checks passed **100/100**: Application **70**, Infrastructure **30** (the Infrastructure filter also includes one existing import comparison regression). The **25 new cases** are 18 Application and 7 SQLite cases. Evidence: `artifacts/m194-focused/application-final.trx` and `infrastructure-complete.trx`. Initial compilation caught a test mapper namespace typo and an xUnit assertion-style analyzer violation; both were corrected before the final build/tests. No focused test assertion failed and no product lifetime, UI or test-harness change was required.

The complete `pwsh -File scripts/Test-Release.ps1 -ResultsDirectory artifacts/m194-release` gate passed **4,151/4,151**, **zero failures/skips**:

| Project | Passed / total |
| --- | --- |
| Domain | 490 / 490 |
| Application | 816 / 816 |
| Infrastructure | 1,280 / 1,280 |
| Desktop (88 exclusive WPF + 1,477 parallel) | 1,565 / 1,565 |

Calendar native completed in **73.143 seconds**, **46.857 seconds** below its unchanged 120-second deadline. The Journal editor layout test passed in **7.344 seconds** (child process **10.752 seconds**). No Calendar/Journal timeout or fixture cleanup failure occurred. These observed margins do not guarantee every host-load condition. Existing isolation, deadlines, concurrency tests and the complete gate's scheduling remain unchanged. Full evidence: `artifacts/m194-release/release-20261014-211411-3439f241e77c4bd19fbff59b67e3dbfc/summary.json`, project/child TRX and supervision logs (artifact timestamps are UTC; verification date is 15 October locally).

Release build passed with **0 warnings/errors**; EF reports **no pending model changes**. Whitespace checks passed for tracked and new files. Branch **develop**, HEAD **`51f28c2a7ee8962989f70c5a98633a7c8a97f908`**; initial worktree clean, final state **four modified and four new files**, listed below. Existing committed M19.1–M19.3 and fixture-lifetime work is preserved. No commits, pushes, merges, production-data access or provider calls. M19.4 local automated verification is green; no UI/live acceptance is claimed, and uncommitted changes cannot have matching GitHub Actions acceptance. Earlier M19.1–M19.3 results and the Journal lifetime chronology below remain historical evidence rather than claims for this worktree.

Exact files:

- `src/PersonalTradingJournal.Application/PeriodReviews/PeriodReviewComparison.cs` — pair/result, coverage, limits and reader contracts.
- `src/PersonalTradingJournal.Application/PeriodReviews/PeriodReviewComparisonCalculator.cs` — deterministic comparison and breakdowns.
- `src/PersonalTradingJournal.Infrastructure/PeriodReviews/PeriodReviewEvidenceReader.cs` — shared transaction/budget for both periods; single-period behavior retained.
- `src/PersonalTradingJournal.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs` — comparison reader registration.
- `tests/PersonalTradingJournal.Application.Tests/DailyReview/PeriodReviewComparisonTests.cs`.
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/DailyReview/PeriodReviewComparisonIntegrationTests.cs`.
- `README.md` and `docs/period-reviews.md`.

## M19.3 — Rules coverage and recorded mistake patterns

`PeriodReviewPatternsCalculator.Calculate` consumes the **complete M19.1 snapshot**, returning `PeriodPatternsResult` (`Ready` or `LimitExceeded`, with no partial patterns on excess). `PeriodReviewPatterns.ContractVersion` is `period-patterns.v1`. It is pure Application code: no database queries, writes, automatic checks, provider requests, UI or saved review. The result retains the original evidence object; Journal fields/revisions, frozen Playbook context, currencies and nullable economics are unchanged. No migration or existing evidence-contract change is needed.

### Rule identity, corrections and outcomes

A coverage target is the tuple **Rule ID + exact revision + target kind + exact target identity + Account scope**. Trade targets retain their Trade and Account IDs. Day targets retain their explicit New York date and exact Account or explicit All-accounts scope. For each tuple, the assessment with the **highest durable sequence** is effective, not the latest timestamp and not preferentially the manual or automatic record. Equal recording times are valid. The entire ascending-sequence history retains IDs, supersedes links, original explanation, Rule text/revision, UTC time, source references, source states and automatic evaluator/calculation metadata. A correction on a newer Rule revision does not rewrite the old revision's recorded result.

| Summary outcome | Meaning |
| --- | --- |
| Compliant | Latest assessment recorded **No violation**. An assessment result, not a fresh certification of the Trade or period. |
| Confirmed violation | Latest assessment recorded a violation, retaining whether evidence-backed or explicitly user-entered. |
| Cannot evaluate | Latest recorded assessment had missing/ambiguous evidence; not a pass or a violation. |
| Not assessed | No assessment for that exact revision/target. Never stored, inferred as compliance, or confused with Cannot evaluate. |

`LatestManual` and `LatestAutomatic` count effective results by recorded origin; older corrections remain in each target's history. Current source availability is independent of recorded outcome: `UnavailableTargets`, `ChangedTargets` and `TargetsWithChangedOrUnavailableEvidence` expose limitations without rewriting old results. An old pass whose source was deleted remains an old recorded pass with an unavailable source, not proof about a replacement record. No wording or Setup/Playbook association is interpreted as adherence.

### Populations and denominators

No combined adherence percentage is provided. Every `PeriodRuleSummary` is one Rule revision, one Trade/day target kind, one Account scope and one explicitly labeled denominator:

- **CurrentAssessmentOpportunities:** for each active current global Rule, one target per selected Trade (both closure-attributed Trades and separate open/partial activity), grouped by that Trade's exact Account; and one target per calendar date in the requested week/month at the **request's exact scope**. Zero-Trade dates remain possible manual review targets. These are current-catalog assessment opportunities, **not required checks, automatic eligibility, available-evidence guarantees, or historical as-of obligations**. A Rule created/revised after the period is not claimed to have existed during it. Current Rules are manually assessable against either kind of target under the existing assessment contract.
- **RecordedTargetsOnly:** retained old revisions, legacy Rules, absent-current Rules, targets outside the current selected Trade population (including deleted Trades), and exact-account day records returned within an All-accounts query. Only distinct recorded targets are counted; no missing assessments are invented for these cohorts. This denominator is not comparable to complete coverage of all possible Account/day combinations.

For each group, `TargetCount = Compliant + ConfirmedViolation + CannotEvaluate + NotAssessed`, and `Assessed = TargetCount - NotAssessed`. Corrections count once, not once per history row. A day assessment counts once regardless of its number of Trades. An All-accounts day and a P 21 day are different targets; their results are **not merged or multiplied**. Aggregate queries include each Trade once under its original Account. Exact queries exclude other Account and All-accounts day records.

Manual-only Rules have no automatic capability. `HasAutomaticCapability` only indicates that the **current global** Rule has a typed check matching this Trade/day kind; it does not run the check or assert that costs, currency, lifecycle or other required evidence are sufficient. Unrun checks remain Not assessed. Already-recorded automatic Cannot evaluate results retain their original coverage/evaluator facts. There is no implicit Net calculation or FX conversion.

Version states are `CurrentGlobal`, `SupersededGlobal` (same Rule has a different active current revision), `NotInActiveGlobalCatalog`, and `LegacyScoped`. M19.1 only supplies the active current catalog, so an absent historical Rule cannot reliably be distinguished as archived versus deleted; the result explicitly uses **Not in active global catalog**, not an invented deletion reason. Legacy scoped definitions retain their original applicability/Account text and receive recorded-only coverage, never promotion into a new global population. Archived/deleted/old Rules without any retained period assessment do not produce current opportunities.

Trade assessment date attribution is inherited unchanged from M19.1 (frozen source date where unambiguous, documented live fallback otherwise). `UnattributedTradeAssessments` are retained separately and excluded from every period outcome count; they may belong outside this period and are not guessed from their recording timestamp.

### Recorded mistake observations

Mistakes group by **mistake ID**, never by display name. Frequency means distinct selected Trades with that recorded mistake; multiple executions do not increase it. Each observation keeps its assignment ID/audit times, Trade ID, Account reference and closed/activity inclusion. Each Account subgroup exposes sorted closed-Trade and open/partial/unavailable-lifecycle activity Trade IDs. Overall frequency is the sum of these disjoint populations, with `ClosedFrequency` and `ActivityFrequency` available separately. Counts contain no monetary aggregation and do not erase currency boundaries in the retained evidence.

Inactive and unavailable mistake/Account labels remain explicit. Same-name replacement identities stay separate. No recorded mistake is **not** a verified mistake-free Trade; observed frequency is **not** a cause of losses, a Rule assessment or a mistake rate over executions. Deleted Trades have no invented current mistake assignments. No correlation, profitability claim or loss attribution is calculated.

### Bounds, determinism and verification

The reader's existing bounds remain. The calculator independently rejects excess source rows (10,000 Trades, Rules or assignments; 5,000 attributed plus unattributed assessments) and caps expanded Rule/revision/target rows at **100,000**, configurable downward for tests. Checked preallocation counting rejects excessive current Cartesian populations before expansion; retained historical additions obey the same cap. Cancellation throws without publishing a partial result. Malformed duplicate IDs/sequences, inconsistent mistake metadata, wrong scope or out-of-range day assessments throw rather than silently deduplicate potentially contradictory evidence. Valid summaries sort by Rule ID/revision, target kind, scope, denominator and date/Trade ID; mistakes sort by identity, Account and Trade ID, independent of input order or culture.

Regression coverage includes latest-sequence corrections at equal timestamps; mixed manual/calculated origin; newer Rule revisions; explicit Cannot evaluate; empty/DST/leap-month calendar denominators; exact/aggregate day separation; old/legacy/archived Rules; deleted/replaced sources; inactive/unavailable mistakes; closed versus partial activity; same-name identities; stable ordering; bounded expansion; cancellation and unchanged input. Synthetic SQLite cases exercise real automatic checks followed by manual correction, deduplicated reruns, scope filters, deletion retention, fresh service-provider/restart reads, legacy-schema migration reads and concurrent assessment/mistake appends during one read snapshot. Calculation adds **zero reads/writes** to the observed reader operations.

Focused period tests passed **74/74** (Application **52**, Infrastructure **22**), including **19 new M19.3 cases** (14 Application, 5 SQLite). The first SQLite restart test exposed an incorrect test-setup assumption that `IApplicationPaths` was registered by `AddPersistence`; the fixture now recreates services using the verified synthetic connection path. This was a test setup correction, not a production persistence defect. Final focused results are in `artifacts/m193-focused/application-final.trx` and `infrastructure-final.trx`.

Release build passed with **0 warnings/errors**; EF reports no pending model changes, and whitespace checks include all new files. The final complete `pwsh -File scripts/Test-Release.ps1 -ResultsDirectory artifacts/m193-release` gate finished **4,121 passed, 1 failed, 0 skipped (4,122 total)**:

| Project | Passed / total | Result |
| --- | --- | --- |
| Domain | 490 / 490 | Passed |
| Application | 798 / 798 | Passed |
| Infrastructure | 1,273 / 1,273 | Passed |
| Desktop (88 exclusive + 1,473 parallel) | 1,560 / 1,561 | One cleanup failure |

The failure is the existing `JournalFormAccountTests.RevisionConflictAfterTargetCollisionCannotBeClearedByChangingTheFormAccount`: `System.IO.IOException` deleting the fixture's temporary `journal.db` because it remained in use (`JournalSqliteTests.JournalTestDatabase.DisposeAsync`, line 308). The trace is teardown after the test body (line 159), not a failed period-pattern or Journal conflict assertion. The handle owner/root cause is **not proven**; no speculative Desktop change, retry, skip or deadline adjustment was made. **M19.3 implementation/focused verification is complete; full-suite acceptance remains open.**

Calendar native exited successfully in **102.386 seconds**, leaving **17.614 seconds** to its unchanged 120-second deadline. The isolated Journal editor test passed in **15.341 seconds** (child process 21.203 seconds); neither Calendar nor Journal hit a deadline. These single-run timings do not eliminate previously documented variability. Full evidence: `artifacts/m193-release/release-20261014-190334-cca614043847403ab204e8fb650e9579/summary.json`, top-level TRX files and child supervision logs.

Branch **develop**, HEAD **`7e29aa802a3e63045d89c164d5923dee2a943272`**; initial worktree clean, final state two modified documentation files and five new source/test files. No production journal/provider access or Git writes. Local automated evidence is separate from live interaction and matching GitHub CI; no UI was added or manually accepted, and uncommitted M19.3 changes cannot have a matching CI run.

Exact M19.3 files:

- `src/PersonalTradingJournal.Application/PeriodReviews/PeriodReviewPatterns.cs` — result, denominator and provenance contracts.
- `src/PersonalTradingJournal.Application/PeriodReviews/PeriodReviewPatternsCalculator.cs` — pure bounded calculation.
- `tests/PersonalTradingJournal.Application.Tests/DailyReview/PeriodReviewPatternsCalculatorTests.cs`.
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/DailyReview/PeriodReviewPatternsIntegrationTests.cs`.
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/PeriodLegacyRulePatternsTests.cs`.
- `README.md` and `docs/period-reviews.md`.

## Journal fixture lifetime follow-up (14 October 2026)

The M19.3 implementation was already committed at **`b70fd65229ef4ec8354329b498bee131bc76e7f9`** on `develop` when this investigation began; the worktree was clean. Its calculations and tests are preserved. The following evidence supersedes the earlier unresolved-cleanup finding without erasing that failed full run.

### Proven ownership and failure sequence

`JournalFormAccountTests.RevisionConflictAfterTargetCollisionCannotBeClearedByChangingTheFormAccount` changes the form Account, encounters a destination collision, encounters a newer persisted revision, and changes the Account back. Each form Account change starts `JournalTradeContextViewModel.SetScopeAsync`. This cancels the previous read and replaces `LoadTask`; cancellation is **not** completion of its native SQLite operation.

An opt-in test interceptor holds the real Calendar Trade query immediately after it has obtained a `DbDataReader`, before EF consumes/releases it. It logs bounded ownership/state only: DbContext instance ID, connection/reader state, explicit-transaction presence, cancellation, old/latest task completion and worker-thread ID. It asserts that the connection belongs to the fixture's exact synthetic database without logging the path or any source text/SQL. The conflict scenario then proceeds unchanged.

Observed before cleanup: **connection Open; reader not closed; cancellation requested; older task incomplete; latest task complete; no explicit transaction**. An exclusive file-open probe confirms that this very fixture still holds the file. The read runs on the ViewModel's `Task.Run` worker; no actual WPF window/dispatcher lifetime is involved in this test. `TradingCalendarDayReader` owns its factory-created DbContext with `await using`; the reader and its implicit SQLite read lifecycle keep the connection alive until the cancelled operation unwinds. The factory/DI provider does not dispose or join an already-running factory-created context. `SqliteConnection.ClearPool` clears idle entries but cannot make an active reader disappear.

With original teardown order, the controlled regression reproduces **the same `IOException` at `Directory.Delete`**, both alone and under verified concurrent Infrastructure load. It is the test's own superseded reader, not a different fixture or M19.3 period calculation. The uninstrumented original conflict case passed in these targeted runs; that does not erase the original load-sensitive failure or prove the original race impossible.

### Correction and safety

`JournalFixtureConnections` is a **test-only, per-fixture EF connection interceptor**. It registers opening connections before native opening begins, so teardown cannot miss an opening-in-progress. After ViewModel deactivation/cancellation, fixture teardown seals new connection opens, awaits all existing connections' closed/disposed/failed-open notifications, then awaits current editor/context tasks, disposes the provider, clears the exact owned SQLite pool and deletes the fixture directory. A failed close with a still-open connection does not release the barrier. A ten-second bounded drain fails explicitly rather than ignoring a leaked handle or retrying deletion. No global pool clearing, test serialization, production lifetime changes, sleeps, retries or existing deadline changes.

The held-reader regression verifies that teardown waits, a late connection open is rejected **before database access**, release disposes the reader/DbContext, stale results do not publish, the database and directory are actually removed, and the executed-query count does not increase after shutdown. Three further cases cover opening-in-progress, failed-open, close and disposal notifications, including refusal to treat an open handle as released after a failure. This is resource quiescence, not an assumption that the latest UI task represents all previous work.

### Experiments and results

| Experiment | Evidence | Result |
| --- | --- | --- |
| Isolated original ordering | `artifacts/m193-lifetime-before/before.trx` | Controlled regression fails with locked temporary database; owned cancelled reader remains open. |
| Original ordering with actual overlap | `artifacts/m193-lifetime-before-concurrent/desktop.trx`, `infrastructure.trx` | Desktop 6 passed / 1 expected reproduction failure; Infrastructure 103 passed. Runs overlap 20:03:27–20:03:34 UTC. |
| Corrected ordering under the same competitors | `artifacts/m193-lifetime-after-concurrent/desktop.trx`, `infrastructure.trx` | **16/16 Desktop and 103/103 Infrastructure passed**, zero skips. Runs overlap 20:05:10–20:05:18 UTC; Desktop test duration ~7 s, Infrastructure ~31 s. |
| Release build / EF consistency | Normal isolated design-time verification | **0 warnings/errors**, no pending model changes. |
| Exclusive WPF phase | Calendar native + Journal editor, unchanged scheduling/deadlines | **88/88 passed**. Calendar native **77.349 s**, **42.651 s** headroom to 120 s; Journal editor test **7.097 s**. This run's margin is not a guarantee against host-load variability. |
| Complete Release gate | `artifacts/m193-lifetime-release/release-20261014-200645-55f750ae76bd424c8016a4ca0918d0c1/summary.json` and TRX | **4,126/4,126 passed, zero failures/skips**: Domain 490, Application 798, Infrastructure 1,273, Desktop 1,565 (88 exclusive + 1,477 parallel). Original conflict test and all four new regressions passed. No Calendar/Journal timeout. |
| Whitespace / final worktree | Tracked and new files checked | Passed. Three modified files plus two new test files; branch/HEAD unchanged. |

The earlier M19.3 **local full-gate blocker is resolved** by this measured fixture-only correction. The historical failed run above remains part of the chronology, not the final result. Only synthetic databases were opened; no production journal or AI provider was accessed. A local pass is not matching GitHub CI acceptance; this fix remains uncommitted. Native timing remains host-load-sensitive; no claim of live UI acceptance or elimination of every possible file-lock cause is made. No M19.4 work was started.

Exact follow-up files:

- `tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalSqliteTests.cs` — ordered fixture shutdown and optional diagnostic injection.
- `tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalFixtureConnections.cs` — fixture-local connection barrier.
- `tests/PersonalTradingJournal.Desktop.Tests/Journals/JournalFixtureLifetimeTests.cs` — owned-reader reproduction and four regression cases.
- `README.md` and `docs/period-reviews.md`.
