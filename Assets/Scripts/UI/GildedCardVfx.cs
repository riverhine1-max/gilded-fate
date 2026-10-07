using System.Collections.Generic;
using GildedFate.Combat;
using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    // Presentation-only card and combat VFX. Everything here is a pure function of
    // unscaled time plus a few self-expiring bursts; gameplay sequencing, card
    // motions and their durations are never changed. Procedural textures are built
    // once (HideAndDontSave). Reduce Motion / Reduced VFX / Reduce Flashing are
    // honoured by every effect.
    public sealed partial class GildedMainMenu
    {
        private enum CardVfxKind { Slash, Ward, Sigil, PilePulse, Fracture, HandSweep }
        private sealed class CardVfxBurst
        {
            public CardVfxKind kind;
            public Vector2 position;
            public float start,duration,strength=1,angle;
            public int seed;
            public Color color;
        }
        private readonly List<CardVfxBurst> cardVfxBursts=new();
        private readonly HashSet<CardMotion> cardVfxSeenMotions=new();
        private readonly HashSet<CombatNumber> cardVfxSeenNumbers=new();
        private readonly Dictionary<int,bool> cardVfxConditionReady=new();
        private readonly Dictionary<int,float> cardVfxConditionBurstAt=new();
        private CombatState cardVfxCombat;
        private CombatPhase cardVfxPhase;
        private bool cardVfxShardFractured;
        private float cardVfxHitStopUntil=-1,cardVfxHitStopShake,cardVfxHitStopFoe,cardVfxPunchStart=-10,cardVfxPunchStrength;
        private Vector2 cardVfxPunchCenter;
        private Texture2D cardVfxGlow,cardVfxRing,cardVfxEdge,cardVfxSweep,cardVfxFlame,cardVfxLock,cardVfxPip;
        private Texture2D[] cardVfxBurn;
        private const int CardVfxBurnFrames=8;

        private bool CardVfxMotionAllowed=>profile!=null&&!profile.reduceMotion;
        private bool CardVfxParticles=>profile!=null&&!profile.reduceMotion&&!profile.reducedVfx;
        private static bool CardVfxRepaint=>Event.current!=null&&Event.current.type==EventType.Repaint;

        // ---------- deterministic noise ----------
        private static float CardVfxHash(int a,int b)
        {
            unchecked
            {
                var h=(uint)(a*73856093)^(uint)(b*19349663)^0x9E3779B9u;
                h^=h>>16;h*=0x7FEB352Du;h^=h>>15;h*=0x846CA68Bu;h^=h>>16;
                return (h&0xFFFFFF)/16777216f;
            }
        }
        private static float CardVfxNoise(float x,float y)
        {
            var ix=Mathf.FloorToInt(x);var iy=Mathf.FloorToInt(y);var fx=x-ix;var fy=y-iy;
            fx=fx*fx*(3-2*fx);fy=fy*fy*(3-2*fy);
            return Mathf.Lerp(Mathf.Lerp(CardVfxHash(ix,iy),CardVfxHash(ix+1,iy),fx),Mathf.Lerp(CardVfxHash(ix,iy+1),CardVfxHash(ix+1,iy+1),fx),fy);
        }
        private static float CardVfxFlicker(float t,int seed)
        {
            var i=Mathf.FloorToInt(t);var f=t-i;f=f*f*(3-2*f);
            return Mathf.Lerp(CardVfxHash(i,seed),CardVfxHash(i+1,seed),f);
        }
        private float CardVfxBreath(CardDef card)=>CardVfxMotionAllowed?(Mathf.Sin(shimmer*1.7f+CardVfxHash(card.instanceId,3)*6.283f)+1)*.5f:.5f;

        // ---------- cached procedural textures ----------
        private static Texture2D CreateCardVfxTexture(string name,int width,int height,System.Func<int,int,Color> pixel)
        {
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false){name=name,hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[width*height];
            for(var y=0;y<height;y++)for(var x=0;x<width;x++)pixels[y*width+x]=pixel(x,y);
            texture.SetPixels(pixels);texture.Apply(false,true);return texture;
        }
        private void EnsureCardVfxTextures()
        {
            if(cardVfxGlow&&cardVfxPip)return;
            cardVfxGlow=CreateCardVfxTexture("Card VFX soft glow",64,64,(x,y)=>
            {
                var d=new Vector2(x-31.5f,y-31.5f).magnitude/32f;
                return new Color(1,1,1,Mathf.Clamp01(Mathf.Exp(-d*d*4.2f)*Mathf.Clamp01((1-d)*4)));
            });
            cardVfxRing=CreateCardVfxTexture("Card VFX thin ring",128,128,(x,y)=>
            {
                var d=new Vector2(x-63.5f,y-63.5f).magnitude/64f;
                var line=Mathf.Exp(-Mathf.Pow((d-.84f)/.028f,2));var halo=Mathf.Exp(-Mathf.Pow((d-.84f)/.10f,2))*.32f;
                return new Color(1,1,1,Mathf.Clamp01((line+halo)*Mathf.Clamp01((1-d)*12)));
            });
            // 1:1 with the 194x264 hand card plus a 12 px margin; the line follows the
            // same 12 px chamfer as DrawReadableCard, so it sits on the outer frame only.
            cardVfxEdge=CreateCardVfxTexture("Card VFX chamfered edge",218,288,(x,y)=>
            {
                var qx=Mathf.Abs(x+.5f-109)-97;var qy=Mathf.Abs(y+.5f-144)-132;
                var box=new Vector2(Mathf.Max(qx,0),Mathf.Max(qy,0)).magnitude+Mathf.Min(Mathf.Max(qx,qy),0);
                var d=Mathf.Max(box,(qx+qy+12)*.70711f);
                var line=Mathf.Exp(-Mathf.Pow((d+1.4f)/1.1f,2))*.95f;var glow=d>-1?Mathf.Exp(-Mathf.Max(d,0)*Mathf.Max(d,0)/30f)*.28f:0;
                return new Color(1,1,1,Mathf.Clamp01(line+glow));
            });
            // Diagonal sheen; slid horizontally through UV offset so it is always clipped
            // to the card rect. Transparent side columns make Clamp sampling safe.
            cardVfxSweep=CreateCardVfxTexture("Card VFX sheen",96,132,(x,y)=>
            {
                var u=(x+.5f)/96f;var v=(y+.5f)/132f;var line=u-.5f-(v-.5f)*.55f;
                var band=Mathf.Exp(-Mathf.Pow(line/.03f,2))+Mathf.Exp(-Mathf.Pow(line/.085f,2))*.4f;
                var vertical=Mathf.SmoothStep(0,1,Mathf.Clamp01(v/.07f))*Mathf.SmoothStep(0,1,Mathf.Clamp01((1-v)/.07f));
                return new Color(1,1,1,Mathf.Clamp01(band*vertical*.9f));
            });
            // Baked crimson-to-gold flame tongue; base at the bottom row.
            cardVfxFlame=CreateCardVfxTexture("Card VFX flame tongue",32,64,(x,y)=>
            {
                var u=(x+.5f)/32f-.5f;var v=(y+.5f)/64f;
                var half=.40f*Mathf.Pow(Mathf.Clamp01(1-v),.85f)*Mathf.Clamp01(v*6+.35f);
                var body=half<=0?0:Mathf.Clamp01(1-Mathf.Abs(u)/half);
                var alpha=Mathf.Pow(body,1.3f)*Mathf.Clamp01(v*8+.2f);var heat=body*(1-v*.7f);
                var color=heat>.62f?Color.Lerp(new Color(1f,.62f,.2f),new Color(1f,.93f,.7f),(heat-.62f)/.38f)
                    :heat>.28f?Color.Lerp(new Color(.86f,.12f,.14f),new Color(1f,.62f,.2f),(heat-.28f)/.34f)
                    :Color.Lerp(new Color(.5f,.03f,.07f),new Color(.86f,.12f,.14f),heat/.28f);
                color.a=Mathf.Clamp01(alpha);return color;
            });
            // Padlock glyph, 2x2 supersampled, baked silver shading.
            cardVfxLock=CreateCardVfxTexture("Card VFX padlock",40,48,(x,y)=>
            {
                float cover=0,light=0;
                for(var sy=0;sy<2;sy++)for(var sx=0;sx<2;sx++)
                {
                    var px=(x+.25f+sx*.5f)/40f;var py=(y+.25f+sy*.5f)/48f;
                    var bx=Mathf.Abs(px-.5f)-.27f;var by=Mathf.Abs(py-.33f)-.18f;
                    var body=new Vector2(Mathf.Max(bx,0),Mathf.Max(by,0)).magnitude+Mathf.Min(Mathf.Max(bx,by),0)-.07f;
                    var shackle=py>=.56f?Mathf.Abs(new Vector2(px-.5f,py-.56f).magnitude-.215f)-.052f:py>=.44f?Mathf.Abs(Mathf.Abs(px-.5f)-.215f)-.052f:1;
                    if(body<0)
                    {
                        var keyhole=new Vector2(px-.5f,py-.37f).magnitude<.055f||Mathf.Abs(px-.5f)<.024f&&py>.19f&&py<.37f;
                        cover+=1;light+=keyhole?.08f:(.55f+.35f*Mathf.Clamp01((py-.08f)/.5f))*(body>-.035f?.62f:1);
                    }
                    else if(shackle<0){cover+=1;light+=.80f;}
                }
                var l=cover>0?light/cover:0;return new Color(l,l,l*1.04f,cover/4f);
            });
            cardVfxPip=CreateCardUiPolygon("Card VFX fate pip",32,32,new[]{new Vector2(.5f,0),new Vector2(1,.5f),new Vector2(.5f,1),new Vector2(0,.5f)},true);
        }
        // Eight dissolve frames: charred interior, violet ember front, scorched rim.
        // Frames are cross-faded, so the burn front advances smoothly.
        private void EnsureCardVfxBurn()
        {
            if(cardVfxBurn!=null&&cardVfxBurn[CardVfxBurnFrames-1])return;
            const int width=64,height=88;var threshold=new float[width*height];var inside=new float[width*height];
            for(var y=0;y<height;y++)for(var x=0;x<width;x++)
            {
                var ex=Mathf.Min(x+.5f,width-.5f-x);var ey=Mathf.Min(y+.5f,height-.5f-y);
                var edge=Mathf.Min(ex/(width*.5f),ey/(height*.5f));
                var noise=CardVfxNoise(x*.13f,y*.13f)*.62f+CardVfxNoise(x*.37f+19,y*.37f+7)*.38f;
                threshold[y*width+x]=edge*.78f+noise*.34f;
                inside[y*width+x]=Mathf.Clamp01((ex+ey-3.96f)*.70711f+.5f); // matches the card chamfer
            }
            cardVfxBurn=new Texture2D[CardVfxBurnFrames];
            for(var f=0;f<CardVfxBurnFrames;f++)
            {
                var front=(f+1)/(float)CardVfxBurnFrames*1.25f;
                cardVfxBurn[f]=CreateCardVfxTexture("Card VFX dissipate "+f,width,height,(x,y)=>
                {
                    var i=y*width+x;var d=front-threshold[i];Color c;
                    if(d>.09f)c=new Color(.035f,.022f,.05f,.94f);
                    else if(d>0){var k=d/.09f;c=k<.45f?Color.Lerp(new Color(1f,.84f,1f,1f),new Color(.66f,.32f,.98f,1f),k/.45f):Color.Lerp(new Color(.66f,.32f,.98f,1f),new Color(.07f,.03f,.10f,.96f),(k-.45f)/.55f);}
                    else if(d>-.07f){var k=1+d/.07f;c=new Color(.22f,.08f,.2f,k*k*.5f);}
                    else c=new Color(0,0,0,0);
                    c.a*=inside[i];return c;
                });
            }
        }

        // ---------- per-frame update (called at the end of UpdateCombatPresentation) ----------
        private void ResetCardVfx()
        {
            cardVfxCombat=combat;cardVfxBursts.Clear();cardVfxSeenMotions.Clear();cardVfxSeenNumbers.Clear();
            cardVfxConditionReady.Clear();cardVfxConditionBurstAt.Clear();
            cardVfxPhase=combat?.phase??CombatPhase.Player;cardVfxHitStopUntil=-1;cardVfxPunchStart=-10;
            cardVfxShardFractured=CardVfxActiveFracture(out _);
        }
        private bool CardVfxActiveFracture(out int slot)
        {
            slot=0;if(combat==null||string.IsNullOrEmpty(combat.activeShardId)||run?.shards==null)return false;
            foreach(var shard in run.shards)if(shard.active&&shard.activeFractured&&shard.id==combat.activeShardId){slot=shard.slot;return true;}
            return false;
        }
        private void QueueCardVfx(CardVfxKind kind,Vector2 position,float start,float duration,Color color,int seed,float strength=1,float angle=0)
        {
            if(cardVfxBursts.Count>=64)cardVfxBursts.RemoveAt(0);
            cardVfxBursts.Add(new CardVfxBurst{kind=kind,position=position,start=start,duration=Mathf.Max(.05f,duration),color=color,seed=seed,strength=strength,angle=angle});
        }
        private void UpdateCardVfx(float dt,float now)
        {
            if(combat==null)return;
            if(cardVfxCombat!=combat)ResetCardVfx();
            // New flights: Dissipate arrivals pulse their pile; plays schedule impact art.
            foreach(var motion in cardMotions)
            {
                if(motion.card==null||motion.back||cardVfxSeenMotions.Contains(motion))continue;
                cardVfxSeenMotions.Add(motion);
                if(motion.exhaust)QueueCardVfx(CardVfxKind.PilePulse,motion.to,motion.start+motion.duration,.46f,new Color(.72f,.46f,1f),motion.card.instanceId);
                else if(!motion.absorb&&motion.card.instanceId==movingCard&&(motion.from-motion.to).sqrMagnitude>1)QueuePlayImpactVfx(motion);
            }
            if(cardMotions.Count==0)cardVfxSeenMotions.Clear();else if(cardVfxSeenMotions.Count>cardMotions.Count)cardVfxSeenMotions.RemoveWhere(m=>!cardMotions.Contains(m));
            // Big hits: ~70 ms presentation hold (shake/hit decay frozen) plus a zoom punch.
            foreach(var number in combatNumbers)
            {
                if(number.start>now||cardVfxSeenNumbers.Contains(number))continue;
                cardVfxSeenNumbers.Add(number);
                var amount=CardVfxCritAmount(number);if(amount<50||now-number.start>.12f)continue;
                // Hold scales with the hit: 70 ms at 50+, 95 ms at 100+, 115 ms at 150+.
                cardVfxHitStopUntil=number.start+(amount>=150?.115f:amount>=100?.095f:.07f);cardVfxHitStopShake=impactShake;cardVfxHitStopFoe=foeHit;
                cardVfxPunchStart=number.start;cardVfxPunchStrength=amount>=150?1:amount>=100?.8f:.55f;cardVfxPunchCenter=new Vector2(number.origin.x,CombatHeight*.36f);
            }
            if(combatNumbers.Count==0)cardVfxSeenNumbers.Clear();else if(cardVfxSeenNumbers.Count>combatNumbers.Count)cardVfxSeenNumbers.RemoveWhere(n=>!combatNumbers.Contains(n));
            if(now<cardVfxHitStopUntil){impactShake=Mathf.Max(impactShake,cardVfxHitStopShake);foeHit=Mathf.Max(foeHit,cardVfxHitStopFoe);}
            // Condition (Heavy/Revenge-style) readiness transitions fire a one-shot burst.
            foreach(var view in handViews.Values)
            {
                if(view?.card==null||now<view.readyAt)continue;
                var ready=combat.CanPlay(view.card)&&CardPreview(view.card).conditionActive;var id=view.card.instanceId;
                if(ready&&cardVfxConditionReady.TryGetValue(id,out var was)&&!was)cardVfxConditionBurstAt[id]=now;
                cardVfxConditionReady[id]=ready;
            }
            // End turn: golden sweep across the discarding hand.
            var phase=combat.phase;
            if(cardVfxPhase==CombatPhase.Player&&phase!=CombatPhase.Player&&!combat.IsOver&&CardVfxMotionAllowed)
                QueueCardVfx(CardVfxKind.HandSweep,new Vector2(CombatWidth*.5f,CombatHeight-150),now,AnimationSeconds(.6f),Gold,combat.turn);
            cardVfxPhase=phase;
            // Fractured (third) shard activation: crystal burst from its socket.
            var fractured=CardVfxActiveFracture(out var slot);
            if(fractured&&!cardVfxShardFractured)QueueCardVfx(CardVfxKind.Fracture,ShrineSocket(Mathf.Clamp(slot,0,1)).center,now,.85f,new Color(1f,.58f,.24f),slot*31+combat.turn);
            cardVfxShardFractured=fractured;
            for(var i=cardVfxBursts.Count-1;i>=0;i--)if(now>cardVfxBursts[i].start+cardVfxBursts[i].duration)cardVfxBursts.RemoveAt(i);
        }
        private bool CardVfxIsStrike(CardDef card)=>card!=null&&(card.kind==CardKind.Attack||card.effect==EffectKind.Damage&&combat.RequiresEnemyTarget(card));
        private void QueuePlayImpactVfx(CardMotion motion)
        {
            var card=motion.card;var at=motion.start+motion.duration;var seed=card.instanceId;var strength=card.rarity==Rarity.Rare?1.15f:1f;
            if(card.kind==CardKind.Power){QueueCardVfx(CardVfxKind.Sigil,motion.to,at,AnimationSeconds(.72f),Gold,seed,strength);return;}
            if(CardVfxIsStrike(card))
            {
                var hits=Mathf.Clamp(card.hits,1,3);
                // The swing trail carries the hero's colour; the hit itself is drawn by GildedCombatFinalVfx.
                var hero=card.hero??run.hero;var tint=hero==HeroId.Hexer?new Color(.76f,.50f,1f):hero==HeroId.Reaper?new Color(.30f,.92f,.82f):new Color(1f,.78f,.40f);
                void Slash(Vector2 point,int index){for(var h=0;h<hits;h++)QueueCardVfx(CardVfxKind.Slash,point,at+h*.07f,.32f,tint,seed+h*101+index*7,strength,h%2==0?35:-35);}
                if(combat.RequiresEnemyTarget(card))Slash(motion.to,0);
                else if(GroupCombat){for(var i=0;i<combat.EnemyCount;i++)if(combat.IsLivingTarget(i))Slash(GroupPortrait(i).center,i);}
                else Slash(EnemyPortraitRect.center,0);
                return;
            }
            QueueCardVfx(CardVfxKind.Ward,combat.RequiresEnemyTarget(card)?motion.to:HeroPortraitRect.center,at,AnimationSeconds(.5f),new Color(.48f,.80f,1f),seed,strength);
        }
        private static int CardVfxCritAmount(CombatNumber number)
        {
            // Enemy-side damage numbers are "−N" in Gold; player damage is red.
            var text=number?.text;
            if(string.IsNullOrEmpty(text)||text.Length<2||text[0]!='−'||number.color.g<.7f||number.color.b>.6f)return 0;
            return int.TryParse(text.Substring(1),out var amount)?amount:0;
        }

        // ---------- hand cards ----------
        // Around DrawMovingCard in DrawHandView: landing settle bounce (matrix only;
        // hit testing keeps using the stable slots).
        private Matrix4x4 BeginHandCardVfx(HandView view)
        {
            var matrix=GUI.matrix;
            if(view==null||!CardVfxMotionAllowed||view.readyAt<=0)return matrix;
            var age=Time.unscaledTime-view.readyAt;if(age<0||age>.3f)return matrix;
            var wave=Mathf.Sin(age*22)*Mathf.Exp(-age*12);var pivot=new Vector3(view.position.x,view.position.y,0);
            GUI.matrix=matrix*Matrix4x4.TRS(pivot+new Vector3(0,wave*9,0),Quaternion.identity,new Vector3(1+wave*.025f,1-wave*.02f,1))*Matrix4x4.Translate(-pivot);
            return matrix;
        }
        private void EndHandCardVfx(HandView view,bool focus,Matrix4x4 restore)
        {
            if(view?.card!=null&&combat!=null&&CardVfxRepaint)
            {
                var matrix=GUI.matrix;var old=GUI.color;
                GUI.matrix=matrix*Matrix4x4.TRS(new Vector3(view.position.x,view.position.y,0),Quaternion.Euler(0,0,view.angle),new Vector3(view.scale,view.scale,1));
                try{DrawHandCardOverlay(new Rect(-HandLayout.CardWidth*.5f,-HandLayout.CardHeight*.5f,HandLayout.CardWidth,HandLayout.CardHeight),view,focus);}
                finally{GUI.color=old;}
            }
            GUI.matrix=restore;
        }
        private void DrawHandCardOverlay(Rect r,HandView view,bool focus)
        {
            EnsureCardVfxTextures();
            var card=view.card;var motion=CardVfxMotionAllowed;var seed=card.instanceId;
            var playable=combat.CanPlay(card);var ready=playable&&CardPreview(card).conditionActive;
            var edgeRect=new Rect(r.x-12,r.y-12,r.width+24,r.height+24);
            if(playable)
            {
                Color edge;
                if(ready){var flick=motion?CardVfxFlicker(shimmer*9,seed):.5f;edge=Color.Lerp(new Color(.92f,.16f,.14f),new Color(1f,.74f,.3f),flick*.55f);edge.a=(focus?.9f:.66f)+flick*.1f;}
                else{var breath=CardVfxBreath(card);edge=new Color(1f,.83f,.43f,(focus?.62f:.34f)+breath*(focus?.3f:.24f));}
                DrawCardUiShape(edgeRect,cardVfxEdge,edge);
            }
            var perfected=card.perfected||card.specialModification is "perfected" or "perfected_edge" or "perfected_guard";
            if(motion&&!profile.reducedVfx)
            {
                if(perfected)DrawCardVfxSheen(r,seed,4.6f,true,focus);
                else if(playable)DrawCardVfxSheen(r,seed,6.2f,false,focus);
            }
            if(card.unplayable)DrawCardVfxLock(r);
            if(card.specialModification=="fateful")DrawCardVfxFatefulPips(r,card);
            if(card.IsModified&&card.specialModification is not ("golden_echo" or "perfected_edge" or "perfected_guard"))DrawCardVfxCornerGlint(r,seed);
            if(motion)
            {
                var count=profile.reducedVfx?4:10;
                if(view.readinessPulse>0)DrawCardVfxSparks(r,1-view.readinessPulse/.32f,seed,count,ready?new Color(1f,.42f,.28f):new Color(1f,.86f,.5f));
                if(cardVfxConditionBurstAt.TryGetValue(seed,out var at)){var k=(Time.unscaledTime-at)/.42f;if(k>=0&&k<1)DrawCardVfxSparks(r,k,seed+977,count+2,new Color(1f,.36f,.22f));}
            }
        }
        // Underlay, drawn behind the card from DrawIdentityCardLight (hand only).
        private void DrawCardVfxUnderlay(Rect r,CardDef card)
        {
            if(card.specialModification!="golden_echo"||combat.memory.specialPlayIds?.Contains(card.instanceId)==true||!CardVfxRepaint)return;
            EnsureCardVfxTextures();
            // Golden Echo (unused this combat): a faint offset copy waiting behind the card.
            var drift=CardVfxMotionAllowed?Mathf.Sin(shimmer*1.3f+card.instanceId)*2.5f:0;
            var ghost=new Rect(r.x+5+drift,r.y-10-Mathf.Abs(drift)*.6f,r.width,r.height);
            FillCardSilhouette(ghost,new Color(.55f,.40f,.16f,.16f),Mathf.Clamp(r.width*.062f,7,22));
            DrawCardUiShape(new Rect(ghost.x-12,ghost.y-12,ghost.width+24,ghost.height+24),cardVfxEdge,new Color(1f,.84f,.45f,.55f));
        }
        private void DrawCardVfxFlames(Rect r,CardDef card,bool focused)
        {
            if(!CardVfxRepaint)return;
            EnsureCardVfxTextures();
            var animate=CardVfxMotionAllowed;var reduced=profile.reducedVfx||!animate;
            var top=reduced?3:6;var side=reduced?1:3;var seed=card.instanceId;var matrix=GUI.matrix;var old=GUI.color;
            for(var i=0;i<top+side*2;i++)
            {
                Vector2 anchor;float angle;
                if(i<top){var u=(i+.5f)/top;anchor=new Vector2(Mathf.Lerp(r.x+18,r.xMax-18,u),r.y+4);angle=(u-.5f)*16;}
                else{var j=i-top;var left=j<side;var v=((left?j:j-side)+.5f)/side;anchor=new Vector2(left?r.x+4:r.xMax-4,Mathf.Lerp(r.y+26,r.y+r.height*.46f,v));angle=left?-72:72;}
                var flicker=animate?CardVfxFlicker(shimmer*(6.5f+CardVfxHash(seed,i)*3)+i*3.1f,seed*31+i):.55f;
                var height=(focused?40:32)*(.62f+.5f*flicker);var width=height*.46f;
                GUI.matrix=matrix*Matrix4x4.TRS(new Vector3(anchor.x,anchor.y,0),Quaternion.Euler(0,0,angle+(flicker-.5f)*8),Vector3.one);
                GUI.color=new Color(1,1,1,(focused?.9f:.72f)*(.7f+.3f*flicker));
                GUI.DrawTexture(new Rect(-width*.5f,-height,width,height),cardVfxFlame);
            }
            GUI.matrix=matrix;GUI.color=old;
        }
        private void DrawCardVfxSheen(Rect r,int seed,float period,bool prismatic,bool focus)
        {
            var phase=Mathf.Repeat(shimmer/period+CardVfxHash(seed,11),1f);var window=prismatic?.14f:.11f;
            if(phase>window)return;
            var k=phase/window;var offset=Mathf.Lerp(1.1f,-1.1f,k);
            var strength=(focus?.22f:.15f)*(profile.reduceFlashing?.6f:1f)*Mathf.Sin(k*Mathf.PI);
            if(!prismatic){DrawCardVfxBand(r,offset,new Color(1f,.93f,.72f,strength));return;}
            var hue=Mathf.Repeat(shimmer*.11f+CardVfxHash(seed,5),1f);
            DrawCardVfxBand(r,offset,CardVfxHue(hue,strength*.8f));
            DrawCardVfxBand(r,offset+.045f,CardVfxHue(hue+.33f,strength*.8f));
            DrawCardVfxBand(r,offset+.09f,CardVfxHue(hue+.66f,strength*.8f));
        }
        private void DrawCardVfxBand(Rect r,float offset,Color color)
        {
            var old=GUI.color;GUI.color=color;GUI.DrawTextureWithTexCoords(r,cardVfxSweep,new Rect(offset,0,1,1),true);GUI.color=old;
        }
        private static Color CardVfxHue(float hue,float alpha)
        {
            hue=Mathf.Repeat(hue,1f)*6;
            var r=Mathf.Clamp01(Mathf.Abs(hue-3)-1);var g=Mathf.Clamp01(2-Mathf.Abs(hue-2));var b=Mathf.Clamp01(2-Mathf.Abs(hue-4));
            return new Color(Mathf.Lerp(r,1,.45f),Mathf.Lerp(g,1,.45f),Mathf.Lerp(b,1,.45f),alpha);
        }
        // Unplayable: padlock seal in the (empty) Energy-cost corner.
        private void DrawCardVfxLock(Rect r)
        {
            var size=r.width*.17f;var c=new Vector2(r.x+r.width*.018f+size*.5f,r.y+r.height*.026f+size*.5f);
            DrawCardUiShape(new Rect(c.x-size*.62f,c.y-size*.62f,size*1.24f,size*1.24f),cardVfxGlow,new Color(0,0,0,.72f));
            DrawCardUiShape(new Rect(c.x-size*.56f,c.y-size*.56f,size*1.12f,size*1.12f),cardVfxRing,new Color(.62f,.60f,.68f,.85f));
            var h=size*.78f;var w=h*40f/48f;var lr=new Rect(c.x-w*.5f,c.y-h*.52f,w,h);
            DrawCardUiShape(new Rect(lr.x+1.2f,lr.y+1.5f,lr.width,lr.height),cardVfxLock,new Color(0,0,0,.8f));
            DrawCardUiShape(lr,cardVfxLock,new Color(.88f,.86f,.93f,1f));
        }
        // Fateful: three pips above the title; every third play is the extra play.
        private void DrawCardVfxFatefulPips(Rect r,CardDef card)
        {
            var memory=combat.memory;var index=memory.fatefulIds?.IndexOf(card.instanceId)??-1;
            var plays=index>=0&&memory.fatefulCounts!=null&&index<memory.fatefulCounts.Count?memory.fatefulCounts[index]:0;
            var filled=plays%3;const float size=12f;
            for(var i=0;i<3;i++)
            {
                var c=new Vector2(r.center.x+(i-1)*17,r.y-3);var pip=new Rect(c.x-size*.5f,c.y-size*.5f,size,size);
                var lit=i<filled;var armed=i==filled&&filled==2;var pulse=armed&&CardVfxMotionAllowed?(Mathf.Sin(shimmer*4.2f)+1)*.5f:0;
                DrawCardUiShape(new Rect(pip.x-2,pip.y-2,pip.width+4,pip.height+4),cardVfxPip,new Color(.08f,.05f,.02f,.92f));
                if(lit||armed)DrawCardUiShape(new Rect(c.x-size,c.y-size,size*2,size*2),cardVfxGlow,new Color(1f,.78f,.35f,lit?.45f:.18f+pulse*.25f));
                DrawCardUiShape(pip,cardVfxPip,lit?new Color(1f,.84f,.45f):new Color(.42f,.33f,.18f,1f));
                if(!lit)DrawCardUiShape(InsetCardRect(pip,3),cardVfxPip,new Color(.10f,.07f,.04f,1f));
            }
        }
        // Bindings / Fateweaves: engraved gilt corner cap with an occasional travelling glint.
        private void DrawCardVfxCornerGlint(Rect r,int seed)
        {
            var a=new Vector2(r.x-1.5f,r.y+30);var b=new Vector2(r.x-1.5f,r.y+11.5f);var c=new Vector2(r.x+11.5f,r.y-1.5f);var d=new Vector2(r.x+30,r.y-1.5f);
            var groove=new Color(.10f,.07f,.03f,.85f);var gold=new Color(.96f,.78f,.40f,.95f);
            DrawLine(a,b,groove,3.2f);DrawLine(b,c,groove,3.2f);DrawLine(c,d,groove,3.2f);
            DrawLine(a,b,gold,1.3f);DrawLine(b,c,gold,1.3f);DrawLine(c,d,gold,1.3f);
            if(!CardVfxMotionAllowed||profile.reducedVfx)return;
            var phase=Mathf.Repeat(shimmer/4.4f+CardVfxHash(seed,17),1f);if(phase>.16f)return;
            var k=phase/.16f;var along=k*3;var point=along<1?Vector2.Lerp(a,b,along):along<2?Vector2.Lerp(b,c,along-1):Vector2.Lerp(c,d,along-2);
            var glint=Mathf.Sin(k*Mathf.PI);var size=5+glint*6;var color=new Color(1f,.95f,.78f,glint*(profile.reduceFlashing?.55f:.95f));
            DrawCardUiShape(new Rect(point.x-size*1.6f,point.y-size*1.6f,size*3.2f,size*3.2f),cardVfxGlow,new Color(1f,.85f,.5f,glint*.5f));
            DrawLine(point-new Vector2(size,0),point+new Vector2(size,0),color,1.2f);DrawLine(point-new Vector2(0,size),point+new Vector2(0,size),color,1.2f);
        }
        private void DrawCardVfxSparks(Rect r,float k,int seed,int count,Color color)
        {
            var fade=1-k;var ease=1-fade*fade;
            if(!profile.reduceFlashing&&k<.35f)DrawCardUiShape(new Rect(r.x-12,r.y-12,r.width+24,r.height+24),cardVfxEdge,new Color(1f,.95f,.8f,(1-k/.35f)*.55f));
            for(var i=0;i<count;i++)
            {
                // Sparks leave the upper silhouette, which the fanned hand keeps visible.
                var u=CardVfxHash(seed,i*7+1);var side=CardVfxHash(seed,i*7+2);Vector2 origin,dir;
                if(side<.6f){origin=new Vector2(Mathf.Lerp(r.x+10,r.xMax-10,u),r.y);dir=new Vector2((u-.5f)*.9f,-1);}
                else if(side<.8f){origin=new Vector2(r.x,Mathf.Lerp(r.y+10,r.center.y,u));dir=new Vector2(-1,-.35f);}
                else{origin=new Vector2(r.xMax,Mathf.Lerp(r.y+10,r.center.y,u));dir=new Vector2(1,-.35f);}
                dir.Normalize();var travel=(14+CardVfxHash(seed,i*7+3)*22)*ease;
                var c=Color.Lerp(color,Color.white,.35f*fade);c.a=fade*.95f;
                DrawLine(origin+dir*Mathf.Max(0,travel-7-8*fade),origin+dir*Mathf.Max(.5f,travel),c,1.6f);
            }
        }

        // ---------- card motions (hook inside DrawCombatMotions) ----------
        private Vector2 CardVfxMotionPoint(CardMotion m,Vector2 destination,float t)
        {
            t=Mathf.Clamp01(t);var eased=CardMotionEase(m,t);var p=Vector2.Lerp(m.from,destination,eased);
            if(CardVfxMotionAllowed)p.y-=CardMotionLift(m,t,eased);return p;
        }
        private bool CardVfxIsPileDraw(CardMotion m)=>!m.back&&!m.absorb&&!m.exhaust&&m.fromScale<.3f&&m.toScale>.99f&&(m.from-PilePoint(0)).sqrMagnitude<4&&combat.hand.Contains(m.card);
        // Returns true when the motion was fully drawn here (the caller then skips it).
        private bool DrawCardMotionVfx(CardMotion motion,float t,float eased,Vector2 p)
        {
            if(motion?.card==null||combat==null)return false;
            var repaint=CardVfxRepaint;
            if(motion.exhaust){if(repaint)DrawCardVfxDissipate(motion,t);return true;}
            if(!CardVfxMotionAllowed)return false;
            if(motion.absorb){if(repaint)DrawCardVfxAbsorbTrail(motion,t);return false;}
            if(CardVfxIsPileDraw(motion)){if(repaint)DrawCardVfxDrawFlip(motion,t,eased,p);return true;}
            if(motion.card.instanceId==movingCard&&(motion.from-motion.to).sqrMagnitude>1){if(repaint)DrawCardVfxPlayTrail(motion,t);return false;}
            return false;
        }
        // Draw pile -> hand: face-down, flip at mid-arc, short gilded wake.
        private void DrawCardVfxDrawFlip(CardMotion motion,float t,float eased,Vector2 p)
        {
            EnsureCardVfxTextures();
            var scale=Mathf.Lerp(motion.fromScale,motion.toScale,eased);var trail=profile.reducedVfx?2:5;
            for(var i=trail;i>=1;i--)
            {
                var at=t-i*.05f;if(at<0)continue;var q=CardVfxMotionPoint(motion,motion.to,at);
                var fade=(1-i/(float)(trail+1))*Mathf.Clamp01(t*4)*Mathf.Clamp01((1-t)*5);var size=8+20*fade*scale;
                DrawCardUiShape(new Rect(q.x-size,q.y-size,size*2,size*2),cardVfxGlow,new Color(1f,.80f,.38f,.42f*fade));
            }
            var flip=Mathf.Clamp01((t-.2f)/.34f);var squeeze=Mathf.Max(.04f,Mathf.Abs(Mathf.Cos(flip*Mathf.PI)));
            var matrix=GUI.matrix;
            GUI.matrix=matrix*Matrix4x4.TRS(new Vector3(p.x,p.y,0),Quaternion.identity,new Vector3(squeeze,1,1))*Matrix4x4.Translate(new Vector3(-p.x,-p.y,0));
            try{DrawMovingCard(motion.card,p,Mathf.Lerp(motion.fromAngle,motion.toAngle,eased),scale,false,1,flip<.5f);}
            finally{GUI.matrix=matrix;}
            var edge=1-squeeze;
            if(edge>.25f){var half=HandLayout.CardHeight*scale*.5f;DrawLine(p+new Vector2(0,-half),p+new Vector2(0,half),new Color(1f,.88f,.55f,edge*(profile.reduceFlashing?.35f:.75f)),2);}
        }
        private void DrawCardVfxPlayTrail(CardMotion motion,float t)
        {
            EnsureCardVfxTextures();
            var card=motion.card;var strike=CardVfxIsStrike(card);var power=card.kind==CardKind.Power;
            var segments=profile.reducedVfx?3:6;var previous=CardVfxMotionPoint(motion,motion.to,t);
            for(var i=1;i<=segments;i++)
            {
                var at=t-i*.075f;if(at<0)break;var next=CardVfxMotionPoint(motion,motion.to,at);var fade=1-(i-1)/(float)segments;
                if(strike){DrawLine(previous,next,new Color(.78f,.12f,.06f,.30f*fade),16*fade+2);DrawLine(previous,next,new Color(1f,.72f,.30f,.80f*fade),5*fade+1);}
                else{var size=8+16*fade;DrawCardUiShape(new Rect(next.x-size,next.y-size,size*2,size*2),cardVfxGlow,power?new Color(1f,.80f,.36f,.5f*fade):new Color(.50f,.80f,1f,.5f*fade));}
                previous=next;
            }
        }
        private void DrawCardVfxAbsorbTrail(CardMotion motion,float t)
        {
            if(t<.12f)return;EnsureCardVfxTextures();
            var destination=PowerHudTarget(motion.card).center;var count=profile.reducedVfx?2:5;
            for(var i=1;i<=count;i++)
            {
                var at=t-i*.06f;if(at<.1f)break;var q=CardVfxMotionPoint(motion,destination,at);var fade=1-i/(float)(count+1);var size=5+11*fade;
                DrawCardUiShape(new Rect(q.x-size,q.y-size,size*2,size*2),cardVfxGlow,new Color(1f,.82f,.40f,.55f*fade));
            }
        }
        // Dissipate: shrink/tilt while violet embers eat inward, crumble to ash, then a
        // wisp carries the essence to the Dissipate pile (which pulses on arrival).
        private void DrawCardVfxDissipate(CardMotion motion,float t)
        {
            EnsureCardVfxTextures();EnsureCardVfxBurn();
            var card=motion.card;var seed=card.instanceId;var moving=CardVfxMotionAllowed;
            var burn=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,.60f,t));var crumble=Mathf.InverseLerp(.60f,.76f,t);
            var tilt=CardVfxHash(seed,41)<.5f?-1f:1f;
            var center=motion.from+(moving?new Vector2(tilt*4*burn,-16*burn):Vector2.zero);
            var scale=motion.fromScale*(moving?1-.12f*burn:1);var angle=motion.fromAngle+(moving?tilt*7*burn:0);
            if(crumble<1)
            {
                // The card body is opaque; once fully charred it is no longer drawn and
                // only the fading char remains.
                if(crumble<=0)DrawMovingCard(card,center,angle,scale,false,1);
                var matrix=GUI.matrix;
                GUI.matrix=matrix*Matrix4x4.TRS(new Vector3(center.x,center.y,0),Quaternion.Euler(0,0,angle),new Vector3(scale,scale,1));
                DrawCardVfxBurnMask(new Rect(-HandLayout.CardWidth*.5f,-HandLayout.CardHeight*.5f,HandLayout.CardWidth,HandLayout.CardHeight),burn,1-crumble,seed);
                GUI.matrix=matrix;
            }
            if(CardVfxParticles)DrawCardVfxEmbers(center,angle,scale,t,burn,seed);
            var wisp=Mathf.InverseLerp(.56f,1f,t);
            if(wisp>0&&moving)DrawCardVfxWisp(center,motion.to,wisp,seed);
        }
        private void DrawCardVfxBurnMask(Rect r,float burn,float alpha,int seed)
        {
            if(burn<=0||alpha<=0)return;
            var frames=burn*CardVfxBurnFrames;var index=Mathf.Min(CardVfxBurnFrames-1,Mathf.FloorToInt(frames));var blend=Mathf.Clamp01(frames-index);
            var flipX=CardVfxHash(seed,51)<.5f;var flipY=CardVfxHash(seed,52)<.5f;
            var uv=new Rect(flipX?1:0,flipY?1:0,flipX?-1:1,flipY?-1:1);var old=GUI.color;
            if(index>0){GUI.color=new Color(1,1,1,alpha);GUI.DrawTextureWithTexCoords(r,cardVfxBurn[index-1],uv,true);}
            GUI.color=new Color(1,1,1,alpha*blend);GUI.DrawTextureWithTexCoords(r,cardVfxBurn[index],uv,true);
            GUI.color=old;
        }
        private void DrawCardVfxEmbers(Vector2 center,float angle,float scale,float t,float burn,int seed)
        {
            var rad=angle*Mathf.Deg2Rad;var cos=Mathf.Cos(rad);var sin=Mathf.Sin(rad);
            Vector2 World(Vector2 local)=>center+new Vector2(local.x*cos-local.y*sin,local.x*sin+local.y*cos)*scale;
            var hx=HandLayout.CardWidth*.5f;var hy=HandLayout.CardHeight*.5f;const int embers=14,ash=8;
            for(var i=0;i<embers;i++)
            {
                var born=.08f+i/(float)embers*.5f;var age=(t-born)/.30f;if(age<0||age>1)continue;
                var inset=1-Mathf.Clamp01(Mathf.InverseLerp(.08f,.60f,born))*.82f;var u=CardVfxHash(seed,i*13+3)*4;
                var local=u<1?new Vector2(Mathf.Lerp(-hx,hx,u),-hy):u<2?new Vector2(hx,Mathf.Lerp(-hy,hy,u-1)):u<3?new Vector2(Mathf.Lerp(hx,-hx,u-2),hy):new Vector2(-hx,Mathf.Lerp(hy,-hy,u-3));
                var point=World(local*inset)+new Vector2(Mathf.Sin(age*5+i)*6,-age*(46+CardVfxHash(seed,i*13+5)*30));
                var hot=CardVfxHash(seed,i*13+7);var size=hot>.7f?3f:2f;
                var color=hot>.7f?new Color(1f,.86f,1f):hot>.35f?new Color(.78f,.46f,1f):new Color(1f,.58f,.30f);color.a=Mathf.Sin(age*Mathf.PI)*.9f;
                Fill(new Rect(point.x-size*.5f,point.y-size*.5f,size,size),color);
            }
            for(var i=0;i<ash;i++)
            {
                var born=.26f+i/(float)ash*.45f;var age=(t-born)/.34f;if(age<0||age>1)continue;
                var local=new Vector2((CardVfxHash(seed,i*17+1)-.5f)*hx*1.6f,(CardVfxHash(seed,i*17+2)-.5f)*hy*1.6f)*(1-burn*.4f);
                var point=World(local)+new Vector2((CardVfxHash(seed,i*17+3)-.5f)*36*age,-age*(34+CardVfxHash(seed,i*17+4)*34));
                var spin=age*(CardVfxHash(seed,i*17+5)*9-4.5f)+i;var len=3.5f+CardVfxHash(seed,i*17+6)*3.5f;
                var axis=new Vector2(Mathf.Cos(spin),Mathf.Sin(spin))*len*.5f;var grey=CardVfxHash(seed,i*17+7)*.25f+.10f;
                DrawLine(point-axis,point+axis,new Color(grey,grey*.95f,grey*1.08f,Mathf.Sin(age*Mathf.PI)*.8f),2.4f);
            }
        }
        private void DrawCardVfxWisp(Vector2 from,Vector2 to,float k,int seed)
        {
            var control=Vector2.Lerp(from,to,.45f)+new Vector2(0,-110-CardVfxHash(seed,61)*50);
            Vector2 At(float s){s=Mathf.Clamp01(s);s=s*s*(3-2*s);return Vector2.Lerp(Vector2.Lerp(from,control,s),Vector2.Lerp(control,to,s),s);}
            var trail=profile.reducedVfx?2:6;var fadeIn=Mathf.Clamp01(k*6);
            for(var i=trail;i>=1;i--)
            {
                var at=k-i*.045f;if(at<0)continue;var q=At(at);var fade=(1-i/(float)(trail+1))*fadeIn;var size=6+12*fade;
                DrawCardUiShape(new Rect(q.x-size,q.y-size,size*2,size*2),cardVfxGlow,new Color(.62f,.38f,.96f,.42f*fade));
            }
            var head=At(k);
            DrawCardUiShape(new Rect(head.x-18,head.y-18,36,36),cardVfxGlow,new Color(.74f,.48f,1f,.85f*fadeIn));
            DrawCardUiShape(new Rect(head.x-6,head.y-6,12,12),cardVfxGlow,new Color(1f,.92f,1f,(profile.reduceFlashing?.5f:.9f)*fadeIn));
        }

        // ---------- combat layer (end of DrawCombatMotions) ----------
        private void DrawCardVfxLayer()
        {
            if(!CardVfxRepaint||cardVfxBursts.Count==0)return;
            EnsureCardVfxTextures();var now=Time.unscaledTime;var old=GUI.color;
            foreach(var burst in cardVfxBursts)
            {
                var k=(now-burst.start)/burst.duration;if(k<0||k>=1)continue;
                switch(burst.kind)
                {
                    // Slash and Ward target actors; they draw from DrawCardVfxActorBursts.
                    case CardVfxKind.Sigil:DrawCardVfxSigil(burst,k);break;
                    case CardVfxKind.PilePulse:DrawCardVfxPilePulse(burst,k);break;
                    case CardVfxKind.HandSweep:DrawCardVfxHandSweep(burst,k);break;
                }
            }
            GUI.color=old;
        }
        // Actor-targeted bursts draw in the actor layer (DrawCombatActors) so health,
        // intents and status icons are never covered.
        private void DrawCardVfxActorBursts()
        {
            if(!CardVfxRepaint||cardVfxBursts.Count==0)return;
            EnsureCardVfxTextures();var now=Time.unscaledTime;var old=GUI.color;
            foreach(var burst in cardVfxBursts)
            {
                var k=(now-burst.start)/burst.duration;if(k<0||k>=1)continue;
                if(burst.kind==CardVfxKind.Slash)DrawCardVfxSlash(burst,k);else if(burst.kind==CardVfxKind.Ward)DrawCardVfxWard(burst,k);
            }
            GUI.color=old;
        }
        private void DrawCardVfxSlash(CardVfxBurst b,float k)
        {
            var angle=(b.angle+(CardVfxHash(b.seed,71)-.5f)*20)*Mathf.Deg2Rad;var dir=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
            var length=150*b.strength;var draw=Mathf.Clamp01(k/.28f);var fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,1f,k));
            var start=b.position-dir*length*.5f;var end=Vector2.Lerp(start,b.position+dir*length*.5f,1-(1-draw)*(1-draw));
            var tail=Vector2.Lerp(start,end,.18f);var head=Vector2.Lerp(start,end,.82f);
            void Stroke(float width,Color color){DrawLine(start,tail,color,width*.45f);DrawLine(tail,head,color,width);DrawLine(head,end,color,width*.45f);}
            Stroke(15,new Color(b.color.r*.45f,b.color.g*.2f,b.color.b*.3f,.32f*fade));
            Stroke(6,new Color(b.color.r,b.color.g,b.color.b,.85f*fade));
            if(!profile.reduceFlashing&&!profile.reducedVfx)Stroke(2,new Color(1f,.96f,.84f,.95f*fade));
        }
        private void DrawCardVfxWard(CardVfxBurst b,float k)
        {
            var ease=1-(1-k)*(1-k);var size=(CardVfxMotionAllowed?Mathf.Lerp(70,210,ease):170)*b.strength;var alpha=(1-k)*(profile.reduceFlashing?.45f:.7f);
            DrawCardUiShape(new Rect(b.position.x-size*.5f,b.position.y-size*.5f,size,size),cardVfxRing,new Color(b.color.r,b.color.g,b.color.b,alpha));
            if(!CardVfxParticles)return;
            for(var i=0;i<8;i++)
            {
                var a=i*Mathf.PI*.25f+shimmer*1.6f+b.seed;var q=b.position+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*size*.42f;var s=5+Mathf.Sin(k*Mathf.PI*3+i)*3;
                DrawCardUiShape(new Rect(q.x-s,q.y-s,s*2,s*2),cardVfxGlow,new Color(.80f,.93f,1f,alpha));
            }
        }
        private void DrawCardVfxSigil(CardVfxBurst b,float k)
        {
            var moving=CardVfxMotionAllowed;var ease=Mathf.SmoothStep(0,1,k);var center=b.position+new Vector2(0,moving?-52*ease:0);
            var width=Mathf.Lerp(80,138,ease)*b.strength;var height=width*.42f;var alpha=(k<.2f?k/.2f:1-(k-.2f)/.8f)*.85f;
            DrawCardUiShape(new Rect(center.x-width*.5f,center.y-height*.5f,width,height),cardVfxRing,new Color(1f,.82f,.40f,alpha));
            DrawCardUiShape(new Rect(center.x-width*.36f,center.y-height*.36f,width*.72f,height*.72f),cardVfxRing,new Color(1f,.9f,.6f,alpha*.5f));
            var spin=moving?shimmer*.8f:0;
            for(var i=0;i<8;i++)
            {
                var a=i*Mathf.PI*.25f+spin;var dir=new Vector2(Mathf.Cos(a)*width*.5f,Mathf.Sin(a)*height*.5f);
                DrawLine(center+dir*.78f,center+dir*1.02f,new Color(1f,.86f,.5f,alpha*.9f),1.6f);
            }
        }
        private void DrawCardVfxPilePulse(CardVfxBurst b,float k)
        {
            var ease=1-(1-k)*(1-k);var glow=120*(.8f+.3f*ease);var ring=Mathf.Lerp(56,118,ease);
            DrawCardUiShape(new Rect(b.position.x-glow*.5f,b.position.y-glow*.5f,glow,glow),cardVfxGlow,new Color(b.color.r,b.color.g,b.color.b,(1-k)*(profile.reduceFlashing?.25f:.5f)));
            DrawCardUiShape(new Rect(b.position.x-ring*.5f,b.position.y-ring*.5f,ring,ring),cardVfxRing,new Color(b.color.r,b.color.g,b.color.b,(1-k)*.75f));
        }
        private void DrawCardVfxHandSweep(CardVfxBurst b,float k)
        {
            const float width=460,height=300;var x=Mathf.Lerp(b.position.x-600,b.position.x+600,Mathf.SmoothStep(0,1,k));
            var alpha=Mathf.Sin(k*Mathf.PI)*(profile.reduceFlashing?.28f:.5f);var band=new Rect(x-width*.5f,CombatHeight-height,width,height);
            var old=GUI.color;GUI.color=new Color(1f,.84f,.45f,alpha);GUI.DrawTexture(band,cardVfxSweep);GUI.color=old;
            if(profile.reducedVfx)return;
            for(var i=0;i<12;i++)
            {
                // Follow the texture's lean: the band sits further right toward the top.
                var v=CardVfxHash(b.seed,i);var y=band.y+30+v*(height-40);var lean=(.5f-(y-band.y)/height)*.55f*width;
                var q=new Vector2(x+lean+(CardVfxHash(b.seed,i+20)-.5f)*40,y-k*30);
                var s=3+CardVfxHash(b.seed,i+40)*4;DrawCardUiShape(new Rect(q.x-s,q.y-s,s*2,s*2),cardVfxGlow,new Color(1f,.86f,.5f,alpha*1.3f));
            }
        }

        // ---------- damage numbers (hook inside DrawCombatNumbers) ----------
        private bool DrawCritCombatNumber(CombatNumber number,float t,float alpha)
        {
            var amount=CardVfxCritAmount(number);if(amount<50)return false;
            if(!CardVfxRepaint)return true;
            EnsureCardVfxTextures();
            var big=amount>=150;var motion=CardVfxMotionAllowed;
            // Drift waits out the hit-stop hold.
            var p=number.origin-new Vector2(0,motion?Mathf.Max(0,t-.07f)*44:0);
            if(motion&&t<.24f){var shake=(1-t/.24f)*(big?4.5f:3f);p+=new Vector2(Mathf.Sin(t*97)*shake,Mathf.Cos(t*83)*shake*.6f);}
            if(big&&!profile.reduceFlashing)
            {
                var k=Mathf.Clamp01(t/.38f);var fade=(1-k)*(1-k);var glow=Mathf.Lerp(140,300,1-fade);
                DrawCardUiShape(new Rect(p.x-glow*.5f,p.y-glow*.5f,glow,glow),cardVfxGlow,new Color(1f,.78f,.34f,.55f*fade));
                if(!profile.reducedVfx)for(var i=0;i<10;i++)
                {
                    var a=i*Mathf.PI*.2f+CardVfxHash(amount,i)*.3f;var dir=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                    DrawLine(p+dir*(28+k*46),p+dir*(58+k*120),new Color(1f,.86f,.5f,.6f*fade),3f*(1-k)+1);
                }
            }
            var size=(big?58:44)+(profile.largeDamageNumbers?8:0);
            var punch=motion?1+Mathf.Pow(1-Mathf.Clamp01(t/.16f),2)*(big?.62f:.42f):1;
            var hot=profile.reduceFlashing?0:1-Mathf.Clamp01(t/.14f);
            var color=Color.Lerp(new Color(1f,.83f,.36f),new Color(1f,.97f,.84f),hot*.8f);color.a=alpha;
            var style=new GUIStyle(titleStyle){font=labelFont,fontSize=size,normal={textColor=color}};
            var matrix=GUI.matrix;
            if(punch>1.001f)GUI.matrix=matrix*Matrix4x4.TRS(new Vector3(p.x,p.y,0),Quaternion.identity,new Vector3(punch,punch,1))*Matrix4x4.Translate(new Vector3(-p.x,-p.y,0));
            ShadowLabel(new Rect(p.x-240,p.y-46,480,84),number.text,style);
            GUI.matrix=matrix;
            return true;
        }
        // After the screen-shake matrix in DrawCombat: brief zoom punch on big hits.
        private void ApplyBigHitPunch()
        {
            if(profile.reduceMotion||!profile.screenShake)return;
            var k=(Time.unscaledTime-cardVfxPunchStart)/.2f;if(k<0||k>=1)return;
            var amount=Mathf.Sin(Mathf.Sqrt(k)*Mathf.PI)*(1-k)*.045f*cardVfxPunchStrength;var c=cardVfxPunchCenter;
            GUI.matrix=GUI.matrix*Matrix4x4.TRS(new Vector3(c.x,c.y,0),Quaternion.identity,new Vector3(1+amount,1+amount,1))*Matrix4x4.Translate(new Vector3(-c.x,-c.y,0));
        }

        // ---------- energy orb (hook inside DrawEnergyMeter) ----------
        private void DrawEnergyOrbVfx(Rect seal,Color color)
        {
            if(!CardVfxRepaint||combat==null)return;
            EnsureCardVfxTextures();var c=seal.center;var diameter=Mathf.Min(seal.width,seal.height);
            if(energyPulse>0&&identityLastEnergy>=0)
            {
                // Spending sends a ring outward; gaining draws one inward.
                var k=1-energyPulse;var ease=1-(1-k)*(1-k);var gain=identityEnergyGain;
                var size=diameter*(CardVfxMotionAllowed?(gain?Mathf.Lerp(1.55f,.9f,ease):Mathf.Lerp(.85f,1.6f,ease)):1.15f);
                var tint=Color.Lerp(color,Gold,.45f);tint.a=(1-k)*(profile.reduceFlashing?.45f:.8f);
                DrawCardUiShape(new Rect(c.x-size*.5f,c.y-size*.5f,size,size),cardVfxRing,tint);
                if(!gain&&CardVfxParticles){var inner=size*.8f;tint.a*=.5f;DrawCardUiShape(new Rect(c.x-inner*.5f,c.y-inner*.5f,inner,inner),cardVfxRing,tint);}
            }
            if(combat.energy==0&&combat.phase==CombatPhase.Player&&!combat.IsOver&&CardVfxMotionAllowed&&!profile.reduceFlashing)
            {
                var radius=diameter*.42f;var arcs=profile.reducedVfx?1:3;var tick=Mathf.FloorToInt(shimmer*11);
                for(var i=0;i<arcs;i++)
                {
                    var seed=tick*7+i*131;if(CardVfxHash(seed,1)<.42f)continue;
                    var a0=CardVfxHash(seed,2)*Mathf.PI*2;var span=.35f+CardVfxHash(seed,3)*.4f;
                    var previous=c+new Vector2(Mathf.Cos(a0),Mathf.Sin(a0))*radius;
                    var tint=Color.Lerp(color,new Color(.92f,.9f,1f),.35f);tint.a=.5f+CardVfxHash(seed,4)*.35f;
                    for(var s=1;s<=3;s++)
                    {
                        var a=a0+span*s/3f;var rr=radius+(s==3?0:(CardVfxHash(seed,4+s)-.5f)*9);
                        var next=c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*rr;DrawLine(previous,next,tint,1.3f);previous=next;
                    }
                    if(!profile.reducedVfx){var spark=previous+(previous-c).normalized*(3+CardVfxHash(seed,9)*6);Fill(new Rect(spark.x-1,spark.y-1,2,2),tint);}
                }
            }
            DrawEnergyOrbFront(seal,color);
        }

        // ---------- fate shard (hook at the end of DrawShardFlights) ----------
        private void DrawShardFractureBursts()
        {
            if(!CardVfxRepaint||cardVfxBursts.Count==0||screen!=ScreenMode.Combat)return;
            var now=Time.unscaledTime;
            foreach(var b in cardVfxBursts)
            {
                if(b.kind!=CardVfxKind.Fracture)continue;var k=(now-b.start)/b.duration;if(k<0||k>=1)continue;
                EnsureCardVfxTextures();var fade=1-k;var ease=1-fade*fade;
                var glow=Mathf.Lerp(90,170,ease);var ring=Mathf.Lerp(60,190,ease);
                DrawCardUiShape(new Rect(b.position.x-glow*.5f,b.position.y-glow*.5f,glow,glow),cardVfxGlow,new Color(1f,.66f,.32f,(profile.reduceFlashing?.18f:.55f)*fade*fade));
                DrawCardUiShape(new Rect(b.position.x-ring*.5f,b.position.y-ring*.5f,ring,ring),cardVfxRing,new Color(1f,.74f,.42f,.6f*fade));
                if(!CardVfxMotionAllowed)continue;
                var count=profile.reducedVfx?6:16;var age=k*b.duration;
                for(var i=0;i<count;i++)
                {
                    var a=i/(float)count*Mathf.PI*2+(CardVfxHash(b.seed,i)-.5f)*.5f;var speed=110+CardVfxHash(b.seed,i+40)*150;
                    var point=b.position+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*speed*age*(1-.35f*k)+new Vector2(0,160*age*age);
                    var spin=a+age*(CardVfxHash(b.seed,i+80)*14-7);var len=5+CardVfxHash(b.seed,i+120)*8;var width=2.5f+CardVfxHash(b.seed,i+160)*2.5f;
                    var axis=new Vector2(Mathf.Cos(spin),Mathf.Sin(spin))*len*.5f;
                    var crystal=i%3==0?new Color(1f,.62f,.28f):new Color(.80f,.90f,1f);crystal.a=Mathf.Clamp01(fade*1.4f);
                    DrawLine(point-axis,point+axis,crystal,width);
                    if(i%4==0&&!profile.reduceFlashing)DrawLine(point-axis*.5f,point+axis*.5f,new Color(1,1,1,crystal.a*.8f),1);
                }
            }
        }
    }
}
