# Act-Specific Neutral Enemies (Prompt 11)

Twelve enemies shared by the three themes of one act. "Neutral" is an internal term only: the player never sees it.
One gameplay definition per enemy; the picture follows the current theme.

| Act | Themes | HP | Normals | Elite |
|---|---|---|---|---|
| 1 | Gilded Ruins / Ashen Wilds / Drowned Quarter | 90% | Unbound Blade 40, Fateworn 46, Stray Idol 54 | The Wayfarer 116 |
| 2 | Crimson Foundry / Hollowwood / Shattered Observatory | 92% | Ragged Vanguard 68, Shifting Husk 80, Crooked Oracle 70 | The Deepcrawler 174 |
| 3 | Black Cathedral / Fractured Realm / Gilded Throne | 94% | Iron Wanderer 96, Pale Chimera 100, Nameless Seer 94 | The Worldbreaker 252 |

Ids are `act<N>_neutral_<name>`. Code: `Core/NeutralContent.cs` (roster, formations, art variant ids), `Combat/NeutralCombat.cs` (moves, hooks, state, thresholds, counters).
Neutral enemies never appear outside their act (normals, elites, debug/random generators, Daily).

## Encounters
- `A1N-E01..03`: Easy neutral-only Act 1 fights (all three Act 1 themes; allowed in the first two combats, never forced).
- `GR/AW/DQ/CF/HW/SO/BC/FR/GT-N01..06`: six mixed formations per theme.
- `A1/A2/A3-NO1..3`: neutral-only fights, the third of each act being the dangerous one.
- Maximum 4 enemies per combat; minions stand in front of their owner.
- Neutral share of normal fights (4,000-seed sample): Act 1 about 35%, Act 2 about 25%, Act 3 about 32%. No immediate repeat of the same formation when alternatives exist.
- Each theme's elite pool gains its act's neutral elite as a 4th. Elite nodes in an act draw from a seeded shuffle of the pool, so no elite repeats before all have appeared.

## Special states (state text only)
Wayfarer stances (Wanderer/Hunter/Survivor), Shifting Husk Armored then permanently Exposed at 40 HP or less, Crooked Oracle response to last turn's unused Energy,
Deepcrawler Surface/Burrowed (always targetable, Eruption warned), Pale Chimera Predatory/Guarded, Nameless Seer Sentence prepared,
Worldbreaker Charge 0/2 then "WORLD BREAK NEXT" (single 42 hit before Strength, not cancelled by damage).

## Art
No new art is required by the game. For each neutral the picture is looked up as `Assets/Resources/Art/Enemies/<id>_<theme>.tga` and falls back to `<id>.tga`.
Theme ids: gilded_ruins, ashen_wilds, drowned_quarter, crimson_foundry, hollowwood, shattered_observatory, black_cathedral, fractured_realm, gilded_throne.
