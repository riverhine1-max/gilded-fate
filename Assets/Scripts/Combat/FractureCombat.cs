using System;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Core;

namespace GildedFate.Combat
{
    // Act 3 · Theme 2: Fractured Realm AI. Plugs into the shared themed-enemy engine at the end of the chain
    // (… → Ruins → Cathedral → here). Identity: FRACTURE.
    //
    // Fairness contract:
    //  - Every Echo, Repeat, copy and two-way outcome is visible BEFORE it resolves (intent label, counter or forecast).
    //  - Echo damage is fixed when the Echo is created. An Echo is a delayed repeat on that enemy's own normal action,
    //    never an off-turn attack. Nothing here interrupts the player's turn.
    //  - RNG exists only for Command targets and the two-option pairs (Broken Oracle, Unmade Phase 3). A pair is generated
    //    when the previous action resolves, is saved with the combat, and never rerolls on reload.
    //
    // WildMind use here:
    //   lastSeed = the previous completed NON-Repeat action (Echo Soldier, Duplicate) / the Unmade's stored memory
    //   counter  = pending Echo damage (Time-Shear, Unmade phase 2) · Split Sovereign thresholds (bit 1 = 130, bit 2 = 70) ·
    //              total buff stacks a Mirror Husk has copied
    //   state    = Phase Beast form (SOLID / FLICKER / RIFT) · "BLADE"/"WARD" while a Fragment is being summoned
    //   phase    = Splitling (1 whole, 2 split) · Rift Colossus stage · The Unmade phase
    //   flag     = Splitling has split
    //   pair     = the two possible next actions (Broken Oracle, Unmade phase 3)
    //   step     = pattern position (the Loopkeeper uses a 12-step loop: A, A replay, B, B replay)
    public sealed partial class CombatState
    {
        public const string FracturePairMove="fr_pair";
        private static readonly string[] OraclePool={"fr_or_strike","fr_or_ward","fr_or_curse","fr_or_surge"};
        private static readonly string[] UnmadePool={"fr_um_world_rend","fr_um_barrage","fr_um_reality_guard","fr_um_collapse_wave","fr_um_surge"};
        public int fxStartStrength,fxStartFortify;

        // ---------- Mirror Husk: what you gained during your turn ----------
        private void RollFractureMemory(){fxStartStrength=player.strength;fxStartFortify=player.fortify;}
        // Called as the player's turn ends (never mid-card): up to 2 total stacks, Strength first.
        private void CaptureFractureEnd()
        {
            if(!wildCombat||opponents==null)return;
            var ds=Math.Max(0,player.strength-fxStartStrength);var df=Math.Max(0,player.fortify-fxStartFortify);
            if(ds+df<=0)return;
            var s=Math.Min(ds,2);var f=Math.Min(df,2-s);if(s+f<=0)return;
            for(var i=0;i<opponents.Count;i++)
            {
                if(opponents[i].id!=FracturedRealmContent.Husk||!IsLivingTarget(i))continue;
                var m=opponents[i].mind;var gs=s;var gf=f;
                InEnemyContext(i,()=>
                {
                    if(gs>0){enemy.strength+=gs;Emit(CombatEventKind.Status,gs,false,null,"STRENGTH");}
                    if(gf>0){enemy.fortify+=gf;Emit(CombatEventKind.Status,gf,false,null,"FORTIFY");}
                    m.counter+=gs+gf;EmitHook("mirror_buff_copied");
                });
            }
        }

        // ---------- setup ----------
        private static void ResetFractureState(WildMind m,string id)
        {
            if(id==FracturedRealmContent.Beast)m.state="SOLID";
            else if(id==FracturedRealmContent.Splitling||id==FracturedRealmContent.Unmade||id==FracturedRealmContent.Colossus)m.phase=1;
        }

        // ---------- helpers ----------
        private static bool IsRepeatMove(string move)=>move is "fr_repeat" or "fr_dup_repeat" or "fr_um_replay" or "fr_um_memory";
        private static int Mult125(int v)=>(int)Math.Round(v*1.25,MidpointRounding.AwayFromZero);
        private bool DamagedAlliesAnyFx(int self)=>DamagedAllies(self).Any();
        // Rift Binder pairs. Source and target are always two DIFFERENT damage-capable allies; the Binder is never either.
        private bool BinderStrengthPair(int self,out int src,out int tgt)
        {
            src=tgt=-1;
            var allies=LivingAllies(self,false).Where(i=>DamageCapable(EnemyIdAt(i))).ToArray();if(allies.Length<2)return false;
            var sources=allies.Where(i=>EnemyAt(i).strength>0).OrderByDescending(i=>EnemyAt(i).strength).ThenBy(i=>i).ToArray();if(sources.Length==0)return false;
            var s0=sources[0];src=s0;var others=allies.Where(i=>i!=s0).OrderBy(i=>EnemyAt(i).strength).ThenBy(i=>i).ToArray();
            tgt=others[0];return true;
        }
        // Block an ally already has, or is about to gain from its own planned move earlier in this enemy turn.
        private int ExpectedBlock(int i,int binder)
        {
            var b=EnemyAt(i).block;var mi=MindAt(i);
            if(i<binder&&mi!=null&&!string.IsNullOrEmpty(mi.planned)&&!mi.minion&&mi.planned!=FracturePairMove&&!IsRepeatMove(mi.planned)&&mi.planned.StartsWith("fr_"))
                foreach(var a in FractureActions(mi.planned,mi)??Array.Empty<PlannedEnemyAction>())if(a.type==EnemyActionType.Block)b+=a.amount;
            return b;
        }
        private bool BinderGuardPair(int self,out int src,out int tgt,bool forPlan)
        {
            src=tgt=-1;
            var allies=LivingAllies(self,false).Where(i=>DamageCapable(EnemyIdAt(i))).ToArray();if(allies.Length<2)return false;
            Func<int,int> blk=i=>forPlan?ExpectedBlock(i,self):EnemyAt(i).block;
            var sources=allies.Where(i=>blk(i)>0).OrderByDescending(blk).ThenBy(i=>i).ToArray();if(sources.Length==0)return false;
            var s0=sources[0];src=s0;tgt=allies.Where(i=>i!=s0).OrderBy(blk).ThenBy(i=>i).First();return true;
        }

        // ---------- move choice ----------
        private string ChooseFractureMove(WildMind m)
        {
            var self=enemyContextIndex;
            switch(enemyId)
            {
                case FracturedRealmContent.Soldier:return Cycle(m,4) switch{0=>"fr_slash",1=>"fr_echo_guard",2=>"fr_repeat",_=>"fr_slash"};
                case FracturedRealmContent.Splitling:
                    if(m.phase<=1)return Cycle(m,2)==0?"fr_bite":"fr_unstable_guard";
                    return Cycle(m,2)==1&&EligibleCommandTargets(self).Any()?"fr_fractured_command":"fr_core_pulse";
                case FracturedRealmContent.SplitEcho:return Cycle(m,2)==0?"fr_echo_claw":"fr_flicker_guard";
                case FracturedRealmContent.Husk:return Cycle(m,3) switch{0=>"fr_mirror_strike",1=>"fr_reflective_shell",_=>"fr_shattered_reflection"};
                case FracturedRealmContent.Binder:
                    switch(Cycle(m,4))
                    {
                        case 0:return "fr_rift_ward";
                        case 1:
                            if(BinderStrengthPair(self,out _,out _))return "fr_binder_dup_strength";
                            if(BinderGuardPair(self,out _,out _,true))return "fr_binder_dup_guard";
                            return DamagedAlliesAnyFx(self)?"fr_stabilize":"fr_rift_ward";
                        case 2:return DamagedAlliesAnyFx(self)?"fr_stabilize":"fr_rift_ward";
                        default:return BinderGuardPair(self,out _,out _,true)?"fr_binder_dup_guard":"fr_rift_ward";
                    }
                case FracturedRealmContent.Shear:
                    switch(Cycle(m,4))
                    {
                        case 0:return "fr_shear_strike";
                        case 1:return m.counter>0?"fr_resolve_echo":"fr_temporal_guard";
                        case 2:return "fr_temporal_guard";
                        default:return "fr_split_moment";
                    }
                case FracturedRealmContent.Beast:
                    return m.state=="FLICKER"?"fr_distorted_guard":m.state=="RIFT"?"fr_rift_maul":"fr_crushing_bite";
                case FracturedRealmContent.Oracle:
                    if(string.IsNullOrEmpty(m.pair))m.pair=PickPair(OraclePool,m.lastPair);
                    return FracturePairMove;
                case FracturedRealmContent.Colossus:
                    return m.phase<=1?(Cycle(m,2)==0?"fr_col_slam":"fr_col_armor"):m.phase==2?(Cycle(m,2)==0?"fr_col_hammer":"fr_col_splinter"):(Cycle(m,2)==0?"fr_col_storm":"fr_col_crush");
                case FracturedRealmContent.Duplicate:
                    return Cycle(m,6) switch{0=>"fr_dup_slash",2=>"fr_dup_guard",4=>"fr_dup_ruin",_=>"fr_dup_repeat"};
                case FracturedRealmContent.Sovereign:
                {
                    var minions=OwnedMinions(self).Any();
                    switch(Cycle(m,4))
                    {
                        case 0:return "fr_sov_cut";
                        case 1:return minions?"fr_sov_authority":"fr_sov_collapse";
                        case 2:return EligibleCommandTargets(self).Any()?"fr_sov_command":"fr_sov_cut";
                        default:return "fr_sov_collapse";
                    }
                }
                case FracturedRealmContent.BladeFragment:return Cycle(m,2)==0?"fr_blade_fractured":"fr_blade_razor";
                case FracturedRealmContent.WardFragment:return Cycle(m,2)==0?"fr_ward_fractured":"fr_ward_strike";
                case FracturedRealmContent.Loopkeeper:
                {
                    var s=Cycle(m,12);var i=s%3;
                    return s<6?(i==0?"fr_loop_cut":i==1?"fr_loop_ward":"fr_loop_fracture"):(i==0?"fr_loop_crush":i==1?"fr_loop_reinforce":"fr_loop_barrage");
                }
                case FracturedRealmContent.Unmade:
                    switch(m.phase)
                    {
                        case 1:
                            switch(Cycle(m,5))
                            {
                                case 0:return "fr_um_strike";
                                case 1:return "fr_um_memory";
                                case 2:return string.IsNullOrEmpty(m.lastSeed)?"fr_um_rift_guard":"fr_um_replay";
                                case 3:return "fr_um_rift_guard";
                                default:return "fr_um_pulse";
                            }
                        case 2:
                            switch(Cycle(m,5))
                            {
                                case 0:return "fr_um_rend";
                                case 1:return m.counter>0?"fr_um_resolve":"fr_um_fguard";
                                case 2:return "fr_um_fguard";
                                case 3:return "fr_um_borrowed";
                                default:return "fr_um_composite";
                            }
                        default:
                            if(string.IsNullOrEmpty(m.pair))m.pair=PickPair(UnmadePool,m.lastPair);
                            return FracturePairMove;
                    }
            }
            return null;
        }

        // ---------- moves ----------
        private PlannedEnemyAction[] FractureActions(string move,WildMind m)
        {
            const EnemyActionType A=EnemyActionType.Attack,B=EnemyActionType.Block,S=EnemyActionType.Strength,W=EnemyActionType.Weak,V=EnemyActionType.Vulnerable;
            switch(move)
            {
                // Echo Soldier
                case "fr_slash":return new[]{P(A,15)};
                case "fr_echo_guard":return new[]{P(B,15)};
                case "fr_repeat":return FractureActions(string.IsNullOrEmpty(m.lastSeed)?"fr_slash":m.lastSeed,m);
                // Splitling and Split Echo
                case "fr_bite":return new[]{P(A,13)};
                case "fr_unstable_guard":return new[]{P(B,14)};
                case "fr_core_pulse":return new[]{P(A,12),P(EnemyActionType.BlockMinions,5)};
                case "fr_fractured_command":return new[]{P(EnemyActionType.Command,1),P(B,6)};
                case "fr_echo_claw":return new[]{P(A,8)};
                case "fr_flicker_guard":return new[]{P(B,7)};
                // Mirror Husk
                case "fr_mirror_strike":return new[]{P(A,14)};
                case "fr_reflective_shell":return new[]{P(B,17)};
                case "fr_shattered_reflection":return new[]{P(A,10),P(B,8)};
                // Rift Binder
                case "fr_binder_dup_strength":return new[]{P(EnemyActionType.DuplicateStrength,2)};
                case "fr_binder_dup_guard":return new[]{P(EnemyActionType.DuplicateGuard,10)};
                case "fr_rift_ward":return new[]{P(EnemyActionType.BlockAllies,7)};
                case "fr_stabilize":return new[]{P(EnemyActionType.HealDamagedAlly,7),P(EnemyActionType.BlockTarget,5)};
                // Time-Shear
                case "fr_shear_strike":return new[]{P(A,12),P(EnemyActionType.EchoCreate,12)};
                case "fr_resolve_echo":return new[]{P(A,Math.Max(1,m.counter)),P(B,7)};
                case "fr_temporal_guard":return new[]{P(B,18)};
                case "fr_split_moment":return new[]{P(A,9),P(W,1)};
                // Phase Beast
                case "fr_crushing_bite":return new[]{P(A,16),P(B,6)};
                case "fr_distorted_guard":return new[]{P(B,20),P(W,1)};
                case "fr_rift_maul":return new[]{P(A,9,2)};
                // Broken Oracle (one of the two shown possibilities is chosen when it acts)
                case "fr_or_strike":return new[]{P(A,17)};
                case "fr_or_ward":return new[]{P(B,20)};
                case "fr_or_curse":return new[]{P(A,10),P(W,1)};
                case "fr_or_surge":return new[]{P(S,1),P(B,10)};
                case FracturePairMove:
                    // Display / fallback form only: both possibilities, never executed together.
                    return (m.pair??"").Split(new[]{'|'},StringSplitOptions.RemoveEmptyEntries).SelectMany(x=>FractureActions(x,m)).ToArray();
                // Rift Colossus
                case "fr_col_slam":return new[]{P(A,15),P(B,9)};
                case "fr_col_armor":return new[]{P(B,21)};
                case "fr_col_hammer":return new[]{P(A,19)};
                case "fr_col_splinter":return new[]{P(A,8),P(B,15)};
                case "fr_col_storm":return new[]{P(A,8,3)};
                case "fr_col_crush":return new[]{P(A,23)};
                // The Duplicate
                case "fr_dup_slash":return new[]{P(A,18)};
                case "fr_dup_guard":return new[]{P(B,22)};
                case "fr_dup_ruin":return new[]{P(A,12),P(V,1)};
                case "fr_dup_repeat":
                    return (FractureActions(string.IsNullOrEmpty(m.lastSeed)?"fr_dup_slash":m.lastSeed,m)??Array.Empty<PlannedEnemyAction>())
                        .Select(a=>a.type==A||a.type==B?P(a.type,Mult125(a.amount),a.hits):a).ToArray();
                // The Split Sovereign and its Fragments
                case "fr_sov_cut":return new[]{P(A,17),P(B,7)};
                case "fr_sov_authority":return new[]{P(EnemyActionType.StrengthMinions,1),P(B,10)};
                case "fr_sov_command":return new[]{P(EnemyActionType.Command,1),P(B,7)};
                case "fr_sov_collapse":return new[]{P(A,OwnedMinions(enemyContextIndex).Any()?16:22)};
                case "fr_blade_fractured":return new[]{P(A,11)};
                case "fr_blade_razor":return new[]{P(A,6,2)};
                case "fr_ward_fractured":return new[]{P(EnemyActionType.BlockOwner,10),P(B,5)};
                case "fr_ward_strike":return new[]{P(A,7),P(B,7)};
                // The Loopkeeper
                case "fr_loop_cut":return new[]{P(A,16)};
                case "fr_loop_ward":return new[]{P(B,20)};
                case "fr_loop_fracture":return new[]{P(A,11),P(W,1)};
                case "fr_loop_crush":return new[]{P(A,20)};
                case "fr_loop_reinforce":return new[]{P(S,1),P(B,12)};
                case "fr_loop_barrage":return new[]{P(A,7,3)};
                // The Unmade
                case "fr_um_strike":return new[]{P(A,18),P(B,8)};
                case "fr_um_memory":return new[]{P(EnemyActionType.Memory,0)};
                case "fr_um_replay":return FractureActions(string.IsNullOrEmpty(m.lastSeed)?"fr_um_rift_guard":m.lastSeed,m);
                case "fr_um_rift_guard":return new[]{P(B,22)};
                case "fr_um_pulse":return new[]{P(A,13),P(W,1)};
                case "fr_um_rend":return new[]{P(A,20),P(EnemyActionType.EchoCreate,10)};
                case "fr_um_resolve":return new[]{P(A,Math.Max(1,m.counter)),P(B,9)};
                case "fr_um_fguard":return new[]{P(A,9),P(B,17)};
                case "fr_um_borrowed":return new[]{P(S,2),P(B,10)};
                case "fr_um_composite":return new[]{P(A,8,3)};
                case "fr_um_world_rend":return new[]{P(A,26)};
                case "fr_um_barrage":return new[]{P(A,8,4)};
                case "fr_um_reality_guard":return new[]{P(B,26),P(S,1)};
                case "fr_um_collapse_wave":return new[]{P(A,17),P(V,1)};
                case "fr_um_surge":return new[]{P(S,2),P(B,12)};
            }
            return null;
        }
        // Display form of a possibility pair, one line per possibility.
        private PlannedEnemyAction[] FractureActionsOf(string move,WildMind m)=>FractureActions(move,m)??Array.Empty<PlannedEnemyAction>();

        private static string FractureName(string move)=>move switch
        {
            "fr_slash"=>"FRACTURED SLASH","fr_echo_guard"=>"ECHO GUARD","fr_repeat"=>"REPEAT",
            "fr_bite"=>"FRACTURED BITE","fr_unstable_guard"=>"UNSTABLE GUARD","fr_core_pulse"=>"CORE PULSE","fr_fractured_command"=>"FRACTURED COMMAND",
            "fr_echo_claw"=>"ECHO CLAW","fr_flicker_guard"=>"FLICKER GUARD",
            "fr_mirror_strike"=>"MIRROR STRIKE","fr_reflective_shell"=>"REFLECTIVE SHELL","fr_shattered_reflection"=>"SHATTERED REFLECTION",
            "fr_binder_dup_strength"=>"DUPLICATE STRENGTH","fr_binder_dup_guard"=>"DUPLICATE GUARD","fr_rift_ward"=>"RIFT WARD","fr_stabilize"=>"STABILIZE",
            "fr_shear_strike"=>"SHEAR STRIKE","fr_resolve_echo"=>"RESOLVE ECHO","fr_temporal_guard"=>"TEMPORAL GUARD","fr_split_moment"=>"SPLIT MOMENT",
            "fr_crushing_bite"=>"CRUSHING BITE","fr_distorted_guard"=>"DISTORTED GUARD","fr_rift_maul"=>"RIFT MAUL",
            "fr_or_strike"=>"BROKEN STRIKE","fr_or_ward"=>"BROKEN WARD","fr_or_curse"=>"BROKEN CURSE","fr_or_surge"=>"BROKEN SURGE",
            "fr_col_slam"=>"COLOSSUS SLAM","fr_col_armor"=>"RIFT ARMOR","fr_col_hammer"=>"CRACKED HAMMER","fr_col_splinter"=>"SPLINTER GUARD","fr_col_storm"=>"FRAGMENT STORM","fr_col_crush"=>"CORE CRUSH",
            "fr_dup_slash"=>"DUPLICATE SLASH","fr_dup_guard"=>"DUPLICATE GUARD","fr_dup_ruin"=>"DUPLICATE RUIN","fr_dup_repeat"=>"REPEAT",
            "fr_sov_cut"=>"SOVEREIGN CUT","fr_sov_authority"=>"FRACTURED AUTHORITY","fr_sov_command"=>"COMMAND","fr_sov_collapse"=>"SOVEREIGN COLLAPSE",
            "fr_blade_fractured"=>"FRACTURED BLADE","fr_blade_razor"=>"RAZOR ECHO","fr_ward_fractured"=>"FRACTURED WARD","fr_ward_strike"=>"WARD STRIKE",
            "fr_loop_cut"=>"LOOP CUT","fr_loop_ward"=>"LOOP WARD","fr_loop_fracture"=>"LOOP FRACTURE","fr_loop_crush"=>"LOOP CRUSH","fr_loop_reinforce"=>"LOOP REINFORCE","fr_loop_barrage"=>"LOOP BARRAGE",
            "fr_um_strike"=>"UNMADE STRIKE","fr_um_memory"=>"FRACTURED MEMORY","fr_um_replay"=>"REPLAY","fr_um_rift_guard"=>"RIFT GUARD","fr_um_pulse"=>"BROKEN PULSE",
            "fr_um_rend"=>"COMPOSITE REND","fr_um_resolve"=>"RESOLVE ECHO","fr_um_fguard"=>"FRACTURED GUARD","fr_um_borrowed"=>"BORROWED STRENGTH","fr_um_composite"=>"COMPOSITE BARRAGE",
            "fr_um_world_rend"=>"WORLD REND","fr_um_barrage"=>"FRACTURED BARRAGE","fr_um_reality_guard"=>"REALITY GUARD","fr_um_collapse_wave"=>"COLLAPSE WAVE","fr_um_surge"=>"UNSTABLE SURGE",
            _=>"STRIKE"
        };
        // The label names the move and the rule that produced it, so a repeat, an Echo or a replay is never a surprise.
        private string FractureLabel(string move,WildMind m)
        {
            var name=FractureName(move);
            switch(move)
            {
                case "fr_repeat":return "REPEAT · "+FractureName(string.IsNullOrEmpty(m.lastSeed)?"fr_slash":m.lastSeed);
                case "fr_dup_repeat":return "REPEAT · "+FractureName(string.IsNullOrEmpty(m.lastSeed)?"fr_dup_slash":m.lastSeed)+" AT 125%";
                case "fr_shear_strike":return name+" · CREATES ECHO 12";
                case "fr_resolve_echo":return name+" · ECHO "+m.counter;
                case "fr_um_rend":return name+" · CREATES ECHO 10";
                case "fr_um_resolve":return name+" · ECHO "+m.counter;
                case "fr_um_memory":return name+" · REPLAYS "+FractureName(m.lastSeed==null||m.lastSeed==""?"fr_um_strike":m.lastSeed)+" NEXT";
                case "fr_um_replay":return "REPLAY · "+FractureName(string.IsNullOrEmpty(m.lastSeed)?"fr_um_rift_guard":m.lastSeed);
                case "fr_crushing_bite":return name+" · SOLID → FLICKER";
                case "fr_distorted_guard":return name+" · FLICKER → RIFT";
                case "fr_rift_maul":return name+" · RIFT → SOLID";
                case "fr_sov_collapse":return OwnedMinions(enemyContextIndex).Any()?name+" · FRAGMENTS ALIVE":name+" · NO FRAGMENTS";
                case FracturePairMove:return "POSSIBLE: "+string.Join(" / ",(m.pair??"").Split(new[]{'|'},StringSplitOptions.RemoveEmptyEntries).Select(FractureName));
            }
            if(enemyId==FracturedRealmContent.Loopkeeper&&move.StartsWith("fr_loop_"))
            {
                var s=Cycle(m,12);var replay=s%6>=3;
                return replay?name+" · LOOP REPLAY ACTIVE":name+$" · LOOP {(s<6?"A":"B")} {s%3+1}/3";
            }
            return name;
        }
        private static string FractureHook(string move)=>move switch
        {
            "fr_repeat" or "fr_dup_repeat" or "fr_um_replay"=>"action_repeated",
            "fr_shear_strike" or "fr_um_rend"=>"echo_created","fr_resolve_echo" or "fr_um_resolve"=>"echo_resolved",
            "fr_fractured_command" or "fr_sov_command"=>"fracture_command",
            "fr_binder_dup_strength" or "fr_binder_dup_guard"=>"rift_binder_duplicate",
            "fr_um_memory"=>"fractured_memory_recorded",
            _=>""
        };

        // ---------- resolution ----------
        private bool BeginFractureAction(WildMind m,string move)
        {
            if(!IsRepeatMove(move)&&move.StartsWith("fr_"))
                if(enemyId is FracturedRealmContent.Soldier or FracturedRealmContent.Duplicate or FracturedRealmContent.Unmade)m.lastSeed=move;
            switch(enemyId)
            {
                case FracturedRealmContent.Beast:
                    m.state=m.state=="FLICKER"?"RIFT":m.state=="RIFT"?"SOLID":"FLICKER";EmitHook("phase_beast_form:"+m.state);return false;
                case FracturedRealmContent.Oracle:
                    m.lastPair=m.pair;m.pair=PickPair(OraclePool,m.lastPair);EmitHook("oracle_pair_reveal");return true;
                case FracturedRealmContent.Shear:
                    if(move=="fr_resolve_echo")m.counter=0;return false;
                case FracturedRealmContent.Loopkeeper:
                {
                    var s=Cycle(m,12);
                    if(s%6==3)EmitHook("loopkeeper_replay_start");
                    if(s%6==5)EmitHook("loopkeeper_replay_finish");
                    return false;
                }
                case FracturedRealmContent.Unmade:
                    if(m.phase==2&&move=="fr_um_resolve")m.counter=0;
                    if(m.phase>=3)
                    {
                        m.lastPair=m.pair;m.pair=PickPair(UnmadePool,m.lastPair);EmitHook("unmade_pair_reveal");return true;
                    }
                    return false;
                default:return false;
            }
        }
        private void ExecuteFractureAction(PlannedEnemyAction action)
        {
            var self=enemyContextIndex;
            switch(action.type)
            {
                case EnemyActionType.EchoCreate:
                {
                    var m=Mind;m.counter=action.amount;Emit(CombatEventKind.Status,action.amount,false,null,"ECHO");break;
                }
                case EnemyActionType.Memory:break; // the stored move is shown in the label and the counter; the hook is the presentation receipt
                case EnemyActionType.DuplicateStrength:
                {
                    if(!BinderStrengthPair(self,out var src,out var tgt))break;
                    var gain=Math.Min(action.amount,(EnemyAt(src).strength+1)/2);if(gain<=0)break;
                    InEnemyContext(tgt,()=>{var g=EnemyBuffAfterWither(gain);enemy.strength+=g;Emit(CombatEventKind.Status,g,false,null,"STRENGTH");});break;
                }
                case EnemyActionType.DuplicateGuard:
                {
                    if(!BinderGuardPair(self,out var src,out var tgt,false))break;
                    var gain=Math.Min(action.amount,EnemyAt(src).block);if(gain<=0)break;
                    InEnemyContext(tgt,()=>{enemy.block+=gain;Emit(CombatEventKind.Block,gain,false);});break;
                }
                default:throw new InvalidOperationException("Enemy action has no executor: "+action.type);
            }
        }

        // ---------- deaths and thresholds ----------
        private bool FractureReplanOnDeath(int k)
        {
            var id=opponents[k].id;
            return id is FracturedRealmContent.Binder or FracturedRealmContent.Splitling or FracturedRealmContent.Sovereign;
        }
        private void FractureOnDeath(int index)
        {
            var id=opponents[index].id;
            if(id==FracturedRealmContent.SplitEcho)InEnemyContext(index,()=>EmitHook("split_echo_death"));
            else if(id is FracturedRealmContent.BladeFragment or FracturedRealmContent.WardFragment)InEnemyContext(index,()=>EmitHook("fragment_death"));
        }
        // Splitling at half health, Rift Colossus at 69/104 and 34/104, Split Sovereign at 130/190 and 70/190,
        // The Unmade at 274/410 and 137/410 (all proportional to the creature's actual maximum health).
        private static int Frac(int max,int num,int den)=>(int)Math.Round(max*(double)num/den,MidpointRounding.AwayFromZero);
        private bool CheckFractureThresholds(int index)
        {
            var o=opponents[index];var m=o.mind;var f=o.fighter;
            if(FracturedRealmContent.Find(o.id)==null)return false;
            switch(o.id)
            {
                case FracturedRealmContent.Splitling:
                    if(!m.flag&&f.hp<=f.maxHp/2)
                    {
                        // Splits exactly once. No heal, Strength or Block. Fewer Echoes if the board is full; never retried.
                        m.flag=true;m.phase=2;m.step=0;m.hold=false;
                        InEnemyContext(index,()=>EmitHook("splitling_split"));
                        var before=opponents.Count;var spawned=new List<int>();
                        for(var k=0;k<2;k++)
                        {
                            if(!CanSummon(index))break;
                            var living=Enumerable.Range(0,opponents.Count).Where(IsLivingTarget).ToHashSet();WildSummon(index);
                            foreach(var i in Enumerable.Range(0,opponents.Count))if(IsLivingTarget(i)&&!living.Contains(i))spawned.Add(i);
                        }
                        if(PlayerPhaseReplan){ReplanAt(index);foreach(var i in spawned)ReplanAt(i);}
                    }
                    return true;
                case FracturedRealmContent.Colossus:
                {
                    var t1=Frac(f.maxHp,69,104);var t2=f.maxHp*34/104;
                    var target=f.hp<=t2?3:f.hp<=t1?2:1;
                    while(m.phase<target)
                    {
                        m.phase++;m.step=0;m.hold=false;var phase=m.phase;
                        InEnemyContext(index,()=>EmitHook("rift_colossus_stage:"+phase));
                        if(PlayerPhaseReplan)ReplanAt(index);
                    }
                    return true;
                }
                case FracturedRealmContent.Sovereign:
                {
                    var t1=Frac(f.maxHp,130,190);var t2=Frac(f.maxHp,70,190);
                    if((m.counter&1)==0&&f.hp<=t1){m.counter|=1;SovereignSplit(index,m,"BLADE");}
                    if((m.counter&2)==0&&f.hp<=t2){m.counter|=2;SovereignSplit(index,m,"WARD");}
                    return true;
                }
                case FracturedRealmContent.Unmade:
                {
                    var t1=Frac(f.maxHp,274,410);var t2=Frac(f.maxHp,137,410);
                    var target=f.hp<=t2?3:f.hp<=t1?2:1;
                    while(m.phase<target)
                    {
                        // Transformation only: no heal, Strength, Fortify or Block. Pending replay and Echo are cleared.
                        m.phase++;m.step=0;m.hold=false;m.lastSeed="";m.counter=0;m.state="";var phase=m.phase;
                        if(phase>=3){m.lastPair="";m.pair=PickPair(UnmadePool,"");}
                        InEnemyContext(index,()=>
                        {
                            EmitHook(phase==2?"the_unmade_phase_1_to_2":"the_unmade_phase_2_to_3");
                            if(phase>=3)EmitHook("unmade_pair_reveal");
                        });
                        bossPhase=m.phase;memory.resolvedBossPhase=m.phase;
                        if(PlayerPhaseReplan)ReplanAt(index);
                    }
                    return true;
                }
            }
            return true;
        }
        private void SovereignSplit(int index,WildMind m,string type)
        {
            m.state=type;InEnemyContext(index,()=>EmitHook("split_sovereign_threshold:"+type));
            var living=Enumerable.Range(0,opponents.Count).Where(IsLivingTarget).ToHashSet();
            if(CanSummon(index))WildSummon(index);
            m.state="";
            if(PlayerPhaseReplan){ReplanAt(index);foreach(var i in Enumerable.Range(0,opponents.Count))if(IsLivingTarget(i)&&!living.Contains(i))ReplanAt(i);}
        }

        // ---------- presentation ----------
        private static string FractureForm(string state)=>string.IsNullOrEmpty(state)?"SOLID":state;
        private static string ColossusStageName(int phase)=>phase<=1?"WHOLE":phase==2?"CRACKED":"FRAGMENTED";
        private static string UnmadePhaseName(int phase)=>phase<=1?"UNSTABLE FORM":phase==2?"FRAGMENTED COMPOSITE":"LIVING REALITY TEAR";
        private bool FractureCounter(int index,WildMind m,out string label,out int value,out int max)
        {
            label="";value=max=0;
            switch(EnemyIdAt(index))
            {
                case FracturedRealmContent.Soldier:label=string.IsNullOrEmpty(m.lastSeed)?"PREVIOUS: NONE":"PREVIOUS: "+FractureName(m.lastSeed);return true;
                case FracturedRealmContent.Splitling:label=m.flag?"SPLIT":$"SPLITS AT {Math.Max(1,EnemyAt(index).maxHp/2)} HP";return true;
                case FracturedRealmContent.Husk:label=m.counter>0?$"COPIED +{m.counter}":"COPIES YOUR BUFFS";return true;
                case FracturedRealmContent.Shear:label=m.counter>0?$"PENDING ECHO: {m.counter} DAMAGE":"NO ECHO";return true;
                case FracturedRealmContent.Beast:label="FORM · "+FractureForm(m.state);return true;
                case FracturedRealmContent.Colossus:label="STAGE · "+ColossusStageName(m.phase);return true;
                case FracturedRealmContent.Duplicate:label=Cycle(m,6)%2==1?"REPEAT NEXT":"PREVIOUS: "+(string.IsNullOrEmpty(m.lastSeed)?"NONE":FractureName(m.lastSeed));return true;
                case FracturedRealmContent.Sovereign:label=$"FRAGMENTS {OwnedMinions(index).Count()}/2";return true;
                case FracturedRealmContent.Loopkeeper:
                {
                    var s=Cycle(m,12);label=s%6>=3?$"LOOP REPLAY ACTIVE · {(s<6?"A":"B")} {s%3+1}/3":$"LOOP {(s<6?"A":"B")} · {s%3+1}/3";return true;
                }
                case FracturedRealmContent.Unmade:
                    label=m.phase==2&&m.counter>0?$"PHASE 2 · PENDING ECHO: {m.counter}":$"PHASE {m.phase} · {UnmadePhaseName(m.phase)}";return true;
            }
            return false;
        }
        public bool FractureForecast(int index,WildMind m,out string title,out List<string> lines,out string tip)
        {
            title="";tip="";lines=new List<string>();
            var id=EnemyIdAt(index);
            if((id==FracturedRealmContent.Oracle||id==FracturedRealmContent.Unmade&&m.phase>=3)&&!string.IsNullOrEmpty(m.pair))
            {
                title="POSSIBLE NEXT ACTIONS";lines.AddRange(m.pair.Split('|').Select(p=>FractureSummary(p,m)));
                tip="It will use ONE of these two, chosen when it acts. Never both, and there is no third possibility. The pair is generated once and never rerolls.";return true;
            }
            if(id==FracturedRealmContent.Loopkeeper)
            {
                var s=Cycle(m,12);var next=(s+1)%12;var i=next%3;
                var mv=next<6?(i==0?"fr_loop_cut":i==1?"fr_loop_ward":"fr_loop_fracture"):(i==0?"fr_loop_crush":i==1?"fr_loop_reinforce":"fr_loop_barrage");
                title=next%6>=3?"UPCOMING REPLAY":"NEXT ACTION";lines.Add(FractureSummary(mv,m));
                tip="Its Current action is the intent above. The sequence is shown in full: three actions, then the exact same three once more, then the other sequence.";return true;
            }
            return false;
        }
        private string FractureSummary(string move,WildMind m)
        {
            var parts=new List<string>();
            foreach(var a in FractureActionsOf(move,m))
                switch(a.type)
                {
                    case EnemyActionType.Attack:parts.Add(a.hits>1?$"{a.amount}×{a.hits}":a.amount+" dmg");break;
                    case EnemyActionType.Block:parts.Add(a.amount+" Block");break;
                    case EnemyActionType.Strength:parts.Add("+"+a.amount+" Str");break;
                    case EnemyActionType.Weak:parts.Add(a.amount+" Weak");break;
                    case EnemyActionType.Vulnerable:parts.Add(a.amount+" Vuln");break;
                }
            return parts.Count==0?FractureName(move):FractureName(move)+" · "+string.Join(" + ",parts);
        }
        private void FractureStateText(int index,WildMind m,List<string> lines)
        {
            switch(EnemyIdAt(index))
            {
                case FracturedRealmContent.Soldier:lines.Add("REPEAT · Slash → Echo Guard → Repeat → Slash. Repeat redoes its previous completed non-Repeat action exactly (never another Repeat). The Repeat intent names the action.");
                    lines.Add(string.IsNullOrEmpty(m.lastSeed)?"No previous action yet.":"Previous action: "+FractureName(m.lastSeed)+".");break;
                case FracturedRealmContent.Splitling:lines.Add(m.flag?"SPLIT · It has split once. Core Pulse gives its Split Echoes Block; Fractured Command makes one Split Echo repeat its previous action.":$"SPLIT · At {Math.Max(1,EnemyAt(index).maxHp/2)} HP or less it splits once into up to 2 Split Echoes (as many as the battlefield allows). It does not heal or gain anything.");break;
                case FracturedRealmContent.SplitEcho:lines.Add("Echo Claw → Flicker Guard. Disappears when its Splitling dies.");break;
                case FracturedRealmContent.Husk:lines.Add("COPIES · After your turn it copies up to 2 total stacks of Strength and Fortify you gained during that turn (Strength first). It never copies Energy, Block, cards, relics or temporary effects. It never copies mid-card.");
                    if(m.counter>0)lines.Add($"It has copied {m.counter} stack(s) so far.");break;
                case FracturedRealmContent.Binder:lines.Add("SUPPORT · Never attacks. Duplicate Strength (half of an ally's Strength, max +2) and Duplicate Guard (up to 10 Block) copy between two different allies. Rift Ward, Stabilize. Leaves if only harmless Supports remain.");break;
                case FracturedRealmContent.Shear:lines.Add("ECHO · Shear Strike (12) leaves an Echo of 12. Resolve Echo then deals exactly that and gains 7 Block. Only one Echo at a time, always shown as PENDING ECHO.");break;
                case FracturedRealmContent.Beast:lines.Add($"FORM · {FractureForm(m.state)}. Solid: Crushing Bite (16 + 6 Block) → Flicker: Distorted Guard (20 Block + Weak) → Rift: Rift Maul (2×9) → Solid.");break;
                case FracturedRealmContent.Oracle:lines.Add("TWO POSSIBILITIES · It always shows exactly two possible next actions. One is chosen when it acts. No third possibility. It never shows the same pair twice in a row when another exists.");break;
                case FracturedRealmContent.Colossus:lines.Add($"STAGE · {ColossusStageName(m.phase)}. At 69/104 of its health it Cracks and at 34/104 it Fragments. Changing stage gives no heal, Strength or Block.");break;
                case FracturedRealmContent.Duplicate:lines.Add("REPEAT · Every second action repeats the previous non-Repeat action at 125% (rounded). Slash → Repeat → Guard → Repeat → Ruin → Repeat.");break;
                case FracturedRealmContent.Sovereign:lines.Add($"SPLITS · Summons a Blade Fragment at {Frac(EnemyAt(index).maxHp,130,190)} HP and a Ward Fragment at {Frac(EnemyAt(index).maxHp,70,190)} HP, once each. {OwnedMinions(index).Count()}/2 alive. Authority strengthens them, Command makes one repeat its previous action. Sovereign Collapse hits harder when none remain.");break;
                case FracturedRealmContent.BladeFragment:lines.Add("Fractured Blade → Razor Echo (2 hits).");break;
                case FracturedRealmContent.WardFragment:lines.Add("Fractured Ward (Block to the Sovereign) → Ward Strike.");break;
                case FracturedRealmContent.Loopkeeper:
                {
                    var s=Cycle(m,12);
                    lines.Add($"LOOP · Sequence {(s<6?"A: Loop Cut → Loop Ward → Loop Fracture":"B: Loop Crush → Loop Reinforce → Loop Barrage")}. After three actions it replays the exact same three once, then switches sequences. The replay is always labelled LOOP REPLAY ACTIVE.");
                    break;
                }
                case FracturedRealmContent.Unmade:
                    lines.Add($"PHASE {m.phase} · {UnmadePhaseName(m.phase)} — changes form at 274/410 and 137/410 of its health. No heal, Strength, Fortify or Block from changing form.");
                    if(m.phase==1)lines.Add("Fractured Memory stores its last move; the very next action replays it at normal value.");
                    else if(m.phase==2)lines.Add("Composite Rend leaves an Echo of 10 that Resolve Echo lands. Only one Echo at a time.");
                    else lines.Add("Shows exactly two possible next actions. One is chosen when it acts.");
                    break;
            }
        }
        private string DescribeFractureAction(EnemyActionType type,int amount,EnemyIntentAction a)
        {
            switch(type)
            {
                case EnemyActionType.EchoCreate:a.title="ECHO";return $"Creates an Echo of {amount} damage. It lands on this enemy's next Resolve Echo. Its value is fixed now and does not change.";
                case EnemyActionType.Memory:a.title="MEMORY";return "Stores its previous move. Its very next action repeats that move at normal value.";
                case EnemyActionType.DuplicateStrength:a.title="DUPLICATE STRENGTH";return $"Copies half of one ally's positive Strength (rounded up, maximum +{amount}) onto a different damage-dealing ally.";
                case EnemyActionType.DuplicateGuard:a.title="DUPLICATE GUARD";return $"Copies up to {amount} of one ally's Block onto a different ally. The source keeps its Block.";
            }
            return "";
        }
    }
}
