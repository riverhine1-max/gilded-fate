using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Audio;
using GildedFate.Combat;
using GildedFate.Core;
using GildedFate.Map;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private bool finalTooltipProbe;
        private string FinalTooltipText=>string.Join("\n\n",RuleKeywords.Select(k=>"<b><color=#"+k.hex+">"+k.title+"</color></b>\n"+k.detail));
        private void DrawFinalPolishProbe(float w,float h)
        {
            if(captureMode&&finalTooltipProbe)DrawTooltip(new Rect(w-378,116,350,200),"KEYWORD INSPECTION",FinalTooltipText);
        }
        private bool ConfigureFinalPolishCapture(string mode)
        {
            if(!mode.StartsWith("polish-final-"))return false;
            combatTestInput=true;AudioListener.pause=true;controllerNavigation=true;menuUsesGamepad=true;
            profile.tooltips=true;profile.fastMode=false;profile.reduceMotion=false;profile.cardAnimationSpeed=1;
            if(mode is "polish-final-merchant" or "polish-final-tooltip")
            {
                ConfigureMapShopCapture("merchant-full");run.merchantCardIds[0]="ward";screenControllerIndex=0;controllerScreen=screen;
                finalTooltipProbe=mode.EndsWith("tooltip");return true;
            }
            run.NewRun(HeroId.Hexer,20260909);currentNode=run.nodes.First(n=>n.kind==NodeKind.Event);
            if(mode=="polish-final-fateweave"){run.act=2;run.BeginFateweave();screen=ScreenMode.Fateweave;screenControllerIndex=1;}
            else
            {
                currentEvent=EventContent.Find("hungry_chest")??WorldContent.Events.First(e=>e.ambientCue=="chest");
                EventSystem.BeginEvent(run,currentEvent);screen=ScreenMode.Event;screenControllerIndex=1;
            }
            controllerScreen=screen;return true;
        }
        private IEnumerator RunFinalPolishChecks()
        {
            combatTestInput=true;AudioListener.pause=true;
            yield return new WaitForSecondsRealtime(.3f);
            // Every authored choice, not just the gallery example, must fit its
            // dedicated title/cost/reward regions at the supported reference size.
            foreach(var ev in WorldContent.Events)
            {
                var count=ev.choices.Length;var height=Mathf.Min(190,(810-164-12*Mathf.Max(0,count-1))/Mathf.Max(1,count));
                var rect=new Rect(749,116,663,height);var parts=EventChoiceLayout(rect);
                foreach(var choice in ev.choices)
                {
                    var title=FittedEventStyle(choice.title,parts[0],titleStyle,22,17);
                    var cost=FittedEventStyle(choice.costText,parts[1],footerStyle,14,13);
                    var rewardRect=parts[3];if((choice.rewardText??"").ToUpperInvariant().Contains("GOLD"))rewardRect.width-=32;
                    var reward=FittedEventStyle(choice.rewardText,rewardRect,footerStyle,17,14);
                    CombatCheck(title.CalcHeight(new GUIContent(choice.title),parts[0].width)<=parts[0].height+.5f,"Event title fits: "+ev.id+" / "+choice.id);
                    CombatCheck(cost.CalcHeight(new GUIContent(choice.costText),parts[1].width)<=parts[1].height+.5f,"Event cost fits: "+ev.id+" / "+choice.id);
                    CombatCheck(reward.CalcHeight(new GUIContent(choice.rewardText),rewardRect.width)<=rewardRect.height+.5f,"Event reward fits without tiny text: "+ev.id+" / "+choice.id);
                    CombatCheck(parts[3].yMax<=rect.yMax&&parts[1].yMax<parts[3].y,"Event sections never overlap: "+ev.id+" / "+choice.id);
                }
            }
            foreach(var binding in WorldContent.Bindings)
            {
                var card=GameContent.Find("strike").Copy();card.specialModification=binding.id;card.specialModificationKind=SpecialModificationKind.Binding;
                var attachment=CardAttachments(card).FirstOrDefault(a=>a.binding);
                CombatCheck(attachment!=null&&attachment.tile>=0&&attachment.tile<16&&attachment.detail.Contains(binding.text)&&attachment.detail.Contains("this run"),"Binding identity, effect and duration: "+binding.id);
            }
            ConfigureFinalPolishCapture("polish-final-merchant");yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
            CombatCheck(hoveredCardHelp?.id=="ward","Controller-focused merchant card exposes its keyword glossary");
            var money=run.gold;var deck=run.cards.Count;
            DispatchMenuCheck(new MenuNavigation{inspect=true});
            CombatCheck(inspectedCard!=null&&run.gold==money&&run.cards.Count==deck,"Merchant inspection cannot purchase");
            DispatchMenuCheck(new MenuNavigation{back=true});
            DispatchMenuCheck(new MenuNavigation{accept=true});var afterPurchase=run.gold;
            CombatCheck(run.cards.Count==deck+1&&afterPurchase==money-RunModel.MerchantCardPrice(GameContent.Find("ward")),"Controller purchase uses the exact shared card price");
            DispatchMenuCheck(new MenuNavigation{accept=true});
            CombatCheck(run.cards.Count==deck+1&&run.gold==afterPurchase,"Controller cannot duplicate a purchase while it travels");
            yield return new WaitForSecondsRealtime(1.8f);
            finalTooltipProbe=true;profile.tooltips=true;controllerNavigation=true;
            yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
            CombatCheck(lastTooltipBounds.width>0&&lastTooltipBounds.xMax<=CombatWidth&&lastTooltipBounds.yMax<=CombatHeight,"Long non-combat tooltip stays on screen");
            var pad=InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(pad,new GamepadState{rightStick=new Vector2(0,-1)});
            yield return new WaitForSecondsRealtime(.3f);
            CombatCheck(scrollingTooltipPosition.y>0,"Right stick scrolls a long non-combat tooltip");
            InputSystem.QueueStateEvent(pad,new GamepadState{rightStick=new Vector2(0,1)});
            var scrollDeadline=Time.realtimeSinceStartup+2f;
            while(scrollingTooltipPosition.y>.1f&&Time.realtimeSinceStartup<scrollDeadline)yield return new WaitForEndOfFrame();
            CombatCheck(scrollingTooltipPosition.y<=.1f,"Reverse right-stick scroll clamps at the top");
            CombatCheck(Mathf.Abs(TooltipStickScrollDelta(1,1)+16.5f)<.001f&&TooltipStickScrollDelta(-1,0)==0,"A slow frame cannot jump the tooltip scroll position");
            InputSystem.RemoveDevice(pad);
            profile.tooltips=false;lastTooltipBounds=Rect.zero;yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
            CombatCheck(lastTooltipBounds==Rect.zero&&!TooltipsEnabled,"Global tooltip setting also disables HUD and long inspection help");
            finalTooltipProbe=false;profile.tooltips=true;
            CombatCheck(TooltipScrollValue(5,-99,200)==0&&TooltipScrollValue(5,999,200)==200,"Tooltip scrolling clamps both ends");
            run.NewRun(HeroId.Vanguard,20260909);currentEvent=EventContent.Find("abandoned_forge");EventSystem.BeginEvent(run,currentEvent);
            screen=controllerScreen=ScreenMode.Event;screenControllerIndex=0;var beforeEvent=JsonUtility.ToJson(run);
            DispatchMenuCheck(new MenuNavigation{x=1});
            CombatCheck(screenControllerIndex==1&&JsonUtility.ToJson(run)==beforeEvent,"Event focus is read-only");
            DispatchMenuCheck(new MenuNavigation{x=1});var gold=run.gold;
            DispatchMenuCheck(new MenuNavigation{accept=true});
            CombatCheck(screen==ScreenMode.EventResult&&run.gold==gold+45,"Event controller confirms its exact displayed outcome");
            run.NewRun(HeroId.Hexer,20260909);run.act=1;run.BeginFateweave();run.fateweaveOffers.Clear();run.fateweaveOffers.Add("foreign_memory");
            screen=controllerScreen=ScreenMode.Fateweave;screenControllerIndex=0;
            var selections=run.fateweaveSelections.Count;DispatchMenuCheck(new MenuNavigation{accept=true});
            CombatCheck(acquisitionActive&&acquisitionKind==AcquisitionKind.Fateweave,"Fateweave confirm starts the existing strand-pull sequence");
            DispatchMenuCheck(new MenuNavigation{accept=true});
            CombatCheck(run.fateweaveSelections.Count==selections+1,"Repeated Fateweave confirmation cannot duplicate selection");
            yield return new WaitForSecondsRealtime(1.6f);
            CombatCheck(screen==ScreenMode.FateweaveCard&&run.pendingCardOfferIds.Count>0,"Fateweave pull arrives at its real reward selection");
            var chosenId=run.pendingCardOfferIds[0];var cardsBefore=run.cards.Count;
            DispatchMenuCheck(new MenuNavigation{accept=true});yield return new WaitForSecondsRealtime(1.4f);
            CombatCheck(run.cards.Count==cardsBefore+1&&run.cards.Last().cardId==chosenId,"Fateweave reward adds only the chosen physical card");
            foreach(var label in new[]{"SOUL","CURSE","STATUS"})
                CombatCheck(CardArrivalSound(new CombatEvent(CombatEventKind.Draw,label:label),true)!=(label=="SOUL"?SoundCue.Debuff:SoundCue.CardDraw),"Generated-card audio has a semantic family: "+label);
            // Real, high-density mixed engine: no counterfeit receipts or changes
            // to the production definitions. Large HP is local to this QA fixture.
            PrepareCombatCheck("soul",5);yield return WaitForCombatQueue();
            combat.enemy.hp=combat.enemy.maxHp=1000000;combat.player.hp=combat.player.maxHp=1000000;combat.energy=10000;
            combat.hand.Clear();combat.draw.Clear();combat.discard.Clear();combat.exhaust.Clear();combat.TakeEvents();
            combat.sigils.AddRange(new[]{SigilKind.Ember,SigilKind.Hex,SigilKind.Echo,SigilKind.Ruin,SigilKind.Wither,SigilKind.Mirror});combat.memory.extraSigilSlots=3;
            void PlayFixture(string id)
            {
                var card=GameContent.Find(id).Copy();card.instanceId=++combat.nextInstanceId;combat.hand.Add(card);
                if(!combat.Play(card))throw new System.InvalidOperationException("Stress fixture could not play "+id);
            }
            foreach(var id in new[]{"endless_harvest","grim_ascension","soulbound_tome","eternal_souls"})PlayFixture(id);
            combat.TakeEvents();RestoreCombatPresentation();var maxMs=0d;var totalFacts=0;var peakMotions=0;
            for(var round=0;round<3;round++)
            {
                PlayFixture("army_of_the_dead");
                foreach(var soul in combat.hand.Where(c=>c.id=="soul").ToArray())combat.Play(soul);
                PlayFixture("perfect_ritual");PlayFixture("final_procession");
                var facts=combat.TakeEvents();totalFacts+=facts.Length;var state=JsonUtility.ToJson(combat.CaptureCheckpoint());
                var watch=System.Diagnostics.Stopwatch.StartNew();var duration=ConsumeCombatEvents(facts);watch.Stop();maxMs=System.Math.Max(maxMs,watch.Elapsed.TotalMilliseconds);peakMotions=Mathf.Max(peakMotions,cardMotions.Count);
                CombatCheck(duration<=8.5f,"Dense Soul/Sigil sequence has a bounded presentation duration, round "+round);
                CombatCheck(JsonUtility.ToJson(combat.CaptureCheckpoint())==state,"Dense feedback cannot resolve gameplay a second time, round "+round);
                yield return new WaitForSecondsRealtime(duration+.55f);
                CombatCheck(cardMotions.Count==0&&powerPulseBeats.Count==0&&relicPulseBeats.Count==0,"Dense engine releases its flights and source pulses, round "+round);
                CombatCheck(playerStatusBeats.Count==0&&enemyStatusBeats.Count==0&&statusVisualBeats.Count==0,"Dense engine drains per-owner status playback, round "+round);
            }
            CombatCheck(totalFacts>200,"Mixed engine stress exercises hundreds of real combat receipts");
            CombatCheck(GameAudio.PendingCount==0&&GameAudio.VoiceCount==24,"Dense engine drains delayed audio and keeps the fixed voice budget");
            Debug.Log($"[Gilded Fate Final Stress] {totalFacts} real receipts; peak {peakMotions} card motions; worst scheduling {maxMs:F2} ms. This is not a hardware FPS benchmark.");
            ConfigureFinalPolishCapture("polish-final-event");
            Debug.Log($"[Gilded Fate Final Polish] {combatInteractionChecks} checks · {combatInteractionFailures} failures");
        }
    }
}
