using System;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Audio;
using GildedFate.Chronicle;
using GildedFate.ChronicleBook;
using GildedFate.Saving;
using UnityEngine;

namespace GildedFate.UI
{
    // THE CHRONICLE, the permanent story archive reached from the main menu's Archive hub.
    // It is the book itself: contents on the first spread, one spread per chapter, and a reading view for each memory.
    // What each page shows and what each button does lives in ChronicleArchive / ChronicleArchiveController (engine-free, tested);
    // this file only draws it and forwards input. If the 3D book cannot be built the same pages are drawn flat.
    public sealed partial class GildedMainMenu
    {
        private ChronicleArchiveController archive;
        private Texture2D chronicleArchiveIcon;
        private bool archiveFlipActive, archiveSyncBook, archiveClosing;
        private float archiveClosingAt;
        private int archiveOutSpread, archiveOutReadingSpread;
        private string archiveOutReadingId = "";
        // A chapter jump is a physical journey: one real sheet per spread crossed, quick through the middle, slowing at the end.
        private ChronicleFlipPlan archiveFlipPlan; private int archiveFlipStep;
        private readonly Dictionary<string, ChronicleMemoryReading> archiveReadings = new();
        private readonly Dictionary<int, GUIStyle> archiveStyles = new();
        private readonly ChronicleSceneState archiveInkState = new();
        private readonly List<(string title, string sub, bool big, float at)> chronicleToasts = new();

        private static readonly Color ArchiveInk = new(.10f, .055f, .025f), ArchiveDim = new(.24f, .15f, .08f), ArchiveRed = new(.68f, .035f, .025f), ArchiveGold = new(.34f, .21f, .07f);
        private static string ChronicleRoman(int n) => new[] { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" }[Mathf.Clamp(n, 0, 10)];

        // ---------------- entering and leaving ----------------
        private void OpenChronicleArchive()
        {
            if (profile == null) return;
            var progress = profile.chronicle; progress.Ensure();
            // Reading pages are laid out with the same hand and paper the screen draws, so page counts always match.
            archive = new ChronicleArchiveController(progress, ChronicleArchiveController.ContentsSpread, () => chronicleHost != null ? ChronicleWritingMetrics() : null);
            archive.GetNarrationVolume = () => ChronicleDialogueAudio.VoiceVolume; archive.SetNarrationVolume = v => ChronicleDialogueAudio.VoiceVolume = v;   // the game's existing Narration setting
            if (ChronicleJournal.HasReadingPosition(progress)) archive.SetFocusIndex(ChronicleArchiveController.ContentsOptionStart + ChronicleArchiveController.OptContinue);
            archiveReadings.Clear(); archiveFlipActive = archiveSyncBook = archiveClosing = false;
            chronicleHost = chronicleHost ?? ChronicleBookStageHost.Create(2400, 1350);
            if (chronicleHost != null)
            {
                chronicleHost.SetActive(true);
                chronicleHost.Rig.SetPoseImmediate(ChronicleBookPose.Closed, 0);
                if (chronicleHost.Rig is ChronicleProceduralBook procedural) procedural.Logic.reduceMotion = profile.reduceMotion;
                chronicleHost.Rig.Perform(ChronicleBookAction.Open, progress.NarratorState); ChronicleBookSound(ChronicleBookAction.Open);
            }
            else Debug.LogWarning("[Chronicle] The 3D book could not be built, so the archive is drawn flat.");
            if (progress.TakePendingNotices().Count > 0) ProfileService.Save(profile);   // the player has now seen what is new
            screen = ScreenMode.Chronicle; screenControllerIndex = 0;
        }

        // Leaving with BACK closes the book first, and only then returns to the menu. Input is ignored while the cover swings shut.
        private void BeginCloseChronicleArchive()
        {
            if (archiveClosing) return;
            if (chronicleHost == null || chronicleActive) { CloseChronicleArchive(); return; }   // flat fallback: nothing to close
            if (archiveFlipPlan != null) EndArchiveFlip();
            archiveClosing = true; archiveClosingAt = Time.unscaledTime; archiveFlipActive = archiveSyncBook = false;
            chronicleHost.Rig.Perform(ChronicleBookAction.Close, profile.chronicle.NarratorState); ChronicleBookSound(ChronicleBookAction.Close);
        }

        // Leaves immediately. Used once the book has closed, and as the failsafe when something goes wrong.
        private void CloseChronicleArchive()
        {
            archiveClosing = false;
            if (chronicleHost != null && !chronicleActive) chronicleHost.SetActive(false);
            if (profile != null) ProfileService.Save(profile);   // the reading place and page are kept for next time
            archive = null; archiveReadings.Clear(); screen = ScreenMode.Menu;
        }

        // Called every Update while the archive is on screen: advances the book and keeps its stack in step after multi-page jumps.
        private void TickChronicleArchive(float dt)
        {
            if (screen != ScreenMode.Chronicle || chronicleActive || chronicleHost == null || archive == null) return;
            try { TickChronicleArchiveCore(dt); }
            catch (Exception error) { Debug.LogError("[Chronicle] The archive failed, returning to the menu: " + error); CloseChronicleArchive(); }
        }

        private void TickChronicleArchiveCore(float dt)
        {
            dt = Mathf.Clamp(dt, 0f, .1f); shimmer += dt;
            chronicleHost.Tick(dt, null);
            var frame = chronicleHost.Rig.Frame;
            if (archiveClosing)
            {
                if (Time.unscaledTime - archiveClosingAt >= chronicleHost.Rig.DurationOf(ChronicleBookAction.Close) + .3f) CloseChronicleArchive();
                return;   // no page sync while the cover closes, or it would spring back open
            }
            if (archiveFlipPlan != null)
            {
                if (!frame.busy) { if (archiveFlipStep < archiveFlipPlan.Turns) ArchiveFlipNext(); else EndArchiveFlip(); }
                return;
            }
            if (archiveFlipActive && frame.flipDirection == 0 && !frame.busy) archiveFlipActive = false;
            if (archiveSyncBook && !frame.busy) { archiveSyncBook = false; chronicleHost.Rig.SetPoseImmediate(ChronicleBookPose.Open, archive.Spread); }
        }

        // ---------------- input ----------------
        private bool HandleChronicleArchiveNavigation(MenuNavigation input)
        {
            if (archive == null) { screen = ScreenMode.Menu; return true; }
            if (archiveClosing || !input.Any) return true;
            if (archiveFlipPlan != null) { EndArchiveFlip(); return true; }   // any press during a long journey lands on the destination at once
            controllerNavigation = true;
            ChronicleArchiveResult result;
            if (input.back) result = archive.Back();
            else if (input.accept) result = archive.Accept();
            else if (input.page != 0) result = archive.Page(input.page);
            else result = archive.Move(input.x, input.y);
            ApplyArchiveResult(result);
            return true;
        }

        private void ApplyArchiveResult(ChronicleArchiveResult r)
        {
            switch (r.action)
            {
                case ChronicleArchiveAction.Moved: Sfx(SoundCue.UiHover); break;
                case ChronicleArchiveAction.TurnedPage:
                    Sfx(SoundCue.UiHover);
                    if (chronicleHost != null)
                    {
                        var reading = !string.IsNullOrEmpty(r.id);
                        archiveOutSpread = reading ? archive.Spread : r.fromSpread; archiveOutReadingId = reading ? r.id : ""; archiveOutReadingSpread = reading ? r.fromSpread : 0;
                        if (!reading && Mathf.Abs(r.toSpread - r.fromSpread) > 1 && !profile.reduceMotion)
                        {
                            archiveFlipPlan = ChronicleJournal.PlanFlip(r.fromSpread, r.toSpread); archiveFlipStep = 0;
                            archiveFlipActive = true; archiveSyncBook = false; ArchiveFlipNext(); break;
                        }
                        var forward = r.toSpread > r.fromSpread; var action = forward ? ChronicleBookAction.TurnPageForward : ChronicleBookAction.TurnPageBack;
                        chronicleHost.Rig.Perform(action, profile.chronicle.NarratorState); ChronicleBookSound(action);
                        archiveFlipActive = true; archiveSyncBook = true;
                    }
                    if (!string.IsNullOrEmpty(r.id)) ProfileService.Save(profile);   // a page turned inside a reading is remembered at once
                    break;
                case ChronicleArchiveAction.OpenedReading: Sfx(SoundCue.UiConfirm); ProfileService.Save(profile); break;
                case ChronicleArchiveAction.ClosedReading: Sfx(SoundCue.UiBack); ProfileService.Save(profile); break;
                case ChronicleArchiveAction.ChangedOption:
                    Sfx(SoundCue.UiHover);
                    if (r.option == ChronicleArchiveController.OptTextSize) { archiveReadings.Clear(); chronicleRichCache.Clear(); }   // reflow the pages
                    ProfileService.Save(profile); break;
                case ChronicleArchiveAction.Replay: PlayArchiveScene(r.id); break;
                case ChronicleArchiveAction.BeginSecret: PlayArchiveScene(ChronicleCatalog.SecretSceneId); break;
                case ChronicleArchiveAction.Exit: Sfx(SoundCue.UiBack); BeginCloseChronicleArchive(); break;
            }
        }

        // Replays run on the same open book and return to the exact page they started from. They never grant anything twice.
        private void PlayArchiveScene(string sceneId)
        {
            var spread = archive.Spread; var readingId = archive.ReadingId;
            if (!PlayChronicleScene(sceneId, false, () => ReturnFromArchiveScene(spread, readingId), true)) { banner = "THE CHRONICLE CANNOT BE SHOWN RIGHT NOW"; Debug.LogWarning("[Chronicle] Could not play " + sceneId + " from the archive."); }
        }

        private void ReturnFromArchiveScene(int spread, string readingId)
        {
            archiveReadings.Clear(); archiveFlipActive = archiveSyncBook = false;
            if (chronicleHost != null) { chronicleHost.SetActive(true); chronicleHost.Rig.SetPoseImmediate(ChronicleBookPose.Open, spread); }
            archive?.Restore(spread, readingId);
        }

        private void ArchiveFlipNext()
        {
            if (archiveFlipPlan == null || chronicleHost == null) return;
            var action = archiveFlipPlan.direction > 0 ? ChronicleBookAction.TurnPageForward : ChronicleBookAction.TurnPageBack;
            SetChronicleTempo(archiveFlipPlan.tempos[archiveFlipStep++]);
            chronicleHost.Rig.Perform(action, profile.chronicle.NarratorState); ChronicleBookSound(action);
        }

        // Ends a journey (finished or skipped): normal tempo, and the book set exactly on the destination with its stacks in place.
        private void EndArchiveFlip()
        {
            archiveFlipPlan = null; archiveFlipActive = false; archiveSyncBook = false; SetChronicleTempo(1f);
            if (chronicleHost != null && archive != null) chronicleHost.Rig.SetPoseImmediate(ChronicleBookPose.Open, archive.Spread);
        }

        // ---------------- drawing ----------------
        private void DrawChronicleArchive(float w, float h)
        {
            if (archive == null) { screen = ScreenMode.Menu; return; }
            try { DrawChronicleArchiveCore(w, h); }
            catch (Exception error) { Debug.LogError("[Chronicle] Drawing the archive failed, returning to the menu: " + error); CloseChronicleArchive(); }
        }

        private void DrawChronicleArchiveCore(float w, float h)
        {
            if (archiveFlipPlan != null && Event.current.type == EventType.MouseDown) { EndArchiveFlip(); Event.current.Use(); }   // a click skips the journey
            DrawChronicleAtmosphere(w, h);
            var dest = new Rect((w - 1600f * Mathf.Min(w / 1600f, h / 900f)) * .5f, (h - 900f * Mathf.Min(w / 1600f, h / 900f)) * .5f, 1600f * Mathf.Min(w / 1600f, h / 900f), 900f * Mathf.Min(w / 1600f, h / 900f));
            Rect left, right; var alpha = 1f; var show = true;
            if (chronicleHost != null)
            {
                if (Event.current.type == EventType.Repaint) GUI.DrawTexture(dest, chronicleHost.Texture, ScaleMode.StretchToFill, true);
                var frame = chronicleHost.Rig.Frame; show = frame.overlayVisible; alpha = frame.overlayAlpha;
                if (chronicleHost.TryGetPageViewportRect(ChroniclePageSide.Left, out var lv) && chronicleHost.TryGetPageViewportRect(ChroniclePageSide.Right, out var rv)) { left = ChronicleGuiRect(dest, lv); right = ChronicleGuiRect(dest, rv); }
                else { show = false; left = right = default; }
            }
            else
            {
                left = new Rect(w * .09f, h * .17f, w * .37f, h * .62f); right = new Rect(w * .54f, h * .17f, w * .37f, h * .62f);
                var paper = new Color(.93f, .85f, .68f);
                Fill(new Rect(left.x - 26, left.y - 26, left.width + 52, left.height + 52), paper); Fill(new Rect(right.x - 26, right.y - 26, right.width + 52, right.height + 52), paper);
                Outline(new Rect(left.x - 26, left.y - 26, left.width + 52, left.height + 52), ArchiveGold, 2); Outline(new Rect(right.x - 26, right.y - 26, right.width + 52, right.height + 52), ArchiveGold, 2);
            }
            if (show) DrawArchivePages(left, right, alpha);

            var caption = new GUIStyle(ReadableStyle(13, true)) { alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(.85f, .72f, .45f) } };
            if (!chronicleArchiveIcon) chronicleArchiveIcon = Resources.Load<Texture2D>("Chronicle/UI/ChronicleArchiveIcon");
            if (chronicleArchiveIcon) GUI.DrawTexture(new Rect(30, 10, 48, 48), chronicleArchiveIcon, ScaleMode.ScaleToFit, true);
            SetChronicleTextInk(caption,caption.normal.textColor);
            GUI.Label(new Rect(chronicleArchiveIcon ? 88 : 34, 22, 420, 24), "THE CHRONICLE OF BROKEN FATE", caption);
            var back = new Rect(34, h - 76, 148, 46); DrawButtonFrame(back, false, archiveClosing);
            var wasEnabled = GUI.enabled; GUI.enabled = wasEnabled && !archiveClosing;
            var backStyle=new GUIStyle(buttonStyle);SetChronicleTextInk(backStyle,backStyle.normal.textColor);
            if (GUI.Button(back, "BACK", backStyle)) ApplyArchiveResult(archive.Back());
            GUI.enabled = wasEnabled;
            DrawMenuNavigationHint(w, h, "← →  Turn page    ↑ ↓  Select    Enter  Open / Read    Esc  Back", "D-pad  Select    LB / RB  Turn page    A  Open    B  Back");
        }

        private void DrawArchivePages(Rect left, Rect right, float alpha)
        {
            var frame = chronicleHost != null ? chronicleHost.Rig.Frame : default;
            // During a multi-page journey the pages in between are blank paper; the destination appears as its last sheet lands.
            if (archiveFlipPlan != null && !(archiveFlipStep >= archiveFlipPlan.Turns && frame.flipRaw >= .5f)) return;
            var outgoing = chronicleHost != null && archiveFlipActive && frame.flipDirection != 0 && frame.flipRaw < .5f;
            var spread = outgoing ? archiveOutSpread : archive.Spread;
            var readId = outgoing ? archiveOutReadingId : archive.ReadingId; var readIndex = outgoing ? archiveOutReadingSpread : archive.ReadingSpread;
            var enabled = GUI.enabled; GUI.enabled = enabled && alpha > .95f && !outgoing && !archiveClosing;   // no clicks on a page that is mid-turn
            // Quiet the parchment beneath the ink while retaining the wrinkles,
            // border artwork and exactly the same content rectangles.
            var wash=new Color(1f,.94f,.80f,.18f*alpha);
            Fill(left,wash);Fill(right,wash);
            if (!string.IsNullOrEmpty(readId)) DrawArchiveReading(left, right, alpha, readId, readIndex);
            else if (spread == ChronicleArchiveController.ContentsSpread) DrawArchiveContents(left, right, alpha);
            else if (spread == ChronicleArchiveController.SecretSpread) DrawArchiveSecret(left, right, alpha);
            else DrawArchiveChapter(spread, left, right, alpha);
            GUI.enabled = enabled;
        }

        // ---- shared bits ----
        private GUIStyle ArchiveStyle(int size, bool italic, TextAnchor anchor)
        {
            var key = size * 64 + (italic ? 32 : 0) + (int)anchor;
            if (archiveStyles.TryGetValue(key, out var s)) return s;
            if (!chronicleHandwritingFont) chronicleHandwritingFont = Resources.Load<Font>("Chronicle/Fonts/ChronicleHandwriting");
            s = new GUIStyle(GUI.skin.label) { font = chronicleHandwritingFont ? chronicleHandwritingFont : bodyFont, fontSize = size, wordWrap = true, richText = false, alignment = anchor, fontStyle = FontStyle.Normal };
            archiveStyles[key] = s; return s;
        }

        private float ArchiveText(Rect page, float y, string text, int size, Color ink, float alpha, bool italic = false, TextAnchor anchor = TextAnchor.UpperLeft, float after = 8f)
        {
            var style = ArchiveStyle(Mathf.Max(8, size), italic, anchor); SetChronicleTextInk(style, new Color(ink.r, ink.g, ink.b, ink.a * alpha));
            var height = style.CalcHeight(new GUIContent(text), page.width);
            GUI.Label(new Rect(page.x, y, page.width, height + 2f), text, style);
            return y + height + after;
        }

        // An original sentence; once its correction is discovered it is struck through in red with the replacement beneath.
        private float DrawArchivePreface(Rect page, float y, IList<ChroniclePrefaceEntry> entries, float unit, float alpha)
        {
            var size = Mathf.Max(9, Mathf.RoundToInt(20f * unit));
            foreach (var e in entries)
            {
                var style = ArchiveStyle(size, false, TextAnchor.UpperLeft); var height = style.CalcHeight(new GUIContent(e.text), page.width);
                y = ArchiveText(page, y, e.text, size, e.struck ? new Color(ArchiveInk.r, ArchiveInk.g, ArchiveInk.b, .75f) : ArchiveInk, alpha, false, TextAnchor.UpperLeft, 4f * unit);
                if (e.struck)
                {
                    DrawChronicleStrike(new Rect(page.x, y - 4f * unit - height, page.width, height), size, e.text, 1f, alpha);
                    y = ArchiveText(page, y, e.corrected, size, ArchiveRed, alpha, false, TextAnchor.UpperLeft, 10f * unit);
                }
                else y += 6f * unit;
            }
            return y;
        }

        private bool ArchiveHit(Rect r, int focusIndex)
        {
            var over = r.Contains(PointerPosition);
            if (!controllerNavigation && over && archive.Focus != focusIndex && focusIndex >= 0) archive.SetFocusIndex(focusIndex);
            var focused = archive.Focus == focusIndex && (controllerNavigation || over);
            if (focused && GUI.enabled) DrawArchiveInkSelection(r, 1f);   // an ink underline, never a box
            return GUI.Button(r, "", GUIStyle.none);
        }

        private ChronicleMemoryReading ArchiveReadingFor(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (archiveReadings.TryGetValue(id, out var cached)) return cached;
            var reading = ChronicleArchive.Reading(id, profile.chronicle, ChronicleWritingMetrics()); archiveReadings[id] = reading; return reading;
        }

        // ---- journal ink: everything on these pages is ink on parchment, never filled cards or framed buttons ----
        private static Color ArchiveTint(Color c, float a) => new Color(c.r, c.g, c.b, c.a * a);

        // A thin antique-gold rule with a small inked diamond at its centre.
        private void DrawArchiveOrnament(Rect page, float y, float alpha, float widthFraction = .62f, bool diamond = true)
        {
            var u = page.width / 470f; var cx = page.center.x; var half = page.width * widthFraction * .5f; var ink = ArchiveTint(ArchiveGold, .7f * alpha);
            var d = diamond ? 3.5f * u : 0f; var gap = diamond ? d + 4f * u : 0f;
            ChronicleArtLine(new Vector2(cx - half, y), new Vector2(cx - gap, y), ink, 1f);
            ChronicleArtLine(new Vector2(cx + gap, y), new Vector2(cx + half, y), ink, 1f);
            if (!diamond) return;
            ChronicleArtLine(new Vector2(cx - d, y), new Vector2(cx, y - d), ink, 1f); ChronicleArtLine(new Vector2(cx, y - d), new Vector2(cx + d, y), ink, 1f);
            ChronicleArtLine(new Vector2(cx + d, y), new Vector2(cx, y + d), ink, 1f); ChronicleArtLine(new Vector2(cx, y + d), new Vector2(cx - d, y), ink, 1f);
        }

        // The selection: a slightly uneven pen underline and a small gold pointing mark in the margin.
        private void DrawArchiveInkSelection(Rect r, float alpha)
        {
            var ink = ArchiveTint(ArchiveInk, .5f * alpha); var y = r.yMax - 3f; var mid = r.x + r.width * .52f;
            ChronicleArtLine(new Vector2(r.x + 2f, y), new Vector2(mid, y + .7f), ink, 1.3f);
            ChronicleArtLine(new Vector2(mid, y + .7f), new Vector2(r.xMax - 4f, y - .3f), ink, 1.1f);
            var mark = ArchiveStyle(Mathf.Max(9, Mathf.RoundToInt(Mathf.Min(r.height, 40f) * .6f)), false, TextAnchor.MiddleRight); SetChronicleTextInk(mark, ArchiveTint(ArchiveGold, alpha));
            GUI.Label(new Rect(r.x - 20f, r.y, 17f, Mathf.Min(r.height, 40f)), "\u203a", mark);
        }

        // A faint dotted leader between a title and its marks, as in a printed table of contents.
        private void DrawArchiveLeader(float x0, float x1, float y, float unit, float alpha)
        {
            var ink = ArchiveTint(ArchiveDim, .35f * alpha); var step = 7f * unit;
            for (var x = x0 + step * .5f; x < x1 - step * .5f; x += step) ChronicleArtLine(new Vector2(x, y), new Vector2(x + 1.2f * unit, y), ink, 1.2f);
        }

        // Kept name: now an ink text link, underlined in gold, red when hovered.
        private void DrawArchiveSmallButton(Rect r, string label)
        {
            var hot = GUI.enabled && r.Contains(PointerPosition);
            var s = ArchiveStyle(Mathf.Max(8, Mathf.RoundToInt(r.height * .62f)), true, TextAnchor.MiddleLeft); SetChronicleTextInk(s, hot ? ArchiveRed : ArchiveInk);
            var width = Mathf.Min(r.width, s.CalcSize(new GUIContent(label)).x);
            GUI.Label(r, label, s);
            ChronicleArtLine(new Vector2(r.x, r.yMax - 3f), new Vector2(r.x + width, r.yMax - 3f), ArchiveTint(hot ? ArchiveRed : ArchiveGold, hot ? .9f : .5f), 1f);
        }

        private float ArchiveLinkWidth(string label, float height) => ArchiveStyle(Mathf.Max(8, Mathf.RoundToInt(height * .62f)), true, TextAnchor.MiddleLeft).CalcSize(new GUIContent(label)).x + 6f;

        private static string ArchiveTextSizeName(float scale) => scale < .9f ? "small" : scale < 1.07f ? "standard" : scale < 1.22f ? "large" : "largest";

        // ---- contents spread ----
        private void DrawArchiveContents(Rect left, Rect right, float alpha)
        {
            var progress = profile.chronicle; var v = ChronicleArchive.Contents(progress); var u = left.width / 470f; var y = left.y;
            y = ArchiveText(left, y, "THE CHRONICLE", Mathf.RoundToInt(15 * u), ArchiveDim, alpha, false, TextAnchor.UpperLeft, 2);
            y = ArchiveText(left, y, "OF BROKEN FATE", Mathf.RoundToInt(32 * u), ArchiveInk, alpha, false, TextAnchor.UpperLeft, 8 * u);
            y = ArchiveText(left, y, "A history that keeps correcting itself.", Mathf.RoundToInt(17 * u), ArchiveDim, alpha, true, TextAnchor.UpperLeft, 10 * u);
            y = ArchiveText(left, y, $"{v.memoriesUnlocked} of {ChronicleCatalog.MemoryCount} memories recovered  \u00b7  {v.memoriesViewed} watched  \u00b7  {v.correctionsFound} of {ChronicleCatalog.Corrections.Length} corrections" + (v.newCount > 0 ? $"  \u00b7  {v.newCount} new" : ""), Mathf.RoundToInt(14 * u), ArchiveGold, alpha, false, TextAnchor.UpperLeft, 10 * u);
            DrawArchiveOrnament(left, y + 2 * u, alpha); y += 16 * u;
            DrawArchivePreface(left, y, v.prologue, u, alpha);

            // ---- the reader's preferences, at the foot of the left page (the same place as before) ----
            var rowH = 25f * u; var continueH = 34f * u; var top = left.yMax - rowH * 6f - continueH - 26f * u;
            var calm = profile.reduceMotion || profile.reduceFlashing;
            ArchiveText(left, top, "READING" + (calm ? "   \u00b7   reduced motion and flashing follow your Settings" : ""), Mathf.RoundToInt(12 * u), ArchiveDim, alpha, false, TextAnchor.UpperLeft, 0);
            top += 20f * u;
            var mode = progress.openingMode == 0 ? "full at every launch" : progress.openingMode == 1 ? "short after the first" : "skipped";
            var rows = new[]
            {
                ("Instant text", progress.instantText ? "on" : "off"),
                ("Writing speed", progress.textSpeed.ToString("0.##") + "\u00d7"),
                ("Opening", mode),
                ("Narration volume", Mathf.RoundToInt(archive.NarrationVolume * 100f) + "%"),
                ("Text size", ArchiveTextSizeName(progress.journal.textScale)),
                ("Page turns", progress.journal.manualAdvance ? "when I turn them" : "on their own"),
            };
            for (var i = 0; i < rows.Length; i++)
            {
                var r = new Rect(left.x, top + i * rowH, left.width, rowH - 2f * u); var focus = ChronicleArchiveController.ContentsOptionStart + i;
                var size = Mathf.Max(8, Mathf.RoundToInt(15 * u));
                var label = ArchiveStyle(size, false, TextAnchor.MiddleLeft); SetChronicleTextInk(label, ArchiveTint(ArchiveDim, alpha));
                var value = ArchiveStyle(size, false, TextAnchor.MiddleRight); SetChronicleTextInk(value, ArchiveTint(ArchiveInk, alpha));
                var focused = archive.Focus == focus && (controllerNavigation || r.Contains(PointerPosition));
                var shown = focused ? "\u2039  " + rows[i].Item2 + "  \u203a" : rows[i].Item2;
                var lw = label.CalcSize(new GUIContent(rows[i].Item1)).x; var vw = value.CalcSize(new GUIContent(shown)).x;
                GUI.Label(new Rect(r.x, r.y, r.width * .6f, r.height), rows[i].Item1, label);
                DrawArchiveLeader(r.x + lw + 4f * u, r.xMax - vw - 4f * u, r.center.y + 3f * u, u, alpha);
                GUI.Label(new Rect(r.x + r.width * .3f, r.y, r.width * .7f, r.height), shown, value);
                if (ArchiveHit(r, focus)) ApplyArchiveResult(archive.Accept());
            }

            // Continue Reading: the saved Archive place (separate from where the automatic story stands).
            var cont = new Rect(left.x, top + rowH * 6f + 6f * u, left.width, continueH - 6f * u);
            DrawArchiveOrnament(left, cont.y - 2f * u, alpha * .8f, .9f, false);
            var cs = ArchiveStyle(Mathf.Max(9, Mathf.RoundToInt(17 * u)), true, TextAnchor.MiddleLeft);
            SetChronicleTextInk(cs, v.canContinue ? (GUI.enabled && cont.Contains(PointerPosition) ? ArchiveRed : ArchiveTint(ArchiveInk, alpha)) : ArchiveTint(ArchiveDim, alpha * .6f));
            GUI.Label(cont, v.canContinue ? "Continue reading  \u2014  " + v.continueLabel : "Continue reading  \u2014  nothing marked yet", cs);
            if (ArchiveHit(cont, ChronicleArchiveController.ContentsOptionStart + ChronicleArchiveController.OptContinue)) ApplyArchiveResult(archive.ContinueReading());

            // ---- right page: the table of contents, written into the journal ----
            u = right.width / 470f; y = right.y;
            y = ArchiveText(right, y, "CONTENTS", Mathf.RoundToInt(15 * u), ArchiveDim, alpha, false, TextAnchor.UpperCenter, 4 * u);
            DrawArchiveOrnament(right, y + 2f * u, alpha); y += 14f * u;
            var rh = Mathf.Min(40f * u, (right.yMax - y) / (10f + 3f * .55f + .35f)); var capH = rh * .55f;
            var indent = 24f * u; var romanW = 40f * u; var marksW = 96f * u;
            for (var i = 0; i < 10; i++)
            {
                var secret = i == 9; var row = secret ? null : v.chapters[i];
                if (!secret && i % 3 == 0)
                {
                    ArchiveText(new Rect(right.x + indent, y, right.width - indent, capH), y + capH * .12f, "ACT " + ChronicleRoman(row.act) + "  \u00b7  " + ChronicleCatalog.ActNames[row.act], Mathf.RoundToInt(11 * u), ArchiveTint(ArchiveDim, .85f), alpha, false, TextAnchor.UpperLeft, 0);
                    y += capH;
                }
                if (secret) y += capH * .35f;
                var r = new Rect(right.x + indent, y, right.width - indent, rh);
                var open = secret ? v.secret != ChronicleSecretState.Locked : row.visible;
                var size = Mathf.Max(9, Mathf.RoundToInt(18 * u));
                var roman = ArchiveStyle(size, false, TextAnchor.MiddleRight); SetChronicleTextInk(roman, ArchiveTint(ArchiveGold, alpha * (open ? 1f : .55f)));
                GUI.Label(new Rect(r.x, r.y, romanW, r.height), ChronicleRoman(secret ? 10 : row.number), roman);
                var name = secret ? (v.secret == ChronicleSecretState.Locked ? "???" : "The One Who Remembers") : row.title;
                var title = ArchiveStyle(size, false, TextAnchor.MiddleLeft); SetChronicleTextInk(title, open ? ArchiveTint(ArchiveInk, alpha) : ArchiveTint(ArchiveDim, alpha * .6f));
                var tx = r.x + romanW + 12f * u; var tw = Mathf.Min(r.xMax - marksW - tx, title.CalcSize(new GUIContent(name)).x);
                GUI.Label(new Rect(tx, r.y, r.xMax - marksW - tx, r.height), name, title);
                var after = tx + tw + 4f * u;
                if (!secret && row.updated && row.visible)
                {
                    // Newly written ink since this chapter was last opened: a small red pilcrow.
                    var mark = ArchiveStyle(Mathf.Max(8, size - 4), false, TextAnchor.MiddleLeft); SetChronicleTextInk(mark, ArchiveTint(ArchiveRed, alpha));
                    GUI.Label(new Rect(after, r.y - 2f * u, 16f * u, r.height), "\u00b6", mark); after += 14f * u;
                }
                DrawArchiveLeader(after, r.xMax - marksW, r.center.y + 5f * u, u, alpha * (open ? 1f : .5f));
                var marks = new Rect(r.xMax - marksW, r.y, marksW, r.height);
                var small = ArchiveStyle(Mathf.Max(8, Mathf.RoundToInt(13 * u)), true, TextAnchor.MiddleRight);
                if (secret)
                {
                    SetChronicleTextInk(small, v.secret == ChronicleSecretState.Unlocked ? ArchiveTint(ArchiveRed, alpha) : ArchiveTint(ArchiveDim, alpha));
                    GUI.Label(marks, v.secret == ChronicleSecretState.Locked ? "FATE DEBT X \u00d73" : v.secret == ChronicleSecretState.Unlocked ? "new" : "read", small);
                }
                else
                {
                    // One mark per hero's memory: dark = watched, faint = recovered, a dot = not yet recovered.
                    var cx = marks.xMax; var dot = ArchiveStyle(Mathf.Max(8, size), false, TextAnchor.MiddleRight);
                    for (var slot = 2; slot >= 0; slot--)
                    {
                        var id = ChronicleCatalog.MemoryFor(row.number, (ChronicleHero)slot).id; var st = ChronicleArchive.StateOf(progress, id);
                        SetChronicleTextInk(dot, st == ChronicleEntryState.Viewed ? ArchiveTint(ArchiveInk, alpha) : st == ChronicleEntryState.Unlocked ? ArchiveTint(ArchiveGold, alpha * .75f) : ArchiveTint(ArchiveDim, alpha * .35f));
                        GUI.Label(new Rect(cx - 14f * u, r.y, 14f * u, r.height), st == ChronicleEntryState.Locked ? "\u00b7" : "\u2022", dot); cx -= 13f * u;
                    }
                    if (row.unlocked > row.viewed) { SetChronicleTextInk(small, ArchiveTint(ArchiveRed, alpha)); GUI.Label(new Rect(marks.x - 40f * u, r.y, 40f * u + (cx - marks.x), r.height), "new ", small); }
                }
                if (ArchiveHit(r, i)) ApplyArchiveResult(secret ? archive.OpenSecret() : archive.OpenChapter(row.number));
                y += rh;
            }
        }

        // ---- a chapter ----
        private void DrawArchiveChapter(int number, Rect left, Rect right, float alpha)
        {
            var progress = profile.chronicle; var v = ChronicleArchive.Chapter(number, progress); var u = left.width / 470f; var y = left.y;
            y = ArchiveText(left, y, "CHAPTER " + ChronicleRoman(number), Mathf.RoundToInt(15 * u), ArchiveGold, alpha, false, TextAnchor.UpperLeft, 2 * u);
            y = ArchiveText(left, y, v.row.title, Mathf.RoundToInt(30 * u), ArchiveInk, alpha, false, TextAnchor.UpperLeft, 4 * u);
            y = ArchiveText(left, y, v.row.actName, Mathf.RoundToInt(13 * u), ArchiveDim, alpha, false, TextAnchor.UpperLeft, 10 * u);
            DrawArchiveOrnament(left, y + 2f * u, alpha); y += 16 * u;
            if (v.row.visible) y = DrawArchivePreface(left, y, v.preface, u, alpha);
            else ArchiveText(left, y, "Nothing is written here yet.", Mathf.RoundToInt(19 * u), ArchiveDim, alpha, true, TextAnchor.UpperLeft, 6 * u);
            ArchiveText(new Rect(left.x, left.yMax - 26 * u, left.width, 26 * u), left.yMax - 26 * u, v.row.viewed + " of 3 watched   \u00b7   page " + number + " of 9", Mathf.RoundToInt(13 * u), ArchiveDim, alpha, true, TextAnchor.UpperLeft, 0);

            // Right page: each hero's memory as a written entry, divided by thin gold rules.
            u = right.width / 470f; var count = v.memories.Count + v.revelations.Count; var entryH = (right.height - 8 * u) / Mathf.Max(3, count);
            for (var i = 0; i < count; i++)
            {
                var revelation = i >= v.memories.Count; var m = revelation ? v.revelations[i - v.memories.Count] : v.memories[i];
                var entry = new Rect(right.x + 22 * u, right.y + i * entryH, right.width - 22 * u, entryH - 10 * u);
                var tint = ChronicleColor.ForHero(m.hero); var heroInk = new Color(tint.r * .55f, tint.g * .55f, tint.b * .55f, alpha);
                var locked = m.state == ChronicleEntryState.Locked;
                var cx = entry.x; var cw = entry.width; var cy = entry.y + 4 * u;
                ChronicleArtLine(new Vector2(cx - 10 * u, cy + 3 * u), new Vector2(cx - 10 * u, cy + 15 * u), locked ? ArchiveTint(heroInk, .35f) : heroInk, 2f);   // a hero-coloured ink stroke
                var hero = ArchiveStyle(Mathf.Max(8, Mathf.RoundToInt(13 * u)), false, TextAnchor.UpperLeft); SetChronicleTextInk(hero, ArchiveTint(ArchiveDim, alpha));
                GUI.Label(new Rect(cx, cy, cw * .6f, 18 * u), ChronicleCatalog.HeroName(m.hero).ToUpperInvariant() + (revelation ? "  \u00b7  A LATER RECOLLECTION" : ""), hero);
                var status = locked ? "not yet recovered" : m.isNew ? "new" : "watched";
                var chip = ArchiveStyle(Mathf.Max(8, Mathf.RoundToInt(13 * u)), true, TextAnchor.UpperRight); SetChronicleTextInk(chip, m.isNew ? ArchiveTint(ArchiveRed, alpha) : ArchiveTint(ArchiveDim, alpha));
                GUI.Label(new Rect(cx + cw * .5f, cy, cw * .5f, 18 * u), status, chip);
                cy += 20 * u;
                var titleText = revelation ? "" : locked ? "A memory not yet recovered" : m.title;
                var titleStyle2 = ArchiveStyle(Mathf.Max(9, Mathf.RoundToInt(21 * u)), false, TextAnchor.UpperLeft); SetChronicleTextInk(titleStyle2, locked ? ArchiveTint(ArchiveDim, alpha * .8f) : ArchiveTint(ArchiveInk, alpha));
                if (titleText.Length > 0) GUI.Label(new Rect(cx, cy, cw, 30 * u), titleText, titleStyle2);
                if (locked) ArchiveText(new Rect(cx, cy, cw, 40 * u), cy + 28 * u, m.hint, Mathf.RoundToInt(15 * u), ArchiveDim, alpha, true, TextAnchor.UpperLeft, 0);
                else
                {
                    // READ opens the written pages; WATCH AGAIN replays the scene. Both are plain ink links.
                    var lh = 26 * u; var ry = entry.yMax - lh - 2 * u;
                    var read = new Rect(cx, ry, ArchiveLinkWidth("Read", lh), lh); var replay = new Rect(read.xMax + 22 * u, ry, ArchiveLinkWidth("Watch again", lh), lh);
                    DrawArchiveSmallButton(read, "Read"); DrawArchiveSmallButton(replay, "Watch again");
                    if (GUI.Button(read, "", GUIStyle.none)) { archive.SetFocusIndex(i); ApplyArchiveResult(archive.OpenReading(m.id)); }
                    if (GUI.Button(replay, "", GUIStyle.none)) { archive.SetFocusIndex(i); ApplyArchiveResult(archive.Replay(m.id)); }
                }
                if (i < count - 1) DrawArchiveOrnament(new Rect(right.x + 22 * u, 0, right.width - 22 * u, 0), entry.yMax + 4 * u, alpha * .6f, .5f, false);
                ArchiveHit(entry, i);   // the whole entry is the focus target; its links do the work with a pointer
            }
        }

        // ---- reading a memory, Chapter X or a revelation ----
        private void DrawArchiveReading(Rect left, Rect right, float alpha, string id, int index)
        {
            var reading = ArchiveReadingFor(id); if (reading == null) return;
            archiveInkState.narrator = profile.chronicle.NarratorState;
            var page = Mathf.Clamp(index, 0, reading.spreads.Count - 1); var spread = reading.spreads[page];
            DrawChronicleText(left, spread.left, ChroniclePageSide.Left, archiveInkState, alpha);
            DrawChronicleText(right, spread.right, ChroniclePageSide.Right, archiveInkState, alpha);
            var u = left.width / 470f; var lh = 26 * u; var footY = left.yMax - lh;
            ArchiveText(new Rect(left.x, footY, left.width, lh), footY + 2 * u, "page " + (page + 1) + " of " + reading.spreads.Count, Mathf.RoundToInt(13 * u), ArchiveDim, alpha, true, TextAnchor.UpperCenter, 0);
            // Pointer page turns, written at the outer corners like a reader's marginal arrows.
            if (page > 0)
            {
                var prev = new Rect(left.x, footY, ArchiveLinkWidth("\u2039 previous", lh), lh); DrawArchiveSmallButton(prev, "\u2039 previous");
                if (GUI.Button(prev, "", GUIStyle.none)) ApplyArchiveResult(archive.Page(-1));
            }
            u = right.width / 470f; lh = 26 * u; footY = right.yMax - lh;
            if (page < reading.spreads.Count - 1)
            {
                var next = new Rect(right.x, footY, ArchiveLinkWidth("next \u203a", lh), lh); DrawArchiveSmallButton(next, "next \u203a");
                if (GUI.Button(next, "", GUIStyle.none)) ApplyArchiveResult(archive.Page(1));
            }
            var watchLabel = id == ChronicleCatalog.SecretSceneId ? "Watch Chapter X again" : "Watch again";
            var button = new Rect(right.xMax - ArchiveLinkWidth(watchLabel, lh), footY, ArchiveLinkWidth(watchLabel, lh), lh);
            DrawArchiveSmallButton(button, watchLabel);
            if (ArchiveHit(button, 0)) ApplyArchiveResult(archive.Accept());
        }

        // ---- Chapter X ----
        private void DrawArchiveSecret(Rect left, Rect right, float alpha)
        {
            var progress = profile.chronicle; var state = ChronicleArchive.SecretState(progress); var u = left.width / 470f; var y = left.y;
            y = ArchiveText(left, y, "CHAPTER X", Mathf.RoundToInt(15 * u), ArchiveGold, alpha, false, TextAnchor.UpperLeft, 2 * u);
            y = ArchiveText(left, y, state == ChronicleSecretState.Locked ? "???" : "The One Who Remembers", Mathf.RoundToInt(30 * u), ArchiveInk, alpha, false, TextAnchor.UpperLeft, 12 * u);
            var body = state == ChronicleSecretState.Locked ? "A last chapter waits at the end of the book. It cannot be opened until all three heroes have recovered their True History by defeating Fate Debt X."
                : state == ChronicleSecretState.Unlocked ? "Every memory is recovered. The Chronicle has something to say." : "The Chronicle has said it. It can be read again.";
            ArchiveText(left, y, body, Mathf.RoundToInt(19 * u), ArchiveInk, alpha, true, TextAnchor.UpperLeft, 8 * u);

            u = right.width / 470f; y = right.y;
            y = ArchiveText(right, y, "MEMORIES RECOVERED  " + progress.TotalUnlocked + " / " + ChronicleCatalog.MemoryCount, Mathf.RoundToInt(16 * u), ArchiveGold, alpha, false, TextAnchor.UpperLeft, 12 * u);
            foreach (ChronicleHero hero in Enum.GetValues(typeof(ChronicleHero)))
            {
                var have = ChronicleCatalog.Memories.Count(m => m.hero == hero && progress.IsUnlocked(m.id));
                y = ArchiveText(right, y, ChronicleCatalog.HeroName(hero).ToUpperInvariant() + "   " + have + " / 9", Mathf.RoundToInt(17 * u), ArchiveInk, alpha, false, TextAnchor.UpperLeft, 4 * u);
            }
            if (state != ChronicleSecretState.Locked)
            {
                var lh = 30 * u; var by = right.yMax - lh - 8 * u;
                if (state == ChronicleSecretState.Unlocked)
                {
                    var open = new Rect(right.x, by, ArchiveLinkWidth("Open the final chapter", lh), lh); DrawArchiveSmallButton(open, "Open the final chapter");
                    if (ArchiveHit(open, 0)) ApplyArchiveResult(archive.BeginSecret());
                }
                else
                {
                    var read = new Rect(right.x, by, ArchiveLinkWidth("Read", lh), lh); var watch = new Rect(read.xMax + 26 * u, by, ArchiveLinkWidth("Watch again", lh), lh);
                    DrawArchiveSmallButton(read, "Read"); DrawArchiveSmallButton(watch, "Watch again");
                    if (ArchiveHit(read, 0)) ApplyArchiveResult(archive.OpenReading(ChronicleCatalog.SecretSceneId));
                    if (ArchiveHit(watch, 1)) ApplyArchiveResult(archive.BeginSecret());
                }
            }
        }

        // ---------------- unlock notifications ----------------
        private bool ChronicleToastsAllowed => screen is ScreenMode.Menu or ScreenMode.RunResult or ScreenMode.Records or ScreenMode.Map && !chronicleActive && !bootIntroActive && !runStartActive;

        // Called from the always-on-top overlay pass. Notices wait (saved in the profile) until a calm screen; combat is never interrupted.
        private void DrawChronicleToasts(float w, float h)
        {
            if (profile == null) return;
            var now = Time.unscaledTime;
            if (ChronicleToastsAllowed && profile.chronicle.pendingNotices.Count > 0)
            {
                var notices = profile.chronicle.TakePendingNotices();
                var memories = notices.Where(n => n != ChronicleCatalog.SecretChapterId).ToList();
                if (notices.Contains(ChronicleCatalog.SecretChapterId)) chronicleToasts.Add(("THE CHRONICLE IS COMPLETE", "A final chapter awaits in THE CHRONICLE.", true, now));
                if (memories.Count == 1) { var m = ChronicleCatalog.FindMemory(memories[0]); if (m != null) chronicleToasts.Add(("A MEMORY STIRS", ChronicleCatalog.HeroName(m.hero).ToUpperInvariant() + "  ·  CHAPTER " + ChronicleRoman(m.chapter), false, now)); }
                else if (memories.Count > 1) chronicleToasts.Add(("THE CHRONICLE REMEMBERS", memories.Count + " MEMORIES RECOVERED", false, now));
                Sfx(SoundCue.Resonance, intensity: .6f); ProfileService.Save(profile);
            }
            if (chronicleToasts.Count == 0) return;
            chronicleToasts.RemoveAll(t => now > t.at + 5f);
            var y = h - 150f;
            foreach (var t in chronicleToasts)
            {
                var age = now - t.at; var fade = Mathf.Clamp01((5f - age) / .6f) * Mathf.Clamp01(age / .25f);
                var r = new Rect(w * .5f - 240, y, 480, t.big ? 84 : 66); var old = GUI.color; GUI.color = new Color(1, 1, 1, fade);
                Fill(r, new Color(.02f, .03f, .025f, .96f)); Outline(r, new Color(.4f, .95f, .7f), 2); Fill(new Rect(r.x, r.y, 4, r.height), new Color(.4f, .95f, .7f));
                GUI.Label(new Rect(r.x + 18, r.y + 8, r.width - 30, 20), t.title, new GUIStyle(footerStyle) { font = labelFont ? labelFont : bodyFont, fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(.45f, 1f, .75f, fade) } });
                GUI.Label(new Rect(r.x + 18, r.y + 28, r.width - 30, r.height - 34), t.sub, new GUIStyle(titleStyle) { font = headingFont ? headingFont : labelFont, fontSize = t.big ? 17 : 21, alignment = TextAnchor.MiddleLeft, wordWrap = true, normal = { textColor = new Color(.95f, .97f, .9f, fade) } });
                GUI.color = old; y -= r.height + 10;
            }
        }

        // ---------------- main-menu hub ----------------
        private string ChronicleHubFooter()
        {
            var c = profile.chronicle; c.Ensure();
            var fresh = ChronicleCatalog.Memories.Count(m => c.IsUnlocked(m.id) && !c.IsViewed(m.id));
            return c.TotalUnlocked + " / " + ChronicleCatalog.MemoryCount + " MEMORIES" + (fresh > 0 ? "  ·  " + fresh + " NEW" : c.secretUnlocked && !c.secretCompleted ? "  ·  A FINAL CHAPTER" : "");
        }

        // Use the supplied book artwork in the Archive's Chronicle banner.
        private void HubArtChronicle(Rect r)
        {
            if (!chronicleArchiveIcon) chronicleArchiveIcon = Resources.Load<Texture2D>("Chronicle/UI/ChronicleArchiveIcon");
            if (chronicleArchiveIcon)
            {
                var size = Mathf.Min(r.width, r.height) * .96f;
                ShardSoft(r.center, size * 1.3f, new Color(.72f, .44f, .12f, .14f));
                GUI.DrawTexture(new Rect(r.center.x - size * .5f, r.center.y - size * .5f, size, size), chronicleArchiveIcon, ScaleMode.ScaleToFit, true);
                return;
            }
            var c = new Vector2(r.center.x, r.center.y + r.height * .04f); var arm = Mathf.Min(r.width, r.height) * .36f;
            var gold = new Color(1f, .8f, .4f, .95f); var emerald = new Color(.4f, .95f, .7f);
            ShardSoft(c, arm * 5.2f, new Color(.3f, .9f, .6f, .16f));
            // Four interwoven strands: across, down and the two diagonals, as stepped dots.
            for (var i = -(int)arm; i <= (int)arm; i += 2)
            {
                Fill(new Rect(c.x + i - 1, c.y - 1, 2, 2), gold); Fill(new Rect(c.x - 1, c.y + i - 1, 2, 2), gold);
                Fill(new Rect(c.x + i * .72f - 1, c.y + i * .72f - 1, 2, 2), gold); Fill(new Rect(c.x + i * .72f - 1, c.y - i * .72f - 1, 2, 2), gold);
            }
            HubDiamond(c, arm * .9f);
            Fill(new Rect(c.x - 4, c.y - 4, 8, 8), emerald);
        }
    }
}
