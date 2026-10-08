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
        private bool archiveFlipActive, archiveSyncBook;
        private int archiveOutSpread, archiveOutReadingSpread;
        private string archiveOutReadingId = "";
        private readonly Dictionary<string, ChronicleMemoryReading> archiveReadings = new();
        private readonly Dictionary<int, GUIStyle> archiveStyles = new();
        private readonly ChronicleSceneState archiveInkState = new();
        private readonly List<(string title, string sub, bool big, float at)> chronicleToasts = new();

        private static readonly Color ArchiveInk = new(.16f, .10f, .06f), ArchiveDim = new(.42f, .33f, .22f), ArchiveRed = new(.78f, .08f, .08f), ArchiveGold = new(.62f, .42f, .10f);
        private static string ChronicleRoman(int n) => new[] { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" }[Mathf.Clamp(n, 0, 10)];

        // ---------------- entering and leaving ----------------
        private void OpenChronicleArchive()
        {
            if (profile == null) return;
            var progress = profile.chronicle; progress.Ensure();
            archive = new ChronicleArchiveController(progress);
            archiveReadings.Clear(); archiveFlipActive = archiveSyncBook = false;
            chronicleHost = chronicleHost ?? ChronicleBookStageHost.Create(1600, 900);
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

        private void CloseChronicleArchive()
        {
            if (chronicleHost != null && !chronicleActive) chronicleHost.SetActive(false);
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
            if (archiveFlipActive && frame.flipDirection == 0 && !frame.busy) archiveFlipActive = false;
            if (archiveSyncBook && !frame.busy) { archiveSyncBook = false; chronicleHost.Rig.SetPoseImmediate(ChronicleBookPose.Open, archive.Spread); }
        }

        // ---------------- input ----------------
        private bool HandleChronicleArchiveNavigation(MenuNavigation input)
        {
            if (archive == null) { screen = ScreenMode.Menu; return true; }
            if (!input.Any) return true;
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
                        var forward = r.toSpread > r.fromSpread; var action = forward ? ChronicleBookAction.TurnPageForward : ChronicleBookAction.TurnPageBack;
                        chronicleHost.Rig.Perform(action, profile.chronicle.NarratorState); ChronicleBookSound(action);
                        archiveFlipActive = true; archiveSyncBook = true;
                    }
                    break;
                case ChronicleArchiveAction.OpenedReading: Sfx(SoundCue.UiConfirm); break;
                case ChronicleArchiveAction.ClosedReading: Sfx(SoundCue.UiBack); break;
                case ChronicleArchiveAction.ChangedOption: Sfx(SoundCue.UiHover); ProfileService.Save(profile); break;
                case ChronicleArchiveAction.Replay: PlayArchiveScene(r.id); break;
                case ChronicleArchiveAction.BeginSecret: PlayArchiveScene(ChronicleCatalog.SecretSceneId); break;
                case ChronicleArchiveAction.Exit: Sfx(SoundCue.UiBack); CloseChronicleArchive(); break;
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

        // ---------------- drawing ----------------
        private void DrawChronicleArchive(float w, float h)
        {
            if (archive == null) { screen = ScreenMode.Menu; return; }
            try { DrawChronicleArchiveCore(w, h); }
            catch (Exception error) { Debug.LogError("[Chronicle] Drawing the archive failed, returning to the menu: " + error); CloseChronicleArchive(); }
        }

        private void DrawChronicleArchiveCore(float w, float h)
        {
            Fill(new Rect(0, 0, w, h), new Color(.02f, .015f, .02f, 1f));
            var dest = new Rect((w - 1600f * Mathf.Min(w / 1600f, h / 900f)) * .5f, (h - 900f * Mathf.Min(w / 1600f, h / 900f)) * .5f, 1600f * Mathf.Min(w / 1600f, h / 900f), 900f * Mathf.Min(w / 1600f, h / 900f));
            Rect left, right; var alpha = 1f; var show = true;
            if (chronicleHost != null)
            {
                if (Event.current.type == EventType.Repaint) GUI.DrawTexture(dest, chronicleHost.Texture, ScaleMode.StretchToFill, false);
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
            GUI.Label(new Rect(34, 22, 420, 24), "THE CHRONICLE OF BROKEN FATE", caption);
            var back = new Rect(34, h - 76, 148, 46); DrawButtonFrame(back, back.Contains(PointerPosition), false);
            if (GUI.Button(back, "BACK", buttonStyle)) ApplyArchiveResult(archive.Back());
            DrawMenuNavigationHint(w, h, "← →  Turn page    ↑ ↓  Select    Enter  Open / Read    Esc  Back", "D-pad  Select    LB / RB  Turn page    A  Open    B  Back");
        }

        private void DrawArchivePages(Rect left, Rect right, float alpha)
        {
            var frame = chronicleHost != null ? chronicleHost.Rig.Frame : default;
            var outgoing = chronicleHost != null && archiveFlipActive && frame.flipDirection != 0 && frame.flipRaw < .5f;
            var spread = outgoing ? archiveOutSpread : archive.Spread;
            var readId = outgoing ? archiveOutReadingId : archive.ReadingId; var readIndex = outgoing ? archiveOutReadingSpread : archive.ReadingSpread;
            var enabled = GUI.enabled; GUI.enabled = enabled && alpha > .95f && !outgoing;   // no clicks on a page that is mid-turn
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
            s = new GUIStyle(GUI.skin.label) { font = headingFont ? headingFont : bodyFont, fontSize = size, wordWrap = true, richText = false, alignment = anchor, fontStyle = italic ? FontStyle.Italic : FontStyle.Normal };
            archiveStyles[key] = s; return s;
        }

        private float ArchiveText(Rect page, float y, string text, int size, Color ink, float alpha, bool italic = false, TextAnchor anchor = TextAnchor.UpperLeft, float after = 8f)
        {
            var style = ArchiveStyle(Mathf.Max(8, size), italic, anchor); style.normal.textColor = new Color(ink.r, ink.g, ink.b, ink.a * alpha);
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
            if (focused) Outline(r, new Color(.95f, .72f, .25f, .95f), 2);
            return GUI.Button(r, "", GUIStyle.none);
        }

        private ChronicleMemoryReading ArchiveReadingFor(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (archiveReadings.TryGetValue(id, out var cached)) return cached;
            var reading = ChronicleArchive.Reading(id, profile.chronicle); archiveReadings[id] = reading; return reading;
        }

        // ---- contents spread ----
        private void DrawArchiveContents(Rect left, Rect right, float alpha)
        {
            var progress = profile.chronicle; var v = ChronicleArchive.Contents(progress); var u = left.width / 470f; var y = left.y;
            y = ArchiveText(left, y, "THE CHRONICLE", Mathf.RoundToInt(15 * u), ArchiveDim, alpha, false, TextAnchor.UpperLeft, 2);
            y = ArchiveText(left, y, "OF BROKEN FATE", Mathf.RoundToInt(32 * u), ArchiveInk, alpha, false, TextAnchor.UpperLeft, 8 * u);
            y = ArchiveText(left, y, "A history that keeps correcting itself.", Mathf.RoundToInt(17 * u), ArchiveDim, alpha, true, TextAnchor.UpperLeft, 14 * u);
            y = ArchiveText(left, y, $"MEMORIES  {v.memoriesUnlocked} / {ChronicleCatalog.MemoryCount}     VIEWED  {v.memoriesViewed}", Mathf.RoundToInt(15 * u), ArchiveGold, alpha, false, TextAnchor.UpperLeft, 2 * u);
            y = ArchiveText(left, y, $"CORRECTIONS FOUND  {v.correctionsFound} / {ChronicleCatalog.Corrections.Length}" + (v.newCount > 0 ? $"     {v.newCount} NEW" : ""), Mathf.RoundToInt(15 * u), ArchiveGold, alpha, false, TextAnchor.UpperLeft, 12 * u);
            Fill(new Rect(left.x, y, left.width, 1.5f), new Color(ArchiveGold.r, ArchiveGold.g, ArchiveGold.b, .5f * alpha)); y += 14 * u;
            DrawArchivePreface(left, y, v.prologue, u, alpha);

            // The three options live at the foot of the left page: accessibility and the opening cinematic.
            var rowH = 34f * u; var top = left.yMax - rowH * 3f - 4f * u;
            var mode = progress.openingMode == 0 ? "FULL AT EVERY LAUNCH" : progress.openingMode == 1 ? "SHORT AFTER THE FIRST" : "SKIP IT";
            var rows = new[] { ("INSTANT TEXT", progress.instantText ? "ON" : "OFF"), ("WRITING SPEED", progress.textSpeed.ToString("0.##") + "x"), ("OPENING", mode) };
            for (var i = 0; i < rows.Length; i++)
            {
                var r = new Rect(left.x, top + i * rowH, left.width, rowH - 4f * u);
                Fill(r, new Color(.2f, .12f, .05f, .08f * alpha));
                var label = ArchiveStyle(Mathf.Max(8, Mathf.RoundToInt(14 * u)), false, TextAnchor.MiddleLeft); label.normal.textColor = new Color(ArchiveDim.r, ArchiveDim.g, ArchiveDim.b, alpha);
                var value = ArchiveStyle(Mathf.Max(8, Mathf.RoundToInt(14 * u)), false, TextAnchor.MiddleRight); value.normal.textColor = new Color(ArchiveInk.r, ArchiveInk.g, ArchiveInk.b, alpha);
                GUI.Label(new Rect(r.x + 8 * u, r.y, r.width * .5f, r.height), rows[i].Item1, label); GUI.Label(new Rect(r.x + r.width * .4f, r.y, r.width * .6f - 8 * u, r.height), rows[i].Item2, value);
                if (ArchiveHit(r, ChronicleArchiveController.ContentsOptionStart + i)) ApplyArchiveResult(archive.Accept());
            }

            // Right page: the chapters, then the secret one.
            u = right.width / 470f; y = right.y;
            y = ArchiveText(right, y, "CHAPTERS", Mathf.RoundToInt(14 * u), ArchiveDim, alpha, false, TextAnchor.UpperLeft, 6 * u);
            var rh = Mathf.Min(46f * u, (right.yMax - y) / 10f);
            for (var i = 0; i < 10; i++)
            {
                var r = new Rect(right.x, y + i * rh, right.width, rh - 4f * u); var secret = i == 9;
                var row = secret ? null : v.chapters[i]; var open = secret ? v.secret != ChronicleSecretState.Locked : row.visible;
                Fill(r, secret && v.secret == ChronicleSecretState.Unlocked ? new Color(.3f, .8f, .55f, .16f * alpha) : new Color(.2f, .12f, .05f, (open ? .12f : .05f) * alpha));
                var size = Mathf.Max(9, Mathf.RoundToInt(17 * u));
                var roman = ArchiveStyle(size, false, TextAnchor.MiddleLeft); roman.normal.textColor = new Color(ArchiveGold.r, ArchiveGold.g, ArchiveGold.b, alpha);
                var title = ArchiveStyle(size, false, TextAnchor.MiddleLeft); title.normal.textColor = open ? new Color(ArchiveInk.r, ArchiveInk.g, ArchiveInk.b, alpha) : new Color(ArchiveDim.r, ArchiveDim.g, ArchiveDim.b, alpha * .8f);
                var count = ArchiveStyle(Mathf.Max(8, size - 2), false, TextAnchor.MiddleRight); count.normal.textColor = new Color(ArchiveDim.r, ArchiveDim.g, ArchiveDim.b, alpha);
                GUI.Label(new Rect(r.x + 8 * u, r.y, 46 * u, r.height), ChronicleRoman(secret ? 10 : row.number), roman);
                var name = secret ? (v.secret == ChronicleSecretState.Locked ? "???" : "The One Who Remembers") : row.title;
                GUI.Label(new Rect(r.x + 58 * u, r.y, r.width - 150 * u, r.height), name, title);
                var tag = secret ? (v.secret == ChronicleSecretState.Locked ? "27 REQUIRED" : v.secret == ChronicleSecretState.Unlocked ? "NEW" : "VIEWED") : (row.unlocked > row.viewed ? "NEW  " : "") + row.viewed + " / 3";
                GUI.Label(new Rect(r.xMax - 128 * u, r.y, 120 * u, r.height), tag, count);
                if (ArchiveHit(r, i)) ApplyArchiveResult(secret ? archive.OpenSecret() : archive.OpenChapter(row.number));
            }
        }

        // ---- a chapter ----
        private void DrawArchiveChapter(int number, Rect left, Rect right, float alpha)
        {
            var progress = profile.chronicle; var v = ChronicleArchive.Chapter(number, progress); var u = left.width / 470f; var y = left.y;
            y = ArchiveText(left, y, "CHAPTER " + ChronicleRoman(number), Mathf.RoundToInt(15 * u), ArchiveGold, alpha, false, TextAnchor.UpperLeft, 2 * u);
            y = ArchiveText(left, y, v.row.title, Mathf.RoundToInt(30 * u), ArchiveInk, alpha, false, TextAnchor.UpperLeft, 4 * u);
            y = ArchiveText(left, y, v.row.actName, Mathf.RoundToInt(13 * u), ArchiveDim, alpha, false, TextAnchor.UpperLeft, 12 * u);
            Fill(new Rect(left.x, y, left.width, 1.5f), new Color(ArchiveGold.r, ArchiveGold.g, ArchiveGold.b, .5f * alpha)); y += 14 * u;
            if (v.row.visible) y = DrawArchivePreface(left, y, v.preface, u, alpha);
            else ArchiveText(left, y, "Nothing is written here yet.", Mathf.RoundToInt(19 * u), ArchiveDim, alpha, true, TextAnchor.UpperLeft, 6 * u);
            ArchiveText(new Rect(left.x, left.yMax - 26 * u, left.width, 26 * u), left.yMax - 26 * u, v.row.viewed + " OF 3 WATCHED   ·   PAGE " + number + " OF 9", Mathf.RoundToInt(13 * u), ArchiveDim, alpha, false, TextAnchor.UpperLeft, 0);

            u = right.width / 470f; var cardH = (right.height - 8 * u) / 3f;
            for (var i = 0; i < v.memories.Count; i++)
            {
                var m = v.memories[i]; var card = new Rect(right.x, right.y + i * cardH, right.width, cardH - 10 * u);
                var tint = ChronicleColor.ForHero(m.hero); var heroColor = new Color(tint.r, tint.g, tint.b, alpha);
                var locked = m.state == ChronicleEntryState.Locked;
                Fill(card, new Color(.2f, .12f, .05f, (locked ? .05f : .12f) * alpha)); Fill(new Rect(card.x, card.y, 5 * u, card.height), new Color(heroColor.r, heroColor.g, heroColor.b, locked ? .35f * alpha : alpha));
                var cx = card.x + 16 * u; var cw = card.width - 24 * u; var cy = card.y + 6 * u;
                var hero = ArchiveStyle(Mathf.Max(8, Mathf.RoundToInt(13 * u)), false, TextAnchor.UpperLeft); hero.normal.textColor = new Color(ArchiveDim.r, ArchiveDim.g, ArchiveDim.b, alpha);
                GUI.Label(new Rect(cx, cy, cw * .6f, 18 * u), ChronicleCatalog.HeroName(m.hero).ToUpperInvariant(), hero);
                var status = locked ? "LOCKED" : m.isNew ? "NEW" : "WATCHED";
                var chip = ArchiveStyle(Mathf.Max(8, Mathf.RoundToInt(12 * u)), false, TextAnchor.UpperRight); chip.normal.textColor = m.isNew ? new Color(.1f, .5f, .3f, alpha) : new Color(ArchiveDim.r, ArchiveDim.g, ArchiveDim.b, alpha);
                GUI.Label(new Rect(cx + cw * .5f, cy, cw * .5f, 18 * u), status, chip);
                cy += 20 * u;
                var titleStyle2 = ArchiveStyle(Mathf.Max(9, Mathf.RoundToInt(20 * u)), false, TextAnchor.UpperLeft); titleStyle2.normal.textColor = locked ? new Color(ArchiveDim.r, ArchiveDim.g, ArchiveDim.b, alpha * .8f) : new Color(ArchiveInk.r, ArchiveInk.g, ArchiveInk.b, alpha);
                GUI.Label(new Rect(cx, cy, cw, 28 * u), locked ? "A memory not yet recovered" : m.title, titleStyle2);
                if (locked) ArchiveText(new Rect(cx, cy, cw, 40 * u), cy + 28 * u, m.hint, Mathf.RoundToInt(15 * u), ArchiveDim, alpha, true, TextAnchor.UpperLeft, 0);
                else
                {
                    var read = new Rect(cx, card.yMax - 34 * u, 110 * u, 28 * u); var replay = new Rect(cx + 120 * u, card.yMax - 34 * u, 150 * u, 28 * u);
                    DrawArchiveSmallButton(read, "READ"); DrawArchiveSmallButton(replay, "WATCH AGAIN");
                    if (GUI.Button(read, "", GUIStyle.none)) { archive.SetFocusIndex(i); ApplyArchiveResult(archive.OpenReading(m.id)); }
                    if (GUI.Button(replay, "", GUIStyle.none)) { archive.SetFocusIndex(i); ApplyArchiveResult(archive.Replay(m.id)); }
                }
                ArchiveHit(card, i);   // the whole card is the focus target; its buttons do the work with a pointer
            }
        }

        private void DrawArchiveSmallButton(Rect r, string label)
        {
            var hot = r.Contains(PointerPosition) && !controllerNavigation;
            Fill(r, hot ? new Color(.62f, .42f, .10f, .22f) : new Color(.62f, .42f, .10f, .10f)); Outline(r, new Color(ArchiveGold.r, ArchiveGold.g, ArchiveGold.b, .8f), 1);
            var s = ArchiveStyle(Mathf.Max(8, Mathf.RoundToInt(r.height * .5f)), false, TextAnchor.MiddleCenter); s.normal.textColor = ArchiveInk; GUI.Label(r, label, s);
        }

        // ---- reading a memory ----
        private void DrawArchiveReading(Rect left, Rect right, float alpha, string id, int index)
        {
            var reading = ArchiveReadingFor(id); if (reading == null) return;
            archiveInkState.narrator = profile.chronicle.NarratorState;
            var spread = reading.spreads[Mathf.Clamp(index, 0, reading.spreads.Count - 1)];
            DrawChronicleText(left, spread.left, ChroniclePageSide.Left, archiveInkState, alpha);
            DrawChronicleText(right, spread.right, ChroniclePageSide.Right, archiveInkState, alpha);
            var u = left.width / 470f;
            ArchiveText(new Rect(left.x, left.yMax - 22 * u, left.width, 22 * u), left.yMax - 22 * u, "PAGE " + (Mathf.Clamp(index, 0, reading.spreads.Count - 1) + 1) + " OF " + reading.spreads.Count, Mathf.RoundToInt(13 * u), ArchiveDim, alpha, false, TextAnchor.UpperLeft, 0);
            var button = new Rect(right.xMax - 200 * u, right.yMax - 40 * u, 200 * u, 34 * u);
            DrawArchiveSmallButton(button, "WATCH THIS MEMORY");
            if (ArchiveHit(button, 0)) ApplyArchiveResult(archive.Replay(id));
        }

        // ---- Chapter X ----
        private void DrawArchiveSecret(Rect left, Rect right, float alpha)
        {
            var progress = profile.chronicle; var state = ChronicleArchive.SecretState(progress); var u = left.width / 470f; var y = left.y;
            y = ArchiveText(left, y, "CHAPTER X", Mathf.RoundToInt(15 * u), ArchiveGold, alpha, false, TextAnchor.UpperLeft, 2 * u);
            y = ArchiveText(left, y, state == ChronicleSecretState.Locked ? "???" : "The One Who Remembers", Mathf.RoundToInt(30 * u), ArchiveInk, alpha, false, TextAnchor.UpperLeft, 12 * u);
            var body = state == ChronicleSecretState.Locked ? "A last chapter waits at the end of the book. It cannot be opened until every memory has been recovered."
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
                var button = new Rect(right.x, right.yMax - 60 * u, right.width, 48 * u);
                Fill(button, new Color(.3f, .8f, .55f, .2f * alpha)); Outline(button, new Color(.1f, .5f, .3f, alpha), 2);
                var s = ArchiveStyle(Mathf.Max(9, Mathf.RoundToInt(20 * u)), false, TextAnchor.MiddleCenter); s.normal.textColor = new Color(.05f, .3f, .2f, alpha);
                GUI.Label(button, state == ChronicleSecretState.Unlocked ? "OPEN THE FINAL CHAPTER" : "READ IT AGAIN", s);
                if (ArchiveHit(button, 0)) ApplyArchiveResult(archive.BeginSecret());
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

        // Drawn with plain fills only: the hub draws its art inside a clipped group, where rotated lines would land in the wrong place.
        private void HubArtChronicle(Rect r)
        {
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
