# Notebook

## M22.12A22 — Matching New tag and New folder styling

29 October 2026, one agent. Actual baseline: clean `develop` / **`39651eda63567cb12a9944885c4e562becdfb3de`**, worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. A21's previously pending files were committed in the intervening commit. No repository/ancestor AGENTS.md or competing testhost was found. A21 and historical evidence are preserved. Tested source is this HEAD plus the four-file A22 inventory below.

New tag previously overrode font size (12), padding (6,4) and minimum height (32). Both buttons now opt into `NotebookCreationButton`, derived from the existing teal `NotebookActionButton`, with shared width **112 DIP**, height **34 DIP** and left content alignment. Inherited Segoe UI 14/SemiBold, padding 10,6, one-DIP border and existing small corner radius match New folder's established presentation. The fixed width removes label-length sizing differences; left content alignment gives both plus signs the same inset and baseline. Existing theme hover/pressed/disabled colors and keyboard-focus rectangle are shared through the same style/template. No global resource changed.

Both controls remain left-aligned beneath their respective lists. Click handlers, bindings, accessibility names/tooltips, owned focus, forms and right-aligned Cancel controls are unchanged. No creation, validation, asynchronous ownership, note-mode or tag-menu behavior changed. Existing creation-behavior evidence is reused; it is not counted as new verification.

### Verification and commands

```powershell
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore
$env:PTJ_TEST_RESULTS_DIRECTORY = Join-Path (Get-Location) 'TestResults/m2212a22-verified/presentation'
dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~CreationButtonsMatchDimensionsTypographyAndStateOffscreen' --logger 'trx;LogFileName=creation-buttons.trx' --results-directory TestResults/m2212a22-verified/presentation
pwsh -NoProfile -File TestResults/m2212a22-verified/check-whitespace.ps1
pwsh -NoProfile -File TestResults/m2212a22-verified/check-doc-links.ps1
```

Release build/dependencies: **zero warnings/errors, exit 0** (`build.log`). One focused invocation: **4/4 distinct parents and 4/4 matching distinct isolated children**, zero failures/skips, exit 0 (`presentation.log`, `presentation/creation-buttons.trx` and each child TRX/supervision log). No repeated selections, overlaps or initial failed attempts. Exact display-name selection, single-result validation, existing exclusive WPF collection, 90-second child deadline and 30-second watchdog are preserved.

The offscreen checks compare actual arranged dimensions, font family/size/weight, padding, borders, corner radius and left alignment; measured label text fits without clipping. Both controls remain focusable and retain the template's keyboard-focus indicator trigger. A held synthetic New note write supplies a legitimate busy/disabled state; both buttons keep identical dimensions/colors, then re-enable on completion. No native pointer/focus state was forced. Hover/pressed/focus inheritance was inspected in the shared template; these states were not activated with native input.

Eight actual images opened/inspected under `TestResults/m2212a22-verified/presentation/`. Append `/creation-button-renders/<prefix><state>.png`, where state is `enabled` or `busy-disabled`:

| Theme / viewport / DPI | Child directory | Prefix |
| --- | --- | --- |
| Light / 1280 / 96 | `notebook-creation-buttons-20261029-131417-64e88f94e29646c58cc5c0c661a05cf1` | `Light-1280-96-` |
| Light / 480 / 240 | `notebook-creation-buttons-20261029-131427-297d1c678d1244d8adf8b2fd67aaa20b` | `Light-480-240-` |
| Dark / 1280 / 96 | `notebook-creation-buttons-20261029-131438-e017e1232b5445e19bf2709b953b25d0` | `Dark-1280-96-` |
| Dark / 480 / 240 | `notebook-creation-buttons-20261029-131448-f590de2ea34e4bbd805c6a009074d791` | `Dark-480-240-` |

Both buttons are visible together, equally sized and readable; no clipping/alignment defect was observed. Narrow captures select Organization using the existing panel layout. Evidence/hashes/identity audit: `TestResults/m2212a22-verified/summary.json`. Whitespace and local documentation-target checks passed after documentation edits. No native input, complete gate, provider/production access, publication, schema/format/CI/deadline changes or workflow trigger.

Exact task inventory: **three modified files and one added file**, all A22:

- `README.md`
- `docs/notebook.md`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookHeaderViewTests.CreationButtons.cs` (new)

**A22 focused presentation verification is complete. A23 tag-menu toggling, resumed M22.12B and overall acceptance remain pending.** A21's automated acceptance is preserved; this styling selection adds no native/human, Narrator, restart or hosted acceptance. The user's incomplete manual report remains separate from automated evidence.

## M22.12A21 — Readable note view with explicit Edit mode

29 October 2026, one agent. Actual baseline: **clean `develop` / `672da3309a88aaf1e51969a4d24930f92475dc8b`**, worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. The intervening commit since `d2d981b` contains B2 documentation only. No AGENTS.md was found in the repository/ancestors. Existing implementation, isolated manual profile and historical evidence are preserved. Results apply to HEAD **plus the uncommitted A21 inventory below**, not HEAD alone. Source/binary hashes are in `TestResults/m2212a21-verified/`.

### Behavior and ownership

- Existing-note selection and clean activation/linked return load reading mode. The same allowlisted `NotebookEditorAdapter` / `RichTextBox` document remains in use: no plain-text flattening, format change, presentation-only save or mode-driven document replacement. Existing 15-DIP body text, 23-DIP line height, headings, paragraph spacing, emphasis, lists and checklist indicators are retained. Title input chrome and formatting/insertion/assignment-removal/image-edit controls hide while reading. Text remains selectable/copyable. Hyperlinks use the existing HTTP(S) validation/launch boundary; original-image viewing and linked-target opening remain available.
- Header order is **persistent save status → Pin → Edit/Done → note menu**. Edit retains the same draft, document and undo stack. Checklist changes, paste/cut, formatting and image edits require the additional UI edit capability. Internal draft capabilities still serve autosave/recovery; this is not a parallel persistence/save-state machine.
- Done calls `SaveDraftAsync`, including its serialized in-flight flush/checkpoint coordination. It returns to reading only after canonical save success, unchanged draft revision, retained selection/activation ownership and no dirty/saving/paused/conflicting/detached state. A recovery checkpoint alone never acknowledges Done or Saved locally. Failure/conflict/newer input retains Edit and recovery/retry feedback. No permanent Cancel or new autosave timing was introduced.
- Blank and all four nonblank templates enter Edit after existing atomic creation; duplicates also enter Edit. Existing archived/trashed/unreadable content gains no edit capability. Explicit recovery opens its retained draft in Edit, read-only where existing capabilities require it, with persistent explanations. A20's read-only retained detail during loading, cancellation, latest-result checks and per-note history separation remain.
- The rendered regression exposed an existing save-publication edge: `Notes.Clear()` makes WPF's bound ListBox briefly clear/reselect its row while `SaveCore` publishes refreshed membership. That advanced accepted-selection generation despite retaining the displayed editor, so Done refused to exit after a successful save. `AcceptSelection` now excludes **only `_publishing && IsSaving`** from navigation-generation advancement. Actual accepted navigation still advances it; user selection is already guarded during writes. A saved note outside its filter retains its editor ownership. No query, persistence or scheduling change was made.

### Focused evidence

Root: **`TestResults/m2212a21-verified/`**. `summary.json`, `result-audit.json`, `tested-source.json` and `inspected-captures.json` retain counts, identities, paths and hashes. Literal-path TRX auditing compares declared totals with result rows and rejects duplicate identities within each file. Existing exact child selection/validation, recursion protection, 90-second process deadlines and 30-second watchdogs are unchanged.

| Selection / artifact | Parents | Children | Result |
| --- | ---: | ---: | --- |
| `logic-final/logic.trx`: ReadingMode, Editing, Autosave, DetailSwitch, NoteActions, Link, ImageCoordination | 93 | 0 | All passed, exit 0 |
| `views-final/views.trx`: reading, A20 transition, A16 status, editor, links, images | 19 | 22 | 15 parents / 18 children passed; four image rows failed after Refresh returned to reading |
| `images-corrected/images.trx`: complete image family, explicit Edit after Refresh | 4 | 4 | All passed, exit 0; overlaps those four rows |
| `reading-history/reading.trx`: strengthened reading family, also WPF text-only undo across Done/Edit | 4 | 4 | All passed, exit 0; overlaps the four reading rows |
| **Final distinct current-source coverage** | **112** | **22** | **All passed; zero remaining failures/skips/incomplete cases** |

One selected legacy editor parent launches its four distinct child theme/DPI rows once. Other selected isolated parents retain exact one-row dispatch. Parent and child totals are **not added**. Coverage includes 16 new logic cases and four new isolated presentation cases. The native `NotebookPresentationViewTests` setup was adapted to enter Edit but that native family was not executed here.

Retained failures: `logic/logic.trx` passed 15/16; its reactivation fixture refused the test's discard confirmation, corrected by explicitly approving that fake dialog. `render-initial/` and `render-diagnostic/` each retain one failed Light-normal parent/child; diagnostics showed **one successful write, unchanged draft revision, no dirty state and Saved**, establishing the selection-generation defect above. The four image failures in `views-final/` attempted paste after Refresh while read-only; setup now enters Edit before continuing the unchanged byte/ordering/caption/history assertions. The initial test build retained three compiler errors from declaring an encoded-content record as a string, corrected to its actual inferred type. No assertions were removed or trimmed. Development totals: **138 parent executions / 32 child executions**, including **seven parent / six child failure results**; overlaps are not extra coverage or hidden successes.

Commands, from the repository (each test invocation also set `PTJ_TEST_RESULTS_DIRECTORY` to its absolute result directory; logs and exit files retained):

```powershell
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore
$logic = 'FullyQualifiedName~NotebookReadingModeTests|FullyQualifiedName~NotebookEditingTests|FullyQualifiedName~NotebookAutosaveTests|FullyQualifiedName~NotebookDetailSwitchTests|FullyQualifiedName~NotebookNoteActionsTests|FullyQualifiedName~NotebookLinkTests|FullyQualifiedName~NotebookImageCoordinationTests'
dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter $logic --logger 'trx;LogFileName=logic.trx' --results-directory TestResults/m2212a21-verified/logic-final
$views = 'FullyQualifiedName~ReadingAndEditShareRichDocumentAndHistoryOffscreen|FullyQualifiedName~DelayedNoteSwitchRetainsReadOnlyDetailAndWorkspaceGeometry|FullyQualifiedName~SaveStatusBesidePinReflectsCanonicalStateOffscreen|FullyQualifiedName~NotebookImageEditorTests|FullyQualifiedName~NotebookLinkViewTests|(FullyQualifiedName~NotebookEditorViewTests&DisplayName~Light&DisplayName~1280)'
dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter $views --logger 'trx;LogFileName=views.trx' --results-directory TestResults/m2212a21-verified/views-final
dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~NotebookImageEditorTests' --logger 'trx;LogFileName=images.trx' --results-directory TestResults/m2212a21-verified/images-corrected
dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~ReadingAndEditShareRichDocumentAndHistoryOffscreen' --logger 'trx;LogFileName=reading.trx' --results-directory TestResults/m2212a21-verified/reading-history
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
pwsh -NoProfile -File TestResults/m2212a21-verified/check-whitespace.ps1
pwsh -NoProfile -File TestResults/m2212a21-verified/check-doc-links.ps1
```

Final build `build-history.log`: **zero warnings/errors**, dependencies included. EF `ef.log`: no pending model changes, existing design-time factory. Whitespace covers tracked changes and three new files; local Markdown targets are checked after documentation edits. No runtime repetition for documentation-only changes and no complete gate.

### Inspected captures and limits

Twelve original offscreen images opened/inspected: `reading`, `reading-links`, `editing` states in each directory below, under **`TestResults/m2212a21-verified/views-final/`**. Append `/reading-renders/<prefix><state>.png`:

| Theme / size / DPI | Child directory | Prefix |
| --- | --- | --- |
| Dark / 1280 / 96 | `notebook-reading-20261029-124808-03543d7c11dd4f5b8f352ff4b1405075` | `Dark-1280-96-` |
| Dark / 480 / 240 | `notebook-reading-20261029-124816-4096db7ddba24a56bfd19131a2fb1279` | `Dark-480-240-` |
| Light / 1280 / 96 | `notebook-reading-20261029-124824-38fd01dd356e4c08a5c7eb027b9f0790` | `Light-1280-96-` |
| Light / 480 / 240 | `notebook-reading-20261029-124832-4d4cd1eca0c14c42981e22e3176dcaac` | `Light-480-240-` |

No blocking clipping/contrast issue was observed in scope. Long titles wrap and the header actions remain reachable at 480 DIP/240 DPI. Reading content fits the 1050-DIP viewport, so bottom-scroll reading frames retain the same viewport; editing uses existing scrolling for lower linked records. Later test-only changes added explicit Edit to the old image fixture and appended text-only undo assertions after these captures; production and capture inputs are unchanged. Additional strengthened-run captures are retained without inflating the inspected count. Existing shell, explicit checklist text and recovery controls are retained deliberate differences.

All automated interactions were offscreen WPF commands/routed events and synthetic image/link/clipboard ports. The hyperlink launch delegate was observed without opening a browser. **No native pointer/focus workflow, real clipboard/picker/viewer, journal restart, Narrator or hosted verification occurred.** The user's report of manual checks passing so far remains an incomplete human report; no B2 check ID was marked passed. B/B1 capture findings are unchanged; no troubleshooting resumed. B2's launch procedure/profile remains available, but its recorded binary hash predates A21 and must be rechecked before resumed manual verification.

**A21 focused automated verification is complete. A22 — New tag sizing, A23 — tag-menu toggling, resumed M22.12B and overall acceptance remain pending.** No schema/format, CI/deadline/scheduling, production/provider, publication or workflow changes.

### Exact A21 inventory

**17 modified files + 3 additions**, all A21; initial worktree clean. Full paths are the prefix plus each filename below:

- Documentation: `README.md`, `docs/notebook.md`.
- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/`: `NotebookWorkspaceViewModel.cs`, `NotebookWorkspaceViewModel.Editing.cs`, `NotebookWorkspaceViewModel.Autosave.cs`, `NotebookWorkspaceViewModel.Lifecycle.cs`, `NotebookWorkspaceViewModel.CopyDelete.cs`; **new** `NotebookWorkspaceViewModel.Presentation.cs`.
- `src/PersonalTradingJournal.Desktop/Views/Notebook/`: `NotebookWorkspaceView.xaml`, `NotebookWorkspaceView.xaml.cs`, `NotebookWorkspaceView.History.cs`, `NotebookWorkspaceView.Images.cs`, `NotebookWorkspaceView.Toolbar.cs`.
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/`: `NotebookHeaderViewTests.SaveStatus.cs`, `NotebookEditorViewTests.cs`, `NotebookImageEditorTests.cs`, `NotebookLinkViewTests.cs`, `NotebookPresentationViewTests.cs`; **new** `NotebookReadingModeTests.cs`, `NotebookHeaderViewTests.Reading.cs`.

## M22.12B2 — Prepared human native verification

29 October 2026, one agent. Actual baseline: **clean develop / `d2d981becea04ed63f7239dd27aacc0e86b7b4fd`**, existing worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. The intervening commit contains B1's two documentation files only. Repository/ancestor inspection found no AGENTS.md. Exact tracked B2 inventory: **README.md and docs/notebook.md only**. New ignored preparation evidence is under **`TestResults/m2212b2-manual/`**; B/B1 evidence and the existing profile are preserved.

This phase prepares human verification; it does not operate the native application. No Computer Use/capture attempt, application launch, build, test, gate or production/harness change occurred. B/B1's accurate finding remains: **window discovery succeeded, frame capture timed out; general capture failure and PTJ unresponsiveness remain unproven**. Tool support was recommended previously; **no support investigation/ticket has been opened by the agent**. Human manual verification can proceed without claiming that capture has been repaired.

### Verified source, binary and isolation

Only README/docs changed between B's verified source and current HEAD; no source/test/build-configuration successor invalidates its binaries. The production DLL still has SHA-256 **`991EB49BA5B2356ADCB72C24BC1AE464DD2C8C5399D3F4E5510E9B26B3EC5178`**, matching retained B/A20 evidence; the executable exists at the path below. `baseline.json` records source/hash/process observations. No PTJ or test process was active at preparation time. A rebuild was unnecessary.

The current compiled `DesktopApplicationPaths.FromArguments` factory was invoked read-only with **`--isolated-data-root`**, not an environment override. Every resolved path remains under:

`C:/Users/Yovko/source/repos/Personal-Trading-Journal/TestResults/m2212b-native/profile`

`resolved-paths.json` records the exact results. The database is `PersonalTradingJournal/journal.db`; Notebook recovery checkpoints and ownership metadata use this database. Managed images/originals are under `PersonalTradingJournal/screenshots`; logs/backups/settings use `PersonalTradingJournal/{logs,backups,settings.json}`. Credential/provider paths resolve to the sibling `PersonalTradingJournal.Secrets` beneath the same isolated profile. The existing database, image/backups/log directories and synthetic PNG were checked by filesystem metadata only; the real journal was not read/copied and the isolated database was not edited. No provider operation is part of this procedure.

### Launch and confirm the profile before editing — P0

Run this **in PowerShell**. It works independently of the terminal's current directory and uses the same supported normal-startup option as B/B1. The quoted profile argument deliberately includes command-line quotes for Start-Process:

```powershell
$journalB2 = Start-Process -FilePath 'C:\Users\Yovko\source\repos\Personal-Trading-Journal\src\PersonalTradingJournal.Desktop\bin\Release\net10.0-windows\PersonalTradingJournal.Desktop.exe' `
    -WorkingDirectory 'C:\Users\Yovko\source\repos\Personal-Trading-Journal\src\PersonalTradingJournal.Desktop\bin\Release\net10.0-windows' `
    -ArgumentList '--isolated-data-root', '"C:\Users\Yovko\source\repos\Personal-Trading-Journal\TestResults\m2212b-native\profile"' -PassThru
$journalB2.Id
```

Before editing, inspect that exact process with the following read-only command. Confirm its executable and **both** the `--isolated-data-root` switch and full profile path above in CommandLine. If PowerShell cannot read this metadata, inspect the same PID's Command line in Task Manager's Details view instead; do not infer the profile from the app title alone. Stop and report P0 as Failed/Not tested if it cannot be confirmed.

```powershell
Get-CimInstance Win32_Process -Filter "ProcessId = $($journalB2.Id)" |
    Select-Object ProcessId, ExecutablePath, CommandLine
```

Also check that a newly timestamped startup entry appears in a `ptj-*.log` beneath the **isolated** `profile/PersonalTradingJournal/logs` directory. The factory/path verification above explains where this process stores its journal; neither a familiar window title nor an empty note list is sufficient proof by itself. Record **P0**, the PID and confirmed profile path. Use this launch command rather than a normal app shortcut, and use only the confirmed isolated window. Do not inspect, copy or edit the real journal to compare it.

### Synthetic prerequisites

Create fixtures through the application's existing controls only, after P0 is confirmed. Use clearly different synthetic titles/bodies such as **B2 Alpha — alpha text**, **B2 Beta — beta text**, **B2 Gamma — gamma text**, plus a disposable note moved to Trash. Keep Alpha unpinned and Beta pinned. Use a real synthetic folder, a retained tag and a spare global-deletion tag; leave the representative restart note separate from deletion fixtures. If needed, create these fixtures while working through the creation checks below, then return to group A.

The existing synthetic PNG is:

`C:/Users/Yovko/source/repos/Personal-Trading-Journal/TestResults/m2212b-native/assets/synthetic-chart.png`

For C3, create **one synthetic manual Trade and one Trading Setup** through their existing application screens in this profile. Trade entry may require a local synthetic account and instrument; create/select appropriate local records without connecting a provider or importing real data. Record target names/IDs so correct navigation can be checked. Missing targets make C3 **Not tested**, not a pass based on another action. For C4, use a separately identified synthetic WebP only if available; no WebP fixture or installed-codec result is assumed. Before the clipboard check, preserve any clipboard content you need; record whether it could be restored afterward.

### Short grouped manual checklist

Every row below is initially **Not tested**. Perform manageable groups and return results by ID; failed or missing subchecks must be stated in Observation rather than hidden behind a group pass. No artificial production slowdown is needed.

**A — Navigation and switching**

1. **A1:** From All Notes, click Trash once; repeat from Pinned. **Expected:** Trash highlight, heading and only trashed rows/count agree on the first accepted click.
2. **A2:** Switch Alpha/Beta/Gamma normally, then rapidly A→B→C and A→B→A with the list scrolled where possible. **Expected:** Organization/list geometry and scroll stay stable during clean selection, no whole-workspace blank/rebuild, and the final title/body/tags/images/links/save status all belong to the last accepted note. **A2L:** If a loading interval is visible, retained previous content is clearly identified and read-only. If too fast to observe, report A2L **Not tested — pending state not visible**, without introducing a delay or claiming it passed.
3. **A3:** Edit a note, switch away and return. **Expected:** edits survive canonical autosave/leave handling and Saved locally appears only for saved current content. If a Save/Discard/Cancel prompt appears, Cancel retains the prior selection/draft; report that subcheck unobserved if no prompt appears. Newer edits must not be replaced by an older completion.
4. **A4:** Use explicit Workspace Refresh. **Expected:** current stored results reload; current edits receive the existing save/navigation protection rather than disappearing.

**B — Organization and editor**

1. **B1:** Exercise New folder with empty/whitespace and populated names, Add and Enter, Cancel and Escape; rename a created folder, then confirm deletion of a synthetic folder containing a note. **Expected:** conditional Add, visible right-aligned Cancel that stays anchored, one creation per submission, no creation on cancellation, updated names, contained note preserved and All Notes selected after deletion.
2. **B2:** Exercise New tag/Add/Enter/Cancel/Escape, tag filtering and its management menu. Rename the spare tag; assign tags to a note, remove one assigned chip, then globally delete only the spare tag through confirmation. **Expected:** New tag below the catalog, stable right-aligned Cancel, creation neither assigns nor filters automatically, chip removal keeps the global catalog tag, global deletion removes that tag/its assignments without deleting notes, and menu actions target the intended tag without toggling its filter.
3. **B3:** Create a blank note using the main action and one existing template using its menu; use the main action again after choosing a template. Edit title/rich text, headings/lists/checklist and a hyperlink; exercise Undo/Redo. **Expected:** main action always blank, template content appears once, formatting affects the intended selection, checklist insertion and checked-state control remain distinct, and Undo never introduces another note's content. A synthetic URL can be entered without opening an external website.
4. **B4:** Pin/unpin a synthetic note and toggle the note ellipsis plus a tag management menu with repeated clicks, Escape and outside click. **Expected:** authoritative pin state, first open/second close/reopen, no accidental action on opening/dismissing, one intended execution when choosing an item, and actions continue to target their captured note/tag after selection changes. Workspace Refresh stays directly reachable.
5. **B5:** Observe Saved locally with its green check immediately left of pin, and an operation notification when one appears; time its automatic dismissal at approximately ten seconds and try manual dismissal on another. Inspect Light/Dark and a narrow/high-DPI layout if available. **Expected:** notifications do not take focus or clear persistent failure/recovery/save state; no routine autosave success-banner backlog; dates use English `MMM dd, yyyy` without time, controls/text remain readable. Record any unavailable theme/DPI/notification subcheck separately.

**C — Images and linked records**

1. **C1:** Import the synthetic PNG through the real file picker. Copy a synthetic bitmap from an image application and paste it through the real clipboard into a note. **Expected:** both insertions appear once at the intended positions without losing nearby text. State any clipboard-preservation/restoration limitation; a file-path paste is not the bitmap-paste check.
2. **C2:** Caption an image, change image order, open its original/viewer, remove a disposable image reference and Undo/Redo. **Expected:** readable preview/caption, correct ordering/reference removal, original opens and retained originals resolve after undo. Keep an image/caption for D3.
3. **C3:** Link the synthetic Trade and Trading Setup, open each and use the application's Notebook-return action. **Expected:** each correct target opens and return restores the originating note/context, not an unrelated selection. Record target IDs/names. If a target cannot be prepared, mark its subcheck Not tested.
4. **C4:** If a synthetic WebP exists, import/open it using the installed codec path. **Expected:** record the actual successful decoding or exact rejection/error independently of PNG. Otherwise **Not tested — no suitable synthetic WebP**.

**D — Lifecycle and actual process restart**

1. **D1:** Duplicate a synthetic note, edit the copy, Trash and Restore it. **Expected:** source content remains independent; restored note is discoverable with its relationships/content preserved.
2. **D2:** Use a different disposable synthetic note for permanent deletion: move it to Trash, inspect/cancel confirmation first, then explicitly confirm deletion. **Expected:** cancellation preserves it; confirmed Trash-only deletion removes only that disposable note. Do not use the representative restart note.
3. **D3:** Prepare a representative note with text, assigned tags, an image/caption and both linked-record kinds. Wait for authoritative Saved locally, record its title/details and the first PID, then close normally. **Expected:** the old PID exits, relaunching with the **same launch block/profile** creates a different PID, and reopening the representative note shows persisted text/tags/image/caption/link targets and readable originals. Record old/new PIDs and the profile path. Merely seeing a new window/PID is insufficient.

After closing in D3, this read-only check should return **no process** before relaunch (record the first PID before reusing `$journalB2`):

```powershell
Get-Process -Id $journalB2.Id -ErrorAction SilentlyContinue
```

### Results, evidence and pending status

Return rows in this compact format; use exactly **Passed / Failed / Not tested** for each ID:

```text
Check ID | Passed / Failed / Not tested | Observation
P0 | Not tested | PID and isolated command-line confirmation pending
A1 | Not tested | All Notes and Pinned first-click paths pending
A2L | Not tested | Say whether the loading state was visible
D3 | Not tested | Old PID, new PID, same profile and persisted content details pending
```

Use the same format for all other IDs; `response-template.txt` under the preparation evidence root contains every ID initialized to Not tested. **Send screenshots only for visual discrepancies or failures**, identifying the check ID and action; success does not require screenshots. Human-reported outcomes will be labeled as such and retained separately from independently collected automated tests/render/metadata evidence. No workflow is passed until the user supplies its result. Partial/missing observations stay Not tested, including unobserved pending-state behavior and unavailable link/WebP/DPI prerequisites.

Narrator assessment, hosted CI and abrupt-termination recovery are separate unverified categories; do not substitute ordinary keyboard use or graceful restart for them. No deliberate kill/crash is part of this checklist. No capture troubleshooting or support case was opened in B2. **M22.12B remains pending until human results are recorded and reviewed. Overall acceptance remains pending the final Release gate and required unresolved checks.** Preparation-only whitespace/local Markdown-target checks are recorded beside `summary.json`; no native workflow, test parent or isolated child was run. No CI, production/provider access, Git publication or workflow triggering occurred.

## M22.12B1 — Bounded native capture diagnosis

29 October 2026, one agent. Actual baseline: **clean develop / `d2606d39fcf27cec122f179ecd2f3bd9f117653e`**, existing worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. The intervening commit includes B's two documentation files only. All B artifacts, including its approval timeout and two capture errors, remain unchanged; new evidence is under **`TestResults/m2212b1-native/`**. Exact tracked task inventory: **README.md and docs/notebook.md only**. No production or test-harness changes were made.

### Supported diagnostic boundary and new evidence

The installed Computer Use SKILL.md and its complete guidance/API/confirmation documents were read. The documented surface exposes app/window discovery, activation, `get_window_state` with separate screenshot/text flags, and targeted input. The installed docs directory contains only `api.md`, `guidance.md` and `confirmations.md`; there is no documented capture-helper health/session, minimize-state mutation, graphics-backend reset or helper-restart endpoint. No undocumented API, helper executable/protocol, alternative screenshot mechanism, direct PowerShell UI automation, blind coordinate input or ChatGPT UI automation was used. Existing approvals were reused; no new app-approval timeout occurred.

B's combined activation/capture calls did not identify the exact failing sub-operation. B1 separated the final PTJ screenshot request from activation, and changed the process/window lifetime rather than repeating B's fresh-selection retry against the same old HWND. A harmless blank comparison was prepared first: no Paint process/window existed initially, and supported `sky.launch_app` launched pre-existing `C:/Windows/System32/mspaint.exe`, returning **Untitled - Paint**, PID **22708**, window **1837138**, session **1**.

| Order / operation | Exact observation | Interpretation |
| --- | --- | --- |
| Paint `get_window_state(screenshot=true, text=false)` without combined activation | `window is minimized; call activate_window, refresh with get_window, then retry get_window_state` | Explicit minimized-window prerequisite; no graphics frame delivered, so not a general capture failure test |
| Paint `activate_window`, as directed | `user input was detected in this window; call get_window_state before continuing` | Supported activation blocked by input guard; no manual input event/cause is established by this message |
| Required Paint `get_window` / state refresh | Same minimized diagnostic | Refreshed identity/state did not resolve the prerequisite |
| Paint activation after that refresh | Same user-input guard | Stop this activation/state loop; no forced state or further Paint capture attempts |
| Normal PTJ startup with existing profile; unique discovery | PID **15676**, window **133322**, exact Release executable app identity/title **Personal Trading Journal** | New process/window rather than B's PID 10888/window 460268; discovery succeeds |
| **One** explicit PTJ `get_window_state(screenshot=true, text=false)`; no combined activation | **`window capture timed out: timed out waiting on channel`** | The failed public operation is now identified; no screenshot/accessibility state returned, no further PTJ capture attempted |

`events.json` preserves this sequence. It does not assign fabricated per-operation timestamps. The harmless comparison is **inconclusive** because its minimized/input prerequisites prevented a valid frame-capture comparison; it cannot establish that capture fails across applications. No additional kernel reset/relaunch loop, longer timeout, alternate capture path or application rendering adjustment was justified.

### Identity, isolation and session evidence

The current production DLL still matches B/A20 SHA-256 **`991EB49BA5B2356ADCB72C24BC1AE464DD2C8C5399D3F4E5510E9B26B3EC5178`**. No build was needed. The compiled `DesktopApplicationPaths.FromArguments` factory was re-invoked read-only with the existing `--isolated-data-root` argument, and all returned paths were again checked beneath **`TestResults/m2212b-native/profile`**. `resolved-paths.json` records database, managed screenshots/originals, settings, logs, backups and credential-path locations; recovery remains in that isolated database. No real journal or provider was accessed, no database data was copied/edited, and no synthetic native fixtures were inserted.

The existing supported executor's read-only observer records **session 1 / WinSta0 / Default**, cursor access true/error 0, interactive, its existing administrator token and no competing dotnet/testhost/vstest process (`supported-context.json`). The PTJ process and blank Paint process also reported session **1**. There was no token/desktop switch, always-on-top behavior, forced focus/capture/control state, or closure of unrelated apps. These caller/application observations do **not** expose or prove the Computer Use capture helper's desktop/session. A backend/session mismatch therefore remains unverified rather than excluded.

Read-only process samples reported `Responding=true` for both processes (`window-process-evidence.json`). That metadata is not an accessibility/render responsiveness proof. PTJ processed a supported normal-close shortcut afterward and logged normal shutdown; the capture timeout alone does not establish an unresponsive application. PTJ minimized status, visible bounds, occlusion and graphics frame availability could not be inspected because no usable snapshot was returned. Unlike Paint, PTJ's error did not explicitly diagnose minimized state; that absence is not proof of visibility. Discovery tied the sole new PTJ window to the exact executable while the sole task-launched PTJ process was alive; B's stale window identity was never reused.

```powershell
# Existing supported input desktop; read-only observer followed by normal production startup:
pwsh -NoProfile -File TestResults/m2212b1-native/context-check.ps1 -OutputName supported-context.json -Execution 'B1 supported executor; read-only desktop observation; no desktop/token switching'
pwsh -NoProfile -File TestResults/m2212b1-native/launch.ps1
pwsh -NoProfile -File TestResults/m2212b1-native/check-whitespace.ps1
pwsh -NoProfile -File TestResults/m2212b1-native/check-doc-links.ps1
```

The launcher uses ProcessStartInfo.ArgumentList with the normal existing executable and **B's existing isolated profile**, not a replacement profile or runner. `launch.json` records PID/session/arguments/hash. Original application logs remain under `TestResults/m2212b-native/profile/PersonalTradingJournal/logs/`; B1's startup/shutdown appended to those isolated logs without replacing B's records. The public JS calls and their exact outcomes are listed in `events.json`. No request used an increased timeout.

### Cleanup, acceptance and concrete handoff

After the failed bounded capture, only cleanup input was sent: a freshly returned unique task-owned PTJ window received supported `Alt_L+F4`, which returned success; PID **15676** exited and the log recorded Application stopping/shutting down/stopped. The same normal-close attempt for the task-owned blank Paint hit the user-input guard. No capture retry or process kill was used to work around it. The user was asked to close only that blank Paint window and replied **“Closed the blank Paint window.”** Final process inventory is recorded in `cleanup.json`; no user application was closed by the agent. Clipboard content was untouched.

**M22.12B remains blocked, with startup/close prerequisites partially verified only.** Zero native Notebook workflow cases, test parents or isolated test children completed; zero builds, tests and complete gates ran. No screenshots or transition recording could be captured. The requested first-click Trash, smooth switching/ownership, editing/autosave, real picker/clipboard/viewer, linked-target navigation, folder/tag/lifecycle workflows and representative-content restart matrix remains unexercised. PID 15676 is a fresh launch against the same isolated profile, but no representative content was prepared/observed before or after it; **no restart-persistence acceptance is claimed**. A15/A19/A20 historical focused evidence retains its original scope. Human/Narrator, installed-codec and matching hosted checks remain separately unverified; overall M22.12 acceptance remains pending.

The concrete next action is a **Computer Use tool-support investigation** using `events.json`, `supported-context.json`, `window-process-evidence.json` and the fresh PTJ identity. Support must resolve (1) why the minimized Paint window's requested activation remains blocked after its required refresh and (2) why the fresh PTJ `get_window_state` receives no frame on the observed session. A supported visible blank-window capture and then a valid isolated-PTJ capture are prerequisites to resuming B. The documented API offers no further backend recovery command, so another identical PTJ launch/capture loop or test gate would not resolve the specified uncertainty. No production fix is proposed from these observations.

Whitespace and local Markdown-target checks are recorded with `summary.json`; external URLs/anchors remain outside that check. No source/harness, schema/export, CI, timeout, publication or workflow-trigger change occurred. The isolated profile, synthetic PNG and all historical evidence are retained.

## M22.12B — Native workflow prerequisites and window-capture blocker

29 October 2026, one agent. Actual baseline: **clean develop / `656cfd75034d99a4643c0a950c0a4d4b87e585d0`**, worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. The intervening commit includes A20's six modified/two added files. No production, harness, schema, archive/export, CI or timeout change was made. Exact tracked task inventory is **README.md and docs/notebook.md only**; preparation scripts, a synthetic PNG, launch/context records and the disposable profile are ignored evidence under **`TestResults/m2212b-native/`**.

### Isolation and build provenance

Source inspection traced normal startup through `MaintenanceLaunch.Parse → DesktopApplicationPaths.FromArguments → LocalApplicationPaths`. The already-supported **`--isolated-data-root <absolute-root>`** option routes data without depending on a LOCALAPPDATA environment override. The current compiled `DesktopApplicationPaths.FromArguments` method was invoked read-only before launch; every returned property was checked to lie beneath this task's evidence root. `resolved-paths.json` records the actual constructor results:

- Profile root: `C:/Users/Yovko/source/repos/Personal-Trading-Journal/TestResults/m2212b-native/profile`.
- Database: `profile/PersonalTradingJournal/journal.db`; Notebook recovery checkpoints and ownership metadata reside in this SQLite database.
- Originals/images: `profile/PersonalTradingJournal/screenshots` (Notebook's existing `notebook-*` namespace).
- Settings, logs and backups: `profile/PersonalTradingJournal/{settings.json,logs,backups}`.
- Provider choice/credential paths: `profile/PersonalTradingJournal.Secrets/{provider.txt,openai.dpapi,groq.dpapi}`. No credentials were read or provider requests made.

The profile did not exist before launch. Only normal application startup created its database/directories; no database was copied, seeded or edited directly. `isolated-files-after-startup.json` records synthetic-directory metadata. A 640×240 procedural synthetic chart was prepared at `assets/synthetic-chart.png` for a future real picker operation; it has not been imported or copied to the clipboard. An initial reflection preparation call failed to bind the LocalApplicationPaths constructor; no profile/journal was accessed. Preparation was corrected to invoke the actual DesktopApplicationPaths factory, as recorded in `preparation.log`.

`binaries.json` retains the A20 binary identity. The current production DLL SHA-256 is **`991EB49BA5B2356ADCB72C24BC1AE464DD2C8C5399D3F4E5510E9B26B3EC5178`**, matching A20's recorded Release build. HEAD contains that source, so no rebuild or focused rerun was necessary. **Zero test parents and zero isolated test children ran**, with no test/gate exit code asserted.

### Desktop conditions, normal launch, capture blocker and cleanup

The user explicitly replied **“Ready — I’ll minimize them and leave the desktop idle”** after commands/assets/profile verification were prepared. The existing read-only A15.3 context observer ran through the supported executor: **session 1 / WinSta0 / Default**, interactive, existing administrator token, cursor access true/error 0, no competing dotnet/testhost/vstest processes. There was no UAC, desktop/token switching, always-on-top behavior, forced input state or closure of unrelated applications. `supported-context.json` records this observation; it is not a guarantee that later target coordinates stay unobstructed.

```powershell
pwsh -NoProfile -File TestResults/m2212b-native/context-check.ps1 -OutputName supported-context.json -Execution 'Existing supported input desktop; user confirmed idle desktop; read-only context observer'
pwsh -NoProfile -File TestResults/m2212b-native/launch.ps1 -Name first
# The prepared launcher starts this existing executable normally, with ProcessStartInfo.ArgumentList:
src/PersonalTradingJournal.Desktop/bin/Release/net10.0-windows/PersonalTradingJournal.Desktop.exe --isolated-data-root C:/Users/Yovko/source/repos/Personal-Trading-Journal/TestResults/m2212b-native/profile
```

`first-launch.json` records **PID 10888**, executable, source DLL hash, arguments and session 1. The normal startup log is under `profile/PersonalTradingJournal/logs/`; initialization completed and the app issued ordinary initial browse queries against its isolated database. Computer Use discovered the returned window **460268, Personal Trading Journal**, associated with the exact Release executable. The first `sky.activate_window` / `sky.get_window_state` sequence returned **“Computer Use app approval timed out”** before any native workflow interaction or screenshot could be confirmed. The call's exact completed sub-step is not separately reported, so activation is not claimed as verified. `computer-use-blocker.json` preserves that result.

The user then replied **“Approved — continue with the isolated journal.”** App approval therefore ceased to be the blocker. A fresh `list_windows` selection returned the same unique task-owned executable/window. Its activation/state request failed **“FrameArrived timed out: timed out waiting on channel”**. One bounded recovery re-listed the window, rehydrated the returned window through `get_window`, and requested activation/state again; it failed **“window capture timed out: timed out waiting on channel”**. No usable screenshot or accessibility tree was returned by either attempt. `computer-use-events.json` records the sequence; the original approval-timeout artifact is preserved rather than overwritten.

The supported Computer Use skill was read and used. Its [guidance](C:/Users/Yovko/.codex/plugins/cache/openai-bundled/computer-use/26.1023.50603/docs/guidance.md) says: **“If state capture or window activation fails, stop using prior coordinates or element indexes. Refresh the app/window selection and retry once; report the exact error if recovery fails.”** No direct PowerShell UI automation, alternate input helper, blind workflow input, permission bypass or speculative production correction was substituted. There is no evidence of a Notebook rendering/ownership defect from this blocked attempt. The smallest next action is to restore supported Computer Use capture of the isolated journal and obtain one usable current-window observation before resuming the native checklist. No additional test/gate run is justified by this tool failure.

Cleanup alone used a freshly returned, unique task-owned journal window and the supported `sky.press_key(Alt_L+F4)` normal-close shortcut. It returned success; subsequent read-only process inventory confirmed **PID 10888 exited** (`cleanup.json`), and the isolated log records normal application shutdown. No unrelated application received input or was closed. No process was killed. This verifies normal startup/close and process exit, **not** the requested restart of representative persisted Notebook content. No second journal PID was launched because no native content/workflows could first be prepared or observed.

### Workflow status matrix

| Requested workflow | Status in B | Evidence / limitation |
| --- | --- | --- |
| Current source/binary identity and disposable profile resolution | Passed | `baseline.json`, `binaries.json`, `resolved-paths.json`; no real journal access |
| Supported input-desktop prerequisites and normal executable startup | Passed at observation/startup | `supported-context.json`, `first-launch.json`, isolated startup log; not a native action pass |
| A19 one-click Trash from All Notes/Pinned, matching scope/heading/rows | Blocked / not exercised | Capture timed out after approval and one recovery; historical A19 controlled native evidence remains separate |
| A20 ordinary/rapid note switching, consistent target ownership, edits and Refresh | Blocked / not exercised | No production slowdown or native transition capture; A20 deterministic offscreen evidence remains separate |
| Folder/tag Add/Cancel/Enter/Escape, alignment, rename/delete, filtering/assignment/global deletion | Blocked / not exercised | No synthetic notes/catalog entries created through controls |
| Blank/template notes, formatting/checklist/link, undo/redo, pin/menu behavior | Blocked / not exercised | No native input performed |
| Saved locally, approximately ten-second notification, Light/Dark/date readability | Blocked / not exercised | No current native screenshot or human visual assessment |
| Real image picker/clipboard, viewer/caption/order/removal; installed WebP codec | Blocked / not exercised | Synthetic PNG prepared only; clipboard untouched, no restoration needed; WebP support not inferred |
| Synthetic Trade/Setup links, navigate/return, duplication, Trash/Restore/permanent deletion | Blocked / not exercised | No synthetic targets or lifecycle changes performed |
| Normal close and confirmed process exit | Passed for cleanup | Supported Alt+F4, PID 10888 exit and normal shutdown log; no unsaved synthetic edits existed |
| New-PID relaunch and persisted representative content | Blocked / not exercised | No second PID/content could be prepared and observed; no process restart claimed |
| Human keyboard/Narrator, real journal workflow, hosted compatibility | Unverified | Automated prerequisite observations cannot establish these |

No user/test process was killed. All processes started by this task are closed; the isolated profile and evidence are retained. No workaround or forced termination was used. No complete Release gate, build, focused test, production/provider access, Git publication or workflow trigger ran. **M22.12B is blocked at native window capture; overall acceptance remains pending the final Release gate and unresolved native checks.** Historical A15 focused acceptance and A20 focused automated verification remain passed within their recorded scope.

Whitespace and local documentation-target existence checks are recorded beside `summary.json`; external URLs/anchors are outside the local-target check's scope. Historical evidence is preserved.

## M22.12A20 — Stable workspace during note switching

29 October 2026, one agent. Actual baseline: **clean develop / `8a6624c0829a0ecc06669887da92d7335d13767d`**, existing worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. The intervening commit incorporates A19's three modified files and added test file; those were not pending changes. HEAD remains unchanged. Repository/ancestor inspection found no AGENTS.md. All prior evidence is preserved. Evidence root: **`TestResults/m2212a20-verified/`**; `baseline.json`, `provenance.json` and `binaries.json` distinguish HEAD from the uncommitted implementation tested.

### Demonstrated cause and correction

The actual path is `NoteList.SelectedItem → SelectedNote → GuardedAsync → AcceptSelection → ReadAsync → ReadDetailAsync`. A clean selection already makes **no list/catalog service query**. However, `ReadAsync` marked the entire workspace busy, causing Loading notes above the list, disabling list selection and allocating A19's footer row. `ReadDetailAsync` cleared Note, links and draft **before awaiting the target**, replacing the editor with an empty document; after the await it installed the target a second time. Note publication also cleared/repopulated the global Tags/filter collections. The view rebuilds its RichTextBox document on EditorRevision, so the empty draft caused an avoidable visible blank frame and undo reset. A19's top-row movement was fixed already; it did not explain this additional detail-clearing behavior. Ordinary selection does not recreate the view or explicitly request focus; the problem was the changes published into the existing view.

The controlled baseline held B's service response with a TaskCompletionSource after displaying A with an image, assigned tag and Trade link. Its new real-binding/offscreen regression **failed**: `retained=false`, `oldOwner=false`, one pending editor replacement and two total replacements; list disabled, Loading notes visible and footer visible. The list-query count stayed **1→1**, proving this was presentation work rather than an unavoidable list reload. Raw failed parent/child TRX, supervision logs, metrics and all three PNGs remain under `before/`.

Ordinary detail reads now use a local loading classification while retaining internal busy/mutation guards. The displayed A title, body/image, assigned tags, metadata and links remain owned by A and read-only; status says Loading note rather than Saved locally. The requested B row can be selected while this explanation is visible. No A edit or management command can be applied as B. When the accepted target arrives, existing activation/cancellation/selection checks permit **one** draft/document installation, including the normal fresh undo history. The note list remains enabled for superseding selections. Global Tags and selected filter options reconcile by identity/value, preserving unchanged rows instead of Reset. No cache, animation, sleep or new global input lock was added.

Cancellation/deactivation restores the displayed note's row before editing can resume. Superseding workspace reads restore that consistent starting ownership too. Missing, invalid, unsupported, oversized or failed targets clear the previous detail into the existing explicit unavailable/Refresh state; they never enable the retained A document as B. Existing guarded saves, recovery, expected versions, image ownership and operation ownership are unchanged. A successful save intentionally queries stored search membership/counts and republishes that result; this required write-side behavior is **not** claimed to be removed or scroll-stable by A20. Explicit Refresh/scope reads retain their full reload and contextual footer Cancel behavior. A detail load has local status and can be superseded by another selection; it does not allocate the workspace footer. Content-local loading explanation may wrap and move the document within the detail pane; the surrounding workspace/list geometry remains stable.

### Commands and distinct focused results

```powershell
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore
# Controlled reproduction before the production correction:
pwsh -NoProfile -File TestResults/m2212a20-verified/run.ps1 -Name before -Filter 'FullyQualifiedName~DelayedNoteSwitchRetainsReadOnlyDetailAndWorkspaceGeometry&DisplayName~Light'
# Corrected transition checks, then relevant logic (not a historical suite union):
pwsh -NoProfile -File TestResults/m2212a20-verified/run.ps1 -Name corrected -Filter 'FullyQualifiedName~DelayedNoteSwitchRetainsReadOnlyDetailAndWorkspaceGeometry'
pwsh -NoProfile -File TestResults/m2212a20-verified/run.ps1 -Name logic -Filter 'FullyQualifiedName~NotebookDetailSwitchTests|FullyQualifiedName~NotebookWorkspaceTests|FullyQualifiedName~NotebookEditingTests|FullyQualifiedName~NotebookAutosaveTests|FullyQualifiedName~NotebookNoteActionsTests|FullyQualifiedName~NotebookTagTests|FullyQualifiedName~NotebookTagFormTests|FullyQualifiedName~NotebookLifecycleTests|FullyQualifiedName~NotebookLinkTests|FullyQualifiedName~NotebookNotificationTests|FullyQualifiedName~ReleaseSchedulingTests'
# Strengthened unavailable-target assertions plus existing image coordination:
pwsh -NoProfile -File TestResults/m2212a20-verified/run.ps1 -Name ownership-images -Filter 'FullyQualifiedName~NotebookDetailSwitchTests|FullyQualifiedName~NotebookImageCoordinationTests'
pwsh -NoProfile -File TestResults/m2212a20-verified/run.ps1 -Name header-regressions -Filter 'Workload=ExclusiveWpf&(FullyQualifiedName~ScopeItemSelectionKeepsRequestQueryHeadingAndRowsConsistent|FullyQualifiedName~SaveStatusBesidePinReflectsCanonicalStateOffscreen)'
pwsh -NoProfile -File TestResults/m2212a20-verified/run.ps1 -Name image-editor -Filter 'FullyQualifiedName~NotebookImageEditorTests'
# Direct dirty-selection coverage gap, then corrected test contracts (production unchanged):
pwsh -NoProfile -File TestResults/m2212a20-verified/run.ps1 -Name dirty-switch-guards -Filter 'FullyQualifiedName~DirtySelectionOnlyInstallsTargetAfterSuccessfulLeaveGuard|FullyQualifiedName~NewerEditDuringGuardSaveIsPersistedBeforeTargetIsInstalled'
pwsh -NoProfile -File TestResults/m2212a20-verified/run.ps1 -Name dirty-switch-contracts -Filter 'FullyQualifiedName~DirtySelectionOnlyInstallsTargetAfterSuccessfulLeaveGuard|FullyQualifiedName~NewerEditDuringGuardSaveVetoesSwitchAndRetainsDraft'
pwsh -NoProfile -File TestResults/m2212a20-verified/audit.ps1
pwsh -NoProfile -File TestResults/m2212a20-verified/check-whitespace.ps1
pwsh -NoProfile -File TestResults/m2212a20-verified/check-doc-links.ps1
```

The helper uses `dotnet test ... -c Release --no-build --no-restore --filter ... --logger trx --results-directory ...`, with separate durable output/result files for each invocation. The initial fixture build failed **CS0103** because its visual-tree helper had not been defined (`build/before.log`, 49.31s, one error/zero warnings); that authoring error was corrected before runtime. Subsequent builds passed with zero warnings/errors: `before-corrected.log` **10.85s**, `corrected.log` **16.14s**, `final.log` **7.21s**, `final-assertions.log` **6.82s**, `guard-cases.log` **25.26s**, `guard-cases-final.log` **17.67s**, and final `guard-contracts.log` **10.01s**. Builds followed source/assertion changes; there was no restore or complete gate. Final changes after the transition runs were confined to the independent logic-test file, so passing transition/image/header bodies and production source were not rerun without a concrete need.

| Run | Parents passed/total | Separate children passed/total | Exit | Command wall time |
| --- | --- | --- | --- | --- |
| `before` | 0/1 | 0/1 | 1 | 10.879s |
| `corrected` | 2/2 | 2/2 | 0 | 13.915s |
| `logic` | 140/140 | None | 0 | 4.236s |
| `ownership-images` | 15/15 | None | 0 | 3.184s |
| `header-regressions` | 6/6 | 6/6 | 0 | 33.263s |
| `image-editor` | 4/4 | 4/4 | 0 | 43.703s |
| `dirty-switch-guards` | 3/5 | None | 1 | See retained result.json |
| `dirty-switch-contracts` | 5/5 | None | 0 | 4.833s |

The added dirty cases initially made two incorrect assumptions. Save success was expected to issue no list query, but SaveCore intentionally refreshes search membership/counts. The newer-edit case expected a second implicit save and B installation; the actual manual-guard contract is to veto the switch after the older commit, retain A's newer dirty draft, and let subsequent save/consent proceed. The revised exact assertions verify those contracts: persisted First/version+1, retained Newer/body under A, one write and no B load. No production guard/autosave change or weakened deadline was made. Both failed raw results remain in `dirty-switch-guards/`; the latter test was renamed to describe the correct contract. The literal-path-safe audit records that explicit superseded identity mapping without removing its original name/result.

Final current coverage: **163 distinct passing parents and twelve distinct matching child cases**, zero final failures/skips/incomplete or duplicate results. Across development there are **178 parent executions / thirteen child executions** and **164 raw historical parent names**, including the one superseded test name. Nine `ownership-images` cases overlap `logic`; the baseline Light case and the five corrected guard identities repeat earlier cases and are not additional coverage. Twelve isolated children are two transition rows, two A19 scope rows, four save-status rows and four image-editor rows, each with exactly one matching child. All original failures remain visible in `test-audit.json`; `results.json` distinguishes executions from final current coverage. Existing exclusive collection/traits, collection limits, exact child validation, 90-second process deadline, 30-second case watchdog and cleanup remain intact. No competing test process was found before launching selections or after completion. A later Win32_Process metadata lookup was denied; the supported Get-Process fallback and retained supervised exit results were used. No unrelated process was stopped.

| Requirement / risk | Current evidence and result |
| --- | --- |
| Stable clean selection through actual binding, list scroll and navigation layout | Two delayed WPF rows: document retained, zero pending replacements, one final replacement; zero Notes/Tags Resets, unchanged rows/scopes/list bounds; offset **8→8**, list queries **1→1** |
| Displayed ownership during loading; fresh target undo history | A title/body/image/tag/Trade retained read-only; B has its own document, no assigned tags and Trading Setup link; fresh editor cannot Undo into A |
| Rapid A→B→C and A→B→A; cancellation/deactivation | Four new controlled async logic cases, service deliberately ignores cancellation; late B never publishes; cancelled pending selection restores A |
| Missing/unreadable/failed target and explicit Refresh | Five new exact failure-state cases; empty body/correct unavailable title, old draft not editable, Refresh queries and recovers B |
| Dirty save/discard/cancel/failure and newer typing | Five direct selection cases plus existing editing/autosave/recovery tests; failed/cancelled guard retains A, newer typing vetoes departure without loss |
| Images, tags, links, action ownership and A15 persistent status | Existing image coordination/editor, tag/form, link, note-action, notification and save-status regressions all pass; assignment/global-delete recovery protections retained |
| A19 first-click scope behavior | Existing two actual selection-binding/held-read geometry rows pass; previous native A19 evidence retained, native mouse path not re-executed in A20 |

### Transition evidence and limitations

Nine actual PNGs were opened and inspected: three original Light frames and six corrected frames. `renders.json` lists original paths, hashes and identical convenience copies under `renders/`.

- Baseline: `before/notebook-switch-20261028-234711-bb4482fcb76949e5bb2e41bcb54d7729/Light-1280-96-{01-before,02-pending,03-complete}.png`.
- Corrected Light: `corrected/notebook-switch-20261028-234838-a70873f59e164cbdb2e6ee18cb9d2969/Light-1280-96-{01-before,02-pending,03-complete}.png`.
- Corrected Dark/narrow/high-DPI: `corrected/notebook-switch-20261028-234833-5ac7dcc73841437694aad721c3539617/Dark-480-240-{01-before,02-pending,03-complete}.png`.

These are ordered before/pending/completed captures around a held dependency, not only settled screenshots or fabricated composites. Pending Light keeps Organization and scrolled list intact; pending Dark keeps the old image/title/tag with readable wrapped loading text. Completed frames show B's own body/link and truthful saved status. Narrow layout intentionally uses the Content panel during the sequence; the fixture separately verifies Notes-panel geometry and unchanged scroll position. No blocking clipping, contrast or workspace-target displacement was observed. The detail-local explanation can wrap while the target loads, and different target content naturally changes the detail height. This is **offscreen WPF binding/layout/command evidence** (no Window/HwndSource, pointer/focus test, or forced focus), not a native switching recording, compositor/frame-rate guarantee or human/Narrator assessment. Linked records use synthetic unavailable targets, with actual opening/navigation coordination covered separately by existing logic tests.

Exact task inventory: **six modified files and two added files**, no pre-existing pending changes:

- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.cs` — local detail loading, retain/install ownership, cancellation restoration and list selection capability.
- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Editing.cs` — loading/read-only capability and persistent status derived from the same state.
- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Tags.cs` — identity/value reconciliation for global catalog/filter options.
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml` — reserve global busy footer for workspace reads.
- Added `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookDetailSwitchTests.cs` — fourteen controlled ownership/guard/failure cases.
- Added `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookHeaderViewTests.Switching.cs` — two exact isolated transition cases and artifact capture.
- `README.md` and `docs/notebook.md` — cause, evidence, exact contracts/counts and handoff.

Whitespace and local Markdown target-existence checks passed after documentation edits; anchors/external URLs are outside that check's scope. A20 focused automated verification is complete. A15 focused acceptance remains passed; A18 layout and A19 stable navigation behavior are preserved. **M22.12B and overall acceptance remain pending.** Native journal workflow/restart, human/Narrator, real clipboard/picker/viewer/codec and matching hosted timing/input compatibility remain unverified. No persistence, schema, document/archive/export, CI or timeout change, publication, workflow trigger or production/provider access occurred.

## M22.12A19 — Stable scope targets during asynchronous reads

28 October 2026, one agent. Actual baseline: **clean develop / `32bc081c7b49ccb2e4e8f47fc79585debee1686b`**, worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. The intervening commit incorporates A18's four files. Repository/ancestor inspection found no AGENTS.md. Existing work and historical evidence are preserved; HEAD remains unchanged. Evidence root: **`TestResults/m2212a19-verified/`**.

### Trace, demonstrated defect and remaining native uncertainty

Workspace row data contains explicit `NotebookScopeOption` identities in **All Notes, Pinned, Trash** order; there is no positional enum conversion. WPF item selection raises `SelectPrimaryScope` with the added scope object. That handler sets `model.Scope` and awaits `LoadTask`, then synchronizes the accepted highlighting. The setter starts `SelectScopeAsync` through the existing leave guard; accepted scope identity is installed before `ReloadAsync`. `ReadPageAsync` constructs a query from that identity, search, tags and sort. Cancellation tokens/activation checks reject superseded page results. On completion, catalog publication restores the matching scope object, heading and rows. While a new query is pending, the existing applied heading/rows remain visible with Loading notes and selection disabled; this truthful transitional behavior is unchanged.

Two initial checks against **unchanged production code** passed: direct WPF automation selection of Trash, then expanded transitions/guard/out-of-order cases. These do not reproduce a native click. The more specific held-read layout regression **failed before the production correction**: after requesting Trash, the original Trash center **`(105,229.17)` DIP** lay inside **Pinned's arranged bounds**. The trace says `accepted=Trash, selected=Trash, heading=All Notes` while the request is pending, but the same press point resolves to Pinned. `geometry-before/` retains **one failed parent and its one failed child**, exit **1**, Expected Trash / Actual Pinned. This is a demonstrated moving-target defect, independent of service timing: the synthetic query is held by a TaskCompletionSource without sleeps.

The cause of that movement is the existing **42-DIP busy/cancel row inserted above the panels when IsBusy becomes true**. It moves the entire scope list under a pointer whose press initiated loading. WPF's [ListBox implementation](https://github.com/dotnet/wpf/blob/v10.0.12/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/ListBox.cs) and [ListBoxItem implementation](https://github.com/dotnet/wpf/blob/v10.0.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/ListBoxItem.cs) support the input-specific explanation: press selection, list capture and dragged-item navigation can retarget a held press when another row enters the pointer. This last connection to the user's complete physical click sequence is **an inference**, not an observed native reproduction in A19. The default test runtime requests WindowsDesktop 10.0.0 with installed 10.0.11/10.0.12 servicing runtimes; the linked source establishes the relevant implementation, not a traced native event sequence.

### Small correction and coverage

The only production change moves **ActiveNotebookOperation**, including its existing loading text, accessible Cancel button, command, busy binding and contextual visibility, to an auto-sized footer below the panels. It no longer inserts space above scope rows. Notification placement/timers, A18 folder/tag creation layout, scope identities/counts/filters, guard logic, list generations, query construction and lifecycle safeguards are unchanged. No artificial offset, delay, extra query, second selection or captured-input override is added. This shared workspace layout also appears during other reads; A19 does **not** change note-selection/detail-loading semantics or claim to complete A20's smooth-switching work.

The new partial `NotebookHeaderViewTests.Scopes.cs` uses four distinguishable synthetic notes: active unpinned, active pinned, trashed, and a real-folder note. Through actual generated ListBox item automation providers and the bound SelectionChanged path, it checks All Notes→Trash once, Pinned→Trash, Folder→Trash, Trash→All Notes/Pinned, saved dirty navigation, cancelled/failed-save guards, delayed Trash completing after Pinned, and empty Trash. It verifies accepted object/highlight, bound `FolderHeading` text, completed query identity, exact row IDs and empty counts. It also verifies that the original Trash press point stays within Trash while the query is held and the real Cancel command remains available. Each narrow transition opens Organization first, as the actual UI requires. No direct Scope-property assignment is used to drive these view tests.

The same file includes **NativeTrashPressRetainsTargetWhileReadIsPending**, a separately selectable native check. After the user replied Ready and minimized chat/browser windows, the supported executor confirmed **session 1 / WinSta0 / Default**, cursor access and no competing test processes (`native-context.json`, `native-corrected-context.json`). Five existing dotnet processes were identified as MSBuild node-reuse servers and left alone. The fixture uses the existing input primitives/readiness helpers, actual HWND ownership and mouse state, one down/up pair and a controlled pending read. Cleanup releases only its own outstanding press after assertions, releases the query and closes its own window. No unrelated window was automated, closed or made topmost.

The initial native run stopped **before injecting mouse-down**: expected HWND, actual hit and foreground all belonged to the test window, but an immediate `target.IsMouseOver` assertion was false. The new fixture had omitted the existing native menu helper's causal readiness loop after SetCursorPos. It was corrected to the same **three-second bound / Input-priority dispatcher loop**, retaining both native point ownership and actual mouse-over requirements. The corrected case ran once and passed. Its initial mouse-over was again false, then became true after **9.4ms** of normal dispatcher processing. The real held-press trace records **ListBox capture**, `hitScope=Trash`, `accepted=Trash`, and query sequence **AllNotes,Trash**. Original press point `(645,1011)` physical pixels remains inside Trash; the row center becomes `(645,1016)` after the ordinary focus border adjustment, rather than moving an entire row. Mouse-up, completed Trash heading/highlight and exact trashed note ID all passed. This verifies the controlled real native path after the layout correction; it does not create a recording of the user's original event sequence. Original failure and successful traces remain under `native/` and `native-corrected/`.

### Commands, retained failures and results

```powershell
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore
# Initial automation only, then expanded guards/transition diagnostics on unchanged production:
pwsh -NoProfile -File TestResults/m2212a19-verified/run.ps1 -Name before -Filter 'FullyQualifiedName~ScopeItemSelectionKeepsRequestQueryHeadingAndRowsConsistent&DisplayName~Light'
pwsh -NoProfile -File TestResults/m2212a19-verified/run.ps1 -Name expanded-before -Filter 'FullyQualifiedName~ScopeItemSelectionKeepsRequestQueryHeadingAndRowsConsistent&DisplayName~Light'
# Retained fixture diagnostic and corrected arranged-geometry reproduction before production change:
pwsh -NoProfile -File TestResults/m2212a19-verified/run.ps1 -Name hit-before -Filter 'FullyQualifiedName~ScopeItemSelectionKeepsRequestQueryHeadingAndRowsConsistent&DisplayName~Light'
pwsh -NoProfile -File TestResults/m2212a19-verified/run.ps1 -Name geometry-before -Filter 'FullyQualifiedName~ScopeItemSelectionKeepsRequestQueryHeadingAndRowsConsistent&DisplayName~Light'
# After the production correction, then narrow-fixture setup correction and added bound-heading/capture evidence:
pwsh -NoProfile -File TestResults/m2212a19-verified/run.ps1 -Name corrected -Filter 'FullyQualifiedName~ScopeItemSelectionKeepsRequestQueryHeadingAndRowsConsistent'
pwsh -NoProfile -File TestResults/m2212a19-verified/run.ps1 -Name final-scopes -Filter 'FullyQualifiedName~ScopeItemSelectionKeepsRequestQueryHeadingAndRowsConsistent'
pwsh -NoProfile -File TestResults/m2212a19-verified/run.ps1 -Name final-evidence -Filter 'FullyQualifiedName~ScopeItemSelectionKeepsRequestQueryHeadingAndRowsConsistent'
pwsh -NoProfile -File TestResults/m2212a19-verified/run.ps1 -Name logic -Filter 'FullyQualifiedName~NotebookWorkspaceTests|FullyQualifiedName~NotebookLegacyNavigationTests|FullyQualifiedName~NotebookFolderActionTests|FullyQualifiedName~NotebookNotificationTests|FullyQualifiedName~ReleaseSchedulingTests'
# Supported executor, user-confirmed idle desktop; first fixture and causal-readiness correction:
$env:PTJ_INPUT_ORIGIN_TRACE = '1'
pwsh -NoProfile -File TestResults/m2212a19-verified/run.ps1 -Name native -Filter 'Workload=ExclusiveWpf&FullyQualifiedName~NativeTrashPressRetainsTargetWhileReadIsPending'
pwsh -NoProfile -File TestResults/m2212a19-verified/run.ps1 -Name native-corrected -Filter 'Workload=ExclusiveWpf&FullyQualifiedName~NativeTrashPressRetainsTargetWhileReadIsPending'
pwsh -NoProfile -File TestResults/m2212a19-verified/audit.ps1
pwsh -NoProfile -File TestResults/m2212a19-verified/check-whitespace.ps1
pwsh -NoProfile -File TestResults/m2212a19-verified/check-doc-links.ps1
```

Every listed runtime selection ran once for its recorded source revision. Builds occurred before each changed fixture/correction; no runtime retry was performed without a changed test/correction. `build/` retains all original logs. Initial `regression.log` failed with one CS7036 constructor error; `hit-geometry.log` later failed with one xUnit2031 analyzer error. Both were fixture-authoring errors corrected before runtime, with zero warnings. Successful builds: `regression-corrected.log` **8.61s**, `expanded.log` **8.24s**, `hit-regression.log` **7.83s**, `hit-geometry-corrected.log` **7.89s**, `final.log` **16.75s**, `narrow-fixture.log` **7.85s**, `final-evidence.log` **7.77s**, and final native-fixture-only `native-readiness.log` **6.82s**, all zero warnings/errors, exit **0**. Offscreen/logic bodies and production code were unchanged by the last readiness correction, so their passing runs were not repeated.

| Run | Parents passed/total | Separate children passed/total | Exit | Interpretation |
| --- | --- | --- | --- | --- |
| `before` / `expanded-before` | 1/1 each | 1/1 each | 0 each | Same Light identity; automation did not reproduce physical-click symptom |
| `hit-before` | 0/1 | 0/1 | 1 | Fixture error: unattached root InputHitTest returned null; corrected to arranged rectangles, without changing native input checks |
| `geometry-before` | 0/1 | 0/1 | 1 | Relevant production layout regression: Expected Trash, Actual Pinned at original press point |
| `corrected` | 1/2 | 1/2 | 1 | Light passed correction; Dark fixture had not opened narrow Organization, so no row container existed |
| `final-scopes` | 2/2 | 2/2 | 0 | Corrected narrow setup; unchanged production correction |
| `final-evidence` | **2/2** | **2/2** | **0** | Final bound-heading/capture assertions, **15.268s** command wall time |
| `logic` | **51/51** | None | **0** | Existing workspace, legacy navigation, folder deletion, notification and scheduling cases, **3.265s** |
| `native` | 0/1 | 0/1 | 1 | New fixture sampled mouse-over before dispatch; correct native owner, no mouse injection |
| `native-corrected` | **1/1** | **1/1** | **0** | Real held press/release with existing bounded readiness, **7.409s** |

Final distinct passing coverage is **54 parents and three child cases**, counted separately, with zero final failures/skips. There are **63 parent executions and twelve child executions** across development evidence; repeated identities are not counted as extra coverage. Four failed parents/children remain retained, including the real geometry failure and three fixture errors/readiness omissions. Final children match the exact Light/1280/96 and Dark/480/240 parents plus the native Fact, once each. Literal-path-safe TRX audit checks completeness, no skipped/duplicate identities per invocation and exact child cardinality. The existing exclusive collection, two-thread limit, 90-second child deadline and 30-second watchdog remain unchanged. The 51 logic cases overlap historical evidence and do not increase historical totals. Original SQL/archive/lifecycle coverage remains valid for its unchanged source; no migration or persistence changes require a new storage run. All supervised child processes completed cleanup; no unrelated process was terminated.

### Visuals, inventory and acceptance limits

Four current-source PNGs were opened and inspected, with paths/hashes in `renders.json`: **Light-1280-96-scope-busy.png**, **Light-1280-96-empty-trash.png**, **Dark-480-240-scope-busy.png**, **Dark-480-240-empty-trash.png**. Originals remain under `final-evidence/notebook-scopes-*/`; copies are under `renders/`. Busy views retain the original scope row positions and readable contextual footer cancellation; completed empty views show Trash with zero rows/count. Narrow busy Organization and completed Notes panels are separate viewport states. No blocking footer clipping or contrast issue was found. The busy view intentionally retains the previous applied list heading/rows while Loading notes until completion.

Exact inventory: **three modified files and one added file**, no pre-existing pending changes:

- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml` — relocate existing busy feedback/Cancel below panels.
- Added `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookHeaderViewTests.Scopes.cs` — deterministic automation/bounds regression and separately selectable native verification case.
- `README.md` and `docs/notebook.md` — confirmed layout finding, evidence boundaries, results and handoff.

**A19's demonstrated layout defect is corrected; focused automation and the controlled native first-click check pass.** The real native test is distinct from offscreen geometry evidence and verifies a held press after the correction, rather than reconstructing the user's original click. A15 focused acceptance remains passed; A18 is preserved. **A20, postponed M22.12B and overall M22.12 acceptance remain pending.** Human/Narrator, native journal workflows/restart and matching hosted compatibility remain unverified. No complete gate, production/provider access, publication, workflow triggering, schema/export/archive, CI or timeout change occurred.

## M22.12A18 — Consistent folder and tag creation layout

28 October 2026, one agent. Actual baseline: **clean develop / `5219ba238e771ddc07816e1e1d33d96ae140648f`**, worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. The intervening commit contains A15.3's two documentation files. Repository/ancestor inspection found no AGENTS.md. Existing changes and artifacts are preserved; HEAD remains unchanged. Evidence root: **`TestResults/m2212a18-verified/`**.

### Layout and preserved behavior

The supplied screenshot `C:/Users/Yovko/AppData/Local/Temp/codex-clipboard-2d2a7c64-8a3d-43d4-85f9-8dcfcaa64e9d.png` was opened. It shows folder Cancel on the left and New tag beside TAGS. The explicit A18 requirements take precedence over that current-state placement: **New tag moves below the catalog and More tag names**, left-aligned with New folder. Its field remains the next element below the button. Empty catalogs retain a directly accessible New tag below TAGS. Existing button typography, compact sizing, teal theme resources, tooltips and accessible names are retained; no incidental image styling was copied.

Folder creation replaces its left-flow WrapPanel with the existing tag creation row's Grid arrangement: flexible leading space, a reserved 54-DIP Add column including its six-DIP gap, and an auto-sized Cancel column at the input's right edge. Both actions have a 32-DIP minimum height and centered alignment. Tag creation already had this arrangement and retains it. Add remains **Collapsed** for empty/whitespace-only names; its reserved space and the always-visible Cancel keep both the form height and Cancel origin stationary. At the supported normal and narrow layouts both actions fit on one row. Catalog scrolling keeps the relocated button and management controls reachable for long lists.

The only production change is XAML layout. Existing click/key handlers, commands, name bindings, validation/pending/read-only bindings and focus ownership code are unchanged. Add remains before Cancel in traversal; global catalog/filter/paging controls remain before New tag. Creating a catalog tag still neither assigns it to the selected note nor selects its filter. Assigned-tag chips are unchanged. Existing validation/error retention, duplicate prevention, navigation consent, guarded selection and late-completion semantics continue through the same paths.

### Commands and focused results

```powershell
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore
pwsh -NoProfile -File TestResults/m2212a18-verified/run.ps1 -Name offscreen -Filter 'FullyQualifiedName~BoundTagsDistinguishFiltersAssignmentsAndGlobalDeletionWithRetainedEditor&DisplayName~Light&DisplayName~1280'
pwsh -NoProfile -File TestResults/m2212a18-verified/run.ps1 -Name logic -Filter 'FullyQualifiedName~NotebookTagFormTests|FullyQualifiedName~NotebookFolderCreationTests'
pwsh -NoProfile -File TestResults/m2212a18-verified/audit.ps1
pwsh -NoProfile -File TestResults/m2212a18-verified/check-whitespace.ps1
pwsh -NoProfile -File TestResults/m2212a18-verified/check-doc-links.ps1
```

The initial build (`build/output.log`, **74.72s**) failed with **one xUnit2013 analyzer error**, zero warnings: a newly added collection-count assertion needed `Assert.Empty` rather than `Assert.Equal(0, ...Count)`. The equivalent assertion was corrected; no test ran against that failed build. The final build (`build/final-output.log`, **20.49s**) passed **exit 0, zero warnings/errors**. Both logs remain retained. `binaries.json` identifies the final Release DLLs and `provenance.json` records HEAD plus the actual uncommitted files.

| Selection | Parents passed/total | Separate children passed/total | Failures/skips | Exit | Command wall time |
| --- | --- | --- | --- | --- | --- |
| Existing offscreen tag-view matrix, extended with both forms and catalog scrolling | 1/1 | 4/4 | 0/0 | 0 | 36.393s |
| Existing tag-form and folder-creation logic | 31/31 | None | 0/0 | 0 | 5.027s |

**32 distinct parents and four distinct isolated children** passed, with no overlap between these selections and no runtime retries. The unchanged tag-view dispatcher launches all four Light/Dark × 1280/96 and 480/240 child rows from the one selected Light/1280 parent. The other three visible parents were not invoked, avoiding redundant whole-suite launches. The literal-path-safe audit validates the four intended complete, unique child identities, completeness and zero skipped/duplicate results. These identities overlap historical coverage and are not added to historical totals. Existing 90-second process / 30-second case deadlines and scheduling remain intact.

The 31 logic cases comprise **17 folder-creation and 14 tag-form cases**: validation/persistence failure, retained draft/error, explicit cancellation, no automatic assignment/filtering, duplicate/in-flight prevention, late completion and guarded selection, plus captured global rename/delete ownership. Existing view assertions for assignment removal, filters and editor preservation remain. The obsolete heading/button vertical-centering assertion is replaced by the requested below-catalog/left-alignment geometry contract. New geometry checks verify both forms' field/Cancel right edges, Add-before-Cancel order, stable Cancel origin/form height, empty/whitespace visibility, empty catalogs and reaching New tag plus the last management button after scrolling a synthetic 26-tag catalog. This is layout/command evidence; no Window, native pointer, keyboard or focus helper was used. The restricted executor ran the checks without requesting input-desktop access. Native interaction was **not exercised**, and A15.3's passing native record remains scoped to its tested build and arrangement.

### Inspected captures

**Thirteen current-source PNGs were opened and inspected** from 88 generated by the existing matrix. `renders.json` maps copied paths, original paths and SHA-256 hashes. All copied paths below are beneath **`TestResults/m2212a18-verified/renders/`**; originals are beneath `offscreen/notebook-tags-20261028-204456-67d49ffcd1db42938cc0fe33164e3348/notebook-tag-renders/`.

| Profile | Inspected filenames (profile prefix plus suffix and `.png`) |
| --- | --- |
| `Light-1280-96-` | `folder-creation-empty`, `folder-creation-populated`, `creation-empty`, `creation-populated` |
| `Dark-480-240-` | `folder-creation-empty`, `folder-creation-populated`, `creation-empty`, `creation-populated` |
| `Dark-1280-96-` | `empty-catalog`, `long-catalog-bottom` |
| `Light-480-240-` | `empty-catalog-creation-populated`, `filters`, `long-catalog-bottom` |

Both forms have readable, stable, right-aligned action rows. New tag is below the labels and left-aligned; empty catalogs keep it visible. Long names wrap beside reachable management buttons without overlap. Long-catalog bottom captures deliberately scroll Organization: preceding rows are naturally clipped at the viewport edge, while the final management action and New tag remain visible. No blocking A18 layout defect was found in the inspected images. No composite images, state forcing or native-interaction acceptance is claimed.

### Exact inventory and deferred work

Four modified files, **no added tracked files**, no pre-existing pending changes:

- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml` — relocate New tag and align folder creation with the existing tag action row.
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookTagViewTests.cs` — update the changed layout contract and extend existing offscreen coverage/captures for forms and catalog reachability.
- `README.md` and `docs/notebook.md` — behavior, verification, inventory and handoff.

Whitespace and local documentation-target checks are recorded in the evidence root. **A18 implementation and focused verification are complete. A15 focused regression acceptance remains passed.** Native/human/Narrator, journal workflow/restart and hosted compatibility are not newly established here. **A19 — Trash selection** and **A20 — note-switch refresh** are explicitly pending user-requested corrections; their causes have not been investigated or established in A18. No correction to either is claimed. **M22.12B is postponed and overall M22.12 acceptance remains pending.** No complete gate, persistence/schema/archive/export, CI/timeout, production/provider, publication or workflow-triggering change occurred.

## M22.12A15.3 — Sustainable desktop prerequisite

28 October 2026, one agent. Initial state: **clean develop / `4dd5d0af158edb5d18343a17a983c39be28a359e`**, worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. The intervening commit contains A17.1's three files. Repository/ancestor inspection found no AGENTS.md. All changes and historical artifacts are preserved. Evidence root: **`TestResults/m2212a15-3-verified/`**.

**Final status: A15 focused regression acceptance passed after the user confirmed an idle desktop with chat/browser windows minimized.** The initial withheld-launch record is preserved as `pre-readiness-summary.json`. On continuation HEAD was unchanged; only README.md and this document had been modified by A15.3. No code or binaries changed between the withheld launch and the successful run.

### Provenance and execution observations

`baseline.json` verifies all three A17.1 source/document hashes before documentation edits and both Release DLL hashes against the retained manifests. Desktop DLL SHA-256 remains `B64AD391958937411F3FE216ECAEFA1014E79AF4D89B6809D9803811721EA730`; the test DLL is `733CF58D4B9BDCAB58A478C27A178259D4C764EB2BF8021F6F6EAA04B485A05A`. Both hashes were checked again immediately before launch. No build, restore or separate discovery run was needed. The tested code from A17.1 is now committed at the actual HEAD; documentation changes do not change those binaries.

The retained A15 failures are three distinct exact-dispatch rows of `OverflowTriggersToggleThroughNativeMouseAndOwnedKeyboard`: **Light/1280/96, Dark/1280/96, Light/480/240**. A15.1 identified Chrome over two later reproduction targets. A15.2 identified ChatGPT over three targets after an initially successful preflight. Neither identifies the unknown owner of the original A15 foreign foreground HWND. No new obstruction, production defect or fix is inferred today, and A17.1's resolved Refresh finding was not reopened.

Read-only context commands, adapted from the existing A15.2 context observer (not a test runner):

```powershell
pwsh -NoProfile -File TestResults/m2212a15-3-verified/context-check.ps1
# Same read-only observer through the supported executor, separately authorized:
pwsh -NoProfile -File TestResults/m2212a15-3-verified/context-check.ps1 -OutputName supported-context.json -Execution 'Supported executor context; read-only observation only; no test launch, UAC request or scripted desktop/token switching'
```

- Default restricted executor: session 1, WinSta0, `CodexSandboxDesktop-d322985e9b4b74cf3eaaca9dacb9f4eb`, non-administrator; `GetCursorPos` failed with **error 5**. Observer exited **1**; this is an execution-prerequisite result, not a failed test.
- Supported executor: session 1, **WinSta0/Default**, existing administrator token, cursor access **true/error 0**, observer **exit 0**. No UAC or scripted token/desktop switch was performed. CIM and process-name inventory showed **no dotnet/testhost/vstest process**. This proves access at observation time, not future target ownership.
- **Initial launch was withheld** while waiting for the user to prepare the desktop. No WPF preflight or test was launched at that stage. The user subsequently replied “Ready — I will minimize them and leave the desktop idle.” `launch.json` records that arrangement; `launch-context.json` again confirms session 1 / WinSta0 / Default, cursor access and no competing test processes. The agent did not automate or close unrelated windows, switch desktops, add topmost behavior or change native input prerequisites.

### Prepared selection and actionable execution arrangement

The supported executor launched the existing PowerShell runner after the user prepared the unlocked desktop, with ChatGPT/Codex and browsers minimized and no user app switching during the run. The same outer invocation launched the exclusive selections serially, then the non-exclusive selection; no browser/computer-use actions were taken between children. The fixtures retained their ordinary activation path and **every per-interaction readiness/WindowFromPoint check**. Four separate menu invocations allowed subsequent launches to stop on the first nonzero exit without modifying scheduling or the harness. All passed, so all remaining selections ran once.

`selection.json` records the smallest retained family selection, excluding the four subsequently added A16 save-status rows:

| Selection | Expected parents | Expected isolated children | Coverage |
| --- | --- | --- | --- |
| Exclusive menus | 4 | 4 | All three original failures plus Dark/480/240; complete native toggle/dismissal/switching/owned-action matrix |
| Exclusive split header | 2 | 2 | Complete template/Refresh/contextual cancellation family |
| Exclusive notifications | 4 | 4 | Complete Light/Dark normal/high-DPI notification presentation family on current XAML |
| Non-exclusive logic | 20 | 0 | 12 notification timing/state, 7 note-action ownership, 1 scheduling regression |

The source-derived expected counts above were confirmed against actual TRX. The selections do not overlap. Every UI parent retains exact child selection and single-result identity/cardinality validation, case-owned Application/dispatcher lifetime, 90-second process deadline and 30-second watchdog. Exclusive membership, collection limits and helpers are unchanged. Since A15.2, changes affecting this UI are A16's header placement and A17's tag alignment; current notification/menu renders are therefore included. Existing A16 save-state/notification logic and A17.1 tag/Refresh evidence remain valid for their recorded scope; the unchanged isolation helper needs no speculative rerun. Historical results are not added to new totals.

Executed commands (existing runner, fresh destination names, exclusive first):

```powershell
$env:PTJ_NOTEBOOK_MENU_DIAGNOSTICS = '1'
$env:PTJ_INPUT_ORIGIN_TRACE = '1'
foreach ($case in @(@('Light','1280'), @('Dark','1280'), @('Light','480'), @('Dark','480'))) {
    $name = 'menus-' + $case[0] + '-' + $case[1]
    $filter = 'Workload=ExclusiveWpf&FullyQualifiedName~OverflowTriggersToggleThroughNativeMouseAndOwnedKeyboard&DisplayName~' + $case[0] + '&DisplayName~' + $case[1]
    pwsh -NoProfile -File TestResults/m2212a15-2-verified/run.ps1 -Name "../m2212a15-3-verified/$name" -Filter $filter
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
pwsh -NoProfile -File TestResults/m2212a15-2-verified/run.ps1 -Name '../m2212a15-3-verified/header-notifications' -Filter 'Workload=ExclusiveWpf&(FullyQualifiedName~SplitCreationMenusRefreshAndContextualCancellation|FullyQualifiedName~TopNotificationsWrapAcrossPanelsAndDismissWithoutResolvingFailedSave)'
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
pwsh -NoProfile -File TestResults/m2212a15-2-verified/run.ps1 -Name '../m2212a15-3-verified/logic' -Filter 'Workload!=ExclusiveWpf&(FullyQualifiedName~NotebookNotificationTests|FullyQualifiedName~NotebookNoteActionsTests|FullyQualifiedName~ReleaseSchedulingTests)'
pwsh -NoProfile -File TestResults/m2212a15-3-verified/audit.ps1
pwsh -NoProfile -File TestResults/m2212a15-3-verified/check-whitespace.ps1
pwsh -NoProfile -File TestResults/m2212a15-3-verified/check-doc-links.ps1
```

The existing runner uses Release `--no-build --no-restore`, durable output, TRX and unique child supervision directories. All six invocations and the outer command exited **0**. No new runner, production mechanism, scheduling change or additional deadline was introduced. `test-audit.json`, `results.json` and `coverage-audit.json` verify literal-path-safe TRX completeness, 30 unique parent identities, ten unique intended child identities, no extra/missing/skipped/duplicate results and ten confirmed child exits. Parent and child totals are kept separate.

| Run directory | Parents | Children | Failures/skips | Exit | Command wall time |
| --- | --- | --- | --- | --- | --- |
| `menus-Light-1280` | 1/1 | 1/1 | 0/0 | 0 | 11.244s |
| `menus-Dark-1280` | 1/1 | 1/1 | 0/0 | 0 | 10.625s |
| `menus-Light-480` | 1/1 | 1/1 | 0/0 | 0 | 11.307s |
| `menus-Dark-480` | 1/1 | 1/1 | 0/0 | 0 | 11.213s |
| `header-notifications` | 6/6 | 6/6 | 0/0 | 0 | 47.038s |
| `logic` | 20/20 | None | 0/0 | 0 | 3.887s |

`readiness-audit.json` preserves **38 successful native target checks and 38 injections**, ten each in normal-width menu cases and nine each in narrow cases. Every actual hit HWND equals its intended HWND; zero failed readiness checks. The opt-in menu/input-origin diagnostics were enabled. The initial Light/1280 trace reports foreground **Progman** while the intended target point already belongs to the test window; the first click then foregrounds the test window. Foreground identity alone is therefore not used as proof of obstruction or ownership. Native WPF DPI is 2.5× on this desktop; parameter DPI 96/240 controls captured bitmap density and is not a claim that the desktop DPI changed per row. No notification manipulation or readiness bypass was used to obtain a pass. These are observations at each tested interaction, not a continuous desktop recording.

Five current PNGs were opened and inspected; `renders.json` records their exact original paths and hashes beneath `header-notifications/notebook-notifications-*/notification-renders/`: **Light-1280-96-success.png, Dark-1280-96-information.png, Light-480-240-warning.png, Dark-480-240-error.png and Dark-480-240-dismissed-save-error.png**. All four severity presentations remain readable above the workspace, including wrapped narrow errors. The dismissed save-error image retains **Save paused**, explanatory failure text and Save/Discard, with no false green check. The generic error-severity sample has a canonically saved note, so its separate Saved locally state is expected. A16 status placement and A17 TAGS header alignment remain intact. No blocking defect was found in these inspected captures; unchanged A17.1 evidence is reused for populated tag rows and Refresh.

### Result, inventory and limits

**30/30 distinct parents and 10/10 distinct matching isolated children passed; zero failures/skips/incomplete cases; all exits 0.** Each selected identity executed once in this phase, with no retries. All identities overlap historical A15 coverage; A15.2's nine completed/six passed/three failed children and tenth incomplete child remain historical and are not added to today's totals. All ten supervision logs confirm child exit; final process-name inventory found no dotnet/testhost/vstest process (`after-context.json`). No unrelated process was terminated.

Exact tracked inventory: **README.md and docs/notebook.md only**, including the earlier withheld-launch documentation. Ignored artifacts contain the read-only context observer, manifests, original results/logs/captures and audits. Whitespace and **224 local documentation targets** passed after edits; URL and fragment validation are outside that check. **A15 focused regression acceptance is complete under the user-prepared supported input-desktop conditions.** This pass does not identify the unknown original A15 foreground owner or prove every historical failure had the same cause. M22.12B and overall acceptance remain pending. Human/Narrator, native journal workflow/restart and matching hosted input/timing compatibility remain unverified. No complete gate, production/harness change, publication or workflow triggering occurred.

## M22.12A17.1 — Refresh render discrepancy resolved

28 October 2026 (Europe/Kiev), one agent. Actual initial state: **clean develop / `54a8f57cbb3a78d30757db6da3569fa176e20eb0`**, worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. A17's four changed files were committed in the intervening commit. Repository/ancestor inspection found no AGENTS.md. HEAD remains unchanged and all historical artifacts are preserved. Evidence root: **`TestResults/m2212a17-1-verified/`**.

### Evidenced cause and superseded conclusion

**The earlier missing Refresh icon report was an incorrect visual assessment.** Opening the original-resolution retained PNGs shows the icon in both the early creation and later filter frames. All four A17 source/document hashes and both retained Desktop/test DLL hashes matched before this task changed anything (`baseline.json`). Each copied PNG also matches both its A17 manifest hash and referenced original file, eliminating stale/mismatched copies as the explanation.

The exact disputed population is `TestResults/m2212a17-verified/renders/Light-480-240-` with these `.png` suffixes: **`default`, `notification-dismissed`, `creation-empty`, `creation-populated`, `creation-error`, `filters`**. Their originals remain under `final-renders/notebook-tags-20261028-161830-af9c5185a61143008b8a9ea4a8cf3ac7/notebook-tag-renders/`. Every image is **1200×1900 pixels**, representing a 480×760-DIP viewport at 240 DPI, with Organization selected.

`compare-retained.ps1` reads the files without changing them. The same interior region `(1076,380,74,74)` in all six images has **573 pixels with each RGB component ≥240**, bounded by `(1090,394)`–`(1134,437)`, and the same RGBA SHA-256: **`9500E61AFA5E8C35D27E49F3FDCD0C089E25409983C8984F7266652D62C19A9C`**. The white arrow therefore exists in the alleged missing frames and is byte-identical to the later one. See `retained-pixels.json` for paths, bounds, counts and original/manifest hash checks.

This supersedes the **A17 README unresolved-Refresh paragraph, Notebook “Retained visual limitation” paragraph, and A17 summary's pending Refresh-raster diagnosis**. They remain historical records with an explicit supersession note; their screenshots and failures are unchanged. There is no evidenced production icon/style defect, stale-capture defect or timing defect to repair. A17's earlier dispatcher flush was not demonstrated to fix anything, and this task does not claim that it did.

“Offscreen” here describes an unattached WPF tree rendered into a bitmap, **not a control outside the viewport**. The enabled Refresh button is within the Organization viewport, at 36×36 DIP; the narrow coordinates are `(427,148.24)` and the icon has 18×18-DIP arranged bounds. Effective `IsVisible` is false for this unattached tree even though declared Visibility is Visible and its pixels are rendered. The foreground is white on teal. The icon uses the existing stroked `PtjIconRefresh` Geometry, not a font glyph. The new traces below verify actual state, bounds, geometry, clips, scroll offsets and foreground pixels rather than interpreting effective IsVisible as a raster result.

### Narrow verification extension and commands

The only code change is in the existing offscreen `NotebookTagViewTests` fixture. It holds its synthetic `OnList` result while invoking the **existing RefreshCommand**, captures a legitimately busy/disabled control, then releases the result and verifies re-enablement. It never sets Visibility, IsEnabled, colors or control state. Existing tag behavior and alignment assertions are retained. Snapshot diagnostics record the command identity, busy/enabled state, measured/rendered geometry, colors, root-relative button/icon bounds, viewport/scroll offsets, Path clip/layout clip and layout validity. Enabled, busy and ready images assert actual foreground pixels in the icon bounds and the unchanged 36-DIP hit-target layout. No production XAML, commands, resources, autosave, guards, accessibility names or focus behavior changed.

```powershell
pwsh -NoProfile -File TestResults/m2212a17-1-verified/compare-retained.ps1
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore
pwsh -NoProfile -File TestResults/m2212a17-1-verified/run.ps1 -Name focused -Filter 'FullyQualifiedName~BoundTagsDistinguishFiltersAssignmentsAndGlobalDeletionWithRetainedEditor&DisplayName~Light&DisplayName~1280'
pwsh -NoProfile -File TestResults/m2212a17-1-verified/audit.ps1
pwsh -NoProfile -File TestResults/m2212a17-1-verified/check-whitespace.ps1
pwsh -NoProfile -File TestResults/m2212a17-1-verified/check-doc-links.ps1
```

The initial diagnostic build (`build/output.log`) failed with **five CS0103 errors**, all from a missing `System.Globalization` import in the new diagnostic expressions; no tests ran against that failed build. After adding the import, `build/final-output.log` passed **exit 0, zero warnings/errors, 17.47s**. The retained build failure is separate from runtime results. `provenance.json` and `binaries.json` identify the final HEAD-plus-test-change build; production source remains identical to A17, with rebuilt metadata/hashes recorded separately.

One focused invocation passed **1/1 parent and 4/4 distinct children**, zero failures/skips, **exit 0**, **25.324s** command wall time. The unchanged suite dispatcher launches all four Light/Dark × 1280/96 and 480/240 child rows from the single selected Light/1280 parent. The other three discoverable parents were not invoked, preventing redundant suite launches. These identities overlap A17 evidence and are not new cases added to its totals. The literal-path-safe audit verifies exactly those four identities, completeness and no skipped/duplicate child results (`test-audit.json`). The existing 90-second process/30-second case watchdogs, recursion protection, scheduling and assertions remain intact. No runtime retry, other focused suite, native-input test or complete gate ran.

### State and image review

`refresh-states.json` extracts the twelve enabled/busy/ready observations from the child's `refresh-state.jsonl`. All use **Panel=0**, declared Visible, valid measure/arrange, zero horizontal/vertical scroll offsets and no Path/layout clip. Busy state adds the existing 42-DIP active-operation row above the workspace, legitimately moving the button downward; it remains within the viewport. No control is outside the screenshot or hidden by scrolling.

| Profile | Enabled → busy → ready | Measured foreground pixels, respectively | Button bounds: enabled / busy |
| --- | --- | --- | --- |
| Light 1280/96 | True → False → True | 56 / 58 / 56 | `(157,79.62,36,36)` / `(157,121.62,36,36)` |
| Dark 1280/96 | True → False → True | 56 / 58 / 56 | Same normal-width bounds |
| Light 480/240 | True → False → True | 557 / 567 / 557 | `(427,148.24,36,36)` / `(427,190.24,36,36)` |
| Dark 480/240 | True → False → True | 557 / 567 / 557 | Same narrow bounds |

These new pixel counts use the actual icon bounds and a stricter foreground-color tolerance; they must not be numerically compared with the larger retained-image region's 573-white-pixel count. Both methods establish the icon's presence. Disabled states use the existing secondary-text stroke and surface background, with readable arrows in both themes. Enabled states use the existing white-on-teal styling. Geometry is unchanged throughout; full values are recorded in the JSON traces.

**Ten new captures were opened and inspected**, plus the two original A17 creation-empty/filters images at original resolution. New copied paths are beneath `TestResults/m2212a17-1-verified/renders/`: for each prefix **`Light-1280-96`, `Dark-1280-96`, `Light-480-240`, `Dark-480-240`**, the inspected suffixes are **`-default.png` and `-refresh-busy.png`**; both narrow profiles additionally have inspected **`-refresh-ready.png`**. `renders.json` maps hashes and original paths under the unique child directory. Icons, bounds and 36-DIP action layout are readable and unclipped in all inspected states; A17's centered tag rows and A16's saved/check/pin presentation remain intact. Twelve target-state images are retained, including the two uninspected normal-width ready frames; the complete existing fixture also retains its other synthetic states.

### Inventory and acceptance limits

Exact task inventory: **three modified files, no added tracked files**:

- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookTagViewTests.cs` — controlled busy/ready captures and state/pixel observability in the existing fixture.
- `README.md` and `docs/notebook.md` — correct the visual conclusion and record focused evidence.

Artifact readers, manifests and pixel analysis are ignored files in the phase directory. Whitespace and local documentation-target checks are retained there. The Refresh discrepancy is resolved; **no production repair is warranted**. A17's specific Refresh-raster visual limitation is closed. **A15 native regression acceptance and its obstructed-desktop blocker, M22.12B and overall M22.12 acceptance remain pending.** Offscreen rendering verifies pixels and hit-target layout, not native hit testing, real pointer/focus interaction, human/Narrator or journal restart. Matching hosted compatibility remains unverified. No production/provider access, Git publication, workflow triggering, CI/timeout, schema or export changes occurred.

## M22.12A17 — Organization TAGS alignment and integrated review

Historical record: its missing-Refresh visual conclusion is superseded by the A17.1 evidence above. The original artifacts are preserved.

28 October 2026 (Europe/Kiev), one agent. Actual initial baseline: **clean develop / `64fef0e23c25b97afdc7e35e699c1915244d37fa`**, worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. The intervening commit incorporated A16's three modified files and added fixture; all previous work and artifacts are preserved. Repository/ancestor inspection found no AGENTS.md. HEAD remains unchanged. Evidence root: **`TestResults/m2212a17-verified/`**. The supplied current screenshot `C:/Users/Yovko/AppData/Local/Temp/codex-clipboard-161e2b7b-5af4-41c3-bdae-fac111ff7b64.png` was opened and compared: its red box identifies the left global TAGS list, whose labels sat above the management-button centers.

### Presentation and behavior

- TAGS and New tag occupy one centered header row. The button retains its teal style, name, tooltip, focus indicators and command handler, with compact 12-point text/32-DIP minimum height. Its declaration remains after the filter/paging controls to preserve their keyboard traversal order despite the visual placement.
- Each tag row has a 36-DIP minimum height and consistent two-DIP vertical margins. The checkbox/text and 36-DIP management button are centered vertically; an eight-DIP gap separates wrapped labels from the action. A full filter-label tooltip preserves long-name access. Taller wrapped rows keep the action centered without overlap.
- The creation name field stays full width. Add and Cancel share an aligned row beneath it; a **54-DIP reserved Add column** includes its six-DIP gap. Add still uses its original conditional **Collapsed/Visible** binding, while Cancel retains its screen position and the form retains its height when Add appears. Empty/whitespace input, explicit Cancel, validation feedback and input retention are unchanged.
- No view-model, persistence, focus helper, popup ownership or command code changed. Enter/Escape, pending/late completion guards, filtering/selection, captured Rename/Delete identities and global catalog versus note-assignment semantics retain their existing paths. Note-editor assigned-tag chips were not moved or redesigned.

### Commands and retained results

```powershell
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore
pwsh -NoProfile -File TestResults/m2212a17-verified/run.ps1 -Name offscreen -Filter 'FullyQualifiedName~BoundTagsDistinguishFiltersAssignmentsAndGlobalDeletionWithRetainedEditor&DisplayName~Light&DisplayName~1280'
# Correct the new offscreen-only assertion after inspecting the retained failure, then rebuild:
pwsh -NoProfile -File TestResults/m2212a17-verified/run.ps1 -Name corrected-offscreen -Filter 'FullyQualifiedName~BoundTagsDistinguishFiltersAssignmentsAndGlobalDeletionWithRetainedEditor&DisplayName~Light&DisplayName~1280'
pwsh -NoProfile -File TestResults/m2212a17-verified/run.ps1 -Name logic -Filter 'FullyQualifiedName~NotebookTagFormTests|FullyQualifiedName~NotebookTagTests'
# Rebuild after adding capture-readiness diagnostics/dispatcher completion, then regenerate:
pwsh -NoProfile -File TestResults/m2212a17-verified/run.ps1 -Name final-renders -Filter 'FullyQualifiedName~BoundTagsDistinguishFiltersAssignmentsAndGlobalDeletionWithRetainedEditor&DisplayName~Light&DisplayName~1280'
pwsh -NoProfile -File TestResults/m2212a17-verified/audit.ps1
pwsh -NoProfile -File TestResults/m2212a17-verified/check-whitespace.ps1
pwsh -NoProfile -File TestResults/m2212a17-verified/check-doc-links.ps1
```

Build logs under `build/`: `output.log` **50.74s**, `final-output.log` **18.48s** (preserve keyboard traversal before tests), `fixture-correction.log` **8.36s**, `render-readiness.log` **7.33s**. All exited **0**, zero warnings/errors. The final test binary includes both fixture corrections; the production XAML was unchanged across the three runtime selections. Hashes and tested source identity are recorded in `provenance.json` and `binaries.json`.

| Run | Parents passed/total | Children passed/total | Skips | Exit | Command wall time |
| --- | --- | --- | --- | --- | --- |
| `offscreen` — retained initial failure | 0/1 | 0/4 | 0 | 1 | 12.509s |
| `corrected-offscreen` | 1/1 | 4/4 | 0 | 0 | 14.317s |
| `logic` | 21/21 | None | 0 | 0 | 3.187s |
| `final-renders` | 1/1 | 4/4 | 0 | 0 | 20.320s |

There are **22 distinct parent identities and four distinct child identities** with final passing evidence, across **24 parent executions and 12 child executions**. Rendering reruns overlap completely and are not summed as extra coverage. The unchanged `NotebookTagViewTests` dispatcher launches the existing **whole four-row child suite** from the selected Light/1280 parent; the four discoverable parent rows were not all invoked, avoiding sixteen child executions. This task does not change that dispatch or claim four executed parents. The literal-path-safe audit verifies four distinct intended Light/Dark × 1280/96 and 480/240 child identities, complete results and no skipped/duplicate cases per run. The initial parent and four child failures remain visible in `test-audit.json`; latest passing projections do not erase them.

All original tag-view behavior assertions remain. The fixture now checks actual arranged centers, non-overlap and action bounds, stable Cancel position/form height, empty/populated/invalid creation, Workspace scopes/Refresh accessibility and the saved header. The 21 existing logic cases cover validation/persistence failure, duplicate submissions, cancellation and late completion, captured rename/delete identity, assignment/global deletion, filtering and autosave coordination. Their overlaps with earlier A11/A15 evidence are not added to historical totals. Existing scheduling, 90-second process and 30-second case deadlines are retained. No Window, native pointer or focus helper is used by these selections; the offscreen layout asserts no `PresentationSource`. Process-name inventories found no testhost/vstest before or after; no user process was changed.

The initial failure was the **new** `Assert.True(cancel.IsVisible)` at `NotebookTagViewTests.cs:55`. WPF's effective IsVisible does not establish displayed controls for an unattached offscreen root. The correction checks declared Visible plus positive arranged dimensions; existing behavior assertions were preserved. Four identical child failures share that one fixture error, rather than four production failures. Raw TRX, stdout/stderr and supervision evidence remain under `offscreen/`.

### Integrated visual findings and paths

The final suite generated **56 offscreen PNGs**. Fifteen current-source images were inspected; `renders.json` records original paths, SHA-256 hashes and inspection provenance. Fourteen final images are byte-identical to the already inspected `corrected-offscreen` captures, so that inspection is reused; the final Light/narrow notification capture was opened separately. All copied paths below are beneath `TestResults/m2212a17-verified/renders/`; originals remain under `final-renders/notebook-tags-20261028-161830-af9c5185a61143008b8a9ea4a8cf3ac7/notebook-tag-renders/`.

| Profile | Inspected filenames | Findings |
| --- | --- | --- |
| Light 1280/96 | `Light-1280-96-default.png`, `creation-empty`, `creation-populated`, `filters`, `notification-dismissed` with the same profile prefix and `.png` suffix | TAGS/header and label/action centers align; empty/populated controls retain position; long selected labels wrap without covering menus; A14 scopes and A16 saved/check/pin remain visible |
| Dark 1280/96 | `Dark-1280-96-default.png`, `creation-error`, `notification-visible` | Theme contrast and wrapped validation remain readable; notification stays above the workspace and saved status stays beside pin |
| Dark 480/240 | `Dark-480-240-creation-populated.png`, `filters`, `saved-content` | Organization and Content panels remain accessible separately; long names wrap; form actions fit; saved check/text precedes pin |
| Light 480/240 | `Light-480-240-creation-empty.png`, `creation-error`, `filters`, `notification-visible` | Creation and errors fit; notification wraps above panel switching; **Refresh icon discrepancy below remains unresolved** |

**Retained visual limitation:** the early Light/480/240 default/dismissed/creation frames show a teal Refresh button without its icon, while notification-visible and later `filters` frames show the icon. The Render helper was updated to drain ContextIdle before capture and log the Refresh Path stroke. The final images are pixel-identical to the previous captures; `render-readiness.log` reports a white (`#FFFFFFFF`) stroke before and after the flush. This **does not establish a foreground-binding cause or resolve the missing raster**. Refresh production code/resources were not changed by A17, and the observation does not establish an A17 production regression or native behavior. No further reproduction or unrelated icon repair was attempted. A clean integrated visual acceptance claim is therefore withheld; this discrepancy needs bounded diagnosis before overall UI acceptance.

Tag alignment itself has no remaining blocking issue in the inspected captures. Field validation can still appear in its pre-existing editor TagMessage location in addition to Organization; this presentation was preserved. Automated renders and model commands do not verify real keyboard/focus return, menu mouse readiness, Narrator or native journal restart. A15.2's obstructed input desktop remains the reason native pointer/focus tests were not launched; its current and historical failure limitations remain intact.

### Exact inventory and handoff

Four modified files, no added tracked files, no pre-existing pending changes:

- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml` — only the left global TAGS header, rows and creation layout.
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookTagViewTests.cs` — extend existing behavior/render coverage with geometry and integrated states, durable render paths and capture readiness diagnostics.
- `README.md` and `docs/notebook.md` — outcomes, retained failures, visual limitation and evidence.

Whitespace and local documentation-target checks are retained in phase artifacts. **A17 alignment implementation and focused regression checks pass; integrated visual review retains the Refresh-raster limitation. A15 focused regression acceptance, M22.12B and overall M22.12 acceptance remain pending.** No complete gate, native pointer/focus tests, production/provider access, publication or workflow triggering ran. Schema, persistence, archive/export formats, CI, scheduling and timeouts are unchanged.

## M22.12A16 — Save status beside the pin

28 October 2026 (Europe/Kiev), one agent. Actual initial baseline: **clean develop / `5529d01fea38caa0642fc494ef8d6280ef37ee7c`**, worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. The intervening commit contains A15.2's README/Notebook documentation; all previous work and evidence are preserved. Repository/ancestor guidance inspection found no AGENTS.md. HEAD remains unchanged; tested binaries represent that HEAD plus the XAML and new test file listed below. Evidence root: **`TestResults/m2212a16-verified/`**.

### Header contract and unchanged ownership

The existing `NoteSaveStatus` moves from beneath the folder breadcrumb into one horizontal status/icon group directly before the pin, vertically centered with the pin and note menu. The saved state is **green check → Saved locally → pin → note menu**. It reuses `PtjIconActivate` and `PtjSuccessBrush`; accessible live status text conveys meaning independently of color. The status/icon group stays together; the breadcrumb can wrap in the remaining column, and the long editable title retains its own full-width row. The original compact-status location is removed. The detailed `SaveState` explanation, Save/retry, Discard, recovery and failure text remain contextual, rather than being removed as a duplicate of the compact label.

There is **no new save-state machine**: the XAML check visibility is triggered only by the existing `CompactSaveState == "Saved locally"`. Its existing precedence suppresses success during saving, recovery conflict, recovered/paused state, dirty content or unavailable content. Existing `SaveCoreAsync` checks activation and selected-note identity before publishing, and compares the current draft against the persisted signature so an older committed revision cannot acknowledge newer edits. A selection change while a write is pending is rejected by the existing guard; a subsequent accepted selection installs that note's authoritative draft. These mechanisms, expected versions, autosave debounce, persistence, recovery, images and undo/redo were not changed.

Existing compact wording is retained: `Saving…`, `Unsaved changes`, `Save paused`, `Conflict` and unavailable/recovery states. In particular, a canonical conflict can retain compact **Save paused** with its explicit **Conflict** explanation in persistent `SaveState`; recovered content also retains its existing explanatory text. A checkpoint alone never shows the green check. Notification expiry/dismissal does not control this persistent presentation.

### Focused commands and distinct results

```powershell
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore
pwsh -NoProfile -File TestResults/m2212a16-verified/run.ps1 -Name offscreen -Filter 'FullyQualifiedName~SaveStatusBesidePinReflectsCanonicalStateOffscreen'
pwsh -NoProfile -File TestResults/m2212a16-verified/run.ps1 -Name logic -Filter 'FullyQualifiedName~NotebookAutosaveTests|FullyQualifiedName~NotebookNotificationTests|FullyQualifiedName~CapturedSaveCannotAcknowledgeNewerEditsOrPublishAfterDisposal'
pwsh -NoProfile -File TestResults/m2212a16-verified/audit.ps1
pwsh -NoProfile -File TestResults/m2212a16-verified/check-whitespace.ps1
pwsh -NoProfile -File TestResults/m2212a16-verified/check-doc-links.ps1
```

The runner uses Release `dotnet test --no-build --no-restore --filter ... --logger trx --results-directory ...`. Build: **exit 0, zero warnings/errors, 53.53s** (`build/output.log`). Current source and DLL hashes are retained in `provenance.json` and `binaries.json`.

| Selection | Parent results | Separate child results | Failures/skips | Exit | Command wall time |
| --- | --- | --- | --- | --- | --- |
| New offscreen save-status matrix | **4/4** | **4/4**, one exact matching child per parent | 0/0 | 0 | 29.611s |
| Autosave, notification logic, captured-save ownership | **34/34** | None | 0/0 | 0 | 3.000s |

Total: **38 distinct parent cases and four distinct isolated children**, counted separately; no duplicate identities or overlap between the two current selections. The 34 logic cases overlap historical A15 coverage and are not added to historical totals. They comprise 20 autosave/recovery cases, 12 notification cases and two older-save/newer-edit/disposal cases. No test failed or was retried. The literal-path-safe audit checks complete counts, no duplicate results and exactly one matching child identity for each isolated parent.

The new partial fixture retains its existing `NotebookHeaderViewTests` exclusive trait/collection and runs before the logic selection. It uses case-owned Application/dispatcher lifetimes, exact child filters/recursion guard, **90-second process / 30-second case deadlines**, and existing two-thread collection settings. It creates **no Window or HwndSource**, calls no pointer/keyboard/focus helper, and asserts `PresentationSource.FromVisual(view) == null` after layout. The restricted executor ran the build and both selections; native input-desktop access was neither requested nor used. A read-only CIM process query was denied; the fallback `Get-Process` inventory showed no testhost, vstest or dotnet process before the build. Final process inventory is retained separately. No A15 native menu selection ran.

The offscreen matrix verifies exact accessible status/check visibility for saved, edited, saving, newer edits during a held save, storage failure, canonical conflict and explicit recovery. It checks guard rejection of switching during the held write, the later accepted note's clean status, notification dismissal and elapsed ten-second notification interval without clearing saved status, and persistent failure after dismissal. Geometry assertions verify status-before-pin-before-menu order, vertical alignment and action bounds at 1280 DIP/96 DPI and 480 DIP/240 DPI. Existing logic tests retain deterministic clock boundaries, recovery durability, cancellation/activation/disposal and stale-save assertions.

### Inspected captures and exact inventory

Eight offscreen PNGs were generated; **six were inspected**. `renders.json` maps every image to its original child directory, hash and inspection status. Paths below are beneath `offscreen/`, with files in each directory's `save-status-renders/`:

| Child directory | Inspected files |
| --- | --- |
| `notebook-save-status-20261028-102624-5c684088ca634d8da2121f6eb3bc1af3` | `Light-1280-96-saved.png`, `Light-1280-96-paused.png` |
| `notebook-save-status-20261028-102632-605c1a5828f94943aa5bd08a47ee3b4b` | `Dark-480-240-saved.png`, `Dark-480-240-paused.png` |
| `notebook-save-status-20261028-102637-cd6b8df546b145a2a6294ff8b8bd6887` | `Dark-1280-96-saved.png` |
| `notebook-save-status-20261028-102642-959de63df8cc45c387793fc04c08c720` | `Light-480-240-saved.png` |

The check, status, pin and ellipsis are readable and aligned in both themes; long titles wrap below without overlapping the controls. Paused captures correctly remove the check and retain the failed-save/recovery explanation and retry controls after banner dismissal. No blocking A16 visual issue was found in these captures. These are real WPF offscreen renders, **not native mouse/focus or human accessibility acceptance**.

Exact task inventory: **three modified files and one added file**, with no pre-existing uncommitted changes:

- `README.md` — current outcome and limitations.
- `docs/notebook.md` — contract, commands, results and evidence.
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml` — relocate the existing status and conditionally show the existing themed check icon.
- Added `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookHeaderViewTests.SaveStatus.cs` — four exact-dispatch offscreen cases and retained captures.

Whitespace and local documentation-target validation are recorded in the phase artifacts; no schema/model, export/archive format, CI, deadline or scheduling change requires additional checks. **A16 focused logic/presentation verification is complete; A15 regression acceptance, A17 and M22.12B remain pending.** A15.2 demonstrated ChatGPT's window obstructing native targets despite an initially passing preflight; that blocker and the original failures' unidentified historical foreground owner remain documented. Native pin/menu hit testing, keyboard/focus/Narrator assessment, journal restart and matching hosted input compatibility remain unverified. No complete gate, provider/production access, Git publication or workflow triggering occurred. A17's left TAGS creation section was not changed.

## M22.12A15.2 — Focused input-desktop verification

28 October 2026 (Europe/Kiev), one agent. Actual initial baseline was **clean develop / `a3dbab5e284893c20ee5002732e740e9548ddade`**, worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. The intervening commit incorporated A15.1's three files. Repository/ancestor guidance inspection found no AGENTS.md. HEAD is unchanged. **A15 focused regression acceptance remains pending: the input desktop did not remain unobstructed.** No production or test/harness source changes were made.

Evidence root: **`TestResults/m2212a15-2-verified/`**. `provenance.json` verifies all A15 production-source hashes against its retained manifest; the only differences since A15 are documentation and the opt-in menu diagnostics committed in A15.1. The current Release rebuild passed, **exit 0, zero warnings/errors**, reported build time **63.46s**, command wall time **64.187s**. `build-identity.txt` records `1.0.0+a3dbab5e284893c20ee5002732e740e9548ddade`; `binaries.json` records Desktop/test DLL SHA-256 hashes. No stale binaries were tested.

### Execution conditions and bounded selection

`before-context.json` and `launch-context.json` record session **1 / WinSta0 / Default**, interactive mode, cursor access and the executor's existing administrator token, with **zero active test processes**. No UAC/token/desktop switch was performed. `preflight.ps1` creates one ordinary test-sized WPF window and uses the existing activation pattern: Show, thread-local SetActiveWindow, keyboard focus and layout completion. Four sampled points returned that window's HWND; it also owned foreground. The probe injects no input, does not use topmost/global-foreground overrides and closes only its own window. This is a point-in-time prerequisite check, **not proof of later child-window ownership**. Actual tests retain per-target native hit testing and all readiness assertions.

The smallest planned non-overlapping selection was **30 parents**: the complete `NotebookHeaderViewTests` family (**10 exclusive parents / ten exact isolated children**: four menu, four notification-render and two split-header cases), plus `NotebookNotificationTests` (**12**), `NotebookNoteActionsTests` (**7**) and `ReleaseSchedulingTests` (**1**) in the existing parallel classification. The latter 20 were **not launched** after native verification stopped; their passing A15 evidence remains source-compatible. These are overlaps with A15's 186 parents, not additional coverage totals. The remaining unchanged A15 selections were not repeated.

Executed commands, from the repository root:

```powershell
pwsh -NoProfile -File TestResults/m2212a15-2-verified/context.ps1 -OutputName before-context.json
pwsh -NoProfile -STA -File TestResults/m2212a15-2-verified/preflight.ps1
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore
pwsh -NoProfile -File TestResults/m2212a15-2-verified/context.ps1 -OutputName launch-context.json
$env:PTJ_NOTEBOOK_MENU_DIAGNOSTICS='1'
$env:PTJ_INPUT_ORIGIN_TRACE='1'
pwsh -NoProfile -File TestResults/m2212a15-2-verified/run.ps1 -Name exclusive -Filter 'Workload=ExclusiveWpf&FullyQualifiedName~NotebookHeaderViewTests'
# Native verification interrupted after external obstruction was observed; no retry.
pwsh -NoProfile -File TestResults/m2212a15-2-verified/context.ps1 -OutputName after-context.json
pwsh -NoProfile -File TestResults/m2212a15-2-verified/partial-audit.ps1
pwsh -NoProfile -File TestResults/m2212a15-2-verified/check-whitespace.ps1
pwsh -NoProfile -File TestResults/m2212a15-2-verified/check-doc-links.ps1
```

The runner uses `dotnet test ... -c Release --no-build --no-restore --filter ... --logger trx --results-directory ...`. Exclusive collection membership, two-thread assembly limit, exact child selection, recursion guard, **90-second process / 30-second case deadlines**, input guards and cleanup code remain unchanged. No complete gate or new cancellation deadline was introduced.

### Partial results and established blocker

The first menu child, Dark/480/240, passed. Subsequent children failed **before the first mouse injection** at the unchanged `MouseClick` readiness assertion (`NotebookHeaderViewTests.Menus.cs:199`, called at line 98). All three show settled animation, valid WPF measure/arrange, correct cursor coordinates, local keyboard focus, released left button and no fixture capture. **Notification=null, expiry task=RanToCompletion, banner=Collapsed/0, menu closed**. The native HWND at each point belongs to a different process:

| Menu identity | Failure UTC | Target point / expected fixture HWND | Retained child directory under `exclusive/` |
| --- | --- | --- | --- |
| Light / 1280 / 96 | 09:45:00.238 | `(3251,518)` / `0x200272` | `notebook-menus-20261028-094451-923b2faaff7545e19496fcffe334d70c` |
| Dark / 1280 / 96 | 09:45:08.678 | `(3373,640)` / `0x905A4` | `notebook-menus-20261028-094500-a7fa98b222a246a1b4ff2a15423fe6d9` |
| Light / 480 / 240 | 09:45:16.902 | `(1495,934)` / `0xF0580` | `notebook-menus-20261028-094509-f7312f4492214798b2b14310090d3ab5` |

All three hit **`0x2027C`, class `Chrome_RenderWidgetHostHWND`, PID 13552**; foreground was **`0x2027A`, class `Chrome_WidgetWin_1`, same PID**. Read-only process lookup in `hit-process.json` identifies **ChatGPT**, not the Chrome browser. Window class names alone do not establish the application name. These observations confirm an external obstruction for this run, not an A15 notification/layout defect. They do not identify what took foreground or the owner of the **original** failures' historical `0x600F4` HWND.

When the retained logs exposed the obstruction, the single invocation had already advanced through other children. Ctrl+C stopped further native verification; no case was retried, no application was closed/moved and no user process was killed. The tool reported interruption exit **1**; **there is no finalized parent TRX, normal VSTest exit status or completed command wall-time record**. Do not present that interruption code as a completed test-run exit or invent parent pass totals.

| Evidence level | Actual outcome |
| --- | --- |
| Parent selection | Ten intended identities; three explicit failed-parent reports in `exclusive/output.log`; no final parent summary, so final parent pass count is unverified |
| Completed children | **Nine distinct one-result TRX files: six passed, three failed, zero skipped**, all within the exact intended case matrix; no duplicate child identity |
| Passing child coverage | Dark/480 menu; all four Light/Dark normal/narrow notification cases; Light/1280 split-header case |
| Incomplete child | Dark/480 split-header started, then interrupted; no child TRX or final supervision-cleanup record, so it is not counted as passed or skipped |
| Unlaunched parallel selection | Twelve notification logic, seven note ownership and one scheduling regression; retained passing A15 results reused as historical compatible evidence, not a fresh run |

`partial-audit.ps1` reads TRX with literal paths, checks one intended result per completed child and duplicate identities, explicitly reports the missing child/parent summary, and exits **1** for incomplete/failed verification. `test-audit.json` retains exact names, errors, stacks, timings and failed native observations. The nine completed launchers have supervision exit/cleanup records; `after-context.json` independently confirms **zero remaining test processes**, including the interrupted launcher and testhost. Existing build-server processes are not tests and were left alone. Cleanup does not imply the interrupted child's WPF finally block completed.

### Visual evidence, inventory and handoff

All four notification children passed their rendered placement, severity, dismissal, persistent failed-save, focus and controllable-clock assertions. Two new actual captures were inspected: `exclusive/notebook-notifications-20261028-094517-511c132244bc4d62973c28771e9ce7a8/notification-renders/Dark-480-240-error.png` and `exclusive/notebook-notifications-20261028-094524-8f81357c05df4eb182ebd187baa222e3/notification-renders/Light-1280-96-error.png`. Error text, icon and close control remain readable; the narrow banner wraps above the panels without overlapping controls. The palette publishes a synthetic operation error before changing the saved note, so the visible Saved locally label is not a failed-save claim. The existing A15 final capture inspection remains valid because its production view/resources and notification fixture are unchanged. These rendered bitmaps do not establish unobstructed native input.

Exact tracked task inventory: **two modified files, no added tracked files** — `README.md` and `docs/notebook.md`. All probe scripts, partial results, hashes and diagnostics are ignored artifacts under the phase root. No production, test, configuration, scheduling, assertion, timeout or persistence behavior changed. Whitespace passed and **220 local documentation targets passed**, with no missing paths; URLs and fragment-anchor validation are outside that local-link check.

**A15 focused regression acceptance remains pending.** The next prerequisite is an execution arrangement that keeps the native test windows unobstructed through child-process transitions, without closing unrelated applications or forcing foreground/state. No further run is justified on the currently demonstrated obstructed arrangement. Historical A15/A15.1 failures and the earlier passing diagnostic are preserved, not reclassified as harmless. A16–A17 and M22.12B remain pending. Human/Narrator, native journal workflow/restart and matching hosted timing/input compatibility remain unverified.

## M22.12A15.1 — Retained menu-readiness diagnosis

27 October 2026 (Europe/Kiev), one agent. Actual initial state: **clean develop / `4ad570970654870c7e303a744f525dbaf521a6b9`**, worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. A15's 14 modified and three added files were committed in this intervening commit; nothing was discarded. Repository and ancestor guidance inspection found no AGENTS.md. HEAD remains unchanged. This phase adds diagnostics and documentation only; A15 production behavior is unchanged. Evidence root: **`TestResults/m2212a15-1-diagnosis/`**. Original A15 artifacts, failures and the earlier diagnostic pass are preserved under `TestResults/m2212a15-verified/`.

### Source, binary and scheduling provenance

Before instrumentation, all **17** retained A15 source/document hashes and both final Desktop/test DLL hashes matched the current checkout/binaries. The original failing run preceded A15's diagnostic-only menu tracing and synthetic severity-text correction, so its test DLL is not the retained final test DLL; an original byte-for-byte assembly was not retained. Its TRX stacks and build history establish that it used the same production changes and the pre-diagnostic menu helper. The new Release build uses current HEAD plus only the diagnostic changes below. Production source remains identical, but rebuilt assembly metadata now includes `1.0.0+4ad570970654870c7e303a744f525dbaf521a6b9`, so new DLL hashes are recorded separately rather than claimed identical to the pre-commit binaries.

`NotebookHeaderViewTests` belongs to `ExclusiveWpfCollection` (`DisableParallelization=true`) and has `Workload=ExclusiveWpf`. The full gate would run it in the exclusive-first Desktop partition; A15 and this phase used filtered Desktop runs, not full gate partitions. Existing assembly concurrency remains two threads. Every parent computes its full parameterized display name, passes `ExactDisplayNameFilter` plus `expectedTestName`, and launches one isolated child with the recursion host variable. There is **no shared whole-class launch or multiplication of these failures**. Retained supervision intervals show sequential menu launches with cleanup before the next launch. There is no evidenced competing test child; xUnit exclusivity does not exclude other desktop applications.

### Original failure matrix

All identities have prefix `PersonalTradingJournal.Desktop.Tests.Notebook.NotebookHeaderViewTests.OverflowTriggersToggleThroughNativeMouseAndOwnedKeyboard`, followed by the exact arguments below. Each parent maps to a distinct, same-named child. Times are **UTC**; TRX duration is reported separately, not added to contained parent time.

| Exact arguments | Child interval / TRX duration | Launcher / testhost PID | Keyboard-readiness active HWND / foreground HWND |
| --- | --- | --- | --- |
| `(theme: "Light", width: 1280, dpi: 96)` | 07:43:49.729–07:43:54.907 / 5.244s | 22324 / 520 | `0xA0528` / `0x600F4` |
| `(theme: "Dark", width: 1280, dpi: 96)` | 07:43:58.398–07:44:03.238 / 4.894s | 12924 / 18396 | `0xE04A8` / `0x600F4` |
| `(theme: "Light", width: 480, dpi: 240)` | 07:44:06.451–07:44:11.336 / 4.932s | 18000 / 21752 | `0xA0436` / `0x600F4` |

Original child folders under `m2212a15-verified/focused/`, in table order:

- `notebook-menus-20261027-074346-9fbe202c58bf48519bb58f24bc95bddb/`
- `notebook-menus-20261027-074355-e1fcb59635fe4ca39574af592b92e33d/`
- `notebook-menus-20261027-074403-a3708f5da274416db3854e748b51f5d8/`

Each contains `notebook-menus.trx`, `keyboard-readiness.log`, `calendar-sta-phases.log`, `process-supervision.log`, stdout/stderr, and `menu-evidence/`. The original parent TRX is `focused/Yovko_DESKTOP-N2ONT35_2026-10-27_09_43_36_net10.0.trx`. Exact identities, stacks and paths are also indexed by the retained `failure-diagnostics.json` and this phase's `evidence.json`.

All three stop at the **first `MouseClick(NoteOverflow)`**, original `NotebookHeaderViewTests.Menus.cs:78`, with its assertion at original line **170**: “The intended control did not become hit-testable with settled popup animation and mouse-over.” Current locations after additive diagnostics are lines **98 / 199**. No mouse-down/up injection, menu-open callback or note command has occurred. Visibility/enabled, keyboard readiness, focus outline and three rendered dots had already passed. The failed condition for this **Button** is actually `settled && WindowFromPoint(point) == expectedHwnd`; `IsMouseOver` is required only for MenuItems. The original logs cannot distinguish opacity/animation from native HWND mismatch.

Original readiness logs show a loaded, visible, enabled target; local active window and native focus match the fixture; modifiers/buttons are released and fixture capture is null/native zero. The foreground HWND instead belongs to an unidentified window `0x600F4`. The null mouse source alone is not diagnostic: the original passing Dark/480 case also initially had a null mouse source. The original passing Dark/480 case and later passing Light/1280 diagnostic both had foreground equal to fixture HWND. This correlation does not identify the original foreground owner or focus-taking event. Original input-sequence logs are empty because input injection was never reached. Assertions, not 30-second watchdogs or 90-second process deadlines, ended these children; no hang dump was generated. All three STA cleanup traces reached dispatcher shutdown and supervisors confirmed process exit.

### A15 path review

- Initial activation/list/detail loading uses successful synthetic reads and does not publish a notification. The three retained closed-menu images were inspected: the banner is absent, the ellipsis is arranged and visibly focused, and no overlay covers it within the rendered WPF tree. These are WPF renders, not desktop screenshots; they cannot reveal another application's covering window.
- The notification is in a separate auto-sized grid row above the pane grid. When non-null it can move pane controls vertically; it is not a popup overlay. The helper recomputes `PointToScreen` on every readiness tick and resets the cursor before injection, so it does not rely solely on coordinates computed before its initial layout flush. A hypothetical layout change after the final sample is not evidence of one in these failures.
- `ShowNotification` alone starts the ten-second timer. No initial notification event means no pending timer. There is no notification-specific focus request; dismissal changes the notification property, not keyboard/capture state. Deactivation cancels the owned timer. Each test has its own Application/dispatcher/VM and teardown; no timer or WPF state is shared between these child processes.
- The menu toggle/ownership handlers run on Click/Opened, after the failing boundary. The first menu is closed and no prior gesture exists. A15 did not change these handlers. Retained mouse readiness correctly prevents injection into another HWND. `UiKeyboardProbe.Ready` intentionally establishes thread-local activation/focus using `SetActiveWindow`, **without forcing global foreground activation**; this is weaker than proving the screen point is unobstructed.

### Bounded instrumentation and reproduction

Missing evidence was the actual HWND/process at the target point, current foreground, cursor coordinates, layout state and notification/timer state at failed readiness. Only the menu test file gains opt-in `PTJ_NOTEBOOK_MENU_DIAGNOSTICS=1` observations before mouse readiness, at readiness completion, and on activation/notification changes. It records native/WPF state and owning HWND PIDs/classes, numeric DPI, bounds and timestamps. Subscriptions are removed in the existing finally block. No extra dispatcher pump, capture release, state assignment, focus request, deadline or scheduling change was added.

```powershell
pwsh -NoProfile -File TestResults/m2212a15-1-diagnosis/context.ps1 -OutputName before-context.json
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore
$env:PTJ_NOTEBOOK_MENU_DIAGNOSTICS='1'
pwsh -NoProfile -File TestResults/m2212a15-1-diagnosis/run.ps1 -Name bounded -Filter 'FullyQualifiedName~OverflowTriggersToggleThroughNativeMouseAndOwnedKeyboard&((DisplayName~Dark&DisplayName~1280)|(DisplayName~Light&DisplayName~480))'
pwsh -NoProfile -File TestResults/m2212a15-1-diagnosis/audit.ps1
pwsh -NoProfile -File TestResults/m2212a15-1-diagnosis/context.ps1 -OutputName after-context.json
```

Build: **exit 0, zero warnings/errors, 59.16s**, `build/output.log`. Run: **0/2 parents and 0/2 distinct matching children passed**, two failures, zero skips, **exit 1**, **22.454s** command wall time. Each affected child ran once. These two cases overlap the original A15 selection and are not new coverage totals. The already-passing Light/1280 diagnostic and passing Dark/480 case were not repeated. Literal-path-safe audit confirms one intended result per child and complete/distinct identities; its exit 1 reflects actual failures, not a parser failure. Parent TRX: `bounded/Yovko_DESKTOP-N2ONT35_2026-10-27_14_42_19_net10.0.trx`.

| Reproduction | Target point; expected fixture HWND | Actual native hit / foreground | Evidence folder under `bounded/` |
| --- | --- | --- | --- |
| Dark / 1280 / 96 | `(3434,701)`; `0x1805B4`, testhost PID 15048 | hit `0x8044A`, foreground `0x10582`, PID 16988 | `notebook-menus-20261027-124210-bc8b2da49d11455ab53a9fc708cc49ec` |
| Light / 480 / 240 | `(1556,995)`; `0xC0604`, testhost PID 18752 | same hit/foreground, PID 16988 | `notebook-menus-20261027-124219-e6f985535591427d9da9ed76302b01b3` |

Both readiness completions report **settled=True**, cursor at the requested coordinates, valid measure/arrange, WPF hit on the button's Path, no fixture capture, released left button and local focus on the intended Button. The native hit class is **Chrome_RenderWidgetHostHWND**, foreground class **Chrome_WidgetWin_1**; read-only process lookup confirms PID 16988 is **chrome**. Native hit testing therefore sees another application's window covering the intended point. In both, **notification=null**, expiry task **RanToCompletion**, banner **Collapsed/0**, menu **closed**, dispatcher alive. Point coordinates remained the same from pre-readiness to failure; no notification event occurred. The readiness wait itself is about three seconds; the rest of each child's roughly five seconds covers setup, renders and teardown, not a measured menu execution cost.

This **confirms foreign-window obstruction for the two new reproductions**, not a production menu or A15 timer/layout defect. It supports but cannot retrospectively prove the same obstruction for the original three: the historical HWND `0x600F4` was not identified at failure time. A current PID/window lookup must not be used to infer its historical owner or the event that took foreground.

Prerequisites were recorded in session **1 / WinSta0 / Default**, with cursor access and the executor's existing token; no UAC/token/desktop switching occurred. Cursor access and local WPF focus did not establish unobstructed native input. New launcher PIDs 4828 and 5028 exited sequentially; `after-context.json` reports zero active tests. No cleanup workaround or further reproduction was attempted.

### Responsibility, next correction and handoff

| Failure | Established responsibility / uncertainty | Smallest justified next action and verification |
| --- | --- | --- |
| Original Light/1280 | Local focus versus foreign foreground is established; exact original hit HWND/settled condition remains unknown. Earlier diagnostic pass is not a correction. | Keep diagnostics; use an unobstructed supported interactive test desktop for its next required focused verification. Do not rerun now to hunt for a failure. |
| Original Dark/1280 and Light/480 | Original mechanism remains incompletely recorded. New reproductions establish an external Chrome window at the intended point; the input prerequisite fails correctly. | Correct the execution prerequisite, not production or timeouts: arrange an unobstructed supported test desktop before a later authorized focused run of the affected matrix. Retain exact dispatch and hit-test checks; require the expected native HWND and full menu assertions. |
| Harness observability | Original failure text could not distinguish animation from HWND obstruction; thread-local active/focus is not global foreground. | The additive opt-in trace closes this evidence gap. No automatic foreground stealing, disabled assertion, relaxed wait or helper rewrite is justified. |

No production behavior correction is supported by this evidence. A new execution-context arrangement and an authorized focused pass would be required before acceptance; this diagnostic phase does not change desktop windows or claim that arrangement is complete. Exact phase inventory is **three modified files, no added tracked files**: `README.md`, `docs/notebook.md`, and `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookHeaderViewTests.Menus.cs` (37 additive diagnostic lines). Ignored scripts/manifests and evidence live only in the phase directory. Post-edit whitespace passed; **219 local documentation targets** passed with no missing paths (URLs and anchors excluded), using the phase copies of `check-whitespace.ps1` and `check-doc-links.ps1`. Original A15 evidence remains historical and unchanged.

**A15.1 diagnosis is complete within its bounds; A15 regression acceptance, A16–A17 and M22.12B remain pending.** No complete gate, production/provider access, publication, workflow triggering or speculative behavior fix occurred. Human interaction/Narrator, native journal workflow/restart and matching hosted input compatibility remain unverified.

## M22.12A15 — Top Notebook notifications

27 October 2026 (Europe/Kiev). One agent. Actual initial baseline: **clean develop / `e42d8c9fc744f0d54dd35b3baafb9ad4d6986bda`**, worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. The reported A14 pending changes had already been committed after `cd26864f`. On resumption, the in-progress A15 edits and original artifacts were preserved. HEAD is unchanged. Repository/ancestor inspection found no AGENTS.md. Tested production source is HEAD plus the A15 source changes below, not HEAD alone. Evidence root: **`TestResults/m2212a15-verified/`**.

### Notification contract and message inventory

One wrapping info box sits below the primary header and above Organization/Notes/Content switching. It participates in layout, does not overlay controls, and remains visible whichever narrow panel is selected. Existing theme brushes provide green Success, blue Information, amber Warning and red Error; an icon and written severity distinguish them without color. The close button has an accessible name, tooltip and existing keyboard-focus style. Publishing or automatically dismissing feedback does not request focus.

| Source | A15 presentation and severity |
| --- | --- |
| Verified note creation, move/pin, lifecycle, duplication/deletion, folder/tag catalog and assignment/link completion | Top Success event after the existing authoritative completion; deletion with retained cleanup work is Warning |
| Wait-before-leaving guidance, saved note outside the filter/page, catalog changed outside the current context | Top Information |
| Operation cancellation, form dismissal during a possible committed write, conflicts/missing targets, saved note whose list refresh failed | Top Warning; cancellation never claims rollback |
| General read/write failure, unverified operation result, image admission/size/codec failure | Top Error; image-specific detail and retention caveats are retained |
| Folder creation/rename and tag name validation/persistence messages | Retained beside their field; validation-only feedback does not need a transient event |
| `DraftError`, new `SaveFeedback`, `SaveState`, `CompactSaveState`, `RecoveryMessage` and recovery actions | Persistent contextual state. Save errors retain their explanation and retry/Discard controls after banner expiry or dismissal. Checkpoint durability is not canonical-save success |
| `ContentMessage`, unavailable/read-only explanation, target/link picker errors, editor/link/image inline feedback | Retained contextual loading, capability or field/selection diagnostics; no blanket subscription turns them into transient events |
| Confirmations and active-operation cancellation | Remain interactive; not timed away |

The previous general `Message` TextBlock and its duplicate inside the move picker are removed. `Message` remains available to existing command/error coordination; it is not the timer trigger. Explicit operation events choose severity. Routine successful manual/automatic saves keep compact status and do not produce a success toast. Save validation and canonical failure details clear only when the draft is replaced or a canonical save succeeds, not on notification dismissal.

The existing injected `TimeProvider` drives one asynchronous ten-second delay. A new event, including the same text from a distinct operation, cancels and invalidates its predecessor and starts a full interval. Binding reads/refreshes do not start events. There is no queue. Identity checks prevent a queued old expiry from clearing a replacement; timers are cancelled on accepted note departure, deactivation and disposal, then disposed by their completion. The continuation retains the UI synchronization context; a busy dispatcher may process expiry when it next becomes available. No blocking wait, focus request, persistence mutation or recovery acknowledgement is performed by dismissal. Catch-path feedback now checks activation/selection ownership; late inactive completions cannot notify a newly opened workspace. Existing operation ownership, autosave timing and expected-version writes are retained.

### Commands and focused results

```powershell
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore
pwsh -NoProfile -File TestResults/m2212a15-verified/run.ps1 -Name focused -Filter 'FullyQualifiedName~NotebookNotificationTests|FullyQualifiedName~NotebookHeaderViewTests|FullyQualifiedName~NotebookAutosaveTests|FullyQualifiedName~NotebookEditingTests|FullyQualifiedName~NotebookFolderCreationTests|FullyQualifiedName~NotebookFolderActionTests|FullyQualifiedName~NotebookTagFormTests|FullyQualifiedName~NotebookTagTests|FullyQualifiedName~NotebookNoteActionsTests|FullyQualifiedName~NotebookImageCoordinationTests|FullyQualifiedName~NotebookCopyDeleteTests|FullyQualifiedName~NotebookLifecycleTests|FullyQualifiedName~NotebookLinkTests|FullyQualifiedName~NotebookWorkspaceTests|FullyQualifiedName~ReleaseSchedulingTests'
# After adding only hit-test diagnostics, rebuild the same Release project:
pwsh -NoProfile -File TestResults/m2212a15-verified/run.ps1 -Name hit-test-diagnostic -Filter 'FullyQualifiedName~OverflowTriggersToggleThroughNativeMouseAndOwnedKeyboard&DisplayName~Light&DisplayName~1280'
# Correct severity-example text in the synthetic palette fixture, then regenerate affected evidence:
pwsh -NoProfile -File TestResults/m2212a15-verified/run.ps1 -Name final-notification-renders -Build -Filter 'FullyQualifiedName~TopNotificationsWrapAcrossPanelsAndDismissWithoutResolvingFailedSave'
pwsh -NoProfile -File TestResults/m2212a15-verified/audit.ps1
pwsh -NoProfile -File TestResults/m2212a15-verified/check-whitespace.ps1
pwsh -NoProfile -File TestResults/m2212a15-verified/check-doc-links.ps1
```

The runner sets `PTJ_TEST_RESULTS_DIRECTORY` and uses `dotnet test ... -c Release --no-build --no-restore --filter $Filter --logger trx --results-directory $RunDirectory`. Build logs: `build/output.log` (**74.46s**), `diagnostic-build/output.log` (**10.38s**) and `final-notification-renders/build.log` (**22.64s**); all exit **0**, zero warnings/errors. The second build adds only test diagnostics; the third changes only synthetic capture text. Production binaries include the same A15 source throughout. No restore, EF-model change or complete gate was needed for this Desktop-only change.

| Run | Parents passed/total | Separate children passed/total | Failures / skips | Exit | Command wall time excluding build |
| --- | ---: | ---: | --- | ---: | ---: |
| `focused` | 183/186 | 7/10 | 3 parents mapping to 3 children / 0 | 1 | 82.763s |
| `hit-test-diagnostic` | 1/1 | 1/1 | 0 / 0 | 0 | 10.869s |
| `final-notification-renders` | 4/4 | 4/4 | 0 / 0 | 0 | 32.542s |

There are **186 distinct parents and ten distinct children**, across **191 parent and 15 child executions**. The diagnostic case and four regenerated visual cases overlap the initial selection; parent and child totals are never added together. Every child TRX contains exactly its one intended parent identity, with no missing, duplicate or skipped result. The audit deliberately remains nonzero because retained failures remain unresolved; its latest-result projection does not erase the original three failures.

All **12 new timing/state cases** passed: four severities, 9,999/10,000ms boundary, replacement with identical text, queued expiry ownership, manual dismissal, failed/conflicting canonical save with retained recovery, field validation, silent routine autosave, operation success/failure/cancellation mapping, deactivation/disposal/late completion and accepted versus rejected note departure. The **four new UI parents and four matching children** passed in both runs, covering binding refresh without timer restart, all narrow panels, theme brushes, wrapped text, close-button geometry/accessibility, native Space dismissal, persistent failed-save controls and focus retained through timed dismissal. The tests reuse `NotebookClock`; no repeated real ten-second waits were added.

Other original focused counts, all passing except the menu family described below: autosave **20**, editing **15**, folder creation **17**, folder actions **14**, tag form **14**, tags **7**, note action ownership **7**, image coordination **6**, copy/delete **8**, lifecycle/templates **18**, links **15**, workspace/query **22**, scheduling **1**, header creation/Refresh/cancellation **2**. Menu family **1/4**, notification logic **12/12**, notification UI **4/4** complete the 186-parent count. These selections exercise the changed feedback producers and retained draft protections; they are not a repeat of every historical Notebook suite.

### Retained failure diagnosis and acceptance limit

All three original failures are in `NotebookHeaderViewTests.OverflowTriggersToggleThroughNativeMouseAndOwnedKeyboard`, before the **first** `MouseClick(NoteOverflow)` injects mouse input. Each fails the unchanged three-second readiness assertion: **“The intended control did not become hit-testable with settled popup animation and mouse-over.”** Keyboard focus and the control's focus visual had already passed; no menu command had run. Closed-control renders show an arranged, visible control. The original log does not record the mismatching HWND or animation condition, so it cannot establish the cause or attribute it to a notification/layout change.

Paths below are under `focused/`; each folder retains `notebook-menus.trx`, stdout/stderr, supervision evidence, `menu-evidence/input-sequence.log` and captures:

| Parent and distinct child parameters | Evidence folder | Result |
| --- | --- | --- |
| Light / 1280 / 96 | `notebook-menus-20261027-074346-9fbe202c58bf48519bb58f24bc95bddb` | Failed readiness before first click |
| Dark / 1280 / 96 | `notebook-menus-20261027-074355-e1fcb59635fe4ca39574af592b92e33d` | Same observed boundary |
| Light / 480 / 240 | `notebook-menus-20261027-074403-a3708f5da274416db3854e748b51f5d8` | Same observed boundary |

Dark / 480 / 240 passed the complete existing menu matrix. A narrowly scoped diagnostics-only change now records target/screen coordinates, expected/actual HWND, hit window title, settled animation, mouse-over, activation, window/desktop bounds and DPI at readiness completion. One Light / 1280 / 96 diagnostic reproduction passed. Its first click had point **3434,701**, matching HWND **4588706**, settled animation, mouse-over and an active fixture. This is evidence for that later execution only: it does **not** identify the original blocking window/event, prove the three original failures harmless, or justify increasing deadlines. No behavior fix, forced capture/focus, timeout change or retry-until-green loop was applied. **A15 implementation and its notification-specific verification are complete; full A15 focused regression acceptance remains pending diagnosis of the retained menu-input failures.**

`test-audit.json`, `results.json` and `failure-diagnostics.json` retain exact identities and original error stacks. Original parent TRX is `focused/Yovko_DESKTOP-N2ONT35_2026-10-27_09_43_36_net10.0.trx`. Supported native fixtures used **session 1 / WinSta0 / Default**, cursor access and the executor's existing administrator token, as recorded in `test-context.json`; no UAC/token/desktop switching was requested. `after-context.json` confirms no active test processes. Native mouse readiness remains a narrower unverified prerequisite than cursor access alone.

### Inspected captures and exact inventory

Final captures use production resources, synthetic notes and the final passing four-row notification fixture. `renders.json` maps copied paths to original child artifacts and SHA-256 hashes. The severity palette samples intentionally publish explicit synthetic events; the failed-save captures invoke the actual save command with a synthetic storage failure. Original intermediate palette captures are retained and superseded by the final examples.

Representative final images inspected under `renders/`: `Light-1280-96-success.png`, `Dark-1280-96-information.png`, `Light-480-240-warning.png`, `Dark-480-240-error.png`, `Light-480-240-error.png`, `Light-1280-96-failed-save.png`, `Dark-480-240-dismissed-save-error.png` and `Dark-1280-96-warning.png`. The banner wraps without clipping or covering the pane controls; persistent Save paused/error/Save/Discard remain after dismissal. No blocking notification visual defect was found. Application shell, Saved locally placement and tag alignment are deliberately unchanged.

Exact A15 inventory: **14 modified files and three added files**, all relative to the clean baseline; there were no pre-existing uncommitted A14 edits:

- `README.md`, `docs/notebook.md` — behavior, evidence, retained failure and boundaries.
- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.cs` — initialization, accepted-selection/deactivation expiry invalidation, general feedback and catch ownership.
- Same directory: `NotebookWorkspaceViewModel.CopyDelete.cs`, `NotebookWorkspaceViewModel.FolderActions.cs`, `NotebookWorkspaceViewModel.Folders.cs`, `NotebookWorkspaceViewModel.Lifecycle.cs`, `NotebookWorkspaceViewModel.Links.cs`, `NotebookWorkspaceViewModel.TagForm.cs`, `NotebookWorkspaceViewModel.Tags.cs` — explicit existing operation-feedback events/severities and guarded completion.
- Same directory: `NotebookWorkspaceViewModel.Editing.cs` — persistent save feedback, validation, silent save success and owned refresh failure.
- Same directory: `NotebookWorkspaceViewModel.Images.cs` — specific image failure/retention feedback; a nominal success without verified returned content is not reported as success.
- **Added** in the same directory: `NotebookWorkspaceViewModel.Notifications.cs` — notification record, explicit publication, replacement-safe delay and dismissal.
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml` — top banner, accessible close and persistent save detail; duplicate general message binding removed.
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookHeaderViewTests.Menus.cs` — additive native readiness diagnostics only; existing conditions and deadlines unchanged.
- **Added** in the test directory: `NotebookNotificationTests.cs`, `NotebookHeaderViewTests.Notifications.cs` — deterministic timing/state tests and four isolated WPF interaction/render cases.

`source-provenance.json` and `binaries.json` record current source/binary hashes; ignored evidence remains in this phase directory. Tracked/new-file whitespace passed, and **218 local documentation targets** passed with zero missing paths (URL and anchor validation excluded); these checks are separate from runtime acceptance. No persistence/schema, archive/export format, CI, scheduling, assertion or timeout change was made. Human keyboard/Narrator assessment, native journal workflows/restart and matching hosted execution remain unverified. **A16–A17, M22.12B and overall M22.12 acceptance remain pending.** No production/provider access, publication or workflow triggering occurred.

## M22.12A14 — Workspace Refresh and Trash placement

26 October 2026 (Europe/Kiev). One agent. Actual baseline: **clean develop / `cd26864fef756ca037fab32921b3c7d29d39e43b`**, worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. A13's six modified files and added helper had been committed after the reported `613dbac7` HEAD. HEAD remains unchanged; no existing work or evidence was discarded. Repository/ancestor inspection found no AGENTS.md. Tested source is this HEAD plus the eight source/test changes below; documentation was edited afterward. Evidence root: **`TestResults/m2212a14-verified/`**.

### Interaction and compatibility

- The removed Workspace popup contained exactly **Trash and Refresh**. Both remain directly accessible: WORKSPACE is now **All Notes, Pinned, Trash**, and its top-right control is a 36-DIP teal Refresh icon. The application geometry dictionary gains `PtjIconRefresh`; Trash uses the existing `PtjIconDelete`. Refresh retains a tooltip, accessible name, keyboard focus outline and the established hover/pressed/disabled theme styling.
- Refresh binds directly to the existing `RefreshCommand`. No command/model logic changed: dirty drafts use Save/Discard/Cancel consent, an in-flight refresh disables repeated execution, writes disable Refresh, and the contextual Cancel operation control still stops a pending request without claiming rollback. Existing generation/ownership protection prevents stale results from replacing newer state.
- Trash enters through the same `SelectPrimaryScope` path as other rows. The accepted scope is synchronized back into the correct list after refresh or rejected draft navigation. Its label, count and query are the existing Trash scope. Folder deletion still preserves notes and falls back to All Notes. Archived and Unfiled navigation remain absent; non-Trash legacy archived/folderless notes retain A9 discovery/read-only semantics.
- The note menu preserves same-trigger toggling, Escape/outside dismissal, captured commands, lifecycle availability and stale-selection rejection. Removing the Workspace menu also removes its obsolete filter/open/selection handlers. Note-menu opening explicitly closes a tag popup; tag opening already closes the note menu. Global tag management, assignment chips, Restore and confirmed Trash-only permanent deletion remain intact.

### Focused verification

```powershell
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore
pwsh -NoProfile -File TestResults/m2212a14-verified/run.ps1 -Name focused -Filter 'FullyQualifiedName~NotebookHeaderViewTests|(FullyQualifiedName~NotebookViewTests&DisplayName~Light&DisplayName~1280)|FullyQualifiedName~InlineTagCreationAndCapturedManagementUseRealControls|((FullyQualifiedName~NotebookPresentationViewTests|FullyQualifiedName~NotebookLifecycleViewTests)&((DisplayName~Light&DisplayName~1280)|(DisplayName~Dark&DisplayName~480)))|FullyQualifiedName~SelectionAndRefreshUseSaveDiscardCancel|FullyQualifiedName~NotebookLegacyNavigationTests|FullyQualifiedName~NotebookFolderActionTests|FullyQualifiedName~NotebookLifecycleTests|FullyQualifiedName~NotebookCopyDeleteTests|FullyQualifiedName~NotebookWorkspaceTests|FullyQualifiedName~ReleaseSchedulingTests'
```

The runner sets `PTJ_TEST_RESULTS_DIRECTORY`, then invokes `dotnet test` on the same Desktop test project with `-c Release --no-build --no-restore --filter $Filter --logger trx --results-directory $RunDirectory`. `build/output.log` records **zero warnings/errors**, exit **0**, **58.99s** build-reported duration. The single focused run passed **81/81 parent cases**, zero failures/skips, exit **0**, **134.693s** command wall time excluding build. No test failed or was retried.

| Selection | Parent cases | Isolated children |
| --- | ---: | ---: |
| Header creation/Refresh/cancellation + native note-menu/Refresh matrix | 6 | 6 |
| Workspace row selection, refresh, count/icon and rejected dirty-navigation matrix | 1 | 4 |
| Actual tag creation/captured global management | 2 | 2 |
| Populated presentation and retained editor/tag behavior | 2 | 2 |
| Actual note lifecycle/menu controls, Restore and permanent deletion | 2 | 2 |
| Refresh Save/Discard/Cancel consent | 3 | 0 |
| Legacy navigation normalization | 2 | 0 |
| Folder actions, retention/fallback and asynchronous ownership | 14 | 0 |
| Lifecycle coordination and templates | 18 | 0 |
| Copy/delete coordination | 8 | 0 |
| Workspace query, paging, cancellation and stale-result protection | 22 | 0 |
| Release scheduling membership | 1 | 0 |
| **Total** | **81** | **16** |

The 13 UI parents comprise twelve exact one-child selections plus one existing shared four-row workspace suite. Parent and child counts are separate, not 97 independent top-level tests. `audit.ps1`, `test-audit.json` and `results.json` reconcile literal-path-safe TRX counters, exact child identities, the shared Light/Dark × 1280/96 and 480/240 matrix, and absence of skipped, missing or duplicate results. There is one focused run; the OR selection reports each parent once. Coverage overlapping historical A1–A13 evidence is not added to these totals.

New/updated actual-control checks assert removal of the Workspace ellipsis/popup; Refresh command binding, native click/Space execution exactly once, visible focus and disabled style; dirty Cancel retaining input and Save committing once; active Refresh cancellation/re-enablement and disabled Refresh during creation; exact scope order/count/icon; Trash selection and refresh highlighting; rejected dirty Trash navigation restoring folder selection. Existing note-menu open/close/reopen/Escape/outside, single Duplicate and stale Trash-command checks remain. All existing assertions unrelated to the removed popup, process isolation, collection/trait scheduling, 90-second child process deadlines and 30-second watchdogs remain intact.

The initial restricted process-inspection call denied CIM access; the authorized supported executor then recorded the actual context and no active tests before launch. Native fixtures ran in **session 1 / WinSta0 / Default**, with cursor access and the executor's existing administrator token; no UAC/token/desktop switching was requested. `before-context.json` and `after-context.json` record prerequisites and no active test processes after cleanup. These automated native-input fixtures are not M22.12B journal acceptance. No complete gate ran.

### Inspected current captures

All twelve images below were inspected. `renders.json` records copied and original paths, SHA-256 hashes, passing child names and intervals; every original capture timestamp falls inside its owning child interval. Raw captures, TRX and supervision/input logs remain retained. Paths below are relative to the phase root.

- `renders/workspace/Light-1280-96-navigation.png` and `Dark-1280-96-navigation.png` — ordered scopes, counts, icons, Refresh and long folders in both themes.
- `renders/workspace/Dark-480-240-navigation.png` — narrow/high-DPI Organization pane with active Refresh and all three scopes.
- `renders/workspace/Light-480-240-trash-navigation.png` and `Dark-480-240-trash-navigation.png` — Trash heading, retained note/date and query count in the Notes pane.
- `renders/header/Light-1280-96-refresh.png` — saved draft after guarded Refresh, retained note menu and scope controls.
- `renders/header/Dark-480-240-busy.png` — disabled Refresh, truthful operation feedback and contextual cancellation.
- `renders/menu/Light-480-240-workspace-refresh-focus.png` — visible Refresh keyboard outline; the transparent top band is the fixture host's external-button offset in this attached-view diagnostic, not application chrome.
- `renders/menu/Dark-480-240-NoteOverflow-open-menu.png` — retained note actions and correct disabled entries.
- `renders/tags/Light-1280-96-management-menu.png` and `Dark-480-240-management-menu.png` — readable Rename/Delete globally actions.
- `renders/presentation/Light-1280-96-organization.png` — populated combined workspace with rich content, image, tags and linked records.

No blocking A14 visual defect was found. Narrow layouts retain the established Organization/Notes/Content switching. Tests verify accepted Trash selection even when narrow navigation switches to Notes; no composite was made to show both panes simultaneously. The application shell, notification timing, Saved locally placement and tag alignment are unchanged.

### Exact task inventory and handoff

Final A14 inventory is **ten modified files, no added tracked files**. The baseline was clean, so none are pre-existing uncommitted A13 changes:

- `README.md` and `docs/notebook.md` — current behavior, evidence and phase boundary.
- `src/PersonalTradingJournal.Desktop/Resources/Icons.xaml` — additive Refresh geometry, used only by the new Notebook control.
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml` — direct Refresh, Trash row icon, removal of Workspace popup resources.
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml.cs` — primary Trash filtering/highlighting; obsolete popup handlers removed.
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.Menus.cs` — remaining note-menu coordination without the removed trigger.
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookHeaderViewTests.cs` — real Refresh binding, draft consent, busy/cancellation assertions.
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookHeaderViewTests.Menus.cs` — retained note-menu native matrix and direct Refresh/focus checks.
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookViewTests.cs` — scope order, Trash icon/count/selection and refresh/guard regressions.
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookPresentationViewTests.cs` — current Workspace control expectations; note/tag ellipsis checks retained.

`source-provenance.json`, `binaries.json` and `summary.json` bind the results to HEAD plus these uncommitted source changes. Ignored scripts/manifests and durable artifacts remain under the phase root. Post-documentation `check-whitespace.ps1` and `check-doc-links.ps1` passed; the latter checks local Markdown target existence in README, notebook and backup/restore documentation, excluding URLs and anchors. No schema, persistence, export/archive format, EF model, CI or timeout changed.

**A14 is complete; A15–A17, postponed M22.12B and overall M22.12 acceptance remain pending.** Human keyboard/Narrator assessment, native journal workflow/restart, real clipboard/picker/viewer/codec and matching hosted timing/input compatibility remain unverified. No provider/production access, publication or workflow triggering occurred.

## M22.12A13 — Final Notebook UI correction and integrated review

26 October 2026 (Europe/Kiev). One agent. Actual baseline and final HEAD: **develop / `613dbac7a01af305fd6caacaadea4d1d62151df3`**, worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. The baseline was clean: A12 had been committed after the reported `7e2a188` baseline. No unrelated work or historical artifacts were discarded. Repository/ancestor inspection found no AGENTS.md. Tested binaries include this HEAD plus the five source/test files listed below; documentation was edited afterward. Evidence root throughout this section: **`TestResults/m2212a13-verified/`**. `source-provenance.json`, `binaries.json`, `results.json` and `summary.json` record exact hashes, identities and outcomes.

### Findings and smallest correction

The retained overflow-dot diagnosis was inaccurate, not an established WPF rendering defect. Original A8 `Light-480-240-archived.png` and A9 `Light-480-240-legacy-folderless.png` both visibly contain **three dots**. `retained-dot-audit.json` records their original paths/hashes and separated bright pixel runs starting at x=1091, 1108 and 1126 on row 416 of each original 1200×2250 image. The shared `PtjIconMore` is vector geometry with three figures, not a font glyph. The note Path's arranged size is 18×3.857 DIPs, with bounds x=9, y=16.071, width=18, height=3.857 inside its 36×36 button; the absence of explicit Width/Height did not clip it. No evidence supported changing geometry, sizing, alignment or foreground. This supersedes the earlier outstanding “single dot” warning; it does not claim to have repaired a nonexistent clipping cause.

The new evidence helper checks actual arranged bounds and crops each button from its rendered root, requiring three separated contrasting pixel runs. **52 passing checks** cover both ellipses in focus/open/hover/disabled states and the three synthetic tag affordances across Light/Dark, 1280/96 DPI and 480/240 DPI. Existing accessible names, teal states, focus, hit targets, action ownership and menu behavior remain unchanged.

A separate blocking visual defect was demonstrated in the saving/recovery captures: disabling the Notebook's lists produced white backgrounds in Dark mode. WPF's Aero2 ListBox template uses a static white `ListBox.Disabled.Background`, so overriding `SystemColors.ControlBrushKey` did not affect it. The [WPF ListBox template source](https://raw.githubusercontent.com/dotnet/wpf/main/src/Microsoft.DotNet.Wpf/src/Themes/XAML/ListBox.xaml) corroborates the observed runtime behavior. A local `NotebookList` template now uses `PtjSurfaceBrush`/`PtjBorderBrush` when disabled, preserving the standard Border/ScrollViewer/ItemsPresenter structure and grouping condition. It applies only to ScopeList, FolderScopeList and NoteList. Item styles, recycling settings, selection commands, persistence and all menu resources are unchanged. Exact theme-color assertions and final saving captures verify the correction in both themes and layouts.

### Commands, retained failures and focused results

The retained runner executes the following commands, setting `PTJ_TEST_RESULTS_DIRECTORY` to the named run directory. Each run's `result.json` records its exact filter; `run.ps1` retains the original invocation mechanism.

```powershell
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore
dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter $Filter --logger trx --results-directory $RunDirectory
```

| Run directory | Parents passed/total | Children passed/total | Test exit | Test elapsed | Finding |
| --- | --- | --- | --- | --- | --- |
| `baseline-dot` | 0/1 | 0/1 | 1 | 12.673s | Initial diagnostic rendered an attached child with an ancestor offset into a small target: transparent crop, zero detected runs |
| `integrated` | 5/9 | 4/8 | 1 | 72.883s | Presentation and scheduling passed; four menu crops still used the nested view instead of the top-level host |
| `menus-final` | 4/4 | 4/4 | 0 | 38.310s | Correct top-level capture; native menu matrix and all retained assertions passed |
| `visual-gaps` | 3/3 | 6/6 | 0 | 29.947s | Lifecycle, folder and shared autosave cases passed; image inspection exposed disabled Dark white surfaces |
| `disabled-lists-final` (historical name) | 0/1 | 1/4 | 1 | 11.385s | First resource override ineffective; Dark expected #FF2C2236, actual #FFFFFFFF. Narrow Light assertion also inspected an unrealized collapsed pane |
| `corrected-lists` | 7/7 | 13/13 | 0 | 69.991s | Local template correction; assertions reveal the relevant pane before inspection; final affected UI and workspace checks passed |

All runs had zero skipped cases. Original failed TRX, output, supervision and images are retained, not replaced. Changes between runs addressed the demonstrated crop coordinate, disabled-color and unrealized-pane causes; no assertion, input path or deadline was weakened. A manifest-generation check initially lost subsecond timestamps through PowerShell's automatic JSON date conversion; preserving timestamp strings fixed that reporting check without rerunning tests.

All five Release builds passed with zero warnings/errors. The final `corrected-lists/build.log` reports **22.80s**, separate from tests. Its exact selection was:

```text
FullyQualifiedName~NotebookPresentationViewTests|(FullyQualifiedName~NotebookAutosaveViewTests&DisplayName~Light&DisplayName~1280)|(FullyQualifiedName~NotebookViewTests&DisplayName~Light&DisplayName~1280)|(FullyQualifiedName~NotebookFolderActionsViewTests&DisplayName~Dark)
```

The native menu selection was `FullyQualifiedName~OverflowTriggersToggleThroughNativeMouseAndOwnedKeyboard`. It retains mouse first/second/subsequent click toggling, outside/Escape dismissal, keyboard activation/focus return, menu switching, exactly one Refresh/Duplicate and stale Trash-command rejection. Its four rows are Light/Dark × 1280/96 and 480/240. The later list-chrome change does not affect the tested menu handlers, resources or standalone icon/menu captures, so this passing selection was not repeated.

`audit.ps1` reads TRX with literal paths, reconciles counters and unique identities, and verifies exact children match parent names. The two existing shared workspace/autosave launches each contain their complete four-row matrix; they are not four additional parent tests. Across the retained correction runs there were **25 parent executions and 36 child executions**. After removing overlaps and using each identity's final applicable result, evidence is **13 distinct passing parents and 18 distinct passing children**, zero unresolved failures/skips. This comprises 12 UI parents plus one scheduling regression; ten exact children plus two four-case shared suites. These counts do not add historical A1–A12 suites. The final affected run contains seven parents: four presentation, one folder, one workspace and one autosave; its thirteen children are counted separately.

No test process was active before launch or after supervised cleanup. `before-context.json`, `resumed-context.json` and `after-context.json` record session 1 / WinSta0 / Default, interactive cursor access and the already authorized executor's existing administrator token, without UAC/token/desktop switching. A temporary automatic-review usage-limit denial did not execute the attempted command; after the user's continuation, prerequisites were checked before resuming. Isolation, exclusive membership, exact dispatch, 90-second process limits and 30-second watchdogs remain intact. No complete Release gate ran.

### Integrated requirement-to-evidence matrix

“Passed” below means automated scope only. Current evidence includes the final presentation/workspace/autosave/folder captures and tests above. Reused logic evidence is identified explicitly; it is not treated as a newly executed suite.

| Requirement | Current evidence | Status / remaining issue |
| --- | --- | --- |
| WORKSPACE only All Notes/Pinned with icons | Final populated/organization captures; workspace child matrix | Passed |
| Separate folders, counts, real-folder-only rename/delete | Final organization and Dark long-heading/validation captures; folder child; unchanged A9 folder-action logic | Passed |
| New folder, conditional Add, explicit Cancel | Final empty/populated creation captures; retained A10/A11 actual-control and guarded-creation results | Passed |
| New tag, conditional Add, explicit Cancel | Same final combined forms; retained A11 tag-form results | Passed |
| Rename/global deletion versus assignment removal | Current tag affordance checks; two unchanged A11 popup captures; A11 captured-target/confirmation/SQLite recovery tests; current presentation assignment removal | Passed |
| Archived/Unfiled navigation, archive actions and help absent | Current organization and Notebook/note menu captures; lifecycle child | Passed |
| Archived/folderless discovery, consistent counts/filter/paging | Unchanged A9 SQL/query/backup tests and source hashes; current legacy-folderless lifecycle capture | Passed |
| Archived read-only with editable duplication | Lifecycle child asserts capabilities and duplication; legacy-folderless explanation | Passed |
| Folder deletion retains notes and selects All Notes | Unchanged A9 folder-action/legacy-navigation and persistence contract evidence; current workspace selection matrix | Passed |
| Split blank New note and atomic templates; contextual Cancel | Current lifecycle creation assertions and header captures; unchanged A4/A9 command/duplicate/guard tests | Passed |
| Compact title, pin, truthful status, owned note menu/contextual actions | Current lifecycle, menu and autosave children; long-title error and recovered draft captures | Passed |
| Compact formatting, selection, chips, bounded readable rows | Final presentation matrix: formatting/checklist/link, cancellation/undo and assignment assertions; populated/rows captures | Passed |
| English MMM dd, yyyy, Updated timestamp, New York first | Unchanged A12 18 date cases and two SQL cases; current presentation exact date assertions | Passed |
| Image/caption and Trade/Trading Setup links reachable | Final combined populated and scrolled linked-items viewports; existing presentation assertions | Passed |
| Autosave/recovery and stale-action protection | Final autosave matrix; menu stale command assertion; unchanged A5/A9/A11 ownership/recovery logic evidence | Passed |
| Three visible overflow dots and consistent disabled contrast | Original pixel audit; 52 current dot checks; final disabled-list assertions and saving captures | Passed; historical dot finding superseded |

Reuse boundaries are recorded in `reuse-provenance.json`, `a12-production-diff.txt` and `a9-persistence-diff.txt`. A11 tag/form/owned-action code is unchanged; the A11-to-A12 production changes were the date helper and date bindings. A9 Application/Infrastructure/Domain code has no subsequent diff. A12 date helper, workspace date bindings and date tests match recorded hashes. Changed XAML/presentation test hashes are not represented as identical: final renders and affected view cases were regenerated. Prior A9 (50 Infrastructure/29 Application), A11 (99 distinct Desktop/eight Infrastructure) and A12 focused results are reused only for those unchanged contracts, not added to A13 totals.

### Inspected captures and reference comparison

The supplied `C:/Users/Yovko/Downloads/the target design reference.png` was opened and compared directly. **Thirty accepted captures** were inspected; `renders.json` lists every exact path, original location, SHA-256, passing child identity/interval and reuse boundary. Each new capture's original timestamp falls within its passing child interval. No composite or artificial replacement UI was generated.

| Evidence under this phase root | Inspected files/states |
| --- | --- |
| `renders/presentation-final/` | Light/Dark 1280-96 populated; Light-480-240 populated, rows and creation-populated; Dark-480-240 linked-items, organization and creation-empty (eight) |
| `renders/folders-final/` | Dark-480-240 long-heading and validation (two) |
| `renders/recovery-corrected/` | Dark-1280-96 saving; Dark-480-240 saving-ScopeList; Light-480-240 saving-NoteList; Light-1280-96 recovered (four) |
| `renders/lifecycle/` | Light-480-240 empty, save-error and legacy-folderless (three; unchanged Content pane) |
| `renders/menus/` | Light-480-240 NotebookOverflow-open-menu; Dark-1280-96 NoteOverflow-open-menu (two; unchanged popup resources) |
| `renders/dots/` | Light-480-240 both ellipses in focus/open/hover/disabled; Dark-480-240 NoteOverflow-open (nine actual rendered-control crops) |
| `renders/reused-tag-menus/` | Light-1280-96 and Dark-480-240 management-menu (two unchanged A11 standalone popups) |

The coherent synthetic populated fixture includes rich text/checklist, a pinned note in Market research, assigned tags including a long name, an illustrative chart/caption and Trade/Trading Setup links. The Updated fixture timestamp is 2024-10-05 03:30 UTC, displayed as Oct 04, 2024 in New York. Separate scrolled viewports retain readability; narrow layouts expose Organization/Notes/Content rather than shrinking all panes into one image. Long folder names leave actions reachable; inline errors, explicit Add/Cancel, empty selection, recovered unsaved draft and paused-save explanations remain visible. Final Dark pending lists retain theme surfaces. No blocking visual defect remains in the inspected scope.

Deliberate differences remain: the existing application shell/themes, responsive pane switching, checkbox sidebar filters, supported checklist representation and explicit image/linked-record/recovery controls. The illustrative reference's unsupported callout/AI features are not fabricated. This is visual/automated interaction acceptance, not a pixel-identical redesign.

### Exact inventory and acceptance boundary

The clean baseline means there are no pre-existing uncommitted A12 files to subtract. Final A13 inventory: **six modified files and one added helper**:

- `README.md` — current acceptance summary; earlier records preserved.
- `docs/notebook.md` — this diagnosis, integrated matrix, commands and limitations.
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml` — theme-safe disabled chrome for the three Notebook lists only.
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookAutosaveViewTests.cs` — disabled-surface assertions and narrow saving viewports.
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookHeaderViewTests.Menus.cs` — four-row native menu matrix and actual dot-state checks.
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookPresentationViewTests.cs` — combined forms/organization captures and tag/ellipsis evidence.
- Added `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookOverflowEvidence.cs` — bounded geometry and actual rendered-pixel validation.

Ignored diagnostic scripts, raw logs/TRX, original failures, copies and manifests stay under the phase evidence root. `pwsh -NoProfile -File TestResults/m2212a13-verified/audit.ps1` and `finalize-evidence.ps1` validate identities and provenance. Post-documentation `check-whitespace.ps1` and `check-doc-links.ps1` passed; the latter checks local target existence in README, this notebook and backup/restore documentation, excluding web URLs and fragment validation. No schema/format, persistence, EF model, CI, timeout, scheduling or general reclamation changes were made, so no new EF migration check or complete gate was needed for this view-only correction.

**A13 and integrated A1–A13 automated UI acceptance are complete. M22.12B and overall M22.12 acceptance remain pending.** Automated fixture native input is not human keyboard/Narrator assessment, real clipboard/picker/viewer or installed-codec acceptance, native journal workflow/restart, or matching hosted timing/input compatibility. Those remain unverified, and historical input/focus causes are not claimed resolved. No publication, provider access or workflow triggering occurred.

## M22.12A12 — Consistent Notebook dates

26 October 2026 (Europe/Kiev). One agent; A12 only. Actual baseline: **clean develop / `7e2a1884545e8e58b5242731c7f76be8d87c56b3`**, worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. A11's reported nine modified/four added files after `614c42e768b1712e786552dbfda410ad940f5d2f` had been committed. No pending work or historical artifacts were discarded. Repository/ancestor guidance inspection found no AGENTS.md. Tested code is this HEAD plus the four source/test files below; README and this record were edited afterward. Final source/binary hashes are in `TestResults/m2212a12-verified/source-provenance.json` and `binaries.json`.

### Formatting and timestamp contract

`NotebookDatePresentation.Format` is the small shared Notebook-only presentation helper. It calls the existing `TradingTimePolicy.ConvertUtcToTradingTime` first, then `.ToString("MMM dd, yyyy", CultureInfo.GetCultureInfo("en-US"))`. English month abbreviations and Gregorian year/day output therefore remain stable under other current cultures. Days have two digits: **Oct 04, 2024**. No time or NY suffix is appended to these date labels; their date boundary remains **America/New_York**, including the existing winter/summer offsets. Machine-local timezone is not substituted.

| Existing label | Timestamp retained | Display/fallback |
| --- | --- | --- |
| Note-list date, `NotebookRow.Updated` | `NotebookListItem.UpdatedAtUtc` | English date after existing New York conversion |
| Loaded note-header `Metadata` | `Note.UpdatedAtUtc` | Existing lifecycle label and `Updated` prefix, e.g. `Active · Updated Oct 04, 2024` |
| Header when content is unavailable but a row remains selected | Selected row's `UpdatedAtUtc` | Same row-date fallback and lifecycle/Updated labels |
| No loaded note and no selected row | None | Existing empty metadata string |

No Created label was present in these bindings, so none was added; `CreatedAtUtc` is not substituted for Updated. Canonical timestamps are non-null `DateTimeOffset` values, and the existing zero-UTC-offset validation/conversion is retained. Formatting does not parse display strings, mutate timestamps or feed sorting/filtering/paging. SQL still orders Recently updated by `UpdatedAtUtc` descending with the existing ID tie-breaker; Title order and cursor behavior are unchanged. Relative autosave/recovery status, linked-record labels and dates elsewhere in the application are untouched. A1–A11 controls, guards and persistence contracts remain intact.

### Focused commands and results

All paths below are under `TestResults/m2212a12-verified/`. The existing runner builds the selected project using `dotnet build <project> -c Release --no-restore`, then runs `dotnet test <project> -c Release --no-build --no-restore --filter <filter> --logger trx --results-directory <run>` with `PTJ_TEST_RESULTS_DIRECTORY`. It stops on build failure.

```powershell
& ./TestResults/m2212a12-verified/context.ps1 -OutputName before-context.json
& ./TestResults/m2212a12-verified/run.ps1 -Name desktop -Build -Filter 'FullyQualifiedName~NotebookDatePresentationTests|FullyQualifiedName~NotebookWorkspaceTests|(FullyQualifiedName~NotebookPresentationViewTests&((DisplayName~Light&DisplayName~1280)|(DisplayName~Dark&DisplayName~480)))'
& ./TestResults/m2212a12-verified/run.ps1 -Name infrastructure -Build -Project tests/PersonalTradingJournal.Infrastructure.Tests/PersonalTradingJournal.Infrastructure.Tests.csproj -Filter 'FullyQualifiedName~ExactFinalPagesStableTiesAndRestartDoNotOmitOrRepeatRows'
& ./TestResults/m2212a12-verified/audit.ps1
& ./TestResults/m2212a12-verified/context.ps1 -OutputName after-context.json
& ./TestResults/m2212a12-verified/check-whitespace.ps1
& ./TestResults/m2212a12-verified/check-doc-links.ps1
```

| Selection | Passed / total parents | Failed / skipped | Exit | Test-command wall time |
| --- | --- | --- | --- | --- |
| Desktop: date semantics, workspace and two populated presentation rows | 42 / 42 | 0 / 0 | 0 | 35.691s |
| Infrastructure: existing SQL title/recently-updated paging and stable ties | 2 / 2 | 0 / 0 | 0 | 12.156s |

Release builds passed with **zero warnings/errors**: `desktop/build.log` **70.62s**, `infrastructure/build.log` **22.80s** (build-reported elapsed time, separate from tests). No preparation or test failures occurred. Desktop comprises 18 new date cases, 22 existing workspace cases and two existing presentation parents strengthened with exact date/bounds assertions. The latter each dispatched exactly one matching child: **Light / 1280 / 96 DPI** and **Dark / 480 / 240 DPI**. **Two children are counted separately**, not added to parent totals. The two project selections do not overlap; repeated coverage from older milestone records is not added. `test-audit.json` reconciles literal-path-safe raw TRX counters, names and child identities with no missing, aborted, skipped or duplicate results.

Exact date cases cover English output under en-US/fr-FR/bg-BG/ar-SA cultures; single-digit days; New York midnight under summer and winter offsets; year and leap-day boundaries; dates immediately following DST transitions; loaded Updated versus Created/list timestamps; all four existing unavailable-content statuses and empty selection. A 55-note case confirms same-day labels do not collapse minute-level timestamp ordering or paging, and Title sorting still uses the original data. Existing SQL coverage verifies stable ties, complete pages and cursor-query compatibility. Existing populated UI assertions continue to exercise editing, tag chips/assignment preservation, retained selection, image cancellation and undo/redo; assertions, exact dispatch, collection placement, 90-second process and 30-second watchdog limits are unchanged.

No test process was already active. UI cases used the supported authorized executor in **session 1 / WinSta0 / Default**, interactive cursor access and the existing administrator token, without UAC/token/desktop switching. `before-context.json` and `after-context.json` record prerequisites and no remaining test processes after cleanup. Original TRX, output, supervision/input logs and all generated snapshots remain in their run directories.

### Inspected evidence and limitations

Four current real WPF captures were inspected:

- `renders/Light-1280-96-populated.png`
- `renders/Light-1280-96-rows.png`
- `renders/Dark-480-240-populated.png`
- `renders/Dark-480-240-rows.png`

`renders.json` records original paths, SHA-256 hashes and passing child intervals. The fixture uses UTC **2024-10-05 03:30**, which is **Oct 04** in New York; the distinct Created timestamp is in 2023. Both list/header labels are readable without clipping and retain their existing hierarchy. At normal width the populated/rows states show the same three-pane layout; at narrow width separate Content and Notes viewports show the corresponding labels. No composite or reduced-readability layout was used. Additional unchanged fixture snapshots remain retained but are not claimed as separately inspected A12 evidence.

The pre-existing **Light/high-DPI note-overflow-dot issue remains outstanding for correction before A13 acceptance**; this phase did not repair or reclassify it. Human keyboard/Narrator assessment, real clipboard/picker/viewer operation, native journal restart and matching hosted timing/input compatibility remain unverified. Whitespace and local Markdown target-existence checks passed after documentation edits (remote URLs and fragment resolution excluded). No schema/backend/format/CI changes or new EF validation are claimed. **A12 focused acceptance is complete; A13, M22.12B and overall M22.12 acceptance remain pending.** No complete gate, production/provider access, Git publication or workflow triggering occurred.

### Exact A12 inventory

**Four modified and two added files (six distinct files)**; no pre-existing uncommitted A11 changes. Ignored TestResults evidence is separate.

Modified:

- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookPresentationViewTests.cs`
- `README.md`
- `docs/notebook.md`

Added:

- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookDatePresentation.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookDatePresentationTests.cs`


## M22.12A11 — Inline tag creation and per-tag management

26 October 2026 (Europe/Kiev). One agent; A11 only. Actual baseline: **clean develop / `614c42e768b1712e786552dbfda410ad940f5d2f`**, worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. A10's nine reported pending changes after `438883acb8fb7ca668c1761161eba934c0e7dbd8` had been committed. Repository/ancestor guidance inspection found no AGENTS.md. The interrupted A11 turn had only its two model-file edits; they were preserved on resumption, HEAD was unchanged and no tests were active. Tested code is this HEAD plus the source/test inventory below; documentation was updated afterward. `TestResults/m2212a11-verified/source-provenance.json` and `binaries.json` record final hashes.

### Behavior and safety

- The Manage tags expander and its combined create/rename/delete controls are removed. **+ New tag** uses New folder's teal style and owned-focus mechanism. It opens one compact name field; Cancel is visible and teal Add appears only for non-whitespace text. Enter and Add share the existing validated creation command; Escape/Cancel reset the form without a pre-submission write. Blur does not submit. Input/errors survive invalid names, normalized duplicates, conflicts and persistence failures.
- Creation only adds a global catalog tag. It neither assigns the tag to the current note nor enables a filter. Successful completion refreshes the bounded catalog/list, clears the name/error and collapses the form without requesting focus. The existing assignment picker/chips and AND-tag filtering retain their meanings.
- Pending work makes the field read-only, disables duplicate creation/management submissions and leaves Cancel available. Dismissal invalidates form ownership and says a submitted change may already be saved; it is not a rollback. Captured form, activation and note-selection ownership prevent abandoned consent from persisting and late completion from clearing newer input or retargeting an unrelated note. Existing save/discard/cancel consent, autosave timing and expected-version writes remain intact. Assignment operations do not falsely announce that an unopened catalog form was dismissed.
- Every tag has a separate compact ellipsis management button with the full tag name in its tooltip and accessible name. Mouse/Space activation or the context-menu keyboard key/Shift+F10 opens Rename and Delete globally. The button is separate from the filter checkbox; opening/closing a menu or rename field does not toggle filtering or write data. Rename opens a prefilled inline name with Rename/Cancel and Enter/Escape, retaining validation and persistence feedback. The intended tag ID/version is captured before asynchronous work, rather than following mutable `ManagedTag` selection. Stale versions/deleted identities remain subject to the existing persistence checks.
- Delete retains the destructive confirmation identifying the tag and explaining removal from every note, including Archived and Trash, without deleting notes/content. Assignment removal remains a distinct note-level action that preserves the global catalog. The existing global-delete baseline verification, note-version advancement, conflict/recovery handling and stale-autosave rejection remain in place; no persistence/schema/archive/export change was made.
- A8 menu toggling and note-action ownership, A9 archived/folderless visibility and All Notes deletion fallback, and A10 explicit folder creation remain intact. No note-date, CI, deadline, collection, partition or concurrency changes were made. New native interaction cases use the existing exclusive folder-creation fixture via a partial class, retaining exact child selection, 90-second process and 30-second case watchdogs; no scheduling helper was changed.

### Commands and focused evidence

Paths below are under `TestResults/m2212a11-verified/`. `run.ps1` uses `dotnet test <project> -c Release --no-build --no-restore --filter <filter> --logger trx --results-directory <run>` and `PTJ_TEST_RESULTS_DIRECTORY`. `-Build` first runs `dotnet build <project> -c Release --no-restore` and stops on failure. The default project is `tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj`.

```powershell
& ./TestResults/m2212a11-verified/context.ps1 -OutputName resumed-context.json
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore
& ./TestResults/m2212a11-verified/run.ps1 -Name focused -Filter 'FullyQualifiedName~NotebookTagFormTests|FullyQualifiedName~NotebookTagTests|FullyQualifiedName~NotebookSqliteWorkspaceTests|FullyQualifiedName~NotebookAutosaveTests|FullyQualifiedName~NotebookFolderCreationTests|FullyQualifiedName~NotebookFolderCreationViewTests|FullyQualifiedName~NotebookLegacyNavigationTests|FullyQualifiedName~NotebookFolderActionTests|FullyQualifiedName~NotebookNoteActionsTests|FullyQualifiedName~OverflowTriggersToggleThroughNativeMouseAndOwnedKeyboard|FullyQualifiedName~ReleaseSchedulingTests|(FullyQualifiedName~NotebookTagViewTests&DisplayName~Light&DisplayName~1280)'
& ./TestResults/m2212a11-verified/run.ps1 -Name tag-corrected -Build -Filter 'FullyQualifiedName~NotebookTagFormTests|FullyQualifiedName~NotebookTagTests|FullyQualifiedName~NotebookSqliteWorkspaceTests|FullyQualifiedName~InlineTagCreationAndCapturedManagementUseRealControls|(FullyQualifiedName~NotebookTagViewTests&DisplayName~Light&DisplayName~1280)'
& ./TestResults/m2212a11-verified/run.ps1 -Name infrastructure -Build -Project tests/PersonalTradingJournal.Infrastructure.Tests/PersonalTradingJournal.Infrastructure.Tests.csproj -Filter 'FullyQualifiedName~NotebookTagWorkflowTests'
& ./TestResults/m2212a11-verified/run.ps1 -Name final-form-logic -Build -Filter 'FullyQualifiedName~NotebookTagFormTests|FullyQualifiedName~NotebookTagTests'
& ./TestResults/m2212a11-verified/audit.ps1
& ./TestResults/m2212a11-verified/context.ps1 -OutputName after-context.json
& ./TestResults/m2212a11-verified/check-whitespace.ps1
& ./TestResults/m2212a11-verified/check-doc-links.ps1
```

| Run | Parent passed / total | Failed / skipped | Exit | Command wall time |
| --- | --- | --- | --- | --- |
| Initial focused Desktop selection, retained | 95 / 98 | 3 / 0 | 1 | 60.275s |
| Corrected tag logic, SQLite and UI selection | 33 / 33 | 0 / 0 | 0 | 33.346s |
| Infrastructure tag workflows | 8 / 8 | 0 / 0 | 0 | 11.242s |
| Final form-feedback and tag logic | 21 / 21 | 0 / 0 | 0 | 4.154s |

Current evidence is **99 distinct passing Desktop parents**, not the sum of overlapping runs: 65 unaffected initial cases, 13 applicable corrected-selection cases and 21 final logic cases (including one new feedback regression). Infrastructure's eight cases are a separate project. `test-audit.json`/`audit-summary.json` reconcile raw literal-path-safe TRX identities and counters, including files containing brackets.

**Ten distinct child cases** are counted separately: six exact one-child parents (Light 1280/96 and Dark 480/240 for A8 menus, A10 folder creation and A11 tag creation/management), plus the existing four-case Light/Dark normal/narrow tag-render suite dispatched once by its selected Light-normal parent. Its other three visible parents were not separately rerun, avoiding repeated class-wide dispatch without changing that harness. Latest applicable child results have no failures/skips, missing cases or duplicate identities. Initial child failures remain in `failure-diagnostics.json` and their original directories.

Passing builds reported **zero warnings/errors**: `build-ready.log` 10.82s, `tag-corrected/build.log` 34.90s, `infrastructure/build.log` 5.66s and `final-form-logic/build.log` 14.40s. Two preparatory compilation failures are retained: `build.log` (25.75s), duplicate `AddTagButton` field, corrected by naming the new global creation control `AddNewTagButton` while retaining the note-assignment control; `build-controls.log` (31.14s), a local test helper named `Key` shadowing the enum, corrected to `PressTagKey`. No tests were launched against those failed builds.

The initial three failed parents represented six child failures with two causes:

1. Both new native form rows reached successful Add persistence but failed the retained closed-form assertion. The new selection-generation check treated WPF's transient clear/reselect during the command-owned `Notes` refresh as external navigation. Completion now checks the still-owned form/activation after this refresh; external selection is disabled while `IsWriting`, while dismissal/newer-input generation checks remain. Before persistence and before adopting a returned note baseline, the stronger context checks still apply. Model and actual bound-control tests passed after correction.
2. The selected retained-render parent reported all four child rows failing the newly added `IsVisible` assertion. This fixture measures a detached visual, whose screen visibility is false. It now asserts configured `Visibility.Visible`; the separate hosted interaction rows still assert actual visibility, enabled state, keyboard focus, command counts and input retention. Existing filter, assignment-removal, editor-retention and wrapping assertions remain.

The final small correction limits the pending-dismissal warning to a form that was actually open; a new delayed assignment-removal case demonstrates that changing panels with no catalog form does not show misleading form-cancellation feedback. Only the affected 21 logic cases ran again. No assertion, scheduling or deadline was weakened and no run was repeated merely to obtain green results. Original stdout, raw TRX and child supervision/input diagnostics remain under `focused/`; corrected evidence is under `tag-corrected/`, `infrastructure/` and `final-form-logic/`.

Coverage includes blank/whitespace/nonblank Add visibility, Add/Enter exactly-once persistence, Cancel/Escape no-write behavior, retained errors, pending repeated Enter, cancellation and newer-input/deactivation protection, captured rename/delete targets, confirmation cancellation, no automatic assignment/filter, real SQLite duplicate/stale-deleted-tag behavior, selected externally deleted filter repair, autosave/recovery, A10 guards and A8 native menu interaction. Infrastructure covers concurrent deletion/save, stale autosave after assignment/global deletion, counts/search/paging, unsupported content and global versus single-assignment deletion.

Supported UI execution used **session 1 / WinSta0 / Default**, interactive cursor access, the existing administrator token and the supported authorized executor; no UAC/token/desktop switching. `before-context.json`, `resumed-context.json` and `after-context.json` record no active tests before launch/resumption or after final cleanup. UI automation invokes the actual Button/MenuItem command paths; keyboard activation uses the existing input helper. This is not human interaction, native journal restart or proof of hosted input compatibility.

### Inspected captures and limits

Ten real WPF captures under `renders/` were inspected:

- `Light-1280-96-{empty,populated,validation,rename,management-menu}.png`.
- `Dark-480-240-{empty,populated,validation,rename,management-menu}.png`.

`renders.json` records each exact path, original child path, SHA-256 and passing child interval. Menus are separately captured popup viewports, not composited application screenshots. Empty/whitespace input shows Cancel only; populated input shows Add/Cancel; error text wraps readably; long rename text remains editable by horizontal caret scrolling; management actions are readable in both themes. No blocking A11 visual defect was identified. These corrected-run captures remain current: the final logic-only warning condition does not alter their view/resources/fixture inputs or any displayed state. No exhaustive matrix or unrelated recapture was performed.

The pre-existing **Light/high-DPI note-overflow-dot issue remains tracked for correction before A13 acceptance**, with A9's original evidence retained. Shared tag feedback still appears in both the organization pane and note-assignment feedback, as before. Native mouse activation of the new tag buttons was not separately exercised; their standard Button path was invoked through UI Automation and keyboard, while the retained A8 suite exercised native mouse menus. Human keyboard/Narrator, real clipboard/picker/viewer, native journal restart and matching hosted timing/input remain unverified. No new EF check is claimed for this presentation-only phase; backend/schema files are unchanged.

Whitespace and local Markdown target-existence checks passed after documentation edits; remote URLs and fragment resolution are excluded. **A11 focused acceptance is complete. A12–A13, M22.12B and overall M22.12 acceptance remain pending.** No full gate, production/provider access, publication, workflow triggering or unrelated redesign occurred.

### Exact A11 inventory

**Nine modified and four added files (13 distinct files)**; no pre-existing pending changes. Ignored TestResults evidence is separate. A1–A10 source and historical artifacts were preserved.

Modified:

- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Tags.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookFolderCreationViewTests.cs` (partial declaration only; original A10 case retained)
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookSqliteWorkspaceTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookTagViewTests.cs`
- `README.md`
- `docs/notebook.md`

Added:

- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.TagForm.cs`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.Tags.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookFolderCreationViewTests.Tags.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookTagFormTests.cs`


## M22.12A10 — Explicit folder Add and Cancel

25 October 2026. One agent; A10 only. Actual baseline: **clean develop / `438883acb8fb7ca668c1761161eba934c0e7dbd8`**, worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. A9's reported pending changes after `70d65eaa9991b972398503c00cf111d18fc080b5` were already committed. No existing changes or artifacts were discarded. Repository/ancestor inspection found no AGENTS.md. Tested code is this HEAD plus the seven source/test files below; README and this record were edited afterward. `final-source.json` and `final-binaries.json` record the final hashes; the original `tested-source.json` and `binaries.json` remain historical.

### Behavior and ownership

- **+ New folder** retains owned focus into the name field. Cancel is always visible while the form is open; Add is visible only for non-whitespace input and uses the existing teal action style. Cancel uses the secondary style. Wrapping buttons, accessible names, tooltips, focus indicators and inline error text remain usable at narrow width/high DPI.
- Add and Enter use the same validated `CreateFolderCommand`. Enter is intercepted only while the name field owns focus, avoiding a second submission from button keyboard activation. Blur never submits. Invalid names, normalized SQLite duplicates and persistence errors retain the exact draft and error for correction.
- Explicit Cancel/Escape clears the name, inline error and matching global validation error without clearing unrelated feedback. Before submission it creates nothing. Implicit dismissal retains its previous draft behavior. Success refreshes counts/catalog and selects the created folder through existing guarded navigation, then resets/collapses the form. Search/tag filters and newer note edits remain protected.
- While creation is pending, Add is disabled, the name is read-only and Cancel remains available. The form explains that Cancel closes input and creation may already be saved; the existing pending-dismissal message persists after closing. It does not promise rollback. Existing generation checks prevent persistence after abandoned consent and prevent an already committed late result from taking unrelated selection or keyboard focus. No persistence, autosave timing, navigation consent or expected-version contract changed.
- A8 menu toggling/ownership/colors and A9 non-Trash legacy visibility, nullable assignments and All Notes deletion fallback remain intact. Tag creation, schema, formats, CI settings, watchdogs and scheduling are unchanged.

### Commands, results and retained failure evidence

All evidence paths below are under `TestResults/m2212a10-verified/`. The runner uses `dotnet test <Desktop.Tests.csproj> -c Release --no-build --no-restore --filter <filter> --logger trx --results-directory <run>` with `PTJ_TEST_RESULTS_DIRECTORY`. Builds use `dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore`.

```powershell
& ./TestResults/m2212a10-verified/context.ps1 -OutputName before-context.json
& ./TestResults/m2212a10-verified/run.ps1 -Name focused -Filter 'FullyQualifiedName~NotebookFolderCreationTests|FullyQualifiedName~NotebookFolderCreationViewTests|FullyQualifiedName~NotebookSqliteWorkspaceTests|FullyQualifiedName~NotebookWorkspaceTests|FullyQualifiedName~NotebookLegacyNavigationTests|FullyQualifiedName~NotebookFolderActionTests|FullyQualifiedName~NotebookAutosaveTests|FullyQualifiedName~NotebookNoteActionsTests|FullyQualifiedName~OverflowTriggersToggleThroughNativeMouseAndOwnedKeyboard|FullyQualifiedName~ReleaseSchedulingTests'
& ./TestResults/m2212a10-verified/run.ps1 -Name folder-ui-corrected -Filter 'FullyQualifiedName~NotebookFolderCreationViewTests'
& ./TestResults/m2212a10-verified/run.ps1 -Name cancel-reset-final -Build -Filter 'FullyQualifiedName~NotebookFolderCreationTests|FullyQualifiedName~NotebookFolderCreationViewTests'
& ./TestResults/m2212a10-verified/final-audit.ps1
& ./TestResults/m2212a10-verified/context.ps1 -OutputName final-after-context.json
& ./TestResults/m2212a10-verified/check-whitespace.ps1
& ./TestResults/m2212a10-verified/check-doc-links.ps1
```

| Run | Parent passed / total | Failed / skipped | Exit | Command wall time |
| --- | --- | --- | --- | --- |
| Initial focused selection, retained | 94 / 96 | 2 / 0 | 1 | 54.976s |
| Corrected UI fixture only | 2 / 2 | 0 / 0 | 0 | 16.583s |
| Final explicit-error-reset logic and UI | 19 / 19 | 0 / 0 | 0 | 19.335s |

All three Release builds passed with **zero warnings/errors**: `build.log` 51.88s; `build-fixture-corrected.log` 10.79s; `cancel-reset-final/build.log` 36.74s (build-reported elapsed times). The latter includes the final Cancel/global-message reset. Current evidence comprises **96 distinct passing parents**: 77 unaffected cases reused from the first selection and 19 final affected cases, not the sum of overlapping runs. These cover real SQLite duplicates, creation consent/selection, pending cancellation, failures, autosave/newer drafts, folder deletion and legacy navigation, note-action ownership and release scheduling.

`final-test-audit.json` preserves literal-path-safe raw TRX identities/counters across all runs. **Four distinct matching children** are counted separately: Light 1280/96 and Dark 480/240 folder creation (final run), plus the same two parameter rows for native overflow interaction (initial run). Each exact parent dispatched one matching child; final applicable evidence has no missing, skipped, duplicate or failed results. Historical repeated executions are not added to coverage totals.

The two initial failures were the new folder-creation UI rows, at `Ready(SearchBox)` during the pending-creation check. Input logs recorded Search as **enabled=False**: existing `CanInteract` intentionally disables it during persistence. This was an invalid fixture focus target, not evidence of failed creation or denied desktop access. The correction asserts that disabled state and focuses an enabled fixture-owned button outside Notebook to model moving elsewhere; retained assertions verify that late completion leaves that focus and the prior scope intact. Original parent/child TRX, phase/input/supervision logs and errors remain under `focused/`, indexed by `failure-diagnostics.json`. The first corrected run passed both children. Final review then found matching global validation feedback remained after explicit Cancel; the small reset correction and its assertion justified the final 19-case run. No retries merely to obtain green results, relaxed assertion, arbitrary wait, forced focus guard, changed deadline or complete gate was used.

UI Add checks invoke the actual Button automation peer/OnClick command path and require a fresh command task; Enter, Escape and Space checks use the existing keyboard helper. Existing A8 overflow checks use native mouse/keyboard input. Supported execution was **session 1 / WinSta0 / Default**, interactive cursor access, existing administrator token; no UAC/token/desktop switching. Restricted process inspection initially denied CIM access; supported executor context was recorded before testing. `before-context.json`, `before-corrected-context.json` and `final-after-context.json` establish context and no independently active tests before launch or remaining test processes after final cleanup. This does not establish restricted-executor or hosted input compatibility.

### Visual evidence and limitations

Six real WPF snapshots were inspected for readable fields, inline errors, teal Add, secondary Cancel and wrapping without clipping:

- `renders-final/Light-1280-96-empty.png`
- `renders-final/Light-1280-96-populated.png`
- `renders-final/Light-1280-96-validation.png`
- `renders-final/Dark-480-240-empty.png`
- `renders-final/Dark-480-240-populated.png`
- `renders-final/Dark-480-240-validation.png`

`final-renders.json` records their original child-artifact paths, SHA-256 hashes and matching passing child intervals. All six final-build images are byte-identical to the inspected corrected-fixture images in `renders/`; the final message-reset correction did not change these visible states. Empty/whitespace input shows Cancel only; populated input shows Add and Cancel; validation retains the field/error. The existing Light/high-DPI note-overflow-dot limitation remains tracked in A9; no unrelated visual repair was attempted.

Whitespace and local documentation-target existence checks passed after documentation edits (remote URLs and fragment targets are outside that check). Backend/model files are unchanged; no new EF/schema verification or backend acceptance is claimed. Human keyboard/Narrator assessment, real clipboard/picker/viewer, native journal restart and matching hosted timing/input compatibility remain unverified. **A10 focused acceptance is complete; A11–A13, M22.12B and overall M22.12 acceptance remain pending.** No full Release gate, production/provider access, publication or workflow triggering occurred.

### Exact A10 inventory

**Nine modified files; zero added source files; no pre-existing uncommitted A9 changes.** Ignored TestResults artifacts are separate.

- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Folders.cs`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookFolderCreationTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookFolderCreationViewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookSqliteWorkspaceTests.cs`
- `README.md`
- `docs/notebook.md`


## M22.12A9 — Supported scopes and legacy-note visibility

25 October 2026. One agent; A9 only. Actual baseline: **clean develop / `70d65eaa9991b972398503c00cf111d18fc080b5`**, existing worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. A8's reported pending changes after `afb369a415376563f0feea1b68b9f88ea105032a` had already been committed. No pre-existing changes were overwritten. Repository/ancestor guidance inspection found no AGENTS.md. Results apply to this HEAD **plus the A9 inventory below**, with source and binary SHA-256 manifests in `TestResults/m2212a9-verified/source-provenance.json` and `binaries.json`.

### Behavior and compatibility

- WORKSPACE retains All Notes/Pinned and FOLDERS retains real folders/counts. The Notebook overflow now contains Trash and Refresh. Archived/Unfiled entries, Archive/Return to Active note actions and the Count & filters help block are absent. A8's teal states, owned focus, second-click toggling, Escape/outside dismissal, mutual exclusion and captured-note command ownership are unchanged.
- All Notes includes every non-Trash note; Pinned includes pinned non-Trash notes. Folder/search/AND-tag queries, summaries and folder/tag catalog counts use the same population. SQL filtering, bounded projections, stable ordering, continuation limits, batched tags and snapshot-consistent counts remain intact. No document decoding, unbounded in-memory filtering or per-row database query was introduced. The legacy `ActiveNoteCount` member name is retained but documented as counting non-Trash assignments. Cursor fingerprints distinguish the new population; an old cursor must restart at the first page rather than silently mix semantics.
- Stored Archived notes remain read-only: title/content, folder movement, pin and assignment changes are still rejected by their existing domain rules. Readable notes expose Duplicate (an independent Active copy) and Move to Trash. The editor explains this restriction; unsupported content explains preservation without promising an unavailable Duplicate action. There is deliberately no in-place Return to Active UI. Existing backend lifecycle commands remain compatible; no bulk conversion or migration was added.
- Trash still restores the prior lifecycle state, including Archived, and the surviving previous folder. Restored archived notes immediately appear in All Notes and applicable pinned/folder/filter results. Nullable assignments remain nullable, displayed as **No folder**; Active notes can still remove their folder assignment through Move. Folder deletion preserves notes/content, clears current/restore-point folder references through the existing contract, and uses guarded All Notes fallback with consistent heading/highlighting/editor state. Existing search/tag filters remain applied.
- Retained Archived/Unfiled scope values normalize to All Notes in both the query contract and guarded workspace selection. No note data changes during normalization. Draft cancellation retains the original scope/note/draft; successful consent preserves edits and selection remains supported after deactivation/reactivation.
- Document JSON/archive v1 and existing export compatibility remain unchanged. Fresh-provider restore checks exercise both the retained older chosen backup and the newer backup, confirm archived discoverability in search/pinned/folder/tag results, and preserve exact exports, version, canonical content, image originals, links and recovery data. These are synthetic service/context restart checks, not a native journal-process restart.

### Focused commands and evidence

All paths below are under `TestResults/m2212a9-verified/` unless shown otherwise. No test/build was already active before this phase. Native UI checks used the supported authorized executor in **session 1 / WinSta0 / Default**, interactive cursor access, existing administrator token; no UAC, token or desktop switching was requested. `execution-context.json`, `before-ui-context.json` and `after-ui-context.json` record the context; the final check found no active test processes. Restricted-executor compatibility is not established by these runs.

```powershell
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore
dotnet build tests/PersonalTradingJournal.Infrastructure.Tests/PersonalTradingJournal.Infrastructure.Tests.csproj -c Release --no-restore
dotnet build tests/PersonalTradingJournal.Application.Tests/PersonalTradingJournal.Application.Tests.csproj -c Release --no-restore
& ./TestResults/m2212a9-verified/run.ps1 -Name application -Project tests/PersonalTradingJournal.Application.Tests/PersonalTradingJournal.Application.Tests.csproj -Filter 'FullyQualifiedName~Notebook'
& ./TestResults/m2212a9-verified/run.ps1 -Name infrastructure -Project tests/PersonalTradingJournal.Infrastructure.Tests/PersonalTradingJournal.Infrastructure.Tests.csproj -Filter 'FullyQualifiedName~NotebookQueryTests|FullyQualifiedName~NotebookLegacyVisibilityTests|FullyQualifiedName~NotebookTagWorkflowTests|FullyQualifiedName~NotebookPersistenceTests|FullyQualifiedName~NotebookTemplateLifecycleTests|FullyQualifiedName~NotebookPortabilityTests'
& ./TestResults/m2212a9-verified/run.ps1 -Name desktop-logic -Filter 'FullyQualifiedName~NotebookWorkspaceTests|FullyQualifiedName~NotebookLegacyNavigationTests|FullyQualifiedName~NotebookSqliteWorkspaceTests|FullyQualifiedName~NotebookFolderActionTests|FullyQualifiedName~NotebookFolderCreationTests|FullyQualifiedName~NotebookHeaderTests|FullyQualifiedName~NotebookNoteActionsTests|FullyQualifiedName~NotebookLifecycleTests|FullyQualifiedName~NotebookCopyDeleteTests|FullyQualifiedName~NotebookTagTests|FullyQualifiedName~NotebookAutosaveTests|FullyQualifiedName~NotebookEditingTests|FullyQualifiedName~NotebookLinkTests|FullyQualifiedName~ReleaseSchedulingTests'
& ./TestResults/m2212a9-verified/context.ps1 -OutputName before-ui-context.json
& ./TestResults/m2212a9-verified/run.ps1 -Name desktop-ui -Filter 'FullyQualifiedName~NotebookHeaderViewTests|FullyQualifiedName~NotebookLifecycleViewTests|FullyQualifiedName~NotebookFolderActionsViewTests|(FullyQualifiedName~NotebookViewTests&DisplayName~Light&DisplayName~1280)'
& ./TestResults/m2212a9-verified/context.ps1 -OutputName after-ui-context.json
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
& ./TestResults/m2212a9-verified/audit.ps1
& ./TestResults/m2212a9-verified/check-whitespace.ps1
& ./TestResults/m2212a9-verified/check-doc-links.ps1
```

The runner uses `dotnet test <project> -c Release --no-build --no-restore --filter <filter> --logger trx --results-directory <phase/run>` and sets `PTJ_TEST_RESULTS_DIRECTORY`. Builds passed with **zero warnings/errors**: Desktop **62.73s**, Infrastructure **8.51s**, Application **4.11s** (reported build elapsed time). EF: **no pending model changes**, design-time factory; no production database access.

| Selection | Passed / total | Failed / skipped | Exit | Measured command wall time |
| --- | --- | --- | --- | --- |
| Application Notebook contracts | 29 / 29 | 0 / 0 | 0 | 4.975s |
| Infrastructure query, mixed-state visibility, tags, persistence, lifecycle and portability | 50 / 50 | 0 / 0 | 0 | 63.417s |
| Desktop workspace, legacy navigation, SQLite, folders, header/actions, lifecycle/copy, tags, autosave/editing, links and scheduling | 155 / 155 | 0 / 0 | 0 | 14.172s |
| Desktop UI parents | 11 / 11 | 0 / 0 | 0 | 104.860s |

**166 distinct top-level Desktop cases**; child counts are separate. Literal-path-safe `test-audit.json` reconciles all TRX counters and result identities. Ten exact-dispatch parents each completed their one matching child: four lifecycle, two header, two native menu and two folder-action cases. One selected legacy workspace parent dispatched its existing four-case Light/Dark normal/narrow matrix once. **14 distinct child cases**, no missing, skipped, duplicate or failed results. The native menu cases retain actual mouse/keyboard dispatch and verify single execution, closure/reopening and stale-command rejection using the remaining Trash action. Original assertions, collection/trait scheduling, 90-second process limits and 30-second child watchdogs remain intact. No failed run was replaced and no full gate ran.

Raw parent/child TRX, output, supervision, phase and keyboard/input logs remain beneath their four run directories. Successful focused logic includes folder deletion consent/retention/All Notes fallback, changed selection during operations, A1 refresh/cancel highlighting, A2 creation, A3 stale rename/deletion, A4 creation/cancellation, A5 newer drafts, autosave/recovery, retained image editing, tags and linked navigation. Old expectations were changed only where the requested scope population/UI contract changed; exact content assertions remain.

### Inspected visuals and limitations

Fourteen current real WPF captures were inspected and copied into `renders/`; `renders.json` records original paths, hashes, passing child identities and capture times within their child intervals:

- `renders/{Light,Dark}-1280-96-legacy-{folderless,note-menu}.png` (four).
- `renders/{Light,Dark}-480-240-legacy-{folderless,note-menu}.png` (four).
- `renders/Light-1280-96-{NotebookOverflow,NoteOverflow}-open-menu.png` and corresponding `Dark-480-240` captures (four).
- `renders/{Light-1280-96,Dark-480-240}-NotebookOverflow-closed.png` (two).

The removed options/help are absent, remaining menus are readable, No folder and the archived read-only explanation wrap without overlap, and the existing teal/focus styles remain visible. No blocking A9 visual defect was found. One pre-existing visual limitation remains: the Light 480/240 lifecycle render shows a single visible dot in the note overflow trigger. The same appearance is present in retained A8 `final/notebook-lifecycle-20261024-185449-ca6dc119ad2443af8e02c20203d875fb/m2210b-renders/Light-480-240-archived.png`; no claim is made about its underlying rendering/input cause, and it was not broadened into a styling rewrite. The accessible name and keyboard/menu behavior pass. The native menu fixture's black outer strip and unavailable synthetic image are fixture presentation, not an application screenshot or evidence of a newly lost original.

Whitespace (tracked and added files) and local Markdown target-existence checks passed; link checks exclude remote URLs and fragment validation. Human keyboard/Narrator assessment, real clipboard/picker/viewer operations, native journal restart and matching hosted timing/input compatibility remain unverified. Legacy archived notes cannot be edited or moved in place; Duplicate is the supported editable path. **A9 focused acceptance is complete. A10–A13, M22.12B and overall M22.12 acceptance remain pending.** No schema/format/CI/harness change, publication, workflow trigger or production/provider access occurred.

### Exact A9 inventory

**22 modified and two added files**, all A9; no pre-existing pending A8 files. The two added files are `NotebookLegacyNavigationTests.cs` and `NotebookLegacyVisibilityTests.cs`. Ignored TestResults evidence is separate from source inventory.

- `README.md`
- `docs/notebook.md`
- `src/PersonalTradingJournal.Application/Notebook/NotebookQueries.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.FolderActions.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Lifecycle.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.NoteActions.cs`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml.cs`
- `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.Queries.cs`
- `tests/PersonalTradingJournal.Application.Tests/Notebook/NotebookContractTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookFolderActionTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookHeaderViewTests.Menus.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookHeaderViewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookLegacyNavigationTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookLifecycleTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookLifecycleViewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookTagTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookViewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookWorkspaceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/NotebookIntegratedBackupTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookLegacyVisibilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookQueryTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookTagWorkflowTests.cs`

## M22.12A8 — Notebook action colors and menu toggling

24 October 2026. One agent; A8 only. Verified baseline: clean **develop / `afb369a415376563f0feea1b68b9f88ea105032a`**, existing worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. A7 had already been committed after the reported `6df156e869e7d802a021414de4fc47815cd6a3eb`; no pending changes were overwritten. Repository/ancestor guidance inspection found no AGENTS.md. Results apply to this HEAD plus the seven-file inventory below.

**Confirmed cause and correction.** Both ellipsis buttons used the shared EntityActionMenu handler, which unconditionally set IsOpen=true. The retained native-input baseline reproduces the second-click failure: outside-down at timestamp 121302156 releases menu capture, then the underlying trigger receives mouse-down/up and opens the menu again at 121302171. Waiting for the eventual Closed event or simply inverting IsOpen in Click cannot reliably distinguish this sequence. WPF's [MenuBase outside-click handling](https://raw.githubusercontent.com/dotnet/wpf/v10.0.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/Primitives/MenuBase.cs) corroborates the observed ordering.

The Notebook-local handler records the trigger and outside-down timestamp for that pointer gesture. The corresponding Click consumes the dismissal instead of reopening; a fresh mouse-down or keyboard input clears an obsolete gesture. Other activation toggles normally and explicitly closes the other ellipsis menu. Escape, outside clicks, menu-item dismissal and synchronous return to the originating trigger retain WPF's own menu behavior; no delayed focus restoration was added. Detaching closes both menus. Existing captured-note commands, selection-generation checks, confirmations and Save/Discard/Cancel guards are unchanged. Opening/closing either menu performs no write. The shared EntityActionMenu implementation, template dropdown and unrelated controls were not modified.

Only New folder, NotebookOverflow and NoteOverflow opt into the local styles. They reuse existing `PtjPlanReadyBrush`, hover/pressed variants and `PtjPlanOnActionBrush`; disabled controls use existing surface/secondary-text/border resources. The ellipsis buttons remain 36 DIP square; New folder remains compact. Opaque state colors preserve contrast; a dashed foreground focus indicator is separate from the pressed/open color. Existing tooltips and accessible names remain. Automated checks verify open/hover/pressed/disabled brush selection, visible focus and at least **4.5:1** foreground/background contrast for the exercised states in both themes.

### Focused verification

Final Release build: `TestResults/m2212a8-verified/menu-readiness/build.log`, **0 warnings/errors**, reported **7.50s**. The final source was then exercised once by the combined focused selection below: **36/36 top-level Desktop tests**, zero failures/skips, exit **0**, **99.229327s** including isolated child startup. Ten one-case children match ten distinct parent identities; the two new mouse/menu theory rows are Light/1280/96 DPI and Dark/480/240 DPI. Child results and earlier representative runs are not added to the 36-parent total.

| Selection | Passed/total |
| --- | --- |
| NotebookHeaderViewTests (two existing + two new) | 4/4 |
| NotebookLifecycleViewTests | 4/4 |
| NotebookFolderCreationViewTests | 2/2 |
| NotebookHeaderTests | 3/3 |
| NotebookNoteActionsTests | 7/7 |
| NotebookFolderCreationTests | 15/15 |
| ReleaseSchedulingTests | 1/1 |

The new test uses native mouse movement/down/up against verified fixture-owned HWNDs. It covers open/close/reopen on both triggers, outside dismissal without focus theft, Escape and return focus, Space activation, switching menus in the full-width layout, exactly one Refresh query and one Duplicate write, stale-note command rejection, New folder focus and action colors. Existing view tests exercise keyboard selection of menu actions, all templates, lifecycle availability/confirmations, pin/move and stale popup ownership. Logic regressions retain duplicate-submit, cancelled navigation, newer-draft and late-result checks. The existing exclusive trait/collection, case-owned Application, exact display-name selection/single-result validation, 90-second process and 30-second watchdog limits remain unchanged.

Commands (the existing runner preserves output, exit JSON, TRX and supervision artifacts):

```powershell
& ./TestResults/m2212a8-verified/context.ps1 -OutputName before-context.json
& ./TestResults/m2212a8-verified/run.ps1 -Name menu-readiness -Build -Filter 'FullyQualifiedName~OverflowTriggersToggleThroughNativeMouseAndOwnedKeyboard&DisplayName~Dark'
& ./TestResults/m2212a8-verified/run.ps1 -Name final -Filter 'FullyQualifiedName~NotebookHeaderViewTests|FullyQualifiedName~NotebookLifecycleViewTests|FullyQualifiedName~NotebookFolderCreationViewTests|FullyQualifiedName~NotebookHeaderTests|FullyQualifiedName~NotebookNoteActionsTests|FullyQualifiedName~NotebookFolderCreationTests|FullyQualifiedName~ReleaseSchedulingTests'
& ./TestResults/m2212a8-verified/audit.ps1
& ./TestResults/m2212a8-verified/context.ps1 -OutputName after-context.json
& ./TestResults/m2212a8-verified/check-whitespace.ps1
& ./TestResults/m2212a8-verified/check-doc-links.ps1
```

The runner builds `tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore`, then tests that project with `-c Release --no-build --no-restore`, the recorded filter, `--logger trx` and its unique results directory. The final test run did not rebuild again. `final/result.json`, `test-audit.json`, raw TRX and each child’s `menu-evidence/input-sequence.log` retain results and event order. Literal-path-safe auditing rejects missing, duplicate, failed or skipped results. `before-context.json` / `after-context.json` confirm supported authorized interactive Windows session 1, WinSta0/Default, cursor access and no active tests before/after. The executor used its existing token; no UAC/token/desktop switching occurred.

Development evidence is retained and excluded from final totals:

- `before-fix`: one failed Light case confirms the production reopen defect (second-click IsOpen remained true).
- `toggle-fixed`: two failures in the new fixture's immediate HWND readiness assertion before clicking Refresh; repeated toggles and dismissal had passed.
- `popup-ready`: Light passed; Dark's Refresh command never started because a moving popup item was clicked outside its final position. A hit-testable HWND alone was insufficient.
- `input-ready`: two readiness failures because the new mouse-over condition incorrectly required outside triggers to report mouse-over while the menu held capture.
- `menu-readiness`: corrected Dark case passed 1/1 with one matching child before the final combined run.

Fixture readiness now waits, within three seconds and the unchanged watchdog, for the intended HWND and completed popup translation/fade; menu items additionally require actual mouse-over. Outside triggers intentionally do not require mouse-over during menu capture. No arbitrary sleep, input retry, forced capture release, substituted command task or direct-command replacement was introduced. The [WPF PopupRoot animation implementation](https://raw.githubusercontent.com/dotnet/wpf/v10.0.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/Primitives/PopupRoot.cs) informed these checks. Production behavior was not altered to compensate for fixture animation timing.

### Visual evidence and scope

**Sixteen final real WPF captures inspected**, all copied from the passing final children into `TestResults/m2212a8-verified/renders/`; `renders.json` records original paths and SHA-256 hashes:

- `{Light-1280-96,Dark-480-240}-{NotebookOverflow,NoteOverflow}-{closed,open-trigger,open-menu}.png` (12).
- `{Light-1280-96,Dark-480-240}-NewFolder-pressed-focus.png` (two).
- `{Light-1280-96,Dark-480-240}-disabled.png` (two).

Controls remain compact and readable; teal state differences and dashed focus borders are visible in both themes. Open popup content is captured separately from its trigger rather than composited. Narrow evidence uses the existing pane switcher and 240-DPI render target; it does not establish physical monitor scaling. The transparent top strip comes from the fixture’s external focus target offset; it is not Notebook content. Disabled captures explicitly exercise the local style without changing production command availability. No blocking A8 visual defect was found. A7's broader rich-content evidence remains historical/current for unchanged content; it is not substituted for A8 action-state captures.

Exact inventory: **five modified and two added files**, no pre-existing pending files:

- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml` — local three-control action styles and two toggle bindings.
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml.cs` — initialize local menu handling and close both menus on detach.
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.Menus.cs` (added) — gesture-aware toggle and menu exclusivity.
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookHeaderViewTests.cs` — partial declaration for the new focused cases; existing assertions retained.
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookHeaderViewTests.Menus.cs` (added) — native-input cases, bounded fixture readiness, diagnostic trace and renders.
- `README.md` and `docs/notebook.md` — phase record. Ignored TestResults evidence is not additional source inventory.

Whitespace and local documentation-target checks passed; URL/fragment validation is outside that check. No persistence/schema/document/export change requires a new EF check. No complete gate, CI change, provider access, publication or workflow triggering occurred. **A8 is complete; A9–A13 and M22.12B remain pending. Overall M22.12 acceptance remains pending.** Automated native input here does not establish human keyboard/Narrator assessment, real clipboard/picker/viewer operation, installed-codec behavior, native journal restart or matching hosted timing/input compatibility.

## M22.12A7 — Integrated Notebook UI acceptance

24 October 2026. **A1–A7 integrated automated UI acceptance is complete; ready for M22.12B. Overall M22.12 acceptance remains pending.** One agent, no complete Release gate. Actual baseline was clean **develop / `6df156e869e7d802a021414de4fc47815cd6a3eb`**, existing worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. The reported A6 changes after `213c2d744d17fb61784627e215e0f5c043ad4428` had already been committed. No pending changes were overwritten. Repository/ancestor guidance inspection found no AGENTS.md.

Evidence provenance: all ten A6 final file hashes and both recorded Desktop/test binary hashes matched at entry (`TestResults/m2212a7-verified/baseline-provenance.json`). Therefore the A6 final 191/191 parent results, 34 separately counted child results and current A1–A6 renders remain applicable. A1–A5 UI families were rendered again during that A6 run; older phase screenshots were not substituted for the final UI. Each reused image is copied with its original path, SHA-256, passing child identity and TRX interval in `reused-renders.json`; every capture timestamp falls inside that child's passing interval. Autosave captures from the test output directory were checked the same way. The legacy directory name `m2210b-renders` does not indicate old evidence: these copies originate in the A6 final lifecycle child runs.

The concrete visual coverage gap was the lower linked-record area of the coherent populated note. The existing presentation fixture now also scrolls its rendered ContentScroll to the bottom and asserts that both complete linked rows are within the viewport. Its top and bottom captures show the same fixture: rich text, checklist, three assigned tags including a long name, folder context, captioned chart, Trade and Trading Setup links, pin and save state. No composite or reduced-scale substitute was made. Existing interaction assertions, exact child selection, case-owned lifetime, exclusive scheduling, 90-second process deadline and 30-second watchdog remain unchanged. No production correction was demonstrated or made.

### Requirement-to-evidence matrix

All paths below are under `TestResults/m2212a7-verified/` unless explicitly identified as A6. Automated coverage means the retained passing assertions plus source/real-render review, not a human usability assessment.

| Requirement | Current evidence | Status | Remaining issue or limit |
| --- | --- | --- | --- |
| A1 Workspace contains only All Notes/Pinned with icons; separate counted folders; virtual scopes in one overflow | A6 NotebookViewTests and Workspace logic; reused populated and header/overflow captures | Passed | Existing shell retained |
| A2 inline folder creation, validation, owned focus, duplicate/late submission and guarded selection | A6 FolderCreation logic/view cases; reused folder-creation expanded/validation captures | Passed | Validation also appears in global feedback; readable, nonblocking duplication |
| A3 real-folder-only rename/delete, long heading, confirmation, note retention and fallback | A6 FolderAction logic/view cases, including changed selection during confirmation/persistence; reused folder-action long-heading/rename/validation | Passed | Narrow layout uses existing pane navigation |
| A4 search, split blank/template creation, all five templates, Refresh/virtual scopes, contextual Cancel | A6 Header logic/view cases and Workspace regressions; reused header templates/overflow/busy | Passed | Search semantics unchanged; no live search |
| A5 title, authoritative pin, note menu, lifecycle availability and stale-note/draft safety | A6 NoteActions/Lifecycle/CopyDelete and lifecycle view cases; reused long-title/note-menu/archived | Passed | Save/Discard remain contextual when needed |
| Truthful save/recovery/error states, empty selection and read-only capabilities | A6 Autosave logic/view and lifecycle view; reused autosave recovered/conflict/saving/failed, lifecycle empty/save-error/archived | Passed | Recovery checkpoint explicitly differs from canonical save |
| Compact toolbar, selection-preserving link/image insertion, checklist versus checked-state, undo/redo and safe paste | A6 Editing/EditorView/ImageEditor/ImageCoordination plus four A7 presentation cases | Passed | Native picker/clipboard/viewer usage is not established by test doubles |
| Assigned chips versus global tags; bounded list title/preview/date/tags/pin; filters and selection | A6 Tag logic/view, Workspace and presentation cases; reused populated/rows/tag-picker | Passed | No new queries, serialization or features |
| Integrated rich note in Light/Dark at 1280 DIP/96 DPI and 480 DIP/240 DPI, including bottom actions | Four A6 populated views plus four A7 linked-items views and viewport assertions | Passed | Content scrolls; all content is not forced into one image |
| Keyboard access, visible focus styling and popup ownership | A6 native keyboard view assertions; A7 presentation keyboard assertions; existing button/menu/row focus styles inspected | Passed (automated) | Human keyboard and Narrator assessment unverified |
| Human/native/hosted acceptance | No new evidence supplied or performed | Unverified; deferred | Real clipboard/picker/viewer, installed codec, native journal restart and matching hosted timing/input remain outstanding |

The interaction audit includes name/persistence failures, repeated pending submissions, Save/Discard/Cancel consent, late completion after selection/deactivation, deleted folders, captured template identity, main blank creation after prior template use, menu dismissal, stale popup commands, pin failure, newer drafts during Discard confirmation, retained originals across image undo/redo and assignment removal without global tag deletion. A6 `test-audit.json` retains exact identities; `interaction-identities.txt` provides an extracted navigation/draft/creation index. The A6 scheduling regressions remain valid because no trait, collection, helper or dispatch changed. No union of earlier focused selections was rerun or summed.

### Commands and results

Only the existing four-case presentation family was rebuilt/run to close the viewport gap:

```powershell
& ./TestResults/m2212a7-verified/context.ps1 -OutputName before-context.json
& ./TestResults/m2212a7-verified/run.ps1 -Name viewport -Build -Filter 'FullyQualifiedName~NotebookPresentationViewTests'
& ./TestResults/m2212a7-verified/context.ps1 -OutputName after-context.json
& ./TestResults/m2212a7-verified/check-whitespace.ps1
& ./TestResults/m2212a7-verified/check-doc-links.ps1
```

The runner executes `dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore`, then `dotnet test` on that project with `-c Release --no-build --no-restore --filter 'FullyQualifiedName~NotebookPresentationViewTests' --logger trx --results-directory TestResults/m2212a7-verified/viewport`. Release build passed with **zero warnings/errors**, reported **54.92s**. Focused parent wall time, including child startup: **46.4102448s**; **4/4 parents passed**, zero failures/skips, exit **0**. Literal-path-safe TRX auditing confirms **four distinct matching children**, one completed result per parent and no duplicate/missing/skipped cases. The four parent identities overlap A6's 191; they do not establish 195 unique cases. `viewport/result.json`, `test-audit.json`, raw parent/child TRX, output and supervision artifacts retain the evidence. No failed A7 run was replaced.

The supported authorized input-desktop executor used interactive Windows session **1 / WinSta0 / Default**, successful cursor access and its existing administrator token, with no UAC/token/desktop switching. Before/after context records found no active tests. Test-created windows and native keyboard/capture automation do not establish human interaction or restart acceptance. Build/test results apply to baseline HEAD plus the presentation-test change; documentation was updated afterward. No runtime repeat was needed for documentation. Whitespace and local Markdown-target existence checks passed; remote URLs/fragment anchors are outside that link check. No schema/model/format changed, so no new EF consistency run was needed.

### Inspected visual evidence

Compared with `C:/Users/Yovko/Downloads/the target design reference.png`. **29 real WPF images were inspected**, with original-source/hash manifests `reused-renders.json` (25) and `added-renders.json` (four). Exact copied paths, using brace notation for alternatives:

- `reused/presentation-renders/{Light,Dark}-{1280-96,480-240}-populated.png` (four); `Dark-480-240-rows.png`; `Light-1280-96-tag-picker.png`.
- `reused/folder-action-renders/Light-1280-96-long-heading.png`; `Dark-480-240-{rename,validation}.png`.
- `reused/folder-creation-renders/Light-1280-96-expanded.png`; `Dark-480-240-validation.png`.
- `reused/header-renders/{Light-1280-96,Dark-480-240}-templates.png`; `Dark-480-240-overflow.png`; `Light-1280-96-busy.png`.
- `reused/m2210b-renders/Light-1280-96-empty.png`; `Dark-480-240-{long-title,save-error,archived}.png`; `{Light-1280-96,Dark-480-240}-note-menu.png`.
- `reused/notebook-autosave-renders/Light-1280-96-{recovered,saving}.png`; `Dark-480-240-{conflict,failed}.png`.
- `added-renders/{Light,Dark}-{1280-96,480-240}-linked-items.png` (four additional bottom viewport states).

No blocking integrated visual defect was found. Long headings/titles/tags remain bounded or wrap without covering actions; toolbar groups wrap; selection uses teal; disabled controls and contextual errors remain legible across themes. Existing focus styles provide keyboard borders and highlighted menu items. Both linked records and their controls, Refresh linked items and Link item are visible in the scrolled views. Menus, empty state, archived read-only state and recovery messages were checked in current renders.

Deliberate differences from the reference remain: established application shell/palette; pane switching at narrow widths; editable title surface; explicit checklist checked-state text; original/image-action controls; more detailed linked-record cards/identities and recovery messages; contextual Save/Discard. The reference's illustrative callout is not a newly supported block. Duplicated inline/global creation validation is nonblocking. Native workflows/restart and the final complete gate follow separately in M22.12B; no hosted execution, CI change, provider access, publication, schema/format change or unrelated redesign occurred. Historical A1–A6 records below retain their then-pending phase statuses.

Exact A7 inventory: **three modified files, no added source files and no pre-existing pending changes**:

- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookPresentationViewTests.cs` — one additional scrolled viewport and both linked-row visibility assertions; existing fixture, input assertions and dispatch preserved.
- `README.md` — current integrated acceptance/status summary.
- `docs/notebook.md` — requirement matrix, provenance, commands, captures, limitations and inventory.

Ignored `TestResults/m2212a7-verified/` scripts/manifests/logs/TRX/captures are retained evidence, not additional tracked implementation. A1–A6 production changes are already committed in the baseline and were not rewritten.

## M22.12A6 — Compact toolbar, assigned chips and note rows

23 October 2026 verification; documentation finalized 24 October 2026. Actual baseline: **clean develop / `213c2d744d17fb61784627e215e0f5c043ad4428`**, existing worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. A5 was already committed after the reported `40ec9ef0f3c9d079cb26d676d80764a3e7618679`; there were no pre-existing uncommitted changes. One agent; no repository/ancestor AGENTS.md found. Results below apply to this HEAD plus the A6 source inventory, not HEAD alone.

Formatting now uses 32-DIP buttons grouped beside the paragraph/heading selector, with application vector geometry, tooltips, accessible names and existing focus styles. Bold/italic/underline remain familiar letter glyphs. Lists, checklist creation, checked-state changes, hyperlink/image insertion and undo/redo remain separate reachable actions. Groups wrap at narrow widths. The hyperlink URL panel is contextual, uses owned focus, captures its opening document/range/note ownership and closes on editor-context replacement; Escape/Cancel dismiss it without mutation. Selection feedback no longer overwrites the URL draft while that panel is open, and Apply captures the URL before restoring its range. This fixes the selection-triggered URL reset demonstrated by the new keyboard test. Existing formatting, safe-paste and image-aware semantic history remain authoritative.

Assigned tags wrap as compact removable chips beneath the title, followed by **+ Add tag**. The contextual picker reuses existing assignment commands and expected-version/autosave coordination. Removal preserves the global tag. Long names wrap with full-name tooltips; errors remain outside the collapsible picker. Sidebar filters and global tag management retain their distinct controls. Note rows have a bounded two-line title and preview, existing NY date/time, a pin icon, two bounded tag badges and a remaining-tag count, with a teal selected background/edge. These properties read the existing bounded list snapshot; no row queries or document decoding were added. The document follows the compact toolbar without a stretched empty grid row; existing width-bounded image previews, captions and image actions remain intact. A5 title/status/menu/recovery and A1–A4 guards are preserved.

Focused evidence is retained under `TestResults/m2212a6-verified/`. The Release build, exact results, commands and dispatch audit are recorded in `summary.json`, `logic-final/`, `ui-final/` and `test-audit.json`. No complete Release gate ran. The new four-case native family uses the existing exclusive collection/trait, 90-second child deadline, 30-second watchdog and exact display-name selection/single-result validation. Existing scheduling limits and all old assertions remain unchanged.

Final focused results: **191/191 top-level Desktop tests passed** (169 logic + 22 UI parents), zero failures/skips, both test exits **0**. The UI audit separately verifies **34/34 child cases**, including four distinct single-result presentation children; child results are not added to parent totals. The four legacy class-wide view selections were each invoked by only one Light/1280 parent, yielding their four theme/width children once. Logic wall time: **13.733s**; UI wall time: **167.641s**, including child startup. These focused measurements are not a complete-gate or hosted timing claim. Final Release build: `logic-final/build.log`, **0 warnings/errors**, 7.92s. No EF model/schema files changed.

Commands (existing runner retains output, result JSON, TRX and child supervision):

```powershell
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore
& ./TestResults/m2212a6-verified/run.ps1 -Name logic-final -Build -Filter 'FullyQualifiedName~NotebookNoteActionsTests|FullyQualifiedName~NotebookEditingTests|FullyQualifiedName~NotebookAutosaveTests|FullyQualifiedName~NotebookLifecycleTests|FullyQualifiedName~NotebookWorkspaceTests|FullyQualifiedName~NotebookHeaderTests|FullyQualifiedName~NotebookFolderActionTests|FullyQualifiedName~NotebookFolderCreationTests|FullyQualifiedName~ReleaseSchedulingTests|FullyQualifiedName~NotebookCopyDeleteTests|FullyQualifiedName~NotebookImageCoordinationTests|FullyQualifiedName~NotebookImageAdmissionTests|FullyQualifiedName~NotebookTagTests|FullyQualifiedName~NotebookLinkTests|FullyQualifiedName~NotebookSqliteWorkspaceTests'
& ./TestResults/m2212a6-verified/run.ps1 -Name ui-final -Filter 'FullyQualifiedName~NotebookPresentationViewTests|FullyQualifiedName~NotebookLifecycleViewTests|FullyQualifiedName~NotebookHeaderViewTests|FullyQualifiedName~NotebookFolderActionsViewTests|FullyQualifiedName~NotebookFolderCreationViewTests|FullyQualifiedName~NotebookImageEditorTests|((FullyQualifiedName~NotebookViewTests|FullyQualifiedName~NotebookAutosaveViewTests|FullyQualifiedName~NotebookEditorViewTests|FullyQualifiedName~NotebookTagViewTests)&DisplayName~Light&DisplayName~1280)'
& ./TestResults/m2212a6-verified/audit.ps1
& ./TestResults/m2212a6-verified/check-whitespace.ps1
& ./TestResults/m2212a6-verified/check-doc-links.ps1
```

Coverage includes toolbar B/I/U and list/checklist actions through keyboard input, checklist toggle distinct from insertion, captured-range hyperlink application and dismissal, image-picker cancellation, reference-safe undo/redo, tag assignment/removal with global-tag preservation and retained editor, bounded snapshot tags, context replacement, and existing safe-paste, image, autosave/recovery, lifecycle/menu and A1–A5 draft/selection regressions. The native keyboard helper checks readiness, one click and actual capture release; no direct-command substitute or deadline relaxation was introduced.

Development artifacts remain intact: `build-tests.log` records a new-fixture task-property typo corrected to existing ImageOperationTask; `presentation-first` records the incorrect initial Undo/Redo-enabled fixture expectation (4 failures); `presentation-selection` records the URL reset defect (1 failure); `presentation-link-fixed` records an overbroad document-identity fixture assertion after image-aware Undo had legitimately replaced the FlowDocument (1 failure). The corrected fixture uses existing reference-history toolbar Undo and checks retained document identity across tag operations themselves. `presentation-history-fixed` passed the representative case before extending formatting assertions. These development runs are excluded from final totals; no original assertion was weakened. An EOF-only whitespace correction after final testing has no runtime effect.

The UI run used the existing explicitly approved input-desktop executor: interactive Windows session 1, WinSta0/Default, cursor access successful, existing administrator token; no UAC/token/desktop switching. `before-context.json` records no active tests before launch; `after-context.json` confirms no remaining test processes at completion. This is automated WPF/native keyboard and capture verification, not human interaction or a native journal restart.

Twelve final real WPF renders were inspected: `renders/{Light,Dark}-{1280-96,480-240}-{populated,rows,tag-picker}.png`. `renders.json` maps copies to original isolated-child paths and SHA-256 hashes. The populated fixture contains headings, paragraph text, a checklist, three assigned tags including a long name, a synthetic chart with caption, and trade/setup links. All four theme/layout combinations showed readable controls/chips, bounded rows, wrapped toolbar groups and width-contained images without an A6 blocking defect. Lower content remains accessible through existing scrolling; the narrow layout retains Organization/Notes/Content switching. Normal-width row captures also include the populated editor because all three panes remain visible.

Deliberate reference differences: the application shell/palette and A5 editable title surface remain; checklists retain their explicit checked-state text, images retain original/actions controls, and linked records retain existing cards. The illustrative reference callout is not a supported new block. No new document feature, persistence/schema/format change, autosave interval change, provider access, CI change or publication occurred. Human interaction, native journal restart, real clipboard/picker/viewer, installed-codec behavior and matching hosted execution remain unverified by this phase. **A7 and M22.12B remain pending.**

Final supporting checks passed: tracked/new-file whitespace, scheduling regression and **209 local Markdown targets with zero missing** across README, Notebook and backup/restore documentation. The link check excludes remote URL and anchor validation. No runtime suite was repeated for documentation or whitespace-only edits.

Exact A6 inventory (10 files; no pre-existing pending files):

- `src/PersonalTradingJournal.Desktop/Resources/Icons.xaml` — seven geometry resources in the existing icon system.
- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.cs` — bounded row tag presentation.
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml` — toolbar, chips, rows and content spacing.
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml.cs` — contextual panel reset, URL/selection handling and informative formatting tooltips.
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.Toolbar.cs` (added) — panel toggles, owned focus, range ownership and dismissal.
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookPresentationViewTests.cs` (added) — four exact isolated keyboard/layout cases and populated captures.
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookImageEditorTests.cs` — existing synthetic chart presentation fixture made internal for reuse; assertions/dispatch unchanged.
- `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/ReleaseSchedulingTests.cs` — require the new native family in the existing exclusive lane.
- `README.md` and `docs/notebook.md` — phase record. Ignored TestResults artifacts are evidence, not additional source inventory.

## M22.12A5 — Compact note header and owned note actions

23 October 2026. Actual baseline: **clean develop / `40ec9ef0f3c9d079cb26d676d80764a3e7618679`**, existing worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. The reported A4 changes were already committed in that successor to `9cb925c86dd7ec4150668ce847b29c4c17375204`. No pending baseline changes were overwritten. Repository/ancestor guidance search found no AGENTS.md. One agent; A5 only.

The permanent management and lifecycle button rows are replaced with compact folder/save context, a 28-point editable title and top-right pin/ellipsis controls. Titles wrap independently of those controls and scroll within a bounded 180-DIP field for long input. Existing title validation/read-only bindings remain. The pin uses the existing application geometry, outlined when unpinned and filled/accented when pinned, with authoritative persisted state, dynamic tooltip/accessibility name and existing keyboard focus styling. Failed pin operations leave the persisted state displayed.

A single **note-specific** themed overflow, separate from the Notebook overflow, offers Move to folder, Duplicate, Archive/Return to Active, Move to Trash/Restore and Trash-only permanent deletion according to existing command capabilities. Popup commands explicitly connect to the current view model and capture note identity, accepted-selection generation and activation. Opening/Escape dismissal is read-only. A retained command cannot act after selection changes, even after returning to the original note. Move opens a compact contextual picker using the existing folder catalog, destination capture and guarded Move command; Cancel/Escape dismisses it. Failure feedback remains beside the picker. A verified destination change collapses it without taking focus; a same-folder no-op can be dismissed with Cancel.

Existing Save/Discard/Cancel consent, destructive confirmations and atomic lifecycle/duplication paths remain authoritative. Pin/Move now revalidate captured ownership after draft preparation. Trash revalidates after its modal confirmation and before its canonical operation. Review also identified that explicit Discard previously resumed after confirmation without checking whether the draft had changed. It now captures the context and draft revision and checks both before checkpoint cleanup and before restoring the draft. The checkpoint helper accepts an optional ownership predicate around its existing drain/CAS cleanup; other callers retain their existing behavior. Deterministic tests cover newer input and changed selection during confirmation. No autosave interval, expected-version write, image ownership, undo/redo, schema, document/archive v1 or export v18 behavior was changed.

Compact save feedback is derived from existing saving, dirty, paused/conflict, detached-recovery and loaded-note state. Detailed SaveState, recovery messages and errors remain adjacent to contextual Save/Discard while dirty; Save/retry and Discard also remain in the menu, and Control S is unchanged. A checkpointed draft is explicitly described as **not yet saved as a note**, and a failed canonical write never displays Saved locally. Recovered/conflicting drafts retain explanatory messages and their actions. With no note/recovery context, the editor and management controls collapse to a selection prompt. Unavailable content retains its explanatory/read-only presentation and rejected commands stay disabled. Formatting, tags and linked-item controls are unchanged for A6.

**Final focused Release result: 186/186 distinct top-level Desktop tests**, zero failures/skips, exits **0**, with **26 child cases counted separately**:

| Selection | Passed/total | Command wall time |
| --- | --- | --- |
| `logic-final/`: note actions, editing/autosave, lifecycle/copy/delete, image coordination/admission, links/tags, SQLite, A1–A4 and scheduling | 169/169 | 13.579s |
| `views-final/`: lifecycle, header/folders, editor/images, workspace and recovery | 17/17 parents; 26 children | 129.625s |

The four lifecycle parents now exercise actual menu keyboard selection for every existing lifecycle action, duplication and confirmed deletion, retaining exact content/read-only/confirmation assertions. They additionally verify title binding, pin success/failure, menu dismissal without writes, move through the picker, stale popup commands and stale picker closure. All four Light/Dark × 1280/96-DPI and 480/240-DPI rows remain. Header/folder creation/folder actions each have two exact children; image editing has four exact children. Workspace, editor and autosave each use one selected parent with their established four-case child suite. The 17 isolated launches therefore contain 26 distinct children. Literal-path-safe `trx-audit.json` verifies counters, unique identities, exact dispatch correspondence and class-suite cardinality. No deadlines, assertions, collection limits, scheduling or isolation settings changed.

Commands from repository root (exact complete filters are retained in each `result.json` and `summary.json`):

```powershell
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore
$env:PTJ_TEST_RESULTS_DIRECTORY = (Resolve-Path TestResults/m2212a5-verified/<selection>).Path
dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter '<recorded filter>' --logger trx --results-directory $env:PTJ_TEST_RESULTS_DIRECTORY
& ./TestResults/m2212a5-verified/audit.ps1
git -c core.safecrlf=false diff --check
& ./TestResults/m2212a5-verified/check-doc-links.ps1
```

Final Release build: **zero warnings/errors, 15.46s**, `logic-final/build.log`. Both final selections used the unchanged runtime/test source from that build, recorded by hashes in `verified-source.json`; results belong to HEAD **plus A5's uncommitted changes**. Evidence root **`TestResults/m2212a5-verified/`** includes summary, source provenance, raw TRX, stdout/stderr, process-supervision and keyboard diagnostics. Earlier passing selections remain retained but are excluded from final totals. Initial `build.log` preserves MC3072 from using an incorrect attached-property name; it was corrected to the existing `EntityActionMenu.OpensContextMenu`. No tests launched against that failed build. No test run failed, and no complete gate ran.

Fourteen current captures under **`TestResults/m2212a5-verified/renders/`** were inspected: `Light-1280-96-` and `Dark-480-240-` variants of **template-active, empty, long-title, save-error, note-menu, recovered and conflict** (all PNG). `renders.json` records original paths, timestamps and hashes. The first five states come from final lifecycle fixtures; recovery/conflict come from the final autosave fixtures' existing bin-directory render output, copied into durable evidence. Menus are captured from live fixtures, while lifecycle workspace captures use detached render surfaces to avoid desktop clipping. No blocking contrast, overlap or accessibility-action visibility defect was observed. Long titles intentionally scroll within their bounded field, and narrow layouts retain the existing Content pane/vertical scrolling. The title keeps the application's input styling; the supplied reference guides hierarchy, while toolbar/tag/link styling remains for A6.

`before-context.json` and `final-context.json` confirm the authorized **session 1 / interactive WinSta0 / Default**, cursor available/error 0, existing executor token, and no independent active tests before/after. There was no desktop/token switching. Native keyboard probes and focused MenuItem key events are automated evidence; human keyboard/Narrator interaction, real clipboard/picker/viewer, native journal restart and matching hosted timing/input compatibility remain unverified.

Exact A5 inventory: **nine modified + two added files (11 unique)**. A1–A4 were committed at baseline; there were no pre-existing uncommitted files to include. Modified:

- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Editing.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Autosave.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Lifecycle.cs`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookLifecycleViewTests.cs`
- `README.md`
- `docs/notebook.md`

Added: `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.NoteActions.cs`, `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookNoteActionsTests.cs`.

Whitespace and local documentation-target checks passed after documentation edits; no EF model/schema change requires a migration check. Historical acceptance evidence is preserved. **A6–A7, M22.12B and overall M22.12 acceptance remain pending.** No CI changes, production/provider access, publication or workflow triggering occurred.

## M22.12A4 — Compact Notebook header

23 October 2026. Actual baseline: **clean develop / `9cb925c86dd7ec4150668ce847b29c4c17375204`**, existing worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. A3's reported pending changes had already been committed; no pre-existing modifications were overwritten. Repository/ancestor guidance search found no AGENTS.md. One agent; A4 only.

The header retains search and a compact split **+ New note** control. Its main action and Control N still invoke `NewNoteCommand`, always creating an Untitled blank note regardless of prior template selection. The arrow uses the existing EntityActionMenu and themed menu resources, with the existing chevron icon rotated downward. All five definitions remain available: Blank Note, Market Study, Trading Idea, Lesson Learned and Psychology. Opening/Escape dismissal writes nothing; selecting a definition captures it synchronously before the existing `CreateTemplateCommand` captures its ID. Both entry points share the existing note-operation guard, preventing overlapping submissions. Creation still sends one `CreateNotebookNote` with final template content; there is no blank-write/content-update sequence.

Standalone template selection, Create from template, Refresh and permanent Cancel controls are removed. Refresh is appended after a separator in the **same** Notebook overflow as Unfiled, Archived and Trash; on opening, it connects to the current placement owner's existing Refresh command. A1 scope identities/counts and A2/A3 folder controls remain unchanged. Search text binding, Search/Enter submission, filters and stale-result protection are unchanged. Source inspection found an existing **300 ms search debounce**; A4 neither adds live search nor changes that established behavior.

Cancellation was traced before removing the binding: `CancelAsync` invalidates the read generation, cancels lifetime/search/read/write work, drains completion tasks, restores applied filters and renews the active lifetime, with feedback that a completed change may already be saved. Its implementation and navigation/disposal cancellation remain unchanged. A contextual **Cancel operation** button, bound to the same command, is visible only while `IsBusy`; loading and failure feedback remain visible. Save/Discard/Cancel dialogs remain authoritative. Confirmed commits after cancellation retain their existing acknowledgement semantics rather than implying rollback.

Creation publication now checks the captured activation before/after refresh. It switches to Content and raises the creation-focus event only while the original pane generation remains current and cancellation has not occurred. View-owned PageFocus replaces unconditional title focus: input generation and original/menu-owner focus are checked before requesting focus, and queued focus still respects newer input, context and visibility. The native view regression moves focus to a separate shell control during creation and verifies it remains there afterward. Original autosave/recovery, newer-edit and failure-retention assertions are retained.

**Final focused verification: 141/141 distinct top-level tests — Desktop 133/133, Infrastructure 8/8**, zero failures/skips, all final exits **0**. Child results are separate:

| Selection | Passed/total | Test-command wall time |
| --- | --- | --- |
| A4 header (`header-connected/`) | 2/2 parents, two exact children | 21.708s |
| Creation/lifecycle/search/cancellation and A1–A3 logic (`logic/`) | 122/122 | 13.720s |
| Lifecycle and A1–A3 views (`views/`) | 9/9 parents, 12 children | 77.521s |
| Atomic template storage (`storage/`) | 8/8 | 9.820s |

Four lifecycle parents retain their complete Light/Dark × normal/narrow matrix and all lifecycle assertions, now entering template creation through the menu. Header, A2 and A3 each have two exact owned-window children; the representative A1 parent invokes its existing four-row suite once. Thus **11 view parents / 14 children across 11 isolated launches**, without adding child counts to the top-level total. Literal-path-safe TRX auditing verifies complete/pass counts, unique final parent identities and exact child correspondence. Existing exclusive-first membership, collection/concurrency limits, process isolation, 90-second process deadlines and 30-second watchdogs remain unchanged; the new header family is added to the existing exclusive scheduling expectation.

The header tests exercise keyboard Space opening, focused MenuItem Enter selection and Escape dismissal, all five exact initial template projections, one write/version 1 per creation, blank main action after prior template selection, current Refresh-command identity and activation, contextual cancellation/draining, accessible names and external-focus preservation. Logic checks additionally cover template capture before awaited saving, competing main/menu submissions, newer pane selection, cancellation/commit acknowledgement, failed creation, dirty-draft consent, autosave/recovery and stale reads. Existing Infrastructure tests verify final document visibility at one commit, failure/cancellation rollback and post-commit cancellation acknowledgement through fresh contexts.

Commands from repository root; `run.ps1` retains the exact project/filter, original output, TRX, exit and command time for each selection:

```powershell
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore
dotnet build tests/PersonalTradingJournal.Infrastructure.Tests/PersonalTradingJournal.Infrastructure.Tests.csproj -c Release --no-restore
$env:PTJ_TEST_RESULTS_DIRECTORY = (Resolve-Path TestResults/m2212a4-verified/<selection>).Path
dotnet test <project.csproj> -c Release --no-build --no-restore --filter '<filter>' --logger trx --results-directory $env:PTJ_TEST_RESULTS_DIRECTORY
```

Exact final filters in table order; only `storage/` uses the Infrastructure project:

```text
FullyQualifiedName~NotebookHeaderViewTests
FullyQualifiedName~NotebookHeaderTests|FullyQualifiedName~NotebookLifecycleTests|FullyQualifiedName~NotebookFolderActionTests|FullyQualifiedName~NotebookFolderCreationTests|FullyQualifiedName~NotebookWorkspaceTests|FullyQualifiedName~NotebookEditingTests|FullyQualifiedName~NotebookAutosaveTests|FullyQualifiedName~NotebookTagTests|FullyQualifiedName~NotebookSqliteWorkspaceTests|FullyQualifiedName~ReleaseSchedulingTests
FullyQualifiedName~NotebookLifecycleViewTests|FullyQualifiedName~NotebookFolderActionsViewTests|FullyQualifiedName~NotebookFolderCreationViewTests|(FullyQualifiedName~NotebookViewTests&DisplayName~Light&DisplayName~1280)
FullyQualifiedName~NotebookTemplateLifecycleTests
```

Final Release builds passed with **zero warnings/errors**: Desktop MSBuild **14.25s**, Infrastructure **5.40s**. All final selections used unchanged source after the successful Desktop build. Evidence root **`TestResults/m2212a4-verified/`** contains `summary.json`, `verified-source.json`, `trx-audit.json`, `development-failures.json`, per-selection build/output/result files, original parent/child TRX and supervision/phase artifacts. Results describe the baseline **plus A4's source changes**, not HEAD alone. No complete gate ran.

Development evidence remains distinct from final passes: `header/build.log` contains four CS0119 errors caused by the new test helper shadowing WPF's Key enum (no tests launched). `header-verified/` records **0/2 parents passed, two failed, no skips**, with two matching child failures at the retained Refresh-command identity assertion: the popup's declarative command binding was null. Refresh now explicitly receives the current owner's command on opening. `header-final/build.log` retains the initial correction's UIElement/DataContext cast compile error (no tests launched). After correcting the cast, `header-connected/` passed both complete cases, including that exact assertion and keyboard Refresh. No deadlines or assertions were weakened.

Eight current captures under **`TestResults/m2212a4-verified/renders/`** were inspected: `Light-1280-96-header.png`, `Light-1280-96-templates.png`, `Light-1280-96-overflow.png`, `Light-1280-96-busy.png`, and the corresponding four `Dark-480-240-*.png` files. `renders.json` retains original child paths and SHA-256 hashes. The reduced header, split control, full template names, existing overflow scopes/Refresh and contextual cancel/status remain readable without blocking overlap or contrast defects. Workspace captures use detached render surfaces; open menus come from the live fixtures. Existing editor action rows, formatting toolbar and application shell are intentionally retained for A5/A6.

`before-context.json` and `final-context.json` record the authorized **session 1 / interactive WinSta0 / Default**, cursor available/error 0 and existing executor token, without desktop/token switching or independent active tests before/after. Tests use existing UiKeyboardProbe readiness/capture/release and routed keyboard events on focused menu items; human interaction, real clipboard/picker/viewer, native journal restart, installed-codec WebP and matching hosted timing/input compatibility remain unverified.

Exact A4 inventory: **eight modified files and two added files (10 unique)**. A3 was committed at baseline; its folder implementations/tests are unchanged. Modified:

- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Lifecycle.cs`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookLifecycleViewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookViewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/ReleaseSchedulingTests.cs`
- `README.md`
- `docs/notebook.md`

Added: `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookHeaderTests.cs`, `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookHeaderViewTests.cs`.

Whitespace (`git -c core.safecrlf=false diff --check`) and local documentation-target checks passed after edits. There is no schema/model/format change requiring EF work; document/archive v1 and export v18 remain unchanged. Historical M22.11 acceptance remains passed. **A5–A7, M22.12B and overall M22.12 acceptance remain pending**. No CI changes, production/provider access, publication or workflow triggering occurred.

## M22.12A3 — Folder actions in the note-list header

23 October 2026. Actual baseline: **clean develop / `41bbf0eae9593688bcf667b8156fefb47bd6f01a`**, existing worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. The reported A2 changes had already been committed; there were no pending baseline modifications to overwrite. Repository/ancestor guidance search found no AGENTS.md. One agent; A3 only.

Actual folder scopes show their name and existing `PtjIconEdit`/`PtjIconDelete` icon buttons in the middle-pane header. Names wrap independently of the right-hand actions. Tooltips, accessible names and standard keyboard button behavior are supplied; All Notes, Pinned, Unfiled, Archived and Trash have no folder actions. Rename replaces the heading with a compact field, prefilled from the selected folder and independent of A2's creation draft. Enter submits; Escape dismisses without mutation and uses PageFocus to return to the appropriate visible pencil button. Validation/persistence errors retain the draft beside the field. Duplicate submissions are rejected while pending. Completion never requests keyboard focus, so a newer interaction cannot be displaced. The old Manage folders container and its picker/rename/delete/clear controls are removed; A2's New folder interaction is unchanged.

Rename/delete capture the intended ID and expected version before any confirmation or awaited work. Request, activation, accepted-note selection, read generation, scope and panel ownership are checked before mutation and publication. Accepted scope navigation dismisses rename mode rather than applying its draft to the next folder. Existing Save/Discard/Cancel and autosave guards remain authoritative. Successful rename refreshes the catalog/heading while retaining folder identity, selected note and filters. A failed mutation retains actionable feedback; stale rename after external deletion returns NotFound and never creates a replacement folder. Dismissal/cancellation during an already submitted operation is not a rollback claim; cancelled or abandoned results require Refresh to inspect persisted state.

Deletion uses the existing destructive confirmation pattern, identifying the captured folder and explaining **Unfiled, no notes/content deleted**. Confirmation now precedes draft consent: declining deletion cannot save/discard a dirty note. Selection changed by a modal confirmation invalidates the request. While persistence owns the view, scope changes are blocked by the existing write guard; cancellation drains the operation before later selection. On confirmed success, the existing Unfiled fallback retains the selected note when it still matches the unchanged filters. Sidebar counts, list heading and editor reload from storage.

The unchanged storage contract removes the folder in its transaction, clears affected current/previous folder references and advances affected note versions/timestamps. It does not decode/re-encode document content or delete notes; archived/trash note states remain intact. Expected-version conflicts and rollback behavior are preserved. Schema, document/archive v1, export v18, CI, application header, templates and note-action toolbar are unchanged.

**Focused Release results: 118/118 distinct top-level tests — Desktop 106/106, Infrastructure 12/12**, zero failures/skips, all exits **0**. The eight isolated child cases are separate evidence, not added to these totals:

| Selection | Passed/total | Command wall time |
| --- | --- | --- |
| Desktop coordination, SQLite, A1/A2 and scheduling (`logic/`) | 98/98 | 12.854s |
| A3/A2 owned views and representative A1 parent (`views/`) | 5/5 | 40.212s |
| Existing storage contract (`storage/`) | 12/12 | 10.475s |
| Additional deletion-failure/stale-deleted-folder boundaries (`failure-boundaries/`) | 3/3 | 7.461s |

The A3 and A2 view families each retain two visible parents, Light/1280/96 and Dark/480/240, with exactly one matching child per parent. The representative A1 parent runs its existing four-row Light/Dark normal/narrow suite once. Literal-path-safe TRX audit confirms complete executed/pass counts, no duplicate top-level identities and exact A3/A2 child correspondence. New logic checks cover rename success/cancel/validation/storage/conflict/NotFound, selected-note/filter retention, delete cancellation before draft consent, exact content retention and Unfiled counts, pending duplicates, blocked selection during writes, cancellation/late completion, modal selection changes and dirty-draft veto. Existing SQLite conflict/fresh-service and storage tests preserve their original assertions; the additional SQLite stale-rename case verifies the deleted folder remains absent.

Commands from repository root (`run.ps1` retains exact filters, logs, exits and measured test-command times):

```powershell
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore
dotnet build tests/PersonalTradingJournal.Infrastructure.Tests/PersonalTradingJournal.Infrastructure.Tests.csproj -c Release --no-restore
# For each selection below, set its own results directory and project:
$env:PTJ_TEST_RESULTS_DIRECTORY = (Resolve-Path TestResults/m2212a3-verified/<selection>).Path
dotnet test <project.csproj> -c Release --no-build --no-restore --filter '<filter>' --logger trx --results-directory $env:PTJ_TEST_RESULTS_DIRECTORY
```

Exact filters, in table order (only `storage/` uses the Infrastructure test project):

```text
FullyQualifiedName~NotebookFolderActionTests|FullyQualifiedName~NotebookFolderCreationTests|FullyQualifiedName~NotebookWorkspaceTests|FullyQualifiedName~NotebookEditingTests|FullyQualifiedName~NotebookAutosaveTests|FullyQualifiedName~NotebookTagTests|FullyQualifiedName~NotebookSqliteWorkspaceTests|FullyQualifiedName~ReleaseSchedulingTests
FullyQualifiedName~NotebookFolderActionsViewTests|FullyQualifiedName~NotebookFolderCreationViewTests|(FullyQualifiedName~NotebookViewTests&DisplayName~Light&DisplayName~1280)
FullyQualifiedName~NotebookPersistenceTests|FullyQualifiedName~NotebookLifecycleImageTests
FullyQualifiedName~FailedDeletionRetainsFolderNoteAndActionableFeedback|FullyQualifiedName~StaleInlineRenameNeverRecreatesExternallyDeletedFolder
```

All affected Release builds passed with **zero warnings/errors**. Initial Desktop build: MSBuild **33.76s**; final pre-selection test additions: **8.77s**; Infrastructure: **5.66s**; final three additive boundary tests: **7.60s**. Production source remained unchanged throughout executed tests. Passing functional/view selections were reused after the last three tests were added and compiled; those three ran separately. No failed tests or test retries occurred; no full Release gate ran. New native view coverage uses the existing exclusive WPF trait/collection and its additive scheduling assertion. Concurrency, collection limits, exact child dispatch/completeness, 90-second process limit, 30-second watchdog and cleanup remain intact.

Evidence root **`TestResults/m2212a3-verified/`** contains `summary.json`, `verified-source.json`, `trx-audit.json`, `run.ps1`, per-selection logs/results and original TRX, child phase/supervision artifacts, build logs and `renders.json`. Results apply to the baseline **plus the A3 source inventory below**, not HEAD alone. `before-context.json` and `final-context.json` retain the supported, authorized **session 1 / interactive WinSta0 / Default**, cursor access/error 0 and existing executor token, without desktop/token switching or active tests before/after. Live focus is asserted in case-owned windows using existing bounded UiKeyboardProbe and focused-field routed Enter/Escape events. Human/native keyboard interaction and real modal-dialog interaction are not claimed.

Six current A3 captures were inspected under **`TestResults/m2212a3-verified/renders/`**: `Light-1280-96-long-heading.png`, `Light-1280-96-rename.png`, `Light-1280-96-validation.png`, `Dark-480-240-long-heading.png`, `Dark-480-240-rename.png`, `Dark-480-240-validation.png`. Original child paths and copied SHA-256 hashes are in `renders.json`. Long headings wrap without displacing either action; icons, inline field and error contrast are readable. The compact single-line edit field scrolls long text; the normal heading exposes the full name. No blocking A3 visual defect was found. Captures are detached view renders, while focus assertions use live owned windows. The established shell and tall header/editor controls remain deliberate differences for later phases.

Exact A3 inventory: **nine modified files and three added files (12 unique)**. A2 was committed at baseline; its creation tests and implementation behavior are preserved. Modified files:

- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Folders.cs`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookWorkspaceTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookSqliteWorkspaceTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/ReleaseSchedulingTests.cs`
- `README.md`
- `docs/notebook.md`

Added files:

- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.FolderActions.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookFolderActionTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookFolderActionsViewTests.cs`

Whitespace (`git -c core.safecrlf=false diff --check`) and local documentation-target checks passed after edits. No model/schema change requires EF migration work. Historical M22.11 acceptance remains passed. **A4–A7, M22.12B and overall M22.12 acceptance remain pending**. Human keyboard/Narrator, native journal restart, real clipboard/picker/viewer, installed-codec WebP and matching hosted timing/input compatibility remain unverified. No production/provider access, publication or workflow triggering occurred.

## M22.12A2 — Inline folder creation

23 October 2026 (implementation began 22 October). Actual baseline: clean **develop / `92dce641d1c4c492d43b167313bfe6dd6880cfd3`**, existing worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. A1's seven files had already been committed in intervening commit `92dce64`; they were not uncommitted baseline changes. No applicable repository/ancestor AGENTS.md was found. One agent; only A2 implemented.

The **+ New folder** button below FOLDERS reveals one compact input. The existing PageFocus owner mechanism focuses it; Enter submits and Escape dismisses, returning focus to the button only while the request still owns the appropriate visible context. Blur does not submit. Existing name validation and catalog commands remain authoritative; inline validation/storage/cancellation feedback retains the entered name. Pending creation disables editing/submission while keeping Escape usable. The old Manage folders Create button is removed; its rename/delete controls remain reachable until A3. Search/tag semantics, A1 icons/counts/overflow and refresh/cancelled-navigation highlighting are preserved.

Creation uses existing Save/Discard/Cancel consent and catalog persistence. Request, activation, accepted-selection/read and panel ownership prevent stale completion from selecting a folder or requesting focus. Ownership is rechecked around awaited navigation consent; closing the input before persistence prevents the folder command. A write already committed is not reversed by dismissal: when still active, the returned folder is made available in FOLDERS with truthful feedback, without taking selection or clearing the retained input. Deactivation suppresses publication; a later refresh reads committed state. Successful owned creation refreshes counts/list through the normal guarded scope transition, selects the new folder, updates the middle heading, and clears/collapses the field. A new folder is reachable even beyond the first catalog page. No persistence/schema/service-contract change was made.

**Focused verification: 89/89 top-level tests passed**, zero failures/skips, all command exits **0**. Owned Light/1280/96 and Dark/480/240: **2/2 parents**, each with exactly one matching distinct child, command **27.573s**. Logic/A1 selection: **86/86**, **23.296s**, including 15 new creation cases and one representative A1 parent that dispatches the existing four Light/Dark normal/narrow children once. Scheduling regression: **1/1**, **6.520s**. The **six children are counted separately**, not added to the top-level total. Literal-path-safe TRX audit verifies complete executed counts, passing outcomes, unique names per run and exact owned-child identity correspondence. No tests failed or were retried; no complete gate ran.

Coverage includes open/dismiss, blank/invalid names, persistence failure/cancellation, pending duplicate submission, success beyond the first catalog page, retained search, dirty-draft Save/Discard/Cancel, stale consent, dismissed/panel-changed/deactivated late completion, and A1 refresh/cancelled-navigation selection. Live owned-window assertions verify field focus, Escape focus return, blur without persistence, Enter validation/success and selected-folder highlighting. The new native-focus family uses the existing exclusive WPF trait/collection; the scheduling expectation was extended additively. Concurrency, collection limits, 90-second process deadlines, 30-second watchdogs, exact child selection/completeness and production focus guards are unchanged.

Commands from repository root:

```powershell
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore
# Each selection uses its own owned/, logic/ or scheduling/ evidence directory:
$env:PTJ_TEST_RESULTS_DIRECTORY = (Resolve-Path TestResults/m2212a2-verified/<selection>).Path
dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter '<filter below>' --logger trx --results-directory $env:PTJ_TEST_RESULTS_DIRECTORY
```

Exact filters, respectively:

```text
FullyQualifiedName~NotebookFolderCreationViewTests
FullyQualifiedName~NotebookFolderCreationTests|FullyQualifiedName~NotebookWorkspaceTests|FullyQualifiedName~NotebookEditingTests|FullyQualifiedName~NotebookAutosaveTests|FullyQualifiedName~NotebookTagTests|FullyQualifiedName~NotebookSqliteWorkspaceTests|(FullyQualifiedName~NotebookViewTests&DisplayName~Light&DisplayName~1280)
FullyQualifiedName~ReleaseSchedulingTests
```

Both Release builds passed with **zero warnings/errors**: initial functional build MSBuild **50.37s**; final scheduling-expectation rebuild **40.23s** (command **40.851s**). Only the scheduling test's expected exclusive-class list changed between builds; production and functional-test sources were unchanged, so passing functional selections were reused. Evidence root **`TestResults/m2212a2-verified/`** contains `summary.json`, `verified-source.json`, `trx-audit.json`, both build logs, per-selection `output.log`/`result.json`, original parent/child TRX, supervision and phase artifacts. Tests apply to the baseline plus the A2 source inventory below, not HEAD alone.

`before-context.json`, `resumed-context.json` and `final-context.json` record authorized session **1 / interactive WinSta0 / Default**, cursor access/error 0 and the executor's existing token; no token/desktop switch. No independent tests were active before execution or after cleanup. The initial launch request could not be reviewed because the approval service reported a usage limit; it launched no tests. After the user requested continuation, the supported executor was checked again and the first actual focused runs proceeded. Enter/Escape tests raise routed key events on the verified focused field; button activation uses the existing bounded UiKeyboardProbe. This is automated input/focus evidence, not human keyboard/Narrator acceptance.

Six current captures were inspected under **`TestResults/m2212a2-verified/renders/`**: `Light-1280-96-collapsed.png`, `Light-1280-96-expanded.png`, `Light-1280-96-validation.png`, `Dark-480-240-collapsed.png`, `Dark-480-240-expanded.png`, `Dark-480-240-validation.png`. `renders.json` retains original child paths and copy hashes. The button sits below FOLDERS; the single field and wrapping validation remain readable in both representative layouts/themes, with no blocking A2 defect. These are detached snapshots; live focus assertions come from the owned window. Existing global operation feedback can also display the validation message. The tall header, rename/delete management interface and editor controls deliberately remain for later phases.

Exact A2 inventory: **eight modified files and three added files (11 unique)**. Five modified files overlap A1's committed inventory; A1's `Icons.xaml` and `NotebookViewTests.cs` are unchanged in A2.

- Modified, shared with A1: `README.md`, `docs/notebook.md`, `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.cs`, `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml`, `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml.cs`.
- Other modified files: `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Editing.cs`, `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookWorkspaceTests.cs`, `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/ReleaseSchedulingTests.cs`.
- Added: `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Folders.cs`, `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookFolderCreationTests.cs`, `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookFolderCreationViewTests.cs`.

Whitespace (`git -c core.safecrlf=false diff --check`) and local documentation-target checks passed after edits; no EF/model change requires migration work. Historical M22.11 acceptance remains passed. **A3–A7, M22.12B and overall M22.12 acceptance remain pending**. Human keyboard/Narrator, native journal restart, real clipboard/picker/viewer, installed-codec WebP and matching hosted timing/input compatibility remain unverified. No CI change, production/provider access, publication or workflow triggering occurred.

## M22.12A1 — Workspace navigation and icons

22 October 2026. Baseline: clean **develop / `040272b07a925bdbc72716d69bc12e9e48616da4`**, existing worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`; prior A and CI-budget work were already committed. Repository/ancestor guidance search found no AGENTS.md. One agent; only A1 from the supplied roadmap was implemented. The current-implementation and target-reference screenshots supplied in this turn guided the navigation hierarchy; later header/editor changes were excluded.

WORKSPACE contains only All Notes and Pinned. FOLDERS uses its own filtered catalog view, document/pin/folder geometries live in the existing `Icons.xaml` resource system, and names wrap with full-name tooltips and accessible count labels. The compact ellipsis beside WORKSPACE uses the existing `EntityActionMenu` behavior and themed menu styles for Unfiled, Archived and Trash. Counts and all-selected-tags explanations are in collapsed, keyboard-expandable **Counts & filters** help, with row accessibility help retained. Existing scope commands, filtering, paging, folder management and navigation guards remain authoritative; persistence, export v18 and document/archive v1 are unchanged.

Two selection issues exposed by the new assertions were corrected: catalog publication must notify when an equal-valued record is replaced, and the view must explicitly clear the other list when the accepted scope is not in it. The canonical Scope setter still performs navigation/dirty-draft coordination. Refresh restores the selected catalog instance; rejected navigation restores the accepted highlight and preserves the exact dirty title. No save/discard bypass was introduced.

**Final focused verification passed:** **44/44 top-level tests**, comprising 43 workspace/tag/editing regressions and one representative view parent. That parent invokes the existing whole four-row child suite once: Light/Dark × 1280/96 DPI and 480/240 DPI, **4/4 distinct children**. Both parent and child TRX have zero failed, skipped, aborted or missing cases and no duplicate names. Children are not added to the parent total. Final command exit **0**, wall time **14.013s**; Release build passed with zero warnings/errors (final MSBuild **13.32s**). Existing process isolation, 90-second process limit, 30-second watchdog and dispatch implementation are unchanged. No complete gate was run.

```powershell
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore
$env:PTJ_TEST_RESULTS_DIRECTORY = (Resolve-Path TestResults/m2212a1-verified/selection-verified).Path
dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~NotebookWorkspaceTests|FullyQualifiedName~NotebookTagTests|FullyQualifiedName~NotebookEditingTests|(FullyQualifiedName~NotebookViewTests&DisplayName~Light&DisplayName~1280)' --logger trx --results-directory $env:PTJ_TEST_RESULTS_DIRECTORY
```

Evidence root: **`TestResults/m2212a1-verified/`**. `summary.json`, `verified-source.json` (five tested source files and binary SHA-256 hashes), `trx-audit.json`, `selection-verified/build.log`, `selection-verified/focused.log`, `selection-verified/result.json` and its original parent/child TRX, supervision and phase logs retain final results. `before-context.json` and `final-context.json` record the already-authorized session 1 / interactive WinSta0 / Default with cursor access and the executor's existing token, without desktop/token switching or active tests before/after. Fixtures use detached renders and programmatic routed events; native human keyboard/focus interaction is not established.

Development failures are retained separately, not represented as successful runs: the initial root run and `view-completion/` stopped at a newly added detached-view `IsVisible` assertion; `view-final/` queried an unrealized collapsed content presenter; `view-layout/` incorrectly required an unowned popup to retain focus through dispatcher idle. The fixture now checks expansion through the actual rendered content parent/layout, checks menu opening immediately, then renders the menu detached. `view-menu/` demonstrated the catalog-refresh selection loss; `verified/` demonstrated the cancelled-navigation stale highlight. These led to the two production selection corrections above. All original content, safe-rendering, error/loading/empty-state assertions remain. The final `selection-verified/` run passed after those concrete changes; earlier artifacts remain intact.

`renders.json` records original paths, timestamps and SHA-256 hashes for copied current captures in `renders/`. Twelve were inspected: `Light-1280-96-navigation.png`, `Dark-1280-96-navigation.png`, `Light-480-240-navigation.png`, `Dark-480-240-navigation.png`; the corresponding four `*-overflow.png` files; `Light-1280-96-count-help.png`, `Dark-480-240-count-help.png`, `Light-1280-96-content.png`, and `Dark-480-240-organization.png`. Icons, right-aligned counts, long folder wrapping, mutually exclusive selected styling and menu/help contrast showed no blocking A1 defect. Detached fixtures omit the application shell. Existing wide/narrow panel behavior, tall header, Manage folders and editor actions are deliberate retained differences for A2–A7, not completed redesign work.

Exact phase inventory, **seven modified files, no added source files**:

- `src/PersonalTradingJournal.Desktop/Resources/Icons.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookViewTests.cs`
- `README.md`
- `docs/notebook.md`

Whitespace and local documentation-target checks passed after documentation updates. There is no EF/model/schema change. No CI, concurrency, timeout or harness optimization occurred. Historical M22.11 acceptance remains passed; overall M22.12 acceptance, A2–A7 and M22.12B remain pending. Human keyboard/Narrator, native journal restart, real clipboard/picker/viewer, installed-codec WebP and matching hosted timing/input compatibility remain unverified. No production/provider access, publication or workflow triggering occurred.

## M22.12A — Notebook visual review and picker polish

22 October 2026. Actual baseline: clean `develop` at **`9b6208448f7213c231f1c1181bbfc26f1475849c`**, existing worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. The three reported M22.11D changes had been committed. Repository/ancestor guidance search found no AGENTS.md. One agent; CI remains unchanged in A. The supplied `C:/Users/Yovko/Downloads/Trading journal notebook workspace.png` was opened before editing, together with retained `TestResults/m229a-verified/renders/Light-1280-96-linked-items.png`, `TestResults/m2210b-verified/renders/Dark-480-240-duplicated.png` and `TestResults/release-20261020-174016-954f7bd34d35413a860b961ced6ad1e9/m227-visuals/Dark-480-240-management.png`.

The reference supports the existing organization/list/editor hierarchy, selected-note accent, clear primary creation action, readable document and image captions. The production shell supplies the Notebook heading/navigation; detached view fixtures omit that shell. Fixed 210/290-pixel organization/list columns leave the editor flexible. Current text controls, template creation, explicit saves, recovery/conflict feedback, lifecycle actions and unavailable-target detail are deliberately retained rather than removed to reproduce the less functional mockup. Titles and linked targets already wrap; toolbar/actions wrap; narrow views expose Organization / Notes / Content navigation. Destructive global-tag and permanent-note deletion remain distinct danger actions. Themes retain the established application palette and typography.

**Correction:** the previously recorded compact-picker truncation is addressed in folder management, move-folder, tag management and tag assignment. Bounded wrapping templates replace single-line display members; the selected full name/assignment label is also available through tooltip and accessibility help without changing the control's existing accessible name. The existing long-tag fixture now asserts complete tooltip/help text and a wrapping rendered label in every child row. This changes presentation only: no persistence, document/archive, autosave, image ownership, recovery or navigation coordination changes.

**Focused results:** affected Desktop test-project Release build (`dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore`) passed, zero warnings/errors, MSBuild **54.34s**. Tests used `dotnet test` on that project with `-c Release --no-build --no-restore --logger trx --results-directory <phase-directory>`. Lifecycle ran first with `FullyQualifiedName~NotebookLifecycleViewTests`: **4/4 parents and four exact children**. Then the filter below ran **12/12 parents and 24 children**. Total **16 passed parents**, zero failures/skips, both exits **0**; child results are not added to parent totals.

```text
FullyQualifiedName~NotebookImageEditorTests|FullyQualifiedName~NotebookLinkViewTests|((FullyQualifiedName~NotebookViewTests|FullyQualifiedName~NotebookTagViewTests|FullyQualifiedName~NotebookAutosaveViewTests|FullyQualifiedName~NotebookEditorViewTests)&DisplayName~Light&DisplayName~1280)
```

The four shared suites each launch once from the representative parent and execute all Light/Dark × 1280/96-DPI and 480/240-DPI children. Image/link/lifecycle families retain exact one-child dispatch for every row. No harness optimization, assertion weakening, deadline or concurrency change was made. Native context: authorized session 1 / interactive WinSta0 / Default with cursor access, existing executor token; `before-context.json` records no active tests before launch. Existing child isolation/cleanup and 90-second process / 30-second watchdog limits remain intact. No full gate was run or tests retried.

Evidence root **`TestResults/m2212a-verified/`** contains `build.log`, `exclusive.log`, `presentation.log`, `results.json` with exact filters/exits, raw parent/child TRX and supervision artifacts, `trx-audit.json`, and `renders.json` with original paths/copy hashes. Existing fixtures regenerate their states; copies preserve this phase independently of future fixture runs. The following **nine current captures were actually inspected**, all relative to its `renders/` directory:

- `notebook-tag-renders-Light-1280-96-management.png`
- `notebook-tag-renders-Dark-480-240-assignments.png`
- `notebook-tag-renders-Light-480-240-management.png`
- `notebook-image-renders-Light-1280-96-inline-caption-missing.png`
- `notebook-image-renders-Dark-1280-96-inline-caption-missing.png`
- `m229a-renders-Dark-480-240-picker.png`
- `notebook-autosave-renders-Dark-1280-96-conflict.png`
- `notebook-renders-Light-480-240-empty.png`
- `m2210b-renders-Dark-480-240-delete-ready.png`

Full long labels now remain readable, including the assignment's already-assigned suffix. Chart originals scale within the editor, captions wrap and unavailable originals retain an explicit message. Long linked-target labels and disabled duplicate linking remain understandable. Conflict copy states that the draft is retained; empty/Trash states disable inappropriate editing and retain recovery/restore actions. No blocking overlap or contrast defect was observed in these captures. Ordinary scrolling crops offscreen content; the tests verify reachable panels and contained actions. Automated fixtures cover formatted text, real synthetic image presentation, tags and linked items in complementary states, **not one populated fixture combining all of them**.

Remaining findings, by impact: (1) integrated all-features visual coverage, real keyboard/Narrator/focus interaction and native restart acceptance still need the later acceptance phase; (2) narrow views retain a tall action area and nested document/page scrolling, a lower-priority density tradeoff rather than a demonstrated inaccessible control; (3) real clipboard/picker/viewer, installed-codec WebP and matching hosted execution remain unverified. No claim is made that every generated render was visually inspected. Historical input/focus causes remain unexplained. Overall M22.12 acceptance remains pending; M22.11's prior 5,361-test acceptance remains passed.

Exact modified-file inventory: `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml`, `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookTagViewTests.cs`, `README.md`, `docs/notebook.md`. Whitespace and all 203 local documentation targets passed after documentation edits; final process inspection found no active tests; no database/model change requires EF migration work. No CI modification, publication, workflow trigger, production/provider access or general reclamation occurred.

Previous checkpoint: **M22.11D raises only the CI Test-step budget to 15 minutes (900 seconds); matching hosted validation remains pending.** M22.11B3 local automated Release acceptance remains complete: **5,361/5,361 passed, zero failures/skips, exit 0**, tested at `197f9e6a120b55e1ae73d0657083561bde22a882`. Its 737.375-second gate has 162.625 seconds of local measured headroom against the new budget. Matching hosted timing/input compatibility, human interaction and native journal restart remain unverified. Historical input/focus causes remain unexplained. Document/archive v1, export v18 and 29 migrations are unchanged; general reclamation and M22.12 remain deferred.

Historical checkpoint: **M22.11B finished 5,356/5,361 passed, five failed, zero skipped, exit 1**. Its five parents map to two distinct native validation-focus child failures. A2 remains 201/203, two failed; A3's two diagnostic children passed once each and input origin remains unproven. B's 696.258-second failed, traced run is not a healthy-runtime benchmark. B1/B2/B3 do not retroactively explain those original observations. Earlier phase sections below retain their status at the time of execution.

## M22.11D — CI Test-step budget

22 October 2026. Baseline: clean **develop / `04618d02576ca1a63ca65ef708689a7e5e3ee16c`**, existing worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. No applicable repository/ancestor AGENTS.md was found. Exact phase inventory: **`.github/workflows/ci.yml`, `README.md`, `docs/notebook.md`**. The workflow's sole behavioral change is `timeout-minutes: 10` → `15` on the existing Test step invoking `./scripts/Test-Release.ps1 -ResultsDirectory TestResults`; triggers, commands, permissions, job settings, artifact handling, scheduling and concurrency remain unchanged. All assertions, isolation, process deadlines and per-case watchdogs are preserved; no harness or dispatch optimization is included.

Retained B3 evidence (`TestResults/m2211b3-verified/summary.json`, `gate-exit.json`) records **5,361/5,361 passed, zero failures/skips, exit 0**, gate **737.375 seconds excluding restore/build**. The new **900-second** budget provides **162.625 seconds** of measured local headroom. Another retained passing gate, M22.10C (`TestResults/m2210c-verified/summary.json`), took **769.349 seconds**, leaving **130.651 seconds**. Source, coverage and scheduling differ between these runs; they are not controlled performance comparisons. The configuration accommodates observed passing work and does not make tests execute faster. Historical records below retain their original 600-second budget and measured margins.

Static validation passed: the workflow text equals baseline with only the Test-step timeout substituted, `git -c core.safecrlf=false diff --check` is clean, and all **202 local Markdown targets** exist (same target-existence logic as B3; remote URLs/fragments excluded). No local actionlint/yamllint, PowerShell YAML converter, bundled PyYAML or Node YAML package was available, so no full YAML/schema validation is claimed; no validator was installed. This phase runs no restore, build, focused tests or complete gate. **M22.11 local automated acceptance remains passed; the new CI configuration has not been validated by a matching hosted run.** Hosted timing/input compatibility, human interaction and native journal restart remain unverified, and historical input/focus causes remain unexplained. No publication, workflow triggering, production/provider access, general reclamation or M22.12 implementation occurred.

## M22.11B3 — Complete Release acceptance after harness corrections

22 October 2026. Actual build/test baseline: **clean develop at `197f9e6a120b55e1ae73d0657083561bde22a882`**, worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. The expected `e6d488c7031b87ae18bf0a32cd167bad12f3793a` plus eight modified files/one helper was superseded by the user's intervening B2 commit, `197f9e6 Isolate foreground validation tests and verify owner readiness`. No tracked changes were present before preparation. All **1,344 runtime source/configuration hashes and four test DLL hashes** matched B2 before preparation; B3's rebuilt binary/source manifest is `TestResults/m2211b3-verified/verified-runtime-source.json`. Runtime hashes were rechecked after the gate; only these two documentation files changed afterward. No subagents, behavioral corrections, focused reruns or gate retries were used.

### Preparation, commands and execution context

Repository/ancestor guidance inspection found no AGENTS.md. README, this notebook, B2 evidence, release script and CI workflow were inspected. Before launch, the supported executor confirmed no independent test processes and **Windows 10.0.19045, session 1, interactive WinSta0 / Default, cursor available/error 0**. The explicit already-authorized `require_escalated` executor used its existing administrator token; scripts requested no UAC elevation, token switch, desktop switch or alternate input path. Restricted execution is not assumed to provide native cursor/focus access. `before-context.json`, `pre-gate-context.json` and `final-context.json` retain the actual context; final inspection found no active test processes.

Commands from repository root (durable wrappers retain exact arguments and UTC timestamps):

```powershell
dotnet restore PersonalTradingJournal.sln
dotnet build PersonalTradingJournal.sln --configuration Release --no-restore
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
# discovery.ps1 lists the four built projects, with both established Desktop filters;
# --configuration Release --no-build --no-restore --list-tests executes no cases.
$env:PTJ_INPUT_ORIGIN_TRACE = '1'
$env:PTJ_VALIDATION_FOCUS_TRACE = '1'
$env:PTJ_VALIDATION_FOCUS_SHARED_SUITE = '1'
pwsh -NoProfile -File scripts/Test-Release.ps1 -ResultsDirectory TestResults/m2211b3-verified
```

`prepare.ps1`: restore **5.7558451s**, build **65.6377492s** command wall time (MSBuild 64.06s), EF **5.1984042s**; all exits **0**, build **zero warnings/errors**, no pending model changes. `run-gate.ps1` refused replacement of an existing invocation and preserved original output/exit. The gate retains exclusive-first ordering, max project concurrency four, collection limit two, native child deadlines/watchdogs, Calendar's 120-second deadline and 45-second case watchdog, and its own `PTJ_CALENDAR_TIMINGS=1`. B2's trait/collection membership and one-time neutral-owner prerequisites are included. No new 600-second cancellation wrapper or CI setting change was made.

### Complete results and coverage audit

One gate, **exit 0**, **5,361 passed / 5,361 total**, **zero failed/skipped**:

| Project / partition | Passed / discovered | Failed / skipped |
| --- | --- | --- |
| Domain | 602 / 602 | 0 / 0 |
| Application | 1,048 / 1,048 | 0 / 0 |
| Infrastructure | 1,605 / 1,605 | 0 / 0 |
| Desktop exclusive WPF | 101 / 101 | 0 / 0 |
| Desktop parallel remainder | 2,005 / 2,005 | 0 / 0 |
| Desktop combined | 2,106 / 2,106 | 0 / 0 |

Actual discovery matches the expected counts. `completed-identities.json`, `partitions.json`, `project-totals.json`, `audit.json` and `completion-audit.json` verify all five result groups, result/definition/entry identity, timestamps, no duplicate project/test-ID/display-name identities and complete execution. Desktop partitions are disjoint and complete. Readers use `-LiteralPath`, including bracketed TRX names. The initial display-name comparison identified ten cases where TRX escaped non-BMP characters as UTF-16 surrogate pairs while discovery printed the characters. `completion-audit-raw-display.json` preserves that discrepancy; the reader decodes only valid paired surrogate escapes for display comparison, preserving all other characters and counts. The resulting multisets match exactly. An earlier reporting-script syntax error was also corrected before audit execution; neither reporting issue was a test failure or caused a rerun.

Required source-derived method checks in `coverage.json` include all **six integrated backup cases**, three Settings-navigation consent cases, the real Settings/offline restore/fresh-read workflow, linked return navigation and A2 abandoned-context protections. The broader audited groups include recovery **9**, autosave **20**, image persistence **57**, image portability **6**, Notebook portability **14**, archive **45**, export **39**, templates **6**, lifecycle **18**, duplication **4**, and copy/delete **8**, all passed. Groups overlap; these counts are coverage evidence, not additions to 5,361. The manifest also records image admission/coordination/drafts, autosave UI, SQLite workspace, backup UI and harness regressions.

| Family | Passed parents | Child launches | Distinct matching children |
| --- | --- | --- | --- |
| Plan Workspace | 4 | 1 shared | 4 |
| Session Preparation | 1 | 1 | 1 |
| Plan Sections | 4 | 1 shared | 4 |
| Session Brief | 4 | 1 shared | 4 |
| Breakdown accessibility | 3 | 3 exact | 3 |
| Notebook lifecycle | 4 | 4 exact | 4 |
| Notebook image editor | 4 | 4 exact | 4 |
| Notebook links UI | 4 | 4 exact | 4 |

`dispatch.json` matches full parameterized names and test IDs against parent definitions/results, validates expected cardinality and cleanup, and rejects zero, extra, duplicate or skipped child cases. Child counts are not added to parent totals. Workspace, Preparation and Sections executed only in the exclusive partition; supervisor intervals including cleanup do not overlap: **06:33:53.5226956–06:34:16.6695520**, **06:34:16.6903367–06:34:22.2535440**, **06:34:22.2868635–06:34:50.9659663 UTC**.

`supervision-audit.json` covers **87 supervision logs and 82 raw child TRX files**. It explicitly distinguishes intentional negative probes: exit 7, blocked-STA timeout/tree cleanup, injected first-log/output-pump failures, and the synthetic native unhandled-dispatcher crash. The zero-result crash TRX and zero-match selection TRX are required negative-harness evidence, each linked to its passing asserting parent; they are not missing feature results. The deliberately empty first-log-failure log is backed by its passing process-exit assertion and final process inspection. All other supervised processes have confirmed cleanup. No feature-parent failures, failed child case results or hang dumps were found; raw crash/negative evidence remains retained rather than hidden by aggregate counts.

### Timing and diagnostics

`gate-exit.json`: **06:31:52.4940900–06:44:09.8692766 UTC**, **737.3751866s (12m17.375s)**. Restore/build are excluded; both partitions, startup and gate accounting are included. `timings.json` reports exclusive **178.5977986s**, parallel **558.2064532s**, each through its final accounting boundary. Post-command `StageFinishedUtc` is not treated as an individual project completion time, and overlapping project durations are never summed. Calendar supervisor start-to-exit **68.6080233s**, existing 120-second deadline headroom **51.3919767s**, exit 0 and confirmed cleanup.

`.github/workflows/ci.yml` still gives the **Test step** `timeout-minutes: 10`; restore, build and artifact upload are separate steps. This passing local run's margin is **−137.3751866s** against 600 seconds. It is a passing test run with an unresolved CI-budget risk. Input/focus and Calendar diagnostic tracing was enabled, and machine load/environment are local; no whole-gate speedup, hosted timeout, hosted timing or hosted input compatibility is established.

`input-traces.json`: **13 files / 1,259 samples**, none with a recorded native-down high bit. This does not identify the original pressed-button source. `focus-traces.json`: Workspace **38 rows, four posted/completed requests**, Preparation **33 rows, five posted/completed requests**, active neutral owners before validation, expected TextBox focus at original idle assertions, no observed deactivation before cleanup and no pending requests at disposal. The nine requests include detached-owner observations; completion does not imply every request should focus a target. Original B focus-taking events and request dispositions, and historical Plan/Brief slowdown causes, remain unexplained.

### Evidence, compatibility and exact inventory

Root: **`TestResults/m2211b3-verified/`**; final **`summary.json`** combines source, counts, commands, timing, audits and supporting checks. Raw gate: **`release-20261022-063152-ea9831ca85b947a3b393855344c30486/summary.json`** under that root, with original `complete-gate-output.log`, all TRX/stdout/stderr, supervision, phase and input/focus diagnostics. `failure-diagnostics.json` preserves the empty feature-failure mapping; negative-harness details are in `supervision-audit.json`. Historical A/A2/A3/B/B1/B2 artifacts remain unchanged.

Visuals reuse the prior A/B review: **eight distinct Settings/maintenance images in 12 PNG files**, Light/Dark at 400/760 widths, under **`TestResults/m2211a-verified/renders/`** (`maintenance-{Light,Dark}-{400,760}.png` and Settings top/restore captures). `visuals.json` enumerates exact filenames/hashes and the view, resource, theme and fixture comparison paths. All hashes match and those inputs are unchanged since A's committed `2d2317b5bc7461e87eaef0e98c2c2c2d77e3cb00`; the retained no-blocking-defect review remains valid. No new capture or human-interaction acceptance is claimed.

B3's exact tracked inventory is **README.md and docs/notebook.md only**, edited after the runtime gate. Generated verification scripts/reports are ignored durable artifacts under its results root. Overall M22.11 has **26 distinct changed paths**, counting shared files once. `inventory-phases.json` records exact commit boundaries: A `ec6360b→2d2317b`, A2 `2d2317b→7156494`, A3 `7156494→36f507a`, B `36f507a→50f3ef2`, B1 `50f3ef2→e6d488c`, B2 `e6d488c→197f9e6`; B3 is documentation after that tested HEAD. A1 was read-only diagnosis.

| Exact repository path | Phases |
| --- | --- |
| `docs/backup-restore.md` | A |
| `docs/notebook.md` | A, A2, A3, B, B1, B2, B3 |
| `README.md` | A, A2, A3, B, B1, B2, B3 |
| `src/PersonalTradingJournal.Desktop/ViewModels/MainWindowViewModel.cs` | A2 |
| `src/PersonalTradingJournal.Desktop/ViewModels/MainWindowViewModel.Notebook.cs` | A2 |
| `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.cs` | A2 |
| `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Editing.cs` | A2 |
| `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Links.cs` | A2 |
| `src/PersonalTradingJournal.Desktop/ViewModels/Settings/DataBackupsViewModel.cs` | A |
| `tests/PersonalTradingJournal.Desktop.Tests/Breakdown/BreakdownAccessibilityViewTests.cs` | A3 |
| `tests/PersonalTradingJournal.Desktop.Tests/Navigation/NotebookLinkedNavigationTests.cs` | A2 |
| `tests/PersonalTradingJournal.Desktop.Tests/Navigation/NotebookNavigationTests.cs` | A |
| `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookLinkTests.cs` | A2 |
| `tests/PersonalTradingJournal.Desktop.Tests/Settings/DataBackupsFlowTests.cs` | A |
| `tests/PersonalTradingJournal.Desktop.Tests/Settings/DataBackupsTests.cs` | A |
| `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/ExclusiveWpfCollection.cs` | B2 |
| `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/ReleaseSchedulingTests.cs` | B2 |
| `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/UiHarnessDiagnosticsTests.cs` | A2 |
| `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/UiKeyboardProbe.cs` | A2, A3 |
| `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/ValidationFocusOwner.cs` | B2 |
| `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/ValidationFocusTrace.cs` | B1, B2 |
| `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/PlanSectionsViewTests.cs` | B2 |
| `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/SessionPreparationViewTests.cs` | B1, B2 |
| `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/TradingPlanWorkspaceViewTests.cs` | B1, B2 |
| `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/NotebookIntegratedBackupTests.cs` | A |
| `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/NotebookPortabilityTests.cs` | A |

After documentation edits, EF was checked again with the same Release/no-build command; `check-whitespace.ps1` checks tracked/new-file whitespace, and `check-doc-links.ps1` checks local Markdown target existence in README, this notebook and backup-restore guidance. These supporting checks passed; the link checker excludes remote URLs and fragment validation. No runtime gate was repeated for documentation. Final source/binary hashes and process inspection remain clean of runtime drift/leftover tests.

Document/archive **v1**, logical export **v18**, schema **29 migrations** remain unchanged. Integrated restore reuses the existing whole-database snapshot, managed-original inventory, capture lease and offline recovery engine. Atomic final image placement, original-byte retention, independent duplication, confirmed Trash-only deletion and ownership-safe bounded cleanup remain covered. Prepared originals with uncertain retention are kept; general reclamation and automatic cleanup retries remain deferred. JSON/CSV are not restorable backups.

**Local automated M22.11 acceptance is complete.** The CI-budget deficit remains open; matching hosted CI/input compatibility, human interaction, native journal restart and previously deferred real clipboard/picker/viewer/installed-codec checks are not established by this run. Original input/focus causes remain documented rather than inferred from a later pass. Smallest next action: read-only review of this passing gate's retained timing evidence before proposing a CI-budget change. No production/provider access, Git publication, workflow trigger, timeout change, general reclamation or M22.12 work occurred.

## M22.11B2 — Foreground isolation and owner prerequisites

21 October 2026. Actual baseline: **clean develop at `e6d488c7031b87ae18bf0a32cd167bad12f3793a`**, existing worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. B1's five files were already committed. All **1,343 source/configuration hashes and four test-binary hashes** from B1 matched before changes (`TestResults/m2211b2-verified/baseline.json`). Repository/ancestor guidance search found no AGENTS.md. One agent, no subagents; retained B/B1 evidence and unrelated work are preserved.

### Source-based scheduling review

| Family | Independent desktop requirement | Previous protection / demonstrated gap | B2 correction |
| --- | --- | --- | --- |
| `TradingPlanWorkspaceViewTests` | The 980/96 row shows a native owner, asserts `Keyboard.FocusedElement` for risk/scenario/level/date validation, then exercises real MainWindow closing. | Default collection, no exclusive trait. Four parents await one shared child containing four rows; the per-process dispatcher gate cannot exclude other processes' windows. | Existing exclusive-WPF trait **and** collection for all four visible parents; shared four-row child retained. |
| `SessionPreparationViewTests` | Native invalid-level validation must switch context and focus `Previous Day High`; assertion reads global keyboard focus. | Default collection, no exclusive trait. One isolated child protects Application lifetime but shares the input desktop. `Show()` was followed by mutation/Save without an explicit active-owner readiness boundary. | Existing exclusive trait/collection; one fact/child retained; active owner established before selection/mutation. |
| `PlanSectionsViewTests` | Shows an activating native window, focuses the fold header before routed Enter, requests focus during validation and focuses removal controls before automation invocation. Logical-focus assertions do not make those native operations independent of other windows. | Default collection, no exclusive trait. Four parents await one shared four-row child. In B its window-show interval overlapped Workspace; the source requirement holds independently of that failure. | Existing exclusive trait/collection for all four parents; shared child retained; one-time neutral-owner setup before validation/input. |

`scripts/Test-Release.ps1` still runs `Workload=ExclusiveWpf` first, then its exact complement `Workload!=ExclusiveWpf`. The existing `ExclusiveWpfCollection` has `DisableParallelization=true`; all three families join it, preventing their parent/child lifetimes from overlapping each other or other Desktop collections during that partition (and standard IDE collection execution). Trait membership also keeps these families out of the solution's parallel partition. Existing **two-collection** assembly limit, **four-project** remainder, assertions, process recursion guards, child **90-second deadline / 30-second watchdog**, parent blame limit, process-tree cleanup, completeness and duplicate-identity checks remain unchanged. The collection comment and strict scheduling regression's expected class set were updated. The entire Desktop suite is not serialized and no alternative scheduler is introduced.

### Readiness and diagnostics

Added test-only `ValidationFocusOwner`: `Ready` calls the existing, unchanged `UiKeyboardProbe.Ready` **once on the neutral Window**, using its bounded five-second event/dispatcher setup. It activates only the test thread's HWND through the existing helper, not a global foreground takeover. It verifies the live Application/owner dispatcher, loaded/visible/active owner and attached ready scope; initial keyboard focus must be the Window. It never focuses the expected invalid field. `AssertReady` is read-only: it checks active owner, correct live dispatcher, attachment/presentation source and visible/enabled control (plus focusability for TextBoxes). It contains no activation, pumping or focus repair.

Workspace verifies each existing raw-error control before its corresponding validation. Preparation verifies the level-host readiness while preserving the nonselected invalid context: production validation must still select that context and update its bindings/focus. Plan Sections establishes readiness before its validation and input sequence. Original Save/task/idle boundaries and post-validation assertions are retained. No validation is retried; lost ownership fails or is exposed by the original assertion instead of being repaired. These changes address a missing **fixture prerequisite and scheduling contract**, not a demonstrated production PageFocus defect.

B1 focus tracing remains available. `PTJ_VALIDATION_FOCUS_SHARED_SUITE=1` allows full-family Workspace verification to retain its normal shared child while `PTJ_VALIDATION_FOCUS_TRACE=1` records requests; the old opt-in exact-row diagnostic behavior remains available without the shared-suite switch. Preparation remains a single fact with exact-result validation under tracing. No diagnostic reimplementation of production focus was added. Scope-specific trace files record neutral Window focus at the new readiness boundary, then production-selected TextBox focus after the original idle assertions. Workspace observed four posted/completed requests; Preparation five, including four unloaded detached owners rejected without taking focus. There were **zero pending requests and no deactivation before window-close cleanup** in these observed sequences (`focus-observations.json`). This does not reconstruct either original failed sequence or intercept `Focus()`'s Boolean return.

### Focused verification performed once

All commands used current Release binaries, with the verified uncommitted harness source recorded in `verified-runtime-source.json` (**1,344 runtime files and four test binaries**, post-run hashes match). Input-dependent work used the explicitly authorized supported executor: **interactive session 1, WinSta0/Default, cursor available/error 0**, existing administrator token; no UAC/token/desktop switching, denied-permission bypass or alternative input path. `before-context.json`, `before-families-context.json` and `final-context.json` confirm context and no active test process at the boundaries.

| Selection | Top-level result | Child accounting | Command wall time / exit |
| --- | --- | --- | --- |
| All three affected families in exclusive collection | **9/9 passed**, zero failed/skipped | Workspace **4/4**, Preparation **1/1**, Sections **4/4**; exactly three child launches, nine distinct child names matching the nine parent names | **59.2202732s / 0** |
| Scheduling, isolation/selection and lifetime regressions | **46/46 passed**, zero failed/skipped | ReleaseScheduling **1**, CalendarSta **10**, IsolatedTestProcess **8**, IsolatedTestSelection **23**, JournalFixtureLifetime **4**. Intentional failure/timeout/zero-match probes are subordinate evidence, not failed parent tests. | **36.3887905s / 0** |
| Existing `OwnedFocusValidationAutomationAndRetainedStatesInBothThemes` | **1/1 passed**, zero failed/skipped | One exact matching isolated child; case-owned Application, genuine keyboard/capture assertions, newer-focus/invalidation/replaced-DataContext PageFocus guards retained | **17.4257703s / 0** |

Total is **56/56 disjoint top-level focused tests**; supervised child cases are not added to that total. No redundant exact-row pre-run, complete gate or retry occurred. Build command passed **exit 0, zero warnings/errors, MSBuild elapsed 54.22s** (`build.log`). Discovery, not execution, of the complete Desktop assembly confirms **101 exclusive + 2,005 parallel = 2,106 cases**. All nine affected names appear only in the exclusive partition; no cross-partition discovery-name overlap (`discovery.json`). This preserves the previous 2,106-case set while shifting nine cases from the remainder. Existing gate identity checks remain unchanged; the focused audit also rejects duplicate TRX result identities and mismatched family cardinality.

Supervision proves the affected children did not overlap: Workspace **19:45:22.3914900–19:45:45.8636519 UTC**, Preparation **19:45:45.9032932–19:45:52.2568335**, Sections **19:45:52.2944959–19:46:16.8063378**. Each endpoint is confirmed process cleanup, not a post-command project accounting timestamp. Parent/child full identities, per-case TRX timestamps, raw logs and supervision paths are indexed in `summary.json`.

Commands, run from the repository root (wrappers record expanded arguments):

```powershell
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore
./TestResults/m2211b2-verified/context.ps1 -OutputName before-families-context.json
$env:PTJ_VALIDATION_FOCUS_TRACE='1'
$env:PTJ_VALIDATION_FOCUS_SHARED_SUITE='1'
./TestResults/m2211b2-verified/run-tests.ps1 -Name affected-exclusive-families -Project Desktop -Filter 'Workload=ExclusiveWpf&(FullyQualifiedName~TradingPlanWorkspaceViewTests|FullyQualifiedName~SessionPreparationViewTests|FullyQualifiedName~PlanSectionsViewTests)'
# The next two selections were separate fresh executor invocations, without the validation trace variables.
./TestResults/m2211b2-verified/run-tests.ps1 -Name helper-regressions -Project Desktop -Filter 'FullyQualifiedName~ReleaseSchedulingTests|FullyQualifiedName~CalendarStaTestTests|FullyQualifiedName~IsolatedTestProcessTests|FullyQualifiedName~IsolatedTestSelectionTests|FullyQualifiedName~JournalFixtureLifetimeTests'
$env:PTJ_INPUT_ORIGIN_TRACE='1'
./TestResults/m2211b2-verified/run-tests.ps1 -Name focus-owner-regression -Project Desktop -Filter 'FullyQualifiedName=PersonalTradingJournal.Desktop.Tests.Breakdown.BreakdownAccessibilityViewTests.OwnedFocusValidationAutomationAndRetainedStatesInBothThemes'
./TestResults/m2211b2-verified/discovery.ps1
./TestResults/m2211b2-verified/context.ps1 -OutputName final-context.json
./TestResults/m2211b2-verified/audit.ps1
./TestResults/m2211b2-verified/check-whitespace.ps1
./TestResults/m2211b2-verified/check-doc-links.ps1
```

Evidence root: **`TestResults/m2211b2-verified/`**. Each selection directory contains `parents.trx`, `output.log`, `vstest.log`, command `summary.json` and child directories. Affected child paths:

- `affected-exclusive-families/plan-workspace-20261021-194522-f5bbcfb8ee084145b2871d1f394cc79c/`
- `affected-exclusive-families/plan-preparation-20261021-194545-ab7c4534d4c04cda97277d1a34c8fa4c/`
- `affected-exclusive-families/plan-sections-20261021-194552-9d3a6f7a207b4a5baaa5af0efa8d5d84/`

These retain named child TRX, stdout/stderr, STA phase and supervision logs; Workspace/Preparation include `*-validation-focus.jsonl`. Existing Breakdown consistency renders were copied into `breakdown-renders-before/` before its regression. Visual production implementation is unchanged; this phase is not a new visual acceptance claim. Supporting checks: whitespace including the new helper and local Markdown targets passed; URL/fragment validation is outside that local-link check. No EF/schema/production change occurred; B's passing EF evidence is reused. Only documentation changed after verification, with no runtime rerun.

### Exact B2 inventory and limits

**Eight modified files plus one added file**, relative to the clean B1 commit:

- `README.md` and `docs/notebook.md` — results, evidence, source-based review and limitations.
- `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/ExclusiveWpfCollection.cs` — clarify the existing foreground-family collection contract.
- `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/ReleaseSchedulingTests.cs` — require the three additional native families in the existing exclusive class set; concurrency assertions unchanged.
- `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/ValidationFocusTrace.cs` — opt-in shared-suite tracing switch.
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/TradingPlanWorkspaceViewTests.cs` — exclusive membership, normal shared-suite tracing and neutral-owner/read-only target prerequisites.
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/SessionPreparationViewTests.cs` — exclusive membership and owner/level-host prerequisites without preselecting the invalid context.
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/PlanSectionsViewTests.cs` — exclusive membership and one-time neutral-owner readiness before validation/input.
- Added `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/ValidationFocusOwner.cs` — bounded setup via the unchanged keyboard helper, followed by read-only structural/active-owner checks.

Ignored evidence/scripts are under the B2 root; no production, CI, release-script, input-helper, Notebook backup/restore or navigation-ownership changes were made. The new collection membership fixes a demonstrated shared-desktop isolation deficiency; the readiness boundary makes setup assumptions explicit. Neither establishes who took focus in B, the original Workspace request disposition, or the original Preparation request disposition. Historical pressed-button and Plan/Brief uncertainties are not declared resolved.

Scheduling cost: the exclusive partition grows **92 → 101 parents**, and the parallel Desktop remainder shrinks **2,014 → 2,005**. The three families now run serially in that partition; the selected local command took 59.220s. Removing overlap can increase the gate critical path, so **no whole-gate speedup, 600-second budget guarantee or hosted compatibility is claimed**. The CI-budget risk remains open. Smallest next action is a separately authorized complete Release acceptance run of this harness, retaining diagnostics and measuring the same gate scope. M22.11 acceptance remains pending until that gate passes; historical B remains **5,356/5,361, five failed, zero skipped, exit 1**. Human interaction, native journal restart and matching hosted CI remain unverified. No Git publication, workflow trigger, production/provider access, general reclamation or M22.12 work occurred.

## M22.11B1 — Bounded validation-focus diagnosis

21 October 2026. Actual initial baseline: clean **develop / `50f3ef29ad5f6c8a54dd93dcd371bfc89a9c59b3`**, worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. The expected B documentation changes were already committed. No repository/ancestor AGENTS.md was found. Before edits, B's **1,342 runtime source/configuration hashes and four test-binary hashes matched** its retained manifest. No subagents were used. The restricted `Get-CimInstance Win32_Process` query returned **Access denied**; the explicitly authorized supported executor then confirmed **session 1, interactive WinSta0/Default, cursor available/error 0**, existing administrator token, no UAC/token/desktop switch. No tests were active before either execution or after both; `before-plan-context.json`, `before-preparation-context.json` and `final-context.json` record this under `TestResults/m2211b1-diagnostic/`.

### Retained failure mapping and lifetime

All original paths below are relative to `TestResults/m2211b-verified/release-20261021-182044-ce17b3244c764a0eb74938f431860184/parallel/`. `failure-diagnostics.json`, `plan-focus-failure.json` and `preparation-focus-failure.json` in the B root retain full parent/child identities, errors and paths. The parent TRX is `Yovko_DESKTOP-N2ONT35_2026-10-21_21_23_09_net10.0[2].trx`; readers use literal paths.

| Family | Exact failed child / expected target | Retained observation and original source location | Process and evidence |
| --- | --- | --- | --- |
| `TradingPlanWorkspaceViewTests` | `FullPageBindingsLifecycleGuardsFocusAndLightDarkResponsiveLayouts(width: 980, dpi: 96)`; first scenario's `Condition` TextBox | Shown: active, keyboard Window. Risk validation: active, expected TextBox. Scenario validation: **inactive, keyboard null**, `Assert.IsType<TextBox>` at original line **125** | Child testhost **20376**, STA **14**, supervisor **25896**; `plan-workspace-20261021-182308-4b655a0bbb264ff78e7c76d8c632aef0/plan-workspace.trx`, `calendar-sta-phases.log`, `stdout.log`, `stderr.log`, `process-supervision.log` |
| `SessionPreparationViewTests` | `CompiledBindingsAccessibleInputsValidationAndResponsiveLightDarkRenders`; original context's `Previous Day High` level TextBox | Original context selected successfully, then **keyboard null**, `Assert.IsType<TextBox>` at original line **111**; activation/logical focus unrecorded | Child testhost **15204**, STA **21**, supervisor **24996**; `plan-preparation-20261021-183152-5d779a468e864666945e5db2a1073a4d/plan-preparation.trx` and corresponding phase/output/supervision logs |

Both classes are under `PersonalTradingJournal.Desktop.Tests.TradingPlans`. Four failed Workspace parents have parameters **980/96, 1280/144, 1600/192, 460/240**. They await one shared `Lazy<Task>` class-wide child; there were **not four separate failed children or four repeated suite launches**. Actual child order was **980/96 failed**, **460/240 passed**, **1600/192 passed**, **1280/144 passed**. Child intervals in UTC: 18:23:17.7739762–18:23:34.2143099, 18:23:34.3400919–18:23:42.8854372, 18:23:42.8976986–18:23:50.4880456, 18:23:50.5099016–18:23:57.5001801. Only 980/96 enters the native focus block. Passed siblings do not exercise that block and do not explain its failure.

Workspace's first row begins with `Application.Current=null`, creates an explicit-shutdown Application on its child-owned native dispatcher, and performs its own detached Light/Dark rendering and lifecycle loops before the native **Light**, 670×800 window. The row's workspace instances are disposed; windows close in `finally`. Application/dispatcher remains alive for later rows through `shutdownDispatcher:false`. There is no preceding theory row to preserve. Preparation is one fact: its detached Light/Dark, 1040/96 and 460/240 loops reuse one editor; the native **Dark-resource**, 650×750 window follows a separate ready-view check. Native window DPI was not recorded as a theory parameter. Its Application starts null on the child-owned dispatcher; VM disposal/window close are case scoped, dispatcher lifetime remains child scoped. Preparation's child ran **18:31:55.6104126–18:31:58.4616781 UTC**. Neither failure has a retained hang dump or watchdog timeout; both finish with ordinary failed assertions and confirmed child cleanup.

### Validation and focus path

Workspace `SaveAsync` checks `CanSave`, reports saving, and calls its editor's `SaveAsync`. `SessionPreparationViewModel.WriteAsync` detects invalid content **before storage/operation startup**, increments `ValidationAttempt`, raises its notification synchronously and returns Invalid. `FirstInvalidField` scans fields in document order: contexts precede scenarios, which precede risk. Thus the later invalid scenario wins over the still-invalid risk field; a later invalid level wins over both. `ScenariosSection` expands and requests the first TextBox whose DataContext is that scenario field. `SessionPreparationSection` selects the owning invalid context, expands and requests the matching level TextBox. Detached Preparation sections remain subscribed through the weak property-event mechanism while alive; they also receive the validation notification.

Each section owns its own `PageFocus` generation. Request creation captures version, DataContext, keyboard focus and logical focus, then queues **DispatcherPriority.Loaded**. It rejects a newer request/input generation, unloaded/hidden owner, replaced DataContext or incompatible focus change; it then requires a visible, enabled, focusable target. It brings the target into view, sets logical focus, and calls `Focus()` only if the owner window is active. DataContext/input invalidation and these stale-focus safeguards are unchanged. There is no asynchronous save awaiting storage on these invalid-input branches and no navigation/disposal operation within the validation block.

Workspace's existing `Pump` awaits the task and drains ApplicationIdle, followed by another existing Idle call. Preparation synchronously observes the already-completed invalid Save task, then invokes ApplicationIdle. **Save completion alone does not mean the queued focus callback completed.** Loaded is above ApplicationIdle, but the retained null assertions do not reveal whether the callback was rejected, targeted an unavailable element, applied then cleared, or remained pending. In particular, Workspace's inactive state at the assertion does not establish the earlier branch or prove that another specific process took focus. Preparation's selected-context assertion establishes the selection change, not successful keyboard focus.

The retained **Plan Sections** testhost **28880**, STA **24**, has `ui-case-phases.jsonl` in `plan-sections-20261021-182319-6674f19657014d73a1c1adc4fbe9eec5/`: native window **initializing/showing 18:23:32.0687379**, **shown 18:23:34.2706777 UTC**. This overlaps Workspace's failing action, which ends at 18:23:34.0902152. Both suites are in the parallel partition with separate process-owned WPF state, so the existing in-process native action gate does not serialize their windows. This is confirmed concurrent native-window activity and a foreground-isolation exposure, **not proof that this window caused the deactivation**. During Preparation's interval, the overlapping retained child supervisor found by timestamp was `playbook-sqlite-restart-read`; there is no retained activation/foreground-owner evidence establishing a competing native window. Neither null result establishes physical input or connects to the earlier pressed-button investigation.

### Bounded observations and results

Before execution, the stated question was whether each queued request was rejected, unavailable, applied then cleared, or pending at its existing assertion. Added **opt-in `PTJ_VALIDATION_FOCUS_TRACE=1`**, test-only `ValidationFocusTrace`: buffered timestamps, task-return boundaries, request identity/generation, target readiness/attachment, Application/window/dispatcher identity, logical/keyboard focus events, activation and foreground HWND/PID. It reads the installed WPF callback closure through reflection and flushes observations after assertions, including on failure. It posts no dispatcher work, adds no waits, forces no focus and changes no input. It does **not intercept the Boolean return from `Focus()`**; actual successful focus is evidenced by routed focus events and retained post-callback ownership. The reflection is runtime-specific diagnostic instrumentation. First-invalid candidates describe the two validation paths, not every unrelated date-focus request.

Each diagnostic parent uses the existing exact display-name filter and single-result validation when tracing is enabled. Default class-wide dispatch remains unchanged. Each unit ran **once**, sequentially, in the supported desktop, retaining the **90-second child process deadline, 30-second child hang watchdog**, cleanup, assertions and original within-case setup. Plan's failed row was first originally, so exact 980/96 retains the relevant fixture/Application history without executing later sibling rows. The whole Preparation fact retains its rendering/VM history. These probes intentionally do not recreate original parallel activity; their passes cannot validate behavior under foreground contention or classify the original failure as flaky.

| Diagnostic unit | Parent / intended child | Failures / skips / exit | Command wall time, including startup | Observations |
| --- | --- | --- | --- | --- |
| `plan-exact-once` | **1/1 + 1/1**, exact 980/96 identity | **0 / 0 / 0** | **22.0133248s** | 37 trace rows; four requests posted/started/completed |
| `preparation-exact-once` | **1/1 + 1/1**, exact fact identity | **0 / 0 / 0** | **10.1587859s** | 32 trace rows; five requests posted/started/completed, four from detached owners |

These are parent/child accounting levels, not four independent feature tests. `summary.json` verifies both complete identities and records TRX timestamps, durations and paths. Plan child: `plan-exact-once/plan-workspace-20261021-192252-c3079c5ae27e4f58a81397ec28885b02/`; Preparation child: `preparation-exact-once/plan-preparation-20261021-192327-455bb4dc0e7f4d16a9e46337db3b450b/`. Each contains its named TRX, `*-validation-focus.jsonl`, stdout/stderr, supervision and STA logs; parent TRX and vstest diagnostics are in the enclosing unit directory. No run was repeated.

Plan scenario request **19:23:04.7123989 UTC**: version/current version 2, unchanged context/input, attached visible/enabled/focusable target on the same active window/dispatcher. Save already completed at .7124446; callback starts .7607087, target receives keyboard focus .7691246, callback completes .7730429 with logical and keyboard focus matching; original idle boundary .8295719 retains focus. Preparation's native level request **19:23:32.6630651 UTC** follows selection back to the invalid context; Save returns completed .6637947, callback starts .7307627, target gets keyboard focus .7449922, callback completes .7603245, and original idle boundary .8117752 retains it. Four detached-owner callbacks execute first, have `IsLoaded=false`, and leave focus unchanged. Both native targets are ready and attached to their owning Application's dispatcher. No request remains pending at trace disposal. In both probes deactivation/focus clearing occurs **after** the recorded `before-window-close` cleanup boundary, not before the assertion.

Commands (executed once per unit; wrappers retain expanded arguments and timestamps):

```powershell
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore
$env:PTJ_VALIDATION_FOCUS_TRACE='1'
./TestResults/m2211b1-diagnostic/context.ps1 -OutputName before-plan-context.json
./TestResults/m2211b1-diagnostic/run-tests.ps1 -Name plan-exact-once -Project Desktop -Filter 'FullyQualifiedName~TradingPlanWorkspaceViewTests.FullPageBindingsLifecycleGuardsFocusAndLightDarkResponsiveLayouts&DisplayName~980'
./TestResults/m2211b1-diagnostic/context.ps1 -OutputName before-preparation-context.json
./TestResults/m2211b1-diagnostic/run-tests.ps1 -Name preparation-exact-once -Project Desktop -Filter 'FullyQualifiedName=PersonalTradingJournal.Desktop.Tests.TradingPlans.SessionPreparationViewTests.CompiledBindingsAccessibleInputsValidationAndResponsiveLightDarkRenders'
./TestResults/m2211b1-diagnostic/context.ps1 -OutputName final-context.json
./TestResults/m2211b1-diagnostic/audit.ps1
./TestResults/m2211b1-diagnostic/check-whitespace.ps1
./TestResults/m2211b1-diagnostic/check-doc-links.ps1
```

Release build: **exit 0, zero warnings/errors, MSBuild elapsed 59.52s**, `build.log`. Wrapper uses Release `dotnet test --no-build --no-restore`, parent TRX/vstest logs and the existing three-minute outer blame watchdog. The child retains the existing 90/30-second protection and exact single-result validation. The initial combined preflight command returned without launching tests because of a stale/null `$LASTEXITCODE` guard; it created no run directory or test process. The two recorded units are the only launches. A console-only audit projection initially displayed null fields for ordered dictionaries; the saved summary was correct, the projection was corrected, and no tests were repeated for reporting.

### Findings, next action and exact inventory

**Plan:** observed loss of active-window/keyboard ownership is confirmed in B; simultaneous native-window work is also confirmed. Request rejection versus focus-applied-then-lost remains unknown for that original event. No production focus-coordination defect, wrong dispatcher ownership, or too-early idle assertion is demonstrated. Smallest justified harness proposal is to review the foreground-dependent Workspace test's membership in the existing exclusive-WPF workload and make active-owner readiness an explicit test prerequisite, preserving PageFocus's refusal to steal focus. This is a proposed fixture/isolation contract correction, not a proven complete cure. If causal attribution to Plan Sections is required, a separately authorized, bounded paired observation would be needed; it was not run here.

**Preparation:** original cause remains inconclusive. The traced pass verifies selected-context binding readiness, rejection of detached-owner requests and successful native focus before the existing assertion. It does not justify removing subscriptions, extending waits, forcing activation/focus or modifying PageFocus. Retain the narrow observer for the next authorized occurrence and classify activation/request disposition before a behavioral correction. Its global keyboard-focus assertion likewise requires an owned active desktop, but there is no direct evidence of the original deactivation or foreground taker.

After an authorized correction, focus regression scope should cover these exact native validation cases, active-target readiness, inactive-window refusal, stale request/input/DataContext rejection, and detached owners; any partition classification change also needs scheduling regressions. A later successful complete acceptance gate is still required. No correction is implemented in B1, and the separate historical pressed-button/Plan/Brief uncertainties are not declared resolved.

Exact B1 inventory: **four modified files + one added file**, with no production/configuration changes:

- `README.md` — diagnostic checkpoint and limitations.
- `docs/notebook.md` — failure mapping, trace evidence, commands and inventory.
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/TradingPlanWorkspaceViewTests.cs` — opt-in exact diagnostic dispatch and observations around unchanged Save/pump/assertion boundaries.
- `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/SessionPreparationViewTests.cs` — opt-in exact-result validation and observations around unchanged validation/idle/assertion boundaries.
- Added `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/ValidationFocusTrace.cs` — opt-in observational helper.

Ignored `TestResults/m2211b1-diagnostic/` contains evidence scripts, build log, context, summary, per-run artifacts and manifest of **1,343 runtime files/four test binaries**. Post-run hashes match. The only existing runtime files differing from B are the two test files above; the new helper is separately inventoried. Documentation is edited after execution. Whitespace (including the added file) and local Markdown target checks passed; external URLs/fragments are outside the link check. EF/schema/production source is unchanged, so B's passing EF evidence is reused. No runtime rerun follows documentation. M22.11 local automated acceptance remains **pending**, with historical **5,356/5,361, five failed, exit 1** intact. No CI budget/concurrency/input-helper changes, Git publication, provider access, general reclamation or M22.12 work occurred. Human interaction, native journal restart and matching hosted timing/input compatibility remain unverified.

## M22.11B — Complete Release verification

21 October 2026, Europe/Kiev. Actual baseline: clean **`develop` at `36f507afb057437971038bc15de84d3556cc40e9`**, sole existing worktree **`C:/Users/Yovko/source/repos/Personal-Trading-Journal`**. A3's four files were already committed, rather than the expected older HEAD with four dirty files. No repository/ancestor AGENTS.md or active test process was found. One agent, no subagents. `baseline.json`, `gate-start.json` and `verified-runtime-source.json` record the actual clean runtime state, **1,342 source/configuration hashes and four test-binary hashes**. `post-gate-provenance.json` checks these after execution. Only the two documentation files below change after verification; no runtime changes are attributed to that later dirty state.

### Preparation, execution context and commands

Evidence root: **`TestResults/m2211b-verified/`**. Restore **3.735s**, full Release build **66.632s** command wall time (MSBuild reports **66.02s**), EF consistency **5.868s**; each exit **0**, build **zero warnings/errors**, EF no pending model changes. Preparation completed before the gate; binaries include A2 navigation ownership/prerequisites and A3 tracing. `preparation.json`, `restore.log`, `build.log`, `ef.log` retain commands, output and separate timing.

Used the already-authorized supported executor: interactive **session 1 / WinSta0 / Default**, cursor available/error **0**, existing administrator token, no UAC/token/desktop switching or alternate input path. `execution-context.json`, `pre-gate-context.json` and `final-context.json` record context and no active test processes before/after; reusable MSBuild nodes are not test runs. No denied desktop access was observed. No unrelated process was stopped.

```powershell
dotnet restore PersonalTradingJournal.sln
dotnet build PersonalTradingJournal.sln --configuration Release --no-restore
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
pwsh -NoProfile -File TestResults/m2211b-verified/context.ps1
$env:PTJ_INPUT_ORIGIN_TRACE='1'
pwsh -NoProfile -File scripts/Test-Release.ps1 -ResultsDirectory TestResults/m2211b-verified
pwsh -NoProfile -File TestResults/m2211b-verified/audit.ps1
pwsh -NoProfile -File TestResults/m2211b-verified/audit-input.ps1
pwsh -NoProfile -File TestResults/m2211b-verified/context.ps1 -OutputName final-context.json
pwsh -NoProfile -File TestResults/m2211b-verified/check-whitespace.ps1
pwsh -NoProfile -File TestResults/m2211b-verified/check-doc-links.ps1
```

`prepare.ps1` runs the first three commands sequentially and stops on preparation failure. `run-gate.ps1` checks context/preparation, snapshots sources, enables the existing opt-in flag and invokes the gate **once**, refusing a second invocation if `gate-start.json` exists. It retains merged output in `complete-gate-output.log` and the original **exit 1** in `gate-exit.json`. Gate start **18:20:43.9303077 UTC**, end **18:32:20.1884227 UTC**. No extra 600-second cancellation wrapper, filtered replacement, failed-family retry or behavior fix. Existing exclusive-first partitioning, project/collection concurrency, assertions, native-input requirements, child/watchdog/calendar deadlines and CI configuration remain unchanged.

### Complete results, coverage and timing

Raw gate root: **`TestResults/m2211b-verified/release-20261021-182044-ce17b3244c764a0eb74938f431860184/`**. Its `summary.json`, both `test-output.log` files, top-level TRX, VSTest logs and supervised child directories remain preserved. Literal-path readers handle the Infrastructure **`net10.0[1].trx`** and Desktop **`net10.0[2].trx`** filenames correctly. Reporting is separate from gate execution and never triggers another run.

| Project / partition | Passed / total | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Domain | 602 / 602 | 0 | 0 |
| Application | 1,048 / 1,048 | 0 | 0 |
| Infrastructure | 1,605 / 1,605 | 0 | 0 |
| Desktop exclusive WPF | 92 / 92 | 0 | 0 |
| Desktop parallel | 2,009 / 2,014 | 5 | 0 |
| Desktop combined | 2,101 / 2,106 | 5 | 0 |
| Complete distinct parent set | **5,356 / 5,361** | **5** | **0** |

The combined Desktop row is explanatory, not added again. Complete-set audits read all five expected project/partition reports, counters, definitions, execution entries and distinct completed identities. `completed-identities.json`, `partitions.json`, `project-totals.json`, `audit.json` and `coverage.json` preserve the audit. Children and overlapping regression groups are never added to functional totals. Intentional negative harness children are retained separately from unexpected parent failures.

The first reporting pass (`audit-initial.json` / `audit-initial.log`) used display text alone for uniqueness and mistakenly used `NotebookLifecycleImageTests` (a partial-file name) as a class name. Eight truncated theory display-name collisions have distinct test IDs; the report now uses the gate's ID-plus-name identity and reads that file's actual `NotebookImageTests` methods. These reporting findings were corrected without executing tests again or changing runtime checks. Final audit has no missing/duplicate/incomplete top-level result finding; it remains nonzero for the real failed parents/child outcome. `negative-child-evidence.json` separately maps the intentional aborted native-callback child and zero-match child to their successful regression parents. No hang dump was generated for the two unexpected assertion failures.

Source-derived integration coverage includes the four methods/six theory rows in `NotebookIntegratedBackupTests.cs` (partial `NotebookPortabilityTests`): chosen older/newer snapshots with copies/deleted retention; Snapshot/Inventory autosave/import/checkpoint boundaries; closed-session late callback rejection; capture-safe permanent deletion. Also included are three Save/Discard/Cancel `OpeningBackupSettingsHonorsNotebookDurabilityConsent` rows and `IsolatedSettingsWorkflowUsesRealSnapshotPreflightAndExclusiveRecoveryThenFreshRead`. The audit matches those source methods and parameters to completed gate identities, along with A2 stale-selection/disposal/reactivation/ancestor-save coordination, real shell trade/setup return, failed/cancelled navigation and retained draft fields. Image persistence, atomic placement, recovery, autosave, lifecycle/copy/delete, archive/export and helper/scheduling groups remain part of the complete gate.

All six integrated storage rows, three backup Settings-consent rows and the real Settings/maintenance round trip **passed**. All **10** linked-shell navigation/return/failure rows and **15** Desktop link coordination rows passed. Relevant keyboard/isolation/exact-selection/scheduling/STA regressions **57/57** passed (15 + 8 + 23 + 1 + 10). These counts overlap the project totals and are not additional tests.

Exact-dispatch evidence covers **four lifecycle**, **four image-editor**, **four Notebook link** and **three Breakdown** parents/children with one matching case per child. Plan Sections and Session Brief retain their existing single shared child per four parent cases, not four child launches. `dispatch.json` checks names/IDs, execution entries, cardinality, outcomes and supervised cleanup. The failed Plan Workspace shared suite is audited separately below. No dispatch implementation changed.

The existing trace flag reached isolated children: **10 `input-origin.log` files, 1,230 observations**, including all lifecycle and Breakdown children. `input-traces.json` records file paths, first/last observations, native-high-bit samples and release observations. **No sampled left/right native high bit was set.** This is bounded observation, not continuous monitoring or proof of equivalent input history. The two new focus-failing paths do not call `UiKeyboardProbe`, so they have no new native button samples; no second tracing mechanism was added. A green Breakdown family here does not explain the retained A2 failures.

| Timing scope | Seconds |
| --- | ---: |
| Complete Test gate, startup and accounting; excludes restore/build | **696.258115** |
| Exclusive partition through final accounting | **134.610480** |
| Parallel partition through final accounting | **560.865142** |
| Local margin against CI Test-step budget (600s) | **−96.258115** |
| Calendar supervised process | **77.578836** |
| Calendar headroom against existing 120s deadline | **42.421164** |

`timings.json` and Calendar `process-supervision.log` retain boundaries. Parallel project durations are not summed; post-partition `StageFinishedUtc` values are accounting timestamps, not individual project completion times. Wrapper startup/accounting and the inter-partition gap explain the remainder. `.github/workflows/ci.yml` still places `timeout-minutes: 10` on **Test**; restore/build are separate steps. This local failed run with tracing enabled exceeds that budget, but establishes neither a healthy-runtime benchmark, an actual matching hosted timeout nor hosted input-desktop compatibility. Tracing overhead was not separately measured.

### Exact failures and smallest next action

`failure-diagnostics.json` maps each failed parent to raw child results, errors/stacks, timestamps and any dumps. Supplemental `plan-focus-failure.json` and `preparation-focus-failure.json` preserve source boundaries and missing observations. Full class prefix for both families is `PersonalTradingJournal.Desktop.Tests.TradingPlans.`; paths below are under the raw gate's `parallel/` directory.

| Failure family and exact identity | Parent/child mapping and earliest observed failure | Evidence |
| --- | --- | --- |
| `TradingPlanWorkspaceViewTests.FullPageBindingsLifecycleGuardsFocusAndLightDarkResponsiveLayouts` | All four parents `(width: 980, dpi: 96)`, `(width: 1280, dpi: 144)`, `(width: 1600, dpi: 192)`, `(width: 460, dpi: 240)` fail because they await **one shared Lazy<Task>**. Child **980/96 fails**, other three children pass. In the Light native focus scenario, risk validation logs active=True / keyboard=TextBox / within=True; scenario validation logs active=False / keyboard=null / within=False, then line **125** fails `Assert.IsType<TextBox>(Keyboard.FocusedElement)`. Child interval **18:23:17.7739762–18:23:34.2143099 UTC**, testhost **20376**, STA **14**; supervisor **25896**, exit 1 at **18:23:57.9279735**, confirmed cleanup. | `plan-workspace-20261021-182308-4b655a0bbb264ff78e7c76d8c632aef0/plan-workspace.trx`, `calendar-sta-phases.log`, `process-supervision.log`, stdout/stderr |
| `SessionPreparationViewTests.CompiledBindingsAccessibleInputsValidationAndResponsiveLightDarkRenders` | One parent and matching child fail. After invalid level save, ApplicationIdle and successful selected-context assertion, line **111** sees null keyboard focus instead of TextBox; expected `Previous Day High` name/invalid-state assertions are not reached. Unparameterized native block follows the Light/Dark rendering loops (last resources Dark), window 650×750; native DPI is not recorded. Child interval **18:31:55.6104126–18:31:58.4616781 UTC**, testhost **15204**, STA **21**; supervisor **24996**, exit 1 at **18:31:58.8434779**, confirmed cleanup. | `plan-preparation-20261021-183152-5d779a468e864666945e5db2a1073a4d/plan-preparation.trx`, `calendar-sta-phases.log`, `process-supervision.log`, stdout/stderr |

These are **two distinct failed children and five failed parents**, not five separate root causes or four repeated Plan child executions. Both fail focus assertions; neither fails Space's released-input prerequisite or its capture-release wait. No watchdog increase, forced focus/capture or assertion repair was attempted. The Plan log demonstrates an inactive window at the assertion but has no timestamped deactivation/foreground owner transition explaining it. Session Preparation does not log its activation transition. Neither path samples native button state, so no earliest native-down transition can be inferred there. The unchanged `PageFocus` guard only assigns keyboard focus when its window is active; that is consistent with Plan's observation, not proof of which callback decision or external event caused the loss. No physical input, scheduling interference, permission denial or shared cause is established.

Smallest evidence-supported next action: a separately authorized bounded diagnosis of window activation/focus ownership and dispatcher completion between risk/scenario validation for **Plan 980/96**, and selected-context rebinding/validation focus for the **Session Preparation native block**. Preserve production focus guards and exact assertions before proposing any correction. Do not retry this gate or label these failures flaky. Historical A2 **201/203, two failed, zero skipped** and A3's two single passing diagnostics remain unchanged; the original pressed-button source is still unproven.

### Visuals, inventory and acceptance limitations

Reused A's **12 retained files / eight distinct images** in `TestResults/m2211a-verified/renders/`: `settings-{Light,Dark}-{760,400}-{top,restore}.png` and `maintenance-{Light,Dark}-{760,400}.png`. All eight distinct captures were inspected in B: 760 DIP/96 DPI and 400 DIP/240 DPI. Settings top/restore pairs have identical bytes. No blocking wrapping, contrast or action-visibility defect was found in the captured states. `visuals.json` records exact paths/hashes, matching retained bytes, and unchanged view/resource/ViewModel/fixture inputs since A's committed state. No separate capture run was needed; the complete gate retains its ordinary render outputs as well.

**B tracked inventory: exactly two modified files, `README.md` and `docs/notebook.md`, both edited after the runtime gate; zero added source files.** All implementation, harness, scheduling and diagnostics remain unchanged. `inventory-phases.json` lists the exact **19 distinct overall M22.11 files** across A/A2/A3, with shared paths counted once; A had nine files, A2 eleven, A3 four, and B adds no new overall path. Ignored B evidence contains preparation/context/source manifests, gate wrapper/output, raw gate, coverage/dispatch/failure/input/timing/visual audits and supporting-check results. Existing artifacts were not overwritten.

Whitespace/local documentation-link checks and EF consistency are checked after these edits; local-link checking excludes external URLs and fragments. No runtime gate is repeated for documentation changes. **M22.11 local automated Release acceptance remains pending** because of the retained failures. Document/archive **v1**, export **v18**, schema/ownership/retention guarantees are unchanged; general prepared-file reclamation remains deferred. Matching hosted CI timing/input compatibility, human interaction and native journal restart remain unverified. No production/provider access, Git publication, workflow triggering or M22.12 implementation occurred.

## M22.11A3 — Native pressed-button provenance diagnosis

21 October 2026, Europe/Kiev. Actual baseline: clean **`develop`, `715649439d98c2439ca6d05f6ab87dd5ed9e6472`**, existing worktree **`C:/Users/Yovko/source/repos/Personal-Trading-Journal`**. A2's eleven files were already committed, rather than the expected older HEAD with uncommitted files. No repository/ancestor AGENTS.md or active test process was found. Before edits/build, all **18 SHA256 entries** in A2 `verified-source.json` matched the current files, including tested binaries and relevant source. A3's rebuilt source/binary hashes and dirty inventory are in `TestResults/m2211a3-diagnostic/verified-source.json`. One agent, no subagents; production/navigation code and old artifacts preserved.

### Retained failure sequence and provenance

Exact identities below use prefix `PersonalTradingJournal.Desktop.Tests.Breakdown.BreakdownAccessibilityViewTests.`. Each A2 parent dispatched exactly its matching single child; there was no whole-suite duplication. Parent TRX: `TestResults/m2211a2-verified/breakdown-regressions/parents.trx`. `failure-diagnostics.json` maps raw errors and stacks; A3 `retained-failure-map.json` copies that map without altering A2. Times below are **UTC**; TRX timestamps carry `+03:00`.

| Exact parent/child suffix | First observations and earliest failure | Process / ownership | Child directory under A2 `breakdown-regressions/` |
| --- | --- | --- | --- |
| `OwnedFocusValidationAutomationAndRetainedStatesInBothThemes` | **15:17:03.8237142**, Light before Space: no capture, inactive mouse source, WPF Released; thread/async samples **0x0001/0x0001**, high bits clear. **15:17:04.2608679**, `ready`: still no capture/inactive source/WPF Released, native **0xFF80/0x8000**. First observed transition lies within the approximately **437ms preparation interval**, before key-down. After verified capture, **15:17:04.2867781** fails the released-input prerequisite, WPF Pressed and native high bits set. | Supervised launcher **26808**, actual testhost **8132**, managed STA **19**, fresh Application/dispatcher; `MoreFiltersButton#2964F2E`, `Window#18F0DAB`, HWND **0x7090C**, active source `#100207D`. Light's first Space fails; Dark is not reached. | `breakdown-accessibility-20261021-151655-e8cabc799ffa487b8d326a403443aab4/` |
| `ScopedTradesEntryDetailReturnMissingRowAndExitUseLiveFocusTargets(inputDuringDelete: False)` | First retained `ready` sample **15:17:13.9230016**: no capture, inactive mouse source, WPF Released; native **0xFF81/0x8001**, already down before key-down. Capture exposes active-source WPF Pressed; **15:17:14.0112678** fails with **0xFF81/0x8000**. The initialization/preparation origin interval was not recorded. | Launcher **21780**, testhost **28844**, managed STA **19** in a different process, fresh Application/dispatcher; first OpenDetail button **#308DFAD**, `Window#7AB499`, HWND **0x1E0904**, source `#2AA0E89`. No delete/return action is reached. | `breakdown-accessibility-20261021-151705-d05d286c37d546ee81473fe1758e680d/` |

Both use .NET **10.0.12**, WPF **10.0.12-servicing.26422.108+95017c711e6afc1085133d440e42b4bd78155701**, `WinSta0/Default`. Old traces identify the managed STA and matching dispatcher ownership, but not a native thread ID/dispatcher object identifier. Focus, loaded/visible/enabled state and managed/native capture were correct. `ClickMode=Release`, modifiers None, synthetic event `eventIsDown=False`. The failed check is **after capture acquisition**, before the pressed callback and before the second, immediately-pre-key-up prerequisite. Neither child reaches Space key-up, Click or successful command completion. This is not a release-wait timeout.

Observers detach at **15:17:04.2886534 / 15:17:14.0149228**, with capture still held and before fixture cleanup. Existing finally blocks close their owned Window, dispose/clear owned view state and shut down their Application; STA phase logs confirm dispatcher shutdown **15:17:04.4354149 / 15:17:14.1624476**. The old trace does not sample native button state after Window.Close, so it cannot establish button-state cleanup failure or success. Capture ownership is distinct from global button state; cleanup does not inject releases. Ordinary prerequisite failures produced no hang dumps.

Each directory retains `keyboard-readiness.log`, `calendar-sta-phases.log`, `process-supervision.log`, `breakdown-accessibility.trx`, stdout/stderr. Owned launcher interval **15:16:55.2743981–15:17:05.1505692**, Scoped **15:17:05.2050882–15:17:14.7004202**: sequential, distinct processes, no stale shared Application/STA demonstrated. A2's concurrently selected STA probes did overlap: unhandled-native launcher **25532**, **15:17:00.6327492–15:17:06.4595107**, spans Owned's failure; timeout/poison launcher **21216**, **15:17:06.5387631–15:17:11.9940432**, spans Scoped startup; import-resource composite launcher **15480**, **15:17:12.0714993–15:17:31.0459201**, spans Scoped's failure. Its nested import process starts only at **15:17:16.4406552**, after the failure. The unhandled/poison probes exercise dispatchers/exceptions rather than mouse injection. Raw intervals are retained in A3 `retained-process-overlap.json`. **Process overlap does not prove foreground/input competition**. Historical foreground HWND **0x2C055A** has no contemporaneous PID mapping; do not assign it retrospectively. No concurrency, partition or collection setting changed.

### Input path and interop audit

`Ready` brings the target into view, processes bounded dispatcher readiness, calls the existing thread-local `SetActiveWindow`, then `Keyboard.Focus`. Space raises WPF routed `KeyEventArgs` for down/up; it does not call `SendInput`, `mouse_event`, `keybd_event`, cursor positioning or post a mouse message. WPF's button acquires actual managed/native capture; the helper observes it and checks released native/WPF prerequisites. A source search found no global mouse injection on these callers. `SyntheticKeyboardState` saves/restores a 256-byte thread keyboard table in finally and changes Alt/Shift only, but has **no call path from these Breakdown cases or their STA probes**. Scoped's synthetic wheel is confined to the later `inputDuringDelete: True` branch, not this failing False path. Unrelated tooltip `SendMessage(WM_ACTIVATEAPP)` is not button injection and is not reached by the overlapping composite before this failure.

`GetKeyState(int)` and `GetAsyncKeyState(int)` return signed **SHORT**, correctly declared `short`; `& 0x8000` tests the documented high bit, not low-bit/toggle or reserved values. Thread state follows message-queue processing; asynchronous state is a separate observation. Async left/right refer to **physical** buttons regardless of swapped mapping; A2 did not record the mapping. The new trace records both buttons and `SM_SWAPBUTTON`. These sequential samples are not an atomic snapshot and cannot identify a human, driver, injector or another process. Additional async queries may consume its unreliable low bit, which assertions do not use. See Microsoft's [GetKeyState](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getkeystate) and [GetAsyncKeyState](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getasynckeystate) contracts.

Existing `SetActiveWindow` uses pointer-sized HWND input/return, with no INPUT/MOUSEINPUT layout or injection flags involved. A3 records the returned previous HWND; its existing declaration does not retain last error, so tracing explicitly does not report a purported error code. Readiness still validates actual active HWND/focus. Read-only GetCapture/GetFocus/GetForegroundWindow use HWND, GetWindowThreadProcessId uses DWORD return/out PID, and GetCurrentThreadId uses DWORD. The unrelated keyboard-state helper uses a 256-byte buffer, marshalled BOOL, SetLastError and checked results including restore; it changes thread state, not global asynchronous state. See [SetActiveWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setactivewindow) and [SetKeyboardState](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setkeyboardstate). No demonstrated signature/layout/flag defect, unintended mouse injection, reused fixture ownership or permission denial was found on the failed path.

### Bounded diagnostic execution

Retained logs could not locate the input source. Added opt-in **`PTJ_INPUT_ORIGIN_TRACE=1`** tracing only: target-free STA entry before fixture/Application, before/after Show, Ready entry, before/after SetActiveWindow and Keyboard.Focus with returned values, existing key/capture/focus/prerequisite/release events, and before/after Window.Close/Application cleanup. Native samples run on the same STA and identify managed/native thread, dispatcher, session, foreground PID/TID and owned target/window/capture. Target-free entry avoids initializing WPF input. No hooks, unrelated window contents, injected releases, forced state, direct commands, sleeps, synchronization or production changes. Assertions, exact dispatch/cardinality, process cleanup, **90s child / 30s watchdog** and existing five-second helper deadlines remain unchanged.

Each originally failed exact child ran **once**, sequentially through its normal parent; no other family, complete Breakdown selection or gate was run. Evidence root **`TestResults/m2211a3-diagnostic/`**. `dispatch.json` validates exactly one complete passed parent and matching child per run, zero skips/extras, and two distinct child identities. `summary.json` keeps these diagnostics separate from the unchanged A2 **201/203, two failed, zero skipped**.

| Run | Parent / child results | Exit | Command wall time | Child artifact directory |
| --- | --- | ---: | ---: | --- |
| `owned-once` | 1/1 each; zero failures/skips | 0 | **18.8947939s** | `owned-once/breakdown-accessibility-20261021-153315-55cc577fa6ba4d3292c548e2a567c959/` |
| `scoped-false-once` | 1/1 each; zero failures/skips | 0 | **10.2224721s** | `scoped-false-once/breakdown-accessibility-20261021-153345-3dc2eb30c9264892be547e5513544937/` |

Owned entry **15:33:18.9454597**, PID **6884**, managed STA **18**, native TID **29152**; Scoped entry **15:33:48.3026552**, PID **21104**, managed STA **18**, native TID **14140**. Both fresh process-local dispatchers happen to print hash `#4E5AE0`; that is not cross-process identity. Entry capture/active/focus HWNDs are zero, native high bits clear. Across **149 / 98** trace rows, both native button high bits remain clear, mapping **SM_SWAPBUTTON=0**. Owned's first async low bit is 1, then 0; it is not a down state. Own windows become foreground, HWND **0xEA04E8 / 0x150AB8**; SetActiveWindow returns those previous active HWNDs and focus returns the intended controls. Capture is verified, key-up invokes one Click per Space, and actual release completes before fixture cleanup (**six / four** release observations). Final samples show capture/active/focus zero and high bits clear; STA logs confirm shutdown. These are observations of the two diagnostic executions, **not an explanation of A2 or proof of flakiness**. Tracing adds observations/IO and these isolated runs do not reproduce A2's concurrent selection.

```powershell
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore
pwsh -NoProfile -File TestResults/m2211a3-diagnostic/context.ps1
$env:PTJ_INPUT_ORIGIN_TRACE='1'
pwsh -NoProfile -File TestResults/m2211a3-diagnostic/run-tests.ps1 -Name owned-once -Project Desktop -Filter 'FullyQualifiedName=PersonalTradingJournal.Desktop.Tests.Breakdown.BreakdownAccessibilityViewTests.OwnedFocusValidationAutomationAndRetainedStatesInBothThemes'
pwsh -NoProfile -File TestResults/m2211a3-diagnostic/run-tests.ps1 -Name scoped-false-once -Project Desktop -Filter 'FullyQualifiedName=PersonalTradingJournal.Desktop.Tests.Breakdown.BreakdownAccessibilityViewTests.ScopedTradesEntryDetailReturnMissingRowAndExitUseLiveFocusTargets&DisplayName~False'
pwsh -NoProfile -File TestResults/m2211a3-diagnostic/audit.ps1
pwsh -NoProfile -File TestResults/m2211a3-diagnostic/context.ps1 -OutputName execution-context-final.json
pwsh -NoProfile -File TestResults/m2211a3-diagnostic/check-whitespace.ps1
pwsh -NoProfile -File TestResults/m2211a3-diagnostic/check-doc-links.ps1
```

Build `build.log`: **exit 0, zero warnings/errors, 57.26s**. Wrapper expands to Release `dotnet test --no-build --no-restore` with durable parent TRX/vstest diagnostics, existing three-minute outer blame watchdog; per-run summary records exact arguments and elapsed time. Parent False selection is unique and verified against the full TRX identity; existing child uses exact full display identity and single-result validation. Literal-path readers are used throughout. Native execution used the already-authorized supported executor, **session 1 / WinSta0 / Default**, interactive/cursor available/error 0, existing administrator token, no UAC/token/desktop switch. Context artifacts record no active tests before/after. No denied access or alternate input path was used. Whitespace and local Markdown target existence checks follow documentation edits; external URLs/fragments are outside that local-link check. No EF/schema changes; A2 EF evidence remains applicable and was not rerun.

### Classification, inventory and next action

For **Owned**, state is demonstrated present before Space/capture, but not at its first pre-Ready observation; the exact event/source changing it inside preparation is missing. For **Scoped False**, state is already present at the first retained post-preparation observation; its earlier starting state and originating event/source are missing. In **both**, a helper/interop input defect or fixture/cleanup ownership defect is **not demonstrated**, and origin remains **unproven**. The A3 runs have released starting states and therefore do not fill those missing observations from a failing execution. No behavior correction is justified from this evidence. The smallest supported change is retaining the opt-in bounded tracing for a future authorized failing execution, to correlate the first high-bit transition with preparation/capture and contemporaneous ownership; even a transition between two samples needs event/source evidence before assigning causation. No further run is authorized by this diagnosis. Any eventual correction should first verify only the affected exact child with unchanged identity/cardinality, capture, Click/command and release assertions; expand helper regressions only if helper behavior changes.

Exact A3 tracked inventory: **four modified files, zero added source files** (baseline clean):

- `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/UiKeyboardProbe.cs` — opt-in native/managed phase observations and operation return values only.
- `tests/PersonalTradingJournal.Desktop.Tests/Breakdown/BreakdownAccessibilityViewTests.cs` — fixture initialization/Show/cleanup observation points only.
- `README.md` — diagnostic status, evidence and limitations.
- `docs/notebook.md` — this failure reconstruction, provenance, commands, inventory and classification.

Ignored `TestResults/m2211a3-diagnostic/` holds copied evidence helpers, audit/summary/dispatch/provenance, retained failure/overlap maps, context/build/supporting results and two unique run directories. Historical evidence is preserved. M22.11 acceptance and M22.11B remain **pending**; CI timing, matching hosted/input compatibility, human interaction and native journal restart remain unresolved/unverified. No production/provider access, Git publication, workflow triggering, general reclamation or unrelated refactoring.

## M22.11A2 — Linked-navigation ownership and keyboard prerequisites

21 October 2026, Europe/Kiev. Actual baseline: clean **`develop` at `2d2317b5bc7461e87eaef0e98c2c2c2d77e3cb00`**, existing worktree **`C:/Users/Yovko/source/repos/Personal-Trading-Journal`**. A's nine files were already committed. No repository/ancestor AGENTS.md or active test process was found. One agent; no subagents. Verification applies to this HEAD plus the implementation/test changes below, not HEAD alone. `TestResults/m2211a2-verified/verified-runtime-source.json` records the tested sources/binaries before documentation edits; `verified-source.json` includes the final documentation inventory. Earlier artifacts remain unchanged.

### Ownership correction and controlled coverage

A1 confirmed that accepting a new selection advances the read operation while `Note` can still contain the old detail until `ReadAsync` resumes after `Task.Yield`. The previous linked-navigation check compared the loaded note and activation, so a suspended resolver could invoke its callback from that abandoned selection. The shell then captured the loaded note again, losing the original ownership boundary.

The internal callback now carries one request containing origin note identity, workspace activation/cancellation and accepted-selection generation. Selection changes invalidate it before property notifications or replacement detail loading; same-note metadata refresh does not advance the generation. A→B→A remains stale. Resolution, unavailable-target messaging and failure publication check this request. The shell checks it before the ancestor/Notebook guards, after asynchronous guard completion and synchronously at navigation commitment, including after synchronous guards that can pump modal UI. Notebook preparation rechecks ownership after flush/save/discard awaits and modal consent before touching another draft. No application/storage contract changed.

Leave consent is associated with the linked request: stale cleanup cannot clear a newer request's consent, and failed destination recovery clears its own discard consent so the retained draft can resume autosave. Commands release their existing busy state. Return eligibility records the captured origin only after successful destination opening; abandoned requests create no return context. Existing Save/Discard/Cancel, unavailable-target fallback, image/editor history, autosave and recovery checks remain intact.

The original zero-callback assertion is retained and made deterministic by holding the replacement read's initial continuation while completing resolution with the old detail still installed. Added cases cover A→B→A, disposal/reactivation, production-shell rejection during resolution and save completion, and selection while an ancestor save guard is suspended. The latter installs a newer dirty draft and verifies no prompt/write/discard or shell transition touches it. Tests also verify no return-context mutation, command recovery, subsequent successful navigation and return to the correct origin. Existing trade/setup return tests retain exact body, folder, tag, search and sort assertions; cancellation retains the draft.

### Keyboard behavior and retained new failures

`UiKeyboardProbe` retains focused routed Space activation, actual managed/native capture acquisition, exactly one Click, fresh command-task validation and real release completion. It now requires the expected active mouse source, WPF Released, no modifiers, and released high bits from **both separately sampled** `GetKeyState(VK_LBUTTON)` and `GetAsyncKeyState(VK_LBUTTON)`, after acquisition and immediately before key-up. The trace names sampling methods and records event order, identities, capture, focus and source activity. The observations are sequential, not an atomic snapshot or interchangeable proof of physical versus queued input. Invalid prerequisites fail explicitly at that boundary; no forced state/release, injected mouse-up, direct command, arbitrary sleep or cleanup-as-success was introduced. Existing five-second readiness/release waits, 90-second child deadline, 30-second watchdog, isolation, partition/collection limits and CI settings remain unchanged.

The retained **Dark/1280/96 lifecycle case passed once**, then the **complete four-case family passed** through four distinct exact children. The original activation, command and release requirements completed. Two separate Breakdown regressions failed at the new prerequisite, before Space key-up/Click/command completion; their release requirements are **not verified** by those failed runs:

| Exact child identity (`BreakdownAccessibilityViewTests`) | Earliest failure / observed input | Retained child directory under `breakdown-regressions/` |
| --- | --- | --- |
| `OwnedFocusValidationAutomationAndRetainedStatesInBothThemes` | 15:17:04.286 UTC, `MoreFiltersButton#2964F2E`, HWND `0x7090C`: active WPF source, left Pressed, modifiers None; `GetKeyState=0xFF80`, `GetAsyncKeyState=0x8000`; managed/native capture and focus correct. | `breakdown-accessibility-20261021-151655-e8cabc799ffa487b8d326a403443aab4/` |
| `ScopedTradesEntryDetailReturnMissingRowAndExitUseLiveFocusTargets(inputDuringDelete: False)` | 15:17:14.011 UTC, button `#308DFAD`, HWND `0x1E0904`: active WPF source, left Pressed, modifiers None; `GetKeyState=0xFF81`, `GetAsyncKeyState=0x8000`; managed/native capture and focus correct. | `breakdown-accessibility-20261021-151705-d05d286c37d546ee81473fe1758e680d/` |

Both traces already show pressed native high bits **before key-down**, while the inactive WPF mouse source reports Released. Capture exposes the active WPF Pressed state; these observations do not establish which physical/device/queued-input source caused it. Foreground HWND `0x2C055A` differed, but that alone does not establish causation. There is no evidenced permission denial, wrong dispatcher or incomplete acquisition. The `inputDuringDelete: True` child passed. Each parent matches exactly one distinct child, including failures; no skipped/extra/zero-match child is accepted. Raw TRX, `keyboard-readiness.log`, phase logs, stdout/stderr and process-supervision logs remain in each directory. These ordinary prerequisite failures produced no hang dump. `failure-diagnostics.json` maps parent/child identities, errors, stacks and timestamps. No failing native case was retried and no production lifecycle behavior was changed. The new prerequisite diagnosis and passing lifecycle runs do not explain the earlier A Pressed observation.

### Focused Release results, context and commands

Durable root **`TestResults/m2211a2-verified/`**; `summary.json` preserves all invocations, including unsuccessful preliminary work. `dispatch.json` validates parent/child identity, cardinality and matching outcome; `audit.ps1` checks result completeness and duplicate identity without treating failed tests as passes.

| Final selection | Passed / total | Failed / skipped | Exit | Command wall time |
| --- | ---: | ---: | ---: | ---: |
| Navigation / save / autosave / recovery / image regressions | 139 / 139 | 0 / 0 | 0 | 21.987s |
| Exact retained Dark lifecycle precheck | 1 / 1 | 0 / 0 | 0 | 12.719s |
| Complete lifecycle family | 4 / 4 | 0 / 0 | 0 | 37.883s |
| Keyboard / isolation / exact dispatch / scheduling | 47 / 47 | 0 / 0 | 0 | 26.152s |
| Breakdown D1 + STA regressions | 11 / 13 | 2 / 0 | 1 | 46.898s |

The last row comprises **STA 10/10 and Breakdown 1/3**. Final distinct parent results: **201 passed / 203 total**, two failed, zero skipped; the Dark precheck overlaps the family and children are not added to parent totals. These are focused selections, not a complete-gate timing benchmark.

The first navigation invocation (`navigation-focused/`) aborted under the unchanged three-minute inactivity watchdog after **231 completed passes**, exit **1**. The new deterministic pause helper forwarded its initially queued continuation but accidentally held subsequent continuations; it now forwards all posts after release. The abort, TRX, sequence XML and `testhost_9956_20261021T181044_hangdump.dmp` remain preserved. The final rebuilt selection is narrowed to the requested Notebook/navigation regressions and verifies the correction; the incomplete run is not acceptance evidence. Initial `build-desktop.log` records a nullable-reference compile error, corrected before execution; final `build-final.log` passed with **zero warnings/errors**. `lifecycle-dark-exact/` records **MSB4177** from literal quotes in an MSBuild parent selector before discovery/execution. The equivalent unique parameter selector below selected one parent; its existing child dispatch still uses the exact full display name and single-result validation. This command parsing failure is distinct from a test failure.

```powershell
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore
pwsh -NoProfile -File TestResults/m2211a2-verified/context.ps1
pwsh -NoProfile -File TestResults/m2211a2-verified/run-tests.ps1 -Name navigation-final -Project Desktop -Filter 'FullyQualifiedName~NotebookLinkTests|(FullyQualifiedName~MainWindowViewModelTests&(FullyQualifiedName~Notebook|FullyQualifiedName~Linked))|FullyQualifiedName~NotebookEditingTests|FullyQualifiedName~NotebookAutosaveTests|FullyQualifiedName~NotebookImageCoordinationTests|FullyQualifiedName~NotebookSqliteWorkspaceTests|FullyQualifiedName~NotebookWorkspaceTests|FullyQualifiedName~NotebookCopyDeleteTests|FullyQualifiedName~NotebookLifecycleTests|FullyQualifiedName~NotebookImageAdmissionTests|FullyQualifiedName~DataBackupsFlowTests'
pwsh -NoProfile -File TestResults/m2211a2-verified/run-tests.ps1 -Name lifecycle-dark-selected -Project Desktop -Filter 'FullyQualifiedName=PersonalTradingJournal.Desktop.Tests.Notebook.NotebookLifecycleViewTests.TemplateAndReversibleLifecycleControls&DisplayName~Dark&DisplayName~1280&DisplayName~96'
pwsh -NoProfile -File TestResults/m2211a2-verified/run-tests.ps1 -Name lifecycle-family -Project Desktop -Filter 'FullyQualifiedName~NotebookLifecycleViewTests'
pwsh -NoProfile -File TestResults/m2211a2-verified/run-tests.ps1 -Name keyboard-regressions -Project Desktop -Filter 'FullyQualifiedName~UiHarnessDiagnosticsTests|FullyQualifiedName~IsolatedTestSelectionTests|FullyQualifiedName~IsolatedTestProcessTests|FullyQualifiedName~ReleaseSchedulingTests'
pwsh -NoProfile -File TestResults/m2211a2-verified/run-tests.ps1 -Name breakdown-regressions -Project Desktop -Filter 'FullyQualifiedName~BreakdownAccessibilityViewTests|FullyQualifiedName~CalendarStaTestTests'
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
pwsh -NoProfile -File TestResults/m2211a2-verified/audit.ps1
pwsh -NoProfile -File TestResults/m2211a2-verified/check-whitespace.ps1
pwsh -NoProfile -File TestResults/m2211a2-verified/check-doc-links.ps1
```

The reused wrapper refuses to overwrite a run directory and records exact arguments/exit/timing in each `summary.json`. It expands to `dotnet test <Desktop.Tests.csproj> -c Release --no-build --no-restore --filter <selection> --logger 'trx;LogFileName=parents.trx' --results-directory <run> --diag <run>/vstest.log --blame-hang --blame-hang-timeout 3m --blame-hang-dump-type mini`. Literal-path readers preserve bracket-bearing artifact names. Runtime tests used the already authorized supported executor: session **1**, interactive **WinSta0/Default**, cursor available/error **0**, existing administrator token, no UAC/token/desktop switching. `execution-context.json`, `execution-context-after-abort.json` and `execution-context-final.json` record this and absence of test processes before/after runs; reusable MSBuild nodes are not duplicate tests. EF reports no pending model changes. Whitespace and local documentation target existence checks run after these edits; URL/fragment validation is outside that link check's scope.

### Exact A2 inventory and remaining work

**11 modified files, zero added source files**; no pre-existing dirty files. Ignored diagnostic artifacts are separate.

| Area | Exact repository-relative paths |
| --- | --- |
| Workspace ownership and consent (3) | `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.cs`; `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Links.cs`; `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Editing.cs` |
| Shell commit and captured origin (2) | `src/PersonalTradingJournal.Desktop/ViewModels/MainWindowViewModel.cs`; `src/PersonalTradingJournal.Desktop/ViewModels/MainWindowViewModel.Notebook.cs` |
| Deterministic navigation coverage (2) | `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookLinkTests.cs`; `tests/PersonalTradingJournal.Desktop.Tests/Navigation/NotebookLinkedNavigationTests.cs` |
| Input prerequisites and regression coverage (2) | `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/UiKeyboardProbe.cs`; `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/UiHarnessDiagnosticsTests.cs` |
| Documentation (2) | `README.md`; `docs/notebook.md` |

Navigation correction is focused-verified. Original lifecycle activation/release passed, while two Breakdown cases remain blocked by native input prerequisites. The smallest next action is to review their retained native-input traces and establish the released-input prerequisite in the supported desktop before authorizing further focused verification; no force-release, timeout increase, retry-to-green or production lifecycle change is justified by these traces. Full M22.11 acceptance and M22.11B remain pending. Historical M22.10 acceptance, the **169.349s** CI-budget deficit, document/archive **v1**, export **v18**, 29 migrations and deferred general reclamation remain unchanged. Hosted timing/input compatibility, human interaction and native journal restart remain unverified. No complete gate, production/provider access, Git publication or workflow triggering occurred.

## M22.11A — Integrated Notebook backup and restore

21 October 2026, Europe/Kiev. Actual starting baseline: clean **`develop` at `ec6360b67be845b37e5c1bdf8c6e8eb2f3a511e3`**, existing worktree **`C:/Users/Yovko/source/repos/Personal-Trading-Journal`**. The reported M22.10C documentation changes had already been committed. No repository/ancestor AGENTS.md or active test process was found. One agent, no subagents. Results apply to this HEAD plus this phase's uncommitted source/test changes, recorded in `TestResults/m2211a-verified/verified-source.json`; no Git publication. Complete Release acceptance is reserved for M22.11B.

### Reused contract and demonstrated additions

The existing whole-database SQLite snapshot already captures every Notebook table/column: rich content and derived searchable text, folders/tags/assignments/pinning, Active/Archived/Trashed restoration metadata, typed links, current image ownership, durable recovery and retained cleanup ledger rows. Unreadable/future bodies remain exact strings. Required originals already include Notebook current/retained owned files and Trade references alongside existing Playbook/Plan images. No storage engine, application contract, migration, cleanup scheduler or archive/export format needed changing. The demonstrated product gap was the Settings disclosure still saying only Trade screenshots; it now includes required originals, Notebook recovery and the saved/checkpointed durability boundary. Backup documentation's historical M22.3A limitation is explicitly superseded.

SQLite snapshot and original-byte staging share the existing capture lease. A save/import/checkpoint committed before capture is included; one committed afterward is absent from that archive. Import publishes immutable original bytes before one canonical document/metadata/ledger/version commit. Permanent deletion can remove its canonical rows during capture, but physical cleanup waits until staged snapshot originals are safe; compression runs after lease release. There is no new compression-long UI lock. Only persisted/checkpointed drafts are durable. Opening Settings from a dirty Notebook uses the existing Save/Discard/Cancel guard.

`CleanupPending=false` means retained originals remain required, including unknown-reference retention after note deletion. `CleanupPending=true` is proven-safe deletion intent: the durable obligation survives even if those optional bytes have already gone; other required owners still win. Restoring a recent post-deletion backup does not recreate the note, while an explicitly chosen older backup restores its own earlier state. Copies retain independent IDs/keys/ownership and byte-identical originals. Untracked prepared files have no backup guarantee; general reclamation and automatic retries remain deferred.

Offline restore revalidates schema/manifest/integrity/required originals, requires closed normal sessions/file handles, captures verified installed-journal recovery, then uses its durable marker/directory-switch/rollback engine. Cancellation before replacement leaves the old journal intact; after the switch starts, verification or recovery completes. Fresh maintenance instances resolve simulated interruptions before normal startup. A disposed session prevents retained factories/services from writing even if an incoming note's ID/version would otherwise match. Fresh services and a fresh Notebook view model reload restored data/originals and expose recovery conflicts without overwriting canonical notes. These are fresh-context tests, **not native-process restart evidence**.

Compatibility is unchanged: **29 migrations**, latest `20261021121554_AddNotebookDeletionCleanup`; logical export **v18**; Notebook document **v1**; complete archive **v1**. These are separate version axes. JSON/four CSVs/inventory exports are not restore inputs. Older/future schema archives and unsupported archive versions fail before replacement. Installed-database migration does not extend archive support. [Full backup policy](backup-restore.md#integrated-notebook-backup-and-restore--m2211a).

### Focused evidence and commands

Durable root: `TestResults/m2211a-verified/`; final summary `summary.json`. All parent results are present and distinct; zero skipped cases. Counts exclude supervised children, which are retained separately in `children.json`.

| Release selection | Passed/total | Failed | Exit | Command wall time |
| --- | --- | --- | --- | --- |
| Infrastructure Notebook/Backups/Screenshot | **464/464** | 0 | 0 | 206.013s |
| Application Notebook/Backups | **64/64** | 0 | 0 | 5.239s |
| Desktop Notebook/DataBackups | **199/201** | 2 | 1 | 292.979s |
| Distinct selected parents | **727/729** | **2** | **not green** | Not a complete-gate benchmark |

The six new Infrastructure cases cover both chosen archive ages, both snapshot mutation boundaries, post-restore stale writes and capture/deletion ordering; all passed. Existing tests add exact all-table round trips, unreadable/future payloads, unavailable links, retained removed-image recovery, pending cleanup with missing optional bytes, missing/corrupt required originals, unsupported schemas/archives, cancellation, rollback and fresh-service interruption recovery. All three new Save/Discard/Cancel navigation cases and the strengthened real Settings/maintenance round trip passed. `coverage.json` records these identities. Focused selections overlap historical phase coverage and are not added to historical gate totals.

Final builds passed with **zero warnings/errors** (`build-final.log`, final Desktop `build-desktop-verified.log`); EF reports no pending model changes (`ef-consistency.log`). Tracked/untracked whitespace checks and **195** local documentation target existence checks passed. A reporting-only render grouping error was corrected from dictionary grouping to property-bearing records (`reporting-correction.log`); raw test evidence was unaffected and no runtime run was repeated.

The updated disclosure was rendered by the existing `DataBackupsLayoutTests` harness. `renders/` contains `settings-{Light,Dark}-{760,400}-{top,restore}.png` and `maintenance-{Light,Dark}-{760,400}.png`: **12 files, eight distinct images** (each Settings top/restore pair is byte-identical). All eight distinct images were inspected. The 760-DIP captures use 96 DPI; 400-DIP captures use 240 DPI. Wrapping, contrast, scroll access and visible controls have no blocking defect in the captured states. `visuals.json` lists exact paths/hashes. No unchanged Notebook UI was recaptured separately.

### Retained failures and next diagnostic boundary

The focused Desktop command ran once. Two existing cases failed; neither their assertions nor the affected runtime/helper code was modified. `failure-diagnostics.json` preserves raw parent/child errors, stacks, timestamps and TRX paths. No rerun-to-green, timeout increase, forced capture release or unrelated navigation fix was made.

| Failure | Earliest evidenced divergence | Evidence and remaining question |
| --- | --- | --- |
| `NotebookLinkTests.LateNavigationResolutionCannotOpenAnotherSelectedNoteOrDisposedWorkspace` | At 17:13:13 +03:00, `NotebookLinkTests.cs:134` expected zero navigation callbacks after selecting Other; actual count **1**. | `desktop-focused/parents.trx`, `output.log`. Source inspection: `SelectedNote` starts an asynchronous detail read while `Note` can still hold the previous snapshot; `OpenLinkedItemAsync` checks `Note.Id` and lifetime but not pending selection/read ownership. This is a concrete race candidate consistent with the assertion; exact interleaving still needs deterministic instrumentation. The same case passed in M22.10C (`historical-navigation-result.json`); that does not dismiss this failure. |
| `NotebookLifecycleViewTests.TemplateAndReversibleLifecycleControls("Dark", 1280, 96)` | Child 17:15:39–17:15:48 +03:00: `ArchiveNoteButton`, Space KeyUp cleared `IsPressed`, but native/managed mouse capture remained held through the existing five-second release deadline. | `desktop-focused/notebook-lifecycle-20261021-141534-e238ea37ba6743979c9aa40779807bf1/`: child TRX, `keyboard-readiness.log`, `calendar-sta-phases.log`, `process-supervision.log`, stdout/stderr. At KeyUp, left mouse state was **Pressed**; at failure it was Released while capture remained. The foreground handle differed from the test window; the initiating cause/process is not established. No permission denial was reported, and this was an assertion failure, not a watchdog timeout. |

The lifecycle parent propagated its one failed exact child; it is not a third distinct failure. The existing nonparallel `ExclusiveWpfCollection` remained enabled. The focused command selected both workload traits together; it did not execute the complete gate's two-stage partition ordering, so its elapsed time is not comparable to the gate. The other three lifecycle parents passed. Smallest next action: deterministic diagnosis of the late-navigation ownership boundary and native release/device-state trace before broader acceptance; keep these failures open rather than labeling them flaky or claiming backup tests prove their resolution. A required correction should receive its own narrow regression evidence before M22.11B.

The Desktop Settings flow uses the real database/snapshot/preflight/offline maintenance services with synthetic local data and fake UI choices/parent wait/reopen. It now includes a decoder-admitted original, a template note and conflicting durable recovery, then loads a fresh Notebook view model. Three new navigation cases verify Save/Discard/Cancel on entry to Settings. An initial Desktop test compilation caught two incorrect new test API names (`TradeReview`, `Activate`); they were corrected to existing `MarketStudy`/`ActivateAsync`. The failed build log is retained as `build-desktop-final.log`; this was a test-authoring compile error, not a runtime engine failure. Subsequent builds retain exact assertions and pass.

Commands (run from repository root; `run-tests.ps1` retains output, diagnostics, TRX, duration and exit code and refuses existing evidence directories):

```powershell
dotnet build PersonalTradingJournal.sln -c Release --no-restore
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore
pwsh -NoProfile -File TestResults/m2211a-verified/run-tests.ps1 -Name infrastructure-focused -Project Infrastructure -Filter 'FullyQualifiedName~Notebook|FullyQualifiedName~Backups|FullyQualifiedName~Screenshot'
pwsh -NoProfile -File TestResults/m2211a-verified/run-tests.ps1 -Name application-focused -Project Application -Filter 'FullyQualifiedName~Notebook|FullyQualifiedName~Backups'
$env:PTJ_BACKUP_RENDER_DIRECTORY = Join-Path (Get-Location) 'TestResults/m2211a-verified/renders'
pwsh -NoProfile -File TestResults/m2211a-verified/run-tests.ps1 -Name desktop-focused -Project Desktop -Filter 'FullyQualifiedName~Notebook|FullyQualifiedName~DataBackups'
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
pwsh -NoProfile -File TestResults/m2211a-verified/check-whitespace.ps1
pwsh -NoProfile -File TestResults/m2211a-verified/check-doc-links.ps1
```

The wrapper expands to `dotnet test <project> -c Release --no-build --no-restore --filter <selection> --logger 'trx;LogFileName=parents.trx' --results-directory <run> --diag <run>/vstest.log --blame-hang --blame-hang-timeout 3m --blame-hang-dump-type mini`; existing isolated child/watchdog and collection settings are unchanged. Native assertions use the existing explicitly authorized Windows executor: session **1**, **WinSta0/Default**, interactive, cursor available, existing administrator token; no UAC/token/desktop switch. `execution-context.json` records the probe and absence of active tests. Native input is an environment requirement; it is not evidence of matching hosted compatibility or human interaction.

### Exact M22.11A changed-file inventory

**Nine unique files: eight modified, one added.** No pre-existing dirty files were present. All other implementation and historical evidence is preserved.

| Area | Exact repository-relative paths |
| --- | --- |
| Product disclosure (modified) | `src/PersonalTradingJournal.Desktop/ViewModels/Settings/DataBackupsViewModel.cs` |
| Integrated SQLite/filesystem coverage (added) | `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/NotebookIntegratedBackupTests.cs` |
| Existing rich fixture reuse (modified) | `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/NotebookPortabilityTests.cs` (partial class only) |
| Desktop integration (modified) | `tests/PersonalTradingJournal.Desktop.Tests/Navigation/NotebookNavigationTests.cs`; `tests/PersonalTradingJournal.Desktop.Tests/Settings/DataBackupsFlowTests.cs`; `tests/PersonalTradingJournal.Desktop.Tests/Settings/DataBackupsTests.cs` |
| Documentation (modified) | `README.md`; `docs/notebook.md`; `docs/backup-restore.md` |

Ignored `TestResults/m2211a-verified/` evidence is separate from this source inventory. M22.10's passing gate remains historical evidence; no complete gate or timing benchmark is run in A. The unresolved local CI-budget deficit is **169.349s** against the unchanged **600s** Test-step limit. Matching hosted timing/input compatibility, human interaction, native journal restart, real clipboard/picker/viewer and installed-codec WebP remain unverified. M22.11B and later work remain pending. No production/provider access, workflow triggering or Git publication occurred.

## M22.10C — Release verification and acceptance

21 October 2026, Europe/Kiev. Actual verified baseline: clean **`develop` at `99004f29cfa54692c85c0aa29728226fdd6a8b2c`**, sole worktree **`C:/Users/Yovko/source/repos/Personal-Trading-Journal`**. Both A and B were committed before C; the reported B dirty state was historical. No repository/ancestor AGENTS.md or duplicate test process was found. One agent, no subagents. Runtime verification uses this exact HEAD with **no uncommitted source changes**; C changes only README.md and this notebook after/beside the run. `TestResults/m2210c-verified/verified-source-settings.json` records source, tests, gate and CI hashes.

**Local automated acceptance is complete.** One rebuilt complete gate passed **5,339/5,339**, zero failures/skips, **exit 0**. No replacement invocation or focused-suite rerun was needed. Final process inspection found no remaining test process; reusable MSBuild nodes were left alone.

| Project / partition | Passed / total | Failed / skipped |
| --- | ---: | ---: |
| Domain | 602 / 602 | 0 / 0 |
| Application | 1,048 / 1,048 | 0 / 0 |
| Infrastructure | 1,599 / 1,599 | 0 / 0 |
| Desktop exclusive | 92 / 92 | 0 / 0 |
| Desktop parallel | 1,998 / 1,998 | 0 / 0 |
| Desktop combined | 2,090 / 2,090 | 0 / 0 |
| All projects (Desktop counted once) | 5,339 / 5,339 | 0 / 0 |

Gate **769.349s (12m49.349s)**, exclusive **91.843s**, parallel **676.766s**; measured margin against 600 seconds **−169.349s**. Calendar child **48.415s**, **71.585s** headroom within its unchanged 120-second deadline, exit 0. Timing is a measured local overrun, not a hosted timeout diagnosis or evidence that any particular implementation caused the difference. Local acceptance passes; CI-budget risk remains unresolved. The smallest next step is a read-only timing/phase review of these retained artifacts before proposing changes; no timeout or concurrency change is made here.

Evidence index: **`TestResults/m2210c-verified/summary.json`**. Raw gate summary: **`TestResults/release-20261021-124953-09ac2d20d89c4eff930e4686f0ddf8e5/summary.json`**. `gate-start.json`, `gate-exit.json`, `timings.json`, `calendar.json`, `project-totals.json` and `complete-gate-output.log` retain invocation, accounting and exit evidence. Both partition directories retain top-level TRX, VSTest logs and isolated-child artifacts. In particular, the Desktop parallel TRX ends in **`net10.0[1].trx`** and was successfully read using `-LiteralPath`. All four reporting/coverage audits exited 0; `reporting-exits.json` is separate from the runtime exit. No reporting failure or test failure occurred in C.

`m2210-coverage.json` verifies **36 phase-specific test methods / 70 parameterized cases**, plus **2** external-ownership/pending-cleanup archive cases, all passed. This includes template commit boundaries, reversible lifecycle, duplication identities/bytes, stale versions, failure/cancellation/compensation, checkpoint races, fresh-context deletion/cleanup recovery and the capture-lease regression. Four lifecycle parents each completed one distinct matching isolated child in the exclusive partition. Broader `coverage.json` confirms image/atomic-placement, autosave/recovery, ownership, archive/export and SQLite workspace regressions actually executed; **8** atomic placement cases and all **35** linking/navigation cases passed. Image-editor dispatch remained four parents/four matching distinct children. Plan Sections **4/4**, Session Brief **4/4**, Breakdown **3/3** and **51** helper/scheduling/selection regressions passed (`ui-dispatch.json`), with cleanup and exact identities checked. These overlapping coverage groups and children are not added to the complete-gate total.

Release build: **zero warnings/errors**, **58.95s**, `release-build.log` / `build.json`. EF: **no pending model changes**, exit 0, `ef-consistency.log` / `ef-exit.json`. Final whitespace checks cover the overall A/B/C diff and C's worktree edits. Documentation checks validate local Markdown target existence (not remote URLs or anchors); their results are retained separately. Runtime source and gate/CI hashes remain unchanged after documentation edits, so the passing gate is not repeated.

The established `scripts/Test-Release.ps1` runs the exclusive WPF partition before the complementary parallel solution partition. Assertions, collection/project concurrency, child-process/watchdog deadlines, the 120-second calendar deadline and `.github/workflows/ci.yml`'s ten-minute Test-step budget remain unchanged. There is no added local 600-second cancellation wrapper. Gate wall time includes both partitions, startup and gate accounting, excluding build/restore. Partition accounting uses the recorded stage bounds; parallel project durations are not added, and post-command `StageFinishedUtc` values are not individual project completion times. Raw TRX and diagnostics remain authoritative if reporting fails; literal-path readers support bracketed artifact names, and reporting failure does not trigger a replacement gate.

Commands (repository root):

```powershell
dotnet build PersonalTradingJournal.sln --configuration Release --no-restore
& TestResults/m2210c-verified/run-gate.ps1
# The wrapper invokes this established gate exactly once:
pwsh -NoProfile -File scripts/Test-Release.ps1 -ResultsDirectory TestResults
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
& TestResults/m2210c-verified/measure.ps1
& TestResults/m2210c-verified/audit-gate.ps1
& TestResults/m2210c-verified/audit-ui.ps1
& TestResults/m2210c-verified/audit-m2210.ps1
& TestResults/m2210c-verified/check-whitespace.ps1
& TestResults/m2210c-verified/check-doc-links.ps1
```

The authorized native-test executor reported Windows **session 1 / WinSta0\Default**, interactive desktop, successful cursor query and an administrator token. This is the existing supported context explicitly requested for the run; scripts did not switch tokens/desktops or bypass a denied operation. `preflight-context.json`, `execution-context.json` and `final-context.json` record access/process state. A pass here does not establish matching hosted timing or input-desktop compatibility, human keyboard/mouse/Narrator behavior or native journal restart. Real clipboard/picker/viewer and installed-codec WebP remain unverified. Earlier Plan/Brief watchdog causes remain historically unresolved; current results do not retroactively explain them.

### Visual evidence and compatibility

A's **12** and B's **16** final render hashes match their retained manifests. B's final source manifest matches the current source, including UI and harness. The B inspection is reused for its 16 action/dialog captures; four existing final B `template-active` captures were also inspected in C, covering Light/Dark at 1280 DIP / 96 DPI and 480 DIP / 240 DPI. All selected **20** captures have readable template/lifecycle controls, note content and destructive confirmation; no blocking visual defect. `TestResults/m2210c-verified/visuals.json` lists exact paths and hashes: B's `renders/` plus its `desktop-final/notebook-lifecycle-*/m2210b-renders/*-template-active.png`. Historical A captures remain evidence of A; the final B captures show the added actions. No separate capture run was needed. The complete gate's normal harness may also emit its standard captures.

Five templates commit their complete initial document atomically. Archive/Return to Active/Trash/Restore preserve bodies and retained originals. Duplication uses a coherent expected-version source, fresh note/node/wrapper/managed-original identities and byte-preserving copies; failures never expose a partial copy. Confirmed permanent deletion is Trash-only, removes only that note's relationships/recovery, blocks delayed saves/checkpoints from recreating it, and detaches its editor/undo state. External targets and other notes remain independent.

Committed deletion is distinct from physical cleanup. B's corrected thread-affine capture lease and writer ordering protect bounded ownership/reference checks. Unknown references retain originals; failures preserve durable ledger obligations and report pending cleanup without claiming that the note still exists. Proven optional cleanup intent survives the file/database interruption boundary. Explicit bounded resumption is available; no automatic retry scheduler or broad orphan scan is introduced. General prepared-file reclamation remains deferred, including possible unregistered bytes when a process dies before staging returns ownership identity.

Logical export is **v18**, including `NotebookOwnedImageFiles.CleanupPending`; the additive migration defaults existing records to required retention. Canonical document JSON and complete archive stay **v1**. Archives retain database cleanup metadata and required originals, while proven optional cleanup bytes may be absent. Archive/preflight/offline-restore coverage exercises this state. This does not promise that older executables understand the new database schema or export v18. Full M22.11 acceptance and implementation remain deferred.

### Overall M22.10 inventory

A: **22 files** (`5fbbd1f..1d086c8`); B: **39 files** (`1d086c8..99004f2`); **8 shared**, yielding **53 unique files** overall. C edits only the two already-shared documentation files, adding no new unique implementation path. The phase manifest is checked against Git's cumulative diff from `5fbbd1ffb2e411c37aea2412f7aa4d9c58e7aa41`; ignored TestResults artifacts are evidence, not additional implementation files.

| File | Phases |
| --- | --- |
| `docs/notebook.md` | A, B, C |
| `README.md` | A, B, C |
| `src/PersonalTradingJournal.Application/Backups/IPortableExportService.cs` | B |
| `src/PersonalTradingJournal.Application/Notebook/INotebookImages.cs` | B |
| `src/PersonalTradingJournal.Application/Notebook/INotebookService.cs` | A, B |
| `src/PersonalTradingJournal.Application/Notebook/NotebookCommands.cs` | A |
| `src/PersonalTradingJournal.Application/Notebook/NotebookDuplication.cs` | B |
| `src/PersonalTradingJournal.Application/Notebook/NotebookTemplates.cs` | A |
| `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Autosave.cs` | A |
| `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.CopyDelete.cs` | B |
| `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.cs` | A |
| `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Editing.cs` | A |
| `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Images.cs` | A |
| `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Lifecycle.cs` | A, B |
| `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml` | A, B |
| `src/PersonalTradingJournal.Domain/Notebook/NotebookLifecycle.cs` | A |
| `src/PersonalTradingJournal.Domain/Notebook/NotebookNote.cs` | A |
| `src/PersonalTradingJournal.Infrastructure/Backups/PlaybookImageInventory.cs` | B |
| `src/PersonalTradingJournal.Infrastructure/Backups/PortableExportSchema.cs` | B |
| `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookImageFiles.Cleanup.cs` | B |
| `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookImageFiles.cs` | B |
| `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookRecoveryStore.cs` | B |
| `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.CopyDelete.cs` | B |
| `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.cs` | A, B |
| `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.Lifecycle.cs` | A |
| `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261021121554_AddNotebookDeletionCleanup.cs` | B |
| `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261021121554_AddNotebookDeletionCleanup.Designer.cs` | B |
| `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/JournalDbContextModelSnapshot.cs` | B |
| `src/PersonalTradingJournal.Infrastructure/Persistence/Records/NotebookRecords.cs` | B |
| `tests/PersonalTradingJournal.Application.Tests/Notebook/NotebookDuplicationTests.cs` | B |
| `tests/PersonalTradingJournal.Application.Tests/Notebook/NotebookTemplateTests.cs` | A |
| `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookCopyDeleteTests.cs` | B |
| `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookLifecycleTests.cs` | A |
| `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookLifecycleViewTests.cs` | A, B |
| `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookWorkspaceTests.cs` | A, B |
| `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/ReleaseSchedulingTests.cs` | A |
| `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/NotebookImagePortabilityTests.cs` | B |
| `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/NotebookPortabilityTests.cs` | B |
| `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/PortableExportTests.cs` | B |
| `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/SavedPeriodPortabilityTests.cs` | B |
| `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookCopyDeleteTests.cs` | B |
| `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookLifecycleImageTests.cs` | A |
| `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookPersistenceTests.cs` | B |
| `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookTemplateLifecycleTests.cs` | A |
| `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Initialization/JournalDatabaseInitializerTests.cs` | B |
| `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integration/ProductionPersistenceIntegrationTests.cs` | B |
| `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Migrations/InitialMigrationTests.cs` | B |
| `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePersistenceTests.cs` | B |
| `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePortabilityTests.cs` | B |
| `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookPersistenceTests.cs` | B |
| `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/DailyTradingPrincipleTests.cs` | B |
| `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/SharedTradingPlanTests.cs` | B |
| `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/TradingPlanPortabilityTests.cs` | B |

No production/provider access, Git publication, workflow triggering or M22.11 implementation occurred. Earlier sections retain their historical phase results and limitations.

## M22.10B — Duplication and permanent deletion

21 October 2026, Europe/Kiev. Actual clean starting baseline: **`1d086c8da0eb1cef6bf59e5c5df15cc7d513784d`**, `develop`, sole worktree **`C:/Users/Yovko/source/repos/Personal-Trading-Journal`**. M22.10A's 22 files had already been committed; the reported `5fbbd1f` baseline and uncommitted A state were historical. No applicable repository/ancestor AGENTS.md was found. One agent, no subagents. Results apply to HEAD plus this phase's uncommitted changes. Evidence root: **`TestResults/m2210b-verified/`**; existing A and earlier diagnostics remain untouched.

### Duplication policy and transaction boundaries

Active and Archived notes can be duplicated; Trash must be restored first. A duplicate is Active, unpinned, version **1**, in the source folder, with fresh creation/update UTC audit values and a fresh note identity. The chosen naming policy is **`Copy of <source title>`**, truncated to the existing 200-character title limit without splitting a surrogate pair. Titles need not be unique. Missing/unreadable/future source bodies return UnsupportedContent; unavailable owned originals fail the operation, never substitute an empty body. Existing unavailable Trade/Setup links and Trade screenshot references remain references with their original target identities.

The application clones all supported document nodes with fresh identities, preserving text/marks, heading levels, lists/check state, hyperlink URLs, captions, alternative text and exact canonical newlines. Every image wrapper gets a fresh identity. Owned originals are read with size/hash verification, staged through the existing admission/storage port under fresh note/image/storage identities, and copied without re-encoding their bytes. Trade screenshots retain external ownership and are neither copied nor deleted. Existing catalog tags are assigned to the copy; linked target records are unchanged.

The source is loaded as a coherent read snapshot. After staging all required files, one immediate SQLite writer transaction rechecks the source expected version/state, inserts the final note/document/projections, assignments, links, wrappers and ownership records, verifies readback and commits. Source changes during copying produce Conflict and no partial duplicate. Folder/tag catalog maintenance already advances affected source versions, so the same recheck protects copied references. There is no visible blank/partially populated note.

On failure/cancellation, a fresh non-cancelled read resolves whether the new note committed before any destructive compensation. A verified committed copy returns Success even when acknowledgement was lost. Otherwise the existing capture-lease/writer-locked compensation checks ownership and recovery for **only this operation's staged files**. Failed compensation records immutable prepared ownership where the database is available, reports CleanupPending, and retains bytes. Unresolvable database outcomes retain bytes without claiming durable acknowledgement. Process death before a published file's identity reaches the caller can still leave unregistered prepared bytes, as in the existing staging contract; no orphan scan or general collector is introduced.

### Permanent deletion, recovery and cleanup

Only a confirmed Trash note is eligible. The Desktop confirmation identifies the note by title and warns that its recovery drafts are also removed and the action cannot be undone. The operation guard captures identity/version and prevents competing selection/navigation/submissions while confirmation and persistence run. The service independently validates confirmation, Trash state and expected version under its immediate writer lock. Invalid/stale requests preserve the note, body, recovery and files. Malformed/future bodies may be explicitly deleted without decoding/conversion.

One canonical transaction adopts any legacy owned originals into the retention ledger, explicitly removes **only this note's** tag assignments, links and image wrappers, removes its recovery rows, then removes the note. Existing restrictive foreign keys remain; there is no cascade into catalog tags, Trades, Trading Setups, screenshots, folders or other notes. Recovery Put now checks source-note existence under the same writer lock: a checkpoint either commits first and is removed by deletion, or observes NotFound afterward. New sessions and delayed updates cannot recreate deleted recovery. Existing orphan recovery from older/raw source deletion remains readable; the new Put rule does not silently erase it. Stale canonical saves likewise return NotFound.

Database deletion and physical cleanup have distinct outcomes. A confirmed database commit returns **Success with DeletedNoteId**, even when cleanup fails; the UI says the note was deleted and files remain pending safe cleanup. An ambiguous commit is resolved through a fresh read before cleanup. After acknowledgement, the editor installs an empty draft, resets its revision/undo presentation, clears selection, removes that note's in-memory recovery rows, and refreshes counts. Unrelated notes/recovery/filter state remain intact.

`CleanupDeletedNoteAsync` is bounded to one deleted note and at most **128 ledger entries**. Under the established **capture lease → immediate writer** lock order it checks that the note is absent, the managed key matches its note/image identity, no other image/ledger/Trade/Playbook/Plan owner claims the key, and no retained canonical document or recovery body references the image. It inspects at most **128 notes and 128 recovery records**, checking version/length before reading bodies. Unknown, oversized, malformed or over-capacity content is conservative retention, never proof of absence. Other-note live undo remains protected by that note's retained ledger; the deleted note cannot persist its stale undo state back into a missing note.

For a proven unreferenced original, cleanup first commits **`CleanupPending=true`** in the ownership ledger. It then rechecks retention under a new writer transaction while retaining the capture lease, verifies the managed file's identity/hash and deletes only that original. Finally it removes the ledger row. An interruption after file deletion but before ledger acknowledgement leaves durable optional cleanup metadata, so a fresh service can safely finish that exact note's cleanup. Permission, alias, tampering or deletion failures retain the record; absence is distinguished from access failure. Unknown retention leaves `CleanupPending=false`, keeping those bytes required by complete archives. A later failed retention recheck restores the required-retention state. No arbitrary directories, source files or existing archives are deleted.

The ledger is the durable record of outstanding work: remaining rows whose source note is absent retain cleanup/retention obligations. This phase attempts bounded cleanup after deletion and exposes the same bounded service operation for explicit verified resumption; **there is no automatic retry scheduler or orphan collector**. General prepared-file reclamation remains deferred.

The capture mutex is thread-affine. Cleanup acquires it on a worker thread, synchronously joins the asynchronous database/file work there, and releases it on that same owner thread, including failure paths. The database work may resume on other threads; mutex ownership does not. A deterministic interceptor failure resumed from a dedicated thread verifies release by acquiring the same lease from another dedicated thread afterward.

### Desktop integration and compatibility

Duplicate uses A's Save / Discard / Cancel guard, drains in-flight saving, and snapshots the persisted version afterward. Failed duplication preserves an explicitly discarded draft until a verified commit; cancellation/conflicts do not replace it. The copy's editor opens after success while search/tag/sort choices are preserved. Archived/Pinned scope moves to All Notes so the Active/unpinned copy can be selected; if the current filter/page excludes it, its editor remains open with an explanatory message. New/Blank/Ctrl+N and A lifecycle behavior remain intact. Both actions prevent overlapping submissions and ignore disposed/foreign-note acknowledgements.

Additive migration **`20261021121554_AddNotebookDeletionCleanup`** adds non-null Boolean `NotebookOwnedImageFiles.CleanupPending`, default false for existing retention records. Exact migration-history/table/column assertions are extended, not weakened. Logical export advances **17 → 18** and explicitly includes the Boolean field. Document JSON stays **v1**. Complete archive stays **v1**: the database snapshot retains cleanup metadata, while only proven optional cleanup files are omitted from the required managed-file inventory. Current references and non-pending ledger originals still participate in size/hash completeness checks. Archive/preflight/offline-restore coverage includes a pending row whose bytes were already deleted, followed by fresh-service cleanup resumption. This is format compatibility with the new schema, not a claim that older executables understand it.

### Focused verification and evidence

**Implementation and focused Release verification complete.** Final results apply to HEAD plus the 39-file uncommitted B state. `summary.json` indexes commands and evidence; each selection retains `summary.json`, `parents.trx`, `output.log` and `vstest.log`.

| Selection | Passed / total | Failed / skipped | Exit | Invocation wall time |
| --- | ---: | ---: | ---: | ---: |
| domain-focused | 35 / 35 | 0 / 0 | 0 | 7.773s |
| application-focused | 27 / 27 | 0 / 0 | 0 | 7.668s |
| infrastructure-lease-final | 562 / 562 | 0 / 0 | 0 | 178.986s |
| desktop-final | 582 / 582 | 0 / 0 | 0 | 240.326s |
| lease-regressions | 2 / 2 | 0 / 0 | 0 | 13.252s |

The four broad selections contain **1,206 distinct passed parent cases**. The two lease regressions overlap Infrastructure; child results and earlier overlapping runs are not added. Invocation times include startup and are not summed across concurrent selections. Domain/Application and Desktop results remain valid after the final storage-only correction; `verified-source.json`, `final-source-manifest.json` and `final-binaries.json` record provenance and the three subsequent storage-only source/test changes. The affected storage/archive selection was rebuilt and rerun.

Final solution build: **zero warnings/errors**, `lease-build.log` (**21.02s**). EF: **no pending model changes**, exit 0, `ef-final.log`. Whitespace and local documentation-target checks are recorded in `whitespace.json` and `documentation-links.json`. No complete Release gate or CI timing measurement belongs to B.

Commands from the repository root (full argument arrays and invocation times are retained in each selection's summary):

```powershell
dotnet build PersonalTradingJournal.sln -c Release --no-restore
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
& TestResults/m2210b-verified/run-tests.ps1 -Name domain-focused -Project Domain -Filter 'FullyQualifiedName~Notebook'
& TestResults/m2210b-verified/run-tests.ps1 -Name application-focused -Project Application -Filter 'FullyQualifiedName~Notebook'
& TestResults/m2210b-verified/run-tests.ps1 -Name desktop-final -Project Desktop -Filter 'FullyQualifiedName~Notebook|FullyQualifiedName~Navigation.MainWindowViewModelTests|FullyQualifiedName~MainWindowHeaderTests|FullyQualifiedName~TradingSetupsViewModelTests|FullyQualifiedName~TradesViewModelTests|FullyQualifiedName~ReleaseSchedulingTests'
$selection = Get-Content TestResults/m2210b-verified/infrastructure-final/summary.json -Raw | ConvertFrom-Json
& TestResults/m2210b-verified/run-tests.ps1 -Name infrastructure-lease-final -Project Infrastructure -Filter $selection.Arguments[7]
& TestResults/m2210b-verified/run-tests.ps1 -Name lease-regressions -Project Infrastructure -Filter 'FullyQualifiedName~CleanupReleasesCaptureLeaseOnOwnerThreadAfterAsynchronousFailure|FullyQualifiedName~PendingDeletionWithoutBytesArchivesAndRestoresItsDurableCleanupState'
& TestResults/m2210b-verified/check-whitespace.ps1
& TestResults/m2210b-verified/check-doc-links.ps1
& TestResults/m2210b-verified/finalize-evidence.ps1
```

The storage filter is the established A selection: `FullyQualifiedName~Notebook|FullyQualifiedName~Backups|FullyQualifiedName~SqlitePersistenceIntegrityTests|FullyQualifiedName~InitialMigrationTests|FullyQualifiedName~JournalDatabaseInitializerTests|FullyQualifiedName~ProductionPersistenceIntegrationTests|FullyQualifiedName~TradingRulePersistenceTests|FullyQualifiedName~TradingRulePortabilityTests|FullyQualifiedName~PlaybookPersistenceTests|FullyQualifiedName~PlaybookImagePersistenceTests|FullyQualifiedName~DailyTradingPrincipleTests|FullyQualifiedName~SharedTradingPlanTests|FullyQualifiedName~TradingPlanPortabilityTests|FullyQualifiedName~LocalTradeScreenshotFileStorageTests`. The wrapper uses Release `--no-build --no-restore`, durable TRX/diagnostics and the established 3-minute outer hang collector; it refuses to overwrite a run directory. These are recorded invocations, not instructions to replace retained runs. CI, exclusive partition classification, collection limits and internal deadlines are unchanged.

The initial storage selection is retained as **541/561 passed, 20 failed, zero skipped, exit 1** (`infrastructure-focused/`, `initial-failures.json`). Findings: owned-image deletion had tracked rows left after SQL relationship removal; the ownership-adoption read now uses AsNoTracking. Two new ImmutableArray assertions now compare exact element arrays. Existing Deferred/export-v17/migration-history/column/count expectations were updated to the implemented contract and export v18/schema. The next targeted selection passed the five corrected deletion/copy/archive cases and exposed one remaining migration-count expectation (**5/6, exit 1**, `storage-corrections/`); both explicit history counts now include migration 29. An initial build-only missing namespace import is retained in `build-initial.log`; subsequent builds passed. No assertion, timeout or concurrency limit was removed.

The subsequent broad storage run (`infrastructure-final/`) passed **560/561**, with the pending-cleanup archive creation assertion failing (exit 1, zero skipped). That assertion originally omitted the detailed archive result; it now reports the result record on failure. Inspection found the new cleanup awaited while holding the existing thread-affine capture mutex, allowing disposal on a different thread and leaving capture blocked. The corrected owner-thread join and deterministic asynchronous-failure regression passed together with the archive/preflight/restore/resume case: **2/2**, exit 0 (`lease-regressions/`). The final broader storage selection includes that added regression. All failed artifacts remain retained; they are not included in final passing totals.

Domain/Application tests preserve templates and all node kinds, independent identities, title bounds and canonical content. SQLite coverage exercises staged multi-file failure, cancellation, changing source versions, compensation failure, fresh-context commit/acknowledgement boundaries, delete eligibility, stale writes/checkpoint races, unknown-body policy, copy independence, cleanup retention/failure, and no cross-note/external-owner deletion. Storage/archive/export selections include A/image/recovery/ownership/portability regressions and exact schema/export completeness.

The existing four lifecycle WPF parents retain exact one-child selection, independent Application/STA lifetime, exclusive WPF classification, native focus/capture/release checks and **90-second process / 30-second watchdog** limits. They now also keyboard-activate Duplicate/Delete, verify confirmation cancellation and editor/undo detachment, and render the actual themed confirmation card. Confirmation choice uses a controlled dialog port; physical modal interaction is not claimed. The initial render margin clipped the card's lower edge; final card capture excludes only the outer window margin. All raw captures are retained.

All **16 final captures** were inspected: Light/Dark at **1280 DIP / 96 DPI** and **480 DIP / 240 DPI**, each showing Archived/Duplicate, selected duplicate, Trash/Delete and permanent-delete confirmation. Action labels, state, content and confirmation text remain readable, the narrow layout wraps its controls, and Cancel/destructive actions are distinguishable. No blocking visual defect remains. Files are `renders/{Light|Dark}-{1280-96|480-240}-{archived|duplicated|delete-ready|delete-confirmation}.png`; `visuals.json` records exact source child paths and SHA-256 values. `dispatch.json` verifies all four parents and four distinct matching single-result children, zero failures/skips and successful process exits/cleanup. The last storage-only lease correction does not change the UI, view models or render harness; valid Desktop/render evidence is reused.

Native tests used the already-authorized **Windows session 1 / WinSta0\Default**, successful cursor query, interactive environment and observed administrator token. The default executor denied process-enumeration access; no permission was bypassed or token/desktop silently changed. `execution-context.json` / `final-context.json` record the supported executor used for tests. This does not establish human keyboard/mouse/Narrator behavior, native journal restart, hosted CI/input-desktop compatibility, real clipboard/picker/viewer or installed-codec WebP success.

### Exact B inventory and remaining work

**39 files: 30 modified + 9 added**, all belonging to this B phase; M22.10A was already committed. Ignored TestResults artifacts are listed separately in the evidence index.

```text
 M README.md
 M docs/notebook.md
 M src/PersonalTradingJournal.Application/Backups/IPortableExportService.cs
 M src/PersonalTradingJournal.Application/Notebook/INotebookImages.cs
 M src/PersonalTradingJournal.Application/Notebook/INotebookService.cs
 M src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Lifecycle.cs
 M src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml
 M src/PersonalTradingJournal.Infrastructure/Backups/PlaybookImageInventory.cs
 M src/PersonalTradingJournal.Infrastructure/Backups/PortableExportSchema.cs
 M src/PersonalTradingJournal.Infrastructure/Notebook/NotebookImageFiles.cs
 M src/PersonalTradingJournal.Infrastructure/Notebook/NotebookRecoveryStore.cs
 M src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.cs
 M src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/JournalDbContextModelSnapshot.cs
 M src/PersonalTradingJournal.Infrastructure/Persistence/Records/NotebookRecords.cs
 M tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookLifecycleViewTests.cs
 M tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookWorkspaceTests.cs
 M tests/PersonalTradingJournal.Infrastructure.Tests/Backups/NotebookImagePortabilityTests.cs
 M tests/PersonalTradingJournal.Infrastructure.Tests/Backups/NotebookPortabilityTests.cs
 M tests/PersonalTradingJournal.Infrastructure.Tests/Backups/PortableExportTests.cs
 M tests/PersonalTradingJournal.Infrastructure.Tests/Backups/SavedPeriodPortabilityTests.cs
 M tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookPersistenceTests.cs
 M tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Initialization/JournalDatabaseInitializerTests.cs
 M tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integration/ProductionPersistenceIntegrationTests.cs
 M tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Migrations/InitialMigrationTests.cs
 M tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePersistenceTests.cs
 M tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePortabilityTests.cs
 M tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookPersistenceTests.cs
 M tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/DailyTradingPrincipleTests.cs
 M tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/SharedTradingPlanTests.cs
 M tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/TradingPlanPortabilityTests.cs
?? src/PersonalTradingJournal.Application/Notebook/NotebookDuplication.cs
?? src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.CopyDelete.cs
?? src/PersonalTradingJournal.Infrastructure/Notebook/NotebookImageFiles.Cleanup.cs
?? src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.CopyDelete.cs
?? src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261021121554_AddNotebookDeletionCleanup.Designer.cs
?? src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261021121554_AddNotebookDeletionCleanup.cs
?? tests/PersonalTradingJournal.Application.Tests/Notebook/NotebookDuplicationTests.cs
?? tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookCopyDeleteTests.cs
?? tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookCopyDeleteTests.cs
```

Overall M22.10 acceptance remains pending. M22.10C owns the full Release gate and acceptance; this phase stops before C. General prepared-file reclamation and full M22.11 acceptance remain deferred. No production/provider access, Git publication, workflow triggering or subagent use occurred.

## M22.10A — Templates and reversible note lifecycle

21 October 2026, Europe/Kiev. Actual clean starting baseline: **`5fbbd1ffb2e411c37aea2412f7aa4d9c58e7aa41`** on `develop`, existing worktree **`C:/Users/Yovko/source/repos/Personal-Trading-Journal`**. The reported `570dfdb` baseline had advanced to include M22.9E documentation. No applicable repository/ancestor AGENTS.md or duplicate test run was found. One agent, no subagents. Results apply to this HEAD **plus the uncommitted implementation below**, not HEAD alone. Existing implementation, documentation and diagnostic evidence are retained.

Evidence root: **`TestResults/m2210a-verified/`**. `summary.json` indexes final results, `source-manifest.json` records the broad-run source/settings, and `final-source-manifest.json`, `final-binaries.json` and `inventory.json` retain final provenance. After the broad runs, the new view test received a render-surface correction and the existing exclusive-WPF classification; its scheduling regression was updated. All affected cases were rebuilt and rerun. Production source stayed unchanged after the broad verification.

### Templates and atomic creation

The immutable application definitions remain separate from user documents; there is no persisted/custom-template catalog. The agreed specification supplies **headings only**, using supported level-2 heading/run nodes and exact canonical paragraph newlines, with no invented trading evidence, images, links or statistics:

| Template | Initial sections |
| --- | --- |
| Blank Note | Empty document; Untitled title |
| Market Study | Context; Observation; Examples; Conclusion; Open questions |
| Trading Idea | Idea; Conditions; Invalidation; Evidence to collect |
| Lesson Learned | Situation; Observation; Lesson; Next action |
| Psychology | Trigger; Thoughts; Behavior; Alternative response |

Every creation generates a fresh note identity and fresh heading/run identities; editing one note cannot change a definition or another note. Existing title/content limits apply. The header has an accessible template selector and explicit **Create from template** action. **+ New note** and **Ctrl+N** still create Blank Note regardless of the selected template. Named templates use their names as initial titles. Creation uses the current folder only in Folder scope; all other scopes create Unfiled. After success, search/tag filters clear, sorting returns to recently updated, the destination folder or All Notes is selected, and the new editor/title receives focus.

`CreateNotebookNote` accepts an optional template, preserving existing callers. SQLite validates the folder inside the immediate writer transaction, constructs/encodes the final initial body, inserts the note at **version 1**, verifies readback and commits once. There is no initial blank-note commit followed by a content update. Cancellation/failure before the final boundary rolls back the new row. Cancellation after the non-cancellable commit returns the confirmed result; a fresh reader sees the complete template. Invalid template/title/missing-folder requests leave no partial note. No canonical JSON format or schema change is required.

### Reversible lifecycle and draft coordination

Active notes offer **Archive** and **Move to Trash**; Archived notes offer **Return to Active** and **Move to Trash**; Trash offers **Restore**. Trash uses the existing destructive confirmation convention. Archived/Trash content is read-only. No duplication/permanent-delete control is exposed. The shared body-independent domain transition rules are used by both normal notes and persistence metadata operations:

- Archive changes Active to Archived; Return to Active reverses it.
- Trash records the previous Active/Archived state and folder. Restore returns to that state and an existing previous folder, otherwise Unfiled. Pinning survives, while Pinned scope continues to include Active notes only.
- Each confirmed transition checks the expected version and advances it once, with UTC/non-backwards audit rules. Stale requests leave newer state/content intact.
- The SQLite transaction changes only lifecycle/restoration/version/update-audit columns. It never re-encodes the body or alters tags, links, image metadata/ownership, recovery rows or original files. Invalid/future bodies retain their exact raw JSON and projections; success can return unavailable-body metadata rather than fabricate editable content.
- Lists/counts refresh after confirmation. Existing search/tag filters remain; selection follows the existing page rule and clears when the transitioned note leaves that result. Archived and Trash remain searchable in their dedicated scopes; scope totals remain before text/tag filters and tag counts remain Active-note assignments.

A note-operation guard prevents overlapping creation/lifecycle/navigation/image/link/tag actions. Existing Save / Discard / Cancel guards stop debounce and drain in-flight autosave, including newer edits made during that save. Only the canonical transition freezes editing. Versions are taken after saving; late/disposed results cannot publish into another editor. Explicit discard is applied to the accepted draft revision only after verified persistence; failure/conflict/cancellation retains the editor draft. Checkpoint removal remains limited to confirmed saves or explicit discard, and unresolved recovery entries are retained. Confirmed commits arriving after cancellation are acknowledged without treating cancellation as rollback or reviving an abandoned draft. No file reclamation is performed.

Implementation review found that shell departure's `HasLeaveConsent` is false while the note mutation is writing. A separate acknowledgement consumes only the exact accepted discard revision after confirmed persistence; the cancellation-after-commit regression covers this boundary. Creation also clears stale linked-item presentation and the old “Choose a note” message.

### Focused Release verification

| Selection | Passed / total | Failed / skipped | Exit | Invocation wall time |
| --- | ---: | ---: | ---: | ---: |
| Domain Notebook | 35 / 35 | 0 / 0 | 0 | 6.164s |
| Application Notebook | 23 / 23 | 0 / 0 | 0 | 6.473s |
| Infrastructure Notebook/storage/recovery/archive/export | 542 / 542 | 0 / 0 | 0 | 243.984s |
| Desktop Notebook/navigation/linked-target integration | 573 / 573 | 0 / 0 | 0 | 222.622s |
| Final view/render cases, overlapping Desktop | 4 / 4 | 0 / 0 | 0 | 33.341s |
| Final exclusive view cases + scheduling regression, four overlapping Desktop | 5 / 5 | 0 / 0 | 0 | 32.989s |

The four non-overlapping broad selections contain **1,173 passed parent cases**; the additional scheduling regression brings the distinct focused set to **1,174**. Child results and repeated focused selections are not added to this total, and overlapping project durations are not summed. Domain/Application results were reused because their source stayed unchanged. Earlier passing `persistence-initial` **11/11** and `desktop-initial` **20/20** remain retained; expanded final suites include added cancellation/checkpoint regressions. This phase adds **6 Application + 12 Infrastructure + 22 Desktop** cases. Existing Domain lifecycle tests exercise the extracted shared rules.

Real SQLite tests observe creation before commit (no row) and after commit (one complete version-1 note) through fresh contexts/services; inject preparation failure/cancellation; verify lifecycle cancellation rollback and stale expected versions; and round-trip prior Active/Archived states with existing/deleted folders. They retain exact body JSON, canonical newlines, tags, unavailable links, pins, image bytes/metadata/ownership and unresolved checkpoints. Desktop coverage includes all five templates, independent identities, blank shortcut behavior, failure/discard retention, in-flight save/newer revisions, confirmed commit after cancellation, pending-operation/disposal guards, recovery, future-body actions, filters/counts/selection and repeated activation. Broader selections retain existing image/atomic-placement, autosave/recovery, links/navigation and portability regressions.

Full solution Release build passed with **zero warnings/errors**, `release-build.log` / `release-build-output.log` (MSBuild **17.53s**). After the render-only change, affected Desktop projects rebuilt with **zero warnings/errors**, `render-build.log` / `render-build-output.log` (**8.10s**). The final scheduling rebuild also passed with zero warnings/errors in `scheduling-build.log` / `scheduling-build-output.log` (**7.85s**). Earlier successful builds remain in `build-initial*` and `build-coordination*`. EF reports **no pending model changes**, exit **0**, in `ef-consistency.log`. Final tracked/new-file whitespace and **177 local documentation targets / zero missing** checks passed and are retained in `whitespace.json` and `documentation-links.json`; URL/fragment validation is outside that established link check.

Commands from repository root; exact expanded argument arrays, start/end times and exit codes are in each selection's `summary.json`. The durable wrapper sets child results under the selection directory and retains output, TRX, vstest diagnostics and hang artifacts:

```powershell
$evidence = 'TestResults/m2210a-verified'
pwsh -NoProfile -File "$evidence/context.ps1" -OutputName execution-context.json
dotnet build PersonalTradingJournal.sln -c Release --no-restore -v minimal -flp:logfile=TestResults/m2210a-verified/release-build.log
pwsh -NoProfile -File "$evidence/run-tests.ps1" -Name domain-focused -Project Domain -Filter 'FullyQualifiedName~Notebook'
pwsh -NoProfile -File "$evidence/run-tests.ps1" -Name application-focused -Project Application -Filter 'FullyQualifiedName~Notebook'
$desktop = 'FullyQualifiedName~Notebook|FullyQualifiedName~Navigation.MainWindowViewModelTests|FullyQualifiedName~MainWindowHeaderTests|FullyQualifiedName~TradingSetupsViewModelTests|FullyQualifiedName~TradesViewModelTests'
$storage = 'FullyQualifiedName~Notebook|FullyQualifiedName~Backups|FullyQualifiedName~SqlitePersistenceIntegrityTests|FullyQualifiedName~InitialMigrationTests|FullyQualifiedName~JournalDatabaseInitializerTests|FullyQualifiedName~ProductionPersistenceIntegrationTests|FullyQualifiedName~TradingRulePersistenceTests|FullyQualifiedName~TradingRulePortabilityTests|FullyQualifiedName~PlaybookPersistenceTests|FullyQualifiedName~PlaybookImagePersistenceTests|FullyQualifiedName~DailyTradingPrincipleTests|FullyQualifiedName~SharedTradingPlanTests|FullyQualifiedName~TradingPlanPortabilityTests|FullyQualifiedName~LocalTradeScreenshotFileStorageTests'
pwsh -NoProfile -File "$evidence/run-tests.ps1" -Name desktop-final -Project Desktop -Filter $desktop
pwsh -NoProfile -File "$evidence/run-tests.ps1" -Name infrastructure-final -Project Infrastructure -Filter $storage
# Each wrapper runs dotnet test <project> -c Release --no-build --no-restore --filter <filter>
# with TRX, --diag, --blame-hang --blame-hang-timeout 3m --blame-hang-dump-type mini.
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore -v minimal -flp:logfile=TestResults/m2210a-verified/render-build.log
pwsh -NoProfile -File "$evidence/run-tests.ps1" -Name desktop-renders-final -Project Desktop -Filter 'FullyQualifiedName~NotebookLifecycleViewTests'
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore -v minimal -flp:logfile=TestResults/m2210a-verified/scheduling-build.log
pwsh -NoProfile -File "$evidence/run-tests.ps1" -Name desktop-scheduling-final -Project Desktop -Filter 'FullyQualifiedName~NotebookLifecycleViewTests|FullyQualifiedName~ReleaseSchedulingTests'
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
pwsh -NoProfile -File "$evidence/context.ps1" -OutputName final-context.json
git diff --check
pwsh -NoProfile -File "$evidence/check-whitespace.ps1"
pwsh -NoProfile -File "$evidence/check-doc-links.ps1"
pwsh -NoProfile -File "$evidence/audit.ps1"
```

These are recorded invocations, not instructions to overwrite evidence: `run-tests.ps1` refuses an existing selection directory. No complete gate was run; CI timeout, partition ordering, collection/project concurrency and existing test deadlines are unchanged.

The initial local whitespace wrapper forced `core.autocrlf=false`, incorrectly treating retained CRLF endings as trailing whitespace. `whitespace-initial.log` and `check-whitespace.initial.ps1` preserve that audit failure. The corrected wrapper honors the repository's existing `core.autocrlf=true`; standard `git diff --check` and all nine new-file checks pass without source rewrites or relaxed whitespace rules.

### Visual and input-desktop evidence

Four visible parent cases retain **Light/1280/96, Dark/1280/96, Light/480/240, Dark/480/240**, each selecting exactly one matching child by full display name with independent TRX identity/cardinality validation. `dispatch.json` audits the broad, render and final scheduling selections: four distinct children, one completed passed result per parent, no extra/skipped/zero-match success, matching definitions/entries and exit **0**. Process supervision confirms cleanup. The new suite carries the existing `ExclusiveWpf` trait and nonparallel collection, preventing it from overlapping other Desktop collections and placing it in the established gate's first partition. `ReleaseSchedulingTests` verifies this membership and the unchanged two-thread collection limit; no gate/CI configuration changed. Each child owns its Application/STA and retains the **90-second process / 30-second watchdog** deadlines. Live keyboard activation uses the existing focus/capture/release helper, fresh command execution and retained `IsPressed == false` assertions for Create, Archive, Trash, Restore and Return to Active. Ctrl+N's command mapping is checked.

Native checks used the explicitly authorized input-desktop executor: **session 1, WinSta0\Default, UserInteractive=true, cursor query successful**, administrator token observed. No UAC/token/desktop switch was requested; this records the actual environment without claiming administrator rights are intrinsically required. `execution-context.json` and `final-context.json` retain context/process checks. The restricted-executor limitation documented in D1 remains applicable. Routed input plus native focus/capture checks do not establish physical keyboard device-state behavior or human interaction.

All **12 final captures** were inspected: `TestResults/m2210a-verified/renders/{Light,Dark}-{1280-96,480-240}-{template-active,archived,trash}.png`. `visuals.json` records original final-child paths and SHA-256 hashes. Templates/headings, lifecycle controls, state labels and read-only actions are readable; controls wrap at 480 DIP, the content remains scrollable, and no blocking visual defect was found. Initial review caught the stale creation message (corrected) and unused lower pixels from the live window's desktop-limited viewport. The final harness retains live native assertions but renders a separate WPF surface at the requested 900-DIP height, checking that rendering changes neither draft identity/revision nor dirty state. Initial captures and `visuals-live-window.json` remain preserved; the final images are not physical screen captures.

### Exact phase inventory and remaining work

**22 files = 13 modified + 9 added**, relative to `5fbbd1f`, all uncommitted. Ignored TestResults artifacts/scripts are local evidence outside this source inventory; there were no unrelated entry changes to discard.

| Status | Path |
| --- | --- |
| Modified | `README.md` |
| Modified | `docs/notebook.md` |
| Modified | `src/PersonalTradingJournal.Application/Notebook/INotebookService.cs` |
| Modified | `src/PersonalTradingJournal.Application/Notebook/NotebookCommands.cs` |
| Added | `src/PersonalTradingJournal.Application/Notebook/NotebookTemplates.cs` |
| Modified | `src/PersonalTradingJournal.Domain/Notebook/NotebookNote.cs` |
| Added | `src/PersonalTradingJournal.Domain/Notebook/NotebookLifecycle.cs` |
| Modified | `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.cs` |
| Added | `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.Lifecycle.cs` |
| Modified | `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.cs` |
| Modified | `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Editing.cs` |
| Modified | `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Autosave.cs` |
| Modified | `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Images.cs` |
| Added | `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Lifecycle.cs` |
| Modified | `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml` |
| Added | `tests/PersonalTradingJournal.Application.Tests/Notebook/NotebookTemplateTests.cs` |
| Added | `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookTemplateLifecycleTests.cs` |
| Added | `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookLifecycleImageTests.cs` |
| Modified | `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookWorkspaceTests.cs` |
| Added | `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookLifecycleTests.cs` |
| Added | `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookLifecycleViewTests.cs` |
| Modified | `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/ReleaseSchedulingTests.cs` |

Document/archive **v1** and export **v17** remain compatible. Archive/Trash retain canonical records and original image files, including ownership/recovery references; prepared-file retention and physical reclamation remain unchanged/deferred. Fresh-context/service interruption tests are **not** a native journal restart. Human keyboard/mouse/Narrator interaction, native journal restart, matching hosted CI/input-desktop compatibility, real clipboard/picker/viewer and installed-codec WebP remain unverified. Historical Plan/Brief watchdog causes are not claimed resolved by these focused tests.

**M22.10A is complete; overall M22.10 acceptance remains pending.** M22.10B owns duplication/permanent deletion; M22.10C owns the complete Release gate and acceptance. Full M22.11 acceptance/reclamation remains deferred. No complete gate, production/provider access, Git publication, workflow triggering or M22.10B implementation occurred.

## M22.9E — Release reverification

21 October 2026, Europe/Kiev. Actual starting and verified HEAD: **`570dfdb6dcd67da3ecd52a971ddbf8f732d54d91`**, clean `develop`, sole worktree **`C:/Users/Yovko/source/repos/Personal-Trading-Journal`**. The reported `0830e0d` baseline had advanced: D1's five files were committed in `570dfdb` before this phase. No applicable repository/ancestor `AGENTS.md` or active duplicate test run was found. One agent, no subagents. Verification applies to this exact HEAD with **no uncommitted runtime changes**; only README.md and this document were edited afterward. Existing implementation and B/C/D/D1 diagnostics remain preserved.

Evidence root, abbreviated **E evidence** below: **`TestResults/m229e-20261021-111109-e8f617a6f17b45688027e122c84cbf83/`**. `start.json`, `gate-start.json`, `verified-source-settings.json` and `verified-binaries.json` retain entry state and SHA-256 provenance. Source/settings manifest SHA-256: **`36F5BAD55AF881F1BFC0AAD806036967DD489EA7293190C04849CF733E1EE081`**. Final summary verifies source/settings and binary hashes remain unchanged after documentation edits.

### Execution context and preserved gate

Native-input checks used the **explicitly authorized input-desktop executor used for D1**, not the restricted executor that denied its cursor query. Read-only preflight and gate-entry probes recorded Windows **10.0.19045**, interactive session **1**, window station **WinSta0**, thread desktop **Default**, `GetCursorPos=true` / error **0**, and an **administrator token** in that executor. No script requested UAC elevation, token switching or desktop switching. This does not establish that administrator rights are inherently required; it records the actual verified environment. `preflight-context.json`, `execution-context.json`, `final-context.json` and `dotnet-info.txt` retain details. Final process inspection found no test runner/host; seven reusable MSBuild workers were left untouched. No permission-related runtime failure occurred in E.

The helpers retain routed WPF key-down/up handling, actual focus and managed/native capture checks; they do not emulate physical keyboard device state or establish human interaction. The Owned child retained `GotMouseCapture` followed by `LostMouseCapture`, `IsPressed=false`, managed capture null and native capture zero before the release assertions; see `owned-release-excerpt.log` and the child's complete `keyboard-readiness.log`. There was no direct-command substitution, forced state, skipped native assertion or cleanup-based success.

One invocation of unchanged `scripts/Test-Release.ps1` ran **exclusive WPF first, then the parallel solution partition**, with exact complementary filters. Desktop/Infrastructure collection bounds remain **2/4**, and project concurrency, process-tree cleanup, duplicate-identity/completeness checks, host recursion guards and all assertions are unchanged. Relevant children retain **90-second process / 30-second case-watchdog** limits; Calendar retains **120 seconds**; the outer inactivity watchdog remains **3 minutes**. `.github/workflows/ci.yml` still gives only its Test step **10 minutes**. No new local 600-second cancellation wrapper was introduced.

### Complete results and timing

Full solution Release build passed, **exit 0, zero warnings/errors**, MSBuild **58.47s** (invocation **58.921s**), recorded in `release-build.log`, `release-build-output.log` and `build.json`. Current binaries contain all D/D1 corrections. The gate ran once, **11:13:02.2392972–11:22:02.1860475 UTC**, **exit 0**.

| Project | Passed / total | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Domain | 602 / 602 | 0 | 0 |
| Application | 1,038 / 1,038 | 0 | 0 |
| Infrastructure | 1,567 / 1,567 | 0 | 0 |
| Desktop | 2,060 / 2,060 | 0 | 0 |
| **Complete gate** | **5,267 / 5,267** | **0** | **0** |

Desktop comprises **88 exclusive + 1,972 parallel** results. Child cases and overlapping historical focused selections are not added to these totals. The gate includes nine additional harness regression rows relative to B's 5,258-case set. Raw logs, parent/child TRX, process supervision, phase logs and diagnostic artifacts are retained under **`TestResults/release-20261021-111302-1a25b9fed088430d934df15004ef3d8f/`**, with its **`summary.json`** as the complete-gate summary. E `summary.json`, `gate-exit.json`, `project-totals.json`, `coverage.json`, `ui-dispatch.json`, `dispatch.json` and `failures.json` provide the audited report. Deliberately failing child probes belong to passing harness regression assertions; no parent failed.

| Measurement | Seconds |
| --- | ---: |
| Complete gate, including startup/accounting, excluding build/restore | **539.947** |
| Exclusive partition through its result accounting | **69.615** |
| Parallel partition through its final result accounting | **469.668** |
| Between-partition accounting gap | 0.028 |
| Remaining wrapper/startup/accounting | 0.636 |
| Signed local margin against 600 seconds | **+60.053** |
| Calendar supervised child | **54.227** |
| Calendar headroom against 120 seconds | **65.773** |

`timings.json` records exact intervals; no overlapping project durations are summed, and post-command `StageFinishedUtc` values are not treated as individual project completion times. `calendar.json` points to the actual supervisor start/exit records. This establishes one measured local margin, **not** matching hosted timing or repeatability. B's failed **953.408s** run remains diagnostic history, not a healthy-runtime comparison or a claimed controlled speedup.

### Required families, visual reuse and supporting checks

`ui-dispatch.json` audits names, IDs, definition/entry mappings, completion timestamps and outcomes against the actual parent TRX:

| Family | Parents passed | Child launches | Distinct completed children |
| --- | ---: | ---: | ---: |
| Plan Sections | 4 / 4 | 1 shared suite | 4 |
| Session Brief | 4 / 4 | 1 shared suite | 4 |
| Breakdown accessibility | 3 / 3 | 3 exact selections | 3 |

Each Breakdown child contains exactly one intended passed result, with no extra/skipped/zero-match result. Relevant helper/selection/scheduling regressions passed **51/51**. All **35 new linking/navigation cases** passed (**29 linking + 6 navigation**), including four distinct matching link-view children. Image-editor dispatch remains four matching distinct children. The required persistence, ownership, autosave/recovery, image and archive/export families executed, including **eight atomic placement/interruption/order cases**; `coverage.json` reports no missing or failed coverage. These are subsets of the complete total.

All **eight** validated M22.9A/B captures were reused and visually reviewed again: Light/Dark × **1280 DIP / 96 DPI** or **480 DIP / 240 DPI** × linked-items/picker. Paths: **`TestResults/m229b-verified/renders/`**, names `{Light,Dark}-{1280-96,480-240}-{linked-items,picker}.png`. E `visuals.json` records each path, original A capture, SHA-256 and review. Every hash matches B's validated archive; `git diff d6f8911 HEAD -- src` is empty, so no production UI was affected. Labels wrap, actions remain reachable, Dark picker contrast is readable, and unavailable references retain a disabled Open details plus available Remove link. No blocking visual defect found; clipping at a scrolled viewport edge reflects the recorded scroll position. No additional visual run was needed.

EF model consistency passed, **exit 0**, with no pending changes: `ef-consistency.log` / `ef-exit.json`. Final whitespace checks cover this phase and the complete M22.9 diff; **both exit 0**. Documentation target check: **176 local targets / zero missing**, exit **0**, excluding URL/fragment validation. Runtime tests were not repeated after documentation edits.

The first three post-run evidence readers exited **1** because PowerShell interpreted the literal Desktop filename `net10.0[1].trx` as a wildcard. This was an **audit-reader error**, not a missing result or gate failure; the established gate already uses literal paths. The local E readers were corrected to `Get-Content -LiteralPath` and completed with **exit 0**, without changing tests or rerunning the gate. `audit-reader-correction.json`, the three `*.initial.ps1` copies and `audit-exits.json` retain the finding and correction.

Commands from repository root (preflight, gate and final context inspection used the explicitly authorized input-desktop executor):

```powershell
$evidence = 'TestResults/m229e-20261021-111109-e8f617a6f17b45688027e122c84cbf83'
pwsh -NoProfile -File "$evidence/context.ps1" -OutputName preflight-context.json
dotnet build PersonalTradingJournal.sln -c Release --no-restore -v minimal -flp:logfile=TestResults/m229e-20261021-111109-e8f617a6f17b45688027e122c84cbf83/release-build.log
pwsh -NoProfile -File "$evidence/run-gate.ps1"
# The once-only wrapper executes: pwsh -NoProfile -File scripts/Test-Release.ps1 -ResultsDirectory TestResults
pwsh -NoProfile -File "$evidence/measure.ps1"
pwsh -NoProfile -File "$evidence/audit-gate.ps1"
pwsh -NoProfile -File "$evidence/audit-ui.ps1"
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
pwsh -NoProfile -File "$evidence/context.ps1" -OutputName final-context.json
git diff --check
git diff --check 9c4ed749256728a2c2067c5aa983722d56a302cc --
pwsh -NoProfile -File "$evidence/check-doc-links.ps1"
pwsh -NoProfile -File "$evidence/summarize.ps1"
```

### Exact overall inventory and phase boundaries

Net M22.9 inventory versus **`9c4ed749256728a2c2067c5aa983722d56a302cc`**: **28 files = 16 modified + 12 added**. Status below is relative to that baseline; phase columns identify actual contributions. A's **22 files** were committed in `d6f8911`; B's **two documentation files** in `6f82eea`; C changed no files; D's **eight files (five modified, three added)** in `0830e0d`; D1's **five modified files** in `570dfdb`. E modifies **only README.md and docs/notebook.md**, uncommitted after verification. The six distinct harness files added/changed in D/D1 are listed separately by their phase labels below. Ignored TestResults scripts/artifacts are local evidence, outside this source inventory. `inventory.json` retains exact per-commit lists.

| Status | File | Phases |
| --- | --- | --- |
| M | `README.md` | A, B, D, D1, E |
| M | `docs/notebook.md` | A, B, D, D1, E |
| M | `src/PersonalTradingJournal.Application/Notebook/INotebookService.cs` | A |
| A | `src/PersonalTradingJournal.Application/Notebook/NotebookLinkTargets.cs` | A |
| M | `src/PersonalTradingJournal.Application/Notebook/NotebookQueries.cs` | A |
| M | `src/PersonalTradingJournal.Desktop/MainWindow.xaml` | A |
| A | `src/PersonalTradingJournal.Desktop/ViewModels/MainWindowViewModel.Notebook.cs` | A |
| M | `src/PersonalTradingJournal.Desktop/ViewModels/MainWindowViewModel.cs` | A |
| M | `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Autosave.cs` | A |
| M | `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Editing.cs` | A |
| A | `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Links.cs` | A |
| M | `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.cs` | A |
| M | `src/PersonalTradingJournal.Desktop/ViewModels/Setups/TradingSetupsViewModel.cs` | A |
| M | `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml` | A |
| A | `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.Links.cs` | A |
| M | `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.Queries.cs` | A |
| A | `tests/PersonalTradingJournal.Application.Tests/Notebook/NotebookLinkTargetContractTests.cs` | A |
| M | `tests/PersonalTradingJournal.Desktop.Tests/Breakdown/BreakdownAccessibilityViewTests.cs` | D, D1 |
| A | `tests/PersonalTradingJournal.Desktop.Tests/Navigation/NotebookLinkedNavigationTests.cs` | A |
| A | `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookLinkTests.cs` | A |
| A | `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookLinkViewTests.cs` | A |
| M | `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookWorkspaceTests.cs` | A |
| A | `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/UiCaseTrace.cs` | D |
| A | `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/UiHarnessDiagnosticsTests.cs` | D, D1 |
| A | `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/UiKeyboardProbe.cs` | D, D1 |
| M | `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/PlanSectionsViewTests.cs` | D |
| M | `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/SessionBriefCompositionTests.cs` | D |
| A | `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookLinkTests.cs` | A |

### Acceptance and limits

**M22.9 local automated Release acceptance is complete.** Trades and Trading Setups (including underlying Playbook identities) remain the only link targets; inactive records remain eligible, unavailable references remain removable without reassignment, and guarded details/return navigation retains Notebook context and drafts. Atomic image placement, associated metadata/ownership and one expected-version advance remain a single canonical commit. Document/archive **v1**, logical export **v17** and existing original ownership/retention remain compatible. Prepared files may survive abrupt precommit death; reclamation and full M22.11 acceptance remain deferred.

Historical Plan/Brief watchdog causes and the foreground-competition hypothesis remain unresolved. This passing complete run verifies the current source in the recorded input-desktop environment; it does not prove those old causes fixed, guarantee CI headroom, or establish restricted/hosted input compatibility. Matching-source hosted CI, human keyboard/mouse/Narrator interaction, native journal restart, real clipboard/picker/viewer and installed-codec WebP success remain **unverified**. Fresh-context/service interruption coverage is not a native-process restart claim. Smallest next action: review a future matching hosted run's Test-step timing and native-input context when one is available; no trigger or publication was performed. **M22.10 onward remains pending.**

## M22.9D1 — Keyboard release and capture synchronization

21 October 2026, Europe/Kiev. Actual starting and verified HEAD: **`0830e0dc1fc262b8d09fe5c5ee2801b59390e62b`**, clean `develop`, sole worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. The reported `6f82eea` baseline had advanced: all eight D files were committed in `0830e0d` before this phase. No applicable repository/ancestor `AGENTS.md` or duplicate test run was found. One agent, no subagents. Existing D implementation/evidence is preserved. New evidence root: **`TestResults/m229d1-20261021-100829-cd02d9a16afc4737bb8a1d03f8a6143a/`**, abbreviated **D1 evidence** below. Results belong to HEAD plus the three uncommitted test files listed below; two documentation files were edited after runtime verification.

### Confirmed sequence and environment requirement

One **diagnostic-only exact Owned parent/child** ran before behavior changes and reproduced the retained failure. It changed only observation: identity/state snapshots, actual key/click/capture/focus events, `IsPressed` transitions and counts of observed dispatcher operations not yet completed. No hot layout loop was logged. Diagnostic source copies, build, parent/child TRX, stdout/stderr, phase and supervision logs are retained. The child was `diagnostic-owned/breakdown-accessibility-20261021-101120-3ad5d232e5ef4ac29e020bee8d06a677/`.

The first **Light** iteration used the same `MoreFiltersButton#20E8A21`, attached to `Window#25F8C1E`, STA 18, HWND **0x2D04F4**, `ClickMode.Release`, actual keyboard focus and a loaded/visible/active window. Its sequence:

| UTC on 21 October | Observed event/state |
| --- | --- |
| 10:11:28.644314 | Before key-down: unpressed, no managed/native capture |
| 10:11:28.668827 | Key-down set `IsPressed=true` |
| 10:11:28.672407 | Handled Space key-down returned; HWND had native capture, but `Mouse.Captured=null`, `IsMouseCaptured=false`; no GotMouseCapture event |
| 10:11:28.678525 | After the original pressed-state/Idle/render observation: managed capture still absent |
| 10:11:28.680528 | Key-up raised exactly one Click; button still pressed |
| 10:11:28.681174 | Handled Space key-up returned; no LostMouseCapture transition |
| 10:11:28.704166 | Existing ContextIdle returned; `IsPressed=true`, native capture still held, retained release assertion failed (diagnostic source line 181) |

Observed outstanding operations around key-up included Input, Render and Send. Those counters cover operations posted while the observer was attached, not a global dispatcher census. A later ContextIdle boundary still found no managed capture; this was not evidence of a delayed successful acquisition waiting for another arbitrary sleep.

The actual installed **WPF 10.0.12** binaries were inspected read-only through an isolated assembly-load context. `matching-wpf-buttonbase-il.txt` and `matching-wpf-capture-provider-il.txt` retain IL and SHA-256 identities, product version **`10.0.12-servicing.26422.108+95017c711e6afc1085133d440e42b4bd78155701`**. ButtonBase sets its pressed flag before attempting capture; key-up only calls release when managed capture is present, and LostMouseCapture clears the pressed flag. The capture provider can acquire the HWND yet return failure while initializing an inactive mouse source through cursor lookup. Public [.NET WPF ButtonBase source](https://github.com/dotnet/wpf/blob/v10.0.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/Primitives/ButtonBase.cs) and [mouse-provider source](https://github.com/dotnet/wpf/blob/v10.0.0/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/InterOp/HwndMouseInputProvider.cs) corroborate this flow; the local IL establishes the matching installed behavior.

The **confirmed immediate cause** is incomplete managed capture, followed by key-up consuming an invalid input sequence. A separate read-only cursor query in the restricted executor returned **false / Win32 error 5 (access denied)**, recorded in `cursor-availability.json`; that call was not retroactively attributed to the exited child. This establishes an environment limitation consistent with WPF's capture-provider initialization path. Verification therefore used approved **input-desktop access**, explicitly differing from the restricted diagnostic run. There is no claim that waiting alone makes unavailable desktop input work, or that a specific native API return inside the original child was instrumented.

The helper still raises genuine routed Space key-down/up events through WPF's control handlers; it does **not** emulate physical keyboard device state. The diagnostic `KeyEventArgs.IsDown`/`Keyboard.IsKeyDown(Space)` remained false even for the synthetic key-down. Actual keyboard focus, modifiers, mouse-button state and managed/native capture are independently checked. This remains an automated routed-input test, not proof of human keyboard interaction.

### Narrow correction and release evidence

`UiKeyboardProbe.Space` now rejects pre-existing capture, requires the intended control to own both managed and native capture after key-down, and requires that ownership to persist until key-up. It then waits on the actual capture/pressed-state condition, scheduling checks at ContextIdle after capture/property events, with a **five-second bound inside the unchanged case deadline**. Success requires `IsPressed=false`, no target managed capture and no remaining capture by its HWND. It does not set/clear control state, manufacture capture events, reacquire/release capture on behalf of the button, move post-action focus, call the command directly or perform cleanup to make release succeed. Exactly-one-Click and fresh execution-task validation remain intact; original focus and release assertions and production `PageFocus` remain unchanged. Failure produces phase, target/window/dispatcher, mouse/keyboard source, capture, activation and pending-operation diagnostics.

The corrected exact Owned child shows, for Light: **10:16:33.702076 GotMouseCapture**, **10:16:33.721081 LostMouseCapture with `pressed=false`, managed capture null and native capture zero**, then exactly one Click and release completion; the original release assertion observed false at **10:16:33.809979**. Dark repeats the sequence at **10:16:34.053138 / 10:16:34.073020**, with the assertion at **10:16:34.129858**. Both use control **#3C0D6B1** on HWND **0xD0720**. All later Owned validation, cancellation, date-picker, retained-state/theme/render assertions completed. Cleanup occurs afterward and is separately identified. The normal full family reproduced passing capture/release evidence for all three children.

Two new isolated regression rows verify consecutive successful Space invocations return only after release, and that deliberately losing capture during the pressed observation fails before key-up with **zero Clicks**. That negative fixture does not pretend cleanup completed a successful activation. Existing missing/stale-task, failure propagation, exact-selection/completeness, dispatcher lifetime, cancellation/process-tree cleanup and scheduling regressions are retained.

### Focused results and commands

Both affected-project Release builds passed with **zero warnings/errors**: diagnostic build **53.89s**, final build **9.91s**, exits **0**, `diagnostic-build.log` / `release-build.log`. Tests ran in order below, once per requested selection; there were no repeated attempts until green.

| Selection | Passed / total | Failed / skipped | Exit | Invocation wall time |
| --- | ---: | ---: | ---: | ---: |
| Diagnostic-only Owned, restricted executor | 0/1 | 1 / 0 | 1 | 14.002s |
| Corrected exact Owned, input-desktop access | 1/1 | 0 / 0 | 0 | 16.287s |
| Complete Breakdown family, input-desktop access | 3/3 | 0 / 0 | 0 | 25.964s |
| Relevant regressions, input-desktop access | 51/51 | 0 / 0 | 0 | 33.501s |

The final family plus disjoint regressions cover **54 unique passing parent cases**. The exact Owned pass overlaps the full family and is not added. Diagnostic failure remains historical evidence, not suppressed. Child results are not added to parent totals. Wall times include test-command/child startup and return, excluding build/restore; they do not establish complete-gate savings or CI margin.

`dispatch.json` audits complete result counts, exact parent/child names, distinct identities, definition/entry mappings and timestamps. Normal full-family children each contain exactly one completed matching Passed result, zero skipped/extra/zero-match cases:

- Owned: `breakdown/breakdown-accessibility-20261021-101723-a084c1d538cc4d14b5afa0bac033e68e/`, supervised **12.987s**, exit **0**.
- Scoped `inputDuringDelete: False`: `breakdown/breakdown-accessibility-20261021-101736-adc40fc76d9049319914b0722f34a5b6/`, **4.891s**, exit **0**.
- Scoped `inputDuringDelete: True`: `breakdown/breakdown-accessibility-20261021-101741-7326d39a9f6846c1b31d05fab228d253/`, **4.749s**, exit **0**.

The separate corrected Owned child is `corrected-owned/breakdown-accessibility-20261021-101629-54ef0ef8b232495ab1fe56734fc93d1a/`, **12.919s**, exit **0**. Names retain the full `PersonalTradingJournal.Desktop.Tests.Breakdown.BreakdownAccessibilityViewTests` prefix and existing method identities. Exact dispatch and built-in single-result validation remain active. Process **90s**, case-watchdog **30s**, supervisor-cleanup limits, host recursion protection, case-owned Application lifetime, CI timeout, partition ordering and concurrency/collection settings are unchanged.

Commands from repository root (the last three test invocations used input-desktop access; diagnostic-only used the restricted executor):

```powershell
$evidence = 'TestResults/m229d1-20261021-100829-cd02d9a16afc4737bb8a1d03f8a6143a'
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore -v minimal -flp:logfile=TestResults/m229d1-20261021-100829-cd02d9a16afc4737bb8a1d03f8a6143a/diagnostic-build.log
pwsh -NoProfile -File "$evidence/run-focused.ps1" -Family diagnostic-owned
pwsh -NoProfile -File "$evidence/inspect-wpf.ps1"
pwsh -NoProfile -File "$evidence/inspect-provider.ps1"
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore -v minimal -flp:logfile=TestResults/m229d1-20261021-100829-cd02d9a16afc4737bb8a1d03f8a6143a/release-build.log
pwsh -NoProfile -File "$evidence/run-focused.ps1" -Family corrected-owned
pwsh -NoProfile -File "$evidence/run-focused.ps1" -Family breakdown
pwsh -NoProfile -File "$evidence/run-focused.ps1" -Family regressions
pwsh -NoProfile -File "$evidence/audit.ps1"
git diff --check
pwsh -NoProfile -File "$evidence/check-doc-links.ps1"
```

The wrapper runs `dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter <selection> --logger 'trx;LogFileName=parents.trx' --results-directory <unique selection path> --diag <selection>/vstest.log`, records exact arguments/exit/UTC/wall time in each `summary.json`, and refuses to overwrite an existing selection. Owned selects its exact fully qualified fact name; full family uses `FullyQualifiedName~BreakdownAccessibilityViewTests`. Regression filter: `FullyQualifiedName~IsolatedTestProcessTests|FullyQualifiedName~CalendarStaTestTests|FullyQualifiedName~ReleaseSchedulingTests|FullyQualifiedName~UiHarnessDiagnosticsTests|FullyQualifiedName~IsolatedTestSelectionTests`. No complete gate or unrelated Plan/Brief rerun occurred. Runtime tests were not repeated for documentation changes. Final whitespace **exit 0**; documentation **175 local targets / zero missing, exit 0** (URL/fragment validation excluded). No test host/runner remains; five reusable MSBuild workers were left untouched. Checks, final process status, source/binary hashes and inventory are recorded in D1 `summary.json` and supporting files.

### Exact D1 inventory and remaining limitations

**Five modified files, no additions**, uncommitted over `0830e0d`; ignored TestResults diagnostics/scripts are local evidence outside this source inventory. Earlier D files and retained artifacts remain intact.

| File | D1 change |
| --- | --- |
| `README.md` | Current focused outcome and environment limitation |
| `docs/notebook.md` | Cause, sequence, commands, evidence and inventory |
| `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/UiKeyboardProbe.cs` | Acquisition/release requirements and compact event/dispatcher diagnostics |
| `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/UiHarnessDiagnosticsTests.cs` | Two isolated capture/release regression rows |
| `tests/PersonalTradingJournal.Desktop.Tests/Breakdown/BreakdownAccessibilityViewTests.cs` | Theme/instance observations bracketing existing input and release assertion |

**D1 focused verification is complete; overall M22.9 acceptance remains pending.** Native capture requires a suitable input desktop; success is not claimed in an executor denying that access. No production defect was demonstrated, and production code was not changed. D's Plan/Brief focused passes are reused; their historical watchdog/slowdown causes and foreground-competition hypothesis remain unresolved. No whole-gate timing conclusion follows from these focused results. Human keyboard/mouse/Narrator interaction, native journal restart and matching hosted CI remain unverified; prior real clipboard/picker/viewer, codec, retention/reclamation and M22.11 limitations remain. No production/provider access, Git publication, workflow triggering or M22.10 work occurred. **M22.10 onward remains pending.**

## M22.9D — UI test isolation, keyboard readiness and diagnostics

21 October 2026, Europe/Kiev. Starting and verified HEAD: **`6f82eeae6ebce27f6c13b650ede071b314f2db0a`**, clean `develop`, sole worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. Results apply to that HEAD **plus the six uncommitted test/helper files listed below**, not HEAD alone. README and this document were edited afterward. No applicable repository/ancestor `AGENTS.md` or active duplicate test host was found. One agent, no subagents. Existing implementation and B/C evidence are preserved. Evidence root for this phase: **`TestResults/m229d-20261021-093039-46dc5ef268c440d6958173add86c0597/`**, abbreviated **D evidence** below; `start.json` records entry state and `summary.json` records final inventory/hashes/checks.

### Demonstrated defects and scoped corrections

C's read-only diagnosis found that eleven failed B parents represented five child launches: four Plan Sections parents shared one child, four Session Brief parents shared one child, and each of three Breakdown parents dispatched the entire three-case class. Breakdown also reused `Application.Current` after its original dispatcher shut down. Some synthetic keys never started the expected command. Plan's retained dump sampled automation/layout work; Brief's sampled a dispatcher pump waiting on an unidentified task. Neither sample proved deadlock or 30 seconds of stalled UI work, and foreground competition remained a hypothesis. The three exact-row C probes passed; they did not establish a complete family pass. Original B evidence remains under `TestResults/m229b-verified/` and its referenced release directory; C probes remain under `TestResults/m229c-diagnostic/`.

Breakdown now constructs each parent’s complete discoverable display name, including the Boolean row argument, and passes `ExactDisplayNameFilter` plus `expectedTestName` to the existing supervisor. All three parents remain discoverable. Existing single-result validation still checks completed counters, exact result/definition/entry identities and timestamps; child failure propagation, recursion protection and process-tree cleanup are unchanged. Each child asserts that no previous Application exists, creates its own Application on its STA, checks the live dispatcher before theme iteration, and calls Application shutdown only during final cleanup. The two themes and all offscreen variants within the Owned case retain the same live Application; they do not shut it down between iterations. Final STA logs show shutdown completed before process exit, with no later case reusing that Application.

`UiKeyboardProbe` requires a loaded/visible window, a connected target on the same dispatcher/HWND, enabled/focusable state, command readiness and actual keyboard focus. It flushes layout/bindings at ContextIdle, activates only the test thread's HWND through `SetActiveWindow`, and checks activation/focus through bounded event-driven readiness with a **five-second** limit inside the unchanged outer deadlines. It does not request global foreground activation, sleep arbitrarily or repair post-action focus. UTC diagnostics include window/target state, keyboard/native focus, foreground HWND and command readiness. Space must produce exactly one actual Click; async consumers must observe a non-null execution task distinct from the previous invocation. Detail-read fixtures stay pending until the test verifies the reader call and fresh task, then release the original command normally. Missing/stale tasks fail instead of being replaced or null-suppressed. All original focus, keyboard, validation, rendering and accessibility assertions remain; production `PageFocus` is untouched.

Plan Sections and Session Brief retain their original four-case shared child and persistent dispatcher. `UiCaseTrace` writes UTC and monotonic timing at child entry, fixture/task creation, STA scheduling/action entry, application/resources/window initialization, layout/Idle boundaries, named task waits, cleanup and case exit. Operation expression, source line, task ID/status, process and thread IDs make the pending operation traceable from its creation site. Original pump priorities/behavior remain; no hot event/layout callback is instrumented. Window/fixture cleanup and the unchanged child-process ownership of dispatcher shutdown are explicit. Plan's automation-remove path now fails if no command task exists instead of accepting a completed-task fallback. No speculative layout/performance or production correction was made.

### Focused Release verification

The Desktop test project and referenced Release projects were rebuilt. Final build **exit 0, zero warnings/errors, 16.90s**: `release-build-final.log`. Initial `release-build.log` retains **CS0619/xUnit2014** caused by selecting xUnit's obsolete Task-returning `Assert.Throws` overload in a new synchronous validation test; using an Action block corrected that compile error before any runtime test. No failed build evidence was overwritten.

Each family below ran **once through normal parent dispatch**, in separate focused invocations, without changing project/collection concurrency or partition membership. The complete gate was not run.

| Selection | Parent passed/total | Failed / skipped | Exit | Parent invocation wall time |
| --- | ---: | ---: | ---: | ---: |
| Breakdown accessibility | 2/3 | 1 / 0 | 1 | 25.150s |
| Plan Sections | 4/4 | 0 / 0 | 0 | 29.179s |
| Session Brief | 4/4 | 0 / 0 | 0 | 25.969s |
| Isolation, STA, scheduling and new diagnostic regressions | 26/26 | 0 / 0 | 0 | 34.455s |
| Exact-selection regressions | 23/23 | 0 / 0 | 0 | 12.771s |

Combined disjoint focused parents: **59 passed / 60 total, one failed, zero skipped**. Children are evidence within parents and are not additional totals. Parent wall times include test-command startup, child startup/execution and return, and exclude build/restore. These separate runs do not establish complete-gate runtime, CI margin or the earlier gate's failure cause. Process deadlines remain **90s**, case watchdogs **30s**, supervisor cleanup **10s**; existing STA/scheduling probe deadlines are unchanged. CI's Test budget, exclusive partition ordering, Desktop two / Infrastructure four collection limits and production code are unchanged.

`dispatch.json` verifies the full child/parent name sets, distinct identities, completion timestamps and definition/entry mappings. Breakdown launched exactly one matching child for each of:

- `OwnedFocusValidationAutomationAndRetainedStatesInBothThemes`: **failed**, child `breakdown/breakdown-accessibility-20261021-093409-2191bb31207c400983e3f45b9068079d/`, supervised wall **6.745s**, exit **1**.
- `ScopedTradesEntryDetailReturnMissingRowAndExitUseLiveFocusTargets(inputDuringDelete: False)`: **passed**, child `breakdown/breakdown-accessibility-20261021-093415-1f80ba9c2ede4c42bcb9ec917f3c5fbf/`, **7.262s**, exit **0**.
- `ScopedTradesEntryDetailReturnMissingRowAndExitUseLiveFocusTargets(inputDuringDelete: True)`: **passed**, child `breakdown/breakdown-accessibility-20261021-093423-9abf86f84eaa4acba8c95334c9197c1c/`, **6.528s**, exit **0**.

All three use class prefix `PersonalTradingJournal.Desktop.Tests.Breakdown.BreakdownAccessibilityViewTests.`. Each retained child TRX has exactly **one executed result**, no extra/zero/skipped case. Failed child exit propagates to its parent; it is not reported as a successful single-case validation. Plan's one child (`plan/plan-sections-20261021-093443-684fe8d3de2040669b99887ebb0ce0ce/`) passed all four Light/Dark × 980/96 or 460/240 rows, supervised wall **25.363s**, exit **0**. Brief's one child (`brief/brief-composition-20261021-093553-bb435a155cc042c98385a50f86a0cf05/`) passed all four Light/Dark × 1586×1000/96 or 1000×850/240 rows, **22.151s**, exit **0**. Supervision logs retain normal exits and cleanup; no relevant test host remains active.

The **49** regression cases include the existing 32 isolation/selection/scheduling cases, ten STA-lifetime cases and seven new diagnostics/readiness cases. They exercise actual zero-match/wrong-row VSTest results, malformed/missing/extra/duplicate/skipped/incomplete TRX, expected failure propagation, cancellation/tree cleanup and unchanged scheduling. New cases reject absent/stale command tasks and disconnected input targets, preserve command failures, and verify task-identity/timing records for pending/completed/failed/cancelled operations.

### Remaining failure and measured Plan/Brief phases

The Owned child's earliest failure is **line 179**, `Idle(); Assert.False(moreAction.IsPressed);`, in the first **Light** action-state iteration. Readiness logged at **09:34:15.2317363 UTC**: loaded/visible/active HWND, actual keyboard focus on `MoreFiltersButton`, enabled/focusable target and no modifiers. At **09:34:15.2756654 UTC**, Space had produced exactly **one Click**. The original pressed-state assertion then found **true** after key-up/Idle. This is an ordinary assertion failure, not a watchdog, null-task failure, selection mismatch or demonstrated foreground race. `keyboard-readiness.log`, `stdout.log`, `stderr.log`, `calendar-sta-phases.log`, `process-supervision.log` and the child/parent TRX are retained in the paths above. No hang dump was required/generated for this completed assertion failure.

This run proves command-start/readiness behavior for both scoped rows and Click delivery for the first Owned action. It does **not** establish the remaining Owned Dark, validation, cancellation, date-picker or retained-render assertions: the case stopped before them. The new failure's native mouse-capture/release state was not recorded, so attributing it to capture or foreground competition would be a hypothesis. The smallest next action is a narrowly scoped capture/pressed-state observation around that existing Space key-up, followed by a correction justified by that evidence and a complete Breakdown family check. Keep the pressed-state assertion, actual key input, deadlines and production code intact. No rerun-until-green or additional implementation change followed this failed family.

Measured boundary timings, in seconds (`phase-timings.json` contains exact records and all named task/phase spans):

| Child case | Fixture creation | STA scheduling | STA action | Longest layout/Idle boundary |
| --- | ---: | ---: | ---: | ---: |
| Plan Dark 980/96 | 5.372 | 0.085 | 5.204 | 0.082 |
| Plan Light 980/96 | 0.697 | 0.008 | 3.363 | 0.292 |
| Plan Dark 460/240 | 0.837 | 0.010 | 2.137 | 0.034 |
| Plan Light 460/240 | 0.905 | 0.008 | 2.519 | 0.032 |
| Brief Dark 1000×850/240 | 4.674 | 0.109 | 4.561 | 2.014 |
| Brief Light 1000×850/240 | 1.258 | 0.010 | 2.647 | 0.950 |
| Brief Dark 1586×1000/96 | 0.821 | 0.009 | 2.241 | 0.630 |
| Brief Light 1586×1000/96 | 0.605 | 0.010 | 1.499 | 0.430 |

Fixture durations include synchronous work before the returned task becomes observable. Longest recorded post-creation task waits were **0.421s** for Plan `vm.MarkReadyAsync()` and **0.413s** for Brief `vm.StartDraftAsync()`; all recorded tasks completed successfully. These overlapping phase/task spans are not summed into runtime. The first cases spend substantial time before STA action entry; this supports distinguishing startup/fixture work from UI action time, but does not retroactively explain B's watchdogs. No deadlock, specific pending operation in B, or foreground competition has been established. Persistent Plan/Brief dispatcher shutdown remains owned by the child process and is evidenced by its exit, not a fabricated in-case shutdown timestamp.

### Commands, supporting checks and exact phase inventory

From repository root (D evidence stores `run-focused.ps1`, which preserves the exact dotnet argument array, timestamps, exit and counters in each selection's `summary.json`):

```powershell
dotnet build tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-restore -v minimal -flp:logfile=TestResults/m229d-20261021-093039-46dc5ef268c440d6958173add86c0597/release-build-final.log
$evidence = 'TestResults/m229d-20261021-093039-46dc5ef268c440d6958173add86c0597'
pwsh -NoProfile -File "$evidence/run-focused.ps1" -Family regressions
pwsh -NoProfile -File "$evidence/run-focused.ps1" -Family breakdown
pwsh -NoProfile -File "$evidence/run-focused.ps1" -Family plan
pwsh -NoProfile -File "$evidence/run-focused.ps1" -Family brief
pwsh -NoProfile -File "$evidence/run-focused.ps1" -Family selection
pwsh -NoProfile -File "$evidence/audit.ps1"
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
git diff --check
pwsh -NoProfile -File "$evidence/check-doc-links.ps1"
```

Each focused command uses `dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter <selection> --logger 'trx;LogFileName=parents.trx' --results-directory <unique family path> --diag <family>/vstest.log`. Family filters are `FullyQualifiedName~BreakdownAccessibilityViewTests`, `FullyQualifiedName~PlanSectionsViewTests`, and `FullyQualifiedName~SessionBriefCompositionTests`. Regression filters are `FullyQualifiedName~IsolatedTestProcessTests|FullyQualifiedName~CalendarStaTestTests|FullyQualifiedName~ReleaseSchedulingTests|FullyQualifiedName~UiHarnessDiagnosticsTests`, then separately `FullyQualifiedName~IsolatedTestSelectionTests`. Existing `PTJ_TEST_RESULTS_DIRECTORY` and opt-in `PTJ_CALENDAR_TIMINGS=1` retain diagnostic output; child host flags are set only by the supervisor. Evidence directories are guarded against overwrite/retry.

EF **exit 0**, no pending model changes, `ef-consistency.log`. Final whitespace **exit 0** (all eight tracked/new files checked); documentation **174 local targets / zero missing, exit 0**, recorded in `summary.json`, `whitespace.log` and `documentation-links.json`; URL/fragment validation is outside that check. No passing runtime selection was repeated for documentation edits.

Exact D source inventory: **five modified + three added = eight files**, all uncommitted over the clean starting HEAD. Earlier A/B inventory/evidence is preserved as historical phase scope. Ignored TestResults artifacts/scripts are local evidence outside the source inventory.

| Status | File | Purpose |
| --- | --- | --- |
| Modified | `README.md` | Focused outcome and retained limitation |
| Modified | `docs/notebook.md` | This phase's evidence, commands and inventory |
| Modified | `tests/PersonalTradingJournal.Desktop.Tests/Breakdown/BreakdownAccessibilityViewTests.cs` | Exact children, case-owned Application, keyboard readiness and fresh command tasks |
| Modified | `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/PlanSectionsViewTests.cs` | Phase/task/cleanup breadcrumbs |
| Modified | `tests/PersonalTradingJournal.Desktop.Tests/TradingPlans/SessionBriefCompositionTests.cs` | Phase/task/cleanup breadcrumbs |
| Added | `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/UiKeyboardProbe.cs` | Bounded keyboard prerequisites, Click/fresh-task evidence and failure diagnostics |
| Added | `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/UiCaseTrace.cs` | UTC/monotonic operation/task boundary diagnostics |
| Added | `tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/UiHarnessDiagnosticsTests.cs` | Seven readiness/task-diagnostic regressions |

**Overall M22.9 acceptance remains pending.** No complete gate, production/provider access, Git publication, workflow triggering, deadline increase, assertion removal or production-code change occurred. Human interaction, native journal restart, real clipboard/picker/viewer, installed-codec WebP and matching hosted CI remain unverified. Document/archive v1, export v17 and deferred reclamation/full M22.11 acceptance are unchanged. **M22.10 onward remains pending; this phase stops after M22.9D.**

## M22.9B — Release verification and acceptance

21 October 2026, Europe/Kiev. **M22.9 local automated Release acceptance remains pending; retained failures/findings are recorded below.** Actual verified HEAD: **`d6f891126167921b4d7e145b615820c183da7605`**, `develop`, sole worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. The reported A baseline `9c4ed74` had advanced: all **22 M22.9A files were committed in `d6f8911`** before B began. Starting/gate-entry worktree was clean; no applicable repository/ancestor `AGENTS.md` or duplicate test run was found. One agent, no subagents.

Results belong to the rebuilt source represented by HEAD **plus the exact gate-entry working state and SHA-256 manifest** in `TestResults/m229b-verified/gate-start.json` and `verified-source-settings.json`. The runtime/settings manifest hash is **`AEF8F763187E2A741A846AD6BAA14B8A6F9C887316810B43C1F9BE1FDFBB40DC`**. All recorded A runtime hashes match. B changes only README and this document after runtime verification; no uncommitted implementation is falsely attributed to HEAD. Final source/binary comparisons and worktree inventory are in `summary.json`.

### Complete Release results and timing

Full solution build: **exit 0, zero warnings/errors, 74.50s**, `TestResults/m229b-verified/release-build.log`. The established complete runner executed **once**, with `Workload=ExclusiveWpf` first and its exact complementary parallel partition afterward. Result completeness, duplicate identities, Desktop **two** / Infrastructure **four** collection limits, assertions, child isolation/cleanup and existing deadlines are unchanged. Windows symlink and isolated synthetic DPAPI fixtures used their existing capabilities. No real provider/key access occurred. **No local 600-second cancellation wrapper was added; CI is unchanged.**

| Project | Total | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: | ---: |
| application | 1038 | 1038 | 0 | 0 |
| desktop | 2051 | 2040 | 11 | 0 |
| domain | 602 | 602 | 0 | 0 |
| infrastructure | 1567 | 1567 | 0 | 0 |

Overall: **5258 total / 5247 passed / 11 failed / 0 skipped**, final runner **exit 1**. Desktop totals combine **88 exclusive + 1963 parallel**. Child results are evidence within parents and are not added. Focused A selections (including its overlapping 9 SQLite cases) are reused, not summed with this gate.

| Interval | Wall-clock duration |
| --- | ---: |
| Complete gate, startup and accounting; excludes build/restore | **953.408s** |
| Exclusive WPF partition | **106.124s** |
| Parallel partition, all projects together | **846.402s** |
| Between-partition accounting gap | 0.031s |
| Other wrapper startup/final overhead | 0.851s |
| Signed local margin against CI's 600-second Test-step budget | **-353.408s** |

UTC **2026-10-21T08:19:53.9857844+00:00 → 2026-10-21T08:35:47.3935140+00:00**. Scope matches M22.8F's recorded 589.693s: one outer invocation through both partitions and gate accounting, excluding build/restore, later EF/documentation checks and artifact handling. Partition intervals use the recorded stage start and last post-command accounting timestamp. `StageFinishedUtc` is **not an individual project completion timestamp**, and overlapping project durations are not added. Raw timing components and TRX host windows remain available in `timings.json` / `summary.json`.

The measured gate is **353.408s above 600 seconds**. Its test failures and CI timing risk remain unresolved. A local measurement establishes only its actual local margin; matching hosted timing/acceptance and repeatability remain **unverified**. No workflow was triggered. Historical M22.8 acceptance remains preserved.

Calendar native child: **70.412s**, **49.588s internal headroom** to its unchanged **120-second aggregate deadline**, child exit **0**. `calendar.json` points to the exact supervision start/exit timestamps; no overlapping case durations were summed.

Exact non-passing cases (full diagnostics in `failures.json`):

- `PersonalTradingJournal.Desktop.Tests.TradingPlans.PlanSectionsViewTests.GlobalRemindersAndCollapsibleSectionsPreserveEditingAndExpandForValidation(theme: "Dark", width: 980, dpi: 96)`: **Failed** — System.InvalidOperationException : Isolated test suite 'plan-sections' failed with exit 1. Individual child cases and complete output: C:\Users\Yovko\source\repos\Personal-Trading-Journal\TestResults\release-20261021-081954-3f4d077f52d94b55a957251720289026\parallel\plan-sections-20261021-082731-d3792b6e9895431e8b9fdd55860f0d52.
- `PersonalTradingJournal.Desktop.Tests.Breakdown.BreakdownAccessibilityViewTests.OwnedFocusValidationAutomationAndRetainedStatesInBothThemes`: **Failed** — System.InvalidOperationException : Isolated test suite 'breakdown-accessibility' failed with exit 1. Individual child cases and complete output: C:\Users\Yovko\source\repos\Personal-Trading-Journal\TestResults\release-20261021-081954-3f4d077f52d94b55a957251720289026\parallel\breakdown-accessibility-20261021-082914-3e37dc594eda4ac3803716ac3d1b6cf6.
- `PersonalTradingJournal.Desktop.Tests.TradingPlans.PlanSectionsViewTests.GlobalRemindersAndCollapsibleSectionsPreserveEditingAndExpandForValidation(theme: "Dark", width: 460, dpi: 240)`: **Failed** — System.InvalidOperationException : Isolated test suite 'plan-sections' failed with exit 1. Individual child cases and complete output: C:\Users\Yovko\source\repos\Personal-Trading-Journal\TestResults\release-20261021-081954-3f4d077f52d94b55a957251720289026\parallel\plan-sections-20261021-082731-d3792b6e9895431e8b9fdd55860f0d52.
- `PersonalTradingJournal.Desktop.Tests.TradingPlans.SessionBriefCompositionTests.ProductionShellDraftReadyAndInvalidComposition(width: 1000, height: 850, dpi: 240, theme: "Light")`: **Failed** — System.InvalidOperationException : Isolated test suite 'brief-composition' failed with exit 1. Individual child cases and complete output: C:\Users\Yovko\source\repos\Personal-Trading-Journal\TestResults\release-20261021-081954-3f4d077f52d94b55a957251720289026\parallel\brief-composition-20261021-082827-12bdce0eefe040fd8064529a623200f5.
- `PersonalTradingJournal.Desktop.Tests.TradingPlans.PlanSectionsViewTests.GlobalRemindersAndCollapsibleSectionsPreserveEditingAndExpandForValidation(theme: "Light", width: 980, dpi: 96)`: **Failed** — System.InvalidOperationException : Isolated test suite 'plan-sections' failed with exit 1. Individual child cases and complete output: C:\Users\Yovko\source\repos\Personal-Trading-Journal\TestResults\release-20261021-081954-3f4d077f52d94b55a957251720289026\parallel\plan-sections-20261021-082731-d3792b6e9895431e8b9fdd55860f0d52.
- `PersonalTradingJournal.Desktop.Tests.TradingPlans.SessionBriefCompositionTests.ProductionShellDraftReadyAndInvalidComposition(width: 1000, height: 850, dpi: 240, theme: "Dark")`: **Failed** — System.InvalidOperationException : Isolated test suite 'brief-composition' failed with exit 1. Individual child cases and complete output: C:\Users\Yovko\source\repos\Personal-Trading-Journal\TestResults\release-20261021-081954-3f4d077f52d94b55a957251720289026\parallel\brief-composition-20261021-082827-12bdce0eefe040fd8064529a623200f5.
- `PersonalTradingJournal.Desktop.Tests.TradingPlans.SessionBriefCompositionTests.ProductionShellDraftReadyAndInvalidComposition(width: 1586, height: 1000, dpi: 96, theme: "Light")`: **Failed** — System.InvalidOperationException : Isolated test suite 'brief-composition' failed with exit 1. Individual child cases and complete output: C:\Users\Yovko\source\repos\Personal-Trading-Journal\TestResults\release-20261021-081954-3f4d077f52d94b55a957251720289026\parallel\brief-composition-20261021-082827-12bdce0eefe040fd8064529a623200f5.
- `PersonalTradingJournal.Desktop.Tests.TradingPlans.PlanSectionsViewTests.GlobalRemindersAndCollapsibleSectionsPreserveEditingAndExpandForValidation(theme: "Light", width: 460, dpi: 240)`: **Failed** — System.InvalidOperationException : Isolated test suite 'plan-sections' failed with exit 1. Individual child cases and complete output: C:\Users\Yovko\source\repos\Personal-Trading-Journal\TestResults\release-20261021-081954-3f4d077f52d94b55a957251720289026\parallel\plan-sections-20261021-082731-d3792b6e9895431e8b9fdd55860f0d52.
- `PersonalTradingJournal.Desktop.Tests.TradingPlans.SessionBriefCompositionTests.ProductionShellDraftReadyAndInvalidComposition(width: 1586, height: 1000, dpi: 96, theme: "Dark")`: **Failed** — System.InvalidOperationException : Isolated test suite 'brief-composition' failed with exit 1. Individual child cases and complete output: C:\Users\Yovko\source\repos\Personal-Trading-Journal\TestResults\release-20261021-081954-3f4d077f52d94b55a957251720289026\parallel\brief-composition-20261021-082827-12bdce0eefe040fd8064529a623200f5.
- `PersonalTradingJournal.Desktop.Tests.Breakdown.BreakdownAccessibilityViewTests.ScopedTradesEntryDetailReturnMissingRowAndExitUseLiveFocusTargets(inputDuringDelete: True)`: **Failed** — System.InvalidOperationException : Isolated test suite 'breakdown-accessibility' failed with exit 1. Individual child cases and complete output: C:\Users\Yovko\source\repos\Personal-Trading-Journal\TestResults\release-20261021-081954-3f4d077f52d94b55a957251720289026\parallel\breakdown-accessibility-20261021-082955-02d8d720c32a4ff2b062b70d5877c5fe.
- `PersonalTradingJournal.Desktop.Tests.Breakdown.BreakdownAccessibilityViewTests.ScopedTradesEntryDetailReturnMissingRowAndExitUseLiveFocusTargets(inputDuringDelete: False)`: **Failed** — System.InvalidOperationException : Isolated test suite 'breakdown-accessibility' failed with exit 1. Individual child cases and complete output: C:\Users\Yovko\source\repos\Personal-Trading-Journal\TestResults\release-20261021-081954-3f4d077f52d94b55a957251720289026\parallel\breakdown-accessibility-20261021-082935-ee99135bd18d454fb1984742368bed26.

Retained diagnostic findings: the four `PlanSectionsViewTests.GlobalRemindersAndCollapsibleSectionsPreserveEditingAndExpandForValidation` parents share one existing Lazy child suite. That child hit its **30-second inactivity watchdog**; the sequence identifies **Dark / 980 / 96 DPI** as incomplete. The four `SessionBriefCompositionTests.ProductionShellDraftReadyAndInvalidComposition` parents also share one existing Lazy child; its **30-second inactivity watchdog** aborted with **Dark / 1000 × 850 / 240 DPI** incomplete. The three `BreakdownAccessibilityViewTests` parents report child failures involving keyboard focus assertions, null command execution tasks after synthetic key input (`Pump`, line 302; callers at lines 63/86), and a later **30-second watchdog** abort. Child failures are not additional top-level test totals. `child-failure-diagnostics.json` retains each child TRX/counter/error/stack, supervision and dump paths. Both suites' test files and the directly exercised Breakdown/Trade view files are unchanged by M22.9; this does **not** establish the failure's underlying cause or excuse it. Hang dumps/sequence files are retained; dump-level root-cause analysis and a corrective isolated rerun were not performed in B. No failing assertion/deadline was weakened, no unrelated suite was changed, and no replacement gate was launched.

### Coverage, visuals and supporting checks

`coverage.json` audits actual complete-gate identities for the M22.9 query contracts, SQLite links and Desktop coordination/view cases, plus guarded navigation/return/failure scenarios. Its new-link groups contain **29** cases and navigation contains **6** cases; these are subsets of project totals. The audit also covers existing persistence, images, retained ownership, recovery/autosave and archive/export regressions, including **8 atomic-placement interruption/order/recovery cases**. Audit findings: **none**. Exact-row child evidence confirms **4 distinct link-view children**; existing image-editor dispatch evidence is in `dispatch.json`. All child deadlines, including image **90s process / 30s case**, remain unchanged.

All **eight** final A captures were re-reviewed and SHA-256-checked against both their original isolated-child files and A's archives. UI/runtime source is unchanged from A; no additional dedicated capture run was needed. Reused, hash-verified copies: **`TestResults/m229b-verified/renders/`**. `visuals.json` preserves original A child paths, prior archive paths, copied paths and hashes. Light/Dark × normal **1280/96 DPI** or narrow **480/240 DPI** × linked-items/picker are all represented. Long Trade/Setup labels wrap; inactive/already-linked indicators and separate open/remove controls are readable. Unavailable targets retain their label with disabled opening and reachable removal. The corrected Dark picker surface remains readable. **No blocking visual defect** was found; ordinary vertical scrolling is expected. Incidental captures produced by the required gate remain in its child artifacts.

EF: **No changes have been made to the model since the last migration.**, exit **0**, `TestResults/m229b-verified/ef-consistency.txt`. Final tracked whitespace: **exit 0**. Documentation: **173 local targets / 0 missing**, target existence only (URL/fragment validation excluded). Exact inventory, source/settings/binary hashes and supporting-check findings are recorded in **`TestResults/m229b-verified/summary.json`**. No runtime gate was repeated solely for documentation edits.

Original runner summary: **`TestResults/release-20261021-081954-3f4d077f52d94b55a957251720289026/summary.json`**. Its `exclusive-wpf/` and `parallel/` directories retain top-level TRX, VSTest diagnostics, output and supervised-child evidence. `TestResults/m229b-verified/gate-exit.json` records the final process exit, UTC boundaries and gate-entry source state; `complete-gate-output.log` retains combined output. `gate-start.json` guards against accidental replacement runs.

Commands used:

```powershell
dotnet build PersonalTradingJournal.sln -c Release --no-restore -v minimal -flp:logfile=TestResults/m229b-verified/release-build.log
pwsh -NoProfile -File TestResults/m229b-run-gate.ps1 # times one scripts/Test-Release.ps1 -ResultsDirectory TestResults invocation
pwsh -NoProfile -File TestResults/m229b-measure.ps1
pwsh -NoProfile -File TestResults/m229b-audit-gate.ps1
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
git diff --check
pwsh -NoProfile -File TestResults/m229b-check-doc-links.ps1
pwsh -NoProfile -File TestResults/m229b-summarize.ps1
```

### Link behavior and remaining limitations

Targets remain exactly **Trade** and **Trading Setup**; Playbooks use their Setup identity and separate Strategy support stays deferred. Trades remain eligible with inactive accounts/instruments; inactive Setups remain eligible and labelled. Add rechecks existence with the expected note version in its mutation transaction; remove never deletes or mutates the target/screenshots. Current labels resolve without dirtying/versioning a note. Deleted/missing targets retain their stored reference and unavailable state; they are never silently removed or substituted.

Open uses existing shell save/navigation guards and existing details-by-ID operations without changing ordinary Trade filters. Successful return retains Notebook scope, text/tag filters, sort and selected note under the established list-membership rules. Failed/cancelled saves/navigation retain the editor; failed destinations return with useful feedback, and reactivation does not overwrite retained recovery drafts. Active-only lifecycle restrictions and unreadable/future document protection remain. Document **v1**, archive **v1** and export **v17** are unchanged, including retained image originals/ownership and recovery portability.

Human keyboard/mouse/Narrator interaction, native journal restart and matching-source hosted CI remain explicitly **unverified**. Existing real clipboard/picker/viewer and installed-codec WebP success checks remain unverified. Prepared-file reclamation and full M22.11 integrated acceptance remain deferred. Smallest next action: diagnose the exact retained failing test/check artifacts before any additional runtime run; preserve the established checks and deadlines. No production/provider access, Git publication, workflow triggering, CI timeout/concurrency changes or unrelated-suite optimization occurred. **M22.10 onward remains pending; this phase stops after M22.9B.**

### Exact overall M22.9 inventory

Overall baseline: **`9c4ed749256728a2c2067c5aa983722d56a302cc`** (completed M22.8F). Net M22.9 inventory is **13 modified + 9 added = 22 files**. Phase A (`9c4ed74` → `d6f8911`) committed all 22 implementation/test/documentation paths. Phase B (`d6f8911` → current worktree) modifies **README.md and docs/notebook.md only**, uncommitted, overlapping A's documentation paths; there are no additional runtime/source files in B. Ignored TestResults logs/TRX/measurement scripts are local evidence outside source inventory.

| Net status | File | Changed in phases |
| --- | --- | --- |
| M | `README.md` | A, B |
| M | `docs/notebook.md` | A, B |
| M | `src/PersonalTradingJournal.Application/Notebook/INotebookService.cs` | A |
| A | `src/PersonalTradingJournal.Application/Notebook/NotebookLinkTargets.cs` | A |
| M | `src/PersonalTradingJournal.Application/Notebook/NotebookQueries.cs` | A |
| M | `src/PersonalTradingJournal.Desktop/MainWindow.xaml` | A |
| A | `src/PersonalTradingJournal.Desktop/ViewModels/MainWindowViewModel.Notebook.cs` | A |
| M | `src/PersonalTradingJournal.Desktop/ViewModels/MainWindowViewModel.cs` | A |
| M | `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Autosave.cs` | A |
| M | `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Editing.cs` | A |
| A | `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Links.cs` | A |
| M | `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.cs` | A |
| M | `src/PersonalTradingJournal.Desktop/ViewModels/Setups/TradingSetupsViewModel.cs` | A |
| M | `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml` | A |
| A | `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.Links.cs` | A |
| M | `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.Queries.cs` | A |
| A | `tests/PersonalTradingJournal.Application.Tests/Notebook/NotebookLinkTargetContractTests.cs` | A |
| A | `tests/PersonalTradingJournal.Desktop.Tests/Navigation/NotebookLinkedNavigationTests.cs` | A |
| A | `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookLinkTests.cs` | A |
| A | `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookLinkViewTests.cs` | A |
| M | `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookWorkspaceTests.cs` | A |
| A | `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookLinkTests.cs` | A |

Exact uncommitted B inventory:

```text
README.md
docs/notebook.md
```

## M22.9A — Linked Trades and Trading Setups

21 October 2026, Europe/Kiev. Starting baseline: **clean `develop`, `9c4ed749256728a2c2067c5aa983722d56a302cc`**, sole worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. Existing M22.8F documentation was already committed. Repository/ancestor guidance, this contract, persisted references, readers, shell navigation, save/recovery and image history were inspected first; no applicable `AGENTS.md` or duplicate test host was found. One agent, no subagents. HEAD remains unchanged; the inventory below is uncommitted. **M22.9A functional implementation/focused verification is complete; overall M22.9 acceptance and M22.9B's complete Release gate remain pending.** Historical M22.8 acceptance/timing remains preserved below.

### Selection, identities and target eligibility

The existing `(Trade | TradingSetup, targetId, lastKnownLabel)` metadata, `AddNotebookLink` / `RemoveNotebookLink` commands and immediate SQLite transaction remain authoritative. No separate Playbook/Strategy identity, target creation, schema migration or export-format change was introduced. A Setup's existing details destination is used, including for a Setup with Playbook guidance.

`NotebookTargetQuery` adds only the missing bounded target lookup to `INotebookService`: **25 rows by default, 1–100 permitted**, 256-character search, one lookahead row and a query/type-bound opaque continuation. Stable identity ordering makes every additional match reachable. SQL applies the count/filter/order/page; no whole-catalog or per-row detail loading. Trade projections reuse the existing Trade/account/instrument/browse data and New York time presentation. Labels include instrument, current account, opening time and identity; absent browse time is explicitly unavailable rather than invented. Setup choices use current names and contextual identity/activation. A connection-local deterministic ordinal-ignore-case function supplies Unicode substring search, including account/name/identity search, without schema changes. Queries are parameterized.

Trades remain eligible regardless of account/instrument activation, matching `ITradeListReader`. Existing inactive Setups also remain eligible under the existing target-existence mutation contract and are visibly marked **Inactive**. Existing links are retained through deactivation/deletion. Picker rows visibly report **Already linked**; duplicates use kind **and** ID, so equal GUIDs of different kinds remain distinct. Search/type changes cancel the previous owner; generation, note identity, lifetime and disposal checks suppress stale completions. More results, retry, loading and useful empty/error states remain reachable.

### Writes, resolution and navigation

Link actions capture the intended note and typed target before awaiting. The existing note-operation guard drains in-flight autosave and saves dirty content (or honors explicit Save/Discard/Keep editing consent) before using the current acknowledged expected version. Newer typing during a save is retained and flushed before metadata mutation. Failure/conflict/cancellation does not replace the editor or adopt an unrelated version. Metadata acknowledgements verify identity, one version advance, unchanged title/document/images and exact expected links. They never call `InstallDraft`; WPF document, caret, image references and semantic undo/redo survive. Departure is blocked while a link change owns preparation/mutation.

Add validates target existence inside the same immediate SQLite transaction as link insertion and note-version advancement. A deleted selection returns `UnavailableTarget`; a stale version returns `Conflict`. Remove affects only the reference and can remove a missing target's link. Neither operation changes Trade economics/executions/classification, Setup definitions, Trade screenshots/ownership, original files or the image ownership ledger. Metadata writes do not re-encode document JSON. Active-only lifecycle rules remain; unreadable/future documents return `UnsupportedContent` untouched and offer no editable substitute.

Linked items now show current labels/context with separate **Open details** and **Remove link** actions. Bounded batch resolution refreshes labels without dirtying/versioning the note. Missing targets retain their stored label and explicit **Unavailable** state; open is disabled while removal remains available on an editable note. Failed resolution retains the reference and a retry message. No same-name substitution or silent link removal occurs.

Open re-resolves the captured reference, then uses existing shell save/navigation guards and the existing Trade-by-ID or Setup details use case. Trade filters are not changed and no synthetic browse row is created. A missing/failed destination returns to Notebook with the retained draft/reference and useful feedback; discard consent is applied only after destination success. **Back to Notebook** uses the existing destination navigation, retaining scope, text/tag filters, sort and selected note under existing list membership rules. Re-activation refreshes current data/labels; retained dirty recovery content is not overwritten by canonical content. No parallel history framework was added. Late resolution/mutation acknowledgements and disposed activations cannot publish into another note.

### Focused Release evidence

Final solution Release build: **exit 0, zero warnings/errors**, `TestResults/m229a-verified/release-build-verified.log` (10.84s). Full-set ordering, collection limits, deadlines and helper contracts are unchanged. The complete gate was **not** launched.

| Focused selection | Passed / total | Failed / skipped | Exit | Evidence under TestResults/m229a-verified/ |
| --- | ---: | ---: | ---: | --- |
| Notebook, shell/header/navigation, Trade and Setup Desktop regressions | 551 / 551 | 0 / 0 | 0 | `desktop-verified/desktop-verified.trx` |
| Notebook, storage, archive/export, migration/integrity and related ownership persistence | 530 / 530 | 0 / 0 | 0 | `infrastructure-focused.trx` |
| Latest SQLite link tests after strengthening existing-screenshot assertions | 9 / 9 | 0 / 0 | 0 | `infrastructure-links-verified.trx` |
| Notebook application contracts | 17 / 17 | 0 / 0 | 0 | `application.trx` |

The 9 SQLite link cases are included in the broader persistence selection; these reruns are **not added** to its total. Only three test files changed after the broader persistence/runtime source was built: late-test list setup, explicit redo verification and stronger existing-screenshot assertions. Production source hashes remain identical; final binaries/source hashes and separate exit records are retained in `summary.json`. Final Desktop execution uses all corrected tests. Real SQLite tests use independent contexts/services for fresh reads, cancellation, duplicate typed identities, deletion races, conflict winners, renamed/inactive/unavailable targets, exact JSON/original bytes and retained recovery. Desktop tests cover latest picker ownership, paging, autosave/newer edits, failure preservation, disposal/activation, image undo/redo, successful detail opening/return context and failed/cancelled navigation.

Four new theory parents each run one exact isolated child, preserving **90-second process / 30-second case** deadlines and cleanup. All four completed once with matching identities. **Eight final captures** were inspected: Light/Dark × 1280/96 DPI or 480/240 DPI × linked-items/picker. Archived copies: `TestResults/m229a-verified/renders/`; `visuals.json` records original child paths and SHA-256 provenance. Long Trade/Setup labels wrap; inactive/already-linked/unavailable states and separate open/remove actions remain readable and horizontally contained. The first Dark picker capture exposed a white default ListBox surface with light text; explicit themed surface/text/border resources corrected it. Final renders show no blocking visual defect. Normal scrolling is required for the editor and long linked-item collections.

Retained intermediate diagnostics: the first build reported two unassigned fake-hook errors before new tests used the hooks. `infrastructure-initial.trx` recorded **111 passed / 2 failed**, both an EF projection translation failure in Setup search; explicit projection members and parameterized Unicode search corrected it. `desktop-initial/desktop-initial.trx` passed **20/20**. `desktop-focused/desktop-focused.trx` recorded **548 passed / 3 failed**: one new test added its second note after list loading (fixture timing corrected, assertion preserved), and both existing `DailyReviewAcceptanceCompiledShellKeepsCapturedEvidenceOrCancelsOnSourceNotification` rows could not read their synthetic DPAPI key inside the sandbox. The same final Desktop selection passed using the existing isolated synthetic DPAPI capability, with no real key/provider access. Initial logs/TRX/renders remain; assertions/deadlines were not weakened.

EF: **no pending model changes, exit 0**, `ef-consistency.txt`. Whitespace checks cover tracked **and new** source files. Final local Markdown target check: **172 checked / zero missing** (target existence; URL/fragment validation excluded). Exact commands, TRX counts/times, source/binary/inventory checks and visual provenance: **`TestResults/m229a-verified/summary.json`**.

Commands used (the long persistence filter is preserved verbatim in `infrastructure-exit.json`; Desktop filter in `desktop-verified-exit.json`):

```powershell
dotnet build PersonalTradingJournal.sln -c Release --no-restore -v minimal -flp:logfile=TestResults/m229a-verified/release-build-verified.log
$desktopFilter = 'FullyQualifiedName~Notebook|FullyQualifiedName~Navigation.MainWindowViewModelTests|FullyQualifiedName~MainWindowHeaderTests|FullyQualifiedName~TradingSetupsViewModelTests|FullyQualifiedName~TradesViewModelTests'
$env:PTJ_TEST_RESULTS_DIRECTORY = "$PWD/TestResults/m229a-verified/desktop-verified"
dotnet test tests/PersonalTradingJournal.Desktop.Tests -c Release --no-build --no-restore --filter $desktopFilter --logger 'trx;LogFileName=desktop-verified.trx' --results-directory TestResults/m229a-verified/desktop-verified --blame-hang --blame-hang-timeout 3m
$persistenceFilter = (Get-Content TestResults/m229a-verified/infrastructure-exit.json -Raw | ConvertFrom-Json).Filter
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests -c Release --no-build --no-restore --filter $persistenceFilter --logger 'trx;LogFileName=infrastructure-focused.trx' --results-directory TestResults/m229a-verified --blame-hang --blame-hang-timeout 3m
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests -c Release --no-build --no-restore --filter 'FullyQualifiedName~NotebookLinkTests' --logger 'trx;LogFileName=infrastructure-links-verified.trx' --results-directory TestResults/m229a-verified --blame-hang --blame-hang-timeout 3m
dotnet test tests/PersonalTradingJournal.Application.Tests -c Release --no-build --no-restore --filter 'FullyQualifiedName~Notebook' --logger 'trx;LogFileName=application.trx' --results-directory TestResults/m229a-verified --blame-hang --blame-hang-timeout 3m
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
git diff --check
# New files: git diff --no-index --check -- NUL <each new source file>
pwsh -NoProfile -File TestResults/m229a-check-doc-links.ps1
pwsh -NoProfile -File TestResults/m229a-summarize.ps1
```

### Exact M22.9A inventory and remaining acceptance

**13 modified + 9 added files** relative to `9c4ed749256728a2c2067c5aa983722d56a302cc`. No unrelated files changed. Ignored TestResults scripts/artifacts are local verification evidence outside source inventory.

```text
M docs/notebook.md
M README.md
M src/PersonalTradingJournal.Application/Notebook/INotebookService.cs
A src/PersonalTradingJournal.Application/Notebook/NotebookLinkTargets.cs
M src/PersonalTradingJournal.Application/Notebook/NotebookQueries.cs
M src/PersonalTradingJournal.Desktop/MainWindow.xaml
M src/PersonalTradingJournal.Desktop/ViewModels/MainWindowViewModel.cs
A src/PersonalTradingJournal.Desktop/ViewModels/MainWindowViewModel.Notebook.cs
M src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Autosave.cs
M src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.cs
M src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Editing.cs
A src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Links.cs
M src/PersonalTradingJournal.Desktop/ViewModels/Setups/TradingSetupsViewModel.cs
M src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml
A src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.Links.cs
M src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.Queries.cs
A tests/PersonalTradingJournal.Application.Tests/Notebook/NotebookLinkTargetContractTests.cs
A tests/PersonalTradingJournal.Desktop.Tests/Navigation/NotebookLinkedNavigationTests.cs
A tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookLinkTests.cs
A tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookLinkViewTests.cs
M tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookWorkspaceTests.cs
A tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookLinkTests.cs
```

Canonical document **v1**, complete archive **v1** and logical export **v17** remain unchanged, including unavailable links, current/retained image metadata and recovery preservation. Prepared-file retention/reclamation and full M22.11 acceptance remain deferred. No schema migration, file reclamation, target/economics mutation, CI timeout/concurrency change, production/provider access, workflow triggering or Git publication occurred.

**Remaining:** M22.9B complete Release acceptance (including whole-gate timing/Calendar headroom); human keyboard/mouse/Narrator interaction, native journal restart and matching-source CI are unverified. Earlier real clipboard/picker/viewer and installed-codec WebP limitations remain unverified. Separate Strategy support is deferred. This phase stops before M22.9B; M22.10 onward remains pending.

## M22.8F — Complete gate measurement after deduplication

21 October 2026, Europe/Kiev. Actual verified baseline and unchanged HEAD: **clean `develop`, `7cb4c7af6b4c325c1134f049a6403668ac82bf3f`**, sole existing worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. M22.8E is already committed here. Repository/ancestor guidance, the current gate/workflow, E's focused source hashes and historical C artifacts were inspected first. No applicable `AGENTS.md`, duplicate active gate or test host was found. One agent, no subagents. No implementation, production source, assertion, deadline, partition or concurrency setting changed in F.

### Complete Release results and measurement scope

**The changed M22.8E harness passed the complete local Release gate.** The full solution rebuilt before testing: **exit 0**, **zero warnings/errors**, **32.56s**, log `TestResults/m228f-verified/release-build.log`. Rebuilt test/dependency hashes are in `verified-binaries.json`; the Desktop Infrastructure copy matches the current Infrastructure output. E's focused source hashes still match; focused suites were not repeated.

The established `scripts/Test-Release.ps1` ran **once**, retaining its `Workload=ExclusiveWpf` then `Workload!=ExclusiveWpf` partitions, complete-result/duplicate-identity guards, Desktop **two** / Infrastructure **four** collection limits, child isolation/cleanup, **90-second image-child / 30-second case** limits and existing VSTest diagnostics. Required Windows symlink/isolated synthetic DPAPI fixtures used their existing capabilities. The workflow's Test-step timeout stays **600 seconds**. **No local 600-second cancellation wrapper was introduced.**

| Project | Total | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: | ---: |
| application | 1034 | 1034 | 0 | 0 |
| desktop | 2029 | 2029 | 0 | 0 |
| domain | 602 | 602 | 0 | 0 |
| infrastructure | 1558 | 1558 | 0 | 0 |

Overall **5223 total / 5223 passed / 0 failed / 0 skipped**, observed complete-runner **exit 0**. Desktop comprises **88 exclusive + 1941 parallel**. The 23 additional top-level cases relative to historical C's 5,200 are E's selection/validation regressions; all four original image-editor parents remain. Supervised child cases are evidence within their parents and are not added to totals.

| Measured interval | Wall-clock duration |
| --- | ---: |
| Complete gate, including startup and accounting | **589.693s** |
| Exclusive WPF partition | **86.782s** |
| Parallel partition, all projects together | **502.306s** |
| Between-partition accounting gap | 0.030s |
| Other wrapper startup/final overhead | 0.575s |
| Local margin against 600 seconds | **10.307s** |

Scope matches historical C's **624.851s**: outer invocation of the same runner through both partitions and gate accounting, **excluding build/restore, EF, documentation checks and later artifact handling**. UTC 2026-10-20T21:32:49.8824800+00:00 through 2026-10-20T21:42:39.5757060+00:00. Partition intervals use each stage's recorded start and the last accounting timestamp for that stage. Project `StageFinishedUtc` values are written after the shared command completes; they are **not individual project completion times**. Parallel project durations are not added. `timings.json` retains the raw definitions, accounting components and signed comparison to the historical run.

This passing local run is **10.307s below 600 seconds**. That establishes only the measured local margin, not hosted CI headroom or repeatability. Historical C is preserved as **5,200 passed / exit 0 / 624.851s**. This is one actual new whole-gate observation, with additional regression cases and potentially different scheduling/load; it is not a repeatability guarantee or attribution of every timing difference to deduplication. **Matching hosted timing remains unverified.** No workflow was triggered.

Calendar native: **70.927s**, **49.073s internal headroom** against its unchanged **120-second aggregate deadline**, child exit **0**. The measurement comes from that child's start/exit supervision timestamps, not summed overlapping case durations. `calendar.json` points to the precise supervision artifact.

### Image dispatch and supporting checks

The complete-gate TRX audit confirms **4 image-editor parents / 4 matching child results**, with **4 distinct test IDs**, each selected once. Every matching child TRX has one total/executed/passed case, no failed/skipped cases, and agrees with its parent display name and test ID. `dispatch.json` records all four theory identities, child artifact paths and validation findings. The helper's opt-in completeness/definition/execution-entry checks remain active in the actual parents. Its zero-match/wrong-row negative-control child processes have separate artifact prefixes and are not counted as successful image-parent children. All **32 helper/scheduling regressions passed** in the complete run.

| Parent/child display name | Child outcome | Matching test ID |
| --- | --- | --- |
| `PersonalTradingJournal.Desktop.Tests.Notebook.NotebookImageEditorTests.ImageRoundTripUndoPlacementPasteAndRenders(theme: "Dark", width: 480, dpi: 240)` | Passed | `44dcb1d9-9e81-cbe3-0d08-9237a1d68736` |
| `PersonalTradingJournal.Desktop.Tests.Notebook.NotebookImageEditorTests.ImageRoundTripUndoPlacementPasteAndRenders(theme: "Light", width: 1280, dpi: 96)` | Passed | `37c3e225-29f4-7e0f-b396-f7e25e55ffb6` |
| `PersonalTradingJournal.Desktop.Tests.Notebook.NotebookImageEditorTests.ImageRoundTripUndoPlacementPasteAndRenders(theme: "Dark", width: 1280, dpi: 96)` | Passed | `d9ad7ac6-561d-c4e6-eb55-ea5f68588b5a` |
| `PersonalTradingJournal.Desktop.Tests.Notebook.NotebookImageEditorTests.ImageRoundTripUndoPlacementPasteAndRenders(theme: "Light", width: 480, dpi: 240)` | Passed | `a9a357f9-e426-8251-d39c-9f92aa569832` |

Image parent-suite wall interval in this complete run: **37.106s**, including child startup; this interval overlaps other parallel work and is not added to gate time. `coverage.json` retains actual top-level image, atomic-placement, ownership, recovery and archive/export regression coverage, including the **eight B1 interruption/order/recovery cases**.

EF: **No changes have been made to the model since the last migration.**, observed **exit 0**, `TestResults/m228f-verified/ef-consistency.txt`. Final tracked whitespace and **171 local Markdown targets / 0 missing** are recorded with inventory/source checks in `summary.json`. URL/fragment validation is outside the documentation check. The final F inventory is exactly the two documentation files below. No runtime gate was repeated solely for documentation changes.

The runner's unchanged summary is **`TestResults/release-20261020-213250-debed1eb87654b3799d6a0d35ff548a4/summary.json`**. Raw partition output/TRX, VSTest diagnostics, child logs and supervision are underneath that run directory. **`TestResults/m228f-verified/summary.json`** combines the actual exit/timestamps, partition/project counts, local budget comparison, Calendar and dispatch evidence, source/binary/settings hashes and supporting checks. `gate-start.json` guards against an accidental replacement invocation; `gate-exit.json` records the actual final exit; `complete-gate-output.log` retains output. The wrapper records the same scope as C and does not impose a new timeout.

Commands used:

```powershell
dotnet build PersonalTradingJournal.sln -c Release --no-restore -v minimal -flp:logfile=TestResults/m228f-verified/release-build.log
pwsh -NoProfile -File TestResults/m228f-run-gate.ps1 # times one invocation of scripts/Test-Release.ps1 -ResultsDirectory TestResults
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
git diff --check
pwsh -NoProfile -File TestResults/m228f-check-doc-links.ps1
pwsh -NoProfile -File TestResults/m228f-audit-gate.ps1
pwsh -NoProfile -File TestResults/m228f-measure.ps1
pwsh -NoProfile -File TestResults/m228f-summarize.ps1
```

Smallest next action: inspect the next available matching hosted run and its Test-step timing read-only; this measurement does not justify changing timeouts or more suites. Existing Light/Dark visual evidence and production behavior are unchanged. Human keyboard/mouse/Narrator, real clipboard/picker/viewer, native journal restart, successful installed-codec WebP admission and matching-source CI remain explicitly unverified. Canonical document/archive **v1**, export **v17**, prepared-file retention and deferred reclamation/full M22.11 acceptance are unchanged. No Git publication, workflow triggering, production/provider access or M22.9 work occurred.

### Exact M22.8F phase inventory

Relative to clean baseline `7cb4c7af6b4c325c1134f049a6403668ac82bf3f`: **2 modified documentation files, no added source files**, uncommitted. E's six-file dispatch/helper/documentation change is already committed in this baseline; C/E evidence remains preserved. Ignored TestResults logs/TRX/measurement scripts are local evidence outside source inventory.

```text
README.md
docs/notebook.md
```

## M22.8E — Image-editor test dispatch deduplication

21 October 2026, Europe/Kiev. Actual starting and unchanged HEAD: **clean `develop`, `2c6b73a483470181394ac9317f5aa503b3815070`**, sole worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. The C documentation was already committed. Repository/ancestor guidance, current suite/helper, discovery/child TRX conventions and existing timing evidence were inspected first; no applicable `AGENTS.md` or active duplicate test run was found. One agent, no subagents. This phase changes only the image-editor dispatch, additive test-helper validation/regressions and documentation.

### Exact theory-row dispatch and result proof

Previously every one of the four `NotebookImageEditorTests` theory parents called `RunSuiteAsync` with its default class-wide filter. The historical C artifacts contain four child processes, each running all four theory rows: **16 executions of four distinct child cases**. The top-level partitions were already disjoint; there was no duplicate restore/build or second complete gate to remove.

Each parent now constructs the exact fully qualified xUnit display name, including named theme/width/DPI arguments, using invariant formatting. `ExactDisplayNameFilter` uses the installed TestPlatform's public `FilterHelper.Escape` and the **`DisplayName=`** equality operator. A class/method-only selector would still match four rows. Parent discovery is preserved verbatim in `TestResults/m228e-verified/discovery.txt`; actual execution proves that the installed xUnit adapter distinguishes each selected display name.

The helper's new optional `expectedTestName` argument enables single-case validation only for opted-in callers. After the existing nonzero-exit failure propagation, a successful child must supply exactly one root TRX file, a Completed run, exact one-executed/one-passed counters with no skipped/failed/pending outcomes, and exactly one passed result whose full display name matches the intended case. Its definition and execution entry must agree with that result's nonempty identities; completion timestamps must be valid. Missing/empty/malformed/extra reports, extra/duplicate results, skipped/failed outcomes and mismatched identities are rejected. Zero-match VSTest success cannot pass the parent. Diagnostics/TRX remain in the existing per-process artifact directories, including when validation fails.

All **four discoverable parents**, their full assertions and host-variable recursion protection remain. Process isolation, the **90-second aggregate process deadline**, **30-second case-hang watchdog**, process-tree cleanup, output/error propagation and diagnostic artifacts are unchanged. Existing whole-suite helper callers keep their previous contract unless they explicitly opt into validation. The other three Notebook suites are not optimized here. Release partition ordering, full-set/duplicate-identity guards, CI's **600-second Test-step timeout**, Desktop **two** / Infrastructure **four** collection limits and all production code remain unchanged.

### Focused Release verification and timing

Affected Desktop/test Release build passed, **exit 0**, **zero warnings/errors**, **53.85s**, log `TestResults/m228e-verified/release-build.log`. Discovery exited **0** and listed the same four parents. Image and helper measurements ran separately; helper negative-control children are not part of the measured four-parent image run.

| Focused run | Passed / total | Failed / skipped | Process exit | Evidence |
| --- | ---: | ---: | ---: | --- |
| Image-editor theory parents | 4 / 4 | 0 / 0 | 0 | `image/parents.trx` |
| Helper, selection and Release scheduling regressions | 32 / 32 | 0 / 0 | 0 | `helpers/helpers.trx` |

All paths in this section are under **`TestResults/m228e-verified/`**, unless given in full. `dispatch.json` verifies one child per parent, exact name and test-ID agreement, and **four distinct child names/IDs**. Each child result is Passed with no additional/skipped cases. Common method: `PersonalTradingJournal.Desktop.Tests.Notebook.NotebookImageEditorTests.ImageRoundTripUndoPlacementPasteAndRenders`.

| Theme | Width / DPI | Parent count | Child passed / total | Matching parent/child test ID |
| --- | --- | ---: | ---: | --- |
| Light | 1280 / 96 | 1 | 1 / 1 | `37c3e225-29f4-7e0f-b396-f7e25e55ffb6` |
| Dark | 1280 / 96 | 1 | 1 / 1 | `d9ad7ac6-561d-c4e6-eb55-ea5f68588b5a` |
| Light | 480 / 240 | 1 | 1 / 1 | `a9a357f9-e426-8251-d39c-9f92aa569832` |
| Dark | 480 / 240 | 1 | 1 / 1 | `44dcb1d9-9e81-cbe3-0d08-9237a1d68736` |

The **parent-suite wall interval is 27.964s**, measured from the first parent's start to the last parent's completion; this includes child startup and supervision. Full focused-command elapsed time, including outer VSTest startup/discovery, is **32.578s**, recorded with process timestamps/Stopwatch in `image-exit.json`. The console reports 27s of image test duration. The helper run reports 22s; its 32 cases comprise **23 new selection/validation regressions**, **8 existing process-supervision regressions**, and **1 existing scheduling regression**. Real negative-control subprocesses prove that a zero-match exit 0 and a passed but different theory row both fail opt-in selection validation. Existing timeout/cancellation/tree termination, diagnostic-write/output-pump failures and nonzero synthetic exits still pass their unchanged assertions.

`baseline.json` retains the C complete-gate parent/child identities and **52.082s** parent interval from `TestResults/release-20261020-205728-e45bbd22cd194a1884bf16a73a64ded6/parallel/`. That baseline included competing full-gate workloads; the new focused run did not. The cardinality correction **16 → 4** is verified, but these timings are not a controlled A/B estimate or proof of a whole-gate saving. Historical M22.8C acceptance remains **5,200 passed / zero failures or skips / exit 0**, with **624.851s** gate duration. **No complete gate was launched in E; full-load timing, compliance with the 600-second CI step and matching hosted CI remain pending.**

`summary.json` records baseline/HEAD, exact inventory/source and binary hashes, actual commands/exits, child identities, timing definitions and unchanged settings. Final tracked/added whitespace checks and **170 local Markdown targets / zero missing** pass; URL/fragment validation is outside that documentation check. No runtime rerun is required solely for final documentation changes.

Commands used:

```powershell
dotnet build tests/PersonalTradingJournal.Desktop.Tests -c Release --no-restore -v minimal -flp:logfile=TestResults/m228e-verified/release-build.log
dotnet test tests/PersonalTradingJournal.Desktop.Tests -c Release --no-build --no-restore --list-tests --filter 'FullyQualifiedName~NotebookImageEditorTests'
dotnet test tests/PersonalTradingJournal.Desktop.Tests -c Release --no-build --no-restore --filter 'FullyQualifiedName~NotebookImageEditorTests' --logger 'trx;LogFileName=parents.trx' --results-directory TestResults/m228e-verified/image --blame-hang-timeout 3m -v minimal
dotnet test tests/PersonalTradingJournal.Desktop.Tests -c Release --no-build --no-restore --filter 'FullyQualifiedName~IsolatedTestProcessTests|FullyQualifiedName~IsolatedTestSelectionTests|FullyQualifiedName~ReleaseSchedulingTests' --logger 'trx;LogFileName=helpers.trx' --results-directory TestResults/m228e-verified/helpers --blame-hang-timeout 3m -v minimal
git diff --check
pwsh -NoProfile -File TestResults/m228e-check-doc-links.ps1
pwsh -NoProfile -File TestResults/m228e-summarize.ps1
```

The image/helper wrapper scripts set `PTJ_TEST_RESULTS_DIRECTORY` to their separate evidence subdirectories and capture observed process exits and wall time. No production/provider access, workflow triggering, Git publication or M22.9 work occurred. Existing interactive/native-journal-restart/codec/CI limitations remain; storage/archive compatibility and deferred reclamation/M22.11 acceptance are unchanged.

### Exact M22.8E changed-file inventory

Relative to clean baseline `2c6b73a483470181394ac9317f5aa503b3815070`: **4 modified + 2 added files**, uncommitted. Historical acceptance artifacts are preserved; ignored TestResults scripts/logs/TRX are evidence outside the source inventory.

Modified:

```text
README.md
docs/notebook.md
tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookImageEditorTests.cs
tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/IsolatedTestProcess.cs
```

Added:

```text
tests/PersonalTradingJournal.Desktop.Tests/Calendar/IsolatedTestSelectionTests.cs
tests/PersonalTradingJournal.Desktop.Tests/TestDoubles/IsolatedTestProcess.Selection.cs
```

## M22.8C — Automated Release verification and acceptance

20 October 2026 UTC. Verified source: **clean `develop`, `a86b0c02a728006391fa5c939d8249415d2084d1`**, sole worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. This HEAD already contains A, B and B1. Repository/ancestor guidance, the Notebook contract, durable focused summaries and current Git/process state were inspected first; no applicable `AGENTS.md` or duplicate active test run was found. One agent, no subagents. C changes **only README.md and docs/notebook.md**; implementation, assertions, deadlines, concurrency and unrelated files are preserved.

### Complete gate and supporting checks

**M22.8 local automated Release acceptance is complete.** The full solution was rebuilt from this source before the established `scripts/Test-Release.ps1` gate. Build: **zero warnings/errors**, exit **0**, **62.04 s**, log `TestResults/m228c-verified/release-build.log`. The runner used its unchanged complementary `Workload=ExclusiveWpf` and `Workload!=ExclusiveWpf` partitions, Desktop two-collection / Infrastructure four-collection limits, original child deadlines and three-minute VSTest inactivity diagnostics. Required Windows symlink and isolated synthetic DPAPI fixtures ran with their existing capabilities. No production/provider access occurred.

Complete gate: **5200 / 5200 passed**, zero failures/skips, final **exit 0**. Exclusive WPF **88/88**; parallel project totals follow. Isolated child TRX files overlap the top-level cases and are not added again.

| Project | Passed / total | Partition |
| --- | ---: | --- |
| Domain | 602 / 602 | Parallel |
| Application | 1034 / 1034 | Parallel |
| Infrastructure | 1558 / 1558 | Parallel |
| Desktop | 2006 / 2006 | 88 exclusive + 1918 parallel |

UTC **2026-10-20T20:57:27.4035688+00:00–2026-10-20T21:07:52.2543115+00:00**, **624.851 s** elapsed (21 October locally at completion). Calendar native child: **73.332 s**, **46.668 s headroom** against its unchanged **120 s** aggregate deadline, exit **0**. The CI Test step remains configured for ten minutes; this local measurement exceeds that duration by **24.851 s** and is not a hosted-run guarantee or matching-source CI result.

The unchanged complete-gate summary is `TestResults/release-20261020-205728-e45bbd22cd194a1884bf16a73a64ded6/summary.json`, with partition output/TRX, VSTest diagnostics, child supervision and timing artifacts beneath that directory. **`TestResults/m228c-verified/summary.json`** ties together source/binary/settings hashes, observed gate exit/timestamps, coverage, visual provenance, phase inventory and supporting checks. `gate-exit.json` retains the actual process exit; `complete-gate-output.log` retains its output. Calendar timing comes from `exclusive-wpf/calendar-native-20261020-205733-ae14d5c36a564184b7dfb9e207d5a765/process-supervision.log`, not summed overlapping test durations.

`coverage.json` audits actual passed top-level identities for image admission/persistence, final atomic placement, ordered reference reconciliation, retained ownership, autosave/recovery and Notebook archive/export/restore. All **eight B1 interruption/order/recovery cases** are present in the complete gate. The prior focused **111 Desktop / 521 Infrastructure / 13 Application** results are reused from `TestResults/m228b1-verified/summary.json`; their source hashes match the current implementation. No focused suite was repeated without a failure, source change or uncovered requirement.

EF reports **no pending model changes**, exit **0**, evidence `TestResults/m228c-verified/ef-consistency.txt`. Final repository-configured whitespace and **169 local documentation targets / zero missing** pass. Documentation checks cover local Markdown target existence, not external URLs or fragments. `inventory-check.json` compares the documented overall inventory with Git and confirms C's two documentation changes. No runtime code changed after the passing gate; it was not repeated for documentation edits.

Commands actually used:

```powershell
dotnet build PersonalTradingJournal.sln -c Release --no-restore -v minimal -flp:logfile=TestResults/m228c-verified/release-build.log
pwsh -NoProfile -File scripts/Test-Release.ps1 -ResultsDirectory TestResults
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
git diff --check
pwsh -NoProfile -File TestResults/m228c-check-doc-links.ps1
pwsh -NoProfile -File TestResults/m228c-summarize.ps1
```

The local `TestResults/m228c-run-gate.ps1` wrapper invokes that exact runner, captures its output and persists the process exit/timestamps without changing gate settings. Ignored evidence/checker files are not source changes.

### Final image evidence and behavior

All **12** Light/Dark normal/narrow-high-DPI captures were inspected in C. Reused copies are under **`TestResults/m228c-verified/visuals/`**, filename matrix `{Light|Dark}-{1280-96|480-240}-{inline-caption-missing|missing-and-actions|codec-error}.png`. Normal: 1280×1000 pixels / 96 DPI. Narrow: 1200×2125 pixels for 480×850 DIP / 240 DPI. They originate from B1's final harness run, `TestResults/m228b1-verified/renders/notebook-image-renders/`; `visual-provenance.json` verifies each hash against that phase's summary, and the relevant view/presenter sources remain unchanged. No missing/affected visual coverage required a new render run.

Inline aspect ratio and border/caption spacing remain correct; long captions and codec feedback wrap, unavailable images retain readable placeholders, and expanded caption/alternative text/order/removal controls remain reachable and readable in both themes. Dark image-action contrast remains corrected. Narrow captures use the established inner/outer scrolling; scrolled captures intentionally clip earlier content above the viewport. **No blocking visual defect was found.** These are synthetic workspace captures, not human/live-shell interaction acceptance.

B1's final candidate is captured after pending save/dirty-draft coordination. The original is admitted/staged outside the canonical transaction; final intended document position, selection/paragraph changes, metadata/projection, ownership ledger and one expected-version advance commit together. There is no intermediate canonical tail placement and no background move after restart. Fresh readers before commit see the previous note; after commit, even before client acknowledgement, they see the final position and matching ownership. Newer editor changes remain a protected dirty draft, stale writes/recovery cannot replace newer persisted content, and undo/redo/recovery retain original identities. Fresh-context interruption verification is not a claimed native journal process restart.

### Storage compatibility and remaining limits

Canonical document **JSON v1**, complete archive **v1**, and logical export **v17** remain unchanged in B/B1/C. A introduced the additive `20261020180223_AddNotebookOwnedImageFiles` migration (28 current migrations) and export 16→17. Complete archives include current and retained Notebook-owned originals with exact size/hash verification; missing/tampered required files fail explicitly. Logical metadata export is not an image-byte restore input. Older-schema archives remain rejected by exact-schema preflight; no archive upgrade mechanism is introduced.

Prepared originals can remain after abrupt death between filesystem publication and canonical commit. Committed originals and ownership ledger entries remain intentionally retained through reference removal, undo/redo and recovery. Uncertain commit/cleanup outcomes preserve bytes. **File reclamation, duplication/permanent-note deletion and full integrated M22.11 acceptance remain deferred.** No production files were reclaimed.

Human keyboard/mouse/Narrator interaction, real OS clipboard/native picker/full-original viewer interaction, actual native journal restart, successful installed-codec WebP admission and matching-source CI remain **explicitly unverified**. The local automated gate and synthetic visuals do not establish those checks. M22.9 onward remains pending; no provider access, Git publication or later milestone work occurred.

### Exact overall M22.8 changed-file inventory

Overall baseline: `e0682d3a898742ff8612857de5a2245267eae041` (completed M22.7). Net through verified HEAD plus C documentation: **59 files — 39 modified + 20 added**. Status is relative to that overall baseline; phases identify each later touch. A/B/B1 are already committed at this verification baseline, while C leaves only README.md and docs/notebook.md modified. Counts below overlap and must not be summed as unique files.

| Phase | From → through | Phase delta |
| --- | --- | --- |
| A — storage/application | `e0682d3` → `80eada6` | 28 modified + 11 added = 39 |
| B — Desktop integration | `80eada6` → `aa75cdc` | 19 modified + 8 added = 27 |
| B1 — atomic placement | `aa75cdc` → `a86b0c0` | 14 modified + 1 added = 15 |
| C — verification/documentation | `a86b0c0` → current worktree | 2 modified, no added source files |

| Net status | File | Changed in phases |
| --- | --- | --- |
| M | `README.md` | A, B, B1, C |
| M | `docs/notebook.md` | A, B, B1, C |
| M | `src/PersonalTradingJournal.Application/Backups/IPortableExportService.cs` | A |
| A | `src/PersonalTradingJournal.Application/Notebook/INotebookImages.cs` | A, B1 |
| M | `src/PersonalTradingJournal.Application/Notebook/INotebookService.cs` | A |
| M | `src/PersonalTradingJournal.Application/Notebook/NotebookCommands.cs` | B, B1 |
| M | `src/PersonalTradingJournal.Desktop/App.xaml.cs` | A, B |
| A | `src/PersonalTradingJournal.Desktop/Screenshots/NotebookImages.cs` | B |
| A | `src/PersonalTradingJournal.Desktop/Screenshots/WpfNotebookImageValidator.cs` | A |
| M | `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Autosave.cs` | B |
| M | `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Editing.cs` | B |
| A | `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Images.cs` | B, B1 |
| M | `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.cs` | B |
| A | `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookEditorAdapter.Images.cs` | B, B1 |
| M | `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookEditorAdapter.cs` | B |
| A | `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.History.cs` | B |
| A | `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.Images.cs` | B, B1 |
| M | `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml` | B |
| M | `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml.cs` | B |
| M | `src/PersonalTradingJournal.Infrastructure/Backups/PlaybookImageInventory.cs` | A |
| M | `src/PersonalTradingJournal.Infrastructure/Backups/PortableExportSchema.cs` | A |
| A | `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookImageFiles.cs` | A |
| A | `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookImageService.cs` | A, B, B1 |
| A | `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.Images.cs` | A, B, B1 |
| M | `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.cs` | A, B, B1 |
| M | `src/PersonalTradingJournal.Infrastructure/Persistence/Configurations/NotebookConfiguration.cs` | A |
| M | `src/PersonalTradingJournal.Infrastructure/Persistence/JournalDbContext.cs` | A |
| A | `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261020180223_AddNotebookOwnedImageFiles.Designer.cs` | A |
| A | `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261020180223_AddNotebookOwnedImageFiles.cs` | A |
| M | `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/JournalDbContextModelSnapshot.cs` | A |
| M | `src/PersonalTradingJournal.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs` | A |
| M | `src/PersonalTradingJournal.Infrastructure/Persistence/Records/NotebookRecords.cs` | A |
| M | `src/PersonalTradingJournal.Infrastructure/Screenshots/RetainedScreenshotFileStorage.cs` | A |
| A | `tests/PersonalTradingJournal.Application.Tests/Notebook/NotebookImageContractTests.cs` | A |
| M | `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookAutosaveTests.cs` | B |
| M | `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookEditingTests.cs` | B |
| A | `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookImageAdmissionTests.cs` | A, B, B1 |
| A | `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookImageCoordinationTests.cs` | B, B1 |
| A | `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookImageEditorTests.cs` | B, B1 |
| M | `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookViewTests.cs` | B |
| M | `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookWorkspaceTests.cs` | B |
| A | `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/NotebookImagePortabilityTests.cs` | A, B |
| M | `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/NotebookPortabilityTests.cs` | A |
| M | `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/PortableExportTests.cs` | A |
| M | `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/SavedPeriodPortabilityTests.cs` | A |
| A | `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookImageDraftTests.cs` | B |
| A | `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookImagePlacementTests.cs` | B1 |
| A | `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookImageTests.cs` | A, B1 |
| M | `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookPersistenceTests.cs` | A |
| M | `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Initialization/JournalDatabaseInitializerTests.cs` | A |
| M | `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integration/ProductionPersistenceIntegrationTests.cs` | A |
| M | `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integrity/SqlitePersistenceIntegrityTests.cs` | A |
| M | `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Migrations/InitialMigrationTests.cs` | A |
| M | `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePersistenceTests.cs` | A |
| M | `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePortabilityTests.cs` | A |
| M | `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookPersistenceTests.cs` | A |
| M | `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/DailyTradingPrincipleTests.cs` | A |
| M | `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/SharedTradingPlanTests.cs` | A |
| M | `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/TradingPlanPortabilityTests.cs` | A |

## M22.8B1 — Atomic image placement

20 October 2026. Actual baseline and unchanged HEAD: **clean `develop`, `aa75cdc884e1c9acff7bb15fee4c75296c2507dd`**, sole existing worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`, containing the verified B implementation. Repository/ancestor guidance, this contract, import/placement, ownership ledger, save/checkpoint/recovery and prior artifacts were inspected first. No applicable `AGENTS.md` or duplicate active test run was found. One agent, no subagents; all existing implementation is preserved.

### Cause and transaction boundaries

Previously the ViewModel constructed an appended-image document. `NotebookImageService.ImportAsync` staged the original and invoked `AddNotebookImage`, whose strict delta validation permitted only an isolated attachment addition. The first SQLite commit advanced the version and saved that appended document, derived search projection, image metadata and ownership ledger. Only after acknowledgement did the view split paragraphs/replace its selection at a live bookmark. A second ordinary autosave committed that position and another version. Thus the first commit made the image canonical at the tail, and interruption before the second write preserved that unintended placement. A dirty draft could also require a separate preceding flush; it was not the cause of the placement gap.

The corrected Desktop route captures the final validated cloned editor candidate **after draining pending autosave and flushing the dirty draft**, before asynchronous admission. The candidate contains the real new attachment/node IDs, selection replacement, split paragraph/run IDs and intended image position. `ImportNotebookImage.EditorPlacement` selects the explicit `PlaceNotebookImage` command; legacy isolated additions retain their strict unchanged-other-nodes contract. This extension permits editor text/structure changes and removal of selected references, while remaining references must preserve immutable file/Trade ownership and node identity. It cannot introduce a second new or rebound attachment.

The transaction sequence is now:

1. Bound, decode/admit, hash and copy the original bytes to a private temporary file; flush/close and publish a fresh managed filename. This preparatory filesystem operation changes no canonical note or current image row. A failure/cancellation before publication removes only the operation's private temporary file.
2. Acquire the existing immediate SQLite writer transaction and recheck the note's expected version and lifecycle. Verify the new managed original and decoder admission, adopt existing owned references into retention, and add/verify the new ledger identity. Save the **final candidate document**, searchable projection and exact ordered metadata; advance the note version **once**. Verified readback and the existing last cancellation boundary precede the non-cancellable commit. All these database changes roll back together on rejection/failure.
3. Acknowledge only verified success. If the editor revision is unchanged, render the exact committed candidate under the existing update guard, preserving semantic undo history and the following-paragraph caret; it is Saved without a placement autosave. If typing/title changes occurred meanwhile, a still-valid live bookmark applies the same identity to that newer draft and it remains dirty. A changed nonempty selection/removed anchor is never overwritten; the newer draft remains available for its ordinary guarded save. That later write represents newer editing, not a required correction to canonical placement.

Before canonical commit, a fresh reader sees the previous exact note and no new current reference/ledger row. After commit, including before client acknowledgement, a fresh reader sees the intended middle position with matching metadata/ownership and the single version advance. Lost acknowledgement uses the existing fresh, non-cancelled ledger/document resolution; uncertain outcomes retain bytes and report uncertainty. Precommit failures compensate published uncommitted files only after the existing writer-locked ownership/recovery recheck. Abrupt death after file publication but before the database commit can leave unreferenced bytes; no reclamation is introduced. There is **no restart-time or background compensating move**.

The existing exclusive import ownership, lifetime cancellation, navigation veto and disposal drain remain. A stale expected version preserves newer persisted content. Later saves/checkpoints use the acknowledged version; stale recovery remains conflict-protected. Removal, selection replacement and undo do not reclaim originals. Restored references continue to resolve through the same note's immutable retained ledger, including missing-file placeholders. Original bytes, document JSON **1**, logical export **17**, archive **1** and exact archive inventories are unchanged.

### Focused verification

Eight new deterministic Infrastructure cases cover middle-paragraph replacement before commit, committed-before-acknowledgement fresh reads, private preparation failure/cancellation, cancellation after staging and at the final precommit boundary, a newer-content conflict after staging, consecutive middle placements, exact metadata/order/version and retained originals through undo/redo and a freshly loaded recovery checkpoint. All eight passed in `TestResults/m228b1-verified/atomic-boundaries.trx`. Fresh service/context instances are used at interruption/recovery boundaries; these are **not native-process restart tests**.

Desktop coverage additionally asserts exact committed editor JSON and ordered metadata immediately on insertion, with no dirty placement save. The real SQLite/Windows-decoder test now inserts between paragraphs, verifies the separate dirty flush plus exactly one image commit, and retains its original-byte, preview/file-handle and fresh-workspace recovery checks. Existing coordination tests now exercise the final-placement contract with newer typing/disposal. Existing exact JSON/content assertions, canonical paragraph newlines, deadlines and concurrency settings remain intact.

**M22.8B1 focused implementation/verification is complete.** Final affected Release builds passed with **zero warnings/errors**: Desktop/tests **9.62 s** (`TestResults/m228b1-desktop-build-focused.log`), Infrastructure/tests **12.41 s** (`m228b1-infrastructure-build.log`), Application/tests **3.47 s** (`m228b1-application-build.log`). The earlier successful Desktop build (**55.93 s**, `m228b1-desktop-build.log`) precedes the final coordination-test extension. No runtime source changed after the final builds/runs.

Evidence root: **`TestResults/m228b1-verified/`**; `summary.json` records actual commands/exits/counters, baseline, source/binary hashes, checks and exact inventory. Every focused run exited **0**, with zero failures/skips:

| Final focused run | Passed / total | Reported test duration | TRX under evidence root |
| --- | ---: | --- | --- |
| Desktop — all Notebook editor, image, autosave, recovery, tag, navigation and SQLite cases | 111 / 111 | 3m4s | `desktop-focused.trx` |
| Infrastructure — Notebook/storage/schema/export/archive/restore and attachment compatibility | 521 / 521 | 3m44s | `infrastructure-focused.trx` |
| Application — Notebook contracts | 13 / 13 | 73 ms | `application-focused.trx` |

The earlier **8/8** boundary-only run (**5 s**, `atomic-boundaries.trx`) overlaps the 521 Infrastructure cases and is not added again. **20 final isolated report/log sets** are preserved in `isolated/`. All **12 regenerated renders** are preserved in `renders/notebook-image-renders/` using `{Light|Dark}-{1280-96|480-240}-{codec-error|inline-caption-missing|missing-and-actions}.png`. The four inline-caption-missing theme/size combinations were inspected in B1: aspect ratio, caption wrapping, placeholder and Dark contrast remain readable; no blocking visual regression was found. Narrow content retains the established inner/outer scrolling and viewport clipping. These are synthetic renders, not live-shell acceptance.

EF reports **no pending model changes** in `ef-consistency.txt`. Final tracked/added whitespace and **168 local Markdown target-existence checks / zero missing** pass; URL and anchor validation are outside that documentation check. `inventory-check.json` confirms the exact inventory below. An initial whitespace invocation with `core.autocrlf=false` exited **2**, treating the existing README CRLF endings as trailing whitespace; its output is preserved in `whitespace-raw-crlf.txt`. The repository-configured `git diff --check` passes, without changing whitespace rules or content assertions. New-file `git diff --no-index --check -- NUL <path>` exit 1 with no whitespace diagnostics is the normal added-file diff. Only documentation/evidence changed afterward; passing runtime runs were not repeated solely for documentation edits. **No complete Release gate or Calendar/headroom measurement was run.**

Commands actually used (Infrastructure's focused portability fixtures ran with their required Windows symlink/isolated synthetic DPAPI capabilities):

```powershell
dotnet build tests/PersonalTradingJournal.Desktop.Tests -c Release --no-restore -v minimal -flp:logfile=TestResults/m228b1-desktop-build-focused.log
dotnet build tests/PersonalTradingJournal.Infrastructure.Tests -c Release --no-restore -v minimal -flp:logfile=TestResults/m228b1-infrastructure-build.log
dotnet build tests/PersonalTradingJournal.Application.Tests -c Release --no-restore -v minimal -flp:logfile=TestResults/m228b1-application-build.log
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests -c Release --no-build --no-restore --filter 'FullyQualifiedName~FinalMiddlePlacement|FullyQualifiedName~ConsecutiveMiddlePlacements' --logger 'trx;LogFileName=atomic-boundaries.trx' --results-directory TestResults/m228b1-verified --blame-hang-timeout 3m -v minimal
dotnet test tests/PersonalTradingJournal.Desktop.Tests -c Release --no-build --no-restore --filter 'FullyQualifiedName~Notebook' --logger 'trx;LogFileName=desktop-focused.trx' --results-directory TestResults/m228b1-verified --blame-hang-timeout 3m -v minimal
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests -c Release --no-build --no-restore --filter 'FullyQualifiedName~Notebook|FullyQualifiedName~Backups|FullyQualifiedName~SqlitePersistenceIntegrityTests|FullyQualifiedName~InitialMigrationTests|FullyQualifiedName~JournalDatabaseInitializerTests|FullyQualifiedName~ProductionPersistenceIntegrationTests|FullyQualifiedName~TradingRulePersistenceTests|FullyQualifiedName~TradingRulePortabilityTests|FullyQualifiedName~PlaybookPersistenceTests|FullyQualifiedName~PlaybookImagePersistenceTests|FullyQualifiedName~DailyTradingPrincipleTests|FullyQualifiedName~SharedTradingPlanTests|FullyQualifiedName~TradingPlanPortabilityTests|FullyQualifiedName~LocalTradeScreenshotFileStorageTests' --logger 'trx;LogFileName=infrastructure-focused.trx' --results-directory TestResults/m228b1-verified --blame-hang-timeout 3m -v minimal
dotnet test tests/PersonalTradingJournal.Application.Tests -c Release --no-build --no-restore --filter 'FullyQualifiedName~Notebook' --logger 'trx;LogFileName=application-focused.trx' --results-directory TestResults/m228b1-verified -v minimal
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
git diff --check
git diff --no-index --check -- NUL tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookImagePlacementTests.cs
pwsh -NoProfile -File TestResults/m228b1-check-doc-links.ps1
pwsh -NoProfile -File TestResults/m228b1-summarize.ps1
```

### Exact M22.8B1 changed-file inventory

Relative to clean baseline `aa75cdc884e1c9acff7bb15fee4c75296c2507dd`: **14 modified + 1 added file**, uncommitted. B's historical inventory below belongs to its earlier checkpoint; its implementation is already in this baseline. Ignored TestResults evidence/checkers are outside the source inventory.

Modified:

```text
README.md
docs/notebook.md
src/PersonalTradingJournal.Application/Notebook/INotebookImages.cs
src/PersonalTradingJournal.Application/Notebook/NotebookCommands.cs
src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Images.cs
src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookEditorAdapter.Images.cs
src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.Images.cs
src/PersonalTradingJournal.Infrastructure/Notebook/NotebookImageService.cs
src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.Images.cs
src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.cs
tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookImageAdmissionTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookImageCoordinationTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookImageEditorTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookImageTests.cs
```

Added:

```text
tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookImagePlacementTests.cs
```

**At the B1 checkpoint, overall acceptance remained pending; C verification is recorded above.** Human keyboard/mouse/Narrator, real clipboard/picker/viewer, native-process restart, actual installed-codec WebP success and matching-source CI remain unverified. No production/provider access, Git publication, file reclamation or M22.9 work occurred.

## M22.8B — Desktop image integration

20 October 2026. Actual baseline: **clean `develop`, `80eada6b7a758997505de3bad49fe8a0a52b9940`**, sole worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. The supplied request described M22.8A as uncommitted, but the actual baseline already contains it. Repository/ancestor instructions, this contract, A's import/read/retention operations, editor, autosave and existing artifacts were inspected; no applicable `AGENTS.md` or duplicate active test run was found. One agent, no subagents; no production/provider access or Git publication.

### Image actions, paste and projection

Active supported JSON v1 documents containing images are now editable. The adapter projects each image as a protected semantic block carrying its existing node ID and attachment ID. Captions/alternative text and ordered reference metadata remain in the established model; image bytes, WPF objects and source filenames never enter canonical JSON. Future/unreadable payloads still have no editable draft and are preserved. Archived/Trashed image bodies and caption inputs remain read-only.

**Insert image** opens the existing local file picker. PNG/JPEG and installed-decoder WebP use A's actual signature, single-frame, byte and dimension admission; original file bytes are retained exactly. WebP is explicitly conditional on an installed decoder. Paste accepts an explicit encoded PNG stream when available, or encodes a bounded clipboard bitmap as lossless PNG. The editor's existing non-image Unicode-text paste and title limits remain; HTML/RTF images are never fetched or imported. Cancellation and invalid/unavailable codecs leave selected content intact, with useful feedback. No OS clipboard read occurs during activation, selection or CanExecute.

Insertion validates a cloned text-container candidate before touching the live selection. Paragraph splitting uses a direct text-pointer operation, preserving surrounding marks and content. Each new image has an editable following paragraph, including document-end insertion and consecutive bitmap pastes. Control-V selects an actually present clipboard format through a testable read port. A live bookmark follows typing at a caret. A changed nonempty selection, removed anchor, replaced editor or different note prevents delayed replacement. Image paste uses the same insertion route and testable input port. Cut exports readable Unicode text and removes selected references through the same validated editor path; it does not copy managed ownership into another note.

Inline previews use the existing bounded decoder (longest preview dimension at most 384 pixels), at most two concurrent view loads, uniform aspect ratio and editor-constrained width. Originals retain their full resolution. Long captions wrap under the image, following the supplied mockup's bordered chart/caption treatment. **Open original** explicitly opens the existing themed scrollable viewer pattern; reading a note never opens an external viewer. **Image actions** provides caption/alternative text, Move earlier/later within the containing block collection, and Remove reference. Missing, corrupt and unsupported originals have a readable placeholder while surrounding text remains editable. Owned reads use A's reader; Trade references use the existing screenshot reader and verify the returned Trade/screenshot identities. Source streams are disposed; preview bitmaps are frozen and retain no source-file handle. Cancelled or obsolete note loads cannot publish a late preview.

### Save coordination, undo and retention

Import drains an in-flight checkpoint/save and saves pending dirty content before capturing the current note/version. **M22.8B1 supersedes B's original two-write placement sequence:** the final editor candidate, image metadata/ledger and one note-version advance now commit together. See the transaction and interruption verification above. Only newer edits made during import require a subsequent ordinary save; navigation continues to guard that newer dirty draft.

The import owns the existing exclusive write and lifetime cancellation boundary. Navigation is guarded during preparation/import; disposal cancels ownership before draining. Failure retains the draft; serious conflict/storage outcomes pause automatic retries. Rejected formats/codecs do not pause unrelated autosave. A stale selection after a committed import is preserved, and the next draft save removes the unused current reference while retaining its bytes. No second import or duplicate reference is issued.

Necessary Application integration: **`SaveNotebookImageDraft`** atomically saves title, document and the bounded image projection for text edits, multiple keyboard removals, caption/order changes and undo. Existing file identities cannot be rebound; current attachment/node identities are checked, and any restored owned ID/key/size/hash must match this same note's immutable ledger. It cannot stage/import new bytes. New Trade references must resolve to their authoritative Trade screenshot; existing unavailable references remain preservable. Ordinary `SaveNotebookDraft`/`UpdateNotebookContent` still reject arbitrary metadata changes. Lifecycle/version checks, verified readback and transaction rollback remain. Missing retained originals can be restored semantically to placeholders without rewriting or discarding the reference; the owned reader now returns resolved metadata with a missing/unreadable status so recovery can preserve that identity safely.

Native WPF undo was found to recreate split Runs without their IDs and sometimes restore an empty image visual child. Therefore, from the first image reference onward, the view uses **100 undo steps / 101 immutable document states**, including exact image identities and surrounding formatting, with selection offsets. Toolbar and Control-Z/Control-Y use that history; native image-object undo is disabled. Restoring a state renders fresh previews from semantic references. Text-only documents retain native undo, with semantic states recorded in case an image is subsequently inserted. Save does not clear history; replacement/refresh of the note does. Invalid image-editor structure can undo to the last valid state. Programmatic rendering/loading does not create dirty content or saves; actual undo/redo is an editor change and participates in autosave.

Undo/removal never delete originals. A's ledger and file retention are unchanged and intentionally indefinite, including recovery and removed references. Trade originals are never transferred or deleted by Notebook removal. No garbage collection, duplication or permanent-note deletion is enabled. Same-version recovery may restore an image-bearing checkpoint from the retained ledger; stale versions remain conflict-protected. Complete archives still include exact current/retained originals and fail on missing/tampered required files; logical export stays **17**, document/archive formats stay **1**.

### Focused verification and visual evidence

**M22.8B implementation and focused verification are complete.** Final affected Release builds passed with **zero warnings/errors**: Desktop/test **13.00 s** (`TestResults/m228b-desktop-build-caret-complete.log`), Infrastructure/test **7.14 s** (`m228b-infrastructure-build-complete.log`), Application/test **4.75 s** (`m228b-application-build-final.log`). EF reports **no pending model changes**, recorded in `TestResults/m228b-verified/ef-consistency.txt`; no schema/export/archive version changed in B.

Durable evidence root: **`TestResults/m228b-verified/`**. **`summary.json`** records baseline/HEAD, exact inventory, source and binary hashes, final counters, exit codes and timestamps. The Desktop test output's Infrastructure binary matches the current Infrastructure build. Every final focused run exited **0**, with zero failures/skips:

| Final focused run | Passed / total | Failed / skipped | Reported test duration | TRX under evidence root |
| --- | ---: | ---: | --- | --- |
| Desktop — all Notebook editor, image, autosave, recovery, tag, navigation and SQLite cases | 111 / 111 | 0 / 0 | 2m | `desktop-focused-caret-complete.trx` |
| Infrastructure — Notebook/storage/schema/export/archive/restore and existing attachment compatibility | 513 / 513 | 0 / 0 | 3m10s | `infrastructure-focused-complete.trx` |
| Application — Notebook contracts | 13 / 13 | 0 / 0 | 97 ms | `application-focused.trx` |

Top-level TRX counts are reported once; repeated isolated child-suite executions overlap those cases and are not added to the totals. **20 final isolated process reports/log sets** are copied to `isolated-caret-final/`; earlier isolated reports are retained separately. New coverage comprises **14 Desktop cases and 7 Infrastructure cases**, plus the existing archive round trip now using the editor reconciliation command. Controlled gates cover dirty/in-flight autosave, newer typing, selected-content protection, conflict/cancellation, navigation/disposal and obsolete preview completion. Real isolated SQLite/Windows decoder coverage verifies import, original bytes, retained removal/recovery, missing originals, bounded preview decode and released file handles. Trade preview tests verify actual owner identity and stream disposal. Full JSON/format/reference assertions and canonical paragraph/image newlines remain exact; values are not trimmed to hide differences.

All **12 final renders** were inspected from `renders/`, with the supplied reference preserved as `reference-workspace.png`. Filename matrix: `{Light|Dark}-{1280-96|480-240}-{codec-error|inline-caption-missing|missing-and-actions}.png`. Normal renders are 1280×1000 pixels at 96 DPI; narrow renders are 1200×2125 pixels for 480×850 DIP at 240 DPI. Inline charts retain the reference's wide aspect ratio, thin border and caption spacing; long captions wrap within the editor. Missing references, expanded caption/order/removal controls and codec feedback remain readable. The discovered Dark-mode black **Image actions** label is corrected with the theme foreground and a regression assertion. No blocking defect remains in these captures. Long content uses the existing inner/outer scrolling at narrow sizes; scrolled evidence intentionally clips earlier content above the viewport. These are synthetic workspace renders, not human/live-shell acceptance or pixel-perfect reproduction of the supplied mockup.

Intermediate failures are preserved: `TestResults/m228b/images-initial.trx` **6 passed / 4 failed** exposed the routed paragraph-break placement defect; `images-placement.trx` **6 / 4** exposed native undo losing Run identities. Visual inspection additionally found native undo restoring an empty image child, motivating semantic image history. `desktop-focused-first.trx` **104 / 4** found the inconsistent text-only fixture's unrelated image metadata; the fixture was corrected without weakening its Saved/content assertions. `TestResults/m228b-verified/desktop-focused.trx` **110 / 1** expected IOException where corrupt decoding actually throws InvalidDataException; the assertion now specifies each exact failure type. The initial `infrastructure-focused.trx` **506 / 7** compared ImmutableArray storage identity; explicit element-by-element equality now verifies every image field. An early test build's **CS0649** for the not-yet-connected `Imports.Before` gate is retained in `TestResults/m228b-desktop-build-tests.log`; connecting the coordination seam resolved it. The pre-contrast successful Desktop run (**111/111**, `desktop-focused-complete.trx`) is retained separately; the final run above includes the contrast fix and expanded/scrolled evidence. Assertions, existing process deadlines and concurrency settings were not relaxed.

Final tracked and all **8 added-file whitespace checks pass**. README/Notebook local Markdown target-existence checks pass **167 targets / zero missing**, evidence `TestResults/m228b-documentation-links.json`; URL/fragment validation is outside that check. The documented inventory exactly matches all **27 current changed files**, evidence `TestResults/m228b-verified/inventory-check.json`. Only documentation/evidence changed after final runtime verification; runtime gates were not repeated for those edits. **No complete Release gate or new Calendar/headroom measurement was run.** Human keyboard/mouse/Narrator, real OS clipboard, native picker/viewer interaction, native-process restart, installed-codec WebP success and matching-source CI remain **unverified**. Synthetic clipboard data, controlled ports and isolated WPF renders do not establish those checks. M22.8 overall acceptance and M22.8C remain pending; retention/reclamation, duplication/permanent deletion and integrated M22.11 limitations above remain.

Commands (PowerShell from the repository root; the existing in-memory design-time factory is used by EF):

```powershell
dotnet build tests/PersonalTradingJournal.Desktop.Tests -c Release --no-restore -v minimal -flp:logfile=TestResults/m228b-desktop-build-caret-complete.log
dotnet build tests/PersonalTradingJournal.Infrastructure.Tests -c Release --no-restore -v minimal -flp:logfile=TestResults/m228b-infrastructure-build-complete.log
dotnet build tests/PersonalTradingJournal.Application.Tests -c Release --no-restore -v minimal -flp:logfile=TestResults/m228b-application-build-final.log
dotnet test tests/PersonalTradingJournal.Desktop.Tests -c Release --no-build --no-restore --filter 'FullyQualifiedName~Notebook' --logger 'trx;LogFileName=desktop-focused-caret-complete.trx' --results-directory TestResults/m228b-verified --blame-hang-timeout 3m -v minimal
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests -c Release --no-build --no-restore --filter 'FullyQualifiedName~Notebook|FullyQualifiedName~Backups|FullyQualifiedName~SqlitePersistenceIntegrityTests|FullyQualifiedName~InitialMigrationTests|FullyQualifiedName~JournalDatabaseInitializerTests|FullyQualifiedName~ProductionPersistenceIntegrationTests|FullyQualifiedName~TradingRulePersistenceTests|FullyQualifiedName~TradingRulePortabilityTests|FullyQualifiedName~PlaybookPersistenceTests|FullyQualifiedName~PlaybookImagePersistenceTests|FullyQualifiedName~DailyTradingPrincipleTests|FullyQualifiedName~SharedTradingPlanTests|FullyQualifiedName~TradingPlanPortabilityTests|FullyQualifiedName~LocalTradeScreenshotFileStorageTests' --logger 'trx;LogFileName=infrastructure-focused-complete.trx' --results-directory TestResults/m228b-verified --blame-hang-timeout 3m -v minimal
dotnet test tests/PersonalTradingJournal.Application.Tests -c Release --no-build --no-restore --filter 'FullyQualifiedName~Notebook' --logger 'trx;LogFileName=application-focused.trx' --results-directory TestResults/m228b-verified -v minimal
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
git diff --check
pwsh -NoProfile -File TestResults/m228b-check-doc-links.ps1
pwsh -NoProfile -File TestResults/m228b-summarize.ps1
```

Final command-path review added an explicit bitmap-only Control-V test. `desktop-focused-clipboard-final.trx` retained **107 passed / 4 failed**: after the first document-end image, the caret had no paragraph and the consecutive image paste left the reference count at **3 instead of 4**. Insertion now leaves an editable following paragraph and safely handles an image boundary. The format-selection correction and consecutive command paste are included in the final Desktop run above; `desktop-focused-final.trx` retains the earlier successful **111/111** contrast verification. The caret patch's initial **CS1503** (unsupported Paragraph constructor) and **CS0136** (local/pattern name collision) builds are retained in `m228b-desktop-build-boundary-final.log` and `m228b-desktop-build-boundary-complete.log`; the explicit block insertion and renamed local compile with zero warnings/errors in `m228b-desktop-build-caret-complete.log`.

Infrastructure fixtures ran with their required Windows link/DPAPI capabilities in isolated temporary directories/databases. Whitespace for each added file also uses `git diff --no-index --check -- NUL <path>`; exit 1 with no whitespace diagnostics is the normal new-file diff, not a failed whitespace check.


### Exact M22.8B changed-file inventory

Relative to clean baseline `80eada6b7a758997505de3bad49fe8a0a52b9940`: **19 modified + 8 added files**, all uncommitted. M22.8A is preserved in the baseline; its historical 39-file inventory below is not a second set of current changes. Ignored TestResults logs/TRX/renders/checkers/summary are local evidence outside source inventory.

Modified:

```text
README.md
docs/notebook.md
src/PersonalTradingJournal.Application/Notebook/NotebookCommands.cs
src/PersonalTradingJournal.Desktop/App.xaml.cs
src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Autosave.cs
src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Editing.cs
src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.cs
src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookEditorAdapter.cs
src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml
src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml.cs
src/PersonalTradingJournal.Infrastructure/Notebook/NotebookImageService.cs
src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.Images.cs
src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.cs
tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookAutosaveTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookEditingTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookImageAdmissionTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookViewTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookWorkspaceTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Backups/NotebookImagePortabilityTests.cs
```

Added:

```text
src/PersonalTradingJournal.Desktop/Screenshots/NotebookImages.cs
src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Images.cs
src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookEditorAdapter.Images.cs
src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.History.cs
src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.Images.cs
tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookImageCoordinationTests.cs
tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookImageEditorTests.cs
tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookImageDraftTests.cs
```

## M22.8A — image storage and application operations

20 October 2026. Actual starting baseline: **clean `develop`, `e0682d3a898742ff8612857de5a2245267eae041`**, worktree `C:/Users/Yovko/source/repos/Personal-Trading-Journal`. M22.7 automated verification was already committed. Inspected repository/ancestor instructions (no applicable `AGENTS.md`), this contract, existing Deferred commands, recovery, screenshot storage/capture/retention, Playbook admission, export and archive/restore implementations. One agent; no subagents. This phase preserves existing work and makes no Git publication or production/provider requests.

### Operations and admission

- `INotebookImages.ImportAsync` receives a caller-owned readable stream, note ID, expected version, new attachment ID and the complete proposed document. The source path/name is never stored. Import returns a verified canonical snapshot through the existing `AddNotebookImage` command. Stream ownership stays with the caller; managed read/write handles are disposed internally. No clipboard or image-insertion control is added.
- Existing `UpdateNotebookImage` and `RemoveNotebookImage` commands now persist caption, alternative-text, ordering and reference removal. The immutable domain image model is unchanged. Image positions/captions/alt text must match the document's image nodes exactly; the command's named delta must preserve every unrelated node, identity, mark, reference and metadata value. Ordering is a move of the named image in the document plus matching ordered metadata. The canonical document, derived search text, metadata, audit time and one version advance commit together.
- Ordinary title/body saves cannot smuggle file metadata changes. Archived/Trashed notes remain read-only to image mutations. Unsupported/malformed original documents are not rewritten or replaced by an empty body. Duplicate and permanent-note deletion remain explicitly **Deferred**.
- Byte reads are bounded before decode: nonempty, at most **8 MiB**; signatures must be PNG/JPEG/WebP. Host decoding enforces one frame, dimensions at most **8,192 each**, at most **16,000,000 pixels**, and actual pixel materialization. APNG/WebP animation chunks are explicitly rejected even if an installed decoder exposes only the first frame. The existing Playbook decoder policy is reused by a thin Windows admission adapter, serialized to bound concurrent full decoding. Original bytes are never re-encoded. Cancellation is checked during bounded reads and around decode/staging/commit boundaries; native decode itself is a bounded synchronous operation.
- The decoder is an Application interface; all WPF code remains in Desktop. Non-Desktop hosts fail closed with `Unsupported` unless they provide a decoder. Import distinguishes Invalid, UnsupportedContent, TooLarge, Conflict, NotFound, Cancelled and StorageFailure. `ReadOriginalAsync` explicitly distinguishes Success, Missing, Unreadable, Invalid, Unsupported, TooLarge and Cancelled, verifying frozen size/hash before returning bounded original bytes and stored file metadata. Trade screenshot references return Unsupported through this **owned-image** reader; they do not grant ownership or authorize Trade mutation.

### Storage, commit and retention

Originals use a dedicated **`notebook-{noteId:N}-{imageId:N}-{random:N}.{png|jpg|webp}` namespace** in `IApplicationPaths.ScreenshotsDirectory`, normally `%LocalAppData%/PersonalTradingJournal/screenshots`. This deliberately reuses the documented flat managed-key model, application paths, backup attachment layout and screenshot capture lease instead of introducing another image root/archive path convention. No source paths or file-name identities survive import. A newly generated key cannot collide with a referenced Trade screenshot key. Resolution rejects path traversal, links/aliases and unsafe keys using the existing storage and opened-handle identity checks.

Sequence: validate version/document delta → read bounded source and decode → write/flush/close a private temporary file → publish a fresh immutable original → expected-version SQLite writer transaction inserts the ownership ledger/current reference and writes the canonical document/projection → verify readback → non-cancellable commit → return Success. Direct add/restoration commands also verify original size/hash and actual admission; a caller-supplied metadata array is not proof of a valid image.

Before-publication failure/cancellation cleans the private temporary file. After publication, unsuccessful canonical writes are resolved through a fresh non-cancelled database read. A verified committed result returns Success even if its original acknowledgement/cancellation was ambiguous. Compensation acquires the existing capture lease and an immediate SQLite writer transaction in the established lock order, rechecking all ownership catalogs before deletion. A failed/unavailable resolution **never deletes the original**; `CleanupPending` and sanitized issues explicitly report uncertain outcomes or failed cleanup. Process death can leave uncommitted staging/orphan bytes; this phase does not promise automatic orphan reclamation.

Additive migration **`20261020180223_AddNotebookOwnedImageFiles`** introduces `NotebookOwnedImageFiles`: application-assigned attachment ID, original Note ID, unique storage key, byte count and SHA-256. It has no note FK/cascade, so future source deletion cannot silently destroy recovery ownership. Current image references remain in `NotebookImages`; the ledger records immutable file identity, not a second image/document model. Existing owned metadata is adopted into retention before image edits can remove its current reference.

Reference removal deletes current metadata/document nodes **only**. Ledger entries/originals remain indefinitely across Active/Archived/Trash, recovery and upcoming undo/redo, including an explicit restoration of a retained same-note ID/key. Screenshot/Playbook cleanup now checks both Notebook catalogs as well as existing owners. If a recovery checkpoint wins while an import is waiting for its canonical write, compensation retains the staged original and records its ownership even when the note write is cancelled. Unreadable/future/oversized or over-capacity recovery inventories are conservatively treated as potential references; their raw originals are untouched. Ordinary note/image/recovery reads never create retention records or change canonical content.

**Physical cleanup responsibility:** no committed owned-file reclamation occurs here. Later lifecycle/undo work must prove absence from every retained note, durable checkpoint and live undo/redo history before reclaiming ledger/file pairs under the same capture/writer locks. Uncertain staging cleanup requires later verified resolution, not deletion by filename age. Until that ownership/lifecycle work ships, disk retention is intentional; deleting a reference or recovery draft alone cannot release bytes. Trade-owned originals are never deleted by Notebook reference removal.

### Export and archive compatibility

The ledger adds a persisted record set, so logical export advances **16 → 17**, exporting every ledger field and preserving the exact table/column completeness assertions. Document JSON remains **v1**. Complete archive format remains **v1** because the existing managed `screenshots/` paths, manifest entry kind, size/hash and capture/restore machinery already represent these files. Inventory now includes **both current owned Notebook images and the retained ledger**, across all lifecycle states and recovery-only copies. Missing required files or metadata/hash mismatches fail capture/preflight instead of publishing an apparently complete archive. Offline restore includes those originals using the existing atomic switch/recovery path. Logical JSON/CSV still contain metadata, not image bytes, and are not restore inputs.

Existing metadata-only portability fixtures now supply exact owned originals; their exact content/recovery assertions remain intact. Migration, model/column/index inventories and export-version assertions include the additive table and unique key. The installed schema now has **28 migrations**. Older-schema archives remain rejected by exact-schema preflight; a live additive migration is not an offline archive-upgrade mechanism. **Full integrated M22.11 acceptance remains pending**, including the later image UI/lifecycle/duplication workflows, reclamation, broader fault scenarios and any separately designed old-schema archive upgrade path.

### Focused verification and limitations

Final affected Release builds passed with **0 warnings/errors**: Infrastructure/test project **8.12 s**, Desktop/test project **8.88 s**, Application/test project **2.97 s**. Final build logs are `TestResults/m228a-infrastructure-build-complete.log`, `TestResults/m228a-desktop-build-complete.log` and `TestResults/m228a-application-build-ready.log`; earlier build logs are also retained. EF reports **no pending model changes** through the existing in-memory design-time factory.

Durable final evidence is under **`TestResults/m228a-verified/`**, including **`summary.json`** with actual counters, durations, source inventory and binary/source hashes. Final focused runs all exited **0**, with zero failures/skips:

| Final focused run | Passed | Failed / skipped | Reported test duration | TRX |
| --- | ---: | ---: | --- | --- |
| Infrastructure storage/Notebook/schema/export/archive/restore compatibility | 506 | 0 / 0 | 2m39s | `infrastructure-focused-complete.trx` |
| Desktop admission/editor/autosave/SQLite coordination | 49 | 0 / 0 | 11s | `admission-and-editor-complete.trx` |
| Application Notebook contracts | 13 | 0 / 0 | 124 ms | `application-focused.trx` |

This includes **27 new image/storage/fault/retention cases, 4 new archive/ownership cases, 7 actual Windows decoder/import cases, and 2 portable header-policy cases**, alongside unchanged editor/recovery content assertions and focused existing storage, schema, export, backup, preflight, offline restore and compatibility suites. The older/intermediate runs below overlap these totals; they are not additional cases. Fixtures use isolated temporary paths/databases; deterministic interceptors and operation boundaries replace timing sleeps.

Earlier artifacts are retained: `TestResults/m228a/images-initial.trx` passed **16/16**; `images-and-archives.trx` recorded **19 passed / 1 failed**, exposing a paths-DI registration omission; `admission-and-editor-coordination.trx` recorded **40 passed / 8 failed**, seven from that same DI issue and one from an uncaught WPF `FileFormatException`. The factory registration and exception mapping were corrected. The first broader Infrastructure run at `TestResults/m228a-verified/infrastructure-focused.trx` recorded **504 passed / 1 failed**, zero skips: migration test `InitialMigrationTests.LatestMigrationsCreateOnlyTheApprovedSchema` still expected 18 unique indexes, now 19 with `NotebookOwnedImageFiles.StorageKey`. The exact inventory now includes and asserts that new index; no assertion/deadline/concurrency rule was relaxed.

The corrected migration-inventory run passed **505/505**, retained as `infrastructure-focused-final.trx`. Final API review then corrected low-level image-command outcome mapping (UnsupportedContent for an unavailable decoder, StorageFailure for unreadable storage) and made null/invalid-version import requests explicit Invalid results. One added direct-admission regression proves those boundaries; the rebuilt final source passed **506/506** Infrastructure and **49/49** Desktop in the `*-complete.trx` runs above. `summary-before-api-review.json` retains the earlier source/binary hashes; `summary.json` identifies the final tested source.

Final tracked/new-file whitespace checks pass. README/Notebook local Markdown target-existence checks passed **166 targets / zero missing**, evidence `TestResults/m228a-documentation-links.json`; the retained local checker is `TestResults/m228a-check-doc-links.ps1`. External URLs and fragment/anchor validation are outside that target-existence check. Only documentation/evidence changed after final runtime verification, so passing runtime suites were not repeated for those edits.

**M22.8A focused implementation/verification is complete; M22.8 overall acceptance remains pending.** No complete Release gate was launched in M22.8A. Desktop insertion, inline rendering/thumbnails, clipboard admission flows, caret/undo integration and image-reference-body editing are M22.8B work; image-reference bodies remain read-only in the existing editor. Complete integrated verification belongs to M22.8C. Native-process restart, human interaction/Narrator, matching CI and actual installed-codec WebP success on this machine are unverified. The decoder implementation supports WebP only when the installed codec actually accepts it; PNG/JPEG success and animated/malformed rejection are tested. No M22.8B work or Git publication was performed.

Commands for this focused phase (the Infrastructure filter intentionally includes every modified compatibility-test class):

```powershell
dotnet build tests/PersonalTradingJournal.Infrastructure.Tests -c Release --no-restore -v minimal -flp:logfile=TestResults/m228a-infrastructure-build-complete.log
dotnet build tests/PersonalTradingJournal.Desktop.Tests -c Release --no-restore -v minimal -flp:logfile=TestResults/m228a-desktop-build-complete.log
dotnet build tests/PersonalTradingJournal.Application.Tests -c Release --no-restore -v minimal -flp:logfile=TestResults/m228a-application-build-ready.log
dotnet ef migrations add AddNotebookOwnedImageFiles --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests -c Release --no-build --no-restore --filter 'FullyQualifiedName~Notebook|FullyQualifiedName~Backups|FullyQualifiedName~SqlitePersistenceIntegrityTests|FullyQualifiedName~InitialMigrationTests|FullyQualifiedName~JournalDatabaseInitializerTests|FullyQualifiedName~ProductionPersistenceIntegrationTests|FullyQualifiedName~TradingRulePersistenceTests|FullyQualifiedName~TradingRulePortabilityTests|FullyQualifiedName~PlaybookPersistenceTests|FullyQualifiedName~PlaybookImagePersistenceTests|FullyQualifiedName~DailyTradingPrincipleTests|FullyQualifiedName~SharedTradingPlanTests|FullyQualifiedName~TradingPlanPortabilityTests|FullyQualifiedName~LocalTradeScreenshotFileStorageTests' --logger 'trx;LogFileName=infrastructure-focused-complete.trx' --results-directory TestResults/m228a-verified
dotnet test tests/PersonalTradingJournal.Desktop.Tests -c Release --no-build --no-restore --filter 'FullyQualifiedName~NotebookImageAdmissionTests|FullyQualifiedName~NotebookEditingTests|FullyQualifiedName~NotebookAutosaveTests|FullyQualifiedName~NotebookSqliteWorkspaceTests' --logger 'trx;LogFileName=admission-and-editor-complete.trx' --results-directory TestResults/m228a-verified
dotnet test tests/PersonalTradingJournal.Application.Tests -c Release --no-build --no-restore --filter 'FullyQualifiedName~Notebook' --logger 'trx;LogFileName=application-focused.trx' --results-directory TestResults/m228a-verified
git diff --check
pwsh -NoProfile -File TestResults/m228a-check-doc-links.ps1
```

### Exact M22.8A changed-file inventory

Relative to starting HEAD `e0682d3a898742ff8612857de5a2245267eae041`: **28 modified + 11 added files**, all uncommitted. Generated migration/designer/snapshot changes belong to the single additive ownership-ledger migration. Existing test-file changes preserve the exact schema/export/content assertions while including the new table/index/version. Ignored TestResults artifacts are additional local evidence, not source inventory.

Modified:

- `README.md`
- `docs/notebook.md`
- `src/PersonalTradingJournal.Application/Backups/IPortableExportService.cs`
- `src/PersonalTradingJournal.Application/Notebook/INotebookService.cs`
- `src/PersonalTradingJournal.Desktop/App.xaml.cs`
- `src/PersonalTradingJournal.Infrastructure/Backups/PlaybookImageInventory.cs`
- `src/PersonalTradingJournal.Infrastructure/Backups/PortableExportSchema.cs`
- `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Configurations/NotebookConfiguration.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/JournalDbContext.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/JournalDbContextModelSnapshot.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Records/NotebookRecords.cs`
- `src/PersonalTradingJournal.Infrastructure/Screenshots/RetainedScreenshotFileStorage.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/NotebookPortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/PortableExportTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/SavedPeriodPortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookPersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Initialization/JournalDatabaseInitializerTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integration/ProductionPersistenceIntegrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integrity/SqlitePersistenceIntegrityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Migrations/InitialMigrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookPersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/DailyTradingPrincipleTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/SharedTradingPlanTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/TradingPlanPortabilityTests.cs`

Added:

- `src/PersonalTradingJournal.Application/Notebook/INotebookImages.cs`
- `src/PersonalTradingJournal.Desktop/Screenshots/WpfNotebookImageValidator.cs`
- `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookImageFiles.cs`
- `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookImageService.cs`
- `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.Images.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261020180223_AddNotebookOwnedImageFiles.Designer.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261020180223_AddNotebookOwnedImageFiles.cs`
- `tests/PersonalTradingJournal.Application.Tests/Notebook/NotebookImageContractTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookImageAdmissionTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/NotebookImagePortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookImageTests.cs`

## M22.7 — tags, assignments and combined filters: focused verification

20 October 2026. Resumed the interrupted implementation in **`C:/Users/Yovko/source/repos/Personal-Trading-Journal`**, branch **`develop`**, unchanged HEAD **`4520b3f782453ff5ef1bdeac509b765fac7b7099`**. One agent; no subagents. Inspected ancestor/repository instructions (no applicable `AGENTS.md`), existing changes, this contract and prior TRX. Process inspection confirmed reusable MSBuild workers only, with no active test run before rebuilding. All existing changes were preserved; this phase made no commits, pushes, resets or clean operations.

### Existing implementation and focused corrections

- Desktop tag creation, rename and confirmed global deletion are distinct from assigning/removing a tag on one active note. Identity/version checks and dirty-draft flushes coordinate with autosave without replacing the editor. Global deletion removes assignments across lifecycle states while preserving note content.
- Text over saved title/body, all selected tags and the selected scope combine with AND. Stable cursor paging preserves tag IDs; selected filters resolve independently of the first catalog page in the same SQLite read snapshot. Sidebar counts precede text/tag filters; list totals reflect them. Search has a separate **300 ms** debounce with superseded-read suppression and cancellation/draining. Clear filters retains scope/sort.
- Prior artifacts remain unchanged: `TestResults/m227-desktop/` records **85 passed / 2 failed / zero skipped**; `TestResults/m227-infrastructure/` records **96/96 passed**. The two failures expected paragraph text without its canonical LF. **Both corrections already existed on resumption**, comparing the full expected document projection. `NotebookDocument.Visit` appends `"\n"` after paragraphs/headings, consistent with the original domain projection test and this document's content contract. No production serialization change, trimming, weakened content assertion or timeout adjustment was used.
- Expanded SQLite refresh coverage to selected tags both within and beyond the first 50 catalog entries, retaining another selected filter. The new assertion exposed a deleted tag retained in catalog/pickers from the pre-refresh open-note snapshot. Filter repair now removes authoritatively deleted selected identities from the retained catalog before republishing it. It preserves remaining filters and uses the existing single bounded repair read.
- Expanded Desktop recovery coverage for both external global deletion and removal of one assignment. A stale autosave conflicts, keeps exact title/body in recovery, and cannot restore an assignment after workspace restart. Added deterministic Infrastructure cases proving stale-version rejection after either removal and proving a subsequent explicitly refreshed content save also leaves the assignment removed. Existing concurrent deletion/save coverage remains intact.

The initial added regression run is retained at `TestResults/m227-resumed-regression/selected-tag-regression.trx`: **0 passed / 2 failed**. One failure exposed stale catalog retention; the beyond-first-page case also revealed a test setup omission, fixed by loading the next catalog page before each selection. Both variants pass in the final focused run below.

### Verified focused results

```powershell
dotnet build tests/PersonalTradingJournal.Desktop.Tests -c Release --no-restore -v minimal -flp:logfile=TestResults/m227-resumed-desktop-build-final.log
dotnet build tests/PersonalTradingJournal.Infrastructure.Tests -c Release --no-restore -v minimal -flp:logfile=TestResults/m227-resumed-infrastructure-build.log
dotnet test tests/PersonalTradingJournal.Desktop.Tests -c Release --no-build --no-restore --filter 'FullyQualifiedName~Notebook' --logger 'trx;LogFileName=notebook-desktop.trx' --results-directory TestResults/m227-resumed-focused
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests -c Release --no-build --no-restore --filter 'FullyQualifiedName~Notebook|FullyQualifiedName~PortableExportTests' --logger 'trx;LogFileName=notebook-infrastructure.trx' --results-directory TestResults/m227-resumed-focused
dotnet test tests/PersonalTradingJournal.Domain.Tests -c Release --no-restore --filter 'FullyQualifiedName~NotebookDocumentTests.ProjectionIncludesVisibleTextAndCaptionsNotUrlsOrAlternativeText' --logger 'trx;LogFileName=document-projection-contract.trx' --results-directory TestResults/m227-resumed-focused
git diff --check
```

Both affected Release test-project builds, including their production dependencies, passed with **0 warnings/errors**: Desktop **16.27 s**, Infrastructure **5.25 s**. The domain contract run also rebuilt its test project. Final TRX under `TestResults/m227-resumed-focused/`:

| Focused run | Passed | Failed / skipped | Reported test duration | Evidence |
| --- | ---: | ---: | --- | --- |
| Notebook Desktop, including matching shell navigation cases | 90 | 0 / 0 | 2m01s | `notebook-desktop.trx` |
| Notebook Infrastructure and portable export | 99 | 0 / 0 | 1m09s | `notebook-infrastructure.trx` |
| Exact canonical document projection | 1 | 0 / 0 | 27 ms | `document-projection-contract.trx` |

All commands exited **0**. Desktop includes the existing autosave, editing, workspace, navigation, SQLite restart, tag coordination and isolated compiled-WPF view suites. Coverage retains entered-autosave/newer-typing coordination, returned versions, guarded/failed filter changes, failure recovery, stale search suppression, paging/counts, outside-filter editor retention, cancellation and reactivation. Infrastructure includes codec, query, persistence, migration, recovery, tag workflow and portability/export checks. Isolated child suites can repeat cases internally; these table totals are the outer TRX counts, not sums of nested runs.

Tag WPF renders were regenerated for Light/Dark at 1280 DIP / 96 DPI and 480 DIP / 240 DPI under `tests/PersonalTradingJournal.Desktop.Tests/bin/Release/net10.0-windows/notebook-tag-renders/`. Sampled Light normal and Dark narrow filter renders were inspected; the narrow organization pane uses vertical scrolling. Bound tests assert retained editor identity, accessible labels/live validation and visible button bounds. This is automated render/interaction evidence, not human or Narrator acceptance. Final whitespace checks pass.

### Focused-phase acceptance boundary (historical)

At the focused checkpoint, **final Release acceptance was pending**: the complete gate had deliberately not been launched. The historical M22.6 gate did not accept the changed worktree. The subsequent complete verification below supersedes that automated-verification gap. No EF/schema or image-ownership work was introduced; M22.8 onward remains deferred.

### Complete Release verification — 20 October 2026

This phase resumed on **clean `develop`, `97b0ee82a7622ebd578b150db946d5c661b6c4ef`**, which already commits the implementation and focused corrections described above. The worktree remains `C:/Users/Yovko/source/repos/Personal-Trading-Journal`; no applicable repository/ancestor `AGENTS.md` or duplicate test process was present. The existing tag-view harness was extended only to capture saved-body search combined with both selected tags, the scrolled management controls, and the expanded note assignment picker in all four theme/size cases. Its existing assertions, process/STA deadlines, and concurrency remain intact; additional exact list/filter-count assertions confirm the combined state. No production code changed during this completion.

The entire solution was rebuilt after this capture extension: **0 warnings/errors, 8.06 s**, log `TestResults/m227-complete-release-build-final.log`. EF's existing in-memory design-time factory reports **no pending model changes**, with no production database access. `TestResults/m227-complete-source-manifest.json` records the tested HEAD, Notebook source hashes and Desktop/Infrastructure test-binary SHA-256 values. Full-gate execution uses the established Windows context for synthetic credential/symlink checks and the unchanged `scripts/Test-Release.ps1` partition scheduling.

Commands executed for the final source:

```powershell
dotnet build PersonalTradingJournal.sln -c Release --no-restore -v minimal -flp:logfile=TestResults/m227-complete-release-build-final.log
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
pwsh -NoProfile -File scripts/Test-Release.ps1 -ResultsDirectory TestResults
# After final documentation edits:
git diff --check 4520b3f782453ff5ef1bdeac509b765fac7b7099
pwsh -NoProfile -File TestResults/m227-check-doc-links.ps1
```

The **complete Release gate passed 5,131/5,131, zero failures/skips, exit 0**, once, without retries, removed cases, relaxed assertions/deadlines, or scheduling changes. It ran from **20:40:16.226 to 20:50:03.114 Europe/Kiev**, **9m46.887s**. The wrapper preserved the actual process exit code in `TestResults/m227-complete-release-exit-code.txt`.

| Complete partition / project | Passed | Failed / skipped |
| --- | ---: | ---: |
| Exclusive WPF / Desktop | 88 | 0 / 0 |
| Parallel / Domain | 602 | 0 / 0 |
| Parallel / Application | 1,032 | 0 / 0 |
| Parallel / Infrastructure | 1,512 | 0 / 0 |
| Parallel / Desktop | 1,897 | 0 / 0 |
| Total | 5,131 | 0 / 0 |

Combined Desktop total: **1,985**. The complete gate includes all **15 M22.7 Desktop workflow/visual cases and 8 Infrastructure tag workflow cases**, alongside existing Notebook and repository regressions. These are included in the table, not additional totals. Reported test durations: Infrastructure **4m58s**, parallel Desktop **8m08s**. Calendar native process exited **0** in **72.7175179 s**, leaving **47.2824821 s** against its unchanged **120 s** deadline; process cleanup was confirmed.

Durable gate evidence:

- `TestResults/release-20261020-174016-954f7bd34d35413a860b961ced6ad1e9/summary.json` — complete project/partition totals.
- Same directory: partition TRX, VSTest diagnostics, supervised child results/stdout/stderr/phase and process logs; `m227-verification.json` records exit/totals/timing, verified unchanged source/binary hashes and the 28 render hashes.
- Calendar timing/cleanup: `exclusive-wpf/calendar-native-20261020-174020-7907050c66364b1197b9de627883bc3a/process-supervision.log` beneath that gate directory.

**Visual verification:** the existing compiled-WPF harness generated **28 PNGs**, copied without modification from `tests/PersonalTradingJournal.Desktop.Tests/bin/Release/net10.0-windows/notebook-tag-renders/` into the gate's **`m227-visuals/`** directory. The filename pattern is `{Light|Dark}-{1280-96|480-240}-{state}.png`; dimensions are 1280 × 960 DIP / 96 DPI and 480 × 760 DIP / 240 DPI. All **12** combinations of the four theme/size prefixes and `management`, `combined-filters`, `assignments` states were inspected. Additional retained states are `filters`, `validation`, `assignment-removed`, `cleared`.

Management captures show the scrolled Create/Rename/Delete globally controls and explanatory text; combined-filter captures show `observation` search, both selected tags, one matching saved note and filter help; assignment captures show full chips, remove buttons, the expanded picker and disabled duplicate assignment. Narrow views wrap buttons/text and expose lower content by vertical scrolling. No blocking layout defect was found. **Minor visual limitation:** long selected names truncate in the compact management/assignment combo boxes; full names remain readable in chips/filter labels. Render fixtures use synthetic stub catalog counts; actual count correctness is covered by SQLite query/workflow tests. These captures and bound tests do not establish human keyboard/mouse/Narrator acceptance.

Final whitespace checks cover the full M22.7 slice, including the capture extension and completion documentation. Local Markdown target-existence checks passed **165 targets / zero missing**, for README and this document; evidence `TestResults/m227-documentation-links.json`, reproducible with the retained local `TestResults/m227-check-doc-links.ps1`. External URLs and fragment/anchor validation are outside that target-existence check. Only documentation changed after the passing runtime gate, so it was not repeated.

**M22.7 local automated Release verification is complete.** Human interaction, actual native-process restart and matching-source CI remain **unverified**. The local gate used **586.887 s**, approximately **13.113 s** below CI's configured **600 s** test-step timeout before wrapper/runner overhead; hosted timing margin is not assured. No production/provider access, publication, new EF/schema work or M22.8 implementation was performed. Notebook image ownership/backup and linked-target workflows remain deferred to their planned slices.

### Exact final M22.7 changed-file inventory

Relative to starting HEAD **`4520b3f782453ff5ef1bdeac509b765fac7b7099`**, M22.7 contains **8 modified + 4 added files**. The implementation is committed in `97b0ee8…`; this completion additionally edits only `README.md`, `docs/notebook.md`, and the existing `NotebookTagViewTests.cs` capture harness, leaving them uncommitted. Durable local test/log/render artifacts are ignored evidence, not additional source files.

Modified:

- `README.md`
- `docs/notebook.md`
- `src/PersonalTradingJournal.Application/Notebook/NotebookQueries.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml`
- `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.Queries.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookSqliteWorkspaceTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookWorkspaceTests.cs`

Added:

- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Tags.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookTagTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookTagViewTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookTagWorkflowTests.cs`

## M22.6 — autosave, concurrency and durable recovery

20 October 2026. Starting baseline: **clean `develop`, `0a89d26ff036b12561160e6bb4c569954a5a28c3`**, preserving committed M22.1–M22.5. No applicable repository/ancestor `AGENTS.md` was found. Existing SaveNotebookDraft, semantic draft generations, shell close/navigation consent, managed application paths, short-lived SQLite context/transaction ownership, portable-export completeness and controlled-time conventions were inspected.

### Autosave and operation ownership

- A meaningful title/document edit replaces the **750 ms** quiet-period timer, using injectable `TimeProvider`. Loading, preview rendering and selecting do not save. Undo to the persisted semantic value cancels unnecessary canonical work; an obsolete checkpoint can still be cleaned up.
- One tracked pipeline serializes checkpoint, existing `SaveNotebookDraft`, returned-version acknowledgement and checkpoint cleanup. It is not a second canonical write path. Ctrl+S/Save cancels the pending timer and requests immediate saving; its UI remains disabled during an entered write. Captured identity, expected version, draft generation and activation owner prevent stale acknowledgement. Cancellation of a replaced timer does not cancel a write already entered.
- Typing during saving keeps the live editor and newer generation. A successful older save advances the persisted baseline/version but cannot mark newer input Saved. Remaining dirty content receives another quiet interval without overlapping writes. Pin and move flush first and then use the returned version; existing destination capture and guards are retained.
- Any failed/invalid/conflicting save pauses automatic canonical attempts. Further meaningful edits can checkpoint the latest draft, but do not retry a failing canonical write. Use Save explicitly to retry. Stale-version conflicts never fetch a new version to overwrite it. There is no periodic retry, popup loop, merge or duplicate-note workaround.
- The existing editor status distinguishes Unsaved, Saving, Saved, failure/retry and conflict. Checkpoint success explicitly says **not yet saved as a note**. Only canonical success/readback produces Saved. Background acknowledgement does not recreate the FlowDocument or reset selection/caret/scroll/undo.

### Checkpoint format, limits and portability

The earlier architecture paragraph promised only canonical-save durability. This slice explicitly strengthens that guarantee with the requested durable dirty-draft checkpoints; it does not promise durability for every keystroke.

`INotebookRecoveryStore` is the application port; `NotebookRecoveryStore` is a DI-wired infrastructure adapter. Additive migration **`20261020130020_AddNotebookRecovery`** adds `NotebookRecoveryDrafts` inside `IApplicationPaths.DatabasePath`: `journal.db` in the managed data directory (normally `%LocalAppData%/PersonalTradingJournal`, overridden for isolated tests). No Desktop control performs filesystem persistence.

The **envelope version is 1**: editing-session/checkpoint ID, original Note ID, expected canonical version, monotonic checkpoint revision, title, canonical document JSON v1 and UTC checkpoint time. Empty/temporarily invalid titles are recoverable within the unchanged 200-character bound. Document limits remain **1 MiB serialized UTF-8**, 200,000 plain-text characters, 4,096 nodes/depth 16/list depth 3. Invalid editor structures cannot be checkpointed; their last successful checkpoint remains intact and the editor reports the limitation.

At most **128 checkpoints** are retained, discovered in deterministic ID order through **50-row pages**. Capacity failure is explicit; no eviction or silent truncation. Each writer owns an immediate SQLite transaction and short-lived context/connection. Updates require the exact previous checkpoint revision and stable note identity, with no backwards expected-version change. Failed/cancelled writes roll back. Cleanup uses the captured checkpoint revision, so an old acknowledgement cannot delete a newer checkpoint. Updating an ID after its removal conflicts instead of recreating it. No note FK/cascade is used: deleting the source cannot erase unresolved work.

Checkpoint and canonical transactions are intentionally separate. A process can stop between canonical commit and cleanup. Recovery compares semantic title/document content with the current note, recognizes an already-persisted copy and cleans it without another canonical write. A cleanup failure does not deny a confirmed canonical save.

Logical export advances **v15 → v16**, explicitly exporting every checkpoint field alongside all existing records. The unchanged completeness assertion covers the new table. Complete SQLite archives remain format **v1**, including checkpoints; synthetic export, preflight, offline restore, cancellation, rollback and interrupted-restore tests seed valid, malformed and future-envelope recovery copies and compare their raw fields. Credentials remain excluded; note/recovery text is sensitive local content. Earlier logical exports contain no checkpoints and are not restore inputs. **Older-schema archives are still rejected by exact-schema preflight**; the normal additive live-database migration does not make offline old-schema archives compatible. No Notebook image-file restore workflow is added.

### Recovery and departure decisions

Activation/Refresh discovers recovery copies in Organization → Recovery drafts, including a bounded More action. **Recover** reads the original copy and current source before installing a draft. Nothing is automatically overwritten on discovery. A matching active source version allows explicit Save; recovery initially pauses automatic canonical saving. A changed version, missing source, archived/trashed source or incompatible image references remains protected. Missing/non-editable sources show recovered title/body read-only and are never recreated/reactivated. Existing image bodies remain read-only with title-only editing. Unreadable/future/oversized recovery originals remain stored; only explicit confirmed discard removes them.

For conflicts, preserve/copy the text you need, or explicitly discard the pending draft before refreshing the current note. There is no merge engine, automatic adoption of the latest source version, duplication workflow or attachment editing.

Before replacing/closing a dirty draft, the existing guard cancels its pending timer, drains the entered save and attempts a final flush unless automatic attempts are paused. Successful persistence proceeds without a dialog. Otherwise the existing Save / Discard / Cancel choice remains. Cancel keeps the editor. Explicit Discard drains the pipeline and removes the exact durable checkpoint before granting departure consent; failure to remove it refuses departure. The in-memory editor is restored only after the ancestor accepts. A later ancestor veto retains the input, clears consent and schedules a new checkpoint; until that checkpoint succeeds, the retained in-memory edits are not crash-durable. Discard does not undo an already committed autosave.

Actual deactivation/disposal cancels the activation owner before linked operations, drains tracked tasks/timers and prevents late publication. Re-entry waits for prior deactivation work before loading. A returned successful commit can still be acknowledged when cancellation arrived after the commit boundary; cancelled cleanup leaves a comparable recovery copy rather than claiming nothing was written.

**Durability boundary:** only the last successfully committed checkpoint/canonical note survives abrupt termination. Edits inside the debounce, edits made during an earlier save but not yet checkpointed, invalid editor structures, capacity failures and failed checkpoint writes may remain memory-only. Abrupt-process recovery is not a substitute for backups.

### Verification and remaining acceptance

Synthetic data only; controlled clock/timer tests and explicit operation gates, not timing sleeps. Coverage includes debounce replacement/undo, manual flush, newer edits/returned versions, metadata coordination, image restrictions, failure pause/retry, discard/later veto, cancellation/draining, checkpoint CAS/rollback/capacity/paging, restart conflicts, source deletion/read-only sources, future/malformed content, interrupted cleanup and export/restore inclusion.

Commands:

```powershell
dotnet ef migrations add AddNotebookRecovery --project src/PersonalTradingJournal.Infrastructure --configuration Release
dotnet test tests/PersonalTradingJournal.Desktop.Tests -c Release --no-restore --filter "FullyQualifiedName~Notebook" --logger trx --results-directory TestResults/m226-focused-desktop-final
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests -c Release --no-restore --filter "FullyQualifiedName~Notebook|FullyQualifiedName~Portability|FullyQualifiedName~PortableExport|FullyQualifiedName~InitialMigration|FullyQualifiedName~JournalDatabaseInitializer|FullyQualifiedName~ProductionPersistenceIntegration" --logger trx --results-directory TestResults/m226-focused-infra-final
dotnet test tests/PersonalTradingJournal.Desktop.Tests -c Release --no-restore --filter "FullyQualifiedName~NotebookAutosaveTests|FullyQualifiedName~NotebookSqliteWorkspaceTests|FullyQualifiedName~NotebookNavigationTests|FullyQualifiedName~NotebookEditingTests" --logger trx --results-directory TestResults/m226-final-coordination
dotnet build PersonalTradingJournal.sln -c Release --no-restore
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests -c Release --no-build --no-restore --filter "FullyQualifiedName~SqlitePersistenceIntegrityTests|FullyQualifiedName~NotebookRecoveryTests|FullyQualifiedName~NotebookPortabilityTests|FullyQualifiedName~PortableExportTests|FullyQualifiedName~InitialMigrationTests|FullyQualifiedName~JournalDatabaseInitializerTests" --logger trx --results-directory TestResults/m226-inventory-verified
pwsh -File scripts/Test-Release.ps1 -ResultsDirectory TestResults
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
git diff --check
```

Development chronology: initial compilation caught two invalid target-typed generic constructor expressions. The first focused Desktop run was **64 passed / 2 failed**: an accidentally relaxed Save-button enablement rule violated the existing no-duplicate-in-flight assertion. The existing rule was restored; no assertion changed. Initial affected Infrastructure was **107 passed / 2 failed**: the explicit schema lists already included the migration, but two migration-count checks still expected 26 instead of 27; those expectations now include the additive migration.

Subsequent focused runs: **71/71 Notebook Desktop**, **114/114 persistence/portability**, then **38/38 final coordination** cases passed, zero skips. These overlap; they are not additive unique totals. The preceding solution Release build passed with **0 warnings/errors**, 6.50 seconds.

The first complete gate finished with **5,107 passed / 1 failed / zero skipped**, exit 1, at `TestResults/release-20261020-161829-bf9a8e8f1d794dd0a29d185534755e39/summary.json`. The sole failure was `SqlitePersistenceIntegrityTests.ModelContainsExactlyExpectedRecordsWithDomainOwnedIdentifiers` (line 91 in that build): its explicit expected inventory omitted the new `NotebookRecoveryRecord`. Calendar passed in **79.106 s**, leaving **40.894 s**. The one-line inventory addition was already present in the worktree but postdated the tested binary; this failed run is retained, not reclassified as successful.

**Final verification, 20 October 2026:** the actual baseline had advanced to clean `develop`, **`7dbcd657ee48313d582a92952cf12fcc4954f060`**, already containing all 35 M22.6 files and the inventory correction. No test process was active before the rebuild. Inspection confirmed the recovery record is deliberately mapped, has an application-assigned `Id` with `ValueGeneratedNever`, and participates in the unchanged exact-inventory and all-primary-key assertions. Foreign-key/cascade checks remain unchanged; recovery deliberately has no source-note FK so source deletion cannot silently erase unresolved work. No further production/test correction was necessary.

Rebuilt the entire solution from this HEAD: **0 warnings/errors**, **1m01.31s**. The rebuilt-binary affected run above passed **81/81**, zero failures/skips, **43s**; evidence `TestResults/m226-inventory-verified/Yovko_DESKTOP-N2ONT35_2026-10-20_19_51_38_net10.0.trx`. It covers the full integrity class plus checkpoint, migration, initialization, export and portability checks. Infrastructure test DLL SHA-256: `E57BC1C5238416D395CB8AA2CDF3EFC5CEAF135CC449F7269A7C424E59C1B51F`.

The **final complete Release gate passed 5,108/5,108**, zero failures/skips, exit **0**, using those rebuilt binaries and the unchanged repository scheduling/deadlines/assertions. Evidence directory: `TestResults/release-20261020-165252-3c7dae4b44fd491ea3a0867595b81443/`; `summary.json`, partition TRX, VSTest diagnostics and supervised child logs are retained. The formerly failing integrity test also passed in this full run.

| Final partition / project | Passed | Failed / skipped |
| --- | ---: | ---: |
| Exclusive WPF / Desktop | 88 | 0 / 0 |
| Parallel / Domain | 602 | 0 / 0 |
| Parallel / Application | 1,032 | 0 / 0 |
| Parallel / Infrastructure | 1,504 | 0 / 0 |
| Parallel / Desktop | 1,882 | 0 / 0 |

Calendar native process: **69.942 s / 120 s**, **50.058 s headroom**, exit 0 and confirmed cleanup (`exclusive-wpf/calendar-native-20261020-165256-3104b8759f93481a96d1a6140e5d7283/process-supervision.log`). Parallel Infrastructure took **4m32s** and Desktop **7m30s**. These timings describe this local run, not a guarantee against future contention or CI deadlines.

EF reports **no pending model changes**, using the existing in-memory design-time factory. Final whitespace checks cover the full M22.6 slice and completion edits; local documentation target-existence checks pass **163 targets / zero missing** (not anchor validation). Only `README.md` and `docs/notebook.md` changed during this completion, after runtime verification; no additional runtime gate was needed for these documentation-only edits.

Compiled-WPF renders: `tests/PersonalTradingJournal.Desktop.Tests/bin/Release/net10.0-windows/notebook-autosave-renders/{Light|Dark}-{1280-96|480-240}-{recovery-available|recovered|saving|saved|failed|conflict}.png`. Inspected Light/Dark at 1280 × 900 DIP / 96 DPI and 480 × 720 DIP / 240 DPI: recovery actions, wrapped status/help, disabled in-flight actions and retained document are readable. Narrow content/organization use the established panel switcher and vertical scrolling. These are automated renders and bound interactions, **not live human or screen-reader acceptance**.

| Evidence | Status |
| --- | --- |
| Autosave/version/guard/recovery deterministic checks | Passed focused |
| SQLite migration, CAS/rollback, restart, source retention and export/restore | Passed focused |
| Compiled WPF retained document/selection, accessible status and layout | Passed focused; renders inspected |
| Complete repository Release gate | Passed 5,108/5,108; zero failures/skips |
| Human keyboard/mouse/Narrator and native-process restart | Not performed; pending |
| Matching published-source CI | Not checked/claimed for `7dbcd657…`; local passing gate is not CI acceptance |

No production journal, provider, real clipboard or browser access during implementation/verification. This completion made no commits, pushes or merges and leaves its documentation edits uncommitted. **M22.6 implementation and local automated verification are complete; human interaction, native-process restart and matching CI remain separate unverified checks. M22.7–M22.12 were pending at that checkpoint.**

### Exact M22.6 changed-file inventory

The full slice relative to `0a89d26ff036b12561160e6bb4c569954a5a28c3` contains **26 modified + 9 added files** below, now included in `7dbcd657…`. The only additional uncommitted changes from this verification/completion are `README.md` and `docs/notebook.md`, already included in that inventory. No implementation/test files were altered during this completion.

Modified:

- `README.md`
- `docs/notebook.md`
- `src/PersonalTradingJournal.Application/Backups/IPortableExportService.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Editing.cs`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml.cs`
- `src/PersonalTradingJournal.Infrastructure/Backups/PortableExportSchema.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Configurations/NotebookConfiguration.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/JournalDbContext.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/JournalDbContextModelSnapshot.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookSqliteWorkspaceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/NotebookPortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/PortableExportTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/SavedPeriodPortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Initialization/JournalDatabaseInitializerTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integration/ProductionPersistenceIntegrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integrity/SqlitePersistenceIntegrityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Migrations/InitialMigrationTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePortabilityTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookPersistenceTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/DailyTradingPrincipleTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/SharedTradingPlanTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/TradingPlanPortabilityTests.cs`

Added:

- `src/PersonalTradingJournal.Application/Notebook/INotebookRecoveryStore.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Autosave.cs`
- `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookRecoveryStore.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Records/NotebookRecoveryRecord.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261020130020_AddNotebookRecovery.cs`
- `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261020130020_AddNotebookRecovery.Designer.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookAutosaveTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookAutosaveViewTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookRecoveryTests.cs`

## M22.5 — rich-text editor and manual Save

20 October 2026. Starting baseline: **clean `develop`, `41f0bfac263f5214e99bcee614f00491d4dbe953`**, preserving committed M22.1–M22.4. No applicable repository/ancestor `AGENTS.md` was found. Existing Journal text controls, Notebook immutable nodes/codec, transactional mutations, theme resources, supervised STA tests and shell close/navigation guards were inspected before implementation.

### Canonical content and editing

- Desktop-only `NotebookEditorAdapter` explicitly maps canonical JSON-v1 nodes to/from WPF paragraphs, headings 1–3, marked runs, line breaks, HTTP(S) hyperlinks, bulleted/numbered lists and checklist items. WPF `Tag` retains document-local identities where an element survives; native splitting/insertion receives unique IDs. Semantic dirty comparison ignores IDs, not content or formatting. No WPF/XAML/RTF payload is persisted or deserialized.
- Native undoable paragraph properties carry heading presentation. Selection-based formatting reports On/Off/Mixed states; title text remains outside body formatting. Native undo/redo is retained across Save. Checklist markers expose readable Checked/Not checked text; click their marker, use Toggle check or Ctrl+Enter. Enter at the end adds an unchecked item; mid-item Enter inserts a line break. Editing a structural marker directly gives a recoverable validation error rather than silently losing its state.
- Images retain exact canonical reference nodes and metadata. This milestone deliberately makes a body containing image references read-only, with a clear explanation; title editing still works. No fabricated image insertion/viewing workflow or lossy placeholder serialization. Archived/trashed and unavailable/unsupported content remain read-only.
- Paste is explicit, Unicode-text only, including line boundaries. Unsupported external formatting is normalized with visible feedback; HTML scripts, XAML, RTF objects, fonts/colors and images are never imported. Admission validates the complete candidate tree and existing codec byte/node/text/depth limits before touching the live selection. Oversized/invalid input leaves the draft intact. Clipboard access occurs only in Paste execution, never command enablement, activation or polling. Clipboard failure produces a recoverable message.
- Links are validated against the existing HTTP(S)-only domain policy before editing. Creation requires text in one paragraph; edit/remove preserve surrounding content. Opening requires the separate Open link action and repeats validation before OS URI launch. No existing general HTTP-link launcher was found to reuse; this small explicit handler does not fetch content. Tests do not launch browsers or inspect the actual clipboard.

### Manual save and draft protection

`SaveNotebookDraft` adds one atomic title+document mutation through the existing application service, Domain validation and SQLite expected-version transaction/readback. Validation happens before mutation; cancellation before commit rolls back. No schema/migration, export version or financial/source semantics changed. Existing image metadata cannot be changed through this command.

The workspace tracks captured note identity/version, saved semantic content, current draft generation and confirmed Save status. Duplicate submissions are disabled. Edits remain possible during saving: only the captured generation is acknowledged; newer input stays dirty. Successful Save does not replace the live FlowDocument, selection/caret or undo stack. The affected row/search membership/counts refresh through bounded reads without reloading the selected document. A saved note outside the current filter/page keeps its editor and explains why the list no longer contains it.

Failures and conflicts keep all draft text; no automatic overwrite/retry. Save / Discard / Cancel protects note/scope/sort/search/Refresh, creation, metadata changes, shell navigation and application close. Pin/move construct their mutation using the post-save version; Move captures its exact requested destination before Save refreshes the bounded catalog, including Unfiled and later-page folders. Departure during an unfinished write is blocked until completion or explicit cancellation. Discard consent is not destructive until the owning navigation/close actually accepts; later veto clears consent and preserves the draft. Disposal cancels/drains tracked operations and rejects late publication. A canceled operation may already have committed; a confirmed returned commit is acknowledged truthfully.

Shortcuts: Ctrl+N creates, Ctrl+F focuses search, Ctrl+S saves; Ctrl+B/I/U formats, Ctrl+Z/Y undoes/redoes; Ctrl+K opens link editing; Ctrl+Enter toggles the current checklist item. All toolbar actions remain reachable by keyboard. Autosave timers, crash recovery of unsaved drafts, image operations and linked-target opening are not implemented here.

### Visual and verification evidence

Automated compiled-WPF renders use 1280 × 900 DIP at 96 DPI and 480 × 650 DIP at 240 DPI, in Light/Dark. Files are under `tests/PersonalTradingJournal.Desktop.Tests/bin/Release/net10.0-windows/notebook-editor-renders/`: `{Light|Dark}-{1280-96|480-240}-{long-title|saved|document}.png`. The wide view follows the supplied reference's organization/list/editor composition; separate title, secondary metadata, wrapping toolbar and readable document area are retained within the existing shell. Narrow layouts use the established panel switcher and scrollable content; long content and toolbar remain reachable. Image previews, rich imported formatting and a one-line graphical checkbox treatment are not claimed to match the mockup.

Development checks caught checklist TextRange export prefixes, native heading undo representation, normalized hyperlink selection boundaries and stale test paragraph references after native undo. The adapter now reads explicit marker runs, uses undoable paragraph styling and resolves selected link intersections. Two compile errors in the last toolbar/checklist test extension (bool-returning Undo/Redo method groups and IReadOnlyList.Count) were corrected before execution. Final render review caught a blank initial paragraph-style selector; it now initializes from the current selection. Coordination review added regressions for retaining an Unfiled/later-page Move destination across Save. Failed intermediate checks are retained under `TestResults/m225/`; they are not acceptance evidence. Assertions/deadlines/isolation/concurrency were not weakened.

Commands (synthetic data only):

```powershell
dotnet test tests/PersonalTradingJournal.Desktop.Tests -c Release --no-restore --filter "FullyQualifiedName~Notebook" --logger "trx;LogFileName=editor-verified.trx" --results-directory TestResults/m225
dotnet test tests/PersonalTradingJournal.Domain.Tests -c Release --no-build --no-restore --filter "FullyQualifiedName~Notebook" --logger "trx;LogFileName=domain.trx" --results-directory TestResults/m225
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests -c Release --no-build --no-restore --filter "FullyQualifiedName~Notebook|FullyQualifiedName~PortableExportTests" --logger "trx;LogFileName=persistence-export.trx" --results-directory TestResults/m225
dotnet build PersonalTradingJournal.sln -c Release --no-restore
pwsh -File scripts/Test-Release.ps1 -ResultsDirectory TestResults
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
git diff --check
```

Focused Domain **35/35** and Infrastructure Notebook/export **82/82** passed, zero failures/skips; logs `domain.trx` and `persistence-export.trx`. The initial complete gate passed **5,072/5,072**, exit 0, at `TestResults/release-20261020-121301-656044d633ae4e88b561b0df42303fbe/`. Calendar child wall time was **61.370 s / 120 s** (58.630 s headroom). This gate preceded the final style-initialization/Move-destination correction and is not substituted for final-source verification.

Final focused Desktop verification passed **50/50**, zero failures/skips, 42 seconds (`editor-verified.trx`), including both Move destinations and the initial style-state check. Final solution Release build passed with **0 warnings/errors**, 4.58 seconds. EF reports no pending model changes, using the existing in-memory design-time factory. Tracked and all five new files passed whitespace checks; new files additionally used `git diff --no-index --check -- /dev/null <path>`. Documentation target-existence validation checked **162 local links / zero missing** (not anchor validation).

Final **complete Release gate: 5,074 passed, zero failures/skips, exit 0**. Freshly built final binaries, unchanged scheduling/deadlines, outside the restricted sandbox for the existing native/symlink tests. Evidence directory: `TestResults/release-20261020-122506-76b995df1971421982df95e4644726ed/` (summary JSON, per-project TRX, VSTest diagnostics and supervised child output).

| Final gate partition / project | Passed | Failed / skipped |
| --- | ---: | ---: |
| Exclusive WPF / Desktop | 88 | 0 / 0 |
| Parallel / Domain | 602 | 0 / 0 |
| Parallel / Application | 1,032 | 0 / 0 |
| Parallel / Infrastructure | 1,495 | 0 / 0 |
| Parallel / Desktop | 1,857 | 0 / 0 |

Calendar native child: **66.346 seconds**, **53.654 seconds headroom** to the unchanged 120-second deadline, exit 0 and confirmed cleanup. Infrastructure duration 4m37s; parallel Desktop 6m56s. These measurements describe these runs, not a guarantee against future machine contention.

| Acceptance evidence | Status |
| --- | --- |
| Canonical editor mapping, atomic/conflicting saves, guards, lifecycle, cancellation, synthetic restart/export | Passed focused and complete automated gates |
| Bound WPF formatting, mixed selection, checklist/undo, links, paste, retained document/caret and narrow reachability | Passed automated checks |
| Light/Dark normal and narrow/240-DPI long-title, saved and scrolled-document renders | Inspected; initial selector and narrow-scroll issues corrected |
| Human keyboard/mouse, Narrator, real clipboard, explicit browser opening and live restart | Not performed; pending |
| CI matching these changes | Not verified; changes are uncommitted, no matching-source CI acceptance claimed |

Human keyboard/mouse, Narrator, real clipboard/browser launch, live app restart and matching published-source CI remain unverified. Automated WPF interaction and synthetic SQLite restart are not substitutes for those checks.

Final branch/HEAD remain **`develop` / `41f0bfac263f5214e99bcee614f00491d4dbe953`**, with **15 modified + 5 new files** below. No production journal, provider, credentials, real clipboard or browser was accessed; no commit/push/merge. **M22.6–M22.12 remain pending.**

### Exact M22.5 changed-file inventory

Modified:

- `README.md`
- `docs/notebook.md`
- `src/PersonalTradingJournal.Application/Notebook/NotebookCommands.cs`
- `src/PersonalTradingJournal.Domain/Notebook/NotebookNote.cs`
- `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.cs`
- `src/PersonalTradingJournal.Desktop/App.xaml.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/MainWindowViewModel.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml.cs`
- `tests/PersonalTradingJournal.Domain.Tests/Notebook/NotebookNoteTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookPersistenceTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Navigation/NotebookNavigationTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookWorkspaceTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookSqliteWorkspaceTests.cs`

Added:

- `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.Editing.cs`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookEditorAdapter.cs`
- `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookLeaveInteraction.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookEditingTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookEditorViewTests.cs`

## M22.4 — workspace and navigation

20 October 2026. Actual starting baseline: **clean `develop`, `d974177bdaca3743210c335af8bac329d88ae53d`**. M22.1–M22.3A, including export v15, were already committed and remain preserved. No applicable `AGENTS.md` was found in the repository or checked ancestor chain.

### Workspace and operation ownership

The existing Notebook destination now resolves a DI-backed `NotebookWorkspaceViewModel` and its compiled view through the existing shell/DataTemplate mechanism. The shared centered section header, application sidebar and theme selection are unchanged. The visual reference guides the three adjacent bordered surfaces, teal selection accent, clear title hierarchy and dominant reading panel; its sample content is not seeded.

- Organization shows All Notes, Pinned, Unfiled, named folders, Archived and Trash with scope counts. Existing tag names/counts are informational, not pretend filter controls. Catalogs start with the service's bounded 50-item pages; More folders/More tag names expose continuations.
- Notes show title, short saved projection, New York updated time, pin state and tag labels. Recently updated and Title sorting retain repository tie-breaking. Search is explicit through Search/Enter (Ctrl+F focuses its input); Refresh/scope/sort changes use the current query. Full debounced/tag-filter UX remains M22.7. List totals use the filtered population; sidebar counts exclude search.
- Only list metadata is loaded for rows. Each 50-row page exposes Load more until its cursor ends. Selection is by note identity; refresh preserves it when present in the refreshed page, otherwise clears it predictably. A failed scope request retains the last applied list heading/counts and prevents wrong-scope selection until a successful refresh. Moving a selected note outside the current scope removes it from that list; it remains reachable in its destination.
- New note/Ctrl+N creates one verified **Untitled**, Active, unpinned, empty note in the selected folder or Unfiled. It clears search, chooses Recently updated and an appropriate active scope, refreshes counts/list and selects that note. Concurrent submissions are blocked.
- Folder management provides name entry, Create, Rename, confirmed Delete and explicit Clear. Names and expected versions come from real catalog snapshots, including later catalog pages. Deletion uses existing transactional Unfiled behavior across lifecycle/restoration references; it never deletes notes. Failed/conflicting operations retain entered names and explain refresh/retry.
- Active note pin/unpin and Move use the loaded note's exact ID/version. Archived/Trashed and unavailable content remain read-only. No archive/trash/restore/duplicate/permanent-delete/image operations were newly exposed.
- Selection projects the existing validated document tree into a `FlowDocument`: headings, paragraphs, marked runs, line breaks, lists and recorded checklist state. A read-only RichTextBox supports text selection, keyboard scrolling and a text automation pattern. URLs are informational text, not auto-opened; images show available caption/alt text and an honest unavailable-preview label. No arbitrary XAML/parser, network/file loading, body mutation or simulated save feedback.
- Existing Trade/Trading Setup link labels resolve in one bounded batch, retain explicit unavailable states and occupy a scroll-bounded secondary area. There is no fabricated Strategy/Playbook identity or nonfunctional navigation link. Unreadable/unsupported/oversized content keeps list metadata visible and original storage untouched; no blank editable replacement or raw JSON is shown.

List/detail/link reads share a cancellation owner; supersession cancels the prior operation and prevents late publication even if a service ignores cancellation. Tracked completion tasks drain cancellation/departure work. SQLite service work runs off the UI dispatcher; UI continuations publish on the captured context. Repeated activation does not duplicate loads/subscriptions. Writes are exclusive, carry captured identity/version, and block destination/window departure until completion or explicit cancellation. Cancellation messages do not promise rollback of an already committed change. Folder-name input survives navigation; this slice has no unsaved editable body. Disposal cancels tracked work and suppresses late publication; deterministic tests drain its exposed completion task.

### Responsive and visual verification

At **1,000 DIP of page width or more**, the organization/list columns are 210/290 DIP and the remaining width belongs to content. Below that breakpoint, reachable Organization / Notes / Content buttons switch the same panels; note selection opens Content. This avoids squeezing three unreadable columns into the shell's minimum window size. The toolbar wraps; note list, organization, preview and long linked metadata have bounded scrolling. Folder management is secondary and expandable. Shared theme-based input/action styles and visible selected/focus borders are used locally.

Automated renders are written under:

`tests/PersonalTradingJournal.Desktop.Tests/bin/Release/net10.0-windows/notebook-renders/`

The four combinations are **Light/Dark at 1280 DIP / 96 DPI and 480 DIP / 240 DPI**. Filenames use `<Theme>-<Width>-<Dpi>-<state>.png`; states include `content`, `unavailable`, `error`, `loading`, `empty`, plus narrow `organization` and `notes`. These are synthetic bound-WPF renders, not real application input or Narrator acceptance.

Initial inspection exposed fallback Windows styling on several controls; local styles now reuse the actual shared theme resources. Empty feedback spacing was removed, and the Dark folder-management heading received explicit themed foreground. Wide composition and narrow panel reachability were inspected against the supplied mockup. Formatting toolbar, editable body/checklists, chart images, autosave indicator and target-opening chips are intentionally absent rather than simulated. No complete visual parity with those later capabilities is claimed.

### Verification chronology and limits

The first implementation build caught target-typed construction of command interfaces; concrete Toolkit command types corrected compilation. Initial ViewModel checks passed **17/17**. An expanded compile caught the repository's xUnit collection-assertion analyzer; the assertion was expressed as `DoesNotContain` without changing its meaning. The first expanded run passed **23/23**, then additional paging/selection regressions passed **26/26**. The final focused run, including failed-scope recovery, passed **27/27**, zero failures/skips, 33 seconds (`TestResults/m224/notebook-final.trx`). No existing assertion, deadline, isolation or concurrency setting was weakened.

```powershell
dotnet test tests/PersonalTradingJournal.Desktop.Tests -c Release --no-restore --filter "FullyQualifiedName~Notebook" --logger "trx;LogFileName=notebook-final.trx" --results-directory TestResults/m224
dotnet build PersonalTradingJournal.sln -c Release --no-restore
pwsh -File scripts/Test-Release.ps1 -ResultsDirectory TestResults
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
git diff --check
```

Final solution Release build: **zero warnings/errors**, 4.90 seconds. EF: **no pending model changes**, using the in-memory design-time factory. Tracked and all eight new files passed whitespace checks (new files checked with `git diff --no-index --check -- /dev/null <path>`). Documentation target-existence checks found **161 local links, zero missing** across README and this document; this is not anchor validation.

The **complete Release gate passed: 5,049 tests, zero failures/skips, exit 0**. Results are under `TestResults/release-20261020-105354-ff90cd57b00e4720aa1a6ac97c2d7e79/`, including top-level per-project TRX, stage output, diagnostics and supervised child logs. This was one complete gate run against freshly built final code, outside the restricted sandbox for the existing native/symlink tests; scheduling and deadlines were unchanged.

| Gate partition/project | Passed | Failed / skipped |
| --- | ---: | ---: |
| Exclusive Desktop WPF | 88 | 0 / 0 |
| Parallel Desktop | 1,834 | 0 / 0 |
| Domain | 601 | 0 / 0 |
| Application | 1,032 | 0 / 0 |
| Infrastructure | 1,494 | 0 / 0 |

Parallel Desktop took 7m57s and Infrastructure 5m45s. Calendar's native child exited successfully in **92.361 seconds**, leaving **27.639 seconds** against its unchanged 120-second deadline, measured from `process-supervision.log` start/exit timestamps. This is this run's headroom, not a guarantee against future machine-load variability.

| Acceptance evidence | Status |
| --- | --- |
| Workspace, navigation, owned cancellation, mutation recovery, synthetic SQLite restart | Passed focused and complete automated checks |
| Compiled Light/Dark, normal/narrow 240-DPI layout and accessibility properties | Passed automated checks; representative content, unavailable and narrow panel renders inspected |
| Human keyboard/Narrator, native window/DPI interaction | Not performed; still pending |
| CI for these exact changes | Unverified; working-tree changes are uncommitted |

No production journal, provider, credentials, clipboard or Notebook image-file workflow was accessed. Final branch/HEAD remain `develop` / `d974177bdaca3743210c335af8bac329d88ae53d`, with **6 modified and 8 new files**, no commit/push/merge. M22.5–M22.12 remain pending; export/schema/backup contracts and Deferred outcomes are unchanged.

### Exact M22.4 changed-file inventory

Modified:

1. `README.md`
2. `docs/notebook.md`
3. `src/PersonalTradingJournal.Desktop/App.xaml`
4. `src/PersonalTradingJournal.Desktop/App.xaml.cs`
5. `src/PersonalTradingJournal.Desktop/ViewModels/MainWindowViewModel.cs`
6. `tests/PersonalTradingJournal.Desktop.Tests/Navigation/MainWindowViewModelTests.cs`

New:

7. `src/PersonalTradingJournal.Desktop/ViewModels/Notebook/NotebookWorkspaceViewModel.cs`
8. `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookDocumentPreview.cs`
9. `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml`
10. `src/PersonalTradingJournal.Desktop/Views/Notebook/NotebookWorkspaceView.xaml.cs`
11. `tests/PersonalTradingJournal.Desktop.Tests/Navigation/NotebookNavigationTests.cs`
12. `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookWorkspaceTests.cs`
13. `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookSqliteWorkspaceTests.cs`
14. `tests/PersonalTradingJournal.Desktop.Tests/Notebook/NotebookViewTests.cs`

## M22.3A — export completeness and database compatibility

20 October 2026. Starting baseline: **clean `develop`, `1402f6a3909d3fef479fccd9297decc9047acf3c`**. Existing M22.1–M22.3 work is preserved. No repository/ancestor `AGENTS.md` was found. This correction adds no migration, Desktop workspace, image operation, duplication, permanent deletion or AI integration.

### Failure, correction and version boundaries

`PortableExportTests.AllLogicalColumnsAreExplicitlyCoveredExceptRebuildableBrowseCache` failed at its table-count assertion: **35 persisted logical tables versus 29 explicit export tables**. The existing assertion then checks every persisted column, excluding only the rebuildable Trade browse cache. It remains unchanged, with no Notebook exemption.

**v14 was the portable logical JSON/CSV contract**, not a SQLite schema or restore version. `PortableExportService` reads an immutable SQLite snapshot through `PortableExportSchema`'s explicit allowlist, streams deterministic JSON and four CSVs, and publishes a directory with a versioned, hashed row/file inventory. Merely migrating SQLite did not add Notebook to that allowlist.

The correction advances logical export to **v15** and adds JSON arrays `notebookFolders`, `notebookTags`, `notebookNotes`, `notebookTagAssignments`, `notebookLinks` and `notebookImages`, with every persisted scalar column. Identity, UTC audit values, versions, normalized keys, plain/search text, folder membership, tag assignments, pins, lifecycle/restoration metadata, Trade/Trading Setup links and image ownership/reference metadata are retained. Ordering is explicit by stable IDs, composite reference keys and image position. Folders/tags precede notes and Notebook child arrays; references retain exact IDs, not display names.

`documentJson` is exported as **opaque original text**, not decoded/reformatted by the Notebook codec. Malformed and future-version payloads, including whitespace/Unicode, retain their exact string values after JSON decoding. Existing exporter field/output limits, snapshot consistency, cancellation/atomic publication, sanitization, credentials exclusion and CSV behavior are unchanged. Seven existing version assertions advance from 14 to 15 because they read a newly generated whole-database export; their existing payload checks are preserved.

| Boundary | Supported behavior |
| --- | --- |
| SQLite schema | Unchanged here: 26 migrations, latest `20261020085611_AddNotebook`; normal installed-database upgrade is additive. |
| Logical export v15 | All 35 logical tables and columns, including Notebook text/metadata. Four existing CSVs unchanged; Notebook is JSON-only. Unencrypted, no image bytes or credentials. |
| Logical exports v14 and earlier | Keep their original meaning and lack Notebook data. No upgrade can reconstruct omitted notes. Neither old nor new JSON/CSV has a supported import/restore path. |
| Complete archive v1, current schema | Existing whole-database capture and offline replacement already retain all six tables. No restore algorithm/version change is necessary. Synthetic round trips verify metadata and existing screenshot guarantees. |
| Pre-Notebook schema archive | Recognized as requiring staged migration and rejected before replacing installed data. The separate older-archive upgrade path remains deferred. |
| Future schema or archive version | Rejected before replacement; existing target Notebook/data remain unchanged. |
| Future/unreadable Notebook content inside current schema | Retained byte-for-byte as stored text through export/archive/restore. A fresh service still reports UnsupportedContent/InvalidContent, not an empty editable note. |

Restore means the established **`.ptjbackup` workflow**, not importing `journal.json`. Archive preflight checks database integrity, FKs and exact schema; it does not rewrite or claim to semantically validate every Notebook document. Notebook-owned image bytes are **not yet captured/restored**. Tests intentionally seed ownership metadata without an owned image file; metadata recovery is not full image backup. Existing Trade screenshot bytes remain covered by their unchanged inventory. M22.8/M22.11 must integrate Notebook-owned file inventory, retention, staging/hash reconciliation, compensation and complete image round trips before claiming full Notebook portability.

### Regression evidence and verification

New synthetic tests use the existing archive/export fixtures and real SQLite. They compare every Notebook scalar to EF records, verify repeat-export ordering, and compare all 35 logical tables before/after offline restore. Fixtures include Active/Archived/Trashed notes, both restoration origins, folders/tags/pins, version increments, Trade/Setup links, both image ownership kinds, rich content, malformed/future payloads, existing Accounts/Instruments/Trades/executions/Journal and screenshot bytes. Restart uses a fresh DI provider and normal initialization.

Fault cases include cancellation before switch, failure after incoming installation, interrupted-switch recovery, and pre-Notebook/future-schema/future-archive rejection against a populated target. No ignored cleanup failures, retries, skips, relaxed assertions or deadline/concurrency changes.

Initial test-authoring corrections: compilation caught an incorrectly named preflight result property; the first executed focused run had **184 passed / 4 failed / zero skipped** (`TestResults/m223a/focused.trx`). All four new restart cases used the old archive fixture's execution-free Trade, which normal startup correctly rejected. Adding a valid synthetic execution to the new fixture fixes its stronger restart precondition; no production startup/restore behavior was changed.

Final commands (fresh Release binaries, synthetic databases only):

```powershell
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests -c Release --filter "FullyQualifiedName~Notebook|FullyQualifiedName~PortableExportTests|FullyQualifiedName~JournalRestoreTests|FullyQualifiedName~RestorePreflightTests" --logger "trx;LogFileName=focused-final.trx" --results-directory TestResults/m223a
dotnet build PersonalTradingJournal.sln -c Release --no-restore
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests -c Release --no-build --no-restore --logger "trx;LogFileName=infrastructure-all.trx" --results-directory TestResults/m223a
dotnet test tests/PersonalTradingJournal.Application.Tests -c Release --no-build --no-restore --logger "trx;LogFileName=application-all.trx" --results-directory TestResults/m223a
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
git diff --check
git diff --no-index --check -- /dev/null tests/PersonalTradingJournal.Infrastructure.Tests/Backups/NotebookPortabilityTests.cs
```

- Focused Notebook/export/preflight/restore: **188 passed, zero failures/skips**, 1m50s, `focused-final.trx`. Includes eight new portability cases and the unchanged completeness assertion.
- **Complete Infrastructure Release: 1,494 passed, zero failures/skips, exit 0**, 3m02s, `infrastructure-all.trx`. Run outside the restricted sandbox to provide the Windows symlink privilege needed by the existing backup-security test; no filters, test-setting changes or retries. This supersedes M22.3's one-failure Infrastructure result.
- Complete Application Release: **1,032 passed, zero failures/skips**, 2s, `application-all.trx`.
- Solution Release build: **0 warnings/errors**, 29.87s. EF reports **no pending model changes**, using the in-memory design-time factory.
- Tracked/new-file whitespace passed; PowerShell target-existence validation checked **172 local links, zero missing** across the three changed documents (not anchor validation).
- Final tree: **12 modified + 1 new file**, unchanged branch/HEAD, no commit/push/merge. The exact inventory follows.

The requested **M22.3A local checks are green**. The full multi-project Desktop Release gate, human interaction and matching corrected-source CI were not run or claimed for this bounded database correction. No production/provider access occurred. No full Notebook backup claim is made while owned image bytes remain deferred. **M22.4 onward remains pending**; M22.11 owns remaining integrated file portability and M22.12 full feature acceptance.

### Exact M22.3A changed-file inventory

Modified:

1. `README.md`
2. `docs/notebook.md`
3. `docs/backup-restore.md`
4. `src/PersonalTradingJournal.Application/Backups/IPortableExportService.cs`
5. `src/PersonalTradingJournal.Infrastructure/Backups/PortableExportSchema.cs`
6. `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/PortableExportTests.cs`
7. `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/SavedPeriodPortabilityTests.cs`
8. `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePortabilityTests.cs`
9. `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Setups/PlaybookPersistenceTests.cs`
10. `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/DailyTradingPrincipleTests.cs`
11. `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/SharedTradingPlanTests.cs`
12. `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/TradingPlans/TradingPlanPortabilityTests.cs`

New:

13. `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/NotebookPortabilityTests.cs`

## M22.1 — scope, UX contract and repository review

Design checkpoint, 20 October 2026. Inspected baseline: clean `develop`, `a279aaf28845e614b8ca94a2e533aaa52f45957c`. This document specifies subsequent implementation; it does **not** describe an already functional Notebook. Deliver one submilestone at a time. No entities, migrations, editor, seeded notes or application behavior change in M22.1.

The supplied *Trading journal notebook workspace.png* is visual direction for organization, note selection and a wide editor. Reuse the actual PTJ shell, centered shared section heading, navigation, dynamic theme resources and focus conventions; do not replace them with the mockup's branding/sidebar. Sample text, charts, counts and timestamps are illustrative and must never be seeded into a user's database.

### Ownership and agreed link scope

| Module | Owns |
| --- | --- |
| Journal | Date-associated observations and reflection |
| Trading Plan | Session preparation, intentions and scenarios |
| Trading Setup / Playbook | Existing pattern classification and its guidance |
| Notebook | Flexible knowledge, research and lessons spanning sessions |

Notebook is global, not Account/date-scoped. A link does not change target data, classify a Trade, prove adherence or supply automatic AI evidence.

**User clarification:** M22.9 is **Linked Trades & Trading Setups**. Separate Strategy support is deferred. The current model has no independent Playbook catalog identity: `CurrentPlaybookRecord` is owned by `TradingSetupId`; retained Playbook revisions use `(TradingSetupId, Revision)`. Link the underlying Trading Setup, labelled **Trading Setup**, not an invented Playbook ID. No Strategy catalog, Strategy-specific persistence, placeholder Strategy target kind or duplicate Setup catalog. Typed links can be extended by a future version when real target identities exist.

Initial scope: formatted notes, flat folders, tags, pins, search, managed images, these links, built-in templates, duplication, archive, trash, autosave and complete backup/restore. Deferred: nested folders, arbitrary attachments, custom templates, collaboration, sync, OCR, semantic search and AI features. Notebook must work offline and never enter review prompts automatically.

### Repository review and reuse map

Paths below are relative to `src/`; inspect their then-current implementations again at the relevant implementation slice.

| Existing implementation | Reuse / necessary extension |
| --- | --- |
| `PersonalTradingJournal.Desktop/Navigation/NavigationDestination.cs`, `ViewModels/MainWindowViewModel.cs` | Notebook destination/icon already exist, but fall through to `PlaceholderViewModel`. Replace that content mapping, not navigation taxonomy; integrate activation, departure and close guards. |
| `PersonalTradingJournal.Desktop/MainWindow.xaml`, `Resources/Controls.xaml`, `Typography.xaml`, `Spacing.xaml`, `Themes/LightTheme.xaml`, `Themes/DarkTheme.xaml` | Shared heading, surfaces, text, actions and focus treatments. Local Notebook templates may compose them; keep both themes and live theme switching. |
| `PersonalTradingJournal.Desktop/Views/Journals/InlineJournalView.xaml` | Existing journal fields are plain multiline TextBoxes, not a reusable rich-text editor. Searches of application source found no RichTextBox/FlowDocument editor. A bounded document adapter is new work, not an extension to Journal storage. |
| `PersonalTradingJournal.Desktop/ViewModels/TradingPlans/TradingPlanWorkspaceViewModel.cs` | Reuse owned asynchronous lifetimes and guarded navigation patterns. Cancel the owner before dependent cancellation can release continuations; drain tracked operations. Do not copy an unrelated Plan state machine. |
| `PersonalTradingJournal.Infrastructure/Persistence/JournalDbContext.cs`, `TradingPlans/DailyTradingPrincipleStore.cs` | Existing EF records/configurations, factory-created short-lived contexts, SQLite transactions, expected-version outcomes and bounded reads. Add Notebook mappings/use cases, not another database or long-lived editor DbContext. |
| `PersonalTradingJournal.Application/Common/Storage/IApplicationPaths.cs`, `PersonalTradingJournal.Infrastructure/Storage/LocalApplicationPaths.cs` | Managed database, screenshots, logs and backups paths. Resolve injected paths; tests use isolated roots, never the production default. Credentials remain outside portable data. |
| `PersonalTradingJournal.Infrastructure/Screenshots/LocalTradeScreenshotFileStorage.cs`, `ScreenshotCaptureLease.cs`, `RetainedScreenshotFileStorage.cs` | Safe storage keys, staging and capture/deletion coordination. Existing retention union covers Trade, Playbook and Plan assets, **not Notebook**; extend it before note images can be removed safely. Do not fabricate a Trade to own a note image. |
| `PersonalTradingJournal.Desktop/Screenshots/PlaybookExampleImages.cs` | Bounded byte reading, signatures, single-frame/dimension validation and 384-pixel thumbnails are the relevant reusable image policy. The generic Trade decoder loads full images and is not sufficient for Notebook list/inline preview admission. |
| `PersonalTradingJournal.Infrastructure/Backups/BackupArchiveService.cs`, `PlaybookImageInventory.cs`, `RestorePreflightService.cs`, `JournalRestoreService.cs` | Existing capture lease, immutable SQLite snapshot, file inventory/hash reconciliation, preflight and recovery. Extend image inventory, metadata checks and retention to Notebook, including Archived/Trashed notes. A database table alone does not include its images. |
| `PersonalTradingJournal.Infrastructure/Backups/PortableExportSchema.cs` | Current logical JSON export v14 explicitly enumerates tables. Advance its version when Notebook persistence ships; never silently omit notes/content/metadata. Separate Notebook import/export remains deferred. |

Related conventions: [architecture](architecture.md), [backup and restore](backup-restore.md), [Trading Plan lifetime/guards](trading-plan.md), [WPF verification](ci-wpf-tests.md).

### Workspace, commands and navigation

- Organization: All Notes, Pinned, named flat folders, Unfiled, Archived, Trash and tag filters. Normal views include Active notes only. Archived and Trash are explicit, searchable scopes.
- Note list: title, short plain-text preview, last-updated time, tags and pin indicator; stable selection by note ID. Default Recently Updated descending, then stable ID; alternative normalized Title ascending, then stable ID. No automatic pin-first reorder inside All Notes.
- Editor: separate title, selection-aware formatting toolbar, body, image captions, typed links and save status. Saved metadata is secondary; never expose JSON as the normal editor.
- New note creates a persisted empty **Untitled** note in the selected folder, otherwise Unfiled; active state even when launched from Archived/Trash. Focus title after creation succeeds. Failed creation retains input and cannot claim a persisted identity. Blank titles fail validation; body may be empty.
- Empty workspace explains Notebook and offers Create note. No fabricated research/trades or templates inserted until an explicit user action.
- On wide layouts show three panels; at narrow widths collapse organization and switch between list/editor with explicit Back/Notes controls. Retain selected identity, filters, caret, scroll and unsaved content; reflow alone cannot save or discard.
- Commands: create/load/edit, move, pin/unpin, duplicate, archive/unarchive, trash/restore/permanent delete; folder and tag management; link/unlink/open; add/remove/view image; Retry save and explicit discard. Trash is read-only until restored; Archived notes are readable and can be unarchived to edit.
- Keyboard: native Tab/Shift+Tab, lists and toolbar navigation; scoped Ctrl+N New, Ctrl+F Search and Ctrl+S flush save without stealing editor undo/redo. Accessible labels, selection/expanded state and restrained live save/error announcements. Do not move focus merely to announce Saving/Saved.
- Note switch, linked-target navigation and owner-window closing must first settle dirty content. Respect other destinations' vetoes; do not destroy/discard the editor before all relevant guards accept. Search/filter exclusion of a dirty selected note uses the same guard.

### Content format decision

Use **Notebook document JSON v1**, a bounded typed tree, not XAML, RTF or HTML as persistent truth. Domain/Application contracts contain only data. A Desktop adapter maps allowlisted nodes to WPF presentation/editor objects; those objects never cross persistence boundaries.

Envelope: `format: "ptj.notebook.document"`, integer `version: 1`, ordered `blocks`. Nodes have stable document-local IDs to preserve editing/image identity. Supported blocks:

- Paragraph and heading (levels 1–3), with inline text runs and explicit line breaks.
- Runs with bold/italic/underline marks; hyperlink with displayed runs and an absolute HTTP/HTTPS URL.
- Ordered/unordered lists and checklist items with explicit checked state. List nesting is bounded.
- Image block at its position in the text flow, referring to a note attachment ID plus optional caption/alternative text; no embedded bytes, source filename or arbitrary filesystem path.

No arbitrary runtime type names, scripts, bindings, event handlers, unrestricted XAML loading, external images or automatic URL fetching. Open a validated HTTP/HTTPS link only on explicit activation; no file/data/javascript schemes. Clipboard access occurs only on explicit paste. A restricted parser may retain supported clipboard formatting; unsupported styling is normalized to safe plain text with feedback. Never execute/deserialise clipboard objects.

The WPF implementation should use a restricted RichTextBox/FlowDocument adapter where appropriate, with explicit checklist/image handling. **Control feasibility, undo/redo and accessible interactive checklists still require M22.5 tests**; choosing the portable format does not claim an editor already works. Autosave serializes the current model without rebuilding the editor document.

Persist formatted content and a deterministic plain-text projection in the same transaction with metadata/version. The projection includes visible body text, checklist/list text and image captions, not linked-record contents, OCR or hidden formatting. Search title separately. Never trust a caller-supplied stale projection.

Unknown versions, malformed JSON or invalid nodes yield an unavailable-content state retaining exact original stored bytes/text. No automatic conversion to an empty document, autosave or overwrite. Offer safe metadata/read-only recovery guidance; future repair/export actions must be explicit. Round-trip support and schema upgrades require fixtures before enabling a new format.

### Initial bounds and normalization

These are the M22 implementation contract. M22.2 enforces the domain/query bounds described in its implementation section below; serialized-byte and image-decoding limits still require their later adapters. Reject invalid/oversized changes with retained input and an actionable error; never silently truncate. Counts of text below use .NET UTF-16 code units; reject malformed Unicode.

| Value | Limit / rule |
| --- | --- |
| Title | Required after trimming, 200 characters; default Untitled |
| Folder / tag display name | Required, respectively 128 / 64 characters |
| Name uniqueness | Trim and Unicode Form C; ordinal case-insensitive within folders or tags separately. Preserve normalized display casing; rename collision returns validation, no implicit merge. |
| Document | 1 MiB serialized UTF-8; 200,000 projected body characters; 4,096 total nodes; depth 16, list nesting at most 3 |
| URL | 2,048 characters, allowed absolute schemes above |
| Per-note assignments | 32 distinct tags; 100 distinct typed links; 32 managed images |
| Caption / alternative text | Optional, at most 2,000 / 500 characters |
| Image | Nonempty, at most 8 MiB, PNG/JPEG/WebP with installed decoder support; validated signature and single frame; each dimension at most 8,192, total at most 16,000,000 pixels |
| Image preview | Decode to display size; list thumbnail longest edge at most 384 pixels. Never decode every original in list rows. Full-size opens only on request. |
| Search / page | Search at most 256 characters; default page 50, accepted 1–100; stable cursor with lookahead, never silently truncate a complete result into one page |

Image bounds reuse the established Playbook admission policy, including single-frame WebP only when decoding succeeds. New Notebook images cannot exploit legacy zero-byte Trade screenshot admission. Existing screenshots/data remain unchanged. Stream byte bounds and cancellation apply before decode; bounded concurrent decoding is required. File bytes remain original; clipboard bitmap encoding is a newly created image and must pass the same admission policy.

### Identity, lifecycle and persistence contracts

Stable GUIDs for notes/folders/tags/attachments; typed target IDs for links; UTC audit timestamps from injected TimeProvider. Display positions/names are never identities. One nullable folder reference means Unfiled; duplicate `(NoteId, TagId)` and `(NoteId, TargetKind, TargetId)` are rejected.

Expected note version covers content and metadata mutations. Saves are atomic; stale writes return Conflict and retain local content rather than overwriting a newer version. Success requires confirmed committed state/version, not merely queued work. Folder/tag rename/delete also recheck their expected state transactionally. No per-keystroke revision archive or mandatory immutable note history in M22; the last persisted note is the recovery baseline.

Active, Archived and Trashed are distinct. Pinning is metadata, not a lifecycle state. Trash stores the prior Active/Archived state and folder reference. Restore returns there if available, otherwise Unfiled; pin/tag/link/image content survives. Deleting a folder moves its notes to Unfiled, including archived/trashed references; deleting a tag removes only assignments. Permanent deletion is available only in Trash after explicit confirmation and transactional ownership recheck. No automatic expiry.

Duplicate creates an Active note with independent identity/version/audit, copies content/tags/links, and starts unpinned; it does not mutate the source. Use new attachment identities and independent managed file copies for note-owned images, so deleting either note cannot break the other. Validate and stage all copies before publishing success; compensate failed staging without deleting referenced assets.

### Autosave and operation ownership

Target a 750 ms quiet-period debounce using injectable time, with explicit Ctrl+S/navigation flush. Serialize writes per note; each operation captures note ID, expected version and edit generation. Edits arriving during a write remain dirty and schedule the next save. A completion can acknowledge only its captured generation, never newer typing or another selected note.

Saving…, Saved and Could not save describe real persisted state. A failed/conflicting save keeps every field/image selection and offers Retry, Remain or explicit Discard to the last persisted state. Discard must not delete an already saved note. Never automatically retry a conflict or navigate through a failure.

Cancel the context owner before dependencies, drain tracked loads/saves and check operation identity before publishing. Cancellation cannot retract a committed write: reconcile that result before reporting Saved/Cancelled. Repeated cancellation, disposal, rapid selection and activation retry require controlled-interleaving tests. A crash may lose edits still inside the debounce; promise recovery of **last successful persistence**, not uncommitted keystrokes. No separate volatile working-file exclusion for saved Notebook drafts.

### Filtering, counts and linked records

Text search covers title and saved plain-text body; text, folder and all selected tags combine with AND. Dedicated Archived/Trash searches stay inside their lifecycle scope. Sidebar scope counts represent that scope's lifecycle population before text/tag filters; list count represents all applied filters. Read counts/list consistently; label both clearly. Debounce search, cancel superseded work and suppress stale results. Bounded pages keep all results reachable.

M22.9 pickers resolve existing **Trades** and **Trading Setups** only. Persist `(kind, targetId)` plus last-known display label for unavailable presentation, not a copy of target-owned content. Current names are resolved in bounded batches, not a query per row; renamed targets do not change link identity. Deleted/unavailable targets retain the note and a clearly marked unavailable chip; never attach a same-name replacement. Open existing details through guarded navigation. Opening a Setup may use its Playbook destination when available; it remains a Setup link, not a promise of frozen Playbook guidance.

Removing a link never deletes a target or its screenshots. A displayed Trade screenshot reference remains Trade-owned; if an independent image is wanted in the note, import an explicitly owned copy. Do not add Strategy-specific persistence or an unused discriminator. Future real target types require explicit contract/reader support.

### Images, backup and compatibility

Store note-owned images through managed screenshot storage keys and a Notebook attachment ledger, not caller paths. Retain originals and metadata (size, hash, type, ordering and caption). Missing/unreadable images show placeholders while preserving text and references; do not silently remove them on load.

Extend the existing capture lease and reference checks to include all retained notes. Image removal commits metadata first, then cleanup rechecks shared ownership; import stages bytes before metadata references become visible. Failed compensation is reported/retained for safe cleanup rather than deleting uncertain references. Trade, Playbook and Plan assets retain their existing protections.

Notebook persistence requires additive migration(s), no seed notes and no modification of existing economics/classifications. Complete backups include note content, folders/tags, links, all lifecycle states and note-owned binaries. Preflight checks storage/content versions, required files, sizes/hashes and metadata consistency; restore must preserve recovery protection and installed data on failure.

At this baseline archive format is v1, logical JSON export v14. Installed databases migrate additively; **older-schema archive restore remains unsupported** until a separate staged-upgrade path is implemented. A pre-Notebook archive must not be presented as restorable after a Notebook schema change merely because its archive format is still v1. Advance logical export when tables are added, preserving formatted JSON and metadata; logical export is not a restorable backup and does not embed image binaries. Standalone Notebook import/export is deferred. Backups/exports contain sensitive unencrypted notes; credentials remain excluded.

Portability hooks must accompany the owning storage changes; M22.11 is their integrated acceptance, not permission to silently omit newly persisted data until then.

### Delivery and verification

| Slice | Deliverable / evidence required | Status |
| --- | --- | --- |
| M22.1 | Repository map, UX/content/limits/ownership contract | Documented here |
| M22.2 | Domain/Application invariants, lifecycle and explicit outcomes | Implemented and locally verified below |
| M22.3 | EF/additive migration, bounded reads, safe codec, transactional projection and concurrency | Implemented; historical verification below |
| M22.3A | Logical export completeness and current-schema database round-trip compatibility | Correction and verification above; owned image files still deferred |
| M22.4 | Shell workspace, folders, responsive navigation and guards | Implemented; verification above |
| M22.5 | Rich-text adapter, protected image references, safe paste, manual Save and draft guards | Implemented; automated verification and live/CI limits above |
| M22.6 | Deterministic autosave, failure, stale completion, cancellation and restart tests | Pending |
| M22.7 | Tags, AND filters, stable paging, counts and stale-search tests | Implemented; complete local Release gate 5,131/5,131; human/native restart and matching CI unverified above |
| M22.8 | Image admission, ownership, compensation, missing files and retention | Pending |
| M22.9 | **Linked Trades & Trading Setups**, current labels/unavailable targets and guarded opening | E local automated Release acceptance passed: 5,267/5,267; hosted/interactive/native-restart limits remain |
| M22.10 | Blank Note, Market Study, Trading Idea, Lesson Learned, Psychology templates; duplicate/archive/trash/restore/delete | A/B implemented; C local automated Release acceptance complete, 5,339/5,339; CI-budget risk remains |
| M22.11 | Consistent complete backup/preflight/restore round trips and failure recovery | Pending |
| M22.12 | Integrated synthetic workflows, full Release gate, automated renders and separate live acceptance | Pending |

Templates supply editable headings only: Market Study (context/observation/examples/conclusion/open questions), Trading Idea (idea/conditions/invalidation/evidence to collect), Lesson Learned (situation/observation/lesson/next action), Psychology (trigger/thoughts/behavior/alternative response). No invented outcomes or automatic links.

Implementation verification must cover Light/Dark, normal and narrow/240-DPI layouts, keyboard focus, accessible names/states, long content and missing-content recovery. Distinguish bound-WPF/render evidence from actual input and screen-reader acceptance. Use isolated synthetic databases/images and fake dependencies only. Preserve existing assertions, deadlines and concurrency coverage.

M22.1 changed-file inventory: **`README.md`, `docs/notebook.md` only**. Read-only repository inspection confirmed the existing placeholder, plain-text Journal controls, Setup-owned Playbook identities, image bounds/retention and explicit backup inventory. No `AGENTS.md` was found in the repository or checked ancestor chain. Verification: `git diff --check` and `git diff --no-index --check -- /dev/null docs/notebook.md` passed; a PowerShell local-link target check across both changed documents checked **157 targets, zero missing** (target existence, not anchor validation). No build, EF check, test suite, render, live UI or matching CI run is claimed for this documentation-only slice. The prior 4,935-test Release result remains historical and is not Notebook acceptance. Run focused checks as implementation arrives and the established `pwsh -File scripts/Test-Release.ps1 -ResultsDirectory TestResults` gate at integrated completion, with Release build, EF consistency and whitespace checks.

## M22.2 — Domain Model and Application Contracts

20 October 2026. Actual starting baseline: **clean `develop` at `ab9097c008755acf512c91c1e2b6b9f7cf3b36e2`**. M22.1 documentation was already committed; its scope decisions are preserved. Repository/ancestor instruction discovery found no `AGENTS.md`. Only the Domain/Application foundation and focused tests are implemented in this slice.

### Implemented boundaries and invariants

- `NotebookNote`, `NotebookFolder` and `NotebookTag` reuse `AuditableEntity`: nonempty stable GUIDs, zero-offset UTC timestamps, monotonic updates and reconstruction validation. Their positive versions start at one and advance on mutations; overflow or invalid audit input cannot partially apply an edit. The future store compares expected versions transactionally.
- A new note is Active, unpinned, empty and **Untitled**, with either a valid folder ID or null (Unfiled). Required titles trim and retain text/line breaks; folder/tag names use trim + Unicode Form C with ordinal-ignore-case name comparers. Invalid Unicode/NUL and documented length bounds are rejected. Cross-entity name uniqueness and folder/tag existence remain transactional application responsibilities.
- Note collections are bounded defensive read-only copies. Tags reject duplicate/empty IDs; links reject duplicate `(kind, targetId)` even when labels differ. Link kinds are exactly `Trade` and `TradingSetup`; a same GUID in different target kinds is not the same link. No Strategy or independent Playbook kind was introduced.
- Active-only editing covers title, document/images, folder, pin, tags and links. Archive/unarchive and trash/restore are explicit transitions. Trash preserves prior Active/Archived state and folder, plus content, pin and assignments. Restore takes folder availability as application-supplied transactional information and falls back to Unfiled; no database lookup occurs in the domain.
- Folder/tag deletion maintenance is explicitly different from editing read-only content: `ApplyFolderDeletion` and `ApplyTagDeletion` update references in all lifecycle states and advance the affected note version. A deleted folder is cleared from both current and restoration metadata. Ordinary editing of Archived/Trashed notes remains prohibited.
- `NotebookDocument` / `NotebookNode` represent the agreed v1 tree: paragraph, heading, run marks, line break, HTTP/HTTPS hyperlink, ordered/unordered lists, interactive checklist data and positioned images. Factories prevent irrelevant attributes; document construction validates parent/child shape, unique node IDs, total nodes/depth/list nesting, image references and total projected text. Its immutable search projection is derived from visible text/captions, with LF after paragraphs/headings/images and explicit line breaks; URLs, alt text, target content and image bytes are excluded. No caller can supply a different projection.
- `NotebookImage` distinguishes **NoteOwned** (managed key, nonzero bounded byte size and SHA-256) from **TradeScreenshotReference** (exact Trade and screenshot IDs, no owned key/hash/size). Metadata has ordered attachment identity, optional caption and alternative text. The document and ordered image list must match one-to-one, including captions/alt text, before an atomic aggregate content edit succeeds. Removing an image/reference has no file side effects.
- Link fallback labels are capped at 2,000 characters to bound unavailable-target metadata; they are labels, not frozen Trade/Setup content. Opaque pagination cursors are capped at 2,048 characters. These transport metadata bounds do not change authored title/body limits.

Application ports and DTOs:

- `INotebookService`: load, consistent list/summary, bounded folder/tag continuation, note commands, catalog commands and batched link resolution. Read cancellation/storage exceptions follow existing ports; writes carry explicit sanitized Invalid, Conflict, NotFound, UnavailableTarget, UnsupportedContent, TooLarge, Cancelled or StorageFailure outcomes.
- `NotebookCommands.cs`: create/load-related workflows, title/content update, move, pin, tag assignment/removal, link/unlink, archive/unarchive, trash/restore, confirmed permanent delete, duplicate and image metadata add/update/remove. Catalog commands cover folder/tag create/rename/delete. Every existing-entity mutation carries an expected version. Transport command payload validation belongs to later handlers; domain types enforce aggregate invariants now.
- Image commands include the complete resulting document and ordered metadata, not an isolated caption mutation that could desynchronize search text. The later handler verifies the operation's delta, managed file admission and ownership before committing.
- Duplication requires independent managed copies/new attachment identities and a new Active/unpinned note; permanent deletion requires confirmed Trash state and atomic reference/retention recheck. These are **orchestration contracts**, not filesystem operations or a completed duplicate/delete workflow.
- `NotebookQuery` specifies lifecycle scope, exact folder, bounded search, all-tags-required filtering, sort and 1–100 page size. Folder scope requires its ID; other scopes reject one instead of silently changing scope. Recently Updated uses descending timestamp and ascending ID; Title uses normalized ordinal-ignore-case ordering and ascending ID. Cursors must be bound to the query and derived from the last displayed row. Scope counts exclude text/tag filtering; filtered total and note rows share the same read. Folder/tag catalog pages also remain bounded, with continuations.
- `NotebookNoteSnapshot` copies aggregate collections into immutable arrays, so later aggregate edits cannot rewrite a previously returned snapshot. Load outcomes distinguish missing, malformed, unsupported and oversized content without substituting an empty editable note.
- `INotebookContentCodec` defines the **future** safe JSON boundary: exact format/version, 1 MiB UTF-8 limit, allowlisted nodes and projection derived from the validated document. No parser/serializer implementation or editor serialization was added. Exact serialized byte size cannot be measured by this typed-tree layer; codec/store enforcement is required in M22.3. Image signatures, dimensions, decoder support and original bytes likewise require M22.8 admission; metadata validation is not proof of decoded image safety.

### Verification and limits

All tests use synthetic in-memory objects; no database/file/provider operations were introduced. Final commands and results:

```powershell
dotnet test tests/PersonalTradingJournal.Domain.Tests/PersonalTradingJournal.Domain.Tests.csproj -c Release --filter FullyQualifiedName~Notebook --logger trx --results-directory TestResults/m222-focused-domain
dotnet test tests/PersonalTradingJournal.Application.Tests/PersonalTradingJournal.Application.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~Notebook --logger trx --results-directory TestResults/m222-focused-application-final
dotnet test tests/PersonalTradingJournal.Domain.Tests/PersonalTradingJournal.Domain.Tests.csproj -c Release --no-restore --logger trx --results-directory TestResults/m222-domain-all
dotnet test tests/PersonalTradingJournal.Application.Tests/PersonalTradingJournal.Application.Tests.csproj -c Release --no-restore --logger trx --results-directory TestResults/m222-application-all
dotnet build PersonalTradingJournal.sln -c Release --no-restore
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
git diff --check
```

- Focused: **34 Domain + 11 Application = 45 passed**, zero failures/skips.
- Complete affected-layer Release suites: **601 Domain + 1,032 Application = 1,633 passed**, zero failures/skips. TRX files are under the respective directories above.
- Final Release solution build: **0 warnings / 0 errors**, 25.57 seconds.
- EF: **no pending model changes**, using the repository's `:memory:` design-time factory. The first EF invocation incorrectly specified Desktop as startup and failed because Desktop does not reference EF Design. Correcting the command to the existing Infrastructure design-time factory passed; no dependency/package/schema change was made to accommodate the command.
- The first focused Application run passed 10 cases; the final run includes the additional bounded catalog-query case. No failing test attempt or deadline/assertion change occurred.
- `git diff --check` plus `git diff --no-index --check -- /dev/null <new-file>` for all eleven new files passed. PowerShell checked **158 local documentation targets, zero missing** across README and this document (target existence, not anchor validation). Final working tree: two modified documentation files and eleven new source/test files; branch/HEAD unchanged, no commit/push/merge.

Coverage includes malformed/boundary input, normalization, audit/version atomicity, lifecycle/read-only transitions, restoration to existing/missing folders, maintenance of trashed references, duplicate/limit enforcement, immutable collections/snapshots, typed links, safe URL schemes, content structure/projection, image metadata ownership and exact document/attachment reconciliation. Contract-only ports cannot yet demonstrate transactional rollback, concurrent database writes, file compensation, restart or actual cancellation draining; those tests belong to the implementing slices.

No unresolved scope conflict: separate Strategy support remains deferred and Playbook links use actual Trading Setup identity. **M22.3–M22.12 remain pending.** No migration, persistence handler/DI wiring, filesystem work, editor/UI, autosave, export/backup contract change or AI evidence integration. No complete Desktop/Infrastructure Release gate or live/render/CI acceptance claimed at this foundation checkpoint; integrated full-gate acceptance remains M22.12.

### Exact M22.2 changed-file inventory

Two existing documentation files modified; eleven new source/test files:

1. `README.md`
2. `docs/notebook.md`
3. `src/PersonalTradingJournal.Domain/Notebook/NotebookLimits.cs`
4. `src/PersonalTradingJournal.Domain/Notebook/NotebookCatalog.cs`
5. `src/PersonalTradingJournal.Domain/Notebook/NotebookReferences.cs`
6. `src/PersonalTradingJournal.Domain/Notebook/NotebookDocument.cs`
7. `src/PersonalTradingJournal.Domain/Notebook/NotebookNote.cs`
8. `src/PersonalTradingJournal.Application/Notebook/NotebookQueries.cs`
9. `src/PersonalTradingJournal.Application/Notebook/NotebookCommands.cs`
10. `src/PersonalTradingJournal.Application/Notebook/INotebookService.cs`
11. `tests/PersonalTradingJournal.Domain.Tests/Notebook/NotebookNoteTests.cs`
12. `tests/PersonalTradingJournal.Domain.Tests/Notebook/NotebookDocumentTests.cs`
13. `tests/PersonalTradingJournal.Application.Tests/Notebook/NotebookContractTests.cs`

## M22.3 — persistence, migrations and content format

20 October 2026. Starting baseline: **clean `develop` at `66243544eae985b58f68a104f54f7a94f68a81cc`**. M22.1/M22.2 and the preceding Trading Plan corrections are preserved. No branch switch, commit, production database, network/provider request or Desktop change.

### Storage and content decisions

- Additive migration **`20261020085611_AddNotebook`**, generated through EF, creates six tables: `NotebookNotes`, `NotebookFolders`, `NotebookTags`, `NotebookTagAssignments`, `NotebookLinks`, `NotebookImages`. No backfill, seed notes or changes to existing financial records. Audits use the existing UTC converter; persisted versions are concurrency tokens.
- The existing Domain `NotebookNote.Rehydrate` remains authoritative. Separate records contain document JSON/version, derived plain/search text, title, pin, lifecycle/restoration state, folder, tags, ordered images, typed links and audit/version fields. Loading validates the complete aggregate and projection; it does not reset identity/timestamps or manufacture content.
- Folder/tag names use Domain trim + Form C normalization and ordinal-ignore-case identity. Persisted keys are invariant-uppercase UTF-16 big-endian hex with SQLite BINARY ordering, not SQLite's ASCII-only NOCASE. Unique indexes enforce those keys. Note-title sort keys normalize for ordering without rewriting saved titles. Storage does not require custom SQLite collations.
- Restrictive FKs prevent catalog/child cascades. Folder deletion atomically moves all lifecycle/restoration references to Unfiled; tag deletion removes assignments, never notes. Both advance affected note versions/timestamps. Linked Trade/Trading Setup identities and Trade-screenshot references deliberately have no target cascade: missing sources remain identifiable by their original IDs/last-known labels.
- JSON v1 has exact `format`, `version`, `blocks` properties. Every node has `id` and an allowlisted `kind`: `text`, `lineBreak`, `paragraph`, `heading`, `hyperlink`, `bulletedList`, `numberedList`, `checklist`, `listItem`, `checklistItem`, `image`. Type-specific properties preserve marks, heading levels, ordered children, checkbox state, safe URLs, attachment IDs, captions and alternative text. Unknown/duplicate/missing properties, unsupported nodes, unsafe values and invalid shapes fail explicitly. No WPF objects, XAML, arbitrary types, HTML execution or editor serialization.
- Codec checks strict UTF-8 and **1 MiB serialized content**, then the Domain's **200,000 text characters / 4,096 nodes / depth 16 / list depth 3** constraints. Encoding also checks the actual escaped JSON byte count. `PlainText` comes only from the validated tree, not a supplied projection.
- Unsupported version, malformed content and oversized content remain distinct load outcomes. No empty editable document is substituted. Bounded original JSON is returned for recovery; oversized original stays in storage. Minimal contract additions: optional `NotebookUnavailableMetadata` (identity/title/version/state) and an appended `Deferred` write result. Ordinary writes to unreadable notes are blocked rather than fabricating a Domain object. Unrelated catalog maintenance can clear folder/tag references without decoding or rewriting that payload.

### Wired operations and transaction ownership

DI registers `INotebookContentCodec` → `NotebookJsonCodec` and `INotebookService` → `NotebookService`. Each operation owns a short-lived factory-created context and SQLite connection.

Implemented database-only commands: create; title and text/document editing with unchanged image metadata; move; pin/unpin; assign/remove tag; link/unlink existing **Trade / Trading Setup**; archive/unarchive; trash/restore; create/rename/confirmed-delete folders and tags. No Strategy link/catalog is introduced.

Writes reserve SQLite's writer before reading/checking the expected version and references. Domain validation, metadata/content/projection changes, child assignments and verified readback share that transaction. Stale writes return Conflict without mutation. Invalid/missing/unavailable/unsupported/too-large/storage outcomes are explicit and sanitized. Cancellation before the final commit rolls everything back; the final cancellation check is followed by a non-cancellable commit. A cancellation arriving after commit returns Success with the verified snapshot. There is no shared editor context, retry policy or autosave scheduler.

Reads use a deferred SQLite snapshot transaction. Active/Pinned/folder/Unfiled exclude Archived/Trash; search combines with **all selected tags**. Recently Updated is descending, Title is normalized ordinal-ignore-case ascending, each with stable ID tie-breaking. Page sizes remain 1–100; opaque cursors bind filter/sort and advance from the last displayed row, not lookahead. Scalar list projections omit full JSON and bound previews to 180 characters. Sidebar counts exclude text/tag filters and share the same snapshot as filtered totals/rows. Initial folder/tag pages are bounded to 50, with separate bounded continuations. Link resolution is a bounded batch, not per-note target queries; same-name replacements never retarget an old link.

**Explicitly deferred:** all duplicate/permanent-delete/image commands return `Deferred` without writes, including currently image-free duplicates/deletes. This avoids metadata-only shortcuts before file ownership, copy/cleanup compensation and retention orchestration are implemented. `UpdateNotebookContent` also rejects image-set changes through this route. Image metadata round trips are tested using synthetic records; no file import, decoding, deletion, thumbnail generation or retention-union implementation is claimed. Cross-resource work belongs to the later image/lifecycle slices.

### Portability and compatibility boundary

Historical M22.3 result (superseded by M22.3A above): per that slice's instruction, archive format v1 and logical export v14 were unchanged. SQLite snapshots naturally included tables, but that alone did **not** establish complete Notebook backup/preflight/restore or owned-image inclusion. The logical export omitted the six new tables and its unchanged all-logical-columns assertion detected the gap. M22.3A brings the database/export correction forward rather than leaving this failure until M22.11. Older-schema archive restore remains unsupported by the existing exact-schema preflight; a pre-Notebook archive is not automatically compatible with the migration.

No Notebook Desktop workspace, editor adapter, autosave, image-file workflow, template workflow, complete portability, live accessibility or matching CI acceptance is claimed. **M22.4 onward remains pending**; M22.12 owns integrated acceptance.

### Verification

Synthetic temporary SQLite databases only, with independent contexts/connections, controlled interceptors and bounded task gates. Coverage includes every supported node, Unicode and byte bounds, strict malformed/future payload handling, audit/lifecycle/restore, uniqueness and relational constraints, queries/tags/cursors/counts, unavailable links, unchanged image ownership, stale concurrent writers, cancellation/rollback before commit, cancellation after commit, DI restart reads and upgrade preservation of Journal/Trades/Setups/screenshots/Playbook/Rule records.

Commands used (all Release; logs in `TestResults/m223`):

```powershell
dotnet ef migrations add AddNotebook --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests -c Release --filter "FullyQualifiedName~Notebook|FullyQualifiedName~InitialMigrationTests" --logger "trx;LogFileName=notebook-focused-final.trx" --results-directory TestResults/m223
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests -c Release --no-restore --filter "FullyQualifiedName~Notebook|FullyQualifiedName~InitialMigrationTests|FullyQualifiedName~JournalDatabaseInitializerTests|FullyQualifiedName~SqlitePersistenceIntegrityTests|FullyQualifiedName~ProductionPersistenceIntegrationTests|FullyQualifiedName~TradingRulePersistenceTests" --logger "trx;LogFileName=notebook-persistence-final.trx" --results-directory TestResults/m223
dotnet test tests/PersonalTradingJournal.Domain.Tests -c Release --no-build --no-restore --logger "trx;LogFileName=domain-all.trx" --results-directory TestResults/m223
dotnet test tests/PersonalTradingJournal.Application.Tests -c Release --no-build --no-restore --logger "trx;LogFileName=application-all.trx" --results-directory TestResults/m223
dotnet test tests/PersonalTradingJournal.Infrastructure.Tests -c Release --no-build --no-restore --logger "trx;LogFileName=infrastructure-all.trx" --results-directory TestResults/m223
dotnet build PersonalTradingJournal.sln -c Release --no-restore
dotnet ef migrations has-pending-model-changes --project src/PersonalTradingJournal.Infrastructure --configuration Release --no-build
git diff --check
```

Implementation-test chronology: the first focused run was **25 passed / 1 failed** (an immutable-array identity assertion, corrected to sequence comparison). The expanded run was **26 passed / 4 failed**: EF could not translate ordering over the positional catalog projection; member-initialized projections fixed translation while keeping paging in SQL. Subsequent focused runs passed **42**, then **45**, then **46** tests. The first complete Infrastructure run was **1,477 passed / 9 failed / zero skipped** (3m21s): seven pre-Notebook schema/model inventory expectations, one unchanged export-completeness failure (35 model tables versus 29 exported tables), and one Windows symbolic-link privilege failure. Schema inventory checks were extended explicitly for the migration; no functional assertion/deadline was removed. Final verification results are recorded below.

Final local results:

- Expanded affected persistence regressions: **109 passed / zero failed or skipped**, 34s (`notebook-persistence-final.trx`). Includes **34 new Notebook codec/SQLite checks**, the approved-schema inventory and existing initialization/integrity/Rules/graph regressions.
- Complete Domain **601/601** and Application **1,032/1,032**, zero failures/skips.
- Complete Infrastructure rerun, same entire project, freshly built binaries, outside the sandbox to permit the existing symlink-security check: **1,485 passed / 1 failed / zero skipped**, exit 1, **3m18s**, `infrastructure-all-final.trx`. Command identical to the complete Infrastructure command above except `LogFileName=infrastructure-all-final.trx`. The symbolic-link test passed; the sole remaining failure is `PortableExportTests.AllLogicalColumnsAreExplicitlyCoveredExceptRebuildableBrowseCache`: **35 model tables / 29 export tables**. This is the six-table Notebook portability gap deliberately deferred by the M22.3 request. The assertion, backup/export code and formats remain unchanged; integrated regression acceptance is **not green**.
- Final `dotnet build PersonalTradingJournal.sln -c Release --no-restore`: **zero warnings/errors**, 8.04s. EF: **no pending model changes**, in-memory design-time factory, no production connection.
- Tracked and all 14 new-file whitespace checks passed. README/Notebook documentation target-existence checks: **159 local targets, zero missing** (not anchor validation).
- Final branch/HEAD unchanged; **11 existing files modified + 14 new files**, all uncommitted. Complete Desktop/parallel Release gate, renders, human interaction and matching corrected-source CI were not run/claimed for this persistence-only slice. No retries/skips/deadline changes were added to tests.

### Exact M22.3 changed-file inventory

Existing files:

1. `README.md`
2. `docs/notebook.md`
3. `src/PersonalTradingJournal.Application/Notebook/INotebookService.cs`
4. `src/PersonalTradingJournal.Infrastructure/Persistence/JournalDbContext.cs`
5. `src/PersonalTradingJournal.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs`
6. `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/JournalDbContextModelSnapshot.cs`
7. `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Initialization/JournalDatabaseInitializerTests.cs`
8. `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integration/ProductionPersistenceIntegrationTests.cs`
9. `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Integrity/SqlitePersistenceIntegrityTests.cs`
10. `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Migrations/InitialMigrationTests.cs`
11. `tests/PersonalTradingJournal.Infrastructure.Tests/Persistence/Rules/TradingRulePersistenceTests.cs`

New files:

12. `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookJsonCodec.cs`
13. `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookMapper.cs`
14. `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.cs`
15. `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.Catalog.cs`
16. `src/PersonalTradingJournal.Infrastructure/Notebook/NotebookService.Queries.cs`
17. `src/PersonalTradingJournal.Infrastructure/Persistence/Configurations/NotebookConfiguration.cs`
18. `src/PersonalTradingJournal.Infrastructure/Persistence/Records/NotebookRecords.cs`
19. `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261020085611_AddNotebook.cs`
20. `src/PersonalTradingJournal.Infrastructure/Persistence/Migrations/20261020085611_AddNotebook.Designer.cs`
21. `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookCodecTests.cs`
22. `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookFixture.cs`
23. `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookPersistenceTests.cs`
24. `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookQueryTests.cs`
25. `tests/PersonalTradingJournal.Infrastructure.Tests/Notebook/NotebookMigrationTests.cs`
