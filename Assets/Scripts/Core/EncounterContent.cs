using System;
using System.Linq;

namespace GildedFate.Core
{
    public enum EncounterTier { Opening, Early, Mid, Late }
    [Serializable] public sealed class EncounterDef
    {
        public string id;public string[] enemies;public int minimumAct,maximumAct;public EncounterTier tier;
        public EncounterDef(string id,EncounterTier tier,int minAct,int maxAct,params string[] enemies)
        {this.id=id;this.tier=tier;minimumAct=minAct;maximumAct=maxAct;this.enemies=enemies;}
    }
    public static class EncounterContent
    {
        // Authored formations: group membership is explicit, never arbitrary duplication.
        public static readonly EncounterDef[] All={
            new("first_rat",EncounterTier.Opening,1,1,"vault_rat"),
            new("first_wisp",EncounterTier.Opening,1,1,"golden_wisp"),
            new("first_spider",EncounterTier.Opening,1,1,"vault_spider"),
            new("scavenger",EncounterTier.Early,1,1,"vault_rat"),
            new("web_sentinel",EncounterTier.Early,1,1,"vault_spider"),
            new("lonely_acolyte",EncounterTier.Early,1,1,"masked_acolyte"),
            new("rat_pair",EncounterTier.Early,1,1,"vault_rat","vault_rat"),
            new("sentry",EncounterTier.Mid,1,1,"gilded_sentry"),
            new("ash_hunter",EncounterTier.Mid,1,1,"ash_hound"),
            new("false_treasure",EncounterTier.Mid,1,1,"coin_mimic"),
            new("sentry_scavenger",EncounterTier.Mid,1,1,"gilded_sentry","vault_rat"),
            new("silver_web",EncounterTier.Mid,1,1,"vault_spider","golden_wisp"),
            new("cinder_rites",EncounterTier.Mid,1,1,"masked_acolyte","ash_hound"),
            new("fallen_oath",EncounterTier.Late,1,1,"broken_knight"),
            new("iron_prisoner",EncounterTier.Late,1,1,"chained_brute"),
            new("rat_guard",EncounterTier.Late,1,1,"vault_rat","gilded_sentry","vault_rat"),
            new("rune_guard",EncounterTier.Late,1,1,"rune_mage","broken_knight"),
            new("swarm",EncounterTier.Late,1,1,"vault_rat","vault_spider","vault_rat","golden_wisp"),
            new("second_watch",EncounterTier.Early,2,2,"gilded_sentry","masked_acolyte"),
            new("second_knight",EncounterTier.Early,2,2,"broken_knight"),
            new("furnace_pack",EncounterTier.Mid,2,2,"ash_hound","ash_hound","golden_wisp"),
            new("mimic_nest",EncounterTier.Mid,2,2,"coin_mimic","vault_spider","vault_rat"),
            new("shackled_choir",EncounterTier.Late,2,2,"masked_acolyte","chained_brute","golden_wisp"),
            new("gilded_patrol",EncounterTier.Late,2,2,"gilded_sentry","rune_mage","ash_hound","vault_rat"),
            new("final_ward",EncounterTier.Early,3,3,"broken_knight","golden_wisp"),
            new("final_sentinel",EncounterTier.Early,3,3,"chained_brute"),
            new("final_rites",EncounterTier.Early,3,3,"rune_mage","gilded_sentry"),
            new("final_furnace",EncounterTier.Mid,3,3,"ash_hound","chained_brute","rune_mage"),
            new("final_watch",EncounterTier.Mid,3,3,"broken_knight","gilded_sentry","masked_acolyte"),
            new("last_procession",EncounterTier.Late,3,3,"broken_knight","rune_mage","chained_brute","golden_wisp"),
            new("vault_coven",EncounterTier.Late,3,3,"masked_acolyte","ash_hound","rune_mage","broken_knight")
        };
        public static int GroupHp(int baseHp)=>Math.Max(1,(int)Math.Round(baseHp*.9,MidpointRounding.AwayFromZero));
        public static EncounterTier TierFor(int act,int floor,int combatsCompleted)
            =>act==1&&combatsCompleted==0?EncounterTier.Opening:floor<=4?EncounterTier.Early:floor<=10?EncounterTier.Mid:EncounterTier.Late;
        public static EncounterDef Choose(int act,int floor,int combatsCompleted,int seed)
        {
            var tier=TierFor(act,floor,combatsCompleted);
            var pool=All.Where(e=>e.minimumAct<=act&&e.maximumAct>=act&&e.tier==tier
                &&!(act==1&&combatsCompleted<2&&e.enemies.Length>1)).ToArray();
            return pool[new Random(seed).Next(pool.Length)];
        }
    }
}
