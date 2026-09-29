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
        private const float GildBurstSeconds=.9f;
        // Left of the hand fan, above the Deck pile and beside the Energy meter. The
        // fan's leftmost card (7+ cards, rotated) never reaches x<112, so the button
        // is never under a resting or raised card and never competes with card picking.
        private Rect GildButtonRect=>new Rect(14,CombatHeight-202,94,82);
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
            gildResolvedAt=at;
            combatHistory.Add((card?.name??"A card")+" was gilded and resolves twice.");if(combatHistory.Count>40)combatHistory.RemoveAt(0);
            return true;
        }

        private void DrawGildControl()
        {
            if(combat==null)return;
            var now=Time.unscaledTime;var r=GildButtonRect;var armed=combat.gildArmed;var cost=combat.GildCost;
            var blocked=GildBlockedHint();var ready=blocked==null;var hot=!controllerNavigation&&dragView==null&&r.Contains(combatPointer);
            DrawGildHandAura(now);
            var calm=profile.reduceMotion||profile.reduceFlashing;var wave=calm?.5f:.5f+.5f*Mathf.Sin(now*(armed?5.4f:2.2f));
            var molten=new Color(1f,.55f,.12f);var bright=new Color(1f,.88f,.50f);var font=labelFont?labelFont:bodyFont;
            if(armed){Fill(new Rect(r.x-9,r.y-9,r.width+18,r.height+18),new Color(molten.r,molten.g,molten.b,.10f+.14f*wave));Fill(new Rect(r.x-4,r.y-4,r.width+8,r.height+8),new Color(1f,.74f,.28f,.16f+.16f*wave));}
            else if(ready)Fill(new Rect(r.x-6,r.y-6,r.width+12,r.height+12),new Color(1f,.70f,.22f,(hot?.17f:.07f)+.05f*wave));
            Fill(r,armed?new Color(.21f,.09f,.015f,.96f):ready?(hot?new Color(.20f,.125f,.035f,.95f):new Color(.035f,.03f,.026f,.94f)):new Color(.02f,.024f,.03f,.88f));
            var edge=armed?Color.Lerp(molten,bright,wave):ready?(hot?bright:new Color(.86f,.64f,.27f,.95f)):new Color(.32f,.30f,.27f,.8f);
            Outline(r,edge,armed||hot?3:2);Outline(new Rect(r.x+5,r.y+5,r.width-10,r.height-10),new Color(edge.r*.45f,edge.g*.38f,edge.b*.28f,.8f),1);
            // A bright bead runs the top rim while armed, like metal still flowing.
            if(armed&&!profile.reduceMotion&&!profile.reducedVfx){var t=Mathf.Repeat(now*.9f,1f);Fill(new Rect(r.x+4+t*(r.width-30),r.y,22,3),new Color(1f,.96f,.74f,.9f));}
            var text=armed?bright:ready?Gold:new Color(.52f,.50f,.46f);
            GUI.Label(new Rect(r.x,r.y+5,r.width,20),"GILD",new GUIStyle(titleStyle){font=font,fontSize=15,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=text}});
            if(armed)GUI.Label(new Rect(r.x,r.y+24,r.width,32),"×2",new GUIStyle(titleStyle){font=font,fontSize=25,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white}});
            else
            {
                var old=GUI.color;if(!ready)GUI.color=new Color(.62f,.62f,.62f,.7f);
                var coin=new Rect(r.center.x-31,r.y+29,24,24);DrawGoldIcon(coin);GUI.color=old;
                GUI.Label(new Rect(coin.xMax+3,r.y+25,r.width*.55f,32),cost.ToString(),new GUIStyle(titleStyle){font=font,fontSize=21,alignment=TextAnchor.MiddleLeft,normal={textColor=ready?new Color(1f,.9f,.62f):new Color(.56f,.54f,.5f)}});
            }
            var status=armed?"ARMED":ready?GildKeyLabel+" · READY":combat.gildUsedThisTurn?"NEXT TURN":combat.GildReady&&!combat.CanGild(run.gold)?"NEED GOLD":"WAIT";
            GUI.Label(new Rect(r.x,r.yMax-24,r.width,18),status,new GUIStyle(footerStyle){font=font,fontSize=armed?11:9,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=armed?new Color(1f,.93f,.66f):text}});
            // Short flare when the gilded card resolves and the gild is spent.
            var spent=now-gildResolvedAt;
            if(spent>=0&&spent<.45f&&!profile.reduceFlashing){var a=1-spent/.45f;var grow=profile.reduceMotion?4:4+14*(1-a);Outline(new Rect(r.x-grow,r.y-grow,r.width+grow*2,r.height+grow*2),new Color(1f,.84f,.42f,a*.85f),2);}
            DrawGildCoinBurst(now,r);
            var detail=$"Spend {cost} gold. Your next card plays twice. Once per turn; the price rises each time this combat.\n\n"+
                (armed?"ARMED · the next playable card you play resolves twice. Curses and Statuses never spend it.":ready?$"READY · click, or press {GildKeyLabel}. You have {run.gold} gold.":blocked);
            RegisterCombatHudTarget("gild",6,r,"GILD",detail);
            if(hot)SetCombatEffectTooltip("GILD",detail,r.center);
            // Always clickable so a refused Gild explains itself through the input hint.
            if(GUI.Button(r,"",GUIStyle.none))TryGild();
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

        // Coins arc from the shared gold counter into the button.
        private void DrawGildCoinBurst(float now,Rect button)
        {
            var elapsed=now-gildBurstAt;if(elapsed<0||elapsed>GildBurstSeconds)return;
            var fade=1-elapsed/GildBurstSeconds;var from=RunGoldIconRect.center;var to=button.center;
            GUI.Label(new Rect(from.x+4,58+(profile.reduceMotion?0:elapsed*14),96,26),"−"+gildBurstCost,new GUIStyle(titleStyle){font=labelFont?labelFont:bodyFont,fontSize=18,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(1f,.64f,.36f,fade)}});
            if(profile.reduceMotion){Outline(new Rect(button.x-3,button.y-3,button.width+6,button.height+6),new Color(1f,.86f,.46f,fade),2);return;}
            // Bow toward the hero side so the flight stays clear of the Shard shrine and the hand fan.
            var control=new Vector2(from.x+90,(from.y+to.y)*.5f);var coins=profile.reducedVfx?3:6;
            for(var i=0;i<coins;i++)
            {
                var p=Mathf.Clamp01((elapsed-i*.06f)/.55f);if(p<=0||p>=1)continue;
                var e=p*p*(3-2*p);var at=GildArc(from,control+new Vector2((i%2==0?-1:1)*(4+i*3),0),to,e);
                var size=Mathf.Lerp(12,7,e);var color=Color.Lerp(new Color(1f,.92f,.55f),new Color(1f,.62f,.16f),e);
                if(!profile.reducedVfx){var trail=GildArc(from,control,to,Mathf.Max(0,e-.07f));DrawLine(trail,at,new Color(1f,.78f,.32f,.35f),2);}
                var h=size*.5f;
                Fill(new Rect(at.x-h,at.y-h*.62f,size,size*.62f),color);Fill(new Rect(at.x-h*.62f,at.y-h,size*.62f,size),color);
                Fill(new Rect(at.x-h*.4f,at.y-h*.55f,size*.32f,size*.24f),new Color(1f,.98f,.86f,.95f));
            }
            var land=Mathf.Clamp01((elapsed-.55f)/.3f);
            if(land>0&&land<1&&!profile.reducedVfx&&!profile.reduceFlashing)
                for(var k=0;k<6;k++){var a=k*Mathf.PI/3+elapsed*3;var radius=16+land*28;Fill(new Rect(to.x+Mathf.Cos(a)*radius-1.5f,to.y+Mathf.Sin(a)*radius-1.5f,3,3),new Color(1f,.86f,.46f,(1-land)*.8f));}
        }
        private static Vector2 GildArc(Vector2 a,Vector2 control,Vector2 b,float t){var u=1-t;return u*u*a+2*u*t*control+t*t*b;}
    }
}
