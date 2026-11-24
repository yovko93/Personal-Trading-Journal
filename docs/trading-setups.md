# Trading Setups

Open **Analysis → Setups** to manage the patterns used to classify Trades. The table shows Name, Description, Status and Actions. Scroll it horizontally on a narrow screen to reach Actions. **View** opens the chosen Setup and scrolls to the page top. The adjacent three-dot menu contains **Edit**, Activate or Deactivate, and Delete.

## Example images

In **New Setup**, enter a name and optional description, then choose **Select images** to stage one or more PNG or JPEG files. Each thumbnail has its own optional Timeframe. **Remove** on a staged thumbnail takes it out of the draft. **Create Setup** saves the Setup and all remaining image references together. Cancel discards the draft and its in-memory selections. A failed save keeps the draft available for correction.

To add an example later, open the Setup with **View**, choose **Upload image**, enter an optional Timeframe and choose **Save image**. Each file must be a single-frame image of up to 8 MiB.

In Setup details, cards reserve the same space for a thumbnail, filename, optional Timeframe and Remove button. Portrait and landscape images keep their proportions. Long filenames wrap to two lines. Hovering the card or opening its preview reveals the full label. The card’s accessible name also includes the filename and any Timeframe.

Click a card or focus it and press Space or Enter to show the original below the gallery. The other cards stay available so you can switch images directly. **Close Preview** returns focus to the card that opened the current image. Scroll vertically over the details or cards to move the page. The table retains its horizontal scrollbar at narrow widths.

In **Edit**, the Example images section shows every saved image and its optional Timeframe. Use **Select images** to add files, edit or clear a Timeframe, or choose **Remove**. Removing a saved image from the draft asks for confirmation. **Save Changes** commits the Setup fields and image changes together. **Cancel** returns directly to the list and discards the draft. If Save fails, the saved gallery stays intact and the draft remains available. In the read-only details gallery, the separate **Remove** action still asks for confirmation and takes effect immediately.

Galleries start empty and are optional. Deleting an eligible Setup removes its image references. Physical cleanup checks the other owners before deleting an original. A Setup referenced by Trades retains its existing deletion protection. Historical Playbook content stays in its separate archive.

## Storage and recovery

Migration `20261123085402_AddSetupExamples` adds the Setup image table and its owner foreign key. A successful upload records the file's size and SHA-256 alongside its generated storage key. Canceling before save writes nothing. A failed file copy or rejected metadata insert cleans up the staged file. Complete backups carry the originals, and portable exports carry the metadata. [Backup and restore](backup-restore.md#trading-setup-example-images)

## Verification

The current verification covers the 24 November workspace changes on develop at `0879748` plus the working-tree changes. Fixtures use synthetic journals and images.

| Check | Local result |
| --- | --- |
| Release build | Exit 0 in 22.40s with zero warnings or errors. |
| Complete local Release gate | 5,493 passed, 0 failed, exit 0 in 13m 59.34s. The only skip was the existing [Rules quarantine](rules-view-quarantine.md). Exclusive WPF passed 152/152. |
| Focused tests | 78 passed, 0 failed, 0 skipped. Application passed 17, Infrastructure 37 and Desktop 24. |
| Editing and file ownership | Tests covered staging and clearing Timeframes, confirming or declining removal, Cancel returning to the list, failed Save retaining the draft, and successful Save. A synthetic SQLite rejection preserved the previous fields and gallery and reclaimed the new file. A second Setup retained access to its shared original after removal from the first Setup. |
| WPF layout and scrolling | Inspected compiled Light and Dark galleries, open previews and editors at 1000 DIP/96 DPI and 460 DIP/240 DPI. The fixture paired a short and long filename with wide and tall thumbnails. Bound checks measured equal cards and thumbnail areas, aligned Remove buttons, original placement, scroll-to-top on View, and ordinary and fractional wheel deltas in both directions. |
| Windows input | A native mouse click opened a card. WPF Space activated another card while the preview was open. Close Preview returned keyboard focus to the correct opener. |

Focused TRX files and render PNGs are under `TestResults/setup-gallery-edit/final/`.

### Live Windows acceptance

On 24 November 2026, the Release executable built from clean develop `48dfb6af92ff3f77596c45397585f9fe5ac86494` started with `--isolated-data-root` in a fresh temporary profile. The build passed with zero warnings and zero errors. The live Setup used four generated PNGs with wide, portrait and square proportions. The portrait image had a long filename and a **15 min** Timeframe.

| Check | Live result |
| --- | --- |
| Native multi-file selection | The operator selected all four files in one Windows picker confirmation. Four staged cards appeared. After saving, the isolated database listed all four under the same Setup. The portrait retained its Timeframe and every stored original matched its source byte for byte. |
| Physical touchpad | The operator used a two-finger gesture to scroll down with the pointer over the portrait card, then scrolled up with the pointer over its open preview. The page moved in both directions. |
| Windows Narrator | Opening the portrait preview focused **Close Preview**. Pressing Space closed the preview, and Windows UI Automation and Narrator's visible focus outline both identified the original portrait card. The operator heard Narrator announce the image and its **15 min** Timeframe. |
| Live card layout | Inspected Light at 100% and both Light and Dark at 250% Windows scale on the live app. The app was relaunched at 250% and Windows reported 240 DPI for its window. All four cards had equal dimensions and aligned Remove buttons. Wide, square and portrait thumbnails kept their proportions. The long filename wrapped to two lines and the preview showed its full label. The Dark preview at 240 DPI opened below all four cards. |

### Changed files

- `README.md`
- `docs/trading-setups.md`
- `src/PersonalTradingJournal.Application/Setups/ISetupExampleStore.cs`
- `src/PersonalTradingJournal.Application/Setups/UpdateTradingSetupCommand.cs`
- `src/PersonalTradingJournal.Application/Setups/UpdateTradingSetupResult.cs`
- `src/PersonalTradingJournal.Application/Setups/UpdateTradingSetupUseCase.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Setups/TradingSetupsViewModel.Images.cs`
- `src/PersonalTradingJournal.Desktop/ViewModels/Setups/TradingSetupsViewModel.cs`
- `src/PersonalTradingJournal.Desktop/Views/Setups/TradingSetupsView.xaml`
- `src/PersonalTradingJournal.Desktop/Views/Setups/TradingSetupsView.xaml.cs`
- `src/PersonalTradingJournal.Infrastructure/Setups/SetupExampleStore.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Setups/SetupCardInputTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Setups/SetupExampleViewTests.cs`
- `tests/PersonalTradingJournal.Desktop.Tests/Setups/TradingSetupsViewModelTests.cs`
- `tests/PersonalTradingJournal.Infrastructure.Tests/Backups/SetupExampleTests.cs`

### Previous create and inline-view verification

Verified locally on 24 November 2026 on develop at `a7f1502` plus the working-tree changes. The Release build completed with zero warnings and errors. All fixtures used synthetic journals and files.

| Check | Result |
| --- | --- |
| Focused tests | 107 passed, zero failed or skipped. Application passed 31, Infrastructure 52 and Desktop 24. |
| Create and rollback | Covered creation without images, multi-image creation with optional Timeframes, staged removal and Cancel. Cancellation after the first copy, rejection of a later file and a failed database write left no newly created rows or files. A previously owned original remained intact. |
| Gallery and accessibility | In an isolated Windows window, a native mouse click and WPF Space input each opened the intended card. Back returned keyboard focus to its opener. Opening preserved the image and raised no deletion prompt. Separate removal checks covered declining and confirming. |
| Restore | A restored Setup opened its original through the production decoder and returned to the gallery. This change used the existing schema and backup format. |
| Compiled renders | Inspected the new form, saved cards, dividers and inline original in Light and Dark at 1000 DIP/96 DPI and 460 DIP/240 DPI. The two staged images retained independent Timeframe fields and the full preview scaled to fit. |
| Complete local Release gate | 5,490 passed, 0 failed. The existing [Rules quarantine](rules-view-quarantine.md) was the single skip. All 152 exclusive WPF cases passed. Exit 0 in 13m 45.17s. |

Manual Windows multi-file picker interaction, physical high-DPI inspection and screen-reader speech remain pending. Local evidence is under `TestResults/setup-create-inline/final/`. It includes the gate summary, timer, focused results, renders and the exact changed-file list in `changed-files.txt`.

## Previous gallery verification

The Release build on develop at `3ef9957` plus the working-tree changes completed with zero warnings and errors. EF reported no pending model changes. Verification used synthetic journals and image files.

| Check | Evidence |
| --- | --- |
| Setup commands and compiled WPF | 63 Desktop tests passed. The gallery check exercised Upload, Cancel, failed Save, successful Save, View and confirmed removal. It checked the exact row ID on Edit, all headings, equal 78 × 36 DIP action buttons, and horizontal reachability. |
| Database, file ownership and recovery | 174 Infrastructure tests passed. Coverage included failed copies and metadata writes, shared originals, exact-owner removal, damaged backup rejection and restore of a pre-image backup. |
| Restored gallery | The Desktop backup workflow reopened a restored Setup, decoded the card thumbnail through the production WPF viewer, checked the original bytes and verified that View was enabled. |
| Visual inspection | Compiled offscreen renders were inspected in Light and Dark at 1000 DIP/96 DPI and 460 DIP/240 DPI. Physical mouse and keyboard use, the native file picker and full-size viewer, screen-reader speech, and hosted acceptance remain unverified. |
| Complete local Release gate | 5,484 passed, 0 failed, 1 existing Rules quarantine skip. Exit 0 in 13m 35.90s. Exclusive WPF passed 151/151. |

The first integrated gate had three stale schema expectations. The final contracts explicitly require the 33rd migration, the Setup example record, and its owner foreign key and cascade. The authorized Rules skip is documented in the [quarantine record](rules-view-quarantine.md).

Local evidence is under `TestResults/setup-images/`. The `acceptance` directory contains the complete gate summary and timer, `acceptance-desktop` and `acceptance-infrastructure` contain the focused TRX files, and `renders` contains the eight inspected layouts. The exact working-tree changes are listed in `changed-files.txt`.
