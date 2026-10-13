using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace GildedFate.Chronicle.Tests
{
    /// <summary>The living journal: story continuity, reading positions, page rhythm, manual advancement, the finishing moment,
    /// Chapter X reading, text size reflow, updated-page marks and save compatibility.</summary>
    [TestFixture]
    public class ChronicleJournalTests
    {
        static readonly ChronicleHero[] Heroes = { ChronicleHero.Vanguard, ChronicleHero.Hexer, ChronicleHero.Reaper };

        sealed class Book : IChronicleStage, IChronicleDialogueStage
        {
            public readonly List<string> events = new List<string>(); public float time; public float voiceSeconds;
            public float BookActionDuration(ChronicleBookAction a) => a == ChronicleBookAction.Open ? 1.6f : a == ChronicleBookAction.Close ? 1.3f : .9f;
            public void BookAction(ChronicleBookAction a, ChronicleNarratorState n) { events.Add(a + "@" + time.ToString("0.00")); }
            public void SetBookPoseImmediate(ChronicleBookPose p, int s) { }
            public void PlayAudio(string c, float v) { }
            public void StopAllAudio() { }
            public void MagicEffect(string e, float x, float y, float i) { }
            public void SceneStarted(string id) { }
            public void SceneEnded(string id, bool skipped) { events.Add("end"); }
            public void SetDialoguePaused(bool paused) { }
            public ChronicleDialoguePlayback BeginDialogue(string sceneId, string id, string text, string speaker, ChronicleNarratorState n)
            {
                if (voiceSeconds <= 0f || text != "First.") return null;
                var started = time; var duration = voiceSeconds;
                var timing = new ChronicleDialogueTiming { ready = true, text = text, durationSeconds = duration, words = new[] { new ChronicleWordTiming { text = "First.", charStart = 0, charLength = 6, startSeconds = .1f, endSeconds = duration * .6f } } };
                return new ChronicleDialoguePlayback { timing = timing, positionSeconds = () => time - started };
            }
            public int Count(string prefix) => events.Count(e => e.StartsWith(prefix, StringComparison.Ordinal));
        }

        static float Run(ChronicleSequencer seq, Book book = null, float limit = 400f, float dt = .05f)
        {
            var t = 0f; while (!seq.Finished && t < limit) { seq.Update(dt); t += dt; if (book != null) book.time = t; }
            return t;
        }

        static ChronicleScene TwoPages() => new ChronicleSceneBuilder("test", "test").Open().Say("First.").Turn().Say("Second.").Close().Finish();

        static ChronicleProgress ActCleared(ChronicleHero hero, int act)
        {
            var p = new ChronicleProgress(); for (var a = 1; a <= act; a++) p.RegisterBossDefeated(hero, a, a * 18);
            p.RegisterRunEnded(hero, false, act, act * 18, "ended-" + hero + act); return p;   // the run then ended, as real runs do
        }

        // ---------------------------------------------------------------- story continuity
        [Test] public void TheStoryContinuesFromActIThroughActIII()
        {
            var p = new ChronicleProgress();
            p.RegisterBossDefeated(ChronicleHero.Vanguard, 1, 18);
            var actOne = ChronicleMilestoneScenes.Memories(p, ChronicleHero.Vanguard, 1, false);
            Assert.IsNull(ChronicleJournal.PlanContinuation(p, actOne[0]), "the very first chapter opens the closed book as it always has");
            foreach (var id in actOne) { p.CommitMemoryViewed(id); ChronicleJournal.RecordStoryReached(p, id); }
            Assert.AreEqual(3, p.journal.storySpread);

            p.RegisterBossDefeated(ChronicleHero.Vanguard, 2, 36);
            var actTwo = ChronicleMilestoneScenes.Memories(p, ChronicleHero.Vanguard, 2, false);
            var plan = ChronicleJournal.PlanContinuation(p, actTwo[0]);
            Assert.AreEqual(3, plan.fromSpread); Assert.AreEqual(4, plan.toSpread); Assert.AreEqual(3, plan.startSpread); Assert.AreEqual(1, plan.Turns); Assert.IsFalse(plan.Backward);
            foreach (var id in actTwo) { p.CommitMemoryViewed(id); ChronicleJournal.RecordStoryReached(p, id); }
            Assert.AreEqual(6, p.journal.storySpread);

            p.RegisterBossDefeated(ChronicleHero.Vanguard, 3, 54);
            var actThree = ChronicleMilestoneScenes.Memories(p, ChronicleHero.Vanguard, 3, false);
            plan = ChronicleJournal.PlanContinuation(p, actThree[0]);
            Assert.AreEqual(6, plan.fromSpread); Assert.AreEqual(7, plan.toSpread); Assert.IsTrue(plan.Moves);
            Assert.AreEqual(string.Join(",", actThree), string.Join(",", ChronicleMilestoneScenes.Memories(p, ChronicleHero.Vanguard, 3, false)), "the act scenes themselves still play in full");
        }

        [Test] public void LongJumpsRiffleAndTurnsCanGoBackward()
        {
            var p = new ChronicleProgress(); p.journal.storySpread = 1;
            var plan = ChronicleJournal.PlanContinuation(p, "MEM_25");
            Assert.AreEqual(9, plan.toSpread); Assert.AreEqual(6, plan.startSpread); Assert.AreEqual(ChronicleJournal.MaxVisibleTurns, plan.Turns);
            var revelation = ChronicleAscensionCatalog.At(ChronicleHero.Hexer, 2); var before = revelation.chapter;
            try
            {
                revelation.chapter = 2; p.journal.storySpread = 6;   // a future recollection written into an earlier chapter
                plan = ChronicleJournal.PlanContinuation(p, revelation.sceneId);
                Assert.IsTrue(plan.Backward); Assert.AreEqual(2, plan.toSpread); Assert.AreEqual(5, plan.startSpread);
            }
            finally { revelation.chapter = before; }
            Assert.IsNull(ChronicleJournal.PlanContinuation(p, ChronicleMilestoneScenes.VictoryId), "a scene that is not a journal page never moves the book");
            Assert.IsTrue(ChronicleJournal.PlanContinuation(p, ChronicleAscensionCatalog.At(ChronicleHero.Hexer, 2).sceneId).Moves, "a recollection returns to its own chapter");
        }

        [Test] public void EndingsAndTheOpeningAreNotJournalPagesAndNeverMoveTheStory()
        {
            var p = new ChronicleProgress(); p.journal.storySpread = 4;
            foreach (var id in new[] { ChronicleMilestoneScenes.VictoryId, ChronicleMilestoneScenes.DefeatId, ChronicleCatalog.OpeningSceneId, ChronicleCatalog.OpeningShortSceneId, "nonsense" })
            { Assert.IsFalse(ChronicleJournal.RecordStoryReached(p, id)); Assert.AreEqual(-1, ChronicleJournal.SpreadOf(id)); }
            Assert.AreEqual(4, p.journal.storySpread); Assert.AreEqual(10, ChronicleJournal.SpreadOf(ChronicleCatalog.SecretSceneId));
        }

        [Test] public void StoryAndReadingPositionsStayIndependent()
        {
            var p = ActCleared(ChronicleHero.Hexer, 3);
            ChronicleJournal.RecordStoryReached(p, "MEM_17");
            var a = new ChronicleArchiveController(p); a.OpenChapter(2); a.OpenReading("MEM_05");
            Assert.AreEqual(6, p.journal.storySpread, "reading in the Archive never moves the story");
            Assert.AreEqual(2, p.journal.readingSpread); Assert.AreEqual("MEM_05", p.journal.readingId);
            ChronicleJournal.RecordStoryReached(p, "MEM_20");
            Assert.AreEqual(2, p.journal.readingSpread, "the story never moves the reader's place"); Assert.AreEqual("MEM_05", p.journal.readingId);
        }

        // ---------------------------------------------------------------- reading positions
        // Small paper so readings span several spreads; the hand scales with the Text Size preference, as on screen.
        static Func<ChroniclePageMetrics> Tight(ChronicleProgress p, float paper = 70f) => () => new ChroniclePageMetrics { textHeight = paper, fontSize = 22f * p.journal.textScale, lineHeight = 31f * p.journal.textScale };

        [Test] public void ContinueReadingReturnsToTheExactMemoryAndPage()
        {
            var p = ActCleared(ChronicleHero.Reaper, 3); var a = new ChronicleArchiveController(p, 0, Tight(p));
            a.OpenChapter(7); a.OpenReading("MEM_21"); Assert.Greater(a.ReadingSpreadCount, 2, "tight pages give a long reading");
            a.Page(1); a.Page(1); Assert.AreEqual(2, a.ReadingSpread);
            a.Back(); a.Back(); Assert.AreEqual(0, a.Spread);
            var copy = ChronicleProgressTests.FromJson(ChronicleProgressTests.ToJson(p)); copy.Ensure();   // a new session
            var b = new ChronicleArchiveController(copy, 0, Tight(copy));
            Assert.IsTrue(ChronicleArchive.Contents(copy).canContinue);
            StringAssertContains(ChronicleArchive.Contents(copy).continueLabel, "Chapter VII");
            var r = b.ContinueReading();
            Assert.AreEqual(ChronicleArchiveAction.TurnedPage, r.action); Assert.AreEqual(0, r.fromSpread); Assert.AreEqual(7, r.toSpread);
            Assert.AreEqual("MEM_21", b.ReadingId); Assert.AreEqual(2, b.ReadingSpread);
        }

        [Test] public void ReopeningAMemoryReturnsToItsLastPageAndReplaysRestoreIt()
        {
            var p = ActCleared(ChronicleHero.Vanguard, 1); var a = new ChronicleArchiveController(p, 1, Tight(p));
            a.OpenReading("MEM_01"); a.Page(1); a.Back();
            a.OpenReading("MEM_01"); Assert.AreEqual(1, a.ReadingSpread, "each memory remembers its own page");
            a.Restore(1, "MEM_01"); Assert.AreEqual(1, a.ReadingSpread, "returning from Watch Again lands on the same page");
            a.Back(); a.OpenReading("MEM_04".Replace("4", "1")); Assert.AreEqual(1, a.ReadingSpread);
        }

        [Test] public void NothingToContinueOnAFreshJournalOrAfterAReset()
        {
            var p = new ChronicleProgress(); var a = new ChronicleArchiveController(p);
            Assert.IsFalse(ChronicleArchive.Contents(p).canContinue); Assert.AreEqual(ChronicleArchiveAction.None, a.ContinueReading().action);
            var q = ActCleared(ChronicleHero.Hexer, 1); var b = new ChronicleArchiveController(q); b.OpenChapter(1); b.OpenReading("MEM_02");
            q.ResetAll(); q.Ensure();
            Assert.IsFalse(ChronicleJournal.HasReadingPosition(q), "a reset journal never points at locked pages");
        }

        [Test] public void ContinueReadingIsReachableWithAControllerFromTheOptions()
        {
            var p = ActCleared(ChronicleHero.Vanguard, 1); var a = new ChronicleArchiveController(p); a.OpenChapter(3); a.OpenReading("MEM_07"); a.Back(); a.Back();
            a.SetFocusIndex(ChronicleArchiveController.ContentsOptionStart + ChronicleArchiveController.OptContinue);
            var r = a.Accept(); Assert.AreEqual(ChronicleArchiveAction.TurnedPage, r.action); Assert.AreEqual("MEM_07", a.ReadingId);
        }

        // ---------------------------------------------------------------- text size
        [Test] public void LargerTextReflowsOntoMorePagesWithoutOverflow()
        {
            var p = ActCleared(ChronicleHero.Hexer, 3); var hexer = ChronicleCatalog.Memories.Where(m => m.hero == ChronicleHero.Hexer && p.IsUnlocked(m.id)).Select(m => m.id).ToList(); Assert.Greater(hexer.Count, 5);
            int Pages() => hexer.Sum(id => ChronicleArchive.Reading(id, p, Tight(p, 330f)()).spreads.Count);
            var standard = Pages();
            var a = new ChronicleArchiveController(p, 0, Tight(p, 330f)); a.OpenChapter(5); a.OpenReading("MEM_14"); a.Back(); a.Back();
            for (var i = 0; i < 2; i++) { a.SetFocusIndex(ChronicleArchiveController.ContentsOptionStart + ChronicleArchiveController.OptTextSize); a.Accept(); }
            Assert.AreEqual(1.3f, p.journal.textScale, .001f);
            var largest = Pages();
            Assert.Greater(largest, standard, "bigger writing reflows onto more pages");
            foreach (var id in hexer) Assert.IsTrue(ChronicleArchive.Reading(id, p, Tight(p, 330f)()).spreads.All(s => s.left.Count + s.right.Count > 0), id + " has no empty pages");
            a.ContinueReading(); Assert.AreEqual("MEM_14", a.ReadingId); Assert.IsTrue(a.ReadingSpread < a.ReadingSpreadCount, "the saved page is clamped to the new layout");
        }

        // ---------------------------------------------------------------- page rhythm
        [Test] public void BeatsFollowThePassageAndRevisitsAreQuicker()
        {
            var plain = ChronicleRhythm.Beat("The kingdom stood.", .35f, false, false);
            Assert.Greater(ChronicleRhythm.Beat("Who was the fourth?", .35f, false, false), plain);
            Assert.Greater(ChronicleRhythm.Beat("There were four\u2014", .35f, false, false), ChronicleRhythm.Beat("Who was the fourth?", .35f, false, false));
            Assert.Less(ChronicleRhythm.Beat("And then,", .35f, false, false), plain);
            Assert.Less(ChronicleRhythm.Beat("The kingdom stood.", .35f, true, false), plain);
            Assert.AreEqual(plain, ChronicleRhythm.Beat("The kingdom stood.", .35f, false, false), "deterministic");
            Assert.AreEqual(0f, ChronicleRhythm.Beat("", 0f, false, false));
        }

        [Test] public void APageNeverTurnsWhileItsNarrationIsStillSpeaking()
        {
            var book = new Book { voiceSeconds = 3f };
            var seq = new ChronicleSequencer(TwoPages(), book, new ChronicleStaticContext(), null);
            seq.Start(ChronicleBookPose.Closed); Run(seq, book);
            var turn = book.events.First(e => e.StartsWith("TurnPageForward", StringComparison.Ordinal));
            Assert.GreaterOrEqual(float.Parse(turn.Split('@')[1], System.Globalization.CultureInfo.InvariantCulture), 1.6f + 3f, "the recording finishes before the sheet lifts");
        }

        [Test] public void PageBreaksBreatheBeforeTurningAndAutomaticModeNeverWaits()
        {
            var first = new Book(); var seq = new ChronicleSequencer(TwoPages(), first, new ChronicleStaticContext(), null); seq.Start(ChronicleBookPose.Closed);
            var seconds = Run(seq, first); Assert.IsTrue(seq.Finished, "automatic advancement finishes on its own"); Assert.Less(seconds, 30f);
            var revisit = new Book(); var again = new ChronicleSequencer(TwoPages(), revisit, new ChronicleStaticContext(), null, null, null, true); again.Start(ChronicleBookPose.Closed);
            Assert.Less(Run(again, revisit), seconds, "revisits keep the rhythm but are quicker");
            Assert.AreEqual(1, first.Count("TurnPageForward")); Assert.AreEqual(1, first.Count("Close"));
        }

        // ---------------------------------------------------------------- manual advancement and pause
        [Test] public void ManualAdvancementWaitsAtEachPageBreakUntilThePlayerTurns()
        {
            var book = new Book(); var seq = new ChronicleSequencer(TwoPages(), book, new ChronicleStaticContext(), null, new ChronicleSettings { manualAdvance = true });
            seq.Start(ChronicleBookPose.Closed); Run(seq, book, 60f);
            Assert.IsFalse(seq.Finished); Assert.IsTrue(seq.AwaitingAdvance); Assert.IsTrue(seq.State.awaitingAdvance);
            Assert.AreEqual(0, book.Count("TurnPageForward"), "a minute later the page is still waiting");
            Assert.AreEqual(1, seq.State.blocks.Count); Assert.IsTrue(seq.State.blocks[0].complete);
            Assert.IsTrue(seq.Advance()); Assert.IsFalse(seq.Advance(), "one press, one turn");
            Run(seq, book, 60f);
            Assert.AreEqual(1, book.Count("TurnPageForward")); Assert.IsTrue(seq.AwaitingAdvance, "the book also waits before closing");
            seq.Advance(); Run(seq, book, 60f);
            Assert.IsTrue(seq.Finished); Assert.AreEqual(1, book.Count("Close"));
        }

        [Test] public void AdvanceNeverCutsWritingShortAndSkipStillWorksWhileWaiting()
        {
            var seq = new ChronicleSequencer(TwoPages(), new Book(), new ChronicleStaticContext(), null, new ChronicleSettings { manualAdvance = true });
            seq.Start(ChronicleBookPose.Closed); seq.Update(.05f);
            Assert.IsFalse(seq.Advance(), "nothing is waiting yet"); Assert.IsFalse(seq.AwaitingAdvance);
            Run(seq, null, 30f); Assert.IsTrue(seq.AwaitingAdvance);
            seq.Skip(); Assert.IsTrue(seq.Finished); Assert.IsFalse(seq.State.awaitingAdvance);
            Assert.IsTrue(seq.State.transcript.All(b => b.complete));
        }

        [Test] public void APassageRunningOntoFreshPaperWaitsForThePlayerInManualMode()
        {
            var b = new ChronicleSceneBuilder("test", "test").Open(); for (var i = 0; i < 6; i++) b.Say("A long passage that fills the page completely, line after line after line, until the paper runs out.");
            var metrics = new ChroniclePageMetrics { textHeight = 200f, autoPageTurns = true };
            var book = new Book(); var seq = new ChronicleSequencer(b.Finish(), book, new ChronicleStaticContext(), null, new ChronicleSettings { manualAdvance = true }, metrics);
            seq.Start(ChronicleBookPose.Closed); Run(seq, book, 60f);
            Assert.IsTrue(seq.AwaitingAdvance); Assert.IsTrue(seq.State.awaitingTurn, "the outgoing page stays visible"); Assert.AreEqual(0, book.Count("TurnPageForward"));
            seq.Advance(); Run(seq, book, 2f); Assert.AreEqual(1, book.Count("TurnPageForward"));
            var auto = new ChronicleSequencer(b.Finish(), new Book(), new ChronicleStaticContext(), null, null, metrics); auto.Start(ChronicleBookPose.Closed); Run(auto);
            Assert.IsTrue(auto.Finished, "automatic mode is unchanged");
        }

        [Test] public void PausingFreezesWritingAndTheDirectorReportsIt()
        {
            var d = new ChronicleDirector(); Assert.IsTrue(d.TryPlay(TwoPages(), new Book(), new ChronicleStaticContext(), null, null, ChronicleBookPose.Closed, false));
            d.Update(2f); d.Pause(); Assert.IsTrue(d.Paused);
            var written = d.Current.State.transcript.Sum(x => x.VisibleLength); var elapsed = d.Current.Elapsed;
            for (var i = 0; i < 50; i++) d.Update(.5f);
            Assert.AreEqual(written, d.Current.State.transcript.Sum(x => x.VisibleLength)); Assert.AreEqual(elapsed, d.Current.Elapsed);
            d.Resume(); Assert.IsFalse(d.Paused); d.Update(.5f); Assert.Greater(d.Current.Elapsed, elapsed);
        }

        // ---------------------------------------------------------------- the finishing moment
        static ChronicleSequencer Memory(ChronicleProgress p, string id, bool replay, out RecordingChronicleStage stage, ChronicleSettings settings = null)
        {
            stage = new RecordingChronicleStage();
            var seq = new ChronicleSequencer(ChronicleScripts.Get(id), stage, new ChronicleProgressContext(p), new ChronicleProgressSink(p), settings, null, replay);
            seq.Start(ChronicleBookPose.Open); return seq;
        }

        [Test] public void ANewlyWrittenChapterHoldsWhileItsInkDries()
        {
            var p = ActCleared(ChronicleHero.Vanguard, 1); var seq = Memory(p, "MEM_01", false, out _);
            var seen = new List<float>(); while (!seq.Finished) { seq.Update(.05f); if (seq.State.completion >= 0f) seen.Add(seq.State.completion); }
            Assert.Greater(seen.Count, 10, "the finished page is held"); Assert.AreEqual(0f, seen.First(), .2f); Assert.AreEqual(1f, seen.Last(), .001f);
            Assert.IsTrue(p.IsViewed("MEM_01"));
            var again = Memory(p, "MEM_01", true, out _); var shown = false; while (!again.Finished) { again.Update(.05f); shown |= again.State.completion >= 0f; }
            Assert.IsFalse(shown, "revisits never replay the finishing moment");
        }

        [Test] public void TheFinishingMomentNeverAppearsOnSkipsReadingsOrNonJournalScenes()
        {
            var p = ActCleared(ChronicleHero.Hexer, 1); var seq = Memory(p, "MEM_02", false, out _); seq.Update(.5f); seq.Skip();
            Assert.AreEqual(-1f, seq.State.completion);
            var reading = ChronicleSceneResolver.Resolve(ChronicleScripts.Get("MEM_02"), new ChronicleProgressContext(p)); Assert.AreEqual(-1f, reading.completion);
            var opening = new ChronicleSequencer(ChronicleScripts.Get(ChronicleCatalog.OpeningShortSceneId), new NullChronicleStage(), new ChronicleProgressContext(p), new ChronicleProgressSink(p));
            opening.Start(ChronicleBookPose.Closed); var shown = false; while (!opening.Finished) { opening.Update(.05f); shown |= opening.State.completion >= 0f; }
            Assert.IsFalse(shown);
        }

        // ---------------------------------------------------------------- Chapter X reading
        [Test] public void ACompletedChapterXCanBeReadAsPagesAndWatchedAgain()
        {
            var p = ChronicleAscensionTestKit.CompleteEveryTrueHistory(ActCleared(ChronicleHero.Vanguard, 3));
            foreach (var h in Heroes) p.RegisterRunEnded(h, true, 3, 54, "w" + h);
            Assert.IsTrue(p.secretUnlocked); Assert.IsFalse(ChronicleArchive.CanRead(p, ChronicleCatalog.SecretSceneId), "not readable before it has been watched");
            var a = new ChronicleArchiveController(p, 10); Assert.AreEqual(1, a.FocusCount); Assert.AreEqual(ChronicleArchiveAction.BeginSecret, a.Accept().action);
            p.CommitSecretCompleted();
            a = new ChronicleArchiveController(p, 10); Assert.AreEqual(2, a.FocusCount);
            var read = a.Accept(); Assert.AreEqual(ChronicleArchiveAction.OpenedReading, read.action); Assert.AreEqual(ChronicleCatalog.SecretSceneId, a.ReadingId);
            Assert.Greater(a.ReadingSpreadCount, 0);
            Assert.IsTrue(a.Reading.spreads.SelectMany(s => s.left.Concat(s.right)).Any(b => b.text == "I AM THE FORGOTTEN OBSERVER."), "the established text, unchanged");
            Assert.AreEqual(ChronicleArchiveAction.BeginSecret, a.Accept().action, "from the pages, Accept watches it again");
            a.Back(); a.Move(1, 0); Assert.AreEqual(1, a.Focus); Assert.AreEqual(ChronicleArchiveAction.BeginSecret, a.Accept().action);
            Assert.AreEqual(1, p.corrections.Count(c => c == "CORR_10"), "reading grants nothing twice");
        }

        // ---------------------------------------------------------------- recently updated pages
        [Test] public void ChaptersShowNewWritingUntilTheyAreOpened()
        {
            var p = ActCleared(ChronicleHero.Vanguard, 1);
            Assert.IsFalse(ChronicleArchive.Contents(p).chapters.Any(c => c.updated), "unlocking alone writes nothing new");
            p.CommitMemoryViewed("MEM_07");   // writes Vanguard's memory and corrects chapter III
            Assert.IsTrue(ChronicleArchive.Contents(p).chapters[2].updated);
            Assert.IsFalse(ChronicleArchive.Contents(p).chapters[1].updated);
            new ChronicleArchiveController(p).OpenChapter(3);
            Assert.IsFalse(ChronicleArchive.Contents(p).chapters[2].updated);
            p.CommitMemoryViewed("MEM_07"); Assert.IsFalse(ChronicleJournal.IsChapterUpdated(p, 3), "rewatching changes nothing");
        }

        // ---------------------------------------------------------------- future recollections
        [Test] public void UnwrittenRecollectionsNeverAppearAsPages()
        {
            var p = ChronicleAscensionTestKit.CompleteEveryTrueHistory(ActCleared(ChronicleHero.Reaper, 3));
            foreach (var d in ChronicleAscensionCatalog.All) { Assert.IsFalse(ChronicleArchive.CanRead(p, d.id)); Assert.IsNull(ChronicleArchive.Reading(d.id, p)); }
            for (var c = 1; c <= 9; c++) { Assert.AreEqual(0, ChronicleArchive.Chapter(c, p).revelations.Count); Assert.AreEqual(3, new ChronicleArchiveController(p, c).FocusCount); }
            Assert.AreEqual(ChronicleArchiveAction.None, new ChronicleArchiveController(p).OpenReading("ASC_VAN_01").action);
        }

        // ---------------------------------------------------------------- preferences
        [Test] public void TheLeftPageOptionsWorkAndSave()
        {
            var p = new ChronicleProgress(); var narration = .5f;
            var a = new ChronicleArchiveController(p) { GetNarrationVolume = () => narration, SetNarrationVolume = v => narration = v };
            a.SetFocusIndex(ChronicleArchiveController.ContentsOptionStart + ChronicleArchiveController.OptNarration);
            var r = a.Move(1, 0); Assert.AreEqual(ChronicleArchiveAction.ChangedOption, r.action); Assert.AreEqual(ChronicleArchiveController.OptNarration, r.option); Assert.AreEqual(.75f, narration, .001f);
            a.Move(-1, 0); a.Move(-1, 0); a.Move(-1, 0); a.Move(-1, 0); Assert.AreEqual(0f, narration, .001f, "clamps at muted");
            a.SetFocusIndex(ChronicleArchiveController.ContentsOptionStart + ChronicleArchiveController.OptAdvance); a.Accept();
            Assert.IsTrue(p.journal.manualAdvance); Assert.IsTrue(p.ToSettings(false, false).manualAdvance);
            a.SetFocusIndex(ChronicleArchiveController.ContentsOptionStart + ChronicleArchiveController.OptTextSize); a.Move(-1, 0); Assert.AreEqual(.85f, p.journal.textScale, .001f);
            Assert.AreEqual(17, a.FocusCount);
            var copy = ChronicleProgressTests.FromJson(ChronicleProgressTests.ToJson(p)); copy.Ensure();
            Assert.IsTrue(copy.journal.manualAdvance); Assert.AreEqual(.85f, copy.journal.textScale, .001f);
            copy.ResetAll(); copy.Ensure(); Assert.IsTrue(copy.journal.manualAdvance, "resetting progress keeps reading preferences"); Assert.AreEqual(.85f, copy.journal.textScale, .001f);
        }

        [Test] public void EveryContentsItemIsReachableWithKeysAndLockedPagesStayHidden()
        {
            var p = ActCleared(ChronicleHero.Vanguard, 1); var a = new ChronicleArchiveController(p);
            for (var i = 0; i < 40; i++) a.Move(0, 1);
            Assert.AreEqual(a.FocusCount - 1, a.Focus);
            Assert.AreEqual(ChronicleArchiveAction.None, new ChronicleArchiveController(p, 9).OpenReading("MEM_25").action, "locked memories never open");
            Assert.IsFalse(ChronicleArchive.Contents(p).chapters[8].visible);
        }

        // ---------------------------------------------------------------- save compatibility
        [Test] public void AnOlderSaveStartsTheStoryWhereThePlayerLeftItAndMarksNothingNew()
        {
            var json = "{\"version\":2,\"unlockedMemories\":[\"MEM_01\",\"MEM_02\",\"MEM_04\",\"MEM_10\"],\"viewedMemories\":[\"MEM_01\",\"MEM_10\"],\"corrections\":[\"CORR_04\"],\"textSpeed\":1.5}";
            var p = ChronicleProgressTests.FromJson(json); p.Ensure();
            Assert.AreEqual(1, p.journal.version); Assert.AreEqual(4, p.journal.storySpread, "the latest chapter the player watched");
            Assert.IsFalse(ChronicleArchive.Contents(p).chapters.Any(c => c.updated));
            Assert.AreEqual(1f, p.journal.textScale); Assert.IsFalse(p.journal.manualAdvance); Assert.AreEqual(1.5f, p.textSpeed, .001f);
            Assert.AreEqual("CORR_04", string.Join(",", p.corrections)); Assert.AreEqual(4, p.TotalUnlocked);
            var v1 = ChronicleProgressTests.FromJson("{\"version\":1,\"secretUnlocked\":true,\"viewedMemories\":[\"MEM_25\"],\"unlockedMemories\":[\"MEM_25\"]}"); v1.Ensure();
            Assert.IsTrue(v1.secretUnlocked && v1.ascension.secretGrandfathered); Assert.AreEqual(9, v1.journal.storySpread);
        }

        [Test] public void DamagedJournalDataIsRepaired()
        {
            var p = new ChronicleProgress { journal = null }; p.Ensure(); Assert.IsNotNull(p.journal);
            p.journal.textScale = 9f; p.journal.storySpread = 42; p.journal.readingSpread = -3; p.journal.pages = null; p.journal.readingId = null; p.Ensure();
            Assert.AreEqual(1f, p.journal.textScale); Assert.AreEqual(10, p.journal.storySpread); Assert.AreEqual(0, p.journal.readingSpread);
            Assert.IsNotNull(p.journal.pages); Assert.AreEqual("", p.journal.readingId);
        }

        static void StringAssertContains(string text, string part) => Assert.IsTrue(text != null && text.Contains(part), "\"" + text + "\" should contain \"" + part + "\"");
    }
}
