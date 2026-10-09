# Daily Review with AI Coaching — M15

## Concise evidence and Source details

This M15.7 follow-up does not begin M15.8 acceptance. The normal reading path contains one summary per **Account and currency**, using the existing account statistics without recalculation. Currency-wide totals are no longer duplicated as extra cards; the complete statistics, including those totals, remain in day provenance.

- Each card shows closed Trade count, Gross with currency, complete Net or **Unavailable**, and wins/losses/break-evens explicitly based on Gross. Unknown Gross outcomes are not classified as break-even; their known denominator is stated when coverage is incomplete.
- An unavailable Net has one short explanation of missing commissions/fees for closed Trades, or other incomplete P&L/cost data. Known partial Net amounts never become a total. Open/partial activity and unavailable closing status are explained only when present. Multiple-currency guidance appears only when there is more than one currency; zero-only coverage explanations are omitted.
- **Contributing Trades and activity** starts collapsed. Its compact rows show instrument, Account, direction, New York closure date/time (including seconds and explicit UTC offset), Gross, Net state, and **Open Trade** for current sources. Open/partial or unreadable closing status is described in plain language and excluded from realized results. Row values follow the supplied statistics' known-source sets, not a second economics calculation.
- **Source details · [Account/currency] calculation** contains that card's exact IDs, population, coverage and statistics. **Source details · complete day provenance** contains the entire supplied daily evidence and statistics. Trade/execution and Journal details contain the full original records. These distinct collapsed sections replace repeated Contributing sources headings.
- All exact details use read-only selectable text: Tab to the field, Ctrl+A to select, Ctrl+C to copy. Text wraps and expands into the page's scrolling area; it has no independent vertical or horizontal scrollbar. IDs, quality codes, internal inclusion enums, revision values and update timestamps are not part of ordinary evidence cards.
- Journal observations remain user-written, with human-readable Account and Draft/Completed labels. Saved AI prose remains interpretation, with exact citations in collapsed saved-citation details. Historical identity, generation metadata/contract versions and the original packet remain available separately. No stored snapshots or response content are changed.

Generate's existing selected-provider disclosure, explicit click, limits/charges help, cancellation, validation and atomic save behavior are unchanged. No query, schema, Trade/Journal write, Account filtering or statistics contract changes accompany this work. Live interaction, screen-reader and physical high-DPI checks remain separate from compiled WPF renders; no live provider request is needed for this refinement.

Verification (2026-10-09, refinement only):

- **320 Daily Review tests passed**: 13 Domain, 128 Application, 87 Infrastructure, 92 Desktop. New cases verify one Account/currency summary, unchanged statistics and provenance, complete versus unavailable Net, missing commissions/fees and other P&L data, conditional warnings, and known-source sets for individual Trade values.
- **3,336 full parallel Release tests passed**, zero failures/skips: 454 Domain, 657 Application, 894 Infrastructure, 1,331 Desktop. The final source-field wheel routing and wrapping expander headers were then verified by **324 focused tests** (the 320 Daily Review cases plus 4 existing wheel-routing cases). The compiled evidence case measures a single native wheel step forwarded from a source field to the page.
- Eight isolated compiled WPF cases cover Light/Dark, **960 × 760 DIP / 96 DPI** and **480 × 760 DIP / 240 DPI**. Inspected collapsed account summaries, expanded compact Trades, expanded selectable exact facts, and saved AI interpretation. UI Automation expand/collapse, focusability, exact Trade navigation, select-all text, source identity, reachable scroll extent and no horizontal overflow pass. The source TextBox's disabled inner viewport uses the existing scoped wheel router; no global scrolling handler changed.
- Release build: **0 warnings / 0 errors**. EF: no pending model changes. Tracked and new-file whitespace checks passed. Raw logs, TRX and synthetic renders remain under ignored `artifacts/review-evidence/`.
- The first run exposed two test-only issues, both corrected: the saved-prose assertion relied on the formerly appended citation text, and the new compiled test called cleanup off the owning dispatcher. No retries, skips or increased deadlines were used.
- No live UI, clipboard, screen-reader or paid provider checks performed; no real journal accessed. No matching CI run can verify this uncommitted diff. M15.8 acceptance remains **not started**.

## Daily Review presentation refinement

The page relies on the shell's Daily Review heading, without repeating it. A compact wrapping toolbar selects the New York date and Account, with secondary Today and Refresh actions. Immediately below, the summary identifies the date (for example **04 Oct 2026 · New York**) and exact selected scope, closed Trade count, user-written Journal count, known-Net coverage, unknown commissions/fees and excluded activity. A Journal-only or empty day says **No closed trades**; observations never imply a trading outcome. Native date entry retains culture-aware parsing; displayed scope and source dates use readable day/month/year labels.

Generate AI Review is the primary action within the summary. Its visible disclosure names the selected provider receiving this scope's Trade facts and Journal text, only on an explicit click. **Privacy, provider limits and charges** expands the existing full disclosure: Groq Free-tier limits/upgraded billing or OpenAI charges, and the fact that local cancellation cannot guarantee no charge. No generation, provider, pricing, validation or save behavior changes. Cancel loading and Cancel generation are absent while idle, and appear only while their respective commands can cancel.

Current calculated facts, user-written Journal observations, saved AI history and generated interpretation are separate sections. Applicable coverage warnings and unknown Net are not collapsed into help. Journal cards retain their original identity and navigation while showing human-readable Account and Draft/Completed labels; **Source details · Journal** exposes revisions, IDs and internal scope information on demand. Inactive/unavailable Account labels remain visible. Prose is limited to 780 DIPs; data rows use available width. One vertical page scroll surface keeps long evidence and snapshots reachable.

Acceptance uses synthetic evidence and compiled WPF renders, not the real journal. Live pointer/keyboard, screen-reader behavior, actual monitor scaling, live-provider generation and a matching GitHub Actions run are separate M15.8 gates; automated render checks do not complete them.

Verification (2026-10-09):

- Focused Daily Review: **314 passed** (13 Domain, 128 Application, 87 Infrastructure, 86 Desktop), zero failures/skips. Includes 7 isolated compiled WPF cases, summary counts/coverage, Journal-only state, hidden idle cancellation, cancellable loading/generation, stale cancellation, provider disclosure, source-detail automation expansion and scrolling.
- Full parallel Release: **3,330 passed** (454 Domain, 657 Application, 894 Infrastructure, 1,325 Desktop), zero failures/skips. The final readable progress-date strings and additional Journal source-detail render/automation assertions were subsequently verified by rerunning all 314 focused tests.
- Release build: **0 warnings / 0 errors**. EF: no pending model changes. `git diff --check`: passed. No schema, query, provider or persistence changes.
- Automated Light/Dark renders generated at **960 × 760 DIP / 96 DPI** and **480 × 760 DIP / 240 DPI**. Inspected summary, expanded generation disclosure, Journal cards and saved interpretation. Controls wrap, long prose is bounded, and the page has one vertical scrolling surface with no horizontal overflow at tested sizes. Render files and TRX evidence are under ignored `artifacts/review-presentation/`.
- No live UI or provider request performed. No real journal accessed. These seven uncommitted files cannot yet have a matching GitHub Actions run; the user must commit/push and obtain a run for that exact commit before CI acceptance.

## M15.7: Manual AI generation

### Groq provider follow-up

Settings → AI Coaching now selects **Groq** or **OpenAI**. A new installation with no existing OpenAI configuration defaults to Groq. Existing installations (settings/database file or an existing OpenAI credential source) retain OpenAI; a saved provider choice always wins. The nonsecret choice is persisted atomically in `%LOCALAPPDATA%\PersonalTradingJournal.Secrets\provider.txt`. An unreadable/unknown preference requires an explicit selection, never an automatic provider switch. Opening Settings, selecting a provider, saving/replacing/removing a key, browsing or refreshing still makes **no provider request**.

1. Choose the provider, enter **your own** key in its masked field, and Save / Replace. Switching provider clears unsubmitted key text to avoid saving it under the wrong provider.
2. Groq uses `groq.dpapi` and `GROQ_API_KEY`; OpenAI retains `openai.dpapi` and `OPENAI_API_KEY`. Each has independent DPAPI CurrentUser saved-key precedence and environment fallback only when its own saved file is absent. Neither missing credentials nor a failed request tries the other provider. Removal affects only the selected provider and reports any newly exposed environment fallback.
3. Both secret files live outside SQLite and the journal data directory and must stay excluded from M16 backup/export/restore. Re-enter keys on another computer/Windows user. No key is embedded in the app, redisplayed, or logged.
4. Return to Daily Review, check the provider-specific disclosure, select the date/Account, and explicitly choose Generate AI Review. Groq receives the selected Trade facts and Journal text, not OpenAI despite the model ID's `openai/` prefix. Groq Free tier has limits; upgraded accounts may incur charges. OpenAI retains its usage-charge warning. Cancellation cannot guarantee an accepted request was not billed.

The dedicated Groq adapter posts to `https://api.groq.com/openai/v1/chat/completions` with `openai/gpt-oss-120b`, two system/user messages, the **unchanged complete packet** as user content, `response_format.json_schema.strict:true`, `max_completion_tokens`, low reasoning effort, one choice and no streaming/tools. It shares trusted evidence instructions and response schema, not the OpenAI Responses payload. Groq documents [strict schema support](https://console.groq.com/docs/structured-outputs) for this model; account access is separate and was not live-confirmed. Instructions retain Gross/strict-Net/currency/Account boundaries and treat all source text as untrusted. Every response still passes the M15.3 identity, structure, basis and source-reference validator before atomic save. Valid citations do not prove that generated prose is factually correct.

**Free-tier budget:** the documented [base limits](https://console.groq.com/docs/rate-limits) are 30 requests/minute, 1,000/day, 8,000 tokens/minute and 200,000/day for this model; actual organization limits and other clients' usage can differ. The [model](https://console.groq.com/docs/model/openai/gpt-oss-120b) supports a 131,072-token context, but PTJ deliberately uses a smaller profile: **5,500 estimated input tokens (including safety reserve) and 2,000 completion tokens**, with a maximum combined configured budget of 7,500. The packaged offline o200k-base tokenizer counts the entire escaped wire request including schema, adds 10% plus 512 tokens for framing, and rejects overflow without truncation. This is a conservative estimate, not Groq's exact Harmony accounting or a guarantee of available organization TPM. Completion includes reasoning; length-limited output is rejected, not shown as a completed review. No live token-count request, vocabulary download, automatic splitting, retry or model/provider fallback occurs. Monetary cost remains **unknown**.

Groq requires a complete single assistant choice with matching model and successful finish reason. Refusal, content filtering, incomplete output, malformed/oversized envelopes, invalid citations and packet mismatch never save. Bounded allowlisted errors distinguish credentials, permission/model access, quota/plan, rate limits, input size, request/schema and service failures; unknown codes remain unknown. Retry-After is displayed where available but never schedules another request. The existing bounded provider deadline/cancellation and secret-free diagnostics apply.

The selected provider route is captured at Generate click, **before** asynchronous evidence loading. Double submissions remain blocked; navigation/cancellation rejects late results. The successful saved record contains the actual Groq/OpenAI provider, model, usage and exact evidence/validated response. Model metadata permits a single nonempty namespace/model pair; other identifiers retain their existing restrictions. No migration, Trade/Journal economics or historical snapshot rewrite is needed. A saved analysis remains readable after changing providers, removing keys or deleting mutable sources.

The tokenizer uses packaged Microsoft.ML.Tokenizers/O200kBase 2.0.0, with Microsoft.Bcl.Memory explicitly pinned to patched 10.0.11 rather than its vulnerable transitive minimum. No security warning is suppressed. Automated tests use only synthetic credentials, fake HTTP and disposable migrated SQLite.

**Live/M15.8 gates:** neither a saved Groq key file nor a GROQ_API_KEY fallback was present during this task; **zero live Groq requests were made**. Model access, Free-plan acceptance and a real validated Groq result remain unverified. The earlier OpenAI model-access rejection below remains unresolved externally; adding Groq does not alter or bypass it. Use a disposable data root and synthetic evidence for a separately approved/manual live test after configuring Groq. Verify both providers' disclosure, key rotation/removal, keyboard focus, narrow scrolling and screen-reader feedback. Automated RenderTargetBitmap checks are not live interaction. These uncommitted changes require the user's commit/push and a matching GitHub Actions run before CI acceptance.

### Groq verification — 2026-10-09

Clean-start baseline: `develop` at `e8c8f086cc81cd9edea36f1ffae15a05885df864`. Focused Release **327/327 passed** (13 Domain, 128 Application, 87 Infrastructure, 99 Desktop). Final styled WPF layout suite **6/6 passed**. Complete parallel Release **3,328/3,328 passed** (454 Domain, 657 Application, 894 Infrastructure, 1,323 Desktop), zero failures/skips; both the pre-style-correction run and final styled build passed. Release build: **zero warnings/errors**. EF: **no pending model changes**. Tracked and new-file whitespace checks passed. Logs/TRX and synthetic renders are ignored under `artifacts/groq/` and `artifacts/groq-*.log`.

Coverage includes strict Chat Completions wire format, exact packet/citation validation, rate/plan/model/schema failures, whole-packet oversize rejection, independent saved/environment key sources, corruption and failed selection persistence, legacy/default/restart selection, captured routing during evidence loading, double-click suppression, cancellation, atomic SQLite save and historical display after switching/removing keys. No real journal or live provider was used. Initial verification caught a vulnerable tokenizer transitive minimum (pinned to a patched version), a new test's off-dispatcher cleanup (moved onto its owning dispatcher), and an unthemed selector in the Dark render (now uses the shared theme style with a regression assertion). No retry, skip, relaxed validation or deadline increase was used.

Settings and Daily Review disclosure were rendered in Light/Dark at 960 DIP/96 DPI and 480 DIP/240 DPI, with wrapping controls and reachable scroll content. These are automated WPF renders, not live mouse/keyboard/assistive-technology acceptance. Live Groq access/generation and matching GitHub CI remain open as described above. Worktree: 19 modified and 8 new files; no commit, push, merge, schema migration or changes to Trade/Journal data.

### Groq changed files

- `Directory.Packages.props`
- `README.md`
- `docs/daily-review.md`
- `src/PersonalTradingJournal.Application/DailyReview/Coaching/CoachingGeneration.cs`
- `src/PersonalTradingJournal.Application/DailyReview/Coaching/CoachingSafeDiagnostics.cs`
- `src/PersonalTradingJournal.Application/DailyReview/Coaching/GenerateAndSaveCoachingService.cs`
- `src/PersonalTradingJournal.Application/DailyReview/Coaching/SavedCoachingAnalysis.cs`
- `src/PersonalTradingJournal.Desktop/App.xaml.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/DailyReview/DailyReviewViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Settings/SettingsViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/DailyReview/DailyReviewView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Settings/SettingsView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Settings/SettingsView.xaml.cs`
- `src/PersonalTradingJournal.Infrastructure/DailyReview/Coaching/CoachingServiceCollectionExtensions.cs`
- `src/PersonalTradingJournal.Infrastructure/DailyReview/Coaching/OpenAiCoachingProvider.cs`
- `src/PersonalTradingJournal.Infrastructure/PersonalTradingJournal.Infrastructure.csproj`
- `src/PersonalTradingJournal.Infrastructure/Storage/LocalApplicationPaths.cs`
- `tests/PersonalTradingJournal.Application.Tests/DailyReview/SavedCoachingAnalysisTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/DailyReview/DailyReviewViewTests.cs`
- `src/PersonalTradingJournal.Application/DailyReview/Coaching/ICoachingConfiguration.cs`
- `src/PersonalTradingJournal.Desktop/Settings/CoachingConfiguration.cs`
- `src/PersonalTradingJournal.Infrastructure/DailyReview/Coaching/GroqCoachingOptions.cs`
- `src/PersonalTradingJournal.Infrastructure/DailyReview/Coaching/GroqCoachingProvider.cs`
- `src/PersonalTradingJournal.Infrastructure/DailyReview/Coaching/SelectedCoachingProvider.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/DailyReview/GroqWorkspaceTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Settings/CoachingProviderSelectionTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/DailyReview/GroqCoachingProviderTests.cs`

### Generation rejection investigation — 2026-10-09

One explicitly authorized live request used the saved Settings DPAPI credential, a disposable migrated SQLite database with synthetic Trade/Journal evidence, and the actual DailyReviewViewModel Generate command. No real journal was read or sent. Evidence preflight succeeded; historical-scope discovery had no error. The provider rejected the request before structured-response parsing, citation validation or atomic saving:

- Phase: `HttpResponse`; HTTP **403**; allowlisted `error.code`: **model_not_found**; `error.type`: **invalid_request_error**.
- Configured model: `gpt-4.1-mini-2025-04-14`.
- Client request ID: `c56796923916440b94c6fe355ef97190`.
- Server `x-request-id`: `req_0f46a5f012224f44840bd801082f138a`.
- Saved/displayed analysis: **none**; history rows: **0**. No automatic retry or second live request was made.

The confirmed product defect was classification: 403/404 previously returned AccessDenied without inspecting the provider code. Non-success bodies are now bounded to 64 KiB, JSON depth 16, and only a closed allowlist of code/type values is extracted. Model unavailability, authentication, permission, quota, rate limit, context size, invalid request/schema and service failures have distinct outcomes. Unknown or malformed details remain explicitly unknown; an unknown 404 is not assumed to be an access denial. The UI exposes only phase, HTTP status, allowlisted code/type, configured model and validated request IDs. It never exposes error.message, raw bodies, prompts, source text, credentials or responses. The same safe phase metadata distinguishes provider-envelope validation, response validation, snapshot validation and atomic-save failures. No diagnostic schema or source-data change was required.

**External configuration gate:** the live evidence establishes model rejection, not which project setting caused it. The project's permissions and model-access settings were not inspected or changed. In the OpenAI Platform project that owns the saved key, ask its owner to verify access to the pinned model and the key's Responses write/model-request permission, together with the user's project role. The official [RBAC documentation](https://developers.openai.com/api/docs/guides/rbac) lists model-request permission for `/v1/responses`; the [model documentation](https://developers.openai.com/api/docs/models/gpt-4.1-mini) lists the pinned snapshot. If access cannot be enabled, provide the safe server request ID above to the project owner/OpenAI support. Replace the key in Settings only if needed. There is no evidence that changing billing, switching models or weakening the response validator would resolve this rejection. A subsequent explicit user-generated request is required to verify corrected access; this investigation does not claim live generation success.

Historical Account discovery is separate: it succeeded against the isolated live dataset, and fake failure regressions demonstrate that discovery errors do not block current evidence/generation for available exact or aggregate scopes. The separately reported discovery error was not reproduced; the real journal was deliberately not inspected.

Automated HTTP tests cover the observed 403 and distinct 404 codes, unknown/malformed/oversized errors, safe diagnostics and no retries. An isolated SQLite flow uses Settings credentials, the provider adapter with fake HTTP, validation, atomic saving and the Generate command: a valid response saves and opens exactly one snapshot; rejected responses and invalid citations save none. Compiled Light/Dark normal and narrow/240-DPI renders verify wrapping diagnostics while current evidence remains visible. These are automated renders and command-path tests, not live mouse/keyboard acceptance. Live successful provider generation, interactive UI acceptance and GitHub Actions for the eventual commit remain separate gates.

Only **Generate AI Review** invokes the existing `GenerateAndSaveCoachingService`. A visible disclosure beside the action names the selected provider receiving the Trade facts and Journal text; expandable privacy help explains limits, possible charges and local cancellation. Loading, Refresh, navigation, reopening, committed-data notifications and history browsing never start a provider request.

The click captures the New York date and exact Account ID (or All accounts aggregate scope), rechecks current Account availability, reads fresh evidence, and builds one immutable M15.3 packet with M15.2 calculated facts. Only that packet is submitted, validated and saved through M15.4/M15.5. No UI calculation replaces the authoritative statistics. Trades alone or meaningful content in any of a Journal's four fields can support a request. Zero Trade evidence plus absent/whitespace-only Journal content sends nothing. Open/incomplete Trade facts remain explicitly uncertain/excluded from realized statistics; the UI does not invent missing Net. Invalid or oversized packets are not truncated.

Historical/deleted Account options remain browseable but cannot generate. If an Account disappears after selector loading, click-time revalidation sends nothing and marks the selection unavailable without changing its ID. Inactive persisted Accounts are still available scopes. All accounts continues to generate an aggregate analysis, distinct from an exact-account analysis and from a null-scoped Journal.

### Request lifecycle and results

- One in-flight operation per workspace, guarded both by command availability and its execution body. Double execution cannot create a second provider call.
- Progress reports evidence preparation, then generation/validation/storage. **Cancel generation** cancels the linked operation. Changing date/scope, Refresh/history-page reload, committed-data reload, leaving the destination or accepted main-window closing also cancels. Other navigation guards retain their existing behavior.
- A request version, captured date/scope and active-state check reject late completions, including a switch away and back. A new request is unavailable until the cancelled operation unwinds. No automatic retry is added.
- Local cancellation terminates the synchronous request connection through the existing adapter. It is not a guarantee of no provider processing or charge. The [official cancellation guidance](https://developers.openai.com/api/docs/guides/background#limits) distinguishes synchronous connection termination from background-response cancellation; this implementation does not enable background mode.
- Only atomic **Saved** success refreshes the captured scope's history to page one and opens the saved response/evidence/citations. A later selection is never changed to show an older request. Token metadata remains nullable; cost is explicitly unknown because no verified pricing configuration is installed.
- Database failure reports that saving was not confirmed, never shows the generated response as a saved analysis, and warns against another potentially billable generation before checking history. Cancellation racing a commit may leave the already committed snapshot at the original scope; Refresh that scope to reconcile. This does not alter the existing atomic repository semantics.
- Missing credentials, invalid configuration/authentication/access, oversized input, rate limit/quota, service/provider failure, refusal, incomplete/invalid output, timeout, cancellation and storage failure retain their distinct sanitized orchestration messages. Current statistics and existing history are not replaced on generation failure. Raw exception strings, HTTP bodies, prompts, source text and provider output are not logged by the UI.

### Configuration and acceptance limits

Use **Settings → AI Coaching** as described below. M15.4's existing `gpt-4.1-mini-2025-04-14` profile, input limits and 90-second provider timeout are unchanged; no provider/model or pricing configuration UI is added. Local Trade/Journal workflows work without a credential or network.

### Rejection follow-up verification and changed files

Baseline: `develop` at `0dd725313d2bb7fbaa2bc7b2236aa699c66ddae4`, clean before this investigation. Focused Release: **287/287 passed** (13 Domain, 122 Application, 66 Infrastructure, 86 Desktop). Complete parallel Release: **3,288/3,288 passed** (454 Domain, 651 Application, 873 Infrastructure, 1,310 Desktop), zero failures/skips. Release build: **zero warnings/errors**. EF: **no pending model changes**. Tracked and new-file whitespace checks passed. All four rejection renders were inspected (Light/Dark, 960 DIP/96 DPI and 480 DIP/240 DPI). Logs, TRX and synthetic renders remain under ignored `artifacts/ai-rejection/` and `artifacts/ai-rejection-*.log`; the authorized request's safe summary is `artifacts/ai-diagnostic-safe.log`. The initial focused run exposed an existing metadata reference-identity assertion; it now compares all metadata fields including the deliberately advanced validation phase. No test retries or weakened response validation were introduced.

Exact changed files:

- `README.md`
- `docs/daily-review.md`
- `src/PersonalTradingJournal.Application/DailyReview/Coaching/CoachingGeneration.cs`
- `src/PersonalTradingJournal.Application/DailyReview/Coaching/CoachingSafeDiagnostics.cs` (new)
- `src/PersonalTradingJournal.Application/DailyReview/Coaching/GenerateAndSaveCoachingService.cs`
- `src/PersonalTradingJournal.Infrastructure/DailyReview/Coaching/OpenAiCoachingProvider.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/DailyReview/DailyReviewViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/DailyReview/DailyReviewView.xaml`
- `tests/PersonalTradingJournal.Application.Tests/DailyReview/CoachingGenerationTests.cs`
- `tests/PersonalTradingJournal.Application.Tests/DailyReview/SavedCoachingAnalysisTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/DailyReview/OpenAiCoachingProviderTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/DailyReview/DailyReviewGenerationTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/DailyReview/DailyReviewSqliteTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/DailyReview/DailyReviewViewTests.cs`

### Settings — AI Coaching credentials

- Enter your own key in the masked field and choose **Save / Replace key**. The field is cleared after submission and on leaving Settings; stored keys are never loaded into the field. Only configuration status and active source are displayed. Saving performs local validation/storage, **not an authentication test or paid provider call**. Daily Review links to Settings when no usable local credential is available.
- Windows DPAPI `CurrentUser` protects the bytes at `%LOCALAPPDATA%\PersonalTradingJournal.Secrets\openai.dpapi`. This separate app-specific directory is outside `PersonalTradingJournal` journal data, SQLite, settings JSON, logs, screenshots and backups. A uniquely named same-directory temporary contains ciphertext only; it is flushed before atomic replacement, with best-effort ciphertext temporary cleanup on failure. The previous active file survives a failed replacement.
- M16 backup/export/restore must **exclude this secrets directory and all its temporary files**. Keys are not portable application data: enter them again on another computer/Windows user. Isolated runs place their secrets under the explicitly isolated root, never the real LocalAppData root. No application-owned key is built in.
- Each explicit generation resolves the selected provider's saved key again; no restart or cached credential is required. Only absence of that provider's saved file permits its `OPENAI_API_KEY` or `GROQ_API_KEY` development fallback. **Remove saved key** removes only the selected provider's local storage, not the provider-side key; Settings explicitly reports whether its environment fallback is now active. Remove that environment variable separately to leave no active credential.
- Corrupt, inaccessible, oversized or undecryptable saved storage reports a safe replace/remove message and blocks fallback/provider submission. Validation and storage failures expose no key, raw exception or path diagnostic. Existing evidence, statistics and saved analyses are unaffected.
- DPAPI protects a local secret at rest, not against malware running as the same Windows user, an unlocked compromised machine, process-memory inspection or a credential the user pastes into Journal content. Keep the Windows account/device secure. The provider needs a transient plaintext credential in memory to authenticate; do not include it in screenshots, logs, support bundles or bug reports. [Official authentication guidance](https://developers.openai.com/api/reference/overview#authentication) treats API keys as secrets; this Desktop application uses only the user's own key, never a shared embedded application credential.

Automated coverage uses synthetic keys in disposable directories, actual CurrentUser DPAPI, and in-memory HTTP handlers: save/replace/restart/remove, precedence/fallback, unreadable storage, failed replacement, no plaintext at rest, sanitized errors, immediate rotation on the same provider, zero provider calls from Settings, navigation, and compiled Light/Dark 960-DIP / 480-DIP-at-240-DPI layouts. Live provider authentication, another Windows user/computer, interactive screen-reader/keyboard operation and GitHub Actions for the eventual commit remain separate checks.

Credential Settings follow-up changed files (no schema, economics, provider model or evidence changes):

Final local verification on 2026-10-09, based on develop `e87d63fde3c40dd020f56b29f0dac874332a5f1a` plus this uncommitted follow-up:

- Focused Release: **288 passed** (Domain 13, Application 116, Infrastructure 52, Desktop 107); no failures/skips.
- Complete parallel Release: **3,259 passed** (Domain 454, Application 645, Infrastructure 859, Desktop 1,301); no failures/skips. Desktop elapsed 3m13s; existing isolation/deadlines/concurrency configuration unchanged.
- Release build: **0 warnings, 0 errors**. EF model consistency: no pending changes. `git diff --check` and new-file whitespace checks: clean.
- All four compiled Settings renders inspected (Light/Dark, 960 DIP/96 DPI, 480 DIP/240 DPI): readable status/actions/disclosure and masked entry, no clipping. Automated save click clears input. These are RenderTargetBitmap checks, not live mouse/keyboard or physical-monitor acceptance.
- Evidence: ignored `artifacts/ai-settings/` and `artifacts/ai-settings-*.log`. Initial new-test issues (PasswordBox internal scroll viewer and off-dispatcher teardown) were corrected in the test; no retry/deadline/production interaction workaround. No real journal, real credential, paid call or schema migration. GitHub Actions cannot verify uncommitted work; the user's eventual commit/push requires its own matching CI result.

Files:

- `README.md`, `docs/daily-review.md`
- `src/PersonalTradingJournal.Application/DailyReview/Coaching/ICoachingCredentials.cs` (new local credential boundary), `CoachingGeneration.cs` (sanitized Settings guidance)
- `src/PersonalTradingJournal.Desktop/Settings/ProtectedCoachingCredentials.cs` (new DPAPI store)
- `src/PersonalTradingJournal.Desktop/App.xaml.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Settings/SettingsViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/Settings/SettingsView.xaml`, `SettingsView.xaml.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/DailyReview/DailyReviewViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/DailyReview/DailyReviewView.xaml`
- `src/PersonalTradingJournal.Desktop/ViewModels/MainWindowViewModel.cs`
- `src/PersonalTradingJournal.Infrastructure/Storage/LocalApplicationPaths.cs`
- `src/PersonalTradingJournal.Infrastructure/DailyReview/Coaching/CoachingServiceCollectionExtensions.cs`, `OpenAiCoachingProvider.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Settings/CoachingCredentialsTests.cs` (new)
- `tests/PersonalTradingJournal.Desktop.Tests/DailyReview/DailyReviewViewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Navigation/MainWindowDailyReviewTests.cs`

Remaining isolated manual checks: navigate from Daily Review to Settings with no key; keyboard-enter a disposable synthetic key, save/replace/remove it, and confirm masking, clearing and source messages in both themes. Check real keyboard focus, screen-reader status announcements, narrow scrolling and actual monitor scaling. Do not click Generate with a real key without explicitly accepting provider transmission/charges. A second Windows user or computer should reconfigure its own key, not restore the encrypted file. M16 must retain the exclusion above. DPAPI is not protection from software already running as the same Windows user.

Automated verification uses fake providers and isolated SQLite only. Live paid-provider behavior, actual mouse/keyboard/assistive-technology operation and GitHub Actions for the user's eventual commit are separate acceptance gates. No real journal or paid API is used during implementation.

Remaining isolated live checks: launch with a disposable data directory; inspect the disclosure and Generate/Cancel keyboard focus in both themes; cancel a request and change date/scope while it is pending; confirm a saved snapshot opens with its original evidence and citations. A live provider request requires the user's own credential and explicit acceptance of charges. Check a missing-key launch separately. Verify actual monitor scaling and screen-reader progress announcements. Cancellation/commit races require checking history, not blindly generating again.

M15.7 changed files:

- `README.md`, `docs/daily-review.md`
- `src/PersonalTradingJournal.Desktop/ViewModels/DailyReview/DailyReviewViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/MainWindowViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/DailyReview/DailyReviewView.xaml`
- `tests/PersonalTradingJournal.Desktop.Tests/DailyReview/DailyReviewGenerationTests.cs` (new)
- `tests/PersonalTradingJournal.Desktop.Tests/DailyReview/DailyReviewSqliteTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/DailyReview/DailyReviewViewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/DailyReview/DailyReviewWorkspaceTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Navigation/MainWindowDailyReviewTests.cs`

### M15.7 verification

- Baseline/final HEAD `a064a73ed655fb992a46075056cbf09ed7de5df1`, branch `develop`. Initial worktree clean; final 9 modified tracked files and 1 new test file, none staged. No commit, push or merge.
- Final focused Release: **247/247 passed** — Domain 13, Application 116, Infrastructure 52, Desktop 66; zero failures/skips.
- Final complete parallel Release: **3,247/3,247 passed** — Domain 454, Application 645, Infrastructure 859, Desktop 1,289; zero failures/skips, Desktop 2 m 28 s. An earlier full run also passed before a final small fix clearing a previous success message when changing selection; the final run includes that regression assertion. No harness deadline or coverage was weakened.
- Release build **0 warnings / 0 errors**; EF **no pending model changes**; `git diff --check` and new-file whitespace check passed.
- New fake-provider coverage includes Trade-only/Journal-only/combined evidence, fresh immutable packet and exact scope, no automatic calls, double execution, every provider failure enum, actual controlled timeout, malformed output/identity/citations, failed storage, empty/blank/oversized/read-failed preflight, unavailable Accounts, late responses after cancellation/date/scope/Refresh/navigation/close, and unchanged existing evidence/history on failure.
- Migrated SQLite flow saves one validated snapshot from an explicit Journal-only request, opens the original saved evidence and confirms a later rate-limit failure creates no additional row or source changes.
- Compiled WPF tests exercise bound Generate/Cancel commands, disclosure, busy/disabled states, accessible names/focusability and no horizontal overflow. Light/Dark automated generation renders inspected at **960 × 760 DIP / 96 DPI** and **480 × 760 DIP / 240 DPI**. Native monitor scaling, real pointer/keyboard use and screen-reader announcements are not claimed.
- Evidence: ignored `artifacts/m157/` and `artifacts/m157-*.log`. No paid request, credential access, real journal or schema change. GitHub Actions must verify the user's eventual commit; local results are not CI acceptance.

## M15.6: Desktop workspace

The Daily Review menu opens local current evidence and saved history, initially **today in America/New_York** and **All accounts**. Its date is a calendar date, not a converted machine-local midnight. Today explicitly selects the current New York date. Navigation away cancels pending reads; returning preserves the selected date, account and history page, then reloads. Browsing never generates or makes paid requests; M15.7 adds the separate explicit action documented above. No migration or source persistence/economics change accompanies this UI.

### Current evidence versus history

- Current evidence uses `IDailyReviewEvidenceReader` and `DailyReviewStatisticsCalculator` unchanged. Fully closed Trades use their New York closure day; cross-midnight Trades are not split, and DST uses the established boundaries. Open/partial and unavailable-lifecycle activity are separately counted as excluded context.
- Currency-level and per-account rows show closed count, Gross, strict Net, known Gross wins/losses/break-even, cost completeness and contributing IDs. Total/coverage use M15.2 populations; no partial subtotal is presented as a total, no Gross-as-Net estimate is substituted, and no currency conversion or cross-currency money total exists. Display rounds amounts to two decimals only; complete Trade facts and saved evidence retain the exact decimal values.
- All accounts current evidence includes each account's records plus the distinct null-scoped Journal. Exact account selection retains only its records. Inactive accounts remain selectable. A selected account that disappears remains explicitly unavailable, never silently changes to All accounts, and can still read its saved history. The selector also discovers historical/deleted Account identities with analyses on the selected date, including after restart; see the follow-up below.
- Current Trade and Journal sections keep IDs, original Account, timestamps, Journal state/revision, exact text and answers. No Journal entry is distinct from an existing empty field. Trade detail navigation reads the chosen ID independently of list paging. Journal navigation uses the exact date/account with the existing unsaved-editor guard. These actions say **Open current**; saved sources have no misleading live-source navigation button.
- History is ten analyses per page, filtered by date and **analysis scope**, ordered by stored generation UTC descending then ID. All accounts history contains aggregate analyses only, not a relabeled mixture of exact-account analyses. Page changes do not change current date/account evidence. A page emptied by deletion is reconciled to the last valid page.
- Opening a row loads its saved record once, not once per cited source. The version-aware Desktop decoder renders stored statistics, Trades, Journal content, response sections and citation catalog without rebuilding calculations or querying today's sources. A complete saved-evidence expander retains execution details, source identifiers and missing-data markers. Unsupported/damaged snapshots show an actionable error rather than substitute current data. Citation membership/labels do not prove the AI's wording correct.
- Generation time is labeled New York with the actual explicit UTC offset. Provider/model, analysis/packet identity, contract versions and input/cached-input/output/total token usage appear on the opened snapshot; absent usage is unknown. Monetary cost stays unknown. Raw provider diagnostics/credentials are not displayed.
- **Delete analysis** confirms its exact ID, date, original scope and generation metadata. It permanently deletes that analysis's response/evidence only, reconciles the page and closes the snapshot. Cancellation at confirmation does nothing; errors retain the current list and offer Refresh. Source Trades/Journals and other analyses are untouched. Existing local snapshot privacy/backup limits below still apply.

### Loading, accessibility and limits

Current evidence and saved-history reads have independent error handling, off-dispatcher database work, cancellation and selection-generation checks. Detail reads have an additional generation so closing a snapshot, switching rows/pages, refreshing or leaving cannot resurrect a stale view. Date/account changes clear previous data before reading. Refresh and committed Trade/Journal notifications reload only the active workspace; reopening the destination also rereads current data. No raw exception or private source text is logged in these error paths.

A single vertical scroller contains wrapping cards, focusable date/account controls, bounded history actions and read-only snapshots. Shared theme brushes/styles explicitly apply inside WPF templates. The calendar picker reuses Dashboard's themed calendar; dates and snapshots do not add nested scroll traps. Screen-reader names identify source/analysis IDs and loading/error announcements. Controls wrap at narrow widths; exact large payloads remain available through expandable detail.

Automated synthetic renders and isolated SQLite/VM tests are distinct from live interaction. Remaining manual checks: in a disposable data directory, use mouse and Tab/Enter/Alt+Down to select dates/accounts, scroll/expand all saved evidence, open current sources and decline a dirty Journal navigation prompt, confirm/cancel one-analysis deletion, and check actual monitor scaling/screen-reader announcements in both themes. No live UI or GitHub Actions acceptance is implied by local tests; a run must include the user's eventual commit of these changes. No real journal or paid API was used.

### Historical Account scope discovery

The original selector loaded only persisted Accounts and could retain an unavailable selection only in memory. After restart, a deleted ID was missing from the selector; All accounts history intentionally selects aggregate analyses only, so it could not reveal that exact-account history. The snapshot itself remained readable in persistence.

For the selected New York review date, the Account selector now includes **saved name · original Account ID (historical / unavailable)** options. Choose that option, then Open saved analysis. Names come from that scope's newest saved analysis (generation UTC descending, analysis ID ascending on ties); absent saved names are explicitly “Name not supplied”. A same-name replacement has its own ID and does not inherit history. Inactive persisted Accounts continue to use the ordinary selector entry.

`BrowseHistoricalAccountsAsync(HistoricalCoachingAccountQuery)` performs a count and bounded scalar page in one read transaction: exact-account analyses on that review date whose original Account ID is absent from Accounts, grouped by ID, sorted by ID ascending. Default 25 scopes per page, valid size 1–100. Evidence, response and metadata JSON are not read. The existing date/scope/account index is reused; no migration or snapshot mutation is needed. Previous/Next historical Accounts browse these pages independently of the ten-row analysis history and retain a selected historical ID even if it is outside the discovery page.

Date changes reset discovery to page one; Refresh and committed-data reloads rediscover scopes. Cancellation and separate generation checks reject stale discovery pages. Discovery and current-evidence failures have independent recovery messages: a current Trade/Journal read failure cannot hide saved Account options. Unavailable current scopes keep their original ID and show empty/error current evidence as appropriate while snapshots remain accessible. No Account is recreated, no source is retargeted, and no provider call is made.

Follow-up verification and remaining manual checks are recorded below. In an isolated app, select a deleted scope by date with keyboard/mouse, open its snapshot, and distinguish it from a same-name replacement and aggregate analysis. Check actual monitor scaling and screen-reader reading of the full label. Local automated verification does not establish live interaction or GitHub Actions acceptance.

Follow-up verification (2026-10-09):

- Branch `develop`, unchanged HEAD `46be768d41e04e162d1c6958aece124670999ab3`; initial worktree clean, final 12 modified tracked files. No commit/push/merge or real-journal access.
- Focused DailyReview Release: **208 passed**, zero failures/skips — Domain 13, Application 116, Infrastructure 52, Desktop 27.
- Complete parallel Release: **3,208 passed**, zero failures/skips — Domain 454, Application 645, Infrastructure 859, Desktop 1,250 (2 m 54 s). Existing test isolation/deadlines retained.
- Release build: zero warnings/errors; EF model consistency: no pending changes; `git diff --check` passed.
- Isolated migrated SQLite regression saves original exact and aggregate snapshots, deletes the source Journal/Account, creates a same-name replacement, disposes/recreates services and a fresh workspace, selects the historical option and opens its original snapshot. Exact, replacement and aggregate histories remain separate.
- Additional coverage: 28 unavailable scopes paged 25/3, duplicate analyses per scope, latest saved name, live/inactive exclusion, read-only count/page queries without JSON, cancellation, date changes, late discovery, paging without losing selected snapshot, discovery errors and current-evidence failures.
- Compiled WPF ComboBox selection opens the correct snapshot; full unavailable labels wrap and history paging buttons stay reachable. Light/Dark automated PNGs inspected at **960 × 760 DIP / 96 DPI** and **480 × 760 DIP / 240 DPI**. These are automated renders, not live mouse/keyboard or actual-monitor verification.
- During test development, the new selection test exposed a missing dispatcher synchronization context in its test setup; the test now uses the existing shared STA host with a WPF synchronization context and awaits commands outside its action. No production dispatcher behavior, assertion, deadline or retry policy was changed.
- Evidence: ignored `artifacts/m156-scopes/`, `artifacts/m156-scopes-*.log`. Live UI/assistive technology and GitHub Actions on the eventual user commit remain unverified.

Follow-up changed files:

- `README.md`, `docs/daily-review.md`
- `src/PersonalTradingJournal.Application/DailyReview/Coaching/SavedCoachingAnalysis.cs`
- `src/PersonalTradingJournal.Infrastructure/DailyReview/Coaching/CoachingAnalysisRepository.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/DailyReview/DailyReviewViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/DailyReview/ReviewPresentation.cs`
- `src/PersonalTradingJournal.Desktop/Views/DailyReview/DailyReviewView.xaml`
- `tests/PersonalTradingJournal.Application.Tests/DailyReview/SavedCoachingAnalysisTests.cs` (repository fake contract)
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/DailyReview/CoachingAnalysisRepositoryTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/DailyReview/DailyReviewSqliteTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/DailyReview/DailyReviewWorkspaceTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/DailyReview/DailyReviewViewTests.cs`

### M15.6 verification

Baseline and final HEAD: `4d80b975e32cec51cdf38da4b139f063e0f3a38b`, branch `develop`. The initial worktree was clean. No commit, push, merge, schema or economics changes were made.

- Focused Release: **354/354 passed** — Domain 13, Application 116, Infrastructure 51, Desktop 174. New coverage contributes 23 cases, including NY midnight/DST defaults, mixed currencies/strict missing Net, genuine zero, excluded open activity, inactive/unavailable selection, ten-row pages, confirmation/failure/deletion, saved-source survival after an isolated SQLite Journal deletion, stale current/history/detail results, exact-source navigation and the standalone Journal unsaved guard.
- Complete parallel Release: **3,203/3,203 passed**, zero failures/skips — Domain 454, Application 645, Infrastructure 858, Desktop 1,246. Desktop duration reported 2 m 29 s. No native-suite timeout occurred in this run; unchanged deadlines and isolation were retained.
- Release build: **0 warnings, 0 errors**. EF: **no pending model changes**. `git diff --check` and new-file whitespace checks passed (Git's LF-to-CRLF notices are not whitespace errors).
- Automated WPF: compiled bindings, shared foreground brushes (including template/expander content), wrapping, control reachability, snapshot separation and end-of-page scrolling passed. Synthetic current/history/snapshot PNGs were generated in both themes at **960 × 760 DIP / 96 DPI** and **480 × 760 DIP / 240 DPI**; representative current/history/snapshot renders were visually inspected across both themes/sizes. The initial renders exposed default black/unwrapped template text; explicit shared styles fixed it and assertions now guard it. These are RenderTargetBitmap layouts, not live mouse/keyboard or real-monitor DPI acceptance.
- Early focused runs caught the new callback cleanup being placed in the wrong lifecycle method and a test incorrectly requiring a native DatePicker template button to be independently focusable. Cleanup is now in Dispose; keyboard assertions target actual workspace commands, with DatePicker/Account focusability checked separately. Final focused/full runs are green.
- Original logs/TRX/PNG evidence remains in ignored `artifacts/m156/` and `artifacts/m156-*.log`. The original restart-discovery limitation is resolved by the follow-up above. Live UI, real monitor scaling and assistive technology remain separate from automated tests; CI must test the user's eventual commit/push.

Changed files (7 existing, 8 new):

- `README.md`
- `docs/daily-review.md`
- `src/PersonalTradingJournal.Desktop/App.xaml`
- `src/PersonalTradingJournal.Desktop/App.xaml.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/MainWindowViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Trades/TradesViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/DailyReview/DailyReviewViewModel.cs` (new)
- `src/PersonalTradingJournal.Desktop/ViewModels/DailyReview/ReviewPresentation.cs` (new)
- `src/PersonalTradingJournal.Desktop/Views/DailyReview/DailyReviewView.xaml` (new)
- `src/PersonalTradingJournal.Desktop/Views/DailyReview/DailyReviewView.xaml.cs` (new)
- `tests/PersonalTradingJournal.Desktop.Tests/Navigation/MainWindowViewModelTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Navigation/MainWindowDailyReviewTests.cs` (new)
- `tests/PersonalTradingJournal.Desktop.Tests/DailyReview/DailyReviewWorkspaceTests.cs` (new)
- `tests/PersonalTradingJournal.Desktop.Tests/DailyReview/DailyReviewViewTests.cs` (new)
- `tests/PersonalTradingJournal.Desktop.Tests/DailyReview/DailyReviewSqliteTests.cs` (new)

## M15.1: scope and evidence only

`IDailyReviewEvidenceReader.GetAsync(DailyReviewQuery, CancellationToken)` provides disconnected, read-only evidence for later calculated statistics and user-requested AI generation. This milestone makes no AI request, creates no generated prose or analysis record, and adds no Daily Review UI. Existing Journal review answers remain the user's saved content. Later M15 tasks must define calculation coverage, generation, persistence and presentation explicitly.

`DailyReviewQuery(DateOnly date, Guid? tradingAccountId = null)` requires an explicit **New York calendar date**. It has no machine-timezone default or retained selection. `Guid.Empty` and a date whose exclusive end cannot be represented are rejected before database access. `FromUtc` and `BeforeUtc` reuse `TradingCalendarDayQuery`, which delegates to the existing DST-safe Dashboard/TradingTimePolicy boundaries.

| Scope | Trades | Journals |
| --- | --- | --- |
| Exact Account ID | Only that original Account ID | Only that original Account ID; excludes null-scoped entries |
| All accounts (`null` query filter) | Every Account | Every Account **plus** the distinct null-scoped All accounts entry |

The returned query echoes the requested scope. An unavailable filter never falls back to All accounts. Inactive Accounts are included; historical dangling references retain their original IDs with unavailable metadata. A valid ID with no matching records returns empty lists. Journal write uniqueness and null-scoped editing retain their existing meaning; aggregation here does not move, merge or retarget entries.

## Trade day attribution and ordering

The Trade browse projection is maintained transactionally from the Domain by normal writes/imports. This reader copies its status, lifecycle and economics rather than recomputing outcomes from displayed prices.

- **ClosedOnDate:** fully closed Trades whose authoritative `ClosedAtUtc` is in `[New York midnight, next New York midnight)`. This is the same eligible set as Calendar day details. Opening on a previous date does not split P&L or attribute the Trade to that previous date. A closure exactly at next midnight belongs to the next day. Import `TradeDay` and current Instrument pricing do not participate.
- **OpenActivityOnDate:** currently open Trades, including partial exits, with at least one allocated execution inside those same UTC bounds. These are separate context, **not inputs to the selected day's realized closed-Trade metrics**. An older open position with no execution that day is omitted. No end-of-day valuation is inferred.
- **UnavailableLifecycleActivityOnDate:** a Trade with an absent projection, unavailable closure time or unsupported lifecycle status can be retained through an execution on that day. Its available facts and limitations are explicit; it must not be silently treated as a completed Trade.

The candidate query uses existence checks, not execution joins that multiply Trade rows. Each Trade ID appears once in a response. Several executions in the day do not increase the Trade count. Open activity may legitimately be context on several dates; it is not a second realized-day attribution. Once a Trade closes, subsequent reads follow its current closed status and closure date. This is **current persisted evidence**, not a reconstruction of which positions were open at a past midnight.

Spring and autumn transition days span 23 and 25 hours respectively. Stored UTC instants stay authoritative; repeated autumn clock times remain separate instants. Trade rows sort by recorded closure descending (null last), then Trade ID. Executions retain full lifecycle sequence order, then execution ID. Assigned Mistakes sort by definition ID and assignment ID. Journals sort null scope first, then Account ID and Journal ID; renaming an Account does not reorder evidence. Ordering never depends on Trades-list paging or UI sorting.

## Contract and provenance

`DailyReviewEvidence` contains the request and two read-only collections, `Trades` and `Journals`; it contains no combined P&L or inferred assessment.

Each `DailyReviewTradeEvidence` includes:

- Original Trade ID, Account and Instrument IDs, current names/activity/availability, optional Setup reference, and assigned Mistake definition/assignment IDs with assignment audit timestamps. A null Setup means unassigned; a reference with an ID and null metadata means unavailable. Assigned Mistakes are user classifications, not inferred violations.
- Historical pricing currency and point value, Trade creation/update UTC timestamps, inclusion reason, and nullable `DailyReviewTradeFacts`. Facts copy projection version, status/direction, opened/closed UTC instants, open quantity, average prices, total costs, Gross P&L and authoritative Net P&L.
- Full `TradeExecutionDetailItem` facts, reusing the Trade-details contract: stable execution ID, sequence, UTC instant, side, allocated quantity, price, nullable commission and fees, and available broker/order/execution references. Execution total cost uses the existing commission-plus-fees semantic; a missing component keeps it null. Full lifecycle context can include executions outside the selected date; their timestamps are preserved.
- `DailyReviewTradeQuality` flags for missing projection/executions/closure, unsupported projection format, unknown commissions/fees/Gross/Net, and unavailable references. A closed projection with unavailable P&L is retained, not changed into a zero result. These flags describe evidence limitations, not rule violations or a comprehensive corruption diagnosis. Open-Trade P&L being unavailable is expected Domain behavior.

No Net estimate is invented: unknown authoritative `NetPnL` remains null even when Gross is known. Genuine zero costs and zero P&L remain numeric zeros. Historical currencies stay on each Trade; the reader never sums or converts them. Any later use of the Dashboard Effective Net estimate must be an explicitly labeled calculation with its own coverage, not a replacement of these source values.

Each `DailyReviewJournalEvidence` includes original Journal ID, exact trading date and saved Account ID, current Account name/state, all four exact text fields, Draft/Completed state, durable revision number, and created/updated UTC timestamps. An absent entry is an absent list item. An existing entry with `Text == ""` or empty optional answers still exists. Legacy Completed entries with only answers remain readable. Whitespace and Unicode are not trimmed or rewritten. Only current content is read; revision browsing remains the separate Journal history contract.

Journal `(ID, Revision)` references its current durable revision. Trades have **no durable per-Trade revision token**: audit timestamps and source IDs are the available metadata. `ProjectionVersion` is the projection **format**, not a concurrency token. Reference metadata is current, not a historical name snapshot. Future persisted AI analysis must retain the actual evidence it used if reproducibility after edits/deletion is required; M15.1 does not pretend IDs/timestamps alone can restore old Trade content.

## Persistence, consistency and cancellation

`DailyReviewEvidenceReader` is registered by `AddPersistence`. Every call creates a fresh `JournalDbContext`, uses no-tracking projections and executes four SELECTs: selected Trades/references, executions, assigned Mistakes, and current Journals. Filtering is performed in SQL, using the same selected-Trade subquery for child batches. No query runs once per row; no arbitrary page or result cap silently drops evidence. Memory scales with the selected day's Trades and their full executions/Journals; future prompt-size policies must be explicit and must not redefine this complete source read.

A SQLite **deferred read transaction** establishes one database snapshot at the first SELECT. All four batches observe that snapshot, including when another connection commits between the Trade and Journal reads. It does not acquire an immediate writer reservation. The transaction and context are disposed after assembling the response; no SaveChanges, projection repair, schema update, Journal revision or other database write is performed. A new call reflects later commits. This boundary is intentionally SQLite-specific in Infrastructure.

Cancellation is checked before context creation, passed to connection/query operations, checked while assembling Trades and again before return. Cancellation or a read error throws; no partial response is published or retained, and another call remains usable. A malformed record with neither a usable day-attribution projection nor any dated execution cannot be assigned to a selected day and is omitted; this reader is not a whole-database integrity audit.

No migration is required: existing Trade/browse/execution/reference records and current Journal fields already contain the evidence. No Trade economics, import rules, Journal write/revision policy, Calendar summaries or navigation change.

## M15.2: statistics and data quality

`DailyReviewStatisticsCalculator.Calculate(DailyReviewEvidence, CancellationToken)` is a pure Application calculation. It consumes the complete M15.1 snapshot; it does not issue another read, update a projection or write data. The existing Dashboard/Calendar `PnlAccumulator` was moved unchanged into an internal shared class. Thus outcome formulas and undefined states are reused rather than independently reimplemented. M15.2 adds no UI, AI calls, recommendations, generated text, analysis persistence or schema migration.

### Inclusion, scope and result structure

- Only `ClosedOnDate` contributes to realized metrics. The calculator validates that these facts are Closed and their closure lies in the request's existing inclusive/exclusive UTC bounds. A cross-midnight Trade contributes its entire final P&L on its New York closure date, once; both DST transitions retain M15.1's 23-/25-hour boundaries.
- `OpenActivityOnDate` (including partial exits) and `UnavailableLifecycleActivityOnDate` are counted in separate excluded sets. No partial-exit realized P&L or open-position valuation is inferred. Closed Trades with missing economics remain in the closed denominator, not silently excluded.
- The requested Account scope is echoed. Exact-account evidence containing another Account is rejected. All accounts aggregates compatible values by original historical pricing currency, with nested Account-ID groups retaining current names/activity and original Trade IDs. Unavailable/inactive reference names do not change economics or scope.
- `DailyReviewStatistics.Population` gives nonmonetary counts and source-ID sets across all currencies. `Currencies` sorts by ordinal currency code; each currency has its own `Population`, `Gross`, `Net` and `Accounts`. Account groups sort by ID. There is deliberately **no all-currency monetary total** and no currency conversion.
- Every population/known/unavailable/win/loss/break-even set exposes ordered Trade IDs and a derived count. IDs sort ascending, including arithmetic inputs, so input enumeration order cannot change results. Duplicate Trade IDs, inconsistent closed-date attribution, noncanonical currency and out-of-scope evidence fail with `ArgumentException`, rather than publishing misleading statistics.
- Journals do not affect Trade statistics. Their exact current text, Draft/Completed state and durable revisions remain available in the source evidence; no Journal, an existing empty field, and an empty trading day retain their M15.1 meaning.

### P&L basis, coverage and formulas

Both `DailyReviewPnlStatistics` bundles contain the established `PnlMetrics` contract with an explicit `Basis` (Gross or Net), `Coverage` and source-ID sets. **No EffectiveNet estimate is exposed by M15.2.**

| Fact | Exact definition |
| --- | --- |
| Closed-Trade count | Number of unique fully closed, selected-day Trades, including Trades with unknown economics |
| Gross outcome | Authoritative GrossPnL > 0 is a win, < 0 is a loss, == 0 is break-even; null is unavailable, never break-even |
| Gross realized total | Sum of authoritative GrossPnL, only when every closed Trade has usable Gross |
| Strict Net outcome/total | Use authoritative NetPnL only with usable Gross, known nonnegative TotalCosts, at least one execution and no unknown commission/fee component or corresponding quality flag; every closed Trade must qualify for a complete total |
| Known subtotal | Sum of eligible known amounts only, explicitly paired with partial coverage; null when no eligible amount exists. It is **not** the complete total |
| Win Rate percent | 100 × known wins / **all closed Trades**, only with complete coverage for that basis; break-evens remain in the denominator |
| Average Win | Sum of positive amounts / winning-Trade count, with complete basis coverage; no wins is unavailable |
| Average Loss | Sum of absolute negative amounts / losing-Trade count, with complete basis coverage; a positive magnitude, no losses is unavailable |
| Profit Factor | Sum of positive amounts / sum of absolute negative amounts, with complete basis coverage and nonzero loss magnitude |

No losses with positive profit yields `NoLosses` and null Profit Factor, not infinity. All break-even Trades yield genuine zero total and 0% Win Rate, but unavailable averages and `AllBreakEven` Profit Factor. Losses with no wins produce a defined zero Profit Factor. Empty populations yield `Empty` coverage, null amounts/rates/averages and `NoTrades` states; an entirely empty day has no currency groups. A currency with only open/incomplete-lifecycle context still exists but its realized metrics are Empty. Coverage distinguishes Empty, Complete, Partial and Unavailable.

Missing/unsupported projection and unknown-Gross flags make both bases unavailable for that Trade. Missing executions or unknown cost components make Net unavailable even if an inconsistent projection contains numeric Net. An otherwise usable authoritative Gross remains available despite unknown costs or missing executions. Unknown Net never falls back to Gross. For normal supported evidence, Gross and strict Net agree with Calendar; Calendar's separately labeled EffectiveNet estimate is intentionally not equivalent.

### Quality and traceability

`Population.UnknownCommissions` and `UnknownFees` count distinct Trades across that population (including context); a Trade can appear in both. Empty execution lists imply both unknown components. `ClosedUnknownCosts` is the **union** of closed Trades missing either component or usable TotalCosts, not the sum of those two counts. Metric `Unavailable` sets also identify missing Gross/Net or unsupported projections. `SourceQuality` preserves each nonempty M15.1 quality flag with its source IDs. Quality flags are evidence limitations, never inferred rule violations.

All totals and ratios trace to `Known` IDs; wins/profits/average wins to `Wins`; losses/loss magnitudes/average losses to `Losses`; denominator to `Population.Closed`. Each complete currency total includes all of that currency's Account groups. No monetary statistic aggregates different currencies. Source journals/revisions remain on the input snapshot, not copied into unrelated statistics.

Arithmetic uses .NET decimal and the shared checked sums without presentation rounding. Decimal's finite scale/precision still applies to addition/division. Overflow (including a loss magnitude outside decimal's range or an unrepresentable ratio) throws `OverflowException`; no partial result, saturated amount or fabricated zero is returned. Cancellation is checked at input enumeration and group/metric processing and throws without a result. Consumers must handle cancellation, invalid evidence and overflow explicitly.

The calculator adds no new reads and cannot change the source snapshot. Existing source limits remain: current-state evidence, not historical as-of valuation; no durable Trade revision token; prompt-size policies and reproducible AI evidence storage belong to later milestones.

### M15.2 verification

Focused calculator regressions cover signed/zero outcomes, strict-Net coverage, +100/-40/0/+20 formulas and provenance, unknown components even with inconsistent numeric Net, null/unsupported economics, context-only/empty days, currencies/Accounts, deterministic input order, precision, overflow, invalid scope/duplicates and cancellation. Isolated migrated SQLite tests compare both bases and contributing Trade IDs against Calendar across both DST changes, cross-midnight and exact-boundary closures, mixed Accounts/currencies and unknown costs. The existing 75-Trade read-only-connection test also calculates statistics while retaining its four-SELECT/no-write assertions.

Local verification on `develop`, baseline `d2b0277494447829a807f951213a1a198c2a669a`: focused **94/94 passed** (13 Domain, 61 Application, 20 Infrastructure, including shared Dashboard/Calendar regressions). Complete parallel Release **3,046/3,046 passed**, zero failures/skips (454 Domain, 551 Application, 818 Infrastructure, 1,223 Desktop). Release build: **zero warnings/errors**. EF: **no pending model changes**. `git diff --check` and new-file whitespace checks passed. Logs/TRX are under ignored `artifacts/m152/` and `artifacts/m152-*.log`. No real journal, UI/AI acceptance or matching GitHub Actions verification is involved; these are local automated results.

## M15.3: coaching evidence and response contract

This milestone defines a provider-independent input/output boundary only. `CoachingEvidencePacketBuilder.Build(evidence, cancellationToken)` consumes one M15.1 snapshot and computes M15.2 statistics from the same defensively copied records. It does not accept separately supplied statistics that could describe a different population. It makes no provider calls, produces no coaching, persists no analysis, and adds no UI or database/schema changes.

### Immutable, scoped packet

A successful `CoachingPacketBuildResult` has status `Ready` and a `CoachingEvidencePacket`. Its get-only properties include `ContractVersion`, `PacketId`, typed `Content`, complete wire `Json`, and `Utf8ByteCount`. Every nested collection is a defensive read-only copy (including executions, classifications and calculated source-ID sets); mutating a caller's input lists cannot change a constructed packet. Strings/scalar source records remain unchanged.

The initial version is **daily-coaching.v1**, shared by packet and response. JSON uses camelCase properties, string enums, explicit nulls, exact decimal serialization and original dates/UTC instants. Packet identity is lowercase SHA-256 over the UTF-8 JSON object containing `contractVersion` and `content`, before adding `packetId` to the final envelope. It is a deterministic content fingerprint, **not** authentication or a persisted analysis ID. No build-time timestamp is injected. Identical evidence, regardless of input list order/current culture, produces identical packet JSON and fingerprint; content/revision changes produce a different fingerprint. This is initial contract identification, not the comprehensive prompt/model/version management reserved for M20.

Content has clearly separated sections:

- **Query:** the exact New York date, requested Account scope and existing DST-safe UTC bounds. Exact Account accepts only matching Trades/Journals. All accounts retains all original Account IDs and the distinct null-scoped Journals; no monetary currencies are merged.
- **CalculatedFacts:** the complete M15.2 result, including Gross/strict-Net basis, denominators, coverage, known versus unavailable sets, Account/currency groups, excluded open/partial activity and contributing Trade IDs. Unknown Net is never estimated here.
- **RecordedTradeFacts:** full supplied M15.1 Trade facts, source/execution IDs, historical pricing currency, nullable costs and economics, classifications, audit instants, inclusion reasons and quality flags. These are recorded facts, not AI instructions or independently verified market data.
- **UntrustedJournalObservations:** current exact Journal text and three answers, saved Account identity, Journal ID, durable revision, Draft/Completed state and audit timestamps. User-written explanations are observations, not verified trading facts. No Journal is an absent item, not an empty synthetic Journal.
- **MissingOrUncertainData:** explicit no-Trade/no-closed-Trade/no-Journal states, missing Journal scopes among Accounts represented by Trades plus the requested scope, empty/whitespace field names for each existing Journal, the lack of supplied Trade notes and the existing absence of durable Trade revisions. Under All accounts the null scope is checked independently. This is not a census of Accounts with no records in the snapshot. Unknown costs/Net and excluded lifecycle activity remain explicit in CalculatedFacts and per-Trade quality.
- **Sources:** an ordered citation catalog linking each accepted source identifier to its kind and available Account/currency/Trade/execution/Journal/revision identity.

Trade/Journal rows sort by Trade ID and null-first Account ID/Journal ID respectively; child execution/classification ordering follows M15.1. Source catalog sorts ordinally. Duplicate Trade/execution/Journal IDs, duplicate Journal scopes on the selected date, invalid revisions, wrong dates and foreign exact-Account records are rejected, not silently filtered.

### Source trust and size bounds

The packet's fixed `ContentHandling` text identifies all source strings as **untrusted data**, never instructions: Journal text/answers, reference names, broker strings and any future Trade notes. JSON serialization escapes source text so it cannot introduce packet properties. M15.1 has no Trade-note field, so v1 explicitly says `tradeNotesSupplied: false`; it does not pretend notes were inspected or fabricate them. Later provider integration must place source content only in a data role, maintain a trusted instruction boundary, and must not obey instructions embedded in evidence. Labeling/escaping alone does not guarantee prompt-injection resistance.

Version 1 limits:

| Boundary | Limit |
| --- | ---: |
| Complete packet, including version/fingerprint/envelope | 262,144 escaped UTF-8 bytes (256 KiB) |
| Trades / executions / Journals / assigned Mistakes | 500 / 5,000 / 100 / 5,000 |
| Response JSON | 65,536 UTF-8 bytes (64 KiB) |
| Each response section list | 12 items |
| Each summary/observation/suggestion/uncertainty text | Nonblank, at most 1,000 UTF-16 code units |
| Citations per item | 1–16 distinct supplied source IDs |
| Response JSON nesting | 48 levels |

Record limits are preflight limits, not a promise that those counts fit the byte limit. A bounded serializer measures the actual escaped JSON, not a character estimate or model tokenizer estimate. `TooLarge` returns no packet and a clear explanation: no records, fields, statistics or citations were dropped/truncated. `InvalidEvidence` and `CalculationOverflow` also return no packet; cancellation throws without a partial result. Required input programmer arguments are not optional. All four outcomes are distinct from a valid empty-day packet.

No evidence is silently narrowed to fit: a future caller must explain the failure or explicitly obtain a different supported Account/date request. Provider-specific context/token budgets, transport overhead, request-size limits and output reservations still require validation in later milestones. This byte limit does not guarantee fit in any particular model. The upstream M15.1 reader remains complete/unbounded for its selected date; packet limits do not alter reader/database semantics.

### Structured response and citation validation

`CoachingResponseValidator.Validate(json, packet, cancellationToken)` returns `IsValid`, a frozen typed `Response` only on success, and non-source-content error messages on failure. The required JSON shape is:

| Field | Shape / purpose |
| --- | --- |
| contractVersion | Exactly daily-coaching.v1 |
| packetId | Exactly the supplied packet fingerprint |
| daySummary | One concise `{text, sourceIds}` statement |
| executionObservations | Array of `{basis, text, sourceIds}` observations |
| behaviorObservations | Same observation shape |
| improvementSuggestions | Array of actionable proposed `{text, sourceIds}` statements |
| uncertainties | Array of `{text, sourceIds}` statements |

Observation basis is one of `CalculatedFact`, `RecordedTradeFact`, `UserWrittenJournalObservation`. Each observation's citations must **all** match its declared basis: calculated catalog items, Trade/execution items, or Journal items respectively. To contrast a calculation with a user's explanation, provide separate correctly labeled observations. Summary, suggestions and uncertainties may cite any supplied source kind. All items require citations, even advice, so their stated rationale remains traceable. Lists may be empty when evidence is insufficient; the contract does not force fabricated observations or suggestions. An empty day can cite `calculated:day` for absent evidence.

Catalog identifiers are exact, ordinal/case-sensitive strings:

- `calculated:day` — scoped population/statistics (no cross-currency total).
- `calculated:currency:{escapedCurrency}` and `calculated:currency:{escapedCurrency}:account:{accountIdN}` — explicit currency and optional exact Account metric groups, with their contributing Trade IDs.
- `trade:{tradeIdN}` and `execution:{executionIdN}` — recorded Trade/lifecycle facts.
- `journal:{journalIdN}:revision:{revision}` — current user-written fields at that exact durable revision.

The validator rejects unknown sources (including omitted, foreign or older Journal revisions), wrong packet/version, blank/oversized text, null/missing sections/items, duplicate citations, excess counts, malformed/deep/oversized JSON, duplicate property names, extra properties and integer/unknown enum values. It does not query the database to resolve a citation: only evidence actually supplied in this packet is valid. Thus a response for an earlier source snapshot cannot be attached to a newer packet merely because Trade IDs still exist.

**Passing validation proves structure and source membership, not that the cited material entails the wording.** It cannot verify whether prose invents profit, misreads a partial subtotal, attributes behavior correctly, contradicts an uncertainty, obeys an injected instruction or offers useful/actionable advice. Response text remains proposed, untrusted AI output. Future generation/presentation must preserve calculated facts as authoritative, make uncertainties visible and handle these semantic limits explicitly. No generated text is produced in M15.3. Neither a packet hash nor Trade audit timestamps restore old source content; future saved analyses must retain their actual evidence as required for reproducibility.

### M15.3 verification

Tests cover exact/aggregate scopes, mixed currencies, current Draft/Completed revisions, unknown costs, empty days, excluded open activity, absent/empty Journal fields, defensive immutability, deterministic ordering/culture, malicious source strings kept as data, record/wire limits, calculation overflow, response source/basis validation, response version/fingerprint, malformed/duplicate/extra properties and cancellation. The existing isolated migrated SQLite scope test now constructs packets from both aggregate and exact-account reads.

Local verification on clean-start `develop`, baseline `381f98eeefe0140669d94ac46d9c9220c8f791e3`: focused **77/77 passed** (13 Domain, 52 Application, 12 Infrastructure). Complete parallel Release **3,076/3,076 passed**, zero failures/skips (454 Domain, 581 Application, 818 Infrastructure, 1,223 Desktop). Release build: **zero warnings/errors**. EF: **no pending model changes**. `git diff --check` and new-file whitespace checks passed. Logs/TRX are under ignored `artifacts/m153/` and `artifacts/m153-*.log`. No real journal, AI provider, live UI or GitHub Actions acceptance is involved; these are local automated results.

## M15.4: manual generation and OpenAI provider

`DailyCoachingGenerationService.GenerateAsync(CoachingEvidencePacket, CancellationToken)` is the explicit-only Application entry point. It accepts the already immutable M15.3 packet, not a date that could silently re-read different evidence. It calls `ICoachingProvider` at most once and applies the complete M15.3 validator before returning a typed Review. Only Success contains a Review. The provider's raw JSON is an internal transport result, never an accepted review.

There are no generation hooks in startup, Calendar/Journal reads, edits, refresh, imports or navigation. Desktop registers the service lazily but has no action wired to it. A later **Generate AI Review** UI must explain before invocation that the selected Trade and Journal evidence leaves the machine for the configured provider. There is no automatic generation, analysis persistence, UI or migration here; all existing local workflows remain independent of API configuration and availability.

### Provider, configuration and request boundary

The initial adapter uses one HTTPS POST to OpenAI Responses with the pinned `gpt-4.1-mini-2025-04-14` snapshot. Official documentation checked on 2026-10-08 confirms Responses/structured-output support, a 1,047,576-token context and maximum 32,768 output tokens: [model profile](https://developers.openai.com/api/docs/models/gpt-4.1-mini), [structured outputs](https://developers.openai.com/api/docs/guides/structured-outputs), [Responses parameters](https://developers.openai.com/api/reference/resources/responses/methods/create), [error codes](https://developers.openai.com/api/docs/guides/error-codes).

Configuration for a future explicit caller:

1. In Desktop use **Settings → AI Coaching** (protected saved key first); `OPENAI_API_KEY` remains a development fallback when no saved key exists. A service-only host defaults to that environment source unless it registers `ICoachingCredentials`. Never paste credentials into repository files, database rows, logs, screenshots or bug reports. The adapter resolves the credential only when explicitly called; missing credentials do not prevent local application use.
2. Resolve `DailyCoachingGenerationService` after `AddDailyCoaching()`, build a successful M15.3 packet from the desired read-only snapshot, and explicitly invoke GenerateAsync with cancellation. No Desktop button is enabled in this milestone.
3. Optional nonsecret DI configuration is `CoachingGenerationOptions` (default 90 seconds, positive and at most 180 seconds) and `OpenAiCoachingOptions` (8,192 output tokens, 300,000 input-token budget). Register overrides after AddDailyCoaching. Only the verified model profile is supported; arbitrary model strings are rejected rather than assuming compatible limits/schema.

The preflight uses the **whole serialized request's UTF-8 byte length plus 16,384 framing reserve** as a conservative input-token ceiling, including escaped evidence, instructions and schema. This is deliberately not an exact tokenizer/price estimate and can reject some otherwise fitting packets. The configured input budget plus reserved output must fit the verified model context. The server's context-length error is also handled. No evidence, statistic or currency/account group is truncated; changing scope requires a separate explicit user choice.

One user data message contains exactly `packet.Json`, with trusted instructions separately in `instructions`. Instructions distinguish calculated facts from recorded Trades, self-reported Journal observations and missing evidence; require supplied citations; prohibit obeying source-text instructions, fabricated economics or inferring violations from missing data. No screenshots, other dates, external tools, conversation ID or previous response is supplied. M15.1 still has no Trade notes; none are fabricated. Structured output uses a strict JSON schema with required properties and no additional properties, then local validation enforces the stronger M15.3 limits/basis/citation rules.

The request is non-streaming, non-background, with `store:false` and `truncation:disabled`. The dedicated HTTP transport has no logging/retry middleware, cookies or redirects. Disabling response storage is **not** a guarantee of zero provider retention; provider/account data policies still apply. No application log records credentials, Journal text, Trade source strings, prompt bodies, responses or provider exception/error-body text.

### Outcomes, metadata and limits

Distinct sanitized outcomes include MissingCredentials, InvalidConfiguration, AuthenticationFailed, AccessDenied, ModelUnavailable, InvalidRequest, InputTooLarge, RateLimited, QuotaExceeded, ServiceUnavailable, ProviderFailure, Refused, IncompleteResponse, InvalidResponse, Cancelled and TimedOut. Messages describe the next manual action without echoing raw provider errors. Known allowlisted error codes take precedence over generic HTTP status classification; unknown codes remain unknown. HTTP 429 quota codes are distinct from temporary rate limiting; available RetryAfter is returned but never scheduled automatically.

Cancellation and the bounded deadline cover the request, body read and validation. Late completion cannot become a successful result. Neither cancellation nor timeout proves the provider did not process/bill an accepted request. There is no automatic retry, repair generation or follow-up request; the user must decide whether to incur another request.

Completed envelopes must contain exactly one completed assistant output-text message for the pinned model. Incomplete, refused, malformed, oversized, mismatched-version/fingerprint and unsupported-citation responses never return a partial coaching review. Envelopes are bounded to 1 MiB and review JSON to M15.3's 64 KiB. Citation validation establishes membership, **not semantic truth, useful advice or immunity to prompt injection**; generated prose remains untrusted and cannot override authoritative calculations.

Metadata returns provider/model, a generated client request ID, allowlisted request/response IDs, HTTP status, retry delay and available input/cached-input/output/total token counts. Missing or inconsistent usage stays unknown, not zero. Metadata may be unavailable when cancellation wins before the adapter returns it. **MonetaryCost is always null/Unknown in M15.4:** no verified pricing configuration is installed and no list-price assumption is made. Future pricing/history work must account for model, cache and applicable billing terms. Secrets and raw errors are not metadata.

### M15.4 verification

Automated tests use fake providers and in-memory HTTP handlers only: explicit invocation, whole-packet/schema wire contract, valid/invalid citations and identities, incomplete/refused envelopes, HTTP status/quota/context failures, credentials/configuration preflight, bounded responses, metadata/unknown cost, cancellation, deterministic timer-driven timeout and ignored late results. No live paid API request or real journal is used.

Local verification on clean-start `develop`, baseline `71343e118f0b2c10b5a1945219d03930af058693`: focused **132/132 passed** (13 Domain, 75 Application, 44 Infrastructure). Complete parallel Release **3,131/3,131 passed**, zero failures/skips (454 Domain, 604 Application, 850 Infrastructure, 1,223 Desktop). Release build: **zero warnings/errors**. EF: **no pending model changes**. Tracked/new-file whitespace checks passed. Logs/TRX remain in ignored `artifacts/m154/` and `artifacts/m154-*.log`. The first build caught one nullable assertion in a new test, corrected before these passing runs. Live provider availability/billing, future UI consent and GitHub Actions acceptance remain unverified, separate gates. No Desktop interactive acceptance is claimed.

## M15.5: saved analyses and source snapshots

### Explicit generation and atomic storage

Future manual generation callers use `GenerateAndSaveCoachingService.GenerateAsync(packet, cancellationToken)`. It calls the M15.4 generator once, captures the successful completion time in UTC, revalidates the structured response against that exact immutable packet, and commits through `ICoachingAnalysisRepository`. It returns **Saved** only after commit, with the saved analysis identity. M15.4's lower-level generation result alone is not a storage acknowledgement. Nothing is generated/saved by registration, startup, local source reads, editing or history browsing.

Failed, refused, cancelled, timed-out, oversized and invalid generations do not reach the repository. `CoachingAnalysisSnapshot.Create` accepts only Success with a response and provider identity; it rechecks contract/version/fingerprint/citations and freezes the response as JSON before any write. A caller cannot mutate a response list after creation to change the pending snapshot. Missing provider identity or inconsistent metadata is not a saveable success.

The new independent **CoachingAnalyses** table contains response and evidence in the same row, committed in one explicit SQLite transaction. Failure/cancellation after insertion but before commit rolls back the whole record. There is no update API. A duplicate analysis ID cannot overwrite history; separate explicit generations can have identical evidence fingerprints and distinct analysis IDs. No automatic retry, retention, analysis cascade or source write occurs. Storage failure returns StorageFailed with no purported saved review; it never automatically repeats the paid generation. Cancellation racing with commit can have an uncertain caller outcome: inspect history before requesting another generation. Once commit is acknowledged, later cancellation does not retroactively undo that save.

### Immutable historical contract

`SavedCoachingAnalysis` contains:

- `Summary`: stable analysis ID, explicit New York ReviewDate, `Scope.Kind` (**AllAccounts** or **ExactAccount**) and original nullable AccountId, available snapshot Account display name, GeneratedAtUtc, provider and model.
- EvidenceContractVersion, ResponseContractVersion and the M15.3 PacketId fingerprint.
- **EvidenceJson**: exactly the `packet.Json` string supplied to the provider. No regeneration, rounding, filtering or recalculation on save/read. It contains original Trade/execution IDs and facts, account/reference metadata, contributing IDs, calculated statistics/coverage, missing-data flags, currencies and current-at-generation Journal text, answers, Draft/Completed state, identity and durable revision.
- **ResponseJson**: the validated structured coaching object serialized in the response contract format, not a raw HTTP envelope.
- **Metadata**: allowlisted provider/model, client/request/response IDs and available input/cached-input/output/total token counts. Missing usage remains null/unknown. API keys, HTTP bodies, provider error messages, retry diagnostics and arbitrary generation messages are not fields in this saved contract. Monetary cost remains unknown; no unverified pricing is introduced.

The summary name is copied from the evidence, never resolved from mutable Accounts after generation. An exact-account packet with no named source retains its original ID and a null display name (name not supplied), rather than inventing a current/historical name. AllAccounts is labeled “All accounts”; original per-record Account names/identities and activity/availability states remain in its evidence. An AllAccounts **analysis** summarizes every included Account plus null-scoped Journals; it is not the distinct null-scoped Journal entry.

Historical consumers must read the stored, versioned JSON. Loading never queries current Trades/Journals/Accounts, rebuilds statistics or runs today's packet builder/validator to replace historical meanings. JSON is returned intact so future readers can choose the appropriate version decoder; unsupported future versions must be identified explicitly, not interpreted as the current contract. No M15.5 UI/renderer is added. Saved prose remains untrusted AI output; valid source references do not prove its claims.

### Read, paging and deletion

`ICoachingAnalysisRepository` supplies:

- `SaveAsync(CoachingAnalysisSnapshot)`: immutable validated write boundary; returns only a committed analysis.
- `GetAsync(id)`: one complete saved snapshot, or null if absent/deleted. No source joins or writes.
- `BrowseAsync(CoachingAnalysisHistoryQuery)`: exact review date and explicit analysis scope. AllAccounts selects **only aggregate analyses**, not a mixture with individually generated analyses. ExactAccount selects only that original ID, including unavailable/deleted Accounts. Default 20 rows, allowed page size 1–100, positive overflow-checked page; out-of-range pages return empty Items with the accurate TotalCount. Order: GeneratedAtUtc descending, then analysis ID ascending. Count/page use one deferred SQLite read transaction; summary queries exclude JSON payloads. HasPrevious/HasNext accompany the immutable page.
- `DeleteAsync(id)`: one atomic permanent logical deletion; true if removed, false if missing. The future UI must confirm the exact analysis before calling. Other analyses, Trades, Journals and their revisions are untouched.

All operations accept cancellation. The composite date/scope/account/time/ID index supports bounded database paging, not per-account page merging or per-source reads. There are **no foreign keys to mutable source records**, so renaming/deactivating/deleting a source or Account cannot cascade to or retarget an analysis. The database scope constraint rejects inconsistent kind/account combinations.

### Migration and sensitive local information

Additive migration `20261008195353_AddCoachingAnalysisSnapshots` creates only CoachingAnalyses, its scope constraint and history index. It changes no Trade/Journal/import columns or economics. Downgrade drops analysis history but leaves existing source tables/data intact; normal startup uses the established migration initializer. No real database was accessed during implementation.

The local SQLite database now retains **copies of financial facts, account names, Journal prose/answers and AI coaching**. These remain after source edits or deletion until the user explicitly deletes that analysis. No additional encryption or automatic redaction is introduced: protect the data directory and backups with OS permissions/encryption appropriate to sensitive data. Do not put secrets in Journal text; exact evidence snapshots necessarily preserve source content, including any secrets a user wrote there. Provider credentials are never intentionally included in metadata or logs.

Analysis deletion is not forensic secure erasure: SQLite pages/WAL, exported files and backups may retain older content. There is no automatic retention or backup erasure. Request/token metadata is informational and does not establish verified monetary cost. Prompt/version management beyond the existing evidence/response contracts remains M20; saved analyses do not contain the raw HTTP request/instruction envelope.

### M15.5 verification

Tests use fake generation and isolated migrated SQLite databases, including source edits/deletes, exact/aggregate scopes, separate currencies and unknown Net, original names/revisions/status, deterministic bounded paging, read-only connections, duplicate-ID protection, single-analysis deletion, invalid/failed generation rejection, and injected post-insert failures/cancellation proving rollback. Migration tests check the additive table/constraint, no source foreign keys, round-trip upgrade/downgrade and model consistency. No paid provider or live UI acceptance is claimed.

Local results on clean-start `develop`, HEAD `e02c0a0e8deaaf8864b48e925b1ff4f80d01794b`: **208/208 focused tests passed** (13 Domain, 116 Application, 79 Infrastructure); final complete parallel Release **3,180/3,180 passed**, zero failures/skips (454 Domain, 645 Application, 858 Infrastructure, 1,223 Desktop). Release build: **zero warnings/errors**. EF: **no pending model changes**. Tracked/new-file whitespace checks passed. The milestone adds 49 tests. Results are local, not GitHub Actions or interactive acceptance.

The first full run had 3,175 passes and five failures: four outdated migration/schema inventory assertions, updated to include the ninth migration/new record, and the unchanged `JournalViewTests.CompiledEditorBindingsScopeVetoAndLightDarkNormalHighDpiLayouts` child exceeding VSTest's 30-second inactivity bound. Breadcrumbs showed ongoing rendering and “assertions completed” at **30.269 s**, not a stalled analysis storage operation. That case passed isolated (reported test duration **9 s**) and in the final parallel run (assertions **14.315 s**, child process **18.731 s**). No WPF code, coverage, retries or deadlines were changed; this does not establish that the pre-existing timing variance is resolved. Earlier migration-stage tests are now explicitly pinned to their intended historical migration rather than implicitly migrating to the latest schema.

TRX/logs and the first WPF timeout's phase trace/dump remain under ignored `artifacts/m155/` (`full`, `journal-diagnostic`, `focused-verified`, `full-final`) and `artifacts/m155-*.log`. A new GitHub run must test the user's eventual commit/push; live provider, future UI/history rendering and consent remain unverified.

## M15.1 verification

Baseline: clean `develop`, `b04ba12a0fef2a7c28cdd5a19e4ed0ebe586b429`. Focused Release: **15/15 passed**, zero failures/skips (5 Application, 10 Infrastructure). Tests use isolated migrated SQLite databases and cover:

- Exact and aggregate scope, null-scoped and inactive/unavailable Account journals, source identities and current revision refresh.
- Calendar-equivalent closure selection, cross-midnight Trades, both DST changes and precise inclusive/exclusive boundaries, deterministic ties and unique Trade rows.
- Open/partial activity versus realized closed results, known/unknown cost components, mixed currencies, zero P&L, missing projections/executions/economics/references, empty days/fields and legacy entries.
- A 75-Trade date with four SELECTs on an actual read-only SQLite connection; no tracked entities or writes.
- Cancellation before/between queries, subsequent reuse, and a concurrent WAL commit proving the four reads cannot mix database snapshots.

Full parallel Release: **3,027/3,027 passed**, zero failures/skips — Domain 454, Application 534, Infrastructure 816, Desktop 1,223. Release build passed with **zero warnings/errors**; EF reports **no pending model changes**; `git diff --check` passed. Synthetic TRX/log evidence is retained under ignored `artifacts/m151/` and `artifacts/m151-*.log`. No UI or AI interaction is part of this milestone, and no real journal was used for verification. These are local results; GitHub Actions must verify the user's eventual commit/push.
