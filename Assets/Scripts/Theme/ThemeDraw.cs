using System;

namespace GildedFate.Theme
{
    /// <summary>Drawing helpers built on IThemeCanvas: 9-slice, tiling, and text with shadow, outline and a two-tone metal fill.</summary>
    public static class ThemeDraw
    {
        /// <summary>Stretches a 9-slice image over dest. srcSlice is the border in image pixels; destSlice is how big that border should be on screen.</summary>
        public static void Slice9(IThemeCanvas c, ThemeImage img, ThemeRect dest, float destSlice, ThemeColor tint)
        {
            var s = img.slice; if (s <= 0 || dest.w <= 0 || dest.h <= 0) { c.Image(img, dest, tint); return; }
            var ds = Math.Min(destSlice, Math.Min(dest.w, dest.h) * .5f);
            float u = s / (float)img.width, v = s / (float)img.height;
            float[] dx = { dest.x, dest.x + ds, dest.xMax - ds, dest.xMax }, dy = { dest.y, dest.y + ds, dest.yMax - ds, dest.yMax };
            float[] ux = { 0, u, 1 - u, 1 }, uy = { 0, v, 1 - v, 1 };
            for (var j = 0; j < 3; j++) for (var i = 0; i < 3; i++)
            {
                var r = new ThemeRect(dx[i], dy[j], dx[i + 1] - dx[i], dy[j + 1] - dy[j]); if (r.w <= 0 || r.h <= 0) continue;
                c.ImageUV(img, r, new ThemeRect(ux[i], uy[j], ux[i + 1] - ux[i], uy[j + 1] - uy[j]), tint);
            }
        }

        /// <summary>Tiles a repeating image (grain) over dest. tileSize is the on-screen size of one tile.</summary>
        public static void Tile(IThemeCanvas c, ThemeImage img, ThemeRect dest, float tileSize, ThemeColor tint, float offsetX = 0, float offsetY = 0) =>
            c.ImageUV(img, dest, new ThemeRect(offsetX / tileSize, offsetY / tileSize, dest.w / tileSize, dest.h / tileSize), tint);

        static float LineHeight(float size) => size * 1.28f;

        /// <summary>Text with the theme's effects. Shadow: two offset copies. Outline: eight offset copies. Metal: three horizontal bands of color, clipped.</summary>
        public static void Text(IThemeCanvas c, string text, ThemeRect r, ThemeText s)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (s.outline > 0)
            {
                var o = s.outline; var oc = s.outlineColor.a > 0 ? s.outlineColor : new ThemeColor(0, 0, 0, .75f);
                for (var i = 0; i < 8; i++) { var a = i * (float)Math.PI / 4; c.TextRaw(text, r.Offset((float)Math.Cos(a) * o, (float)Math.Sin(a) * o), s.font, s.size, oc, s.anchor, s.wrap); }
            }
            if (s.shadowY > 0 && s.shadowAlpha > 0)
            {
                c.TextRaw(text, r.Offset(0, s.shadowY), s.font, s.size, new ThemeColor(0, 0, 0, s.shadowAlpha * .55f), s.anchor, s.wrap);
                c.TextRaw(text, r.Offset(0, s.shadowY * .5f), s.font, s.size, new ThemeColor(0, 0, 0, s.shadowAlpha * .45f), s.anchor, s.wrap);
            }
            if (!s.gradient || s.wrap) { c.TextRaw(text, r, s.font, s.size, s.color, s.anchor, s.wrap); return; }

            // metal fill: find the line box for this anchor, then draw the text through three clips (light, mid, dark)
            var lh = LineHeight(s.size); float top;
            var row = (int)s.anchor / 3;
            top = row == 0 ? r.y : row == 1 ? r.cy - lh * .5f : r.yMax - lh;
            var mid = ThemeColor.Lerp(s.color, s.bottomColor, .5f);
            var b1 = top + lh * .42f; var b2 = top + lh * .66f;
            var full = new ThemeRect(r.x - s.size, top - s.size, r.w + s.size * 2, lh + s.size * 2);
            Band(c, text, r, s, s.color, new ThemeRect(full.x, full.y, full.w, b1 - full.y));
            Band(c, text, r, s, mid, new ThemeRect(full.x, b1, full.w, b2 - b1));
            Band(c, text, r, s, s.bottomColor, new ThemeRect(full.x, b2, full.w, full.yMax - b2));
        }
        static void Band(IThemeCanvas c, string text, ThemeRect r, ThemeText s, ThemeColor color, ThemeRect clip)
        {
            c.PushClip(clip); c.TextRaw(text, r, s.font, s.size, color, s.anchor, false); c.PopClip();
        }
    }
}
