using System;
using System.Collections.Generic;
using GildedFate.Audio;
using GildedFate.Chronicle;
using GildedFate.ChronicleBook;
using GildedFate.Core;
using GildedFate.Saving;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private enum ChronicleMilestonePhase { FadeOut, Emerge, Continuing, Playing, Closing, FadeIn }
        // The living journal: the book reopens where the story stopped and turns to the new chapter.
        private ChronicleContinuationPlan chronicleContinuation;
        private int chronicleContinueSpread, chronicleNextStartSpread = -1;
        private float chronicleContinueNextTurn;
        private string chronicleMilestoneLastScene = "";
        // Prompt 4: multi-page journeys (accelerating, real sheets), recollections returning to their chapter, the Chapter X reveal.
        private ChronicleFlipPlan chronicleTravelPlan; private int chronicleTravelStep;
        private ChronicleScene chronicleHeldScene;
        private bool chronicleRevealSecret, chronicleSecretRevealed; private float chronicleRevealT;
        private bool chronicleMilestoneActive, chronicleMilestoneRunEnded, chronicleMilestoneVictory, chronicleMilestoneOpened;
        private ChronicleMilestonePhase chronicleMilestonePhase;
        private float chronicleMilestoneTime, chronicleMilestoneCloseDuration;
        private readonly Queue<ChronicleScene> chronicleMilestoneScenes = new();
        private Action chronicleMilestoneContinue;

        private void BeginChronicleMilestone(bool runEnded, bool victory, Action then)
        {
            if (chronicleMilestoneActive) return;
            // Capture and test harnesses retain their existing instantaneous result contract.
            if (run.sandbox || captureMode || profile == null) { then?.Invoke(); return; }
            chronicleMilestoneScenes.Clear();
            var hero = (ChronicleHero)(int)run.hero;
            foreach (var id in ChronicleMilestoneScenes.Memories(profile.chronicle, hero, run.act, runEnded))
                chronicleMilestoneScenes.Enqueue(ChronicleScripts.Get(id));
            if (runEnded) chronicleMilestoneScenes.Enqueue(ChronicleMilestoneScenes.Ending(victory, hero, Math.Max(0, profile.chronicle.runsEnded[(int)hero] - profile.chronicle.wins[(int)hero] - 1)));
            // The Ten Recollections, after the existing victory presentation. Only authored, approved and registered revelation scenes
            // are returned, so until that content ships this adds nothing and no placeholder is ever shown.
            if (runEnded && victory) foreach (var id in ChronicleAscension.ScenesToPresent(profile.chronicle, hero)) chronicleMilestoneScenes.Enqueue(ChronicleScripts.Get(id));
            chronicleContinuation = chronicleMilestoneScenes.Count > 0 && chronicleMilestoneScenes.Peek() != null ? ChronicleJournal.PlanContinuation(profile.chronicle, chronicleMilestoneScenes.Peek().id) : null;
            chronicleNextStartSpread = -1; chronicleMilestoneLastScene = "";
            chronicleTravelPlan = chronicleContinuation != null ? ChronicleJournal.PlanFlip(chronicleContinuation.startSpread, chronicleContinuation.toSpread, profile.reduceMotion) : null; chronicleTravelStep = 0;
            chronicleHeldScene = null; chronicleSecretRevealed = false; chronicleRevealT = 0f;
            // This victory just opened Chapter X (its notice is still pending): the journal turns to the final chapter before closing.
            chronicleRevealSecret = runEnded && victory && profile.chronicle.secretUnlocked && !profile.chronicle.secretCompleted && profile.chronicle.pendingNotices.Contains(ChronicleCatalog.SecretChapterId);
            chronicleMilestoneContinue = then;
            chronicleMilestoneRunEnded = runEnded; chronicleMilestoneVictory = victory;
            chronicleMilestoneOpened = false; chronicleMilestoneActive = true;
            chronicleMilestonePhase = ChronicleMilestonePhase.FadeOut; chronicleMilestoneTime = 0;
            transitionAlpha = 0; ArmScreenInputGuard();
        }

        private void SetChronicleMilestonePhase(ChronicleMilestonePhase phase)
        { chronicleMilestonePhase = phase; chronicleMilestoneTime = 0; }

        private void AdvanceChronicleMilestone(float dt)
        {
            try
            {
                chronicleMilestoneTime += Mathf.Clamp(dt, 0, .1f);
                if (chronicleMilestonePhase != ChronicleMilestonePhase.Playing) shimmer += Mathf.Clamp(dt, 0, .1f);
                // Keep the existing mixer settings and ducking live; gameplay and result buttons remain paused.
                UpdateAudioPresentation();
                switch (chronicleMilestonePhase)
                {
                    case ChronicleMilestonePhase.FadeOut:
                        if (chronicleMilestoneTime < (profile.reduceMotion ? .45f : 1.6f)) break;
                        chronicleHost ??= ChronicleBookStageHost.Create(2400, 1350);
                        if (chronicleHost == null) { FinishChronicleMilestone(); break; }
                        chronicleHost.SetActive(true);
                        // A journal already begun emerges open at the page where its story last stopped.
                        if (chronicleContinuation != null) chronicleHost.Rig.SetPoseImmediate(ChronicleBookPose.Open, chronicleContinuation.startSpread);
                        else chronicleHost.Rig.SetPoseImmediate(ChronicleBookPose.Closed, 0);
                        chronicleHost.Tick(0, null);
                        SetChronicleMilestonePhase(ChronicleMilestonePhase.Emerge);
                        break;
                    case ChronicleMilestonePhase.Emerge:
                        chronicleHost.Tick(dt, null);
                        if (chronicleMilestoneTime < (profile.reduceMotion ? .4f : 1.35f)) break;
                        if (chronicleContinuation == null) { PlayNextChronicleMilestone(); break; }
                        chronicleContinueSpread = chronicleContinuation.startSpread; chronicleContinueNextTurn = .15f;
                        SetChronicleMilestonePhase(ChronicleMilestonePhase.Continuing);
                        break;
                    case ChronicleMilestonePhase.Continuing:
                    {
                        chronicleHost.Tick(dt, null);
                        var plan = chronicleContinuation;
                        if (chronicleContinueSpread == plan.toSpread)
                        {
                            if (chronicleHost.Rig.Frame.busy) break;
                            SetChronicleTempo(1f);
                            // The Chapter X reveal holds the final page open while its title is written in.
                            if (chronicleHeldScene == null && chronicleSecretRevealed && chronicleRevealT < (profile.reduceMotion ? 1.2f : 2.4f)) { chronicleRevealT += dt; break; }
                            chronicleMilestoneOpened = true; chronicleNextStartSpread = plan.toSpread; chronicleContinuation = null; chronicleTravelPlan = null;   // the scene continues on this open page
                            PlayNextChronicleMilestone(); break;
                        }
                        if (chronicleMilestoneTime < chronicleContinueNextTurn || chronicleHost.Rig.Frame.busy) break;
                        var action = plan.toSpread > chronicleContinueSpread ? ChronicleBookAction.TurnPageForward : ChronicleBookAction.TurnPageBack;
                        var tempo = chronicleTravelPlan != null && chronicleTravelStep < chronicleTravelPlan.Turns ? chronicleTravelPlan.tempos[chronicleTravelStep] : 1f; chronicleTravelStep++;
                        SetChronicleTempo(tempo);   // quick through the middle of a long journey, slowing at the destination
                        chronicleHost.Rig.Perform(action, profile.chronicle.NarratorState); ChronicleBookSound(action);
                        chronicleContinueSpread += action == ChronicleBookAction.TurnPageForward ? 1 : -1;
                        chronicleContinueNextTurn = chronicleMilestoneTime + chronicleHost.Rig.DurationOf(action) / tempo * (profile.reduceMotion ? 1f : .85f);
                        break;
                    }
                    case ChronicleMilestonePhase.Playing:
                        AdvanceChronicle(dt);
                        break;
                    case ChronicleMilestonePhase.Closing:
                        chronicleHost.Tick(dt, chronicleDirector?.Current?.State.camera);
                        if (chronicleMilestoneTime >= chronicleMilestoneCloseDuration + .4f)
                        {
                            chronicleHost.SetActive(false);
                            var continuation = chronicleMilestoneContinue; chronicleMilestoneContinue = null;
                            continuation?.Invoke();
                            SetChronicleMilestonePhase(ChronicleMilestonePhase.FadeIn); transitionAlpha = 0;
                        }
                        break;
                    case ChronicleMilestonePhase.FadeIn:
                        if (chronicleMilestoneTime >= (profile.reduceMotion ? .3f : .9f)) FinishChronicleMilestone();
                        break;
                }
            }
            catch (Exception error)
            {
                Debug.LogError("[Chronicle] Milestone playback failed; returning to progression: " + error);
                try { chronicleDirector?.Abort(); } catch { }
                chronicleActive = false; chronicleDialogueAudio?.Stop(); chronicleHost?.SetActive(false);
                FinishChronicleMilestone();
            }
        }

        private void PlayNextChronicleMilestone()
        {
            // The scene that just ended is where the story now stands (run endings and the opening are not journal pages).
            if (!string.IsNullOrEmpty(chronicleMilestoneLastScene) && ChronicleJournal.RecordStoryReached(profile.chronicle, chronicleMilestoneLastScene)) ProfileService.Save(profile);
            chronicleMilestoneLastScene = "";
            while (chronicleHeldScene != null || chronicleMilestoneScenes.Count > 0)
            {
                var held = chronicleHeldScene != null;
                var scene = held ? chronicleHeldScene : chronicleMilestoneScenes.Dequeue(); chronicleHeldScene = null;
                if (scene == null) continue;
                // A recollection first carries the open journal back (or forward) to its own chapter, through the pages in between.
                var recollection = ChronicleAscensionCatalog.FindByScene(scene.id);
                if (!held && recollection != null && recollection.chapter >= 1 && chronicleMilestoneOpened)
                {
                    var here = chronicleDirector?.Current != null ? chronicleDirector.Current.State.spreadIndex : chronicleContinueSpread;
                    if (here != recollection.chapter) { BeginChronicleTravel(here, recollection.chapter, scene); return; }
                }
                var presentationOnly = scene.id == ChronicleMilestoneScenes.VictoryId || scene.id == ChronicleMilestoneScenes.DefeatId;
                // The shared ending never unlocks Reaper's memory, facts, corrections or Chapter X.
                // Each scene starts on the page the previous one finished on, so the book never jumps back to its first sheet.
                var start = chronicleNextStartSpread >= 0 ? chronicleNextStartSpread : chronicleMilestoneOpened && chronicleDirector?.Current != null ? chronicleDirector.Current.State.spreadIndex : 0;
                if (!PlayChronicleSceneData(scene, presentationOnly, PlayNextChronicleMilestone, true, !chronicleMilestoneOpened, start)) continue;
                chronicleNextStartSpread = -1; if (!presentationOnly) chronicleMilestoneLastScene = scene.id;
                chronicleMilestoneOpened = true;
                SetChronicleMilestonePhase(ChronicleMilestonePhase.Playing); return;
            }
            if (chronicleRevealSecret && !chronicleSecretRevealed && chronicleMilestoneOpened)
            {
                chronicleSecretRevealed = true; chronicleRevealT = 0f;
                var here = chronicleDirector?.Current != null ? chronicleDirector.Current.State.spreadIndex : chronicleContinueSpread;
                BeginChronicleTravel(here, ChronicleJournal.SecretSpread, null); return;
            }
            SetChronicleTempo(1f);
            chronicleMilestoneCloseDuration = chronicleHost.Rig.DurationOf(ChronicleBookAction.Close);
            chronicleHost.Rig.Perform(ChronicleBookAction.Close, profile.chronicle.NarratorState);
            ChronicleBookSound(ChronicleBookAction.Close);
            SetChronicleMilestonePhase(ChronicleMilestonePhase.Closing);
        }

        /// <summary>Carries the journal from one spread to another as a physical journey, then plays the held scene (or, with none,
        /// holds the Chapter X reveal). Very long journeys begin a few sheets from the destination so they never drag.</summary>
        private void BeginChronicleTravel(int from, int to, ChronicleScene scene)
        {
            var start = Math.Abs(to - from) > 8 ? (to > from ? to - 8 : to + 8) : from;
            if (chronicleHost.Rig.Frame.pose != ChronicleBookPose.Open)
            { chronicleHost.Rig.SetPoseImmediate(ChronicleBookPose.Closed, start); chronicleHost.Rig.Perform(ChronicleBookAction.Open, profile.chronicle.NarratorState); ChronicleBookSound(ChronicleBookAction.Open); }
            else if (start != from) chronicleHost.Rig.SetPoseImmediate(ChronicleBookPose.Open, start);
            chronicleContinuation = new ChronicleContinuationPlan { fromSpread = from, toSpread = to, startSpread = start };
            chronicleTravelPlan = ChronicleJournal.PlanFlip(start, to, profile.reduceMotion); chronicleTravelStep = 0;
            chronicleContinueSpread = start; chronicleHeldScene = scene; chronicleContinueNextTurn = .2f;
            SetChronicleMilestonePhase(ChronicleMilestonePhase.Continuing);
        }

        private void FinishChronicleMilestone()
        {
            chronicleMilestoneActive = false; chronicleMilestoneScenes.Clear();
            var continuation = chronicleMilestoneContinue; chronicleMilestoneContinue = null;
            continuation?.Invoke();
            heldMenuAxis = Vector2Int.zero; menuAxisRepeatAt = Time.unscaledTime + .3f; ArmScreenInputGuard(.35f);
            storyEndResultStart = Time.unscaledTime; storyEndResultLast = Time.unscaledTime;
        }

        private bool DrawChronicleMilestone(float w, float h)
        {
            if (chronicleMilestonePhase == ChronicleMilestonePhase.FadeOut || chronicleMilestonePhase == ChronicleMilestonePhase.FadeIn)
            {
                DrawFullBackdrop(chronicleMilestoneVictory ? rewardBackground : combatBackground, w, h, .25f);
                var duration = chronicleMilestonePhase == ChronicleMilestonePhase.FadeOut ? (profile.reduceMotion ? .45f : 1.6f) : (profile.reduceMotion ? .3f : .9f);
                var fade = Mathf.SmoothStep(0, 1, chronicleMilestoneTime / duration);
                Fill(new Rect(0, 0, w, h), new Color(0, 0, 0, chronicleMilestonePhase == ChronicleMilestonePhase.FadeOut ? fade : 1 - fade));
                DrawChronicleThreads(w, h, .12f, false); return true;
            }
            if (chronicleMilestonePhase == ChronicleMilestonePhase.Playing)
            {
                DrawChronicleCore(w, h);
                DrawChronicleThreads(w, h, .16f, chronicleDirector?.Current?.State.pageDarkness > .1f);
                return true;
            }
            DrawChronicleAtmosphere(w, h);
            var reveal = chronicleMilestonePhase == ChronicleMilestonePhase.Emerge ? Mathf.SmoothStep(0, 1, chronicleMilestoneTime / (profile.reduceMotion ? .4f : 1.35f)) : 1;
            var size = Mathf.Min(w / 1600, h / 900) * Mathf.Lerp(.92f, 1, reveal);
            var lift = profile.reduceMotion ? 0 : Mathf.Sin(chronicleMilestoneTime * .8f) * h * .005f;
            var dest = new Rect((w - 1600 * size) * .5f, (h - 900 * size) * .5f + lift, 1600 * size, 900 * size);
            ShardSoft(new Rect(w * .26f, h * .15f, w * .48f, h * .7f), new Color(.07f, .45f, .22f, .18f * reveal));
            var old = GUI.color; GUI.color = new Color(1, 1, 1, reveal);
            if (Event.current.type == EventType.Repaint) GUI.DrawTexture(dest, chronicleHost.Texture, ScaleMode.StretchToFill, true);
            if (chronicleMilestonePhase == ChronicleMilestonePhase.Closing && chronicleDirector?.Current != null) DrawChroniclePages(dest, chronicleDirector.Current.State);
            // The final chapter becomes accessible: its existing contents title is written onto the newly reached page.
            if (chronicleMilestonePhase == ChronicleMilestonePhase.Continuing && chronicleSecretRevealed && chronicleContinuation != null && chronicleContinueSpread == chronicleContinuation.toSpread
                && chronicleHost.TryGetPageViewportRect(ChroniclePageSide.Right, out var revealView) && Event.current.type == EventType.Repaint)
            {
                var page = ChronicleGuiRect(dest, revealView); var u = page.width / 470f; var a = Mathf.SmoothStep(0f, 1f, chronicleRevealT / .9f) * reveal;
                var y = ArchiveText(page, page.y + page.height * .3f, "X", Mathf.RoundToInt(40 * u), ArchiveGold, a, false, TextAnchor.UpperCenter, 8 * u);
                ArchiveText(page, y, "The One Who Remembers", Mathf.RoundToInt(28 * u), ArchiveInk, a, false, TextAnchor.UpperCenter, 0);
            }
            GUI.color = old;
            DrawChronicleThreads(w, h, .2f * reveal, chronicleMilestonePhase == ChronicleMilestonePhase.Closing);
            return true;
        }

        private void DrawChronicleThreads(float w, float h, float alpha, bool tightening)
        {
            var count = profile.reducedVfx ? 3 : 7;
            var moving = !profile.reduceMotion; var time = moving ? shimmer : 0;
            for (var i = 0; i < count; i++)
            {
                var previous = new Vector2(-w * .1f, h * (.14f + i * .12f));
                for (var j = 1; j <= 30; j++)
                {
                    var x = j / 30f; var edge = Mathf.Abs(x - .5f) * 2;
                    var y = .14f + i * .12f + Mathf.Sin(x * 5 + time * .22f + i) * .025f;
                    if (tightening) y = Mathf.Lerp(.5f, y, .6f + edge * .4f);
                    var point = new Vector2(w * (x * 1.2f - .1f), h * y);
                    // Keep threads around the book, away from the writing and illustration.
                    var opacity = alpha * Mathf.Clamp01((edge - .63f) * 5);
                    ChronicleArtLine(previous, point, new Color(.92f, .72f, .3f, opacity), profile.reduceFlashing ? 1 : 1.3f);
                    previous = point;
                }
            }
        }
    }
}
