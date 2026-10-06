# Act 2 · Theme 1 — The Crimson Foundry

An ancient weapons factory that never stopped making war. The combat identity is **Heat**:
machines grow more dangerous as they are pushed toward their limits, then vent or overload.

Act 2 rolls **The Vault** (the original Act 2 roster, standing in for Shattered Observatory until it
exists), **The Crimson Foundry** or **Hollowwood** (see `Hollowwood.md`). The map shows the theme under
the summit boss label.

## Files

| File | What it holds |
|---|---|
| `Core/CrimsonFoundryContent.cs` | Enemies, elites, boss, starting Minions, Heat users and starting Heat, 30 formations |
| `Combat/FoundryCombat.cs` | All Foundry AI, Heat / Load / Armor / mode logic, Iron Saint phases, hooks, tooltip text, and `WildCounter` (the counter shown on enemies) |
| `UI/GildedWildPresentation.cs` | Counter pill (`DrawWildCounter`), Heat / Load intent icons, hook callouts, Iron Saint phase art, placeholder motion |
| `Resources/Art/Enemies/crimson_foundry_*.tga` | 18 images: 16 enemies plus Iron Saint phases 2 and 3 |
| `Resources/Art/UI/Meta/HeatIcon.tga` | Heat icon for the counter pill and Heat intents |

The theme registers itself in `ThemeRosters` (in `DrownedQuarterContent.cs`). Act 1 themes and the
legacy enemies are untouched.

## Heat

- Heat is an enemy-specific counter from 0 to 4, stored in `WildMind.counter`.
- It is shown as a small **Heat X/4** pill on the enemy's top-right, with the Heat icon. The pill
  pulses at 4/4 (Overheated). Heat is not a player status and never uses the Strength icon.
- Heat moves show their Heat gain in the intent.
- Every Heat-dependent number is fixed when the intent is chosen, so it never changes after it is
  shown. This covers Heated Strike, Heated Bite, Redline Pounce, Masterwork Blow, Furnace Blade and
  the Overdrive Assault form.
- Heat clamps at 4, and resets every combat.

**Heat users:**

| Enemy | Starting Heat |
|---|---|
| Forgehand | 0 |
| Furnace Hound | 1 |
| Crucible Knight | 0 |
| Redline Automaton | 1 |
| Forgemaster | 1 |
| Iron Saint | 0 |

**Not Heat users:**

- Molten Carrier uses **Load 0/3**.
- The Smelter uses **Armor 3/3**.
- Prototype Zero shows its operating **mode**.
- The Chain Warden and Rivet Priest have no counter.

## Roster

Group HP (shown in brackets) is about 92% of base HP. It applies to normals that start beside
another non-Minion.

| Enemy | HP (group) | Pattern |
|---|---|---|
| Forgehand | 54 (50) | Hammer Blow 10 +1 Heat → Temper Plate 11 Block +1 → Heated Strike 10 + 2/Heat. At 4 Heat: Vent (7 Block), then resume |
| Furnace Hound | 50 (46) | Sprint 7 +2 → Redline Pounce 12 (16 at 4 Heat, needs 2+) → Heated Bite 9 + Heat, +1. At 4 Heat it prefers Pounce, then must Cool Down (8 Block) |
| Rivet Priest | 48 (44) | Support, never alone. Rivet Armor 9 → Stoke Furnace (+1 Heat + 5 Block to its hottest Heat ally below 4) or Reinforce Frame → Emergency Repair (heal 6 + 5) or Reinforce Frame → Reinforce Frame (+1 Strength, 7 Block) |
| Chain Warden | 61 (56) | Lash 10 → Binding Chain Weak + 8 Block → Drag Forward Vulnerable + 8 → Crushing Links 7 × 2. No Heat |
| Assembly Master | 59 (54) | Deploy Drone → Tool Strike 9 → Deploy / Command → Repair / Overclock → Tool Strike. Up to 2 Scrap Drones (20 HP: Cutter 6 → Plate Weld) |
| Crucible Knight | 72 (66) | Cold 0–1: Reinforced Guard / Measured Blade. Hot 2–3: Heated Cleave / Molten Guard. Overheated 4: Crucible Breaker 20, then Heat resets |
| Molten Carrier | 64 (59) | Collect Slag → Heavy Swing 11 → Add to Crucible …; at Load 3: Molten Spill 18 + Vulnerable, then Load resets |
| Redline Automaton | 70 (64) | Slash 10 → Boost Servos → Overdrive Assault (11 / 15 + 5 Block / 10 × 2 by Heat). After the 4-Heat form: Emergency Vent (−1 Strength, floor 0; 10 Block) |
| The Forgemaster | 148 | Elite, Heat 1. Hammer Drone (27) + Shield Drone (30). Stoke the Line → Masterwork Blow 14 + Heat → Command → Reassemble (once per combat) / Blow. At 4 Heat: Forge Overload 11 × 2 |
| The Smelter | 162 | Elite, Armor 3/3. Press → Melt Layer at each layer (13 + 14 / 16 + 9 / 19 + 5; Melt 18 / 13 / 8 Block), then Core Slam 22 → Core Flare → Liquid Metal Sweep 10 × 2. No heal or Strength from melting |
| Prototype Zero | 154 | Elite. Assault (17, 8 × 2) → Defense (22 Block, 8 + 14) → Overdrive (19 + Strength, 7 × 3), two actions per mode. Mode switches give nothing |
| The Iron Saint | 300 | Boss. Phase 2 at 200 HP, phase 3 at 100 HP; see below |

## The Iron Saint

| Phase | HP | What happens |
|---|---|---|
| 1 | 300–201 | Starts with a Saint Servitor (32 HP: Blade 8 → Reinforce Saint 10). Restrained Strike 14 → Assembly Guard 18 → Sacred Production (empower the Servitor, or resummon it once if dead) → Furnace Prayer |
| 2 | 200–101 | On entry: Heat becomes 1, the Servitor is destroyed, nothing else is gained. Furnace Blade 16 + Heat → Heated Guard 8 + 13 → Saint's Advance 9 × 2; at 4 Heat, Controlled Vent (15 Block, +1 Strength) |
| 3 | ≤ 100 | On entry: Heat becomes 4, with no heal, Block or Strength. The cycle is Redline Judgment (10 × 3, Heat resets) · Burning Guard · Violent Reheat · Saint Breaker · Violent Reheat · Furnace Barrage 6 × 4. Its major slots (1st, 4th and 6th) become Redline Judgment whenever Heat is 4 |

Each transition fires once and survives reload.

## Formations (Act 2)

**There is no protected opening pool in Act 2.** Every combat picks a tier from how deep into the act
it is, then picks a random formation from that tier. The same formation never comes up twice in a
row. Enemies are never limited to particular fight numbers.

| Floor | Standard | Advanced | Dangerous |
|---|---|---|---|
| 1–5 | 70% | 25% | 5% |
| 6–11 | 35% | 45% | 20% |
| 12+ | 15% | 40% | 45% |

| Tier | Formations |
|---|---|
| Standard (S01–S10) | Forgehand+Hound · Forgehand+Warden · Knight · Assembly Master+Drone · Carrier+Forgehand · Automaton · Hound+Warden · Forgehand+Priest · Knight+Forgehand · Carrier |
| Advanced (A01–A10) | Forgehand+Hound+Priest · Knight+Warden · Assembly+Forgehand · Automaton+Priest · Carrier+Warden · Hound ×2+Forgehand · Knight+Priest · Assembly+Hound · Automaton+Warden · Carrier+Hound |
| Dangerous (D01–D10) | Knight+Hound+Priest · Automaton+Warden+Forgehand · Assembly+Knight · Carrier+Priest+Hound · Automaton+Priest+Warden · Knight+Carrier · Assembly+Warden+Forgehand · Forgehand ×2+Priest+Hound · Automaton+Knight · Carrier+Automaton |

Elite rooms pick the Forgemaster, the Smelter or Prototype Zero. The boss is always the Iron Saint.

## Rewards (Act 2)

The central `RewardRules` table now has an act layer. **Every** Act 2 run uses it, including the
Vault Act 2. Acts 1 and 3 are unchanged.

| Fight | Gold | Cards (common / uncommon / rare) | Relic | Shard |
|---|---|---|---|---|
| Normal | 21 | 50 / 40 / 10 | 4% | — |
| Elite | 40 | 25 / 55 / 20 | 55% | 9% |
| Boss | 110 | Rare | Boss relic choice | — |

Minions give nothing.

## Hooks for final animation work

Each hook is emitted as `CombatEventKind.Hook` with the label `HOOK:<name>`, and is received in
`ScheduleWildHook`.

| Area | Hooks |
|---|---|
| Heat (any Heat user) | `foundry_heat_gain`, `foundry_overheated`, `foundry_vent` |
| Furnace Hound | `furnace_hound_redline_pounce` |
| Rivet Priest | `rivet_priest_stoke` |
| Assembly Master | `assembly_master_summon`, `assembly_master_repair`, `assembly_master_command` |
| Scrap Drone | `scrap_drone_spawn` |
| Crucible Knight | `crucible_knight_state:<COLD/HOT/OVERHEATED>` |
| Molten Carrier | `molten_carrier_spill` |
| Redline Automaton | `redline_automaton_overdrive` |
| Forgemaster | `forgemaster_overload`, `forgemaster_reassemble`, `forgemaster_command` |
| Smelter | `smelter_layer_break`, `smelter_exposed_core` |
| Prototype Zero | `prototype_zero_mode:<MODE>` |
| Iron Saint | `iron_saint_phase2`, `iron_saint_phase3`, `iron_saint_sacred_production`, `iron_saint_redline_judgment` |
| Saint Servitor | `saint_servitor_destroyed` |
| Any Minion | `command_answer`, `minion_withers` |

## Future Act 2 themes and neutrals

- **Shattered Observatory:** add a content file, register it in `ThemeRosters` (the way
  `HollowwoodContent` is), and add the theme to `ActThemes.ForAct(2)`.
- **Act 2 neutrals** (Ragged Vanguard, Shifting Husk, Crooked Oracle, The Deepcrawler): give their
  formations the theme `EncounterContent.Neutral` with act 2–2. They then join every Act 2 theme
  automatically.
