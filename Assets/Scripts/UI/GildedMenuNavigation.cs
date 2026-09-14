using System;
using System.Linq;
using GildedFate.Audio;
using GildedFate.Core;
using GildedFate.Map;
using GildedFate.Saving;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private struct MenuNavigation
        {
            public int x,y,page;
            public bool accept,back,inspect,category,sort,hud,deck,map,pause;
            public bool Any=>x!=0||y!=0||page!=0||accept||back||inspect||category||sort||hud||deck||map||pause;
        }
        private bool menuInputConsumed,menuUsesGamepad;
        private MenuNavigation combatNavigationInput;
        private int pauseMenuIndex,shardDiscoveryIndex,hudNavigationIndex=-1;
        private string menuNavigationContext;
        private Vector2Int heldMenuAxis;
        private float menuAxisRepeatAt;
        private MenuNavigation ReadMenuNavigation()
        {
            var pad=Gamepad.current;var key=Keyboard.current;var input=new MenuNavigation();
            input.x=(pad?.dpad.right.wasPressedThisFrame==true||key?.rightArrowKey.wasPressedThisFrame==true?1:0)-(pad?.dpad.left.wasPressedThisFrame==true||key?.leftArrowKey.wasPressedThisFrame==true?1:0);
            input.y=(pad?.dpad.down.wasPressedThisFrame==true||key?.downArrowKey.wasPressedThisFrame==true?1:0)-(pad?.dpad.up.wasPressedThisFrame==true||key?.upArrowKey.wasPressedThisFrame==true?1:0);
            var stick=pad?.leftStick.ReadValue()??Vector2.zero;var axis=stick.magnitude<.55f?Vector2Int.zero:Mathf.Abs(stick.x)>Mathf.Abs(stick.y)?new Vector2Int(stick.x>0?1:-1,0):new Vector2Int(0,stick.y>0?-1:1);
            if(axis!=heldMenuAxis){heldMenuAxis=axis;menuAxisRepeatAt=Time.unscaledTime+.32f;if(input.x==0&&input.y==0){input.x=axis.x;input.y=axis.y;}}
            else if(axis!=Vector2Int.zero&&Time.unscaledTime>=menuAxisRepeatAt){menuAxisRepeatAt=Time.unscaledTime+.14f;if(input.x==0&&input.y==0){input.x=axis.x;input.y=axis.y;}}
            input.page=(pad?.rightShoulder.wasPressedThisFrame==true||key?.eKey.wasPressedThisFrame==true?1:0)-(pad?.leftShoulder.wasPressedThisFrame==true||key?.qKey.wasPressedThisFrame==true?1:0);
            input.accept=pad?.buttonSouth.wasPressedThisFrame==true||key?.enterKey.wasPressedThisFrame==true;
            input.back=pad?.buttonEast.wasPressedThisFrame==true||pad?.startButton.wasPressedThisFrame==true||key?.escapeKey.wasPressedThisFrame==true||key?.backspaceKey.wasPressedThisFrame==true;
            input.pause=pad?.startButton.wasPressedThisFrame==true||key?.escapeKey.wasPressedThisFrame==true;
            input.inspect=pad?.buttonWest.wasPressedThisFrame==true||key?.iKey.wasPressedThisFrame==true;
            input.category=pad?.buttonNorth.wasPressedThisFrame==true||key?.cKey.wasPressedThisFrame==true;
            input.sort=pad?.rightStickButton.wasPressedThisFrame==true||key?.sKey.wasPressedThisFrame==true;
            input.hud=pad?.selectButton.wasPressedThisFrame==true||key?.tabKey.wasPressedThisFrame==true;
            input.deck=key?.dKey.wasPressedThisFrame==true;input.map=key?.mKey.wasPressedThisFrame==true;
            if(input.Any)menuUsesGamepad=pad!=null&&(pad.allControls.Any(c=>c is UnityEngine.InputSystem.Controls.ButtonControl b&&b.wasPressedThisFrame)||axis!=Vector2Int.zero);
            return input;
        }
        private bool RouteMenuNavigation(MenuNavigation input)
        {
            if(runStartActive)return true;
            var context=screen+":"+run.pendingShardDiscoveryId+":"+run.shards.Count+":"+combatPauseOpen+":"+mapPauseOpen+":"+runPauseOpen;
            if(menuNavigationContext!=context){menuNavigationContext=context;pauseMenuIndex=shardDiscoveryIndex=0;hudNavigationIndex=-1;}
            if(acquisitionActive){menuInputConsumed=input.Any;return true;}
            if(ShardDiscoveryOpen){if(input.Any){controllerNavigation=true;HandleShardDiscoveryNavigation(input);}menuInputConsumed=true;return true;}
            if(screen==ScreenMode.Settings){if(input.Any){controllerNavigation=true;HandleSettingsNavigation(input);}menuInputConsumed=true;return true;}
            if(screen==ScreenMode.Combat&&combatPauseOpen||screen==ScreenMode.Map&&mapPauseOpen||runPauseOpen)
            {
                if(input.Any)
                {
                    controllerNavigation=true;var move=input.y!=0?input.y:input.x;
                    if(move!=0)pauseMenuIndex=(pauseMenuIndex+move+4)%4;
                    if(input.back)ResumePauseMenu();else if(input.accept)ActivatePauseMenu(pauseMenuIndex);
                }
                menuInputConsumed=true;return true;
            }
            if(screen==ScreenMode.Combat)return false;
            if(inspectedCard!=null||inspectedRelic!=null)return false;
            if(input.pause&&screen is ScreenMode.Event or ScreenMode.EventResult or ScreenMode.Reward or ScreenMode.RelicReward or ScreenMode.Treasure or ScreenMode.Merchant or ScreenMode.Sanctuary or ScreenMode.Fateweave)
            {ToggleRunSettings();menuInputConsumed=true;return true;}
            if(ShowsPersistentRunHud&&(input.hud||hudNavigationIndex>=0))
            {
                controllerNavigation=true;
                if(input.hud)hudNavigationIndex=(hudNavigationIndex+1)%3;
                var move=input.x!=0?input.x:input.y;
                if(move!=0)hudNavigationIndex=(hudNavigationIndex+move+3)%3;
                if(input.back)hudNavigationIndex=-1;
                else if(input.accept){var index=hudNavigationIndex;hudNavigationIndex=-1;if(index==0)OpenRouteInspection();else if(index==1)OpenRunDeck();else ToggleRunSettings();}
                return true;
            }
            if(ShowsPersistentRunHud&&input.deck){OpenRunDeck();return true;}
            if(ShowsPersistentRunHud&&input.map){OpenRouteInspection();return true;}
            if(screen==ScreenMode.Collection){if(input.Any){controllerNavigation=true;HandleCollectionNavigation(input);}return true;}
            return false;
        }
        private bool runPauseOpen;
        private void ResumePauseMenu(){combatPauseOpen=false;mapPauseOpen=false;runPauseOpen=false;pauseMenuIndex=0;}
        private void ActivatePauseMenu(int index)
        {
            if(index==0){ResumePauseMenu();return;}
            if(index==1){settingsReturnScreen=screen;settingsPage=settingsFocusIndex=0;settingsOverview=true;screen=ScreenMode.Settings;return;}
            if(screen==ScreenMode.Combat)
            {
                if(index==2){SaveCombatAndReturnToMenu();return;}
                if(!SaveCombatCheckpoint())return;
            }
            else if(!SaveService.Save(run)){banner=SaveService.LastError;return;}
            if(index==3){Application.Quit();return;}
            ResumePauseMenu();screen=ScreenMode.Menu;
        }
        private void DrawUnifiedPauseMenu(float w,float h,bool inCombat)
        {
            Fill(new Rect(0,58,w,h-58),new Color(.004f,.008f,.014f,.95f));
            var panel=new Rect(w*.5f-260,174,520,468);Fill(panel,new Color(.008f,.013f,.022f,.99f));Outline(panel,new Color(.59f,.47f,.28f),1);
            GUI.Label(new Rect(panel.x+20,panel.y+22,panel.width-40,44),inCombat?"COMBAT PAUSED":"ASCENT PAUSED",new GUIStyle(titleStyle){fontSize=29});
            var names=new[]{"RESUME ASCENT","SETTINGS","SAVE & RETURN TO MENU","SAVE & QUIT"};
            for(var i=0;i<names.Length;i++)
            {
                var r=new Rect(panel.x+48,panel.y+93+i*68,panel.width-96,50);DrawButtonFrame(r,controllerNavigation?pauseMenuIndex==i:r.Contains(PointerPosition),false);
                if(GUI.Button(r,names[i],buttonStyle))ActivatePauseMenu(i);
            }
            var note=string.IsNullOrEmpty(SaveService.LastError)?"Your current run is preserved when you save.":SaveService.LastError;
            GUI.Label(new Rect(panel.x+36,panel.yMax-83,panel.width-72,55),note,new GUIStyle(footerStyle){fontSize=14,wordWrap=true});
            DrawMenuNavigationHint(w,h,"↑ ↓  Select    Enter  Confirm    Esc  Resume","D-pad / Stick  Select    A  Confirm    B  Resume");
        }
        private void DrawScreenNavigationHint(float w,float h)
        {
            if(ShardDiscoveryOpen||acquisitionActive||inspectedCard!=null||inspectedRelic!=null||hudNavigationIndex>=0)return;
            if(screen==ScreenMode.Reward)DrawMenuNavigationHint(w,h,"Arrows  Select    Enter  Claim    I  Upgrade preview    Tab  Map / Deck / Settings","D-pad / Stick  Select    A  Claim    X  Upgrade preview    View  Map / Deck / Settings");
            else if(screen is ScreenMode.Event or ScreenMode.Sanctuary or ScreenMode.BindingSelect or ScreenMode.Fateweave or ScreenMode.Treasure or ScreenMode.RelicReward)
                DrawMenuNavigationHint(w,h,"Arrows  Select    Enter  Confirm    Tab  Map / Deck / Settings","D-pad / Stick  Select    A  Confirm    View  Map / Deck / Settings");
        }
        private void DrawMenuNavigationHint(float w,float h,string keyboard,string gamepad)
        {
            if(!controllerNavigation)return;
            GUI.Label(new Rect(210,h-36,w-420,24),menuUsesGamepad?gamepad:keyboard,new GUIStyle(footerStyle){fontSize=12,normal={textColor=new Color(.78f,.77f,.71f)}});
        }
        private void HandleCollectionNavigation(MenuNavigation input)
        {
            if(input.back){ReturnFromCollection();return;}
            if(input.category&&!viewingRunDeck){collectionRelics=!collectionRelics;collectionFilter=collectionSort=screenControllerIndex=0;collectionScroll=0;}
            if(!collectionRelics)
            {
                var filters=viewingRunDeck?4:Core.CardArchive.Tabs.Length;
                if(input.page!=0)SelectCollectionFilter((collectionFilter+input.page+filters)%filters);
                if(input.sort&&!viewingRunDeck){collectionSort=(collectionSort+1)%3;screenControllerIndex=0;collectionScroll=0;}
            }
            var cards=collectionRelics?null:CollectionCardEntries(viewingRunDeck);var count=collectionRelics?GameContent.Relics.Length:cards.Length;
            if(count==0){screenControllerIndex=0;return;}
            screenControllerIndex=Mathf.Clamp(screenControllerIndex+input.x+input.y*6,0,count-1);
            if(input.accept||input.inspect){if(collectionRelics)inspectedRelic=GameContent.Relics[CollectionRelicIndices[screenControllerIndex]];else inspectedCard=cards[screenControllerIndex];}
        }
        private void ReturnFromCollection()
        {
            inspectedCard=inspectionSource=null;inspectedRelic=null;viewingRunDeck=false;screen=collectionReturnScreen;
        }
        private void DrawMenuHudNavigation(float w,float h)
        {
            if(hudNavigationIndex<0||!ShowsPersistentRunHud||screen==ScreenMode.Combat||acquisitionActive||ShardDiscoveryOpen)return;
            var rect=hudNavigationIndex==0?RouteMapButton(w):hudNavigationIndex==1?RunDeckControlRect(w):new Rect(w-67,7,46,44);
            Outline(new Rect(rect.x-4,rect.y-3,rect.width+8,rect.height+6),Gold,2);
            var names=new[]{"ACT MAP","FULL DECK","SETTINGS"};
            DrawTooltip(new Rect(rect.x-240,rect.yMax+12,284,90),names[hudNavigationIndex],menuUsesGamepad?"A: open · B: return · D-pad: select":"Enter: open · Esc: return · Arrows: select");
        }
    }
}
