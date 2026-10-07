using System;
using GildedFate.Audio;
using GildedFate.Saving;
using UnityEngine;

namespace GildedFate.UI
{
    // Polish pass: flow safety. One shared UI scale, a short input guard after every screen change, a
    // confirmation before a new run replaces a saved run, and a notice when a saved run cannot be read.
    public sealed partial class GildedMainMenu
    {
        // The single authoring canvas: 1440 x 810 fitted to the window. Everything that draws or hit-tests in
        // canvas units (menus, combat, meta overlay) must use this one formula.
        private static float UiScale=>Mathf.Max(.35f,Mathf.Min(Screen.width/1440f,Screen.height/810f));

        // Clicks and A presses that arrive right after a screen change belong to the screen that was just left
        // (a double-click on the last button must not land on a card in the next screen).
        private float screenInputReadyAt;
        private const float ScreenInputGuard=.2f;
        private void ArmScreenInputGuard(float seconds=ScreenInputGuard)=>screenInputReadyAt=Time.unscaledTime+seconds;
        private bool ScreenInputReady=>captureMode||Time.unscaledTime>=screenInputReadyAt;
        private bool ScreenUsesInputGuard=>screen!=ScreenMode.Combat&&screen!=ScreenMode.Menu&&screen!=ScreenMode.Playground;
        // Called first thing in DrawMainGUI: swallows pointer presses during the guard window.
        private void SwallowEarlyPointerInput()
        {
            if(captureMode||!ScreenUsesInputGuard||ScreenInputReady)return;
            var type=Event.current.type;
            if(type==EventType.MouseDown||type==EventType.MouseUp)Event.current.Use();
        }

        // ---------- replace-run confirmation ----------
        private bool replaceRunConfirmOpen;
        private Action replaceRunAction;
        private int replaceRunFocus; // 0 = KEEP MY RUN (the safe default), 1 = REPLACE
        private string replaceRunSummary="";
        private bool replaceRunDamaged;

        // Runs `begin` straight away unless a saved run (or a save that cannot be read) would be replaced.
        private void GuardReplaceRun(Action begin)
        {
            if(captureMode||SaveService.Suspended||!(SaveService.HasRun||SaveService.DamagedSaveExists)){begin();return;}
            replaceRunAction=begin;replaceRunFocus=0;replaceRunDamaged=!SaveService.HasRun;replaceRunSummary="";
            if(!replaceRunDamaged)
            {
                var saved=SaveService.Load();
                if(saved!=null)replaceRunSummary=saved.hero.ToString().ToUpperInvariant()+" · ACT "+RomanAct(saved.act)+" · ROOM "+(saved.floor+1)+" · "+saved.hp+"/"+saved.maxHp+" HP · "+saved.gold+" GOLD";
            }
            replaceRunConfirmOpen=true;Sfx(SoundCue.UiConfirm);
        }
        private void CloseReplaceRun(bool replace)
        {
            var action=replaceRunAction;replaceRunConfirmOpen=false;replaceRunAction=null;
            if(replace){Sfx(SoundCue.UiConfirm);action?.Invoke();}else Sfx(SoundCue.UiBack);
        }
        private void ReplaceRunRects(float w,float h,out Rect panel,out Rect keep,out Rect replace)
        {
            panel=new Rect(w*.5f-290,h*.5f-135,580,270);
            keep=new Rect(panel.x+36,panel.yMax-74,250,46);replace=new Rect(panel.xMax-286,panel.yMax-74,250,46);
        }
        // Pointer and key presses are consumed before any screen draws, so nothing underneath can react to them.
        private void HandleReplaceRunPointer(float w,float h)
        {
            if(!replaceRunConfirmOpen)return;
            var type=Event.current.type;
            if(type==EventType.KeyDown){Event.current.Use();return;}
            if(type!=EventType.MouseDown&&type!=EventType.MouseUp)return;
            ReplaceRunRects(w,h,out _,out var keep,out var replace);
            var point=guiPointerPosition; // canvas units, read before GUI.matrix was applied
            if(type==EventType.MouseDown){if(replace.Contains(point))CloseReplaceRun(true);else if(keep.Contains(point))CloseReplaceRun(false);}
            Event.current.Use();
        }
        private bool HandleReplaceRunNavigation(MenuNavigation input)
        {
            if(!replaceRunConfirmOpen)return false;
            if(!input.Any)return true;controllerNavigation=true;
            if(input.x!=0||input.y!=0){replaceRunFocus=1-replaceRunFocus;Sfx(SoundCue.UiHover);}
            else if(input.back)CloseReplaceRun(false);
            else if(input.accept)CloseReplaceRun(replaceRunFocus==1);
            return true;
        }
        private void DrawReplaceRunConfirm(float w,float h)
        {
            ReplaceRunRects(w,h,out var panel,out var keep,out var replace);DrawModalPanel(w,h,panel);
            GUI.Label(new Rect(panel.x,panel.y+22,panel.width,44),replaceRunDamaged?"REPLACE THE DAMAGED SAVE?":"REPLACE YOUR SAVED RUN?",new GUIStyle(titleStyle){fontSize=28});
            var body=new GUIStyle(footerStyle){fontSize=15,wordWrap=true,alignment=TextAnchor.UpperCenter,normal={textColor=new Color(.92f,.88f,.8f)}};
            if(replaceRunDamaged)GUI.Label(new Rect(panel.x+36,panel.y+76,panel.width-72,100),"Your saved run could not be read. Its files were kept on disk, but starting a new run replaces them.",body);
            else
            {
                GUI.Label(new Rect(panel.x+36,panel.y+76,panel.width-72,28),replaceRunSummary,new GUIStyle(body){fontSize=17,normal={textColor=new Color(1f,.82f,.45f)}});
                GUI.Label(new Rect(panel.x+36,panel.y+110,panel.width-72,70),"Starting a new run replaces this run. It cannot be recovered.",body);
            }
            var pointer=PointerPosition;var keepHot=controllerNavigation?replaceRunFocus==0:keep.Contains(pointer);var replaceHot=controllerNavigation?replaceRunFocus==1:replace.Contains(pointer);
            DrawButtonFrame(keep,keepHot,false);DrawButtonFrame(replace,replaceHot,false);
            var label=new GUIStyle(buttonStyle);GUI.Label(keep,"KEEP MY RUN",label);GUI.Label(replace,replaceRunDamaged?"START NEW RUN":"REPLACE RUN",label);
            if(ShowPadGlyphs){DrawPadGlyph(new Vector2(keep.x+18,keep.center.y),replaceRunFocus==0?"A":"B",true);DrawPadGlyph(new Vector2(replace.x+18,replace.center.y),replaceRunFocus==1?"A":"X",true);}
        }
        // Drawn above every screen, below the achievement toasts.
        private void DrawFlowOverlays()
        {
            if(!replaceRunConfirmOpen)return;
            var scale=UiScale;GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1f));
            var enabled=GUI.enabled;GUI.enabled=true;DrawReplaceRunConfirm(Screen.width/scale,Screen.height/scale);GUI.enabled=enabled;
        }

        // ---------- damaged-save notice on the title screen ----------
        private void DrawDamagedSaveNotice(float w,float h)
        {
            if(!SaveService.DamagedSaveExists)return;
            var style=new GUIStyle(footerStyle){fontSize=14,wordWrap=true,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(1f,.55f,.42f)}};
            GUI.Label(new Rect(w*.2f,h-96,w*.6f,34),"YOUR SAVED RUN COULD NOT BE READ · THE FILES WERE KEPT · STARTING A NEW RUN WILL REPLACE THEM",style);
        }

        // ---------- sanctuary / deck service helpers ----------
        private bool HasUpgradableCard()
        {
            foreach(var saved in run.cards)
            {
                var def=saved.BuildDefinition();
                if(def!=null&&!saved.upgraded&&def.rarity!=GildedFate.Core.Rarity.Curse&&def.rarity!=GildedFate.Core.Rarity.Status)return true;
            }
            return false;
        }
    }
}
