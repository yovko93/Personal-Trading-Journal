# Topstep CSV Preview, Transactional Import and Source Provenance

M11.1 provides a read-only source boundary: Application owns `ITopstepCsvParser`, immutable `TopstepSourceRow` records, and structured parse results; Infrastructure implements `TopstepCsvParser`. It is not connected to the Desktop Import page yet. That page continues to accept the existing Tradovate matched-fill format only.

M11.1–M11.5 preparation and preview create no Trades, executions, reference data or import records. M11.6 adds explicit reviewed confirmation through an Application use case and transactional SQLite persistence; the Topstep Desktop workflow remains M11.7. One accepted CSV source row produces one normalized row and one closed-row candidate; matching timestamps do not group rows or establish broker execution identities.

M11.2 adds `ITopstepTradeCandidateReconstructor` and the independent Infrastructure implementation `TopstepTradeCandidateReconstructor`. It prepares **reported closed-row candidates**, not verified account-level positions. The result can be ready for further row-level review without establishing automatic-import eligibility. It does not call Tradovate reconstruction or change the M11.1 parser.

M11.3 adds the pure Application `TopstepEconomicsReconciler`. It reconciles each existing candidate against caller-verified Instrument pricing and an explicitly verified cost interpretation. It neither changes the source rows/candidates nor makes them importable by itself. The parser still requires every financial field; missing costs are rejected, never normalized to zero.

M11.4 adds Application `TopstepReferencePreparationService`, which reads the existing Instrument and Account catalogs, resolves independently verified pricing, and calls M11.3. Its result is ready for read-only preview only when reference and economics checks pass. It has no write-service dependency, confirmation operation, or Desktop registration. Reference proposals are data, not authorization to create anything.

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

All occurrences of the repeated ID are rejected, with diagnostics referring to source lines rather than echoing the identifier. IDs in well-shaped rows still participate in conflict detection if another field is invalid. Ignored extra columns do not affect duplicate comparison. The parser performs no cross-import lookup; M11.6 confirmation separately enforces account-scoped durable identity as described below.

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

## M11.4 Instrument resolution and explicit Account mapping

### Contract identity and verified economics

The normalized `ContractName` stays unchanged. Resolution parses a case-insensitive futures root + month code + one/two-digit year suffix into `TopstepContractIdentity`: for example, `MNQZ6` retains that exact source token and maps to canonical Instrument `MNQ`, month `Z`, year token `6`. The Domain catalog represents a canonical product, not an expiry-specific contract. No absolute year/century, exact expiry date, continuous symbol, fuzzy alias, or tick-aligned average price is inferred. Unsupported tokens block with `UNRECOGNIZED_CONTRACT`.

All canonical matches are inspected **before** filtering by activity, validity or verification. Two matches block with `MULTIPLE_INSTRUMENT_MATCHES`, even if only one is active or economically plausible. There is no silent tie-breaker or fallback Instrument. A unique inactive, verified Instrument is retained with `INSTRUMENT_INACTIVE`, without activation.

The built-in verified profile is deliberately limited to MNQ: Futures, Micro E-mini Nasdaq-100, CME, USD, tick size **0.25**, tick value **0.50**, point value **2**, quarterly months H/M/U/Z. These are independent reference specifications, not values solved from source prices/PnL. CME's [MNQ contract specifications](https://www.cmegroup.com/markets/equities/nasdaq/micro-e-mini-nasdaq-100.contractSpecs.html) confirm the USD multiplier and tick; its [Micro E-mini FAQ](https://www.cmegroup.com/articles/faqs/micro-e-mini-equity-index-futures-frequently-asked-questions.html) confirms the quarterly cycle (reviewed 2026-09-28). Production preparation makes no network request and does not use any published commission schedule.

- One matching MNQ must agree with all those specifications. Wrong asset class, exchange, currency, tick size/value or point value blocks with `INSTRUMENT_SPECIFICATION_MISMATCH`; the catalog and source values are not corrected automatically.
- No matching MNQ yields `ProposedCreation` plus `INSTRUMENT_CREATION_PROPOSED`. The complete immutable proposal includes name, symbol, asset class, exchange, currency, tick/point economics and reference evidence. Its pricing can be reconciled read-only; a later workflow must obtain explicit creation approval. Multiple source expiries can share one canonical proposal without merging their row candidates.
- No matching Instrument for another root yields `INSTRUMENT_METADATA_REQUIRED`: supply independently verified contract identity and complete specifications, create/verify the catalog entry separately, then rerun. There is no incomplete or price-derived proposal.
- An existing non-profile Instrument must have complete Futures metadata, positive mutually consistent tick/point economics, and explicit independent verification. Because the catalog has no verified flag, the caller supplies `TopstepInstrumentVerification` bound to the **exact source contract and complete immutable `InstrumentListItem` snapshot**, not merely an ID. This is a trust-boundary attestation, not automatic verification by the program. Missing/stale attestation yields `INSTRUMENT_VERIFICATION_REQUIRED`; it cannot override duplicate matches. No attestation UI is implemented in M11.4.

Verified pricing snapshots are keyed by exact source contract and passed directly to the unchanged M11.3 reconciler. The caller must still explicitly select `SeparateReportedRoundTurnTotalsUsd` for the reviewed cost schema; its default is unverified. Every row must pass exact Gross comparison and the existing cost checks. Instrument failures withhold pricing, so affected rows additionally retain `PRICING_NOT_VERIFIED` and null Net. A mismatched source PnL retains its reported amount and row-located `GROSS_PNL_MISMATCH`; no partial batch becomes preview-ready. Nothing rounds source prices, adjusts quantities, replaces costs, or infers specifications from a zero/profitable row.

### Destination Account policy

The CSV contains no destination Account identity. Each call requires an explicit nullable Account ID; null/empty yields `ACCOUNT_SELECTION_REQUIRED` without an Account lookup. The service never selects from the account list, derives an account from a filename/source ID, or falls back to a previously successful request. A missing/deleted selected account yields `ACCOUNT_NOT_FOUND`. Both diagnostics direct the caller to refresh/select an available account.

`ProviderName` must be exactly **Topstep**, after trimming and case-insensitive comparison. The project's provider field is free text, so this is an explicit Topstep policy, not a pre-existing verified-provider flag. Blank, unrelated providers and unreviewed aliases such as TopstepX produce `ACCOUNT_PROVIDER_MISMATCH`; the user must verify/correct provider metadata or select another account, not have it silently reassigned. Account type alone does not prove provider identity. Currency must be USD for the reviewed source schema (`ACCOUNT_CURRENCY_MISMATCH` otherwise); no FX conversion or relabeling occurs.

Consistent with M10's historical-import selector, an explicitly selected inactive account is allowed with `ACCOUNT_INACTIVE`, never reactivated. The result includes its activity status for later review. Preparation still requires an explicit selection on every call; a future selector must not infer confirmation of an old choice.

### Read-only result and future revalidation

`TopstepReferencePreparationResult` retains the policy version, source provider `Topstep`, explicit selection and full Account read snapshot, each exact contract's complete canonical matching set, existing Instrument/proposal, verification evidence, pricing, all economics rows and all upstream parse/grouping diagnostics. `TopstepMappedCandidate` links the original candidate and separate source `Id` to the destination Account and resolution; it generates no journal Trade ID, execution ID or durable deduplication key. Provider + selected internal Account ID + source-row ID are available for later account-scoped identity policy; they do not prove that the broker IDs are stable across exports.

`IsReadyForPreview` requires valid source, account mapping, nonblocked Instrument resolutions, and successful economics for every row. It does **not** grant import permission. `RequiresInstrumentCreationApproval` makes proposed reference data visible. Account failures leave no destination-mapped rows while preserving source/economics/instrument diagnostics for recovery. Instrument failures retain affected row links for inspection but keep readiness false. Diagnostics expose contract and source locations through these links, never complete customer rows or broker identifiers in messages; consumers must not log result objects wholesale.

Every preparation call reads current Account and Instrument data; there is no cached account selection or catalog. A new duplicate, deletion, provider/currency/specification change, or replaced Instrument is reflected on rerun. Earlier results are immutable review snapshots, **not** locks or an atomic cross-reader database snapshot. M11.6 confirmation now revalidates the whole canonical matching set (including newly added inactive matches), selected account and material facts inside its transaction, compares with the accepted snapshot, and requires renewed review on changes. Checking only a previously selected Instrument ID is insufficient. Creation proposals also need renewed absence/specification checks and explicit approval. M11.4 preparation itself remains read-only; M10.6's existing safeguard is untouched.

Cancellation is forwarded to both readers and checked during resolution/mapping and reconciliation. Reader/cancellation failures propagate rather than becoming missing-reference results. Production changes are confined to new Topstep Application types; no database schema, records, Instrument creation, Tradovate behavior, Domain calculations, or Desktop controls change.

### Supplied-file reference preparation

The local sample was exercised through production parsing → reconstruction → reference preparation → economics with isolated in-memory read-only reference readers, never the real journal. All **25 accepted rows, 25 candidates, original source links and four grouping warnings** were preserved. Synthetic reference scenarios on that same source produced:

| Reference scenario | Preview readiness / outcome |
| --- | --- |
| Explicit USD Topstep account + existing verified MNQ | Ready; 25 mappings, no reference/economics errors. |
| MNQ absent | Ready for review; one complete canonical creation proposal, approval still required later. |
| Inactive selected account and unique verified MNQ | Ready; two activity warnings, no reactivation. |
| Two canonical MNQ matches | Blocked: `MULTIPLE_INSTRUMENT_MATCHES`; no pricing/Net guessed. |
| Incorrect MNQ tick/point economics | Blocked: `INSTRUMENT_SPECIFICATION_MISMATCH`. |
| No account selection / deleted account | Blocked: `ACCOUNT_SELECTION_REQUIRED` / `ACCOUNT_NOT_FOUND`. |
| Wrong provider / wrong account currency | Blocked: `ACCOUNT_PROVIDER_MISMATCH` / `ACCOUNT_CURRENCY_MISMATCH`. |
| References corrected and same service rerun | Ready again; current facts reread. |

Verified scenarios preserve aggregate **1,241.00 USD Gross − 51.84 Fees − 36.00 Commissions = 1,153.16 USD Net**, with zero failed economics rows. Independent integration tests use fresh migrated temporary SQLite databases and real Account/Instrument readers with `Mode=ReadOnly`; existing and proposed paths make no import/reference writes. The customer file and IDs are not tracked. This is reference-preparation evidence, not Desktop or persistence acceptance.

## M11.5 Read-only preview and validation

### Composition and presentation contract

`TopstepImportPreviewBuilder.BuildAsync` composes the existing `ITopstepCsvParser` → `ITopstepTradeCandidateReconstructor` → `TopstepReferencePreparationService` (including M11.3 economics). It accepts a file name, caller-owned readable stream, explicit destination Account ID, cost interpretation and optional exact-snapshot Instrument verification attestations. It neither selects an account nor changes the M11.1–M11.4 rules. The default cost interpretation remains unverified, and reference data is fetched again on every build. No Desktop registration, confirmation command, write interface, database migration or Tradovate change is introduced.

The immutable result provides:

- File basename, exact byte length and SHA-256 content identity; absolute customer paths are not retained.
- Selected Account ID and resolved Account snapshot where available. Invalid parser/reconstruction input short-circuits reference reads, so the selected ID remains visible but no account is falsely presented as resolved.
- Source/accepted/rejected row counts, **closed-row candidate count**, existing/proposed/blocked exact-contract resolution counts, distinct proposed canonical Instrument count, warning/error counts and nullable complete reconciled USD totals.
- One `TopstepPreviewCandidate` per M11.2 candidate, labeled **Topstep closed-row record**: source record/physical line, exact source contract, direction, closed-row quantity, UTC entry/exit instants, exact prices, reported Gross, Fees, Commissions, calculated Gross/nullable Net, pricing currency, Instrument resolution and original normalized source row. Original offsets, broker date, duration and source ID remain accessible through that immutable row. No quantity sum is presented as peak exposure; no count is labeled broker position count.
- Complete existing/proposed/blocked Instrument snapshots, including every proposed specification and evidence. Proposed objects are never database records.
- Unified `Csv`, `Reconstruction`, `Account`, `Instrument`, `Economics` and `Preview` diagnostics with severity, original code/message, recovery guidance, field/line where applicable, and affected source-row references. Every original grouping warning remains independently visible; it is not collapsed into a single generic warning. Blocked account/Instrument results retain candidate rows for inspection; invalid source reconstruction produces no partial importable candidate set.

Prices and costs remain decimals without presentation rounding or string formatting. Totals are available only when **every row** has reconciled economics; there is no partial verified Net total. Overflow or precision loss when summing otherwise valid rows adds blocking `TOTALS_OVERFLOW` / `TOTALS_PRECISION_LOSS` rather than exposing a rounded/partial total. Individual exact row values remain available. Two-decimal display formatting is deferred to M11.7.

### Eligibility and mandatory review

`State` is `Blocked` if reference/economics preparation is ineligible or any preview diagnostic is an error. Otherwise it is `RequiresReview`; a warning-only preview is not automatically accepted. Account, Instrument, parser and economics errors cannot be overridden by acknowledging warnings.

Each warning produces a `WarningAcknowledgment` requirement retaining its message and affected source rows. This includes `POSITION_BOUNDARIES_UNVERIFIED` and **each** `POSITION_GROUPING_AMBIGUOUS` group. The caller must show and obtain acknowledgment that the records are separate reported closed rows, not recovered complete broker positions; acknowledging them never changes the candidates' false `ArePositionBoundariesVerified` value. Each distinct Instrument proposal also adds an `InstrumentCreationApproval` requirement linked to its canonical symbol, exact specifications and affected rows. A warning acknowledgment alone cannot approve creation.

`TopstepPreviewReview` carries the reviewed `SnapshotFingerprint` and accepted requirement keys. `MeetsReviewRequirements` returns true only for an otherwise eligible preview, an exact matching fingerprint, and the complete exact set of required keys. It is a **pure review-completeness check**, not an import operation, freshness guarantee or write authorization. M11.6 enforces it both before and during transactional source/reference revalidation. Consumers must never prepopulate acknowledgments from an older/different preview or treat programmatic key enumeration as user consent. Capturing this consent in the Desktop is M11.7 work.

### Source identity, stale snapshots and concurrent requests

The builder copies the source asynchronously into a private in-memory byte snapshot and both hashes and parses that same snapshot. Seekable streams must begin at position zero; nonseekable streams must supply the complete file. The source is consumed but left open. Callers must not concurrently read/mutate the supplied stream; when opening a file, use sharing that excludes writers. I/O failures and cancellation propagate rather than becoming a valid empty preview. No raw source content, full path or broker identifier is logged.

The versioned `topstep-preview-v2` SHA-256 snapshot fingerprint covers basename/content hash/length, explicit Account selection, cost interpretation, copied and sorted verification attestations, prepared row/source economics and provenance, relevant Account/full canonical matching-set snapshots, proposals, diagnostics, summary and review requirements. JSON property order and invariant date serialization are fixed; decimals use exact invariant `G29` canonicalization and sets have deterministic ordering. This makes SQL-projected `2.0` and in-transaction calculated `2` the same numeric fact without rounding prices/costs. Earlier v1 previews must be rebuilt. Original exact verification attestations remain attached for transactional revalidation. Rebuilding the same input/facts is stable across culture; changed bytes (even if normalized economics are unchanged), basename, account selection or relevant reference facts invalidate old review decisions. Unrelated catalog entries do not need to invalidate review. Source IDs are not diagnostic messages; never log the whole preview or serialized hash payload.

A fingerprint is a change detector, **not** an authenticated token, durable deduplication key, database lock or promise of present-time freshness. M11.6 rechecks a newly opened complete source and revalidates the selected Account and complete matching set inside its write transaction, including newly ambiguous inactive matches and proposal absence/specifications. Fresh material facts must match the accepted snapshot; otherwise renewed preview/review is required. Policy/serialization changes require a fingerprint-version change; no timestamp/random GUID makes identical previews spuriously different. The separate cross-import identity is defined below.

Each builder permits one active request. An overlapping call throws a clear `InvalidOperationException` before consuming its source; requests are not silently queued behind an obsolete selection. `finally` releases this guard after success, failure or cancellation. There is no shared `LastPreview`, cached selection or publication callback; separate builder instances may prepare independent streams. A later UI must cancel/await the current request before rebuilding and own which returned result belongs to its current file/account selection. The stream copy, stages and reader operations receive cancellation, with cancellation checks before returning a result.

### Supplied-file verification

The production full preview pipeline was run on the supplied CSV against **three isolated migrated SQLite databases**, using real readers with `Mode=ReadOnly`. Customer source data/IDs were neither tracked nor persisted. The real journal was not opened.

| Reference setup | Preview state | Counts, diagnostics and review requirements |
| --- | --- | --- |
| One verified MNQ + explicit compatible account | `RequiresReview` | 25 accepted, 0 rejected, 25 distinct closed-row candidates; 1 boundary + 3 grouping warnings, 0 errors. All four acknowledgments required. |
| Missing MNQ | `RequiresReview` | Same 25 rows/four grouping warnings plus one creation-proposal warning; one complete canonical proposal with separate creation approval required. |
| Two canonical MNQ matches | `Blocked` | Same 25 source candidates and four warnings; `MULTIPLE_INSTRUMENT_MATCHES` plus 25 row-specific `PRICING_NOT_VERIFIED` errors; Net/verified totals unavailable. Accepting all requirements cannot override the errors. |

Both verified/proposed cases produce exact aggregates **1,241.00 USD Gross, 51.84 Fees, 36.00 Commissions and 1,153.16 Net**. Rebuilding each case produced the same fingerprint. Trades, executions, browse projections and import-ledger counts remained zero; seeded Account/Instrument counts were unchanged and the isolated database files were byte-for-byte unchanged after preview. Passing review-completeness checks with synthetic decisions was tested, not represented as actual user consent or interactive acceptance.

## M11.6 Transactional confirmation and deduplication

### Explicit confirmation and reference revalidation

`ImportTopstepTradesUseCase.ImportAsync(preview, review, fileName, source, cancellationToken)` accepts a reviewed preview and a freshly opened complete CSV stream. The caller owns and disposes that stream; confirmation leaves it open. The use case obtains its audit instant from `TimeProvider` and delegates to `ITopstepImportStore`. It never invents consent. A Blocked preview, incomplete warning acknowledgments, stale review fingerprint, or missing exact-specification proposal approval returns `Blocked / REVIEW_REQUIRED` without writes.

`TopstepImportStore` opens a fresh context and non-deferred SQLite write transaction before reading current Account and Instrument records. It reruns the production parser, reconstruction, reference preparation and economics using snapshots read on that **same transaction/context**, not the independent normal reader contexts. It verifies source bytes/identity, the complete relevant canonical matching sets, account/provider/currency/activity, specifications/pricing, original attestations and exact reviewed fingerprint before looking up duplicates or creating records. Previously reviewed inactive historical references remain allowed; changing activity after preview, deleting/replacing a reference, introducing a second matching Instrument (even inactive), or changing relevant specifications requires a fresh review. It never substitutes a different Instrument. An already-imported file does not bypass these checks.

| Outcome / code | Meaning and recovery |
| --- | --- |
| `Imported` | At least one new closed-row Trade committed; result gives imported, skipped and created-Instrument counts and internal IDs. |
| `NoChanges` | Every source row already has identical imported facts in this destination Account; zero writes and no invalidation. |
| `Blocked / REVIEW_REQUIRED` | Rebuild if needed, then explicitly review every current warning/proposal requirement. |
| `Blocked / SOURCE_CHANGED` | File identity differs from the reviewed bytes/name; select the current complete source and rebuild/review. |
| `Blocked / REFERENCE_DATA_CHANGED` | Account, Instrument resolution, pricing or review policy differs; correct references if needed, rebuild and review. |
| `Blocked / SOURCE_IDENTITY_CONFLICT` | An existing source key has different facts; verify source/account selection. Nothing is overwritten or partially imported. Diagnostic identifies source record/line, not broker ID. |
| `Blocked / PERSISTED_ECONOMICS_MISMATCH` | Derived Domain economics fail exact parity with reviewed Gross/Net; no import is saved. |

Cancellation and unexpected I/O/database/Domain failures propagate, not as `NoChanges` or missing-reference results. Noncommitted transactions are disposed/rolled back. No cancellable step is deliberately introduced after commit, so successful persistence is not subsequently reported as cancellation.

### Durable source-row identity and replay policy

The stable key is **provider Topstep + explicitly selected destination Account ID + normalized source `Id`**. Provider separation is enforced by the dedicated `TopstepImportedRows` table. Source Id is the parser-trimmed, case-sensitive string (SQLite `BINARY`), preserving leading zeros. File path/name, record order, physical line, contract and timestamps do not become surrogate source IDs. Distinct destination Accounts have separate identity scopes; the source contains no broker account identity, so this does not prove that the selected journal Account matches the broker account.

The ledger stores an immutable `topstep-row-v1` economic fingerprint over exact source contract/type, entry/exit UTC instants, quantity/prices, reported Gross/Fees/Commissions, broker TradeDay, duration, verified point value/currency and cost interpretation. Numeric scale is canonicalized without loss; source locations, file identity and textual decimal scale are excluded so a reordered/re-exported identical row is still a duplicate. Changed facts under the same key block the **whole batch**, even when other rows are new. Existing Trade records are never overwritten. The original normalized row JSON, original reviewed preview/content fingerprints and internal derived execution IDs are retained separately as provenance; later Trade edits do not rewrite the imported facts used for deduplication.

Mixed new/exact-duplicate batches import only new rows and skip duplicates in one transaction. Exact all-duplicate replay returns `NoChanges`. If an earlier confirmation created an Instrument, its old proposal preview is now stale: rebuild and explicitly review the now-existing Instrument, then replay returns `NoChanges`. Concurrent eligible existing-Instrument confirmations serialize to one `Imported` and one `NoChanges`; concurrent proposal confirmations produce one `Imported` and a stale-reference block for the other, followed by the same rebuild/review recovery. A database unique account/source index prevents duplicate identities independently of the entry point.

Migration `20260928201843_AddTopstepImportPersistence` adds only the Topstep ledger and its unique account/source and Trade indexes. Its Trade foreign key cascades on hard delete, consistent with the existing Tradovate lifecycle: deleting an imported Trade removes that row's import identity and allows an explicit later reimport. This is not a permanent tombstone or import-history UI. No account IDs are inferred from source identifiers, dates or file names.

### Derived lifecycle records and exact cost allocation

Each accepted source row persists one closed Trade plus two internal `TradeExecution` records: sequence 1 opens the exact row quantity at its UTC entry instant/price; sequence 2 closes it at its exact UTC exit instant/price. Long uses Buy then Sell; Short uses Sell then Buy. No source rows are merged and no complete broker-position boundaries are asserted. The ledger labels this representation `TopstepClosedRowDerivedEntryExitV1` and links both internal execution IDs to the source row. `ExternalExecutionId` and `ExternalOrderId` remain null: these are **derived lifecycle records, not invented broker fills**.

The entry receives zero allocated commission/fees; the exit receives the entire actual reported Commission and Fees totals, exactly once. Entry zero is an allocation convention, not evidence that the broker charged no entry cost. The Trade's immutable pricing snapshot uses verified point value/currency. Domain calculations must equal reviewed directional Gross and `Gross − Fees − Commissions` Net exactly before persistence. Trade, executions, authoritative browse projection, ledger and any approved Instrument are saved/committed together. Tradovate's unknown-cost behavior is unchanged.

After the Application use case observes a committed `Imported` result, a thread-safe shared change tracker advances retained Trades data; it advances Instruments only if creation occurred. The Desktop shell checks these versions before its next navigation load and invalidates affected retained data. `NoChanges`, blocked/cancelled/failed attempts do not advance versions. This is dependency/invalidation wiring only; no Topstep Desktop controls or confirmation presentation are introduced.

### Isolated acceptance evidence

The supplied CSV was previewed with real read-only readers and confirmed against a uniquely isolated migrated SQLite database, never the real journal. Preview retained **25 accepted, 0 rejected, 25 closed-row candidates and four grouping/boundary warnings**, plus an MNQ creation proposal requiring its separate approval. Missing review and changed-source confirmation both blocked. Preview/rejected operations left import-data counts at zero and database bytes unchanged.

Explicit synthetic acceptance imported **25 Trades, 50 derived execution records, 25 browse projections, 25 source ledger rows and one Instrument**. A fresh reviewed replay returned **NoChanges: 0 imported, 25 skipped, 0 Instruments created**. Freshly loaded persisted Domain aggregates totaled **Gross 1,241.00 USD; Fees 51.84 USD; Commissions 36.00 USD; Net 1,153.16 USD**. External broker execution/order IDs remained absent; Tradovate ledger count stayed zero. Retained-data versions advanced once, not on replay. Customer source rows/IDs remain only in the ignored local isolated acceptance data, not tracked fixtures or documentation.

Synthetic fault injection ran after SQL had written two Trades and two ledger rows within the transaction, but before commit. Both an injected exception and cancellation rolled back Trades, executions, browse projections, ledger and proposed Instrument to zero; a later retry succeeded. These are API/SQLite acceptance observations, **not interactive WPF verification or actual user acknowledgment**.

## Automated coverage

M11.6 adds 35 cases: 26 SQLite import tests, 5 Application review/fingerprint/invalidation cases and 4 Desktop retained-data tests. Coverage includes initial existing/proposed import, exact replay, mixed overlap, changed source facts/costs, account isolation and case-sensitive identity, changed/deleted/ambiguous/inactive references, stale/missing approvals, source changes, concurrent confirmation, cancellation, post-SQL rollback/retry, unique-index enforcement, delete/reimport policy and exact persisted economics. Existing schema/migration assertions include the additive ledger; Tradovate regression coverage remains unchanged.

All **232 focused Topstep tests** and **1,952 full Release tests** pass (400 Domain, 405 Application, 639 Infrastructure, 508 Desktop), with no failures or skips. Release build has zero warnings/errors, the EF model has no pending migration changes, and `git diff --check` passes.

M11.5 adds 25 cases (21 Application, 4 Infrastructure) for composed preview fields/counts, exact prices/costs, warning-only and proposed-Instrument review, stage-specific blockers, affected-row provenance, snapshot stability across culture, source/file/account/reference changes, deleted/cleared selection, cancellation/failure recovery, overlapping requests, and aggregate overflow/precision loss. Real-stage SQLite coverage protects existing/proposed/ambiguous results and zero import/reference writes. All **197 focused Topstep tests** and the **1,917-test full Release suite** pass (400 Domain, 400 Application, 613 Infrastructure, 504 Desktop), with no failures/skips. Release build has zero warnings/errors; `git diff --check` passes.

M11.4 adds 31 Application and 4 SQLite integration cases for existing/missing/proposed/inactive/ambiguous references, profile/specification and explicit verification gates, exact contract tokens, account selection/provider/currency/deletion, repeated fresh resolution, cancellation/failures, unchanged costs/provenance and read-only real-reader operation. All **172 focused Topstep tests** and **1,892 full Release tests** pass (400 Domain, 379 Application, 609 Infrastructure, 504 Desktop), with no failures/skips and zero build warnings/errors; `git diff --check` passes. There is no M11.4 interactive UI claim.

M11.3 adds 37 synthetic economics/pipeline cases covering Long/Short profit/loss/zero, exact fractional economics, actual row costs differing from published rates, Domain parity without double charging, missing/malformed costs, reported-PnL mismatches, pricing/currency/interpretation gates, negative costs, overflow/precision loss, provenance, grouping-warning retention, and cancellation. All **137 focused Topstep tests** and the full **1,857-test Release suite** pass (400 Domain, 348 Application, 605 Infrastructure, 504 Desktop), with no failed/skipped tests. Release build has zero warnings/errors; `git diff --check` passes.

M11.2 adds 35 focused contract/reconstruction cases, including one-row Long/Short economics, apparent partial closes, same-time distinct rows, differing prices, overlapping interval chains, cross-contract separation, TradeDay preservation, touching/opposing directions, deterministic source traceability, invalid/duplicate input, cancellation, and read-only parser integration. The 100-test Topstep filter passes. The full Release suite passes **1,820 tests** (400 Domain, 320 Application, 596 Infrastructure, 504 Desktop), with zero failed or skipped tests; Release build has zero warnings/errors and `git diff --check` passes.

Synthetic fixtures cover valid Long/Short rows, exact decimals and zero/negative PnL, quoted/multiline CSV, source offsets, broker-date preservation, subsecond duration, ordering by instants, malformed fields/headers/rows, duplicate/conflicting IDs, culture independence, cancellation, stream ownership, I/O failures, and provider-schema separation. Existing Tradovate tests protect the shared tokenizer extraction.

M11.1 verification passed 108 focused Topstep/Tradovate CSV tests and the full 1,785-test suite (400 Domain, 317 Application, 564 Infrastructure, 504 Desktop), with no failed or skipped tests. Release build completed with zero warnings and zero errors.

The supplied 25-row export was parsed locally through the production parser: 25 accepted, 0 rejected, 0 diagnostics. Its source `+03:00` timestamps, `-05:00` TradeDay offsets, and all 25 subsecond durations were preserved. The customer CSV and its identifiers are not repository fixtures. This is parsing evidence, not import acceptance; the real journal was not opened.

## Remaining limitations and M11.7 work

- M11.2 supports the reported closed quantity per row. Its membership in a complete broker flat-to-flat position remains unknown. A finer position-grouping mode would require reliable common position/fill identifiers, account context, or complete position history; shared times, TradeDay, and source Id proximity do not supply that evidence.
- Deduplication treats a source `Id` as stable within the explicitly selected destination Account and rejects changed facts. The export itself cannot prove broker-account membership or ID stability across different broker accounts/products. Users must select the correct destination; there is no cross-account automatic deduplication or identity inference.
- M11.4 resolves reviewed MNQ metadata and explicitly verified existing Instruments; other roots have no built-in creation profile. Historical specification changes need independent evidence and policy review. A zero-PnL row cannot verify point value. Short year tokens are preserved rather than expanded into a guessed absolute expiry.
- The reviewed USD schema supports additive Fees and Commissions totals and Gross-reported PnL, but a different schema/currency, net-reported PnL, rebates, or rounded economics requires new evidence and explicit policy. Future rates must still come from the CSV, not the published table.
- M11.6 enforces review and transaction rules at the Application/SQLite boundary. M11.7 must add explicit Topstep file/account selection, preview formatting, closed-row provenance/boundary warnings, exact proposal review, user acknowledgments, confirmation/cancellation, outcome counts and stale/conflict recovery to Desktop. No interactive Topstep import acceptance is claimed yet. Tradovate's unknown-cost behavior remains unchanged.
