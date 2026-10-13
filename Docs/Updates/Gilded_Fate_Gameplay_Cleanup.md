# Gilded Fate gameplay cleanup

Installed and verified in `C:\Users\river\Documents\Unity Projects\Shattered Fate` on October 9, 2026, using Unity 6000.5.9f1.

## Requested fixes

1. **Relic wording:** relic UI paths now use the shared terminology formatter. Funeral Bell and the other 89 relics display Dissipate/Dissipated and Aspect consistently, including collection, inspection, reward, merchant, acquisition, Shrine and overflow tooltips. Funeral Bell's trigger and cadence are unchanged.
2. **Upgrade costs:** removed redundant printed cost clauses from eight authored upgrades and the duplicate energy comparison sidebar. The card's cost badge uses its existing upgrade cost. Resumed combat checkpoints refresh descriptions without overwriting modified costs or permanent bonuses.
3. **Card descriptions:** reviewed all 282 cards and revised 80 cards' base or upgrade wording. Upgrades now include complete rules rather than fragments where necessary; Draw/discard counts, targeting, Block, statuses and Energy are clearer. All 282 base and upgraded mechanical definitions compare identically to the backup. The text-driven Soulbound Block classification also remains unchanged. The separate wording report lists every edited description.
4. **Combat right-click:** removed its upgrade-preview action. It now cancels selection or dragging. Existing tooltips, ordinary inspection and upgrade comparison outside combat remain available.
5. **Enemy rendering:** imported 154 missing enemy PNGs from the supplied completed-themes ZIP. Retained 22 matching existing PNGs and their asset identities. All eight themes' images, shared enemy variants, summoned units and boss phases use the filenames expected by the combat resource loader. The Cathedral Bell and all Cathedral combat setups pass actual texture-loading and visible-alpha checks. Rendering uses resource-loaded Texture2D images, so missing files and resource identities were the cause. Targeted import settings preserve transparency. The Hollow Parasite keeps its base illustration while exposed because the supplied archive has no separate exposed-form image; its mechanics still work normally.
6. **Fate Shard sockets:** valid sockets are reserved before legacy/unassigned Shards fill gaps. Acquisitions take the first free socket; replacements preserve the chosen socket and validate the new Shard before removing or charging for anything. Loading repairs legacy assignments without losing items. Existing activation, three-use durability, fracture and shattering behavior remain intact.
7. **Final act:** defeating the Act 3 boss goes directly to run completion, with no normal gold/card/relic/Shard/bonus reward sequence. Completion statistics and marks are recorded once. Older saves paused at final-boss rewards also finish directly. Acts 1 and 2 rewards, and ordinary Act 3 combat/elite rewards, remain intact. Fixed a related existing menu warning that incorrectly called a valid completed-run save marker damaged; actual unreadable save files still trigger the warning and remain preserved.

The existing startup production check was also updated: it still expected the original 17-enemy catalog, unthemed shared-enemy art and immediate phase changes. It now validates the current catalog and themed resource names, and uses explicit phase planning for its isolated boss checks. No boss mechanics were changed.

## Validation completed

| Check | Result |
| --- | --- |
| Runtime and Editor compilation | Passed; existing unrelated compiler warnings remain |
| Native reliability regression suite | 1,005 checks; 282 cards, 90 relics, 30 Shards, three final bosses; zero failures |
| Actual enemy combat texture cache | 688 setups covering 323 formations; 1,243 texture checks and 191 distinct textures; zero failures |
| Existing gameplay regression suite | 171 checks, 24 boss phase/checkpoint scenarios; zero failures |
| Native card typography | 16,230 checks, including larger-text settings; zero clipped or missing-text cases |
| Production quality gate | Passed |
| Fresh Unity Play session | Main menu reached; false completed-save warning absent; no new runtime exceptions observed |
| Before/after balance comparison | All 282 base/upgraded definitions unchanged; zero Block eligibility differences |
| Imported image integrity | All 154 new PNGs match their supplied source hashes |

Tests used isolated fixtures with profile/save writes suspended. This validation is not a full interactive playthrough.

## Modified code files (25)

Paths below are relative to the Unity project.

| File | Change |
| --- | --- |
| `Assets/Editor/GildedCleanupTypography.cs` | New native standard/large-text card layout check. |
| `Assets/Editor/GildedCleanupVerification.cs` | New isolated regression checks for all seven requested fixes; suspends save writes and restores test state. |
| `Assets/Editor/GildedEnemyArtworkVerification.cs` | Verify the actual combat texture cache, phases, summons and the exposed Parasite fallback. |
| `Assets/Editor/GildedEnemyTextureImporter.cs` | New targeted enemy texture import settings preserving alpha and Texture2D rendering. |
| `Assets/Editor/GildedFateQualityGate.cs` | Update current enemy counts, theme-specific neutral art checks and explicit boss phase planning; save a machine-readable result. |
| `Assets/Scripts/Combat/CombatCheckpoint.cs` | Refresh saved printed descriptions while retaining saved costs, values, identity and modifiers. |
| `Assets/Scripts/Core/GameContent.cs` | Canonical relic Description property; self-contained card upgrade wording and explicit draw/discard units. |
| `Assets/Scripts/Core/MajorVanguardCards.cs` | Consistent draw/discard units and enemy targeting wording. |
| `Assets/Scripts/Core/MartialOccultCards.cs` | Consistent draw/discard units and enemy targeting wording. |
| `Assets/Scripts/Core/RemainingExpansionCards.cs` | Consistent draw/discard units and enemy targeting wording. |
| `Assets/Scripts/Core/RewardRules.cs` | Central final-boss run completion rule. |
| `Assets/Scripts/Map/EventSystem.cs` | Atomic event Shard replacement. |
| `Assets/Scripts/Map/RunModel.cs` | Validate Shard acquisition/replacement before mutation; retain the chosen physical socket. |
| `Assets/Scripts/Map/RunRewards.cs` | Reserve valid sockets before repairs, atomic purchased Shard replacement, final reward suppression and valid empty final receipts. |
| `Assets/Scripts/Saving/SaveService.cs` | Repair Shard socket assignments on load; distinguish a valid completed-run marker from a damaged save. |
| `Assets/Scripts/UI/GildedAcquisitionPresentation.cs` | Use canonical relic descriptions in acquisition presentation. |
| `Assets/Scripts/UI/GildedBossRelicRewards.cs` | Use canonical relic descriptions in boss reward presentation. |
| `Assets/Scripts/UI/GildedCardInspection.cs` | Block combat right-click upgrade inspection; remove duplicate energy comparison wording. |
| `Assets/Scripts/UI/GildedCombatPresentation.cs` | Right-click cancels card selection/dragging without opening an upgrade preview. |
| `Assets/Scripts/UI/GildedMainMenu.cs` | Canonical relic descriptions; finish Act 3 directly, preserve ordinary rewards and handle older pending final-reward saves. |
| `Assets/Scripts/UI/GildedMerchantPresentation.cs` | Use canonical relic descriptions in the merchant. |
| `Assets/Scripts/UI/GildedQuickPlaytestVerification.cs` | Keep relic text checks consistent with canonical displayed wording. |
| `Assets/Scripts/UI/GildedRelicExpansionPresentation.cs` | Use canonical relic descriptions in relic detail presentation. |
| `Assets/Scripts/UI/GildedShardShrine.cs` | Use canonical relic descriptions in Shrine presentation. |
| `Assets/Scripts/UI/GildedWildPresentation.cs` | Keep the Hollow Parasite visible when the archive has no exposed-form variant. |

Also added 154 PNGs and their 154 new `.png.meta` files under `Assets/Resources/Art/Enemies`. The import manifest records every new asset path, source archive entry and SHA-256 hash. Existing enemy images and GUIDs were retained. Unity regenerated its local import/cache data.

## Remaining Unity play testing

- Play a complete run and check the Act 3 ending and return to the menu.
- Check mouse/controller inspection, tooltips and right-click drag cancellation during live combat.
- Exercise merchant/event Shard replacement and save/resume during a real run.

No known failures remain in the completed checks. No card, relic, Shard or enemy balance changes were made. A pre-change source backup remains in this chat's `work/gameplay-cleanup/before` directory.
