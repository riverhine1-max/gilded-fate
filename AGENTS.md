# AGENTS.md — Gilded Fate coding instructions

These instructions apply to coding agents (including OpenAI Codex) working in the root of the **Gilded Fate** repository. Read [`README.md`](README.md) first for the game's identity, character mechanics, acts, Chronicle, architecture and build setup. This file is the **implementation contract**, not a request to redesign the project.

## 1. What this project is

- Unity **6000.5.9f1**, C#, URP, single-player dark-fantasy roguelike deckbuilder.
- Three playable heroes: **Vanguard, Hexer, Reaper**, with intentionally different mechanics.
- Three-act seeded branching runs, themed enemies and shared act-specific neutrals, map events, reward decisions, shops and sanctuaries.
- Permanent card instances, upgraded cards, Bindings, Fateweaves, relics, and **30 Fate Shards** with charge/attunement and stable/fractured uses.
- **The Chronicle of Broken Fate**, an animated/book-based narrative system with 27 original memories, ten original shared corrections, 30 additional Fate Debt recollections and a secret Chapter X.
- A project under active development: never claim an asset or build is production-ready without verifying it.

## 2. Source-of-truth order

Resolve conflicting information in this order:

1. **Current checked-out source, source-controlled assets, current verified tests and live scene/prefab references**.
2. Feature-specific rules in `Assets/GILDED_FATE_GAMEPLAY_RULES.md`, `Docs/` and canonical story source / manifests; compare dates when docs conflict.
3. This `AGENTS.md` and the root README for design intent and navigation.
4. Historical updates, staging files, older READMEs, conversations and proposals.

If a request conflicts with hard-coded game data, report the conflict and implement only the agreed change. If source and documentation disagree, prefer runtime truth and update the relevant documentation/tests as part of the fix.

**Do not mistake root-level duplicated `.cs` files, `Archive/`, or `GildedFate_VFX_Polish/` staging material for live Unity code.** Runtime scripts live in `Assets/Scripts/`; assets referenced by Unity live in `Assets/`. Check project references before moving a file.

## 3. Non-negotiable invariants

### Gameplay and combat

- `CombatModel` is authoritative. UI reads model state and the ordered `CombatEvent` stream; it must not invent or apply independent damage, Block, enemy behavior or reward logic.
- Keep math order consistent with `Assets/GILDED_FATE_GAMEPLAY_RULES.md`. Distinguish Attack damage from raw triggered damage. Resolve multi-hit math per hit. Apply Block gain exactly once for threshold triggers.
- Never introduce infinite replay, recursion, draw/energy or trigger loops by accident. Echo/Fateful/Golden Echo/repeated card effects need non-recursive safeguards.
- Preserve readable intents. Deterministic shown enemy actions cannot secretly reroll. If a displayed intent has explicit alternatives, any randomized outcome must stay within those alternatives. Replan only in situations supported by current logic.
- Respect current living-enemy capacity, summon/owner/minion rules, target legality, support-only exits and phase transitions. Older theme docs disagree on the cap: inspect the implementation instead of guessing.
- Preserve seeded generation, combat state, route/node state, future prediction state, counter values and RNG implications across save/load.
- New cards, relics, Bindings, Fateweaves and Shards must extend existing catalogs and resolution paths, not create a parallel system.
- Preserve individual `RunCard.persistentId` and save migration behavior. `RunModel.cards` is authoritative; legacy `deck`/`upgradedCards` are compatibility data.
- `FateShardState` and combat attunement/charge must survive checkpoints. Natural card plays generate charge; automatic replay copies must not duplicate natural-play charge. One activation per combat, subject to current source rules.

### Chronicle, narration, and progression

- **Do not paraphrase, overwrite or silently "improve" canonical story writing.** Original scripts, facts, revelations, correction strings, speaker identities, and stable IDs are intentional. A lore-writing task must be explicitly authorized.
- Keep the 27 original memories, ten `CORR_*` shared corrections and original Chapter X distinct from the 30 `ASC_*` recollections and their twelve `ASC_CORR_*` additions.
- Fate Debt recollections progress **one per eligible victory, in order, for that hero**, exactly matching the next required Fate Debt level. Defeat, Normal victories, repeat victories at completed levels, admin unlocks, sandboxes, replay and duplicate run callbacks must not award revelations.
- The *updated* Chapter X unlock requires **all three True Histories**. Do not restore the old "view the original 27" unlock rule from stale architecture notes.
- A recollection cannot be shown ahead of its required base memories; if gated, queue it without losing progress.
- Run-ending book scenes (including the crossed-out *The End* event and the Being's response) are **presentation only**. Do not change a real win/loss, unlock an unrelated memory, or commit Chapter X from an ending transition.
- Keep the Chronicle save embedded in the existing profile: **no duplicate profile/save database**. Preserve migrated profiles and idempotent commits.
- The Chronicle sequencer, stage interfaces, book rig, archive and on-screen presentation have separate roles. Do not merge them into one UI script.
- For voiced text, synchronize writing to the **actual recorded audio clock** using the matching entry in `Assets/Resources/Chronicle/Audio/DialogueIndex.json`. Do not fake timestamps, synthesize an unrelated take, or stretch the clip to force synchronization. If canonical text changes with authorization, update the recording and measured word timings with it.
- Red corrections must preserve and visibly strike the original before showing the new text. Skip must complete the final permanent state exactly once.
- Preserve book page anchors, timing, page-turn direction, exact page destination, saved spread, accelerated cross-chapter travel, and reduced-motion behavior. Covers must stay open during ordinary sheet turns.
- The authored story source for new recollections is under `SourceAssets/ChronicleAscension/`. The generated runtime script `Assets/Scripts/Chronicle/Scripts/ChronicleScripts.Ascension.cs` must stay consistent with its source and manifests.

### Unity assets and repository hygiene

- Preserve `.meta` files and GUIDs when moving/modifying Unity assets; do not delete them as cleanup.
- Use Git LFS correctly for tracked binary content. A pointer file is not an image/audio/model and must not be used as such. Avoid unnecessary re-encoding of large binary assets.
- Never overwrite finished book assets, working voice clips or local save files during unrelated tasks.
- Do not change `Packages/manifest.json` or upgrade Unity by default; project requires **6000.5.9f1** unless explicitly requested.
- Avoid mass mechanical formatting of legacy C# partial files; focused diffs are easier to verify.
- Do not hardcode machine-specific paths, secrets, tokens, account details or Unity credentials in the repo or logs.
- Respect third-party credit/license files and avoid importing unlicensed resources.

## 4. Where to work

| Task | Start here |
|---|---|
| Card definitions / upgrades | `Assets/Scripts/Core/GameContent.cs`, card expansion catalogs, `Assets/Scripts/Cards/CardData.cs` |
| Relics and hero engines | `Assets/Scripts/Core/MajorRelics.cs`, other core catalogs, `Assets/Scripts/Combat/CombatRelicExpansion.cs` |
| Run deck, Fateweaves, maps, rewards | `Assets/Scripts/Map/RunModel.cs`, `RunRewards.cs`, `RunMerchant.cs` |
| Combat rules | `Assets/Scripts/Combat/CombatModel.cs`, `CombatEffects.cs` and related partials |
| Theme-specific enemies | Matching `Assets/Scripts/Core/*Content.cs`, `Assets/Scripts/Combat/*Combat.cs`, then matching `Docs/<Theme>.md` |
| Neutral enemies | `Assets/Scripts/Core/NeutralContent.cs`, `Assets/Scripts/Combat/NeutralCombat.cs`, `Docs/NeutralEnemies.md` |
| Intents/status/icon rules | `Assets/Scripts/Combat/EnemyTurnPlan.cs`, `EnemyIconRules.cs`, `PowerIconCatalog.cs`, `Assets/Scripts/UI/GildedEnemy*.cs` |
| Shard catalog, charge and awakening | `Assets/Scripts/Core/WorldContent.cs`, `Assets/Scripts/Combat/CombatShardCharge.cs`, `CombatShards.cs`, `FATESHARD_OVERHAUL_README.md` |
| UI, animations, screen transitions | `Assets/Scripts/UI/GildedMainMenu.cs`, its partials and feature-specific UI scripts |
| Save/recovery | `Assets/Scripts/Saving/`, `Assets/Scripts/Combat/CombatCheckpoint.cs`, `Assets/Scripts/Map/RunModel.cs` |
| Chronicle story and unlocks | `Assets/Scripts/Chronicle/` including `ChronicleAscensionProgress.cs` and `ChronicleMilestoneScenes.cs` |
| Chronicle book and presentation | `Assets/Scripts/ChronicleBook/`, `Assets/Scripts/UI/GildedChronicle*.cs`, `Assets/Resources/Chronicle/` |
| Chronicle canonical authoring | `SourceAssets/ChronicleAscension/`, original `ChronicleScripts.Act*.cs`, `DialogueIndex.json` |
| Editor validation | `Assets/Editor/`, `Assets/Scripts/UI/*Verification.cs` |
| Chronicle tests | `Assets/Tests/EditMode/`, `Tools/ChronicleHarness/` |
| Cloud build and Pages deploy | `.github/workflows/`, `ci/` |

## 5. Work procedure

**Before coding**

1. Read the complete user task. Name what will and will not change.
2. Confirm the branch and inspect `git status` / current diff. Never discard changes that the user or another agent has already made.
3. Locate the live scripts, relevant content definition, serialization fields, UI display path and any tests that exercise the target behavior.
4. Read the feature docs and check whether newer source supersedes an older claim.
5. Prefer the smallest coherent implementation. For risky save/Chronicle changes, state the migration/rollback risk before acting.

**While coding**

1. Keep logic in the authoritative subsystem and presentation in its own layer.
2. Use existing style and IDs; preserve serialization contracts and Unity GUID references.
3. Make content definitions declarative where the project already uses content catalogs.
4. Update tests and docs alongside changes to mechanics, UI wording, progression or asset bindings.
5. Keep strong, intentional player combos but protect against accidental infinite loops and impossible-to-understand effects.

**After coding**

1. Examine the complete diff (`git diff --check`, changed paths, and unexpected binaries/meta files).
2. Run tests you can actually run and report the commands and results exactly. Do **not** fabricate a test pass if Unity or an SDK is unavailable.
3. For combat, test deterministic behavior, intents, minions and replay limits. For saving, test new and older profiles/checkpoints. For Chronicle, run `dotnet run --project Tools/ChronicleHarness` and Unity EditMode when available.
4. Explain the changed files, design implications, tests, remaining gaps and manual Play Mode steps. If a build is required, distinguish **source compiles** from **packaged build works**.

## 6. Verification commands

```bash
# In a full checkout with a compatible .NET SDK
dotnet run --project Tools/ChronicleHarness

# Hygiene
 git diff --check
 git status --short

# If assets appear to be LFS pointer text
 git lfs install
 git lfs pull
```

Unity-specific tests require the actual Unity Editor/Test Runner. `Assets/Editor/` also contains production quality checks and feature-specific verification. A successful standalone Chronicle harness is **not** proof that UI, video, animation, sound, FBX import or WebGL works in a packaged build.

## 7. Known current uncertainties

- October 13, 2026 source contains **all 30 authored Ascension recollections, 45 new voice clips and six additional art studies**, but the matching integration report says full Unity play/visual testing and listening review are still outstanding.
- The book has source Blender assets, runtime FBX, rig, Unity prefab/materials and a fallback renderer. Final end-to-end performance/visual validation must still be measured in the target Unity version.
- Some older docs describe the original 17-enemy game, a one-enemy encounter architecture, six-enemy groups, and the *old* Chapter X unlock. Newer code and tests supersede these notes.
- New narrative ambience, dedicated Foley and additional score themes should not be claimed to exist unless checked in the actual asset tree.
- Steam achievement/cloud code does not mean the store integration has been configured or published.

**Final rule:** the game belongs to the project owner. Do not change the intended story, balance philosophy, hero identities, visual direction, platform strategy or progression rules on your own initiative. Inspect, implement the requested task, verify and make the result easy to review.
