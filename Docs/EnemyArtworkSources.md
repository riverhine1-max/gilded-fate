# Enemy artwork repair

The missing Crimson Foundry illustrations (16 enemies and two Iron Saint phase variants) and the Ashen Wilds/Drowned Quarter Unbound Blade and Wayfarer variants were imported from `Gilded_Fate_All_Completed_Enemy_Themes.zip`. The original PNG data and alpha were preserved. Images already present in the project were retained.

Four missing Gilded Ruins illustrations were created with built-in image generation: Giltblade Scavenger, Tarnished Appraiser, Crown Collector, and Living Treasury. They are transparent PNG files at `Assets/Resources/Art/Enemies/gilded_ruins_<enemy>.png`.

Shared elite artwork uses filenames without the `elite` segment in the gameplay ID. Neutral artwork follows the current theme; an available variant from the neutral's own act is used when the playground previews it in another act or in the legacy Vault. The playground sets the selected themed opponent's backdrop instead of retaining an unrelated random theme.

`Gilded Fate > Verify Enemy Artwork` checks the actual combat texture cache for every enemy, all authored formations, themed boss phases, minions and the exposed Hollow Parasite. Verification uses an isolated inactive menu and does not change the scene, saved run or profile. Results are written to `Logs/EnemyArtworkVerification.json`.
