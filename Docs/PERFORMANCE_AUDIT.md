# Performance audit: UI allocations, and how to measure them

Status: **static audit only. Nothing here has been profiled in Unity**, so none of it proves a frame-rate problem exists. It says where garbage is likely and gives you a tool to find out.

## What the code shows

All game UI is Unity's immediate-mode GUI (`OnGUI`), which runs at least twice per frame (Layout and Repaint) and again for every input event.

| Finding | Count |
| --- | --- |
| `new GUIStyle(` sites in `Assets/Scripts` | about 340 across 44 files |
| of those, inside methods named `Draw*` / `Update*` / `Handle*` / `OnGUI` (run every frame) | about 310 (roughly 90%) |
| of those, inside one-time setup (`Build*`, `Awake`, `Init*`) | 4 |
| LINQ calls (`Where` / `Select` / `OrderBy` / `ToList` / `Any` ...) in `GildedMainMenu.cs` | 48 |
| LINQ calls in `GildedCombatPresentation.cs` | 16 |

Largest per-frame style allocators: `GildedMainMenu.cs` (73), `GildedRecordsScreens.cs` (25), `GildedCombatPresentation.cs` (20), `GildedPlayground.cs` (17, Editor/Development only), `GildedSettingsPresentation.cs`, `GildedFateweaveLoom.cs` (13 each), `GildedWildPresentation.cs`, `GildedMetaProgress.cs` (12 each).

Already done well: the base menu styles are built once (`BuildStyles`), and the Chronicle book and Archive cache their styles by size (`chronicleStyles`, `archiveStyles`). The Chronicle presentation code has no per-frame LINQ.

A copied `GUIStyle` allocates managed and native objects each time. Hundreds per frame can mean steady garbage and, on a modest PC, an occasional collection hitch. Whether it does here is a measurement, not a guess.

## Why nothing was mass-edited

Roughly 310 call sites in legacy partial classes, with no way here to see the result on screen, is a recipe for subtle visual regressions (the project's own rules also ask for focused diffs in those files). The plan is to measure first, then cache the few styles that matter, one screen at a time.

## How to measure (QA build)

The QA build records frame times and an approximate garbage figure on every screen, including during Chronicle scenes and combat.

* **F11** shows or hides the readout: FPS, average frame time, p95, p99, worst frame, approximate KB allocated per frame, number of garbage collections, hitch counts (frames over 33, 50 and 100 ms).
* **F12** drops a numbered marker at the moment something stutters.
* Every second a row is written to a CSV, and a summary when the game closes, in `%USERPROFILE%\AppData\LocalLow\Gilded Fate\Gilded Fate QA\ChronicleQA\perf\`.

Run each of these for about a minute on the weakest PC you have and send me the CSV and summary files:

1. Main menu, idle.
2. Archive: open it, jump chapters, read a memory.
3. A recollection in Preview mode, a major correction in particular.
4. A real combat with many enemies.
5. The Records and Collection screens.

## How to read the numbers

| What you see | Meaning |
| --- | --- |
| p99 under 16.7 ms | 60 fps holds for 99% of frames |
| KB/frame steady above a few KB | allocation every frame; the likely source of garbage collections |
| collections climbing and hitches that line up with them | garbage is causing stutter; the style and LINQ sites on that screen are the first fix |
| hitches with no collections | something else (asset loading, shader compile, the book); the marker times tell us where |

The readout itself draws only on Repaint and reuses one text buffer, so it adds almost nothing to what it reports. The QA panel's own styles are cached for the same reason.
