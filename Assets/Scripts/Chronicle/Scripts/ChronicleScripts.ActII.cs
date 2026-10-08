namespace GildedFate.Chronicle
{
    // ACT II: THE LAST PROMISE (Chapters IV-VI). The sanctuary, the plan, and the catastrophe.
    public static partial class ChronicleScripts
    {
        static void RegisterActII()
        {
            Reg(10, M10); Reg(11, M11); Reg(12, M12); Reg(13, M13); Reg(14, M14); Reg(15, M15); Reg(16, M16); Reg(17, M17); Reg(18, M18);
        }

        // ---------------- CHAPTER IV: BENEATH THE KINGDOM ----------------

        static ChronicleScene M10() // Vanguard: the watching sanctuary. CORR_04
        {
            var b = Begin(10).Illustrate("sanctuary")
                .Hero("v", ChronicleHero.Vanguard, .08f, .64f);
            for (var i = 0; i < 4; i++) b.Actor("eye" + i, ChronicleShape.Eye, ChronicleColor.Emerald, .35f + i * .14f, .24f + (i % 2) * .1f, .07f, .04f, "", 0f);
            b.Original("CORR_04")
             .Say("It waited.")
             .Move("v", .5f, .64f, 8f)
             .Fade("eye0", 1f, 1.2f).Fade("eye1", 1f, 1.6f).Fade("eye2", 1f, 2f).Fade("eye3", 1f, 2.4f).Audio("magic_hum", .5f)
             .Hesitate(.7f).Correct("CORR_04")
             .Say("Every door opened as though it already knew who approached.")
             .Say("The warrior had faced enemies who could predict his movements.")
             .Say("He had never faced a room that appeared to remember him before he entered.", .6f);
            return End(b, 10);
        }

        static ChronicleScene M11() // Hexer: the thread between minds
        {
            var b = Begin(11).Illustrate("threads")
                .Actor("traveler", ChronicleShape.Figure, ChronicleColor.Being, .5f, .3f, .08f, .2f, "", .7f)
                .Hero("v", ChronicleHero.Vanguard, .16f, .66f).Hero("h", ChronicleHero.Hexer, .38f, .66f).Hero("r", ChronicleHero.Reaper, .62f, .66f)
                .Fourth("f4", .84f, .66f)
                .Anim("threads", "", 5f, .9f)
                .Say("Memory was more than recollection.")
                .Say("Within the sanctuary, it had become a map.")
                .Say("A person could be found through the things they remembered.")
                .Move("f4", .96f, .78f, 4f).Fade("f4", .4f, 4f)
                .Say("And a connection could perhaps be broken by removing the memory that sustained it.")
                .Say("Perhaps.", .6f)
                .Say("That uncertainty should have been enough to stop them.");
            return End(b, 11);
        }

        static ChronicleScene M12() // Reaper: forgotten futures trapped beneath the kingdom
        {
            var b = Begin(12).Illustrate("fading").Hero("r", ChronicleHero.Reaper, .5f, .64f);
            for (var i = 0; i < 4; i++) b.Actor("w" + i, ChronicleShape.Rect, ChronicleColor.Parchment, .16f + i * .22f, .3f, .16f, .2f, "", 0f);
            b.Say("The sanctuary held more than machines and symbols.")
             .Fade("w0", .4f, 1f).Fade("w1", .4f, 1.4f).Fade("w2", .4f, 1.8f).Fade("w3", .4f, 2.2f)
             .Say("It held consequences.")
             .Say("Every thread seemed attached to something that had mattered.")
             .Say("The Reaper understood then why the traveler feared letting any of them go.")
             .Fade("w0", .1f, 3f).Fade("w2", .1f, 3f).Fade("w3", .1f, 3f)
             .Say("But fear of loss had built a prison from the things he wished to save.", .6f);
            return End(b, 12);
        }

        // ---------------- CHAPTER V: THE LAST PROMISE ----------------

        static ChronicleScene M13() // Vanguard: the argument before dawn
        {
            var b = Begin(13).Illustrate("sanctuary")
                .Hero("v", ChronicleHero.Vanguard, .28f, .64f).Hero("h", ChronicleHero.Hexer, .12f, .64f).Hero("r", ChronicleHero.Reaper, .2f, .64f)
                .Fourth("f4", .56f, .64f)
                .Actor("core", ChronicleShape.Circle, ChronicleColor.Emerald, .88f, .5f, .1f, .1f)
                .FourthIntro()
                .Say("The warrior demanded a battle they could face together.")
                .Say("The fourth offered a victory that would leave no one certain it had happened.")
                .Move("v", .46f, .64f, 4f).Move("f4", .74f, .64f, 4f)
                .Speak("v", "VANGUARD", "Then we face him together.")
                .Speak("f4", "FOURTH COMPANION", "Together is exactly how he finds us.")
                .Turn(ChronicleSpreadLayout.IllustrationLeftTextRight).Illustrate("sanctuary")
                .Hero("v", ChronicleHero.Vanguard, .46f, .64f).Fourth("f4", .74f, .64f)
                .Speak("v", "VANGUARD", "You don't get to decide that for everyone.")
                .Speak("f4", "FOURTH COMPANION", "I know.")
                .Say("They did not disagree about whom to save.")
                .Say("They disagreed about who had the right to bear the cost.", .6f);
            return End(b, 13);
        }

        static ChronicleScene M14() // Hexer: the impossible solution. CORR_05
        {
            var b = Begin(14).Illustrate("diagram")
                .Hero("h", ChronicleHero.Hexer, .16f, .64f).Fourth("f4", .5f, .64f)
                .Actor("core", ChronicleShape.Circle, ChronicleColor.Emerald, .84f, .42f, .12f, .12f)
                .Actor("l1", ChronicleShape.Line, ChronicleColor.Vanguard, .62f, .3f, .3f, 0f).Actor("l2", ChronicleShape.Line, ChronicleColor.Hexer, .62f, .42f, .3f, 0f).Actor("l3", ChronicleShape.Line, ChronicleColor.Reaper, .62f, .54f, .3f, 0f)
                .FourthIntro()
                .Say("The scholar understood the design. He could follow every line of reasoning.")
                .Say("He could also see the place where reason ended.")
                .Fade("l1", 0f, 1.5f).Fade("l2", 0f, 2.5f).Fade("l3", 0f, 3.5f).Anim("shake", "core", 3.5f, .7f)
                .Turn(ChronicleSpreadLayout.IllustrationLeftTextRight).Illustrate("diagram")
                .Hero("h", ChronicleHero.Hexer, .16f, .64f).Fourth("f4", .5f, .64f)
                .Actor("core", ChronicleShape.Circle, ChronicleColor.Emerald, .84f, .42f, .12f, .12f).Anim("shake", "core", 4f, .8f)
                .Original("CORR_05")
                .Say("The plan required certainty from a power none of them fully understood.")
                .Say("Yet the fourth had already decided.")
                .Speak("h", "HEXER", "You know what happens if the connection refuses to break.")
                .Speak("f4", "FOURTH COMPANION", "No. I know what happens if we do nothing.")
                .Hesitate(.8f).Correct("CORR_05");
            return End(b, 14);
        }

        static ChronicleScene M15() // Reaper: a promise without witnesses
        {
            var b = Begin(15).Illustrate("sanctuary")
                .Fourth("f4", .3f, .64f)
                .Hero("v", ChronicleHero.Vanguard, .62f, .64f).Hero("h", ChronicleHero.Hexer, .74f, .64f).Hero("r", ChronicleHero.Reaper, .86f, .64f)
                .FourthIntro()
                .Say("There are promises made to be heard.")
                .Say("And promises made because the speaker needs to believe them.")
                .Say("He told them he would remember.")
                .Fade("v", .1f, 9f).Fade("h", .1f, 9f).Fade("r", .1f, 9f)
                .Say("Even if every path between them vanished.")
                .Say("Even if the world forgot their names.")
                .Say("He would remember.", .6f)
                .Hesitate(2f)
                .Say("Someone should have asked whether that was possible.", .8f);
            return End(b, 15);
        }

        // ---------------- CHAPTER VI: THE DAY FATE SHATTERED ----------------

        static ChronicleScene M16() // Vanguard: run while you can
        {
            var b = Begin(16).Illustrate("collapse")
                .Fourth("f4", .7f, .62f).Hero("v", ChronicleHero.Vanguard, .16f, .64f)
                .Actor("barrier", ChronicleShape.Rect, ChronicleColor.Emerald, .5f, .5f, .02f, .6f, "", 0f)
                .FourthIntro()
                .Say("The warrior turned back.")
                .Move("v", .46f, .64f, 3f).Fade("barrier", .8f, 1.5f).Anim("shatter_world", "", 6f, .7f)
                .Say("He would remember that movement long after forgetting why he made it.")
                .Speak("v", "VANGUARD", "I'm not leaving you!")
                .Move("v", .22f, .64f, 1.4f).Anim("shake", "", 1.4f, .8f)
                .Speak("f4", "FOURTH COMPANION", "You have to!")
                .Turn(ChronicleSpreadLayout.IllustrationLeftTextRight).Illustrate("collapse").Fourth("f4", .7f, .62f).Hero("v", ChronicleHero.Vanguard, .22f, .64f)
                .Anim("shatter_world", "", 3f, .9f)
                .Say("He heard an order to run. He resisted.")
                .Speak("v", "VANGUARD", "We made a promise!")
                .Speak("f4", "FOURTH COMPANION", "Then live long enough to break it.")
                .Say("And somewhere behind him, a voice called out with the certainty of someone who knew there would be no second chance.", .6f)
                .Fade("v", 0f, 3f);
            return End(b, 16);
        }

        static ChronicleScene M17() // Hexer: the breaking of the Weave. CORR_06
        {
            var b = Begin(17).Illustrate("weave")
                .Actor("w1", ChronicleShape.Line, ChronicleColor.Hexer, .5f, .3f, .8f, 0f).Actor("w2", ChronicleShape.Line, ChronicleColor.Being, .5f, .5f, .8f, 0f).Actor("w3", ChronicleShape.Line, ChronicleColor.Reaper, .5f, .7f, .8f, 0f)
                .Original("CORR_06")
                .Anim("threads", "", 5f, .9f)
                .Say("They sought to sever a handful of threads.")
                .Say("But the threads were tied to everything.")
                .Move("w1", .3f, .3f, 3f).Move("w3", .7f, .7f, 3f)
                .Say("One hand pulled them apart. Another refused to let them go.")
                .Say("The Weave could not obey both.")
                .Anim("fracture", "", 3f, 1f).Anim("flash", "", .6f, .8f).Backdrop("collapse", 2f).Anim("shatter_world", "", 4f, .9f)
                .Say("And so it broke.", .8f)
                .Hesitate(.8f).Correct("CORR_06");
            return End(b, 17);
        }

        static ChronicleScene M18() // Reaper: two voices in the dark
        {
            var b = Begin(18).Illustrate("souls")
                .Fourth("mortal", .3f, .64f).Actor("being", ChronicleShape.Shadow, ChronicleColor.Shade, .72f, .5f, .22f, .55f)
                .Say("Two wills entered the breaking.")
                .Move("mortal", .44f, .62f, 5f).Move("being", .56f, .54f, 5f)
                .Say("Neither emerged alone.")
                .Anim("clash", "mortal", 3f, .6f)
                .Say("One remembered too much. One would remember too little.")
                .Anim("overlap", "", 3f, .8f).Move("mortal", .5f, .6f, 3f).Move("being", .5f, .56f, 3f)
                .Say("And between them, a promise disappeared.", .6f)
                .Turn(ChronicleSpreadLayout.IllustrationLeftTextRight).Illustrate("souls")
                .Actor("merged", ChronicleShape.Shadow, ChronicleColor.Shade, .5f, .58f, .24f, .55f).Actor("spark", ChronicleShape.Mote, ChronicleColor.Being, .5f, .5f, .05f, .05f)
                .Deeper("Nothing was meant to be lost.")
                .Say("Who said that?", .8f)
                .Hesitate(1.6f);
            return End(b, 18);
        }
    }
}
