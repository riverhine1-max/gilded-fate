using UnityEngine;
using GildedFate.Chronicle;
using GildedFate.ChronicleBook;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        Texture2D chronicleCastleArt, chronicleTravelerArt, chroniclePaintedSky;
        ChronicleArtLineRenderer chronicleArtLines;
        float chronicleSkyUpdatedAt;
        string chronicleSkyBackdrop;
        Color chronicleSkyTop, chronicleSkyBottom;

        private void ChronicleArtLine(Vector2 a, Vector2 b, Color color, float width)
        {
            if(chronicleArtLines == null) chronicleArtLines = new ChronicleArtLineRenderer();
            chronicleArtLines.Draw(a,b,color,width);
        }

        private void ChronicleArtCircle(Vector2 center, float rx, float ry, Color color, float width, int segments)
        {
            var previous = center + new Vector2(rx,0);
            for(var i=1;i<=segments;i++)
            {
                var angle=i*Mathf.PI*2/segments;
                var point=center+new Vector2(Mathf.Cos(angle)*rx,Mathf.Sin(angle)*ry);
                ChronicleArtLine(previous,point,color,width);previous=point;
            }
        }

        private void DrawChronicleAtmosphere(float w, float h)
        {
            Fill(new Rect(0, 0, w, h), new Color(.018f, .024f, .023f));
            ShardSoft(new Rect(w * .13f, h * .08f, w * .74f, h * .84f), new Color(.27f, .18f, .07f, .22f));
            ShardSoft(new Rect(w * .32f, h * .25f, w * .36f, h * .52f), new Color(.07f, .22f, .15f, .16f));
            var moving = profile == null || !profile.reduceMotion;
            for (var i = 0; i < 44; i++)
            {
                var x = ((i * 73.17f) % 100f) / 100f;
                var y = ((i * 29.61f) % 100f) / 100f;
                if (moving) { x += Mathf.Sin(shimmer * .075f + i * 2.3f) * .004f; y += Mathf.Cos(shimmer * .11f + i) * .008f; }
                var size = i % 4 == 0 ? 2f : 1f;
                Fill(new Rect(w * x, h * y, size, size), new Color(.93f, .72f, .32f, .16f + i % 3 * .045f));
            }
        }

        private bool DrawChronicleCastle(Rect box, float alpha, float rotation)
        {
            if (!chronicleCastleArt) chronicleCastleArt = Resources.Load<Texture2D>("Chronicle/Illustrations/IlluminatedCastle");
            if (!chronicleCastleArt) return false;
            var saved = GUI.color; var matrix = GUI.matrix;
            try
            {
                GUI.color = new Color(1, 1, 1, alpha);
                if (Mathf.Abs(rotation) > .001f)
                    GUI.matrix = matrix * Matrix4x4.Translate(box.center) * Matrix4x4.Rotate(Quaternion.Euler(0, 0, rotation)) * Matrix4x4.Translate(-box.center);
                GUI.DrawTexture(box, chronicleCastleArt, ScaleMode.ScaleToFit, true);
            }
            finally { GUI.color = saved; GUI.matrix = matrix; }
            return true;
        }

        private void DrawChronicleIllustratedBackdrop(Rect r, ChronicleIllustrationState ill, float alpha)
        {
            if (DrawChronicleCinematicBackdrop(r, ill, alpha)) return;
            var top = Color.Lerp(ChronCol(ill.top, 1), new Color(.67f, .58f, .39f), .22f);
            var bottom = Color.Lerp(ChronCol(ill.bottom, 1), new Color(.84f, .74f, .51f), .25f);
            if (!chroniclePaintedSky || chronicleSkyBackdrop != ill.backdrop || ((top != chronicleSkyTop || bottom != chronicleSkyBottom) && Time.unscaledTime >= chronicleSkyUpdatedAt + .12f))
            {
                if (!chroniclePaintedSky) chroniclePaintedSky = new Texture2D(384, 512, TextureFormat.RGBA32, false) { name = "Chronicle watercolor wash", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color32[384 * 512];
                for (var y = 0; y < 512; y++) for (var x = 0; x < 384; x++)
                {
                    var u = x / 383f; var v = y / 511f;
                    var hash = unchecked((uint)x * 374761393u + (uint)y * 668265263u);
                    hash = unchecked((hash ^ (hash >> 13)) * 1274126177u);
                    var grain = (hash & 1023) / 1023f - .5f;
                    var cloud = Mathf.PerlinNoise(u * 3.3f + 1.7f, v * 4.7f + 2.1f);
                    var c = Color.Lerp(bottom, top, v);
                    c = Color.Lerp(c, new Color(.90f,.83f,.68f), Mathf.Max(0,cloud-.47f)*.32f);
                    var outdoor = ill.backdrop == "kingdom" || ill.backdrop == "gate" || ill.backdrop == "field" || ill.backdrop == "migration" || ill.backdrop == "plane" || ill.backdrop == "dawn" || ill.backdrop == "campfire" || ill.backdrop == "battle";
                    if(outdoor)for(var layer=0;layer<3;layer++)
                    {
                        var ridge=.53f+layer*.11f+Mathf.Sin(u*9f+layer*2.1f)*.055f+Mathf.Sin(u*22f+layer)*.018f;
                        var mountain=Color.Lerp(top,new Color(.13f,.20f,.17f),.30f+layer*.17f);
                        var coverage=Mathf.Clamp01(((1-v)-ridge)*512f+.5f)*(.65f+layer*.10f);
                        c=Color.Lerp(c,mountain,coverage);
                    }
                    c *= .99f + grain * .045f;
                    // Transparent pigment lets the page's actual grain, creases and staining show through the miniature.
                    var edge = Mathf.Min(Mathf.Min(u, 1-u), Mathf.Min(v, 1-v));
                    c.a = (.88f + cloud * .07f) * Mathf.SmoothStep(.64f, 1f, Mathf.Clamp01(edge * 90f + grain * .2f));
                    pixels[y * 384 + x] = c;
                }
                chroniclePaintedSky.SetPixels32(pixels); chroniclePaintedSky.Apply();
                chronicleSkyTop = top; chronicleSkyBottom = bottom; chronicleSkyUpdatedAt = Time.unscaledTime; chronicleSkyBackdrop = ill.backdrop;
            }
            var saved = GUI.color; GUI.color = new Color(1, 1, 1, alpha);
            GUI.DrawTexture(r, chroniclePaintedSky, ScaleMode.StretchToFill, true); GUI.color = saved;
            var outdoors = ill.backdrop == "kingdom" || ill.backdrop == "gate" || ill.backdrop == "field" || ill.backdrop == "migration" || ill.backdrop == "plane" || ill.backdrop == "dawn" || ill.backdrop == "campfire" || ill.backdrop == "battle";
            if (outdoors)
            {
                var sun = new Vector2(r.width * .23f, r.height * .22f);
                ShardSoft(sun, r.width * .40f, new Color(1, .81f, .40f, alpha * .16f));
                ChronDisc(r, sun, r.width * .045f, r.width * .045f, new Color(.96f, .83f, .57f, alpha * .55f));
                for (var i = 0; i < 7; i++)
                {
                    var y = r.height * (.76f + i * .028f);
                    ChronicleArtLine(new Vector2(r.width * .07f, y), new Vector2(r.width * .93f, y - r.height * .026f), new Color(.82f, .74f, .49f, alpha * .10f), 1);
                }
                // Fine engraved pines and their branches give scale to the miniature landscape.
                for (var i = 0; i < 13; i++)
                {
                    var x = r.width * ((i * .173f + .03f) % 1f); var baseY = r.height * (.68f + i % 3 * .045f);
                    var height = r.height * (.07f + i % 4 * .025f); var c = new Color(.17f, .23f, .17f, alpha * .36f);
                    ChronicleArtLine(new Vector2(x, baseY), new Vector2(x, baseY - height), c, 1);
                    for (var branch = 1; branch <= 6; branch++)
                    {
                        var y = baseY - height + height * branch / 7f; var reach = height * branch / 21f;
                        ChronicleArtLine(new Vector2(x, y), new Vector2(x - reach, y + height * .13f), c, 1);
                        ChronicleArtLine(new Vector2(x, y), new Vector2(x + reach, y + height * .13f), c, 1);
                    }
                }
            }
            else if (ill.backdrop == "chamber" || ill.backdrop == "sanctuary" || ill.backdrop == "passage")
            {
                var stone = new Color(.85f, .73f, .49f, alpha * .22f);
                for (var i = 0; i < 4; i++)
                {
                    var x = r.width * (.06f + i * .29f);
                    ChronicleArtLine(new Vector2(x, r.height * .14f), new Vector2(x, r.height * .82f), stone, r.width * .025f);
                    for (var j = 0; j < 9; j++) ChronicleArtLine(new Vector2(x - r.width * .02f, r.height * (.2f + j * .067f)), new Vector2(x + r.width * .02f, r.height * (.2f + j * .067f)), stone, 1);
                }
                for (var i = 0; i < 9; i++) ChronicleArtLine(new Vector2(r.width * .5f, r.height * .70f), new Vector2(r.width * i / 8f, r.height), stone, 1);
                ChronicleArtCircle(new Vector2(r.width * .5f, r.height * .24f), r.width * .40f, r.height * .15f, stone, 1.5f, 64);
            }
            else
            {
                var center = new Vector2(r.width * .5f, r.height * .43f);
                var gold = new Color(.87f, .70f, .35f, alpha * .21f);
                for (var ring = 0; ring < 3; ring++) ChronicleArtCircle(center, r.width * (.27f + ring * .05f), r.width * (.27f + ring * .05f), gold, 1, 64);
                for (var i = 0; i < 48; i++)
                {
                    var angle = i * Mathf.PI * 2 / 48f; var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    ChronicleArtLine(center + direction * r.width * .32f, center + direction * r.width * (i % 4 == 0 ? .36f : .34f), gold, 1);
                }
            }
            // A restrained constellation is part of the painted background, never a gameplay flash.
            for (var i = 0; i < 28; i++)
            {
                var x = ((i * .371f + .11f) % 1f) * r.width; var y = ((i * .219f + .08f) % .48f) * r.height;
                ChronDisc(r, new Vector2(x, y), i % 5 == 0 ? 1.4f : .65f, i % 5 == 0 ? 1.4f : .65f, new Color(.97f, .86f, .61f, alpha * .30f));
            }
        }

        private void DrawChronicleArtFrame(Rect r, float alpha)
        {
            var gold = new Color(.77f, .58f, .24f, alpha * .82f);
            var inset = Mathf.Max(4, r.width * .012f);
            for (var corner = 0; corner < 4; corner++)
            {
                var origin = new Vector2(corner % 2 == 0 ? inset : r.width - inset, corner < 2 ? inset : r.height - inset);
                var sx = corner % 2 == 0 ? 1 : -1; var sy = corner < 2 ? 1 : -1; var extent = r.width * .085f;
                ChronicleArtLine(origin, origin + new Vector2(sx * extent, 0), gold, 1.4f);
                ChronicleArtLine(origin, origin + new Vector2(0, sy * extent), gold, 1.4f);
                var previous = origin;
                for (var j = 0; j <= 24; j++)
                {
                    var angle = j / 24f * Mathf.PI * 3.2f; var radius = extent * (.11f + j / 24f * .22f);
                    var point = origin + new Vector2(sx * (extent * .35f + Mathf.Cos(angle) * radius), sy * (extent * .35f + Mathf.Sin(angle) * radius));
                    if (j > 0) ChronicleArtLine(previous, point, gold, 1); previous = point;
                }
            }
        }

        private void DrawChronicleFigure(Rect bounds, Rect b, Color c, ChronicleActor actor)
        {
            if (DrawChronicleCinematicFigure(bounds, b, c, actor)) return;
            if (!chronicleTravelerArt) chronicleTravelerArt = Resources.Load<Texture2D>("Chronicle/Illustrations/IlluminatedTraveler");
            if (chronicleTravelerArt)
            {
                var saved = GUI.color;
                var tint = Color.Lerp(new Color(c.r,c.g,c.b,1), Color.white, .65f); tint.a = c.a;
                GUI.color = tint;
                var painted = new Rect(b.center.x - b.height * chronicleTravelerArt.width / chronicleTravelerArt.height * .5f, b.y, b.height * chronicleTravelerArt.width / chronicleTravelerArt.height, b.height);
                GUI.DrawTexture(painted, chronicleTravelerArt, ScaleMode.ScaleToFit, true);
                GUI.color = saved; return;
            }

            var dark = new Color(c.r * .43f, c.g * .43f, c.b * .43f, c.a);
            var light = new Color(Mathf.Min(1, c.r * 1.20f + .06f), Mathf.Min(1, c.g * 1.15f + .045f), Mathf.Min(1, c.b * 1.08f + .02f), c.a);
            var head = new Vector2(b.center.x, b.y + b.height * .135f);
            // Layered hood, a shaded face and tailored shoulders replace the old block figure.
            ChronDisc(bounds, head, b.width * .31f, b.height * .11f, dark);
            ChronDisc(bounds, head + new Vector2(b.width * .03f, b.height * .015f), b.width * .17f, b.height * .064f, new Color(.72f, .59f, .42f, c.a));
            ChronDisc(bounds, head + new Vector2(-b.width * .13f, -b.height * .035f), b.width * .17f, b.height * .064f, c);
            for (var row = 0; row < 36; row++)
            {
                var t = row / 35f; var y = b.y + b.height * (.25f + t * .70f);
                var width = b.width * (.63f + t * .22f + Mathf.Sin(t * Mathf.PI) * .13f);
                ChronFillIn(bounds, new Rect(b.center.x - width * .5f, y, width, b.height * .021f + .5f), Color.Lerp(c, dark, t * .36f));
            }
            var shoulderY = b.y + b.height * .27f;
            ChronicleArtLine(new Vector2(b.x + b.width * .13f, shoulderY), new Vector2(b.center.x, b.y + b.height * .22f), light, Mathf.Max(1, b.width * .08f));
            ChronicleArtLine(new Vector2(b.center.x, b.y + b.height * .22f), new Vector2(b.xMax - b.width * .13f, shoulderY), light, Mathf.Max(1, b.width * .08f));
            for (var fold = 0; fold < 5; fold++)
            {
                var x = b.x + b.width * (.23f + fold * .14f);
                ChronicleArtLine(new Vector2(b.center.x + (x - b.center.x) * .45f, shoulderY + b.height * .10f), new Vector2(x, b.yMax - b.height * .08f), fold % 2 == 0 ? dark : light, Mathf.Max(.6f, b.width * .025f));
            }
            ChronicleArtLine(new Vector2(b.x + b.width * .22f, b.y + b.height * .55f), new Vector2(b.xMax - b.width * .20f, b.y + b.height * .57f), dark, Mathf.Max(1, b.height * .022f));
            ChronDisc(bounds, new Vector2(b.center.x, b.y + b.height * .56f), b.width * .07f, b.width * .07f, new Color(.83f, .64f, .29f, c.a));
            ChronFillIn(bounds, new Rect(b.center.x - b.width * .29f, b.yMax - b.height * .075f, b.width * .18f, b.height * .075f), dark);
            ChronFillIn(bounds, new Rect(b.center.x + b.width * .09f, b.yMax - b.height * .075f, b.width * .18f, b.height * .075f), dark);
        }
    }
}
