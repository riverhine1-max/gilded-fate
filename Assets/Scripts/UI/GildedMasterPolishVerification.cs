using System.Collections;
using System.Linq;
using GildedFate.Combat;
using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private bool ConfigureMasterPolishCapture(string mode)
        {
            if(!mode.StartsWith("polish-"))return false;
            combatTestInput=true;
            if(ConfigureCompletionCapture(mode))return true;
            if(ConfigureFinalPolishCapture(mode))return true;
            if(ConfigureMenuPolishCapture(mode))return true;
            if(ConfigureEnemyIntentCapture(mode))return true;
            PrepareCombatCheck(mode.Contains("health")?"stand_firm":"ward",5);
            profile.fastMode=false;profile.reduceMotion=false;
            if(run.hero==HeroId.Hexer)
            {
                combat.memory.extraSigilSlots=mode=="polish-sigils-base"?0:3;
                combat.sigils.AddRange(new[]{SigilKind.Ember,SigilKind.Hex,SigilKind.Echo,SigilKind.Ruin,SigilKind.Wither,SigilKind.Mirror}.Take(combat.SigilCapacity));
                combat.resonance=3;
            }
            run.AddShard("bloodstone");var shard=run.shards.First(s=>s.id=="bloodstone");
            shard.active=true;shard.uses=mode.Contains("fractured")?3:mode.Contains("stable")?1:2;
            shard.activeFractured=shard.uses==3;
            combat.ActivateShard(WorldContent.FateShards.First(s=>s.id==shard.id),shard.activeFractured);
            combat.player.hp=44;combat.player.block=12;combat.energy=1;
            var ids=run.hero==HeroId.Hexer?new[]{"hex_strike","ward","perfect_ritual","beyond_the_veil_hexer","first_ritual"}:new[]{"strike","stand_firm","executioners_cleave","unbreakable_spirit","defend"};
            for(var i=0;i<5;i++){var card=GameContent.Find(ids[i]).Copy();card.instanceId=combat.hand[i].instanceId;combat.hand[i]=card;}
            combat.TakeEvents();RestoreCombatPresentation();combatPointer=new Vector2(CombatWidth*.5f,220);
            return true;
        }
        private IEnumerator RunPowerPolishChecks()
        {
            foreach(var id in new[]{"battle_temper","beyond_the_veil_hexer","soulbound_tome"})
            {
                PrepareCombatCheck(id,5);profile.fastMode=false;profile.cardAnimationSpeed=1;
                yield return WaitForCombatQueue();yield return new WaitForSecondsRealtime(.3f);
                var card=combat.hand[2];var view=handViews[card.instanceId];var energy=combat.energy;
                QueueCardPlay(view);
                CombatCheck(cardMotions.Any(m=>m.card==card&&!m.absorb),"Power remains a full card before resolution: "+id);
                var deadline=Time.unscaledTime+2;
                while(!cardMotions.Any(m=>m.card==card&&m.absorb)&&Time.unscaledTime<deadline)yield return null;
                CombatCheck(cardMotions.Any(m=>m.card==card&&m.absorb),"Power has a separate post-resolution HUD journey: "+id);
                var settledState=JsonUtility.ToJson(combat.CaptureCheckpoint());
                var target=PowerHudTarget(card);var chips=PlayerEffectChips();var index=chips.FindIndex(c=>c.title==PowerIconCatalog.Title(card.name));
                CombatCheck(index>=0&&target==EffectCell(PlayerEffectArea(chips.Count),index,chips.Count),"Power lands on its actual effect slot: "+id);
                CombatCheck(target.y>HeroPortraitRect.yMax&&target.yMax<CombatHeight-244,"Power destination clears health and the hand: "+id);
                CombatCheck(PowerArrivalOpacity(card.name)<.01f,"New icon waits for its arriving identity: "+id);
                CombatCheck(combat.energy<energy&&combat.activeAspects.Count(c=>c.instanceId==card.instanceId)==1&&!combat.exhaust.Contains(card),"Aspect resolves and pays only once without Dissipating: "+id);
                yield return WaitForCombatQueue();yield return new WaitForSecondsRealtime(.2f);
                CombatCheck(JsonUtility.ToJson(combat.CaptureCheckpoint())==settledState,"HUD journey never reapplies gameplay: "+id);
                CombatCheck(PowerArrivalOpacity(card.name)==1&&powerArrivals.Count==0,"Power icon activates and arrival objects are released: "+id);
            }
            PrepareCombatCheck("soulbound_tome",5);combat.hand.Clear();combat.TakeEvents();combat.energy=99;
            foreach(var id in new[]{"deaths_embrace","endless_harvest","grim_ascension","soulbound_tome","eternal_souls"})
            {var card=GameContent.Find(id).Copy();card.instanceId=++combat.nextInstanceId;combat.hand.Add(card);CombatCheck(combat.Play(card),"Reaper Power gallery installs "+id);}
            combat.memory.soulsPlayedThisTurn=2;
            foreach(var id in new[]{"soul","soul","scythe_strike","deaths_veil","army_of_the_dead"})
            {var card=GameContent.Find(id).Copy();card.instanceId=++combat.nextInstanceId;combat.hand.Add(card);}
            combat.TakeEvents();RestoreCombatPresentation();combatPointer=new Vector2(CombatWidth*.5f,220);
            CombatCheck(PowerIsDormant("ENDLESS HARVEST")&&PowerIsDormant("DEATH'S EMBRACE"),"Limited Reaper triggers visibly rest after use");
            CombatCheck(!PowerIsDormant("GRIM ASCENSION")&&!PowerIsDormant("SOULBOUND TOME"),"Unlimited Powers remain active");
            foreach(var card in GameContent.Cards.Where(c=>c.kind==CardKind.Power))
            {CombatCheck(PowerIconCatalog.TryGet(card.name,out var icon)&&LoadAuthoredArt(icon.resource)!=null,"Power artwork resource exists: "+card.id);}
            yield return WaitForCombatQueue();
        }
        private IEnumerator RunMasterPolishChecks()
        {
            ConfigureMasterPolishCapture("polish-sigils");yield return WaitForCombatQueue();yield return new WaitForSecondsRealtime(.35f);
            CombatCheck(LoadAuthoredArt("Art/UI/MasterPolish/FateHealth")!=null,"Fitted Fate health artwork is in player resources");
            CombatCheck(OpenCombatHudFocus(7),"Controller can enter Sigil inspection");
            var readOnlyState=JsonUtility.ToJson(combat.CaptureCheckpoint());var readOnlyRun=JsonUtility.ToJson(run);
            var focusedSigils=new System.Collections.Generic.HashSet<string>();
            for(var i=0;i<6;i++){focusedSigils.Add(combatHudFocusKey);MoveCombatHudFocus(1,false);}
            CombatCheck(focusedSigils.Count==6&&focusedSigils.All(k=>k.StartsWith("sigil:")),"Controller can inspect every Sigil without moving ownership");
            yield return new WaitForSecondsRealtime(.22f);
            CombatCheck(JsonUtility.ToJson(combat.CaptureCheckpoint())==readOnlyState&&JsonUtility.ToJson(run)==readOnlyRun,"HUD inspection preserves all gameplay state and run time");
            CombatCheck(!CanAcceptCombatInput,"HUD inspection cannot accidentally play a card");
            combatHudInspectActive=false;
            CombatCheck(OpenCombatHudFocus(8)&&combatHudTargets[CombatHudFocusIndex].key.StartsWith("shard:"),"Shoulder inspection finds shard actions before activation");
            combatHudInspectActive=false;
            var area=HexerSigilArea;
            for(var count=3;count<=6;count++)
            {
                combat.memory.extraSigilSlots=count-3;visibleSigilSlots=count;
                var first=SigilSlotRect(0);var last=SigilSlotRect(count-1);
                CombatCheck(Mathf.Abs((first.center.x+last.center.x)*.5f-HeroPortraitRect.center.x)<.1f,"Sigil row centered "+count);
                CombatCheck(first.x>=area.x-1&&last.xMax<=area.xMax+1,"Sigil row remains in its area "+count);
                for(var i=0;i<count;i++)
                {
                    var r=SigilSlotRect(i);
                    CombatCheck(r.width>=44&&r.y>=106&&r.yMax+15<HeroPortraitRect.y,"Sigil readable and clear of HUD/actor "+count+"/"+i);
                    if(i>0)CombatCheck(!r.Overlaps(SigilSlotRect(i-1)),"Sigil slots never overlap "+count+"/"+i);
                }
            }
            combat.memory.extraSigilSlots=3;
            CombatCheck(RouteMapButton(CombatWidth).x==CombatWidth-185&&RunDeckControlRect(CombatWidth).x==CombatWidth-126,"Top-right order is Map / Deck / Settings");
            var originalScreen=screen;OpenRunDeck();
            var before=JsonUtility.ToJson(run);var combatBefore=JsonUtility.ToJson(combat.CaptureCheckpoint());
            yield return new WaitForSecondsRealtime(.3f);
            CombatCheck(IsRunInspectionPaused&&JsonUtility.ToJson(run)==before&&JsonUtility.ToJson(combat.CaptureCheckpoint())==combatBefore,"Deck inspection does not advance run or combat");
            screen=originalScreen;viewingRunDeck=false;yield return new WaitForSecondsRealtime(.25f);
            CombatCheck(handViews.Values.Any(v=>v.readinessKnown&&v.playable)&&handViews.Values.Any(v=>v.readinessKnown&&!v.playable),"Hand has distinct ready and unavailable states");
            combat.energy=99;yield return new WaitForSecondsRealtime(.05f);
            CombatCheck(handViews.Values.All(v=>v.playable)&&handViews.Values.Any(v=>v.readinessPulse>0),"New affordability produces one readiness pulse");
            yield return new WaitForSecondsRealtime(.4f);
            CombatCheck(handViews.Values.All(v=>v.readinessPulse<=0),"Readiness pulse stops instead of flashing continuously");
            combat.TakeEvents();var card=GameContent.Find("perfect_ritual").Copy();card.instanceId=++combat.nextInstanceId;combat.hand.Add(card);
            CombatCheck(combat.Play(card),"Six-Sigil ritual retains combat behavior");
            var facts=combat.TakeEvents();combatBusy=true;
            var finish=ConsumeCombatEvents(facts,card);
            CombatCheck(sigilPulseBeats.Count>=12&&sigilPulseBeats.Select(p=>p.slot).Distinct().Count()==6,"Every real Sigil owns its queued activation pulses");
            yield return new WaitForSecondsRealtime(finish+.1f);combatBusy=false;
            CombatCheck(sigilStateBeats.Count==0&&resonanceBeats.Count==0,"Sigil and Resonance playback receipts finish");
            ConfigureMasterPolishCapture("polish-sigils");yield return WaitForCombatQueue();yield return new WaitForSecondsRealtime(.3f);
            OpenCombatHudFocus(7);yield return new WaitForSecondsRealtime(.2f);
            CombatCheck(lastTooltipBounds.x>=16&&lastTooltipBounds.y>=68&&lastTooltipBounds.xMax<=CombatWidth-15&&lastTooltipBounds.yMax<=CombatHeight-15,"Focused tooltip stays inside the viewport");
            combatHudInspectActive=false;
        }
    }
}
