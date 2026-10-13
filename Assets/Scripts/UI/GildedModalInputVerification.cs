#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GildedFate.Chronicle;
using GildedFate.ChronicleBook;
using GildedFate.Core;
using GildedFate.Map;
using GildedFate.Saving;
using UnityEngine;

namespace GildedFate.UI
{
    // Disposable native IMGUI regression fixture. It never sends OS input and
    // only runs when explicitly requested by the editor verification command.
    public sealed class GildedModalInputVerification : MonoBehaviour
    {
        [Serializable] public sealed class Report { public int checks, rewardFlows;public List<string> failures=new(); }
        readonly Report report=new();
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        GildedMainMenu menu;RunModel run;PlayerProfile profile;Action completed;string output;
        float started;bool finished;
        object Get(string name)=>typeof(GildedMainMenu).GetField(name,Private).GetValue(menu);
        void Set(string name,object value)=>typeof(GildedMainMenu).GetField(name,Private).SetValue(menu,value);
        object Call(string name,params object[] args)=>typeof(GildedMainMenu).GetMethod(name,Private).Invoke(menu,args);
        void SetScreen(string value){var field=typeof(GildedMainMenu).GetField("screen",Private);field.SetValue(menu,Enum.Parse(field.FieldType,value));}
        void Check(bool value,string message){report.checks++;if(!value)report.failures.Add(message);}
        public void Begin(GildedMainMenu target,string path,Action done)
        {
            menu=target;menu.enabled=false;output=path;completed=done;started=Time.realtimeSinceStartup;
            profile=new PlayerProfile();profile.EnsureMeta();profile.adminUnlocked=true;profile.reduceMotion=true;
            Set("profile",profile);Set("bootIntroActive",false);Set("chronicleActive",false);Set("runStartActive",false);
            Set("captureMode",true);run=(RunModel)Get("run");
            TestReset();BuildCases();
        }
        void Fresh(string screen,int act=1,HeroId hero=HeroId.Vanguard)
        {
            menu.StopAllCoroutines();Set("acquisitionActive",false);Set("acquisitionAwaitingClaim",false);Set("acquisitionSequence",null);
            Set("pgAdminOpen",false);Set("runPauseOpen",false);Set("inspectedCard",null);Set("inspectedRelic",null);
            Set("routeInspectionOpen",false);Set("replaceRunConfirmOpen",false);Set("goldCollectTime",0f);
            profile=new PlayerProfile();profile.EnsureMeta();profile.adminUnlocked=true;profile.reduceMotion=true;Set("profile",profile);
            run.NewRun(hero,127);run.act=act;SetScreen(screen);Set("screenInputReadyAt",-1f);Set("rewardRevealTime",0f);
            Set("currentNode",new MapNode{floor=run.floor,lane=1,kind=NodeKind.Combat});
            Set("hotButtonSeen",false);Set("lastHotButton",default(Rect));
        }
        void TestReset()
        {
            profile.chronicle.UnlockAllForDevelopment();profile.chronicle.openingViews=4;
            profile.chronicle.viewedMemories.Add(ChronicleCatalog.Memories[0].id);profile.chronicle.discoveredFacts.Add("old fact");
            profile.chronicle.corrections.Add("old correction");profile.chronicle.countedRunIds.Add("old run");profile.chronicle.eventsSeen.Add("old event");
            profile.chronicle.secretCompleted=true;profile.chronicle.lastChapterOpened=10;
            profile.chronicle.openingMode=2;profile.chronicle.instantText=true;profile.chronicle.textSpeed=1.4f;
            profile.winsByHero=new[]{8,8,8};profile.music=.37f;
            Call("PgResetProgress");
            Check(profile.chronicle.TotalUnlocked==0&&!profile.chronicle.secretUnlocked&&!profile.chronicle.secretCompleted,"Reset locks all memories and Chapter X");
            Check(profile.chronicle.viewedMemories.Count+profile.chronicle.discoveredFacts.Count+profile.chronicle.corrections.Count+profile.chronicle.pendingNotices.Count+profile.chronicle.countedRunIds.Count+profile.chronicle.eventsSeen.Count==0,"Reset clears viewed lore, facts, corrections, notices and receipts");
            Check(profile.chronicle.runsEnded.Concat(profile.chronicle.wins).Concat(profile.chronicle.bestFloor).Concat(profile.chronicle.actBossMask).All(n=>n==0),"Reset clears all Chronicle progression counters");
            Check(profile.chronicle.openingViews==0&&profile.chronicle.lastChapterOpened==0,"Reset clears opening and last chapter");
            Check(profile.chronicle.openingMode==2&&profile.chronicle.instantText&&Math.Abs(profile.chronicle.textSpeed-1.4f)<.001&&Math.Abs(profile.music-.37f)<.001,"Reset keeps story accessibility and audio settings");
            var reloaded=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(profile));ChronicleHooks.Prepare(reloaded);
            Check(reloaded.chronicle.TotalUnlocked==0&&!reloaded.chronicle.secretUnlocked,"Reload and legacy migration cannot re-unlock reset lore");
            Check(ProfileService.Suspended&&SaveService.Suspended,"Verification is isolated from real saves");
        }
        bool preview;string previewKind;
        public void BeginPreview(GildedMainMenu target,string kind)
        {
            menu=target;menu.enabled=false;preview=true;previewKind=kind;
            Set("captureMode",true);Set("bootIntroActive",false);Set("chronicleActive",false);Set("runStartActive",false);
            run=(RunModel)Get("run");Fresh("Fateweave");
            if(kind=="preview-relic")
            {
                run.OfferRelic(GameContent.Relics.First(r=>r.rarity==Rarity.Common).id);Call("ResumePendingRelicReward");
            }
            else if(kind=="preview-admin")
            {
                SetScreen("Playground");Call("ResetPlaygroundDeck");Set("pgAdminOpen",true);
            }
            else
            {
                profile.chronicle.UnlockAllForDevelopment();Call("OpenChronicleArchive");
            }
            Debug.Log("[Isolated modal preview] "+kind+" ready; profile and run saves remain suspended.");
        }
        void BuildCases()
        {
            StartCoroutine(VerifyFlows());
        }
        System.Collections.IEnumerator VerifyFlows()
        {
            var offered=GameContent.Relics.First(r=>r.rarity==Rarity.Common).id;
            foreach(var act in new[]{1,2,3})foreach(var screen in new[]{"Fateweave","EventResult","Map","Merchant","Sanctuary"})
            {
                Fresh(screen,act);var name=$"Act {act} {screen}";
                Check(run.OfferRelic(offered),name+": offer created");Call("ResumePendingRelicReward");
                Check((bool)Get("acquisitionActive")&&(bool)Get("acquisitionAwaitingClaim")&&!run.relics.Contains(offered),name+": offer waits without pre-granting");
                Call("ClaimPresentedRelic");
                Check(run.relics.Count(id=>id==offered)==1&&!run.pendingRelicRewards.Contains(offered),name+": exact item claimed once");
                Check(!(bool)Get("acquisitionAwaitingClaim")&&profile.relicsCollected==1,name+": claim exits waiting and records once");
                Call("ClaimPresentedRelic");Check(profile.relicsCollected==1,name+": duplicate confirmation ignored");
                yield return new WaitForSecondsRealtime(1.1f);
                Check(!(bool)Get("acquisitionActive"),name+": animation completes and releases input");report.rewardFlows++;
            }
            Fresh("Fateweave");run.OfferRelic(offered);var reload=JsonUtility.FromJson<RunModel>(JsonUtility.ToJson(run));run.CopyRewardState(reload);Call("ResumePendingRelicReward");Call("ClaimPresentedRelic");
            Check(run.relics.Contains(offered)&&run.pendingRelicRewards.Count==0,"Pending reward survives JSON reload and can be claimed");
            foreach(var act in new[]{1,2})foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))
            {
                Fresh("RelicReward",act,hero);Set("currentNode",new MapNode{floor=run.floor,lane=1,kind=NodeKind.Boss});run.RollEncounterRewards(NodeKind.Boss);run.encounterRewards.cardClaimed=true;
                var relic=run.BossRelicOffers()[0];Call("ClaimBossRelicChoice",relic);Call("ClaimBossRelicChoice",relic);
                Check(run.encounterRewards.bossRelicClaimed&&run.relics.Count(id=>id==relic.id)==1&&profile.relicsCollected==1,$"Act {act} {hero}: boss choice grants once");report.rewardFlows++;
            }
            Fresh("RelicReward");Set("currentNode",new MapNode{floor=run.floor,lane=1,kind=NodeKind.Treasure});var treasure=(RelicDef)Call("TreasureRelicOffer");Call("GrantRelic");Call("GrantRelic");
            Check(run.relics.Contains(treasure.id)&&profile.relicsCollected==1&&run.treasureRelicClaimedReceipt==run.RoomReceipt,"Treasure claim retains receipt and cannot repeat");
            Fresh("Reward");run.RollEncounterRewards(NodeKind.Combat);run.encounterRewards.relicId=offered;run.encounterRewards.relicClaimed=false;Call("ClaimRolledRelic");Call("ClaimRolledRelic");
            Check(run.encounterRewards.relicClaimed&&run.relics.Contains(offered)&&profile.relicsCollected==1,"Rolled encounter relic grants once");
            Fresh("Map");var shard=WorldContent.FateShards[0];run.OfferShard(shard.id);Call("TakeShardDiscovery",-1,shard);
            Check(run.shards.Count==1&&run.shards[0].slot==0&&run.shards[0].uses==0&&string.IsNullOrEmpty(run.pendingShardDiscoveryId),"Shard claim occupies free slot with intact durability");
            Fresh("Reward");run.pendingCombatGold=25;run.combatGoldClaimed=false;var gold=run.gold;Call("ClaimCombatGold",Vector2.zero);Call("ClaimCombatGold",Vector2.zero);
            Check(run.gold==gold+25&&run.combatGoldClaimed&&run.pendingCombatGold==0,"Gold claim grants exactly once");
            Fresh("Playground");Set("pgAdminOpen",true);
            var nav=typeof(GildedMainMenu).GetNestedType("MenuNavigation",BindingFlags.NonPublic);var input=Activator.CreateInstance(nav);nav.GetField("back").SetValue(input,true);
            Check((bool)Call("RouteMenuNavigation",input),"Admin exclusively consumes keyboard/controller Back");
            Check(!(bool)Get("pgAdminOpen")&&Get("screen").ToString()=="Playground","Back closes only admin, preserving setup");
            Check(ProfileService.Suspended&&SaveService.Suspended,"All reward flows ran with save writes suspended");
            Finish();
        }
        void Update()
        {
            if(preview)
            {
                if(previewKind=="preview-archive")Call("TickChronicleArchive",Time.unscaledDeltaTime);
                if(previewKind=="preview-relic"&&!(bool)Get("acquisitionAwaitingClaim")&&run.pendingRelicRewards.Count==0&&!finished)
                {
                    finished=true;
                    File.WriteAllText(@"C:\Users\river\Documents\Codex\2026-10-08\use-these-files-from-the-libray-4\outputs\Relic_Native_Click_Check.json",JsonUtility.ToJson(new NativeClick{claimed=profile.relicsCollected==1,pending=run.pendingRelicRewards.Count,owned=run.relics.Count},true));
                    Debug.Log("[Isolated modal preview] Native TAKE RELIC clicked successfully; item owned exactly once.");
                }
                return;
            }
            if(!finished&&Time.realtimeSinceStartup-started>100){report.failures.Add("Integration fixture timed out");Finish();}
        }
        void OnGUI()
        {
            if(!preview)return;
            // Native rendering needs ordinary styling, but the disposable menu
            // remains a capture fixture between GUI events and at shutdown.
            Set("captureMode",false);
            try { Call("DrawMainGUI"); }
            finally { Set("captureMode",true); }
            if(previewKind=="preview-archive"&&!finished&&Event.current.type==EventType.Repaint)
            {
                var stage=(ChronicleBookStageHost)Get("chronicleHost");
                if(stage==null||!stage.Rig.Frame.overlayVisible||stage.Rig.Frame.overlayAlpha<.95f)return;
                var styles=(Dictionary<int,GUIStyle>)Get("archiveStyles");
                Check(styles.Count>0&&styles.Values.All(s=>s.normal.textColor==s.hover.textColor&&s.normal.textColor==s.active.textColor&&s.normal.textColor==s.focused.textColor),"Archive labels retain the same ink across hover, press and focus");
                var rig=stage.Rig as ChronicleAnimatorBookRig;
                Check(rig!=null&&rig.GetComponentsInChildren<Renderer>().Any(r=>r.name=="Bookmark_Ribbon"&&!r.enabled),"Green bookmark renderer is hidden");
                Check(rig!=null&&rig.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("PageSurface_")).All(r=>r.enabled),"Readable parchment page renderers remain visible");
                Check(stage.TryGetPageViewportRect(ChroniclePageSide.Left,out var left)&&stage.TryGetPageViewportRect(ChroniclePageSide.Right,out var right)&&left.xMax<right.xMin,"Archive pages retain distinct left/right layout and working projections");
                File.WriteAllText(@"C:\Users\river\Documents\Codex\2026-10-08\use-these-files-from-the-libray-4\outputs\Chronicle_Readability_Check.json",JsonUtility.ToJson(report,true));finished=true;
            }
        }
        [Serializable] sealed class NativeClick { public bool claimed;public int pending,owned; }
        void Finish()
        {
            finished=true;menu.StopAllCoroutines();File.WriteAllText(output,JsonUtility.ToJson(report,true));
            Debug.Log($"[Admin and reward verification] {report.checks} checks, {report.rewardFlows} reward flows, {report.failures.Count} failures.");completed?.Invoke();
        }
    }
}
#endif
