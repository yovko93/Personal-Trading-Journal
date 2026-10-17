# Trading Plan

## M21.1 — scope, identity and behavioral contracts

The first dependency slice establishes pure Domain/Application contracts. It does **not** yet replace the Desktop placeholder, write plans, create a migration, or add plan evidence to reviews. Existing Accounts, Instruments, Trading Setups/Playbooks and global Rules remain the only catalogs. The accepted M20 implementation and its separate Strategy scope decision are unchanged.

Repository inspection began on clean `develop`, `c76d819541e0b47341fc913518744c8c9410c7df` (`Merge pull request #23 from yovko93/develop`). That history includes `9eb002a` (the M20 coverage-sort correction). This is local merge-history evidence, not a fresh CI acceptance claim.

### Identity and time

- One logical Plan has a stable non-empty Plan ID and the unique key **(exact Account ID, New York calendar date)**. No All-accounts or null-scoped Plan exists. A same-name replacement Account is a different scope.
- Session is metadata: Asia, London, New York or Other. A Draft may leave it unselected. Changing session or adding Instruments cannot bypass account/date uniqueness.
- The default date comes from injected `TimeProvider.GetUtcNow()` through the existing `TradingTimePolicy.ConvertUtcToTradingTime`. A user-selected date is captured in the write command; saving does not recalculate today from the machine's timezone.
- Audit and Ready timestamps must be canonical UTC. Market/session labels do not redefine a trading day. Later Trade/Review membership continues to use the application's New York closure-date convention; a cross-midnight Trade is not counted twice.
- Ready time strictly before a Trade's entry time proves only that the record existed before entry. Equal/later timestamps, missing entry times and a backdated selected Plan date do not establish prior preparation. Association does not prove adherence.

### Draft and Ready lifecycle

| Operation | Contract |
| --- | --- |
| Create Draft | Exact account/date must be absent. Empty/incomplete content is allowed. New Plan identity and initial optimistic version; no Ready revision yet. |
| Save Draft | Require loaded Plan ID/version and Draft status. Preserve incomplete content. Advance the optimistic version only after a successful commit. |
| Mark Ready | Require Draft status, valid required content, all five explicit confirmations and revalidated references. Atomically append the next immutable Ready revision and advance the optimistic version. |
| Start Draft | Explicitly derive from an existing Ready revision of the same Plan; retain the latest Ready revision independently. Do not overwrite an already working Draft. |
| Copy forward | A new target account/date must be absent. An existing target is opened or reported as a conflict; never overwritten. Reset daily observations and every confirmation. |

The **save version** and **Ready revision number** are distinct. Ready revisions are addressed by `(PlanId, positive revision)` and allocated monotonically from the maximum ever issued; never reused, renumbered or edited in place. A Draft derived from an older Ready revision does not replace the latest Ready revision or rewind numbering. Starting a new Draft resets confirmations. Draft saves do not claim to be Ready history. Existing saved Reviews keep their exact captured Ready revision even after later Plan changes.

`TradingPlanWriteCommand.CheckTransition` is a pure preliminary decision: fresh state is required under the future SQLite writer reservation. A permitted transition is **not** a successful write. Content validation, reference validation, unique indexing, transaction rollback, cancellation boundaries, atomic append and verified readback are mandatory work for M21.2–M21.3, not guarantees implemented by this contract-only slice. An application message may say Saved only after committed readback. Exceptions, validation and conflicts must retain the editor's raw input and images.

Write outcomes distinguish Success, Invalid, Conflict, Target already exists, Reference unavailable, Reference changed, Cancelled and Storage failure. A stale or duplicate submission cannot silently edit Ready content. No provider calls, automatic trading checks or broker enforcement are introduced.

### Structured content and readiness

The bounded immutable authoring values preserve exact text, whitespace and decimal values. They defensively copy collections and reject duplicate Instrument, Scenario or Rule IDs. A Scenario must belong to an Instrument context included in that Plan. Scenario ordering follows the supplied collection. Its ID is local to its containing Plan; retaining an ID during copy-forward does not make two Plans share mutable content.

| Content | Draft | Ready |
| --- | --- | --- |
| Exact Account/date | Required identity | Required identity; available active Account checked at commit |
| Session and Today's Focus | May be empty | Selected session and meaningful focus required |
| Instrument contexts | May be empty | At least one context; exact existing active identities |
| Opportunity plan | May be incomplete | Bias and market thesis for every context, plus at least one complete scenario |
| Scenario | Name/direction/IF/THEN/INVALID IF may be incomplete | Every included scenario requires all five; Long/Short reuse `TradeDirection`; a second/opposite-direction scenario is optional |
| Stand-aside-only plan | May be incomplete | Explicit StandAside intent and reason; no trading scenarios required or allowed; no invented thesis, bias or levels required |
| Price levels | Optional | Optional; blank remains null, never zero |
| Risk limits | Optional | Optional; user explicitly confirms review of any unset limits |
| News/restricted-time notes | Optional manual text | Optional manual text; news checklist confirmation still required |
| Playbook and Rule links | Optional | Optional; all supplied links must validate at commit |
| Checklist | Any explicitly confirmed subset | All five checks required |

An opportunity plan may include stand-aside restrictions without changing its intent. A stand-aside-only plan cannot simultaneously advertise trading scenarios. A context can be watched without its own scenario, but an opportunity Plan must contain at least one scenario overall.

Bounds: 500 UTF-16 code units for Focus; 4,000 for each long-text field; 120 for a Scenario name; 12 Instrument contexts, 24 Scenarios and 50 linked Rules per Plan. Enumeration stops at limit + 1 and rejects excess rather than truncating. Optional levels are exact nullable decimals, including a deliberately entered zero or negative level; no instrument tick rounding, FX conversion or economic calculation occurs. Risk amounts and maximum Trades must be nonnegative; a specified consecutive-loss stop must be positive. Zero monetary risk or zero maximum Trades explicitly permits none; null means unset. Amounts will be labeled in the exact Account's captured currency, with no cross-currency total.

Malformed typed numeric text is not coerced into these Domain values. M21.4/Desktop must retain the raw text and show validation. Saving an incomplete Draft is different from accepting malformed values.

The five checks are user confirmations: context/levels, scenarios or stand-aside decision, risk limits, news calendar, mindset/focus. Populated fields never auto-confirm them. On an actual semantic edit, the working Draft invalidates:

| Changed content | Confirmation reset |
| --- | --- |
| Session or Instrument set | All five |
| Focus | Mindset/focus |
| Context, bias or levels | Context/levels and scenarios |
| Scenarios, linked Setup or stand-aside decision | Scenarios |
| Risk limits or linked Rules | Risk limits |
| News/restricted-time notes | News and scenarios |

Navigation, focus and an unchanged assignment are not semantic edits. The editor and committing use cases must apply this policy to the working Draft; earlier Ready confirmations remain immutable. Progress counts only explicit known flags; unknown flags/enums are rejected.

### References, deletion and history

Selection tokens reuse exact existing source IDs: a Playbook link retains its expected revision and Setup update timestamp; a Rule link retains its expected revision. These are **selection tokens, not historical captures**. M21.2 must freeze the referenced Setup name, relevant guidance and exact revision, Rule name/criterion and revision, Account name/currency, and Instrument labels in each Ready revision. No reference may be resolved by display name.

Fresh create/copy/Ready operations require an active Account and Instruments, active linked Playbook Setups at the selected revision, and active global Rules at the selected revision. Changed, inactive, unavailable and legacy-scoped references are explicit states, not silent substitutions. New Plan operations cannot broaden a legacy scoped Rule into a global one. A Draft with a now-unavailable optional link must retain its authored text, offer relink/remove, and explain why finalization is blocked. Existing Draft content may be saved without inventing replacement catalog content; the persisted availability warning must be resolved before Ready. An unavailable/inactive owning Account permits historical reading only until it is explicitly reactivated; a removed Account is never recreated automatically.

Frozen Ready content must remain readable after source edits/archiving/deletion. Future persistence must retain original identity/labels independently of mutable catalogs and must not cascade Plan history deletion. Plan links do not authorize deletion of shared images or alteration of existing Trade/Playbook retention policy. No screenshot attachments are added to Plans in this contract; there are no new image paths or binary transfers.

History is bounded to 1–100 entries per read (default 20), exact Account and inclusive selected date range, optionally Draft/Ready. Ordering is date descending, stable Plan ID, Ready revision descending. Later reads need a cursor carrying that full order and a separate representation for the working Draft. Historical Ready views are read-only and visually distinct; an explicit Start Draft is required to edit derived content.

### Copy forward

`TradingPlanContent.CopyForward` retains selected session, Focus, Instrument identities, scenario structure/order/text and selected Playbook tokens, planned limits and selected Rule tokens. It clears bias, market thesis, all six price levels, dated news/restriction notes and stand-aside notes, and resets all five confirmations. StandAside intent is retained but its daily reason must be re-entered. Retained scenario text may itself mention a daily condition; the user must review it before reconfirming. Copy never carries Ready status.

The future use case must load the exact authoritative source Ready revision, apply this reset, revalidate all references, and enforce target uniqueness transactionally. Caller-supplied content alone cannot prove a source revision or authorize overwriting the target. The pure method performs no catalog reads or writes.

### Synthetic NQ example

Account “Synthetic USD”, New York date 17 October 2026, New York session; Focus “Wait for confirmation. One setup at a time.” An NQ context has Conditional bias and a manually entered thesis. Previous Day High 20,000.25 is an illustrative nullable decimal, not downloaded market data. One Long Primary scenario says IF price reclaims that level, THEN wait for a retest, INVALID IF acceptance returns below it. A Playbook link may identify the existing Setup's exact revision; it is optional and does not assert compliance. Planned risk 50 USD/trade, daily limit 150 USD, maximum 3 Trades and stop after 2 losses are preparation, not enforcement. Every check is user-confirmed. Alternatively, StandAside with “Missing confirmation; no entries today” requires no invented opportunity or numerical limit.

### Verification and implementation sequence

M21.1 adds focused Domain/Application tests for exact identity, New York year/midnight/DST defaults, selected-date stability, incomplete Draft versus Ready, stand-aside, scenario ownership, bounded immutable inputs, decimals/nulls, checklist invalidation, copy resets, version/source tokens, transition conflicts, Ready protection, history bounds and before-entry timing. These are synthetic in-memory tests, not SQLite restart/UI acceptance.

Final local verification for this slice (17 October 2026):

| Check | Observed result |
| --- | --- |
| New Domain/Application contract cases | 47 passed: 24 Domain and 23 Application, included in the complete project runs below |
| Complete Domain Release tests | **514 passed / 0 failed / 0 skipped** |
| Complete Application Release tests | **999 passed / 0 failed / 0 skipped** |
| Release solution build | **0 warnings / 0 errors**, 26.09 seconds |
| EF model consistency | No changes since the last migration |
| Whitespace and documentation | Tracked and new-file whitespace passed; 145 local links resolved across the three changed documents |
| Complete Desktop/Infrastructure Release gate | Not run for this contract-only slice; required again after M21 integration |
| UI renders, restart, live interaction, matching CI | Not applicable to this contract-only implementation / remain pending for integrated M21 |

Evidence: `artifacts/m21-1/regression/domain.trx` and `application.trx`. Initial focused runs covered 24 Domain and 21 Application cases; two additional transition cases were then added and passed in the complete Application run. Two initial test-fixture assertions were corrected (a reusable fixture intentionally contained a Rule link; immutable-array value comparison required sequence comparison); the final complete runs above have no failures. No old M20 test total is reused as M21 evidence. All nine files remain uncommitted, so no matching CI run exists for this change set.

| Slice | State |
| --- | --- |
| M21.1 Scope/identity/behavior | Implemented and locally verified as above |
| M21.2 Preserved Ready revisions | Pending aggregate, historical captures and lifecycle implementation |
| M21.3 Persistence | Pending additive migration, transactions, readback, cancellation, DI, backup/export coverage |
| M21.4–M21.6 Preparation/scenarios/risk | Authoring value/readiness contracts defined; Desktop workflows pending |
| M21.7 Session Brief | Pending full-width three-section WPF workspace |
| M21.8 History/copy/navigation | Policies defined; durable operations and guarded UI pending |
| M21.9 Daily Review/AI evidence | Pending versioned frozen revision integration; existing reviews unchanged |
| M21.10 Acceptance | Pending integrated gate, renders, restart and live interaction |

M21 is **not complete** at this checkpoint. No Trading Plan persistence, visual/live acceptance, matching CI or new full-gate result is implied by the first contract slice.

Exact M21.1 changed/new files:

- `README.md`
- `docs/architecture.md`
- `docs/trading-plan.md`
- `src/PersonalTradingJournal.Domain/TradingPlans/TradingPlanIdentity.cs`
- `src/PersonalTradingJournal.Domain/TradingPlans/TradingPlanContent.cs`
- `src/PersonalTradingJournal.Domain/TradingPlans/TradingPlanReadiness.cs`
- `src/PersonalTradingJournal.Application/TradingPlans/TradingPlanContracts.cs`
- `tests/PersonalTradingJournal.Domain.Tests/TradingPlans/TradingPlanContractTests.cs`
- `tests/PersonalTradingJournal.Application.Tests/TradingPlans/TradingPlanContractTests.cs`
