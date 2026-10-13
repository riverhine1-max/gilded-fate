# Recordings and exact handwriting

The canonical strings are in the opening and prototype JSON files. Recording_Requests.json is the complete 23-line production request list, with delivery directions in a separate field. Do not speak filenames, line IDs or instructions. Do not rewrite a line to reach a fixed duration. Natural pauses are part of the performance.

Choose and lock a separate voice preset for each speaker, audition OPENING_001 and BEING_001, then generate or record the remaining exact lines. Preserve the Narrator's humanity and the Being's very quiet certainty. Normalize and finalize the recording before alignment. Resampling to 48 kHz is acceptable; slowing or speeding speech to fit a writing animation is not.

Use forced alignment with the exact transcript when available. Otherwise open the final recording in an audio editor and fill Manual_Word_Timing.csv by listening. Each row corresponds to one canonical whitespace-delimited word. Preserve punctuation in the text column. Enter onset and end in seconds and set verifiedByListening to true after checking the interval against the sound. Leave pauses as real gaps between end and the next start. Do not interpolate a whole sentence into a uniform typing schedule.

Convert the verified sheet to the existing compiler's input with:

`python SourceAssets/ChronicleAudio/Scripts/csv_to_measured.py SourceAssets/ChronicleAudio/Sync/Manual_Word_Timing.csv measured_words.json`

The converter rejects partially filled lines, altered words, missing verification and non-increasing intervals. All-blank lines remain unavailable. The compiler then measures the actual WAV duration, verifies mono 48 kHz PCM, checks every word fits the recording and records the audio SHA256. Flat input WAV folders are accepted; speaker subfolders are also supported.

`python SourceAssets/ChronicleAudio/Scripts/compile_alignment.py SourceAssets/ChronicleAudio/Sync/Opening/Opening.json Chronicle_Audio measured_words.json opening_ready.json`

Repeat with Sync/ChapterX/TwoVoicePrototype.json for the prototype. Merge ready line arrays into one DialogueIndex.json after confirming line IDs are unique and speaker/scene/command bindings match Claude's story commands. Copy the final WAVs to the Resources path specified in each line's audio field, adding .wav to the extensionless reference. Do not publish a ready line with a missing clip or mismatched canonical text.

The runtime uses AudioSource.timeSamples / clip.frequency, rather than elapsed typing time. Within each measured word, its letters appear progressively. Space and punctuation follow that word; intervals without speech leave the ink paused. This is a handwritten font with progressive letter reveal, not a custom pen-stroke font renderer. Narration pause/resume pauses both sound and writing. Instant text changes reveal only and does not truncate playback. Skipping completes the story state and stops dialogue.

For the prototype, play NARRATOR_001, weaken ambient hum, write the unspoken phrase The End, cross it out with a red correction, then play BEING_001. Leave a short editorial silence after its measured ending, then NARRATOR_002 and NARRATOR_003 in order. Do not overlap the voices. The silence length is an editorial setting; spoken-word boundaries must still come from the final recordings.

The Being pauses the Narrator's original ink, introduces a subtle page vibration and writes in darker charcoal or muted gold. Return to the normal Caveat handwriting and near-black/brown ink when the Narrator resumes. Ordinary red corrections use a restrained quill scratch and low resonance; keep critical information readable with reduced motion and reduced flashing enabled.

Remaining accessibility and final runtime checks belong to the existing game settings and Archive: verify mute, separate voice/music/SFX volume, instant text, reduced motion/flashing, replay and skip on the final Unity build. No plot information should be audio-only. All 19 opening recordings are aligned and bound to b1–b19; three recorded Narrator prototype lines retain unbound command IDs. The Being, effects/music, final listening review and target-device checks remain pending.
