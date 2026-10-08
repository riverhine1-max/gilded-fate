# The Chronicle: architecture (batches 1 and 2: story engine and the book)

The Chronicle of Broken Fate is the book that tells Gilded Fate's hidden history. This batch builds everything *except* the
visible book: the story data, save data, unlock rules, red corrections, the scene engine and all 30 scenes. Rendering
(book model, illustration drawing, archive screen, startup hook) comes in later batches and plugs into the seams below.

## Layout

| Path | What it is |
| --- | --- |
| `Assets/Scripts/Chronicle/` | The engine-free assembly `GildedFate.Chronicle` (no UnityEngine). Everything below lives here. |
| `Assets/Scripts/Saving/ChronicleProfileBridge.cs` | The only place the game's real progression touches the Chronicle (`ChronicleHooks`). |
| `Assets/Scripts/Saving/PlayerProfile.cs` | Gains one field, `chronicle`. Missing field = fresh Chronicle. |
| `Assets/Editor/GildedChronicleDevConsole.cs` | Editor-only console: Gilded Fate > Chronicle > Developer Console. |
| `Assets/Tests/EditMode/` | NUnit tests (Unity Test Runner, EditMode). |
| `Tools/ChronicleHarness/` | Runs the same tests with no Unity: `dotnet run --project Tools/ChronicleHarness`. |

## The pieces

- **ChronicleCatalog**: 9 chapters, 27 memories (`MEM_01`..`MEM_27`: chapter = (n-1)/3+1; hero = Vanguard, Hexer, Reaper in order), 10 corrections (`CORR_01`..`CORR_10`), 28 facts, chapter prefaces, unlock rules. IDs are stable strings, never display text.
- **ChronicleProgress**: all persisted state (unlocked, viewed, facts, corrections in discovery order, per-hero counters, notices, opening preference). Every operation is idempotent.
- **Scenes**: `ChronicleScene` is a flat list of `ChronicleCommand`s (23 ops). Authored with the `ChronicleSceneBuilder` DSL in `Scripts/ChronicleScripts*.cs`. Plain serializable data, so ScriptableObject or JSON authoring can replace the C# later without touching the sequencer.
- **ChronicleSequencer**: runs one scene against an `IChronicleStage`. Owns all scene state (book pose, page text, illustration, camera). Renders nothing. `Skip()` jumps to the exact final state and commits every permanent result.
- **ChronicleDirector**: one scene at a time; ignores a skip press in the first 0.35 s so the press that launched a scene cannot cancel it.
- **Handwriting**: progressive reveal with per-word, punctuation and per-line pauses, inline `{p=0.8}` pauses, wrapping and page-budget checks, instant-text setting.

## The seams (what later batches implement)

```csharp
IChronicleStage    // book open/close/turn/idle/react/major-magic, set pose immediately, audio, magic effects
IChronicleContext  // facts, corrections and narrator state the player already has
IChronicleCommitSink // where permanent results go (null = preview, saves nothing)
```

The temporary book and, later, the finished Blender book are both just `IChronicleStage` implementations. Story data,
page content, narration and illustrations never reference a book object. Not yet built: the book rig, its clip-mapping
configuration and the replacement guide. That arrives with the book batch.

## Unlock rules (deterministic, per hero)

Chapter I finish a run | II reach floor 9 | III beat the Act I boss | IV floor 24 | V floor 30 | VI beat the Act II boss | VII floor 42 | VIII floor 48 | IX win a run.
One victory with a hero unlocks all nine of that hero's memories. Chapter X needs all 27 (so a win with all three heroes).
Elites, drops and random events never gate memories. Existing profiles are credited once from their run history and win counts.

## Corrections

A correction preserves the original sentence, strikes it in red, writes the replacement beneath, and is committed once.
`CORR_01` can be committed by any Chapter III memory; late arrivals get a conditional reconciling beat. In the archive a
correction appears only once discovered, on its chapter's preface page, whatever order memories were watched in.

## Testing

- Unity: Window > General > Test Runner > EditMode.
- Standalone: `dotnet run --project Tools/ChronicleHarness [filter]`.
- Editor menu: Gilded Fate > Chronicle > Validate Content; the Production Quality Gate also validates the Chronicle on load.
- Developer Console: plays any scene on a sandbox saved in `Library/GildedChronicleSandbox.json`, never the real profile.

## The book (batch 2)

| Piece | Where | Role |
| --- | --- | --- |
| `ChronicleBookAnimator` | `Assets/Scripts/Chronicle/ChronicleBook.cs` | Pure logic: poses, timing, page flips, glow, tremor, overlay visibility. Unit-tested. Rule: covers stay at exactly 180 degrees for every page turn. |
| `IChronicleBookRig` | same | Open, close, turn forward/back, idle, react, major magic, set pose immediately, plus the current frame. |
| `ChronicleRigStage` | same | The only thing story playback knows about the book: maps `IChronicleStage` onto a rig. |
| `ChronicleProceduralBook` | `Assets/Scripts/ChronicleBook/` | The temporary book from Unity primitives. Applies the animator's frame to transforms. |
| `ChronicleAnimatorBookRig` + `ChronicleBookRigConfig` | same | Drives the finished model through its Animator; clip names mapped in a config asset. See `CHRONICLE_BOOK_REPLACEMENT.md`. |
| `ChronicleBookStageHost` | same | Isolated layer (29), manually rendered camera into a RenderTexture (same pattern as the combat stage), picks the rig. |
| `GildedChroniclePresentation` | `Assets/Scripts/UI/` | The overlay: shows the book, draws handwriting, red strikes and the placeholder illustration over its pages, handles skip input. Mirrors the boot cinematic's hooks. |
| `GildedChronicleBook.shader` | `Assets/Resources/` | Flat-shaded depth-correct surface, loaded like the project's other shaders. |

Try it: enter Play Mode, then Gilded Fate > Chronicle > Play On Book (Play Mode).

## Status

Built and tested in the standalone harness: the story engine and the book's behaviour (covers fixed during turns, page-turn timing,
overlay fades, skip snapping the book to its final pose, sequencer and rig in step). Type-checked against engine stand-ins but never
run in Unity: the procedural book geometry, the shader, the render-to-texture camera, the on-screen overlay, the finished-model rig.
Not yet built: THE CHRONICLE archive screen, startup cinematic hookup, unlock notifications, controller navigation, a settings screen for
instant text and writing speed (the preferences are saved already).
