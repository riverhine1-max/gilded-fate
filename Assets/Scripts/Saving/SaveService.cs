using System;
using System.IO;
using System.Linq;
using GildedFate.Core;
using GildedFate.Map;
using UnityEngine;

namespace GildedFate.Saving
{
    public static class SaveService
    {
        public static string LastError {get;private set;}="";
        public static string RecoveryNotice {get;private set;}="";
        private static string cachedStamp="";
        private static bool cachedHasRun;
        private static bool preserveRecoveredBackup;
        public static string DirectoryPath
        {
            get
            {
                // Capture builds cannot read or overwrite the user's actual save/profile.
                var args=Environment.GetCommandLineArgs();
                for(var i=0;i<args.Length-1;i++)if(args[i]=="-gfCapturePath")return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[i+1])),"save-fixtures",Path.GetFileNameWithoutExtension(args[i+1]));
                return Application.persistentDataPath;
            }
        }
        public static string PathName=>Path.Combine(DirectoryPath,"gilded_fate_run.json");
        public static bool HasRun
        {
            get
            {
                try
                {
                    var stamp=PathName+File.GetLastWriteTimeUtc(PathName).Ticks+File.GetLastWriteTimeUtc(PathName+".bak").Ticks;
                    if(stamp!=cachedStamp){cachedHasRun=Load()!=null;cachedStamp=stamp;}
                    return cachedHasRun;
                }
                catch(Exception error) when(error is IOException||error is UnauthorizedAccessException)
                {LastError="Could not access the saved run: "+error.Message;return false;}
            }
        }
        public static bool Save(RunModel run)
        {
            if(run==null)return false;
            if(!run.closed){run.EnsureCardInstances();run.SyncLegacyDeck();}
            run.saveFormat=4;
            if(!Validate(run)){LastError="Cannot save an incomplete run. Your previous save is intact.";Debug.LogWarning(LastError);return false;}
            if(!AtomicSaveFile.TryWrite(PathName,JsonUtility.ToJson(run,true),out var error,preserveRecoveredBackup)){LastError="Save failed: "+error;Debug.LogWarning(LastError);return false;}
            LastError="";cachedStamp="";preserveRecoveredBackup=false;return true;
        }
        public static RunModel Load()
        {
            RecoveryNotice="";
            if(!AtomicSaveFile.TryRead(PathName,ParseRun,Validate,out var loaded,out var recovered,out var error))
            {
                LastError=string.IsNullOrEmpty(error)?"":"Could not load this run. The save files were preserved. "+error;
                return null;
            }
            LastError="";
            preserveRecoveredBackup=recovered;
            if(recovered)RecoveryNotice="Recovered the previous safe save after an interrupted or damaged save.";
            if(loaded.closed)return null;
            loaded.upgradedCards??=new();loaded.relics??=new();loaded.consumables??=new();loaded.merchantSold??=new();loaded.cards??=new();loaded.shards??=new();loaded.fateweaveSelections??=new();loaded.temporaryMultiCombatStatuses??=new();loaded.fateweaveOffers??=new();loaded.bindingOffers??=new();loaded.pendingCardOfferIds??=new();loaded.pendingSelectedCardIds??=new();loaded.EnsureEventState();
            if(string.IsNullOrEmpty(loaded.runId))loaded.runId="legacy-"+loaded.seed;
            loaded.EnsureCardInstances();
            if(ProfileService.Load().completedRunIds?.Contains(loaded.runId)==true){RecoveryNotice="This run was already completed.";return null;}
            loaded.saveFormat=4;
            return loaded;
        }
        public static bool Clear()=>Save(new RunModel{closed=true,saveFormat=4});
        private static RunModel ParseRun(string json)
        {
            var run=JsonUtility.FromJson<RunModel>(json);
            if(run!=null&&json.IndexOf("\"saveFormat\"",StringComparison.Ordinal)<0)run.saveFormat=0;
            return run;
        }
        private static bool Validate(RunModel run)
        {
            if(run==null||run.saveFormat<0||run.saveFormat>4)return false;
            if(run.closed)return true;
            if(!run.HasValidRewardState()||!run.HasValidMerchantStock())return false;
            if(run.perfectedSelectionStep<0||run.perfectedSelectionStep>3||run.perfectedSelectionPending&&(!run.relics.Contains("perfected_thread")||run.perfectedSelectionStep>=3))return false;
            var validLegacyMap=run.saveFormat<=2&&run.nodes!=null&&run.nodes.Count==42;
            if(run.deck==null||run.deck.Count==0||run.nodes==null||!validLegacyMap&&!run.HasValidMap()||run.hp<0||run.maxHp<=0||run.hp>run.maxHp||run.gold<0||run.floor<0||run.floor>RunModel.FloorCount)return false;
            if(!Enum.IsDefined(typeof(HeroId),run.hero)||!Enum.IsDefined(typeof(RunStage),run.stage))return false;
            if(run.deck.Any(id=>!Array.Exists(GameContent.Cards,c=>c.id==id)))return false;
            if(run.saveFormat>=3)
            {
                if(run.cards==null||run.cards.Count==0||run.cards.Any(c=>c==null||string.IsNullOrEmpty(c.persistentId)||GameContent.Find(c.cardId)==null||!Enum.IsDefined(typeof(SpecialModificationKind),c.specialModificationKind)))return false;
                if(run.cards.Select(c=>c.persistentId).Distinct().Count()!=run.cards.Count)return false;
                if(run.cards.Any(c=>c.perfectedGrowth<0||c.perfectedCostReduction<0||c.perfectedCostReduction>20))return false;
                if(run.cards.Any(c=>(c.specialModificationKind==SpecialModificationKind.None)!=string.IsNullOrEmpty(c.specialModification)))return false;
                if(run.shards==null||run.shards.Count>3||run.shards.Any(s=>s==null||WorldContent.FateShards.All(d=>d.id!=s.id)||s.uses<0||s.uses>3||s.uses==3&&!s.active||s.activeFractured&&!s.active))return false;
                if(run.shards.Count(s=>s.active)>1)return false;
                if(run.fateweaveSelections==null||run.temporaryMultiCombatStatuses==null||run.fateweaveOffers==null||run.bindingOffers==null||run.pendingCardOfferIds==null||run.pendingSelectedCardIds==null)return false;
                if(run.fateweaveSelections.Any(id=>WorldContent.Fateweaves.All(f=>f.id!=id))||run.bindingOffers.Any(id=>WorldContent.Bindings.All(b=>b.id!=id))||run.fateweaveOffers.Any(id=>WorldContent.Fateweaves.All(f=>f.id!=id)))return false;
            }
            if(run.saveFormat>=4)
            {
                if(run.seenEventIds==null||run.pendingEventOfferIds==null||run.pendingEventSelectionIds==null||run.pendingEventShardDecisions==null||run.temporaryEventEffects==null)return false;
                if(run.seenEventIds.Any(id=>EventContent.Find(id)==null)||run.temporaryEventEffects.Any(e=>e==null||string.IsNullOrEmpty(e.id)||e.combatsRemaining<1))return false;
                if(!Enum.IsDefined(typeof(EventSelectionKind),run.eventSelectionKind))return false;
                if(run.stage is RunStage.Event or RunStage.EventSelection or RunStage.EventResult)
                {
                    if(EventContent.Find(run.activeEventId)==null)return false;
                    if(run.stage==RunStage.EventSelection&&EventContent.FindChoice(run.activeEventId,run.pendingEventChoiceId)==null)return false;
                }
            }
            if(run.saveFormat>=2&&run.stage==RunStage.Combat&&!run.hasCombatCheckpoint)return false;
            if(run.stage==RunStage.Combat&&!run.nodes.Any(n=>n.floor==run.activeNodeFloor&&n.lane==run.activeNodeLane))return false;
            if(run.hasCombatCheckpoint)
            {
                if(run.stage!=RunStage.Combat||run.combatCheckpoint==null||!run.combatCheckpoint.TryRestore(out var state,out _))return false;
                if(state.EnemyIdAt(0)!=run.activeEnemyId||state.hero!=run.hero)return false;
            }
            return true;
        }
    }
}
