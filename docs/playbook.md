# Playbook — M17.1 Domain/Application foundation

## Scope and existing catalog

Playbook extends **Trading Setup**, the existing repeatable-pattern catalog. Strategy was deliberately removed in `RemoveStrategies`; it is not a current parent entity and is not reintroduced. A Playbook is an owned definition for a Setup, not a competing catalog with another name, identity or lifecycle. Existing Setups without a Playbook remain valid. Existing `Trade.TradingSetupId` remains mutable classification metadata; no existing Trade or import is backfilled or reinterpreted.

M17.1 adds immutable Domain values and an Application authoring policy with a future persistence interface. There is **no store implementation, dependency-injection registration, schema migration, Desktop UI, automatic rule evaluation or AI prompt change**. These definitions are not yet saved by the running app. Current Setup/Trade editors and screenshot storage remain unchanged.

## Version 1 contract and validation

| Value | Contract |
| --- | --- |
| `TradingSetupPlaybookRevision` | Owned identity `(TradingSetupId, Revision)`. Starts at 1, checked increment on each explicit accepted revision, never changes Setup identity. Freezes Setup name/description, guidance and UTC recording time. |
| `PlaybookGuidance` | Required market context, entry conditions, confirmations, invalidation, execution guidance. Each is trimmed, non-whitespace and at most 4,000 UTF-16 characters. This validates completeness of the authored definition, not trading correctness. No executable expressions. |
| `PlaybookExample` | Optional, ordered examples: at most 12, unique non-empty stable example IDs. Required annotation has the same 4,000-character limit. An annotation can stand alone or carry a source Trade ID and optional screenshot ID; screenshot provenance requires a Trade ID. |
| Collections | Defensive bounded copy and read-only exposure; values are immutable. Editing annotations retains their example identity in a new definition revision. Removing an example from a new revision does not mutate an old revision. |
| Audit | Zero UTC offset; definition time cannot precede the current Setup audit state or previous revision. Capture time cannot precede any supplied Trade, Setup or definition state. |

Archiving means existing `TradingSetup.Deactivate`, with `IsActive == false`; no second archived flag is introduced. Archived Setups cannot supply new Playbook revisions or new Trade-context captures. Existing revisions/snapshots remain readable. Reactivating uses the same Setup ID and continues the revision sequence. Existing hard-delete behavior is unchanged in M17.1; before persistence is wired, deletion must treat stored Playbook revisions/context as references and retain history rather than cascade it away.

`SaveTradingSetupPlaybookUseCase` requires an explicit Setup ID and expected latest revision (0 for first definition), validates the owning Setup and produces a new revision. `ITradingSetupPlaybookStore.TryAppendAsync` must later compare the revision **and** owning Setup's active/existing/audit state atomically. Stale revision, archive/delete/rename races return failure without appending. Cancellation before commit writes nothing; a committed append must not be reported as cancellation. The use case checks cancellation before reads and before append. M17.1 fake-store tests prove orchestration, not database concurrency; a later persistence milestone must prove the atomic contract in SQLite.

## Historical Trade meaning

`TradePlaybookContext` freezes Trade ID, opening instant at capture, capture UTC time, and the **complete immutable definition revision**, not just a link. Setup classification, Setup identity and definition identity must match at capture. Later definition edits, renaming, archiving or changing/clearing the Trade's Setup link cannot mutate the captured text. `IsRetrospective` explicitly identifies capture after the recorded entry time. A retrospectively recorded definition must never be presented as a contemporaneous plan; even an equal-time capture is user-selected context, not independently verified evidence.

**A Setup link or context snapshot is never proof that the plan was followed.** M17.1 assigns no pass/fail, rule violation, adherence score or inferred intent from fills, screenshots, profits or missing evidence. The definition can contain user-authored expectations; actual executions retain their existing economic meaning.

Future integration must capture and persist context with an explicit Trade action, in the same transaction as the intended classification/change, and recheck current Trade/Setup/definition versions. Retain the full frozen value with a serialized contract version; do not resolve historical text from the latest Setup. Corrections require explicit append-only history rather than overwriting the original context. Do not automatically capture all legacy classifications. M17.1 does not add a field to the persisted Trade aggregate that existing mappers would silently drop.

### Screenshot examples and retention boundary

The existing `TradeScreenshot` remains Trade-owned metadata with a storage key; file lifecycle belongs to the existing screenshot services. `PlaybookExample.FromScreenshot` copies its authoritative Trade/screenshot identities plus a separate annotation. It does not read/copy images, take ownership of the storage key, validate image bytes or promise an image remains available after Trade/screenshot deletion. Text annotations survive in frozen values; unresolved provenance must be shown as unavailable, never redirected to a different source.

Durable example-image retention requires a later explicit, independently owned media copy with integrity metadata and backup/delete rules, before any UI promises immutable image examples. This is intentionally not implemented through another reference to a Trade-owned file. No local paths or binary data are part of M17.1 values.

## Synthetic NQ example (illustration, not a trading recommendation)

Use one existing Setup named **NQ — range-break retest**, with description **Synthetic training example**. Its first Playbook revision could contain:

- **Market context:** A manually marked session range; record whether price is inside or outside it. Do not infer context solely from profit.
- **Entry conditions:** A move above the marked range followed by a retest; record the proposed entry before execution.
- **Confirmations:** Describe the observed retest and acceptance above the boundary. If confirmation is absent, document that absence rather than inventing it.
- **Invalidation:** The planned premise no longer applies after price returns inside the marked range; record the chosen boundary and cancellation condition.
- **Execution guidance:** Record planned size, entry, protective exit and management intent before acting; record deviations separately. This prose neither places orders nor calculates risk.
- **Optional example:** A synthetic Entry screenshot with annotation “Marked range boundary and retest; this image illustrates the pattern, not compliance.” Use its real synthetic Trade/screenshot IDs through `FromScreenshot`, never an absolute path.

A Trade captured with revision 1 keeps those words even if revision 2 changes the confirmation. A later classification-only assignment remains classification only. No NQ contract specification, source economics, cost assumption or recommendation is inferred from this example.

## Verification and remaining work

Focused Release tests cover required/bounded fields, defensive copies, stable example/provenance IDs, UTC/monotonic audits, Setup identity, revisions, archive/reactivation, stale appends, cancellation/write failure, matching Trade context and immutable history after later edits. Existing Setup/classification/screenshot regressions are included. Synthetic in-memory records only; no production journal, screenshots or provider access.

Local verification on 2026-10-11: **106 Domain + 79 Application focused cases passed**, including **13 + 8 new cases**. Complete Domain/Application Release suites: **467 + 718 = 1,185 passed**, zero failures/skips. Solution Release build: **0 warnings/errors**. The complete Infrastructure/Desktop suite was not rerun for this Domain/Application-only milestone. Tracked and new-file whitespace checks pass.

Persistence/migration, transactional Trade capture, image retention, UI/accessibility, automatic checks and AI integration remain outside M17.1. No Desktop/live acceptance or matching GitHub CI is claimed. The current contracts must receive persistence round-trip and race tests when implementations are introduced.
