# Repository hygiene inventory (2026-10-14)

Status: **inventory only. Nothing was moved or deleted.** It extends `Archive/README.md`, which already plans to move the loose root uploads into `Archive/RootUploads/` once uploads are finished.

## Loose root files

Unity only compiles and imports what is under `Assets/`; files in the repository root are never used by a build. Nothing in `.github/`, `ci/`, `Assets/Editor`, `ProjectSettings` or `Packages` refers to them (the README and AGENTS.md mention them only to warn they are not live code).

| Group | Files | Finding |
| --- | --- | --- |
| Root `.cs` identical to the live copy | 12 | Exact duplicates. Safe to archive. |
| Root `.cs` that differ from the live copy | 11 (`CombatModel`, `EventStories`, `EventSystem`, `GildedCombatHudFocus`, `GildedCombatPresentation`, `GildedMainMenu`, `GildedMenuNavigation`, `GildedShardReliquary`, `GildedShardShrine`, `RunModel`, `WorldContent`) | **Every one is older** than its live counterpart (root last committed 7 Oct; live 8 to 13 Oct). They are stale snapshots, not unmerged work. `Archive/README.md` lists only 3 as differing; that was true on 7 Oct and is out of date. |
| Fractured Realm `.tga` textures | 5 files (about 14 MB) and 4 `.meta` | Unreferenced. Each has a live `.png` under `Assets/Resources/Art/Enemies/`. `fractured_realm_splitling.tga.meta` is an orphan with no texture. |
| `vfx-polish.patch` | 1 | Unreferenced root copy. `GildedFate_VFX_Polish/` has its own copy. |
| `GILDED_FATE_GAMEPLAY_RULES.md` | 1 | Byte-identical to `Assets/GILDED_FATE_GAMEPLAY_RULES.md`, which is the one AGENTS.md names. |
| `FATESHARD_OVERHAUL_README.md` | 1 | Referenced by README.md and AGENTS.md. Keep it where it is (or update both links when moving). |
| `GildedFate_VFX_Polish/` | 15 files | Staging material, tracked. |

### Proposed move (reversible; needs your OK)

Everything above except `FATESHARD_OVERHAUL_README.md` into `Archive/RootUploads/`, using `git mv` so history follows the files. `Archive/README.md` should then be updated to say the root is clean and why. Nothing is deleted, so it can be undone with another `git mv`.

## Git LFS

`.gitattributes` sends `*.png *.jpg *.wav *.ogg *.mp3 *.mp4 *.fbx *.ttf *.otf *.blend` to Git LFS, but the repository does not follow its own rule:

* 488 files are proper LFS pointers.
* **413 files (about 307 MB) are stored as ordinary git blobs**, including 280 original narrator recordings, the 45 new narrator recordings, the Chronicle book model (`Chronicle.fbx`, `.blend`), and the parchment/emerald textures.

The usual cause is uploading through the GitHub web page, which does not apply LFS rules. The Actions logs show the same thing as "Encountered 51 files that should have been pointers".

Builds work either way (raw files are simply files). **I recommend not converting them now:**

* Moving 307 MB into LFS adds to your LFS storage and, through every CI cache miss, to your monthly LFS bandwidth. If the free allowance is exceeded, `git lfs pull` in CI fails and every build breaks until the allowance resets or you buy more.
* Converting only changes how new commits are stored. The 307 MB stays in history unless history is rewritten, which is far too risky for a repository people upload to by hand.

If you do want it later: check Settings > Billing > Git LFS first, and use Git (GitHub Desktop or the command line) rather than the web uploader, so LFS applies to new files.
