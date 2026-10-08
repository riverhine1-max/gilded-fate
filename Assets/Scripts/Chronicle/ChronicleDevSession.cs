using System;
using System.Collections.Generic;
using System.Text;

namespace GildedFate.Chronicle
{
    /// <summary>
    /// Everything the editor's Chronicle Developer Console does, with no UI: play any scene, skip, pause, fast-forward,
    /// override the narrator state, simulate story milestones, and read the page as text.
    /// It works on its own SANDBOX progress and never touches the player's real profile, so testing cannot overwrite real progression.
    /// </summary>
    public sealed class ChronicleDevSession
    {
        sealed class OverrideContext : IChronicleContext
        {
            public IChronicleContext inner; public int narratorOverride = -1; public readonly HashSet<string> forcedFacts = new HashSet<string>();
            public bool HasFact(string id) => forcedFacts.Contains(id) || inner.HasFact(id);
            public bool HasCorrection(string id) => inner.HasCorrection(id);
            public ChronicleNarratorState NarratorState => narratorOverride >= 0 ? (ChronicleNarratorState)narratorOverride : inner.NarratorState;
        }

        public ChronicleProgress sandbox = new ChronicleProgress();
        public ChronicleSettings settings = new ChronicleSettings();
        public RecordingChronicleStage stage = new RecordingChronicleStage();
        public ChronicleSequencer current;
        public bool saveToSandbox = true;       // false = a pure preview that records nothing, not even in the sandbox
        public bool allowLockedScenes = true;   // the sandbox may play scenes the sandbox has not unlocked
        public string message = "";
        readonly OverrideContext context = new OverrideContext();

        public ChronicleDevSession() { sandbox.Ensure(); context.inner = new ChronicleProgressContext(sandbox); }

        public int NarratorOverride { get => context.narratorOverride; set => context.narratorOverride = value; }
        public void ForceFact(string factId, bool on) { if (on) context.forcedFacts.Add(factId); else context.forcedFacts.Remove(factId); }
        public bool IsFactForced(string factId) => context.forcedFacts.Contains(factId);
        public bool IsPlaying => current != null && !current.Finished;

        // ---------------- playback ----------------
        public bool Play(string sceneId, bool replay = false)
        {
            var scene = ChronicleScripts.Get(sceneId);
            if (scene == null) { message = "No scene " + sceneId; return false; }
            if (IsPlaying) { message = "A scene is already playing. Skip or stop it first."; return false; }
            stage = new RecordingChronicleStage();
            var sink = saveToSandbox ? new ChronicleProgressSink(sandbox, allowLockedScenes) : null;
            current = new ChronicleSequencer(scene, stage, context, sink, settings, null, replay);
            var pose = sceneId.StartsWith("SCENE_", StringComparison.Ordinal) ? ChronicleBookPose.Closed : ChronicleBookPose.Open;
            current.Start(pose);
            message = "Playing " + scene.title;
            return true;
        }

        public void Tick(float dt) { if (IsPlaying) current.Update(dt); }
        public void Skip() { if (IsPlaying) { current.Skip(); message = "Skipped to the final state."; } }
        public void Stop() { if (IsPlaying) { current.Skip(); } current = null; message = "Stopped."; }
        public void Pause() { current?.Pause(); }
        public void Resume() { current?.Resume(); }

        /// <summary>Runs a scene to the end instantly, with no stage, and returns whether it finished without warnings.</summary>
        public bool CheckScene(string sceneId, out string report)
        {
            var warnings = new List<string>(); var previous = ChronicleLog.Warn; ChronicleLog.Warn = w => warnings.Add(w);
            try
            {
                var scene = ChronicleScripts.Get(sceneId);
                if (scene == null) { report = "missing"; return false; }
                var seq = new ChronicleSequencer(scene, new NullChronicleStage(), new ChronicleProgressContext(new ChronicleProgress()), null);
                seq.Start(sceneId.StartsWith("SCENE_", StringComparison.Ordinal) ? ChronicleBookPose.Closed : ChronicleBookPose.Open); seq.ResolveInstantly();
                var overflow = seq.State.overflowCount;
                report = warnings.Count == 0 && overflow == 0 ? "ok" : warnings.Count + " warnings, " + overflow + " overflowing passages";
                return warnings.Count == 0 && overflow == 0 && seq.Finished;
            }
            finally { ChronicleLog.Warn = previous; }
        }

        // ---------------- milestone simulation (sandbox only) ----------------
        public ChronicleUnlockResult SimulateRunEnded(ChronicleHero hero, bool victory, int act, int floor) { var r = sandbox.RegisterRunEnded(hero, victory, act, floor, "dev-" + Guid.NewGuid().ToString("N")); Note(r); return r; }
        public ChronicleUnlockResult SimulateBoss(ChronicleHero hero, int act) { var r = sandbox.RegisterBossDefeated(hero, act, act * 18); Note(r); return r; }
        public ChronicleUnlockResult SimulateFloor(ChronicleHero hero, int floor) { var r = sandbox.RegisterFloorReached(hero, floor); Note(r); return r; }
        public void UnlockEverythingButChapterX()
        {
            foreach (var m in ChronicleCatalog.Memories) if (!sandbox.unlockedMemories.Contains(m.id)) sandbox.unlockedMemories.Add(m.id);
            message = "All 27 memories unlocked in the sandbox (Chapter X " + (sandbox.secretUnlocked ? "unlocked" : "still locked until refreshed") + ").";
        }
        public void RefreshUnlocks() { var r = sandbox.RefreshUnlocks(); Note(r); }
        public void ResetSandbox() { var mode = sandbox.openingMode; sandbox = new ChronicleProgress { openingMode = mode }; sandbox.Ensure(); context.inner = new ChronicleProgressContext(sandbox); current = null; message = "Sandbox progress reset. The real profile was not touched."; }
        void Note(ChronicleUnlockResult r) { message = r.Any ? "Unlocked: " + string.Join(", ", r.newMemories) + (r.secretUnlocked ? " + Chapter X" : "") : "Nothing new unlocked."; }

        // ---------------- reading the page ----------------
        /// <summary>The unlock table: one line per memory with its state and its (spoiler-free) rule.</summary>
        public List<string> UnlockTable()
        {
            var lines = new List<string>();
            foreach (var m in ChronicleCatalog.Memories)
            {
                var state = sandbox.IsViewed(m.id) ? "viewed  " : sandbox.IsUnlocked(m.id) ? "unlocked" : "locked  ";
                lines.Add(m.id + "  " + state + "  Ch " + m.chapter + " " + ChronicleCatalog.HeroName(m.hero) + "  \"" + m.title + "\"  -  " + m.rule.hint);
            }
            lines.Add(ChronicleCatalog.SecretChapterId + "  " + (sandbox.secretCompleted ? "viewed  " : sandbox.secretUnlocked ? "unlocked" : "locked  ") + "  Chapter X - requires all 27 memories");
            return lines;
        }

        /// <summary>The page as text: written passages up to the pen, red strike-through for corrected sentences.
        /// Tags are limited to &lt;b&gt; and &lt;color&gt; so any rich-text label can show them.</summary>
        public static string FormatPage(ChronicleSceneState state, bool currentSpreadOnly = false)
        {
            var sb = new StringBuilder();
            foreach (var b in currentSpreadOnly ? state.blocks : state.transcript)
            {
                var written = b.text.Substring(0, Math.Max(0, Math.Min(b.text.Length, b.VisibleLength)));
                if (b.erased && b.eraseProgress >= 1f) continue;
                switch (b.style)
                {
                    case ChronicleTextStyle.Heading: case ChronicleTextStyle.Ending: sb.Append("<b>").Append(written.ToUpperInvariant()).Append("</b>"); break;
                    case ChronicleTextStyle.Note: sb.Append("<color=#888888>").Append(written).Append("</color>"); break;
                    case ChronicleTextStyle.Dialogue: sb.Append("<b>").Append(b.speaker).Append(":</b> \u201C").Append(written).Append('\u201D'); break;
                    case ChronicleTextStyle.DeeperVoice: sb.Append("<color=#7a5cff><b>").Append(b.speaker).Append(":</b> ").Append(written).Append("</color>"); break;
                    case ChronicleTextStyle.Correction: sb.Append("<color=#c01818>").Append(written).Append("</color>"); break;
                    default:
                        if (b.struck && b.strikeProgress > 0f) sb.Append("<color=#c01818>").Append(Struck(written)).Append("</color>");
                        else sb.Append(written);
                        break;
                }
                sb.Append("\n\n");
            }
            return sb.ToString();
        }

        /// <summary>Strike-through using the combining long stroke overlay, so it shows in any text control.</summary>
        public static string Struck(string text)
        {
            var sb = new StringBuilder(text.Length * 2);
            foreach (var c in text) { sb.Append(c); if (c != ' ') sb.Append('\u0336'); }
            return sb.ToString();
        }

        public string StatusLine()
        {
            if (current == null) return "Idle. " + message;
            var s = current.State;
            return (current.Finished ? (current.Skipped ? "Skipped" : "Finished") : current.Paused ? "Paused" : "Playing") + "  |  " + current.Scene.title + "  |  " + current.Elapsed.ToString("0.0") + "s  |  book " + s.pose + " spread " + s.spreadIndex + "  |  narrator " + s.narrator + "  |  " + message;
        }
    }
}
