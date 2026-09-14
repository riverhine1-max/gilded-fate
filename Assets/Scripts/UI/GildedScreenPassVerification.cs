using System.Collections;
using System.Linq;
using GildedFate.Core;
using GildedFate.Map;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private bool ConfigureScreenPassCapture(string mode)
        {
            if(mode is not ("screens-input" or "screens-binding" or "screens-pause" or "screens-settings"))return false;
            combatTestInput=true;AudioListener.pause=true;run.NewRun(HeroId.Vanguard,20260911);
            run.BeginBindingChoice();run.SelectBinding(run.bindingOffers[0]);screen=controllerScreen=ScreenMode.BindingCard;
            if(mode=="screens-pause"){screen=ScreenMode.Reward;runPauseOpen=true;}
            if(mode=="screens-settings"){screen=ScreenMode.Settings;settingsOverview=true;settingsFocusIndex=0;}
            return true;
        }
        private IEnumerator RunScreenPassChecks()
        {
            yield return new WaitForSecondsRealtime(.3f);
            foreach(var ev in WorldContent.Events)
            {
                var height=Mathf.Min(190,(810-164-12*Mathf.Max(0,ev.choices.Length-1))/Mathf.Max(1,ev.choices.Length));
                foreach(var choice in ev.choices)
                {
                    var area=EventDecisionArea(new Rect(749,116,663,height));var text=FormatCardRules(EventDecisionSentence(choice));
                    var style=FittedEventStyle(text,area,footerStyle,18,16);style.richText=true;
                    CombatCheck(style.fontSize>=16&&style.CalcHeight(new GUIContent(text),area.width)<=area.height+.5f,"Decision sentence fits: "+ev.id+" / "+choice.id);
                }
            }
            foreach(var word in new[]{"Dissipate","Dissipates","Dissipated","Dissipating","Soulbind","Discarded","Retained"})
                CombatCheck(FormatCardRules(word).Contains(">"+word+"</color>"),"Complete keyword colored: "+word);
            var pad=InputSystem.AddDevice<Gamepad>();var keys=InputSystem.AddDevice<Keyboard>();
            try
            {
                var before=JsonUtility.ToJson(run.cards.Select(c=>c.BuildDefinition().id).ToArray());var count=run.cards.Count;
                PressXboxCheck(pad,GamepadButton.B);CombatCheck(screen==ScreenMode.BindingSelect&&run.pendingBindingId=="","Xbox Back returns to Binding choice without attachment");
                PressXboxCheck(pad,GamepadButton.B);CombatCheck(screen==ScreenMode.Sanctuary&&run.cards.Count==count,"Xbox Back cancels Binding without losing cards");
                run.BeginBindingChoice();screen=ScreenMode.BindingSelect;CombatCheck(CancelBindingScreen()&&screen==ScreenMode.Sanctuary,"Mouse Cancel callback leaves Binding choice safely");
                run.BeginBindingChoice();run.SelectBinding(run.bindingOffers[0]);screen=ScreenMode.BindingCard;
                InputSystem.QueueStateEvent(keys,new KeyboardState(Key.Escape));InputSystem.Update();DispatchMenuCheck(ReadMenuNavigation());
                CombatCheck(screen==ScreenMode.BindingSelect,"Escape goes back one Binding level");InputSystem.QueueStateEvent(keys,new KeyboardState());InputSystem.Update();
                run.SelectBinding(run.bindingOffers[0]);screen=ScreenMode.BindingCard;var card=run.BindingEligibleCards().First();
                CombatCheck(run.ApplyPendingBinding(card)&&!CancelBindingScreen(),"Committed Binding cannot be canceled or undone");
                acquisitionActive=false;inspectedCard=null;inspectedRelic=null;
                foreach(var page in new[]{ScreenMode.Event,ScreenMode.Reward,ScreenMode.Merchant,ScreenMode.Sanctuary,ScreenMode.Fateweave,ScreenMode.Treasure})
                {
                    screen=page;runPauseOpen=false;DispatchMenuCheck(new MenuNavigation{pause=true,back=true});
                    CombatCheck(runPauseOpen&&screen==page&&!CanInspectRoute,"Pause is canonical and blocks route changes: "+page);
                    ActivatePauseMenu(1);CombatCheck(screen==ScreenMode.Settings,"Pause opens settings: "+page);
                    ChangeSettingsPage(1);BackFromSettings();CombatCheck(screen==ScreenMode.Settings&&settingsOverview,"Subsection Back returns to Settings: "+page);
                    BackFromSettings();CombatCheck(screen==page&&runPauseOpen,"Settings returns to prior pause: "+page);
                    ResumePauseMenu();CombatCheck(screen==page&&!runPauseOpen,"Resume returns to prior screen: "+page);
                }
                ConfigureMapShopCapture("merchant-full");merchantHealed=false;run.hp=run.maxHp;var money=run.gold;BuyShopHeal();
                CombatCheck(run.gold==money&&!merchantHealed,"Full-health purchase rejected without charge");
                run.hp=run.maxHp-30;BuyShopHeal();CombatCheck(run.hp==run.maxHp-12&&run.gold==money-35&&healthServiceMessage=="+18 HP","Shop heals exactly 18 for 35 with immediate feedback");
                BuyShopHeal();CombatCheck(run.gold==money-35,"Healing service cannot be bought twice");
                merchantHealed=false;run.hp=run.maxHp-3;BuyShopHeal();CombatCheck(run.hp==run.maxHp&&healthServiceMessage=="+3 HP","Heal feedback reports capped actual gain");
                screen=ScreenMode.Sanctuary;run.hp=1;var expected=1+Mathf.RoundToInt(run.maxHp*.3f);RestAtShrine();
                CombatCheck(run.hp==expected&&screen==ScreenMode.Map,"Shrine heals 30 percent immediately and returns to map");
                var icon=FittedServiceIcon(new Rect(0,0,66,76),60);CombatCheck(icon.width==icon.height&&icon.center.x==33,"Service art remains centered and square");
                ConfigureScreenPassCapture("screens-binding");
            }
            finally{InputSystem.RemoveDevice(pad);InputSystem.RemoveDevice(keys);}
        }
    }
}
