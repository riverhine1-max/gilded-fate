using System.Collections;
using System.Linq;
using GildedFate.Core;
using GildedFate.Combat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private bool ConfigureStatusResonanceCapture(string mode)
        {
            if(mode is not ("status-input" or "status-family" or "curse-family" or "resonance-high" or "mirror-readable"))return false;
            PrepareCombatCheck("ward",5);combatTestInput=true;AudioListener.pause=true;
            if(mode=="resonance-high"){run.hero=combat.hero=HeroId.Hexer;combat.resonance=int.MaxValue;RestoreCombatPresentation();}
            else{screen=ScreenMode.Collection;viewingRunDeck=false;collectionRelics=false;collectionFilter=mode=="curse-family"?6:5;collectionSort=2;collectionScroll=0;}
            if(mode=="mirror-readable"){inspectedCard=GameContent.Cards.First(c=>c.text.Contains("Mirror Sigil"));}
            return true;
        }
        private IEnumerator RunStatusResonanceChecks()
        {
            yield return new WaitForSecondsRealtime(.5f);
            var pad=InputSystem.AddDevice<Gamepad>();var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                foreach(var filter in Enumerable.Range(0,CardArchive.Tabs.Length))
                {
                    collectionScroll=100;screenControllerIndex=7;SelectCollectionFilter(filter);
                    CombatCheck(collectionScroll==0&&screenControllerIndex==0,"Mouse tab callback resets scroll and focus: "+filter);
                    var entries=CollectionCardEntries(false);
                    CombatCheck(entries.All(c=>CardArchive.Matches(c,filter)),"Actual archive entries obey "+CardArchive.Tabs[filter]);
                    PressXboxCheck(pad,GamepadButton.A);CombatCheck(inspectedCard==entries[0],"Xbox selects the first card in "+CardArchive.Tabs[filter]);
                    PressXboxCheck(pad,GamepadButton.B);PressXboxCheck(pad,GamepadButton.RightShoulder);
                    CombatCheck(collectionFilter==(filter+1)%7,"Xbox shoulders reach all seven tabs");
                }
                collectionFilter=4;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E));InputSystem.Update();DispatchMenuCheck(ReadMenuNavigation());
                CombatCheck(collectionFilter==5,"Keyboard reaches dedicated Status tab");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));InputSystem.Update();DispatchMenuCheck(ReadMenuNavigation());
                CombatCheck(IsStatusCard(inspectedCard),"Keyboard opens a Status inspection");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();inspectedCard=null;
                foreach(var card in GameContent.Cards.Where(IsStatusCard))
                {
                    CombatCheck(StatusEngraving(card.id).Length>2,"Individual Status motif: "+card.id);
                    foreach(var width in new[]{140f,190f,310f})
                    {
                        var rect=new Rect(0,0,width,width*1.48f);var fit=ReadableCardRules(rect,card);var area=CardRuleArea(rect);
                        CombatCheck(fit.Height<=area.height&&fit.widest<=area.width,"Status text fits: "+card.id+" at "+width);
                    }
                }
                var formatted=FormatCardRules("Mirror Sigil and Sigil",GameContent.Find("ward"));
                CombatCheck(formatted.Contains("#F2F5EE>Mirror Sigil")&&formatted.Contains("#B99AFF>Sigil"),"Mirror and normal Sigil remain distinct in rich text");
                PrepareCombatCheck("ward",5);run.hero=combat.hero=HeroId.Hexer;combat.resonance=999999999;RestoreCombatPresentation();
                yield return WaitForCombatQueue();yield return new WaitForEndOfFrame();
                CombatCheck(resonanceTarget==999999999,"HUD preserves the exact large integer");
                CombatCheck(combatHudTargets.Count(t=>t.key=="hero:resonance")==1,"One dedicated Hexer resource target");
                OpenCombatHudFocus();combatHudFocusKey="hero:resonance";
                CombatCheck(combatHudTargets[CombatHudFocusIndex].detail.Contains("No stack cap"),"Xbox resource focus exposes explanatory tooltip");
                combatHudInspectActive=false;controllerNavigation=false;combatPointer=HexerResonanceArea.center;
                CombatCheck(CombatInspectionAllowed&&HexerResonanceArea.Contains(combatPointer),"Mouse resource hover uses the same visible bounds");
                combat.resonance=10000;RestoreCombatPresentation();combat.PlayFree(GameContent.Find("kindle"));handReadyAt=Time.unscaledTime+ConsumeCombatEvents(combat.TakeEvents());
                CombatCheck(resonanceBeats.Any(b=>b.value==10001),"Gain schedules exact resource receipt");
                yield return WaitForCombatQueue();
                CombatCheck(resonanceTarget==10001,"Gain receipt reaches displayed resource");
                combat.PlayFree(GameContent.Find("resonant_flame"));handReadyAt=Time.unscaledTime+ConsumeCombatEvents(combat.TakeEvents());
                CombatCheck(resonanceBeats.Any(b=>b.value==9998),"Spend schedules exact resource receipt");
                yield return WaitForCombatQueue();
                CombatCheck(resonanceTarget==9998,"Spend receipt reaches displayed resource");
                foreach(var value in new[]{0,6,999,10000,999999999,int.MaxValue})
                {resonanceTarget=value;var area=HexerResonanceArea;CombatCheck(area.xMax<CombatWidth&&area.width-44>=value.ToString().Length*14,"Exact resource digits fit: "+value);}
                ConfigureStatusResonanceCapture("status-family");
            }
            finally{InputSystem.RemoveDevice(pad);InputSystem.RemoveDevice(keyboard);}
        }
    }
}
