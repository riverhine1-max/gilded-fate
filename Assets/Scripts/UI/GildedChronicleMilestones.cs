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
        private enum ChronicleMilestonePhase { FadeOut, Emerge, Playing, Closing, FadeIn }
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
                        chronicleHost.SetActive(true); chronicleHost.Rig.SetPoseImmediate(ChronicleBookPose.Closed, 0); chronicleHost.Tick(0, null);
                        SetChronicleMilestonePhase(ChronicleMilestonePhase.Emerge);
                        break;
                    case ChronicleMilestonePhase.Emerge:
                        chronicleHost.Tick(dt, null);
                        if (chronicleMilestoneTime >= (profile.reduceMotion ? .4f : 1.35f)) PlayNextChronicleMilestone();
                        break;
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
            while (chronicleMilestoneScenes.Count > 0)
            {
                var scene = chronicleMilestoneScenes.Dequeue();
                if (scene == null) continue;
                var presentationOnly = scene.id == ChronicleMilestoneScenes.VictoryId || scene.id == ChronicleMilestoneScenes.DefeatId;
                // The shared ending never unlocks Reaper's memory, facts, corrections or Chapter X.
                if (!PlayChronicleSceneData(scene, presentationOnly, PlayNextChronicleMilestone, true, !chronicleMilestoneOpened)) continue;
                chronicleMilestoneOpened = true;
                SetChronicleMilestonePhase(ChronicleMilestonePhase.Playing); return;
            }
            chronicleMilestoneCloseDuration = chronicleHost.Rig.DurationOf(ChronicleBookAction.Close);
            chronicleHost.Rig.Perform(ChronicleBookAction.Close, profile.chronicle.NarratorState);
            ChronicleBookSound(ChronicleBookAction.Close);
            SetChronicleMilestonePhase(ChronicleMilestonePhase.Closing);
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
