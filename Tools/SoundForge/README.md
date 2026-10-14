# Sound forge

The game's sound effects, rebuilt: every clip is **synthesised from code** (no recordings, no licences to track), deterministically, in about 20 seconds.
The original effects (`Assets/Resources/Audio/SFX`), the music and the Chronicle's narration and book audio are **not touched**.

```bash
pip install numpy scipy
python3 Tools/SoundForge/forge.py                         # writes Assets/Resources/Audio/SFX_Forged/<Cue>_<1..3>.wav (+ Unity .meta files)
python3 Tools/SoundForge/forge.py --only HitHeavy,UiConfirm
python3 Tools/SoundForge/forge.py --out /tmp/f --report /tmp/f-report --mp3   # metrics, .npy for sheets, mp3s for listening
python3 Tools/SoundForge/sheet.py /tmp/f-report sheet.png HitHeavy IntroClash   # spectrogram sheet
python3 Tools/SoundForge/sequences.py /tmp/f /tmp/seq     # old-vs-new demos: the opening and a sample combat turn, on the real timing
python3 Tools/SoundForge/check.py                         # every SoundCue has a design; clips render clean (runs in the PR checks)
```

## How it fits the game

* `GameAudio` loads `Audio/SFX_Forged/<Cue>_<n>` (three takes per cue, as before). **Settings > Audio > Sound design** switches between NEW (this set) and ORIGINAL (the previous recordings). Cues added with the new set (`IntroAwaken` ... `IntroShimmer`, `EnemyDeath`, `CardPlay`, `CritHit`) fall back to the sound the game used before (or to silence where there was none) when ORIGINAL is chosen, so ORIGINAL plays exactly as the game used to.
* The generated `.wav` files are **not committed** while the set is still being tuned: they are git-ignored and **CI renders them before every Unity build** (`Forge the sound effects` step). Their `.meta` files are committed so Unity's import settings and GUIDs are stable. To hear or use the sounds in the Editor, run `forge.py` first. When the set is approved, remove the ignore line and commit the wavs once (they are Git LFS files).
* Adding a cue: add it to `SoundCue` in `SoundCatalog.cs` (and a `SoundSpec`, and an `OriginalFallback`), add a recipe in `designs.py`, then run `check.py`.

## Files

| File | What |
| --- | --- |
| `dsp.py` | the toolkit: envelopes, oscillators, noise, filters (including time-varying sweeps), modal bells/metal/glass/wood, reverb rooms, saturation, transient shaping, loudness (K-weighted) and mastering |
| `designs.py` | one recipe per cue, built from layers (sub, body, crack, ring, debris, room); `PV` gives each take its own pitch; `TARGET_RMS` sets loudness |
| `original_loudness.json` | what the original recordings measure, so the new set sits in the same mix (`measure_originals.py` regenerates it) |
| `forge.py` | renders everything and writes the wavs and metas |
| `sequences.py`, `sheet.py`, `check.py` | listening demos, spectrogram sheets, the CI check |

Design language: dark gilded fantasy. Bells and chimes are tuned to D minor pentatonic so the effects sound like one instrument. Heavy hits are layered (sub drop, body thud, bright crack, ringing metal, debris, room). Loudness is matched to the originals by ear-weighted measurement, impacts a little hotter.
