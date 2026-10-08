using System;

namespace GildedFate.Chronicle
{
    // The Chronicle's logic lives in its own engine-free assembly (noEngineReferences) so the story,
    // unlock rules, save data and scene engine can be unit-tested without Unity, and so the rendering
    // layer (book rig, illustration renderer, UI) can be replaced without touching narrative logic.

    /// <summary>Heroes in the same order as the game's HeroId enum (Vanguard, Hexer, Reaper).</summary>
    public enum ChronicleHero { Vanguard = 0, Hexer = 1, Reaper = 2 }

    public enum ChronicleNarratorState { ConfidentHistorian = 0, DoubtingHistorian = 1, RememberingObserver = 2 }

    public enum ChronicleBookPose { Closed, Open }

    public enum ChronicleBookAction { Open, Close, TurnPageForward, TurnPageBack, Idle, ReactToCorrection, MajorMagic }

    public enum ChroniclePageSide { Left = 0, Right = 1 }

    /// <summary>What each page of the current spread hosts.</summary>
    public enum ChronicleSpreadLayout { IllustrationLeftTextRight, TextLeftIllustrationRight, TextOnly, IllustrationOnly }

    public enum ChronicleTextStyle { Narration, Heading, Dialogue, DeeperVoice, Quote, Correction, Note, Ending }

    public enum ChronicleShape { Rect, Figure, Circle, Castle, Eye, Shadow, Mote, Clock, Glyph, Page, Line }

    [Serializable]
    public struct ChronicleColor
    {
        public float r, g, b, a;
        public ChronicleColor(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static ChronicleColor Lerp(ChronicleColor x, ChronicleColor y, float t)
        {
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            return new ChronicleColor(x.r + (y.r - x.r) * t, x.g + (y.g - x.g) * t, x.b + (y.b - x.b) * t, x.a + (y.a - x.a) * t);
        }
        public ChronicleColor WithAlpha(float alpha) => new ChronicleColor(r, g, b, alpha);

        // Character palette shared by every placeholder illustration.
        public static readonly ChronicleColor Vanguard = new ChronicleColor(1f, .36f, .2f);
        public static readonly ChronicleColor Hexer = new ChronicleColor(.72f, .36f, 1f);
        public static readonly ChronicleColor Reaper = new ChronicleColor(.22f, .95f, .85f);
        public static readonly ChronicleColor Fourth = new ChronicleColor(1f, .93f, .66f);   // ivory-gold: the forgotten companion
        public static readonly ChronicleColor Being = new ChronicleColor(1f, .74f, .22f);    // gold: the traveler
        public static readonly ChronicleColor Shade = new ChronicleColor(.08f, .05f, .12f);
        public static readonly ChronicleColor Ink = new ChronicleColor(.16f, .1f, .06f);
        public static readonly ChronicleColor RedInk = new ChronicleColor(.78f, .08f, .08f);
        public static readonly ChronicleColor Emerald = new ChronicleColor(.2f, .9f, .6f);
        public static readonly ChronicleColor Parchment = new ChronicleColor(.93f, .86f, .7f);
        public static ChronicleColor ForHero(ChronicleHero hero) => hero == ChronicleHero.Vanguard ? Vanguard : hero == ChronicleHero.Hexer ? Hexer : Reaper;
    }

    /// <summary>Player-facing and developer settings that change how the Chronicle plays (never what it means).</summary>
    [Serializable]
    public sealed class ChronicleSettings
    {
        public bool instantText;          // accessibility: every written passage appears immediately
        public bool reduceMotion;         // no tremors, shorter camera moves
        public bool reduceFlashing;       // no flashes or strobing magic
        public float textSpeed = 1f;      // 0.5 .. 2
        public float fastForward = 1f;    // developer: scale all time
        public static ChronicleSettings Default => new ChronicleSettings();
    }

    /// <summary>How the narrator's presentation shifts between the three narrative states. All three drive the same book model.</summary>
    public struct ChronicleNarratorStyle
    {
        public float charsPerSecond, pauseScale, tremor, doubtPause;
        public ChronicleColor inkTint;
        public bool extraGlow;

        public static ChronicleNarratorStyle For(ChronicleNarratorState state)
        {
            switch (state)
            {
                case ChronicleNarratorState.DoubtingHistorian:
                    return new ChronicleNarratorStyle { charsPerSecond = 20f, pauseScale = 1.5f, tremor = .35f, doubtPause = .45f, inkTint = new ChronicleColor(.2f, .12f, .08f) };
                case ChronicleNarratorState.RememberingObserver:
                    return new ChronicleNarratorStyle { charsPerSecond = 28f, pauseScale = .85f, tremor = .15f, doubtPause = .2f, inkTint = new ChronicleColor(.05f, .2f, .22f), extraGlow = true };
                default:
                    return new ChronicleNarratorStyle { charsPerSecond = 24f, pauseScale = 1f, tremor = 0f, doubtPause = 0f, inkTint = new ChronicleColor(.16f, .1f, .06f) };
            }
        }
    }

    /// <summary>Engine-free logging so the logic assembly never needs UnityEngine.Debug. The Unity layer assigns these.</summary>
    public static class ChronicleLog
    {
        public static Action<string> Warn = _ => { };
        public static Action<string> Info = _ => { };
    }
}
