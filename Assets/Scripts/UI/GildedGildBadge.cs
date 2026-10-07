using GildedFate.Combat;
using UnityEngine;

namespace GildedFate.UI
{
    // Polish pass: the GILD badge. The coin used to sit small in the bottom-left corner under the shard sockets, with a thin
    // ribbon, and it was easy to forget it existed. It now lives directly under End Turn (the control every turn passes
    // through), as a minted coin set in a forged bezel on a nameplate that matches the End Turn plate:
    //   Ready  - the coin turns now and then, a warm ring pulses outward every few seconds, and the plate shows
    //            "GILD", the price and its key. When a new turn starts and Gild can be afforded, the bezel and plate flash once.
    //   Armed  - molten ring around the bezel, plate reads "GILDED · NEXT CARD ×2".
    //   Spent  - bronze and quiet, "NEXT TURN".
    //   Short of gold - the price in red with "NEED".
    //   Not now (enemy turn, a card resolving) - dim, with the price still shown, so the layout never jumps.
    // State changes fade (colours ease toward the new state) instead of flipping in one frame.
    // Presentation only: GildedGildingPresentation.cs still owns paying, arming and the shortcut.
    public sealed partial class GildedMainMenu
    {
        private static Texture2D gildBezelTexture,gildPlateTexture;
        private static readonly Vector2[] GildPlateShape={new(.07f,0),new(.93f,0),new(1,.2f),new(1,.8f),new(.93f,1),new(.07f,1),new(0,.8f),new(0,.2f)};
        private int gildAnnouncedTurn=-1;
        private float gildAnnounceAt=-10f;
        private Color gildRimLook,gildBodyLook,gildTintLook;
        private bool gildLookSeeded;
        private const float GildBezelDiameter=112f;

        private void ResetGildBadge(){gildAnnouncedTurn=-1;gildAnnounceAt=-10f;gildLookSeeded=false;}

        // ---------- textures ----------
        private static void EnsureGildBadgeTextures()
        {
            if(!gildBezelTexture)gildBezelTexture=BuildGildBezelTexture();
            if(!gildPlateTexture)gildPlateTexture=CreateCardUiPolygon("Gild plate",204,108,GildPlateShape,true);
        }

        // A bronze-and-gold ring with a raised outer rim, a groove and twelve rivets, lit from the upper left like the coin.
        private static Texture2D BuildGildBezelTexture()
        {
            const int n=192;var pixels=new Color[n*n];const float e=1.6f/n;
            var light=new Vector3(-.45f,.55f,.70f).normalized;var half=(light+Vector3.forward).normalized;
            var deep=new Color(.25f,.13f,.035f);var mid=new Color(.74f,.51f,.18f);var pale=new Color(1f,.89f,.57f);
            for(var y=0;y<n;y++)for(var x=0;x<n;x++)
            {
                var u=(x+.5f)/n*2-1;var v=(y+.5f)/n*2-1;var r=Mathf.Sqrt(u*u+v*v);
                var alpha=Mathf.Clamp01((1f-r)*n*.5f)*Mathf.Clamp01((r-.79f)*n*.5f);
                if(alpha<=0){pixels[y*n+x]=new Color(deep.r,deep.g,deep.b,0);continue;}
                var h=GildBezelHeight(u,v);
                var dx=(GildBezelHeight(u+e,v)-GildBezelHeight(u-e,v))/(2*e);
                var dy=(GildBezelHeight(u,v+e)-GildBezelHeight(u,v-e))/(2*e);
                var normal=new Vector3(-dx*.07f,-dy*.07f,1f).normalized;
                var diffuse=Mathf.Clamp01(Vector3.Dot(normal,light));var spec=Mathf.Pow(Mathf.Clamp01(Vector3.Dot(normal,half)),26f);
                var sweep=Mathf.Clamp01(Vector2.Distance(new Vector2(u,v),new Vector2(-.42f,.46f))/1.8f);
                var baseColor=sweep<.5f?Color.Lerp(pale,mid,sweep*2):Color.Lerp(mid,deep,(sweep-.5f)*2);
                var c=baseColor*(.34f+.86f*diffuse)*Mathf.Lerp(.72f,1f,Mathf.Clamp01(h))+new Color(1f,.95f,.80f)*(spec*.5f);
                // Contact shadow where the bezel meets the coin, milled lip on the outer edge.
                c=Color.Lerp(c,new Color(.12f,.06f,.015f),Mathf.Clamp01((.86f-r)/.07f)*.55f);
                c=Color.Lerp(c,new Color(.30f,.15f,.03f),Mathf.Clamp01((r-.965f)/.035f)*.7f);
                c.r=Mathf.Clamp01(c.r);c.g=Mathf.Clamp01(c.g);c.b=Mathf.Clamp01(c.b);c.a=alpha;
                pixels[y*n+x]=c;
            }
            return NewGildTexture("Gild bezel",n,pixels,true);
        }

        private static float GildBezelHeight(float u,float v)
        {
            var r=Mathf.Sqrt(u*u+v*v);var t=(r-.79f)/.21f; // 0 at the inner edge, 1 at the outer edge
            var lip=(1f-GildSmooth(.10f,.30f,t))*GildSmooth(-.02f,.08f,t)*.55f;      // inner lip that holds the coin
            var plateau=GildSmooth(.08f,.22f,t)*.20f;                                    // flat band the rivets sit on
            var groove=GildBand(t,.60f,.68f,.045f)*.16f;                                  // engraved line
            var rim=GildSmooth(.74f,.88f,t)*(1f-GildSmooth(.94f,1f,t))*.62f;             // raised, rolled outer rim
            var h=Mathf.Max(lip,Mathf.Max(plateau-groove,rim));
            const int rivets=12;var step=Mathf.PI*2f/rivets;var angle=Mathf.Round(Mathf.Atan2(v,u)/step)*step;
            var rivet=1f-GildSmooth(.014f,.028f,Vector2.Distance(new Vector2(u,v),new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*.875f));
            return Mathf.Max(h,rivet*.62f);
        }

        // ---------- the badge ----------
        private void DrawGildBadge(float now,bool armed,bool ready,bool used,bool poor,bool hot,int cost)
        {
            var motion=!profile.reduceMotion;var flashes=!profile.reduceFlashing;var rich=!profile.reducedVfx;
            EnsureGildBadgeTextures();EnsureCardVfxTextures();
            var center=GildCoinCenter;var plate=GildPlateRect;const float d=GildCoinDiameter;const float D=GildBezelDiameter;
            var held=hot&&ready&&PointerHeld;
            // New turn with Gild available: announce it once.
            if(ready&&combat!=null&&gildAnnouncedTurn!=combat.turn){gildAnnouncedTurn=combat.turn;gildAnnounceAt=now;}

            // The look of each state, eased so a card resolving or the turn changing never flips the badge in one frame.
            Color rimGoal,bodyGoal,tintGoal;
            if(armed){rimGoal=new Color(1f,.80f,.40f);bodyGoal=new Color(.32f,.11f,.03f);tintGoal=Color.white;}
            else if(ready){rimGoal=hot?new Color(1f,.88f,.52f):new Color(.86f,.62f,.26f);bodyGoal=hot?new Color(.30f,.18f,.06f):new Color(.16f,.10f,.04f);tintGoal=hot?Color.white:new Color(.95f,.93f,.88f);}
            else if(used){rimGoal=new Color(.50f,.36f,.22f);bodyGoal=new Color(.11f,.08f,.05f);tintGoal=new Color(.70f,.50f,.36f);}
            else if(poor){rimGoal=new Color(.58f,.40f,.30f);bodyGoal=new Color(.19f,.07f,.06f);tintGoal=new Color(.74f,.66f,.54f);}
            else{rimGoal=new Color(.42f,.36f,.26f);bodyGoal=new Color(.07f,.07f,.08f);tintGoal=new Color(.52f,.50f,.47f);}
            if(!gildLookSeeded||!motion){gildRimLook=rimGoal;gildBodyLook=bodyGoal;gildTintLook=tintGoal;gildLookSeeded=true;}
            else if(Event.current.type==EventType.Repaint)
            {
                var k=1-Mathf.Exp(-9f*Mathf.Min(.05f,Time.unscaledDeltaTime));
                gildRimLook=Color.Lerp(gildRimLook,rimGoal,k);gildBodyLook=Color.Lerp(gildBodyLook,bodyGoal,k);gildTintLook=Color.Lerp(gildTintLook,tintGoal,k);
            }
            var wave=motion?.5f+.5f*Mathf.Sin(now*(armed?4.2f:1.9f)):.5f;

            // Shadow, then the warm (ready) or molten (armed) glow behind the bezel.
            DrawGildSprite(GildCoinGlow,center+new Vector2(2,7),D*1.12f,D*1.02f,new Color(0,0,0,ready||armed?.62f:.45f));
            if(armed)DrawGildSprite(GildCoinGlow,center,D*1.8f,D*1.8f,new Color(1f,.52f,.12f,(flashes?.40f:.28f)+.14f*wave));
            else if(ready)DrawGildSprite(GildCoinGlow,center,D*1.6f,D*1.6f,new Color(1f,.70f,.24f,(hot?.40f:motion?.20f:.30f)+.08f*wave));

            // A ring that pulses outward every few seconds while Gild is available and unused.
            if(ready&&motion)
            {
                var p=Mathf.Repeat(now,3.4f)/1.5f;
                if(p<1f){var e=1f-(1f-p)*(1f-p);var size=Mathf.Lerp(D*.96f,D*1.75f,e);DrawCardUiShape(new Rect(center.x-size*.5f,center.y-size*.5f,size,size),cardVfxRing,new Color(1f,.78f,.38f,(1f-p)*(flashes?.42f:.22f)));}
            }
            var announce=(now-gildAnnounceAt)/1.2f;var announcing=announce>=0&&announce<1;
            if(announcing)
            {
                var e=1f-(1f-announce)*(1f-announce);var size=Mathf.Lerp(D*.9f,D*2.1f,e);var fade=(1f-announce)*(1f-announce);
                DrawCardUiShape(new Rect(center.x-size*.5f,center.y-size*.5f,size,size),cardVfxRing,new Color(1f,.86f,.5f,fade*(flashes?.8f:.4f)));
                if(flashes)DrawGildSprite(GildCoinGlow,center,D*1.5f,D*1.5f,new Color(1f,.9f,.62f,.45f*fade));
            }

            DrawGildPlate(plate,gildRimLook,gildBodyLook,ready?(motion?.5f+.5f*Mathf.Sin(now*2.4f):.4f):0f);
            if(armed)DrawGildMoltenRing(now,center,D*.5f-3f,motion,rich,flashes,false);
            DrawGildSprite(gildBezelTexture,center,D,D,new Color(gildTintLook.r,gildTintLook.g,gildTintLook.b,1f));

            // Face and turn: the flip to "×2" right after a Gild, or an occasional half-turn while Gild is ready.
            var flip=now-gildBurstAt;var turn=1f;var doubled=armed;
            if(motion&&armed&&flip>=0&&flip<GildFlipSeconds){var p=flip/GildFlipSeconds;turn=Mathf.Cos(p*Mathf.PI);doubled=p>=.5f;}
            else if(motion&&ready){var cycle=Mathf.Repeat(now,4.6f);if(cycle<.9f)turn=Mathf.Cos(cycle/.9f*Mathf.PI*2f);}
            DrawGildCoin(center,d*(ready&&hot?(held?.96f:1.05f):1f),turn,doubled,gildTintLook);
            if(ready&&hot)DrawGildSprite(GildCoinGlow,center,d*.95f*Mathf.Max(.05f,Mathf.Abs(turn)),d*.95f,new Color(1f,.96f,.80f,.18f));
            if(armed)DrawGildMoltenRing(now,center,D*.5f-3f,motion,rich,flashes,true);
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

            // A bright bar crosses the plate on the turn-start announcement, and very softly now and then while Gild waits.
            var shimmerCycle=Mathf.Repeat(now,6.5f);
            var sweep=announcing?announce:(ready&&motion&&shimmerCycle<.9f?shimmerCycle/.9f:-1f);
            if(sweep>=0)
            {
                GUI.BeginGroup(plate);
                var x=Mathf.Lerp(-26f,plate.width+8f,sweep*sweep*(3f-2f*sweep));
                Fill(new Rect(x,2,16,plate.height-4),new Color(1f,.95f,.75f,(announcing?.30f:.14f)*Mathf.Sin(sweep*Mathf.PI)));
                GUI.EndGroup();
            }
            DrawGildPlateText(plate,armed,ready,used,poor,cost);
        }

        // The forged nameplate: shadow, gold rim, dark body, inner line and a polished top lip, the same build as End Turn.
        private void DrawGildPlate(Rect plate,Color rim,Color body,float breathe)
        {
            if(!CardVfxRepaint)return;
            var lit=Color.Lerp(rim,new Color(1f,.86f,.48f),breathe*.35f);
            DrawCardUiShape(new Rect(plate.x+2,plate.y+4,plate.width,plate.height),gildPlateTexture,new Color(0,0,0,.55f));
            DrawCardUiShape(plate,gildPlateTexture,lit);
            DrawCardUiShape(new Rect(plate.x+3,plate.y+3,plate.width-6,plate.height-6),gildPlateTexture,new Color(body.r,body.g,body.b,1f));
            var inner=new Rect(plate.x+7,plate.y+7,plate.width-14,plate.height-14);
            DrawCardUiShape(inner,gildPlateTexture,new Color(lit.r,lit.g,lit.b,.35f));
            DrawCardUiShape(new Rect(inner.x+1.5f,inner.y+1.5f,inner.width-3,inner.height-3),gildPlateTexture,new Color(body.r*.78f,body.g*.78f,body.b*.78f,1f));
            Fill(new Rect(plate.x+plate.width*.14f,plate.y+4,plate.width*.72f,1.5f),new Color(1f,.9f,.62f,.45f));
        }

        private float GildTextWidth(string text,int size,Font font)=>new GUIStyle(footerStyle){font=font,fontSize=size,fontStyle=FontStyle.Bold,wordWrap=false}.CalcSize(new GUIContent(text)).x;

        // Two lines on the plate: the name, then the price and key, or whatever state Gild is in.
        private void DrawGildPlateText(Rect plate,bool armed,bool ready,bool used,bool poor,int cost)
        {
            var font=labelFont?labelFont:bodyFont;var titleRect=new Rect(plate.x,plate.y+7,plate.width,22);var line=new Rect(plate.x,plate.y+29,plate.width,20);
            var rim=gildRimLook;
            if(armed)
            {
                GildRibbonText(titleRect,"GILDED",18,new Color(1f,.93f,.66f),TextAnchor.MiddleCenter,font);
                GildRibbonText(line,"NEXT CARD ×2",11,new Color(1f,.84f,.52f),TextAnchor.MiddleCenter,font);
                return;
            }
            var titleColor=ready?new Color(1f,.90f,.62f):used?new Color(.82f,.64f,.48f):poor?new Color(.92f,.74f,.62f):new Color(.62f,.60f,.56f);
            GildRibbonText(titleRect,"GILD",18,titleColor,TextAnchor.MiddleCenter,font);
            if(used){GildRibbonText(line,"NEXT TURN",11,new Color(.82f,.64f,.48f),TextAnchor.MiddleCenter,font);return;}
            // Price with the coin glyph; the key cap follows when Gild can be pressed.
            var priceColor=ready?new Color(1f,.92f,.66f):poor?new Color(1f,.46f,.36f):new Color(.62f,.60f,.56f);
            var costText=cost.ToString();var costWidth=GildTextWidth(costText,13,font);var needWidth=poor?GildTextWidth("NEED",11,font)+4:0;
            var showKey=ready;var keyWidth=showKey?20f:0f;var total=needWidth+13f+3f+costWidth+(showKey?8f+keyWidth:0f);
            var x=plate.center.x-total*.5f;
            if(poor){GildRibbonText(new Rect(x,line.y,needWidth,line.height),"NEED",11,new Color(.92f,.70f,.62f),TextAnchor.MiddleLeft,font);x+=needWidth;}
            DrawGildCoin(new Vector2(x+6.5f,line.center.y),13,1f,false,ready?Color.white:poor?new Color(.85f,.72f,.60f):new Color(.60f,.58f,.54f,.85f));x+=16f;
            GildRibbonText(new Rect(x,line.y-1,costWidth+4,line.height+2),costText,13,priceColor,TextAnchor.MiddleLeft,font);x+=costWidth;
            if(!showKey)return;
            var key=new Rect(x+8,line.center.y-8,keyWidth,16);
            Fill(key,new Color(0,0,0,.45f));Outline(key,new Color(rim.r,rim.g,rim.b,.8f),1);
            GildRibbonText(key,GildKeyLabel,GildKeyLabel.Length>1?9:11,Color.white,TextAnchor.MiddleCenter,font);
        }
    }
}
