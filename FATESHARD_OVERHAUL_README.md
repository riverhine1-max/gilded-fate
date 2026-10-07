# Fateshard Overhaul: batch 1

Unzip and copy the `Assets` folder over the one in your project, keeping the folder structure. Every file goes to the same path it has in the repo, so on GitHub you can drag the unzipped `Assets` folder onto "Add file > Upload files" at the repo root.

New files (Unity creates their .meta files on first open):
- Assets/Scripts/Combat/CombatShardCharge.cs: charge meter, archetype matching, shatter
- Assets/Scripts/UI/GildedShardReliquary.cs: Attune screen, reliquary frame, charge rings, use pips, hold-to-shatter

Changed files:
- Combat/CombatModel.cs: card plays add charge; activation needs a full meter
- Combat/CombatShards.cs: Silvermind draws 2 on activation instead of only on turn one
- Core/WorldContent.cs: Bloodstone, Hourglass and Silvermind text reworded to "On activation"
- Map/RunModel.cs: remembers your last Attune pick
- UI/GildedMainMenu.cs: opens Attune when a combat starts; draws it
- UI/GildedMenuNavigation.cs: controller support for Attune
- UI/GildedCombatPresentation.cs: input blocked while Attune is open; shatter path
- UI/GildedShardShrine.cs: shards move to the reliquary during combat (map view unchanged)
- UI/GildedShardVerification.cs, GildedPersistenceVerification.cs, GildedMasterPolishVerification.cs: updated and new checks for charging
- GILDED_FATE_GAMEPLAY_RULES.md: new "Fate Shard charging" section
