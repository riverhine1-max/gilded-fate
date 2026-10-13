using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace GildedFate.Chronicle.Tests
{
    /// <summary>Prompt 4: the thirty authored recollections, their recordings, corrections, Archive pages and page journeys.</summary>
    [TestFixture]
    public class ChronicleAscensionIntegrationTests
    {
        static readonly ChronicleHero[] Heroes = { ChronicleHero.Vanguard, ChronicleHero.Hexer, ChronicleHero.Reaper };

        sealed class IndexLine { public string lineId, sceneId, commandId, text, audio; public float duration; }

        static string FindProjectFile(string relative)
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            for (var i = 0; dir != null && i < 6; i++, dir = dir.Parent) { var p = Path.Combine(dir.FullName, relative); if (File.Exists(p)) return p; }
            return null;
        }

        static string Unescape(string s) => Regex.Unescape(s);

        static List<IndexLine> AscensionLines()
        {
            var path = FindProjectFile("Assets/Resources/Chronicle/Audio/DialogueIndex.json"); Assert.IsNotNull(path, "DialogueIndex.json");
            var json = File.ReadAllText(path); var list = new List<IndexLine>();
            foreach (Match m in Regex.Matches(json, "\"lineId\":\\s*\"(ASC_[^\"]+)\",\\s*\"sceneId\":\\s*\"([^\"]+)\",\\s*\"commandId\":\\s*\"([^\"]+)\",\\s*\"speaker\":\\s*\"NARRATOR\",\\s*\"characterSpeaker\":\\s*\"\",\\s*\"text\":\\s*\"((?:[^\"\\\\]|\\\\.)*)\""))
            {
                var tail = json.Substring(m.Index, Math.Min(json.Length - m.Index, 20000));
                var audio = Regex.Match(tail, "\"audio\":\\s*\"([^\"]+)\"").Groups[1].Value;
                var duration = float.Parse(Regex.Match(tail, "\"durationSeconds\":\\s*([0-9.]+)").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                list.Add(new IndexLine { lineId = m.Groups[1].Value, sceneId = m.Groups[2].Value, commandId = m.Groups[3].Value, text = Unescape(m.Groups[4].Value), audio = audio, duration = duration });
            }
            return list;
        }

        // A stage that serves each indexed recording on a clock, and counts every voice request.
        sealed class VoiceStage : IChronicleStage, IChronicleDialogueStage
        {
            readonly Dictionary<string, IndexLine> lines; public float time; public readonly Dictionary<string, int> played = new Dictionary<string, int>();
            public VoiceStage(IEnumerable<IndexLine> lines) { this.lines = lines.ToDictionary(l => l.sceneId + "|" + l.commandId); }
            public float BookActionDuration(ChronicleBookAction a) => .9f;
            public void BookAction(ChronicleBookAction a, ChronicleNarratorState n) { }
            public void SetBookPoseImmediate(ChronicleBookPose p, int s) { }
            public void PlayAudio(string c, float v) { } public void StopAllAudio() { } public void MagicEffect(string e, float x, float y, float i) { }
            public void SceneStarted(string id) { } public void SceneEnded(string id, bool skipped) { } public void SetDialoguePaused(bool paused) { }
            public ChronicleDialoguePlayback BeginDialogue(string sceneId, string id, string text, string speaker, ChronicleNarratorState n)
            {
                if (!lines.TryGetValue(sceneId + "|" + id, out var l) || l.text != text || speaker != "NARRATOR") return null;
                played[l.lineId] = played.TryGetValue(l.lineId, out var c) ? c + 1 : 1;
                var start = time; var words = Regex.Matches(text, "\\S+").Cast<Match>().ToArray(); var span = l.duration / Math.Max(1, words.Length);
                var timing = new ChronicleDialogueTiming { ready = true, text = text, durationSeconds = l.duration, words = words.Select((w, i) => new ChronicleWordTiming { text = w.Value, charStart = w.Index, charLength = w.Length, startSeconds = i * span, endSeconds = i * span + span * .9f }).ToArray() };
                return new ChronicleDialoguePlayback { timing = timing, positionSeconds = () => time - start };
            }
        }

        static ChronicleProgress EveryMemoryWatched()
        {
            var p = new ChronicleProgress();
            foreach (var h in Heroes) p.RegisterRunEnded(h, true, 3, 54, "normal-" + h);
            foreach (var m in ChronicleCatalog.Memories) p.CommitMemoryViewed(m.id);
            return p;
        }

        static void Play(ChronicleProgress p, string sceneId, VoiceStage stage, float skipAt = -1f)
        {
            var seq = new ChronicleSequencer(ChronicleScripts.Get(sceneId), stage, new ChronicleProgressContext(p), new ChronicleProgressSink(p), null, new ChroniclePageMetrics { autoPageTurns = true });
            seq.Start(ChronicleBookPose.Open); var t = 0f;
            while (!seq.Finished && t < 600f) { if (skipAt >= 0f && t >= skipAt) { seq.Skip(); break; } seq.Update(.05f); t += .05f; stage.time = t; }
            Assert.IsTrue(seq.Finished, sceneId + " finished"); Assert.AreEqual(0, seq.State.overflowCount, sceneId + " overflow");
        }

        // ---------------------------------------------------------------- recordings
        [Test] public void EveryNewRecordingBindsToItsExactBlockAndFile()
        {
            var lines = AscensionLines(); Assert.AreEqual(45, lines.Count, "45 recorded narration lines");
            Assert.AreEqual(lines.Count, lines.Select(l => l.lineId).Distinct().Count());
            foreach (var l in lines)
            {
                var scene = ChronicleScripts.Get(l.sceneId); Assert.IsNotNull(scene, l.lineId);
                var c = scene.commands.FirstOrDefault(x => x.id == l.commandId); Assert.IsNotNull(c, l.lineId + " has its command");
                Assert.AreEqual(l.text, c.text, l.lineId + " text is exact");
                Assert.IsTrue(c.style == ChronicleTextStyle.Narration || c.style == ChronicleTextStyle.Correction, l.lineId + " is the Narrator, never the Being");
                Assert.IsNotNull(FindProjectFile("Assets/Resources/" + l.audio + ".wav"), l.lineId + " recording exists");
            }
            foreach (var d in ChronicleAscensionCatalog.All.Where(d => d.IsMajor)) Assert.AreEqual(3, lines.Count(l => l.sceneId == d.sceneId), d.id + " voices observation, correction and closing");
        }

        [Test] public void EachVoiceIsHeardExactlyOnceAndSilentPassagesStaySilent()
        {
            var lines = AscensionLines(); var p = EveryMemoryWatched();
            foreach (var d in ChronicleAscensionCatalog.All)
            {
                ChronicleAscensionTestKit.ClearThrough(p, d.hero, d.tier);
                var stage = new VoiceStage(lines); Play(p, d.sceneId, stage);
                var expected = lines.Where(l => l.sceneId == d.sceneId).Select(l => l.lineId).OrderBy(x => x).ToList();
                Assert.AreEqual(string.Join(",", expected), string.Join(",", stage.played.Keys.OrderBy(x => x)), d.id);
                Assert.IsTrue(stage.played.Values.All(v => v == 1), d.id + " no voice plays twice");
            }
        }

        // ---------------------------------------------------------------- corrections
        [Test] public void MajorCorrectionsStrikeOnlyTheirOwnNewObservationAndSave()
        {
            var lines = AscensionLines();
            foreach (var d in ChronicleAscensionCatalog.All.Where(x => x.IsMajor))
                foreach (var skip in new[] { false, true })
                {
                    var p = EveryMemoryWatched(); ChronicleAscensionTestKit.ClearThrough(p, d.hero, d.tier);
                    var before = string.Join(",", p.corrections);
                    var stage = new VoiceStage(lines);
                    var seq = new ChronicleSequencer(ChronicleScripts.Get(d.sceneId), stage, new ChronicleProgressContext(p), new ChronicleProgressSink(p), null, new ChroniclePageMetrics { autoPageTurns = true });
                    seq.Start(ChronicleBookPose.Open);
                    if (skip) { seq.Update(1f); stage.time = 1f; seq.Skip(); }
                    else for (var t = 0f; !seq.Finished && t < 600f; t += .05f) { seq.Update(.05f); stage.time = t + .05f; }   // the recordings' clock runs with the scene
                    Assert.IsTrue(seq.Finished, d.id);
                    var struck = seq.State.transcript.Where(b => b.struck).ToList();
                    Assert.AreEqual(1, struck.Count, d.id); Assert.AreEqual("new_observation", struck[0].id, d.id + " strikes only its own new observation");
                    Assert.IsTrue(seq.State.transcript.Any(b => b.id == "replacement" && b.style == ChronicleTextStyle.Correction), d.id + " red-ink replacement");
                    Assert.IsTrue(p.HasAscensionCorrection(d.correctionId), d.id + (skip ? " (skipped)" : ""));
                    Assert.AreEqual(before, string.Join(",", p.corrections), "the ten shared corrections are untouched");
                }
        }

        // ---------------------------------------------------------------- the whole journey
        [Test] public void AllThirtyAreEarnedOneVictoryAtATimeAndLeadToChapterX()
        {
            var lines = AscensionLines(); var p = EveryMemoryWatched();
            foreach (var h in Heroes)
                for (var tier = 1; tier <= 10; tier++)
                {
                    if (tier < 10) Assert.IsFalse(p.RegisterAscensionVictory(h, tier + 1, "skip-" + h + tier).Earned, "a higher level never skips ahead");
                    var r = p.RegisterAscensionVictory(h, tier, "win-" + h + tier); Assert.IsTrue(r.Earned, h + " " + tier);
                    var next = ChronicleAscension.ScenesToPresent(p, h); Assert.AreEqual(1, next.Count); Assert.AreEqual(r.earned.sceneId, next[0]);
                    Play(p, next[0], new VoiceStage(lines));
                    Assert.IsTrue(p.IsRevelationWatched(r.earned.id)); Assert.AreEqual(0, ChronicleAscension.ScenesToPresent(p, h).Count, "nothing presented twice");
                    Assert.IsFalse(p.RegisterAscensionVictory(h, tier, "again-" + h + tier).Earned, "repeating a level grants nothing");
                    Assert.AreEqual(h == ChronicleHero.Reaper && tier == 10, p.secretUnlocked, "Chapter X only after the last True History");
                }
            Assert.AreEqual(12, p.ascension.corrections.Count); Assert.AreEqual(30, p.ascension.watched.Count);
            Assert.AreEqual(1, p.pendingNotices.Count(n => n == ChronicleCatalog.SecretChapterId), "announced once");
            var copy = ChronicleProgressTests.FromJson(ChronicleProgressTests.ToJson(p)); copy.Ensure();
            Assert.AreEqual(30, copy.ascension.watched.Count); Assert.AreEqual(12, copy.ascension.corrections.Count); Assert.IsTrue(copy.secretUnlocked);
        }

        [Test] public void ARecollectionWaitsForTheOriginalDiscoveryItBuildsOn()
        {
            var p = new ChronicleProgress(); p.RegisterRunEnded(ChronicleHero.Vanguard, true, 3, 54, "w"); p.CommitMemoryViewed("MEM_07");
            ChronicleAscensionTestKit.ClearThrough(p, ChronicleHero.Vanguard, 1);
            Assert.AreEqual(0, ChronicleAscension.ScenesToPresent(p, ChronicleHero.Vanguard).Count, "the base friendship recovery (MEM_25) comes first");
            Assert.IsFalse(ChronicleArchive.CanWatch(p, "ASC_VAN_01")); Assert.IsTrue(p.IsRevelationEarned("ASC_VAN_01"), "deferred, never lost");
            p.CommitMemoryViewed("MEM_25");
            Assert.AreEqual("ASC_VAN_01", ChronicleAscension.ScenesToPresent(p, ChronicleHero.Vanguard).Single());
            Assert.IsTrue(ChronicleArchive.CanWatch(p, "ASC_VAN_01")); Assert.IsFalse(ChronicleArchive.CanRead(p, "ASC_VAN_01"), "readable once it has been presented");
        }

        // ---------------------------------------------------------------- the Archive
        [Test] public void NewWritingBecomesAPermanentPartOfItsMemory()
        {
            var lines = AscensionLines(); var p = EveryMemoryWatched();
            var original = ChronicleArchive.Reading("MEM_07", p); var originalText = string.Join("|", original.spreads.SelectMany(s => s.left.Concat(s.right)).Select(b => b.text));
            ChronicleAscensionTestKit.ClearThrough(p, ChronicleHero.Vanguard, 1);
            Assert.AreEqual(original.spreads.Count, ChronicleArchive.Reading("MEM_07", p).spreads.Count, "not yet presented: not yet written");
            Play(p, "ASC_VAN_01", new VoiceStage(lines));
            var expanded = ChronicleArchive.Reading("MEM_07", p);
            Assert.Greater(expanded.spreads.Count, original.spreads.Count);
            var expandedText = string.Join("|", expanded.spreads.SelectMany(s => s.left.Concat(s.right)).Select(b => b.text));
            Assert.IsTrue(expandedText.StartsWith(originalText, StringComparison.Ordinal), "the original pages come first, unchanged");
            Assert.IsTrue(expandedText.Contains("The wire was crooked.") && expandedText.Contains("The cup held."), "the new writing, verbatim");
            var a = new ChronicleArchiveController(p, ChronicleAscensionCatalog.Find("ASC_VAN_01").chapter);
            Assert.AreEqual(4, a.FocusCount, "three memories and the recollection");
            a.SetFocusIndex(3); Assert.AreEqual(ChronicleArchiveAction.OpenedReading, a.Accept().action);
            Assert.IsTrue(a.Reading.spreads.SelectMany(s => s.left.Concat(s.right)).Any(b => b.id == "new_observation" && b.struck), "the struck observation stays readable with its correction");
            Assert.AreEqual(ChronicleArchiveAction.Replay, a.Accept().action, "Watch again");
            var ch = ChronicleAscensionCatalog.Find("ASC_VAN_01").chapter;
            Assert.IsTrue(ChronicleArchive.Contents(p).chapters[ch - 1].updated, "the new writing marks its chapter in the contents");
            new ChronicleArchiveController(p).OpenChapter(ch);
            Assert.IsFalse(ChronicleArchive.Contents(p).chapters[ch - 1].updated, "opening the chapter marks it seen");
        }

        // ---------------------------------------------------------------- page journeys
        [Test] public void PageJourneysScaleWithDistanceAccelerateAndRunBackward()
        {
            Assert.AreEqual(1, ChronicleJournal.PlanFlip(1, 2).Turns); Assert.AreEqual(1f, ChronicleJournal.PlanFlip(1, 2).tempos[0]);
            var five = ChronicleJournal.PlanFlip(1, 5); var nine = ChronicleJournal.PlanFlip(1, 9); var back = ChronicleJournal.PlanFlip(9, 3);
            Assert.AreEqual(4, five.Turns); Assert.AreEqual(8, nine.Turns); Assert.AreEqual(6, back.Turns); Assert.AreEqual(-1, back.direction); Assert.AreEqual(1, nine.direction);
            Assert.Greater(nine.tempos.Max(), five.tempos.Max(), "longer journeys reach a higher speed");
            Assert.Greater(nine.tempos[4], nine.tempos[0], "quick through the middle"); Assert.LessOrEqual(nine.tempos.Last(), 1.35f, "slowing at the destination");
            Assert.Less(nine.Seconds(.9f), 8 * .9f * .5f, "never makes the player watch eight full turns");
            Assert.Greater(nine.Seconds(.9f), five.Seconds(.9f), "but visibly more movement than a short jump");
            Assert.IsTrue(ChronicleJournal.PlanFlip(1, 9, true).tempos.All(t => t == 1f), "reduced motion");
            Assert.AreEqual(0, ChronicleJournal.PlanFlip(4, 4).Turns);
        }

        [Test] public void TheBookTurnsFasterOnlyWhenAskedAndItsStacksFollowEverySheet()
        {
            var book = new ChronicleBookAnimator(); book.SetPoseImmediate(ChronicleBookPose.Open, 1); var right = book.Frame.rightStack;
            book.Tempo = 3f; book.Perform(ChronicleBookAction.TurnPageForward, ChronicleNarratorState.ConfidentHistorian);
            for (var t = 0f; t < .9f / 3f + .02f; t += .01f) book.Update(.01f);
            Assert.IsFalse(book.Busy, "a sheet at tempo 3 lands in a third of the time");
            Assert.AreEqual(2, book.Frame.spreadIndex); Assert.Less(book.Frame.rightStack, right, "the right stack thins as the reader moves forward");
            book.Tempo = 100f; Assert.AreEqual(6f, book.Tempo, "clamped"); book.Tempo = 1f;
            Assert.AreEqual(.9f, book.DurationOf(ChronicleBookAction.TurnPageForward), .001f, "the sequencer's turn length is unchanged");
        }
    }
}
