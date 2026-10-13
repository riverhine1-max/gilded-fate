# Existing Chronicle audio — Unity handoff

This is a verified snapshot of the audio already installed in Shattered Fate. The voices, canonical story, Unity source and Blender book were not changed. No new recordings, artificial timestamps, Foley tracks or music themes were generated.

## What to reuse

242 mono, 48 kHz PCM 24-bit dialogue WAVs serve 244 spoken commands in 30 scenes: the full and short opening, 27 memories, and Chapter X. Narrator and Being have separate recordings and folders. The exact 19 opening strings match the supplied opening script. OPENING_001 is the existing narrator sample; MEM_24__b4 (Not yet.) is the existing Being sample. They are unchanged, full recordings rather than newly generated demos.

Sync/DialogueIndex.json is the byte-for-byte installed runtime index. Every scene JSON contains only that scene's existing bindings and measured word timings, with existing conditions retained. Narrative pauses are already present in the measured word intervals. Source data reports CTC forced alignment and independent ASR with 20 ms alignment frames; this is not a guarantee of sample-exact phonetic onsets. Hashes confirm the final files still match the indexed recordings. Final listening review remains pending; no new alignment or listening approval is claimed.

## Canonical text and clock

The existing ChronicleScripts builders are authoritative. ChronicleMarkup.Parse removes only authoring pause tags. The resulting exact text, scene ID, command ID and NARRATOR/BEING speaker must match a ready DialogueIndex entry. Original corrections use corr:ID; revised corrections use fix:ID. Character quotations keep their original character labels while using the narrator performance. Headings, notes, The End and TO BE CONTINUED are unspoken writing events.

ChronicleDialogueAudio.Begin validates the complete word coverage and exact wording, loads the extensionless Resources reference and rejects clips whose duration differs by more than 25 ms. It schedules one AudioSource at DSP time +20 ms. AudioSource.timeSamples / clip.frequency drives the word and letter reveal. No independent typing timer should replace that clock while voiced dialogue is active. Original audio is never stretched to fit a page or writing speed.

Word charStart and charLength are UTF-16 string offsets, as used by C#. Each token retains its punctuation. Letters progressively reveal during that word's measured interval; punctuation completes at its end. Gaps between words pause ink. The installed stroke renderer supplies the visible pen-like reveal. The writing remains the same size and automatically advances to fresh paper; pending voice playback starts after a page turn settles.

The sequencer waits for the active spoken line to complete, so the two voices do not overlap. Stop invalidates the previous playback generation. Pause/UnPause keep the AudioSource and writing clock together. Skip stops audio and resolves the complete canonical story state. Archive replay uses the same scene and timing bindings.

## Book effects and music

Sync/CueSheet.json describes the current mappings, not a set of new dedicated WAV filenames. The included 24 existing game SFX variations cover EventReveal, Debuff, Resonance, BossPhase, HitHeavy and the three Archive UI cues. Open/close/page turns use EventReveal; corrections use Debuff; magic/emblem events use Resonance; deeper_voice uses BossPhase; fate_crack uses HitHeavy. Unknown cues remain silent. There is no installed quill-writing loop or book idle loop. magic_hum is a one-shot cue, not an ambient track.

Do not relabel those existing effects as newly recorded leather, parchment or quill Foley. No separate cinematic page-turn recording is present. The current correction sound is triggered with the red strike event; it does not implement the proposed multi-layer ambient-drop/quill/resonance recording.

The three requested Chronicle music themes and stems are absent. Preserve the existing contextual game score. Chronicle playback keeps MoodForScreen() and UpdateAudioPresentation(); Archive/opening normally use Menu, while in-run memories can inherit the underlying screen's mood. The existing director crossfades over approximately 2.3 seconds, saves resume positions and avoids restarting the same mood. Dialogue requests Duck(.72, clip.length + .3), with smooth attack/recovery. Music stays a separate AudioSource system.

The eight installed music assets are measured and hashed in the manifest as existing Unity dependencies. They are not bundled as a standalone soundtrack. Keep the existing score and AUDIO_CREDITS.txt in the project/game distribution.

## Interruption prototype and events

Sync/ChapterX/Existing_Interruption.json maps all four requested prototype lines to the exact recordings already present in MEM_24 (b3, b4, b5 and b6). The existing memory provides the ending interruption and remains authoritative, including its other correction passages. Keep those existing scene/command IDs instead of installing duplicate NARRATOR_001/BEING_001 aliases. The End remains a silent writing command followed by the existing strike/correction events.

Sync/SceneCommands.json exports the live scene commands for integration reference only; it is not a new loader or replacement engine. Preserve command order, conditions, relative waits, book rig duration and correction discovery rules. Absolute scene timestamps are intentionally absent because page layout, unlocked facts, replay state and accessibility change the duration of a scene. Only spoken word timings are recording-relative. Do not play alternative conditional lines simultaneously.

## Existing accessibility

Audio settings already expose independent Master, Music, Effects, UI and NARRATION controls. NARRATION = 0 mutes the recorded voice while the same narration clock continues to drive readable writing. Instant text is an Archive option; it reveals the complete passage but does not truncate voice playback. Writing speed affects the silent fallback, not a voiced take. Reduced motion and reduced flashing are inherited from the profile. Replay, skip and full unlocked Archive reading use the existing engine. Critical story text remains present when narration is muted. Existing character speaker labels and the distinct Being style are preserved; do not invent additional spoken labels.

## Using this ZIP

The files are already in Unity. This ZIP is a backup/handoff; importing it again into the same project is unnecessary. If restoring in another checkout, use each manifest unityAssetPath, not the display folder name in this ZIP. Keep WAV importer .meta files alongside their original assets to preserve GUID/import settings, and put Sync/DialogueIndex.json at Assets/Resources/Chronicle/Audio/DialogueIndex.json. The existing game SFX return to their Audio/SFX paths. Do not duplicate assets into a second Resources tree. Keep the existing music files referenced in the manifest.

For future wording/localization changes, update canonical text, recording, measured timings, hashes and bindings together. Never mark a changed line ready with guessed word timings. On mismatch the current system refuses the take and uses its established silent-writing fallback.

## Validation and remaining review

The audit checks every live C# spoken command against the installed index, all 19 exact opening strings, assets, file hashes, duration, UTF-16 word ranges, token coverage, timing order and waveform clipping. It also measures the existing SFX and music formats/levels. This packaging pass does not replace Unity playback or human listening review. Remaining subjective checks are narrator/Being performance, naturalness of word boundaries and final mix on the target device. Retain the current working recordings while doing that review.
