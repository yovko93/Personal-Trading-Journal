# M11.7 final acceptance — TopstepX import

Audit date: 2026-09-29. Actual HEAD: `94e0ed175766f0a540bf3c55be94e529d37ea2e7`, branch `develop`, initially clean. The reported `78f56a5` baseline was older; the Diagnostics fix was already committed. This audit changes documentation only. No production defect was confirmed, no Git writes (commit/push/branch changes) were performed, and the real journal was not accessed.

## Scope and implemented behavior

The audit covers M11.1–M11.7, not just the Import page:

| Stage | Audited contract and evidence |
| --- | --- |
| M11.1 parsing | Separate Topstep schema; exact decimals, explicit-offset UTC instants, broker TradeDay and subsecond duration preserved; line-located invalid/duplicate-ID diagnostics; cancellation. Synthetic parser/contract tests rerun. |
| M11.2 row semantics | One supported TopstepX Trades row becomes one Trade candidate. No inferred broker fills, merged timestamp groups or proven flat-to-flat positions. Internal boundary provenance is retained, not a warning-acknowledgment gate. |
| M11.3 economics | Exact directional price movement × quantity × verified point value equals reported Gross. Net = Gross − actual Fees − actual Commissions, counted once. Missing costs are not zero; inconsistent/unverified economics block. |
| M11.4 references | Explicit USD Account with ProviderName `Topstep`; no account inference. Canonical Instrument uniqueness and independently verified specifications precede pricing. MNQ may be proposed, never written during preview. |
| M11.5 preview | Read-only composition retains source lines, exact values, counts, proposals and actionable diagnostics. Source bytes/account/reference/economics bind the snapshot; source/account changes require a fresh preview. |
| M11.6 confirmation | Fresh reference/source revalidation inside SQLite transaction; atomic Trades, derived executions, browse rows, ledger and approved Instruments. Account-scoped exact replay skips; conflicting facts block without overwrites. |
| M11.7 Desktop | Explicit Tradovate/TopstepX choice before Select CSV; schema mismatch never falls through to the other parser. No Topstep warning/proposal checkboxes. Normal final dialog approves exact proposal specifications. Completed state clears preview, retains counts and uses Confirm import without a step number. |

Reviewed entry points include `ImportView.xaml`, `ImportViewModel.SelectCsvAsync`, its Topstep build/confirm methods, production DI, `TopstepImportPreviewBuilder`, `TopstepImportPreview.AcceptsConfirmation` and `TopstepImportStore.ImportAsync`. The store rechecks current reference matching sets and source content before deduplication or writes. Derived entry/exit records have no invented broker execution IDs; entry cost allocation is zero and exit receives the full reported costs once. Tradovate's unknown-cost and ambiguity policies remain unchanged.

The Diagnostics card depends only on `HasDiagnostics`; its complete border/heading/margin collapses when empty. The Topstep candidate view is independent. Real warnings/errors and separate format/confirmation recovery messages remain available. Internal account/provider identity is still `Topstep`; the visible source choice is `TopstepX`.

## Acceptance matrix

Automated and interactive evidence are deliberately separate. **Passed — automated** does not mean a person or UI automation exercised that screen.

| Scenario | Automated status and evidence | Interactive status |
| --- | --- | --- |
| Required source selection, exact names, wrong-source/unknown schema | **Passed** — command and compiled-WPF tests; one format diagnostic, no cross-provider parsing, recovery after reselection. | **Unverified** |
| Supplied-file preview and proposal | **Passed** — 25 accepted, 0 rejected, 25 candidates, no displayed grouping diagnostics; one MNQ proposal; confirmation available without checkboxes. Preview creates no import records. | **Unverified** |
| Empty Diagnostics card | **Passed** — ViewModel transitions, XAML card/spacing and compiled-WPF visibility tests. | **Passed — user-reported manual check**, explicitly confirmed in this request; not agent-observed. |
| Real warnings/errors and candidate visibility | **Passed** — proposal/incompatible-account/Tradovate diagnostics remain; candidate view is independent of the Diagnostics card. | **Unverified** |
| Final dialog, proposal approval and cancellation | **Passed** — exact specs asserted in dialog request; simulated Cancel retains preview with zero Trades/ledger/Instruments. Affirmative confirmation imports 25 Trades and one Instrument. | **Unverified** |
| Imported result and stored economics | **Passed** — 25 Trades, 50 derived executions, 25 ledger rows; persisted totals below. Old preview cannot resubmit. | **Unverified** |
| Fresh replay and completed-state reset | **Passed** — NoChanges, 25 skipped, no extra Instrument; preview cleared, counts retained and commands usable. Heading variants have regression coverage. | **Unverified** |
| Incompatible/deleted Account, ambiguous/wrong Instrument, bad economics | **Passed** — reference/economics/SQLite and Desktop tests block without writes. | **Unverified** |
| Stale source/account/Instrument and recovery | **Passed** — source/fingerprint/proposal checks; coded recovery message, stale confirmation disabled, Build Preview re-enabled, fresh preview usable. | **Unverified** |
| Duplicate conflict, mixed overlap, concurrent confirmation, rollback | **Passed** — isolated SQLite tests include post-SQL injected failure/cancellation; no partial graph or duplicate writes. | **Unverified** for UI interactions; transactional evidence is automated. |
| Cancellation, repeated clicks and source/file/account reset | **Passed** — gated command tests and transition tests preserve recovery and reject overlapping submission. | **Unverified** |
| Tradovate regression | **Passed** — same-session explicit source switch builds one ready candidate; existing matched-fill import/replay tests pass. | **Unverified** |

No failed acceptance scenario was found. The interactive **Unverified** entries remain completion gates; M11 is **not fully interactively accepted**.

## Supplied-file isolated rerun

The ignored local helper exercised actual Desktop ViewModel commands and production services, replacing only the file picker and dialog decision. It read the supplied CSV locally without emitting customer IDs/rows. Its fresh migrated database was:

```text
.tools/m11-routing-check/isolated-20260929-final-audit/PersonalTradingJournal/journal.db
```

Observed sequence:

- Preview: **25 accepted / 0 rejected / 25 candidates**, zero displayed grouping diagnostics, one proposed MNQ.
- Cancel final dialog: zero Trades, Topstep ledger rows or Instruments; preview retained.
- Confirm: **Imported 25 / skipped 0 / created Instruments 1**; active preview cleared and resubmission disabled.
- Fresh preview and confirm: **NoChanges / skipped 25**.
- Same-session source switch to Tradovate: one candidate, PreviewReady, confirmation available; preview-only, zero Tradovate ledger rows.
- Freshly rehydrated persisted Domain aggregates and execution totals:

| Gross (USD) | Fees (USD) | Commissions (USD) | Net (USD) |
| ---: | ---: | ---: | ---: |
| 1,241.00 | 51.84 | 36.00 | 1,153.16 |

Final database counts: 25 Trades, 50 executions, 25 Topstep ledger rows, one Instrument. No source file or broker identifier is a tracked deliverable. Simulated consent is automated evidence, not interactive confirmation.

## Windows attempt and user verification

The current Release binary was rebuilt and launched only with `--isolated-data-root`, using a second fresh seeded root `.tools/m11-routing-check/isolated-20260929-final-audit-ui` with synthetic Topstep/Tradovate USD accounts and no Trades/Instruments:

```text
C:\Users\Yovko\source\repos\Personal-Trading-Journal\src\PersonalTradingJournal.Desktop\bin\Release\net10.0-windows\PersonalTradingJournal.Desktop.exe
```

File version: `1.0.0.0`; product version: `1.0.0+94e0ed175766f0a540bf3c55be94e529d37ea2e7`. Paths and isolated-data arguments were checked. The sandbox process was not targetable; the outside-sandbox relaunch exposed Dashboard's accessibility tree. Clicking Import failed with **coordinate input geometry is unavailable**. Fresh window selection/activation and capture failed with **FrameArrived timed out: timed out waiting on channel**. No Import interaction was verified; startup alone is not acceptance. Both path/argument-verified isolated processes were stopped.

Separately, the user explicitly confirmed that the empty Diagnostics section now disappears correctly. This manual evidence is accepted for **that visual scenario only**. No other interactive scenario is inferred from it, and no screenshot was observed by the agent in this audit.

## Verification results

```powershell
dotnet build PersonalTradingJournal.sln --configuration Release
dotnet test PersonalTradingJournal.sln --configuration Release --no-build --filter "FullyQualifiedName~Topstep|FullyQualifiedName~ImportView|FullyQualifiedName~Tradovate"
dotnet test PersonalTradingJournal.sln --configuration Release --no-build
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure/PersonalTradingJournal.Infrastructure.csproj --context JournalDbContext --configuration Release --no-build
git diff --check
```

- Release build and restore: **passed, 0 warnings / 0 errors**.
- Focused: **462 passed** — Application 169, Infrastructure 231, Desktop 62; Domain has no matches for this filter.
- Full: **1,986 passed**, no failures/skips — Domain 400, Application 405, Infrastructure 651, Desktop 530.
- Migration consistency: **no pending model changes**. The design-time factory uses SQLite `:memory:`, not local application storage; migrated database tests also passed.
- Diff whitespace check: **passed**.
- Existing tests cover the audited rules; no additional code or test changes were needed.

## Remaining Windows checklist

Use the exact Release executable above with the absolute isolated root above (or a freshly seeded separate test root), never the real journal or a junction/alias to it. Do not omit `--isolated-data-root`.

The already-seeded, empty UI audit database can be opened from PowerShell with:

```powershell
& 'C:/Users/Yovko/source/repos/Personal-Trading-Journal/src/PersonalTradingJournal.Desktop/bin/Release/net10.0-windows/PersonalTradingJournal.Desktop.exe' --isolated-data-root 'C:/Users/Yovko/source/repos/Personal-Trading-Journal/.tools/m11-routing-check/isolated-20260929-final-audit-ui'
```

1. Open Import. Verify Select CSV is disabled until choosing Tradovate or TopstepX. Choose TopstepX, the supplied CSV and the synthetic Topstep USD Account; Build Preview must show 25 valid / 25 Trades, no warning checkboxes, and the expected economics.
2. Inspect the proposed MNQ and real proposal diagnostic. Import Trades must show its exact specifications in the normal final dialog. Cancel, then confirm: Imported 25, one Instrument; intermediate sections disappear and Confirm import retains counts. Inspect imported Trades/Details against the totals above.
3. Re-select the file/account, build and confirm again: NoChanges, 25 skipped. The now-existing Instrument requires no creation; commands remain usable and the completed preview cannot be submitted again.
4. Switch to Tradovate: previous state must clear. Selecting the TopstepX file must show one mismatch. Select the small valid Tradovate CSV instead and verify its normal preview/warnings without switching provider automatically.
5. With TopstepX, select the wrong-provider synthetic account: blocked diagnostics remain visible. Restore the compatible account and rebuild. For stale-source recovery, use a disposable synthetic CSV, preview it, then append a blank line externally before confirming: expect SOURCE_CHANGED and no writes; rebuild restores confirmation.
6. Check keyboard access to source/account/dialog controls, Cancel operation during an in-progress operation where timing allows, and repeated Import clicks: no concurrent submissions or duplicate writes. Stale/ambiguous reference tests remain automated until repeated against an isolated reference-data mutation.

## Findings, limitations and worktree

The confirmed inconsistencies were documentation-only: obsolete baseline/worktree inventory, outdated test counts, a Tradovate instruction omitting the now-required source choice, and no distinction for the user's Diagnostics verification. These were corrected without changing import behavior.

Remaining supported limits: reviewed USD cost semantics; built-in verified MNQ creation profile; no Desktop attestation editor for other roots; no inferred broker account or recovered raw-fill history. One supported TopstepX row is one Trade, not proof of a complete broker position. Costs come from the CSV, not a fixed published rate. Existing Trade hard delete removes its deduplication identity and permits explicit reimport, as documented.

Changed tracked files: `README.md`, `docs/topstep-csv-import.md`, `docs/m11-acceptance.md`. Final worktree: these **three modified documentation files**, no new tracked/untracked deliverables; ignored isolated helper/data remain local. HEAD and branch are unchanged. No commit or push.

Suggested manual commit message: `docs: finalize TopstepX import acceptance audit`
