using System;
using System.Collections.Generic;
using GildedFate.Audio;
using GildedFate.Chronicle;
using GildedFate.ChronicleBook;
using GildedFate.Saving;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GildedFate.UI
{
    // The Chronicle's on-screen presentation. Like the boot cinematic it is an overlay: a flag advanced in Update with unscaled
    // time and drawn in OnGUI, blocking menu input while it is active.
    //
    //   3D book (RenderTexture from ChronicleBookStageHost)
    //     + page text, red strikes and the placeholder illustration, drawn by this file with the game's normal UI tools
    //
    // The story itself (ChronicleDirector / ChronicleSequencer) never knows any of this exists.
    public sealed partial class GildedMainMenu
    {
        private enum ChronicleStage { Playing, WaitingToClose, Closing }

        private ChronicleBookStageHost chronicleHost;
        private ChronicleDirector chronicleDirector;
        private ChronicleProgressSink chronicleSink;
        private ChronicleStage chronicleStage;
        private bool chronicleActive;
        private float chronicleElapsed, chronicleEndedAt, chronicleCloseHold;
        private Action chronicleOnEnd;
        private readonly Dictionary<ChronicleTextBlock, KeyValuePair<int, string>> chronicleRichCache = new();
        private readonly Dictionary<int, GUIStyle> chronicleStyles = new(), chronicleMeasureStyles = new();

        public bool ChronicleIsPlaying => chronicleActive;

        /// <summary>Plays a Chronicle scene on the 3D book as a full-screen overlay. preview = true saves nothing.
        /// Returns false if the book cannot be shown (a missing shader, say); the game then carries on without it.</summary>
        public bool PlayChronicleScene(string sceneId, bool preview = false, Action onEnd = null)
        {
            if (chronicleActive || profile == null) return false;
            var scene = ChronicleScripts.Get(sceneId);
            if (scene == null) { Debug.LogWarning("[Chronicle] No scene '" + sceneId + "'."); return false; }
            chronicleHost = chronicleHost ?? ChronicleBookStageHost.Create(1600, 900);
            if (chronicleHost == null) { onEnd?.Invoke(); return false; }
            if (chronicleDirector == null) { chronicleDirector = new ChronicleDirector(); chronicleDirector.SceneEnded += OnChronicleSceneEnded; }

            var progress = profile.chronicle; progress.Ensure();
            var settings = progress.ToSettings(profile.reduceMotion, profile.reduceFlashing);
            chronicleHost.Rig.SetPoseImmediate(ChronicleBookPose.Closed, 0);
            if (chronicleHost.Rig is ChronicleProceduralBook procedural) procedural.Logic.reduceMotion = profile.reduceMotion;
            var stage = new ChronicleRigStage(chronicleHost.Rig) { audio = ChronicleAudio, stopAudio = () => { }, onBookAction = ChronicleBookSound };
            var replay = sceneId == ChronicleCatalog.OpeningSceneId || sceneId == ChronicleCatalog.OpeningShortSceneId ? progress.openingViews > 0 : progress.IsViewed(sceneId);
            chronicleSink = preview ? null : new ChronicleProgressSink(progress, false);
            if (!chronicleDirector.TryPlay(scene, stage, new ChronicleProgressContext(progress), chronicleSink, settings, ChronicleBookPose.Closed, replay)) return false;

            chronicleHost.SetActive(true);
            chronicleActive = true; chronicleStage = ChronicleStage.Playing; chronicleElapsed = 0f; chronicleOnEnd = onEnd;
            return true;
        }

        private void OnChronicleSceneEnded(ChronicleSequencer sequencer)
        {
            chronicleRichCache.Clear();
            if (chronicleSink != null) ProfileService.Save(profile);   // facts, corrections and viewed state are already committed in memory
            chronicleEndedAt = Time.unscaledTime;
            if (sequencer.Scene.endPose == ChronicleBookPose.Closed) { chronicleStage = ChronicleStage.Closing; chronicleCloseHold = .6f; }
            else chronicleStage = ChronicleStage.WaitingToClose;
        }

        private void AdvanceChronicle(float delta)
        {
            delta = Mathf.Clamp(delta, 0f, .1f); chronicleElapsed += delta; shimmer += delta;
            UpdateAudioPresentation();
            if (chronicleHost == null) { EndChronicle(); return; }
            chronicleDirector.Update(delta);
            chronicleHost.Tick(delta, chronicleDirector.Current?.State.camera);
            var pressed = ChronicleSkipPressed();
            switch (chronicleStage)
            {
                case ChronicleStage.Playing:
                    if (pressed) chronicleDirector.RequestSkip();   // ignored during the first fraction of a second, so the press that opened the scene cannot cancel it
                    break;
                case ChronicleStage.WaitingToClose:
                    if (pressed && Time.unscaledTime - chronicleEndedAt > .4f)
                    {
                        chronicleHost.Rig.Perform(ChronicleBookAction.Close, ChronicleNarratorState.ConfidentHistorian); ChronicleBookSound(ChronicleBookAction.Close);
                        chronicleStage = ChronicleStage.Closing; chronicleEndedAt = Time.unscaledTime; chronicleCloseHold = chronicleHost.Rig.DurationOf(ChronicleBookAction.Close) + .35f;
                    }
                    break;
                case ChronicleStage.Closing:
                    if (Time.unscaledTime - chronicleEndedAt > chronicleCloseHold) EndChronicle();
                    break;
            }
        }

        private void EndChronicle()
        {
            chronicleActive = false; chronicleRichCache.Clear();
            chronicleHost?.SetActive(false);
            // The same hand-back the boot cinematic does: the press that skipped must not also activate a menu item.
            heldMenuAxis = Vector2Int.zero; menuAxisRepeatAt = Time.unscaledTime + .25f; ArmScreenInputGuard();
            var callback = chronicleOnEnd; chronicleOnEnd = null; callback?.Invoke();
        }

        private bool ChronicleSkipPressed()
        {
            var keyboard = Keyboard.current; if (keyboard != null && keyboard.anyKey != null && keyboard.anyKey.wasPressedThisFrame) return true;
            var mouse = Mouse.current; if (mouse != null && ((mouse.leftButton != null && mouse.leftButton.wasPressedThisFrame) || (mouse.rightButton != null && mouse.rightButton.wasPressedThisFrame))) return true;
            var touch = Touchscreen.current; if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) return true;
            var pad = Gamepad.current;
            if (pad != null)
                foreach (var b in new[] { pad.buttonSouth, pad.buttonEast, pad.buttonWest, pad.buttonNorth, pad.startButton, pad.selectButton, pad.leftShoulder, pad.rightShoulder })
                    if (b != null && b.wasPressedThisFrame) return true;
            return false;
        }

        // ---------------- sound (placeholder mapping onto the game's existing cues; missing cues stay silent) ----------------
        private void ChronicleAudio(string cue, float volume)
        {
            switch (cue)
            {
                case "page_rustle": case "book_open": case "page_turn": Sfx(SoundCue.EventReveal, intensity: .5f * volume); break;
                case "red_ink": Sfx(SoundCue.Debuff, intensity: .7f * volume); break;
                case "magic_hum": case "emerald_glint": Sfx(SoundCue.Resonance, intensity: .6f * volume); break;
                case "deeper_voice": Sfx(SoundCue.BossPhase, intensity: .7f * volume); break;
                case "fate_crack": Sfx(SoundCue.HitHeavy, intensity: .6f * volume); break;
            }
        }
        private void ChronicleBookSound(ChronicleBookAction action)
        {
            if (action == ChronicleBookAction.Open || action == ChronicleBookAction.Close) Sfx(SoundCue.EventReveal, intensity: .7f);
            else if (action == ChronicleBookAction.TurnPageForward || action == ChronicleBookAction.TurnPageBack) Sfx(SoundCue.EventReveal, intensity: .4f);
            else if (action == ChronicleBookAction.MajorMagic) Sfx(SoundCue.Resonance, intensity: .7f);
        }

        // ---------------- drawing ----------------
        private bool DrawChronicle(float w, float h)
        {
            if (!chronicleActive || chronicleHost == null) return false;
            Fill(new Rect(0, 0, w, h), new Color(.02f, .015f, .02f, 1f));
            // Fit the 16:9 book render inside the screen; page rectangles are mapped through the same rectangle.
            var scale = Mathf.Min(w / 1600f, h / 900f); var dest = new Rect((w - 1600f * scale) * .5f, (h - 900f * scale) * .5f, 1600f * scale, 900f * scale);
            if (Event.current.type == EventType.Repaint) GUI.DrawTexture(dest, chronicleHost.Texture, ScaleMode.StretchToFill, false);
            var sequencer = chronicleDirector.Current;
            if (sequencer != null) DrawChroniclePages(dest, sequencer.State);
            DrawChronicleHint(w, h);
            return true;
        }

        private Rect ChronicleGuiRect(Rect dest, Rect viewport) => new Rect(dest.x + viewport.x * dest.width, dest.y + (1f - viewport.yMax) * dest.height, viewport.width * dest.width, viewport.height * dest.height);

        private void DrawChroniclePages(Rect dest, ChronicleSceneState state)
        {
            var frame = chronicleHost.Rig.Frame;
            if (!frame.overlayVisible) return;
            if (!chronicleHost.TryGetPageViewportRect(ChroniclePageSide.Left, out var leftView) || !chronicleHost.TryGetPageViewportRect(ChroniclePageSide.Right, out var rightView)) return;
            var left = ChronicleGuiRect(dest, leftView); var right = ChronicleGuiRect(dest, rightView);
            var alpha = frame.overlayAlpha;

            // While a sheet is lifting away, keep drawing the page that was there; the new spread appears as it lands.
            IList<ChronicleTextBlock> blocks = state.blocks; var layout = state.layout; var illustration = state.illustration;
            if (frame.flipDirection != 0 && frame.flipRaw < .5f && state.outgoing != null) { blocks = state.outgoing.blocks; layout = state.outgoing.layout; illustration = state.outgoing.illustration; }

            if (layout == ChronicleSpreadLayout.IllustrationLeftTextRight || layout == ChronicleSpreadLayout.IllustrationOnly) DrawChronicleIllustration(left, illustration, alpha);
            else if (layout == ChronicleSpreadLayout.TextLeftIllustrationRight) DrawChronicleIllustration(right, illustration, alpha);
            DrawChronicleText(left, blocks, ChroniclePageSide.Left, state, alpha);
            DrawChronicleText(right, blocks, ChroniclePageSide.Right, state, alpha);
            if (state.pageDarkness > 0f) { Fill(left, new Color(0, 0, 0, state.pageDarkness * .35f * alpha)); Fill(right, new Color(0, 0, 0, state.pageDarkness * .35f * alpha)); }
            if (state.pageGlow > 0f && !(profile != null && profile.reduceFlashing)) { var c = new Color(.2f, .9f, .6f, Mathf.Clamp01(state.pageGlow) * .2f * alpha); ShardSoft(left, c); ShardSoft(right, c); }
        }

        private void DrawChronicleHint(float w, float h)
        {
            var text = chronicleStage == ChronicleStage.Playing ? "ANY KEY  ·  SKIP" : chronicleStage == ChronicleStage.WaitingToClose ? "ANY KEY  ·  CLOSE THE CHRONICLE" : "";
            if (text.Length == 0) return;
            var pulse = profile != null && profile.reduceMotion ? .7f : .55f + Mathf.Sin(shimmer * 2.2f) * .2f;
            var style = new GUIStyle(ReadableStyle(13, true)) { alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(.85f, .72f, .45f, pulse) } };
            GUI.Label(new Rect(0, h - 40f, w, 24f), text, style);
        }

        // ---------------- text ----------------
        private GUIStyle ChronicleStyle(int size, bool italic)
        {
            var key = size * 2 + (italic ? 1 : 0);
            if (chronicleStyles.TryGetValue(key, out var style)) return style;
            style = new GUIStyle(GUI.skin.label) { font = headingFont ? headingFont : bodyFont, fontSize = size, wordWrap = true, richText = true, alignment = TextAnchor.UpperLeft, fontStyle = italic ? FontStyle.Italic : FontStyle.Normal };
            chronicleStyles[key] = style; return style;
        }
        private GUIStyle ChronicleMeasureStyle(int size)
        {
            if (chronicleMeasureStyles.TryGetValue(size, out var style)) return style;
            style = new GUIStyle(ChronicleStyle(size, false)) { wordWrap = false }; chronicleMeasureStyles[size] = style; return style;
        }

        // The whole passage is laid out from the first frame; the part not yet written is made transparent, so words never jump as the pen moves.
        private string ChronicleWritten(ChronicleTextBlock b)
        {
            if (b.complete) return b.text;
            if (chronicleRichCache.TryGetValue(b, out var cached) && cached.Key == b.cursor) return cached.Value;
            var n = Mathf.Clamp(b.cursor, 0, b.text.Length);
            var rich = b.text.Substring(0, n) + "<color=#00000000>" + b.text.Substring(n) + "</color>";
            chronicleRichCache[b] = new KeyValuePair<int, string>(n, rich); return rich;
        }

        private static Color ChronicleInk(ChronicleTextBlock b, ChronicleSceneState state)
        {
            switch (b.style)
            {
                case ChronicleTextStyle.Correction: { var c = ChronicleColor.RedInk; return new Color(c.r, c.g, c.b); }
                case ChronicleTextStyle.DeeperVoice: return new Color(.26f, .12f, .46f);
                case ChronicleTextStyle.Note: return new Color(.42f, .33f, .22f);
                default: { var c = ChronicleNarratorStyle.For(state.narrator).inkTint; return new Color(c.r, c.g, c.b); }
            }
        }

        private void DrawChronicleText(Rect page, IList<ChronicleTextBlock> blocks, ChroniclePageSide side, ChronicleSceneState state, float alpha)
        {
            var unit = page.width / 470f; var y = page.y;
            foreach (var b in blocks)
            {
                if (b.side != side || (b.erased && b.eraseProgress >= 1f)) continue;
                var scale = b.style == ChronicleTextStyle.Heading ? 1.4f : b.style == ChronicleTextStyle.Ending ? 1.6f : 1f;
                var size = Mathf.Max(9, Mathf.RoundToInt(22f * unit * scale));
                var italic = b.style == ChronicleTextStyle.Dialogue || b.style == ChronicleTextStyle.DeeperVoice;
                var style = ChronicleStyle(size, italic); var ink = ChronicleInk(b, state);
                var fade = (b.erased ? 1f - b.eraseProgress : 1f) * alpha;
                if (!string.IsNullOrEmpty(b.speaker))
                {
                    var small = ChronicleStyle(Mathf.Max(8, Mathf.RoundToInt(13f * unit)), false);
                    small.normal.textColor = new Color(ink.r, ink.g, ink.b, fade * .8f);
                    GUI.Label(new Rect(page.x, y, page.width, size * 1.3f), b.speaker, small); y += size * 1.05f;
                }
                style.normal.textColor = new Color(ink.r, ink.g, ink.b, fade);
                var height = style.CalcHeight(new GUIContent(b.text), page.width);
                GUI.Label(new Rect(page.x, y, page.width, height + 4f), ChronicleWritten(b), style);
                if (b.struck && b.strikeProgress > 0f) DrawChronicleStrike(new Rect(page.x, y, page.width, height), size, b.text, b.strikeProgress, fade);
                y += height + 14f * unit;
            }
        }

        // A red line through every line of the sentence, growing as the pen strikes it.
        private void DrawChronicleStrike(Rect area, int size, string text, float progress, float alpha)
        {
            var measure = ChronicleMeasureStyle(size);
            var lineHeight = measure.CalcHeight(new GUIContent("Ag"), 9999f);
            var lines = Mathf.Max(1, Mathf.RoundToInt(area.height / Mathf.Max(1f, lineHeight)));
            var total = measure.CalcSize(new GUIContent(text)).x; var remaining = total; var drawn = 0f; var budget = Mathf.Clamp01(progress) * total;
            var colour = new Color(.78f, .08f, .08f, alpha);
            for (var i = 0; i < lines && remaining > 0f; i++)
            {
                var span = Mathf.Min(area.width, remaining); var segment = Mathf.Min(span, Mathf.Max(0f, budget - drawn));
                if (segment > 0f) { var yy = area.y + lineHeight * (i + .58f); DrawLine(new Vector2(area.x, yy), new Vector2(area.x + segment, yy), colour, Mathf.Max(2f, lineHeight * .09f)); }
                drawn += span; remaining -= span;
            }
        }

        // ---------------- the placeholder illustration ----------------
        private static Color ChronCol(ChronicleColor c, float a) => new Color(c.r, c.g, c.b, c.a * a);

        private void ChronFillIn(Rect bounds, Rect r, Color c)
        {
            var x0 = Mathf.Max(bounds.x, r.x); var y0 = Mathf.Max(bounds.y, r.y); var x1 = Mathf.Min(bounds.xMax, r.xMax); var y1 = Mathf.Min(bounds.yMax, r.yMax);
            if (x1 > x0 && y1 > y0) Fill(new Rect(x0, y0, x1 - x0, y1 - y0), c);
        }
        private void ChronDisc(Rect bounds, Vector2 centre, float rx, float ry, Color c)
        {
            const int slices = 10;
            for (var i = 0; i < slices; i++)
            {
                var t = (i + .5f) / slices * 2f - 1f; var half = Mathf.Sqrt(Mathf.Max(0f, 1f - t * t)) * rx; var hh = ry * 2f / slices;
                ChronFillIn(bounds, new Rect(centre.x - half, centre.y - ry + i * hh, half * 2f, hh + .5f), c);
            }
        }
        private static bool ChronInside(Rect bounds, Vector2 p, float margin) => p.x > bounds.x - margin && p.x < bounds.xMax + margin && p.y > bounds.y - margin && p.y < bounds.yMax + margin;

        private void DrawChronicleIllustration(Rect r, ChronicleIllustrationState ill, float alpha)
        {
            var a = Mathf.Clamp01(ill.alpha) * alpha; if (a <= .003f || !ill.visible && ill.alpha <= .003f) return;
            const int bands = 14;
            for (var i = 0; i < bands; i++)
                Fill(new Rect(r.x, r.y + r.height * i / bands, r.width, r.height / bands + 1f), ChronCol(ChronicleColor.Lerp(ill.top, ill.bottom, i / (bands - 1f)), a));
            foreach (var actor in ill.actors) DrawChronicleActor(r, actor, a);
            foreach (var anim in ill.animations) DrawChronicleEffect(r, ill, anim, a);
            Outline(r, new Color(.16f, .1f, .06f, a), 2);
            Outline(new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8), new Color(.55f, .4f, .2f, a * .6f), 1);
        }

        private void DrawChronicleActor(Rect r, ChronicleActor ac, float a)
        {
            var alpha = a * ac.alpha; if (alpha <= .003f) return;
            var box = new Rect(r.x + (ac.x - ac.w * .5f) * r.width, r.y + (ac.y - ac.h * .5f) * r.height, ac.w * r.width, ac.h * r.height);
            var col = ChronCol(ac.color, alpha); var centre = box.center;
            switch (ac.shape)
            {
                case ChronicleShape.Figure:
                    ChronFillIn(r, new Rect(box.x + box.width * .12f, box.y + box.height * .28f, box.width * .76f, box.height * .72f), col);
                    { var hr = Mathf.Min(box.width, box.height) * .28f; ChronDisc(r, new Vector2(centre.x, box.y + box.height * .16f), hr, hr, col); }
                    break;
                case ChronicleShape.Rect: case ChronicleShape.Page:
                    ChronFillIn(r, box, col);
                    if (ac.shape == ChronicleShape.Page) { Outline(box, new Color(.3f, .2f, .1f, alpha), 1); for (var l = 1; l < 7; l++) ChronFillIn(r, new Rect(box.x + box.width * .1f, box.y + box.height * l / 7f, box.width * (.55f + .3f * ((l * 37) % 5) / 5f), 1.5f), new Color(.2f, .12f, .06f, alpha * .6f)); }
                    break;
                case ChronicleShape.Circle: ChronDisc(r, centre, box.width * .5f, box.height * .5f, col); break;
                case ChronicleShape.Castle:
                    ChronFillIn(r, new Rect(box.x, box.y + box.height * .3f, box.width, box.height * .7f), col);
                    ChronFillIn(r, new Rect(box.x, box.y, box.width * .2f, box.height), col); ChronFillIn(r, new Rect(box.xMax - box.width * .2f, box.y, box.width * .2f, box.height), col);
                    for (var m = 0; m < 5; m++) ChronFillIn(r, new Rect(box.x + box.width * (.24f + m * .13f), box.y + box.height * .22f, box.width * .07f, box.height * .1f), col);
                    ChronFillIn(r, new Rect(centre.x - box.width * .08f, box.yMax - box.height * .3f, box.width * .16f, box.height * .3f), new Color(0, 0, 0, alpha * .7f));
                    break;
                case ChronicleShape.Eye:
                    ChronDisc(r, centre, box.width * .5f, box.height * .5f, col); ChronDisc(r, centre, box.width * .14f, box.height * .4f, new Color(0, 0, 0, alpha));
                    break;
                case ChronicleShape.Shadow:
                    ChronFillIn(r, box, new Color(col.r, col.g, col.b, col.a * .85f));
                    if (ChronInside(r, centre, 0f)) ShardSoft(new Rect(box.x - box.width * .3f, box.y - box.height * .1f, box.width * 1.6f, box.height * 1.2f), new Color(0, 0, 0, alpha * .25f));
                    break;
                case ChronicleShape.Mote: ChronDisc(r, centre, Mathf.Max(2f, box.width * .5f), Mathf.Max(2f, box.width * .5f), col); if (ChronInside(r, centre, 0f)) ShardSoft(centre, box.width * 3f, new Color(col.r, col.g, col.b, alpha * .4f)); break;
                case ChronicleShape.Clock:
                    if (ChronInside(r, centre, 0f)) { ShardCircle(centre, box.width * .5f, box.height * .5f, col, 2f, 28); DrawLine(centre, centre + new Vector2(0, -box.height * .35f), col, 2f); DrawLine(centre, centre + new Vector2(box.width * .25f, 0), col, 2f); }
                    break;
                case ChronicleShape.Glyph:
                    if (ChronInside(r, centre, 0f))
                    {
                        var s = Mathf.Max(8f, Mathf.Min(box.width, box.height) * .5f);
                        EventGem(centre, s, col, false); ChronDisc(r, centre, s * .3f, s * .3f, col);
                        if (!string.IsNullOrEmpty(ac.label) && ac.shape == ChronicleShape.Glyph) GUI.Label(new Rect(centre.x - 60, centre.y + s + 2, 120, 18), ac.label, new GUIStyle(ReadableStyle(11, true)) { alignment = TextAnchor.MiddleCenter, normal = { textColor = col } });
                    }
                    break;
                case ChronicleShape.Line:
                {
                    var from = new Vector2(Mathf.Max(r.x, box.x), centre.y); var to = new Vector2(Mathf.Min(r.xMax, box.xMax), centre.y);
                    if (to.x > from.x && centre.y > r.y && centre.y < r.yMax) DrawLine(from, to, col, 2.5f);
                    break;
                }
            }
            if (!string.IsNullOrEmpty(ac.label) && ac.shape != ChronicleShape.Glyph && ac.shape != ChronicleShape.Page && alpha > .3f && ChronInside(r, centre, 0f))
                GUI.Label(new Rect(centre.x - 50, box.yMax + 2, 100, 16), ac.label, new GUIStyle(ReadableStyle(10, true)) { alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(.9f, .85f, .7f, alpha * .85f) } });
            if (ac.speakTimer > 0f && ChronInside(r, centre, 0f)) for (var d = 0; d < 3; d++) ChronDisc(r, new Vector2(centre.x - 8 + d * 8, box.y - 10), 2.5f, 2.5f, new Color(1, 1, 1, alpha));
        }

        private void DrawChronicleEffect(Rect r, ChronicleIllustrationState ill, ChronicleAnimation anim, float a)
        {
            var p = anim.Progress; var centre = r.center;
            if (!string.IsNullOrEmpty(anim.target)) { var t = ill.Find(anim.target); if (t != null) centre = new Vector2(r.x + t.x * r.width, r.y + t.y * r.height); }
            switch (anim.name)
            {
                case "flash": Fill(r, new Color(1, 1, 1, (1f - p) * .6f * anim.intensity * a)); break;
                case "pulse": case "expand_circle": case "echo_ring":
                { var rad = r.width * .32f * p; if (ChronInside(r, centre, rad)) ShardCircle(centre, rad, rad, new Color(1f, .9f, .6f, (1f - p) * .7f * a), 2f, 40); break; }
                case "threads": case "branch":
                    for (var i = 0; i < 6; i++) { var y = r.y + r.height * (i + 1) / 7f; DrawLine(new Vector2(r.x + 6, y), new Vector2(r.x + 6 + (r.width - 12) * p, y + Mathf.Sin(i * 1.7f + shimmer) * 6f), new Color(1f, .8f, .4f, .55f * a), 2f); }
                    break;
                case "fracture": case "shatter_world": case "crack_pass": case "split_screen":
                    for (var i = 0; i < 6; i++)
                    {
                        var x = r.x + r.width * (i + .5f) / 6f; var bottom = r.y + r.height * Mathf.Clamp01(p * (.6f + i % 3 * .2f));
                        DrawLine(new Vector2(x, r.y), new Vector2(x + (i % 2 == 0 ? 10f : -10f), (r.y + bottom) * .5f), new Color(1, 1, 1, .5f * a), 2f);
                        DrawLine(new Vector2(x + (i % 2 == 0 ? 10f : -10f), (r.y + bottom) * .5f), new Vector2(x, bottom), new Color(1, 1, 1, .5f * a), 2f);
                    }
                    break;
                case "sigil_glow": ShardSoft(centre, r.width * .8f * Mathf.Max(.2f, p), new Color(.3f, .9f, .6f, .28f * p * a)); break;
            }
        }
    }
}
