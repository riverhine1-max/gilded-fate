# Act 1 · Theme 1 — The Gilded Ruins

A dead kingdom whose systems still run. The combat identity is **Seized Wealth** (enemies take Gold from
you and hold it until they die) plus **Formation Priority** (who stands beside whom changes what each
creature does).

Act 1 rolls three ways (about a third each): The Gilded Ruins, Ashen Wilds and The Drowned Quarter. Ruins
elites and The Last Procession appear only in this theme. The Vault's legacy Act 1 enemies are kept as
definitions but are no longer routed into Act 1; Acts 2 and 3 are unchanged.

## Files

| File | What it holds |
|---|---|
| `Core/GildedRuinsContent.cs` | 14 enemies, 3 elites, the boss, starting Minions and 26 formations (data only) |
| `Combat/RuinsCombat.cs` | All Ruins AI, Seized Gold, Bonus Gold, Reserve, Lots, Duelist memory, thresholds, hooks, tooltip text |
| `Combat/WildCombat.cs`, `ObservatoryCombat.cs`, `EnemyTurnPlan.cs`, `CombatModel.cs` | Chain points into the shared engine. New action types SeizeGold, BonusConsume, Fortify, ReserveSpend, ReserveAll, BlockLowestNonMinion |
| `Core/AshenWildsContent.cs`, `Core/DrownedQuarterContent.cs` | `ActThemes.GildedRuins`, `ThemeRosters` registration |
| `UI/GildedWildPresentation.cs` | Hook callouts, Seized Gold pill, Reserve / Bonus Gold counters, text icons, motion profiles, Procession phase art |
| `UI/GildedMainMenu.cs` | Starts combat with your real Gold, returns Seized Gold on victory, adds Bonus Gold to the reward |
| `Resources/Art/Enemies/gilded_ruins_*.tga` | 16 images (14 enemies, Procession phases 2 and 3) |

## Seized Gold rules

- Combat uses its own copy of your Gold (`CombatState.playerGold`) and each enemy tracks what it holds. Your run Gold is never lowered during combat, so Gold cannot be lost permanently.
- An enemy can never take more than you have. Gold never goes below 0.
- Held Gold returns immediately when its holder dies, and anything still held returns when you win. A second return finds nothing, so it cannot double refund.
- Bonus Gold (Coin Mimic, Crown Collector) is not yours. It is added to the reward only if you win, separately from Seized Gold.
- Held Gold appears on the enemy ("HOLDS n GOLD"), in a Gold line near your portrait while any is held, and in tooltips.
- Save and load restore Gold, held Gold, Bonus Gold, Reserve, announced Lots and Duelist memory without rerolling.

## Fairness rules

- Every intent shows its real value. Dynamic values (Desperate Cut with held Gold, Asset Release, Foreclosure, Appraiser result) are read live and re-read when the player changes them mid-turn.
- Enemy Fortify adds to every later Block that enemy gains, and the intent shows it.
- RNG is used only for Command targets, Chorister target ties, Bastion Interpose ties and the Royal Auctioneer's Lot.
- Minions follow the shared Owner/Command rules. At most 4 enemies start a fight and at most 6 are ever alive.
- The first two Act 1 combats use only Easy formations.

## Roster

HP shown as solo (group). Group HP is about 90% and applies to normals that start beside another non-Minion.

| Enemy | HP | Behaviour |
|---|---|---|
| Giltblade Scavenger | 34 (31) | Ragged Slash 7 → Pocket the Spoils (Seize 3 + 4) → Desperate Cut 11, or 13 while holding Seized Gold |
| Oathbound Bastion | 52 (47) | With company: Interpose (10 Block to the lowest-HP non-Minion ally, ties random; Brace 13 if none) → Shield Crush 7 + 6 Block → Hold the Line (6 Block + 8 to the lowest ally, 14 alone). Alone: Brace → Crush → Crush |
| Gilded Chorister | 38 (34) | Pure Support, never solo. March of Gold (+1 Strength to a random damage-capable ally) → Royal Refrain (7 Block to the others) → Grand Chorus (6 Block + 1 Strength to one random) |
| Tarnished Appraiser | 44 (40) | Appraise Wealth (6 Block, then queues by your Gold) → Overvalued (Seize 6 + 7 Block) if you hold 50 or more, otherwise Worthless (6 + 1 Weak) → Marked Asset (1 Vulnerable + 7). Re-evaluated every appraisal |
| Reliquary Keeper | 50 (45) | Awaken Servitor (summon + 5 Block) → Relic Slam 8 → one Servitor: summon a second, two: Command + 5 Block → Repair Rite (heal lowest Servitor 8 + 6 Block) if legal, else Slam |
| Gilded Servitor | 16 | Minion. Servitor Jab 5 → Brace Frame 6 Block. Cap 2 |
| Coin Mimic | 48 (43) | Holds 18 Bonus Gold. Glittering Bait 5 Block → Snap Shut 12 → Hoard (spend 4 Bonus: 12 Block) → Devour Value (spend 4 Bonus: 15). Remaining Bonus Gold is added to the reward |
| Bellbound Herald | 46 (41) | First Toll (5 Block to others, 9 alone) → Second Toll (1 Weak + 5) → Third Toll (13 + 1 Strength) |
| Crownless Duelist | 54 (49) | Reads your previous turn. 3+ cards, Attack-majority: Punishing Guard (15 Block + 6). Non-Attack-majority: Relentless Advance (11 + 1 Strength). Otherwise Measured Cut (9 + 5 Block) |

### Elites

| Elite | HP | Behaviour |
|---|---|---|
| Crown Collector | 98 + 2 Coinbound Guards (23) | Collect Due (Seize 8 + 10) → Royal Levy (Seize 5 + 8 Block to Guards) → Collection Order (Command + 8 Block) if legal, else Foreclosure → Repossess (summon a Guard + 10 Block, once per fight) if legal, else Foreclosure. Foreclosure 18, or 22 while holding 10+ Seized Gold. Reward +10 Bonus Gold. Guards: Taxblade 7 → Guard the Collector (8 Block to the owner + 4 to self) |
| Royal Auctioneer | 118 | Open Bidding (9 Block, announces a random Lot, never the same twice in a row) → the Lot → Hammer Fall 16. Lots: Blades 24 + 1 Strength, Protection 25 Block, Misfortune 2 Weak + 1 Vulnerable + 5, Tribute Seize 10 + 10. The Lot cannot be cancelled and survives save/load |
| Living Treasury | 108 | Reserve 0/6, +1 per Attack card that deals unblocked damage (once per card). Vault Slam 14 → Lockdown (spend 2: 20 Block, else Vault Slam) → Asset Release (spend all: 10 + 3 per Reserve). Emergency Reserve once, below 35% HP: spend up to 3, heal 6 each, 10 Block, taking priority |

### Boss — The Last Procession (210, solo, no Minions)

Phase thresholds are 2/3 and 1/3 of maximum health (140 and 70 at base, scaling with Fate Debt). A phase change gives no heal, Strength, Fortify or Block, and the pattern restarts.

| Phase | Moves |
|---|---|
| 1 · The Ceremony | Royal Salute 11 + 8 Block → Collect Tribute (Seize 6 + 8 Block) → Processional Guard 19 Block → Golden Fanfare +2 Strength |
| 2 · The Broken Parade | Gilded Ram 16 + 8 Block → Coin Barrage 4×4 → Forced March 10 + 1 Strength → Seize the Streets (Seize 8 + 12) |
| 3 · The Crown Engine | Crown Hammer 18 + 10 Block → Final Tribute (Seize 10 + 14) → Royal Furnace (+2 Strength, +1 Fortify) → End of the Procession 5×5 |

Debuffs an enemy applies before attacking in the same intent are included in the shown damage (a fresh Vulnerable lands on the same intent's attack), as in the other themes.

## Formations

Easy: GR-E01 to GR-E06. Standard: GR-S01 to GR-S10. Dangerous: GR-D01 to GR-D10. Exact lists are in `GildedRuinsContent.cs`.

## Rewards

Existing centralized rules: normal 18 Gold (3 card choices, 60/35/5, 3% relic), elite 34 Gold (35/50/15, 50% relic, 7% shard), boss 100 Gold (3 Rare, boss relic). Bonus Gold is added on top before Lucky Coin and Fate Debt.

## Combat hooks

`seize_gold`, `gold_returned`, `chorister_support`, `bell_toll_1/2/3`, `servitor_awaken`, `servitor_spawn`, `servitor_death`, `coinbound_guard_spawn`, `coinbound_guard_death`, `ruins_command`, `collector_repossess`, `collector_foreclosure`, `duelist_response:ATTACKS|SKILLS|MIXED`, `auction_lot_announced`, `auction_lot_resolved`, `reserve_gain`, `asset_release`, `emergency_reserve`, `bonus_gold_consumed`, `last_procession_phase_1_to_2`, `last_procession_phase_2_to_3`, `end_of_the_procession`. Final animation work attaches here later.
