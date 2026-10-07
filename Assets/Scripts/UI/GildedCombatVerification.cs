using System.Collections;
using System.Linq;
using GildedFate.Combat;
using GildedFate.Core;
using GildedFate.Map;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private int combatInteractionFailures,combatInteractionChecks;
        private bool ConfigureCombatCapture(string mode)
        {
            if(mode=="combat-hexer"||mode=="combat-reaper")return false;
            if(!mode.StartsWith("combat-")&&mode!="pile-discard"&&mode!="pile-inspection")return false;
            combatTestInput=true;
            var count=mode=="combat-hand3"?3:mode=="combat-hand8"?8:mode=="combat-hand10"?10:5;
            PrepareCombatCheck(mode=="combat-keyword"?"bloodied_armor":mode=="combat-power"?"living_armor":"strike",count);
            if(mode=="combat-hand8"||mode=="combat-hand10")
            {
                // Variety comes from existing content, not additional authored cards.
                var pool=GameContent.Cards.Where(c=>c.hero==HeroId.Vanguard&&c.rarity!=Rarity.Curse).ToArray();
                for(var i=0;i<combat.hand.Count;i++){var id=combat.hand[i].instanceId;var copy=pool[i].Copy();copy.instanceId=id;combat.hand[i]=copy;}
                combat.events.Clear();foreach(var card in combat.hand)combat.events.Add(new CombatEvent(CombatEventKind.Draw,1,true,card));ResetCombatPresentation();
            }
            if(mode=="combat-hover"||mode=="combat-target"||mode=="combat-keyword")StartCoroutine(PrepareHoveredCapture(mode));
            if(mode=="combat-power")
            {
                combat.player.strength=2;combat.player.weak=1;combat.enemy.block=10;combat.enemy.burn=4;combat.enemy.marked=2;
                StartCoroutine(PreparePowerCapture());
            }
            if(mode=="pile-discard"){combat.discard.AddRange(combat.draw.Take(5));combat.draw.RemoveRange(0,5);pileOpen=1;}
            if(mode=="pile-inspection"){pileOpen=0;inspectedCard=combat.draw[0];}
            return true;
        }
        private IEnumerator PreparePowerCapture()
        {
            yield return new WaitForSecondsRealtime(.72f);
            var start=CardPickPoint(2);var destination=SkillDropZone.center;
            HandleCombatPointer(start,true,true,false);HandleCombatPointer(destination,false,true,false);HandleCombatPointer(destination,false,false,true);
        }
        private void PrepareCombatCheck(string cardId,int count)
        {
            var card=GameContent.Cards.First(c=>c.id==cardId);var hero=card.hero??HeroId.Vanguard;
            run.NewRun(hero,20260903);currentNode=new MapNode{floor=0,lane=1,kind=NodeKind.Combat};currentEnemy=WorldContent.Enemies.First(e=>e.id=="gilded_sentry");
            combat=new CombatState();combat.Begin(hero,Enumerable.Repeat(card,14),1000,0,80,80,null,currentEnemy.id,9,772);
            combat.energy=20;
            while(combat.hand.Count<count)combat.DrawCards(1);
            while(combat.hand.Count>count){combat.draw.Add(combat.hand[^1]);combat.hand.RemoveAt(combat.hand.Count-1);}
            combat.events.Clear();foreach(var c in combat.hand)combat.events.Add(new CombatEvent(CombatEventKind.Draw,1,true,c));
            screen=ScreenMode.Combat;bossIntroTime=0;ResetCombatPresentation();
        }
        private IEnumerator PrepareHoveredCapture(string mode)
        {
            yield return new WaitForSecondsRealtime(.9f);
            var slot=HandLayout.Slot(2,combat.hand.Count,CombatWidth,CombatHeight);
            HandleCombatPointer(new Vector2(slot.x-60,slot.y),false,false,false);
            if(mode=="combat-target")
            {
                HandleCombatPointer(new Vector2(slot.x-60,slot.y),true,true,false);
                HandleCombatPointer(new Vector2(EnemyVisualCenterX,EnemyDropZone.center.y),false,true,false);
            }
        }
        private void CombatCheck(bool passed,string message)
        {
            combatInteractionChecks++;
            if(!passed){combatInteractionFailures++;Debug.LogError("[Gilded Fate Interaction] FAIL · "+message);}
            else Debug.Log("[Gilded Fate Interaction] PASS · "+message);
        }
        private Vector2 CardPickPoint(int index)
        {
            var slot=HandLayout.Slot(index,combat.hand.Count,CombatWidth,CombatHeight);return new Vector2(slot.x-65,slot.y);
        }
        private IEnumerator WaitForCombatQueue()
        {
            var timeout=Time.unscaledTime+7;
            while((combatBusy||Time.unscaledTime<handReadyAt)&&Time.unscaledTime<timeout)yield return null;
            CombatCheck(!combatBusy&&Time.unscaledTime>=handReadyAt,"Queue completes and unlocks input");
        }
        private IEnumerator RunCombatInteractionChecks()
        {
            combatTestInput=true;profile.fastMode=false;profile.cardAnimationSpeed=1;
            PrepareCombatCheck("strike",5);yield return new WaitForSecondsRealtime(.85f);
            var resting=HandLayout.Slot(2,5,CombatWidth,CombatHeight);
            CombatCheck(resting.y+HandLayout.CardHeight*.5f>CombatHeight-20&&resting.y+HandLayout.CardHeight*.5f<CombatHeight+8,"Resting hand is only slightly tucked below the screen edge");
            CombatCheck(resting.y-144-HandLayout.CardHeight*1.18f*.5f>126&&resting.y-144+HandLayout.CardHeight*1.18f*.5f<CombatHeight-20,"Hovered card rises into a fully readable position");
            CombatCheck(PileRect(0).center.x<CombatWidth*.12f&&PileRect(1).center.x>CombatWidth*.88f,"Draw pile is on the left and Discard is on the right");
            // Polish: the fan never slides under the energy orb or the End Turn button, at any hand size.
            for(var handSize=1;handSize<=12;handSize++)
            {
                var first=HandLayout.Slot(0,handSize,CombatWidth,CombatHeight);var last=HandLayout.Slot(handSize-1,handSize,CombatWidth,CombatHeight);
                CombatCheck(first.x-HandLayout.CardEdgeAllowance>=EnergyMeterRect.xMax&&last.x+HandLayout.CardEdgeAllowance<=EndTurnRect.x,"Hand of "+handSize+" clears the energy orb and End Turn");
                CombatCheck(!EndTurnRect.Overlaps(PileRect(1))&&!EndTurnRect.Overlaps(PileRect(2))&&!EndTurnRect.Overlaps(EnergyMeterRect),"End Turn does not overlap the piles or the energy orb");
                CombatCheck(last.x+HandLayout.CardEdgeAllowance<=GildButtonRect.x,"Hand of "+handSize+" clears the Gild badge");
            }
            // Polish: the Gild badge sits under End Turn, clear of every other control, and its plate stays on screen.
            CombatCheck(!GildButtonRect.Overlaps(EndTurnRect)&&!GildButtonRect.Overlaps(PileRect(0))&&!GildButtonRect.Overlaps(PileRect(1))&&!GildButtonRect.Overlaps(PileRect(2))&&!GildButtonRect.Overlaps(EnergyMeterRect),"Gild badge does not overlap End Turn, the piles or the energy orb");
            CombatCheck(GildButtonRect.xMax<=CombatWidth&&GildButtonRect.yMax<=CombatHeight-20&&GildButtonRect.y>EndTurnRect.yMax&&GildPlateRect.yMax<=PileRect(1).yMax&&GildButtonRect.Contains(GildCoinCenter)&&GildButtonRect.Contains(GildPlateRect.center),"Gild badge sits under End Turn with its coin and plate inside the clickable area");
            var scaledCenter=CanvasPointFromPhysical(new Vector2(1440,810),1620,2);
            CombatCheck(Vector2.Distance(scaledCenter,new Vector2(720,405))<.01f,"Physical pointer converts to the same virtual-canvas point at scaled resolutions");
            var markedText=GameContent.Cards.First(c=>c.id=="hex").text;
            CombatCheck(FormatCardRules(markedText).Contains("<b><color=")&&RuleKeywords.Any(keyword=>HasRuleKeyword(markedText,keyword)),"Card rules emphasize keywords and expose matching help");
            CheckBindingClaspGeometry();
            var visualTarget=EnemyDropZone;var targetingCard=combat.hand[0];
            foreach(var targetPoint in new[]{visualTarget.center,new Vector2(visualTarget.x+8,visualTarget.center.y),new Vector2(visualTarget.xMax-8,visualTarget.center.y),new Vector2(visualTarget.center.x,visualTarget.y+8),new Vector2(visualTarget.center.x,visualTarget.yMax-8)})
                CombatCheck(IsValidCardDrop(targetingCard,targetPoint),"Attack accepts the full visible enemy target at "+targetPoint);
            CombatCheck(!IsValidCardDrop(targetingCard,new Vector2(visualTarget.x-14,visualTarget.center.y)),"Attack rejects space clearly outside the enemy target");
            // Polish: aiming. A locked target holds across its soft edge; a plain pointer move keeps the exact edge.
            var lockedOn=AimTargetAt(visualTarget.center,true);
            CombatCheck(lockedOn>=0&&AimTargetAt(new Vector2(visualTarget.x-12,visualTarget.center.y),true)==lockedOn,"A locked target holds while the pointer drifts a few pixels outside it");
            CombatCheck(AimTargetAt(new Vector2(visualTarget.x-StickyTargetMargin-12,visualTarget.center.y),true)==-1,"The lock releases once the pointer clearly leaves the target");
            CombatCheck(AimTargetAt(new Vector2(visualTarget.x-12,visualTarget.center.y),false)==-1&&stickyTarget==-1,"Without a drag the target edge stays exact");
            var playFlight=new CardMotion{play=true,from=Vector2.zero,to=new Vector2(300,-200)};var easeOk=Mathf.Abs(CardMotionEase(playFlight,0))<.0001f&&Mathf.Abs(CardMotionEase(playFlight,1)-1)<.0001f;
            for(var step=1;step<=20;step++)easeOk&=CardMotionEase(playFlight,step/20f)>=CardMotionEase(playFlight,(step-1)/20f);
            CombatCheck(easeOk&&CardMotionEase(playFlight,.5f)>CardMotionEase(null,.5f),"A played card's flight leaves quickly, never moves backwards and lands exactly on its impact point");
            CombatCheck(Mathf.Abs(CardMotionLift(playFlight,0,0))<.0001f&&Mathf.Abs(CardMotionLift(playFlight,1,1))<.01f,"A played card's arc starts and ends flat");
            var strikeImpact=CardImpactPoint(targetingCard);
            CombatCheck(visualTarget.Contains(strikeImpact)&&Mathf.Abs(strikeImpact.x-EnemyVisualCenterX)<1,"Attack animation lands on the visible enemy center");
            foreach(var count in new[]{3,5,8,10})
            {
                PrepareCombatCheck("strike",count);
                // Ten cards take longer to finish their staggered draw than five.
                // Test the ready hand, not a fixed one-second animation snapshot.
                yield return WaitForCombatQueue();yield return new WaitForSecondsRealtime(.25f);
                for(var i=0;i<count;i++)CombatCheck(PickHandCard(CardPickPoint(i))?.card==combat.hand[i],$"{count}-card fan: card {i+1} is independently pickable");
                var card=combat.hand[0];var before=combat.energy;var start=CardPickPoint(0);
                hoverView=null;HandleCombatPointer(start,true,true,false);
                CombatCheck(dragView!=null&&dragView.card==card,$"{count}-card fan: mouse-down starts drag without a prior hover frame");
                HandleCombatPointer(new Vector2(10,90),false,true,false);HandleCombatPointer(new Vector2(10,90),false,false,true);
                yield return new WaitForSecondsRealtime(.65f);
                var slot=HandLayout.Slot(0,count,CombatWidth,CombatHeight);var view=handViews[card.instanceId];
                CombatCheck(combat.hand.Contains(card)&&combat.energy==before&&!combatBusy,$"{count}-card invalid drop does not spend or play");
                CombatCheck(Vector2.Distance(view.position,new Vector2(slot.x,slot.y))<3&&Mathf.Abs(view.angle-slot.angle)<.5f,$"{count}-card invalid drop returns to its exact fan slot");
                // Polish: a hovered card rises, settles and then holds perfectly still. It must never buzz around its own slot,
                // because a shaking card cannot be read.
                var hoverIndex=Mathf.Min(1,count-1);var hoveredCard=combat.hand[hoverIndex];
                HandleCombatPointer(CardPickPoint(hoverIndex),false,false,false);
                yield return new WaitForSecondsRealtime(.75f);
                var hoveredView=handViews[hoveredCard.instanceId];var lowX=float.MaxValue;var highX=float.MinValue;var lowY=float.MaxValue;var highY=float.MinValue;
                for(var frame=0;frame<30;frame++){yield return null;lowX=Mathf.Min(lowX,hoveredView.position.x);highX=Mathf.Max(highX,hoveredView.position.x);lowY=Mathf.Min(lowY,hoveredView.position.y);highY=Mathf.Max(highY,hoveredView.position.y);}
                CombatCheck(hoverView==hoveredView&&highX-lowX<.5f&&highY-lowY<.5f,$"{count}-card fan: a hovered card holds perfectly still (moved {highX-lowX:F2} x {highY-lowY:F2} px over 30 frames)");
                HandleCombatPointer(new Vector2(CombatWidth*.5f,90),false,false,false);
            }
            foreach(var id in new[]{"strike","defend","hex","scorch","living_armor"})
            {
                var realId=id;
                PrepareCombatCheck(realId,5);yield return new WaitForSecondsRealtime(.85f);
                var card=combat.hand[2];var before=combat.energy;var cost=combat.CostFor(card);var start=CardPickPoint(2);
                var destination=combat.RequiresEnemyTarget(card)?EnemyDropZone.center:new Vector2(start.x,CombatHeight-180);
                if(!combat.RequiresEnemyTarget(card))CombatCheck(SkillDropZone.Contains(destination),"Targetless card accepts a natural release just above the hand: "+realId);
                HandleCombatPointer(start,true,true,false);HandleCombatPointer(destination,false,true,false);HandleCombatPointer(destination,false,false,true);
                CombatCheck(combatBusy,"Valid drag immediately locks against double play: "+realId);
                if(card.kind==CardKind.Power)CombatCheck(cardMotions.Any(m=>m.card==card&&!m.exhaust&&!m.absorb),"Power keeps its full identity before its separate post-resolution HUD flight");
                HandleCombatPointer(start,true,true,false);HandleCombatPointer(destination,false,false,true);
                yield return WaitForCombatQueue();
                CombatCheck(combat.energy==before-cost&&combat.cardsPlayed==1,"One drag spends Energy and resolves exactly once: "+realId);
                CombatCheck(!combat.hand.Contains(card)&&(card.kind==CardKind.Power?combat.activeAspects:combat.discard).Contains(card),"Played instance reaches correct ownership area: "+realId);
                if(card.id=="living_armor")CombatCheck(PlayerEffectChips().Any(chip=>chip.title=="LIVING ARMOR"&&chip.value==combat.memory.livingArmor),"Resolved Power appears beneath hero health with its live value");
                if(combat.RequiresEnemyTarget(card))CombatCheck(!IsValidCardDrop(combat.hand[0],new Vector2(470,210)),"Offensive card rejects self/battlefield target: "+realId);
            }
            PrepareCombatCheck("strike",5);yield return new WaitForSecondsRealtime(.85f);combat.energy=0;var denied=combat.hand[0];var point=CardPickPoint(0);
            HandleCombatPointer(point,true,true,false);HandleCombatPointer(EnemyDropZone.center,false,true,false);HandleCombatPointer(EnemyDropZone.center,false,false,true);
            CombatCheck(!combatBusy&&combat.hand.Contains(denied)&&combat.energy==0,"Insufficient Energy blocks actual pointer interaction");
            combat.energy=3;HandleCombatPointer(point,true,true,false);HandleCombatPointer(point,false,false,true);
            CombatCheck(selectedView==null&&combat.hand.Contains(denied)&&!combatBusy,"Click focuses without selecting or accidentally playing");
            HandleCombatPointer(EnemyDropZone.center,true,true,false);HandleCombatPointer(EnemyDropZone.center,false,false,true);
            CombatCheck(combat.cardsPlayed==0&&combat.energy==3,"Clicking the target cannot resolve a card without a drag");
            HandleCombatPointer(point,true,true,false);HandleCombatPointer(EnemyDropZone.center,false,true,false);HandleCombatPointer(EnemyDropZone.center,false,false,true);yield return WaitForCombatQueue();
            CombatCheck(combat.cardsPlayed==1&&combat.energy==2,"A deliberate click-drag-release resolves exactly once");
            QueueEndTurn();CombatCheck(combatBusy,"End Turn queues enemy action");yield return WaitForCombatQueue();
            CombatCheck(combat.turn==2&&combat.energy==3&&combat.hand.Count==5&&combat.phase==CombatPhase.Player,"Enemy queue returns to a fresh playable turn");
            QueueEndTurn();yield return WaitForCombatQueue();QueueEndTurn();yield return WaitForCombatQueue();
            CombatCheck(combat.turn==4&&combat.hand.Concat(combat.draw).Concat(combat.discard).Concat(combat.exhaust).Select(c=>c.instanceId).Distinct().Count()==14,"Repeated turns shuffle without lost or duplicated cards");
            yield return RunUpgradeHitFeedbackChecks();
            yield return CheckBindingClaspDrag();
            Debug.Log($"[Gilded Fate Interaction] {combatInteractionChecks} checks · {combatInteractionFailures} failures · real pointer routing, queue and card conservation");
        }
        private IEnumerator RunUpgradeHitFeedbackChecks()
        {
            var owned=GameContent.Upgrade(GameContent.Find("battle_rush"));owned.permanentDamageBonus=4;
            PrepareCardInspection(owned);CombatCheck(inspectionShowUpgrade,"An upgraded owned card initially inspects its actual upgrade");
            SelectInspectionVersion(false);CombatCheck(!inspectionShowUpgrade&&inspectionBase.exhaust,"Normal comparison restores Battle Rush's Exhaust in the preview");
            SelectInspectionVersion(true);CombatCheck(inspectionShowUpgrade&&!inspectionUpgrade.exhaust&&owned.upgraded&&owned.permanentDamageBonus==4,"Upgrade comparison is reusable and leaves the owned copy unchanged");
            CombatCheck(ReadableCardTitleColor(owned)==ReadableCardTitleColor(GameContent.Find("battle_rush"))&&CardTitleRarityColor(owned)==CardTitleRarityColor(GameContent.Find("battle_rush")),"Normal upgraded cards do not retain comparison-green text or change rarity");
            PrepareCombatCheck("flurry",5);yield return new WaitForSecondsRealtime(.85f);
            var before=combat.enemy.hp;var visible=new System.Collections.Generic.List<int>();var times=new System.Collections.Generic.List<float>();
            var start=CardPickPoint(2);HandleCombatPointer(start,true,true,false);HandleCombatPointer(EnemyDropZone.center,false,true,false);HandleCombatPointer(EnemyDropZone.center,false,false,true);
            var last=displayedEnemyHp;var deadline=Time.unscaledTime+7;var sawIntermediate=false;
            while(combatBusy&&Time.unscaledTime<deadline)
            {
                if(displayedEnemyHp!=last){last=displayedEnemyHp;visible.Add(last);times.Add(Time.unscaledTime);if(last>combat.enemy.hp)sawIntermediate=true;}
                yield return null;
            }
            if(displayedEnemyHp!=last){visible.Add(displayedEnemyHp);times.Add(Time.unscaledTime);}
            CombatCheck(!combatBusy&&visible.Count==GameContent.Find("flurry").hits,"Flurry displays one health step for every individual hit");
            CombatCheck(sawIntermediate&&displayedEnemyHp==combat.enemy.hp&&vitalBeats.Count==0,"Health playback shows intermediate HP then reaches the exact saved result");
            CombatCheck(times.Count>=2&&times.Skip(1).Select((time,i)=>time-times[i]).All(gap=>gap>=.07f),"Multi-hit health beats have a visible short gap");
            Debug.Log("[Gilded Fate Hit Playback] "+before+" → "+string.Join(" → ",visible)+" HP");
            yield return WaitForCombatQueue();
        }
    }
}
