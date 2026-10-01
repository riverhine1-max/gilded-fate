using System;
using System.Collections.Generic;

namespace GildedFate.Core
{
    // Card unlocks. About a third of every pool (each hero and the Wanderer cards)
    // starts locked and is split into five tiers. A hero's tiers open with the Fate
    // Marks earned on that hero; Wanderer tiers open with total marks. The locked
    // set is deterministic (stable hash of the card id), so it never changes between
    // sessions. When no profile has been bound (tests, tools) everything is allowed.
    public static class MetaUnlocks
    {
        public const int Tiers=5;
        public static readonly int[] HeroThresholds={40,110,200,320,480};
        public static readonly int[] WandererThresholds={80,220,400,640,960};
        private static Dictionary<string,int> lockTier;
        private static int[] boundHeroMarks;private static int boundTotalMarks;private static bool bound;

        public static void Bind(int[] heroMarks,int totalMarks){boundHeroMarks=heroMarks;boundTotalMarks=totalMarks;bound=true;}
        public static void Unbind(){bound=false;}
        public static bool IsBound=>bound;

        private static int StableHash(string s){unchecked{var h=2166136261u;foreach(var c in s){h^=c;h*=16777619u;}return (int)(h&0x7FFFFFFF);}}
        private static int OriginIndex(CardOrigin origin)=>origin switch{CardOrigin.Knight=>0,CardOrigin.Arcane=>1,CardOrigin.Reaper=>2,CardOrigin.Wanderer=>3,_=>-1};
        private static void Build()
        {
            if(lockTier!=null)return;
            lockTier=new Dictionary<string,int>();
            foreach(var origin in new[]{CardOrigin.Knight,CardOrigin.Arcane,CardOrigin.Reaper,CardOrigin.Wanderer})
            {
                var picked=new List<CardDef>();
                foreach(var rarity in new[]{Rarity.Common,Rarity.Uncommon,Rarity.Rare})
                {
                    var pool=new List<CardDef>();
                    foreach(var c in GameContent.Cards)if(c.origin==origin&&c.rarity==rarity)pool.Add(c);
                    pool.Sort((a,b)=>StableHash(a.id).CompareTo(StableHash(b.id)));
                    var fraction=rarity==Rarity.Common?.25f:rarity==Rarity.Uncommon?.33f:.45f;
                    var count=(int)Math.Round(pool.Count*fraction);
                    for(var i=0;i<count&&i<pool.Count;i++)picked.Add(pool[i]);
                }
                picked.Sort((a,b)=>StableHash(a.id+"#tier").CompareTo(StableHash(b.id+"#tier")));
                for(var i=0;i<picked.Count;i++)lockTier[picked[i].id]=1+i%Tiers;
            }
        }
        // 0 = never locked; 1..5 = tier that unlocks it.
        public static int TierOf(CardDef card){if(card==null)return 0;Build();return lockTier.TryGetValue(card.id,out var t)?t:0;}
        public static int UnlockedTiers(CardOrigin origin,int[] heroMarks,int totalMarks)
        {
            var index=OriginIndex(origin);if(index<0)return Tiers;
            var marks=index==3?totalMarks:heroMarks!=null&&index<heroMarks.Length?heroMarks[index]:0;
            var thresholds=index==3?WandererThresholds:HeroThresholds;var tiers=0;
            foreach(var t in thresholds)if(marks>=t)tiers++;
            return tiers;
        }
        public static bool IsUnlocked(CardDef card,int[] heroMarks,int totalMarks)
        {
            var tier=TierOf(card);if(tier==0)return true;
            return tier<=UnlockedTiers(card.origin,heroMarks,totalMarks);
        }
        public static bool Allowed(CardDef card)=>!bound||IsUnlocked(card,boundHeroMarks,boundTotalMarks);
        // Reward pools: drop locked cards, but never leave a pool too thin to fill.
        public static IEnumerable<CardDef> Filter(IEnumerable<CardDef> pool,int minimum=3)
        {
            if(!bound)return pool;
            var all=new List<CardDef>(pool);var kept=all.FindAll(Allowed);
            return kept.Count>=minimum||kept.Count==all.Count?kept:all;
        }
        public static IEnumerable<string> FilterIds(IEnumerable<string> ids,int minimum=3)
        {
            if(!bound)return ids;
            var all=new List<string>(ids);var kept=all.FindAll(id=>Allowed(GameContent.Find(id)));
            return kept.Count>=minimum||kept.Count==all.Count?kept:all;
        }
        public static int LockedTotal(){Build();return lockTier.Count;}
        public static IEnumerable<CardDef> AllLockable(){Build();foreach(var c in GameContent.Cards)if(lockTier.ContainsKey(c.id))yield return c;}
    }
}
