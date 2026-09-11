# Domain Model

## Scope

This document describes the domain model established in M2, preserved by the M3 persistence implementation, and consumed by the M6 Manual Trade Entry and M7 Trade browsing workflows without a Domain redesign. It remains focused on Domain behavior and boundaries rather than Desktop workflow or database-provider details.

## Core Principles

- Ordered execution history is the authoritative source of trade state and economics.
- Trading-process quality is independent from profit or loss.
- Relevant timestamps are supplied explicitly and canonical audit timestamps use UTC zero offset.
- Persistable entities use stable, non-empty `Guid` identities, including when rehydrated.
- Domain remains independent from UI, persistence, filesystem, logging, and broker frameworks.
- State that can be derived reliably is not duplicated as mutable data.

## Model Overview

```text
TradingAccount
      |
      v
    Trade <--------- InstrumentId
      |
      +---- TradeExecution[]
      |
      +---- StrategyId?
      |
      +---- TradingSetupId?

TradeScreenshot
      |
    TradeId

TradeMistake
   |       |
TradeId    TradingMistakeId
              |
       TradingMistake
```

The diagram shows identifiers and conceptual associations, not object navigation properties. `Strategy` and `TradingSetup` remain independent reference dimensions.

## Shared Entity and Time Semantics

All M2 entity concepts—`Instrument`, `TradingAccount`, `Trade`, `TradeExecution`, `Strategy`, `TradingSetup`, `TradeScreenshot`, `TradingMistake`, and `TradeMistake`—use non-empty `Guid` identities. Creation APIs generate identities, while rehydration APIs accept and preserve existing identities without introducing persistence-specific ID behavior.

Audited entities record `CreatedAtUtc` and `UpdatedAtUtc` with zero offset. Their lifecycle updates are monotonic. These audit timestamps describe journal record lifecycle and are intentionally distinct from market/event timestamps such as `TradeExecution.ExecutedAtUtc` and `TradeScreenshot.CapturedAtUtc`. A historical execution or captured image may legitimately predate its later entry or import into the journal.

## Reference Data

### Instrument

`Instrument` is canonical/root instrument reference data such as NQ, MNQ, ES, or MES—not a specific futures expiration such as NQU6. Symbol and currency are canonicalized, tick size and tick value must be positive, and point value is derived from them. Its active flag represents reference-data lifecycle. Contract months, expiration, and rollover are outside M2.

### TradingAccount

`TradingAccount` represents stable account identity and reference data. An optional non-negative starting balance may provide a baseline. Transactional values such as current balance, equity, realized or unrealized P&L, buying power, drawdown, profit targets, and prop-firm rules do not belong to its M2 state.

### Strategy and TradingSetup

`Strategy` represents a broad methodology or framework. `TradingSetup` represents a specific repeatable market configuration. Neither owns the other, and no `StrategyId` is forced into a setup.

## Trade Executions and Lifecycle

`TradeExecution` is an immutable Buy/Sell market fact owned by a `TradeId` and ordered by a positive contiguous `Sequence`. It supports fractional quantities, zero or negative prices, and non-negative canonical commission and fee values. Buy/Sell records what happened in the market; Long/Short is derived at the trade level.

A `Trade` represents one directional flat-to-flat position lifecycle. It starts with an opening execution, supports scale-in and partial scale-out, and closes only when executions return the position exactly to zero. Its execution collection is externally read-only. Additions must preserve ownership, unique execution identity, exact next sequence, and non-decreasing execution chronology; equal timestamps are valid. An execution after closure or one that would reverse through zero is rejected atomically.

Long example:

```text
Buy 2
Buy 1
Sell 1
Sell 2
=> Closed Long
```

Short example:

```text
Sell 2
Sell 1
Buy 1
Buy 2
=> Closed Short
```

A reversal is not one trade:

```text
Buy 2
Sell 3
```

It is conceptually split as:

```text
Trade A: Buy 2, Sell 2
Trade B: Sell 1
```

Automatic splitting is deferred to future import/grouping logic.

The following properties derive from execution history rather than separately mutable state: direction, status, open quantity, opened/closed timestamps, total costs, average entry/exit prices, gross P&L, and net P&L.

M6's `CreateManualTradeUseCase` creates one opening execution and an optional full closing execution through the public `TradeExecution`, `Trade.Start(...)`, and `Trade.AddExecution(...)` APIs. It does not use `Rehydrate(...)` for creation. This narrower manual-capture shape does not remove the Domain's scale-in or partial scale-out capabilities.

M7 list and detail readers reconstruct each persisted aggregate through the existing mapper and reuse these Domain-derived lifecycle and economics properties. They do not persist duplicate calculated state or recalculate Trade behavior in Application, Infrastructure, or Desktop.

## Historical Pricing and P&L

Each trade owns an immutable `TradePricingSnapshot` with `PointValue` and `Currency`. This captures the pricing facts used for that trade so historical P&L does not depend on loading current `Instrument` metadata or change when instrument reference data changes later.

For a closed flat trade:

```text
GrossPnL = (SellNotional - BuyNotional) * PointValue
NetPnL   = GrossPnL - TotalCosts
```

Notional sums quantity multiplied by price for the corresponding side. The sign naturally represents both long and short outcomes. Final `GrossPnL` and `NetPnL` are `null` while a trade is open. M2 does not implement mark-to-market, unrealized P&L, partial realized accounting policies, FIFO/LIFO, or R-multiple calculations.

## Trade Classification

A trade may independently reference an optional `StrategyId` and optional `TradingSetupId`; either, both, or neither may be present. These classifications are review metadata rather than market facts. They may be corrected while a trade is open or after it closes without changing execution history or P&L. Reapplying the same classification is a no-op, and rejected changes leave the existing classification unchanged.

## Screenshots

`TradeScreenshot` is storage-agnostic metadata associated with a trade through `TradeId`. It remains outside the `Trade` execution aggregate, which has no screenshot collection.

`StorageKey` is an opaque storage identifier, not a Windows path or a promise of any particular physical layout. `TradeScreenshot` metadata is persisted in SQLite, but Domain contains neither binary image data nor file operations. Physical image storage remains outside the database and its file-storage workflow is still deferred.

## Mistakes and Process Quality

`TradingMistake` is a reusable, user-defined mistake definition. Mistakes are not hardcoded as an enum and M2 imposes no category taxonomy, severity, or calculated financial cost.

`TradeMistake` represents one occurrence/association between a `Trade` and a `TradingMistake`. Its optional `Note` is specific to that occurrence. Neither related aggregate owns an association collection.

M3 SQLite persistence allows a specific mistake on a trade at most once by enforcing:

```text
UNIQUE (TradeId, TradingMistakeId)
```

This remains a database integrity rule rather than a Domain collection invariant; no association collection was added to the Domain model. A causal mistake cost is also deliberately absent: assigning part of a trade's outcome to one mistake requires policy the current model does not define.

Process quality and financial outcome are independent. PTJ must support all four combinations:

- good trade and profit;
- good trade and loss;
- bad or process-violating trade and profit; and
- bad or process-violating trade and loss.

Profit does not prove correct execution, and loss does not prove poor execution. Strategy, setup, and mistake classification must remain independent from P&L for future analytics and coaching.

## Deliberately Deferred Concerns

The following omissions remain intentional rather than accidental missing fields:

- richer manual capture for scale-in and partial exits;
- Trade edit/delete, CSV imports, and execution-grouping workflows;
- physical screenshot file storage and lifecycle operations;
- initial risk, R-multiple, partial realized P&L, MAE/MFE, and mark-to-market;
- trading rules, rule violations, and prop-firm rules;
- journal entries and daily, weekly, or monthly reviews;
- tags, session classification, and timeframe on `Trade`;
- trading plans and playbooks;
- expectancy, profit factor, win rate, drawdown, and equity-curve analytics;
- futures contract expiration and rollover modeling; and
- AI Coach behavior.

## Persistence Realization in M3

M3 persistence preserves the existing domain contract without redesigning it, including:

- stable `Guid` identity;
- UTC audit timestamps and separate historical event timestamps;
- `TradeExecution.Sequence` ordering;
- `Trade` ownership of its execution history;
- historical `TradePricingSnapshot` point value and currency;
- optional, independent `StrategyId` and `TradingSetupId` values;
- the `TradeScreenshot` to `Trade` reference and opaque `StorageKey`;
- the `TradeMistake` to `Trade` reference;
- the `TradeMistake` to `TradingMistake` reference;
- uniqueness of `(TradeId, TradingMistakeId)`; and
- decimal values without arbitrary persistence-layer rounding.

EF Core materializes Infrastructure-owned records, and explicit mappers call the Domain's rehydration APIs. Provider representation, schema, relationships, migrations, and operational workflow are documented in [Persistence](persistence.md).
