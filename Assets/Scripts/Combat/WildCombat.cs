using System;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Core;

namespace GildedFate.Combat
{
    // Per-enemy AI state for themed enemies (Ashen Wilds and later themes).
    // Stored on each CombatOpponent so it saves with the combat checkpoint.
    [Serializable] public sealed class WildMind
    {
        public int uid,ownerUid=-1,step,step2,phase=1,diedTurn=-1,summonedTurn=-1,plannedValue,counter,held;
        public string state="",planned="",lastMove="",prior="",lastSeed="",queue="",pair="",lastPair="";
        // flag: per-enemy one-shot (Forgemaster Reassemble used, Iron Saint resummon used, must-vent);
        // hold: the planned move is an override that keeps the pattern position.
        public bool minion,dead,mourning,fled,flag,hold;
        public WildMind Copy()=>(WildMind)MemberwiseClone();
    }

    // Shared Owner / Minion / Summon / Command rules plus the Ashen Wilds AI.
    // Every action resolves inside the normal enemy turn: no interrupts, no hidden
    // retaliation. Intents are chosen when the player's turn begins and only change
    // when a rule explicitly says so (a Mourning Elk's packmate dies, a Minion-based
    // move loses its last Minion, an elite or boss crosses an HP threshold).
    public sealed partial class CombatState
    {
        public const int MaxBattlefieldBodies=4;
        public bool wildCombat;public int nextWildUid=1;
        // Fate Debt scaling for creatures summoned mid-fight (set with the rest of Fate Debt).
        public int summonHpPercent=100,summonStrength;
        [NonSerialized] private int wildSweepDepth,wildSinkMute,wildPlanning;[NonSerialized] private int wildTarget=-1;

        public static bool IsWildId(string id)=>ThemeRosters.Find(id)!=null;
        private static EnemyDef WildDef(string id)=>ThemeRosters.Find(id);
        public WildMind MindAt(int index)=>wildCombat&&opponents!=null&&index>=0&&index<opponents.Count?opponents[index].mind:null;
        private WildMind Mind=>MindAt(enemyContextIndex);
        private bool IsWildContext=>wildCombat&&Mind!=null&&IsWildId(enemyId);
        public bool IsMinionAt(int index)=>MindAt(index)?.minion==true;
        public int OwnerIndexOf(int index){var m=MindAt(index);if(m==null||m.ownerUid<0)return -1;for(var i=0;i<EnemyCount;i++)if(MindAt(i)?.uid==m.ownerUid)return i;return -1;}
        public int LivingNonMinionCount=>Enumerable.Range(0,EnemyCount).Count(i=>IsLivingTarget(i)&&!IsMinionAt(i));
        public int NonMinionTotal=>wildCombat?Enumerable.Range(0,EnemyCount).Count(i=>!IsMinionAt(i)):EnemyCount;
        private int LivingBodies=>Enumerable.Range(0,EnemyCount).Count(IsLivingTarget);
        private IEnumerable<int> OwnedMinions(int ownerIndex){var uid=MindAt(ownerIndex)?.uid??-99;return Enumerable.Range(0,EnemyCount).Where(i=>IsLivingTarget(i)&&MindAt(i)?.ownerUid==uid);}
        private IEnumerable<int> EligibleCommandTargets(int ownerIndex)=>OwnedMinions(ownerIndex).Where(i=>{var m=MindAt(i);return !string.IsNullOrEmpty(m.lastMove)&&m.summonedTurn!=turn;});
        private static bool DamageCapable(string id)=>WildDef(id)?.support!=true;
        private static string SummonFor(string id)=>ThemeRosters.SummonType(id);
        private static int MinionCap(string id)=>ThemeRosters.MinionCap(id);

        // ---------- setup ----------
        private void InitializeWildGroup(string[] ids)
        {
            wildCombat=true;opponents=new();enemyContextIndex=0;nextWildUid=1;
            var expanded=new List<(string id,int owner)>();
            foreach(var id in ids)
            {
                var def=WildDef(id)??Array.Find(WorldContent.Enemies,e=>e.id==id);if(def==null)throw new ArgumentException("Unknown enemy "+id);
                if(def.minion)
                {
                    // An authored Minion belongs to the nearest earlier creature that can own it.
                    var owner=-1;for(var k=expanded.Count-1;k>=0;k--)if(ThemeRosters.CanOwn(expanded[k].id,id)){owner=k;break;}
                    if(owner<0)throw new ArgumentException("A Minion cannot appear without its owner: "+id);
                    expanded.Add((id,owner));continue;
                }
                var me=expanded.Count;expanded.Add((id,-1));
                foreach(var minion in ThemeRosters.StartingMinions(id))expanded.Add((minion,me));
            }
            if(expanded.Count>MaxBattlefieldBodies)throw new ArgumentException("Encounters support at most four creatures.");
            if(expanded.Where(e=>!(WildDef(e.id)?.minion??false)).All(e=>WildDef(e.id)?.support==true))throw new ArgumentException("A pure Support cannot start a fight alone.");
            var groupedNormals=expanded.Count(e=>!(WildDef(e.id)?.minion??false))>1;
            var uids=new int[expanded.Count];
            for(var i=0;i<expanded.Count;i++)
            {
                var (id,owner)=expanded[i];var def=WildDef(id)??Array.Find(WorldContent.Enemies,e=>e.id==id);
                var hp=def.minion||def.elite||def.boss||!groupedNormals?def.hp:EncounterContent.GroupHp(def.hp,ThemeRosters.GroupHpPercent(def));
                uids[i]=nextWildUid++;
                var mind=new WildMind{uid=uids[i],ownerUid=owner>=0?uids[owner]:-1,minion=def.minion};
                ResetWildState(mind,id);
                opponents.Add(new CombatOpponent{id=id,baseDamage=def.baseDamage,fighter=new FighterState{hp=hp,maxHp=hp},mind=mind});
            }
            LoadEnemyContext(0);
        }
        private static void ResetWildState(WildMind m,string id)
        {
            m.state=id switch{AshenWildsContent.Maw=>"HUNGRY",AshenWildsContent.Hart=>"CROWNED",AshenWildsContent.Titan=>"ROOTED",_=>""};
            ResetDrownedState(m,id);ResetFoundryState(m,id);
        }

        // ---------- planning ----------
        private void PlanWildIntent()
        {
            var m=Mind;wildPlanning++;
            try
            {
                SweepWilds();
                var move=ChooseWildMove(m);m.planned=move;
                var actions=WildActions(move,m);var label=WildMoveLabel(move,m);
                var attack=actions.FirstOrDefault(a=>a.type==EnemyActionType.Attack);
                if(move==AstrologerPairMove||move==CuratorPairMove||move==FracturePairMove)SetIntent(IntentKind.Buff,0,label); // two possibilities: nothing is promised
                else if(actions.Any(a=>a.type==EnemyActionType.Attack))SetIntent(IntentKind.Attack,attack.amount,label,attack.hits);
                else if(actions.All(a=>a.type==EnemyActionType.Block))SetIntent(IntentKind.Defend,actions.Sum(a=>a.amount),label);
                else SetIntent(IntentKind.Buff,actions.Length>0?actions[0].amount:0,label);
                RefreshWildMechanicText();
            }
            finally{wildPlanning--;}
        }
        private int Cycle(WildMind m,int length)=>((m.step%length)+length)%length;
        private string ChooseWildMove(WildMind m)
        {
            var self=enemyContextIndex;
            switch(enemyId)
            {
                case AshenWildsContent.Cinderfang:
                    switch(Cycle(m,3))
                    {
                        case 0:return "ember_bite";
                        case 1:
                            // Pack Instinct: decided now, and the shown value never changes afterwards.
                            var wounded=Enumerable.Range(0,EnemyCount).Any(i=>i!=self&&IsLivingTarget(i)&&!IsMinionAt(i)&&EnemyAt(i).hp*2<EnemyAt(i).maxHp);
                            m.plannedValue=wounded?12:8;return "pack_pounce";
                        default:return "stoke";
                    }
                case AshenWildsContent.Grazer:
                {
                    var alone=!Enumerable.Range(0,EnemyCount).Any(i=>i!=self&&IsLivingTarget(i)&&!IsMinionAt(i));
                    if(alone)return Cycle(m,3) switch{0=>"headbutt",1=>"rooted_hide",_=>"shed_bark"};
                    return Cycle(m,4) switch{0=>"headbutt",1=>"shelter_herd",2=>"shed_bark",_=>"headbutt"};
                }
                case AshenWildsContent.Emberwing:return Cycle(m,4) switch{0=>"fan_embers",1=>"ash_veil",2=>"feeding_cry",_=>"wild_chorus"};
                case AshenWildsContent.Thornjaw:return Cycle(m,4) switch{0=>"thorn_bite",1=>"bramble_coil",2=>"thornburst",_=>"splinter_rush"};
                case AshenWildsContent.Stalker:
                    switch(Cycle(m,4))
                    {
                        case 0:return "stalking_claw";case 1:return "smoke_mark";
                        case 2:m.plannedValue=player.weak>0||player.vulnerable>0?13:9;return "ambush";
                        default:return "tear_open";
                    }
                case AshenWildsContent.Rootcaller:
                {
                    var owned=OwnedMinions(self).Count();var canGrow=CanSummon(self);
                    switch(Cycle(m,4))
                    {
                        case 0:return owned==0&&canGrow?"grow_sapling":"root_slam";
                        case 1:return "root_slam";
                        case 2:if(owned<2&&canGrow)return "grow_sapling";return EligibleCommandTargets(self).Any()?"rc_command":"root_slam";
                        default:return OwnedMinions(self).Any(i=>EnemyAt(i).hp<EnemyAt(i).maxHp)?"nourish":"root_slam";
                    }
                }
                case AshenWildsContent.Sapling:return Cycle(m,2)==0?"thorn_peck":"curl_roots";
                case AshenWildsContent.Elk:
                    if(m.mourning)return "mourning_cry";
                    return Cycle(m,3) switch{0=>"gore",1=>"stand_vigil",_=>"antler_sweep"};
                case AshenWildsContent.Maw:return m.state switch{"FED"=>"smoldering_belly","BURNING"=>"furnace_eruption",_=>"feeding_bite"};
                case AshenWildsContent.Packmother:
                    if(!OwnedMinions(self).Any())return (((m.step2%3)+3)%3) switch{0=>"bereaved_fury",1=>"alpha_maul",_=>"bereaved_fury"};
                    return Cycle(m,4) switch{0=>"protective_snarl",1=>"alpha_maul",2=>EligibleCommandTargets(self).Any()?"pm_command":"alpha_maul",_=>"feed_pack"};
                case AshenWildsContent.FangPup:return Cycle(m,2)==0?"quick_bite":"frenzy";
                case AshenWildsContent.AshbackCub:return Cycle(m,2)==0?"shoulder_ram":"shelter_mother";
                case AshenWildsContent.Hart:
                    return m.state switch
                    {
                        "SPLINTERED"=>Cycle(m,3) switch{0=>"splintering_charge",1=>"broken_crown",_=>"antler_sweep_h"},
                        "BARE"=>Cycle(m,3) switch{0=>"wild_rush",1=>"desperate_gore",_=>"ashen_frenzy_h"},
                        _=>Cycle(m,3) switch{0=>"crowned_gore",1=>"charred_crown",_=>"ember_kick"}
                    };
                case AshenWildsContent.Titan:return Cycle(m,5) switch{0=>"sink_roots",1=>"uproot",2=>"timberfall",3=>"trampling_roots",_=>"replant"};
                case AshenWildsContent.Alpha:
                    if(m.phase==1)return Cycle(m,4) switch{0=>"alpha_bite",1=>"pack_howl",2=>EligibleCommandTargets(self).Any()?"alpha_command":"smoke_pounce",_=>"smoke_pounce"};
                    if(m.phase==2)
                    {
                        if(Cycle(m,4)==1){m.plannedValue=EligibleCommandTargets(self).Any()?1:0;return "cinder_howl";}
                        return Cycle(m,4) switch{0=>"burning_rush",2=>"blazing_claw",_=>"predators_guard"};
                    }
                    return Cycle(m,4) switch{0=>"eruption_claw",1=>"ashen_frenzy",2=>"scorching_roar",_=>"apex_maul"};
                case AshenWildsContent.Whelp:return Cycle(m,2)==0?"cinder_bite":"heated_pounce";
                case AshenWildsContent.Runner:return Cycle(m,2)==0?"scorching_swipe":"fleet_guard";
            }
            m.hold=false;return ChooseDrownedMove(m)??ChooseFoundryMove(m)??"strike";
        }
        // The Minion type this owner would create right now (the Forgemaster rebuilds whichever Drone is missing).
        private string SummonTypeAt(int ownerIndex)=>EnemyIdAt(ownerIndex)==CrimsonFoundryContent.Forgemaster?MissingForgemasterDrone(ownerIndex):EnemyIdAt(ownerIndex)==FracturedRealmContent.Sovereign?(MindAt(ownerIndex)?.state=="WARD"?FracturedRealmContent.WardFragment:FracturedRealmContent.BladeFragment):SummonFor(EnemyIdAt(ownerIndex));
        private bool CanSummon(int ownerIndex)
        {
            var id=EnemyIdAt(ownerIndex);var type=SummonTypeAt(ownerIndex);if(string.IsNullOrEmpty(type))return false;
            if(OwnedMinions(ownerIndex).Count()>=MinionCap(id)||LivingBodies>=MaxBattlefieldBodies)return false;
            return opponents.Count<MaxBattlefieldBodies||ReusableMinionSlot()>=0;
        }
        private int ReusableMinionSlot(){for(var i=0;i<opponents.Count;i++){var m=opponents[i].mind;if(m!=null&&m.minion&&opponents[i].fighter.hp<=0&&m.dead&&m.diedTurn<turn)return i;}return -1;}

        // ---------- moves ----------
        private static PlannedEnemyAction P(EnemyActionType t,int amount,int hits=1)=>new(t,amount,hits);
        private PlannedEnemyAction[] WildActions(string move,WildMind m)
        {
            const EnemyActionType A=EnemyActionType.Attack,B=EnemyActionType.Block,S=EnemyActionType.Strength,H=EnemyActionType.Heal;
            switch(move)
            {
                case "ember_bite":return new[]{P(A,7)};
                case "pack_pounce":return new[]{P(A,m.plannedValue>0?m.plannedValue:8)};
                case "stoke":return new[]{P(S,1),P(B,4)};
                case "headbutt":return new[]{P(A,9)};
                case "rooted_hide":return new[]{P(B,15)};
                case "shelter_herd":return new[]{P(B,9),P(EnemyActionType.BlockPack,4)};
                case "shed_bark":return new[]{P(H,5),P(B,8)};
                case "fan_embers":return new[]{P(EnemyActionType.StrengthAlly,1)};
                case "ash_veil":return new[]{P(EnemyActionType.BlockAllies,6)};
                case "feeding_cry":return new[]{P(EnemyActionType.HealAlly,5),P(EnemyActionType.BlockTarget,5)};
                case "wild_chorus":return new[]{P(EnemyActionType.StrengthAlly,1),P(EnemyActionType.BlockAllies,4)};
                case "thorn_bite":return new[]{P(A,8)};
                case "bramble_coil":return new[]{P(B,12)};
                case "thornburst":return new[]{P(A,4,3)};
                case "splinter_rush":return new[]{P(A,11),P(EnemyActionType.Weak,1)};
                case "stalking_claw":return new[]{P(A,7)};
                case "smoke_mark":return new[]{P(EnemyActionType.Weak,1),P(B,5)};
                case "ambush":return new[]{P(A,m.plannedValue>0?m.plannedValue:9)};
                case "tear_open":return new[]{P(EnemyActionType.Vulnerable,1),P(A,6)};
                case "grow_sapling":return new[]{P(EnemyActionType.Summon,1),P(B,4)};
                case "root_slam":return new[]{P(A,7)};
                case "nourish":return new[]{P(EnemyActionType.HealMinion,6),P(EnemyActionType.BlockTarget,5)};
                case "rc_command":return new[]{P(EnemyActionType.Command,1),P(B,4)};
                case "thorn_peck":return new[]{P(A,5)};
                case "curl_roots":return new[]{P(B,6)};
                case "gore":return new[]{P(A,10)};
                case "stand_vigil":return new[]{P(B,11)};
                case "antler_sweep":return new[]{P(A,8),P(B,5)};
                case "mourning_cry":return new[]{P(S,1),P(B,12)};
                case "feeding_bite":return new[]{P(A,10),P(H,4)};
                case "smoldering_belly":return new[]{P(B,14),P(H,4)};
                case "furnace_eruption":return new[]{P(A,6,3)};
                case "alpha_maul":return new[]{P(A,14)};
                case "feed_pack":return new[]{P(EnemyActionType.StrengthMinions,1),P(EnemyActionType.BlockMinions,5)};
                case "protective_snarl":return new[]{P(B,12),P(EnemyActionType.BlockMinions,7)};
                case "pm_command":return new[]{P(EnemyActionType.Command,1),P(B,6)};
                case "bereaved_fury":return new[]{P(A,16),P(S,1)};
                case "quick_bite":return new[]{P(A,6)};
                case "frenzy":return new[]{P(A,4,2)};
                case "shoulder_ram":return new[]{P(A,5),P(B,5)};
                case "shelter_mother":return new[]{P(EnemyActionType.BlockOwner,8),P(B,4)};
                case "crowned_gore":return new[]{P(A,13),P(B,6)};
                case "charred_crown":return new[]{P(B,18)};
                case "ember_kick":return new[]{P(A,10),P(S,1)};
                case "splintering_charge":return new[]{P(A,16)};
                case "broken_crown":return new[]{P(B,11),P(A,6)};
                case "antler_sweep_h":return new[]{P(A,7,2)};
                case "wild_rush":return new[]{P(A,18),P(B,5)};
                case "desperate_gore":return new[]{P(A,21)};
                case "ashen_frenzy_h":return new[]{P(A,6,3)};
                case "sink_roots":return new[]{P(B,22),P(H,6)};
                case "uproot":return new[]{P(A,12),P(S,1)};
                case "timberfall":return new[]{P(A,20)};
                case "trampling_roots":return new[]{P(A,8,2)};
                case "replant":return new[]{P(B,12),P(H,4)};
                case "alpha_bite":return new[]{P(A,13)};
                case "pack_howl":return new[]{P(EnemyActionType.StrengthMinions,1),P(B,7)};
                case "alpha_command":return new[]{P(EnemyActionType.Command,1),P(B,6)};
                case "smoke_pounce":return new[]{P(A,10),P(EnemyActionType.Weak,1)};
                case "burning_rush":return new[]{P(A,15),P(B,5)};
                case "cinder_howl":return m.plannedValue>0?new[]{P(EnemyActionType.Command,1),P(A,7)}:new[]{P(A,14)};
                case "blazing_claw":return new[]{P(A,8,2)};
                case "predators_guard":return new[]{P(B,14),P(S,1)};
                case "eruption_claw":return new[]{P(A,17),P(EnemyActionType.Vulnerable,1)};
                case "ashen_frenzy":return new[]{P(A,7,3)};
                case "scorching_roar":return new[]{P(S,2),P(B,10)};
                case "apex_maul":return new[]{P(A,24)};
                case "cinder_bite":return new[]{P(A,7)};
                case "heated_pounce":return new[]{P(A,9)};
                case "scorching_swipe":return new[]{P(A,5),P(EnemyActionType.Weak,1)};
                case "fleet_guard":return new[]{P(B,7)};
            }
            return DrownedActions(move,m)??FoundryActions(move,m)??new[]{P(A,Math.Max(1,enemyBaseDamage))};
        }
        private string WildMoveLabel(string move,WildMind m)=>move switch
        {
            "ember_bite"=>"EMBER BITE","pack_pounce"=>m.plannedValue>=12?"PACK POUNCE · WOUNDED PACKMATE":"PACK POUNCE","stoke"=>"STOKE",
            "headbutt"=>"HEADBUTT","rooted_hide"=>"ROOTED HIDE","shelter_herd"=>"SHELTER THE HERD","shed_bark"=>"SHED BARK",
            "fan_embers"=>"FAN EMBERS","ash_veil"=>"ASH VEIL","feeding_cry"=>"FEEDING CRY","wild_chorus"=>"WILD CHORUS",
            "thorn_bite"=>"THORN BITE","bramble_coil"=>"BRAMBLE COIL · THORNBURST NEXT","thornburst"=>"THORNBURST","splinter_rush"=>"SPLINTER RUSH",
            "stalking_claw"=>"STALKING CLAW","smoke_mark"=>"SMOKE MARK","ambush"=>m.plannedValue>=13?"AMBUSH · PREY WEAKENED":"AMBUSH","tear_open"=>"TEAR OPEN",
            "grow_sapling"=>"GROW SAPLING","root_slam"=>"ROOT SLAM","nourish"=>"NOURISH","rc_command"=>"COMMAND",
            "thorn_peck"=>"THORN PECK","curl_roots"=>"CURL ROOTS",
            "gore"=>"GORE","stand_vigil"=>"STAND VIGIL","antler_sweep"=>"ANTLER SWEEP","mourning_cry"=>"MOURNING CRY",
            "feeding_bite"=>"FEEDING BITE","smoldering_belly"=>"SMOLDERING BELLY","furnace_eruption"=>"FURNACE ERUPTION",
            "alpha_maul"=>"ALPHA MAUL","feed_pack"=>"FEED THE PACK","protective_snarl"=>"PROTECTIVE SNARL","pm_command"=>"COMMAND","bereaved_fury"=>"BEREAVED FURY",
            "quick_bite"=>"QUICK BITE","frenzy"=>"FRENZY","shoulder_ram"=>"SHOULDER RAM","shelter_mother"=>"SHELTER MOTHER",
            "crowned_gore"=>"CROWNED GORE","charred_crown"=>"CHARRED CROWN","ember_kick"=>"EMBER KICK",
            "splintering_charge"=>"SPLINTERING CHARGE","broken_crown"=>"BROKEN CROWN","antler_sweep_h"=>"ANTLER SWEEP",
            "wild_rush"=>"WILD RUSH","desperate_gore"=>"DESPERATE GORE","ashen_frenzy_h"=>"ASHEN FRENZY",
            "sink_roots"=>"SINK ROOTS","uproot"=>"UPROOT","timberfall"=>"TIMBERFALL","trampling_roots"=>"TRAMPLING ROOTS","replant"=>"REPLANT",
            "alpha_bite"=>"ALPHA BITE","pack_howl"=>"PACK HOWL","alpha_command"=>"COMMAND","smoke_pounce"=>"SMOKE POUNCE",
            "burning_rush"=>"BURNING RUSH","cinder_howl"=>"CINDER HOWL","blazing_claw"=>"BLAZING CLAW","predators_guard"=>"PREDATOR'S GUARD",
            "eruption_claw"=>"ERUPTION CLAW","ashen_frenzy"=>"ASHEN FRENZY","scorching_roar"=>"SCORCHING ROAR","apex_maul"=>"APEX MAUL",
            "cinder_bite"=>"CINDER BITE","heated_pounce"=>"HEATED POUNCE","scorching_swipe"=>"SCORCHING SWIPE","fleet_guard"=>"FLEET GUARD",
            _=>DrownedMoveLabel(move,m)
        };
        // Presentation hook names for future animation / VFX (emitted as CombatEventKind.Hook).
        private static string WildHookFor(string id,string move)=>move switch
        {
            "pack_pounce"=>"cinderfang_pounce","shelter_herd"=>"barkhide_shelter","fan_embers" or "ash_veil" or "feeding_cry" or "wild_chorus"=>"emberwing_support",
            "bramble_coil"=>"thornjaw_bramble_coil","thornburst"=>"thornjaw_thornburst","ambush"=>"ash_stalker_ambush",
            "grow_sapling"=>"rootcaller_summon","rc_command"=>"rootcaller_command","mourning_cry"=>"mourning_cry",
            "pm_command"=>"packmother_command","bereaved_fury"=>"packmother_bereaved_fury",
            "uproot"=>"root_titan_uproot","replant"=>"root_titan_root",
            "alpha_bite" or "smoke_pounce" or "burning_rush" or "blazing_claw" or "eruption_claw" or "ashen_frenzy" or "apex_maul"=>"cinder_alpha_major_attack",
            "alpha_command" or "cinder_howl"=>"cinder_alpha_command",
            _=>DrownedHook(move)
        };
        private PlannedEnemyAction[] WildPlannedTurn(){var m=Mind;if(string.IsNullOrEmpty(m.planned))PlanWildIntent();return WildActions(m.planned,m);}

        // ---------- resolution ----------
        // Advances the pattern before the actions run, so a threshold crossed mid-action
        // (Retaliate, Thorns) cleanly restarts the new move pool at its first move.
        private void BeginWildAction()
        {
            var m=Mind;var move=m.planned;
            var hook=WildHookFor(enemyId,move);if(!string.IsNullOrEmpty(hook))EmitHook(hook);
            if(move=="mourning_cry"){m.mourning=false;}
            else if(enemyId==AshenWildsContent.Packmother&&(move=="bereaved_fury"||move=="alpha_maul"&&!OwnedMinions(enemyContextIndex).Any()))m.step2++;
            else if(enemyId==AshenWildsContent.Maw){m.state=m.state switch{"HUNGRY"=>"FED","FED"=>"BURNING",_=>"HUNGRY"};EmitHook("hollow_maw_state:"+m.state);}
            else if(!BeginDrownedAction(m,move)&&!BeginFoundryAction(m,move))m.step++;
        }
        private void EndWildAction(){var m=Mind;ReleasePairSink();if(m==null)return;if(m.minion)m.lastMove=m.planned;m.prior=m.planned;m.planned="";m.hold=false;}
        private void EmitHook(string name)=>Emit(CombatEventKind.Hook,0,false,null,"HOOK:"+name);

        private void ExecuteWildAction(PlannedEnemyAction action)
        {
            var self=enemyContextIndex;
            switch(action.type)
            {
                case EnemyActionType.Summon:WildSummon(self);break;
                case EnemyActionType.Command:WildCommand(self);break;
                case EnemyActionType.BlockAllies:foreach(var i in LivingAllies(self,false))InEnemyContext(i,()=>{enemy.block+=action.amount;Emit(CombatEventKind.Block,action.amount,false);});break;
                case EnemyActionType.BlockPack:foreach(var i in LivingAllies(self,true))InEnemyContext(i,()=>{enemy.block+=action.amount;Emit(CombatEventKind.Block,action.amount,false);});break;
                case EnemyActionType.BlockMinions:foreach(var i in OwnedMinions(self).ToArray())InEnemyContext(i,()=>{enemy.block+=action.amount;Emit(CombatEventKind.Block,action.amount,false);});break;
                case EnemyActionType.BlockOwner:{var owner=OwnerIndexOf(self);if(owner>=0&&IsLivingTarget(owner))InEnemyContext(owner,()=>{enemy.block+=action.amount;Emit(CombatEventKind.Block,action.amount,false);});break;}
                case EnemyActionType.StrengthMinions:foreach(var i in OwnedMinions(self).ToArray())InEnemyContext(i,()=>{var gain=EnemyBuffAfterWither(action.amount);enemy.strength+=gain;Emit(CombatEventKind.Status,gain,false,null,"STRENGTH");});break;
                case EnemyActionType.StrengthAlly:
                {
                    var allies=LivingAllies(self,false).Where(i=>DamageCapable(EnemyIdAt(i))).ToArray();if(allies.Length==0)break;
                    var hurt=allies.Where(i=>EnemyAt(i).hp*4<EnemyAt(i).maxHp*3).ToArray();if(hurt.Length>0)allies=hurt;
                    var target=allies[NextRandom(allies.Length)];
                    InEnemyContext(target,()=>{var gain=EnemyBuffAfterWither(action.amount);enemy.strength+=gain;Emit(CombatEventKind.Status,gain,false,null,"STRENGTH");});break;
                }
                case EnemyActionType.HealAlly:
                {
                    var allies=LivingAllies(self,false).Where(i=>DamageCapable(EnemyIdAt(i))).ToArray();wildTarget=-1;if(allies.Length==0)break;
                    wildTarget=allies.OrderBy(i=>EnemyAt(i).hp).ThenBy(i=>i).First();HealEnemyAt(wildTarget,action.amount);break;
                }
                case EnemyActionType.HealMinion:
                {
                    var hurt=OwnedMinions(self).Where(i=>EnemyAt(i).hp<EnemyAt(i).maxHp).ToArray();wildTarget=-1;if(hurt.Length==0)break;
                    wildTarget=hurt.OrderBy(i=>EnemyAt(i).hp).ThenBy(i=>i).First();HealEnemyAt(wildTarget,action.amount);break;
                }
                case EnemyActionType.BlockTarget:if(wildTarget>=0&&IsLivingTarget(wildTarget))InEnemyContext(wildTarget,()=>{enemy.block+=action.amount;Emit(CombatEventKind.Block,action.amount,false);});wildTarget=-1;break;
                default:ExecuteDrownedAction(action);break;
            }
        }
        private void HealEnemyAt(int index,int amount)=>InEnemyContext(index,()=>{var before=enemy.hp;enemy.hp=Math.Min(enemy.maxHp,enemy.hp+amount);if(enemy.hp>before)Emit(CombatEventKind.Heal,enemy.hp-before,false);});
        private IEnumerable<int> LivingAllies(int self,bool nonMinionOnly)=>Enumerable.Range(0,EnemyCount).Where(i=>i!=self&&IsLivingTarget(i)&&(!nonMinionOnly||!IsMinionAt(i))).ToArray();
        // Runs an action in another enemy's context and stores its state back.
        private void InEnemyContext(int index,Action action)
        {
            SaveEnemyContext();var previous=enemyContextIndex;LoadEnemyContext(index);
            try{action();SaveEnemyContext();}finally{LoadEnemyContext(previous);}
        }
        private void WildSummon(int ownerIndex)
        {
            if(!CanSummon(ownerIndex))return;
            var ownerMind=MindAt(ownerIndex);var id=SummonTypeAt(ownerIndex);var def=WildDef(id);
            var hp=Math.Max(1,(int)Math.Round(def.hp*summonHpPercent/100.0));
            var created=new CombatOpponent{id=id,baseDamage=def.baseDamage,fighter=new FighterState{hp=hp,maxHp=hp,strength=summonStrength},
                mind=new WildMind{uid=nextWildUid++,ownerUid=ownerMind.uid,minion=true,summonedTurn=turn}};
            SaveEnemyContext();
            var slot=opponents.Count<MaxBattlefieldBodies?-1:ReusableMinionSlot();
            if(slot>=0)opponents[slot]=created;else{opponents.Add(created);slot=opponents.Count-1;}
            created.intent=IntentKind.Unknown;created.intentLabel="SUMMONED";
            InEnemyContext(slot,()=>{Emit(CombatEventKind.Status,1,false,null,"SUMMONED");if(id==DrownedQuarterContent.Hand)EmitHook("drowned_hand_spawn");else if(id==CrimsonFoundryContent.ScrapDrone)EmitHook("scrap_drone_spawn");else if(id==HollowwoodContent.Sporeling)EmitHook("sporeling_spawn");else if(id==HollowwoodContent.Huskbud)EmitHook("huskbud_spawn");else if(id==ShatteredObservatoryContent.StarFragment)EmitHook("star_fragment_spawn");else if(id==GildedRuinsContent.Servitor)EmitHook("servitor_spawn");else if(id==GildedRuinsContent.Guard)EmitHook("coinbound_guard_spawn");else if(id==FracturedRealmContent.SplitEcho)EmitHook("split_echo_spawn");else if(id is FracturedRealmContent.BladeFragment or FracturedRealmContent.WardFragment)EmitHook("fragment_spawn");});
            rosterVersion++;
        }
        private void WildCommand(int ownerIndex)
        {
            var options=EligibleCommandTargets(ownerIndex).ToArray();if(options.Length==0)return;
            // The player knows a Command is coming, not which Minion answers it.
            var chosen=options[NextRandom(options.Length)];
            InEnemyContext(chosen,()=>
            {
                var m=Mind;var actions=WildActions(m.lastMove,m);
                EmitHook("command_answer");Emit(CombatEventKind.EnemyAction,EnemyContextIndex);
                wildSinkMute++;try{RunEnemyActions(actions);}finally{wildSinkMute--;}
            });
        }

        // ---------- battlefield formation ----------
        // RULE: Minions stand IN FRONT of their Owner (between the player and the Owner), for every themed fight.
        // Starting Minions begin there and summoned Minions take the place directly in front of their Owner.
        // Enemy indices stay stable for the whole fight (so saves, events and intents never shift); this order is
        // what the battlefield shows and what left/right targeting follows. Front = lowest slot = nearest the player.
        public int[] BattleOrder()
        {
            var n=opponents?.Count??EnemyCount;var order=new List<int>(n);
            if(!wildCombat||opponents==null){for(var i=0;i<n;i++)order.Add(i);return order.ToArray();}
            for(var i=0;i<n;i++)
            {
                var m=opponents[i].mind;if(m!=null&&m.minion&&OwnerIndexOf(i)>=0)continue; // placed with its Owner
                for(var j=0;j<n;j++){var mj=opponents[j].mind;if(j!=i&&mj!=null&&mj.minion&&m!=null&&mj.ownerUid==m.uid)order.Add(j);}
                order.Add(i);
            }
            for(var i=0;i<n;i++)if(!order.Contains(i))order.Insert(0,i); // orphans stand at the front
            return order.ToArray();
        }
        public int BattleSlotOf(int index){var order=BattleOrder();var slot=Array.IndexOf(order,index);return slot<0?index:slot;}

        // ---------- deaths, ownership, thresholds ----------
        public int rosterVersion;
        internal void SweepWilds()
        {
            if(!wildCombat||wildSweepDepth>0||opponents==null||opponents.Count==0)return;
            wildSweepDepth++;
            try
            {
                SaveEnemyContext();
                for(var pass=0;pass<8;pass++)
                {
                    var changed=false;
                    for(var i=0;i<opponents.Count;i++)
                    {
                        var o=opponents[i];var m=o.mind;if(m==null)continue;
                        if(o.fighter.hp>0||m.dead)continue;
                        m.dead=true;m.diedTurn=turn;changed=true;OnWildDeath(i);
                    }
                    // Harmless pure Supports never hold the player hostage.
                    var living=Enumerable.Range(0,opponents.Count).Where(i=>opponents[i].fighter.hp>0).ToArray();
                    if(living.Length>0&&living.All(i=>!DamageCapable(opponents[i].id)))
                        foreach(var i in living){opponents[i].fighter.hp=0;opponents[i].mind.fled=true;InEnemyContext(i,()=>EmitHook("support_flees"));changed=true;}
                    if(!changed)break;
                }
                for(var i=0;i<opponents.Count;i++)if(opponents[i].fighter.hp>0&&opponents[i].mind!=null)CheckWildThresholds(i);
            }
            finally{wildSweepDepth--;}
        }
        private bool PlayerPhaseReplan=>phase==CombatPhase.Player&&wildPlanning==0;
        // Everlasting Ember: an enemy that dies while burning passes half of its remaining Burn to a random living enemy.
        private void EverlastingEmberTransfer(int index)
        {
            if(!relics.Contains("everlasting_ember"))return;
            var half=opponents[index].fighter.burn/2;if(half<=0)return;
            var living=Enumerable.Range(0,opponents.Count).Where(i=>i!=index&&opponents[i].fighter.hp>0).ToArray();if(living.Length==0)return;
            var target=living[NextRandom(living.Length)];
            RelicTrigger("everlasting_ember",()=>InEnemyContext(target,()=>{enemy.burn+=half;Emit(CombatEventKind.Status,half,false,null,"BURN");}));
        }
        private void OnWildDeath(int index)
        {
            var m=MindAt(index);var id=EnemyIdAt(index);
            RuinsOnDeath(index);
            EverlastingEmberTransfer(index);
            if(m.minion)InEnemyContext(index,()=>EmitHook(id==AshenWildsContent.Sapling?"sapling_death":id==DrownedQuarterContent.Hand?"drowned_hand_death":id==HollowwoodContent.Sporeling?"sporeling_death":id==ShatteredObservatoryContent.StarFragment?"star_fragment_death":id is ShatteredObservatoryContent.SunFragment or ShatteredObservatoryContent.MoonFragment?"orrery_fragment_death":id==GildedRuinsContent.Servitor?"servitor_death":id==GildedRuinsContent.Guard?"coinbound_guard_death":"minion_death"));
            // Owner death: every Minion it owns withers immediately (no rewards of their own).
            foreach(var k in Enumerable.Range(0,opponents.Count).Where(k=>opponents[k].fighter.hp>0&&opponents[k].mind?.ownerUid==m.uid).ToArray())
            {opponents[k].fighter.hp=0;InEnemyContext(k,()=>EmitHook("minion_withers"));}
            if(!m.minion)
                for(var k=0;k<opponents.Count;k++)
                    if(opponents[k].id==AshenWildsContent.Elk&&opponents[k].fighter.hp>0&&opponents[k].mind!=null)
                    {
                        // One pending Mourning Cry, however many packmates fall before the Elk acts.
                        opponents[k].mind.mourning=true;
                        if(PlayerPhaseReplan)ReplanAt(k);
                    }
            // Moves that relied on Minions are re-read while the player can still see them change.
            if(PlayerPhaseReplan)
                for(var k=0;k<opponents.Count;k++)
                {
                    var mk=opponents[k].mind;if(mk==null||opponents[k].fighter.hp<=0)continue;
                    if(mk.planned is "rc_command" or "nourish" or "pm_command" or "alpha_command" or "cinder_howl" or "feed_pack" or "protective_snarl"||opponents[k].id==AshenWildsContent.Packmother||DrownedReplanOnDeath(k))ReplanAt(k);
                }
        }
        private void ReplanAt(int index)=>InEnemyContext(index,PlanWildIntent);
        private void CheckWildThresholds(int index)
        {
            var o=opponents[index];var m=o.mind;var f=o.fighter;
            if(o.id==AshenWildsContent.Hart)
            {
                var next=f.hp*3<=f.maxHp?"BARE":f.hp*3<=f.maxHp*2?"SPLINTERED":"CROWNED";
                if(next!=m.state&&(m.state=="CROWNED"||m.state=="SPLINTERED"&&next=="BARE"))
                {
                    // Transformation only: no heal, no Strength.
                    m.state=next;m.step=0;InEnemyContext(index,()=>EmitHook("burned_hart_"+next.ToLowerInvariant()));
                    if(PlayerPhaseReplan)ReplanAt(index);
                }
            }
            else if(o.id==AshenWildsContent.Alpha)
            {
                var target=f.hp*3<=f.maxHp?3:f.hp*3<=f.maxHp*2?2:1;
                while(m.phase<target)
                {
                    m.phase++;m.step=0;
                    if(m.phase==2)InEnemyContext(index,()=>{enemy.strength+=1;Emit(CombatEventKind.Status,1,false,null,"STRENGTH");EmitHook("cinder_alpha_phase2");});
                    else
                    {
                        InEnemyContext(index,()=>EmitHook("cinder_alpha_phase3"));
                        // Surviving pack members flee. The Alpha gains nothing from it.
                        for(var k=0;k<opponents.Count;k++)
                            if(opponents[k].fighter.hp>0&&opponents[k].mind?.ownerUid==m.uid){opponents[k].fighter.hp=0;opponents[k].mind.fled=true;opponents[k].mind.dead=true;opponents[k].mind.diedTurn=turn;InEnemyContext(k,()=>EmitHook("cinder_alpha_minion_flees"));}
                    }
                    bossPhase=m.phase;memory.resolvedBossPhase=m.phase;
                    if(PlayerPhaseReplan)ReplanAt(index);
                }
            }
            else CheckDrownedThresholds(index);
        }
        public int WildBossIndex{get{if(!wildCombat)return -1;for(var i=0;i<EnemyCount;i++)if(WildDef(EnemyIdAt(i))?.boss==true)return i;return -1;}}

        // ---------- presentation text ----------
        public string WildStateText(int index)
        {
            var m=MindAt(index);if(m==null)return "";var id=EnemyIdAt(index);
            var lines=new List<string>();
            if(m.minion){var owner=OwnerIndexOf(index);var ownerName=owner>=0?WildDef(EnemyIdAt(owner))?.name??"its owner":"its owner";lines.Add($"MINION · Owned by {Title(ownerName)}. Dies when its owner dies. No reward of its own.");}
            RuinsHeldText(m,lines);
            switch(id)
            {
                case AshenWildsContent.Maw:lines.Add("STATE · "+m.state+" — Hungry → Fed → Burning → Hungry.");break;
                case AshenWildsContent.Titan:lines.Add("STANCE · "+(Cycle(m,5)<=1?"ROOTED":"UPROOTED")+" — Sink Roots → Uproot → Timberfall → Trampling Roots → Replant.");break;
                case AshenWildsContent.Hart:lines.Add("CROWN · "+m.state+" — its antlers break at 2/3 and 1/3 health; it grows wilder.");break;
                case AshenWildsContent.Elk:lines.Add(m.mourning?"MOURNING · A packmate fell. Its next action is Mourning Cry.":"MOURNING · If a packmate dies, its next action becomes Mourning Cry.");break;
                case AshenWildsContent.Cinderfang:lines.Add("PACK INSTINCT · Pack Pounce deals 12 instead of 8 if a packmate is below half health when it is chosen.");break;
                case AshenWildsContent.Stalker:lines.Add("OPPORTUNIST · Ambush deals 13 instead of 9 if you are Weak or Vulnerable when it is chosen.");break;
                case AshenWildsContent.Emberwing:lines.Add("SUPPORT · Never attacks. Flees if only harmless Supports remain.");break;
                case AshenWildsContent.Rootcaller:lines.Add($"SUMMONER · {OwnedMinions(index).Count()}/2 Ash Saplings.");break;
                case AshenWildsContent.Packmother:lines.Add(OwnedMinions(index).Any()?"PACK · Commands and feeds her cubs.":"BEREAVED · Her pack is gone. She fights with fury.");break;
                case AshenWildsContent.Alpha:lines.Add($"PHASE {m.phase} · "+(m.phase==1?"Predatory Alpha":m.phase==2?"Burning Alpha":"Exposed Fire-Beast"));break;
                default:DrownedStateText(index,m,lines);break;
            }
            return string.Join("\n",lines);
        }
        private static string Title(string upper)=>System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(upper.ToLowerInvariant());
        private void RefreshWildMechanicText()
        {
            var def=WildDef(enemyId);mechanicTitle=def?.name??"ENEMY";mechanicText=WildStateText(enemyContextIndex);
        }
        private string DescribeWildAction(EnemyActionType type,int amount,EnemyIntentAction a)
        {
            switch(type)
            {
                case EnemyActionType.Summon:a.title="SUMMON";return $"Create {amount} {Title(WildDef(SummonFor(enemyId))?.name??"Minion")} (a Minion). Minions die when their owner dies.";
                case EnemyActionType.Command:a.title="COMMAND";return "One random living Minion it owns repeats its previous action. You won't know which one in advance.";
                case EnemyActionType.BlockAllies:a.title="SHIELD ALLIES";return $"Every other living enemy gains {amount} Block.";
                case EnemyActionType.BlockPack:a.title="SHELTER THE HERD";return $"Every other living non-Minion enemy gains {amount} Block.";
                case EnemyActionType.BlockMinions:a.title="SHIELD MINIONS";return $"Each of its living Minions gains {amount} Block.";
                case EnemyActionType.BlockOwner:a.title="SHELTER OWNER";return $"Its owner gains {amount} Block.";
                case EnemyActionType.StrengthAlly:a.title="EMPOWER ALLY";return $"One random ally that can attack gains {amount} Strength (wounded allies below 75% first).";
                case EnemyActionType.StrengthMinions:a.title="EMPOWER MINIONS";return $"Each of its living Minions gains {amount} Strength.";
                case EnemyActionType.HealAlly:a.title="HEAL ALLY";return $"The ally with the lowest health that can attack heals {amount} HP.";
                case EnemyActionType.HealMinion:a.title="NOURISH";return $"Its most wounded Minion heals {amount} HP.";
                case EnemyActionType.BlockTarget:a.title="BLOCK ALLY";return $"That same ally gains {amount} Block.";
            }
            return DescribeDrownedAction(type,amount,a);
        }
    }
}
