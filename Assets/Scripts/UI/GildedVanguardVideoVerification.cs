using System.Collections;
using System.IO;
using System.Linq;
using GildedFate.Core;
using GildedFate.Combat;
using UnityEngine;
namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private bool ConfigureVanguardVideoCapture(string mode)
        {
            if(mode!="vanguard-video-input"&&mode!="vanguard-video-attack")return false;
            combatTestInput=true;PrepareCombatCheck("strike",5);profile.reduceMotion=false;profile.fastMode=false;return true;
        }
        private IEnumerator RunVanguardVideoChecks()
        {
            vanguardVideoVerification=true;UpdateVanguardVideos();vanguardVideoBeats.Clear();
            var hp=combat.player.hp;var energy=combat.energy;var deck=run.deck.Count;
            foreach(var clip in VanguardVideoCatalog.Clips)
            {
                CombatCheck(File.Exists(clip.Path),"Vanguard asset present: "+clip.Name);
                vanguardVideos.Play(clip.Name,clip.Duration,true);
                var until=Time.realtimeSinceStartup+10;
                while(vanguardVideos.CurrentName!=clip.Name&&Time.realtimeSinceStartup<until)yield return null;
                yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
                CombatCheck(vanguardVideos.CurrentName==clip.Name,"Vanguard decoded: "+clip.Name);
                var rt=vanguardVideos.PresentedTexture;
                CombatCheck(rt!=null&&rt.width==rt.height,"Packed frame becomes square: "+clip.Name);
                if(rt)
                {
                    var old=RenderTexture.active;RenderTexture.active=rt;
                    var sample=new Texture2D(1,1,TextureFormat.RGBA32,false);sample.ReadPixels(new Rect(0,0,1,1),0,0);sample.Apply();
                    CombatCheck(sample.GetPixel(0,0).a<.08f,"Transparent background: "+clip.Name);
                    Destroy(sample);RenderTexture.active=old;
                }
            }
            CombatCheck(combat.player.hp==hp&&combat.energy==energy&&run.deck.Count==deck,"17 previews preserve gameplay state");
            vanguardVideoFrozen=true;vanguardVideos.Tick(true);yield return new WaitForSecondsRealtime(.12f);
            var pausedAt=vanguardVideos.ActionTime;yield return new WaitForSecondsRealtime(.18f);
            CombatCheck(System.Math.Abs(vanguardVideos.ActionTime-pausedAt)<.06,"Vanguard pauses during inspection");
            vanguardVideoFrozen=false;vanguardVideoVerification=false;vanguardVideos.Reset();
            foreach(var id in new[]{"strike","defend","unbreakable_spirit"})
            {
                PrepareCombatCheck(id,5);profile.reduceMotion=false;
                yield return WaitForCombatQueue();yield return new WaitForSecondsRealtime(.3f);
                var card=combat.hand[0];var before=combat.energy;var expected=combat.CostFor(card);var source=CardPickPoint(0);
                var target=combat.RequiresEnemyTarget(card)?EnemyDropZone.center:SkillDropZone.center;
                HandleCombatPointer(source,true,true,false);HandleCombatPointer(target,false,true,false);HandleCombatPointer(target,false,false,true);
                yield return WaitForCombatQueue();
                CombatCheck(!combat.hand.Contains(card)&&combat.energy==before-expected,"Vanguard drag/release still plays "+id);
            }
            PrepareCombatCheck("strike",5);profile.reduceMotion=false;
            CombatCheck(VanguardCardAnimation(combat.hand[0])=="BasicAttack","Basic attack mapping");
            vanguardAttackVariant=1;CombatCheck(VanguardCardAnimation(combat.hand[0])=="AlternateAttack","Alternate attack mapping");
            foreach(var pair in new[]{new[]{"STRENGTH","BuffStrength"},new[]{"FORTIFY","Fortify"},new[]{"RETALIATE","RetaliateReady"}})
            {
                vanguardVideoBeats.Clear();PlayVanguardStatus(new CombatEvent(CombatEventKind.Status,1,true,null,pair[0]));
                CombatCheck(vanguardVideoBeats.Any(b=>b.clip==pair[1]),"Status maps to "+pair[1]);
            }
            vanguardVideoBeats.Clear();PlayVanguardVital(new CombatEvent(CombatEventKind.Damage,5,false,null,"RETALIATE"));
            CombatCheck(vanguardVideoBeats.Any(b=>b.clip=="RetaliateAttack"),"Retaliation receipt maps to counterattack");
            vanguardVideoBeats.Clear();profile.reduceMotion=true;UpdateVanguardVideos();
            CombatCheck(vanguardVideos==null&&!VanguardVideoEnabled,"Reduced motion uses static fallback");
            profile.reduceMotion=false;UpdateVanguardVideos();
            Debug.Log("[Vanguard Video Verification] 17 clips and combat controls checked; failures="+combatInteractionFailures);
        }
        private IEnumerator PrepareVanguardVideoCapture()
        {
            vanguardVideoVerification=true;UpdateVanguardVideos();vanguardVideoBeats.Clear();
            vanguardVideos.Play("HeavyAttack",4,true);
            var until=Time.realtimeSinceStartup+10;
            while(vanguardVideos.CurrentName!="HeavyAttack"&&Time.realtimeSinceStartup<until)yield return null;
            yield return new WaitForSecondsRealtime(1.9f);
            vanguardVideoFrozen=true;vanguardVideos.Tick(true);
        }
    }
}
