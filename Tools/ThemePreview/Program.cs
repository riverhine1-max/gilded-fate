using System;
using System.IO;
using GildedFate.Theme;

public static class Program
{
    static string Root()
    {
        var d = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (d != null && !Directory.Exists(Path.Combine(d.FullName, "Assets", "Scripts", "Theme"))) d = d.Parent;
        return d?.FullName ?? Directory.GetCurrentDirectory();
    }

    public static int Main(string[] args)
    {
        var root = Root(); var outDir = args.Length > 0 ? args[0] : Path.Combine(root, "theme-preview"); Directory.CreateDirectory(outDir);
        var fonts = Path.Combine(root, "Assets", "Resources", "Fonts");
        string background = args.Length > 1 ? args[1] : null;
        var art = Path.Combine(root, "Assets", "Resources", "Art");
        ImagesSheet(Path.Combine(outDir, "images.png"), fonts);
        MenuSheet(Path.Combine(outDir, "menu.png"), fonts, Path.Combine(art, "GildedVault_Menu.png"));
        ComponentSheet(Path.Combine(outDir, "components.png"), fonts, Path.Combine(art, "CombatArena_LowerVault.png"));
        MapSheet(Path.Combine(outDir, "map.png"), fonts, Path.Combine(art, "RunMap_LowerVault.png"), Path.Combine(art, "Identity", "RoomEmblems.png"));
        Console.WriteLine("wrote previews to " + outDir);
        return 0;
    }

    static void ImagesSheet(string path, string fonts)
    {
        var items = new (string name, ThemeImage img)[]
        {
            ("frame.gold", ThemeAssets.FrameGold), ("frame.bronze", ThemeAssets.FrameBronze), ("frame.crimson", ThemeAssets.FrameCrimson), ("frame.azure", ThemeAssets.FrameAzure),
            ("frame.violet", ThemeAssets.FrameViolet), ("frame.jade", ThemeAssets.FrameJade), ("frame.steel", ThemeAssets.FrameSteel), ("shadow.soft", ThemeAssets.Shadow),
            ("glow.soft", ThemeAssets.Glow), ("glow.tight", ThemeAssets.GlowTight), ("ring.glow", ThemeAssets.Ring), ("vignette", ThemeAssets.Vignette),
            ("grain", ThemeAssets.Grain), ("diamond.gold", ThemeAssets.DiamondGold), ("plate.diamond", ThemeAssets.DiamondPlate), ("medallion", ThemeAssets.Medallion), ("flare", ThemeAssets.Flare), ("plate.hex", ThemeAssets.HexPlate),
        };
        const int cell = 256, cols = 4; var rows = (items.Length + cols - 1) / cols;
        using (var c = new SkiaCanvas(cols * cell, rows * (cell + 28), fonts))
        {
            c.Clear(new ThemeColor(.16f, .15f, .15f, 1));
            for (var i = 0; i < items.Length; i++)
            {
                var x = (i % cols) * cell; var y = (i / cols) * (cell + 28);
                c.Fill(new ThemeRect(x + 6, y + 6, cell - 12, cell - 12), new ThemeColor(.07f, .09f, .13f, 1));
                var im = items[i].img; var size = Math.Min(cell - 24f, im.width * 3f); var dest = new ThemeRect(x + (cell - size) / 2, y + (cell - size) / 2, size, size);
                c.Image(im, dest, ThemeColor.White);
                ThemeDraw.Text(c, items[i].name + "  " + im.width + "x" + im.height, new ThemeRect(x, y + cell - 2, cell, 24), ThemeText.Of(ThemeFont.Label, 14, ThemeColor.White));
            }
            c.Save(path);
        }
    }

    static void Label(SkiaCanvas c, string text, ThemeRect r, float size = 20, ThemeColor? color = null, ThemeFont font = ThemeFont.Label)
    {
        var t = ThemeText.Of(font, size, color ?? ThemePalette.Text); t.shadowY = 2; t.shadowAlpha = .9f; ThemeDraw.Text(c, text, r, t);
    }

    static void MenuSheet(string path, string fonts, string bg)
    {
        using (var c = new SkiaCanvas(1600, 900, fonts))
        {
            c.Clear(ThemeColor.Black); c.DrawBackground(bg); c.Time = 0.9f;
            ThemeWidgets.Title(c, new ThemeRect(400, 60, 800, 140), "GILDED FATE", "ENTER THE VAULT", 64);
            var names = new[] { "PLAY", "ARCHIVE", "SETTINGS", "QUIT" };
            var states = new[]
            {
                new ButtonVisual { enabled = true, hover = 1, selected = true, pulse = .8f },   // hovered + focused
                ButtonVisual.Idle,                                                              // idle
                new ButtonVisual { enabled = true, hover = 1, press = 1 },                      // pressed
                new ButtonVisual { enabled = false },                                           // disabled
            };
            for (var i = 0; i < 4; i++)
            {
                var r = new ThemeRect(545, 310 + i * 73.5f, 510, 58);
                ThemeWidgets.Button(c, r, states[i]);
                Label(c, names[i], r, 22, states[i].enabled ? ThemePalette.Text : ThemePalette.TextFaint);
            }
            Label(c, "hovered + focused", new ThemeRect(1080, 310, 300, 58), 14, ThemePalette.TextDim);
            Label(c, "idle", new ThemeRect(1080, 383, 300, 58), 14, ThemePalette.TextDim);
            Label(c, "pressed", new ThemeRect(1080, 457, 300, 58), 14, ThemePalette.TextDim);
            Label(c, "disabled", new ThemeRect(1080, 530, 300, 58), 14, ThemePalette.TextDim);
            var small = new ThemeRect(1410, 825, 160, 44); ThemeWidgets.Button(c, small, ButtonVisual.Idle); Label(c, "WHAT'S NEW", small, 14);
            c.Save(path);
        }
    }

    static void ComponentSheet(string path, string fonts, string bg)
    {
        using (var c = new SkiaCanvas(1600, 900, fonts))
        {
            c.Clear(ThemeColor.Black); c.DrawBackground(bg, .25f); c.Time = 0.3f;
            // a settings-style panel with title and rows
            var panel = new ThemeRect(60, 50, 700, 560); ThemeWidgets.Panel(c, panel, FrameKind.Gold);
            ThemeWidgets.Title(c, new ThemeRect(panel.x + 20, panel.y + 24, panel.w - 40, 80), "SETTINGS", "SHAPE THE VAULT TO YOUR NEEDS", 40);
            var rows = new[] { ("WINDOW MODE", "BORDERLESS"), ("RESOLUTION", "NATIVE"), ("VERTICAL SYNC", "OFF"), ("FRAME RATE LIMIT", "120 FPS"), ("ANTI-ALIASING", "4x") };
            for (var i = 0; i < rows.Length; i++)
            {
                var r = new ThemeRect(panel.x + 36, panel.y + 150 + i * 74, panel.w - 72, 60); ThemeWidgets.Well(c, r);
                var l = ThemeText.Of(ThemeFont.Label, 17, ThemePalette.Text, ThemeAnchor.MiddleLeft); l.shadowY = 1.5f; l.shadowAlpha = .8f; ThemeDraw.Text(c, rows[i].Item1, new ThemeRect(r.x + 22, r.y, 300, r.h), l);
                var v = ThemeText.Of(ThemeFont.Label, 17, ThemePalette.EmberSoft, ThemeAnchor.MiddleRight); ThemeDraw.Text(c, rows[i].Item2, new ThemeRect(r.xMax - 322, r.y, 300, r.h), v);
            }
            // panel kinds
            var kinds = new[] { FrameKind.Crimson, FrameKind.Azure, FrameKind.Violet, FrameKind.Jade };
            var names = new[] { "ATTACK", "BLOCK", "ARCANE", "GROWTH" };
            for (var i = 0; i < 4; i++)
            {
                var r = new ThemeRect(820 + (i % 2) * 360, 50 + (i / 2) * 150, 330, 126); ThemeWidgets.Panel(c, r, kinds[i], .8f);
                Label(c, names[i], new ThemeRect(r.x, r.y + 20, r.w, 30), 20, ThemePalette.GoldBright);
                Label(c, "Deal 6 damage. Apply 2 Burn.", new ThemeRect(r.x + 16, r.y + 52, r.w - 32, 50), 15, ThemePalette.Text, ThemeFont.Body);
            }
            // bars
            var bx = 820f; var by = 380f;
            Label(c, "HEALTH  full / hurt (ghost trail) / low", new ThemeRect(bx, by - 30, 700, 24), 14, ThemePalette.TextDim);
            ThemeWidgets.Bar(c, new ThemeRect(bx, by, 330, 26), BarKind.Health, 1f);
            ThemeWidgets.Bar(c, new ThemeRect(bx + 360, by, 330, 26), BarKind.Health, .55f, .78f);
            ThemeWidgets.Bar(c, new ThemeRect(bx, by + 44, 330, 26), BarKind.Health, .18f, 0, true);
            ThemeWidgets.Bar(c, new ThemeRect(bx + 360, by + 44, 330, 26), BarKind.Block, .7f);
            ThemeWidgets.Bar(c, new ThemeRect(bx, by + 88, 330, 18), BarKind.Energy, .66f);
            ThemeWidgets.Bar(c, new ThemeRect(bx + 360, by + 88, 330, 18), BarKind.Arcane, .4f);
            // tooltip
            ThemeWidgets.Tooltip(c, new ThemeRect(820, 530, 360, 120), "STRENGTH", "Adds its value to attack damage, on every hit.");
            ThemeWidgets.Tooltip(c, new ThemeRect(1220, 530, 340, 120), "WEAK", "Deals 25% less attack damage.", FrameKind.Violet);
            // plate + small buttons
            ThemeWidgets.Plate(c, new ThemeRect(60, 650, 700, 40)); Label(c, "COMBAT  ·  MERCHANT  ·  EVENT  (label plates keep small text readable)", new ThemeRect(60, 650, 700, 40), 15);
            var b1 = new ThemeRect(60, 720, 220, 54); ThemeWidgets.Button(c, b1, ButtonVisual.Idle); Label(c, "END TURN", b1, 20);
            var b2 = new ThemeRect(300, 720, 220, 54); ThemeWidgets.Button(c, b2, new ButtonVisual { enabled = true, hover = 1 }); Label(c, "CONFIRM", b2, 20);
            var b3 = new ThemeRect(540, 720, 220, 54); ThemeWidgets.Button(c, b3, new ButtonVisual { enabled = false }); Label(c, "LOCKED", b3, 20, ThemePalette.TextFaint);
            // combat pieces
            var ex = 820f; var ey = 690f; Label(c, "END TURN: ready / hover / nothing left to play / enemy turn", new ThemeRect(ex, ey - 28, 760, 22), 14, ThemePalette.TextDim);
            var plates = new (bool en, float hv, float br, string t)[] { (true, 0f, 0f, "END TURN"), (true, 1f, 0f, "END TURN"), (true, 0f, .8f, "END TURN"), (false, 0f, 0f, "ENEMY TURN") };
            for (var i = 0; i < 4; i++) { var pr = new ThemeRect(ex + (i % 2) * 380, ey + (i / 2) * 96, 340, 76); ThemeWidgets.EndTurnPlate(c, pr, plates[i].en, plates[i].hv, plates[i].br); Label(c, plates[i].t, pr, 24, plates[i].en ? ThemePalette.GoldBright : ThemePalette.TextFaint); }
            var bb = new ThemeRect(1480, 40, 46, 46); ThemeWidgets.BlockBadge(c, bb, .6f); Label(c, "11", bb, 18, ThemeColor.White);
            // outlines in several colours around flat panels
            var oc = new[] { new ThemeColor(.9f, .62f, .24f, 1), new ThemeColor(.28f, .56f, .95f, 1), new ThemeColor(.78f, .3f, .35f, 1), new ThemeColor(.5f, .85f, .55f, .9f), ThemeColor.White };
            for (var i = 0; i < oc.Length; i++)
            {
                var orr = new ThemeRect(60 + i * 140, 800, 124, 70); c.Fill(orr, new ThemeColor(.009f, .014f, .024f, .96f)); ThemeWidgets.Outline(c, orr, oc[i], i == 4 ? 3 : i % 2 == 0 ? 2 : 1);
            }
            c.Save(path);
        }
    }

    static ThemeImage LoadImage(string id, string path)
    {
        using (var src = SkiaSharp.SKBitmap.Decode(path))
        {
            var info = new SkiaSharp.SKImageInfo(src.Width, src.Height, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Unpremul);
            var img = new ThemeImage(id, info.Width, info.Height);
            var h = System.Runtime.InteropServices.GCHandle.Alloc(img.rgba, System.Runtime.InteropServices.GCHandleType.Pinned);
            try { using (var im = SkiaSharp.SKImage.FromBitmap(src)) im.ReadPixels(info, h.AddrOfPinnedObject(), info.RowBytes, 0, 0); } finally { h.Free(); }
            return img;
        }
    }

    /// <summary>The act map as the game draws it in the new look. The room emblems are the game's real art (Art/Identity/RoomEmblems); everything around them is theme.</summary>
    static void MapSheet(string path, string fonts, string bg, string emblemPath, float time = 1.3f)
    {
        const int W = 1920, H = 1080;
        var emblems = File.Exists(emblemPath) ? LoadImage("emblems", emblemPath) : null;
        void Emblem(SkiaCanvas c, int kind, ThemeRect r, ThemeColor tint)
        {
            if (emblems == null) return;
            c.ImageUV(emblems, r, new ThemeRect((kind % 4) * .25f, kind < 4 ? 0f : .5f, .25f, .5f), tint);
        }
        using (var c = new SkiaCanvas(W, H, fonts))
        {
            c.Clear(new ThemeColor(.003f, .004f, .007f, 1)); c.DrawBackgroundTinted(bg, 0, 58, W, H - 58, new ThemeColor(.62f, .68f, .78f, .62f));
            c.Fill(new ThemeRect(0, 58, W, H - 58), new ThemeColor(.002f, .005f, .012f, .40f));
            c.Image(ThemeAssets.Vignette, new ThemeRect(0, 58, W, H - 58), new ThemeColor(1, 1, 1, .85f)); c.Time = time;
            var map = new ThemeRect(0, 58, 1550, H - 58);
            ThemeMap.LightShafts(c, map, 1f); ThemeMap.Embers(c, new ThemeRect(0, 58, W, H - 58), 40, 0);
            ThemeWidgets.HudStrip(c, new ThemeRect(0, 0, W, 58));
            Label(c, "80/80", new ThemeRect(90, 10, 120, 38), 22, ThemePalette.Text, ThemeFont.Heading);
            ThemeMap.Header(c, 775, 92, "CHOOSE YOUR NEXT ROOM · CLICK A GILDED NODE", true);

            // x, y, kind (0..6), state
            var N = new (float x, float y, int kind, NodeState st)[]
            {
                /*0*/ (265, 955, 0, NodeState.Cleared), (580, 955, 0, NodeState.Cleared), (885, 955, 0, NodeState.Cleared),
                /*3*/ (265, 800, 0, NodeState.Locked), (580, 800, 0, NodeState.Current), (840, 800, 2, NodeState.Locked), (1165, 800, 0, NodeState.Locked), (1470, 800, 0, NodeState.Locked),
                /*8*/ (290, 640, 2, NodeState.Locked), (550, 640, 0, NodeState.Available), (855, 640, 0, NodeState.Available), (1130, 640, 5, NodeState.Locked),
                /*12*/ (850, 480, 0, NodeState.Locked), (1150, 480, 3, NodeState.Locked), (1440, 480, 0, NodeState.Locked),
                /*15*/ (270, 320, 4, NodeState.Locked), (550, 320, 4, NodeState.Locked), (850, 320, 4, NodeState.Locked), (1130, 320, 0, NodeState.Locked),
                /*19*/ (290, 160, 0, NodeState.Locked), (590, 160, 0, NodeState.Locked), (860, 160, 0, NodeState.Locked), (1130, 160, 1, NodeState.Locked), (1450, 160, 1, NodeState.Locked),
            };
            var hover = 10; // the pointer is over this room
            var E = new (int a, int b)[]
            {
                (1, 4), (0, 3), (2, 5), (3, 8), (4, 9), (4, 10), (5, 10), (6, 11), (7, 11), (8, 15), (9, 16), (10, 12), (10, 13), (11, 13), (11, 14), (12, 17), (13, 18), (14, 18),
                (15, 19), (16, 20), (17, 21), (18, 22), (18, 23),
            };
            foreach (var e in E)
            {
                var a = N[e.a]; var b = N[e.b];
                var kind = a.st == NodeState.Cleared && b.st == NodeState.Current ? ThreadKind.Traveled : a.st == NodeState.Current && b.st == NodeState.Available ? ThreadKind.Available
                    : (e.a == hover || e.b == hover) ? ThreadKind.Reachable : ThreadKind.Future;
                ThemeMap.Thread(c, a.x, a.y, b.x, b.y, kind, 1f, 3);
            }
            for (var i = 0; i < N.Length; i++)
            {
                var n = N[i]; var size = n.st == NodeState.Current ? 84f : n.st == NodeState.Available ? (i == hover ? 84f : 78f) : 64f; var hv = i == hover ? 1f : 0f;
                var tone = (NodeTone)n.kind;
                ThemeMap.NodeUnder(c, n.x, n.y, size, tone, n.st, hv, i, 2);
                Emblem(c, n.kind, new ThemeRect(n.x - size * .5f, n.y - size * .5f, size, size), n.st == NodeState.Cleared ? new ThemeColor(.44f, .42f, .37f, 1) : ThemeColor.White);
                ThemeMap.NodeOver(c, n.x, n.y, size, tone, n.st, hv, i, 2);
                var name = n.kind == 5 ? "REST SHRINE" : tone.ToString().ToUpperInvariant();
                ThemeMap.RoomLabel(c, n.x, n.y + size * .5f + 18, name + (n.st == NodeState.Current ? " · HERE" : n.st == NodeState.Cleared ? " · CLEARED" : ""), n.st >= NodeState.Available);
            }
            // boss column
            var crown = new ThemeRect(W - 207, 106, 126, 132);
            ThemeMap.BossCrown(c, crown, 2); Emblem(c, 6, new ThemeRect(crown.x + 8, crown.y + 8, crown.w - 16, crown.h - 16), ThemeColor.White);
            ThemeMap.LabelPlate(c, new ThemeRect(W - 268, 250, 252, 50));
            Label(c, "ACT I · SUMMIT KEEPER", new ThemeRect(W - 268, 252, 252, 24), 13, ThemePalette.Gold);
            Label(c, "THE DROWNED QUARTER", new ThemeRect(W - 268, 274, 252, 24), 14, new ThemeColor(.45f, .85f, .82f, 1));
            // legend
            var legend = new ThemeRect(W - 262, 330, 240, 376); ThemeWidgets.Panel(c, legend);
            Label(c, "VAULT ROOMS", new ThemeRect(legend.x, legend.y + 12, legend.w, 34), 22, ThemePalette.GoldBright, ThemeFont.Heading);
            var kinds = new[] { "COMBAT", "ELITE", "EVENT", "TREASURE", "MERCHANT", "SANCTUARY", "BOSS" };
            for (var i = 0; i < kinds.Length; i++)
            {
                var row = new ThemeRect(legend.x + 11, legend.y + 49 + i * 44, legend.w - 22, 40); ThemeWidgets.Row(c, row, i == 3 ? 1f : 0f);
                Emblem(c, i, new ThemeRect(row.x + 7, row.y + 2, 36, 36), ThemeColor.White);
                var t = ThemeText.Of(ThemeFont.Label, 14, i == 3 ? ThemePalette.GoldBright : ThemePalette.Text, ThemeAnchor.MiddleLeft); t.shadowY = 1.5f; t.shadowAlpha = .8f;
                ThemeDraw.Text(c, kinds[i], new ThemeRect(row.x + 53, row.y, row.w - 59, row.h), t);
            }
            ThemeMap.RoomLabel(c, 775, H - 26, "SCROLL TO EXPLORE · FOLLOW THE GILDED THREADS", false);
            c.Save(path);
        }
    }
}
