# Application Branding

## N26 Emerald Bull

Implemented on 1 November 2026 in the existing checkout, starting from clean commit `1fe01fc`. The supplied branding-package README and patch were reference material; its branch-creation instructions were not followed. The selected artwork is copied unchanged, including its lettering and transparency.

| Asset | Properties | SHA-256 |
| --- | --- | --- |
| [Sidebar PNG](../src/PersonalTradingJournal.Desktop/Resources/Branding/ptj-logo-n26.png) | 1254 × 1254, 8-bit RGBA | `52511600342FB22C4C6282055899BC532B3B781D30AE569621415E83E63CC4EE` |
| [Windows ICO](../src/PersonalTradingJournal.Desktop/Resources/Branding/ptj-app-n26.ico) | 16, 20, 24, 32, 40, 48, 64, 96, 128, and 256 pixel frames | `964C1CA092D69D60B6DF1445D22CB568F952A298179B3BA0B3868A5568B859D3` |

## Sidebar and accessibility

The existing sidebar header contains a 48 × 48 DIP image followed by a 12 DIP gap and the application name. `Stretch="Uniform"` preserves the artwork's aspect ratio; high-quality bitmap scaling handles display density changes. The title retains its `ApplicationTitle` binding, existing section-title style, dynamic sidebar text brush, and natural wrapping. The sidebar remains 252 DIP wide, with the same navigation structure and scrolling behavior.

`DecorativeImage` supplies a WPF automation peer excluded from both the control and content views. The image cannot receive keyboard focus or pointer input; the adjacent application name conveys its meaning. This replaces the package's `AutomationProperties.AccessibilityView` suggestion, which is not a supported WPF property.

## Embedded resources and Windows icon

The Desktop project declares exactly one WPF `Resource` item per asset and removes the corresponding default `None` items. Assembly-qualified `pack://application:,,,/PersonalTradingJournal.Desktop;component/Resources/Branding/…` URIs load the PNG and ICO independently of the working directory.

`ApplicationIcon` embeds the supplied ICO into the Windows apphost executable. `MainWindow.Icon` loads the embedded ICO for the main window's title bar and normal Windows taskbar/Alt+Tab representation. No loose PNG or ICO is needed beside the executable.

## Verification

Evidence is retained locally under `TestResults/n26-branding/` (ignored build/test artifacts, not committed documentation assets).

| Check | Result |
| --- | --- |
| Supplied asset identity | Source and repository SHA-256 hashes match for both files. |
| Decode and transparency | WPF decodes the PNG and all ten ICO frames; decoded frame sizes and transparent pixels are asserted. Embedded resource hashes match the originals. |
| Release build | Passed with zero warnings and errors. |
| Header rendering | All 19 isolated header cases passed, including four new branding cases covering Light/Dark at 96/240 DPI (100%/250%). Four sidebar renders were inspected with no title clipping or overlap. |
| Working-directory independence | Compiled shell and both resource streams load while the test's current directory is an empty temporary directory. |
| Accessibility and layout | Logo is absent from UIA control/content views and cannot focus; 48 DIP image, 12 DIP gap, 252 DIP sidebar, full title height, binding, and theme updates are checked. |
| Built EXE icon | Windows resource inspection found all ten `RT_ICON` payloads and matching `RT_GROUP_ICON` entries. Each frame's bytes and metadata match the supplied ICO exactly. |
| Publish | No existing publishing profile was found. A default framework-dependent Release publish passed; its EXE also contains all ten exact icon frames and its output has no loose PNG/ICO assets. The published app started from a separate empty working directory with an isolated profile, and Windows automation discovered its main window. Native visual acceptance remains pending. |
| Shell/navigation checks | Passed with normal Windows permissions: 227/227 parent tests, zero failures/skips. The isolated header child passed 19/19 cases; child totals are not added to parent totals. |
| Complete Release gate | Completed with exit 1: **5,530 passed / 5 failed / 0 skipped**, 5,535 parent tests total. All five failures are in existing Notebook native-menu cases, detailed below. No timeout or existing assertion changes. |
| Native application visuals | Capture blocked twice: `FrameArrived timed out: timed out waiting on channel`, then `window capture timed out: timed out waiting on channel` after fresh selection/activation. No usable native screenshot was obtained. Automated WPF renders are not native screenshots or interactive verification. |

The first build rejected the package's unsupported accessibility property; the WPF peer implementation corrected it. An initial new-test expectation incorrectly assumed the PNG was 1280 pixels wide; the actual supplied IHDR is 1254 × 1254, and the assertion now matches it without changing the asset. After those corrections, the restricted focused run passed 225/227 parents, with two Daily Review cases unable to read their synthetic protected credential storage. The final normal-permission run passed all 227 parents. Earlier failures remain in the evidence directory.

### Complete gate results

Run: `complete/release-20261101-183214-d054ff7ba4d749ecacd6b525ae1d22fd/`. All five expected final parent TRX files and the runner's `summary.json` are present. Child executions are evidence, not extra cases in the totals.

| Partition / project | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Exclusive WPF / Desktop | 135 | 5 | 0 |
| Parallel / Domain | 602 | 0 | 0 |
| Parallel / Application | 1,050 | 0 | 0 |
| Parallel / Infrastructure | 1,608 | 0 | 0 |
| Parallel / Desktop | 2,135 | 0 | 0 |
| **Total** | **5,530** | **5** | **0** |

The five failed parents each have a corresponding failed isolated child:

- `NotebookHeaderViewTests.OverflowTriggersToggleThroughNativeMouseAndOwnedKeyboard`: Light/1280/96, Dark/1280/96, and Light/480/240. The intended control did not become hit-testable with settled popup animation and mouse-over.
- `NotebookHeaderViewTests.TagMenuRoutedToggleDismissalAndCapturedOwnership`: Light/1280/96 and Dark/480/240. The intended control did not own the native target coordinates.

These existing fixtures host Notebook views directly, without the branded `MainWindow`. Their failure cause was not established by this task; no clean-baseline comparison or assertion/timeout changes were made. The complete gate remains failed despite the passing branding and shell checks. All 19 isolated header cases also passed inside this full run.

### Native verification boundary

Sandbox-launched trial processes started but were not discoverable by the computer-use tool. Launching on the interactive Windows desktop made the exact published executable discoverable as **Personal Trading Journal**. Its startup log confirms application initialization. The two supported capture attempts then timed out, so Light/Dark native appearance, actual monitor scaling, title-bar/Alt+Tab/taskbar presentation, and screen-reader behavior remain unverified. The task-owned process was stopped after capture recovery failed; the isolated profile is retained, and the user's normal journal was not opened.

`summary.json`, `native-result.json`, `asset-hashes.json`, build/test logs, render PNGs, and executable-resource inspection results under `TestResults/n26-branding/` retain the evidence. No push, pull request, or merge was performed.

### Commands

From the repository root in PowerShell:

```powershell
dotnet build PersonalTradingJournal.sln --configuration Release

$env:PTJ_HEADER_RENDER_DIRECTORY = Join-Path $PWD 'TestResults/n26-branding/renders'
$env:PTJ_TEST_RESULTS_DIRECTORY = Join-Path $PWD 'TestResults/n26-branding/focused-windows'
dotnet test tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~MainWindowViewModelTests' --logger trx --results-directory TestResults/n26-branding/focused-windows
Remove-Item Env:PTJ_HEADER_RENDER_DIRECTORY

pwsh -NoProfile -File scripts/Test-Release.ps1 -ResultsDirectory TestResults/n26-branding/complete

dotnet publish src/PersonalTradingJournal.Desktop/PersonalTradingJournal.Desktop.csproj -c Release --no-build --no-restore -o TestResults/n26-branding/publish
```

The local `inspect-icons.ps1` evidence script uses the Windows resource loader in data-file mode to compare group entries and SHA-256 hashes of all embedded icon payloads against the source ICO. Its results are in `exe-icons.json` and `published-exe-icons.json`.

## Remaining manual checks

- Inspect the actual app header in Light and Dark themes at Windows 100% and 250% scaling.
- Inspect the native title-bar, Alt+Tab, and normal taskbar icons, including the published executable launched from a separate working directory.
- Confirm screen-reader navigation announces the application title without repeating the decorative logo.
- Existing pinned shortcuts may retain a Windows-cached icon; check a fresh launch separately from an old pinned shortcut.

## Changed files

- `README.md`
- `docs/application-branding.md`
- `src/PersonalTradingJournal.Desktop/MainWindow.xaml`
- `src/PersonalTradingJournal.Desktop/PersonalTradingJournal.Desktop.csproj`
- `src/PersonalTradingJournal.Desktop/Interactions/DecorativeImage.cs`
- `src/PersonalTradingJournal.Desktop/Resources/Branding/ptj-logo-n26.png`
- `src/PersonalTradingJournal.Desktop/Resources/Branding/ptj-app-n26.ico`
- `tests/PersonalTradingJournal.Desktop.Tests/Navigation/MainWindowBrandingTests.cs`
