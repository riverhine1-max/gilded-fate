// GENERATED from SourceAssets/ChronicleAscension (Prompt 3 content) by the integration generator. Every journal string is verbatim
// from Scenes/*.json (SHA-256 checked); narrated blocks bind to recordings by scene id + block key + exact text.
// To change wording, edit the source package, re-record and re-time that line, then regenerate. Original Chronicle content is untouched.
namespace GildedFate.Chronicle
{
    public static partial class ChronicleScripts
    {
        static void RegisterAscension()
        {
            factories["ASC_HEX_01"] = ASC_HEX_01;
            factories["ASC_HEX_02"] = ASC_HEX_02;
            factories["ASC_HEX_03"] = ASC_HEX_03;
            factories["ASC_HEX_04"] = ASC_HEX_04;
            factories["ASC_HEX_05"] = ASC_HEX_05;
            factories["ASC_HEX_06"] = ASC_HEX_06;
            factories["ASC_HEX_07"] = ASC_HEX_07;
            factories["ASC_HEX_08"] = ASC_HEX_08;
            factories["ASC_HEX_09"] = ASC_HEX_09;
            factories["ASC_HEX_10"] = ASC_HEX_10;
            factories["ASC_REA_01"] = ASC_REA_01;
            factories["ASC_REA_02"] = ASC_REA_02;
            factories["ASC_REA_03"] = ASC_REA_03;
            factories["ASC_REA_04"] = ASC_REA_04;
            factories["ASC_REA_05"] = ASC_REA_05;
            factories["ASC_REA_06"] = ASC_REA_06;
            factories["ASC_REA_07"] = ASC_REA_07;
            factories["ASC_REA_08"] = ASC_REA_08;
            factories["ASC_REA_09"] = ASC_REA_09;
            factories["ASC_REA_10"] = ASC_REA_10;
            factories["ASC_VAN_01"] = ASC_VAN_01;
            factories["ASC_VAN_02"] = ASC_VAN_02;
            factories["ASC_VAN_03"] = ASC_VAN_03;
            factories["ASC_VAN_04"] = ASC_VAN_04;
            factories["ASC_VAN_05"] = ASC_VAN_05;
            factories["ASC_VAN_06"] = ASC_VAN_06;
            factories["ASC_VAN_07"] = ASC_VAN_07;
            factories["ASC_VAN_08"] = ASC_VAN_08;
            factories["ASC_VAN_09"] = ASC_VAN_09;
            factories["ASC_VAN_10"] = ASC_VAN_10;
        }

        /// <summary>A recollection continues an existing memory on fresh paper: the same chapter line the memory opened with,
        /// then its title. The marginal study (when it has one) sits on the facing page as a small static detail.</summary>
        static RecollectionWriter BeginRecollection(string id)
        {
            var d = ChronicleAscensionCatalog.Find(id); var art = !string.IsNullOrEmpty(d.marginalArt);
            var w = new RecollectionWriter(new ChronicleSceneBuilder(d.id, d.title).Open(art ? ChronicleSpreadLayout.IllustrationLeftTextRight : ChronicleSpreadLayout.TextOnly), !art);
            w.Write("", "CHAPTER " + Roman(d.chapter) + "  \u00b7  " + ChronicleCatalog.HeroName(d.hero).ToUpperInvariant(), ChronicleTextStyle.Note, .15f);
            w.Write("", d.title, ChronicleTextStyle.Heading, .5f);
            return w;
        }

        static ChronicleScene EndRecollection(ChronicleSceneBuilder b) => b.Wait(.6f).Finish();

        /// <summary>Lays a recollection out the way the original memories are authored: passages fill the page and an authored
        /// page turn begins fresh paper when the next passage will not fit (measured with the sequencer's own layout). A mistaken
        /// observation keeps room beside it for its red-ink correction. Text is never shrunk; the player's text size still reflows it.</summary>
        sealed class RecollectionWriter
        {
            public readonly ChronicleSceneBuilder Builder; readonly ChroniclePageMetrics m = new ChroniclePageMetrics(); float left, right;
            public RecollectionWriter(ChronicleSceneBuilder b, bool twoSides) { Builder = b; left = twoSides ? m.textHeight : 0f; right = m.textHeight; }
            float H(string t, ChronicleTextStyle s) => ChronicleLayout.HeightOf(ChronicleMarkup.Parse(t, new System.Collections.Generic.List<ChroniclePause>()), m, s);
            bool Take(float h) { if (left >= h) { left -= h; return true; } if (right >= h) { right -= h; return true; } return false; }
            public void Write(string key, string text, ChronicleTextStyle style, float beat, string keepWith = null)
            {
                var h = H(text, style); var need = keepWith == null ? h : h + H(keepWith, ChronicleTextStyle.Correction);
                if (!(left >= need || right >= need)) { Builder.Turn(ChronicleSpreadLayout.TextOnly); left = right = m.textHeight; }
                if (keepWith != null) { if (left >= need) left -= h; else right -= h; } else Take(h);
                if (string.IsNullOrEmpty(key)) Builder.Write(text, style, beat); else Builder.WriteBlock(key, text, style, beat);
            }
        }

        // The Scholar's Mistake · Hexer · Fate Debt 1 · expands MEM_02 (chapter 1). Source: Scenes/Hexer_Debt01_The_Scholar_s_Mistake.json
        static ChronicleScene ASC_HEX_01()
        {
            var w = BeginRecollection("ASC_HEX_01"); var b = w.Builder;
            w.Write("context", "The scholar's first comparison used two impressions of the same unfinished day. In one, a visitor arrived before sunset. In the other, the gate remained empty. After the traveler demonstrated his method, only the first impression could be summoned clearly.", ChronicleTextStyle.Narration, .35f);   // silent, part 1 of 2
            w.Write("context_2", "The scholar entered the result among his successes.", ChronicleTextStyle.Narration, .7f);   // silent, part 2 of 2
            w.Write("new_observation", "The traveler's successful prediction proved that the future had always been fixed.", ChronicleTextStyle.Narration, .7f, keepWith: "His intervention had removed the future that would have disproved the prediction.");   // narrated
            b.Hesitate(.8f).StrikeBlock("new_observation", "ASC_CORR_HEX_1");   // only this addition's own mistaken observation
            w.Write("replacement", "His intervention had removed the future that would have disproved the prediction.", ChronicleTextStyle.Correction, .7f); b.Save("ASC_CORR_HEX_1");
            w.Write("evidence", "The recovered notes contain the second impression before the demonstration. Its absence afterward was treated as an error in the earlier calculation. Yet the comparison had changed while it was being tested.", ChronicleTextStyle.Narration, .35f);   // silent, part 1 of 2
            w.Write("evidence_2", "The scholar had accepted the surviving answer without accounting for the answer that could no longer object.", ChronicleTextStyle.Narration, .7f);   // silent, part 2 of 2
            w.Write("account", "This was more than mistaking an intervention for observation. He had allowed the method to decide what counted as evidence of its own success. The traveler could be genuinely helpful and still make that test dishonest. A result remained; the grounds upon which it might be questioned had been lost.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("closing", "The first mistake was not a failed calculation. It was accepting a test that could discard its own counterexample.", ChronicleTextStyle.Narration, .9f);   // narrated
            return EndRecollection(b);
        }

        // The First Calculation · Hexer · Fate Debt 2 · expands MEM_05 (chapter 2). Source: Scenes/Hexer_Debt02_The_First_Calculation.json
        static ChronicleScene ASC_HEX_02()
        {
            var w = BeginRecollection("ASC_HEX_02"); var b = w.Builder;
            w.Write("paragraph_1", "Beside the first calculation, the scholar had left an interval instead of a single answer. It allowed for choices he had not observed: a delayed departure, a changed intention, someone deciding against the road before them. Later he copied the neat result and left the interval behind.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("paragraph_2", "The discarded marks had been part of his understanding, not a blemish upon it. He recovered them before he attempted the calculation again. The page now keeps the uncertainty beside the answer it qualifies.", ChronicleTextStyle.Narration, .9f);   // silent
            return EndRecollection(b);
        }

        // The Unexplained Signature · Hexer · Fate Debt 3 · expands MEM_08 (chapter 3). Source: Scenes/Hexer_Debt03_The_Unexplained_Signature.json
        static ChronicleScene ASC_HEX_03()
        {
            var w = BeginRecollection("ASC_HEX_03"); var b = w.Builder;
            w.Write("paragraph_1", "The fourth signature occupied an unusual place in the old diagram. The other marks sat beside the paths being traced. This one stood beside an empty comparison space. A narrow return stroke joined it to the place where an observation could be entered after the work was done.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("paragraph_2", "The missing participant had not merely supplied another strand. He had been assigned to check what the diagram claimed against what someone actually experienced.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("closing", "Removing that signature had also removed a place from which the plan could be questioned.", ChronicleTextStyle.Narration, .9f);   // narrated
            return EndRecollection(b);
        }

        // The Warning He Remembered Wrong · Hexer · Fate Debt 4 · expands MEM_14 (chapter 5). Source: Scenes/Hexer_Debt04_The_Warning_He_Remembered_Wrong.json
        static ChronicleScene ASC_HEX_04()
        {
            var w = BeginRecollection("ASC_HEX_04"); var b = w.Builder;
            w.Write("context", "The scholar recovered a second sheet beneath the proposed memory erasure. It listed the observations needed to tell whether a connection had weakened. Several depended upon recollections the plan intended to remove.", ChronicleTextStyle.Narration, .35f);   // silent, part 1 of 2
            w.Write("context_2", "He had seen the contradiction, then remembered it afterward as a warning about incomplete execution.", ChronicleTextStyle.Narration, .7f);   // silent, part 2 of 2
            w.Write("new_observation", "The warning meant that sufficiently complete memory loss would end the danger.", ChronicleTextStyle.Narration, .7f, keepWith: "Complete memory loss could also remove the evidence needed to discover that the danger remained.");   // narrated
            b.Hesitate(.8f).StrikeBlock("new_observation", "ASC_CORR_HEX_2");   // only this addition's own mistaken observation
            w.Write("replacement", "Complete memory loss could also remove the evidence needed to discover that the danger remained.", ChronicleTextStyle.Correction, .7f); b.Save("ASC_CORR_HEX_2");
            w.Write("evidence", "A blank measurement would not establish a broken connection if the person making it could no longer recognize what had been measured. The scholar's question at the sanctuary was larger than he later recalled. It concerned both the failure of the plan and their ability to know whether it had failed.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("account", "The fourth companion's decision remains his own. Urgency did not become certainty merely because there was little time. The scholar had understood a possible route out of the danger; he had not established a safe way to verify its result. Losing the witness was itself one of the costs.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("closing", "He had warned of more than a thread refusing to break. He had warned of no longer knowing where to look.", ChronicleTextStyle.Narration, .9f);   // narrated
            return EndRecollection(b);
        }

        // The Memory Threads · Hexer · Fate Debt 5 · expands MEM_11 (chapter 4). Source: Scenes/Hexer_Debt05_The_Memory_Threads.json
        static ChronicleScene ASC_HEX_05()
        {
            var w = BeginRecollection("ASC_HEX_05"); var b = w.Builder;
            w.Write("paragraph_1", "One notation in the sanctuary gave a remembered person a single mark. Beside it, the scholar had listed several things that made recognition possible: a name, a habitual movement, an argument left unfinished. When the name faded from one recollection, the movement remained.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("paragraph_2", "He had treated the mark as though it measured one indivisible thing. This example showed otherwise. The connection could not be understood by counting names alone. He retained the separate observations instead of forcing them back into the neat symbol.", ChronicleTextStyle.Narration, .9f);   // silent
            return EndRecollection(b);
        }

        // An Incomplete Answer · Hexer · Fate Debt 6 · expands MEM_14 (chapter 5). Source: Scenes/Hexer_Debt06_An_Incomplete_Answer.json
        static ChronicleScene ASC_HEX_06()
        {
            var w = BeginRecollection("ASC_HEX_06"); var b = w.Builder;
            w.Write("paragraph_1", "There was an unfinished calculation on the reverse of the plan. The scholar had described an opening through the connected memories, then left the final condition empty. It was not a missing quantity.", ChronicleTextStyle.Narration, .35f);   // silent, part 1 of 2
            w.Write("paragraph_1_2", "It asked whether the people carried through that opening would understand what was being taken from them.", ChronicleTextStyle.Narration, .7f);   // silent, part 2 of 2
            w.Write("paragraph_2", "He could estimate a change in the pattern. The empty condition concerned people who would have to live with that change.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("closing", "He could calculate an opening. He could not calculate consent on another person's behalf.", ChronicleTextStyle.Narration, .9f);   // narrated
            return EndRecollection(b);
        }

        // The Broken Sequence · Hexer · Fate Debt 7 · expands MEM_17 (chapter 6). Source: Scenes/Hexer_Debt07_The_Broken_Sequence.json
        static ChronicleScene ASC_HEX_07()
        {
            var w = BeginRecollection("ASC_HEX_07"); var b = w.Builder;
            w.Write("context", "Two surviving studies placed the same fracture on opposite sides of a remembered movement. In one, the hand withdrew before the line divided. In the other, the divided line was already beneath it. The scholar had once chosen the cleaner sequence and treated the second as a damaged copy.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("new_observation", "The last severed thread marked the first moment of the catastrophe.", ChronicleTextStyle.Narration, .7f, keepWith: "The surviving records preserve incompatible orders; neither establishes a single first moment.");   // narrated
            b.Hesitate(.8f).StrikeBlock("new_observation", "ASC_CORR_HEX_3");   // only this addition's own mistaken observation
            w.Write("replacement", "The surviving records preserve incompatible orders; neither establishes a single first moment.", ChronicleTextStyle.Correction, .7f); b.Save("ASC_CORR_HEX_3");
            w.Write("evidence", "The conflict remained recognizable in both studies: a connection pulled apart, and a will refusing release. What failed to agree was the ordering of the marks around it. After the breaking, even the account of the breaking carried damaged time. The easier sequence was not thereby the truer one.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("account", "This does not turn responsibility into an unknowable mystery. The dangerous choices and the refusal to heed uncertainty remain. It does prevent a diagram from assigning all blame to whichever movement can be placed last. A record may preserve a cause without preserving a trustworthy clock for it.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("closing", "The conflict was recoverable. The order was not. He had mistaken a tidy sequence for a complete explanation.", ChronicleTextStyle.Narration, .9f);   // narrated
            return EndRecollection(b);
        }

        // The Hour That Returned · Hexer · Fate Debt 8 · expands MEM_20 (chapter 7). Source: Scenes/Hexer_Debt08_The_Hour_That_Returned.json
        static ChronicleScene ASC_HEX_08()
        {
            var w = BeginRecollection("ASC_HEX_08"); var b = w.Builder;
            w.Write("paragraph_1", "A familiar chime returned while the scholar was comparing his notes. The sound was the same. The cup beside him was warmer than his earlier mark recorded, and a fold in the page lay the other way. He could not establish whether the hour had repeated or two paths had approached the same event.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("paragraph_2", "For once he preserved the disagreement. He wrote down the warmth, the fold, and the uncertain order. A recurring sign was no longer enough to erase everything that differed around it.", ChronicleTextStyle.Narration, .9f);   // silent
            return EndRecollection(b);
        }

        // The Persistent Thread · Hexer · Fate Debt 9 · expands MEM_23 (chapter 8). Source: Scenes/Hexer_Debt09_The_Persistent_Thread.json
        static ChronicleScene ASC_HEX_09()
        {
            var w = BeginRecollection("ASC_HEX_09"); var b = w.Builder;
            w.Write("paragraph_1", "The scholar tried to locate the enduring connection upon several maps of the broken paths. Its position shifted with the map. The relationships he could trace from it remained recognizable. He had been searching for a fixed place when the evidence described something held between histories.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("paragraph_2", "He stopped copying its apparent position as though that were its identity. Beside each study he recorded which recollections still led toward it, and which had fallen silent.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("closing", "The useful question was becoming what the anchor connected, rather than where he could draw it.", ChronicleTextStyle.Narration, .9f);   // narrated
            return EndRecollection(b);
        }

        // The Truth Beyond Calculation · Hexer · Fate Debt 10 · expands MEM_26 (chapter 9). Source: Scenes/Hexer_Debt10_The_Truth_Beyond_Calculation.json
        static ChronicleScene ASC_HEX_10()
        {
            var w = BeginRecollection("ASC_HEX_10"); var b = w.Builder;
            w.Write("context", "The old page with the familiar handwriting contained something besides a match: a proof left unfinished. Its last space was reserved for an account from outside the diagram. The hand that drew the paths had not filled it. The scholar had initially treated that gap as damage to be repaired.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("new_observation", "The matching handwriting would supply the answer missing from the calculations.", ChronicleTextStyle.Narration, .7f, keepWith: "The matching handwriting identifies a witness; it does not make that witness's account complete.");   // narrated
            b.Hesitate(.8f).StrikeBlock("new_observation", "ASC_CORR_HEX_4");   // only this addition's own mistaken observation
            w.Write("replacement", "The matching handwriting identifies a witness; it does not make that witness's account complete.", ChronicleTextStyle.Correction, .7f); b.Save("ASC_CORR_HEX_4");
            w.Write("evidence", "The unfinished space belonged to the design. Another person's experience could still contradict the proposed result. Recovering the author might explain why the page had been written, but it could not make the omitted answer unnecessary.", ChronicleTextStyle.Narration, .35f);   // silent, part 1 of 2
            w.Write("evidence_2", "The scholar's earliest comparison had failed by allowing precisely such an answer to disappear.", ChronicleTextStyle.Narration, .7f);   // silent, part 2 of 2
            w.Write("account", "His recovered history ends with a requirement, not a solution to the anchor. Any attempt to change what remains must preserve a way for those affected to answer. Understanding the pattern gives him work to do. It gives him no ownership of the lives the pattern contains.", ChronicleTextStyle.Narration, .35f);   // silent, part 1 of 2
            w.Write("account_2", "The familiar hand remains an unanswered question for the Chronicle.", ChronicleTextStyle.Narration, .7f);   // silent, part 2 of 2
            w.Write("closing", "He could begin another calculation. This time, a person who disagreed with its conclusion would still have a place upon the page.", ChronicleTextStyle.Narration, .9f);   // narrated
            return EndRecollection(b);
        }

        // The Echo That Wasn't Empty · Reaper · Fate Debt 1 · expands MEM_03 (chapter 1). Source: Scenes/Reaper_Debt01_The_Echo_That_Wasn_t_Empty.json
        static ChronicleScene ASC_REA_01()
        {
            var w = BeginRecollection("ASC_REA_01"); var b = w.Builder;
            w.Write("context", "One echo on the abandoned road repeated a small movement: it shifted aside where no traveler passed. The Reaper first took it for the last trace of a journey, a meaningless habit remaining after the life had thinned beyond recognition. He watched the space the movement made.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("new_observation", "An echo without a name had nothing left to recognize.", ChronicleTextStyle.Narration, .7f, keepWith: "It retained the habit of making room for someone beside it.");   // narrated
            b.Hesitate(.8f).StrikeBlock("new_observation", "ASC_CORR_REA_1");   // only this addition's own mistaken observation
            w.Write("replacement", "It retained the habit of making room for someone beside it.", ChronicleTextStyle.Correction, .7f); b.Save("ASC_CORR_REA_1");
            w.Write("evidence", "The step was not simply forward. It left another person's share of the road clear. Whenever the impression faltered, that space appeared again. The lost name did not tell him who had been expected there. The movement told him that the life had once included someone other than itself.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("account", "He could not restore the absent traveler by inventing a name. He could record the relationship the echo still described. His earlier account had measured identity by the parts he could label, then called the rest empty.", ChronicleTextStyle.Narration, .35f);   // silent, part 1 of 2
            w.Write("account_2", "This fragment required a gentler discipline: attend to what it could express before deciding what it lacked.", ChronicleTextStyle.Narration, .7f);   // silent, part 2 of 2
            w.Write("closing", "He left the name unknown. He no longer left the life unacknowledged.", ChronicleTextStyle.Narration, .9f);   // narrated
            return EndRecollection(b);
        }

        // A Forgotten Life · Reaper · Fate Debt 2 · expands MEM_06 (chapter 2). Source: Scenes/Reaper_Debt02_A_Forgotten_Life.json
        static ChronicleScene ASC_REA_02()
        {
            var w = BeginRecollection("ASC_REA_02"); var b = w.Builder;
            w.Write("paragraph_1", "A discarded impression preserved a sleeve repaired with two uneven stitches. The hand wearing it kept adjusting the cuff before reaching toward someone beyond the fragment. The Reaper could recover neither a face nor the occasion.", ChronicleTextStyle.Narration, .35f);   // silent, part 1 of 2
            w.Write("paragraph_1_2", "He could see that someone had taken time to mend what another person would wear.", ChronicleTextStyle.Narration, .7f);   // silent, part 2 of 2
            w.Write("paragraph_2", "The account of the lost futures had counted their disappearance. He added this smaller evidence of living: a repair made badly, patiently, and for someone else. The wound was not the whole record.", ChronicleTextStyle.Narration, .9f);   // silent
            return EndRecollection(b);
        }

        // The Fourth Shadow · Reaper · Fate Debt 3 · expands MEM_09 (chapter 3). Source: Scenes/Reaper_Debt03_The_Fourth_Shadow.json
        static ChronicleScene ASC_REA_03()
        {
            var w = BeginRecollection("ASC_REA_03"); var b = w.Builder;
            w.Write("paragraph_1", "Before the fourth shadow crossed the threshold, it waited. The Reaper had paused to examine a trace behind the others. The shadow held its place until he turned toward the passage, then moved with him. The recovered motion was so ordinary that it had almost escaped the account.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("paragraph_2", "An imitation might have followed the three shapes ahead. This delay concerned the person who had fallen behind. The fourth presence had made room for someone whose attention was elsewhere.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("closing", "What he recovered was more than a missing count. Someone had noticed him waiting.", ChronicleTextStyle.Narration, .9f);   // narrated
            return EndRecollection(b);
        }

        // The Prison Built from Mercy · Reaper · Fate Debt 4 · expands MEM_12 (chapter 4). Source: Scenes/Reaper_Debt04_The_Prison_Built_from_Mercy.json
        static ChronicleScene ASC_REA_04()
        {
            var w = BeginRecollection("ASC_REA_04"); var b = w.Builder;
            w.Write("context", "Within one preserved future, a hand kept reaching toward a doorway. The frame remained clear. Nothing of the room beyond it could be recovered. The Reaper followed the associated traces and found that the reaching and the welcome it expected had been held apart.", ChronicleTextStyle.Narration, .35f);   // silent, part 1 of 2
            w.Write("context_2", "Each fragment continued without the other.", ChronicleTextStyle.Narration, .7f);   // silent, part 2 of 2
            w.Write("new_observation", "Everything held beneath the sanctuary had been saved from loss.", ChronicleTextStyle.Narration, .7f, keepWith: "Some fragments were kept after the relationships that gave them meaning had been torn apart.");   // narrated
            b.Hesitate(.8f).StrikeBlock("new_observation", "ASC_CORR_REA_2");   // only this addition's own mistaken observation
            w.Write("replacement", "Some fragments were kept after the relationships that gave them meaning had been torn apart.", ChronicleTextStyle.Correction, .7f); b.Save("ASC_CORR_REA_2");
            w.Write("evidence", "A doorway was not a home merely because its outline endured. The repeated gesture belonged to someone, and had once been answered by someone. The traveler's collection retained evidence of both while denying this impression the meeting that made them intelligible.", ChronicleTextStyle.Narration, .35f);   // silent, part 1 of 2
            w.Write("evidence_2", "Keeping more fragments had not made this particular life whole.", ChronicleTextStyle.Narration, .7f);   // silent, part 2 of 2
            w.Write("account", "The Reaper understood the fear that had built the sanctuary. He also understood why counting preserved shapes could conceal another kind of loss. His record would keep the separation visible.", ChronicleTextStyle.Narration, .35f);   // silent, part 1 of 2
            w.Write("account_2", "If the fragments could ever be helped, that work would have to consider what had been taken between them, not merely what had been stored.", ChronicleTextStyle.Narration, .7f);   // silent, part 2 of 2
            w.Write("closing", "The prison had preserved the reach. It had not preserved the possibility of being met.", ChronicleTextStyle.Narration, .9f);   // narrated
            return EndRecollection(b);
        }

        // The Promise Without Witnesses · Reaper · Fate Debt 5 · expands MEM_15 (chapter 5). Source: Scenes/Reaper_Debt05_The_Promise_Without_Witnesses.json
        static ChronicleScene ASC_REA_05()
        {
            var w = BeginRecollection("ASC_REA_05"); var b = w.Builder;
            w.Write("paragraph_1", "After promising to remember, the fourth companion followed the others' departing steps with his hand against the stone. Three different rhythms passed beneath his fingers. The Reaper remembered him repeating them once after the footsteps themselves had faded.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("paragraph_2", "He had tried to keep more than a list of names. He was attending to the ways the others moved when no explanation was needed. The attempt makes the promise more human. It offers no proof that a human memory could withstand what followed.", ChronicleTextStyle.Narration, .9f);   // silent
            return EndRecollection(b);
        }

        // The Two Wills · Reaper · Fate Debt 6 · expands MEM_18 (chapter 6). Source: Scenes/Reaper_Debt06_The_Two_Wills.json
        static ChronicleScene ASC_REA_06()
        {
            var w = BeginRecollection("ASC_REA_06"); var b = w.Builder;
            w.Write("paragraph_1", "In a recovered instant of the breaking, the same reaching gesture seemed to end in two intentions. One movement opened a way for someone to depart. The other closed around what it could still hold. The Reaper could not separate their shapes, but neither could he make their purposes agree.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("paragraph_2", "The contrast remained inside the damaged recollection. Whatever had bound the wills together had not turned every impulse into consent. He recorded the disagreement without pretending it offered a way to undo the fusion.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("closing", "Sharing an existence had not made their wishes the same.", ChronicleTextStyle.Narration, .9f);   // narrated
            return EndRecollection(b);
        }

        // What Protection Could Not Restore · Reaper · Fate Debt 7 · expands MEM_21 (chapter 7). Source: Scenes/Reaper_Debt07_What_Protection_Could_Not_Restore.json
        static ChronicleScene ASC_REA_07()
        {
            var w = BeginRecollection("ASC_REA_07"); var b = w.Builder;
            w.Write("context", "At the refuge, two survivors gave accounts of the same household. In one, a mother had lived to see her child grown. In the other, that child remembered tending an empty hearth. The Reaper could not settle the accounts by choosing the more merciful one.", ChronicleTextStyle.Narration, .35f);   // silent, part 1 of 2
            w.Write("context_2", "Both arrived with someone who had lived their loss.", ChronicleTextStyle.Narration, .7f);   // silent, part 2 of 2
            w.Write("new_observation", "A life made safe could finally become one unbroken history.", ChronicleTextStyle.Narration, .7f, keepWith: "The refuge sheltered conflicting memories without requiring either survivor to surrender an account.");   // narrated
            b.Hesitate(.8f).StrikeBlock("new_observation", "ASC_CORR_REA_3");   // only this addition's own mistaken observation
            w.Write("replacement", "The refuge sheltered conflicting memories without requiring either survivor to surrender an account.", ChronicleTextStyle.Correction, .7f); b.Save("ASC_CORR_REA_3");
            w.Write("evidence", "The first register gave them one narrow space. The keeper added another. Nothing on the page repaired the past, and the disagreement did not disappear. Yet both could remain without having their safety depend upon accepting the history preferred by someone else. Tomorrow had room for them together.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("account", "Protection had accomplished something restoration could not be assumed to accomplish: it allowed the survivors to continue while their differences remained unresolved. The Reaper retained both testimonies.", ChronicleTextStyle.Narration, .35f);   // silent, part 1 of 2
            w.Write("account_2", "Combining them into a cleaner story would have made his record easier to read by asking another person to disappear within it.", ChronicleTextStyle.Narration, .7f);   // silent, part 2 of 2
            w.Write("closing", "A shared shelter did not require a single past. He could preserve the accounts without declaring one life a mistake.", ChronicleTextStyle.Narration, .9f);   // narrated
            return EndRecollection(b);
        }

        // An Ending Interrupted · Reaper · Fate Debt 8 · expands MEM_24 (chapter 8). Source: Scenes/Reaper_Debt08_An_Ending_Interrupted.json
        static ChronicleScene ASC_REA_08()
        {
            var w = BeginRecollection("ASC_REA_08"); var b = w.Builder;
            w.Write("paragraph_1", "Beneath the vanished ending, the Reaper noticed the pressure left by its strokes. The ink had ceased to form the intended conclusion, but the paper still carried the attempt. He followed the shallow marks without trying to complete them.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("paragraph_2", "A refusal could stop the account from closing. It could not make the attempt to close it innocent. I had pressed those strokes into the paper. I will not conceal that beneath another certainty, or fill the other voice's silence with what I think it means.", ChronicleTextStyle.Narration, .9f);   // silent
            return EndRecollection(b);
        }

        // The Last Remaining Echo · Reaper · Fate Debt 9 · expands MEM_27 (chapter 9). Source: Scenes/Reaper_Debt09_The_Last_Remaining_Echo.json
        static ChronicleScene ASC_REA_09()
        {
            var w = BeginRecollection("ASC_REA_09"); var b = w.Builder;
            w.Write("paragraph_1", "Within the merged impression, the Reaper recognized an old habit. Where the face would not resolve, a movement still left room beside it, as the fourth shadow had waited at the threshold. It was slight enough to be dismissed as damage in the recollection.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("paragraph_2", "He kept it. The surviving pattern included a way of attending to someone else. Recovering that detail did not separate the wills or restore the friend they had known. It changed what the Reaper would listen for.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("closing", "There was still a relation to acknowledge before there was an answer about how to free him.", ChronicleTextStyle.Narration, .9f);   // narrated
            return EndRecollection(b);
        }

        // The Truth of What Remains · Reaper · Fate Debt 10 · expands MEM_27 (chapter 9). Source: Scenes/Reaper_Debt10_The_Truth_of_What_Remains.json
        static ChronicleScene ASC_REA_10()
        {
            var w = BeginRecollection("ASC_REA_10"); var b = w.Builder;
            w.Write("context", "The Reaper had been arranging the surviving details into a portrait of the friend before the breaking. Every match seemed useful; every difference seemed another injury to remove.", ChronicleTextStyle.Narration, .35f);   // silent, part 1 of 2
            w.Write("context_2", "Then the habit of making room returned within the merged recollection, alongside a resistance he could not attribute with the same confidence.", ChronicleTextStyle.Narration, .7f);   // silent, part 2 of 2
            w.Write("new_observation", "To save what remained, they had to recover the fourth companion exactly as he had been.", ChronicleTextStyle.Narration, .7f, keepWith: "What remained deserved a future, even if it could never become its former self.");   // narrated
            b.Hesitate(.8f).StrikeBlock("new_observation", "ASC_CORR_REA_4");   // only this addition's own mistaken observation
            w.Write("replacement", "What remained deserved a future, even if it could never become its former self.", ChronicleTextStyle.Correction, .7f); b.Save("ASC_CORR_REA_4");
            w.Write("evidence", "The old portrait could help them recognize him. It could not answer on behalf of the existence that had endured. The Reaper had already met echoes whose names were gone and survivors whose pasts could not agree. Insisting upon a cleaner account had repeatedly concealed the person still present.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("account", "This is no acceptance of the prison, and no judgment that it must last. Freedom remains worth seeking. Its purpose must include the possibility of an answer they did not expect. If returning their friend required silencing whatever he had become, preservation would once again be mistaken for care.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("closing", "They could search for a way to free him. They could not decide in advance who he must be when he answered.", ChronicleTextStyle.Narration, .9f);   // narrated
            return EndRecollection(b);
        }

        // The Friendship · Vanguard · Fate Debt 1 · expands MEM_07 (chapter 3). Source: Scenes/Vanguard_Debt01_The_Friendship.json
        static ChronicleScene ASC_VAN_01()
        {
            var w = BeginRecollection("ASC_VAN_01"); var b = w.Builder;
            w.Write("context", "The fire returns with a detail the larger account neglected: a cup with a broken handle. The warrior had set it aside. The fourth companion sat beside him and repaired it with a short length of wire, working while the others discussed the road below the kingdom.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("new_observation", "The warrior's loyalty began as a debt he owed the fourth companion.", ChronicleTextStyle.Narration, .7f, keepWith: "Their loyalty grew from small acts freely given, before either man had anything to repay.");   // narrated
            b.Hesitate(.8f).StrikeBlock("new_observation", "ASC_CORR_VAN_1");   // only this addition's own mistaken observation
            w.Write("replacement", "Their loyalty grew from small acts freely given, before either man had anything to repay.", ChronicleTextStyle.Correction, .7f); b.Save("ASC_CORR_VAN_1");
            w.Write("evidence", "There was no rescue to reward that evening, no oath demanded in exchange. The repaired cup passed between them without ceremony. Later, when the fourth spoke too confidently, the warrior answered him without fear that disagreement would end their place beside the same fire.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("account", "That ordinary freedom matters. A follower might have mistaken certainty for strength. Someone who had shared cold meals and foolish plans could recognize the person beneath it. The warrior would challenge him precisely because the friendship allowed more than obedience. The wire was crooked.", ChronicleTextStyle.Narration, .35f);   // silent, part 1 of 2
            w.Write("account_2", "The cup held.", ChronicleTextStyle.Narration, .7f);   // silent, part 2 of 2
            w.Write("closing", "Before he tried to protect his friend from a terrible choice, he had learned that he could tell him no.", ChronicleTextStyle.Narration, .9f);   // narrated
            return EndRecollection(b);
        }

        // The Life He Protected · Vanguard · Fate Debt 2 · expands MEM_01 (chapter 1). Source: Scenes/Vanguard_Debt02_The_Life_He_Protected.json
        static ChronicleScene ASC_VAN_02()
        {
            var w = BeginRecollection("ASC_VAN_02"); var b = w.Builder;
            w.Write("paragraph_1", "The warrior remembered a doorway beside the eastern wall. Someone had marked a child's height upon its frame; the newest line still lay below the latch. On patrol he used to notice another mark appearing, then another. It was a measure no report of the kingdom included.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("paragraph_2", "He did not defend the stone because he believed it eternal. He defended the mornings in which someone could grow taller. The wall was only what stood around them.", ChronicleTextStyle.Narration, .9f);   // silent
            return EndRecollection(b);
        }

        // A Warning Remembered · Vanguard · Fate Debt 3 · expands MEM_04 (chapter 2). Source: Scenes/Vanguard_Debt03_A_Warning_Remembered.json
        static ChronicleScene ASC_VAN_03()
        {
            var w = BeginRecollection("ASC_VAN_03"); var b = w.Builder;
            w.Write("paragraph_1", "At the gate, the warrior watched people begin answering the traveler's questions with the answers they hoped to hear. A warning that proved useful once became a reason to neglect the next uncertainty.", ChronicleTextStyle.Narration, .35f);   // silent, part 1 of 2
            w.Write("paragraph_1_2", "He remembered a guard turning away an ordinary report because it had not appeared among the stranger's predictions.", ChronicleTextStyle.Narration, .7f);   // silent, part 2 of 2
            w.Write("paragraph_2", "The warrior kept that report. Nothing disastrous followed that day. That was why the habit was so easy to dismiss.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("closing", "Trust had begun to decide which warnings were allowed to count.", ChronicleTextStyle.Narration, .9f);   // narrated
            return EndRecollection(b);
        }

        // The Disagreement · Vanguard · Fate Debt 4 · expands MEM_13 (chapter 5). Source: Scenes/Vanguard_Debt04_The_Disagreement.json
        static ChronicleScene ASC_VAN_04()
        {
            var w = BeginRecollection("ASC_VAN_04"); var b = w.Builder;
            w.Write("context", "Beneath the argument lay a quieter disagreement. The fourth companion had marked the positions of the four companions upon the sanctuary floor. The warrior noticed that every path in the proposed escape led away from its center. He knelt and added a space where an answer could be given.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("new_observation", "The warrior opposed the plan because he could not bear to lose his friend.", ChronicleTextStyle.Narration, .7f, keepWith: "He also opposed a rescue that gave the people being saved no choice about its cost.");   // narrated
            b.Hesitate(.8f).StrikeBlock("new_observation", "ASC_CORR_VAN_2");   // only this addition's own mistaken observation
            w.Write("replacement", "He also opposed a rescue that gave the people being saved no choice about its cost.", ChronicleTextStyle.Correction, .7f); b.Save("ASC_CORR_VAN_2");
            w.Write("evidence", "The new mark was not another route through the stone. It was room for a refusal. The fourth understood it, then continued with the plan. Neither had mistaken the other's concern for cowardice. Their disagreement survived because concern did not settle the question of authority.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("account", "The warrior's anger had two objects: the loss he feared, and the decision being made for them. Remembering only the first made him seem unable to understand the danger. Remembering both restores what he was defending.", ChronicleTextStyle.Narration, .35f);   // silent, part 1 of 2
            w.Write("account_2", "He could accept a risk without accepting another person's right to choose it for everyone.", ChronicleTextStyle.Narration, .7f);   // silent, part 2 of 2
            w.Write("closing", "He wanted his friend beside him. He wanted the others to be able to answer for themselves.", ChronicleTextStyle.Narration, .9f);   // narrated
            return EndRecollection(b);
        }

        // A Place That Remembered · Vanguard · Fate Debt 5 · expands MEM_10 (chapter 4). Source: Scenes/Vanguard_Debt05_A_Place_That_Remembered.json
        static ChronicleScene ASC_VAN_05()
        {
            var w = BeginRecollection("ASC_VAN_05"); var b = w.Builder;
            w.Write("paragraph_1", "At one sanctuary door, the warrior stopped before the step he usually took with his shield raised. The opening had already begun. He waited, lowered the shield, and approached differently. For a moment the room offered space for a movement he had not made.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("paragraph_2", "What watched him had remembered a habit. The mismatch did not prove the sanctuary harmless, or blind to what came next. It showed him something smaller: its recognition could be mistaken.", ChronicleTextStyle.Narration, .9f);   // silent
            return EndRecollection(b);
        }

        // The Unfinished Promise · Vanguard · Fate Debt 6 · expands MEM_16 (chapter 6). Source: Scenes/Vanguard_Debt06_The_Unfinished_Promise.json
        static ChronicleScene ASC_VAN_06()
        {
            var w = BeginRecollection("ASC_VAN_06"); var b = w.Builder;
            w.Write("paragraph_1", "The memory of the breaking contains a movement the warrior could not explain afterward. Before his hand reached for the collapsing barrier, it closed as though around the handle of a cup. The gesture was useless there. It belonged to another place, to a quieter exchange whose words would not return.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("paragraph_2", "He had remembered the pressure of making an agreement before he remembered what the agreement asked of him. I cannot recover its words from the grief that followed.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("closing", "He knew he had promised something. He could not yet tell obligation from grief.", ChronicleTextStyle.Narration, .9f);   // narrated
            return EndRecollection(b);
        }

        // The Separation · Vanguard · Fate Debt 7 · expands MEM_16 (chapter 6). Source: Scenes/Vanguard_Debt07_The_Separation.json
        static ChronicleScene ASC_VAN_07()
        {
            var w = BeginRecollection("ASC_VAN_07"); var b = w.Builder;
            w.Write("context", "The warrior's last backward glance had become the whole of this memory. Beyond its edge, others were still trying to leave. One had lost a shoe. Another could not lift the arm that held a borrowed cloak. The warrior reached them while the voice behind the barrier was still calling.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("new_observation", "He left because his friend's command mattered more than his own conviction.", ChronicleTextStyle.Narration, .7f, keepWith: "He left to lead the others out, without consenting to the separation he could not prevent.");   // narrated
            b.Hesitate(.8f).StrikeBlock("new_observation", "ASC_CORR_VAN_3");   // only this addition's own mistaken observation
            w.Write("replacement", "He left to lead the others out, without consenting to the separation he could not prevent.", ChronicleTextStyle.Correction, .7f); b.Save("ASC_CORR_VAN_3");
            w.Write("evidence", "He did not stop arguing when he turned. He kept trying to look back while moving a frightened survivor ahead of him. The movement was awkward, divided between two duties. It was neither the obedience of a follower nor the clean decision his later guilt insisted it must have been.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("account", "This does not make the loss just. It does not prove that another choice would have saved everyone. It restores the people his account had cropped away. While one friend remained beyond his reach, others were within it. He could fail to keep them together and still have acted for more than himself.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("closing", "The separation was real. So were the lives he could still reach when he turned away.", ChronicleTextStyle.Narration, .9f);   // narrated
            return EndRecollection(b);
        }

        // What He Carried Forward · Vanguard · Fate Debt 8 · expands MEM_19 (chapter 7). Source: Scenes/Vanguard_Debt08_What_He_Carried_Forward.json
        static ChronicleScene ASC_VAN_08()
        {
            var w = BeginRecollection("ASC_VAN_08"); var b = w.Builder;
            w.Write("paragraph_1", "On the broken roads, the warrior packed the weakest travelers' provisions where they could reach them. He left his own beneath the rest. When asked why, he could give no account of learning the arrangement.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("paragraph_2", "The fourth companion had once repacked their supplies that way after a long argument about distance. The warrior had mocked the extra trouble, then copied it. A useful kindness survived the name of the person who taught it. He carried that forward without knowing whose example it was.", ChronicleTextStyle.Narration, .9f);   // silent
            return EndRecollection(b);
        }

        // The Repeating Memory · Vanguard · Fate Debt 9 · expands MEM_22 (chapter 8). Source: Scenes/Vanguard_Debt09_The_Repeating_Memory.json
        static ChronicleScene ASC_VAN_09()
        {
            var w = BeginRecollection("ASC_VAN_09"); var b = w.Builder;
            w.Write("paragraph_1", "In one returning battle, the warrior remembered where the first blow would fall. He also remembered stepping toward it. This time he noticed someone behind his shoulder before he moved. He placed his shield differently.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("paragraph_2", "The danger felt familiar; the person he protected did not. He could not tell whether this was another hour, another path, or a memory borrowing the shape of both. He recorded the difference instead of discarding it.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("closing", "Recognition gave him a moment to consider. It did not tell him what he owed that moment.", ChronicleTextStyle.Narration, .9f);   // narrated
            return EndRecollection(b);
        }

        // The Warrior's True History · Vanguard · Fate Debt 10 · expands MEM_25 (chapter 9). Source: Scenes/Vanguard_Debt10_The_Warrior_s_True_History.json
        static ChronicleScene ASC_VAN_10()
        {
            var w = BeginRecollection("ASC_VAN_10"); var b = w.Builder;
            w.Write("context", "The repaired cup returns at last with the conversation that surrounded it. On an earlier road, after one of them had been forced to go ahead, the two men had agreed that distance would not excuse forgetting the other. They would turn back when turning back became possible.", ChronicleTextStyle.Narration, .35f);   // silent, part 1 of 2
            w.Write("context_2", "Neither had imagined the breaking.", ChronicleTextStyle.Narration, .7f);   // silent, part 2 of 2
            w.Write("new_observation", "The promise required the warrior to remain beside his friend, whatever happened.", ChronicleTextStyle.Narration, .7f, keepWith: "The promise bound him to return, not to make their deaths the same.");   // narrated
            b.Hesitate(.8f).StrikeBlock("new_observation", "ASC_CORR_VAN_4");   // only this addition's own mistaken observation
            w.Write("replacement", "The promise bound him to return, not to make their deaths the same.", ChronicleTextStyle.Correction, .7f); b.Save("ASC_CORR_VAN_4");
            w.Write("evidence", "At the barrier he had remembered only the shame of moving away. The agreement had also contained a task for the person who survived: carry the unfinished obligation beyond the place where help had become impossible. Going on could leave a debt unpaid without declaring it worthless.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("account", "Returning would demand more than recovering a face. The warrior would have to find his friend, hear what remained of him, and allow an answer he could not choose in advance. Their old disagreement was part of the friendship he sought to recover. A rescue that erased it would repeat the harm.", ChronicleTextStyle.Narration, .7f);   // silent
            w.Write("closing", "He had not lived merely to remember whom he lost. He had lived long enough to try to reach him again.", ChronicleTextStyle.Narration, .9f);   // narrated
            return EndRecollection(b);
        }
    }
}
