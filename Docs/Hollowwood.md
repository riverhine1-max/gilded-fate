# Act 2 · Theme 2 — Hollowwood

A forest that grows through everything. The combat identity is **Growth**: creatures that grow
toward a "Bloom" action, are accelerated by Supports, or transform at fixed health thresholds.

Act 2 rolls The Vault, The Crimson Foundry or Hollowwood (about one third each). Shattered
Observatory is still to come.

## Files

| File | What it holds |
|---|---|
| `Core/HollowwoodContent.cs` | 16 enemies, Growth users and starting Growth, Minions, 30 formations |
| `Combat/HollowwoodCombat.cs` | All Hollowwood AI, Growth, states, thresholds, Seeds, hooks, tooltip text, `HollowCounter` |
| `Combat/FoundryCombat.cs`, `WildCombat.cs`, `EnemyTurnPlan.cs` | Chain points into the shared engine; six new action types (Growth, GrowthSet, GrowthAlly, BlockOtherAllies, HealOwner, PlantSeed) |
| `UI/GildedWildPresentation.cs` | Growth pill and state pills, Growth intent label, hook callouts, Heartroot phase art and Parasite exposed art, motion profiles |
| `Resources/Art/Enemies/hollowwood_*.tga` | 19 images (16 enemies, Heartroot phases 2 and 3, Parasite exposed form) |

## Growth

- Growth is an enemy-specific counter from 0 to 3 stored in `WildMind.counter`. It is not Strength, Block or a status.
- It shows as a green **GROWTH X/3** pill; the tooltip says what happens at 3 (for example "At 3 Growth, uses Bloom Burst").
- Growth moves show **+1 Growth** in the intent. When an action will carry the enemy to 3, its name warns "· BLOOM BURST NEXT".
- Every Growth decision is made when the intent is chosen, so the shown intent is the one that lands.
- Growth users: Sproutling, Hollow Stag (starts at 1), Brood Pod, Bloomfang, Garden Mother (starts at 1), Heartroot.

## Roster

HP shown as solo (group). Group HP is about 92% and applies to normals that start beside another non-Minion.

| Enemy | HP | Pattern |
|---|---|---|
| Sproutling | 48 (44) | Root Peck 8 → Curl Leaves 9 Block → Root Peck, each +1 Growth. At 3: Bloom Burst 16 + Weak, Growth → 0 |
| Hollow Stag | 69 (63) | Antler Bash 11 → Bark Guard 15 → Antler Bash. At 3: Crown Bloom 16 + 10 Block, Growth → 1, then Rooted Recovery (heal 6 + 6 Block) |
| Sporekeeper | 50 (46) | Pure Support. Feed the Bloom (+1 Growth and 5 Block to the ally nearest 3; ties random) → Spore Veil (7 Block to allies) → Regenerative Mist (if an ally is hurt) or Mature the Grove → Mature the Grove |
| Root Snare | 65 (60) | Entangle (Weak + 11 Block, roots tighten) → Root Crush 17 (roots loosen) → Splinter Snap 10 + 6 → Root Crush |
| Brood Pod | 58 (53) | Incubate (+1 Growth, 7 Block) → Spore Lash 8 → Incubate. At 3: Hatch a Sporeling (Growth → 0); at the limit, Overgrown Shell (16 Block, Growth → 0). Commands a Sporeling in place of Spore Lash once it has 3 actions since its last Command |
| Sporeling | 19 | Spore Bite 6 → Puff Spores 4 + Weak. Minion, cap 2 per Pod |
| Bloomfang | 63 (58) | Closed: Bud Bite 9, Petal Guard 11 Block, Thorned Step 7 + 5 Block, each +1 Growth. At 3 it permanently becomes Full Bloom (no heal, Strength or Block): Blooming Rend 15 → Pollen Claw 10 + Vulnerable → Thorn Frenzy 8 × 2 |
| Hollow Parasite | 62 (57) | Hosted: Host Swipe 11 → Dead Shell 14 → Parasitic Pull 8 + heal 4. At half health (31) the host breaks once: Exposed: Skittering Bite 14 → Drain 10 + heal 5 → Frenzied Lunge 7 × 2 |
| Elder Husk | 78 (72) | Rooted: Ancient Guard 18 Block + heal 4 → Blooming: Elder Bloom 15 + 1 Strength → Withered: Dry Collapse 10 + Weak + 5 Block → Rooted |
| The Garden Mother | 158 | Elite. Starts with Thornbud (26) and Bloombud (24), Growth 1. Cultivate → Vine Lash → Command → Mothering Roots (or Vine Lash). Grand Bloom at 3 Growth (16 + all Minions +1 Strength, Growth → 1). Replace the Fallen once per encounter with one Huskbud (30) |
| The Walking Grove | 178 | Elite, one creature, three stages. Root-Woken (Root Hammer → Sink Deep → Root Sweep); at 118 Branch-Woken (Branch Crush → Splinter Guard → Canopy Sweep); at 59 Crown-Woken (Elder Crash → Living Canopy → Falling Grove). Waking gives nothing |
| The Pale Gardener | 160 | Elite. Plant Seed (9 Block, random Seed, never the same twice in a row) → resolve the Seed → Weeding Cut 13. Seeds: Thorn (20), Ward (26 Block then 6), Rot (2 Weak + 8), Bloom (heal 8, +1 Strength, 8 Block). The planted Seed is visible and cannot be cancelled |
| The Heartroot | 320 | Boss, no Minions. Phase 1 (Root Lash / Ancient Bark / Sap Draw, Heart Bloom at 3); at 214 Phase 2, Growth 1 (Uprooting Claw / Blooming Guard / Parasitic Pull, Spreading Bloom 10 × 2 + Vulnerable); at 107 Phase 3, Growth 0 (Heart Rend / Thornstorm 6 × 4 / Predator Bloom, Final Bloom 10 × 3 then 10 Block). Transitions give no heal, Strength, Fortify or Block |

Thresholds scale with Fate Debt max HP and are exact at base HP: Heartroot 2/3 and 1/3 rounded up (214 / 107),
Walking Grove rounded down (118 / 59), Hollow Parasite half rounded up (31).

## Design calls worth knowing

- **Sporekeeper targeting:** it accelerates the damage-dealing Growth ally *closest* to 3 (still below 3); ties are random. Mature the Grove gives +1 Growth to that ally and 4 Block to every other ally; when nobody can grow it gives all allies 6 Block.
- **Garden Mother priorities:** Grand Bloom (at 3 Growth) comes before Replace the Fallen, which comes before the normal pattern. Both keep the pattern position.
- **Brood Pod Command:** "every fourth legal action" is implemented as: the Spore Lash slot becomes Command when the Pod has done at least 3 actions since its last Command and a Sporeling can answer.
- **Intents that update:** a Pod, Garden Mother or Sporekeeper re-reads its move if an ally or Minion dies while the intent is shown (the move relied on it). Thresholds (Parasite, Grove, Heartroot) also update the intent.
- **Pale Gardener:** the Seed is picked when Plant Seed resolves, and is shown from then on. It persists through save/load.

## Formations

No protected opening pool in Act 2; tiers weight by depth exactly as in the Crimson Foundry. The same formation never repeats back to back. Max 4 starting bodies, max 6 living.

| Tier | Formations |
|---|---|
| Standard (S01–S10) | Sproutling+Stag · 2 Sproutlings · Root Snare+Sproutling · Brood Pod+Sporeling · Bloomfang · Parasite · Stag+Sporekeeper · Root Snare+Stag · Elder Husk · Bloomfang+Sproutling |
| Advanced (A01–A10) | 2 Sproutlings+Sporekeeper · Stag+Root Snare · Pod+Sproutling · Bloomfang+Sporekeeper · Parasite+Root Snare · Elder Husk+Sproutling · Stag+Bloomfang · Pod+Root Snare · Parasite+Sporekeeper · Elder Husk+Stag |
| Dangerous (D01–D10) | Stag+Sporekeeper+Sproutling · Bloomfang+Root Snare+Sporekeeper · Pod+Stag · Parasite+Bloomfang · Elder Husk+Sporekeeper+Sproutling · Pod+Bloomfang · Root Snare+Stag+Sproutling · Parasite+Elder Husk · 2 Sproutlings+Bloomfang+Sporekeeper · Elder Husk+Root Snare+Sporekeeper |

Elite rooms pick the Garden Mother, the Walking Grove or the Pale Gardener. The boss is the Heartroot. Rewards use the Act 2 table (see `CrimsonFoundry.md`).

## Hooks for final animation work

Emitted as `CombatEventKind.Hook` with the label `HOOK:<name>`, received in `ScheduleWildHook`.

| Area | Hooks |
|---|---|
| Growth (any user) | `growth_gain`, `growth_three` |
| Sproutling / Stag | `sproutling_bloom_burst`, `hollow_stag_crown_bloom` |
| Sporekeeper | `sporekeeper_acceleration` |
| Root Snare | `root_snare_tighten` |
| Brood Pod / Sporeling | `brood_pod_hatch`, `brood_pod_command`, `sporeling_spawn`, `sporeling_death` |
| Bloomfang | `bloomfang_full_bloom` |
| Hollow Parasite | `hollow_parasite_host_break` |
| Elder Husk | `elder_husk_state:<ROOTED/BLOOMING/WITHERED>` |
| Garden Mother | `garden_mother_cultivate`, `garden_mother_grand_bloom`, `garden_mother_command`, `garden_mother_huskbud_replacement`, `huskbud_spawn` |
| Walking Grove | `walking_grove_stage2`, `walking_grove_stage3` |
| Pale Gardener | `pale_gardener_seed_planted:<SEED>`, `pale_gardener_seed_resolved:<SEED>` |
| Heartroot | `heartroot_phase2`, `heartroot_phase3`, `heartroot_heart_bloom`, `heartroot_spreading_bloom`, `heartroot_final_bloom` |
| Any Minion | `command_answer`, `minion_withers` |

## Art notes

Placeholder motion reuses existing animation archetypes. The Hollow Parasite swaps to its exposed image
(`hollowwood_hollow_parasite_phase2`) on Host Break; Bloomfang has a single image for both forms.
