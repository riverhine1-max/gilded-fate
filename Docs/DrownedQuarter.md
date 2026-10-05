# Act 1 · Theme 3 — The Drowned Quarter

A city district sinking beneath dark water. The combat identity is **delayed threats**: enemies
submerge, raise anchors, build Pressure and lose Strings. The player always sees what is coming
next, either in the intent or in the enemy's state text.

Each run rolls one theme per act, and Act 1 rolls one of three:

- The Vault, which stands in for Gilded Ruins
- Ashen Wilds
- The Drowned Quarter

The map shows the theme under the summit boss label.

## Files

| File | What it holds |
|---|---|
| `Core/DrownedQuarterContent.cs` | Enemy definitions, elites, boss, starting Minions, 26 formations, and `ThemeRosters` (the shared registry every theme plugs into) |
| `Combat/DrownedCombat.cs` | All Drowned Quarter AI, states, counters, thresholds, hooks and tooltip text |
| `UI/GildedWildPresentation.cs` | Hook callouts, Magistrate phase art, placeholder motion profiles and body scales |
| `Resources/Art/Enemies/drowned_quarter_*.tga` | 18 images: 16 enemies plus two extra Magistrate phases |

Ashen Wilds and the legacy enemies are untouched. Drowned Quarter routing never uses them.

## Roster

Group HP applies to normal enemies that start next to another non-Minion. Minions, elites and the
boss always use full HP.

| Enemy | Role | HP (group) | Notes |
|---|---|---|---|
| Rustwalker | Normal | 38 (34) | Cleaver 8 → Brace 10 Block → Advance 6 + 5 Block |
| Drowned Lurker | Normal | 42 (38) | SURFACE: Drag Below 5 + 8 Block → SUBMERGED: Surface Strike 14 |
| Bell Diver | Normal | 45 (41) | Swing 8 → Toll Weak + 6 Block → Resonance Vulnerable + 7 → Deep Toll 11 + Weak |
| Rust Priest | Normal, pure Support (never alone) | 39 (35) | Iron Prayer → Patchwork Blessing / Benediction → Benediction → Flooded Rite |
| Canal Stalker | Normal | 43 (39) | Cut 7 → Watch the Water 8 Block → Ambush 10 / 14 → Retreating Slash 6 + 7 |
| Tidecaller | Normal, Owner (Drowned Hands, cap 2) | 50 (45) | Summon, Undertow, Deep Command, Restore the Drowned |
| Drowned Hand | Minion | 15 | Grasp 5 → Dragging Grip 4 + Weak |
| Barnacle Hulk | Normal | 62 (56) | SHELLED until 30 HP or less, then EXPOSED for good |
| Flooded Marionette | Normal | 54 (49) | Strings 3/3. Loses one every action and never regains them |
| The Ferryman | Elite | 112 | Dragging Anchor / Anchor Raised: Chain Drag → Raise → Drop 22 → Chain Drag → Raise → Crushing Wake |
| The Bellkeeper | Elite, starts with Toll Thrall (21) + Sinker Thrall (25) | 96 | Chorus → Grand Toll → Command → Grand Toll; alone: Final Ring → Grand Toll → Final Ring |
| The Sunken Engine | Elite | 124 | Pressure 0–4 |
| The Drowned Magistrate | Boss, 3 phases, Bailiff Echo (22) Minion, cap 1 | 220 | Phase 2 at 146 HP, phase 3 at 73 HP |

### Rules worth knowing

- **Submerged enemies stay targetable.** Submerged only changes the Lurker's Block, its next move
  and its state text.
- **Canal Stalker Ambush** checks whether the player ended their previous turn with 0 Block. This
  is checked at the moment the Ambush is chosen, so the shown 10 or 14 never changes afterwards.
- **Rust Priest:**
  - Patchwork Blessing only mends constructs and armored allies (Rustwalker, Bell Diver, Barnacle
    Hulk, Flooded Marionette, Sunken Engine). With no damaged one, it uses Corroded Benediction.
  - If only Supports remain, they flee and the combat ends.
- **Sunken Engine.** This was confirmed with the designer: as written, Pressure could never reach 4
  before Vent reset it, so Burst Valve never fired. The engine now builds, then bursts:

  | Cycle | Turns |
  |---|---|
  | Pressure not yet full | Intake · Strike 12 · Compress · Strike 16 · Intake |
  | Pressure full | Intake · Strike 18 · Compress · **Burst Valve 7 × 3** · Vent (14 Block) |

  The two cycles alternate. Pressure Strike deals 10 + 2 per Pressure. Burst Valve and Vent reset
  Pressure to 0.
- **Drowned Magistrate:**
  - Phases change at 2/3 and 1/3 of max HP (146 and 73 at base HP). Each transition fires once and
    gives no heal, Strength, Fortify or Block.
  - The Bailiff survives the move to phase 2. It sinks at phase 3, and the boss gains nothing.
  - In phase 2, Drowned Order Commands a living Bailiff (+7 Block) or summons one (no Block). If the
    Bailiff dies before the order resolves, the intent updates to Summon.
- **Command** picks a random eligible Minion owned by that enemy. Minions summoned this turn, or
  that have not acted yet, are not eligible.
- **Fate Debt** scales each creature by its class. Summoned Hands and Bailiffs get the same summon
  scaling as Ashen Wilds Minions.
- **Rewards** use the existing tables, which already match the spec:

  | Fight | Gold | Cards | Other drops |
  |---|---|---|---|
  | Normal | 18 | 60/35/5 | Relic 3% |
  | Elite | 34 | 35/50/15 | Relic 50%, Shard 7% |
  | Boss | 100 | Rare | Boss relic choice |

  Minions give nothing.

## Formations

These follow the same tier rules as Ashen Wilds:

- The first two Act 1 combats are always Easy.
- After that, Standard and Dangerous formations take over, with Dangerous growing more likely
  deeper into the act.
- The same formation never comes up twice in a row.

| Tier | Formations |
|---|---|
| Easy (E01–E06) | Rustwalker · Rustwalker ×2 · Lurker · Bell Diver · Canal Stalker · Marionette |
| Standard (S01–S10) | Rustwalker+Lurker · Rustwalker+Priest · Bell Diver+Rustwalker · Tidecaller+Hand · Canal Stalker+Lurker · Hulk · Marionette+Rustwalker · Bell Diver+Canal Stalker · Rustwalker+Hulk · Tidecaller |
| Dangerous (D01–D10) | Hulk+Priest+Rustwalker · Bell Diver+Canal Stalker+Rustwalker · Tidecaller+Hulk · Lurker+Bell Diver+Canal Stalker · Marionette+Priest+Rustwalker · Lurker ×2+Rustwalker · Hulk+Bell Diver · Tidecaller+Canal Stalker · Marionette+Lurker+Bell Diver · Hulk+Canal Stalker+Priest |

## Save / load

- All per-enemy state lives in `WildMind`, which saves with the combat checkpoint:
  - states (Submerged, Shelled/Exposed, Anchor)
  - counters (Strings, Pressure)
  - pattern position, planned move and planned value
  - owner and Minion ids, last actions
  - boss phase
- The player's last end-of-turn Block (`playerEndBlock`) is saved too.
- Reloading never rerolls a planned move, resets a counter, duplicates a Minion or retriggers a
  phase.
- Every combat starts from fresh state.

## Hooks for final animation work

Each hook is emitted as a `CombatEventKind.Hook` event labelled `HOOK:<name>`. They are all received
in `ScheduleWildHook`.

| Enemy | Hooks |
|---|---|
| Drowned Lurker | `drowned_lurker_submerge`, `drowned_lurker_surface_strike` |
| Bell Diver | `bell_diver_toll` |
| Tidecaller | `tidecaller_summon`, `tidecaller_command` |
| Drowned Hand | `drowned_hand_spawn`, `drowned_hand_death` |
| Barnacle Hulk | `barnacle_hulk_shell_break` |
| Flooded Marionette | `marionette_string_loss` |
| The Ferryman | `ferryman_raise_anchor`, `ferryman_anchor_drop`, `ferryman_crushing_wake` |
| The Bellkeeper | `bellkeeper_toll`, `bellkeeper_command` |
| The Sunken Engine | `sunken_engine_pressure`, `sunken_engine_burst_valve`, `sunken_engine_vent` |
| The Drowned Magistrate | `drowned_magistrate_summon`, `drowned_magistrate_order`, `drowned_magistrate_phase2`, `drowned_magistrate_phase3`, `drowned_magistrate_final_sentence` |
| Bailiff Echo | `bailiff_echo_removed` |
| Any Minion | `command_answer`, `minion_withers` |

Until bespoke animation sheets exist, all enemies reuse the existing motion archetypes.
