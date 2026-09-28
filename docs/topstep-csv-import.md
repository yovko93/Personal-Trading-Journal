# Topstep CSV Source Rows and Read-Only Candidates

M11.1 provides a read-only source boundary: Application owns `ITopstepCsvParser`, immutable `TopstepSourceRow` records, and structured parse results; Infrastructure implements `TopstepCsvParser`. It is not connected to the Desktop Import page yet. That page continues to accept the existing Tradovate matched-fill format only.

No Topstep Trades, executions, reference data, import history, or database records are created. There is no Topstep preview/confirmation workflow at this stage. One accepted CSV source row produces one normalized row; matching timestamps do not group rows or establish broker execution identities.

M11.2 adds `ITopstepTradeCandidateReconstructor` and the independent Infrastructure implementation `TopstepTradeCandidateReconstructor`. It prepares **reported closed-row candidates**, not verified account-level positions. The result can be ready for further row-level review without establishing automatic-import eligibility. It does not call Tradovate reconstruction or change the M11.1 parser.

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

### Automated coverage

M11.2 adds 35 focused contract/reconstruction cases, including one-row Long/Short economics, apparent partial closes, same-time distinct rows, differing prices, overlapping interval chains, cross-contract separation, TradeDay preservation, touching/opposing directions, deterministic source traceability, invalid/duplicate input, cancellation, and read-only parser integration. The 100-test Topstep filter passes. The full Release suite passes **1,820 tests** (400 Domain, 320 Application, 596 Infrastructure, 504 Desktop), with zero failed or skipped tests; Release build has zero warnings/errors and `git diff --check` passes.

Synthetic fixtures cover valid Long/Short rows, exact decimals and zero/negative PnL, quoted/multiline CSV, source offsets, broker-date preservation, subsecond duration, ordering by instants, malformed fields/headers/rows, duplicate/conflicting IDs, culture independence, cancellation, stream ownership, I/O failures, and provider-schema separation. Existing Tradovate tests protect the shared tokenizer extraction.

M11.1 verification passed 108 focused Topstep/Tradovate CSV tests and the full 1,785-test suite (400 Domain, 317 Application, 564 Infrastructure, 504 Desktop), with no failed or skipped tests. Release build completed with zero warnings and zero errors.

The supplied 25-row export was parsed locally through the production parser: 25 accepted, 0 rejected, 0 diagnostics. Its source `+03:00` timestamps, `-05:00` TradeDay offsets, and all 25 subsecond durations were preserved. The customer CSV and its identifiers are not repository fixtures. This is parsing evidence, not import acceptance; the real journal was not opened.

### Remaining limitations and M11.3 questions

- M11.2 supports the reported closed quantity per row. Its membership in a complete broker flat-to-flat position remains unknown. A finer position-grouping mode would require reliable common position/fill identifiers, account context, or complete position history; shared times, TradeDay, and source Id proximity do not supply that evidence.
- What are the scope and stability of `Id` across account exports and re-exports, and how is account identity supplied? This must be settled before durable deduplication in M11.6.
- Does `PnL` consistently represent gross price movement across supported contracts/exports? Sample agreement alone is not a general contract.
- Does `Fees` include `Commissions`, or are they additive? Are these per-row totals, per-side/per-contract amounts, or another allocation? What currency, sign/rebate, and rounding conventions apply?
- How should source Size/contract economics and costs be mapped once those meanings are confirmed? No Net P&L formula, cost allocation, Instrument creation, broker execution reconstruction, or persistence is introduced in M11.1/M11.2.
