using System.Collections;
using System.IO;
using System.Linq;
using GildedFate.Combat;
using GildedFate.Core;
using GildedFate.Map;
using GildedFate.Saving;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private int persistenceChecks,persistenceFailures;
        private void PersistenceCheck(bool passed,string message)
        {
            persistenceChecks++;
            if(passed)Debug.Log("[Gilded Fate Persistence] PASS · "+message);
            else{persistenceFailures++;Debug.LogError("[Gilded Fate Persistence] FAIL · "+message);}
        }
        private string CheckpointJson(CombatState state)=>JsonUtility.ToJson(state.CaptureCheckpoint());
        private void PreparePersistenceBattle(string id)
        {
            verificationSavesAllowed=false;combatTestInput=true;
            run.NewRun(HeroId.Hexer,809);currentNode=run.nodes[0];currentEnemy=WorldContent.Enemies.First(e=>e.id=="gilded_sentry");
            var definition=GameContent.Cards.First(c=>c.id==id);
            combat=new CombatState();combat.Begin(HeroId.Hexer,Enumerable.Repeat(definition,14),1000,0,200,200,null,currentEnemy.id,9,9513);combat.energy=10;
            run.stage=RunStage.Combat;run.activeEnemyId=currentEnemy.id;run.activeNodeFloor=currentNode.floor;run.activeNodeLane=currentNode.lane;run.hp=run.maxHp=200;
            screen=ScreenMode.Combat;bossIntroTime=0;ResetCombatPresentation();verificationSavesAllowed=true;
        }
        private IEnumerator WaitForChoiceUI()
        {
            var deadline=Time.unscaledTime+5;
            while(!choicePresented&&Time.unscaledTime<deadline)yield return null;
            PersistenceCheck(choicePresented&&combat.AwaitingChoice,"Choice UI opens and waits for input");
        }
        private IEnumerator RunPersistenceChecks()
        {
            combatTestInput=true;profile.cardAnimationSpeed=1;profile.fastMode=false;
            PreparePersistenceBattle("hex_strike");
            var snapshot=combat.CaptureCheckpoint();var json=JsonUtility.ToJson(snapshot);var decoded=JsonUtility.FromJson<CombatCheckpoint>(json);
            PersistenceCheck(decoded.TryRestore(out var restored,out var decodeError),"Unity serializer restores a valid combat: "+decodeError);
            if(restored==null){Debug.Log("[Gilded Fate Persistence] Checkpoint diagnostics: "+json);verificationSavesAllowed=false;yield break;}
            PersistenceCheck(CheckpointJson(restored)==json,"Unity serialization preserves every saved field");
            PersistenceCheck(restored.AllOwnedCards().All(c=>c.hero==HeroId.Hexer),"Reloaded Hexer cards retain their artwork family");
            PersistenceCheck(SaveCombatCheckpoint(),"Initial battle checkpoint writes safely");
            combat.Play(combat.hand[2]);combat.player.weak=2;combat.enemy.burn=4;combat.memory.echoArmed=1;
            PersistenceCheck(SaveCombatCheckpoint(),"Played card, Energy, statuses and one-use memory write together");
            var expected=CheckpointJson(combat);var loaded=SaveService.Load();PersistenceCheck(loaded!=null&&loaded.hasCombatCheckpoint,"Saved battle loads from disk");
            if(loaded==null){verificationSavesAllowed=false;yield break;}
            CopyRun(loaded);RestoreRunStage();PersistenceCheck(CheckpointJson(combat)==expected,"Continue restores this exact battle instead of BeginCombat");

            foreach(var id in new[]{"clear_mind","recycle"})
            {
                PreparePersistenceBattle(id);
                yield return new WaitForSecondsRealtime(.9f);
                var source=combat.hand[2];var cost=combat.CostFor(source);var energy=combat.energy;QueueCardPlay(handViews[source.instanceId]);
                yield return WaitForChoiceUI();
                var choicesBefore=combat.ChoiceCards.Select(c=>c.instanceId).ToArray();expected=CheckpointJson(combat);
                PersistenceCheck(SaveCombatAndReturnToMenu()&&screen==ScreenMode.Menu,"Save & Menu works during "+id+" choice");
                CopyRun(SaveService.Load());RestoreRunStage();yield return WaitForChoiceUI();
                PersistenceCheck(CheckpointJson(combat)==expected&&combat.ChoiceCards.Select(c=>c.instanceId).SequenceEqual(choicesBefore),"Pending "+id+" resumes without redrawing or repaying");
                var selected=combat.ChoiceCards[0];var point=ChoiceCardRect(0).center;
                HandleCombatPointer(point,true,true,false);HandleCombatPointer(point,false,false,true);
                PersistenceCheck(!SubmitCombatChoice(selected),"Choice rejects a duplicate click before resolution");
                yield return WaitForCombatQueue();
                PersistenceCheck(combat.pendingPlay==null&&combat.cardsPlayed==1&&combat.energy==energy-cost,"Choice finishes the original paid card once: "+id);
                PersistenceCheck(id=="clear_mind"?combat.discard.Contains(selected):combat.exhaust.Contains(selected),"Exact selected copy reaches its chosen pile: "+id);
                PersistenceCheck(combat.AllOwnedCards().Select(c=>c.instanceId).Distinct().Count()==14,"Choice does not lose or duplicate card ownership");
            }

            PreparePersistenceBattle("hex_strike");yield return new WaitForSecondsRealtime(.9f);
            var originalCards=combat.hand.Select(c=>c.instanceId).ToArray();var originalEnergy=combat.energy;
            QueueCardPlay(handViews[combat.hand[0].instanceId]);combatPauseOpen=true;SaveCombatAndReturnToMenu();CopyRun(SaveService.Load());RestoreRunStage();
            PersistenceCheck(combat.energy==originalEnergy&&combat.hand.Select(c=>c.instanceId).SequenceEqual(originalCards)&&combat.cardsPlayed==0,"Saving during card anticipation preserves the unspent card and Energy");
            run.AddShard("bloodstone");var savedShard=run.shards[0];var shard=WorldContent.FateShards.First(p=>p.id=="bloodstone");
            QueueShard(shard,savedShard,0);combatPauseOpen=true;SaveCombatAndReturnToMenu();CopyRun(SaveService.Load());RestoreRunStage();
            PersistenceCheck(string.IsNullOrEmpty(combat.activeShardId)&&run.shards[0].uses==0,"Saving during Fate Shard anticipation preserves it until activation commits");
            savedShard=run.shards[0];QueueShard(shard,savedShard,0);yield return WaitForCombatQueue();
            PersistenceCheck(combat.activeShardId=="bloodstone"&&combat.player.strength==0&&run.shards[0].uses==1,"Normal Bloodstone activates once; bonus waits for an actual Strength gain");
            loaded=SaveService.Load();PersistenceCheck(loaded?.combatCheckpoint?.state.activeShardId=="bloodstone"&&loaded.shards[0].uses==1,"Fate Shard inventory and applied effect share one checkpoint");

            foreach(var afterAction in new[]{false,true})
            {
                PreparePersistenceBattle("hex_strike");combat.intent=IntentKind.Attack;combat.intentValue=9;combat.enemy.strength=2;combat.player.vulnerable=1;combat.EndPlayerTurn();
                if(afterAction)combat.ResolveEnemyTurn();
                var baseline=combat.CaptureCheckpoint();baseline.TryRestore(out var continued,out _);continued.EndTurn();
                PersistenceCheck(SaveCombatCheckpoint(),"Enemy-phase checkpoint writes: "+combat.phase);
                CopyRun(SaveService.Load());RestoreRunStage();yield return WaitForCombatQueue();
                PersistenceCheck(CheckpointJson(combat)==CheckpointJson(continued),"Resume executes only the remaining enemy-turn steps: "+(afterAction?"after action":"before action"));
                PersistenceCheck(combat.player.hp==184,"Resumed enemy attack deals its modified damage exactly once");
            }

            foreach(var victory in new[]{true,false})
            {
                PreparePersistenceBattle("hex_strike");if(victory)combat.enemy.hp=0;else combat.player.hp=0;combat.phase=CombatPhase.Finished;
                var before=victory?profile.enemiesDefeated:profile.losses;
                PersistenceCheck(SaveCombatCheckpoint(),"Finished combat can be checkpointed before its result screen");
                var terminalSave=File.ReadAllText(SaveService.PathName);
                SaveCombatAndReturnToMenu();CopyRun(SaveService.Load());RestoreRunStage();yield return WaitForCombatQueue();
                PersistenceCheck(victory?screen==ScreenMode.Reward&&run.gold==75&&run.pendingCombatGold==18&&!run.combatGoldClaimed:screen==ScreenMode.RunResult&&!SaveService.HasRun,"Resume reaches the correct terminal result with rewards awaiting collection: "+(victory?"victory":"defeat"));
                var after=victory?profile.enemiesDefeated:profile.losses;PersistenceCheck(after==before+1,"Terminal statistics apply once");
                // Simulate interruption between committing profile statistics and the run transition.
                AtomicSaveFile.TryWrite(SaveService.PathName,terminalSave,out _);profile=ProfileService.Load();
                if(victory)
                {
                    CopyRun(SaveService.Load());RestoreRunStage();yield return WaitForCombatQueue();
                    PersistenceCheck(profile.enemiesDefeated==after,"Persisted encounter receipt prevents duplicate result statistics");
                    PersistenceCheck(run.gold==75&&run.pendingCombatGold==18&&!run.combatGoldClaimed,"Terminal recovery does not duplicate or automatically claim gold");
                    ClaimCombatGold(Vector2.zero);ClaimCombatGold(Vector2.zero);
                    PersistenceCheck(run.gold==93&&run.combatGoldClaimed,"Click-to-collect reward adds gold once, even on a duplicate click");
                }
                else PersistenceCheck(SaveService.Load()==null&&!SaveService.HasRun&&profile.losses==after,"A defeat receipt blocks stale-backup resurrection and duplicate losses");
            }

            PreparePersistenceBattle("hex_strike");verificationSavesAllowed=false;run.stage=RunStage.RelicReward;run.ClearCombatCheckpoint();run.elapsedSeconds=321;
            var beforeWin=profile.wins;var beforeFinish=JsonUtility.ToJson(run);CompleteRun();CompleteRun();
            PersistenceCheck(profile.wins==beforeWin+1,"Completing the same run twice cannot add two wins");
            AtomicSaveFile.TryWrite(SaveService.PathName,beforeFinish,out _);
            PersistenceCheck(SaveService.Load()==null&&!SaveService.HasRun,"Completed victory cannot be reopened from a stale active-run backup");

            PreparePersistenceBattle("hex_strike");var legacy=JsonUtility.ToJson(run).Replace("\"saveFormat\":4,","").Replace("\"saveFormat\":3,","").Replace("\"saveFormat\":2,","");
            Directory.CreateDirectory(SaveService.DirectoryPath);File.WriteAllText(SaveService.PathName,legacy);
            loaded=SaveService.Load();PersistenceCheck(loaded!=null&&!loaded.hasCombatCheckpoint,"Pre-checkpoint saves remain loadable");
            if(loaded!=null){CopyRun(loaded);RestoreRunStage();PersistenceCheck(run.hasCombatCheckpoint&&combat.turn==1,"Legacy encounter upgrades to exact checkpoint saving after it starts");}

            PreparePersistenceBattle("hex_strike");combat.energy=7;PersistenceCheck(SaveCombatCheckpoint(),"Write recovery baseline");combat.energy=6;PersistenceCheck(SaveCombatCheckpoint(),"Write next atomic generation");
            var primary=SaveService.PathName;var lastGood=File.ReadAllText(primary+".bak");File.WriteAllText(primary,"{ deliberately interrupted verification write");
            loaded=SaveService.Load();PersistenceCheck(loaded?.combatCheckpoint?.state.energy==7&&!string.IsNullOrEmpty(SaveService.RecoveryNotice),"Malformed primary recovers validated backup with a visible notice");
            PersistenceCheck(loaded!=null&&SaveService.Save(loaded)&&File.ReadAllText(primary+".bak")==lastGood,"Recovery repair preserves the last known-good backup");
            var untouched=File.ReadAllText(primary);
            using(var locked=new FileStream(primary+".tmp",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
            {PersistenceCheck(!SaveService.Save(loaded)&&File.ReadAllText(primary)==untouched,"A locked destination reports failure and leaves the previous save intact");}
            PersistenceCheck(SaveService.Clear()&&!SaveService.HasRun&&SaveService.Load()==null,"Completed-run tombstone cannot resurrect an active backup");
            verificationSavesAllowed=false;
            PreparePersistenceBattle("clear_mind");verificationSavesAllowed=false;
            yield return new WaitForSecondsRealtime(.9f);QueueCardPlay(handViews[combat.hand[2].instanceId]);yield return WaitForChoiceUI();
            Debug.Log($"[Gilded Fate Persistence] {persistenceChecks} checks · {persistenceFailures} failures · Unity serializer, actual save files, choice UI and exact continuation");
        }
    }
}
