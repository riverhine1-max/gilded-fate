using System;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Audio;
using GildedFate.Core;
using GildedFate.Combat;
using GildedFate.Map;
using GildedFate.Saving;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private int selectedShrineSlot=-1;
        private sealed class ShardFlight { public Vector2 from,to;public float start;public bool shatter;public FateShardDef shard; }
        private readonly List<ShardFlight> shardFlights=new();
        private bool ShardDiscoveryOpen=>ShowsPersistentRunHud&&(!string.IsNullOrEmpty(run.pendingShardDiscoveryId)||run.shards.Count>RunModel.ShardCapacity);
        private bool EventShardPresentation=>screen==ScreenMode.EventSelection&&run.eventSelectionKind is EventSelectionKind.ShardReward or EventSelectionKind.ShardReplacement or EventSelectionKind.OwnedShard;
        private bool ShowsNormalShardShrine=>ShowsPersistentRunHud&&screen is not (ScreenMode.BindingSelect or ScreenMode.BindingCard)&&(screen is not (ScreenMode.Event or ScreenMode.EventSelection or ScreenMode.EventResult)
            ||EventShardPresentation||acquisitionActive&&acquisitionKind==AcquisitionKind.Shard||ShardDiscoveryOpen);
        private Rect ShrineSocket(int index)
        {
            const float size=84,step=124;var top=Mathf.Clamp(CombatHeight*.43f,160,Mathf.Max(160,CombatHeight-370));
            return new Rect(10,top+index*step,size,size);
        }
        private Rect ShrineStateLabel(int index){var r=ShrineSocket(index);return new Rect(r.x-4,r.yMax+9,r.width+8,22);}
        private void DrawShardShrine(float w,float h)
        {
            run.EnsureShardSlots();
            var first=ShrineSocket(0);var last=ShrineSocket(1);var gold=new Color(.68f,.51f,.25f,.86f);
            // A narrow edge-mounted metal spine, not a rectangular inventory panel.
            DrawLine(new Vector2(0,first.y-28),new Vector2(0,last.yMax+28),new Color(.025f,.03f,.045f,.96f),12);
            DrawLine(new Vector2(3,first.y-28),new Vector2(3,last.yMax+28),gold,2);
            for(var i=0;i<2;i++)
            {
                var r=ShrineSocket(i);var owned=run.shards.FirstOrDefault(s=>s.slot==i);
                var def=owned==null?null:WorldContent.FateShards.FirstOrDefault(s=>s.id==owned.id);
                var active=owned?.active==true;var fracture=owned!=null&&(active?owned.activeFractured:owned.Fractured);
                var dim=screen==ScreenMode.Combat&&!string.IsNullOrEmpty(combat?.activeShardId)&&!active;
                var pulse=shardFlights.Where(f=>!f.shatter&&Time.unscaledTime>=f.start&&Vector2.Distance(f.from,r.center)<3).Select(f=>Mathf.Clamp01(1-(Time.unscaledTime-f.start)/.36f)).DefaultIfEmpty(0).Max();
                var accent=fracture?new Color(1f,.58f,.24f):new Color(.72f,.85f,1f);
                if(active)accent=Color.Lerp(accent,Gold,.6f);var edge=Color.Lerp(gold,accent,active?.65f:.12f);
                var c=r.center;var points=new[]{new Vector2(c.x,r.y-5),new Vector2(r.xMax+2,c.y),new Vector2(c.x,r.yMax+5),new Vector2(r.x-2,c.y)};
                for(var side=0;side<4;side++){DrawLine(points[side],points[(side+1)%4],new Color(.025f,.028f,.045f,.98f),7);DrawLine(points[side],points[(side+1)%4],edge,1.4f+pulse);}
                DrawLine(new Vector2(3,c.y),new Vector2(r.x+12,c.y),gold,2);
                var old=GUI.color;GUI.color=dim?new Color(.48f,.48f,.55f,.64f):Color.white;
                if(def!=null)
                {
                    var scale=1+pulse*.14f+(fracture&&!profile.reduceMotion?Mathf.Sin(shimmer*2.6f)*.022f:0);
                    DrawFateShardArt(new Rect(c.x-r.width*.47f*scale,c.y-r.height*.47f*scale,r.width*.94f*scale,r.height*.94f*scale),def);
                    if(owned.uses>=(active?2:1))
                    {
                        var cracks=fracture?3:1;
                        for(var crack=0;crack<cracks;crack++){var start=c+new Vector2(-23+crack*21,-30);var mid=c+new Vector2(-7+crack*5,-4);var end=c+new Vector2(-17+crack*15,31);DrawLine(start,mid,new Color(accent.r,accent.g,accent.b,fracture?.85f:.42f),fracture?2:1);DrawLine(mid,end,new Color(accent.r,accent.g,accent.b,.62f),1);}
                    }
                    if(fracture&&!profile.reduceMotion&&!profile.reducedVfx)for(var spark=0;spark<5;spark++){var angle=shimmer*.6f+spark*1.257f;var at=c+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*52;Fill(new Rect(at.x-2,at.y-2,4,4),new Color(1f,.72f,.37f,.65f));}
                }
                else
                {DrawLine(c+Vector2.up*17,c+Vector2.down*17,new Color(.58f,.52f,.39f,.46f),1);DrawLine(c+Vector2.left*17,c+Vector2.right*17,new Color(.58f,.52f,.39f,.46f),1);}
                GUI.color=old;
                RegisterCombatHudTarget("shard:"+i,8,r,def?.name??"EMPTY SHARD SOCKET",def==null?"An empty socket for a Fate Shard.":(active?"ACTIVE\n":fracture?"FRACTURED\n":"STABLE\n")+(fracture?def.fracturedText:def.stableText)+"\n\nA: awaken this shard. Only one can be active each combat.",def==null?0:4,owned==null?-1:run.shards.IndexOf(owned));
                var state=def==null?"EMPTY":fracture?"FRACTURED":owned.uses==(active?1:0)?"STABLE I":"STABLE II";
                var stateStyle=ReadableStyle(11,true);stateStyle.normal.textColor=def==null?gold:accent;
                GUI.Label(ShrineStateLabel(i),state,stateStyle);
                if(r.Contains(PointerPosition)&&!ShardDiscoveryOpen)
                    SetRunHudTooltip(r,def?.name??"FATE SHARD SHRINE",def==null?"An empty socket. Rare Fate Shards can alter three combats. Carry two; awaken one per combat.":state+" · "+owned.uses+" / 3 ACTIVATIONS\n\nSTABLE\n"+def.stableText+"\n\nFRACTURED\n"+def.fracturedText);
                if(screen==ScreenMode.Combat&&def!=null&&GUI.Button(r,"",GUIStyle.none)&&CanAcceptCombatInput&&!dim&&!active)selectedShrineSlot=selectedShrineSlot==i?-1:i;
                if(selectedShrineSlot==i&&def!=null&&screen==ScreenMode.Combat&&!active&&!dim)
                {
                    var activate=new Rect(r.xMax+15,r.center.y-20,124,40);var enabled=GUI.enabled;GUI.enabled=enabled&&CanAcceptCombatInput&&owned.CanActivate;
                    DrawButtonFrame(activate,activate.Contains(PointerPosition),!GUI.enabled);
                    if(GUI.Button(activate,"ACTIVATE",buttonStyle)){selectedShrineSlot=-1;QueueShard(def,owned,run.shards.IndexOf(owned));}GUI.enabled=enabled;
                }
            }
        }
        private int ShardDiscoveryChoiceCount=>run.shards.Count>RunModel.ShardCapacity?run.shards.Count:(run.shards.Count<RunModel.ShardCapacity?2:RunModel.ShardCapacity+1);
        private void ConfirmShardDiscoveryChoice(int index)
        {
            if(acquisitionActive||!ShardDiscoveryOpen)return;
            if(run.shards.Count>RunModel.ShardCapacity){if(run.ResolveLegacyShardCapacity(index))SaveService.Save(run);return;}
            if(index==ShardDiscoveryChoiceCount-1){run.DeclineDiscoveredShard();SaveService.Save(run);return;}
            var shard=WorldContent.FateShards.FirstOrDefault(s=>s.id==run.pendingShardDiscoveryId);
            if(shard!=null&&index>=0&&index<ShardDiscoveryChoiceCount-1)TakeShardDiscovery(run.shards.Count<RunModel.ShardCapacity?-1:index,shard);
        }
        private void HandleShardDiscoveryNavigation(MenuNavigation input)
        {
            var count=ShardDiscoveryChoiceCount;var move=input.x!=0?input.x:input.y;
            shardDiscoveryIndex=(shardDiscoveryIndex+move+count)%count;
            if(input.back){if(run.shards.Count<=RunModel.ShardCapacity)ConfirmShardDiscoveryChoice(count-1);}
            else if(input.accept)ConfirmShardDiscoveryChoice(shardDiscoveryIndex);
        }
        private static Rect ShardReplacementTooltipRect(Rect anchor,float width,float height,float tooltipHeight)
        {
            const float tooltipWidth=330;
            var x=anchor.center.x<width*.5f?anchor.x-tooltipWidth-16:anchor.xMax+16;
            return new Rect(Mathf.Clamp(x,16,width-tooltipWidth-16),Mathf.Clamp(anchor.center.y-tooltipHeight*.5f,80,height-tooltipHeight-16),tooltipWidth,tooltipHeight);
        }
        private bool ShardChoiceHot(Rect rect,int index)=>controllerNavigation?shardDiscoveryIndex==index:rect.Contains(PointerPosition);
        private void DrawShardDiscovery(float w,float h)
        {
            runHudTooltipTitle="";hoveredCardHelp=null;
            Fill(new Rect(0,0,w,h),new Color(.002f,.004f,.01f,.94f));
            DrawMenuNavigationHint(w,h,"Arrows  Select    Enter  Confirm    Esc  Leave","D-pad / Stick  Select    A  Confirm    B  Leave");
            if(run.shards.Count>RunModel.ShardCapacity)
            {
                GUI.Label(new Rect(w*.15f,130,w*.7f,50),"CHOOSE TWO SHARDS TO CARRY",titleStyle);
                GUI.Label(new Rect(w*.2f,195,w*.6f,60),"Your older save contains three shards. Nothing was removed. Select the one you want to release.",new GUIStyle(footerStyle){fontSize=18,wordWrap=true});
                for(var i=0;i<run.shards.Count;i++)
                {
                    var def=WorldContent.FateShards.First(s=>s.id==run.shards[i].id);var r=new Rect(w*.5f-390+i*270,h*.40f,240,230);
                    DrawFateShardArt(new Rect(r.x+50,r.y,140,140),def);var release=new Rect(r.x,r.yMax-60,r.width,60);DrawButtonFrame(release,ShardChoiceHot(release,i),run.shards[i].active);
                    if(GUI.Button(release,"RELEASE\n"+def.name,buttonStyle))ConfirmShardDiscoveryChoice(i);
                    if(ShardChoiceHot(r,i))SetRunHudTooltip(r,def.name,def.stableText+"\n\nFRACTURED\n"+def.fracturedText);
                }
                GUI.Label(new Rect(w*.2f,h-100,w*.6f,36),"Choose one shard to release. No shard is removed automatically.",footerStyle);return;
            }
            var shard=WorldContent.FateShards.FirstOrDefault(s=>s.id==run.pendingShardDiscoveryId);if(shard==null){run.DeclineDiscoveredShard();return;}
            GUI.Label(new Rect(w*.15f,90,w*.7f,58),"FATE SHARD DISCOVERED",new GUIStyle(titleStyle){fontSize=36,normal={textColor=Gold}});
            var art=new Rect(w*.5f-105,167,210,210);var matrix=GUI.matrix;
            if(!profile.reduceMotion)GUIUtility.RotateAroundPivot(Mathf.Sin(shimmer*.75f)*5,art.center);DrawFateShardArt(art,shard);GUI.matrix=matrix;
            GUI.Label(new Rect(w*.2f,382,w*.6f,42),shard.name,new GUIStyle(titleStyle){fontSize=28,normal={textColor=new Color(.76f,.9f,1f)}});
            var body=new GUIStyle(footerStyle){fontSize=18,wordWrap=true,alignment=TextAnchor.UpperCenter,normal={textColor=new Color(.94f,.92f,.86f)}};
            GUI.Label(new Rect(w*.5f-330,439,660,72),"STABLE I / II\n"+shard.stableText,body);body.normal.textColor=new Color(1f,.78f,.49f);
            GUI.Label(new Rect(w*.5f-330,520,660,82),"FRACTURED\n"+shard.fracturedText,body);
            if(run.shards.Count<RunModel.ShardCapacity)
            {var take=new Rect(w*.5f-140,h-143,280,48);DrawButtonFrame(take,ShardChoiceHot(take,0),run.gold<run.pendingShardPrice);if(GUI.Button(take,run.pendingShardPrice>0?"TAKE SHARD · "+run.pendingShardPrice+" GOLD":"TAKE SHARD",buttonStyle))ConfirmShardDiscoveryChoice(0);}
            else
            {
                GUI.Label(new Rect(w*.25f,h-188,w*.5f,28),run.pendingShardPrice>0?"CHOOSE A SHARD TO REPLACE · PAY "+run.pendingShardPrice+" GOLD":"YOUR SHRINE IS FULL · CHOOSE A SHARD TO REPLACE",footerStyle);
                for(var i=0;i<RunModel.ShardCapacity;i++)
                {
                    var old=WorldContent.FateShards.First(s=>s.id==run.shards[i].id);var r=new Rect(w*.5f-330+i*340,h-156,320,64);var hot=ShardChoiceHot(r,i);
                    DrawButtonFrame(r,hot,run.shards[i].active||run.gold<run.pendingShardPrice);DrawFateShardArt(new Rect(r.x+8,r.y+7,50,50),old);
                    if(GUI.Button(new Rect(r.x+62,r.y,r.width-68,r.height),"REPLACE "+old.name,new GUIStyle(buttonStyle){fontSize=15,wordWrap=true}))ConfirmShardDiscoveryChoice(i);
                    if(hot)SetRunHudTooltip(r,"REPLACE "+old.name,"You release this shard and take "+shard.name+".\n\nCURRENT SHARD\n"+(run.shards[i].Fractured?old.fracturedText:old.stableText));
                }
            }
            var leave=new Rect(w*.5f-105,h-78,210,38);DrawButtonFrame(leave,ShardChoiceHot(leave,ShardDiscoveryChoiceCount-1),false);
            if(GUI.Button(leave,"LEAVE THE SHARD",buttonStyle))ConfirmShardDiscoveryChoice(ShardDiscoveryChoiceCount-1);
        }
        private void TakeShardDiscovery(int index,FateShardDef shard)
        {
            if(!run.TakeDiscoveredShard(index))return;
            merchantSold.Clear();foreach(var id in run.merchantSold)merchantSold.Add(id);
            SaveService.Save(run);BeginShardAcquisition(shard,()=>{});
        }
        private void DrawMerchantShardOffer(float w,float h,Vector2 pointer)
        {
            run.PrepareMerchantShard();if(string.IsNullOrEmpty(run.merchantShardId))return;
            var shard=WorldContent.FateShards.First(s=>s.id==run.merchantShardId);var sold=merchantSold.Contains("shard:"+shard.id);
            var r=new Rect(w*.58f,h*.62f,290,152);DrawFateShardArt(new Rect(r.x,r.y,94,94),shard);
            GUI.Label(new Rect(r.x+104,r.y+12,186,72),shard.name+"\nRARE FATE SHARD",new GUIStyle(footerStyle){fontSize=15,wordWrap=true,normal={textColor=Gold}});
            var buy=new Rect(r.x,r.yMax-45,r.width,42);DrawButtonFrame(buy,buy.Contains(pointer),sold||run.gold<45);
            if(r.Contains(pointer))SetRunHudTooltip(r,shard.name,shard.stableText+"\n\nFRACTURED: "+shard.fracturedText);
            if(GUI.Button(buy,sold?"SOLD":"DISCOVER · 45 GOLD",buttonStyle)&&!sold&&run.OfferShard(shard.id,45))SaveService.Save(run);
        }
        private bool DrawRolledCombatExtras(float w,float h)
        {
            run.RollEncounterRewards(currentNode?.kind??NodeKind.Combat);var reward=run.encounterRewards;
            if(!reward.relicClaimed&&!string.IsNullOrEmpty(reward.relicId))
            {
                DrawFullBackdrop(rewardBackground,w,h,.28f);DrawImportantRewardAtmosphere(w,h);DrawRunDock(w);Heading(w,"A RELIC AWAKENS","A RARE SPOIL FROM THIS ENCOUNTER");
                var relic=GameContent.Relics.First(r=>r.id==reward.relicId);var r=new Rect(w*.5f-250,h*.30f,500,260);
                DrawRelicArt(new Rect(r.x+15,r.y+24,130,130),Array.IndexOf(GameContent.Relics,relic));GUI.Label(new Rect(r.x+168,r.y+24,320,52),relic.name,new GUIStyle(titleStyle){fontSize=23,wordWrap=true});GUI.Label(new Rect(r.x+168,r.y+94,320,112),relic.text,new GUIStyle(footerStyle){fontSize=18,wordWrap=true,alignment=TextAnchor.UpperLeft});
                if(GUI.Button(new Rect(r.x+100,r.yMax-30,300,48),"TAKE RELIC",buttonStyle))ClaimRolledRelic();return true;
            }
            if(!reward.shardClaimed&&!string.IsNullOrEmpty(reward.shardId))
            {reward.shardClaimed=true;run.OfferShard(reward.shardId);SaveService.Save(run);return true;}
            return false;
        }
        private void ClaimRolledRelic()
        {
            var reward=run.encounterRewards;if(reward==null||reward.relicClaimed||string.IsNullOrEmpty(reward.relicId))return;
            var relic=GameContent.Relics.First(r=>r.id==reward.relicId);reward.relicClaimed=true;
            if(run.AcquireRelic(relic.id))profile.relicsCollected++;ProfileService.Save(profile);SaveService.Save(run);BeginRelicAcquisition(relic,()=>{});
        }
        private void DrawShardFlights()
        {
            var now=Time.unscaledTime;shardFlights.RemoveAll(f=>now-f.start>1.1f);
            foreach(var f in shardFlights)
            {
                var t=(now-f.start)/.6f;if(t<0||t>1.65f)continue;
                if(f.shatter)
                {
                    if(t<.6f&&f.shard!=null)DrawFateShardArt(new Rect(f.from.x-44,f.from.y-44,88,88),f.shard);
                    for(var i=0;i<12;i++){var a=i*Mathf.PI/6;var at=f.from+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*Mathf.Min(1,t)*72;var alpha=Mathf.Clamp01(1.6f-t);DrawLine(at,at+new Vector2(-8,-14),new Color(1f,.77f,.35f,alpha),2);}continue;
                }
                // Pulse, then a thread arriving at the .20-second effect receipt.
                var elapsed=now-f.start;
                if(elapsed>.04f&&elapsed<.20f){var p=(elapsed-.04f)/.16f;var end=Vector2.Lerp(f.from,f.to,p);DrawLine(Vector2.Lerp(f.from,f.to,Mathf.Max(0,p-.17f)),end,new Color(1f,.82f,.4f,.8f),2.5f);}
            }
        }
    }
}
