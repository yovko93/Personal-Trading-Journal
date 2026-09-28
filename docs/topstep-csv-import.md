# Topstep CSV Parsing and Normalization

M11.1 provides a read-only source boundary: Application owns `ITopstepCsvParser`, immutable `TopstepSourceRow` records, and structured parse results; Infrastructure implements `TopstepCsvParser`. It is not connected to the Desktop Import page yet. That page continues to accept the existing Tradovate matched-fill format only.

No Topstep Trades, executions, reference data, import history, or database records are created. There is no Topstep preview/confirmation workflow at this stage. One accepted CSV source row produces one normalized row; matching timestamps do not group rows or establish broker execution identities.

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

Synthetic fixtures cover valid Long/Short rows, exact decimals and zero/negative PnL, quoted/multiline CSV, source offsets, broker-date preservation, subsecond duration, ordering by instants, malformed fields/headers/rows, duplicate/conflicting IDs, culture independence, cancellation, stream ownership, I/O failures, and provider-schema separation. Existing Tradovate tests protect the shared tokenizer extraction.

M11.1 verification passed 108 focused Topstep/Tradovate CSV tests and the full 1,785-test suite (400 Domain, 317 Application, 564 Infrastructure, 504 Desktop), with no failed or skipped tests. Release build completed with zero warnings and zero errors.

The supplied 25-row export was parsed locally through the production parser: 25 accepted, 0 rejected, 0 diagnostics. Its source `+03:00` timestamps, `-05:00` TradeDay offsets, and all 25 subsecond durations were preserved. The customer CSV and its identifiers are not repository fixtures. This is parsing evidence, not import acceptance; the real journal was not opened.

### Open questions for M11.2 and M11.3

- What does one exported row represent economically: a complete broker trade, a matched portion, or another grouping? What reliable source evidence, if any, authorizes grouping separate rows? Equal timestamps alone are not evidence.
- What are the scope and stability of `Id` across account exports and re-exports, and how is account identity supplied? This must be settled before durable deduplication in M11.6.
- Does `PnL` consistently represent gross price movement across supported contracts/exports? Sample agreement alone is not a general contract.
- Does `Fees` include `Commissions`, or are they additive? Are these per-row totals, per-side/per-contract amounts, or another allocation? What currency, sign/rebate, and rounding conventions apply?
- How should source Size/contract economics and costs be mapped once those meanings are confirmed? No Net P&L formula, cost allocation, Instrument creation, or execution reconstruction is introduced in M11.1.
