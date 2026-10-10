# Tradovate CSV Import

## Bound WPF preview failure — 2026-10-09

The reported generic preview error was reproduced with the supplied 342-row, 13-column export in an isolated migrated SQLite database and the actual compiled `ImportView`. No production journal was opened, the source file was read without modification, and the full CSV is not a repository fixture.

**Proven cause:** parsing, reconstruction, Instrument resolution, Account preparation and preview projection all succeeded. Publishing `PreviewSummary` on the WPF dispatcher synchronously evaluated the period-start/end bindings. Those values already carry their New York offset. `TradingTimestampConverter.Convert` called `TradingTimestampFormatter.FormatNewYork`, which passed the offset-bearing value directly to `TradingTimePolicy.ConvertUtcToTradingTime` / `ConvertUtcToLocal`. The latter correctly requires canonical UTC and threw `System.ArgumentException` (nonzero UTC offset). `BuildPreviewAsync` caught that display exception as the generic error, before publishing Trade rows. A ViewModel-only test had no bound converter and therefore passed.

One ordinary matched-fill row is sufficient to trigger this display failure; it is not dependent on account identity/type, unusual CSV fields, contract rollover, or existing Trade data. Both an empty Instrument catalog and an existing compatible MNQ reproduced it. The sanitized regression uses two synthetic matched rows (summer MNQU6 and winter MNQZ6) to cover both offsets and independent contract streams without including customer rows or identities.

**Correction:** normalize the `DateTimeOffset` instant to UTC at the presentation boundary before projecting it to New York. This preserves instants, wall-clock display, summer/winter/repeated-hour offsets, and the strict UTC-only Application contract. No parser, reconstruction, source P&L, time policy, ambiguity, deduplication or persistence rule changes. Preview projections are prepared before publication; an unexpected failure clears preview/confirmation state.

Unexpected Tradovate preview failures now show a bounded `TVP-<stage>-<category>` diagnostic. Stages identify `INSTRUMENTS`, `ANALYSIS-DISPLAY`, `ACCOUNT-PREPARATION`, `PROJECTION`, or `PREVIEW-DISPLAY`; categories are allowlisted `ARGUMENT`, `OVERFLOW`, `DATABASE`, `IO`, `STATE`, or `UNEXPECTED`. Report that code and the attempted operation when requesting help. The message does not expose exception messages, stacks, raw CSV, file paths or account identifiers. Expected blocking diagnostics still use their established actionable row/symbol context. No new raw-error logging is introduced.

**Supplied-file result:** after the fix, the actual bound WPF preview reaches `PreviewReady` with **342 valid rows, zero rejected rows, 532 reconstructed fills and 99 Trade candidates**. Both proposed-MNQ and existing-MNQ scenarios pass. Preview leaves zero persisted Trades/executions and creates no Instruments (the existing-MNQ scenario starts and ends with its single synthetic Instrument). Source-completeness and unavailable-cost warnings remain; a valid preview is not a claim that the export contains all broker activity. Confirmation was never invoked for the supplied file.

The compiled WPF regression exercises the real CSV/parser/reconstructor/readers/commands and period bindings in Light and Dark, existing/proposed Instruments, declined confirmation, and injected display failure with no writes. Formatter tests also cover both occurrences of a repeated New York autumn hour and another input offset. Instrument/account/display failures have sanitized diagnostic and no-confirmation regressions. Automated window exercise is not manual mouse/keyboard acceptance; production-database behavior and a matching GitHub Actions run remain unverified. M15.8 has not begun.

Verification: **567 focused tests passed** (Application 169, Infrastructure 283, Desktop 115); the final strengthened bound-WPF regression separately passed. **3,345 full parallel Release tests passed** (Domain 454, Application 657, Infrastructure 894, Desktop 1,340), zero failures/skips. Final Release build: zero warnings/errors; EF reports no pending model changes; `git diff --check` and the new-file whitespace check passed. Only synthetic rows belong to the repository test fixture; the supplied CSV and reproduction probe are not staged or committed.

M10 implements a reviewed Tradovate matched-fills import from file selection through atomic persistence and duplicate replay. Its scope includes all seven stages:

| Stage | Responsibility |
| --- | --- |
| M10.1 | CSV parsing and source normalization |
| M10.2 | Unique-fill reconstruction and flat-to-flat Trade grouping, including supported reversals |
| M10.3 | Canonical Instrument resolution and missing-Instrument creation planning |
| M10.4 | Explicit Account selection and named-zone timestamp preparation |
| M10.5 | Read-only analysis and deterministic preview |
| M10.6 | Transactional confirmation, reference revalidation, and durable deduplication |
| M10.7 | WPF confirmation, result/recovery state, and imported Trade presentation |

Implementation and automated acceptance are available. The remaining interactive sign-off and its evidence are recorded in [M10 acceptance](m10-acceptance.md).

## Supported source contract

The shared limits are **16 MiB of source bytes**, **64 KiB for header detection including BOM/leading blanks**, **50,000 CSV records including header and blank records**, **32 fields per record**, **4,096 decoded UTF-16 code units per field**, and **16,384 raw UTF-16 code units per record**. Raw record length includes syntax and embedded newlines but not the terminating newline; quoted multiline fields remain one record. All limits apply together. See [shared limits and recovery](../README.md#tradovate-and-topstepx-desktop-import).

Selection counts bytes without trusting file length, and format detection stops after the first nonblank record. Complete parsing is incremental, strict UTF-8 with optional UTF-8 BOM, cancellation-aware and bounded before field/list growth. `CSV_LIMIT_EXCEEDED` is distinct from unsupported schema: export a smaller range or remove unnecessary columns/oversized fields/excessive blanks, then select the complete CSV again. Limit failure rejects the whole input and cannot confirm a partially parsed prefix. Within-budget matched-fill reconstruction, economics, warnings, transactional confirmation and replay remain unchanged.

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

For an unambiguous symbol stream, reconstructed fills are ordered by source wall-clock time. Equal timestamps are not inherently ambiguous. Matched-row quantities paired with earlier fills identify closures; quantities paired with later fills identify openings. Closing earlier matched lots precedes unrelated new openings at the same timestamp. This source-constrained lifecycle rule does not claim to recover unrecorded sub-second broker chronology. A stable fill-ID presentation order is allowed only when it cannot change Trade membership, allocation quantities, direction, weighted prices, or P&L.

A candidate begins when signed position moves away from zero and completes when it returns to zero. Multiple opening fills and partial exits remain in one candidate; the next fill after flat begins a separate candidate. Sell-first streams form provisional Short candidates. A source-supported reversal allocates one real fill between the final closing execution of one Trade and the first opening execution of the opposite Trade. Both allocations preserve the same immutable broker fill identity, price, and timestamp. Allocation indexes 0 and 1 distinguish the portions; their quantities must sum exactly to the original fill quantity. Matched-row evidence is partitioned so Source P&L is attributed once. Domain Trades still cannot reverse through zero: each receives only its allocated portion. No broker fill ID or balancing execution is fabricated.

Directly matched buy/sell fills at the same timestamp are checked for feasible source-constrained orders. If direction, boundaries, or allocation quantities remain unresolved, `TIMESTAMP_ORDER_AMBIGUOUS` blocks import and identifies the symbol, timestamp, affected fills, and missing evidence. The bounded, cancellable search also blocks when it cannot establish a safe order. Source P&L remains evidence and cannot by itself establish chronology. Contradictory reversal allocations remain blocking.

Because every accepted matched row contributes the same quantity to one buy and one sell fill, a fully reconstructed supported matched-fills stream is quantity-balanced by construction. This does not prove that the export contains unmatched open fills or all activity from the account. The result therefore reports that source completeness is not independently verified, even when its visible flat-to-flat candidates are structurally reconstructed.

## Financial and identity limitations

Source-reported P&L remains row-level reconciliation evidence. It is not promoted to authoritative `Trade.NetPnL`, and no zero commission or fee facts are invented. The source contains no reliable Trading Account identity, so M10.4 requires an explicit PTJ Trading Account selection. Reconstructed fills carry no Domain identifiers, pricing snapshot, external order identifier, or fabricated UTC timestamp.

## Instrument resolution and creation planning (M10.3)

Only an eligible, nonempty M10.2 reconstruction proceeds to Instrument resolution. The resolver reads existing Instruments through `IInstrumentReader`; it does not call an Instrument write use case or save data. Each exact broker symbol used by a candidate is interpreted as a futures root followed by one recognized CME month code (`F G H J K M N Q U V X Z`) and one or two year digits. For example, `MNQU6` and `MNQZ6` both map to canonical `MNQ`, while their original broker symbols remain unchanged on reconstructed executions. Unrecognized syntax is not treated as a canonical Instrument symbol and requires user mapping. [CME contract month codes](https://www.cmegroup.com/month-codes.html) are the source for the month-code set.

Contracts sharing a canonical root produce one canonical resolution. Their reconstructed source tick sizes must agree exactly. A single existing Instrument with the canonical symbol is reused, including when inactive; inactivity produces a warning but no automatic reactivation. Multiple existing records with the same symbol are blocking ambiguity. An existing Instrument must be Futures and must match the source tick size. Where a verified profile exists, currency and tick value must also match. Cosmetic DisplayName or Exchange differences do not overwrite or block an otherwise safe existing record.

The initial verified profile is limited to `MNQ`: DisplayName `Micro E-mini Nasdaq-100`, AssetClass `Futures`, Exchange `CME`, Currency `USD`, TickSize `0.25`, and TickValue `0.50`. [CME's Micro E-mini contract specifications](https://www.cmegroup.com/articles/faqs/micro-e-mini-equity-index-futures-frequently-asked-questions.html) identify MNQ, its CME exchange, and the 0.25-point/$0.50 outright tick. The `CME` text is the chosen exchange-label representation; it is not treated as a strict equality requirement for an existing user-entered Exchange label. TickValue is verified separately, never derived from the CSV `_tickSize`.

If MNQ is missing and its source tick size agrees with the profile, M10.3 returns one complete creation proposal for all MNQ contract symbols; the proposal has no persisted InstrumentId. A syntactically valid but missing root without a verified profile retains its canonical symbol and source tick size, but requires user-supplied metadata rather than invented currency or tick value. Source/profile conflicts and materially unsafe existing economics block automatic resolution.

`ReadyForPreview` means every broker symbol has a safe existing Instrument or complete creation proposal; it does not mean any Instrument has been created. The preview displays complete proposals and keeps unresolved metadata blocked. There is no free-form mapping or Instrument-economics editor on the Import page. Supported canonical Instruments can be created or corrected in Instruments, then resolution can be rerun; unrecognized broker-symbol syntax requires source correction or future mapping support. Actual proposed-Instrument creation occurs only after final confirmation, transactionally with imported Trades.

## Account selection and trading-time preparation (M10.4)

Preparation requires an eligible, nonempty M10.2 reconstruction, an M10.3 result ready for preview, and an explicitly selected Trading Account Id. The Account is read through the Application boundary and snapshotted for preview; it is never inferred from a filename, external account text, or the set of active Accounts. A missing Account blocks preparation. An inactive selected Account is allowed for historical import with a warning and is not reactivated. Accounts are never auto-created. A known difference between Account and Instrument currency is reported as a non-blocking warning, with no FX conversion or reference-data mutation.

The unified time policy is explicit: Tradovate timestamps are `Europe/Sofia` wall-clock values, manual Trade input uses `America/New_York`, Domain and persistence timestamps remain zero-offset UTC, and Trade activity is presented in `America/New_York`. Conversion uses `TimeZoneInfo` rules for the exact date, never the machine-local zone or a fixed offset. Local inputs remain `DateTimeKind.Unspecified` until their named zone is applied. Nonexistent daylight-saving times block preparation with `INVALID_SOURCE_LOCAL_TIME`; repeated/ambiguous Sofia times require correction with `AMBIGUOUS_SOURCE_LOCAL_TIME`. No offset is guessed.

Each prepared execution preserves the original Sofia wall-clock timestamp and `Europe/Sofia` identity, adds the canonical UTC instant, and projects that instant to New York with its exact resolved offset and `America/New_York` identity. UTC-to-New-York projection is deterministic even when the resulting wall clock falls in a repeated hour because the instant and offset are already known. Candidate times are derived from those prepared executions, M10.2 ordering is retained, equal instants remain allowed, and a sequence that becomes decreasing in UTC blocks with `UTC_CHRONOLOGY_INVALID`.

Preparation reuses the exact M10.3 existing-Instrument Id or creation proposal. It neither re-resolves Instruments nor invents an Id for a proposal. M10.4 performs no Account, Instrument, or Trade write and never persists a proposed Instrument. UTC remains the only authoritative timestamp intended for future Domain and database writes; original Sofia and projected New York values are in-memory preview evidence.

## Read-only import preview (M10.5)

The Import destination is a concrete WPF page backed by one retained `ImportViewModel`. Entering it clears prior file, analysis, diagnostics, account selection, and preview state while retaining a successfully loaded Account list. The page does not open the file picker automatically. It lists every Account and requires an explicit selection; options include Account type, currency, and available provider/external identity so duplicate names remain distinguishable. Inactive Accounts remain available and produce the M10.4 warning.

`Select CSV` uses a Desktop-only picker that exposes only the base filename and a caller-owned stream. The ViewModel disposes that stream immediately after parsing, retains no raw CSV content or full path, and then invokes the approved parser, reconstruction, and Instrument-resolution stages without waiting for an Account selection. A newly selected file clears the preceding analysis and preview before work begins. Cancellation, operation gating, and workflow version checks prevent stale or overlapping completion from replacing newer state.

`Build Preview` requires structurally eligible reconstruction and an explicit Account selection. It reruns Instrument resolution, then calls `TradovateImportPreparationService` and a deterministic, side-effect-free `TradovateImportPreviewBuilder`; unresolved Instrument decisions prevent a confirmation-ready preview. Changing the selected Account invalidates the prepared preview while preserving file analysis. A reconstruction blocker cannot be bypassed by selecting an Account. Selecting a different CSV replaces the old analysis; cancelling the file picker preserves it.

The preview reports source/valid/rejected row counts, unique buy/sell fills, candidate counts, canonical/existing/proposed Instrument counts, Account and timezone context, the New York preview period, and diagnostic totals. Existing Instrument rows show the PTJ display metadata and tick economics preserved by resolution; the preview builder does not re-read them. Proposed rows show the verified creation proposal and state that creation waits for confirmation. Trade rows show broker and canonical symbols, direction, New York open/close time, execution count, opening quantity, weighted entry/exit prices, and source-reported P&L attributed once by source-record index. Weighted averages display two culture-aware decimal places, while calculations, preview identity, and confirmation retain full precision. Source P&L is reconciliation evidence, not authoritative Domain P&L. Preview does not query durable duplicate identities; duplicate counts are determined during confirmation.

Diagnostics retain their CSV, Reconstruction, Instrument, Preparation, or Preview stage and stable code. `SOURCE_COMPLETENESS_UNVERIFIED` and `COSTS_UNAVAILABLE` remain warnings: neither complete history nor costs are invented. A structurally valid preview with no Error diagnostics can be confirmed.

## Desktop confirmation and recovery (M10.7)

After selecting the Account and CSV and reviewing **Build Preview**, choose **Import Trades**. A safe-default confirmation dialog identifies the file, destination Account, candidate count, proposed Instruments, and warnings. Cancelling leaves the reviewed preview intact and performs no write. Import rules and persistence remain in Application/Infrastructure; WPF coordinates the workflow and displays results.

| Outcome | Desktop behavior |
| --- | --- |
| `Imported` | Shows imported Trades, duplicate Trades skipped, and Instruments created; invalidates Trades/Instruments caches for authoritative reload |
| `NoChanges` (displayed as `No changes`) | Shows zero imported Trades and the duplicate count; no new data is written |
| `Blocked` | Shows the conflict code and recovery guidance; disables the obsolete confirmation while retaining relevant analysis/diagnostics |
| Cancelled operation | Retains usable workflow state; cancellation before commit performs no import |
| Unexpected failure | Shows a safe failure message and permits retry; database failures roll back the transaction |

Both completed outcomes clear the selected file/Account, candidates, diagnostics, Instrument-resolution presentation, and active preview. Only the source section and final counts remain, under **Confirm import**. During active review the heading is **3. Confirm import**. An old completed preview cannot be submitted again. Selecting a new file clears the old result and starts a new review. Command gating and a submission guard prevent concurrent/repeated acceptance; the backend still owns durable idempotency. Cancellation is supported during asynchronous analysis, preview, and import work; a committed import is reported as success rather than undone.

Common recovery paths:

- Invalid CSV, an empty source, conflicting fill facts, duplicate matched rows, or unresolved timestamp order: correct/reselect the source and rebuild. Diagnostics preserve stage, code, and available source locations.
- Missing/ambiguous Instrument metadata: correct the canonical catalog data, return to Import, and rebuild/reselect; no Instrument is silently substituted.
- `REFERENCE_DATA_CHANGED`: rebuild the preview to rerun resolution. Existing Instrument identity, uniqueness, and economics are revalidated for new candidates during confirmation, including a newly introduced duplicate canonical symbol.
- `TRADING_ACCOUNT_NOT_FOUND`: Account options are refreshed; explicitly choose an available Account and rebuild.
- `DEDUPLICATION_CONFLICT`: use the complete original source or resolve the conflicting history. Repeated acceptance cannot merge partial or changed fill/allocation sets.

## Transactional persistence and durable deduplication (M10.6)

`ImportTradovateTradesUseCase` reruns M10.4 preparation against the selected Account immediately before delegating to `ITradovateImportStore`. The store uses one context and explicit transaction to revalidate that Account and the current Instrument economics, re-resolve proposals, create an approved Instrument only if still absent, construct Trades through Domain APIs, and persist Trade roots, exact execution provenance, version-2 browse projections, and durable fill identities atomically. Existing-Instrument DisplayName/Exchange changes and inactivity do not block, but a material Symbol, AssetClass, Currency, TickSize, or TickValue conflict does.

The durable allocation identity is `(TradingAccountIdAtImport, BrokerSymbol, Side, ExternalExecutionId, AllocationIndex)`. The ledger also stores immutable source quantity, allocated quantity, price, and UTC timestamp. All identities absent means a new candidate; an exact match to one existing imported Trade's complete allocation set is skipped. Partial overlap, changed source economics, a changed grouping boundary, or an incomplete reversal allocation set blocks the entire import. Exact duplicates and new candidates may coexist; importing the same source to another explicitly selected PTJ Account is allowed. Deduplication precedes proposal creation: an all-duplicate request returns `NoChanges` without creating or re-resolving Instruments. New candidates undergo confirmation-time reference validation. Legacy identity-only rows retain null source economics; migration never infers those facts from editable Trade data.

Hard-deleting an imported Trade cascades its ledger rows. A complete deleted standalone Trade can be imported again. If only one Trade from a shared reversal fill is deleted, replay blocks the resulting partial allocation overlap rather than duplicating the surviving portion.

The matched-fills source has no independent commission or fee facts. Imported executions therefore store both values as `null`: null means unknown, while zero remains known zero for manual workflows. Imported closed Trades can have Domain-derived `GrossPnL` but unknown `TotalCosts` and `NetPnL`. Source-reported P&amp;L remains preview-only reconciliation evidence; no import-history or source-P&amp;L persistence subsystem is introduced.

## Imported Trade review and remaining limitations

The Trades list uses authoritative paged reads. **Size** is the peak absolute position over that Trade's own execution allocations, not cumulative entries or final Open Qty. Details retain the actual remaining Open Qty and full ordered executions. Average prices display exactly two decimal places in preview, list, and Details without changing stored precision. Gross and Net show their currencies and independent sign colors. A null Net is `—` with unknown-cost guidance; known zero is numeric. Row color follows known Net, otherwise known Gross; a green Gross result does not establish profitability after unknown costs.

The current Edit form supports the manual one-entry/optional-full-exit shape and refuses unknown costs or richer imported lifecycles. The row menu still exposes Edit with that explanatory refusal, and Delete retains its confirmation. Cost source identification and commission/fee import are explicitly deferred. Other limits include the fixed Sofia source-time assumption, one destination Account per file, the MNQ-only automatic creation profile, no arbitrary mapping editor, no independent completeness proof, no source-P&L reconciliation/FX conversion, and no persisted import-history or raw-CSV archive.

The original `Tradovate-A049.csv` contains private trading activity and must remain untracked. Synthetic fixtures are used for automated tests. Full-file validation is local-only when the original file is explicitly made available.
