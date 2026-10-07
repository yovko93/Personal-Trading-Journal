# Personal Trading Journal

Personal Trading Journal is a local-first Windows desktop application designed to help traders record, review, analyze, and improve their trading process. The initial focus is futures trading, especially instruments such as NQ and ES, while the architecture is intended to remain extensible to other markets and a possible future SaaS or web version.

The repository currently contains the application foundation, the core trading Domain model, local EF Core/SQLite persistence, the WPF shell and navigation foundation, persisted System/Dark/Light appearance preferences, complete lifecycle management for Trading Accounts, Instruments, Trading Setups, and Trading Mistakes, and manual Trade create/list/view/edit/close/delete workflows. Trades also support local screenshots, Setup classification, Mistake assignments, an authoritative SQLite-paged and sortable browse view, and reviewed Tradovate matched-fills and Topstep closed-row CSV import workflows. Dashboard and Calendar provide analytics and daily Trade review. The Journal page supports daily text, read-only Trade context, three Daily Review questions, explicit completion/reopening, exact-scope launch from Calendar and paged Review History with read-only durable revisions. AI capabilities remain deferred.

## Current Status

**Required Journal text and in-row read-only History**

**Save Journal** requires a Unicode letter or digit in Journal text and atomically records Completed; the three Daily Review answers are optional. Both hosts show field-level validation without losing edits. Cancel still saves partial content as Draft; empty new forms close without a write. Legacy answers-only Completed entries and revisions remain readable without migration. **Open review** expands its revisions/snapshot directly beneath the selected History row while retaining the aggregate Account filter, count, order, page and unsaved editor. Only **Open in editor** explicitly selects the original date/Account through existing guards. See [verification and remaining checks](docs/daily-journal.md#required-journal-text-and-inline-history-2026-10-07)..
