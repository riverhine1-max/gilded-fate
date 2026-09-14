using System.Collections;
using System.Linq;
using GildedFate.Combat;
using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private void PrepareAttachmentGallery()
        {
            PrepareCombatCheck("scythe_strike",5);combatTestInput=true;
            profile.fastMode=false;profile.reduceMotion=false;
            var card=combat.hand[2];card.perfected=true;card.perfectedGrowth=4;
            card.specialModificationKind=SpecialModificationKind.Binding;card.specialModification="serrated";
            var m=combat.memory.remaining;var rm=combat.memory.relicExpansion;
            m.boundIds.Add(card.instanceId);m.boundRates.Add(2);m.boundBonuses.Add(4);
            m.reflectedIds.Add(card.instanceId);m.imprintCosts.Add(card.cost);m.imprintDamage.Add(5);
            rm.destined=card.instanceId;rm.unwritten.Add(card.instanceId);rm.foresightCards.Add(card.instanceId);
            combat.TakeEvents();RestoreCombatPresentation();cardAttachmentCache.Clear();
        }
        private IEnumerator RunPresentationReceiptChecks()
        {
            PrepareAttachmentGallery();yield return WaitForCombatQueue();yield return new WaitForSecondsRealtime(.3f);
            var card=combat.hand[2];var before=JsonUtility.ToJson(combat.CaptureCheckpoint());
            var attachments=CardAttachments(card);
            foreach(var title in new[]{"PERFECTED ATTACK","SOULBOUND","UNWRITTEN","DESTINED","FORESIGHT","ECHOED","IMPRINTED COST 1"})
                CombatCheck(attachments.Any(a=>a.title==title),"Physical card has its own attachment: "+title);
            CombatCheck(attachments.All(a=>a.detail.Contains("Source:")&&a.detail.Contains("Duration:")),"Every attachment explains its effect, source and duration");
            foreach(var a in attachments.Where(a=>!a.binding))CombatCheck(LoadAuthoredArt(a.resource)!=null,"Attachment uses available artwork: "+a.title);
            CombatCheck(attachments.Count>6,"Dense attachment stack uses a bounded overflow badge");
            var rectangle=new Rect(100,200,194,264);
            CombatCheck(CardAttachmentHelp(rectangle,card,CardAttachmentRect(rectangle,1).center,out var badgeTitle,out var badgeDetail)&&badgeTitle=="PERFECTED ATTACK"&&!badgeDetail.Contains("Soul Infusion"),"Hovering one badge explains only that modification");
            CombatCheck(CardAttachmentHelp(rectangle,card,CardAttachmentRect(rectangle,5).center,out badgeTitle,out badgeDetail)&&badgeTitle=="MORE CARD EFFECTS"&&badgeDetail.Contains("Gilded Imprint"),"Overflow badge retains hidden effect descriptions");
            var local=new Rect(-HandLayout.CardWidth*.5f,-HandLayout.CardHeight*.5f,HandLayout.CardWidth,HandLayout.CardHeight);
            foreach(var angle in new[]{-12f,0f,12f})foreach(var scale in new[]{.7f,1f,1.18f})
            {
                var origin=new Vector2(CombatWidth*.5f,CombatHeight*.5f);
                for(var i=0;i<6;i++)
                {
                    var center=CardAttachmentRect(local,i).center;var world=(Vector2)(Quaternion.Euler(0,0,angle)*(Vector3)(center*scale))+origin;
                    CombatCheck(HandCardContains(world,card,origin.x,origin.y,angle,scale),"Attachment hover follows rotation and scale: "+i);
                }
                CombatCheck(!HandCardContains(origin+Vector2.left*300,card,origin.x,origin.y,angle,scale),"Empty space does not hover a card");
            }
            CombatCheck(CardAttachments(GameContent.Find(card.id)).Count==0,"Collection definition does not inherit combat-copy enchantments");
            CombatCheck(JsonUtility.ToJson(combat.CaptureCheckpoint())==before,"Attachments and hover checks never mutate combat");
            foreach(var width in new[]{1440f,2304f,2880f})
            {
                acquisitionKind=AcquisitionKind.Card;CombatCheck(AcquisitionTarget(width)==RunDeckControlRect(width).center,"Card reward lands on Deck at width "+width);
                run.relics.Clear();run.relics.AddRange(GameContent.Relics.Select(r=>r.id));acquisitionKind=AcquisitionKind.Relic;acquisitionRelic=GameContent.Relics[2];
                CombatCheck(AcquisitionTarget(width)==RunRelicSlotRect(2).center,"Relic lands beneath the main bar");
                acquisitionRelic=GameContent.Relics.Last();var target=AcquisitionTarget(width);
                CombatCheck(target==RunRelicSlotRect(VisibleRunRelics(width)).center&&target.x<width-16,"Overflow reward stays on-screen and inspectable");
            }
            acquisitionKind=AcquisitionKind.None;acquisitionRelic=null;
            var finishCount=0;var perfected=GameContent.Find("strike").Copy();perfected.perfected=true;
            BeginModificationAcquisition(perfected,()=>finishCount++,perfected:true);
            CombatCheck(acquisitionActive&&acquisitionPerfected,"Perfected-only card actually starts acquisition feedback");
            BeginModificationAcquisition(perfected,()=>finishCount++,perfected:true);
            yield return new WaitForSecondsRealtime(1.6f);
            CombatCheck(finishCount==1&&!acquisitionActive,"Repeated input cannot duplicate the acquisition callback");
            PrepareCombatCheck("army_of_the_dead",5);combatTestInput=true;yield return WaitForCombatQueue();
            var army=combat.hand[2];combat.TakeEvents();CombatCheck(combat.Play(army),"Army resolves before presentation");var facts=combat.TakeEvents();
            var settled=JsonUtility.ToJson(combat.CaptureCheckpoint());var duration=ConsumeCombatEvents(facts,army);
            var souls=facts.Where(f=>f.generatedCard).ToArray();
            CombatCheck(souls.Length==8&&souls.All(f=>handViews.ContainsKey(f.card.instanceId)),"All eight generated Souls receive actual hand views");
            CombatCheck(souls.All(f=>cardMotions.Any(m=>m.card==f.card&&m.to==ReceiptCardTarget(f)&&!m.back)),"Created Souls visibly travel to their own slots");
            CombatCheck(!cardMotions.Any(m=>m.back),"Creating Souls never shows a false discard reshuffle");
            yield return new WaitForSecondsRealtime(duration+.2f);
            CombatCheck(JsonUtility.ToJson(combat.CaptureCheckpoint())==settled,"Creation animation does not replay effects");
            CombatCheck(cardMotions.Count==0,"Completed card flights are released");
            var discardSoul=GameContent.Find("soul").Copy();discardSoul.instanceId=++combat.nextInstanceId;combat.discard.Add(discardSoul);
            var receipt=new CombatEvent(CombatEventKind.Shuffle,1,true,discardSoul,"SOUL"){generatedCard=true,destination=CombatCardDestination.Discard};
            var total=ConsumeCombatEvents(new[]{receipt});
            CombatCheck(cardMotions.Any(m=>m.card==discardSoul&&m.to==PilePoint(1))&&!cardMotions.Any(m=>m.back),"Endless Harvest creation lands in Discard");
            yield return new WaitForSecondsRealtime(total+.2f);
            relicPulseBeats.Clear();var now=Time.unscaledTime;
            ConsumeCombatEvents(new[]{new CombatEvent(CombatEventKind.RelicTrigger,label:"funeral_bell"),new CombatEvent(CombatEventKind.RelicTrigger,label:"funeral_bell")});
            CombatCheck(relicPulseBeats.Count==2&&relicPulseBeats[1].time>relicPulseBeats[0].time,"Repeated triggers keep separate ordered source beats");
            ConsumeCombatEvents(new[]{new CombatEvent(CombatEventKind.Status,0,true,label:"TRIGGER:Grim Ascension")});
            CombatCheck(powerPulseBeats.Any(b=>b.key=="P:GRIM ASCENSION"),"Power trigger identity matches its HUD icon regardless of title casing");
            CombatCheck(RelicPulseGrowth("funeral_bell",now+.10f)>0,"An upcoming trigger does not erase the current relic pulse");
            yield return new WaitForSecondsRealtime(.8f);CombatCheck(relicPulseBeats.Count==0,"Relic pulse receipts are cleaned up");
            PrepareAttachmentGallery();yield return WaitForCombatQueue();yield return new WaitForSecondsRealtime(.3f);
            var view=handViews[combat.hand[2].instanceId];hoverView=view;
            combatPointer=view.position;combatHudInspectActive=false;
            Debug.Log($"[Gilded Fate Presentation] {combatInteractionChecks} checks · {combatInteractionFailures} failures");
        }
        private IEnumerator PrepareReceiptGallery(string mode)
        {
            PrepareAttachmentGallery();yield return WaitForCombatQueue();yield return new WaitForSecondsRealtime(.2f);
            if(mode=="polish-rewards")
            {
                var card=combat.hand[2];BeginModificationAcquisition(card,()=>{},perfected:true);
                acquisitionDuration=30;acquisitionStarted=Time.unscaledTime-12;
            }
            else
            {
                hoverView=handViews[combat.hand[2].instanceId];combatPointer=hoverView.position;
            }
        }
    }
}
