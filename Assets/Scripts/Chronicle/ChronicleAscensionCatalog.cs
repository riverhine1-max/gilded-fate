using System;
using System.Collections.Generic;
using System.Linq;

namespace GildedFate.Chronicle
{
    // The Ten Recollections: ten Fate Debt revelations per hero, layered on top of the original Chronicle.
    // This file only DESCRIBES the thirty slots (ids, order, kind, references). It contains no story text and registers no scenes:
    // every revelation stays unpresentable until its scene is written, approved and registered in ChronicleScripts.
    // The original 27 memories, the ten shared corrections (CORR_01..CORR_10) and Chapter X live in ChronicleCatalog and are untouched.

    public enum ChronicleRevelationKind { MajorCorrection = 0, SmallRevelation = 1 }

    /// <summary>How an authored revelation will be presented. Unspecified until the content phase decides it.</summary>
    public enum ChronicleRevelationPresentation { Unspecified = 0, SilentHandwriting = 1, Narrated = 2, FullCorrection = 3 }

    public sealed class ChronicleRevelationDef
    {
        /// <summary>Stable save id (ASC_VAN_01 .. ASC_REA_10). Never reuse or renumber once shipped.</summary>
        public string id = "";
        public ChronicleHero hero;
        /// <summary>The Fate Debt level whose victory earns this revelation (1-10). Also its position in the hero's chain.</summary>
        public int tier;
        public ChronicleRevelationKind kind;
        /// <summary>1-4 for major corrections, 1-6 for smaller revelations.</summary>
        public int kindOrdinal;
        /// <summary>Developer-only planning label. Not approved dialogue, not an in-game title, never shown to players.</summary>
        public string workingTitle = "";
        /// <summary>The existing chapter (1-9) whose pages receive this addition. 0 = not yet assigned by the content phase.</summary>
        public int chapter;
        /// <summary>The existing memory (MEM_xx) this addition expands. Empty = not yet assigned.</summary>
        public string memoryId = "";
        /// <summary>Reserved id for a major revelation's own new correction. Separate from CORR_01..CORR_10; empty for smaller revelations.</summary>
        public string correctionId = "";
        /// <summary>Reserved scene id. Nothing is registered under it until the scene is authored.</summary>
        public string sceneId = "";
        public ChronicleRevelationPresentation presentation;
        /// <summary>Set only when the authored, approved scene is registered. Until then the revelation can be earned but is never presented.</summary>
        public bool contentReady;
        /// <summary>The approved title written above the recollection (Prompt 3 content).</summary>
        public string title = "";
        /// <summary>Optional small static marginal study (Resources/Chronicle/Illustrations/Ascension/&lt;key&gt;). Empty = text only.</summary>
        public string marginalArt = "";
        /// <summary>Base memories that must have been watched first, so original discoveries always come first. A revelation whose
        /// gates are not yet met stays earned and queued; it is presented once they are.</summary>
        public string[] requiredViewed = new string[0];

        public bool IsMajor => kind == ChronicleRevelationKind.MajorCorrection;
        /// <summary>The Fate Debt X revelation completes the hero's personal True History (not Chapter X).</summary>
        public bool IsTrueHistory => tier == ChronicleAscensionCatalog.TierCount;
    }

    public static class ChronicleAscensionCatalog
    {
        public const int TierCount = 10, HeroCount = 3, Total = TierCount * HeroCount, MajorsPerHero = 4, SmallPerHero = 6;
        public const string IdPrefix = "ASC_";
        static readonly int[] majorTiers = { 1, 4, 7, 10 };
        static readonly string[] heroCodes = { "VAN", "HEX", "REA" };

        // Planning labels supplied for the later writing phase. Developer-facing only.
        static readonly string[][] majorWorkingTitles =
        {
            new[] { "The Friendship", "The Disagreement", "The Separation", "The True History" },
            new[] { "The Scholar's Mistake", "The Warning He Remembered Wrong", "The Broken Sequence", "The Truth Beyond Calculation" },
            new[] { "The Echo That Wasn't Empty", "The Prison Built from Mercy", "What Protection Could Not Restore", "The Truth of What Remains" },
        };

        public static readonly ChronicleRevelationDef[] All;
        static readonly Dictionary<string, ChronicleRevelationDef> byId = new Dictionary<string, ChronicleRevelationDef>();
        static readonly Dictionary<string, ChronicleRevelationDef> byScene = new Dictionary<string, ChronicleRevelationDef>();
        static readonly Dictionary<string, ChronicleRevelationDef> byCorrection = new Dictionary<string, ChronicleRevelationDef>();
        static readonly ChronicleRevelationDef[][] byHero = new ChronicleRevelationDef[HeroCount][];

        public static string IdFor(ChronicleHero hero, int tier) => IdPrefix + heroCodes[(int)hero] + "_" + tier.ToString("00");
        public static string CorrectionIdFor(ChronicleHero hero, int majorOrdinal) => IdPrefix + "CORR_" + heroCodes[(int)hero] + "_" + majorOrdinal;
        public static bool IsMajorTier(int tier) => Array.IndexOf(majorTiers, tier) >= 0;

        static ChronicleAscensionCatalog()
        {
            var all = new List<ChronicleRevelationDef>();
            for (var h = 0; h < HeroCount; h++)
            {
                var hero = (ChronicleHero)h; var chain = new ChronicleRevelationDef[TierCount]; int majors = 0, smalls = 0;
                for (var tier = 1; tier <= TierCount; tier++)
                {
                    var major = IsMajorTier(tier); var ordinal = major ? ++majors : ++smalls; var id = IdFor(hero, tier);
                    var def = new ChronicleRevelationDef
                    {
                        id = id, hero = hero, tier = tier,
                        kind = major ? ChronicleRevelationKind.MajorCorrection : ChronicleRevelationKind.SmallRevelation, kindOrdinal = ordinal,
                        workingTitle = major ? majorWorkingTitles[h][ordinal - 1] : "",
                        correctionId = major ? CorrectionIdFor(hero, ordinal) : "",
                        sceneId = id,
                        presentation = major ? ChronicleRevelationPresentation.FullCorrection : ChronicleRevelationPresentation.Unspecified,
                    };
                    chain[tier - 1] = def; all.Add(def);
                }
                byHero[h] = chain;
            }
            All = all.ToArray();
            // The authored content: where each recollection is written and how it is presented. Ready once its scene is registered.
            var index = new Dictionary<string, ChronicleRevelationDef>(); foreach (var d in All) index[d.id] = d;
            ChronicleAscensionContent.Apply((id, title, chapter, memory, presentation, art, gates) =>
            {
                if (!index.TryGetValue(id, out var d)) return;
                d.title = title; d.chapter = chapter; d.memoryId = memory; d.presentation = presentation; d.marginalArt = art ?? ""; d.requiredViewed = gates ?? new string[0];
                d.contentReady = ChronicleScripts.Has(d.sceneId);
            });
            foreach (var d in All)
            {
                byId[d.id] = d;
                if (!string.IsNullOrEmpty(d.sceneId)) byScene[d.sceneId] = d;
                if (!string.IsNullOrEmpty(d.correctionId)) byCorrection[d.correctionId] = d;
            }
        }

        /// <summary>The hero's ten revelations in the order they must be earned (Fate Debt I first).</summary>
        public static IReadOnlyList<ChronicleRevelationDef> For(ChronicleHero hero) => (int)hero >= 0 && (int)hero < HeroCount ? byHero[(int)hero] : new ChronicleRevelationDef[0];
        public static ChronicleRevelationDef At(ChronicleHero hero, int tier) => (int)hero >= 0 && (int)hero < HeroCount && tier >= 1 && tier <= TierCount ? byHero[(int)hero][tier - 1] : null;
        public static ChronicleRevelationDef Find(string id) => id != null && byId.TryGetValue(id, out var d) ? d : null;
        public static ChronicleRevelationDef FindByScene(string sceneId) => sceneId != null && byScene.TryGetValue(sceneId, out var d) ? d : null;
        public static ChronicleRevelationDef FindByCorrection(string correctionId) => correctionId != null && byCorrection.TryGetValue(correctionId, out var d) ? d : null;

        /// <summary>A revelation may be shown only when its authored scene is approved and actually registered.</summary>
        public static bool IsPresentable(ChronicleRevelationDef def) => def != null && def.contentReady && !string.IsNullOrEmpty(def.sceneId) && ChronicleScripts.Has(def.sceneId);

        /// <summary>The base memories this recollection builds on have been watched (original discoveries always come first).</summary>
        public static bool GatesMet(ChronicleProgress p, ChronicleRevelationDef def)
        {
            if (def == null || p == null) return false;
            foreach (var m in def.requiredViewed) if (!p.IsViewed(m)) return false;
            return true;
        }

        /// <summary>Structural checks, run by ChronicleValidator and the unit tests.</summary>
        public static void Validate(List<string> errors)
        {
            if (All.Length != Total) errors.Add("Expected " + Total + " ascension revelations, found " + All.Length);
            if (All.Select(d => d.id).Distinct().Count() != All.Length) errors.Add("Duplicate ascension revelation ids");
            if (All.Count(d => d.IsMajor) != MajorsPerHero * HeroCount) errors.Add("Expected 12 major ascension corrections");
            if (All.Count(d => !d.IsMajor) != SmallPerHero * HeroCount) errors.Add("Expected 18 smaller ascension revelations");
            var originalIds = new HashSet<string>(ChronicleCatalog.Memories.Select(m => m.id));
            foreach (var c in ChronicleCatalog.Corrections) originalIds.Add(c.id);
            foreach (var f in ChronicleCatalog.Facts) originalIds.Add(f.id);
            originalIds.Add(ChronicleCatalog.SecretSceneId); originalIds.Add(ChronicleCatalog.SecretChapterId);
            originalIds.Add(ChronicleCatalog.OpeningSceneId); originalIds.Add(ChronicleCatalog.OpeningShortSceneId);
            originalIds.Add(ChronicleMilestoneScenes.VictoryId); originalIds.Add(ChronicleMilestoneScenes.DefeatId);
            for (var h = 0; h < HeroCount; h++)
            {
                var chain = For((ChronicleHero)h);
                for (var tier = 1; tier <= TierCount; tier++)
                {
                    var d = chain[tier - 1];
                    if (d.tier != tier || (int)d.hero != h) errors.Add(d.id + " is out of order");
                    if (d.IsMajor != IsMajorTier(tier)) errors.Add(d.id + " has the wrong kind for Fate Debt " + tier);
                    if (d.IsMajor == string.IsNullOrEmpty(d.correctionId)) errors.Add(d.id + ": only major revelations reserve a correction id");
                }
            }
            foreach (var d in All)
            {
                if (originalIds.Contains(d.id) || originalIds.Contains(d.sceneId) || originalIds.Contains(d.correctionId)) errors.Add(d.id + " collides with an original Chronicle id");
                if (!d.id.StartsWith(IdPrefix, StringComparison.Ordinal)) errors.Add(d.id + " must use the " + IdPrefix + " prefix");
                if (d.chapter < 0 || d.chapter > ChronicleCatalog.ChapterCount) errors.Add(d.id + " references chapter " + d.chapter);
                if (d.contentReady && (d.chapter < 1 || string.IsNullOrEmpty(d.memoryId) || string.IsNullOrEmpty(d.title))) errors.Add(d.id + " is ready but has no chapter, memory or title");
                foreach (var g in d.requiredViewed) if (ChronicleCatalog.FindMemory(g) == null) errors.Add(d.id + " gates on unknown memory " + g);
                if (d.IsMajor && d.contentReady && d.presentation != ChronicleRevelationPresentation.FullCorrection) errors.Add(d.id + " is a major correction but not presented as one");
                if (!string.IsNullOrEmpty(d.memoryId))
                {
                    var m = ChronicleCatalog.FindMemory(d.memoryId);
                    if (m == null) errors.Add(d.id + " references unknown memory " + d.memoryId);
                    else if (m.hero != d.hero) errors.Add(d.id + " references another hero's memory " + d.memoryId);
                    else if (d.chapter != 0 && m.chapter != d.chapter) errors.Add(d.id + " references " + d.memoryId + " outside chapter " + d.chapter);
                }
                if (d.contentReady && !ChronicleScripts.Has(d.sceneId)) errors.Add(d.id + " is marked ready but its scene " + d.sceneId + " is not registered");
                if (!d.contentReady && ChronicleScripts.Has(d.sceneId)) errors.Add(d.id + " has a registered scene but is not marked ready");
            }
            if (All.Where(d => d.IsMajor).Select(d => d.correctionId).Distinct().Count() != MajorsPerHero * HeroCount) errors.Add("Duplicate ascension correction ids");
        }
    }
}
