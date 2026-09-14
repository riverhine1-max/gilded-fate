using System;
using System.Collections.Generic;

namespace GildedFate.Core
{
    // Explicit row-major identities: upgrades share their base card's illustration,
    // while no two different cards share an atlas cell. Black padding is unmapped.
    public static class GildedArtCatalog
    {
        public sealed class CardSheet
        {
            public readonly string resource;
            public readonly int columns, rows;
            public readonly string[] ids;
            public CardSheet(string resource,int columns,int rows,string[] ids)
            {this.resource=resource;this.columns=columns;this.rows=rows;this.ids=ids;}
        }

        public readonly struct CardTile
        {
            public readonly CardSheet sheet;
            public readonly int index;
            public CardTile(CardSheet sheet,int index){this.sheet=sheet;this.index=index;}
        }

        public static readonly CardSheet[] Sheets={
            new CardSheet("Art/Cards/Expansion/major_hexer",4,5,GameContent.MajorHexerCardIds),
            new CardSheet("Art/Cards/Expansion/major_reaper",5,2,GameContent.MajorReaperCardIds),
            new CardSheet("Art/Cards/Expansion/major_wanderer",4,4,GameContent.MajorWandererCardIds),
            new CardSheet("Art/Cards/Expansion/major_vanguard",4,5,GameContent.MajorVanguardCardIds),
            new CardSheet("Art/Cards/Expansion/gilded_warlord",1,1,new[]{"gilded_warlord"}),
            new CardSheet("Art/Cards/Expansion/unmovable",1,1,new[]{"unmovable"}),
            new CardSheet("Art/Cards/Expansion/break_the_line",1,1,new[]{"break_the_line"}),
            new CardSheet("Art/Cards/Expansion/relentless_conquest",1,1,new[]{"relentless_conquest"}),
            new CardSheet("Art/Cards/Expansion/vanguard_grid",3,2,new[]{"iron_momentum","crushing_advance","stand_your_ground","gilded_fury","breaking_momentum","vengeful_rush"}),
            new CardSheet("Art/Cards/Expansion/hexer_grid",3,3,new[]{"forbidden_convergence","brand_of_ruin","arcane_overload","beyond_the_veil_hexer","sigil_of_malice","unstable_ritual","dark_resonance","arcane_detonation","hexed_reverberation"}),
            new CardSheet("Art/Cards/Vanguard_5x10",5,10,new[]{"strike","defend","battle_temper","battle_cry","stand_firm","iron_will","armored_strike","iron_blood","living_armor","war_machine","crown_breaker","unbreakable_spirit","great_cleave","brace","crushing_blow","overhead_strike","crushing_weight","patient_warrior","executioners_cleave","final_judgment","spiked_guard","payback","come_at_me","no_mercy","shield_wall","retribution","hold_the_line","eye_for_an_eye","last_bastion","quick_slash","advance","battle_rush","rising_strike","relentless","blood_rush","flurry","onslaught","grand_finale","bloodied_armor","forceful_guard","raging_blow","countercharge","second_wind","blood_price","indomitable"}),
            new CardSheet("Art/Cards/Hexer_5x10",5,10,new[]{"hex_strike","ward","ember_ritual","hex_ritual","echo_ritual","invocation","first_ritual","arcane_ward","shatter_sigil","ritual_cycle","sigil_mastery","perfect_ritual","grand_convergence","cinder","scorch","kindle","wildfire","feed_the_flame","ignite","ashes","cremation","inferno","hex","marked_shot","soul_pierce","expose","soul_drain","hexed_blade","deaths_gaze","execution_hex","final_curse","dark_bargain","blood_magic","forbidden_knowledge","void_bolt","consume_darkness","embrace_the_void","dark_offering","damnation","forbidden_one","burning_hex","resonant_strike","blasphemous_ritual","hexfire","resonant_flame","void_sigil","black_sun"}),
            new CardSheet("Art/Cards/Reaper_6x10",6,10,new[]{"soul","scythe_strike","deaths_veil","soul_call","reaping_blow","grave_cut","soul_slash","spirit_cleave","deaths_touch","reaping_sweep","grave_guard","soul_guard","dark_veil_reaper","call_beyond","soul_offering","death_knell","grim_focus","grave_search","scythe_cycle","grim_flurry","soul_piercer_reaper","dark_insight","hollow_cut","soul_feast","spirit_scythe","deaths_embrace","grave_pact","soul_rend","death_march","hollow_scythe","soul_exchange","gravekeeper","deaths_door","soulstorm","beyond_the_veil","empty_grave","soul_carver","graves_edge","call_from_beyond","soul_echo","reapers_momentum","army_of_the_dead","soul_reaper","endless_harvest","devour_the_dead","death_incarnate","final_procession","grim_ascension","claim_the_fallen","soul_conversion","soulbound_tome","eternal_souls","reapers_calling"}),
            new CardSheet("Art/Cards/WandererAfflictions_6x10",6,10,new[]{"quick_thinking","cheap_shot","brace_yourself","opening_blow","preparation","follow_through","clear_mind","steady_hands","opportunist","adrenaline_rush","pocket_guard","finishing_cut","adapt","exploit_weakness","battle_rhythm","reserve_energy","recycle","preparation_strike","emergency_guard","tactical_advantage","breakthrough","controlled_breathing","resourceful","perfect_opportunity","improvisation","overflow","chain_reaction","limit_break","against_all_odds","perfect_form","dead_weight","dread","frailty","lingering_pain","decay","shackled","hollow","greed","doom","dazed_mind","shattered_guard","falter","heavy_chains","misfortune","haunting","fractured_will","arcane_lock","rust","fatebound","lost_moment","spirit_scar","twisted_fate"})
        };
        private static readonly Dictionary<string,CardTile> Cards=BuildLookup();
        private static Dictionary<string,CardTile> BuildLookup()
        {
            var result=new Dictionary<string,CardTile>(StringComparer.Ordinal);
            foreach(var sheet in Sheets)
                for(var i=0;i<sheet.ids.Length;i++)result.Add(sheet.ids[i],new CardTile(sheet,i));
            return result;
        }
        public static bool TryGetCard(string id,out CardTile tile)=>Cards.TryGetValue(id??"",out tile);
        public static string HeroResource(HeroId hero)=>"Art/Characters/"+hero.ToString().ToLowerInvariant();
        public static string EnemyResource(string id)=>"Art/Enemies/"+id;
    }
}
