using System;
using System.Collections.Generic;
using GildedFate.Core;
using GildedFate.Map;
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
        /// <summary>True when an exception was thrown by the theme's own drawing (as opposed to a bug elsewhere), judged by its stack trace.</summary>
        public static bool IsFromTheme(Exception e)
        {
            var trace = e?.StackTrace; if (string.IsNullOrEmpty(trace)) return false;
            return trace.Contains("GildedFate.Theme.") || trace.Contains("UnityThemeCanvas") || trace.Contains("GildedMainMenu.Theme");
        }
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

        public void Line(float x0, float y0, float x1, float y1, float width, ThemeColor color)
        {
            var a = new Vector2(x0 - origin.x, y0 - origin.y); var b = new Vector2(x1 - origin.x, y1 - origin.y); var length = Vector2.Distance(a, b);
            if (length < .01f || width <= 0f) return;
            // Composed in canvas space after the existing UI scale, like the original DrawLine, so it sits right at every resolution.
            var matrix = GUI.matrix; var angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            GUI.matrix = matrix * Matrix4x4.TRS(new Vector3(a.x, a.y, 0), Quaternion.Euler(0, 0, angle), Vector3.one);
            var old = GUI.color; GUI.color = Mul(old, C(color));
            GUI.DrawTexture(new Rect(0, -width * .5f, length, width), Tex(ThemeAssets.LineSoft), ScaleMode.StretchToFill, true);
            GUI.color = old; GUI.matrix = matrix;
        }

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
        private void ThemeHudWell(Rect r) { if (Event.current.type == EventType.Repaint) ThemeWidgets.Well(ThemeCanvas(), TR(r), .9f); }
        private void ThemeHudStrip(float w) { if (Event.current.type == EventType.Repaint) ThemeWidgets.HudStrip(ThemeCanvas(), new ThemeRect(0, 0, w, 58), profile != null && profile.highContrastUi); }
        private void ThemeRow(Rect r, bool hot) { if (Event.current.type == EventType.Repaint) ThemeWidgets.Row(ThemeCanvas(), TR(r), hot ? 1f : 0f); }
        // ---- choice panels (sanctuary and similar) ----
        /// <summary>A choice panel: a bevelled bronze frame over dark glass that warms to gold, with a soft halo, when the pointer or focus is on it.</summary>
        private void ThemeChoicePanel(Rect r, bool hot)
        {
            if (Event.current.type != EventType.Repaint) return;
            var keep = GUI.color; GUI.color = Color.white; var canvas = ThemeCanvas();
            if (hot) canvas.Image(ThemeAssets.Glow, new ThemeRect(r.x - r.width * .12f, r.y - r.height * .18f, r.width * 1.24f, r.height * 1.36f), ThemePalette.Ember.WithAlpha(.20f));
            ThemeWidgets.Panel(canvas, TR(r), hot ? FrameKind.Gold : FrameKind.Bronze);
            if (hot) canvas.Image(ThemeAssets.ButtonGlassHot, new ThemeRect(r.x + 10, r.y + 10, r.width - 20, r.height - 20), new ThemeColor(1, 1, 1, .55f));
            canvas.Image(ThemeAssets.RuleFade, new ThemeRect(r.x + 28, r.y + 11, r.width - 56, 2), ThemePalette.GoldBright.WithAlpha(hot ? .85f : .45f));
            GUI.color = keep;
        }

        // ---- fate shard sockets ----
        /// <summary>A bevelled diamond socket for one Fate Shard slot. tint shades the metal toward the shard's accent when it is active or fractured.</summary>
        private void ThemeShardSocket(Rect r, Color tint, bool filled, float pulse)
        {
            if (Event.current.type != EventType.Repaint) return;
            var keep = GUI.color; GUI.color = Color.white; var canvas = ThemeCanvas();
            var d = new ThemeRect(r.x - 2, r.y - 5, r.width + 4, r.height + 10);
            if (filled) ThemeWidgets.Glow(canvas, r.center.x, r.center.y, r.width * .9f, new ThemeColor(tint.r, tint.g, tint.b, .16f + .2f * pulse));
            canvas.Image(ThemeAssets.DiamondPlate, d, new ThemeColor(tint.r, tint.g, tint.b, 1f));
            GUI.color = keep;
        }

        // ---- shops ----
        /// <summary>One of the broker's threads, from his hand to an item: faint until the item is under the pointer, then a bright thread with a spark running along it.</summary>
        private void ThemeShopThread(Vector2 source, Vector2 target, bool hot)
        {
            if (Event.current.type != EventType.Repaint) return;
            var keep = GUI.color; GUI.color = Color.white; var canvas = ThemeCanvas();
            if (hot) ThemeMap.Thread(canvas, source.x, source.y, target.x, target.y, ThreadKind.Available, 1f, MapPolishMotion ? 2 : 0);
            else canvas.Line(source.x, source.y, target.x, target.y, 3f, new ThemeColor(.9f, .65f, .28f, .17f));
            GUI.color = keep;
        }
        /// <summary>A medallion under a shop item that has no frame of its own (a relic, a shard, a service), lit gold when the pointer is on it.</summary>
        private void ThemeShopSocket(Rect r, bool hot, bool dim)
        {
            if (Event.current.type != EventType.Repaint) return;
            var keep = GUI.color; GUI.color = Color.white; var canvas = ThemeCanvas();
            var d = Mathf.Min(r.width, r.height) * (hot ? 1.12f : 1.04f);
            ThemeWidgets.Glow(canvas, r.center.x, r.center.y, d * .72f, ThemePalette.Ember.WithAlpha(hot ? .34f : .13f));
            canvas.Image(ThemeAssets.Medallion, new ThemeRect(r.center.x - d * .5f, r.center.y - d * .5f, d, d), dim ? new ThemeColor(.6f, .6f, .62f, 1f) : ThemeColor.White);
            if (hot) { var ring = d * 1.16f; canvas.Image(ThemeAssets.Ring, new ThemeRect(r.center.x - ring * .5f, r.center.y - ring * .5f, ring, ring), ThemePalette.GoldBright.WithAlpha(.8f)); }
            GUI.color = keep;
        }
        private void ThemePricePlate(Rect r) { if (Event.current.type == EventType.Repaint) ThemeWidgets.Plate(ThemeCanvas(), TR(new Rect(r.x - 10, r.y - 1, r.width + 20, r.height + 2)), .9f); }

        // ---- character select ----
        /// <summary>The hero's details panel: a bevelled frame in the hero's own metal (gold, violet or jade) with a lit rule across the top in their accent colour.</summary>
        private void ThemeHeroPanel(Rect r, HeroId hero, Color accent)
        {
            if (Event.current.type != EventType.Repaint) return;
            var keep = GUI.color; GUI.color = Color.white; var canvas = ThemeCanvas();
            ThemeWidgets.Panel(canvas, TR(r), hero == HeroId.Hexer ? FrameKind.Violet : hero == HeroId.Reaper ? FrameKind.Jade : FrameKind.Gold);
            canvas.Image(ThemeAssets.RuleFade, new ThemeRect(r.x + 34, r.y + 7, r.width - 68, 2), new ThemeColor(accent.r, accent.g, accent.b, .95f));
            GUI.color = keep;
        }

        /// <summary>The event and Fateweave screens keep their own authored look. Shared chrome (the top bar, tooltips, buttons) is themed everywhere; their panels are left alone.</summary>
        private bool ThemeKeepsClassicScreen => screen is ScreenMode.Event or ScreenMode.EventSelection or ScreenMode.EventResult or ScreenMode.Fateweave or ScreenMode.FateweaveCard;

        /// <summary>
        /// Light shafts and a few drifting embers over a screen's backdrop, so reward, treasure, shop, sanctuary, binding and run-result screens are not still pictures.
        /// Only on those screens: never on the Chronicle, the event and Fateweave screens, or the collection and records pages.
        /// </summary>
        private void ThemeBackdropLife(float w, float h)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (!(screen is ScreenMode.Reward or ScreenMode.RelicReward or ScreenMode.Treasure or ScreenMode.Merchant or ScreenMode.Sanctuary or ScreenMode.BindingSelect or ScreenMode.RunResult)) return;
            var keep = GUI.color; GUI.color = Color.white; var canvas = ThemeCanvas(); var area = new ThemeRect(0, 58, w, h - 58);
            if (ThemeMotion > 0) ThemeMap.LightShafts(canvas, area, .8f);
            ThemeMap.Embers(canvas, area, ThemeMotion == 2 ? 24 : ThemeMotion == 1 ? 8 : 12, 0f);
            GUI.color = keep;
        }

        // ---- combat numbers ----
        /// <summary>
        /// A floating damage, healing or Block number: it pops in a little large and settles, has a dark outline so it reads over any art, and a light-to-deep gradient.
        /// t is its age in seconds; alpha its fade. Reduce Motion skips the pop.
        /// </summary>
        private void ThemeCombatNumber(Vector2 p, string text, Color color, float alpha, float t, int size)
        {
            if (Event.current.type != EventType.Repaint) return;
            var keep = GUI.color; GUI.color = Color.white; var canvas = ThemeCanvas();
            var pop = profile != null && profile.reduceMotion ? 1f : Mathf.Lerp(1.5f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / .16f)));
            var top = new ThemeColor(Mathf.Lerp(color.r, 1f, .35f), Mathf.Lerp(color.g, 1f, .35f), Mathf.Lerp(color.b, 1f, .35f), alpha);
            var style = ThemeText.Of(ThemeFont.Label, size * pop, top, ThemeAnchor.MiddleCenter);
            style.gradient = true; style.bottomColor = new ThemeColor(color.r * .78f, color.g * .78f, color.b * .78f, alpha);
            style.outline = 2f; style.outlineColor = new ThemeColor(.02f, .01f, .01f, .88f * alpha); style.shadowY = 3f; style.shadowAlpha = .55f * alpha;
            ThemeDraw.Text(canvas, text, new ThemeRect(p.x - 170, p.y - 30, 340, 54), style);
            GUI.color = keep;
        }

        /// <summary>A soft dark plate centred on a point, behind small text that sits over busy art.</summary>
        private void ThemeTextPlate(float cx, float cy, float width, float height)
        {
            if (Event.current.type != EventType.Repaint) return;
            var keep = GUI.color; GUI.color = Color.white; ThemeWidgets.Plate(ThemeCanvas(), new ThemeRect(cx - width * .5f, cy - height * .5f, width, height), .85f); GUI.color = keep;
        }

        // ---- combat ----
        /// <summary>A bevelled rim in the colour of the flat outline it replaces. The flat outline ignored the caller's GUI.color, so this does too.</summary>
        private void ThemeOutline(Rect r, Color color, int width)
        {
            if (Event.current.type != EventType.Repaint) return;
            var keep = GUI.color; GUI.color = Color.white;
            ThemeWidgets.Outline(ThemeCanvas(), TR(r), new ThemeColor(color.r, color.g, color.b, color.a), width);
            GUI.color = keep;
        }
        /// <summary>A glossy health (or, while guarded, Block-blue) bar with the orange slice just lost. Same inputs as FillBeveledHealthBar.</summary>
        private void ThemeHealthBar(Rect r, float ratio, float trail, bool guarded)
        {
            if (Event.current.type != EventType.Repaint) return;
            var keep = GUI.color; GUI.color = Color.white;
            ThemeWidgets.Bar(ThemeCanvas(), TR(r), guarded ? BarKind.Block : BarKind.Health, ratio, trail > ratio ? trail : 0f, !guarded && ratio > 0f && ratio < .25f && !(profile != null && profile.reduceFlashing));
            GUI.color = keep;
        }
        private void ThemeBlockBadge(Rect r, float pulse)
        {
            if (Event.current.type != EventType.Repaint) return;
            var keep = GUI.color; GUI.color = Color.white; ThemeWidgets.BlockBadge(ThemeCanvas(), TR(r), pulse); GUI.color = keep;
        }
        private void ThemeEndTurnPlate(Rect shown, bool enabled, float hover, float breathe)
        {
            if (Event.current.type != EventType.Repaint) return;
            var keep = GUI.color; GUI.color = Color.white; ThemeWidgets.EndTurnPlate(ThemeCanvas(), TR(shown), enabled, hover, breathe, !ShowPadGlyphs); GUI.color = keep;
        }

        // ---- act map ----
        /// <summary>One route thread, clipped to the map's viewport. Same inputs as the original DrawFateThread (a and b in canvas space).</summary>
        private void ThemeThread(Vector2 a, Vector2 b, Rect clip, bool braided, bool available, bool hot, float progress)
        {
            if (Event.current.type != EventType.Repaint) return;
            var kind = braided ? ThreadKind.Traveled : available ? ThreadKind.Available : hot ? ThreadKind.Reachable : ThreadKind.Future;
            var canvas = ThemeCanvas();
            canvas.PushClip(new ThemeRect(clip.x + 3, clip.y + 3, clip.width - 6, clip.height - 6));
            try { ThemeMap.Thread(canvas, a.x, a.y, b.x, b.y, kind, progress, MapPolishMotion ? (MapPolishLowVfx ? 1 : 3) : 0); }
            finally { canvas.PopClip(); }
        }
        /// <summary>0 = Reduce Motion (everything holds still), 1 = reduced effects (fewer particles and rays), 2 = full.</summary>
        private int ThemeMotion => profile != null && profile.reduceMotion ? 0 : profile != null && profile.reducedVfx ? 1 : 2;
        private struct ThemeHoverAnim { public float value; public int frame; }
        private readonly Dictionary<int, ThemeHoverAnim> themeNodeHover = new();

        /// <summary>How far the pointer has lit a room, 0..1. Eases in and out; snaps when Reduce Motion is on.</summary>
        private float ThemeNodeHover(MapNode node, bool hot)
        {
            var key = node.floor * 16 + node.lane; themeNodeHover.TryGetValue(key, out var a);
            if (a.frame != Time.frameCount)
            {
                a.value = profile != null && profile.reduceMotion ? (hot ? 1f : 0f) : Mathf.MoveTowards(a.value, hot ? 1f : 0f, Mathf.Min(Time.unscaledDeltaTime, .05f) * 8f);
                a.frame = Time.frameCount; themeNodeHover[key] = a;
            }
            return a.value;
        }
        private static NodeState ThemeNodeState(MapNode node, bool active, bool current) => current ? NodeState.Current : active ? NodeState.Available : node.complete ? NodeState.Cleared : NodeState.Locked;

        /// <summary>Behind a room's emblem: shadow, the room's colour, the medallion. The emblem is the game's own art, drawn by the caller after this.</summary>
        private void ThemeNodeUnder(MapNode node, Rect r, bool active, bool hot, bool current)
        {
            if (Event.current.type != EventType.Repaint) return;
            var keep = GUI.color; GUI.color = Color.white;   // the emblem is tinted for cleared rooms; the light around it is not
            ThemeMap.NodeUnder(ThemeCanvas(), r.center.x, r.center.y, r.width, (NodeTone)(int)node.kind, ThemeNodeState(node, active, current), ThemeNodeHover(node, hot), node.floor * 7 + node.lane, ThemeMotion);
            GUI.color = keep;
        }
        /// <summary>Over a room's emblem: ring, turning ticks, ripples, the beacon of the current room, the glint on cleared ones.</summary>
        private void ThemeNodeOver(MapNode node, Rect r, bool active, bool hot, bool current)
        {
            if (Event.current.type != EventType.Repaint) return;
            var keep = GUI.color; GUI.color = Color.white;
            ThemeMap.NodeOver(ThemeCanvas(), r.center.x, r.center.y, r.width, (NodeTone)(int)node.kind, ThemeNodeState(node, active, current), ThemeNodeHover(node, hot), node.floor * 7 + node.lane, ThemeMotion);
            GUI.color = keep;
        }
        /// <summary>Light shafts and drifting embers over the map's backdrop.</summary>
        private void ThemeMapAmbience(Rect area, float scroll)
        {
            if (Event.current.type != EventType.Repaint) return;
            var canvas = ThemeCanvas();
            ThemeMap.LightShafts(canvas, TR(area), ThemeMotion == 0 ? .8f : 1f);
            ThemeMap.Embers(canvas, TR(area), ThemeMotion == 2 ? 36 : ThemeMotion == 1 ? 14 : 20, scroll);
        }
        private void ThemeMapHeader(float cx, float cy, string text, bool ready) { if (Event.current.type == EventType.Repaint) ThemeMap.Header(ThemeCanvas(), cx, cy, text, ready); }
        private void ThemeBossCrown(Rect crown) { if (Event.current.type == EventType.Repaint) ThemeMap.BossCrown(ThemeCanvas(), TR(crown), ThemeMotion); }
        private void ThemeLabelPlate(Rect r) { if (Event.current.type == EventType.Repaint) ThemeMap.LabelPlate(ThemeCanvas(), TR(r)); }
        /// <summary>The backdrop drifts slightly against the scroll, so the vault has depth. Drawn only in the new look.</summary>
        private void ThemeMapBackdrop(Texture2D art, float w, float h)
        {
            if (Event.current.type != EventType.Repaint || !art) return;
            var scrollable = Mathf.Max(1f, MapContentHeight - MapViewport(w, h).height); var shift = (profile != null && profile.reduceMotion ? 0f : Mathf.Clamp01(mapScroll / scrollable)) * 36f;
            var old = GUI.color; GUI.color = new Color(.62f, .68f, .78f, .62f);
            GUI.BeginGroup(new Rect(0, 58, w, h - 58));
            GUI.DrawTexture(new Rect(0, -18 - shift, w, h - 58 + 72), art, ScaleMode.ScaleAndCrop);
            GUI.EndGroup(); GUI.color = old;
        }

        private void ThemeMapLabel(float cx, float cy, string text, bool lit) { if (Event.current.type == EventType.Repaint) ThemeMap.RoomLabel(ThemeCanvas(), cx, cy, text, lit); }
        /// <summary>A soft dark vignette over the map's backdrop, so the middle glows and the edges recede.</summary>
        private void ThemeMapAtmosphere(Rect area) { if (Event.current.type == EventType.Repaint) ThemeCanvas().Image(ThemeAssets.Vignette, TR(area), new ThemeColor(1, 1, 1, .85f)); }

        private void ThemeSlider(Rect track, float fraction, bool hot) { if (Event.current.type == EventType.Repaint) ThemeWidgets.Slider(ThemeCanvas(), TR(track), fraction, hot); }
    }
}
