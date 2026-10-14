# Weekly and Monthly Review — M19.1

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

Weekly/Monthly UI, further statistics, provider requests and saved analyses are deferred to later milestones. No database migration or backup/export format change is needed for this read-only contract.
