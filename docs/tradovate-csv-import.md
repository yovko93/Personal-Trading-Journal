# Tradovate CSV Import

M10.1 provides parsing and source normalization. M10.2 adds deterministic unique-fill reconstruction and provisional flat-to-flat Trade candidates. These stages do not resolve Instruments or accounts, interpret source timestamps as UTC, create Domain Trades, show an import preview, or persist data. The complete M10 import workflow is not yet implemented.

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

Timestamps must use the exact invariant format `MM/dd/yyyy HH:mm:ss`. Because the export declares neither a timezone nor an offset, the normalized values are `DateTime` instances with `DateTimeKind.Unspecified`. No machine-local or UTC conversion occurs. A sell timestamp earlier than a buy timestamp is accepted because it can describe short-side activity.

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

Source-reported P&L remains row-level reconciliation evidence. It is not promoted to authoritative `Trade.NetPnL`, and no zero commission or fee facts are invented. The source contains no reliable Trading Account identity, so multiple-account activity cannot be distinguished without later configuration or additional evidence. Reconstructed fills carry no Domain identifiers, pricing snapshot, external order identifier, or fabricated UTC timestamp.

## Later M10 stages

Instrument resolution will later map contract symbols to canonical Instruments. Missing Instruments may be proposed with source metadata, reviewed and corrected in Import Preview, and persisted only after confirmation; tick value, currency, and other economics must not be inferred from `_tickSize` alone. Later stages will also select or map the Trading Account, apply an explicitly configured timezone, confirm the import, persist it transactionally, and provide durable deduplication.

The original `Tradovate-A049.csv` contains private trading activity and must remain untracked. Synthetic fixtures are used for automated tests. Full-file validation is local-only when the original file is explicitly made available.
