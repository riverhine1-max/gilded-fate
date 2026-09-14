using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using GildedFate.Combat;
using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private bool ConfigureCompletionCapture(string mode)
        {
            if(!mode.StartsWith("polish-complete-"))return false;
            combatTestInput=true;AudioListener.pause=true;profile.fastMode=false;profile.reduceMotion=false;profile.reducedVfx=false;
            if(mode.EndsWith("fateweave")){ConfigureFinalPolishCapture("polish-final-fateweave");return true;}
            if(mode.EndsWith("retaliate"))
            {
                ConfigureShardCapture("group-four");combat.retaliation=18;combat.TakeEvents();RestoreCombatPresentation();return true;
            }
            run.NewRun(HeroId.Vanguard,20260909);currentNode=run.nodes.First(n=>n.kind==NodeKind.Boss);
            run.floor=currentNode.floor;run.RollEncounterRewards(NodeKind.Boss);run.pendingCombatGold=75;run.combatGoldClaimed=false;
            screen=controllerScreen=ScreenMode.Reward;screenControllerIndex=0;BeginRewardPresentation();return true;
        }
        private IEnumerator RunCompletionChecks()
        {
            yield return new WaitForSecondsRealtime(.2f);
            var snapshot=JsonUtility.ToJson(run);var entrance=importantRewardStarted;
            CombatCheck(!BeginRewardPresentation()&&importantRewardStarted==entrance,"Boss reward entrance is exactly once per room");
            var origin=screen;screen=ScreenMode.Collection;screen=origin;
            CombatCheck(!BeginRewardPresentation()&&JsonUtility.ToJson(run)==snapshot,"Deck return neither repeats the entrance nor mutates rewards");
            ConfigureFinalPolishCapture("polish-final-fateweave");run.act=1;run.BeginFateweave();run.fateweaveOffers.Clear();run.fateweaveOffers.AddRange(new[]{"foreign_memory","gilded_cache","burdened_fortune"});screenControllerIndex=0;
            DispatchMenuCheck(new MenuNavigation{accept=true});
            CombatCheck(FateweavePullActive&&FateweaveChoiceOpacity>.8f,"Chosen strand starts with visible unselected choices");
            yield return new WaitForSecondsRealtime(.42f);
            CombatCheck(FateweavePullActive&&FateweaveChoiceOpacity==0,"Other strands fade away before the pull completes");
            yield return new WaitForSecondsRealtime(1.1f);
            CombatCheck(!FateweavePullActive&&screen==ScreenMode.FateweaveCard,"Strand pull reaches its existing reward, without altering rules");
            for(var count=1;count<=4;count++)
            {
                if(count==1)PrepareCombatCheck("strike",5);else ConfigureShardCapture(count==2?"group-two":count==3?"group-three":"group-four");
                yield return WaitForCombatQueue();
                combat.retaliation=11;combat.player.block=40;combat.player.hp=80;combat.player.maxHp=80;
                var owner=count-1;combat.EnemyAt(owner).hp=combat.EnemyAt(owner).maxHp=500;combat.EnemyAt(owner).block=3;
                combat.TakeEvents();RestoreCombatPresentation();combatBusy=true;
                combat.InspectEnemy(owner,()=>
                {
                    var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                    var damage=typeof(CombatState).GetMethod("DamagePlayer",flags);
                    Action attack=()=>{for(var hit=0;hit<3;hit++)damage.Invoke(combat,new object[]{2});};
                    typeof(CombatState).GetMethod("ResolveEnemyAttack",flags).Invoke(combat,new object[]{attack});return 0;
                });
                var facts=combat.TakeEvents();var state=JsonUtility.ToJson(combat.CaptureCheckpoint());
                var duration=ConsumeCombatEvents(facts);var beat=retaliateReturns.SingleOrDefault();
                CombatCheck(beat!=null&&beat.owner==owner&&beat.amount==11&&beat.landAt>beat.start,"One full-stack return to the actual multi-hit attacker: "+count);
                CombatCheck(combat.player.hp==80&&combat.retaliation==0&&combat.EnemyAt(owner).hp==492,"Full Block triggers one return, retaining exact damage: "+count);
                if(beat!=null)
                {
                    yield return new WaitForSecondsRealtime(Mathf.Max(.01f,beat.start-Time.unscaledTime+.005f));
                    PlayerEffectChips();CombatCheck(VisibleRetaliation==11,"Consumed stack stays visible until its return lands: "+count);
                }
                yield return new WaitForSecondsRealtime(duration+.3f);combatBusy=false;
                CombatCheck(retaliateReturns.Count==0&&VisibleRetaliation==0,"Return flight and spent source clean up: "+count);
                CombatCheck(JsonUtility.ToJson(combat.CaptureCheckpoint())==state,"Return VFX cannot repeat gameplay: "+count);
            }
            foreach(var reduced in new[]{false,true})
            {
                profile.reduceMotion=reduced;profile.reducedVfx=reduced;
                var s=new CombatState();s.Begin(HeroId.Vanguard,Enumerable.Repeat(GameContent.Find("defend"),12),500,0,80,80,new[]{"gilded_buckle"},"gilded_sentry",9,444);s.TakeEvents();s.PlayFree(GameContent.Find("defend"));
                var facts=s.TakeEvents();combat=s;RestoreCombatPresentation();var duration=ConsumeCombatEvents(facts);
                CombatCheck(relicPulseBeats.Count(b=>b.id=="gilded_buckle")==1,"One legacy relic pulse, including reduced effects: "+reduced);
                yield return new WaitForSecondsRealtime(duration+.3f);
                CombatCheck(relicPulseBeats.Count==0,"Legacy relic feedback cleans up: "+reduced);
            }
            PrepareCombatCheck("strike",5);yield return WaitForCombatQueue();
            combat=new CombatState();combat.Begin(HeroId.Vanguard,Enumerable.Repeat(GameContent.Find("strike"),12),500,0,80,80,new[]{"gilded_heart"},"gilded_sentry",9,444);
            var openingFacts=combat.TakeEvents();RestoreCombatPresentation();ConsumeCombatEvents(openingFacts);
            var created=openingFacts.Where(f=>f.label=="OPENING STATUS").ToArray();
            CombatCheck(created.Length==2&&created.All(f=>f.generatedCard&&f.destination==CombatCardDestination.Draw),"Opening relic additions name both created cards and the actual destination");
            var createdLand=cardMotions.Where(m=>created.Any(f=>f.card==m.card)).Max(m=>m.start+m.duration);
            var shuffleStart=cardMotions.Where(m=>m.back).Min(m=>m.start);
            var drawnStart=cardMotions.Where(m=>!m.back&&openingFacts.Any(f=>f.kind==CombatEventKind.Draw&&f.card==m.card)).Where(m=>m.from==PilePoint(0)).Min(m=>m.start);
            CombatCheck(shuffleStart>=createdLand-.001f&&drawnStart>shuffleStart,"Opening additions arrive, then shuffle, then normal hand draws");
            yield return new WaitForSecondsRealtime(3f);
            ConfigureShardCapture("group-four");combat.retaliation=18;combat.memory.livingArmor=1;combat.memory.extraSigilSlots=3;
            combat.TakeEvents();RestoreCombatPresentation();
            yield return new WaitForSecondsRealtime(.3f);
            var frames=new float[90];var allocated=GC.GetAllocatedBytesForCurrentThread();var memory=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            for(var i=0;i<frames.Length;i++){yield return new WaitForEndOfFrame();frames[i]=Time.unscaledDeltaTime*1000;}
            allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;Array.Sort(frames);
            var allocationReport=allocated>0?$"{allocated/frames.Length} main-thread allocated bytes/frame":"allocation counter unavailable on this player runtime";
            Debug.Log($"[Gilded Fate Frame Profile] Four enemies / many effects: {frames.Length} release frames; median {frames[45]:F2} ms; p95 {frames[85]:F2} ms; {allocationReport}; Unity allocated {memory/1048576f:F1} MiB. Isolated-desktop sample, not a hardware FPS guarantee.");
            CombatCheck(frames.All(f=>!float.IsNaN(f)&&f>0)&&cardMotions.Count==0,"Four-enemy release frame sample and transient cleanup complete");
            ConfigureCompletionCapture("polish-complete-boss");
            Debug.Log($"[Gilded Fate Completion] {combatInteractionChecks} checks · {combatInteractionFailures} failures");
        }
    }
}
