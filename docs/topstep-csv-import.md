# Topstep CSV Source Rows, Candidates and Economics

M11.1 provides a read-only source boundary: Application owns `ITopstepCsvParser`, immutable `TopstepSourceRow` records, and structured parse results; Infrastructure implements `TopstepCsvParser`. It is not connected to the Desktop Import page yet. That page continues to accept the existing Tradovate matched-fill format only.

No Topstep Trades, executions, reference data, import history, or database records are created. There is no Topstep preview/confirmation workflow at this stage. One accepted CSV source row produces one normalized row; matching timestamps do not group rows or establish broker execution identities.

M11.2 adds `ITopstepTradeCandidateReconstructor` and the independent Infrastructure implementation `TopstepTradeCandidateReconstructor`. It prepares **reported closed-row candidates**, not verified account-level positions. The result can be ready for further row-level review without establishing automatic-import eligibility. It does not call Tradovate reconstruction or change the M11.1 parser.

M11.3 adds the pure Application `TopstepEconomicsReconciler`. It reconciles each existing candidate against caller-verified Instrument pricing and an explicitly verified cost interpretation. It neither changes the source rows/candidates nor makes them importable by itself. The parser still requires every financial field; missing costs are rejected, never normalized to zero.

## Supported schema

The following case-sensitive headers are required; order may vary:

```text
Id,ContractName,EnteredAt,ExitedAt,EntryPrice,ExitPrice,Fees,PnL,Size,Type,TradeDay,TradeDuration,Commissions
```

Empty, duplicate, malformed, or missing required headers make the file unusable. Extra columns are ignored with a warning. This is a distinct schema from Tradovate, not a reinterpretation of matched-fill rows. The parsers share only the existing CSV syntax tokenizer, whose behavior is unchanged.

UTF-8 with or without a BOM, CRLF/LF line endings, quoted commas, escaped double quotes, and multiline quoted fields are supported. Blank lines are ignored; record indexes are one-based data-record indexes and line numbers identify the physical starting line. Values are trimmed at field boundaries. Malformed quoting and wrong field counts reject the affected logical record; an unclosed quote can consume the remainder of the input as one malformed record.

## Normalized field meanings

| CSV field | Normalized meaning |
| --- | --- |
| `Id` | Required, case-sensitive source identifier string; leading zeros remain intact. Not a PTJ Trade ID or broker fill ID. |
| `ContractName` | Required broker contract name preserved without canonical Instrument resolution. |
| `EnteredAt`, `ExitedAt` | Explicit-offset `SourceEnteredAt` / `SourceExitedAt` plus zero-offset `EnteredAtUtc` / `ExitedAtUtc` derived from the same instants. |
| `EntryPrice`, `ExitPrice` | Exact reported decimals, without display rounding or economics validation. |
| `Fees` | Independent required `SourceReportedFees` decimal; not assumed to include or exclude commissions. |
| `PnL` | Required `SourceReportedPnL` decimal, including positive, zero, and negative values. Not recalculated or assigned to Trade Gross/Net P&L. |
| `Size` | Positive decimal source quantity. M11.1 does not infer grouping, aggregate exposure, or impose a whole-contract rule. |
| `Type` | Exactly `Long` or `Short`, represented by `TopstepTradeType`. No Buy/Sell or numeric aliases. |
| `TradeDay` | `SourceTradeDay` preserves the broker timestamp and offset; `BrokerTradingDate` takes its calendar date without timezone conversion. |
| `TradeDuration` | Non-negative invariant `TimeSpan` and retained source text. Not recomputed from rounded timestamps. |
| `Commissions` | Independent required `SourceReportedCommissions` decimal, not combined with Fees. |

Every field is required. Numeric syntax uses an optional leading sign and dot decimal separator, without currency symbols, digit grouping, or exponent notation. Values outside exact `System.Decimal` precision/range are rejected rather than rounded. Reported price/PnL/cost signs are preserved; M11.1 does not invent cost semantics or silently replace missing costs with zero.

## Time policy

`EnteredAt`, `ExitedAt`, and `TradeDay` use `MM/dd/yyyy HH:mm:ss zzz`, including an explicit numeric UTC offset. Missing offsets are invalid; neither the machine timezone nor a named zone is guessed. Exit must not precede entry when compared as instants; equal instants are allowed at the export's one-second precision.

A source `+03:00` is used directly to normalize entry/exit to UTC. A `TradeDay` carrying `-05:00` remains exactly the broker's date label and source offset, even during New York daylight-saving time. It is not silently corrected to `-04:00`, shifted to another date, or interpreted through Tradovate's Europe/Sofia policy.

`TradeDuration` uses invariant `[d.]hh:mm:ss[.fffffff]` syntax. Seven fractional-second digits are retained. Duration may disagree with the difference between second-precision displayed timestamps; that difference is not an error and is not used for grouping or reconstructing hidden executions.

## Diagnostics and duplicate IDs

Results contain accepted rows, source/valid/rejected counts, header usability, and complete-input validity. Any error or rejected record makes the complete input invalid, even if other valid rows remain available for inspection. Empty input and header-only files are not valid preparation inputs.

Diagnostics contain severity, a stable code, logical record index, physical source line, field name where applicable, and an actionable message. Codes cover `EMPTY_INPUT`, `NO_DATA_ROWS`, `INVALID_ENCODING`, `INVALID_HEADER`, `MISSING_HEADER`, `DUPLICATE_HEADER`, `ADDITIONAL_HEADER`, `INVALID_ROW_SHAPE`, `REQUIRED_VALUE`, `INVALID_DECIMAL`, `INVALID_SIZE`, `UNSUPPORTED_TYPE`, `INVALID_TIMESTAMP`, `TIMESTAMP_ORDER`, and `INVALID_DURATION`.

Repeated IDs within one file are explicit errors:

- `DUPLICATE_ID`: the same ID repeats with identical decoded, trimmed required source fields.
- `CONFLICTING_ID`: the same ID repeats with different required source fields, including differences in source representation. This conservative check does not guess equivalence or choose one version.

All occurrences of the repeated ID are rejected, with diagnostics referring to source lines rather than echoing the identifier. IDs in well-shaped rows still participate in conflict detection if another field is invalid. Ignored extra columns do not affect duplicate comparison. No cross-import identity lookup or deduplication is implemented; that belongs to M11.6.

The parser accepts a caller-owned readable stream, leaves it open, honors cancellation during asynchronous reading and normalization, and propagates cancellation and I/O failures. It has no database, filesystem-writing, logging, or import-service dependency. Diagnostics do not include complete customer rows or source identifiers; consumers must not log normalized source records as diagnostic payloads.

## Verification and boundaries

### M11.2 supported row semantics and grouping decision

The available source evidence establishes a reported closed quantity: one Id, contract, Long/Short type, entry/exit instants, entry/exit prices, Size, and attached reported PnL/cost/date/duration fields. It does **not** establish that the row is an entire broker position or an original broker fill. The schema has no shared entry-fill ID, order ID, position ID, or account-position history. Unique numeric source IDs are treated as opaque row identities, not chronology or evidence that nearby IDs belong together.

Apparent partial closes are not sufficient proof of a shared entry. For example, quantities 2 and 3 with the same entry time/price could be portions of one opening quantity of 5, or separately reported activity. Distinct IDs do not establish which grouping is correct. Rows sharing an entry second may also have different entry prices. Subsecond duration is not used to invent finer entry/exit timestamps or order those rows.

The conservative representation is **one `TopstepTradeCandidate` per source row**:

- `SourceRow` retains the complete immutable normalized record, including source Id, record index, physical line, reported PnL, Fees, Commissions, TradeDay, duration, and original offsets.
- `Direction`, `Quantity`, `EntryPrice`, `ExitPrice`, `OpenedAtUtc`, and `ClosedAtUtc` expose that row's exact economics and instants. There are no generated broker/execution identifiers, reconstructed fills, quantity allocations, averaged prices, or computed Gross/Net PnL.
- Source quantity is represented once per row, not merged, netted, split, or deduplicated by timestamps. The sum of candidate quantities is the sum of source-row closed quantities, **not** account peak exposure or a verified opening-fill total.
- Candidate order follows source record indexes solely for deterministic traceability. CSV order, source Id order, shared timestamps, and TradeDay do not establish a fill sequence.
- Partial-close-like rows, overlapping rows, equal-time round trips, and Long/Short transitions retain their individual reported facts. Opposing directions are not turned into a reversal fill or netted against one another.

The existing Domain can represent a row-local closed quantity as a directional opening quantity followed by an equal opposite quantity, including equal timestamps with an explicit sequence. It has no cross-Trade prohibition on overlapping intervals. Synthetic tests demonstrate that this representation preserves row-local prices, quantity, direction, and gross economics. This is a **representation proof**, not reconstruction of the broker's executions; M11.2 creates no Domain Trade or execution. A row candidate's `ArePositionBoundariesVerified` and the result's `IsPositionGroupingVerified` remain false. Later import work must preserve this granularity/provenance rather than presenting it as recovered complete broker-position history.

### Reconstruction diagnostics and safety

`TopstepTradeReconstructionResult.Source` retains the parse result and its diagnostics. Reconstruction diagnostics carry source record/physical-line pairs, not broker IDs. Candidate-to-row references and diagnostic-to-row references allow every decision to be inspected without logging customer rows.

| Code | Meaning / behavior |
| --- | --- |
| `POSITION_BOUNDARIES_UNVERIFIED` | Warning for all candidates: account-level flat-to-flat boundaries and source completeness are not established, even for isolated rows. |
| `POSITION_GROUPING_AMBIGUOUS` | Warning on each connected set of same-contract intervals that overlaps or touches at reported timestamp precision. Keep separate row candidates; a common position or fill/reversal order requires additional broker evidence. |
| `SOURCE_NOT_VALID` | Blocking: an empty, rejected, duplicate/conflicting, or otherwise invalid parser result yields no candidates. Correct the retained parser diagnostics and reparse. |
| `INVALID_NORMALIZED_ROW` | Blocking defense for non-parser callers: missing source location/identity/contract, nonpositive quantity, unsupported direction, or reversed instants cannot become a candidate. |
| `SOURCE_IDENTITY_NOT_UNIQUE` | Blocking defense for non-parser callers: Ids and record/line locations must be unique. No arbitrary occurrence is selected. |

Interval sets are **diagnostic sets only**, not grouped Trades. Contracts are compared exactly and independently. TradeDay is not an interval partition: an overlapping relationship is not suppressed merely because the broker's date labels differ. Touching rows with opposite directions do not prove a shared reversal fill. A strictly later, non-overlapping interval needs no relationship warning, but still has unverified account-position boundaries. Neither costs nor source PnL are used to choose grouping.

Warnings do not prevent `CanUseRowCandidates` from being true because row-level evidence can be preserved accurately without position grouping. This flag means readiness for further read-only row review, **not** permission to persist or automatic-import eligibility. If a later feature requires verified complete broker positions rather than closed-row candidates, this export alone is insufficient. Blocking diagnostics suppress all candidates rather than accepting a misleading partial set.

Reconstruction is deterministic, synchronous/cancellation-aware like the existing CPU-bound reconstruction boundary, and has no database, file-writing, logging, clock, random-ID, or Tradovate service dependency. Cancellation propagates; the source records are not mutated.

### Supplied-file M11.2 investigation

The local 25-row sample has 25 distinct source IDs, one contract, two broker trading-date labels, 24 Short rows, and one Long row. Repeated entry-time sets contain source records 4–6, 7–18, and 20–24. The 12 rows in the middle set have two entry prices and eleven exit instants, so treating its shared timestamp as one fill would erase source distinctions. Differing exit prices and partial quantities remain on their original rows.

The read-only parser-to-candidate run yields **25 candidates**, 0 rejected rows, and 0 blocking reconstruction errors. Closed-row quantity totals **72** on both sides of the boundary. Candidate ordinal 1–25 maps one-to-one to source record 1–25 and physical line 2–26; the exact source Id remains attached in memory but is not printed or copied to fixtures/documentation. Exact price/quantity/source-object retention was checked for every candidate.

There are four reconstruction warnings: one `POSITION_BOUNDARIES_UNVERIFIED` covering all rows and three `POSITION_GROUPING_AMBIGUOUS` diagnostic sets covering records 1–3, 4–18, and 19–25. The first set includes a direction change at a shared closing/opening second; no reversal execution is inferred. **25 is the retained closed-row candidate count, not a proven broker-position count.** The original file is read in place and remains outside Git. No journal database is opened.

## M11.3 Gross, Fees, Commissions and Net

### Evidence and supported interpretation

The supplied MNQ export's reported PnL equals directional price movement multiplied by closed quantity and the independently verified MNQ multiplier. CME specifies MNQ at **2 USD per index point** ([CME contract overview](https://www.cmegroup.com/markets/equities/nasdaq/micro-e-mini-nasdaq-100.html), checked 2026-09-28). This pricing is supplied explicitly to the local sample check; production code does not infer point value from PnL or hard-code a symbol multiplier.

The sample separately reports Fees of 0.72 USD and Commissions of 0.50 USD per closed contract. Topstep's published breakdown distinguishes exchange/regulatory fees from commissions and currently lists MNQ at 1.22 USD total round turn ([TopstepX commissions and fees](https://help.topstep.com/en/articles/8284213-topstepx-commissions-and-fees), checked 2026-09-28). This corroborates additive cost totals for the reviewed schema; it is not a guarantee about every export or future rate. Published rates are not fetched by the application, stored as imported costs, or used as row-validation thresholds.

The supported explicit interpretation is `TopstepCostInterpretation.SeparateReportedRoundTurnTotalsUsd`: `PnL` is before costs, `Fees` excludes `Commissions`, and both cost fields are totals for the row's completed quantity. Reconciliation defaults to `Unverified`, which blocks numeric Net. Callers must establish that this particular source uses the reviewed interpretation; matching column names or a matching Gross alone does not prove cost semantics or currency. The CSV has no currency column, so the supported interpretation is limited to USD and requires matching verified Instrument currency.

### Formulas and precision policy

For each candidate:

```text
Long Gross  = (ExitPrice - EntryPrice) × Size × PointValue
Short Gross = (EntryPrice - ExitPrice) × Size × PointValue
TotalCosts  = reported Fees + reported Commissions
Net         = verified Gross - TotalCosts
```

Implementation follows `Trade.CalculateGrossPnL`'s exact operation order: entry/exit price × quantity, directional notional subtraction, then point value. Net follows `Trade.NetPnL`: Gross minus the sum of the two costs. All financial inputs/results are `decimal`; there is no display rounding, tick snapping, currency rounding, per-side multiplication, quantity-based cost replacement, or cost deduction from reported PnL before comparison.

**Tolerance is zero.** The current Domain does not round Gross or costs, the parser retains exact source decimals, and the verified sample reconciles exactly. Any discrepancy, including a sub-cent one, blocks verification instead of silently changing source economics. A differently rounded export needs a separately justified policy rather than an arbitrary epsilon. Arithmetic overflow is blocking. Because checked decimal operations can still round or underflow within their range, integer coefficient checks audit each decimal operation for precision loss; they do not produce a different PnL value or override the Domain's operation order. Any loss blocks Net.

The CSV amounts are used once, unchanged. `Commissions` maps to Domain commission and `Fees` to other fees for the completed row, not to a second commission-inclusive total. Later execution mapping must allocate each row total only once across the represented lifecycle, never copy the full total onto both entry and exit. Synthetic Domain-parity tests demonstrate an internal representation with known zero costs on opening and the complete known row totals on closing; this is an allocation proof, not recovery of the broker's per-fill charges. M11.3 does not create executions or implement persistence allocation.

### Boundary, results and diagnostics

`Reconcile` takes the M11.2 result, a dictionary of verified `TradePricingSnapshot` values keyed by **exact source contract**, the cost interpretation, and cancellation. Snapshot validity only establishes positive point value/nonempty currency; correct historical Instrument mapping remains a caller prerequisite and later reference-resolution responsibility. Case-insensitive or canonical-symbol fallback is not performed. No Instrument is resolved or created here.

Each `TopstepReconciledTradeEconomics` retains the original candidate, immutable pricing snapshot, chosen interpretation, separate source PnL/Fees/Commissions, calculated Gross comparison value, nullable Net, and row-located diagnostics. Calculated Gross can remain visible for diagnosing a mismatch; it must not be mistaken for a reconciled result. `IsReconciled` requires numeric Net and no row diagnostics. A genuine numeric zero remains distinct from null/unverified Net.

`TopstepEconomicsReconciliationResult.Source` retains the entire M11.2 result, including all boundary/grouping warnings and original parser diagnostics. `IsEconomicallyReconciled` requires every candidate to pass. A failing row blocks the batch economics gate while preserving successful row checks for review; it does not authorize partial import. This gate is not `IsImportable`: account selection, Instrument verification, source interpretation, preview acceptance, transaction/deduplication, and M11.2 limitations still apply. Cancellation propagates, and no database, network, logging, file writing, or UI is involved.

| Blocking code | Meaning / recovery |
| --- | --- |
| `SOURCE_NOT_VALID` | Empty or invalid parser/reconstruction input; fix retained source diagnostics and rebuild. No partial candidate set is reconciled. |
| `MISSING_REPORTED_COST` | Fees/Commissions column or row value is absent. The parser still rejects it; obtain the actual amount rather than assuming zero. |
| `PRICING_NOT_VERIFIED` | No verified point value/currency supplied for the exact contract. Resolve it explicitly. |
| `COST_INTERPRETATION_UNVERIFIED` | Source has not been confirmed to use separate USD round-turn row totals. Do not infer Net. |
| `CURRENCY_NOT_SUPPORTED` | Instrument currency differs from the verified USD source interpretation. Do not convert or relabel costs. |
| `NEGATIVE_REPORTED_COST` | A rebate/negative-cost model is not verified, and the Domain only accepts non-negative costs. Retain the source value for investigation. |
| `GROSS_PNL_MISMATCH` | Reported PnL is not exactly calculated Gross. Check source values, direction, pricing and export semantics; do not reinterpret net-looking PnL automatically. |
| `ARITHMETIC_OVERFLOW` / `ARITHMETIC_PRECISION_LOSS` | Exact supported decimal arithmetic is unavailable; Net remains null. |

Malformed cost/PnL values retain their original field-specific parser diagnostic. Economics diagnostics identify source record and line without echoing customer rows or broker IDs. Costs are not rejected merely for differing from today's published rates: valid source amounts may reflect different rates without requiring a software update.

### Local sample reconciliation

The production parser → candidate reconstruction → economics path was exercised read-only on the supplied file using independently verified MNQ pricing of 2 USD/point and explicit acceptance of the reviewed USD cost interpretation. All **25 rows passed**, with **0 failed rows and 0 blocking economics diagnostics**:

| USD aggregate | Observed |
| --- | ---: |
| Reported PnL / calculated Gross | 1,241.00 / 1,241.00 |
| Fees | 51.84 |
| Commissions | 36.00 |
| Net | 1,153.16 |

All original row/candidate references and the four M11.2 boundary/grouping warnings remained intact. No row blocks the economics gate for this verified sample. Broker-position grouping is still unverified; this is not import acceptance. The customer file and identifiers remain outside Git, and no journal database was opened.

## Automated coverage

M11.3 adds 37 synthetic economics/pipeline cases covering Long/Short profit/loss/zero, exact fractional economics, actual row costs differing from published rates, Domain parity without double charging, missing/malformed costs, reported-PnL mismatches, pricing/currency/interpretation gates, negative costs, overflow/precision loss, provenance, grouping-warning retention, and cancellation. All **137 focused Topstep tests** and the full **1,857-test Release suite** pass (400 Domain, 348 Application, 605 Infrastructure, 504 Desktop), with no failed/skipped tests. Release build has zero warnings/errors; `git diff --check` passes.

M11.2 adds 35 focused contract/reconstruction cases, including one-row Long/Short economics, apparent partial closes, same-time distinct rows, differing prices, overlapping interval chains, cross-contract separation, TradeDay preservation, touching/opposing directions, deterministic source traceability, invalid/duplicate input, cancellation, and read-only parser integration. The 100-test Topstep filter passes. The full Release suite passes **1,820 tests** (400 Domain, 320 Application, 596 Infrastructure, 504 Desktop), with zero failed or skipped tests; Release build has zero warnings/errors and `git diff --check` passes.

Synthetic fixtures cover valid Long/Short rows, exact decimals and zero/negative PnL, quoted/multiline CSV, source offsets, broker-date preservation, subsecond duration, ordering by instants, malformed fields/headers/rows, duplicate/conflicting IDs, culture independence, cancellation, stream ownership, I/O failures, and provider-schema separation. Existing Tradovate tests protect the shared tokenizer extraction.

M11.1 verification passed 108 focused Topstep/Tradovate CSV tests and the full 1,785-test suite (400 Domain, 317 Application, 564 Infrastructure, 504 Desktop), with no failed or skipped tests. Release build completed with zero warnings and zero errors.

The supplied 25-row export was parsed locally through the production parser: 25 accepted, 0 rejected, 0 diagnostics. Its source `+03:00` timestamps, `-05:00` TradeDay offsets, and all 25 subsecond durations were preserved. The customer CSV and its identifiers are not repository fixtures. This is parsing evidence, not import acceptance; the real journal was not opened.

## Remaining limitations for M11.4–M11.7

- M11.2 supports the reported closed quantity per row. Its membership in a complete broker flat-to-flat position remains unknown. A finer position-grouping mode would require reliable common position/fill identifiers, account context, or complete position history; shared times, TradeDay, and source Id proximity do not supply that evidence.
- What are the scope and stability of `Id` across account exports and re-exports, and how is account identity supplied? This must be settled before durable deduplication in M11.6.
- Instrument resolution must supply verified historical point value and currency for each exact contract; a zero-PnL row cannot independently verify its point value. No Instrument creation or reference-data lookup is added in M11.3.
- The reviewed USD schema supports additive Fees and Commissions totals and Gross-reported PnL, but a different schema/currency, net-reported PnL, rebates, or rounded economics requires new evidence and explicit policy. Future rates must still come from the CSV, not the published table.
- Later preview/persistence must retain row-level provenance, these diagnostics, grouping warnings, and the cost interpretation, then allocate each verified row cost total exactly once. No transactions, deduplication, Desktop wiring, or broker execution fabrication is implemented here. Tradovate's unknown-cost behavior remains unchanged.
