# Gilded Fate

A dark-fantasy roguelike deckbuilder built with Unity. Play as Vanguard, Hexer, or Reaper, build a deck, discover relics, and follow branching routes through the Vault.

This repository contains the current live Unity project snapshot, including its source, scenes, artwork, audio, package dependencies, and project settings. It includes the latest refined character-specific run-start transitions and relic collection sorting.

## Open the project

1. Install Git LFS and Unity Hub.
2. Clone this repository, then run `git lfs install` and `git lfs pull` in the cloned folder. The artwork and audio require LFS; downloading the source ZIP alone may leave placeholder pointers.
3. Install Unity **6000.5.9f1**, including Windows Build Support.
4. Add the cloned repository folder in Unity Hub and open it. Allow Unity to restore packages and import assets.
5. Open `Assets/Scenes/SampleScene.unity` and enter Play mode.

The game supports mouse/keyboard and Xbox controller input. Start a new run to see the chosen character's brief transition; Continue does not replay it.

## Hexer combat animation set

Hexer has 22 video-based animations connected to combat actions, Sigils, card feedback, and damage reactions. The runtime videos include an alpha matte and need Git LFS just like the artwork. Missing video files or Reduce Motion use the original illustrated portrait fallback. Vanguard and Reaper presentation is unchanged.

Preview every clip inside Unity through **Gilded Fate > Animation Preview > Hexer**. This panel does not change a run or save. Runtime MP4s intentionally contain RGB and alpha side-by-side; the game reconstructs the cutout. Format, mappings, and source provenance are documented in `Assets/StreamingAssets/Animations/Hexer/README.md` and `Sources.json`.

## Build a Windows playtest

Open Unity's Build Profiles, select Windows, include `Assets/Scenes/SampleScene.unity`, and build into a local `Builds` folder. Keep the executable and all generated runtime files together when sharing a build.

The existing `GildedFate.Editor.GildedFateAutomatedBuild.BuildWindows` editor entry point also creates a development build in `Builds/VisualCheck`.

## Repository contents

- `Assets`: gameplay and presentation code, scenes, art, audio, fonts, and credits.
- `Packages`: Unity dependency manifest and lockfile.
- `ProjectSettings`: shared Unity project configuration.

Unity caches, personal editor preferences, local saves, previous exports, and development backup folders are intentionally excluded. Existing `.meta` files are retained to preserve asset references.

## Credits and rights

Third-party audio credits are in `Assets/StreamingAssets/AUDIO_CREDITS.txt`; font licenses are in `Assets/StreamingAssets/ThirdParty`.

No open-source license is granted for the original game code or assets by this repository. Third-party components retain their respective license terms.
