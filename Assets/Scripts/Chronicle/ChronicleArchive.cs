using System;
using System.Collections.Generic;
using System.Linq;

namespace GildedFate.Chronicle
{
    // The archive's logic, with no drawing. The Unity screen asks these classes WHAT to show and WHAT a button press does;
    // keeping that here makes locked-content hiding, correction display and controller navigation testable.

    public enum ChronicleEntryState { Locked, Unlocked, Viewed }
    public enum ChronicleSecretState { Locked, Unlocked, Completed }

    public sealed class ChronicleChapterRow
    {
        public int number; public string id, title, teaser, actName; public int act;
        public int unlocked, viewed; public bool visible;   // visible = at least one memory unlocked, so its pages may be read
        public bool Complete => viewed >= 3;
    }

    /// <summary>One sentence of a chapter page. If a correction applies, the original is shown struck through with the replacement beneath.</summary>
    public sealed class ChroniclePrefaceEntry { public string text = "", correctionId = "", corrected = ""; public bool hasCorrection, struck; }

    public sealed class ChronicleMemoryEntry
    {
        public string id = "", title = "", hint = ""; public ChronicleHero hero; public ChronicleEntryState state; public bool isNew;
    }

    public sealed class ChronicleContentsView
    {
        public readonly List<ChronicleChapterRow> chapters = new List<ChronicleChapterRow>();
        public readonly List<ChroniclePrefaceEntry> prologue = new List<ChroniclePrefaceEntry>();
        public int memoriesUnlocked, memoriesViewed, correctionsFound, newCount;
        public ChronicleSecretState secret;
    }

    public sealed class ChronicleChapterView
    {
        public ChronicleChapterRow row = new ChronicleChapterRow();
        public readonly List<ChroniclePrefaceEntry> preface = new List<ChroniclePrefaceEntry>();
        public readonly List<ChronicleMemoryEntry> memories = new List<ChronicleMemoryEntry>();
    }

    public sealed class ChronicleReadingSpread
    {
        public readonly List<ChronicleTextBlock> left = new List<ChronicleTextBlock>(), right = new List<ChronicleTextBlock>();
    }

    /// <summary>A memory's recorded pages, readable without replaying it. Built from the scene's final state, so it always reflects
    /// exactly the corrections and facts the player has discovered.</summary>
    public sealed class ChronicleMemoryReading
    {
        public ChronicleMemoryDef memory; public ChronicleIllustrationState illustration;
        public readonly List<ChronicleReadingSpread> spreads = new List<ChronicleReadingSpread>();
    }

    public static class ChronicleArchive
    {
        public static ChronicleSecretState SecretState(ChronicleProgress p) => p.secretCompleted ? ChronicleSecretState.Completed : p.secretUnlocked ? ChronicleSecretState.Unlocked : ChronicleSecretState.Locked;

        public static ChronicleEntryState StateOf(ChronicleProgress p, string memoryId) => p.IsViewed(memoryId) ? ChronicleEntryState.Viewed : p.IsUnlocked(memoryId) ? ChronicleEntryState.Unlocked : ChronicleEntryState.Locked;

        static ChronicleChapterRow Row(ChronicleChapterDef chapter, ChronicleProgress p) => new ChronicleChapterRow
        {
            number = chapter.number, id = chapter.id, title = chapter.title, teaser = chapter.teaser, act = chapter.act, actName = ChronicleCatalog.ActNames[Math.Max(0, Math.Min(3, chapter.act))],
            unlocked = p.ChapterUnlockedCount(chapter.number), viewed = p.ChapterViewedCount(chapter.number), visible = p.ChapterUnlockedCount(chapter.number) > 0
        };

        static void AddPreface(ChronicleChapterDef chapter, ChronicleProgress p, List<ChroniclePrefaceEntry> into)
        {
            foreach (var line in chapter.preface)
            {
                if (string.IsNullOrEmpty(line.correctionId)) { into.Add(new ChroniclePrefaceEntry { text = line.text }); continue; }
                var def = ChronicleCatalog.FindCorrection(line.correctionId);
                into.Add(new ChroniclePrefaceEntry { text = def.original, correctionId = def.id, corrected = def.corrected, hasCorrection = true, struck = p.HasCorrection(def.id) });
            }
        }

        public static ChronicleContentsView Contents(ChronicleProgress p)
        {
            p.Ensure();
            var v = new ChronicleContentsView { memoriesUnlocked = p.TotalUnlocked, memoriesViewed = p.TotalViewed, correctionsFound = p.corrections.Count, secret = SecretState(p) };
            foreach (var chapter in ChronicleCatalog.Chapters) v.chapters.Add(Row(chapter, p));
            AddPreface(ChronicleCatalog.Prologue, p, v.prologue);
            v.newCount = ChronicleCatalog.Memories.Count(m => p.IsUnlocked(m.id) && !p.IsViewed(m.id));
            return v;
        }

        /// <summary>A chapter's pages. A chapter with nothing unlocked shows only its spoiler-free teaser and locked entries.</summary>
        public static ChronicleChapterView Chapter(int number, ChronicleProgress p)
        {
            p.Ensure();
            var chapter = ChronicleCatalog.FindChapter(number); var v = new ChronicleChapterView { row = Row(chapter, p) };
            if (v.row.visible) AddPreface(chapter, p, v.preface);
            foreach (var m in ChronicleCatalog.MemoriesInChapter(number))
            {
                var state = StateOf(p, m.id);
                v.memories.Add(new ChronicleMemoryEntry
                {
                    id = m.id, hero = m.hero, state = state, isNew = state == ChronicleEntryState.Unlocked,
                    title = state == ChronicleEntryState.Locked ? "" : m.title,           // titles can reveal, so locked entries never show them
                    hint = state == ChronicleEntryState.Locked ? m.rule.hint : ""
                });
            }
            return v;
        }

        /// <summary>The memory's written pages for reading. Null if the memory is still locked.</summary>
        public static ChronicleMemoryReading Reading(string memoryId, ChronicleProgress p, ChroniclePageMetrics metrics = null)
        {
            p.Ensure(); metrics = metrics ?? new ChroniclePageMetrics();
            var memory = ChronicleCatalog.FindMemory(memoryId);
            if (memory == null || !p.IsUnlocked(memoryId)) return null;
            var scene = ChronicleScripts.Get(memoryId); if (scene == null) return null;
            var state = ChronicleSceneResolver.Resolve(scene, new ChronicleProgressContext(p), metrics);
            var reading = new ChronicleMemoryReading { memory = memory, illustration = state.illustration };
            var budget = new ChroniclePageBudget(metrics); var spread = new ChronicleReadingSpread(); reading.spreads.Add(spread);
            var side = ChroniclePageSide.Left;
            foreach (var b in state.transcript)
            {
                if (b.erased && b.eraseProgress >= 1f) continue;
                var height = ChronicleLayout.HeightOf(b.text, metrics, b.style) + (string.IsNullOrEmpty(b.speaker) ? 0f : metrics.LineHeightFor(b.style));
                if (!budget.Fits(side, height))
                {
                    if (side == ChroniclePageSide.Left) side = ChroniclePageSide.Right;
                    else { spread = new ChronicleReadingSpread(); reading.spreads.Add(spread); budget.Reset(); side = ChroniclePageSide.Left; }
                }
                budget.Place(side, height); b.side = side;
                (side == ChroniclePageSide.Left ? spread.left : spread.right).Add(b);
            }
            return reading;
        }
    }

    public enum ChronicleArchiveAction { None, Moved, TurnedPage, OpenedReading, ClosedReading, Replay, BeginSecret, Exit, ChangedOption }

    public struct ChronicleArchiveResult
    {
        public ChronicleArchiveAction action; public string id; public int fromSpread, toSpread;
        public static ChronicleArchiveResult Of(ChronicleArchiveAction a, string id = "", int from = 0, int to = 0) => new ChronicleArchiveResult { action = a, id = id, fromSpread = from, toSpread = to };
    }

    /// <summary>
    /// Keyboard, controller and mouse navigation for the archive, as a state machine. Spread 0 is the contents; 1-9 are chapters;
    /// 10 is secret Chapter X. Opening a memory enters a reading view; Back always steps outward one level and finally exits.
    /// Locked entries can be focused (so their hint can be read) but never activated.
    /// </summary>
    public sealed class ChronicleArchiveController
    {
        public const int ContentsSpread = 0, SecretSpread = 10, ContentsOptionStart = 10, OptionCount = 3;
        public static readonly float[] SpeedSteps = { .75f, 1f, 1.25f, 1.5f, 2f };

        readonly ChronicleProgress progress;
        public int Spread { get; private set; }
        public int Focus { get; private set; }
        public int ReadingSpread { get; private set; }
        public string ReadingId { get; private set; }
        public ChronicleMemoryReading Reading { get; private set; }
        public bool InReading => ReadingId != null;

        public ChronicleArchiveController(ChronicleProgress progress, int startSpread = ContentsSpread) { this.progress = progress; progress.Ensure(); Spread = Math.Max(0, Math.Min(SecretSpread, startSpread)); }

        public int FocusCount => InReading ? 1 : Spread == ContentsSpread ? ContentsOptionStart + OptionCount : Spread == SecretSpread ? 1 : 3;

        public ChronicleArchiveResult Move(int dx, int dy)
        {
            if (InReading) return dx != 0 ? TurnReading(dx) : Idle();
            if (dy != 0) return SetFocus(Focus + dy);
            if (dx == 0) return Idle();
            if (Spread == ContentsSpread) return Focus >= ContentsOptionStart ? ChangeOption(Focus - ContentsOptionStart, dx) : Idle();
            return Page(dx);
        }

        public ChronicleArchiveResult Page(int direction)
        {
            if (direction == 0) return Idle();
            if (InReading) return TurnReading(direction);
            return GoTo(Spread + (direction > 0 ? 1 : -1));
        }

        public ChronicleArchiveResult Accept()
        {
            if (InReading) return ChronicleArchiveResult.Of(ChronicleArchiveAction.Replay, ReadingId);
            if (Spread == ContentsSpread)
            {
                if (Focus < 9) return GoTo(Focus + 1);
                if (Focus == 9) return GoTo(SecretSpread);
                return ChangeOption(Focus - ContentsOptionStart, 1);
            }
            if (Spread == SecretSpread) return progress.secretUnlocked ? ChronicleArchiveResult.Of(ChronicleArchiveAction.BeginSecret, ChronicleCatalog.SecretSceneId) : Idle();
            return Focus >= 0 && Focus < 3 ? OpenReading(MemoryAt(Spread, Focus)) : Idle();
        }

        public ChronicleArchiveResult Back()
        {
            if (InReading) { ReadingId = null; Reading = null; ReadingSpread = 0; return ChronicleArchiveResult.Of(ChronicleArchiveAction.ClosedReading, "", Spread, Spread); }
            if (Spread != ContentsSpread) return GoTo(ContentsSpread);
            return ChronicleArchiveResult.Of(ChronicleArchiveAction.Exit);
        }

        // ---- direct actions (mouse clicks) ----
        public ChronicleArchiveResult OpenChapter(int chapter) => InReading ? Idle() : GoTo(chapter < 1 ? ContentsSpread : Math.Min(chapter, 9));
        public ChronicleArchiveResult OpenSecret() => InReading ? Idle() : GoTo(SecretSpread);
        public ChronicleArchiveResult OpenReading(string memoryId)
        {
            var reading = ChronicleArchive.Reading(memoryId, progress);
            if (reading == null) return Idle();    // a locked memory cannot be opened
            ReadingId = memoryId; Reading = reading; ReadingSpread = 0; Focus = 0;
            return ChronicleArchiveResult.Of(ChronicleArchiveAction.OpenedReading, memoryId, Spread, Spread);
        }
        public ChronicleArchiveResult Replay(string memoryId) => progress.IsUnlocked(memoryId) ? ChronicleArchiveResult.Of(ChronicleArchiveAction.Replay, memoryId) : Idle();
        public ChronicleArchiveResult BeginSecret() => progress.secretUnlocked ? ChronicleArchiveResult.Of(ChronicleArchiveAction.BeginSecret, ChronicleCatalog.SecretSceneId) : Idle();
        public ChronicleArchiveResult SetFocusIndex(int index) => InReading ? Idle() : SetFocus(index);
        /// <summary>Returns the archive to a spread after a replay or a rebuilt screen.</summary>
        public void Restore(int spread, string readingId) { Spread = Math.Max(0, Math.Min(SecretSpread, spread)); ReadingId = null; Reading = null; if (!string.IsNullOrEmpty(readingId)) OpenReading(readingId); }

        public string MemoryAt(int chapter, int slot) => ChronicleCatalog.MemoryFor(chapter, (ChronicleHero)slot).id;
        public int ReadingSpreadCount => Reading == null ? 0 : Reading.spreads.Count;

        // ---- internals ----
        ChronicleArchiveResult Idle() => ChronicleArchiveResult.Of(ChronicleArchiveAction.None);
        ChronicleArchiveResult SetFocus(int index)
        {
            var clamped = Math.Max(0, Math.Min(FocusCount - 1, index));
            if (clamped == Focus) return Idle();
            Focus = clamped; return ChronicleArchiveResult.Of(ChronicleArchiveAction.Moved);
        }
        ChronicleArchiveResult GoTo(int spread)
        {
            spread = Math.Max(0, Math.Min(SecretSpread, spread));
            if (spread == Spread) return Idle();
            var from = Spread; Spread = spread; Focus = 0; progress.lastChapterOpened = spread;
            return ChronicleArchiveResult.Of(ChronicleArchiveAction.TurnedPage, "", from, spread);
        }
        ChronicleArchiveResult TurnReading(int direction)
        {
            var next = Math.Max(0, Math.Min(Math.Max(0, ReadingSpreadCount - 1), ReadingSpread + (direction > 0 ? 1 : -1)));
            if (next == ReadingSpread) return Idle();
            var from = ReadingSpread; ReadingSpread = next;
            return ChronicleArchiveResult.Of(ChronicleArchiveAction.TurnedPage, ReadingId, from, next);
        }
        ChronicleArchiveResult ChangeOption(int option, int direction)
        {
            switch (option)
            {
                case 0: progress.instantText = !progress.instantText; break;
                case 1:
                {
                    var i = Array.FindIndex(SpeedSteps, s => Math.Abs(s - progress.textSpeed) < .01f); if (i < 0) i = 1;
                    progress.textSpeed = SpeedSteps[(i + (direction >= 0 ? 1 : SpeedSteps.Length - 1)) % SpeedSteps.Length]; break;
                }
                default: progress.openingMode = (progress.openingMode + (direction >= 0 ? 1 : 2)) % 3; break;
            }
            return ChronicleArchiveResult.Of(ChronicleArchiveAction.ChangedOption);
        }
    }
}
