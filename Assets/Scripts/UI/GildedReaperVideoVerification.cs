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
        private bool ConfigureReaperVideoCapture(string mode)
        {
            if(mode!="reaper-video-input"&&mode!="reaper-video-attack")return false;
            combatTestInput=true;PrepareCombatCheck("scythe_strike",5);profile.reduceMotion=false;profile.fastMode=false;return true;
        }
        private IEnumerator RunReaperVideoChecks()
        {
            reaperVideoVerification=true;UpdateReaperVideos();reaperVideoBeats.Clear();
            var hp=combat.player.hp;var energy=combat.energy;var deck=run.deck.Count;
            foreach(var clip in ReaperVideoCatalog.Clips)
            {
                CombatCheck(File.Exists(clip.Path),"Reaper asset present: "+clip.Name);
                reaperVideos.Play(clip.Name,clip.Duration,true);
                var until=Time.realtimeSinceStartup+10;
                while(reaperVideos.CurrentName!=clip.Name&&Time.realtimeSinceStartup<until)yield return null;
                yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
                CombatCheck(reaperVideos.CurrentName==clip.Name,"Reaper decoded: "+clip.Name);
                var rt=reaperVideos.PresentedTexture;
                CombatCheck(rt!=null&&rt.width==rt.height,"Packed frame becomes square: "+clip.Name);
                if(rt)
                {
                    var old=RenderTexture.active;RenderTexture.active=rt;
                    var sample=new Texture2D(1,1,TextureFormat.RGBA32,false);sample.ReadPixels(new Rect(0,0,1,1),0,0);sample.Apply();
                    CombatCheck(sample.GetPixel(0,0).a<.08f,"Transparent background: "+clip.Name);
                    Destroy(sample);RenderTexture.active=old;
                }
            }
            CombatCheck(combat.player.hp==hp&&combat.energy==energy&&run.deck.Count==deck,"16 previews preserve gameplay state");
            reaperVideoFrozen=true;reaperVideos.Tick(true);yield return new WaitForSecondsRealtime(.12f);
            var pausedAt=reaperVideos.ActionTime;yield return new WaitForSecondsRealtime(.18f);
            CombatCheck(System.Math.Abs(reaperVideos.ActionTime-pausedAt)<.06,"Reaper pauses during inspection");
            reaperVideoFrozen=false;reaperVideoVerification=false;reaperVideos.Reset();
            foreach(var id in new[]{"scythe_strike","deaths_veil","death_incarnate"})
            {
                PrepareCombatCheck(id,5);profile.reduceMotion=false;
                yield return WaitForCombatQueue();yield return new WaitForSecondsRealtime(.3f);
                var card=combat.hand[0];var before=combat.energy;var expected=combat.CostFor(card);var source=CardPickPoint(0);
                var target=combat.RequiresEnemyTarget(card)?EnemyDropZone.center:SkillDropZone.center;
                HandleCombatPointer(source,true,true,false);HandleCombatPointer(target,false,true,false);HandleCombatPointer(target,false,false,true);
                yield return WaitForCombatQueue();
                CombatCheck(!combat.hand.Contains(card)&&combat.energy==before-expected,"Reaper drag/release still plays "+id);
            }
            PrepareCombatCheck("scythe_strike",5);profile.reduceMotion=false;
            CombatCheck(ReaperCardAnimation(combat.hand[0])=="BasicScytheAttack","Basic attack mapping");
            reaperAttackVariant=1;CombatCheck(ReaperCardAnimation(combat.hand[0])=="AlternateScytheAttack","Alternate attack mapping");
            reaperVideoBeats.Clear();PlayReaperStatus(new CombatEvent(CombatEventKind.Status,1,false,null,"GRAVEMARK"));
            CombatCheck(reaperVideoBeats.Any(b=>b.clip=="GraveMarkInteraction"),"Gravemark receipt selects signature animation");
            reaperVideoBeats.Clear();
            ScheduleReaperVideoReceipts(new[]{new CombatEvent(CombatEventKind.Draw,1,true,GameContent.Find("soul"),"SOUL"){generatedCard=true}},new float[]{0},Time.unscaledTime);
            CombatCheck(reaperVideoBeats.Any(b=>b.clip=="SoulGeneration"),"Generated Soul animation");
            reaperVideoBeats.Clear();
            ScheduleReaperVideoReceipts(new[]{new CombatEvent(CombatEventKind.Exhaust,1,true,GameContent.Find("soul"))},new float[]{0},Time.unscaledTime);
            CombatCheck(reaperVideoBeats.Any(b=>b.clip=="SoulConsumption"),"Soul exhaust animation");
            reaperVideoBeats.Clear();
            ScheduleReaperVideoReceipts(new[]{new CombatEvent(CombatEventKind.Exhaust,1,true,GameContent.Find("scythe_strike"))},new float[]{0},Time.unscaledTime);
            CombatCheck(reaperVideoBeats.Any(b=>b.clip=="Dissipate"),"Other exhaust animation");
            CombatCheck(ReaperCardAnimation(GameContent.Find("soul"))=="SoulConsumption","Targeted Soul action mapping");
            CombatCheck(ReaperCardAnimation(GameContent.Find("death_incarnate"))=="AspectActivation","Power animation mapping");
            reaperVideoBeats.Clear();profile.reduceMotion=true;UpdateReaperVideos();
            CombatCheck(reaperVideos==null&&!ReaperVideoEnabled,"Reduced motion uses static fallback");
            profile.reduceMotion=false;UpdateReaperVideos();
            Debug.Log("[Reaper Video Verification] 16 clips and combat controls checked; failures="+combatInteractionFailures);
        }
        private IEnumerator PrepareReaperVideoCapture()
        {
            reaperVideoVerification=true;UpdateReaperVideos();reaperVideoBeats.Clear();
            reaperVideos.Play("HeavyAttack",4,true);
            var until=Time.realtimeSinceStartup+10;
            while(reaperVideos.CurrentName!="HeavyAttack"&&Time.realtimeSinceStartup<until)yield return null;
            yield return new WaitForSecondsRealtime(1.9f);
            reaperVideoFrozen=true;reaperVideos.Tick(true);
        }
    }
}
