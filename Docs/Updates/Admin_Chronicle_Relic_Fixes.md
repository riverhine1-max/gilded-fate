# Admin, Chronicle and relic fixes

Installed in the live Shattered Fate Unity project on October 9, 2026.

## What changed

- **Chronicle reset:** Admin Reset Progress now clears unlocked/viewed memories, corrections, facts, pending notices, recorded story triggers, opening-view history and Chapter X progress. Archive caches are cleared too. Story display preferences and ordinary settings remain intact. The reset action was tested on an isolated profile.
- **Admin input:** Opening admin disables all background setup controls and removes their hover, keyboard and scroll input. Close/Back affect only the admin panel. Existing setup and sandbox behavior are preserved.
- **Take Relic:** The global acquisition input blocker was swallowing mouse events before the waiting Take button could process them. Acquisition now renders as an exclusive modal, handles its own button first, and consumes unused input afterward. Reward receipts still prevent duplicate grants. The shared fix covers offered relics in all acts and screens using that presentation.
- **Death interruption:** The Being now says “Not yet.”, “Nothing was meant to be lost.” and “The world is still broken.” using recordings already in Unity. Their exact text and measured word timings are reused. Narrator responses and the red correction remain; victory dialogue and the existing 244 story dialogue bindings are preserved. No replacement audio was fabricated.
- **Chronicle readability:** Removed the green bookmark ribbon from view. Darkened ink and softened the parchment immediately behind existing text rectangles, preserving ornamentation, wrinkles, fonts, sizes, placement and page layout. Text remains the same color across hover/press/focus. Chapter-selection row backgrounds darken on hover. Other Chronicle hover highlights were removed; controller focus remains visible.

## Validation

- Engine-free Chronicle suite: 158 passed, 0 failed.
- Runtime, Chronicle and Editor compilation passed. Native Unity also loaded the compiled changes.
- Admin/reward integration: 111 checks across 21 reward flows, 0 failures. Includes populated-profile reset plus reload/migration, queued relics across three acts, all heroes' Act 1/2 boss choices, treasure/encounter receipts, Shards, gold and controller Back.
- Act/run-ending integration: 295 checks across 12 combat flows, 30 actual audio bindings checked, 0 failures.
- Live Archive checks: 4 passed; ribbon disabled, parchment renderers visible, both page projections intact, label ink invariant across GUI states.
- Real mouse input confirmed Take Relic claims exactly once. Background hero/Start Fight buttons stayed inactive beneath admin, while Close worked. Hovering page writing kept it dark; hovering a chapter darkened that row.
- **Save recovery and test safety:** A verification shutdown restored saving too early and temporarily overwrote the profile. The exact pretest profile and backup were recovered and restored, verified against their recorded SHA256 hashes. The fixture now keeps saving suspended until fully back in Edit Mode, cleans up disposable test objects and copies all four save files before testing. All four current save files match the original recorded baseline: True.

## Files modified

All paths below are relative to `C:\Users\river\Documents\Unity Projects\Shattered Fate`.

- `Assets/Scripts/UI/GildedMainMenu.cs`
- `Assets/Scripts/UI/GildedAcquisitionPresentation.cs`
- `Assets/Scripts/UI/GildedPlayground.cs`
- `Assets/Scripts/UI/GildedMenuNavigation.cs`
- `Assets/Scripts/Chronicle/ChronicleMilestoneScenes.cs`
- `Assets/Resources/Chronicle/Audio/DialogueIndex.json`
- `Assets/Tests/EditMode/ChronicleMilestoneTests.cs`
- `Assets/Editor/ChronicleMilestoneVerification.cs`
- `Assets/Scripts/UI/GildedModalInputVerification.cs`
- `Assets/Editor/GildedModalVerificationRunner.cs`
- `Assets/Scripts/UI/GildedChronicleArchive.cs`
- `Assets/Scripts/UI/GildedChroniclePresentation.cs`
- `Assets/Scripts/ChronicleBook/ChronicleAnimatorBookRig.cs`

## Remaining manual review

The focused automated and native input checks passed. A complete normal playthrough covering every event/reward combination and a listening review of the revised death scene remain useful final QA. No full game build or end-to-end playthrough is claimed.
