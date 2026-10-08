namespace GildedFate.Chronicle
{
    // ACT III: THE STORY THAT WOULD NOT END (Chapters VII-IX). Aftermath, the repeating runs, and the last pieces.
    // None of these scenes has the narrator say who he is; that belongs to Chapter X.
    public static partial class ChronicleScripts
    {
        static void RegisterActIII()
        {
            Reg(19, M19); Reg(20, M20); Reg(21, M21); Reg(22, M22); Reg(23, M23); Reg(24, M24); Reg(25, M25); Reg(26, M26); Reg(27, M27);
        }

        // ---------------- CHAPTER VII: THE WORLD THAT FORGOT ----------------

        static ChronicleScene M19() // Vanguard: the long migration
        {
            var b = Begin(19).Illustrate("migration").Hero("v", ChronicleHero.Vanguard, .08f, .64f);
            for (var i = 0; i < 4; i++) b.Actor("p" + i, ChronicleShape.Figure, Folk, .02f + i * .05f, .68f, .045f, .12f);
            b.Move("v", .5f, .64f, 14f);
            for (var i = 0; i < 4; i++) b.Move("p" + i, .38f + i * .05f, .68f, 14f);
            b.Say("After the breaking, there was no single road home.")
             .Backdrop("clocks", 5f)
             .Say("The survivors followed fragments of maps and memories that no longer agreed.")
             .Say("The warrior protected people whose names he barely knew.")
             .Backdrop("field", 5f)
             .Say("He kept looking over his shoulder.")
             .Say("As though someone had fallen behind.", .6f)
             .Anim("echo_ring", "v", 2.5f, .5f);
            return End(b, 19);
        }

        static ChronicleScene M20() // Hexer: the broken hours. CORR_07
        {
            var b = Begin(20).Illustrate("clocks").Hero("h", ChronicleHero.Hexer, .1f, .64f)
                .Actor("clock1", ChronicleShape.Clock, new ChronicleColor(.55f, .5f, .35f), .3f, .3f, .12f, .12f).Actor("clock2", ChronicleShape.Clock, new ChronicleColor(.55f, .5f, .35f), .62f, .26f, .12f, .12f).Actor("clock3", ChronicleShape.Clock, new ChronicleColor(.55f, .5f, .35f), .86f, .34f, .12f, .12f)
                .Original("CORR_07")
                .Say("Time did not stop. It lost its agreement with itself.")
                .Move("h", .5f, .64f, 4f).Say("A morning might return.")
                .Move("h", .26f, .64f, 1.2f).Anim("flash", "", .4f, .6f)
                .Say("A century might pass between two familiar steps.")
                .Move("h", .78f, .64f, 1.2f).Anim("flash", "", .4f, .6f)
                .Say("The scholar searched for a clock that could tell him which moment was real.")
                .Say("He found only fragments.", .6f)
                .Hesitate(.8f).Correct("CORR_07");
            return End(b, 20);
        }

        static ChronicleScene M21() // Reaper: the Plane of Protection
        {
            var b = Begin(21).Illustrate("weave").Hero("r", ChronicleHero.Reaper, .12f, .64f)
                .Actor("s1", ChronicleShape.Figure, Folk, .06f, .68f, .045f, .12f).Actor("s2", ChronicleShape.Figure, Folk, .2f, .68f, .045f, .12f)
                .Actor("threshold", ChronicleShape.Rect, ChronicleColor.Emerald, .56f, .5f, .03f, .6f, "", .6f)
                .Anim("shatter_world", "", 6f, .6f)
                .Move("r", .5f, .64f, 6f).Move("s1", .42f, .68f, 6f).Move("s2", .46f, .68f, 6f)
                .Say("Somewhere beyond the broken roads, there was a place where tomorrow could follow today.")
                .Move("r", .78f, .64f, 4f).Move("s1", .7f, .68f, 4f).Move("s2", .74f, .68f, 4f)
                .Backdrop("plane", 3f).Anim("pulse", "threshold", 2.5f, .7f)
                .Say("They called it the Plane of Protection.")
                .Say("It did not promise to restore everything.")
                .Say("It promised only that something might endure.")
                .Say("And for the survivors, that was enough.", .6f);
            return End(b, 21);
        }

        // ---------------- CHAPTER VIII: THE UNFINISHED TALE ----------------

        static ChronicleScene M22() // Vanguard: a battle remembered twice
        {
            var b = Begin(22).Illustrate("battle").Hero("v", ChronicleHero.Vanguard, .14f, .64f)
                .Actor("foe", ChronicleShape.Shadow, ChronicleColor.Shade, .8f, .6f, .12f, .3f)
                .Move("v", .56f, .64f, 6f)
                .Say("The warrior remembered a battle he had not yet fought.")
                .Say("He knew the shape of a danger before it arrived.")
                .Anim("clash", "", 1.8f, .8f).Fade("v", 0f, 1.2f)
                .Backdrop("void", 1.2f)
                .Say("He had stood here before.")
                .Backdrop("battle", .8f).Move("v", .14f, .64f, .1f).Fade("v", 1f, .8f).Move("v", .56f, .64f, 5f)
                .Say("Or somewhere very much like here.")
                .Say("The world had forgotten the difference.", .6f);
            return End(b, 22);
        }

        static ChronicleScene M23() // Hexer: the anchor that refused
        {
            var b = Begin(23).Illustrate("knot")
                .Actor("knot", ChronicleShape.Circle, ChronicleColor.Being, .5f, .5f, .1f, .1f)
                .Actor("pathA", ChronicleShape.Line, ChronicleColor.Hexer, .26f, .3f, .3f, 0f).Actor("pathB", ChronicleShape.Line, ChronicleColor.Reaper, .74f, .34f, .3f, 0f)
                .Anim("branch", "", 5f, .8f)
                .Say("Some endings remained endings.")
                .Fade("pathA", 0f, 2.5f)
                .Say("Others did not.")
                .Say("The scholar found a connection that endured where the surrounding paths disappeared.")
                .Move("pathA", .26f, .62f, 3f).Fade("pathA", 1f, 3f)
                .Say("A knot in the Weave. An anchor.")
                .Anim("pulse", "knot", 3f, .9f).Anim("sigil_glow", "", 3f, .7f)
                .Say("Something was refusing to let their stories finish.", .6f);
            return End(b, 23);
        }

        static ChronicleScene M24() // Reaper: the ending that vanished. CORR_08
        {
            var b = Begin(24).Illustrate("page")
                .Actor("pageart", ChronicleShape.Page, ChronicleColor.Parchment, .5f, .5f, .8f, .8f, "the Chronicle")
                .Actor("ending", ChronicleShape.Glyph, ChronicleColor.Ink, .5f, .5f, .3f, .1f, "The End.", 0f)
                .Actor("pass", ChronicleShape.Shadow, ChronicleColor.Shade, -.2f, .5f, .3f, .9f, "", 0f)
                .Original("CORR_08")
                .Say("And there, at last, the tale came to—", .5f)
                .Fade("ending", 1f, 1.5f)
                .WriteBlock("end_word", "The End.", ChronicleTextStyle.Ending, .6f)
                .Anim("shake", "", 1.4f, .7f).Magic("book_tremor", .6f)
                .Fade("pass", .8f, .6f).Move("pass", 1.2f, .5f, 3f).Wait(.4f)
                .Deeper("Not yet.")
                .StrikeBlock("end_word", "CORR_08").Strike("CORR_08").Fade("ending", 0f, 1.5f)
                .Say("I did not write that.", .6f)
                .Corrected("CORR_08").Save("CORR_08")
                .Say("Then there is more.", .6f);
            return End(b, 24);
        }

        // ---------------- CHAPTER IX: THE FOURTH SHADOW ----------------

        static ChronicleScene M25() // Vanguard: the friend I forgot
        {
            var b = Begin(25).Illustrate("restored")
                .Actor("fire", ChronicleShape.Circle, new ChronicleColor(1f, .55f, .15f), .5f, .7f, .08f, .08f)
                .Hero("v", ChronicleHero.Vanguard, .28f, .62f).Hero("h", ChronicleHero.Hexer, .72f, .62f).Hero("r", ChronicleHero.Reaper, .5f, .5f)
                .Actor("f4", ChronicleShape.Figure, ChronicleColor.Fourth, .5f, .84f, .09f, .22f, "Fourth", 0f)
                .Anim("pulse", "fire", 8f, .5f).Fade("f4", 1f, 2f)
                .Say("The warrior remembered a friend.")
                .Say("Not a shadow. Not a missing name.")
                .Say("A person who laughed beside the fire, argued over impossible plans, and believed he could protect everyone.")
                .Say("Across every broken road, the warrior had carried the promise without remembering who made it.", .6f)
                .Turn(ChronicleSpreadLayout.IllustrationLeftTextRight).Illustrate("restored")
                .Hero("v", ChronicleHero.Vanguard, .28f, .62f).Actor("f4", ChronicleShape.Figure, ChronicleColor.Fourth, .66f, .64f, .09f, .22f, "Fourth")
                .Move("v", .44f, .62f, 3f)
                .Speak("v", "VANGUARD", "You were there.")
                .Speak("v", "VANGUARD", "I knew you were there.")
                .Speak("v", "VANGUARD", "And I let myself believe you were gone.")
                .Hesitate(1.2f)
                .Speak("v", "VANGUARD", "No.")
                .Speak("v", "VANGUARD", "Someone made me forget.");
            return End(b, 25);
        }

        static ChronicleScene M26() // Hexer: the handwriting of the lost
        {
            var b = Begin(26, ChronicleSpreadLayout.TextLeftIllustrationRight).Illustrate("journal")
                .Actor("old", ChronicleShape.Page, new ChronicleColor(.5f, .4f, .26f), .3f, .5f, .4f, .7f, "ancient journal")
                .Actor("new", ChronicleShape.Page, ChronicleColor.Parchment, .72f, .5f, .4f, .7f, "the Chronicle")
                .Say("The scholar found a page older than the breaking.")
                .Say("The ink was faded. The paper barely survived.")
                .Say("But the handwriting remained unmistakable.")
                .Move("old", .46f, .5f, 4f).Move("new", .56f, .5f, 4f).Anim("sigil_glow", "", 4f, .9f)
                .Say("That is...{p=1.0}", .6f)
                .Say("That cannot be.", .6f)
                .Turn(ChronicleSpreadLayout.IllustrationLeftTextRight).Illustrate("journal")
                .Hero("h", ChronicleHero.Hexer, .3f, .64f).Actor("old", ChronicleShape.Page, new ChronicleColor(.5f, .4f, .26f), .62f, .5f, .3f, .5f, "journal")
                .Speak("h", "HEXER", "I've seen this writing.")
                .Speak("h", "HEXER", "Not in the ruins.")
                .Speak("h", "HEXER", "Here.")
                .Speak("h", "HEXER", "In the book that's been telling our story.");
            return End(b, 26);
        }

        static ChronicleScene M27() // Reaper: the one left behind. CORR_09
        {
            var b = Begin(27).Illustrate("souls")
                .Actor("light", ChronicleShape.Circle, ChronicleColor.Parchment, .5f, .5f, .3f, .3f, "", .5f)
                .Actor("f4", ChronicleShape.Figure, ChronicleColor.Fourth, .2f, .64f, .09f, .24f, "Fourth")
                .Actor("being", ChronicleShape.Shadow, ChronicleColor.Shade, .8f, .5f, .2f, .5f, "", 0f)
                .Hero("r", ChronicleHero.Reaper, .08f, .7f, .8f)
                .Original("CORR_09")
                .Move("f4", .5f, .62f, 5f).Anim("expand_circle", "light", 4f, .8f)
                .Say("The fourth did not vanish into nothing.")
                .Fade("being", .8f, 2f).Move("being", .52f, .56f, 3f).Anim("overlap", "", 3f, .8f)
                .Say("He remained.")
                .Hesitate(.8f).Correct("CORR_09")
                .Turn(ChronicleSpreadLayout.IllustrationLeftTextRight).Illustrate("souls")
                .Actor("f4", ChronicleShape.Figure, ChronicleColor.Fourth, .5f, .62f, .09f, .24f).Actor("being", ChronicleShape.Shadow, ChronicleColor.Shade, .52f, .56f, .2f, .5f, "", .8f)
                .Hero("r", ChronicleHero.Reaper, .12f, .7f)
                .Say("Not as he had been. Not alone.")
                .Say("The traveler and the companion had become inseparable.")
                .Say("Two histories. Two wills. One unfinished existence.", .6f)
                .Speak("r", "REAPER", "We spent all this time believing he was gone.")
                .Speak("r", "REAPER", "But he never reached an ending.")
                .Speak("r", "REAPER", "He is still somewhere inside it.");
            return End(b, 27);
        }
    }
}
