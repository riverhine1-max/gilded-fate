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
            ("grain", ThemeAssets.Grain), ("diamond.gold", ThemeAssets.DiamondGold),
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
            c.Save(path);
        }
    }
}
