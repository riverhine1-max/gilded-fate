using System;
using System.Collections.Generic;
using System.Text;

namespace GildedFate.Chronicle
{
    // The Living Chronicle: the book remembers where its story stopped and where the player last read.
    // Two positions are kept apart on purpose:
    //   STORY   - where the automatic Act milestones left the narrative. Only milestone playback moves it.
    //   READING - where the player last was in the Archive (a chapter page, or a page inside a memory). Only the Archive moves it.
    // Spread numbers match the physical book: 0 = contents, 1-9 = the chapters, 10 = Chapter X.

    [Serializable]
    public sealed class ChronicleReadingMark { public string id = ""; public int page; }

    [Serializable]
    public sealed class ChronicleChapterMark { public int chapter; public string seen = ""; }

    /// <summary>Saved inside ChronicleProgress. Plain fields and lists so JsonUtility reads it; older saves load it fresh.</summary>
    [Serializable]
    public sealed class ChronicleJournalState
    {
        public int version;                         // 0 = not yet initialised from an older save
        // ---- story continuation (automatic milestones) ----
        public int storySpread;                     // the chapter spread the story last reached (0 = it has not begun)
        public string storySceneId = "";            // the scene that left it there
        // ---- manual Archive reading ----
        public int readingSpread;                   // the spread the player was on when they last left the Archive
        public string readingId = "";               // the memory being read there, if any
        public List<ChronicleReadingMark> pages = new List<ChronicleReadingMark>();       // last page read inside each memory
        public List<ChronicleChapterMark> chapterMarks = new List<ChronicleChapterMark>(); // what each chapter looked like when last opened
        // ---- reading preferences ----
        public float textScale = 1f;                // 0.85 .. 1.3, reflows Archive pages; new scenes use it as they start
        public bool manualAdvance;                  // true = the book waits for the player before every page turn

        public void Ensure()
        {
            pages = pages ?? new List<ChronicleReadingMark>(); chapterMarks = chapterMarks ?? new List<ChronicleChapterMark>();
            pages.RemoveAll(m => m == null || string.IsNullOrEmpty(m.id)); chapterMarks.RemoveAll(m => m == null);
            storySceneId = storySceneId ?? ""; readingId = readingId ?? "";
            storySpread = Clamp(storySpread, 0, ChronicleJournal.SecretSpread); readingSpread = Clamp(readingSpread, 0, ChronicleJournal.SecretSpread);
            if (!(textScale >= ChronicleJournal.TextScales[0] && textScale <= ChronicleJournal.TextScales[ChronicleJournal.TextScales.Length - 1])) textScale = 1f;
            if (pages.Count > 128) pages.RemoveRange(0, pages.Count - 128);
        }
        static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
    }

    /// <summary>How the book travels from where the story stopped to where the next scene is written.</summary>
    public sealed class ChronicleContinuationPlan
    {
        public int fromSpread, toSpread;
        public int startSpread;                     // where the book is shown open (at most MaxVisibleTurns away from the target)
        public int Turns => Math.Abs(toSpread - startSpread);
        public bool Backward => toSpread < startSpread;
        public bool Moves => Turns > 0;
    }

    /// <summary>A physical journey through the book: one sheet per spread crossed, slow at the ends and quick in the middle.</summary>
    public sealed class ChronicleFlipPlan
    {
        public int fromSpread, toSpread, direction;
        public readonly List<float> tempos = new List<float>();   // per turn: 1 = the normal single-turn speed
        public int Turns => tempos.Count;
        public float Seconds(float turnSeconds) { var s = 0f; foreach (var t in tempos) s += turnSeconds / t; return s; }
    }

    public static class ChronicleJournal
    {
        public const float MaxFlipTempo = 4.5f;
        /// <summary>Plans a multi-page journey between two spreads of the physical book (0 = contents, 1-9 chapters, 10 = Chapter X).
        /// Every spread crossed is a real sheet, so the page stacks move with it; long journeys accelerate through the middle and
        /// slow down before the destination. Reduced motion keeps every turn at the normal speed but caps the number shown.</summary>
        public static ChronicleFlipPlan PlanFlip(int fromSpread, int toSpread, bool reduceMotion = false)
        {
            var plan = new ChronicleFlipPlan { fromSpread = fromSpread, toSpread = toSpread, direction = Math.Sign(toSpread - fromSpread) };
            var n = Math.Abs(toSpread - fromSpread); if (n == 0) return plan;
            for (var i = 0; i < n; i++)
            {
                if (n == 1) { plan.tempos.Add(1f); break; }
                var x = (i + .5f) / n;                                   // 0..1 along the journey
                var bell = (float)Math.Sin(Math.PI * x);                 // 0 at the ends, 1 in the middle
                var peak = Math.Min(MaxFlipTempo, 1.6f + n * .35f);      // longer journeys reach a higher speed
                var t = 1.25f + (peak - 1.25f) * bell;
                if (i == n - 1) t = Math.Min(t, 1.35f);                   // the last sheet settles at nearly normal speed
                plan.tempos.Add(reduceMotion ? 1f : t);
            }
            return plan;
        }

        public const int ContentsSpread = 0, SecretSpread = 10, MaxVisibleTurns = 3;
        public static readonly float[] TextScales = { .85f, 1f, 1.15f, 1.3f };

        // ---------------- which page a scene belongs on ----------------
        /// <summary>The book spread a scene is written on: its chapter for a memory or an authored revelation, 10 for Chapter X,
        /// -1 for scenes that are not pages of the journal (the opening, the run endings) or revelations with no chapter yet.</summary>
        public static int SpreadOf(string sceneId)
        {
            if (string.IsNullOrEmpty(sceneId)) return -1;
            if (sceneId == ChronicleCatalog.SecretSceneId) return SecretSpread;
            var memory = ChronicleCatalog.FindMemory(sceneId); if (memory != null) return memory.chapter;
            var revelation = ChronicleAscensionCatalog.FindByScene(sceneId);
            if (revelation != null && revelation.chapter >= 1 && revelation.chapter <= ChronicleCatalog.ChapterCount) return revelation.chapter;
            return -1;
        }

        // ---------------- story continuation ----------------
        /// <summary>Called when an automatic milestone scene finished writing. Archive replays never call this.</summary>
        public static bool RecordStoryReached(ChronicleProgress p, string sceneId)
        {
            p.Ensure(); var spread = SpreadOf(sceneId); if (spread < 1) return false;
            var j = p.journal; var changed = j.storySpread != spread || j.storySceneId != sceneId;
            j.storySpread = spread; j.storySceneId = sceneId; return changed;
        }

        /// <summary>How the book should travel before an automatic scene: open where the story stopped, then turn (forward, or
        /// back for a revelation written into an earlier chapter) to the scene's own chapter. Null = the story has not begun yet
        /// or the scene is not a journal page, so the scene opens the closed book exactly as it always has.</summary>
        public static ChronicleContinuationPlan PlanContinuation(ChronicleProgress p, string sceneId)
        {
            p.Ensure(); var to = SpreadOf(sceneId); var from = p.journal.storySpread;
            if (to < 1 || from < 1) return null;
            var start = from;
            if (Math.Abs(to - from) > MaxVisibleTurns) start = to > from ? to - MaxVisibleTurns : to + MaxVisibleTurns;   // riffle past the rest
            return new ChronicleContinuationPlan { fromSpread = from, toSpread = to, startSpread = start };
        }

        // ---------------- manual reading ----------------
        public static int PageFor(ChronicleProgress p, string id)
        {
            p.Ensure(); foreach (var m in p.journal.pages) if (m.id == id) return Math.Max(0, m.page);
            return 0;
        }

        public static void RememberPage(ChronicleProgress p, string id, int page)
        {
            p.Ensure(); if (string.IsNullOrEmpty(id)) return;
            var pages = p.journal.pages; var i = pages.FindIndex(m => m.id == id);
            if (i >= 0) pages.RemoveAt(i);
            pages.Add(new ChronicleReadingMark { id = id, page = Math.Max(0, page) });   // most recent last, so trimming drops the oldest
            if (pages.Count > 128) pages.RemoveAt(0);
        }

        public static void RememberReading(ChronicleProgress p, int spread, string readingId)
        {
            p.Ensure(); p.journal.readingSpread = Math.Max(0, Math.Min(SecretSpread, spread)); p.journal.readingId = readingId ?? "";
        }

        /// <summary>True when there is somewhere to continue reading from that is still readable.</summary>
        public static bool HasReadingPosition(ChronicleProgress p)
        {
            p.Ensure(); var j = p.journal;
            if (!string.IsNullOrEmpty(j.readingId)) return ChronicleArchive.CanRead(p, j.readingId);
            return j.readingSpread > ContentsSpread && (j.readingSpread == SecretSpread ? p.secretUnlocked : true);
        }

        /// <summary>A short, spoiler-free description of the saved reading place, for the Continue Reading line.</summary>
        public static string DescribeReadingPosition(ChronicleProgress p)
        {
            p.Ensure(); var j = p.journal;
            var chapter = j.readingSpread == SecretSpread ? "Chapter X" : "Chapter " + Roman(j.readingSpread);
            if (string.IsNullOrEmpty(j.readingId) || !ChronicleArchive.CanRead(p, j.readingId)) return chapter;
            var title = j.readingId == ChronicleCatalog.SecretSceneId ? "The One Who Remembers" : ChronicleCatalog.FindMemory(j.readingId)?.title ?? "";
            return chapter + " \u00b7 " + title + " \u00b7 page " + (PageFor(p, j.readingId) + 1);
        }

        // ---------------- recently updated pages ----------------
        /// <summary>What has been written on a chapter's pages: watched memories, its discovered corrections, and presented revelations.</summary>
        public static string Fingerprint(ChronicleProgress p, int chapter)
        {
            p.Ensure(); var sb = new StringBuilder();
            foreach (var m in ChronicleCatalog.MemoriesInChapter(chapter)) if (p.IsViewed(m.id)) sb.Append(m.id).Append(';');
            foreach (var c in ChronicleCatalog.Corrections) if (c.chapter == chapter && p.HasCorrection(c.id)) sb.Append(c.id).Append(';');
            foreach (var d in ChronicleAscensionCatalog.All)
                if (d.chapter == chapter && (p.IsRevelationPresented(d.id) || p.IsRevelationWatched(d.id))) sb.Append(d.id).Append(';');
            return sb.ToString();
        }

        /// <summary>New writing has appeared on this chapter since the player last opened it in the Archive.</summary>
        public static bool IsChapterUpdated(ChronicleProgress p, int chapter)
        {
            p.Ensure(); var now = Fingerprint(p, chapter); if (now.Length == 0) return false;
            foreach (var m in p.journal.chapterMarks) if (m.chapter == chapter) return m.seen != now;
            return true;
        }

        public static void MarkChapterSeen(ChronicleProgress p, int chapter)
        {
            p.Ensure(); if (chapter < 1 || chapter > ChronicleCatalog.ChapterCount) return;
            var now = Fingerprint(p, chapter);
            foreach (var m in p.journal.chapterMarks) if (m.chapter == chapter) { m.seen = now; return; }
            p.journal.chapterMarks.Add(new ChronicleChapterMark { chapter = chapter, seen = now });
        }

        // ---------------- preferences ----------------
        public static float NextTextScale(float current, int direction)
        {
            var i = Array.FindIndex(TextScales, s => Math.Abs(s - current) < .01f); if (i < 0) i = 1;
            return TextScales[(i + (direction >= 0 ? 1 : TextScales.Length - 1)) % TextScales.Length];
        }

        /// <summary>Version 0 -> 1: a save from before the living journal. The story position starts at the latest chapter the
        /// player has watched, and every chapter's current writing counts as already seen, so nothing is falsely marked new.</summary>
        internal static void Initialise(ChronicleProgress p)
        {
            var j = p.journal;
            if (j.version >= 1) return;
            var latest = 0;
            foreach (var m in ChronicleCatalog.Memories) if (p.viewedMemories.Contains(m.id) && m.chapter > latest) latest = m.chapter;
            if (j.storySpread == 0) j.storySpread = latest;
            j.version = 1;
            for (var c = 1; c <= ChronicleCatalog.ChapterCount; c++) MarkChapterSeen(p, c);
        }

        static string Roman(int n) => n >= 0 && n <= 10 ? new[] { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" }[n] : n.ToString();
    }
}
