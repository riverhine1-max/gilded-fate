using System;
using System.Collections.Generic;
using System.Linq;

namespace GildedFate.Core
{
    public static class RewardRules
    {
        public const int NormalRelicChance=3,EliteRelicChance=50,EliteShardChance=7,TreasureShardChance=12,MerchantShardChance=5;
        private static void ValidateRoll(int roll){if(roll<0||roll>=100)throw new ArgumentOutOfRangeException(nameof(roll));}
        public static Rarity CardRarity(NodeKind kind,int roll)
        {
            ValidateRoll(roll);if(kind==NodeKind.Boss)return Rarity.Rare;
            var common=kind==NodeKind.Elite?35:60;var rareStart=kind==NodeKind.Elite?85:95;
            return roll<common?Rarity.Common:roll<rareStart?Rarity.Uncommon:Rarity.Rare;
        }
        public static bool DropsRelic(NodeKind kind,int roll)
        {ValidateRoll(roll);return roll<(kind==NodeKind.Elite?EliteRelicChance:kind==NodeKind.Combat?NormalRelicChance:0);}
        public static bool DropsShard(NodeKind kind,int roll)
        {ValidateRoll(roll);return roll<(kind==NodeKind.Elite?EliteShardChance:kind==NodeKind.Treasure?TreasureShardChance:kind==NodeKind.Merchant?MerchantShardChance:0);}
        public static Rarity RelicRarity(int roll)
        {ValidateRoll(roll);return roll<55?Rarity.Common:roll<87?Rarity.Uncommon:Rarity.Rare;}
        public static CardDef[] Cards(HeroId hero,NodeKind kind,Random random)
        {
            var result=new List<CardDef>();
            // Roll a rarity for each slot BEFORE choosing uniformly within that rarity.
            // Catalog sizes therefore cannot distort the requested percentages.
            for(var i=0;i<3;i++)
            {
                var rarity=CardRarity(kind,random.Next(100));
                var pool=GameContent.Cards.Where(c=>c.hero==hero&&c.rarity==rarity&&!result.Any(r=>r.id==c.id)).ToArray();
                if(pool.Length==0)throw new InvalidOperationException("The reward rarity needs three unique character cards.");
                result.Add(pool[random.Next(pool.Length)].Copy());
            }
            return result.ToArray();
        }
        public static RelicDef Relic(NodeKind kind,Random random,IEnumerable<string> owned)
        {
            if(!DropsRelic(kind,random.Next(100)))return null; // No rarity roll on failure.
            var rarity=RelicRarity(random.Next(100));var ownedIds=new HashSet<string>(owned??Array.Empty<string>());
            var pool=GameContent.Relics.Where(r=>r.rarity==rarity&&!ownedIds.Contains(r.id)).ToArray();
            return pool.Length==0?null:pool[random.Next(pool.Length)];
        }
        public static CardDef[] UnboundCards(NodeKind kind,Random random,int count=4,IEnumerable<string> excluded=null)
        {
            var used=new HashSet<string>(excluded??Array.Empty<string>());var result=new List<CardDef>();
            var origins=new[]{CardOrigin.Knight,CardOrigin.Arcane,CardOrigin.Reaper,CardOrigin.Wanderer};
            for(var i=0;i<count;i++)
            {
                // Preserve encounter rarity odds, then weight each source equally.
                // Catalog size must not make the larger character pool more likely.
                var rarity=CardRarity(kind,random.Next(100));
                var groups=origins.Select(origin=>GameContent.Cards.Where(c=>c.origin==origin&&c.rarity==rarity&&!used.Contains(c.id)).ToArray()).Where(g=>g.Length>0).ToArray();
                if(groups.Length==0)throw new InvalidOperationException("The Unbound Deck reward rarity has no unique cards remaining.");
                var pool=groups[random.Next(groups.Length)];var card=pool[random.Next(pool.Length)];used.Add(card.id);result.Add(card.Copy());
            }
            return result.ToArray();
        }
    }
}
