using System;
using System.Collections.Generic;

namespace GildedFate.Chronicle
{
    /// <summary>Uses the existing story and recorded interruption; milestone playback grants no new unlocks.</summary>
    public static class ChronicleMilestoneScenes
    {
        public const string VictoryId = "SCENE_RUN_VICTORY", DefeatId = "SCENE_RUN_DEFEAT";

        public static List<string> Memories(ChronicleProgress progress, ChronicleHero hero, int act, bool runEnded)
        {
            var result = new List<string>();
            if (progress == null || act < 1 || act > 3 || (int)hero < 0 || (int)hero > 2) return result;
            progress.Ensure();
            foreach (var memory in ChronicleCatalog.Memories)
                if (memory.hero == hero && memory.chapter <= act * 3 && progress.IsUnlocked(memory.id)
                    && !progress.IsViewed(memory.id) && progress.RuleMet(memory.rule, hero)) result.Add(memory.id);
            // Every act-clear gets its own memory, even when it was previously watched in the Archive.
            if (!runEnded)
            {
                var boundary = ChronicleCatalog.MemoryFor(act * 3, hero);
                if (progress.IsUnlocked(boundary.id) && progress.RuleMet(boundary.rule, hero) && !result.Contains(boundary.id)) result.Add(boundary.id);
            }
            return result;
        }

        public static string EndingResponse(bool victory, int responseIndex)
        {
            if (victory) return "Not yet.";
            // Positive modulo also handles restored or test counters safely.
            switch ((responseIndex % 3 + 3) % 3)
            {
                case 1: return "Nothing was meant to be lost.";
                case 2: return "The world is still broken.";
                default: return "Not yet.";
            }
        }

        public static ChronicleScene Ending(bool victory, ChronicleHero hero, int responseIndex = 0)
        {
            if ((int)hero < 0 || (int)hero > 2) throw new ArgumentOutOfRangeException(nameof(hero));
            var b = new ChronicleSceneBuilder(victory ? VictoryId : DefeatId, victory ? "A Journey Without an Ending" : "An Ending Refused")
                .Open().Heading(ChronicleCatalog.HeroName(hero).ToUpperInvariant())
                .Illustrate("page")
                .Hero("hero", hero, .5f, .62f)
                .Anim("breathe", "hero", 8f, .2f)
                .Say("And there, at last, the tale came to—", .5f)
                .WriteBlock("run_conclusion", "The End", ChronicleTextStyle.Ending, .5f)
                .Audio("ending_hush")
                .Anim("shake", "", 1.4f, .7f).Magic("book_tremor", .7f).Wait(.3f)
                .StrikeBlock("run_conclusion")
                .Deeper(EndingResponse(victory, responseIndex), .8f);
            b.Say("I did not write that.", .6f)
                .Say("Then there is more.", .6f);
            b.Magic("emerald_glint", .4f).Anim("threads", "", 1.2f, .5f).Wait(.8f);
            var scene = b.Finish();
            foreach (var command in scene.commands)
                if (command.op == ChronicleOp.PlayAudio && command.id == "deeper_voice") command.amount = .18f;
            return scene;
        }
    }
}
