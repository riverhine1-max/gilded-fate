# Chronicle visual upgrade

Installed in the live **Shattered Fate** Unity project. This pass upgrades Chronicle illustrations while keeping the existing book layout, narration, story commands and gameplay rules.

- Reused the actual Vanguard, Hexer and Reaper character paintings, replacing the shared traveler placeholder. Added subtle cloth motion, class-colored light and motes, and grounded foot shadows.
- Added four detailed painted environments: Kingdom, Sanctum, Campfire and Fractured Weave. All 26 backdrop IDs resolve to these plates, with atmospheric tinting and layered fog, dust or embers. Each new image is native 1024 × 1536; the import ceiling of 4096 does not upscale the artwork.
- Added animated gold threads and travelling nodes, branching emerald fractures, sigil rings, magical rays, expanding shockwaves and spiral distortion. Effects remain clipped to the illustration and do not cover the writing page.
- Replaced flat magical shapes with lit crystal spheres, engraved watching eyes, brass clocks with moving hands, and illuminated radial symbols. Replaced the campfire disc with layered flickering flame and heat light.
- Grounded the existing castle artwork on the landscape and improved the nested parchment illustration and ending lettering.
- Respected reduced motion and reduced flashing settings. GPU surfaces are cached, bounded and explicitly disposed; original rendering remains available when an asset or shader cannot load.
- Added a disposable, read-only Unity review window that samples the real Chronicle scripts without writing profile or run data. Temporary cameras and objects are cleaned up when the review closes.

## Validation

- Unity compiled the runtime and editor assemblies successfully. Existing unrelated warnings remain.
- **96 native Unity graphics/story checks passed, zero failures:** four native art plates, three distinct animated heroes, two supported shaders, six progressive VFX modes, four animated artifact modes including deterministic reduced-motion frames, backdrop coverage and all 30 story scripts.
- **158 passed, 0 failed** in the existing Chronicle engine regression harness.
- Profile, run and backup file hashes remain unchanged after the visual review.
- Actual memories and character/effects showcase screens were visually reviewed in the running Unity Editor. Saved previews are real Unity captures.

## Review and remaining release checks

Open **Gilded Fate → Chronicle → Review Cinematic Illustrations** in Unity for the six showcase views. The scene/time controls can sample any existing scene, for example `MEM_10` for watching eyes, `MEM_14` for crystal effects and `MEM_20` for clocks. The review does not play narration or save unlocks. Normal Archive playback uses the upgraded renderer automatically.

Before a Steam release, still check a standalone Windows build, a full act-clear/victory/defeat playback with audio, and frame time and memory on the intended minimum hardware. The graphics checks establish that the assets and shaders work in the current Unity Editor; they do not certify an entire game as AAA quality.

## Files modified

- `Assets/Editor/ChronicleCinematicArtReview.cs`
- `Assets/Editor/ChronicleCinematicArtReview.cs.meta`
- `Assets/Resources/Chronicle/ChronicleIllustrationVfx.shader`
- `Assets/Resources/Chronicle/ChronicleIllustrationVfx.shader.meta`
- `Assets/Resources/Chronicle/ChronicleLivingIllustration.shader`
- `Assets/Resources/Chronicle/ChronicleLivingIllustration.shader.meta`
- `Assets/Resources/Chronicle/Illustrations/Cinematic.meta`
- `Assets/Resources/Chronicle/Illustrations/Cinematic/Campfire.png`
- `Assets/Resources/Chronicle/Illustrations/Cinematic/Campfire.png.meta`
- `Assets/Resources/Chronicle/Illustrations/Cinematic/FracturedWeave.png`
- `Assets/Resources/Chronicle/Illustrations/Cinematic/FracturedWeave.png.meta`
- `Assets/Resources/Chronicle/Illustrations/Cinematic/Kingdom.png`
- `Assets/Resources/Chronicle/Illustrations/Cinematic/Kingdom.png.meta`
- `Assets/Resources/Chronicle/Illustrations/Cinematic/Sanctum.png`
- `Assets/Resources/Chronicle/Illustrations/Cinematic/Sanctum.png.meta`
- `Assets/Scripts/ChronicleBook/ChronicleLivingArt.cs`
- `Assets/Scripts/ChronicleBook/ChronicleLivingArt.cs.meta`
- `Assets/Scripts/UI/GildedChronicleArt.cs`
- `Assets/Scripts/UI/GildedChronicleCinematicArt.cs`
- `Assets/Scripts/UI/GildedChronicleCinematicArt.cs.meta`
- `Assets/Scripts/UI/GildedChroniclePresentation.cs`
- `Assets/Scripts/UI/GildedCombatChoices.cs`

## Saved artifacts

- [Native Unity graphics verification](Chronicle_Visual_Verification.json)
- [Asset and modified-file manifest](Chronicle_Visual_Manifest.json)
- [Exact image-generation prompts](Chronicle_Art_Prompts.md)
- [Vanguard preview](Chronicle_Vanguard_Review.png)
- [Reaper preview](Chronicle_Reaper_Review.png)
- [Weave VFX preview](Chronicle_Weave_Review.png)
- [Clock memory preview](Chronicle_Clocks_Review.png)
- [Sanctuary memory preview](Chronicle_Sanctuary_Review.png)
- [Campfire character preview](Chronicle_Campfire_Review.png)
- New environment PNGs: [Kingdom](Chronicle_Artwork/Kingdom.png), [Sanctum](Chronicle_Artwork/Sanctum.png), [Campfire](Chronicle_Artwork/Campfire.png), [Fractured Weave](Chronicle_Artwork/FracturedWeave.png).
