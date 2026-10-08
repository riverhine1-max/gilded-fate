using System;
using System.Collections.Generic;

namespace GildedFate.Chronicle
{
    // ------------------------------------------------------------------------------------------------
    // Seams between the narrative engine and everything that draws or persists it.
    // ------------------------------------------------------------------------------------------------

    /// <summary>What the sequencer needs from the book and the outside world. The placeholder book, the future Blender rig,
    /// audio and VFX all sit behind this; none of them know about scenes, corrections or progression.</summary>
    public interface IChronicleStage
    {
        float BookActionDuration(ChronicleBookAction action);
        void BookAction(ChronicleBookAction action, ChronicleNarratorState narrator);
        void SetBookPoseImmediate(ChronicleBookPose pose, int spreadIndex);
        void PlayAudio(string cue, float volume);
        void StopAllAudio();
        void MagicEffect(string effect, float x, float y, float intensity);
        void SceneStarted(string sceneId);
        void SceneEnded(string sceneId, bool skipped);
    }

    /// <summary>What the player already knows. Drives adaptive narration and the archive's corrected text.</summary>
    public interface IChronicleContext
    {
        bool HasFact(string factId);
        bool HasCorrection(string correctionId);
        ChronicleNarratorState NarratorState { get; }
    }

    /// <summary>Where permanent results go. Null means a preview that saves nothing.</summary>
    public interface IChronicleCommitSink
    {
        void SaveCorrection(string correctionId, string sceneId);
        void DiscoverFact(string factId, string sceneId);
        void SceneFinished(string sceneId, bool skipped);
    }

    public sealed class NullChronicleStage : IChronicleStage
    {
        public float BookActionDuration(ChronicleBookAction a) => a == ChronicleBookAction.Open ? 1.6f : a == ChronicleBookAction.Close ? 1.3f : a == ChronicleBookAction.TurnPageForward || a == ChronicleBookAction.TurnPageBack ? .9f : 0f;
        public void BookAction(ChronicleBookAction action, ChronicleNarratorState narrator) { }
        public void SetBookPoseImmediate(ChronicleBookPose pose, int spreadIndex) { }
        public void PlayAudio(string cue, float volume) { }
        public void StopAllAudio() { }
        public void MagicEffect(string effect, float x, float y, float intensity) { }
        public void SceneStarted(string sceneId) { }
        public void SceneEnded(string sceneId, bool skipped) { }
    }

    /// <summary>Records everything the sequencer asks the stage to do. Used by tests and the editor preview log.</summary>
    public sealed class RecordingChronicleStage : IChronicleStage
    {
        public readonly List<string> log = new List<string>();
        public ChronicleBookPose pose = ChronicleBookPose.Closed; public int spread; public bool audioStopped, ended, endedSkipped;
        readonly NullChronicleStage timing = new NullChronicleStage();
        public float BookActionDuration(ChronicleBookAction a) => timing.BookActionDuration(a);
        public void BookAction(ChronicleBookAction action, ChronicleNarratorState narrator) { log.Add("book:" + action); }
        public void SetBookPoseImmediate(ChronicleBookPose p, int s) { pose = p; spread = s; log.Add("pose:" + p + "@" + s); }
        public void PlayAudio(string cue, float volume) { log.Add("audio:" + cue); }
        public void StopAllAudio() { audioStopped = true; log.Add("audio:STOP"); }
        public void MagicEffect(string effect, float x, float y, float intensity) { log.Add("magic:" + effect); }
        public void SceneStarted(string sceneId) { log.Add("start:" + sceneId); }
        public void SceneEnded(string sceneId, bool skipped) { ended = true; endedSkipped = skipped; log.Add("end:" + sceneId + (skipped ? ":skipped" : "")); }
        public int Count(string prefix) { var n = 0; foreach (var l in log) if (l.StartsWith(prefix, StringComparison.Ordinal)) n++; return n; }
    }

    /// <summary>A fixed set of known facts and corrections. Used by previews, tests and archive rendering.</summary>
    public sealed class ChronicleStaticContext : IChronicleContext
    {
        public readonly HashSet<string> facts = new HashSet<string>(), corrections = new HashSet<string>();
        public ChronicleNarratorState narrator;
        public bool HasFact(string id) => facts.Contains(id);
        public bool HasCorrection(string id) => corrections.Contains(id);
        public ChronicleNarratorState NarratorState => narrator;
    }

    public sealed class ChronicleProgressContext : IChronicleContext
    {
        readonly ChronicleProgress progress;
        public ChronicleProgressContext(ChronicleProgress progress) { this.progress = progress; }
        public bool HasFact(string id) => progress.HasFact(id);
        public bool HasCorrection(string id) => progress.HasCorrection(id);
        public ChronicleNarratorState NarratorState => progress.NarratorState;
    }

    /// <summary>Commits scene results into a ChronicleProgress. Refuses anything from a scene the player has not unlocked,
    /// unless allowLocked (the developer sandbox, which never points at the real profile).</summary>
    public sealed class ChronicleProgressSink : IChronicleCommitSink
    {
        readonly ChronicleProgress progress; readonly bool allowLocked;
        public readonly List<string> newCorrections = new List<string>(), newFacts = new List<string>();
        public bool firstView, secretCompleted;
        public ChronicleProgressSink(ChronicleProgress progress, bool allowLocked = false) { this.progress = progress; this.allowLocked = allowLocked; }

        bool Legit(string sceneId)
        {
            if (allowLocked) return true;
            if (sceneId == ChronicleCatalog.SecretSceneId) return progress.secretUnlocked;
            return progress.IsUnlocked(sceneId);
        }
        public void SaveCorrection(string correctionId, string sceneId) { if (Legit(sceneId) && progress.CommitCorrection(correctionId)) newCorrections.Add(correctionId); }
        public void DiscoverFact(string factId, string sceneId) { if (Legit(sceneId) && progress.DiscoverFact(factId)) newFacts.Add(factId); }
        public void SceneFinished(string sceneId, bool skipped)
        {
            if (sceneId == ChronicleCatalog.OpeningSceneId || sceneId == ChronicleCatalog.OpeningShortSceneId) { progress.CommitOpeningViewed(); return; }
            if (sceneId == ChronicleCatalog.SecretSceneId)
            {
                var s = progress.CommitSecretCompleted(allowLocked);
                if (s.accepted) { secretCompleted |= s.secretCompleted; firstView |= s.firstView; newCorrections.AddRange(s.newCorrections); newFacts.AddRange(s.newFacts); }
                return;
            }
            var r = progress.CommitMemoryViewed(sceneId, allowLocked);
            if (r.accepted) { firstView |= r.firstView; newCorrections.AddRange(r.newCorrections); newFacts.AddRange(r.newFacts); }
        }
    }

    // ------------------------------------------------------------------------------------------------
    // The sequencer
    // ------------------------------------------------------------------------------------------------

    /// <summary>
    /// Executes one scene's commands in order against a stage. Owns all scene state; renders nothing.
    /// Supports pause/resume, fast-forward, and skip, which jumps straight to the scene's correct final state:
    /// text fully written, corrections struck and rewritten, persistent effects at full, book in its final pose,
    /// and every permanent result (facts, corrections, viewed) committed exactly as if the scene had played out.
    /// </summary>
    public sealed class ChronicleSequencer
    {
        sealed class Tween { public float t, d; public Action<float> apply; public bool illustration; }

        public readonly ChronicleScene Scene;
        public readonly ChronicleSceneState State = new ChronicleSceneState();
        public readonly bool Replay, ArchiveMode;
        public bool Started { get; private set; }
        public bool Paused { get; private set; }
        public bool Finished { get; private set; }
        public bool Skipped { get; private set; }
        public float Elapsed { get; private set; }
        public event Action<ChronicleSequencer> Ended;

        readonly IChronicleStage stage; readonly IChronicleContext ctx; readonly IChronicleCommitSink sink;
        readonly ChronicleSettings settings; readonly ChroniclePageMetrics metrics; readonly ChroniclePageBudget budget;
        readonly List<Tween> tweens = new List<Tween>();
        int pc; float wait; bool finishing, committed; ChronicleTextBlock activeWrite; float activeBeat; ChronicleNarratorStyle narratorStyle;

        public ChronicleSequencer(ChronicleScene scene, IChronicleStage stage, IChronicleContext ctx, IChronicleCommitSink sink, ChronicleSettings settings = null, ChroniclePageMetrics metrics = null, bool replay = false, bool archiveMode = false)
        {
            Scene = scene ?? throw new ArgumentNullException(nameof(scene));
            this.stage = stage ?? new NullChronicleStage(); this.ctx = ctx ?? new ChronicleStaticContext();
            this.sink = archiveMode ? null : sink; this.settings = settings ?? ChronicleSettings.Default;
            this.metrics = metrics ?? new ChroniclePageMetrics(); budget = new ChroniclePageBudget(this.metrics);
            Replay = replay; ArchiveMode = archiveMode;
        }

        public void Start(ChronicleBookPose initialPose = ChronicleBookPose.Closed, int initialSpread = 0)
        {
            if (Started) return;
            Started = true; State.pose = initialPose; State.spreadIndex = initialSpread;
            State.narrator = Scene.narratorOverride >= 0 ? (ChronicleNarratorState)Scene.narratorOverride : ctx.NarratorState;
            narratorStyle = ChronicleNarratorStyle.For(State.narrator);
            State.tremor = settings.reduceMotion ? 0f : narratorStyle.tremor;
            stage.SceneStarted(Scene.id);
        }

        public void Pause() { Paused = true; }
        public void Resume() { Paused = false; }

        public void Update(float dt)
        {
            if (!Started || Finished || Paused) return;
            dt = Math.Max(0f, dt) * Math.Max(.01f, settings.fastForward);
            Elapsed += dt;
            Tick(dt);
            RunCommands();
        }

        /// <summary>Jumps to the end: applies the scene's correct final visual and progression state, then finishes.</summary>
        public void Skip()
        {
            if (!Started || Finished) return;
            Skipped = true;
            FastForwardToEnd();
            Finish(true);
        }

        /// <summary>Runs the whole scene instantly without finishing it as a skip (used to resolve final page state).</summary>
        public void ResolveInstantly()
        {
            if (!Started) Start();
            FastForwardToEnd();
            Finish(false);
        }

        public float WriteProgress => activeWrite == null ? 1f : activeWrite.Progress;
        public bool IsWriting => activeWrite != null;

        // ---------------- time ----------------
        void Tick(float dt)
        {
            if (wait > 0f) wait -= dt;
            for (var i = tweens.Count - 1; i >= 0; i--)
            {
                var tw = tweens[i]; tw.t += dt;
                tw.apply(Ease(tw.d <= 0f ? 1f : Math.Min(1f, tw.t / tw.d)));
                if (tw.t >= tw.d) tweens.RemoveAt(i);
            }
            var anims = State.illustration.animations;
            for (var i = anims.Count - 1; i >= 0; i--)
            {
                anims[i].t += dt;
                if (anims[i].Done && !anims[i].persist) anims.RemoveAt(i);
            }
            foreach (var a in State.illustration.actors) if (a.speakTimer > 0f) a.speakTimer = Math.Max(0f, a.speakTimer - dt);
            foreach (var b in State.blocks) if (!b.complete) ChronicleHandwriting.Step(b, dt, narratorStyle, settings);
            if (activeWrite != null && activeWrite.complete) { activeWrite = null; wait = Math.Max(wait, activeBeat * narratorStyle.pauseScale); }
            State.pageGlow = Math.Max(0f, State.pageGlow - dt * .8f);
            State.pageDarkness = Math.Max(0f, State.pageDarkness - dt * .6f);
        }
        static float Ease(float t) => t * t * (3f - 2f * t);

        void RunCommands()
        {
            var guard = 0;
            while (!Finished && !finishing && wait <= 0f && activeWrite == null && pc < Scene.commands.Count && guard++ < 4096)
                Execute(Scene.commands[pc++], false);
            if (pc >= Scene.commands.Count) finishing = true;
            if (finishing && !Finished && wait <= 0f && activeWrite == null && tweens.Count == 0 && !EffectsRunning()) Finish(false);
        }

        // A scene is not over while a timed effect (a flash, a shake, drawing threads) is still playing.
        bool EffectsRunning() { foreach (var a in State.illustration.animations) if (!a.Done) return true; return false; }

        void FastForwardToEnd()
        {
            foreach (var tw in tweens.ToArray()) tw.apply(1f);
            tweens.Clear(); wait = 0f; activeWrite = null;
            foreach (var b in State.blocks) ChronicleHandwriting.Complete(b);
            var guard = 0;
            while (pc < Scene.commands.Count && guard++ < 8192) Execute(Scene.commands[pc++], true);
            foreach (var tw in tweens.ToArray()) tw.apply(1f);
            tweens.Clear();
            foreach (var b in State.transcript) { ChronicleHandwriting.Complete(b); if (b.struck) b.strikeProgress = 1f; if (b.erased) b.eraseProgress = 1f; }
            var anims = State.illustration.animations;
            for (var i = anims.Count - 1; i >= 0; i--) { if (!anims[i].persist) anims.RemoveAt(i); else anims[i].t = anims[i].duration; }
            foreach (var a in State.illustration.actors) a.speakTimer = 0f;
            State.pageGlow = 0f; State.pageDarkness = 0f; State.subtitle = "";
            finishing = true;
        }

        void Finish(bool skipped)
        {
            if (Finished) return;
            Finished = true;
            if (skipped) { stage.StopAllAudio(); stage.SetBookPoseImmediate(State.pose, State.spreadIndex); }
            if (!committed && sink != null) { committed = true; sink.SceneFinished(Scene.id, skipped); }
            stage.SceneEnded(Scene.id, skipped);
            Ended?.Invoke(this);
        }

        // ---------------- commands ----------------
        bool Holds(string condition)
        {
            if (string.IsNullOrEmpty(condition)) return true;
            foreach (var raw in condition.Split('&'))
            {
                var term = raw.Trim(); if (term.Length == 0) continue;
                var negate = term[0] == '!'; if (negate) term = term.Substring(1);
                bool value;
                if (term.StartsWith("fact:", StringComparison.Ordinal)) value = ctx.HasFact(term.Substring(5));
                else if (term.StartsWith("corr:", StringComparison.Ordinal)) value = ctx.HasCorrection(term.Substring(5));
                else if (term == "replay") value = Replay;
                else if (term == "first") value = !Replay;
                else if (term.StartsWith("state:", StringComparison.Ordinal)) value = (int)State.narrator >= int.Parse(term.Substring(6));
                else { ChronicleLog.Warn("Unknown Chronicle condition '" + term + "' in " + Scene.id); value = true; }
                if (value == negate) return false;
            }
            return true;
        }

        void Hold(float seconds, bool instant) { if (!instant && seconds > 0f) wait = Math.Max(wait, seconds); }

        void AddTween(float seconds, Action<float> apply, bool instant, bool illustration)
        {
            if (instant || seconds <= 0f) { apply(1f); return; }
            tweens.Add(new Tween { d = seconds, apply = apply, illustration = illustration });
        }

        static ChronicleSpreadLayout Layout(string name) => Enum.TryParse(name, out ChronicleSpreadLayout l) ? l : ChronicleSpreadLayout.IllustrationLeftTextRight;
        bool SpreadIsDirty => State.blocks.Count > 0 || State.illustration.visible;

        void ResetSpread()
        {
            State.blocks.Clear(); budget.Reset(); activeWrite = null;
            tweens.RemoveAll(t => t.illustration);
            var ill = State.illustration; ill.visible = false; ill.alpha = 0f; ill.actors.Clear(); ill.animations.Clear(); ill.backdrop = "";
        }

        void TurnToNewSpread(ChronicleSpreadLayout layout, bool instant)
        {
            State.spreadIndex++; State.layout = layout; ResetSpread();
            if (instant) return;
            stage.BookAction(ChronicleBookAction.TurnPageForward, State.narrator);
            Hold(stage.BookActionDuration(ChronicleBookAction.TurnPageForward), false);
        }

        void Execute(ChronicleCommand c, bool instant)
        {
            if (!Holds(c.condition)) return;
            var ill = State.illustration;
            switch (c.op)
            {
                case ChronicleOp.OpenBook:
                {
                    var layout = Layout(c.arg);
                    if (State.pose == ChronicleBookPose.Closed)
                    {
                        State.pose = ChronicleBookPose.Open; State.layout = layout; ResetSpread();
                        if (!instant) { stage.BookAction(ChronicleBookAction.Open, State.narrator); Hold(c.seconds > 0f ? c.seconds : stage.BookActionDuration(ChronicleBookAction.Open), false); }
                    }
                    else if (SpreadIsDirty) TurnToNewSpread(layout, instant);
                    else State.layout = layout;
                    break;
                }
                case ChronicleOp.CloseBook:
                    if (State.pose == ChronicleBookPose.Open)
                    {
                        State.pose = ChronicleBookPose.Closed; ResetSpread();
                        if (!instant) { stage.BookAction(ChronicleBookAction.Close, State.narrator); Hold(stage.BookActionDuration(ChronicleBookAction.Close), false); }
                    }
                    break;
                case ChronicleOp.TurnPage:
                    // Covers stay fully open during a flip; turning a closed book is refused rather than faked.
                    if (State.pose != ChronicleBookPose.Open) { ChronicleLog.Warn(Scene.id + ": TurnPage ignored because the book is closed"); break; }
                    TurnToNewSpread(Layout(c.arg), instant); break;
                case ChronicleOp.Wait: Hold(c.seconds, instant); break;

                case ChronicleOp.ShowNarration:
                case ChronicleOp.WriteText:
                {
                    var text = c.text;
                    var correctionId = "";
                    if (string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(c.arg) && c.id.StartsWith("corr:", StringComparison.Ordinal))
                    {
                        var def = ChronicleCatalog.FindCorrection(c.arg);
                        if (def == null) { ChronicleLog.Warn(Scene.id + ": unknown correction " + c.arg); break; }
                        text = def.original; correctionId = def.id;
                    }
                    var isSpoken = c.style == ChronicleTextStyle.Dialogue || c.style == ChronicleTextStyle.DeeperVoice;
                    var block = BeginBlock(c.id, text, c.style, isSpoken ? c.arg : "", correctionId, false, instant);
                    if (c.op == ChronicleOp.ShowNarration)
                    {
                        State.subtitle = block.text;
                        if (!instant && !string.IsNullOrEmpty(c.arg)) stage.PlayAudio(c.arg, 1f);
                        else if (!instant) State.lastAudioCue = "ink_scratch";
                    }
                    if (!block.complete)
                    {
                        activeWrite = block; activeBeat = c.seconds;
                    }
                    else Hold(c.seconds, instant);
                    break;
                }
                case ChronicleOp.PauseWriting:
                    if (!instant) { Hold(c.seconds * narratorStyle.pauseScale + narratorStyle.doubtPause * .5f, false); }
                    break;
                case ChronicleOp.CrossOutText:
                {
                    // In the archive a strike tied to a correction only shows once that correction is discovered.
                    // c.text can name a correction that gates a plain block strike (see StrikeBlock).
                    var gate = string.IsNullOrEmpty(c.text) ? c.id : c.text;
                    if (ArchiveMode && ChronicleCatalog.FindCorrection(gate) != null && !ctx.HasCorrection(gate)) break;
                    var target = FindBlock("corr:" + c.id) ?? FindBlock(c.id);
                    if (target == null) { ChronicleLog.Warn(Scene.id + ": nothing to cross out for " + c.id); break; }
                    if (c.arg == "fade")
                    {
                        target.erased = true;
                        var fadeSeconds = settings.reduceMotion ? .5f : 1.2f;
                        if (!instant) { AddTween(fadeSeconds, e => target.eraseProgress = e, false, false); Hold(fadeSeconds, false); } else target.eraseProgress = 1f;
                        break;
                    }
                    target.struck = true;
                    if (!instant)
                    {
                        stage.PlayAudio("red_ink", 1f); stage.BookAction(ChronicleBookAction.ReactToCorrection, State.narrator);
                        var seconds = settings.reduceMotion ? .5f : .9f;
                        AddTween(seconds, e => target.strikeProgress = e, false, false); Hold(seconds + .15f, false);
                    }
                    else target.strikeProgress = 1f;
                    break;
                }
                case ChronicleOp.WriteCorrection:
                {
                    if (ArchiveMode && !ctx.HasCorrection(c.id)) break;
                    var def = ChronicleCatalog.FindCorrection(c.id);
                    if (def == null) { ChronicleLog.Warn(Scene.id + ": unknown correction " + c.id); break; }
                    var block = BeginBlock("fix:" + c.id, def.corrected, ChronicleTextStyle.Correction, "", def.id, true, instant);
                    if (!block.complete) { activeWrite = block; activeBeat = c.seconds; } else Hold(c.seconds, instant);
                    break;
                }

                case ChronicleOp.ShowIllustration:
                {
                    var pair = ChronicleBackdrops.Get(c.id);
                    ill.visible = true; ill.backdrop = c.id; ill.top = pair.top; ill.bottom = pair.bottom;
                    AddTween(c.seconds, e => ill.alpha = e, instant, true);
                    Hold(c.blocking ? c.seconds : 0f, instant);
                    break;
                }
                case ChronicleOp.HideIllustration:
                    AddTween(c.seconds, e => { ill.alpha = 1f - e; if (e >= 1f) ill.visible = false; }, instant, true); break;
                case ChronicleOp.SpawnActor:
                {
                    var existing = ill.Find(c.id); if (existing != null) ill.actors.Remove(existing);
                    ill.actors.Add(new ChronicleActor { id = c.id, shape = c.shape, color = c.color, x = c.x, y = c.y, w = c.w, h = c.h, label = c.arg, alpha = c.amount <= 0f ? 1f : c.amount });
                    break;
                }
                case ChronicleOp.MoveIllustrationCharacter:
                {
                    var a = ill.Find(c.id); if (a == null) { ChronicleLog.Warn(Scene.id + ": no actor " + c.id); break; }
                    float sx = a.x, sy = a.y, tx = c.x, ty = c.y;
                    AddTween(c.seconds, e => { a.x = sx + (tx - sx) * e; a.y = sy + (ty - sy) * e; }, instant, true);
                    Hold(c.blocking ? c.seconds : 0f, instant); break;
                }
                case ChronicleOp.FadeIllustrationCharacter:
                {
                    var a = ill.Find(c.id); if (a == null) { ChronicleLog.Warn(Scene.id + ": no actor " + c.id); break; }
                    var from = a.alpha; var to = c.amount;
                    AddTween(c.seconds, e => a.alpha = from + (to - from) * e, instant, true);
                    Hold(c.blocking ? c.seconds : 0f, instant); break;
                }
                case ChronicleOp.ChangeIllustrationBackground:
                {
                    var pair = ChronicleBackdrops.Get(c.id); var t0 = ill.top; var b0 = ill.bottom; ill.backdrop = c.id;
                    if (!ill.visible) { ill.visible = true; ill.alpha = 1f; t0 = pair.top; b0 = pair.bottom; }
                    AddTween(c.seconds, e => { ill.top = ChronicleColor.Lerp(t0, pair.top, e); ill.bottom = ChronicleColor.Lerp(b0, pair.bottom, e); }, instant, true);
                    Hold(c.blocking ? c.seconds : 0f, instant); break;
                }
                case ChronicleOp.AnimateIllustration:
                {
                    var persist = ChronicleAnimations.Persists(c.arg);
                    var intensity = c.amount;
                    if (settings.reduceFlashing && ChronicleAnimations.IsFlashy(c.arg)) intensity *= .3f;
                    if (settings.reduceMotion && (c.arg == "shake" || c.arg == "clash")) intensity = 0f;
                    if (c.arg == "speak") { var a = ill.Find(c.id); if (a != null && !instant) a.speakTimer = c.seconds; break; }
                    if (instant && !persist) break;
                    ill.animations.Add(new ChronicleAnimation { name = c.arg, target = c.id, duration = Math.Max(.01f, c.seconds), intensity = intensity, persist = persist, t = instant ? Math.Max(.01f, c.seconds) : 0f });
                    Hold(c.blocking ? c.seconds : 0f, instant); break;
                }
                case ChronicleOp.CameraMove:
                {
                    float sx = State.camera.x, sy = State.camera.y, tx = c.x, ty = c.y; var seconds = settings.reduceMotion ? c.seconds * .4f : c.seconds;
                    AddTween(seconds, e => { State.camera.x = sx + (tx - sx) * e; State.camera.y = sy + (ty - sy) * e; }, instant, false); break;
                }
                case ChronicleOp.CameraZoom:
                {
                    var z0 = State.camera.zoom; var z1 = c.amount; var seconds = settings.reduceMotion ? c.seconds * .4f : c.seconds;
                    AddTween(seconds, e => State.camera.zoom = z0 + (z1 - z0) * e, instant, false); break;
                }
                case ChronicleOp.PlayAudio: if (!instant) { stage.PlayAudio(c.id, c.amount); State.lastAudioCue = c.id; } break;
                case ChronicleOp.TriggerMagicEffect:
                    if (instant) break;
                    {
                        var intensity = c.amount; if (settings.reduceFlashing && ChronicleAnimations.IsFlashy(c.id)) intensity *= .3f;
                        State.pageGlow = Math.Max(State.pageGlow, intensity);
                        if (c.id == "deeper_voice") State.pageDarkness = Math.Max(State.pageDarkness, intensity);
                        stage.MagicEffect(c.id, c.x, c.y, intensity);
                        if (intensity >= .6f) stage.BookAction(ChronicleBookAction.MajorMagic, State.narrator);
                    }
                    break;
                case ChronicleOp.SaveCorrection: sink?.SaveCorrection(c.id, Scene.id); break;
                case ChronicleOp.DiscoverFact: sink?.DiscoverFact(c.id, Scene.id); break;
                case ChronicleOp.FinishScene: finishing = true; break;
            }
        }

        ChronicleTextBlock FindBlock(string id) { foreach (var b in State.transcript) if (b.id == id) return b; return null; }

        ChroniclePageSide[] SidesFor()
        {
            switch (State.layout)
            {
                case ChronicleSpreadLayout.TextLeftIllustrationRight: return new[] { ChroniclePageSide.Left };
                case ChronicleSpreadLayout.TextOnly: return new[] { ChroniclePageSide.Left, ChroniclePageSide.Right };
                default: return new[] { ChroniclePageSide.Right };
            }
        }

        ChronicleTextBlock BeginBlock(string id, string text, ChronicleTextStyle style, string speaker, string correctionId, bool isCorrectionText, bool instant)
        {
            var block = new ChronicleTextBlock { id = id, style = style, speaker = speaker ?? "", correctionId = correctionId ?? "", isCorrectionText = isCorrectionText, spread = State.spreadIndex };
            ChronicleHandwriting.Prepare(block, text, metrics);
            var height = ChronicleLayout.HeightOf(block.text, metrics, style) + (string.IsNullOrEmpty(block.speaker) ? 0f : metrics.LineHeightFor(style));
            var sides = SidesFor(); var chosen = sides[sides.Length - 1]; var fits = false;
            foreach (var side in sides) if (budget.Fits(side, height)) { chosen = side; fits = true; break; }
            if (!fits) { block.overflow = true; State.overflowCount++; }
            budget.Place(chosen, height); block.side = chosen;
            if (instant || settings.instantText) ChronicleHandwriting.Complete(block);
            State.blocks.Add(block); State.transcript.Add(block);
            return block;
        }
    }

    // ------------------------------------------------------------------------------------------------
    // Director: one scene at a time
    // ------------------------------------------------------------------------------------------------

    /// <summary>Owns the single active sequencer. Refuses overlapping playback and ignores skip presses that arrive in the
    /// grace window right after a scene starts, so the press that launched a scene cannot instantly cancel it.</summary>
    public sealed class ChronicleDirector
    {
        public const float SkipGrace = .35f;
        public ChronicleSequencer Current { get; private set; }
        public bool IsPlaying => Current != null && !Current.Finished;
        public event Action<ChronicleSequencer> SceneEnded;
        float sinceStart;

        public bool TryPlay(ChronicleScene scene, IChronicleStage stage, IChronicleContext ctx, IChronicleCommitSink sink, ChronicleSettings settings, ChronicleBookPose startPose, bool replay, ChroniclePageMetrics metrics = null, int startSpread = 0)
        {
            if (IsPlaying || scene == null) return false;
            Current = new ChronicleSequencer(scene, stage, ctx, sink, settings, metrics, replay);
            Current.Ended += s => SceneEnded?.Invoke(s);
            sinceStart = 0f; Current.Start(startPose, startSpread);
            return true;
        }

        public void Update(float dt) { if (IsPlaying) { sinceStart += dt; Current.Update(dt); } }

        /// <summary>Returns true when a skip was actually performed (so the caller can consume the input).</summary>
        public bool RequestSkip()
        {
            if (!IsPlaying || sinceStart < SkipGrace) return false;
            Current.Skip(); return true;
        }

        public void Pause() { if (IsPlaying) Current.Pause(); }
        public void Resume() { if (IsPlaying) Current.Resume(); }
        public void Abort() { if (IsPlaying) Current.Skip(); }
    }

    /// <summary>Runs a scene to its end instantly, with no stage and no saving, to read its final page content
    /// (used by the archive and by validation).</summary>
    public static class ChronicleSceneResolver
    {
        public static ChronicleSceneState Resolve(ChronicleScene scene, IChronicleContext ctx, ChroniclePageMetrics metrics = null, ChronicleBookPose startPose = ChronicleBookPose.Open)
        {
            var seq = new ChronicleSequencer(scene, new NullChronicleStage(), ctx, null, null, metrics, true, true);
            seq.Start(startPose); seq.ResolveInstantly();
            return seq.State;
        }
    }
}
