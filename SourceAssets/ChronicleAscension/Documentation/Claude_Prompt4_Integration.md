# Handoff for Prompt 4 — content, not a competing story engine

The ZIPs stage new content only under `SourceAssets/ChronicleAscension/`. They contain no replacement Unity scripts, Resources assets, original recordings, or original illustrations. Base story was read at `9132d13a79af79e1d44d593f6da43cdd64f7478c`; the actual Prompt 1/2 contracts added during production were read at `7211864e93fee914ef370912ae1ed8aafd66266b`. Those two uploads did not change the base story scripts or original corrections.

## Use the existing Ascension IDs

The actual catalog reserves `ASC_VAN_01` through `ASC_VAN_10`, `ASC_HEX_01` through `ASC_HEX_10`, and `ASC_REA_01` through `ASC_REA_10`. Scene ID equals revelation ID. Its twelve correction IDs use major ordinal 1–4, such as `ASC_CORR_VAN_1`, not debt number or zero padding. This package binds those exact IDs. `contentKey` such as `Vanguard:01` is only an authoring locator, not another save identifier. Do not create another progression table or rename existing saves.

`Lore/Revelation_Catalog.json` links all 30 entries to the matching scenes, chapters, memories, presentations and kind ordinals. Local `blockKey` and `assetLineKey` identify new content, not aliases for base commands. Audio line IDs derive from the reserved scene ID plus explicit block key. Suggested command IDs match `WriteBlock(blockId, ...)`; preserve them or update the binding deliberately when authoring. Keep exact text, audio, duration, hash and measured timing together.

No runtime JSON loader was found. This is validated authoring data for Prompt 4, not an already-installed loader. Register approved scenes through the existing `ChronicleScripts` structure, fill the existing definitions' memory/chapter/presentation, and set `contentReady` only after approval and actual registration. `ChronicleAscensionCatalog.IsPresentable` requires both readiness and a registered scene. This task leaves contentReady false and registers nothing.

## Preserve the base story

All existing Chronicle strings, 27 memories, ten shared `CORR_01` through `CORR_10` definitions, Chapter X, milestone responses, voices, animation clips, and illustration assets stay unchanged. A new major correction targets only its own `new_observation` block on a fresh continuation leaf. Validate both the owner and block key before drawing a strike. Never resolve a strike by matching an old sentence's text or by searching the whole Archive.

The authoring `continuityGates` keep the old discoveries first. Defer a queued addition if its base memory or required knowledge has not been viewed. In particular, Vanguard Debt I follows the base friendship recovery; Hexer Debt I follows the existing intervention correction; Reaper Debt IX/X follow the existing fusion correction. These are content dependencies for review against Prompt 1/2, not a replacement unlock implementation. Preserve victory/debt progression; a deferred scene remains queued and is not lost or re-awarded.

Chapter X remains the first identification of the Narrator as the Forgotten Observer. The new True Histories prepare return, testimony, and an open future. They do not stage a rescue, reveal that identity, resolve the anchor, or replace the final unfinished ending. No new Being dialogue is included.

## Use the existing presentation

Use `ChronicleJournal.PlanContinuation` to return to the relevant chapter from the saved story position, including its maximum of three visible turns. Keep the book open during the sheet motion. Use `ChronicleRhythm.TurnAnticipation`, `TurnSettle`, `Beat` and `CompletionHold`, with its shorter revisit pacing. Respect manual advance and the player's selected textScale; never shrink automatically to avoid a page turn. Reserve a continuation leaf for every major correction and retain original blocks. Reuse the current Caveat/stroke renderer, ink palette, red strike, reduced-motion/flashing preferences and Archive navigation.

The progression sink supports saving reserved Ascension correction IDs, but `ChronicleCatalog.FindCorrection` still returns only the ten original text definitions. Do not call `Original`/`Corrected` on a reserved ASC_CORR ID and assume its strings already exist. Author explicit new observation/replacement blocks from this package and use `StrikeBlock(new_observation, reservedCorrectionId)` plus the existing save seam, or add a dedicated new-definition lookup in Prompt 4 while keeping the original definitions untouched. Reserve space for both new blocks. Never locate a strike by scanning old journal text.

Silent blocks have authored pacing and meaningful holds; their timing fields are not recordings' word timestamps. Narrated blocks use the final recording's sample clock and measured words. Do not use fixed typing speed during voiced lines. Wait for each clip to finish before any correction or the next voice. If a recording/timing binding is unavailable or text/hash/duration validation fails, use silent writing and do not play unsynchronized speech.

The six marginal images are narrative studies. They contain no canonical lettering or executable diagrams; the JSON/text supplies exact facts. Add them as small static details. Do not replace the current scene background, characters, book textures, or art animation system. Static display is the default and works in reduced motion.

## Audio and existing sounds

All new speech uses the established Arthur preset at native normal pace. Major entries have three separate spoken blocks: new mistaken observation, corrected replacement, and closing reflection. The nine narrated smaller entries voice only their closing observation. The other nine smaller entries are silent. No original WAV is included in either ZIP.

`Audio/Manifests/Dialogue_Production.json` records the true per-file status, duration, PCM format, exact text, hashes, and measured word intervals. Human listening approval remains pending. The two-line/three-line generation passages are production sources, not a soundtrack that should be played continuously through a correction.

Preserve the existing game score and its dialogue ducking. Request the existing page-turn and `red_ink` mappings. There is no installed quill-writing or living-book ambient loop, and `magic_hum` is a one-shot mapping; do not label them as new Foley or fake a continuous ambience. No music or SFX assets are replaced.

## Skip, reading, and replay

Skip resolves all new text immediately and retains the new mistaken observation visibly struck with its replacement below. It does not remove old ink, run voice callbacks after stopping, or grant progression twice. The completed additions become readable alongside the expanded memories. Archive reading is immediate; cinematic replay is an explicit option. Respect independent narration/music/effects volumes, instant text, reduced motion, reduced flashing, and narration replay.

## Review before integration

Read `Continuity_Report.md`, especially the newly authored literal terms of Vanguard's earlier agreement and the bounded observational details. These are proposed new canon, not facts recovered from hidden repository files. Keep the agreed titles, debt order, and memory attachments. If any new wording is edited, regenerate that new recording and its measured timing together. Original audio and writing remain immutable.

This package has content/reference/format validation and exact reserved ID bindings. It has not been imported or played in Unity. Preserve the existing new Ascension progression rules, Chapter X access and grandfathering; this content task does not change them. Scene registration, final editorial/listening approval and Unity verification remain for Prompt 4.
