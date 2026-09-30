# Combat animation playback

Vanguard (17 clips), Hexer (22 clips), and Reaper (16 clips) use local video-based combat animation. No gameplay rules are applied by the presentation layer.

## Preview

In Unity, open **Gilded Fate > Animation Preview > Vanguard / Hexer / Reaper**. Select a clip or press **Play / Restart**. Play Mode is not required. Pause/resume, loop and playback speed are available. Restart applies changed loop/speed settings.

The preview uses Unity's editor-native media decoder, avoiding the edit-mode VideoPlayer preparation timeout. The decoder wrapper is isolated in editor-only code and has been verified on Unity 6000.5.9f1. If a future Unity release changes that internal API, it reports a compatibility error instead of waiting indefinitely.

## Combat

Select the character and enter combat with Reduce Motion off. Attacks, defense, reactions, powers and character-specific effects select presentation clips. Inspection menus pause playback; reduced motion or unavailable media retains the illustrated portrait fallback. Reaper generation/consumption/Gravemark/exhaust receipts are presentation only; duplicate simultaneous cues are coalesced.

## Assets and fresh clones

Install Git LFS and fetch the repository's LFS objects (`git lfs pull`) before opening a fresh clone. The MP4 files are under `Assets/StreamingAssets/Animations`. Files smaller than 1 KB are likely unfetched LFS pointers, not playable videos.

Runtime files deliberately contain RGB on the left and a grayscale transparency matte on the right. The character's packed-alpha shader reconstructs transparency. Use the Unity preview rather than a normal media player to see the final cutout. The new Vanguard and Reaper derivatives are silent 24-fps H.264 baseline videos with fixed framing. Per-character source manifests preserve provenance and hashes. Earlier source artwork and gameplay content are unchanged.

## Known limits

These are generated video performances, not skeletal animation clips. Fine detail, soft halos, edge-reaching trails and imperfect loop seams can remain. Unity/Windows Media Foundation may emit a color-metadata fallback warning despite explicit BT.709 tags; tested clips still decode. No revive clip is mapped for Vanguard or Reaper because the current gameplay does not require one.

## Enemy animation and combat VFX

Enemies are illustrated portraits animated procedurally in `GildedEnemyAnimation.cs`: the sprite is transformed around its feet with `GUI.matrix`, so health bars, intents, status strips and hit targets keep their stable anchors. Each enemy has a motion archetype (beast, crawler, knight, brute, caster, spirit, colossus) with idle breathing or hovering, an intent-matched wind-up, a strike timed to the first damage receipt, a hit recoil held through the existing hit-stop, a white silhouette hit flash, and a death collapse or dissolve. Bosses tremble and flare on phase changes and have a longer, larger death. The flash and charge-aura silhouettes are baked once per enemy texture (a small RenderTexture read-back) and cached.

`GildedCombatFinalVfx.cs` draws the combat effects in the actor layer, before any HUD: attacker-matched enemy strikes (claws, bites, blades, cleaves, slams, bolts, mirror shards, thrown cards, spectral blades, crushes), hero-specific hits (Vanguard steel slashes and sparks, Hexer violet glyphs, Reaper scythe crescent and souls), damage-tiered scale, shake and hit-stop, gilded accents, enemy casts, status apply pulses and deaths.

Both files are presentation only: they read combat receipts that were already being presented and never change rules, AI, damage or saves. Reduce Motion freezes enemy transforms and moving particles, Reduce Flashing dims flashes and replaces the white hit flash with a warm tint, Reduced VFX lowers particle counts and sizes, and Screen Shake still gates all shake and zoom punch.
