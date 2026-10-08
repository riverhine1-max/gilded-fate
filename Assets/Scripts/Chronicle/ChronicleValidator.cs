using System;
using System.Collections.Generic;
using System.Linq;

namespace GildedFate.Chronicle
{
    /// <summary>
    /// Static checks over the Chronicle's authored content. Run by the unit tests and by the editor quality gate, so a typo in a
    /// scene script, an impossible unlock rule or a spoiler in early narration fails loudly instead of shipping.
    /// </summary>
    public static class ChronicleValidator
    {
        public static List<string> Validate()
        {
            var errors = new List<string>();
            ValidateCatalog(errors);
            ValidateScenes(errors);
            ValidateRules(errors);
            return errors;
        }

        static void ValidateCatalog(List<string> errors)
        {
            if (ChronicleCatalog.Memories.Length != ChronicleCatalog.MemoryCount) errors.Add("Expected 27 memories, found " + ChronicleCatalog.Memories.Length);
            if (ChronicleCatalog.Memories.Select(m => m.id).Distinct().Count() != ChronicleCatalog.Memories.Length) errors.Add("Duplicate memory ids");
            if (ChronicleCatalog.Corrections.Select(c => c.id).Distinct().Count() != ChronicleCatalog.Corrections.Length) errors.Add("Duplicate correction ids");
            if (ChronicleCatalog.Facts.Select(f => f.id).Distinct().Count() != ChronicleCatalog.Facts.Length) errors.Add("Duplicate fact ids");
            for (var chapter = 1; chapter <= ChronicleCatalog.ChapterCount; chapter++)
            {
                var inChapter = ChronicleCatalog.MemoriesInChapter(chapter).ToList();
                if (inChapter.Count != 3) errors.Add("Chapter " + chapter + " must have 3 memories");
                foreach (ChronicleHero hero in Enum.GetValues(typeof(ChronicleHero)))
                    if (inChapter.Count(m => m.hero == hero) != 1) errors.Add("Chapter " + chapter + " must have exactly one " + hero + " memory");
            }
            foreach (var m in ChronicleCatalog.Memories)
            {
                foreach (var f in m.facts) if (ChronicleCatalog.FindFact(f) == null) errors.Add(m.id + " references unknown fact " + f);
                foreach (var c in m.corrections)
                {
                    var def = ChronicleCatalog.FindCorrection(c);
                    if (def == null) errors.Add(m.id + " references unknown correction " + c);
                    else if (!def.sources.Contains(m.id)) errors.Add(m.id + " commits " + c + " but is not one of its sources");
                }
            }
            foreach (var c in ChronicleCatalog.Corrections)
            {
                if (string.IsNullOrWhiteSpace(c.original) || string.IsNullOrWhiteSpace(c.corrected)) errors.Add(c.id + " needs original and corrected text");
                foreach (var s in c.sources) if (s != "SECRET" && ChronicleCatalog.FindMemory(s) == null) errors.Add(c.id + " has unknown source " + s);
                // The original sentence must live on its chapter's page so an old passage can be corrected later.
                var chapter = ChronicleCatalog.FindChapter(c.chapter);
                if (chapter == null || !chapter.preface.Any(p => p.correctionId == c.id)) errors.Add(c.id + " is missing from the preface of chapter " + c.chapter);
            }
        }

        static void ValidateRules(List<string> errors)
        {
            foreach (var m in ChronicleCatalog.Memories)
            {
                var r = m.rule;
                if (r == null) { errors.Add(m.id + " has no unlock rule"); continue; }
                if (string.IsNullOrWhiteSpace(r.hint)) errors.Add(m.id + " rule needs a spoiler-free hint");
                if (r.metric == ChronicleMetric.BestFloor && (r.threshold < 1 || r.threshold > 54)) errors.Add(m.id + " floor target " + r.threshold + " is outside the 54-floor run");
                if (r.metric == ChronicleMetric.ActBossDefeated && (r.act < 1 || r.act > 3)) errors.Add(m.id + " boss rule needs an act 1-3");
                if (r.metric == ChronicleMetric.Wins && r.threshold < 1) errors.Add(m.id + " win rule needs a positive target");
            }
            // Winning one run with a hero must be enough to earn all nine of that hero's memories (everything is reachable).
            foreach (ChronicleHero hero in Enum.GetValues(typeof(ChronicleHero)))
            {
                var p = new ChronicleProgress();
                p.RegisterRunEnded(hero, true, 3, 54, "validate-" + hero);
                foreach (var m in ChronicleCatalog.Memories.Where(x => x.hero == hero))
                    if (!p.IsUnlocked(m.id)) errors.Add(hero + ": a single victory does not unlock " + m.id);
            }
        }

        static void ValidateScenes(List<string> errors)
        {
            var required = new List<string> { ChronicleCatalog.OpeningSceneId, ChronicleCatalog.OpeningShortSceneId, ChronicleCatalog.SecretSceneId };
            required.AddRange(ChronicleCatalog.Memories.Select(m => m.id));
            var contexts = Contexts();
            foreach (var id in required)
            {
                var scene = ChronicleScripts.Get(id);
                if (scene == null) { errors.Add("No scene registered for " + id); continue; }
                ValidateScene(scene, errors, contexts);
            }
        }

        public static List<IChronicleContext> Contexts()
        {
            // Layout must hold in every adaptive-narration variant: facts known or not, corrections known or not.
            var list = new List<IChronicleContext>();
            foreach (var knowsFourth in new[] { false, true })
                foreach (var knowsCorrections in new[] { false, true })
                {
                    var c = new ChronicleStaticContext();
                    if (knowsFourth) c.facts.Add("FACT_FOURTH_COMPANION");
                    if (knowsCorrections) foreach (var corr in ChronicleCatalog.Corrections) c.corrections.Add(corr.id);
                    list.Add(c);
                }
            return list;
        }

        static readonly HashSet<string> actorCommands = new HashSet<string>
        { nameof(ChronicleOp.MoveIllustrationCharacter), nameof(ChronicleOp.FadeIllustrationCharacter) };

        static void ValidateScene(ChronicleScene scene, List<string> errors, List<IChronicleContext> contexts)
        {
            var id = scene.id;
            if (scene.commands.Count == 0 || scene.commands[scene.commands.Count - 1].op != ChronicleOp.FinishScene) errors.Add(id + " must end with FinishScene");
            if (scene.CountOf(ChronicleOp.FinishScene) != 1) errors.Add(id + " must contain exactly one FinishScene");

            // Walk the commands tracking per-spread state: actors, written correction originals, struck corrections.
            var actors = new HashSet<string>(); var originals = new HashSet<string>(); var blocks = new HashSet<string>(); var struck = new HashSet<string>();
            var saved = new HashSet<string>(); var facts = new HashSet<string>(); var bookOpen = false; var visible = 0;
            foreach (var c in scene.commands)
            {
                if (!ConditionOk(c.condition)) errors.Add(id + ": bad condition '" + c.condition + "'");
                switch (c.op)
                {
                    case ChronicleOp.OpenBook: bookOpen = true; actors.Clear(); originals.Clear(); blocks.Clear(); break;
                    case ChronicleOp.CloseBook: bookOpen = false; break;
                    case ChronicleOp.TurnPage:
                        if (!bookOpen) errors.Add(id + ": TurnPage while the book is closed");
                        actors.Clear(); originals.Clear(); blocks.Clear(); break;
                    case ChronicleOp.ShowIllustration:
                    case ChronicleOp.ChangeIllustrationBackground:
                        if (!ChronicleBackdrops.Exists(c.id)) errors.Add(id + ": unknown backdrop '" + c.id + "'");
                        visible++; break;
                    case ChronicleOp.SpawnActor: actors.Add(c.id); visible++; break;
                    case ChronicleOp.MoveIllustrationCharacter:
                    case ChronicleOp.FadeIllustrationCharacter:
                        if (!actors.Contains(c.id)) errors.Add(id + ": " + c.op + " targets '" + c.id + "' which is not on this spread");
                        visible++; break;
                    case ChronicleOp.AnimateIllustration:
                        if (c.arg == "speak" && !string.IsNullOrEmpty(c.id) && !actors.Contains(c.id)) errors.Add(id + ": speaker '" + c.id + "' is not on this spread");
                        visible++; break;
                    case ChronicleOp.WriteText:
                        blocks.Add(c.id);
                        if (c.id.StartsWith("corr:", StringComparison.Ordinal))
                        {
                            if (ChronicleCatalog.FindCorrection(c.arg) == null) errors.Add(id + ": unknown correction " + c.arg);
                            originals.Add(c.arg);
                        }
                        break;
                    case ChronicleOp.ShowNarration: blocks.Add(c.id); break;
                    case ChronicleOp.CrossOutText:
                        if (!originals.Contains(c.id) && !blocks.Contains(c.id)) errors.Add(id + ": strike/erase targets '" + c.id + "' which is not written on this spread");
                        if (c.arg != "fade") struck.Add(c.id);
                        break;
                    case ChronicleOp.WriteCorrection:
                        if (ChronicleCatalog.FindCorrection(c.id) == null) errors.Add(id + ": unknown correction " + c.id);
                        else if (!struck.Contains(c.id)) errors.Add(id + ": " + c.id + " is rewritten before its original is struck through");
                        break;
                    case ChronicleOp.SaveCorrection:
                        if (ChronicleCatalog.FindCorrection(c.id) == null) errors.Add(id + ": unknown correction " + c.id);
                        saved.Add(c.id); break;
                    case ChronicleOp.DiscoverFact:
                        if (ChronicleCatalog.FindFact(c.id) == null) errors.Add(id + ": unknown fact " + c.id);
                        facts.Add(c.id); break;
                }
            }

            var memory = ChronicleCatalog.FindMemory(id);
            if (memory != null)
            {
                if (!facts.SetEquals(memory.facts)) errors.Add(id + ": discovered facts [" + string.Join(",", facts) + "] do not match the catalog [" + string.Join(",", memory.facts) + "]");
                if (!saved.SetEquals(memory.corrections)) errors.Add(id + ": saved corrections [" + string.Join(",", saved) + "] do not match the catalog [" + string.Join(",", memory.corrections) + "]");
                if (scene.CountOf(ChronicleOp.ShowNarration) < 3) errors.Add(id + ": needs at least 3 narration lines");
                if (visible < 3) errors.Add(id + ": the placeholder illustration does not visibly do anything");
                if (scene.endPose != ChronicleBookPose.Open) errors.Add(id + ": a memory must leave the book open for the archive");
            }
            if (id == ChronicleCatalog.SecretSceneId && (!facts.Contains("FACT_NARRATOR_IS_OBSERVER") || !saved.Contains("CORR_10"))) errors.Add(id + ": the finale must reveal the narrator and record CORR_10");
            if ((id == ChronicleCatalog.OpeningSceneId || id == ChronicleCatalog.OpeningShortSceneId) && scene.endPose != ChronicleBookPose.Closed) errors.Add(id + ": the opening must close the book");

            // Layout: nothing may overflow its page, in any adaptive-narration variant.
            foreach (var ctx in contexts)
            {
                var state = ChronicleSceneResolver.Resolve(scene, ctx);
                foreach (var b in state.transcript)
                    if (b.overflow) errors.Add(id + ": passage \"" + Trim(b.text) + "\" overflows its page");
                // Spoiler guard: the narrator's identity is named only in Chapter X.
                if (id != ChronicleCatalog.SecretSceneId)
                    foreach (var b in state.transcript)
                        if (b.text.IndexOf("Observer", StringComparison.OrdinalIgnoreCase) >= 0) errors.Add(id + ": names the Forgotten Observer before Chapter X");
                if (memory != null && memory.chapter <= 2 || id == ChronicleCatalog.OpeningSceneId || id == ChronicleCatalog.OpeningShortSceneId)
                    foreach (var b in state.transcript)
                        if (b.text.IndexOf("fourth", StringComparison.OrdinalIgnoreCase) >= 0) errors.Add(id + ": mentions the fourth companion too early");
            }
        }

        static string Trim(string s) => s.Length > 40 ? s.Substring(0, 40) + "..." : s;

        static bool ConditionOk(string condition)
        {
            if (string.IsNullOrEmpty(condition)) return true;
            foreach (var raw in condition.Split('&'))
            {
                var t = raw.Trim().TrimStart('!');
                if (t.StartsWith("fact:", StringComparison.Ordinal)) { if (ChronicleCatalog.FindFact(t.Substring(5)) == null) return false; }
                else if (t.StartsWith("corr:", StringComparison.Ordinal)) { if (ChronicleCatalog.FindCorrection(t.Substring(5)) == null) return false; }
                else if (t.StartsWith("state:", StringComparison.Ordinal)) { if (!int.TryParse(t.Substring(6), out _)) return false; }
                else if (t != "replay" && t != "first") return false;
            }
            return true;
        }
    }
}
