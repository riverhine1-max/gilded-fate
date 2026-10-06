using System;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Core;

namespace GildedFate.Combat
{
    // Act 2 · Theme 2: Hollowwood AI. Plugs into the shared themed-enemy engine (WildCombat.cs) through
    // the Foundry chain. Identity: Growth (0–3), an enemy-specific counter stored in WildMind.counter.
    // It is not Strength, Block or a status. Every Growth-dependent decision is made when the intent is
    // chosen, so the intent the player sees is the intent that lands.
    //
    // WildMind use here:
    //   counter  = Growth (0–3) for Growth users
    //   state    = Bloomfang CLOSED/BLOOMED · Parasite HOSTED/EXPOSED · Elder Husk ROOTED/BLOOMING/WITHERED
    //              · Root Snare LOOSE/TIGHT · Pale Gardener's pending Seed (THORN/WARD/ROT/BLOOM, "" = none)
    //   lastSeed = the Seed planted last (never planted twice in a row)
    //   phase    = Walking Grove stage / Heartroot phase
    //   flag     = one-shot (Stag must Recover next, Garden Mother's one Replace the Fallen used)
    //   step2    = Brood Pod: actions since its last Command
    //   hold     = this move is an override that keeps the pattern position
    public sealed partial class CombatState
    {
        public const int MaxGrowth=3;
        private const string GrowthHelp="Certain actions increase Growth (maximum 3). At 3 Growth this enemy uses its Bloom action, then Growth resets.";

        private static void ResetHollowState(WildMind m,string id)
        {
            if(HollowwoodContent.UsesGrowth(id))m.counter=HollowwoodContent.StartingGrowth(id);
            switch(id)
            {
                case HollowwoodContent.Bloomfang:m.state="CLOSED";break;
                case HollowwoodContent.Parasite:m.state="HOSTED";break;
                case HollowwoodContent.ElderHusk:m.state="ROOTED";break;
                case HollowwoodContent.RootSnare:m.state="LOOSE";break;
                case HollowwoodContent.PaleGardener:m.state="";m.lastSeed="";break;
            }
            ResetObservatoryState(m,id);
        }
        private static string SeedName(string seed)=>seed switch{"THORN"=>"THORN SEED","WARD"=>"WARD SEED","ROT"=>"ROT SEED","BLOOM"=>"BLOOM SEED",_=>""};

        // ---------- move choice ----------
        private string ChooseHollowMove(WildMind m)
        {
            var self=enemyContextIndex;
            switch(enemyId)
            {
                case HollowwoodContent.Sproutling:
                    if(m.counter>=MaxGrowth){m.hold=true;return "sp_bloom_burst";}
                    return Cycle(m,3)==1?"sp_curl_leaves":"sp_root_peck";
                case HollowwoodContent.Stag:
                    if(m.flag){m.hold=true;return "stag_rooted_recovery";}
                    if(m.counter>=MaxGrowth){m.hold=true;return "stag_crown_bloom";}
                    return Cycle(m,3)==1?"stag_bark_guard":"stag_antler_bash";
                case HollowwoodContent.Sporekeeper:
                    switch(Cycle(m,4))
                    {
                        case 0:return GrowthTargets(self).Any()?"sk_feed_bloom":"sk_spore_veil";
                        case 1:return "sk_spore_veil";
                        case 2:if(DamagedAllies(self).Any())return "sk_mist";goto default;
                        default:m.plannedValue=GrowthTargets(self).Any()?1:0;return "sk_mature";
                    }
                case HollowwoodContent.RootSnare:return Cycle(m,4) switch{0=>"rs_entangle",1=>"rs_root_crush",2=>"rs_splinter_snap",_=>"rs_root_crush"};
                case HollowwoodContent.BroodPod:
                    if(m.counter>=MaxGrowth){m.hold=true;return CanSummon(self)?"pod_hatch":"pod_overgrown_shell";}
                    switch(Cycle(m,3))
                    {
                        case 1:return m.step2>=3&&EligibleCommandTargets(self).Any()?"pod_command":"pod_spore_lash";
                        default:return "pod_incubate";
                    }
                case HollowwoodContent.Sporeling:return Cycle(m,2)==0?"sl_spore_bite":"sl_puff_spores";
                case HollowwoodContent.Bloomfang:
                    if(m.state=="BLOOMED")return Cycle(m,3) switch{0=>"bf_blooming_rend",1=>"bf_pollen_claw",_=>"bf_thorn_frenzy"};
                    return Cycle(m,3) switch{0=>"bf_bud_bite",1=>"bf_petal_guard",_=>"bf_thorned_step"};
                case HollowwoodContent.Parasite:
                    if(m.state=="EXPOSED")return Cycle(m,3) switch{0=>"pa_skittering_bite",1=>"pa_drain",_=>"pa_frenzied_lunge"};
                    return Cycle(m,3) switch{0=>"pa_host_swipe",1=>"pa_dead_shell",_=>"pa_parasitic_pull"};
                case HollowwoodContent.ElderHusk:return m.state switch{"BLOOMING"=>"eh_elder_bloom","WITHERED"=>"eh_dry_collapse",_=>"eh_ancient_guard"};
                case HollowwoodContent.GardenMother:
                    if(m.counter>=MaxGrowth){m.hold=true;return "gm_grand_bloom";}
                    if(!m.flag&&CanReplaceFallen(self)){m.hold=true;return "gm_replace";}
                    switch(Cycle(m,4))
                    {
                        case 0:return "gm_cultivate";
                        case 2:return EligibleCommandTargets(self).Any()?"gm_command":"gm_vine_lash";
                        case 3:return OwnedMinions(self).Any(i=>EnemyAt(i).hp<EnemyAt(i).maxHp)?"gm_mothering_roots":"gm_vine_lash";
                        default:return "gm_vine_lash";
                    }
                case HollowwoodContent.Thornbud:return Cycle(m,2)==0?"tb_thorn_jab":"tb_thorn_spray";
                case HollowwoodContent.Bloombud:return Cycle(m,2)==0?"bb_pollen_guard":"bb_feeding_bloom";
                case HollowwoodContent.Huskbud:return Cycle(m,2)==0?"hb_husk_bash":"hb_thick_husk";
                case HollowwoodContent.WalkingGrove:
                    return m.phase switch
                    {
                        1=>Cycle(m,3) switch{0=>"wg_root_hammer",1=>"wg_sink_deep",_=>"wg_root_sweep"},
                        2=>Cycle(m,3) switch{0=>"wg_branch_crush",1=>"wg_splinter_guard",_=>"wg_canopy_sweep"},
                        _=>Cycle(m,3) switch{0=>"wg_elder_crash",1=>"wg_living_canopy",_=>"wg_falling_grove"}
                    };
                case HollowwoodContent.PaleGardener:
                {
                    var pos=Cycle(m,3);
                    if(pos==0||pos==1&&m.state=="")return "pg_plant_seed";
                    if(pos==1)return m.state switch{"THORN"=>"pg_thorn_seed","WARD"=>"pg_ward_seed","ROT"=>"pg_rot_seed",_=>"pg_bloom_seed"};
                    return "pg_weeding_cut";
                }
                case HollowwoodContent.Heartroot:
                    if(m.phase==1)
                    {
                        if(m.counter>=MaxGrowth){m.hold=true;return "hr_heart_bloom";}
                        return Cycle(m,3) switch{0=>"hr_root_lash",1=>"hr_ancient_bark",_=>"hr_sap_draw"};
                    }
                    if(m.phase==2)
                    {
                        if(m.counter>=MaxGrowth){m.hold=true;return "hr_spreading_bloom";}
                        return Cycle(m,3) switch{0=>"hr_uprooting_claw",1=>"hr_blooming_guard",_=>"hr_parasitic_pull"};
                    }
                    if(m.counter>=MaxGrowth){m.hold=true;return "hr_final_bloom";}
                    return Cycle(m,3) switch{0=>"hr_heart_rend",1=>"hr_thornstorm",_=>"hr_predator_bloom"};
            }
            return ChooseObservatoryMove(m);
        }
        // Allies a Sporekeeper can accelerate: damage-capable Growth users still below 3.
        private IEnumerable<int> GrowthTargets(int self)=>LivingAllies(self,false).Where(i=>
        {
            var id=EnemyIdAt(i);var mi=MindAt(i);
            return DamageCapable(id)&&HollowwoodContent.UsesGrowth(id)&&mi!=null&&mi.counter<MaxGrowth&&!(id==HollowwoodContent.Bloomfang&&mi.state=="BLOOMED");
        }).ToArray();
        // Replace the Fallen: a starting Minion of this Mother has died, and there is a slot for one Huskbud.
        private bool CanReplaceFallen(int self)
        {
            var uid=MindAt(self)?.uid??-99;
            var fallen=opponents.Any(o=>o.mind!=null&&o.mind.ownerUid==uid&&o.mind.dead&&!o.mind.fled&&o.fighter.hp<=0&&o.id!=HollowwoodContent.Huskbud);
            return fallen&&CanSummon(self);
        }

        // ---------- moves ----------
        private PlannedEnemyAction[] HollowActions(string move,WildMind m)
        {
            const EnemyActionType A=EnemyActionType.Attack,B=EnemyActionType.Block,S=EnemyActionType.Strength,W=EnemyActionType.Weak,V=EnemyActionType.Vulnerable,
                H=EnemyActionType.Heal,G=EnemyActionType.Growth,R=EnemyActionType.GrowthSet;
            switch(move)
            {
                case "sp_root_peck":return new[]{P(A,8),P(G,1)};
                case "sp_curl_leaves":return new[]{P(B,9),P(G,1)};
                case "sp_bloom_burst":return new[]{P(A,16),P(W,1),P(R,0)};
                case "stag_antler_bash":return new[]{P(A,11),P(G,1)};
                case "stag_bark_guard":return new[]{P(B,15)};
                case "stag_rooted_recovery":return new[]{P(H,6),P(B,6)};
                case "stag_crown_bloom":return new[]{P(A,16),P(B,10),P(R,1)};
                case "sk_feed_bloom":return new[]{P(EnemyActionType.GrowthAlly,1),P(EnemyActionType.BlockTarget,5)};
                case "sk_spore_veil":return new[]{P(EnemyActionType.BlockAllies,7)};
                case "sk_mist":return new[]{P(EnemyActionType.HealDamagedAlly,6),P(EnemyActionType.BlockTarget,4)};
                case "sk_mature":return m.plannedValue==1?new[]{P(EnemyActionType.GrowthAlly,1),P(EnemyActionType.BlockOtherAllies,4)}:new[]{P(EnemyActionType.BlockAllies,6)};
                case "rs_entangle":return new[]{P(W,1),P(B,11)};
                case "rs_root_crush":return new[]{P(A,17)};
                case "rs_splinter_snap":return new[]{P(A,10),P(B,6)};
                case "pod_incubate":return new[]{P(G,1),P(B,7)};
                case "pod_spore_lash":return new[]{P(A,8)};
                case "pod_hatch":return new[]{P(EnemyActionType.Summon,1),P(R,0)};
                case "pod_overgrown_shell":return new[]{P(B,16),P(R,0)};
                case "pod_command":return new[]{P(EnemyActionType.Command,1),P(B,5)};
                case "sl_spore_bite":return new[]{P(A,6)};
                case "sl_puff_spores":return new[]{P(A,4),P(W,1)};
                case "bf_bud_bite":return new[]{P(A,9),P(G,1)};
                case "bf_petal_guard":return new[]{P(B,11),P(G,1)};
                case "bf_thorned_step":return new[]{P(A,7),P(B,5),P(G,1)};
                case "bf_blooming_rend":return new[]{P(A,15)};
                case "bf_pollen_claw":return new[]{P(A,10),P(V,1)};
                case "bf_thorn_frenzy":return new[]{P(A,8,2)};
                case "pa_host_swipe":return new[]{P(A,11)};
                case "pa_dead_shell":return new[]{P(B,14)};
                case "pa_parasitic_pull":return new[]{P(A,8),P(H,4)};
                case "pa_skittering_bite":return new[]{P(A,14)};
                case "pa_drain":return new[]{P(A,10),P(H,5)};
                case "pa_frenzied_lunge":return new[]{P(A,7,2)};
                case "eh_ancient_guard":return new[]{P(B,18),P(H,4)};
                case "eh_elder_bloom":return new[]{P(A,15),P(S,1)};
                case "eh_dry_collapse":return new[]{P(A,10),P(W,1),P(B,5)};
                case "gm_cultivate":return new[]{P(EnemyActionType.StrengthRandomMinion,1),P(G,1),P(B,7)};
                case "gm_mothering_roots":return new[]{P(EnemyActionType.HealMinion,7),P(EnemyActionType.BlockTarget,8)};
                case "gm_vine_lash":return new[]{P(A,14),P(G,1)};
                case "gm_command":return new[]{P(EnemyActionType.Command,1),P(B,6)};
                case "gm_replace":return new[]{P(EnemyActionType.Summon,1),P(B,7)};
                case "gm_grand_bloom":return new[]{P(A,16),P(EnemyActionType.StrengthMinions,1),P(R,1)};
                case "tb_thorn_jab":return new[]{P(A,8)};
                case "tb_thorn_spray":return new[]{P(A,5,2)};
                case "bb_pollen_guard":return new[]{P(EnemyActionType.BlockOwner,8),P(B,4)};
                case "bb_feeding_bloom":return new[]{P(EnemyActionType.HealOwner,5)};
                case "hb_husk_bash":return new[]{P(A,6),P(B,6)};
                case "hb_thick_husk":return new[]{P(EnemyActionType.BlockOwner,10)};
                case "wg_root_hammer":return new[]{P(A,14),P(B,8)};
                case "wg_sink_deep":return new[]{P(B,22),P(H,5)};
                case "wg_root_sweep":return new[]{P(A,7,2)};
                case "wg_branch_crush":return new[]{P(A,18)};
                case "wg_splinter_guard":return new[]{P(A,8),P(B,14)};
                case "wg_canopy_sweep":return new[]{P(A,6,3)};
                case "wg_elder_crash":return new[]{P(A,21)};
                case "wg_living_canopy":return new[]{P(B,16),P(S,1)};
                case "wg_falling_grove":return new[]{P(A,11,2)};
                case "pg_plant_seed":return new[]{P(B,9),P(EnemyActionType.PlantSeed,1)};
                case "pg_thorn_seed":return new[]{P(A,20)};
                case "pg_ward_seed":return new[]{P(B,26),P(A,6)};
                case "pg_rot_seed":return new[]{P(W,2),P(A,8)};
                case "pg_bloom_seed":return new[]{P(H,8),P(S,1),P(B,8)};
                case "pg_weeding_cut":return new[]{P(A,13)};
                case "hr_root_lash":return new[]{P(A,14),P(G,1)};
                case "hr_ancient_bark":return new[]{P(B,20),P(G,1)};
                case "hr_sap_draw":return new[]{P(H,7),P(B,7)};
                case "hr_heart_bloom":return new[]{P(A,17),P(W,1),P(B,8),P(R,0)};
                case "hr_uprooting_claw":return new[]{P(A,17),P(G,1)};
                case "hr_blooming_guard":return new[]{P(A,8),P(B,15),P(G,1)};
                case "hr_parasitic_pull":return new[]{P(A,12),P(H,5)};
                case "hr_spreading_bloom":return new[]{P(A,10,2),P(V,1),P(R,0)};
                case "hr_heart_rend":return new[]{P(A,21),P(G,1)};
                case "hr_thornstorm":return new[]{P(A,6,4),P(G,1)};
                case "hr_predator_bloom":return new[]{P(A,13),P(S,1),P(G,1)};
                case "hr_final_bloom":return new[]{P(A,10,3),P(B,10),P(R,0)};
            }
            return ObservatoryActions(move,m);
        }
        // "+ BLOOM NEXT" style suffix when this action carries the enemy to 3 Growth.
        private string GrowthTail(WildMind m,string next)=>m.counter+1>=MaxGrowth?" · "+next:"";
        private string HollowMoveLabel(string move,WildMind m)=>move switch
        {
            "sp_root_peck"=>"ROOT PECK"+GrowthTail(m,"BLOOM BURST NEXT"),"sp_curl_leaves"=>"CURL LEAVES"+GrowthTail(m,"BLOOM BURST NEXT"),"sp_bloom_burst"=>"BLOOM BURST",
            "stag_antler_bash"=>"ANTLER BASH"+GrowthTail(m,"CROWN BLOOM NEXT"),"stag_bark_guard"=>"BARK GUARD","stag_rooted_recovery"=>"ROOTED RECOVERY","stag_crown_bloom"=>"CROWN BLOOM",
            "sk_feed_bloom"=>"FEED THE BLOOM","sk_spore_veil"=>"SPORE VEIL","sk_mist"=>"REGENERATIVE MIST","sk_mature"=>"MATURE THE GROVE",
            "rs_entangle"=>"ENTANGLE · ROOTS TIGHTEN","rs_root_crush"=>"ROOT CRUSH","rs_splinter_snap"=>"SPLINTER SNAP",
            "pod_incubate"=>"INCUBATE"+GrowthTail(m,"HATCH NEXT"),"pod_spore_lash"=>"SPORE LASH","pod_hatch"=>"HATCH","pod_overgrown_shell"=>"OVERGROWN SHELL","pod_command"=>"COMMAND",
            "sl_spore_bite"=>"SPORE BITE","sl_puff_spores"=>"PUFF SPORES",
            "bf_bud_bite"=>"BUD BITE"+GrowthTail(m,"FULL BLOOM"),"bf_petal_guard"=>"PETAL GUARD"+GrowthTail(m,"FULL BLOOM"),"bf_thorned_step"=>"THORNED STEP"+GrowthTail(m,"FULL BLOOM"),
            "bf_blooming_rend"=>"BLOOMING REND","bf_pollen_claw"=>"POLLEN CLAW","bf_thorn_frenzy"=>"THORN FRENZY",
            "pa_host_swipe"=>"HOST SWIPE","pa_dead_shell"=>"DEAD SHELL","pa_parasitic_pull"=>"PARASITIC PULL","pa_skittering_bite"=>"SKITTERING BITE","pa_drain"=>"DRAIN","pa_frenzied_lunge"=>"FRENZIED LUNGE",
            "eh_ancient_guard"=>"ANCIENT GUARD · BLOOMING NEXT","eh_elder_bloom"=>"ELDER BLOOM · WITHERED NEXT","eh_dry_collapse"=>"DRY COLLAPSE · ROOTED NEXT",
            "gm_cultivate"=>"CULTIVATE","gm_mothering_roots"=>"MOTHERING ROOTS","gm_vine_lash"=>"VINE LASH"+GrowthTail(m,"GRAND BLOOM NEXT"),"gm_command"=>"COMMAND",
            "gm_replace"=>"REPLACE THE FALLEN","gm_grand_bloom"=>"GRAND BLOOM",
            "tb_thorn_jab"=>"THORN JAB","tb_thorn_spray"=>"THORN SPRAY","bb_pollen_guard"=>"POLLEN GUARD","bb_feeding_bloom"=>"FEEDING BLOOM","hb_husk_bash"=>"HUSK BASH","hb_thick_husk"=>"THICK HUSK",
            "wg_root_hammer"=>"ROOT HAMMER","wg_sink_deep"=>"SINK DEEP","wg_root_sweep"=>"ROOT SWEEP","wg_branch_crush"=>"BRANCH CRUSH","wg_splinter_guard"=>"SPLINTER GUARD","wg_canopy_sweep"=>"CANOPY SWEEP",
            "wg_elder_crash"=>"ELDER CRASH","wg_living_canopy"=>"LIVING CANOPY","wg_falling_grove"=>"FALLING GROVE",
            "pg_plant_seed"=>"PLANT SEED","pg_thorn_seed"=>"THORN SEED","pg_ward_seed"=>"WARD SEED","pg_rot_seed"=>"ROT SEED","pg_bloom_seed"=>"BLOOM SEED","pg_weeding_cut"=>"WEEDING CUT",
            "hr_root_lash"=>"ROOT LASH"+GrowthTail(m,"HEART BLOOM NEXT"),"hr_ancient_bark"=>"ANCIENT BARK"+GrowthTail(m,"HEART BLOOM NEXT"),"hr_sap_draw"=>"SAP DRAW","hr_heart_bloom"=>"HEART BLOOM",
            "hr_uprooting_claw"=>"UPROOTING CLAW"+GrowthTail(m,"SPREADING BLOOM NEXT"),"hr_blooming_guard"=>"BLOOMING GUARD"+GrowthTail(m,"SPREADING BLOOM NEXT"),
            "hr_parasitic_pull"=>"PARASITIC PULL","hr_spreading_bloom"=>"SPREADING BLOOM",
            "hr_heart_rend"=>"HEART REND"+GrowthTail(m,"FINAL BLOOM NEXT"),"hr_thornstorm"=>"THORNSTORM"+GrowthTail(m,"FINAL BLOOM NEXT"),
            "hr_predator_bloom"=>"PREDATOR BLOOM"+GrowthTail(m,"FINAL BLOOM NEXT"),"hr_final_bloom"=>"FINAL BLOOM",
            _=>ObservatoryMoveLabel(move,m)
        };
        // Presentation hook names for future animation / VFX (CombatEventKind.Hook "HOOK:<name>").
        private static string HollowHook(string move)=>move switch
        {
            "sp_bloom_burst"=>"sproutling_bloom_burst","stag_crown_bloom"=>"hollow_stag_crown_bloom",
            "sk_feed_bloom" or "sk_mature"=>"sporekeeper_acceleration","rs_entangle"=>"root_snare_tighten",
            "pod_hatch"=>"brood_pod_hatch","pod_command"=>"brood_pod_command",
            "gm_cultivate"=>"garden_mother_cultivate","gm_grand_bloom"=>"garden_mother_grand_bloom","gm_command"=>"garden_mother_command","gm_replace"=>"garden_mother_huskbud_replacement",
            "hr_heart_bloom"=>"heartroot_heart_bloom","hr_spreading_bloom"=>"heartroot_spreading_bloom","hr_final_bloom"=>"heartroot_final_bloom",
            _=>ObservatoryHook(move)
        };

        // ---------- resolution ----------
        // Returns true when the enemy keeps (or sets) its own pattern position.
        private bool BeginHollowAction(WildMind m,string move)
        {
            switch(enemyId)
            {
                case HollowwoodContent.RootSnare:
                    if(move=="rs_entangle")m.state="TIGHT";else if(move=="rs_root_crush")m.state="LOOSE";
                    return false;
                case HollowwoodContent.BroodPod:
                    if(move=="pod_command")m.step2=0;else m.step2++;
                    return m.hold;
                case HollowwoodContent.Stag:
                    if(move=="stag_crown_bloom")m.flag=true;else if(move=="stag_rooted_recovery")m.flag=false;
                    return m.hold;
                case HollowwoodContent.GardenMother:
                    if(move=="gm_replace")m.flag=true; // one replacement per encounter
                    return m.hold;
                case HollowwoodContent.ElderHusk:
                {
                    m.state=m.state switch{"ROOTED"=>"BLOOMING","BLOOMING"=>"WITHERED",_=>"ROOTED"};
                    EmitHook("elder_husk_state:"+m.state);return true;
                }
                case HollowwoodContent.PaleGardener:
                    if(move is "pg_thorn_seed" or "pg_ward_seed" or "pg_rot_seed" or "pg_bloom_seed")
                    {EmitHook("pale_gardener_seed_resolved:"+m.state);m.state="";}
                    return false;
            }
            return HollowwoodContent.Find(enemyId)!=null?m.hold:BeginObservatoryAction(m,move);
        }
        private void GainGrowth(int index,int amount)
        {
            var m=MindAt(index);if(m==null||amount<=0)return;var id=EnemyIdAt(index);
            if(id==HollowwoodContent.Bloomfang&&m.state=="BLOOMED")return; // Growth is locked after Full Bloom
            var before=m.counter;m.counter=Math.Min(MaxGrowth,m.counter+amount);if(m.counter==before)return;
            InEnemyContext(index,()=>
            {
                Emit(CombatEventKind.Status,m.counter-before,false,null,"GROWTH");EmitHook("growth_gain");
                if(m.counter>=MaxGrowth)EmitHook("growth_three");
            });
        }
        private void SetGrowth(int index,int value)
        {
            var m=MindAt(index);if(m==null)return;value=Math.Max(0,Math.Min(MaxGrowth,value));
            if(EnemyIdAt(index)==HollowwoodContent.Bloomfang&&m.state=="BLOOMED")return;
            var before=m.counter;m.counter=value;if(before==value)return;
            InEnemyContext(index,()=>Emit(CombatEventKind.Status,value-before,false,null,"GROWTH"));
        }
        private void ExecuteHollowAction(PlannedEnemyAction action)
        {
            var self=enemyContextIndex;
            switch(action.type)
            {
                case EnemyActionType.Growth:GainGrowth(self,action.amount);break;
                case EnemyActionType.GrowthSet:SetGrowth(self,action.amount);break;
                case EnemyActionType.GrowthAlly:
                {
                    // The ally closest to 3 Growth is accelerated; ties are broken at random.
                    var options=GrowthTargets(self).ToArray();wildTarget=-1;
                    if(options.Length==0){var any=LivingAllies(self,false).Where(i=>DamageCapable(EnemyIdAt(i))).ToArray();if(any.Length>0)wildTarget=any[NextRandom(any.Length)];break;}
                    var top=options.Max(i=>MindAt(i).counter);var best=options.Where(i=>MindAt(i).counter==top).ToArray();
                    wildTarget=best[NextRandom(best.Length)];GainGrowth(wildTarget,action.amount);break;
                }
                case EnemyActionType.BlockOtherAllies:
                    foreach(var i in LivingAllies(self,false).Where(i=>i!=wildTarget).ToArray())InEnemyContext(i,()=>{enemy.block+=action.amount;Emit(CombatEventKind.Block,action.amount,false);});
                    wildTarget=-1;break;
                case EnemyActionType.HealOwner:
                {
                    var owner=OwnerIndexOf(self);if(owner>=0&&IsLivingTarget(owner))HealEnemyAt(owner,action.amount);break;
                }
                case EnemyActionType.PlantSeed:
                {
                    var m=Mind;var options=new[]{"THORN","WARD","ROT","BLOOM"}.Where(s=>s!=m.lastSeed).ToArray();
                    m.state=options[NextRandom(options.Length)];m.lastSeed=m.state;EmitHook("pale_gardener_seed_planted:"+m.state);break;
                }
                default:ExecuteObservatoryAction(action);break;
            }
        }

        // ---------- deaths and thresholds ----------
        private bool HollowReplanOnDeath(int k)
        {
            var id=opponents[k].id;
            return id is HollowwoodContent.BroodPod or HollowwoodContent.GardenMother or HollowwoodContent.Sporekeeper||ObservatoryReplanOnDeath(k);
        }
        // Heartroot thresholds: 2/3 and 1/3 of maximum health, rounded up (214 and 107 at the base 320).
        // Garden-style stages: Walking Grove 2/3 and 1/3 rounded down (118 and 59 at the base 178).
        // Hollow Parasite: half of maximum health, rounded up (31 at the base 62).
        private bool CheckHollowThresholds(int index)
        {
            var o=opponents[index];var m=o.mind;var f=o.fighter;
            switch(o.id)
            {
                case HollowwoodContent.Bloomfang:
                    if(m.state=="CLOSED"&&m.counter>=MaxGrowth)
                    {
                        // One time, permanent. No heal, Strength or Block; Growth stays locked at 3.
                        m.state="BLOOMED";m.step=0;m.hold=false;InEnemyContext(index,()=>EmitHook("bloomfang_full_bloom"));
                        if(PlayerPhaseReplan)ReplanAt(index);
                    }
                    return true;
                case HollowwoodContent.Parasite:
                    if(m.state=="HOSTED"&&f.hp<=(f.maxHp+1)/2)
                    {
                        m.state="EXPOSED";m.step=0;m.hold=false;InEnemyContext(index,()=>EmitHook("hollow_parasite_host_break"));
                        if(PlayerPhaseReplan)ReplanAt(index);
                    }
                    return true;
                case HollowwoodContent.WalkingGrove:
                {
                    var target=f.hp<=f.maxHp/3?3:f.hp<=f.maxHp*2/3?2:1;
                    while(m.phase<target)
                    {
                        // Transformation only: no heal, Strength or Block.
                        m.phase++;m.step=0;m.hold=false;var stage=m.phase;
                        InEnemyContext(index,()=>EmitHook(stage==2?"walking_grove_stage2":"walking_grove_stage3"));
                        if(PlayerPhaseReplan)ReplanAt(index);
                    }
                    return true;
                }
                case HollowwoodContent.Heartroot:
                {
                    var t1=(f.maxHp*2+2)/3;var t2=(f.maxHp+2)/3;
                    var target=f.hp<=t2?3:f.hp<=t1?2:1;
                    while(m.phase<target)
                    {
                        // The wood tears loose: no heal, Strength, Fortify or Block. Growth is reset by phase.
                        m.phase++;m.step=0;m.hold=false;var phase=m.phase;
                        SetGrowth(index,phase==2?1:0);
                        InEnemyContext(index,()=>EmitHook(phase==2?"heartroot_phase2":"heartroot_phase3"));
                        bossPhase=m.phase;memory.resolvedBossPhase=m.phase;
                        if(PlayerPhaseReplan)ReplanAt(index);
                    }
                    return true;
                }
            }
            return HollowwoodContent.Find(o.id)!=null||CheckObservatoryThresholds(index);
        }

        // ---------- presentation ----------
        // The counter / state pill beside a Hollowwood enemy. max == 0 means a text-only pill.
        private bool HollowCounter(int index,WildMind m,out string label,out int value,out int max)
        {
            label="";value=max=0;
            switch(EnemyIdAt(index))
            {
                case HollowwoodContent.Sproutling:case HollowwoodContent.Stag:case HollowwoodContent.BroodPod:case HollowwoodContent.GardenMother:case HollowwoodContent.Heartroot:
                    label="GROWTH";value=m.counter;max=MaxGrowth;return true;
                case HollowwoodContent.Bloomfang:
                    if(m.state=="BLOOMED"){label="FULL BLOOM";return true;}
                    label="CLOSED · GROWTH";value=m.counter;max=MaxGrowth;return true;
                case HollowwoodContent.Parasite:label=m.state;return true;
                case HollowwoodContent.ElderHusk:label=m.state;return true;
                case HollowwoodContent.RootSnare:label=m.state=="TIGHT"?"TIGHTENED ROOTS":"LOOSE ROOTS";return true;
                case HollowwoodContent.WalkingGrove:label=m.phase==1?"ROOT-WOKEN":m.phase==2?"BRANCH-WOKEN":"CROWN-WOKEN";return true;
                case HollowwoodContent.PaleGardener:label=string.IsNullOrEmpty(m.state)?"NO SEED":"PLANTED SEED: "+SeedName(m.state);return true;
            }
            return ObservatoryCounter(index,m,out label,out value,out max);
        }
        private void HollowStateText(int index,WildMind m,List<string> lines)
        {
            var id=EnemyIdAt(index);
            if(HollowwoodContent.UsesGrowth(id)&&!(id==HollowwoodContent.Bloomfang&&m.state=="BLOOMED"))
                lines.Add($"GROWTH · {m.counter}/3"+(m.counter>=MaxGrowth?" · FULL":"")+" — "+GrowthHelp);
            switch(id)
            {
                case HollowwoodContent.Sproutling:lines.Add("At 3 Growth, uses Bloom Burst (16 + Weak), then Growth resets to 0.");break;
                case HollowwoodContent.Stag:lines.Add("Starts with 1 Growth. At 3 Growth, uses Crown Bloom (16 + 10 Block), then Growth resets to 1 and it uses Rooted Recovery.");break;
                case HollowwoodContent.Sporekeeper:lines.Add("SUPPORT · Never attacks. Feeds the Growth of allies and shields them. Flees if only harmless Supports remain.");break;
                case HollowwoodContent.RootSnare:
                    lines.Add(m.state=="TIGHT"?"TIGHTENED ROOTS · Its roots are tight. Root Crush loosens them.":"LOOSE ROOTS · Entangle tightens them. Root Crush loosens them.");break;
                case HollowwoodContent.BroodPod:lines.Add($"SUMMONER · {OwnedMinions(index).Count()}/2 Sporelings. At 3 Growth, hatches a Sporeling if possible; at the limit it uses Overgrown Shell instead.");break;
                case HollowwoodContent.Bloomfang:
                    lines.Add(m.state=="BLOOMED"?"FULL BLOOM · It has permanently bloomed. Blooming Rend → Pollen Claw → Thorn Frenzy."
                        :"CLOSED · At 3 Growth it permanently transforms into Full Bloom. Nothing is healed or gained by the change.");break;
                case HollowwoodContent.Parasite:
                    lines.Add(m.state=="EXPOSED"?"EXPOSED · The host has broken. Skittering Bite → Drain → Frenzied Lunge."
                        :"HOSTED · When the host breaks (at half health) the parasite is exposed. Nothing is healed or gained by the change.");break;
                case HollowwoodContent.ElderHusk:lines.Add("STATE · "+m.state+" — Rooted → Blooming → Withered → Rooted.");break;
                case HollowwoodContent.GardenMother:
                    lines.Add($"MINIONS · {OwnedMinions(index).Count()}/2. "+(m.flag?"Its one Replace the Fallen is used.":"Can Replace the Fallen once with a Huskbud.")+" At 3 Growth, uses Grand Bloom.");break;
                case HollowwoodContent.Thornbud:case HollowwoodContent.Huskbud:break;
                case HollowwoodContent.Bloombud:lines.Add("Shields and heals its Mother.");break;
                case HollowwoodContent.WalkingGrove:
                    lines.Add($"STAGE {m.phase} · "+(m.phase==1?"Root-Woken":m.phase==2?"Branch-Woken":"Crown-Woken")+(m.phase<3?" — wakes further at "+(m.phase==1?"2/3":"1/3")+" health. No heal, Strength or Block from waking.":""));break;
                case HollowwoodContent.PaleGardener:
                    lines.Add(string.IsNullOrEmpty(m.state)?"SEEDS · It will plant one visible Seed. The Seed resolves on its next action and cannot be cancelled."
                        :"PLANTED SEED: "+SeedName(m.state)+" — "+SeedText(m.state)+" It resolves on its next action and cannot be cancelled.");break;
                case HollowwoodContent.Heartroot:
                    lines.Add($"PHASE {m.phase} · "+(m.phase==1?"The Waking Heart":m.phase==2?"The Spreading Heart":"The Predator Heart")
                        +(m.phase<3?" — tears looser at "+(m.phase==1?"2/3":"1/3")+" health.":" — Final Bloom at 3 Growth."));break;
                default:ObservatoryStateText(index,m,lines);break;
            }
        }
        private static string SeedText(string seed)=>seed switch
        {
            "THORN"=>"Next action: 20 damage.","WARD"=>"Next action: 26 Block, then 6 damage.","ROT"=>"Next action: 2 Weak and 8 damage.","BLOOM"=>"Next action: heals 8, +1 Strength, 8 Block.",_=>""
        };
        private string DescribeHollowAction(EnemyActionType type,int amount,EnemyIntentAction a)
        {
            switch(type)
            {
                case EnemyActionType.Growth:a.title="GROWTH";return $"Gains {amount} Growth (maximum 3). "+GrowthHelp;
                case EnemyActionType.GrowthSet:a.title="BLOOM";return $"Its Growth resets to {amount}.";
                case EnemyActionType.GrowthAlly:a.title="FEED GROWTH";return $"The damage-dealing ally closest to 3 Growth gains {amount} Growth. Ties are random.";
                case EnemyActionType.BlockOtherAllies:a.title="SHIELD ALLIES";return $"Every other living ally, except the one that gains Growth, gains {amount} Block.";
                case EnemyActionType.HealOwner:a.title="FEED OWNER";return $"Its owner heals {amount} HP.";
                case EnemyActionType.PlantSeed:a.title="PLANT SEED";return "Plants one random Seed (never the same twice in a row). The Seed is shown once planted, resolves on its next action and cannot be cancelled.";
            }
            return DescribeObservatoryAction(type,amount,a);
        }
    }
}
