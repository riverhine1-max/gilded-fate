using System;
using System.Collections.Generic;
using GildedFate.Audio;
using GildedFate.Saving;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private sealed class SettingRow
        {
            public string name,description;
            public Func<float> get;public Action<float> set;public Func<string> format;
            public bool toggle,slider,display;public float minimum,maximum,step,reset;
            public int[] values;public string[] labels;
            public string Display=>format!=null?format():toggle?(get()>.5f?"ON":"OFF"):slider?(maximum>1?get().ToString("0.00")+"×":Mathf.RoundToInt(get()*100)+"%"):labels[Mathf.Max(0,Array.IndexOf(values,Mathf.RoundToInt(get())))];
        }
        // Page order is fixed: verification and saved focus depend on Accessibility staying page 3.
        private static readonly string[] SettingsPageNames={"GRAPHICS","AUDIO","GAMEPLAY","ACCESSIBILITY","CONTROLS"};
        private static readonly string[] SettingsPageArt={"Settings_Display","Settings_Audio","Settings_Gameplay","Settings_Accessibility","Settings_Controls"};
        private static readonly string[] SettingsPageHelp={"Window, resolution, brightness and image quality.","Music, effects, interface volume and focus.","Game speed, feedback, tooltips and turn flow.","Contrast, text size, motion and flashing.","Button prompts and the full control layout."};
        private const int SettingsPageCount=5;
        private int settingsFocusIndex;
        private bool settingsOverview;
        private SettingRow[][] settingsRows;
        private PlayerProfile settingsRowsProfile;
        private string settingsNotice="";private float settingsNoticeUntil;
        // Display changes ask to be kept, then revert on their own, so a bad mode never strands the player.
        private bool displayConfirmOpen;private float displayConfirmUntil;
        private int displayRevertMode,displayRevertWidth,displayRevertHeight;
        private Vector2Int[] resolutionChoices;
        private Vector2Int[] ResolutionChoices()
        {
            if(resolutionChoices!=null)return resolutionChoices;
            var list=new List<Vector2Int>();
            try{foreach(var r in Screen.resolutions){var v=new Vector2Int(r.width,r.height);if(v.x>=1024&&v.y>=576&&!list.Contains(v))list.Add(v);}}catch{}
            list.Sort((a,b)=>a.x!=b.x?a.x.CompareTo(b.x):a.y.CompareTo(b.y));
            return resolutionChoices=list.ToArray();
        }
        private int ResolutionIndex(){var list=ResolutionChoices();for(var i=0;i<list.Length;i++)if(list[i].x==profile.resolutionWidth&&list[i].y==profile.resolutionHeight)return i+1;return 0;}
        private void SetResolutionIndex(int index){var list=ResolutionChoices();if(index<=0||index>list.Length){profile.resolutionWidth=profile.resolutionHeight=0;return;}profile.resolutionWidth=list[index-1].x;profile.resolutionHeight=list[index-1].y;}
        private static readonly string[] WindowModeNames={"FULLSCREEN","BORDERLESS","WINDOWED"};
        private SettingRow[] CurrentSettingsRows()
        {
            if(settingsRows!=null&&ReferenceEquals(settingsRowsProfile,profile))return settingsRows[Mathf.Clamp(settingsPage,0,SettingsPageCount-1)];
            settingsRowsProfile=profile;
            SettingRow ToggleRow(string name,string help,Func<bool> get,Action<bool> set)=>new(){name=name,description=help,toggle=true,get=()=>get()?1:0,set=v=>set(v>.5f)};
            SettingRow Range(string name,string help,Func<float> get,Action<float> set,float min=0,float max=1,float step=.05f)=>new(){name=name,description=help,slider=true,get=get,set=set,minimum=min,maximum=max,step=step};
            SettingRow Cycle(string name,string help,Func<int> get,Action<int> set,int[] values,string[] labels)=>new(){name=name,description=help,get=()=>get(),set=v=>set(Mathf.RoundToInt(v)),values=values,labels=labels};
            var resolutions=ResolutionChoices();var resolutionValues=new int[resolutions.Length+1];var resolutionLabels=new string[resolutions.Length+1];
            resolutionLabels[0]="NATIVE";for(var i=0;i<resolutions.Length;i++){resolutionValues[i+1]=i+1;resolutionLabels[i+1]=resolutions[i].x+" × "+resolutions[i].y;}
            var windowMode=Cycle("WINDOW MODE","Fullscreen is exclusive; Borderless fills the screen and switches apps instantly.",()=>profile.windowMode,v=>profile.windowMode=v,new[]{0,1,2},WindowModeNames);windowMode.display=true;
            var resolution=Cycle("RESOLUTION","Native matches your display. Changes ask for confirmation.",ResolutionIndex,SetResolutionIndex,resolutionValues,resolutionLabels);resolution.display=true;
            var brightness=Range("BRIGHTNESS","Lighten or darken the whole picture. 100% is the intended look.",()=>profile.brightness,v=>profile.brightness=v,.7f,1.3f,.05f);brightness.format=()=>Mathf.RoundToInt(profile.brightness*100)+"%";
            var promptValues=new[]{0,1,2};var promptLabels=new[]{"AUTO","XBOX","PLAYSTATION"};
            settingsRows=new[]{
                new[]{
                    windowMode,resolution,brightness,
                    ToggleRow("VERTICAL SYNC","Synchronize drawing to your display to reduce tearing.",()=>profile.vSync,v=>profile.vSync=v),
                    Cycle("FRAME RATE LIMIT","Maximum frame rate when vertical sync is disabled.",()=>profile.fpsLimit,v=>profile.fpsLimit=v,new[]{60,120,144,240},new[]{"60 FPS","120 FPS","144 FPS","240 FPS"}),
                    Cycle("TEXTURE QUALITY","Lower settings use less graphics memory.",()=>profile.textureQuality,v=>profile.textureQuality=v,new[]{0,1,2},new[]{"HIGH","MEDIUM","LOW"}),
                    Cycle("ANTI-ALIASING","Smooth the edges of rendered shapes.",()=>profile.antiAliasing,v=>profile.antiAliasing=v,new[]{0,2,4,8},new[]{"OFF","2×","4×","8×"})},
                new[]{
                    Range("MASTER VOLUME","Overall volume. At 0%, all audio is muted.",()=>profile.master,v=>profile.master=v),
                    Range("MUSIC","Background music volume.",()=>profile.music,v=>profile.music=v),
                    Range("EFFECTS","Combat, card and world sound effects.",()=>profile.effects,v=>profile.effects=v),
                    Range("UI","Menu selection and interface sounds.",()=>profile.ui,v=>profile.ui=v),
                    ToggleRow("MUTE IN BACKGROUND","Silence the game while another window has focus.",()=>profile.muteInBackground,v=>profile.muteInBackground=v)},
                new[]{
                    Cycle("GAME SPEED","Speeds up combat animation. Rules and timing windows never change.",()=>profile.gameSpeed,v=>profile.gameSpeed=v,new[]{0,1,2},new[]{"NORMAL","FAST","VERY FAST"}),
                    ToggleRow("INSTANT ENEMY TURNS","Shorten the pause before and after each enemy action.",()=>profile.instantEnemyTurns,v=>profile.instantEnemyTurns=v),
                    ToggleRow("CONFIRM END TURN","Ask again before ending a turn while cards are still playable.",()=>profile.confirmEndTurn,v=>profile.confirmEndTurn=v),
                    ToggleRow("SCREEN SHAKE","Small impact movement. Reduce Motion also suppresses it.",()=>profile.screenShake,v=>profile.screenShake=v),
                    ToggleRow("DAMAGE NUMBERS","Show damage and combat feedback numbers.",()=>profile.damageNumbers,v=>profile.damageNumbers=v),
                    ToggleRow("TOOLTIPS","Explain keywords and objects beside the focused item.",()=>profile.tooltips,v=>profile.tooltips=v),
                    ToggleRow("FAST TRANSITIONS","Speed up card and scene transitions without changing combat rules.",()=>profile.fastMode,v=>profile.fastMode=v),
                    Range("CARD MOTION SPEED","0.50× is slower; 2.00× is faster. This changes presentation only.",()=>profile.cardAnimationSpeed,v=>profile.cardAnimationSpeed=v,.5f,2f,.1f)},
                new[]{
                    ToggleRow("HIGH-CONTRAST UI","Increase contrast behind important interface text.",()=>profile.highContrastUi,v=>profile.highContrastUi=v),
                    ToggleRow("HIGH-CONTRAST INTENTS","Strengthen enemy-intention colors and outlines.",()=>profile.highContrastIntents,v=>profile.highContrastIntents=v),
                    ToggleRow("LARGE CARD TEXT","Use the larger readable card-text treatment.",()=>profile.largeCardText,v=>profile.largeCardText=v),
                    ToggleRow("LARGE INTENT ICONS","Increase enemy-intention icon and value size.",()=>profile.largeIntents,v=>profile.largeIntents=v),
                    ToggleRow("LARGE EFFECT ICONS","Increase buff, debuff and Power HUD icons.",()=>profile.largeEffectIcons,v=>profile.largeEffectIcons=v),
                    ToggleRow("LARGE DAMAGE NUMBERS","Increase floating combat-number size.",()=>profile.largeDamageNumbers,v=>profile.largeDamageNumbers=v),
                    ToggleRow("STATUS TEXT LABELS","Show status text so color is not the only signal.",()=>profile.colorblindStatus,v=>profile.colorblindStatus=v),
                    ToggleRow("REDUCED VFX INTENSITY","Reduce decorative particles and effect density.",()=>profile.reducedVfx,v=>profile.reducedVfx=v),
                    ToggleRow("REDUCE FLASHING","Suppress decorative flashes and bright screen effects.",()=>profile.reduceFlashing,v=>profile.reduceFlashing=v),
                    ToggleRow("REDUCE MOTION","Reduce movement and simplify animated transitions.",()=>profile.reduceMotion,v=>profile.reduceMotion=v)},
                new[]{
                    Cycle("BUTTON PROMPTS","Auto matches the connected controller. Choose a style to force it.",()=>profile.padPromptStyle,v=>profile.padPromptStyle=v,promptValues,promptLabels),
                    ToggleRow("CONTROLLER BADGES","Show the small button badges beside the top bar and End Turn.",()=>profile.showPadPrompts,v=>profile.showPadPrompts=v)}
            };
            // Defaults come from a fresh profile, read through the same getters.
            var saved=profile;profile=new PlayerProfile();
            foreach(var page in settingsRows)foreach(var row in page)row.reset=row.get();
            profile=saved;
            return settingsRows[Mathf.Clamp(settingsPage,0,SettingsPageCount-1)];
        }
        private static bool SettingsTwoColumns(int rowCount)=>rowCount>5;
        private static Rect SettingsRowRect(float width,int index,bool twoColumns)
        {
            var total=twoColumns?960:760;var column=twoColumns?index%2:0;var row=twoColumns?index/2:index;
            var cell=twoColumns?468:760;
            return new Rect((width-total)*.5f+column*492,246+row*73,cell,58);
        }
        private void CaptureDisplayRevert(){if(displayConfirmOpen)return;displayRevertMode=profile.windowMode;displayRevertWidth=profile.resolutionWidth;displayRevertHeight=profile.resolutionHeight;}
        private void OpenDisplayConfirm(){if(captureMode)return;displayConfirmOpen=true;displayConfirmUntil=Time.unscaledTime+12f;}
        private void KeepDisplaySettings(){displayConfirmOpen=false;ProfileService.Save(profile);Sfx(SoundCue.UiConfirm);}
        private void RevertDisplaySettings(){displayConfirmOpen=false;profile.windowMode=displayRevertMode;profile.resolutionWidth=displayRevertWidth;profile.resolutionHeight=displayRevertHeight;ApplySettings();ProfileService.Save(profile);ShowSettingsNotice("DISPLAY SETTINGS RESTORED");}
        private void ShowSettingsNotice(string text){settingsNotice=text;settingsNoticeUntil=Time.unscaledTime+2.4f;}
        private void ChangeSetting(SettingRow row,int direction)
        {
            var before=row.get();if(row.display)CaptureDisplayRevert();
            if(row.toggle)row.set(row.get()>.5f?0:1);
            else if(row.slider)row.set(Mathf.Clamp(Mathf.Round((row.get()+direction*row.step)*100)/100,row.minimum,row.maximum));
            else{var index=Mathf.Max(0,Array.IndexOf(row.values,Mathf.RoundToInt(row.get())));row.set(row.values[(index+direction+row.values.Length)%row.values.Length]);}
            ApplySettings();AudioListener.volume=profile.master;
            if(row.display&&!Mathf.Approximately(before,row.get()))OpenDisplayConfirm();
        }
        private void ResetSettingsPage()
        {
            var rows=CurrentSettingsRows();var displayChanged=false;
            foreach(var row in rows){if(row.display&&!Mathf.Approximately(row.get(),row.reset)){CaptureDisplayRevert();displayChanged=true;}row.set(row.reset);}
            ApplySettings();AudioListener.volume=profile.master;Sfx(SoundCue.UiConfirm);
            ShowSettingsNotice(SettingsPageNames[Mathf.Clamp(settingsPage,0,SettingsPageCount-1)]+" RESTORED TO DEFAULTS");
            if(displayChanged)OpenDisplayConfirm();
        }
        private void ChangeSettingsPage(int page){settingsPage=(page+SettingsPageCount)%SettingsPageCount;settingsFocusIndex=0;settingsOverview=false;}
        private void CloseSettings(){if(displayConfirmOpen)KeepDisplaySettings();ProfileService.Save(profile);settingsOverview=false;screen=settingsReturnScreen;}
        private void BackFromSettings(){if(!settingsOverview){settingsOverview=true;settingsFocusIndex=settingsPage;ProfileService.Save(profile);}else CloseSettings();}
        // Focus order on a page: every row, then BACK (rows.Length), then RESET (rows.Length+1).
        private void HandleSettingsNavigation(MenuNavigation input)
        {
            if(displayConfirmOpen){if(input.accept)KeepDisplaySettings();else if(input.back)RevertDisplaySettings();return;}
            if(input.back){BackFromSettings();return;}
            if(settingsOverview){var move=input.y!=0?input.y:input.x;if(move!=0)settingsFocusIndex=(settingsFocusIndex+move+SettingsPageCount+1)%(SettingsPageCount+1);if(input.accept){if(settingsFocusIndex==SettingsPageCount)CloseSettings();else ChangeSettingsPage(settingsFocusIndex);}return;}
            if(input.page!=0){ChangeSettingsPage(settingsPage+input.page);return;}
            var rows=CurrentSettingsRows();var count=rows.Length+2;settingsFocusIndex=Mathf.Clamp(settingsFocusIndex,0,count-1);
            if(input.y!=0||input.hud)settingsFocusIndex=(settingsFocusIndex+(input.hud?1:input.y)+count)%count;
            if(settingsFocusIndex>=rows.Length)
            {
                if(input.x!=0)settingsFocusIndex=settingsFocusIndex==rows.Length?rows.Length+1:rows.Length;
                else if(input.accept){if(settingsFocusIndex==rows.Length)BackFromSettings();else ResetSettingsPage();}
                return;
            }
            if(input.x!=0)ChangeSetting(rows[settingsFocusIndex],input.x);
            else if(input.accept&&!rows[settingsFocusIndex].slider)ChangeSetting(rows[settingsFocusIndex],1);
        }
        private void DrawSettings(float w,float h)
        {
            if(ShowsPersistentRunHud)DrawRunHud(w);Heading(w,"SETTINGS","SHAPE THE VAULT TO YOUR NEEDS");
            if(displayConfirmOpen&&Time.unscaledTime>displayConfirmUntil)RevertDisplaySettings();
            if(settingsOverview){DrawSettingsOverview(w,h);return;}
            var wasEnabled=GUI.enabled;GUI.enabled=!displayConfirmOpen;
            DrawTabs(w*.5f-375,186,150,SettingsPageNames,settingsPage,ChangeSettingsPage);
            var rows=CurrentSettingsRows();settingsFocusIndex=Mathf.Clamp(settingsFocusIndex,0,rows.Length+1);var help="Select an option to see what it changes.";
            var twoColumns=SettingsTwoColumns(rows.Length);
            for(var i=0;i<rows.Length;i++)
            {
                var row=rows[i];var r=SettingsRowRect(w,i,twoColumns);var hot=controllerNavigation?settingsFocusIndex==i:r.Contains(PointerPosition);
                Fill(r,hot?new Color(.085f,.066f,.035f,.97f):new Color(.014f,.022f,.031f,.95f));Outline(r,hot?Gold:new Color(.28f,.30f,.31f),hot?2:1);
                if(hot)Fill(new Rect(r.x,r.y+8,3,r.height-16),Gold);
                var style=new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=15,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(.94f,.91f,.83f)}};
                var labelWidth=row.slider?(twoColumns?196:250):r.width-190;
                GUI.Label(new Rect(r.x+18,r.y+10,labelWidth,38),row.name,style);
                var changed=!Mathf.Approximately(row.get(),row.reset);
                if(changed)Fill(new Rect(r.x+r.width-6,r.y+6,3,3),new Color(.96f,.78f,.42f,.8f));
                if(row.slider)
                {
                    var trackX=r.x+(twoColumns?210:292);var track=new Rect(trackX,r.center.y-10,r.xMax-110-trackX,20);var fraction=Mathf.InverseLerp(row.minimum,row.maximum,row.get());
                    Fill(new Rect(track.x+9,r.center.y-2,track.width-18,4),new Color(.24f,.26f,.28f));
                    Fill(new Rect(track.x+9,r.center.y-2,(track.width-18)*fraction,4),new Color(.80f,.62f,.30f));
                    if(row.minimum<1&&row.maximum>1){var mark=track.x+9+(track.width-18)*Mathf.InverseLerp(row.minimum,row.maximum,1);Fill(new Rect(mark-1,r.center.y-6,2,12),new Color(.55f,.52f,.45f));}
                    var knob=new Rect(track.x+(track.width-18)*fraction+3,r.center.y-8,12,16);Fill(knob,new Color(.96f,.83f,.57f));Outline(knob,new Color(.32f,.22f,.09f),1);
                    var value=GUI.HorizontalSlider(track,row.get(),row.minimum,row.maximum,GUIStyle.none,new GUIStyle(GUIStyle.none){fixedWidth=18,fixedHeight=20});
                    if(!Mathf.Approximately(value,row.get())){row.set(Mathf.Round(value/row.step)*row.step);ApplySettings();AudioListener.volume=profile.master;}
                }
                else if(!row.toggle&&hot)
                {
                    var arrows=new GUIStyle(style){alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(.78f,.66f,.42f)}};
                    GUI.Label(new Rect(r.xMax-196,r.y+10,18,38),"‹",arrows);GUI.Label(new Rect(r.xMax-24,r.y+10,18,38),"›",arrows);
                }
                var valueRect=new Rect(r.xMax-178,r.y+10,152,38);GUI.Label(valueRect,row.Display,new GUIStyle(style){alignment=TextAnchor.MiddleRight,normal={textColor=row.toggle&&row.get()>.5f?new Color(.67f,.95f,.77f):Gold}});
                if(!row.slider&&GUI.Button(r,"",GUIStyle.none)){settingsFocusIndex=i;ChangeSetting(row,Event.current.button==1?-1:1);}
                if(hot)help=row.description;
            }
            if(settingsPage==4)DrawControlsReference(w,SettingsRowRect(w,rows.Length-1,false).yMax+18);
            if(Time.unscaledTime<settingsNoticeUntil)help=settingsNotice;
            GUI.Label(new Rect(w*.5f-440,640,880,52),help,new GUIStyle(footerStyle){fontSize=15,wordWrap=true,alignment=TextAnchor.UpperCenter,normal={textColor=Time.unscaledTime<settingsNoticeUntil?new Color(1f,.84f,.5f):new Color(.83f,.81f,.73f)}});
            var reset=new Rect(w*.5f-300,h-94,260,48);DrawButtonFrame(reset,controllerNavigation?settingsFocusIndex==rows.Length+1:reset.Contains(PointerPosition),false);
            if(GUI.Button(reset,"RESET TO DEFAULTS",buttonStyle)){settingsFocusIndex=rows.Length+1;ResetSettingsPage();}
            var back=new Rect(w*.5f+40,h-94,260,48);DrawButtonFrame(back,controllerNavigation?settingsFocusIndex==rows.Length:back.Contains(PointerPosition),false);
            if(GUI.Button(back,"BACK TO SETTINGS",buttonStyle))BackFromSettings();
            GUI.enabled=wasEnabled;
            DrawMenuNavigationHint(w,h,"↑ ↓  Select    ← →  Adjust    Q / E  Page    Esc  Return","D-pad / Stick  Select & Adjust    LB / RB  Page    B  Return");
            if(displayConfirmOpen)DrawDisplayConfirm(w,h);
        }
        private void DrawDisplayConfirm(float w,float h)
        {
            Fill(new Rect(0,0,w,h),new Color(0,0,0,.62f));
            var r=new Rect(w*.5f-260,h*.5f-110,520,220);Fill(r,new Color(.02f,.022f,.03f,.98f));Outline(r,Gold,2);Outline(new Rect(r.x+7,r.y+7,r.width-14,r.height-14),new Color(.3f,.24f,.13f),1);
            GUI.Label(new Rect(r.x,r.y+22,r.width,34),"KEEP THESE DISPLAY SETTINGS?",new GUIStyle(titleStyle){fontSize=22});
            var left=Mathf.Max(0,Mathf.CeilToInt(displayConfirmUntil-Time.unscaledTime));
            GUI.Label(new Rect(r.x+30,r.y+62,r.width-60,56),$"{WindowModeNames[Mathf.Clamp(profile.windowMode,0,2)]} · {(profile.resolutionWidth>0?profile.resolutionWidth+" × "+profile.resolutionHeight:"NATIVE")}\nReverting in {left} second{(left==1?"":"s")}.",new GUIStyle(footerStyle){fontSize=15,wordWrap=true,alignment=TextAnchor.UpperCenter});
            var keep=new Rect(r.x+40,r.yMax-74,200,48);var revert=new Rect(r.xMax-240,r.yMax-74,200,48);
            DrawButtonFrame(keep,keep.Contains(PointerPosition)||controllerNavigation,false);DrawButtonFrame(revert,revert.Contains(PointerPosition),false);
            if(GUI.Button(keep,"KEEP",buttonStyle))KeepDisplaySettings();
            if(GUI.Button(revert,"REVERT",buttonStyle))RevertDisplaySettings();
            if(ShowPadGlyphs){DrawPadGlyph(new Vector2(keep.x+26,keep.center.y),"A",true);DrawPadGlyph(new Vector2(revert.x+26,revert.center.y),"B",true);}
        }
        private void DrawControlsReference(float w,float top)
        {
            var panel=new Rect(w*.5f-380,top,760,Mathf.Min(616-top,250));
            Fill(panel,new Color(.012f,.018f,.026f,.93f));Outline(panel,new Color(.33f,.30f,.22f),1);
            var head=new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=12,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(1f,.82f,.48f)}};
            var key=new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=13,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleRight,normal={textColor=new Color(.95f,.9f,.78f)}};
            var act=new GUIStyle(footerStyle){fontSize=13,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(.80f,.78f,.71f)}};
            var ps=PadUsesPlayStation;
            string P(string xbox)=>ps?PlayStationName(xbox):xbox;
            var keyboard=new[]{("Mouse","Drag or click cards to play"),("Space","End turn"),("Arrows","Move focus"),("Enter","Confirm"),("Esc","Back / Pause"),("Tab · D · M","Top bar · Deck · Map"),("Q / E","Change page"),("I","Inspect card")};
            var pad=new[]{("Stick / D-pad","Move focus"),(P("A"),"Confirm / Play card"),(P("B"),"Back"),(P("Y"),"End turn"),(P("X")+" / "+P("View"),"Top bar · Collection: inspect"),("R3","Inspect card"),(P("LB")+" / "+P("RB"),"Change page"),(P("Menu"),"Pause")};
            void Column(float x,string title,(string,string)[] list)
            {
                GUI.Label(new Rect(x,panel.y+10,340,20),title,head);
                for(var i=0;i<list.Length;i++){var y=panel.y+34+i*23;if(y+20>panel.yMax-4)break;GUI.Label(new Rect(x,y,120,20),list[i].Item1,key);GUI.Label(new Rect(x+134,y,230,20),list[i].Item2,act);}
            }
            Column(panel.x+16,"KEYBOARD & MOUSE",keyboard);Fill(new Rect(panel.center.x,panel.y+12,1,panel.height-24),new Color(.3f,.27f,.2f));
            Column(panel.center.x+16,ps?"CONTROLLER · PLAYSTATION":"CONTROLLER · XBOX",pad);
        }
        private void DrawSettingsOverview(float w,float h)
        {
            for(var i=0;i<SettingsPageCount;i++)
            {
                var r=i<3?new Rect(w*.5f-470+i*320,236,300,140):new Rect(w*.5f-310+(i-3)*320,396,300,140);var hot=controllerNavigation?settingsFocusIndex==i:r.Contains(PointerPosition);
                DrawButtonFrame(r,hot,false);
                var art=MetaArt(SettingsPageArt[i]);var textX=r.x+18;
                if(art){GUI.DrawTexture(new Rect(r.x+14,r.y+(r.height-78)*.5f,78,78),art,ScaleMode.ScaleToFit,true);textX=r.x+102;}
                GUI.Label(new Rect(textX,r.y+22,r.xMax-textX-14,32),SettingsPageNames[i],new GUIStyle(titleStyle){fontSize=i==3?19:22,alignment=art?TextAnchor.MiddleLeft:TextAnchor.MiddleCenter});
                GUI.Label(new Rect(textX+(art?0:8),r.y+60,r.xMax-textX-(art?16:24),64),SettingsPageHelp[i],new GUIStyle(footerStyle){fontSize=15,wordWrap=true,alignment=art?TextAnchor.UpperLeft:TextAnchor.UpperCenter});
                if(GUI.Button(r,"",GUIStyle.none))ChangeSettingsPage(i);
            }
            var back=new Rect(w*.5f-130,h-94,260,48);DrawButtonFrame(back,controllerNavigation?settingsFocusIndex==SettingsPageCount:back.Contains(PointerPosition),false);if(GUI.Button(back,"SAVE & RETURN",buttonStyle))CloseSettings();
            GUI.Label(new Rect(w*.5f-300,h-128,600,22),"Gilded Fate "+GameVersion,new GUIStyle(footerStyle){fontSize=12,normal={textColor=new Color(.55f,.53f,.48f)}});
            DrawMenuNavigationHint(w,h,"Arrows  Select    Enter  Open    Esc  Return","D-pad / Stick  Select    A  Open    B  Return");
        }
    }
}
