using System;
using System.Collections.Generic;
using UnityEngine;

namespace GildedFate.ChronicleBook
{
    // Traces the actual handwritten glyph's centerline. Revealing ink along that path keeps
    // curves, loops, crossbars and dots, instead of wiping or fading a whole character.
    public sealed class ChronicleInkStrokeRenderer : IDisposable
    {
        const int RasterSize = 96;
        readonly Font font;
        readonly Dictionary<char, CharacterInfo> metrics = new();
        readonly Dictionary<char, Glyph> glyphs = new();
        Texture2D atlas;
        Texture2D inkTip;
        readonly Dictionary<string, Vector2[]> layouts = new();
        sealed class Glyph
        {
            public CharacterInfo info;
            public int width, height;
            public Color32[] original, working;
            public float[] arrival;
            public List<Vector2> path;
            public Texture2D texture, finished;
            public float progress = -1f;
        }

        public ChronicleInkStrokeRenderer(Font font) { this.font = font; }

        void EnsureAtlas()
        {
            if (atlas) return;
            var letters = "";
            for (var c = 32; c < 127; c++) letters += (char)c;
            letters += "’‘“”—–…éèáàöüñ";
            font.RequestCharactersInTexture(letters, RasterSize, FontStyle.Italic);
            foreach (var c in letters)
                if (font.GetCharacterInfo(c, out var info, RasterSize, FontStyle.Italic)) metrics[c] = info;
            var source = font.material.mainTexture;
            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            try
            {
                Graphics.Blit(source, target); RenderTexture.active = target;
                atlas = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
                atlas.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); atlas.Apply();
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
        }

        Glyph Get(char letter)
        {
            if (glyphs.TryGetValue(letter, out var cached)) return cached;
            EnsureAtlas();
            if (!metrics.TryGetValue(letter, out var info)) return null;
            var w = info.maxX - info.minX; var h = info.maxY - info.minY;
            if (w <= 0 || h <= 0) return null;
            var glyph = new Glyph { info = info, width = w, height = h, original = new Color32[w * h], working = new Color32[w * h], arrival = new float[w * h] };
            var mask = new bool[w * h];
            for (var y = 0; y < h; y++) for (var x = 0; x < w; x++)
            {
                var bottom = Vector2.Lerp(info.uvBottomLeft, info.uvBottomRight, (x + .5f) / w);
                var top = Vector2.Lerp(info.uvTopLeft, info.uvTopRight, (x + .5f) / w);
                var uv = Vector2.Lerp(bottom, top, (y + .5f) / h);
                var alpha = (byte)Mathf.RoundToInt(atlas.GetPixelBilinear(uv.x, uv.y).a * 255);
                var i = y * w + x;
                // Small, deterministic density differences suggest ink caught in the vellum's fibers.
                // Retain the font's thick downstrokes and fine connecting strokes.
                var grain = Mathf.PerlinNoise(x * .43f + letter * .19f, y * .37f + letter * .07f);
                var density = .88f + .12f * grain;
                var tone = (byte)Mathf.RoundToInt(238f + grain * 17f);
                glyph.original[i] = new Color32(tone, tone, tone, (byte)Mathf.RoundToInt(alpha * density));
                mask[i] = alpha >= 100;
            }
            Thin(mask, w, h); glyph.path = Trace(mask, w, h, letter);
            if (glyph.path.Count == 0) return null;
            // Measure the nib's local width from the glyph silhouette. A round brush front avoids
            // nearest-skeleton Voronoi seams across thick downstrokes.
            var outline = new List<Vector2>();
            for (var y = -1; y <= h; y++) for (var x = -1; x <= w; x++)
            {
                if (x >= 0 && x < w && y >= 0 && y < h && glyph.original[y * w + x].a >= 100) continue;
                var touches = false;
                for (var dy = -1; dy <= 1 && !touches; dy++) for (var dx = -1; dx <= 1; dx++)
                {
                    var nx = x + dx; var ny = y + dy;
                    if (nx >= 0 && nx < w && ny >= 0 && ny < h && glyph.original[ny * w + nx].a >= 100) { touches = true; break; }
                }
                if (touches) outline.Add(new Vector2(x, y));
            }
            var radiusSquared = new float[glyph.path.Count];
            for (var p = 0; p < glyph.path.Count; p++)
            {
                var radius = float.MaxValue;
                foreach (var edge in outline) radius = Mathf.Min(radius, (edge - glyph.path[p]).sqrMagnitude);
                radiusSquared[p] = Mathf.Pow(Mathf.Sqrt(radius) + .65f, 2);
            }
            for (var y = 0; y < h; y++) for (var x = 0; x < w; x++)
            {
                var i = y * w + x; if (glyph.original[i].a == 0) { glyph.arrival[i] = 1f; continue; }
                var best = float.MaxValue; var when = 0; var coveredAt = -1;
                for (var p = 0; p < glyph.path.Count; p++)
                {
                    var dx = glyph.path[p].x - x; var dy = glyph.path[p].y - y; var d = dx * dx + dy * dy;
                    if (d < best) { best = d; when = p; }
                    if (coveredAt < 0 && d <= radiusSquared[p]) coveredAt = p;
                }
                glyph.arrival[i] = Mathf.Min(.99f, (coveredAt >= 0 ? coveredAt : when + Mathf.Sqrt(best) * .15f) / glyph.path.Count);
            }
            // Smooth only the ink's local arrival times. Skeleton junction pixels otherwise make a serrated pen front.
            // Keep distant stroke times separate, preserving lifted crossbars and dots.
            for (var pass = 0; pass < 2; pass++)
            {
                var filtered = new float[w * h];
                for (var y = 0; y < h; y++) for (var x = 0; x < w; x++)
                {
                    var i = y * w + x;
                    var sum = 0f; var weight = 0f;
                    for (var dy = -2; dy <= 2; dy++) for (var dx = -2; dx <= 2; dx++)
                    {
                        var nx = x + dx; var ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        var neighbor = ny * w + nx;
                        if (glyph.original[neighbor].a == 0 || Mathf.Abs(glyph.arrival[i] - glyph.arrival[neighbor]) > .18f) continue;
                        var contribution = Mathf.Exp(-(dx * dx + dy * dy) * .38f) * glyph.original[neighbor].a;
                        sum += glyph.arrival[neighbor] * contribution; weight += contribution;
                    }
                    filtered[i] = weight > 0f ? sum / weight : glyph.arrival[i];
                }
                glyph.arrival = filtered;
            }
            glyph.texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Chronicle ink " + letter, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            glyph.finished = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Chronicle dry ink " + letter, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            glyph.finished.SetPixels32(glyph.original); glyph.finished.Apply(false);
            glyphs[letter] = glyph; return glyph;
        }

        static void Thin(bool[] pixels, int w, int h)
        {
            var remove = new List<int>(); var n = new bool[8]; bool changed;
            do
            {
                changed = false;
                for (var pass = 0; pass < 2; pass++)
                {
                    remove.Clear();
                    for (var y = 1; y < h - 1; y++) for (var x = 1; x < w - 1; x++)
                    {
                        var i = y * w + x; if (!pixels[i]) continue;
                        n[0] = pixels[i + w]; n[1] = pixels[i + w + 1]; n[2] = pixels[i + 1]; n[3] = pixels[i - w + 1];
                        n[4] = pixels[i - w]; n[5] = pixels[i - w - 1]; n[6] = pixels[i - 1]; n[7] = pixels[i + w - 1];
                        var count = 0; var changes = 0;
                        for (var k = 0; k < 8; k++) { if (n[k]) count++; if (!n[k] && n[(k + 1) % 8]) changes++; }
                        if (count < 2 || count > 6 || changes != 1) continue;
                        if (pass == 0 ? n[0] && n[2] && n[4] || n[2] && n[4] && n[6] : n[0] && n[2] && n[6] || n[0] && n[4] && n[6]) continue;
                        remove.Add(i);
                    }
                    foreach (var i in remove) pixels[i] = false;
                    changed |= remove.Count > 0;
                }
            } while (changed);
        }

        static List<int> Neighbors(int i, bool[] mask, int w, int h)
        {
            var result = new List<int>(8); var x = i % w; var y = i / w;
            for (var dy = -1; dy <= 1; dy++) for (var dx = -1; dx <= 1; dx++)
                if ((dx != 0 || dy != 0) && x + dx >= 0 && x + dx < w && y + dy >= 0 && y + dy < h && mask[(y + dy) * w + x + dx]) result.Add((y + dy) * w + x + dx);
            return result;
        }

        static List<Vector2> Trace(bool[] mask, int w, int h, char letter)
        {
            var components = new List<List<int>>(); var seen = new bool[mask.Length]; var queue = new Queue<int>();
            for (var i = 0; i < mask.Length; i++)
            {
                if (!mask[i] || seen[i]) continue;
                var component = new List<int>(); queue.Enqueue(i); seen[i] = true;
                while (queue.Count > 0)
                {
                    var next = queue.Dequeue(); component.Add(next);
                    foreach (var neighbor in Neighbors(next, mask, w, h)) if (!seen[neighbor]) { seen[neighbor] = true; queue.Enqueue(neighbor); }
                }
                components.Add(component);
            }
            components.Sort((a, b) => b.Count.CompareTo(a.Count)); // stem and loops first; lifted dots last
            Array.Clear(seen, 0, seen.Length); var path = new List<Vector2>();
            foreach (var component in components)
            {
                var start = component[0]; var score = float.MaxValue;
                foreach (var p in component)
                {
                    var candidate = p % w + .45f * (h - 1 - p / w);
                    // Round letters begin at their bowl; a descender is an exit stroke.
                    if ("acdegoqs".IndexOf(char.ToLowerInvariant(letter)) >= 0 && component.Count == components[0].Count)
                    {
                        var target = char.ToLowerInvariant(letter) == 'e' ? new Vector2(w * .25f, h * .55f)
                            : char.ToLowerInvariant(letter) == 'd' ? new Vector2(w * .65f, h * .48f) : new Vector2(w * .75f, h * .82f);
                        candidate = (new Vector2(p % w, p / w) - target).sqrMagnitude;
                    }
                    else if (Neighbors(p, mask, w, h).Count > 1) candidate += w + h;
                    if (candidate < score) { score = candidate; start = p; }
                }
                var parents = new int[mask.Length]; Array.Fill(parents, -1);
                var stack = new Stack<int>(); stack.Push(start); seen[start] = true; path.Add(new Vector2(start % w, start / w));
                while (stack.Count > 0)
                {
                    var current = stack.Peek(); var next = -1; var best = float.MaxValue;
                    foreach (var neighbor in Neighbors(current, mask, w, h))
                    {
                        if (seen[neighbor]) continue;
                        var candidate = (neighbor % w + .25f * (h - 1 - neighbor / w)) * .001f;
                        if (parents[current] >= 0)
                        {
                            var parent = parents[current];
                            var incoming = new Vector2(current % w - parent % w, current / w - parent / w).normalized;
                            var outgoing = new Vector2(neighbor % w - current % w, neighbor / w - current / w).normalized;
                            candidate += (1f - Vector2.Dot(incoming, outgoing)) * 4f;
                        }
                        if (candidate < best) { best = candidate; next = neighbor; }
                    }
                    if (next >= 0) { parents[next] = current; stack.Push(next); seen[next] = true; path.Add(new Vector2(next % w, next / w)); }
                    else { stack.Pop(); if (stack.Count > 0) { var p = stack.Peek(); path.Add(new Vector2(p % w, p / w)); } }
                }
            }
            return path;
        }

        public Texture2D Sample(char letter, float progress, out Vector2 tip)
        {
            tip = default; var glyph = Get(letter); if (glyph == null) return null;
            progress = Mathf.Clamp01(progress);
            if (Mathf.Abs(glyph.progress - progress) > .001f)
            {
                for (var i = 0; i < glyph.working.Length; i++)
                {
                    var opacity = progress <= 0 ? 0f : progress >= 1 ? 1f : Mathf.Clamp01((progress - glyph.arrival[i]) * glyph.path.Count * .22f + .5f);
                    glyph.working[i] = glyph.original[i]; glyph.working[i].a = (byte)(glyph.original[i].a * opacity);
                }
                glyph.texture.SetPixels32(glyph.working); glyph.texture.Apply(false); glyph.progress = progress;
            }
            tip = glyph.path[Mathf.Min(glyph.path.Count - 1, Mathf.FloorToInt(progress * glyph.path.Count))]; return glyph.texture;
        }

        Vector2[] Layout(Rect area, string text, GUIStyle style)
        {
            var key = style.fontSize + ":" + area.width.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ":" + text;
            if (layouts.TryGetValue(key, out var result)) return result;
            if (layouts.Count > 64) layouts.Clear();
            result = new Vector2[text.Length];
            var content = new GUIContent(text);
            var local = new Rect(0, 0, area.width, area.height);
            for (var i = 0; i < text.Length; i++) result[i] = style.GetCursorPixelPosition(local, content, i);
            layouts[key] = result; return result;
        }

        Rect GlyphRect(Rect area, Vector2 caret, Glyph glyph, GUIStyle style)
        {
            var scale = style.fontSize / (float)RasterSize;
            var baseline = area.y + caret.y + font.ascent * style.fontSize / Mathf.Max(1f, font.fontSize);
            return new Rect(area.x + caret.x + glyph.info.minX * scale, baseline - glyph.info.maxY * scale, glyph.width * scale, glyph.height * scale);
        }

        public Texture2D FinishedSample(char letter) => Get(letter)?.finished;

        public void DrawPassage(Rect area, string text, int written, int strokeIndex, float progress, GUIStyle style, Color ink)
        {
            if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(text)) return;
            var positions = Layout(area, text, style);
            var old = GUI.color;
            try
            {
                GUI.color = ink;
                for (var i = 0; i < Mathf.Min(written, text.Length); i++)
                {
                    if (char.IsWhiteSpace(text[i])) continue;
                    var glyph = Get(text[i]);
                    if (glyph == null) continue;
                    GUI.DrawTexture(GlyphRect(area, positions[i], glyph, style), glyph.finished);
                }
                if (strokeIndex >= 0 && strokeIndex < text.Length && progress > 0f)
                    Draw(area, text, strokeIndex, progress, style, ink);
            }
            finally { GUI.color = old; }
        }

        public void Draw(Rect area, string text, int index, float progress, GUIStyle style, Color ink)
        {
            if (Event.current.type != EventType.Repaint || char.IsWhiteSpace(text[index])) return;
            var texture = Sample(text[index], progress, out var tip); if (!texture) return;
            var glyph = glyphs[text[index]]; var scale = style.fontSize / (float)RasterSize;
            var dest = GlyphRect(area, Layout(area, text, style)[index], glyph, style);
            var old = GUI.color;
            try
            {
                GUI.color = ink; GUI.DrawTexture(dest, texture);
                // A small ink bead follows the stroke without rotating the page's clipping rectangle.
                if (!inkTip)
                {
                    inkTip = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                    var pixels = new Color32[256];
                    for (var y = 0; y < 16; y++) for (var x = 0; x < 16; x++)
                    {
                        var distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(8, 8));
                        pixels[y * 16 + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(7.5f - distance) * 255));
                    }
                    inkTip.SetPixels32(pixels); inkTip.Apply(false, true);
                }
                var point = new Vector2(dest.x + (tip.x + .5f) * scale, dest.yMax - (tip.y + .5f) * scale);
                var diameter = Mathf.Max(1.3f, style.fontSize * .085f);
                GUI.color = new Color(ink.r * .75f, ink.g * .75f, ink.b * .75f, ink.a * .75f);
                GUI.DrawTexture(new Rect(point.x - diameter * .5f, point.y - diameter * .5f, diameter, diameter), inkTip);
            }
            finally { GUI.color = old; }
        }

        public void Dispose()
        {
            foreach (var glyph in glyphs.Values) { Destroy(glyph.texture); Destroy(glyph.finished); }
            if (atlas) Destroy(atlas); if (inkTip) Destroy(inkTip); glyphs.Clear(); layouts.Clear();
        }
        static void Destroy(UnityEngine.Object value) { if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }
    }
}
