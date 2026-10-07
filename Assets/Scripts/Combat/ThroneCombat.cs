using System;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Core;

namespace GildedFate.Combat
{
    // Act 3 · Theme 3: Gilded Throne AI. Plugs into the shared themed-enemy engine at the end of the chain
    // (… → Cathedral → Fracture → here). Identity: ROYAL ORDER, expressed per enemy through its own relationships
    // (Minions, allies, isolation, Reserve, stance, countdown). There is no universal Royal Order status.
    //
    // Fairness contract: no interception of player attacks, no off-turn actions. Protection is visible Block and buffs.
    // Randomness exists only for Command targets and ally tie / random-ally picks. Reserve, Siege, stances and the
    // Duelist's response are deterministic and always shown.
    //
    // WildMind use here:
    //   step    = pattern position (Thronebreaker Siege, Duelmaster stance = step/2 % 3, Sovereign cycles per phase)
    //   counter = Reserve (Treasury Beast 0-4, Treasury Warden 0-6)
    //   flag    = Royal General replacement used · Treasury Warden Emergency Treasury used
    //   phase   = The Sovereign phase
    public sealed partial class CombatState
    {
        private const EnemyActionType A_=EnemyActionType.Attack;
        private static void ResetThroneState(WildMind m,string id)
        {
            if(id==GildedThroneContent.Beast||id==GildedThroneContent.Warden)m.counter=2;
            else if(id==GildedThroneContent.Sovereign)m.phase=1;
            ResetNeutralState(m,id);
        }
        private static int ReserveCap(string id)=>id==GildedThroneContent.Warden?6:4;

        // ---------- Crown Duelist: previous-turn classification (deterministic) ----------
        public const string DuelAggressive="gt_counterstance",DuelDefensive="gt_piercing",DuelBalanced="gt_measure";
        private string DuelistResponse()
        {
            if(!judgeHadPreviousTurn)return DuelBalanced;
            if(judgeLastAttacks>=3)return DuelAggressive;
            if(judgeLastEndBlock>=20)return DuelDefensive;
            return DuelBalanced;
        }
        private static string DuelStanceName(int stance)=>stance==0?"KING'S EDGE":stance==1?"ROYAL GUARD":"EXECUTION";
        private static string SovereignPhaseName(int phase)=>phase<=1?"SEATED SOVEREIGN":phase==2?"RISING SOVEREIGN":"FINAL SOVEREIGN";

        // ---------- move choice ----------
        private string ChooseThroneMove(WildMind m)
        {
            var self=enemyContextIndex;
            switch(enemyId)
            {
                case GildedThroneContent.Vanguard:return Cycle(m,4) switch{0=>"gt_advance",1=>"gt_disciplined_guard",2=>"gt_cleave",_=>"gt_kings_discipline"};
                case GildedThroneContent.Crownshield:
                {
                    var alone=!LivingAllies(self,false).Any();var plain=LivingAllies(self,true).Any();
                    switch(Cycle(m,3))
                    {
                        case 0:return alone||!plain?"gt_bastion":"gt_guard_court";
                        case 1:return "gt_shielded_strike";
                        default:return alone?"gt_shielded_strike":"gt_unified";
                    }
                }
                case GildedThroneContent.Adjudicator:return Cycle(m,4) switch{0=>"gt_unworthy",1=>"gt_judicial",2=>"gt_sentence",_=>"gt_seal"};
                case GildedThroneContent.Strategist:return Cycle(m,4) switch{0=>"gt_tactical",1=>"gt_fortified",2=>"gt_preparation",_=>"gt_perfect_formation"};
                case GildedThroneContent.Commander:
                {
                    var guards=OwnedMinions(self).Count();var canDeploy=CanSummon(self);
                    switch(Cycle(m,5))
                    {
                        case 0:return guards==0&&canDeploy?"gt_deploy":"gt_commanding_strike";
                        case 1:return "gt_commanding_strike";
                        case 2:
                            if(guards<2&&canDeploy)return "gt_deploy";
                            return guards>=2&&EligibleCommandTargets(self).Any()?"gt_command":"gt_commanding_strike";
                        case 3:return guards>0?"gt_drill":"gt_commanding_strike";
                        default:return OwnedMinions(self).Any(i=>EnemyAt(i).hp<EnemyAt(i).maxHp)?"gt_rally":"gt_commanding_strike";
                    }
                }
                case GildedThroneContent.Guard:return Cycle(m,2)==0?"gt_spear":"gt_shield_formation";
                case GildedThroneContent.Beast:
                    if(m.counter>=4)return "gt_stampede"; // priority override: the pattern keeps its place
                    switch(Cycle(m,3))
                    {
                        case 0:return "gt_maw";
                        case 1:return "gt_hoard";
                        default:return m.counter>=2?"gt_spend":"gt_maw";
                    }
                case GildedThroneContent.Duelist:return DuelistResponse();
                case GildedThroneContent.Thronebreaker:return Cycle(m,4) switch{0=>"gt_load",1=>"gt_advance_ram",2=>"gt_lock",_=>"gt_charge"};
                case GildedThroneContent.General:
                {
                    var guards=OwnedMinions(self).Count();
                    var canReinforce=!m.flag&&guards<2&&CanSummon(self)&&guards<2&&OwnedGuardDied(self);
                    switch(Cycle(m,5))
                    {
                        case 0:return guards>0?"gt_gen_formation":"gt_gen_advance";
                        case 1:return "gt_gen_advance";
                        case 2:return EligibleCommandTargets(self).Any()?"gt_gen_command":"gt_gen_advance";
                        case 3:return canReinforce?"gt_gen_reinforce":"gt_execution";
                        default:return "gt_execution";
                    }
                }
                case GildedThroneContent.Warden:
                    if(!m.flag&&m.counter>0&&EnemyAt(self).hp*100<35*EnemyAt(self).maxHp)return "gt_emergency";
                    switch(Cycle(m,6))
                    {
                        case 0:return "gt_build";
                        case 1:return "gt_reserve_strike";
                        case 2:return m.counter>=3?"gt_fortress":"gt_reserve_strike";
                        case 3:return "gt_build";
                        case 4:return m.counter>=4?"gt_barrage":"gt_reserve_strike";
                        default:return "gt_reserve_strike";
                    }
                case GildedThroneContent.Duelmaster:
                {
                    var s=Cycle(m,6);
                    switch(s)
                    {
                        case 0:return "gt_dm_cut";case 1:return "gt_dm_advance";
                        case 2:return "gt_dm_defense";case 3:return "gt_dm_riposte";
                        case 4:return "gt_dm_breaker";default:return "gt_dm_flurry";
                    }
                }
                case GildedThroneContent.Blade:return Cycle(m,2)==0?"gt_blade_strike":"gt_blade_order";
                case GildedThroneContent.Shield:return Cycle(m,2)==0?"gt_shield_bash":"gt_guard_throne";
                case GildedThroneContent.Sovereign:
                    switch(m.phase)
                    {
                        case 1:
                            switch(Cycle(m,4))
                            {
                                case 0:return "gt_sov_decree";case 1:return "gt_sov_formation";
                                case 2:return EligibleCommandTargets(self).Any()?"gt_sov_command":"gt_sov_decree";
                                default:return "gt_sov_gaze";
                            }
                        case 2:return Cycle(m,5) switch{0=>"gt_sov_advance",1=>"gt_sov_sentence",2=>"gt_sov_discipline",3=>"gt_sov_flurry",_=>"gt_sov_imperial"};
                        default:return Cycle(m,5) switch{0=>"gt_sov_edge",1=>"gt_sov_dominion",2=>"gt_sov_barrage",3=>"gt_sov_final_authority",_=>"gt_sov_end"};
                    }
            }
            return ChooseNeutralMove(m);
        }
        // A Royal Guard must actually have fallen before the General may reinforce.
        private bool OwnedGuardDied(int self)
        {
            var uid=MindAt(self).uid;return opponents.Any(o=>o.mind!=null&&o.mind.ownerUid==uid&&o.mind.dead&&o.id==GildedThroneContent.Guard);
        }

        // ---------- moves ----------
        private PlannedEnemyAction[] ThroneActions(string move,WildMind m)
        {
            const EnemyActionType A=EnemyActionType.Attack,B=EnemyActionType.Block,S=EnemyActionType.Strength,W=EnemyActionType.Weak,V=EnemyActionType.Vulnerable;
            var self=enemyContextIndex;
            switch(move)
            {
                // Royal Vanguard
                case "gt_advance":return new[]{P(A,15),P(B,8)};
                case "gt_disciplined_guard":return new[]{P(B,20)};
                case "gt_cleave":return new[]{P(A,18),P(W,1)};
                case "gt_kings_discipline":return new[]{P(S,1),P(B,12)};
                // Crownshield
                case "gt_shielded_strike":return new[]{P(A,11),P(B,13)};
                case "gt_guard_court":return new[]{P(EnemyActionType.BlockLowestNonMinion,14),P(B,8)};
                case "gt_bastion":return new[]{P(B,24)};
                case "gt_unified":return new[]{P(EnemyActionType.BlockAllies,6),P(B,12)};
                // Royal Adjudicator
                case "gt_unworthy":return new[]{P(W,1),P(V,1)};
                case "gt_judicial":return new[]{P(A,14)};
                case "gt_sentence":return new[]{P(V,1),P(A,12)};
                case "gt_seal":return new[]{P(A,10),P(B,11),P(W,1)};
                // Court Strategist (never attacks)
                case "gt_tactical":return new[]{P(EnemyActionType.PickRandomDmgAlly,0),P(EnemyActionType.StrengthTarget,1),P(EnemyActionType.BlockTarget,7)};
                case "gt_fortified":return new[]{P(EnemyActionType.BlockAllies,8)};
                case "gt_preparation":return new[]{P(EnemyActionType.PickLowestDmgAlly,0),P(EnemyActionType.StrengthTarget,1),P(EnemyActionType.BlockTarget,11)};
                case "gt_perfect_formation":
                {
                    var list=new List<PlannedEnemyAction>{P(EnemyActionType.BlockAllies,6),P(EnemyActionType.PickRandomDmgAlly,0),P(EnemyActionType.StrengthTarget,1)};
                    if(LivingAllies(self,false).Count()>=2)list.Add(P(B,8));
                    return list.ToArray();
                }
                // Gilded Commander and Royal Guard
                case "gt_deploy":return new[]{P(EnemyActionType.Summon,1),P(B,6)};
                case "gt_commanding_strike":return new[]{P(A,14),P(B,6)};
                case "gt_command":return new[]{P(EnemyActionType.Command,1),P(B,7)};
                case "gt_drill":return new[]{P(EnemyActionType.StrengthMinions,1),P(EnemyActionType.BlockMinions,6)};
                case "gt_rally":return new[]{P(EnemyActionType.HealMinion,6),P(EnemyActionType.BlockTarget,7)};
                case "gt_spear":return new[]{P(A,9)};
                case "gt_shield_formation":return new[]{P(EnemyActionType.BlockOwner,8),P(B,6)};
                // Treasury Beast (palace Reserve, never the player's Gold)
                case "gt_maw":return new[]{P(A,13+2*Math.Max(0,m.counter))};
                case "gt_hoard":return new[]{P(EnemyActionType.ReserveGain,1),P(B,10)};
                case "gt_spend":return new[]{P(EnemyActionType.ReserveSpend,2),P(B,22)};
                case "gt_stampede":return new[]{P(A,12,2),P(EnemyActionType.ReserveSet,1)};
                // Crown Duelist
                case "gt_counterstance":return new[]{P(B,20),P(A,11)};
                case "gt_piercing":return new[]{P(A,18),P(V,1)};
                case "gt_measure":return new[]{P(A,14),P(B,9)};
                // Thronebreaker
                case "gt_load":return new[]{P(B,15)};
                case "gt_advance_ram":return new[]{P(A,12),P(B,8)};
                case "gt_lock":return new[]{P(S,1),P(B,8)};
                case "gt_charge":return new[]{P(A,31)};
                // The Royal General
                case "gt_gen_advance":return new[]{P(A,16),P(B,10)};
                case "gt_gen_formation":return new[]{P(EnemyActionType.StrengthMinions,1),P(EnemyActionType.BlockMinions,8),P(B,10)};
                case "gt_gen_command":return new[]{P(EnemyActionType.Command,1),P(B,8)};
                case "gt_gen_reinforce":return new[]{P(EnemyActionType.Summon,1),P(B,9)};
                case "gt_execution":return new[]{P(A,OwnedMinions(self).Count()>=2?24:21)};
                // The Treasury Warden
                case "gt_build":return new[]{P(EnemyActionType.ReserveGain,2),P(B,10)};
                case "gt_reserve_strike":return new[]{P(A,14+2*Math.Max(0,m.counter))};
                case "gt_fortress":return new[]{P(EnemyActionType.ReserveSpend,3),P(B,30)};
                case "gt_barrage":return new[]{P(EnemyActionType.ReserveSpend,4),P(A,9,3)};
                case "gt_emergency":{var used=Math.Max(0,m.counter);return new[]{P(EnemyActionType.ReserveAll,0),P(EnemyActionType.Heal,5*used),P(B,12)};}
                // The Crown Duelmaster
                case "gt_dm_cut":return new[]{P(A,20)};
                case "gt_dm_advance":return new[]{P(A,15),P(B,10)};
                case "gt_dm_defense":return new[]{P(B,27)};
                case "gt_dm_riposte":return new[]{P(A,12),P(B,18)};
                case "gt_dm_breaker":return new[]{P(A,25)};
                case "gt_dm_flurry":return new[]{P(A,9,3)};
                // The Sovereign, Royal Blade and Royal Shield
                case "gt_blade_strike":return new[]{P(A,11)};
                case "gt_blade_order":return new[]{P(A,8),P(V,1)};
                case "gt_shield_bash":return new[]{P(A,7),P(B,8)};
                case "gt_guard_throne":return new[]{P(EnemyActionType.BlockOwner,12),P(B,6)};
                case "gt_sov_decree":return new[]{P(A,17),P(B,10)};
                case "gt_sov_command":return new[]{P(EnemyActionType.Command,1),P(B,8)};
                case "gt_sov_formation":return new[]{P(EnemyActionType.StrengthMinions,1),P(EnemyActionType.BlockMinions,7),P(B,12)};
                case "gt_sov_gaze":return new[]{P(W,1),P(V,1),P(A,8)};
                case "gt_sov_advance":return new[]{P(A,21),P(B,11)};
                case "gt_sov_sentence":return new[]{P(V,1),P(A,17)};
                case "gt_sov_discipline":return new[]{P(S,2),P(B,13)};
                case "gt_sov_flurry":return new[]{P(A,9,3)};
                case "gt_sov_imperial":return new[]{P(B,24),P(A,8)};
                case "gt_sov_edge":return new[]{P(A,27)};
                case "gt_sov_dominion":return new[]{P(S,2),P(B,15)};
                case "gt_sov_barrage":return new[]{P(A,8,4)};
                case "gt_sov_final_authority":return new[]{P(A,20),P(W,1),P(V,1)};
                case "gt_sov_end":return new[]{P(A,12,3)};
            }
            return NeutralActions(move,m);
        }

        private static string ThroneName(string move)=>move switch
        {
            "gt_advance"=>"ROYAL ADVANCE","gt_disciplined_guard"=>"DISCIPLINED GUARD","gt_cleave"=>"PALACE CLEAVE","gt_kings_discipline"=>"KING'S DISCIPLINE",
            "gt_shielded_strike"=>"SHIELDED STRIKE","gt_guard_court"=>"GUARD THE COURT","gt_bastion"=>"ROYAL BASTION","gt_unified"=>"UNIFIED DEFENSE",
            "gt_unworthy"=>"DECLARE UNWORTHY","gt_judicial"=>"JUDICIAL STRIKE","gt_sentence"=>"ROYAL SENTENCE","gt_seal"=>"SEAL OF AUTHORITY",
            "gt_tactical"=>"TACTICAL ORDER","gt_fortified"=>"FORTIFIED FORMATION","gt_preparation"=>"ROYAL PREPARATION","gt_perfect_formation"=>"PERFECT FORMATION",
            "gt_deploy"=>"DEPLOY ROYAL GUARD","gt_commanding_strike"=>"COMMANDING STRIKE","gt_command"=>"COMMAND","gt_drill"=>"FORMATION DRILL","gt_rally"=>"RALLY THE COURT",
            "gt_spear"=>"ROYAL SPEAR","gt_shield_formation"=>"SHIELD FORMATION",
            "gt_maw"=>"TREASURY MAW","gt_hoard"=>"HOARD POWER","gt_spend"=>"SPEND RESERVE","gt_stampede"=>"GOLDEN STAMPEDE",
            "gt_counterstance"=>"ROYAL COUNTERSTANCE","gt_piercing"=>"PIERCING ADVANCE","gt_measure"=>"PERFECT MEASURE",
            "gt_load"=>"LOAD RAM","gt_advance_ram"=>"ADVANCE RAM","gt_lock"=>"LOCK TARGET","gt_charge"=>"THRONEBREAKER CHARGE",
            "gt_gen_advance"=>"GENERAL'S ADVANCE","gt_gen_formation"=>"PERFECT FORMATION","gt_gen_command"=>"COMMAND","gt_gen_reinforce"=>"REINFORCEMENTS","gt_execution"=>"ROYAL EXECUTION",
            "gt_build"=>"BUILD RESERVE","gt_reserve_strike"=>"RESERVE STRIKE","gt_fortress"=>"FORTRESS EXPENDITURE","gt_barrage"=>"ROYAL BARRAGE","gt_emergency"=>"EMERGENCY TREASURY",
            "gt_dm_cut"=>"PRECISE CUT","gt_dm_advance"=>"ADVANCING BLADE","gt_dm_defense"=>"PERFECT DEFENSE","gt_dm_riposte"=>"SHIELDED RIPOSTE","gt_dm_breaker"=>"CROWN BREAKER","gt_dm_flurry"=>"EXECUTION FLURRY",
            "gt_blade_strike"=>"SOVEREIGN'S BLADE","gt_blade_order"=>"PIERCING ORDER","gt_shield_bash"=>"SHIELD BASH","gt_guard_throne"=>"GUARD THE THRONE",
            "gt_sov_decree"=>"ROYAL DECREE","gt_sov_command"=>"COMMAND THE COURT","gt_sov_formation"=>"ABSOLUTE FORMATION","gt_sov_gaze"=>"SOVEREIGN'S GAZE",
            "gt_sov_advance"=>"KING'S ADVANCE","gt_sov_sentence"=>"CROWN SENTENCE","gt_sov_discipline"=>"ROYAL DISCIPLINE","gt_sov_flurry"=>"SOVEREIGN FLURRY","gt_sov_imperial"=>"IMPERIAL GUARD",
            "gt_sov_edge"=>"SOVEREIGN'S EDGE","gt_sov_dominion"=>"GOLDEN DOMINION","gt_sov_barrage"=>"IMPERIAL BARRAGE","gt_sov_final_authority"=>"FINAL AUTHORITY","gt_sov_end"=>"END OF THE CROWN",
            _=>"STRIKE"
        };
        private string ThroneLabel(string move,WildMind m)
        {
            var name=ThroneName(move);
            switch(move)
            {
                case "gt_counterstance":return name+" · YOU PLAYED 3+ ATTACKS";
                case "gt_piercing":return name+" · YOU ENDED WITH 20+ BLOCK";
                case "gt_measure":return name+(judgeHadPreviousTurn?" · A BALANCED TURN":" · NO PREVIOUS TURN");
                case "gt_maw":return name+$" · RESERVE {m.counter}";
                case "gt_stampede":return name+" · RESERVE 4 · RESETS TO 1";
                case "gt_charge":return name+" · SIEGE COMPLETE";
                case "gt_load":return name+" · SIEGE 3 → 2";
                case "gt_advance_ram":return name+" · SIEGE 2 → 1";
                case "gt_lock":return name+" · SIEGE 1 · CHARGE NEXT";
                case "gt_execution":return OwnedMinions(enemyContextIndex).Count()>=2?name+" · BOTH GUARDS ALIVE":name;
                case "gt_reserve_strike":return name+$" · RESERVE {m.counter}";
                case "gt_command":case "gt_gen_command":case "gt_sov_command":return name+" · A GUARD REPEATS";
                case "gt_sov_end":return name+" · FINAL ATTACK";
            }
            if(enemyId==GildedThroneContent.Duelmaster){var s=Cycle(m,6);return name+" · "+DuelStanceName(s/2)+" "+(s%2+1)+"/2";}
            return name;
        }
        private static string ThroneHook(string move)=>move switch
        {
            "gt_tactical" or "gt_fortified" or "gt_preparation" or "gt_perfect_formation"=>"royal_order_support",
            "gt_guard_court" or "gt_unified"=>"crownshield_protection",
            "gt_command" or "gt_gen_command" or "gt_sov_command"=>"royal_command",
            "gt_deploy"=>"commander_summon",
            "gt_hoard" or "gt_build"=>"treasury_reserve_gain","gt_spend" or "gt_fortress"=>"treasury_reserve_spend",
            "gt_stampede"=>"golden_stampede","gt_barrage"=>"treasury_warden_royal_barrage","gt_emergency"=>"treasury_warden_emergency",
            "gt_charge"=>"thronebreaker_charge",
            "gt_gen_formation"=>"royal_general_formation","gt_gen_reinforce"=>"royal_general_reinforcement",
            "gt_sov_end"=>"end_of_the_crown",
            _=>NeutralHook(move)
        };

        // ---------- resolution ----------
        private bool BeginThroneAction(WildMind m,string move)
        {
            switch(enemyId)
            {
                case GildedThroneContent.Duelist:EmitHook("duelist_response:"+move);return true; // reaction is not a pattern; nothing to advance
                case GildedThroneContent.Thronebreaker:
                {
                    var s=Cycle(m,4);if(s<3)EmitHook("thronebreaker_siege:"+(3-s));return false;
                }
                case GildedThroneContent.Beast:
                    return move=="gt_stampede"; // override keeps its place in the pattern
                case GildedThroneContent.Warden:
                    if(move=="gt_emergency"){m.flag=true;return true;}
                    return false;
                case GildedThroneContent.General:
                    if(move=="gt_gen_reinforce")m.flag=true;
                    return false;
                case GildedThroneContent.Duelmaster:
                {
                    var s=Cycle(m,6);if(s%2==0&&!string.IsNullOrEmpty(m.prior))EmitHook("crown_duelmaster_stance:"+DuelStanceName(s/2));
                    return false;
                }
                default:return BeginNeutralAction(m,move);
            }
        }
        private void ExecuteThroneAction(PlannedEnemyAction action)
        {
            var self=enemyContextIndex;var m=Mind;
            switch(action.type)
            {
                case EnemyActionType.ReserveGain:
                {
                    var gain=Math.Min(action.amount,Math.Max(0,ReserveCap(enemyId)-m.counter));if(gain<=0)break;
                    m.counter+=gain;Emit(CombatEventKind.Status,gain,false,null,"RESERVE");break;
                }
                case EnemyActionType.ReserveSet:
                {
                    var delta=action.amount-m.counter;if(delta!=0)Emit(CombatEventKind.Status,delta,false,null,"RESERVE");m.counter=action.amount;break;
                }
                case EnemyActionType.PickRandomDmgAlly:
                {
                    var allies=LivingAllies(self,false).Where(i=>DamageCapable(EnemyIdAt(i))).ToArray();wildTarget=allies.Length==0?-1:allies[NextRandom(allies.Length)];break;
                }
                case EnemyActionType.PickLowestDmgAlly:
                {
                    var allies=LivingAllies(self,false).Where(i=>DamageCapable(EnemyIdAt(i))).ToArray();
                    if(allies.Length==0){wildTarget=-1;break;}
                    var low=allies.Min(i=>EnemyAt(i).hp);var tied=allies.Where(i=>EnemyAt(i).hp==low).ToArray();wildTarget=tied[tied.Length==1?0:NextRandom(tied.Length)];break;
                }
                case EnemyActionType.StrengthTarget:
                    if(wildTarget>=0&&IsLivingTarget(wildTarget))InEnemyContext(wildTarget,()=>{var g=EnemyBuffAfterWither(action.amount);enemy.strength+=g;Emit(CombatEventKind.Status,g,false,null,"STRENGTH");});
                    break;
                default:ExecuteNeutralAction(action);break;
            }
        }

        // ---------- deaths and thresholds ----------
        private bool ThroneReplanOnDeath(int k)
        {
            var id=opponents[k].id;
            return id is GildedThroneContent.Crownshield or GildedThroneContent.Strategist or GildedThroneContent.Commander or GildedThroneContent.General or GildedThroneContent.Sovereign or GildedThroneContent.Guard;
        }
        private void ThroneOnDeath(int index)
        {
            var id=opponents[index].id;
            if(id is GildedThroneContent.Guard or GildedThroneContent.Blade or GildedThroneContent.Shield)InEnemyContext(index,()=>EmitHook("royal_guard_death"));
        }
        private bool CheckThroneThresholds(int index)
        {
            var o=opponents[index];var m=o.mind;var f=o.fighter;
            if(GildedThroneContent.Find(o.id)==null)return CheckNeutralThresholds(index);
            if(o.id==GildedThroneContent.Warden)
            {
                // Emergency Treasury takes priority the moment it becomes legal, so the player sees the intent change.
                if(PlayerPhaseReplan&&m.planned!="gt_emergency"&&!m.flag&&m.counter>0&&f.hp*100<35*f.maxHp)ReplanAt(index);
                return true;
            }
            if(o.id!=GildedThroneContent.Sovereign)return true;
            var t1=Frac(f.maxHp,288,430);var t2=Frac(f.maxHp,144,430);
            var target=f.hp<=t2?3:f.hp<=t1?2:1;
            while(m.phase<target)
            {
                // Transformation only: no heal, Strength, Fortify or Block. Phase 1 Minions withdraw; the Sovereign gains nothing.
                m.phase++;m.step=0;m.hold=false;var phase=m.phase;
                InEnemyContext(index,()=>EmitHook(phase==2?"sovereign_phase_1_to_2":"sovereign_phase_2_to_3"));
                if(phase==2)
                    for(var k=0;k<opponents.Count;k++)
                        if(opponents[k].fighter.hp>0&&opponents[k].mind?.ownerUid==m.uid)
                        {
                            opponents[k].fighter.hp=0;opponents[k].fighter.block=0;var mk=opponents[k].mind;mk.fled=true;mk.dead=true;mk.diedTurn=turn;
                            InEnemyContext(k,()=>EmitHook("royal_minion_withdrawn"));
                        }
                bossPhase=m.phase;memory.resolvedBossPhase=m.phase;
                if(PlayerPhaseReplan)ReplanAt(index);
            }
            return true;
        }

        // ---------- presentation ----------
        private bool ThroneCounter(int index,WildMind m,out string label,out int value,out int max)
        {
            label="";value=max=0;
            switch(EnemyIdAt(index))
            {
                case GildedThroneContent.Beast:label=$"RESERVE {m.counter}/4";value=m.counter;max=4;return true;
                case GildedThroneContent.Warden:label=$"ROYAL RESERVE {m.counter}/6";value=m.counter;max=6;return true;
                case GildedThroneContent.Duelist:
                {
                    var r=DuelistResponse();label=r==DuelAggressive?"ROYAL COUNTERSTANCE":r==DuelDefensive?"PIERCING ADVANCE":"PERFECT MEASURE";return true;
                }
                case GildedThroneContent.Thronebreaker:
                {
                    var s=Cycle(m,4);label=s==3?"SIEGE 0 · CHARGE NOW":s==2?"SIEGE 1 · CHARGE NEXT":$"SIEGE {3-s}";return true;
                }
                case GildedThroneContent.General:label=$"GUARDS {OwnedMinions(index).Count()}/2"+(m.flag?" · REINFORCED":"");return true;
                case GildedThroneContent.Duelmaster:{var s=Cycle(m,6);label="STANCE · "+DuelStanceName(s/2);return true;}
                case GildedThroneContent.Commander:label=$"ROYAL GUARDS {OwnedMinions(index).Count()}/2";return true;
                case GildedThroneContent.Sovereign:label=$"PHASE {m.phase} · {SovereignPhaseName(m.phase)}";return true;
            }
            return NeutralCounter(index,m,out label,out value,out max);
        }
        private void ThroneStateText(int index,WildMind m,List<string> lines)
        {
            switch(EnemyIdAt(index))
            {
                case GildedThroneContent.Vanguard:lines.Add("ROYAL ORDER · A plain, strong soldier. Advance → Disciplined Guard → Palace Cleave (Weak) → King's Discipline (Strength).");break;
                case GildedThroneContent.Crownshield:lines.Add("ROYAL ORDER · Protects its formation only with visible Block. It never redirects or intercepts your attacks. Alone it uses Royal Bastion.");break;
                case GildedThroneContent.Adjudicator:lines.Add("ROYAL ORDER · Weak and Vulnerable first, then the sentence. Royal Sentence applies Vulnerable BEFORE its damage.");break;
                case GildedThroneContent.Strategist:lines.Add("ROYAL ORDER · A pure Support. It never attacks and never fights alone. Strength and Block for the formation.");break;
                case GildedThroneContent.Commander:lines.Add($"ROYAL ORDER · Deploys up to 2 Royal Guards ({OwnedMinions(index).Count()}/2). Command makes one repeat its previous action. Drill gives them Strength and Block.");break;
                case GildedThroneContent.Guard:lines.Add("Royal Spear → Shield Formation (Block to its Owner). Withdraws when its Owner dies.");break;
                case GildedThroneContent.Beast:lines.Add($"RESERVE {m.counter}/4 · The palace's own resource. It is NOT your Gold and never touches it. Treasury Maw deals 13 +2 per Reserve (not consumed). At 4 it unleashes Golden Stampede (2×12) and resets to 1.");break;
                case GildedThroneContent.Duelist:lines.Add("RESPONSE · By your last turn: 3+ Attacks → Royal Counterstance; ended with 20+ Block → Piercing Advance; else Perfect Measure.");
                    lines.Add(judgeHadPreviousTurn?$"Last turn: {judgeLastAttacks} Attack cards, {judgeLastEndBlock} Block at end.":"No previous turn yet: Perfect Measure.");break;
                case GildedThroneContent.Thronebreaker:lines.Add("SIEGE · 3 Load Ram → 2 Advance Ram → 1 Lock Target → Thronebreaker Charge (31). Always visible. Siege resets to 3 afterwards.");break;
                case GildedThroneContent.General:lines.Add($"FORMATION · Leads 2 Royal Guards. May replace ONE fallen Guard once per fight ({(m.flag?"used":"available")}). Royal Execution deals 24 while both Guards live, else 21.");break;
                case GildedThroneContent.Warden:lines.Add($"ROYAL RESERVE {m.counter}/6 · Palace-owned power, never your Gold. Reserve Strike 14 +2 per Reserve. Emergency Treasury once, below 35% health: all Reserve becomes 5 healing each, then 12 Block ({(m.flag?"used":"unused")}).");break;
                case GildedThroneContent.Duelmaster:{var s=Cycle(m,6);lines.Add($"STANCE · {DuelStanceName(s/2)} (action {s%2+1}/2). King's Edge → Royal Guard → Execution, two actions each. Switching gives no heal, Strength or Block.");break;}
                case GildedThroneContent.Sovereign:
                    lines.Add($"PHASE {m.phase} · {SovereignPhaseName(m.phase)} — changes at 288/430 and 144/430 of its health. No heal, Strength, Fortify or Block from changing phase.");
                    lines.Add(m.phase==1?"Seated: it commands a Royal Blade and a Royal Shield. They withdraw when it rises, and it gains nothing from them.":m.phase==2?"Rising: it fights directly. No Minions.":"Standing free of the throne. No Minions.");break;
                case GildedThroneContent.Blade:lines.Add("Sovereign's Blade → Piercing Order (Vulnerable). Withdraws when the Sovereign rises.");break;
                case GildedThroneContent.Shield:lines.Add("Shield Bash → Guard the Throne (Block to the Sovereign). Withdraws when the Sovereign rises.");break;
                default:NeutralStateText(index,m,lines);break;
            }
        }
        private string DescribeThroneAction(EnemyActionType type,int amount,EnemyIntentAction a)
        {
            switch(type)
            {
                case EnemyActionType.ReserveGain:a.title="RESERVE";return $"Gains {amount} Reserve. Reserve is palace-owned power, not your Gold.";
                case EnemyActionType.ReserveSet:a.title="RESERVE RESET";return $"Reserve is reset to {amount}.";
                case EnemyActionType.PickRandomDmgAlly:a.title="CHOOSES ALLY";return "Chooses one random living ally that can deal damage.";
                case EnemyActionType.PickLowestDmgAlly:a.title="CHOOSES ALLY";return "Chooses its lowest-health living ally that can deal damage.";
                case EnemyActionType.StrengthTarget:a.title="EMPOWER ALLY";return $"The chosen ally gains {amount} Strength.";
            }
            return DescribeNeutralAction(type,amount,a);
        }
    }
}
