using System.Collections.Generic;
using System.Linq;
using GildedFate.Audio;
using GildedFate.Core;
using GildedFate.Map;
using GildedFate.Saving;
using UnityEngine;

namespace GildedFate.UI
{
    // The pre-act Fateweave, "pull one, cut one". Three strands hang from a great
    // loom: the player pulls one for its boon, then cuts one of the two left behind
    // for gold. The last strand frays away. Everything here is procedural IMGUI and
    // UI-local animation state; the rules live in RunModel.
    public sealed partial class GildedMainMenu
    {
        private struct FateweavePalette { public Color strand,glow,backdrop,accent,deep; }

        private const float FateweaveLoomRadius=104f,FateweaveBeamY=232f,FateweaveTapestryTop=340f,FateweaveTapestryWidth=252f;
        private readonly float[] fateweaveHover=new float[8];
        private string fateweaveEntranceKey="",fateweaveCutPhaseKey="",fateweaveSealedKey="",fateweaveCutAnimKey="",fateweaveFrayId="";
        private float fateweaveEntranceStart=-10f,fateweaveCutPhaseStart=-10f,fateweaveSealedStart=-10f,fateweaveCutStarted=-10f;
        private int fateweaveCutAnimGold;
        private Rect fateweavePullFrom,fateweaveCutFrom;
        private Texture2D fateweaveGlow,fateweaveWeave;

        // Act I dawn gold, Act II violet dusk, Act III crimson.
        private static FateweavePalette FateweaveColors(int act)=>Mathf.Clamp(act,1,3) switch
        {
            1=>new FateweavePalette{strand=new Color(1f,.78f,.38f),glow=new Color(1f,.60f,.22f),backdrop=new Color(.028f,.017f,.008f),accent=new Color(1f,.89f,.60f),deep=new Color(.21f,.12f,.04f)},
            2=>new FateweavePalette{strand=new Color(.84f,.64f,1f),glow=new Color(.58f,.32f,.96f),backdrop=new Color(.015f,.008f,.030f),accent=new Color(.92f,.82f,1f),deep=new Color(.14f,.06f,.23f)},
            _=>new FateweavePalette{strand=new Color(1f,.44f,.37f),glow=new Color(.92f,.16f,.14f),backdrop=new Color(.028f,.005f,.008f),accent=new Color(1f,.76f,.66f),deep=new Color(.23f,.035f,.05f)}
        };
        private static Color FateweaveTint(Color color,float alpha)=>new Color(color.r,color.g,color.b,Mathf.Clamp01(alpha));
        private string FateweaveRoundKey=>run.runId+"/"+run.act;
        private string FateweaveCutKey=>FateweaveRoundKey+"/"+run.fateweaveCutId;
        private float FateweaveCutDuration=>profile.reduceMotion?.55f:profile.fastMode?1.05f:1.6f;
        private bool FateweaveCutAnimating=>run.FateweaveCutDone&&fateweaveCutAnimKey==FateweaveCutKey&&Time.unscaledTime-fateweaveCutStarted<FateweaveCutDuration;
        // Seconds on the authored 1.6s cut timeline, whatever the playback speed.
        private float FateweaveCutClock=>(Time.unscaledTime-fateweaveCutStarted)*(1.6f/FateweaveCutDuration);

        // ---------- layout ----------
        private FateweaveDef[] FateweaveOfferDefs()=>run.fateweaveOffers==null?new FateweaveDef[0]:run.fateweaveOffers.Select(id=>WorldContent.Fateweaves.FirstOrDefault(f=>f.id==id)).Where(f=>f!=null).ToArray();
        private static Vector2 FateweaveLoomCenter(float w)=>new Vector2(w*.5f,112f);
        private static float FateweaveTapestryHeight(float h)=>Mathf.Clamp(h-480f,290f,380f);
        private static float FateweaveSlotX(float w,int index,int count)=>w*.5f+(index-(count-1)*.5f)*Mathf.Min(298f,(w-80f)/Mathf.Max(1,count));
        private static Rect FateweaveTapestryRect(float w,float h,int index,int count)=>new Rect(FateweaveSlotX(w,index,count)-FateweaveTapestryWidth*.5f,FateweaveTapestryTop,FateweaveTapestryWidth,FateweaveTapestryHeight(h));
        private static Vector2 FateweavePeg(float w,int index,int count)=>new Vector2(FateweaveSlotX(w,index,count),FateweaveBeamY+5f);
        private Rect FateweaveSlotRect(string id)
        {
            var offers=FateweaveOfferDefs();var index=System.Array.FindIndex(offers,f=>f.id==id);
            if(index<0)return new Rect(CombatWidth*.5f-FateweaveTapestryWidth*.5f,FateweaveTapestryTop,FateweaveTapestryWidth,FateweaveTapestryHeight(CombatHeight));
            var r=FateweaveTapestryRect(CombatWidth,CombatHeight,index,offers.Length);
            if(!profile.reduceMotion&&index<fateweaveHover.Length)r.y-=fateweaveHover[index]*10f;
            return r;
        }
        private float FateweaveDescent(float start,int order)
        {
            var elapsed=Time.unscaledTime-start;if(profile.reduceMotion)return Mathf.Clamp01(elapsed/.3f);
            var x=Mathf.Clamp01((elapsed-.08f-order*.12f)/.7f);return 1-(1-x)*(1-x)*(1-x);
        }
        private float FateweaveSwayX(int index,float hover)=>profile.reduceMotion?0:(1-hover)*Mathf.Sin(Time.unscaledTime*1.05f+index*2.1f)*5.5f;
        private float FateweaveYank(float t)
        {
            if(profile.reduceMotion)return 0;var x=Mathf.Clamp01(t/.16f)-1;return 58f*(1+2.70158f*x*x*x+1.70158f*x*x);
        }
        private void UpdateFateweaveHover(int hot,int count)
        {
            if(Event.current.type!=EventType.Repaint)return;var step=profile.reduceMotion?1f:Time.unscaledDeltaTime*7f;
            for(var i=0;i<fateweaveHover.Length;i++)fateweaveHover[i]=Mathf.MoveTowards(fateweaveHover[i],i==hot&&i<count?1f:0f,step);
        }
        private float FateweaveFocus(int count){var most=0f;for(var i=0;i<Mathf.Min(count,fateweaveHover.Length);i++)most=Mathf.Max(most,fateweaveHover[i]);return most;}

        // ---------- actions shared by mouse, controller and the trailer harness ----------
        private void PullFateweaveStrand(FateweaveDef fate)
        {
            if(fate==null||acquisitionActive)return;
            var cardsBefore=SnapshotCardCopies();var relicsBefore=SnapshotRelics();
            if(!run.SelectFateweave(fate.id))return;
            SaveService.Save(run);
            BeginFateweavePull(fate,()=>{screenControllerIndex=0;screen=run.stage==RunStage.FateweaveCard?ScreenMode.FateweaveCard:ScreenMode.Fateweave;PresentNewRunAcquisitions(cardsBefore,relicsBefore);});
        }
        private bool CutFateweaveStrand(string id,bool animate=true)
        {
            if(acquisitionActive||FateweaveCutAnimating||!run.FateweaveAwaitingCut)return false;
            var remaining=run.FateweaveCutCandidates();var gain=run.FateweaveCutGold(id);var from=FateweaveSlotRect(id);
            if(!run.CutFateweave(id))return false;
            // The loom plays its own coin flight and cue; keep the generic gold
            // flight and gold audio from firing a second time.
            observedGold=run.gold;ResetGoldAudio();profile.goldCollected+=gain;ProfileService.Save(profile);SaveService.Save(run);
            fateweaveFrayId=remaining.FirstOrDefault(other=>other!=id)??"";fateweaveCutAnimGold=gain;fateweaveCutFrom=from;
            if(!animate){fateweaveCutAnimKey="";Sfx(SoundCue.GoldCollect,pan:-.25f);return true;}
            fateweaveCutAnimKey=FateweaveCutKey;fateweaveCutStarted=Time.unscaledTime;
            var pan=Mathf.Clamp(from.center.x/Mathf.Max(1f,CombatWidth)*2f-1f,-1f,1f)*.4f;
            Sfx(SoundCue.AttackSteel,pan:pan);Sfx(SoundCue.EventThread,pan:pan,delay:.09f);
            Sfx(SoundCue.GoldCollect,pan:-.25f,delay:profile.reduceMotion?.12f:FateweaveCutDuration*.62f);
            return true;
        }
        private void EnterFateweaveAct()
        {
            if(acquisitionActive||run.FateweaveAwaitingCut)return;
            if(!run.CompleteFateweave())return;
            SaveService.Save(run);mapFocusFloor=-1;screen=ScreenMode.Map;
        }
        // Continue restores the exact Fateweave step: pull, card choice, or cut.
        private void ResumeFateweave()
        {
            run.EnsureFateweaveState();rewardRevealTime=0;
            if(run.stage==RunStage.FateweaveCard&&!string.IsNullOrEmpty(run.pendingFateweaveId)&&run.pendingChoicesNeeded>0){screen=ScreenMode.FateweaveCard;return;}
            run.stage=RunStage.Fateweave;screen=ScreenMode.Fateweave;
        }
        private void HandleControllerFateweave(int delta,bool accept)
        {
            if(acquisitionActive)return;
            if(FateweaveCutAnimating){if(accept)fateweaveCutStarted=Time.unscaledTime-FateweaveCutDuration;return;}
            var resolved=!string.IsNullOrEmpty(run.pendingFateweaveId)&&run.pendingChoicesNeeded==0;
            if(resolved&&run.FateweaveAwaitingCut)
            {
                var remaining=run.FateweaveCutCandidates();if(remaining.Count==0)return;
                if(delta!=0)MoveScreenController(delta,remaining.Count);screenControllerIndex=Mathf.Clamp(screenControllerIndex,0,remaining.Count-1);
                if(accept)CutFateweaveStrand(remaining[screenControllerIndex]);return;
            }
            var offers=FateweaveOfferDefs();
            if(resolved||offers.Length==0){if(accept)EnterFateweaveAct();return;}
            if(delta!=0)MoveScreenController(delta,offers.Length);screenControllerIndex=Mathf.Clamp(screenControllerIndex,0,offers.Length-1);
            if(accept)PullFateweaveStrand(offers[screenControllerIndex]);
        }

        // ---------- screen ----------
        private void DrawFateweave(float w,float h)
        {
            DrawFateweaveVoid(w,h);
            var pal=FateweaveColors(run.act);
            if(fateweaveEntranceKey!=FateweaveRoundKey){fateweaveEntranceKey=FateweaveRoundKey;fateweaveEntranceStart=Time.unscaledTime;for(var i=0;i<fateweaveHover.Length;i++)fateweaveHover[i]=0;}
            var offers=FateweaveOfferDefs();
            var resolved=!FateweavePullActive&&!string.IsNullOrEmpty(run.pendingFateweaveId)&&run.pendingChoicesNeeded==0;
            for(var i=0;i<offers.Length;i++)DrawFateweavePeg(FateweavePeg(w,i,offers.Length),pal);
            if(resolved&&(run.FateweaveAwaitingCut||FateweaveCutAnimating))DrawFateweaveCutPhase(w,h,pal,offers);
            else if(resolved||offers.Length==0&&!FateweavePullActive)DrawFateweaveSealed(w,h,pal);
            else DrawFateweavePullPhase(w,h,pal,offers);
        }
        private void DrawFateweavePullPhase(float w,float h,FateweavePalette pal,FateweaveDef[] offers)
        {
            Heading(w,"THE FATEWEAVE",$"BEFORE ACT {RomanAct(run.act)} · PULL ONE STRAND · CUT ANOTHER FOR GOLD");
            var count=offers.Length;var pulling=FateweavePullActive?acquisitionFateweave?.id:null;
            var pullT=FateweavePullActive?Mathf.Clamp01((Time.unscaledTime-acquisitionStarted)/Mathf.Max(.01f,acquisitionDuration)):0;
            var hot=-1;if(!acquisitionActive)for(var i=0;i<count;i++)if(ScreenChoiceHot(FateweaveTapestryRect(w,h,i,count),i))hot=i;
            UpdateFateweaveHover(hot,count);var focus=FateweaveFocus(count);
            for(var i=0;i<count;i++)
            {
                var fate=offers[i];var baseRect=FateweaveTapestryRect(w,h,i,count);var hover=i<fateweaveHover.Length?fateweaveHover[i]:0;
                var chosen=pulling!=null&&fate.id==pulling;var descent=FateweaveDescent(fateweaveEntranceStart,i);
                var alpha=chosen?1-Mathf.Clamp01((pullT-.45f)/.2f):pulling!=null?FateweaveChoiceOpacity:1;
                alpha*=profile.reduceMotion?descent:Mathf.Clamp01(descent*2.5f);alpha*=Mathf.Lerp(1,.46f,Mathf.Clamp01(focus-hover));
                if(alpha>.01f)
                {
                    var unfurl=profile.reduceMotion?1:Mathf.SmoothStep(0,1,Mathf.Clamp01((descent-.25f)/.75f));
                    var r=new Rect(baseRect.x+(chosen?0:FateweaveSwayX(i,hover)),baseRect.y-(1-descent)*46f-(profile.reduceMotion?0:hover*10f)+(chosen?FateweaveYank(pullT):0),baseRect.width,baseRect.height*Mathf.Lerp(.1f,1,unfurl));
                    var peg=FateweavePeg(w,i,count);var apex=new Vector2(r.center.x,Mathf.Max(peg.y+4,r.y-24));var tension=chosen?1:hover;
                    DrawFateweaveFiber(w,peg,pal,alpha,tension,i*1.9f);
                    DrawFateweaveRope(peg,apex,pal,alpha,tension,i*1.9f,chosen?4.5f:3f+hover*1.4f);
                    DrawFateweaveRopeMotes(peg,apex,pal,alpha,hover,i*1.9f);
                    // The chosen tapestry itself is drawn by the pull cinematic as it unravels.
                    if(!chosen)DrawFateweaveTapestry(r,fate,pal,alpha,hover,"PULL THIS STRAND",0,alpha*Mathf.Clamp01((unfurl-.82f)/.18f));
                }
                if(!acquisitionActive&&GUI.Button(baseRect,"",GUIStyle.none))PullFateweaveStrand(fate);
            }
        }
        private void DrawFateweaveCutPhase(float w,float h,FateweavePalette pal,FateweaveDef[] offers)
        {
            var now=Time.unscaledTime;var animating=FateweaveCutAnimating;var count=offers.Length;var remaining=run.FateweaveCutCandidates();
            var key=FateweaveRoundKey+"/cut/"+run.pendingFateweaveId;
            if(fateweaveCutPhaseKey!=key){fateweaveCutPhaseKey=key;fateweaveCutPhaseStart=now;screenControllerIndex=0;}
            if(animating)Heading(w,"THE FATEWEAVE","THE SEVERED STRAND TURNS TO GOLD");
            else Heading(w,"THE FATEWEAVE",$"BEFORE ACT {RomanAct(run.act)} · STRAND PULLED · NOW CUT ANOTHER FOR GOLD");
            var hot=-1;
            if(!animating&&!acquisitionActive)for(var i=0;i<count;i++){var order=remaining.IndexOf(offers[i].id);if(order>=0&&ScreenChoiceHot(FateweaveTapestryRect(w,h,i,count),order))hot=i;}
            UpdateFateweaveHover(hot,count);var focus=FateweaveFocus(count);
            for(var i=0;i<count;i++)
            {
                var fate=offers[i];var baseRect=FateweaveTapestryRect(w,h,i,count);var peg=FateweavePeg(w,i,count);var phase=i*1.9f;
                if(fate.id==run.pendingFateweaveId){DrawFateweavePulledSeal(peg,fate,pal,Mathf.Clamp01((now-fateweaveCutPhaseStart)/.5f));continue;}
                if(animating&&fate.id==run.fateweaveCutId){DrawFateweaveSeverance(peg,fate,pal,phase,i);continue;}
                if(animating&&fate.id==fateweaveFrayId){DrawFateweaveFraying(w,peg,baseRect,fate,pal,phase,i);continue;}
                var order=remaining.IndexOf(fate.id);if(order<0)continue;
                var hover=i<fateweaveHover.Length?fateweaveHover[i]:0;var descent=FateweaveDescent(fateweaveCutPhaseStart,order);
                var alpha=(profile.reduceMotion?descent:Mathf.Clamp01(descent*2.5f))*Mathf.Lerp(1,.46f,Mathf.Clamp01(focus-hover));
                var unfurl=profile.reduceMotion?1:Mathf.SmoothStep(0,1,Mathf.Clamp01((descent-.25f)/.75f));
                var r=new Rect(baseRect.x+FateweaveSwayX(i,hover),baseRect.y-(1-descent)*46f-(profile.reduceMotion?0:hover*10f),baseRect.width,baseRect.height*Mathf.Lerp(.1f,1,unfurl));
                var apex=new Vector2(r.center.x,Mathf.Max(peg.y+4,r.y-24));var gold=run.FateweaveCutGold(fate.id);
                DrawFateweaveFiber(w,peg,pal,alpha,hover,phase);DrawFateweaveRope(peg,apex,pal,alpha,hover,phase,3f+hover*1.4f);DrawFateweaveRopeMotes(peg,apex,pal,alpha,hover,phase);
                DrawFateweaveTapestry(r,fate,pal,alpha,hover,$"CUT · +{gold} GOLD",gold,alpha*Mathf.Clamp01((unfurl-.82f)/.18f));
                if(!animating&&!acquisitionActive&&GUI.Button(baseRect,"",GUIStyle.none))CutFateweaveStrand(fate.id);
            }
            if(animating){DrawFateweaveCoins(pal);return;}
            if(hot>=0&&!acquisitionActive)
            {
                var fate=offers[hot];var r=FateweaveTapestryRect(w,h,hot,count);var gold=run.FateweaveCutGold(fate.id);
                DrawTooltip(new Rect(r.xMax+14,r.y+36,300,150),$"CUT · +{gold} GOLD",$"Sever {fate.name}: its boon is lost and the strand turns into {gold} Gold. The other strand frays away.\nGold can be spent to Gild cards in combat.",r);
            }
        }
        private void DrawFateweaveSealed(float w,float h,FateweavePalette pal)
        {
            var now=Time.unscaledTime;var pulled=WorldContent.Fateweaves.FirstOrDefault(f=>f.id==run.pendingFateweaveId);
            var key=FateweaveRoundKey+"/sealed/"+run.pendingFateweaveId+"/"+run.fateweaveCutId;
            if(fateweaveSealedKey!=key){fateweaveSealedKey=key;fateweaveSealedStart=now;}
            var appear=profile.reduceMotion?1:Mathf.SmoothStep(0,1,Mathf.Clamp01((now-fateweaveSealedStart)/.45f));
            if(pulled==null)Heading(w,"THE FATEWEAVE",$"NO STRANDS DESCEND BEFORE ACT {RomanAct(run.act)}");
            else if(run.FateweaveCutDone)Heading(w,"FATE ALTERED",$"THE SEVERED STRAND TURNS TO GOLD · +{run.FateweaveCutGold(run.fateweaveCutId)} GOLD");
            else Heading(w,"FATE ALTERED",pulled.name);
            var bottom=FateweaveTapestryTop;
            if(pulled!=null)
            {
                var peg=new Vector2(w*.5f,FateweaveBeamY+5f);DrawFateweavePeg(peg,pal);
                var r=new Rect(w*.5f-150,FateweaveTapestryTop-(1-appear)*30f,300,Mathf.Min(FateweaveTapestryHeight(h),300f));
                DrawFateweaveGlowRect(new Rect(r.x-130,r.y-90,r.width+260,r.height+180),pal.glow,(profile.reducedVfx?.12f:.20f)*appear*(.9f+.1f*Mathf.Sin(now*1.5f)));
                DrawFateweaveFiber(w,peg,pal,appear,.4f,0);DrawFateweaveRope(peg,new Vector2(r.center.x,r.y-24),pal,appear,.3f,0,3.4f);
                DrawFateweaveTapestry(r,pulled,pal,appear,.6f,"WOVEN INTO YOUR FATE",0,appear);
                bottom=r.yMax+22;
            }
            var next=new Rect(w*.5f-170,Mathf.Min(bottom+18,h-112),340,56);var enabled=!acquisitionActive&&!run.FateweaveAwaitingCut;
            DrawButtonFrame(next,enabled&&(next.Contains(PointerPosition)||controllerNavigation),!enabled);GUI.enabled=enabled;
            if(GUI.Button(next,"ENTER ACT "+RomanAct(run.act),buttonStyle))EnterFateweaveAct();
            GUI.enabled=true;
        }

        // ---------- backdrop and loom ----------
        private void DrawFateweaveVoid(float w,float h)
        {
            var pal=FateweaveColors(run.act);Fill(new Rect(0,0,w,h),pal.backdrop);
            // No room art, horizon or floor: the strands exist outside the Vault.
            if(Event.current.type==EventType.Repaint)
            {
                var now=Time.unscaledTime;var motion=profile.reduceMotion?0f:1f;
                DrawFateweaveGlowRect(new Rect(w*.5f-640,-320,1280,920),pal.glow,profile.reducedVfx?.08f:.14f);
                if(!profile.reducedVfx&&combatVfxAtlas){var previous=GUI.color;GUI.color=FateweaveTint(pal.glow,.07f);DrawAtlasIcon(combatVfxAtlas,4,4,2,new Rect(w*.5f-470,h*.5f-370,940,740));GUI.color=previous;}
                for(var i=0;i<22;i++)
                {
                    var x=Mathf.Repeat(i*137.71f+57,w);var sway=Mathf.Sin(now*.12f+i)*5*motion;
                    DrawLine(new Vector2(x+sway,0),new Vector2(x+Mathf.Sin(i)*35,h),FateweaveTint(pal.strand,.022f+i%4*.007f),1);
                }
                var motes=profile.reducedVfx?10:28;
                for(var i=0;i<motes;i++)
                {
                    var x=Mathf.Repeat(i*211.3f+Mathf.Sin(now*.2f+i)*24*motion,w);var y=h+20-Mathf.Repeat(now*(6+i%5*3)*motion+i*97.3f,h+40);
                    Fill(new Rect(x,y,2,2),FateweaveTint(pal.accent,.06f+.2f*(.5f+.5f*Mathf.Sin(now*1.7f*motion+i*2.1f))));
                }
                for(var i=0;i<6;i++)Fill(new Rect(0,h-(i+1)*38,w,38),new Color(0,0,0,.05f));
            }
            DrawFateweaveLoom(w,pal);
            DrawRunHud(w);
        }
        // A slowly turning great wheel behind the heading, seated on the loom beam.
        private void DrawFateweaveLoom(float w,FateweavePalette pal)
        {
            if(Event.current.type!=EventType.Repaint)return;
            var c=FateweaveLoomCenter(w);const float R=FateweaveLoomRadius;var now=Time.unscaledTime;var spin=profile.reduceMotion?0:now*.16f;
            var energy=FateweaveFocus(fateweaveHover.Length);
            DrawFateweaveGlow(c,R*4.4f,pal.glow,(profile.reducedVfx?.10f:.17f)*(1+energy*.5f));
            for(var k=0;k<48;k++)
            {
                var a0=k*Mathf.PI*2/48;var a1=(k+1)*Mathf.PI*2/48;var lower=Mathf.Clamp01(Mathf.Sin((a0+a1)*.5f)*.8f+.35f);
                DrawLine(c+new Vector2(Mathf.Cos(a0),Mathf.Sin(a0))*R,c+new Vector2(Mathf.Cos(a1),Mathf.Sin(a1))*R,FateweaveTint(Color.Lerp(pal.strand,pal.accent,.3f),.18f+.42f*lower),3);
                DrawLine(c+new Vector2(Mathf.Cos(a0),Mathf.Sin(a0))*(R-9),c+new Vector2(Mathf.Cos(a1),Mathf.Sin(a1))*(R-9),FateweaveTint(pal.strand,.10f+.22f*lower),1.2f);
            }
            for(var k=0;k<10;k++)
            {
                var a=spin+k*Mathf.PI*2/10;var dir=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var lower=Mathf.Clamp01(dir.y*.8f+.4f);
                DrawLine(c+dir*30,c+dir*(R-9),FateweaveTint(pal.strand,.10f+.18f*lower),2);Fill(new Rect(c.x+dir.x*R*.62f-2,c.y+dir.y*R*.62f-2,4,4),FateweaveTint(pal.accent,.2f+.3f*lower));
            }
            for(var k=0;k<20;k++){var a=spin+k*Mathf.PI*2/20+.157f;var dir=new Vector2(Mathf.Cos(a),Mathf.Sin(a));DrawLine(c+dir*(R+1),c+dir*(R+8),FateweaveTint(pal.accent,.16f+.3f*Mathf.Clamp01(dir.y+.3f)),2);}
            // Thread wound around the hub, dashed so the spool visibly turns.
            for(var ring=0;ring<5;ring++)
            {
                var radius=12+ring*4f;var offset=spin*(ring%2==0?2.2f:-1.6f);
                for(var k=0;k<18;k++){var a0=offset+k*Mathf.PI*2/18;var a1=a0+Mathf.PI*2/18;DrawLine(c+new Vector2(Mathf.Cos(a0),Mathf.Sin(a0))*radius,c+new Vector2(Mathf.Cos(a1),Mathf.Sin(a1))*radius,FateweaveTint(k%2==0?pal.strand:pal.accent,(k%2==0?.42f:.22f)-ring*.04f),1.6f);}
            }
            DrawFateweaveGlow(c,68,pal.accent,.35f+.15f*Mathf.Sin(now*1.3f));Fill(new Rect(c.x-3,c.y-3,6,6),FateweaveTint(pal.accent,.9f));
            // Keep the heading readable: the wheel emerges from shadow below the title.
            DrawFateweaveGlowRect(new Rect(c.x-380,40,760,170),Color.black,.58f);
            // Loose fibres spooling off the rim.
            if(!profile.reducedVfx&&!profile.reduceMotion)for(var k=0;k<6;k++)
            {
                var a0=spin*1.4f+k*1.05f;var previous=c+new Vector2(Mathf.Cos(a0),Mathf.Sin(a0))*R;
                for(var j=1;j<=6;j++){var a=a0-j*.09f;var p=c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(R+j*j*1.3f);DrawLine(previous,p,FateweaveTint(pal.strand,.3f*(1-j/7f)),1.1f);previous=p;}
            }
            // Mount and beam.
            var gold=new Color(.95f,.72f,.34f);
            DrawLine(new Vector2(c.x-24,c.y+R-10),new Vector2(c.x-40,FateweaveBeamY),FateweaveTint(gold,.55f),3);DrawLine(new Vector2(c.x+24,c.y+R-10),new Vector2(c.x+40,FateweaveBeamY),FateweaveTint(gold,.55f),3);
            var x0=c.x-470;var x1=c.x+470;
            if(!profile.reducedVfx)DrawLine(new Vector2(x0,FateweaveBeamY),new Vector2(x1,FateweaveBeamY),FateweaveTint(pal.glow,.10f),12);
            Fill(new Rect(x0,FateweaveBeamY-4,x1-x0,8),new Color(.15f,.09f,.035f,.96f));Fill(new Rect(x0,FateweaveBeamY-4,x1-x0,2),FateweaveTint(gold,.78f));Fill(new Rect(x0,FateweaveBeamY+3,x1-x0,1),new Color(0,0,0,.5f));
            foreach(var x in new[]{x0-9,x1+9})
            {
                var p=new Vector2(x,FateweaveBeamY);DrawLine(p+new Vector2(-8,0),p+new Vector2(0,-8),gold,2);DrawLine(p+new Vector2(0,-8),p+new Vector2(8,0),gold,2);DrawLine(p+new Vector2(8,0),p+new Vector2(0,8),gold,2);DrawLine(p+new Vector2(0,8),p+new Vector2(-8,0),gold,2);Fill(new Rect(p.x-2,p.y-2,4,4),FateweaveTint(pal.accent,.9f));
            }
        }
        private void DrawFateweavePeg(Vector2 peg,FateweavePalette pal)
        {
            Fill(new Rect(peg.x-6,FateweaveBeamY-8,12,14),new Color(.34f,.21f,.08f,1));Fill(new Rect(peg.x-6,FateweaveBeamY-8,12,3),new Color(1f,.82f,.45f,.9f));Fill(new Rect(peg.x-2,peg.y-2,4,4),FateweaveTint(pal.accent,.8f));
        }
        // The taut fibre from the wheel's lower rim to a strand's peg on the beam.
        private void DrawFateweaveFiber(float w,Vector2 peg,FateweavePalette pal,float alpha,float tension,float phase)
        {
            if(alpha<=.01f||Event.current.type!=EventType.Repaint)return;
            var c=FateweaveLoomCenter(w);var dx=peg.x-c.x;var angle=Mathf.Abs(dx)<24?Mathf.PI*.5f:Mathf.Clamp(Mathf.Atan2(peg.y-c.y,dx),.70f,2.44f);
            var start=c+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*FateweaveLoomRadius;var control=new Vector2((start.x+peg.x)*.5f,Mathf.Max(start.y,peg.y)+10);
            Vector2 At(float s)=>(1-s)*(1-s)*start+2*(1-s)*s*control+s*s*peg;
            var color=FateweaveTint(Color.Lerp(pal.strand,pal.accent,tension*.5f),(.45f+.4f*tension)*alpha);var previous=start;
            for(var k=1;k<=10;k++){var p=At(k/10f);DrawLine(previous,p,color,1.6f+tension);DrawLine(previous+Vector2.up*2,p+Vector2.up*2,FateweaveTint(pal.strand,.22f*alpha),1f);previous=p;}
            if(profile.reduceMotion||tension<=.05f)return;
            var count=profile.reducedVfx?1:3;var now=Time.unscaledTime;
            for(var k=0;k<count;k++){var s=Mathf.Repeat(now*.9f+k/(float)count+phase*.1f,1f);var p=At(s);Fill(new Rect(p.x-1.5f,p.y-1.5f,3,3),FateweaveTint(pal.accent,tension*alpha*Mathf.Sin(s*Mathf.PI)));}
        }
        private static Vector2 FateweaveRopePoint(Vector2 top,Vector2 bottom,Vector2 normal,float s,float tension,float phase,float now,float motion)
        {
            var bend=Mathf.Sin(s*Mathf.PI);var sway=Mathf.Sin(now*1.6f+phase+s*2.7f)*7f*(1-tension)*motion;var hum=Mathf.Sin(now*57f+phase*3f+s*19f)*1.5f*tension*motion;
            return Vector2.Lerp(top,bottom,s)+normal*(bend*(sway+hum));
        }
        // A thick glowing rope: loose and swaying at rest, drawn straight and humming under tension.
        private void DrawFateweaveRope(Vector2 top,Vector2 bottom,FateweavePalette pal,float alpha,float tension,float phase,float thickness)
        {
            if(alpha<=.01f||Event.current.type!=EventType.Repaint||(bottom-top).sqrMagnitude<1)return;
            var now=Time.unscaledTime;var motion=profile.reduceMotion?0f:1f;var normal=new Vector2(-(bottom.y-top.y),bottom.x-top.x).normalized;const int segments=16;
            var glow=FateweaveTint(pal.glow,.16f*alpha*(.6f+tension*.6f));var core=FateweaveTint(Color.Lerp(pal.strand,pal.accent,tension*.55f),alpha*(.72f+.28f*tension));var twist=FateweaveTint(pal.accent,.5f*alpha);
            var previous=top;
            for(var i=1;i<=segments;i++)
            {
                var p=FateweaveRopePoint(top,bottom,normal,i/(float)segments,tension,phase,now,motion);
                if(!profile.reducedVfx)DrawLine(previous,p,glow,thickness*3.2f);
                DrawLine(previous,p,core,thickness);
                if(i%2==0)DrawLine(previous+normal*thickness*.3f,p-normal*thickness*.3f,twist,Mathf.Max(1,thickness*.35f));
                previous=p;
            }
        }
        private void DrawFateweaveRopeMotes(Vector2 top,Vector2 bottom,FateweavePalette pal,float alpha,float tension,float phase)
        {
            if(profile.reduceMotion||alpha<=.05f||tension<=.05f||Event.current.type!=EventType.Repaint)return;
            var now=Time.unscaledTime;var normal=new Vector2(-(bottom.y-top.y),bottom.x-top.x).normalized;var count=profile.reducedVfx?2:5;
            for(var k=0;k<count;k++)
            {
                var s=Mathf.Repeat(now*.85f+k/(float)count,1f);var p=FateweaveRopePoint(top,bottom,normal,s,tension,phase,now,1);var a=alpha*tension*Mathf.Sin(s*Mathf.PI);
                if(!profile.reducedVfx)DrawFateweaveGlow(p,20,pal.accent,a*.55f);Fill(new Rect(p.x-2,p.y-2,4,4),new Color(1f,.95f,.8f,a));
            }
        }

        // ---------- tapestries ----------
        private void DrawFateweaveTapestryShell(Rect r,FateweavePalette pal,float alpha,float hot,bool fringe)
        {
            if(alpha<=.01f)return;var now=Time.unscaledTime;var gold=new Color(1f,.80f,.42f);
            if(hot>.01f)DrawFateweaveGlowRect(new Rect(r.x-70,r.y-50,r.width+140,r.height+100),pal.glow,.20f*hot*alpha);
            var apex=new Vector2(r.center.x,r.y-24);var hang=FateweaveTint(pal.strand,.85f*alpha);
            DrawLine(apex,new Vector2(r.x-4,r.y-1),hang,1.6f);DrawLine(apex,new Vector2(r.xMax+4,r.y-1),hang,1.6f);
            Fill(new Rect(r.x-9,r.y+12,r.width+18,r.height+8),new Color(0,0,0,.42f*alpha));
            Fill(r,new Color(pal.deep.r*.34f+.006f,pal.deep.g*.34f+.006f,pal.deep.b*.34f+.010f,.97f*alpha));
            var previous=GUI.color;GUI.color=new Color(pal.strand.r*.6f,pal.strand.g*.55f,pal.strand.b*.55f,.15f*alpha);GUI.DrawTextureWithTexCoords(r,FateweaveWeaveTexture,new Rect(0,0,r.width/12f,r.height/12f),true);GUI.color=previous;
            Fill(new Rect(r.x,r.y,r.width,r.height*.3f),FateweaveTint(pal.glow,(.05f+.05f*hot)*alpha));
            // Woven border: two offset rows of dashes read as an over-under weave.
            const float band=11;var bandColor=FateweaveTint(pal.deep,.92f*alpha);var dashA=FateweaveTint(gold,.55f*alpha);var dashB=FateweaveTint(pal.strand,.45f*alpha);
            Fill(new Rect(r.x,r.y,r.width,band),bandColor);Fill(new Rect(r.x,r.yMax-band,r.width,band),bandColor);Fill(new Rect(r.x,r.y+band,band,r.height-band*2),bandColor);Fill(new Rect(r.xMax-band,r.y+band,band,r.height-band*2),bandColor);
            for(var x=r.x+4;x<r.xMax-9;x+=10){Fill(new Rect(x,r.y+3,5,2.5f),dashA);Fill(new Rect(x+5,r.y+6.5f,5,2.5f),dashB);if(r.height>30){Fill(new Rect(x,r.yMax-5.5f,5,2.5f),dashA);Fill(new Rect(x+5,r.yMax-9,5,2.5f),dashB);}}
            for(var y=r.y+14;y<r.yMax-19;y+=10){Fill(new Rect(r.x+3,y,2.5f,5),dashA);Fill(new Rect(r.x+6.5f,y+5,2.5f,5),dashB);Fill(new Rect(r.xMax-5.5f,y,2.5f,5),dashA);Fill(new Rect(r.xMax-9,y+5,2.5f,5),dashB);}
            Outline(r,hot>.5f?FateweaveTint(Gold,alpha):FateweaveTint(new Color(.58f,.45f,.24f),alpha),hot>.5f?3:2);
            if(r.height>40){Outline(new Rect(r.x+12,r.y+12,r.width-24,r.height-24),FateweaveTint(gold,.35f*alpha),1);foreach(var p in new[]{new Vector2(r.x+12,r.y+12),new Vector2(r.xMax-12,r.y+12),new Vector2(r.x+12,r.yMax-12),new Vector2(r.xMax-12,r.yMax-12)})Fill(new Rect(p.x-3,p.y-3,6,6),FateweaveTint(pal.accent,.7f*alpha));}
            // Hanging rod.
            Fill(new Rect(r.x-14,r.y-5,r.width+28,8),new Color(.26f,.16f,.06f,alpha));Fill(new Rect(r.x-14,r.y-5,r.width+28,2),FateweaveTint(gold,.85f*alpha));
            Fill(new Rect(r.x-21,r.y-7,8,12),FateweaveTint(gold,alpha));Fill(new Rect(r.xMax+13,r.y-7,8,12),FateweaveTint(gold,alpha));
            if(!fringe)return;var motion=profile.reduceMotion?0f:1f;
            for(var i=0;r.x+6+i*8<r.xMax-4;i++){var x=r.x+6+i*8;var sway=Mathf.Sin(now*2.1f+i*.8f)*2*motion;DrawLine(new Vector2(x,r.yMax),new Vector2(x+sway,r.yMax+12+(i%3)*4),FateweaveTint(pal.strand,.55f*alpha),1.4f);}
        }
        private void DrawFateweaveTapestry(Rect r,FateweaveDef fate,FateweavePalette pal,float alpha,float hot,string action,int coin,float content)
        {
            if(alpha<=.01f)return;
            DrawFateweaveTapestryShell(r,pal,alpha,hot,true);
            if(content<=.01f)return;
            var iconCenter=new Vector2(r.center.x,r.y+62);
            DrawFateweaveGlow(iconCenter,124,pal.glow,(.26f+.22f*hot)*content);
            for(var k=0;k<24;k++){var a0=k*Mathf.PI/12;var a1=a0+Mathf.PI/12;DrawLine(iconCenter+new Vector2(Mathf.Cos(a0),Mathf.Sin(a0))*46,iconCenter+new Vector2(Mathf.Cos(a1),Mathf.Sin(a1))*46,FateweaveTint(new Color(1f,.8f,.42f),.5f*content),1.5f);}
            var previous=GUI.color;GUI.color=new Color(1,1,1,content);DrawAtlasIcon(bindingFateIconAtlas,FateweaveIconIndex(fate.id),4,4,new Rect(iconCenter.x-40,iconCenter.y-40,80,80));GUI.color=previous;
            // A darker reading panel keeps the rules crisp over the weave.
            Fill(new Rect(r.x+16,r.y+112,r.width-32,r.height-176),new Color(0,0,0,.40f*content));
            GUI.Label(new Rect(r.x+18,r.y+116,r.width-36,16),FateweaveTag(fate.id),new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=12,normal={textColor=FateweaveTint(pal.accent,.92f*content)}});
            GUI.Label(new Rect(r.x+16,r.y+133,r.width-32,32),fate.name,new GUIStyle(titleStyle){fontSize=20,normal={textColor=hot>.5f?new Color(1f,.93f,.66f,content):new Color(.92f,.78f,.48f,content)}});
            Fill(new Rect(r.x+40,r.y+168,r.width-80,1),FateweaveTint(Gold,.4f*content));Fill(new Rect(r.center.x-3,r.y+165,6,6),FateweaveTint(Gold,.7f*content));
            GUI.Label(new Rect(r.x+24,r.y+176,r.width-48,Mathf.Max(20,r.height-240)),fate.text,new GUIStyle(footerStyle){fontSize=15,wordWrap=true,alignment=TextAnchor.UpperCenter,normal={textColor=new Color(.95f,.92f,.85f,content)}});
            var bandRect=new Rect(r.x+20,r.yMax-56,r.width-40,36);Fill(bandRect,new Color(.035f,.022f,.012f,.95f*content));Outline(bandRect,hot>.5f?FateweaveTint(Gold,content):FateweaveTint(new Color(.58f,.44f,.21f),content),hot>.5f?2:1);
            var labelRect=bandRect;
            if(coin>0){GUI.color=new Color(1,1,1,content);DrawGoldIcon(new Rect(bandRect.x+10,bandRect.y+6,24,24));GUI.color=previous;labelRect=new Rect(bandRect.x+26,bandRect.y,bandRect.width-30,bandRect.height);}
            GUI.Label(labelRect,action,new GUIStyle(buttonStyle){fontSize=12,normal={textColor=hot>.5f?new Color(1,1,1,content):FateweaveTint(Gold,content)}});
        }
        // The pulled boon, kept on the loom as a woven seal while the player chooses what to cut.
        private void DrawFateweavePulledSeal(Vector2 peg,FateweaveDef fate,FateweavePalette pal,float appear)
        {
            var now=Time.unscaledTime;var c=new Vector2(peg.x,FateweaveTapestryTop+86);var top=c-new Vector2(0,60);
            if(Event.current.type==EventType.Repaint){DrawLine(peg,top,FateweaveTint(pal.glow,.25f*appear),8);DrawLine(peg,top,FateweaveTint(pal.accent,.9f*appear),2.5f);}
            DrawFateweaveGlow(c,200,pal.glow,.34f*appear*(.85f+.15f*Mathf.Sin(now*2f)));
            for(var k=0;k<32;k++){var a0=k*Mathf.PI/16;var a1=a0+Mathf.PI/16;DrawLine(c+new Vector2(Mathf.Cos(a0),Mathf.Sin(a0))*58,c+new Vector2(Mathf.Cos(a1),Mathf.Sin(a1))*58,FateweaveTint(Gold,.8f*appear),2.5f);DrawLine(c+new Vector2(Mathf.Cos(a0),Mathf.Sin(a0))*50,c+new Vector2(Mathf.Cos(a1),Mathf.Sin(a1))*50,FateweaveTint(pal.strand,.55f*appear),1.2f);}
            var previous=GUI.color;GUI.color=new Color(1,1,1,appear);DrawAtlasIcon(bindingFateIconAtlas,FateweaveIconIndex(fate.id),4,4,new Rect(c.x-40,c.y-40,80,80));GUI.color=previous;
            GUI.Label(new Rect(c.x-130,c.y+68,260,16),"PULLED",new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=12,normal={textColor=FateweaveTint(pal.accent,appear)}});
            GUI.Label(new Rect(c.x-130,c.y+86,260,28),fate.name,new GUIStyle(titleStyle){fontSize=18,normal={textColor=new Color(1f,.9f,.6f,appear)}});
            GUI.Label(new Rect(c.x-130,c.y+116,260,20),"WOVEN INTO YOUR FATE",new GUIStyle(footerStyle){fontSize=12,normal={textColor=new Color(.9f,.86f,.76f,.8f*appear)}});
        }

        // ---------- the cut ----------
        private Vector2 FateweaveBurstPoint=>fateweaveCutFrom.center+new Vector2(0,.5f*1900f*.34f*.34f);
        private void DrawFateweaveSeverance(Vector2 peg,FateweaveDef fate,FateweavePalette pal,float phase,int slot)
        {
            var r0=fateweaveCutFrom;var apex0=new Vector2(r0.center.x,r0.y-24);var gold=fateweaveCutAnimGold;var label=$"CUT · +{gold} GOLD";
            if(profile.reduceMotion)
            {
                var fade=1-Mathf.Clamp01((Time.unscaledTime-fateweaveCutStarted)/.3f);
                DrawFateweaveRope(peg,apex0,pal,fade,0,phase,3);DrawFateweaveTapestry(r0,fate,pal,fade,0,label,gold,fade);return;
            }
            var tau=FateweaveCutClock;var cut=Vector2.Lerp(peg,apex0,.42f);var repaint=Event.current.type==EventType.Repaint;
            if(tau<.1f){DrawFateweaveRope(peg,apex0,pal,1,1,phase,4.4f);DrawFateweaveTapestry(r0,fate,pal,1,1,label,gold,1);}
            else
            {
                // The upper stub whips back toward the loom and settles.
                var snap=tau-.1f;var length=Vector2.Distance(peg,cut);var recoil=1-Mathf.Exp(-7f*snap)*Mathf.Cos(15f*snap);var whip=Mathf.Sin(snap*24f)*Mathf.Exp(-4.5f*snap)*22f;
                var end=peg+new Vector2(whip,length*Mathf.Clamp(1-.72f*recoil,.04f,1f));var bendAt=Vector2.Lerp(peg,end,.5f)+new Vector2(whip*1.4f,0);
                if(repaint)
                {
                    var previous=peg;var core=FateweaveTint(pal.strand,.95f);
                    for(var k=1;k<=10;k++){var s=k/10f;var p=(1-s)*(1-s)*peg+2*(1-s)*s*bendAt+s*s*end;DrawLine(previous,p,core,3.4f);previous=p;}
                    for(var k=-1;k<=1;k++)DrawLine(end,end+new Vector2(k*5+whip*.2f,7),FateweaveTint(pal.accent,.8f),1.2f);
                }
                // The lower strand and tapestry fall, turning, until they burst into coin.
                if(tau<.44f)
                {
                    var dy=.5f*1900f*snap*snap;var turn=snap*26f*(slot%2==0?-1:1);var r=new Rect(r0.x,r0.y+dy,r0.width,r0.height);
                    var matrix=GUI.matrix;var pivot=new Vector3(r.center.x,r.center.y,0);GUI.matrix=matrix*Matrix4x4.TRS(pivot,Quaternion.Euler(0,0,turn),Vector3.one)*Matrix4x4.TRS(-pivot,Quaternion.identity,Vector3.one);
                    if(repaint)DrawLine(new Vector2(r.center.x,r.y-24)-new Vector2(0,Vector2.Distance(cut,apex0)),new Vector2(r.center.x,r.y-24),FateweaveTint(pal.strand,.9f),3.4f);
                    DrawFateweaveTapestry(r,fate,pal,1,0,label,gold,1);GUI.matrix=matrix;
                }
            }
            if(tau<.3f&&repaint)
            {
                // Blade streak across the strand.
                var p=Mathf.Clamp01(tau/.14f);var fade=1-Mathf.Clamp01((tau-.14f)/.14f);var from=cut+new Vector2(-72,-40);var to=cut+new Vector2(72,40);
                var head=Vector2.Lerp(from,to,p);var tail=Vector2.Lerp(from,to,Mathf.Max(0,p-.5f));var bright=profile.reduceFlashing?.45f:1f;
                if(!profile.reducedVfx)DrawLine(tail,head,FateweaveTint(pal.glow,.4f*fade*bright),11);DrawLine(tail,head,new Color(1f,.97f,.88f,.95f*fade*bright),2.5f);
                var flash=Mathf.Sin(Mathf.Clamp01((tau-.05f)/.22f)*Mathf.PI);
                if(profile.reduceFlashing)DrawFateweaveGlow(cut,80,pal.accent,.18f*flash);else DrawFateweaveGlow(cut,130,Color.white,.55f*flash);
            }
            if(tau>=.40f&&tau<.8f)
            {
                var burst=FateweaveBurstPoint;var k01=Mathf.Clamp01((tau-.42f)/.34f);
                DrawFateweaveGlow(burst,220*(.6f+k01),new Color(1f,.78f,.3f),(profile.reduceFlashing?.18f:.6f)*(1-k01));
                if(repaint){var scraps=profile.reducedVfx?4:10;for(var k=0;k<scraps;k++){var a=k*2.4f+.5f;var d=new Vector2(Mathf.Cos(a),Mathf.Sin(a)*.8f-.3f)*(40+k*9)*k01;var p=burst+d+new Vector2(0,60*k01*k01);Fill(new Rect(p.x-3,p.y-2,6+k%3*2,4),FateweaveTint(k%2==0?pal.strand:pal.deep,.9f*(1-k01)));}}
            }
            if(tau>=.44f){var rise=Mathf.Clamp01((tau-.44f)/1f);var show=Mathf.Clamp01((tau-.44f)/.12f)*(1-Mathf.Clamp01((tau-1.25f)/.3f));var burst=FateweaveBurstPoint;
                GUI.Label(new Rect(burst.x-120,burst.y-46-54*rise,240,36),$"+{gold} GOLD",new GUIStyle(titleStyle){font=labelFont?labelFont:bodyFont,fontSize=24,normal={textColor=new Color(1f,.86f,.44f,show)}});}
        }
        // Coins arc from the burst to the run HUD gold counter, drawn above everything on screen.
        private void DrawFateweaveCoins(FateweavePalette pal)
        {
            var target=RunGoldIconRect.center;
            if(profile.reduceMotion)
            {
                var t=Mathf.Clamp01((Time.unscaledTime-fateweaveCutStarted)/FateweaveCutDuration);var show=Mathf.Sin(t*Mathf.PI);
                GUI.Label(new Rect(fateweaveCutFrom.center.x-120,fateweaveCutFrom.center.y-18,240,36),$"+{fateweaveCutAnimGold} GOLD",new GUIStyle(titleStyle){font=labelFont?labelFont:bodyFont,fontSize=24,normal={textColor=new Color(1f,.86f,.44f,show)}});
                DrawFateweaveGlow(target,70,new Color(1f,.8f,.35f),.3f*show);return;
            }
            var tau=FateweaveCutClock;var burst=FateweaveBurstPoint;var count=profile.reducedVfx?6:14;var repaint=Event.current.type==EventType.Repaint;
            for(var k=0;k<count;k++)
            {
                var q=Mathf.Clamp01((tau-.44f-k*.018f)/(.62f+k*.025f));if(q<=0||q>=1)continue;var e=Mathf.SmoothStep(0,1,q);
                var angle=(-90f+((k/(float)Mathf.Max(1,count-1))-.5f)*150f)*Mathf.Deg2Rad;var control=burst+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(150+(k*37)%90);
                Vector2 At(float s)=>(1-s)*(1-s)*burst+2*(1-s)*s*control+s*s*target;
                var p=At(e);var size=Mathf.Lerp(26,18,e);var width=size*Mathf.Max(.3f,Mathf.Abs(Mathf.Cos(tau*10f+k)));
                if(repaint&&!profile.reducedVfx)DrawLine(At(Mathf.Max(0,e-.06f)),p,new Color(1f,.8f,.35f,.35f),2);
                DrawGoldIcon(new Rect(p.x-width*.5f,p.y-size*.5f,width,size));
            }
            var land=Mathf.Clamp01((tau-.95f)/.5f);if(land>0&&land<1)DrawFateweaveGlow(target,90,new Color(1f,.8f,.35f),(profile.reduceFlashing?.2f:.5f)*Mathf.Sin(land*Mathf.PI));
        }
        // The strand nobody chose frays apart and fades.
        private void DrawFateweaveFraying(float w,Vector2 peg,Rect baseRect,FateweaveDef fate,FateweavePalette pal,float phase,int slot)
        {
            var apex=new Vector2(baseRect.center.x,baseRect.y-24);var label=$"CUT · +{run.FateweaveCutGold(fate.id)} GOLD";
            if(profile.reduceMotion)
            {
                var fade=1-Mathf.Clamp01((Time.unscaledTime-fateweaveCutStarted)/.4f);DrawFateweaveRope(peg,apex,pal,fade*.5f,0,phase,3);DrawFateweaveTapestry(baseRect,fate,pal,fade*.5f,0,label,0,fade*.5f);return;
            }
            var tau=FateweaveCutClock;var f=Mathf.Clamp01((tau-.5f)/.9f);var alpha=.46f*(1-f);if(alpha<=.01f)return;
            if(f<=0){DrawFateweaveFiber(w,peg,pal,alpha,0,phase);DrawFateweaveRope(peg,apex,pal,alpha,0,phase,3);DrawFateweaveTapestry(new Rect(baseRect.x+FateweaveSwayX(slot,0),baseRect.y,baseRect.width,baseRect.height),fate,pal,alpha,0,label,0,alpha);return;}
            var now=Time.unscaledTime;var repaint=Event.current.type==EventType.Repaint;
            if(repaint)for(var k=0;k<4;k++)
            {
                var spread=(k-1.5f)*(1+f*16f);var previous=peg;
                for(var j=1;j<=10;j++){var s=j/10f;var p=Vector2.Lerp(peg,apex+new Vector2(spread*1.6f,f*30),s)+new Vector2(spread*Mathf.Sin(s*Mathf.PI)+Mathf.Sin(now*2.3f+k+s*4)*4*f,0);DrawLine(previous,p,FateweaveTint(pal.strand,alpha*(1-s*.4f)),1.2f);previous=p;}
            }
            var r=new Rect(baseRect.x,baseRect.y+f*26,baseRect.width,baseRect.height*(1-f*.8f));
            DrawFateweaveTapestry(r,fate,pal,alpha,0,label,0,alpha*(1-Mathf.Clamp01(f*3)));
            if(repaint)
            {
                for(var k=0;r.x+8+k*14<r.xMax;k++){var x=r.x+8+k*14;DrawLine(new Vector2(x,r.yMax),new Vector2(x+Mathf.Sin(now*1.8f+k)*3,r.yMax+18+f*44+(k%4)*6),FateweaveTint(pal.strand,alpha*.8f),1.1f);}
                var motes=profile.reducedVfx?3:9;for(var k=0;k<motes;k++){var x=r.x+18+(r.width-36)*((k*.618f)%1f);var y=r.yMax+f*(60+k*9);Fill(new Rect(x-1.5f,y,3,3),FateweaveTint(pal.accent,alpha*(1-f)));}
            }
        }

        // ---------- the pull cinematic (drawn by DrawAcquisitionPresentation) ----------
        private void DrawFateweavePullCinematic(float w,float h,float t,float reveal,float depart,Vector2 center)
        {
            var fate=acquisitionFateweave;if(fate==null)return;
            var pal=FateweaveColors(run.act);var accent=new Color(1f,.72f,.28f);var iconCenter=new Vector2(center.x,center.y-15);var repaint=Event.current.type==EventType.Repaint;
            var from=fateweavePullFrom.width>1?fateweavePullFrom:new Rect(center.x-126,center.y-170,252,330);
            if(profile.reduceMotion)
            {
                var fade=1-Mathf.Clamp01(t/.35f);DrawFateweaveTapestry(from,fate,pal,fade,1,"PULL THIS STRAND",0,fade);
                var show=Mathf.Clamp01((t-.2f)/.3f)*(1-depart);var old=GUI.color;GUI.color=new Color(1,1,1,show);DrawAtlasIcon(bindingFateIconAtlas,FateweaveIconIndex(fate.id),4,4,new Rect(iconCenter.x-85,iconCenter.y-85,170,170));GUI.color=old;
                if(t>.3f&&depart<.4f)DrawFateweavePullLabels(w,h,fate,accent,show);return;
            }
            var src=new Rect(from.x,from.y+FateweaveYank(t),from.width,from.height);var unravel=Mathf.Clamp01((t-.08f)/.30f);
            if(unravel<1)
            {
                // The weave shortens from the bottom as its threads are drawn out.
                var shell=new Rect(src.x,src.y,src.width,src.height*(1-unravel));DrawFateweaveTapestryShell(shell,pal,1,1,false);
                if(shell.height>112){DrawFateweaveGlow(new Vector2(src.center.x,src.y+62),124,pal.glow,.4f);DrawAtlasIcon(bindingFateIconAtlas,FateweaveIconIndex(fate.id),4,4,new Rect(src.center.x-40,src.y+22,80,80));}
                if(shell.height>170)GUI.Label(new Rect(src.x+16,src.y+133,src.width-32,32),fate.name,new GUIStyle(titleStyle){fontSize=20,normal={textColor=new Color(1f,.93f,.66f)}});
                if(repaint)for(var i=0;src.x+8+i*9<src.xMax-6;i++){var x=src.x+8+i*9;DrawLine(new Vector2(x,shell.yMax),new Vector2(x+Mathf.Sin(t*30+i)*3,shell.yMax+8+(i%4)*4),FateweaveTint(pal.strand,.8f),1.2f);}
            }
            if(repaint)
            {
                var cols=profile.reducedVfx?5:9;var rows=profile.reducedVfx?6:12;
                for(var row=0;row<rows;row++)for(var col=0;col<cols;col++)
                {
                    var k=row*cols+col;var u=(row+.5f)/rows;var release=.08f+.30f*u+(k*37%11)*.004f;var q=Mathf.Clamp01((t-release)/.22f);if(q<=0||q>=1)continue;
                    var origin=new Vector2(src.x+18+(src.width-36)*(col+.5f)/cols,src.yMax-16-(src.height-32)*u);var control=Vector2.Lerp(origin,iconCenter,.5f)+new Vector2(Mathf.Sin(k*1.7f)*140,-60-Mathf.Cos(k*.9f)*70);
                    Vector2 At(float s)=>(1-s)*(1-s)*origin+2*(1-s)*s*control+s*s*iconCenter;
                    var e=Mathf.SmoothStep(0,1,q);var p=At(e);var color=k%3==0?pal.strand:accent;var a=Mathf.Sin(q*Mathf.PI)*.9f+.1f;
                    DrawLine(At(Mathf.Max(0,e-.1f)),p,FateweaveTint(color,.45f*a),2);Fill(new Rect(p.x-2,p.y-2,4,4),new Color(1f,.93f,.72f,a));
                }
            }
            var gather=Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.30f)/.24f));var flash=Mathf.Sin(Mathf.Clamp01((t-.36f)/.26f)*Mathf.PI);
            DrawFateweaveGlow(iconCenter,260*(.6f+gather*.6f)*(1-depart*.5f),pal.glow,(profile.reduceFlashing?.16f:.42f)*flash+.22f*gather*(1-depart));
            var iconSize=Mathf.Lerp(60,184,gather)*(1-depart*.55f);var snap=Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.56f)/.18f));
            if(gather>0&&snap<1&&repaint){var loom=FateweaveLoomCenter(w)+new Vector2(0,FateweaveLoomRadius);var end=Vector2.Lerp(iconCenter-new Vector2(0,iconSize*.5f),loom,snap);DrawLine(loom,end,FateweaveTint(pal.glow,.3f*gather*(1-snap)),9);DrawLine(loom,end,FateweaveTint(Color.Lerp(pal.strand,accent,.5f),.92f*gather*(1-depart)),Mathf.Lerp(4,1,snap));}
            var previous=GUI.color;GUI.color=new Color(1,1,1,gather);DrawAtlasIcon(bindingFateIconAtlas,FateweaveIconIndex(fate.id),4,4,new Rect(iconCenter.x-iconSize*.5f,iconCenter.y-iconSize*.5f,iconSize,iconSize));GUI.color=previous;
            if(!profile.reducedVfx&&repaint)for(var i=0;i<22;i++){var a=i*Mathf.PI*2/22+shimmer*.7f;var radius=50+snap*130;var p=center+new Vector2(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius*.62f);Fill(new Rect(p.x-2,p.y-2,4,4),new Color(i%3==0?pal.strand.r:1f,i%3==0?pal.strand.g:.7f,i%3==0?pal.strand.b:.25f,(1-depart)*.75f*gather));}
            if(t>.40f&&depart<.4f)DrawFateweavePullLabels(w,h,fate,accent,Mathf.Clamp01((t-.40f)/.12f));
        }
        private void DrawFateweavePullLabels(float w,float h,FateweaveDef fate,Color accent,float alpha)
        {
            GUI.Label(new Rect(w*.20f,h*.12f,w*.60f,46),"THE STRAND IS PULLED",new GUIStyle(titleStyle){fontSize=32,normal={textColor=FateweaveTint(accent,alpha)}});
            GUI.Label(new Rect(w*.25f,h*.72f,w*.50f,34),fate.name,new GUIStyle(titleStyle){fontSize=24,normal={textColor=new Color(1f,.88f,.54f,alpha)}});
            GUI.Label(new Rect(w*.27f,h*.77f,w*.46f,64),fate.text,new GUIStyle(footerStyle){fontSize=15,wordWrap=true,alignment=TextAnchor.UpperCenter,normal={textColor=new Color(.94f,.92f,.86f,alpha)}});
        }

        // ---------- procedural textures ----------
        private Texture2D FateweaveGlowTexture
        {
            get
            {
                if(fateweaveGlow)return fateweaveGlow;const int n=64;var pixels=new Color[n*n];
                for(var y=0;y<n;y++)for(var x=0;x<n;x++){var d=Mathf.Clamp01(1-Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f))/32f);pixels[y*n+x]=new Color(1,1,1,d*d*(3-2*d));}
                fateweaveGlow=new Texture2D(n,n,TextureFormat.RGBA32,false){name="Fateweave glow",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                fateweaveGlow.SetPixels(pixels);fateweaveGlow.Apply(false,true);return fateweaveGlow;
            }
        }
        private Texture2D FateweaveWeaveTexture
        {
            get
            {
                if(fateweaveWeave)return fateweaveWeave;const int n=16;var pixels=new Color[n*n];
                for(var y=0;y<n;y++)for(var x=0;x<n;x++)
                {
                    // Plain weave: 4px warp and weft threads alternating over and under.
                    var warpOver=((x/4+y/4)&1)==0;var across=warpOver?x%4:y%4;var along=(warpOver?y%4:x%4)/3f;
                    var light=(.55f+.45f*Mathf.Sin(along*Mathf.PI))*(across==0||across==3?.55f:1f);pixels[y*n+x]=new Color(light,light,light,warpOver?.9f:.7f);
                }
                fateweaveWeave=new Texture2D(n,n,TextureFormat.RGBA32,false){name="Fateweave weave",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Repeat};
                fateweaveWeave.SetPixels(pixels);fateweaveWeave.Apply(false,true);return fateweaveWeave;
            }
        }
        private void DrawFateweaveGlow(Vector2 center,float size,Color color,float alpha)=>DrawFateweaveGlowRect(new Rect(center.x-size*.5f,center.y-size*.5f,size,size),color,alpha);
        private void DrawFateweaveGlowRect(Rect r,Color color,float alpha)
        {
            if(alpha<=.005f||Event.current.type!=EventType.Repaint)return;
            var previous=GUI.color;GUI.color=FateweaveTint(color,alpha);GUI.DrawTexture(r,FateweaveGlowTexture);GUI.color=previous;
        }
    }
}
