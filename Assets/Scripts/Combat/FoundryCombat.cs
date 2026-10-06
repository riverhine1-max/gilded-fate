using System;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Core;

namespace GildedFate.Combat
{
    // Act 2 · Theme 1: The Crimson Foundry AI. Plugs into the shared themed-enemy engine in
    // WildCombat.cs. Identity: Heat (0–4), an enemy-specific counter stored in WildMind.counter.
    // Every Heat-dependent value is fixed when the intent is chosen (WildMind.plannedValue),
    // so the number the player sees is the number that lands.
    //
    // WildMind use here: counter = Heat / Load / Armor Layers; state = Prototype Zero mode;
    // flag = one-shot (Reassemble used, Servitor resummoned, must vent / cool down);
    // hold = this move is an override that keeps the pattern position.
    public sealed partial class CombatState
    {
        public const int MaxHeat=4,MaxLoad=3,SmelterLayers=3;

        private static void ResetFoundryState(WildMind m,string id)
        {
            if(CrimsonFoundryContent.UsesHeat(id))m.counter=CrimsonFoundryContent.StartingHeat(id);
            if(id==CrimsonFoundryContent.Carrier)m.counter=0;
            if(id==CrimsonFoundryContent.Smelter)m.counter=SmelterLayers;
            if(id==CrimsonFoundryContent.Prototype)m.state="ASSAULT";
            ResetHollowState(m,id);
        }
        private static string KnightBand(int heat)=>heat>=MaxHeat?"OVERHEATED":heat>=2?"HOT":"COLD";

        // ---------- move choice ----------
        private string ChooseFoundryMove(WildMind m)
        {
            var self=enemyContextIndex;var heat=m.counter;
            switch(enemyId)
            {
                case CrimsonFoundryContent.Forgehand:
                    if(heat>=MaxHeat){m.hold=true;return "fh_vent";}
                    switch(Cycle(m,3)){case 0:return "hammer_blow";case 1:return "temper_plate";default:m.plannedValue=10+2*heat;return "heated_strike";}
                case CrimsonFoundryContent.Hound:
                    if(m.flag){m.hold=true;return "cool_down";}
                    if(heat>=MaxHeat){m.hold=Cycle(m,3)!=1;m.plannedValue=16;return "redline_pounce";}
                    switch(Cycle(m,3))
                    {
                        case 0:return "furnace_sprint";
                        case 1:if(heat>=2){m.plannedValue=12;return "redline_pounce";}m.plannedValue=9+heat;return "heated_bite";
                        default:m.plannedValue=9+heat;return "heated_bite";
                    }
                case CrimsonFoundryContent.RivetPriest:
                    switch(Cycle(m,4))
                    {
                        case 0:return "rivet_armor";
                        case 1:return StokeTargets(self).Any()?"stoke_furnace":"reinforce_frame";
                        case 2:return DamagedAllies(self).Any()?"emergency_repair":"reinforce_frame";
                        default:return "reinforce_frame";
                    }
                case CrimsonFoundryContent.ChainWarden:return Cycle(m,4) switch{0=>"chain_lash",1=>"binding_chain",2=>"drag_forward",_=>"crushing_links"};
                case CrimsonFoundryContent.AssemblyMaster:
                {
                    var owned=OwnedMinions(self).Count();var canDeploy=CanSummon(self);
                    switch(Cycle(m,5))
                    {
                        case 0:return canDeploy?"deploy_drone":"tool_strike";
                        case 1:return "tool_strike";
                        case 2:if(owned<2&&canDeploy)return "deploy_drone";return EligibleCommandTargets(self).Any()?"am_command":"tool_strike";
                        case 3:
                            if(OwnedMinions(self).Any(i=>EnemyAt(i).hp<EnemyAt(i).maxHp))return "repair_drone";
                            return owned>0?"overclock_drone":"tool_strike";
                        default:return "tool_strike";
                    }
                }
                case CrimsonFoundryContent.ScrapDrone:return Cycle(m,2)==0?"cutter":"plate_weld";
                case CrimsonFoundryContent.Knight:
                    if(heat>=MaxHeat)return "crucible_breaker";
                    if(heat>=2)return m.prior=="heated_cleave"?"molten_guard":"heated_cleave";
                    return m.prior=="reinforced_guard"?"measured_blade":"reinforced_guard";
                case CrimsonFoundryContent.Carrier:
                    if(m.counter>=MaxLoad)return "molten_spill";
                    return Cycle(m,3) switch{0=>"collect_slag",1=>"heavy_swing",_=>"add_to_crucible"};
                case CrimsonFoundryContent.Automaton:
                    if(m.flag){m.hold=true;return "emergency_vent";}
                    if(heat>=MaxHeat){m.hold=Cycle(m,3)!=2;m.plannedValue=3;return "overdrive_assault";}
                    switch(Cycle(m,3)){case 0:return "redline_slash";case 1:return "boost_servos";default:m.plannedValue=heat>=2?2:1;return "overdrive_assault";}
                case CrimsonFoundryContent.Forgemaster:
                    if(heat>=MaxHeat){m.hold=true;return "forge_overload";}
                    switch(Cycle(m,4))
                    {
                        case 0:return "stoke_the_line";
                        case 1:m.plannedValue=14+heat;return "masterwork_blow";
                        case 2:if(EligibleCommandTargets(self).Any())return "fm_command";m.plannedValue=14+heat;return "masterwork_blow";
                        default:if(!m.flag&&CanSummon(self))return "reassemble";m.plannedValue=14+heat;return "masterwork_blow";
                    }
                case CrimsonFoundryContent.HammerDrone:return Cycle(m,2)==0?"hammer_strike":"overhead_smash";
                case CrimsonFoundryContent.ShieldDrone:return Cycle(m,2)==0?"shield_ram":"reinforce_master";
                case CrimsonFoundryContent.Smelter:
                    if(m.counter>0)return Cycle(m,2)==0?(m.counter>=3?"iron_press":m.counter==2?"heated_press":"molten_press"):"melt_layer";
                    return Cycle(m,3) switch{0=>"core_slam",1=>"core_flare",_=>"liquid_metal_sweep"};
                case CrimsonFoundryContent.Prototype:
                    return Cycle(m,6) switch{0=>"precision_strike",1=>"twin_cutter",2=>"reinforced_shell",3=>"counterframe",4=>"redline_burst",_=>"system_surge"};
                case CrimsonFoundryContent.Saint:
                    if(m.phase==1)
                        switch(Cycle(m,4))
                        {
                            case 0:return "restrained_strike";
                            case 1:return "assembly_guard";
                            case 2:
                                if(OwnedMinions(self).Any()){m.plannedValue=2;return "sacred_production";}
                                if(!m.flag&&CanSummon(self)){m.plannedValue=1;return "sacred_production";}
                                return "restrained_strike";
                            default:return "furnace_prayer";
                        }
                    if(m.phase==2)
                    {
                        if(heat>=MaxHeat){m.hold=true;return "controlled_vent";}
                        switch(Cycle(m,3)){case 0:m.plannedValue=16+heat;return "furnace_blade";case 1:return "heated_guard";default:return "saints_advance";}
                    }
                    {
                        // Phase 3: the major-action positions (0, 3, 5) become Redline Judgment at full Heat.
                        var pos=Cycle(m,6);
                        if(heat>=MaxHeat&&(pos==0||pos==3||pos==5))return "redline_judgment";
                        return pos switch{1=>"burning_guard",2=>"violent_reheat",3=>"saint_breaker",4=>"violent_reheat",_=>"furnace_barrage"};
                    }
                case CrimsonFoundryContent.Servitor:return Cycle(m,2)==0?"service_blade":"reinforce_saint";
            }
            return ChooseHollowMove(m);
        }
        private IEnumerable<int> StokeTargets(int self)=>LivingAllies(self,false)
            .Where(i=>CrimsonFoundryContent.UsesHeat(EnemyIdAt(i))&&MindAt(i)!=null&&MindAt(i).counter<MaxHeat);
        private IEnumerable<int> DamagedAllies(int self)=>LivingAllies(self,false).Where(i=>EnemyAt(i).hp<EnemyAt(i).maxHp);
        private string MissingForgemasterDrone(int ownerIndex)
        {
            var owned=OwnedMinions(ownerIndex).Select(EnemyIdAt).ToArray();
            if(!owned.Contains(CrimsonFoundryContent.HammerDrone))return CrimsonFoundryContent.HammerDrone;
            if(!owned.Contains(CrimsonFoundryContent.ShieldDrone))return CrimsonFoundryContent.ShieldDrone;
            return "";
        }

        // ---------- moves ----------
        private PlannedEnemyAction[] FoundryActions(string move,WildMind m)
        {
            const EnemyActionType A=EnemyActionType.Attack,B=EnemyActionType.Block,S=EnemyActionType.Strength,W=EnemyActionType.Weak,V=EnemyActionType.Vulnerable,
                H=EnemyActionType.Heat,X=EnemyActionType.HeatVent;
            int Pv(int fallback)=>m.plannedValue>0?m.plannedValue:fallback;
            switch(move)
            {
                case "hammer_blow":return new[]{P(A,10),P(H,1)};
                case "temper_plate":return new[]{P(B,11),P(H,1)};
                case "heated_strike":return new[]{P(A,Pv(10+2*m.counter))};
                case "fh_vent":return new[]{P(X,m.counter),P(B,7)};
                case "furnace_sprint":return new[]{P(A,7),P(H,2)};
                case "redline_pounce":return new[]{P(A,Pv(12))};
                case "heated_bite":return new[]{P(A,Pv(9+m.counter)),P(H,1)};
                case "cool_down":return new[]{P(X,m.counter),P(B,8)};
                case "rivet_armor":return new[]{P(EnemyActionType.BlockLowestAlly,9)};
                case "stoke_furnace":return new[]{P(EnemyActionType.HeatAlly,1),P(EnemyActionType.BlockTarget,5)};
                case "reinforce_frame":return new[]{P(EnemyActionType.StrengthRandomAlly,1),P(EnemyActionType.BlockTarget,7)};
                case "emergency_repair":return new[]{P(EnemyActionType.HealDamagedAlly,6),P(EnemyActionType.BlockTarget,5)};
                case "chain_lash":return new[]{P(A,10)};
                case "binding_chain":return new[]{P(W,1),P(B,8)};
                case "drag_forward":return new[]{P(V,1),P(A,8)};
                case "crushing_links":return new[]{P(A,7,2)};
                case "deploy_drone":return new[]{P(EnemyActionType.Summon,1),P(B,5)};
                case "tool_strike":return new[]{P(A,9)};
                case "repair_drone":return new[]{P(EnemyActionType.HealMinion,7),P(EnemyActionType.BlockTarget,5)};
                case "am_command":return new[]{P(EnemyActionType.Command,1),P(B,5)};
                case "overclock_drone":return new[]{P(EnemyActionType.StrengthRandomMinion,1),P(EnemyActionType.BlockTarget,5)};
                case "cutter":return new[]{P(A,6)};
                case "plate_weld":return new[]{P(EnemyActionType.BlockOwner,6),P(B,4)};
                case "reinforced_guard":return new[]{P(B,16),P(H,1)};
                case "measured_blade":return new[]{P(A,10),P(H,1)};
                case "heated_cleave":return new[]{P(A,14),P(H,1)};
                case "molten_guard":return new[]{P(A,7),P(B,10),P(H,1)};
                case "crucible_breaker":return new[]{P(A,20),P(X,m.counter)};
                case "collect_slag":return new[]{P(EnemyActionType.Load,1),P(B,8)};
                case "heavy_swing":return new[]{P(A,11)};
                case "add_to_crucible":return new[]{P(EnemyActionType.Load,1),P(A,6)};
                case "molten_spill":return new[]{P(A,18),P(V,1),P(EnemyActionType.LoadRelease,m.counter)};
                case "redline_slash":return new[]{P(A,10),P(H,1)};
                case "boost_servos":return new[]{P(S,1),P(H,1),P(B,6)};
                case "overdrive_assault":return Pv(1) switch{3=>new[]{P(A,10,2)},2=>new[]{P(A,15),P(B,5)},_=>new[]{P(A,11)}};
                case "emergency_vent":return new[]{P(X,m.counter),P(EnemyActionType.LoseStrength,1),P(B,10)};
                case "masterwork_blow":return new[]{P(A,Pv(14+m.counter)),P(H,1)};
                case "stoke_the_line":return new[]{P(EnemyActionType.StrengthMinions,1),P(H,1),P(B,8)};
                case "fm_command":return new[]{P(EnemyActionType.Command,1),P(B,7)};
                case "reassemble":return new[]{P(EnemyActionType.Summon,1),P(B,8)};
                case "forge_overload":return new[]{P(A,11,2),P(X,m.counter)};
                case "hammer_strike":return new[]{P(A,9)};
                case "overhead_smash":return new[]{P(A,13)};
                case "shield_ram":return new[]{P(A,6),P(B,6)};
                case "reinforce_master":return new[]{P(EnemyActionType.BlockOwner,10),P(B,5)};
                case "iron_press":return new[]{P(A,13),P(B,14)};
                case "heated_press":return new[]{P(A,16),P(B,9)};
                case "molten_press":return new[]{P(A,19),P(B,5)};
                case "melt_layer":return new[]{P(B,m.counter>=3?18:m.counter==2?13:8)};
                case "core_slam":return new[]{P(A,22)};
                case "liquid_metal_sweep":return new[]{P(A,10,2)};
                case "core_flare":return new[]{P(S,2),P(B,7)};
                case "precision_strike":return new[]{P(A,17)};
                case "twin_cutter":return new[]{P(A,8,2)};
                case "reinforced_shell":return new[]{P(B,22)};
                case "counterframe":return new[]{P(A,8),P(B,14)};
                case "redline_burst":return new[]{P(A,19),P(S,1)};
                case "system_surge":return new[]{P(A,7,3)};
                case "restrained_strike":return new[]{P(A,14),P(H,1)};
                case "assembly_guard":return new[]{P(B,18),P(H,1)};
                case "sacred_production":return m.plannedValue==1?new[]{P(EnemyActionType.Summon,1)}:new[]{P(EnemyActionType.StrengthMinions,1),P(EnemyActionType.BlockMinions,8)};
                case "furnace_prayer":return new[]{P(S,1),P(H,1),P(B,8)};
                case "furnace_blade":return new[]{P(A,Pv(16+m.counter)),P(H,1)};
                case "heated_guard":return new[]{P(A,8),P(B,13),P(H,1)};
                case "saints_advance":return new[]{P(A,9,2),P(H,1)};
                case "controlled_vent":return new[]{P(X,m.counter),P(B,15),P(S,1)};
                case "redline_judgment":return new[]{P(A,10,3),P(X,m.counter)};
                case "burning_guard":return new[]{P(A,11),P(B,15)};
                case "violent_reheat":return new[]{P(H,2),P(S,1),P(B,8)};
                case "saint_breaker":return new[]{P(A,21),P(S,1)};
                case "furnace_barrage":return new[]{P(A,6,4)};
                case "service_blade":return new[]{P(A,8)};
                case "reinforce_saint":return new[]{P(EnemyActionType.BlockOwner,10)};
            }
            return HollowActions(move,m);
        }
        private string FoundryMoveLabel(string move,WildMind m)=>move switch
        {
            "hammer_blow"=>"HAMMER BLOW","temper_plate"=>"TEMPER PLATE","heated_strike"=>"HEATED STRIKE","fh_vent"=>"VENT",
            "furnace_sprint"=>"FURNACE SPRINT","redline_pounce"=>m.plannedValue>=16?"REDLINE POUNCE · OVERHEATED":"REDLINE POUNCE","heated_bite"=>"HEATED BITE","cool_down"=>"COOL DOWN",
            "rivet_armor"=>"RIVET ARMOR","stoke_furnace"=>"STOKE FURNACE","reinforce_frame"=>"REINFORCE FRAME","emergency_repair"=>"EMERGENCY REPAIR",
            "chain_lash"=>"CHAIN LASH","binding_chain"=>"BINDING CHAIN","drag_forward"=>"DRAG FORWARD","crushing_links"=>"CRUSHING LINKS",
            "deploy_drone"=>"DEPLOY DRONE","tool_strike"=>"TOOL STRIKE","repair_drone"=>"REPAIR DRONE","am_command"=>"COMMAND","overclock_drone"=>"OVERCLOCK DRONE",
            "cutter"=>"CUTTER","plate_weld"=>"PLATE WELD",
            "reinforced_guard"=>"REINFORCED GUARD","measured_blade"=>"MEASURED BLADE","heated_cleave"=>"HEATED CLEAVE","molten_guard"=>"MOLTEN GUARD","crucible_breaker"=>"CRUCIBLE BREAKER",
            "collect_slag"=>"COLLECT SLAG","heavy_swing"=>"HEAVY SWING","add_to_crucible"=>m.counter+1>=MaxLoad?"ADD TO CRUCIBLE · SPILL NEXT":"ADD TO CRUCIBLE","molten_spill"=>"MOLTEN SPILL",
            "redline_slash"=>"REDLINE SLASH","boost_servos"=>"BOOST SERVOS",
            "overdrive_assault"=>m.plannedValue==3?"OVERDRIVE ASSAULT · OVERHEATED":m.plannedValue==2?"OVERDRIVE ASSAULT · HOT":"OVERDRIVE ASSAULT","emergency_vent"=>"EMERGENCY VENT",
            "masterwork_blow"=>"MASTERWORK BLOW","stoke_the_line"=>"STOKE THE LINE","fm_command"=>"COMMAND","reassemble"=>"REASSEMBLE","forge_overload"=>"FORGE OVERLOAD",
            "hammer_strike"=>"HAMMER STRIKE","overhead_smash"=>"OVERHEAD SMASH","shield_ram"=>"SHIELD RAM","reinforce_master"=>"REINFORCE MASTER",
            "iron_press"=>"IRON PRESS","heated_press"=>"HEATED PRESS","molten_press"=>"MOLTEN PRESS",
            "melt_layer"=>m.counter<=1?"MELT LAYER · CORE EXPOSED NEXT":$"MELT LAYER · ARMOR {m.counter}→{m.counter-1}",
            "core_slam"=>"CORE SLAM","liquid_metal_sweep"=>"LIQUID METAL SWEEP","core_flare"=>"CORE FLARE",
            "precision_strike"=>"PRECISION STRIKE","twin_cutter"=>"TWIN CUTTER · DEFENSE NEXT","reinforced_shell"=>"REINFORCED SHELL","counterframe"=>"COUNTERFRAME · OVERDRIVE NEXT",
            "redline_burst"=>"REDLINE BURST","system_surge"=>"SYSTEM SURGE · ASSAULT NEXT",
            "restrained_strike"=>"RESTRAINED STRIKE","assembly_guard"=>"ASSEMBLY GUARD","sacred_production"=>m.plannedValue==1?"SACRED PRODUCTION · SUMMON":"SACRED PRODUCTION",
            "furnace_prayer"=>"FURNACE PRAYER","furnace_blade"=>"FURNACE BLADE","heated_guard"=>"HEATED GUARD","saints_advance"=>"SAINT'S ADVANCE","controlled_vent"=>"CONTROLLED VENT",
            "redline_judgment"=>"REDLINE JUDGMENT","burning_guard"=>"BURNING GUARD","violent_reheat"=>"VIOLENT REHEAT","saint_breaker"=>"SAINT BREAKER","furnace_barrage"=>"FURNACE BARRAGE",
            "service_blade"=>"SERVICE BLADE","reinforce_saint"=>"REINFORCE SAINT",
            _=>HollowMoveLabel(move,m)
        };
        // Presentation hook names for future animation / VFX (CombatEventKind.Hook "HOOK:<name>").
        private static string FoundryHook(string move)=>move switch
        {
            "redline_pounce"=>"furnace_hound_redline_pounce","stoke_furnace"=>"rivet_priest_stoke",
            "deploy_drone"=>"assembly_master_summon","repair_drone"=>"assembly_master_repair","am_command"=>"assembly_master_command",
            "molten_spill"=>"molten_carrier_spill","overdrive_assault"=>"redline_automaton_overdrive",
            "forge_overload"=>"forgemaster_overload","reassemble"=>"forgemaster_reassemble","fm_command"=>"forgemaster_command",
            "sacred_production"=>"iron_saint_sacred_production","redline_judgment"=>"iron_saint_redline_judgment",
            _=>HollowHook(move)
        };

        // ---------- resolution ----------
        // Returns true when the enemy keeps (or sets) its own pattern position.
        private bool BeginFoundryAction(WildMind m,string move)
        {
            switch(enemyId)
            {
                case CrimsonFoundryContent.Hound:
                    if(move=="cool_down"){m.flag=false;return true;}
                    if(move=="redline_pounce"&&m.plannedValue>=16)m.flag=true; // must Cool Down next
                    return m.hold;
                case CrimsonFoundryContent.Automaton:
                    if(move=="emergency_vent"){m.flag=false;return true;}
                    if(move=="overdrive_assault"&&m.plannedValue==3)m.flag=true; // must vent next
                    return m.hold;
                case CrimsonFoundryContent.Forgehand:case CrimsonFoundryContent.Forgemaster:
                    if(move=="reassemble")m.flag=true; // one replacement per combat
                    return m.hold;
                case CrimsonFoundryContent.Carrier:
                    if(move=="molten_spill"){m.step=0;return true;}
                    return false;
                case CrimsonFoundryContent.Smelter:
                    if(move!="melt_layer")return false;
                    m.counter=Math.Max(0,m.counter-1);m.step=0;EmitHook("smelter_layer_break");
                    if(m.counter==0)EmitHook("smelter_exposed_core");
                    return true;
                case CrimsonFoundryContent.Prototype:
                {
                    var before=Cycle(m,6)/2;m.step++;var after=Cycle(m,6)/2;
                    m.state=after switch{0=>"ASSAULT",1=>"DEFENSE",_=>"OVERDRIVE"};
                    if(after!=before)EmitHook("prototype_zero_mode:"+m.state);
                    return true;
                }
                case CrimsonFoundryContent.Saint:
                    if(move=="sacred_production"&&m.plannedValue==1)m.flag=true; // the one Phase 1 resummon
                    return m.hold;
            }
            return BeginHollowAction(m,move);
        }
        private void GainHeat(int index,int amount)
        {
            var m=MindAt(index);if(m==null||amount<=0)return;var id=EnemyIdAt(index);
            var before=m.counter;m.counter=Math.Min(MaxHeat,m.counter+amount);if(m.counter==before)return;
            InEnemyContext(index,()=>
            {
                Emit(CombatEventKind.Status,m.counter-before,false,null,"HEAT");EmitHook("foundry_heat_gain");
                if(m.counter>=MaxHeat)EmitHook("foundry_overheated");
                if(id==CrimsonFoundryContent.Knight&&KnightBand(before)!=KnightBand(m.counter))EmitHook("crucible_knight_state:"+KnightBand(m.counter));
            });
        }
        private void VentHeat(int index)
        {
            var m=MindAt(index);if(m==null)return;var id=EnemyIdAt(index);var before=m.counter;m.counter=0;
            InEnemyContext(index,()=>
            {
                if(before>0)Emit(CombatEventKind.Status,-before,false,null,"HEAT");EmitHook("foundry_vent");
                if(id==CrimsonFoundryContent.Knight&&KnightBand(before)!=KnightBand(0))EmitHook("crucible_knight_state:COLD");
            });
        }
        private void ExecuteFoundryAction(PlannedEnemyAction action)
        {
            var self=enemyContextIndex;
            switch(action.type)
            {
                case EnemyActionType.Heat:GainHeat(self,action.amount);break;
                case EnemyActionType.HeatVent:VentHeat(self);break;
                case EnemyActionType.Load:
                {
                    var m=Mind;var before=m.counter;m.counter=Math.Min(MaxLoad,m.counter+action.amount);
                    if(m.counter>before)Emit(CombatEventKind.Status,m.counter-before,false,null,"LOAD");break;
                }
                case EnemyActionType.LoadRelease:{var m=Mind;if(m.counter>0)Emit(CombatEventKind.Status,-m.counter,false,null,"LOAD");m.counter=0;break;}
                case EnemyActionType.LoseStrength:
                {
                    var loss=Math.Min(enemy.strength,action.amount);if(loss>0){enemy.strength-=loss;Emit(CombatEventKind.Status,-loss,false,null,"STRENGTH");}break;
                }
                case EnemyActionType.BlockLowestAlly:
                {
                    var allies=LivingAllies(self,false).Where(i=>DamageCapable(EnemyIdAt(i))).ToArray();if(allies.Length==0)break;
                    var target=allies.OrderBy(i=>EnemyAt(i).hp).ThenBy(i=>i).First();
                    InEnemyContext(target,()=>{enemy.block+=action.amount;Emit(CombatEventKind.Block,action.amount,false);});break;
                }
                case EnemyActionType.HeatAlly:
                {
                    // The hottest ally below 4 Heat is stoked; ties are broken at random.
                    var options=StokeTargets(self).ToArray();wildTarget=-1;
                    if(options.Length==0){var any=LivingAllies(self,false).Where(i=>DamageCapable(EnemyIdAt(i))).ToArray();if(any.Length>0)wildTarget=any[NextRandom(any.Length)];break;}
                    var top=options.Max(i=>MindAt(i).counter);var hottest=options.Where(i=>MindAt(i).counter==top).ToArray();
                    wildTarget=hottest[NextRandom(hottest.Length)];GainHeat(wildTarget,action.amount);break;
                }
                case EnemyActionType.HealDamagedAlly:
                {
                    var hurt=DamagedAllies(self).ToArray();wildTarget=-1;if(hurt.Length==0)break;
                    wildTarget=hurt.OrderBy(i=>EnemyAt(i).hp).ThenBy(i=>i).First();HealEnemyAt(wildTarget,action.amount);break;
                }
                case EnemyActionType.StrengthRandomMinion:
                {
                    var owned=OwnedMinions(self).ToArray();wildTarget=-1;if(owned.Length==0)break;
                    wildTarget=owned[NextRandom(owned.Length)];
                    InEnemyContext(wildTarget,()=>{var gain=EnemyBuffAfterWither(action.amount);enemy.strength+=gain;Emit(CombatEventKind.Status,gain,false,null,"STRENGTH");});break;
                }
                default:ExecuteHollowAction(action);break;
            }
        }

        // ---------- deaths and thresholds ----------
        private bool FoundryReplanOnDeath(int k)
        {
            var mk=opponents[k].mind;
            return mk.planned is "am_command" or "repair_drone" or "overclock_drone" or "fm_command" or "rivet_armor" or "stoke_furnace" or "emergency_repair" or "reinforce_frame"
                ||mk.planned=="sacred_production"||HollowReplanOnDeath(k);
        }
        private bool CheckFoundryThresholds(int index)
        {
            var o=opponents[index];var m=o.mind;var f=o.fighter;
            if(o.id!=CrimsonFoundryContent.Saint)return CheckHollowThresholds(index);
            // 2/3 and 1/3 of maximum health: 200 and 100 at the base 300.
            var target=f.hp*3<=f.maxHp?3:f.hp*3<=f.maxHp*2?2:1;
            while(m.phase<target)
            {
                m.phase++;m.step=0;m.hold=false;
                if(m.phase==2)
                {
                    // Chains snap. No heal, Strength or Fortify. Heat resets to 1; the Servitor is destroyed for nothing.
                    m.counter=1;InEnemyContext(index,()=>EmitHook("iron_saint_phase2"));
                    for(var k=0;k<opponents.Count;k++)
                        if(opponents[k].fighter.hp>0&&opponents[k].mind?.ownerUid==m.uid)
                        {
                            opponents[k].fighter.hp=0;opponents[k].fighter.block=0;var mk=opponents[k].mind;mk.fled=true;mk.dead=true;mk.diedTurn=turn;
                            InEnemyContext(k,()=>EmitHook("saint_servitor_destroyed"));
                        }
                }
                else
                {
                    // Armor tears away. No heal, Block, Strength or Fortify. It begins Phase 3 Overheated.
                    m.counter=MaxHeat;InEnemyContext(index,()=>{EmitHook("iron_saint_phase3");EmitHook("foundry_overheated");});
                }
                bossPhase=m.phase;memory.resolvedBossPhase=m.phase;
                if(PlayerPhaseReplan)ReplanAt(index);
            }
            return true;
        }

        // ---------- presentation ----------
        // The one small counter shown beside a themed enemy (Heat, Load, Armor, Strings, Pressure or mode).
        public bool WildCounter(int index,out string label,out int value,out int max)
        {
            label="";value=max=0;var m=MindAt(index);if(m==null)return false;
            switch(EnemyIdAt(index))
            {
                case var id when CrimsonFoundryContent.UsesHeat(id):label="HEAT";value=m.counter;max=MaxHeat;return true;
                case CrimsonFoundryContent.Carrier:label="LOAD";value=m.counter;max=MaxLoad;return true;
                case CrimsonFoundryContent.Smelter:label="ARMOR";value=m.counter;max=SmelterLayers;return true;
                case CrimsonFoundryContent.Prototype:label=m.state;value=Cycle(m,6)%2+1;max=2;return true;
                case DrownedQuarterContent.Marionette:label="STRINGS";value=m.counter;max=3;return true;
                case DrownedQuarterContent.Engine:label="PRESSURE";value=m.counter;max=4;return true;
            }
            return HollowCounter(index,m,out label,out value,out max);
        }
        private const string HeatHelp="Certain actions increase Heat. High Heat may strengthen this enemy's actions. Some enemies Vent to reduce or reset Heat.";
        private void FoundryStateText(int index,WildMind m,List<string> lines)
        {
            var id=EnemyIdAt(index);
            if(CrimsonFoundryContent.UsesHeat(id))lines.Add($"HEAT · {m.counter}/4"+(m.counter>=MaxHeat?" · OVERHEATED":"")+" — "+HeatHelp);
            switch(id)
            {
                case CrimsonFoundryContent.Forgehand:lines.Add("Heated Strike deals 10 + 2 per Heat. At 4 Heat it Vents next.");break;
                case CrimsonFoundryContent.Hound:lines.Add("Redline Pounce deals 16 instead of 12 at 4 Heat, then it must Cool Down.");break;
                case CrimsonFoundryContent.RivetPriest:lines.Add("SUPPORT · Never attacks. Stokes allies' Heat and rivets on armor. Flees if only harmless Supports remain.");break;
                case CrimsonFoundryContent.AssemblyMaster:lines.Add($"SUMMONER · {OwnedMinions(index).Count()}/2 Scrap Drones.");break;
                case CrimsonFoundryContent.Knight:lines.Add("STANCE · "+KnightBand(m.counter)+" — Cold (0–1 Heat) defends, Hot (2–3) attacks, Overheated (4) uses Crucible Breaker.");break;
                case CrimsonFoundryContent.Carrier:lines.Add($"LOAD · {m.counter}/3 — at 3 Load its next action is Molten Spill (18 + Vulnerable).");break;
                case CrimsonFoundryContent.Automaton:lines.Add("Overdrive Assault: 11 at 0–1 Heat, 15 + 5 Block at 2–3, 10 × 2 at 4 (then Emergency Vent).");break;
                case CrimsonFoundryContent.Forgemaster:lines.Add("DRONES · "+(m.flag?"Its one Reassemble is used.":"Can Reassemble one destroyed Drone, once.")+" At 4 Heat it uses Forge Overload.");break;
                case CrimsonFoundryContent.Smelter:
                    lines.Add(m.counter>0?$"ARMOR · {m.counter}/3 — each Melt Layer burns one away. No layer is ever restored."
                        :"EXPOSED CORE · Its armor is gone. Core Slam, Core Flare, Liquid Metal Sweep.");break;
                case CrimsonFoundryContent.Prototype:
                    lines.Add($"MODE · {m.state} ({Cycle(m,6)%2+1}/2) — Assault → Defense → Overdrive, two actions each.");break;
                case CrimsonFoundryContent.Saint:
                    lines.Add($"PHASE {m.phase} · "+(m.phase==1?"Restrained Saint":m.phase==2?"Freed Iron Saint":"Overheated Final Weapon")
                        +(m.phase<3?" — breaks further loose at "+(m.phase==1?"2/3":"1/3")+" health.":" — Redline Judgment at 4 Heat."));break;
                default:HollowStateText(index,m,lines);break;
            }
        }
        private string DescribeFoundryAction(EnemyActionType type,int amount,EnemyIntentAction a)
        {
            switch(type)
            {
                case EnemyActionType.Heat:a.title="HEAT";return $"Gains {amount} Heat (maximum 4). "+HeatHelp;
                case EnemyActionType.HeatVent:a.title="VENT";return "Resets its Heat to 0.";
                case EnemyActionType.HeatAlly:a.title="STOKE";return $"Its hottest ally below 4 Heat gains {amount} Heat.";
                case EnemyActionType.Load:a.title="LOAD";return $"Gains {amount} Load (maximum 3). At 3 Load its next action is Molten Spill.";
                case EnemyActionType.LoadRelease:a.title="SPILL";return "Its Load resets to 0.";
                case EnemyActionType.LoseStrength:a.title="COOL";return $"Loses {amount} Strength (never below 0).";
                case EnemyActionType.BlockLowestAlly:a.title="RIVET ARMOR";return $"Its lowest-health ally that can attack gains {amount} Block.";
                case EnemyActionType.HealDamagedAlly:a.title="REPAIR";return $"Its lowest-health damaged ally heals {amount} HP.";
                case EnemyActionType.StrengthRandomMinion:a.title="OVERCLOCK";return $"One random Minion it owns gains {amount} Strength.";
            }
            return DescribeHollowAction(type,amount,a);
        }
    }
}
