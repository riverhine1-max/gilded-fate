using System;
using System.Linq;
using GildedFate.Audio;
using GildedFate.Core;
using GildedFate.Saving;
using UnityEngine;

namespace GildedFate.UI
{
    // Title-screen hubs. PLAY and ARCHIVE open a full-screen choice of large banners
    // that hang from a gold rod on strands, echoing the Fateweave. Hovering a banner
    // pulls its strand taut and lifts it. The hub stays open while you visit a
    // sub-screen, so BACK from Collection, Character Select, Records, Daily or
    // Credits returns to the hub it came from.
    public sealed partial class GildedMainMenu
    {
        private enum MenuHub { None, Play, Archive }
        private MenuHub menuHub=MenuHub.None;
        private int hubFocus;private readonly float[] hubHover=new float[6];private int hubLastHot=-1;
        private HeroId continueHero=HeroId.Vanguard;

        private sealed class HubPanel
        {
            public string kicker,title,text,footer,action;
            public Color accent;public Action<Rect> art;
        }

        private void OpenMenuHub(MenuHub hub){menuHub=hub;hubFocus=0;hubLastHot=-1;Array.Clear(hubHover,0,hubHover.Length);}
        private void CloseMenuHub(){menuHub=MenuHub.None;Sfx(SoundCue.UiBack);}

        private HubPanel[] BuildHubPanels()
        {
            var gold=new Color(.95f,.74f,.38f);
            if(menuHub==MenuHub.Play)
            {
                var list=new System.Collections.Generic.List<HubPanel>();
                var maxDebt=profile.fateDebtUnlocked==null?0:profile.fateDebtUnlocked.Max();
                list.Add(new HubPanel{kicker="THE DESCENT",title="NEW RUN",action="NEW RUN",accent=gold,
                    text="Choose one of three cursed heroes and fight down through three acts of the Gilded Vault.",
                    footer=maxDebt>0?"FATE DEBT UP TO "+FateDebt.Roman(maxDebt):"WIN A RUN TO OPEN FATE DEBT",art=HubArtHeroes});
                var date=TodayDaily;var mods=DailyModifiers(date);
                list.Add(new HubPanel{kicker=DateTime.UtcNow.ToString("dddd").ToUpperInvariant(),title="DAILY RUN",action="DAILY RUN",accent=new Color(.55f,.82f,1f),
                    text=$"One fate for everyone today: the {DailyHero(date)}, bound by {Pretty(FateDebt.Names[mods[0]-1])} and {Pretty(FateDebt.Names[mods[1]-1])}.",
                    footer=DailyCompleted(date)?"COMPLETED TODAY · PRACTICE ONLY":"SCORED ATTEMPT READY",art=HubArtDaily});
                var summary=ContinueSummary();
                if(!string.IsNullOrEmpty(summary))list.Add(new HubPanel{kicker="UNFINISHED FATE",title="CONTINUE",action="CONTINUE RUN",accent=HeroAccent(continueHero),
                    text="Pick up exactly where you left off.",footer=summary,art=r=>HubArtHero(r,continueHero)});
                return list.ToArray();
            }
            var runs=profile.runsPlayed;var ach=profile.achievements?.Count??0;
            return new[]
            {
                new HubPanel{kicker="CARDS & RELICS",title="COLLECTION",action="COLLECTION",accent=gold,text="Every card and relic in the Vault, with locks showing what Fate Marks still open.",footer=$"{GameContent.Cards.Length} CARDS · {GameContent.Relics.Length} RELICS",art=r=>HubArtTexture(r,collectionBackground)},
                new HubPanel{kicker="THE THREE",title="CHARACTERS",action="CHARACTERS",accent=new Color(.86f,.55f,1f),text="The Vanguard, the Hexer and the Reaper: their stories, decks and engines.",footer="3 HEROES",art=HubArtHeroes},
                new HubPanel{kicker="YOUR LEGEND",title="RECORDS",action="RECORDS",accent=new Color(.55f,1f,.78f),text="Achievements, run history, lifetime statistics and unlock progress.",footer=$"{ach} / {AchievementCatalog.All.Length} ACHIEVEMENTS · {runs} RUNS",art=r=>HubArtEmblems(r,"AchievementsEmblem","RunHistoryEmblem")},
                new HubPanel{kicker="THE MAKERS",title="CREDITS",action="CREDITS",accent=new Color(.8f,.78f,.72f),text="The people and the music behind Gilded Fate.",footer="MUSIC · SCOTT BUCKLEY",art=HubArtLogo},
            };
        }
        private static string Pretty(string upper)=>System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(upper.ToLowerInvariant());

        // ---------- banner art ----------
        private void HubArtTexture(Rect r,Texture2D t){if(!t)return;GUI.DrawTexture(r,t,ScaleMode.ScaleAndCrop,true);}
        private void HubArtHero(Rect r,HeroId hero)
        {
            DrawFloatingHero(new Rect(r.x+r.width*.08f,r.y+r.height*.06f,r.width*.84f,r.height*.96f),hero);
        }
        private void HubArtHeroes(Rect r)
        {
            var w=r.width*.62f;var hgt=r.height*.92f;
            DrawFloatingHero(new Rect(r.x-r.width*.08f,r.y+r.height*.12f,w,hgt*.9f),HeroId.Hexer);
            DrawFloatingHero(new Rect(r.xMax-w+r.width*.08f,r.y+r.height*.12f,w,hgt*.9f),HeroId.Reaper);
            DrawFloatingHero(new Rect(r.center.x-w*.55f,r.y+r.height*.04f,w*1.1f,hgt),HeroId.Vanguard);
        }
        private void HubArtDaily(Rect r)
        {
            HubArtHero(r,DailyHero(TodayDaily));
            var emblem=MetaArt("DailyEmblem");if(emblem)GUI.DrawTexture(new Rect(r.xMax-r.width*.36f,r.y+10,r.width*.3f,r.width*.3f),emblem,ScaleMode.ScaleToFit,true);
        }
        private void HubArtEmblems(Rect r,string a,string b)
        {
            var ta=MetaArt(a);var tb=MetaArt(b);var s=Mathf.Min(r.width*.56f,r.height*.8f);
            if(tb)GUI.DrawTexture(new Rect(r.center.x-s*.15f,r.center.y-s*.45f,s,s),tb,ScaleMode.ScaleToFit,true);
            if(ta)GUI.DrawTexture(new Rect(r.center.x-s*.85f,r.center.y-s*.55f,s,s),ta,ScaleMode.ScaleToFit,true);
        }
        private void HubArtLogo(Rect r){if(logoTexture)GUI.DrawTexture(new Rect(r.x+r.width*.06f,r.y+r.height*.2f,r.width*.88f,r.height*.5f),logoTexture,ScaleMode.ScaleToFit,true);}

        // ---------- drawing ----------
        private Rect HubPanelRect(float w,float h,int index,int count)
        {
            var gap=34f;var maxW=count<=2?430f:count==3?380f:300f;
            var pw=Mathf.Min(maxW,(w-180-(count-1)*gap)/count);var total=pw*count+gap*(count-1);
            return new Rect((w-total)*.5f+index*(pw+gap),214,pw,h-214-118);
        }
        private void DrawMenuHub(float w,float h)
        {
            MenuLiveBackdrop(w,h);Fill(new Rect(0,0,w,h),new Color(.004f,.006f,.012f,.5f));
            var play=menuHub==MenuHub.Play;
            Heading(w,play?"CHOOSE YOUR PATH":"THE ARCHIVE",play?"EVERY DESCENT BEGINS WITH A SINGLE STRAND":"EVERYTHING THE VAULT REMEMBERS");
            var panels=BuildHubPanels();hubFocus=Mathf.Clamp(hubFocus,0,panels.Length-1);
            // The rod the banners hang from, with Fateweave end caps.
            var rodY=176f;var first=HubPanelRect(w,h,0,panels.Length);var last=HubPanelRect(w,h,panels.Length-1,panels.Length);
            var rx0=first.x-46;var rx1=last.xMax+46;
            Fill(new Rect(rx0,rodY-2,rx1-rx0,4),new Color(.62f,.46f,.2f));Fill(new Rect(rx0,rodY-2,rx1-rx0,1),new Color(1f,.86f,.5f,.8f));
            HubDiamond(new Vector2(rx0,rodY),9);HubDiamond(new Vector2(rx1,rodY),9);
            var hot=-1;
            for(var i=0;i<panels.Length;i++)
            {
                var baseRect=HubPanelRect(w,h,i,panels.Length);
                var over=controllerNavigation?hubFocus==i:baseRect.Contains(PointerPosition)&&!runStartActive;
                if(over)hot=i;
                var dt=Mathf.Clamp(Time.unscaledDeltaTime,0,.1f);hubHover[i]=profile.reduceMotion?(over?1:0):Mathf.MoveTowards(hubHover[i],over?1:0,dt*5f);
                DrawHubPanel(baseRect,panels[i],hubHover[i],rodY,i);
                if(GUI.Button(baseRect,"",GUIStyle.none)){hubFocus=i;ActivateHubPanel(panels[i]);}
            }
            if(hot>=0&&hot!=hubLastHot)Sfx(SoundCue.UiHover);hubLastHot=hot;
            var back=new Rect(34,h-76,148,46);DrawButtonFrame(back,back.Contains(PointerPosition),false);if(GUI.Button(back,"BACK",buttonStyle))CloseMenuHub();
            DrawMenuNavigationHint(w,h,"← →  Choose    Enter  Pull the strand    Esc  Back","D-pad / Stick  Choose    A  Pull the strand    B  Back");
        }
        // Diamond built from stacked rows, so it stays put under the UI scale matrix.
        private void HubDiamond(Vector2 c,float s)
        {
            var half=Mathf.Max(2,Mathf.RoundToInt(s*.6f));
            for(var k=-half;k<=half;k++)
            {
                var span=(half-Mathf.Abs(k))*2+1;var edge=Mathf.Abs(k)>=half-1||true;
                Fill(new Rect(c.x-span*.5f,c.y+k,span,1),new Color(1f,.82f,.45f));
                if(span>4)Fill(new Rect(c.x-span*.5f+2,c.y+k,span-4,1),new Color(.12f,.08f,.03f));
            }
        }
        private void DrawHubPanel(Rect baseRect,HubPanel p,float hover,float rodY,int index)
        {
            var e=Mathf.SmoothStep(0,1,hover);var lift=e*12f;
            var r=new Rect(baseRect.x,baseRect.y-lift,baseRect.width,baseRect.height);
            var a=p.accent;var repaint=Event.current.type==EventType.Repaint;
            // Strands: slack when idle, pulled taut and glowing when hovered.
            var anchors=new[]{r.x+r.width*.22f,r.xMax-r.width*.22f};
            foreach(var ax in anchors)
            {
                var sway=profile.reduceMotion?0:Mathf.Sin(Time.unscaledTime*1.3f+index*1.7f+ax*.01f)*(1-e)*2.5f;
                var top=new Vector2(ax+sway,rodY+2);var bottom=new Vector2(ax,r.y);
                Fill(new Rect(top.x-1f,top.y,2f,bottom.y-top.y),Color.Lerp(new Color(.5f,.38f,.18f,.85f),new Color(1f,.84f,.48f,1f),e));
                Fill(new Rect(top.x-3,top.y-1,6,4),new Color(.85f,.64f,.3f));
                if(e>.05f&&repaint&&!profile.reduceMotion)
                {
                    var t=Mathf.Repeat(Time.unscaledTime*.9f+index*.3f,1f);var gy=Mathf.Lerp(top.y,bottom.y,t);
                    Fill(new Rect(top.x-2f,gy-4f,4f,8f),new Color(1f,.95f,.75f,.85f*e));
                }
            }
            // Banner body.
            if(e>.01f)Fill(new Rect(r.x-8,r.y-8,r.width+16,r.height+16),new Color(a.r,a.g,a.b,.10f*e));
            Fill(r,new Color(.016f,.017f,.024f,.97f));
            var art=new Rect(r.x+8,r.y+8,r.width-16,r.height*.54f);
            Fill(art,new Color(.03f,.03f,.045f,1f));
            GUI.BeginGroup(art);
            var old=GUI.color;GUI.color=new Color(1,1,1,.78f+e*.22f);
            p.art?.Invoke(new Rect(0,0,art.width,art.height));
            GUI.color=old;
            GUI.EndGroup();
            // Fade the art into the banner cloth.
            for(var s=0;s<10;s++){var t=s/9f;Fill(new Rect(art.x,art.yMax-art.height*.32f+t*art.height*.32f,art.width,art.height*.032f+1),new Color(.016f,.017f,.024f,t*.95f));}
            Outline(art,new Color(a.r,a.g,a.b,.25f+e*.45f),1);
            Outline(r,Color.Lerp(new Color(.42f,.33f,.18f,.85f),a,e),e>.5f?3:2);
            Outline(new Rect(r.x+5,r.y+5,r.width-10,r.height-10),new Color(.24f,.19f,.11f,.8f),1);
            HubDiamond(new Vector2(r.center.x,r.y),10);
            // Copy.
            var y=art.yMax-18;
            GUI.Label(new Rect(r.x+16,y,r.width-32,18),p.kicker,new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=11,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(a.r,a.g,a.b,.95f)}});y+=20;
            GUI.Label(new Rect(r.x+12,y,r.width-24,46),p.title,new GUIStyle(titleStyle){font=headingFont?headingFont:titleStyle.font,fontSize=r.width<320?30:36,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.Lerp(new Color(.95f,.9f,.78f),new Color(1f,.95f,.82f),e)}});y+=50;
            var line=Mathf.Min(150,r.width*.42f);Fill(new Rect(r.center.x-line*.5f,y,line,1),new Color(.8f,.65f,.38f,.65f));Fill(new Rect(r.center.x-3,y-3,6,6),new Color(.97f,.85f,.55f,.9f));y+=12;
            GUI.Label(new Rect(r.x+24,y,r.width-48,r.yMax-y-64),p.text,new GUIStyle(footerStyle){fontSize=15,wordWrap=true,alignment=TextAnchor.UpperCenter,normal={textColor=new Color(.84f,.82f,.76f)}});
            // Footer status plate and the pull prompt.
            var plate=new Rect(r.x+16,r.yMax-54,r.width-32,34);
            Fill(plate,new Color(.04f,.035f,.025f,.95f));Outline(plate,new Color(a.r,a.g,a.b,.35f+e*.5f),1);
            GUI.Label(plate,e>.5f?"PULL THE STRAND  ▸":p.footer,new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=12,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=e>.5f?new Color(1f,.9f,.62f):new Color(.9f,.86f,.76f)}});
            if(e>.5f&&ShowPadGlyphs)DrawPadGlyph(new Vector2(plate.x+22,plate.center.y),"A",true,20);
            // Tassel fringe.
            for(var fx=r.x+10;fx<r.xMax-10;fx+=9)Fill(new Rect(fx,r.yMax,1.5f,6+((int)(fx*.37f)%3)),new Color(.6f,.46f,.22f,.75f));
        }
        private void ActivateHubPanel(HubPanel p){Sfx(SoundCue.UiConfirm);ActivateMenuItem(p.action);}
        private void HandleMenuHubNavigation(MenuNavigation input)
        {
            if(input.back){CloseMenuHub();return;}
            var panels=BuildHubPanels();var delta=input.x!=0?input.x:input.y;
            if(delta!=0){hubFocus=(hubFocus+delta+panels.Length)%panels.Length;Sfx(SoundCue.UiHover);}
            if(input.accept&&panels.Length>0)ActivateHubPanel(panels[Mathf.Clamp(hubFocus,0,panels.Length-1)]);
        }
    }
}
