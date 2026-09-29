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
        private const float GildBurstSeconds=.9f,GildFlipSeconds=.42f,GildSplitSeconds=.62f,GildCoinDiameter=86f;
        private Vector2 gildResolvedPoint;
        // Left of the hand fan, above the Deck pile and beside the Energy meter. The
        // fan's leftmost card (7+ cards, rotated) never reaches x<112, so the medallion
        // is never under a resting or raised card and never competes with card picking.
        // The coin sits above the Deck pile (y>=H-104) and its ribbon ends above/left of
        // the Energy orb (x>=103, y>=H-112); the second Shard socket label ends above it.
        private Vector2 GildCoinCenter=>new Vector2(61,CombatHeight-170);
        private Rect GildRibbonRect=>new Rect(19,CombatHeight-133,84,19);
        private Rect GildButtonRect=>new Rect(17,CombatHeight-213,88,100);
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
            var motion=!profile.reduceMotion;var flashes=!profile.reduceFlashing;var rich=!profile.reducedVfx;
            DrawGildHandAura(now);
            if(armed)DrawGildThread(now);
            var rest=GildCoinCenter;var center=rest;const float d=GildCoinDiameter;
            if(ready&&hot)center.y-=4;
            // Face and turn: the flip to "×2" right after a Gild, or the idle half-turns when ready.
            var flip=now-gildBurstAt;var turn=1f;var doubled=armed;
            if(motion&&armed&&flip>=0&&flip<GildFlipSeconds){var p=flip/GildFlipSeconds;turn=Mathf.Cos(p*Mathf.PI);doubled=p>=.5f;}
            else if(motion&&ready){var cycle=Mathf.Repeat(now,4.6f);if(cycle<.9f)turn=Mathf.Cos(cycle/.9f*Mathf.PI*2f);}
            var tint=armed?Color.white:ready?(hot?Color.white:new Color(.93f,.91f,.86f)):used?new Color(.70f,.50f,.36f):poor?new Color(.72f,.66f,.54f):new Color(.52f,.50f,.47f,.80f);
            var wave=motion?.5f+.5f*Mathf.Sin(now*(armed?4.2f:1.9f)):.5f;
            // Drop shadow, then the warm (ready) or molten (armed) glow behind the coin.
            DrawGildSprite(GildCoinGlow,rest+new Vector2(2,6),d*1.12f,d*1.02f,new Color(0,0,0,ready||armed?.62f:.45f));
            if(armed)DrawGildSprite(GildCoinGlow,center,d*1.75f,d*1.75f,new Color(1f,.52f,.12f,(flashes?.40f:.28f)+.14f*wave));
            else if(ready)DrawGildSprite(GildCoinGlow,center,d*1.55f,d*1.55f,new Color(1f,.70f,.24f,(hot?.40f:.22f)+.08f*wave));
            if(armed)DrawGildMoltenRing(now,center,d*.5f,motion,rich,flashes,false);
            DrawGildCoin(center,d,turn,doubled,tint);
            if(ready&&hot)DrawGildSprite(GildCoinGlow,center,d*.95f*Mathf.Max(.05f,Mathf.Abs(turn)),d*.95f,new Color(1f,.96f,.80f,.18f));
            if(armed)DrawGildMoltenRing(now,center,d*.5f,motion,rich,flashes,true);
            // Specular glint: a streak swept across the face, clipped to the coin's chord.
            if(motion&&(ready||armed)&&Mathf.Abs(turn)>.985f)
            {
                var period=armed?2.4f:4.6f;var cycle=Mathf.Repeat(now,period);var start=armed?.5f:2.3f;var p=(cycle-start)/.75f;
                if(p>0&&p<1)
                {
                    var radius=d*.5f;var s=Mathf.Lerp(-radius*.92f,radius*.92f,p);var dir=new Vector2(.82f,.57f);var chord=2f*Mathf.Sqrt(Mathf.Max(0,radius*radius-s*s))*.9f;
                    var a=Mathf.Sin(p*Mathf.PI)*(flashes?.55f:.30f);
                    DrawGildSprite(GildCoinGlow,center+dir*s,11,chord,new Color(1f,.97f,.86f,a),35f);
                    DrawGildSprite(GildCoinGlow,center+dir*s,4,chord*.8f,new Color(1f,1f,.96f,a),35f);
                }
            }
            DrawGildFlipSparks(now,center,d*.5f,motion,rich,flashes);
            DrawGildRibbon(GildRibbonRect,armed,ready,used,poor,cost);
            DrawGildSplitFlourish(now,rest);
            DrawGildCoinBurst(now,rest);
            var detail=$"Spend {cost} gold. Your next card plays twice. Once per turn; the price rises each time this combat.\n\n"+
                (armed?"ARMED · the next playable card you play resolves twice. Curses and Statuses never spend it.":ready?$"READY · click, or press {GildKeyLabel}. You have {run.gold} gold.":blocked);
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
            var from=GildCoinCenter+new Vector2(28,-24);var sum=Vector2.zero;var count=0;
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

        // Price and key banner under the coin; its words change with the Gild state.
        private void DrawGildRibbon(Rect r,bool armed,bool ready,bool used,bool poor,int cost)
        {
            var body=armed?new Color(.46f,.13f,.035f,.97f):ready?new Color(.40f,.07f,.07f,.96f):used?new Color(.22f,.13f,.08f,.94f):poor?new Color(.22f,.09f,.08f,.94f):new Color(.12f,.11f,.11f,.90f);
            var trim=armed?new Color(1f,.84f,.44f):ready?new Color(1f,.76f,.33f):used?new Color(.58f,.40f,.24f):poor?new Color(.52f,.44f,.34f):new Color(.36f,.34f,.31f);
            var tail=new Color(body.r*.62f,body.g*.62f,body.b*.62f,body.a);
            Fill(new Rect(r.x-5,r.y+4,11,r.height-1),tail);Fill(new Rect(r.xMax-6,r.y+4,11,r.height-1),tail);
            Fill(new Rect(r.x-5,r.yMax+2,3,1),tail);Fill(new Rect(r.xMax+2,r.yMax+2,3,1),tail);
            Fill(r,body);Fill(new Rect(r.x,r.y,r.width,1),trim);Fill(new Rect(r.x,r.yMax-1,r.width,1),new Color(trim.r*.7f,trim.g*.6f,trim.b*.5f));
            Fill(new Rect(r.x,r.y+2,r.width,1),new Color(1f,1f,1f,.06f));
            var font=labelFont?labelFont:bodyFont;
            if(armed){GildRibbonText(new Rect(r.x,r.y,r.width,r.height),"×2 ARMED",11,new Color(1f,.93f,.66f),TextAnchor.MiddleCenter,font);return;}
            if(used){GildRibbonText(new Rect(r.x,r.y,r.width,r.height),"NEXT TURN",10,new Color(.82f,.64f,.48f),TextAnchor.MiddleCenter,font);return;}
            // Price and coin glyph; the key cap (G / LT) sits on the right unless Gold is short.
            var textColor=ready?new Color(1f,.92f,.66f):poor?new Color(1f,.46f,.36f):new Color(.62f,.60f,.56f);
            var x=r.x+(poor?7:11);
            if(poor)x+=GildRibbonText(new Rect(x,r.y,40,r.height),"NEED",9,new Color(.88f,.68f,.60f),TextAnchor.MiddleLeft,font)+4;
            x+=GildRibbonText(new Rect(x,r.y-1,40,r.height+2),cost.ToString(),13,textColor,TextAnchor.MiddleLeft,font);
            DrawGildCoin(new Vector2(x+8,r.center.y),12,1f,false,ready?Color.white:poor?new Color(.85f,.72f,.60f):new Color(.60f,.58f,.54f,.85f));
            if(poor)return;
            var key=new Rect(r.xMax-23,r.y+3,20,r.height-6);
            Fill(key,new Color(0,0,0,.45f));Outline(key,new Color(trim.r,trim.g,trim.b,.75f),1);
            GildRibbonText(key,GildKeyLabel,GildKeyLabel.Length>1?8:10,ready?Color.white:new Color(.66f,.64f,.60f),TextAnchor.MiddleCenter,font);
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
            // Bow toward the hero side so the flight stays clear of the Shard shrine and the hand fan.
            var control=new Vector2(from.x+90,(from.y+to.y)*.5f);var coins=profile.reducedVfx?3:6;
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
