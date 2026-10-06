using System;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Core;

namespace GildedFate.Combat
{
    // Act 3 · Theme 1: Black Cathedral AI. Plugs into the shared themed-enemy engine at the end of the chain
    // (… → Observatory → Ruins → here). Identity: JUDGMENT.
    //
    // Judgment rules (the fairness contract):
    //  - A Judgment only ever reads the player's IMMEDIATELY PREVIOUS turn and only chooses the enemy's NEXT move.
    //    The verdict is known (and shown) during the player's turn, and resolves on the normal enemy turn.
    //  - Nothing here interrupts the player's turn, intercepts a card or punishes immediately.
    //  - Judgment is deterministic from what the player did. RNG is only used for Command targets and Chorister-style
    //    ally ties (Choir Warden's random ally).
    //
    // Previous-turn tracking is combat-local plain data (saved by the combat checkpoint, gone with the fight):
    //   judgeThis*  : what the player has done so far this turn
    //   judgeLast*  : what the player did during the previous turn (read by Judgment)
    //   judgeEnd*   : how the previous turn ENDED (unused Energy, Block left)
    //
    // WildMind use here:
    //   counter = Execution Herald Sentence (3/2/1) · Bell of Sentence Toll (0–4)
    //   state   = Living Icon state (MERCY / JUDGMENT / WRATH) · High Confessor / Final Bishop (phase 2) Judgment category
    //             · Choir Eternal "BROKEN" once every Voice is dead
    //   phase   = Final Bishop phase
    public sealed partial class CombatState
    {
        public int judgeThisAttacks,judgeThisSkills,judgeThisPowers,judgeThisTotal;
        public int judgeLastAttacks,judgeLastSkills,judgeLastPowers,judgeLastTotal,judgeLastUnusedEnergy,judgeLastEndBlock;
        public bool judgeHadPreviousTurn;

        // ---------- previous-turn tracking ----------
        private void RollCathedralMemory()
        {
            judgeHadPreviousTurn=turn>0;
            judgeLastAttacks=judgeThisAttacks;judgeLastSkills=judgeThisSkills;judgeLastPowers=judgeThisPowers;judgeLastTotal=judgeThisTotal;
            if(!judgeHadPreviousTurn){judgeLastUnusedEnergy=0;judgeLastEndBlock=0;}
            judgeThisAttacks=judgeThisSkills=judgeThisPowers=judgeThisTotal=0;
            RollFractureMemory();
        }
        private void CountCathedralCard(CardDef card)
        {
            judgeThisTotal++;
            if(card.kind==CardKind.Attack)judgeThisAttacks++;else if(card.kind==CardKind.Skill)judgeThisSkills++;else if(card.kind==CardKind.Power)judgeThisPowers++;
        }
        // Called as the player's turn ends: how the turn ENDED.
        private void CaptureCathedralEnd(){judgeLastUnusedEnergy=Math.Max(0,energy);judgeLastEndBlock=Math.Max(0,player.block);CaptureFractureEnd();}

        // ---------- Judgment (pure rules, easy to test) ----------
        // Confessor: Attack count.
        public const string JConfessorCowardice="bc_accuse_cowardice",JConfessorMeasured="bc_measured_penance",JConfessorViolence="bc_punish_violence";
        private string ConfessorJudgment()
        {
            if(!judgeHadPreviousTurn)return JConfessorMeasured;
            return judgeLastAttacks<=1?JConfessorCowardice:judgeLastAttacks==2?JConfessorMeasured:JConfessorViolence;
        }
        // Oathbreaker Priest: one card type is at least 70% of at least 3 cards. Returns ATTACKS / SKILLS / POWERS / MIXED.
        public string PriestJudgmentKind()
        {
            if(!judgeHadPreviousTurn||judgeLastTotal<3)return "MIXED";
            if(judgeLastAttacks*100>=70*judgeLastTotal)return "ATTACKS";
            if(judgeLastSkills*100>=70*judgeLastTotal)return "SKILLS";
            if(judgeLastPowers*100>=70*judgeLastTotal)return "POWERS";
            return "MIXED";
        }
        // High Confessor: checked in order, one category per round.
        public string HighConfessorCategory()
        {
            if(!judgeHadPreviousTurn)return "BALANCED";
            if(judgeLastAttacks>=4)return "BLOODTHIRST";
            if(judgeLastEndBlock>=20)return "FORTRESS";
            if(judgeLastUnusedEnergy>=2)return "RESTRAINT";
            if(judgeLastTotal>=7)return "EXCESS";
            return "BALANCED";
        }
        // Final Bishop, Phase 2: Violence → Ritual → Restraint → Balanced.
        public string BishopCategory()
        {
            if(!judgeHadPreviousTurn)return "BALANCED";
            if(judgeLastAttacks>=3)return "VIOLENCE";
            if(judgeLastTotal-judgeLastAttacks>=3)return "RITUAL";
            if(judgeLastUnusedEnergy>=2)return "RESTRAINT";
            return "BALANCED";
        }
        private string JudgmentSummaryLine()=>judgeHadPreviousTurn
            ?$"Last turn: {judgeLastAttacks} Attack · {judgeLastSkills} Skill · {judgeLastPowers} Power ({judgeLastTotal} cards), {judgeLastUnusedEnergy} unused Energy, {judgeLastEndBlock} Block at end of turn."
            :"No previous turn yet: Balanced.";

        // ---------- setup ----------
        private static void ResetCathedralState(WildMind m,string id)
        {
            if(id==BlackCathedralContent.Herald)m.counter=3;
            else if(id==BlackCathedralContent.Bell)m.counter=0;
            else if(id==BlackCathedralContent.Icon)m.state="MERCY";
            ResetFractureState(m,id);
        }

        // ---------- move choice ----------
        private string ChooseCathedralMove(WildMind m)
        {
            var self=enemyContextIndex;
            switch(enemyId)
            {
                case BlackCathedralContent.Guard:return Cycle(m,4) switch{0=>"bc_penitent_strike",1=>"bc_kneel_behind_steel",2=>"bc_punishing_advance",_=>"bc_sacred_discipline"};
                case BlackCathedralContent.Warden:
                    switch(Cycle(m,4))
                    {
                        case 0:return "bc_hymn_steel";
                        case 1:return "bc_hymn_shelter";
                        case 2:return DamagedAlliesAny(self)?"bc_hymn_mercy":"bc_grand_hymn";
                        default:return "bc_grand_hymn";
                    }
                case BlackCathedralContent.Confessor:return ConfessorJudgment();
                case BlackCathedralContent.Censer:return Cycle(m,4) switch{0=>"bc_bitter_incense",1=>"bc_censer_swing",2=>"bc_sacred_smoke",_=>"bc_choking_procession"};
                case BlackCathedralContent.Saint:
                {
                    var owned=OwnedMinions(self).Count();var canSummon=CanSummon(self);
                    switch(Cycle(m,5))
                    {
                        case 0:return owned==0&&canSummon?"bc_consecrate_effigy":"bc_saints_blow";
                        case 1:return "bc_saints_blow";
                        case 2:
                            if(owned<2)return canSummon?"bc_consecrate_effigy":"bc_saints_blow";
                            return EligibleCommandTargets(self).Any()?"bc_saint_command":"bc_saints_blow";
                        case 3:
                            if(OwnedMinions(self).Any(i=>EnemyAt(i).hp<EnemyAt(i).maxHp))return "bc_bless_effigy";
                            return owned>0?"bc_consecrated_formation":"bc_saints_blow";
                        default:return "bc_saints_blow";
                    }
                }
                case BlackCathedralContent.Effigy:return Cycle(m,2)==0?"bc_stone_strike":"bc_prayer_guard";
                case BlackCathedralContent.Herald:return Cycle(m,4) switch{0=>"bc_first_proclamation",1=>"bc_second_proclamation",2=>"bc_final_proclamation",_=>"bc_execution"};
                case BlackCathedralContent.Priest:
                    switch(PriestJudgmentKind()){case "ATTACKS":return "bc_rebuke_blade";case "SKILLS":return "bc_break_ritual";case "POWERS":return "bc_deny_ascension";default:return "bc_broken_vow";}
                case BlackCathedralContent.Icon:
                    return m.state=="JUDGMENT"?"bc_golden_sentence":m.state=="WRATH"?"bc_icons_fury":"bc_merciful_veil";
                case BlackCathedralContent.HighConfessor:
                {
                    var cat=HighConfessorCategory();m.state=cat;
                    return cat switch{"BLOODTHIRST"=>"bc_condemn_violence","FORTRESS"=>"bc_break_wall","RESTRAINT"=>"bc_punish_hesitation","EXCESS"=>"bc_silence_frenzy",_=>"bc_measured_judgment"};
                }
                case BlackCathedralContent.ChoirEternal:
                    if(!OwnedMinions(self).Any())
                    {
                        // Every Voice is dead: alternate Broken Choir → Sacred Refrain, starting with Broken Choir.
                        if(m.state!="BROKEN"){m.state="BROKEN";m.step=0;}
                        return Cycle(m,2)==0?"bc_broken_choir":"bc_sacred_refrain";
                    }
                    switch(Cycle(m,4))
                    {
                        case 0:return "bc_eternal_hymn";
                        case 2:return EligibleCommandTargets(self).Any()?"bc_conduct":"bc_sacred_refrain";
                        default:return "bc_sacred_refrain";
                    }
                case BlackCathedralContent.VoiceBlade:return Cycle(m,2)==0?"bc_blade_verse":"bc_piercing_verse";
                case BlackCathedralContent.VoiceMercy:return Cycle(m,2)==0?"bc_restoring_verse":"bc_gentle_ward";
                case BlackCathedralContent.VoiceVigil:return Cycle(m,2)==0?"bc_vigil_strike":"bc_protective_verse";
                case BlackCathedralContent.Bell:return Cycle(m,5) switch{0=>"bc_first_toll",1=>"bc_second_toll",2=>"bc_third_toll",3=>"bc_fourth_toll",_=>"bc_sentence_of_the_bell"};
                case BlackCathedralContent.Bishop:
                    switch(m.phase)
                    {
                        case 1:
                            switch(Cycle(m,4))
                            {
                                case 0:return "bc_opening_homily";
                                case 1:return "bc_judge_faithful";
                                case 2:return "bc_cathedral_hymn";
                                default:return EligibleCommandTargets(self).Any()?"bc_bishop_command":"bc_opening_homily";
                            }
                        case 2:
                        {
                            var cat=BishopCategory();m.state=cat;
                            return cat switch{"VIOLENCE"=>"bc_punitive_strike","RITUAL"=>"bc_shatter_devotion","RESTRAINT"=>"bc_demand_obedience",_=>"bc_bishops_measure"};
                        }
                        default:return Cycle(m,5) switch{0=>"bc_final_decree",1=>"bc_black_benediction",2=>"bc_shattered_gospel",3=>"bc_last_judgment",_=>"bc_cathedral_collapse"};
                    }
            }
            return ChooseFractureMove(m);
        }
        private bool DamagedAlliesAny(int self)=>DamagedAllies(self).Any();

        // ---------- moves ----------
        private PlannedEnemyAction[] CathedralActions(string move,WildMind m)
        {
            const EnemyActionType A=EnemyActionType.Attack,B=EnemyActionType.Block,S=EnemyActionType.Strength,W=EnemyActionType.Weak,V=EnemyActionType.Vulnerable;
            switch(move)
            {
                // Penitent Guard
                case "bc_penitent_strike":return new[]{P(A,14),P(B,7)};
                case "bc_kneel_behind_steel":return new[]{P(B,19)};
                case "bc_punishing_advance":return new[]{P(A,17),P(W,1)};
                case "bc_sacred_discipline":return new[]{P(S,1),P(B,11)};
                // Choir Warden
                case "bc_hymn_steel":return new[]{P(EnemyActionType.StrengthRandomAlly,1),P(EnemyActionType.BlockTarget,7)};
                case "bc_hymn_shelter":return new[]{P(EnemyActionType.BlockAllies,8)};
                case "bc_hymn_mercy":return new[]{P(EnemyActionType.HealDamagedAlly,7),P(EnemyActionType.BlockTarget,5)};
                case "bc_grand_hymn":return new[]{P(EnemyActionType.BlockAllies,5),P(EnemyActionType.StrengthRandomAlly,1),P(EnemyActionType.HealDamagedAlly,4)};
                // Confessor
                case "bc_accuse_cowardice":return new[]{P(A,18)};
                case "bc_measured_penance":return new[]{P(A,13),P(B,9)};
                case "bc_punish_violence":return new[]{P(B,18),P(A,9),P(W,1)};
                // Censer Bearer
                case "bc_bitter_incense":return new[]{P(W,1),P(V,1)};
                case "bc_censer_swing":return new[]{P(A,14),P(B,6)};
                case "bc_sacred_smoke":return new[]{P(EnemyActionType.BlockAllies,7),P(B,7)};
                case "bc_choking_procession":return new[]{P(A,10),P(W,1)};
                // Reliquary Saint and Chapel Effigy
                case "bc_consecrate_effigy":return new[]{P(EnemyActionType.Summon,1),P(B,6)};
                case "bc_saints_blow":return new[]{P(A,13)};
                case "bc_bless_effigy":return new[]{P(EnemyActionType.HealMinion,7),P(EnemyActionType.BlockTarget,6)};
                case "bc_saint_command":return new[]{P(EnemyActionType.Command,1),P(B,6)};
                case "bc_consecrated_formation":return new[]{P(EnemyActionType.StrengthMinions,1),P(B,9)};
                case "bc_stone_strike":return new[]{P(A,8)};
                case "bc_prayer_guard":return new[]{P(EnemyActionType.BlockOwner,7),P(B,5)};
                // Execution Herald
                case "bc_first_proclamation":return new[]{P(A,11)};
                case "bc_second_proclamation":return new[]{P(B,13)};
                case "bc_final_proclamation":return new[]{P(S,1),P(B,7)};
                case "bc_execution":return new[]{P(A,28)};
                // Oathbreaker Priest
                case "bc_rebuke_blade":return new[]{P(B,16),P(A,12)};
                case "bc_break_ritual":return new[]{P(A,18),P(V,1)};
                case "bc_deny_ascension":return new[]{P(A,15),P(S,1)};
                case "bc_broken_vow":return new[]{P(A,14),P(B,7)};
                // Living Icon
                case "bc_merciful_veil":return new[]{P(B,20),P(EnemyActionType.Heal,5)};
                case "bc_golden_sentence":return new[]{P(A,16),P(W,1)};
                case "bc_icons_fury":return new[]{P(A,7,3)};
                // The High Confessor
                case "bc_condemn_violence":return new[]{P(A,22),P(B,12)};
                case "bc_break_wall":return new[]{P(A,25),P(V,1)};
                case "bc_punish_hesitation":return new[]{P(A,19),P(S,2)};
                case "bc_silence_frenzy":return new[]{P(A,15),P(W,2),P(B,8)};
                case "bc_measured_judgment":return new[]{P(A,17),P(B,10)};
                // The Choir Eternal and its Voices
                case "bc_conduct":return new[]{P(EnemyActionType.Command,1),P(B,8)};
                case "bc_eternal_hymn":return new[]{P(EnemyActionType.StrengthMinions,1),P(EnemyActionType.BlockMinions,5)};
                case "bc_sacred_refrain":return new[]{P(A,15),P(B,10)};
                case "bc_broken_choir":return new[]{P(A,20),P(S,1)};
                case "bc_blade_verse":return new[]{P(A,10)};
                case "bc_piercing_verse":return new[]{P(A,7),P(V,1)};
                case "bc_restoring_verse":return new[]{P(EnemyActionType.HealOwner,6)};
                case "bc_gentle_ward":return new[]{P(EnemyActionType.BlockOwner,9),P(B,4)};
                case "bc_vigil_strike":return new[]{P(A,6),P(B,6)};
                case "bc_protective_verse":return new[]{P(EnemyActionType.BlockSiblings,5)};
                // The Bell of Sentence
                case "bc_first_toll":return new[]{P(A,13)};
                case "bc_second_toll":return new[]{P(B,16)};
                case "bc_third_toll":return new[]{P(A,17),P(W,1)};
                case "bc_fourth_toll":return new[]{P(S,1),P(B,10)};
                case "bc_sentence_of_the_bell":return new[]{P(A,32)};
                // The Final Bishop
                case "bc_opening_homily":return new[]{P(A,16),P(B,10)};
                case "bc_judge_faithful":return judgeHadPreviousTurn&&judgeLastAttacks>=3?new[]{P(W,1),P(A,13)}:new[]{P(A,17)};
                case "bc_cathedral_hymn":return new[]{P(EnemyActionType.StrengthMinions,1),P(B,12)};
                case "bc_bishop_command":return new[]{P(EnemyActionType.Command,1),P(B,8)};
                case "bc_punitive_strike":return new[]{P(A,22),P(B,10)};
                case "bc_shatter_devotion":return new[]{P(A,17),P(V,1),P(B,6)};
                case "bc_demand_obedience":return new[]{P(A,18),P(S,2)};
                case "bc_bishops_measure":return new[]{P(A,19),P(B,12)};
                case "bc_final_decree":return new[]{P(A,24),P(W,1)};
                case "bc_black_benediction":return new[]{P(S,2),P(B,14)};
                case "bc_shattered_gospel":return new[]{P(A,9,3)};
                case "bc_last_judgment":return new[]{P(A,28),P(V,1)};
                case "bc_cathedral_collapse":return new[]{P(A,8,4)};
            }
            return FractureActions(move,m);
        }
        private static string CathedralName(string move)=>move switch
        {
            "bc_penitent_strike"=>"PENITENT STRIKE","bc_kneel_behind_steel"=>"KNEEL BEHIND STEEL","bc_punishing_advance"=>"PUNISHING ADVANCE","bc_sacred_discipline"=>"SACRED DISCIPLINE",
            "bc_hymn_steel"=>"HYMN OF STEEL","bc_hymn_shelter"=>"HYMN OF SHELTER","bc_hymn_mercy"=>"HYMN OF MERCY","bc_grand_hymn"=>"GRAND HYMN",
            "bc_accuse_cowardice"=>"ACCUSE COWARDICE","bc_measured_penance"=>"MEASURED PENANCE","bc_punish_violence"=>"PUNISH VIOLENCE",
            "bc_bitter_incense"=>"BITTER INCENSE","bc_censer_swing"=>"CENSER SWING","bc_sacred_smoke"=>"SACRED SMOKE","bc_choking_procession"=>"CHOKING PROCESSION",
            "bc_consecrate_effigy"=>"CONSECRATE EFFIGY","bc_saints_blow"=>"SAINT'S BLOW","bc_bless_effigy"=>"BLESS THE EFFIGY","bc_saint_command"=>"COMMAND","bc_consecrated_formation"=>"CONSECRATED FORMATION",
            "bc_stone_strike"=>"STONE STRIKE","bc_prayer_guard"=>"PRAYER GUARD",
            "bc_first_proclamation"=>"FIRST PROCLAMATION","bc_second_proclamation"=>"SECOND PROCLAMATION","bc_final_proclamation"=>"FINAL PROCLAMATION","bc_execution"=>"EXECUTION",
            "bc_rebuke_blade"=>"REBUKE THE BLADE","bc_break_ritual"=>"BREAK THE RITUAL","bc_deny_ascension"=>"DENY ASCENSION","bc_broken_vow"=>"BROKEN VOW",
            "bc_merciful_veil"=>"MERCIFUL VEIL","bc_golden_sentence"=>"GOLDEN SENTENCE","bc_icons_fury"=>"ICON'S FURY",
            "bc_condemn_violence"=>"CONDEMN VIOLENCE","bc_break_wall"=>"BREAK THE WALL","bc_punish_hesitation"=>"PUNISH HESITATION","bc_silence_frenzy"=>"SILENCE THE FRENZY","bc_measured_judgment"=>"MEASURED JUDGMENT",
            "bc_conduct"=>"CONDUCT","bc_eternal_hymn"=>"ETERNAL HYMN","bc_sacred_refrain"=>"SACRED REFRAIN","bc_broken_choir"=>"BROKEN CHOIR",
            "bc_blade_verse"=>"BLADE VERSE","bc_piercing_verse"=>"PIERCING VERSE","bc_restoring_verse"=>"RESTORING VERSE","bc_gentle_ward"=>"GENTLE WARD","bc_vigil_strike"=>"VIGIL STRIKE","bc_protective_verse"=>"PROTECTIVE VERSE",
            "bc_first_toll"=>"FIRST TOLL","bc_second_toll"=>"SECOND TOLL","bc_third_toll"=>"THIRD TOLL","bc_fourth_toll"=>"FOURTH TOLL","bc_sentence_of_the_bell"=>"SENTENCE OF THE BELL",
            "bc_opening_homily"=>"OPENING HOMILY","bc_judge_faithful"=>"JUDGE THE FAITHFUL","bc_cathedral_hymn"=>"CATHEDRAL HYMN","bc_bishop_command"=>"COMMAND",
            "bc_punitive_strike"=>"PUNITIVE STRIKE","bc_shatter_devotion"=>"SHATTER DEVOTION","bc_demand_obedience"=>"DEMAND OBEDIENCE","bc_bishops_measure"=>"BISHOP'S MEASURE",
            "bc_final_decree"=>"FINAL DECREE","bc_black_benediction"=>"BLACK BENEDICTION","bc_shattered_gospel"=>"SHATTERED GOSPEL","bc_last_judgment"=>"LAST JUDGMENT","bc_cathedral_collapse"=>"CATHEDRAL COLLAPSE",
            _=>"STRIKE"
        };
        // The label always names the move AND the rule that chose it, so a Judgment is never a surprise.
        private string CathedralLabel(string move,WildMind m)
        {
            var name=CathedralName(move);
            switch(move)
            {
                case "bc_accuse_cowardice":return name+(judgeHadPreviousTurn?" · YOU PLAYED 0–1 ATTACKS":"");
                case "bc_measured_penance":return name+(judgeHadPreviousTurn?" · YOU PLAYED 2 ATTACKS":" · NO PREVIOUS TURN");
                case "bc_punish_violence":return name+" · YOU PLAYED 3+ ATTACKS";
                case "bc_rebuke_blade":return name+" · JUDGED: ATTACK-HEAVY";
                case "bc_break_ritual":return name+" · JUDGED: SKILL-HEAVY";
                case "bc_deny_ascension":return name+" · JUDGED: POWER-HEAVY";
                case "bc_broken_vow":return name+" · JUDGED: MIXED";
                case "bc_first_proclamation":return name+" · SENTENCE 3 → 2";
                case "bc_second_proclamation":return name+" · SENTENCE 2 → 1";
                case "bc_final_proclamation":return name+" · SENTENCE 1 · EXECUTION NEXT";
                case "bc_execution":return name+" · SENTENCE 1 → 3";
                case "bc_merciful_veil":return name+" · MERCY";
                case "bc_golden_sentence":return name+" · JUDGMENT";
                case "bc_icons_fury":return name+" · WRATH";
                case "bc_condemn_violence":return name+" · JUDGMENT: BLOODTHIRST";
                case "bc_break_wall":return name+" · JUDGMENT: FORTRESS";
                case "bc_punish_hesitation":return name+" · JUDGMENT: RESTRAINT";
                case "bc_silence_frenzy":return name+" · JUDGMENT: EXCESS";
                case "bc_measured_judgment":return name+" · JUDGMENT: BALANCED";
                case "bc_first_toll":return name+" · TOLL 0 → 1";
                case "bc_second_toll":return name+" · TOLL 1 → 2";
                case "bc_third_toll":return name+" · TOLL 2 → 3";
                case "bc_fourth_toll":return name+" · TOLL 3 → 4";
                case "bc_sentence_of_the_bell":return name+" · TOLL 4/4 · TOLL → 0";
                case "bc_judge_faithful":return judgeHadPreviousTurn&&judgeLastAttacks>=3?name+" · YOU PLAYED 3+ ATTACKS":name;
                case "bc_punitive_strike":return name+" · JUDGMENT: VIOLENCE";
                case "bc_shatter_devotion":return name+" · JUDGMENT: RITUAL";
                case "bc_demand_obedience":return name+" · JUDGMENT: RESTRAINT";
                case "bc_bishops_measure":return name+" · JUDGMENT: BALANCED";
            }
            return name;
        }
        private static string CathedralHook(string move)=>move switch
        {
            "bc_accuse_cowardice" or "bc_measured_penance" or "bc_punish_violence"=>"confessor_judgment",
            "bc_rebuke_blade" or "bc_break_ritual" or "bc_deny_ascension" or "bc_broken_vow"=>"oathbreaker_judgment",
            "bc_bitter_incense" or "bc_sacred_smoke"=>"censer_smoke",
            "bc_consecrate_effigy"=>"effigy_consecrate","bc_saint_command" or "bc_bishop_command"=>"cathedral_command",
            "bc_first_proclamation" or "bc_second_proclamation" or "bc_final_proclamation"=>"herald_proclamation",
            "bc_execution"=>"execution_strike",
            "bc_condemn_violence" or "bc_break_wall" or "bc_punish_hesitation" or "bc_silence_frenzy" or "bc_measured_judgment"=>"high_confessor_judgment",
            "bc_conduct"=>"choir_eternal_conduct",
            "bc_first_toll" or "bc_second_toll" or "bc_third_toll" or "bc_fourth_toll"=>"bell_toll",
            "bc_sentence_of_the_bell"=>"bell_sentence",
            "bc_punitive_strike" or "bc_shatter_devotion" or "bc_demand_obedience" or "bc_bishops_measure"=>"bishop_judgment",
            "bc_last_judgment"=>"final_judgment","bc_cathedral_collapse"=>"cathedral_collapse",
            _=>FractureHook(move)
        };

        // ---------- resolution ----------
        private bool BeginCathedralAction(WildMind m,string move)
        {
            switch(enemyId)
            {
                case BlackCathedralContent.Herald:
                {
                    // The Sentence is a plain visible counter: 3 → 2 → 1 → (stays 1) → Execution → 3.
                    var before=m.counter;
                    m.counter=move switch{"bc_first_proclamation"=>2,"bc_second_proclamation"=>1,"bc_final_proclamation"=>1,_=>3};
                    if(m.counter!=before)Emit(CombatEventKind.Status,m.counter-before,false,null,"SENTENCE");
                    EmitHook("herald_sentence:"+m.counter);
                    return false;
                }
                case BlackCathedralContent.Bell:
                {
                    var before=m.counter;
                    m.counter=move=="bc_sentence_of_the_bell"?0:Math.Min(4,m.counter+1);
                    if(m.counter!=before)Emit(CombatEventKind.Status,m.counter-before,false,null,"TOLL");
                    EmitHook("bell_toll_count:"+m.counter);
                    return false;
                }
                case BlackCathedralContent.Icon:
                {
                    var next=m.state=="MERCY"?"JUDGMENT":m.state=="JUDGMENT"?"WRATH":"MERCY";
                    m.state=next;EmitHook("living_icon_state:"+next);return false;
                }
                case BlackCathedralContent.Confessor:EmitHook("confessor_judgment:"+move);return false;
                case BlackCathedralContent.Priest:EmitHook("oathbreaker_judgment:"+PriestJudgmentKind());return false;
                case BlackCathedralContent.HighConfessor:EmitHook("high_confessor_judgment:"+m.state);return false;
                case BlackCathedralContent.Bishop:
                    if(m.phase==2)EmitHook("bishop_judgment:"+m.state);
                    return false;
                default:return BeginFractureAction(m,move);
            }
        }
        private void ExecuteCathedralAction(PlannedEnemyAction action)
        {
            var self=enemyContextIndex;
            switch(action.type)
            {
                case EnemyActionType.BlockSiblings:
                {
                    // Every OTHER living Minion owned by the same Owner.
                    var owner=OwnerIndexOf(self);if(owner<0)break;
                    foreach(var i in OwnedMinions(owner).Where(i=>i!=self).ToArray())
                        InEnemyContext(i,()=>{enemy.block+=action.amount;Emit(CombatEventKind.Block,action.amount,false);});
                    break;
                }
                default:ExecuteFractureAction(action);break;
            }
        }

        // ---------- deaths and thresholds ----------
        private bool CathedralReplanOnDeath(int k)
        {
            var id=opponents[k].id;
            return id is BlackCathedralContent.Saint or BlackCathedralContent.ChoirEternal or BlackCathedralContent.Warden or BlackCathedralContent.Bishop||FractureReplanOnDeath(k);
        }
        private void CathedralOnDeath(int index)
        {
            var id=opponents[index].id;
            if(id==BlackCathedralContent.Effigy)InEnemyContext(index,()=>EmitHook("chapel_effigy_death"));
            else if(id is BlackCathedralContent.VoiceBlade or BlackCathedralContent.VoiceMercy or BlackCathedralContent.VoiceVigil)InEnemyContext(index,()=>EmitHook("voice_death"));
            else FractureOnDeath(index);
        }
        // The Final Bishop: 2/3 of maximum health rounded up (260 at the base 390) and 1/3 rounded down (130).
        private bool CheckCathedralThresholds(int index)
        {
            var o=opponents[index];var m=o.mind;var f=o.fighter;
            if(BlackCathedralContent.Find(o.id)==null)return CheckFractureThresholds(index);
            if(o.id!=BlackCathedralContent.Bishop)return true;
            var t1=(f.maxHp*2+2)/3;var t2=f.maxHp/3;
            var target=f.hp<=t2?3:f.hp<=t1?2:1;
            while(m.phase<target)
            {
                // Transformation only: no heal, Strength, Fortify or Block. The prior Judgment is cleared.
                m.phase++;m.step=0;m.hold=false;m.state="";var phase=m.phase;
                InEnemyContext(index,()=>EmitHook(phase==2?"final_bishop_phase_1_to_2":"final_bishop_phase_2_to_3"));
                if(phase==2)
                {
                    // Surviving Voices collapse. The Bishop gains nothing from it.
                    foreach(var k in Enumerable.Range(0,opponents.Count).Where(k=>opponents[k].fighter.hp>0&&opponents[k].mind?.ownerUid==m.uid).ToArray())
                    {
                        opponents[k].fighter.hp=0;opponents[k].mind.dead=true;opponents[k].mind.diedTurn=turn;
                        InEnemyContext(k,()=>EmitHook("bishop_minion_removed"));
                    }
                    rosterVersion++;
                }
                bossPhase=m.phase;memory.resolvedBossPhase=m.phase;
                if(PlayerPhaseReplan)ReplanAt(index);
            }
            return true;
        }

        // ---------- presentation ----------
        private static string BishopPhaseName(int phase)=>phase<=1?"THE ALTARED BISHOP":phase==2?"THE RISING BISHOP":"THE REVEALED FORM";
        private bool CathedralCounter(int index,WildMind m,out string label,out int value,out int max)
        {
            label="";value=max=0;
            switch(EnemyIdAt(index))
            {
                case BlackCathedralContent.Confessor:label=judgeHadPreviousTurn?$"JUDGES ATTACKS · LAST TURN {judgeLastAttacks}":"JUDGES ATTACKS · NO PREVIOUS TURN";return true;
                case BlackCathedralContent.Priest:label=judgeHadPreviousTurn?$"JUDGES CARD TYPE · LAST TURN {judgeLastTotal} CARDS":"JUDGES CARD TYPE · NO PREVIOUS TURN";return true;
                case BlackCathedralContent.Herald:label="SENTENCE "+m.counter;value=m.counter;return true;
                case BlackCathedralContent.Icon:label="STATE · "+(string.IsNullOrEmpty(m.state)?"MERCY":m.state);return true;
                case BlackCathedralContent.HighConfessor:label="JUDGMENT: "+(string.IsNullOrEmpty(m.state)?HighConfessorCategory():m.state);return true;
                case BlackCathedralContent.Bell:label="TOLL";value=m.counter;max=4;return true;
                case BlackCathedralContent.Bishop:
                    label=m.phase==2?$"PHASE 2 · JUDGMENT: {(string.IsNullOrEmpty(m.state)?BishopCategory():m.state)}":$"PHASE {m.phase} · {BishopPhaseName(m.phase)}";return true;
            }
            return FractureCounter(index,m,out label,out value,out max);
        }
        private void CathedralStateText(int index,WildMind m,List<string> lines)
        {
            FractureStateText(index,m,lines);
            switch(EnemyIdAt(index))
            {
                case BlackCathedralContent.Guard:lines.Add("Penitent Strike → Kneel Behind Steel → Punishing Advance → Sacred Discipline. No Judgment.");break;
                case BlackCathedralContent.Warden:lines.Add("SUPPORT · Never attacks. Hymns give allies Strength, Block and healing. Leaves if only harmless Supports remain.");break;
                case BlackCathedralContent.Confessor:
                    lines.Add("JUDGMENT · Judges your previous turn based on how many Attack cards you played. 0–1: Accuse Cowardice (18). 2: Measured Penance (13 + 9 Block). 3+: Punish Violence (18 Block, 9 + Weak). No previous turn: Measured Penance.");
                    lines.Add(JudgmentSummaryLine());break;
                case BlackCathedralContent.Censer:lines.Add("Bitter Incense (Weak + Vulnerable) → Censer Swing → Sacred Smoke (Block to allies) → Choking Procession.");break;
                case BlackCathedralContent.Saint:lines.Add($"SUMMONER · {OwnedMinions(index).Count()}/2 Chapel Effigies. Consecrates, blesses and commands them. Dies together with its Effigies' owner link.");break;
                case BlackCathedralContent.Effigy:lines.Add("Stone Strike → Prayer Guard (Block to its Owner).");break;
                case BlackCathedralContent.Herald:lines.Add($"SENTENCE {m.counter} · First Proclamation (3 → 2) → Second (2 → 1) → Final (stays 1) → EXECUTION (28 damage), then the Sentence resets to 3. It is always visible.");break;
                case BlackCathedralContent.Priest:
                    lines.Add("JUDGMENT · Judges whether you repeated one card type last turn: if one type (Attack, Skill or Power) was at least 70% of at least 3 cards played, it answers that type. Otherwise: Broken Vow.");
                    lines.Add(judgeHadPreviousTurn?$"Last turn: {judgeLastAttacks} Attack · {judgeLastSkills} Skill · {judgeLastPowers} Power of {judgeLastTotal}. Verdict: {PriestJudgmentKind()}.":"No previous turn: Broken Vow.");break;
                case BlackCathedralContent.Icon:lines.Add($"STATE · {(string.IsNullOrEmpty(m.state)?"MERCY":m.state)}. Mercy (Block + heal) → Judgment (16 + Weak) → Wrath (3 × 7) → Mercy.");break;
                case BlackCathedralContent.HighConfessor:
                    lines.Add("JUDGMENT · At the end of each of your turns it picks ONE verdict, checked in this order: Bloodthirst (4+ Attacks), Fortress (20+ Block at end of turn), Restraint (2+ unused Energy), Excess (7+ cards), else Balanced. The verdict decides its next move.");
                    lines.Add(JudgmentSummaryLine());break;
                case BlackCathedralContent.ChoirEternal:lines.Add($"VOICES · {OwnedMinions(index).Count()}/3 alive (Blade, Mercy, Vigil). It never replaces a dead Voice. Eternal Hymn → Sacred Refrain → Conduct → Sacred Refrain. With every Voice dead: Broken Choir ↔ Sacred Refrain.");break;
                case BlackCathedralContent.VoiceBlade:lines.Add("Blade Verse → Piercing Verse.");break;
                case BlackCathedralContent.VoiceMercy:lines.Add("Restoring Verse (heals its Owner) → Gentle Ward.");break;
                case BlackCathedralContent.VoiceVigil:lines.Add("Vigil Strike → Protective Verse (Block to the other Voices).");break;
                case BlackCathedralContent.Bell:lines.Add($"TOLL {m.counter}/4 · First, Second, Third and Fourth Toll each add 1. At Toll 4 the Sentence (32 damage) falls on its own turn, then Toll resets to 0.");break;
                case BlackCathedralContent.Bishop:
                    lines.Add($"PHASE {m.phase} · {BishopPhaseName(m.phase)} — changes form at 2/3 and 1/3 health. No heal, Strength, Fortify or Block from changing form.");
                    if(m.phase==1)lines.Add("Judge the Faithful reads whether you played 3+ Attacks last turn. Its Voices collapse when the Bishop rises.");
                    else if(m.phase==2){lines.Add("JUDGMENT · Picks one verdict from your last turn: Violence (3+ Attacks), Ritual (3+ non-Attack cards), Restraint (2+ unused Energy), else Balanced. It uses that verdict's move.");lines.Add(JudgmentSummaryLine());}
                    else lines.Add("Final Decree → Black Benediction → Shattered Gospel → Last Judgment → Cathedral Collapse. No Judgment.");
                    break;
            }
        }
        private string DescribeCathedralAction(EnemyActionType type,int amount,EnemyIntentAction a)
        {
            if(type==EnemyActionType.BlockSiblings){a.title="SHIELD VOICES";return $"Every other Minion owned by the same Owner gains {amount} Block.";}
            return DescribeFractureAction(type,amount,a);
        }
        public int OwnedMinionsAlive(int ownerIndex)=>OwnedMinions(ownerIndex).Count();
        public bool EligibleCommandTargetsPublic(int ownerIndex)=>EligibleCommandTargets(ownerIndex).Any();
    }
}
