# Headless balance simulation: feasibility (2026-10-14)

Status: **feasibility check only. No bot has been built.**

## Verified

* `Assets/Scripts/Combat` (33 files, about 7,600 lines), `Core` (28 files) and `Map` (6 files) import **no** `UnityEngine`. Every `Time` / `JsonUtility` hit is a comment or a card name.
* All of it, about 11,700 lines, **compiles under plain .NET 8 with no Unity**. The only missing piece is the three small enums in `Cards/CardData.cs` (a Unity ScriptableObject), which can be stubbed.
* The existing Chronicle harness (`Tools/ChronicleHarness`) already shows the pattern: compile the engine-free C# and run it on a Linux machine in about a minute, with no Unity and no license.

So a headless simulator that plays thousands of fights, or whole runs, per minute is realistic.

## The catch

The rules engine does not start a fight on its own. `GildedMainMenu.BeginCombat` (UI code) does it: it builds the deck from the run, scales enemy HP and damage (`Scaling`, in `GildedMetaProgress.cs`), calls `CombatState.Begin(...)`, then applies Fate Debt (`ApplyFateDebtToCombat`), shard attunement and event bonuses. A bot that calls `CombatState.Begin` directly would test an approximation of the game, and could quietly drift from it.

## Options

1. **Extract that setup into an engine-free class in `Combat/`** that both the UI and the simulator call. Correct, but it edits the live fight-start path, and I cannot play-test the result here. It needs a careful behaviour-preserving change plus a Unity play-test (and the Playground) before trusting it.
2. **Copy the setup into the simulator.** Safe for the game, but the simulator can drift; results would be hints, not truth.

Either way a bot also needs a card-choosing policy. A simple one (play the highest-value affordable card, block when the intent is lethal) finds broken combos and gross difficulty problems, not subtle balance. Treat early results as hints.

## Recommendation

Do option 2 first as a throwaway experiment to see whether it finds anything useful (an afternoon of work, no risk to the game). Do option 1 only if the simulator earns its keep.
