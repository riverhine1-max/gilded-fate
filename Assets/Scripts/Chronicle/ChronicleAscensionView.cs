using System.Collections.Generic;

namespace GildedFate.Chronicle
{
    // What the Archive and the post-victory flow may ask about the Ten Recollections, with no drawing.
    // Nothing here mutates progress; nothing here exposes developer working titles to the player.

    public sealed class ChronicleRecollectionEntry
    {
        public string id = ""; public ChronicleHero hero; public int tier; public ChronicleRevelationKind kind; public int kindOrdinal;
        public bool earned, presented, watched, contentReady;
        public int chapter; public string memoryId = "";      // 0 / "" until the content phase assigns them
        public bool isNext;                                    // the one this hero earns next
    }

    public sealed class ChronicleHeroRecollections
    {
        public ChronicleHero hero;
        public int earned, watched, pending;
        public int nextFateDebt;                               // 0 once the True History is complete
        public string nextId = "";
        public int nextChapter;                                // the existing chapter that receives the next addition (0 = unassigned)
        public bool trueHistoryComplete, wonFateDebtX;
        public readonly List<ChronicleRecollectionEntry> entries = new List<ChronicleRecollectionEntry>();
    }

    public sealed class ChronicleRecollectionsView
    {
        public readonly ChronicleHeroRecollections[] heroes = new ChronicleHeroRecollections[3];
        public bool allTrueHistoriesComplete, chapterXAvailable, chapterXGrandfathered;
        public int totalEarned, totalWatched;
    }

    public static class ChronicleAscension
    {
        /// <summary>Which finished runs may advance the recollections: genuine victories at Fate Debt I-X, never defeats, abandoned
        /// runs (which never reach the run-end hook), sandbox/Playground runs, or Daily Runs (which already never advance Fate Debt).</summary>
        public static bool IsQualifyingVictory(bool victory, bool sandbox, bool daily, int fateDebt) =>
            victory && !sandbox && !daily && fateDebt >= 1 && fateDebt <= ChronicleAscensionCatalog.TierCount;

        public static ChronicleHeroRecollections Hero(ChronicleProgress p, ChronicleHero hero)
        {
            p.Ensure();
            var next = p.NextRevelation(hero);
            var v = new ChronicleHeroRecollections
            {
                hero = hero, earned = p.RevelationsEarned(hero), watched = p.RevelationsWatched(hero), pending = p.PendingRevelations(hero).Count,
                nextFateDebt = next?.tier ?? 0, nextId = next?.id ?? "", nextChapter = next?.chapter ?? 0,
                trueHistoryComplete = p.TrueHistoryComplete(hero), wonFateDebtX = p.HasWonFateDebtX(hero)
            };
            foreach (var d in ChronicleAscensionCatalog.For(hero))
                v.entries.Add(new ChronicleRecollectionEntry
                {
                    id = d.id, hero = d.hero, tier = d.tier, kind = d.kind, kindOrdinal = d.kindOrdinal, chapter = d.chapter, memoryId = d.memoryId,
                    earned = p.IsRevelationEarned(d.id), presented = p.IsRevelationPresented(d.id), watched = p.IsRevelationWatched(d.id),
                    contentReady = ChronicleAscensionCatalog.IsPresentable(d), isNext = next != null && next.id == d.id
                });
            return v;
        }

        public static ChronicleRecollectionsView View(ChronicleProgress p)
        {
            p.Ensure();
            var v = new ChronicleRecollectionsView
            {
                allTrueHistoriesComplete = p.AllTrueHistoriesComplete, chapterXAvailable = p.secretUnlocked, chapterXGrandfathered = p.ascension.secretGrandfathered
            };
            for (var h = 0; h < 3; h++)
            {
                v.heroes[h] = Hero(p, (ChronicleHero)h);
                v.totalEarned += v.heroes[h].earned; v.totalWatched += v.heroes[h].watched;
            }
            return v;
        }

        /// <summary>
        /// The integration point for the post-victory flow: scene ids of this hero's earned, unpresented revelations whose authored
        /// scenes are approved and registered, in chain order. Empty until content ships, so nothing unfinished can ever be shown.
        /// Playing one through a ChronicleProgressSink marks it presented and watched.
        /// </summary>
        public static List<string> ScenesToPresent(ChronicleProgress p, ChronicleHero hero)
        {
            var scenes = new List<string>();
            if (p == null || (int)hero < 0 || (int)hero > 2) return scenes;
            // Only the next one, and only once its base memories have been watched: exactly one new revelation per presentation.
            foreach (var d in p.PendingRevelations(hero)) if (ChronicleAscensionCatalog.IsPresentable(d) && ChronicleAscensionCatalog.GatesMet(p, d)) { scenes.Add(d.sceneId); break; }
            return scenes;
        }
    }
}
