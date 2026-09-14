using System.Collections;
using System.Linq;
using GildedFate.Combat;
using GildedFate.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        // Only the disposable capture player creates this virtual device. No OS
        // input is sent and no physical controller state is modified.
        private void DispatchXboxCheck(Gamepad pad,GamepadState state)
        {
            InputSystem.QueueStateEvent(pad,state);InputSystem.Update();
            var input=ReadMenuNavigation();combatNavigationInput=input;menuInputConsumed=false;
            if(routeInspectionOpen)HandleRouteInspectionNavigation(input);
            else if(!RouteMenuNavigation(input))
            {
                if(screen==ScreenMode.Combat)HandleCombatController();else HandleLegacyMenuNavigation(input);
            }
        }
        private void PressXboxCheck(Gamepad pad,GamepadButton button)
        {
            DispatchXboxCheck(pad,new GamepadState());
            DispatchXboxCheck(pad,new GamepadState().WithButton(button));
            DispatchXboxCheck(pad,new GamepadState());
        }
        private IEnumerator RunXboxInteractionChecks()
        {
            if(!captureMode)yield break;
            var pad=InputSystem.AddDevice<Gamepad>();
            try
            {
                ConfigureShardCapture("group-four");profile.fastMode=true;
                for(var i=0;i<4;i++){combat.EnemyAt(i).hp=combat.EnemyAt(i).maxHp=1000;combat.EnemyAt(i).block=0;}
                combat.hand.Clear();
                foreach(var id in new[]{"strike","defend","living_armor","strike","strike"})
                {var c=GameContent.Find(id).Copy();c.instanceId=++combat.nextInstanceId;combat.hand.Add(c);combat.events.Add(new CombatEvent(CombatEventKind.Draw,1,true,c));}
                combat.energy=99;ResetCombatPresentation();yield return WaitForCombatQueue();yield return new WaitForSecondsRealtime(.3f);
                PressXboxCheck(pad,GamepadButton.DpadRight);
                CombatCheck(menuUsesGamepad&&controllerNavigation&&controllerHandIndex==0&&hoverView!=null&&selectedView==null,"Xbox browse lifts first card without committing it");
                CombatCheck(!CombatInspectionAllowed,"Stationary mouse cannot add enemy/status tooltips over controller card browsing");
                DispatchXboxCheck(pad,new GamepadState{leftStick=Vector2.right});
                CombatCheck(controllerHandIndex==1,"Left stick browses hand through shared deadzone/repeat reader");
                DispatchXboxCheck(pad,new GamepadState{leftStick=Vector2.right});
                CombatCheck(controllerHandIndex==1,"Held stick does not race through cards every frame");
                DispatchXboxCheck(pad,new GamepadState());
                var focus=hoverView.card.instanceId;
                var first=combat.hand[0];combat.hand.RemoveAt(0);combat.discard.Add(first);ReconcileCombatHandFocus();
                CombatCheck(controllerFocusedCardId==focus&&controllerHandIndex==0&&hoverView.card.instanceId==focus,"Removing preceding card retains physical-card focus");
                combat.discard.Remove(first);combat.hand.Insert(0,first);ReconcileCombatHandFocus();
                CombatCheck(controllerFocusedCardId==focus&&controllerHandIndex==1,"Inserting preceding card retains physical-card focus");
                PressXboxCheck(pad,GamepadButton.DpadLeft);
                for(var target=0;target<4;target++)
                {
                    combatTargetIndex=target;var before=combat.energy;
                    PressXboxCheck(pad,GamepadButton.South);
                    CombatCheck(controllerTargeting&&TargetAt(combatPointer)==target&&selectedView?.card.instanceId==first.instanceId,"Selecting attack preserves highlighted target "+target);
                    PressXboxCheck(pad,GamepadButton.LeftShoulder);
                    CombatCheck(controllerTargeting&&!combatHudInspectActive,"HUD shortcut cannot steal active targeting "+target);
                    PressXboxCheck(pad,GamepadButton.East);
                    CombatCheck(!controllerTargeting&&selectedView==null&&hoverView?.card.instanceId==first.instanceId&&combat.energy==before,"B cancels only targeting, retaining card and Energy "+target);
                }
                combatTargetIndex=0;PressXboxCheck(pad,GamepadButton.South);
                PressXboxCheck(pad,GamepadButton.DpadRight);PressXboxCheck(pad,GamepadButton.DpadRight);
                CombatCheck(combatTargetIndex==2&&TargetAt(combatPointer)==2,"D-pad changes both highlighted target and confirmation point");
                var hp=Enumerable.Range(0,4).Select(i=>combat.EnemyAt(i).hp).ToArray();
                PressXboxCheck(pad,GamepadButton.South);yield return WaitForCombatQueue();
                CombatCheck(combat.EnemyAt(2).hp<hp[2]&&Enumerable.Range(0,4).Where(i=>i!=2).All(i=>combat.EnemyAt(i).hp==hp[i]),"Xbox confirm damages exactly the highlighted enemy");
                ReconcileCombatHandFocus();CombatCheck(hoverView?.card.id=="defend","Played card hands focus to its nearest surviving neighbor");
                var block=combat.player.block;PressXboxCheck(pad,GamepadButton.South);yield return WaitForCombatQueue();
                CombatCheck(combat.player.block>block,"Xbox A plays non-targeted Skill without enemy targeting");
                ReconcileCombatHandFocus();CombatCheck(hoverView?.card.id=="living_armor","Focus remains stable after Skill leaves hand");
                PressXboxCheck(pad,GamepadButton.South);yield return WaitForCombatQueue();
                CombatCheck(combat.activeAspects.Any(c=>c.id=="living_armor")&&!combat.exhaust.Any(c=>c.id=="living_armor"),"Xbox A installs Aspect through existing card-to-HUD path");
                yield return null;ReconcileCombatHandFocus();focus=controllerFocusedCardId;
                PressXboxCheck(pad,GamepadButton.LeftShoulder);
                CombatCheck(combatHudInspectActive&&CombatHudFocusIndex>=0,"LB opens real rendered HUD targets");
                var key=combatHudFocusKey;DispatchXboxCheck(pad,new GamepadState{leftStick=Vector2.down});
                CombatCheck(combatHudFocusKey!=key,"Left stick navigates HUD inspection groups");
                DispatchXboxCheck(pad,new GamepadState());PressXboxCheck(pad,GamepadButton.East);ReconcileCombatHandFocus();
                CombatCheck(!combatHudInspectActive&&hoverView?.card.instanceId==focus,"B returns from HUD to the same physical card");
                PressXboxCheck(pad,GamepadButton.West);
                CombatCheck(inspectedCard?.instanceId==focus,"X inspects focused card");
                PressXboxCheck(pad,GamepadButton.East);ReconcileCombatHandFocus();
                CombatCheck(inspectedCard==null&&hoverView?.card.instanceId==focus,"Inspection Back restores hand focus");
                PressXboxCheck(pad,GamepadButton.Start);CombatCheck(combatPauseOpen,"Xbox Menu opens pause");
                PressXboxCheck(pad,GamepadButton.East);CombatCheck(!combatPauseOpen&&!controllerTargeting,"B resumes without reopening pause");
                ShowInputHint("A CONFIRM");HandleCombatPointer(GroupPortrait(0).center,false,false,false);
                CombatCheck(!controllerNavigation&&hoverView==null&&selectedView==null&&CombatInspectionAllowed&&inputHint=="","Actual mouse movement clears controller highlights, stale prompts and tooltip ownership");
                var point=CardPickPoint(0);HandleCombatPointer(point,true,true,false);HandleCombatPointer(GroupDropZone(0).center,false,true,false);
                CombatCheck(dragView!=null&&cardDragging,"Mouse still starts an Attack drag after controller takeover");
                PressXboxCheck(pad,GamepadButton.DpadRight);
                CombatCheck(controllerNavigation&&dragView==null&&!cardDragging,"Xbox takeover safely returns a mouse-held card");
                PressXboxCheck(pad,GamepadButton.South);OnApplicationFocus(false);
                CombatCheck(!controllerTargeting&&selectedView==null&&!cardDragging,"Losing application focus leaves no stranded targeting state");
                var turn=combat.turn;PressXboxCheck(pad,GamepadButton.North);yield return WaitForCombatQueue();
                CombatCheck(combat.turn==turn+1,"Xbox Y ends turn through production queue");
                PrepareCombatCheck("flurry",5);yield return WaitForCombatQueue();
                var late=Time.unscaledTime+10;combat.enemy.hp=991;vitalBeats.Clear();
                for(var i=0;i<3;i++)vitalBeats.Add(new VitalBeat{time=late-1+i*.18f,fact=new CombatEvent(CombatEventKind.Damage,3){hasVitals=true,enemyHp=997-i*3,playerHp=combat.player.hp,enemyIndex=0,hitId=i+1}});
                UpdateVitalsPlayback(late);CombatCheck(displayedEnemyHp==997&&vitalBeats.Count==2,"Late frame presents first hit instead of skipping to total damage");
                UpdateVitalsPlayback(late+.04f);CombatCheck(displayedEnemyHp==997,"Late sequential hit retains a readable minimum gap");
                UpdateVitalsPlayback(late+.09f);CombatCheck(displayedEnemyHp==994&&vitalBeats.Count==1,"Late second hit has its own health step");
                UpdateVitalsPlayback(late+.18f);CombatCheck(displayedEnemyHp==991&&vitalBeats.Count==0,"Late final hit reaches authoritative HP without changing gameplay");
                ResetVitalsPlayback();
                UseCombatPointerMode();yield return RunMenuPolishChecks();
                ConfigureShardCapture("group-four");yield return WaitForCombatQueue();
                for(var i=0;i<4;i++)
                {
                    var a=GroupPresentedPortrait(i);var old=shimmer;shimmer+=2;var b=GroupPresentedPortrait(i);shimmer=old;
                    CombatCheck(a.y==b.y,"Enemy idle stays grounded "+i);
                }
                PressXboxCheck(pad,GamepadButton.DpadRight);
                yield return new WaitForSecondsRealtime(.35f);
            }
            finally{InputSystem.RemoveDevice(pad);}
        }
    }
}
