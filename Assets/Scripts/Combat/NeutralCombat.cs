using System;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Core;

namespace GildedFate.Combat
{
    // Prompt 11: act-specific neutral enemies (end of the themed chain: … → Fracture → Throne → here).
    // Neutrals deliberately use only plain mechanics (Attack, Block, Strength, Weak, visible states and
    // countdowns) so they fit every theme of their act. Everything is shown before it resolves; no RNG at all.
    //
    // WildMind use here:
    //   step    = pattern position (Wayfarer / Chimera stance = step/2, Deepcrawler and Worldbreaker cycles)
    //   state   = Shifting Husk form (ARMORED / EXPOSED) · Deepcrawler (SURFACE / BURROWED)
    //   counter = Worldbreaker Charge 0-2
    public sealed partial class CombatState
    {
        private static void ResetNeutralState(WildMind m,string id)
        {
            if(id==NeutralContent.ShiftingHusk)m.state="ARMORED";
            else if(id==NeutralContent.Deepcrawler)m.state="SURFACE";
        }
        private static string WayfarerStance(int stance)=>stance==0?"WANDERER":stance==1?"HUNTER":"SURVIVOR";
        private static string ChimeraStance(int stance)=>stance==0?"PREDATORY":"GUARDED";
        public const int ShiftingHuskExposeHp=40;

        private string ChooseNeutralMove(WildMind m)
        {
            switch(enemyId)
            {
                case NeutralContent.UnboundBlade:return Cycle(m,3) switch{0=>"nt_measured",1=>"nt_gather_nerve",_=>"nt_driving"};
                case NeutralContent.Fateworn:return Cycle(m,3) switch{0=>"nt_guarded_step",1=>"nt_fated_cut",_=>"nt_turn_thread"};
                case NeutralContent.StrayIdol:return Cycle(m,3) switch{0=>"nt_stone_pulse",1=>"nt_gather_fortune",_=>"nt_unstable_release"};
                case NeutralContent.Wayfarer:return Cycle(m,6) switch{0=>"nt_way_cut",1=>"nt_way_road",2=>"nt_way_hunter",3=>"nt_way_pursuit",4=>"nt_way_endure",_=>"nt_way_desperate"};
                case NeutralContent.RaggedVanguard:return Cycle(m,4) switch{0=>"nt_forward",1=>"nt_brace",2=>"nt_momentum",_=>"nt_driving_strike"};
                case NeutralContent.ShiftingHusk:
                    return m.state=="EXPOSED"?Cycle(m,3) switch{0=>"nt_exposed_rush",1=>"nt_desperate_frame",_=>"nt_shattering"}:Cycle(m,3) switch{0=>"nt_shell_up",1=>"nt_weighted",_=>"nt_heavy_step"};
                case NeutralContent.CrookedOracle:
                    if(!judgeHadPreviousTurn||judgeLastUnusedEnergy<=0)return "nt_full_measure";
                    return judgeLastUnusedEnergy==1?"nt_quiet_omen":"nt_left_unspent";
                case NeutralContent.Deepcrawler:return Cycle(m,4) switch{0=>"nt_dc_maw",1=>"nt_dc_coil",2=>"nt_dc_burrow",_=>"nt_dc_eruption"};
                case NeutralContent.IronWanderer:return Cycle(m,4) switch{0=>"nt_iw_step",1=>"nt_iw_guard",2=>"nt_iw_warpath",_=>"nt_iw_breaker"};
                case NeutralContent.PaleChimera:return Cycle(m,4) switch{0=>"nt_pc_maul",1=>"nt_pc_combo",2=>"nt_pc_claw",_=>"nt_pc_bone"};
                case NeutralContent.NamelessSeer:return Cycle(m,4) switch{0=>"nt_ns_veiled",1=>"nt_ns_omen",2=>"nt_ns_readied",_=>"nt_ns_sentence"};
                case NeutralContent.Worldbreaker:return Cycle(m,5) switch{0=>"nt_wb_march",1=>"nt_wb_gather",2=>"nt_wb_overload",3=>"nt_wb_break",_=>"nt_wb_aftershock"};
            }
            return null;
        }

        private PlannedEnemyAction[] NeutralActions(string move,WildMind m)
        {
            const EnemyActionType A=EnemyActionType.Attack,B=EnemyActionType.Block,S=EnemyActionType.Strength,W=EnemyActionType.Weak;
            switch(move)
            {
                case "nt_measured":return new[]{P(A,8)};
                case "nt_gather_nerve":return new[]{P(S,1),P(B,5)};
                case "nt_driving":return new[]{P(A,12)};
                case "nt_guarded_step":return new[]{P(B,11)};
                case "nt_fated_cut":return new[]{P(A,9)};
                case "nt_turn_thread":return new[]{P(A,7),P(B,7)};
                case "nt_stone_pulse":return new[]{P(A,8)};
                case "nt_gather_fortune":return new[]{P(B,12),P(S,1)};
                case "nt_unstable_release":return new[]{P(A,14),P(EnemyActionType.ReleaseStrength,1)};
                case "nt_way_cut":return new[]{P(A,12),P(B,7)};
                case "nt_way_road":return new[]{P(B,15)};
                case "nt_way_hunter":return new[]{P(A,17)};
                case "nt_way_pursuit":return new[]{P(A,12),P(S,1)};
                case "nt_way_endure":return new[]{P(B,21)};
                case "nt_way_desperate":return new[]{P(A,14),P(B,9)};
                case "nt_forward":return new[]{P(A,12)};
                case "nt_brace":return new[]{P(A,9),P(B,9)};
                case "nt_momentum":return new[]{P(S,1),P(B,8)};
                case "nt_driving_strike":return new[]{P(A,17)};
                case "nt_shell_up":return new[]{P(B,20)};
                case "nt_weighted":return new[]{P(A,11),P(B,10)};
                case "nt_heavy_step":return new[]{P(A,14)};
                case "nt_exposed_rush":return new[]{P(A,18)};
                case "nt_shattering":return new[]{P(A,9,2)};
                case "nt_desperate_frame":return new[]{P(A,13),P(B,7)};
                case "nt_full_measure":return new[]{P(A,13),P(B,9)};
                case "nt_quiet_omen":return new[]{P(B,17),P(A,7)};
                case "nt_left_unspent":return new[]{P(A,19)};
                case "nt_dc_maw":return new[]{P(A,19)};
                case "nt_dc_coil":return new[]{P(B,24)};
                case "nt_dc_burrow":return new[]{P(B,16)};
                case "nt_dc_eruption":return new[]{P(A,28),P(W,1)};
                case "nt_iw_step":return new[]{P(A,17),P(B,11)};
                case "nt_iw_guard":return new[]{P(B,25)};
                case "nt_iw_warpath":return new[]{P(A,12),P(S,1),P(B,8)};
                case "nt_iw_breaker":return new[]{P(A,24)};
                case "nt_pc_maul":return new[]{P(A,20)};
                case "nt_pc_combo":return new[]{P(A,10,2)};
                case "nt_pc_claw":return new[]{P(A,13),P(B,16)};
                case "nt_pc_bone":return new[]{P(B,25)};
                case "nt_ns_veiled":return new[]{P(A,16),P(B,10)};
                case "nt_ns_omen":return new[]{P(A,12),P(W,1)};
                case "nt_ns_readied":return new[]{P(S,1),P(B,15)};
                case "nt_ns_sentence":return new[]{P(A,25)};
                case "nt_wb_march":return new[]{P(A,20),P(B,9)};
                case "nt_wb_gather":return new[]{P(B,15),P(EnemyActionType.ChargeSet,1)};
                case "nt_wb_overload":return new[]{P(S,1),P(B,17),P(EnemyActionType.ChargeSet,2)};
                case "nt_wb_break":return new[]{P(A,42),P(EnemyActionType.ChargeSet,0)};
                case "nt_wb_aftershock":return new[]{P(B,16),P(A,10)};
            }
            return null;
        }

        private static string NeutralName(string move)=>move switch
        {
            "nt_measured"=>"MEASURED STRIKE","nt_gather_nerve"=>"GATHER NERVE","nt_driving"=>"DRIVING BLOW",
            "nt_guarded_step"=>"GUARDED STEP","nt_fated_cut"=>"FATED CUT","nt_turn_thread"=>"TURN THE THREAD",
            "nt_stone_pulse"=>"STONE PULSE","nt_gather_fortune"=>"GATHER FORTUNE","nt_unstable_release"=>"UNSTABLE RELEASE",
            "nt_way_cut"=>"TRAVELING CUT","nt_way_road"=>"MEASURE THE ROAD","nt_way_hunter"=>"HUNTER'S STRIKE","nt_way_pursuit"=>"RELENTLESS PURSUIT","nt_way_endure"=>"ENDURE","nt_way_desperate"=>"DESPERATE CUT",
            "nt_forward"=>"FORWARD CUT","nt_brace"=>"BRACE THROUGH","nt_momentum"=>"GATHER MOMENTUM","nt_driving_strike"=>"DRIVING STRIKE",
            "nt_shell_up"=>"SHELL UP","nt_weighted"=>"WEIGHTED BLOW","nt_heavy_step"=>"HEAVY STEP","nt_exposed_rush"=>"EXPOSED RUSH","nt_shattering"=>"SHATTERING BLOW","nt_desperate_frame"=>"DESPERATE FRAME",
            "nt_full_measure"=>"FULL MEASURE","nt_quiet_omen"=>"QUIET OMEN","nt_left_unspent"=>"LEFT UNSPENT",
            "nt_dc_maw"=>"GNASHING MAW","nt_dc_coil"=>"COIL","nt_dc_burrow"=>"BURROW","nt_dc_eruption"=>"ERUPTION",
            "nt_iw_step"=>"CRUSHING STEP","nt_iw_guard"=>"RAISE GUARD","nt_iw_warpath"=>"WARPATH","nt_iw_breaker"=>"BREAKER",
            "nt_pc_maul"=>"RENDING MAUL","nt_pc_combo"=>"FERAL COMBINATION","nt_pc_claw"=>"GUARDED CLAW","nt_pc_bone"=>"HIDE BEHIND BONE",
            "nt_ns_veiled"=>"VEILED STRIKE","nt_ns_omen"=>"ILL OMEN","nt_ns_readied"=>"READIED FATE","nt_ns_sentence"=>"SENTENCE",
            "nt_wb_march"=>"CRUSHING MARCH","nt_wb_gather"=>"GATHER THE WORLD","nt_wb_overload"=>"OVERLOAD","nt_wb_break"=>"WORLD BREAK","nt_wb_aftershock"=>"AFTERSHOCK",
            _=>"STRIKE"
        };
        private string NeutralLabel(string move,WildMind m)
        {
            var name=NeutralName(move);
            switch(move)
            {
                case "nt_full_measure":return name+(judgeHadPreviousTurn?" · 0 UNUSED ENERGY":" · NO PREVIOUS TURN");
                case "nt_quiet_omen":return name+" · 1 UNUSED ENERGY";
                case "nt_left_unspent":return name+$" · {judgeLastUnusedEnergy} UNUSED ENERGY";
                case "nt_dc_burrow":return name+" · ERUPTION NEXT";
                case "nt_dc_eruption":return name+" · BURROWED";
                case "nt_ns_readied":return name+" · SENTENCE NEXT";
                case "nt_wb_gather":return name+" · CHARGE 1/2 · WORLD BREAK COMING";
                case "nt_wb_overload":return name+" · CHARGE 2/2 · WORLD BREAK NEXT";
                case "nt_wb_break":return name+" · 42 DAMAGE";
            }
            switch(enemyId)
            {
                case NeutralContent.Wayfarer:{var s=Cycle(m,6);return name+" · "+WayfarerStance(s/2)+" "+(s%2+1)+"/2";}
                case NeutralContent.PaleChimera:{var s=Cycle(m,4);return name+" · "+ChimeraStance(s/2)+" "+(s%2+1)+"/2";}
                case NeutralContent.ShiftingHusk:return name+" · "+m.state;
            }
            return name;
        }
        private static string NeutralHook(string move)=>move switch
        {
            "nt_gather_nerve"=>"unbound_blade_gather_nerve","nt_gather_fortune"=>"stray_idol_gather_fortune","nt_unstable_release"=>"stray_idol_unstable_release",
            "nt_dc_burrow"=>"deepcrawler_burrow","nt_dc_eruption"=>"deepcrawler_eruption","nt_ns_readied"=>"nameless_seer_readied_fate",
            "nt_wb_gather"=>"worldbreaker_gather","nt_wb_overload"=>"worldbreaker_overload","nt_wb_break"=>"worldbreaker_world_break","nt_wb_aftershock"=>"worldbreaker_aftershock",
            _=>""
        };

        private bool BeginNeutralAction(WildMind m,string move)
        {
            switch(enemyId)
            {
                case NeutralContent.Wayfarer:{var s=Cycle(m,6);if(s%2==0&&!string.IsNullOrEmpty(m.prior))EmitHook("wayfarer_stance:"+WayfarerStance(s/2));return false;}
                case NeutralContent.PaleChimera:{var s=Cycle(m,4);if(s%2==0&&!string.IsNullOrEmpty(m.prior))EmitHook("pale_chimera_stance:"+ChimeraStance(s/2));return false;}
                case NeutralContent.CrookedOracle:EmitHook("crooked_oracle_response:"+move);return true; // a reaction, not a pattern
                case NeutralContent.Deepcrawler:
                    if(move=="nt_dc_burrow")m.state="BURROWED";else if(move=="nt_dc_eruption")m.state="SURFACE";
                    return false;
            }
            return false;
        }
        private void ExecuteNeutralAction(PlannedEnemyAction action)
        {
            var m=Mind;
            switch(action.type)
            {
                case EnemyActionType.ReleaseStrength:
                {
                    var lose=Math.Min(action.amount,Math.Max(0,enemy.strength));if(lose<=0)break;
                    enemy.strength-=lose;Emit(CombatEventKind.Status,-lose,false,null,"STRENGTH");break;
                }
                case EnemyActionType.ChargeSet:
                    if(m.counter!=action.amount){Emit(CombatEventKind.Status,action.amount-m.counter,false,null,"CHARGE");m.counter=action.amount;}break;
                default:throw new InvalidOperationException("Enemy action has no executor: "+action.type);
            }
        }

        // ---------- Shifting Husk: exposes once at 40 HP or less ----------
        private bool CheckNeutralThresholds(int index)
        {
            var o=opponents[index];var m=o.mind;var f=o.fighter;
            if(NeutralContent.Find(o.id)==null)return false;
            if(o.id==NeutralContent.ShiftingHusk&&m.state!="EXPOSED"&&f.hp<=ShiftingHuskExposeHp)
            {
                // Permanent, once. No heal, Strength or Block. The pattern restarts with the Exposed moves.
                m.state="EXPOSED";m.step=0;m.hold=false;
                InEnemyContext(index,()=>EmitHook("shifting_husk_shell_break"));
                if(PlayerPhaseReplan)ReplanAt(index);
            }
            return true;
        }

        private bool NeutralCounter(int index,WildMind m,out string label,out int value,out int max)
        {
            label="";value=max=0;
            switch(EnemyIdAt(index))
            {
                case NeutralContent.Wayfarer:{var s=Cycle(m,6);label=$"STANCE · {WayfarerStance(s/2)} {s%2+1}/2";return true;}
                case NeutralContent.ShiftingHusk:label=m.state=="EXPOSED"?"EXPOSED":"ARMORED";return true;
                case NeutralContent.CrookedOracle:label=judgeHadPreviousTurn?$"LAST TURN · {judgeLastUnusedEnergy} UNUSED ENERGY":"NO PREVIOUS TURN";return true;
                case NeutralContent.Deepcrawler:label=Cycle(m,4)==3?"BURROWED · ERUPTION NEXT":"SURFACE";return true;
                case NeutralContent.PaleChimera:{var s=Cycle(m,4);label=$"STANCE · {ChimeraStance(s/2)} {s%2+1}/2";return true;}
                case NeutralContent.NamelessSeer:label=Cycle(m,4)==3?"SENTENCE PREPARED":"";return Cycle(m,4)==3;
                case NeutralContent.Worldbreaker:
                    label=m.counter>=2?"CHARGE 2/2 · WORLD BREAK NEXT":$"CHARGE {m.counter}/2";value=m.counter;max=2;return true;
            }
            return false;
        }
        private void NeutralStateText(int index,WildMind m,List<string> lines)
        {
            switch(EnemyIdAt(index))
            {
                case NeutralContent.UnboundBlade:lines.Add("A plain attacker. Measured Strike → Gather Nerve (Strength, Block) → Driving Blow.");break;
                case NeutralContent.Fateworn:lines.Add("Guarded Step → Fated Cut → Turn the Thread (attack and Block).");break;
                case NeutralContent.StrayIdol:lines.Add("Gather Fortune gives it Strength. Unstable Release hits hard, then it loses 1 Strength (never below 0).");break;
                case NeutralContent.Wayfarer:{var s=Cycle(m,6);lines.Add($"STANCE · {WayfarerStance(s/2)} (action {s%2+1}/2). Wanderer → Hunter → Survivor, two actions each. Changing stance gives nothing for free.");break;}
                case NeutralContent.RaggedVanguard:lines.Add("Forward Cut → Brace Through → Gather Momentum → Driving Strike.");break;
                case NeutralContent.ShiftingHusk:lines.Add(m.state=="EXPOSED"?"EXPOSED · Its shell is broken for good. Exposed Rush → Desperate Frame → Shattering Blow.":$"ARMORED · At {ShiftingHuskExposeHp} HP or less its shell breaks once and it becomes Exposed. No free heal, Strength or Block.");break;
                case NeutralContent.CrookedOracle:lines.Add("It reacts to the Energy you left unspent on your previous turn: 0 → Full Measure, 1 → Quiet Omen, 2 or more → Left Unspent. No previous turn → Full Measure. No randomness.");break;
                case NeutralContent.Deepcrawler:lines.Add("Gnashing Maw → Coil → Burrow → Eruption (28 + Weak). Burrowing never hides it or makes it untargetable, and Eruption is announced a full turn ahead.");break;
                case NeutralContent.IronWanderer:lines.Add("Crushing Step → Raise Guard → Warpath → Breaker.");break;
                case NeutralContent.PaleChimera:{var s=Cycle(m,4);lines.Add($"STANCE · {ChimeraStance(s/2)} (action {s%2+1}/2). Two Predatory actions, then two Guarded actions. No free stats on switching.");break;}
                case NeutralContent.NamelessSeer:lines.Add("Veiled Strike → Ill Omen → Readied Fate → Sentence. Readied Fate always prepares the Sentence.");break;
                case NeutralContent.Worldbreaker:lines.Add($"CHARGE {m.counter}/2 · Crushing March → Gather the World → Overload → WORLD BREAK (42, one hit) → Aftershock. The charge is always shown for two full turns. It cannot be interrupted by damage; kill it, Block, or weaken it.");break;
            }
        }
        private string DescribeNeutralAction(EnemyActionType type,int amount,EnemyIntentAction a)
        {
            switch(type)
            {
                case EnemyActionType.ReleaseStrength:a.title="RELEASE";return $"Loses {amount} Strength afterwards (never below 0).";
                case EnemyActionType.ChargeSet:a.title="CHARGE";return amount>=2?"World Break is charged. It resolves on its next action.":amount==1?"World Break is being prepared.":"The charge is spent.";
            }
            return "";
        }
    }
}
