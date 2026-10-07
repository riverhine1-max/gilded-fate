using GildedFate.Audio;
using GildedFate.Combat;
using UnityEngine;

namespace GildedFate.UI
{
    // Polish pass: how aiming an Attack and playing a card FEEL. Presentation only; which target a card hits, what it
    // costs and when it resolves are decided by the same rules as before.
    //
    // What was rough, and what replaces it:
    //   * The pull that carries an Attack card out of the hand was read straight from the pointer, so a quick flick made the
    //     card and the guide jump. It is now an eased value (aimPull) that the card pose and the guide both follow.
    //   * The target guide popped in at one pointer height and was drawn as a stiff line of rotated rectangles. It now fades
    //     in with the pull, the brackets close onto the target as it locks, and the aim arrow is a soft dotted curve whose
    //     tip glides onto the target instead of snapping.
    //   * A target's hit region had a hard edge, so a steady hand could flicker between "on" and "off". Once an Attack is
    //     over a target, the pointer may drift a few pixels outside it and the lock holds; the same rule decides the drop.
    //   * A played card started its flight with a slow ease-in (it seemed to hang for a moment after release); the flight now
    //     leaves at speed and settles onto the impact point.
    public sealed partial class GildedMainMenu
    {
        private float aimPull,aimReveal,aimLock;
        private Vector2 aimTip;
        private int stickyTarget=-1,aimTargetIndex=-1,aimLockKey=-2;
        private bool aimFadeEnemy;
        private Rect aimFadeZone;
        // How far outside a target the pointer may drift once it is locked on.
        private const float StickyTargetMargin=22f;

        private static float Approach(float value,float target,float rate,float dt)=>Mathf.Lerp(value,target,1-Mathf.Exp(-rate*dt));
        private Rect TargetZone(int index)=>GroupCombat?GroupDropZone(Mathf.Clamp(index,0,Mathf.Max(0,combat.EnemyCount-1))):ComfortableEnemyTarget(EnemyPortraitRect);

        private void ResetTargetingFeel()
        {
            aimPull=aimReveal=aimLock=0;aimTip=combatPointer;stickyTarget=aimTargetIndex=-1;aimLockKey=-2;aimFadeZone=default;
        }

        // The living target under the pointer. While an Attack is being dragged, a target it is already over stays locked
        // until the pointer leaves it by StickyTargetMargin; another target's own region always wins immediately.
        private int AimTargetAt(Vector2 point,bool dragging)
        {
            var hit=TargetAt(point);
            if(!dragging){stickyTarget=-1;return hit;}
            if(hit>=0){stickyTarget=hit;return hit;}
            if(stickyTarget>=0&&combat!=null&&combat.IsLivingTarget(stickyTarget))
            {
                var zone=TargetZone(stickyTarget);
                if(new Rect(zone.x-StickyTargetMargin,zone.y-StickyTargetMargin,zone.width+StickyTargetMargin*2,zone.height+StickyTargetMargin*2).Contains(point))return stickyTarget;
            }
            stickyTarget=-1;return -1;
        }

        // Called once per frame before the hand is laid out.
        private void UpdateTargetingFeel(float dt)
        {
            var view=dragView??selectedView;var reduce=profile!=null&&profile.reduceMotion;
            var live=view?.card!=null&&combat!=null&&!combatBusy&&!combat.IsOver;
            if(!live)
            {
                // Nothing is being aimed: ease everything out so the guide fades instead of vanishing.
                aimPull=Approach(aimPull,0,22,dt);aimLock=Approach(aimLock,0,22,dt);aimReveal=Approach(aimReveal,0,reduce?30:16,dt);
                stickyTarget=aimTargetIndex=-1;aimLockKey=-2;aimTip=combatPointer;return;
            }
            var enemy=combat.RequiresEnemyTarget(view.card);var dragging=dragView!=null&&cardDragging;
            var rawPull=dragView==null?1f:dragging?Mathf.Clamp01((pressPoint.y-combatPointer.y-24)/105f):0f;
            aimPull=enemy?Approach(aimPull,rawPull,reduce?30:16,dt):0f;
            aimTargetIndex=enemy?AimTargetAt(combatPointer,dragging):-1;
            var valid=enemy?aimTargetIndex>=0:SkillDropZone.Contains(combatPointer);
            aimLock=Approach(aimLock,valid?1f:0f,reduce?30:20,dt);
            var key=valid?(enemy?aimTargetIndex:0):-1;
            if(key!=aimLockKey)
            {
                if(key>=0&&dragging){targetLockPulse=1;if(enemy&&!captureMode&&aimReveal>.3f)Sfx(SoundCue.UiHover,intensity:.5f);}
                aimLockKey=key;
            }
            if(enemy)aimReveal=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.22f,.55f,aimPull));
            else aimReveal=Approach(aimReveal,dragging||dragView==null?1f:0f,reduce?30:16,dt);
            // The arrow tip follows the pointer with a very short lag (it filters hand jitter without feeling late) and is
            // drawn gently toward the centre of a locked target.
            var desired=combatPointer;
            if(enemy&&aimTargetIndex>=0&&!reduce&&dragView!=null)desired=Vector2.Lerp(combatPointer,TargetZone(aimTargetIndex).center,.38f*aimLock);
            aimTip=aimReveal<.02f?combatPointer:Vector2.Lerp(aimTip,desired,1-Mathf.Exp(-(reduce?60:36)*dt));
            aimFadeEnemy=enemy;aimFadeZone=enemy?EnemyDropZone:SkillDropZone;
        }

        // ---------- card flights ----------
        // A played card leaves the hand at speed and settles onto its impact point. (The other flights keep their
        // smooth-step: draws, discards and Dissipate start and end at rest on purpose.)
        private static float CardMotionEase(CardMotion motion,float t)
        {
            t=Mathf.Clamp01(t);
            if(motion!=null&&motion.play)return 1f-Mathf.Pow(1f-t,2.4f);
            return t*t*(3f-2f*t);
        }
        // The lift follows distance travelled for a played card, so the arc is a smooth parabola that never starts with a
        // jump, and short plays stay flat.
        private static float CardMotionLift(CardMotion motion,float t,float eased)
        {
            if(motion.back)return Mathf.Sin(t*Mathf.PI)*65f;
            if(motion.play)return Mathf.Sin(eased*Mathf.PI)*Mathf.Min(38f,Vector2.Distance(motion.from,motion.to)*.12f);
            return Mathf.Sin(t*Mathf.PI)*38f;
        }

        // ---------- the aiming guide ----------
        private void DrawTargetGuide()
        {
            var active=dragView??selectedView;
            if(active?.card==null||combat==null||combatBusy){DrawTargetGuideFade();return;}
            var enemy=combat.RequiresEnemyTarget(active.card);var reveal=aimReveal;var lockAmount=aimLock;var dragging=dragView!=null&&cardDragging;
            var target=enemy?EnemyDropZone:SkillDropZone;
            if(enemy&&dragging)
            {
                var hint=(1-reveal)*(1-reveal);
                if(hint>.02f)GUI.Label(new Rect(active.position.x-150,active.position.y-HandLayout.CardHeight*.72f,300,28),"PULL UP TO TARGET",new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=13,normal={textColor=new Color(.92f,.82f,.58f,hint)}});
            }
            if(reveal<.01f)return;
            var color=Color.Lerp(enemy?new Color(1,.42f,.3f):new Color(.46f,.81f,1),Gold,lockAmount);
            DrawAimFrame(target,enemy,color,reveal,lockAmount);
            if(!enemy)return;
            var start=new Vector2(active.position.x,active.position.y-HandLayout.CardHeight*active.scale*.43f);
            DrawAimArrow(start,aimTip,color,reveal,lockAmount);
            var preview=CardPreview(active.card);var shownEnemy=combat.EnemyAt(Mathf.Clamp(combatTargetIndex,0,combat.EnemyCount-1));var damage=preview.DamageExpression;if(preview.triggeredDamage>0)damage+=" + "+preview.triggeredDamage+" TRIGGERED";
            var absorbed=Mathf.Min(shownEnemy.block,preview.blockedByTarget);var lost=preview.hpDamage;
            var result=new Rect(target.center.x-160,target.yMax+8+(profile.reduceMotion?0:(1-reveal)*8),320,42);
            Fill(result,new Color(.008f,.012f,.019f,.96f*reveal));Outline(result,new Color(color.r,color.g,color.b,reveal),2);
            GUI.Label(result,$"{damage} DAMAGE   ·   BLOCK {shownEnemy.block}→{Mathf.Max(0,shownEnemy.block-absorbed)}   ·   HP {shownEnemy.hp}→{Mathf.Max(0,shownEnemy.hp-lost)}",new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=12,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(color.r,color.g,color.b,reveal)}});
            if(lockAmount>.5f&&targetLockPulse>0)
            {
                var spread=5+Mathf.RoundToInt((1-targetLockPulse)*9);
                Outline(new Rect(target.x-spread,target.y-spread,target.width+spread*2,target.height+spread*2),new Color(1f,.88f,.52f,targetLockPulse*reveal),4);
            }
        }

        // After a release the dim and the brackets ease out; nothing pops off in a single frame.
        private void DrawTargetGuideFade()
        {
            if(aimReveal<.02f||aimFadeZone.width<=0)return;
            DrawAimFrame(aimFadeZone,aimFadeEnemy,Color.Lerp(aimFadeEnemy?new Color(1,.42f,.3f):new Color(.46f,.81f,1),Gold,aimLock),aimReveal,aimLock);
        }

        // The dim around the target, the four brackets (they close onto the target as it locks) and the caption.
        private void DrawAimFrame(Rect target,bool enemy,Color color,float reveal,float lockAmount)
        {
            if(enemy)
            {
                // Recede everything except the valid target without placing an opaque panel over the enemy itself.
                var side=new Color(0,0,0,.20f*reveal);var cap=new Color(0,0,0,.13f*reveal);
                Fill(new Rect(0,66,target.x,CombatHeight-66),side);Fill(new Rect(target.xMax,66,CombatWidth-target.xMax,CombatHeight-66),side);
                Fill(new Rect(target.x,66,target.width,Mathf.Max(0,target.y-66)),cap);Fill(new Rect(target.x,target.yMax,target.width,CombatHeight-target.yMax),cap);
            }
            var grow=profile.reduceMotion?0f:(1-lockAmount)*8f;
            var zone=new Rect(target.x-grow,target.y-grow,target.width+grow*2,target.height+grow*2);
            var bracket=new Color(color.r,color.g,color.b,Mathf.Lerp(.6f,1f,lockAmount)*reveal);var thickness=Mathf.Lerp(2f,4f,lockAmount);
            foreach(var corner in new[]{new Vector2(zone.x,zone.y),new Vector2(zone.xMax,zone.y),new Vector2(zone.x,zone.yMax),new Vector2(zone.xMax,zone.yMax)})
            {
                var dx=corner.x==zone.x?1:-1;var dy=corner.y==zone.y?1:-1;
                DrawLine(corner,corner+new Vector2(24*dx,0),bracket,thickness);DrawLine(corner,corner+new Vector2(0,24*dy),bracket,thickness);
            }
            var caption=new Rect(zone.x,zone.y-28,zone.width,25);
            AimCaption(caption,enemy?"TARGET ENEMY":"PLAY IN THE BATTLEFIELD",new Color(color.r,color.g,color.b,(1-lockAmount)*reveal));
            AimCaption(caption,"RELEASE TO PLAY",new Color(color.r,color.g,color.b,lockAmount*reveal));
        }
        private void AimCaption(Rect rect,string text,Color color)
        {
            if(color.a<.02f)return;
            GUI.Label(rect,text,new GUIStyle(footerStyle){fontSize=16,normal={textColor=color}});
        }

        // A soft dotted curve that flows toward the target, a ring that tightens and brightens on lock, and a diamond head.
        private void DrawAimArrow(Vector2 start,Vector2 tip,Color color,float reveal,float lockAmount)
        {
            var distance=Vector2.Distance(start,tip);if(distance<14f||reveal<.01f)return;
            EnsureCardVfxTextures();
            var motion=!profile.reduceMotion;var flashes=!profile.reduceFlashing;var now=Time.unscaledTime;
            // Quadratic curve bowed upward; the bow grows with distance so near and far targets both read as one gesture.
            var bow=Mathf.Clamp(distance*.16f,16f,64f);var control=(start+tip)*.5f-Vector2.up*(bow*2f);
            var ringSize=Mathf.Lerp(46f,66f,lockAmount)*(motion?1f+.05f*Mathf.Sin(now*7f)*lockAmount:1f);
            var headLength=ringSize*.35f+26f+4f;var curveLength=distance*1.08f;
            var tEnd=Mathf.Clamp(1f-headLength/Mathf.Max(curveLength,1f),.12f,.95f);
            var count=Mathf.Clamp(Mathf.RoundToInt(curveLength*tEnd/18f),3,34);
            var phase=motion?Mathf.Repeat(now*.9f,1f):0f;var bright=Color.Lerp(color,Color.white,.55f);
            for(var i=0;i<count;i++)
            {
                var u=(i+phase)/count;var p=GildArc(start,control,tip,u*tEnd);
                var fade=Mathf.SmoothStep(0,1,Mathf.Clamp01(u/.14f))*(1f-Mathf.SmoothStep(0,1,Mathf.Clamp01((u-.86f)/.14f)));
                var size=Mathf.Lerp(7f,13f,u)*(1f+.18f*lockAmount);var alpha=Mathf.Lerp(.40f,.95f,u)*fade*reveal;
                DrawCardUiShape(new Rect(p.x-size*1.3f,p.y-size*1.3f,size*2.6f,size*2.6f),cardVfxGlow,new Color(color.r,color.g,color.b,alpha*.35f));
                DrawCardUiShape(new Rect(p.x-size*.5f,p.y-size*.5f,size,size),cardVfxGlow,new Color(bright.r,bright.g,bright.b,alpha));
            }
            DrawCardUiShape(new Rect(tip.x-ringSize*.75f,tip.y-ringSize*.75f,ringSize*1.5f,ringSize*1.5f),cardVfxGlow,new Color(color.r,color.g,color.b,(.16f+.18f*lockAmount)*reveal*(flashes?1f:.6f)));
            DrawCardUiShape(new Rect(tip.x-ringSize*.5f,tip.y-ringSize*.5f,ringSize,ringSize),cardVfxRing,new Color(color.r,color.g,color.b,Mathf.Lerp(.55f,.95f,lockAmount)*reveal));
            var direction=(tip-control).normalized;var angle=Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg;var matrix=GUI.matrix;
            GUI.matrix=matrix*Matrix4x4.TRS(new Vector3(tip.x,tip.y,0),Quaternion.Euler(0,0,angle),Vector3.one);
            var inner=ringSize*.35f;
            DrawCardUiShape(new Rect(-(inner+26f)+1.5f,-9f+2f,26f,18f),cardVfxPip,new Color(0,0,0,.45f*reveal));
            DrawCardUiShape(new Rect(-(inner+26f),-9f,26f,18f),cardVfxPip,new Color(bright.r,bright.g,bright.b,reveal));
            GUI.matrix=matrix;
        }
    }
}
