using System;

namespace GildedFate.Chronicle
{
    /// <summary>
    /// The journal's breathing: how long the pen rests after a passage, how a page turn is anticipated and lands, and how long a
    /// newly finished chapter is held. Shaped by each passage's own punctuation and length, so sentences no longer share one
    /// identical delay, and shortened on revisits so a cinematic stays cinematic the first time without becoming tedious.
    /// Deterministic (no randomness), so skips, tests and replays always agree.
    /// </summary>
    public static class ChronicleRhythm
    {
        public const float RevisitScale = .7f;

        /// <summary>The rest after a passage. authored = the scene's own beat, which is kept as the foundation.</summary>
        public static float Beat(string text, float authored, bool revisit, bool instantText)
        {
            var beat = Math.Max(0f, authored);
            var t = (text ?? "").TrimEnd();
            if (t.Length > 0)
            {
                var last = t[t.Length - 1];
                if (t.EndsWith("...", StringComparison.Ordinal) || last == '\u2026' || last == '\u2014' || last == '-') beat += .35f;   // a thought left hanging
                else if (last == '?') beat += .22f;
                else if (last == '!') beat += .15f;
                else if (last == ':' || last == ',') beat -= .05f;                                                                       // it continues at once
                if (beat > 0f) beat *= t.Length < 24 ? .9f : t.Length > 110 ? 1.12f : 1f;                                            // short lines land quickly, long ones settle
            }
            if (revisit) beat *= RevisitScale;
            if (instantText) beat *= .85f;
            return Math.Max(0f, beat);
        }

        /// <summary>The held breath between the last word on a page and the sheet lifting.</summary>
        public static float TurnAnticipation(bool revisit, bool instantText, bool reduceMotion) =>
            instantText ? .12f : revisit ? .15f : reduceMotion ? .2f : .32f;

        /// <summary>Extra rest once the turned sheet has landed, before the pen touches the new page.</summary>
        public static float TurnSettle(bool revisit) => revisit ? .06f : .14f;

        /// <summary>How long a newly finished chapter or revelation is held while its ink dries. Never on revisits.</summary>
        public static float CompletionHold(bool reduceMotion, bool instantText) => instantText ? .6f : reduceMotion ? .8f : 1.25f;
    }
}
