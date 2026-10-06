using System;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Core;

namespace GildedFate.Combat
{
    // Act 1 · Theme 3: The Drowned Quarter AI. Plugs into the shared themed-enemy engine in
    // WildCombat.cs (Owner / Minion / Summon / Command, planning, sweeps, checkpoints).
    // Identity: delayed threats. Every prepared attack is visible in the intent or the
    // enemy's state text before it lands, and Submerged enemies stay fully targetable.
    public sealed partial class CombatState
    {
        // Player Block when they last ended a turn (-1 before the first). Canal Stalker's
        // Ambush reads it when the Ambush is chosen, so the shown value never changes.
        public int playerEndBlock=-1;

        private static void ResetDrownedState(WildMind m,string id)
        {
            switch(id)
            {
                case DrownedQuarterContent.Lurker:m.state="SURFACE";break;
                case DrownedQuarterContent.Hulk:m.state="SHELLED";break;
                case DrownedQuarterContent.Ferryman:m.state="DRAGGING";break;
                case DrownedQuarterContent.Marionette:m.counter=3;break; // Strings
                case DrownedQuarterContent.Engine:m.counter=0;break;     // Pressure
            }
        }

        // ---------- move choice ----------
        private string ChooseDrownedMove(WildMind m)
        {
            var self=enemyContextIndex;
            switch(enemyId)
            {
                case DrownedQuarterContent.Rustwalker:return Cycle(m,3) switch{0=>"rusted_cleaver",1=>"brace_frame",_=>"grinding_advance"};
                case DrownedQuarterContent.Lurker:return m.state switch{"SURFACE"=>"drag_below","SUBMERGED"=>"surface_strike",_=>"waterlogged_claw"};
                case DrownedQuarterContent.BellDiver:return Cycle(m,4) switch{0=>"bell_swing",1=>"pressure_toll",2=>"drowning_resonance",_=>"deep_toll"};
                case DrownedQuarterContent.RustPriest:
                    switch(Cycle(m,4))
                    {
                        case 0:return "iron_prayer";
                        case 1:return DamagedArmoredAllies(self).Any()?"patchwork_blessing":"corroded_benediction";
                        case 2:return "corroded_benediction";
                        default:return "flooded_rite";
                    }
                case DrownedQuarterContent.CanalStalker:
                    switch(Cycle(m,4))
                    {
                        case 0:return "canal_cut";case 1:return "watch_the_water";
                        case 2:m.plannedValue=playerEndBlock==0?14:10;return "canal_ambush";
                        default:return "retreating_slash";
                    }
                case DrownedQuarterContent.Tidecaller:
                {
                    var owned=OwnedMinions(self).Count();var canCall=CanSummon(self);
                    switch(Cycle(m,4))
                    {
                        case 0:return owned==0&&canCall?"reach_from_below":"undertow";
                        case 1:return "undertow";
                        case 2:if(owned<2&&canCall)return "reach_from_below";return EligibleCommandTargets(self).Any()?"deep_command":"undertow";
                        default:return OwnedMinions(self).Any(i=>EnemyAt(i).hp<EnemyAt(i).maxHp)?"restore_the_drowned":"undertow";
                    }
                }
                case DrownedQuarterContent.Hand:return Cycle(m,2)==0?"grasp":"dragging_grip";
                case DrownedQuarterContent.Hulk:
                    return m.state=="EXPOSED"?Cycle(m,3) switch{0=>"ripping_charge",1=>"raw_swing",_=>"frantic_crush"}
                                             :Cycle(m,3) switch{0=>"shell_bash",1=>"hardened_growth",_=>"heavy_lunge"};
                case DrownedQuarterContent.Marionette:
                    return m.counter switch{>=3=>"controlled_cut",2=>"jerking_slash",1=>"violent_swing",_=>((m.step2%2)+2)%2==0?"unbound_flail":"collapse_and_rise"};
                case DrownedQuarterContent.Ferryman:
                    return Cycle(m,6) switch{0=>"chain_drag",1=>"raise_anchor",2=>"anchor_drop",3=>"chain_drag",4=>"raise_anchor",_=>"crushing_wake"};
                case DrownedQuarterContent.Bellkeeper:
                    // Alone: its own Final Ring cycle (marked so the shared step stays put).
                    if(!OwnedMinions(self).Any()){m.plannedValue=-7;return (((m.step2%3)+3)%3) switch{0=>"final_ring",1=>"grand_toll",_=>"final_ring"};}
                    m.plannedValue=0;return Cycle(m,4) switch{0=>"drowned_chorus",1=>"grand_toll",2=>EligibleCommandTargets(self).Any()?"bk_command":"grand_toll",_=>"grand_toll"};
                case DrownedQuarterContent.TollThrall:return Cycle(m,2)==0?"toll_strike":"ring_weakness";
                case DrownedQuarterContent.SinkerThrall:return Cycle(m,2)==0?"weighted_slam":"sink_guard";
                case DrownedQuarterContent.Engine:
                    // Build, then burst: Intake · Strike · Compress · Strike · Intake, and once
                    // Pressure is full: Intake · Strike · Compress · BURST VALVE · Vent.
                    switch(Cycle(m,5))
                    {
                        case 0:return "intake";
                        case 1:m.plannedValue=10+2*m.counter;return "pressure_strike";
                        case 2:return "compress";
                        case 3:if(m.counter>=4)return "burst_valve";m.plannedValue=10+2*m.counter;return "pressure_strike";
                        default:return m.step2==1?"vent":"intake";
                    }
                case DrownedQuarterContent.Magistrate:
                    if(m.phase==1)return Cycle(m,4) switch{0=>"sentence",1=>CanSummon(self)?"bailiffs_order":"sentence",2=>"court_barrier",_=>"guilty_verdict"};
                    if(m.phase==2)
                    {
                        if(Cycle(m,4)==2)
                        {
                            // Command a living Bailiff, otherwise summon one. Decided now and shown.
                            m.plannedValue=EligibleCommandTargets(self).Any()?1:CanSummon(self)&&!OwnedMinions(self).Any()?2:0;
                            return "drowned_order";
                        }
                        return Cycle(m,4) switch{0=>"flooded_gavel",1=>"undertow_sentence",_=>"crushing_appeal"};
                    }
                    return Cycle(m,4) switch{0=>"execution_gavel",1=>"drowning_judgment",2=>"relentless_verdict",_=>"final_sentence"};
                case DrownedQuarterContent.Bailiff:return Cycle(m,2)==0?"bailiff_strike":"hold_court";
            }
            return null;
        }
        private IEnumerable<int> DamagedArmoredAllies(int self)=>LivingAllies(self,false)
            .Where(i=>DrownedQuarterContent.Armored.Contains(EnemyIdAt(i))&&EnemyAt(i).hp<EnemyAt(i).maxHp);

        // ---------- moves ----------
        private PlannedEnemyAction[] DrownedActions(string move,WildMind m)
        {
            const EnemyActionType A=EnemyActionType.Attack,B=EnemyActionType.Block,S=EnemyActionType.Strength,W=EnemyActionType.Weak,V=EnemyActionType.Vulnerable;
            switch(move)
            {
                case "rusted_cleaver":return new[]{P(A,8)};
                case "brace_frame":return new[]{P(B,10)};
                case "grinding_advance":return new[]{P(A,6),P(B,5)};
                case "drag_below":return new[]{P(A,5),P(B,8)};
                case "surface_strike":return new[]{P(A,14)};
                case "waterlogged_claw":return new[]{P(A,8)};
                case "bell_swing":return new[]{P(A,8)};
                case "pressure_toll":return new[]{P(W,1),P(B,6)};
                case "drowning_resonance":return new[]{P(V,1),P(A,7)};
                case "deep_toll":return new[]{P(A,11),P(W,1)};
                case "iron_prayer":return new[]{P(EnemyActionType.BlockAllies,6)};
                case "patchwork_blessing":return new[]{P(EnemyActionType.HealArmored,6),P(EnemyActionType.BlockTarget,5)};
                case "corroded_benediction":return new[]{P(EnemyActionType.StrengthRandomAlly,1)};
                case "flooded_rite":return new[]{P(EnemyActionType.HealAlly,4),P(EnemyActionType.BlockAllies,3)};
                case "canal_cut":return new[]{P(A,7)};
                case "watch_the_water":return new[]{P(B,8)};
                case "canal_ambush":return new[]{P(A,m.plannedValue>0?m.plannedValue:10)};
                case "retreating_slash":return new[]{P(A,6),P(B,7)};
                case "reach_from_below":return new[]{P(EnemyActionType.Summon,1),P(B,4)};
                case "undertow":return new[]{P(A,7),P(W,1)};
                case "deep_command":return new[]{P(EnemyActionType.Command,1),P(B,4)};
                case "restore_the_drowned":return new[]{P(EnemyActionType.HealMinion,6),P(EnemyActionType.BlockTarget,5)};
                case "grasp":return new[]{P(A,5)};
                case "dragging_grip":return new[]{P(A,4),P(W,1)};
                case "shell_bash":return new[]{P(A,8),P(B,7)};
                case "hardened_growth":return new[]{P(B,16)};
                case "heavy_lunge":return new[]{P(A,11)};
                case "ripping_charge":return new[]{P(A,15)};
                case "raw_swing":return new[]{P(A,12),P(S,1)};
                case "frantic_crush":return new[]{P(A,7,2)};
                case "controlled_cut":return new[]{P(A,7),P(B,6)};
                case "jerking_slash":return new[]{P(A,10)};
                case "violent_swing":return new[]{P(A,13),P(B,3)};
                case "unbound_flail":return new[]{P(A,7,2)};
                case "collapse_and_rise":return new[]{P(B,9)};
                case "chain_drag":return new[]{P(A,11),P(B,8)};
                case "raise_anchor":return new[]{P(B,14)};
                case "anchor_drop":return new[]{P(A,22)};
                case "crushing_wake":return new[]{P(A,14),P(V,1)};
                case "grand_toll":return new[]{P(A,10),P(W,1)};
                case "drowned_chorus":return new[]{P(EnemyActionType.BlockMinions,6),P(B,7)};
                case "bk_command":return new[]{P(EnemyActionType.Command,1),P(B,5)};
                case "final_ring":return new[]{P(A,16),P(S,1)};
                case "toll_strike":return new[]{P(A,6)};
                case "ring_weakness":return new[]{P(W,1),P(B,4)};
                case "weighted_slam":return new[]{P(A,7)};
                case "sink_guard":return new[]{P(EnemyActionType.BlockOwner,7),P(B,5)};
                case "intake":return new[]{P(EnemyActionType.Pressure,1),P(B,8)};
                case "pressure_strike":return new[]{P(A,m.plannedValue>0?m.plannedValue:10+2*m.counter)};
                case "compress":return new[]{P(EnemyActionType.Pressure,2),P(B,6)};
                case "burst_valve":return new[]{P(A,7,3),P(EnemyActionType.PressureRelease,m.counter)};
                case "vent":return m.counter>0?new[]{P(EnemyActionType.PressureRelease,m.counter),P(B,14)}:new[]{P(B,14)};
                case "sentence":return new[]{P(A,12)};
                case "bailiffs_order":return new[]{P(EnemyActionType.Summon,1),P(B,5)};
                case "court_barrier":return new[]{P(B,18)};
                case "guilty_verdict":return new[]{P(V,1),P(A,9)};
                case "flooded_gavel":return new[]{P(A,16),P(B,6)};
                case "undertow_sentence":return new[]{P(A,10),P(W,1)};
                case "drowned_order":return m.plannedValue switch{1=>new[]{P(EnemyActionType.Command,1),P(B,7)},2=>new[]{P(EnemyActionType.Summon,1)},_=>new[]{P(B,7)}};
                case "crushing_appeal":return new[]{P(A,8,2)};
                case "execution_gavel":return new[]{P(A,19)};
                case "drowning_judgment":return new[]{P(V,1),P(A,13)};
                case "relentless_verdict":return new[]{P(S,2),P(B,8)};
                case "final_sentence":return new[]{P(A,6,4)};
                case "bailiff_strike":return new[]{P(A,7)};
                case "hold_court":return new[]{P(EnemyActionType.BlockOwner,7)};
            }
            return null;
        }
        private string DrownedMoveLabel(string move,WildMind m)=>move switch
        {
            "rusted_cleaver"=>"RUSTED CLEAVER","brace_frame"=>"BRACE FRAME","grinding_advance"=>"GRINDING ADVANCE",
            "drag_below"=>"DRAG BELOW · SURFACE STRIKE NEXT","surface_strike"=>"SURFACE STRIKE","waterlogged_claw"=>"WATERLOGGED CLAW",
            "bell_swing"=>"BELL SWING","pressure_toll"=>"PRESSURE TOLL","drowning_resonance"=>"DROWNING RESONANCE","deep_toll"=>"DEEP TOLL",
            "iron_prayer"=>"IRON PRAYER","patchwork_blessing"=>"PATCHWORK BLESSING","corroded_benediction"=>"CORRODED BENEDICTION","flooded_rite"=>"FLOODED RITE",
            "canal_cut"=>"CANAL CUT","watch_the_water"=>"WATCH THE WATER · AMBUSH NEXT","canal_ambush"=>m.plannedValue>=14?"AMBUSH · YOU ENDED WITHOUT BLOCK":"AMBUSH","retreating_slash"=>"RETREATING SLASH",
            "reach_from_below"=>"REACH FROM BELOW","undertow"=>"UNDERTOW","deep_command"=>"DEEP COMMAND","restore_the_drowned"=>"RESTORE THE DROWNED",
            "grasp"=>"GRASP","dragging_grip"=>"DRAGGING GRIP",
            "shell_bash"=>"SHELL BASH","hardened_growth"=>"HARDENED GROWTH","heavy_lunge"=>"HEAVY LUNGE",
            "ripping_charge"=>"RIPPING CHARGE","raw_swing"=>"RAW SWING","frantic_crush"=>"FRANTIC CRUSH",
            "controlled_cut"=>"CONTROLLED CUT","jerking_slash"=>"JERKING SLASH","violent_swing"=>"VIOLENT SWING",
            "unbound_flail"=>"UNBOUND FLAIL","collapse_and_rise"=>"COLLAPSE AND RISE · FLAIL NEXT",
            "chain_drag"=>"CHAIN DRAG","raise_anchor"=>Cycle(m,6)==4?"RAISE ANCHOR · CRUSHING WAKE NEXT":"RAISE ANCHOR · ANCHOR DROP NEXT",
            "anchor_drop"=>"ANCHOR DROP","crushing_wake"=>"CRUSHING WAKE",
            "grand_toll"=>"GRAND TOLL","drowned_chorus"=>"DROWNED CHORUS","bk_command"=>"COMMAND","final_ring"=>"FINAL RING",
            "toll_strike"=>"TOLL STRIKE","ring_weakness"=>"RING WEAKNESS","weighted_slam"=>"WEIGHTED SLAM","sink_guard"=>"SINK GUARD",
            "intake"=>"INTAKE","pressure_strike"=>"PRESSURE STRIKE","compress"=>m.counter+2>=4?"COMPRESS · PRESSURE FULL":"COMPRESS",
            "burst_valve"=>"BURST VALVE","vent"=>"VENT",
            "sentence"=>"SENTENCE","bailiffs_order"=>"BAILIFF'S ORDER","court_barrier"=>"COURT BARRIER","guilty_verdict"=>"GUILTY VERDICT",
            "flooded_gavel"=>"FLOODED GAVEL","undertow_sentence"=>"UNDERTOW SENTENCE",
            "drowned_order"=>m.plannedValue==1?"DROWNED ORDER · COMMAND":m.plannedValue==2?"DROWNED ORDER · SUMMON":"DROWNED ORDER",
            "crushing_appeal"=>"CRUSHING APPEAL",
            "execution_gavel"=>"EXECUTION GAVEL","drowning_judgment"=>"DROWNING JUDGMENT","relentless_verdict"=>"RELENTLESS VERDICT","final_sentence"=>"FINAL SENTENCE",
            "bailiff_strike"=>"BAILIFF STRIKE","hold_court"=>"HOLD COURT",
            _=>FoundryMoveLabel(move,m)
        };
        // Presentation hook names for future animation / VFX (CombatEventKind.Hook "HOOK:<name>").
        private static string DrownedHook(string move)=>move switch
        {
            "drag_below"=>"drowned_lurker_submerge","surface_strike"=>"drowned_lurker_surface_strike",
            "pressure_toll" or "deep_toll"=>"bell_diver_toll",
            "reach_from_below"=>"tidecaller_summon","deep_command"=>"tidecaller_command",
            "raise_anchor"=>"ferryman_raise_anchor","anchor_drop"=>"ferryman_anchor_drop","crushing_wake"=>"ferryman_crushing_wake",
            "grand_toll" or "final_ring"=>"bellkeeper_toll","bk_command"=>"bellkeeper_command",
            "intake" or "compress"=>"sunken_engine_pressure","burst_valve"=>"sunken_engine_burst_valve","vent"=>"sunken_engine_vent",
            "bailiffs_order"=>"drowned_magistrate_summon","drowned_order"=>"drowned_magistrate_order","final_sentence"=>"drowned_magistrate_final_sentence",
            _=>FoundryHook(move)
        };

        // ---------- resolution ----------
        // Called as the action begins (the hook has already fired). Returns true when the
        // enemy keeps its own pattern position instead of the shared step counter.
        private bool BeginDrownedAction(WildMind m,string move)
        {
            switch(enemyId)
            {
                case DrownedQuarterContent.Lurker:m.state=move=="drag_below"?"SUBMERGED":"SURFACE";return false;
                case DrownedQuarterContent.Ferryman:m.state=move=="raise_anchor"?"RAISED":"DRAGGING";return false;
                case DrownedQuarterContent.Marionette:
                    // One String is lost for every action; at 0 it alternates Flail / Collapse.
                    if(m.counter>0){m.counter--;EmitHook("marionette_string_loss");}else m.step2++;
                    return false;
                case DrownedQuarterContent.Engine:
                    if(move=="burst_valve")m.step2=1;else if(move=="vent")m.step2=0;
                    return false;
                case DrownedQuarterContent.Bellkeeper:
                    if(m.plannedValue==-7){m.step2++;return true;}
                    return false;
            }
            return false;
        }
        private void ExecuteDrownedAction(PlannedEnemyAction action)
        {
            var self=enemyContextIndex;
            switch(action.type)
            {
                case EnemyActionType.HealArmored:
                {
                    var hurt=DamagedArmoredAllies(self).ToArray();wildTarget=-1;if(hurt.Length==0)break;
                    wildTarget=hurt.OrderBy(i=>EnemyAt(i).hp).ThenBy(i=>i).First();HealEnemyAt(wildTarget,action.amount);break;
                }
                case EnemyActionType.StrengthRandomAlly:
                {
                    var allies=LivingAllies(self,false).Where(i=>DamageCapable(EnemyIdAt(i))).ToArray();if(allies.Length==0)break;
                    var target=allies[NextRandom(allies.Length)];wildTarget=target;
                    InEnemyContext(target,()=>{var gain=EnemyBuffAfterWither(action.amount);enemy.strength+=gain;Emit(CombatEventKind.Status,gain,false,null,"STRENGTH");});break;
                }
                case EnemyActionType.Pressure:
                {
                    var m=Mind;var before=m.counter;m.counter=Math.Min(4,m.counter+action.amount);
                    if(m.counter>before)Emit(CombatEventKind.Status,m.counter-before,false,null,"PRESSURE");break;
                }
                case EnemyActionType.PressureRelease:{var m=Mind;if(m.counter>0)Emit(CombatEventKind.Status,-m.counter,false,null,"PRESSURE");m.counter=0;break;}
                default:ExecuteFoundryAction(action);break;
            }
        }

        // ---------- deaths and thresholds ----------
        private bool DrownedReplanOnDeath(int k)
        {
            var mk=opponents[k].mind;
            return mk.planned is "deep_command" or "restore_the_drowned" or "bk_command" or "drowned_chorus" or "patchwork_blessing"
                ||mk.planned=="drowned_order"&&mk.plannedValue==1||FoundryReplanOnDeath(k);
        }
        private void CheckDrownedThresholds(int index)
        {
            var o=opponents[index];var m=o.mind;var f=o.fighter;
            if(CheckFoundryThresholds(index))return;
            if(o.id==DrownedQuarterContent.Hulk)
            {
                if(m.state=="SHELLED"&&f.hp<=30)
                {
                    // Permanent and once only. No heal, no Strength, no Block.
                    m.state="EXPOSED";m.step=0;InEnemyContext(index,()=>EmitHook("barnacle_hulk_shell_break"));
                    if(PlayerPhaseReplan)ReplanAt(index);
                }
            }
            else if(o.id==DrownedQuarterContent.Magistrate)
            {
                // 2/3 and 1/3 of maximum health: 146 and 73 at the base 220.
                var target=f.hp*3<=f.maxHp?3:f.hp*3<=f.maxHp*2?2:1;
                while(m.phase<target)
                {
                    m.phase++;m.step=0;
                    InEnemyContext(index,()=>EmitHook("drowned_magistrate_phase"+m.phase));
                    if(m.phase==3)
                        // The Bailiff sinks. The Magistrate gains nothing from it.
                        for(var k=0;k<opponents.Count;k++)
                            if(opponents[k].fighter.hp>0&&opponents[k].mind?.ownerUid==m.uid)
                            {
                                opponents[k].fighter.hp=0;opponents[k].fighter.block=0;var mk=opponents[k].mind;mk.fled=true;mk.dead=true;mk.diedTurn=turn;
                                InEnemyContext(k,()=>EmitHook("bailiff_echo_removed"));
                            }
                    bossPhase=m.phase;memory.resolvedBossPhase=m.phase;
                    if(PlayerPhaseReplan)ReplanAt(index);
                }
            }
        }

        // ---------- presentation text ----------
        private void DrownedStateText(int index,WildMind m,List<string> lines)
        {
            switch(EnemyIdAt(index))
            {
                case DrownedQuarterContent.Lurker:
                    lines.Add(m.state=="SUBMERGED"?"SUBMERGED · Still targetable. It is preparing Surface Strike (14) for its next action."
                        :"SURFACE · Drag Below sinks it with 8 Block; Surface Strike (14) follows next turn.");break;
                case DrownedQuarterContent.BellDiver:lines.Add("TOLLS · Its bell leaves you Weak and Vulnerable for its allies.");break;
                case DrownedQuarterContent.RustPriest:lines.Add("SUPPORT · Never attacks. Patchwork Blessing mends constructs and armored allies. Flees if only harmless Supports remain.");break;
                case DrownedQuarterContent.CanalStalker:lines.Add("AMBUSH · Deals 14 instead of 10 if you ended your previous turn with 0 Block (checked when Ambush is chosen).");break;
                case DrownedQuarterContent.Tidecaller:lines.Add($"SUMMONER · {OwnedMinions(index).Count()}/2 Drowned Hands.");break;
                case DrownedQuarterContent.Hulk:
                    lines.Add(m.state=="EXPOSED"?"EXPOSED · Its shell is broken. More damage, less Block."
                        :"SHELLED · Its shell breaks for good at 30 health or less.");break;
                case DrownedQuarterContent.Marionette:lines.Add($"STRINGS · {m.counter}/3 — it loses one every action and never regains them. Fewer Strings, wilder attacks.");break;
                case DrownedQuarterContent.Ferryman:
                    lines.Add(m.state=="RAISED"?"ANCHOR RAISED · A heavy anchor attack comes next."
                        :"DRAGGING ANCHOR · Chain Drag, then Raise Anchor before a heavy attack.");break;
                case DrownedQuarterContent.Bellkeeper:
                    lines.Add(OwnedMinions(index).Any()?"THRALLS · Shields and Commands its thralls. Lost thralls are not replaced."
                        :"ALONE · Its thralls are gone. It rings the Final Ring.");break;
                case DrownedQuarterContent.Engine:
                    lines.Add($"PRESSURE · {m.counter}/4 — Pressure Strike deals 10 + 2 per Pressure. At 4 Pressure it can Burst Valve (7 × 3)."
                        +(m.counter>=4?" PRESSURE IS FULL.":""));break;
                case DrownedQuarterContent.Magistrate:
                    lines.Add($"PHASE {m.phase} · "+(m.phase==1?"Bound Magistrate":m.phase==2?"Partially Freed Magistrate":"Freed Magistrate")
                        +(m.phase<3?" — tears further free at "+(m.phase==1?"2/3":"1/3")+" health.":""));break;
                default:FoundryStateText(index,m,lines);break;
            }
        }
        private string DescribeDrownedAction(EnemyActionType type,int amount,EnemyIntentAction a)
        {
            switch(type)
            {
                case EnemyActionType.HealArmored:a.title="PATCHWORK";return $"Its most wounded construct or armored ally heals {amount} HP.";
                case EnemyActionType.StrengthRandomAlly:a.title="EMPOWER ALLY";return $"One random ally that can attack gains {amount} Strength.";
                case EnemyActionType.Pressure:a.title="PRESSURE";return $"Gains {amount} Pressure (maximum 4). Pressure Strike deals 2 more damage per Pressure.";
                case EnemyActionType.PressureRelease:a.title="RELEASE";return $"Its {amount} Pressure resets to 0.";
            }
            return DescribeFoundryAction(type,amount,a);
        }
    }
}
