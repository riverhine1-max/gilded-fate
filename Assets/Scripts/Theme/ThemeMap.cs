using System;

namespace GildedFate.Theme
{
    /// <summary>How a link between two rooms on the act map is lit.</summary>
    public enum ThreadKind
    {
        /// <summary>A route not yet reachable. Quiet, but readable over the backdrop.</summary>
        Future,
        /// <summary>A route touching the room under the pointer.</summary>
        Reachable,
        /// <summary>A route you can take right now. Bright, with motes drifting along it.</summary>
        Available,
        /// <summary>A route already walked: a warm gold cable.</summary>
        Traveled,
    }

    /// <summary>The kind of room, in the same order as the game's NodeKind, so the game can cast. It sets the room's colour cast.</summary>
    public enum NodeTone { Combat, Elite, Event, Treasure, Merchant, Sanctuary, Boss }

    /// <summary>How a room stands: not reachable yet, already cleared, one of the rooms you can enter now, or the room you are standing in.</summary>
    public enum NodeState { Locked, Cleared, Available, Current }

    /// <summary>Widgets for the act map. Pure functions of their inputs and the canvas clock, like the rest of the theme.</summary>
    public static class ThemeMap
    {
        static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
        static float Frac(float v) => v - (float)Math.Floor(v);

        /// <summary>
        /// The point t (0..1) along the thread from a to b. The ends are fixed; the gentle sideways bend matches the one the map's hit-testing and
        /// travel animation already use, so a thread, its drifting motes and the travelling spark all follow the same path.
        /// </summary>
        public static void ThreadPoint(float ax, float ay, float bx, float by, float t, out float x, out float y)
        {
            var bend = (bx - ax) * .08f; bend = bend < -12 ? -12 : bend > 12 ? 12 : bend;
            x = ax + (bx - ax) * t + (float)Math.Sin(t * Math.PI) * bend; y = ay + (by - ay) * t;
        }

        /// <summary>
        /// A glowing link between two rooms. progress (0..1) draws only that fraction, for the travel animation. motes is how many sparks
        /// drift along an Available thread (0 for none; the caller passes 0 when motion is reduced).
        /// </summary>
        public static void Thread(IThemeCanvas c, float ax, float ay, float bx, float by, ThreadKind kind, float progress = 1f, int motes = 0)
        {
            progress = Clamp01(progress); if (progress <= 0) return;
            float glowW, bodyW, coreW; ThemeColor glow, body, core;
            var breath = .5f + .5f * (float)Math.Sin(c.Time * 2.2f + ax * .013f);
            switch (kind)
            {
                case ThreadKind.Available:
                    glowW = 24; bodyW = 5.2f; coreW = 2.4f;
                    glow = ThemePalette.Ember.WithAlpha(.30f + .14f * breath); body = ThemePalette.GoldBright.WithAlpha(.97f); core = new ThemeColor(1f, .97f, .86f, .95f); break;
                case ThreadKind.Reachable:
                    glowW = 18; bodyW = 4.2f; coreW = 1.9f;
                    glow = ThemePalette.Ember.WithAlpha(.24f); body = ThemePalette.Gold.WithAlpha(.92f); core = new ThemeColor(1f, .94f, .76f, .70f); break;
                case ThreadKind.Traveled:
                    glowW = 18; bodyW = 4.4f; coreW = 2f;
                    glow = ThemePalette.Gold.WithAlpha(.22f); body = ThemePalette.GoldDeep.WithAlpha(.97f); core = ThemePalette.GoldBright.WithAlpha(.62f); break;
                default:
                    glowW = 10; bodyW = 3.2f; coreW = 0;
                    glow = new ThemeColor(.66f, .54f, .34f, .14f); body = new ThemeColor(.78f, .66f, .46f, .66f); core = ThemeColor.Clear; break;
            }

            const int steps = 6; float px = ax, py = ay;
            for (var pass = 0; pass < 3; pass++)
            {
                var w = pass == 0 ? glowW : pass == 1 ? bodyW : coreW; var col = pass == 0 ? glow : pass == 1 ? body : core;
                if (w <= 0 || col.a <= .001f) continue;
                px = ax; py = ay;
                for (var i = 1; i <= steps; i++)
                {
                    var t = Math.Min(progress, i / (float)steps); ThreadPoint(ax, ay, bx, by, t, out var nx, out var ny);
                    c.Line(px, py, nx, ny, w, col); px = nx; py = ny;
                    if (t >= progress) break;
                }
            }

            if (kind == ThreadKind.Available && motes > 0)
                for (var i = 0; i < motes; i++)
                {
                    var f = Frac(c.Time * .34f + i / (float)motes + ax * .0017f); if (f > progress) continue;
                    ThreadPoint(ax, ay, bx, by, f, out var mx, out var my); var fade = (float)Math.Sin(f * Math.PI);
                    ThemeWidgets.Glow(c, mx, my, 9, ThemePalette.GoldBright.WithAlpha(.85f * fade));
                    c.Fill(new ThemeRect(mx - 1.5f, my - 1.5f, 3, 3), new ThemeColor(1f, .98f, .88f, .95f * fade));
                }
        }

        // ---------------------------------------------------------------- rooms
        static float Smooth(float a, float b, float v) { var t = Clamp01((v - a) / (b - a)); return t * t * (3 - 2 * t); }
        static float Hash(int n) => Frac((float)Math.Sin(n * 12.9898 + 78.233) * 43758.5453f);

        /// <summary>Each room type's colour cast: red for combat, deep red for elites, violet for events, gold, jade, azure and ember.</summary>
        public static ThemeColor ToneColor(NodeTone tone)
        {
            switch (tone)
            {
                case NodeTone.Elite: return ThemeColor.Hex(0xE0455F);
                case NodeTone.Event: return ThemeColor.Hex(0xA67CF0);
                case NodeTone.Treasure: return ThemeColor.Hex(0xF5C84C);
                case NodeTone.Merchant: return ThemeColor.Hex(0x3FCFA0);
                case NodeTone.Sanctuary: return ThemeColor.Hex(0x66C2FF);
                case NodeTone.Boss: return ThemeColor.Hex(0xFF5A2E);
                default: return ThemeColor.Hex(0xD6453A);
            }
        }

        static float Breath(IThemeCanvas c, int seed, int motion) => motion == 0 ? .5f : .5f + .5f * (float)Math.Sin(c.Time * 2.3f + seed * .9f);

        /// <summary>A ring of short ticks that slowly turns. Drawn with soft lines so the ticks glow rather than saw at their edges.</summary>
        static void Ticks(IThemeCanvas c, float cx, float cy, float radius, int count, float spin, float lengthShort, float lengthLong, float width, ThemeColor color)
        {
            for (var i = 0; i < count; i++)
            {
                var a = spin + i * (float)Math.PI * 2 / count; var dx = (float)Math.Cos(a); var dy = (float)Math.Sin(a); var len = i % 4 == 0 ? lengthLong : lengthShort;
                c.Line(cx + dx * radius, cy + dy * radius, cx + dx * (radius + len), cy + dy * (radius + len), width, color);
            }
        }

        /// <summary>
        /// Everything that sits behind a room's emblem: a grounding shadow, the room's colour as an aura, and a bevelled medallion socket the
        /// emblem is set into so it stands out from the busy backdrop. hover is 0..1 (eased by the caller). motion: 0 still, 1 reduced, 2 full.
        /// The emblem itself is the game's own art and is drawn by the game between NodeUnder and NodeOver.
        /// </summary>
        public static void NodeUnder(IThemeCanvas c, float cx, float cy, float size, NodeTone tone, NodeState state, float hover, int seed, int motion)
        {
            hover = Clamp01(hover); var tc = ToneColor(tone); var breath = Breath(c, seed, motion); var live = state == NodeState.Available || state == NodeState.Current;
            ThemeWidgets.Glow(c, cx, cy + size * .06f, size * .68f, new ThemeColor(0, 0, 0, live ? .64f : .52f));
            var aura = state == NodeState.Current ? .44f : state == NodeState.Available ? .28f + .14f * breath : state == NodeState.Cleared ? .10f : .13f;
            ThemeWidgets.Glow(c, cx, cy, size * ((state == NodeState.Current ? .98f : .80f) + (live ? .06f * breath : 0f) + .10f * hover), tc.WithAlpha(Clamp01(aura + .20f * hover)));
            if (state == NodeState.Current && motion > 0)
            {
                // Slow rays turning behind the emblem mark where you stand.
                var n = motion == 1 ? 6 : 10;
                for (var i = 0; i < n; i++)
                {
                    var a = c.Time * .10f + i * (float)Math.PI * 2 / n; var dx = (float)Math.Cos(a); var dy = (float)Math.Sin(a);
                    c.Line(cx + dx * size * .45f, cy + dy * size * .45f, cx + dx * size * 1.55f, cy + dy * size * 1.55f, 16, ThemePalette.GoldBright.WithAlpha(.07f + .03f * breath));
                }
            }
            var disc = size * (1.02f + .05f * hover);
            var tint = state == NodeState.Cleared ? new ThemeColor(.64f, .62f, .60f, 1f) : state == NodeState.Locked ? new ThemeColor(.84f, .86f, .92f, .94f) : ThemeColor.White;
            c.Image(ThemeAssets.Medallion, new ThemeRect(cx - disc * .5f, cy - disc * .5f, disc, disc), tint);
            ThemeWidgets.Glow(c, cx, cy, disc * .46f, tc.WithAlpha(state == NodeState.Locked ? .10f : .24f));
        }

        /// <summary>What sits over a room's emblem: the ring, turning ticks and ripples of a room you can enter, the beacon of the one you stand in, and the glint on cleared ones.</summary>
        public static void NodeOver(IThemeCanvas c, float cx, float cy, float size, NodeTone tone, NodeState state, float hover, int seed, int motion)
        {
            hover = Clamp01(hover); var tc = ToneColor(tone); var breath = Breath(c, seed, motion); var t = motion == 0 ? 0f : c.Time;
            if (state == NodeState.Available)
            {
                var d = size * (1.26f + .035f * breath + .06f * hover);
                c.Image(ThemeAssets.Ring, new ThemeRect(cx - d * .5f, cy - d * .5f, d, d), ThemePalette.GoldBright.WithAlpha(Clamp01(.55f + .30f * breath + .15f * hover)));
                Ticks(c, cx, cy, size * .66f, hover > .3f ? 16 : 12, t * .35f, 4f, 9f, 2.6f, ThemePalette.GoldBright.WithAlpha(.55f + .4f * hover));
                if (motion > 0)
                    for (var k = 0; k < 2; k++)
                    {
                        var p = Frac(t * .32f + k * .5f + seed * .07f); var rd = size * (1.1f + .9f * p);
                        c.Image(ThemeAssets.Ring, new ThemeRect(cx - rd * .5f, cy - rd * .5f, rd, rd), tc.WithAlpha((1 - p) * (1 - p) * .50f));
                    }
            }
            else if (state == NodeState.Current)
            {
                var d = size * 1.24f;
                c.Image(ThemeAssets.Ring, new ThemeRect(cx - d * .5f, cy - d * .5f, d, d), ThemePalette.GoldBright.WithAlpha(.95f));
                Ticks(c, cx, cy, size * .67f, 16, -t * .22f, 3f, 8f, 2.4f, ThemePalette.GoldBright.WithAlpha(.85f));
                if (motion > 0)
                    for (var k = 0; k < 2; k++)
                    {
                        var p = Frac(t * .28f + k * .5f); var rd = size * (1.2f + 1.3f * p);
                        c.Image(ThemeAssets.Ring, new ThemeRect(cx - rd * .5f, cy - rd * .5f, rd, rd), ThemePalette.GoldBright.WithAlpha((1 - p) * (1 - p) * .55f));
                    }
            }
            else if (state == NodeState.Cleared)
            {
                // A small gold glint on the lower right, like a seal catching the light.
                var glint = motion == 0 ? .35f : (float)Math.Pow(Math.Max(0, Math.Sin(t * .8f + seed * 1.3f)), 6);
                var fs = 16 + 12 * glint; var fx = cx + size * .30f; var fy = cy + size * .30f;
                c.Image(ThemeAssets.Flare, new ThemeRect(fx - fs, fy - fs, fs * 2, fs * 2), ThemePalette.GoldBright.WithAlpha(.55f + .40f * glint));
            }
            else if (hover > .01f)
            {
                var d = size * 1.16f;
                c.Image(ThemeAssets.Ring, new ThemeRect(cx - d * .5f, cy - d * .5f, d, d), ThemePalette.Gold.WithAlpha(.45f * hover));
            }
        }

        /// <summary>
        /// Slow gold-and-ember motes rising through the vault. Deterministic from the clock: the same time gives the same sky, so a still is
        /// repeatable and Reduce Motion (a held clock) leaves them frozen in place. scroll drifts them a little so they move with the map.
        /// </summary>
        public static void Embers(IThemeCanvas c, ThemeRect area, int count, float scroll)
        {
            for (var i = 0; i < count; i++)
            {
                var h1 = Hash(i * 3 + 1); var h2 = Hash(i * 3 + 2); var h3 = Hash(i * 3 + 3);
                var speed = 9f + 24f * h1; var travel = area.h * 1.1f;
                var rise = Frac((c.Time * speed + h3 * travel + scroll * (.12f + .35f * h1)) / travel);    // 0 at the bottom, 1 at the top
                var x = area.x + h2 * area.w + (float)Math.Sin(c.Time * (.35f + h3 * .5f) + i * 1.7f) * 20f; var y = area.yMax - rise * area.h;
                var life = Smooth(0, .12f, rise) * (1 - Smooth(.65f, 1f, rise)); var flicker = .6f + .4f * (float)Math.Sin(c.Time * (2f + 4f * h1) + i);
                var size = 3.5f + 6f * h2 * h2; var cool = h3 > .86f;
                var col = cool ? new ThemeColor(.55f, .78f, 1f, 1f) : ThemeColor.Lerp(ThemePalette.Ember, ThemePalette.GoldBright, h2);
                ThemeWidgets.Glow(c, x, y, size * 2.4f, col.WithAlpha(.55f * life * flicker));
                c.Fill(new ThemeRect(x - size * .22f, y - size * .22f, size * .44f, size * .44f), new ThemeColor(1f, .96f, .86f, .9f * life * flicker));
            }
        }

        /// <summary>A few very soft angled beams of light falling through the vault, swaying slowly. Clipped to the area.</summary>
        public static void LightShafts(IThemeCanvas c, ThemeRect area, float strength)
        {
            c.PushClip(area);
            try
            {
                for (var k = 0; k < 4; k++)
                {
                    var sway = (float)Math.Sin(c.Time * .11f + k * 1.9f) * 70f; var x0 = area.x + area.w * (.12f + k * .26f) + sway; var width = 150f + 60f * (k % 2);
                    var a = .045f + .02f * (float)Math.Sin(c.Time * .23f + k * 2.3f);
                    c.Line(x0, area.y - 40, x0 - area.h * .42f, area.yMax + 40, width, ThemeColor.Lerp(ThemePalette.GoldBright, new ThemeColor(.6f, .75f, 1f, 1f), (k % 2) * .6f).WithAlpha(Math.Max(0, a) * strength));
                }
            }
            finally { c.PopClip(); }
        }

        /// <summary>The heading above the map: gold text on a plate with a fading gold rule either side. ready makes it breathe.</summary>
        public static void Header(IThemeCanvas c, float cx, float cy, string text, bool ready)
        {
            const float size = 14f; var width = c.MeasureText(text, ThemeFont.Label, size) + 70f;
            var r = new ThemeRect(cx - width * .5f, cy - 14, width, 28);
            if (ready) ThemeWidgets.Glow(c, cx, cy, width * .42f, ThemePalette.Ember.WithAlpha(.10f + .06f * (float)Math.Sin(c.Time * 2f)));
            ThemeWidgets.Plate(c, r, .96f);
            c.Image(ThemeAssets.RuleFade, new ThemeRect(r.x - 120, cy - 1, 130, 2), ThemePalette.GoldBright.WithAlpha(.9f));
            c.Image(ThemeAssets.RuleFade, new ThemeRect(r.xMax - 10, cy - 1, 130, 2), ThemePalette.GoldBright.WithAlpha(.9f));
            var t = ThemeText.Of(ThemeFont.Label, size, ready ? ThemePalette.GoldBright : ThemePalette.TextDim, ThemeAnchor.MiddleCenter); t.shadowY = 1.5f; t.shadowAlpha = .9f;
            ThemeDraw.Text(c, text, r, t);
        }

        /// <summary>A plate behind text drawn by the game (the boss column's labels), sized to a given width.</summary>
        public static void LabelPlate(IThemeCanvas c, ThemeRect r) => ThemeWidgets.Plaque(c, r, .96f);

        /// <summary>The summit: a large ember aura, a crimson-lit medallion behind the boss portrait, a ring of ticks and slow ripples. The portrait is the game's own art.</summary>
        public static void BossCrown(IThemeCanvas c, ThemeRect r, int motion)
        {
            var tc = ToneColor(NodeTone.Boss); var breath = Breath(c, 3, motion); var t = motion == 0 ? 0f : c.Time; var cx = r.cx; var cy = r.cy;
            ThemeWidgets.Glow(c, cx, cy, r.w * (.98f + .06f * breath), tc.WithAlpha(.34f + .16f * breath));
            ThemeWidgets.Glow(c, cx, cy, r.w * .62f, ThemePalette.Gold.WithAlpha(.10f + .08f * breath));
            var d = r.w * 1.02f; c.Image(ThemeAssets.Medallion, new ThemeRect(cx - d * .5f, cy - d * .5f, d, d), ThemeColor.White);
            ThemeWidgets.Glow(c, cx, cy, d * .46f, tc.WithAlpha(.30f));
            var ring = r.w * 1.18f; c.Image(ThemeAssets.Ring, new ThemeRect(cx - ring * .5f, cy - ring * .5f, ring, ring), new ThemeColor(1f, .62f, .30f, .8f + .15f * breath));
            Ticks(c, cx, cy, r.w * .60f, 24, t * .18f, 4f, 10f, 2.6f, new ThemeColor(1f, .72f, .36f, .8f));
            if (motion > 0)
                for (var k = 0; k < 2; k++)
                {
                    var p = Frac(t * .24f + k * .5f); var rd = r.w * (1.1f + .9f * p);
                    c.Image(ThemeAssets.Ring, new ThemeRect(cx - rd * .5f, cy - rd * .5f, rd, rd), tc.WithAlpha((1 - p) * (1 - p) * .55f));
                }
        }

        /// <summary>A room's name under its icon: a soft dark plate behind shadowed text, so it reads over any part of the backdrop.</summary>
        public static void RoomLabel(IThemeCanvas c, float cx, float cy, string text, bool lit)
        {
            const float size = 13f;
            var width = c.MeasureText(text, ThemeFont.Label, size) + 38f;
            var r = new ThemeRect(cx - width * .5f, cy - 12, width, 24);
            ThemeWidgets.Plate(c, r, lit ? .96f : .82f);
            var t = ThemeText.Of(ThemeFont.Label, size, lit ? ThemePalette.GoldBright : ThemePalette.Text, ThemeAnchor.MiddleCenter);
            t.shadowY = 1.5f; t.shadowAlpha = .9f;
            ThemeDraw.Text(c, text, r, t);
        }
    }
}
