# Gilded Fate — Final Chronicle Integration

**Base:** your `main`, commit `7211864e93fee914ef370912ae1ed8aafd66266b` (Prompts 1 and 2 present). If `main` changes before you upload, compare first: `FILE_MANIFEST.md` lists every file's hash on that commit.

## Install
1. Back up your local save (`gilded_fate_profile.json`).
2. Unzip. On GitHub: **Add file → Upload files**, drag in the **`Assets`** folder, commit. Nothing needs deleting; no package, `.asmdef` or project-setting changes.
3. About 50 MB is audio. GitHub's web upload accepts files up to 25 MB each (the largest WAV here is far smaller), but upload in two batches if the browser struggles: first `Assets/Resources/Chronicle/Audio`, then the rest.
4. Optional: the Prompt 3 `SourceAssets/ChronicleAscension` package is the editable source. It is not needed at runtime and is not in this ZIP.

## What it does
- **All 30 recollections play after their Fate Debt victory**, one per victory: after the established victory scene the journal flips back through its pages to the recollection's own chapter and writes it there. Majors hesitate, strike their new mistaken observation in red and write the correction beneath; smaller ones are written quietly. 45 narrated lines play in sync with the handwriting; silent passages are written by hand.
- **Permanent:** each recollection becomes part of its memory's pages in the Archive (after the original pages), is listed in its chapter with *Read* and *Watch again*, and marks the chapter with the red ¶ until opened.
- **Chapter X:** when the final True History completes it, the journal turns to the last chapter and its title is written in before the book closes. Chapter X itself is unchanged, and readable after it is watched.
- **Physical page journeys:** choosing a chapter flips through every sheet in between, quick in the middle and slowing at the end, forward or backward, with the page stacks moving. Any key or click lands at once. Single-page reading turns are unchanged.

## Check it in Unity
1. **Test Runner → EditMode → Run All** (226 tests). Without Unity: `dotnet run --project Tools/ChronicleHarness`.
2. In the Archive, go from Chapter I to Chapter IX and back to III: you should see eight quick sheets, then six backward.
3. Win a Fate Debt I run: after the victory scene the book should return to the hero's chapter and write their first major correction with narration.
4. See `REMAINING_ISSUES.md` for open items (Unity verification, listening review).
