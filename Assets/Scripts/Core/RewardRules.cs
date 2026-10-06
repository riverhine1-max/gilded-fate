using System;
using System.Collections.Generic;
using System.Linq;

namespace GildedFate.Core
{
    public static class RewardRules
    {
        public const int NormalRelicChance=3,EliteRelicChance=50,EliteShardChance=7,TreasureShardChance=12,MerchantShardChance=5;
        // Act 2 encounters pay more and roll better cards. Act 1 and Act 3 use the original table.
        public const int Act2NormalRelicChance=4,Act2EliteRelicChance=55,Act2EliteShardChance=9;
        private static void ValidateRoll(int roll){if(roll<0||roll>=100)throw new ArgumentOutOfRangeException(nameof(roll));}
        // Base Gold for winning an encounter (before relics and Fate Debt).
        public static int BaseGold(NodeKind kind,int act=1)=>act==2
            ?(kind==NodeKind.Boss?110:kind==NodeKind.Elite?40:21)
            :(kind==NodeKind.Boss?100:kind==NodeKind.Elite?34:18);
        public static Rarity CardRarity(NodeKind kind,int roll)=>CardRarity(kind,roll,1);
        public static Rarity CardRarity(NodeKind kind,int roll,int act)
        {
            ValidateRoll(roll);if(kind==NodeKind.Boss)return Rarity.Rare;
            var elite=kind==NodeKind.Elite;
            // Per slot: Act 1/3 normal 60/35/5, elite 35/50/15; Act 2 normal 50/40/10, elite 25/55/20.
            var (common,rareStart)=act==2?(elite?(25,80):(50,90)):(elite?(35,85):(60,95));
            return roll<common?Rarity.Common:roll<rareStart?Rarity.Uncommon:Rarity.Rare;
        }
        public static bool DropsRelic(NodeKind kind,int roll)=>DropsRelic(kind,roll,1);
        public static bool DropsRelic(NodeKind kind,int roll,int act)
        {
            ValidateRoll(roll);var a2=act==2;
            return roll<(kind==NodeKind.Elite?(a2?Act2EliteRelicChance:EliteRelicChance):kind==NodeKind.Combat?(a2?Act2NormalRelicChance:NormalRelicChance):0);
        }
        public static bool DropsShard(NodeKind kind,int roll)=>DropsShard(kind,roll,1);
        public static bool DropsShard(NodeKind kind,int roll,int act)
        {ValidateRoll(roll);return roll<(kind==NodeKind.Elite?(act==2?Act2EliteShardChance:EliteShardChance):kind==NodeKind.Treasure?TreasureShardChance:kind==NodeKind.Merchant?MerchantShardChance:0);}
        public static Rarity RelicRarity(int roll)
        {ValidateRoll(roll);return roll<55?Rarity.Common:roll<87?Rarity.Uncommon:Rarity.Rare;}
        public static CardDef[] Cards(HeroId hero,NodeKind kind,Random random)=>Cards(hero,kind,random,1);
        public static CardDef[] Cards(HeroId hero,NodeKind kind,Random random,int act)
        {
            var result=new List<CardDef>();
            // Roll a rarity for each slot BEFORE choosing uniformly within that rarity.
            // Catalog sizes therefore cannot distort the requested percentages.
            for(var i=0;i<3;i++)
            {
                var rarity=CardRarity(kind,random.Next(100),act);
                var pool=MetaUnlocks.Filter(GameContent.Cards.Where(c=>c.hero==hero&&c.rarity==rarity&&!result.Any(r=>r.id==c.id)),1).ToArray();
                if(pool.Length==0)throw new InvalidOperationException("The reward rarity needs three unique character cards.");
                result.Add(pool[random.Next(pool.Length)].Copy());
            }
            return result.ToArray();
        }
        public static RelicDef Relic(NodeKind kind,Random random,IEnumerable<string> owned)=>Relic(kind,random,owned,1);
        public static RelicDef Relic(NodeKind kind,Random random,IEnumerable<string> owned,int act)
        {
            if(!DropsRelic(kind,random.Next(100),act))return null; // No rarity roll on failure.
            var rarity=RelicRarity(random.Next(100));var ownedIds=new HashSet<string>(owned??Array.Empty<string>());
            var pool=GameContent.Relics.Where(r=>r.rarity==rarity&&!ownedIds.Contains(r.id)).ToArray();
            return pool.Length==0?null:pool[random.Next(pool.Length)];
        }
        public static CardDef[] UnboundCards(NodeKind kind,Random random,int count=4,IEnumerable<string> excluded=null)=>UnboundCards(kind,random,count,excluded,1);
        public static CardDef[] UnboundCards(NodeKind kind,Random random,int count,IEnumerable<string> excluded,int act)
        {
            var used=new HashSet<string>(excluded??Array.Empty<string>());var result=new List<CardDef>();
            var origins=new[]{CardOrigin.Knight,CardOrigin.Arcane,CardOrigin.Reaper,CardOrigin.Wanderer};
            for(var i=0;i<count;i++)
            {
                // Preserve encounter rarity odds, then weight each source equally.
                // Catalog size must not make the larger character pool more likely.
                var rarity=CardRarity(kind,random.Next(100),act);
                var groups=origins.Select(origin=>MetaUnlocks.Filter(GameContent.Cards.Where(c=>c.origin==origin&&c.rarity==rarity&&!used.Contains(c.id)),1).ToArray()).Where(g=>g.Length>0).ToArray();
                if(groups.Length==0)throw new InvalidOperationException("The Unbound Deck reward rarity has no unique cards remaining.");
                var pool=groups[random.Next(groups.Length)];var card=pool[random.Next(pool.Length)];used.Add(card.id);result.Add(card.Copy());
            }
            return result.ToArray();
        }
    }
}
