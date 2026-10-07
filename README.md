# Personal Trading Journal

Personal Trading Journal is a local-first Windows desktop application designed to help traders record, review, analyze, and improve their trading process. The initial focus is futures trading, especially instruments such as NQ and ES, while the architecture is intended to remain extensible to other markets and a possible future SaaS or web version.

The repository currently contains the application foundation, the core trading Domain model, local EF Core/SQLite persistence, the WPF shell and navigation foundation, persisted System/Dark/Light appearance preferences, complete lifecycle management for Trading Accounts, Instruments, Trading Setups, and Trading Mistakes, and manual Trade create/list/view/edit/close/delete workflows. Trades also support local screenshots, Setup classification, Mistake assignments, an authoritative SQLite-paged and sortable browse view, and reviewed Tradovate matched-fills and Topstep closed-row CSV import workflows. Dashboard and Calendar provide analytics and daily Trade review. The Journal page supports daily text, read-only Trade context, three Daily Review questions, explicit completion/reopening, exact-scope launch from Calendar and paged Review History with read-only durable revisions. AI capabilities remain deferred.

## Current Status

**Completed Cancel and aggregate Calendar journal indicators**

Reopen now opens a local editor without changing the saved Completed state. Cancel with all four fields unchanged (including change-then-revert) closes without a write or revision; changed Cancel saves one Draft revision using the loaded concurrency token. Save still requires meaningful Journal text and completes. Calendar **All accounts** indicators now include every account and the distinct null-scoped journal: any Draft shows **Draft**, otherwise **✓**, with accessible counts for mixed states. Modal editing remains exact-scope. See [behavior and verification](docs/daily-journal.md#completed-cancel-and-calendar-aggregation-2026-10-07).

**Journal previews, distinct History actions and older-revision deletion**

Expanded History rows now preview current Journal text and nonempty Daily Review answers, capped to three readable lines. Open review (amber), Open in editor (slate), and View revision (sky) have distinct shared Light/Dark styles. Confirmed **Delete revision** permanently removes only an older snapshot. The current/latest revision is explicitly protected; journal content/status, concurrency token and future revision numbering do not change. Aggregate History and unsaved editor fields remain unchanged by previewing or revision deletion. Save still requires Journal text and completes the entry; Cancel saves changed content as Draft, with the unchanged-Completed exception above. See [revision deletion policy and verification](docs/daily-journal.md#previews-action-colors-and-revision-deletion-2026-10-07).
