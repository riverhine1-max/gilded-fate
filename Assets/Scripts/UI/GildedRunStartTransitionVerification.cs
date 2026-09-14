using System.Collections;
using GildedFate.Core;
using GildedFate.Saving;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private bool ConfigureRunStartCapture(string mode)
        {
            if(!mode.StartsWith("run-start-"))return false;
            combatTestInput=true;AudioListener.pause=true;screen=ScreenMode.CharacterSelect;
            selectingNewRun=true;selectedHero=mode.Contains("hexer")?HeroId.Hexer:mode.Contains("reaper")?HeroId.Reaper:HeroId.Vanguard;
            heroSelectionTime=Time.unscaledTime-1;profile.reduceFlashing=false;profile.reduceMotion=false;profile.reducedVfx=false;
            if(mode!="run-start-input")
            {
                BeginRunStartTransition();runStartCaptureFrozen=true;
                runStartElapsed=RunStartDuration(selectedHero)*(mode.Contains("impact")?.22f:mode.Contains("resolve")?.64f:selectedHero==HeroId.Vanguard?.34f:selectedHero==HeroId.Hexer?.43f:.42f);
            }
            return true;
        }
        private IEnumerator RunStartTransitionChecks()
        {
            runStartCaptureFrozen=true;
            PrepareRunStartMaterial();CombatCheck(runStartVeilMaterial&&runStartVeilMaterial.shader.isSupported,"Premium character veil shader loads and is supported");
            foreach(var hero in new[]{HeroId.Vanguard,HeroId.Hexer,HeroId.Reaper})
            {
                foreach(var reduced in new[]{false,true})
                {
                    profile.reduceMotion=reduced;profile.reduceFlashing=reduced;profile.reducedVfx=reduced;profile.fastMode=reduced;
                    runStartActive=false;screen=ScreenMode.CharacterSelect;selectingNewRun=true;selectedHero=hero;
                    var before=JsonUtility.ToJson(run);var count=profile.runsPlayed;
                    ConfirmSelectedHero();var duration=RunStartDuration(hero);
                    CombatCheck(runStartActive&&screen==ScreenMode.CharacterSelect,"Run start owns selection before load: "+hero+" reduced="+reduced);
                    CombatCheck(duration>=.5f&&duration<=.9f,"Character transition stays within duration budget: "+hero);
                    for(var i=0;i<6;i++)
                    {
                        ConfirmSelectedHero();DispatchMenuCheck(new MenuNavigation{accept=true,back=true,pause=true,map=true,deck=true,x=1});
                        HandleLegacyMenuNavigation(new MenuNavigation{accept=true,back=true,x=1});
                    }
                    CombatCheck(runStartHero==hero&&selectedHero==hero&&profile.runsPlayed==count&&screen==ScreenMode.CharacterSelect,"Confirm spam and cancellation cannot skip, switch hero, or duplicate start: "+hero);
                    AdvanceRunStart(duration*.5f);
                    CombatCheck(JsonUtility.ToJson(run)==before,"Existing run is untouched during the effect: "+hero);
                    AdvanceRunStart(duration*2);
                    CombatCheck(runStartActive&&!runStartBlackPresented&&profile.runsPlayed==count,"Stalled frame still requires visible black before loading: "+hero);
                    yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
                    CombatCheck(runStartBlackPresented,"Opaque full-screen black was rendered: "+hero);
                    AdvanceRunStart(0);
                    CombatCheck(!runStartActive&&screen==ScreenMode.Fateweave&&run.hero==hero,"Starts chosen hero through existing Fateweave flow: "+hero);
                    CombatCheck(profile.runsPlayed==count+1,"Run counted exactly once: "+hero);
                    var saved=SaveService.Load();CombatCheck(saved!=null&&saved.hero==hero,"New character run persists after transition: "+hero);
                    AdvanceRunStart(1);CombatCheck(profile.runsPlayed==count+1,"Completed transition cannot commit twice: "+hero);
                }
            }
            selectingNewRun=false;screen=ScreenMode.CharacterSelect;ConfirmSelectedHero();
            CombatCheck(!runStartActive&&screen==ScreenMode.Collection,"Character archive never triggers run-start animation");
            // Exercise real Update timing, including a paused gameplay clock.
            var priorScale=Time.timeScale;
            foreach(var hero in new[]{HeroId.Vanguard,HeroId.Hexer,HeroId.Reaper})
            {
                runStartCaptureFrozen=false;Time.timeScale=0;selectedHero=hero;screen=ScreenMode.CharacterSelect;selectingNewRun=true;
                ConfirmSelectedHero();var started=Time.realtimeSinceStartup;var deadline=started+3;
                while(runStartActive&&Time.realtimeSinceStartup<deadline)yield return null;
                var elapsed=Time.realtimeSinceStartup-started;
                CombatCheck(!runStartActive&&screen==ScreenMode.Fateweave,"Unscaled real-frame transition completes while gameplay clock is paused: "+hero);
                CombatCheck(elapsed>=.5f&&elapsed<1.15f,"Measured real-frame timing including black handoff: "+hero+" "+elapsed.ToString("F3")+"s");
            }
            Time.timeScale=priorScale;
            runStartCaptureFrozen=false;profile.fastMode=false;screen=ScreenMode.CharacterSelect;selectingNewRun=true;transitionAlpha=0;
        }
    }
}
