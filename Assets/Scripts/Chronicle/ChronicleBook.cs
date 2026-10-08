using System;

namespace GildedFate.Chronicle
{
    // The book's BEHAVIOUR, separate from how it is drawn. A rig is anything that can open, close, turn pages and react;
    // the temporary procedural book and the finished Blender model are both rigs. The story, page text and illustrations
    // never reference a book object, only this interface.

    /// <summary>Per-frame description of the book that a renderer applies. All values are renderer-neutral.</summary>
    public struct ChronicleBookFrame
    {
        public float coverAngle;        // degrees: 0 closed .. 180 fully open. Stays 180 for the whole of every page flip.
        public float openAmount;        // 0 closed .. 1 open
        public float flipProgress;      // 0..1 while a sheet is turning, else 0 (eased)
        public float flipRaw;           // 0..1 un-eased, used for overlay timing
        public int flipDirection;       // +1 turning forward (right to left), -1 turning back, 0 none
        public float flipLift;          // 0..1 arch of the turning sheet; a future curved page rig can use it as its curl
        public float rightStack, leftStack; // fractions of the page block on each side; always sum to 1
        public float cameraPitch;       // degrees tilted from straight-down: tilted while closed, 0 when open for reading
        public float glow;              // magical reaction glow, 0..~1.4
        public float tremorX, tremorZ;  // page tremors
        public float bob;               // idle float
        public float overlayAlpha;      // how visible the page content (text and illustration) should be
        public bool overlayVisible;
        public int spreadIndex;
        public bool busy;
        public ChronicleBookPose pose;
    }

    public interface IChronicleBookRig
    {
        /// <summary>Seconds the rig takes for an action. The sequencer waits exactly this long, so rig and story stay in step.</summary>
        float DurationOf(ChronicleBookAction action);
        void Perform(ChronicleBookAction action, ChronicleNarratorState narrator);
        void SetPoseImmediate(ChronicleBookPose pose, int spreadIndex);
        ChronicleBookFrame Frame { get; }
    }

    public sealed class ChronicleBookTimings
    {
        public float open = 1.6f, close = 1.3f, turn = .9f, react = .6f, major = 1.1f;
        public float Of(ChronicleBookAction action)
        {
            switch (action)
            {
                case ChronicleBookAction.Open: return open;
                case ChronicleBookAction.Close: return close;
                case ChronicleBookAction.TurnPageForward:
                case ChronicleBookAction.TurnPageBack: return turn;
                case ChronicleBookAction.ReactToCorrection: return react;
                case ChronicleBookAction.MajorMagic: return major;
                default: return 0f;
            }
        }
    }

    /// <summary>
    /// The book's pose and motion as a small state machine. Pure logic, no engine types, fully unit-tested.
    /// Hard rule (tested): once the covers are open they stay at exactly 180 degrees for the entire duration of every page turn.
    /// The Unity rigs read <see cref="Frame"/> each frame and apply it to meshes or to an imported model's Animator.
    /// </summary>
    public sealed class ChronicleBookAnimator : IChronicleBookRig
    {
        public const float MaxSpreads = 14f;   // how many spreads map to the whole page block moving from right to left
        public readonly ChronicleBookTimings timings;
        public bool reduceMotion;

        enum Phase { Closed, Opening, Open, Closing }
        Phase phase = Phase.Closed; float phaseT;
        bool flipping; float flipT; int flipDir, spread, flipFrom;
        float glow, shakeT, shakeDuration, shakeAmp, clock;
        ChronicleNarratorState narrator;

        public ChronicleBookAnimator(ChronicleBookTimings timings = null) { this.timings = timings ?? new ChronicleBookTimings(); }

        public float DurationOf(ChronicleBookAction action) => timings.Of(action);

        public void Perform(ChronicleBookAction action, ChronicleNarratorState narratorState)
        {
            narrator = narratorState;
            switch (action)
            {
                case ChronicleBookAction.Open:
                    if (phase == Phase.Closing) { phase = Phase.Closed; phaseT = 0f; }
                    if (phase == Phase.Closed) { phase = Phase.Opening; phaseT = 0f; }
                    break;
                case ChronicleBookAction.Close:
                    FinishFlip();
                    if (phase == Phase.Opening) { phase = Phase.Open; }
                    if (phase == Phase.Open) { phase = Phase.Closing; phaseT = 0f; }
                    break;
                case ChronicleBookAction.TurnPageForward: StartFlip(1); break;
                case ChronicleBookAction.TurnPageBack: StartFlip(-1); break;
                case ChronicleBookAction.ReactToCorrection:
                    glow = Math.Max(glow, .9f); Shake(timings.react, .02f * (narrator == ChronicleNarratorState.DoubtingHistorian ? 1.5f : narrator == ChronicleNarratorState.RememberingObserver ? 1f : .6f));
                    break;
                case ChronicleBookAction.MajorMagic:
                    glow = Math.Max(glow, 1.4f); Shake(timings.major, .05f);
                    break;
                case ChronicleBookAction.Idle: break;
            }
        }

        void StartFlip(int direction)
        {
            // Covers never move during a flip; turning a closed book is refused.
            if (phase != Phase.Open) return;
            FinishFlip();
            flipping = true; flipT = 0f; flipDir = direction; flipFrom = spread; spread = Math.Max(0, spread + direction);
            if (spread == flipFrom) flipping = false;
        }
        void FinishFlip() { flipping = false; flipT = 0f; flipDir = 0; flipFrom = spread; }
        void Shake(float seconds, float amplitude) { shakeT = shakeDuration = Math.Max(.01f, seconds); shakeAmp = amplitude; }

        public void SetPoseImmediate(ChronicleBookPose pose, int spreadIndex)
        {
            phase = pose == ChronicleBookPose.Open ? Phase.Open : Phase.Closed; phaseT = 0f;
            spread = Math.Max(0, spreadIndex); flipping = false; flipT = 0f; flipDir = 0; flipFrom = spread;
            glow = 0f; shakeT = 0f;
        }

        public void Update(float dt)
        {
            if (dt <= 0f) return;
            clock += dt;
            if (phase == Phase.Opening) { phaseT += dt; if (phaseT >= timings.open) { phase = Phase.Open; phaseT = 0f; } }
            else if (phase == Phase.Closing) { phaseT += dt; if (phaseT >= timings.close) { phase = Phase.Closed; phaseT = 0f; } }
            if (flipping) { flipT += dt; if (flipT >= timings.turn) { flipFrom = spread; flipping = false; flipT = 0f; flipDir = 0; } }
            glow = Math.Max(0f, glow - dt / 1.1f);
            if (shakeT > 0f) shakeT = Math.Max(0f, shakeT - dt);
        }

        public bool IsOpenOrOpening => phase == Phase.Open || phase == Phase.Opening;
        public bool Busy => phase == Phase.Opening || phase == Phase.Closing || flipping;

        static float Smooth(float a, float b, float t) { var x = Math.Max(0f, Math.Min(1f, (t - a) / (b - a))); return x * x * (3f - 2f * x); }

        public ChronicleBookFrame Frame
        {
            get
            {
                var f = new ChronicleBookFrame { spreadIndex = spread, busy = Busy, pose = phase == Phase.Open || phase == Phase.Opening ? ChronicleBookPose.Open : ChronicleBookPose.Closed };
                float coverT = phase == Phase.Opening ? phaseT / Math.Max(.01f, timings.open) : phase == Phase.Open ? 1f : phase == Phase.Closing ? 1f - phaseT / Math.Max(.01f, timings.close) : 0f;
                coverT = Math.Max(0f, Math.Min(1f, coverT));
                f.coverAngle = 180f * Smooth(.08f, .92f, coverT);
                if (phase == Phase.Open) f.coverAngle = 180f;   // exactly, never "almost": the covers are still while pages turn
                f.openAmount = f.coverAngle / 180f;
                f.cameraPitch = 28f * (1f - Smooth(0f, .85f, coverT));

                var p = flipping ? Math.Max(0f, Math.Min(1f, flipT / Math.Max(.01f, timings.turn))) : 0f;
                f.flipRaw = p; f.flipProgress = flipping ? Smooth(0f, 1f, p) : 0f; f.flipDirection = flipping ? flipDir : 0;
                f.flipLift = flipping ? (float)Math.Sin(Math.PI * p) : 0f;

                var from = Math.Max(0f, Math.Min(1f, flipFrom / MaxSpreads)); var to = Math.Max(0f, Math.Min(1f, spread / MaxSpreads));
                f.leftStack = flipping ? from + (to - from) * f.flipProgress : to; f.rightStack = 1f - f.leftStack;

                var overlay = phase == Phase.Open ? 1f : phase == Phase.Opening ? Smooth(.9f, 1f, coverT) : phase == Phase.Closing ? Smooth(.85f, 1f, coverT) : 0f;
                if (flipping) overlay *= p < .3f ? 1f - p / .3f : p < .7f ? 0f : (p - .7f) / .3f;
                f.overlayAlpha = overlay; f.overlayVisible = overlay > .001f;

                f.glow = glow;
                if (!reduceMotion)
                {
                    if (shakeT > 0f) { var k = shakeT / shakeDuration * shakeAmp; f.tremorX = (float)Math.Sin(clock * 47f) * k; f.tremorZ = (float)Math.Cos(clock * 39f) * k; }
                    if (phase == Phase.Open && narrator == ChronicleNarratorState.DoubtingHistorian) f.tremorX += (float)Math.Sin(clock * 9f) * .0025f;
                    if (phase == Phase.Open && !flipping) f.bob = (float)Math.Sin(clock * 1.1f) * .012f;
                }
                return f;
            }
        }
    }

    /// <summary>Connects the sequencer to any book rig. This is the whole of what story playback knows about the book.</summary>
    public sealed class ChronicleRigStage : IChronicleStage
    {
        readonly IChronicleBookRig rig;
        public Action<string, float> audio; public Action stopAudio; public Action<string, float, float, float> magic;
        public Action<string> sceneStarted; public Action<string, bool> sceneEnded;
        public Action<ChronicleBookAction> onBookAction;   // lets the game play page-turn and cover sounds

        public ChronicleRigStage(IChronicleBookRig rig) { this.rig = rig ?? throw new ArgumentNullException(nameof(rig)); }
        public float BookActionDuration(ChronicleBookAction action) => rig.DurationOf(action);
        public void BookAction(ChronicleBookAction action, ChronicleNarratorState narrator) { rig.Perform(action, narrator); onBookAction?.Invoke(action); }
        public void SetBookPoseImmediate(ChronicleBookPose pose, int spreadIndex) { rig.SetPoseImmediate(pose, spreadIndex); }
        public void PlayAudio(string cue, float volume) { audio?.Invoke(cue, volume); }
        public void StopAllAudio() { stopAudio?.Invoke(); }
        public void MagicEffect(string effect, float x, float y, float intensity) { magic?.Invoke(effect, x, y, intensity); }
        public void SceneStarted(string sceneId) { sceneStarted?.Invoke(sceneId); }
        public void SceneEnded(string sceneId, bool skipped) { sceneEnded?.Invoke(sceneId, skipped); }
    }
}
