# Hexer combat animations
22 Higgsfield / Wan 3.0 clips, generated for Gilded Fate. Original jobs, URLs and hashes are listed in Sources.json. Original reference archive is retained outside the Unity project.

## Runtime format
Each MP4 is a 1024x512 H.264 video at 24fps: the left 512x512 half contains RGB, and the right half contains a grayscale alpha matte. HexerPackedAlpha.shader reconstructs a transparent 512x512 image. Opening these MP4s in a normal player shows both halves; that is intentional. They are streamed from disk, not imported as skeletal animations.

Matting: rembg u2netp silhouette extraction with violet-effect retention and light temporal stabilization. No model code/weights run or ship with the game. Some soft haloing/card-fan continuity is inherited from the generated references; inspect at gameplay scale.

## Combat mapping
- Idle / low-health idle: persistent grounded actor loop.
- Basic / alternate / powerful / finisher: Attack anticipation, selected by attack sequence, base value and rarity.
- Defensive / major defensive: Block skills; barrier impact: incoming blocked hit.
- Light / heavy hit, defeat and revive: ordered HP receipts.
- First Ritual: ritual choice cards; selected Sigil: newly filled slot.
- Normal / Overflow / Echo: single, repeated-slot, and Echo receipts. Overflow is a visual double-activation name, not a new combat rule.
- Aspect: played Power; buff: positive player status.
- Draw / Dissipate: card receipt feedback, coalesced behind higher-priority actions.

Only two VideoPlayers are active, with bounded/coalesced presentation events. No gameplay rules or save data are modified. Modal inspection pauses playback; leaving combat disposes resources. Reduce Motion retains the illustrated fallback, as do missing/failed video files. Original Vanguard/Reaper presentation is unchanged.

## Preview
Unity menu: **Gilded Fate > Animation Preview > Hexer**. Select any of the 22 clips. Pause, loop and speed controls do not change a run/save. "Show packed source" is a diagnostic view.

Use Git LFS when cloning; MP4 pointer files are not playable videos.
