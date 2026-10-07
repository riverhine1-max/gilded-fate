using System;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Core;

namespace GildedFate.Combat
{
    // Act 2 · Theme 3: Shattered Observatory AI. Plugs into the shared themed-enemy engine (WildCombat.cs)
    // through the Hollowwood end of the chain. Identity: PREDICTION.
    //
    // Fairness rules this file keeps:
    //  - A shown deterministic intent never secretly changes.
    //  - A shown set of possibilities is always complete; the random pick happens when the enemy acts.
    //  - Every queue / pair / future is plain data on WildMind, so it saves and loads without rerolling.
    //  - RNG is used only for: Command targets, future-action selection (Chronoglyph, Blind Seer),
    //    Fallen Astrologer pair selection and pick, and Astral Curator Phase 3 pair selection and pick.
    //
    // WildMind use here:
    //   counter  = Orbit Plates (Sentinel, 0–3) · Momentum (Fallen Comet, 0–4)
    //   state    = Chronoglyph's CURRENT action
    //   queue    = Chronoglyph FUTURE action · Scribe / Curator queued next action · Blind Seer "current|next|following"
    //   pair     = the two possible next actions "a|b" (Fallen Astrologer, Curator Phase 3), lastPair = the previous pair
    //   plannedValue = Lenskeeper: Energy spent last turn (-1 = no previous turn) · Orrery Keeper: living Fragments
    //   flag     = Fallen Comet: Cool Orbit is owed after Impact
    //   phase    = Astral Curator phase
    public sealed partial class CombatState
    {
        public const int MaxPlates=3,MaxMomentum=4;
        public const string AstrologerPairMove="so_ast_pair",CuratorPairMove="so_cur_pair";
        private const string PlateHelp="Orbit Plates (0–3). Launch Plate spends one. Reassemble Orbit restores one. Each missing Plate sharpens Orbital Strike.";
        private const string MomentumHelp="Accelerate builds Momentum (0–4). At 4 Momentum the Comet uses Impact, then Momentum resets and it cools.";

        // Energy spent in the current / immediately previous player turn (Lenskeeper reads the previous one only).
        // lastTurnEnergySpent is -1 before there is a previous player turn.
        public int energySpentThisTurn,lastTurnEnergySpent=-1;
        internal void RollEnergyMemory(){lastTurnEnergySpent=turn>0?energySpentThisTurn:-1;energySpentThisTurn=0;RollRuinsMemory();}

        private static readonly string[] AstrologerPool={"so_starfall","so_astral_guard","so_falling_omen","so_celestial_surge"};
        private static readonly string[] ChronoPool={"so_time_cut","so_delay_ward","so_temporal_fracture","so_future_collapse"};
        private static readonly string[] SeerPool={"so_seers_cut","so_foresight_ward","so_doomed_vision","so_predicted_ruin","so_calm_future"};
        private static readonly string[] TwinFatePool={"so_cur_stellar_execution","so_cur_constellation_barrage","so_cur_celestial_fortress","so_cur_gravity_sentence","so_cur_astral_surge"};

        private static void ResetObservatoryState(WildMind m,string id)
        {
            m.queue="";m.pair="";m.lastPair="";
            if(id==ShatteredObservatoryContent.Sentinel)m.counter=MaxPlates;
            if(id==ShatteredObservatoryContent.Comet)m.counter=0;
            if(id==ShatteredObservatoryContent.Lenskeeper)m.plannedValue=-1;
            ResetRuinsState(m,id);
        }

        // ---------- queue / pair helpers (all RNG here) ----------
        private string PickFrom(IList<string> options)=>options[NextRandom(options.Count)];
        private string PickOther(string[] pool,string avoid)
        {
            var options=pool.Where(x=>x!=avoid).ToArray();return PickFrom(options.Length>0?options:pool);
        }
        // Two distinct actions, in pool order, never the same pair as the previous one when another exists.
        private string PickPair(string[] pool,string previous)
        {
            var all=new List<string>();
            for(var i=0;i<pool.Length;i++)for(var j=i+1;j<pool.Length;j++)all.Add(pool[i]+"|"+pool[j]);
            var fresh=all.Where(p=>p!=previous).ToList();
            return PickFrom(fresh.Count>0?fresh:all);
        }
        // Blind Seer: never the same action three times in a row.
        private string SeerNext(string two,string one)
        {
            var options=SeerPool.Where(x=>!(x==two&&x==one)).ToArray();return PickFrom(options);
        }
        private string[] SeerQueue(WildMind m)=>(m.queue??"").Split(new[]{'|'},StringSplitOptions.RemoveEmptyEntries);

        // ---------- move choice ----------
        private string ChooseObservatoryMove(WildMind m)
        {
            var self=enemyContextIndex;
            switch(enemyId)
            {
                case ShatteredObservatoryContent.Scribe:
                    switch(Cycle(m,4))
                    {
                        case 0:m.queue="";return "so_astral_bolt";
                        case 1:m.queue="so_falling_star";return "so_chart_stars";   // Falling Star is queued as the next action
                        case 2:m.queue="";return "so_falling_star";
                        default:m.queue="";return "so_arcane_margin";
                    }
                case ShatteredObservatoryContent.Sentinel:return Cycle(m,4) switch{0=>"so_orbit_guard",1=>"so_launch_plate",2=>"so_orbital_strike",_=>"so_reassemble"};
                case ShatteredObservatoryContent.Attendant:
                    switch(Cycle(m,4))
                    {
                        case 0:return "so_celestial_ward";
                        case 1:return "so_stellar_guidance";
                        case 2:return DamagedAllies(self).Any()?"so_correct_orbit":"so_astral_alignment";
                        default:return "so_astral_alignment";
                    }
                case ShatteredObservatoryContent.Chronoglyph:
                    if(string.IsNullOrEmpty(m.state)||string.IsNullOrEmpty(m.queue))
                    {
                        // First plan: Current is Time Cut, Future is one of the other three.
                        if(string.IsNullOrEmpty(m.state))m.state="so_time_cut";
                        m.queue=PickOther(ChronoPool,m.state);
                    }
                    return m.state;
                case ShatteredObservatoryContent.Lenskeeper:
                {
                    var spent=lastTurnEnergySpent;m.plannedValue=spent;
                    if(spent<0)return "so_balanced_lens";
                    return spent<=1?"so_underexposed_beam":spent==2?"so_balanced_lens":"so_overexposed_ward";
                }
                case ShatteredObservatoryContent.Weaver:
                {
                    var owned=OwnedMinions(self).Count();var canForm=CanSummon(self);
                    switch(Cycle(m,4))
                    {
                        case 0:return canForm?"so_form_constellation":"so_stellar_thread";
                        case 1:return "so_stellar_thread";
                        case 2:
                            if(owned>=2)return EligibleCommandTargets(self).Any()?"so_weaver_command":"so_stellar_thread";
                            return canForm?"so_form_constellation":"so_stellar_thread";
                        default:return OwnedMinions(self).Any(i=>EnemyAt(i).hp<EnemyAt(i).maxHp)?"so_realign":"so_stellar_thread";
                    }
                }
                case ShatteredObservatoryContent.StarFragment:return Cycle(m,2)==0?"so_star_pulse":"so_fragment_guard";
                case ShatteredObservatoryContent.Monk:return Cycle(m,4) switch{0=>"so_weighted_palm",1=>"so_gravity_guard",2=>"so_compression",_=>"so_collapse_point"};
                case ShatteredObservatoryContent.Astrologer:
                    if(string.IsNullOrEmpty(m.pair))m.pair=PickPair(AstrologerPool,m.lastPair);
                    return AstrologerPairMove;
                case ShatteredObservatoryContent.OrreryKeeper:
                {
                    var fragments=OwnedMinions(self).Count();m.plannedValue=fragments;
                    switch(Cycle(m,4))
                    {
                        case 0:return "so_celestial_rotation";
                        case 1:return "so_star_measure";
                        case 2:return EligibleCommandTargets(self).Any()?"so_orrery_command":"so_star_measure";
                        default:return fragments>0?"so_grand_alignment":"so_star_measure";
                    }
                }
                case ShatteredObservatoryContent.SunFragment:return Cycle(m,2)==0?"so_solar_flare":"so_radiant_surge";
                case ShatteredObservatoryContent.MoonFragment:return Cycle(m,2)==0?"so_lunar_guard":"so_crescent_strike";
                case ShatteredObservatoryContent.BlindSeer:
                {
                    var q=SeerQueue(m);
                    if(q.Length<3)
                    {
                        // First plan: Current, Next and Following all exist from the start.
                        var a=PickFrom(SeerPool);var b=SeerNext("",a);var c=SeerNext(a,b);
                        m.queue=a+"|"+b+"|"+c;q=new[]{a,b,c};
                    }
                    return q[0];
                }
                case ShatteredObservatoryContent.Comet:
                    if(m.flag)return "so_cool_orbit";
                    if(m.counter>=MaxMomentum)return "so_impact";
                    return Cycle(m,4) switch{0=>"so_accelerate",1=>"so_comet_strike",2=>"so_accelerate",_=>"so_falling_arc"};
                case ShatteredObservatoryContent.Curator:
                    if(m.phase>=3)
                    {
                        if(string.IsNullOrEmpty(m.pair))m.pair=PickPair(TwinFatePool,m.lastPair);
                        return CuratorPairMove;
                    }
                    {
                        var pos=Cycle(m,5);var move=CuratorMoveAt(m.phase,pos);
                        // Archive the Future / Predicted Collapse queue the next action; it is visible until it is used.
                        m.queue=move is "so_cur_archive" or "so_cur_predicted_collapse"?CuratorMoveAt(m.phase,pos+1):"";
                        return move;
                    }
            }
            return ChooseRuinsMove(m);
        }
        private static string CuratorMoveAt(int phase,int pos)
        {
            pos=((pos%5)+5)%5;
            if(phase<=1)return pos switch{0=>"so_cur_beam",1=>"so_cur_astral_ward",2=>"so_cur_archive",3=>"so_cur_grand_alignment",_=>"so_cur_cosmic_measure"};
            return pos switch{0=>"so_cur_orbiting_blades",1=>"so_cur_gravity_lock",2=>"so_cur_predicted_collapse",3=>"so_cur_collapse_event",_=>"so_cur_celestial_split"};
        }

        // ---------- moves ----------
        private PlannedEnemyAction[] ObservatoryActions(string move,WildMind m)
        {
            const EnemyActionType A=EnemyActionType.Attack,B=EnemyActionType.Block,S=EnemyActionType.Strength,W=EnemyActionType.Weak,V=EnemyActionType.Vulnerable;
            switch(move)
            {
                // Starbound Scribe
                case "so_astral_bolt":return new[]{P(A,10)};
                case "so_chart_stars":return new[]{P(B,9)};
                case "so_falling_star":return new[]{P(A,15)};
                case "so_arcane_margin":return new[]{P(A,7),P(B,6)};
                // Orbiting Sentinel (reads Plates when asked; they only change through its own actions)
                case "so_orbit_guard":return new[]{P(B,5*Math.Max(0,m.counter))};
                case "so_launch_plate":return new[]{P(EnemyActionType.PlateSpend,1),P(A,7),P(B,5)};
                case "so_orbital_strike":return new[]{P(A,10+3*Math.Max(0,MaxPlates-m.counter))};
                case "so_reassemble":return new[]{P(EnemyActionType.PlateGain,1),P(B,7)};
                // Astral Attendant
                case "so_celestial_ward":return new[]{P(EnemyActionType.BlockLowestAlly,10)};
                case "so_stellar_guidance":return new[]{P(EnemyActionType.StrengthRandomAlly,1),P(EnemyActionType.BlockTarget,5)};
                case "so_correct_orbit":return new[]{P(EnemyActionType.HealDamagedAlly,6)};
                case "so_astral_alignment":return new[]{P(EnemyActionType.BlockAllies,5),P(EnemyActionType.StrengthRandomAlly,1)};
                // Chronoglyph
                case "so_time_cut":return new[]{P(A,11)};
                case "so_delay_ward":return new[]{P(B,14)};
                case "so_temporal_fracture":return new[]{P(A,7),P(V,1)};
                case "so_future_collapse":return new[]{P(A,17)};
                // Lenskeeper
                case "so_underexposed_beam":return new[]{P(A,15)};
                case "so_balanced_lens":return new[]{P(A,11),P(B,7)};
                case "so_overexposed_ward":return new[]{P(B,16),P(A,7)};
                // Constellation Weaver / Star Fragment
                case "so_form_constellation":return new[]{P(EnemyActionType.Summon,1),P(B,5)};
                case "so_stellar_thread":return new[]{P(A,9)};
                case "so_realign":return new[]{P(EnemyActionType.HealMinion,6),P(EnemyActionType.BlockTarget,5)};
                case "so_weaver_command":return new[]{P(EnemyActionType.Command,1),P(B,5)};
                case "so_star_pulse":return new[]{P(A,6)};
                case "so_fragment_guard":return new[]{P(B,7)};
                // Gravity Monk
                case "so_weighted_palm":return new[]{P(A,10),P(W,1)};
                case "so_gravity_guard":return new[]{P(A,6),P(B,12)};
                case "so_compression":return new[]{P(S,1),P(B,8)};
                case "so_collapse_point":return new[]{P(A,17)};
                // Fallen Astrologer (one of the two shown possibilities is chosen when it acts)
                case "so_starfall":return new[]{P(A,16)};
                case "so_astral_guard":return new[]{P(B,18)};
                case "so_falling_omen":return new[]{P(A,9),P(W,1)};
                case "so_celestial_surge":return new[]{P(S,1),P(B,9)};
                case AstrologerPairMove:case CuratorPairMove:
                    // Display / fallback form only: both possibilities, never executed together.
                    return (m.pair??"").Split(new[]{'|'},StringSplitOptions.RemoveEmptyEntries).SelectMany(x=>ObservatoryActions(x,m)).ToArray();
                // The Orrery Keeper and its Fragments
                case "so_celestial_rotation":return new[]{P(EnemyActionType.BlockMinions,6),P(B,8)};
                case "so_star_measure":return new[]{P(A,14)};
                case "so_orrery_command":return new[]{P(EnemyActionType.Command,1),P(B,6)};
                case "so_grand_alignment":return new[]{P(A,m.plannedValue>=2?17:14),P(EnemyActionType.StrengthMinions,1)};
                case "so_solar_flare":return new[]{P(A,10)};
                case "so_radiant_surge":return new[]{P(S,1),P(A,6)};
                case "so_lunar_guard":return new[]{P(EnemyActionType.BlockOwner,9),P(B,5)};
                case "so_crescent_strike":return new[]{P(A,8)};
                // The Blind Seer
                case "so_seers_cut":return new[]{P(A,15)};
                case "so_foresight_ward":return new[]{P(B,21)};
                case "so_doomed_vision":return new[]{P(W,1),P(V,1)};
                case "so_predicted_ruin":return new[]{P(A,20)};
                case "so_calm_future":return new[]{P(S,1),P(B,10)};
                // The Fallen Comet
                case "so_accelerate":return new[]{P(EnemyActionType.Momentum,1),P(B,8)};
                case "so_comet_strike":return new[]{P(A,16+2*m.counter)};
                case "so_falling_arc":return new[]{P(A,8+m.counter,2)};
                case "so_impact":return new[]{P(A,32),P(EnemyActionType.MomentumReset,0)};
                case "so_cool_orbit":return new[]{P(B,15)};
                // The Astral Curator
                case "so_cur_beam":return new[]{P(A,14)};
                case "so_cur_astral_ward":return new[]{P(B,18)};
                case "so_cur_cosmic_measure":return new[]{P(A,8),P(W,1)};
                case "so_cur_archive":return new[]{P(B,8)};
                case "so_cur_grand_alignment":return new[]{P(A,18),P(S,1)};
                case "so_cur_orbiting_blades":return new[]{P(A,6,3)};
                case "so_cur_gravity_lock":return new[]{P(W,1),P(B,13)};
                case "so_cur_celestial_split":return new[]{P(A,10),P(B,10)};
                case "so_cur_predicted_collapse":return new[]{P(B,7)};
                case "so_cur_collapse_event":return new[]{P(A,22)};
                case "so_cur_stellar_execution":return new[]{P(A,21)};
                case "so_cur_constellation_barrage":return new[]{P(A,4,6)};
                case "so_cur_celestial_fortress":return new[]{P(B,22),P(S,1)};
                case "so_cur_gravity_sentence":return new[]{P(A,13),P(V,1)};
                case "so_cur_astral_surge":return new[]{P(S,2),P(B,8)};
            }
            return RuinsActions(move,m);
        }
        private static string ObservatoryName(string move)=>move switch
        {
            "so_astral_bolt"=>"ASTRAL BOLT","so_chart_stars"=>"CHART THE STARS","so_falling_star"=>"FALLING STAR","so_arcane_margin"=>"ARCANE MARGIN",
            "so_orbit_guard"=>"ORBIT GUARD","so_launch_plate"=>"LAUNCH PLATE","so_orbital_strike"=>"ORBITAL STRIKE","so_reassemble"=>"REASSEMBLE ORBIT",
            "so_celestial_ward"=>"CELESTIAL WARD","so_stellar_guidance"=>"STELLAR GUIDANCE","so_correct_orbit"=>"CORRECT THE ORBIT","so_astral_alignment"=>"ASTRAL ALIGNMENT",
            "so_time_cut"=>"TIME CUT","so_delay_ward"=>"DELAY WARD","so_temporal_fracture"=>"TEMPORAL FRACTURE","so_future_collapse"=>"FUTURE COLLAPSE",
            "so_underexposed_beam"=>"UNDEREXPOSED BEAM","so_balanced_lens"=>"BALANCED LENS","so_overexposed_ward"=>"OVEREXPOSED WARD",
            "so_form_constellation"=>"FORM CONSTELLATION","so_stellar_thread"=>"STELLAR THREAD","so_realign"=>"REALIGN","so_weaver_command"=>"COMMAND",
            "so_star_pulse"=>"STAR PULSE","so_fragment_guard"=>"ORBIT GUARD",
            "so_weighted_palm"=>"WEIGHTED PALM","so_gravity_guard"=>"GRAVITY GUARD","so_compression"=>"COMPRESSION","so_collapse_point"=>"COLLAPSE POINT",
            "so_starfall"=>"STARFALL","so_astral_guard"=>"ASTRAL GUARD","so_falling_omen"=>"FALLING OMEN","so_celestial_surge"=>"CELESTIAL SURGE",
            "so_celestial_rotation"=>"CELESTIAL ROTATION","so_star_measure"=>"STAR MEASURE","so_orrery_command"=>"COMMAND","so_grand_alignment"=>"GRAND ALIGNMENT",
            "so_solar_flare"=>"SOLAR FLARE","so_radiant_surge"=>"RADIANT SURGE","so_lunar_guard"=>"LUNAR GUARD","so_crescent_strike"=>"CRESCENT STRIKE",
            "so_seers_cut"=>"SEER'S CUT","so_foresight_ward"=>"FORESIGHT WARD","so_doomed_vision"=>"DOOMED VISION","so_predicted_ruin"=>"PREDICTED RUIN","so_calm_future"=>"CALM THE FUTURE",
            "so_accelerate"=>"ACCELERATE","so_comet_strike"=>"COMET STRIKE","so_falling_arc"=>"FALLING ARC","so_impact"=>"IMPACT","so_cool_orbit"=>"COOL ORBIT",
            "so_cur_beam"=>"CURATOR'S BEAM","so_cur_astral_ward"=>"ASTRAL WARD","so_cur_cosmic_measure"=>"COSMIC MEASURE","so_cur_archive"=>"ARCHIVE THE FUTURE","so_cur_grand_alignment"=>"GRAND ALIGNMENT",
            "so_cur_orbiting_blades"=>"ORBITING BLADES","so_cur_gravity_lock"=>"GRAVITY LOCK","so_cur_celestial_split"=>"CELESTIAL SPLIT","so_cur_predicted_collapse"=>"PREDICTED COLLAPSE","so_cur_collapse_event"=>"COLLAPSE EVENT",
            "so_cur_stellar_execution"=>"STELLAR EXECUTION","so_cur_constellation_barrage"=>"CONSTELLATION BARRAGE","so_cur_celestial_fortress"=>"CELESTIAL FORTRESS","so_cur_gravity_sentence"=>"GRAVITY SENTENCE","so_cur_astral_surge"=>"ASTRAL SURGE",
            _=>"STRIKE"
        };
        // "16 damage" / "6 × 3 damage" / "18 Block" / "+1 Strength" … a short line for the forecast panel.
        private string ObservatorySummary(string move,WildMind m)
        {
            var actions=ObservatoryActions(move,m);if(actions==null)return ObservatoryName(move);
            var parts=new List<string>();
            foreach(var a in actions)
                switch(a.type)
                {
                    case EnemyActionType.Attack:parts.Add(a.hits>1?$"{a.amount}×{a.hits}":a.amount.ToString());break;
                    case EnemyActionType.Block:parts.Add(a.amount+" Block");break;
                    case EnemyActionType.Strength:parts.Add("+"+a.amount+" Str");break;
                    case EnemyActionType.Weak:parts.Add(a.amount+" Weak");break;
                    case EnemyActionType.Vulnerable:parts.Add(a.amount+" Vuln");break;
                }
            return parts.Count==0?ObservatoryName(move):ObservatoryName(move)+" · "+string.Join(" + ",parts);
        }
        private string ObservatoryMoveLabel(string move,WildMind m)
        {
            switch(move)
            {
                case "so_chart_stars":return "CHART THE STARS · FALLING STAR NEXT";
                case "so_orbit_guard":return $"ORBIT GUARD · {m.counter} PLATE{(m.counter==1?"":"S")}";
                case "so_orbital_strike":return MaxPlates-m.counter>0?$"ORBITAL STRIKE · {MaxPlates-m.counter} MISSING":"ORBITAL STRIKE";
                case "so_underexposed_beam":return "UNDEREXPOSED BEAM";
                case "so_cur_archive":return "ARCHIVE THE FUTURE · "+ObservatoryName(m.queue)+" NEXT";
                case "so_cur_predicted_collapse":return "PREDICTED COLLAPSE · "+ObservatoryName(m.queue)+" NEXT";
                case "so_grand_alignment":return m.plannedValue>=2?"GRAND ALIGNMENT · BOTH ALIGNED":"GRAND ALIGNMENT · ONE ALIGNED";
                case "so_comet_strike":return m.counter>0?$"COMET STRIKE · {m.counter} MOMENTUM":"COMET STRIKE";
                case "so_falling_arc":return m.counter>0?$"FALLING ARC · {m.counter} MOMENTUM":"FALLING ARC";
                case "so_impact":return "IMPACT · 4 MOMENTUM";
                case AstrologerPairMove:case CuratorPairMove:
                    return "POSSIBLE: "+string.Join(" / ",(m.pair??"").Split(new[]{'|'},StringSplitOptions.RemoveEmptyEntries).Select(ObservatoryName));
            }
            return move.StartsWith("gr_")||move.StartsWith("bc_")||move.StartsWith("fr_")||move.StartsWith("gt_")||move.StartsWith("nt_")?RuinsMoveLabel(move,m):ObservatoryName(move);
        }
        private static string ObservatoryHook(string move)=>move switch
        {
            "so_chart_stars"=>"scribe_chart_stars","so_falling_star"=>"scribe_falling_star",
            "so_launch_plate"=>"sentinel_plate_launch","so_reassemble"=>"sentinel_plate_reassemble",
            "so_form_constellation"=>"weaver_form_constellation","so_weaver_command"=>"weaver_command","so_realign"=>"weaver_realign",
            "so_collapse_point"=>"gravity_monk_collapse_point","so_future_collapse"=>"chronoglyph_future_collapse",
            "so_celestial_rotation"=>"orrery_celestial_rotation","so_orrery_command"=>"orrery_keeper_command","so_grand_alignment"=>"orrery_grand_alignment",
            "so_predicted_ruin"=>"blind_seer_predicted_ruin","so_impact"=>"fallen_comet_impact","so_cool_orbit"=>"fallen_comet_cool_orbit",
            "so_cur_archive"=>"curator_archive_future","so_cur_predicted_collapse"=>"curator_predicted_collapse","so_cur_collapse_event"=>"curator_collapse_event",
            "so_cur_grand_alignment"=>"curator_grand_alignment",
            "so_cur_stellar_execution" or "so_cur_constellation_barrage" or "so_cur_celestial_fortress" or "so_cur_gravity_sentence" or "so_cur_astral_surge"=>"curator_twin_fate_resolved",
            _=>RuinsHook(move)
        };

        // ---------- resolution ----------
        // Runs before the enemy's turn is read. A shown pair resolves to one action here (and only here).
        // In an intent preview both possibilities are written to the preview instead of the one picked.
        internal void PreResolveObservatory()
        {
            var m=Mind;if(m==null||!IsWildContext)return;
            if(string.IsNullOrEmpty(m.planned)&&(enemyId==ShatteredObservatoryContent.Astrologer||enemyId==ShatteredObservatoryContent.Curator||enemyId==FracturedRealmContent.Oracle||enemyId==FracturedRealmContent.Unmade))PlanWildIntent();
            if(m.planned!=AstrologerPairMove&&m.planned!=CuratorPairMove&&m.planned!=FracturePairMove)return;
            var parts=(m.pair??"").Split(new[]{'|'},StringSplitOptions.RemoveEmptyEntries);
            if(parts.Length!=2)throw new InvalidOperationException("A possibility pair must hold exactly two actions.");
            if(intentPreviewSink!=null&&wildSinkMute==0&&EnemyContextIndex<intentPreviewSink.Length)
            {
                for(var g=0;g<2;g++)
                    foreach(var action in parts[g].StartsWith("fr_")?FractureActionsOf(parts[g],m):ObservatoryActions(parts[g],m))
                    {
                        var shown=DescribeEnemyAction(action);shown.choice=g;shown.detail+="\nOne of two possible actions. Only one is chosen, when this creature acts.";
                        if(shown.amount<=0&&action.type==EnemyActionType.HealDamagedAlly)continue;
                        intentPreviewSink[EnemyContextIndex].Add(shown);
                    }
                pairSinkHeld=true;
            }
            var pick=parts[NextRandom(2)];m.planned=pick;
            EmitHook(enemyId==ShatteredObservatoryContent.Curator?"astral_curator_twin_fate_chosen":m.planned.StartsWith("fr_")?"fracture_pair_chosen":"astrologer_pair_chosen");
        }
        [NonSerialized] private bool pairSinkHeld;
        private void ReleasePairSink()=>pairSinkHeld=false;

        // Returns true when the enemy keeps (or sets) its own pattern position; false lets the engine advance the pattern one step.
        private bool BeginObservatoryAction(WildMind m,string move)
        {
            switch(enemyId)
            {
                case ShatteredObservatoryContent.Chronoglyph:
                    // The Future becomes Current; a new Future is generated (never the same action twice in a row).
                    m.state=string.IsNullOrEmpty(m.queue)?PickOther(ChronoPool,move):m.queue;
                    m.queue=PickOther(ChronoPool,m.state);
                    EmitHook("chronoglyph_future_to_current");EmitHook("chronoglyph_future_shift");EmitHook("future_intent_generated");
                    return true;
                case ShatteredObservatoryContent.Lenskeeper:
                    EmitHook("lenskeeper_response:"+(move=="so_underexposed_beam"?"UNDER":move=="so_overexposed_ward"?"OVER":"BALANCED"));
                    return false;
                case ShatteredObservatoryContent.Scribe:
                    if(move=="so_chart_stars")EmitHook("future_intent_generated");
                    if(move=="so_falling_star")EmitHook("future_to_current");
                    return false;
                case ShatteredObservatoryContent.Astrologer:
                    m.lastPair=m.pair;m.pair=PickPair(AstrologerPool,m.lastPair);
                    EmitHook("astrologer_pair_reveal");EmitHook("future_intent_generated");
                    return true;
                case ShatteredObservatoryContent.BlindSeer:
                {
                    var q=SeerQueue(m);
                    if(q.Length==3){m.queue=q[1]+"|"+q[2]+"|"+SeerNext(q[1],q[2]);}
                    else m.queue="";
                    EmitHook("blind_seer_queue_shift");EmitHook("future_to_current");EmitHook("future_intent_generated");
                    return true;
                }
                case ShatteredObservatoryContent.Comet:
                    if(move=="so_impact"){m.flag=true;return true;}
                    if(move=="so_cool_orbit"){m.flag=false;return true;}
                    return false;
                case ShatteredObservatoryContent.Curator:
                    if(m.phase>=3)
                    {
                        m.lastPair=m.pair;m.pair=PickPair(TwinFatePool,m.lastPair);
                        EmitHook("astral_curator_twin_fate_reveal");EmitHook("future_intent_generated");
                        return true;
                    }
                    if(move is "so_cur_archive" or "so_cur_predicted_collapse")EmitHook("future_intent_generated");
                    if(move is "so_cur_grand_alignment" or "so_cur_collapse_event")EmitHook("future_to_current");
                    if(!(move is "so_cur_archive" or "so_cur_predicted_collapse"))m.queue=""; // the queue stays until the queued action is chosen
                    return false;
                default:return BeginRuinsAction(m,move);
            }
        }

        private void PlateChange(int index,int delta)
        {
            var m=MindAt(index);if(m==null)return;var before=m.counter;m.counter=Math.Max(0,Math.Min(MaxPlates,m.counter+delta));if(m.counter==before)return;
            InEnemyContext(index,()=>Emit(CombatEventKind.Status,m.counter-before,false,null,"ORBIT PLATE"));
        }
        private void MomentumChange(int index,int delta)
        {
            var m=MindAt(index);if(m==null)return;var before=m.counter;m.counter=Math.Max(0,Math.Min(MaxMomentum,m.counter+delta));if(m.counter==before)return;
            InEnemyContext(index,()=>
            {
                Emit(CombatEventKind.Status,m.counter-before,false,null,"MOMENTUM");
                if(delta>0){EmitHook("fallen_comet_momentum");if(m.counter>=MaxMomentum)EmitHook("fallen_comet_full_momentum");}
            });
        }
        private void ExecuteObservatoryAction(PlannedEnemyAction action)
        {
            var self=enemyContextIndex;
            switch(action.type)
            {
                case EnemyActionType.PlateSpend:PlateChange(self,-action.amount);break;
                case EnemyActionType.PlateGain:PlateChange(self,action.amount);break;
                case EnemyActionType.Momentum:MomentumChange(self,action.amount);break;
                case EnemyActionType.MomentumReset:MomentumChange(self,-MaxMomentum);break;
                default:ExecuteRuinsAction(action);break;
            }
        }

        // ---------- deaths and thresholds ----------
        // Moves whose legal targets or values depend on living Minions / damaged allies are re-read while the player can see it.
        private bool ObservatoryReplanOnDeath(int k)
        {
            var id=opponents[k].id;
            return id is ShatteredObservatoryContent.OrreryKeeper or ShatteredObservatoryContent.Weaver or ShatteredObservatoryContent.Attendant||RuinsReplanOnDeath(k);
        }
        // Astral Curator: 2/3 of maximum health rounded up (207 at the base 310) and 1/3 rounded down (103).
        private bool CheckObservatoryThresholds(int index)
        {
            var o=opponents[index];var m=o.mind;var f=o.fighter;
            if(ShatteredObservatoryContent.Find(o.id)==null)return CheckRuinsThresholds(index);
            if(o.id!=ShatteredObservatoryContent.Curator)return true;
            var t1=(f.maxHp*2+2)/3;var t2=f.maxHp/3;
            var target=f.hp<=t2?3:f.hp<=t1?2:1;
            while(m.phase<target)
            {
                // The Curator fractures: no heal, Strength, Fortify or Block, and any queued future intent is discarded.
                m.phase++;m.step=0;m.hold=false;m.queue="";var phase=m.phase;
                if(phase>=3){m.lastPair="";m.pair=PickPair(TwinFatePool,"");}
                InEnemyContext(index,()=>
                {
                    EmitHook(phase==2?"astral_curator_phase2":"astral_curator_phase3");
                    if(phase>=3){EmitHook("astral_curator_constellation_transformation");EmitHook("astral_curator_twin_fate_reveal");}
                });
                bossPhase=m.phase;memory.resolvedBossPhase=m.phase;
                if(PlayerPhaseReplan)ReplanAt(index);
            }
            return true;
        }

        // ---------- presentation ----------
        private bool ObservatoryCounter(int index,WildMind m,out string label,out int value,out int max)
        {
            label="";value=max=0;
            switch(EnemyIdAt(index))
            {
                case ShatteredObservatoryContent.Sentinel:label="ORBIT PLATES";value=m.counter;max=MaxPlates;return true;
                case ShatteredObservatoryContent.Comet:label="MOMENTUM";value=m.counter;max=MaxMomentum;return true;
                case ShatteredObservatoryContent.Lenskeeper:
                    label=m.plannedValue<0?"NO PREVIOUS TURN":"LAST TURN: "+m.plannedValue+" ENERGY";return true;
                case ShatteredObservatoryContent.Curator:label=m.phase==1?"PHASE 1 · ARCHIVE":m.phase==2?"PHASE 2 · FRACTURED":"PHASE 3 · TWIN FATE";return true;
            }
            return RuinsCounter(index,m,out label,out value,out max);
        }
        // The forecast panel next to an Observatory enemy. The first line is a title; later lines are the shown future.
        // Returns false when this enemy shows nothing beyond its current intent.
        public bool WildForecast(int index,out string title,out List<string> lines,out string tip)
        {
            title="";tip="";lines=new List<string>();var m=MindAt(index);if(m==null||!IsLivingTarget(index))return false;
            switch(EnemyIdAt(index))
            {
                case ShatteredObservatoryContent.Scribe:
                    if(string.IsNullOrEmpty(m.queue))return false;
                    title="FUTURE INTENT";lines.Add(ObservatorySummary(m.queue,m));tip="Queued: this will be its next action. It cannot change.";return true;
                case ShatteredObservatoryContent.Chronoglyph:
                    if(string.IsNullOrEmpty(m.queue))return false;
                    title="FUTURE";lines.Add(ObservatorySummary(m.queue,m));tip="Its Current action is the intent above. This Future action becomes Current when it acts, and a new Future is generated.";return true;
                case ShatteredObservatoryContent.Astrologer:
                    if(string.IsNullOrEmpty(m.pair))return false;
                    title="POSSIBLE NEXT ACTIONS";lines.AddRange(m.pair.Split('|').Select(p=>ObservatorySummary(p,m)));
                    tip="It will use ONE of these two, chosen when it acts. Not both.";return true;
                case ShatteredObservatoryContent.BlindSeer:
                {
                    var q=SeerQueue(m);if(q.Length<3)return false;
                    title="NEXT · FOLLOWING";lines.Add("NEXT: "+ObservatorySummary(q[1],m));lines.Add("THEN: "+ObservatorySummary(q[2],m));
                    tip="Its Current action is the intent above. After it acts, Next becomes Current and Following becomes Next. A new Following is generated.";return true;
                }
                case ShatteredObservatoryContent.Curator:
                    if(m.phase>=3)
                    {
                        if(string.IsNullOrEmpty(m.pair))return false;
                        title="POSSIBLE NEXT ACTIONS";lines.AddRange(m.pair.Split('|').Select(p=>ObservatorySummary(p,m)));
                        tip="Twin Fate: it will use ONE of these two, chosen when it acts. Not both.";return true;
                    }
                    {
                        // Phases 1 and 2 show the one Future Intent: the queued action, or the next move of the pattern.
                        var next=!string.IsNullOrEmpty(m.queue)?m.queue:CuratorMoveAt(m.phase,Cycle(m,5)+1);
                        title="FUTURE INTENT";lines.Add(ObservatorySummary(next,m));
                        tip=!string.IsNullOrEmpty(m.queue)?"Queued: this will be its next action. It cannot change.":"Its next action after the Current one. It does not change unless its form does.";return true;
                    }
            }
            return RuinsForecast(index,m,out title,out lines,out tip);
        }
        private void ObservatoryStateText(int index,WildMind m,List<string> lines)
        {
            switch(EnemyIdAt(index))
            {
                case ShatteredObservatoryContent.Scribe:lines.Add("Chart the Stars queues Falling Star as its next action, and the queue is shown.");break;
                case ShatteredObservatoryContent.Sentinel:lines.Add($"ORBIT PLATES · {m.counter}/3 — "+PlateHelp);break;
                case ShatteredObservatoryContent.Attendant:lines.Add("SUPPORT · Never attacks. Wards, guides, heals and aligns its allies. Flees if only harmless Supports remain.");break;
                case ShatteredObservatoryContent.Chronoglyph:lines.Add("It always shows a Current and a Future action. When it acts, the Future becomes Current and a new Future is generated.");break;
                case ShatteredObservatoryContent.Lenskeeper:
                    lines.Add("Changes its next action based on Energy spent during your previous turn.");
                    lines.Add(m.plannedValue<0?"No previous turn: Balanced Lens.":$"Last turn you spent {m.plannedValue} Energy. 0–1: Underexposed Beam. 2: Balanced Lens. 3+: Overexposed Ward.");break;
                case ShatteredObservatoryContent.Weaver:lines.Add($"SUMMONER · {OwnedMinions(index).Count()}/2 Star Fragments. Forms one at a time, mends them and commands them.");break;
                case ShatteredObservatoryContent.Monk:lines.Add("Weighted Palm → Gravity Guard → Compression → Collapse Point.");break;
                case ShatteredObservatoryContent.Astrologer:lines.Add("TWIN PREDICTION · It shows two possible next actions. One is chosen when it acts. It never uses both.");break;
                case ShatteredObservatoryContent.OrreryKeeper:lines.Add($"FRAGMENTS · {OwnedMinions(index).Count()}/2 in orbit. It never replaces a fallen Fragment. Grand Alignment is stronger the more Fragments live.");break;
                case ShatteredObservatoryContent.BlindSeer:lines.Add("It previews Current, Next and Following. The queue persists; it never repeats one action three times in a row.");break;
                case ShatteredObservatoryContent.Comet:lines.Add($"MOMENTUM · {m.counter}/4 — "+MomentumHelp);break;
                case ShatteredObservatoryContent.Curator:
                    lines.Add($"PHASE {m.phase} · "+(m.phase==1?"The Archive":m.phase==2?"The Fractured Archive":"Twin Fate")
                        +(m.phase<3?" — fractures at "+(m.phase==1?"2/3":"1/3")+" health. No heal, Strength or Block from fracturing.":" — it shows two possible next actions and uses one."));break;
                case ShatteredObservatoryContent.StarFragment:lines.Add("Star Pulse → Orbit Guard.");break;
                case ShatteredObservatoryContent.SunFragment:lines.Add("Solar Flare → Radiant Surge (gains Strength).");break;
                case ShatteredObservatoryContent.MoonFragment:lines.Add("Lunar Guard (shields its Keeper) → Crescent Strike.");break;
            }
            RuinsStateText(index,m,lines);
        }
        private string DescribeObservatoryAction(EnemyActionType type,int amount,EnemyIntentAction a)
        {
            switch(type)
            {
                case EnemyActionType.PlateSpend:a.title="LAUNCH PLATE";return $"Spends {amount} Orbit Plate. "+PlateHelp;
                case EnemyActionType.PlateGain:a.title="REASSEMBLE";return $"Gains {amount} Orbit Plate (maximum 3).";
                case EnemyActionType.Momentum:a.title="MOMENTUM";return $"Gains {amount} Momentum (maximum 4). "+MomentumHelp;
                case EnemyActionType.MomentumReset:a.title="IMPACT";return "Its Momentum resets to 0.";
            }
            return DescribeRuinsAction(type,amount,a);
        }
    }
}
