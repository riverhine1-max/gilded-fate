using System;

namespace GildedFate.Theme
{
    public enum FrameKind { Gold, Bronze, Steel, Crimson, Azure, Violet, Jade }
    public enum BarKind { Health, Block, Energy, Arcane, Growth }

    /// <summary>Everything a button needs to be drawn. The game animates hover/press/pulse smoothly; the preview just sets them.</summary>
    public struct ButtonVisual
    {
        public float hover, press;          // 0..1, eased by the caller
        public float pulse;                 // 0..1 slow breathing while keyboard/controller focused
        public bool enabled, selected;
        public static ButtonVisual Idle => new ButtonVisual { enabled = true };
    }

    /// <summary>The shared widgets. Each is a pure function of its inputs and the canvas clock, so it can be previewed as a still and tested.</summary>
    public static class ThemeWidgets
    {
        static ThemeImage Frame(FrameKind k)
        {
            switch (k)
            {
                case FrameKind.Bronze: return ThemeAssets.FrameBronze; case FrameKind.Steel: return ThemeAssets.FrameSteel; case FrameKind.Crimson: return ThemeAssets.FrameCrimson;
                case FrameKind.Azure: return ThemeAssets.FrameAzure; case FrameKind.Violet: return ThemeAssets.FrameViolet; case FrameKind.Jade: return ThemeAssets.FrameJade; default: return ThemeAssets.FrameGold;
            }
        }
        static ThemeColor Black(float a) => new ThemeColor(0, 0, 0, a);
        static ThemeColor White(float a) => new ThemeColor(1, 1, 1, a);
        static float Frac(float v) => v - (float)Math.Floor(v);
        static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;

        // ---------------------------------------------------------------- light and depth
        /// <summary>A soft warm (or any color) light centred on a point.</summary>
        public static void Glow(IThemeCanvas c, float cx, float cy, float radius, ThemeColor color) =>
            c.Image(ThemeAssets.Glow, new ThemeRect(cx - radius, cy - radius, radius * 2, radius * 2), color);

        /// <summary>A soft drop shadow beneath a rectangle.</summary>
        public static void Shadow(IThemeCanvas c, ThemeRect r, float blur, float offsetY, float alpha) =>
            ThemeDraw.Slice9(c, ThemeAssets.Shadow, r.Inflate(blur).Offset(0, offsetY), blur, Black(alpha));

        // ---------------------------------------------------------------- panels
        /// <summary>An obsidian panel in a bevelled metal frame, with depth: drop shadow, grain, a lit top edge and an inner vignette.</summary>
        public static void Panel(IThemeCanvas c, ThemeRect r, FrameKind kind = FrameKind.Gold, float depth = 1f, float opacity = 1f, float slice = 24f)
        {
            if (depth > 0) Shadow(c, r, 16 * depth, 7 * depth, .72f * opacity);
            var inner = r.Inflate(-2);
            c.Image(ThemeAssets.PanelGlass, inner, White(opacity));
            ThemeDraw.Tile(c, ThemeAssets.Grain, inner, 128, White(.035f * opacity));
            c.Image(ThemeAssets.TopSheen, new ThemeRect(inner.x, inner.y, inner.w, Math.Min(inner.h * .5f, 56)), White(.55f * opacity));
            c.Image(ThemeAssets.Vignette, inner, Black(.30f * opacity));
            ThemeDraw.Slice9(c, Frame(kind), r, Math.Min(slice, Math.Min(r.w, r.h) * .45f), White(opacity));
        }

        /// <summary>A recessed well, for lists and inputs: darker than a panel and framed by a slim rim.</summary>
        public static void Well(IThemeCanvas c, ThemeRect r, float opacity = 1f)
        {
            c.Image(ThemeAssets.BarTrough, r.Inflate(-1), White(opacity));
            c.Fill(r.Inflate(-1), Black(.35f * opacity));
            ThemeDraw.Slice9(c, ThemeAssets.RimBronze, r, 6, White(.9f * opacity));
        }

        /// <summary>A dark band that fades out at both ends. Put it behind small text laid over busy art so it stays readable.</summary>
        public static void Plate(IThemeCanvas c, ThemeRect r, float alpha = 1f) => c.Image(ThemeAssets.PlateFade, r, White(alpha));

        // ---------------------------------------------------------------- buttons
        public static void Button(IThemeCanvas c, ThemeRect r, ButtonVisual v)
        {
            float hover = v.enabled ? Clamp01(v.hover) : 0, press = v.enabled ? Clamp01(v.press) : 0, sel = v.enabled && v.selected ? 1f : 0f;
            var lit = Math.Max(hover, sel);
            var tint = v.enabled ? ThemeColor.White : new ThemeColor(.62f, .62f, .66f, 1);

            Shadow(c, r, 10, 5 - 3 * press, (.62f - .3f * press) * (v.enabled ? 1f : .6f));
            if (lit > .01f) c.Image(ThemeAssets.Glow, new ThemeRect(r.x - r.w * .2f, r.y - r.h * .75f, r.w * 1.4f, r.h * 2.5f), ThemePalette.Ember.WithAlpha(.20f * lit + .07f * v.pulse * sel));

            var inner = r.Inflate(-3);
            c.Image(ThemeAssets.ButtonGlass, inner, tint);
            if (lit > .01f) c.Image(ThemeAssets.ButtonGlassHot, inner, White(lit));
            if (press > .01f) c.Image(ThemeAssets.ButtonGlassPressed, inner, White(press));
            c.Image(ThemeAssets.TopSheen, new ThemeRect(inner.x, inner.y, inner.w, inner.h * .55f), White((1 - press * .6f) * (v.enabled ? 1f : .45f)));
            ThemeDraw.Tile(c, ThemeAssets.Grain, inner, 128, White(.04f));

            if (hover > .01f && press < .5f)
            {
                var bw = r.w * .26f; var x = r.x - bw + (r.w + bw * 2) * Frac(c.Time * .55f);
                c.PushClip(inner); c.Image(ThemeAssets.Sheen, new ThemeRect(x, inner.y, bw, inner.h), White(.15f * hover)); c.PopClip();
            }

            var slice = Math.Min(24f, r.h * .42f);
            if (v.enabled)
            {
                ThemeDraw.Slice9(c, ThemeAssets.FrameBronze, r, slice, White(1f - .3f * lit));
                if (lit > .01f) ThemeDraw.Slice9(c, ThemeAssets.FrameGold, r, slice, White(lit * (1 - press * .25f)));
            }
            else ThemeDraw.Slice9(c, ThemeAssets.FrameSteel, r, slice, White(.42f));

            if (sel > 0 && v.pulse > 0) ThemeDraw.Slice9(c, ThemeAssets.RimGold, r.Inflate(3), 6, ThemePalette.GoldBright.WithAlpha(.18f + .34f * v.pulse));
        }

        // ---------------------------------------------------------------- bars
        static ThemeImage[] barFill;
        static ThemeImage BarFillImage(BarKind k)
        {
            if (barFill == null)
            {
                ThemeImages.Stop S(float t, uint hex) => new ThemeImages.Stop(t, ThemeColor.Hex(hex));
                barFill = new[]
                {
                    ThemeImages.Gradient("bar.health", 32, S(0, 0xFF6A58), S(.5f, 0xD3302A), S(1, 0x7F1512)),
                    ThemeImages.Gradient("bar.block", 32, S(0, 0x8FC4FF), S(.5f, 0x3F8FE0), S(1, 0x1B4C95)),
                    ThemeImages.Gradient("bar.energy", 32, S(0, 0xFFE59A), S(.5f, 0xE6B85C), S(1, 0x8F5C14)),
                    ThemeImages.Gradient("bar.arcane", 32, S(0, 0xCDB0FF), S(.5f, 0x8A5CFF), S(1, 0x43238A)),
                    ThemeImages.Gradient("bar.growth", 32, S(0, 0x8CF0CE), S(.5f, 0x2FBF9A), S(1, 0x12684F)),
                };
            }
            return barFill[(int)k];
        }
        static ThemeColor BarLight(BarKind k) => k == BarKind.Health ? ThemePalette.CrimsonBright : k == BarKind.Block ? ThemePalette.AzureBright : k == BarKind.Energy ? ThemePalette.GoldBright : k == BarKind.Arcane ? ThemePalette.VioletBright : ThemePalette.JadeBright;

        /// <summary>A glossy bar. ghost (>= fill) shows the part that was just lost as a pale trail; low makes a health bar pulse.</summary>
        public static void Bar(IThemeCanvas c, ThemeRect r, BarKind kind, float fill, float ghost = 0f, bool low = false)
        {
            fill = Clamp01(fill); ghost = Clamp01(ghost);
            c.Image(ThemeAssets.BarTrough, r.Inflate(-1), ThemeColor.White);
            var inner = r.Inflate(-2.5f);
            if (ghost > fill + .003f) c.Fill(new ThemeRect(inner.x, inner.y, inner.w * ghost, inner.h), new ThemeColor(1f, .52f, .14f, .94f));   // the slice just lost, in the orange the game already uses for it
            if (fill > 0)
            {
                var f = new ThemeRect(inner.x, inner.y, inner.w * fill, inner.h);
                c.Image(BarFillImage(kind), f, ThemeColor.White);
                if (low) c.Fill(f, ThemePalette.CrimsonBright.WithAlpha(.10f + .12f * (float)(.5 + .5 * Math.Sin(c.Time * 5))));
                c.Image(ThemeAssets.BarGloss, f, ThemeColor.White);
                // Faint marks every tenth give the bar a scale; a slow band of light passes over the fill (it holds still when the clock does).
                if (inner.w > 120) for (var i = 1; i < 10; i++) { var tx = inner.x + inner.w * i / 10f; if (tx < f.xMax - 2) c.Fill(new ThemeRect(tx, inner.y + 2, 1, inner.h - 4), Black(.20f)); }
                if (f.w > 40) { var bw = Math.Max(30f, f.w * .22f); var sx = f.x - bw + (f.w + bw * 2) * Frac(c.Time * .24f + (int)kind * .17f); c.PushClip(f); c.Image(ThemeAssets.Sheen, new ThemeRect(sx, f.y, bw, f.h), White(.20f)); c.PopClip(); }
                var gr = inner.h * 1.5f; c.Image(ThemeAssets.GlowTight, new ThemeRect(f.xMax - gr, inner.cy - gr, gr * 2, gr * 2), BarLight(kind).WithAlpha(.38f));
            }
            ThemeDraw.Slice9(c, ThemeAssets.RimBronze, r, 6, ThemeColor.White);
        }

        // ---------------------------------------------------------------- combat
        /// <summary>
        /// The End Turn plate: a bevelled gold hexagon over dark glass. It lights on hover, breathes a warm glow when there is nothing left to play
        /// (breathe, 0..1), and dims when it is not your turn. The caller supplies the lift and dip as a moved rectangle, and draws the text.
        /// </summary>
        public static void EndTurnPlate(IThemeCanvas c, ThemeRect r, bool enabled, float hover, float breathe, bool leftStud = true)
        {
            hover = enabled ? Clamp01(hover) : 0; breathe = enabled ? Clamp01(breathe) : 0;
            var glow = enabled ? .12f + .16f * hover + .34f * breathe : 0f;
            if (glow > 0) c.Image(ThemeAssets.Glow, new ThemeRect(r.x - 40, r.y - r.h * .95f, r.w + 80, r.h * 2.9f), ThemePalette.Ember.WithAlpha(glow));
            c.Image(ThemeAssets.Glow, new ThemeRect(r.x - 8, r.y + r.h * .12f, r.w + 16, r.h * 1.1f), Black(.60f));
            c.Image(ThemeAssets.HexPlate, r, enabled ? ThemeColor.Lerp(ThemeColor.White, new ThemeColor(1f, .93f, .78f, 1f), hover) : new ThemeColor(.46f, .46f, .50f, 1f));
            var inner = new ThemeRect(r.x + r.w * .075f, r.y + 10, r.w * .85f, r.h - 20);
            if (enabled) c.Image(ThemeAssets.ButtonGlassHot, inner, White(.50f * hover + .62f * breathe + .16f));
            c.Image(ThemeAssets.TopSheen, new ThemeRect(inner.x, inner.y, inner.w, inner.h * .5f), White(enabled ? .55f : .2f));
            if (hover > .01f) { var bw = r.w * .2f; var x = inner.x - bw + (inner.w + bw * 2) * Frac(c.Time * .55f); c.PushClip(inner); c.Image(ThemeAssets.Sheen, new ThemeRect(x, inner.y, bw, inner.h), White(.18f * hover)); c.PopClip(); }
            var stud = enabled ? ThemeColor.White : new ThemeColor(.55f, .55f, .58f, .8f);
            if (leftStud) c.Image(ThemeAssets.DiamondGold, new ThemeRect(r.x + r.w * .045f - 6, r.cy - 6, 12, 12), stud);
            c.Image(ThemeAssets.DiamondGold, new ThemeRect(r.xMax - r.w * .045f - 6, r.cy - 6, 12, 12), stud);
        }

        /// <summary>The Block badge beside a health bar: an azure-lit medallion. The caller draws the number over it.</summary>
        public static void BlockBadge(IThemeCanvas c, ThemeRect r, float pulse)
        {
            pulse = Clamp01(pulse);
            Glow(c, r.cx, r.cy, r.w * .95f, ThemePalette.AzureBright.WithAlpha(.28f + .22f * pulse));
            c.Image(ThemeAssets.Medallion, r, new ThemeColor(.62f, .84f, 1f, 1f));
            var d = r.w * 1.14f; c.Image(ThemeAssets.Ring, new ThemeRect(r.cx - d * .5f, r.cy - d * .5f, d, d), ThemePalette.AzureBright.WithAlpha(.55f + .35f * pulse));
        }

        /// <summary>A bevelled rim in a given colour, drawn inside the rectangle. Replaces a flat outline around a panel: the same colour, but cut like metal.</summary>
        public static void Outline(IThemeCanvas c, ThemeRect r, ThemeColor color, float width)
        {
            var slice = Math.Max(4f, Math.Min(9f, 3f + width * 1.5f));
            ThemeDraw.Slice9(c, ThemeAssets.RimNeutral, r, slice, color);
        }

        // ---------------------------------------------------------------- headings
        /// <summary>A rule that fades at both ends, with a gold diamond at its centre.</summary>
        public static void Divider(IThemeCanvas c, float cx, float y, float width)
        {
            c.Image(ThemeAssets.RuleFade, new ThemeRect(cx - width * .5f, y - 1, width, 2), ThemeColor.White);
            c.Image(ThemeAssets.DiamondGold, new ThemeRect(cx - 9, y - 9, 18, 18), ThemeColor.White);
        }

        /// <summary>A screen title with explicit layout (so existing screens keep their exact spacing): metal-gradient lettering with outline and shadow
        /// over a faint golden glow, an ornamental rule, and a subtitle.</summary>
        public static void TitleAt(IThemeCanvas c, ThemeRect titleRect, string title, float size, float ruleY, float ruleWidth, string subtitle, ThemeRect subtitleRect, float subtitleSize)
        {
            c.Image(ThemeAssets.Glow, new ThemeRect(titleRect.cx - titleRect.w * .5f, titleRect.y - size * .3f, titleRect.w, size * 2.1f), ThemePalette.Ember.WithAlpha(.09f));
            var style = ThemeText.Of(ThemeFont.Heading, size, ThemePalette.GoldBright, ThemeAnchor.MiddleCenter);
            style.gradient = true; style.bottomColor = ThemePalette.GoldDeep; style.outline = 1.2f; style.outlineColor = new ThemeColor(.05f, .03f, .01f, .9f); style.shadowY = 4; style.shadowAlpha = .9f;
            ThemeDraw.Text(c, title, titleRect, style);
            Divider(c, titleRect.cx, ruleY, ruleWidth);
            if (!string.IsNullOrEmpty(subtitle))
            {
                var sub = ThemeText.Of(ThemeFont.Body, subtitleSize, new ThemeColor(.93f, .89f, .79f, 1f), ThemeAnchor.MiddleCenter); sub.shadowY = 2; sub.shadowAlpha = .85f;
                ThemeDraw.Text(c, subtitle, subtitleRect, sub);
            }
        }

        public static void Title(IThemeCanvas c, ThemeRect r, string title, string subtitle = null, float size = 44f) =>
            TitleAt(c, new ThemeRect(r.x, r.y, r.w, size * 1.3f), title, size, r.y + size * 1.42f, Math.Min(r.w, 600), subtitle, new ThemeRect(r.x, r.y + size * 1.52f, r.w, size * .6f), Math.Max(13f, size * .36f));

        // ---------------------------------------------------------------- strips, rows, sliders
        /// <summary>The run bar across the top of the screen: lit glass, a bevelled gold edge underneath, and a soft shadow cast onto the scene.</summary>
        public static void HudStrip(IThemeCanvas c, ThemeRect r, bool highContrast = false)
        {
            c.Image(ThemeAssets.ShadeDown, new ThemeRect(r.x, r.yMax, r.w, 18), ThemeColor.White);
            c.Image(ThemeAssets.HudGlass, r, highContrast ? new ThemeColor(.6f, .6f, .6f, 1f) : ThemeColor.White);
            ThemeDraw.Tile(c, ThemeAssets.Grain, r, 128, White(.03f));
            c.Fill(new ThemeRect(r.x, r.y, r.w, 1), White(.07f));
            c.Fill(new ThemeRect(r.x, r.yMax - 4, r.w, 1), ThemePalette.GoldBright.WithAlpha(.30f));
            c.Fill(new ThemeRect(r.x, r.yMax - 3, r.w, 2), ThemePalette.GoldDeep.WithAlpha(.96f));
            c.Fill(new ThemeRect(r.x, r.yMax - 1, r.w, 1), Black(.8f));
        }

        /// <summary>A compact plaque for tooltips and small labels: lit glass in a slim rim, with a soft shadow.</summary>
        public static void Plaque(IThemeCanvas c, ThemeRect r, float opacity = 1f, FrameKind kind = FrameKind.Bronze)
        {
            Shadow(c, r, 12, 5, .6f * opacity);
            var inner = r.Inflate(-1.5f);
            c.Image(ThemeAssets.PanelGlass, inner, White(opacity));
            ThemeDraw.Tile(c, ThemeAssets.Grain, inner, 128, White(.035f * opacity));
            c.Image(ThemeAssets.TopSheen, new ThemeRect(inner.x, inner.y, inner.w, Math.Min(inner.h * .5f, 40)), White(.5f * opacity));
            ThemeDraw.Slice9(c, kind == FrameKind.Gold ? ThemeAssets.RimGold : kind == FrameKind.Steel ? ThemeAssets.RimSteel : ThemeAssets.RimBronze, r, 6, White(opacity));
        }

        /// <summary>One row of a list (a setting, a record). hot is 0..1: how far the pointer or focus has lit it.</summary>
        public static void Row(IThemeCanvas c, ThemeRect r, float hot)
        {
            hot = Clamp01(hot);
            var inner = r.Inflate(-1.5f);
            c.Image(ThemeAssets.RowGlass, inner, ThemeColor.White);
            if (hot > .01f)
            {
                c.Image(ThemeAssets.ButtonGlassHot, inner, White(.9f * hot));
                c.Image(ThemeAssets.Glow, new ThemeRect(r.x - r.w * .05f, r.y - r.h * .8f, r.w * 1.1f, r.h * 2.6f), ThemePalette.Ember.WithAlpha(.10f * hot));
            }
            ThemeDraw.Tile(c, ThemeAssets.Grain, inner, 128, White(.03f));
            ThemeDraw.Slice9(c, ThemeAssets.RimBronze, r, 6, White(1f - .4f * hot));
            if (hot > .01f)
            {
                ThemeDraw.Slice9(c, ThemeAssets.RimGold, r, 6, White(hot));
                c.Fill(new ThemeRect(r.x + 3, r.y + 8, 3, r.h - 16), ThemePalette.GoldBright.WithAlpha(.9f * hot));
            }
        }

        /// <summary>A slider: a glossy gold track and a faceted gold knob. The caller still handles the pointer.</summary>
        public static void Slider(IThemeCanvas c, ThemeRect track, float fraction, bool hot)
        {
            fraction = Clamp01(fraction);
            var bar = new ThemeRect(track.x, track.cy - 5, track.w, 10);
            Bar(c, bar, BarKind.Energy, fraction);
            var kx = track.x + track.w * fraction;
            if (hot) Glow(c, kx, track.cy, 20, ThemePalette.Ember.WithAlpha(.35f));
            c.Image(ThemeAssets.DiamondGold, new ThemeRect(kx - 12, track.cy - 12, 24, 24), ThemeColor.White);
        }

        // ---------------------------------------------------------------- tooltips
        /// <summary>A compact tooltip: bronze-framed glass, a gold heading and body text.</summary>
        public static void Tooltip(IThemeCanvas c, ThemeRect r, string heading, string body, FrameKind kind = FrameKind.Bronze)
        {
            Panel(c, r, kind, .7f, .98f, 16);
            var pad = 16f; float y = r.y + pad - 2;
            if (!string.IsNullOrEmpty(heading))
            {
                var h = ThemeText.Of(ThemeFont.Label, 15, ThemePalette.Gold, ThemeAnchor.UpperLeft); h.shadowY = 1.5f; h.shadowAlpha = .8f;
                ThemeDraw.Text(c, heading, new ThemeRect(r.x + pad, y, r.w - pad * 2, 22), h); y += 25;
            }
            ThemeDraw.Text(c, body, new ThemeRect(r.x + pad, y, r.w - pad * 2, r.yMax - y - pad + 4), new ThemeText { font = ThemeFont.Body, size = 14.5f, color = ThemePalette.Text, anchor = ThemeAnchor.UpperLeft, wrap = true });
        }
    }
}
