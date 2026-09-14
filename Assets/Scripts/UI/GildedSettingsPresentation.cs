using System;
using GildedFate.Saving;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private sealed class SettingRow
        {
            public string name,description;
            public Func<float> get;public Action<float> set;
            public bool toggle,slider;public float minimum,maximum,step;
            public int[] values;public string[] labels;
            public string Display=>toggle?(get()>.5f?"ON":"OFF"):slider?(maximum>1?get().ToString("0.00")+"×":Mathf.RoundToInt(get()*100)+"%"):labels[Mathf.Max(0,Array.IndexOf(values,Mathf.RoundToInt(get())))];
        }
        private int settingsFocusIndex;
        private bool settingsOverview;
        private SettingRow[][] settingsRows;
        private PlayerProfile settingsRowsProfile;
        private SettingRow[] CurrentSettingsRows()
        {
            if(settingsRows!=null&&ReferenceEquals(settingsRowsProfile,profile))return settingsRows[Mathf.Clamp(settingsPage,0,3)];
            settingsRowsProfile=profile;
            SettingRow ToggleRow(string name,string help,Func<bool> get,Action<bool> set)=>new(){name=name,description=help,toggle=true,get=()=>get()?1:0,set=v=>set(v>.5f)};
            SettingRow Range(string name,string help,Func<float> get,Action<float> set,float min=0,float max=1,float step=.05f)=>new(){name=name,description=help,slider=true,get=get,set=set,minimum=min,maximum=max,step=step};
            SettingRow Cycle(string name,string help,Func<int> get,Action<int> set,int[] values,string[] labels)=>new(){name=name,description=help,get=()=>get(),set=v=>set(Mathf.RoundToInt(v)),values=values,labels=labels};
            settingsRows=new[]{
                new[]{
                    ToggleRow("BORDERLESS FULLSCREEN","Fill the display without a window border.",()=>profile.fullscreen,v=>profile.fullscreen=v),
                    ToggleRow("VERTICAL SYNC","Synchronize drawing to your display to reduce tearing.",()=>profile.vSync,v=>profile.vSync=v),
                    Cycle("FRAME RATE LIMIT","Maximum frame rate when vertical sync is disabled.",()=>profile.fpsLimit,v=>profile.fpsLimit=v,new[]{60,120,144,240},new[]{"60 FPS","120 FPS","144 FPS","240 FPS"}),
                    Cycle("TEXTURE QUALITY","Lower settings use less graphics memory.",()=>profile.textureQuality,v=>profile.textureQuality=v,new[]{0,1,2},new[]{"HIGH","MEDIUM","LOW"}),
                    Cycle("ANTI-ALIASING","Smooth the edges of rendered shapes.",()=>profile.antiAliasing,v=>profile.antiAliasing=v,new[]{0,2,4,8},new[]{"OFF","2×","4×","8×"})},
                new[]{
                    Range("MASTER VOLUME","Overall volume. At 0%, all audio is muted.",()=>profile.master,v=>profile.master=v),
                    Range("MUSIC","Background music volume.",()=>profile.music,v=>profile.music=v),
                    Range("EFFECTS","Combat, card and world sound effects.",()=>profile.effects,v=>profile.effects=v),
                    Range("UI","Menu selection and interface sounds.",()=>profile.ui,v=>profile.ui=v)},
                new[]{
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
                    ToggleRow("REDUCE MOTION","Reduce movement and simplify animated transitions.",()=>profile.reduceMotion,v=>profile.reduceMotion=v)}
            };
            return settingsRows[Mathf.Clamp(settingsPage,0,3)];
        }
        private static Rect SettingsRowRect(float width,int index,bool twoColumns)
        {
            var total=twoColumns?960:760;var column=twoColumns?index%2:0;var row=twoColumns?index/2:index;
            var cell=twoColumns?468:760;
            return new Rect((width-total)*.5f+column*492,246+row*73,cell,58);
        }
        private void ChangeSetting(SettingRow row,int direction)
        {
            if(row.toggle)row.set(row.get()>.5f?0:1);
            else if(row.slider)row.set(Mathf.Clamp(Mathf.Round((row.get()+direction*row.step)*100)/100,row.minimum,row.maximum));
            else{var index=Mathf.Max(0,Array.IndexOf(row.values,Mathf.RoundToInt(row.get())));row.set(row.values[(index+direction+row.values.Length)%row.values.Length]);}
            ApplySettings();AudioListener.volume=profile.master;
        }
        private void ChangeSettingsPage(int page){settingsPage=(page+4)%4;settingsFocusIndex=0;settingsOverview=false;}
        private void CloseSettings(){ProfileService.Save(profile);settingsOverview=false;screen=settingsReturnScreen;}
        private void BackFromSettings(){if(!settingsOverview){settingsOverview=true;settingsFocusIndex=settingsPage;ProfileService.Save(profile);}else CloseSettings();}
        private void HandleSettingsNavigation(MenuNavigation input)
        {
            if(input.back){BackFromSettings();return;}
            if(settingsOverview){var move=input.y!=0?input.y:input.x;if(move!=0)settingsFocusIndex=(settingsFocusIndex+move+5)%5;if(input.accept){if(settingsFocusIndex==4)CloseSettings();else ChangeSettingsPage(settingsFocusIndex);}return;}
            if(input.page!=0){ChangeSettingsPage(settingsPage+input.page);return;}
            var rows=CurrentSettingsRows();settingsFocusIndex=Mathf.Clamp(settingsFocusIndex,0,rows.Length);
            if(input.y!=0||input.hud)settingsFocusIndex=(settingsFocusIndex+(input.hud?1:input.y)+rows.Length+1)%(rows.Length+1);
            if(settingsFocusIndex==rows.Length){if(input.accept)BackFromSettings();return;}
            if(input.x!=0)ChangeSetting(rows[settingsFocusIndex],input.x);
            else if(input.accept&&!rows[settingsFocusIndex].slider)ChangeSetting(rows[settingsFocusIndex],1);
        }
        private void DrawSettings(float w,float h)
        {
            if(ShowsPersistentRunHud)DrawRunHud(w);Heading(w,"SETTINGS","SHAPE THE VAULT TO YOUR NEEDS");
            if(settingsOverview){DrawSettingsOverview(w,h);return;}
            var tabs=new[]{"GRAPHICS","AUDIO","GAMEPLAY","ACCESSIBILITY"};DrawTabs(w*.5f-328,186,164,tabs,settingsPage,ChangeSettingsPage);
            var rows=CurrentSettingsRows();settingsFocusIndex=Mathf.Clamp(settingsFocusIndex,0,rows.Length);var help="Select an option to see what it changes.";
            for(var i=0;i<rows.Length;i++)
            {
                var row=rows[i];var r=SettingsRowRect(w,i,settingsPage==3);var hot=controllerNavigation?settingsFocusIndex==i:r.Contains(PointerPosition);
                Fill(r,hot?new Color(.085f,.066f,.035f,.97f):new Color(.014f,.022f,.031f,.95f));Outline(r,hot?Gold:new Color(.28f,.30f,.31f),hot?2:1);
                var style=new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=15,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(.94f,.91f,.83f)}};
                GUI.Label(new Rect(r.x+18,r.y+10,row.slider?250:r.width-120,38),row.name,style);
                if(row.slider)
                {
                    var track=new Rect(r.x+292,r.center.y-10,r.width-392,20);var fraction=Mathf.InverseLerp(row.minimum,row.maximum,row.get());
                    Fill(new Rect(track.x+9,r.center.y-2,track.width-18,4),new Color(.24f,.26f,.28f));
                    Fill(new Rect(track.x+9,r.center.y-2,(track.width-18)*fraction,4),new Color(.80f,.62f,.30f));
                    var knob=new Rect(track.x+(track.width-18)*fraction+3,r.center.y-8,12,16);Fill(knob,new Color(.96f,.83f,.57f));Outline(knob,new Color(.32f,.22f,.09f),1);
                    var value=GUI.HorizontalSlider(track,row.get(),row.minimum,row.maximum,GUIStyle.none,new GUIStyle(GUIStyle.none){fixedWidth=18,fixedHeight=20});
                    if(!Mathf.Approximately(value,row.get())){row.set(value);ApplySettings();AudioListener.volume=profile.master;}
                }
                var valueRect=new Rect(r.xMax-100,r.y+10,84,38);GUI.Label(valueRect,row.Display,new GUIStyle(style){alignment=TextAnchor.MiddleRight,normal={textColor=row.toggle&&row.get()>.5f?new Color(.67f,.95f,.77f):Gold}});
                if(!row.slider&&GUI.Button(r,"",GUIStyle.none)){settingsFocusIndex=i;ChangeSetting(row,1);}
                if(hot)help=row.description;
            }
            GUI.Label(new Rect(w*.5f-440,640,880,52),help,new GUIStyle(footerStyle){fontSize=15,wordWrap=true,alignment=TextAnchor.UpperCenter,normal={textColor=new Color(.83f,.81f,.73f)}});
            var back=new Rect(w*.5f-130,h-94,260,48);DrawButtonFrame(back,controllerNavigation?settingsFocusIndex==rows.Length:back.Contains(PointerPosition),false);
            if(GUI.Button(back,"BACK TO SETTINGS",buttonStyle))BackFromSettings();
            DrawMenuNavigationHint(w,h,"↑ ↓  Select    ← →  Adjust    Q / E  Page    Esc  Return","D-pad / Stick  Select & Adjust    LB / RB  Page    B  Return");
        }
        private void DrawSettingsOverview(float w,float h)
        {
            var names=new[]{"GRAPHICS","AUDIO","GAMEPLAY","ACCESSIBILITY"};var help=new[]{"Display, performance and image quality.","Music, effects and interface volume.","Tooltips, feedback and transition speed.","Contrast, card text, motion and flashing."};
            for(var i=0;i<4;i++)
            {
                var r=new Rect(w*.5f-370+i%2*380,236+i/2*155,360,130);var hot=controllerNavigation?settingsFocusIndex==i:r.Contains(PointerPosition);
                DrawButtonFrame(r,hot,false);GUI.Label(new Rect(r.x+18,r.y+18,r.width-36,35),names[i],new GUIStyle(titleStyle){fontSize=23});
                GUI.Label(new Rect(r.x+25,r.y+67,r.width-50,45),help[i],new GUIStyle(footerStyle){fontSize=16,wordWrap=true});if(GUI.Button(r,"",GUIStyle.none))ChangeSettingsPage(i);
            }
            var back=new Rect(w*.5f-130,h-94,260,48);DrawButtonFrame(back,controllerNavigation?settingsFocusIndex==4:back.Contains(PointerPosition),false);if(GUI.Button(back,"SAVE & RETURN",buttonStyle))CloseSettings();
            DrawMenuNavigationHint(w,h,"Arrows  Select    Enter  Open    Esc  Return","D-pad / Stick  Select    A  Open    B  Return");
        }
    }
}
