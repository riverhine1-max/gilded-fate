using GildedFate.Audio;
using GildedFate.Combat;
using GildedFate.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GildedFate.UI
{
    // GILD button, shortcut and feedback. Rules live in CombatState (CombatGilding.cs);
    // this file only pays run Gold, saves, and presents the armed/resolved states.
    public sealed partial class GildedMainMenu
    {
        private float gildBurstAt=-10f,gildResolvedAt=-10f;
        private int gildBurstCost;
        private const float GildBurstSeconds=.9f,GildFlipSeconds=.42f,GildSplitSeconds=.62f,GildCoinDiameter=88f;
        private Vector2 gildResolvedPoint;
        // Under End Turn, in the right-hand column beside the Dissipate/Discard piles: the control every turn passes through.
        // The hand reserves HandLayout.SideReserve (200) on both sides, so the badge (x >= CombatWidth-203) never sits under
        // a resting or raised card. The bezel is centred 137 px above the bottom edge; the nameplate hangs beneath it and ends
        // above the Discard pile's baseline; both clear End Turn (which ends 204 px above the bottom edge).
        private Vector2 GildCoinCenter=>new Vector2(CombatWidth-147,CombatHeight-137);
        private Rect GildPlateRect=>new Rect(CombatWidth-198,CombatHeight-90,102,54);
        private Rect GildButtonRect=>new Rect(CombatWidth-203,CombatHeight-196,112,160);
        private bool GildShortcutPressed=>Keyboard.current?.gKey.wasPressedThisFrame==true||Gamepad.current?.leftTrigger.wasPressedThisFrame==true;
        private string GildKeyLabel=>controllerNavigation&&menuUsesGamepad?"LT":"G";

        private string GildBlockedHint()
        {
            if(combat==null)return "Gild is unavailable.";
            if(combat.gildArmed)return "Gild armed · your next card plays twice.";
            if(combat.gildUsedThisTurn)return "Already gilded this turn.";
            if(!CanAcceptCombatInput||pileOpen>=0||combatHistoryOpen)return combat.phase==CombatPhase.Player&&!combat.IsOver?"Finish the current action first.":"Gild only during your turn.";
            if(!combat.CanGild(run.gold))return $"Need {combat.GildCost} gold to Gild.";
            return null;
        }

        // The single entry point for mouse, keyboard G, gamepad LT and the trailer harness.
        private bool TryGild()
        {
            if(screen!=ScreenMode.Combat||combat==null)return false;
            var blocked=GildBlockedHint();
            if(blocked!=null){ShowInputHint(blocked);Sfx(SoundCue.UiDenied);return false;}
            var cost=combat.GildCost;
            // Rules first, Gold second: a refused arm can never charge the player.
            if(!combat.ArmGild()){ShowInputHint("Gild is unavailable right now.");Sfx(SoundCue.UiDenied);return false;}
            run.gold=Mathf.Max(0,run.gold-cost);observedGold=audioGold=run.gold;
            gildBurstAt=Time.unscaledTime;gildBurstCost=cost;cardPreviewCache.Clear();
            Sfx(SoundCue.GoldSpend,pan:-.3f);Sfx(SoundCue.Resonance,intensity:.4f,combatSound:true,delay:profile.reduceMotion?0:AnimationSeconds(.55f));
            ShowInputHint($"GILDED · −{cost} gold · your next card plays twice");
            // Run Gold and the armed checkpoint are written in one save, so resuming can
            // neither refund the Gold nor charge it again.
            SaveCombatCheckpoint();
            return true;
        }

        // ConsumeCombatEvents hook: the rules' GILDED receipt becomes a gold label at the
        // card's impact point instead of a generic status number.
        private bool PresentGildReceipt(CombatEvent fact,CardDef played,float at)
        {
            if(fact==null||fact.kind!=CombatEventKind.Status||fact.label!=CombatState.GildReceiptLabel)return false;
            var card=fact.card??played;
            combatNumbers.Add(new CombatNumber{text="GILDED ×"+Mathf.Max(2,fact.amount),origin=CardImpactPoint(card)+new Vector2(0,-72),color=new Color(1f,.80f,.30f),start=at});
            gildResolvedAt=at;gildResolvedPoint=CardImpactPoint(card);
            combatHistory.Add((card?.name??"A card")+" was gilded and resolves twice.");if(combatHistory.Count>40)combatHistory.RemoveAt(0);
            return true;
        }

        private void DrawGildControl()
        {
            if(combat==null)return;
            var now=Time.unscaledTime;var r=GildButtonRect;var armed=combat.gildArmed;var cost=combat.GildCost;
            var blocked=GildBlockedHint();var ready=blocked==null;var hot=!controllerNavigation&&dragView==null&&r.Contains(combatPointer);
            var used=!armed&&combat.gildUsedThisTurn;var poor=!armed&&!used&&combat.GildReady&&!combat.CanGild(run.gold);
            DrawGildHandAura(now);
            if(armed)DrawGildThread(now);
            DrawGildBadge(now,armed,ready,used,poor,hot,cost); // GildedGildBadge.cs
            DrawGildSplitFlourish(now,GildCoinCenter);
            DrawGildCoinBurst(now,GildCoinCenter);
            var detail=$"{cost} Gold: your next card plays twice. Once per turn; the price rises with each Gild.\n"+
                (armed?"ARMED · next card resolves twice (Curses and Statuses don't use it).":ready?$"READY · click or press {GildKeyLabel}. You have {run.gold} Gold.":blocked);
            RegisterCombatHudTarget("gild",6,r,"GILD",detail);
            if(hot)SetCombatEffectTooltip("GILD",detail,r.center);
            // Always clickable so a refused Gild explains itself through the input hint.
            if(GUI.Button(r,"",GUIStyle.none))TryGild();
        }

        // A molten ring hugging the rim: a bright head orbits with a cooling tail. The back
        // half is drawn behind the coin and the front half over it, so the ring wraps it.
        private void DrawGildMoltenRing(float now,Vector2 center,float radius,bool motion,bool rich,bool flashes,bool front)
        {
            const int dots=30;var head=motion?Mathf.Repeat(now*2.3f,Mathf.PI*2f):0f;var ring=radius+3f;
            for(var i=0;i<dots;i++)
            {
                var angle=i*Mathf.PI*2f/dots;var sin=Mathf.Sin(angle);if(front!=(sin>=0))continue;
                var trail=motion?Mathf.Repeat((head-angle)/(Mathf.PI*2f),1f):.6f;var heat=motion?Mathf.Pow(1f-trail,2.2f):.35f;
                var at=center+new Vector2(Mathf.Cos(angle)*ring,sin*ring);
                var color=Color.Lerp(new Color(1f,.46f,.08f),new Color(1f,.95f,.72f),heat);color.a=(.22f+.62f*heat)*(flashes?1f:.75f);
                DrawGildSprite(GildCoinGlow,at,7+7*heat,7+7*heat,color);
            }
            if(!motion)return;
            var sparks=rich?3:1;
            for(var k=0;k<sparks;k++)
            {
                var angle=now*(1.55f+.4f*k)+k*2.1f;var sin=Mathf.Sin(angle);if(front!=(sin>=0))continue;
                var orbit=radius+9f+3f*Mathf.Sin(now*2.1f+k*1.7f);var at=center+new Vector2(Mathf.Cos(angle)*orbit,sin*orbit*.9f);
                DrawGildSprite(GildCoinGlow,at,14,14,new Color(1f,.62f,.18f,.55f));DrawGildSprite(GildCoinGlow,at,5,5,new Color(1f,.98f,.88f,.95f));
            }
        }

        // Faint gold thread from the armed coin into the hand, swaying slowly.
        private void DrawGildThread(float now)
        {
            var from=GildCoinCenter+new Vector2(-36,-30);var sum=Vector2.zero;var count=0;
            foreach(var card in combat.hand)
            {
                if(card.instanceId==movingCard||!handViews.TryGetValue(card.instanceId,out var view)||now<view.readyAt||!combat.WillGild(card))continue;
                sum+=view.position;count++;
            }
            var to=count>0?sum/count:new Vector2(CombatWidth*.5f,CombatHeight-120);
            var motion=!profile.reduceMotion;var sway=motion?Mathf.Sin(now*1.25f)*16f:0f;
            var control=new Vector2((from.x+to.x)*.5f+sway,Mathf.Min(from.y,to.y)-70f+sway*.6f);
            const int steps=22;var previous=from;var bead=motion?Mathf.Repeat(now*.45f,1f):-1f;
            for(var i=1;i<=steps;i++)
            {
                var t=i/(float)steps;var point=GildArc(from,control,to,t);
                var glow=bead>=0?Mathf.Clamp01(1f-Mathf.Abs(t-bead)*9f):0f;
                DrawLine(previous,point,new Color(1f,.80f,.36f,(.30f+.40f*glow)*(1f-t*.55f)),1.4f+glow*1.2f);
                previous=point;
            }
        }

        // Sparks thrown off the coin as it flips to its "×2" face.
        private void DrawGildFlipSparks(float now,Vector2 center,float radius,bool motion,bool rich,bool flashes)
        {
            var t=now-gildBurstAt-GildFlipSeconds*.45f;const float life=.6f;if(t<0||t>life)return;
            var p=t/life;
            if(!motion){DrawGildSprite(GildCoinGlow,center,radius*3f,radius*3f,new Color(1f,.78f,.32f,.30f*(1-p)));return;}
            if(flashes)DrawGildSprite(GildCoinGlow,center,radius*(2.2f+p*1.4f),radius*(2.2f+p*1.4f),new Color(1f,.92f,.62f,.45f*(1-p)*(1-p)));
            var count=rich?14:6;
            for(var i=0;i<count;i++)
            {
                var angle=i*Mathf.PI*2f/count+(i%3)*.37f;var reach=radius*.7f+(1f-(1-p)*(1-p))*(34f+(i%4)*9f);
                var at=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*reach+Vector2.up*(p*p*14f);
                var size=3f+6f*(1-p);var a=(1-p)*(flashes?.95f:.55f);
                DrawGildSprite(GildCoinGlow,at,size*2.2f,size*2.2f,new Color(1f,.60f,.16f,a*.6f));DrawGildSprite(GildCoinGlow,at,size,size,new Color(1f,.96f,.80f,a));
            }
        }

        // Shadowed ribbon label; returns the drawn text width for inline layout.
        private float GildRibbonText(Rect r,string text,int size,Color color,TextAnchor anchor,Font font)
        {
            var style=new GUIStyle(footerStyle){font=font,fontSize=size,fontStyle=FontStyle.Bold,alignment=anchor,clipping=TextClipping.Overflow,wordWrap=false,normal={textColor=new Color(0,0,0,.75f*color.a)}};
            GUI.Label(new Rect(r.x,r.y+1,r.width,r.height),text,style);style.normal.textColor=color;GUI.Label(r,text,style);
            return style.CalcSize(new GUIContent(text)).x;
        }

        // When the gilded card resolves, the coin "splits": two small coins fly from the
        // medallion to the card's impact point and flash there.
        private void DrawGildSplitFlourish(float now,Vector2 from)
        {
            var t=now-gildResolvedAt;const float settle=.35f;if(t<0||t>GildSplitSeconds+settle)return;
            var to=gildResolvedPoint;var flashes=!profile.reduceFlashing;var rich=!profile.reducedVfx;
            if(profile.reduceMotion)
            {
                // Static: two coins appear at the impact point and fade; nothing travels.
                var fade=1f-t/(GildSplitSeconds+settle);
                DrawGildSprite(GildCoinGlow,to,84,84,new Color(1f,.76f,.30f,.30f*fade));
                DrawGildCoin(to+new Vector2(-13,0),26,1f,false,new Color(1f,1f,1f,fade));DrawGildCoin(to+new Vector2(13,0),26,1f,false,new Color(1f,1f,1f,fade));
                return;
            }
            var path=to-from;var normal=new Vector2(-path.y,path.x).normalized;
            if(t<.14f&&flashes)DrawGildSprite(GildCoinGlow,from,70,70,new Color(1f,.86f,.50f,.5f*(1-t/.14f)));
            var p=Mathf.Clamp01(t/GildSplitSeconds);var e=p*p*(3-2*p);
            if(p<1f)
                for(var k=0;k<2;k++)
                {
                    var side=k==0?-1f:1f;var start=from+new Vector2(side*9f,0);var end=to+new Vector2(side*10f,0);
                    var control=(from+to)*.5f+normal*side*70f+Vector2.down*40f;
                    var at=GildArc(start,control,end,e);
                    if(rich)for(var j=1;j<=4;j++){var back=GildArc(start,control,end,Mathf.Max(0,e-j*.045f));DrawGildSprite(GildCoinGlow,back,12-j*2,12-j*2,new Color(1f,.74f,.28f,.45f-j*.09f));}
                    DrawGildCoin(at,Mathf.Lerp(34,24,e),Mathf.Cos(t*15f+k*1.4f),false,Color.white);
                }
            else
            {
                var f=(t-GildSplitSeconds)/settle;
                DrawGildSprite(GildCoinGlow,to,50+90*f,50+90*f,flashes?new Color(1f,.94f,.74f,.75f*(1-f)):new Color(1f,.74f,.32f,.28f*(1-f)));
                DrawGildCoin(to+new Vector2(-10,0),24,1f,false,new Color(1f,1f,1f,1-f));DrawGildCoin(to+new Vector2(10,0),24,1f,false,new Color(1f,1f,1f,1-f));
                if(rich&&flashes)for(var k=0;k<8;k++){var a=k*Mathf.PI/4;var at=to+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(18+40*f);DrawGildSprite(GildCoinGlow,at,7,7,new Color(1f,.92f,.62f,.9f*(1-f)));}
            }
        }

        // Drawn behind the hand: only the rim outside each eligible card shows, so the
        // raised card is never overdrawn by its neighbours' glow.
        private void DrawGildHandAura(float now)
        {
            if(!combat.gildArmed)return;
            var calm=profile.reduceMotion||profile.reduceFlashing;
            foreach(var card in combat.hand)
            {
                if(card.instanceId==movingCard||!handViews.TryGetValue(card.instanceId,out var view)||now<view.readyAt||!combat.WillGild(card))continue;
                var pulse=calm?.5f:.5f+.5f*Mathf.Sin(now*3.1f+card.instanceId*.9f);
                var matrix=GUI.matrix;GUI.matrix=matrix*Matrix4x4.TRS(new Vector3(view.position.x,view.position.y,0),Quaternion.Euler(0,0,view.angle),new Vector3(view.scale,view.scale,1));
                var c=new Rect(-HandLayout.CardWidth*.5f,-HandLayout.CardHeight*.5f,HandLayout.CardWidth,HandLayout.CardHeight);
                if(!profile.reducedVfx)Fill(new Rect(c.x-8,c.y-8,c.width+16,c.height+16),new Color(1f,.64f,.18f,.08f+.07f*pulse));
                Outline(new Rect(c.x-4,c.y-4,c.width+8,c.height+8),new Color(1f,.82f,.40f,.34f+.28f*pulse),2);
                if(!profile.reduceMotion&&!profile.reducedVfx){var t=Mathf.Repeat(now*.42f+card.instanceId*.31f,1f);Fill(new Rect(c.x+t*(c.width-24),c.y-7,24,3),new Color(1f,.95f,.72f,.55f*Mathf.Sin(t*Mathf.PI)));}
                GUI.matrix=matrix;
            }
        }

        // Coins arc from the shared gold counter into the medallion.
        private void DrawGildCoinBurst(float now,Vector2 to)
        {
            var elapsed=now-gildBurstAt;if(elapsed<0||elapsed>GildBurstSeconds)return;
            var fade=1-elapsed/GildBurstSeconds;var from=RunGoldIconRect.center;
            GUI.Label(new Rect(from.x+4,58+(profile.reduceMotion?0:elapsed*14),96,26),"−"+gildBurstCost,new GUIStyle(titleStyle){font=labelFont?labelFont:bodyFont,fontSize=18,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(1f,.64f,.36f,fade)}});
            if(profile.reduceMotion){DrawGildSprite(GildCoinGlow,to,GildCoinDiameter*1.5f,GildCoinDiameter*1.5f,new Color(1f,.84f,.44f,.35f*fade));return;}
            // The badge is in the right-hand column: the coins run along the top bar, then drop down the right edge, clear of the hand fan.
            var control=new Vector2(to.x-30,from.y+50);var coins=profile.reducedVfx?3:6;
            for(var i=0;i<coins;i++)
            {
                var p=Mathf.Clamp01((elapsed-i*.06f)/.55f);if(p<=0||p>=1)continue;
                var e=p*p*(3-2*p);var bend=control+new Vector2((i%2==0?-1:1)*(4+i*3),0);var at=GildArc(from,bend,to,e);
                if(!profile.reducedVfx){var trail=GildArc(from,bend,to,Mathf.Max(0,e-.07f));DrawLine(trail,at,new Color(1f,.78f,.32f,.35f),2);}
                DrawGildCoin(at,Mathf.Lerp(17,11,e),Mathf.Cos(elapsed*13f+i*1.1f),false,Color.white);
            }
            var land=Mathf.Clamp01((elapsed-.55f)/.3f);
            if(land>0&&land<1&&!profile.reducedVfx&&!profile.reduceFlashing)
                for(var k=0;k<6;k++){var a=k*Mathf.PI/3+elapsed*3;var radius=GildCoinDiameter*.5f+land*22;DrawGildSprite(GildCoinGlow,to+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,6,6,new Color(1f,.86f,.46f,(1-land)*.85f));}
        }
        private static Vector2 GildArc(Vector2 a,Vector2 control,Vector2 b,float t){var u=1-t;return u*u*a+2*u*t*control+t*t*b;}
    }
}
