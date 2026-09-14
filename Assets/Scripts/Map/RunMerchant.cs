using System;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Core;

namespace GildedFate.Map
{
    public sealed partial class RunModel
    {
        public string merchantStockReceipt="";
        public List<string> merchantCardIds=new(),merchantRelicIds=new();
        public int merchantRemovals;
        public int MerchantRemovalCost=>75+25*Math.Max(0,merchantRemovals);
        public void ResetMerchantStock(){merchantStockReceipt="";merchantCardIds=new();merchantRelicIds=new();merchantRemovals=0;}
        public void CopyMerchantState(RunModel source)
        {merchantStockReceipt=source.merchantStockReceipt??"";merchantCardIds=source.merchantCardIds??new();merchantRelicIds=source.merchantRelicIds??new();merchantRemovals=Math.Max(0,source.merchantRemovals);}
        public void PrepareMerchantStock()
        {
            if(merchantStockReceipt==RoomReceipt&&merchantCardIds?.Count==7&&merchantRelicIds?.Count==2)return;
            var random=new Random(RoomSeed(77893));
            merchantCardIds=StockSample(GameContent.Cards.Where(c=>c.hero==hero&&c.rarity is Rarity.Common or Rarity.Uncommon or Rarity.Rare).Select(c=>c.id),5,random);
            merchantCardIds.AddRange(StockSample(GameContent.Cards.Where(c=>c.origin==CardOrigin.Wanderer&&c.rarity is Rarity.Common or Rarity.Uncommon or Rarity.Rare).Select(c=>c.id),2,random));
            var eligible=GameContent.Relics.Where(r=>r.rarity is Rarity.Common or Rarity.Uncommon or Rarity.Rare).ToArray();
            var unowned=eligible.Where(r=>!relics.Contains(r.id)).ToArray();
            merchantRelicIds=StockSample((unowned.Length>=2?unowned:eligible).Select(r=>r.id),2,random);
            merchantStockReceipt=RoomReceipt;PrepareMerchantShard();
        }
        private static List<string> StockSample(IEnumerable<string> values,int count,Random random)
        {
            var pool=values.Distinct().ToList();for(var i=pool.Count-1;i>0;i--){var other=random.Next(i+1);(pool[i],pool[other])=(pool[other],pool[i]);}
            return pool.Take(count).ToList();
        }
        public static int MerchantCardPrice(CardDef card)=>card.rarity==Rarity.Rare?90:card.rarity==Rarity.Uncommon?65:45;
        public bool PurchaseMerchantCard(int slot)
        {
            PrepareMerchantStock();if(slot<0||slot>=merchantCardIds.Count)return false;
            var card=GameContent.Find(merchantCardIds[slot]);var key="card:"+card.id;var price=MerchantCardPrice(card);
            if(merchantSold.Contains(key)||gold<price)return false;
            AddCard(card.id);gold-=price;merchantSold.Add(key);return true;
        }
        public bool PurchaseMerchantRelic(int slot)
        {
            PrepareMerchantStock();if(slot<0||slot>=merchantRelicIds.Count)return false;
            var id=merchantRelicIds[slot];var key="relic:"+id;
            if(merchantSold.Contains(key)||gold<120||!AcquireRelic(id))return false;
            gold-=120;merchantSold.Add(key);return true;
        }
        public bool PurchaseCardRemoval(RunCard card)
        {
            var price=MerchantRemovalCost;if(gold<price||card==null||!cards.Contains(card)||cards.Count<=1)return false;
            if(!RemoveCard(card))return false;gold-=price;merchantRemovals++;return true;
        }
        public bool HasValidMerchantStock()
        {
            if(merchantRemovals<0||merchantRemovals>100000)return false;
            if(string.IsNullOrEmpty(merchantStockReceipt))return true;
            return merchantCardIds!=null&&merchantCardIds.Count==7&&merchantCardIds.Distinct().Count()==7&&merchantCardIds.All(id=>GameContent.Find(id)!=null)
                &&merchantRelicIds!=null&&merchantRelicIds.Count==2&&merchantRelicIds.Distinct().Count()==2&&merchantRelicIds.All(id=>GameContent.Relics.Any(r=>r.id==id&&r.rarity is Rarity.Common or Rarity.Uncommon or Rarity.Rare));
        }
    }
}
