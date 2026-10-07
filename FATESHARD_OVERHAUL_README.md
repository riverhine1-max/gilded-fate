# Fateshard Overhaul: batches 1 + 2

This zip contains everything from batch 1 (charging, shatter, Attune, reliquary) plus batch 2 (shard VFX). It works whether or not you uploaded batch 1.

Unzip and copy the `Assets` folder over the one in your project, keeping the folder structure. On GitHub, drag the unzipped `Assets` folder onto "Add file > Upload files" at the repo root; files at matching paths are replaced.

## New files (Unity creates their .meta files on first open)
- Assets/Scripts/Combat/CombatShardCharge.cs: charge meter, archetype matching, shatter
- Assets/Scripts/UI/GildedShardReliquary.cs: Attune screen, reliquary frame, charge rings, use pips, hold-to-shatter
- Assets/Scripts/UI/GildedShardVfx.cs: shard colors, awakening and shatter cinematics, charge comets, ready beacon, hero aura, battlefield tint, card glints

## Changed files
- Combat/CombatModel.cs, Combat/CombatShards.cs, Core/WorldContent.cs, Map/RunModel.cs
- UI/GildedMainMenu.cs, UI/GildedMenuNavigation.cs, UI/GildedCombatPresentation.cs, UI/GildedShardShrine.cs, UI/GildedCardVfx.cs
- UI/GildedShardVerification.cs, UI/GildedPersistenceVerification.cs, UI/GildedMasterPolishVerification.cs
- GILDED_FATE_GAMEPLAY_RULES.md

## Settings respected
Reduce Motion, Reduce Flashing, Reduced VFX and Screen Shake all tone the shard effects down.
