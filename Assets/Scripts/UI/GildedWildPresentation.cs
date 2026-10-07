using System.Collections.Generic;
using System.Linq;
using GildedFate.Combat;
using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    // Presentation for themed enemies (Ashen Wilds): the Minion badge, Summon / Command
    // intent art, enemy-specific state text, short callouts for mechanic hooks, the
    // Cinder Alpha's phase art and motion profiles that reuse the existing animations.
    // Rules live in Combat/WildCombat.cs; nothing here changes combat state.
    public sealed partial class GildedMainMenu
    {
        private sealed class WildCallout{public int index;public string text;public float at;public Color color;}
        private readonly List<WildCallout> wildCallouts=new();

        // Phase bosses' art follows their phase: Art/Enemies/<id>(_phase2|_phase3).
        // The Hollow Parasite's exposed form uses <id>_phase2 too (it is looked up by creature index).
        private string EnemyArtId(string id,int index=-1)
        {
            // Act-specific neutrals: one gameplay definition, one existing picture per theme of the act.
            if(NeutralContent.IsNeutral(id))
            {
                var variant=NeutralContent.VariantArtId(id,run?.CurrentTheme);
                return variant!=id&&Resources.Load<Texture2D>(GildedArtCatalog.EnemyResource(variant))!=null?variant:id;
            }
            if(combat!=null&&screen==ScreenMode.Combat&&id==HollowwoodContent.Parasite)
            {
                var pm=index>=0?combat.MindAt(index):null;
                return pm!=null&&pm.state=="EXPOSED"?id+"_phase2":id;
            }
            if(id!=AshenWildsContent.Alpha&&id!=DrownedQuarterContent.Magistrate&&id!=CrimsonFoundryContent.Saint&&id!=HollowwoodContent.Heartroot&&id!=ShatteredObservatoryContent.Curator&&id!=GildedRuinsContent.Procession&&id!=BlackCathedralContent.Bishop&&id!=FracturedRealmContent.Unmade&&id!=GildedThroneContent.Sovereign||combat==null||screen!=ScreenMode.Combat)return id;
            var boss=combat.WildBossIndex;var phase=boss>=0?combat.MindAt(boss)?.phase??1:1;
            return phase>=3?id+"_phase3":phase==2?id+"_phase2":id;
        }
        private void RefreshEnemyArt(int index)
        {
            if(combat==null||index<0||index>=finalEnemyDefs.Length)return;
            var def=WorldContent.Enemies.FirstOrDefault(e=>e.id==combat.EnemyIdAt(index));
            finalEnemyDefs[index]=def;finalEnemyTextures[index]=def==null?null:LoadAuthoredArt(GildedArtCatalog.EnemyResource(EnemyArtId(def.id,index)));
        }

        // Combat hook receipts (CombatEventKind.Hook). Final animation work attaches here later.
        private void ScheduleWildHook(CombatEvent fact,float at)
        {
            var name=fact.label!=null&&fact.label.StartsWith("HOOK:")?fact.label.Substring(5):fact.label??"";
            string text=null;var color=new Color(1f,.72f,.36f);
            switch(name)
            {
                case "cinder_alpha_phase2":case "cinder_alpha_phase3":
                case "drowned_magistrate_phase2":case "drowned_magistrate_phase3":
                case "iron_saint_phase2":case "iron_saint_phase3":
                case "heartroot_phase2":case "heartroot_phase3":
                case "astral_curator_phase2":case "astral_curator_phase3":case "the_unmade_phase_1_to_2":case "the_unmade_phase_2_to_3":case "sovereign_phase_1_to_2":case "sovereign_phase_2_to_3":RefreshEnemyArt(fact.enemyIndex);return; // the boss phase cinematic owns the banner
                // ---- Crimson Foundry ----
                case "foundry_overheated":text="OVERHEATED";color=new Color(1f,.38f,.16f);break;
                case "foundry_vent":text="VENT";color=new Color(.85f,.85f,.8f);break;
                case "furnace_hound_redline_pounce":text="REDLINE POUNCE";color=new Color(1f,.5f,.2f);break;
                case "rivet_priest_stoke":text="STOKED";color=new Color(1f,.6f,.25f);break;
                case "assembly_master_summon":text="DRONE DEPLOYED";color=new Color(.9f,.8f,.55f);break;
                case "assembly_master_repair":text="REPAIRED";color=new Color(.6f,.9f,.6f);break;
                case "assembly_master_command":case "forgemaster_command":text="COMMAND";color=new Color(1f,.85f,.45f);break;
                case "molten_carrier_spill":text="MOLTEN SPILL";color=new Color(1f,.45f,.15f);break;
                case "redline_automaton_overdrive":text="OVERDRIVE";color=new Color(1f,.4f,.25f);break;
                case "forgemaster_overload":text="FORGE OVERLOAD";color=new Color(1f,.42f,.18f);break;
                case "forgemaster_reassemble":text="REASSEMBLED";color=new Color(.9f,.8f,.55f);break;
                case "smelter_layer_break":text="ARMOR MELTS";color=new Color(1f,.6f,.3f);break;
                case "smelter_exposed_core":text="CORE EXPOSED";color=new Color(1f,.4f,.2f);break;
                case "saint_servitor_destroyed":text="THE SERVITOR FALLS";color=new Color(.75f,.72f,.68f);break;
                case "iron_saint_redline_judgment":text="REDLINE JUDGMENT";color=new Color(1f,.32f,.15f);break;
                // ---- Hollowwood ----
                case "growth_three":text="FULL GROWTH";color=new Color(.6f,.95f,.45f);break;
                case "sproutling_bloom_burst":text="BLOOM BURST";color=new Color(.85f,.55f,.95f);break;
                case "hollow_stag_crown_bloom":text="CROWN BLOOM";color=new Color(.85f,.55f,.95f);break;
                case "sporekeeper_acceleration":text="GROWTH ACCELERATED";color=new Color(.6f,.95f,.45f);break;
                case "root_snare_tighten":text="ROOTS TIGHTEN";color=new Color(.8f,.65f,.45f);break;
                case "brood_pod_hatch":text="HATCH";color=new Color(.95f,.9f,.4f);break;
                case "brood_pod_command":case "garden_mother_command":text="COMMAND";color=new Color(1f,.85f,.45f);break;
                case "sporeling_spawn":text="SPORELING HATCHES";color=new Color(.7f,.95f,.5f);break;
                case "huskbud_spawn":case "garden_mother_huskbud_replacement":text="HUSKBUD TAKES ITS PLACE";color=new Color(.85f,.8f,.6f);break;
                case "sporeling_death":text="WITHERS";color=new Color(.7f,.7f,.66f);break;
                case "bloomfang_full_bloom":text="FULL BLOOM";color=new Color(.9f,.5f,.95f);break;
                case "hollow_parasite_host_break":RefreshEnemyArt(fact.enemyIndex);text="THE HOST BREAKS · EXPOSED";color=new Color(.9f,.5f,.4f);break;
                case "garden_mother_cultivate":text="CULTIVATE";color=new Color(.6f,.95f,.45f);break;
                case "garden_mother_grand_bloom":text="GRAND BLOOM";color=new Color(.9f,.5f,.95f);break;
                case "walking_grove_stage2":text="THE BRANCHES WAKE";color=new Color(.65f,.9f,.45f);break;
                case "walking_grove_stage3":text="THE CROWN WAKES";color=new Color(.8f,.95f,.5f);break;
                case "heartroot_heart_bloom":text="HEART BLOOM";color=new Color(.95f,.5f,.8f);break;
                case "heartroot_spreading_bloom":text="SPREADING BLOOM";color=new Color(.95f,.5f,.8f);break;
                case "heartroot_final_bloom":text="FINAL BLOOM";color=new Color(1f,.45f,.7f);break;
                // ---- Black Cathedral ----
                case "final_bishop_phase_1_to_2":case "final_bishop_phase_2_to_3":RefreshEnemyArt(fact.enemyIndex);return; // the boss phase cinematic owns the banner
                case "bishop_minion_removed":text="THE VOICES FALL SILENT";color=new Color(.8f,.75f,.85f);break;
                case "voice_death":text="A VOICE FALLS SILENT";color=new Color(.78f,.74f,.84f);break;
                case "chapel_effigy_death":text="THE EFFIGY CRACKS";color=new Color(.78f,.76f,.72f);break;
                case "effigy_consecrate":text="EFFIGY CONSECRATED";color=new Color(.95f,.88f,.6f);break;
                case "cathedral_command":case "choir_eternal_conduct":text=name=="choir_eternal_conduct"?"CONDUCT":"COMMAND";color=new Color(1f,.85f,.45f);break;
                case "censer_smoke":text="SACRED SMOKE";color=new Color(.85f,.82f,.9f);break;
                case "execution_strike":text="EXECUTION";color=new Color(1f,.35f,.3f);break;
                case "bell_sentence":text="THE BELL PASSES SENTENCE";color=new Color(1f,.4f,.3f);break;
                case "final_judgment":text="LAST JUDGMENT";color=new Color(1f,.55f,.3f);break;
                case "cathedral_collapse":text="CATHEDRAL COLLAPSE";color=new Color(1f,.4f,.25f);break;
                // ---- Fractured Realm ----
                case "echo_created":text="ECHO";color=new Color(.78f,.6f,1f);break;
                case "echo_resolved":text="ECHO RESOLVES";color=new Color(.85f,.7f,1f);break;
                case "action_repeated":text="REPEATED";color=new Color(.7f,.85f,1f);break;
                case "splitling_split":text="SPLIT";color=new Color(.8f,.6f,1f);break;
                case "split_echo_spawn":text="SPLIT ECHO";color=new Color(.8f,.65f,1f);break;
                case "split_echo_death":case "fragment_death":text="SHATTERED";color=new Color(.7f,.7f,.85f);break;
                case "fragment_spawn":text="FRAGMENT BREAKS FREE";color=new Color(.8f,.6f,1f);break;
                case "mirror_buff_copied":text="BUFF COPIED";color=new Color(.7f,.9f,1f);break;
                case "rift_binder_duplicate":text="DUPLICATED";color=new Color(.78f,.6f,1f);break;
                case "oracle_pair_reveal":case "unmade_pair_reveal":text="TWO FUTURES";color=new Color(.85f,.55f,1f);break;
                case "fracture_command":text="FRACTURED COMMAND";color=new Color(1f,.85f,.45f);break;
                case "fractured_memory_recorded":text="MEMORY RECORDED";color=new Color(.75f,.7f,1f);break;
                case "loopkeeper_replay_start":text="LOOP REPLAY ACTIVE";color=new Color(.7f,.85f,1f);break;
                case "loopkeeper_replay_finish":text="LOOP ENDS";color=new Color(.75f,.75f,.9f);break;
                // ---- Gilded Ruins ----
                case "last_procession_phase_1_to_2":case "last_procession_phase_2_to_3":RefreshEnemyArt(fact.enemyIndex);return; // the boss phase cinematic owns the banner
                case "seize_gold":text="GOLD SEIZED";color=new Color(1f,.82f,.3f);break;
                case "gold_returned":text="GOLD RETURNED";color=new Color(.95f,.9f,.5f);break;
                case "chorister_support":text="THE CHORUS RISES";color=new Color(1f,.85f,.5f);break;
                case "bell_toll_1":text="FIRST TOLL";color=new Color(.95f,.85f,.55f);break;
                case "bell_toll_2":text="SECOND TOLL";color=new Color(1f,.78f,.4f);break;
                case "bell_toll_3":text="THIRD TOLL";color=new Color(1f,.6f,.3f);break;
                case "servitor_awaken":case "servitor_spawn":text="SERVITOR AWAKENS";color=new Color(.95f,.85f,.55f);break;
                case "servitor_death":case "coinbound_guard_death":text="CRUMBLES";color=new Color(.72f,.7f,.64f);break;
                case "coinbound_guard_spawn":text="GUARD TAKES ITS PLACE";color=new Color(1f,.85f,.45f);break;
                case "ruins_command":text="COMMAND";color=new Color(1f,.85f,.45f);break;
                case "collector_repossess":text="REPOSSESSED";color=new Color(1f,.75f,.35f);break;
                case "collector_foreclosure":text="FORECLOSURE";color=new Color(1f,.45f,.3f);break;
                case "auction_lot_announced":text="LOT ANNOUNCED";color=new Color(1f,.85f,.45f);break;
                case "auction_lot_resolved":text="SOLD";color=new Color(1f,.7f,.3f);break;
                case "reserve_gain":text="RESERVE +1";color=new Color(.95f,.8f,.4f);break;
                case "asset_release":text="ASSET RELEASE";color=new Color(1f,.55f,.25f);break;
                case "emergency_reserve":text="EMERGENCY RESERVE";color=new Color(.7f,.95f,.6f);break;
                case "bonus_gold_consumed":text="BONUS GOLD SPENT";color=new Color(1f,.8f,.35f);break;
                case "end_of_the_procession":text="THE END OF THE PROCESSION";color=new Color(1f,.4f,.25f);break;
                // ---- Shattered Observatory ----
                case "future_intent_generated":text="FUTURE FORETOLD";color=new Color(.72f,.62f,1f);break;
                case "future_to_current":case "chronoglyph_future_to_current":text="THE FUTURE ARRIVES";color=new Color(.72f,.62f,1f);break;
                case "chronoglyph_future_shift":case "blind_seer_queue_shift":text="THE QUEUE SHIFTS";color=new Color(.62f,.8f,1f);break;
                case "scribe_chart_stars":text="CHARTS THE STARS";color=new Color(.72f,.62f,1f);break;
                case "scribe_falling_star":text="FALLING STAR";color=new Color(1f,.85f,.5f);break;
                case "sentinel_plate_launch":text="PLATE LAUNCHED";color=new Color(.9f,.8f,.5f);break;
                case "sentinel_plate_reassemble":text="ORBIT REASSEMBLES";color=new Color(.62f,.85f,1f);break;
                case "weaver_form_constellation":text="CONSTELLATION FORMS";color=new Color(.62f,.8f,1f);break;
                case "star_fragment_spawn":text="STAR FRAGMENT";color=new Color(.8f,.85f,1f);break;
                case "star_fragment_death":case "orrery_fragment_death":text="SHATTERS";color=new Color(.75f,.75f,.8f);break;
                case "weaver_command":case "orrery_keeper_command":text="COMMAND";color=new Color(1f,.85f,.45f);break;
                case "weaver_realign":text="REALIGNED";color=new Color(.6f,.9f,.6f);break;
                case "gravity_monk_collapse_point":text="COLLAPSE POINT";color=new Color(.8f,.6f,1f);break;
                case "chronoglyph_future_collapse":text="FUTURE COLLAPSE";color=new Color(.8f,.6f,1f);break;
                case "astrologer_pair_reveal":case "astral_curator_twin_fate_reveal":text="TWO FATES";color=new Color(.72f,.62f,1f);break;
                case "astrologer_pair_chosen":case "astral_curator_twin_fate_chosen":text="FATE CHOSEN";color=new Color(1f,.82f,.5f);break;
                case "orrery_celestial_rotation":text="CELESTIAL ROTATION";color=new Color(1f,.85f,.5f);break;
                case "orrery_grand_alignment":case "curator_grand_alignment":text="GRAND ALIGNMENT";color=new Color(1f,.8f,.4f);break;
                case "blind_seer_predicted_ruin":case "curator_predicted_collapse":text="PREDICTED RUIN";color=new Color(.85f,.55f,1f);break;
                case "curator_collapse_event":text="COLLAPSE EVENT";color=new Color(1f,.5f,.4f);break;
                case "curator_archive_future":text="ARCHIVES THE FUTURE";color=new Color(.72f,.62f,1f);break;
                case "fallen_comet_momentum":text="MOMENTUM";color=new Color(.62f,.85f,1f);break;
                case "fallen_comet_full_momentum":text="FULL MOMENTUM";color=new Color(1f,.7f,.4f);break;
                case "fallen_comet_impact":text="IMPACT";color=new Color(1f,.5f,.3f);break;
                case "fallen_comet_cool_orbit":text="COOLS";color=new Color(.7f,.85f,1f);break;
                case "astral_curator_constellation_transformation":text="THE CONSTELLATION FORMS";color=new Color(.8f,.75f,1f);break;
                // ---- Drowned Quarter ----
                case "drowned_lurker_submerge":text="SUBMERGES · SURFACE STRIKE NEXT";color=new Color(.45f,.8f,.85f);break;
                case "drowned_lurker_surface_strike":text="SURFACES";color=new Color(.55f,.9f,.95f);break;
                case "bell_diver_toll":case "bellkeeper_toll":text="TOLL";color=new Color(.9f,.85f,.6f);break;
                case "tidecaller_summon":text="REACH FROM BELOW";color=new Color(.45f,.85f,.82f);break;
                case "tidecaller_command":case "bellkeeper_command":case "drowned_magistrate_order":text="COMMAND";color=new Color(1f,.85f,.45f);break;
                case "drowned_magistrate_summon":text="BAILIFF CALLED";color=new Color(.6f,.85f,1f);break;
                case "drowned_hand_death":text="SINKS";color=new Color(.65f,.75f,.78f);break;
                case "barnacle_hulk_shell_break":text="SHELL BREAKS · EXPOSED";color=new Color(1f,.6f,.35f);break;
                case "marionette_string_loss":text="A STRING SNAPS";color=new Color(.85f,.82f,.9f);break;
                case "ferryman_raise_anchor":text="ANCHOR RAISED";color=new Color(1f,.7f,.4f);break;
                case "ferryman_anchor_drop":text="ANCHOR DROP";color=new Color(1f,.5f,.3f);break;
                case "sunken_engine_pressure":text="PRESSURE RISES";color=new Color(.55f,.9f,1f);break;
                case "sunken_engine_burst_valve":text="BURST VALVE";color=new Color(1f,.55f,.3f);break;
                case "sunken_engine_vent":text="VENT";color=new Color(.7f,.85f,.9f);break;
                case "bailiff_echo_removed":text="THE BAILIFF SINKS";color=new Color(.65f,.75f,.78f);break;
                case "drowned_magistrate_final_sentence":text="FINAL SENTENCE";color=new Color(1f,.42f,.3f);break;
                case "cinder_alpha_minion_flees":text="FLEES";color=new Color(.8f,.8f,.78f);break;
                case "burned_hart_splintered":RefreshEnemyArt(fact.enemyIndex);text="THE CROWN SPLINTERS";color=new Color(1f,.55f,.3f);break;
                case "burned_hart_bare":text="THE CROWN BREAKS · BARE";color=new Color(1f,.4f,.25f);break;
                case "mourning_cry":text="MOURNING CRY";color=new Color(.75f,.82f,1f);break;
                case "rootcaller_summon":text="SUMMON";color=new Color(.6f,1f,.6f);break;
                case "rootcaller_command":case "packmother_command":case "cinder_alpha_command":text="COMMAND";color=new Color(1f,.85f,.45f);break;
                case "command_answer":text="ANSWERS THE COMMAND";color=new Color(1f,.85f,.45f);break;
                case "packmother_bereaved_fury":text="BEREAVED FURY";color=new Color(1f,.35f,.3f);break;
                case "minion_withers":case "sapling_death":text=name=="sapling_death"?"WITHERS":"WITHERS WITH ITS OWNER";color=new Color(.7f,.7f,.66f);break;
                case "support_flees":text="FLEES";color=new Color(.8f,.8f,.78f);break;
                case "thornjaw_bramble_coil":text="COILING · THORNBURST NEXT";color=new Color(.7f,1f,.5f);break;
                case "root_titan_uproot":text="UPROOTED";color=new Color(1f,.6f,.3f);break;
                case "root_titan_root":text="ROOTED";color=new Color(.6f,.9f,.55f);break;
                // ---- Gilded Throne ----
                case "royal_order_support":text="ROYAL ORDER";color=new Color(1f,.84f,.4f);break;
                case "crownshield_protection":text="THE COURT IS SHIELDED";color=new Color(.9f,.85f,.6f);break;
                case "commander_summon":text="ROYAL GUARD DEPLOYED";color=new Color(1f,.84f,.4f);break;
                case "royal_command":text="COMMAND";color=new Color(1f,.85f,.45f);break;
                case "royal_guard_death":text="THE GUARD FALLS";color=new Color(.8f,.75f,.65f);break;
                case "treasury_reserve_gain":text="RESERVE GAINED";color=new Color(1f,.84f,.4f);break;
                case "treasury_reserve_spend":text="RESERVE SPENT";color=new Color(1f,.7f,.3f);break;
                case "golden_stampede":text="GOLDEN STAMPEDE";color=new Color(1f,.75f,.25f);break;
                case "treasury_warden_royal_barrage":text="ROYAL BARRAGE";color=new Color(1f,.75f,.25f);break;
                case "treasury_warden_emergency":text="EMERGENCY TREASURY";color=new Color(.7f,1f,.6f);break;
                case "thronebreaker_charge":text="THRONEBREAKER CHARGE";color=new Color(1f,.4f,.25f);break;
                case "royal_general_formation":text="PERFECT FORMATION";color=new Color(1f,.84f,.4f);break;
                case "royal_general_reinforcement":text="REINFORCEMENTS";color=new Color(1f,.84f,.4f);break;
                case "end_of_the_crown":text="END OF THE CROWN";color=new Color(1f,.35f,.2f);break;
                case "royal_minion_withdrawn":text="WITHDRAWS";color=new Color(.8f,.78f,.7f);break;
                // ---- Act-specific neutrals ----
                case "unbound_blade_gather_nerve":text="GATHERS NERVE";color=new Color(1f,.8f,.5f);break;
                case "stray_idol_gather_fortune":text="GATHERS FORTUNE";color=new Color(1f,.84f,.4f);break;
                case "stray_idol_unstable_release":text="UNSTABLE RELEASE";color=new Color(1f,.5f,.3f);break;
                case "shifting_husk_shell_break":text="SHELL BREAKS";color=new Color(1f,.6f,.3f);break;
                case "deepcrawler_burrow":text="BURROWS · ERUPTION NEXT";color=new Color(.8f,.7f,.5f);break;
                case "deepcrawler_eruption":text="ERUPTION";color=new Color(1f,.45f,.25f);break;
                case "nameless_seer_readied_fate":text="FATE READIED · SENTENCE NEXT";color=new Color(.85f,.7f,1f);break;
                case "worldbreaker_gather":text="GATHERS THE WORLD · 1/2";color=new Color(1f,.7f,.35f);break;
                case "worldbreaker_overload":text="OVERLOAD · WORLD BREAK NEXT";color=new Color(1f,.5f,.25f);break;
                case "worldbreaker_world_break":text="WORLD BREAK";color=new Color(1f,.3f,.2f);break;
                case "worldbreaker_aftershock":text="AFTERSHOCK";color=new Color(.9f,.7f,.5f);break;
                default:
                    if(name.StartsWith("wayfarer_stance:")||name.StartsWith("pale_chimera_stance:")){text=name.Substring(name.IndexOf(':')+1)+" STANCE";color=new Color(.95f,.8f,.5f);break;}
                    if(name.StartsWith("crooked_oracle_response:")){var r=name.Substring(24);text=r=="nt_full_measure"?"FULL MEASURE":r=="nt_quiet_omen"?"QUIET OMEN":"LEFT UNSPENT";color=new Color(.85f,.7f,1f);break;}
                    if(name.StartsWith("duelist_response:")){var r=name.Substring(17);text=r=="gt_counterstance"?"ROYAL COUNTERSTANCE":r=="gt_piercing"?"PIERCING ADVANCE":"PERFECT MEASURE";color=new Color(1f,.84f,.4f);break;}
                    if(name.StartsWith("thronebreaker_siege:")){text="SIEGE "+name.Substring(20);color=new Color(1f,.6f,.3f);break;}
                    if(name.StartsWith("crown_duelmaster_stance:")){text=name.Substring(24);color=new Color(1f,.84f,.4f);break;}
                    if(name.StartsWith("phase_beast_form:")){text=name.Substring(17).ToUpperInvariant()+" FORM";color=new Color(.75f,.65f,1f);break;}
                    if(name.StartsWith("rift_colossus_stage:")){text="STAGE "+name.Substring(20);color=new Color(1f,.7f,.4f);break;}
                    if(name.StartsWith("split_sovereign_threshold:")){text="THE SOVEREIGN SPLITS";color=new Color(.85f,.6f,1f);break;}
                    if(name.StartsWith("herald_sentence:")){var n=name.Substring(16);text=n=="1"?"SENTENCE 1":"SENTENCE "+n;color=new Color(1f,.5f,.35f);break;}
                    if(name.StartsWith("bell_toll_count:")){text="TOLL "+name.Substring(16)+"/4";color=new Color(.95f,.85f,.6f);break;}
                    if(name.StartsWith("living_icon_state:")){text=name.Substring(18);color=text=="MERCY"?new Color(.7f,.95f,.7f):text=="WRATH"?new Color(1f,.4f,.3f):new Color(1f,.85f,.5f);break;}
                    if(name.StartsWith("confessor_judgment:")){var mv=name.Substring(19);text=mv=="bc_accuse_cowardice"?"ACCUSES COWARDICE":mv=="bc_punish_violence"?"PUNISHES VIOLENCE":"MEASURED PENANCE";color=new Color(.95f,.85f,.55f);break;}
                    if(name.StartsWith("oathbreaker_judgment:")){var k=name.Substring(21);text=k=="ATTACKS"?"REBUKES THE BLADE":k=="SKILLS"?"BREAKS THE RITUAL":k=="POWERS"?"DENIES ASCENSION":"BROKEN VOW";color=new Color(.9f,.7f,.95f);break;}
                    if(name.StartsWith("high_confessor_judgment:")||name.StartsWith("bishop_judgment:")){var k=name.Substring(name.IndexOf(':')+1);text=k=="BLOODTHIRST"||k=="VIOLENCE"?"JUDGES VIOLENCE":k=="FORTRESS"?"JUDGES THE FORTRESS":k=="RESTRAINT"?"JUDGES RESTRAINT":k=="EXCESS"?"JUDGES EXCESS":k=="RITUAL"?"JUDGES THE RITUAL":"JUDGES BALANCE";color=new Color(1f,.82f,.45f);break;}
                    if(name.StartsWith("elder_husk_state:")){text=name.Substring(17);color=text=="BLOOMING"?new Color(.9f,.55f,.95f):text=="WITHERED"?new Color(.75f,.7f,.55f):new Color(.6f,.85f,.45f);break;}
                    if(name.StartsWith("pale_gardener_seed_planted:")){text="PLANTED SEED: "+SeedLabel(name.Substring(27));color=new Color(.85f,.9f,.75f);break;}
                    if(name.StartsWith("pale_gardener_seed_resolved:")){text=SeedLabel(name.Substring(28))+" BLOOMS";color=new Color(.95f,.6f,.5f);break;}
                    if(name.StartsWith("prototype_zero_mode:")){text=name.Substring(20)+" MODE";color=new Color(1f,.6f,.4f);break;}
                    if(name.StartsWith("crucible_knight_state:")){text=name.Substring(22);color=text=="COLD"?new Color(.7f,.8f,.9f):new Color(1f,.5f,.25f);break;}
                    if(name.StartsWith("lenskeeper_response:")){var band=name.Substring(20);text=band=="UNDER"?"UNDEREXPOSED":band=="OVER"?"OVEREXPOSED":"BALANCED";color=new Color(.9f,.85f,.6f);break;}
                    if(name.StartsWith("duelist_response:")){var band=name.Substring(17);text=band=="ATTACKS"?"PUNISHES ATTACKS":band=="SKILLS"?"PRESSES THE ADVANTAGE":"MEASURES YOU";color=new Color(.95f,.8f,.5f);break;}
                    if(name.StartsWith("hollow_maw_state:")){text=name.Substring(17);color=text=="BURNING"?new Color(1f,.42f,.2f):text=="FED"?new Color(.6f,.9f,.6f):new Color(1f,.85f,.6f);}
                    break;
            }
            if(text!=null)wildCallouts.Add(new WildCallout{index=fact.enemyIndex,text=text,at=at,color=color});
        }
        private static string SeedLabel(string seed)=>seed switch{"THORN"=>"THORN SEED","WARD"=>"WARD SEED","ROT"=>"ROT SEED","BLOOM"=>"BLOOM SEED",_=>seed};
        private void DrawWildCallouts()
        {
            if(combat==null||wildCallouts.Count==0)return;var now=Time.unscaledTime;
            wildCallouts.RemoveAll(c=>now>c.at+1.6f);
            foreach(var c in wildCallouts)
            {
                var age=now-c.at;if(age<0||c.index<0||c.index>=combat.EnemyCount)continue;
                var body=GroupCombat?GroupPortrait(c.index):EnemyPortraitRect;
                var alpha=Mathf.Clamp01(age/.15f)*Mathf.Clamp01((1.6f-age)/.4f);var rise=profile.reduceMotion?0:age*14;
                var r=new Rect(body.center.x-150,body.y+body.height*.35f-rise,300,30);
                ShadowLabel(r,c.text,new GUIStyle(titleStyle){font=headingFont?headingFont:labelFont,fontSize=GroupCombat?17:21,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(c.color.r,c.color.g,c.color.b,alpha)}});
            }
        }

        // Minion badge: drawn at the top-left of a Minion's body.
        private void DrawMinionBadge(int index,Rect portrait)
        {
            if(combat==null||!combat.IsMinionAt(index))return;
            var art=MetaArt("MinionIcon");var size=Mathf.Clamp(portrait.width*.32f,28,40);
            var r=new Rect(portrait.x-4,portrait.y+2,size,size);
            Fill(new Rect(r.x+3,r.y+3,r.width-6,r.height-6),new Color(.04f,.03f,.02f,.72f));Outline(new Rect(r.x+3,r.y+3,r.width-6,r.height-6),new Color(.95f,.75f,.36f,.85f),1);
            if(art)GUI.DrawTexture(new Rect(r.x+4,r.y+4,r.width-8,r.height-8),art,ScaleMode.ScaleToFit,true);
            else GUI.Label(r,"M",new GUIStyle(titleStyle){fontSize=16,alignment=TextAnchor.MiddleCenter});
            if(CombatInspectionAllowed&&r.Contains(combatPointer))SetCombatEffectTooltip("MINION","Owned creature. Dies when its Owner dies. Gives no reward of its own.",r.center);
        }
        // Summon and Command intents use their own artwork instead of the intent atlas.
        private bool DrawWildIntentIcon(EnemyIntentAction action,Rect icon)
        {
            if(!string.IsNullOrEmpty(action.iconKey)&&DrawEnemyIconFit(icon,action.iconKey,IntentIconProminent(action.iconKey)))return true;
            if(action.Icon<EnemyIntentAction.IconSummon)return false;
            if(action.Icon==EnemyIntentAction.IconLoad)
            {
                GUI.Label(icon,"LOAD",new GUIStyle(titleStyle){font=labelFont?labelFont:bodyFont,fontSize=12,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(1f,.6f,.25f)}});
                return true;
            }
            if(action.Icon==EnemyIntentAction.IconPlate||action.Icon==EnemyIntentAction.IconMomentum)
            {
                GUI.Label(icon,action.Icon==EnemyIntentAction.IconPlate?"PLATE":"MOMENTUM",new GUIStyle(titleStyle){font=labelFont?labelFont:bodyFont,fontSize=action.Icon==EnemyIntentAction.IconPlate?11:9,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(.72f,.78f,1f)}});
                return true;
            }
            if(action.Icon==EnemyIntentAction.IconGrowth)
            {
                GUI.Label(icon,action.type==EnemyActionType.PlantSeed?"SEED":"GROWTH",new GUIStyle(titleStyle){font=labelFont?labelFont:bodyFont,fontSize=11,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(.6f,.95f,.45f)}});
                return true;
            }
            if(action.Icon==EnemyIntentAction.IconReserve||action.Icon==EnemyIntentAction.IconFortify||action.Icon==EnemyIntentAction.IconBonus)
            {
                var word=action.Icon==EnemyIntentAction.IconReserve?"RESERVE":action.Icon==EnemyIntentAction.IconFortify?"FORTIFY":"BONUS";
                GUI.Label(icon,word,new GUIStyle(titleStyle){font=labelFont?labelFont:bodyFont,fontSize=word=="RESERVE"?9:10,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(1f,.84f,.4f)}});
                return true;
            }
            var art=MetaArt(action.Icon==EnemyIntentAction.IconSummon?"SummonIcon":action.Icon==EnemyIntentAction.IconHeat?"HeatIcon":"MinionIcon");
            if(art)GUI.DrawTexture(new Rect(icon.x+icon.width*.1f,icon.y+icon.height*.08f,icon.width*.8f,icon.height*.84f),art,ScaleMode.ScaleToFit,true);
            else GUI.Label(icon,action.Icon==EnemyIntentAction.IconSummon?"+":"⟳",new GUIStyle(titleStyle){fontSize=22,alignment=TextAnchor.MiddleCenter});
            return true;
        }
        // Shown only while an enemy holds some of the player's Gold.
        private void DrawSeizedGoldHud()
        {
            if(combat==null||!combat.wildCombat||combat.TotalSeizedGold<=0)return;
            var hero=HeroPortraitRect;var text=$"GOLD {combat.playerGold} · {combat.TotalSeizedGold} SEIZED";
            var style=new GUIStyle(titleStyle){font=labelFont?labelFont:bodyFont,fontSize=13,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(1f,.9f,.5f)}};
            var width=style.CalcSize(new GUIContent(text)).x+18;var r=new Rect(hero.center.x-width*.5f,hero.y-26,width,24);
            Fill(r,new Color(.12f,.08f,.02f,.88f));Outline(r,new Color(1f,.82f,.3f,.9f),1);GUI.Label(r,text,style);
            if(CombatInspectionAllowed&&r.Contains(combatPointer))SetCombatEffectTooltip("SEIZED GOLD",$"You hold {combat.playerGold} Gold. Enemies are holding {combat.TotalSeizedGold} more. Defeat the holder to get it back; whatever is still held when you win is returned.",r.center);
        }
        // Gold an enemy is holding (Seized Gold). Returned when it dies and when the fight is won.
        private void DrawSeizedGoldPill(int index,Rect portrait)
        {
            var held=combat.SeizedGoldAt(index);if(held<=0)return;
            var text="SEIZED GOLD · "+held;var seizedTex=EnemyIconArt(EnemyIconRules.SeizedGold);
            var style=new GUIStyle(titleStyle){font=labelFont?labelFont:bodyFont,fontSize=12,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(1f,.9f,.5f)}};
            var iconSpace=seizedTex?24f:0f;var width=style.CalcSize(new GUIContent(text)).x+16+iconSpace;
            var r=new Rect(portrait.xMax-width+6,portrait.y+34,width,22);
            Fill(r,new Color(.12f,.08f,.02f,.88f));Outline(r,new Color(1f,.82f,.3f,.9f),1);
            if(seizedTex)DrawEnemyIconFit(new Rect(r.x+3,r.y+1,20,20),EnemyIconRules.SeizedGold,false);
            GUI.Label(new Rect(r.x+iconSpace,r.y,r.width-iconSpace,r.height),text,style);
            var seizedHelp=$"This enemy is holding {held} of your Gold. Defeat it and the Gold comes back. It is never lost permanently and cannot take more than you have.";
            RegisterCombatHudTarget("enemy:"+index+":seized",2+index,r,"SEIZED GOLD",seizedHelp);
            if(CombatInspectionAllowed&&r.Contains(combatPointer))SetCombatEffectTooltip("SEIZED GOLD",seizedHelp,r.center);
        }
        // Enemy-specific counter (Heat, Load, Armor, Strings, Pressure or Prototype Zero's mode),
        // drawn at the top-right of the body instead of cluttering the buff row.
        private void DrawWildCounter(int index,Rect portrait)
        {
            if(combat==null)return;
            DrawSeizedGoldPill(index,portrait);
            if(!combat.WildCounter(index,out var label,out var value,out var max))return;
            var heat=label=="HEAT";var mode=label is "ASSAULT" or "DEFENSE" or "OVERDRIVE"||max<=0;
            var growth=label.EndsWith("GROWTH");var predict=label.StartsWith("LAST TURN")||label.StartsWith("NO PREVIOUS")||label.StartsWith("PHASE ")||label.StartsWith("CHARGE")||label.StartsWith("BURROWED")||label=="SURFACE"||label=="ARMORED"||label=="EXPOSED"||label.StartsWith("LAST TURN")||label=="SENTENCE PREPARED"||label.StartsWith("SIEGE")||label.StartsWith("STANCE")||label.StartsWith("ROYAL")||label.StartsWith("GUARDS")||label.StartsWith("RESERVE")||label=="ROYAL COUNTERSTANCE"||label=="PIERCING ADVANCE"||label=="PERFECT MEASURE"||label.StartsWith("PENDING")||label.StartsWith("LOOP")||label.StartsWith("FORM")||label.StartsWith("STAGE")||label.StartsWith("FRAGMENTS")||label.StartsWith("COPIE")||label.StartsWith("PREVIOUS")||label=="REPEAT NEXT"||label=="NO ECHO"||label.StartsWith("SPLIT")||label.StartsWith("JUDG")||label=="ORBIT PLATES"||label=="MOMENTUM";
            var text=mode?label:heat?$"{value}/{max}":$"{label} {value}/{max}";
            var style=new GUIStyle(titleStyle){font=labelFont?labelFont:bodyFont,fontSize=text.Length>24?11:13,alignment=TextAnchor.MiddleCenter};
            var counterIcon=heat?null:combat.CounterIcon(index);var counterTex=counterIcon!=null?EnemyIconArt(counterIcon):null;
            var width=Mathf.Max(56,style.CalcSize(new GUIContent(text)).x+(heat?34:counterTex?34:16));
            var r=new Rect(portrait.xMax-width+6,portrait.y+4,width,26);
            var full=!mode&&value>=max&&(label is "HEAT" or "LOAD" or "PRESSURE" or "MOMENTUM"||growth);
            var accent=predict?new Color(.72f,.66f,1f):growth||max<=0?new Color(.62f,.9f,.42f):heat?new Color(1f,.45f,.16f):label=="LOAD"?new Color(1f,.62f,.24f):label=="ARMOR"?new Color(.78f,.74f,.68f):label.StartsWith("BONUS GOLD")||label=="RESERVE"||label.StartsWith("STATE")?new Color(1f,.84f,.4f):label.StartsWith("SENTENCE")||label=="TOLL"?new Color(1f,.45f,.32f):mode?new Color(1f,.55f,.35f):new Color(.6f,.85f,.9f);
            var pulse=full&&!profile.reduceMotion?.55f+.45f*Mathf.Sin(Time.unscaledTime*6f):1f;
            Fill(r,new Color(.04f,.025f,.02f,.8f));Outline(r,new Color(accent.r,accent.g,accent.b,full?pulse:.75f),full?2:1);
            var textRect=r;
            if(heat)
            {
                var art=MetaArt("HeatIcon");var ic=new Rect(r.x+4,r.y+2,22,22);
                if(art)GUI.DrawTexture(ic,art,ScaleMode.ScaleToFit,true);else GUI.Label(ic,"♨",style);
                textRect=new Rect(r.x+24,r.y,r.width-26,r.height);
            }
            if(counterTex){DrawEnemyIconFit(new Rect(r.x+3,r.y+1,26,24),counterIcon,false);textRect=new Rect(r.x+28,r.y,r.width-30,r.height);}
            style.normal.textColor=full?new Color(1f,.5f,.3f):new Color(1f,.92f,.82f);GUI.Label(textRect,text,style);
            {
                var title=heat?(value>=max?"HEAT · OVERHEATED":"HEAT"):max<=0?label:mode?"OPERATING MODE":label;
                var help=heat?$"Heat {value}/{max}. Certain actions increase Heat. High Heat may strengthen this enemy's actions. Some enemies Vent to reduce or reset Heat."
                    :combat.WildStateText(index);
                if(counterIcon!=null){var tip=EnemyIconRules.Tip(counterIcon);if(!string.IsNullOrEmpty(tip))help=tip+(string.IsNullOrEmpty(help)?"":"\n\n"+help);}
                RegisterCombatHudTarget("enemy:"+index+":counter",2+index,r,title,help);
                if(CombatInspectionAllowed&&r.Contains(combatPointer))SetCombatEffectTooltip(title,help,r.center);
            }
        }
        // Prediction forecast beside an Observatory enemy: a queued Future Intent, the Chronoglyph's Future, the
        // Blind Seer's Next / Following, or a pair of Possible Next Actions. Drawn in violet so it never reads as
        // the Current intent above the creature.
        private void DrawWildForecast(int index,Rect portrait)
        {
            if(combat==null||!combat.wildCombat||!combat.WildForecast(index,out var title,out var lines,out var tip)||lines.Count==0)return;
            var possible=title.StartsWith("POSSIBLE");var accent=new Color(.72f,.62f,1f);
            var small=new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=GroupCombat?10:11,alignment=TextAnchor.MiddleCenter,wordWrap=false,clipping=TextClipping.Overflow,normal={textColor=accent}};
            var line=new GUIStyle(small){fontSize=GroupCombat?11:12,wordWrap=false,clipping=TextClipping.Overflow,normal={textColor=new Color(.95f,.92f,1f)}};
            var rows=lines.Count+(possible?lines.Count-1:0);var h=16+rows*(possible?14f:17f)+6;
            var width=Mathf.Clamp(portrait.width+30,200,300);
            var widest=0f;foreach(var l in lines)widest=Mathf.Max(widest,line.CalcSize(new GUIContent(l)).x);
            width=Mathf.Clamp(Mathf.Max(width,widest+14),200,420);
            var r=new Rect(portrait.center.x-width*.5f,portrait.yMax-h-4,width,h);
            Fill(r,new Color(.06f,.04f,.12f,.86f));Outline(r,new Color(accent.r,accent.g,accent.b,.85f),1);
            var forecastIcon=combat.ForecastIcon(index);
            if(forecastIcon!=null&&EnemyIconArt(forecastIcon)!=null)
            {
                var headWidth=small.CalcSize(new GUIContent(title)).x;
                DrawEnemyIconFit(new Rect(r.center.x-headWidth*.5f-19,r.y+1,16,16),forecastIcon,false);
            }
            GUI.Label(new Rect(r.x,r.y+1,r.width,15),title,small);
            var y=r.y+16;
            for(var i=0;i<lines.Count;i++)
            {
                GUI.Label(new Rect(r.x+3,y,r.width-6,possible?14:17),lines[i],line);y+=possible?14:17;
                if(possible&&i<lines.Count-1){GUI.Label(new Rect(r.x,y,r.width,14),"— OR —",small);y+=14;}
            }
            var forecastTip=forecastIcon!=null&&EnemyIconRules.Tip(forecastIcon)!=null?EnemyIconRules.Tip(forecastIcon)+"\n\n"+tip:tip;
            RegisterCombatHudTarget("enemy:"+index+":forecast",2+index,r,title,forecastTip);
            if(CombatInspectionAllowed&&r.Contains(combatPointer))SetCombatEffectTooltip(title,forecastTip,r.center);
        }
        private string WildTooltipSuffix(int index)
        {
            if(combat==null||!combat.wildCombat)return "";var text=combat.WildStateText(index);
            return string.IsNullOrEmpty(text)?"":"\n\n"+text;
        }

        // Themed (Ashen Wilds, Drowned Quarter) motion reuses the existing animation archetypes until bespoke
        // animation sheets arrive.
        private static EnemyMotionProfile WildMotionFor(string id)=>id switch
        {
            AshenWildsContent.Cinderfang=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Bite,.75f,0,1.5f,76,new Color(1f,.48f,.16f)),
            AshenWildsContent.Grazer=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Chomp,1.15f,0,1f,60,new Color(.86f,.62f,.32f)),
            AshenWildsContent.Emberwing=>new(EnemyMotionKind.Spirit,EnemyStrikeStyle.Bolt,.6f,10,1.2f,40,new Color(1f,.62f,.26f)),
            AshenWildsContent.Thornjaw=>new(EnemyMotionKind.Crawler,EnemyStrikeStyle.Chomp,.95f,0,1.1f,58,new Color(.72f,.9f,.36f)),
            AshenWildsContent.Stalker=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Claw,.65f,0,1.6f,82,new Color(.82f,.78f,.7f)),
            AshenWildsContent.Rootcaller=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Bolt,.9f,4,1f,20,new Color(1f,.58f,.22f)),
            AshenWildsContent.Sapling=>new(EnemyMotionKind.Crawler,EnemyStrikeStyle.Fang,.5f,0,1.5f,44,new Color(.7f,.95f,.4f)),
            AshenWildsContent.Elk=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Claw,1.05f,0,.9f,64,new Color(.72f,.82f,1f)),
            AshenWildsContent.Maw=>new(EnemyMotionKind.Brute,EnemyStrikeStyle.Chomp,1.2f,0,.9f,48,new Color(1f,.42f,.14f)),
            AshenWildsContent.Packmother=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Claw,1.25f,0,1f,76,new Color(1f,.5f,.2f)),
            AshenWildsContent.FangPup=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Bite,.5f,0,1.8f,58,new Color(.9f,.8f,.7f)),
            AshenWildsContent.AshbackCub=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Slam,.7f,0,1.2f,52,new Color(1f,.62f,.3f)),
            AshenWildsContent.Hart=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Cleave,1.2f,0,.95f,70,new Color(1f,.66f,.26f)),
            AshenWildsContent.Titan=>new(EnemyMotionKind.Colossus,EnemyStrikeStyle.Crush,1.6f,0,.65f,36,new Color(1f,.55f,.2f)),
            AshenWildsContent.Alpha=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Claw,1.45f,0,.95f,84,new Color(1f,.45f,.12f)),
            AshenWildsContent.Whelp=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Bite,.55f,0,1.7f,62,new Color(1f,.5f,.2f)),
            AshenWildsContent.Runner=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Claw,.5f,0,1.9f,70,new Color(1f,.6f,.25f)),
            DrownedQuarterContent.Rustwalker=>new(EnemyMotionKind.Brute,EnemyStrikeStyle.Cleave,1.1f,0,.95f,52,new Color(.86f,.55f,.3f)),
            DrownedQuarterContent.Lurker=>new(EnemyMotionKind.Crawler,EnemyStrikeStyle.Chomp,.9f,0,1.2f,70,new Color(.4f,.85f,.85f)),
            DrownedQuarterContent.BellDiver=>new(EnemyMotionKind.Brute,EnemyStrikeStyle.Slam,1.05f,0,.95f,50,new Color(.45f,.9f,.9f)),
            DrownedQuarterContent.RustPriest=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Bolt,.85f,4,1f,20,new Color(.5f,.95f,.85f)),
            DrownedQuarterContent.CanalStalker=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Blade,.7f,0,1.5f,78,new Color(.6f,.85f,.8f)),
            DrownedQuarterContent.Tidecaller=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Bolt,.9f,4,1f,22,new Color(.4f,.9f,.85f)),
            DrownedQuarterContent.Hand=>new(EnemyMotionKind.Crawler,EnemyStrikeStyle.Grab,.5f,0,1.4f,50,new Color(.5f,.85f,.95f)),
            DrownedQuarterContent.Hulk=>new(EnemyMotionKind.Brute,EnemyStrikeStyle.Crush,1.35f,0,.85f,48,new Color(.7f,.85f,.8f)),
            DrownedQuarterContent.Marionette=>new(EnemyMotionKind.Spirit,EnemyStrikeStyle.Claw,.7f,6,1.3f,52,new Color(.6f,.85f,.9f)),
            DrownedQuarterContent.Ferryman=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Cleave,1.3f,0,.9f,64,new Color(.9f,.6f,.35f)),
            DrownedQuarterContent.Bellkeeper=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Slam,1.2f,2,.9f,40,new Color(.45f,.95f,.9f)),
            DrownedQuarterContent.TollThrall=>new(EnemyMotionKind.Brute,EnemyStrikeStyle.Slam,.65f,0,1.3f,50,new Color(.8f,.75f,.55f)),
            DrownedQuarterContent.SinkerThrall=>new(EnemyMotionKind.Brute,EnemyStrikeStyle.Crush,.8f,0,1.1f,46,new Color(.7f,.7f,.65f)),
            DrownedQuarterContent.Engine=>new(EnemyMotionKind.Colossus,EnemyStrikeStyle.Crush,1.5f,0,.75f,40,new Color(.45f,.9f,1f)),
            DrownedQuarterContent.Magistrate=>new(EnemyMotionKind.Colossus,EnemyStrikeStyle.Crush,1.5f,0,.75f,44,new Color(.4f,.9f,.85f)),
            DrownedQuarterContent.Bailiff=>new(EnemyMotionKind.Spirit,EnemyStrikeStyle.Spectral,.7f,8,1.2f,40,new Color(.5f,.85f,1f)),
            CrimsonFoundryContent.Forgehand=>new(EnemyMotionKind.Brute,EnemyStrikeStyle.Slam,1.05f,0,1f,52,new Color(1f,.48f,.18f)),
            CrimsonFoundryContent.Hound=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Bite,.8f,0,1.5f,82,new Color(1f,.45f,.15f)),
            CrimsonFoundryContent.RivetPriest=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Bolt,.85f,4,1f,20,new Color(1f,.6f,.3f)),
            CrimsonFoundryContent.ChainWarden=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Grab,1.05f,0,1f,60,new Color(.95f,.4f,.25f)),
            CrimsonFoundryContent.AssemblyMaster=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Blade,.9f,2,1.05f,40,new Color(1f,.7f,.4f)),
            CrimsonFoundryContent.ScrapDrone=>new(EnemyMotionKind.Crawler,EnemyStrikeStyle.Claw,.5f,0,1.6f,56,new Color(1f,.6f,.3f)),
            CrimsonFoundryContent.Knight=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Cleave,1.15f,0,.95f,62,new Color(1f,.45f,.18f)),
            CrimsonFoundryContent.Carrier=>new(EnemyMotionKind.Brute,EnemyStrikeStyle.Crush,1.3f,0,.85f,46,new Color(1f,.55f,.15f)),
            CrimsonFoundryContent.Automaton=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Blade,.75f,0,1.5f,84,new Color(1f,.35f,.2f)),
            CrimsonFoundryContent.Forgemaster=>new(EnemyMotionKind.Brute,EnemyStrikeStyle.Slam,1.35f,0,.9f,58,new Color(1f,.5f,.2f)),
            CrimsonFoundryContent.HammerDrone=>new(EnemyMotionKind.Brute,EnemyStrikeStyle.Slam,.75f,0,1.2f,52,new Color(1f,.5f,.2f)),
            CrimsonFoundryContent.ShieldDrone=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Slam,.8f,0,1.1f,48,new Color(.95f,.75f,.5f)),
            CrimsonFoundryContent.Smelter=>new(EnemyMotionKind.Colossus,EnemyStrikeStyle.Crush,1.5f,0,.75f,40,new Color(1f,.45f,.12f)),
            CrimsonFoundryContent.Prototype=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Blade,1.1f,0,1.4f,86,new Color(1f,.35f,.25f)),
            CrimsonFoundryContent.Saint=>new(EnemyMotionKind.Colossus,EnemyStrikeStyle.Crush,1.6f,0,.75f,44,new Color(1f,.5f,.18f)),
            CrimsonFoundryContent.Servitor=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Bolt,.8f,4,1.1f,30,new Color(1f,.7f,.4f)),
            HollowwoodContent.Sproutling=>new(EnemyMotionKind.Crawler,EnemyStrikeStyle.Fang,.6f,0,1.4f,50,new Color(.7f,.95f,.4f)),
            HollowwoodContent.Stag=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Cleave,1.15f,0,.95f,66,new Color(.75f,.95f,.7f)),
            HollowwoodContent.Sporekeeper=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Bolt,.85f,4,1f,20,new Color(.65f,.95f,.45f)),
            HollowwoodContent.RootSnare=>new(EnemyMotionKind.Crawler,EnemyStrikeStyle.Grab,1f,0,1.1f,56,new Color(.8f,.65f,.4f)),
            HollowwoodContent.BroodPod=>new(EnemyMotionKind.Brute,EnemyStrikeStyle.Slam,1.1f,0,.85f,40,new Color(.9f,.9f,.4f)),
            HollowwoodContent.Sporeling=>new(EnemyMotionKind.Crawler,EnemyStrikeStyle.Chomp,.5f,0,1.5f,46,new Color(.7f,.95f,.5f)),
            HollowwoodContent.Bloomfang=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Fang,.9f,0,1.3f,74,new Color(.9f,.5f,.95f)),
            HollowwoodContent.Parasite=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Claw,.85f,0,1.3f,64,new Color(.5f,.95f,.9f)),
            HollowwoodContent.ElderHusk=>new(EnemyMotionKind.Brute,EnemyStrikeStyle.Cleave,1.2f,0,.9f,56,new Color(.7f,.85f,.5f)),
            HollowwoodContent.GardenMother=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Grab,1.25f,2,1f,44,new Color(.75f,.55f,.9f)),
            HollowwoodContent.Thornbud=>new(EnemyMotionKind.Crawler,EnemyStrikeStyle.Claw,.6f,0,1.4f,50,new Color(.75f,.95f,.4f)),
            HollowwoodContent.Bloombud=>new(EnemyMotionKind.Spirit,EnemyStrikeStyle.Bolt,.5f,6,1.2f,36,new Color(.9f,.6f,.95f)),
            HollowwoodContent.Huskbud=>new(EnemyMotionKind.Crawler,EnemyStrikeStyle.Slam,.7f,0,1.2f,48,new Color(.85f,.8f,.55f)),
            HollowwoodContent.WalkingGrove=>new(EnemyMotionKind.Colossus,EnemyStrikeStyle.Crush,1.5f,0,.75f,40,new Color(.65f,.9f,.45f)),
            HollowwoodContent.PaleGardener=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Blade,1.1f,0,1.1f,64,new Color(.85f,.9f,.7f)),
            HollowwoodContent.Heartroot=>new(EnemyMotionKind.Colossus,EnemyStrikeStyle.Crush,1.6f,0,.75f,44,new Color(.95f,.5f,.8f)),
            ShatteredObservatoryContent.Scribe=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Bolt,.85f,4,1f,20,new Color(.72f,.62f,1f)),
            ShatteredObservatoryContent.Sentinel=>new(EnemyMotionKind.Spirit,EnemyStrikeStyle.Slam,1f,6,1.1f,40,new Color(.75f,.8f,1f)),
            ShatteredObservatoryContent.Attendant=>new(EnemyMotionKind.Spirit,EnemyStrikeStyle.Bolt,.7f,8,1.1f,36,new Color(.85f,.8f,1f)),
            ShatteredObservatoryContent.Chronoglyph=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Blade,.8f,3,1.2f,50,new Color(.7f,.7f,1f)),
            ShatteredObservatoryContent.Lenskeeper=>new(EnemyMotionKind.Brute,EnemyStrikeStyle.Bolt,1.1f,0,.95f,44,new Color(.6f,.8f,1f)),
            ShatteredObservatoryContent.Weaver=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Bolt,.9f,6,1f,22,new Color(.65f,.85f,1f)),
            ShatteredObservatoryContent.StarFragment=>new(EnemyMotionKind.Spirit,EnemyStrikeStyle.Bolt,.5f,8,1.3f,36,new Color(.8f,.85f,1f)),
            ShatteredObservatoryContent.Monk=>new(EnemyMotionKind.Spirit,EnemyStrikeStyle.Slam,1f,10,.9f,34,new Color(.75f,.65f,1f)),
            ShatteredObservatoryContent.Astrologer=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Claw,.9f,2,1f,24,new Color(.8f,.7f,1f)),
            ShatteredObservatoryContent.OrreryKeeper=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Slam,1.25f,0,.9f,52,new Color(1f,.85f,.5f)),
            ShatteredObservatoryContent.SunFragment=>new(EnemyMotionKind.Brute,EnemyStrikeStyle.Slam,.75f,0,1.15f,46,new Color(1f,.75f,.3f)),
            ShatteredObservatoryContent.MoonFragment=>new(EnemyMotionKind.Spirit,EnemyStrikeStyle.Claw,.6f,6,1.2f,42,new Color(.75f,.85f,1f)),
            ShatteredObservatoryContent.BlindSeer=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Bolt,1.1f,6,.95f,30,new Color(.8f,.75f,1f)),
            ShatteredObservatoryContent.Comet=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Claw,1.3f,0,1.1f,80,new Color(.55f,.8f,1f)),
            GildedRuinsContent.Scavenger=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Blade,.9f,0,1.15f,50,new Color(1f,.8f,.4f)),
            GildedRuinsContent.Bastion=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Slam,1.15f,0,.9f,54,new Color(1f,.8f,.4f)),
            GildedRuinsContent.Chorister=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Bolt,.85f,3,1f,20,new Color(1f,.88f,.55f)),
            GildedRuinsContent.Appraiser=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Claw,.9f,2,1f,24,new Color(1f,.82f,.4f)),
            GildedRuinsContent.Keeper=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Slam,1.1f,2,.95f,36,new Color(1f,.85f,.5f)),
            GildedRuinsContent.Servitor=>new(EnemyMotionKind.Crawler,EnemyStrikeStyle.Claw,.65f,0,1.35f,46,new Color(.9f,.8f,.5f)),
            GildedRuinsContent.Mimic=>new(EnemyMotionKind.Brute,EnemyStrikeStyle.Chomp,1.05f,0,1.2f,60,new Color(1f,.82f,.3f)),
            GildedRuinsContent.Herald=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Slam,1.1f,0,1f,56,new Color(1f,.8f,.45f)),
            GildedRuinsContent.Duelist=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Blade,1f,0,1.25f,56,new Color(1f,.7f,.35f)),
            GildedRuinsContent.Collector=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Blade,1.25f,0,.95f,52,new Color(1f,.82f,.35f)),
            GildedRuinsContent.Guard=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Slam,.8f,0,1.1f,44,new Color(.95f,.8f,.45f)),
            GildedRuinsContent.Auctioneer=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Bolt,1.1f,2,.95f,32,new Color(1f,.85f,.45f)),
            GildedRuinsContent.Treasury=>new(EnemyMotionKind.Colossus,EnemyStrikeStyle.Crush,1.4f,0,.8f,40,new Color(1f,.8f,.3f)),
            GildedRuinsContent.Procession=>new(EnemyMotionKind.Colossus,EnemyStrikeStyle.Blade,1.55f,3,.8f,42,new Color(1f,.82f,.4f)),
            BlackCathedralContent.Guard=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Slam,1.1f,0,.9f,50,new Color(.82f,.78f,.9f)),
            BlackCathedralContent.Warden=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Bolt,.85f,3,1f,18,new Color(.9f,.85f,1f)),
            BlackCathedralContent.Confessor=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Claw,.9f,2,1f,30,new Color(.9f,.8f,.6f)),
            BlackCathedralContent.Censer=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Slam,.95f,0,.95f,48,new Color(.85f,.8f,.9f)),
            BlackCathedralContent.Saint=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Slam,1.1f,2,.9f,36,new Color(1f,.9f,.6f)),
            BlackCathedralContent.Effigy=>new(EnemyMotionKind.Brute,EnemyStrikeStyle.Slam,.8f,0,.9f,40,new Color(.8f,.78f,.74f)),
            BlackCathedralContent.Herald=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Blade,1.15f,0,.85f,56,new Color(1f,.5f,.35f)),
            BlackCathedralContent.Priest=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Bolt,1f,2,1f,30,new Color(.9f,.7f,.95f)),
            BlackCathedralContent.Icon=>new(EnemyMotionKind.Colossus,EnemyStrikeStyle.Slam,1.2f,3,.85f,36,new Color(1f,.85f,.5f)),
            BlackCathedralContent.HighConfessor=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Bolt,1.4f,2,.85f,36,new Color(1f,.82f,.45f)),
            BlackCathedralContent.ChoirEternal=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Bolt,1.45f,3,.8f,30,new Color(.95f,.85f,1f)),
            BlackCathedralContent.VoiceBlade=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Blade,.7f,3,1.2f,36,new Color(.9f,.6f,.7f)),
            BlackCathedralContent.VoiceMercy=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Bolt,.7f,3,1.1f,18,new Color(.7f,.95f,.8f)),
            BlackCathedralContent.VoiceVigil=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Slam,.75f,3,1f,24,new Color(.8f,.85f,1f)),
            BlackCathedralContent.Bell=>new(EnemyMotionKind.Colossus,EnemyStrikeStyle.Crush,1.5f,2,.75f,36,new Color(1f,.8f,.4f)),
            BlackCathedralContent.Bishop=>new(EnemyMotionKind.Colossus,EnemyStrikeStyle.Blade,1.6f,3,.8f,44,new Color(.95f,.8f,.55f)),
            ShatteredObservatoryContent.Curator=>new(EnemyMotionKind.Colossus,EnemyStrikeStyle.Blade,1.5f,4,.8f,40,new Color(.78f,.7f,1f)),
            FracturedRealmContent.Soldier=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Blade,1f,0,.95f,50,new Color(.8f,.65f,1f)),
            FracturedRealmContent.Splitling=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Claw,.9f,2,1.1f,36,new Color(.8f,.6f,1f)),
            FracturedRealmContent.SplitEcho=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Claw,.7f,3,1.2f,30,new Color(.85f,.7f,1f)),
            FracturedRealmContent.Husk=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Slam,1.05f,0,.9f,48,new Color(.7f,.9f,1f)),
            FracturedRealmContent.Binder=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Bolt,.9f,3,1f,24,new Color(.8f,.65f,1f)),
            FracturedRealmContent.Shear=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Blade,.95f,2,1.1f,36,new Color(.7f,.85f,1f)),
            FracturedRealmContent.Beast=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Claw,1.05f,1,1f,44,new Color(.6f,.75f,1f)),
            FracturedRealmContent.Oracle=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Bolt,1f,2,1f,30,new Color(.85f,.6f,1f)),
            FracturedRealmContent.Colossus=>new(EnemyMotionKind.Colossus,EnemyStrikeStyle.Slam,1.3f,2,.85f,36,new Color(.8f,.7f,1f)),
            FracturedRealmContent.Duplicate=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Blade,1.35f,0,1f,50,new Color(.8f,.65f,1f)),
            FracturedRealmContent.Sovereign=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Blade,1.4f,2,.9f,40,new Color(.9f,.75f,1f)),
            FracturedRealmContent.BladeFragment=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Blade,.7f,3,1.2f,36,new Color(.85f,.6f,1f)),
            FracturedRealmContent.WardFragment=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Slam,.7f,0,1f,30,new Color(.7f,.9f,1f)),
            FracturedRealmContent.Loopkeeper=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Blade,1.4f,2,.9f,36,new Color(.7f,.85f,1f)),
            FracturedRealmContent.Unmade=>new(EnemyMotionKind.Colossus,EnemyStrikeStyle.Blade,1.6f,3,.8f,44,new Color(.85f,.7f,1f)),
            GildedThroneContent.Vanguard=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Blade,1.05f,0,.95f,50,new Color(1f,.84f,.4f)),
            GildedThroneContent.Crownshield=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Slam,1.05f,0,.9f,48,new Color(1f,.84f,.4f)),
            GildedThroneContent.Adjudicator=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Blade,.95f,2,1f,30,new Color(.9f,.35f,.3f)),
            GildedThroneContent.Strategist=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Bolt,.9f,3,1f,24,new Color(1f,.84f,.4f)),
            GildedThroneContent.Commander=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Blade,1.05f,0,.95f,50,new Color(1f,.84f,.4f)),
            GildedThroneContent.Guard=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Blade,.8f,0,1f,44,new Color(.95f,.85f,.55f)),
            GildedThroneContent.Beast=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Chomp,1.1f,1,1f,50,new Color(1f,.8f,.3f)),
            GildedThroneContent.Duelist=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Blade,1f,0,1.15f,56,new Color(1f,.84f,.4f)),
            GildedThroneContent.Thronebreaker=>new(EnemyMotionKind.Colossus,EnemyStrikeStyle.Crush,1.25f,2,.85f,40,new Color(1f,.7f,.3f)),
            GildedThroneContent.General=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Blade,1.35f,0,.9f,50,new Color(1f,.84f,.4f)),
            GildedThroneContent.Warden=>new(EnemyMotionKind.Colossus,EnemyStrikeStyle.Slam,1.4f,2,.85f,40,new Color(1f,.8f,.3f)),
            GildedThroneContent.Duelmaster=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Blade,1.35f,0,1.1f,56,new Color(1f,.84f,.4f)),
            GildedThroneContent.Blade=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Blade,.7f,3,1.2f,36,new Color(.9f,.4f,.35f)),
            GildedThroneContent.Shield=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Slam,.75f,0,.9f,40,new Color(.95f,.85f,.55f)),
            GildedThroneContent.Sovereign=>new(EnemyMotionKind.Colossus,EnemyStrikeStyle.Blade,1.6f,3,.8f,44,new Color(1f,.85f,.45f)),
            NeutralContent.UnboundBlade=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Blade,.95f,0,1.05f,50,new Color(.95f,.85f,.6f)),
            NeutralContent.Fateworn=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Blade,1f,0,.95f,48,new Color(.9f,.8f,.7f)),
            NeutralContent.StrayIdol=>new(EnemyMotionKind.Colossus,EnemyStrikeStyle.Slam,1f,2,.85f,36,new Color(1f,.84f,.45f)),
            NeutralContent.Wayfarer=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Blade,1.3f,0,1f,50,new Color(.95f,.85f,.6f)),
            NeutralContent.RaggedVanguard=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Blade,1.05f,0,.95f,50,new Color(.9f,.8f,.7f)),
            NeutralContent.ShiftingHusk=>new(EnemyMotionKind.Brute,EnemyStrikeStyle.Slam,1.05f,0,.9f,44,new Color(.85f,.8f,.7f)),
            NeutralContent.CrookedOracle=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Bolt,.95f,2,1f,30,new Color(.85f,.75f,1f)),
            NeutralContent.Deepcrawler=>new(EnemyMotionKind.Colossus,EnemyStrikeStyle.Crush,1.45f,2,.8f,40,new Color(.85f,.7f,.5f)),
            NeutralContent.IronWanderer=>new(EnemyMotionKind.Colossus,EnemyStrikeStyle.Slam,1.1f,1,.85f,44,new Color(.8f,.8f,.85f)),
            NeutralContent.PaleChimera=>new(EnemyMotionKind.Beast,EnemyStrikeStyle.Claw,1.1f,1,1.05f,50,new Color(.95f,.92f,.85f)),
            NeutralContent.NamelessSeer=>new(EnemyMotionKind.Caster,EnemyStrikeStyle.Bolt,1f,2,1f,30,new Color(.8f,.75f,1f)),
            NeutralContent.Worldbreaker=>new(EnemyMotionKind.Colossus,EnemyStrikeStyle.Crush,1.6f,3,.75f,44,new Color(1f,.55f,.3f)),
            _=>new(EnemyMotionKind.Knight,EnemyStrikeStyle.Blade,1f,0,1f,50,new Color(1f,.62f,.36f))
        };
        private static float WildBodyScale(EnemyDef enemy)=>enemy?.id switch
        {
            AshenWildsContent.Cinderfang=>.82f,AshenWildsContent.Grazer=>.98f,AshenWildsContent.Emberwing=>.95f,AshenWildsContent.Thornjaw=>.98f,
            AshenWildsContent.Stalker=>.8f,AshenWildsContent.Rootcaller=>1.02f,AshenWildsContent.Sapling=>.55f,AshenWildsContent.Elk=>1.05f,
            AshenWildsContent.Maw=>1.12f,AshenWildsContent.Packmother=>1.25f,AshenWildsContent.FangPup=>.58f,AshenWildsContent.AshbackCub=>.64f,
            AshenWildsContent.Hart=>1.25f,AshenWildsContent.Titan=>1.4f,AshenWildsContent.Alpha=>1.45f,AshenWildsContent.Whelp=>.62f,AshenWildsContent.Runner=>.58f,
            DrownedQuarterContent.Rustwalker=>.95f,DrownedQuarterContent.Lurker=>1.2f,DrownedQuarterContent.BellDiver=>1f,DrownedQuarterContent.RustPriest=>.98f,
            DrownedQuarterContent.CanalStalker=>.95f,DrownedQuarterContent.Tidecaller=>1.02f,DrownedQuarterContent.Hand=>.55f,DrownedQuarterContent.Hulk=>1.15f,
            DrownedQuarterContent.Marionette=>1f,DrownedQuarterContent.Ferryman=>1.25f,DrownedQuarterContent.Bellkeeper=>1.2f,DrownedQuarterContent.TollThrall=>.66f,
            DrownedQuarterContent.SinkerThrall=>.7f,DrownedQuarterContent.Engine=>1.4f,DrownedQuarterContent.Magistrate=>1.65f,DrownedQuarterContent.Bailiff=>.72f,
            CrimsonFoundryContent.Forgehand=>1f,CrimsonFoundryContent.Hound=>1.05f,CrimsonFoundryContent.RivetPriest=>1f,CrimsonFoundryContent.ChainWarden=>1.02f,
            CrimsonFoundryContent.AssemblyMaster=>1f,CrimsonFoundryContent.ScrapDrone=>.6f,CrimsonFoundryContent.Knight=>1.08f,CrimsonFoundryContent.Carrier=>1.12f,
            CrimsonFoundryContent.Automaton=>1.05f,CrimsonFoundryContent.Forgemaster=>1.3f,CrimsonFoundryContent.HammerDrone=>.72f,CrimsonFoundryContent.ShieldDrone=>.75f,
            CrimsonFoundryContent.Smelter=>1.4f,CrimsonFoundryContent.Prototype=>1.3f,CrimsonFoundryContent.Saint=>1.7f,CrimsonFoundryContent.Servitor=>.8f,
            HollowwoodContent.Sproutling=>.9f,HollowwoodContent.Stag=>1.15f,HollowwoodContent.Sporekeeper=>1.05f,HollowwoodContent.RootSnare=>1.05f,
            HollowwoodContent.BroodPod=>1.05f,HollowwoodContent.Sporeling=>.58f,HollowwoodContent.Bloomfang=>1.05f,HollowwoodContent.Parasite=>1.05f,
            HollowwoodContent.ElderHusk=>1.1f,HollowwoodContent.GardenMother=>1.3f,HollowwoodContent.Thornbud=>.7f,HollowwoodContent.Bloombud=>.68f,
            HollowwoodContent.Huskbud=>.7f,HollowwoodContent.WalkingGrove=>1.45f,HollowwoodContent.PaleGardener=>1.3f,HollowwoodContent.Heartroot=>1.7f,
            GildedRuinsContent.Scavenger=>.95f,GildedRuinsContent.Bastion=>1.1f,GildedRuinsContent.Chorister=>1f,GildedRuinsContent.Appraiser=>1f,GildedRuinsContent.Keeper=>1.2f,GildedRuinsContent.Servitor=>.62f,GildedRuinsContent.Mimic=>1.05f,GildedRuinsContent.Herald=>1.1f,
            GildedRuinsContent.Duelist=>1.05f,GildedRuinsContent.Collector=>1.3f,GildedRuinsContent.Guard=>.75f,GildedRuinsContent.Auctioneer=>1.3f,GildedRuinsContent.Treasury=>1.45f,GildedRuinsContent.Procession=>1.75f,
            ShatteredObservatoryContent.Scribe=>.95f,ShatteredObservatoryContent.Sentinel=>1.05f,ShatteredObservatoryContent.Attendant=>.98f,ShatteredObservatoryContent.Chronoglyph=>1f,
            ShatteredObservatoryContent.Lenskeeper=>1.05f,ShatteredObservatoryContent.Weaver=>1f,ShatteredObservatoryContent.StarFragment=>.6f,ShatteredObservatoryContent.Monk=>1.05f,
            ShatteredObservatoryContent.Astrologer=>1f,ShatteredObservatoryContent.OrreryKeeper=>1.3f,ShatteredObservatoryContent.SunFragment=>.72f,ShatteredObservatoryContent.MoonFragment=>.7f,
            ShatteredObservatoryContent.BlindSeer=>1.3f,ShatteredObservatoryContent.Comet=>1.3f,ShatteredObservatoryContent.Curator=>1.7f,
            BlackCathedralContent.Guard=>1.02f,BlackCathedralContent.Warden=>.95f,BlackCathedralContent.Confessor=>1f,BlackCathedralContent.Censer=>1f,BlackCathedralContent.Saint=>1.12f,
            BlackCathedralContent.Effigy=>.7f,BlackCathedralContent.Herald=>1.12f,BlackCathedralContent.Priest=>1.02f,BlackCathedralContent.Icon=>1.25f,
            BlackCathedralContent.HighConfessor=>1.4f,BlackCathedralContent.ChoirEternal=>1.45f,BlackCathedralContent.Bell=>1.55f,
            BlackCathedralContent.VoiceBlade=>.68f,BlackCathedralContent.VoiceMercy=>.66f,BlackCathedralContent.VoiceVigil=>.7f,BlackCathedralContent.Bishop=>1.75f,
            FracturedRealmContent.SplitEcho=>.66f,FracturedRealmContent.BladeFragment=>.68f,FracturedRealmContent.WardFragment=>.68f,FracturedRealmContent.Unmade=>1.8f,
            FracturedRealmContent.Binder=>.95f,FracturedRealmContent.Colossus=>1.3f,
            GildedThroneContent.Guard=>.66f,GildedThroneContent.Blade=>.66f,GildedThroneContent.Shield=>.7f,GildedThroneContent.Sovereign=>1.8f,
            GildedThroneContent.Strategist=>.95f,GildedThroneContent.Thronebreaker=>1.3f,GildedThroneContent.Crownshield=>1.1f,
            NeutralContent.Wayfarer=>1.25f,NeutralContent.Deepcrawler=>1.55f,NeutralContent.Worldbreaker=>1.7f,NeutralContent.UnboundBlade=>.95f,NeutralContent.IronWanderer=>1.1f,NeutralContent.PaleChimera=>1.05f,
            _=>enemy?.boss==true?1.4f:enemy?.elite==true?1.2f:1f
        };
    }
}
