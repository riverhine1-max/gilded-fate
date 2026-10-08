using System;
using System.Linq;
using NUnit.Framework;

namespace GildedFate.Chronicle.Tests
{
    /// <summary>What the archive shows and how every input moves through it.</summary>
    [TestFixture]
    public class ChronicleArchiveTests
    {
        static ChronicleProgress Everything()
        {
            var p = new ChronicleProgress();
            foreach (ChronicleHero h in Enum.GetValues(typeof(ChronicleHero))) p.RegisterRunEnded(h, true, 3, 54, "w" + h);
            return p;
        }

        // ---------------------------------------------------------------- views
        [Test] public void TheContentsListsNineChaptersAndTheLockedSecretOne()
        {
            var v = ChronicleArchive.Contents(new ChronicleProgress());
            Assert.AreEqual(9, v.chapters.Count); Assert.AreEqual(ChronicleSecretState.Locked, v.secret);
            Assert.AreEqual(0, v.memoriesUnlocked); Assert.AreEqual(0, v.newCount); Assert.AreEqual("Before the First Thread", v.chapters[0].title);
            Assert.IsTrue(v.chapters.All(c => !c.visible && c.unlocked == 0));
        }

        [Test] public void CountersTrackUnlockedViewedNewAndCorrections()
        {
            var p = Everything(); p.CommitMemoryViewed("MEM_07"); p.CommitMemoryViewed("MEM_05");
            var v = ChronicleArchive.Contents(p);
            Assert.AreEqual(27, v.memoriesUnlocked); Assert.AreEqual(2, v.memoriesViewed); Assert.AreEqual(25, v.newCount); Assert.AreEqual(2, v.correctionsFound);
            Assert.AreEqual(ChronicleSecretState.Unlocked, v.secret);
            p.CommitSecretCompleted(); Assert.AreEqual(ChronicleSecretState.Completed, ChronicleArchive.Contents(p).secret);
        }

        [Test] public void ALockedChapterRevealsNothingBeyondItsTeaserAndHints()
        {
            var v = ChronicleArchive.Chapter(3, new ChronicleProgress());
            Assert.IsFalse(v.row.visible); Assert.AreEqual(0, v.preface.Count, "the written pages stay hidden");
            Assert.AreEqual(3, v.memories.Count);
            foreach (var m in v.memories) { Assert.AreEqual(ChronicleEntryState.Locked, m.state); Assert.AreEqual("", m.title, "locked titles can spoil, so they are hidden"); Assert.IsTrue(m.hint.Length > 5); Assert.IsFalse(m.isNew); }
        }

        [Test] public void NoLockedTextSpoilsTheFourthCompanionOrTheNarrator()
        {
            var p = new ChronicleProgress();
            for (var c = 1; c <= 9; c++)
            {
                var v = ChronicleArchive.Chapter(c, p);
                var text = (v.row.teaser + " " + string.Join(" ", v.memories.Select(m => m.hint + m.title))).ToLowerInvariant();
                Assert.IsFalse(text.Contains("fourth"), "chapter " + c); Assert.IsFalse(text.Contains("observer"), "chapter " + c); Assert.IsFalse(text.Contains("narrator"), "chapter " + c);
            }
        }

        [Test] public void AnUnlockedMemoryShowsItsTitleAndNewUntilWatched()
        {
            var p = new ChronicleProgress(); p.RegisterBossDefeated(ChronicleHero.Vanguard, 1, 18);
            var v = ChronicleArchive.Chapter(3, p);
            Assert.IsTrue(v.row.visible); Assert.AreEqual(1, v.row.unlocked);
            var m = v.memories[0]; Assert.AreEqual(ChronicleHero.Vanguard, m.hero); Assert.AreEqual("The Empty Place", m.title); Assert.IsTrue(m.isNew); Assert.AreEqual(ChronicleEntryState.Unlocked, m.state);
            p.CommitMemoryViewed("MEM_07");
            var after = ChronicleArchive.Chapter(3, p).memories[0]; Assert.AreEqual(ChronicleEntryState.Viewed, after.state); Assert.IsFalse(after.isNew);
        }

        [Test] public void APageShowsTheOriginalSentenceUntilItsCorrectionIsDiscovered()
        {
            var p = Everything();
            var before = ChronicleArchive.Chapter(3, p).preface.First(e => e.correctionId == "CORR_01");
            Assert.AreEqual("Three travelers stood together.", before.text); Assert.IsFalse(before.struck); Assert.AreEqual("There were four.", before.corrected);
            p.CommitMemoryViewed("MEM_07");
            var after = ChronicleArchive.Chapter(3, p).preface.First(e => e.correctionId == "CORR_01");
            Assert.IsTrue(after.struck); Assert.AreEqual("Three travelers stood together.", after.text, "the original is never lost"); Assert.AreEqual("There were four.", after.corrected);
        }

        [Test] public void SeveralCorrectionsInOneChapterAreIndependent()
        {
            var p = Everything(); p.CommitMemoryViewed("MEM_06");
            var page = ChronicleArchive.Chapter(2, p).preface;
            Assert.IsTrue(page.First(e => e.correctionId == "CORR_03").struck); Assert.IsFalse(page.First(e => e.correctionId == "CORR_02").struck);
            Assert.AreEqual(1, page.Count(e => e.struck)); Assert.IsTrue(page.Any(e => !e.hasCorrection), "passages that were always true stay unchanged");
        }

        [Test] public void ACorrectionAppearsOnItsPageEvenIfTheChapterWasReachedOutOfOrder()
        {
            var p = Everything(); p.CommitMemoryViewed("MEM_24"); p.CommitMemoryViewed("MEM_17");
            Assert.IsTrue(ChronicleArchive.Chapter(8, p).preface.First(e => e.correctionId == "CORR_08").struck);
            Assert.IsTrue(ChronicleArchive.Chapter(6, p).preface.First(e => e.correctionId == "CORR_06").struck);
            Assert.IsFalse(ChronicleArchive.Chapter(4, p).preface.First(e => e.correctionId == "CORR_04").struck);
        }

        [Test] public void TheNarratorsOwnPageIsCorrectedByTheFinale()
        {
            var p = Everything();
            Assert.IsFalse(ChronicleArchive.Contents(p).prologue.First(e => e.correctionId == "CORR_10").struck);
            p.CommitSecretCompleted();
            var entry = ChronicleArchive.Contents(p).prologue.First(e => e.correctionId == "CORR_10");
            Assert.IsTrue(entry.struck); Assert.AreEqual("The narrator is the Forgotten Observer.", entry.corrected);
        }

        // ---------------------------------------------------------------- reading
        [Test] public void ALockedMemoryCannotBeRead()
        {
            Assert.IsNull(ChronicleArchive.Reading("MEM_07", new ChronicleProgress()));
            Assert.IsNull(ChronicleArchive.Reading("MEM_99", Everything()));
        }

        [Test] public void EveryMemoryIsReadableWithEveryBlockPlacedOnAPage()
        {
            var p = Everything();
            foreach (var m in ChronicleCatalog.Memories)
            {
                var r = ChronicleArchive.Reading(m.id, p); Assert.IsNotNull(r, m.id); Assert.Greater(r.spreads.Count, 0, m.id);
                var placed = r.spreads.Sum(s => s.left.Count + s.right.Count);
                Assert.Greater(placed, 4, m.id + " has narration to read"); Assert.IsNotNull(r.illustration);
            }
        }

        [Test] public void ReadingShowsCorrectionsOnlyOnceDiscoveredAndKeepsTheOriginal()
        {
            var p = Everything();
            var fresh = ChronicleArchive.Reading("MEM_07", p); var blocks = fresh.spreads.SelectMany(s => s.left.Concat(s.right)).ToList();
            Assert.IsTrue(blocks.Any(b => b.text == "Three travelers stood together." && !b.struck)); Assert.IsFalse(blocks.Any(b => b.text == "There were four."));
            p.CommitMemoryViewed("MEM_07");
            var after = ChronicleArchive.Reading("MEM_07", p).spreads.SelectMany(s => s.left.Concat(s.right)).ToList();
            Assert.IsTrue(after.Any(b => b.text == "Three travelers stood together." && b.struck)); Assert.IsTrue(after.Any(b => b.text == "There were four." && b.style == ChronicleTextStyle.Correction));
        }

        [Test] public void ReadingFlowsFromTheLeftPageToTheRightAndOnToNewSpreads()
        {
            var r = ChronicleArchive.Reading("MEM_25", Everything());
            Assert.IsTrue(r.spreads[0].left.Count > 0); Assert.IsTrue(r.spreads.Count > 1 || r.spreads[0].right.Count > 0);
            Assert.IsTrue(r.spreads.SelectMany(s => s.left).All(b => b.side == ChroniclePageSide.Left)); Assert.IsTrue(r.spreads.SelectMany(s => s.right).All(b => b.side == ChroniclePageSide.Right));
        }

        [Test] public void ReadingAdaptsToWhatThePlayerKnows()
        {
            var fresh = Everything(); var knows = Everything(); knows.DiscoverFact("FACT_FOURTH_COMPANION");
            Func<ChronicleProgress, string> text = p => string.Join("|", ChronicleArchive.Reading("MEM_08", p).spreads.SelectMany(s => s.left.Concat(s.right)).Select(b => b.text));
            Assert.IsTrue(text(fresh).Contains("Its owner is unknown")); Assert.IsTrue(text(knows).Contains("belongs to the fourth companion"));
        }

        // ---------------------------------------------------------------- navigation
        [Test] public void BackStepsOutwardOneLevelAtATimeThenExits()
        {
            var p = Everything(); var a = new ChronicleArchiveController(p);
            Assert.AreEqual(ChronicleArchiveAction.TurnedPage, a.Accept().action); Assert.AreEqual(1, a.Spread);
            Assert.AreEqual(ChronicleArchiveAction.OpenedReading, a.Accept().action); Assert.IsTrue(a.InReading);
            Assert.AreEqual(ChronicleArchiveAction.ClosedReading, a.Back().action); Assert.IsFalse(a.InReading); Assert.AreEqual(1, a.Spread, "back from reading returns to the same chapter page");
            Assert.AreEqual(ChronicleArchiveAction.TurnedPage, a.Back().action); Assert.AreEqual(0, a.Spread);
            Assert.AreEqual(ChronicleArchiveAction.Exit, a.Back().action);
        }

        [Test] public void LockedEntriesCanBeFocusedButNeverOpened()
        {
            var a = new ChronicleArchiveController(new ChronicleProgress(), 3);
            Assert.AreEqual(ChronicleArchiveAction.Moved, a.Move(0, 1).action); Assert.AreEqual(1, a.Focus);
            Assert.AreEqual(ChronicleArchiveAction.None, a.Accept().action); Assert.IsFalse(a.InReading);
            Assert.AreEqual(ChronicleArchiveAction.None, a.OpenReading("MEM_07").action); Assert.AreEqual(ChronicleArchiveAction.None, a.Replay("MEM_07").action);
        }

        [Test] public void AcceptingAnUnlockedMemoryOpensItAndAcceptingAgainReplaysIt()
        {
            var p = new ChronicleProgress(); p.RegisterBossDefeated(ChronicleHero.Hexer, 1, 18);
            var a = new ChronicleArchiveController(p, 3); a.Move(0, 1);
            var open = a.Accept(); Assert.AreEqual(ChronicleArchiveAction.OpenedReading, open.action); Assert.AreEqual("MEM_08", open.id);
            var replay = a.Accept(); Assert.AreEqual(ChronicleArchiveAction.Replay, replay.action); Assert.AreEqual("MEM_08", replay.id);
        }

        [Test] public void PageTurnsAreClampedToTheBook()
        {
            var a = new ChronicleArchiveController(Everything());
            Assert.AreEqual(ChronicleArchiveAction.None, a.Page(-1).action);
            for (var i = 0; i < 12; i++) a.Page(1);
            Assert.AreEqual(10, a.Spread); Assert.AreEqual(ChronicleArchiveAction.None, a.Page(1).action);
            var turn = a.Page(-1); Assert.AreEqual(10, turn.fromSpread); Assert.AreEqual(9, turn.toSpread); Assert.AreEqual(0, a.Focus);
        }

        [Test] public void LeftAndRightTurnPagesOnAChapterButAdjustOptionsOnTheContents()
        {
            var p = Everything(); var a = new ChronicleArchiveController(p, 4);
            Assert.AreEqual(ChronicleArchiveAction.TurnedPage, a.Move(1, 0).action); Assert.AreEqual(5, a.Spread);
            Assert.AreEqual(ChronicleArchiveAction.TurnedPage, a.Move(-1, 0).action); Assert.AreEqual(4, a.Spread);
            var c = new ChronicleArchiveController(p); Assert.AreEqual(ChronicleArchiveAction.None, c.Move(1, 0).action, "no sideways meaning on a chapter row");
        }

        [Test] public void TheContentsRowsOpenTheirChaptersAndTheSecretRow()
        {
            var p = Everything(); var a = new ChronicleArchiveController(p);
            for (var chapter = 1; chapter <= 9; chapter++)
            {
                a = new ChronicleArchiveController(p); a.SetFocusIndex(chapter - 1); a.Accept(); Assert.AreEqual(chapter, a.Spread);
            }
            a = new ChronicleArchiveController(p); a.SetFocusIndex(9); Assert.AreEqual(ChronicleArchiveAction.TurnedPage, a.Accept().action); Assert.AreEqual(10, a.Spread);
            var begin = a.Accept(); Assert.AreEqual(ChronicleArchiveAction.BeginSecret, begin.action); Assert.AreEqual(ChronicleCatalog.SecretSceneId, begin.id);
        }

        [Test] public void ChapterXCannotBeginWhileLocked()
        {
            var a = new ChronicleArchiveController(new ChronicleProgress(), 10);
            Assert.AreEqual(ChronicleArchiveAction.None, a.Accept().action); Assert.AreEqual(ChronicleArchiveAction.None, a.BeginSecret().action);
            var unlocked = Everything(); Assert.AreEqual(ChronicleArchiveAction.BeginSecret, new ChronicleArchiveController(unlocked, 10).Accept().action);
        }

        [Test] public void TheThreeOptionsCycleAndPersistToProgress()
        {
            var p = new ChronicleProgress(); var a = new ChronicleArchiveController(p);
            a.SetFocusIndex(10); Assert.AreEqual(ChronicleArchiveAction.ChangedOption, a.Accept().action); Assert.IsTrue(p.instantText); a.Accept(); Assert.IsFalse(p.instantText);
            a.SetFocusIndex(11); a.Move(1, 0); Assert.AreEqual(1.25f, p.textSpeed, .001f); a.Move(-1, 0); a.Move(-1, 0); Assert.AreEqual(.75f, p.textSpeed, .001f); a.Move(-1, 0); Assert.AreEqual(2f, p.textSpeed, .001f, "wraps around");
            a.SetFocusIndex(12); a.Move(1, 0); Assert.AreEqual(1, p.openingMode); a.Move(1, 0); Assert.AreEqual(2, p.openingMode); a.Move(1, 0); Assert.AreEqual(0, p.openingMode); a.Move(-1, 0); Assert.AreEqual(2, p.openingMode);
        }

        [Test] public void EveryFocusableItemOnEverySpreadCanBeReachedAndActivatedWithoutAMouse()
        {
            var p = Everything();
            for (var spread = 0; spread <= 10; spread++)
            {
                var a = new ChronicleArchiveController(p, spread); Assert.Greater(a.FocusCount, 0, "spread " + spread);
                for (var i = 0; i < a.FocusCount; i++)
                {
                    a = new ChronicleArchiveController(p, spread);
                    for (var k = 0; k < i; k++) a.Move(0, 1);
                    Assert.AreEqual(i, a.Focus, "reached item " + i + " on spread " + spread);
                    var result = a.Accept(); Assert.IsTrue(Enum.IsDefined(typeof(ChronicleArchiveAction), result.action));
                }
            }
        }

        [Test] public void ReadingPagesTurnWithinTheirBoundsAndIgnoreVerticalMovement()
        {
            var p = Everything(); var a = new ChronicleArchiveController(p, 3); a.Accept(); a.OpenReading("MEM_14");
            Assert.AreEqual(0, a.ReadingSpread); Assert.AreEqual(ChronicleArchiveAction.None, a.Move(0, 1).action); Assert.AreEqual(ChronicleArchiveAction.None, a.Move(-1, 0).action);
            var turns = 0; while (a.Move(1, 0).action == ChronicleArchiveAction.TurnedPage) turns++;
            Assert.AreEqual(a.ReadingSpreadCount - 1, a.ReadingSpread); Assert.AreEqual(a.ReadingSpreadCount - 1, turns);
        }

        [Test] public void OpeningAChapterRemembersWhereYouWereAndRestoreReturnsThere()
        {
            var p = Everything(); var a = new ChronicleArchiveController(p); a.OpenChapter(6);
            Assert.AreEqual(6, p.lastChapterOpened);
            var b = new ChronicleArchiveController(p); b.Restore(6, "MEM_16");
            Assert.AreEqual(6, b.Spread); Assert.IsTrue(b.InReading); Assert.AreEqual("MEM_16", b.ReadingId);
        }

        [Test] public void AGamepadAloneCanOpenReadReplayAndLeave()
        {
            var p = Everything(); var a = new ChronicleArchiveController(p);
            a.Move(0, 1); a.Move(0, 1); a.Accept();                // down to chapter III, open it
            Assert.AreEqual(3, a.Spread);
            a.Move(0, 1); var read = a.Accept(); Assert.AreEqual("MEM_08", read.id);
            var replay = a.Accept(); Assert.AreEqual(ChronicleArchiveAction.Replay, replay.action);
            a.Back(); a.Back(); Assert.AreEqual(ChronicleArchiveAction.Exit, a.Back().action);
        }
    }
}
