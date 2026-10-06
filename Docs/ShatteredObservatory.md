# Act 2 · Theme 3 — Shattered Observatory

A broken tower that reads the future. The combat identity is **Prediction**: creatures that queue their
own Future Intent, show two possible next actions, preview three actions ahead, or react to the Energy
you spent on your previous turn.

Act 2 now rolls four ways (about a quarter each): The Vault, The Crimson Foundry, Hollowwood and the
Shattered Observatory. Observatory elites and the Astral Curator appear only in this theme.

## Files

| File | What it holds |
|---|---|
| `Core/ShatteredObservatoryContent.cs` | 15 enemies, 3 elites, the boss, Minions, 30 formations (data only) |
| `Combat/ObservatoryCombat.cs` | All Observatory AI, queues, pairs, Plates, Momentum, Energy memory, thresholds, hooks, forecast and tooltip text |
| `Combat/HollowwoodCombat.cs`, `WildCombat.cs`, `EnemyTurnPlan.cs`, `CombatModel.cs` | Chain points into the shared engine; new action types PlateSpend, PlateGain, Momentum, MomentumReset; `choice` on intent actions; Energy tracking in `Play`/`NextTurn`; `PreResolveObservatory()` before the enemy turn is read |
| `UI/GildedWildPresentation.cs` | Orbit Plates and Momentum pills, `DrawWildForecast` (Future Intent, Future, Next/Following, Possible Next Actions), Lenskeeper tooltip, callouts, Curator phase art, motion profiles |
| `Resources/Art/Enemies/shattered_observatory_*.tga` | 17 images (15 enemies, Curator phases 2 and 3) |

## Fairness rules

- A shown deterministic intent never secretly changes.
- A shown set of possibilities is always complete. The random branch resolves only when the enemy acts.
- Queues, pairs, Plates, Momentum, the Seer queue, Chronoglyph Current/Future and Energy memory are plain data on `WildMind` and persist through save/load without rerolling.
- RNG is used only for Command targets, future-action selection (Chronoglyph, Blind Seer), Fallen Astrologer pair selection and pick, and Astral Curator Phase 3 pair selection and pick.
- No real-time interrupts. No special easy pool in Act 2.

## Roster

HP shown as solo (group). Group HP is about 92% and applies to normals that start beside another non-Minion. Minions, elites and the boss use full HP.

| Enemy | HP | Behaviour |
|---|---|---|
| Starbound Scribe | 52 (48) | Astral Bolt 10 → Chart the Stars 9 Block, queues Falling Star → Falling Star 15 (shown as Future Intent, then Current) → Arcane Margin 7 + 6 Block |
| Orbiting Sentinel | 68 (63) | Plates 3/3. Orbit Guard (5 Block per Plate) → Launch Plate (spend 1 Plate, 7 + 5 Block) → Orbital Strike (10 + 3 per missing Plate) → Reassemble Orbit (+1 Plate, 7 Block). Plates change only through its own actions |
| Astral Attendant | 49 (45) | Pure Support. Celestial Ward 10 Block to the lowest-HP damage-capable ally → Stellar Guidance (random ally +1 Strength, 5 Block) → Correct the Orbit (heal 6, if an ally is hurt) → Astral Alignment (5 Block to others, +1 Strength to one random damage-capable ally) |
| Chronoglyph | 57 (52) | Always shows Current and Future. Time Cut 11, Delay Ward 14 Block, Temporal Fracture 7 + 1 Vulnerable, Future Collapse 17. Future becomes Current when it acts and a new Future is drawn (never equal to the new Current) |
| Lenskeeper | 60 (55) | Reads Energy spent last player turn. 0–1: Underexposed Beam 15. 2: Balanced Lens 11 + 7 Block. 3+: Overexposed Ward 16 Block then 7 damage. No previous turn: Balanced Lens |
| Constellation Weaver | 61 (56) | Form Constellation (Star Fragment, 5 Block, cap 2) → Stellar Thread 9 → Command or Form → Realign (heal a Fragment 6 + 5 Block) |
| Star Fragment | 20 | Minion. Star Pulse 6 → Orbit Guard 7 Block |
| Gravity Monk | 66 (61) | Weighted Palm 10 + Weak → Gravity Guard 6 + 12 Block → Compression +1 Strength + 8 Block → Collapse Point 17 |
| Fallen Astrologer | 70 (64) | Twin Prediction: two possibilities from Starfall 16, Astral Guard 18 Block, Falling Omen 9 + Weak, Celestial Surge +1 Strength + 9 Block. One is picked when it acts. Never the same pair twice in a row |
| The Orrery Keeper | 150 | Elite. Starts with Sun Fragment (28) and Moon Fragment (30). Celestial Rotation → Star Measure 14 → Command → Grand Alignment (14, 17 with two Fragments, +1 Strength to Minions) |
| Sun / Moon Fragment | 28 / 30 | Minions. Solar Flare 10 → Radiant Surge (+1 Strength, 6 damage). Lunar Guard (9 Block to Keeper + 5) → Crescent Strike 8 |
| The Blind Seer | 156 | Elite. Shows Current / Next / Following. Pool: Seer's Cut 15, Foresight Ward 21 Block, Doomed Vision (Weak + Vulnerable), Predicted Ruin 20, Calm Future (+1 Strength, 10 Block). Never the same action three times in a row |
| The Fallen Comet | 168 | Elite. Momentum 0/4. Accelerate (+1 Momentum, 8 Block) → Comet Strike (12 + 2 per Momentum) → Accelerate → Falling Arc (2 hits of 6 + Momentum). At 4 Momentum: Impact 26, Momentum resets, then Cool Orbit (15 Block) |
| The Astral Curator | 310 | Boss, thresholds 207 and 103. Phases 1 and 2 show a Future Intent (Archive the Future / Predicted Collapse queue the next move). Phase 3 is Twin Fate: two possibilities from Stellar Execution 21, Constellation Barrage 6 × 4, Celestial Fortress 22 Block + 1 Strength, Gravity Sentence 13 + Vulnerable, Astral Surge +2 Strength + 8 Block. Transitions give no heal, Strength or Block and clear the queue |

## Design rulings

- **Energy spent** means Energy paid on cards (not Energy lost, gained or refunded).
- **Pairs are shown in canonical pool order,** so the display never hints at which one will be picked. A pair intent promises 0 damage in the generic intent icon; the pair display carries the detail.
- **Pair plans are previewed without picking.** An intent preview writes both possibilities, and only `ResolveEnemyTurn` makes the pick.
- **Radiant Surge** applies Strength before its 6 damage, so it is shown and dealt as 7.
- **Curator Future Intent** is the next pattern move (or the queued move for Archive / Predicted Collapse).
- **Chronoglyph and Blind Seer** draw their random future lazily at their first plan, so RNG order matches the first time the enemy is shown.
- Summon at the six-body cap is illegal and falls back to Stellar Thread. Command with no Minion is illegal and falls back to another move.

## Hooks (CombatEventKind.Hook, "HOOK:name")

future_intent_generated, future_to_current, chronoglyph_future_to_current, chronoglyph_future_shift, chronoglyph_future_collapse, scribe_chart_stars, scribe_falling_star, sentinel_plate_launch, sentinel_plate_reassemble, lenskeeper_response:UNDER|BALANCED|OVER, weaver_form_constellation, weaver_command, weaver_realign, star_fragment_spawn, star_fragment_death, gravity_monk_collapse_point, astrologer_pair_reveal, astrologer_pair_chosen, orrery_celestial_rotation, orrery_keeper_command, orrery_grand_alignment, orrery_fragment_death, blind_seer_queue_shift, blind_seer_predicted_ruin, fallen_comet_momentum, fallen_comet_full_momentum, fallen_comet_impact, fallen_comet_cool_orbit, curator_archive_future, curator_predicted_collapse, curator_collapse_event, curator_grand_alignment, astral_curator_phase2, astral_curator_phase3, astral_curator_constellation_transformation, astral_curator_twin_fate_reveal, astral_curator_twin_fate_chosen, curator_twin_fate_resolved.

## Art matching

Art was matched by eye from the supplied pictures. Enemies whose picture was a guess are listed in the delivery notes; the five extra supplied pictures not used by any enemy are held outside the project.

## Prompt 11 (Act 2 neutrals)

Neutral enemies use theme `EncounterContent.Neutral`, act 2–2, and never roll through `ThemeRosters`, so they do not interact with the Observatory roster.
