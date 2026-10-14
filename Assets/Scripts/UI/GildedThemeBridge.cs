using System;
using System.Collections.Generic;
using GildedFate.Theme;
using UnityEngine;

namespace GildedFate.UI
{
    /// <summary>
    /// The game's visual theme switch. The new look is on by default. Launch with -gfClassicUi to draw the original look instead
    /// (the screenshot tool uses this to capture before and after). Every use of the new look in the existing screens is guarded by NewLook,
    /// so the original drawing code stays intact next to it.
    /// </summary>
    public static class UiTheme
    {
        static int state = -1;
        public static bool NewLook
        {
            get { if (state < 0) state = Array.IndexOf(Environment.GetCommandLineArgs(), "-gfClassicUi") >= 0 ? 0 : 1; return state == 1; }
            set { state = value ? 1 : 0; }
        }
    }

    /// <summary>Draws the engine-free theme through Unity's GUI. Textures are made once from the theme's generated images; text styles are cached.</summary>
    public sealed class UnityThemeCanvas : IThemeCanvas
    {
        readonly Dictionary<ThemeImage, Texture2D> textures = new Dictionary<ThemeImage, Texture2D>();
        readonly Dictionary<(int font, int size, int anchor, bool wrap), GUIStyle> styles = new Dictionary<(int, int, int, bool), GUIStyle>();
        readonly Stack<Vector2> origins = new Stack<Vector2>();
        readonly GUIContent measure = new GUIContent();
        Vector2 origin;
        Font styleHeading, styleLabel, styleBody;

        public Font heading, label, body;
        public bool reduceMotion;

        /// <summary>Animations in the theme run off this clock. It holds still when Reduce Motion is on.</summary>
        public float Time => reduceMotion ? 0f : UnityEngine.Time.unscaledTime;

        Rect R(ThemeRect d) => new Rect(d.x - origin.x, d.y - origin.y, d.w, d.h);
        static Color C(ThemeColor c) => new Color(c.r, c.g, c.b, c.a);
        static Color Mul(Color a, Color b) => new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);

        Texture2D Tex(ThemeImage img)
        {
            if (textures.TryGetValue(img, out var t) && t) return t;
            t = new Texture2D(img.width, img.height, TextureFormat.RGBA32, false)
            {
                name = "GF Theme " + img.id, hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = img.tiles ? TextureWrapMode.Repeat : TextureWrapMode.Clamp
            };
            var flipped = new byte[img.rgba.Length]; var row = img.width * 4;   // Unity's row 0 is the bottom
            for (var y = 0; y < img.height; y++) Buffer.BlockCopy(img.rgba, y * row, flipped, (img.height - 1 - y) * row, row);
            t.LoadRawTextureData(flipped); t.Apply(false, false);
            textures[img] = t; return t;
        }

        public void Fill(ThemeRect dest, ThemeColor color)
        {
            var old = GUI.color; GUI.color = Mul(old, C(color));
            GUI.DrawTexture(R(dest), Texture2D.whiteTexture); GUI.color = old;
        }

        public void Image(ThemeImage image, ThemeRect dest, ThemeColor tint)
        {
            var old = GUI.color; GUI.color = Mul(old, C(tint));
            GUI.DrawTexture(R(dest), Tex(image), ScaleMode.StretchToFill, true); GUI.color = old;
        }

        public void ImageUV(ThemeImage image, ThemeRect dest, ThemeRect uv, ThemeColor tint)
        {
            var old = GUI.color; GUI.color = Mul(old, C(tint));
            GUI.DrawTextureWithTexCoords(R(dest), Tex(image), new Rect(uv.x, 1f - uv.yMax, uv.w, uv.h), true); GUI.color = old;   // theme uv has y down, Unity's has y up
        }

        GUIStyle Style(ThemeFont font, float size, ThemeAnchor anchor, bool wrap)
        {
            if (!ReferenceEquals(heading, styleHeading) || !ReferenceEquals(label, styleLabel) || !ReferenceEquals(body, styleBody)) { styles.Clear(); styleHeading = heading; styleLabel = label; styleBody = body; }
            var px = Mathf.Max(1, Mathf.RoundToInt(size)); var key = ((int)font, px, (int)anchor, wrap);
            if (styles.TryGetValue(key, out var s)) return s;
            var f = font == ThemeFont.Heading ? (heading ? heading : body) : font == ThemeFont.Label ? (label ? label : body) : body;
            s = new GUIStyle(GUI.skin.label) { font = f, fontSize = px, alignment = (TextAnchor)(int)anchor, wordWrap = wrap, richText = false, clipping = TextClipping.Overflow, fontStyle = FontStyle.Normal, padding = new RectOffset(0, 0, 0, 0) };
            s.normal.textColor = Color.white; styles[key] = s; return s;
        }

        public void TextRaw(string text, ThemeRect dest, ThemeFont font, float size, ThemeColor color, ThemeAnchor anchor, bool wrap)
        {
            var old = GUI.contentColor; GUI.contentColor = Mul(old, C(color));
            GUI.Label(R(dest), text, Style(font, size, anchor, wrap)); GUI.contentColor = old;
        }

        public float MeasureText(string text, ThemeFont font, float size) { measure.text = text; return Style(font, size, ThemeAnchor.MiddleLeft, false).CalcSize(measure).x; }

        public void PushClip(ThemeRect clip) { var r = R(clip); GUI.BeginGroup(r); origins.Push(origin); origin = new Vector2(clip.x, clip.y); }
        public void PopClip() { GUI.EndGroup(); origin = origins.Pop(); }
    }

    public sealed partial class GildedMainMenu
    {
        // ---------------- the new look: bridge from the existing drawing code to the theme ----------------
        private UnityThemeCanvas themeCanvas;
        private struct ThemeButtonAnim { public float hover, press; public int frame; }
        private readonly Dictionary<int, ThemeButtonAnim> themeButtons = new();

        private UnityThemeCanvas ThemeCanvas()
        {
            themeCanvas ??= new UnityThemeCanvas();
            themeCanvas.heading = headingFont; themeCanvas.label = labelFont ? labelFont : bodyFont; themeCanvas.body = bodyFont;
            themeCanvas.reduceMotion = profile != null && profile.reduceMotion;
            return themeCanvas;
        }
        private static ThemeRect TR(Rect r) => new ThemeRect(r.x, r.y, r.width, r.height);

        /// <summary>The new button. Same inputs as the original DrawButtonFrame; hover and press ease in and out instead of snapping.</summary>
        private void ThemeButton(Rect rect, bool hover, bool disabled, bool selected)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (!disabled && hover && GUI.enabled) ButtonHoverFeedback(rect);   // the same hover sound as before
            var key = HashCode.Combine(Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y), Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height));
            themeButtons.TryGetValue(key, out var a);
            var reduce = profile != null && profile.reduceMotion;
            var hot = !disabled && hover && GUI.enabled;
            if (a.frame != Time.frameCount)
            {
                var dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
                var pressedNow = hot && !controllerNavigation && PointerHeld;
                a.hover = reduce ? (hot ? 1f : 0f) : Mathf.MoveTowards(a.hover, hot ? 1f : 0f, dt * 9f);
                a.press = reduce ? (pressedNow ? 1f : 0f) : Mathf.MoveTowards(a.press, pressedNow ? 1f : 0f, dt * 20f);
                a.frame = Time.frameCount; themeButtons[key] = a;
                if (Time.frameCount % 600 == 0) { var stale = new List<int>(); foreach (var pair in themeButtons) if (pair.Value.frame < Time.frameCount - 300) stale.Add(pair.Key); foreach (var k in stale) themeButtons.Remove(k); }
            }
            var visual = new ButtonVisual { enabled = !disabled, selected = selected, hover = a.hover, press = a.press, pulse = reduce ? .5f : .5f + .5f * Mathf.Sin(Time.unscaledTime * 3f) };
            ThemeWidgets.Button(ThemeCanvas(), TR(rect), visual);
        }

        /// <summary>The new screen heading, laid out exactly where the original one was.</summary>
        private void ThemeHeading(float w, string title, string sub)
        {
            var top = ShowsPersistentRunHud ? 68f : 30f;
            // The original also left these style sizes behind for later drawing code, so keep doing that.
            titleStyle.fontSize = 38; titleStyle.normal.textColor = new Color(.98f, .92f, .76f); subtitleStyle.fontSize = 17; subtitleStyle.normal.textColor = new Color(.91f, .87f, .77f);
            if (Event.current.type != EventType.Repaint) return;
            ThemeWidgets.TitleAt(ThemeCanvas(), new ThemeRect(w * .16f, top, w * .68f, 59), title, 38, top + 61, Mathf.Min(440f, w * .38f), sub, new ThemeRect(w * .16f, top + 67, w * .68f, 28), 17);
        }

        private void ThemePanel(Rect panel) { if (Event.current.type == EventType.Repaint) ThemeWidgets.Panel(ThemeCanvas(), TR(panel)); }
        private void ThemePlaque(Rect r, float opacity = .985f) { if (Event.current.type == EventType.Repaint) ThemeWidgets.Plaque(ThemeCanvas(), TR(r), opacity); }
        private void ThemeHudStrip(float w) { if (Event.current.type == EventType.Repaint) ThemeWidgets.HudStrip(ThemeCanvas(), new ThemeRect(0, 0, w, 58), profile != null && profile.highContrastUi); }
        private void ThemeRow(Rect r, bool hot) { if (Event.current.type == EventType.Repaint) ThemeWidgets.Row(ThemeCanvas(), TR(r), hot ? 1f : 0f); }
        private void ThemeSlider(Rect track, float fraction, bool hot) { if (Event.current.type == EventType.Repaint) ThemeWidgets.Slider(ThemeCanvas(), TR(track), fraction, hot); }
    }
}
