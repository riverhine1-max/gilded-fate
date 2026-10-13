# Changelog — Final Chronicle Integration

## New content (from the Prompt 3 packages, validated before import)
- `Assets/Scripts/Chronicle/Scripts/ChronicleScripts.Ascension.cs`: the 30 recollection scenes, generated verbatim from `Scenes/*.json`, laid out with measured page breaks; majors use the existing strike/red-ink/save path on their own new observation only.
- `Assets/Scripts/Chronicle/ChronicleAscensionContent.cs`: chapter, memory, title, presentation, marginal art and continuity gates for each ID.
- 45 recordings in `Assets/Resources/Chronicle/Audio/Dialogue/Narrator/ASC_*.wav` (+ `.meta`); 45 entries appended to `DialogueIndex.json` (original 254 unchanged).
- 6 marginal studies in `Assets/Resources/Chronicle/Illustrations/Ascension/` (+ `.meta`).

## Engine
- Catalog: `title`, `marginalArt`, `requiredViewed`; content applied and `contentReady` set only when the scene is registered; `GatesMet`; stronger validation.
- `ChronicleScripts.cs`: registers the recollections (one line).
- Presentation: exactly one pending revelation at a time, after its base memories.
- Archive: revelations readable once presented, watchable once earned; appended to their memory's reading; chapter entries.
- Journal: `PlanFlip` / `ChronicleFlipPlan` for distance-based journeys.
- Book: `IChronicleBookTempo`; `ChronicleBookAnimator.Tempo` (sequencer timing unchanged).

## Unity
- Archive: multi-page journeys with skip; blank intermediate pages; exact landing.
- Milestones: recollections travel to their chapter; Chapter X reveal; tempo on continuity turns.
- Presentation: tempo helper; marginal art. Rigs: both follow the tempo (Animator speed during turns only).

## Tests
- New `ChronicleAscensionIntegrationTests` (8). Updated three Prompt 1 and one Prompt 2 assertion that described the absence of content.
