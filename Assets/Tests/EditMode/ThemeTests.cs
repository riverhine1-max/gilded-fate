using System;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Theme;
using NUnit.Framework;

namespace GildedFate.Chronicle.Tests
{
    /// <summary>The engine-free visual theme: its generated images, 9-slice math, text effects and widgets, checked without Unity.</summary>
    [TestFixture]
    public class ThemeTests
    {
        sealed class Recorder : IThemeCanvas
        {
            public readonly List<string> calls = new List<string>(); public readonly List<ThemeRect> slices = new List<ThemeRect>(), uvs = new List<ThemeRect>();
            public int pushes, pops, images, fills, texts; public float time = 1.25f; public readonly List<(string text, ThemeColor color)> drawn = new List<(string, ThemeColor)>();
            public float Time => time;
            public void Fill(ThemeRect d, ThemeColor c) { fills++; calls.Add("fill"); }
            public void Image(ThemeImage i, ThemeRect d, ThemeColor t) { images++; calls.Add("img:" + i.id); }
            public void ImageUV(ThemeImage i, ThemeRect d, ThemeRect uv, ThemeColor t) { images++; slices.Add(d); uvs.Add(uv); calls.Add("uv:" + i.id); }
            public void TextRaw(string s, ThemeRect d, ThemeFont f, float size, ThemeColor c, ThemeAnchor a, bool w) { texts++; drawn.Add((s, c)); calls.Add("text:" + s); }
            public float MeasureText(string s, ThemeFont f, float size) => s.Length * size * .5f;
            public void PushClip(ThemeRect c) { pushes++; calls.Add("push"); }
            public void PopClip() { pops++; calls.Add("pop"); }
        }

        static IEnumerable<ThemeImage> AllAssets() => new[]
        {
            ThemeAssets.FrameGold, ThemeAssets.FrameBronze, ThemeAssets.FrameSteel, ThemeAssets.FrameCrimson, ThemeAssets.FrameAzure, ThemeAssets.FrameViolet, ThemeAssets.FrameJade,
            ThemeAssets.RimBronze, ThemeAssets.RimGold, ThemeAssets.RimSteel, ThemeAssets.Shadow, ThemeAssets.Glow, ThemeAssets.GlowTight, ThemeAssets.Ring, ThemeAssets.Vignette,
            ThemeAssets.Grain, ThemeAssets.Sheen, ThemeAssets.DiamondGold, ThemeAssets.PanelGlass, ThemeAssets.ButtonGlass, ThemeAssets.ButtonGlassHot, ThemeAssets.ButtonGlassPressed,
            ThemeAssets.TopSheen, ThemeAssets.RuleFade, ThemeAssets.BarGloss, ThemeAssets.BarTrough, ThemeAssets.PlateFade, ThemeAssets.HudGlass, ThemeAssets.RowGlass, ThemeAssets.ShadeDown,
        };

        // ---------------------------------------------------------------- generated images
        [Test] public void EveryGeneratedImageIsWellFormedWithUniqueIds()
        {
            var all = AllAssets().ToList();
            foreach (var img in all) { Assert.AreEqual(img.width * img.height * 4, img.rgba.Length, img.id); Assert.IsTrue(img.width > 0 && img.height > 0, img.id); }
            Assert.AreEqual(all.Count, all.Select(i => i.id).Distinct().Count(), "image ids are unique (the game caches textures by image)");
        }

        [Test] public void FramesAreHollowOpaqueAtTheEdgeAndSymmetricInShape()
        {
            foreach (var f in new[] { ThemeAssets.FrameGold, ThemeAssets.FrameBronze, ThemeAssets.FrameCrimson, ThemeAssets.FrameAzure, ThemeAssets.FrameViolet, ThemeAssets.FrameJade, ThemeAssets.FrameSteel })
            {
                Assert.AreEqual(0, (int)f.rgba[((f.height / 2) * f.width + f.width / 2) * 4 + 3], f.id + " centre is transparent so the panel behind shows");
                Assert.Greater(f.rgba[(2 * f.width + f.width / 2) * 4 + 3], 200, f.id + " top edge is solid");
                Assert.AreEqual(24, f.slice, f.id);
                for (var y = 0; y < f.height; y++) for (var x = 0; x < f.width / 2; x++)
                    Assert.AreEqual((double)f.rgba[(y * f.width + x) * 4 + 3], (double)f.rgba[(y * f.width + (f.width - 1 - x)) * 4 + 3], 3.0, f.id + " alpha mirrors left-right at " + x + "," + y);
            }
        }

        [Test] public void LightComesFromTheUpperLeftSoTheBevelIsBrighterThereThanLowerRight()
        {
            var f = ThemeAssets.FrameGold;
            float Lum(int x, int y) { var c = f.Get(x, y); return c.r * .3f + c.g * .59f + c.b * .11f; }
            Assert.Greater(Lum(f.width / 2, 2), Lum(f.width / 2, f.height - 3), "top edge lit, bottom edge in shadow");
            Assert.Greater(Lum(2, f.height / 2), Lum(f.width - 3, f.height / 2), "left edge lit, right edge in shadow");
        }

        [Test] public void GlowsAndShadowsFallOffSmoothlyFromTheirCentre()
        {
            var g = ThemeAssets.Glow; Assert.Greater(g.rgba[((g.height / 2) * g.width + g.width / 2) * 4 + 3], 245); Assert.AreEqual(0, (int)g.rgba[3], "corner is dark");
            var s = ThemeAssets.Shadow; Assert.AreEqual(28, s.slice);
            Assert.Greater(s.rgba[((s.height / 2) * s.width + s.width / 2) * 4 + 3], 220, "shadow is densest under the element");
            Assert.Less(s.rgba[3], 6, "and fades to nothing at its outer corner");
            var r = ThemeAssets.Ring; Assert.AreEqual(0, (int)r.rgba[((r.height / 2) * r.width + r.width / 2) * 4 + 3], "a ring is hollow");
        }

        [Test] public void GradientsHitTheirEndStopsExactly()
        {
            var g = ThemeImages.Gradient("t", 16, new ThemeImages.Stop(0, ThemeColor.Hex(0xFF0000)), new ThemeImages.Stop(1, ThemeColor.Hex(0x0000FF)));
            Assert.AreEqual(255, (int)g.rgba[0]); Assert.AreEqual(0, (int)g.rgba[2]);
            var last = (15 * g.width) * 4; Assert.AreEqual(0, (int)g.rgba[last]); Assert.AreEqual(255, (int)g.rgba[last + 2]);
        }

        [Test] public void GenerationIsDeterministicAndGrainTilesWithoutASeam()
        {
            Assert.IsTrue(ThemeImages.Frame9("a", FramePalette.Gold).rgba.SequenceEqual(ThemeImages.Frame9("b", FramePalette.Gold).rgba), "frames regenerate identically");
            var g1 = ThemeImages.Grain("a", 128, 7); var g2 = ThemeImages.Grain("b", 128, 7); Assert.IsTrue(g1.rgba.SequenceEqual(g2.rgba), "grain regenerates identically");
            Assert.IsTrue(g1.tiles);
            var seam = 0.0; for (var y = 0; y < 128; y++) seam += Math.Abs(g1.rgba[(y * 128) * 4 + 3] - g1.rgba[(y * 128 + 127) * 4 + 3]);
            var inner = 0.0; for (var y = 0; y < 128; y++) inner += Math.Abs(g1.rgba[(y * 128 + 60) * 4 + 3] - g1.rgba[(y * 128 + 61) * 4 + 3]);
            Assert.Less(seam / 128, inner / 128 * 3 + 12, "the wrap-around edge is no rougher than any other neighbouring pair");
        }

        [Test] public void ColorHelpersBehave()
        {
            var c = ThemeColor.Hex(0xFF8000); Assert.AreEqual(1f, c.r, .001f); Assert.AreEqual(.502f, c.g, .01f); Assert.AreEqual(0f, c.b, .001f);
            Assert.AreEqual(.5f, ThemeColor.Lerp(ThemeColor.Black, ThemeColor.White, .5f).r, .001f);
            Assert.AreEqual(1f, ThemeColor.Lerp(ThemeColor.Black, ThemeColor.White, 7f).r, .001f, "lerp clamps");
            var over = ThemeImages.Over(new ThemeColor(1, 0, 0, 1), new ThemeColor(0, 0, 1, 1)); Assert.AreEqual(1f, over.r, .001f); Assert.AreEqual(1f, over.a, .001f);
            Assert.AreEqual(0f, ThemeImages.Over(ThemeColor.Clear, ThemeColor.Clear).a);
        }

        // ---------------------------------------------------------------- 9-slice and text effects
        [Test] public void NineSliceTilesTheDestinationAndTheImageExactlyWithNoGapsOrOverlap()
        {
            var rec = new Recorder(); var dest = new ThemeRect(10, 20, 300, 140);
            ThemeDraw.Slice9(rec, ThemeAssets.FrameGold, dest, 24, ThemeColor.White);
            Assert.AreEqual(9, rec.slices.Count);
            Assert.AreEqual(dest.w * dest.h, rec.slices.Sum(r => r.w * r.h), .5f, "destination pieces add up to the destination");
            Assert.AreEqual(1f, rec.uvs.Sum(r => r.w * r.h), .001f, "source pieces add up to the whole image");
            for (var i = 0; i < rec.slices.Count; i++) for (var j = i + 1; j < rec.slices.Count; j++)
            {
                var a = rec.slices[i]; var b = rec.slices[j];
                Assert.IsFalse(a.x < b.xMax - .001f && b.x < a.xMax - .001f && a.y < b.yMax - .001f && b.y < a.yMax - .001f, "pieces " + i + " and " + j + " overlap");
            }
            Assert.AreEqual(24f, rec.slices[0].w, .001f, "corners keep their size"); Assert.AreEqual(24f, rec.slices[0].h, .001f);
        }

        [Test] public void NineSliceShrinksItsCornersForARectangleSmallerThanTwoSlices()
        {
            var rec = new Recorder(); ThemeDraw.Slice9(rec, ThemeAssets.FrameGold, new ThemeRect(0, 0, 30, 20), 24, ThemeColor.White);
            Assert.AreEqual(30 * 20, rec.slices.Sum(r => r.w * r.h), .5f); foreach (var s in rec.slices) Assert.IsTrue(s.w >= 0 && s.h >= 0);
        }

        [Test] public void TextEffectsDrawTheRightNumberOfCopiesAndBalanceTheirClips()
        {
            var rec = new Recorder(); var r = new ThemeRect(0, 0, 400, 60);
            ThemeDraw.Text(rec, "HELLO", r, ThemeText.Of(ThemeFont.Label, 20, ThemeColor.White)); Assert.AreEqual(1, rec.texts, "plain text is one draw");
            rec = new Recorder(); var s = ThemeText.Of(ThemeFont.Label, 20, ThemeColor.White); s.shadowY = 3; s.shadowAlpha = .8f; ThemeDraw.Text(rec, "HELLO", r, s); Assert.AreEqual(3, rec.texts, "a shadow adds two copies");
            rec = new Recorder(); s = ThemeText.Of(ThemeFont.Label, 20, ThemeColor.White); s.outline = 1; ThemeDraw.Text(rec, "HELLO", r, s); Assert.AreEqual(9, rec.texts, "an outline adds eight");
            rec = new Recorder(); s = ThemeText.Of(ThemeFont.Heading, 38, ThemeColor.White); s.gradient = true; s.bottomColor = ThemeColor.Black; ThemeDraw.Text(rec, "HELLO", r, s);
            Assert.AreEqual(3, rec.texts, "metal text is three bands"); Assert.AreEqual(3, rec.pushes); Assert.AreEqual(rec.pushes, rec.pops);
            rec = new Recorder(); ThemeDraw.Text(rec, "", r, s); ThemeDraw.Text(rec, null, r, s); Assert.AreEqual(0, rec.texts + rec.pushes, "empty text draws nothing");
        }

        [Test] public void MetalTextBandsRunFromTheTopColourToTheBottomColour()
        {
            var rec = new Recorder(); var s = ThemeText.Of(ThemeFont.Heading, 38, ThemeColor.White); s.gradient = true; s.bottomColor = new ThemeColor(1, 0, 0, 1);
            ThemeDraw.Text(rec, "X", new ThemeRect(0, 0, 100, 50), s);
            Assert.AreEqual(1f, rec.drawn[0].color.g, .001f, "top band is the top colour"); Assert.AreEqual(0f, rec.drawn[2].color.g, .001f, "bottom band is the bottom colour");
            Assert.Greater(rec.drawn[1].color.g, 0f); Assert.Less(rec.drawn[1].color.g, 1f);
        }

        // ---------------------------------------------------------------- widgets
        [Test] public void ButtonsDrawInEveryStateAtAnySizeAndKeepClipsBalanced()
        {
            var states = new[] { ButtonVisual.Idle, new ButtonVisual { enabled = true, hover = 1 }, new ButtonVisual { enabled = true, hover = 1, press = 1 }, new ButtonVisual { enabled = true, selected = true, pulse = 1 }, new ButtonVisual { enabled = false }, new ButtonVisual { enabled = true, hover = 7, press = -3 } };
            foreach (var size in new[] { new ThemeRect(0, 0, 510, 58), new ThemeRect(0, 0, 20, 10), new ThemeRect(5, 5, 0, 0), new ThemeRect(0, 0, 3000, 400) })
                foreach (var st in states) { var rec = new Recorder(); ThemeWidgets.Button(rec, size, st); Assert.IsTrue(rec.images > 0); Assert.AreEqual(rec.pushes, rec.pops); }
        }

        [Test] public void ADisabledButtonIgnoresHoverAndUsesTheSteelFrame()
        {
            var rec = new Recorder(); ThemeWidgets.Button(rec, new ThemeRect(0, 0, 300, 50), new ButtonVisual { enabled = false, hover = 1, press = 1, selected = true });
            Assert.IsTrue(rec.calls.Any(c => c.Contains("frame.steel"))); Assert.IsFalse(rec.calls.Any(c => c.Contains("frame.gold")), "no gold frame and no hot glass on a disabled button");
            Assert.IsFalse(rec.calls.Contains("push"), "no sheen sweep either");
        }

        [Test] public void HoverBringsInTheGoldFrameAndTheSweepOnlyWhenNotPressed()
        {
            var idle = new Recorder(); ThemeWidgets.Button(idle, new ThemeRect(0, 0, 300, 50), ButtonVisual.Idle);
            var hover = new Recorder(); ThemeWidgets.Button(hover, new ThemeRect(0, 0, 300, 50), new ButtonVisual { enabled = true, hover = 1 });
            var press = new Recorder(); ThemeWidgets.Button(press, new ThemeRect(0, 0, 300, 50), new ButtonVisual { enabled = true, hover = 1, press = 1 });
            Assert.IsFalse(idle.calls.Any(c => c.Contains("frame.gold"))); Assert.IsTrue(hover.calls.Any(c => c.Contains("frame.gold")));
            Assert.AreEqual(1, hover.pushes, "the sheen sweep is clipped to the button"); Assert.AreEqual(0, press.pushes, "no sweep while pressed");
        }

        [Test] public void WidgetsAreDeterministicForTheSameStateAndClock()
        {
            Recorder Draw() { var r = new Recorder(); ThemeWidgets.Button(r, new ThemeRect(0, 0, 300, 50), new ButtonVisual { enabled = true, hover = .6f, selected = true, pulse = .3f }); ThemeWidgets.Panel(r, new ThemeRect(0, 0, 400, 300)); ThemeWidgets.Bar(r, new ThemeRect(0, 0, 200, 20), BarKind.Health, .4f, .7f, true); return r; }
            Assert.IsTrue(Draw().calls.SequenceEqual(Draw().calls), "the same state and clock draw the same calls in the same order");
        }

        [Test] public void BarsShowOnlyWhatIsThere()
        {
            var empty = new Recorder(); ThemeWidgets.Bar(empty, new ThemeRect(0, 0, 200, 20), BarKind.Health, 0f);
            var full = new Recorder(); ThemeWidgets.Bar(full, new ThemeRect(0, 0, 200, 20), BarKind.Health, 1f);
            Assert.IsFalse(empty.calls.Any(c => c.Contains("bar.health")), "an empty bar draws no fill"); Assert.IsTrue(full.calls.Any(c => c.Contains("bar.health")));
            var trail = new Recorder(); ThemeWidgets.Bar(trail, new ThemeRect(0, 0, 200, 20), BarKind.Health, .3f, .6f); Assert.Greater(trail.fills, empty.fills, "a recent loss adds the pale trail");
            foreach (var k in Enum.GetValues(typeof(BarKind)).Cast<BarKind>()) { var r = new Recorder(); ThemeWidgets.Bar(r, new ThemeRect(0, 0, 200, 20), k, 5f, -1f); Assert.IsTrue(r.images > 0, k.ToString()); }
        }

        [Test] public void TitlesDrawTheirTextAnOrnamentAndAnOptionalSubtitle()
        {
            var rec = new Recorder(); ThemeWidgets.Title(rec, new ThemeRect(0, 0, 800, 120), "SETTINGS", "SHAPE THE VAULT", 40);
            Assert.IsTrue(rec.drawn.Any(d => d.text == "SETTINGS")); Assert.IsTrue(rec.drawn.Any(d => d.text == "SHAPE THE VAULT")); Assert.IsTrue(rec.calls.Contains("img:diamond.gold"));
            var none = new Recorder(); ThemeWidgets.Title(none, new ThemeRect(0, 0, 800, 120), "ONLY", null, 40); Assert.IsFalse(none.drawn.Any(d => d.text == null));
            Assert.AreEqual(none.pushes, none.pops);
        }

        [Test] public void PanelsTooltipsRowsAndStripsDrawWithoutLeavingClipsOpen()
        {
            var rec = new Recorder(); var r = new ThemeRect(0, 0, 360, 120);
            ThemeWidgets.Panel(rec, r, FrameKind.Violet, 0f, .5f); ThemeWidgets.Plaque(rec, r); ThemeWidgets.Well(rec, r); ThemeWidgets.Row(rec, r, 1f); ThemeWidgets.Row(rec, r, 0f);
            ThemeWidgets.HudStrip(rec, new ThemeRect(0, 0, 1920, 58), true); ThemeWidgets.Tooltip(rec, r, "WEAK", "Deals 25% less attack damage."); ThemeWidgets.Tooltip(rec, r, null, "No heading.");
            ThemeWidgets.Slider(rec, new ThemeRect(0, 0, 300, 20), 1.4f, true); ThemeWidgets.Divider(rec, 100, 50, 300); ThemeWidgets.Plate(rec, r); ThemeWidgets.Glow(rec, 0, 0, 40, ThemeColor.White);
            Assert.AreEqual(rec.pushes, rec.pops); Assert.IsTrue(rec.images > 20);
        }

        [Test] public void EveryNamedPaletteColorIsOpaqueAndInRange()
        {
            foreach (var f in typeof(ThemePalette).GetFields()) { var c = (ThemeColor)f.GetValue(null); Assert.AreEqual(1f, c.a, f.Name); Assert.IsTrue(c.r >= 0 && c.r <= 1 && c.g >= 0 && c.g <= 1 && c.b >= 0 && c.b <= 1, f.Name); }
        }
    }
}
