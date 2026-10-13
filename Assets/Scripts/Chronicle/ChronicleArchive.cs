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
        public bool updated;                                // new writing since the player last opened this chapter
        public int revelations;                             // authored Ten Recollections pages written into this chapter
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
        public bool canContinue; public string continueLabel = "";   // Continue Reading: the saved Archive position
    }

    public sealed class ChronicleChapterView
    {
        public ChronicleChapterRow row = new ChronicleChapterRow();
        public readonly List<ChroniclePrefaceEntry> preface = new List<ChroniclePrefaceEntry>();
        public readonly List<ChronicleMemoryEntry> memories = new List<ChronicleMemoryEntry>();
        /// <summary>Earned Ten Recollections pages written into this chapter. Only authored, approved content ever appears here.</summary>
        public readonly List<ChronicleMemoryEntry> revelations = new List<ChronicleMemoryEntry>();
    }

    public sealed class ChronicleReadingSpread
    {
        public readonly List<ChronicleTextBlock> left = new List<ChronicleTextBlock>(), right = new List<ChronicleTextBlock>();
    }

    /// <summary>A memory's recorded pages, readable without replaying it. Built from the scene's final state, so it always reflects
    /// exactly the corrections and facts the player has discovered.</summary>
    public sealed class ChronicleMemoryReading
    {
        public ChronicleMemoryDef memory; public ChronicleIllustrationState illustration;   // memory is null for Chapter X and revelations
        public string id = "", title = "";
        public readonly List<ChronicleReadingSpread> spreads = new List<ChronicleReadingSpread>();
    }

    public static class ChronicleArchive
    {
        public static ChronicleSecretState SecretState(ChronicleProgress p) => p.secretCompleted ? ChronicleSecretState.Completed : p.secretUnlocked ? ChronicleSecretState.Unlocked : ChronicleSecretState.Locked;

        public static ChronicleEntryState StateOf(ChronicleProgress p, string memoryId) => p.IsViewed(memoryId) ? ChronicleEntryState.Viewed : p.IsUnlocked(memoryId) ? ChronicleEntryState.Unlocked : ChronicleEntryState.Locked;

        static ChronicleChapterRow Row(ChronicleChapterDef chapter, ChronicleProgress p) => new ChronicleChapterRow
        {
            number = chapter.number, id = chapter.id, title = chapter.title, teaser = chapter.teaser, act = chapter.act, actName = ChronicleCatalog.ActNames[Math.Max(0, Math.Min(3, chapter.act))],
            unlocked = p.ChapterUnlockedCount(chapter.number), viewed = p.ChapterViewedCount(chapter.number), visible = p.ChapterUnlockedCount(chapter.number) > 0,
            updated = ChronicleJournal.IsChapterUpdated(p, chapter.number), revelations = ReadableRevelations(p, chapter.number).Count
        };

        /// <summary>Earned revelations with authored, registered pages in this chapter, in chain order.</summary>
        /// <summary>Earned revelations written into this chapter that can be read or (still waiting to be presented) watched.</summary>
        public static List<ChronicleRevelationDef> ReadableRevelations(ChronicleProgress p, int chapter)
        {
            var list = new List<ChronicleRevelationDef>();
            foreach (var d in ChronicleAscensionCatalog.All) if (d.chapter == chapter && (CanRead(p, d.id) || CanWatch(p, d.id))) list.Add(d);
            return list;
        }

        /// <summary>Whether a page can be opened for reading: an unlocked memory, a completed Chapter X, or an earned revelation
        /// whose authored scene exists. Locked or unwritten pages are never readable.</summary>
        public static bool CanRead(ChronicleProgress p, string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (id == ChronicleCatalog.SecretSceneId) return p.secretCompleted;
            if (ChronicleCatalog.FindMemory(id) != null) return p.IsUnlocked(id);
            var d = ChronicleAscensionCatalog.Find(id);
            // Its pages exist once it has been presented: reading never reveals a correction before the player has seen it written.
            return d != null && p.IsRevelationEarned(d.id) && ChronicleAscensionCatalog.IsPresentable(d) && (p.IsRevelationPresented(d.id) || p.IsRevelationWatched(d.id));
        }

        /// <summary>Whether a scene can be watched again: anything readable, plus an unlocked (not yet completed) Chapter X.</summary>
        public static bool CanWatch(ChronicleProgress p, string id)
        {
            if (CanRead(p, id) || (id == ChronicleCatalog.SecretSceneId && p.secretUnlocked)) return true;
            var d = ChronicleAscensionCatalog.Find(id);   // an earned revelation still waiting to be presented can be watched once its base memories are
            return d != null && p.IsRevelationEarned(d.id) && ChronicleAscensionCatalog.IsPresentable(d) && ChronicleAscensionCatalog.GatesMet(p, d);
        }

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
            v.canContinue = ChronicleJournal.HasReadingPosition(p); v.continueLabel = v.canContinue ? ChronicleJournal.DescribeReadingPosition(p) : "";
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
            foreach (var d in ReadableRevelations(p, number))
                v.revelations.Add(new ChronicleMemoryEntry { id = d.id, hero = d.hero, state = p.IsRevelationWatched(d.id) ? ChronicleEntryState.Viewed : ChronicleEntryState.Unlocked, isNew = !p.IsRevelationWatched(d.id) });
            return v;
        }

        /// <summary>The memory's written pages for reading. Null if the memory is still locked.</summary>
        public static ChronicleMemoryReading Reading(string memoryId, ChronicleProgress p, ChroniclePageMetrics metrics = null)
        {
            p.Ensure(); metrics = metrics ?? new ChroniclePageMetrics();
            if (!CanRead(p, memoryId)) return null;
            var memory = ChronicleCatalog.FindMemory(memoryId); var revelation = ChronicleAscensionCatalog.Find(memoryId);
            var sceneId = revelation != null ? revelation.sceneId : memoryId;
            var scene = ChronicleScripts.Get(sceneId); if (scene == null) return null;
            var pose = sceneId.StartsWith("SCENE_", StringComparison.Ordinal) ? ChronicleBookPose.Closed : ChronicleBookPose.Open;   // Chapter X begins on a closed book
            var state = ChronicleSceneResolver.Resolve(scene, new ChronicleProgressContext(p), metrics, pose);
            var reading = new ChronicleMemoryReading { memory = memory, illustration = state.illustration, id = memoryId, title = memory != null ? memory.title : scene.title };
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
            // The Ten Recollections written into this memory follow its original pages, in chain order, as permanent new writing.
            if (memory != null)
                foreach (var d in ChronicleAscensionCatalog.All)
                    if (d.memoryId == memoryId && CanRead(p, d.id)) { var added = Reading(d.id, p, metrics); if (added != null) reading.spreads.AddRange(added.spreads); }
            return reading;
        }
    }

    public enum ChronicleArchiveAction { None, Moved, TurnedPage, OpenedReading, ClosedReading, Replay, BeginSecret, Exit, ChangedOption }

    public struct ChronicleArchiveResult
    {
        public ChronicleArchiveAction action; public string id; public int fromSpread, toSpread; public int option;
        public static ChronicleArchiveResult Of(ChronicleArchiveAction a, string id = "", int from = 0, int to = 0) => new ChronicleArchiveResult { action = a, id = id, fromSpread = from, toSpread = to, option = -1 };
    }

    /// <summary>
    /// Keyboard, controller and mouse navigation for the archive, as a state machine. Spread 0 is the contents; 1-9 are chapters;
    /// 10 is secret Chapter X. Opening a memory enters a reading view; Back always steps outward one level and finally exits.
    /// Locked entries can be focused (so their hint can be read) but never activated.
    /// The Archive remembers where the player last read (the living journal): the chapter, the memory and the page inside it.
    /// </summary>
    public sealed class ChronicleArchiveController
    {
        public const int ContentsSpread = 0, SecretSpread = 10, ContentsOptionStart = 10, OptionCount = 7;
        // The left-page controls, top to bottom. 0-2 keep their original positions (saved focus and tests rely on them).
        public const int OptInstant = 0, OptSpeed = 1, OptOpening = 2, OptNarration = 3, OptTextSize = 4, OptAdvance = 5, OptContinue = 6;
        public static readonly float[] SpeedSteps = { .75f, 1f, 1.25f, 1.5f, 2f };
        public static readonly float[] NarrationSteps = { 0f, .25f, .5f, .75f, 1f };

        readonly ChronicleProgress progress;
        readonly Func<ChroniclePageMetrics> metrics;
        float narrationFallback = 1f;
        /// <summary>The game's existing narration volume (the same value as Settings > Narration). The Unity layer binds these.</summary>
        public Func<float> GetNarrationVolume; public Action<float> SetNarrationVolume;

        public int Spread { get; private set; }
        public int Focus { get; private set; }
        public int ReadingSpread { get; private set; }
        public string ReadingId { get; private set; }
        public ChronicleMemoryReading Reading { get; private set; }
        public bool InReading => ReadingId != null;

        public ChronicleArchiveController(ChronicleProgress progress, int startSpread = ContentsSpread, Func<ChroniclePageMetrics> metrics = null)
        {
            this.progress = progress; this.metrics = metrics; progress.Ensure(); Spread = Math.Max(0, Math.Min(SecretSpread, startSpread));
            GetNarrationVolume = () => narrationFallback; SetNarrationVolume = v => narrationFallback = v;
        }

        public float NarrationVolume => GetNarrationVolume();

        public int FocusCount
        {
            get
            {
                if (InReading) return 1;
                if (Spread == ContentsSpread) return ContentsOptionStart + OptionCount;
                if (Spread == SecretSpread) return progress.secretCompleted ? 2 : 1;
                return 3 + ChronicleArchive.ReadableRevelations(progress, Spread).Count;
            }
        }

        public ChronicleArchiveResult Move(int dx, int dy)
        {
            if (InReading) return dx != 0 ? TurnReading(dx) : Idle();
            if (dy != 0) return SetFocus(Focus + dy);
            if (dx == 0) return Idle();
            if (Spread == ContentsSpread) return Focus >= ContentsOptionStart ? ChangeOption(Focus - ContentsOptionStart, dx) : Idle();
            if (Spread == SecretSpread && progress.secretCompleted) return SetFocus(Focus + dx);   // READ <-> WATCH AGAIN
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
            if (InReading) return ReadingId == ChronicleCatalog.SecretSceneId ? BeginSecret() : ChronicleArchiveResult.Of(ChronicleArchiveAction.Replay, ReadingId);
            if (Spread == ContentsSpread)
            {
                if (Focus < 9) return GoTo(Focus + 1);
                if (Focus == 9) return GoTo(SecretSpread);
                return ChangeOption(Focus - ContentsOptionStart, 1);
            }
            if (Spread == SecretSpread)
            {
                if (progress.secretCompleted) return Focus == 0 ? OpenReading(ChronicleCatalog.SecretSceneId) : BeginSecret();
                return progress.secretUnlocked ? ChronicleArchiveResult.Of(ChronicleArchiveAction.BeginSecret, ChronicleCatalog.SecretSceneId) : Idle();
            }
            if (Focus >= 0 && Focus < 3) return OpenReading(MemoryAt(Spread, Focus));
            var revelations = ChronicleArchive.ReadableRevelations(progress, Spread);
            if (Focus - 3 >= revelations.Count) return Idle();
            var id = revelations[Focus - 3].id;
            return ChronicleArchive.CanRead(progress, id) ? OpenReading(id) : Replay(id);   // not yet presented: watch it first
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
            var reading = ChronicleArchive.Reading(memoryId, progress, metrics?.Invoke());
            if (reading == null) return Idle();    // a locked or unwritten page cannot be opened
            ReadingId = memoryId; Reading = reading; Focus = 0;
            ReadingSpread = Math.Max(0, Math.Min(reading.spreads.Count - 1, ChronicleJournal.PageFor(progress, memoryId)));   // back to the page last read
            ChronicleJournal.RememberReading(progress, Spread, memoryId);
            return ChronicleArchiveResult.Of(ChronicleArchiveAction.OpenedReading, memoryId, Spread, Spread);
        }
        public ChronicleArchiveResult Replay(string memoryId) => ChronicleArchive.CanWatch(progress, memoryId) && memoryId != ChronicleCatalog.SecretSceneId ? ChronicleArchiveResult.Of(ChronicleArchiveAction.Replay, memoryId) : memoryId == ChronicleCatalog.SecretSceneId ? BeginSecret() : Idle();
        public ChronicleArchiveResult BeginSecret() => progress.secretUnlocked ? ChronicleArchiveResult.Of(ChronicleArchiveAction.BeginSecret, ChronicleCatalog.SecretSceneId) : Idle();
        public ChronicleArchiveResult SetFocusIndex(int index) => InReading ? Idle() : SetFocus(index);

        /// <summary>Continue Reading: back to the exact chapter, memory and page the player last read in the Archive.</summary>
        public ChronicleArchiveResult ContinueReading()
        {
            if (InReading || !ChronicleJournal.HasReadingPosition(progress)) return Idle();
            var j = progress.journal; var id = j.readingId;
            var turn = j.readingSpread != Spread ? GoTo(j.readingSpread) : Idle();
            if (!string.IsNullOrEmpty(id) && ChronicleArchive.CanRead(progress, id)) { var opened = OpenReading(id); if (turn.action == ChronicleArchiveAction.None) return opened; }
            return turn;
        }

        /// <summary>Returns the archive to a spread after a replay or a rebuilt screen.</summary>
        public void Restore(int spread, string readingId) { Spread = Math.Max(0, Math.Min(SecretSpread, spread)); ReadingId = null; Reading = null; if (!string.IsNullOrEmpty(readingId)) OpenReading(readingId); }

        /// <summary>Re-lays the open reading after the text size changed, keeping the reader near the same place.</summary>
        public void RefreshReading()
        {
            if (!InReading) return;
            var id = ReadingId; var before = ReadingSpread; var oldCount = Math.Max(1, ReadingSpreadCount);
            var reading = ChronicleArchive.Reading(id, progress, metrics?.Invoke());
            if (reading == null) { ReadingId = null; Reading = null; ReadingSpread = 0; return; }
            Reading = reading;
            ReadingSpread = Math.Max(0, Math.Min(reading.spreads.Count - 1, (int)Math.Round(before * (double)reading.spreads.Count / oldCount)));
            ChronicleJournal.RememberPage(progress, id, ReadingSpread);
        }

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
            if (spread != ContentsSpread)
            {
                if (spread != progress.journal.readingSpread) ChronicleJournal.RememberReading(progress, spread, "");   // a new chapter replaces the saved place
                ChronicleJournal.MarkChapterSeen(progress, spread);
            }
            return ChronicleArchiveResult.Of(ChronicleArchiveAction.TurnedPage, "", from, spread);
        }
        ChronicleArchiveResult TurnReading(int direction)
        {
            var next = Math.Max(0, Math.Min(Math.Max(0, ReadingSpreadCount - 1), ReadingSpread + (direction > 0 ? 1 : -1)));
            if (next == ReadingSpread) return Idle();
            var from = ReadingSpread; ReadingSpread = next;
            ChronicleJournal.RememberPage(progress, ReadingId, next);
            return ChronicleArchiveResult.Of(ChronicleArchiveAction.TurnedPage, ReadingId, from, next);
        }
        ChronicleArchiveResult ChangeOption(int option, int direction)
        {
            switch (option)
            {
                case OptContinue: return direction > 0 ? ContinueReading() : Idle();
                case OptInstant: progress.instantText = !progress.instantText; break;
                case OptSpeed:
                {
                    var i = Array.FindIndex(SpeedSteps, s => Math.Abs(s - progress.textSpeed) < .01f); if (i < 0) i = 1;
                    progress.textSpeed = SpeedSteps[(i + (direction >= 0 ? 1 : SpeedSteps.Length - 1)) % SpeedSteps.Length]; break;
                }
                case OptNarration:
                {
                    var current = NarrationVolume; var i = 0;
                    for (var k = 0; k < NarrationSteps.Length; k++) if (Math.Abs(NarrationSteps[k] - current) < Math.Abs(NarrationSteps[i] - current)) i = k;
                    SetNarrationVolume(NarrationSteps[Math.Max(0, Math.Min(NarrationSteps.Length - 1, i + (direction >= 0 ? 1 : -1)))]); break;
                }
                case OptTextSize: progress.journal.textScale = ChronicleJournal.NextTextScale(progress.journal.textScale, direction); RefreshReading(); break;
                case OptAdvance: progress.journal.manualAdvance = !progress.journal.manualAdvance; break;
                default: progress.openingMode = (progress.openingMode + (direction >= 0 ? 1 : 2)) % 3; break;
            }
            var r = ChronicleArchiveResult.Of(ChronicleArchiveAction.ChangedOption); r.option = option; return r;
        }
    }
}
