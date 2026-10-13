# Chronicle act-clear and run-ending playback fix

Installed in the live Shattered Fate Unity project on October 9, 2026.

The missing link was playback: boss victories and completed runs unlocked Chronicle memories, but the progression screens never launched them. The fix connects those existing unlocks to the existing book, story engine, illustrations, handwriting, recorded narration, and save system.

## What happens now

- **Acts I and II:** collect the existing boss rewards first. The screen fades to black with drifting golden threads, the closed Chronicle emerges under an emerald glow, and its cover opens. The current hero’s earned, unread memories play in order. The act-boundary memory also plays when it was previously watched. The pages settle, the cover closes, and only then does the run advance to the next act’s Fateweave.
- **Final victory:** the Act III boss still skips normal rewards. Earned memories play, followed by the run-ending book sequence, then the normal victory results screen.
- **Defeat:** the run records its existing loss and closes its save, plays earned memories followed by the interrupted ending, then shows the normal defeat results screen.
- **Interrupted ending:** the narrator says “And there, at last, the tale came to—”. “The End” writes as an unspoken page event. Music ducks, the book trembles, a red line crosses out the conclusion, and the Being says “Not yet.” The narrator’s existing “I did not write that.” and “Then there is more.” follow. The page glows, threads tighten, and the book closes automatically.

## Writing, sound, and safeguards

- Milestone narration uses dark brown ink, the Being uses the existing charcoal/gold treatment, and corrections remain red. The previously installed fixed-size handwriting and automatic page turns are retained; full pages do not shrink their writing.
- Spoken text and handwriting use the same existing recording and measured word timing. Eight new bindings reuse four installed recordings for victory and defeat; all 244 original dialogue bindings remain unchanged. No voices or timestamps were fabricated, and no WAV files were replaced.
- The existing animated illustrations, correction events, page turns, book rig, and stroke drawing remain in use. Reduced motion now also reaches the imported book rig.
- Gameplay timers and underlying buttons pause during the cinematic. Progression waits for the actual cover-close duration. Continuations fire once, duplicate completed combat cannot replay an ending or increment wins/losses again, and input is guarded when gameplay returns.
- Ordinary earned memories retain their normal viewed-state commits and Archive replay behavior. The shared ending is presentation only: it cannot unlock Reaper lore or automatically complete Chapter X. Administrative future unlocks do not cause unearned future memories to play.
- Skipping completes the existing page-writing/correction state and continues the sequence. Errors release the overlay and return control instead of trapping the player.
- Sandbox and capture modes keep their existing immediate progression behavior.

Dedicated leather/closing Foley and the three requested Chronicle music themes were not present. As instructed, this fix reuses the existing sound cues and music ducking. It does not claim to add those missing production recordings. The close cue remains the installed EventReveal effect rather than newly recorded heavy leather/echo Foley.

## Validation

- Core, runtime, and Editor source compilation passed against the installed Unity references. Unity also imported and compiled the final files successfully.
- **158 story regression tests passed, 0 failed**, including 10 new milestone tests covering earned-memory selection, hero isolation, previously watched act boundaries, administrative unlock filtering, exact dialogue, strike-before-Being order, skipping, and no unrelated lore grants.
- **277 native Unity checks passed, 0 failed** across 12 Play Mode fixtures: Act I, Act II, final victory, and defeat for all three heroes. These call the actual combat-result hook, check rewards before act playback, check result screens/counters, and traverse fade, emergence, story, close, and return phases.
- **24 native voice bindings** checked for exact text, valid measured word ranges, loaded clips, and matching durations.
- All original 244 bindings were compared with the backup and remained identical; the eight aliases retain the original recording references and timings.
- A live Unity victory-transition preview showed the crossed-out ending, Being dialogue, golden threads, page artwork, and automatic return after the book closed.

The native flow fixtures deliberately skip scene dialogue to test progression quickly. A complete human playthrough with headphones is still useful for judging pacing, music balance, the full historical illustrations, and the dramatic close at normal speed. The live preview is not a substitute for a packaged-build performance test.

## Modified files

- `Assets/Editor/ChronicleMilestoneVerification.cs`
- `Assets/Resources/Chronicle/Audio/DialogueIndex.json`
- `Assets/Scripts/Chronicle/ChronicleMilestoneScenes.cs`
- `Assets/Scripts/UI/GildedChronicleMilestones.cs`
- `Assets/Scripts/UI/GildedChroniclePresentation.cs`
- `Assets/Scripts/UI/GildedMainMenu.cs`
- `Assets/Tests/EditMode/ChronicleMilestoneTests.cs`

The four new C# files also have matching Unity metadata. Existing scene, prefab, font, and audio assets were preserved.

To preview without granting run results or story unlocks: start Play Mode, finish/skip the launch scene, then use **Gilded Fate → Chronicle → Preview Victory Transition (Play Mode)** or **Preview Defeat Transition (Play Mode)**. The separate **Verify Act and Run Ending Playback** menu runs the isolated flow checks and returns Unity to Edit Mode.
