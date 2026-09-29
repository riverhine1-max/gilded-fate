using UnityEngine;

namespace GildedFate.UI
{
    // Presentation-only polish: golden-thread screen wipe, reward gold burst + card threads, relic pedestal light.
    // Nothing here changes timers, input gating, or run state; everything reads existing presentation values.
    public sealed partial class GildedMainMenu
    {
        private Texture2D tfStrandTex, tfGlowTex, tfRingTex, tfShaftTex;

        private static Texture2D TfMakeTexture(int tw, int th, System.Func<float, float, float> alpha)
        {
            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (var y = 0; y < th; y++)
                for (var x = 0; x < tw; x++)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha((x + .5f) / tw, (y + .5f) / th))));
            tex.Apply();
            return tex;
        }

        private void TfEnsureTextures()
        {
            if (tfStrandTex == null) tfStrandTex = TfMakeTexture(4, 32, (u, v) => { var d = (v - .5f) * 2f; return Mathf.Exp(-d * d * 5.5f); });
            if (tfGlowTex == null) tfGlowTex = TfMakeTexture(64, 64, (u, v) => { var dx = u - .5f; var dy = v - .5f; var r = Mathf.Sqrt(dx * dx + dy * dy) * 2f; return Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f); });
            if (tfRingTex == null) tfRingTex = TfMakeTexture(64, 64, (u, v) => { var dx = u - .5f; var dy = v - .5f; var r = Mathf.Sqrt(dx * dx + dy * dy) * 2f; var d = (r - .78f) / .16f; return Mathf.Exp(-d * d) * (r < 1f ? 1f : 0f); });
            if (tfShaftTex == null) tfShaftTex = TfMakeTexture(32, 64, (u, v) => { var d = (u - .5f) * 2f; var side = Mathf.Exp(-d * d * 3.2f); return side * Mathf.Lerp(.25f, 1f, v); });
        }

        private static void TfDrawTinted(Rect r, Texture2D tex, Color c)
        {
            var old = GUI.color; GUI.color = c; GUI.DrawTexture(r, tex, ScaleMode.StretchToFill, true); GUI.color = old;
        }

        // Soft glowing segment from a to b.
        private void TfStrand(Vector2 a, Vector2 b, float thickness, Color c)
        {
            var d = b - a; var len = d.magnitude; if (len < .5f || c.a <= .002f) return;
            var angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg; var oldM = GUI.matrix;
            var pivot = new Vector3(a.x, a.y, 0f);
            GUI.matrix = oldM * Matrix4x4.TRS(pivot, Quaternion.Euler(0f, 0f, angle), Vector3.one) * Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one);
            TfDrawTinted(new Rect(a.x, a.y - thickness * .5f, len, thickness), tfStrandTex, c);
            GUI.matrix = oldM;
        }

        private void TfGlow(Vector2 at, float size, Color c) => TfDrawTinted(new Rect(at.x - size * .5f, at.y - size * .5f, size, size), tfGlowTex, c);

        private static float TfHash(float n) { var s = Mathf.Sin(n * 12.9898f + 78.233f) * 43758.5453f; return s - Mathf.Floor(s); }

        // 1. Golden-thread wipe layered on top of the transitionAlpha fade. Duration is entirely derived from transitionAlpha.
        private void DrawGoldenThreadWipe(float w, float h)
        {
            if (transitionAlpha <= .001f || profile == null || profile.reduceMotion || Event.current == null || Event.current.type != EventType.Repaint) return;
            TfEnsureTextures();
            var p = 1f - Mathf.Clamp01(transitionAlpha);
            var dir = new Vector2(w, h).normalized; var perp = new Vector2(-dir.y, dir.x); var diag = Mathf.Sqrt(w * w + h * h);
            var center = new Vector2(w * .5f, h * .5f); var count = profile.reducedVfx ? 4 : 7; var spacing = diag * .78f / count;
            var flashScale = profile.reduceFlashing ? .5f : 1f;
            for (var i = 0; i < count; i++)
            {
                var q = Mathf.Clamp01((p - i * .035f) / .8f);
                var extend = Mathf.Sin(q * Mathf.PI); if (extend <= .01f) continue;
                var lateral = (i - (count - 1) * .5f) * spacing + (TfHash(i) - .5f) * spacing * .35f;
                var start = center - dir * diag * .62f + perp * lateral;
                var head = start + dir * diag * 1.24f * Mathf.SmoothStep(0, 1, extend);
                var alpha = Mathf.Clamp01(extend * 1.3f);
                var col = new Color(1f, .78f, .34f, .75f * alpha);
                TfStrand(start, head, profile.reducedVfx ? 5f : 9f, new Color(1f, .62f, .16f, .22f * alpha * flashScale));
                TfStrand(start, head, 2.2f, col);
                TfStrand(start, head, 1f, new Color(1f, .96f, .82f, .8f * alpha * flashScale));
                if (!profile.reducedVfx) TfGlow(head, 46f, new Color(1f, .86f, .5f, .55f * alpha * flashScale));
                TfGlow(head, 14f, new Color(1f, .98f, .9f, .9f * alpha * flashScale));
            }
        }

        // 2a. Coin burst + count-up + sparkle on the Reward screen, driven by the existing goldCollect timer.
        private bool TfRewardGoldBurstActive => goldCollectTime > 0 && goldCollectAmount > 0 && profile != null && !profile.reduceMotion && screen == ScreenMode.Reward;

        private void DrawRewardGoldBurst()
        {
            if (!TfRewardGoldBurstActive || Event.current == null || Event.current.type != EventType.Repaint) return;
            TfEnsureTextures();
            var t = 1f - Mathf.Clamp01(goldCollectTime / 1.08f); var o = goldCollectOrigin;
            var flashScale = profile.reduceFlashing ? .45f : 1f;
            // Warm flare at the claim point.
            TfGlow(o, Mathf.Lerp(120f, 260f, t), new Color(1f, .72f, .26f, .35f * (1f - t) * flashScale));
            // Radial coin burst.
            var coins = profile.reducedVfx ? 6 : 14;
            for (var i = 0; i < coins; i++)
            {
                var a = (i / (float)coins) * Mathf.PI * 2f + TfHash(i + 3) * .6f; var speed = 110f + TfHash(i + 11) * 90f;
                var bt = Mathf.Clamp01(t / .7f); var dist = speed * (1f - (1f - bt) * (1f - bt));
                var at = o + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * .7f) * dist; at.y += 160f * bt * bt;
                var size = Mathf.Lerp(26f, 14f, bt); var fade = 1f - Mathf.Clamp01((t - .45f) / .45f); if (fade <= 0) continue;
                var old = GUI.color; GUI.color = new Color(1f, 1f, 1f, fade); DrawGoldIcon(new Rect(at.x - size * .5f, at.y - size * .5f, size, size)); GUI.color = old;
            }
            // Sparkles.
            if (!profile.reducedVfx)
                for (var i = 0; i < 12; i++)
                {
                    var life = Mathf.Repeat(t * 2.2f + TfHash(i + 40), 1f); var tw = Mathf.Sin(life * Mathf.PI);
                    var at = o + new Vector2((TfHash(i + 70) - .5f) * 260f, (TfHash(i + 90) - .5f) * 140f - life * 26f);
                    var c = new Color(1f, .95f, .78f, .8f * tw * (1f - t * .6f) * flashScale);
                    TfStrand(at - new Vector2(7f * tw, 0), at + new Vector2(7f * tw, 0), 2f, c);
                    TfStrand(at - new Vector2(0, 7f * tw), at + new Vector2(0, 7f * tw), 2f, c);
                }
            // Count-up number.
            var count = Mathf.RoundToInt(goldCollectAmount * Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .55f)));
            var pop = 1f + .18f * Mathf.Sin(Mathf.Clamp01(t / .55f) * Mathf.PI); var alphaText = 1f - Mathf.Clamp01((t - .7f) / .3f);
            var fs = Mathf.RoundToInt(30 * pop); var lr = new Rect(o.x - 160, o.y - 118 - t * 18f, 320, 50);
            var style = new GUIStyle(titleStyle) { font = labelFont ? labelFont : bodyFont, fontSize = fs, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = new Color(.08f, .04f, .01f, .7f * alphaText); GUI.Label(new Rect(lr.x + 2, lr.y + 2, lr.width, lr.height), "+" + count + " GOLD", style);
            style.normal.textColor = new Color(1f, .86f, .42f, alphaText); GUI.Label(lr, "+" + count + " GOLD", style);
        }

        // 2b. Thin swaying gold threads that the reward cards hang from while rewardRevealTime runs.
        private void DrawRewardCardThreads(float w, float h, int optionCount)
        {
            if (optionCount <= 0 || rewardRevealTime <= .01f || profile == null || profile.reduceMotion || Event.current == null || Event.current.type != EventType.Repaint) return;
            TfEnsureTextures();
            var fadeOut = Mathf.Clamp01(rewardRevealTime / .4f) * RewardChoiceOpacity; if (fadeOut <= .01f) return;
            var gap = 28f; var cw = Mathf.Min(220f, (w - 180 - gap * (optionCount - 1)) / optionCount);
            var start = Mathf.Max(124, (w - (cw * optionCount + gap * (optionCount - 1))) * .5f);
            var segments = profile.reducedVfx ? 5 : 9; var time = Time.unscaledTime;
            for (var i = 0; i < optionCount; i++)
            {
                var reveal = RewardReveal(i); var cardTop = h * .29f + (1f - reveal) * 85f; var x0 = start + i * (cw + gap);
                for (var s = 0; s < 2; s++)
                {
                    var ax = x0 + cw * (s == 0 ? .26f : .74f); var topY = cardTop - 150f; var prev = new Vector2(ax, topY);
                    for (var k = 1; k <= segments; k++)
                    {
                        var f = k / (float)segments; var sway = Mathf.Sin(time * 1.7f + i * 1.3f + s * .8f) * 6f * Mathf.Sin(f * Mathf.PI) * (1f - reveal * .6f);
                        var pt = new Vector2(ax + sway, Mathf.Lerp(topY, cardTop + 2f, f)); var taper = Mathf.Clamp01(f * 1.6f);
                        TfStrand(prev, pt, 3.5f, new Color(1f, .64f, .2f, .16f * taper * fadeOut));
                        TfStrand(prev, pt, 1.2f, new Color(1f, .86f, .48f, .75f * taper * fadeOut));
                        prev = pt;
                    }
                    TfGlow(prev, 12f, new Color(1f, .9f, .6f, .7f * fadeOut));
                }
            }
        }

        // 3. Lit pedestal for relic showcases: light shaft from above, rising dust motes, glow ring under the icon.
        private void DrawRelicPedestal(Rect icon)
        {
            if (profile == null || Event.current == null || Event.current.type != EventType.Repaint) return;
            TfEnsureTextures();
            var time = Time.unscaledTime; var still = profile.reduceMotion;
            var pulse = still || profile.reduceFlashing ? 1f : 1f + .12f * Mathf.Sin(time * 1.6f);
            var shaftW = icon.width * 1.25f; var shaft = new Rect(icon.center.x - shaftW * .5f, icon.y - icon.height * 1.1f, shaftW, icon.height * 2.05f);
            TfDrawTinted(shaft, tfShaftTex, new Color(1f, .86f, .55f, profile.reducedVfx ? .12f : .18f));
            var ring = new Rect(icon.center.x - icon.width * .62f, icon.yMax - icon.height * .2f, icon.width * 1.24f, icon.height * .34f);
            TfDrawTinted(ring, tfGlowTex, new Color(1f, .7f, .28f, .32f * pulse));
            TfDrawTinted(ring, tfRingTex, new Color(1f, .84f, .46f, .42f * pulse));
            var motes = profile.reducedVfx ? 4 : 12;
            for (var i = 0; i < motes; i++)
            {
                var life = still ? TfHash(i + 5) : Mathf.Repeat(time * (.07f + TfHash(i + 17) * .06f) + TfHash(i + 5), 1f);
                var x = shaft.x + shaft.width * (.2f + .6f * TfHash(i + 29)) + (still ? 0 : Mathf.Sin(time * .8f + i) * 5f);
                var y = Mathf.Lerp(icon.yMax + 6f, shaft.y + 10f, life); var a = Mathf.Sin(life * Mathf.PI) * .6f;
                var size = 2.5f + TfHash(i + 51) * 3f;
                TfGlow(new Vector2(x, y), size * 3.2f, new Color(1f, .9f, .66f, a));
            }
        }
    }
}
