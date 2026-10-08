# Daily Review with AI Coaching — M15

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

1. Supply `OPENAI_API_KEY` to the application's process environment through a trusted external secret-management/launch mechanism. Never paste credentials into repository files, database rows, logs, screenshots or bug reports. The adapter reads it only when called; missing credentials do not prevent local application use.
2. Resolve `DailyCoachingGenerationService` after `AddDailyCoaching()`, build a successful M15.3 packet from the desired read-only snapshot, and explicitly invoke GenerateAsync with cancellation. No Desktop button is enabled in this milestone.
3. Optional nonsecret DI configuration is `CoachingGenerationOptions` (default 90 seconds, positive and at most 180 seconds) and `OpenAiCoachingOptions` (8,192 output tokens, 300,000 input-token budget). Register overrides after AddDailyCoaching. Only the verified model profile is supported; arbitrary model strings are rejected rather than assuming compatible limits/schema.

The preflight uses the **whole serialized request's UTF-8 byte length plus 16,384 framing reserve** as a conservative input-token ceiling, including escaped evidence, instructions and schema. This is deliberately not an exact tokenizer/price estimate and can reject some otherwise fitting packets. The configured input budget plus reserved output must fit the verified model context. The server's context-length error is also handled. No evidence, statistic or currency/account group is truncated; changing scope requires a separate explicit user choice.

One user data message contains exactly `packet.Json`, with trusted instructions separately in `instructions`. Instructions distinguish calculated facts from recorded Trades, self-reported Journal observations and missing evidence; require supplied citations; prohibit obeying source-text instructions, fabricated economics or inferring violations from missing data. No screenshots, other dates, external tools, conversation ID or previous response is supplied. M15.1 still has no Trade notes; none are fabricated. Structured output uses a strict JSON schema with required properties and no additional properties, then local validation enforces the stronger M15.3 limits/basis/citation rules.

The request is non-streaming, non-background, with `store:false` and `truncation:disabled`. The dedicated HTTP transport has no logging/retry middleware, cookies or redirects. Disabling response storage is **not** a guarantee of zero provider retention; provider/account data policies still apply. No application log records credentials, Journal text, Trade source strings, prompt bodies, responses or provider exception/error-body text.

### Outcomes, metadata and limits

Distinct sanitized outcomes include MissingCredentials, InvalidConfiguration, AuthenticationFailed, AccessDenied, InputTooLarge, RateLimited, QuotaExceeded, ServiceUnavailable, ProviderFailure, Refused, IncompleteResponse, InvalidResponse, Cancelled and TimedOut. Messages describe the next manual action without echoing raw provider errors. HTTP 429 quota codes are distinct from temporary rate limiting; available RetryAfter is returned but never scheduled automatically.

Cancellation and the bounded deadline cover the request, body read and validation. Late completion cannot become a successful result. Neither cancellation nor timeout proves the provider did not process/bill an accepted request. There is no automatic retry, repair generation or follow-up request; the user must decide whether to incur another request.

Completed envelopes must contain exactly one completed assistant output-text message for the pinned model. Incomplete, refused, malformed, oversized, mismatched-version/fingerprint and unsupported-citation responses never return a partial coaching review. Envelopes are bounded to 1 MiB and review JSON to M15.3's 64 KiB. Citation validation establishes membership, **not semantic truth, useful advice or immunity to prompt injection**; generated prose remains untrusted and cannot override authoritative calculations.

Metadata returns provider/model, a generated client request ID, allowlisted request/response IDs, HTTP status, retry delay and available input/cached-input/output/total token counts. Missing or inconsistent usage stays unknown, not zero. Metadata may be unavailable when cancellation wins before the adapter returns it. **MonetaryCost is always null/Unknown in M15.4:** no verified pricing configuration is installed and no list-price assumption is made. Future pricing/history work must account for model, cache and applicable billing terms. Secrets and raw errors are not metadata.

### M15.4 verification

Automated tests use fake providers and in-memory HTTP handlers only: explicit invocation, whole-packet/schema wire contract, valid/invalid citations and identities, incomplete/refused envelopes, HTTP status/quota/context failures, credentials/configuration preflight, bounded responses, metadata/unknown cost, cancellation, deterministic timer-driven timeout and ignored late results. No live paid API request or real journal is used.

Local verification on clean-start `develop`, baseline `71343e118f0b2c10b5a1945219d03930af058693`: focused **132/132 passed** (13 Domain, 75 Application, 44 Infrastructure). Complete parallel Release **3,131/3,131 passed**, zero failures/skips (454 Domain, 604 Application, 850 Infrastructure, 1,223 Desktop). Release build: **zero warnings/errors**. EF: **no pending model changes**. Tracked/new-file whitespace checks passed. Logs/TRX remain in ignored `artifacts/m154/` and `artifacts/m154-*.log`. The first build caught one nullable assertion in a new test, corrected before these passing runs. Live provider availability/billing, future UI consent and GitHub Actions acceptance remain unverified, separate gates. No Desktop interactive acceptance is claimed.

## M15.1 verification

Baseline: clean `develop`, `b04ba12a0fef2a7c28cdd5a19e4ed0ebe586b429`. Focused Release: **15/15 passed**, zero failures/skips (5 Application, 10 Infrastructure). Tests use isolated migrated SQLite databases and cover:

- Exact and aggregate scope, null-scoped and inactive/unavailable Account journals, source identities and current revision refresh.
- Calendar-equivalent closure selection, cross-midnight Trades, both DST changes and precise inclusive/exclusive boundaries, deterministic ties and unique Trade rows.
- Open/partial activity versus realized closed results, known/unknown cost components, mixed currencies, zero P&L, missing projections/executions/economics/references, empty days/fields and legacy entries.
- A 75-Trade date with four SELECTs on an actual read-only SQLite connection; no tracked entities or writes.
- Cancellation before/between queries, subsequent reuse, and a concurrent WAL commit proving the four reads cannot mix database snapshots.

Full parallel Release: **3,027/3,027 passed**, zero failures/skips — Domain 454, Application 534, Infrastructure 816, Desktop 1,223. Release build passed with **zero warnings/errors**; EF reports **no pending model changes**; `git diff --check` passed. Synthetic TRX/log evidence is retained under ignored `artifacts/m151/` and `artifacts/m151-*.log`. No UI or AI interaction is part of this milestone, and no real journal was used for verification. These are local results; GitHub Actions must verify the user's eventual commit/push.
