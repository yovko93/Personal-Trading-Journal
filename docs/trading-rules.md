# Trading Rules — M18.1 contract

M18.1 is a Domain/Application foundation only. It does not persist rules or assessments, show a Desktop screen, inspect Trades automatically, or change AI coaching. M18.2 may implement storage against these contracts; it must not silently reinterpret old revisions.

## Rule identity and scope

A Rule has a stable `RuleId` and immutable numbered `TradingRuleRevision` values (contract version 1). A revision contains a name, human-written criterion, description of required evidence, applicability to **one Trade** or **one New York trading day**, an explicit All-accounts or exact-Account scope, an evaluation mode, active/archive state, and UTC creation time. Create starts at revision 1. Editing, archiving, and reopening each append the next revision; an archived rule cannot receive new assessments. Revision numbers never move backward or get reused. A saved assessment embeds the exact revision, so a later rename, scope edit, or archive cannot rewrite what was evaluated.

All accounts means an explicit aggregate scope, not a missing Account ID. An exact-Account rule may only assess that Account's Trade or date/scope. An All-accounts rule may assess an exact Account or an All-accounts day. The target for a day is a `DateOnly` New York calendar date, never a machine-local instant. Later evidence readers must use Calendar's New York half-open midnight bounds and its closure-date attribution: a Trade spanning midnight belongs to its New York **closure** date, including 23- and 25-hour DST days. Open or partial activity is not silently counted as a closed Trade. No currency conversion is implied by an aggregate scope.

Example: “For NQ Trades in Account A, wait for the documented entry confirmation.” Its criterion and the execution/plan evidence needed to assess it are text, **not** executable expressions. A second rule could apply to a New York day: “Review the day's closed Trades and record whether the daily loss limit was respected.” The day rule must state what evidence is needed; incomplete commissions/fees or ambiguous closing times cannot be treated as known zero or resolved by inference.

## Assessment semantics

| State | Meaning |
| --- | --- |
| Not assessed | No assessment record exists. It is not stored as an assessment outcome. |
| Confirmed violation | Supported by referenced recorded evidence, or explicitly entered by a user and labeled `UserEnteredViolation`. |
| No violation | Evaluated with sufficient, referenced evidence and passed. It is not implied by merely linking a Setup or Playbook. |
| Cannot evaluate | Required facts are missing or ambiguous; the explanation identifies the gap. It is neither a pass nor a violation. |

Each append-only assessment has its own stable ID, optional superseded assessment ID, target Trade ID or explicit New York day, original Account scope, UTC time, source (`Manual` or future `AutomaticCandidate`), basis, evidence sufficiency, explanation, and source references. It freezes the full rule revision. An evidence-based pass/violation requires a sufficient-evidence declaration and at least one non-Playbook-only source reference. A user-entered violation is manual and explicit; it can be recorded despite missing market facts. A `CannotEvaluate` assessment requires `Missing` or `Ambiguous` sufficiency. This contract does **not** claim that a supplied reference proves the wording of an assessment. Future evidence verification must check that cited facts exist, belong to the target Account/date and relevant currency, and actually satisfy the rule before recording an evidence-based pass or violation. A Setup association alone never verifies adherence.

The Application store interfaces require atomic compare-and-append of the latest rule revision and, for assessment transitions, both the current active rule revision and previous assessment ID. A stale write returns `Conflict`; a pass attempted with absent/ambiguous evidence returns `MissingEvidence`; a missing or archived rule has distinct results. Domain validation rejects blank or oversized definitions, invalid scopes/targets, mismatched applicability, unsupported outcomes, and impossible evidence combinations. Cancellation is passed to reads/writes; no persistence adapter is wired in M18.1.

## Deferred work and limits

M18.2 must add an additive migration, durable readers/writers, referential verification and concurrency enforcement. Later milestones may add manual Desktop authoring, Trade/day assessment UI, and carefully specified automatic checks. `AutomaticCandidate` only reserves a source label; it is **not** an engine or a promise of automated evaluation. Rules never modify gross/net economics or imply that a Trade followed a linked Playbook. No assessment, revision, or rule survives process restart until persistence is implemented.
