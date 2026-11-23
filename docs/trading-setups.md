# Trading Setups

Open **Analysis → Setups** to manage the patterns used to classify Trades. The table shows Name, Description, Status and Actions. Scroll it horizontally on a narrow screen to reach Actions. **View** opens the exact Setup. The adjacent three-dot menu contains **Edit**, Activate or Deactivate, and Delete.

## Example images

1. Create and save a Setup, or open an existing one with **View**.
2. Choose **Upload image** and select a single-frame PNG or JPEG, up to 8 MiB. Check its thumbnail and optionally enter a Timeframe such as “15 min”.
3. Choose **Save image** to add the card. **Cancel** discards the selection from memory. Repeat to add another example.
4. Choose **View** on a card to open the original image. **Remove** asks for confirmation and targets that card in its owning Setup.

Galleries start empty and are optional. Deleting an eligible Setup removes its image references. Physical cleanup checks the other owners before deleting an original. A Setup referenced by Trades retains its existing deletion protection. Historical Playbook content stays in its separate archive.

## Storage and recovery

Migration `20261123085402_AddSetupExamples` adds the Setup image table and its owner foreign key. A successful upload records the file's size and SHA-256 alongside its generated storage key. Canceling before save writes nothing. A failed file copy or rejected metadata insert cleans up the staged file. Complete backups carry the originals, and portable exports carry the metadata. [Backup and restore](backup-restore.md#trading-setup-example-images)

## Verification

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
