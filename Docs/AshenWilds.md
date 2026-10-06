# Act 1 · Theme 2 — Ashen Wilds

Each run rolls one theme per act when it starts (`RunModel.actThemes`). Act 1 rolls **The Gilded Ruins**
(see `GildedRuins.md`, which replaced The Vault as the Act 1 default), **Ashen Wilds** or **The Drowned Quarter**
(see `DrownedQuarter.md`). Act 3 only has The Vault for now. Saves made before
this update have no themes, so they keep playing The Vault.

The map shows the act's theme under the summit boss label. Hover it to see the tagline.

## Files

| File | What it holds |
|---|---|
| `Core/AshenWildsContent.cs` | `ActThemes` (theme ids, names, roll), all Ashen Wilds enemy definitions, starting Minions and the 26 authored formations |
| `Combat/WildCombat.cs` | Shared Owner / Minion / Summon / Command rules, every Ashen Wilds AI pattern, the threshold phases and the mechanic hooks |
| `UI/GildedWildPresentation.cs` | Minion badge, Summon / Command intent icons, state text in tooltips, hook callouts, Cinder Alpha phase art and placeholder motion profiles |
| `Resources/Art/Enemies/ashen_wilds_*.tga` | Enemy art (19 images, including the two extra Cinder Alpha phases) |
| `Resources/Art/UI/Meta/MinionIcon.tga`, `SummonIcon.tga` | Minion badge / Command intent, and Summon intent |

The legacy enemies are kept and unchanged. Ashen Wilds routing never picks them: `WorldContent.IsLegacy`
separates the two pools.

## Roster

| Enemy | Role | HP |
|---|---|---|
| Cinderfang | Normal | 36 |
| Barkhide Grazer | Normal | 58 |
| Emberwing | Normal, Support (never alone) | 37 |
| Thornjaw | Normal | 47 |
| Ash Stalker | Normal | 43 |
| Rootcaller | Normal, Owner (summons Ash Saplings, cap 2) | 49 |
| Mourning Elk | Normal | 55 |
| Hollow Maw | Normal (HUNGRY → FED → BURNING) | 65 |
| Ash Sapling | Minion | 15 |
| Packmother | Elite, starts with Fang Pup (20) + Ashback Cub (24) | 106 |
| Burned Hart | Elite, crown thresholds at 2/3 and 1/3 HP | 118 |
| Root Titan | Elite, 5-turn Rooted / Uprooted cycle | 126 |
| Cinder Alpha | Boss, 3 phases, starts with Cinder Whelp (22) + Ember Runner (20) | 225 |

### Rules

- **Group HP:** normal enemies fighting alongside another non-Minion start at about 90% HP. Minions,
  elites and bosses always use full HP.
- **Battlefield size:** at most 6 living bodies.
- **Minions:** a Minion dies when its Owner dies. Minions give no rewards and do not count toward
  "enemies defeated".
- **Summon:** creates a fresh Minion during the Owner's action. It does not act on the turn it
  arrives. A dead Minion's slot is reused.
- **Command:** a random eligible Minion repeats its last move. A Minion is eligible if it is alive, has
  already acted and was not summoned this turn.
- **Intents:** an intent is decided once, when it is shown. It only changes when the prompt allows it:
  - Mourning Elk reacts to a death on its next normal turn.
  - Moves that depend on Minions are replanned when those Minions die.
  - Hart and Alpha threshold transitions replan the intent.
- **Support:** if only harmless Support enemies are left (Emberwing), they flee and the combat ends.
- **Cinder Alpha phases:**
  - Phase 2: +1 Strength and the `_phase2` art.
  - Phase 3: its Minions flee and it switches to the `_phase3` art.
- **Fate Debt:** scales each creature by its own class. Minions use the normal-enemy scaling, and
  summoned Minions get the same HP and Strength scaling.

## Formations

The first two Act 1 combats use only **Easy** formations. After that, the chance of a **Dangerous**
formation is:

| Floor | Dangerous chance |
|---|---|
| 1–6 | 0% |
| 7–11 | 40% |
| 12+ | 70% |

Otherwise the combat uses a **Standard** formation. The same formation is never picked twice in a row.

| Tier | Formations |
|---|---|
| Easy (E01–E06) | Cinderfang · Cinderfang ×2 · Grazer · Thornjaw · Stalker · Elk |
| Standard (S01–S10) | Cinderfang+Grazer · Cinderfang+Emberwing · Stalker+Cinderfang · Rootcaller+Sapling · Thornjaw+Cinderfang · Maw · Elk+Cinderfang · Grazer+Emberwing · Stalker+Grazer · Rootcaller |
| Dangerous (D01–D10) | Grazer+Emberwing+Cinderfang · Stalker+Cinderfang×2 · Rootcaller+Grazer · Thornjaw+Stalker+Cinderfang · Elk+Emberwing+Cinderfang · Maw+Emberwing · Stalker+Elk · Rootcaller+Thornjaw · Grazer+Elk+Cinderfang · Thornjaw+Emberwing+Stalker |

- Elite nodes pick one of the three Ashen elites, and the pick is stable for that node.
- The boss node is always Cinder Alpha.
- Rewards are unchanged.

## Hooks for final animation work

Every enemy move emits a `CombatEventKind.Hook` event. Its label is `HOOK:<name>`, for example:

- `cinderfang_pounce`
- `thornjaw_thornburst`
- `rootcaller_summon`
- `rootcaller_command`
- `packmother_bereaved_fury`
- `cinder_alpha_major_attack`

These state hooks also fire:

- `hollow_maw_state:<STATE>`
- `burned_hart_splintered`
- `burned_hart_bare`
- `cinder_alpha_phase2`
- `cinder_alpha_phase3`
- `cinder_alpha_minion_flees`
- `minion_withers`
- `support_flees`
- `command_answer`

`GildedMainMenu.ScheduleWildHook` receives them all. It shows a short callout today, and that is where
the real animations should be attached later.

Until bespoke animation sheets exist, Ashen Wilds enemies reuse the existing motion archetypes (Beast,
Crawler, Caster, Brute, Colossus, Spirit). See `WildMotionFor` and `WildBodyScale`.

## Adding a future theme or Act 1 neutrals

1. Add a content file like `Core/DrownedQuarterContent.cs`: enemy definitions with `theme` set (and
   `minion` / `support` where needed), elites, bosses, starting Minions and formations that use
   `EncounterDef.theme`.
2. Register it in `ThemeRosters` (same file as the Drowned Quarter content): `Find`, `AllEnemies`,
   `Formations`, `Elites`, `Boss`, `StartingMinions`, and `SummonType` / `MinionCap` for summoners.
   Combat, routing, checkpoints and the Playground all read from there.
3. Add the theme id to `ActThemes.ForAct` (plus `Name` / `Tagline`).
4. Add the AI in its own partial file, like `Combat/DrownedCombat.cs`, and chain it from the
   `default` branches in `WildCombat.cs`.
5. For neutrals, give the formation the theme `EncounterContent.Neutral` and set `minimumAct` and
   `maximumAct` to the same act. Themed selection already includes neutral formations for that act
   only, so they can join any theme in their act but never another act.
