using System;
using System.Collections.Generic;
using System.Linq;

namespace GildedFate.Chronicle
{
    public enum ChronicleMetric { RunsEnded, BestFloor, ActBossDefeated, Wins }

    /// <summary>A deterministic, achievable unlock condition. Every metric is counted per hero.</summary>
    public sealed class ChronicleUnlockRule
    {
        public ChronicleMetric metric;
        public int threshold;   // RunsEnded/Wins: count. BestFloor: overall floor (1-54).
        public int act;         // ActBossDefeated: 1-3.
        public string hint;     // Spoiler-free text shown on locked entries.
    }

    public sealed class ChronicleChapterDef
    {
        public int number; public string id, title, teaser; public int act;
        public ChroniclePrefaceLine[] preface = new ChroniclePrefaceLine[0];
    }

    /// <summary>One sentence the narrator originally wrote for a chapter. A line tied to a correction shows its
    /// original text, then (once the correction is discovered) a red strike-through and the replacement beneath.</summary>
    public sealed class ChroniclePrefaceLine { public string text, correctionId; }

    public sealed class ChronicleMemoryDef
    {
        public string id; public int number, chapter; public ChronicleHero hero; public string title;
        public ChronicleUnlockRule rule;
        public string[] facts = new string[0], corrections = new string[0];
        public string SceneId => id;
    }

    public sealed class ChronicleFactDef { public string id, summary; }

    public sealed class ChronicleCorrectionDef
    {
        public string id, original, corrected; public int chapter;
        public string[] sources = new string[0];   // viewing ANY of these memories commits the correction
    }

    public static class ChronicleCatalog
    {
        public const int MemoryCount = 27, ChapterCount = 9;
        public const string OpeningSceneId = "SCENE_OPENING", OpeningShortSceneId = "SCENE_OPENING_SHORT", SecretSceneId = "SCENE_CH10", SecretChapterId = "CH_10";
        public const string PrologueChapterId = "CH_00";

        public static readonly string[] ActNames = { "", "BEFORE THE WORLD BROKE", "THE LAST PROMISE", "THE STORY THAT WOULD NOT END" };
        public static readonly string[] HeroNames = { "Vanguard", "Hexer", "Reaper" };

        public static readonly ChronicleChapterDef Prologue;
        public static readonly ChronicleChapterDef[] Chapters;
        public static readonly ChronicleMemoryDef[] Memories;
        public static readonly ChronicleCorrectionDef[] Corrections;
        public static readonly ChronicleFactDef[] Facts;

        public static string CorrectionKey(int n) => "CORR_" + n.ToString("00");
        public static string MemoryKey(int n) => "MEM_" + n.ToString("00");

        static ChroniclePrefaceLine P(string text) => new ChroniclePrefaceLine { text = text };
        static ChroniclePrefaceLine C(string correctionId) => new ChroniclePrefaceLine { correctionId = correctionId };

        static ChronicleCatalog()
        {
            Corrections = new[]
            {
                Corr(1, 3, "Three travelers stood together.", "There were four.", "MEM_07", "MEM_08", "MEM_09"),
                Corr(2, 2, "The traveler watched the futures unfold.", "The traveler was changing them.", "MEM_05"),
                Corr(3, 2, "The lost futures left nothing behind.", "Their echoes remained.", "MEM_06"),
                Corr(4, 4, "The sanctuary was abandoned.", "The sanctuary was watching them.", "MEM_10"),
                Corr(5, 5, "The memories were lost by chance.", "Someone attempted to erase them deliberately.", "MEM_14"),
                Corr(6, 6, "The Severing was an unexplained catastrophe.", "The conflict over memory and fate caused the Severing.", "MEM_17"),
                Corr(7, 7, "Time continued unchanged after the Severing.", "Time fragmented into loops, jumps, and broken sequences.", "MEM_20"),
                Corr(8, 8, "Their stories had reached an ending.", "Something in the Weave refused the ending.", "MEM_24"),
                Corr(9, 9, "The fourth companion disappeared completely.", "He survived in a fused existence with the Being.", "MEM_27"),
                Corr(10, 0, "The narrator was only a historian.", "The narrator is the Forgotten Observer.", "SECRET"),
            };

            Facts = new[]
            {
                F("FACT_KINGDOM_STOOD", "An ancient kingdom stood before the Severing."),
                F("FACT_FUTURES_REAL", "Alternative futures were more than predictions."),
                F("FACT_ECHOES_EXIST", "Unchosen futures leave echoes behind."),
                F("FACT_TRAVELER_WELCOMED", "The traveler was welcomed and genuinely helped people."),
                F("FACT_TRAVELER_MERGES_FUTURES", "The traveler was merging possible futures."),
                F("FACT_EXPERIMENTS_HARM", "The traveler's experiments harmed reality."),
                F("FACT_FOURTH_COMPANION", "There was a fourth companion."),
                F("FACT_FOURTH_ERASED", "The fourth companion was removed from memory."),
                F("FACT_FOURTH_WAS_REAL", "The fourth companion was a real person."),
                F("FACT_SANCTUARY_WATCHED", "The sanctuary was watching the companions."),
                F("FACT_MEMORY_LINKED", "Memory and identity were anchors for the Being's power."),
                F("FACT_DESTRUCTION_DANGEROUS", "Destroying the experiment could harm entangled lives."),
                F("FACT_VANGUARD_OPPOSED_PLAN", "Vanguard opposed the memory-erasure plan."),
                F("FACT_ERASURE_PLAN", "The fourth companion planned a protective memory erasure."),
                F("FACT_LAST_PROMISE", "The fourth companion promised to remember."),
                F("FACT_FOURTH_STAYED", "The fourth companion stayed at the center of the catastrophe."),
                F("FACT_SEVERING_CAUSE", "The Severing came from the conflict over memory and fate."),
                F("FACT_BEING_FUSED_WITH_MORTAL", "The Being fused with a mortal consciousness."),
                F("FACT_SURVIVORS_MIGRATED", "Survivors fled the broken world."),
                F("FACT_TIME_FRAGMENTED", "Time fragmented after the Severing."),
                F("FACT_PLANE_OF_PROTECTION", "The Plane of Protection became a refuge."),
                F("FACT_RUNS_ECHO", "Repeated journeys are part of broken fate."),
                F("FACT_FATE_ANCHOR", "A fate-anchor keeps some stories from ending."),
                F("FACT_ENDING_REFUSED", "A second will resists the Chronicle's endings."),
                F("FACT_FOURTH_IS_FRIEND", "Vanguard remembers the fourth companion as a friend."),
                F("FACT_HANDWRITING_MATCH", "The Chronicle's handwriting matches the fourth companion's."),
                F("FACT_FOURTH_FUSED_WITH_BEING", "The fourth companion and the Being were fused."),
                F("FACT_NARRATOR_IS_OBSERVER", "The narrator is the Forgotten Observer."),
            };

            var chapterData = new[]
            {
                new[] { "Before the First Thread", "1", "An old kingdom, and a feeling that something was already wrong." },
                new[] { "The Traveler Beyond Time", "1", "A stranger arrives at the gate." },
                new[] { "Three Against the End", "1", "A fire beneath the night sky." },
                new[] { "Beneath the Kingdom", "2", "Something waits below the stone." },
                new[] { "The Last Promise", "2", "A choice made in the dark." },
                new[] { "The Day Fate Shattered", "2", "The day everything broke." },
                new[] { "The World That Forgot", "3", "The long road after the breaking." },
                new[] { "The Unfinished Tale", "3", "Why some stories refuse to end." },
                new[] { "The Fourth Shadow", "3", "The last pieces." },
            };
            Chapters = new ChronicleChapterDef[ChapterCount];
            for (var i = 0; i < ChapterCount; i++)
                Chapters[i] = new ChronicleChapterDef { number = i + 1, id = "CH_" + (i + 1).ToString("00"), title = chapterData[i][0], act = int.Parse(chapterData[i][1]), teaser = chapterData[i][2] };

            Chapters[0].preface = new[] { P("A kingdom stood beneath an unbroken sky."), P("Its people believed tomorrow would resemble yesterday.") };
            Chapters[1].preface = new[] { P("A traveler came to the kingdom and was welcomed at its gate."), C("CORR_02"), C("CORR_03") };
            Chapters[2].preface = new[] { P("The companions made camp before the sanctuary."), C("CORR_01"), P("They had little certainty and even less agreement.") };
            Chapters[3].preface = new[] { P("They descended into the old sanctuary."), C("CORR_04"), P("Every door opened as they approached.") };
            Chapters[4].preface = new[] { P("Something was lost in those days."), C("CORR_05") };
            Chapters[5].preface = new[] { C("CORR_06"), P("The three survived.") };
            Chapters[6].preface = new[] { P("The survivors scattered across the broken world."), C("CORR_07") };
            Chapters[7].preface = new[] { P("Across the broken world, their stories continued."), C("CORR_08") };
            Chapters[8].preface = new[] { C("CORR_09"), P("Three names endured: Vanguard, Hexer, Reaper.") };
            Prologue = new ChronicleChapterDef
            {
                number = 0, id = PrologueChapterId, title = "The Chronicle of Broken Fate", act = 0, teaser = "The book's own words.",
                preface = new[] { P("Before the world was broken, there was a time when tomorrow belonged to no one."), C("CORR_10") }
            };

            var m = new List<ChronicleMemoryDef>();
            void Add(int n, string title, string[] facts, params string[] corrs)
            {
                var chapter = (n - 1) / 3 + 1; var hero = (ChronicleHero)((n - 1) % 3);
                m.Add(new ChronicleMemoryDef { id = MemoryKey(n), number = n, chapter = chapter, hero = hero, title = title, rule = RuleFor(chapter, hero), facts = facts, corrections = corrs });
            }
            Add(1, "The Kingdom That Stood", new[] { "FACT_KINGDOM_STOOD" });
            Add(2, "The Futures That Never Were", new[] { "FACT_FUTURES_REAL" });
            Add(3, "Echoes Without Names", new[] { "FACT_ECHOES_EXIST" });
            Add(4, "The Welcomed Stranger", new[] { "FACT_TRAVELER_WELCOMED" });
            Add(5, "The Hand Upon Tomorrow", new[] { "FACT_TRAVELER_MERGES_FUTURES" }, "CORR_02");
            Add(6, "The Cost of Possibility", new[] { "FACT_EXPERIMENTS_HARM" }, "CORR_03");
            Add(7, "The Empty Place", new[] { "FACT_FOURTH_COMPANION" }, "CORR_01");
            Add(8, "The Missing Signature", new[] { "FACT_FOURTH_COMPANION", "FACT_FOURTH_ERASED" }, "CORR_01");
            Add(9, "Four Shadows", new[] { "FACT_FOURTH_COMPANION", "FACT_FOURTH_WAS_REAL" }, "CORR_01");
            Add(10, "The Watching Sanctuary", new[] { "FACT_SANCTUARY_WATCHED" }, "CORR_04");
            Add(11, "The Thread Between Minds", new[] { "FACT_MEMORY_LINKED" });
            Add(12, "The Forgotten Futures", new[] { "FACT_DESTRUCTION_DANGEROUS" });
            Add(13, "The Argument Before Dawn", new[] { "FACT_FOURTH_COMPANION", "FACT_VANGUARD_OPPOSED_PLAN" });
            Add(14, "The Impossible Solution", new[] { "FACT_FOURTH_COMPANION", "FACT_ERASURE_PLAN" }, "CORR_05");
            Add(15, "A Promise Without Witnesses", new[] { "FACT_FOURTH_COMPANION", "FACT_LAST_PROMISE" });
            Add(16, "Run While You Can", new[] { "FACT_FOURTH_COMPANION", "FACT_FOURTH_STAYED" });
            Add(17, "The Breaking of the Weave", new[] { "FACT_SEVERING_CAUSE" }, "CORR_06");
            Add(18, "Two Voices in the Dark", new[] { "FACT_BEING_FUSED_WITH_MORTAL" });
            Add(19, "The Long Migration", new[] { "FACT_SURVIVORS_MIGRATED" });
            Add(20, "The Broken Hours", new[] { "FACT_TIME_FRAGMENTED" }, "CORR_07");
            Add(21, "The Plane of Protection", new[] { "FACT_PLANE_OF_PROTECTION" });
            Add(22, "A Battle Remembered Twice", new[] { "FACT_RUNS_ECHO" });
            Add(23, "The Anchor That Refused", new[] { "FACT_FATE_ANCHOR" });
            Add(24, "The Ending That Vanished", new[] { "FACT_ENDING_REFUSED" }, "CORR_08");
            Add(25, "The Friend I Forgot", new[] { "FACT_FOURTH_COMPANION", "FACT_FOURTH_IS_FRIEND" });
            Add(26, "The Handwriting of the Lost", new[] { "FACT_HANDWRITING_MATCH" });
            Add(27, "The One Left Behind", new[] { "FACT_FOURTH_COMPANION", "FACT_FOURTH_FUSED_WITH_BEING" }, "CORR_09");
            Memories = m.ToArray();
        }

        static ChronicleCorrectionDef Corr(int n, int chapter, string original, string corrected, params string[] sources) =>
            new ChronicleCorrectionDef { id = CorrectionKey(n), chapter = chapter, original = original, corrected = corrected, sources = sources };
        static ChronicleFactDef F(string id, string summary) => new ChronicleFactDef { id = id, summary = summary };

        /// <summary>Per-hero unlock rule for a chapter. Deterministic and progress-based: finishing a run, depth reached,
        /// act bosses defeated, then a win. No elites, drops or random events gate the story.</summary>
        public static ChronicleUnlockRule RuleFor(int chapter, ChronicleHero hero)
        {
            var who = "the " + HeroNames[(int)hero];
            switch (chapter)
            {
                case 1: return new ChronicleUnlockRule { metric = ChronicleMetric.RunsEnded, threshold = 1, hint = "Finish a run as " + who + "." };
                case 2: return new ChronicleUnlockRule { metric = ChronicleMetric.BestFloor, threshold = 9, hint = "Reach floor 9 as " + who + "." };
                case 3: return new ChronicleUnlockRule { metric = ChronicleMetric.ActBossDefeated, act = 1, threshold = 1, hint = "Defeat the Act I boss as " + who + "." };
                case 4: return new ChronicleUnlockRule { metric = ChronicleMetric.BestFloor, threshold = 24, hint = "Reach floor 24 as " + who + "." };
                case 5: return new ChronicleUnlockRule { metric = ChronicleMetric.BestFloor, threshold = 30, hint = "Reach floor 30 as " + who + "." };
                case 6: return new ChronicleUnlockRule { metric = ChronicleMetric.ActBossDefeated, act = 2, threshold = 1, hint = "Defeat the Act II boss as " + who + "." };
                case 7: return new ChronicleUnlockRule { metric = ChronicleMetric.BestFloor, threshold = 42, hint = "Reach floor 42 as " + who + "." };
                case 8: return new ChronicleUnlockRule { metric = ChronicleMetric.BestFloor, threshold = 48, hint = "Reach floor 48 as " + who + "." };
                default: return new ChronicleUnlockRule { metric = ChronicleMetric.Wins, threshold = 1, hint = "Win a run as " + who + "." };
            }
        }

        public static ChronicleMemoryDef FindMemory(string id) { foreach (var x in Memories) if (x.id == id) return x; return null; }
        public static ChronicleCorrectionDef FindCorrection(string id) { foreach (var x in Corrections) if (x.id == id) return x; return null; }
        public static ChronicleFactDef FindFact(string id) { foreach (var x in Facts) if (x.id == id) return x; return null; }
        public static ChronicleChapterDef FindChapter(int number) => number == 0 ? Prologue : number >= 1 && number <= ChapterCount ? Chapters[number - 1] : null;
        public static IEnumerable<ChronicleMemoryDef> MemoriesInChapter(int chapter) => Memories.Where(x => x.chapter == chapter);
        public static ChronicleMemoryDef MemoryFor(int chapter, ChronicleHero hero) => Memories.First(x => x.chapter == chapter && x.hero == hero);
        public static string HeroName(ChronicleHero hero) => HeroNames[(int)hero];
    }
}
