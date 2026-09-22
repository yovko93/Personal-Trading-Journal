# Tradovate CSV Import

M10.1 provides parsing and source normalization. M10.2 adds deterministic unique-fill reconstruction and provisional flat-to-flat Trade candidates. M10.3 plans canonical Instrument resolution and any missing-Instrument creation. M10.4 combines those approved results with an explicitly selected Trading Account and applies the fixed trading-time policy. M10.5 exposes that exact pipeline through a read-only Desktop preview. It still does not create Domain Trades, create Instruments, or persist import data. The complete M10 import workflow is not yet implemented.

## Supported source contract

The parser recognizes the following required headers by their exact, case-sensitive names:

```text
symbol,_priceFormat,_priceFormatType,_tickSize,buyFillId,sellFillId,qty,buyPrice,sellPrice,pnl,boughtTimestamp,soldTimestamp,duration
```

All 13 headers are required, but their order may vary. Empty, duplicate, or missing headers make the file unusable. Additional columns are ignored with a warning so that they cannot be mistaken for required data. UTF-8 input with or without a BOM, CRLF or LF line endings, quoted fields, embedded commas, escaped quotes, and safely ignorable blank lines are supported.

Each accepted data row becomes a `TradovateMatchedFillRow` carrying its source record index and starting line number. Invalid records are not silently skipped: the parse result includes error diagnostics, valid and rejected counts, and an indication of whether the header is usable and the complete input is valid.

## Matched-fill semantics

A source row describes a matched quantity between a buy fill and a sell fill. It is not necessarily a complete execution and is not a Trade. The same `buyFillId` or `sellFillId` may occur in multiple rows; identifiers are preserved as strings, including leading zeros, and are not deduplicated. `MatchedQuantity` describes only the quantity represented by that row.

The parser preserves the broker contract symbol, such as `MNQU6`, rather than reducing it to a canonical Instrument symbol. `_priceFormat` and `_priceFormatType` remain uninterpreted source metadata. `_tickSize`, quantity, prices, and source-reported P&L use exact decimal arithmetic. Futures quantities must be positive whole contracts, and tick size must be positive.

`SourceReportedPnL` accepts invariant dollar notation, including positive, zero, leading-minus negative, and accounting-style negative values. It is the broker-reported result for one matched row, not authoritative `Trade.NetPnL`. The export contains no independent commission, exchange-fee, or broker-fee fields, so the parser does not infer them.

## Time and duration

Timestamps must use the exact invariant format `MM/dd/yyyy HH:mm:ss`. Because the export declares neither a timezone nor an offset, the parser preserves normalized values as `DateTime` instances with `DateTimeKind.Unspecified`; parsing itself performs no machine-local or UTC conversion. M10.4 explicitly interprets these wall-clock values as `Europe/Sofia`. A sell timestamp earlier than a buy timestamp is accepted because it can describe short-side activity.

The `duration` value is preserved as source text. It is not recalculated, parsed into business meaning, or used to infer direction or grouping.

## Validation and diagnostics

Expected source problems produce structured diagnostics containing severity, a stable code, source record and line locations where available, the affected field, and a concise message. Validation covers header shape, CSV record shape, required identifiers and symbol, exact numeric syntax and ranges, supported P&L notation, and timestamps. An invalid row is rejected without defaulting malformed values to zero, one, or the current time. Successfully parsed rows may still be returned, but any rejected row makes the complete input unsafe for automatic import.

The parser is deterministic and leaves ownership of the input stream with its caller. It honors cancellation and does not turn cancellation, memory failures, or unrelated I/O failures into ordinary parse diagnostics.

## Execution reconstruction

Reconstruction identifies a broker fill by the exact combination of broker contract symbol, Buy/Sell side, and external fill identifier. All rows for one fill must agree on its price, timezone-unspecified timestamp, and tick-size metadata. Its reconstructed quantity is the checked decimal sum of the matched quantities from those rows. Conflicting facts or arithmetic overflow block the affected symbol rather than selecting an arbitrary value.

Every source row's buy-fill, sell-fill, matched-quantity, source-record, and source-reported-P&L evidence remains available after aggregation. Reconstructed buy and sell totals must each equal the source matched quantity for their exact broker symbol. Symbols such as `MNQU6` and `MNQZ6` remain independent streams even if both may later resolve to the same canonical Instrument.

An identical matched row is not silently removed or unquestioningly counted as an independent match. The available format cannot distinguish duplicate export data from two identical match events, so the affected symbol is marked ambiguous and all source references are retained.

For an unambiguous symbol stream, reconstructed fills are ordered by their source wall-clock timestamps. Same-side timestamp ties use a stable external-ID presentation order, which is not asserted to be broker chronology. If opposite sides share a timestamp and their order can change a flat boundary or direction, grouping is ambiguous.

A candidate begins when signed position moves away from zero and completes when it returns to zero. Multiple opening fills and partial exits remain in one candidate; the next fill after flat begins a separate candidate. Sell-first streams form provisional Short candidates. An execution that crosses through zero blocks the lifecycle because splitting one broker fill would fabricate source events. No opposite-side execution is manufactured to force an incomplete lifecycle closed.

Because every accepted matched row contributes the same quantity to one buy and one sell fill, a fully reconstructed supported matched-fills stream is quantity-balanced by construction. This does not prove that the export contains unmatched open fills or all activity from the account. The result therefore reports that source completeness is not independently verified, even when its visible flat-to-flat candidates are structurally reconstructed.

## Financial and identity limitations

Source-reported P&L remains row-level reconciliation evidence. It is not promoted to authoritative `Trade.NetPnL`, and no zero commission or fee facts are invented. The source contains no reliable Trading Account identity, so M10.4 requires an explicit PTJ Trading Account selection. Reconstructed fills carry no Domain identifiers, pricing snapshot, external order identifier, or fabricated UTC timestamp.

## Instrument resolution and creation planning (M10.3)

Only an eligible, nonempty M10.2 reconstruction proceeds to Instrument resolution. The resolver reads existing Instruments through `IInstrumentReader`; it does not call an Instrument write use case or save data. Each exact broker symbol used by a candidate is interpreted as a futures root followed by one recognized CME month code (`F G H J K M N Q U V X Z`) and one or two year digits. For example, `MNQU6` and `MNQZ6` both map to canonical `MNQ`, while their original broker symbols remain unchanged on reconstructed executions. Unrecognized syntax is not treated as a canonical Instrument symbol and requires user mapping. [CME contract month codes](https://www.cmegroup.com/month-codes.html) are the source for the month-code set.

Contracts sharing a canonical root produce one canonical resolution. Their reconstructed source tick sizes must agree exactly. A single existing Instrument with the canonical symbol is reused, including when inactive; inactivity produces a warning but no automatic reactivation. Multiple existing records with the same symbol are blocking ambiguity. An existing Instrument must be Futures and must match the source tick size. Where a verified profile exists, currency and tick value must also match. Cosmetic DisplayName or Exchange differences do not overwrite or block an otherwise safe existing record.

The initial verified profile is limited to `MNQ`: DisplayName `Micro E-mini Nasdaq-100`, AssetClass `Futures`, Exchange `CME`, Currency `USD`, TickSize `0.25`, and TickValue `0.50`. [CME's Micro E-mini contract specifications](https://www.cmegroup.com/articles/faqs/micro-e-mini-equity-index-futures-frequently-asked-questions.html) identify MNQ, its CME exchange, and the 0.25-point/$0.50 outright tick. The `CME` text is the chosen exchange-label representation; it is not treated as a strict equality requirement for an existing user-entered Exchange label. TickValue is verified separately, never derived from the CSV `_tickSize`.

If MNQ is missing and its source tick size agrees with the profile, M10.3 returns one complete creation proposal for all MNQ contract symbols; the proposal has no persisted InstrumentId. A syntactically valid but missing root without a verified profile retains its canonical symbol and source tick size, but requires user-supplied metadata rather than invented currency or tick value. Source/profile conflicts and materially unsafe existing economics block automatic resolution.

`ReadyForPreview` means every broker symbol has a safe existing Instrument or complete creation proposal; it does not mean any Instrument has been created. The M10.5 Import Preview displays complete proposals and keeps unresolved metadata blocked for future manual resolution. Actual missing-Instrument creation occurs only after final confirmation, transactionally with imported Trades. The confirmation transaction must re-resolve the canonical symbol because another workflow could create an Instrument after Preview but before confirmation.

## Account selection and trading-time preparation (M10.4)

Preparation requires an eligible, nonempty M10.2 reconstruction, an M10.3 result ready for preview, and an explicitly selected Trading Account Id. The Account is read through the Application boundary and snapshotted for preview; it is never inferred from a filename, external account text, or the set of active Accounts. A missing Account blocks preparation. An inactive selected Account is allowed for historical import with a warning and is not reactivated. Accounts are never auto-created. A known difference between Account and Instrument currency is reported as a non-blocking warning, with no FX conversion or reference-data mutation.

The unified time policy is explicit: Tradovate timestamps are `Europe/Sofia` wall-clock values, manual Trade input uses `America/New_York`, Domain and persistence timestamps remain zero-offset UTC, and Trade activity is presented in `America/New_York`. Conversion uses `TimeZoneInfo` rules for the exact date, never the machine-local zone or a fixed offset. Local inputs remain `DateTimeKind.Unspecified` until their named zone is applied. Nonexistent daylight-saving times block preparation with `INVALID_SOURCE_LOCAL_TIME`; repeated/ambiguous Sofia times require correction with `AMBIGUOUS_SOURCE_LOCAL_TIME`. No offset is guessed.

Each prepared execution preserves the original Sofia wall-clock timestamp and `Europe/Sofia` identity, adds the canonical UTC instant, and projects that instant to New York with its exact resolved offset and `America/New_York` identity. UTC-to-New-York projection is deterministic even when the resulting wall clock falls in a repeated hour because the instant and offset are already known. Candidate times are derived from those prepared executions, M10.2 ordering is retained, equal instants remain allowed, and a sequence that becomes decreasing in UTC blocks with `UTC_CHRONOLOGY_INVALID`.

Preparation reuses the exact M10.3 existing-Instrument Id or creation proposal. It neither re-resolves Instruments nor invents an Id for a proposal. M10.4 performs no Account, Instrument, or Trade write and never persists a proposed Instrument. UTC remains the only authoritative timestamp intended for future Domain and database writes; original Sofia and projected New York values are in-memory preview evidence.

## Read-only import preview (M10.5)

The Import destination is a concrete WPF page backed by one retained `ImportViewModel`. Entering it clears prior file, analysis, diagnostics, account selection, and preview state while retaining a successfully loaded Account list. The page does not open the file picker automatically. It lists every Account and requires an explicit selection; inactive Accounts remain available and produce the M10.4 warning.

`Select CSV` uses a Desktop-only picker that exposes only the base filename and a caller-owned stream. The ViewModel disposes that stream immediately after parsing, retains no raw CSV content or full path, and then invokes the approved parser, reconstruction, and Instrument-resolution stages without waiting for an Account selection. A newly selected file clears the preceding analysis and preview before work begins. Cancellation, operation gating, and workflow version checks prevent stale or overlapping completion from replacing newer state.

`Build Preview` becomes available only after structurally eligible reconstruction, complete Instrument resolution, and explicit Account selection. It calls `TradovateImportPreparationService` and a deterministic, side-effect-free `TradovateImportPreviewBuilder`. Changing the selected Account invalidates only the prepared preview; the file analysis remains available.

The preview reports source/valid/rejected row counts, unique buy/sell fills, candidate counts, canonical/existing/proposed Instrument counts, Account and timezone context, the New York preview period, and diagnostic totals. Instrument rows distinguish existing records from complete creation proposals; unresolved metadata remains an explicit user-input state. Trade rows show broker and canonical symbols, direction, New York open/close time, execution count, opening quantity, exact-decimal weighted entry/exit prices, and source-reported P&L attributed once by source-record index. Source P&L is reconciliation evidence, not authoritative Domain P&L.

Diagnostics retain their CSV, Reconstruction, Instrument, Preparation, or Preview stage and stable code. The preview always calls out that source completeness is not independently verified and that commissions and fees are unavailable. Consequently `IsReadyForConfirmation` remains false and there is deliberately no final Import command or button in M10.5.

## Later M10 stages

Later stages will provide manual corrections, confirm the import, persist it transactionally, and provide durable deduplication.

The original `Tradovate-A049.csv` contains private trading activity and must remain untracked. Synthetic fixtures are used for automated tests. Full-file validation is local-only when the original file is explicitly made available.
