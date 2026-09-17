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
        private bool hexerVideoVerification;
        private bool ConfigureHexerVideoCapture(string mode)
        {
            if(mode!="hexer-video-input"&&mode!="hexer-video-attack")return false;
            combatTestInput=true;PrepareCombatCheck("hex_strike",5);profile.reduceMotion=false;profile.fastMode=false;
            return true;
        }
        private IEnumerator RunHexerVideoChecks()
        {
            hexerVideoVerification=true;UpdateHexerVideos();hexerVideoBeats.Clear();
            var hp=combat.player.hp;var energy=combat.energy;var deck=run.deck.Count;
            foreach(var clip in HexerVideoCatalog.Clips)
            {
                CombatCheck(File.Exists(clip.Path),"Hexer asset present: "+clip.Name);
                hexerVideos.Play(clip.Name,clip.Duration,true);
                var until=Time.realtimeSinceStartup+10;
                while(hexerVideos.CurrentName!=clip.Name&&Time.realtimeSinceStartup<until)yield return null;
                yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
                CombatCheck(hexerVideos.CurrentName==clip.Name,"Hexer decoded: "+clip.Name);
                var rt=hexerVideos.PresentedTexture;
                CombatCheck(rt!=null&&rt.width==rt.height,"Packed frame becomes square: "+clip.Name);
                if(rt)
                {
                    var old=RenderTexture.active;RenderTexture.active=rt;
                    var sample=new Texture2D(1,1,TextureFormat.RGBA32,false);sample.ReadPixels(new Rect(0,0,1,1),0,0);sample.Apply();
                    CombatCheck(sample.GetPixel(0,0).a<.08f,"Transparent background: "+clip.Name);
                    Destroy(sample);RenderTexture.active=old;
                }
            }
            CombatCheck(combat.player.hp==hp&&combat.energy==energy&&run.deck.Count==deck,"22 previews preserve gameplay state");
            hexerVideoFrozen=true;hexerVideos.Tick(true);yield return new WaitForSecondsRealtime(.12f);
            var pausedAt=hexerVideos.ActionTime;yield return new WaitForSecondsRealtime(.18f);
            CombatCheck(System.Math.Abs(hexerVideos.ActionTime-pausedAt)<.06,"Hexer video pauses during inspection");
            hexerVideoFrozen=false;hexerVideoVerification=false;hexerVideos.Reset();
            foreach(var id in new[]{"hex_strike","ward","sigil_mastery"})
            {
                PrepareCombatCheck(id,5);profile.reduceMotion=false;
                yield return WaitForCombatQueue();yield return new WaitForSecondsRealtime(.3f);
                var card=combat.hand[0];var before=combat.energy;var expected=combat.CostFor(card);var source=CardPickPoint(0);
                var target=combat.RequiresEnemyTarget(card)?EnemyDropZone.center:SkillDropZone.center;
                HandleCombatPointer(source,true,true,false);
                HandleCombatPointer(target,false,true,false);
                HandleCombatPointer(target,false,false,true);
                yield return WaitForCombatQueue();
                CombatCheck(!combat.hand.Contains(card)&&combat.energy==before-expected,"Hexer drag/release still plays "+id);
            }
            PrepareCombatCheck("first_ritual",5);profile.reduceMotion=false;
            CombatCheck(HexerCardAnimation(combat.hand[0])=="FirstRitual","First Ritual selects its signature animation");
            hexerVideoBeats.Clear();
            var activation=new[]{new CombatEvent(CombatEventKind.Status,1,true,null,"HEX ACTIVATE"){sigilSlot=0},new CombatEvent(CombatEventKind.Status,1,true,null,"HEX ACTIVATE"){sigilSlot=0}};
            ScheduleHexerVideoReceipts(activation,new float[]{0,.12f},Time.unscaledTime);
            CombatCheck(hexerVideoBeats.Any(x=>x.clip=="OverflowTrigger"),"Repeated Sigil activation uses the double-trigger reference");
            hexerVideoBeats.Clear();
            ScheduleHexerVideoReceipts(new[]{new CombatEvent(CombatEventKind.Status,1,true,null,"ECHO SIGIL")},new float[]{0},Time.unscaledTime);
            CombatCheck(hexerVideoBeats.Any(x=>x.clip=="EchoTrigger"),"Echo has a distinct animation mapping");
            hexerVideoBeats.Clear();profile.reduceMotion=true;UpdateHexerVideos();
            CombatCheck(hexerVideos==null&&!HexerVideoEnabled,"Reduced motion uses static portrait fallback");
            profile.reduceMotion=false;UpdateHexerVideos();
            Debug.Log("[Hexer Video Verification] 22 clips and combat controls checked; failures="+combatInteractionFailures);
        }
        private IEnumerator PrepareHexerVideoCapture()
        {
            hexerVideoVerification=true;UpdateHexerVideos();hexerVideoBeats.Clear();
            hexerVideos.Play("PowerfulSigilAttack",3,true);
            var until=Time.realtimeSinceStartup+10;
            while(hexerVideos.CurrentName!="PowerfulSigilAttack"&&Time.realtimeSinceStartup<until)yield return null;
            yield return new WaitForSecondsRealtime(1.2f);
            hexerVideoFrozen=true;hexerVideos.Tick(true);
        }
    }
}
