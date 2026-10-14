using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using GildedFate.Theme;
using SkiaSharp;

/// <summary>Draws theme code onto a bitmap, so the design can be seen without Unity. Mirrors what the game's Unity canvas does.</summary>
public sealed class SkiaCanvas : IThemeCanvas, IDisposable
{
    public readonly SKBitmap Bitmap; readonly SKCanvas g;
    readonly Dictionary<string, SKBitmap> images = new Dictionary<string, SKBitmap>();
    readonly Dictionary<ThemeFont, SKTypeface> fonts = new Dictionary<ThemeFont, SKTypeface>();
    public float Time { get; set; }

    public SkiaCanvas(int width, int height, string fontDir)
    {
        Bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul); g = new SKCanvas(Bitmap);
        fonts[ThemeFont.Heading] = Load(fontDir, "SourceSerif4Display-Semibold.ttf"); fonts[ThemeFont.Label] = Load(fontDir, "SourceSans3-Semibold.ttf"); fonts[ThemeFont.Body] = Load(fontDir, "SourceSans3-Regular.ttf");
    }
    static SKTypeface Load(string dir, string file) { var p = Path.Combine(dir, file); var t = File.Exists(p) ? SKTypeface.FromFile(p) : null; return t ?? SKTypeface.Default; }

    static SKColor C(ThemeColor c) => new SKColor(B(c.r), B(c.g), B(c.b), B(c.a));
    static byte B(float v) => (byte)Math.Round((v < 0 ? 0 : v > 1 ? 1 : v) * 255);
    static SKRect R(ThemeRect r) => new SKRect(r.x, r.y, r.xMax, r.yMax);

    public void Clear(ThemeColor c) => g.Clear(C(c));
    public void DrawBackground(string pngPath, float darken = 0f)
    {
        using (var bg = SKBitmap.Decode(pngPath))
        {
            if (bg == null) return;
            g.DrawBitmap(bg, new SKRect(0, 0, Bitmap.Width, Bitmap.Height), new SKPaint { FilterQuality = SKFilterQuality.High });
            if (darken > 0) g.DrawRect(0, 0, Bitmap.Width, Bitmap.Height, new SKPaint { Color = new SKColor(0, 0, 0, B(darken)) });
        }
    }

    public void DrawBackgroundTinted(string pngPath, float x, float y, float w, float h, ThemeColor tint)
    {
        using (var bg = SKBitmap.Decode(pngPath))
        {
            if (bg == null) return;
            // ScaleAndCrop: fill the rectangle, cropping the overflow evenly.
            var k = Math.Max(w / bg.Width, h / bg.Height); var sw = w / k; var sh = h / k;
            var src = new SKRect((bg.Width - sw) / 2, (bg.Height - sh) / 2, (bg.Width + sw) / 2, (bg.Height + sh) / 2);
            using (var p = new SKPaint { FilterQuality = SKFilterQuality.High, ColorFilter = SKColorFilter.CreateBlendMode(C(tint), SKBlendMode.Modulate) }) g.DrawBitmap(bg, src, new SKRect(x, y, x + w, y + h), p);
        }
    }

    SKBitmap Get(ThemeImage img)
    {
        if (images.TryGetValue(img.id, out var bmp)) return bmp;
        bmp = new SKBitmap(new SKImageInfo(img.width, img.height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        Marshal.Copy(img.rgba, 0, bmp.GetPixels(), img.rgba.Length);
        images[img.id] = bmp; return bmp;
    }

    public void Fill(ThemeRect d, ThemeColor c) { using (var p = new SKPaint { Color = C(c), IsAntialias = false }) g.DrawRect(R(d), p); }
    public void Image(ThemeImage img, ThemeRect d, ThemeColor tint) => ImageUV(img, d, new ThemeRect(0, 0, 1, 1), tint);

    public void ImageUV(ThemeImage img, ThemeRect d, ThemeRect uv, ThemeColor tint)
    {
        var bmp = Get(img);
        using (var p = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = false, ColorFilter = SKColorFilter.CreateBlendMode(C(tint), SKBlendMode.Modulate) })
        {
            var tiled = uv.x < -1e-4f || uv.y < -1e-4f || uv.xMax > 1.0001f || uv.yMax > 1.0001f;
            if (!tiled)
            {
                var src = new SKRect(uv.x * img.width, uv.y * img.height, uv.xMax * img.width, uv.yMax * img.height);
                g.DrawBitmap(bmp, src, R(d), p);
            }
            else
            {
                var sx = d.w / (uv.w * img.width); var sy = d.h / (uv.h * img.height);
                var m = SKMatrix.CreateScale(sx, sy); m = m.PostConcat(SKMatrix.CreateTranslation(d.x - uv.x * img.width * sx, d.y - uv.y * img.height * sy));
                using (var shader = SKShader.CreateBitmap(bmp, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, m)) { p.Shader = shader; g.DrawRect(R(d), p); }
            }
        }
    }

    public float MeasureText(string text, ThemeFont font, float size) { using (var p = Paint(font, size, ThemeColor.White)) return p.MeasureText(text); }
    SKPaint Paint(ThemeFont font, float size, ThemeColor c) => new SKPaint { Typeface = fonts[font], TextSize = size, Color = C(c), IsAntialias = true, SubpixelText = true };

    public void TextRaw(string text, ThemeRect d, ThemeFont font, float size, ThemeColor color, ThemeAnchor anchor, bool wrap)
    {
        using (var p = Paint(font, size, color))
        {
            var lines = new List<string>();
            if (!wrap) lines.Add(text);
            else
            {
                foreach (var para in text.Split('\n'))
                {
                    var cur = "";
                    foreach (var word in para.Split(' '))
                    {
                        var test = cur.Length == 0 ? word : cur + " " + word;
                        if (p.MeasureText(test) > d.w && cur.Length > 0) { lines.Add(cur); cur = word; } else cur = test;
                    }
                    lines.Add(cur);
                }
            }
            var m = p.FontMetrics; var lh = size * 1.28f; var total = lh * lines.Count;
            var col = (int)anchor % 3; var row = (int)anchor / 3;
            var top = row == 0 ? d.y : row == 1 ? d.cy - total * .5f : d.yMax - total;
            for (var i = 0; i < lines.Count; i++)
            {
                var w = p.MeasureText(lines[i]);
                var x = col == 0 ? d.x : col == 1 ? d.cx - w * .5f : d.xMax - w;
                var baseline = top + lh * i + lh * .5f - (m.Ascent + m.Descent) * .5f;
                g.DrawText(lines[i], x, baseline, p);
            }
        }
    }

    public void Line(float x0, float y0, float x1, float y1, float width, ThemeColor color)
    {
        var dx = x1 - x0; var dy = y1 - y0; var len = (float)Math.Sqrt(dx * dx + dy * dy); if (len < .01f) return;
        var bmp = Get(ThemeAssets.LineSoft);
        using (var p = new SKPaint { FilterQuality = SKFilterQuality.High, ColorFilter = SKColorFilter.CreateBlendMode(C(color), SKBlendMode.Modulate) })
        {
            g.Save(); g.Translate(x0, y0); g.RotateDegrees((float)(Math.Atan2(dy, dx) * 180 / Math.PI));
            g.DrawBitmap(bmp, new SKRect(0, 0, bmp.Width, bmp.Height), new SKRect(0, -width * .5f, len, width * .5f), p); g.Restore();
        }
    }

    public void PushClip(ThemeRect c) { g.Save(); g.ClipRect(R(c)); }
    public void PopClip() => g.Restore();

    public void Save(string path) { using (var img = SKImage.FromBitmap(Bitmap)) using (var data = img.Encode(SKEncodedImageFormat.Png, 100)) File.WriteAllBytes(path, data.ToArray()); }
    public void Dispose() { g.Dispose(); Bitmap.Dispose(); foreach (var b in images.Values) b.Dispose(); }
}
