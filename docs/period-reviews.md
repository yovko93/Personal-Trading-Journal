# Weekly and Monthly Review — M19.1–M19.3

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
