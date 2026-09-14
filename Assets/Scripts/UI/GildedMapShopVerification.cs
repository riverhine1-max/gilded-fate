using System.Collections;
using System.Linq;
using GildedFate.Core;
using GildedFate.Map;
using GildedFate.Combat;
using GildedFate.Saving;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private bool ConfigureMapShopCapture(string mode)
        {
            if(mode is not ("merchant-full" or "merchant-sold" or "merchant-hover" or "upgrade-compare" or "upgrade-cost" or "map-shop-input"))return false;
            combatTestInput=true;run.NewRun(HeroId.Reaper,20260908);currentNode=run.nodes.First(n=>n.kind==NodeKind.Merchant);run.activeNodeFloor=currentNode.floor;run.activeNodeLane=currentNode.lane;run.floor=currentNode.floor;run.PrepareMerchantStock();run.gold=450;
            run.merchantShardId="hourglass";run.AddShard("bloodstone");run.stage=RunStage.Merchant;screen=ScreenMode.Merchant;
            if(mode=="merchant-sold"){run.PurchaseMerchantCard(1);run.PurchaseMerchantRelic(0);merchantSold.Clear();foreach(var id in run.merchantSold)merchantSold.Add(id);}
            if(mode is "upgrade-compare" or "upgrade-cost"){screen=ScreenMode.Collection;InspectUpgrade(GameContent.Find(mode=="upgrade-cost"?"invocation":"battle_rush"));}
            return true;
        }
        private IEnumerator RunMapShopInteractionChecks()
        {
            ConfigureMapShopCapture("merchant-full");yield return new WaitForSecondsRealtime(.5f);
            for(var i=0;i<7;i++)for(var j=i+1;j<7;j++)CombatCheck(!ShopCardRect(i).Overlaps(ShopCardRect(j)),"Merchant card slots do not overlap "+i+" / "+j);
            CombatCheck(!ShopRemovalRect.Overlaps(ShopRelicRect(1))&&!ShopRemovalRect.Overlaps(ShopShardRect),"Physical services and objects have separate click targets");
            var stock=run.merchantCardIds.ToArray();var count=run.cards.Count;var gold=run.gold;
            BuyShopCard(0);CombatCheck(run.cards.Count==count+1&&run.gold==gold-RunModel.MerchantCardPrice(GameContent.Find(stock[0])),"Clicking shop card buys exactly once");
            yield return new WaitForSecondsRealtime(1.7f);BuyShopCard(0);CombatCheck(run.cards.Count==count+1&&stock.SequenceEqual(run.merchantCardIds),"Sold slot cannot repurchase or rearrange");
            run.gold=0;BuyShopCard(1);CombatCheck(run.cards.Count==count+1&&rejectedShopItem=="card:"+stock[1],"Unaffordable click only pulses the price");
            run.gold=500;var removalPrice=run.MerchantRemovalCost;CompleteMerchantRemoval(run.cards[0]);CombatCheck(run.MerchantRemovalCost==removalPrice+25&&run.gold==500-removalPrice&&severedCard!=null,"Removal cuts one card and increases next price");
            yield return new WaitForSecondsRealtime(1);var saved=SaveService.Save(run);var restored=SaveService.Load();
            CombatCheck(saved&&restored!=null&&restored.merchantCardIds.SequenceEqual(stock)&&restored.MerchantRemovalCost==100,"Unity save restores shop stock and removal price");
            var card=GameContent.Find("battle_rush");InspectUpgrade(card);CombatCheck(inspectionComparison&&!inspectionUpgrade.exhaust&&UpgradeComparison.Removed(inspectionBase.text,inspectionUpgrade.text).Contains("Exhaust"),"Upgrade comparison explains removed Exhaust");
            var unchanged=GameContent.Upgrade(card);inspectionSource=null;PrepareCardInspection(unchanged);CombatCheck(!inspectionComparison&&inspectionShowUpgrade,"Actual upgraded card opens without comparison highlighting");inspectedCard=inspectionSource=null;
            ConfigureShardCapture("group-two");profile.fastMode=true;yield return new WaitForSecondsRealtime(1);
            var soul=GameContent.Find("soul").Copy();soul.instanceId=++combat.nextInstanceId;combat.hand.Insert(0,soul);combat.events.Add(new CombatEvent(CombatEventKind.Draw,1,true,soul));handReadyAt=Time.unscaledTime+ConsumeCombatEvents(combat.TakeEvents());yield return WaitForCombatQueue();yield return new WaitForSecondsRealtime(.2f);
            var hp=combat.EnemyAt(1).hp;var block=combat.EnemyAt(1).block;var other=combat.EnemyAt(0).hp;var start=CardPickPoint(0);var target=GroupDropZone(1).center;
            HandleCombatPointer(start,true,true,false);HandleCombatPointer(target,false,true,false);HandleCombatPointer(target,false,false,true);yield return WaitForCombatQueue();
            CombatCheck(combat.exhaust.Contains(soul)&&hp+block-combat.EnemyAt(1).hp-combat.EnemyAt(1).block==3&&combat.EnemyAt(0).hp==other,"Dragging Soul onto the second enemy deals only its damage and Exhausts");
            Debug.Log("[Gilded Fate Capture] Map/shop/Soul interaction checks "+combatInteractionChecks+" · failures "+combatInteractionFailures);
        }
    }
}
