# Combat timing and run-ending fixes

Installed in the open Shattered Fate Unity project.

- **One Being line per ending:** defeat endings rotate through “Not yet.”, “Nothing was meant to be lost.”, and “The world is still broken.” using the current hero’s recorded defeat count. Each ending contains one choice, followed by the existing narrator response. Victory keeps its existing single interruption. Reused the actual recordings and measured word timings; repaired the scene/command bindings for the selected line and subsequent narration.
- **Attack and defense timing:** inspected the existing character MP4s and measured sword/scythe contact, magical release, and raised-guard poses. Card flights, attack sound cues, and rule resolution now wait for the corresponding pose. The first decoded frame starts the presentation clock, and recovery finishes before the next card can interrupt it. Defeat’s held final frame bypasses recovery waiting. Vanguard follow-up strikes use the same contact timing instead of a separate fixed lead.
- **Enemy intent timing:** snapshot the executing action before resolution. Keep every group member’s action displayed throughout its receipts and recovery, then hold for one additional real second before showing the next turn. The reading beat remains in fast/instant mode and pauses during inspection. Cache invalidation during normal player actions still updates predictions immediately.
- **Defeat title:** removed the random pale crack paths that drew the reported scribble across “Fate Shattered.” Kept the existing layout and surrounding atmosphere.

Combat effects, card costs, damage, Block, AI selection, rewards, and balance were not changed.

## Validation

- Runtime and editor assemblies compile with zero errors. Two existing unrelated warnings remain.
- 158 Chronicle regression tests pass, including one-line ending selection, skip state, and correction order.
- 245 checks pass in Unity Play Mode with save writes suspended: 15 attack/defense scenarios across Vanguard, Hexer, and Reaper; normal, heavy, finisher, and fast defensive playback; grouped enemy intent holds in normal and instant modes; defeat recovery; and exact audio/word-timing bindings for all three Being choices.
- Player profile, current run, and both backup files have identical hashes before and after verification.

Full observations and the installed file hashes are in `Combat_Timing_Verification.json`.

## Files modified

- `Assets/Editor/CombatActionTimingVerification.cs`
- `Assets/Resources/Chronicle/Audio/DialogueIndex.json`
- `Assets/Scripts/Chronicle/ChronicleMilestoneScenes.cs`
- `Assets/Scripts/UI/GildedChronicleMilestones.cs`
- `Assets/Scripts/UI/GildedCombatPresentation.cs`
- `Assets/Scripts/UI/GildedEnemyIntentPresentation.cs`
- `Assets/Scripts/UI/GildedHeroActionTiming.cs`
- `Assets/Scripts/UI/GildedHexerVideoCombat.cs`
- `Assets/Scripts/UI/GildedReaperVideoCombat.cs`
- `Assets/Scripts/UI/GildedStoryEndPolish.cs`
- `Assets/Scripts/UI/GildedVanguardVideoCombat.cs`
- `Assets/Scripts/UI/HeroActionTiming.cs`
- `Assets/Scripts/UI/HexerVideoSurface.cs`
- `Assets/Scripts/UI/ReaperVideoSurface.cs`
- `Assets/Scripts/UI/VanguardVideoSurface.cs`
- `Assets/Tests/EditMode/ChronicleMilestoneTests.cs`

Three new scripts also have Unity `.meta` files. `CombatActionTimingVerification.cs` adds an optional isolated-save verification menu under **Gilded Fate → Verification**.

## Remaining manual coverage

The native pass covers representative cards and actual combat coroutines, including heavy/finisher variants. It does not claim a full playthrough of every card, relic interaction, or encounter. A release-build check of lengthy multi-hit chains, several simultaneously triggered sigils, reduced-motion mode, and very slow hardware is still useful. The defeat-title removal is verified from its drawing code; a new visual screenshot of that result screen was not captured.
