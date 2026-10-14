namespace GildedFate.Theme
{
    /// <summary>The named colors of the game's look: obsidian and gold, with a handful of accents that always mean the same thing.</summary>
    public static class ThemePalette
    {
        static ThemeColor H(uint hex, float a = 1f) => ThemeColor.Hex(hex, a);

        public static readonly ThemeColor Ink = H(0x03050A), Obsidian = H(0x090D15), Slate = H(0x121A27);
        public static readonly ThemeColor GoldBright = H(0xFBE3A0), Gold = H(0xE6B85C), GoldDeep = H(0xB07A22), Bronze = H(0x8E5F1D), BronzeDark = H(0x3B2509);
        public static readonly ThemeColor Ember = H(0xFF8F2E), EmberSoft = H(0xFFB15E);
        public static readonly ThemeColor Text = H(0xF2E9D3), TextDim = H(0xAA9E86), TextFaint = H(0x6E6657);

        // Accents. Each keeps its meaning everywhere: red hurts, blue guards, gold rewards, violet is arcane, jade heals or grows.
        public static readonly ThemeColor Crimson = H(0xD6392E), CrimsonBright = H(0xFF6A58), Azure = H(0x3F8FE0), AzureBright = H(0x8FC4FF);
        public static readonly ThemeColor Violet = H(0x8A5CFF), VioletBright = H(0xC3A2FF), Jade = H(0x2FBF9A), JadeBright = H(0x7BE8C4);

        // Heroes
        public static readonly ThemeColor Vanguard = H(0xE0623A), Hexer = H(0x9A6BFF), Reaper = H(0x37D6C0);
    }
}
