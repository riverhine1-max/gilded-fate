using System;
using System.Collections.Generic;

namespace GildedFate.Chronicle
{
    /// <summary>
    /// Every Chronicle scene as data: the opening (full and short), the 27 character memories (ChronicleScripts.ActI/II/III.cs)
    /// and secret Chapter X. Narration is the canonical text from the narrative bible; the placeholder animation is a script of
    /// generic commands (slide, fade, flash, threads...), so replacing the visuals later never touches this file.
    /// </summary>
    public static partial class ChronicleScripts
    {
        static readonly Dictionary<string, Func<ChronicleScene>> factories = new Dictionary<string, Func<ChronicleScene>>();
        static readonly Dictionary<string, ChronicleScene> cache = new Dictionary<string, ChronicleScene>();

        static ChronicleScripts()
        {
            factories[ChronicleCatalog.OpeningSceneId] = Opening;
            factories[ChronicleCatalog.OpeningShortSceneId] = OpeningShort;
            factories[ChronicleCatalog.SecretSceneId] = Finale;
            RegisterActI(); RegisterActII(); RegisterActIII();
        }

        static void Reg(int memoryNumber, Func<ChronicleScene> factory) { factories[ChronicleCatalog.MemoryKey(memoryNumber)] = factory; }

        /// <summary>Returns the scene for an ID (MEM_01..MEM_27, SCENE_OPENING, SCENE_OPENING_SHORT, SCENE_CH10), or null.</summary>
        public static ChronicleScene Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (cache.TryGetValue(id, out var scene)) return scene;
            if (!factories.TryGetValue(id, out var factory)) return null;
            scene = factory(); cache[id] = scene; return scene;
        }
        public static IEnumerable<string> Ids => factories.Keys;
        public static bool Has(string id) => factories.ContainsKey(id);

        // ------------------------------------------------------------------ shared helpers
        static string Roman(int n) => new[] { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" }[n];

        /// <summary>Every memory starts the same way: open the book on a fresh spread, label it, title it.</summary>
        static ChronicleSceneBuilder Begin(int memoryNumber, ChronicleSpreadLayout layout = ChronicleSpreadLayout.IllustrationLeftTextRight)
        {
            var m = ChronicleCatalog.FindMemory(ChronicleCatalog.MemoryKey(memoryNumber));
            return new ChronicleSceneBuilder(m.id, m.title).Open(layout)
                .Write("CHAPTER " + Roman(m.chapter) + "  ·  " + ChronicleCatalog.HeroName(m.hero).ToUpperInvariant(), ChronicleTextStyle.Note, .15f)
                .Heading(m.title);
        }

        /// <summary>Ends a memory: a short beat, then every fact the memory reveals, then finish.</summary>
        static ChronicleScene End(ChronicleSceneBuilder b, int memoryNumber)
        {
            b.Wait(.8f);
            foreach (var fact in ChronicleCatalog.FindMemory(ChronicleCatalog.MemoryKey(memoryNumber)).facts) b.Fact(fact);
            return b.Finish();
        }

        /// <summary>The fourth companion, drawn as an unnamed shadow until the player has discovered there was a fourth.</summary>
        public static ChronicleSceneBuilder Fourth(this ChronicleSceneBuilder b, string id, float x, float y, float alpha = 1f)
        {
            b.If("!fact:FACT_FOURTH_COMPANION").Actor(id, ChronicleShape.Shadow, ChronicleColor.Shade, x, y, .09f, .24f, "?", alpha).EndIf();
            b.If("fact:FACT_FOURTH_COMPANION").Actor(id, ChronicleShape.Figure, ChronicleColor.Fourth, x, y, .09f, .24f, "Fourth", alpha).EndIf();
            return b;
        }

        /// <summary>Adaptive narration: a memory that shows the fourth companion before the player has met him says so honestly.</summary>
        public static ChronicleSceneBuilder FourthIntro(this ChronicleSceneBuilder b) =>
            b.If("!fact:FACT_FOURTH_COMPANION").Say("A fourth voice speaks in this memory. The record has no place for him. I will write what I hear.").EndIf();

        /// <summary>If the player reaches a Chapter III memory before the campfire, the narrator reconciles the old record here.</summary>
        public static ChronicleSceneBuilder LateFourthCorrection(this ChronicleSceneBuilder b) =>
            b.If("!corr:CORR_01").Say("I wrote that three stood together. I must look again at that page.")
                .Original("CORR_01").Hesitate(.8f).Correct("CORR_01").EndIf();

        // ------------------------------------------------------------------ opening cinematic
        /// <summary>The launch cinematic, about 75-90 seconds. The narrator still believes the old history; nothing here reveals the fourth companion.</summary>
        static ChronicleScene Opening()
        {
            var b = new ChronicleSceneBuilder(ChronicleCatalog.OpeningSceneId, "The Chronicle of Broken Fate").Narrator(ChronicleNarratorState.ConfidentHistorian);
            b.Wait(1.2f).Audio("page_rustle", .6f).Magic("emerald_glint", .4f).Wait(.8f)
             .Open(ChronicleSpreadLayout.IllustrationLeftTextRight)
             .Say("Before the world was broken, there was a time when tomorrow belonged to no one.")
             .Illustrate("kingdom")
             .Actor("castle", ChronicleShape.Castle, new ChronicleColor(.18f, .16f, .22f), .72f, .44f, .34f, .5f)
             .Actor("p1", ChronicleShape.Figure, new ChronicleColor(.3f, .3f, .38f), .3f, .66f, .06f, .16f)
             .Actor("p2", ChronicleShape.Figure, new ChronicleColor(.34f, .3f, .36f), .42f, .67f, .06f, .15f)
             .Move("p1", .52f, .66f, 5f).Move("p2", .6f, .67f, 5f)
             .Say("A kingdom stood beneath an unbroken sky. Its people built monuments to the past, and dreamed of futures they could not yet see.")
             .Wait(.4f)
             .Say("Then came a traveler.")
             .Actor("traveler", ChronicleShape.Figure, ChronicleColor.Being, .02f, .67f, .08f, .22f, "", 0f)
             .Fade("traveler", 1f, 1f).Move("traveler", .34f, .67f, 5f)
             .Say("He spoke of worlds beyond our own. Of roads not yet traveled. Of lives that might have been.")
             .Say("To some, he offered knowledge. To others, hope.")
             .Wait(.5f)
             .Turn(ChronicleSpreadLayout.IllustrationLeftTextRight)
             .Illustrate("threads")
             .Actor("lineA", ChronicleShape.Line, ChronicleColor.Being, .5f, .5f, .7f, .0f)
             .Anim("threads", "", 5f, .9f)
             .Say("But somewhere between the future he promised and the future he desired, something changed.")
             .Say("The world did not end in fire or darkness.")
             .Say("It broke in ways that no ordinary eye could see.")
             .Anim("fracture", "", 3f, .9f).Anim("shatter_world", "", 4f, .8f).Magic("fate_crack", .5f)
             .Say("Time lost its direction. Memory lost its certainty. The paths of fate scattered into countless pieces.")
             .Wait(.6f)
             .Turn(ChronicleSpreadLayout.IllustrationLeftTextRight)
             .Illustrate("field")
             .Hero("v", ChronicleHero.Vanguard, .22f, .64f, 0f).Hero("h", ChronicleHero.Hexer, .5f, .64f, 0f).Hero("r", ChronicleHero.Reaper, .78f, .64f, 0f)
             .Say("Three names endured.")
             .Say("Vanguard.").Fade("v", 1f, .8f)
             .Say("Hexer.").Fade("h", 1f, .8f)
             .Say("Reaper.").Fade("r", 1f, .8f)
             .Say("Across the broken world, their stories continued.")
             .Move("v", .12f, .64f, 4f).Move("r", .88f, .64f, 4f).Move("h", .5f, .5f, 4f)
             .Say("Perhaps one of them will find the truth.")
             .Say("Perhaps one of them will bring this tale to its final page.")
             .Hesitate(1.2f)
             .Say("Although...{p=1.1}", .5f)
             .Wait(.9f)
             .Say("No. That is how the story was written.")
             .Say("The rest remains to be discovered.")
             .Wait(.8f)
             .Close()
             .Wait(.5f);
            return b.Finish();
        }

        /// <summary>The abridged opening used after the first viewing (about 12 seconds).</summary>
        static ChronicleScene OpeningShort()
        {
            var b = new ChronicleSceneBuilder(ChronicleCatalog.OpeningShortSceneId, "The Chronicle of Broken Fate (short)").Narrator(ChronicleNarratorState.ConfidentHistorian);
            b.Wait(.5f).Audio("page_rustle", .6f).Open(ChronicleSpreadLayout.IllustrationLeftTextRight)
             .Illustrate("field")
             .Hero("v", ChronicleHero.Vanguard, .22f, .64f, 0f).Hero("h", ChronicleHero.Hexer, .5f, .64f, 0f).Hero("r", ChronicleHero.Reaper, .78f, .64f, 0f)
             .Say("Before the world was broken, there was a time when tomorrow belonged to no one.")
             .Say("Three names endured.").Fade("v", 1f, .6f).Fade("h", 1f, .6f).Fade("r", 1f, .6f)
             .Say("Vanguard. Hexer. Reaper.")
             .Close();
            return b.Finish();
        }

        // ------------------------------------------------------------------ Chapter X
        /// <summary>The secret finale: the narrator assembles the evidence and recognizes himself. Seven beats, as written in the bible.</summary>
        static ChronicleScene Finale()
        {
            var b = new ChronicleSceneBuilder(ChronicleCatalog.SecretSceneId, "The One Who Remembers").Narrator(ChronicleNarratorState.RememberingObserver);

            // Scene 1 — the book opens alone
            b.Wait(1.2f).Magic("emerald_glint", .5f).Wait(.6f).Open(ChronicleSpreadLayout.IllustrationLeftTextRight)
             .Write("CHAPTER X  ·  THE ONE WHO REMEMBERS", ChronicleTextStyle.Note, .2f)
             .Say("I have spent so long trying to understand this story.")
             .Say("I believed it began with a kingdom. With a traveler. With three people whose lives refused to end.")
             .Illustrate("kingdom").Actor("castle", ChronicleShape.Castle, new ChronicleColor(.18f, .16f, .22f), .72f, .44f, .34f, .5f)
             .Say("But every answer led somewhere I had already been.")
             .Backdrop("campfire", 1.5f).Say("An empty place by the fire.")
             .Backdrop("diagram", 1.5f).Say("A fourth signature.")
             .Backdrop("sanctuary", 1.5f).Say("A voice beneath the kingdom. A promise.")
             .Backdrop("campfire", 1.5f).Say("And always...{p=0.8} Someone missing.", .7f)

            // Scene 2 — the original four
             .Turn(ChronicleSpreadLayout.IllustrationLeftTextRight)
             .Illustrate("campfire")
             .Actor("fire", ChronicleShape.Circle, new ChronicleColor(1f, .55f, .15f), .5f, .68f, .09f, .09f)
             .Hero("v", ChronicleHero.Vanguard, .24f, .62f, 0f).Hero("h", ChronicleHero.Hexer, .76f, .62f, 0f).Hero("r", ChronicleHero.Reaper, .5f, .5f, 0f)
             .Actor("f4", ChronicleShape.Figure, ChronicleColor.Fourth, .5f, .8f, .09f, .24f, "Fourth", 0f)
             .Fade("v", 1f, 1f).Fade("h", 1f, 1f).Fade("r", 1f, 1f)
             .Say("They were friends. They trusted one another. They were frightened of what was happening to their world.")
             .Wait(.6f).Fade("f4", 1f, 1.4f).Magic("four_strands", .6f)
             .Say("And one of them believed he could save the others.")
             .Move("f4", .5f, .3f, 4f)
             .Say("He made a decision no one could make safely. He took a power he could not control.")
             .Say("He promised that even if the others forgot...{p=1.0}", .6f)
             .Say("He would remember.", .9f)

            // Scene 3 — the writing
             .Turn(ChronicleSpreadLayout.TextLeftIllustrationRight)
             .Illustrate("journal")
             .Actor("old", ChronicleShape.Page, new ChronicleColor(.5f, .4f, .26f), .5f, .5f, .8f, .78f, "ancient journal")
             .Actor("new", ChronicleShape.Page, ChronicleColor.Parchment, .5f, .5f, .8f, .78f, "the Chronicle", 0f)
             .Say("That handwriting. I thought it belonged to someone who had disappeared.")
             .Fade("new", .9f, 2f)
             .Say("But these marks...{p=0.8}")
             .Anim("sigil_glow", "", 3f, .8f)
             .Say("I know how they curve. I know where the hand hesitates. I know the shape of every letter.")
             .Say("Because...{p=1.0}", .6f).Say("No.", .5f).Anim("shake", "", 1.4f, .8f).Magic("book_tremor", .7f)
             .Turn(ChronicleSpreadLayout.IllustrationLeftTextRight)
             .Illustrate("souls")
             .Actor("mortal", ChronicleShape.Figure, ChronicleColor.Fourth, .38f, .62f, .08f, .22f)
             .Actor("deep", ChronicleShape.Shadow, ChronicleColor.Shade, .64f, .56f, .2f, .5f)
             .Move("deep", .5f, .56f, 4f)
             .Deeper("You were never meant to find that.")
             .Speak("mortal", "NARRATOR", "Who are you?")
             .Deeper("You have asked that question for five thousand years.")
             .Speak("mortal", "NARRATOR", "And you have never answered.")
             .Deeper("We are what remained.")

            // Scene 4 — the Severing remembered
             .Turn(ChronicleSpreadLayout.IllustrationLeftTextRight)
             .Illustrate("sanctuary")
             .Actor("f4b", ChronicleShape.Figure, ChronicleColor.Fourth, .5f, .62f, .09f, .24f, "")
             .Hero("v2", ChronicleHero.Vanguard, .12f, .62f).Hero("h2", ChronicleHero.Hexer, .2f, .62f).Hero("r2", ChronicleHero.Reaper, .28f, .62f)
             .Move("v2", -.1f, .62f, 6f).Move("h2", -.1f, .62f, 6f).Move("r2", -.1f, .62f, 6f)
             .Say("I thought I was protecting them. I thought that if they could forget, the power would lose its hold.")
             .Backdrop("collapse", 3f).Anim("shatter_world", "", 4f, .8f)
             .Say("I knew they might forget me. But I did not understand what I was asking them to lose.")
             .Say("I made their choice for them. And when the world broke...{p=0.9} I forgot the very people I wanted to save.", .8f)

            // Scene 5 — the Forgotten Observer
             .Turn(ChronicleSpreadLayout.IllustrationLeftTextRight)
             .Illustrate("emerald")
             .Actor("e1", ChronicleShape.Glyph, ChronicleColor.Vanguard, .3f, .4f, .1f, .1f).Actor("e2", ChronicleShape.Glyph, ChronicleColor.Hexer, .5f, .3f, .1f, .1f)
             .Actor("e3", ChronicleShape.Glyph, ChronicleColor.Reaper, .7f, .4f, .1f, .1f).Actor("e4", ChronicleShape.Glyph, ChronicleColor.Fourth, .5f, .7f, .1f, .1f, "", 0f)
             .Anim("threads", "", 4f, .9f).Fade("e4", 1f, 2f).Anim("sigil_glow", "", 4f, 1f)
             .Say("I was there. I walked beside them. I argued with them. I was the one they tried to stop.")
             .Say("I wrote the words that led us here. I am not merely the keeper of their history.")
             .Hesitate(1f)
             .Original("CORR_10").Hesitate(.8f).Correct("CORR_10")
             .Say("I am the part of it that was forgotten.", .7f)
             .WriteBlock("final_name", "I AM THE FORGOTTEN OBSERVER.", ChronicleTextStyle.Ending, 1f)
             .Magic("emerald_glint", .9f).Anim("pulse", "e4", 2f, 1f)
             .Deeper("And I am still here.")
             .Say("I know. But you are not the only one who remembers now.")

            // Scene 6 — the three heroes
             .Turn(ChronicleSpreadLayout.IllustrationLeftTextRight)
             .Illustrate("dawn")
             .Hero("v3", ChronicleHero.Vanguard, .18f, .64f).Hero("h3", ChronicleHero.Hexer, .5f, .64f).Hero("r3", ChronicleHero.Reaper, .82f, .64f)
             .Speak("v3", "VANGUARD", "We don't leave him there.")
             .Speak("h3", "HEXER", "Then we need to understand what holds him.")
             .Speak("r3", "REAPER", "And what happens if we break it.")
             .Move("v3", .3f, .62f, 3f)
             .Speak("v3", "VANGUARD", "Then we find out.")

            // Scene 7 — the unfinished ending
             .Turn(ChronicleSpreadLayout.TextOnly)
             .Say("For five thousand years, I searched for an ending. I believed I could write one if I understood enough.")
             .Say("But an ending is not something that belongs to the person holding the pen.")
             .WriteBlock("the_end", "The En", ChronicleTextStyle.Ending, .3f)
             .Hesitate(1.4f)
             .Say("I have made that mistake before.")
             .Erase("the_end")
             .Say("Perhaps the story was never waiting to end. Perhaps it was waiting for us to find one another.")
             .Wait(.8f)
             .WriteBlock("to_be_continued", "TO BE CONTINUED.", ChronicleTextStyle.Ending, 1f)
             .Wait(.6f)
             .Deeper("The world is still broken.")
             .Wait(.6f).Close().Wait(.5f)
             .Fact("FACT_NARRATOR_IS_OBSERVER");
            return b.Finish();
        }
    }
}
