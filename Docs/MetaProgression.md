# Meta progression (v0.9.0)

Everything in this document is saved in the player profile (`gilded_fate_profile.json`).
None of it changes the trailer footage.

## Fate Marks and card unlocks

You earn Fate Marks at the end of every run, win or lose:

| Source | Marks |
|---|---|
| Each floor climbed (beyond the first) | 1 |
| Each elite defeated | 5 |
| Each boss defeated | 20 |
| Victory | 40 + 5 × Fate Debt level |
| Daily Run | +10 |

About a third of the card pool starts locked: roughly 25% of commons, 33% of uncommons and 45% of rares for each origin. The locked cards are split into 5 tiers.

| Pool | Tier thresholds (marks) |
|---|---|
| Vanguard / Hexer / Reaper cards | 40 · 110 · 200 · 320 · 480 (that hero's marks) |
| Wanderer cards | 80 · 220 · 400 · 640 · 960 (marks across all heroes) |

- Starter decks are never locked.
- Locked cards never appear in rewards, shops or events. If filtering would leave a pool too thin, the full pool is used instead.
- The Collection shows locked cards with the LockedCard art and the marks still needed.
- Which cards are locked comes from a fixed hash of each card ID, so it is the same on every machine and every session. Adding new cards to a pool later can move a few cards in or out of the locked set.

## Fate Debt (ascension)

Win a run with a hero to open Fate Debt I for that hero. Winning at level N opens level N+1. Each level includes every level below it.

| Level | Name | Effect |
|---|---|---|
| I | Tarnished Blades | Elites start with extra Strength (+20% of their base damage) |
| II | Lean Purses | Combat gold −25% |
| III | Heavier Crowns | Bosses +15% HP |
| IV | Cursed Inheritance | Start with a random Curse |
| V | Cold Hearth | Sanctuary rest heals 20% instead of 30% |
| VI | Costly Gilding | Gilding costs +10 Gold |
| VII | Hardened Vault | Normal enemies +10% HP and +1 Strength |
| VIII | Greedy Elites | Elites +15% HP |
| IX | Frayed Life | Start with 10% less max HP |
| X | The Debt Comes Due | Act III boss +20% HP and +3 Strength |

To change the level, use the selector on Character Select: the arrow buttons, or LB / RB on a controller.

## Daily Run

- The date (UTC) sets the seed, the hero and 2 Fate Debt modifiers, so everyone gets the same fate that day.
- Your first attempt each day is scored. Later attempts are practice and aren't scored.
- Score = floors, elites, bosses and gold, plus a speed bonus for a victory.

## Records

Main menu → RECORDS has four tabs:

- **Achievements:** 30 achievements.
- **Run History:** the last 100 runs, with deck, relics and cause of death.
- **Statistics.**
- **Unlocks:** tier progress bars.

## Achievements

These IDs match the Steam API names (see SteamSetup.md):

ACH_FIRST_ASCENT, ACH_WIN_VANGUARD, ACH_WIN_HEXER, ACH_WIN_REAPER, ACH_BOSS_HOLLOW_KING,
ACH_BOSS_VAULT_MOTHER, ACH_BOSS_LAST_DEALER, ACH_DEBT_1, ACH_DEBT_5, ACH_DEBT_10, ACH_HIT_50,
ACH_HIT_100, ACH_HIT_250, ACH_BLOCK_50, ACH_GILD_1, ACH_GILD_25, ACH_ELITES_10, ACH_ENEMIES_100,
ACH_CARDS_1000, ACH_RELICS_15, ACH_SHARDS_3, ACH_LEAN_DECK, ACH_THICK_DECK, ACH_SPEED,
ACH_FATEWOVEN, ACH_DAILY_1, ACH_DAILY_7, ACH_UNLOCK_FIRST, ACH_UNLOCK_ALL, ACH_RUNS_25

Names, descriptions and icons are listed in `Assets/Scripts/Core/AchievementCatalog.cs`.

## Settings

| Tab | Settings |
|---|---|
| Graphics | Window Mode (Fullscreen / Borderless / Windowed), Resolution (asks to confirm, auto-reverts after 12 s), Brightness, V-Sync, Frame Rate, Texture Quality, Anti-Aliasing |
| Audio | Master, Music, Effects, UI, Mute in Background |
| Gameplay | Game Speed (Normal / Fast / Very Fast), Instant Enemy Turns, Confirm End Turn, Screen Shake, Damage Numbers, Tooltips, Fast Transitions, Card Motion Speed |
| Accessibility | Unchanged |
| Controls | Button Prompts (Auto / Xbox / PlayStation), Controller Badges, and a full keyboard and controller reference |

Every tab has **Reset to Defaults**, which only resets that tab.

Button remapping is not included yet.

## Admin Playground (not in release builds)

The Playground lets you set up any fight:

- hero
- 1–4 enemies, or a single elite or boss
- boss phase
- enemy HP multiplier
- act and Fate Debt
- HP, energy, strength and gold
- infinite energy and god mode
- the exact cards and copies (with upgrades)
- a Fate Shard and relics

It never saves. Your real run is restored when you leave.

The Playground's code is wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`:

- **In the Unity Editor, or a build with Development Build ticked:** on the main menu, press **Ctrl + Shift + P**, or **LB + RB + View** on a controller.
- **In a release build (Development Build unticked):** the Playground is compiled out completely. There is no menu entry, shortcut or code left in it.
