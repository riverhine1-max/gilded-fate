namespace GildedFate.Chronicle
{
    // ACT I: BEFORE THE WORLD BROKE (Chapters I-III). Placeholder animation: rectangles, silhouettes and slides.
    public static partial class ChronicleScripts
    {
        static readonly ChronicleColor Stone = new ChronicleColor(.18f, .16f, .22f);
        static readonly ChronicleColor Folk = new ChronicleColor(.32f, .3f, .38f);

        static void RegisterActI()
        {
            Reg(1, M01); Reg(2, M02); Reg(3, M03); Reg(4, M04); Reg(5, M05); Reg(6, M06); Reg(7, M07); Reg(8, M08); Reg(9, M09);
        }

        // ---------------- CHAPTER I: BEFORE THE FIRST THREAD ----------------

        static ChronicleScene M01() // Vanguard: an ancient kingdom and his early connection to it
        {
            var b = Begin(1).Illustrate("kingdom")
                .Actor("castle", ChronicleShape.Castle, Stone, .72f, .42f, .34f, .5f)
                .Hero("v", ChronicleHero.Vanguard, .12f, .66f)
                .Actor("k1", ChronicleShape.Figure, Folk, .3f, .68f, .05f, .14f).Actor("k2", ChronicleShape.Figure, Folk, .38f, .68f, .05f, .13f)
                .Move("v", .56f, .66f, 9f).Move("k1", .62f, .68f, 9f).Move("k2", .66f, .68f, 9f)
                .Say("The oldest records describe a kingdom that believed itself permanent.")
                .Say("Stone could be repaired. Enemies could be defeated. A broken promise could be answered with another.")
                .Say("For one of its defenders, duty was simple: stand between danger and those who could not stand against it.")
                .Say("Yet the walls were not what failed.", .6f)
                .Anim("crack_pass", "", 1.8f, .6f);
            return End(b, 1);
        }

        static ChronicleScene M02() // Hexer: overlapping possible futures
        {
            var b = Begin(2).Illustrate("chamber")
                .Hero("h", ChronicleHero.Hexer, .5f, .62f)
                .Actor("g1", ChronicleShape.Figure, ChronicleColor.Hexer, .5f, .62f, .09f, .24f, "", 0f).Actor("g2", ChronicleShape.Figure, ChronicleColor.Hexer, .5f, .62f, .09f, .24f, "", 0f).Actor("g3", ChronicleShape.Figure, ChronicleColor.Hexer, .5f, .62f, .09f, .24f, "", 0f)
                .Say("Not every memory belongs to a life that was lived.")
                .Fade("g1", .4f, .8f).Fade("g2", .4f, .8f).Fade("g3", .4f, .8f)
                .Move("g1", .22f, .62f, 4f).Move("g2", .78f, .62f, 4f).Move("g3", .5f, .3f, 4f)
                .Say("Some are impressions of what might have happened.")
                .Say("The scholar believed these visions were calculations, possibilities arranged in the language of magic.")
                .Fade("g1", 0f, 2f).Fade("g3", 0f, 2f)
                .Say("But a calculation should not remember the person studying it.", .6f)
                .Move("g2", .62f, .62f, 2f).Anim("pulse", "g2", 1.6f, .8f).Fade("g2", 0f, 2f);
            return End(b, 2);
        }

        static ChronicleScene M03() // Reaper: echoes of discarded lives
        {
            var b = Begin(3).Illustrate("field").Hero("r", ChronicleHero.Reaper, .08f, .64f);
            for (var i = 1; i <= 5; i++) b.Actor("s" + i, ChronicleShape.Shadow, ChronicleColor.Shade, .18f + i * .14f, .66f, .07f, .2f, "", .55f);
            b.Move("r", .9f, .64f, 11f)
                .Say("History is often measured by what remains.")
                .Fade("s1", 0f, 2f).Fade("s2", 0f, 2.5f)
                .Say("A name carved in stone. A voice remembered. A road worn beneath countless feet.")
                .Fade("s3", 0f, 2f)
                .Say("But what becomes of a life when the road leading to it is never taken?")
                .Say("The Reaper found echoes that no record could explain.")
                .Say("And some of them seemed unwilling to disappear.", .6f)
                .Anim("pulse", "s5", 1.4f, .8f).Fade("s5", 0f, 3f).Fade("s4", 0f, 3f);
            return End(b, 3);
        }

        // ---------------- CHAPTER II: THE TRAVELER BEYOND TIME ----------------

        static ChronicleScene M04() // Vanguard: the ancient traveler welcomed
        {
            var b = Begin(4).Illustrate("gate")
                .Actor("gate", ChronicleShape.Castle, Stone, .8f, .44f, .3f, .5f)
                .Hero("v", ChronicleHero.Vanguard, .7f, .66f)
                .Actor("traveler", ChronicleShape.Figure, ChronicleColor.Being, -.02f, .67f, .08f, .22f)
                .Actor("c1", ChronicleShape.Figure, Folk, .5f, .68f, .05f, .14f).Actor("c2", ChronicleShape.Figure, Folk, .58f, .68f, .05f, .13f).Actor("c3", ChronicleShape.Figure, Folk, .64f, .69f, .05f, .12f)
                .Actor("tower", ChronicleShape.Rect, Stone, .94f, .3f, .05f, .5f, "", 0f)
                .Move("traveler", .44f, .67f, 9f)
                .Say("He arrived with no army. No crown. No demand for obedience.")
                .Move("c1", .38f, .68f, 3f).Move("c2", .42f, .68f, 3f).Move("c3", .46f, .69f, 3f)
                .Say("He offered answers to questions no one had yet learned to ask.")
                .Say("And when his warnings proved true, the kingdom opened its gates to him.")
                .Say("Trust is rarely given all at once.")
                .Say("Sometimes it grows so naturally that no one remembers deciding to give it.", .6f)
                .Fade("tower", .5f, 2f).Anim("pulse", "tower", 2f, .6f);
            return End(b, 4);
        }

        static ChronicleScene M05() // Hexer: the traveler was merging futures. CORR_02
        {
            var b = Begin(5).Illustrate("observatory")
                .Actor("traveler", ChronicleShape.Figure, ChronicleColor.Being, .22f, .62f, .08f, .22f)
                .Hero("h", ChronicleHero.Hexer, .72f, .64f)
                .Actor("pathA", ChronicleShape.Line, ChronicleColor.Hexer, .5f, .3f, .4f, .0f).Actor("pathB", ChronicleShape.Line, ChronicleColor.Reaper, .5f, .45f, .4f, .0f)
                .Anim("branch", "", 4f, .8f)
                .Original("CORR_02")
                .Say("A prediction describes a door. An intervention opens it.")
                .Move("pathA", .5f, .45f, 5f)
                .Hesitate(.8f)
                .Anim("flash", "", .5f, .7f)
                .Correct("CORR_02")
                .Say("He was taking doors that led to different destinations and insisting they should open into the same room.")
                .Say("To him, this was mercy.")
                .Say("To the scholar, it was a question without a safe answer.");
            return End(b, 5);
        }

        static ChronicleScene M06() // Reaper: discarded futures had a cost. CORR_03
        {
            var b = Begin(6).Illustrate("split");
            for (var i = 0; i < 3; i++)
            {
                b.Actor("a" + i, ChronicleShape.Figure, ChronicleColor.Reaper, .12f + i * .08f, .64f, .05f, .15f);
                b.Actor("b" + i, ChronicleShape.Figure, ChronicleColor.Being, .68f + i * .08f, .64f, .05f, .15f);
            }
            b.Original("CORR_03")
             .Say("The traveler counted possibilities. The Reaper counted what those possibilities left behind.")
             .Fade("a0", .2f, 3f).Fade("a1", .2f, 3f).Fade("a2", .2f, 3f).Anim("echo_ring", "a1", 3f, .7f)
             .Hesitate(.8f).Correct("CORR_03")
             .Say("There is a difference between refusing to forget someone and refusing to let them go.")
             .Say("The traveler spoke of a world where no future would ever be abandoned.")
             .Say("But the abandoned were already there.", .6f);
            return End(b, 6);
        }

        // ---------------- CHAPTER III: THREE AGAINST THE END ----------------

        /// <summary>The First Correction demonstration (also reachable from the editor tool before any unlock).</summary>
        static ChronicleScene M07() // Vanguard: the campfire. CORR_01
        {
            var b = Begin(7).Illustrate("campfire")
                .Actor("fire", ChronicleShape.Circle, new ChronicleColor(1f, .55f, .15f), .5f, .7f, .08f, .08f)
                .Hero("v", ChronicleHero.Vanguard, .3f, .62f).Hero("h", ChronicleHero.Hexer, .7f, .62f).Hero("r", ChronicleHero.Reaper, .5f, .5f)
                .Actor("empty", ChronicleShape.Rect, ChronicleColor.Parchment, .5f, .84f, .09f, .2f, "", .12f)
                .Actor("shade4", ChronicleShape.Shadow, ChronicleColor.Shade, .5f, .8f, .09f, .22f, "", 0f)
                .Anim("pulse", "fire", 6f, .5f)
                .Say("Three travelers prepared to challenge the darkness.")
                .Say("They had little certainty and even less agreement.")
                .Say("But they had one another.")
                .Original("CORR_01")
                .Fade("shade4", .7f, 2.5f).Anim("pulse", "shade4", 2.5f, .7f)
                .Say("Wait.", .6f)
                .Say("The fire cast another shadow.")
                .Hesitate(1.2f)
                .Say("That cannot be correct.{p=0.9} Unless...", .6f)
                .Correct("CORR_01").Magic("red_ink_glow", .6f);
            return End(b, 7);
        }

        static ChronicleScene M08() // Hexer: the missing signature
        {
            var b = Begin(8).Illustrate("diagram")
                .Actor("sv", ChronicleShape.Glyph, ChronicleColor.Vanguard, .25f, .7f, .1f, .1f).Actor("sh", ChronicleShape.Glyph, ChronicleColor.Hexer, .75f, .7f, .1f, .1f).Actor("sr", ChronicleShape.Glyph, ChronicleColor.Reaper, .5f, .25f, .1f, .1f)
                .Actor("s4", ChronicleShape.Glyph, ChronicleColor.Fourth, .5f, .52f, .1f, .1f, "", 0f)
                .If("!fact:FACT_FOURTH_COMPANION").Say("Another signature appears in the diagram. Its owner is unknown.").EndIf()
                .If("fact:FACT_FOURTH_COMPANION").Say("The missing signature belongs to the fourth companion.").EndIf()
                .Say("Three identities. Three histories. Three strands...{p=0.8}")
                .Fade("s4", .6f, 3f).Anim("pulse", "s4", 3f, .6f)
                .Say("No. The pattern was never made for three.")
                .Say("Something has been removed.")
                .Say("Not destroyed. Removed from memory.", .6f)
                .Fade("s4", 1f, 1.5f).Anim("sigil_glow", "", 3f, .9f).Magic("four_strands", .6f)
                .If("!corr:CORR_01").Turn(ChronicleSpreadLayout.TextOnly).EndIf()
                .LateFourthCorrection();
            return End(b, 8);
        }

        static ChronicleScene M09() // Reaper: four shadows
        {
            var b = Begin(9).Illustrate("passage")
                .Hero("v", ChronicleHero.Vanguard, .12f, .62f).Hero("h", ChronicleHero.Hexer, .24f, .62f).Hero("r", ChronicleHero.Reaper, .36f, .62f)
                .Actor("sv", ChronicleShape.Shadow, ChronicleColor.Shade, .12f, .8f, .09f, .06f, "", .7f).Actor("sh", ChronicleShape.Shadow, ChronicleColor.Shade, .24f, .8f, .09f, .06f, "", .7f)
                .Actor("sr", ChronicleShape.Shadow, ChronicleColor.Shade, .36f, .8f, .09f, .06f, "", .7f).Actor("s4", ChronicleShape.Shadow, ChronicleColor.Fourth, .0f, .8f, .09f, .06f, "", .8f)
                .Move("v", .56f, .62f, 12f).Move("h", .68f, .62f, 12f).Move("r", .8f, .62f, 12f)
                .Move("sv", .56f, .8f, 12f).Move("sh", .68f, .8f, 12f).Move("sr", .8f, .8f, 12f).Move("s4", .44f, .8f, 12f)
                .Say("An echo can imitate a voice. A discarded future can imitate a life.")
                .Say("But a shadow must belong to something.")
                .Say("Four shadows crossed the threshold.")
                .Say("Four companions entered.")
                .Say("Only three were remembered.", .6f)
                .Anim("pulse", "s4", 1.6f, .8f)
                .If("!corr:CORR_01").Turn(ChronicleSpreadLayout.TextOnly).EndIf()
                .LateFourthCorrection();
            return End(b, 9);
        }
    }
}
