using System;
using System.Collections.Generic;

namespace GildedFate.Chronicle
{
    // Plain state objects. The sequencer owns and mutates them; any renderer (placeholder or final) only reads them.

    public struct ChroniclePause { public int index; public float seconds; }

    public sealed class ChronicleTextBlock
    {
        public string id = "", text = "", speaker = "", correctionId = "";
        public ChronicleTextStyle style;
        public ChroniclePageSide side;
        public int spread;
        public bool struck, isCorrectionText, complete, overflow, erased;
        public float strikeProgress, eraseProgress;   // erased passages fade away instead of being struck in red
        public int cursor;                 // characters written so far
        public float carry, hold;          // handwriting timing
        public int[] lineStarts = new int[0];
        public List<ChroniclePause> pauses = new List<ChroniclePause>();
        public int VisibleLength => complete ? text.Length : cursor;
        /// <summary>Fraction of the passage written, 0-1.</summary>
        public float Progress => text.Length == 0 ? 1f : complete ? 1f : (float)cursor / text.Length;
    }

    public sealed class ChronicleActor
    {
        public string id = "", label = "";
        public ChronicleShape shape;
        public ChronicleColor color;
        public float x, y, w, h, alpha = 1f, rotation, scale = 1f, speakTimer;
        public ChronicleActor Clone() => (ChronicleActor)MemberwiseClone();
    }

    /// <summary>A timed visual effect inside the illustration. Persistent ones (a shattered world, drawn threads) stay at full
    /// progress once finished; transient ones (a flash, a shake) leave nothing behind, which is what skipping relies on.</summary>
    public sealed class ChronicleAnimation
    {
        public string name = "", target = "";
        public float t, duration = 1f, intensity = 1f;
        public bool persist;
        public float Progress => duration <= 0f ? 1f : Math.Min(1f, t / duration);
        public bool Done => t >= duration;
        public ChronicleAnimation Clone() => (ChronicleAnimation)MemberwiseClone();
    }

    public static class ChronicleAnimations
    {
        static readonly HashSet<string> persistent = new HashSet<string> { "threads", "fracture", "shatter_world", "split_screen", "branch", "sigil_glow" };
        public static bool Persists(string name) => persistent.Contains(name);
        public static bool IsFlashy(string name) => name == "flash" || name == "split_screen" || name == "strobe" || name == "magic_burst";
    }

    public sealed class ChronicleIllustrationState
    {
        public bool visible;
        public float alpha;                         // fades in with the illustration
        public string backdrop = "";
        public ChronicleColor top, bottom;          // current gradient (tweened between backdrops)
        public readonly List<ChronicleActor> actors = new List<ChronicleActor>();
        public readonly List<ChronicleAnimation> animations = new List<ChronicleAnimation>();
        public ChronicleActor Find(string id) { foreach (var a in actors) if (a.id == id) return a; return null; }
        /// <summary>An independent copy, used to keep the old page on screen while a sheet turns.</summary>
        public ChronicleIllustrationState Clone()
        {
            var copy = new ChronicleIllustrationState { visible = visible, alpha = alpha, backdrop = backdrop, top = top, bottom = bottom };
            foreach (var a in actors) copy.actors.Add(a.Clone());
            foreach (var a in animations) copy.animations.Add(a.Clone());
            return copy;
        }
    }

    /// <summary>The spread that was on the page before the latest turn. The presentation keeps drawing it until the turning sheet lifts away.</summary>
    public sealed class ChronicleOutgoingSpread
    {
        public int spread; public ChronicleSpreadLayout layout;
        public ChronicleIllustrationState illustration = new ChronicleIllustrationState();
        public readonly List<ChronicleTextBlock> blocks = new List<ChronicleTextBlock>();
    }

    public sealed class ChronicleCameraState { public float x, y, zoom = 1f; }

    public sealed class ChronicleSceneState
    {
        public ChronicleBookPose pose = ChronicleBookPose.Closed;
        public int spreadIndex;
        public ChronicleSpreadLayout layout = ChronicleSpreadLayout.IllustrationLeftTextRight;
        public readonly List<ChronicleTextBlock> blocks = new List<ChronicleTextBlock>();      // the current spread
        public readonly List<ChronicleTextBlock> transcript = new List<ChronicleTextBlock>();  // everything written in the scene
        public readonly ChronicleIllustrationState illustration = new ChronicleIllustrationState();
        public readonly ChronicleCameraState camera = new ChronicleCameraState();
        public ChronicleOutgoingSpread outgoing;     // null until the first page turn
        public ChronicleNarratorState narrator;
        public string subtitle = "";
        public float pageGlow, pageDarkness, tremor;
        public int overflowCount;                   // passages that did not fit their page (authoring validation)
        public string lastAudioCue = "";
    }

    /// <summary>Gradient palette for the placeholder illustrations. Replace these ids with painted art later.</summary>
    public static class ChronicleBackdrops
    {
        public struct Pair { public ChronicleColor top, bottom; }
        static ChronicleColor C(float r, float g, float b) => new ChronicleColor(r, g, b);
        static readonly Dictionary<string, Pair> table = new Dictionary<string, Pair>
        {
            { "void", new Pair { top = C(.05f, .04f, .08f), bottom = C(.1f, .08f, .13f) } },
            { "kingdom", new Pair { top = C(.35f, .5f, .75f), bottom = C(.78f, .7f, .5f) } },
            { "chamber", new Pair { top = C(.14f, .1f, .26f), bottom = C(.3f, .22f, .4f) } },
            { "field", new Pair { top = C(.12f, .14f, .2f), bottom = C(.22f, .24f, .3f) } },
            { "gate", new Pair { top = C(.4f, .45f, .6f), bottom = C(.62f, .55f, .42f) } },
            { "observatory", new Pair { top = C(.06f, .08f, .22f), bottom = C(.2f, .18f, .38f) } },
            { "split", new Pair { top = C(.3f, .28f, .46f), bottom = C(.42f, .22f, .22f) } },
            { "campfire", new Pair { top = C(.04f, .05f, .14f), bottom = C(.34f, .16f, .08f) } },
            { "diagram", new Pair { top = C(.12f, .14f, .22f), bottom = C(.18f, .2f, .3f) } },
            { "passage", new Pair { top = C(.1f, .1f, .12f), bottom = C(.26f, .24f, .22f) } },
            { "sanctuary", new Pair { top = C(.05f, .06f, .09f), bottom = C(.14f, .16f, .2f) } },
            { "threads", new Pair { top = C(.08f, .06f, .16f), bottom = C(.16f, .12f, .28f) } },
            { "fading", new Pair { top = C(.1f, .1f, .14f), bottom = C(.2f, .2f, .24f) } },
            { "collapse", new Pair { top = C(.22f, .06f, .06f), bottom = C(.1f, .04f, .08f) } },
            { "weave", new Pair { top = C(.18f, .1f, .3f), bottom = C(.34f, .2f, .12f) } },
            { "souls", new Pair { top = C(.04f, .03f, .08f), bottom = C(.18f, .12f, .06f) } },
            { "migration", new Pair { top = C(.3f, .3f, .4f), bottom = C(.5f, .42f, .32f) } },
            { "clocks", new Pair { top = C(.18f, .2f, .26f), bottom = C(.34f, .3f, .2f) } },
            { "plane", new Pair { top = C(.5f, .68f, .8f), bottom = C(.82f, .84f, .62f) } },
            { "battle", new Pair { top = C(.2f, .08f, .08f), bottom = C(.1f, .1f, .14f) } },
            { "knot", new Pair { top = C(.07f, .06f, .14f), bottom = C(.2f, .14f, .2f) } },
            { "page", new Pair { top = C(.88f, .8f, .64f), bottom = C(.8f, .7f, .52f) } },
            { "restored", new Pair { top = C(.06f, .06f, .16f), bottom = C(.42f, .22f, .1f) } },
            { "journal", new Pair { top = C(.78f, .68f, .5f), bottom = C(.64f, .54f, .38f) } },
            { "emerald", new Pair { top = C(.03f, .1f, .1f), bottom = C(.08f, .24f, .18f) } },
            { "dawn", new Pair { top = C(.3f, .34f, .5f), bottom = C(.72f, .5f, .36f) } },
        };
        public static Pair Get(string id) => table.TryGetValue(id ?? "", out var p) ? p : table["void"];
        public static bool Exists(string id) => table.ContainsKey(id ?? "");
        public static IEnumerable<string> Ids => table.Keys;
    }
}
