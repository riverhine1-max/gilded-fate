using GildedFate.Combat;
using UnityEngine;

namespace GildedFate.UI
{
    // End Turn seal: a forged gold plate with clear states instead of a plain box.
    //   Ready        - gold rim, "END TURN", incoming enemy damage underneath.
    //   No plays left- the plate breathes a warm glow so the next action is obvious.
    //   Hover/press  - brighter plate, slight lift, pressed dip.
    //   Busy         - dimmed plate, "ENEMY TURN" / "RESOLVING…".
    // Presentation only; the same EndTurnRect is the click/controller target.
    // Reduce Motion stops the breathing and lift; Reduce Flashing dims the glow.
    public sealed partial class GildedMainMenu
    {
        private Texture2D endTurnPlate;
        private float endTurnPressedAt=-10;
        private static readonly Vector2[] EndTurnShape={new(.055f,0),new(.945f,0),new(1,.5f),new(.945f,1),new(.055f,1),new(0,.5f)};

        private bool AnyPlayableCard()
        {
            if(combat==null)return false;
            foreach(var card in combat.hand)if(combat.CanPlay(card))return true;
            return false;
        }
        private int IncomingEnemyDamage()
        {
            if(combat==null||combat.IsOver)return 0;var total=0;
            for(var i=0;i<combat.EnemyCount;i++)
            {
                if(!combat.IsLivingTarget(i))continue;
                foreach(var action in EnemyIntents(i))if(!action.prevented)total+=action.TotalDamage;
            }
            return total;
        }
        // Returns true when clicked.
        private bool DrawEndTurnButton(Rect r,bool enabled)
        {
            if(!endTurnPlate)endTurnPlate=CreateCardUiPolygon("End turn plate",364,108,EndTurnShape,true);
            var now=Time.unscaledTime;var motion=!profile.reduceMotion;
            var hover=enabled&&!controllerNavigation&&r.Contains(combatPointer);
            var idle=enabled&&!AnyPlayableCard();
            var press=Mathf.Clamp01(1-(now-endTurnPressedAt)/.18f);
            var breathe=idle?(motion?(Mathf.Sin(now*3.1f)+1)*.5f:.6f):0;
            // Lift on hover, dip when pressed.
            var shown=r;
            if(motion){var grow=(hover?3:0)-press*3;shown=new Rect(r.x-grow,r.y-grow*.5f-(hover?2:0)+press*2,r.width+grow*2,r.height+grow);}
            var old=GUI.color;
            if(CardVfxRepaint)
            {
                EnsureCardVfxTextures();
                // Soft glow: always faint, strong and breathing when there is nothing left to play.
                var glowA=enabled?(.10f+(hover?.12f:0)+breathe*(profile.reduceFlashing?.18f:.38f)):0;
                if(glowA>0)DrawCardUiShape(new Rect(shown.x-40,shown.y-26,shown.width+80,shown.height+52),cardVfxGlow,new Color(1f,.70f,.26f,glowA));
                // Shadow, gold rim, dark forged body, inner bronze line.
                DrawCardUiShape(new Rect(shown.x+2,shown.y+4,shown.width,shown.height),endTurnPlate,new Color(0,0,0,.55f));
                var rim=!enabled?new Color(.42f,.36f,.26f):Color.Lerp(new Color(.86f,.62f,.26f),new Color(1f,.86f,.48f),hover?1:breathe*.7f);
                DrawCardUiShape(shown,endTurnPlate,rim);
                var body=new Rect(shown.x+3,shown.y+3,shown.width-6,shown.height-6);
                DrawCardUiShape(body,endTurnPlate,!enabled?new Color(.07f,.07f,.08f):hover?new Color(.30f,.18f,.06f):Color.Lerp(new Color(.16f,.10f,.04f),new Color(.26f,.16f,.05f),breathe));
                var inner=new Rect(shown.x+8,shown.y+8,shown.width-16,shown.height-16);
                DrawCardUiShape(inner,endTurnPlate,new Color(rim.r,rim.g,rim.b,enabled?.35f:.18f));
                DrawCardUiShape(new Rect(inner.x+1.5f,inner.y+1.5f,inner.width-3,inner.height-3),endTurnPlate,!enabled?new Color(.06f,.06f,.07f):hover?new Color(.27f,.16f,.05f):Color.Lerp(new Color(.12f,.08f,.035f),new Color(.22f,.13f,.045f),breathe));
                // Polished top lip.
                Fill(new Rect(shown.x+shown.width*.12f,shown.y+4,shown.width*.76f,1.5f),new Color(1f,.9f,.62f,enabled?.55f:.18f));
                // Pointed end studs.
                var stud=new Color(rim.r,rim.g,rim.b,enabled?1:.6f);
                if(!ShowPadGlyphs)DrawCardUiShape(new Rect(shown.x+11,shown.center.y-4,8,8),cardVfxPip,stud);
                DrawCardUiShape(new Rect(shown.xMax-19,shown.center.y-4,8,8),cardVfxPip,stud);
            }
            var incoming=enabled?IncomingEnemyDamage():0;
            var title=!enabled?(combat!=null&&combat.phase!=CombatPhase.Player?"ENEMY TURN":"RESOLVING…"):"END TURN";
            var titleColor=!enabled?new Color(.62f,.58f,.52f):hover||idle?new Color(1f,.95f,.80f):new Color(1f,.87f,.60f);
            var titleStyle2=new GUIStyle(titleStyle){font=labelFont?labelFont:bodyFont,fontSize=incoming>0||idle?20:22,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=titleColor}};
            var sub=incoming>0||idle;
            ShadowLabel(new Rect(shown.x,shown.y+(sub?5:0),shown.width,sub?shown.height*.56f:shown.height),title,titleStyle2);
            if(sub)
            {
                var text=idle&&incoming<=0?"NO PLAYS LEFT":incoming>0?(idle?"NO PLAYS · "+incoming+" INCOMING":"INCOMING "+incoming):"";
                var subStyle=new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=11,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=incoming>0?new Color(1f,.55f,.45f):new Color(.95f,.80f,.52f)}};
                GUI.Label(new Rect(shown.x,shown.y+shown.height*.56f,shown.width,shown.height*.34f),text,subStyle);
            }
            GUI.color=old;
            if(ShowPadGlyphs)DrawPadGlyph(new Vector2(shown.x+20,shown.center.y),"Y",enabled); // Y ends the turn
            if(enabled&&GUI.Button(r,"",GUIStyle.none)){endTurnPressedAt=now;return true;}
            DeniedPress(r,!enabled&&combat!=null&&!combat.IsOver&&pileOpen<0&&!combatPauseOpen&&inspectedCard==null&&inspectedRelic==null);
            return false;
        }
    }
}
