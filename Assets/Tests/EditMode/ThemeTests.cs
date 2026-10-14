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
            public readonly List<(float x0, float y0, float x1, float y1, float w, ThemeColor c)> lines = new List<(float, float, float, float, float, ThemeColor)>();
            public void Line(float x0, float y0, float x1, float y1, float w, ThemeColor c) { lines.Add((x0, y0, x1, y1, w, c)); calls.Add("line"); }
        }

        static IEnumerable<ThemeImage> AllAssets() => new[]
        {
            ThemeAssets.FrameGold, ThemeAssets.FrameBronze, ThemeAssets.FrameSteel, ThemeAssets.FrameCrimson, ThemeAssets.FrameAzure, ThemeAssets.FrameViolet, ThemeAssets.FrameJade,
            ThemeAssets.RimBronze, ThemeAssets.RimGold, ThemeAssets.RimSteel, ThemeAssets.Shadow, ThemeAssets.Glow, ThemeAssets.GlowTight, ThemeAssets.Ring, ThemeAssets.Vignette,
            ThemeAssets.Grain, ThemeAssets.Sheen, ThemeAssets.DiamondGold, ThemeAssets.PanelGlass, ThemeAssets.ButtonGlass, ThemeAssets.ButtonGlassHot, ThemeAssets.ButtonGlassPressed,
            ThemeAssets.TopSheen, ThemeAssets.RuleFade, ThemeAssets.BarGloss, ThemeAssets.BarTrough, ThemeAssets.PlateFade, ThemeAssets.HudGlass, ThemeAssets.RowGlass, ThemeAssets.ShadeDown, ThemeAssets.LineSoft, ThemeAssets.Medallion, ThemeAssets.Flare, ThemeAssets.RimNeutral, ThemeAssets.HexPlate,
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


        // ---------------------------------------------------------------- act map
        [Test] public void ThreadPointsKeepTheirEndsFixedAndBendGentlyBetween()
        {
            ThemeMap.ThreadPoint(100, 400, 300, 100, 0f, out var x0, out var y0); ThemeMap.ThreadPoint(100, 400, 300, 100, 1f, out var x1, out var y1);
            Assert.AreEqual(100.0, x0, .001); Assert.AreEqual(400.0, y0, .001); Assert.AreEqual(300.0, x1, .001); Assert.AreEqual(100.0, y1, .001);
            ThemeMap.ThreadPoint(100, 400, 300, 100, .5f, out var xm, out var ym);
            Assert.AreEqual(250.0, ym, .001); Assert.AreEqual(212.0, xm, .01, "200 units across bends 16, which is capped at 12, at the middle of the thread");
            ThemeMap.ThreadPoint(100, 400, 100, 100, .5f, out var xs, out _); Assert.AreEqual(100.0, xs, .001, "a straight vertical thread does not bend");
        }

        [Test] public void EveryThreadKindDrawsAndAvailableThreadsAreBrighterThanFutureOnes()
        {
            double Alpha(ThreadKind k) { var r = new Recorder(); ThemeMap.Thread(r, 100, 500, 260, 380, k); Assert.IsTrue(r.lines.Count > 0, k.ToString()); return r.lines.Max(l => (double)l.c.a * l.w); }
            Assert.Greater(Alpha(ThreadKind.Available), Alpha(ThreadKind.Future));
            foreach (var k in Enum.GetValues(typeof(ThreadKind)).Cast<ThreadKind>())
            {
                var r = new Recorder(); ThemeMap.Thread(r, 0, 0, 0, 0, k, 1f, 3); Assert.IsFalse(r.lines.Any(l => float.IsNaN(l.x1) || float.IsNaN(l.y1)), k + " survives a zero-length edge");
            }
        }

        [Test] public void ThreadProgressDrawsOnlyThatFractionAndNothingAtZero()
        {
            var none = new Recorder(); ThemeMap.Thread(none, 100, 500, 100, 100, ThreadKind.Traveled, 0f); Assert.AreEqual(0, none.lines.Count);
            var half = new Recorder(); ThemeMap.Thread(half, 100, 500, 100, 100, ThreadKind.Traveled, .5f);
            Assert.IsTrue(half.lines.Count > 0); Assert.IsTrue(half.lines.All(l => l.y1 >= 299.9f), "a half-drawn thread has not gone past its midpoint (y grows downward)");
            var full = new Recorder(); ThemeMap.Thread(full, 100, 500, 100, 100, ThreadKind.Traveled, 1f); Assert.Greater(full.lines.Count, half.lines.Count);
        }

        [Test] public void MotesDriftOnlyOnAvailableThreadsAndOnlyWhenAsked()
        {
            var plain = new Recorder(); ThemeMap.Thread(plain, 100, 500, 300, 100, ThreadKind.Available, 1f, 0);
            var moving = new Recorder(); ThemeMap.Thread(moving, 100, 500, 300, 100, ThreadKind.Available, 1f, 3);
            Assert.Greater(moving.images, plain.images, "motes are glow images"); Assert.Greater(moving.fills, plain.fills);
            var quiet = new Recorder(); ThemeMap.Thread(quiet, 100, 500, 300, 100, ThreadKind.Future, 1f, 3); Assert.AreEqual(0, quiet.images, "a future thread has no motes");
        }

        [Test] public void RoomLabelsSitOnAPlateAndLitOnesAreGold()
        {
            var lit = new Recorder(); ThemeMap.RoomLabel(lit, 300, 200, "COMBAT · HERE", true);
            var dim = new Recorder(); ThemeMap.RoomLabel(dim, 300, 200, "COMBAT", false);
            Assert.IsTrue(lit.calls.Contains("img:plate.fade")); Assert.IsTrue(lit.drawn.Any(d => d.text == "COMBAT · HERE" && d.color.r > .9f && d.color.b < .8f), "lit labels use the gold");
            Assert.IsTrue(dim.drawn.Any(d => d.text == "COMBAT")); Assert.AreEqual(lit.pushes, lit.pops);
        }

        [Test] public void MedallionIsARoundSocketClearAtTheCornersAndOpaqueOnItsRim()
        {
            var m = ThemeAssets.Medallion; Assert.AreEqual(0, (int)m.rgba[3], "the corner is clear");
            Assert.Greater(m.Get(m.width / 2, (int)(m.height * .06f)).a, .95f, "the rim is solid"); Assert.Greater(m.Get(m.width / 2, m.height / 2).a, .8f, "the glass is mostly opaque so the emblem stands out");
            float Lum(ThemeColor c) => c.r * .3f + c.g * .59f + c.b * .11f;
            Assert.Greater(Lum(m.Get((int)(m.width * .22f), (int)(m.height * .22f))), Lum(m.Get((int)(m.width * .78f), (int)(m.height * .78f))), "lit from the upper left");
        }

        [Test] public void FlareIsBrightestAtTheCentreAndFadesAlongTwoCrossedStreaks()
        {
            var f = ThemeAssets.Flare; var mid = f.width / 2;
            Assert.Greater(f.Get(mid, mid).a, .9f); Assert.Less(f.Get((int)(f.width * .2f), (int)(f.height * .2f)).a, .05f, "the diagonals are dark");
            Assert.Greater(f.Get((int)(f.width * .2f), mid).a, f.Get((int)(f.width * .2f), (int)(f.height * .3f)).a, "the streak runs along the axis");
        }

        [Test] public void EveryRoomToneHasADistinctColourCast()
        {
            var seen = new HashSet<string>();
            foreach (var t in Enum.GetValues(typeof(NodeTone)).Cast<NodeTone>()) { var c = ThemeMap.ToneColor(t); Assert.AreEqual(1f, c.a); Assert.IsTrue(seen.Add(c.r + "," + c.g + "," + c.b), t.ToString()); }
        }

        [Test] public void RoomsDrawUnderAndOverTheirEmblemInEveryStateAndMotionLevel()
        {
            foreach (var st in Enum.GetValues(typeof(NodeState)).Cast<NodeState>())
                foreach (var motion in new[] { 0, 1, 2 })
                    foreach (var tone in Enum.GetValues(typeof(NodeTone)).Cast<NodeTone>())
                    {
                        var u = new Recorder(); ThemeMap.NodeUnder(u, 300, 300, 78, tone, st, .5f, 7, motion);
                        Assert.IsTrue(u.calls.Contains("img:medallion"), st + "/" + tone + " sets the emblem in a medallion"); Assert.IsTrue(u.calls.Contains("img:glow.soft"));
                        var o = new Recorder(); ThemeMap.NodeOver(o, 300, 300, 78, tone, st, .5f, 7, motion);
                        Assert.IsFalse(o.lines.Any(l => float.IsNaN(l.x0) || float.IsNaN(l.x1) || float.IsNaN(l.y0) || float.IsNaN(l.y1)));
                        Assert.AreEqual(u.pushes, u.pops); Assert.AreEqual(o.pushes, o.pops);
                    }
        }

        [Test] public void LiveRoomsGlowBrighterAndReceiveARingWhileLockedOnesDoNot()
        {
            var locked = new Recorder(); ThemeMap.NodeOver(locked, 300, 300, 78, NodeTone.Combat, NodeState.Locked, 0f, 1, 2);
            var open = new Recorder(); ThemeMap.NodeOver(open, 300, 300, 78, NodeTone.Combat, NodeState.Available, 0f, 1, 2);
            var here = new Recorder(); ThemeMap.NodeOver(here, 300, 300, 78, NodeTone.Combat, NodeState.Current, 0f, 1, 2);
            Assert.AreEqual(0, locked.images + locked.lines.Count, "a room you cannot reach yet adds nothing over its emblem until the pointer is on it");
            Assert.Greater(open.lines.Count, 0, "turning ticks"); Assert.IsTrue(open.calls.Contains("img:ring.glow")); Assert.IsTrue(here.calls.Contains("img:ring.glow"));
            var still = new Recorder(); ThemeMap.NodeOver(still, 300, 300, 78, NodeTone.Combat, NodeState.Available, 0f, 1, 0); Assert.Less(still.images, open.images, "no ripples when motion is off");
        }

        [Test] public void ReduceMotionFreezesEmbersAndFullMotionMovesThem()
        {
            var a = new Recorder { time = 0f }; var b = new Recorder { time = 0f }; var area = new ThemeRect(0, 0, 1500, 900);
            ThemeMap.Embers(a, area, 30, 0); ThemeMap.Embers(b, area, 30, 0); Assert.AreEqual(a.calls.Count, b.calls.Count);
            var later = new Recorder { time = 4f }; ThemeMap.Embers(later, area, 30, 0); Assert.AreEqual(a.calls.Count, later.calls.Count, "the same number of motes at any time");
            Assert.AreEqual(30 * 2, a.images + a.fills, "each mote is a glow and a bright core");
            var none = new Recorder(); ThemeMap.Embers(none, area, 0, 0); Assert.AreEqual(0, none.calls.Count);
        }

        [Test] public void LightShaftsStayClippedAndHeadersCarryTheirText()
        {
            var sh = new Recorder(); ThemeMap.LightShafts(sh, new ThemeRect(0, 58, 1500, 900), 1f); Assert.AreEqual(1, sh.pushes); Assert.AreEqual(1, sh.pops); Assert.AreEqual(4, sh.lines.Count);
            var off = new Recorder(); ThemeMap.LightShafts(off, new ThemeRect(0, 58, 1500, 900), 0f); Assert.IsTrue(off.lines.All(l => l.c.a == 0f), "strength 0 draws nothing visible");
            var hd = new Recorder(); ThemeMap.Header(hd, 700, 90, "CHOOSE YOUR NEXT ROOM", true); Assert.IsTrue(hd.drawn.Any(d => d.text == "CHOOSE YOUR NEXT ROOM")); Assert.AreEqual(2, hd.calls.Count(c => c == "img:rule.fade"));
        }

        [Test] public void TheSummitCrownDrawsAuraMedallionAndRingsWithoutRipplesWhenStill()
        {
            var full = new Recorder(); ThemeMap.BossCrown(full, new ThemeRect(1713, 106, 126, 132), 2); var still = new Recorder(); ThemeMap.BossCrown(still, new ThemeRect(1713, 106, 126, 132), 0);
            Assert.IsTrue(full.calls.Contains("img:medallion")); Assert.Greater(full.images, still.images); Assert.AreEqual(full.pushes, full.pops);
        }

        // ---------------------------------------------------------------- combat pieces
        [Test] public void HexPlateIsAPointedHexagonClearAtTheCornersAndLitFromTheUpperLeft()
        {
            var h = ThemeAssets.HexPlate; Assert.AreEqual(0, (int)h.Get(1, 1).a * 255 / 255, "the top-left corner is cut away");
            Assert.Less(h.Get(2, 2).a, .05f); Assert.Less(h.Get(h.width - 3, 2).a, .05f); Assert.Less(h.Get(2, h.height - 3).a, .05f);
            Assert.Greater(h.Get(h.width / 2, 3).a, .9f, "the top edge is solid"); Assert.Greater(h.Get(3, h.height / 2).a, .9f, "the left point reaches the middle of the left side");
            float Lum(ThemeColor c) => c.r * .3f + c.g * .59f + c.b * .11f;
            Assert.Greater(Lum(h.Get(h.width / 2, 4)), Lum(h.Get(h.width / 2, h.height - 5)), "the top bevel is lit and the bottom one is in shade");
        }

        [Test] public void NeutralRimTintsToAnyColourWithoutBeingTintedItself()
        {
            var r = ThemeAssets.RimNeutral; var c = r.Get(r.width / 2, 3);
            Assert.IsTrue(Math.Abs(c.r - c.g) < .02f && Math.Abs(c.g - c.b) < .02f, "the rim itself is grey, so a tint gives the screen's colour");
        }

        [Test] public void EndTurnPlateBreathesWhenIdleDimsWhenDisabledAndOmitsTheLeftStudForPadGlyphs()
        {
            var r = new ThemeRect(0, 0, 340, 76);
            var ready = new Recorder(); ThemeWidgets.EndTurnPlate(ready, r, true, 0f, 0f); var idle = new Recorder(); ThemeWidgets.EndTurnPlate(idle, r, true, 0f, 1f); var off = new Recorder(); ThemeWidgets.EndTurnPlate(off, r, false, 1f, 1f);
            Assert.IsTrue(ready.calls.Contains("img:plate.hex")); Assert.AreEqual(ready.pushes, ready.pops);
            Assert.Greater(idle.images, 0); Assert.Less(off.images, ready.images, "a dimmed plate draws no hot glow");
            var noStud = new Recorder(); ThemeWidgets.EndTurnPlate(noStud, r, true, 0f, 0f, false); Assert.AreEqual(ready.calls.Count(c => c == "img:diamond.gold") - 1, noStud.calls.Count(c => c == "img:diamond.gold"));
        }

        [Test] public void BarsGetTenthMarksAndAnOrangeLostSliceAndOnlyOnWideBars()
        {
            var wide = new Recorder(); ThemeWidgets.Bar(wide, new ThemeRect(0, 0, 300, 22), BarKind.Health, 1f); var narrow = new Recorder(); ThemeWidgets.Bar(narrow, new ThemeRect(0, 0, 90, 22), BarKind.Health, 1f);
            Assert.Greater(wide.fills, narrow.fills, "tick marks only on a bar wide enough to show a scale");
            var hurt = new Recorder(); ThemeWidgets.Bar(hurt, new ThemeRect(0, 0, 300, 22), BarKind.Health, .4f, .7f); Assert.AreEqual(wide.fills > 0 ? 1 : 0, 1);
            Assert.AreEqual(hurt.pushes, hurt.pops);
        }

        [Test] public void BlockBadgeAndOutlineDrawTheirImages()
        {
            var b = new Recorder(); ThemeWidgets.BlockBadge(b, new ThemeRect(0, 0, 46, 46), .5f); Assert.IsTrue(b.calls.Contains("img:medallion")); Assert.IsTrue(b.calls.Contains("img:ring.glow"));
            var o = new Recorder(); ThemeWidgets.Outline(o, new ThemeRect(0, 0, 200, 100), new ThemeColor(.9f, .6f, .2f, 1f), 2); Assert.IsTrue(o.calls.Any(c => c.Contains("rim.neutral")));
            var tiny = new Recorder(); ThemeWidgets.Outline(tiny, new ThemeRect(0, 0, 9, 9), ThemeColor.White, 6); Assert.AreEqual(tiny.pushes, tiny.pops);
        }

        [Test] public void EveryNamedPaletteColorIsOpaqueAndInRange()
        {
            foreach (var f in typeof(ThemePalette).GetFields()) { var c = (ThemeColor)f.GetValue(null); Assert.AreEqual(1f, c.a, f.Name); Assert.IsTrue(c.r >= 0 && c.r <= 1 && c.g >= 0 && c.g <= 1 && c.b >= 0 && c.b <= 1, f.Name); }
        }
    }
}
