using System;
using System.Linq;
using GildedFate.Audio;
using GildedFate.Core;
using GildedFate.Map;
using GildedFate.Saving;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private string rejectedShopItem="";private float rejectedShopUntil;
        private CardDef severedCard;private float severStarted;
        private bool MerchantBusy=>acquisitionActive||severedCard!=null||inspectedCard!=null||ShardDiscoveryOpen;
        private Rect ShopCardRect(int slot)
        {
            var width=Mathf.Min(162,(CombatWidth-214)/7.65f);var gap=width*.075f;var total=width*7+gap*7;
            return new Rect(CombatWidth*.5f-total*.5f+60+slot*(width+gap)+(slot>=5?gap:0),203,width,width*1.40f);
        }
        private Rect ShopRelicRect(int slot)=>new Rect(CombatWidth*.23f+slot*CombatWidth*.145f-65,CombatHeight*.65f,130,130);
        private Rect ShopShardRect=>new Rect(CombatWidth*.58f-68,CombatHeight*.64f,136,136);
        private Rect ShopRemovalRect=>new Rect(CombatWidth*.78f-80,CombatHeight*.65f,160,130);
        private void ShopPrice(Rect item,int price,string key,bool sold)
        {
            var affordable=run.gold>=price;var hot=item.Contains(PointerPosition)&&!MerchantBusy;
            var rejection=rejectedShopItem==key?Mathf.Clamp01((rejectedShopUntil-Time.unscaledTime)/.45f):0;
            var r=new Rect(item.center.x-47+Mathf.Sin(rejection*32)*rejection*4,item.yMax+12,94,28);
            if(sold){GUI.Label(r,"SOLD",new GUIStyle(footerStyle){fontSize=13,normal={textColor=new Color(.65f,.60f,.48f)}});return;}
            DrawGoldIcon(new Rect(r.x,r.y+3,23,23));
            GUI.Label(new Rect(r.x+26,r.y,r.width-26,r.height),price.ToString(),new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=hot?18:16,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,normal={textColor=rejection>0?new Color(1,.43f,.34f):affordable?new Color(1,.85f,.49f):new Color(.62f,.56f,.43f)}});
        }
        private void RejectShopPrice(string key){rejectedShopItem=key;rejectedShopUntil=Time.unscaledTime+.45f;}
        private void RefreshMerchantPurchase()
        {merchantSold.Clear();foreach(var id in run.merchantSold)merchantSold.Add(id);SaveMerchantState();}
        private void BuyShopCard(int slot)
        {
            if(MerchantBusy)return;var card=GameContent.Find(run.merchantCardIds[slot]);var key="card:"+card.id;
            if(run.PurchaseMerchantCard(slot)){RefreshMerchantPurchase();BeginCardAcquisition(card,()=>{});}else if(!run.merchantSold.Contains(key))RejectShopPrice(key);
        }
        private void BuyShopRelic(int slot)
        {
            if(MerchantBusy)return;var relic=GameContent.Relics.First(r=>r.id==run.merchantRelicIds[slot]);var key="relic:"+relic.id;
            if(run.PurchaseMerchantRelic(slot)){profile.relicsCollected++;ProfileService.Save(profile);RefreshMerchantPurchase();BeginRelicAcquisition(relic,()=>{});}else if(!run.merchantSold.Contains(key))RejectShopPrice(key);
        }
        private void OpenMerchantRemoval()
        {
            if(MerchantBusy)return;if(run.gold<run.MerchantRemovalCost||run.cards.Count<=1){RejectShopPrice("remove");return;}
            collectionPage=0;cardServiceScroll=0;SaveMerchantState(RunStage.CardRemove);screen=ScreenMode.CardRemove;
        }
        private void CompleteMerchantRemoval(RunCard card)
        {
            if(MerchantBusy)return;var shown=card.BuildDefinition();if(!run.PurchaseCardRemoval(card)){RejectShopPrice("remove");return;}
            SaveMerchantState();screen=ScreenMode.Merchant;severedCard=shown;severStarted=Time.unscaledTime;Sfx(SoundCue.RemoveCard);
        }
        private void ShopThread(Rect item,bool hot)
        {
            var source=new Vector2(CombatWidth*.57f,CombatHeight-55);var target=new Vector2(item.center.x,item.yMax);
            DrawLine(source,target,new Color(.88f,.62f,.24f,hot?.58f:.11f),hot?1.6f:.7f);
            if(hot&&!profile.reduceMotion){var t=Mathf.Repeat(shimmer*.7f,1);var head=Vector2.Lerp(source,target,t);DrawLine(head,Vector2.Lerp(source,target,Mathf.Min(1,t+.05f)),new Color(1,.88f,.54f,.9f),2);}
        }
        private void DrawPhysicalMerchant(float w,float h)
        {
            run.PrepareMerchantStock();DrawLocationBackdrop(w,h,0);DrawRunDock(w);
            if(rejectedShopUntil>Time.unscaledTime)Fill(new Rect(190,8,104,40),new Color(1,.28f,.12f,Mathf.Clamp01((rejectedShopUntil-Time.unscaledTime)/.45f)*.2f));
            Heading(w,"THE THREAD BROKER","A PRICE FOR EVERY POSSIBILITY");
            // Two low gilded stall rails; the existing shop painting remains the room.
            var shelf=ShopCardRect(0).yMax+51;
            Fill(new Rect(160,shelf,w-187,14),new Color(.10f,.058f,.029f,.92f));DrawLine(new Vector2(153,shelf),new Vector2(w-25,shelf),new Color(.78f,.54f,.25f,.8f),2);
            DrawLine(new Vector2(178,h-102),new Vector2(w-47,h-102),new Color(.55f,.36f,.18f,.8f),4);
            var pointer=PointerPosition;
            GUI.Label(new Rect(ShopCardRect(0).x,174,ShopCardRect(4).xMax-ShopCardRect(0).x,22),run.hero.ToString().ToUpperInvariant()+" · FIVE THREADS",new GUIStyle(footerStyle){fontSize=12,normal={textColor=Gold}});
            GUI.Label(new Rect(ShopCardRect(5).x,174,ShopCardRect(6).xMax-ShopCardRect(5).x,22),"WANDERER · TWO PATHS",new GUIStyle(footerStyle){fontSize=12,normal={textColor=Gold}});
            for(var i=0;i<7;i++)
            {
                var card=GameContent.Find(run.merchantCardIds[i]);var key="card:"+card.id;var r=ShopCardRect(i);var price=RunModel.MerchantCardPrice(card);var sold=run.merchantSold.Contains(key);var hot=!MerchantBusy&&ScreenChoiceHot(r,i);ShopThread(r,hot);
                if(!sold){var shown=r;if(hot){shown.y-=8;shown.x-=2;shown.width+=4;shown.height+=5;}var old=GUI.color;if(run.gold<price)GUI.color=new Color(.73f,.73f,.73f,1);DrawCard(shown,card);GUI.color=old;RegisterCardKeywordHelp(shown,card,controllerNavigation&&screenControllerIndex==i);
                    if(hot&&!RuleKeywords.Any(keyword=>HasRuleKeyword(card.text,keyword)))SetRunHudTooltip(r,card.name,card.text+"\n\nRIGHT CLICK · UPGRADE PREVIEW");
                    if(!MerchantBusy&&GUI.Button(r,"",GUIStyle.none))BuyShopCard(i);
                }else{DrawLine(r.center+Vector2.left*24,r.center+Vector2.right*24,new Color(.59f,.43f,.24f,.4f),1);}
                ShopPrice(r,price,key,sold);
            }
            for(var i=0;i<2;i++)
            {
                var relic=GameContent.Relics.First(r=>r.id==run.merchantRelicIds[i]);var key="relic:"+relic.id;var r=ShopRelicRect(i);var sold=run.merchantSold.Contains(key)||run.relics.Contains(relic.id);var hot=!MerchantBusy&&ScreenChoiceHot(r,7+i);ShopThread(r,hot);
                if(!sold){var shown=hot?new Rect(r.x-5,r.y-9,r.width+10,r.height+10):r;var old=GUI.color;if(run.gold<120)GUI.color=new Color(.73f,.73f,.73f,1);DrawRelicArt(shown,Array.IndexOf(GameContent.Relics,relic));GUI.color=old;if(hot)SetRunHudTooltip(r,relic.name,relic.text);if(!MerchantBusy&&GUI.Button(r,"",GUIStyle.none))BuyShopRelic(i);}ShopPrice(r,120,key,sold);
            }
            if(!string.IsNullOrEmpty(run.merchantShardId))
            {
                var shard=WorldContent.FateShards.First(s=>s.id==run.merchantShardId);var r=ShopShardRect;var key="shard:"+shard.id;var sold=run.merchantSold.Contains(key);var hot=!MerchantBusy&&ScreenChoiceHot(r,9);ShopThread(r,hot);
                var center=r.center;foreach(var sign in new[]{-1,1}){DrawLine(new Vector2(center.x-68,center.y),new Vector2(center.x,center.y+sign*74),Gold,1);DrawLine(new Vector2(center.x+68,center.y),new Vector2(center.x,center.y+sign*74),Gold,1);}
                if(!sold){var old=GUI.color;if(run.gold<45)GUI.color=new Color(.73f,.73f,.73f,1);DrawFateShardArt(hot?new Rect(r.x-4,r.y-8,r.width+8,r.height+8):r,shard);GUI.color=old;if(hot)SetRunHudTooltip(r,shard.name,"STABLE\n"+shard.stableText+"\n\nFRACTURED\n"+shard.fracturedText);if(!MerchantBusy&&GUI.Button(r,"",GUIStyle.none))BuyShopShard();}ShopPrice(r,45,key,sold);
            }
            var device=ShopRemovalRect;var deviceHot=!MerchantBusy&&ScreenChoiceHot(device,10);ShopThread(device,deviceHot);
            DrawRemovalServiceIcon(device);
            GUI.Label(new Rect(device.x-30,device.yMax-22,device.width+60,24),"SEVER A THREAD",new GUIStyle(footerStyle){fontSize=15,fontStyle=FontStyle.Bold,normal={textColor=Gold}});ShopPrice(device,run.MerchantRemovalCost,"remove",false);
            if(deviceHot)SetRunHudTooltip(device,"SEVER A THREAD","Permanently remove one chosen card. Each removal raises the next price by 25 Gold.\n\nCurrent price: "+run.MerchantRemovalCost+" Gold.");if(!MerchantBusy&&GUI.Button(device,"",GUIStyle.none))OpenMerchantRemoval();
            var heal=new Rect(w-133,h*.66f+14,66,76);var healTint=GUI.color;if(merchantHealed||run.hp>=run.maxHp)GUI.color=new Color(.45f,.45f,.45f,1);DrawAtlasIcon(hudEmblemAtlas,1,4,2,FittedServiceIcon(heal,60));GUI.color=healTint;
            ShopPrice(heal,35,"heal",merchantHealed);if(!merchantHealed&&run.hp>=run.maxHp)GUI.Label(new Rect(heal.x-24,heal.y-20,114,20),"FULL HEALTH",new GUIStyle(footerStyle){fontSize=11});
            if(controllerNavigation?screenControllerIndex==11:heal.Contains(pointer)){Outline(new Rect(heal.x-4,heal.y-4,heal.width+8,heal.height+8),Gold,2);SetRunHudTooltip(heal,"RESTORE 18 HP","Recover up to 18 HP for 35 Gold. Once per visit."+(run.hp>=run.maxHp?"\n\nAlready at full health — no purchase needed.":""));}
            if(!MerchantBusy&&GUI.Button(heal,"",GUIStyle.none))BuyShopHeal();
            GUI.Label(new Rect(178,h-68,w-430,30),"CLICK AN ITEM TO BUY · RIGHT CLICK / I / X TO INSPECT CARDS",new GUIStyle(footerStyle){fontSize=12,normal={textColor=new Color(.84f,.78f,.66f)}});
            var leave=new Rect(w-220,h-67,190,43);DrawButtonFrame(leave,ScreenChoiceHot(leave,12),MerchantBusy);if(!MerchantBusy&&GUI.Button(leave,"LEAVE SHOP",buttonStyle))Advance();
        }
        private void BuyShopShard()
        {if(MerchantBusy||string.IsNullOrEmpty(run.merchantShardId)||run.merchantSold.Contains("shard:"+run.merchantShardId))return;if(run.OfferShard(run.merchantShardId,45))SaveService.Save(run);else RejectShopPrice("shard:"+run.merchantShardId);}
        private void BuyShopHeal()
        {if(MerchantBusy||merchantHealed)return;if(run.gold>=35&&run.hp<run.maxHp){var before=run.hp;run.gold-=35;run.hp=Mathf.Min(run.maxHp,run.hp+18);merchantHealed=true;SaveMerchantState();ShowHealthServiceGain(before);}else RejectShopPrice("heal");}
        private void DrawSeveredThread(float w,float h)
        {
            if(severedCard==null)return;var duration=profile.reduceMotion?.25f:.85f;var t=(Time.unscaledTime-severStarted)/duration;if(t>=1){severedCard=null;return;}
            Fill(new Rect(0,58,w,h-58),new Color(0,0,0,.6f));var card=new Rect(w*.5f-110,h*.24f,220,310);var old=GUI.color;GUI.color=new Color(1,1,1,1-Mathf.Clamp01((t-.45f)/.55f));DrawCard(card,severedCard);GUI.color=old;
            for(var i=0;i<5;i++){var origin=new Vector2(w*.25f,h*.30f+i*72);var target=new Vector2(card.x,card.y+35+i*54);var end=t<.45f?target:Vector2.Lerp(target,origin,(t-.45f)/.55f);DrawLine(origin,end,new Color(1,.78f,.35f,1-t),2);}
            GUI.Label(new Rect(w*.25f,card.yMax+25,w*.5f,40),"A THREAD RELEASED",new GUIStyle(titleStyle){fontSize=27,normal={textColor=Gold}});
        }
    }
}
