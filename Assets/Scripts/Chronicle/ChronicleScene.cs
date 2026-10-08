using System;
using System.Collections.Generic;

namespace GildedFate.Chronicle
{
    public enum ChronicleOp
    {
        OpenBook, CloseBook, TurnPage, Wait,
        ShowNarration, WriteText, PauseWriting, CrossOutText, WriteCorrection,
        ShowIllustration, HideIllustration, SpawnActor, MoveIllustrationCharacter, FadeIllustrationCharacter,
        ChangeIllustrationBackground, AnimateIllustration,
        CameraMove, CameraZoom, PlayAudio, TriggerMagicEffect,
        SaveCorrection, DiscoverFact, FinishScene
    }

    /// <summary>
    /// One step of a scene. A single flat, serializable shape (op + a few generic fields) so scenes can be authored in code
    /// today and loaded from ScriptableObjects or JSON later without changing the sequencer.
    /// </summary>
    [Serializable]
    public sealed class ChronicleCommand
    {
        public ChronicleOp op;
        public string id = "";            // actor / block / correction / fact / audio cue / backdrop / effect id
        public string text = "";          // text to write
        public string arg = "";           // layout name, shape name, speaker, animation name...
        public float x, y, w, h;          // normalized illustration coordinates (0-1) or camera offsets
        public float seconds, amount;     // duration; intensity / alpha / zoom
        public bool blocking = true;      // does the scene wait for this to finish?
        public ChronicleColor color;
        public ChronicleTextStyle style;
        public ChronicleShape shape;
        public string condition = "";     // adaptive narration: "fact:ID", "!fact:ID", "corr:ID", "!corr:ID", "replay", "first", joined with &
    }

    [Serializable]
    public sealed class ChronicleScene
    {
        public string id = "", title = "";
        public int narratorOverride = -1; // -1 = follow the player's progress; otherwise a ChronicleNarratorState value
        public ChronicleBookPose endPose = ChronicleBookPose.Open;
        public List<ChronicleCommand> commands = new List<ChronicleCommand>();
        public int CountOf(ChronicleOp op) { var n = 0; foreach (var c in commands) if (c.op == op) n++; return n; }
    }

    /// <summary>Authoring DSL. Each method appends one command; nothing here knows how scenes are drawn.</summary>
    public sealed class ChronicleSceneBuilder
    {
        readonly ChronicleScene scene = new ChronicleScene();
        readonly List<string> conditions = new List<string>();
        int blockCounter;

        public ChronicleSceneBuilder(string id, string title) { scene.id = id; scene.title = title; }
        public ChronicleSceneBuilder Narrator(ChronicleNarratorState state) { scene.narratorOverride = (int)state; return this; }

        ChronicleCommand Add(ChronicleOp op)
        {
            var c = new ChronicleCommand { op = op, condition = string.Join("&", conditions) };
            scene.commands.Add(c); return c;
        }
        string NextBlock() => "b" + (++blockCounter);

        // ----- conditions (adaptive narration) -----
        /// <summary>Everything added until EndIf runs only when the condition holds, e.g. If("!fact:FACT_FOURTH_COMPANION").</summary>
        public ChronicleSceneBuilder If(string condition) { conditions.Add(condition); return this; }
        public ChronicleSceneBuilder EndIf() { if (conditions.Count > 0) conditions.RemoveAt(conditions.Count - 1); return this; }

        // ----- book -----
        /// <summary>Ensures the book is open on a fresh spread. A closed book opens; an already-open book turns to a new page.</summary>
        public ChronicleSceneBuilder Open(ChronicleSpreadLayout layout = ChronicleSpreadLayout.IllustrationLeftTextRight, float seconds = 0f) { var c = Add(ChronicleOp.OpenBook); c.arg = layout.ToString(); c.seconds = seconds; return this; }
        public ChronicleSceneBuilder Close() { Add(ChronicleOp.CloseBook); scene.endPose = ChronicleBookPose.Closed; return this; }
        public ChronicleSceneBuilder Turn(ChronicleSpreadLayout layout = ChronicleSpreadLayout.IllustrationLeftTextRight) { Add(ChronicleOp.TurnPage).arg = layout.ToString(); return this; }
        public ChronicleSceneBuilder Wait(float seconds) { Add(ChronicleOp.Wait).seconds = seconds; return this; }

        // ----- illustration -----
        public ChronicleSceneBuilder Illustrate(string backdrop, float fadeSeconds = .6f) { var c = Add(ChronicleOp.ShowIllustration); c.id = backdrop; c.seconds = fadeSeconds; c.blocking = false; return this; }
        public ChronicleSceneBuilder HideIllustration(float seconds = .4f) { var c = Add(ChronicleOp.HideIllustration); c.seconds = seconds; c.blocking = false; return this; }
        public ChronicleSceneBuilder Actor(string id, ChronicleShape shape, ChronicleColor color, float x, float y, float w = .09f, float h = .22f, string label = "", float alpha = 1f)
        { var c = Add(ChronicleOp.SpawnActor); c.id = id; c.shape = shape; c.color = color; c.x = x; c.y = y; c.w = w; c.h = h; c.arg = label; c.amount = alpha; return this; }
        public ChronicleSceneBuilder Hero(string id, ChronicleHero hero, float x, float y, float alpha = 1f) => Actor(id, ChronicleShape.Figure, ChronicleColor.ForHero(hero), x, y, .09f, .24f, ChronicleCatalog.HeroName(hero), alpha);
        public ChronicleSceneBuilder Move(string id, float x, float y, float seconds, bool wait = false) { var c = Add(ChronicleOp.MoveIllustrationCharacter); c.id = id; c.x = x; c.y = y; c.seconds = seconds; c.blocking = wait; return this; }
        public ChronicleSceneBuilder Fade(string id, float alpha, float seconds, bool wait = false) { var c = Add(ChronicleOp.FadeIllustrationCharacter); c.id = id; c.amount = alpha; c.seconds = seconds; c.blocking = wait; return this; }
        public ChronicleSceneBuilder Backdrop(string backdrop, float seconds = 1f, bool wait = false) { var c = Add(ChronicleOp.ChangeIllustrationBackground); c.id = backdrop; c.seconds = seconds; c.blocking = wait; return this; }
        /// <summary>Timed effect inside the illustration: flash, shake, pulse, expand_circle, speak, threads, fracture, shatter_world, split_screen...</summary>
        public ChronicleSceneBuilder Anim(string name, string target = "", float seconds = 1f, float intensity = 1f, bool wait = false) { var c = Add(ChronicleOp.AnimateIllustration); c.arg = name; c.id = target; c.seconds = seconds; c.amount = intensity; c.blocking = wait; return this; }
        public ChronicleSceneBuilder Camera(float x, float y, float seconds = 1f) { var c = Add(ChronicleOp.CameraMove); c.x = x; c.y = y; c.seconds = seconds; c.blocking = false; return this; }
        public ChronicleSceneBuilder Zoom(float zoom, float seconds = 1f) { var c = Add(ChronicleOp.CameraZoom); c.amount = zoom; c.seconds = seconds; c.blocking = false; return this; }
        public ChronicleSceneBuilder Audio(string cue, float volume = 1f) { var c = Add(ChronicleOp.PlayAudio); c.id = cue; c.amount = volume; c.blocking = false; return this; }
        /// <summary>A page-wide magical reaction (ink glow, tremor, emerald light). Intensity 0.6+ also triggers the book's major reaction.</summary>
        public ChronicleSceneBuilder Magic(string effect, float intensity = .6f, float x = .5f, float y = .5f) { var c = Add(ChronicleOp.TriggerMagicEffect); c.id = effect; c.amount = intensity; c.x = x; c.y = y; c.blocking = false; return this; }

        // ----- text -----
        /// <summary>The narrator speaks: written on the page. Inline {p=0.8} marks a pause in the handwriting.</summary>
        public ChronicleSceneBuilder Say(string text, float beat = .35f, string voiceId = "") { var c = Add(ChronicleOp.ShowNarration); c.id = NextBlock(); c.text = text; c.style = ChronicleTextStyle.Narration; c.seconds = beat; c.arg = voiceId; return this; }
        public ChronicleSceneBuilder Write(string text, ChronicleTextStyle style = ChronicleTextStyle.Heading, float beat = .4f) { var c = Add(ChronicleOp.WriteText); c.id = NextBlock(); c.text = text; c.style = style; c.seconds = beat; return this; }
        public ChronicleSceneBuilder Heading(string text) => Write(text, ChronicleTextStyle.Heading, .5f);
        /// <summary>Writes a passage under a fixed ID so a later command can strike or erase it.</summary>
        public ChronicleSceneBuilder WriteBlock(string blockId, string text, ChronicleTextStyle style = ChronicleTextStyle.Narration, float beat = .4f) { var c = Add(ChronicleOp.WriteText); c.id = blockId; c.text = text; c.style = style; c.seconds = beat; return this; }
        /// <summary>Crosses a passage out in red ink (not tied to a correction).</summary>
        /// <param name="shownOnceCorrection">In the archive, show this strike only after that correction has been discovered.</param>
        public ChronicleSceneBuilder StrikeBlock(string blockId, string shownOnceCorrection = "") { var c = Add(ChronicleOp.CrossOutText); c.id = blockId; c.text = shownOnceCorrection; return this; }
        /// <summary>Fades a passage away before it becomes permanent.</summary>
        public ChronicleSceneBuilder Erase(string blockId) { var c = Add(ChronicleOp.CrossOutText); c.id = blockId; c.arg = "fade"; return this; }
        public ChronicleSceneBuilder Speak(string actorId, string speaker, string line, float beat = .4f)
        {
            Anim("speak", actorId, Math.Max(.8f, line.Length / 28f), .6f, false);
            var c = Add(ChronicleOp.WriteText); c.id = NextBlock(); c.text = line; c.arg = speaker; c.style = ChronicleTextStyle.Dialogue; c.seconds = beat; return this;
        }
        /// <summary>The Being's deeper voice: darker ink, a page-darkening reaction, written with deliberate slowness.</summary>
        public ChronicleSceneBuilder Deeper(string line, float beat = .6f)
        {
            Magic("deeper_voice", .7f); Audio("deeper_voice", .8f);
            var c = Add(ChronicleOp.WriteText); c.id = NextBlock(); c.text = line; c.arg = "DEEPER VOICE"; c.style = ChronicleTextStyle.DeeperVoice; c.seconds = beat; return this;
        }
        public ChronicleSceneBuilder Hesitate(float seconds) { Add(ChronicleOp.PauseWriting).seconds = seconds; return this; }

        // ----- corrections (structured, by stable ID; text comes from the catalog) -----
        /// <summary>Writes the correction's ORIGINAL sentence, so it can later be struck through.</summary>
        public ChronicleSceneBuilder Original(string correctionId, float beat = .4f) { var c = Add(ChronicleOp.WriteText); c.id = "corr:" + correctionId; c.arg = correctionId; c.style = ChronicleTextStyle.Narration; c.seconds = beat; return this; }
        public ChronicleSceneBuilder Strike(string correctionId) { var c = Add(ChronicleOp.CrossOutText); c.id = correctionId; return this; }
        public ChronicleSceneBuilder Corrected(string correctionId, float beat = .5f) { var c = Add(ChronicleOp.WriteCorrection); c.id = correctionId; c.seconds = beat; return this; }
        public ChronicleSceneBuilder Save(string correctionId) { Add(ChronicleOp.SaveCorrection).id = correctionId; return this; }
        /// <summary>The full correction beat: strike the original in red, write the replacement beneath it, record it permanently.</summary>
        public ChronicleSceneBuilder Correct(string correctionId) => Strike(correctionId).Corrected(correctionId).Save(correctionId);
        public ChronicleSceneBuilder Fact(string factId) { Add(ChronicleOp.DiscoverFact).id = factId; return this; }

        public ChronicleScene Finish()
        {
            Add(ChronicleOp.FinishScene);
            return scene;
        }
    }
}
