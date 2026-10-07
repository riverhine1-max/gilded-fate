using System.Collections.Generic;
using System.Linq;
using GildedFate.Audio;
using GildedFate.Core;
using GildedFate.Combat;
using GildedFate.Map;
using UnityEngine;

namespace GildedFate.UI
{
    // Fateshard Overhaul: the combat reliquary (bottom-left, beside the Energy orb),
    // charge rings, use pips, hold-to-shatter and the pre-battle Attune screen.
    public sealed partial class GildedMainMenu
    {
        private const float ReliquarySocketSize=96,ShatterHoldSeconds=.9f;
        private bool ReliquaryLayout=>screen==ScreenMode.Combat&&combat!=null;
        private int shardAttuneIndex;
        private float shardAttuneOpenedAt,shatterHoldStart=-1,lastShardChargeGainAt=-10;
        private int shatterHoldSlot=-1,lastSeenShardCharge=-1;
        private CombatState lastSeenChargeCombat;

        // ---------------- Attune screen ----------------
        private bool ShardAttuneOpen=>screen==ScreenMode.Combat&&combat!=null&&combat.shardAttunePending&&!acquisitionActive&&bossIntroTime<=0&&!combatPauseOpen;
        private List<FateShardState> AttuneChoices=>run.shards.Where(s=>s.CanActivate&&WorldContent.FateShards.Any(d=>d.id==s.id)).OrderBy(s=>s.slot).ToList();
        private int AttuneOptionCount{get{var n=AttuneChoices.Count;return n>=2?n+1:n;}}

        // Called right after a fresh combat begins. One usable shard attunes automatically;
        // two usable shards open the Attune screen.
        private void PrepareShardAttune()
        {
            if(combat==null||run.shards.Any(s=>s.active))return;
            var usable=AttuneChoices;
            if(usable.Count==0)return;
            if(usable.Count==1){combat.AttuneShards(usable[0].id,"",false);return;}
            combat.shardAttunePending=true;
            shardAttuneIndex=Mathf.Max(0,usable.FindIndex(s=>s.id==run.lastAttunedShardId));
            shardAttuneOpenedAt=Time.unscaledTime;
        }
        private void ConfirmShardAttune(int index)
        {
            var usable=AttuneChoices;
            if(usable.Count==0){combat.shardAttunePending=false;return;}
            if(index>=usable.Count)
            {
                combat.AttuneShards(usable[0].id,usable.Count>1?usable[1].id:"",true);run.lastAttunedShardId="";
            }
            else
            {
                var chosen=usable[Mathf.Clamp(index,0,usable.Count-1)];combat.AttuneShards(chosen.id,"",false);run.lastAttunedShardId=chosen.id;
                var from=new Vector2(CombatWidth*.5f,CombatHeight*.45f);
                shardFlights.Add(new ShardFlight{from=from,to=ShrineSocket(chosen.slot).center,start=Time.unscaledTime});
            }
            Sfx(SoundCue.Resonance);handReadyAt=Mathf.Max(handReadyAt,Time.unscaledTime+.25f);
            SaveCombatCheckpoint();
        }
        private void HandleShardAttuneNavigation(MenuNavigation input)
        {
            var count=Mathf.Max(1,AttuneOptionCount);var move=input.x!=0?input.x:input.y;
            shardAttuneIndex=(shardAttuneIndex+move+count)%count;
            if(input.accept)ConfirmShardAttune(shardAttuneIndex);
        }
        private void DrawShardAttune(float w,float h)
        {
            var usable=AttuneChoices;
            if(usable.Count<2){if(usable.Count==1)combat.AttuneShards(usable[0].id,"",false);else combat.shardAttunePending=false;return;}
            runHudTooltipTitle="";hoveredCardHelp=null;
            var t=Mathf.Clamp01((Time.unscaledTime-shardAttuneOpenedAt)/(profile.reduceMotion?.01f:.35f));var ease=1-(1-t)*(1-t);
            Fill(new Rect(0,0,w,h),new Color(.002f,.004f,.01f,.88f*ease));
            GUI.Label(new Rect(w*.15f,h*.12f,w*.7f,52),"ATTUNE A FATE SHARD",new GUIStyle(titleStyle){fontSize=34,normal={textColor=Gold}});
            GUI.Label(new Rect(w*.18f,h*.12f+54,w*.64f,48),"Choose the shard to charge this battle. Cards that match its type charge it faster.",new GUIStyle(footerStyle){fontSize=17,wordWrap=true});
            var count=usable.Count+1;const float cardW=290,holdW=220,gap=26;var total=usable.Count*cardW+holdW+usable.Count*gap;
            var x=w*.5f-total*.5f;var top=h*.30f+(1-ease)*60;var cardH=Mathf.Min(360,h*.52f);
            for(var i=0;i<count;i++)
            {
                var hold=i==usable.Count;var r=new Rect(x,top,hold?holdW:cardW,cardH);x+=r.width+gap;
                var hot=controllerNavigation?shardAttuneIndex==i:r.Contains(PointerPosition);if(hot&&!controllerNavigation)shardAttuneIndex=i;
                Fill(r,new Color(.012f,.016f,.026f,.95f));Outline(r,hot?Gold:new Color(.55f,.41f,.2f,.8f),hot?3:2);
                Outline(new Rect(r.x+6,r.y+6,r.width-12,r.height-12),new Color(.2f,.16f,.1f,.7f),1);
                if(hold)
                {
                    GUI.Label(new Rect(r.x+10,r.y+30,r.width-20,40),"HOLD",new GUIStyle(titleStyle){fontSize=26,normal={textColor=new Color(.76f,.9f,1f)}});
                    GUI.Label(new Rect(r.x+16,r.y+84,r.width-32,r.height-130),"Charge both shards and decide mid-fight.\n\nNeeds "+CombatState.ShardChargeFillHold+" charge instead of "+CombatState.ShardChargeFill+".",new GUIStyle(footerStyle){fontSize=16,wordWrap=true,alignment=TextAnchor.UpperCenter});
                }
                else
                {
                    var owned=usable[i];var def=WorldContent.FateShards.First(s=>s.id==owned.id);var nextFractured=owned.uses>=2;
                    var art=new Rect(r.center.x-62,r.y+18,124,124);var lift=profile.reduceMotion?0:Mathf.Sin(shimmer*1.3f+i)*4;
                    if(hot&&!profile.reduceFlashing)Fill(new Rect(art.x-8,art.y-8+lift,art.width+16,art.height+16),new Color(1f,.84f,.47f,.1f));
                    DrawFateShardArt(new Rect(art.x,art.y+lift,art.width,art.height),def);
                    GUI.Label(new Rect(r.x+8,r.y+146,r.width-16,30),def.name,new GUIStyle(titleStyle){fontSize=21,normal={textColor=nextFractured?new Color(1f,.7f,.38f):new Color(.76f,.9f,1f)}});
                    GUI.Label(new Rect(r.x+8,r.y+176,r.width-16,22),owned.RemainingUses+" / 3 USES LEFT"+(nextFractured?" · FRACTURED NEXT":""),ReadableStyle(12,true));
                    var body=new GUIStyle(footerStyle){fontSize=15,wordWrap=true,alignment=TextAnchor.UpperCenter,normal={textColor=new Color(.94f,.92f,.86f)}};
                    GUI.Label(new Rect(r.x+16,r.y+204,r.width-32,r.height-250),nextFractured?def.fracturedText:def.stableText,body);
                    body.normal.textColor=Gold;body.fontSize=13;
                    GUI.Label(new Rect(r.x+12,r.yMax-44,r.width-24,36),ShardChargeHint(def),body);
                }
                if(GUI.Button(r,"",GUIStyle.none)){ConfirmShardAttune(i);return;}
            }
            DrawMenuNavigationHint(w,h,"Arrows  Select    Enter  Attune","D-pad / Stick  Select    A  Attune");
        }

        // ---------------- Reliquary frame, rings, pips ----------------
        private void DrawReliquaryFrame(Rect first,Rect last,Color gold)
        {
            var frame=new Rect(first.x-26,first.y-30,first.width+52,last.yMax-first.y+76);
            Fill(frame,new Color(.012f,.014f,.022f,.72f));Outline(frame,new Color(gold.r,gold.g,gold.b,.7f),2);
            Outline(new Rect(frame.x+5,frame.y+5,frame.width-10,frame.height-10),new Color(.24f,.18f,.1f,.6f),1);
            var c=frame.center.x;
            DrawLine(new Vector2(c-18,frame.y),new Vector2(c,frame.y-12),gold,2);DrawLine(new Vector2(c,frame.y-12),new Vector2(c+18,frame.y),gold,2);
            DrawLine(new Vector2(c-18,frame.yMax),new Vector2(c,frame.yMax+12),gold,2);DrawLine(new Vector2(c,frame.yMax+12),new Vector2(c+18,frame.yMax),gold,2);
            if(combat!=null&&combat.shardHold&&string.IsNullOrEmpty(combat.activeShardId))
                GUI.Label(new Rect(frame.x-20,frame.y-34,frame.width+40,20),"HOLD · BOTH CHARGING",ReadableStyle(10,true));
        }
        private bool ShardReady(FateShardState owned,bool active)=>owned!=null&&!active&&combat!=null&&owned.CanActivate&&combat.CanActivateChargedShard(owned.id);
        private void TrackShardCharge()
        {
            if(combat==null)return;
            if(lastSeenChargeCombat!=combat){lastSeenChargeCombat=combat;lastSeenShardCharge=combat.shardCharge;return;}
            if(combat.shardCharge>lastSeenShardCharge)
            {
                lastShardChargeGainAt=Time.unscaledTime;
                if(!profile.reducedVfx)foreach(var s in run.shards.Where(s=>s.CanActivate&&combat.ShardAttunedTo(s.id)))
                    shardFlights.Add(new ShardFlight{from=new Vector2(CombatWidth*.5f,CombatHeight-150),to=ShrineSocket(s.slot).center,start=Time.unscaledTime});
                if(combat.ShardChargeFull)Sfx(SoundCue.Resonance,-.6f,.7f);
            }
            lastSeenShardCharge=combat.shardCharge;
        }
        private void DrawShardChargeRing(Rect r,FateShardState owned,bool active,bool dim)
        {
            if(combat==null||owned==null)return;
            var c=r.center;var radius=r.width*.62f;var segments=56;
            var charging=string.IsNullOrEmpty(combat.activeShardId)&&combat.ShardAttunedTo(owned.id)&&owned.CanActivate;
            var progress=active?1f:charging?combat.ShardChargeProgress:0f;
            var ready=ShardReady(owned,active);
            var gainFlash=Mathf.Clamp01(1-(Time.unscaledTime-lastShardChargeGainAt)/.4f);
            var pulse=ready&&!profile.reduceMotion?(Mathf.Sin(shimmer*4.2f)*.5f+.5f):0;
            var track=new Color(.05f,.05f,.07f,dim?.5f:.92f);
            var fill=active?Color.Lerp(Gold,new Color(1f,.58f,.24f),owned.activeFractured?.7f:0):ready?Color.Lerp(Gold,Color.white,pulse*.35f):new Color(.86f,.68f,.32f);
            for(var s=0;s<segments;s++)
            {
                var a0=-Mathf.PI*.5f+s*Mathf.PI*2/segments;var a1=a0+Mathf.PI*2/segments;
                var p0=c+new Vector2(Mathf.Cos(a0),Mathf.Sin(a0))*radius;var p1=c+new Vector2(Mathf.Cos(a1),Mathf.Sin(a1))*radius;
                DrawLine(p0,p1,track,6);
                if((s+1)/(float)segments<=progress+.0001f)DrawLine(p0,p1,new Color(fill.r,fill.g,fill.b,dim?.4f:1f),3.2f+gainFlash*(charging?1.6f:0)+pulse*1.4f);
            }
            if((ready||active)&&!profile.reduceFlashing)
            {
                var glow=active?.12f:.1f+pulse*.14f;
                for(var g=1;g<=3;g++)for(var s=0;s<segments;s+=2)
                {
                    var a0=s*Mathf.PI*2/segments;var a1=a0+Mathf.PI*2/segments;var rr=radius+g*3.5f;
                    DrawLine(c+new Vector2(Mathf.Cos(a0),Mathf.Sin(a0))*rr,c+new Vector2(Mathf.Cos(a1),Mathf.Sin(a1))*rr,new Color(fill.r,fill.g,fill.b,glow/g),2);
                }
            }
            if(shatterHoldSlot==owned.slot&&shatterHoldStart>=0)
            {
                var hp=Mathf.Clamp01((Time.unscaledTime-shatterHoldStart)/ShatterHoldSeconds);var rr=radius+11;
                for(var s=0;s<segments;s++){if((s+1)/(float)segments>hp)break;var a0=-Mathf.PI*.5f+s*Mathf.PI*2/segments;var a1=a0+Mathf.PI*2/segments;DrawLine(c+new Vector2(Mathf.Cos(a0),Mathf.Sin(a0))*rr,c+new Vector2(Mathf.Cos(a1),Mathf.Sin(a1))*rr,new Color(1f,.5f,.2f,.95f),3);}
            }
        }
        private string ReliquaryStateText(FateShardState owned,bool active,string legacy)
        {
            if(owned==null)return legacy;
            if(active)return combat.shardShattered?"SHATTERED":"ACTIVE";
            if(!owned.CanActivate)return legacy;
            if(!string.IsNullOrEmpty(combat.activeShardId)||!combat.ShardAttunedTo(owned.id))return legacy;
            return combat.ShardChargeFull?"READY":combat.shardCharge+" / "+combat.ShardChargeTarget;
        }
        private void DrawShardUsePips(int index,FateShardState owned,bool active,Color accent)
        {
            var label=ShrineStateLabel(index);var y=label.yMax+3;var cx=label.center.x;
            for(var p=0;p<3;p++)
            {
                var at=new Vector2(cx+(p-1)*16,y+5);var spent=p<owned.uses;var finalPip=p==2;
                var color=spent?new Color(.3f,.27f,.24f,.85f):finalPip?new Color(1f,.62f,.3f,.95f):new Color(accent.r,accent.g,accent.b,.95f);
                var pts=new[]{at+new Vector2(0,-5),at+new Vector2(5,0),at+new Vector2(0,5),at+new Vector2(-5,0)};
                for(var s=0;s<4;s++)DrawLine(pts[s],pts[(s+1)%4],color,spent?1.2f:2f);
                if(spent){DrawLine(at+new Vector2(-3,-3),at+new Vector2(1,1),color,1);DrawLine(at+new Vector2(1,1),at+new Vector2(-1,4),color,1);}
                else Fill(new Rect(at.x-1.5f,at.y-1.5f,3,3),color);
            }
        }
        private string ShardChargeHint(FateShardDef def)
        {
            var match=def.archetype switch
            {
                "Strength"=>"Strength cards","Block"=>"Block cards","Attack"=>"Attacks","Heavy"=>"Heavy Attacks","Retaliate"=>"Block and Retaliate cards",
                "Burn"=>"Burn cards","Debuff"=>"debuff cards","Exhaust"=>"Exhaust cards","Draw"=>"Draw cards","Energy"=>"Energy cards","Modified"=>"modified cards",_=>""
            };
            return string.IsNullOrEmpty(match)?"Every card charges it · needs "+CombatState.ShardChargeFillUnmatched:"Charges faster from "+match+" · needs "+CombatState.ShardChargeFill;
        }
        private string ShardChargeTooltip(FateShardState owned,FateShardDef def)
        {
            if(combat==null||owned==null||def==null)return "";
            var line="\n"+ShardChargeHint(def).ToUpperInvariant();
            if(!string.IsNullOrEmpty(combat.activeShardId)||!owned.CanActivate)return line;
            if(!combat.ShardAttunedTo(owned.id))return line+"\nNOT ATTUNED THIS BATTLE";
            return line+"\nCHARGE "+combat.shardCharge+" / "+combat.ShardChargeTarget;
        }

        // ---------------- Activate / hold to shatter ----------------
        private void DrawReliquaryShardActions(Rect r,FateShardDef def,FateShardState owned)
        {
            var ready=ShardReady(owned,false)&&CanAcceptCombatInput;var finalUse=owned.uses>=2;
            var activate=new Rect(r.xMax+30,r.center.y-(finalUse?20:44),150,40);
            var enabled=GUI.enabled;GUI.enabled=enabled&&ready;
            DrawButtonFrame(activate,activate.Contains(PointerPosition),!GUI.enabled);
            var label=!combat.ShardChargeFull&&combat.ShardAttunedTo(owned.id)?"CHARGING "+combat.shardCharge+"/"+combat.ShardChargeTarget:!combat.ShardAttunedTo(owned.id)?"NOT ATTUNED":finalUse?"ACTIVATE · FRACTURED":"ACTIVATE";
            if(GUI.Button(activate,label,new GUIStyle(buttonStyle){fontSize=label.Length>12?13:buttonStyle.fontSize})){selectedShrineSlot=-1;QueueShard(def,owned,run.shards.IndexOf(owned));}
            DeniedPress(activate,!ready);GUI.enabled=enabled;
            if(finalUse)return; // the last use is already Fractured, so shattering would do the same thing
            var shatter=new Rect(activate.x,activate.yMax+8,150,40);var hot=shatter.Contains(PointerPosition);
            DrawButtonFrame(shatter,hot,!ready);
            var held=ready&&hot&&UnityEngine.InputSystem.Mouse.current?.leftButton.isPressed==true;
            if(held)
            {
                if(shatterHoldSlot!=owned.slot||shatterHoldStart<0){shatterHoldSlot=owned.slot;shatterHoldStart=Time.unscaledTime;}
                var p=Mathf.Clamp01((Time.unscaledTime-shatterHoldStart)/ShatterHoldSeconds);
                Fill(new Rect(shatter.x+3,shatter.y+3,(shatter.width-6)*p,shatter.height-6),new Color(1f,.45f,.15f,.35f));
                if(p>=1){shatterHoldStart=-1;shatterHoldSlot=-1;selectedShrineSlot=-1;QueueShard(def,owned,run.shards.IndexOf(owned),true);}
            }
            else if(shatterHoldSlot==owned.slot){shatterHoldStart=-1;shatterHoldSlot=-1;}
            var style=new GUIStyle(buttonStyle){fontSize=13,normal={textColor=ready?new Color(1f,.66f,.36f):new Color(.5f,.45f,.4f)}};
            GUI.Label(shatter,"HOLD TO SHATTER",style);
            if(hot)SetRunHudTooltip(shatter,"SHATTER "+def.name,"Hold to take the FRACTURED power now. The shard is destroyed, however many uses it has left.\n\n"+def.fracturedText);
        }
        private void ShatterBurst(int slot,FateShardDef shard)
        {
            shardFrameShatterAt=Time.unscaledTime;
            shardFlights.Add(new ShardFlight{from=ShrineSocket(Mathf.Clamp(slot,0,1)).center,start=Time.unscaledTime,shatter=true,shard=shard});
        }

        // Verification helper: fills the meter so scripted activations run without playing cards.
        private void FillShardChargeForVerification()
        {
            if(combat==null)return;combat.shardAttunePending=false;combat.shardCharge=combat.ShardChargeTarget;
        }
    }
}
