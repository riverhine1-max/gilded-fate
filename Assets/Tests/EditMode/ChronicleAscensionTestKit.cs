using System;

namespace GildedFate.Chronicle.Tests
{
    /// <summary>Shared helpers for tests that need the Ten Recollections in a known state. Test-only: never compiled into the game.</summary>
    public static class ChronicleAscensionTestKit
    {
        /// <summary>The hero wins Fate Debt 1..lastTier in order, one recorded victory each, as the real run-end hook reports them.</summary>
        public static ChronicleProgress ClearThrough(ChronicleProgress p, ChronicleHero hero, int lastTier)
        {
            for (var tier = 1; tier <= lastTier; tier++) p.RegisterAscensionVictory(hero, tier, "asc-" + hero + "-" + tier + "-" + Guid.NewGuid().ToString("N"));
            return p;
        }

        /// <summary>Every hero completes their True History (Fate Debt I-X in order), which is the Chapter X requirement.</summary>
        public static ChronicleProgress CompleteEveryTrueHistory(ChronicleProgress p)
        {
            foreach (ChronicleHero hero in Enum.GetValues(typeof(ChronicleHero))) ClearThrough(p, hero, ChronicleAscensionCatalog.TierCount);
            return p;
        }
    }
}
