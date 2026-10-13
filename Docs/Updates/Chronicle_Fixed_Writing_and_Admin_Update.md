# Chronicle writing and admin story unlocks

Installed in the open **Shattered Fate** Unity project on October 9, 2026.

The Chronicle now uses one fixed handwriting size (34 page units) throughout narrated passages, headings and endings. Existing ink stays the same size when another passage is added. The stroke animation, recorded dialogue, parchment detail and illustrations remain in use.

Full pages turn before the next passage begins. The new passage's voice and handwriting wait for the sheet to land. Reading pages in the Archive use the same font measurements and pagination as playback.

Correction passages retain their red-ink animations. Short correction sequences stay together; longer sequences reserve space for the rewrite beside the original text and turn back to that page before crossing it out. The transcript and voice IDs remain unchanged, so the story text and recordings still match.

The development admin panel has **UNLOCK ALL CHRONICLE STORIES**, and **UNLOCK EVERYTHING** now includes Chronicle stories. Both open all 27 memories and secret Chapter X. They preserve existing readings, facts, corrections, runs, wins and preferences. These controls use the existing profile saving system and existing local-admin flag.

To use the new control: **Playground → Admin Panel → Unlock All Chronicle Stories**. Stories appear under **Archive → Chronicle**.

## Files changed

Paths below are relative to `C:\Users\river\Documents\Unity Projects\Shattered Fate`.

| File | Change |
| --- | --- |
| `Assets/Scripts/Chronicle/ChronicleWriter.cs` | Real font-height measurement and fixed-size pagination options. |
| `Assets/Scripts/Chronicle/ChronicleSequencer.cs` | Automatic page turns, delayed voice and ink, page history and correction rewrite space. |
| `Assets/Scripts/Chronicle/ChronicleProgress.cs` | Idempotent admin unlock for every memory and Chapter X. |
| `Assets/Scripts/UI/GildedChroniclePresentation.cs` | Fixed writing size and actual book-surface measurements. |
| `Assets/Scripts/UI/GildedChronicleArchive.cs` | Uses matching measurements for reading pages. |
| `Assets/Scripts/UI/GildedPlayground.cs` | Story count, dedicated unlock button and inclusion in Unlock Everything. |
| `Assets/Tests/EditMode/ChronicleFixedWritingTests.cs` | Regression coverage for pagination, voice delays, pause, skip, corrections and admin progress preservation. |
| `Assets/Editor/ChronicleFixedWritingVerification.cs` | Native Unity checks with the imported book and handwriting font. |

Unity metadata was added for the two new C# files. Backups and installation receipts are retained in this chat's work folder.

## Validation

- **148 Chronicle regression tests passed, zero failed.**
- **43,995 native Unity checks passed, zero failed.** Includes all 30 scenes across 360 narrator, knowledge and replay combinations, 3,564 passages, both book pages at three zoom levels, and all 27 Archive readings.
- Admin unlocks survived Unity JSON serialization and preserved unrelated progression and preferences in an isolated profile fixture.
- Unity compiled the new runtime, Editor and test assemblies. The project's production quality gate passed.
- Live Play mode showed the imported book, detailed parchment and stroke-written text at the fixed size.

The native checks use an isolated profile and do not activate admin unlocks on the player's real save. Longer listening and visual preference checks remain useful for judging the pacing of the added page turns.
