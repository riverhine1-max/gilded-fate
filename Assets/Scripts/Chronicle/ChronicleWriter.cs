using System;
using System.Collections.Generic;
using System.Text;

namespace GildedFate.Chronicle
{
    /// <summary>Page geometry in abstract "page units" plus a text measuring function. The default measure is an estimate
    /// (used for tests and authoring validation); the Unity layer supplies real font metrics.</summary>
    public sealed class ChroniclePageMetrics
    {
        public float textWidth = 470f, textHeight = 590f, fontSize = 22f, lineHeight = 31f, paragraphGap = 14f;
        public Func<string, float, float> measure = (text, size) => text.Length * size * .5f;

        public float ScaleFor(ChronicleTextStyle style) => style == ChronicleTextStyle.Heading ? 1.4f : style == ChronicleTextStyle.Ending ? 1.6f : 1f;
        public float LineHeightFor(ChronicleTextStyle style) => lineHeight * ScaleFor(style);
    }

    /// <summary>Inline markup: {p=0.8} pauses the pen for 0.8 seconds at that point. Everything else is literal text.</summary>
    public static class ChronicleMarkup
    {
        public static string Parse(string raw, List<ChroniclePause> pauses)
        {
            pauses.Clear();
            if (string.IsNullOrEmpty(raw)) return "";
            if (raw.IndexOf("{p=", StringComparison.Ordinal) < 0) return raw;
            var sb = new StringBuilder(raw.Length);
            for (var i = 0; i < raw.Length; i++)
            {
                if (raw[i] == '{' && i + 3 < raw.Length && raw[i + 1] == 'p' && raw[i + 2] == '=')
                {
                    var close = raw.IndexOf('}', i);
                    if (close > i && float.TryParse(raw.Substring(i + 3, close - i - 3), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds))
                    {
                        pauses.Add(new ChroniclePause { index = sb.Length, seconds = seconds }); i = close; continue;
                    }
                }
                sb.Append(raw[i]);
            }
            return sb.ToString();
        }
    }

    public static class ChronicleLayout
    {
        /// <summary>Greedy word wrap. Returns the index of the first character of every line.</summary>
        public static int[] Wrap(string text, float maxWidth, float fontSize, Func<string, float, float> measure)
        {
            var starts = new List<int> { 0 };
            if (string.IsNullOrEmpty(text)) return starts.ToArray();
            var lineStart = 0; var lastBreak = -1;
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n') { starts.Add(i + 1); lineStart = i + 1; lastBreak = -1; continue; }
                if (text[i] == ' ') lastBreak = i;
                var width = measure(text.Substring(lineStart, i - lineStart + 1), fontSize);
                if (width > maxWidth && i > lineStart)
                {
                    var cut = lastBreak > lineStart ? lastBreak + 1 : i;
                    starts.Add(cut); lineStart = cut; lastBreak = -1;
                }
            }
            return starts.ToArray();
        }

        public static int LineCount(string text, ChroniclePageMetrics m, ChronicleTextStyle style)
        {
            var size = m.fontSize * m.ScaleFor(style);
            return Wrap(text, m.textWidth, size, m.measure).Length;
        }

        /// <summary>Height a passage takes, including the gap after it.</summary>
        public static float HeightOf(string text, ChroniclePageMetrics m, ChronicleTextStyle style) => LineCount(text, m, style) * m.LineHeightFor(style) + m.paragraphGap;
    }

    /// <summary>Tracks how much of each page's text area is used so passages flow onto the next page or are flagged as overflowing.</summary>
    public sealed class ChroniclePageBudget
    {
        readonly float[] used = new float[2];
        readonly ChroniclePageMetrics metrics;
        public ChroniclePageBudget(ChroniclePageMetrics metrics) { this.metrics = metrics; }
        public void Reset() { used[0] = used[1] = 0f; }
        public float Used(ChroniclePageSide side) => used[(int)side];
        public bool Fits(ChroniclePageSide side, float height) => used[(int)side] + height <= metrics.textHeight + .01f;
        public void Place(ChroniclePageSide side, float height) { used[(int)side] += height; }
    }

    /// <summary>
    /// The handwriting animation. Progressive reveal at a speed set by the narrator state and the player's text-speed setting,
    /// with a pause after every word, longer ones at punctuation, at each wrapped line, and wherever the script marks one.
    /// Replaceable later by a stroke-by-stroke renderer: it only reads <c>cursor</c>/<c>Progress</c>, never the script.
    /// </summary>
    public static class ChronicleHandwriting
    {
        public static void Prepare(ChronicleTextBlock block, string raw, ChroniclePageMetrics metrics)
        {
            block.text = ChronicleMarkup.Parse(raw, block.pauses);
            block.lineStarts = ChronicleLayout.Wrap(block.text, metrics.textWidth, metrics.fontSize * metrics.ScaleFor(block.style), metrics.measure);
            block.cursor = 0; block.carry = 0f; block.hold = 0f; block.complete = block.text.Length == 0;
        }

        public static void Complete(ChronicleTextBlock block) { block.cursor = block.text.Length; block.complete = true; block.carry = 0f; block.hold = 0f; }

        public static void Step(ChronicleTextBlock block, float dt, ChronicleNarratorStyle narrator, ChronicleSettings settings)
        {
            if (block.complete) return;
            if (settings.instantText) { Complete(block); return; }
            var cps = Math.Max(4f, narrator.charsPerSecond * Math.Max(.25f, settings.textSpeed));
            if (block.style == ChronicleTextStyle.DeeperVoice) cps *= .6f;     // the Being writes slowly, deliberately
            if (block.style == ChronicleTextStyle.Heading || block.style == ChronicleTextStyle.Ending) cps *= .75f;
            block.carry += dt;
            var guard = 0;
            while (!block.complete && guard++ < 4096)
            {
                if (block.hold > 0f)
                {
                    var use = Math.Min(block.hold, block.carry);
                    block.hold -= use; block.carry -= use;
                    if (block.hold > 0f) break;
                }
                var step = 1f / cps;
                if (block.carry < step) break;
                block.carry -= step; block.cursor++;
                block.hold = PauseAfter(block, block.cursor, narrator);
                if (block.cursor >= block.text.Length) { block.complete = true; block.carry = 0f; block.hold = 0f; }
            }
        }

        /// <summary>Seconds the pen rests after writing the character just before <paramref name="index"/>.</summary>
        public static float PauseAfter(ChronicleTextBlock block, int index, ChronicleNarratorStyle narrator)
        {
            var seconds = 0f;
            foreach (var p in block.pauses) if (p.index == index) seconds += p.seconds;
            if (index <= 0 || index > block.text.Length) return seconds * narrator.pauseScale;
            var ch = block.text[index - 1];
            var next = index < block.text.Length ? block.text[index] : ' ';
            if (ch == ' ') seconds += .03f;
            else if (ch == ',' || ch == ';' || ch == ':') seconds += .12f;
            else if ((ch == '.' || ch == '?' || ch == '!') && next == ' ') seconds += .3f + narrator.doubtPause;
            else if (ch == '.' && index >= 3 && block.text[index - 2] == '.' && block.text[index - 3] == '.') seconds += .35f + narrator.doubtPause; // an ellipsis hesitates
            foreach (var start in block.lineStarts) if (start == index && index > 0) { seconds += .1f; break; }
            return seconds * narrator.pauseScale;
        }
    }
}
