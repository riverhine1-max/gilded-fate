# Chronicle QA build

A separate Windows build, `GildedFate-Windows-QA.zip`, for testing the Chronicle before real playtesting. It is the normal game plus a developer-only **Chronicle QA panel**. The normal Windows release (`GildedFate-Windows.zip`, `playtest` release) is not changed in any way.

## Getting the build

1. GitHub → **Actions** → **Build Gilded Fate** → **Run workflow** → targets: **windows-qa**.
2. When it finishes, download `GildedFate-Windows-QA.zip` from the **Chronicle QA build (testers only)** release (tag `chronicle-qa`), or from the run's **Artifacts** (`GildedFate-Windows-QA`, kept 30 days; the artifact download is a zip containing the zip).
3. Unzip and run `GildedFateQA.exe`. The window title is "Gilded Fate QA".

`windows-qa` is never part of `all`, never built by a push, and never published on the `playtest` release. A repository that is public also makes the `chronicle-qa` release public; use the Artifacts download instead if you do not want testers' spoilers (Chapter X) on the releases page, or delete that release.

The first QA build is slower than later ones (its Unity Library cache starts empty).

## The panel

Main menu → **CHRONICLE QA** (bottom right), or press **F10**.

| Area | What it does |
| --- | --- |
| **Play mode** | **PREVIEW** plays any scene and saves nothing at all. **RECORD** plays it the way the game does: it only counts when the scene is genuinely unlocked (memories) or earned (recollections), so viewing never earns anything. |
| **Original memories (27)** | Play/replay any memory. **MEET RULE** satisfies that memory's own unlock rule through the real milestone calls. |
| **Recollections (30)** | Pick Vanguard / Hexer / Reaper and Fate Debt I–X. Shows id, major or small, state. **EARN IT** earns it by the genuine rule; **WIN A RUN AT THIS LEVEL** sends a victory through the rules. `*` marks the 12 majors (Fate Debt I, IV, VII, X). |
| **Major corrections (12)** | The twelve `ASC_CORR_*` correction scenes. |
| **Opening · Chapter X** | Both openings and the Chapter X finale. **OPEN (GENUINE)** opens Chapter X the real way (see below). |
| **Genuine progression** | Hero + Fate Debt, then: win at that level, defeat, Normal win, Daily Run win, Playground win, repeat the last run. These go through the real rules (`IsQualifyingVictory`, `RegisterAscensionVictory`, then the run-end milestones) in the same order the game's run-end code uses. They do **not** create a real win, marks, run history, achievements or Steam progress. The status block shows each hero's chain (`n/10`, next Fate Debt). |
| | **PREPARE** earns everything before the chosen Fate Debt so the next victory is the one that is presented. **SHOW PENDING** plays the earned, unseen recollections exactly as the post-victory flow would. **REAL POST-VICTORY FLOW** runs the game's own `BeginChronicleMilestone` (new memories, the shared ending, then recollections). **RUN THE RULE CHECKS** runs a 17-step battery on a throwaway profile and prints PASS/FAIL per rule. |
| **Simulate unlock conditions** | Unlock all 27 memories; open Chapter X through 30 genuine victories; open Fate Debt I–X so you can start a real run at any level. |
| **Navigation · saves** | Open the real Archive (chapter jumps, reading, Continue). **SAVE, RELOAD FROM DISK AND COMPARE** saves, reads the file back and compares every Chronicle field, then reports "restored identically" or the exact differences. |
| **RESET ALL QA PROGRESS** | Bottom right, one click. Resets the whole QA profile (memories, recollections, corrections, journal pages, Fate Debt, records). Display/audio settings and reading preferences are kept. |

### Checks you asked about

* **Archive chapter jumps I→IX and IX→III:** panel → *Unlock all 27 memories* → *Open the Archive* → jump chapters from the contents page. Use *Save, reload from disk and compare* before and after to confirm the reading place is restored.
* **A Fate Debt I victory recollection:** *Reset all* → hero Vanguard, Fate Debt I → *Win a run at Fate Debt I* (should say EARNED ASC_VAN_01) → *Show pending* or *Real post-victory flow*. Then *Repeat the last run* (must earn nothing) and win at Fate Debt I again (must earn nothing; Fate Debt II is next).
* **Chapter X reveal:** *Open Chapter X (30 genuine victories)* then Opening · Chapter X → play it in Record mode, or open the Archive and begin it from there.
* **Real wins:** *Open Fate Debt I–X for real runs*, then start a normal run from Character Select. It is a real run in the isolated QA profile.

## Performance readout

Press **F11** on any screen of the QA build to show a frame-time and garbage readout; press **F12** to drop a numbered marker when something stutters. Rows are written every second to `...\ChronicleQA\perf\*.csv`, with a summary when you quit. Send those files back with a note of which marker was which. See `Docs/PERFORMANCE_AUDIT.md`.

## Human review checklist

`Docs/CHRONICLE_QA_CHECKLIST.md` lists every recollection and narrated line to listen to and watch. The machine audit (`python3 ci/audit-chronicle-content.py`, also run on every pull request) already confirms the text, recordings, timings and art agree with each other; the checklist is only what a person has to judge.

## Isolation and production safety

* **Own save folder.** In the QA build every save (profile and run) goes to `…\AppData\LocalLow\Gilded Fate\Gilded Fate QA\ChronicleQA`. The QA build also has its own product name, so its PlayerPrefs are separate from the real game's. The real profile is never read or written.
* **Steam off.** `GildedSteam` refuses to start or unlock while QA is active, even if `GILDED_STEAM` were defined. The QA build ships no `steam_appid.txt`.
* **Compile-time exclusion.** All QA code sits behind `#if UNITY_EDITOR || GILDED_CHRONICLE_QA`. The CI QA build passes `GILDED_CHRONICLE_QA` to that one build only (`BuildPlayerOptions.extraScriptingDefines`); it is never written to `ProjectSettings`. A normal player build contains none of it.
* **Checked on every run.**
  * `ci/verify-qa-config.sh` (before any Unity build): the symbol is not in ProjectSettings, only `BuildWindowsQA` passes it, the other build methods and the normal publish step are untouched, `windows-qa` is not in an automatic target list, the QA zip never goes to the `playtest` release, every QA file is fully guarded and every QA reference elsewhere sits inside a guard.
  * `ci/verify-qa-build.sh release …` (after the **normal** Windows build, before it is published): fails the release if any QA type name is in its assemblies.
  * `ci/verify-qa-build.sh qa …` (after the QA build): fails unless the QA types are present, no Steam id or Steam library is shipped, and the executable is `GildedFateQA.exe`.

## In the Unity Editor

QA code is compiled in the Editor so errors show up as you work, but the panel stays off and your save is untouched until you switch it on: **Gilded Fate → Chronicle → QA → Isolated QA Profile (Editor)** (before entering Play Mode). Then F10 in the main menu opens the panel against a separate QA profile with Steam blocked. The same menu can show or delete the QA save folder. The existing Chronicle Developer Console, Playground and Admin panel are unchanged.

## Content note

The 30 recollection scenes (`ASC_*`) and the 12 correction scenes are listed from the catalog, but a scene is only playable when it is registered in `ChronicleScripts`. Until `ChronicleScripts.Ascension.cs` and its recordings are in the repository, those rows show *not in this build* and everything else (earning, rules, state, reset, save checks) already works. They light up automatically once the scenes are registered. *Show pending* follows the real game and only presents scenes that are authored and marked ready.
