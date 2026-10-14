using System;

namespace GildedFate.Theme
{
    // The Gilded Fate visual theme is engine-free on purpose: colors, rectangles, procedurally generated images and the widgets built from
    // them know nothing about Unity. The game draws them through IThemeCanvas (Unity's GUI), and the ThemePreview tool draws the very same
    // code to PNG files, so a design can be judged in seconds without waiting for a game build.

    public struct ThemeColor
    {
        public float r, g, b, a;
        public ThemeColor(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static ThemeColor Hex(uint rgb, float a = 1f) => new ThemeColor(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);
        public ThemeColor WithAlpha(float alpha) => new ThemeColor(r, g, b, alpha);
        public ThemeColor Mul(float k) => new ThemeColor(Math.Min(1f, r * k), Math.Min(1f, g * k), Math.Min(1f, b * k), a);
        public ThemeColor MulAlpha(float k) => new ThemeColor(r, g, b, a * k);
        public static ThemeColor Lerp(ThemeColor x, ThemeColor y, float t) { t = t < 0 ? 0 : t > 1 ? 1 : t; return new ThemeColor(x.r + (y.r - x.r) * t, x.g + (y.g - x.g) * t, x.b + (y.b - x.b) * t, x.a + (y.a - x.a) * t); }
        public static readonly ThemeColor White = new ThemeColor(1, 1, 1, 1), Black = new ThemeColor(0, 0, 0, 1), Clear = new ThemeColor(0, 0, 0, 0);
    }

    public struct ThemeRect
    {
        public float x, y, w, h;
        public ThemeRect(float x, float y, float w, float h) { this.x = x; this.y = y; this.w = w; this.h = h; }
        public float xMax => x + w; public float yMax => y + h; public float cx => x + w * .5f; public float cy => y + h * .5f;
        public ThemeRect Inflate(float d) => new ThemeRect(x - d, y - d, w + d * 2, h + d * 2);
        public ThemeRect Inflate(float dx, float dy) => new ThemeRect(x - dx, y - dy, w + dx * 2, h + dy * 2);
        public ThemeRect Offset(float dx, float dy) => new ThemeRect(x + dx, y + dy, w, h);
        public bool Contains(float px, float py) => px >= x && px < xMax && py >= y && py < yMax;
    }

    public enum ThemeFont { Heading, Label, Body }
    /// <summary>Same order as Unity's TextAnchor, so the game can cast.</summary>
    public enum ThemeAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }

    public struct ThemeText
    {
        public ThemeFont font; public float size; public ThemeColor color; public ThemeAnchor anchor; public bool wrap;
        public bool gradient; public ThemeColor bottomColor;          // a two-tone (metal) fill from color (top) to bottomColor
        public float shadowY; public float shadowAlpha;               // soft drop shadow
        public float outline; public ThemeColor outlineColor;         // 1px dark outline for legibility over busy art
        public static ThemeText Of(ThemeFont font, float size, ThemeColor color, ThemeAnchor anchor = ThemeAnchor.MiddleCenter) =>
            new ThemeText { font = font, size = size, color = color, anchor = anchor };
    }

    /// <summary>What a screen is drawn onto. The game implements it over Unity's GUI; the preview tool implements it over a bitmap.
    /// All coordinates are in canvas units with the origin at the top left and y growing downward.
    /// Canvases stay deliberately simple: effects (shadow, outline, metal gradient text, 9-slice) are built from these calls in ThemeDraw.</summary>
    public interface IThemeCanvas
    {
        float Time { get; }
        void Fill(ThemeRect dest, ThemeColor color);
        void Image(ThemeImage image, ThemeRect dest, ThemeColor tint);
        /// <summary>uv is in 0..1 with its origin at the image's top left (y down). Values above 1 tile the image.</summary>
        void ImageUV(ThemeImage image, ThemeRect dest, ThemeRect uv, ThemeColor tint);
        /// <summary>Plain text in one color: no effects. Wrapping, if asked for, breaks on spaces to fit the rectangle's width.</summary>
        void TextRaw(string text, ThemeRect dest, ThemeFont font, float size, ThemeColor color, ThemeAnchor anchor, bool wrap);
        float MeasureText(string text, ThemeFont font, float size);
        void PushClip(ThemeRect clip);
        void PopClip();
    }
}
