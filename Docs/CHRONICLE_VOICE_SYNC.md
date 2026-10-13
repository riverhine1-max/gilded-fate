# Chronicle narration and handwritten ink

This extends Claude's existing sequencer rather than creating another story engine. Story strings and picture animation scripts are unchanged. Page text uses the licensed Caveat handwriting font; its letters reveal from the canonical string already supplied by the story command.

## One recording, one clock, one text

Recordings and real alignments belong in `Assets/Resources/Chronicle/Audio/DialogueIndex.json`. `ChronicleDialogueAudio` finds a verified line by scene, command, speaker and exact text. It plays the clip at pitch 1 and supplies `AudioSource.timeSamples / frequency` to `ChronicleSpokenHandwriting`. Frame delta, estimated words per minute, and inline typing pauses do not determine the reveal of a recorded line.

Before a word's measured start, its letters remain hidden. Letters reveal progressively within that word's measured interval. Trailing punctuation reveals when the word finishes. Gaps between intervals leave the ink still. The entire recording must finish before the next spoken command starts, including its final silence. Instant-text mode displays the full line while waiting for playback to finish. Pause/resume pauses the source, skip stops narration and resolves the correct completed page without starting the remaining clips. Archive playback uses the same path.

The Narration slider is independent of music and sound effects. Zero mutes the voice while retaining readable page text. Music ducks during dialogue. Being text uses readable charcoal with a muted gold tint. Existing correction, motion and flashing settings still govern the page response.

## Recording data

Every ready entry requires `lineId`, `sceneId`, `commandId`, `speaker`, canonical `text`, Resources `audio` reference, `durationSeconds`, and `words`. Words include `text` (with associated punctuation), `charStart`, `charLength`, `startSeconds`, `endSeconds`. Character offsets are C# UTF-16 code units. Use speaker IDs `NARRATOR` and `BEING`. Optional fields are `emotion` and `correctionId`.

Validation rejects text differences, missing words, absent timestamps, invalid character offsets, overlapping or unordered word intervals, and words extending beyond measured duration. Playback rejects missing clips and durations that changed after alignment. Audio and text must be updated and realigned together after any localization or recording change.

The runtime index covers all 30 scenes and 244 spoken command bindings: 235 normal-pace Arthur clips and seven distinct Being clips. Recordings are delivered only in Chronicle_Voices.zip; no voice WAV or MP3 is published to GitHub. Word intervals are measured from real audio at approximately 20 ms resolution, with independent ASR checks. Constant gain preserves sample positions. Human listening review, Unity import and scheduling remain pending. Foley, magical effects and music are unrecorded and retain explicit missing status.

## Alignment and integration

Use `SourceAssets/ChronicleAudio/Scripts/compile_alignment.py` with a measured/manual or forced-aligned timing sheet after listening to the recording. It checks the real WAV duration, canonical tokens, contiguous text coverage, monotonic intervals and the source hash, then produces a ready index. This script does not generate or estimate timestamps. After any format conversion, measure the final exported WAV and align that final recording.

Opening records map to `SCENE_OPENING` and command IDs `b1` through `b19`. The existing `Although...{p=1.1}` command renders `Although...`; this canonical typography is preserved. For recorded lines, its inline pause marker does not override the recording. The interruption prototype is a separate cue sheet, not a replacement for existing Chapter X story data. `The End` is a written event and must not be synthesized as speech.

## Verification status

137 pure C# Chronicle tests pass locally, including audio-clock reveal, silence, punctuation, instant text, changed wording, missing timings, and skip/pause integration. 242 dialogue recordings have been generated and automatically aligned; final listening review is pending. The recordings are separate from the GitHub book package. Unity Editor import, audio scheduling and rendered page overlays require verification in the project's Unity environment. The Blender source and FBX checks are documented separately in the book QA folder.
