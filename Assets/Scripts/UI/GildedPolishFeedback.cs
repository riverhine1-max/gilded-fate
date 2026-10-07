using GildedFate.Audio;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GildedFate.UI
{
    // Polish pass: feedback and juice that needs no new assets (existing sound cues, existing vignette draw).
    public sealed partial class GildedMainMenu
    {
        // ---------- combat number lanes ----------
        // Popups for one side stack in four lanes. The lane counter used to restart for every card, so two quick
        // cards drew their numbers on top of each other. It now carries on while earlier numbers are still visible.
        private int playerLaneNext,enemyLaneNext;
        private float playerLaneAt=-10,enemyLaneAt=-10;
        private const float NumberLaneMemory=.9f;
        private int NextNumberLane(bool playerSide,float startTime)
        {
            int lane;
            if(playerSide){lane=startTime-playerLaneAt>NumberLaneMemory?0:playerLaneNext;playerLaneNext=lane+1;playerLaneAt=startTime;}
            else{lane=startTime-enemyLaneAt>NumberLaneMemory?0:enemyLaneNext;enemyLaneNext=lane+1;enemyLaneAt=startTime;}
            return lane;
        }
        private void ResetNumberLanes(){playerLaneNext=enemyLaneNext=0;playerLaneAt=enemyLaneAt=-10;}

        // ---------- hurt vignette and low-health warning ----------
        // Any unblocked hit on the hero pulses the dark-red arena edge (it used to be boss hits only). A heavier
        // pulse is never replaced by a lighter one that arrives while it is still fading.
        private void PulseHurtVignette(int amount,bool heavy)
        {
            if(amount<=0)return;
            var power=heavy?Mathf.Clamp01(amount/40f)*.6f+.4f:Mathf.Clamp01(amount/30f)*.4f+.16f;
            var now=Time.unscaledTime;var remaining=Mathf.Clamp01(1-(now-finalVignetteAt)/.45f);
            if(power>=finalVignettePower*remaining*remaining){finalVignetteAt=now;finalVignettePower=power;}
        }
        private bool LowHealth(int hp,int maxHp)=>maxHp>0&&hp>0&&hp<=Mathf.Max(1,Mathf.FloorToInt(maxHp*.3f));
        // A slow red edge pulse while the hero is at 30% health or less. Reduce Motion holds it steady; Reduce Flashing
        // halves it. It is drawn under the HUD, like the hit vignette.
        private void DrawLowHealthWarning(float now)
        {
            if(combat==null||combat.IsOver||profile==null||!LowHealth(Mathf.RoundToInt(displayedPlayerHp),combat.player.maxHp))return;
            var pulse=profile.reduceMotion?.5f:.5f+.5f*Mathf.Sin(now*2.7f);
            var strength=(.11f+.12f*pulse)*(profile.reduceFlashing?.5f:1f);
            var w=CombatWidth;var h=CombatHeight;var edge=Mathf.Min(w,h)*.08f;
            for(var i=0;i<5;i++)
            {
                var k=(5-i)/5f;var x=edge*i/5f;var ww=edge/5f+1;var color=new Color(.85f,.06f,.05f,strength*k);
                Fill(new Rect(x,66,ww,h-66),color);Fill(new Rect(w-x-ww,66,ww,h-66),color);
            }
        }

        // ---------- first-use hitches ----------
        // The procedural glow, crescent, smoke, orb and ring textures were built the first time a card needed them, inside
        // a draw frame (a likely hitch on the first Dissipate or Gild). Building them when combat opens moves that cost
        // to a moment the player already expects to wait. The builders are idempotent.
        private void PrewarmCombatTextures()
        {
            try{EnsureFinalVfxTextures();EnsureEnergyOrbTextures();EnsureBossPolishTextures();}
            catch(System.Exception exception){Debug.LogWarning("[Gilded Fate] Combat texture prewarm skipped: "+exception.Message);}
        }

        // ---------- menu paths that used to be silent ----------
        private void PauseCue(bool opened)=>Sfx(opened?SoundCue.UiConfirm:SoundCue.UiBack);

        // ---------- enemy defeat ----------
        // No dedicated defeat cue exists, so a defeat is the soft dissipate whoosh plus a short shatter.
        private void PlayEnemyDeathSound(float pan)
        {
            Sfx(SoundCue.CardExhaust,pan,1f,true);
            Sfx(SoundCue.BlockBreak,pan,.6f,true,.06f);
        }

        // ---------- shared button feedback ----------
        // Every framed button reports hover here. One soft tick when the pointer or the controller focus arrives on a
        // new button, none while it rests there, and none for buttons under a modal (GUI.enabled is false).
        private Rect lastHotButton;
        private bool hotButtonSeen;
        private void ButtonHoverFeedback(Rect rect)
        {
            hotButtonSeen=true;
            if(lastHotButton==rect)return;
            if(captureMode){lastHotButton=rect;return;} // verification captures keep their sound counts exact
            lastHotButton=rect;Sfx(SoundCue.UiHover);
        }
        private void EndHotButtonFrame(){if(!hotButtonSeen)lastHotButton=default;hotButtonSeen=false;}
        private static bool PointerHeld=>Mouse.current!=null&&Mouse.current.leftButton.isPressed;

        // ---------- number tween for the top bar ----------
        // Gold and out-of-combat HP count to their new value instead of snapping. Reduce Motion, a new run, and the
        // first frame snap straight to the real number. Rules always use the real value; this is only what is drawn.
        private float goldShown=-1,hpShown=-1;
        private string shownRunId="";
        private int TweenShown(ref float shown,int target)
        {
            if(shown<0||profile==null||profile.reduceMotion){shown=target;return target;}
            if(Event.current.type==EventType.Repaint)shown=Mathf.MoveTowards(shown,target,Mathf.Max(1f,Mathf.Abs(target-shown)*7f)*Time.unscaledDeltaTime);
            return Mathf.RoundToInt(shown);
        }
    }
}
