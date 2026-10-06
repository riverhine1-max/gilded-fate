# Enemy Icons (Prompt 12B)

26 icons live in `Assets/Resources/Art/UI/EnemyIcons/{Intents,Mechanics,Statuses}` (256 px TGA with transparency, the same import settings as the existing Heat/Minion/Summon icons). The three older icons stay where they are (`Art/UI/Meta/HeatIcon`, `MinionIcon`, `SummonIcon`) and are reused, not replaced.

Which icon goes where is decided in one place, `Combat/EnemyIconRules.cs`, so real runs, the Playground and the tests agree. The UI side is `UI/GildedEnemyIcons.cs` plus small hooks in `GildedWildPresentation.cs`. Icons only support the text: every number, counter and move name is still drawn.

## Intent icons (what the enemy is about to do)
| Icon | Used when |
|---|---|
| Command | every Command action |
| Auction Lot | Royal Auctioneer's Open Bidding, and the Upcoming Lot panel |
| Repeat | Echo Soldier / Duplicate Repeat, The Unmade Replay, Loopkeeper Upcoming Replay panel |
| Thronebreaker Charge | the Thronebreaker's final charge attack (exact damage still shown) |
| World Break | the Worldbreaker's World Break attack (exact damage still shown) |
| Eruption | the Deepcrawler's Eruption attack (damage still shown) |

Summon actions keep the existing Summon icon, Heat actions the existing Heat icon. Stoke Furnace now reads "+1" with the Heat icon and explains it gives +1 Heat to a Heat-using ally.
Actions that build or spend a resource wear that resource's icon (Growth, Orbit Plate, Momentum, Reserve, Bonus Gold).

## Counter icons (what the enemy currently has)
Growth (Hollowwood), Reserve (Living Treasury, Treasury Beast, Treasury Warden), Bonus Gold (Coin Mimic), Orbit Plate (Orbiting Sentinel), Momentum (Fallen Comet), Judgment (Confessor, Oathbreaker Priest, High Confessor, Final Bishop phase 2), Sentence (Execution Herald), Toll (Bell of Sentence), Echo (Time-Shear, The Unmade pending Echo), Split (Splitling, Split Sovereign), Siege (Thronebreaker), World Break Charge (Worldbreaker), Burrow Warning (Deepcrawler, only while Eruption is next; it stays targetable), Form Change (Phase Beast, Living Icon, Crown Duelmaster, Pale Chimera, The Wayfarer, Shifting Husk), Prepared Attack (Nameless Seer).
Seized Gold is a status pill ("SEIZED GOLD · N") on any enemy holding Gold; Bonus Gold is a separate icon.
Forecast panels get a header icon: Prediction (Observatory), Dual Possibility (Fractured Realm "Possible Next Actions", always exactly two), Auction Lot, Repeat.

## Informational only
Fracture and Royal Order are not placed on enemies: Fracture is a general realm marker and Royal Order is a formation theme (never a buff). Both appear in the Playground icon legend (ICONS button on the Playground combat bar), which lists all 26 icons with their meanings.

## Tooltips and controller
Every icon has a plain-language tooltip (`EnemyIconRules.Tip`). Counter pills, forecast panels and the Seized Gold pill are now registered as combat HUD targets too, so they can be focused with a controller like intents.
